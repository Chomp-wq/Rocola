// EL CATALOGO DE LA ROCOLA EN LA DEMO (sistema/catalogo, contrato «I8» formato 1; plan en sistema/rocola/Xp/PLAN_PARED_GENEROS.md).
// La pared de hasta 500 paginas por letra (18 discos cada una = 9 000) junta tus discos con los del catalogo, por genero, luego artista
// por abecedario y, de cada artista, primero los tuyos y despues los del catalogo del mas al menos escuchado. Mirarla NO gasta ninguna
// peticion de la API de Spotify (portadas del Cover Art Archive); Spotify solo se usa al abrir un disco y para tocar (Soloist).
use std::collections::{HashMap, HashSet};
use std::io::Read;

use serde_json::{json, Value};

pub const URL_BASE: &str = "https://github.com/Chomp-wq/Rocola/releases/download/catalogo/";
pub const TOPE_LETRA: usize = 9_000; // 500 paginas de 18

pub struct DiscoCat { pub nombre: String, pub anio: String, pub spotify: String, pub mbid: String, pub portada: bool }
pub struct ArtistaCat { pub nombre: String, pub genero: String, pub discos: Vec<DiscoCat> }
pub struct Catalogo {
    pub version: String,
    pub letras: HashMap<String, Vec<ArtistaCat>>, // en el orden del archivo: del mas al menos famoso
    pub genero_de: HashMap<String, String>,       // plano(nombre del artista) -> genero de XP
    pub total_genero: HashMap<String, usize>,     // discos del catalogo por genero (para el orden de los generos)
    pub artistas: usize,
    pub discos: usize,
}

/// Tus discos (los guardados en tu cuenta) y tus listas, como los manda la pagina.
#[derive(Clone, serde::Deserialize)]
pub struct Mio {
    pub tipo: String, // album | lista
    pub id: String,
    pub nombre: String,
    pub artista: String,
    #[serde(default)]
    pub img: String,
    #[serde(default)]
    pub grande: String,
}

fn sin_acento(c: char) -> char {
    match c {
        'á' | 'à' | 'ä' | 'â' | 'ã' | 'å' => 'a',
        'é' | 'è' | 'ë' | 'ê' => 'e',
        'í' | 'ì' | 'ï' | 'î' => 'i',
        'ó' | 'ò' | 'ö' | 'ô' | 'õ' | 'ø' => 'o',
        'ú' | 'ù' | 'ü' | 'û' => 'u',
        'ñ' => 'n',
        'ç' => 'c',
        'ý' | 'ÿ' => 'y',
        _ => c,
    }
}
/// Para comparar nombres: minusculas, sin acentos, solo letras y numeros.
pub fn plano(s: &str) -> String {
    s.to_lowercase().chars().map(sin_acento).filter(|c| c.is_alphanumeric()).collect()
}
/// La letra de XP (FirstLetter): el primer caracter sin espacios, sin acento y en mayuscula; si no es de la A a la Z, «#».
pub fn letra_xp(nombre: &str) -> String {
    match nombre.trim().chars().next().map(|c| sin_acento(c.to_lowercase().next().unwrap_or(c))) {
        Some(c) if c.is_ascii_alphabetic() => c.to_ascii_uppercase().to_string(),
        _ => "#".to_string(),
    }
}

pub fn lee(gz: &[u8]) -> Result<Catalogo, String> {
    let mut texto = String::new();
    flate2::read::GzDecoder::new(gz).read_to_string(&mut texto).map_err(|e| format!("no se pudo descomprimir: {e}"))?;
    let mut lineas = texto.lines();
    let cab: Vec<&str> = lineas.next().unwrap_or("").split('\t').collect();
    if cab.first() != Some(&"#rocola-catalogo") || cab.get(1) != Some(&"1") {
        return Err("no es un catalogo de la rocola de formato 1".into());
    }
    let mut c = Catalogo {
        version: cab.get(2).unwrap_or(&"").to_string(), letras: HashMap::new(), genero_de: HashMap::new(),
        total_genero: HashMap::new(), artistas: 0, discos: 0,
    };
    let mut letra = String::new();
    for l in lineas {
        let f: Vec<&str> = l.split('\t').collect();
        match f.first().copied() {
            Some("L") if f.len() >= 2 => { letra = f[1].to_string(); c.letras.entry(letra.clone()).or_default(); }
            Some("A") if f.len() >= 3 => {
                c.genero_de.entry(plano(f[1])).or_insert_with(|| f[2].to_string());
                c.letras.entry(letra.clone()).or_default().push(ArtistaCat { nombre: f[1].into(), genero: f[2].into(), discos: vec![] });
                c.artistas += 1;
            }
            Some("D") if f.len() >= 6 => {
                if let Some(a) = c.letras.get_mut(&letra).and_then(|v| v.last_mut()) {
                    *c.total_genero.entry(a.genero.clone()).or_default() += 1;
                    a.discos.push(DiscoCat { nombre: f[1].into(), anio: f[2].into(), spotify: f[3].into(), mbid: f[4].into(), portada: f[5] == "1" });
                    c.discos += 1;
                }
            }
            _ => {}
        }
    }
    if c.discos == 0 { return Err("el catalogo vino vacio".into()); }
    Ok(c)
}

// El peso por familia de genero del catalogo (FACTOR_GENERO de arma_catalogo.py), x10: pensado para un bar de Mexico.
fn preferencia(g: &str) -> u32 {
    match g { "Regional" => 30, "Tropical" | "Bolero" => 25, "Latino" => 22, "Reggaeton" => 20, "Flamenco" => 15, _ => 10 }
}
/// El orden de los generos, el mismo en todas las letras: primero los de los que tienes mas discos; a igualdad (o sin discos
/// tuyos), los de la familia que pesa mas en el catalogo, y luego los mas grandes; «Otros» al final.
fn orden_generos(cat: Option<&Catalogo>, mios: &[Mio]) -> HashMap<String, usize> {
    let mut puntos: HashMap<String, (usize, usize)> = HashMap::new();
    if let Some(c) = cat {
        for (g, n) in &c.total_genero { puntos.entry(g.clone()).or_default().1 = *n; }
        for m in mios.iter().filter(|m| m.tipo == "album") {
            if let Some(g) = c.genero_de.get(&plano(&m.artista)) { puntos.entry(g.clone()).or_default().0 += 1; }
        }
    }
    let mut v: Vec<(String, (usize, usize))> = puntos.into_iter().collect();
    v.sort_by(|a, b| b.1 .0.cmp(&a.1 .0).then(preferencia(&b.0).cmp(&preferencia(&a.0))).then(b.1 .1.cmp(&a.1 .1)).then(a.0.cmp(&b.0)));
    let mut r: HashMap<String, usize> = v.into_iter().enumerate().map(|(i, (g, _))| (g, i)).collect();
    r.insert("Otros".into(), usize::MAX);
    r
}

pub fn portada(mbid: &str, tam: u32) -> String { format!("https://coverartarchive.org/release-group/{mbid}/front-{tam}") }

/// Las letras que tienen algo (para la barra de letras).
pub fn letras_con(cat: Option<&Catalogo>, mios: &[Mio]) -> Vec<String> {
    let mut s: HashSet<String> = HashSet::new();
    if let Some(c) = cat { for (l, v) in &c.letras { if !v.is_empty() { s.insert(l.clone()); } } }
    for m in mios {
        if m.tipo == "lista" { s.insert("SPOTIFY".into()); } else { s.insert(letra_xp(&m.artista)); }
    }
    let mut v: Vec<String> = s.into_iter().collect();
    v.sort();
    v
}

/// Una letra entera de la pared. `ids`: mbid -> id de disco de Spotify ya buscado («-» = Spotify no lo tiene).
pub fn arma_letra(cat: Option<&Catalogo>, mios: &[Mio], ids: &HashMap<String, String>, letra: &str) -> Vec<Value> {
    if letra == "SPOTIFY" {
        return mios.iter().filter(|m| m.tipo == "lista").map(|m| json!({
            "tipo": "lista", "id": m.id, "nombre": m.nombre, "artista": m.artista, "genero": "", "img": m.img, "grande": m.grande, "mio": true,
        })).collect();
    }
    let orden = orden_generos(cat, mios);
    let rango = |g: &str| *orden.get(g).unwrap_or(&(usize::MAX - 1));
    // (rango del genero, artista plano, 0 = tuyo / 1 = catalogo, posicion) -> casilla
    let mut filas: Vec<((usize, String, u8, usize), Value)> = vec![];
    let mut ya: HashSet<String> = HashSet::new();
    for (i, m) in mios.iter().filter(|m| m.tipo == "album" && letra_xp(&m.artista) == letra).enumerate() {
        let g = cat.and_then(|c| c.genero_de.get(&plano(&m.artista))).cloned().unwrap_or_else(|| "Otros".into());
        ya.insert(format!("{}|{}", plano(&m.artista), plano(&m.nombre)));
        ya.insert(m.id.clone());
        filas.push(((rango(&g), plano(&m.artista), 0, i), json!({
            "tipo": "album", "id": m.id, "nombre": m.nombre, "artista": m.artista, "genero": g, "img": m.img, "grande": m.grande, "mio": true,
        })));
    }
    if let Some(c) = cat {
        let mut cupo = TOPE_LETRA.saturating_sub(filas.len());
        let mut pos = 0usize;
        'artistas: for a in c.letras.get(letra).map(|v| v.as_slice()).unwrap_or(&[]) {
            for d in &a.discos {
                if cupo == 0 { break 'artistas; }
                let id = if !d.spotify.is_empty() { d.spotify.clone() } else { ids.get(&d.mbid).cloned().unwrap_or_default() };
                if id == "-" { continue; } // ya se busco y Spotify no lo tiene
                if ya.contains(&format!("{}|{}", plano(&a.nombre), plano(&d.nombre))) || (!id.is_empty() && ya.contains(&id)) { continue; }
                pos += 1;
                cupo -= 1;
                filas.push(((rango(&a.genero), plano(&a.nombre), 1, pos), json!({
                    "tipo": "cat", "id": id, "mbid": d.mbid, "nombre": d.nombre, "artista": a.nombre, "genero": a.genero, "anio": d.anio,
                    "img": if d.portada { portada(&d.mbid, 250) } else { String::new() },
                    "grande": if d.portada { portada(&d.mbid, 500) } else { String::new() }, "mio": false,
                })));
            }
        }
    }
    filas.sort_by(|a, b| a.0.cmp(&b.0));
    filas.into_iter().map(|(_, v)| v).collect()
}

/// De los discos que dio la busqueda de Spotify, el que es: mismo nombre (o el nombre con «(Deluxe…)» detras) y el artista.
pub fn elige_disco(items: &[Value], nombre: &str, artista: &str) -> Option<(String, String, String)> {
    let (n, a) = (plano(nombre), plano(artista));
    let del_artista = |x: &Value| x["artists"].as_array().map(|v| v.iter().any(|y| plano(y["name"].as_str().unwrap_or("")) == a)).unwrap_or(false);
    let dato = |x: &Value| {
        let imgs = x["images"].as_array().cloned().unwrap_or_default();
        let img = |i: usize| imgs.get(i).and_then(|v| v["url"].as_str()).unwrap_or("").to_string();
        (x["id"].as_str().unwrap_or("").to_string(), img(0), img(imgs.len().saturating_sub(1).min(1)))
    };
    items.iter().find(|x| del_artista(x) && plano(x["name"].as_str().unwrap_or("")) == n)
        .or_else(|| items.iter().find(|x| del_artista(x) && !n.is_empty() && plano(x["name"].as_str().unwrap_or("")).starts_with(&n)))
        .map(dato).filter(|d| !d.0.is_empty())
}

pub fn codifica(s: &str) -> String {
    s.bytes().map(|b| match b {
        b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => (b as char).to_string(),
        _ => format!("%{b:02X}"),
    }).collect()
}

#[cfg(test)]
mod pruebas {
    use super::*;
    #[test]
    fn letras_y_planos() {
        assert_eq!(letra_xp("Ángeles Azules"), "A");
        assert_eq!(letra_xp("Ñandú"), "N");
        assert_eq!(letra_xp("¡Ay"), "#");
        assert_eq!(letra_xp("3 Gallos"), "#");
        assert_eq!(plano("Los Ángeles Azules!"), "losangelesazules");
    }
}

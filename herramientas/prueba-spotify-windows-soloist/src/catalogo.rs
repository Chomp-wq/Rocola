// EL CATALOGO DE LA ROCOLA EN LA DEMO (sistema/catalogo, contrato «I8» formato 1; plan en sistema/rocola/Xp/PLAN_PARED_GENEROS.md).
// La pared de hasta 500 paginas por letra (18 discos cada una = 9 000) junta tus discos con los del catalogo, por genero, luego los
// artistas del mas al menos famoso (la fama del catalogo: escuchas y oyentes de ListenBrainz, Wikipedias, pais) y, de cada artista,
// primero los tuyos y despues los del catalogo del mas al menos escuchado. Mirarla NO gasta ninguna
// peticion de la API de Spotify (portadas del Cover Art Archive); Spotify solo se usa al abrir un disco y para tocar (Soloist).
use std::collections::{HashMap, HashSet};
use std::io::Read;

use serde_json::{json, Value};

pub const URL_BASE: &str = "https://github.com/Chomp-wq/Rocola/releases/download/catalogo/";
pub const TOPE_LETRA: usize = 9_000; // 500 paginas de 18

pub struct DiscoCat { pub nombre: String, pub plano: String, pub anio: String, pub spotify: String, pub mbid: String, pub portada: bool }
pub struct ArtistaCat { pub nombre: String, pub plano: String, pub genero: String, pub fama: u32, pub discos: Vec<DiscoCat> }
pub struct Catalogo {
    pub version: String,
    pub letras: HashMap<String, Vec<ArtistaCat>>, // en el orden del archivo: del mas al menos famoso
    pub genero_de: HashMap<String, String>,       // plano(nombre del artista) -> genero de XP
    pub fama_de: HashMap<String, u32>,            // plano(nombre del artista) -> fama (0 a 1 000 000)
    pub total_genero: HashMap<String, usize>,     // discos del catalogo por genero (para el orden de los generos)
    pub artistas: usize,
    pub discos: usize,
}

/// Tus discos (los guardados en tu cuenta) y tus listas, como los manda la pagina.
#[derive(Clone, PartialEq, serde::Deserialize, serde::Serialize)]
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
        version: cab.get(2).unwrap_or(&"").to_string(), letras: HashMap::new(), genero_de: HashMap::new(), fama_de: HashMap::new(),
        total_genero: HashMap::new(), artistas: 0, discos: 0,
    };
    let mut letra = String::new();
    for l in lineas {
        let f: Vec<&str> = l.split('\t').collect();
        match f.first().copied() {
            Some("L") if f.len() >= 2 => { letra = f[1].to_string(); c.letras.entry(letra.clone()).or_default(); }
            Some("A") if f.len() >= 3 => {
                let fama = f.get(3).and_then(|x| x.parse::<u32>().ok()).unwrap_or(0);
                c.genero_de.entry(plano(f[1])).or_insert_with(|| f[2].to_string());
                c.fama_de.entry(plano(f[1])).or_insert(fama);
                c.letras.entry(letra.clone()).or_default().push(ArtistaCat { nombre: f[1].into(), plano: plano(f[1]), genero: f[2].into(), fama, discos: vec![] });
                c.artistas += 1;
            }
            Some("D") if f.len() >= 6 => {
                if let Some(a) = c.letras.get_mut(&letra).and_then(|v| v.last_mut()) {
                    *c.total_genero.entry(a.genero.clone()).or_default() += 1;
                    a.discos.push(DiscoCat { nombre: f[1].into(), plano: plano(f[1]), anio: f[2].into(), spotify: f[3].into(), mbid: f[4].into(), portada: f[5] == "1" });
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

/// La portada en el Cover Art Archive (PRUEBA_CAA cambia el servidor, solo para probar con uno falso).
pub fn portada(mbid: &str, tam: u32) -> String {
    let base = std::env::var("PRUEBA_CAA").unwrap_or_else(|_| "https://coverartarchive.org".into());
    format!("{base}/release-group/{mbid}/front-{tam}")
}
/// Un MBID tal como viene en el catalogo (8-4-4-4-12 en hexadecimal minuscula): lo unico que acepta /p/ del servidor.
pub fn es_mbid(m: &str) -> bool {
    m.len() == 36 && m.char_indices().all(|(i, c)| if [8, 13, 18, 23].contains(&i) { c == '-' } else { c.is_ascii_hexdigit() && !c.is_ascii_uppercase() })
}

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

fn casilla_mia(m: &Mio, g: &str) -> Value {
    json!({"tipo": if m.tipo == "lista" { "lista" } else { "album" }, "id": m.id, "nombre": m.nombre, "artista": m.artista, "genero": g,
           "img": m.img, "grande": m.grande, "mio": true})
}
// La portada va por el servidor de la demo (/p/MBID: la guarda en el disco la primera vez); la grande es «?t=500».
fn casilla_cat(a: &ArtistaCat, d: &DiscoCat, id: &str) -> Value {
    json!({"tipo": "cat", "id": id, "mbid": d.mbid, "nombre": d.nombre, "artista": a.nombre, "genero": a.genero, "anio": d.anio,
           "img": if d.portada { format!("/p/{}", d.mbid) } else { String::new() }})
}
/// El id de Spotify de un disco del catalogo: el del archivo, o el ya buscado («-» = Spotify no lo tiene; "" = sin buscar).
fn id_de(d: &DiscoCat, ids: &HashMap<String, String>) -> String {
    if !d.spotify.is_empty() { d.spotify.clone() } else { ids.get(&d.mbid).cloned().unwrap_or_default() }
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
    // (rango del genero, fama al reves, artista plano, 0 = tuyo / 1 = catalogo, posicion) -> casilla. Un artista tuyo que no esta en el
    // catalogo no tiene fama (0): va al final de su genero (que sera «Otros»).
    let fama = |a: &str| cat.and_then(|c| c.fama_de.get(&plano(a))).copied().unwrap_or(0);
    let mut filas: Vec<((usize, std::cmp::Reverse<u32>, String, u8, usize), Value)> = vec![];
    let mut ya: HashSet<String> = HashSet::new();
    for (i, m) in mios.iter().filter(|m| m.tipo == "album" && letra_xp(&m.artista) == letra).enumerate() {
        let g = cat.and_then(|c| c.genero_de.get(&plano(&m.artista))).cloned().unwrap_or_else(|| "Otros".into());
        ya.insert(format!("{}|{}", plano(&m.artista), plano(&m.nombre)));
        ya.insert(m.id.clone());
        let casilla = casilla_mia(m, &g);
        filas.push(((rango(&g), std::cmp::Reverse(fama(&m.artista)), plano(&m.artista), 0, i), casilla));
    }
    if let Some(c) = cat {
        let mut cupo = TOPE_LETRA.saturating_sub(filas.len());
        let mut pos = 0usize;
        'artistas: for a in c.letras.get(letra).map(|v| v.as_slice()).unwrap_or(&[]) {
            for d in &a.discos {
                if cupo == 0 { break 'artistas; }
                let id = id_de(d, ids);
                if id == "-" { continue; } // ya se busco y Spotify no lo tiene
                if ya.contains(&format!("{}|{}", plano(&a.nombre), plano(&d.nombre))) || (!id.is_empty() && ya.contains(&id)) { continue; }
                pos += 1;
                cupo -= 1;
                filas.push(((rango(&a.genero), std::cmp::Reverse(a.fama), plano(&a.nombre), 1, pos), casilla_cat(a, d, &id)));
            }
        }
    }
    filas.sort_by(|a, b| a.0.cmp(&b.0));
    filas.into_iter().map(|(_, v)| v).collect()
}

/// EL BUSCADOR AL MOMENTO (sin gastar nada de la API): artistas y discos del catalogo y tus discos y listas que tengan todas las
/// palabras escritas (sin acentos ni mayusculas). Manda la fama, con premio al nombre igual o que empieza igual.
pub fn busca(cat: Option<&Catalogo>, mios: &[Mio], ids: &HashMap<String, String>, q: &str) -> Value {
    let toks: Vec<String> = q.split_whitespace().map(plano).filter(|t| !t.is_empty()).collect();
    if toks.iter().map(|t| t.len()).sum::<usize>() < 2 { return json!({"artistas": [], "discos": []}); }
    let junto: String = toks.concat();
    let todas = |texto: &str| toks.iter().all(|t| texto.contains(t.as_str()));
    let premio = |p: &str| if p == junto { 600_000u64 } else if p.starts_with(&junto) { 300_000 } else if p.starts_with(toks[0].as_str()) { 150_000 } else { 0 };
    // tus discos y listas primero
    let mut discos: Vec<(u64, Value)> = vec![];
    let mut ya: HashSet<String> = HashSet::new();
    for m in mios {
        let (pn, pa) = (plano(&m.nombre), plano(&m.artista));
        if !toks.iter().all(|t| pn.contains(t.as_str()) || pa.contains(t.as_str())) { continue; }
        let g = cat.and_then(|c| c.genero_de.get(&pa)).cloned().unwrap_or_else(|| if m.tipo == "lista" { String::new() } else { "Otros".into() });
        ya.insert(format!("{pa}|{pn}"));
        ya.insert(m.id.clone());
        discos.push((2_000_000 + premio(&pn), casilla_mia(m, &g)));
    }
    let mut artistas: Vec<(u64, &ArtistaCat)> = vec![];
    if let Some(c) = cat {
        for a in c.letras.values().flatten() {
            let art_todas = todas(&a.plano);
            if art_todas { artistas.push((a.fama as u64 + premio(&a.plano), a)); }
            for d in &a.discos {
                // todas las palabras entre el disco y su artista, y al menos una en el disco (si no, cualquier disco del artista)
                if !toks.iter().all(|t| d.plano.contains(t.as_str()) || a.plano.contains(t.as_str())) { continue; }
                let en_disco = toks.iter().any(|t| d.plano.contains(t.as_str()));
                if !en_disco && !art_todas { continue; }
                let id = id_de(d, ids);
                if id == "-" || ya.contains(&format!("{}|{}", a.plano, d.plano)) || (!id.is_empty() && ya.contains(&id)) { continue; }
                let p = a.fama as u64 + if en_disco { premio(&d.plano) + if todas(&d.plano) { 100_000 } else { 0 } } else { 0 };
                discos.push((p, casilla_cat(a, d, &id)));
            }
        }
    }
    artistas.sort_by(|x, y| y.0.cmp(&x.0).then(x.1.nombre.cmp(&y.1.nombre)));
    discos.sort_by(|x, y| y.0.cmp(&x.0));
    let artistas: Vec<Value> = artistas.into_iter().take(12).map(|(_, a)| {
        let img = a.discos.iter().find(|d| d.portada).map(|d| format!("/p/{}", d.mbid)).unwrap_or_default();
        json!({"nombre": a.nombre, "genero": a.genero, "n": a.discos.len(), "img": img})
    }).collect();
    json!({"artistas": artistas, "discos": discos.into_iter().take(24).map(|x| x.1).collect::<Vec<_>>()})
}

/// La pared de un artista sin gastar nada de la API: tus discos suyos y los del catalogo (del mas al menos escuchado).
pub fn discos_de(cat: Option<&Catalogo>, mios: &[Mio], ids: &HashMap<String, String>, nombre: &str) -> Vec<Value> {
    let pn = plano(nombre);
    let g = cat.and_then(|c| c.genero_de.get(&pn)).cloned().unwrap_or_else(|| "Otros".into());
    let mut v = vec![];
    let mut ya: HashSet<String> = HashSet::new();
    for m in mios.iter().filter(|m| m.tipo == "album" && plano(&m.artista) == pn) {
        ya.insert(plano(&m.nombre));
        ya.insert(m.id.clone());
        v.push(casilla_mia(m, &g));
    }
    if let Some(a) = cat.and_then(|c| c.letras.get(&letra_xp(nombre))).and_then(|l| l.iter().find(|a| a.plano == pn)) {
        for d in &a.discos {
            let id = id_de(d, ids);
            if id == "-" || ya.contains(&d.plano) || (!id.is_empty() && ya.contains(&id)) { continue; }
            v.push(casilla_cat(a, d, &id));
        }
    }
    v
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
        assert!(es_mbid("8f6bd1e4-fbe1-4f50-aa9b-94c450ec0f11"));
        assert!(!es_mbid("8F6BD1E4-fbe1-4f50-aa9b-94c450ec0f11"));
        assert!(!es_mbid("../../../etc/passwd-aaaaaaaaaaaaaaaaaaaaaa"));
    }
    #[test]
    fn buscador_local() {
        let mut c = Catalogo { version: "x".into(), letras: HashMap::new(), genero_de: HashMap::new(), fama_de: HashMap::new(),
            total_genero: HashMap::new(), artistas: 0, discos: 0 };
        let d = |n: &str, m: &str| DiscoCat { nombre: n.into(), plano: plano(n), anio: "".into(), spotify: "".into(), mbid: m.into(), portada: true };
        c.letras.insert("L".into(), vec![
            ArtistaCat { nombre: "Los Tigres del Norte".into(), plano: plano("Los Tigres del Norte"), genero: "Regional".into(), fama: 900_000,
                discos: vec![d("Jefe de jefes", "m1"), d("Contrabando y traición", "m2")] },
            ArtistaCat { nombre: "Los Tigrillos".into(), plano: plano("Los Tigrillos"), genero: "Regional".into(), fama: 100_000,
                discos: vec![d("Tigres de oro", "m3")] },
        ]);
        let mut ids = HashMap::new();
        ids.insert("m2".to_string(), "-".to_string());
        let r = busca(Some(&c), &[], &ids, "tigres");
        assert_eq!(r["artistas"][0]["nombre"], "Los Tigres del Norte");
        assert_eq!(r["artistas"].as_array().unwrap().len(), 1); // «Los Tigrillos» no tiene «tigres»
        let nombres: Vec<&str> = r["discos"].as_array().unwrap().iter().map(|x| x["nombre"].as_str().unwrap()).collect();
        assert_eq!(nombres, vec!["Jefe de jefes", "Tigres de oro"]); // el de Spotify «-» no sale
        assert_eq!(busca(Some(&c), &[], &ids, "jefe tigres")["discos"][0]["nombre"], "Jefe de jefes");
        assert_eq!(discos_de(Some(&c), &[], &ids, "los tigres del norte").len(), 1);
    }
}

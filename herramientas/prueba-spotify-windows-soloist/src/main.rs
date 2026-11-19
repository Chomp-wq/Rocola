// DEMO DE LA ROCOLA CON SPOTIFY, PARA WINDOWS, CON SOLOIST (herramienta de prueba del dueno, 19-11-2026, version con Soloist).
//
// Sustituye a herramientas/prueba-spotify-windows (esa usaba librespot 0.8, que el dueno dijo que no le sirve: "solo
// funciona con cuentas creadas desde antes de 2025"). Esta demo usa Spotify Soloist (cliente oficial de Spotify Connect,
// agosto de 2026, con build para Windows x86_64 nativo: NO hace falta WSL2). Soloist NO se reparte aqui (sus terminos lo
// prohiben): el dueno lo descarga el mismo de developer.spotify.com/documentation/soloist y genera su propia llave en
// developer.spotify.com/dashboard/soloist. Esta herramienta solo lo arranca como proceso aparte y le habla por su
// WebSocket local (--ws) para tocar, pausar y ver la cola; emparejarlo (la cuenta) se hace desde el telefono o el
// escritorio de Spotify, como cualquier aparato Connect.
//
// Para buscar y ver la biblioteca (discos, listas, /v1/me) esta demo entra por su cuenta con el navegador (OAuth con
// PKCE, el mismo cliente de escritorio que usa librespot, pero pidiendo permisos de la API web directamente, SIN Session
// de libespot: ya no hay sesion de reproduccion aqui, Soloist toca). Es una entrada separada de la de Soloist: se puede
// usar una sin la otra. NO esta probado con una cuenta real ni en Windows.
//
// Un solo .exe. Levanta un servidor SOLO en 127.0.0.1 con una pagina (embebida) y abre el navegador. Dos pestanas:
// «Rocola» (demo basica, musica solo de Spotify) y «Pruebas» (Soloist, tu cuenta, diagnostico). NUNCA se escriben
// llaves ni tokens en pantalla ni en el informe. No se guarda musica.
use std::{
    collections::HashMap,
    io::{BufRead, BufReader},
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    sync::{Arc, Mutex},
    time::{Duration, Instant, SystemTime, UNIX_EPOCH},
};

use futures_util::{SinkExt, StreamExt};
use librespot_oauth::{OAuthClient, OAuthClientBuilder};
use serde_json::{json, Value};
use tokio::sync::mpsc;
use tokio_tungstenite::tungstenite::Message;

const PAGINA: &str = include_str!("pagina.html");
const MAX_DIAG: usize = 400;
// Los permisos de la API web que de verdad hacen falta para esta prueba (biblioteca y buscador). Igual que antes, pero
// pedidos directo al navegador (ya no hay Session de librespot que los saque por su cuenta via login5/keymaster).
const ALCANCE: &[&str] = &[
    "user-read-private",
    "playlist-read-private",
    "playlist-read-collaborative",
    "user-library-read",
    "user-follow-read",
    "user-top-read",
    "user-read-recently-played",
];
// El cliente de escritorio de Spotify y su redireccion (el mismo que usa librespot 0.8 con --enable-oauth; puerto y
// cliente deben ser exactamente estos, van juntos en el registro de Spotify).
const OAUTH_CLIENT_ID: &str = "65b708073fc0480ea92a077233ca87bd";
const OAUTH_PUERTO: u16 = 8898;
const OAUTH_REDIRECT: &str = "http://127.0.0.1:8898/login";
const NOMBRE_APARATO_DEFECTO: &str = "Rocola (prueba Soloist)";

// Quita de un texto todo lo que parezca una llave (corridas largas de letras y numeros), para que NUNCA salga en
// pantalla ni en el informe.
fn limpia(t: &str) -> String {
    let mut sal = String::new();
    let mut corrida = String::new();
    let vacia = |c: &mut String, s: &mut String| {
        if c.len() >= 40 { s.push_str("[oculto]"); } else { s.push_str(c); }
        c.clear();
    };
    for ch in t.chars() {
        if ch.is_ascii_alphanumeric() || ch == '_' || ch == '-' || ch == '.' || ch == '~' || ch == '+' || ch == '/' || ch == '=' {
            corrida.push(ch);
        } else {
            vacia(&mut corrida, &mut sal);
            sal.push(ch);
        }
    }
    vacia(&mut corrida, &mut sal);
    sal
}
fn corta(s: &str) -> String { s.chars().take(90).collect() }

// ---------- EL DIAGNOSTICO ----------
struct Diag { epoch_ms: u64, hora: String, que: String, ms: u64, ok: bool, msg: String }

// ---------- TU CUENTA (OAuth del navegador, solo para la API web: biblioteca y buscador) ----------
#[derive(Clone)]
struct TokenWeb { access_token: String, refresh_token: String, expira: Instant }
#[derive(Default)]
struct WebEstado {
    tok: Option<TokenWeb>,
    usuario: String,
    error: String,
    oauth_activo: bool,
    me: Option<Value>,
}

// ---------- SOLOIST (lo que toca la musica) ----------
#[derive(Default, Clone)]
struct SonSoloist {
    uri: String, nombre: String, artista: String, img: String, dur_ms: u64,
    estado: String, // cargando | sonando | pausa | detenida | error
    aviso: String,
    pos_ms: u64, pos_epoch_ms: u64, veloc: f64,
}
// Lo que la rocola pidio y en que va (cargando | sonando | pausa | acabo | error): con esto la pagina cobra, devuelve
// el credito o pasa a la siguiente. Lo que suena (`son`) puede ser otra cosa: el telefono o una continuacion de Spotify.
#[derive(Clone)]
struct Pedida { uri: String, nombre: String, dur_ms: u64, estado: String, aviso: String, t0: Instant, empezo: bool }

// LA CALIDAD MEDIDA: Soloist no dice si toca FLAC o Vorbis (ni su linea de comandos ni su WebSocket). Guarda un archivo
// cifrado por cancion en su cache; su tamano * 8 / la duracion da los kbps (lo mismo que hacen los plugins de Volumio y
// Lyrion). Vorbis 320 da ~330 kbps; Lossless (FLAC) da ~700-1700. Nunca se abre ni se descifra el archivo: solo su tamano.
#[derive(Clone, Default, serde::Serialize, serde::Deserialize)]
struct Medida { kbps: u32, nivel: String }
#[derive(Default)]
struct CalidadEstado {
    uri: String,
    foto: HashMap<PathBuf, u64>, // la cache justo antes de pedir
    desde: Option<Instant>,
    dur_ms: u64,
    bytes: u64,    // todo lo que crecio la cache desde la foto
    archivos: usize,
    mayor: u64,    // lo que crecio el archivo que mas crecio
    estado: String, // midiendo | medida | en_cache | sin_dato
    actual: Option<Medida>,
    hechas: HashMap<String, Medida>, // por uri, guardadas en calidad_medida.json
}

#[derive(Default)]
struct SoloistEstado {
    exe: String,
    nombre_aparato: String,
    vol_rocola: u8,
    verbose: bool,
    formato: Vec<String>, // con --verbose: las ultimas lineas de Soloist que hablan del formato o la calidad
    corriendo: bool,
    arrancando: bool,
    esperando_pareo: bool,
    conectado_ws: bool,
    logged_in: bool,
    is_active: bool,
    volumen: u8,
    ultimo_error: String,
    son: SonSoloist,
    pedida: Option<Pedida>,
    ultimo_pausado: String,
    cola_prev: Vec<Value>,
    cola_sig: Vec<Value>,
    pid_actual: Option<u32>,
}

struct App {
    exe_dir: PathBuf,
    datos: PathBuf,
    puerto: u16,
    http: reqwest::Client,
    diag: Mutex<Vec<Diag>>,
    api_cache: Mutex<HashMap<String, Value>>,
    bloqueo: Mutex<Option<Instant>>,
    web: Mutex<WebEstado>,
    oauth_cancelar: std::sync::atomic::AtomicBool,
    oauth_gen: std::sync::atomic::AtomicU64,
    soloist: Mutex<SoloistEstado>,
    soloist_child: Mutex<Option<Child>>,
    soloist_tx: Mutex<Option<mpsc::UnboundedSender<Value>>>,
    soloist_gen: std::sync::atomic::AtomicU64, // para que un "iniciar" viejo no pise al nuevo
    rt: tokio::runtime::Handle, // tokio::spawn truena desde hilos que no son del runtime (el de --pair)
    calidad: Mutex<CalidadEstado>,
}

impl App {
    fn diag(&self, que: &str, t0: Instant, res: Result<String, String>) {
        let ahora = chrono::Local::now();
        let (ok, msg) = match res { Ok(m) => (true, m), Err(m) => (false, m) };
        let d = Diag {
            epoch_ms: ahora.timestamp_millis() as u64,
            hora: ahora.format("%H:%M:%S").to_string(),
            que: que.to_string(),
            ms: t0.elapsed().as_millis() as u64,
            ok,
            msg: limpia(&msg),
        };
        println!("[{}] {} {} ({} ms) {}", d.hora, if ok { "BIEN" } else { "MAL " }, d.que, d.ms, d.msg);
        let mut v = self.diag.lock().unwrap();
        v.push(d);
        let n = v.len();
        if n > MAX_DIAG { v.drain(0..n - MAX_DIAG); }
    }
    fn ruta_llave_soloist(&self) -> PathBuf { self.datos.join("soloist_llave.txt") }
    fn ruta_config_soloist(&self) -> PathBuf { self.datos.join("soloist_config.json") }
    fn ruta_refresh_web(&self) -> PathBuf { self.datos.join("oauth_refresh.txt") }
    fn ruta_client_id(&self) -> PathBuf { self.datos.join("web_client_id.txt") }
    // El Client ID de la app propia del dueno (developer.spotify.com/dashboard), si lo puso; si no, el compartido de
    // escritorio, que casi siempre contesta 429 porque lo usan todos los programas que lo copian.
    fn client_id(&self) -> String {
        std::fs::read_to_string(self.ruta_client_id()).ok().map(|s| s.trim().to_string())
            .filter(|s| es_client_id(s)).unwrap_or_else(|| OAUTH_CLIENT_ID.to_string())
    }
    fn datos_soloist(&self) -> PathBuf { self.datos.join("soloist_datos") }
    fn cache_soloist(&self) -> PathBuf { self.datos.join("soloist_cache") }
    // v2: las medidas de la primera version salian de un archivo a medias (se cerraban a los 4 s sin crecer).
    fn ruta_calidad(&self) -> PathBuf { self.datos.join("calidad_medida_v2.json") }
}

// ---------- LA CALIDAD MEDIDA (FLAC o no) ----------
fn nivel_de(kbps: u32) -> &'static str {
    match kbps { 0..=59 => "baja", 60..=129 => "normal", 130..=239 => "alta", 240..=499 => "muy_alta", _ => "flac" }
}
fn texto_nivel(n: &str) -> &'static str {
    match n {
        "flac" => "Lossless (FLAC)", "muy_alta" => "Muy alta (320 kbps, Ogg Vorbis)", "alta" => "Alta (160 kbps, Ogg Vorbis)",
        "normal" => "Normal (96 kbps)", "baja" => "Baja (24 kbps)", _ => "",
    }
}
fn es_metadato(p: &Path) -> bool {
    let n = p.file_name().and_then(|n| n.to_str()).unwrap_or("");
    n.ends_with(".ldb") || n.ends_with(".log") || n.starts_with("MANIFEST") || n == "CURRENT" || n == "LOCK" || n.ends_with(".pid") || n.starts_with("ws.")
}
// En Windows el tamano que da la lista del directorio va atrasado mientras Soloist escribe; fs::metadata abre el archivo
// y da el de verdad.
fn tam_real(p: &Path) -> Option<u64> { std::fs::metadata(p).ok().map(|m| m.len()) }
fn foto_cache(dir: &Path) -> HashMap<PathBuf, u64> {
    let mut m = HashMap::new();
    let mut pila = vec![(dir.to_path_buf(), 0u32)];
    while let Some((d, hondo)) = pila.pop() {
        let Ok(rd) = std::fs::read_dir(&d) else { continue };
        for e in rd.flatten() {
            let p = e.path();
            let Ok(ft) = e.file_type() else { continue };
            if ft.is_dir() { if hondo < 8 { pila.push((p, hondo + 1)); } }
            else if ft.is_file() && !es_metadato(&p) {
                if let Some(t) = tam_real(&p) { m.insert(p, t); }
            }
            if m.len() > 50_000 { return m; }
        }
    }
    m
}
fn guarda_calidades(app: &App, hechas: &HashMap<String, Medida>) {
    let _ = std::fs::write(app.ruta_calidad(), serde_json::to_string(hechas).unwrap_or_default());
}

fn crecimiento(foto: &HashMap<PathBuf, u64>, ahora: &HashMap<PathBuf, u64>) -> (u64, usize, u64) {
    let (mut total, mut n, mut mayor) = (0u64, 0usize, 0u64);
    for (f, t) in ahora {
        let antes = foto.get(f).copied().unwrap_or(0);
        if *t > antes { let d = *t - antes; total += d; n += 1; mayor = mayor.max(d); }
    }
    (total, n, mayor)
}

// Cada 2 s suma lo que crecio la cache desde que se pidio la cancion (Soloist baja a tandas, con pausas, y puede
// guardarla en varios archivos). La calidad solo se da al TERMINAR la cancion entera: antes sale de menos.
fn vigila_calidad(app: Arc<App>) {
    loop {
        std::thread::sleep(Duration::from_secs(2));
        let (pedida, son_uri, son_dur) = { let s = app.soloist.lock().unwrap(); (s.pedida.clone(), s.son.uri.clone(), s.son.dur_ms) };
        let Some(p) = pedida else { continue };
        let termino;
        {
            let mut c = app.calidad.lock().unwrap();
            if c.uri != p.uri || c.estado != "midiendo" { continue; }
            if son_uri == p.uri && son_dur > 0 { c.dur_ms = son_dur; }
            if c.dur_ms == 0 { c.dur_ms = p.dur_ms; }
            let ahora = foto_cache(&app.cache_soloist());
            let (b, n, m) = crecimiento(&c.foto, &ahora);
            c.bytes = b; c.archivos = n; c.mayor = m;
            termino = !matches!(p.estado.as_str(), "cargando" | "sonando" | "pausa");
        }
        if termino { cierra_medida(&app, &p); }
    }
}

fn mb(b: u64) -> String { format!("{:.1} MB", b as f64 / 1_048_576.0) }
fn cierra_medida(app: &Arc<App>, p: &Pedida) {
    let diag: Option<(Instant, Result<String, String>)>;
    let mut guardar = None;
    {
        let mut c = app.calidad.lock().unwrap();
        if c.uri != p.uri || c.estado != "midiendo" { return; }
        let ahora = foto_cache(&app.cache_soloist());
        let (b, n, m) = crecimiento(&c.foto, &ahora);
        c.bytes = b; c.archivos = n; c.mayor = m;
        let dur = c.dur_ms.max(1);
        let sono_ms = p.t0.elapsed().as_millis() as u64;
        let desde = c.desde.unwrap_or_else(Instant::now);
        if p.estado == "acabo" && p.empezo && b > 0 && sono_ms * 100 >= dur * 85 {
            let kbps = (b as f64 * 8.0 / (dur as f64 / 1000.0) / 1000.0).round() as u32;
            let nivel = nivel_de(kbps);
            let md = Medida { kbps, nivel: nivel.to_string() };
            c.estado = "medida".into();
            c.actual = Some(md.clone());
            c.hechas.insert(p.uri.clone(), md);
            guardar = Some(c.hechas.clone());
            diag = Some((desde, Ok(format!("{} · {} kbps ({}): {} archivo(s), {} en total, el mayor {}, {} s de cancion",
                texto_nivel(nivel), kbps, p.nombre, n, mb(b), mb(m), dur / 1000))));
        } else if b == 0 && p.empezo {
            let previa = c.hechas.get(&p.uri).cloned();
            c.estado = if previa.is_some() { "en_cache".into() } else { "sin_dato".into() };
            c.actual = previa;
            diag = Some((desde, Ok(format!("sin medir ({}): ya estaba en la cache, no se bajo nada", p.nombre))));
        } else {
            c.estado = "sin_dato".into();
            diag = Some((desde, Ok(format!("sin medir ({}): sono {} de {} s; solo cuenta si suena entera ({} archivo(s), {})",
                p.nombre, sono_ms / 1000, dur / 1000, n, mb(b)))));
        }
    }
    if let Some(h) = guardar { guarda_calidades(app, &h); }
    if let Some((t0, r)) = diag { app.diag("calidad medida", t0, r); }
}

const OAUTH_LISTO: &str = "<!doctype html><meta charset=utf-8><body style='font-family:sans-serif;font-size:28px;background:#111;color:#eee'><h1>Listo</h1><p>Vuelve a la herramienta de la rocola. Puedes cerrar esta pestana.</p></body>";
fn es_client_id(s: &str) -> bool { s.len() == 32 && s.chars().all(|c| c.is_ascii_hexdigit()) }
fn oauth_cliente(client_id: &str, abrir: bool) -> OAuthClient {
    let b = OAuthClientBuilder::new(client_id, OAUTH_REDIRECT, ALCANCE.to_vec()).with_custom_message(OAUTH_LISTO);
    (if abrir { b.open_in_browser() } else { b }).build().expect("el cliente de OAuth no se pudo armar")
}
// Si el navegador nunca contesta, el que espera tiene el puerto 8898 abierto: una visita falsa lo suelta.
fn suelta_oauth() {
    if let Ok(mut s) = std::net::TcpStream::connect(("127.0.0.1", OAUTH_PUERTO)) {
        use std::io::Write;
        let _ = s.write_all(b"GET /login?cancelar=1 HTTP/1.1\r\n\r\n");
    }
}

// ---------- TU CUENTA: entrar, refrescar, olvidar ----------
fn guarda_token_web(app: &App, t: &librespot_oauth::OAuthToken) {
    let _ = std::fs::write(app.ruta_refresh_web(), &t.refresh_token);
    let mut w = app.web.lock().unwrap();
    w.tok = Some(TokenWeb { access_token: t.access_token.clone(), refresh_token: t.refresh_token.clone(), expira: t.expires_at });
    w.error.clear();
}

async fn asegura_token_web(app: &Arc<App>) -> Result<String, String> {
    {
        let w = app.web.lock().unwrap();
        if let Some(t) = &w.tok {
            if t.expira > Instant::now() + Duration::from_secs(60) { return Ok(t.access_token.clone()); }
        }
    }
    let rt = {
        let w = app.web.lock().unwrap();
        w.tok.as_ref().map(|t| t.refresh_token.clone())
    }.filter(|s| !s.is_empty()).or_else(|| {
        std::fs::read_to_string(app.ruta_refresh_web()).ok().map(|s| s.trim().to_string()).filter(|s| !s.is_empty())
    });
    let Some(rt) = rt else { return Err("Falta entrar: todavia no hay cuenta (pestana Pruebas, seccion Tu cuenta).".to_string()) };
    let t0 = Instant::now();
    match oauth_cliente(&app.client_id(), false).refresh_token_async(&rt).await {
        Ok(tok) => { app.diag("renovar la entrada (API web)", t0, Ok("bien".into())); guarda_token_web(app, &tok); Ok(tok.access_token) }
        Err(e) => {
            app.diag("renovar la entrada (API web)", t0, Err(e.to_string()));
            let mut w = app.web.lock().unwrap();
            w.tok = None;
            Err(format!("No se pudo renovar la entrada: {}. Entra otra vez con el navegador.", limpia(&e.to_string())))
        }
    }
}

fn entrar_oauth(app: &Arc<App>) -> Value {
    // Si ya habia una espera (el navegador no se abrio o se cerro), se suelta y se empieza de nuevo.
    if app.web.lock().unwrap().oauth_activo {
        app.oauth_cancelar.store(true, std::sync::atomic::Ordering::SeqCst);
        suelta_oauth();
        std::thread::sleep(Duration::from_millis(400));
    }
    let gen = app.oauth_gen.fetch_add(1, std::sync::atomic::Ordering::SeqCst) + 1;
    app.web.lock().unwrap().oauth_activo = true;
    app.oauth_cancelar.store(false, std::sync::atomic::Ordering::SeqCst);
    let a = app.clone();
    app.rt.spawn(async move {
        let t0 = Instant::now();
        // La libreria espera al navegador bloqueando: va en su propio hilo (y abre el navegador ella misma).
        let cid = a.client_id();
        let r = tokio::task::spawn_blocking(move || oauth_cliente(&cid, true).get_access_token().map_err(|e| e.to_string()))
            .await.unwrap_or_else(|e| Err(e.to_string()));
        if a.oauth_gen.load(std::sync::atomic::Ordering::SeqCst) != gen { return; } // ya hay otra espera mas nueva
        a.web.lock().unwrap().oauth_activo = false;
        let r = if a.oauth_cancelar.load(std::sync::atomic::Ordering::SeqCst) { Err("cancelado".to_string()) } else { r };
        match r {
            Ok(tok) => {
                a.diag("entrar con el navegador", t0, Ok("Spotify entrego el permiso".into()));
                guarda_token_web(&a, &tok);
                let t1 = Instant::now();
                match a.http.get("https://api.spotify.com/v1/me").bearer_auth(&tok.access_token).send().await {
                    Ok(resp) => {
                        let estado = resp.status().as_u16();
                        let cuerpo = resp.text().await.unwrap_or_default();
                        let v: Value = serde_json::from_str(&cuerpo).unwrap_or(Value::Null);
                        if estado == 200 {
                            a.diag("GET /v1/me", t1, Ok(format!("HTTP 200, pais {}", v["country"].as_str().unwrap_or("?"))));
                            let mut w = a.web.lock().unwrap();
                            w.usuario = v["display_name"].as_str().or(v["id"].as_str()).unwrap_or("").to_string();
                            w.me = Some(json!({"estado": estado, "pais": v["country"], "producto": v["product"]}));
                        } else {
                            let m = v["error"]["message"].as_str().unwrap_or("").to_string();
                            a.diag("GET /v1/me", t1, Err(format!("HTTP {estado} {m}")));
                        }
                    }
                    Err(e) => { a.diag("GET /v1/me", t1, Err(e.to_string())); }
                }
            }
            Err(e) => {
                if e != "cancelado" {
                    a.diag("entrar con el navegador", t0, Err(e.to_string()));
                    a.web.lock().unwrap().error = format!("No se pudo entrar: {}", limpia(&e.to_string()));
                }
            }
        }
    });
    json!({"ok": true, "mensaje": "Se esta abriendo el navegador. Inicia sesion en Spotify y acepta los permisos. Si no se abre, copia la direccion «Browse to: ...» de la ventana negra y pegala en el navegador. Si se queda esperando, vuelve a pulsar el boton."})
}

fn olvidar_web(app: &Arc<App>) -> Value {
    let t0 = Instant::now();
    if app.web.lock().unwrap().oauth_activo { app.oauth_cancelar.store(true, std::sync::atomic::Ordering::SeqCst); suelta_oauth(); }
    let r = std::fs::remove_file(app.ruta_refresh_web());
    *app.web.lock().unwrap() = WebEstado::default();
    app.api_cache.lock().unwrap().clear();
    app.diag("olvidar tu cuenta", t0, Ok(format!("listo{}", if r.is_err() { " (no habia nada guardado)" } else { "" })));
    json!({"ok": true})
}

// ---------- LA API WEB (biblioteca y buscador) ----------
async fn pide_api(http: &reqwest::Client, ruta: &str, llave: &str) -> Result<(u16, String, Option<u64>), String> {
    let resp = http.get(format!("https://api.spotify.com{ruta}")).bearer_auth(llave).header("Accept", "application/json")
        .send().await.map_err(|e| e.to_string())?;
    let estado = resp.status().as_u16();
    let espera = resp.headers().get("retry-after").and_then(|v| v.to_str().ok()).and_then(|v| v.trim().parse::<u64>().ok());
    let cuerpo = resp.text().await.map_err(|e| e.to_string())?;
    Ok((estado, cuerpo, espera))
}

async fn api_pagina(app: &Arc<App>, ruta: &str) -> Value {
    let permitida = ["/v1/me", "/v1/albums/", "/v1/playlists/", "/v1/search", "/v1/artists/"].iter().any(|p| ruta.starts_with(p));
    if !permitida || ruta.contains("..") || ruta.contains(' ') { return json!({"ok": false, "error": "ruta no permitida"}); }
    if let Some(v) = app.api_cache.lock().unwrap().get(ruta) { return v.clone(); }
    if let Some(h) = *app.bloqueo.lock().unwrap() {
        let falta = h.saturating_duration_since(Instant::now()).as_secs() + 1;
        if h > Instant::now() { return json!({"ok": false, "estado": 429, "espera_s": falta, "error": format!("Spotify pide esperar {falta} s")}); }
    }
    let t0 = Instant::now();
    let llave = match asegura_token_web(app).await {
        Ok(l) => l,
        Err(e) => { app.diag(&format!("GET {}", corta(ruta)), t0, Err(e.clone())); return json!({"ok": false, "falta_entrar": true, "error": e}); }
    };
    match pide_api(&app.http, ruta, &llave).await {
        Ok((estado, cuerpo, espera)) => {
            let ok = (200..300).contains(&estado);
            if estado == 429 {
                let e = espera.unwrap_or(5).min(600);
                *app.bloqueo.lock().unwrap() = Some(Instant::now() + Duration::from_secs(e));
                app.diag(&format!("GET {}", corta(ruta)), t0, Err(format!("HTTP 429: Spotify frena, esperar {e} s")));
                return json!({"ok": false, "estado": 429, "espera_s": e, "error": format!("Spotify pide esperar {e} s")});
            }
            let cuerpo_v: Value = serde_json::from_str(&cuerpo).unwrap_or(Value::Null);
            if ok {
                app.diag(&format!("GET {}", corta(ruta)), t0, Ok(format!("HTTP {estado}")));
                let v = json!({"ok": true, "estado": estado, "datos": cuerpo_v});
                app.api_cache.lock().unwrap().insert(ruta.to_string(), v.clone());
                v
            } else {
                let m = cuerpo_v["error"]["message"].as_str().unwrap_or("").to_string();
                app.diag(&format!("GET {}", corta(ruta)), t0, Err(format!("HTTP {estado} {m}")));
                json!({"ok": false, "estado": estado, "error": format!("Spotify contesto {estado}: {}", limpia(&m))})
            }
        }
        Err(e) => { app.diag(&format!("GET {}", corta(ruta)), t0, Err(e.clone())); json!({"ok": false, "error": format!("No hubo respuesta de Spotify: {}", limpia(&e))}) }
    }
}

// ---------- SOLOIST: encontrar el programa, config, llave ----------
fn exe_candidatos(exe_dir: &Path) -> Vec<PathBuf> {
    let suf = std::env::consts::EXE_SUFFIX;
    let nombre = format!("soloist{suf}");
    let mut v = vec![exe_dir.join(&nombre), exe_dir.join("soloist").join(&nombre)];
    for var in ["USERPROFILE", "HOME"] {
        if let Ok(h) = std::env::var(var) {
            let base = PathBuf::from(&h).join("Downloads");
            v.push(base.join(&nombre));
            v.push(base.join("soloist_release_windows_x86_64").join(&nombre));
            v.push(base.join("soloist_release_x86_64").join("soloist"));
        }
    }
    if let Ok(la) = std::env::var("LOCALAPPDATA") { v.push(PathBuf::from(la).join("soloist").join(&nombre)); }
    if let Ok(path) = std::env::var("PATH") {
        let sep = if cfg!(windows) { ';' } else { ':' };
        for p in path.split(sep) { v.push(PathBuf::from(p).join(&nombre)); }
    }
    v
}
fn detecta_soloist(exe_dir: &Path) -> Vec<String> {
    exe_candidatos(exe_dir).into_iter().filter(|p| p.is_file()).map(|p| p.display().to_string()).collect::<std::collections::BTreeSet<_>>().into_iter().collect()
}

// Lo que el dueno escribe como ruta: sin comillas («Copiar como ruta» las pone), una carpeta vale (se busca soloist.exe
// dentro) y sin «.exe» tambien (el Explorador de Windows oculta la extension).
fn normaliza_exe(s: &str) -> String {
    let t = s.trim().trim_matches('"').trim();
    if t.is_empty() { return String::new(); }
    let suf = std::env::consts::EXE_SUFFIX;
    let p = PathBuf::from(t);
    if p.is_dir() {
        let dentro = p.join(format!("soloist{suf}"));
        if dentro.is_file() { return dentro.display().to_string(); }
    }
    if !p.is_file() && !suf.is_empty() && !t.to_ascii_lowercase().ends_with(suf) {
        let con = PathBuf::from(format!("{t}{suf}"));
        if con.is_file() { return con.display().to_string(); }
    }
    t.to_string()
}

#[derive(serde::Serialize, serde::Deserialize, Default)]
struct ConfigSoloist {
    exe: String,
    nombre_aparato: String,
    // El volumen de Soloist (0-100) que la rocola le pone al arrancar y antes de cada cancion; el de Spotify Connect se
    // queda en lo ultimo que puso el telefono (a veces 0), y la rocola no debe depender de eso.
    #[serde(default = "vol_defecto")]
    volumen: u8,
    // --verbose de Soloist («Show all logs», no sale en su documentacion web): para ver si dice el formato que le llega.
    #[serde(default)]
    verbose: bool,
}
fn vol_defecto() -> u8 { 100 }

fn carga_config_soloist(app: &App) {
    let c: ConfigSoloist = std::fs::read_to_string(app.ruta_config_soloist()).ok()
        .and_then(|s| serde_json::from_str(&s).ok()).unwrap_or_default();
    let mut exe = normaliza_exe(&c.exe);
    if exe.is_empty() || !Path::new(&exe).is_file() {
        if let Some(d) = detecta_soloist(&app.exe_dir).into_iter().next() { exe = d; }
    }
    let mut s = app.soloist.lock().unwrap();
    s.exe = exe;
    s.nombre_aparato = if c.nombre_aparato.is_empty() { NOMBRE_APARATO_DEFECTO.to_string() } else { c.nombre_aparato };
    s.vol_rocola = if std::fs::metadata(app.ruta_config_soloist()).is_ok() { c.volumen.min(100) } else { 100 };
    s.verbose = c.verbose;
}
fn guarda_config_soloist(app: &App, exe: &str, nombre: &str) {
    let exe = &normaliza_exe(exe);
    let (vol, verbose) = { let s = app.soloist.lock().unwrap(); (s.vol_rocola, s.verbose) };
    let c = ConfigSoloist { exe: exe.to_string(), nombre_aparato: nombre.to_string(), volumen: vol, verbose };
    let _ = std::fs::write(app.ruta_config_soloist(), serde_json::to_string_pretty(&c).unwrap_or_default());
    let mut s = app.soloist.lock().unwrap();
    s.exe = exe.to_string();
    s.nombre_aparato = nombre.to_string();
}
fn tiene_llave_soloist(app: &App) -> bool {
    std::env::var("SOLOIST_API_KEY").map(|v| !v.trim().is_empty()).unwrap_or(false)
        || std::fs::read_to_string(app.ruta_llave_soloist()).map(|s| !s.trim().is_empty()).unwrap_or(false)
}
fn llave_soloist(app: &App) -> Option<String> {
    if let Ok(v) = std::env::var("SOLOIST_API_KEY") { if !v.trim().is_empty() { return Some(v.trim().to_string()); } }
    std::fs::read_to_string(app.ruta_llave_soloist()).ok().map(|s| s.trim().to_string()).filter(|s| !s.is_empty())
}

// ---------- SOLOIST: entidades del protocolo ----------
fn mejor_portada(cover: &Value) -> String {
    let Some(arr) = cover.as_array() else { return String::new() };
    arr.iter().find(|c| c["size"].as_str() == Some("large")).or(arr.first())
        .and_then(|c| c["url"].as_str()).unwrap_or("").to_string()
}
fn nombre_de(e: &Value) -> String { e["decorations"]["identity"]["name"].as_str().unwrap_or("").to_string() }
fn artista_de(e: &Value) -> String {
    let creadores = e["decorations"]["creators"].as_array().map(|a| {
        a.iter().filter_map(|c| c["entity"]["decorations"]["identity"]["name"].as_str()).collect::<Vec<_>>().join(", ")
    }).unwrap_or_default();
    if !creadores.is_empty() { return creadores; }
    e["decorations"]["parent"]["entity"]["decorations"]["identity"]["name"].as_str().unwrap_or("").to_string()
}
fn entidad_a_son(e: &Value, estado: &str) -> SonSoloist {
    SonSoloist {
        uri: e["uri"].as_str().unwrap_or("").to_string(),
        nombre: nombre_de(e), artista: artista_de(e),
        img: mejor_portada(&e["decorations"]["visual_identity"]["cover"]),
        dur_ms: e["decorations"]["playback"]["duration_ms"].as_u64().unwrap_or(0),
        estado: estado.to_string(), aviso: String::new(),
        pos_ms: 0, pos_epoch_ms: 0, veloc: 0.0,
    }
}
fn status_a_estado(status: &str) -> &'static str {
    match status { "playing" => "sonando", "paused" => "pausa", "buffering" => "cargando", _ => "detenida" }
}
fn epoch_ms_ahora() -> u64 { SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_millis() as u64).unwrap_or(0) }

// ---------- SOLOIST: procesar lo que llega por el WebSocket ----------
fn procesa_evento_ws(app: &Arc<App>, texto: &str) {
    let Ok(v) = serde_json::from_str::<Value>(texto) else { return };
    let tipo = v["type"].as_str().unwrap_or("");
    let mut diag: Option<(&'static str, Result<String, String>)> = None;
    match tipo {
        "auth_state" => {
            let mut s = app.soloist.lock().unwrap();
            let antes = s.logged_in;
            s.logged_in = v["logged_in"].as_bool().unwrap_or(false);
            s.is_active = v["is_active"].as_bool().unwrap_or(false);
            if s.logged_in && !antes { diag = Some(("Soloist", Ok("emparejado con una cuenta de Spotify".into()))); }
            else if !s.logged_in && antes { diag = Some(("Soloist", Err("la cuenta se desconecto (sesion cerrada o reemplazada)".into()))); s.son = SonSoloist::default(); }
        }
        "playback_state" => {
            let mut s = app.soloist.lock().unwrap();
            let estado = status_a_estado(v["status"].as_str().unwrap_or(""));
            let mut son = entidad_a_son(&v["item"], estado);
            son.pos_ms = v["position"]["position_ms"].as_u64().unwrap_or(0);
            son.pos_epoch_ms = v["position"]["timestamp_ms"].as_u64().unwrap_or(0);
            son.veloc = v["position"]["speed"].as_f64().unwrap_or(0.0);
            s.son = son;
            s.is_active = v["is_active"].as_bool().unwrap_or(s.is_active);
            if let Some(vol) = v["volume"].as_u64() { s.volumen = vol as u8; }
        }
        "track_changed" => {
            let mut s = app.soloist.lock().unwrap();
            let estado = s.son.estado.clone();
            let mut son = entidad_a_son(&v["item"], &estado);
            son.pos_ms = s.son.pos_ms; son.pos_epoch_ms = s.son.pos_epoch_ms; son.veloc = s.son.veloc;
            s.son = son;
        }
        "playback_changed" => {
            let mut s = app.soloist.lock().unwrap();
            s.son.estado = status_a_estado(v["status"].as_str().unwrap_or("")).to_string();
        }
        "volume_changed" => { if let Some(vol) = v["volume"].as_u64() { app.soloist.lock().unwrap().volumen = vol as u8; } }
        "device_changed" => { app.soloist.lock().unwrap().is_active = v["is_active"].as_bool().unwrap_or(false); }
        "position_sync" => {
            let mut s = app.soloist.lock().unwrap();
            s.son.pos_ms = v["position"]["position_ms"].as_u64().unwrap_or(0);
            s.son.pos_epoch_ms = v["position"]["timestamp_ms"].as_u64().unwrap_or(0);
            s.son.veloc = v["position"]["speed"].as_f64().unwrap_or(0.0);
        }
        "queue_changed" => {
            let mut s = app.soloist.lock().unwrap();
            s.cola_prev = v["previous"].as_array().cloned().unwrap_or_default();
            s.cola_sig = v["upcoming"].as_array().cloned().unwrap_or_default();
        }
        "error" => { diag = Some(("Soloist", Err(v["message"].as_str().unwrap_or("error").to_string()))); }
        _ => {}
    }
    if let Some((q, r)) = diag { app.diag(q, Instant::now(), r); }
    revisa_reproduccion(app);
}

// Decide si lo pedido ya empezo, si acabo (pasa a la siguiente sin devolver credito), si fallo (devuelve credito) o
// si algo esta sonando SIN que la rocola lo haya pedido (el telefono, u otra continuacion): entonces lo pausa.
fn revisa_reproduccion(app: &Arc<App>) {
    let mut diags: Vec<(&'static str, Instant, Result<String, String>)> = vec![];
    let mut pausar = false;
    {
        let mut s = app.soloist.lock().unwrap();
        let (uri, estado, nombre) = (s.son.uri.clone(), s.son.estado.clone(), s.son.nombre.clone());
        let mut libre = true; // nada pedido en curso: lo que suene, sobra
        if let Some(p) = s.pedida.as_mut() {
            if matches!(p.estado.as_str(), "cargando" | "sonando" | "pausa") {
                libre = false;
                if uri == p.uri {
                    match estado.as_str() {
                        "sonando" => {
                            if !p.empezo { p.empezo = true; diags.push(("tocar", p.t0, Ok(format!("empezo a sonar ({nombre})")))); }
                            p.estado = "sonando".into();
                        }
                        "pausa" if p.empezo => p.estado = "pausa".into(),
                        "detenida" if p.empezo => p.estado = "acabo".into(),
                        // Un «idle» justo despues de pedir puede ser de paso; despues de 3 s ya es que no pudo.
                        "detenida" if p.t0.elapsed() > Duration::from_secs(3) => {
                            p.estado = "error".into();
                            p.aviso = "Soloist no pudo tocar esta cancion.".into();
                            diags.push(("tocar", p.t0, Err("se detuvo sin empezar".into())));
                        }
                        _ => {}
                    }
                } else if p.empezo {
                    // La pedida ya sono y Soloist paso a otra cosa (se salto desde el telefono o Spotify siguio solo).
                    p.estado = "acabo".into();
                    libre = true;
                }
            }
        }
        if libre && estado == "sonando" && !uri.is_empty() {
            pausar = true;
            if s.ultimo_pausado != uri {
                s.ultimo_pausado = uri.clone();
                diags.push(("Soloist", Instant::now(), Err(format!("sonaba algo que no se pidio desde la rocola ({nombre}): en pausa"))));
            }
        }
    }
    if pausar { envia_comando(app, json!({"type": "command", "command": "pause"})); }
    for (q, t0, r) in diags { app.diag(q, t0, r); }
}

fn revisa_tardanza(app: &Arc<App>) {
    let mut diag = None;
    {
        let mut s = app.soloist.lock().unwrap();
        if let Some(p) = s.pedida.as_mut() {
            if p.estado == "cargando" && p.t0.elapsed() > Duration::from_secs(20) {
                p.estado = "error".into();
                p.aviso = "Soloist no empezo a tocar en 20 segundos.".into();
                diag = Some(p.t0);
            }
        }
    }
    if let Some(t0) = diag { app.diag("tocar", t0, Err("no empezo en 20 s".into())); }
}

fn envia_comando(app: &Arc<App>, v: Value) -> bool {
    match app.soloist_tx.lock().unwrap().clone() { Some(tx) => tx.send(v).is_ok(), None => false }
}

// ---------- SOLOIST: arrancar, detener, emparejar, olvidar ----------
fn detener_soloist(app: &Arc<App>) {
    *app.soloist_tx.lock().unwrap() = None;
    if let Some(mut c) = app.soloist_child.lock().unwrap().take() { let _ = c.kill(); let _ = c.wait(); }
    let mut s = app.soloist.lock().unwrap();
    s.corriendo = false; s.conectado_ws = false; s.logged_in = false; s.is_active = false; s.pid_actual = None;
}

// Palabras con las que Soloist podria decir que formato o calidad le llega (con --verbose).
const PALABRAS_FORMATO: &[&str] = &["bitrate", "kbps", "flac", "vorbis", "ogg", "codec", "format", "quality", "lossless", "aac", "mp3", "sample rate", "44100", "file_id", "audio file"];
fn lector_lineas(mut r: impl std::io::Read + Send + 'static, app: Arc<App>, prefijo: &'static str, verbose: bool) {
    std::thread::spawn(move || {
        let br = BufReader::new(&mut r);
        for linea in br.lines().map_while(Result::ok) {
            let t = limpia(&linea);
            if t.trim().is_empty() { continue; }
            if !verbose { app.diag(prefijo, Instant::now(), Ok(t)); continue; }
            // Con --verbose sale muchisimo: al diagnostico solo lo del formato y los avisos.
            let m = t.to_lowercase();
            if PALABRAS_FORMATO.iter().any(|p| m.contains(p)) {
                {
                    let mut s = app.soloist.lock().unwrap();
                    s.formato.push(t.clone());
                    let n = s.formato.len();
                    if n > 12 { s.formato.drain(0..n - 12); }
                }
                app.diag("soloist (formato)", Instant::now(), Ok(t));
            } else if ["error", "warn", "expire", "fail", "logged in", "became active", "playing", "paused"].iter().any(|p| m.contains(p)) {
                app.diag(prefijo, Instant::now(), Ok(t));
            }
        }
    });
}

fn iniciar_soloist(app: &Arc<App>) -> Value {
    {
        let s = app.soloist.lock().unwrap();
        if s.corriendo || s.arrancando { return json!({"ok": false, "error": "Soloist ya esta corriendo o arrancando."}); }
    }
    let (exe, nombre) = { let s = app.soloist.lock().unwrap(); (s.exe.clone(), s.nombre_aparato.clone()) };
    if exe.trim().is_empty() { return json!({"ok": false, "error": "Falta la ruta a soloist.exe (seccion Soloist)."}); }
    if !Path::new(&exe).is_file() { return json!({"ok": false, "error": format!("No encuentro el archivo: {exe} (usa «Detectar automaticamente», o pon la ruta completa con .exe)")}); }
    let Some(llave) = llave_soloist(app) else { return json!({"ok": false, "error": "Falta la llave de la API de Soloist (seccion Soloist)."}) };
    app.soloist.lock().unwrap().arrancando = true;
    let gen = app.soloist_gen.fetch_add(1, std::sync::atomic::Ordering::SeqCst) + 1;
    let datos_dir = app.datos_soloist();
    let cache_dir = app.cache_soloist();
    let _ = std::fs::create_dir_all(&datos_dir);
    let _ = std::fs::create_dir_all(&cache_dir);
    // Si el anterior se cerro de golpe (Detener en Windows lo termina), sus ws.addr/ws.port quedan con el puerto viejo.
    let _ = std::fs::remove_file(datos_dir.join("ws.addr"));
    let _ = std::fs::remove_file(datos_dir.join("ws.port"));
    let t0 = Instant::now();
    let (vol_ini, verbose) = { let s = app.soloist.lock().unwrap(); (s.vol_rocola.to_string(), s.verbose) };
    let hijo = Command::new(&exe)
        .args(["--device-name", &nombre, "--api-key", &llave, "--ws", "127.0.0.1:0", "--data-dir"])
        .arg(&datos_dir)
        .args(["--cache-dir"]).arg(&cache_dir)
        .args(["--initial-volume", &vol_ini])
        .args(if verbose { &["--verbose"][..] } else { &[][..] })
        .args(["--cache-size", "1024"]) // sin tope crece sin limite; 1 GB alcanza para ~40 canciones en FLAC
        .stdout(Stdio::piped()).stderr(Stdio::piped())
        .spawn();
    let mut hijo = match hijo {
        Ok(h) => h,
        Err(e) => {
            app.soloist.lock().unwrap().arrancando = false;
            let m = format!("No pude arrancar Soloist: {e}");
            app.diag("iniciar Soloist", t0, Err(m.clone()));
            return json!({"ok": false, "error": m});
        }
    };
    let pid = hijo.id();
    if let Some(o) = hijo.stdout.take() { lector_lineas(o, app.clone(), "soloist", verbose); }
    if let Some(e) = hijo.stderr.take() { lector_lineas(e, app.clone(), "soloist", verbose); }
    *app.soloist_child.lock().unwrap() = Some(hijo);
    {
        let mut s = app.soloist.lock().unwrap();
        s.corriendo = true; s.arrancando = false; s.pid_actual = Some(pid); s.ultimo_error.clear();
    }
    app.diag("iniciar Soloist", t0, Ok(format!("arrancado (pid {pid})")));

    // Vigila que el proceso siga vivo (sin bloquear nada: revisa cada medio segundo).
    {
        let a = app.clone();
        std::thread::spawn(move || {
            loop {
                std::thread::sleep(Duration::from_millis(500));
                if app_generacion_vieja(&a, gen) { return; }
                let salio = { let mut c = a.soloist_child.lock().unwrap();
                    match c.as_mut() { Some(h) => h.try_wait().ok().flatten(), None => return } };
                if let Some(estado) = salio {
                    *a.soloist_child.lock().unwrap() = None;
                    *a.soloist_tx.lock().unwrap() = None;
                    let codigo = estado.code();
                    let mut s = a.soloist.lock().unwrap();
                    s.corriendo = false; s.conectado_ws = false; s.logged_in = false; s.is_active = false;
                    let m = if codigo == Some(10) { "la version de Soloist caducó (90 dias): descarga una nueva.".to_string() }
                        else { format!("Soloist se cerro (codigo {:?}).", codigo) };
                    s.ultimo_error = m.clone();
                    drop(s);
                    a.diag("Soloist", Instant::now(), Err(m));
                    return;
                }
            }
        });
    }
    // Busca el WebSocket (ws.addr/ws.port) y se conecta.
    {
        let a = app.clone();
        let datos_dir2 = datos_dir.clone();
        app.rt.spawn(async move { busca_y_conecta_ws(a, datos_dir2, gen).await; });
    }
    json!({"ok": true})
}

fn app_generacion_vieja(app: &App, gen: u64) -> bool { app.soloist_gen.load(std::sync::atomic::Ordering::SeqCst) != gen }

async fn busca_y_conecta_ws(app: Arc<App>, datos_dir: PathBuf, gen: u64) {
    let t0 = Instant::now();
    // Se reintenta hasta 20 s: los archivos pueden tardar o, si el anterior se cerro de golpe, traer un puerto viejo.
    let mut ultimo_error = String::from("Soloist no abrio su WebSocket");
    let conexion = loop {
        if app_generacion_vieja(&app, gen) { return; }
        if t0.elapsed() > Duration::from_secs(20) { break Err(ultimo_error); }
        let a = tokio::fs::read_to_string(datos_dir.join("ws.addr")).await;
        let p = tokio::fs::read_to_string(datos_dir.join("ws.port")).await;
        if let (Ok(a), Ok(p)) = (a, p) {
            if let Ok(p) = p.trim().parse::<u16>() {
                let (direc, puerto) = (a.trim().to_string(), p);
                match tokio_tungstenite::connect_async(format!("ws://{direc}:{puerto}").as_str()).await {
                    Ok((flujo, _)) => break Ok((flujo, direc, puerto)),
                    Err(e) => ultimo_error = e.to_string(),
                }
            }
        }
        tokio::time::sleep(Duration::from_millis(500)).await;
    };
    match conexion {
        Ok((flujo, direc, puerto)) => {
            if app_generacion_vieja(&app, gen) { return; }
            app.diag("conectar al WebSocket de Soloist", t0, Ok(format!("conectado ({direc}:{puerto})")));
            app.soloist.lock().unwrap().conectado_ws = true;
            let (mut escribe, mut lee) = flujo.split();
            let (tx, mut rx) = mpsc::unbounded_channel::<Value>();
            *app.soloist_tx.lock().unwrap() = Some(tx);
            let escritor = tokio::spawn(async move {
                while let Some(v) = rx.recv().await {
                    if escribe.send(Message::text(v.to_string())).await.is_err() { break; }
                }
            });
            while let Some(msg) = lee.next().await {
                if app_generacion_vieja(&app, gen) { break; }
                match msg {
                    Ok(Message::Text(t)) => procesa_evento_ws(&app, t.as_str()),
                    Ok(Message::Close(_)) | Err(_) => break,
                    _ => {}
                }
            }
            escritor.abort();
            *app.soloist_tx.lock().unwrap() = None;
            if !app_generacion_vieja(&app, gen) {
                let mut s = app.soloist.lock().unwrap();
                s.conectado_ws = false; s.logged_in = false; s.is_active = false;
            }
        }
        Err(e) => { app.diag("conectar al WebSocket de Soloist", t0, Err(format!("no se pudo en 20 s: {e}"))); }
    }
}

fn olvidar_soloist(app: &Arc<App>) -> Value {
    let t0 = Instant::now();
    detener_soloist(app);
    let r = std::fs::remove_dir_all(app.datos_soloist());
    let mut s = app.soloist.lock().unwrap();
    let (exe, nombre) = (s.exe.clone(), s.nombre_aparato.clone());
    *s = SoloistEstado { exe, nombre_aparato: nombre, ..Default::default() };
    drop(s);
    app.diag("olvidar el aparato (Soloist)", t0, r.map(|_| "listo: habra que elegirlo otra vez desde el telefono".to_string()).map_err(|e| e.to_string()));
    json!({"ok": true})
}

// Vuelve a emparejar: para el daemon, corre `--pair` (espera hasta 2 min a que elijan el aparato) y si sale bien
// vuelve a arrancar el daemon normal.
fn reemparejar(app: &Arc<App>) -> Value {
    let (exe, nombre) = { let s = app.soloist.lock().unwrap(); (s.exe.clone(), s.nombre_aparato.clone()) };
    if exe.trim().is_empty() || !Path::new(&exe).is_file() { return json!({"ok": false, "error": "Falta una ruta valida a soloist.exe."}); }
    let Some(llave) = llave_soloist(app) else { return json!({"ok": false, "error": "Falta la llave de la API de Soloist."}) };
    detener_soloist(app);
    app.soloist.lock().unwrap().esperando_pareo = true;
    let a = app.clone();
    let datos_dir = app.datos_soloist();
    std::thread::spawn(move || {
        let t0 = Instant::now();
        let _ = std::fs::create_dir_all(&datos_dir);
        let hijo = Command::new(&exe)
            .args(["--device-name", &nombre, "--api-key", &llave, "--pair", "--data-dir"]).arg(&datos_dir)
            .stdout(Stdio::piped()).stderr(Stdio::piped()).spawn();
        let mut hijo = match hijo {
            Ok(h) => h,
            Err(e) => { a.soloist.lock().unwrap().esperando_pareo = false; a.diag("volver a emparejar", t0, Err(e.to_string())); return; }
        };
        if let Some(o) = hijo.stdout.take() { lector_lineas(o, a.clone(), "soloist (--pair)", false); }
        if let Some(e) = hijo.stderr.take() { lector_lineas(e, a.clone(), "soloist (--pair)", false); }
        let salida = loop {
            match hijo.try_wait() {
                Ok(Some(s)) => break Some(s),
                Ok(None) => {
                    if t0.elapsed() > Duration::from_secs(150) { let _ = hijo.kill(); let _ = hijo.wait(); break None; }
                    std::thread::sleep(Duration::from_millis(400));
                }
                Err(_) => break None,
            }
        };
        a.soloist.lock().unwrap().esperando_pareo = false;
        match salida {
            Some(s) if s.success() => {
                a.diag("volver a emparejar", t0, Ok("listo: cuenta emparejada".into()));
                iniciar_soloist(&a);
            }
            Some(s) => { a.diag("volver a emparejar", t0, Err(format!("Soloist termino con {:?}", s.code()))); }
            None => { a.diag("volver a emparejar", t0, Err("no se eligio el aparato en 150 s".into())); }
        }
    });
    json!({"ok": true, "mensaje": "Abre Spotify (telefono o escritorio, misma red) y elige el aparato en el selector. Hasta 150 segundos."})
}

// ---------- TOCAR, PARAR, PRECARGA ----------
async fn tocar(app: &Arc<App>, v: &Value) -> Value {
    let t0 = Instant::now();
    let uri = v["uri"].as_str().unwrap_or("").to_string();
    if uri.is_empty() { return json!({"ok": false, "error": "uri vacia"}); }
    let logged_in = app.soloist.lock().unwrap().logged_in;
    if !logged_in {
        let m = "Falta emparejar Soloist con una cuenta (pestana Pruebas, seccion Soloist).".to_string();
        app.diag("tocar", t0, Err(m.clone()));
        return json!({"ok": false, "error": m});
    }
    if app.soloist_tx.lock().unwrap().is_none() {
        let m = "Soloist no esta conectado por WebSocket.".to_string();
        app.diag("tocar", t0, Err(m.clone()));
        return json!({"ok": false, "error": m});
    }
    // Primero se cierra la medida de la anterior (la pagina pide la siguiente en cuanto acaba).
    let previa = app.soloist.lock().unwrap().pedida.clone();
    if let Some(pp) = previa { cierra_medida(app, &pp); }
    // La foto de la cache ANTES de pedir: lo que crezca despues es de esta cancion.
    let foto = foto_cache(&app.cache_soloist());
    {
        let mut c = app.calidad.lock().unwrap();
        let hechas = std::mem::take(&mut c.hechas);
        *c = CalidadEstado { uri: uri.clone(), foto, desde: Some(Instant::now()), estado: "midiendo".into(), hechas, ..Default::default() };
    }
    {
        let mut s = app.soloist.lock().unwrap();
        s.pedida = Some(Pedida {
            uri: uri.clone(), nombre: v["nombre"].as_str().unwrap_or("").into(), dur_ms: v["dur_ms"].as_u64().unwrap_or(0),
            estado: "cargando".into(), aviso: String::new(), t0, empezo: false,
        });
        s.ultimo_pausado.clear();
    }
    // Siempre «play» de una sola cancion (nada en la cola de Spotify: la cola es la de la rocola, con creditos).
    envia_comando(app, json!({"type": "command", "command": "activate"}));
    let vol = app.soloist.lock().unwrap().vol_rocola;
    envia_comando(app, json!({"type": "command", "command": "set_volume", "volume": vol}));
    envia_comando(app, json!({"type": "command", "command": "play", "uri": uri}));
    json!({"ok": true})
}
fn parar(app: &Arc<App>) -> Value {
    app.soloist.lock().unwrap().pedida = None;
    envia_comando(app, json!({"type": "command", "command": "pause"}));
    json!({"ok": true})
}
fn vaciar_cache_soloist(app: &Arc<App>) -> Value {
    if app.soloist.lock().unwrap().corriendo { return json!({"ok": false, "error": "Deten Soloist primero."}); }
    let t0 = Instant::now();
    let r = std::fs::remove_dir_all(app.cache_soloist());
    let mut c = app.calidad.lock().unwrap();
    *c = CalidadEstado::default();
    drop(c);
    let _ = std::fs::remove_file(app.ruta_calidad());
    app.diag("vaciar la cache de Soloist", t0, Ok(if r.is_ok() { "listo: cada cancion se bajara y medira otra vez".into() } else { "no habia cache".into() }));
    json!({"ok": true})
}

// ---------- ESTADO E INFORME ----------
fn estado_json(app: &Arc<App>) -> Value {
    revisa_tardanza(app);
    let w = app.web.lock().unwrap();
    let s = app.soloist.lock().unwrap();
    let pos = s.son.pos_ms as f64 + if s.son.veloc != 0.0 && s.son.pos_epoch_ms > 0 {
        s.son.veloc * (epoch_ms_ahora() as f64 - s.son.pos_epoch_ms as f64).max(0.0)
    } else { 0.0 };
    json!({
        "version": env!("CARGO_PKG_VERSION"),
        "web": {
            "conectado": w.tok.is_some(), "usuario": w.usuario, "error": w.error, "oauth_activo": w.oauth_activo,
            "hay_refresh_guardado": app.ruta_refresh_web().exists(), "me": w.me,
            "client_id_propio": app.client_id() != OAUTH_CLIENT_ID, "client_id": app.client_id(),
        },
        "soloist": {
            "exe": s.exe, "nombre_aparato": s.nombre_aparato, "tiene_llave": tiene_llave_soloist(app),
            "corriendo": s.corriendo, "arrancando": s.arrancando, "esperando_pareo": s.esperando_pareo,
            "conectado_ws": s.conectado_ws, "logged_in": s.logged_in, "is_active": s.is_active, "volumen": s.volumen, "vol_rocola": s.vol_rocola, "verbose": s.verbose, "formato": s.formato,
            "ultimo_error": s.ultimo_error,
            "son": {"uri": s.son.uri, "nombre": s.son.nombre, "artista": s.son.artista, "img": s.son.img,
                     "dur_ms": s.son.dur_ms, "estado": s.son.estado, "aviso": s.son.aviso, "pos_ms": pos.max(0.0) as u64},
            "pedida": s.pedida.as_ref().map(|p| json!({"uri": p.uri, "estado": p.estado, "aviso": p.aviso})),
            "cola_n": s.cola_sig.len(),
            "calidad": calidad_json(app),
        },
        "diag_n": app.diag.lock().unwrap().len(),
    })
}
fn calidad_json(app: &App) -> Value {
    let c = app.calidad.lock().unwrap();
    let (kbps, nivel) = c.actual.as_ref().map(|m| (m.kbps, m.nivel.clone())).unwrap_or((0, String::new()));
    json!({"uri": c.uri, "estado": c.estado, "kbps": kbps, "nivel": nivel, "texto": texto_nivel(&nivel), "medidas": c.hechas.len(),
           "bytes": c.bytes, "archivos": c.archivos})
}
fn resumen_calidades(app: &App) -> String {
    let c = app.calidad.lock().unwrap();
    let mut n: std::collections::BTreeMap<&str, u32> = Default::default();
    for m in c.hechas.values() { *n.entry(texto_nivel(&m.nivel)).or_default() += 1; }
    if n.is_empty() { return "ninguna todavia".into(); }
    n.iter().map(|(k, v)| format!("{v} en {k}")).collect::<Vec<_>>().join(", ")
}
fn diag_json(app: &Arc<App>) -> Value {
    let v = app.diag.lock().unwrap();
    Value::Array(v.iter().map(|d| json!({"epoch": d.epoch_ms, "hora": d.hora, "que": d.que, "ms": d.ms, "ok": d.ok, "msg": d.msg})).collect())
}
fn informe_texto(app: &Arc<App>) -> String {
    let e = estado_json(app);
    let mut t = String::new();
    t.push_str("INFORME DE LA DEMO DE LA ROCOLA CON SPOTIFY + SOLOIST (sin llaves ni tokens)\r\n");
    t.push_str(&format!("Hecho: {}\r\nVersion: {}\r\nSistema: {} {}\r\n", chrono::Local::now().format("%Y-%m-%d %H:%M:%S"), e["version"].as_str().unwrap_or(""), std::env::consts::OS, std::env::consts::ARCH));
    t.push_str(&format!("Tu cuenta (API web) entrada: {}\r\nSoloist corriendo: {}\r\nSoloist conectado por WebSocket: {}\r\nSoloist emparejado: {}\r\n",
        e["web"]["conectado"], e["soloist"]["corriendo"], e["soloist"]["conectado_ws"], e["soloist"]["logged_in"]));
    if !e["web"]["me"].is_null() { t.push_str(&format!("/v1/me: HTTP {}, pais {}, producto {}\r\n", e["web"]["me"]["estado"], e["web"]["me"]["pais"], e["web"]["me"]["producto"])); }
    t.push_str(&format!("Calidad medida en la cache de Soloist: {}\r\n", resumen_calidades(app)));
    for l in app.soloist.lock().unwrap().formato.iter() { t.push_str(&format!("Soloist (formato): {l}\r\n")); }
    t.push_str(&format!("Ruta de soloist.exe: {}\r\nUltimo error de Soloist: {}\r\nUltimo error de tu cuenta: {}\r\n\r\nDIAGNOSTICO (hora, cuanto tardo, resultado):\r\n",
        e["soloist"]["exe"].as_str().unwrap_or(""), limpia(e["soloist"]["ultimo_error"].as_str().unwrap_or("")), limpia(e["web"]["error"].as_str().unwrap_or(""))));
    for d in app.diag.lock().unwrap().iter() {
        t.push_str(&format!("{}  {:>6} ms  {}  {}  {}\r\n", d.hora, d.ms, if d.ok { "BIEN" } else { "MAL " }, d.que, d.msg));
    }
    t
}

// ---------- EL SERVIDOR LOCAL ----------
fn decodifica(s: &str) -> String {
    let b = s.as_bytes();
    let mut o = Vec::new();
    let mut i = 0;
    while i < b.len() {
        match b[i] {
            b'+' => o.push(b' '),
            b'%' if i + 2 < b.len() => { if let Ok(x) = u8::from_str_radix(&s[i + 1..i + 3], 16) { o.push(x); i += 2; } else { o.push(b'%'); } }
            c => o.push(c),
        }
        i += 1;
    }
    String::from_utf8_lossy(&o).to_string()
}
fn consulta(q: &str) -> HashMap<String, String> {
    q.split('&').filter(|x| !x.is_empty()).map(|kv| { let (k, v) = kv.split_once('=').unwrap_or((kv, "")); (decodifica(k), decodifica(v)) }).collect()
}

async fn ruta(app: &Arc<App>, post: bool, camino: &str, q: &HashMap<String, String>, cuerpo: &str) -> Value {
    let v: Value = serde_json::from_str(cuerpo).unwrap_or(Value::Null);
    match (post, camino) {
        (false, "/api/estado") => estado_json(app),
        (false, "/api/diag") => diag_json(app),
        (false, "/api/spotify") => api_pagina(app, q.get("ruta").map(|s| s.as_str()).unwrap_or("")).await,
        (true, "/api/entrar_oauth") => entrar_oauth(app),
        (true, "/api/web/client_id") => {
            let id = v["id"].as_str().unwrap_or("").trim().to_string();
            if !id.is_empty() && !es_client_id(&id) { json!({"ok": false, "error": "El Client ID son 32 letras y numeros (0-9, a-f)."}) }
            else {
                let _ = if id.is_empty() { std::fs::remove_file(app.ruta_client_id()) } else { std::fs::write(app.ruta_client_id(), &id) };
                // La entrada guardada es del otro Client ID: no sirve con este.
                olvidar_web(app);
                *app.bloqueo.lock().unwrap() = None; // el 429 era del otro Client ID
                app.diag("Client ID de la API web", Instant::now(), Ok(if id.is_empty() { "el compartido de escritorio".into() } else { "el de tu app".into() }));
                json!({"ok": true})
            }
        }
        (true, "/api/olvidar_web") => { if v["confirmo"].as_bool() == Some(true) { olvidar_web(app) } else { json!({"ok": false, "error": "falta confirmar"}) } }
        (true, "/api/soloist/config") => {
            let exe = v["exe"].as_str().unwrap_or("").trim().to_string();
            let nombre = v["nombre"].as_str().unwrap_or("").trim().to_string();
            guarda_config_soloist(app, &exe, if nombre.is_empty() { NOMBRE_APARATO_DEFECTO } else { &nombre });
            json!({"ok": true})
        }
        (true, "/api/soloist/llave") => {
            let llave = v["llave"].as_str().unwrap_or("").trim().to_string();
            if llave.is_empty() { json!({"ok": false, "error": "llave vacia"}) }
            else { match std::fs::write(app.ruta_llave_soloist(), &llave) { Ok(()) => json!({"ok": true}), Err(e) => json!({"ok": false, "error": e.to_string()}) } }
        }
        (true, "/api/soloist/detectar") => json!({"ok": true, "candidatos": detecta_soloist(&app.exe_dir)}),
        (true, "/api/soloist/iniciar") => iniciar_soloist(app),
        (true, "/api/soloist/detener") => { detener_soloist(app); json!({"ok": true}) }
        (true, "/api/soloist/reemparejar") => reemparejar(app),
        (true, "/api/soloist/olvidar") => { if v["confirmo"].as_bool() == Some(true) { olvidar_soloist(app) } else { json!({"ok": false, "error": "falta confirmar"}) } }
        (true, "/api/soloist/volumen") => {
            let vol = v["v"].as_u64().unwrap_or(100).min(100);
            let (exe, nombre) = { let mut s = app.soloist.lock().unwrap(); s.vol_rocola = vol as u8; (s.exe.clone(), s.nombre_aparato.clone()) };
            guarda_config_soloist(app, &exe, &nombre);
            if envia_comando(app, json!({"type": "command", "command": "set_volume", "volume": vol})) { json!({"ok": true}) } else { json!({"ok": false, "error": "Soloist no esta conectado."}) }
        }
        (true, "/api/soloist/vaciar_cache") => vaciar_cache_soloist(app),
        (true, "/api/soloist/verbose") => {
            let on = v["on"].as_bool().unwrap_or(false);
            let (exe, nombre) = { let mut s = app.soloist.lock().unwrap(); s.verbose = on; (s.exe.clone(), s.nombre_aparato.clone()) };
            guarda_config_soloist(app, &exe, &nombre);
            json!({"ok": true})
        }
        (true, "/api/tocar") => tocar(app, &v).await,
        (true, "/api/parar") => parar(app),
        (true, "/api/guardar_informe") => {
            let f = app.exe_dir.join("informe_spotify_soloist.txt");
            match std::fs::write(&f, informe_texto(app)) { Ok(()) => json!({"ok": true, "archivo": f.display().to_string()}), Err(e) => json!({"ok": false, "error": format!("no pude escribir el informe: {e}")}) }
        }
        (true, "/api/salir") => {
            detener_soloist(app);
            std::thread::spawn(|| { std::thread::sleep(Duration::from_millis(300)); println!("Cerrado desde la pagina."); std::process::exit(0); });
            json!({"ok": true})
        }
        _ => json!({"ok": false, "error": "no existe"}),
    }
}

fn atiende(app: &Arc<App>, h: &tokio::runtime::Handle, req: tiny_http::Request) {
    use tiny_http::{Header, Response};
    let url = req.url().to_string();
    let post = *req.method() == tiny_http::Method::Post;
    let hdr = |nombre: &str| req.headers().iter().find(|x| x.field.as_str().as_str().eq_ignore_ascii_case(nombre)).map(|x| x.value.as_str().to_string());
    let host_ok = hdr("Host").map(|v| v == format!("127.0.0.1:{}", app.puerto) || v == format!("localhost:{}", app.puerto)).unwrap_or(false);
    let marca = hdr("X-Prueba").is_some();
    let (camino, q) = url.split_once('?').unwrap_or((url.as_str(), ""));
    let q = consulta(q);
    let mut req = req;
    let enviar = |req: tiny_http::Request, codigo: u16, tipo: &str, cuerpo: String| {
        let r = Response::from_string(cuerpo).with_status_code(codigo)
            .with_header(Header::from_bytes("Content-Type", tipo).unwrap())
            .with_header(Header::from_bytes("Cache-Control", "no-store").unwrap());
        let _ = req.respond(r);
    };
    if !host_ok { return enviar(req, 403, "text/plain; charset=utf-8", "Solo desde esta misma computadora.".into()); }
    if !post && (camino == "/" || camino == "/index.html") { return enviar(req, 200, "text/html; charset=utf-8", PAGINA.to_string()); }
    if !camino.starts_with("/api/") { return enviar(req, 404, "text/plain; charset=utf-8", "No existe".into()); }
    if !marca { return enviar(req, 403, "text/plain; charset=utf-8", "Falta la marca de la pagina.".into()); }
    let mut cuerpo = String::new();
    if post { use std::io::Read; let _ = req.as_reader().take(1 << 20).read_to_string(&mut cuerpo); }
    let r = h.block_on(ruta(app, post, camino, &q, &cuerpo));
    enviar(req, 200, "application/json; charset=utf-8", r.to_string());
}

fn abre_navegador(url: &str) {
    if std::env::var("PRUEBA_NO_ABRIR").is_ok() { return; }
    #[cfg(windows)]
    { use std::os::windows::process::CommandExt; let _ = Command::new("cmd").args(["/C", "start", "", url]).creation_flags(0x0800_0000).spawn(); }
    #[cfg(not(windows))]
    { let _ = Command::new("xdg-open").arg(url).spawn(); }
}

#[tokio::main]
async fn main() {
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("warn")).init();
    let exe_dir = std::env::current_exe().ok().and_then(|p| p.parent().map(|d| d.to_path_buf())).unwrap_or_else(|| PathBuf::from("."));
    let datos = std::env::var("PRUEBA_DATOS").map(PathBuf::from).unwrap_or_else(|_| exe_dir.join("datos_spotify_soloist"));
    let _ = std::fs::create_dir_all(&datos);
    println!("==============================================================");
    println!("  DEMO DE LA ROCOLA CON SPOTIFY + SOLOIST (prueba de conexion)");
    println!("==============================================================");

    let mut servidor = None;
    let mut puerto = 0;
    for p in 8744..8764u16 { if let Ok(s) = tiny_http::Server::http(("127.0.0.1", p)) { servidor = Some(s); puerto = p; break; } }
    let Some(servidor) = servidor else { println!("No encontre un puerto libre entre 8744 y 8763."); std::process::exit(1); };

    let http = reqwest::Client::builder().timeout(Duration::from_secs(15)).build().expect("cliente http");
    let app = Arc::new(App {
        exe_dir, datos, puerto, http,
        diag: Mutex::new(Vec::new()), api_cache: Mutex::new(HashMap::new()), bloqueo: Mutex::new(None),
        web: Mutex::new(WebEstado::default()), oauth_cancelar: std::sync::atomic::AtomicBool::new(false), oauth_gen: std::sync::atomic::AtomicU64::new(0),
        soloist: Mutex::new(SoloistEstado::default()), soloist_child: Mutex::new(None), soloist_tx: Mutex::new(None),
        soloist_gen: std::sync::atomic::AtomicU64::new(0),
        calidad: Mutex::new(CalidadEstado::default()), rt: tokio::runtime::Handle::current(),
    });
    carga_config_soloist(&app);
    if let Some(h) = std::fs::read_to_string(app.ruta_calidad()).ok().and_then(|s| serde_json::from_str(&s).ok()) {
        app.calidad.lock().unwrap().hechas = h;
    }
    { let a = app.clone(); std::thread::spawn(move || vigila_calidad(a)); }

    let url = format!("http://127.0.0.1:{puerto}/");
    println!("  La pagina esta en: {url}");
    println!("  (se abre sola en tu navegador; si no, copia esa direccion)");
    println!("  Para CERRAR: cierra esta ventana negra, o pulsa Ctrl+C aqui,");
    println!("  o usa el boton «Cerrar» de la pestana Pruebas.");
    println!("  Solo se escucha en esta computadora (127.0.0.1).");
    println!("  OAUTH_PUERTO usado para entrar con el navegador: {OAUTH_PUERTO} (no lo ocupes con otra cosa).");
    println!("--------------------------------------------------------------");

    let servidor = Arc::new(servidor);
    let h = tokio::runtime::Handle::current();
    for _ in 0..6 {
        let (s, a, h) = (servidor.clone(), app.clone(), h.clone());
        std::thread::spawn(move || { while let Ok(r) = s.recv() { atiende(&a, &h, r); } });
    }
    abre_navegador(&url);

    // Si ya hay una entrada guardada (refresh token) o Soloist ya tiene ruta+llave, intenta solo, sin que el dueno
    // tenga que tocar nada.
    if app.ruta_refresh_web().exists() {
        if let Err(e) = asegura_token_web(&app).await { app.web.lock().unwrap().error = e; }
    }
    let (exe_lista, llave_lista) = { let s = app.soloist.lock().unwrap(); (!s.exe.trim().is_empty() && Path::new(&s.exe).is_file(), tiene_llave_soloist(&app)) };
    if exe_lista && llave_lista { iniciar_soloist(&app); }

    // Mantener el programa vivo (el servidor corre en otros hilos). Ctrl+C lo cierra (comportamiento normal del SO).
    loop { tokio::time::sleep(Duration::from_secs(3600)).await; }
}

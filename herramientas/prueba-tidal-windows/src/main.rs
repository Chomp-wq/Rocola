// PRUEBA DE TIDAL PARA LA ROCOLA (Windows). Contesta UNA pregunta antes de hacer la demo entera: con una app de programador de
// developer.tidal.com y tu cuenta, ¿TIDAL deja tocar canciones COMPLETAS (no muestras de 30 s) y en que calidad (FLAC, bits, kHz)?
//
// Como funciona: este programa sirve una pagina en http://127.0.0.1:8760/ (puerto FIJO: es la direccion de regreso que se registra
// en la app de TIDAL) con el SDK oficial de TIDAL para la web sin cambios (@tidal-music/auth y @tidal-music/player, Apache-2.0,
// empaquetados en src/tidal_sdk.js). El sonido sale del navegador (el reproductor oficial, con su DRM). El programa solo hace de
// puente para la API y la entrada de TIDAL (/tx/openapi, /tx/auth, /tx/api): los servidores de TIDAL pueden no contestar a una
// pagina de 127.0.0.1 (CORS), y el SDK deja cambiar esas direcciones con sus funciones publicas (setApiUrl, setLegacyApiUrl,
// tidalAuthServiceBaseUri). El programa NO guarda llaves: las guarda el SDK en el navegador.
use std::{
    io::Read,
    path::PathBuf,
    sync::Arc,
    time::{Duration, Instant},
};

use serde_json::json;
use tiny_http::{Header, Method, Response, Server};

const PUERTO: u16 = 8760;
const PAGINA: &str = include_str!("pagina.html");
const SDK: &[u8] = include_bytes!("tidal_sdk.js");

struct App {
    http: reqwest::Client,
    exe_dir: PathBuf,
    rt: tokio::runtime::Handle,
}

// Las lineas de la ventana negra van por un hilo aparte (un clic en la consola de Windows congela al que escribe).
static CONSOLA: std::sync::OnceLock<std::sync::mpsc::Sender<String>> = std::sync::OnceLock::new();
fn consola(linea: String) {
    let hora = chrono::Local::now().format("%H:%M:%S");
    match CONSOLA.get() { Some(tx) => { let _ = tx.send(format!("[{hora}] {linea}")); } None => println!("[{hora}] {linea}") }
}
fn inicia_consola() {
    let (tx, rx) = std::sync::mpsc::channel::<String>();
    if CONSOLA.set(tx).is_ok() {
        std::thread::spawn(move || {
            use std::io::Write;
            for l in rx { let _ = writeln!(std::io::stdout().lock(), "{l}"); }
        });
    }
}
#[cfg(windows)]
fn sin_seleccion_rapida() {
    #[link(name = "kernel32")]
    extern "system" {
        fn GetStdHandle(n: u32) -> isize;
        fn GetConsoleMode(h: isize, modo: *mut u32) -> i32;
        fn SetConsoleMode(h: isize, modo: u32) -> i32;
    }
    unsafe {
        let h = GetStdHandle(0xFFFF_FFF6);
        let mut m = 0u32;
        if h != 0 && h != -1 && GetConsoleMode(h, &mut m) != 0 { SetConsoleMode(h, (m & !0x0040) | 0x0080); }
    }
}
#[cfg(not(windows))]
fn sin_seleccion_rapida() {}

/// A que servidor de TIDAL va cada prefijo del puente. PRUEBA_TIDAL (solo para probar) manda todo a un servidor falso.
fn destino(camino: &str) -> Option<String> {
    let (pref, resto) = if let Some(r) = camino.strip_prefix("/tx/openapi/") { ("https://openapi.tidal.com/", r) }
        else if let Some(r) = camino.strip_prefix("/tx/auth/") { ("https://auth.tidal.com/", r) }
        else if let Some(r) = camino.strip_prefix("/tx/api/") { ("https://api.tidal.com/", r) }
        else { return None };
    if resto.contains("..") { return None; }
    let base = match std::env::var("PRUEBA_TIDAL") {
        Ok(b) => format!("{b}/{}/", pref.trim_start_matches("https://").split('.').next().unwrap_or("x")),
        Err(_) => pref.to_string(),
    };
    Some(format!("{base}{resto}"))
}

fn puente(app: &App, mut req: tiny_http::Request, url_entera: &str) {
    let (camino, q) = url_entera.split_once('?').unwrap_or((url_entera, ""));
    let Some(mut url) = destino(camino) else { return manda(req, 404, "text/plain", b"No existe".to_vec()) };
    if !q.is_empty() { url = format!("{url}?{q}"); }
    let metodo = req.method().as_str().to_string();
    let mut cuerpo = Vec::new();
    let _ = req.as_reader().take(4 << 20).read_to_end(&mut cuerpo);
    // Se pasan los encabezados de la peticion menos los del navegador que no son de TIDAL (Origin, Referer, cookies, Host...).
    let mut cab = reqwest::header::HeaderMap::new();
    for h in req.headers() {
        let n = h.field.as_str().as_str().to_ascii_lowercase();
        if matches!(n.as_str(), "host" | "origin" | "referer" | "cookie" | "connection" | "content-length" | "accept-encoding"
            | "sec-fetch-site" | "sec-fetch-mode" | "sec-fetch-dest" | "sec-ch-ua" | "sec-ch-ua-mobile" | "sec-ch-ua-platform") { continue; }
        if let (Ok(k), Ok(v)) = (reqwest::header::HeaderName::from_bytes(n.as_bytes()), reqwest::header::HeaderValue::from_str(h.value.as_str())) {
            cab.insert(k, v);
        }
    }
    let t0 = Instant::now();
    let m = reqwest::Method::from_bytes(metodo.as_bytes()).unwrap_or(reqwest::Method::GET);
    let res = app.rt.block_on(async {
        let r = app.http.request(m, &url).headers(cab).body(cuerpo).send().await?;
        let st = r.status().as_u16();
        let tipo = r.headers().get("content-type").and_then(|v| v.to_str().ok()).unwrap_or("application/octet-stream").to_string();
        let espera = r.headers().get("retry-after").and_then(|v| v.to_str().ok()).map(String::from);
        let b = r.bytes().await?.to_vec();
        Ok::<_, reqwest::Error>((st, tipo, espera, b))
    });
    // En la ventana negra: metodo, servidor y camino SIN la consulta (ahi no van llaves, pero tampoco hace falta).
    let corto = camino.trim_start_matches("/tx/");
    match res {
        Ok((st, tipo, espera, b)) => {
            consola(format!("{} {} {metodo} {corto} -> {st} ({} ms)", if st < 400 { "BIEN" } else { "MAL " }, "TIDAL", t0.elapsed().as_millis()));
            let mut r = Response::from_data(b).with_status_code(st).with_header(Header::from_bytes("Content-Type", tipo).unwrap())
                .with_header(Header::from_bytes("Cache-Control", "no-store").unwrap());
            if let Some(e) = espera { if let Ok(h) = Header::from_bytes("Retry-After", e) { r = r.with_header(h); } }
            let _ = req.respond(r);
        }
        Err(e) => {
            consola(format!("MAL  TIDAL {metodo} {corto} -> sin respuesta: {e}"));
            manda(req, 502, "application/json", json!({"errors": [{"code": "PUENTE", "detail": format!("La herramienta no pudo hablar con TIDAL: {e}")}]}).to_string().into_bytes());
        }
    }
}

fn manda(req: tiny_http::Request, st: u16, tipo: &str, b: Vec<u8>) {
    let _ = req.respond(Response::from_data(b).with_status_code(st).with_header(Header::from_bytes("Content-Type", tipo).unwrap())
        .with_header(Header::from_bytes("Cache-Control", "no-store").unwrap()));
}

fn atiende(app: &App, mut req: tiny_http::Request) {
    let host = req.headers().iter().find(|h| h.field.as_str().as_str().eq_ignore_ascii_case("host")).map(|h| h.value.as_str().to_string()).unwrap_or_default();
    if host != format!("127.0.0.1:{PUERTO}") && host != format!("localhost:{PUERTO}") {
        return manda(req, 403, "text/plain; charset=utf-8", "Solo desde esta misma computadora.".into());
    }
    let url = req.url().to_string();
    let camino = url.split('?').next().unwrap_or("").to_string();
    if camino.starts_with("/tx/") { return puente(app, req, &url); }
    let post = *req.method() == Method::Post;
    match (post, camino.as_str()) {
        // La pagina (tambien al volver de entrar en TIDAL: /?code=...).
        (false, "/") | (false, "/index.html") => manda(req, 200, "text/html; charset=utf-8", PAGINA.as_bytes().to_vec()),
        (false, "/tidal_sdk.js") => manda(req, 200, "text/javascript; charset=utf-8", SDK.to_vec()),
        (true, "/api/anota") => {
            // Lo que la pagina quiere ver en la ventana negra (sin llaves: la pagina no manda tokens).
            let mut t = String::new();
            let _ = req.as_reader().take(4096).read_to_string(&mut t);
            consola(t.replace(['\r', '\n'], " "));
            manda(req, 200, "application/json", b"{\"ok\":true}".to_vec());
        }
        (true, "/api/informe") => {
            let mut t = String::new();
            let _ = req.as_reader().take(1 << 20).read_to_string(&mut t);
            let f = app.exe_dir.join("informe_tidal.txt");
            let r = match std::fs::write(&f, t) { Ok(()) => json!({"ok": true, "archivo": f.display().to_string()}), Err(e) => json!({"ok": false, "error": e.to_string()}) };
            manda(req, 200, "application/json", r.to_string().into_bytes());
        }
        (true, "/api/salir") => {
            manda(req, 200, "application/json", b"{\"ok\":true}".to_vec());
            std::thread::spawn(|| { std::thread::sleep(Duration::from_millis(300)); std::process::exit(0); });
        }
        _ => manda(req, 404, "text/plain", b"No existe".to_vec()),
    }
}

fn abre_navegador(url: &str) {
    if std::env::var("PRUEBA_NO_ABRIR").is_ok() { return; }
    #[cfg(windows)]
    { use std::os::windows::process::CommandExt; let _ = std::process::Command::new("cmd").args(["/C", "start", "", url]).creation_flags(0x0800_0000).spawn(); }
    #[cfg(not(windows))]
    { let _ = std::process::Command::new("xdg-open").arg(url).spawn(); }
}

#[tokio::main]
async fn main() {
    sin_seleccion_rapida();
    inicia_consola();
    println!("==============================================================");
    println!("  PRUEBA DE TIDAL PARA LA ROCOLA");
    println!("  ¿Canciones completas? ¿En que calidad?");
    println!("==============================================================");
    let servidor = match Server::http(("127.0.0.1", PUERTO)) {
        Ok(s) => s,
        Err(e) => {
            println!("  No pude usar el puerto {PUERTO} ({e}).");
            println!("  ¿Ya esta abierta la prueba en otra ventana? Cierrala y vuelve a abrir esta.");
            println!("  (El puerto es fijo: es el de la direccion de regreso registrada en tu app de TIDAL.)");
            std::thread::sleep(Duration::from_secs(20));
            std::process::exit(1);
        }
    };
    let exe_dir = std::env::current_exe().ok().and_then(|p| p.parent().map(|d| d.to_path_buf())).unwrap_or_else(|| PathBuf::from("."));
    let http = reqwest::Client::builder().timeout(Duration::from_secs(30)).connect_timeout(Duration::from_secs(8))
        .pool_idle_timeout(Duration::from_secs(300)).redirect(reqwest::redirect::Policy::none()).build().expect("cliente http");
    let app = Arc::new(App { http, exe_dir, rt: tokio::runtime::Handle::current() });
    let url = format!("http://127.0.0.1:{PUERTO}/");
    println!("  La pagina esta en: {url}");
    println!("  (se abre sola; si no, copia esa direccion en Chrome o Edge)");
    println!("  Para CERRAR: cierra esta ventana negra.");
    println!("--------------------------------------------------------------");
    let servidor = Arc::new(servidor);
    for _ in 0..16 {
        let (s, a) = (servidor.clone(), app.clone());
        std::thread::spawn(move || { while let Ok(r) = s.recv() { atiende(&a, r); } });
    }
    abre_navegador(&url);
    loop { tokio::time::sleep(Duration::from_secs(3600)).await; }
}

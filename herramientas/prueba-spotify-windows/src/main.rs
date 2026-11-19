// DEMO DE LA ROCOLA CON SPOTIFY, PARA WINDOWS (herramienta de prueba del dueno, 19-11-2026).
// Un solo .exe, basado en sistema/rocola-spotify/src/main.rs (copiado y adaptado; no se enlaza). Levanta un servidor SOLO en 127.0.0.1 con una
// pagina (embebida) y abre el navegador. La pagina tiene dos pestanas: «Rocola» (una demo basica de la rocola con musica solo de Spotify)
// y «Pruebas» (estado, entrar, llave de la API, diagnostico). El sonido sale por la bocina de Windows (rodio/cpal/WASAPI).
// NUNCA se escriben llaves ni tokens en pantalla ni en el informe. No se guarda musica.
use std::{
    collections::HashMap,
    path::PathBuf,
    sync::{atomic::{AtomicBool, Ordering}, Arc, Mutex},
    time::{Duration, Instant},
};

use bytes::Bytes;
use futures_util::StreamExt;
use http::{header, Method, Request};
use librespot_core::{
    authentication::Credentials, cache::Cache, config::DeviceType, config::SessionConfig, session::Session,
    spotify_id::SpotifyId, spotify_uri::SpotifyUri,
};
use librespot_oauth::OAuthClientBuilder;
use librespot_playback::{
    audio_backend,
    config::{AudioFormat, Bitrate, NormalisationMethod, PlayerConfig},
    mixer::NoOpVolume,
    player::{Player, PlayerEvent},
};
use log::warn;
use serde_json::{json, Value};

const NOMBRE_CONNECT: &str = "Rocola (prueba)";
// Los mismos alcances de la rocola (main.rs).
const ALCANCE: &str = "user-read-private,playlist-read-private,playlist-read-collaborative,user-library-read,user-follow-read,user-top-read,user-read-recently-played";
const ALCANCE_BASICO: &str = "user-read-private,playlist-read-private,user-library-read";
// El cliente de escritorio de Spotify y su redireccion (lo mismo que usa librespot 0.8 con --enable-oauth).
const OAUTH_CLIENT_ID: &str = "65b708073fc0480ea92a077233ca87bd";
const OAUTH_PUERTO: u16 = 8898;
const OAUTH_REDIRECT: &str = "http://127.0.0.1:8898/login";
const PAGINA: &str = include_str!("pagina.html");
const MAX_DIAG: usize = 400;

// EL VOLUMEN DE CADA CANCION: lee del registro de librespot su ganancia (como main.rs de la rocola), solo para mostrarla.
#[derive(Default, Clone, Copy)]
struct Norm { gain_db: f64, peak: f64, n: u64 }
static NORM: Mutex<Norm> = Mutex::new(Norm { gain_db: 0.0, peak: 0.0, n: 0 });
struct Registro { dentro: env_logger::Logger }
impl log::Log for Registro {
    fn enabled(&self, m: &log::Metadata) -> bool { self.dentro.enabled(m) || (m.level() <= log::Level::Debug && m.target().starts_with("librespot_playback")) }
    fn log(&self, r: &log::Record) {
        if r.level() == log::Level::Debug && r.target().starts_with("librespot_playback") {
            let t = r.args().to_string();
            if let Some(resto) = t.strip_prefix("Normalisation Data: ") {
                let num = |k: &str| -> Option<f64> {
                    let i = resto.find(k)? + k.len();
                    let f: String = resto[i..].chars().take_while(|c| c.is_ascii_digit() || ".-eE".contains(*c)).collect();
                    f.parse().ok()
                };
                let mut n = NORM.lock().unwrap();
                n.gain_db = num("track_gain_db: ").unwrap_or(0.0);
                n.peak = num("track_peak: ").unwrap_or(0.0);
                n.n += 1;
            }
        }
        if self.dentro.enabled(r.metadata()) { self.dentro.log(r); }
    }
    fn flush(&self) { self.dentro.flush(); }
}

#[derive(Default, Clone)]
struct Sonando {
    uri: String, nombre: String, artista: String, img: String, dur_ms: u64,
    estado: String, // cargando | sonando | pausa | acabo | detenida | error
    aviso: String, pos_ms: u64, pos_t: Option<Instant>,
}

#[derive(Default)]
struct Estado {
    conectado: bool,
    esperando_connect: bool,
    connect_msg: String,
    oauth_activo: bool,
    entrando: String,
    usuario: String,
    error: String,
    via_llave: String, // login5 | keymaster
    me: Option<Value>,
    son: Sonando,
    tocar_inicio: Option<(Instant, String)>,
    precargada: String,
    bocina: String,
}

struct Diag { epoch_ms: u64, hora: String, que: String, ms: u64, ok: bool, msg: String }

struct App {
    exe_dir: PathBuf,
    datos: PathBuf,
    puerto: u16,
    est: Mutex<Estado>,
    diag: Mutex<Vec<Diag>>,
    sesion: Mutex<Option<Session>>,
    jugador: Mutex<Option<Arc<Player>>>,
    cache: Mutex<Cache>,
    config: SessionConfig,
    connect_tarea: Mutex<Option<tokio::task::JoinHandle<()>>>,
    oauth_cancelar: AtomicBool,
    api_cache: Mutex<HashMap<String, Value>>,
    bloqueo: Mutex<Option<Instant>>, // Spotify dijo 429: hasta cuando esperar
}

// Quita de un texto todo lo que parezca una llave (corridas largas de letras y numeros), para que NUNCA salga en pantalla ni en el informe.
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
    fn sesion(&self) -> Option<Session> { self.sesion.lock().unwrap().clone().filter(|s| !s.is_invalid()) }
    fn falta_entrar(&self) -> String {
        "Falta entrar: todavia no hay cuenta de Spotify (pestana Pruebas, seccion Entrar).".to_string()
    }
}

// ---------- LA LLAVE DE LA API WEB (como main.rs de la rocola: primero login5, si no keymaster) ----------
async fn llave_keymaster(se: &Session) -> Result<String, String> {
    match se.token_provider().get_token(ALCANCE).await {
        Ok(t) => Ok(t.access_token),
        Err(e) => match se.token_provider().get_token(ALCANCE_BASICO).await {
            Ok(t) => { warn!("la llave con todos los permisos no se dio ({e}): con los basicos"); Ok(t.access_token) }
            Err(e2) => Err(format!("{e}; basica: {e2}")),
        },
    }
}

// Devuelve (llave, via, error de login5 si lo hubo).
async fn llave_web(se: &Session) -> Result<(String, &'static str, Option<String>), String> {
    match se.login5().auth_token().await {
        Ok(t) => Ok((t.access_token.clone(), "login5", None)),
        Err(e5) => match llave_keymaster(se).await {
            Ok(t) => Ok((t, "keymaster", Some(format!("login5 fallo: {e5}")))),
            Err(e) => Err(format!("login5: {e5}; keymaster: {e}")),
        },
    }
}

// Una consulta GET a la API web: (estado HTTP, cuerpo, segundos de Retry-After si hubo).
async fn pide_api(se: &Session, ruta: &str, llave: &str) -> Result<(u16, String, Option<u64>), String> {
    let req = Request::builder()
        .method(Method::GET)
        .uri(format!("https://api.spotify.com{ruta}"))
        .header(header::AUTHORIZATION, format!("Bearer {llave}"))
        .header(header::ACCEPT, "application/json")
        .body(Bytes::new())
        .map_err(|e| e.to_string())?;
    let resp = se.http_client().request(req).await.map_err(|e| e.to_string())?;
    let estado = resp.status().as_u16();
    let espera = resp.headers().get("retry-after").and_then(|v| v.to_str().ok()).and_then(|v| v.trim().parse::<u64>().ok());
    use http_body_util::BodyExt;
    let b = resp.into_body().collect().await.map(|c| c.to_bytes()).map_err(|e| e.to_string())?;
    Ok((estado, String::from_utf8_lossy(&b).to_string(), espera))
}

// La API para la pagina: con memoria de lo ya pedido (cuota del modo desarrollo) y espera si Spotify contesta 429.
async fn api_pagina(app: &Arc<App>, ruta: &str) -> Value {
    let permitida = ["/v1/me", "/v1/albums/", "/v1/playlists/", "/v1/search", "/v1/artists/"].iter().any(|p| ruta.starts_with(p));
    if !permitida || ruta.contains("..") || ruta.contains(' ') { return json!({"ok": false, "error": "ruta no permitida"}); }
    if let Some(v) = app.api_cache.lock().unwrap().get(ruta) { return v.clone(); }
    if let Some(h) = *app.bloqueo.lock().unwrap() {
        let falta = h.saturating_duration_since(Instant::now()).as_secs() + 1;
        if h > Instant::now() { return json!({"ok": false, "estado": 429, "espera_s": falta, "error": format!("Spotify pide esperar {falta} s")}); }
    }
    let t0 = Instant::now();
    let Some(se) = app.sesion() else {
        let m = app.falta_entrar();
        app.diag(&format!("GET {}", corta(ruta)), t0, Err(m.clone()));
        return json!({"ok": false, "falta_entrar": true, "error": m});
    };
    let (llave, via, _) = match llave_web(&se).await {
        Ok(x) => x,
        Err(e) => {
            app.diag(&format!("GET {}", corta(ruta)), t0, Err(format!("sin llave de la API: {e}")));
            return json!({"ok": false, "error": format!("No se pudo obtener la llave de la API web: {}", limpia(&e))});
        }
    };
    app.est.lock().unwrap().via_llave = via.to_string();
    let mut res = pide_api(&se, ruta, &llave).await;
    if via == "login5" && matches!(res, Ok((401, _, _)) | Ok((403, _, _))) {
        if let Ok(t) = llave_keymaster(&se).await { res = pide_api(&se, ruta, &t).await; }
    }
    match res {
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
                app.diag(&format!("GET {}", corta(ruta)), t0, Ok(format!("HTTP {estado}, llave por {via}")));
                let v = json!({"ok": true, "estado": estado, "datos": cuerpo_v});
                app.api_cache.lock().unwrap().insert(ruta.to_string(), v.clone());
                v
            } else {
                let m = cuerpo_v["error"]["message"].as_str().unwrap_or("").to_string();
                app.diag(&format!("GET {}", corta(ruta)), t0, Err(format!("HTTP {estado} {m}")));
                json!({"ok": false, "estado": estado, "error": format!("Spotify contesto {estado}: {}", limpia(&m))})
            }
        }
        Err(e) => {
            app.diag(&format!("GET {}", corta(ruta)), t0, Err(e.clone()));
            json!({"ok": false, "error": format!("No hubo respuesta de Spotify: {}", limpia(&e))})
        }
    }
}
fn corta(s: &str) -> String { let t: String = s.chars().take(90).collect(); t }

// ---------- ENTRAR ----------
async fn conecta(app: &Arc<App>, c: Credentials) -> Result<(), String> {
    let cache = app.cache.lock().unwrap().clone();
    let sesion = Session::new(app.config.clone(), Some(cache));
    sesion.connect(c, true).await.map_err(|e| e.to_string())?; // true: guarda la llave para la proxima vez
    let usuario = sesion.username();
    {
        let mut j = app.jugador.lock().unwrap();
        match j.as_ref() {
            Some(p) => p.set_session(sesion.clone()),
            None => {
                let config = PlayerConfig {
                    bitrate: Bitrate::Bitrate320,
                    gapless: false,
                    normalisation: true, // como la rocola
                    normalisation_method: NormalisationMethod::Basic,
                    position_update_interval: Some(Duration::from_secs(1)),
                    ..PlayerConfig::default()
                };
                let p = Player::new(config, sesion.clone(), Box::new(NoOpVolume), move || {
                    (audio_backend::find(Some("rodio".to_string())).expect("sin salida rodio"))(None, AudioFormat::F32)
                });
                let mut eventos = p.get_player_event_channel();
                let a2 = app.clone();
                tokio::spawn(async move {
                    while let Some(e) = eventos.recv().await { evento(&a2, e); }
                });
                *j = Some(p);
            }
        }
    }
    *app.sesion.lock().unwrap() = Some(sesion);
    let mut s = app.est.lock().unwrap();
    s.conectado = true;
    s.usuario = usuario;
    s.error.clear();
    s.esperando_connect = false;
    Ok(())
}

fn uri_de(id: &SpotifyUri) -> String { id.to_uri().unwrap_or_default() }

// Lo que dice el reproductor: actualiza lo que suena y apunta en el diagnostico cuando empieza, acaba o no se puede.
fn evento(app: &Arc<App>, e: PlayerEvent) {
    let mut diag: Option<(String, Instant, Result<String, String>)> = None;
    {
        let mut s = app.est.lock().unwrap();
        let actual = s.son.uri.clone();
        match e {
            PlayerEvent::Loading { track_id, .. } if uri_de(&track_id) == actual => s.son.estado = "cargando".into(),
            PlayerEvent::Playing { track_id, position_ms, .. } => {
                if uri_de(&track_id) == actual {
                    s.son.estado = "sonando".into();
                    s.son.pos_ms = position_ms as u64;
                    s.son.pos_t = Some(Instant::now());
                    if let Some((t0, u)) = s.tocar_inicio.clone() {
                        if u == actual {
                            s.tocar_inicio = None;
                            diag = Some(("tocar".into(), t0, Ok(format!("empezo a sonar ({})", s.son.nombre))));
                        }
                    }
                }
            }
            PlayerEvent::Paused { track_id, position_ms, .. } if uri_de(&track_id) == actual => {
                s.son.estado = "pausa".into(); s.son.pos_ms = position_ms as u64; s.son.pos_t = None;
            }
            PlayerEvent::PositionChanged { track_id, position_ms, .. } | PlayerEvent::PositionCorrection { track_id, position_ms, .. } | PlayerEvent::Seeked { track_id, position_ms, .. }
                if uri_de(&track_id) == actual => { s.son.pos_ms = position_ms as u64; s.son.pos_t = Some(Instant::now()); }
            PlayerEvent::EndOfTrack { track_id, .. } if uri_de(&track_id) == actual => {
                s.son.estado = "acabo".into();
                diag = Some(("fin de la cancion".into(), Instant::now(), Ok(format!("acabo entera ({})", s.son.nombre))));
            }
            PlayerEvent::Unavailable { track_id, .. } if uri_de(&track_id) == actual => {
                s.son.estado = "error".into();
                s.son.aviso = "Spotify no deja tocar esta cancion (no disponible en tu pais o cuenta).".into();
                let t0 = s.tocar_inicio.take().map(|x| x.0).unwrap_or_else(Instant::now);
                diag = Some(("tocar".into(), t0, Err(format!("no disponible ({})", s.son.nombre))));
            }
            PlayerEvent::Stopped { track_id, .. } if uri_de(&track_id) == actual => {
                if s.son.estado != "acabo" && s.son.estado != "error" { s.son.estado = "detenida".into(); }
            }
            PlayerEvent::TrackChanged { audio_item } if uri_de(&audio_item.track_id) == actual => {
                if audio_item.duration_ms > 0 { s.son.dur_ms = audio_item.duration_ms as u64; }
            }
            _ => {}
        }
    }
    if let Some((q, t0, r)) = diag { app.diag(&q, t0, r); }
}

// Entrar con unas credenciales; si sale bien, se apagan las otras formas de entrar.
async fn entra_con(app: &Arc<App>, c: Credentials, via: &str) -> bool {
    let t0 = Instant::now();
    app.est.lock().unwrap().entrando = via.to_string();
    let r = conecta(app, c).await;
    app.est.lock().unwrap().entrando.clear();
    match r {
        Ok(()) => {
            app.diag(&format!("entrar ({via})"), t0, Ok("dentro de Spotify; la llave quedo guardada en datos_spotify".into()));
            if let Some(h) = app.connect_tarea.lock().unwrap().take() { h.abort(); }
            cancela_oauth(app);
            true
        }
        Err(e) => {
            let msg = format!("no pude entrar a Spotify ({via}): {}", limpia(&e));
            app.diag(&format!("entrar ({via})"), t0, Err(e.clone()));
            app.est.lock().unwrap().error = msg;
            if e.contains("BadCredentials") || e.contains("Bad credentials") {
                let _ = std::fs::remove_file(app.datos.join("credentials.json"));
            }
            false
        }
    }
}

// Si el navegador aun esta esperando (la forma OAuth), se le manda una visita falsa para que suelte el puerto.
fn cancela_oauth(app: &Arc<App>) {
    if app.est.lock().unwrap().oauth_activo {
        app.oauth_cancelar.store(true, Ordering::SeqCst);
        if let Ok(mut s) = std::net::TcpStream::connect(("127.0.0.1", OAUTH_PUERTO)) {
            use std::io::Write;
            let _ = s.write_all(b"GET /login?cancelar=1 HTTP/1.1\r\n\r\n");
        }
    }
}

// FORMA A: anunciarse por Spotify Connect (como la rocola) y esperar que el celular entregue la cuenta una vez.
fn lanza_connect(app: &Arc<App>) {
    if let Some(h) = app.connect_tarea.lock().unwrap().take() { h.abort(); }
    let a = app.clone();
    let h = tokio::spawn(async move {
        let mut avisado = false;
        loop {
            a.est.lock().unwrap().esperando_connect = true;
            let t0 = Instant::now();
            match librespot_discovery::Discovery::builder(a.config.device_id.clone(), a.config.client_id.clone())
                .name(NOMBRE_CONNECT.to_string())
                .device_type(DeviceType::Speaker)
                .launch()
            {
                Ok(mut d) => {
                    a.est.lock().unwrap().connect_msg = format!("Anunciado en la red como «{NOMBRE_CONNECT}»: abre Spotify en el celular y elige ese aparato.");
                    if !avisado { avisado = true; a.diag("anuncio Spotify Connect", t0, Ok(format!("anunciado como {NOMBRE_CONNECT}"))); }
                    if let Some(c) = d.next().await {
                        if entra_con(&a, c, "Connect").await { return; }
                    }
                }
                Err(e) => {
                    let m = format!("No pude anunciarme en la red (firewall o puerto 5353): {e}");
                    a.est.lock().unwrap().connect_msg = m.clone();
                    if !avisado { avisado = true; a.diag("anuncio Spotify Connect", t0, Err(m)); }
                }
            }
            tokio::time::sleep(Duration::from_secs(10)).await;
        }
    });
    *app.connect_tarea.lock().unwrap() = Some(h);
}

// FORMA B: entrar con el navegador (OAuth, como librespot 0.8 con --enable-oauth).
fn entrar_oauth(app: &Arc<App>) -> Value {
    if app.est.lock().unwrap().conectado { return json!({"ok": false, "error": "Ya estas dentro."}); }
    {
        let mut s = app.est.lock().unwrap();
        if s.oauth_activo { return json!({"ok": false, "error": "Ya se esta esperando al navegador (termina de entrar ahi, o cierra esta herramienta y vuelve a abrirla)."}); }
        s.oauth_activo = true;
    }
    app.oauth_cancelar.store(false, Ordering::SeqCst);
    let a = app.clone();
    tokio::spawn(async move {
        let t0 = Instant::now();
        let r = tokio::task::spawn_blocking(|| {
            let cli = OAuthClientBuilder::new(OAUTH_CLIENT_ID, OAUTH_REDIRECT, vec!["streaming"])
                .open_in_browser()
                .with_custom_message("<!doctype html><meta charset=utf-8><body style='font-family:sans-serif;font-size:28px;background:#111;color:#eee'><h1>Listo</h1><p>Vuelve a la herramienta de la rocola. Puedes cerrar esta pestana.</p></body>")
                .build()
                .map_err(|e| e.to_string())?;
            cli.get_access_token().map_err(|e| e.to_string())
        }).await;
        let r = match r { Ok(x) => x, Err(e) => Err(e.to_string()) };
        let cancelada = a.oauth_cancelar.load(Ordering::SeqCst);
        match r {
            Ok(tok) if !cancelada => {
                a.diag("OAuth (navegador)", t0, Ok("el navegador entrego el permiso".into()));
                let c = Credentials::with_access_token(tok.access_token);
                a.est.lock().unwrap().oauth_activo = false;
                entra_con(&a, c, "navegador OAuth").await;
            }
            Ok(_) => { a.est.lock().unwrap().oauth_activo = false; }
            Err(e) => {
                a.est.lock().unwrap().oauth_activo = false;
                if !cancelada {
                    a.diag("OAuth (navegador)", t0, Err(e.clone()));
                    a.est.lock().unwrap().error = format!("OAuth: {}", limpia(&e));
                }
            }
        }
    });
    json!({"ok": true, "mensaje": "Se abrio el navegador. Inicia sesion en Spotify y acepta. Si no se abrio, copia la direccion que sale en la consola."})
}

async fn olvidar(app: &Arc<App>) -> Value {
    let t0 = Instant::now();
    if let Some(h) = app.connect_tarea.lock().unwrap().take() { h.abort(); }
    cancela_oauth(app);
    let p = app.jugador.lock().unwrap().take();
    if let Some(p) = p { p.stop(); }
    let se = app.sesion.lock().unwrap().take();
    if let Some(s) = se { s.shutdown(); }
    app.api_cache.lock().unwrap().clear();
    let r = std::fs::remove_dir_all(&app.datos);
    let _ = std::fs::create_dir_all(&app.datos);
    {
        let mut s = app.est.lock().unwrap();
        *s = Estado { bocina: s.bocina.clone(), ..Estado::default() };
    }
    app.diag("olvidar la cuenta", t0, r.map(|_| "carpeta datos_spotify borrada".to_string()).map_err(|e| e.to_string()));
    lanza_connect(app);
    json!({"ok": true})
}

// ---------- LA LLAVE Y /v1/me ----------
async fn prueba_llave(app: &Arc<App>) -> Value {
    let Some(se) = app.sesion() else {
        let m = app.falta_entrar();
        app.diag("llave de la API web", Instant::now(), Err(m.clone()));
        return json!({"ok": false, "error": m});
    };
    let t0 = Instant::now();
    let (llave, via) = match se.login5().auth_token().await {
        Ok(t) => { app.diag("llave de la API web (login5)", t0, Ok("la dio login5".into())); (t.access_token.clone(), "login5") }
        Err(e5) => {
            app.diag("llave de la API web (login5)", t0, Err(e5.to_string()));
            let t1 = Instant::now();
            match llave_keymaster(&se).await {
                Ok(t) => { app.diag("llave de la API web (keymaster)", t1, Ok("la dio keymaster".into())); (t, "keymaster") }
                Err(e) => {
                    app.diag("llave de la API web (keymaster)", t1, Err(e.clone()));
                    return json!({"ok": false, "error": format!("Ninguna de las dos llaves funciono: {}", limpia(&e))});
                }
            }
        }
    };
    app.est.lock().unwrap().via_llave = via.to_string();
    let t2 = Instant::now();
    match pide_api(&se, "/v1/me", &llave).await {
        Ok((estado, cuerpo, _)) => {
            let v: Value = serde_json::from_str(&cuerpo).unwrap_or(Value::Null);
            let me = json!({"estado": estado, "pais": v["country"], "producto": v["product"], "via": via});
            let ok = estado == 200;
            let prod = v["product"].as_str().unwrap_or("?").to_string();
            app.diag("GET /v1/me", t2, if ok { Ok(format!("HTTP 200, pais {}, producto {prod}", v["country"].as_str().unwrap_or("?"))) }
                                       else { Err(format!("HTTP {estado} {}", v["error"]["message"].as_str().unwrap_or(""))) });
            app.est.lock().unwrap().me = Some(me.clone());
            json!({"ok": ok, "me": me, "premium": prod == "premium"})
        }
        Err(e) => { app.diag("GET /v1/me", t2, Err(e.clone())); json!({"ok": false, "error": limpia(&e)}) }
    }
}

// ---------- TOCAR ----------
fn hay_bocina() -> Result<String, String> {
    use cpal::traits::{DeviceTrait, HostTrait};
    match cpal::default_host().default_output_device() {
        Some(d) => Ok(d.name().unwrap_or_else(|_| "(sin nombre)".into())),
        None => Err("Windows no tiene una bocina (salida de audio) predeterminada.".into()),
    }
}

async fn tocar(app: &Arc<App>, v: &Value) -> Value {
    let t0 = Instant::now();
    let uri = v["uri"].as_str().unwrap_or("").to_string();
    let nombre = v["nombre"].as_str().unwrap_or("").to_string();
    let id = match SpotifyUri::from_uri(&uri) { Ok(i) => i, Err(e) => return json!({"ok": false, "error": format!("uri: {e}")}) };
    if app.sesion().is_none() {
        let m = app.falta_entrar();
        app.diag("tocar", t0, Err(m.clone()));
        return json!({"ok": false, "error": m});
    }
    let bocina = match hay_bocina() {
        Ok(b) => b,
        Err(m) => { app.diag("tocar", t0, Err(m.clone())); return json!({"ok": false, "error": m}); }
    };
    let p = app.jugador.lock().unwrap().clone();
    let Some(p) = p else { return json!({"ok": false, "error": app.falta_entrar()}) };
    {
        let mut s = app.est.lock().unwrap();
        s.bocina = bocina;
        s.son = Sonando {
            uri: uri.clone(), nombre, artista: v["artista"].as_str().unwrap_or("").to_string(),
            img: v["img"].as_str().unwrap_or("").to_string(), dur_ms: v["dur_ms"].as_u64().unwrap_or(0),
            estado: "cargando".into(), ..Sonando::default()
        };
        s.tocar_inicio = Some((t0, uri));
    }
    p.load(id, true, 0);
    json!({"ok": true})
}

// Si una cancion pedida no empieza a sonar en 20 s, se da por no tocable (la pagina devuelve el credito).
fn revisa_tardanza(app: &Arc<App>) {
    let mut diag = None;
    {
        let mut s = app.est.lock().unwrap();
        if let Some((t0, _)) = s.tocar_inicio.clone() {
            if t0.elapsed() > Duration::from_secs(20) {
                s.tocar_inicio = None;
                s.son.estado = "error".into();
                s.son.aviso = "Spotify no empezo a tocar en 20 segundos.".into();
                diag = Some((t0, format!("no empezo en 20 s ({})", s.son.nombre)));
            }
        }
    }
    if let Some((t0, m)) = diag { app.diag("tocar", t0, Err(m)); }
}

async fn letra(app: &Arc<App>, id: &str) -> Value {
    let t0 = Instant::now();
    let Some(se) = app.sesion() else { return json!({"ok": false, "error": app.falta_entrar()}) };
    let sid = match SpotifyId::from_base62(id) { Ok(i) => i, Err(e) => return json!({"ok": false, "error": format!("id: {e}")}) };
    match se.spclient().get_lyrics(&sid).await {
        Ok(b) => {
            let v: Value = serde_json::from_slice(&b).unwrap_or(Value::Null);
            let sinc = v["lyrics"]["syncType"].as_str().unwrap_or("") == "LINE_SYNCED";
            let lineas: Vec<Value> = v["lyrics"]["lines"].as_array().map(|a| a.iter().map(|l| {
                let t = l["startTimeMs"].as_str().and_then(|x| x.parse::<u64>().ok()).or(l["startTimeMs"].as_u64()).unwrap_or(0);
                json!({"t": t, "w": l["words"].as_str().unwrap_or("")})
            }).collect()).unwrap_or_default();
            app.diag("letra", t0, Ok(format!("{} lineas, {}", lineas.len(), if sinc { "sincronizada" } else { "sin sincronizar" })));
            json!({"ok": true, "sincronizada": sinc, "lineas": lineas})
        }
        Err(e) => {
            app.diag("letra", t0, Err(e.to_string()));
            json!({"ok": false, "error": format!("Spotify no dio letra para esta cancion ({})", limpia(&e.to_string()))})
        }
    }
}

// ---------- ESTADO E INFORME ----------
fn estado_json(app: &Arc<App>) -> Value {
    revisa_tardanza(app);
    let s = app.est.lock().unwrap();
    let n = *NORM.lock().unwrap();
    let pos = s.son.pos_ms + if s.son.estado == "sonando" { s.son.pos_t.map(|t| t.elapsed().as_millis() as u64).unwrap_or(0) } else { 0 };
    json!({
        "version": env!("CARGO_PKG_VERSION"),
        "conectado": s.conectado && app.sesion().is_some(), "esperando_connect": s.esperando_connect, "connect_msg": s.connect_msg,
        "oauth_activo": s.oauth_activo, "entrando": s.entrando, "usuario": s.usuario, "error": s.error,
        "via_llave": s.via_llave, "me": s.me, "bocina": s.bocina, "precargada": s.precargada,
        "hay_llave_guardada": app.datos.join("credentials.json").exists(),
        "diag_n": app.diag.lock().unwrap().len(),
        "norm": {"n": n.n, "ganancia_db": n.gain_db, "pico": n.peak},
        "son": {"uri": s.son.uri, "nombre": s.son.nombre, "artista": s.son.artista, "img": s.son.img, "dur_ms": s.son.dur_ms,
                "estado": s.son.estado, "aviso": s.son.aviso, "pos_ms": pos},
    })
}

fn diag_json(app: &Arc<App>) -> Value {
    let v = app.diag.lock().unwrap();
    Value::Array(v.iter().map(|d| json!({"epoch": d.epoch_ms, "hora": d.hora, "que": d.que, "ms": d.ms, "ok": d.ok, "msg": d.msg})).collect())
}

fn informe_texto(app: &Arc<App>) -> String {
    let e = estado_json(app);
    let mut t = String::new();
    t.push_str("INFORME DE LA DEMO DE LA ROCOLA CON SPOTIFY (sin llaves ni tokens)\r\n");
    t.push_str(&format!("Hecho: {}\r\nVersion: {}\r\nSistema: {} {}\r\n", chrono::Local::now().format("%Y-%m-%d %H:%M:%S"), e["version"].as_str().unwrap_or(""), std::env::consts::OS, std::env::consts::ARCH));
    t.push_str(&format!("Dentro de Spotify: {}\r\nTiene llave guardada: {}\r\nLlave de la API web por: {}\r\n", e["conectado"], e["hay_llave_guardada"], e["via_llave"].as_str().unwrap_or("(sin probar)")));
    if !e["me"].is_null() { t.push_str(&format!("/v1/me: HTTP {}, pais {}, producto {}\r\n", e["me"]["estado"], e["me"]["pais"], e["me"]["producto"])); }
    t.push_str(&format!("Bocina: {}\r\nUltimo error: {}\r\n\r\nDIAGNOSTICO (hora, cuanto tardo, resultado):\r\n", e["bocina"].as_str().unwrap_or("(sin tocar aun)"), limpia(e["error"].as_str().unwrap_or(""))));
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
            b'%' if i + 2 < b.len() => {
                if let Ok(x) = u8::from_str_radix(&s[i + 1..i + 3], 16) { o.push(x); i += 2; } else { o.push(b'%'); }
            }
            c => o.push(c),
        }
        i += 1;
    }
    String::from_utf8_lossy(&o).to_string()
}
fn consulta(q: &str) -> HashMap<String, String> {
    q.split('&').filter(|x| !x.is_empty()).map(|kv| {
        let (k, v) = kv.split_once('=').unwrap_or((kv, ""));
        (decodifica(k), decodifica(v))
    }).collect()
}

async fn ruta(app: &Arc<App>, post: bool, camino: &str, q: &HashMap<String, String>, cuerpo: &str) -> Value {
    let v: Value = serde_json::from_str(cuerpo).unwrap_or(Value::Null);
    match (post, camino) {
        (false, "/api/estado") => estado_json(app),
        (false, "/api/diag") => diag_json(app),
        (false, "/api/spotify") => api_pagina(app, q.get("ruta").map(|s| s.as_str()).unwrap_or("")).await,
        (false, "/api/letra") => letra(app, q.get("id").map(|s| s.as_str()).unwrap_or("")).await,
        (true, "/api/entrar_oauth") => entrar_oauth(app),
        (true, "/api/entrar_connect") => { lanza_connect(app); json!({"ok": true}) }
        (true, "/api/olvidar") => {
            if v["confirmo"].as_bool() != Some(true) { json!({"ok": false, "error": "falta confirmar"}) } else { olvidar(app).await }
        }
        (true, "/api/llave") => prueba_llave(app).await,
        (true, "/api/tocar") => tocar(app, &v).await,
        (true, "/api/precarga") => {
            let t0 = Instant::now();
            let uri = v["uri"].as_str().unwrap_or("");
            match (SpotifyUri::from_uri(uri), app.jugador.lock().unwrap().clone()) {
                (Ok(id), Some(p)) => {
                    p.preload(id);
                    app.est.lock().unwrap().precargada = v["nombre"].as_str().unwrap_or("").to_string();
                    app.diag("precarga", t0, Ok(format!("pedida: {}", v["nombre"].as_str().unwrap_or(""))));
                    json!({"ok": true})
                }
                _ => json!({"ok": false, "error": app.falta_entrar()}),
            }
        }
        (true, "/api/parar") => {
            if let Some(p) = app.jugador.lock().unwrap().clone() { p.stop(); }
            json!({"ok": true})
        }
        (true, "/api/guardar_informe") => {
            let f = app.exe_dir.join("informe_spotify.txt");
            match std::fs::write(&f, informe_texto(app)) {
                Ok(()) => json!({"ok": true, "archivo": f.display().to_string()}),
                Err(e) => json!({"ok": false, "error": format!("no pude escribir el informe: {e}")}),
            }
        }
        (true, "/api/salir") => {
            std::thread::spawn(|| { std::thread::sleep(Duration::from_millis(300)); println!("Cerrado desde la pagina."); std::process::exit(0); });
            json!({"ok": true})
        }
        _ => json!({"ok": false, "error": "no existe"}),
    }
}

fn atiende(app: &Arc<App>, h: &tokio::runtime::Handle, mut req: tiny_http::Request) {
    use tiny_http::{Header, Response};
    let url = req.url().to_string();
    let post = *req.method() == tiny_http::Method::Post;
    let hdr = |nombre: &str| req.headers().iter().find(|x| x.field.as_str().as_str().eq_ignore_ascii_case(nombre)).map(|x| x.value.as_str().to_string());
    let host_ok = hdr("Host").map(|v| v == format!("127.0.0.1:{}", app.puerto) || v == format!("localhost:{}", app.puerto)).unwrap_or(false);
    let marca = hdr("X-Prueba").is_some();
    let (camino, q) = url.split_once('?').unwrap_or((url.as_str(), ""));
    let q = consulta(q);
    let enviar = |req: tiny_http::Request, codigo: u16, tipo: &str, cuerpo: String| {
        let r = Response::from_string(cuerpo).with_status_code(codigo)
            .with_header(Header::from_bytes("Content-Type", tipo).unwrap())
            .with_header(Header::from_bytes("Cache-Control", "no-store").unwrap());
        let _ = req.respond(r);
    };
    if !host_ok { return enviar(req, 403, "text/plain; charset=utf-8", "Solo desde esta misma computadora.".into()); }
    if !post && (camino == "/" || camino == "/index.html") {
        return enviar(req, 200, "text/html; charset=utf-8", PAGINA.to_string());
    }
    if !camino.starts_with("/api/") { return enviar(req, 404, "text/plain; charset=utf-8", "No existe".into()); }
    // Cualquier orden (POST o no) exige la marca de la propia pagina: otra pagina web no puede mandarla sin permiso.
    if !marca { return enviar(req, 403, "text/plain; charset=utf-8", "Falta la marca de la pagina.".into()); }
    let mut cuerpo = String::new();
    if post { use std::io::Read; let _ = req.as_reader().take(1 << 20).read_to_string(&mut cuerpo); }
    let r = h.block_on(ruta(app, post, camino, &q, &cuerpo));
    enviar(req, 200, "application/json; charset=utf-8", r.to_string());
}

fn abre_navegador(url: &str) {
    if std::env::var("PRUEBA_NO_ABRIR").is_ok() { return; }
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        let _ = std::process::Command::new("cmd").args(["/C", "start", "", url]).creation_flags(0x0800_0000).spawn();
    }
    #[cfg(not(windows))]
    { let _ = std::process::Command::new("xdg-open").arg(url).spawn(); }
}

#[tokio::main]
async fn main() {
    let dentro = env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("warn,librespot_playback=info")).build();
    let nivel = dentro.filter().max(log::LevelFilter::Debug);
    if log::set_boxed_logger(Box::new(Registro { dentro })).is_ok() { log::set_max_level(nivel); }

    let exe_dir = std::env::current_exe().ok().and_then(|p| p.parent().map(|d| d.to_path_buf())).unwrap_or_else(|| PathBuf::from("."));
    let datos = std::env::var("PRUEBA_DATOS").map(PathBuf::from).unwrap_or_else(|_| exe_dir.join("datos_spotify"));
    println!("==============================================================");
    println!("  DEMO DE LA ROCOLA CON SPOTIFY (prueba de conexion)");
    println!("==============================================================");
    let cache = match Cache::new(Some(datos.clone()), None, None, None) {
        Ok(c) => c,
        Err(e) => { println!("No pude abrir la carpeta {}: {e}", datos.display()); std::process::exit(1); }
    };
    let mut servidor = None;
    let mut puerto = 0;
    for p in 8723..8743u16 {
        if let Ok(s) = tiny_http::Server::http(("127.0.0.1", p)) { servidor = Some(s); puerto = p; break; }
    }
    let Some(servidor) = servidor else { println!("No encontre un puerto libre entre 8723 y 8742."); std::process::exit(1); };
    let app = Arc::new(App {
        exe_dir, datos, puerto, est: Mutex::new(Estado::default()), diag: Mutex::new(Vec::new()), sesion: Mutex::new(None),
        jugador: Mutex::new(None), cache: Mutex::new(cache), config: SessionConfig::default(), connect_tarea: Mutex::new(None),
        oauth_cancelar: AtomicBool::new(false), api_cache: Mutex::new(HashMap::new()), bloqueo: Mutex::new(None),
    });
    let url = format!("http://127.0.0.1:{puerto}/");
    println!("  La pagina esta en: {url}");
    println!("  (se abre sola en tu navegador; si no, copia esa direccion)");
    println!("  Para CERRAR: cierra esta ventana negra, o pulsa Ctrl+C aqui,");
    println!("  o usa el boton «Cerrar» de la pestana Pruebas.");
    println!("  Solo se escucha en esta computadora (127.0.0.1).");
    println!("--------------------------------------------------------------");

    let servidor = Arc::new(servidor);
    let h = tokio::runtime::Handle::current();
    for _ in 0..6 {
        let (s, a, h) = (servidor.clone(), app.clone(), h.clone());
        std::thread::spawn(move || { while let Ok(r) = s.recv() { atiende(&a, &h, r); } });
    }
    abre_navegador(&url);

    // LA CUENTA: la llave guardada, o esperar a que el celular la entregue (Connect); el boton de OAuth es la otra forma.
    let credenciales = app.cache.lock().unwrap().credentials();
    match credenciales {
        Some(c) => {
            println!("Hay una llave guardada: entrando solo...");
            if !entra_con(&app, c, "llave guardada").await { lanza_connect(&app); }
        }
        None => { println!("Sin cuenta todavia: esperando que la elijas desde el celular («{NOMBRE_CONNECT}») o entra con el navegador."); lanza_connect(&app); }
    }
    // Que la sesion siga viva: si se cae, se vuelve a entrar con la llave guardada.
    loop {
        tokio::time::sleep(Duration::from_secs(10)).await;
        let (conectado, invalida) = (app.est.lock().unwrap().conectado, app.sesion.lock().unwrap().as_ref().map(|s| s.is_invalid()).unwrap_or(false));
        if conectado && invalida {
            app.est.lock().unwrap().conectado = false;
            let c = app.cache.lock().unwrap().credentials();
            match c { Some(c) => { entra_con(&app, c, "reconexion").await; } None => lanza_connect(&app) }
        }
    }
}

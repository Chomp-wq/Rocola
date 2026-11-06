// EL PROGRAMA DE SPOTIFY DE LA ROCOLA (decision del dueno del 6-11-2026: Spotify integrado en la rocola, para las reuniones familiares).
//
//   rocola-spotify --cache /rocola/datos/spotify --nombre Rocola --control /run/rocola-spotify/control.sock
//
// - LA CUENTA: si /rocola/datos/spotify tiene la llave guardada, entra solo. Si no (la primera vez), se anuncia en la red como «Rocola»
//   y espera a que alguien lo elija UNA VEZ desde la app de Spotify del celular (Premium); la llave queda guardada y ya no hace falta.
// - EL SONIDO: lo que toca sale por stdout, en float de 32 bits, 44100 Hz, estereo (la rocola lo pasa por su cadena). Nada al disco.
// - LO MANEJA LA ROCOLA (Motor/Spotify.cs) por un enchufe Unix, una orden JSON por linea y una respuesta por linea:
//     {"op":"estado"}                          -> como va: la cuenta, lo que suena, si acabo, el ultimo error
//     {"op":"api","ruta":"/v1/search?q=..."}   -> la API web de Spotify con la llave de la sesion: {"ok":true,"estado":200,"cuerpo":"..."}
//     {"op":"baja","url":"https://i.scdn.co/...","archivo":"/run/..."}   -> una portada a un archivo
//     {"op":"toca","uri":"spotify:track:..."}  -> la toca desde el principio
//     {"op":"para"}                            -> calla
//     {"op":"letra","id":"<id de la cancion>"} -> la letra que da Spotify (sincronizada por lineas si la tiene): {"ok":true,"cuerpo":"..."};
//                                                 se pide al momento y no se guarda
use std::{
    path::PathBuf,
    sync::{Arc, Mutex},
    time::Duration,
};

use bytes::Bytes;
use futures_util::StreamExt;
use http::{Method, Request, header};
use librespot_core::{
    authentication::Credentials, cache::Cache, config::DeviceType, config::SessionConfig, session::Session,
    spotify_id::SpotifyId, spotify_uri::SpotifyUri,
};
use librespot_playback::{
    audio_backend,
    config::{AudioFormat, Bitrate, NormalisationMethod, PlayerConfig},
    mixer::NoOpVolume,
    player::{Player, PlayerEvent},
};
use log::{error, info, warn};
use serde_json::{Value, json};
use tokio::{
    io::{AsyncBufReadExt, AsyncWriteExt, BufReader},
    net::{UnixListener, UnixStream},
};

// Lo que se pide a Spotify con la llave de la sesion: leer la biblioteca de la cuenta y buscar. Nada que cambie la cuenta.
const ALCANCE: &str = "user-read-private,playlist-read-private,playlist-read-collaborative,user-library-read,user-follow-read,user-top-read,user-read-recently-played";
// Si Spotify no da la llave con todos (no todos se permiten a cualquier programa), con lo basico: buscar, tus listas y tus discos.
const ALCANCE_BASICO: &str = "user-read-private,playlist-read-private,user-library-read";

#[derive(Default)]
struct Estado {
    conectado: bool,
    esperando_cuenta: bool,
    usuario: String,
    sonando: String,
    acabo: String, // la ultima que acabo (o que no se pudo tocar): la rocola pasa a la siguiente
    no_disponible: String,
    error: String,
    seq: u64,
}

struct Rocola {
    estado: Mutex<Estado>,
    sesion: Mutex<Option<Session>>,
    jugador: Mutex<Option<Arc<Player>>>,
    cache: Cache,
    config: SessionConfig,
}

fn arg(nombre: &str, por_omision: &str) -> String {
    let a: Vec<String> = std::env::args().collect();
    a.iter().position(|x| x == nombre).and_then(|i| a.get(i + 1).cloned()).unwrap_or_else(|| por_omision.to_string())
}

#[tokio::main]
async fn main() {
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("info")).init();
    let cache_dir = PathBuf::from(arg("--cache", "/rocola/datos/spotify"));
    let nombre = arg("--nombre", "Rocola");
    let control = PathBuf::from(arg("--control", "/run/rocola-spotify/control.sock"));

    // Solo la llave de la cuenta (y el volumen, que no se usa); SIN cache de audio: no se guarda musica.
    let cache = match Cache::new(Some(cache_dir.clone()), None, None, None) {
        Ok(c) => c,
        Err(e) => {
            error!("no pude abrir {}: {e}", cache_dir.display());
            std::process::exit(1);
        }
    };
    let config = SessionConfig::default();
    let r = Arc::new(Rocola {
        estado: Mutex::new(Estado::default()),
        sesion: Mutex::new(None),
        jugador: Mutex::new(None),
        cache,
        config,
    });

    // El enchufe de control, desde ya (mientras espera la cuenta, la rocola pregunta como va).
    if let Some(d) = control.parent() {
        let _ = std::fs::create_dir_all(d);
    }
    let _ = std::fs::remove_file(&control);
    let oyente = match UnixListener::bind(&control) {
        Ok(o) => o,
        Err(e) => {
            error!("no pude abrir el enchufe {}: {e}", control.display());
            std::process::exit(1);
        }
    };
    {
        let r = r.clone();
        tokio::spawn(async move {
            loop {
                match oyente.accept().await {
                    Ok((s, _)) => {
                        let r = r.clone();
                        tokio::spawn(async move { atiende(r, s).await });
                    }
                    Err(e) => warn!("enchufe: {e}"),
                }
            }
        });
    }

    // LA CUENTA: la guardada, o la que llegue una vez desde el celular.
    let credenciales = match r.cache.credentials() {
        Some(c) => c,
        None => espera_cuenta(&r, &nombre).await,
    };
    let mut credenciales = credenciales;
    let mut espera = 5u64;
    loop {
        match conecta(&r, credenciales.clone()).await {
            Ok(()) => break,
            Err(e) => {
                let msg = format!("no pude entrar a Spotify: {e}");
                error!("{msg}");
                r.estado.lock().unwrap().error = msg;
                // Llave que ya no vale (se cambio la contrasena, se quito el acceso): se vuelve a pedir desde el celular.
                if e.to_string().contains("BadCredentials") || e.to_string().contains("Bad credentials") {
                    let _ = std::fs::remove_file(cache_dir.join("credentials.json"));
                    credenciales = espera_cuenta(&r, &nombre).await;
                    continue;
                }
                tokio::time::sleep(Duration::from_secs(espera)).await;
                espera = (espera * 2).min(300);
            }
        }
    }

    // Que la sesion siga viva: si se cae (la red), se vuelve a entrar con la llave guardada.
    loop {
        tokio::time::sleep(Duration::from_secs(10)).await;
        let invalida = r.sesion.lock().unwrap().as_ref().map(|s| s.is_invalid()).unwrap_or(true);
        if invalida {
            r.estado.lock().unwrap().conectado = false;
            if let Some(c) = r.cache.credentials() {
                if let Err(e) = conecta(&r, c).await {
                    r.estado.lock().unwrap().error = format!("se cayo la conexion con Spotify: {e}");
                }
            }
        }
    }
}

// La primera vez: anunciarse en la red y esperar a que el celular entregue la cuenta (Spotify Connect, una sola vez).
async fn espera_cuenta(r: &Arc<Rocola>, nombre: &str) -> Credentials {
    r.estado.lock().unwrap().esperando_cuenta = true;
    loop {
        match librespot_discovery::Discovery::builder(r.config.device_id.clone(), r.config.client_id.clone())
            .name(nombre.to_string())
            .device_type(DeviceType::Speaker)
            .launch()
        {
            Ok(mut d) => {
                info!("esperando la cuenta: elige «{nombre}» en la app de Spotify");
                if let Some(c) = d.next().await {
                    r.estado.lock().unwrap().esperando_cuenta = false;
                    return c;
                }
            }
            Err(e) => {
                let msg = format!("no me pude anunciar en la red: {e}");
                warn!("{msg}");
                r.estado.lock().unwrap().error = msg;
            }
        }
        tokio::time::sleep(Duration::from_secs(10)).await;
    }
}

async fn conecta(r: &Arc<Rocola>, c: Credentials) -> Result<(), librespot_core::Error> {
    let sesion = Session::new(r.config.clone(), Some(r.cache.clone()));
    sesion.connect(c, true).await?; // true: guarda la llave para la proxima vez
    let usuario = sesion.username();
    info!("dentro de Spotify ({usuario})");
    {
        let mut j = r.jugador.lock().unwrap();
        match j.as_ref() {
            Some(p) => p.set_session(sesion.clone()),
            None => {
                let config = PlayerConfig {
                    bitrate: Bitrate::Bitrate320,
                    gapless: false,
                    normalisation: true, // la ganancia de cada cancion que da Spotify (cerca de -14 LUFS); el limitador es el de la rocola
                    normalisation_method: NormalisationMethod::Basic,
                    ..PlayerConfig::default()
                };
                let p = Player::new(config, sesion.clone(), Box::new(NoOpVolume), move || {
                    (audio_backend::find(Some("pipe".to_string())).expect("sin salida por tuberia"))(None, AudioFormat::F32)
                });
                let mut eventos = p.get_player_event_channel();
                let r2 = r.clone();
                tokio::spawn(async move {
                    while let Some(e) = eventos.recv().await {
                        let mut s = r2.estado.lock().unwrap();
                        match e {
                            PlayerEvent::Playing { track_id, .. } => s.sonando = track_id.to_uri().unwrap_or_default(),
                            PlayerEvent::EndOfTrack { track_id, .. } => {
                                s.acabo = track_id.to_uri().unwrap_or_default();
                                s.seq += 1;
                            }
                            PlayerEvent::Unavailable { track_id, .. } => {
                                let u = track_id.to_uri().unwrap_or_default();
                                s.no_disponible = u.clone();
                                s.acabo = u;
                                s.seq += 1;
                            }
                            PlayerEvent::Stopped { .. } => s.sonando.clear(),
                            _ => {}
                        }
                    }
                });
                *j = Some(p);
            }
        }
    }
    *r.sesion.lock().unwrap() = Some(sesion);
    let mut s = r.estado.lock().unwrap();
    s.conectado = true;
    s.usuario = usuario;
    s.error.clear();
    Ok(())
}

async fn atiende(r: Arc<Rocola>, s: UnixStream) {
    let (lee, mut escribe) = s.into_split();
    let mut lineas = BufReader::new(lee).lines();
    while let Ok(Some(l)) = lineas.next_line().await {
        let resp = match serde_json::from_str::<Value>(&l) {
            Ok(v) => orden(&r, &v).await,
            Err(e) => json!({"ok": false, "error": format!("orden mal escrita: {e}")}),
        };
        let mut t = resp.to_string();
        t.push('\n');
        if escribe.write_all(t.as_bytes()).await.is_err() {
            break;
        }
    }
}

fn sesion(r: &Rocola) -> Option<Session> {
    r.sesion.lock().unwrap().clone().filter(|s| !s.is_invalid())
}

async fn orden(r: &Arc<Rocola>, v: &Value) -> Value {
    let op = v["op"].as_str().unwrap_or("");
    match op {
        "estado" => {
            let s = r.estado.lock().unwrap();
            json!({"ok": true, "conectado": s.conectado, "esperando_cuenta": s.esperando_cuenta, "usuario": s.usuario,
                   "sonando": s.sonando, "acabo": s.acabo, "no_disponible": s.no_disponible, "seq": s.seq, "error": s.error})
        }
        "toca" => {
            let uri = v["uri"].as_str().unwrap_or("");
            let id = match SpotifyUri::from_uri(uri) {
                Ok(id) => id,
                Err(e) => return json!({"ok": false, "error": format!("uri: {e}")}),
            };
            let j = r.jugador.lock().unwrap().clone();
            match j {
                Some(p) => {
                    // el numero de lo que acabo hasta ahora: la rocola espera uno mayor (asi no confunde el final de la anterior,
                    // aunque sea la misma cancion)
                    let seq = {
                        let mut s = r.estado.lock().unwrap();
                        s.acabo.clear();
                        s.no_disponible.clear();
                        s.seq
                    };
                    p.load(id, true, 0);
                    json!({"ok": true, "seq": seq})
                }
                None => json!({"ok": false, "error": "aun no hay cuenta"}),
            }
        }
        "para" => {
            if let Some(p) = r.jugador.lock().unwrap().clone() {
                p.stop();
            }
            json!({"ok": true})
        }
        "api" => {
            let ruta = v["ruta"].as_str().unwrap_or("");
            if !ruta.starts_with("/v1/") {
                return json!({"ok": false, "error": "ruta"});
            }
            let Some(se) = sesion(r) else { return json!({"ok": false, "error": "sin conexion con Spotify"}) };
            let llave = match se.token_provider().get_token(ALCANCE).await {
                Ok(t) => t,
                Err(e) => match se.token_provider().get_token(ALCANCE_BASICO).await {
                    Ok(t) => { warn!("la llave con todos los permisos no se dio ({e}): con los basicos"); t }
                    Err(e2) => return json!({"ok": false, "error": format!("llave: {e}; basica: {e2}")}),
                },
            };
            let req = Request::builder()
                .method(Method::GET)
                .uri(format!("https://api.spotify.com{ruta}"))
                .header(header::AUTHORIZATION, format!("Bearer {}", llave.access_token))
                .header(header::ACCEPT, "application/json")
                .body(Bytes::new());
            let req = match req {
                Ok(q) => q,
                Err(e) => return json!({"ok": false, "error": format!("{e}")}),
            };
            match se.http_client().request(req).await {
                Ok(resp) => {
                    let estado = resp.status().as_u16();
                    use http_body_util_compat::collect;
                    match collect(resp).await {
                        Ok(b) => json!({"ok": (200..300).contains(&estado), "estado": estado, "cuerpo": String::from_utf8_lossy(&b)}),
                        Err(e) => json!({"ok": false, "estado": estado, "error": e}),
                    }
                }
                Err(e) => json!({"ok": false, "error": format!("{e}")}),
            }
        }
        "letra" => {
            let id = match SpotifyId::from_base62(v["id"].as_str().unwrap_or("")) {
                Ok(id) => id,
                Err(e) => return json!({"ok": false, "error": format!("id: {e}")}),
            };
            let Some(se) = sesion(r) else { return json!({"ok": false, "error": "sin conexion con Spotify"}) };
            match se.spclient().get_lyrics(&id).await {
                Ok(b) => json!({"ok": true, "cuerpo": String::from_utf8_lossy(&b)}),
                Err(e) => json!({"ok": false, "error": format!("{e}")}),   // (sin letra: Spotify responde 404)
            }
        }
        "baja" => {
            let url = v["url"].as_str().unwrap_or("");
            let archivo = v["archivo"].as_str().unwrap_or("");
            if !url.starts_with("https://") || archivo.is_empty() {
                return json!({"ok": false, "error": "url o archivo"});
            }
            let Some(se) = sesion(r) else { return json!({"ok": false, "error": "sin conexion con Spotify"}) };
            let req = match Request::builder().method(Method::GET).uri(url).body(Bytes::new()) {
                Ok(q) => q,
                Err(e) => return json!({"ok": false, "error": format!("{e}")}),
            };
            match se.http_client().request_body(req).await {
                Ok(b) => {
                    let tmp = format!("{archivo}.t");
                    match tokio::fs::write(&tmp, &b).await.and(tokio::fs::rename(&tmp, archivo).await) {
                        Ok(()) => json!({"ok": true}),
                        Err(e) => json!({"ok": false, "error": format!("{e}")}),
                    }
                }
                Err(e) => json!({"ok": false, "error": format!("{e}")}),
            }
        }
        _ => json!({"ok": false, "error": format!("orden desconocida: {op}")}),
    }
}

// El cuerpo de una respuesta entera (tambien la de un error, que dice por que).
mod http_body_util_compat {
    pub async fn collect(resp: http::Response<hyper::body::Incoming>) -> Result<bytes::Bytes, String> {
        use http_body_util::BodyExt;
        resp.into_body().collect().await.map(|c| c.to_bytes()).map_err(|e| e.to_string())
    }
}

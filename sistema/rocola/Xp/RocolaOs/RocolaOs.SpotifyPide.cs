// SPOTIFY EN LA ROCOLA (fase 11, grupo spotify-xp): LA FUNCION «spotify» (el andamio de la fase 11), LO QUE SE PIDE (I6) Y LOS GANCHOS.
// Documento: FASE11_spotify-xp.md.
//
// LA FUNCION. «spotify», ENCENDIDA de fabrica (decision del dueno del 15-11-2026, «Lo de Spotify encender dejalo encendido por defecto»;
// sustituye a R1 «apagado de fabrica»): con ROCOLA_OS=1 va salvo spotify=0 en /rocola/datos/xp/rocola_os.txt (la orden «spotify apagar»
// de la consola de mantenimiento; vale desde el siguiente arranque, como toda funcion del andamio; lo guardado manda). Apagada (o sin
// ROCOLA_OS) no corre nada de Spotify y rocola-xp sale igual que hoy en todo (los ganchos devuelven lo de XP). Encendida SIN NINGUNA LLAVE
// (ni la de Soloist ni la de la API) se ve y suena como XP: el vigilante espera la llave, no hay letra ni resultados de Spotify y nada
// cobra (la escena «fabrica» de Pruebas/RocolaOs/spotify-xp/prueba.sh). Su otra clave: spotify_nombre= (el nombre de la bocina de Spotify
// Connect; de fabrica «Rocola»).
//
// I6, EL PEDIR (lo usa el catalogo, otro grupo, desde el hilo de la pantalla):
//   RocolaOsSpotifyPista              una cancion de Spotify: Uri («spotify:track:ID»), Titulo, Artista, Disco, DuracionMs, PortadaUrl, DiscoUri
//   bool RocolaOsSpotifyListo         Soloist con sesion (el WebSocket conectado y la cuenta dentro). Si no, el catalogo lo dice con lo de XP
//                                     y no pide nada.
//   bool RocolaOsSpotifyPide(p)       EXACTAMENTE lo que hace XP al pedir una cancion suya con PONER desde un disco (AddSong, Form1.cs):
//                                     AddItem (sin creditos, la capsula se sacude y no entra; si ya va seguida, no entra), el cobro, el turno
//                                     en la cola, el mismo aviso y la misma fugaz; y si no entro, el mismo aviso de XP. Devuelve si entro.
//   bool RocolaOsSpotifyPide(p, true) lo mismo, como desde el buscador (ChooseSearchResult: los avisos del buscador)
//   string RocolaOsSpotifyPortada(p)  la portada en /run/rocola-xp/spotify/portadas/<id>.jpg si ya esta; si no, la pide (la baja un hilo, nunca
//                                     al disco) y devuelve null: se vuelve a preguntar en el siguiente cuadro o latido
// Ejemplo (en el catalogo, al elegir una cancion de Spotify con PONER):
//     RocolaOsSpotifyPista p = new RocolaOsSpotifyPista { Uri = "spotify:track:" + id, Titulo = nombre, Artista = artista, Disco = disco,
//                                                         DuracionMs = ms, PortadaUrl = url, DiscoUri = "spotify:album:" + idDisco };
//     if (!RocolaOsSpotifyListo) { ShowToast("Spotify no esta listo", Color.FromArgb(200, 130, 35)); return; }   // (el texto, del catalogo)
//     RocolaOsSpotifyPide(p);   // cobra, turno y aviso de XP; sin creditos, lo de XP
//
// LA CANCION EN LA COLA DE XP: un Track de XP con Path = la uri («spotify:track:ID»), Title, Artist y Album del catalogo y Genre vacio, en
// un PlayItem con Disc = null. Nunca entra en la biblioteca de XP (albums): asi no la ven el repaso, el indice, la busqueda de XP, la cache
// de la biblioteca ni la atraccion, y tras un apagon RestoreState de XP no la encuentra y DEVUELVE su credito (R5; Form1.Datos.cs 2826-2840).
//
// LOS GANCHOS (RocolaOs/GANCHOS.tsv; apagados devuelven lo de XP):
//   g13  Codigo/Form1.Sonido.cs  (OpenAudio)    una ruta «spotify:» se abre con la fuente de Spotify (RocolaOsSpotifyAbre)
//   g20  Codigo/Form1.Datos.cs   (RecordPlay)   lo de Spotify no cuenta para lo mas sonado ni las estadisticas (R6; RocolaOsSpotifyNoCuenta)
//   g23  Codigo/Form1.Sonido.cs  (ArmaCadena)   de lo de Spotify no se aprende nada (ni su sonoridad ni su EQ: R7, suena como una sin medir)
//   g24  Codigo/VolumenParejo.cs (AprendeParejo) ni el volumen parejo del billar
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    // UNA CANCION DE SPOTIFY QUE SE PIDE (I6)
    internal sealed class RocolaOsSpotifyPista
    {
        public string Uri;          // «spotify:track:ID»
        public string Titulo;
        public string Artista;
        public string Disco;
        public int DuracionMs;
        public string PortadaUrl;   // https://i.scdn.co/... (la baja el nucleo a /run)
        public string DiscoUri;     // «spotify:album:ID»
    }

    internal static partial class RocolaOsSpotify
    {
        internal static volatile bool CerrandoLaRocola;
        // lo que se pidio, por su uri: la fuente (que abre otro hilo) saca de aqui su titulo y su duracion
        internal static readonly ConcurrentDictionary<string, RocolaOsSpotifyPista> Pistas = new ConcurrentDictionary<string, RocolaOsSpotifyPista>(StringComparer.Ordinal);
        // una uri de cancion de Spotify valida: «spotify:track:» y su id (base62)
        internal static bool EsUriDeCancion(string u)
        {
            if (u == null || !u.StartsWith("spotify:track:", StringComparison.Ordinal)) return false;
            int n = u.Length - 14;
            if (n < 1 || n > 64) return false;
            for (int i = 14; i < u.Length; i++) { char c = u[i]; if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))) return false; }
            return true;
        }
        internal static string IdDe(string uri) { return IdBase62(uri, "spotify:track:"); }
    }

    public partial class Form1
    {
        // ================================================================== LA FUNCION «spotify» (se registra sola)
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraSpotify()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsSpotify.Nombre,
                Que = "Spotify en la rocola con Spotify Soloist (pedir, pagar y oir en la rocola)",
                DeFabrica = true,    // encendido de fabrica (decision del 15-11-2026; antes R1: apagado)
                Claves = new RocolaOsClave[]
                {
                    RocolaOsClave.Texto(RocolaOsSpotify.ClaveNombre, "Rocola", RocolaOsNombreDeBocinaVale, false, "el nombre de la bocina de Spotify Connect"),
                },
                Arranca = f => f.RocolaOsArrancaSpotify(),
                Latido = f => f.RocolaOsLatidoSpotify(),
                Vivo = (f, sb) => RocolaOsSpotify.Vivo(sb),
                AlCerrar = (f, razon) => { RocolaOsSpotify.CerrandoLaRocola = true; },
                ParaAlCerrar = razon => RocolaOsSpotify.Para(razon.ToString()),
            });
        }
        private static bool RocolaOsNombreDeBocinaVale(string v)
        {
            if (string.IsNullOrWhiteSpace(v) || v.Length > 64) return false;
            foreach (char c in v) if (c < 0x20 || c == '"' || c == '\\') return false;
            return true;
        }

        private void RocolaOsArrancaSpotify()
        {
            RocolaOsSpotify.ArrancaHilos();
            RocolaOsSpotify.LimpiaPortadas();
        }
        // CADA SEGUNDO (hilo de la pantalla; rapido): lo que el hilo de Spotify necesita saber de XP
        private void RocolaOsLatidoSpotify()
        {
            int r = 0;
            try { r = RetrasoAudible(); } catch { }
            RocolaOsSpotify.LatenciaUs = Math.Max(0, r) * 1000;
            int n = 0;
            for (int i = 0; i < queue.Count; i++) { PlayItem it = queue[i]; if (it != null && !it.IsVideo && it.Song != null && RocolaOsSpotify.EsUriDeCancion(it.Song.Path)) n++; }
            Volatile.Write(ref RocolaOsSpotify.EnCola, n);
            RocolaOsSpotifyPublicaSiguiente();
            RocolaOsSpotifyAdelantaDesdeXp();
            RocolaOsSpotifyCatalogoLatido();   // (rapido-spotify: al abrirse el buscador, la llave y las conexiones de la API ya calientes)
        }

        // (rapido-spotify) LA SIGUIENTE DE LA COLA DE XP (la que PlayNext tomara: queue[0]), para el play adelantado del hilo de Spotify: la de
        // Spotify, o nada. En el latido y al pedir (la pantalla; solo lee la cola de XP)
        private void RocolaOsSpotifyPublicaSiguiente()
        {
            PlayItem it = queue.Count > 0 ? queue[0] : null;
            string u = it != null && !it.IsVideo && it.Song != null ? it.Song.Path : null;
            if (u == null || !RocolaOsSpotify.EsUriDeCancion(u)) { RocolaOsSpotify.PonSiguiente(null); return; }
            RocolaOsSpotifyPista pi;
            RocolaOsSpotify.Pistas.TryGetValue(u, out pi);
            RocolaOsSpotify.PonSiguiente(new RocolaOsSpotify.Siguiente { Uri = u, Titulo = it.Song.Title ?? "", DuracionMs = pi != null ? pi.DuracionMs : 0 });
        }

        // (rapido-spotify) DE UNA DE XP A UNA DE SPOTIFY: a la de XP que suena le quedan 2,5 s o menos por leer y la siguiente de la cola es de
        // Spotify: su play adelantado (Soloist empieza mientras acaba la de XP; nadie lo oye hasta que XP abre la suya). En el latido (la pantalla,
        // lo mismo que lee el reloj de la cancion de XP: reader).
        private void RocolaOsSpotifyAdelantaDesdeXp()
        {
            if (nowPlaying == null || nowPlaying.IsVideo || videoOn || nowPlaying.Song == null || queue.Count == 0) return;
            if (RocolaOsSpotify.EsUriDeCancion(nowPlaying.Song.Path)) return;   // (de Spotify: lo adelanta el hilo de Spotify al rematarla)
            PlayItem sig = queue[0];
            if (sig == null || sig.IsVideo || sig.Song == null || !RocolaOsSpotify.EsUriDeCancion(sig.Song.Path)) return;
            IAudioSource r = reader;
            if (r == null) return;
            double quedan;
            try { quedan = (r.TotalTime - r.CurrentTime).TotalSeconds; } catch { return; }
            if (quedan < 0 || quedan > 2.5) return;
            RocolaOsSpotify.PideAdelantoDesdeXp();
        }

        // ================================================================== I6
        // SOLOIST CON SESION (si no, el catalogo no pide)
        // (27-11-2026) tambien que Soloist responda (RocolaOsSpotify.PuedePedir): el catalogo, con esto, no cobra con Soloist sordo y dice el aviso de XP
        internal bool RocolaOsSpotifyListo { get { string m; return RocolaOsSpotify.PuedePedir(out m); } }
        // PIDE UNA CANCION DE SPOTIFY con PONER, como una de XP desde un disco. En el hilo de la pantalla. Devuelve si entro en la cola.
        internal bool RocolaOsSpotifyPide(RocolaOsSpotifyPista p) { return RocolaOsSpotifyPide(p, false); }
        internal bool RocolaOsSpotifyPide(RocolaOsSpotifyPista p, bool desdeBuscador)
        {
            if (p == null || !RocolaOsSpotify.Encendido || !RocolaOsSpotify.EsUriDeCancion(p.Uri)) return false;
            string noPuede;
            if (!RocolaOsSpotify.PuedePedir(out noPuede))
            {   // (nada se cobra ni se mete en la cola: el aviso de XP, «Espera un momento y vuelve a intentarlo»)
                RocolaOsSpotify.Apunta("no se pide " + p.Uri + ": " + noPuede + " (no se cobra)");
                if (desdeBuscador) ShowSearchToast("Espera un momento y vuelve a intentarlo", false); else ShowToast("Espera un momento y vuelve a intentarlo", Color.FromArgb(200, 130, 35));
                return false;
            }
            RocolaOsSpotify.Pistas[p.Uri] = p;
            Track t = new Track();
            t.Path = p.Uri; t.Title = p.Titulo ?? ""; t.Artist = p.Artista ?? ""; t.Album = p.Disco ?? ""; t.Genre = "";
            // SU DISCO, solo para la portada (R8: lo que suena y la portada que vuela a la cola, como las de XP): un Album de XP que NO entra en
            // la biblioteca (albums), con la portada ya bajada a /run (el catalogo la pidio al ensenarla: RocolaOsSpotifyPortada). Sin portada
            // bajada, sin disco (como un video sin carpeta): XP no pinta portada.
            string portada = RocolaOsSpotify.PidePortada(p);
            Album disco = null;
            if (portada != null) disco = RocolaOsSpotifyDiscoDePortada(p, portada);
            else RocolaOsSpotifyPortadaPendiente(p);   // (22-11-2026) aun sin bajar: cuando llegue se le pone (RocolaOsSpotifyPortadaLlego)
            PlayItem it = new PlayItem(); it.IsVideo = false; it.Song = t; it.Disc = disco;
            AddResult r = AddItem(it);
            // los avisos, los de XP (AddSong de Form1.cs y ChooseSearchResult de Form1.Biblioteca.cs)
            if (!desdeBuscador)
            {
                if (r == AddResult.Added) ShowSongAddedToast(false);
                else if (r == AddResult.NoCredit) { if (!InvitaSinCredito(it)) ShowToast("Inserta una moneda", Color.FromArgb(200, 130, 35)); }
                else if (r == AddResult.Danada) ShowToast(AvisoDanada, Color.FromArgb(200, 80, 70));
                else ShowToast("Esa ya va seguida", Color.FromArgb(200, 130, 35));
            }
            else
            {
                if (r == AddResult.Added) ShowSongAddedToast(true);
                else if (r == AddResult.NoCredit) { if (!InvitaSinCredito(it)) ShowSearchToast("Inserta una moneda", false); }
                else if (r == AddResult.Danada) ShowSearchToast(AvisoDanada, false);
                else ShowSearchToast("Esa cancion ya va seguida", false);
            }
            if (r == AddResult.Added)
            {
                RocolaOsSpotifyPublicaSiguiente();   // (rapido-spotify: por si es la siguiente, sin esperar al latido)
                Interlocked.Increment(ref RocolaOsSpotify.Cobradas);
                int puesto = queue.IndexOf(it) + 1;   // 0: ya suena
                RocolaOsSpotify.Apunta("COBRADA " + p.Uri + " «" + (p.Titulo ?? "") + "» de «" + (p.Artista ?? "") + "»: 1 credito, "
                    + (puesto > 0 ? "turno " + puesto.ToString(CultureInfo.InvariantCulture) + " en la cola" : "suena ya") + "; quedan " + credits.ToString(CultureInfo.InvariantCulture) + " creditos"
                    + (portada != null ? "; con portada" : ""));
            }
            else if (r == AddResult.NoCredit) { Interlocked.Increment(ref RocolaOsSpotify.SinCredito); RocolaOsSpotify.Apunta("sin creditos: no entra " + p.Uri); }
            else RocolaOsSpotify.Apunta("no entra " + p.Uri + ": " + r);
            return r == AddResult.Added;
        }
        // LA PORTADA de una cancion pedida o por pedir (I6): la ruta en /run si ya esta; si no, se pide y null
        internal string RocolaOsSpotifyPortada(RocolaOsSpotifyPista p) { return RocolaOsSpotify.PidePortada(p); }

        // UN CREDITO DE SPOTIFY VUELVE (R4, R9): por el camino de XP, con su mismo aviso (Output_PlaybackStopped de Form1.Sonido.cs). En el hilo
        // de la pantalla (un encargo del hilo de Spotify).
        internal void RocolaOsSpotifyDevuelve()
        {
            RefundCredit();
            ShowToast("No se pudo reproducir: credito devuelto", Color.FromArgb(200, 80, 70));
        }

        // ================================================================== LOS GANCHOS
        // g13 (Codigo/Form1.Sonido.cs, OpenAudio): una ruta «spotify:track:» se abre con la fuente de Spotify; si no, null (lo de XP). Corre en
        // el hilo que abre (AperturaEnFondo): solo crea el objeto, sin pedir nada a Soloist.
        private IAudioSource RocolaOsSpotifyAbre(string path)
        {
            if (!RocolaOs.Activo || path == null) return null;
            { IAudioSource calidad = RocolaOsCalidadAbre(path); if (calidad != null) return calidad; }   // (la sustitucion invisible: una tuya desde Spotify; RocolaOs.Calidad.cs)
            if (!path.StartsWith("spotify:", StringComparison.Ordinal) || !RocolaOsSpotify.Encendido) return null;
            if (!RocolaOsSpotify.EsUriDeCancion(path)) return null;
            RocolaOsSpotifyPista p;
            RocolaOsSpotify.Pistas.TryGetValue(path, out p);
            RocolaOsFuenteSpotify f = new RocolaOsFuenteSpotify(path, p != null ? p.Titulo : "", p != null ? p.DuracionMs : 0);
            f.Nivela = RocolaOsSpotifyNivelaPara(path, f.WaveFormat.SampleRate);   // (spotify_nivela, 22-11-2026: RocolaOs.SpotifyNivela.cs)
            f.Corte = RocolaOsSpotifyCortePara(path, f.WaveFormat.SampleRate, p != null ? p.DuracionMs : 0);   // (spotify_eq y spotify_brillo, 22-11-2026: RocolaOs.SpotifyEq.cs)
            return f;
        }
        // g20 (Codigo/Form1.Datos.cs, RecordPlay): lo de Spotify no cuenta (R6). false = lo de XP (cuenta).
        private bool RocolaOsSpotifyNoCuenta(PlayItem it)
        {
            return RocolaOs.Activo && it != null && !it.IsVideo && it.Song != null && RocolaOs.Encendida(RocolaOsSpotify.Nombre) && RocolaOsSpotify.EsUriDeCancion(it.Song.Path);
        }
        // g23 (Codigo/Form1.Sonido.cs, ArmaCadena) y g24 (Codigo/VolumenParejo.cs, AprendeParejo): de lo de Spotify no se aprende nada (R7). true =
        // lo de XP (se aprende).
        private bool RocolaOsSpotifyAprende(string path)
        {
            return !RocolaOs.Activo || path == null || !path.StartsWith("spotify:", StringComparison.Ordinal);
        }
    }

    // ================================================================== LAS PORTADAS (I6: a /run, nunca al disco)
    internal static partial class RocolaOsSpotify
    {
        internal static string CarpetaPortadas { get { return RunSpotify("portadas"); } }
        private static readonly ConcurrentQueue<RocolaOsSpotifyPista> portadasPorBajar = new ConcurrentQueue<RocolaOsSpotifyPista>();
        private static readonly ConcurrentDictionary<string, byte> portadasPedidas = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private static Thread hiloPortadas;
        private static readonly AutoResetEvent hayPortadas = new AutoResetEvent(false);
        // la portada se nombra por su DISCO (portadas/<id del disco>.jpg, el mismo nombre que le da el catalogo: asi no se baja dos veces) y, si
        // la pista no trae disco, por la cancion (portadas/<id de la cancion>.jpg)
        internal static string RutaPortada(RocolaOsSpotifyPista p)
        {
            if (p == null) return null;
            string id = IdBase62(p.DiscoUri, "spotify:album:");
            if (id.Length == 0) id = IdDe(p.Uri);
            return id.Length == 0 ? null : Path.Combine(CarpetaPortadas, id + ".jpg");
        }
        // el id base62 de una uri «PREFIJO:ID» ("" si no lo es: nada de rutas raras)
        internal static string IdBase62(string u, string prefijo)
        {
            if (u == null || !u.StartsWith(prefijo, StringComparison.Ordinal)) return "";
            int n = u.Length - prefijo.Length;
            if (n < 1 || n > 64) return "";
            for (int i = prefijo.Length; i < u.Length; i++) { char c = u[i]; if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))) return ""; }
            return u.Substring(prefijo.Length);
        }
        // la ruta si ya esta; si no, la pide (una vez) y null
        internal static string PidePortada(RocolaOsSpotifyPista p)
        {
            if (p == null || !Encendido || !EsUriDeCancion(p.Uri)) return null;
            string ruta = RutaPortada(p);
            if (ruta != null && File.Exists(ruta)) return ruta;
            if (string.IsNullOrEmpty(p.PortadaUrl) || !UrlDePortadaVale(p.PortadaUrl)) return null;
            if (ruta == null || !portadasPedidas.TryAdd(ruta, 0)) return null;
            portadasPorBajar.Enqueue(p);
            if (hiloPortadas == null) { lock (portadasPedidas) { if (hiloPortadas == null) hiloPortadas = RocolaOs.Hilo("spotify-portadas", BajaPortadas); } }
            hayPortadas.Set();
            return null;
        }
        // solo las de Spotify (https://i.scdn.co/...) y, en las pruebas, las de esta maquina
        internal static bool UrlDePortadaVale(string u)
        {
            Uri x;
            if (!System.Uri.TryCreate(u, UriKind.Absolute, out x)) return false;
            if (x.Scheme == "https" && (x.Host == "i.scdn.co" || x.Host.EndsWith(".scdn.co", StringComparison.Ordinal) || x.Host.EndsWith(".spotifycdn.com", StringComparison.Ordinal))) return true;
            return x.Scheme == "http" && x.Host == "127.0.0.1";
        }
        private static void BajaPortadas()
        {
            using (HttpClient h = new HttpClient() { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 })
                while (!parar)
                {
                    RocolaOsSpotifyPista p;
                    if (!portadasPorBajar.TryDequeue(out p)) { hayPortadas.WaitOne(2000); continue; }
                    string ruta = RutaPortada(p);
                    try
                    {
                        Directory.CreateDirectory(CarpetaPortadas);
                        byte[] b = h.GetByteArrayAsync(p.PortadaUrl).GetAwaiter().GetResult();
                        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) { Apunta("la portada de " + p.Uri + " no es un JPEG: no se usa"); continue; }
                        File.WriteAllBytes(ruta + ".n", b);
                        File.Move(ruta + ".n", ruta, true);
                        // (22-11-2026) a lo que ya suena o espera en la cola sin portada (se pidio antes de que bajara), se le pone ahora
                        string u = p.Uri, r = ruta; RocolaOsSpotifyPista pp = p;
                        RocolaOs.Encarga("spotify:portada", delegate { Form1 f = RocolaOs.LaRocola; if (f != null) f.RocolaOsSpotifyPortadaLlego(pp, r); });
                    }
                    catch (Exception ex) { portadasPedidas.TryRemove(ruta, out _); Apunta("no se pudo bajar la portada de " + p.Uri + ": " + ex.GetType().Name); }
                }
        }
        // al arrancar: las portadas de mas de un dia se borran (estan en la memoria: que no se acumulen si la rocola pasa dias sin reiniciar)
        internal static void LimpiaPortadas()
        {
            try { foreach (string f in Directory.GetFiles(CarpetaPortadas)) if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-1)) File.Delete(f); } catch { }
        }
    }

    // LA PORTADA QUE LLEGA DESPUES (22-11-2026, el dueno: «mi padre puso una cancion de michale jackson de un disco de Spotify [...] se puso, pero
    // no aparecia su caratula en el reproductor»). Si al pedirla la portada aun no estaba en /run (un disco de la pared del catalogo, con la del
    // Cover Art Archive), entraba sin disco y se quedaba sin portada para siempre. Ahora, al bajar, se le pone a todo lo que suena o espera de esa
    // uri y se repinta como cuando XP cambia de cancion. Sin la direccion de la portada en la pista: la de su disco ya leido, o UNA peticion del
    // disco a la API (solo si hay llave y no esta frenando; nada de fondo).
    public partial class Form1
    {
        private static Album RocolaOsSpotifyDiscoDePortada(RocolaOsSpotifyPista p, string portada)
        {
            Album disco = new Album(); disco.Folder = Path.GetDirectoryName(portada); disco.Name = p.Disco ?? ""; disco.Artist = p.Artista ?? ""; disco.CoverPath = portada; disco.Letter = ' ';
            return disco;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> rocolaOsSpPortadaDisco = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private void RocolaOsSpotifyPortadaPendiente(RocolaOsSpotifyPista p)
        {
            if (p == null || !string.IsNullOrEmpty(p.PortadaUrl)) return;   // (ya se esta bajando: PidePortada la encolo)
            string id = RocolaOsSpotify.IdBase62(p.DiscoUri, "spotify:album:");
            if (id.Length == 0) { RocolaOsSpotify.Apunta("sin portada para " + p.Uri + ": la pista no trae ni su portada ni su disco"); return; }
            RocolaOsSpotifyCatalogoDisco ya = RocolaOsSpotifyCatalogoApi.DiscoYa(id);
            if (ya != null && !string.IsNullOrEmpty(ya.PortadaUrl)) { p.PortadaUrl = ya.PortadaUrl; RocolaOsSpotify.PidePortada(p); return; }
            if (!RocolaOsSpotifyCatalogoApi.HayLlave || RocolaOsSpotifyCatalogoApi.Frena || !rocolaOsSpPortadaDisco.TryAdd(id, 0)) return;   // (una peticion por disco en la sesion)
            RocolaOsSpotify.Apunta("la portada de " + p.Uri + ": se pide su disco " + id + " (una peticion)");
            RocolaOsSpotifyCatalogoApi.AbreDisco(id, delegate (object r, string error)
            {
                RocolaOsSpotifyCatalogoDisco d = r as RocolaOsSpotifyCatalogoDisco;
                if (d == null || string.IsNullOrEmpty(d.PortadaUrl)) { RocolaOsSpotify.Apunta("sin portada para " + p.Uri + ": su disco no la trae (" + (error ?? "sin portada") + ")"); return; }
                p.PortadaUrl = d.PortadaUrl;
                RocolaOsSpotify.PidePortada(p);
            });
        }
        // (encargo, en la pantalla) la portada de esa uri ya esta en /run: a lo que suena y a la cola que no la tenian
        internal void RocolaOsSpotifyPortadaLlego(RocolaOsSpotifyPista p, string ruta)
        {
            if (p == null || ruta == null || !File.Exists(ruta)) return;
            bool suena = false; int n = 0;
            List<PlayItem> todos = new List<PlayItem>(queue.Count + 1);
            if (nowPlaying != null) todos.Add(nowPlaying);
            todos.AddRange(queue);
            foreach (PlayItem it in todos)
            {
                if (it == null || it.IsVideo || it.Song == null || it.Disc != null || !string.Equals(it.Song.Path, p.Uri, StringComparison.Ordinal)) continue;
                it.Disc = RocolaOsSpotifyDiscoDePortada(p, ruta);
                n++;
                if (object.ReferenceEquals(it, nowPlaying)) suena = true;
            }
            if (n == 0) return;
            RocolaOsSpotify.Apunta("la portada de " + p.Uri + " llego: puesta a " + n.ToString(CultureInfo.InvariantCulture) + (suena ? " (la que suena)" : " (en la cola)"));
            RefrescaCola();
            if (suena) UpdateNowPlayingUI();
        }
    }
}

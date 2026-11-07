// SPOTIFY DENTRO DE LA ROCOLA (decision del dueno del 6-11-2026: «quiero integrar toda la biblioteca de Spotify [...] se podra apagar con un
// interruptor, solo lo usare para reuniones familiares»; despues: «queria una integracion nativa en la rocola»). Interruptor «spotify=»,
// APAGADO de fabrica (Ctrl+F3 -> Spotify). Encendido:
//  - la rocola abre su programa de Spotify (/usr/bin/rocola-spotify, sistema/rocola-spotify: las piezas de librespot, no oficial; Premium);
//  - la cuenta se pone UNA vez: en el celular, en la misma red, Spotify -> dispositivos -> «Rocola»; queda guardada (solo la llave, en
//    /rocola/datos/spotify) y la rocola entra sola desde entonces;
//  - en la pared aparece la letra SPOTIFY con tus favoritas, tus listas y tus discos guardados (con sus portadas), y el buscador busca
//    tambien en Spotify; se eligen con los botones de siempre;
//  - lo de Spotify se paga con CREDITOS como todo (el dueno, 6-11-2026: el chiste es la rocola clasica, con su moneda) y va a la cola
//    como cualquier cancion; si no se puede tocar, se devuelve el credito; tras un apagon, lo pagado de Spotify vuelve como creditos. Suena
//    por la cadena de la rocola (nivelado al de las no medidas, el local, el modo billar, el limitador; sin DSEE).
//  - De Spotify solo se aprende su volumen y su EQ (SP3, SpotifyMas.cs: bajo su ruta de Spotify, como lo tuyo); no cuenta para lo mas
//    sonado ni lo mas pedido, y no se guarda en el estado (tras un apagon lo pagado vuelve como credito). Nunca se graba ni se descarga
//    musica: el sonido va de la memoria a la tarjeta.
// Apagado, el programa ni se abre y la rocola es la de siempre, bit a bit.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // Una conexion con el enchufe de control de rocola-spotify: una orden JSON por linea, una respuesta por linea.
    internal sealed class ControlSpotify : IDisposable
    {
        private readonly string ruta;
        private readonly object cerrojo = new object();
        private Socket sock; private StreamReader lee; private StreamWriter escribe;
        internal ControlSpotify(string ruta) { this.ruta = ruta; }
        // La respuesta (o null si no hubo: el programa aun no abre el enchufe, se cerro, tardo mas de 'ms').
        internal JsonDocument Pide(string orden, int ms)
        {
            lock (cerrojo)
            {
                for (int intento = 0; intento < 2; intento++)
                {
                    bool vieja = sock != null;
                    try
                    {
                        if (sock == null)
                        {
                            if (!File.Exists(ruta)) return null;
                            Socket s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                            s.Connect(new UnixDomainSocketEndPoint(ruta));
                            NetworkStream ns = new NetworkStream(s, true);
                            sock = s; lee = new StreamReader(ns, new UTF8Encoding(false)); escribe = new StreamWriter(ns, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                        }
                        sock.ReceiveTimeout = ms; sock.SendTimeout = ms;
                        escribe.WriteLine(orden);
                        string l = lee.ReadLine();
                        if (l == null) throw new IOException("se cerro");
                        return JsonDocument.Parse(l);
                    }
                    catch { Cierra(); if (!vieja) return null; }   // con una conexion recien abierta no se repite (la orden pudo haber llegado)
                }
                return null;
            }
        }
        private void Cierra() { try { sock?.Dispose(); } catch { } sock = null; lee = null; escribe = null; }
        public void Dispose() { lock (cerrojo) Cierra(); }
        internal static string Orden(string op, params string[] claveValor)
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (Utf8JsonWriter w = new Utf8JsonWriter(m))
                {
                    w.WriteStartObject(); w.WriteString("op", op);
                    for (int i = 0; i + 1 < claveValor.Length; i += 2) w.WriteString(claveValor[i], claveValor[i + 1]);
                    w.WriteEndObject();
                }
                return Encoding.UTF8.GetString(m.ToArray());
            }
        }
    }

    // El programa rocola-spotify y su sonido: un hilo lo lee de la tuberia (float de 32 bits, 44100 Hz, estereo) a un anillo de 1,5 s.
    internal sealed class SpotifyEnVivo : IDisposable
    {
        internal const int Hz = 44100, Canales = 2;
        internal static string Binario { get { return Env("ROCOLA_SPOTIFY_PROG", "/usr/bin/rocola-spotify"); } }
        internal static string CarpetaRun { get { return Path.Combine(Env("ROCOLA_RUN", "/run"), "rocola-spotify"); } }
        private static string Env(string n, string porOmision) { string v = null; try { v = Environment.GetEnvironmentVariable(n); } catch { } return string.IsNullOrEmpty(v) ? porOmision : v; }

        private Process proc;
        private Thread lee, errores, mira;
        private readonly float[] anillo = new float[Hz * Canales * 3 / 2];
        private int inicio, cuenta;
        private readonly object cerrojo = new object();
        private volatile bool cerrando;
        private volatile bool callado = true;   // lo que llegue sin haberlo pedido la rocola, se tira
        private string bitacora;
        internal ControlSpotify Estado, Toca, Api;   // tres conexiones: preguntar como va, tocar/parar (al momento) y la API (puede tardar)

        // Lo que dice el programa (lo pregunta un hilo cada 400 ms).
        internal volatile bool Conectado, EsperandoCuenta;
        internal volatile string Usuario = "", Acabo = "", Error = "";
        private long seq;
        internal long Seq { get { return Interlocked.Read(ref seq); } }   // cuantas acabaron (o no se pudieron tocar) desde que abrio

        internal bool Vivo { get { Process p = proc; try { return p != null && !p.HasExited; } catch { return false; } } }
        internal bool Callado { get { return callado; } }
        internal int Disponible { get { lock (cerrojo) return cuenta / Canales; } }

        internal void Arranca(string datos, string nombre)
        {
            string cache = Path.Combine(datos, "spotify");
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(CarpetaRun);
            // las portadas de mas de un dia (estan en la memoria: que no se acumulen si la rocola pasa dias sin reiniciar)
            try { foreach (string f in Directory.GetFiles(Path.Combine(CarpetaRun, "portadas"))) if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-1)) File.Delete(f); } catch { }
            string control = Path.Combine(CarpetaRun, "control.sock");
            try { File.Delete(control); } catch { }
            bitacora = Path.Combine(datos, "rocola_spotify.txt");
            ProcessStartInfo si = new ProcessStartInfo(Binario) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string a in new[] { "--cache", cache, "--nombre", nombre, "--control", control }) si.ArgumentList.Add(a);
            proc = Process.Start(si);
            Estado = new ControlSpotify(control); Toca = new ControlSpotify(control); Api = new ControlSpotify(control);
            Apunta("rocola-spotify abierto");
            lee = new Thread(LeeBucle) { IsBackground = true, Name = "spotify (sonido)" };
            errores = new Thread(ErroresBucle) { IsBackground = true, Name = "spotify (avisos)" };
            mira = new Thread(MiraBucle) { IsBackground = true, Name = "spotify (estado)" };
            lee.Start(); errores.Start(); mira.Start();
        }

        // Lo que llega por la tuberia: al anillo (lleno, se espera: el programa espera con el y no se pierde nada). Callado (nada pedido, o
        // tras SALTAR), se tira al ritmo del reloj.
        private void LeeBucle()
        {
            try
            {
                Stream s = proc.StandardOutput.BaseStream;
                byte[] b = new byte[8192];
                float[] f = new float[b.Length / 4];
                int resto = 0;
                while (!cerrando)
                {
                    int n = s.Read(b, resto, b.Length - resto);
                    if (n <= 0) break;
                    n += resto;
                    int util = n - n % (4 * Canales), muestras = util / 4;
                    MemoryMarshal.Cast<byte, float>(b.AsSpan(0, util)).CopyTo(f);
                    resto = n - util;
                    if (resto > 0) Buffer.BlockCopy(b, util, b, 0, resto);
                    if (muestras == 0) continue;
                    if (callado) continue;   // (lo de una cancion ya parada o saltada: se tira enseguida, que no se cuele en la siguiente)
                    int hecho = 0;
                    lock (cerrojo)
                    {
                        while (hecho < muestras && !cerrando && !callado)
                        {
                            int libre = anillo.Length - cuenta;
                            if (libre == 0) { Monitor.Wait(cerrojo, 100); continue; }
                            int k = Math.Min(libre, muestras - hecho);
                            int fin = (inicio + cuenta) % anillo.Length;
                            int k1 = Math.Min(k, anillo.Length - fin);
                            Array.Copy(f, hecho, anillo, fin, k1);
                            if (k > k1) Array.Copy(f, hecho + k1, anillo, 0, k - k1);
                            cuenta += k; hecho += k;
                        }
                    }
                }
            }
            catch (Exception ex) { if (!cerrando) Apunta("se corto el sonido de rocola-spotify: " + ex.Message); }
        }
        private void ErroresBucle()
        {
            try { string l; while (!cerrando && (l = proc.StandardError.ReadLine()) != null) Apunta(l); } catch { }
        }
        private void MiraBucle()
        {
            while (!cerrando)
            {
                try
                {
                    using (JsonDocument d = Estado.Pide("{\"op\":\"estado\"}", 3000))
                    {
                        if (d != null)
                        {
                            JsonElement r = d.RootElement;
                            Conectado = Bool(r, "conectado"); EsperandoCuenta = Bool(r, "esperando_cuenta");
                            Usuario = Texto(r, "usuario"); Error = Texto(r, "error");
                            long sq = r.TryGetProperty("seq", out JsonElement se) && se.ValueKind == JsonValueKind.Number ? se.GetInt64() : 0;
                            Acabo = Texto(r, "acabo"); Interlocked.Exchange(ref seq, sq);   // (primero cual, luego el numero: quien vea el numero ve cual)
                        }
                        else Conectado = false;
                    }
                }
                catch { }
                Thread.Sleep(400);
            }
        }
        internal static bool Bool(JsonElement e, string k) { JsonElement v; return e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.True; }
        internal static string Texto(JsonElement e, string k) { JsonElement v; return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : ""; }

        private readonly object cerrojoBitacora = new object();
        internal void Apunta(string linea)
        {
            if (bitacora == null) return;
            lock (cerrojoBitacora)
            {
                try
                {
                    FileInfo fi = new FileInfo(bitacora);
                    if (fi.Exists && fi.Length > 256 * 1024) { File.Copy(bitacora, bitacora + ".1", true); File.WriteAllText(bitacora, ""); }
                    File.AppendAllText(bitacora, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + linea + "\n");
                }
                catch { }
            }
        }

        // Para la salida de sonido (su hilo): lo que haya, sin esperar nunca.
        internal int Toma(Span<float> destino)
        {
            lock (cerrojo)
            {
                int k = Math.Min(destino.Length, cuenta);
                k -= k % Canales;
                int k1 = Math.Min(k, anillo.Length - inicio);
                anillo.AsSpan(inicio, k1).CopyTo(destino);
                if (k > k1) anillo.AsSpan(0, k - k1).CopyTo(destino.Slice(k1));
                inicio = (inicio + k) % anillo.Length; cuenta -= k;
                if (k > 0) Monitor.PulseAll(cerrojo);
                return k;
            }
        }
        // Calla: el anillo se vacia y lo que llegue se tira. Habla: desde ahora, al anillo (justo antes de pedir una cancion).
        internal void Calla() { lock (cerrojo) { callado = true; inicio = 0; cuenta = 0; Monitor.PulseAll(cerrojo); } }
        internal void Habla() { lock (cerrojo) { inicio = 0; cuenta = 0; callado = false; } }

        public void Dispose()
        {
            cerrando = true;
            lock (cerrojo) Monitor.PulseAll(cerrojo);
            try { if (proc != null && !proc.HasExited) proc.Kill(true); } catch { }
            try { proc?.WaitForExit(2000); } catch { }
            try { lee?.Join(1000); errores?.Join(1000); mira?.Join(1000); } catch { }
            Estado?.Dispose(); Toca?.Dispose(); Api?.Dispose();
            try { proc?.Dispose(); } catch { }
            proc = null;
            Apunta("rocola-spotify cerrado");
        }
    }

    public partial class Form1
    {
        public bool SpotifyOn = false;   // «spotify=»: Spotify dentro de la rocola (reuniones familiares). APAGADO de fabrica.
        public const char LetraSpotify = '♫';   // la «letra» SPOTIFY de la pared (como TOP y VIDEOS)
        internal const string RaizSpotify = "/spotify/";
        internal const float RmsSpotify = 0.2f;   // lo que deja la normalizacion de Spotify (cerca de -14 LUFS), en la escala de normTarget
        private const int SpotifySinSonidoMs = 20000;   // pedida y sin sonido en este tiempo: no se pudo tocar
        private const int SustitutaSinSonidoMs = 6000;  // (la tuya sustituida: antes, para que suene tu MP3 sin mucha espera)
        private SpotifyEnVivo spotify;
        private int spotifyReintentoDesde, spotifyFallos, spotifyMirado;

        // Una cancion de Spotify: su ruta en la rocola es /spotify/ID/Artista - Titulo.spotify (lo que se ve es el nombre del «archivo»).
        private sealed class PistaSpotify { public string Id, Titulo, Artista, Disco, DiscoCarpeta, PortadaUrl, ArtistaId, Anio; public long Ms; }
        private readonly object spotifyCerrojo = new object();
        private readonly Dictionary<string, PistaSpotify> spotifyPistas = new Dictionary<string, PistaSpotify>(StringComparer.Ordinal);
        private readonly Dictionary<string, Disco> spotifyDiscosPorCarpeta = new Dictionary<string, Disco>(StringComparer.Ordinal);
        private readonly Dictionary<Disco, string> spotifyPortadaUrl = new Dictionary<Disco, string>();
        private List<Disco> spotifyPared = new List<Disco>();
        private volatile int spotifyVersion;
        private int spotifyBibliotecaDesde;
        private volatile bool spotifyCargandoPared;
        private readonly HashSet<Disco> spotifyCargando = new HashSet<Disco>();

        public static bool EsRutaSpotify(string ruta) { return ruta != null && ruta.StartsWith(RaizSpotify, StringComparison.Ordinal); }
        private bool EsItemSpotify(PlayItem it) { return it != null && !it.IsVideo && it.Song != null && EsRutaSpotify(it.Song.Path); }
        public bool SonandoSpotify { get { return EsItemSpotify(nowPlaying); } }
        public int VersionSpotify { get { return spotifyVersion; } }
        public bool SpotifyListo { get { SpotifyEnVivo s = spotify; return SpotifyOn && s != null && s.Conectado; } }
        // Los discos de la letra SPOTIFY (una copia: la pared no se mueve mientras se mira).
        public List<Disco> DiscosSpotify() { lock (spotifyCerrojo) return new List<Disco>(spotifyPared); }

        // Llamarlo a menudo (el bucle de la rocola): abre o cierra el programa segun el interruptor y trae la biblioteca.
        // LA SIGUIENTE, YA BAJADA (7-11-2026, «que todo cargue extremadamente rapido»): si la primera de la cola va a sonar desde Spotify
        // (de Spotify, o tuya sustituida: SP2), se le pide a rocola-spotify que la vaya bajando; al tocarla, empieza al momento en vez de
        // esperar a que llegue. Una vez por cancion, en segundo plano (la orden vuelve al instante). Si la cola cambia, no pasa nada.
        private string precargada;
        private void PrecargaSiguiente()
        {
            SpotifyEnVivo s = spotify;
            if (s == null || !s.Conectado || queue.Count == 0) return;
            PlayItem it = queue[0]; string id = null;
            if (EsItemSpotify(it)) { id = it.Song.Path.Substring(RaizSpotify.Length); int barra = id.IndexOf('/'); if (barra > 0) id = id.Substring(0, barra); }
            else id = IdParaSustituir(it);
            if (id == null || id == precargada) return;
            precargada = id; string uri = "spotify:track:" + id;
            ThreadPool.QueueUserWorkItem(delegate { try { using (s.Toca.Pide(ControlSpotify.Orden("precarga", "uri", uri), 1500)) { } } catch { } });
        }
        public void AtiendeSpotify()
        {
            int ahora = Environment.TickCount;
            if (spotifyMirado != 0 && unchecked(ahora - spotifyMirado) < 250) return;
            spotifyMirado = ahora | 1;
            bool quiere = SpotifyOn && File.Exists(SpotifyEnVivo.Binario);
            if (!quiere)
            {
                if (SonandoSpotify) return;   // primero baja lo que suena (el bucle de la rocola: SalidaAlsa.Apaga), luego se cierra
                if (spotifySustituida != null && object.ReferenceEquals(nowPlaying, spotifySustituida)) return;   // una tuya desde Spotify (SP2): que acabe entera
                if (spotify != null) { SpotifyEnVivo s = spotify; spotify = null; s.Calla(); s.Dispose(); }
                int quitadas = queue.RemoveAll(EsItemSpotify);
                if (quitadas > 0) { credits += quitadas; UpdateCredits(); SaveState(); Avisa("Spotify apagado: se quitaron de la cola sus " + quitadas + (quitadas == 1 ? " cancion" : " canciones") + " y se devuelven los creditos"); }
                lock (spotifyCerrojo) { if (spotifyPared.Count > 0) { spotifyPared = new List<Disco>(); spotifyVersion++; } }
                spotifyBibliotecaDesde = 0;
                return;
            }
            if (spotify == null || !spotify.Vivo)
            {
                int espera = spotify == null && spotifyFallos == 0 ? 0 : Math.Min(300000, 5000 << Math.Min(6, spotifyFallos));
                if (spotifyReintentoDesde != 0 && unchecked(ahora - spotifyReintentoDesde) < espera) return;
                if (spotify != null) { spotify.Apunta("rocola-spotify se cerro solo; se vuelve a abrir"); spotify.Dispose(); spotify = null; spotifyFallos++; }
                spotifyReintentoDesde = ahora | 1;
                try { SpotifyEnVivo s = new SpotifyEnVivo(); s.Arranca(CarpetaDatos, "Rocola"); spotify = s; }
                catch (Exception ex) { spotifyFallos++; LogError("Spotify: no se pudo abrir rocola-spotify", ex); }
                return;
            }
            PrecargaSiguiente();
            // LA BIBLIOTECA: al entrar y cada 30 min (listas nuevas), en segundo plano.
            if (spotify.Conectado && !spotifyCargandoPared && (spotifyBibliotecaDesde == 0 || unchecked(ahora - spotifyBibliotecaDesde) > 30 * 60 * 1000))
            {
                if (spotifyBibliotecaDesde == 0) { LeeParejas(); AplicaParejasGuardadas(); ArrancaEmparejador(); ArrancaDiscografo(); }   // SP4, SP5 (SpotifyMas.cs); P1-P3 (SpotifyPortadas.cs)
                spotifyBibliotecaDesde = ahora | 1; spotifyCargandoPared = true; spotifyFallos = 0;
                SpotifyEnVivo s = spotify;
                ThreadPool.QueueUserWorkItem(delegate { try { CargaPared(s); } catch (Exception ex) { LogError("Spotify: la biblioteca", ex); } finally { spotifyCargandoPared = false; } });
            }
        }

        // ---- la API web de Spotify (por rocola-spotify, con la llave de la sesion) ----
        private static JsonDocument Api(SpotifyEnVivo s, string ruta)
        {
            if (s == null) return null;
            using (JsonDocument d = s.Api.Pide(ControlSpotify.Orden("api", "ruta", ruta), 20000))
            {
                if (d == null) return null;
                JsonElement r = d.RootElement;
                if (!SpotifyEnVivo.Bool(r, "ok")) { s.Apunta("API " + ruta + ": " + SpotifyEnVivo.Texto(r, "error") + " " + (r.TryGetProperty("estado", out JsonElement e) ? e.ToString() : "") + " " + Recorta(SpotifyEnVivo.Texto(r, "cuerpo"), 200)); return null; }
                try { return JsonDocument.Parse(SpotifyEnVivo.Texto(r, "cuerpo")); } catch { return null; }
            }
        }
        private static string Recorta(string s, int n) { return s.Length <= n ? s : s.Substring(0, n); }
        private static string Q(string s) { return Uri.EscapeDataString(s ?? ""); }

        // Las portadas: las baja rocola-spotify a /run/rocola-spotify/portadas (en la memoria; no al disco).
        private static string BajaPortada(SpotifyEnVivo s, string url, string clave)
        {
            if (s == null || string.IsNullOrEmpty(url)) return null;
            string dir = Path.Combine(SpotifyEnVivo.CarpetaRun, "portadas");
            string archivo = Path.Combine(dir, LimpiaClave(clave) + ".jpg");
            if (File.Exists(archivo)) return archivo;
            try { Directory.CreateDirectory(dir); } catch { }
            using (JsonDocument d = s.Api.Pide(ControlSpotify.Orden("baja", "url", url, "archivo", archivo), 20000))
                return d != null && SpotifyEnVivo.Bool(d.RootElement, "ok") && File.Exists(archivo) ? archivo : null;
        }
        private static string LimpiaClave(string s) { StringBuilder b = new StringBuilder(); foreach (char c in s) b.Append(char.IsLetterOrDigit(c) ? c : '_'); return b.ToString(); }
        private static string Imagen(JsonElement o)
        {
            // la mas cercana a 300 px (las de Spotify vienen de 640, 300 y 64)
            if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty("images", out JsonElement im) || im.ValueKind != JsonValueKind.Array) return null;
            string mejor = null; int dif = int.MaxValue;
            foreach (JsonElement i in im.EnumerateArray())
            {
                string u = SpotifyEnVivo.Texto(i, "url"); if (u.Length == 0) continue;
                int w = i.TryGetProperty("width", out JsonElement we) && we.ValueKind == JsonValueKind.Number ? we.GetInt32() : 300;
                int d = Math.Abs(w - 300); if (d < dif) { dif = d; mejor = u; }
            }
            return mejor;
        }
        private static string Artistas(JsonElement o)
        {
            if (!o.TryGetProperty("artists", out JsonElement a) || a.ValueKind != JsonValueKind.Array) return "";
            StringBuilder b = new StringBuilder();
            foreach (JsonElement x in a.EnumerateArray()) { string n = SpotifyEnVivo.Texto(x, "name"); if (n.Length > 0) { if (b.Length > 0) b.Append(", "); b.Append(n); } }
            return b.ToString();
        }
        private static string LimpiaNombre(string s)
        {
            StringBuilder b = new StringBuilder(s.Length);
            foreach (char c in s) b.Append(c == '/' || c == '\\' || char.IsControl(c) ? '-' : c);
            return b.Length > 0 ? b.ToString() : "Spotify";
        }
        // Apunta una cancion de Spotify (de una respuesta de la API) y devuelve su ruta en la rocola; null si no se puede tocar.
        private string ApuntaPista(JsonElement t, string disco, string discoCarpeta, string portadaUrl)
        {
            if (t.ValueKind != JsonValueKind.Object || SpotifyEnVivo.Texto(t, "type") is string ty && ty.Length > 0 && ty != "track") return null;
            string id = SpotifyEnVivo.Texto(t, "id");
            if (id.Length == 0 || (t.TryGetProperty("is_local", out JsonElement lo) && lo.ValueKind == JsonValueKind.True)) return null;
            if (t.TryGetProperty("is_playable", out JsonElement pl) && pl.ValueKind == JsonValueKind.False) return null;
            PistaSpotify p = new PistaSpotify { Id = id, Titulo = SpotifyEnVivo.Texto(t, "name"), Artista = Artistas(t), Disco = disco, DiscoCarpeta = discoCarpeta, PortadaUrl = portadaUrl };
            if (t.TryGetProperty("artists", out JsonElement ars) && ars.ValueKind == JsonValueKind.Array)
                foreach (JsonElement a0 in ars.EnumerateArray()) { p.ArtistaId = SpotifyEnVivo.Texto(a0, "id"); break; }
            if (t.TryGetProperty("album", out JsonElement al) && al.ValueKind == JsonValueKind.Object)
            {
                string fecha = SpotifyEnVivo.Texto(al, "release_date"); if (fecha.Length >= 4) p.Anio = fecha.Substring(0, 4);
                if (string.IsNullOrEmpty(p.Disco)) p.Disco = SpotifyEnVivo.Texto(al, "name");
                if (string.IsNullOrEmpty(p.DiscoCarpeta)) p.DiscoCarpeta = RaizSpotify + "album/" + SpotifyEnVivo.Texto(al, "id");
                p.PortadaUrl = Imagen(al) ?? p.PortadaUrl;
            }
            if (t.TryGetProperty("duration_ms", out JsonElement du) && du.ValueKind == JsonValueKind.Number) p.Ms = du.GetInt64();
            string ruta = RaizSpotify + id + "/" + LimpiaNombre(p.Artista.Length > 0 ? p.Artista + " - " + p.Titulo : p.Titulo) + ".spotify";
            lock (spotifyCerrojo) spotifyPistas[ruta] = p;
            return ruta;
        }
        private Disco DiscoSpotify(string carpeta, string nombre, string artista, string portadaUrl)
        {
            lock (spotifyCerrojo)
            {
                Disco d;
                if (!spotifyDiscosPorCarpeta.TryGetValue(carpeta, out d))
                {
                    d = new Disco { Carpeta = carpeta, Nombre = nombre, Artista = artista, Letra = LetraSpotify };
                    spotifyDiscosPorCarpeta[carpeta] = d;
                }
                if (!string.IsNullOrEmpty(portadaUrl)) spotifyPortadaUrl[d] = portadaUrl;
                return d;
            }
        }

        // LA LETRA SPOTIFY: tus favoritas, los exitos, tus listas, por genero y epoca (P4), tus artistas con foto (P3) y tus discos guardados
        // (hasta 50 de cada), con sus portadas.
        private void CargaPared(SpotifyEnVivo s)
        {
            bool alDia; lock (spotifyCerrojo) alDia = spotifyPared.Count > 0;   // (no es la primera vez: las listas se vuelven a pedir)
            List<Disco> pared = new List<Disco>();
            Disco fav = DiscoSpotify(RaizSpotify + "favoritas", "Tus favoritas", "Spotify", null);
            pared.Add(fav);
            try { pared.AddRange(ExitosDelMomento(s)); } catch (Exception ex) { LogError("Spotify: exitos", ex); }   // SP8
            List<Disco> generos = new List<Disco>(), artistas = new List<Disco>(), guardados = new List<Disco>();
            try { generos = PorGeneroYEpoca(s); } catch (Exception ex) { LogError("Spotify: por genero", ex); }   // P4
            try { artistas = TusArtistas(s); } catch (Exception ex) { LogError("Spotify: tus artistas", ex); }      // P3
            using (JsonDocument d = Api(s, "/v1/me/playlists?limit=50"))
                if (d != null && d.RootElement.TryGetProperty("items", out JsonElement it) && it.ValueKind == JsonValueKind.Array)
                    foreach (JsonElement p in it.EnumerateArray())
                    {
                        string id = SpotifyEnVivo.Texto(p, "id"); if (id.Length == 0) continue;
                        string duenio = p.TryGetProperty("owner", out JsonElement ow) ? SpotifyEnVivo.Texto(ow, "display_name") : "";
                        pared.Add(DiscoSpotify(RaizSpotify + "playlist/" + id, SpotifyEnVivo.Texto(p, "name"), duenio.Length > 0 ? duenio : "Spotify", Imagen(p)));
                    }
            pared.AddRange(generos); pared.AddRange(artistas);
            using (JsonDocument d = Api(s, "/v1/me/albums?limit=50"))
                if (d != null && d.RootElement.TryGetProperty("items", out JsonElement it) && it.ValueKind == JsonValueKind.Array)
                    foreach (JsonElement x in it.EnumerateArray())
                    {
                        if (!x.TryGetProperty("album", out JsonElement a)) continue;
                        string id = SpotifyEnVivo.Texto(a, "id"); if (id.Length == 0) continue;
                        Disco disco = DiscoSpotify(RaizSpotify + "album/" + id, SpotifyEnVivo.Texto(a, "name"), Artistas(a), Imagen(a));
                        if (disco.Rutas.Count == 0 && a.TryGetProperty("tracks", out JsonElement tr) && tr.TryGetProperty("items", out JsonElement ti))
                            LlenaDisco(disco, ti, disco.Nombre, disco.Carpeta, Imagen(a), false);
                        pared.Add(disco); guardados.Add(disco);
                    }
            lock (spotifyCerrojo) { spotifyPared = pared; spotifyGuardados = guardados; spotifyVersion++; }
            letrasSpVersion++;   // P1: tus discos guardados, tambien en su letra
            s.Apunta("biblioteca: " + (pared.Count - 1) + " listas y discos");
            // las portadas, despues (la pared ya se ve; cada una aparece al llegar), y las canciones de las listas y las favoritas por
            // adelantado (para que digan cuantas tienen y se entre sin esperar), una por una
            foreach (Disco x in pared) { if (spotify != s) return; PonPortada(s, x); }
            try { CargaAtajos(s); } catch (Exception ex) { LogError("Spotify: atajos", ex); }   // SP9
            foreach (Disco x in pared)
            {
                if (spotify != s) return;   // se apago o se reabrio
                bool lista = x.Carpeta.StartsWith(RaizSpotify + "playlist/", StringComparison.Ordinal) || x.Carpeta == RaizSpotify + "favoritas";
                if (generos.Contains(x) || artistas.Contains(x)) continue;   // (se abren al entrar: son muchas canciones para traerlas todas de antemano)
                if (x.Rutas.Count == 0 || (alDia && lista)) { CargaSpotify(x, true); for (int i = 0; i < 100 && spotifyCargandoDisco(x); i++) Thread.Sleep(100); }
            }
        }
        private bool spotifyCargandoDisco(Disco d) { lock (spotifyCerrojo) return spotifyCargando.Contains(d); }
        private void PonPortada(SpotifyEnVivo s, Disco d)
        {
            string url; lock (spotifyCerrojo) spotifyPortadaUrl.TryGetValue(d, out url);
            if (d.Portada == null && url != null) { string p = BajaPortada(s, url, d.Carpeta); if (p != null) { d.Portada = p; spotifyVersion++; } }
        }
        private void LlenaDisco(Disco d, JsonElement pistas, string disco, string carpeta, string portada, bool conArtista, bool soloTitulo = false)
        {
            List<string> rutas = new List<string>(), titulos = new List<string>();
            long ms = 0; bool todas = true;
            foreach (JsonElement x in pistas.EnumerateArray())
            {
                JsonElement t = x;
                if (x.TryGetProperty("track", out JsonElement tt) && tt.ValueKind == JsonValueKind.Object) t = tt;   // listas y favoritas: {added_at, track}
                else if (x.TryGetProperty("item", out JsonElement ti) && ti.ValueKind == JsonValueKind.Object) t = ti;
                string ruta = ApuntaPista(t, conArtista ? null : disco, conArtista ? null : carpeta, portada);
                if (ruta == null) continue;
                PistaSpotify p; lock (spotifyCerrojo) p = spotifyPistas[ruta];
                rutas.Add(ruta); titulos.Add(conArtista && !soloTitulo && p.Artista.Length > 0 ? p.Artista + " - " + p.Titulo : p.Titulo);
                if (p.Ms > 0) ms += p.Ms; else todas = false;
            }
            d.Titulos = titulos; d.Rutas = rutas;   // (en este orden: lo que se cuenta son las rutas)
            d.Minutos = todas && rutas.Count > 0 ? (int)Math.Round(ms / 60000.0) : -1;
        }

        // Las canciones de un disco de Spotify, al entrar en el (en segundo plano; mientras, el disco esta vacio).
        public void CargaSpotify(Disco d) { CargaSpotify(d, false); }
        private void CargaSpotify(Disco d, bool otraVez)
        {
            if (d == null || !EsRutaSpotify(d.Carpeta) || (d.Rutas.Count > 0 && !otraVez)) return;
            SpotifyEnVivo s = spotify; if (s == null) return;
            lock (spotifyCerrojo) { if (!spotifyCargando.Add(d)) return; }
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string c = d.Carpeta.Substring(RaizSpotify.Length);
                    string portada; lock (spotifyCerrojo) spotifyPortadaUrl.TryGetValue(d, out portada);
                    if (c == "favoritas") Paginas(s, d, "/v1/me/tracks?limit=50&offset=", 50, 500, true, portada);
                    else if (c.StartsWith("playlist/"))
                    {
                        string id = c.Substring(9);
                        if (!Paginas(s, d, "/v1/playlists/" + id + "/tracks?limit=100&offset=", 100, 500, true, portada))
                            Paginas(s, d, "/v1/playlists/" + id + "/items?limit=100&offset=", 100, 500, true, portada);   // (el nombre nuevo de la API)
                    }
                    else if (c.StartsWith("artist/"))
                    {   // P3: un artista: lo mas conocido (cada cancion con su disco); sus discos van en la tira (P2)
                        using (JsonDocument a = Api(s, "/v1/artists/" + c.Substring(7) + "/top-tracks?market=from_token"))
                            if (a != null && a.RootElement.TryGetProperty("tracks", out JsonElement tr) && tr.ValueKind == JsonValueKind.Array)
                                LlenaDisco(d, tr, null, null, null, true, true);
                    }
                    else if (c.StartsWith("album/"))
                    {
                        string id = c.Substring(6);
                        using (JsonDocument a = Api(s, "/v1/albums/" + id))
                            if (a != null)
                            {
                                JsonElement r = a.RootElement;
                                if (string.IsNullOrEmpty(d.Nombre)) d.Nombre = SpotifyEnVivo.Texto(r, "name");
                                if (string.IsNullOrEmpty(d.Artista)) d.Artista = Artistas(r);
                                portada = Imagen(r) ?? portada;
                                lock (spotifyCerrojo) if (portada != null) spotifyPortadaUrl[d] = portada;
                                if (r.TryGetProperty("tracks", out JsonElement tr) && tr.TryGetProperty("items", out JsonElement ti)) LlenaDisco(d, ti, d.Nombre, d.Carpeta, portada, false);
                            }
                    }
                    if (d.Rutas.Count == 0) Avisa("Spotify: no se pudo abrir «" + d + "»");
                    PonPortada(s, d);
                    spotifyVersion++;
                }
                catch (Exception ex) { LogError("Spotify: abrir " + d.Carpeta, ex); }
                finally { lock (spotifyCerrojo) spotifyCargando.Remove(d); }
            });
        }
        private bool Paginas(SpotifyEnVivo s, Disco d, string ruta, int porPagina, int max, bool conArtista, string portada)
        {
            List<JsonDocument> docs = new List<JsonDocument>();
            try
            {
                for (int desde = 0; desde < max; desde += porPagina)
                {
                    JsonDocument doc = Api(s, ruta + desde);
                    if (doc == null) break;
                    docs.Add(doc);
                    if (!doc.RootElement.TryGetProperty("next", out JsonElement nx) || nx.ValueKind != JsonValueKind.String) break;
                }
                if (docs.Count == 0) return false;
                // todas las paginas juntas, en un arreglo
                using (MemoryStream m = new MemoryStream())
                {
                    using (Utf8JsonWriter w = new Utf8JsonWriter(m))
                    {
                        w.WriteStartArray();
                        foreach (JsonDocument doc in docs)
                            if (doc.RootElement.TryGetProperty("items", out JsonElement it) && it.ValueKind == JsonValueKind.Array)
                                foreach (JsonElement x in it.EnumerateArray()) x.WriteTo(w);
                        w.WriteEndArray();
                    }
                    using (JsonDocument todo = JsonDocument.Parse(m.ToArray())) LlenaDisco(d, todo.RootElement, d.Nombre, d.Carpeta, portada, conArtista);
                }
                return d.Rutas.Count > 0;
            }
            finally { foreach (JsonDocument doc in docs) doc.Dispose(); }
        }

        // EL BUSCADOR: lo que se escribe se busca tambien en Spotify (en segundo plano, cuando se deja de escribir 0,6 s). La pantalla
        // pregunta con ResultadosSpotify y los anade detras de los de la biblioteca.
        private string spotifyBuscaPedido, spotifyBuscaHecho;
        private List<Resultado> spotifyBuscaRes;
        private int spotifyBuscaDesde;
        private bool spotifyBuscando;
        public void PideBusquedaSpotify(string texto)
        {
            lock (spotifyCerrojo) { spotifyBuscaPedido = (texto ?? "").Trim(); spotifyBuscaDesde = Environment.TickCount; }
        }
        // Los resultados de Spotify para ese texto (null si aun no estan).
        public List<Resultado> ResultadosSpotify(string texto)
        {
            SpotifyEnVivo s = spotify;
            if (!SpotifyOn || s == null || !s.Conectado) return null;
            string t = (texto ?? "").Trim();
            lock (spotifyCerrojo)
            {
                if (t.Length >= 2 && t == spotifyBuscaPedido && t != spotifyBuscaHecho && !spotifyBuscando && unchecked(Environment.TickCount - spotifyBuscaDesde) >= 600)
                {
                    spotifyBuscando = true;
                    ThreadPool.QueueUserWorkItem(delegate { try { BuscaEnSpotify(s, t); } catch (Exception ex) { LogError("Spotify: buscar", ex); lock (spotifyCerrojo) spotifyBuscando = false; } });
                }
                return t == spotifyBuscaHecho ? spotifyBuscaRes : null;
            }
        }
        private void BuscaEnSpotify(SpotifyEnVivo s, string texto)
        {
            List<Resultado> r = new List<Resultado>();
            try
            {
                using (JsonDocument d = Api(s, "/v1/search?type=track,album,playlist,artist&limit=10&q=" + Q(texto)))
                    if (d != null)
                    {
                        JsonElement root = d.RootElement;
                        if (root.TryGetProperty("tracks", out JsonElement tr) && tr.TryGetProperty("items", out JsonElement ti))
                            foreach (JsonElement t in ti.EnumerateArray())
                            {
                                string ruta = ApuntaPista(t, null, null, null); if (ruta == null) continue;
                                PistaSpotify p; lock (spotifyCerrojo) p = spotifyPistas[ruta];
                                r.Add(new Resultado { Tipo = 2, Ruta = ruta, Texto = p.Titulo, Detalle = "Spotify · " + p.Artista, Carpeta = p.DiscoCarpeta });
                            }
                        // LOS ARTISTAS QUE NO ESTAN EN LA ROCOLA (pedido del dueno del 7-11-2026): su tarjeta de Spotify, con su foto; al
                        // elegirla se abre su pagina (lo mas conocido y sus discos en la tira) y REGRESAR vuelve al buscador. Los que si estan
                        // ya salen como siempre (y llevan a sus discos de la pared). El primero que da Spotify va siempre; los demas, solo si
                        // su nombre casa con lo escrito. Hasta tres.
                        if (root.TryGetProperty("artists", out JsonElement ar) && ar.TryGetProperty("items", out JsonElement ari))
                        {
                            string q = Normaliza(texto); int n = 0, i = 0;
                            foreach (JsonElement a in ari.EnumerateArray())
                            {
                                if (a.ValueKind != JsonValueKind.Object) continue;
                                string id = SpotifyEnVivo.Texto(a, "id"), nombre = SpotifyEnVivo.Texto(a, "name"), nn = Normaliza(nombre);
                                bool primero = i++ == 0;
                                if (id.Length == 0 || nn.Length == 0 || n >= 3) continue;
                                // que se parezca a lo escrito: lo contiene o esta contenido, o (el primero, por si hay una falta) a dos letras
                                bool casa = nn.Contains(q) || q.Contains(nn) || (primero && Cerca(nn, q, 2));
                                if (!casa) continue;
                                if (ArtistaEnLaRocola(id, nn)) continue;
                                Disco disco = DiscoSpotify(RaizSpotify + "artist/" + id, "", nombre, Imagen(a));
                                int seg = a.TryGetProperty("followers", out JsonElement fo) && fo.ValueKind == JsonValueKind.Object && fo.TryGetProperty("total", out JsonElement ft) && ft.ValueKind == JsonValueKind.Number ? ft.GetInt32() : 0;
                                r.Insert(n, new Resultado { Tipo = 0, Carpeta = disco.Carpeta, Texto = nombre, Detalle = "Spotify · artista" + (seg >= 1000 ? " · " + Seguidores(seg) : "") });
                                n++;
                            }
                        }
                        if (root.TryGetProperty("albums", out JsonElement al) && al.TryGetProperty("items", out JsonElement ai))
                            foreach (JsonElement a in ai.EnumerateArray())
                            {
                                string id = SpotifyEnVivo.Texto(a, "id"); if (id.Length == 0) continue;
                                Disco disco = DiscoSpotify(RaizSpotify + "album/" + id, SpotifyEnVivo.Texto(a, "name"), Artistas(a), Imagen(a));
                                r.Add(new Resultado { Tipo = 1, Carpeta = disco.Carpeta, Texto = disco.Nombre, Detalle = "Spotify · disco · " + disco.Artista });
                            }
                        if (root.TryGetProperty("playlists", out JsonElement pl) && pl.TryGetProperty("items", out JsonElement pi))
                            foreach (JsonElement p in pi.EnumerateArray())
                            {
                                if (p.ValueKind != JsonValueKind.Object) continue;   // (Spotify da huecos null en las listas)
                                string id = SpotifyEnVivo.Texto(p, "id"); if (id.Length == 0) continue;
                                string duenio = p.TryGetProperty("owner", out JsonElement ow) ? SpotifyEnVivo.Texto(ow, "display_name") : "";
                                Disco disco = DiscoSpotify(RaizSpotify + "playlist/" + id, SpotifyEnVivo.Texto(p, "name"), duenio, Imagen(p));
                                r.Add(new Resultado { Tipo = 1, Carpeta = disco.Carpeta, Texto = disco.Nombre, Detalle = "Spotify · lista" + (duenio.Length > 0 ? " · " + duenio : "") });
                            }
                    }
            }
            catch (Exception ex) { LogError("Spotify: buscar", ex); }
            lock (spotifyCerrojo) { spotifyBuscaRes = r; spotifyBuscaHecho = texto; spotifyBuscando = false; }
            // las portadas de los resultados, despues
            foreach (Resultado x in r)
            {
                string url = null, clave = x.Carpeta ?? x.Ruta;
                if (x.Tipo == 2) { PistaSpotify p; lock (spotifyCerrojo) spotifyPistas.TryGetValue(x.Ruta, out p); url = p != null ? p.PortadaUrl : null; }
                else { Disco d; lock (spotifyCerrojo) { spotifyDiscosPorCarpeta.TryGetValue(x.Carpeta, out d); if (d != null) spotifyPortadaUrl.TryGetValue(d, out url); } }
                if (url != null && clave != null) x.Portada = BajaPortada(s, url, clave);
            }
            spotifyVersion++;
        }
        // Si 'a' y 'b' estan a 'max' cambios de letra o menos (sin espacios), contando tambien que 'b' sea el principio de 'a'.
        private static bool Cerca(string a, string b, int max)
        {
            a = a.Replace(" ", ""); b = b.Replace(" ", "");
            if (a.Length == 0 || b.Length == 0 || Math.Abs(a.Length - b.Length) > max && !a.StartsWith(b.Substring(0, Math.Min(b.Length, 3)), StringComparison.Ordinal)) return false;
            int[] p = new int[b.Length + 1], c = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) p[j] = j;
            int mejor = int.MaxValue;
            for (int i2 = 1; i2 <= a.Length; i2++)
            {
                c[0] = i2;
                for (int j = 1; j <= b.Length; j++) c[j] = Math.Min(Math.Min(c[j - 1] + 1, p[j] + 1), p[j - 1] + (a[i2 - 1] == b[j - 1] ? 0 : 1));
                if (i2 >= b.Length - max) mejor = Math.Min(mejor, c[b.Length]);   // b contra el principio de a (a medio escribir)
                int[] tt = p; p = c; c = tt;
            }
            return Math.Min(mejor, p[b.Length]) <= max;
        }
        private static string Seguidores(int n) { return n >= 1000000 ? (n / 1000000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " M de seguidores" : (n / 1000) + " mil seguidores"; }
        // Si ese artista ya esta en la rocola: por su pareja de Spotify (SP4) o porque algun disco tuyo lleva su nombre.
        private HashSet<string> nombresLocales; private int nombresLocalesDe = -1;
        private bool ArtistaEnLaRocola(string id, string nombreNormalizado)
        {
            foreach (KeyValuePair<string, ArtistaLocal> kv in ArtistasDeBiblioteca()) if (kv.Key == id) return true;
            HashSet<string> h = nombresLocales;
            if (h == null || nombresLocalesDe != albums.Count)
            {
                h = new HashSet<string>(StringComparer.Ordinal);
                try { foreach (Album a in albums.ToArray()) if (a != null && !a.IsTop && !string.IsNullOrEmpty(a.Artist)) h.Add(Normaliza(a.Artist)); } catch { }
                nombresLocales = h; nombresLocalesDe = albums.Count;
            }
            return h.Contains(nombreNormalizado);
        }
        // Un disco de Spotify de un resultado del buscador (para abrirlo).
        public Disco DiscoSpotifyDe(string carpeta)
        {
            if (!EsRutaSpotify(carpeta)) return null;
            Disco d = DiscoSpotify(carpeta, "", "", null);
            CargaSpotify(d);
            return d;
        }

        // PEDIR UNA DE SPOTIFY: a la cola como cualquier cancion, CON SU CREDITO (decision del dueno del 6-11-2026: «a mi familia le gusta
        // el tema de rocolas clasicas como Rowe AMI y quitar la parte fisica le quitaria chiste»). Lo que no cambia: no cuenta para lo mas
        // sonado ni lo mas pedido esta noche.
        public Pedido PideSpotify(string ruta)
        {
            PistaSpotify p; lock (spotifyCerrojo) spotifyPistas.TryGetValue(ruta, out p);
            if (p == null || !SpotifyOn) return Pedido.Danada;
            if (credits <= 0) { Avisa("Inserta una moneda"); return Pedido.SinCredito; }
            PlayItem last = (queue.Count > 0) ? queue[queue.Count - 1] : nowPlaying;
            if (last != null && string.Equals(last.PathKey(), ruta, StringComparison.Ordinal)) return Pedido.Repetida;
            PlayItem it = new PlayItem();
            it.Song = new Track { Path = ruta, Title = p.Titulo, Artist = p.Artista, Album = p.Disco ?? "", Genre = "" };
            Disco dd = null; if (p.DiscoCarpeta != null) lock (spotifyCerrojo) spotifyDiscosPorCarpeta.TryGetValue(p.DiscoCarpeta, out dd);
            it.Disc = new Album { Folder = p.DiscoCarpeta ?? RaizSpotify, Name = p.Disco ?? "", Artist = p.Artista, CoverPath = dd != null ? dd.Portada : null };
            if (it.Disc.CoverPath == null && p.PortadaUrl != null)
            {   // su portada, en cuanto llegue (para la barra de abajo y la cola)
                Album al = it.Disc; SpotifyEnVivo s = spotify;
                ThreadPool.QueueUserWorkItem(delegate { try { string f = BajaPortada(s, p.PortadaUrl, p.DiscoCarpeta ?? p.Id); if (f != null) al.CoverPath = f; } catch { } });
            }
            queue.Add(it);
            {   // EL TURNO, como en Pide (las de Spotify cuentan con lo que duran)
                double[] antes = new double[queue.Count - 1];
                for (int i = 0; i < queue.Count - 1; i++) antes[i] = queue[i].IsVideo ? -1 : SegundosDeCola(queue[i]);
                UltimoTurno = TextoTurno(queue.Count, EsperaSegundos(antes, RestanDeLaQueSuena()));
            }
            credits--;   // 1 credito, como todo
            UpdateCredits();
            if (nowPlaying == null) PlayNext();
            else SaveState();
            return Pedido.Puesta;
        }

        // Lo que dura una de la cola (las de Spotify, lo que dice Spotify; -1 si no se sabe).
        private double SegundosDeCola(PlayItem it)
        {
            if (EsItemSpotify(it)) { PistaSpotify p; lock (spotifyCerrojo) spotifyPistas.TryGetValue(it.Song.Path, out p); return p != null && p.Ms > 0 ? p.Ms / 1000.0 : -1; }
            return SegundosDe(it.PathKey());
        }
        // Las de Spotify pagadas que aun no suenan (y la que suena): tras un apagon vuelven como creditos (SaveState), porque no se reanudan.
        private int CreditosDeSpotifyPendientes()
        {
            int n = EsItemSpotify(nowPlaying) ? 1 : 0;   // (la que suena: tras un apagon no se reanuda, vuelve como credito)
            foreach (PlayItem it in queue) if (EsItemSpotify(it)) n++;
            return n;
        }

        // EMPIEZA UNA DE SPOTIFY por la cadena de siempre (como EmpiezaItem), sin aprender nada.
        private ISampleProvider EmpiezaSpotify(PlayItem it)
        {
            string id = it.Song.Path.Substring(RaizSpotify.Length); int barra = id.IndexOf('/'); if (barra > 0) id = id.Substring(0, barra);
            return EmpiezaSpotify(it, id, it.Song, false);
        }
        // 'it' es lo que se ve (la de Spotify, o la tuya si se sustituye: SP2) y 'pista', con lo que se arma la cadena (su ruta de
        // Spotify: su nivelado y lo que se aprende son los de la version de Spotify, SP3).
        private ISampleProvider EmpiezaSpotify(PlayItem it, string id, Track pista, bool sustituida)
        {
            SpotifyEnVivo s = spotify;
            if (s == null || !s.Conectado || cadenaSonando != null) return null;
            PistaSpotify p; lock (spotifyCerrojo) spotifyPistas.TryGetValue(pista.Path, out p);
            string uri = "spotify:track:" + id;
            s.Habla();
            long seq0;
            using (JsonDocument d = s.Toca.Pide(ControlSpotify.Orden("toca", "uri", uri), 5000))
            {
                if (d == null || !SpotifyEnVivo.Bool(d.RootElement, "ok")) { s.Calla(); return null; }
                seq0 = d.RootElement.TryGetProperty("seq", out JsonElement sq) && sq.ValueKind == JsonValueKind.Number ? sq.GetInt64() : s.Seq;
            }
            FuenteSpotify f = new FuenteSpotify(s, this, uri, seq0, p != null ? p.Ms : 0, sustituida ? SustitutaSinSonidoMs : SpotifySinSonidoMs);
            try
            {
                sonoAntes = nowPlaying; nowPlaying = it;
                spotifySustituida = sustituida ? it : null;
                // SP3: se aprende como de las tuyas, pero bajo su ruta de Spotify (sonoridad, huella del EQ, volumen parejo del billar)
                CadenaCancion cad = ArmaCadena(pista, f, false, 0.0);
                if (cad.Sbr != null) cad.Sbr.Active = false;   // (llega completa: sin DSEE)
                SellaInicioReloj(cad);
                cad.Item = it;
                lock (audioDatosLock)
                {
                    curMeasurePath = cad.MedidaRuta; curSumSq = 0; curSamples = 0;
                    curFuenteMedida = cad.Fuente;
                    eqMeasPath = cad.EqRuta; eqMeasCut = 0f;
                    eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0;
                    kwRate = 0;
                    tapMedido = cad.Tap;
                }
                curSbr = cad.Sbr; curTap = cad.Tap; curBass = cad.Bajo;
                bassSongFactor = cad.BajoFactor; bassAutoBase = cad.BajoBase;
                cadenaSonando = cad; cadenaPreparada = null;
                secuencia = new Encadenado(cad.Salida, cad);
                sinEncadenar = null;
                pulso = new MedidorPulso(secuencia);
                return pulso;
            }
            catch (Exception ex)
            {
                LogError("Spotify: no se pudo armar la cadena", ex);
                if (object.ReferenceEquals(nowPlaying, it)) nowPlaying = null;
                spotifySustituida = null;
                TerminaSpotify();
                return null;
            }
        }
        // Al acabar (o saltar) una de Spotify: que el programa calle y lo que quede en el camino se tire.
        private volatile bool spotifyFallida;   // la que sonaba no llego a sonar (FuenteSpotify): se devuelve su credito al cerrarla
        private void TerminaSpotify()
        {
            PlayItem sus = spotifySustituida; spotifySustituida = null;
            if (spotifyFallida)
            {
                spotifyFallida = false;
                // SP2: la tuya que no pudo sonar desde Spotify suena desde tu MP3, enseguida y sin avisar; la de Spotify devuelve su credito
                if (sus != null) { sinSpotify.Add(sus); if (queue.Count == 0 || !object.ReferenceEquals(queue[0], sus)) queue.Insert(0, sus); pideEmpezar = true; }
                else DevuelveCredito("Spotify no pudo tocar esa cancion");
            }
            SpotifyEnVivo s = spotify; if (s == null) return;
            s.Calla();
            // AL MOMENTO, no en segundo plano: si no, la orden de parar podia llegar despues de la de tocar la siguiente y callarla
            using (s.Toca.Pide("{\"op\":\"para\"}", 1500)) { }
        }

        // El nivel de Spotify: el de las no medidas (como los videos), con el margen del tono del local.
        private float GananciaSpotify(Track t)
        {
            float g = 1f, rms;
            if (normalizeOn && SonoridadParaNivelar(t.Path, out rms)) return NormGainFor(t.Path) * MargenDelTono(t);   // SP3: ya medida, al limite como las tuyas
            if (normalizeOn) g = Math.Max(0.5f, Math.Min(4f, normTarget / RmsSpotify));
            float m = MargenDelTono(t);
            return m == 1f ? g : g * m;
        }

        // Lo que dice Ctrl+F3 -> Spotify.
        public string TextoSpotify()
        {
            StringBuilder sb = new StringBuilder();
            if (!SpotifyOn)
            {
                sb.Append("Apagado. Es para las reuniones familiares: Spotify no permite ponerlo para el publico de un negocio.\n");
                sb.Append("Encendido, tus listas y discos de Spotify salen en la letra SPOTIFY y en el buscador, con creditos (hace falta Premium).");
                return sb.ToString();
            }
            if (!File.Exists(SpotifyEnVivo.Binario)) { sb.Append("Este sistema aun no trae Spotify: llega con la siguiente actualizacion del sistema."); return sb.ToString(); }
            SpotifyEnVivo s = spotify;
            if (s == null || !s.Vivo) sb.Append("Abriendo Spotify...\n");
            else if (s.EsperandoCuenta)
                sb.Append("FALTA TU CUENTA (una sola vez): en el celular, en el mismo wifi que la rocola, abre Spotify -> el icono de\n")
                  .Append("dispositivos -> «Rocola». Queda guardada y la rocola entra sola desde entonces.\n");
            else if (s.Conectado)
            {
                int n; lock (spotifyCerrojo) n = Math.Max(0, spotifyPared.Count - 1);
                sb.Append("Dentro como ").Append(s.Usuario).Append(". En la letra SPOTIFY: tus favoritas y ").Append(n).Append(" listas y discos; el buscador busca tambien en Spotify.\n");
            }
            else sb.Append("Entrando a Spotify...\n");
            if (s != null && s.Error.Length > 0 && !s.Conectado) sb.Append("Ultimo aviso: ").Append(s.Error).Append('\n');
            sb.Append("Con creditos, como la rocola. De Spotify solo se mide el volumen. Programa no oficial (librespot): Spotify podria bloquear la cuenta.");
            return sb.ToString();
        }

        // UNA CANCION DE SPOTIFY: lo que llega del programa, sin esperar nunca (la salida no puede esperar; lo que falta, en silencio). Se
        // acaba cuando el programa dice que acabo y ya no queda nada en el anillo, si se callo (SALTAR, apagar) o si en 20 s no llego nada.
        private sealed class FuenteSpotify : IAudioSource
        {
            private const int AtascoMs = 15000;   // sonando, 15 s sin nada (se cayo la red o Spotify): se acaba
            private readonly SpotifyEnVivo s;
            private readonly Form1 f;
            private readonly string uri;
            private readonly long ms, seq0;
            private readonly WaveFormat formato = WaveFormat.CreateIeeeFloatWaveFormat(SpotifyEnVivo.Hz, SpotifyEnVivo.Canales);
            private volatile float volumen = 1f;
            private long cuadros;   // los que de verdad llegaron (los huecos de silencio no cuentan: la posicion de la cancion, para las letras)
            private readonly int desde = Environment.TickCount;
            private int ultimo;
            private bool acabada, llego;
            private readonly int plazo;
            internal FuenteSpotify(SpotifyEnVivo s, Form1 f, string uri, long seq0, long ms, int plazo) { this.s = s; this.f = f; this.uri = uri; this.seq0 = seq0; this.ms = ms; this.plazo = plazo; }
            public WaveFormat WaveFormat { get { return formato; } }
            public int Read(Span<float> d)
            {
                if (acabada) return 0;
                if (s.Callado || !object.ReferenceEquals(f.spotify, s)) { acabada = true; return 0; }
                int n = s.Toma(d);
                int ahora = Environment.TickCount;
                if (n > 0) { llego = true; ultimo = ahora; }
                if (n < d.Length)
                {
                    // acabo (el programa lo dijo DESPUES de pedirla: su numero paso del de entonces) y ya no queda nada: el final de verdad
                    if (s.Seq > seq0 && s.Acabo == uri)
                    {
                        acabada = true;
                        if (!llego) f.spotifyFallida = true;   // (no disponible: ni un cuadro)
                        Aplica(d.Slice(0, n)); Interlocked.Add(ref cuadros, n / SpotifyEnVivo.Canales); return n;
                    }
                    if (!llego && unchecked(ahora - desde) > plazo) { acabada = true; f.spotifyFallida = true; return 0; }
                    if (llego && n == 0 && unchecked(ahora - ultimo) > AtascoMs)
                    {   // se atasco a media cancion: si no llego a la mitad, como si no hubiera sonado (credito, o tu MP3)
                        acabada = true;
                        if (ms <= 0 || Interlocked.Read(ref cuadros) * 1000L / SpotifyEnVivo.Hz < ms / 2) f.spotifyFallida = true;
                        return 0;
                    }
                    d.Slice(n).Clear();   // lo que no llego (la red que tarda), en silencio
                }
                Aplica(d);
                Interlocked.Add(ref cuadros, n / SpotifyEnVivo.Canales);
                return d.Length;
            }
            private void Aplica(Span<float> d)
            {
                float v = volumen;
                if (v != 1f) for (int i = 0; i < d.Length; i++) d[i] *= v;
            }
            public int Read(float[] buffer, int offset, int count) { return Read(buffer.AsSpan(offset, count)); }
            public float Volume { set { volumen = value; } }
            public TimeSpan CurrentTime { get { return TimeSpan.FromSeconds(Interlocked.Read(ref cuadros) / (double)SpotifyEnVivo.Hz); } }
            public TimeSpan TotalTime { get { return TimeSpan.FromMilliseconds(ms); } }
            public void Salta(TimeSpan donde) { }
            public void Dispose() { }
        }
    }
}

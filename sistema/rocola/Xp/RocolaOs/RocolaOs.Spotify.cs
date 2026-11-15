// SPOTIFY EN LA ROCOLA (fase 11, grupo spotify-xp): EL NUCLEO, lo que no se ve. Documento: FASE11_spotify-xp.md.
//
// Decision del dueno del 14-11-2026: «Lo de Spotify sera todo en la rocola, nada en el celular». Spotify Soloist (el cliente OFICIAL de
// Spotify para Linux sin pantalla) corre como la bocina de Spotify Connect «Rocola», abierto por su vigilante (rocola-soloist, guion del
// sistema, de otro grupo); su sonido llega por NUESTRO libpulse.so.0 (rocola-pulso) a un anillo de memoria (RocolaOs.SpotifyAnillo.cs) que
// la rocola lee como una cancion mas (RocolaOs.SpotifyFuente.cs); y se le manda por su API local de WebSocket (aqui). Lo que se pide se
// cobra y lleva su turno por el MISMO camino de XP (RocolaOs.SpotifyPide.cs). Nada de esto descifra audio ni imita al cliente oficial: es el
// cliente oficial, y lo nuestro es lo de alrededor.
//
// LOS HILOS (todos de Rocola OS: RocolaOs.Hilo, que lo atrapa todo):
//   «spotify»            el que manda: conecta el WebSocket (lee ws.port de la carpeta de datos de Soloist), atiende los eventos, decide las
//                        reglas (R2-R4: activar, pausar lo no pedido, dar por acabada una cancion), late el anillo y escribe el estado y la
//                        bitacora. Despierta cada 50 ms.
//   «spotify-ws»         solo recibe del WebSocket (un hilo por conexion; lo que llega va a una cola para el que manda)
//   «spotify-vigilante»  abre /usr/bin/rocola-soloist corre (I4) y lo vuelve a abrir si se cae, con esperas crecientes
//   «spotify-portadas»   baja las portadas a /run (RocolaOs.SpotifyPide.cs)
// El hilo del sonido (la fuente) y el de la pantalla (el latido, el pedir) solo leen la copia del estado (Estado) y dejan marcas en la
// peticion de su cancion (RocolaOsSpotifyPeticion): nunca esperan a estos hilos.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    // LO QUE SOLOIST DICE QUE PASA: una copia que se cambia entera (quien la lee no necesita cerrojo)
    internal sealed class RocolaOsSpotifyEstado
    {
        internal bool Ws;             // el WebSocket conectado
        internal long WsCambioMs;     // cuando se conecto o se cayo (Environment.TickCount64)
        internal bool Sesion;         // logged_in
        internal bool Activa;         // is_active (la bocina activa de Spotify Connect)
        internal string Dispositivo = "";
        internal string Status = "";  // idle, playing, paused, buffering
        internal string ItemUri = "", ItemNombre = "", ItemArtista = "", ItemDisco = "", ItemPortada = "";
        internal int ItemDuracionMs;
        internal string ContextoUri = "";
        internal int Volumen = -1;
        internal bool Shuffle;
        internal string Repeat = "";
        internal RocolaOsSpotifyEstado Copia() { return (RocolaOsSpotifyEstado)MemberwiseClone(); }
    }

    // UNA CANCION DE SPOTIFY QUE SUENA (o va a sonar) EN LA ROCOLA: la crea su fuente al empezar a leerse (hilo del sonido); el hilo de Spotify
    // la atiende (manda play, ve si empezo, marca donde acaba). Los campos que cambian, con Volatile/Interlocked.
    internal sealed class RocolaOsSpotifyPeticion
    {
        internal const int Nueva = 0, Mandada = 1, Suena = 2, Acabada = 3;
        internal const int SinMotivo = 0, Termino = 1, OtraCancion = 2, Parada = 3, Inactiva = 4, SinSesion = 5, WsCaido = 6, SinSonido = 7,
            NoEmpezo = 8, Cortada = 9, Cierre = 10;
        internal static readonly string[] Motivos = { "", "termino", "Soloist paso a otra", "se paro", "dejo de ser la bocina activa",
            "perdio la sesion", "el WebSocket se cayo mas de 10 s", "el anillo sin sonido mas de 5 s", "no empezo en 15 s", "la rocola la corto", "se cerro" };
        private static long ultimoId;
        internal readonly long Id = Interlocked.Increment(ref ultimoId);
        internal readonly string Uri;
        internal readonly string Titulo;
        internal readonly long CreadaMs = Environment.TickCount64;
        internal int Fase;                         // Nueva, Mandada, Suena, Acabada (Volatile)
        internal long EscritoAlPedir = -1;         // el «escrito» del anillo justo antes de mandar play (lo de antes no es de esta)
        internal long Frontera = long.MaxValue;    // el cuadro del anillo donde acaba (lo pone el hilo de Spotify al ver que Soloist paso a otra)
        internal int Motivo;                       // por que el hilo de Spotify la dio por acabada (Motivos)
        internal int DuracionMs;                   // la de Soloist al empezar (o la del catalogo)
        internal long MandadaMs;                   // cuando se mando play
        // lo que dice la fuente al acabar
        internal int FinMotivo;                    // el motivo con que acabo de verdad
        internal bool Devolver;                    // se devuelve el credito (R4)
        internal double SegundosSonados;
        internal bool Rematada;                    // (el hilo de Spotify ya la remato: pausa, credito, bitacora)
        internal bool Reintentada;                 // (play se mando otra vez a los 4 s)
        internal long UltimoEscrito = -1, UltimoEscritoMs;   // (el hilo de Spotify: cuando crecio el anillo por ultima vez sonando esta)
        internal RocolaOsSpotifyPeticion(string uri, string titulo, int duracionMs) { Uri = uri; Titulo = titulo ?? ""; DuracionMs = duracionMs; }
    }

    internal static partial class RocolaOsSpotify
    {
        internal const string Nombre = "spotify";
        internal const string ClaveNombre = "spotify_nombre";

        // ---------- lo que se ve desde cualquier hilo ----------
        private static RocolaOsSpotifyEstado estado = new RocolaOsSpotifyEstado();
        internal static RocolaOsSpotifyEstado Estado { get { return Volatile.Read(ref estado); } }
        // SOLOIST CON SESION: el WebSocket conectado y la cuenta dentro (I6: RocolaOsSpotifyListo)
        internal static bool Listo { get { RocolaOsSpotifyEstado e = Estado; return Encendido && e.Ws && e.Sesion; } }
        internal static bool Encendido { get { return Volatile.Read(ref encendido) != 0; } }
        private static int encendido;
        internal static RocolaOsSpotifyAnillo Anillo;   // (se crea al arrancar)
        private static RocolaOsSpotifyPeticion actual;  // la ultima que pidio una fuente
        internal static RocolaOsSpotifyPeticion Actual { get { return Volatile.Read(ref actual); } }
        internal static int LectoresAbiertos;          // fuentes de Spotify leyendo (Interlocked)
        internal static int LatenciaUs = 1000000;      // lo que tarda en oirse lo que se lee (lo pone el latido: RetrasoAudible de XP)
        internal static int EnCola;                    // de Spotify en la cola de XP (lo cuenta el latido)
        internal static long Cobradas, Devueltas, SinCredito, NoPedidas, Sonadas;
        internal static string UltimoError = "";
        internal static string Ws0 = "";               // (pruebas) el ws.addr:ws.port con que se conecto

        // LA PIDE UNA FUENTE (hilo del sonido): solo marca; el hilo de Spotify la atiende en menos de 50 ms
        internal static void Pide(RocolaOsSpotifyPeticion p) { Volatile.Write(ref actual, p); }

        // ---------- las rutas ----------
        internal static string CarpetaSoloist
        {
            get
            {
                string v = null;
                try { v = Environment.GetEnvironmentVariable("ROCOLA_SOLOIST_DATOS"); } catch { }
                return string.IsNullOrWhiteSpace(v) ? RocolaOs.Datos("soloist") : v.Trim();
            }
        }
        internal static string RunSpotify(string nombre) { return RocolaOs.Run(Path.Combine("rocola-xp/spotify", nombre)); }
        internal static string Vigilante
        {
            get
            {
                string v = null;
                try { v = Environment.GetEnvironmentVariable("ROCOLA_SOLOIST_BIN"); } catch { }
                return string.IsNullOrWhiteSpace(v) ? "/usr/bin/rocola-soloist" : v.Trim();
            }
        }

        // ---------- la bitacora (rocola_spotify.txt: lo cobrado, lo devuelto, los turnos, los errores; se rota a .1 al pasar de 1 MB) ----------
        private static readonly ConcurrentQueue<string> porApuntar = new ConcurrentQueue<string>();
        internal const long TopeBitacora = 1048576;
        // desde cualquier hilo (no espera al disco: lo escribe el hilo de Spotify, o el cierre). NUNCA llaves.
        internal static void Apunta(string que)
        {
            if (!Encendido) return;
            porApuntar.Enqueue(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + (que ?? "").Replace('\n', ' ').Replace('\r', ' ') + "\n");
        }
        private static void VaciaBitacora()
        {
            if (porApuntar.IsEmpty) return;
            StringBuilder sb = new StringBuilder();
            string l;
            while (porApuntar.TryDequeue(out l)) sb.Append(l);
            try
            {
                string ruta = RocolaOs.Datos("rocola_spotify.txt");
                FileInfo fi = new FileInfo(ruta);
                if (fi.Exists && fi.Length > TopeBitacora) File.Move(ruta, ruta + ".1", true);
                File.AppendAllText(ruta, sb.ToString());
            }
            catch { }
        }

        // ---------- arrancar y parar (lo llama la funcion «spotify»: RocolaOs.SpotifyPide.cs) ----------
        private static volatile bool parar;
        private static Thread hiloControl, hiloVigilante;
        private static readonly AutoResetEvent despierta = new AutoResetEvent(false);
        internal static void ArrancaHilos()
        {
            Volatile.Write(ref encendido, 1);
            Anillo = new RocolaOsSpotifyAnillo(RocolaOsSpotifyAnillo.Ruta);
            Apunta("modo Spotify encendido: bocina «" + NombreBocina + "», datos de Soloist en " + CarpetaSoloist + ", anillo en " + Anillo.RutaAbierta);
            hiloControl = RocolaOs.Hilo("spotify", ThreadPriority.BelowNormal, Bucle);
            hiloVigilante = RocolaOs.Hilo("spotify-vigilante", Vigila);
        }
        internal static string NombreBocina
        {
            get { string n = RocolaOs.Ajuste(ClaveNombre); return string.IsNullOrWhiteSpace(n) ? "Rocola" : n.Trim(); }
        }
        // AL CERRARSE LA ROCOLA (ParaAlCerrar, en su hilo, con el plazo comun de 4 s): pausa lo que suene en Soloist, cierra el WebSocket y el
        // vigilante (SIGTERM; si no sale, SIGKILL a los 10 s, de fondo). Devuelve lo que tardo.
        internal static void Para(string razon)
        {
            if (!Encendido) return;
            long t0 = Stopwatch.GetTimestamp();
            parar = true;
            despierta.Set();
            try { hiloControl?.Join(1200); } catch { }
            double ms1 = RocolaOs.Ms(t0);
            ParaVigilante(2500);
            Apunta("se cierra (" + razon + "): el hilo de Spotify en " + ms1.ToString("0", CultureInfo.InvariantCulture) + " ms, en total "
                + RocolaOs.Ms(t0).ToString("0", CultureInfo.InvariantCulture) + " ms");
            VaciaBitacora();
        }

        // ================================================================== EL HILO QUE MANDA
        private static ClientWebSocket ws;
        private static int wsGen;
        private static readonly ConcurrentQueue<Evento> eventos = new ConcurrentQueue<Evento>();
        private static string puertoVisto;        // «addr:port» de la conexion actual
        private static long proximoIntentoMs, ultimoMiraPuertoMs, ultimoAnilloMs, ultimoLatidoMs, ultimoEstadoMs, ultimaActivacionMs,
            ultimaPausaNoPedidaMs, ultimoArregloOpcionesMs, ultimaPausaNuestraMs;
        private static int esperaConexionMs = 500;
        private static bool sesionAntes, presenteAntes;
        private static string estadoEscrito;
        private static readonly List<RocolaOsSpotifyPeticion> vivas = new List<RocolaOsSpotifyPeticion>();
        private static bool pausaNuestra;          // la ultima pausa la mandamos nosotros (no cuenta como «se paro»)

        private static void Bucle()
        {
            RocolaOsSpotifyAnillo a = Anillo;
            if (!a.Abre()) Apunta("el anillo no se pudo abrir: " + a.UltimoError + " (se reintenta cada segundo)");
            while (!parar)
            {
                long ahora = Environment.TickCount64;
                try
                {
                    Conexion(ahora);
                    Evento e;
                    while (eventos.TryDequeue(out e)) Atiende(e, ahora);
                    Peticiones(ahora);
                    NoPedido(ahora);
                    if (ahora - ultimoAnilloMs >= 1000)
                    {
                        ultimoAnilloMs = ahora;
                        if (a.Abierto) a.Mira(); else a.Abre();
                    }
                    bool presente = Volatile.Read(ref LectoresAbiertos) > 0;
                    if (presente && ahora - ultimoLatidoMs >= 250) { ultimoLatidoMs = ahora; a.Late(true, LatenciaUs); }
                    else if (!presente && presenteAntes) a.Late(false, LatenciaUs);
                    presenteAntes = presente;
                    if (ahora - ultimoEstadoMs >= 1000) { ultimoEstadoMs = ahora; EscribeEstado(); VaciaBitacora(); }
                }
                catch (Exception ex) { RocolaOs.Falla(Nombre, "hilo de Spotify", ex); UltimoError = ex.Message; }
                despierta.WaitOne(50);
            }
            // al parar: lo que suene en Soloist se pausa (que no siga tocando para nadie) y el WebSocket se cierra
            try
            {
                if (ws != null && ws.State == WebSocketState.Open && (Estado.Status == "playing" || Estado.Status == "buffering")) Manda("{\"type\":\"command\",\"command\":\"pause\"}");
                foreach (RocolaOsSpotifyPeticion p in vivas) if (!p.Rematada && Volatile.Read(ref p.Fase) != RocolaOsSpotifyPeticion.Acabada) Volatile.Write(ref p.Motivo, RocolaOsSpotifyPeticion.Cierre);
            }
            catch { }
            CierraWs("la rocola se cierra");
            try { a.Late(false, 0); a.SueltaTodo(); } catch { }
            EscribeEstado();
        }

        // ---------- la conexion (I2) ----------
        private static string LeePuerto()
        {
            try
            {
                string d = CarpetaSoloist;
                string p = File.ReadAllText(Path.Combine(d, "ws.port")).Trim();
                string ad = "127.0.0.1";
                try { string x = File.ReadAllText(Path.Combine(d, "ws.addr")).Trim(); if (x.Length > 0) ad = x; } catch { }
                int n;
                if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n <= 0 || n > 65535) return null;
                // solo en esta maquina (la API no tiene ninguna proteccion: Soloist la abre en 127.0.0.1)
                if (ad == "0.0.0.0" || ad == "::" || ad == "localhost") ad = "127.0.0.1";
                return ad + ":" + n.ToString(CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }
        private static void Conexion(long ahora)
        {
            bool abierta = ws != null && ws.State == WebSocketState.Open;
            if (abierta)
            {
                if (ahora - ultimoMiraPuertoMs >= 1000)
                {   // ¿Soloist se reinicio? (otro ws.port): se reconecta
                    ultimoMiraPuertoMs = ahora;
                    string p = LeePuerto();
                    if (p != null && p != puertoVisto) { CierraWs("cambio ws.port (" + puertoVisto + " -> " + p + ")"); proximoIntentoMs = ahora; }
                }
                return;
            }
            if (ws != null) CierraWs("la conexion se cerro");
            if (ahora < proximoIntentoMs) return;
            string puerto = LeePuerto();
            if (puerto == null) { proximoIntentoMs = ahora + 1000; return; }
            ClientWebSocket w = new ClientWebSocket();
            try
            {
                w.Options.Proxy = null;   // (127.0.0.1: nunca por un proxy)
                w.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                using (CancellationTokenSource cts = new CancellationTokenSource(3000))
                    w.ConnectAsync(new Uri("ws://" + puerto), cts.Token).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                try { w.Dispose(); } catch { }
                UltimoError = "no conecta con Soloist (" + puerto + "): " + ex.GetType().Name;
                proximoIntentoMs = ahora + esperaConexionMs;
                esperaConexionMs = Math.Min(5000, esperaConexionMs * 2);
                return;
            }
            esperaConexionMs = 500;
            ws = w; puertoVisto = puerto; Ws0 = puerto;
            int gen = ++wsGen;
            CambiaEstado(e => { e.Ws = true; e.WsCambioMs = Environment.TickCount64; });
            Apunta("conectado con Soloist (ws://" + puerto + ")");
            RocolaOs.Hilo("spotify-ws", ThreadPriority.BelowNormal, delegate { Recibe(w, gen); });
            Manda("{\"type\":\"command\",\"command\":\"get_auth_state\"}");
        }
        private static void CierraWs(string por)
        {
            ClientWebSocket w = ws; ws = null; puertoVisto = null;
            if (w == null) return;
            try
            {
                if (w.State == WebSocketState.Open)
                    using (CancellationTokenSource cts = new CancellationTokenSource(300))
                        w.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token).GetAwaiter().GetResult();
            }
            catch { }
            try { w.Abort(); w.Dispose(); } catch { }
            bool estaba = Estado.Ws;
            CambiaEstado(e => { e.Ws = false; e.WsCambioMs = Environment.TickCount64; e.Sesion = false; e.Status = ""; });
            sesionAntes = false;
            if (estaba) Apunta("desconectado de Soloist: " + por);
        }
        // MANDA una orden (solo el hilo que manda; ClientWebSocket admite un envio a la vez)
        private static bool Manda(string json)
        {
            ClientWebSocket w = ws;
            if (w == null || w.State != WebSocketState.Open) return false;
            try
            {
                byte[] b = Encoding.UTF8.GetBytes(json);
                using (CancellationTokenSource cts = new CancellationTokenSource(2000))
                    w.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, cts.Token).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex) { UltimoError = "no se pudo mandar a Soloist: " + ex.GetType().Name; CierraWs("fallo al mandar"); return false; }
        }
        private static string Orden(string comando) { return "{\"type\":\"command\",\"command\":\"" + comando + "\"}"; }
        private static string OrdenSiNo(string comando, bool v) { return "{\"type\":\"command\",\"command\":\"" + comando + "\",\"enabled\":" + (v ? "true" : "false") + "}"; }
        internal static string Json(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        // ---------- lo que llega (su propio hilo: solo recibe y lo pone en la cola) ----------
        private sealed class Evento
        {
            internal string Tipo = "";
            internal int Gen;
            internal bool? LoggedIn, IsActive, Shuffle;
            internal string DeviceName, Status, Repeat, Mensaje, Comando, ContextoUri;
            internal int Volumen = -1;
            internal Entidad Item;
            internal int EnColaSpotify = -1;   // queue_changed: cuantas vienen (para la bitacora)
            internal string FuentesCola;
        }
        private sealed class Entidad { internal string Uri = "", Tipo = "", Nombre = "", Artista = "", Disco = "", Portada = ""; internal int DuracionMs; }
        private static void Recibe(ClientWebSocket w, int gen)
        {
            byte[] buf = new byte[16384];
            MemoryStream msg = new MemoryStream();
            try
            {
                while (true)
                {
                    WebSocketReceiveResult r = w.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None).GetAwaiter().GetResult();
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    if (msg.Length + r.Count > 4 * 1024 * 1024) { msg.SetLength(0); continue; }   // (un mensaje enorme: se tira)
                    msg.Write(buf, 0, r.Count);
                    if (!r.EndOfMessage) continue;
                    if (r.MessageType == WebSocketMessageType.Text)
                    {
                        Evento e = Lee(msg.GetBuffer(), (int)msg.Length);
                        if (e != null) { e.Gen = gen; eventos.Enqueue(e); despierta.Set(); }
                    }
                    msg.SetLength(0);
                }
            }
            catch { }
            eventos.Enqueue(new Evento { Tipo = "_cerrado", Gen = gen });
            despierta.Set();
        }
        private static Evento Lee(byte[] b, int n)
        {
            try
            {
                using (JsonDocument d = JsonDocument.Parse(new ReadOnlyMemory<byte>(b, 0, n)))
                {
                    JsonElement r = d.RootElement;
                    if (r.ValueKind != JsonValueKind.Object) return null;
                    Evento e = new Evento { Tipo = Texto(r, "type") };
                    e.LoggedIn = SiNo(r, "logged_in"); e.IsActive = SiNo(r, "is_active");
                    e.DeviceName = TextoONull(r, "device_name"); e.Status = TextoONull(r, "status"); e.Mensaje = TextoONull(r, "message");
                    e.Comando = TextoONull(r, "command");
                    JsonElement v;
                    if (r.TryGetProperty("volume", out v) && v.ValueKind == JsonValueKind.Number) e.Volumen = (int)v.GetDouble();
                    if (r.TryGetProperty("item", out v) && v.ValueKind == JsonValueKind.Object) e.Item = LeeEntidad(v);
                    if (r.TryGetProperty("context", out v) && v.ValueKind == JsonValueKind.Object) e.ContextoUri = Texto(v, "uri");
                    if (r.TryGetProperty("options", out v) && v.ValueKind == JsonValueKind.Object) { e.Shuffle = SiNo(v, "shuffle"); e.Repeat = TextoONull(v, "repeat"); }
                    if (e.Tipo == "queue_changed" && r.TryGetProperty("upcoming", out v) && v.ValueKind == JsonValueKind.Array)
                    {
                        e.EnColaSpotify = v.GetArrayLength();
                        StringBuilder f = new StringBuilder();
                        int i = 0;
                        foreach (JsonElement x in v.EnumerateArray()) { if (i++ >= 3) break; f.Append(f.Length > 0 ? "," : "").Append(Texto(x, "source")); }
                        e.FuentesCola = f.ToString();
                    }
                    return e;
                }
            }
            catch { return null; }
        }
        private static Entidad LeeEntidad(JsonElement v)
        {
            Entidad x = new Entidad { Uri = Texto(v, "uri"), Tipo = Texto(v, "entity_type") };
            JsonElement dec, t, u;
            if (v.TryGetProperty("decorations", out dec) && dec.ValueKind == JsonValueKind.Object)
            {
                if (dec.TryGetProperty("identity", out t) && t.ValueKind == JsonValueKind.Object) x.Nombre = Texto(t, "name");
                if (dec.TryGetProperty("playback", out t) && t.ValueKind == JsonValueKind.Object && t.TryGetProperty("duration_ms", out u) && u.ValueKind == JsonValueKind.Number)
                    x.DuracionMs = (int)Math.Min(int.MaxValue, Math.Max(0, u.GetDouble()));
                if (dec.TryGetProperty("parent", out t) && t.ValueKind == JsonValueKind.Object && t.TryGetProperty("entity", out u) && u.ValueKind == JsonValueKind.Object)
                {
                    JsonElement d2, i2;
                    if (u.TryGetProperty("decorations", out d2) && d2.ValueKind == JsonValueKind.Object && d2.TryGetProperty("identity", out i2) && i2.ValueKind == JsonValueKind.Object) x.Disco = Texto(i2, "name");
                }
                if (dec.TryGetProperty("creators", out t) && t.ValueKind == JsonValueKind.Array)
                    foreach (JsonElement c in t.EnumerateArray())
                    {
                        JsonElement en, d2, i2;
                        if (c.ValueKind == JsonValueKind.Object && c.TryGetProperty("entity", out en) && en.ValueKind == JsonValueKind.Object && en.TryGetProperty("decorations", out d2)
                            && d2.ValueKind == JsonValueKind.Object && d2.TryGetProperty("identity", out i2) && i2.ValueKind == JsonValueKind.Object) { x.Artista = Texto(i2, "name"); break; }
                    }
                if (dec.TryGetProperty("visual_identity", out t) && t.ValueKind == JsonValueKind.Object && t.TryGetProperty("cover", out u) && u.ValueKind == JsonValueKind.Array)
                {
                    string mejor = "", cualquiera = "";
                    foreach (JsonElement c in u.EnumerateArray())
                    {
                        string url = Texto(c, "url"), tam = Texto(c, "size");
                        if (url.Length == 0) continue;
                        if (cualquiera.Length == 0) cualquiera = url;
                        if (tam == "large" || (tam == "default" && mejor.Length == 0)) mejor = url;
                    }
                    x.Portada = mejor.Length > 0 ? mejor : cualquiera;
                }
            }
            return x;
        }
        internal static string Texto(JsonElement e, string k) { JsonElement v; return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : ""; }
        private static string TextoONull(JsonElement e, string k) { JsonElement v; return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : null; }
        private static bool? SiNo(JsonElement e, string k)
        {
            JsonElement v;
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out v)) return null;
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            return null;
        }

        private static readonly object cerrojoEstado = new object();
        private static void CambiaEstado(Action<RocolaOsSpotifyEstado> que)
        {
            lock (cerrojoEstado) { RocolaOsSpotifyEstado e = Estado.Copia(); que(e); Volatile.Write(ref estado, e); }
        }

        // ---------- lo que se hace con cada evento ----------
        private static void Atiende(Evento e, long ahora)
        {
            if (e.Gen != wsGen) return;   // (de una conexion vieja)
            switch (e.Tipo)
            {
                case "_cerrado":
                    CierraWs("Soloist cerro la conexion");
                    proximoIntentoMs = ahora + 500;
                    return;
                case "auth_state":
                    CambiaEstado(s => { if (e.LoggedIn.HasValue) s.Sesion = e.LoggedIn.Value; if (e.IsActive.HasValue) s.Activa = e.IsActive.Value; if (e.DeviceName != null) s.Dispositivo = e.DeviceName; });
                    break;
                case "playback_state":
                    CambiaEstado(s =>
                    {
                        if (e.Status != null) s.Status = e.Status;
                        if (e.IsActive.HasValue) s.Activa = e.IsActive.Value;
                        if (e.Volumen >= 0) s.Volumen = e.Volumen;
                        if (e.Shuffle.HasValue) s.Shuffle = e.Shuffle.Value;
                        if (e.Repeat != null) s.Repeat = e.Repeat;
                        if (e.ContextoUri != null) s.ContextoUri = e.ContextoUri;
                        PonItem(s, e.Item);
                    });
                    break;
                case "track_changed":
                    CambiaEstado(s => PonItem(s, e.Item));
                    break;
                case "playback_changed":
                    CambiaEstado(s => { if (e.Status != null) s.Status = e.Status; });
                    if (e.Status == "playing") pausaNuestra = false;
                    break;
                case "volume_changed":
                    CambiaEstado(s => { if (e.Volumen >= 0) s.Volumen = e.Volumen; });
                    break;
                case "device_changed":
                    CambiaEstado(s => { if (e.IsActive.HasValue) s.Activa = e.IsActive.Value; if (e.DeviceName != null) s.Dispositivo = e.DeviceName; });
                    break;
                case "context_changed":
                    CambiaEstado(s => { if (e.ContextoUri != null) s.ContextoUri = e.ContextoUri; });
                    break;
                case "options_changed":
                    CambiaEstado(s => { if (e.Shuffle.HasValue) s.Shuffle = e.Shuffle.Value; if (e.Repeat != null) s.Repeat = e.Repeat; });
                    break;
                case "queue_changed":
                    if (e.EnColaSpotify > 0) Apunta("la cola de Spotify trae " + e.EnColaSpotify.ToString(CultureInfo.InvariantCulture) + " (" + e.FuentesCola + "): no suena nada de ella sin pedirlo en la rocola");
                    return;
                case "error":
                    UltimoError = "Soloist: " + (e.Mensaje ?? "");
                    Apunta("Soloist contesto un error: " + (e.Mensaje ?? ""));
                    return;
                default:
                    return;   // (command_result y lo que no se conoce)
            }
            RocolaOsSpotifyEstado st = Estado;
            // LA SESION (R2): al entrar, shuffle y repeat apagados y, si no es la bocina activa, activate
            if (st.Sesion && !sesionAntes)
            {
                Apunta("sesion de Spotify dentro (bocina «" + st.Dispositivo + "»" + (st.Activa ? ", activa" : "") + ")");
                ArreglaOpciones(ahora, true);
                if (!st.Activa) Activa(ahora, "al entrar");
            }
            else if (!st.Sesion && sesionAntes) Apunta("Soloist sin sesion (falta emparejar la bocina desde la app de Spotify, o se cerro la sesion)");
            sesionAntes = st.Sesion;
            if (st.Sesion && (st.Shuffle || (st.Repeat.Length > 0 && st.Repeat != "off"))) ArreglaOpciones(ahora, false);
            if (st.Sesion && !st.Activa && (e.Tipo == "device_changed" || e.Tipo == "playback_state" || e.Tipo == "auth_state")) Activa(ahora, "dejo de ser la bocina activa");
            Evalua(ahora);
        }
        private static void PonItem(RocolaOsSpotifyEstado s, Entidad x)
        {
            if (x == null) return;
            s.ItemUri = x.Uri; s.ItemNombre = x.Nombre; s.ItemArtista = x.Artista; s.ItemDisco = x.Disco; s.ItemPortada = x.Portada; s.ItemDuracionMs = x.DuracionMs;
        }
        // shuffle y los dos repeat apagados (R3; el orden de la documentacion de Soloist para «off»: primero track, luego context)
        private static void ArreglaOpciones(long ahora, bool siempre)
        {
            if (!siempre && ahora - ultimoArregloOpcionesMs < 3000) return;
            ultimoArregloOpcionesMs = ahora;
            Manda(OrdenSiNo("set_shuffle", false));
            Manda(OrdenSiNo("set_repeat_track", false));
            Manda(OrdenSiNo("set_repeat_context", false));
        }
        // UNA PAUSA QUE MANDA LA ROCOLA (al acabar una suya): no cuenta como «se paro», y mientras llega (2 s) lo que siga sonando no es «no pedido»
        private static void PausaNuestra()
        {
            pausaNuestra = true; ultimaPausaNuestraMs = Environment.TickCount64;
            Manda(Orden("pause"));
        }
        private static void Activa(long ahora, string por)
        {
            if (ahora - ultimaActivacionMs < 5000) return;
            ultimaActivacionMs = ahora;
            Apunta("activate (" + por + ")");
            Manda(Orden("activate"));
        }

        // ---------- las reglas de la cancion que suena (R4) ----------
        private static void Evalua(long ahora)
        {
            RocolaOsSpotifyEstado st = Estado;
            foreach (RocolaOsSpotifyPeticion p in vivas)
            {
                int f = Volatile.Read(ref p.Fase);
                if (f == RocolaOsSpotifyPeticion.Mandada && st.ItemUri == p.Uri && (st.Status == "playing" || st.Status == "buffering"))
                {
                    p.UltimoEscrito = -1; p.UltimoEscritoMs = ahora;
                    Volatile.Write(ref p.Fase, RocolaOsSpotifyPeticion.Suena);
                    if (st.ItemDuracionMs > 0) Volatile.Write(ref p.DuracionMs, st.ItemDuracionMs);
                    Apunta("suena " + p.Uri + " (" + p.Titulo + ")");
                    f = RocolaOsSpotifyPeticion.Suena;
                }
                if (f != RocolaOsSpotifyPeticion.Suena || Volatile.Read(ref p.Motivo) != 0) continue;
                if (!st.Sesion) Termina(p, RocolaOsSpotifyPeticion.SinSesion);
                else if (!st.Activa) Termina(p, RocolaOsSpotifyPeticion.Inactiva);
                else if (st.ItemUri != p.Uri) Termina(p, RocolaOsSpotifyPeticion.OtraCancion);
                else if ((st.Status == "paused" || st.Status == "idle") && !pausaNuestra) Termina(p, RocolaOsSpotifyPeticion.Parada);
            }
        }
        // LA DA POR ACABADA: donde va ahora el anillo es su frontera (la fuente lee hasta ahi y acaba); lo que siga sonando en Soloist, en pausa
        private static void Termina(RocolaOsSpotifyPeticion p, int motivo)
        {
            if (Interlocked.CompareExchange(ref p.Motivo, motivo, 0) != 0) return;
            long e = Anillo.Escrito;
            long f;
            do { f = Interlocked.Read(ref p.Frontera); if (e >= f) break; } while (Interlocked.CompareExchange(ref p.Frontera, e, f) != f);
            Apunta("acaba " + p.Uri + ": " + RocolaOsSpotifyPeticion.Motivos[motivo]);
            string s = Estado.Status;
            if (motivo == RocolaOsSpotifyPeticion.OtraCancion && (s == "playing" || s == "buffering"))
            {   // lo que empezo en su lugar no lo pidio la rocola (un celular, autoplay, la cola de Spotify): la pausa de abajo lo calla
                Interlocked.Increment(ref NoPedidas);
                Apunta("NO PEDIDO: Soloist paso a «" + Estado.ItemUri + "» (" + Estado.ItemNombre + ") sin que la rocola lo pidiera: pausa, no suena ni se cobra");
            }
            if (motivo != RocolaOsSpotifyPeticion.SinSesion && (s == "playing" || s == "buffering")) { PausaNuestra(); }
        }
        private static void Peticiones(long ahora)
        {
            RocolaOsSpotifyPeticion n = Actual;
            if (n != null && !vivas.Contains(n) && !n.Rematada) vivas.Add(n);
            RocolaOsSpotifyEstado st = Estado;
            for (int i = 0; i < vivas.Count; i++)
            {
                RocolaOsSpotifyPeticion p = vivas[i];
                int f = Volatile.Read(ref p.Fase);
                if (f == RocolaOsSpotifyPeticion.Acabada) { Remata(p); vivas.RemoveAt(i); i--; continue; }
                if (f == RocolaOsSpotifyPeticion.Nueva && ReferenceEquals(p, Actual) && st.Ws && st.Sesion)
                {
                    // PLAY (R3): la bocina activa, shuffle y repeat apagados, y la que pidio la rocola
                    if (!st.Activa) { ultimaActivacionMs = 0; Activa(ahora, "para tocar"); }
                    ArreglaOpciones(ahora, true);
                    Interlocked.Exchange(ref p.EscritoAlPedir, Anillo.Escrito);
                    pausaNuestra = false;
                    if (Manda("{\"type\":\"command\",\"command\":\"play\",\"uri\":" + Json(p.Uri) + "}"))
                    {
                        Volatile.Write(ref p.MandadaMs, ahora);
                        Volatile.Write(ref p.Fase, RocolaOsSpotifyPeticion.Mandada);
                        Apunta("play " + p.Uri + " (" + p.Titulo + ")");
                        Evalua(ahora);
                    }
                }
                else if (f == RocolaOsSpotifyPeticion.Mandada && !p.Reintentada && ahora - Volatile.Read(ref p.MandadaMs) > 4000 && st.Ws && st.Sesion)
                {   // a los 4 s Soloist aun no la toca: se le pide otra vez (una vez)
                    p.Reintentada = true;
                    Apunta("play otra vez " + p.Uri + " (a los 4 s seguia «" + st.ItemUri + "» " + st.Status + ")");
                    Interlocked.Exchange(ref p.EscritoAlPedir, Anillo.Escrito);
                    Manda("{\"type\":\"command\",\"command\":\"play\",\"uri\":" + Json(p.Uri) + "}");
                }
                else if (f == RocolaOsSpotifyPeticion.Suena && Volatile.Read(ref p.Motivo) == 0)
                {
                    // el WebSocket caido mas de 10 s, o el anillo sin sonido mas de 5 s estando en «playing» (R4)
                    if (!st.Ws && ahora - st.WsCambioMs > 10000) Termina(p, RocolaOsSpotifyPeticion.WsCaido);
                    else if (st.Status == "playing")
                    {
                        long e = Anillo.Escrito;
                        if (e != p.UltimoEscrito) { p.UltimoEscrito = e; p.UltimoEscritoMs = ahora; }
                        else if (ahora - p.UltimoEscritoMs > 5000) Termina(p, RocolaOsSpotifyPeticion.SinSonido);
                    }
                    else p.UltimoEscritoMs = ahora;
                }
            }
        }
        // LA REMATA (la fuente ya acabo): que Soloist no siga solo, el credito si toca, la bitacora
        private static void Remata(RocolaOsSpotifyPeticion p)
        {
            if (p.Rematada) return;
            p.Rematada = true;
            if (Volatile.Read(ref p.Motivo) == 0) Volatile.Write(ref p.Motivo, p.FinMotivo);
            string s = Estado.Status;
            if (ReferenceEquals(p, Actual) && (s == "playing" || s == "buffering")) { PausaNuestra(); }
            string seg = p.SegundosSonados.ToString("0.0", CultureInfo.InvariantCulture);
            if (p.Devolver)
            {
                Interlocked.Increment(ref Devueltas);
                Apunta("DEVUELTA " + p.Uri + " (" + p.Titulo + "): " + RocolaOsSpotifyPeticion.Motivos[p.FinMotivo] + ", sono " + seg + " s: se devuelve el credito");
                RocolaOs.Encarga("spotify-devuelve", delegate { Form1 r = RocolaOs.LaRocola; if (r != null) r.RocolaOsSpotifyDevuelve(); });
            }
            else
            {
                if (p.SegundosSonados > 0) Interlocked.Increment(ref Sonadas);
                Apunta("acabo " + p.Uri + " (" + p.Titulo + "): " + RocolaOsSpotifyPeticion.Motivos[p.FinMotivo] + ", sono " + seg + " s");
            }
        }
        // LO QUE LA ROCOLA NO PIDIO NO SUENA (R3): desde un celular, autoplay, radio o la cola de Spotify: pausa al momento, sin cobrar
        private static void NoPedido(long ahora)
        {
            RocolaOsSpotifyEstado st = Estado;
            if (!st.Ws || !st.Sesion || !(st.Status == "playing" || st.Status == "buffering")) return;
            bool permitido = false;
            foreach (RocolaOsSpotifyPeticion p in vivas)
            {
                int f = Volatile.Read(ref p.Fase);
                if (Volatile.Read(ref p.Motivo) != 0 || f == RocolaOsSpotifyPeticion.Acabada) continue;
                if (f == RocolaOsSpotifyPeticion.Nueva) permitido = true;   // (va a mandar play enseguida)
                else if (f == RocolaOsSpotifyPeticion.Mandada && (st.ItemUri == p.Uri || ahora - Volatile.Read(ref p.MandadaMs) < 8000)) permitido = true;
                else if (f == RocolaOsSpotifyPeticion.Suena && st.ItemUri == p.Uri) permitido = true;
            }
            if (permitido || ahora - ultimaPausaNoPedidaMs < 1000 || ahora - ultimaPausaNuestraMs < 2000) return;
            ultimaPausaNoPedidaMs = ahora;
            Interlocked.Increment(ref NoPedidas);
            Apunta("NO PEDIDO: Soloist tocaba «" + st.ItemUri + "» (" + st.ItemNombre + ") sin que la rocola lo pidiera (un celular, autoplay o la cola de Spotify): pausa, no suena ni se cobra");
            pausaNuestra = true;
            Manda(Orden("pause"));
        }

        // ---------- el estado en /run (sin secretos; clave=valor) ----------
        private static void EscribeEstado()
        {
            try
            {
                RocolaOsSpotifyEstado st = Estado;
                RocolaOsSpotifyPeticion p = Actual;
                bool sonando = p != null && !p.Rematada && Volatile.Read(ref p.Fase) == RocolaOsSpotifyPeticion.Suena;
                StringBuilder sb = new StringBuilder();
                sb.Append("ws=").Append(st.Ws ? 1 : 0).Append('\n');
                sb.Append("sesion=").Append(st.Sesion ? 1 : 0).Append('\n');
                sb.Append("activa=").Append(st.Activa ? 1 : 0).Append('\n');
                sb.Append("sonando=").Append(sonando ? p.Uri : "").Append('\n');
                sb.Append("en_cola=").Append(Volatile.Read(ref EnCola).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("esperando_credito=").Append(Interlocked.Read(ref SinCredito).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("cobradas=").Append(Interlocked.Read(ref Cobradas).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("devueltas=").Append(Interlocked.Read(ref Devueltas).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("no_pedidas=").Append(Interlocked.Read(ref NoPedidas).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("soloist=").Append(st.Status).Append('\n');
                FaseVigilante = LeeFaseVigilante();
                sb.Append("vigilante=").Append(FaseVigilante).Append('\n');
                sb.Append("anillo=").Append(Anillo != null && Anillo.Abierto ? 1 : 0).Append('\n');
                sb.Append("error=").Append((UltimoError ?? "").Replace('\n', ' ')).Append('\n');
                string t = sb.ToString();
                if (t == estadoEscrito) return;
                string ruta = RunSpotify("estado");
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                File.WriteAllText(ruta + ".n", t);
                File.Move(ruta + ".n", ruta, true);
                estadoEscrito = t;
            }
            catch { }
        }
        // LA FASE DEL VIGILANTE (I3: fase= de /run/rocola-soloist/estado: sin_llave, sin_programa, bajando, arrancando, corriendo, caducado,
        // esperando o parado; «-» si no esta). En el hilo de Spotify, con el estado (como mucho cada segundo): un archivo chico en memoria.
        internal static volatile string FaseVigilante = "-";
        private static string LeeFaseVigilante()
        {
            try
            {
                foreach (string l in File.ReadLines(RocolaOs.Run("rocola-soloist/estado")))
                {
                    if (!l.StartsWith("fase=", StringComparison.Ordinal)) continue;
                    string v = l.Substring(5).Trim();
                    if (v.Length == 0 || v.Length > 20) return "-";
                    foreach (char c in v) if (!((c >= 'a' && c <= 'z') || c == '_')) return "-";
                    return v;
                }
            }
            catch { }
            return "-";
        }
        // los renglones de /run/rocola-vivo.txt (Vivo; en el hilo de la pantalla)
        internal static void Vivo(StringBuilder sb)
        {
            RocolaOsSpotifyEstado st = Estado;
            sb.Append("spotify=ws ").Append(st.Ws ? 1 : 0).Append(" sesion ").Append(st.Sesion ? 1 : 0).Append(" activa ").Append(st.Activa ? 1 : 0)
              .Append(" soloist ").Append(st.Status.Length > 0 ? st.Status : "-")
              .Append(" vigilante ").Append(FaseVigilante)
              .Append(" en_cola ").Append(Volatile.Read(ref EnCola).ToString(CultureInfo.InvariantCulture))
              .Append(" cobradas ").Append(Interlocked.Read(ref Cobradas).ToString(CultureInfo.InvariantCulture))
              .Append(" devueltas ").Append(Interlocked.Read(ref Devueltas).ToString(CultureInfo.InvariantCulture))
              .Append(" no_pedidas ").Append(Interlocked.Read(ref NoPedidas).ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        // ================================================================== EL VIGILANTE (I4)
        private static volatile Process vigilante;
        [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int senal);
        private static void Vigila()
        {
            int[] esperas = { 2000, 10000, 30000, 120000 };
            int caidas = 0;
            while (!parar)
            {
                string bin = Vigilante;
                long desde = Environment.TickCount64;
                Process pr = null;
                try
                {
                    ProcessStartInfo si = new ProcessStartInfo(bin) { UseShellExecute = false };
                    si.ArgumentList.Add("corre");
                    si.Environment["SPOTIFY_NOMBRE"] = NombreBocina;
                    si.Environment["ROCOLA_SPOTIFY_ANILLO"] = RocolaOsSpotifyAnillo.Ruta;
                    pr = Process.Start(si);
                    vigilante = pr;
                    Apunta("vigilante abierto: " + bin + " corre (pid " + pr.Id.ToString(CultureInfo.InvariantCulture) + ")");
                }
                catch (Exception ex)
                {
                    UltimoError = "no se pudo abrir el vigilante " + bin + ": " + ex.GetType().Name;
                    Apunta(UltimoError);
                }
                if (pr != null)
                {
                    while (!parar && !pr.WaitForExit(300)) { }
                    if (parar) break;
                    int codigo = -1; try { codigo = pr.ExitCode; } catch { }
                    try { pr.Dispose(); } catch { }
                    vigilante = null;
                    Apunta("el vigilante salio con " + codigo.ToString(CultureInfo.InvariantCulture) + " (estuvo " + ((Environment.TickCount64 - desde) / 1000).ToString(CultureInfo.InvariantCulture) + " s)");
                }
                if (Environment.TickCount64 - desde > 600000) caidas = 0;   // (10 min en pie: vuelve a la espera corta)
                int espera = esperas[Math.Min(caidas, esperas.Length - 1)];
                caidas++;
                long hasta = Environment.TickCount64 + espera;
                while (!parar && Environment.TickCount64 < hasta) Thread.Sleep(200);
            }
        }
        // SIGTERM al vigilante (pasa SIGTERM a Soloist y espera hasta 8 s); si no sale en «esperaMs», sigue un hilo que lo mata a los 10 s
        private static void ParaVigilante(int esperaMs)
        {
            Process pr = vigilante;
            if (pr == null) return;
            int pid = -1;
            try { if (pr.HasExited) return; pid = pr.Id; } catch { return; }
            kill(pid, 15);
            bool salio = false;
            try { salio = pr.WaitForExit(esperaMs); } catch { }
            if (salio) { Apunta("el vigilante salio con SIGTERM"); return; }
            Apunta("el vigilante no salio en " + esperaMs.ToString(CultureInfo.InvariantCulture) + " ms: SIGKILL a los 10 s (si la rocola sigue viva)");
            Thread t = new Thread(delegate () { try { if (!pr.WaitForExit(10000 - esperaMs)) kill(pid, 9); } catch { } }) { IsBackground = true, Name = "RocolaOs:spotify-mata" };
            t.Start();
        }
    }
}

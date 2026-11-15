// UN SOLOIST FALSO (fase 11, grupo spotify-xp; FASE11_spotify-xp.md). Para probar el nucleo de Spotify de rocola-xp SIN el Soloist de
// verdad (que nunca se ejecuta fuera del A4 del dueno). Hace lo que la documentacion de Soloist dice que hace, con datos inventados:
//   - la linea de mandatos de Soloist: -n NOMBRE -k LLAVE -D DATOS -C CACHE -z MB -i VOL -w ADDR:PUERTO (puerto 0: el sistema elige y se
//     escribe en DATOS/ws.addr y DATOS/ws.port; se borran al salir), -V; con -k vacio sale con 1; con SOLOIST_FALSO_CADUCADO=1 sale con 10
//   - la API WebSocket (JSON): al conectar, auth_state (y playback_state con sesion); get_auth_state, get_state, get_queue, play (uri),
//     pause, skip_next, skip_prev, seek, set_volume, set_shuffle, set_repeat_track, set_repeat_context, add_to_queue, activate, deactivate;
//     command_result, error, y los eventos track_changed, playback_changed, device_changed, options_changed, volume_changed, queue_changed
//   - el sonido de cada cancion, en el anillo de I1 (directo, o con --por-pulso RUTA por el libpulse.so.0 de RUTA, como Soloist): un tono
//     con la FIRMA de la cancion para saber cual se oye y si se corto. El id de la cancion dice su firma y su duracion:
//       spotify:track:fFFFdSSS...   tono de FFF Hz, SSS segundos (p. ej. spotify:track:f440d012abc: 440 Hz, 12 s)
//       spotify:track:nx...         una que nunca empieza (no disponible: para la devolucion a los 15 s)
//     El ritmo lo marca el lector (I1: writable = objetivo - pendientes; sin lector, nada).
//   - al acabar una cancion pasa sola a otra (autoplay: spotify:track:f990d030autoplay, de 990 Hz), como haria Spotify: la rocola la tiene que
//     pausar. Con SOLOIST_FALSO_FIN=parar, se para en vez de seguir.
//   - con SOLOIST_FALSO_LENTO=SEG, «play» con uri tarda SEG segundos en empezar (la red lenta) y hasta entonces sigue lo de antes; lo
//     ultimo que se pide gana (otro play o un celular en medio cancelan el de antes, como en Spotify)
//   - con SOLOIST_FALSO_CELULAR_TRAS_PLAY=SEG:URI, SEG segundos despues del PRIMER play con uri, un celular pone URI (la carrera: alguien pone
//     algo mientras la pedida de la rocola aun no empieza)
//   - un guion de eventos (SOLOIST_FALSO_GUION="SEG:evento[:arg];..."; los segundos desde que arranca):
//       celular:URI   alguien pone URI desde un celular (suena sin que la rocola la pida)
//       inactiva      deja de ser la bocina activa (otro aparato se la lleva)
//       sinsesion     pierde la sesion
//       sesion        vuelve la sesion
//       corta         deja de mandar sonido (la red) aunque sigue «playing»
//       sigue         vuelve a mandar
//       cae           se cae (sale con 1, sin borrar ws.port: como un fallo de verdad)
//       cierraws      cierra las conexiones WebSocket (y sigue)
//       cola:URI      alguien anade URI a la cola de Spotify (queue_changed)
//   - lo que recibe y lo que pasa, en SOLOIST_FALSO_LOG (un renglon por cosa: «ms  quien  que»)
// Variables: SOLOIST_FALSO_SESION (1 de fabrica: ya emparejado), SOLOIST_FALSO_ACTIVA (0 de fabrica: la rocola tiene que mandar activate),
// SOLOIST_FALSO_LOG, SOLOIST_FALSO_GUION, SOLOIST_FALSO_FIN, SOLOIST_FALSO_CADUCADO, ROCOLA_SPOTIFY_ANILLO (o /run/rocola-xp/spotify/sonido.anillo).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

static unsafe class SoloistFalso
{
    // ---------- el estado de la bocina ----------
    static readonly object cerrojo = new object();
    static bool sesion = true, activa = false, shuffle = false;
    static string repeat = "off";
    static string status = "idle";
    static string item = "";           // la uri que suena
    static int genPlay;                // (SOLOIST_FALSO_LENTO: cada play o celular nuevo cancela el play lento de antes)
    static int carreraHecha;           // (SOLOIST_FALSO_CELULAR_TRAS_PLAY: una vez)
    static int volumen = 100;
    static long posCuadros;            // dentro de la cancion
    static long durCuadros;
    static double hz = 440;
    static bool cortado;               // («corta»: no manda sonido)
    static bool nunca;                 // (una «nx»: nunca empieza)
    static readonly List<string> colaSpotify = new List<string>();
    static string nombre = "Soloist", datos = ".", log;
    static readonly long t0 = Environment.TickCount64;
    static readonly List<Cliente> clientes = new List<Cliente>();
    const int Rate = 44100;
    const string Autoplay = "spotify:track:f990d030autoplay";

    sealed class Cliente { internal WebSocket Ws; internal readonly object Envio = new object(); }

    static int Main(string[] args)
    {
        string llave = null, ws = null, porPulso = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string v = i + 1 < args.Length ? args[i + 1] : null;
            switch (a)
            {
                case "-n": case "--device-name": nombre = v; i++; break;
                case "-k": case "--api-key": llave = v; i++; break;
                case "-D": case "--data-dir": datos = v; i++; break;
                case "-C": case "--cache-dir": case "-z": case "--cache-size": case "-i": case "--initial-volume": i++; break;
                case "-w": case "--ws": ws = v; i++; break;
                case "--por-pulso": porPulso = v; i++; break;
                case "-V": case "--version": Console.WriteLine("soloist-falso 0.0.1 (pruebas)"); return 0;
                case "-v": case "--verbose": break;
                default: Console.Error.WriteLine("soloist-falso: no conozco «" + a + "»"); return 1;
            }
        }
        log = Environment.GetEnvironmentVariable("SOLOIST_FALSO_LOG");
        if (Environment.GetEnvironmentVariable("SOLOIST_FALSO_CADUCADO") == "1") { Apunta("soloist", "caducado: sale con 10"); return 10; }
        if (string.IsNullOrEmpty(llave) || string.IsNullOrEmpty(nombre)) { Console.Error.WriteLine("soloist-falso: falta -n o -k"); return 1; }
        sesion = Environment.GetEnvironmentVariable("SOLOIST_FALSO_SESION") != "0";
        activa = Environment.GetEnvironmentVariable("SOLOIST_FALSO_ACTIVA") == "1";
        Directory.CreateDirectory(datos);
        File.WriteAllText(Path.Combine(datos, "soloist.pid"), Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        Apunta("soloist", "arranca: bocina «" + nombre + "», llave de " + llave.Length + " letras, datos " + datos + ", ws " + (ws ?? "-") + (porPulso != null ? ", por pulso " + porPulso : ""));
        TcpListener lis = null;
        if (ws != null)
        {
            int dp = ws.LastIndexOf(':');
            IPAddress ad = IPAddress.Parse(ws.Substring(0, dp));
            lis = new TcpListener(ad, int.Parse(ws.Substring(dp + 1), CultureInfo.InvariantCulture));
            lis.Start();
            int puerto = ((IPEndPoint)lis.LocalEndpoint).Port;
            File.WriteAllText(Path.Combine(datos, "ws.addr"), ad.ToString() + "\n");
            File.WriteAllText(Path.Combine(datos, "ws.port"), puerto.ToString(CultureInfo.InvariantCulture) + "\n");
            Apunta("soloist", "ws en " + ad + ":" + puerto);
            TcpListener l2 = lis;
            new Thread(() => Acepta(l2)) { IsBackground = true }.Start();
        }
        bool fin = false;
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, c => { c.Cancel = true; fin = true; });
        PosixSignalRegistration.Create(PosixSignal.SIGINT, c => { c.Cancel = true; fin = true; });
        Escritor esc = porPulso != null ? new EscritorPulso(porPulso) : new EscritorAnillo();
        new Thread(() => Guion()) { IsBackground = true }.Start();
        // EL REPRODUCTOR: cada 5 ms escribe lo que el lector deja, de la cancion que suena
        float[] buf = new float[Rate * 2];
        while (!fin)
        {
            Thread.Sleep(5);
            int quiere;
            bool sonando;
            lock (cerrojo) sonando = status == "playing" && !cortado && !nunca && item.Length > 0;
            esc.Tapado(!sonando);
            if (!sonando) continue;
            quiere = esc.Puede();
            if (quiere <= 0) continue;
            string acabo = null;
            int n;
            lock (cerrojoEscritor)
            {
            lock (cerrojo)
            {
                if (!(status == "playing" && !cortado && !nunca && item.Length > 0)) continue;
                n = (int)Math.Min(quiere, Math.Min(Rate, durCuadros - posCuadros));
                for (int i = 0; i < n; i++)
                {
                    double t = (posCuadros + i) / (double)Rate;
                    float s = (float)(0.25 * Math.Sin(2 * Math.PI * hz * t));
                    buf[i * 2] = s; buf[i * 2 + 1] = s;
                }
                posCuadros += n;
                if (posCuadros >= durCuadros) acabo = item;
            }
            if (n > 0) esc.Escribe(buf, n);
            }
            if (acabo != null)
            {
                Apunta("soloist", "acabo " + acabo);
                if (Environment.GetEnvironmentVariable("SOLOIST_FALSO_FIN") == "parar")
                {
                    lock (cerrojo) { status = "paused"; }
                    Manda("{\"type\":\"playback_changed\",\"status\":\"paused\"}");
                }
                else
                {   // pasa sola a la siguiente: la de su cola de Spotify o el autoplay (como Spotify)
                    string sig; string fuente;
                    lock (cerrojo) { if (colaSpotify.Count > 0) { sig = colaSpotify[0]; colaSpotify.RemoveAt(0); fuente = "queue"; } else { sig = Autoplay; fuente = "autoplay"; } }
                    Apunta("soloist", "pasa sola a " + sig + " (" + fuente + ")");
                    Pon(sig, false);
                    Manda(TrackChanged());
                }
            }
        }
        Apunta("soloist", "SIGTERM: sale");
        try { File.Delete(Path.Combine(datos, "ws.addr")); File.Delete(Path.Combine(datos, "ws.port")); File.Delete(Path.Combine(datos, "soloist.pid")); } catch { }
        esc.Cierra();
        return 0;
    }

    // ---------- la cancion: su firma (Hz) y su duracion, de su id ----------
    static readonly object cerrojoEscritor = new object();   // (escribir y vaciar no se cruzan: lo de la cancion de antes nunca cae detras del vaciado)
    static void Pon(string uri, bool vaciar)
    {
        lock (cerrojoEscritor)
        lock (cerrojo)
        {
            if (vaciar) escritorActual?.Vacia();
            item = uri; posCuadros = 0; status = "playing";
            string id = uri.StartsWith("spotify:track:") ? uri.Substring(14) : "";
            nunca = id.StartsWith("nx");
            hz = 300; double seg = 30;
            if (id.Length > 0 && id[0] == 'f')
            {
                int d = id.IndexOf('d');
                if (d > 1) { double.TryParse(id.Substring(1, d - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out hz); int e = d + 1; while (e < id.Length && char.IsDigit(id[e])) e++; double.TryParse(id.Substring(d + 1, e - d - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out seg); }
            }
            durCuadros = (long)(seg * Rate);
        }
    }
    static Escritor escritorActual;

    // ---------- los mensajes ----------
    static string Entidad(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return "{\"uri\":\"\",\"entity_type\":\"\",\"decorations\":{}}";
        long ms = durCuadros * 1000 / Rate;
        return "{\"uri\":\"" + uri + "\",\"entity_type\":\"track\",\"decorations\":{\"identity\":{\"name\":\"Cancion " + uri.Substring(14) + "\"},"
            + "\"visual_identity\":{\"cover\":[{\"url\":\"http://127.0.0.1:1/portada.jpg\",\"size\":\"large\"}]},"
            + "\"parent\":{\"entity\":{\"uri\":\"spotify:album:inventado\",\"entity_type\":\"album\",\"decorations\":{\"identity\":{\"name\":\"Disco inventado\"}}}},"
            + "\"creators\":[{\"entity\":{\"uri\":\"spotify:artist:inventado\",\"entity_type\":\"artist\",\"decorations\":{\"identity\":{\"name\":\"Artista inventado\"}}}}],"
            + "\"playback\":{\"duration_ms\":" + ms.ToString(CultureInfo.InvariantCulture) + ",\"content_ratings\":[]}}}";
    }
    static string Auth() { lock (cerrojo) return "{\"type\":\"auth_state\",\"logged_in\":" + B(sesion) + ",\"is_active\":" + B(activa) + ",\"device_name\":\"" + nombre + "\"}"; }
    static string Estado()
    {
        lock (cerrojo)
            return "{\"type\":\"playback_state\",\"status\":\"" + status + "\",\"item\":" + Entidad(item) + ",\"context\":" + Entidad(item) + ",\"position\":{\"position_ms\":"
                + (posCuadros * 1000 / Rate).ToString(CultureInfo.InvariantCulture) + ",\"timestamp_ms\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
                + ",\"speed\":" + (status == "playing" ? "1.0" : "0.0") + "},\"volume\":" + volumen + ",\"is_active\":" + B(activa)
                + ",\"options\":{\"shuffle\":" + B(shuffle) + ",\"repeat\":\"" + repeat + "\",\"playback_speed\":1.0,\"modes\":{}},\"available_actions\":{\"pause\":{}}}";
    }
    static string TrackChanged() { lock (cerrojo) return "{\"type\":\"track_changed\",\"item\":" + Entidad(item) + "}"; }
    static string Opciones() { lock (cerrojo) return "{\"type\":\"options_changed\",\"options\":{\"shuffle\":" + B(shuffle) + ",\"repeat\":\"" + repeat + "\",\"playback_speed\":1.0,\"modes\":{}}}"; }
    static string B(bool b) { return b ? "true" : "false"; }

    static void Manda(string json) { Cliente[] cs; lock (clientes) cs = clientes.ToArray(); foreach (Cliente c in cs) MandaA(c, json); }
    static void MandaA(Cliente c, string json)
    {
        try { lock (c.Envio) c.Ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult(); }
        catch { }
    }

    // ---------- el servidor WebSocket (el saludo de HTTP a mano; luego WebSocket.CreateFromStream) ----------
    static void Acepta(TcpListener lis)
    {
        while (true)
        {
            TcpClient tc;
            try { tc = lis.AcceptTcpClient(); } catch { return; }
            new Thread(() => Atiende(tc)) { IsBackground = true }.Start();
        }
    }
    static void Atiende(TcpClient tc)
    {
        Cliente c = null;
        try
        {
            NetworkStream s = tc.GetStream();
            StringBuilder cab = new StringBuilder();
            int b; int fin = 0;
            while (fin < 4 && (b = s.ReadByte()) >= 0) { char ch = (char)b; cab.Append(ch); fin = (ch == '\r' || ch == '\n') ? fin + 1 : 0; }
            string key = null;
            foreach (string l in cab.ToString().Split("\r\n")) if (l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)) key = l.Substring(18).Trim();
            if (key == null) { tc.Close(); return; }
            string acc = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            byte[] r = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + acc + "\r\n\r\n");
            s.Write(r, 0, r.Length);
            c = new Cliente { Ws = WebSocket.CreateFromStream(s, true, null, TimeSpan.FromSeconds(30)) };
            lock (clientes) clientes.Add(c);
            Apunta("ws", "conecta un cliente");
            MandaA(c, Auth());
            bool s2; lock (cerrojo) s2 = sesion;
            if (s2) MandaA(c, Estado());
            byte[] buf = new byte[65536];
            while (true)
            {
                WebSocketReceiveResult rr = c.Ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None).GetAwaiter().GetResult();
                if (rr.MessageType == WebSocketMessageType.Close) break;
                Orden(c, Encoding.UTF8.GetString(buf, 0, rr.Count));
            }
        }
        catch { }
        if (c != null) lock (clientes) clientes.Remove(c);
        Apunta("ws", "se va un cliente");
        try { tc.Close(); } catch { }
    }
    static void Orden(Cliente c, string txt)
    {
        Apunta("orden", txt);
        string tipo = "", cmd = "", uri = null; bool? en = null; int vol = -1;
        try
        {
            using (JsonDocument d = JsonDocument.Parse(txt))
            {
                JsonElement r = d.RootElement, v;
                if (r.TryGetProperty("type", out v)) tipo = v.GetString();
                if (r.TryGetProperty("command", out v)) cmd = v.GetString();
                if (r.TryGetProperty("uri", out v)) uri = v.GetString();
                if (r.TryGetProperty("enabled", out v)) en = v.ValueKind == JsonValueKind.True;
                if (r.TryGetProperty("volume", out v)) vol = v.GetInt32();
            }
        }
        catch { MandaA(c, "{\"type\":\"error\",\"message\":\"malformed JSON\"}"); return; }
        if (tipo != "command") { MandaA(c, "{\"type\":\"error\",\"message\":\"unknown message type\"}"); return; }
        bool s; lock (cerrojo) s = sesion;
        if (cmd == "get_auth_state") { MandaA(c, Auth()); return; }
        if (!s) { MandaA(c, "{\"type\":\"error\",\"message\":\"command requires authentication\"}"); return; }
        switch (cmd)
        {
            case "get_state": MandaA(c, Estado()); return;
            case "get_queue": MandaA(c, "{\"type\":\"queue_changed\",\"previous\":[],\"upcoming\":[]}"); return;
            case "play":
                MandaA(c, "{\"type\":\"command_result\",\"command\":\"play\"}");
                if (uri != null)
                {
                    bool act; lock (cerrojo) act = activa;
                    if (!act) { lock (cerrojo) activa = true; Manda("{\"type\":\"device_changed\",\"is_active\":true,\"device_name\":\"" + nombre + "\"}"); }
                    int g = Interlocked.Increment(ref genPlay);
                    double lento = 0;
                    double.TryParse(Environment.GetEnvironmentVariable("SOLOIST_FALSO_LENTO") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out lento);
                    Action empieza = () =>
                    {
                        if (Volatile.Read(ref genPlay) != g) { Apunta("soloist", "play " + uri + " cancelado (se pidio otra cosa)"); return; }
                        Pon(uri, true);
                        Manda(TrackChanged());
                        if (!uri.Substring(14).StartsWith("nx")) Manda("{\"type\":\"playback_changed\",\"status\":\"playing\"}");
                        else lock (cerrojo) status = "buffering";
                    };
                    if (lento > 0) new Thread(() => { Thread.Sleep((int)(lento * 1000)); empieza(); }) { IsBackground = true }.Start();
                    else empieza();
                    string carrera = Environment.GetEnvironmentVariable("SOLOIST_FALSO_CELULAR_TRAS_PLAY");
                    if (!string.IsNullOrEmpty(carrera) && Interlocked.Exchange(ref carreraHecha, 1) == 0)
                    {
                        string[] cx = carrera.Split(':', 2);
                        double cs = double.Parse(cx[0], CultureInfo.InvariantCulture);
                        new Thread(() =>
                        {
                            Thread.Sleep((int)(cs * 1000));
                            Apunta("guion", "celular (tras el play) " + cx[1]);
                            Interlocked.Increment(ref genPlay);
                            lock (cerrojo) activa = true;
                            Pon(cx[1], true);
                            Manda(TrackChanged()); Manda("{\"type\":\"playback_changed\",\"status\":\"playing\"}");
                        }) { IsBackground = true }.Start();
                    }
                }
                else { lock (cerrojo) { if (item.Length > 0) status = "playing"; } Manda("{\"type\":\"playback_changed\",\"status\":\"playing\"}"); }
                return;
            case "pause":
                MandaA(c, "{\"type\":\"command_result\",\"command\":\"pause\"}");
                lock (cerrojo) { if (status == "playing" || status == "buffering") status = "paused"; }
                Manda("{\"type\":\"playback_changed\",\"status\":\"paused\"}");
                return;
            case "activate":
                MandaA(c, "{\"type\":\"command_result\",\"command\":\"activate\"}");
                lock (cerrojo) activa = true;
                Manda("{\"type\":\"device_changed\",\"is_active\":true,\"device_name\":\"" + nombre + "\"}");
                return;
            case "deactivate":
                MandaA(c, "{\"type\":\"command_result\",\"command\":\"deactivate\"}");
                lock (cerrojo) { activa = false; status = "paused"; }
                Manda("{\"type\":\"device_changed\",\"is_active\":false,\"device_name\":\"" + nombre + "\"}");
                return;
            case "set_shuffle": MandaA(c, "{\"type\":\"command_result\",\"command\":\"set_shuffle\"}"); lock (cerrojo) shuffle = en == true; Manda(Opciones()); return;
            case "set_repeat_track": MandaA(c, "{\"type\":\"command_result\",\"command\":\"set_repeat_track\"}"); lock (cerrojo) { if (en == true) repeat = "track"; else if (repeat == "track") repeat = "off"; } Manda(Opciones()); return;
            case "set_repeat_context": MandaA(c, "{\"type\":\"command_result\",\"command\":\"set_repeat_context\"}"); lock (cerrojo) { if (en == true) repeat = "context"; else if (repeat == "context") repeat = "off"; } Manda(Opciones()); return;
            case "set_volume": MandaA(c, "{\"type\":\"command_result\",\"command\":\"set_volume\"}"); lock (cerrojo) volumen = vol; Manda("{\"type\":\"volume_changed\",\"volume\":" + vol + "}"); return;
            case "skip_next": case "skip_prev": case "seek": case "add_to_queue":
                MandaA(c, "{\"type\":\"command_result\",\"command\":\"" + cmd + "\"}"); return;
            default: MandaA(c, "{\"type\":\"error\",\"message\":\"unknown command\"}"); return;
        }
    }

    // ---------- el guion ----------
    static void Guion()
    {
        string g = Environment.GetEnvironmentVariable("SOLOIST_FALSO_GUION");
        if (string.IsNullOrEmpty(g)) return;
        List<(double, string, string)> ev = new List<(double, string, string)>();
        foreach (string p in g.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] x = p.Split(':', 3);
            ev.Add((double.Parse(x[0], CultureInfo.InvariantCulture), x[1], x.Length > 2 ? x[2] : null));
        }
        ev.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        foreach (var (seg, que, arg) in ev)
        {
            long hasta = t0 + (long)(seg * 1000);
            while (Environment.TickCount64 < hasta) Thread.Sleep(20);
            Apunta("guion", que + (arg != null ? " " + arg : ""));
            switch (que)
            {
                case "celular":
                    Interlocked.Increment(ref genPlay);
                    lock (cerrojo) activa = true;
                    Pon(arg, true);
                    Manda(TrackChanged()); Manda("{\"type\":\"playback_changed\",\"status\":\"playing\"}");
                    break;
                case "inactiva":
                    lock (cerrojo) { activa = false; status = "paused"; }
                    Manda("{\"type\":\"device_changed\",\"is_active\":false,\"device_name\":\"" + nombre + "\"}");
                    Manda("{\"type\":\"playback_changed\",\"status\":\"paused\"}");
                    break;
                case "sinsesion": lock (cerrojo) { sesion = false; status = "idle"; } Manda(Auth()); break;
                case "sesion": lock (cerrojo) sesion = true; Manda(Auth()); Manda(Estado()); break;
                case "corta": lock (cerrojo) cortado = true; break;
                case "sigue": lock (cerrojo) cortado = false; break;
                case "cola": lock (cerrojo) colaSpotify.Add(arg); Manda("{\"type\":\"queue_changed\",\"previous\":[],\"upcoming\":[{\"uid\":\"" + arg + "\",\"source\":\"queue\",\"item\":" + Entidad(arg) + "}]}"); break;
                case "cierraws":
                    { Cliente[] cs; lock (clientes) cs = clientes.ToArray(); foreach (Cliente c in cs) try { c.Ws.Abort(); } catch { } }
                    break;
                case "cae": Apunta("soloist", "se cae (sale con 1)"); Environment.Exit(1); break;
            }
        }
    }

    static readonly object cerrojoLog = new object();
    internal static void Apunta(string quien, string que)
    {
        if (string.IsNullOrEmpty(log)) return;
        lock (cerrojoLog)
            try { File.AppendAllText(log, (Environment.TickCount64 - t0).ToString(CultureInfo.InvariantCulture) + "\t" + quien + "\t" + que + "\n"); } catch { }
    }

    // ================================================================== EL SONIDO
    abstract class Escritor
    {
        internal abstract int Puede();                    // cuantos cuadros deja escribir el lector
        internal abstract void Escribe(float[] b, int n);
        internal abstract void Tapado(bool t);
        internal abstract void Vacia();
        internal abstract void Cierra();
    }

    // DIRECTO AL ANILLO (I1), escrito aqui sin mirar el lector de rocola-xp: el formato, de su descripcion
    sealed class EscritorAnillo : Escritor
    {
        const int Cap = 65536, Cab = 4096;
        const long Tam = Cab + (long)Cap * 8;
        readonly string ruta;
        byte* b;
        MemoryMappedFile mm; MemoryMappedViewAccessor va; FileStream fs;
        bool tapado = true;
        long ultimoLatido;
        uint genFlujo;
        internal EscritorAnillo()
        {
            ruta = Environment.GetEnvironmentVariable("ROCOLA_SPOTIFY_ANILLO");
            if (string.IsNullOrEmpty(ruta)) ruta = Path.Combine(Environment.GetEnvironmentVariable("ROCOLA_RUN") ?? "/run", "rocola-xp/spotify/sonido.anillo");
            escritorActual = this;
            Abre();
            // un flujo conectado (como pa_stream_connect_playback): gen_flujo y gen_vaciado suben, frecuencia y formato
            Volatile.Write(ref *(uint*)(b + 16), (uint)Rate);
            Volatile.Write(ref *(uint*)(b + 84), 5u);   // PA_SAMPLE_FLOAT32LE
            Volatile.Write(ref *(uint*)(b + 72), (uint)Environment.ProcessId);
            Vacia();
            genFlujo = Interlocked.Increment(ref *(uint*)(b + 20));
        }
        void Abre()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            bool vale = false;
            try
            {
                FileInfo fi = new FileInfo(ruta);
                if (fi.Exists && fi.Length == Tam)
                {
                    byte[] c = File.ReadAllBytes(ruta).AsSpan(0, 16).ToArray();
                    vale = BitConverter.ToUInt32(c, 0) == 0x4F4C5550 && BitConverter.ToUInt32(c, 4) == 1 && BitConverter.ToUInt32(c, 8) == Cap && BitConverter.ToUInt32(c, 12) == 2;
                }
            }
            catch { }
            if (!vale)
            {
                string t = ruta + ".t." + Environment.ProcessId;
                using (FileStream f = new FileStream(t, FileMode.Create))
                {
                    byte[] cab = new byte[Cab];
                    BitConverter.GetBytes(0x4F4C5550u).CopyTo(cab, 0); BitConverter.GetBytes(1u).CopyTo(cab, 4);
                    BitConverter.GetBytes((uint)Cap).CopyTo(cab, 8); BitConverter.GetBytes(2u).CopyTo(cab, 12); BitConverter.GetBytes(1u).CopyTo(cab, 24);
                    f.Write(cab); f.SetLength(Tam);
                }
                File.Move(t, ruta, true);
                Apunta("anillo", "creado " + ruta);
            }
            fs = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            mm = MemoryMappedFile.CreateFromFile(fs, null, Tam, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
            va = mm.CreateViewAccessor(0, Tam);
            byte* p = null; va.SafeMemoryMappedViewHandle.AcquirePointer(ref p); b = p + va.PointerOffset;
        }
        static long Mono() { return (long)(System.Diagnostics.Stopwatch.GetTimestamp() * (1e9 / System.Diagnostics.Stopwatch.Frequency)); }
        long Pendientes()
        {
            long l = Math.Max(Volatile.Read(ref *(long*)(b + 40)), Volatile.Read(ref *(long*)(b + 48)));
            return Volatile.Read(ref *(long*)(b + 32)) - l;
        }
        internal override int Puede()
        {
            long ahora = Mono();
            if (ahora - ultimoLatido > 200_000_000) { Volatile.Write(ref *(long*)(b + 56), ahora); ultimoLatido = ahora; }
            bool lector = Volatile.Read(ref *(uint*)(b + 76)) == 1 && ahora - Volatile.Read(ref *(long*)(b + 64)) < 2_000_000_000L;
            if (!lector) return 0;
            long objetivo = Rate / 4;   // 250 ms (sin tlength)
            long p = Pendientes();
            return (int)Math.Max(0, objetivo - p);
        }
        internal override void Escribe(float[] src, int n)
        {
            long e = Volatile.Read(ref *(long*)(b + 32));
            float* d = (float*)(b + Cab);
            for (int i = 0; i < n; i++)
            {
                long pos = (e + i) % Cap;
                d[pos * 2] = src[i * 2]; d[pos * 2 + 1] = src[i * 2 + 1];
            }
            Volatile.Write(ref *(long*)(b + 32), e + n);   // (release: despues de los datos)
            long ahora = Mono(); Volatile.Write(ref *(long*)(b + 56), ahora); ultimoLatido = ahora;
        }
        internal override void Tapado(bool t)
        {
            if (t == tapado) return;
            tapado = t;
            Volatile.Write(ref *(uint*)(b + 24), t ? 1u : 0u);
        }
        internal override void Vacia()
        {
            Volatile.Write(ref *(long*)(b + 48), Volatile.Read(ref *(long*)(b + 32)));   // vaciado_hasta ANTES de subir gen_vaciado
            Interlocked.Increment(ref *(uint*)(b + 28));
            Apunta("anillo", "vaciado en " + Volatile.Read(ref *(long*)(b + 32)));
        }
        internal override void Cierra()
        {
            Volatile.Write(ref *(uint*)(b + 24), 1u);
            Volatile.Write(ref *(uint*)(b + 16), 0u);
            Interlocked.Increment(ref *(uint*)(b + 20));
        }
    }

    // POR UN libpulse.so.0 (el de rocola-pulso, sistema/rocola/pulso): como lo usaria Soloist (la API asincrona con su hilo: threaded mainloop,
    // contexto, flujo de float32 a 44,1 kHz; writable_size, write, cork y flush). Sin la libreria no se puede probar: se dice al arrancar.
    sealed class EscritorPulso : Escritor
    {
        [StructLayout(LayoutKind.Sequential)] struct SampleSpec { public int format; public uint rate; public byte channels; }
        [StructLayout(LayoutKind.Sequential)] struct BufferAttr { public uint maxlength, tlength, prebuf, minreq, fragsize; }
        delegate IntPtr F_ptr();
        delegate IntPtr F_ptr_ptr(IntPtr a);
        delegate IntPtr F_ctx_new(IntPtr api, string nombre);
        delegate int F_ctx_connect(IntPtr c, string server, int flags, IntPtr api);
        delegate int F_int_ptr(IntPtr a);
        delegate void F_void_ptr(IntPtr a);
        delegate IntPtr F_stream_new(IntPtr c, string nombre, ref SampleSpec ss, IntPtr map);
        delegate int F_stream_connect(IntPtr s, string dev, ref BufferAttr attr, int flags, IntPtr vol, IntPtr sync);
        delegate UIntPtr F_size_ptr(IntPtr s);
        delegate int F_stream_write(IntPtr s, IntPtr data, UIntPtr n, IntPtr free, long offset, int seek);
        delegate IntPtr F_stream_cork(IntPtr s, int b, IntPtr cb, IntPtr ud);
        delegate IntPtr F_stream_op(IntPtr s, IntPtr cb, IntPtr ud);
        readonly IntPtr lib, ml, ctx, st;
        readonly F_void_ptr lockF, unlockF, opUnref;
        readonly F_size_ptr writable;
        readonly F_stream_write write;
        readonly F_stream_cork cork;
        readonly F_stream_op flush;
        bool tapado = true;
        T F<T>(string n) where T : Delegate { return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, n)); }
        internal EscritorPulso(string ruta)
        {
            escritorActual = this;
            lib = NativeLibrary.Load(ruta);
            ml = F<F_ptr>("pa_threaded_mainloop_new")();
            IntPtr api = F<F_ptr_ptr>("pa_threaded_mainloop_get_api")(ml);
            ctx = F<F_ctx_new>("pa_context_new")(api, "soloist-falso");
            lockF = F<F_void_ptr>("pa_threaded_mainloop_lock"); unlockF = F<F_void_ptr>("pa_threaded_mainloop_unlock");
            opUnref = F<F_void_ptr>("pa_operation_unref");
            if (F<F_ctx_connect>("pa_context_connect")(ctx, null, 0, IntPtr.Zero) < 0) throw new Exception("pa_context_connect fallo");
            F<F_int_ptr>("pa_threaded_mainloop_start")(ml);
            F_int_ptr ctxState = F<F_int_ptr>("pa_context_get_state");
            for (int i = 0; i < 200; i++) { lockF(ml); int s = ctxState(ctx); unlockF(ml); if (s == 4) break; if (s >= 5) throw new Exception("contexto en " + s); Thread.Sleep(10); }   // PA_CONTEXT_READY = 4
            SampleSpec ss = new SampleSpec { format = 5, rate = Rate, channels = 2 };   // PA_SAMPLE_FLOAT32LE
            lockF(ml);
            st = F<F_stream_new>("pa_stream_new")(ctx, "Soloist", ref ss, IntPtr.Zero);
            BufferAttr at = new BufferAttr { maxlength = uint.MaxValue, tlength = (uint)(Rate / 4 * 8), prebuf = uint.MaxValue, minreq = uint.MaxValue, fragsize = uint.MaxValue };
            int r = F<F_stream_connect>("pa_stream_connect_playback")(st, null, ref at, 0, IntPtr.Zero, IntPtr.Zero);
            unlockF(ml);
            if (r < 0) throw new Exception("pa_stream_connect_playback fallo");
            writable = F<F_size_ptr>("pa_stream_writable_size"); write = F<F_stream_write>("pa_stream_write");
            cork = F<F_stream_cork>("pa_stream_cork"); flush = F<F_stream_op>("pa_stream_flush");
            Apunta("pulso", "conectado por " + ruta);
        }
        internal override int Puede() { lockF(ml); ulong n = (ulong)writable(st); unlockF(ml); return (int)Math.Min(int.MaxValue, n / 8); }
        internal override void Escribe(float[] b, int n)
        {
            fixed (float* p = b) { lockF(ml); write(st, (IntPtr)p, (UIntPtr)(ulong)(n * 8), IntPtr.Zero, 0, 0); unlockF(ml); }
        }
        internal override void Tapado(bool t) { if (t == tapado) return; tapado = t; lockF(ml); IntPtr o = cork(st, t ? 1 : 0, IntPtr.Zero, IntPtr.Zero); if (o != IntPtr.Zero) opUnref(o); unlockF(ml); }
        internal override void Vacia() { lockF(ml); IntPtr o = flush(st, IntPtr.Zero, IntPtr.Zero); if (o != IntPtr.Zero) opUnref(o); unlockF(ml); }
        internal override void Cierra()
        {
            try { lockF(ml); F<F_int_ptr>("pa_stream_disconnect")(st); F<F_void_ptr>("pa_stream_unref")(st); F<F_void_ptr>("pa_context_disconnect")(ctx); unlockF(ml); F<F_void_ptr>("pa_threaded_mainloop_stop")(ml); } catch { }
        }
    }
}

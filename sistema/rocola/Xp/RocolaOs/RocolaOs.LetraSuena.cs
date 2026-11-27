// LA LETRA DE LO QUE SUENA, SOLO LOS DATOS (fase 11, grupo letras; FASE11_letras.md). Funcion «letra_cielo», ENCENDIDA de fabrica: solo PIDE
// la letra; NO SE DIBUJA NADA (el dibujo espera la maqueta y el si del dueno: decision del 20-11-2026, AGENTS.md).
// Al empezar a sonar una cancion (tuya o de Spotify; un video no), en el hilo «letra_cielo»:
//   1. LRCLIB (https://lrclib.net/api/get?track_name=&artist_name=&album_name=&duration=, sin llave, con un User-Agent generico con el nombre
//      del programa y nada del dueno): la letra SINCRONIZADA (syncedLyrics, LRC);
//   2. si no hay, la USLT del MP3 (sin tiempos; solo si es un MP3 tuyo);
//   3. si tampoco, la letra sin tiempos de LRCLIB (plainLyrics), si la dio.
// Lo que se sabe queda en una FOTO INMUTABLE (RocolaOsLetra: el proveedor, «LRCLIB» o «el MP3», si es sincronizada, sus tiempos y sus versos)
// y, en cada latido de la pantalla, otra foto inmutable con el VERSO QUE SE CANTA y el SIGUIENTE segun la posicion de lo que suena
// (RocolaOsLetraVerso): de ahi leera el dibujo cuando lo haya (RocolaOsLetraSuena.Verso, y RocolaOsLetra.En para una posicion mas fina).
// LAS LETRAS NO SE GUARDAN: cache en memoria (la sesion, 64 canciones) y en /run/rocola-xp/letras (lo vivo: se borra al apagar). Ni en los
// datos, ni en la bitacora, ni en el estado (/run/rocola-xp/letras/estado: solo numeros, sin texto de la letra).
// EL TOPE: si LRCLIB CONTESTO que no la tiene (y el MP3 tampoco), esa ya no se vuelve a pedir en la sesion; tras un fallo de la red (sin red,
// 5xx o 429) no se pide nada en 2 minutos, y esa cancion SI se vuelve a intentar la siguiente vez que suene pasado el freno (una peticion
// cada vez que empieza, nunca en bucle: la rocola esta prendida semanas y pudo arrancar sin red). Lo que esta en /run se toca al usarlo
// (el recorte quita lo menos usado). Si /api/get no la encuentra sin disco o sin duracion (o no las acepta: 400), una busqueda
// (/api/search) por titulo y artista, la de la duracion mas cercana (a 3 s si se sabe). Con ROCOLA_LETRA_API, un LRCLIB falso (las pruebas).
// Apagada (letra_cielo=0) o sin ROCOLA_OS: ni el hilo ni una peticion.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    // LA LETRA DE UNA CANCION (inmutable)
    internal sealed class RocolaOsLetra
    {
        internal readonly string Ruta, Proveedor;   // Proveedor: «LRCLIB» o «el MP3»
        internal readonly bool Sincronizada;
        internal readonly double[] Tiempos;         // (s; solo si es sincronizada, en orden)
        internal readonly string[] Versos;
        internal RocolaOsLetra(string ruta, string proveedor, bool sincronizada, double[] tiempos, string[] versos)
        { Ruta = ruta; Proveedor = proveedor; Sincronizada = sincronizada; Tiempos = tiempos; Versos = versos; }
        // EL VERSO QUE SE CANTA en pos (s): su numero (-1 antes del primero o sin tiempos) y el siguiente que dice algo (-1 si no hay)
        internal int En(double pos, out int siguiente)
        {
            siguiente = -1;
            if (!Sincronizada || Tiempos == null || Tiempos.Length == 0) return -1;
            int lo = 0, hi = Tiempos.Length - 1, i = -1;
            while (lo <= hi) { int m = (lo + hi) >> 1; if (Tiempos[m] <= pos) { i = m; lo = m + 1; } else hi = m - 1; }
            for (int k = i + 1; k < Versos.Length; k++) if (Versos[k].Length > 0) { siguiente = k; break; }
            return i;
        }
    }
    // EL VERSO DE AHORA (inmutable; lo pone el latido)
    internal sealed class RocolaOsLetraVerso
    {
        internal readonly RocolaOsLetra Letra;
        internal readonly double Pos;
        internal readonly int Indice, Siguiente;
        internal RocolaOsLetraVerso(RocolaOsLetra l, double pos) { Letra = l; Pos = pos; Indice = l.En(pos, out Siguiente); }
        internal string Actual { get { return Indice >= 0 ? Letra.Versos[Indice] : null; } }
        internal string DespuesDice { get { return Siguiente >= 0 ? Letra.Versos[Siguiente] : null; } }
    }

    internal static class RocolaOsLetraSuena
    {
        internal const string UserAgent = "rocola-xp/1 (Rocola OS)";
        internal const int CacheMax = 64;
        internal static readonly int FrenoRedMs = FrenoDe(Environment.GetEnvironmentVariable("ROCOLA_LETRA_FRENO_MS"));   // (2 min; las pruebas lo acortan)
        private static int FrenoDe(string e) { int ms; return int.TryParse(e, NumberStyles.None, CultureInfo.InvariantCulture, out ms) && ms > 0 ? ms : 120000; }
        internal const int RunMax = 400, RunQueda = 300;   // (/run es memoria: con mas de 400 letras se borran las mas viejas hasta 300)
        internal sealed class Pedido { internal string Ruta, Titulo, Artista, Disco, Mp3; internal int Duracion; }
        private static readonly Dictionary<string, RocolaOsLetra> cache = new Dictionary<string, RocolaOsLetra>(StringComparer.Ordinal);
        private static readonly Queue<string> orden = new Queue<string>();
        private static readonly HashSet<string> sinLetra = new HashSet<string>(StringComparer.Ordinal);   // (la sesion: LRCLIB contesto que no y el MP3 tampoco)
        private static readonly object cerrojo = new object();
        private static readonly AutoResetEvent despierta = new AutoResetEvent(false);
        private static Pedido pendiente;
        private static Thread hilo;
        private static volatile bool parar;
        private static RocolaOsLetra actual;             // la letra de lo que suena (o null)
        private static RocolaOsLetraVerso verso;         // el verso de ahora (o null)
        private static string pidiendo;                  // la ruta que se esta buscando
        private static long peticiones, fallidas, frenoHasta = long.MinValue / 2;
        private static HttpClient http;

        internal static RocolaOsLetra Actual { get { return Volatile.Read(ref actual); } }
        internal static RocolaOsLetraVerso Verso { get { return Volatile.Read(ref verso); } }
        internal static long Peticiones { get { return Interlocked.Read(ref peticiones); } }
        internal static long Fallidas { get { return Interlocked.Read(ref fallidas); } }

        // ---------- desde la pantalla ----------
        internal static void Pide(Pedido p)
        {
            lock (cerrojo)
            {
                pendiente = p; Volatile.Write(ref pidiendo, p.Ruta);
                RocolaOsLetra l;
                Volatile.Write(ref actual, cache.TryGetValue(p.Ruta, out l) ? l : null);
                if (hilo == null) hilo = RocolaOs.Hilo("letra_cielo", Bucle);
            }
            despierta.Set();
        }
        internal static void Quita() { Volatile.Write(ref pidiendo, null); Volatile.Write(ref actual, null); Volatile.Write(ref verso, null); despierta.Set(); }
        // el verso de la posicion de lo que suena (cada latido)
        internal static void Mira(string ruta, double pos)
        {
            RocolaOsLetra l = Actual;
            if (l == null || l.Ruta != ruta) { if (Verso != null) Volatile.Write(ref verso, null); return; }
            RocolaOsLetraVerso v = Verso;
            if (v != null && v.Letra == l && v.Indice == l.En(pos, out _)) return;   // (el mismo verso: la misma foto)
            Volatile.Write(ref verso, new RocolaOsLetraVerso(l, pos));
            despierta.Set();
        }
        internal static void Para() { parar = true; despierta.Set(); }

        // ---------- el hilo ----------
        private static void Bucle()
        {
            string escrito = null;
            while (!parar)
            {
                Pedido p;
                lock (cerrojo) { p = pendiente; pendiente = null; }
                if (p != null)
                {
                    RocolaOsLetra l = Busca(p);
                    lock (cerrojo) { if (Volatile.Read(ref pidiendo) == p.Ruta) Volatile.Write(ref actual, l); }
                }
                string e = Estado();
                if (e != escrito) { escrito = e; EscribeEstado(e); }
                despierta.WaitOne(1000);
            }
        }
        internal static string Estado()
        {
            RocolaOsLetra l = Actual; RocolaOsLetraVerso v = Verso; string r = Volatile.Read(ref pidiendo);
            StringBuilder sb = new StringBuilder();
            sb.Append("letra=").Append(r == null ? "nada_suena" : l == null ? "ninguna" : l.Sincronizada ? "sincronizada" : "sin_tiempos").Append('\n');
            sb.Append("proveedor=").Append(l != null ? l.Proveedor : "").Append('\n');
            sb.Append("versos=").Append(l != null ? l.Versos.Length.ToString(CultureInfo.InvariantCulture) : "0").Append('\n');
            sb.Append("verso=").Append(v != null && v.Letra == l ? v.Indice.ToString(CultureInfo.InvariantCulture) : "-1").Append('\n');
            sb.Append("siguiente=").Append(v != null && v.Letra == l ? v.Siguiente.ToString(CultureInfo.InvariantCulture) : "-1").Append('\n');
            sb.Append("peticiones=").Append(Peticiones.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("fallidas=").Append(Fallidas.ToString(CultureInfo.InvariantCulture)).Append('\n');
            return sb.ToString();
        }
        private static void EscribeEstado(string e)
        {
            try
            {
                string d = RocolaOs.Run("rocola-xp/letras");
                Directory.CreateDirectory(d);
                string ruta = Path.Combine(d, "estado"), t = ruta + ".t";
                File.WriteAllText(t, e);
                File.Move(t, ruta, true);
            }
            catch { }
        }

        // LA LETRA DE UNA CANCION: de memoria, de /run, de LRCLIB, de su MP3 (en este orden). Tambien la usa la orden de prueba (sin hilo).
        internal static RocolaOsLetra Busca(Pedido p)
        {
            RocolaOsLetra l;
            lock (cerrojo) { if (cache.TryGetValue(p.Ruta, out l)) return l; }
            l = DeRun(p.Ruta);
            if (l == null)
            {
                lock (cerrojo) { if (sinLetra.Contains(p.Ruta)) return null; }   // (LRCLIB ya dijo que no en la sesion: nada, sin otra peticion)
                string sinTiempos; bool contesto;
                l = DeLrclib(p, out sinTiempos, out contesto);
                if (l == null && p.Mp3 != null)
                {
                    string uslt = null;
                    try { uslt = RocolaOsLetrasMp3.LetraDelMp3(p.Mp3); } catch { }
                    l = DeTexto(p.Ruta, "el MP3", false, uslt);
                }
                if (l == null && sinTiempos != null) l = DeTexto(p.Ruta, "LRCLIB", false, sinTiempos);
                if (l != null) ARun(l, p.Ruta);
                else if (contesto) lock (cerrojo) sinLetra.Add(p.Ruta);   // (sin contestar -freno, sin red, 5xx, 429-: la siguiente vez que suene)
            }
            if (l == null) return null;
            lock (cerrojo)
            {
                if (!cache.ContainsKey(p.Ruta)) { cache[p.Ruta] = l; orden.Enqueue(p.Ruta); while (orden.Count > CacheMax) cache.Remove(orden.Dequeue()); }
            }
            return l;
        }

        private static string Api
        {
            get { string e = Environment.GetEnvironmentVariable("ROCOLA_LETRA_API"); return string.IsNullOrEmpty(e) ? "https://lrclib.net" : e.TrimEnd('/'); }
        }
        private static HttpClient Http(string api)
        {
            if (http != null) return http;
            bool local = api.StartsWith("http://127.0.0.1", StringComparison.Ordinal);   // (las pruebas: sin el proxy del entorno)
            SocketsHttpHandler h = new SocketsHttpHandler { UseProxy = !local, ConnectTimeout = TimeSpan.FromSeconds(6), AutomaticDecompression = DecompressionMethods.All };
            HttpClient c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(10) };
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            http = c;
            return c;
        }
        // LRCLIB: la sincronizada (o null), y en sinTiempos la que no trae tiempos, si la dio
        // contesto: LRCLIB dijo algo (200 o un 4xx que no es 429); false si no se pregunto (el freno) o no contesto (sin red, 5xx, 429)
        private static RocolaOsLetra DeLrclib(Pedido p, out string sinTiempos, out bool contesto)
        {
            sinTiempos = null; contesto = true;
            if (string.IsNullOrWhiteSpace(p.Titulo) || string.IsNullOrWhiteSpace(p.Artista)) return null;
            if (Environment.TickCount64 < Interlocked.Read(ref frenoHasta)) { contesto = false; return null; }   // (la red fallo hace poco: nada)
            string api = Api;
            StringBuilder u = new StringBuilder(api).Append("/api/get?track_name=").Append(Uri.EscapeDataString(p.Titulo.Trim()))
                .Append("&artist_name=").Append(Uri.EscapeDataString(p.Artista.Trim()));
            bool completa = !string.IsNullOrWhiteSpace(p.Disco) && p.Duracion > 0;
            if (!string.IsNullOrWhiteSpace(p.Disco)) u.Append("&album_name=").Append(Uri.EscapeDataString(p.Disco.Trim()));
            if (p.Duracion > 0) u.Append("&duration=").Append(p.Duracion.ToString(CultureInfo.InvariantCulture));
            int cod;
            string cuerpo = Pide(api, u.ToString(), out cod);
            if (cuerpo == null && cod == 0) { contesto = false; return null; }
            // sin disco o sin duracion, /api/get puede no encontrarla (o no aceptarla: 400): una busqueda por titulo y artista
            if (cuerpo == null && (cod == 400 || (cod == 404 && !completa)))
            {
                StringBuilder b = new StringBuilder(api).Append("/api/search?track_name=").Append(Uri.EscapeDataString(p.Titulo.Trim()))
                    .Append("&artist_name=").Append(Uri.EscapeDataString(p.Artista.Trim()));
                string lista = Pide(api, b.ToString(), out cod);
                if (lista == null) { contesto = cod != 0; return null; }
                cuerpo = DeLaLista(lista, p.Duracion);
                if (cuerpo == null) return null;   // (el 404 o 400 de antes ya conto como fallida)
            }
            if (cuerpo == null) return null;
            try
            {
                using (JsonDocument d = JsonDocument.Parse(cuerpo))
                {
                    JsonElement s;
                    string lrc = d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("syncedLyrics", out s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                    if (d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("plainLyrics", out s) && s.ValueKind == JsonValueKind.String) sinTiempos = s.GetString();
                    RocolaOsLetra l = DeLrc(p.Ruta, "LRCLIB", lrc);
                    if (l == null && sinTiempos == null) Interlocked.Increment(ref fallidas);
                    return l;
                }
            }
            catch (Exception ex) { Interlocked.Increment(ref fallidas); RocolaOs.Apunta("letra_cielo", "LRCLIB contesto algo raro (" + ex.GetType().Name + ")"); return null; }
        }
        // UNA PETICION: el cuerpo (200) o null; cod = el codigo, 0 si no contesto (y entonces, 5xx y 429 tambien, el freno de 2 minutos)
        private static string Pide(string api, string url, out int cod)
        {
            cod = 0;
            try
            {
                Interlocked.Increment(ref peticiones);
                using (HttpResponseMessage r = Http(api).Send(new HttpRequestMessage(HttpMethod.Get, url)))
                {
                    int c = (int)r.StatusCode;
                    if (c != 200)
                    {
                        Interlocked.Increment(ref fallidas);
                        if (c != 404) RocolaOs.Apunta("letra_cielo", "LRCLIB contesto " + c.ToString(CultureInfo.InvariantCulture));
                        if (c >= 500 || c == 429) { Interlocked.Exchange(ref frenoHasta, Environment.TickCount64 + FrenoRedMs); return null; }
                        cod = c;
                        return null;
                    }
                    cod = 200;
                    using (StreamReader sr = new StreamReader(r.Content.ReadAsStream(), Encoding.UTF8)) return sr.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref fallidas);
                Interlocked.Exchange(ref frenoHasta, Environment.TickCount64 + FrenoRedMs);
                RocolaOs.Apunta("letra_cielo", "LRCLIB no contesto (" + ex.GetType().Name + "): nada en 2 minutos");
                return null;
            }
        }
        // DE LO QUE DIO /api/search (una lista): la de la duracion mas cercana (a 3 s o menos si se sabe), mejor si es sincronizada; su JSON o null
        private static string DeLaLista(string lista, int duracion)
        {
            try
            {
                using (JsonDocument d = JsonDocument.Parse(lista))
                {
                    if (d.RootElement.ValueKind != JsonValueKind.Array) return null;
                    string mejor = null; double puntos = double.MaxValue;
                    foreach (JsonElement e in d.RootElement.EnumerateArray())
                    {
                        if (e.ValueKind != JsonValueKind.Object) continue;
                        JsonElement v;
                        bool sinc = e.TryGetProperty("syncedLyrics", out v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString());
                        bool plana = e.TryGetProperty("plainLyrics", out v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString());
                        if (!sinc && !plana) continue;
                        double dur = e.TryGetProperty("duration", out v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
                        double dif = duracion > 0 && dur > 0 ? Math.Abs(dur - duracion) : 0;
                        if (duracion > 0 && dur > 0 && dif > 3) continue;
                        double pts = dif + (sinc ? 0 : 100);
                        if (pts < puntos) { puntos = pts; mejor = e.GetRawText(); }
                    }
                    return mejor;
                }
            }
            catch { return null; }
        }

        // ---------- leer una letra ----------
        // UNA LRC: «[mm:ss.xx]texto» (uno o varios tiempos por renglon; [offset:+/-ms]; las demas etiquetas no cuentan). null si no tiene tiempos.
        internal static RocolaOsLetra DeLrc(string ruta, string proveedor, string lrc)
        {
            if (string.IsNullOrWhiteSpace(lrc)) return null;
            List<KeyValuePair<double, string>> l = new List<KeyValuePair<double, string>>();
            double offset = 0;
            foreach (string crudo in lrc.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string r = crudo.Trim();
                List<double> ts = new List<double>();
                int i = 0;
                while (i < r.Length && r[i] == '[')
                {
                    int f = r.IndexOf(']', i);
                    if (f < 0) break;
                    string et = r.Substring(i + 1, f - i - 1);
                    double t;
                    if (Tiempo(et, out t)) ts.Add(t);
                    else if (et.StartsWith("offset:", StringComparison.OrdinalIgnoreCase))
                    {
                        double ms;
                        if (double.TryParse(et.Substring(7).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out ms)) offset = ms / 1000.0;
                    }
                    i = f + 1;
                }
                if (ts.Count == 0) continue;
                string texto = r.Substring(i).Trim();
                foreach (double t in ts) l.Add(new KeyValuePair<double, string>(t, texto));
            }
            if (l.Count == 0) return null;
            // (orden estable por tiempo)
            KeyValuePair<double, string>[] a = l.ToArray();
            int[] idx = new int[a.Length]; for (int k = 0; k < idx.Length; k++) idx[k] = k;
            Array.Sort(idx, (x, y) => a[x].Key != a[y].Key ? a[x].Key.CompareTo(a[y].Key) : x.CompareTo(y));
            double[] tiempos = new double[a.Length]; string[] versos = new string[a.Length];
            for (int k = 0; k < idx.Length; k++) { tiempos[k] = Math.Max(0, a[idx[k]].Key - offset); versos[k] = a[idx[k]].Value; }
            return new RocolaOsLetra(ruta, proveedor, true, tiempos, versos);
        }
        // «mm:ss», «mm:ss.xx», «mm:ss.xxx» o «mm:ss:xx»
        private static bool Tiempo(string s, out double t)
        {
            t = 0;
            int c = s.IndexOf(':');
            if (c <= 0) return false;
            int mm;
            if (!int.TryParse(s.Substring(0, c), NumberStyles.None, CultureInfo.InvariantCulture, out mm)) return false;
            string resto = s.Substring(c + 1);
            int c2 = resto.IndexOf(':');
            if (c2 >= 0) resto = resto.Substring(0, c2) + "." + resto.Substring(c2 + 1);
            double ss;
            if (resto.Length == 0 || !char.IsDigit(resto[0]) || !double.TryParse(resto, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out ss) || ss >= 60) return false;
            t = mm * 60 + ss;
            return true;
        }
        // UNA SIN TIEMPOS: sus renglones (null si no tiene texto)
        internal static RocolaOsLetra DeTexto(string ruta, string proveedor, bool _, string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            List<string> v = new List<string>();
            foreach (string r in texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')) v.Add(r.Trim());
            while (v.Count > 0 && v[v.Count - 1].Length == 0) v.RemoveAt(v.Count - 1);
            while (v.Count > 0 && v[0].Length == 0) v.RemoveAt(0);
            return v.Count == 0 ? null : new RocolaOsLetra(ruta, proveedor, false, null, v.ToArray());
        }

        // ---------- /run (lo vivo: se va al apagar) ----------
        private static string RutaRun(string ruta)
        {
            ulong h = 14695981039346656037UL; foreach (char ch in ruta) { h ^= ch; h *= 1099511628211UL; }
            return Path.Combine(RocolaOs.Run("rocola-xp/letras"), h.ToString("x16", CultureInfo.InvariantCulture) + ".lrc");
        }
        // el archivo: «proveedor TAB 1|0 TAB ruta» y luego la letra (LRC con sus tiempos, o el texto)
        private static void ARun(RocolaOsLetra l, string ruta)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(l.Proveedor).Append('\t').Append(l.Sincronizada ? "1" : "0").Append('\t').Append(ruta.Replace('\n', ' ')).Append('\n');
                for (int i = 0; i < l.Versos.Length; i++)
                {
                    if (l.Sincronizada)
                    {
                        double t = l.Tiempos[i]; int mm = (int)(t / 60); double ss = t - mm * 60;
                        sb.Append('[').Append(mm.ToString("00", CultureInfo.InvariantCulture)).Append(':').Append(ss.ToString("00.000", CultureInfo.InvariantCulture)).Append(']');
                    }
                    sb.Append(l.Versos[i]).Append('\n');
                }
                string f = RutaRun(ruta), d = Path.GetDirectoryName(f);
                Directory.CreateDirectory(d);
                File.WriteAllText(f + ".t", sb.ToString(), new UTF8Encoding(false));
                File.Move(f + ".t", f, true);
                RecortaRun(d, f);
            }
            catch { }
        }
        // EL TOPE DE /run: una rocola prendida meses no llena la memoria con letras (se quitan las de fecha mas vieja; nunca la que se acaba de poner)
        private static void RecortaRun(string d, string puesta)
        {
            try
            {
                string[] fs = Directory.GetFiles(d, "*.lrc");
                if (fs.Length <= RunMax) return;
                Array.Sort(fs, delegate (string a, string b) { return File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)); });
                for (int i = 0, sobran = fs.Length - RunQueda; i < fs.Length && sobran > 0; i++)
                {
                    if (string.Equals(fs[i], puesta, StringComparison.Ordinal)) continue;
                    try { File.Delete(fs[i]); sobran--; } catch { }
                }
            }
            catch { }
        }
        private static RocolaOsLetra DeRun(string ruta)
        {
            try
            {
                string f = RutaRun(ruta);
                if (!File.Exists(f)) return null;
                string s = File.ReadAllText(f, Encoding.UTF8);
                int n = s.IndexOf('\n');
                if (n < 0) return null;
                string[] c = s.Substring(0, n).Split('\t');
                if (c.Length < 3 || c[2] != ruta.Replace('\n', ' ')) return null;   // (otra cancion con el mismo resumen: no)
                try { File.SetLastWriteTimeUtc(f, DateTime.UtcNow); } catch { }   // (usada: el recorte de /run quita las menos usadas)
                string cuerpo = s.Substring(n + 1);
                return c[1] == "1" ? DeLrc(ruta, c[0], cuerpo) : DeTexto(ruta, c[0], false, cuerpo);
            }
            catch { return null; }
        }

        // ---------- la orden de prueba ----------
        // rocola-xp --prueba-letra-cielo TITULO ARTISTA DISCO DURACION MP3|- POS... : busca la letra como el hilo (LRCLIB con ROCOLA_LETRA_API,
        // el MP3, /run con ROCOLA_RUN) y dice, sin la letra entera, el proveedor y el verso de cada posicion; con «otra», busca otra vez; con
        // «cancion=TITULO», otra vuelta con otra cancion (sin MP3, el mismo artista, disco y duracion); con «pausa=MS», espera antes de la siguiente
        internal static int Prueba(string[] a)
        {
            if (a.Length < 5) { Console.Error.WriteLine("uso: --prueba-letra-cielo TITULO ARTISTA DISCO DURACION MP3|- [POS...] [otra] [cancion=TITULO] [pausa=MS]"); return 2; }
            int dur; int.TryParse(a[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out dur);
            Pedido p = new Pedido { Titulo = a[0], Artista = a[1], Disco = a[2], Duracion = dur, Mp3 = a[4] == "-" ? null : a[4] };
            p.Ruta = p.Mp3 ?? ("prueba:" + a[0] + "|" + a[1]);
            List<Pedido> vueltas = new List<Pedido> { p };
            List<int> pausas = new List<int> { 0 };
            List<double> pos = new List<double>();
            for (int i = 5; i < a.Length; i++)
            {
                if (a[i] == "otra") { vueltas.Add(p); pausas.Add(0); continue; }
                if (a[i].StartsWith("cancion=", StringComparison.Ordinal))
                {
                    string t = a[i].Substring(8);
                    vueltas.Add(new Pedido { Titulo = t, Artista = a[1], Disco = a[2], Duracion = dur, Ruta = "prueba:" + t + "|" + a[1] }); pausas.Add(0); continue;
                }
                int ms;
                if (a[i].StartsWith("pausa=", StringComparison.Ordinal) && int.TryParse(a[i].Substring(6), NumberStyles.None, CultureInfo.InvariantCulture, out ms)) { pausas[pausas.Count - 1] += ms; continue; }
                double x; if (double.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) pos.Add(x);
            }
            for (int v = 0; v < vueltas.Count; v++)
            {
                if (v > 0 && pausas[v - 1] > 0) Thread.Sleep(pausas[v - 1]);
                RocolaOsLetra l = Busca(vueltas[v]);
                Console.WriteLine("vuelta=" + (v + 1).ToString(CultureInfo.InvariantCulture) + " letra=" + (l == null ? "ninguna" : l.Sincronizada ? "sincronizada" : "sin_tiempos")
                    + " proveedor=" + (l != null ? l.Proveedor : "") + " versos=" + (l != null ? l.Versos.Length : 0).ToString(CultureInfo.InvariantCulture)
                    + " peticiones=" + Peticiones.ToString(CultureInfo.InvariantCulture) + " fallidas=" + Fallidas.ToString(CultureInfo.InvariantCulture) + " cancion=" + vueltas[v].Titulo);
                if (l == null || v > 0) continue;
                foreach (double x in pos)
                {
                    RocolaOsLetraVerso vv = new RocolaOsLetraVerso(l, x);
                    Console.WriteLine("pos=" + x.ToString("0.###", CultureInfo.InvariantCulture) + " verso=" + vv.Indice.ToString(CultureInfo.InvariantCulture) + " «" + (vv.Actual ?? "") + "»"
                        + " siguiente=" + vv.Siguiente.ToString(CultureInfo.InvariantCulture) + " «" + (vv.DespuesDice ?? "") + "»");
                }
            }
            return 0;
        }
    }

    public partial class Form1
    {
        private string rocolaOsLetraRuta;            // lo que suena (su ruta), visto por el latido
        private long rocolaOsLetraDesdeMs;
        private bool rocolaOsLetraPedida;

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraLetraCielo()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "letra_cielo",
                Que = "la letra de lo que suena en el cielo del reposo (LRCLIB, o la del MP3): se pide y se dibuja (RocolaOs.LetraCielo.cs)",
                DeFabrica = true,
                Latido = f => f.RocolaOsLetraLatido(),
                Vivo = (f, sb) =>
                {
                    RocolaOsLetra l = RocolaOsLetraSuena.Actual; RocolaOsLetraVerso v = RocolaOsLetraSuena.Verso;
                    sb.Append("letra_cielo=").Append(l == null ? "ninguna" : (l.Sincronizada ? "sincronizada " : "sin_tiempos ") + (l.Proveedor == "LRCLIB" ? "LRCLIB" : "MP3"))
                      .Append(" verso=").Append(v != null ? v.Indice.ToString(CultureInfo.InvariantCulture) : "-1")
                      .Append(" peticiones=").Append(RocolaOsLetraSuena.Peticiones.ToString(CultureInfo.InvariantCulture)).Append('\n');
                },
                ParaAlCerrar = r => RocolaOsLetraSuena.Para(),
            });
            RocolaOsOrdenes.Si("--prueba-letra-cielo", RocolaOsLetraSuena.Prueba);
        }

        // EL LATIDO (1 s, en la pantalla): si empezo a sonar otra cancion, se pide su letra (con su duracion, en cuanto se sepa); y el verso de ahora
        private void RocolaOsLetraLatido()
        {
            PlayItem np = nowPlaying;
            if (np == null || np.IsVideo || np.Song == null || string.IsNullOrEmpty(np.Song.Path))
            {
                if (rocolaOsLetraRuta != null) { rocolaOsLetraRuta = null; RocolaOsLetraSuena.Quita(); }
                return;
            }
            string ruta = np.Song.Path;
            long ahora = Environment.TickCount64;
            if (ruta != rocolaOsLetraRuta) { rocolaOsLetraRuta = ruta; rocolaOsLetraDesdeMs = ahora; rocolaOsLetraPedida = false; RocolaOsLetraSuena.Quita(); }
            if (!rocolaOsLetraPedida)
            {
                double total = 0;
                try { IAudioSource r = reader; if (r != null) total = r.TotalTime.TotalSeconds; } catch { }
                if (total < 1 && ahora - rocolaOsLetraDesdeMs < 5000) return;   // (la duracion aun no se sabe: hasta 5 s, y si no, sin ella)
                rocolaOsLetraPedida = true;
                Track t = np.Song;
                string artista = !string.IsNullOrEmpty(t.Artist) ? t.Artist : np.Disc != null ? np.Disc.Artist ?? "" : "";
                string disco = !string.IsNullOrEmpty(t.Album) ? t.Album : np.Disc != null && !RocolaOsEsSpotify(np.Disc) ? np.Disc.Name ?? "" : "";
                if (disco.Length == 0 && np.Disc != null && RocolaOsEsSpotify(np.Disc)) disco = np.Disc.Name ?? "";
                bool mp3 = ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) && !ruta.StartsWith("spotify:", StringComparison.Ordinal);
                RocolaOsLetraSuena.Pide(new RocolaOsLetraSuena.Pedido
                {
                    Ruta = ruta, Titulo = TituloSinPista(t.Title ?? ""), Artista = artista, Disco = disco,
                    Duracion = total >= 1 ? (int)Math.Round(total) : 0, Mp3 = mp3 ? ruta : null,
                });
            }
            RocolaOsLetraSuena.Mira(ruta, PosicionActual());
        }
    }
}

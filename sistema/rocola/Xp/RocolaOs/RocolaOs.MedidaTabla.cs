// LA MEDIDA NUEVA: LA TABLA, LA MEDIDA AL ENTRAR A LA COLA, EL REPASO DE FONDO Y LA MEDIDA EN VIVO (cadena de sonido nueva, CADENA_NUEVA.md
// §1.2 y §5; el medidor, RocolaOs.Medida.cs; los datos, RocolaOs.MedidaNueva.cs).
//
// LA TABLA: rocola_medida_nueva.txt en la carpeta de datos (aparte de las tablas de XP: nunca se escriben las de XP). Un renglon por cancion y
// cadena, con el SELLO del archivo como rocola_fuentes.txt (largo, creada, escrita: si el archivo cambia, lo medido ya no vale):
//   ROCOLA-MEDIDA-NUEVA v1
//   cadena TAB ruta TAB largo TAB creada TAB escrita TAB algoritmo TAB huellaB TAB segundos TAB lufs TAB dbtp TAB pico TAB lra TAB mudo TAB
//   invertido TAB continuaL TAB continuaR TAB zumbido TAB tercios (30, en decimas de dB, con «;») TAB unix TAB picos del EQ del dueno (10, en
//   decimas de dBFS, con «;») TAB corte de agudos (Hz). (Los dos ultimos desde el 28-11-2026: 21 campos; un renglon de 19 se lee sin ellos.)
// Solo numeros y la ruta (o «spotify:track:ID»). Se anaden renglones; al leerla, si sobra mas de un tercio, se reescribe con el ultimo de cada
// una (con EscribeSeguro de XP). Algoritmo 0 = solo los arreglos (medida en vivo que pidio un arreglo: la siguiente vez se mide con el puesto).
//
// QUIEN MIDE (solo si la cadena nueva suena, o si alguien la pide: Pedida):
//   - LA COLA: cada segundo el latido mira lo que suena y la cola; una cancion tuya sin medida valida se mide ya (hilo «medida_nueva-cola», por
//     debajo de la musica y de la pantalla: la prioridad por debajo de lo normal de XP), por delante del repaso. Si no da tiempo, suena con la
//     estimacion conservadora (RocolaOs.CadenaNuevaGanancia.cs), fija toda la cancion, y la siguiente vez ya va medida.
//   - EL REPASO DE FONDO (hilo «medida_nueva-fondo», nice 19: por debajo del repaso de XP): toda la biblioteca, las mas pedidas primero
//     (songCounts de XP, y a igualdad las de hace menos), 50 ms de pausa entre una y otra.
//   - EN VIVO (Spotify, o una que sono sin medida): RocolaOsMedidorVivo; si sono entera (97 % y 30 s o mas, desde el principio), se guarda.
// Cada medida: la cancion entera decodificada en coma flotante y pasada por la cadena (A, o B con las piezas del billar) a ganancia 1 y sin el
// ecualizador del dueno; si pide un arreglo (canal mudo, invertido, continua), otra pasada con el arreglo puesto.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal static class RocolaOsMedidaTabla
    {
        internal const string Archivo = "rocola_medida_nueva.txt";
        internal const string Cabecera = "ROCOLA-MEDIDA-NUEVA v1";
        private static readonly ConcurrentDictionary<string, RocolaOsMedidaNueva> tabla = new ConcurrentDictionary<string, RocolaOsMedidaNueva>(StringComparer.OrdinalIgnoreCase);
        private static volatile bool cargada;
        internal static bool Cargada { get { return cargada; } }
        internal static int Cuantas { get { return tabla.Count; } }
        internal static int Version;   // sube con cada medida nueva (lo tipico de la biblioteca se rehace)
        private static string Clave(string ruta, char cadena) { return cadena + "|" + ruta; }
        internal static string Ruta { get { return RocolaOs.Datos(Archivo); } }

        // LO MEDIDO de esa cancion con esa cadena, sin mirar el archivo (el disco: las demas pistas)
        internal static RocolaOsMedidaNueva Busca(string ruta, char cadena)
        {
            if (ruta == null) return null;
            RocolaOsMedidaNueva m;
            if (!tabla.TryGetValue(Clave(ruta, cadena), out m)) return null;
            if (cadena == 'B' && m.HuellaB != RocolaOsCadenaB.HuellaAhora) return null;
            return m;
        }
        // LO MEDIDO QUE VALE: el mismo archivo (su sello) y, con la B, sus mismas piezas
        internal static RocolaOsMedidaNueva Valida(string ruta, char cadena)
        {
            RocolaOsMedidaNueva m = Busca(ruta, cadena);
            if (m == null) return null;
            if (ruta.StartsWith("spotify:", StringComparison.Ordinal)) return m;
            long l, c, e;
            if (!Sello(ruta, out l, out c, out e) || l != m.Largo || c != m.Creada || e != m.Escrita) return null;
            return m;
        }
        internal static bool Sello(string ruta, out long largo, out long creada, out long escrita)
        {
            largo = creada = escrita = 0;
            try { FileInfo f = new FileInfo(ruta); if (!f.Exists) return false; largo = f.Length; creada = f.CreationTimeUtc.Ticks; escrita = f.LastWriteTimeUtc.Ticks; return true; }
            catch { return false; }
        }
        // todas las medidas buenas de esa cadena (lo tipico de la biblioteca: RocolaOsPicosEq.Tipico)
        internal static IEnumerable<RocolaOsMedidaNueva> Todas(char cadena)
        {
            foreach (KeyValuePair<string, RocolaOsMedidaNueva> kv in tabla)
                if (kv.Value.Cadena == cadena && kv.Value.Alg == RocolaOsMedidaNueva.Algoritmo && !kv.Value.Muda) yield return kv.Value;
        }
        internal static IEnumerable<RocolaOsMedidaNueva> DeSpotify(char cadena)
        {
            foreach (KeyValuePair<string, RocolaOsMedidaNueva> kv in tabla)
                if (kv.Value.Cadena == cadena && kv.Value.Alg == RocolaOsMedidaNueva.Algoritmo && kv.Value.Ruta != null && kv.Value.Ruta.StartsWith("spotify:", StringComparison.Ordinal))
                    yield return kv.Value;
        }

        // ---------- leer y escribir ----------
        internal static void Carga()
        {
            int lineas = 0;
            try
            {
                string[] ls = File.Exists(Ruta) ? Form1.RocolaOsLeeLineas(Ruta) : null;
                if (ls != null)
                    foreach (string l in ls)
                    {
                        if (string.IsNullOrEmpty(l) || l[0] == '#' || l == Cabecera) continue;
                        lineas++;
                        RocolaOsMedidaNueva m = DeRenglon(l);
                        if (m != null) tabla[Clave(m.Ruta, m.Cadena)] = m;
                    }
                if (lineas - tabla.Count >= 200 && lineas > tabla.Count * 3 / 2)
                {
                    StringBuilder sb = new StringBuilder(Cabecera + "\n");
                    foreach (RocolaOsMedidaNueva m in tabla.Values) Renglon(sb, m);
                    Form1.RocolaOsEscribeSeguro(Ruta, sb.ToString());
                    RocolaOs.Apunta(RocolaOsCadenasNuevas.Medida, Archivo + " compactada: " + lineas + " renglones a " + tabla.Count);
                }
            }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Medida, "leer " + Archivo, ex); }
            Interlocked.Increment(ref Version);
            cargada = true;
        }
        private static readonly object cerrojoEscribe = new object();
        internal static void Pon(RocolaOsMedidaNueva m)
        {
            if (m == null || m.Ruta == null) return;
            tabla[Clave(m.Ruta, m.Cadena)] = m;
            Interlocked.Increment(ref Version);
            StringBuilder sb = new StringBuilder();
            lock (cerrojoEscribe)
            {
                try
                {
                    if (!File.Exists(Ruta)) sb.Append(Cabecera).Append('\n');
                    Renglon(sb, m);
                    File.AppendAllText(Ruta, sb.ToString(), new UTF8Encoding(false));
                }
                catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Medida, "escribir " + Archivo, ex); }
            }
        }
        private static string N(double v, string f) { return double.IsNegativeInfinity(v) ? "-inf" : v.ToString(f, CultureInfo.InvariantCulture); }
        internal static void Renglon(StringBuilder sb, RocolaOsMedidaNueva m)
        {
            sb.Append(m.Cadena).Append('\t').Append(m.Ruta.Replace('\t', ' ').Replace('\n', ' ')).Append('\t')
              .Append(m.Largo.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(m.Creada.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(m.Escrita.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(m.Alg.ToString(CultureInfo.InvariantCulture)).Append('\t')
              .Append(string.IsNullOrEmpty(m.HuellaB) ? "-" : m.HuellaB).Append('\t')
              .Append(N(m.Segundos, "0.00")).Append('\t').Append(N(m.Lufs, "0.00")).Append('\t').Append(N(m.TpDb, "0.00")).Append('\t')
              .Append(N(m.PicoDb, "0.00")).Append('\t').Append(N(m.Lra, "0.0")).Append('\t')
              .Append(m.CanalMudo.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(m.Invertido ? "1" : "0").Append('\t')
              .Append(N(m.ContinuaL, "0.000000")).Append('\t').Append(N(m.ContinuaR, "0.000000")).Append('\t')
              .Append(m.ZumbidoHz.ToString(CultureInfo.InvariantCulture)).Append('\t');
            if (m.Tercios == null) sb.Append('-');
            else for (int b = 0; b < m.Tercios.Length; b++) { if (b > 0) sb.Append(';'); sb.Append(((int)Math.Round(m.Tercios[b] * 10)).ToString(CultureInfo.InvariantCulture)); }
            sb.Append('\t').Append(m.Unix.ToString(CultureInfo.InvariantCulture)).Append('\t');
            if (m.PicosEq == null) sb.Append('-');
            else for (int b = 0; b < m.PicosEq.Length; b++) { if (b > 0) sb.Append(';'); sb.Append(((int)Math.Round(m.PicosEq[b] * 10)).ToString(CultureInfo.InvariantCulture)); }
            sb.Append('\t').Append(((int)Math.Round(m.CorteHz)).ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        private static double D(string s) { if (s == "-inf") return double.NegativeInfinity; double v; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; }
        private static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
        internal static RocolaOsMedidaNueva DeRenglon(string l)
        {
            string[] c = l.Split('\t');
            if (c.Length < 19 || c[0].Length != 1 || (c[0][0] != 'A' && c[0][0] != 'B') || c[1].Length == 0) return null;
            RocolaOsMedidaNueva m = new RocolaOsMedidaNueva();
            m.Cadena = c[0][0]; m.Ruta = c[1]; m.Largo = L(c[2]); m.Creada = L(c[3]); m.Escrita = L(c[4]); m.Alg = (int)L(c[5]); m.HuellaB = c[6];
            m.Segundos = D(c[7]); m.Lufs = D(c[8]); m.TpDb = D(c[9]); m.PicoDb = D(c[10]); m.Lra = D(c[11]);
            m.CanalMudo = (int)L(c[12]); m.Invertido = c[13] == "1"; m.ContinuaL = (float)D(c[14]); m.ContinuaR = (float)D(c[15]); m.ZumbidoHz = (int)L(c[16]);
            if (c[17] != "-")
            {
                string[] t = c[17].Split(';');
                if (t.Length == RocolaOsMedidaNueva.Bandas) { m.Tercios = new float[t.Length]; for (int b = 0; b < t.Length; b++) m.Tercios[b] = L(t[b]) / 10f; }
            }
            m.Unix = L(c[18]);
            if (c.Length >= 21)
            {   // (28-11-2026) los picos de las sondas del EQ del dueno y el corte de agudos; un renglon de antes (19 campos) va sin ellos
                if (c[19] != "-") { string[] t = c[19].Split(';'); if (t.Length == EcualizadorDueno.Bandas) { m.PicosEq = new float[t.Length]; for (int b = 0; b < t.Length; b++) m.PicosEq[b] = L(t[b]) / 10f; } }
                m.CorteHz = Math.Max(0f, (float)L(c[20]));
            }
            if (double.IsNaN(m.Lufs) || double.IsNaN(m.TpDb) || float.IsNaN(m.ContinuaL) || float.IsNaN(m.ContinuaR)) return null;
            if (m.CanalMudo < -1 || m.CanalMudo > 1) m.CanalMudo = -1;
            return m;
        }
    }

    // LOS DOS HILOS DE LA MEDIDA (la cola y el fondo)
    internal static class RocolaOsMedidaTrabajo
    {
        internal const int PausaFondoMs = 50;
        internal sealed class Pedido { internal string Ruta; internal char Cadena; internal int ZumbidoHz; internal RocolaOsMedidaNueva Arreglos; }
        private static readonly ConcurrentQueue<Pedido> cola = new ConcurrentQueue<Pedido>();
        private static List<Pedido> fondo = new List<Pedido>();
        private static int fondoI;
        private static readonly object cerrojo = new object();
        private static readonly HashSet<string> enCurso = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly AutoResetEvent hayCola = new AutoResetEvent(false), hayFondo = new AutoResetEvent(false);
        private static volatile bool parar, arrancado;
        internal static volatile bool Pedida;               // (la comparacion ciega: medir aunque la cadena nueva no suene)
        internal static long Hechas, EnVivo, Fallidas;
        internal static double UltimaMs;
        internal static int EnCola { get { return cola.Count; } }
        internal static int Pendientes { get { lock (cerrojo) return Math.Max(0, fondo.Count - fondoI); } }
        internal static bool Trabaja { get { return RocolaOsCadenasNuevas.Suena || Pedida; } }
        internal static readonly ConcurrentQueue<RocolaOsMedidaNueva> Vivas = new ConcurrentQueue<RocolaOsMedidaNueva>();

        internal static void Arranca()
        {
            if (arrancado) return;
            arrancado = true;
            RocolaOs.Hilo(RocolaOsCadenasNuevas.Medida + "-fondo", Fondo);
            RocolaOs.Hilo(RocolaOsCadenasNuevas.Medida + "-cola", ThreadPriority.BelowNormal, Cola);
        }
        internal static void Para() { parar = true; hayCola.Set(); hayFondo.Set(); }
        internal static void Despierta() { hayCola.Set(); }
        internal static void PideYa(Pedido p) { if (p != null && p.Ruta != null) { cola.Enqueue(p); hayCola.Set(); } }
        internal static void Repasa(List<Pedido> lista) { lock (cerrojo) { fondo = lista; fondoI = 0; } hayFondo.Set(); }
        internal static bool EnCurso(string ruta, char cadena) { lock (enCurso) return enCurso.Contains(cadena + "|" + ruta); }

        private static void Cola()
        {
            if (!RocolaOsMedidaTabla.Cargada) RocolaOsMedidaTabla.Carga();
            while (!parar)
            {
                Pedido p;
                bool algo = false;
                while (!parar && cola.TryDequeue(out p)) { algo = true; Uno(p, true); }
                RocolaOsMedidaNueva v;
                while (Vivas.TryDequeue(out v)) { RocolaOsMedidaTabla.Pon(v); algo = true; }
                if (!algo) hayCola.WaitOne(2000);
            }
        }
        private static void Fondo()
        {
            if (!RocolaOsMedidaTabla.Cargada) RocolaOsMedidaTabla.Carga();
            while (!parar)
            {
                Pedido p = null;
                if (Trabaja)
                    lock (cerrojo) { if (fondoI < fondo.Count) p = fondo[fondoI++]; }
                if (p == null) { hayFondo.WaitOne(30000); continue; }
                if (!cola.IsEmpty) { lock (cerrojo) fondoI--; Thread.Sleep(200); continue; }   // (la cola va primero)
                if (Uno(p, false)) Thread.Sleep(PausaFondoMs);
            }
        }
        // mide uno (si no esta ya medido ni midiendose); true si se midio
        private static bool Uno(Pedido p, bool deLaCola)
        {
            if (RocolaOsMedidaTabla.Valida(p.Ruta, p.Cadena) is RocolaOsMedidaNueva ya && ya.Alg == RocolaOsMedidaNueva.Algoritmo) return false;
            string k = p.Cadena + "|" + p.Ruta;
            lock (enCurso) { if (!enCurso.Add(k)) return false; }
            try
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                RocolaOsMedidaNueva m = Form1.RocolaOsMideCancion(p.Ruta, p.Cadena, p.ZumbidoHz, p.Arreglos, delegate { return parar; });
                UltimaMs = RocolaOs.Ms(t0);
                if (m == null) { Interlocked.Increment(ref Fallidas); return false; }
                RocolaOsMedidaTabla.Pon(m);
                Interlocked.Increment(ref Hechas);
                if (deLaCola) RocolaOs.Apunta(RocolaOsCadenasNuevas.Medida, "medida al entrar a la cola en " + UltimaMs.ToString("0", CultureInfo.InvariantCulture) + " ms: " + Path.GetFileName(p.Ruta) + ": " + m.Resumen());
                return true;
            }
            catch (Exception ex) { Interlocked.Increment(ref Fallidas); RocolaOs.Falla(RocolaOsCadenasNuevas.Medida, "medir " + Path.GetFileName(p.Ruta), ex); return false; }
            finally { lock (enCurso) enCurso.Remove(k); }
        }
    }

    public partial class Form1
    {
        // ---------------------------------------------------------------- medir una cancion entera (hilo de la medida: nada del estado de XP)
        internal const int RocolaOsMideBloque = 8192;
        internal static RocolaOsMedidaNueva RocolaOsMideCancion(string ruta, char cadena, int zumbidoHz, RocolaOsMedidaNueva arreglos, Func<bool> cancela)
        {
            long largo, creada, escrita;
            if (!RocolaOsMedidaTabla.Sello(ruta, out largo, out creada, out escrita)) return null;
            RocolaOsMedidaNueva previa = arreglos;
            RocolaOsMedidaNueva m = RocolaOsUnaPasada(ruta, cadena, zumbidoHz, previa, true, cancela);
            if (m == null) return null;
            // ¿pide arreglos? (solo si no se le pusieron: lo crudo se mide sin ellos) -> otra pasada con ellos puestos
            if (previa == null && (m.CanalMudo >= 0 || m.Invertido || m.ContinuaL != 0f || m.ContinuaR != 0f))
            {
                RocolaOsMedidaNueva otra = RocolaOsUnaPasada(ruta, cadena, zumbidoHz, m, false, cancela);
                if (otra == null) return null;
                otra.CanalMudo = m.CanalMudo; otra.Invertido = m.Invertido; otra.ContinuaL = m.ContinuaL; otra.ContinuaR = m.ContinuaR;
                m = otra;
            }
            long l2, c2, e2;
            if (!RocolaOsMedidaTabla.Sello(ruta, out l2, out c2, out e2) || l2 != largo || c2 != creada || e2 != escrita) return null;   // cambio mientras se media
            m.Ruta = ruta; m.Cadena = cadena; m.Largo = largo; m.Creada = creada; m.Escrita = escrita; m.Alg = RocolaOsMedidaNueva.Algoritmo;
            m.HuellaB = cadena == 'B' ? RocolaOsCadenaB.HuellaAhora : "-"; m.ZumbidoHz = zumbidoHz;
            m.Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return m;
        }

        // UNA PASADA: la cancion por la cadena (sin la ganancia ni el EQ del dueno) y el medidor. 'arreglos': los que se le ponen (null: ninguno);
        // con 'crudo', ademas lo crudo para decidir los arreglos (quedan en lo devuelto).
        internal static RocolaOsMedidaNueva RocolaOsUnaPasada(string ruta, char cadena, int zumbidoHz, RocolaOsMedidaNueva arreglos, bool crudo, Func<bool> cancela)
        {
            IAudioSource s = RocolaOsAbreParaMedir(ruta);
            if (s == null) return null;
            try
            {
                if (s.WaveFormat.Channels != 2) return null;
                RocolaOsReceta r = new RocolaOsReceta { Cadena = cadena, Ruta = ruta, Tasa = s.WaveFormat.SampleRate, Midiendo = true, ZumbidoHz = zumbidoHz };
                if (arreglos != null) { r.CanalMudo = arreglos.CanalMudo; r.Invertido = arreglos.Invertido; r.ContinuaL = arreglos.ContinuaL; r.ContinuaR = arreglos.ContinuaR; }
                RocolaOsPiezaEntrada ent = new RocolaOsPiezaEntrada(s, null, crudo);
                ISampleProvider x = RocolaOsNucleo(r, ent, zumbidoHz);
                RocolaOsMedidor med = new RocolaOsMedidor(r.Tasa);
                float[] b = new float[RocolaOsMideBloque];
                int n, vueltas = 0;
                while ((n = x.Read(b, 0, b.Length)) > 0)
                {
                    med.Procesa(b, 0, n);
                    if ((++vueltas & 15) == 0 && cancela != null && cancela()) return null;
                }
                RocolaOsMedidaNueva m = new RocolaOsMedidaNueva();
                med.Cierra(m);
                if (crudo && ent.Crudo != null) ent.Crudo.Arreglos(m);
                return m;
            }
            finally { try { s.Dispose(); } catch { } }
        }
        // LA CADENA SIN LA GANANCIA NI EL EQ DEL DUENO: lo que se mide (la misma de RocolaOsArmaNueva hasta antes del EQ del dueno, sin lo opcional)
        internal static ISampleProvider RocolaOsNucleo(RocolaOsReceta r, ISampleProvider x, int zumbidoHz)
        {
            if (zumbidoHz > 0) x = new QuitaZumbido(x, zumbidoHz, true);
            x = r.Anota(new RocolaOsArreglosFijos(x, r));
            if (r.Cadena == 'A') return r.Anota(new RocolaOsGravesCentro(x, RocolaOsCadenasNuevas.GravesCentroHz));
            Func<RocolaOsReceta, ISampleProvider, ISampleProvider> armaB = RocolaOsCadenaB.Arma;
            ISampleProvider b = null;
            if (armaB != null) { try { b = armaB(r, x); } catch (Exception ex) { RocolaOs.Falla(RocolaOsCadenasNuevas.Medida, "las piezas de la B al medir (de paso)", ex); } }
            return b ?? r.Anota(new RocolaOsPiezaDePaso(x));
        }
        // abrir para medir: un MP3 en coma flotante y sin huecos (sin la memoria del calentador); otro formato, el lector de XP; en dos canales
        private static IAudioSource RocolaOsAbreParaMedir(string ruta)
        {
            IAudioSource s = null;
            try
            {
                if (ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    if (NoEsMp3Rapido(ruta)) return null;
                    s = ComoEstereo(new RocolaOsFuenteFlotante(ruta, false, false));
                    string como;
                    s = RocolaOsSinHuecos(ruta, s, out como);
                }
                else s = ComoEstereo(new FileAudioSource(ruta));
                return s;
            }
            catch { if (s != null) { try { s.Dispose(); } catch { } } return null; }
        }
        private static bool NoEsMp3Rapido(string ruta) { try { NoEsMp3.Rechaza(ruta); return false; } catch { return true; } }

        // ---------------------------------------------------------------- en vivo (una que suena sin medida)
        private void RocolaOsMedidaVivoPrepara(RocolaOsArmada arm, string ruta, IAudioSource fuente, bool sombra)
        {
            RocolaOsReceta r = arm.Receta;
            char cadena = r.Cadena;
            bool spotify = ruta.StartsWith("spotify:", StringComparison.Ordinal);
            long largo = 0, creada = 0, escrita = 0;
            if (!spotify && !RocolaOsMedidaTabla.Sello(ruta, out largo, out creada, out escrita)) return;
            bool conArreglos = r.CanalMudo >= 0 || r.Invertido || r.ContinuaL != 0f || r.ContinuaR != 0f;
            int zumb = r.ZumbidoHz;
            RocolaOsPiezaEntrada ent = arm.Entrada;
            if (ent != null && ent.Crudo == null) ent.Crudo = new RocolaOsCrudo(r.Tasa);
            arm.Vivo.AlAcabar = delegate (RocolaOsMedidorVivo v)
            {
                // (hilo del sonido: se cierra aqui, que es poco, y se encola para escribir)
                double dura = 0; try { dura = fuente.TotalTime.TotalSeconds; } catch { }
                double sonado = v.Medidor.Cuadros / (double)Math.Max(1, v.Medidor.Tasa);
                if (sonado < 30.0 || (dura > 0 && sonado < 0.97 * dura)) return;
                RocolaOsMedidaNueva m = new RocolaOsMedidaNueva();
                v.Medidor.Cierra(m);
                RocolaOsMedidaNueva crudo = new RocolaOsMedidaNueva();
                if (ent != null && ent.Crudo != null) ent.Crudo.Arreglos(crudo);
                m.Ruta = ruta; m.Cadena = cadena; m.Largo = largo; m.Creada = creada; m.Escrita = escrita; m.ZumbidoHz = zumb;
                m.HuellaB = cadena == 'B' ? RocolaOsCadenaB.HuellaAhora : "-"; m.Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                bool pide = crudo.CanalMudo >= 0 || crudo.Invertido || crudo.ContinuaL != 0f || crudo.ContinuaR != 0f;
                if (conArreglos)
                {   // ya sono con sus arreglos (los de una medida en vivo de antes): esta medida vale
                    m.CanalMudo = r.CanalMudo; m.Invertido = r.Invertido; m.ContinuaL = r.ContinuaL; m.ContinuaR = r.ContinuaR;
                    m.Alg = RocolaOsMedidaNueva.Algoritmo;
                }
                else if (pide)
                {   // pide un arreglo: se guardan los arreglos (algoritmo 0) y la siguiente vez se mide con ellos puestos
                    m.CanalMudo = crudo.CanalMudo; m.Invertido = crudo.Invertido; m.ContinuaL = crudo.ContinuaL; m.ContinuaR = crudo.ContinuaR;
                    m.Alg = 0;
                }
                else m.Alg = RocolaOsMedidaNueva.Algoritmo;
                Interlocked.Increment(ref RocolaOsMedidaTrabajo.EnVivo);
                RocolaOsMedidaTrabajo.Vivas.Enqueue(m);
                RocolaOsMedidaTrabajo.Despierta();
            };
        }

        // ---------------------------------------------------------------- el latido: la cola y la foto para el repaso
        private int rocolaOsMedidaCuenta = -1;
        private long rocolaOsMedidaFotoMs;
        private bool rocolaOsMedidaArmando, rocolaOsMedidaTrabajaba;
        private readonly HashSet<string> rocolaOsMedidaPedidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private void RocolaOsLatidoMedida()
        {
            bool trabaja = RocolaOsMedidaTrabajo.Trabaja;
            if (!trabaja) { rocolaOsMedidaTrabajaba = false; return; }
            if (!RocolaOsMedidaTabla.Cargada) return;
            char cadena = LocalAutoAGuardar ? 'B' : 'A';
            // LA COLA (y lo que suena): las tuyas sin medida valida, ya
            PlayItem np = nowPlaying;
            if (np != null) RocolaOsMedidaMira(np, cadena);
            for (int i = 0; i < queue.Count && i < 16; i++) RocolaOsMedidaMira(queue[i], cadena);
            // EL REPASO: la foto de la biblioteca (al empezar a trabajar, al acabar de armarse o cambiar, y cada hora)
            if (libBuilding) { rocolaOsMedidaArmando = true; return; }
            long ahora = Environment.TickCount64;
            if (rocolaOsMedidaTrabajaba && !rocolaOsMedidaArmando && searchIndex.Count == rocolaOsMedidaCuenta && ahora - rocolaOsMedidaFotoMs < 3600000) return;
            rocolaOsMedidaTrabajaba = true; rocolaOsMedidaArmando = false; rocolaOsMedidaCuenta = searchIndex.Count; rocolaOsMedidaFotoMs = ahora;
            List<KeyValuePair<string, long>> rutas = new List<KeyValuePair<string, long>>();
            HashSet<string> vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < searchIndex.Count; i++)
            {
                SearchItem si = searchIndex[i];
                if (si == null || si.Kind != 2 || si.TrackRef == null || si.TrackRef.Path == null) continue;
                string p = si.TrackRef.Path;
                if (!vistas.Add(p)) continue;
                int veces; songCounts.TryGetValue(p, out veces);
                long seq; songSeq.TryGetValue(p, out seq);
                rutas.Add(new KeyValuePair<string, long>(p, (long)veces * 1000000000000L + seq));
            }
            rutas.Sort((a, b) => b.Value.CompareTo(a.Value));
            List<RocolaOsMedidaTrabajo.Pedido> l = new List<RocolaOsMedidaTrabajo.Pedido>(rutas.Count);
            foreach (KeyValuePair<string, long> kv in rutas)
                if (RocolaOsMedidaTabla.Busca(kv.Key, cadena) == null) l.Add(new RocolaOsMedidaTrabajo.Pedido { Ruta = kv.Key, Cadena = cadena, ZumbidoHz = ZumbidoDe(kv.Key), Arreglos = cadena == 'B' ? RocolaOsMedidaTabla.Busca(kv.Key, 'A') : null });
            RocolaOsMedidaTrabajo.Repasa(l);
            RocolaOs.Apunta(RocolaOsCadenasNuevas.Medida, "repaso de fondo (cadena " + cadena + "): " + l.Count + " de " + rutas.Count + " canciones por medir");
        }
        private void RocolaOsMedidaMira(PlayItem it, char cadena)
        {
            if (it == null || it.IsVideo || it.Song == null || it.Song.Path == null) return;
            string p = it.Song.Path;
            if (p.StartsWith("spotify:", StringComparison.Ordinal)) return;   // (Spotify: en vivo)
            string k = cadena + "|" + p;
            if (rocolaOsMedidaPedidas.Contains(k)) return;
            if (RocolaOsMedidaTabla.Busca(p, cadena) is RocolaOsMedidaNueva ya && ya.Alg == RocolaOsMedidaNueva.Algoritmo) { rocolaOsMedidaPedidas.Add(k); return; }
            rocolaOsMedidaPedidas.Add(k);
            RocolaOsMedidaTrabajo.PideYa(new RocolaOsMedidaTrabajo.Pedido { Ruta = p, Cadena = cadena, ZumbidoHz = ZumbidoDe(p), Arreglos = cadena == 'B' ? RocolaOsMedidaTabla.Busca(p, 'A') : null });
        }
    }
}

// EL NIVELADO DE LAS CANCIONES DE SPOTIFY PEDIDAS (fase 11, funcion «spotify_nivela», encendida de fabrica; FASE11_spotify-nivela.md).
// Decision del dueno del 22-11-2026 («que las canciones de Spotify suenen igual de fuerte que una cancion de la rocola [...] El volumen varia
// mucho en ellas»; «Haz lo del nivelado»). Las del buscador y del catalogo («spotify:track:ID», RocolaOsFuenteSpotify) sonaban como una de XP
// sin medir (ganancia 1, lo que manda Soloist sin la normalizacion de Spotify). Ahora:
//   1. APRENDE: mientras suena se mide como mide el repaso tus canciones (MedidorBs1770: el pico, el P99 y el P90 de los picos por bloque y la
//      sonoridad integrada), y si sono entera (97 % de su duracion y 30 s o mas) se guardan SOLO esos numeros por id en
//      rocola_spotify_niveles.txt (en los datos de rocola-xp, aparte de las tablas de XP: el repaso nunca ve rutas que no son archivos). La
//      siguiente vez se nivela desde el primer cuadro como tus canciones (GananciaAlLimite, con el margen del tono del local y el parejo del
//      billar, como la sustitucion invisible: g62 de RocolaOs.Calidad.cs, que pregunta aqui primero).
//   2. LA PRIMERA VEZ (sin medida): arranca con la mediana de las ganancias de las de Spotify ya medidas (esta tabla y las parejas medidas de
//      la sustitucion; con 5 o mas; si no, 1 como antes) y, solo los primeros 30 s de sonido, un NIVELADO SUAVE: mide la sonoridad de lo que
//      llega de Soloist (ponderacion K y puertas de BS.1770 sobre lo que lleva sonado) y lleva la ganancia hacia la que deja la cancion a la
//      sonoridad tipica de la rocola ya nivelada (OBJETIVO: la mediana de «sonoridad integrada + ganancia al limite» de tus canciones medidas
//      con el algoritmo 5 y de las de Spotify medidas; con menos de 20, no hay nivelado en vivo), como mucho 0,5 dB por segundo, +-8 dB del
//      arranque y sin pasar de +3 dB sobre lo que aguanta su pico (el limitador de XP absorbe el resto), sin decidir mientras la parte es
//      suave (los ultimos 3 s por debajo de 10 LU de lo integrado, o casi silencio) y con 2 s de medida como minimo; despues se CONGELA el resto
//      de la cancion (el dueno rechazo los brincos el 18-11-2026). La ganancia cambia muestra a muestra (rampa), nunca de golpe. Va en la
//      fuente: antes de toda la cadena y del limitador.
// Sin Rocola OS, sin el modo Spotify o con «spotify_nivela=0»: como antes, bit a bit. Las de la sustitucion invisible siguen con lo suyo.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static class RocolaOsSpotifyNivela
    {
        internal const string Nombre = "spotify_nivela";
        internal const string Archivo = "rocola_spotify_niveles.txt";
        internal const string Cabecera = "ROCOLA-SPOTIFY-NIVELES v1";
        internal const int TopeTabla = 20000;
        internal const double MinimoSonadoSeg = 30.0, MinimoFraccion = 0.97;
        internal const int MinimoMediana = 5, MinimoObjetivo = 20;
        // el nivelado en vivo
        internal const double CongelaSeg = 30.0;      // despues de 30 s de sonido, la ganancia ya no se mueve
        internal const double PasoDbPorSeg = 0.5;     // como mucho 0,5 dB por segundo
        internal const double TopeDb = 8.0;           // +-8 dB respecto del arranque
        internal const double SobrePicoDb = 3.0;      // sin pasar de +3 dB sobre lo que aguanta su pico hasta ahora
        internal const int MinimoBloques = 20;        // bloques de 400 ms que pasaron las puertas antes de decidir (2,3 s de sonido)
        internal const double SuaveLu = 10.0;         // los ultimos 3 s a mas de 10 LU por debajo de lo integrado: parte suave, no se decide
        internal const double CasiSilencioLkfs = -50.0;

        internal sealed class Medida
        {
            internal float PicoDb, P99Db, P90Db, Lkfs; internal long Unix;
        }
        private static readonly ConcurrentDictionary<string, Medida> tabla = new ConcurrentDictionary<string, Medida>(StringComparer.Ordinal);
        internal static ConcurrentDictionary<string, Medida> Tabla { get { return tabla; } }
        private static volatile bool cargada;
        private static int encendida;
        internal static bool Va { get { return Volatile.Read(ref encendida) != 0 && cargada && RocolaOsSpotify.Encendido; } }
        internal static long Medidas, EnVivo, Congeladas, Arranques;
        private static long cambios;
        internal static long Cambios { get { return Interlocked.Read(ref cambios); } }

        // lo que deja una cancion que sono entera (lo mide el hilo, no el del sonido)
        internal sealed class Resultado { internal string Id; internal MedidorBs1770 Medidor; internal int Hz; }
        private static readonly ConcurrentQueue<Resultado> trabajo = new ConcurrentQueue<Resultado>();
        private static readonly AutoResetEvent hay = new AutoResetEvent(false);
        private static volatile bool parar;
        internal static void Arranca() { Volatile.Write(ref encendida, 1); RocolaOs.Hilo(Nombre, Corre); }
        // (28-11-2026) SOLO LEER la tabla, sin medir ni nivelar (la cadena nueva la usa de respaldo para la 1.a vez de Spotify): una vez, en el
        // hilo que la pida (el de la medida nueva)
        private static int leida;
        internal static void CargaParaLeer()
        {
            if (Interlocked.Exchange(ref leida, 1) != 0 || Volatile.Read(ref encendida) != 0) return;
            int n = Carga();
            if (n > 0) Apunta("solo para leer: " + n.ToString(CultureInfo.InvariantCulture) + " canciones de Spotify medidas de antes en " + Archivo + " (la ganancia estimada de la 1.a vez)");
        }
        internal static void Para() { parar = true; hay.Set(); }
        internal static void Encola(Resultado r) { trabajo.Enqueue(r); hay.Set(); }
        internal static void Apunta(string que) { RocolaOs.Apunta(Nombre, que); }

        private static void Corre()
        {
            int n = Carga();
            cargada = true;
            Apunta("encendida: " + n.ToString(CultureInfo.InvariantCulture) + " canciones de Spotify ya medidas en " + Archivo);
            while (!parar)
            {
                Resultado r; bool guardar = false;
                while (trabajo.TryDequeue(out r))
                {
                    try { guardar |= Cierra(r); }
                    catch (Exception ex) { RocolaOs.Falla(Nombre, "medida", ex); }
                }
                if (guardar) Guarda();
                hay.WaitOne(5000);
            }
        }
        private static bool Cierra(Resultado r)
        {
            if (r == null || r.Id == null || r.Medidor == null || r.Hz < 8000) return false;
            SonoridadIntegrada s;
            if (!Form1.CierraIntegrada(r.Medidor, r.Medidor.Cuadros / (double)r.Hz, r.Hz, out s) || !s.ConP90 || s.Muda) return false;
            tabla[r.Id] = new Medida { PicoDb = s.PicoDb, P99Db = s.P99Db, P90Db = s.P90Db, Lkfs = s.Lkfs, Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            Interlocked.Increment(ref Medidas); Interlocked.Increment(ref cambios);
            Apunta(r.Id + ": medida al limite: pico " + F1(s.PicoDb) + ", P99 " + F1(s.P99Db) + ", P90 " + F1(s.P90Db) + " dBFS, " + F1(s.Lkfs) + " LKFS (se nivela con eso desde la proxima vez)");
            return true;
        }
        internal static string F1(double x) { return x.ToString("0.0", CultureInfo.InvariantCulture); }

        // ---------- la tabla ----------
        internal static string RutaTabla { get { return RocolaOs.Datos(Archivo); } }
        private static int Carga()
        {
            string[] ls;
            try { ls = Form1.RocolaOsLeeLineas(RutaTabla); } catch { ls = null; }
            if (ls == null) return 0;
            foreach (string l in ls)
            {
                if (string.IsNullOrEmpty(l) || l[0] == '#' || l == Cabecera) continue;
                string[] c = l.Split('\t');
                if (c.Length < 6) continue;
                string id = c[0];
                if (RocolaOsSpotify.IdBase62("spotify:track:" + id, "spotify:track:").Length == 0) continue;
                Medida m = new Medida { PicoDb = Fl(c[1]), P99Db = Fl(c[2]), P90Db = Fl(c[3]), Lkfs = Fl(c[4]), Unix = L(c[5]) };
                if (float.IsNaN(m.PicoDb) || float.IsNaN(m.P99Db) || float.IsNaN(m.P90Db)) continue;
                tabla[id] = m;
            }
            Recorta();
            return tabla.Count;
        }
        private static void Recorta()
        {
            if (tabla.Count <= TopeTabla) return;
            List<KeyValuePair<string, Medida>> l = new List<KeyValuePair<string, Medida>>(tabla);
            l.Sort((a, b) => a.Value.Unix.CompareTo(b.Value.Unix));
            for (int i = 0; i < l.Count && tabla.Count > TopeTabla; i++) { Medida z; tabla.TryRemove(l[i].Key, out z); }
        }
        private static void Guarda()
        {
            Recorta();
            StringBuilder sb = new StringBuilder();
            sb.Append(Cabecera).Append('\n');
            sb.Append("# id_spotify\tpico_db\tp99_db\tp90_db\tlkfs\tmedida_unix\n");
            List<string> ids = new List<string>(tabla.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string k in ids)
            {
                Medida m; if (!tabla.TryGetValue(k, out m)) continue;
                sb.Append(k).Append('\t').Append(Fs(m.PicoDb)).Append('\t').Append(Fs(m.P99Db)).Append('\t').Append(Fs(m.P90Db)).Append('\t').Append(Fs(m.Lkfs))
                  .Append('\t').Append(m.Unix.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            try { Form1.RocolaOsEscribeSeguro(RutaTabla, sb.ToString()); }
            catch (Exception ex) { RocolaOs.Falla(Nombre, "guardar " + Archivo, ex); }
        }
        private static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
        private static float Fl(string s) { float v; return s != "-" && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : float.NaN; }
        private static string Fs(float v) { return float.IsNaN(v) || float.IsInfinity(v) ? "-" : v.ToString("0.###", CultureInfo.InvariantCulture); }

        internal static string IdDe(string uri) { string id = RocolaOsSpotify.IdBase62(uri, "spotify:track:"); return id.Length > 0 ? id : null; }
    }

    // EL NIVELADO EN VIVO DE UNA CANCION (lo usa su fuente, en el hilo del sonido: sin cerrojos ni memoria nueva al leer, salvo el primer
    // trozo mas grande que el de siempre)
    internal sealed class RocolaOsSpotifyNivelador
    {
        internal readonly string Id;
        private readonly int hz;
        internal readonly double ArranqueDb;           // la ganancia con que arranca (la de GananciaDe, sin el margen del local): solo para el registro
        private readonly double objetivoMenosArranque;  // OBJETIVO (LKFS) - ArranqueDb; NaN: sin nivelado en vivo
        private readonly MedidorBs1770 medidor;        // para aprender (null si no hace falta)
        private float[] copia = new float[16384];
        // la ponderacion K (las dos etapas de BS.1770, a la frecuencia de la fuente) y los bloques
        private readonly double[] e1, e2;              // b0 b1 b2 a1 a2
        private double l1a, l1b, l2a, l2b, r1a, r1b, r2a, r2b;   // estados (forma directa II transpuesta) por canal y etapa
        private readonly int porSub;                   // cuadros de 100 ms
        private int enSub; private double sumaSub;
        private readonly double[] sub = new double[(int)(RocolaOsSpotifyNivela.CongelaSeg * 10) + 8];   // energia de cada 100 ms
        private int nSub;
        private double picoLin;
        private double x, objetivo;                     // dB en vivo (sobre el arranque) y adonde va
        private bool congelada, vivo;
        private long sonido;                            // cuadros de sonido vistos

        internal RocolaOsSpotifyNivelador(string id, int hz, double arranqueDb, double objetivoLkfs, bool aprender)
        {
            Id = id; this.hz = hz; ArranqueDb = arranqueDb;
            objetivoMenosArranque = double.IsNaN(objetivoLkfs) ? double.NaN : objetivoLkfs - arranqueDb;
            vivo = !double.IsNaN(objetivoMenosArranque);
            if (aprender) { try { medidor = new MedidorBs1770(hz, new double[] { 1.0, 1.0 }); } catch { medidor = null; } }
            e1 = MedidorBs1770.Estante(hz); e2 = MedidorBs1770.PasoAlto(hz);
            porSub = Math.Max(1, hz / 10);
            if (vivo) Interlocked.Increment(ref RocolaOsSpotifyNivela.EnVivo);
            Interlocked.Increment(ref RocolaOsSpotifyNivela.Arranques);
        }
        internal bool Vivo { get { return vivo; } }
        internal double EnVivoDb { get { return x; } }

        // lo que llego de Soloist (n cuadros de sonido, estereo, aun sin ninguna ganancia): se mide y se le pone la ganancia en vivo
        internal void Procesa(Span<float> d, int n)
        {
            if (n <= 0) return;
            if (medidor != null)
            {
                if (copia.Length < n * 2) copia = new float[n * 2];
                d.Slice(0, n * 2).CopyTo(copia);
                try { medidor.Procesa(copia, 0, n * 2); } catch { }
            }
            if (!vivo) return;
            double antes = x;
            if (!congelada)
            {
                Mide(d, n);
                double paso = RocolaOsSpotifyNivela.PasoDbPorSeg * n / hz;
                if (objetivo > x) x = Math.Min(objetivo, x + paso); else if (objetivo < x) x = Math.Max(objetivo, x - paso);
                sonido += n;
                if (sonido >= (long)(RocolaOsSpotifyNivela.CongelaSeg * hz))
                {
                    congelada = true;
                    Interlocked.Increment(ref RocolaOsSpotifyNivela.Congeladas);
                    RocolaOsSpotify.Apunta("nivelado en vivo de " + Id + ": arranco a " + RocolaOsSpotifyNivela.F1(ArranqueDb) + " dB y se queda en "
                        + (x >= 0 ? "+" : "") + RocolaOsSpotifyNivela.F1(x) + " dB sobre eso (congelado a los " + RocolaOsSpotifyNivela.F1(RocolaOsSpotifyNivela.CongelaSeg) + " s)");
                }
            }
            if (antes == 0.0 && x == 0.0) return;
            // la rampa, muestra a muestra, de lo de antes a lo de ahora
            float ga = (float)Math.Pow(10.0, antes / 20.0), gb = (float)Math.Pow(10.0, x / 20.0);
            if (ga == gb) { for (int i = 0; i < n * 2; i++) d[i] *= gb; return; }
            float paso2 = (gb - ga) / n;
            for (int i = 0; i < n; i++) { float g = ga + paso2 * (i + 1); d[i * 2] *= g; d[i * 2 + 1] *= g; }
        }
        private void Mide(Span<float> d, int n)
        {
            double b0 = e1[0], b1 = e1[1], b2 = e1[2], a1 = e1[3], a2 = e1[4];
            double c0 = e2[0], c1 = e2[1], c2 = e2[2], d1 = e2[3], d2 = e2[4];
            for (int i = 0; i < n; i++)
            {
                double l = d[i * 2], r = d[i * 2 + 1];
                double al = Math.Abs(l), ar = Math.Abs(r);
                if (al > picoLin) picoLin = al; if (ar > picoLin) picoLin = ar;
                double y = b0 * l + l1a; l1a = b1 * l - a1 * y + l1b; l1b = b2 * l - a2 * y; l = y;
                y = c0 * l + l2a; l2a = c1 * l - d1 * y + l2b; l2b = c2 * l - d2 * y; l = y;
                double z = b0 * r + r1a; r1a = b1 * r - a1 * z + r1b; r1b = b2 * r - a2 * z; r = z;
                z = c0 * r + r2a; r2a = c1 * r - d1 * z + r2b; r2b = c2 * r - d2 * z; r = z;
                sumaSub += l * l + r * r;
                if (++enSub >= porSub)
                {
                    if (nSub < sub.Length) sub[nSub++] = sumaSub / enSub;
                    enSub = 0; sumaSub = 0;
                    Decide();
                }
            }
        }
        private static double Lkfs(double e) { return e > 0 ? -0.691 + 10.0 * Math.Log10(e) : double.NegativeInfinity; }
        // con cada 100 ms: la sonoridad integrada (bloques de 400 ms con 75 % de solape, puerta absoluta de -70 y relativa de -10, BS.1770) de lo
        // que lleva sonado y la de los ultimos 3 s; si la parte no es suave y ya hay medida, adonde ir
        private void Decide()
        {
            if (nSub < 4) return;
            double sumaAbs = 0; int nAbs = 0;
            for (int k = 3; k < nSub; k++) { double eb = (sub[k] + sub[k - 1] + sub[k - 2] + sub[k - 3]) * 0.25; if (Lkfs(eb) > MedidorBs1770.PuertaAbsoluta) { sumaAbs += eb; nAbs++; } }
            if (nAbs == 0) return;
            double rel = Lkfs(sumaAbs / nAbs) + MedidorBs1770.PuertaRelativa;
            double suma = 0; int nb = 0;
            for (int k = 3; k < nSub; k++) { double eb = (sub[k] + sub[k - 1] + sub[k - 2] + sub[k - 3]) * 0.25; double lb = Lkfs(eb); if (lb > MedidorBs1770.PuertaAbsoluta && lb > rel) { suma += eb; nb++; } }
            if (nb < RocolaOsSpotifyNivela.MinimoBloques) return;
            double integrada = Lkfs(suma / nb);
            int desde = Math.Max(0, nSub - 30); double st = 0; for (int k = desde; k < nSub; k++) st += sub[k];
            double corta = Lkfs(st / (nSub - desde));
            if (corta < RocolaOsSpotifyNivela.CasiSilencioLkfs || corta < integrada - RocolaOsSpotifyNivela.SuaveLu) return;   // parte suave: no se decide
            double o = objetivoMenosArranque - integrada;
            if (o > RocolaOsSpotifyNivela.TopeDb) o = RocolaOsSpotifyNivela.TopeDb; else if (o < -RocolaOsSpotifyNivela.TopeDb) o = -RocolaOsSpotifyNivela.TopeDb;
            double picoDb = picoLin > 0 ? 20.0 * Math.Log10(picoLin) : -120.0;
            double techo = RocolaOsSpotifyNivela.SobrePicoDb - (picoDb + ArranqueDb);
            if (o > techo) o = Math.Max(Math.Min(0.0, o), techo);
            objetivo = o;
        }

        // al acabar entera: la medida para la tabla (la cierra el hilo de la funcion)
        internal void Acaba(bool natural, double sonadoSeg, int durMs)
        {
            if (medidor == null || !natural || sonadoSeg < RocolaOsSpotifyNivela.MinimoSonadoSeg) return;
            if (durMs > 0 && sonadoSeg * 1000.0 < RocolaOsSpotifyNivela.MinimoFraccion * durMs) return;
            RocolaOsSpotifyNivela.Encola(new RocolaOsSpotifyNivela.Resultado { Id = Id, Medidor = medidor, Hz = hz });
        }
    }

    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraSpotifyNivela()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsSpotifyNivela.Nombre,
                Que = "el nivelado de las canciones de Spotify pedidas: aprende cada una al sonar entera y la primera vez la lleva suave a la sonoridad de la rocola en sus primeros 30 s (necesita «spotify»)",
                // (28-11-2026, «La cadena vieja borrada», CADENA_NUEVA.md) es de la cadena de XP: con ROCOLA_OS=1 ya no va (la cadena nueva mide
                // Spotify en vivo y no nivela a media cancion); solo con una lista de pruebas que la nombre. Su tabla se sigue LEYENDO (CargaParaLeer)
                // para la ganancia fija estimada de Spotify la 1.a vez, hasta la fase 2.
                DeFabrica = false, SoloEnLaLista = true,
                Arranca = f => RocolaOsSpotifyNivela.Arranca(),
                Vivo = (f, sb) => sb.Append("spotify_nivela medidas=").Append(RocolaOsSpotifyNivela.Tabla.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" nuevas=").Append(Interlocked.Read(ref RocolaOsSpotifyNivela.Medidas).ToString(CultureInfo.InvariantCulture))
                    .Append(" en_vivo=").Append(Interlocked.Read(ref RocolaOsSpotifyNivela.EnVivo).ToString(CultureInfo.InvariantCulture))
                    .Append(" congeladas=").Append(Interlocked.Read(ref RocolaOsSpotifyNivela.Congeladas).ToString(CultureInfo.InvariantCulture)).Append('\n'),
                ParaAlCerrar = razon => RocolaOsSpotifyNivela.Para(),
            });
        }

        // LA GANANCIA BASE de una de Spotify pedida (sin el margen del local): su medida al limite; sin medida, la mediana de las de Spotify medidas
        // (con 5 o mas); si no, NaN (como antes: ganancia 1, la de XP sin medir)
        private float RocolaOsSpotifyNivelaBase(string id, out string como, out bool medida)
        {
            medida = false;
            if (!normalizeOn || normStrength <= 0.001f) { como = "el nivelado apagado"; return float.NaN; }
            RocolaOsSpotifyNivela.Medida m;
            if (id != null && RocolaOsSpotifyNivela.Tabla.TryGetValue(id, out m))
            {
                medida = true; como = "su medida";
                return GananciaAlLimite(normStrength, DbALin(m.PicoDb), DbALin(m.P99Db), DbALin(m.P90Db));
            }
            List<float> l = new List<float>();
            foreach (RocolaOsSpotifyNivela.Medida x in RocolaOsSpotifyNivela.Tabla.Values) l.Add(GananciaAlLimite(normStrength, DbALin(x.PicoDb), DbALin(x.P99Db), DbALin(x.P90Db)));
            foreach (RocolaOsCalidad.Pareja x in RocolaOsCalidad.Pares.Values)
                if (x.Id != null && x.ConMedida && !RocolaOsSpotifyNivela.Tabla.ContainsKey(x.Id)) l.Add(GananciaAlLimite(normStrength, DbALin(x.PicoDb), DbALin(x.P99Db), DbALin(x.P90Db)));
            if (l.Count < RocolaOsSpotifyNivela.MinimoMediana) { como = "sin medir (" + l.Count.ToString(CultureInfo.InvariantCulture) + " de Spotify medidas: como una de XP sin medir)"; return float.NaN; }
            l.Sort();
            como = "la mediana de " + l.Count.ToString(CultureInfo.InvariantCulture) + " de Spotify medidas";
            return l.Count % 2 == 1 ? l[l.Count / 2] : 0.5f * (l[l.Count / 2 - 1] + l[l.Count / 2]);
        }

        // g62 (por RocolaOsCalidadGanancia): una de Spotify pedida (no la de la sustitucion) con la funcion: su base con el margen del tono del local
        // y, en el billar, el volumen parejo (como GananciaDe y RocolaOsCalidadGananciaDe). false: lo de antes (XP, ganancia de sin medir).
        private bool RocolaOsSpotifyNivelaGanancia(Track t, out float g)
        {
            g = 1f;
            if (!RocolaOs.Activo || t == null || t.Path == null || !RocolaOsSpotifyNivela.Va || !RocolaOs.Encendida(RocolaOsSpotifyNivela.Nombre)) return false;
            string id = RocolaOsSpotifyNivela.IdDe(t.Path);
            if (id == null) return false;
            string como; bool medida;
            float b = RocolaOsSpotifyNivelaBase(id, out como, out medida);
            if (float.IsNaN(b)) return false;
            float m = MargenDelTono(t);
            if (localAuto) { float pa = ParejoAplicado(t); if (pa != 0f) m *= DbALin(pa); }
            g = m == 1f ? b : b * m;
            return true;
        }

        // EL OBJETIVO DEL NIVELADO EN VIVO (LKFS): la mediana de «sonoridad integrada + ganancia al limite» de las canciones medidas con el
        // algoritmo 5 (las tuyas, integCache) y de las de Spotify medidas. Con menos de MinimoObjetivo, NaN (sin nivelado en vivo). Se recuerda
        // mientras no cambien la tabla de XP, la de Spotify ni la fuerza del nivelado (como mucho se rehace una vez por minuto).
        private double rocolaOsNvObjetivo = double.NaN; private object rocolaOsNvTabla; private long rocolaOsNvCambios = -1, rocolaOsNvCuando; private float rocolaOsNvFuerza = -1f;
        private readonly object rocolaOsNvCerrojo = new object();
        private double RocolaOsSpotifyNivelaObjetivo()
        {
            Dictionary<string, SonoridadIntegrada> t = integCache;
            long ahora = Environment.TickCount64, ca = RocolaOsSpotifyNivela.Cambios;
            lock (rocolaOsNvCerrojo)
            {
                // (23-11-2026, revision) lo de antes vale mientras no cambien la tabla de Spotify ni la fuerza, y como mucho 60 s (la de XP cambia en
                // su sitio: el repaso le anade medidas sin cambiarla de objeto)
                if (rocolaOsNvTabla != null && object.ReferenceEquals(t, rocolaOsNvTabla) && ca == rocolaOsNvCambios && normStrength == rocolaOsNvFuerza && ahora - rocolaOsNvCuando < 60000) return rocolaOsNvObjetivo;
                List<double> l = new List<double>();
                // (23-11-2026, revision) la tabla de XP se copia con SU cerrojo: el repaso le escribe a la vez y recorrerla sin el tronaba («Collection
                // was modified») al abrir una de Spotify
                List<SonoridadIntegrada> deXp = new List<SonoridadIntegrada>();
                if (t != null) lock (integCerrojo) deXp.AddRange(t.Values);
                    foreach (SonoridadIntegrada s in deXp)
                    {
                        if (!s.Valida || !s.ConP90 || s.Muda || float.IsNaN(s.Lkfs) || float.IsInfinity(s.Lkfs)) continue;
                        float g = GananciaAlLimite(normStrength, DbALin(s.PicoDb), DbALin(s.P99Db), DbALin(s.P90Db));
                        l.Add(s.Lkfs + 20.0 * Math.Log10(Math.Max(1e-6, g)));
                    }
                foreach (RocolaOsSpotifyNivela.Medida m in RocolaOsSpotifyNivela.Tabla.Values)
                {
                    if (float.IsNaN(m.Lkfs) || float.IsInfinity(m.Lkfs)) continue;
                    float g = GananciaAlLimite(normStrength, DbALin(m.PicoDb), DbALin(m.P99Db), DbALin(m.P90Db));
                    l.Add(m.Lkfs + 20.0 * Math.Log10(Math.Max(1e-6, g)));
                }
                if (l.Count < RocolaOsSpotifyNivela.MinimoObjetivo) rocolaOsNvObjetivo = double.NaN;
                else { l.Sort(); rocolaOsNvObjetivo = l.Count % 2 == 1 ? l[l.Count / 2] : 0.5 * (l[l.Count / 2 - 1] + l[l.Count / 2]); }
                rocolaOsNvTabla = t; rocolaOsNvCambios = ca; rocolaOsNvFuerza = normStrength; rocolaOsNvCuando = ahora;   // (solo si se pudo calcular)
                return rocolaOsNvObjetivo;
            }
        }

        // (desde g13, al abrir una de Spotify pedida) su nivelador: aprende si no tiene medida y, la primera vez, nivela en vivo. null: como antes.
        private RocolaOsSpotifyNivelador RocolaOsSpotifyNivelaPara(string uri, int hz)
        {
            if (!RocolaOsSpotifyNivela.Va || !RocolaOs.Encendida(RocolaOsSpotifyNivela.Nombre)) return null;
            string id = RocolaOsSpotifyNivela.IdDe(uri);
            if (id == null || RocolaOsSpotifyNivela.Tabla.ContainsKey(id)) return null;   // ya medida: GananciaDe la nivela desde el primer cuadro
            string como; bool medida;
            float b = RocolaOsSpotifyNivelaBase(id, out como, out medida);
            double arranqueDb = float.IsNaN(b) ? 0.0 : 20.0 * Math.Log10(Math.Max(1e-6, b));
            double obj = double.NaN;
            try { if (normalizeOn && normStrength > 0.001f) obj = RocolaOsSpotifyNivelaObjetivo(); }
            catch (Exception ex) { RocolaOs.Falla("spotify_nivela", "el objetivo del nivelado", ex); }   // (23-11-2026: sin objetivo, sin nivelado en vivo; la cancion suena igual)
            RocolaOsSpotify.Apunta("nivelado de " + id + ": arranca con " + como + " (" + RocolaOsSpotifyNivela.F1(arranqueDb) + " dB); "
                + (double.IsNaN(obj) ? "sin nivelado en vivo (menos de " + RocolaOsSpotifyNivela.MinimoObjetivo.ToString(CultureInfo.InvariantCulture) + " canciones medidas o el nivelado apagado)" : "en vivo los primeros 30 s hacia " + RocolaOsSpotifyNivela.F1(obj) + " LKFS")
                + "; se mide para la proxima");
            return new RocolaOsSpotifyNivelador(id, hz, arranqueDb, obj, true);
        }
    }
}

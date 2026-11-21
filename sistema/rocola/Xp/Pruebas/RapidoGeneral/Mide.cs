// LA MEDIDA DE LO GENERAL (rapido-general, 21-11-2026; prueba, no es de XP ni va en Rocola OS): una funcion de Rocola OS de mentira,
// «medir-general», solo con ROCOLA_OS=medir-general (SoloEnLaLista). Mide con la misma toma de cuadros que el diagnostico completo de XP
// (DiagCaptura: el hueco entre dos cuadros presentados, sus fases y los GC, sin reservar nada por cuadro) y la foto del procesador por
// hilo del mismo diagnostico (DiagInstantanea), por TRAMOS que dice ROCOLA_MIDE_TRAMOS («S:nombre,S:nombre,...»: el segundo del reloj de
// la pantalla en que empieza cada tramo; el ultimo acaba al cerrarse la rocola o en «S:fin»). Un renglon por tramo en ROCOLA_MIDE_GENERAL:
//   arranque  ms_desde_el_proceso  (al llamar a Arranca: la rocola construida, antes de ensenarse)
//   primer    ms_desde_el_proceso  (el primer cuadro presentado)
//   lista     ms_desde_el_proceso  canciones discos   (la biblioteca publicada: ya se puede pedir)
//   compara N M D  (con ROCOLA_MIDE_COMPARA=K: cada K cuadros la pantalla compuesta como antes de rapido-general, con todas las capas, contra
//                  la de ahora: N cuadros comparados, M distintos, D el mayor numero de puntos distintos en uno)
//   tramo NOMBRE cuadros p50 p95 p99 max medio procP50 presP50 h50 h100 gc0 gc1 gc2 MB_nuevos_pantalla KB_por_cuadro MB_nuevos_todos cpu%_nucleo hilos...
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void MideGeneralRegistra()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "medir-general",
                Que = "la medida de lo general (prueba)",
                DeFabrica = false,
                SoloEnLaLista = true,
                Arranca = f => f.MgArranca(),
                Latido = f => f.MgLatido(),
                AlCerrar = (f, razon) => f.MgCierra(),
            });
        }
        private static readonly CultureInfo mgc = CultureInfo.InvariantCulture;
        private StreamWriter mgLog; private DiagCaptura mgCap; private DiagInstantanea mgFoto;
        private readonly List<KeyValuePair<double, string>> mgTramos = new List<KeyValuePair<double, string>>();
        private readonly List<double> mgCosteTodo = new List<double>(), mgCosteAhora = new List<double>();
        private int mgComparados, mgDistintos, mgPeor; private int mgSig; private string mgNombre; private long mgAllocUi, mgAllocTodo; private bool mgPrimer, mgLista, mgEnchufada;
        private static double MgDesdeProceso()
        {
            try { using (Process p = Process.GetCurrentProcess()) return (DateTime.Now - p.StartTime).TotalMilliseconds; } catch { return -1; }
        }
        private void MgArranca()
        {
            string ruta = Environment.GetEnvironmentVariable("ROCOLA_MIDE_GENERAL");
            if (string.IsNullOrEmpty(ruta)) return;
            mgLog = new StreamWriter(ruta, true) { AutoFlush = true };
            mgLog.WriteLine("arranque\t" + MgDesdeProceso().ToString("0", mgc) + "\tprocesadores=" + Environment.ProcessorCount);
            string t = Environment.GetEnvironmentVariable("ROCOLA_MIDE_TRAMOS") ?? "";
            foreach (string p in t.Split(','))
            {
                int c = p.IndexOf(':'); double s;
                if (c > 0 && double.TryParse(p.Substring(0, c), NumberStyles.Float, mgc, out s)) mgTramos.Add(new KeyValuePair<double, string>(s * 1000, p.Substring(c + 1)));
            }
            mgCap = new DiagCaptura();
            MgEnchufa();
            // el primer cuadro y la biblioteca publicada, al milisegundo (un hilo que solo mira dos numeros cada 2 ms; se va al verlos)
            System.Threading.Thread vigia = new System.Threading.Thread(delegate ()
            {
                bool p = false, l = false;
                for (int i = 0; i < 30000 && !(p && l); i++)
                {
                    if (!p && mgCap.CuadrosVistos > 0) { p = true; mgPrimer = true; lock (mgLog) mgLog.WriteLine("primer\t" + MgDesdeProceso().ToString("0", mgc)); }
                    if (!l) { List<Track> t = tracks; List<Album> a = albums; if (t != null && a != null && t.Count > 0 && a.Count > 0) { l = true; mgLista = true; lock (mgLog) mgLog.WriteLine("lista\t" + MgDesdeProceso().ToString("0", mgc) + "\t" + t.Count + "\t" + a.Count); } }
                    System.Threading.Thread.Sleep(2);
                }
            });
            vigia.IsBackground = true; vigia.Start();
#if !MIDE_BASE   // (la base no tiene CompositorPantalla.Prueba: compilar con -p:MideBase=1)
            int k;
            if (int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_MIDE_COMPARA") ?? "", out k) && k > 0)
            {
                int cuenta = 0;
                CompositorPantalla.Prueba = delegate (CompositorPantalla comp, List<CapaPantalla> lista)
                {
                    if (++cuenta % k != 0) return;
                    byte[] a = comp.LeePantalla(); comp.ComponeTodo(lista); byte[] b = comp.LeePantalla();
                    int d = 0; for (int i = 0; i < a.Length; i += 4) if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) d++;
                    mgComparados++; if (d > 0) { mgDistintos++; if (d > mgPeor) mgPeor = d; lock (mgLog) mgLog.WriteLine("distinto\t" + PantallaRocola.AhoraMs.ToString("0", mgc) + "\t" + d); }
                };
            }
            // ROCOLA_MIDE_COSTE=K: cada K cuadros, lo que cuesta componer (hasta glFinish) como antes (todas las capas) y como ahora, alternando
            if (int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_MIDE_COSTE") ?? "", out k) && k > 0)
            {
                int cuenta = 0;
                CompositorPantalla.Prueba = delegate (CompositorPantalla comp, List<CapaPantalla> lista)
                {
                    if (++cuenta % k != 0) return;
                    bool primeroTodo = (cuenta / k) % 2 == 0;
                    for (int v = 0; v < 2; v++)
                    {
                        bool todo = (v == 0) == primeroTodo;
                        Rocola.Pantalla.GL.Finish();
                        long t0 = Stopwatch.GetTimestamp();
                        comp.ComponeOtraVez(lista, todo);
                        Rocola.Pantalla.GL.Finish();
                        double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                        (todo ? mgCosteTodo : mgCosteAhora).Add(ms);
                    }
                };
            }
#endif
        }
        private void MgEnchufa()
        {
            if (mgEnchufada || gpuPanel == null) return;
            gpuPanel.LibroActivo = true; gpuPanel.DiagSink = mgCap; mgEnchufada = true;
            mgCap.Empieza(GpuPanel.AhoraMs(), 1000.0 / 60.0);   // (la ventana «antes»: solo para ver el primer cuadro)
        }
        private void MgLatido()
        {
            if (mgLog == null) return;
            MgEnchufa();
            double ahora = PantallaRocola.AhoraMs;
            while (mgSig < mgTramos.Count && ahora >= mgTramos[mgSig].Key)
            {
                MgCierraTramo();
                string n = mgTramos[mgSig].Value; mgSig++;
                if (n == "fin") { mgNombre = null; continue; }
                mgNombre = n; MgCambios();
                mgFoto = DiagInstantanea.Toma(GpuPanel.AhoraMs());
                mgAllocUi = GC.GetAllocatedBytesForCurrentThread(); mgAllocTodo = GC.GetTotalAllocatedBytes(false);
                if (mgEnchufada) mgCap.Empieza(GpuPanel.AhoraMs(), 1000.0 / 60.0);
            }
        }
        private void MgCierraTramo()
        {
            if (mgNombre == null || mgFoto == null) return;
            DiagInstantanea b = DiagInstantanea.Toma(GpuPanel.AhoraMs());
            long ui = GC.GetAllocatedBytesForCurrentThread() - mgAllocUi, todo = GC.GetTotalAllocatedBytes(false) - mgAllocTodo;
            DiagResultado r = new DiagResultado();
            if (mgEnchufada) mgCap.Cierra(GpuPanel.AhoraMs(), r);
            DiagnosticoCompleto.CalculaCpu(mgFoto, b, Environment.ProcessorCount, r);
            StringBuilder sb = new StringBuilder();
            sb.Append("tramo\t").Append(mgNombre).Append('\t').Append(r.Cuadros);
            foreach (double v in new double[] { r.P50, r.P95, r.P99, r.Max, r.Medio, r.ProcP50, r.PresP50 }) sb.Append('\t').Append(v.ToString("0.00", mgc));
            sb.Append('\t').Append(r.Huecos50).Append('\t').Append(r.Huecos100);
            sb.Append('\t').Append(b.Gc0 - mgFoto.Gc0).Append('\t').Append(b.Gc1 - mgFoto.Gc1).Append('\t').Append(b.Gc2 - mgFoto.Gc2);
            sb.Append('\t').Append((ui / 1048576.0).ToString("0.00", mgc)).Append('\t').Append((r.Cuadros > 0 ? ui / 1024.0 / r.Cuadros : 0).ToString("0.0", mgc));
            sb.Append('\t').Append((todo / 1048576.0).ToString("0.00", mgc)).Append('\t').Append((r.CpuNucleos * 100).ToString("0.0", mgc));
            sb.Append("\tfases(ms/cuadro):");
            for (int i = 0; i < DiagCaptura.NSlots; i++)
                sb.Append(' ').Append(DiagCaptura.NombresSlots[i]).Append('=').Append((r.Cuadros > 0 ? r.FasesMs[i] / r.Cuadros : 0).ToString("0.00", mgc));
            sb.Append("\thilos:");
            for (int i = 0; i < r.Hilos.Count && i < 8; i++)
                if (r.Hilos[i].PctNucleo >= 0.5) sb.Append(' ').Append(r.Hilos[i].Nombre.Replace(' ', '_')).Append('=').Append(r.Hilos[i].PctNucleo.ToString("0.0", mgc));
            lock (mgLog) mgLog.WriteLine(sb.ToString()); MgCambios();
            mgFoto = null;
        }
        // los despertares por hilo (cambios de contexto voluntarios e involuntarios de /proc) desde el renglon anterior, sin los de llvmpipe
        private readonly Dictionary<string, long> mgCtx = new Dictionary<string, long>();
        private void MgCambios()
        {
            try
            {
                Dictionary<string, long> ahora = new Dictionary<string, long>();
                foreach (string d in Directory.GetDirectories("/proc/self/task"))
                {
                    string comm = File.ReadAllText(d + "/comm").Trim(); long n = 0;
                    foreach (string l in File.ReadAllLines(d + "/status"))
                        if (l.StartsWith("voluntary_ctxt_switches") || l.StartsWith("nonvoluntary_ctxt_switches")) n += long.Parse(l.Substring(l.IndexOf(':') + 1).Trim());
                    string k = comm + "#" + Path.GetFileName(d); ahora[k] = n;
                }
                StringBuilder sb = new StringBuilder("despertares");
                long tot = 0;
                foreach (KeyValuePair<string, long> kv in ahora)
                {
                    long antes; if (!mgCtx.TryGetValue(kv.Key, out antes)) antes = 0;
                    long dd = kv.Value - antes; if (kv.Key.StartsWith("llvmpipe")) continue;
                    tot += dd; if (dd >= 20) sb.Append(' ').Append(kv.Key).Append('=').Append(dd);
                }
                mgCtx.Clear(); foreach (KeyValuePair<string, long> kv in ahora) mgCtx[kv.Key] = kv.Value;
                lock (mgLog) mgLog.WriteLine(sb.Append("\ttotal=").Append(tot).ToString());
            }
            catch (Exception ex) { lock (mgLog) mgLog.WriteLine("despertares\t" + ex.Message); }
        }
        private void MgCierra()
        {
            if (mgLog == null) return;
            try { MgCierraTramo(); if (mgCosteTodo.Count > 0) { mgCosteTodo.Sort(); mgCosteAhora.Sort(); mgLog.WriteLine("coste\tantes p50=" + mgCosteTodo[mgCosteTodo.Count / 2].ToString("0.00", mgc) + " p90=" + mgCosteTodo[mgCosteTodo.Count * 9 / 10].ToString("0.00", mgc) + "\tahora p50=" + mgCosteAhora[mgCosteAhora.Count / 2].ToString("0.00", mgc) + " p90=" + mgCosteAhora[mgCosteAhora.Count * 9 / 10].ToString("0.00", mgc) + "\tn=" + mgCosteTodo.Count); } if (mgComparados > 0) mgLog.WriteLine("compara\t" + mgComparados + "\t" + mgDistintos + "\t" + mgPeor); mgLog.WriteLine("cierre\t" + MgDesdeProceso().ToString("0", mgc)); mgLog.Dispose(); } catch { }
            mgLog = null;
        }
    }
}

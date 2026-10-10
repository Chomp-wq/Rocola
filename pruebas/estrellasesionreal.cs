using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Instrumento opcional, no runner sintetico. Usa el bucle Application.Idle REAL:
// no llama DibujaUnCuadro, DoEvents ni AnimTimer_Tick para inflar la cadencia.
// Cada p95/p99 pertenece a la ventana reciente de 512 cuadros, no a toda la fase.
class EstrellaSesionReal
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Form form;
    static object panel, track;
    static int mode = -1, errors, reports;
    static bool measuring;
    static long sessionGeneration;
    static readonly Stopwatch stage = new Stopwatch();
    static double p95min, p95max, p99min, p99max, maximum, outside, schedule, prepare, draw, present;
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Call(object o, string n, params object[] args) { return o.GetType().GetMethod(n, B).Invoke(o, args); }
    static int Count(string n) { return (int)Get(form, n); }
    static double Number(object o, string n) { return (double)Get(o, n); }
    static object ValidSession(int frames, int failures)
    {
        object data = Get(panel, "fluidez");
        long intervals = (long)Get(data, "IntervalosSesion");
        // En el mismo hilo UI no puede entrar otro cuadro entre estas lecturas.
        // Reset, minimizacion o dispositivo perdido no deben certificar solo el final.
        if (failures != 0 || frames < 2 || intervals != frames - 1 || (long)Get(data, "GeneracionSesion") != sessionGeneration)
            throw new InvalidOperationException("Fallo de Present o continuidad reiniciada: escena incompleta.");
        return data;
    }
    static void Touch(bool idle)
    {
        Set(form, "lastInput", DateTime.Now.AddSeconds(idle ? -30 : 0));
        Set(form, "lastInputTick", unchecked(Environment.TickCount - (idle ? 30000 : 0)));
    }
    static bool Alive(string name)
    {
        object item = Get(form, name);
        Thread one = item as Thread; if (one != null) return one.IsAlive;
        Thread[] many = item as Thread[];
        if (many != null) foreach (Thread thread in many) if (thread != null && thread.IsAlive) return true;
        return false;
    }
    static bool Settled()
    {
        System.ComponentModel.BackgroundWorker worker = Get(form, "scanner") as System.ComponentModel.BackgroundWorker;
        return !(bool)Get(form, "libBuilding") && (worker == null || !worker.IsBusy) &&
            Count("preScanLeft") == 0 && Count("integLeft") == 0 && Count("durScanLeft") == 0 &&
            !Alive("preparaRepasoTh") && !Alive("preScanTh") && !Alive("durScanTh");
    }
    static bool ConflictingName(string name, string own)
    {
        return name.StartsWith("Rocola", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("estrellasesionreal", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("sesionreal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, own, StringComparison.OrdinalIgnoreCase);
    }
    static void NoOtherRocola()
    {
        using (Process own = Process.GetCurrentProcess())
        foreach (Process process in Process.GetProcesses())
        using (process)
        {
            if (process.Id == own.Id) continue;
            string name;
            try { name = process.ProcessName; }
            catch (InvalidOperationException) { continue; } // termino durante la enumeracion
            if (ConflictingName(name, own.ProcessName)) throw new InvalidOperationException("Hay otra Rocola o prueba abierta. No se cierra ni se mide en paralelo.");
        }
    }
    static void Observe(string message)
    {
        if (!measuring || !message.StartsWith("fluidez GPU:")) return;
        object data = Get(panel, "fluidez");
        double a = Number(data, "P95"), b = Number(data, "P99");
        p95min = Math.Min(p95min, a); p95max = Math.Max(p95max, a);
        p99min = Math.Min(p99min, b); p99max = Math.Max(p99max, b);
        maximum = Math.Max(maximum, Number(data, "Maximo")); outside = Math.Max(outside, Number(data, "FueraRenderMax"));
        schedule = Math.Max(schedule, Number(data, "AgendaMax")); prepare = Math.Max(prepare, Number(data, "PrepararMax"));
        draw = Math.Max(draw, Number(data, "DibujarMax")); present = Math.Max(present, Number(data, "PresentarMax")); reports++;
    }
    static string F(double x) { return x.ToString("0.000", CultureInfo.InvariantCulture); }
    static string ValidateData(string path)
    {
        string data = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar), temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!data.StartsWith(temp, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Datos fuera de TEMP: no se permite escribir en produccion.");
        for (DirectoryInfo d = new DirectoryInfo(data); d != null && !string.Equals(d.FullName.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, temp, StringComparison.OrdinalIgnoreCase); d = d.Parent)
            if ((d.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("No se permiten enlaces de datos hacia produccion.");
        System.Collections.Generic.Stack<DirectoryInfo> pending = new System.Collections.Generic.Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(data));
        while (pending.Count > 0)
        foreach (FileSystemInfo item in pending.Pop().GetFileSystemInfos())
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("No se permiten datos enlazados.");
            DirectoryInfo child = item as DirectoryInfo; if (child != null) pending.Push(child);
        }
        if (!File.Exists(Path.Combine(data, "rocola_biblioteca.txt"))) throw new ArgumentException("Falta copia de la biblioteca.");
        string config = File.ReadAllText(Path.Combine(data, "rocola_config.txt"));
        string[] lines = config.Replace("\r", "").Split('\n'); bool disabled = false;
        foreach (string line in lines) if (line.Trim().StartsWith("ia_datos="))
        {
            if (line.Trim() != "ia_datos=0") throw new ArgumentException("La cosecha IA debe estar desactivada en la COPIA.");
            disabled = true;
        }
        if (!disabled) throw new ArgumentException("Falta ia_datos=0 en la COPIA.");
        return data;
    }
    static bool Idle { get { return (mode & 1) != 0; } }
    static string Label { get { return (mode < 4 ? "Legacy" : "Natural") + ((mode & 2) == 0 ? " 2D" : " 3D") + (Idle ? " extendido" : " navegacion quieta"); } }
    static void Next()
    {
        mode++; measuring = false; reports = 0;
        if (mode == 8) { form.Close(); return; }
        Set(form, "estiloFondo", mode < 4 ? 0 : 1); Set(form, "estrella3d", (mode & 2) != 0);
        Set(form, "lunaActiva", true); Set(form, "estadoExpandido", true); Set(form, "muroReposo", true);
        Call(form, "AplicaEfectosGpu");
        Call(form, "PlayTrackForMeasurement", track); // misma pista desde cero, DSP real y salida muda
        if (Get(form, "output") == null || !ReferenceEquals(Get(form, "nowPlaying"), track))
            throw new InvalidOperationException("No se abrio la misma cancion real muda.");
        Set(panel, "ShowFps", true); Touch(Idle); stage.Reset(); stage.Start();
    }
    static void Tick(object sender, EventArgs args)
    {
        try
        {
            if (mode < 0)
            {
                Touch(true); // permitir que el repaso normal termine durante la PREPARACION
                if (stage.Elapsed.TotalMinutes > 15) throw new TimeoutException("El repaso no termino; no se certifica una escena aislada.");
                if (!Settled()) return;
                IList tracks = (IList)Get(form, "tracks");
                if (tracks.Count == 0) return;
                if ((bool)Get(form, "attractOn")) Call(form, "ExitAttract");
                panel = Get(form, "gpuPanel");
                if (panel == null || !((Control)panel).Visible) throw new InvalidOperationException("GPU no activa.");
                Set(panel, "ReportFluidez", (Action<string>)Observe); Next(); return;
            }
            if (!Settled()) throw new InvalidOperationException("Se reactivo el repaso o el escaneo; escena no valida.");
            if (!ReferenceEquals(Get(form, "nowPlaying"), track) || Get(form, "output") == null)
                throw new InvalidOperationException("Cambio/finalizo la cancion durante la escena.");
            if ((bool)Get(form, "attractOn")) throw new InvalidOperationException("Entro en atraccion.");
            if (form.WindowState == FormWindowState.Minimized) throw new InvalidOperationException("Ventana minimizada: escena no valida.");
            Touch(Idle);
            float blend = (float)Get(panel, "idleBlend");
            if (!measuring)
            {
                if (stage.ElapsedMilliseconds < 5000 || (Idle ? blend < .997f : blend > .003f)) return;
                NoOtherRocola(); Call(panel, "ReiniciaMedida");
                sessionGeneration = (long)Get(Get(panel, "fluidez"), "GeneracionSesion");
                p95min = p99min = double.MaxValue; p95max = p99max = maximum = outside = schedule = prepare = draw = present = 0;
                reports = 0; measuring = true; stage.Reset(); stage.Start(); return;
            }
            if (stage.ElapsedMilliseconds < 25000) return;
            measuring = false; double elapsed = stage.Elapsed.TotalMilliseconds; NoOtherRocola();
            int frames = (int)panel.GetType().GetProperty("CuadrosMedidos", B).GetValue(panel, null);
            int failures = (int)panel.GetType().GetProperty("FallosMedidos", B).GetValue(panel, null);
            object session = ValidSession(frames, failures), worst = Get(session, "PeorIntervaloSesion");
            if (reports < 15) throw new InvalidOperationException("Informes insuficientes; no certificar.");
            Console.WriteLine("REAL " + Label + " resolucion=" + ((Control)panel).Width + "x" + ((Control)panel).Height +
                " cuadros=" + frames + " fallos=" + failures + " presentaciones_s=" + F(frames * 1000 / elapsed) +
                " informes=" + reports + " ventanas_p95_ms=" + F(p95min) + ".." + F(p95max) + " ventanas_p99_ms=" + F(p99min) + ".." + F(p99max) +
                " ventanas_max_ms=" + F(maximum) + " fueraRender_max_ms=" + F(outside) + " agenda_max_ms=" + F(schedule) +
                " preparar_dibujar_presentar_max_ms=" + F(prepare) + "/" + F(draw) + "/" + F(present) +
                " intervalos_sesion=" + Get(session, "IntervalosSesion") + " episodios_sesion=" + Get(session, "EpisodiosSesion") +
                " episodios_gc_coincidente=" + Get(session, "EpisodiosGcSesion") + " max_sesion_ms=" + F(Number(session, "MaximoSesion")) +
                " peor_preparar_dibujar_presentar_ms=" + F(Number(worst, "Preparar")) + "/" + F(Number(worst, "Dibujar")) + "/" + F(Number(worst, "Presentar")) +
                " peor_fueraRender_agenda_ms=" + F(Number(worst, "FueraRender")) + "/" + F(Number(worst, "Agenda")) +
                " peor_gc=" + Get(worst, "Gc0") + "/" + Get(worst, "Gc1") + "/" + Get(worst, "Gc2") + " repaso_pendiente=0");
            Next();
        }
        catch (Exception e) { errors++; Console.Error.WriteLine(e); ((System.Windows.Forms.Timer)sender).Stop(); form.Close(); }
    }
    static int Self(Assembly asm)
    {
        int checks = 0;
        try
        {
            form = (Form)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(asm.GetType("Rocola.Form1", true));
            if (!Settled()) throw new Exception("fixture al dia"); checks++;
            foreach (string name in new string[] { "preScanLeft", "integLeft", "durScanLeft" })
            {
                Set(form, name, 1); if (Settled()) throw new Exception("no detecto " + name); checks++; Set(form, name, 0);
            }
            Set(form, "libBuilding", true); if (Settled()) throw new Exception("escaneo aun activo"); checks++; Set(form, "libBuilding", false);
            using (System.ComponentModel.BackgroundWorker worker = new System.ComponentModel.BackgroundWorker())
            {
                Set(form, "scanner", worker); if (!Settled()) throw new Exception("worker inactivo"); checks++;
            }
            Set(form, "scanner", null);
            using (ManualResetEvent stop = new ManualResetEvent(false))
            {
                Thread thread = new Thread(delegate() { stop.WaitOne(); }); thread.IsBackground = true; thread.Start();
                try
                {
                    foreach (string name in new string[] { "preparaRepasoTh", "durScanTh" })
                    { Set(form, name, thread); if (Settled()) throw new Exception("hilo pendiente " + name); checks++; Set(form, name, null); }
                    Set(form, "preScanTh", new Thread[] { null, thread }); if (Settled()) throw new Exception("carril vivo"); checks++;
                }
                finally { stop.Set(); thread.Join(); }
                if (!Settled()) throw new Exception("carriles ya terminados"); checks++;
            }
            using (Control p = (Control)Activator.CreateInstance(asm.GetType("Rocola.GpuPanel", true), true))
            {
                panel = p; object measure = Get(p, "fluidez");
                Set(measure, "P95", 6d); Set(measure, "P99", 22d); Set(measure, "Maximo", 48d); Set(measure, "FueraRenderMax", 42d);
                p95min = p99min = double.MaxValue; measuring = true;
                Observe("fluidez GPU: prueba"); if (reports != 1 || p95min != 6 || p99max != 22 || maximum != 48 || outside != 42) throw new Exception("captura de ventana"); checks++;
                Observe("episodios GPU:"); measuring = false; Observe("fluidez GPU:"); if (reports != 1) throw new Exception("calentamiento/episodios no son ventanas"); checks++;
                Set(measure, "IntervalosSesion", 99L);
                sessionGeneration = (long)Get(measure, "GeneracionSesion");
                if (!ReferenceEquals(ValidSession(100, 0), measure)) throw new Exception("sesion completa"); checks++;
                foreach (int[] invalid in new int[][] { new int[] { 100, 1 }, new int[] { 0, 0 }, new int[] { 1, 0 }, new int[] { 120, 0 } })
                {
                    bool rejected = false;
                    try { ValidSession(invalid[0], invalid[1]); } catch (InvalidOperationException) { rejected = true; }
                    if (!rejected) throw new Exception("certifico fallo/sesion reiniciada"); checks++;
                }
                Set(measure, "GeneracionSesion", sessionGeneration + 1);
                bool resetRejected = false;
                try { ValidSession(100, 0); } catch (InvalidOperationException) { resetRejected = true; }
                if (!resetRejected) throw new Exception("reset simultaneo de ambos contadores oculto"); checks++;
            }
            for (mode = 0; mode < 8; mode++)
            { if (Idle != ((mode & 1) != 0) || Label.IndexOf(mode < 4 ? "Legacy" : "Natural") < 0) throw new Exception("matriz"); checks++; }
            foreach (string name in new string[] { "Rocola", "rocola-gpu", "Rocola-cpu", "estrellasesionreal-hd", "estrellasesionreal-modest", "sesionreal", "otraPrueba" })
            { if (!ConflictingName(name, "otraPrueba")) throw new Exception("prueba simultanea de otro nombre no detectada"); checks++; }
            if (ConflictingName("explorer", "otraPrueba")) throw new Exception("proceso ajeno bloquea"); checks++;
            try { ValidateData(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); throw new Exception("acepto datos originales"); }
            catch (ArgumentException) { checks++; }
            Console.WriteLine("PASS estrellasesionreal: " + checks + " controles sinteticos de preparacion/captura/matriz/aislamiento. No mide FPS reales.");
            return 0;
        }
        finally { form = null; panel = null; measuring = false; }
    }
    [STAThread]
    static int Main(string[] args)
    {
        string old = Environment.GetEnvironmentVariable("ROCOLA_DATOS");
        try
        {
            if (args.Length == 2 && args[0] == "--autoprueba") return Self(Assembly.LoadFrom(Path.GetFullPath(args[1])));
            if (args.Length != 3) throw new ArgumentException("estrellasesionreal.exe <Rocola.exe> <datos COPIADOS en TEMP> <ruta de pista real de esa biblioteca>");
            NoOtherRocola();
            string data = ValidateData(args[1]);
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", data);
            Assembly asm = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            asm.GetType("Rocola.NLayerIncluido", true).GetMethod("Engancha", B).Invoke(null, null);
            asm.GetType("Rocola.Program", true).GetMethod("PrioridadDeLaRocola", B).Invoke(null, null);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            form = (Form)Activator.CreateInstance(asm.GetType("Rocola.Form1", true)); Set(form, "stateRestored", true);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { errors++; Console.Error.WriteLine(e.Exception); form.Close(); };
            // Seleccionarla despues del escaneo, no fabricar una ficha ajena a la biblioteca.
            string wanted = Path.GetFullPath(args[2]);
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer())
            {
                timer.Interval = 100;
                timer.Tick += delegate(object sender, EventArgs e)
                {
                    if (track == null && !(bool)Get(form, "libBuilding"))
                    {
                        foreach (object item in (IList)Get(form, "tracks"))
                            if (string.Equals((string)Get(item, "Path"), wanted, StringComparison.OrdinalIgnoreCase)) { track = item; break; }
                        if (Settled() && ((IList)Get(form, "tracks")).Count > 0 && track == null)
                        { errors++; Console.Error.WriteLine("La pista no pertenece a la copia de biblioteca."); timer.Stop(); form.Close(); return; }
                    }
                    Tick(sender, e);
                };
                stage.Start(); timer.Start(); Application.Run(form); timer.Stop();
            }
            if (mode != 8) errors++;
            Console.WriteLine("FIN escenas=" + mode + " errores=" + errors + "; intervalos de cuadros presentados, no fotones ni certificacion XP.");
            return errors == 0 ? 0 : 1;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
        finally { if (form != null) form.Dispose(); Environment.SetEnvironmentVariable("ROCOLA_DATOS", old); }
    }
}

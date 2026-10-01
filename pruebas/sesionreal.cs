using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Integracion opcional, con datos COPIADOS: abre la aplicacion completa y su medidor.
// No se incluye en el runner sintetico ni debe apuntar a datos de produccion.
class SesionReal
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type formType;
    static Form form;
    static int errors;
    static string pending = "";
    static object Get(object owner, string name) { return owner.GetType().GetField(name, B).GetValue(owner); }
    static void Set(object owner, string name, object value) { owner.GetType().GetField(name, B).SetValue(owner, value); }
    static object Call(object owner, string name, params object[] args) { return owner.GetType().GetMethod(name, B).Invoke(owner, args); }
    static void Pump(int ms)
    {
        Touch();
        Stopwatch clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < ms) { Call(Get(form, "gpuPanel"), "DibujaUnCuadro"); Application.DoEvents(); Thread.Sleep(1); }
    }
    static void Touch()
    {
        Set(form, "lastInput", DateTime.Now); Set(form, "lastInputTick", Environment.TickCount);
        if ((bool)Get(form, "attractOn")) Call(form, "ExitAttract");
    }
    static bool Ready()
    {
        object view = Get(form, "albumView"), gpu = Get(form, "gpuPanel");
        if (view == null || gpu == null) return false;
        IList page = (IList)Get(view, "Page");
        if (page.Count == 0) return false;
        Delegate peek = (Delegate)Get(view, "PeekCoverGpu");
        IDictionary textures = (IDictionary)Get(gpu, "cellTexCache");
        IDictionary images = (IDictionary)Get(view, "gpuCellCache");
        for (int i = 0; i < page.Count; i++)
        {
            object album = page[i];
            string coverPath = (string)Get(album, "CoverPath");
            if (String.IsNullOrEmpty(coverPath)) continue; // discos sin caratula no son fallos de carga
            if (peek.DynamicInvoke(album) == null) { pending = "portada: " + coverPath; return false; }
            string key = (string)Call(view, "ClaveCasilla", album);
            if (key == null || !textures.Contains(key) || !images.Contains(key)) { pending = "cache: " + key; return false; }
            object tex = textures[key], image = images[key];
            if ((IntPtr)Get(tex, "Tex") == IntPtr.Zero || !Object.ReferenceEquals(Get(tex, "Bmp"), Get(image, "Bmp"))) { pending = "subida: " + key; return false; }
        }
        return true;
    }
    static double WaitReady()
    {
        Touch();
        Stopwatch clock = Stopwatch.StartNew();
        while (!Ready())
        {
            if (clock.ElapsedMilliseconds > 10000) throw new Exception("Pagina sin todas sus portadas/texturas tras 10 segundos: " + pending);
            Call(Get(form, "gpuPanel"), "DibujaUnCuadro"); Application.DoEvents(); Thread.Sleep(1);
        }
        return clock.Elapsed.TotalMilliseconds;
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2 || args.Length > 3 || (args.Length == 3 && args[2] != "--solo-paginas")) throw new ArgumentException("sesionreal.exe <Rocola.exe> <datos COPIADOS en TEMP> [--solo-paginas]");
            string data = Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!data.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(data, "rocola_biblioteca.txt")))
                throw new ArgumentException("Solo acepta una copia de biblioteca en TEMP; nunca datos de produccion");
            string config = File.ReadAllText(Path.Combine(data, "rocola_config.txt"));
            if (config.IndexOf("ia_datos=0", StringComparison.Ordinal) < 0 || config.IndexOf("ia_datos=1", StringComparison.Ordinal) >= 0)
                throw new ArgumentException("Desactivar recogida IA en la copia antes de medir");
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", data);
            Assembly asm = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Type decoder = asm.GetType("Rocola.NLayerIncluido");
            if (decoder != null) decoder.GetMethod("Engancha", B).Invoke(null, null);
            Type program = asm.GetType("Rocola.Program");
            program.GetMethod("PrioridadDeLaRocola", B).Invoke(null, null);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { errors++; Console.WriteLine("ERROR UI: " + e.Exception); };
            formType = asm.GetType("Rocola.Form1");
            using (form = (Form)Activator.CreateInstance(formType))
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer())
            {
                Set(form, "stateRestored", true); // no reanudar cola/cancion de otra sesion de la copia
                Stopwatch start = Stopwatch.StartNew(); int readyAt = -1;
                timer.Interval = 100;
                timer.Tick += delegate
                {
                    try
                    {
                        IList tracks = (IList)Get(form, "tracks");
                        if (tracks.Count == 0)
                        {
                            if (start.ElapsedMilliseconds > 180000) throw new Exception("La biblioteca no quedo lista en tres minutos");
                            return;
                        }
                        if (readyAt < 0) { readyAt = Environment.TickCount; return; }
                        if (Environment.TickCount - readyAt < 3000) return;
                        timer.Stop();
                        Control gpu = (Control)Get(form, "gpuPanel");
                        if (gpu == null || !gpu.Visible) throw new Exception("No esta activo el renderizador GPU");
                        Console.WriteLine("RESOLUCION GPU " + gpu.Width + "x" + gpu.Height + "; canciones=" + tracks.Count);
                        // La seccion # puede tener menos de una pagina: medir C, con suficientes discos.
                        Touch(); Set(form, "currentLetter", 'C'); Set(form, "page", 0); Call(form, "RefreshAlbums"); WaitReady();
                        if (args.Length == 2)
                        {
                            Call(form, "MideRendimiento");
                            object failure = Get(form, "bancoError");
                            if (failure != null) throw new Exception("Medidor: " + failure);
                            Console.WriteLine("AUDIO_REAL_MUDO " + Get(form, "bancoConMusica"));
                            foreach (object scene in (IEnumerable)Get(form, "bancoEscenas"))
                                Console.WriteLine("ESCENA " + Get(scene, "Nombre") + "; trabajo_ms=" + ((double)Get(scene, "MsTrabajo")).ToString("F3", CultureInfo.InvariantCulture) + "; presentaciones_s=" + Get(scene, "Fps"));
                        }
                        // Otro recorrido de paginas, al ritmo normal, incluyendo carga/asignacion real de texturas.
                        Touch(); Set(form, "currentLetter", 'C'); Set(form, "page", 0); Call(form, "RefreshAlbums"); WaitReady();
                        for (int i = 0; i < 6; i++)
                        {
                            Pump(1000);
                            Stopwatch input = Stopwatch.StartNew(); Call(form, "NextPage"); double callMs = input.Elapsed.TotalMilliseconds;
                            double readyMs = WaitReady();
                            Console.WriteLine("PAGINA " + Get(form, "page") + "; input_ms=" + callMs.ToString("F3", CultureInfo.InvariantCulture) + "; espera_texturas_ms=" + readyMs.ToString("F3", CultureInfo.InvariantCulture));
                        }
                        Console.WriteLine("FIN errores_UI=" + errors + "; no mide fotones ni demuestra carga instantanea universal");
                    }
                    catch (Exception e) { timer.Stop(); errors++; Console.WriteLine("FALLO: " + e); }
                    finally { if (!timer.Enabled) form.Close(); }
                };
                timer.Start(); Application.Run(form);
            }
            return errors == 0 ? 0 : 1;
        }
        catch (Exception e) { Console.WriteLine("FALLO: " + e); return 1; }
    }
}

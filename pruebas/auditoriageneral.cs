using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;

// Regresiones de la auditoria: datos artificiales, sin abrir la Rocola ni el audio.
class AuditoriaGeneral
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, search, item; static Assembly app;
    static object chosen; static Image cover; static int n;
    static object Get(object o, string f) { return o.GetType().GetField(f, B).GetValue(o); }
    static void Set(object o, string f, object v) { o.GetType().GetField(f, B).SetValue(o, v); }
    static object Call(object o, string m, params object[] a) { return o.GetType().GetMethod(m, B).Invoke(o, a); }
    static object Static(Type t, string m, params object[] a) { return t.GetMethod(m, B).Invoke(null, a); }
    static void Check(bool c, string m) { n++; if (!c) throw new Exception(m); }
    static void Choose<T>(T v) { chosen = v; }
    static Image Peek<T>(T a) { return cover; }
    static object Item(string title) { object o = Activator.CreateInstance(item, true); Set(o, "Kind", 1); Set(o, "Display", title); Set(o, "Key", title.ToLowerInvariant()); return o; }
    static int Hash(Bitmap b) { int h = 0; unchecked { for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) h = h * 31 + b.GetPixel(x, y).ToArgb(); } return h; }
    sealed class Clock : IWavePosition, IWavePlayer
    {
        public long Bytes; public WaveFormat Format;
        public long GetPosition() { return Bytes; }
        public WaveFormat OutputWaveFormat { get { return Format; } }
        public PlaybackState PlaybackState { get { return PlaybackState.Playing; } }
        public float Volume { get; set; }
        public event EventHandler<StoppedEventArgs> PlaybackStopped { add { } remove { } }
        public void Init(IWaveProvider p) { } public void Play() { } public void Stop() { } public void Pause() { } public void Dispose() { }
    }
    sealed class Empty : ISampleProvider
    {
        public WaveFormat WaveFormat { get { return WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); } }
        public int Read(float[] b, int o, int c) { return 0; }
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            app = Assembly.LoadFrom(args[0]); form = app.GetType("Rocola.Form1", true);
            string dir = Path.Combine(Path.GetTempPath(), "rocola-auditoria-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
            Instancias(dir); Estado(dir); Portada(dir); Reloj(); Cierre();
            if (app.GetType("Rocola.GpuPanel") != null) { Busca(); Duraciones(); }
            Console.WriteLine("PASS auditoria general: " + n + " comprobaciones; sin musica ni datos del usuario."); return 0;
        }
        catch (Exception ex) { Console.WriteLine("FALLO " + ex); return 1; }
    }
    static void Instancias(string dir)
    {
        Type program = app.GetType("Rocola.Program"); object key = Static(program, "ClaveInstancia", dir);
        foreach (string alias in new string[] { Path.Combine(dir, "."), dir + Path.DirectorySeparatorChar, dir.ToUpperInvariant(), dir.Replace('\\', '/') })
            Check(key.Equals(Static(program, "ClaveInstancia", alias)), "alias de carpeta usa otro mutex");
        form.GetField("carpetaDatos", B).SetValue(null, null);
        using (FileStream hold = new FileStream(Path.Combine(dir, "rocola_escribible.tmp"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            Check(string.Equals(dir, (string)form.GetProperty("CarpetaDatos", B).GetValue(null, null), StringComparison.OrdinalIgnoreCase), "sonda ocupada cambia la carpeta");
        Check(Directory.GetFiles(dir, "rocola_escribible_*.tmp").Length == 0, "sonda temporal no liberada");
    }
    static void Estado(string dir)
    {
        object f = Codigo.FormSinEncender(form); string path = (string)Call(f, "StateFile");
        Call(f, "EscribeEstado", "creditos=2\r\n"); Codigo.EsperaGuardado(form, f);
        using (FileStream hold = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            Call(f, "EscribeEstado", "creditos=6\r\n"); Codigo.EsperaGuardado(form, f);
            Check(File.ReadAllText(path) == "creditos=2\r\n", "fallo reemplaza el estado bueno");
            Check((int)Get(f, "estadoPedido") > (int)Get(f, "estadoHecho"), "fallo se confirma como guardado");
            Check(((string[])Call(f, "EstadoSinEscribir"))[0] == "creditos=6", "fallo pierde la instantanea pendiente");
        }
        DateTime until = DateTime.UtcNow.AddSeconds(6);
        while ((int)Get(f, "estadoHecho") < (int)Get(f, "estadoPedido") && DateTime.UtcNow < until) Thread.Sleep(20);
        Check(File.ReadAllText(path) == "creditos=6\r\n", "no reintenta tras liberar el archivo");
        int viejo = (int)Get(f, "estadoPedido");
        Call(f, "EscribeEstado", "creditos=10\r\n"); Call(f, "EncolaEstado", viejo, "creditos=6\r\n"); Codigo.EsperaGuardado(form, f);
        Check(File.ReadAllText(path) == "creditos=10\r\n", "reintento viejo pisa un encargo nuevo");
        using (FileStream hold = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        { Call(f, "EscribeEstado", "creditos=14\r\n"); Codigo.EsperaGuardado(form, f); Call(f, "BorraEstado"); }
        Thread.Sleep(600); Codigo.EsperaGuardado(form, f);
        Check(!File.Exists(path) && !File.Exists(path + ".bak"), "reintento resucita estado borrado");
        Call(f, "DetieneEstadoReintentos"); Call(f, "EscribeEstado", "cerrado"); Codigo.EsperaGuardado(form, f);
        Check(!File.Exists(path), "se encarga trabajo despues del cierre");
        Check(Get(f, "estadoReintento") == null, "temporizador sigue activo tras cierre");
    }
    static void Portada(string dir)
    {
        string original = Path.Combine(dir, "portada.png"), cache = Path.Combine(dir, "cache.png");
        using (Bitmap b = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++)
                b.SetPixel(x, y, Color.FromArgb((x + y) % 256, x, y, (x * 17 + y * 13) % 256));
            b.Save(original, ImageFormat.Png);
        }
        using (Bitmap a = (Bitmap)Static(form, "DecodeCoverWithIdentity", original, cache))
        using (Bitmap b = (Bitmap)Static(form, "DecodeCoverWithIdentity", original, cache))
        {
            Check(File.Exists(cache), "miniatura no publicada");
            Check(a.Width == b.Width && a.Height == b.Height, "cache cambia el tamano");
            int maximo = 0;
            // PNG guarda RGB recto. Al volver a premultiplicar puede haber un nivel
            // de redondeo, pero nunca compresion con perdida ni cambio de opacidad.
            for (int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)
            {
                Color c=a.GetPixel(x,y), d=b.GetPixel(x,y);
                Check(c.A == d.A,"cache cambia alfa");
                maximo=Math.Max(maximo,Math.Abs(c.R*c.A/255-d.R*d.A/255));
                maximo=Math.Max(maximo,Math.Abs(c.G*c.A/255-d.G*d.A/255));
                maximo=Math.Max(maximo,Math.Abs(c.B*c.A/255-d.B*d.A/255));
            }
            Check(maximo <= 1,"cache pierde detalle visible de color: "+maximo);
            Check(a.GetPixel(0, 0).A == 0 && b.GetPixel(0, 0).A == 0, "cache hace negro el fondo transparente");
        }
        Check(((string)Static(form, "ThumbCachePath", original)).EndsWith("_256_v2.png"), "cache nueva mezcla identidades antiguas");
    }
    static void Reloj()
    {
        foreach (WaveFormat fmt in new WaveFormat[] { new WaveFormat(44100, 16, 2), WaveFormat.CreateIeeeFloatWaveFormat(44100, 2) })
        {
            object f = Codigo.FormSinEncender(form); Set(f, "latenciaClasica", true);
            Clock c = new Clock(); c.Format = fmt;
            Check((long)Call(f, "AmpliaRelojSalida", c, 0xFFFFFFF0L) == 0xFFFFFFF0L, "reloj antes del wrap");
            Check((long)Call(f, "AmpliaRelojSalida", c, 0x20L) == 0x100000020L, "primer wrap de WinMM");
            Check((long)Call(f, "AmpliaRelojSalida", c, 0xFFFFFFF0L) == 0x1FFFFFFF0L, "segunda vuelta antes del wrap");
            Check((long)Call(f, "AmpliaRelojSalida", c, 0x40L) == 0x200000040L, "segundo wrap de WinMM");
            Check((long)Call(f, "AmpliaRelojSalida", c, 0x30L) == -1L, "reinicio corto confundido con wrap");
            Check((long)Call(f, "AmpliaRelojSalida", c, 0x50L) == -1L, "reloj reiniciado se vuelve a dar por valido");
            Check((long)Call(f, "AmpliaRelojSalida", new Clock(), 0x50L) == 0x50L, "salida nueva conserva la vuelta anterior");
            Set(f, "latenciaClasica", false);
            Check((long)Call(f, "AmpliaRelojSalida", new Clock(), 0x200000040L) == 0x200000040L, "trunca reloj nativo de 64 bits");
        }
        object owner = Codigo.FormSinEncender(form); Set(owner, "latenciaClasica", true);
        Type seq = app.GetType("Rocola.Encadenado");
        object s = Activator.CreateInstance(seq, B, null, new object[] { new Empty(), new object() }, null);
        Set(owner, "secuencia", s); Clock clock = new Clock { Format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2) }; Set(owner, "output", clock);
        double previo = 0;
        foreach (long bytes in new long[] { 0xFFFFFFF0L, 0x100000020L })
        {
            Set(s, "muestras", (bytes / 8 + 44100) * 2); clock.Bytes = bytes & 0xFFFFFFFFL;
            object[] p = new object[] { 0d, 0d };
            Check((bool)form.GetMethod("PendientePorReloj", B).Invoke(owner, p), "reloj de continuidad invalido tras 4 GiB");
            Check((double)p[1] >= previo && Math.Abs((double)p[0] - 1) < .01, "tiempo oido discontinuo"); previo = (double)p[1];
        }
    }
    static void Cierre()
    {
        object f = Codigo.FormSinEncender(form); Keys key = Keys.Control | Keys.Alt | Keys.Q;
        // El bit 30 de lParam es la repeticion de la MISMA pulsacion, no una pulsacion nueva.
        object[] p = new object[] { Message.Create(IntPtr.Zero, 0x100, (IntPtr)Keys.Q, new IntPtr(0x40000000)), key };
        for (int i = 0; i < 20; i++) Check((bool)form.GetMethod("ProcessCmdKey", B).Invoke(f, p), "repeticion no consumida");
        Check((int)Call(f, "PidenSalir", key) == 1, "una tecla mantenida cuenta como tres pulsaciones");
    }
    static void Busca()
    {
        search = form.GetNestedType("SearchOverlay", B); item = form.GetNestedType("SearchItem", B);
        using (Control view = (Control)Activator.CreateInstance(search, true))
        {
            view.Size = new Size(1024, 680); Call(view, "PonNuevo", true); Call(view, "AseguraReparto");
            IList results = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item)); object first = Item("FIRST"), second = Item("SECOND"); results.Add(first); results.Add(second); Set(view, "Results", results);
            Set(view, "OnChoose", Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(item), typeof(AuditoriaGeneral).GetMethod("Choose", B).MakeGenericMethod(item)));
            Rectangle r = (Rectangle)Call(view, "CartaRect", 1); Call(view, "ClickAt", new Point(r.X + r.Width / 2, r.Y + r.Height / 2));
            Check(ReferenceEquals(chosen, second) && (int)Get(view, "SelResult") == 1 && (bool)Get(view, "FocusResults"), "click no fija la tarjeta elegida");
            foreach (bool click in new bool[] { false, true })
            {
                Set(view, "FocusResults", true); Set(view, "SelResult", 0); Set(view, "Results", results); Set(view, "Query", "first"); Set(view, "Escrito", "firstx"); chosen = null;
                if (click) { r = (Rectangle)Call(view, "CartaRect", 0); Call(view, "ClickAt", new Point(r.X + r.Width / 2, r.Y + r.Height / 2)); }
                else Call(view, "PressSelected");
                Check(chosen == null, "elige resultados de una consulta anterior");
                Check((string)Get(view, "Query") == "firstx", "no posa la consulta pendiente");
            }
        }
    }
    static void Duraciones()
    {
        object f = Codigo.FormSinEncender(form); Type album = form.GetNestedType("Album", B), track = form.GetNestedType("Track", B);
        using (Control view = (Control)Activator.CreateInstance(form.GetNestedType("AlbumView", B), true))
        {
            view.Size = new Size(1000, 500); Set(f, "albumView", view); Set(f, "gpuEnabled", true); Set(f, "portadaEnfocada", true);
            object a = Activator.CreateInstance(album, true), t = Activator.CreateInstance(track, true);
            Set(a, "Name", "TEST"); Set(a, "Artist", "ARTIST"); Set(t, "Path", "aislada.mp3"); Set(t, "Title", "SONG"); ((IList)Get(a, "Tracks")).Add(t);
            IList page = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(album)); page.Add(a); Set(view, "Page", page); Set(view, "Selected", 0);
            Call(f, "BuildFocoState"); int antes = (int)Get(f, "focoVer"), hash = Hash((Bitmap)Get(f, "focoBmp"));
            Call(f, "PublicaDuraciones", 0, new string[] { "aislada.mp3" }, new float[] { 240 }, 0);
            Call(f, "BuildFocoState");
            Check((int)Get(f, "focoVer") > antes && hash != Hash((Bitmap)Get(f, "focoBmp")), "resumen no muestra duraciones nuevas");
            int despues = (int)Get(f, "focoVer"); Call(f, "BuildFocoState"); Check((int)Get(f, "focoVer") == despues, "resumen se reconstruye sin cambios");
            Call(f, "PublicaDuraciones", 0, new string[] { "otro-disco.mp3" }, new float[] { 210 }, 0);
            Call(f, "BuildFocoState"); Check((int)Get(f, "focoVer") == despues, "medir otro disco rehace el resumen visible");
            Set(f, "fichaDisco", true);
            Set(view, "PeekCoverGpu", Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(album, typeof(Image)),
                typeof(AuditoriaGeneral).GetMethod("Peek", B).MakeGenericMethod(album)));
            Call(f, "BuildFichaState"); Set(f, "fichaDesde", Environment.TickCount - 1200); Call(f, "BuildFichaState");
            antes = (int)Get(f, "fichaVer"); hash = Hash((Bitmap)Get(f, "fichaBmp"));
            Call(f, "BuildFichaState"); Check((int)Get(f, "fichaVer") == antes, "ficha se reconstruye sin cambios");
            Call(f, "PublicaDuraciones", 0, new string[] { "otro-disco.mp3" }, new float[] { 220 }, 0);
            Call(f, "BuildFichaState"); Check((int)Get(f, "fichaVer") == antes, "medir otro disco rehace la ficha visible");
            // La medida completa sustituye a la estimacion rapida; esta ultima no
            // sobrescribe entradas existentes deliberadamente.
            Call(f, "PublicaMedida", "aislada.mp3", 0f, 265f, null);
            Call(f, "BuildFichaState"); Check((int)Get(f, "fichaVer") > antes && hash != Hash((Bitmap)Get(f, "fichaBmp")), "ficha conserva duraciones antiguas");
            antes = (int)Get(f, "fichaVer"); Set(f, "generoVer", (int)Get(f, "generoVer") + 1); Call(f, "BuildFichaState");
            Check((int)Get(f, "fichaVer") > antes, "ficha ignora metadatos nuevos");
            antes = (int)Get(f, "fichaVer"); view.Size = new Size(1400, 900); Call(f, "BuildFichaState");
            Check((int)Get(f, "fichaVer") > antes, "ficha ignora cambio de resolucion");
            using (Bitmap portada = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(portada)) g.Clear(Color.Magenta);
                antes = (int)Get(f, "fichaVer"); hash = Hash((Bitmap)Get(f, "fichaBmp")); cover = portada; Call(f, "BuildFichaState");
                Check((int)Get(f, "fichaVer") > antes && hash != Hash((Bitmap)Get(f, "fichaBmp")), "ficha no adopta portada que acaba de llegar");
                antes = (int)Get(f, "fichaVer"); Call(f, "BuildFichaState"); Check((int)Get(f, "fichaVer") == antes, "ficha rasteriza la misma portada repetidamente");
                cover = null;
            }
            ((Bitmap)Get(f, "focoBmp")).Dispose(); ((Bitmap)Get(f, "fichaBmp")).Dispose();
            Codigo.EsperaGuardado(form, f); Call(f, "DetieneEstadoReintentos");
        }
    }
}

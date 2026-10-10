using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;

// Sin iniciar la aplicacion, GPU, audio, hilos o archivos del usuario.
// Compara el reloj GPU cacheado con TextoFaltan y con el lector original; las lecturas no se cachean.
class RelojReposo
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, cacheType;
    static int checks;
    static void Check(bool yes, string what) { if (!yes) throw new Exception(what); checks++; }
    static object Get(object target, string name) { return target.GetType().GetField(name, B).GetValue(target); }
    static void Set(object target, string name, object value) { target.GetType().GetField(name, B).SetValue(target, value); }
    static object Call(Type t, object target, string name, params object[] args)
    { try { return t.GetMethod(name, B).Invoke(target, args); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Cache() { return Activator.CreateInstance(cacheType, true); }
    static string Text(object cache, object source, TimeSpan cur, TimeSpan total, string fallback)
    { return (string)Call(cacheType, cache, "Texto", source, cur, total, fallback); }
    static string Original(TimeSpan cur, TimeSpan total, string fallback)
    { return (string)Call(form, null, "TextoFaltan", cur, total, fallback); }

    static void Equivalencia()
    {
        object cache = Cache(), source = new object();
        long[] samples = { TimeSpan.MinValue.Ticks, -TimeSpan.TicksPerSecond, -1, 0, 1,
            TimeSpan.TicksPerSecond - 1, TimeSpan.TicksPerSecond, 59 * TimeSpan.TicksPerSecond,
            60 * TimeSpan.TicksPerSecond - 1, 60 * TimeSpan.TicksPerSecond, 599 * TimeSpan.TicksPerSecond,
            600 * TimeSpan.TicksPerSecond, TimeSpan.TicksPerHour, TimeSpan.MaxValue.Ticks - 1, TimeSpan.MaxValue.Ticks };
        foreach (long cur in samples) foreach (long total in samples)
            foreach (string fallback in new string[] { null, "", "etiqueta anterior" }) {
                TimeSpan a = TimeSpan.FromTicks(cur), b = TimeSpan.FromTicks(total);
                Check(Text(cache, source, a, b, fallback) == Original(a, b, fallback), "Formato/limites: " + cur + " / " + total);
            }
        Random random = new Random(411);
        for (int i = 0; i < 5000; i++) {
            long total = (long)(random.NextDouble() * TimeSpan.MaxValue.Ticks);
            long cur = (long)(random.NextDouble() * total);
            TimeSpan a = TimeSpan.FromTicks(cur), b = TimeSpan.FromTicks(total);
            Check(Text(cache, source, a, b, "x") == Original(a, b, "x"), "Formato de duraciones extremas");
            if (cur < TimeSpan.MaxValue.Ticks - 100) {
                a = TimeSpan.FromTicks(cur + 99);
                Check(Text(cache, source, a, b, "x") == Original(a, b, "x"), "Redondeo dentro del segundo");
            }
        }
        CultureInfo before = Thread.CurrentThread.CurrentCulture;
        try {
            CultureInfo custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            Thread.CurrentThread.CurrentCulture = custom;
            custom.NumberFormat.NegativeSign = "-";
            string a = Text(cache, source, TimeSpan.MaxValue, TimeSpan.MaxValue, "x");
            custom.NumberFormat.NegativeSign = "menos";
            string b = Text(cache, source, TimeSpan.MaxValue, TimeSpan.MaxValue, "x");
            Check(a != b && b == Original(TimeSpan.MaxValue, TimeSpan.MaxValue, "x"),
                "Mismo formato incluso si cambia el signo de una cultura en un minuto desbordado");
        } finally { Thread.CurrentThread.CurrentCulture = before; }
        Console.WriteLine("  OK formato, sentinelas y duraciones extremas");
    }

    static void Reutiliza()
    {
        object cache = Cache(), source = new object();
        TimeSpan total = TimeSpan.FromTicks(276 * TimeSpan.TicksPerSecond + 500);
        string a = Text(cache, source, TimeSpan.FromSeconds(61), total, "x");
        for (int i = 1; i < 180; i++)
            Check(Object.ReferenceEquals(a, Text(cache, source, TimeSpan.FromTicks(61 * TimeSpan.TicksPerSecond + i * 50000), total, "otra etiqueta")),
                "Reutiliza el texto en cada cuadro del mismo segundo");
        string b = Text(cache, source, TimeSpan.FromSeconds(62), total, "x");
        Check(a != b && !Object.ReferenceEquals(a, b), "Un segundo nuevo cambia el texto");
        a = Text(cache, source, TimeSpan.FromSeconds(61), total, "x");
        Check(a == "1:01 / 4:36", "Seek hacia atras no congela el reloj");
        b = Text(cache, source, TimeSpan.FromSeconds(61), TimeSpan.FromSeconds(277), "x");
        Check(b == "1:01 / 4:37", "Un total nuevo cambia el texto dentro del mismo segundo");
        a = Text(cache, source, TimeSpan.FromSeconds(61), total, "x");
        b = Text(cache, new object(), TimeSpan.FromSeconds(61), total, "x");
        Check(a == b && !Object.ReferenceEquals(a, b), "Otra fuente invalida la cache, aunque muestre los mismos numeros");
        a = Text(cache, source, TimeSpan.FromSeconds(300), total, "x");
        b = Text(cache, source, TimeSpan.FromSeconds(320), total, "x");
        Check(a == "4:36 / 4:36" && Object.ReferenceEquals(a, b), "El final conserva su limite y reutiliza el texto");
        a = Text(cache, source, TimeSpan.FromSeconds(61), total, "x");
        Check(Text(cache, source, TimeSpan.FromSeconds(61), TimeSpan.Zero, "sin duracion") == "sin duracion",
            "Un total cero nunca devuelve el texto valido anterior");
        b = Text(cache, source, TimeSpan.FromSeconds(61), total, "x");
        Check(a == b && !Object.ReferenceEquals(a, b), "Tras un total invalido no queda una entrada antigua");
        Check(Text(cache, source, TimeSpan.FromTicks(-1), total, "negativo") == "negativo", "Actual negativo conserva el fallback");
        a = Text(cache, source, TimeSpan.FromSeconds(61), total, "x");
        Call(cacheType, cache, "Vaciar");
        b = Text(cache, source, TimeSpan.FromSeconds(61), total, "x");
        Check(a == b && !Object.ReferenceEquals(a, b), "Vaciar no retiene la fuente ni el texto");
        Console.WriteLine("  OK cache por fuente y segundos, seek, fin y total");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static WeakReference FuenteTemporal(object cache)
    {
        object source = new object();
        Text(cache, source, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), "x");
        return new WeakReference(source);
    }
    static void NoRetieneLectores()
    {
        object cache = Cache();
        WeakReference source = FuenteTemporal(cache);
        object key = Get(cache, "fuente");
        Check(key is WeakReference, "La clave no es duena del lector");
        for (int i = 0; i < 3 && source.IsAlive; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Check(!source.IsAlive && Get(cache, "texto") != null, "El ultimo lector se libera aunque nadie vuelva a pedir el reloj");
        object active = new object();
        string a = Text(cache, active, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), "x");
        Check(Object.ReferenceEquals(key, Get(cache, "fuente")), "La referencia debil se reutiliza al cambiar de fuente");
        Check(Object.ReferenceEquals(a, Text(cache, active, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), "x")), "El lector activo conserva hits por identidad");
        Call(cacheType, cache, "Vaciar");
        Check(Object.ReferenceEquals(key, Get(cache, "fuente")) && ((WeakReference)key).Target == null && Get(cache, "texto") == null,
            "Vaciar borra la clave y texto sin otra asignacion");
        GC.KeepAlive(active); GC.KeepAlive(cache);
        Console.WriteLine("  OK no retiene lectores cerrados, clave debil reutilizable");
    }

    sealed class LectorMedido : AudioFileReader
    {
        // Se crea sin constructor: nunca se abre un archivo, codec o salida de sonido.
        private LectorMedido() : base("no se usa") { }
        internal WaveFormat Format;
        internal long Cur, Total;
        internal int CurReads, TotalReads;
        internal bool ThrowCur, ThrowTotal;
        public override WaveFormat WaveFormat { get { return Format; } }
        public override long Position {
            get { CurReads++; if (ThrowCur) throw new InvalidOperationException("lectura actual sintetica"); return Cur; }
            set { Cur = value; }
        }
        public override long Length {
            get { TotalReads++; if (ThrowTotal) throw new InvalidOperationException("lectura total sintetica"); return Total; }
        }
    }
    static object Fuente(LectorMedido stream)
    {
        Type t = form.GetNestedType("FileAudioSource", B);
        object source = FormatterServices.GetUninitializedObject(t); Set(source, "r", stream); return source;
    }
    static void Lecturas()
    {
        object owner = Codigo.FormSinEncender(form);
        using (Label label = new Label()) {
            label.Text = "etiqueta";
            Set(owner, "lblTime", label); Set(owner, "suenaGrande", true);
            Type itemType = form.GetField("nowPlaying", B).FieldType;
            object item = FormatterServices.GetUninitializedObject(itemType); Set(owner, "nowPlaying", item);
            LectorMedido stream = (LectorMedido)FormatterServices.GetUninitializedObject(typeof(LectorMedido));
            stream.Format = WaveFormat.CreateIeeeFloatWaveFormat(10000000, 1);
            stream.Cur = 61 * TimeSpan.TicksPerSecond * 4;
            stream.Total = 276 * TimeSpan.TicksPerSecond * 4;
            object source = Fuente(stream);
            string a = (string)Call(form, owner, "TextoRelojReposoCache", source);
            for (int i = 0; i < 180; i++)
                Check(Object.ReferenceEquals(a, Call(form, owner, "TextoRelojReposoCache", source)), "Hit real conserva string");
            Check(stream.CurReads == 181 && stream.TotalReads == 181,
                "Se leen actual y total exactamente una vez por cuadro aun con hit");
            stream.ThrowTotal = true;
            Check((string)Call(form, owner, "TextoRelojReposoCache", source) == "etiqueta", "Excepcion total no usa texto antiguo");
            Check(stream.CurReads == 182 && stream.TotalReads == 182, "Fallo total conserva el orden de lecturas");
            stream.ThrowTotal = false; label.Text = "otra etiqueta";
            stream.ThrowCur = true;
            Check((string)Call(form, owner, "TextoRelojReposoCache", source) == "otra etiqueta", "Excepcion actual usa etiqueta actual");
            Check(stream.CurReads == 183 && stream.TotalReads == 182, "Fallo actual no lee total, igual que antes");
            stream.ThrowCur = false;
            string b = (string)Call(form, owner, "TextoRelojReposoCache", source);
            Check(a == b && !Object.ReferenceEquals(a, b), "Una excepcion invalida la entrada cacheada");
            Set(owner, "suenaGrande", false);
            int reads = stream.CurReads;
            Check((string)Call(form, owner, "TextoRelojReposoCache", source) == "otra etiqueta" && stream.CurReads == reads,
                "Modo pequeño conserva etiqueta sin lecturas adicionales");
            Set(owner, "suenaGrande", true); Set(item, "IsVideo", true);
            Check((string)Call(form, owner, "TextoRelojReposoCache", source) == "otra etiqueta" && stream.CurReads == reads,
                "Video conserva etiqueta sin lecturas");
            Set(item, "IsVideo", false); Set(owner, "nowPlaying", null);
            Check((string)Call(form, owner, "TextoRelojReposoCache", source) == "otra etiqueta" && stream.CurReads == reads,
                "Sin cancion conserva etiqueta sin lecturas");
            Set(owner, "nowPlaying", item);
            Check((string)Call(form, owner, "TextoRelojReposoCache", new object[] { null }) == "otra etiqueta" && stream.CurReads == reads,
                "Sin fuente conserva etiqueta sin lecturas");
            stream.Total = 0;
            Check((string)Call(form, owner, "TextoRelojReposoCache", source) == "otra etiqueta", "Total invalido vuelve a la etiqueta");
            // SampleRate cero provoca division no finita dentro del lector. Su resultado debe coincidir con el camino anterior.
            stream.Format = WaveFormat.CreateIeeeFloatWaveFormat(0, 1); stream.Cur = 0;
            Check((string)Call(form, owner, "TextoRelojReposoCache", source) == (string)Call(form, owner, "TextoRelojReposo", source),
                "Fuente con cuenta no finita conserva el tratamiento anterior");
            Set(owner, "lblTime", null);
            Check((string)Call(form, owner, "TextoRelojReposoCache", new object[] { null }) == "", "Sin etiqueta devuelve cadena vacia");
        }
        Console.WriteLine("  OK lecturas reales, fuentes nulas y fallos sin IO/audio/GPU");
    }

    static void Pixeles()
    {
        object cache = Cache(), source = new object();
        foreach (Size size in new Size[] { new Size(480, 140), new Size(1024, 245), new Size(3440, 420) })
            foreach (int sec in new int[] { 0, 59, 60, 599, 600, 3601 }) {
                string expected = Original(TimeSpan.FromSeconds(sec), TimeSpan.FromSeconds(4000), "x");
                string text = Text(cache, source, TimeSpan.FromSeconds(sec), TimeSpan.FromSeconds(4000), "x");
                using (Bitmap reference = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb))
                using (Bitmap actual = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb))
                using (Graphics a = Graphics.FromImage(reference))
                using (Graphics b = Graphics.FromImage(actual)) {
                    a.Clear(Color.FromArgb(92, 23, 35, 61)); b.Clear(Color.FromArgb(92, 23, 35, 61));
                    a.TextRenderingHint = b.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    int x = (int)Call(form, null, "DrawIdleClock", a, size.Width, size.Height, expected);
                    int y = (int)Call(form, null, "DrawIdleClock", b, size.Width, size.Height, text);
                    Check(x == y, "La barra de progreso conserva su posicion");
                    bool same = true;
                    for (int row = size.Height - 34; row < size.Height && same; row++)
                        for (int col = 0; col < size.Width; col++)
                            if (reference.GetPixel(col, row) != actual.GetPixel(col, row)) { same = false; break; }
                    Check(same, "Reloj identico pixel a pixel");
                }
            }
        Console.WriteLine("  OK pixeles y posicion de la barra en 480, 1024 y 3440 px");
    }
    [STAThread]
    static int Main(string[] args)
    {
        try {
            Assembly assembly = Assembly.LoadFrom(args.Length > 0 ? args[0] : "Rocola.exe");
            form = assembly.GetType("Rocola.Form1", true);
            cacheType = assembly.GetType("Rocola.CacheRelojReposo");
            if (cacheType == null) {
                Check(form.GetMethod("TextoRelojReposoCache", B) == null, "Cache GPU ausente en CPU");
                Console.WriteLine("  OK CPU congelado: no existe la cache GPU"); return 0;
            }
            Equivalencia(); Reutiliza(); NoRetieneLectores(); Lecturas(); Pixeles();
            Check(Codigo.Llama(form.GetMethod("BuildIdlePanel", B), "TextoRelojReposoCache") &&
                Codigo.Llama(form.GetMethod("ParcheaElReloj", B), "TextoRelojReposoCache"), "Los dos caminos usan la cache");
            Check(Codigo.Llama(cacheType.GetMethod("Texto", B), "TextoFaltan"), "Formato central original intacto");
            Console.WriteLine("PASS relojreposo: " + checks + " comprobaciones, sin iniciar aplicacion/GPU/audio"); return 0;
        } catch (Exception e) { Console.WriteLine("FALLO: " + e); return 1; }
    }
}

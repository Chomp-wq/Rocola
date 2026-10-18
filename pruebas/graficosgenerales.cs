using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// .NET 3.5 + leercodigo.cs. Segundo argumento: release anterior o "medir".
// La comparacion contra otro release se omite expresamente si no se proporciona.
class GraficosGenerales
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type panel, star, vertex, oldPanel, oldStar, oldVertex;
    static long checks, bytes;
    static double checksum;
    delegate void Quad<T>(T[] v, float x0, float y0, float x1, float y1, float u, float vv, uint color);
    delegate void Halo<T>(T[] v, int at, float x, float y, float rx, float ry, uint color);
    delegate void Disc<T>(T[] v, ref int at, float x, float y, float radius, uint color);
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Run(object o, string n, params object[] a) { return Call(o.GetType(), o, n, a); }
    static MethodInfo HaloMethod(Type t)
    { return t.GetMethod("Halo", B, null, new Type[] { t.Assembly.GetType("Rocola.D3D9+Vtx").MakeArrayType(), typeof(int), typeof(float), typeof(float), typeof(float), typeof(float), typeof(uint) }, null); }
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static byte[] Bytes(Array a)
    {
        byte[] result = new byte[a.Length * Marshal.SizeOf(a.GetType().GetElementType())];
        GCHandle pin = GCHandle.Alloc(a, GCHandleType.Pinned);
        try { Marshal.Copy(pin.AddrOfPinnedObject(), result, 0, result.Length); } finally { pin.Free(); }
        return result;
    }
    static void Same(Array a, Array b, string label)
    {
        byte[] x = Bytes(a), y = Bytes(b); Check(x.Length == y.Length, label + " longitud");
        for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) throw new Exception(label + " byte=" + i);
        checks++; bytes += x.Length;
    }
    static float Next(Random r) { return (float)(r.NextDouble() * 5000 - 500); }
    static void Geometry()
    {
        Random random = new Random(630018);
        Array current = Array.CreateInstance(vertex, 120), previous = Array.CreateInstance(oldVertex, 120);
        for (int sample = 0; sample < 5000; sample++)
        {
            float x = Next(random), y = Next(random), w = Next(random), h = Next(random), u = (float)random.NextDouble(), v = (float)random.NextDouble();
            uint color = (uint)random.Next() ^ ((uint)random.Next() << 16);
            Call(panel, null, "SetTexQuadA", current, x, y, x+w, y+h, u, v, color);
            Call(oldPanel, null, "SetTexQuadA", previous, x, y, x+w, y+h, u, v, color); Same(current, previous, "quad");
            Call(panel, null, "SetTexQuad", current, x, y, x+w, y+h, u, v);
            Call(oldPanel, null, "SetTexQuad", previous, x, y, x+w, y+h, u, v); Same(current, previous, "quad blanco");
            HaloMethod(star).Invoke(null, new object[] { current, 3, x, y, w, h, color });
            HaloMethod(oldStar).Invoke(null, new object[] { previous, 3, x, y, w, h, color }); Same(current, previous, "halo");
            object[] a = { current, 6, x, y, sample % 5 == 0 ? .00001f : Math.Abs(w), color };
            object[] b = { previous, 6, x, y, a[4], color };
            Call(panel, null, "AsistenteDisco", a); Call(oldPanel, null, "AsistenteDisco", b);
            Check((int)a[1] == (int)b[1] && (int)a[1] == 114, "disco: mismo recuento"); Same(current, previous, "disco asistente");
        }
        Console.WriteLine("GEOMETRIA: 5000 quads, 5000 quads blancos, 5000 halos y 5000 discos identicos, incluidos prefijos y sufijos intactos.");
    }
    static void Guides()
    {
        Random random = new Random(186300);
        foreach (Size size in new Size[] { Size.Empty, new Size(16,16), new Size(1024,768), new Size(1280,1024), new Size(1920,1080), new Size(3840,2160) })
        for (int i = 0; i < 1000; i++)
        {
            RectangleF target = i == 0 ? RectangleF.Empty : new RectangleF(Next(random), Next(random), Next(random), Next(random));
            Rectangle a = (Rectangle)Call(panel, null, "AsistenteGuiaRect", size.Width, size.Height, target);
            Rectangle b = (Rectangle)Call(oldPanel, null, "AsistenteGuiaRect", size.Width, size.Height, target);
            Check(a == b, "guia misma esquina y rectangulo");
            Check(Call(panel, null, "AsistenteGuiaRectSigue", size.Width, size.Height, target, a).Equals(
                Call(oldPanel, null, "AsistenteGuiaRectSigue", size.Width, size.Height, target, b)), "guia conserva seguimiento");
        }
        Console.WriteLine("GUIA: 6000 destinos y su seguimiento identicos.");
    }
    static void Grid(object p, int count, int selected)
    {
        Set(p, "gridN", count); Array grid = (Array)Get(p, "gridBuf");
        for (int i = 0; i < grid.Length; i++) { object c = grid.GetValue(i); Set(c, "Sel", i == selected); grid.SetValue(c, i); }
    }
    static void Dynamics()
    {
        Random random = new Random(186300);
        using (Control p = (Control)Activator.CreateInstance(panel, true))
        using (Control old = (Control)Activator.CreateInstance(oldPanel, true))
        {
            for (int frame = 0; frame < 2000; frame++)
            {
                float dt = frame % 20 == 0 ? 0f : frame % 21 == 0 ? -1f : frame % 23 == 0 ? float.NaN : (float)random.NextDouble() * .1f;
                int count = frame % 3 == 0 ? 0 : frame % 3 == 1 ? 18 : 36;
                Grid(p, count, frame % 18); Grid(old, count, frame % 18);
                float[] initial = (float[])Get(p, "atenua");
                for (int i = 0; i < initial.Length; i++) initial[i] = (float)random.NextDouble();
                Array.Copy(initial, (Array)Get(old, "atenua"), initial.Length);
                Run(p, "AtenuaCeldas", frame % 2 == 0, dt); Run(old, "AtenuaCeldas", frame % 2 == 0, dt);
                Same((Array)Get(p, "atenua"), (Array)Get(old, "atenua"), "rejilla");
                foreach (string field in new string[] { "lunaBeat", "lunaMid", "lunaHigh" })
                { float level = (float)random.NextDouble(); Set(p, field, level); Set(old, field, level); }
                for (int i = 0; i < 4; i++) ((float[])Get(p, "eqAlto"))[i] = (float)random.NextDouble();
                Array.Copy((Array)Get(p, "eqAlto"), (Array)Get(old, "eqAlto"), 4);
                Run(p, "PasoEq", frame * 16.666, dt); Run(old, "PasoEq", frame * 16.666, dt);
                Same((Array)Get(p, "eqAlto"), (Array)Get(old, "eqAlto"), "ecualizador visual");
            }
            for (int frame = 0; frame < 1000; frame++)
            {
                Color[] colors = { Color.FromArgb(random.Next() | unchecked((int)0xFF000000)), Color.Black, Color.Coral };
                float weight = frame % 3 == 0 ? 0f : frame % 3 == 1 ? 1f : (float)random.NextDouble();
                foreach (object target in new object[] { p, old })
                {
                    Run(target, "SetCoverPalette", colors, weight); Set(target, "coverWeight", weight);
                    object wave = Activator.CreateInstance(target.GetType().Assembly.GetType("Rocola.GpuAurora"));
                    Set(wave, "Has", frame % 4 != 0); Set(wave, "RgbA", (uint)colors[0].ToArgb()); Set(wave, "Rgb2A", 0x003D237Bu);
                    Set(target, "auCuadro", wave);
                    Set(target, "ColoresFluidos", frame % 2 == 0); Set(target, "starDrive", .4f);
                    Set(target, "NebColor", (uint)colors[0].ToArgb()); Set(target, "NebColor2", 0xFF83ABF2u);
                    Run(target, "AdvanceCoverPalette", frame * 16.666);
                }
                foreach (string field in new string[] { "coverCurrent", "coverBands", "flowColors", "flowGenre" }) Same((Array)Get(p, field), (Array)Get(old, field), field);
                foreach (string field in new string[] { "coverWeight", "flowBlend", "flowPhase" }) Check(Get(p, field).Equals(Get(old, field)), field);
            }
        }
        Console.WriteLine("DINAMICA: 2000 rejillas, 2000 ecualizadores y 1000 pasos de paleta identicos, con fundidos y opciones alternadas.");
    }
    static void Contract()
    {
        Array a = Array.CreateInstance(vertex, 6);
        Call(panel, null, "SetTexQuadA", a, 10f, 20f, 70f, 90f, .5f, .75f, 0x89ABCDEFu);
        float[] x = {9.5f,69.5f,69.5f,9.5f,69.5f,9.5f}, y = {19.5f,19.5f,89.5f,19.5f,89.5f,89.5f};
        for (int i = 0; i < 6; i++) {
            object v = a.GetValue(i); Check((float)Get(v,"X") == x[i] && (float)Get(v,"Y") == y[i], "quad conserva ajuste medio pixel");
            Check((uint)Get(v,"Color") == 0x89ABCDEFu && (float)Get(v,"Rhw") == 1f && (float)Get(v,"Z") == 0f, "quad conserva color y profundidad");
        }
    }
    static void Timing(string label, Action<int> work)
    {
        double[] times = new double[31];
        for (int b = -3; b < times.Length; b++) {
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 1000; i++) work(i);
            double us = (Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency/1000;
            if (b >= 0) times[b] = us;
        }
        Array.Sort(times); Console.WriteLine(label + " mediana_us=" + times[15].ToString("F3",CultureInfo.InvariantCulture)
            + " p95_lote_us=" + times[29].ToString("F3",CultureInfo.InvariantCulture) + " max_lote_us=" + times[30].ToString("F3",CultureInfo.InvariantCulture));
    }
    static void GeometryTiming<T>()
    {
        T[] data = new T[120];
        Quad<T> quad = (Quad<T>)Delegate.CreateDelegate(typeof(Quad<T>), panel.GetMethod("SetTexQuadA", B));
        Halo<T> halo = (Halo<T>)Delegate.CreateDelegate(typeof(Halo<T>), HaloMethod(star));
        Disc<T> disc = (Disc<T>)Delegate.CreateDelegate(typeof(Disc<T>), panel.GetMethod("AsistenteDisco", B));
        Timing("quad", delegate(int i) { quad(data, i%17, 20f, 174f, 160f, .75f, .875f, 0xFFAABBCCu); });
        Timing("halo", delegate(int i) { halo(data, 0, i%17, 20f, 174f, 160f, 0xFFAABBCCu); });
        Timing("disco_asistente", delegate(int i) { int n=0; disc(data, ref n, i%17, 20f, 10f, 0xFFAABBCCu); checksum += n; });
    }
    static void Measure()
    {
        Console.WriteLine("Microbenchmark Linux/Mono; 31 lotes de 1000 llamadas por delegado, sin reflexion en el tramo medido. Promedios, no FPS.");
        typeof(GraficosGenerales).GetMethod("GeometryTiming", B).MakeGenericMethod(vertex).Invoke(null,null);
        Func<int,int,RectangleF,Rectangle> guide = (Func<int,int,RectangleF,Rectangle>)Delegate.CreateDelegate(typeof(Func<int,int,RectangleF,Rectangle>), panel.GetMethod("AsistenteGuiaRect",B));
        Timing("guia", delegate(int i) { checksum += guide(1024,768,new RectangleF(i%100,20,600,400)).X; });
        using(Control p=(Control)Activator.CreateInstance(panel,true)) {
            Grid(p,36,4);
            Action<bool,float> grid=(Action<bool,float>)Delegate.CreateDelegate(typeof(Action<bool,float>),p,panel.GetMethod("AtenuaCeldas",B));
            Timing("rejilla_36",delegate(int i){grid(i%2==0,1/60f);});
            Action<double,float> eq=(Action<double,float>)Delegate.CreateDelegate(typeof(Action<double,float>),p,panel.GetMethod("PasoEq",B));
            Timing("ecualizador",delegate(int i){eq(i*16.666,1/60f);});
            Action<double> flow=(Action<double>)Delegate.CreateDelegate(typeof(Action<double>),p,panel.GetMethod("AdvanceColorFlow",B));
            foreach(float w in new float[]{0f,.5f,1f}) {Set(p,"coverWeight",w);Timing("paleta_"+w.ToString(CultureInfo.InvariantCulture),delegate(int i){flow(1/60.0);});}
        }
        Console.WriteLine("checksum="+checksum);
    }
    [STAThread] static int Main(string[] args)
    {
        try {
            Assembly app=Assembly.LoadFrom(args[0]);panel=app.GetType("Rocola.GpuPanel");star=app.GetType("Rocola.GpuStarfield");vertex=app.GetType("Rocola.D3D9+Vtx");
            if(args.Length>1 && args[1]=="medir"){Measure();return 0;}
            Contract();
            if(args.Length>1){Assembly old=Assembly.LoadFile(System.IO.Path.GetFullPath(args[1]));Check(old!=app,"referencia independiente");
                oldPanel=old.GetType("Rocola.GpuPanel");oldStar=old.GetType("Rocola.GpuStarfield");oldVertex=old.GetType("Rocola.D3D9+Vtx");
                Geometry();Guides();Dynamics();}
            else Console.WriteLine("Comparacion de geometria y dinamica contra release anterior OMITIDA: falta segundo ejecutable.");
            Console.WriteLine("OK "+checks+" comprobaciones, "+bytes+" bytes comparados; sin GPU real ni audio.");return 0;
        }catch(Exception e){Console.WriteLine("FALLO "+e);return 1;}
    }
}

using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Controlador de performance.cs y su raster. Datos aislados, sin audio ni GPU real.
// Compilar con performance.cs, leercodigo.cs y /main:ResolucionLigera.
class ResolucionLigera
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type H = typeof(Performance), panel, form, vertex;
    static int checks, failures;
    static Bitmap idle;
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Run(object o, string n, params object[] a) { return Call(o.GetType(), o, n, a); }
    static object Help(string n, params object[] a) { return Call(H, null, n, a); }
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Prop(object o, string n) { return o.GetType().GetProperty(n, B).GetValue(o, null); }
    static void Check(bool c, string s) { checks++; if (!c) failures++; Console.WriteLine((c ? "OK " : "FALLO ") + s); }
    static byte[] Bytes(Array a, int count)
    {
        byte[] b = new byte[count * Marshal.SizeOf(vertex)]; GCHandle pin = GCHandle.Alloc(a, GCHandleType.Pinned);
        try { Marshal.Copy(pin.AddrOfPinnedObject(), b, 0, b.Length); } finally { pin.Free(); } return b;
    }
    static bool Equal(byte[] a, byte[] b)
    { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    static Bitmap Idle() { return idle; }
    static readonly float[] bands = { .7f, .6f, .5f, .4f, .6f, .3f, .2f, .1f };
    static T Wave<T>()
    {
        object a = Activator.CreateInstance(typeof(T)); Set(a, "Has", true); Set(a, "B", bands);
        Set(a, "Alto", 140f); Set(a, "BaseY", 650f); Set(a, "Dim", .7f); Set(a, "DimFilo", 1f); Set(a, "Calma", .3f); return (T)a;
    }
    static IList Captura() { return (IList)Help("Captura"); }
    static int Uses(IList sends, IntPtr tex)
    {
        int n = 0;
        foreach (object e in sends) foreach (object stage in (Array)Get(Get(e, "S"), "E"))
            if ((IntPtr)Get(stage, "Tex") == tex && (uint)Get(stage, "Op") != 1) n++;
        return n;
    }
    static void GroundPixels()
    {
        int cols = (int)panel.GetField("AurCols", B).GetRawConstantValue(), rows = 6, ground = 4, points = cols + 1;
        Array full = Array.CreateInstance(vertex, (rows + ground) * cols * 6), cut = Array.CreateInstance(vertex, full.Length);
        float[] heights = new float[points], alpha = new float[points], ray = new float[points];
        uint[] curtain = new uint[(rows + 1) * points], floor = new uint[(ground + 1) * points];
        for (int i = 0; i < points; i++) { heights[i] = 30 + (float)Math.Sin(i * .05) * 10; alpha[i] = .7f; ray[i] = 1; }
        for (int i = 0; i < curtain.Length; i++) curtain[i] = 0x00AABBDD;
        for (int i = 0; i < floor.Length; i++) floor[i] = 0x00225577;
        foreach (int step in new int[] { 1, 2 })
        {
            int nf = (int)Call(panel, null, "AurMallaLigera", full, heights, curtain, floor, alpha, ray, 320f, 182f, 240f, step);
            int nc = (int)Call(panel, null, "AurMallaVisible", cut, heights, curtain, floor, alpha, ray, 320f, 182f, 240f, step, false);
            Array body = Array.CreateInstance(vertex, nc); Array.Copy(full, nf - nc, body, 0, nc);
            Check(nc * 10 == nf * 6 && Equal(Bytes(body, nc), Bytes(cut, nc)), "sin suelo conserva todos los vertices de la cortina, paso " + step);
            object dev = Help("Tarjeta", 320, 240, false, true, true);
            try
            {
                using (Bitmap bmp = new Bitmap(320, 60, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Color.FromArgb(255, 12, 19, 42));
                    object[] args = { bmp, 0f, 0f }; IntPtr tex = (IntPtr)Run(dev, "CreateTexturePow2", args);
                    Array quad = Array.CreateInstance(vertex, 6);
                    Call(panel, null, "SetTexQuadA", quad, 0f, 180f, 320f, 240f, (float)args[1], (float)args[2], 0xFFFFFFFFu);
                    Captura(); Run(dev, "BeginFrame"); Run(dev, "DrawMesh", full, nf / 3); Run(dev, "DrawTexQuad", tex, quad); Run(dev, "EndFrame"); IList before = Captura();
                    Run(dev, "BeginFrame"); Run(dev, "DrawMesh", cut, nc / 3); Run(dev, "DrawOpaqueQuad", tex, quad); Run(dev, "EndFrame"); IList after = Captura();
                    int[] a = (int[])Help("Fondo", 320, 240, 55), b = (int[])a.Clone();
                    Help("Pinta", before, a, 320, 240); Help("Pinta", after, b, 320, 240);
                    bool same = true; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) same = false;
                    Check(same, "raster identico: suelo tapado omitido y panel sin mezcla, paso " + step);
                    object blend = Get(after[after.Count - 1], "S");
                    Check(!(bool)Get(blend, "Mezcla"), "el panel opaco llega al controlador sin mezcla");
                    // El siguiente dibujo translucido debe restablecer su mezcla normal.
                    Run(dev, "BeginFrame"); Run(dev, "DrawTexQuad", tex, quad); Run(dev, "EndFrame");
                    IList normal = Captura(); Check((bool)Get(Get(normal[0], "S"), "Mezcla"), "se restaura la mezcla para la interfaz siguiente");
                    form.Assembly.GetType("Rocola.D3D9").GetMethod("Release", B).Invoke(null, new object[] { tex });
                }
            }
            finally { Run(dev, "Dispose"); }
        }
    }
    static void Resolutions(int w, int h)
    {
        object dev = Help("Tarjeta", w, h, false, true, true), sky = Help("Cielo", dev, w, h);
        using (Control p = (Control)Help("Panel", w, h, sky))
        {
            Set(p, "ConLuna", true); Set(p, "Estrella3D", true); Set(p, "Dim", 1f);
            Run(p, "EnsureLuna"); Check((IntPtr)Get(p, "lunaTex") == IntPtr.Zero, "3D no hornea ni sube la superficie 2D " + w);
            Captura(); Run(p, "DibujaLuna"); Check(Captura().Count > 0, "estrella 3D se dibuja sin textura 2D " + w);
            Set(p, "Estrella3D", false); Run(p, "EnsureLuna"); IntPtr star = (IntPtr)Get(p, "lunaTex");
            Check(star != IntPtr.Zero, "cambiar a 2D prepara su textura antes de dibujar " + w);
            Set(p, "Estrella3D", true); Run(p, "EnsureLuna"); Set(p, "Estrella3D", false); Run(p, "EnsureLuna");
            Check((IntPtr)Get(p, "lunaTex") == star, "volver a 2D reutiliza la textura preparada " + w);
            Set(p, "SinGrano", true); Run(p, "EnsureNieblas");
            Check((IntPtr)Get(p, "ruidoTex") == IntPtr.Zero, "sin grano no crea su textura " + w);
            Set(p, "SinGrano", false); Run(p, "EnsureNieblas"); IntPtr noise = (IntPtr)Get(p, "ruidoTex");
            Check(noise != IntPtr.Zero, "al restaurar grano se prepara al momento " + w);
            Set(p, "ConLuna", false); Set(p, "GranoVineta", true); Set(p, "VinetaSuave", true);
            foreach (bool cache in new bool[] { false, true })
            {
                Set(p, "CieloMedia", cache); Set(p, "FondoUnaCapa", cache); Set(p, "CieloCuarto", cache);
                Set(p, "SinGrano", false); Captura(); Check((bool)Run(p, "RenderFrame"), "cuadro con grano presentado " + w + " cache=" + cache);
                IList withNoise = Captura();
                Set(p, "SinGrano", true); Check((bool)Run(p, "RenderFrame"), "cuadro sin grano presentado " + w + " cache=" + cache);
                IList withoutNoise = Captura();
                Check(Uses(withNoise, noise) > 0 && Uses(withoutNoise, noise) == 0, "no se envia grano ni en pasada doble ni dentro de la cache " + w + " cache=" + cache);
                IntPtr vignette = (IntPtr)Get(p, "vinetaTex");
                Check(vignette != IntPtr.Zero && Uses(withoutNoise, vignette) > 0, "la vineta se conserva " + w + " cache=" + cache);
                Set(p, "SinGrano", false); Check((bool)Run(p, "RenderFrame"), "volver al grano presenta " + w);
                Check(Uses(Captura(), noise) > 0, "la cache se invalida al restaurar el grano " + w + " cache=" + cache);
            }
            FieldInfo wave = panel.GetField("GetAurora", B);
            wave.SetValue(p, Delegate.CreateDelegate(wave.FieldType, typeof(ResolucionLigera).GetMethod("Wave", B).MakeGenericMethod(wave.FieldType.GetMethod("Invoke").ReturnType)));
            int ih = (int)Call(panel, null, "IdlePanelHeight", h);
            idle = new Bitmap(w, ih, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics g = Graphics.FromImage(idle)) g.Clear(Color.FromArgb(255, 12, 19, 42));
                Set(p, "GetIdlePanel", new Func<Bitmap>(Idle)); Set(p, "uiBotH", 96); Set(p, "IdlePanelVersion", 1);
                Set(p, "IdleExpandTarget", true); Set(p, "idleBlend", 1f); Set(p, "EstadoOpaco", true);
                Captura(); Check((bool)Run(p, "RenderFrame"), "reposo opaco presentado " + w);
                IList rendered = Captura(); IntPtr state = (IntPtr)Get(p, "idleTex"); bool unblended = false;
                foreach (object e in rendered) if ((IntPtr)Get(((Array)Get(Get(e, "S"), "E")).GetValue(0), "Tex") == state && !(bool)Get(Get(e, "S"), "Mezcla")) unblended = true;
                Check(unblended && !(bool)Get(p, "aurMallaSuelo"), "solo el estado opaco actualizado omite suelo y mezcla " + w);
                int vertices = (int)Get(p, "aurMallaN");
                Set(p, "EstadoOpaco", false); Check((bool)Run(p, "RenderFrame"), "volver a transparente presenta " + w); Captura();
                Check((bool)Get(p, "aurMallaSuelo") && (int)Get(p, "aurMallaN") * 6 == vertices * 10, "restaurar transparencia devuelve suelo y geometria " + w);
                // Mantener la muestra: al reactivar el suelo tambien deben reconstruirse sus colores.
                Set(p, "AuroraSinSuelo", true); Run(p, "RenderFrame"); Captura();
                Check(!(bool)Get(p, "aurMallaSuelo"), "el interruptor permite omitir suelo tambien sin estado opaco " + w);
                Set(p, "AuroraSinSuelo", false); Run(p, "RenderFrame"); Captura();
                Check((bool)Get(p, "aurMallaSuelo"), "el suelo vuelve al apagar su ahorro " + w);
                Set(p, "EstadoOpaco", true); Set(p, "idleBlend", .9f); Set(p, "IdleExpandTarget", false); Run(p, "RenderFrame"); Captura();
                Check((bool)Get(p, "aurMallaSuelo") && !(bool)Prop(p, "EstadoOpacoListo"), "transicion conserva suelo y mezcla " + w);
                Console.WriteLine("GEOMETRIA " + w + "x" + h + ": suelo oculto " + vertices + " / " + (vertices * 10 / 6) + " vertices; panel=" + (w * ih) + " pixeles");
            }
            finally { idle.Dispose(); idle = null; }
        }
    }
    [STAThread] static int Main(string[] args)
    {
        Assembly app = Assembly.LoadFrom(args[0]); panel = app.GetType("Rocola.GpuPanel"); form = app.GetType("Rocola.Form1");
        if (panel == null) { Console.WriteLine("CPU: no se compilan los cambios de renderizado"); return 0; }
        vertex = app.GetType("Rocola.D3D9").GetNestedType("Vtx", B);
        foreach (string n in new string[] { "TPanel", "TStar", "TDev", "TD3D", "TVtx", "TVtx2", "TForm" })
        {
            string type = n == "TPanel" ? "GpuPanel" : n == "TStar" ? "GpuStarfield" : n == "TDev" ? "D3D9Device" : n == "TForm" ? "Form1" : "D3D9";
            Type t = app.GetType("Rocola." + type); if (n == "TVtx" || n == "TVtx2") t = t.GetNestedType(n == "TVtx" ? "Vtx" : "Vtx2", B);
            H.GetField(n, B).SetValue(null, t);
        }
        try { GroundPixels(); Resolutions(1024, 768); Resolutions(1280, 1024); Resolutions(1920, 1080); }
        catch (Exception e) { failures++; Console.WriteLine("FALLO " + e); }
        Console.WriteLine(checks + " comprobaciones, " + failures + " fallos. Sin GPU real ni audio."); return failures;
    }
}

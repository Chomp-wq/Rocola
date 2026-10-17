using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Controlador artificial de performance.cs. Compilar con /main:AuroraEficiente y leercodigo.cs.
// No hay GPU, audio ni reloj de Windows XP. Comprueba bytes enviados e invalidaciones.
class AuroraEficiente
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type H = typeof(Performance), panel, vertex, tiles;
    static int checks, failures;
    delegate float Onda(float x, float t);
    delegate void Ondas(float x, float t, out float r, out float h);
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Run(object o, string n, params object[] a) { return Call(o.GetType(), o, n, a); }
    static object Help(string n, params object[] a) { return Call(H, null, n, a); }
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static void Check(bool c, string s) { checks++; if (!c) failures++; Console.WriteLine((c ? "OK " : "FALLO ") + s); }
    static byte[] Bytes(Array a, int count)
    {
        byte[] b = new byte[count * Marshal.SizeOf(vertex)]; GCHandle pin = GCHandle.Alloc(a, GCHandleType.Pinned);
        try { Marshal.Copy(pin.AddrOfPinnedObject(), b, 0, b.Length); } finally { pin.Free(); } return b;
    }
    static bool Equal(byte[] a, byte[] b)
    { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    static IList Capture() { return (IList)Help("Captura"); }
    static bool SameDraws(IList a, IList b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if ((int)Get(a[i], "Tris") != (int)Get(b[i], "Tris") || !Equal((byte[])Get(a[i], "Datos"), (byte[])Get(b[i], "Datos"))) return false;
            object sa = Get(a[i], "S"), sb = Get(b[i], "S");
            foreach (string n in new string[] { "Mezcla", "Origen", "Destino", "Fvf", "Blanco" }) if (!Get(sa, n).Equals(Get(sb, n))) return false;
            object ea = ((Array)Get(sa, "E")).GetValue(0), eb = ((Array)Get(sb, "E")).GetValue(0);
            foreach (string n in new string[] { "Tex", "Op", "Dir" }) if (!Get(ea, n).Equals(Get(eb, n))) return false;
        }
        return true;
    }
    static void Waves()
    {
        Onda ray = (Onda)Delegate.CreateDelegate(typeof(Onda), panel.GetMethod("AurRayo", B));
        Onda height = (Onda)Delegate.CreateDelegate(typeof(Onda), panel.GetMethod("AurOndaAlto", B));
        Ondas both = (Ondas)Delegate.CreateDelegate(typeof(Ondas), panel.GetMethod("AurOndas", B));
        Random random = new Random(1046300);
        foreach (float special in new float[] { 0f, 628.3f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue })
        {
            bool exact = true;
            for (int i = 0; i < 2000; i++)
            {
                float x = (float)random.NextDouble(), t = i < 1000 ? special : (float)(random.NextDouble() * 628.3);
                if (i == 0) x = special;
                float r, h; both(x, t, out r, out h);
                if (!Equal(BitConverter.GetBytes(r), BitConverter.GetBytes(ray(x, t))) ||
                    !Equal(BitConverter.GetBytes(h), BitConverter.GetBytes(height(x, t)))) exact = false;
            }
            Check(exact, "altura y brillo identicos bit a bit: tiempo=" + special + " (2000 muestras)");
        }
    }
    static byte[] Slice(Array v, int at, int n)
    { Array a = Array.CreateInstance(vertex, n); Array.Copy(v, at, a, 0, n); return Bytes(a, n); }
    static void Mesh(int w, int h)
    {
        int cols = (int)panel.GetField("AurCols", B).GetRawConstantValue();
        int contour = (int)panel.GetField("AurContCols", B).GetRawConstantValue();
        int line = (int)panel.GetField("AurLineaCols", B).GetRawConstantValue();
        int capacity = (int)panel.GetField("AurBrilloVtx", B).GetRawConstantValue();
        float[] heights = new float[cols + 1], alpha = new float[cols + 1]; uint[] colors = new uint[cols + 1];
        for (int i = 0; i <= cols; i++) { heights[i] = 70 + 40 * (float)Math.Sin(i * .1); alpha[i] = .2f + .7f * i / cols; colors[i] = 0x003366CCu + (uint)(i * 256); }
        foreach (int step in new int[] { 1, 2 })
        {
            Array normal = Array.CreateInstance(vertex, capacity), compact = Array.CreateInstance(vertex, capacity), restored = Array.CreateInstance(vertex, capacity);
            int n = (int)Call(panel, null, "AurBrilloLigero", normal, heights, colors, alpha, colors, alpha, (float)w, h * .75f, step);
            int c = (int)Call(panel, null, "AurBrilloConHalo", compact, heights, colors, alpha, colors, alpha, (float)w, h * .75f, step, true);
            int back = (int)Call(panel, null, "AurBrilloConHalo", restored, heights, colors, alpha, colors, alpha, (float)w, h * .75f, step, false);
            int horizon = line / step * 5 * 6;
            Check(n == 7992 / step && c == 6264 / step, "menos vertices de brillo " + w + " salto=" + step + ": " + n + " -> " + c);
            Check(Equal(Slice(normal, 0, horizon), Slice(compact, 0, horizon)), "horizonte exacto " + w + " salto=" + step);
            bool core = true, compactWidth = true;
            for (int i = 0; i < contour / step; i++)
            {
                int a = horizon + i * 24, b = horizon + i * 18;
                if (!Equal(Slice(normal, a + 12, 6), Slice(compact, b + 6, 6))) core = false;
                float top = (float)Get(compact.GetValue(b), "Y"), bottom = (float)Get(compact.GetValue(b + 17), "Y");
                if (Math.Abs(bottom - top - 8f) > .0002f) compactWidth = false;
            }
            Check(core, "nucleo luminoso exacto en cada segmento " + w + " salto=" + step);
            Check(compactWidth, "halo acotado a ocho pixeles " + w + " salto=" + step);
            Check(back == n && Equal(Bytes(normal, n), Bytes(restored, back)), "desactivar devuelve la malla normal exacta " + w);
            Check((int)Call(panel, null, "AurBrilloConHalo", compact, null, colors, alpha, colors, alpha, (float)w, h * .75f, step, true) == 0,
                "datos ausentes no producen dibujo " + w);
        }
    }
    static IList Layer(object p, object dev, object[] args)
    { Capture(); Run(dev, "BeginFrame"); Run(p, "DibujaTeselas", args); Run(dev, "EndFrame"); return Capture(); }
    static void Cache(int w, int h)
    {
        object dev = Help("Tarjeta", w, h, false, true, true), sf = Help("Cielo", dev, w, h);
        using (Control p = (Control)Help("Panel", w, h, sf))
        using (Bitmap bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); g.FillEllipse(Brushes.CornflowerBlue, 5, 3, 54, 59); }
            object[] up = { bmp, 0f, 0f }; IntPtr tex = (IntPtr)Run(dev, "CreateTexturePow2", up);
            object t = Call(tiles, null, "De", bmp, 2, true);
            object[] args = { tex, t, -9f, -4f, (float)w + 12, (float)h + 8, (float)up[1], (float)up[2], 0xDDAABBCCu, Array.CreateInstance(vertex, 6) };
            Array vertices = (Array)Get(t, "Vtx");
            IList original = Layer(p, dev, args); int made = (int)Get(t, "GeometriasCalculadas");
            for (int i = 0; i < 5; i++)
                Check(SameDraws(original, Layer(p, dev, args)) && (int)Get(t, "GeometriasCalculadas") == made
                    && ReferenceEquals(vertices, Get(t, "Vtx")), "geometria quieta exacta sin otra reserva " + w + " cuadro=" + i);
            Check((int)Get(t, "GeometriasReusadas") == 5, "cinco aciertos sin reconstruir " + w);
            for (int change = 0; change < 8; change++)
            {
                switch (change)
                {
                    case 0: args[2] = -11.25f; break;
                    case 1: args[3] = 2.125f; break;
                    case 2: args[4] = (float)w - 6f; break;
                    case 3: args[5] = (float)h - 4f; break;
                    case 4: args[6] = .875f; break;
                    case 5: args[7] = .9375f; break;
                    case 6: args[8] = 0x89FFEEDDu; break;
                    case 7: args[1] = t = Call(tiles, null, "De", bmp, 4, true); break;
                }
                made = (int)Get(t, "GeometriasCalculadas"); IList changed = Layer(p, dev, args);
                Check((int)Get(t, "GeometriasCalculadas") == made + 1, "invalida transformacion/color/analisis " + change + " a " + w);
                Set(t, "geometriaLista", false); IList fresh = Layer(p, dev, args);
                Check(SameDraws(changed, fresh), "malla actualizada igual al recalculo completo " + change + " a " + w);
            }
            IList clear = Layer(p, dev, args);
            Set(p, "nHuecos", 1); Array.Copy(new int[] { 20, 30, w / 2, h / 2 }, (Array)Get(p, "huecos"), 4);
            Capture(); Run(dev, "BeginFrame"); Run(p, "DibujaTeselasFondo", args); Run(dev, "EndFrame"); Capture();
            Set(p, "nHuecos", 0);
            Check(SameDraws(clear, Layer(p, dev, args)), "recortes de fondo no pisan malla sin recortar " + w);
            foreach (bool intermediate in new bool[] { false, true })
            {
                Set(dev, "VinetaPuntos", !intermediate); Set(dev, "enMedia", intermediate); Set(dev, "mediaDiv", 4);
                IList transformed = Layer(p, dev, args);
                Set(t, "geometriaLista", false);
                Check(SameDraws(transformed, Layer(p, dev, args)), "vineta/cielo intermedio conservan la cache fuente " + w + " media=" + intermediate);
                Set(dev, "VinetaPuntos", false); Set(dev, "enMedia", false);
                Check(SameDraws(clear, Layer(p, dev, args)), "volver a pantalla conserva posiciones y colores " + w);
            }
            Call(panel.Assembly.GetType("Rocola.D3D9"), null, "Release", tex);
        }
    }
    static void Freeze(object p)
    {
        object cadence = Get(p, "waveCadence"); double now = (double)Call(panel, null, "AhoraMs");
        Set(cadence, "origin", now); Set(cadence, "SampleMs", now); Set(cadence, "tick", 1e9);
        Set(p, "aurFinoAnt", now);
    }
    static void Live(int w, int h)
    {
        object dev = Help("Tarjeta", w, h, false, true, true), sf = Help("Cielo", dev, w, h);
        using (Control p = (Control)Help("Panel", w, h, sf))
        {
            object wave = Activator.CreateInstance(panel.Assembly.GetType("Rocola.GpuAurora"));
            Set(wave, "Has", true); Set(wave, "B", new float[] { .7f, .65f, .5f, .4f, .5f, .3f, .2f, .1f });
            Set(wave, "Alto", 140f); Set(wave, "BaseY", h * .75f); Set(wave, "Dim", .6f); Set(wave, "DimFilo", .8f);
            Set(wave, "Rgb", 0x00336688u); Set(wave, "Rgb2", 0x00663399u); Set(wave, "RgbA", 0x0088CCFFu); Set(wave, "Rgb2A", 0x00CC88FFu);
            foreach (bool light in new bool[] { false, true }) foreach (bool noFloor in new bool[] { false, true })
            {
                Set(p, "AuroraHaloCompacto", false); Set(p, "AuroraLigera", light); Set(p, "AuroraSinSuelo", noFloor);
                Set(Get(p, "waveCadence"), "rate", 0);
                Capture(); Run(dev, "BeginFrame"); Run(p, "DrawAurora", wave); Run(dev, "EndFrame"); Capture();
                int n = (int)Get(p, "aurMallaN"), nb = (int)Get(p, "aurBrilloN");
                Array fill = (Array)Get(p, "aurV"), glow = (Array)Get(p, "aurVb");
                byte[] a = Bytes(fill, n), b = Bytes(glow, nb);
                foreach (bool on in new bool[] { true, false, true, false })
                {
                    Freeze(p); double time = (double)Get(Get(p, "waveCadence"), "SampleMs");
                    Set(p, "AuroraHaloCompacto", on);
                    Run(dev, "BeginFrame"); Run(p, "DrawAurora", wave); Run(dev, "EndFrame"); Capture();
                    Check((double)Get(Get(p, "waveCadence"), "SampleMs") == time && (int)Get(p, "aurBrilloN") == (on ? 6264 : 7992) / (light ? 2 : 1),
                        "interruptor inmediato entre muestras " + w + " ligera=" + light + " sinSuelo=" + noFloor + " compacto=" + on);
                    Check(ReferenceEquals(fill, Get(p, "aurV")) && ReferenceEquals(glow, Get(p, "aurVb")) && Equal(a, Bytes(fill, n)),
                        "halo no recalcula relleno ni reserva nuevos buffers");
                    if (!on) Check(Equal(b, Bytes(glow, nb)), "apagar restaura exactamente el brillo anterior");
                }
            }
        }
    }
    [STAThread] static int Main(string[] args)
    {
        Assembly app = Assembly.LoadFrom(args[0]); panel = app.GetType("Rocola.GpuPanel");
        if (panel == null) { Console.WriteLine("CPU: sin cambios de renderizado"); return 0; }
        vertex = app.GetType("Rocola.D3D9").GetNestedType("Vtx", B); tiles = app.GetType("Rocola.TeselasVisibles");
        foreach (string n in new string[] { "TPanel", "TStar", "TDev", "TD3D", "TVtx", "TVtx2", "TForm" })
        {
            string type = n == "TPanel" ? "GpuPanel" : n == "TStar" ? "GpuStarfield" : n == "TDev" ? "D3D9Device" : n == "TForm" ? "Form1" : "D3D9";
            Type t = app.GetType("Rocola." + type); if (n == "TVtx" || n == "TVtx2") t = t.GetNestedType(n == "TVtx" ? "Vtx" : "Vtx2", B);
            H.GetField(n, B).SetValue(null, t);
        }
        try { Waves(); foreach (int[] wh in new int[][] { new int[] {1024,768}, new int[] {1280,1024}, new int[] {1920,1080} }) { Mesh(wh[0], wh[1]); Cache(wh[0], wh[1]); Live(wh[0], wh[1]); } }
        catch (Exception e) { failures++; Console.WriteLine("FALLO " + e); }
        Console.WriteLine(checks + " comprobaciones, " + failures + " fallos. Sin GPU real ni audio."); return failures;
    }
}

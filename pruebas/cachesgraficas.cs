using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Controlador artificial de performance.cs. Compilar con /main:CachesGraficas y leercodigo.cs.
// No hay GPU, audio ni reloj de Windows XP. Comprueba bytes enviados e invalidaciones.
class CachesGraficas
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type H = typeof(Performance), panel, vertex, form, referenceForm;
    static int checks, failures;
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
    static int Count(string name) { return (int)form.GetField(name, B).GetValue(null); }
    static Brush Gradient(Rectangle r, Color a, Color b, bool cached)
    { return (Brush)Call(!cached && referenceForm != null ? referenceForm : form, null, cached || referenceForm != null ? "DegradadoV" : "HorneaDegradadoV", r, a, b); }
    static byte[] Paint(Brush brush, Rectangle r)
    {
        using (Bitmap bmp = new Bitmap(r.Width + 24, r.Height + 24, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(117, 31, 47, 89)); g.FillRectangle(brush, r);
                // Tambien pintar fuera de la primera repeticion vertical del mosaico.
                g.FillRectangle(brush, new Rectangle(3, r.Height + 5, r.Width, 12));
            }
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] result = new byte[bmp.Width * bmp.Height * 4];
                for (int y = 0; y < bmp.Height; y++) Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), result, y * bmp.Width * 4, bmp.Width * 4);
                return result;
            }
            finally { bmp.UnlockBits(d); }
        }
    }
    static void Gradients()
    {
        form.GetField("InterfazTramada", B).SetValue(null, true);
        foreach (int h in new int[] { 245, 326, 340, 2048 }) foreach (int alpha in new int[] { 0, 232, 255 })
        {
            Color a = Color.FromArgb(alpha, 12, 19, 42), b = Color.FromArgb(alpha == 232 ? 248 : alpha, 7, 12, 29);
            Rectangle r = new Rectangle(7, -3, 120, h); byte[] expected;
            using (Brush reference = Gradient(r, a, b, false)) expected = Paint(reference, r);
            using (Brush first = Gradient(r, a, b, true)) Check(Equal(expected, Paint(first, r)), "primer pincel exacto h=" + h + " alfa=" + alpha);
            int baked = Count("DegradadosGuardados"), reused = Count("DegradadosReusados");
            using (Brush hot = Gradient(r, a, b, true)) Check(Equal(expected, Paint(hot, r)), "copia reusada pixel a pixel h=" + h + " alfa=" + alpha);
            Check(Count("DegradadosGuardados") == baked && Count("DegradadosReusados") == reused + 1, "no vuelve a hornear el degradado");
            // El ancho/origen pertenecen al pincel devuelto; no son parte del mosaico de cuatro pixeles.
            Rectangle moved = new Rectangle(-2, 9, 95, h);
            using (Brush reference = Gradient(moved, a, b, false)) using (Brush hot = Gradient(moved, a, b, true))
                Check(Equal(Paint(reference, moved), Paint(hot, moved)) && Count("DegradadosGuardados") == baked, "origen y ancho distintos no contaminan la fuente");
            using (TextureBrush mutable = (TextureBrush)Gradient(r, a, b, true))
            { mutable.ScaleTransform(2f, 3f); mutable.TranslateTransform(50, 70); mutable.WrapMode = WrapMode.Clamp; }
            using (Brush hot = Gradient(r, a, b, true)) Check(Equal(expected, Paint(hot, r)), "transformar/disponer la copia no afecta a otro llamador");
        }
        Rectangle area = new Rectangle(0, 0, 90, 280);
        using (Brush retained = Gradient(area, Color.Red, Color.Blue, true))
        {
            byte[] before = Paint(retained, area);
            for (int i = 0; i < 12; i++) using (Brush b = Gradient(area, Color.FromArgb(120 + i, 20, 30), Color.Green, true)) { }
            Check(Equal(before, Paint(retained, area)), "eviccion de la fuente no invalida pinceles entregados");
        }
        int start = Count("DegradadosGuardados"); area.Height = 2049;
        using (Brush reference = Gradient(area, Color.Red, Color.Blue, false)) using (Brush b = Gradient(area, Color.Red, Color.Blue, true))
            Check(Equal(Paint(reference, area), Paint(b, area)) && Count("DegradadosGuardados") == start, "tamano extraordinario conserva aspecto y no entra en cache");
        Array entries = (Array)form.GetField("degradados", B).GetValue(null); bool bounded = entries.Length == 4;
        foreach (object entry in entries) if (entry != null && (int)Get(entry, "Alto") > 2048) bounded = false;
        Check(bounded, "solo cuatro fuentes de altura acotada");
        form.GetField("InterfazTramada", B).SetValue(null, false); area.Height = 241;
        using (Brush b = Gradient(area, Color.Red, Color.Blue, true)) using (Brush reference = new LinearGradientBrush(area, Color.Red, Color.Blue, 90f))
            Check(Equal(Paint(reference, area), Paint(b, area)), "apagar Interfaz sin escalones conserva su camino original");
        form.GetField("InterfazTramada", B).SetValue(null, true);
        int errors = 0; Thread[] workers = new Thread[4];
        for (int i = 0; i < workers.Length; i++)
        {
            int id = i;
            workers[i] = new Thread(delegate()
            {
                try
                {
                    for (int j = 0; j < 24; j++)
                    {
                        Rectangle r = new Rectangle(id, j % 3, 96, 140 + j % 7);
                        Color a = Color.FromArgb(230, 12 + id, 19, 42), b = Color.FromArgb(248, 7, 12, 29);
                        using (Brush reference = Gradient(r, a, b, false)) using (Brush hot = Gradient(r, a, b, true))
                            if (!Equal(Paint(reference, r), Paint(hot, r))) Interlocked.Increment(ref errors);
                    }
                }
                catch (Exception e) { Console.WriteLine(e); Interlocked.Increment(ref errors); }
            }); workers[i].Start();
        }
        foreach (Thread t in workers) t.Join();
        Check(errors == 0, "cuatro horneadores con evicciones concurrentes mantienen todos los pixeles");
    }
    static void Sky(int w, int h)
    {
        object dev = Help("Tarjeta", w, h, false, true, true), sf = Help("Cielo", dev, w, h);
        using (Control p = (Control)Help("Panel", w, h, sf))
        {
            object[] args = { (float)w, (float)h, h * .42f, 0xFF03050Au, 0xFF102040u, 0xFF392040u, 0f };
            object referenceSky = referenceForm == null ? null : Activator.CreateInstance(referenceForm.Assembly.GetType("Rocola.GpuStarfield"), B, null, new object[] { IntPtr.Zero, w, h, 4321 }, null);
            int n = (int)Run(sf, "ArmaCieloSuave", args); Array vertices = (Array)Get(sf, "skySuave"); byte[] original = Bytes(vertices, n * 3);
            Check(n == 24, "cielo suave conserva sus doce franjas " + w);
            int made = (int)Get(sf, "CielosSuavesCalculados");
            for (int i = 0; i < 6; i++)
            {
                int reused = (int)Run(sf, "ArmaCieloSuave", args);
                Check(reused == n && ReferenceEquals(vertices, Get(sf, "skySuave")) && Equal(original, Bytes(vertices, n * 3))
                    && (int)Get(sf, "CielosSuavesCalculados") == made, "cielo reusado exacto " + w + " cuadro=" + i);
            }
            Check((int)Get(sf, "CielosSuavesReusados") == 6, "evita seis reconstrucciones de cielo " + w);
            for (int change = 0; change < 8; change++)
            {
                if (change < 3) args[change] = (float)args[change] + 17.25f;
                else if (change < 6) args[change] = (uint)args[change] ^ 0x00123456u;
                else args[6] = change == 6 ? 8f : 0f;
                made = (int)Get(sf, "CielosSuavesCalculados"); n = (int)Run(sf, "ArmaCieloSuave", args);
                Check((int)Get(sf, "CielosSuavesCalculados") == made + 1, "invalida parametro " + change + " del cielo " + w);
                byte[] changed = Bytes((Array)Get(sf, "skySuave"), n * 3);
                if (referenceSky != null)
                {
                    int before = (int)Run(referenceSky, "ArmaCieloSuave", args);
                    Check(before == n && Equal(changed, Bytes((Array)Get(referenceSky, "skySuave"), before * 3)), "cielo identico al ejecutable anterior " + w);
                }
                Set(sf, "cieloSuaveListo", false);
                int fresh = (int)Run(sf, "ArmaCieloSuave", args);
                Check(fresh == n && Equal(changed, Bytes((Array)Get(sf, "skySuave"), fresh * 3)), "malla actualizada coincide con reconstruccion completa " + w);
            }
            if (referenceSky != null) Run(referenceSky, "Dispose");
        }
    }
    static void Freeze(object p)
    {
        object cadence = Get(p, "waveCadence"); double now = (double)Call(panel, null, "AhoraMs");
        Set(cadence, "origin", now); Set(cadence, "SampleMs", now); Set(cadence, "tick", 1e9); Set(p, "aurFinoAnt", now);
    }
    static IList Reflect(object p, object dev)
    { Capture(); Run(dev, "BeginFrame"); Run(p, "DibujaReflejoAurora"); Run(dev, "EndFrame"); return Capture(); }
    static void Wave(object p, object dev, object wave, bool fresh)
    {
        if (fresh) Set(Get(p, "waveCadence"), "rate", 0); else Freeze(p);
        Capture(); Run(dev, "BeginFrame"); Run(p, "DrawAurora", wave); Run(dev, "EndFrame"); Capture();
    }
    static void CheckReflection(object p, int w)
    {
        Array actual = (Array)Get(p, "reflejoV"), expected = Array.CreateInstance(vertex, actual.Length);
        int n = (int)Call(form, null, "AurReflejo", expected, Get(p, "aurHs"), Get(p, "aurNc"), Get(p, "aurNi"),
            (int)panel.GetField("AurCols", B).GetRawConstantValue(), (float)((Control)p).Width,
            (float)Get(p, "aurDibujadaBase") - (float)panel.GetField("AurLineaAlza", B).GetRawConstantValue(),
            Call(form, null, "ReflejoHueco", (float)((Control)p).Height));
        Check(n == (int)Get(p, "reflejoN") && Equal(Bytes(actual, n), Bytes(expected, n)), "reflejo coincide con formula completa " + w);
    }
    static void Reflection(int w, int h)
    {
        object dev = Help("Tarjeta", w, h, false, true, true), sf = Help("Cielo", dev, w, h);
        using (Control p = (Control)Help("Panel", w, h, sf))
        {
            object wave = Activator.CreateInstance(panel.Assembly.GetType("Rocola.GpuAurora"));
            Set(wave, "Has", true); Set(wave, "B", new float[] { .7f, .65f, .5f, .4f, .5f, .3f, .2f, .1f });
            Set(wave, "Alto", 140f); Set(wave, "BaseY", h * .75f); Set(wave, "Dim", .6f); Set(wave, "DimFilo", .8f);
            Set(wave, "Rgb", 0x00336688u); Set(wave, "Rgb2", 0x00663399u); Set(wave, "RgbA", 0x0088CCFFu); Set(wave, "Rgb2A", 0x00CC88FFu);
            Wave(p, dev, wave, true); IList first = Reflect(p, dev); CheckReflection(p, w);
            int made = (int)Get(p, "ReflejosCalculados"); object vertices = Get(p, "reflejoV");
            for (int i = 0; i < 10; i++)
            {
                Wave(p, dev, wave, false);
                Check(SameDraws(first, Reflect(p, dev)) && (int)Get(p, "ReflejosCalculados") == made && ReferenceEquals(vertices, Get(p, "reflejoV")),
                    "entre muestras conserva todos los bytes enviados del reflejo " + w + " cuadro=" + i);
            }
            Check((int)Get(p, "ReflejosReusados") == 10, "evita diez reconstrucciones del reflejo " + w);
            for (int change = 0; change < 7; change++)
            {
                bool fresh = change < 2;
                if (change == 0) ((float[])Get(wave, "B"))[0] = .1f;
                if (change == 1) Set(wave, "RgbA", 0x001122FFu);
                if (change == 2) Set(wave, "BaseY", h * .65f);
                if (change == 3) p.Width += 11;
                if (change == 4) p.Height = 400; // cambia tambien el hueco antes de su tope
                if (change == 5) Set(p, "OndasSuaves", !(bool)Get(p, "OndasSuaves"));
                if (change == 6) Set(p, "AuroraBandas", !(bool)Get(p, "AuroraBandas"));
                made = (int)Get(p, "ReflejosCalculados"); Wave(p, dev, wave, fresh); Reflect(p, dev);
                Check((int)Get(p, "ReflejosCalculados") == made + 1, "reconstruye reflejo al cambiar " + change + " a " + w);
                CheckReflection(p, w);
            }
            Set(p, "ReflejoAurora", false); made = (int)Get(p, "ReflejosCalculados");
            Set(wave, "DimFilo", .2f); Wave(p, dev, wave, true);
            Check(Reflect(p, dev).Count == 0 && (int)Get(p, "ReflejosCalculados") == made, "reflejo apagado no prepara ni envia su malla " + w);
            Set(p, "ReflejoAurora", true); Wave(p, dev, wave, false); Reflect(p, dev);
            Check((int)Get(p, "ReflejosCalculados") == made + 1, "reactivar actualiza lo cambiado mientras estaba apagado " + w); CheckReflection(p, w);
            Set(p, "ReposoSinReflejo", true); Set(p, "idleBlend", 1f);
            Check(Reflect(p, dev).Count == 0, "respeta ahorro de reflejo en reposo " + w);
            Set(p, "idleBlend", 0f); Check(Reflect(p, dev).Count > 0, "al navegar recupera el reflejo " + w);
        }
    }
    [STAThread] static int Main(string[] args)
    {
        Assembly app = Assembly.LoadFrom(args[0]); panel = app.GetType("Rocola.GpuPanel");
        if (args.Length > 1) referenceForm = Assembly.LoadFile(System.IO.Path.GetFullPath(args[1])).GetType("Rocola.Form1");
        if (panel == null) { Console.WriteLine("CPU: sin cambios de renderizado"); return 0; }
        vertex = app.GetType("Rocola.D3D9").GetNestedType("Vtx", B); form = app.GetType("Rocola.Form1");
        foreach (string n in new string[] { "TPanel", "TStar", "TDev", "TD3D", "TVtx", "TVtx2", "TForm" })
        {
            string type = n == "TPanel" ? "GpuPanel" : n == "TStar" ? "GpuStarfield" : n == "TDev" ? "D3D9Device" : n == "TForm" ? "Form1" : "D3D9";
            Type t = app.GetType("Rocola." + type); if (n == "TVtx" || n == "TVtx2") t = t.GetNestedType(n == "TVtx" ? "Vtx" : "Vtx2", B);
            H.GetField(n, B).SetValue(null, t);
        }
        try { if (referenceForm != null) Check(referenceForm.Assembly != app, "comparacion con ensamblado anterior independiente"); Gradients(); foreach (int[] wh in new int[][] { new int[] {1024,768}, new int[] {1280,1024}, new int[] {1920,1080} }) { Sky(wh[0], wh[1]); Reflection(wh[0], wh[1]); } }
        catch (Exception e) { failures++; Console.WriteLine("FALLO " + e); }
        Console.WriteLine(checks + " comprobaciones, " + failures + " fallos. Sin GPU real ni audio."); return failures;
    }
}

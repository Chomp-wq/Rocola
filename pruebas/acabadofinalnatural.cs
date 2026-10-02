using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;

// Fixture aislada, sin Form1 real, preferencias, musica ni dispositivo GPU.
class AcabadoFinalNatural
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Type panel, stars, form, list;
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static object Call(Type t, object o, string name, params object[] args)
    { return t.GetMethod(name, B).Invoke(o, args); }
    static void Set(Type t, object o, string n, object v) { t.GetField(n, B).SetValue(o, v); }
    static object Get(Type t, object o, string n) { return t.GetField(n, B).GetValue(o); }
    static float Idle(float value, bool target, ref float velocity, double dt)
    {
        object[] args = { value, target, velocity, dt };
        float next = (float)Call(panel, null, "PasoReposoNatural", args); velocity = (float)args[2]; return next;
    }
    static void Transitions()
    {
        float prev = 0;
        for (int i = 0; i <= 1000; i++) {
            float t = i / 1000f, mix = (float)Call(stars, null, "RelevoNatural", t);
            Check(mix >= prev && mix <= 1, "Relevo no monotono"); prev = mix;
            Check(Math.Abs(mix + (float)Call(stars, null, "RelevoNatural", 1 - t) - 1) < .000001f, "El relevo abre un hueco oscuro");
        }
        Check((float)Call(stars, null, "RelevoNatural", .001f) < .000004f, "Entrada de patron brusca");
        float reference = 0;
        foreach (int fps in new int[] { 24, 50, 60, 180 }) {
            float value = 0, velocity = 0;
            for (int i = 0; i < fps; i++) {
                float old = value; value = Idle(value, true, ref velocity, 1.0 / fps);
                Check(value >= old && value <= 1, "Reposo oscila o sobrepasa destino");
            }
            if (reference == 0) reference = value;
            Check(Math.Abs(value - reference) < .00001f, "Reposo depende del FPS");
            float beforeVelocity = velocity, before = value;
            value = Idle(value, false, ref velocity, .000001);
            Check(Math.Abs(value - before) < .00001 && Math.Abs(velocity - beforeVelocity) < .001, "Al tocar salta la velocidad/posicion");
            for (int i = 0; i < fps * 2; i++) value = Idle(value, false, ref velocity, 1.0 / fps);
            Check(value == 0 && velocity == 0, "Reposo no termina al volver a controles");
            for (int i = 0; i < fps * 2; i++) value = Idle(value, true, ref velocity, 1.0 / fps);
            Check(value == 1 && velocity == 0, "Reposo no termina al revelar");
        }
        float bad = float.NaN;
        float clean = Idle(float.NaN, true, ref bad, .01);
        Check(!float.IsNaN(clean) && clean >= 0 && clean <= 1, "Reposo propaga NaN");
        foreach (double dt in new double[] { double.NaN, -1, 0 }) {
            float velocity = .5f;
            Check(Idle(.3f, true, ref velocity, dt) == .3f, "Reloj invalido mueve la escena");
        }
    }
    static void Light()
    {
        using (Control c = (Control)Activator.CreateInstance(panel, true)) {
            Set(panel, c, "Dim", .45f);
            Call(panel, c, "CapturaLuzNatural", .1);
            Check((float)panel.GetProperty("SceneDim", B).GetValue(c, null) == .45f, "Legacy interpola luz");
            Set(panel, c, "FondoNatural", true); Call(panel, c, "CapturaLuzNatural", .01);
            Check((float)Get(panel, c, "naturalDim") == .45f, "Cambio de estilo empieza a oscuras");
            Set(panel, c, "Dim", 1f);
            Call(panel, c, "CapturaLuzNatural", 1.0 / 180);
            Check((float)Get(panel, c, "naturalDim") > .45f && (float)Get(panel, c, "naturalDim") < 1, "Luz no interpola el latido");
            Check((float)Get(panel, c, "Dim") == 1f, "Natural modifica fuente compartida");
            for (int i = 0; i < 180; i++) Call(panel, c, "CapturaLuzNatural", 1.0 / 180);
            Check(Math.Abs((float)Get(panel, c, "naturalDim") - 1) < .00001, "Luz no alcanza el destino");
            Set(panel, c, "FondoNatural", false); Call(panel, c, "CapturaLuzNatural", .01);
            Check(!(bool)Get(panel, c, "naturalLightReady"), "Legacy deja luz Natural activa");
        }
        float prevSize = 0, prevLight = 0;
        for (int i = 0; i <= 100; i++) {
            object[] args = { -1f + i * .02f, 0f, 0f }; Call(panel, null, "ProfundidadPolvoNatural", args);
            float size = (float)args[1], light = (float)args[2];
            Check(size >= prevSize && light >= prevLight && size <= 1 && light <= 1, "Polvo se agranda/ilumina hacia el fondo");
            prevSize = size; prevLight = light;
        }
    }
    static int[] Pixels(Bitmap b)
    {
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try {
            int[] data = new int[b.Width * b.Height];
            for (int y = 0; y < b.Height; y++) Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + y * d.Stride), data, y * b.Width, b.Width);
            return data;
        } finally { b.UnlockBits(d); }
    }
    static void Glass(Type baseline, string output)
    {
        Directory.CreateDirectory(output);
        foreach (bool neutral in new bool[] { false, true })
        using (Bitmap legacy = (Bitmap)Call(form, null, "BakeSongGlassTint", 900, 420, neutral))
        using (Bitmap natural = (Bitmap)Call(form, null, "BakeSongGlassStyled", 900, 420, neutral, true)) {
            Color a = legacy.GetPixel(450, 210), b = natural.GetPixel(450, 210);
            Check(b.A >= 210 && b.A < 245 && b.R < a.R && b.G < a.G && b.B < a.B, "Cristal no protege contraste del texto");
            Check(natural.GetPixel(0, 0).A == 0, "Esquinas del vidrio opacas");
            if (neutral) Check(b.R == b.G && b.G == b.B, "Cristal neutral impide tinte GPU");
            natural.Save(Path.Combine(output, neutral ? "glass-neutral.png" : "glass-natural.png"));
            legacy.Save(Path.Combine(output, neutral ? "glass-legacy-neutral.png" : "glass-legacy.png"));
            if (baseline != null) using (Bitmap old = (Bitmap)Call(baseline, null, "BakeSongGlassTint", 900, 420, neutral)) {
                int[] expected = Pixels(old), actual = Pixels(legacy);
                for (int i = 0; i < expected.Length; i++) Check(expected[i] == actual[i], "Cambio visual en vidrio Legacy");
            }
        }
        using (Control songs = (Control)Activator.CreateInstance(list, true))
        using (Control holder = (Control)Activator.CreateInstance(form.GetNestedType("BgPanel", B), true))
        using (Bitmap target = new Bitmap(420, 260))
        using (Graphics g = Graphics.FromImage(target)) {
            songs.Size = new Size(420, 260);
            Set(list, songs, "GetCount", new Func<int>(delegate { return 0; }));
            Call(list, songs, "RenderTransparentGpuMode", g, true);
            Bitmap first = (Bitmap)Get(list, songs, "cristalGpu");
            Call(list, songs, "RenderTransparentGpuMode", g, true);
            Check(ReferenceEquals(first, Get(list, songs, "cristalGpu")), "Vidrio compuesto rehornea por cuadro");
            Set(list, songs, "GpuGlassNatural", true);
            Call(list, songs, "RenderTransparentGpuMode", g, true);
            Bitmap next = (Bitmap)Get(list, songs, "cristalGpu");
            Check(!ReferenceEquals(first, next), "Vidrio compuesto ignora cambio de estilo");
            Call(list, songs, "RenderTransparentGpuMode", g, true);
            Check(ReferenceEquals(next, Get(list, songs, "cristalGpu")), "Vidrio Natural compuesto pierde cache");

            // Solo el proveedor de la capa, no se ejecuta el constructor de Form1.
            object owner = FormatterServices.GetUninitializedObject(form);
            Set(form, owner, "songList", songs); Set(form, owner, "panelSongs", holder);
            Set(form, owner, "uiPieza", new Bitmap[5]); Set(form, owner, "uiPiezaRect", new Rectangle[5]);
            Set(form, owner, "uiPiezaSig", new long[5]); Set(form, owner, "uiPiezaVer", new int[5]);
            int version = (int)Call(form, owner, "EnsureSongPiece", 0);
            Bitmap[] pieces = (Bitmap[])Get(form, owner, "uiPieza"); first = pieces[0];
            Check((int)Call(form, owner, "EnsureSongPiece", 0) == version && ReferenceEquals(first, pieces[0]), "Vidrio separado rehornea sin cambios");
            Set(form, owner, "estiloFondo", 1);
            Check((int)Call(form, owner, "EnsureSongPiece", 0) == version + 1 && !ReferenceEquals(first, pieces[0]), "Vidrio separado ignora Natural");
            next = pieces[0];
            Set(form, owner, "bgAccent", Color.Red);
            Check((int)Call(form, owner, "EnsureSongPiece", 0) == version + 1 && ReferenceEquals(next, pieces[0]), "Cambiar color rehornea vidrio neutral");
            Set(form, owner, "estiloFondo", 0); Call(form, owner, "EnsureSongPiece", 0);
            using (Bitmap old = (Bitmap)Call(form, null, "BakeSongGlassTint", 420, 260, true)) {
                int[] expected = Pixels(old), actual = Pixels(pieces[0]);
                for (int i = 0; i < expected.Length; i++) Check(expected[i] == actual[i], "Volver a Legacy deja vidrio Natural");
            }
            foreach (Bitmap piece in pieces) if (piece != null) piece.Dispose();
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try {
            if (args.Length < 1) throw new Exception("Uso: acabadofinalnatural Rocola.exe [salida] [Release-anterior]");
            Assembly app = Assembly.LoadFile(Path.GetFullPath(args[0]));
            panel = app.GetType("Rocola.GpuPanel", true); stars = app.GetType("Rocola.GpuStarfield", true);
            form = app.GetType("Rocola.Form1", true); list = form.GetNestedType("SongListView", B);
            Type baseline = args.Length > 2 ? Assembly.LoadFile(Path.GetFullPath(args[2])).GetType("Rocola.Form1", true) : null;
            Transitions(); Light(); Glass(baseline, args.Length > 1 ? args[1] : Path.Combine(Path.GetDirectoryName(args[0]), "acabado-natural"));
            Console.WriteLine("PASS acabadofinalnatural: " + checks + " comprobaciones; sin musica, biblioteca ni preferencias."); return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

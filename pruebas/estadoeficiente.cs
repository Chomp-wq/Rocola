using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Regresiones del estado extendido. Compilar junto a franjareloj.cs y leercodigo.cs
// con /main:EstadoEficiente. El controlador artificial cuenta creaciones/bloqueos;
// no mide la velocidad de una GPU ni reproduce Windows XP.
class EstadoEficiente
{
    const BindingFlags B = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, panel, helper = typeof(FranjaReloj);
    static int checks, failures;
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Run(object o, string n, params object[] a) { return Call(o.GetType(), o, n, a); }
    static object Static(string n, params object[] a) { return Call(form, null, n, a); }
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static void Check(bool ok, string text)
    { checks++; if (!ok) failures++; Console.WriteLine((ok ? "OK " : "FALLO ") + text); }
    static byte[] Pixels(Bitmap b)
    {
        byte[] data = new byte[b.Width * b.Height * 4];
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try { for (int y = 0; y < b.Height; y++) Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), data, y * b.Width * 4, b.Width * 4); }
        finally { b.UnlockBits(d); }
        return data;
    }
    static bool Equal(byte[] a, byte[] b)
    { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    static Bitmap Draw(Bitmap reuse, int w, bool transparent, bool dozen, bool opaque, string title, int count)
    {
        string[] titles = new string[count], artists = new string[count]; Image[] covers = new Image[count];
        for (int i = 0; i < count; i++) { titles[i] = "Cancion " + i; artists[i] = "Artista " + i; }
        object[] a = { reuse, w, 245, title, "Artista", "", titles, artists, covers, count, Color.Teal, transparent, 1, dozen, null, opaque, 0 };
        return (Bitmap)Static("RenderIdlePanelEn", a);
    }
    static void Bitmaps()
    {
        foreach (int w in new int[] { 800, 1024, 1920 })
        foreach (bool transparent in new bool[] { false, true })
        foreach (bool dozen in new bool[] { false, true })
        using (Bitmap reused = Draw(null, w, transparent, dozen, false, "Titulo anterior muy largo", dozen ? 12 : 3))
        {
            Bitmap next = Draw(reused, w, transparent, dozen, false, "Nuevo", 0);
            using (Bitmap fresh = Draw(null, w, transparent, dozen, false, "Nuevo", 0))
                Check(ReferenceEquals(reused, next) && Equal(Pixels(reused), Pixels(fresh)), "mapa reutilizado sin texto ni alfa residual " + w + "/" + transparent + "/" + dozen);
            using (Bitmap opaque = Draw(null, w, transparent, dozen, true, "Opaco", 3))
            {
                bool all = true; byte[] bytes = Pixels(opaque);
                for (int i = 3; i < bytes.Length; i += 4) if (bytes[i] != 255) all = false;
                Check(all, "opcion opaca cubre todos los pixeles incluso con transparencia configurada");
            }
        }
        using (Bitmap old = Draw(null, 800, false, false, false, "Anterior", 3))
        using (Bitmap resized = Draw(old, 1024, false, false, false, "Nuevo", 3))
            Check(!ReferenceEquals(old, resized) && resized.Width == 1024 && old.Width == 800, "cambio de resolucion conserva el anterior hasta publicar el nuevo");
    }
    static void Clock()
    {
        foreach (int w in new int[] { 800, 1024, 1920 })
        using (Bitmap a = Draw(null, w, true, true, false, "Titulo", 12))
        using (Bitmap clean = new Bitmap(w, 34, PixelFormat.Format32bppPArgb))
        {
            Static("CopiaRegionReposo", a, new Rectangle(0, 211, w, 34), clean, Point.Empty);
            int previous = 0;
            foreach (string time in new string[] { "0:59 / 4:00", "1:00 / 4:00", "59:59 / 100:00", "0:00 / 1:00", "" })
            using (Bitmap expected = Draw(null, w, true, true, false, "Titulo", 12))
            {
                Static("UpdateIdleClock", expected, clean, time);
                object[] args = { a, clean, time, previous, Rectangle.Empty };
                previous = (int)Static("UpdateIdleClockRegion", args);
                Rectangle dirty = (Rectangle)args[4];
                Check(Equal(Pixels(a), Pixels(expected)), "reloj regional identico al completo " + w + " " + time);
                Check(dirty.Width < w / 2 && dirty.Height == 34, "solo se ensucia el recuadro de digitos");
            }
        }
    }
    static IList Locks() { return (IList)helper.GetField("bloqueos", B).GetValue(null); }
    static int Creations() { return (int)helper.GetField("creaciones", B).GetValue(null); }
    static void Texture()
    {
        Assembly app = form.Assembly;
        foreach (string field in new string[] { "TF", "TPanel", "TStar", "TDev", "TD3D" })
        {
            string name = field == "TF" ? "Form1" : field == "TPanel" ? "GpuPanel" : field == "TStar" ? "GpuStarfield" : field == "TDev" ? "D3D9Device" : "D3D9";
            helper.GetField(field, B).SetValue(null, app.GetType("Rocola." + name));
        }
        using (Control p = (Control)Call(helper, null, "Panel", 1024, 768))
        using (Bitmap b = Draw(null, 1024, false, false, true, "Primera", 3))
        {
            Set(p, "IdlePanelVersion", 1); int start = Creations(); Locks().Clear();
            Run(p, "SubeEstadoExtendido", b); IntPtr first = (IntPtr)Get(p, "idleTex");
            Check(first != IntPtr.Zero && Creations() == start + 1, "primera subida crea una textura propia");
            Locks().Clear(); Run(p, "SubeEstadoExtendido", b);
            Check(Locks().Count == 0 && Creations() == start + 1, "misma revision no toca el controlador");
            Set(p, "IdlePanelVersion", 2); Set(p, "IdlePanelDirty", Rectangle.Empty);
            Run(p, "SubeEstadoExtendido", b);
            Check((IntPtr)Get(p, "idleTex") == first && Creations() == start + 1, "nuevos textos reutilizan la textura completa");
            Call(helper, null, "PorTrozos", first);
            Rectangle patch = new Rectangle(170, 211, 200, 34);
            using (Graphics g = Graphics.FromImage(b)) g.FillRectangle(Brushes.Gold, patch);
            Set(p, "IdlePanelVersion", 3); Set(p, "IdlePanelDirty", patch); Locks().Clear();
            Run(p, "SubeEstadoExtendido", b);
            Check(Locks().Count == 1 && (Rectangle)Get(Locks()[0], "Zona") == Rectangle.FromLTRB(patch.Left, patch.Top, patch.Right, 256), "revision inmediata sube solo el recuadro");
            Check((int)Call(helper, null, "DistintosEnTextura", first, b) == 0, "textura regional conserva todos los pixeles");
            Set(p, "IdlePanelVersion", 5); Locks().Clear(); Run(p, "SubeEstadoExtendido", b);
            Check(Locks().Count == 1 && ((Rectangle)Get(Locks()[0], "Zona")).IsEmpty, "revision saltada sube mapa completo sin perder cambios");
            Set(p, "EstadoOpaco", true); Set(p, "idleBlend", 1f); Set(p, "ShowAlbum", false);
            Run(p, "HuecosDePortadas");
            Check((int)Get(p, "nHuecos") == 1, "estado opaco abierto recorta el fondo incluso sin pared");
            Check((bool)Run(p, "EstadoOcultaDesde", 530f) && !(bool)Run(p, "EstadoOcultaDesde", 520f), "solo se omiten reflejos completamente bajo el estado opaco");
            Set(p, "idleBlend", .99f); Run(p, "HuecosDePortadas");
            Check((int)Get(p, "nHuecos") == 0, "la transicion conserva todo el fondo");
            Set(p, "idleBlend", 1f); Set(p, "IdlePanelVersion", 6); Run(p, "HuecosDePortadas");
            Check((int)Get(p, "nHuecos") == 0, "textura aun sin actualizar no oculta el fondo");
            Set(p, "IdlePanelVersion", 5); Set(p, "EstadoOpaco", false); Run(p, "HuecosDePortadas");
            Check((int)Get(p, "nHuecos") == 0, "opcion apagada conserva transparencia");
            using (Bitmap wider = new Bitmap(1280, 245))
            { Set(p, "IdlePanelVersion", 6); Run(p, "SubeEstadoExtendido", wider); int created = Creations(); Check(created > start + 1, "resolucion distinta recrea respetando el limite del driver"); Run(p, "SubeEstadoExtendido", wider); Check(Creations() == created, "mapa grande dividido no se recrea en cada cuadro"); }
        }
    }
    static void Options(string dir)
    {
        object f = Codigo.FormSinEncender(form); Set(f, "carpetaDatos", dir);
        string[] names = { "estadoOpacoPerf", "auroraLigeraPerf", "reposoSinReflejo", "reposoSinEstela", "coronaCompacta", "estadoColorDirecto" };
        for (int i = 0; i < names.Length; i++) Set(f, names[i], true);
        Run(f, "SaveConfigPrefs"); Codigo.EsperaGuardado(form, f);
        object again = Codigo.FormSinEncender(form); Set(again, "carpetaDatos", dir); Run(again, "LoadConfigPrefs");
        foreach (string name in names) Check((bool)Get(again, name), "persiste " + name);
        Run(again, "PonAhorros", false);
        foreach (string name in names) Check(!(bool)Get(again, name), "apagado independiente de fabrica " + name);
        for (int i = 4; i < 10; i++)
        {
            Run(again, "PonAhorro", i, true);
            for (int k = 0; k < names.Length; k++) Check((bool)Get(again, names[k]) == (k == i - 4), "interruptores independientes");
            Run(again, "PonAhorro", i, false);
        }
        Set(again, "estadoColorDirecto", true); Set(again, "bgAccentTarget", Color.Teal);
        Run(again, "CambioElAcentoDelPanel", Color.Red); int data = (int)Get(again, "idlePanelDatos");
        Run(again, "CambioElAcentoDelPanel", Color.Blue);
        Check(data == (int)Get(again, "idlePanelDatos"), "color directo no repinta los pasos intermedios");
        // El medidor tiene una escena real de estado extendido y devuelve los ajustes incluso al salir.
        string[] scenes = (string[])form.GetField("NombreEscena", B).GetValue(null);
        Check(Array.IndexOf(scenes, "Estado extendido") >= 0, "medidor incluye el estado extendido");
    }
    static void Mesh()
    {
        int cols = (int)panel.GetField("AurCols", B).GetRawConstantValue();
        int rows = (int)panel.GetField("AurFilas", B).GetRawConstantValue();
        int ground = (int)panel.GetField("AurSuelo", B).GetRawConstantValue();
        int glow = (int)panel.GetField("AurBrilloVtx", B).GetRawConstantValue();
        Type vertex = form.Assembly.GetType("Rocola.D3D9").GetNestedType("Vtx", B);
        Array full = Array.CreateInstance(vertex, Math.Max(glow, (rows + ground) * cols * 6));
        Array light = Array.CreateInstance(vertex, full.Length);
        float[] heights = new float[cols + 1], alpha = new float[cols + 1], ray = new float[cols + 1];
        uint[] curtain = new uint[(rows + 1) * (cols + 1)], floor = new uint[(ground + 1) * (cols + 1)], colors = new uint[cols + 1];
        for (int i = 0; i <= cols; i++) { heights[i] = 40 + 30 * (float)Math.Sin(i * .04); alpha[i] = .8f; ray[i] = 1; colors[i] = 0x00BBCCFF; }
        for (int i = 0; i < curtain.Length; i++) curtain[i] = 0x00BBCCFF;
        for (int i = 0; i < floor.Length; i++) floor[i] = 0x00BBCCFF;
        int a = (int)Call(panel, null, "AurMalla", full, heights, curtain, floor, alpha, ray, 1024f, 500f, 768f);
        int b = (int)Call(panel, null, "AurMallaLigera", light, heights, curtain, floor, alpha, ray, 1024f, 500f, 768f, 2);
        Check(a == 2 * b && b > 0, "aurora ligera reduce a la mitad los vertices del relleno");
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < b; i++) { float x = (float)Get(light.GetValue(i), "X"); min = Math.Min(min, x); max = Math.Max(max, x); }
        Check(Math.Abs(min + .5f) < .001f && Math.Abs(max - 1023.5f) < .001f, "la aurora ligera sigue cubriendo todo el ancho");
        a = (int)Call(panel, null, "AurBrillo", full, heights, colors, alpha, colors, alpha, 1024f, 500f);
        b = (int)Call(panel, null, "AurBrilloLigero", light, heights, colors, alpha, colors, alpha, 1024f, 500f, 2);
        Check(a == 2 * b && b > 0, "tambien reduce a la mitad el filo y la linea del horizonte");
    }
    static void Layout()
    {
        object f = Codigo.FormSinEncender(form);
        Type card = form.GetNestedType("Tarjeta", B);
        using (Control sheet = (Control)Activator.CreateInstance(card, true))
        using (Font font = new Font(FontFamily.GenericSansSerif, 10))
        using (Form host = new Form())
        {
            host.ClientSize = new Size(480, 660); host.Controls.Add(sheet);
            sheet.Size = new Size(456, 624);
            object[] args = { sheet, font, font, Color.White, Color.Gray, null, null, null };
            Run(f, "LlenaHojaPerformance", args);
            Array switches = (Array)args[5];
            Check(switches.Length == 10, "Performance muestra todos los ahorros independientes");
            ScrollableControl body = sheet.Controls[0] as ScrollableControl;
            Check(body != null && body.AutoScroll && body.Top >= 40, "lista desplazable conserva la cabecera de pestañas");
            host.Show(); Application.DoEvents(); sheet.CreateControl(); body.CreateControl();
            for (int i = 0; i < switches.Length; i++)
            {
                Control control = (Control)switches.GetValue(i);
                body.ScrollControlIntoView(control);
                Check(control.Top >= body.ClientRectangle.Top && control.Bottom <= body.ClientRectangle.Bottom,
                    "interruptor accesible al desplazar: " + control.Text);
                body.AutoScrollPosition = Point.Empty;
            }
        }
    }
    [STAThread] static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "estado-eficiente-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("ROCOLA_DATOS", dir);
        try
        {
            Assembly app = Assembly.LoadFrom(args[0]); form = app.GetType("Rocola.Form1"); panel = app.GetType("Rocola.GpuPanel");
            if (panel == null) { Check(form.GetMethod("RenderIdlePanelEn", B) == null, "CPU conserva su renderizador"); return failures; }
            Bitmaps(); Clock(); Texture(); Options(dir); Mesh(); Layout();
        }
        catch (Exception e) { failures++; Console.WriteLine("FALLO " + e); }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(checks + " comprobaciones, " + failures + " fallos"); return failures == 0 ? 0 : 1;
    }
}

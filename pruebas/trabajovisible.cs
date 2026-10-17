using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Controlador/raster de performance.cs. Compilar con /main:TrabajoVisible y leercodigo.cs.
// No hay GPU, audio ni reloj de Windows XP. Comprueba bytes enviados e invalidaciones.
class TrabajoVisible
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type H = typeof(Performance), panel, vertex, tiles;
    static int checks, failures;
    static Bitmap idle;
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
    static Bitmap Idle() { return idle; }
    static void Holes(object p, params int[] holes)
    { Array.Copy(holes, (Array)Get(p, "huecos"), holes.Length); Set(p, "nHuecos", holes.Length / 4); }
    static IList Layer(object p, object dev, params object[] args)
    { Capture(); Run(dev, "BeginFrame"); Run(p, "DibujaTeselasFondo", args); Run(dev, "EndFrame"); return Capture(); }
    static void ClearCache(object p)
    { Array a = (Array)Get(p, "recortesFondo"); Array.Clear(a, 0, a.Length); }
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
            Holes(p, 22, 30, w / 3, h / 2, w / 2, h / 3, w - 10, h - 8);
            IList original = Layer(p, dev, args); int made = (int)Get(p, "RecortesCalculados");
            // El scratch cambia entre capas. El acierto debe usar una copia independiente.
            Array scratch = (Array)Get(p, "trozosHuecos"); Array.Clear(scratch, 0, scratch.Length);
            IList reused = Layer(p, dev, args);
            Check(SameDraws(original, reused) && (int)Get(p, "RecortesCalculados") == made && (int)Get(p, "RecortesReusados") > 0,
                "recorte reusado byte a byte con scratch sobrescrito " + w);
            if (w == 320)
            {
                int[] a = (int[])Help("Fondo", w, h, 13), b = (int[])a.Clone();
                Help("Pinta", original, a, w, h); Help("Pinta", reused, b, w, h);
                bool equal = true; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) equal = false;
                Check(equal, "raster identico del recorte guardado a 320x240");
            }
            for (int change = 0; change < 9; change++)
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
                    case 7: Holes(p, 23, 31, w / 3 + 2, h / 2 + 3); break;
                    case 8: args[1] = Call(tiles, null, "De", bmp, 4, true); break;
                }
                made = (int)Get(p, "RecortesCalculados"); IList changed = Layer(p, dev, args);
                Check((int)Get(p, "RecortesCalculados") == made + 1, "invalida dato cambiado " + change + " a " + w);
                ClearCache(p); IList reference = Layer(p, dev, args);
                Check(SameDraws(changed, reference), "dato cambiado coincide con recalculo completo " + change + " a " + w);
            }
            ClearCache(p);
            for (int pass = 0; pass < 2; pass++)
            {
                made = (int)Get(p, "RecortesCalculados");
                for (int i = 0; i < 4; i++) { args[8] = 0xFFAABBCCu + (uint)i; Layer(p, dev, args); }
                Check((int)Get(p, "RecortesCalculados") == made + (pass == 0 ? 4 : 0), "cuatro capas alternas conservan sus recortes " + w + " pasada=" + pass);
            }
            Holes(p); IList clear = Layer(p, dev, args); Check(clear.Count > 0, "quitar todos los huecos restaura la capa " + w);
            Holes(p, -100, -100, w + 100, h + 100);
            Check(Layer(p, dev, args).Count == 0 && Layer(p, dev, args).Count == 0, "capa completamente tapada: cache vacia valida " + w);
            Array caches = (Array)Get(p, "recortesFondo");
            bool bounded = caches.Length == 4;
            foreach (object c in caches) if (c != null && ((Array)Get(c, "Vertices")).Length > 8192) bounded = false;
            Check(bounded, "cuatro reservas acotadas, sin guardar texturas " + w);
            // Cielo intermedio: la textura puede cambiar pero su recorte sigue siendo el mismo.
            Set(p, "SinGrano", true); Set(p, "CieloMedia", true); Run(p, "EnsureNieblas");
            Holes(p, 0, h * 2 / 3, w, h);
            object[] sky = { w, h, h * .42f, 0xFF03050Au, 0xFF102040u, 0xFF392040u };
            Capture(); Run(dev, "BeginFrame"); Run(p, "CieloMedioBase", sky); Run(dev, "EndFrame"); Capture();
            object saved = Get(p, "recorteMedia"); object vertices = Get(saved, "Vertices");
            made = (int)Get(p, "RecortesCalculados"); int reusedBefore = (int)Get(p, "RecortesReusados");
            Set(p, "NebFase", 1.25f); // obliga a repintar la imagen, no su malla
            Run(dev, "BeginFrame"); Run(p, "CieloMedioBase", sky); Run(dev, "EndFrame"); IList savedDraws = Capture();
            Check((int)Get(p, "RecortesCalculados") == made && (int)Get(p, "RecortesReusados") > reusedBefore
                && ReferenceEquals(vertices, Get(saved, "Vertices")), "repintar el cielo conserva su recorte y reserva " + w);
            // Comparar el envio final de pantalla con una reconstruccion independiente.
            object last = savedDraws[savedDraws.Count - 1]; Set(p, "recorteMedia", null);
            Run(dev, "BeginFrame"); Run(p, "CieloMedioBase", sky); Run(dev, "EndFrame"); IList fresh = Capture();
            Check(Equal((byte[])Get(last, "Datos"), (byte[])Get(fresh[fresh.Count - 1], "Datos")), "estirado guardado exacto " + w);
            foreach (bool quarter in new bool[] { true, false })
            {
                Set(p, "CieloCuarto", quarter); made = (int)Get(p, "RecortesCalculados");
                Run(dev, "BeginFrame"); Run(p, "CieloMedioBase", sky); Run(dev, "EndFrame"); Capture();
                Check((int)Get(p, "RecortesCalculados") == made + 1, "media/cuarto reconstruye coordenadas " + w + " cuarto=" + quarter);
            }
        }
    }
    static int Build(object sf, Array v, int w, int h, float pulse, double time)
    {
        return (int)Run(sf, "BuildStarVerts", v, Get(sf, "sx"), Get(sf, "sy"), Get(sf, "sphase"), Get(sf, "sspeed"),
            Get(sf, "ssize"), Get(sf, "skind"), Get(sf, "sforma"), Get(sf, "scol"), Get(sf, "nStars"), time, .83f, pulse,
            new float[] { 4, 7, 11 }, new float[] { 9, 13, 17 }, (float)w, (float)h);
    }
    static void Stars(int w, int h)
    {
        object dev = Help("Tarjeta", w, h, false, true, true), sf = Help("Cielo", dev, w, h);
        using (Control p = (Control)Help("Panel", w, h, sf))
        {
            int count = (int)Get(sf, "nStars"); Array full = Array.CreateInstance(vertex, count * 6), cut = Array.CreateInstance(vertex, count * 6);
            int omitted = 0;
            foreach (bool fine in new bool[] { false, true }) foreach (float pulse in new float[] { 0f, .7f, 1f })
            {
                Set(sf, "EstrellasFinas", fine); Set(sf, "EstrellasTapadasDesde", 0f);
                int nf = Build(sf, full, w, h, pulse, 731.37); Check(nf == count * 6, "sin estado conserva todas las estrellas " + w);
                float limit = h * .68f; Set(sf, "EstrellasTapadasDesde", limit); int nc = Build(sf, cut, w, h, pulse, 731.37);
                Array expected = Array.CreateInstance(vertex, nf); int ne = 0;
                for (int i = 0; i < nf; i += 6)
                {
                    float top = float.MaxValue;
                    for (int j = 0; j < 6; j++) top = Math.Min(top, (float)Get(full.GetValue(i + j), "Y"));
                    if (top - .5f >= limit) continue;
                    Array.Copy(full, i, expected, ne, 6); ne += 6;
                }
                Check(nc == ne && nc < nf && Equal(Bytes(expected, ne), Bytes(cut, nc)),
                    "omite solo sprites enteros; conserva orden/posicion/UV/color " + w + " finas=" + fine + " latido=" + pulse);
                omitted = (nf - nc) / 6;
                Set(sf, "EstrellasTapadasDesde", 0f); int restored = Build(sf, cut, w, h, pulse, 731.37);
                Check(restored == nf && Equal(Bytes(full, nf), Bytes(cut, restored)), "restauracion exacta sin reiniciar patron " + w);
            }
            if (w == 320)
            {
                Set(sf, "EstrellasTapadasDesde", h * .68f); int nc = Build(sf, cut, w, h, 1f, 731.37);
                using (Bitmap atlas = (Bitmap)Call(sf.GetType(), null, "BakeStarTexture", 64))
                using (Bitmap cover = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(cover)) g.Clear(Color.DarkSlateBlue);
                    object[] upload = { atlas, 0f, 0f }; IntPtr texture = (IntPtr)Run(dev, "CreateTexturePow2", upload);
                    upload[0] = cover; IntPtr panelTexture = (IntPtr)Run(dev, "CreateTexturePow2", upload);
                    Array quad = Array.CreateInstance(vertex, 6);
                    Call(panel, null, "SetTexQuadA", quad, 0f, h * .68f, (float)w, (float)h, (float)upload[1], (float)upload[2], 0xFFFFFFFFu);
                    Capture(); Run(dev, "BeginFrame"); Run(dev, "DrawStars", full, count * 2, texture); Run(dev, "DrawOpaqueQuad", panelTexture, quad); Run(dev, "EndFrame"); IList before = Capture();
                    Run(dev, "BeginFrame"); Run(dev, "DrawStars", cut, nc / 3, texture); Run(dev, "DrawOpaqueQuad", panelTexture, quad); Run(dev, "EndFrame"); IList after = Capture();
                    int[] a = (int[])Help("Fondo", w, h, 17), b = (int[])a.Clone();
                    Help("Pinta", before, a, w, h); Help("Pinta", after, b, w, h);
                    bool equal = true; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) equal = false;
                    Check(equal, "raster identico con atlas real: estrellas ocultas y panel a 320x240");
                    Call(sf.GetType().Assembly.GetType("Rocola.D3D9"), null, "Release", texture);
                    Call(sf.GetType().Assembly.GetType("Rocola.D3D9"), null, "Release", panelTexture);
                }
            }
            int ih = (int)Call(panel, null, "IdlePanelHeight", h);
            idle = new Bitmap(w, ih, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics g = Graphics.FromImage(idle)) g.Clear(Color.DarkSlateBlue);
                Set(p, "ConLuna", false); Set(p, "SinGrano", true); Set(p, "GetIdlePanel", new Func<Bitmap>(Idle));
                Set(p, "uiBotH", 96); Set(p, "IdlePanelVersion", 1); Set(p, "IdleExpandTarget", true); Set(p, "idleBlend", 1f); Set(p, "EstadoOpaco", true);
                Capture(); Run(p, "RenderFrame"); Capture();
                Check((int)Get(sf, "EstrellasOmitidas") > 0 && (float)Get(sf, "EstrellasTapadasDesde") > 0, "cuadro real activa limite solo tras subir estado " + w);
                Run(sf, "RenuevaPatron", w, h, 9987, Environment.TickCount - 50);
                Run(p, "RenderFrame"); Capture();
                Check((bool)Get(sf, "transing") && (int)Get(sf, "EstrellasOmitidas") > 0
                    && (int)Get(sf, "EstrellasOmitidas") + (int)Get(sf, "EstrellasEnviadas") == (int)Get(sf, "nStars") + (int)Get(sf, "pnStars"),
                    "el cruce conserva ambos patrones y envia sus cantidades compactadas " + w);
                // Versión pendiente, estado incompleto o transparente: nunca omitir.
                Set(p, "IdlePanelVersion", 2); Run(p, "DecideAhorros");
                Check((float)Get(sf, "EstrellasTapadasDesde") == 0f, "version pendiente restaura todas " + w);
                Set(p, "IdlePanelVersion", 1); Set(p, "idleBlend", .999f); Run(p, "DecideAhorros");
                Check((float)Get(sf, "EstrellasTapadasDesde") == 0f, "fundido conserva todas " + w);
                Set(p, "idleBlend", 1f); Set(p, "EstadoOpaco", false); Run(p, "DecideAhorros");
                Check((float)Get(sf, "EstrellasTapadasDesde") == 0f, "transparencia conserva todas " + w);
                Console.WriteLine("ESTRELLAS " + w + "x" + h + " campo=" + count + " omitidas=" + omitted + " vertices_evitados=" + omitted * 6);
            }
            finally { idle.Dispose(); idle = null; }
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
        try { Cache(320, 240); Stars(320, 240); foreach (int[] wh in new int[][] { new int[] {1024,768}, new int[] {1280,1024}, new int[] {1920,1080} }) { Cache(wh[0], wh[1]); Stars(wh[0], wh[1]); } }
        catch (Exception e) { failures++; Console.WriteLine("FALLO " + e); }
        Console.WriteLine(checks + " comprobaciones, " + failures + " fallos. Sin GPU real ni audio."); return failures;
    }
}

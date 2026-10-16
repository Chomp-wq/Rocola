using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Compilar junto a franjareloj.cs, buscaportadas.cs y leercodigo.cs, con /main:RenderCache.
// Reutiliza sus dispositivos imitados; no abre musica ni necesita una tarjeta real.
class RenderCache
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    // 'groups' lo lee el metodo dinamico de GroupsGetter (Ldsfld): el compilador no lo ve y avisaria (CS0414).
#pragma warning disable 414
    static int checks; static Assembly app; static object groups; static Rectangle[] changes;
#pragma warning restore 414
    static Type H = typeof(FranjaReloj), B = typeof(BuscaPortadas);
    static void Check(bool value, string text) { if (!value) throw new Exception(text); checks++; }
    static object Get(object o, string n) { return o.GetType().GetField(n, F).GetValue(o); }
    static void Set(object o, string n, object value) { o.GetType().GetField(n, F).SetValue(o, value); }
    static object Call(object o, string n, params object[] args) { return o.GetType().GetMethod(n, F).Invoke(o, args); }
    static object Static(Type t, string n, params object[] args) { return t.GetMethod(n, F).Invoke(null, args); }
    static void Init(Type helper, string field, string type) { helper.GetField(field, F).SetValue(null, app.GetType(type)); }
    static Delegate GroupsGetter(Type type)
    {
        DynamicMethod method = new DynamicMethod("Groups", type.GetMethod("Invoke").ReturnType, Type.EmptyTypes, typeof(RenderCache).Module, true);
        ILGenerator il = method.GetILGenerator(); il.Emit(OpCodes.Ldsfld, typeof(RenderCache).GetField("groups", F));
        il.Emit(OpCodes.Castclass, type.GetMethod("Invoke").ReturnType); il.Emit(OpCodes.Ret); return method.CreateDelegate(type);
    }
    static Rectangle[] Dirty(int piece, int version) { return version == 1 ? changes : null; }
    static object Run(string key, int width)
    {
        object run = Activator.CreateInstance(app.GetType("Rocola.CintaTexto"), true);
        Set(run, "Key", key); Set(run, "Width", width); Set(run, "Rect", new RectangleF(0, 0, 100, 30));
        Set(run, "Tiles", new Bitmap[] { new Bitmap(width, 30, PixelFormat.Format32bppPArgb) }); return run;
    }
    static IList Locks { get { return (IList)H.GetField("bloqueos", F).GetValue(null); } }
    static void Cintas()
    {
        using (Control panel = (Control)Static(H, "Panel", 1024, 768))
        {
            Type group = app.GetType("Rocola.CintasTexto"); Array array = Array.CreateInstance(group, 5); groups = array;
            array.SetValue(Activator.CreateInstance(group, true), 0); array.SetValue(Activator.CreateInstance(group, true), 1);
            IList compact = (IList)Get(array.GetValue(0), "Items"), queue = (IList)Get(array.GetValue(1), "Items");
            compact.Add(Run("compact", 800)); for (int i = 0; i < 20; i++) queue.Add(Run("queue" + i, 1024));
            FieldInfo field = panel.GetType().GetField("GetCintasTexto", F); field.SetValue(panel, GroupsGetter(field.FieldType));
            // Las dos franjas estan visibles: sus textos deben subirse en este mismo cuadro.
            // Con idleBlend=0 la cola esta oculta y, correctamente, tiene presupuesto de precarga.
            Set(panel, "idleBlend", .5f);
            Locks.Clear(); Call(panel, "PreparaCintasTexto"); Check(Locks.Count == 21, "primera subida de cintas");
            Locks.Clear(); Call(panel, "PreparaCintasTexto"); Check(Locks.Count == 0, "cintas quietas se resuben");
            compact.Add(Run("newcompact", 600)); Locks.Clear(); Call(panel, "PreparaCintasTexto");
            Check(Locks.Count == 1, "agregar una cinta desplaza y resube la cola: " + Locks.Count);
            Console.WriteLine("  OK cintas: agregar una = 1 subida/72.000 bytes, antes 21/2.529.600");
            compact.RemoveAt(0); Locks.Clear(); Call(panel, "PreparaCintasTexto"); Check(Locks.Count == 0, "quitar cinta resube otras");
            Check(((IDictionary)Get(panel, "cintaByBitmap")).Count == 21, "textura retirada retenida");
            Call(panel, "SueltaCintasGpu"); Check(((IDictionary)Get(panel, "cintaByBitmap")).Count == 0, "reinicio retiene texturas");
            Locks.Clear(); Call(panel, "PreparaCintasTexto"); Check(Locks.Count == 21, "reinicio no recupera todas las cintas");
            Call(panel, "SueltaCintasGpu"); groups = null;
            foreach (object g in array) if (g != null) ((IDisposable)g).Dispose();
        }
        using (Control panel = (Control)Static(H, "Panel", 1024, 768))
        {
            Type group = app.GetType("Rocola.CintasTexto"); Array array = Array.CreateInstance(group, 5); groups = array;
            array.SetValue(Activator.CreateInstance(group, true), 0); array.SetValue(Activator.CreateInstance(group, true), 1);
            IList compact = (IList)Get(array.GetValue(0), "Items"), queue = (IList)Get(array.GetValue(1), "Items");
            compact.Add(Run("compact-visible", 800)); for (int i = 0; i < 20; i++) queue.Add(Run("queue-hidden" + i, 1024));
            FieldInfo field = panel.GetType().GetField("GetCintasTexto", F); field.SetValue(panel, GroupsGetter(field.FieldType));
            Set(panel, "idleBlend", 0f); Locks.Clear(); Call(panel, "PreparaCintasTexto");
            IDictionary tiles = (IDictionary)Get(panel, "cintaByBitmap");
            Bitmap visible = ((Bitmap[])Get(compact[0], "Tiles"))[0];
            Check(tiles.Count == 21 && (IntPtr)Get(tiles[visible], "Tex") != IntPtr.Zero, "la compacta visible espera por la cola oculta");
            int loaded = 0; foreach (object tile in tiles.Values) if ((IntPtr)Get(tile, "Tex") != IntPtr.Zero) loaded++;
            Check(loaded == 1, "la cola completamente oculta recibe subidas especulativas");
            Set(panel, "idleBlend", .5f); Locks.Clear(); Call(panel, "PreparaCintasTexto");
            Check(Locks.Count == 21 - loaded, "al aparecer la cola no recupera todas sus cintas pendientes");
            foreach (object tile in tiles.Values) Check((IntPtr)Get(tile, "Tex") != IntPtr.Zero, "una cinta visible sigue sin textura");
            Locks.Clear(); Call(panel, "PreparaCintasTexto"); Check(Locks.Count == 0, "mostrar la cola resube cintas ya preparadas");
            // Una retirada pendiente debe liberarse tambien al apagar/reiniciar, no solo con otro cuadro.
            // Se mueve una textura existente a la cola de retiros; no se fabrica ningun puntero COM.
            IList retired = (IList)Get(panel, "cintaRetiradas");
            retired.Add(Get(tiles[visible], "Tex")); tiles.Remove(visible);
            Check(Codigo.Llama(panel.GetType().GetMethod("StopGpu", F), "SueltaCintasGpu"), "StopGpu no libera las cintas");
            Call(panel, "SueltaCintasGpu");
            Check(tiles.Count == 0 && retired.Count == 0 && ((IList)Get(panel, "cintaTiles")).Count == 0, "apagado retiene cintas o retiradas pendientes");
            groups = null; foreach (object g in array) if (g != null) ((IDisposable)g).Dispose();
            Console.WriteLine("  OK cintas ocultas: visible inmediata, cola completa al aparecer y retiradas vaciadas al apagar");
        }
        using (Bitmap bitmap = new Bitmap(600, 100)) using (Graphics g = Graphics.FromImage(bitmap))
        using (Font font = new Font("Tahoma", 20, FontStyle.Bold, GraphicsUnit.Pixel)) using (StringFormat sf = new StringFormat())
        {
            object group = Activator.CreateInstance(app.GetType("Rocola.CintasTexto"), true);
            RectangleF rect = new RectangleF(0, 0, 80, 30); string title = "Un titulo muy largo que se debe conservar";
            Call(group, "Begin"); Call(group, "Add", g, title, font, Color.White, rect, sf); Call(group, "End");
            object original = ((IList)Get(group, "Items"))[0];
            Call(group, "Begin"); Call(group, "Add", g, "Otra cancion mucho mas larga", font, Color.White, rect, sf);
            Call(group, "Add", g, title, font, Color.White, rect, sf); Call(group, "End");
            Check(ReferenceEquals(original, ((IList)Get(group, "Items"))[1]), "insertar otro titulo hornea y reinicia el existente");
            Call(group, "Begin"); Call(group, "Add", g, title, font, Color.White, rect, sf); Call(group, "End");
            Check(ReferenceEquals(original, ((IList)Get(group, "Items"))[0]), "retirar otro titulo no conserva la cinta");
            ((IDisposable)group).Dispose();
        }
    }
    static Rectangle[] Paint(Control overlay, Bitmap bitmap, Rectangle bounds, bool full)
    {
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SetClip(new Rectangle(0, 0, bitmap.Width, bitmap.Height));   // (ver Completa)
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            return (Rectangle[])Call(overlay, "PintaCartasConCambios", g, -bounds.X, -bounds.Y, full);
        }
    }
    // La pieza entera de referencia, como Pieza (buscaportadas), pero con un recorte rectangular de toda la imagen.
    // En Windows no cambia nada: GDI+ nunca suaviza un recorte. En Mono (libgdiplus) un recorte con forma se suaviza
    // si va solo y no si se cruza con otro, y la tarjeta de iniciales recorta su brillo con su forma: sin esto, la
    // reconstruccion por recuadros diferia en unos pixeles de esa esquina que en Windows salen iguales.
    static Bitmap Completa(Control overlay)
    {
        object[] a = { 4, Rectangle.Empty }; Call(overlay, "PiezaViva", a); Rectangle donde = (Rectangle)a[1];
        Bitmap b = new Bitmap(donde.Width, donde.Height, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SetClip(new Rectangle(0, 0, b.Width, b.Height));
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            Call(overlay, "DibujaPiezaViva", g, 4, -donde.X, -donde.Y);
        }
        return b;
    }
    static void Same(Control overlay, Bitmap actual)
    {
        using (Bitmap expected = Completa(overlay))
        {
            int different = (int)Static(B, "Distintos", actual, expected);
            if (different != 0)
            {
                int reported = 0;
                for (int y = 0; y < actual.Height; y++) for (int x = 0; x < actual.Width; x++)
                    if (actual.GetPixel(x, y) != expected.GetPixel(x, y) && reported++ < 4)
                        Console.WriteLine("  pixel " + x + "," + y + ": " + actual.GetPixel(x, y) + " / " + expected.GetPixel(x, y));
                actual.Save(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rendercache-actual.png"));
                expected.Save(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rendercache-expected.png"));
            }
            Check(different == 0, "recuadros no equivalen a reconstruccion completa: " + different + " pixels, " + overlay.Size);
        }
    }
    static void Tarjetas()
    {
        foreach (Size size in new Size[] { new Size(800, 600), new Size(1024, 768), new Size(1920, 1080), new Size(3440, 1440) })
        using (Control overlay = (Control)Static(B, "Buscador", size.Width, size.Height, 40))
        {
            B.GetField("todasEnMemoria", F).SetValue(null, false);
            ((IDictionary)B.GetField("enMemoria", F).GetValue(null)).Clear();
            object[] piece = { 4, Rectangle.Empty }; Call(overlay, "PiezaViva", piece); Rectangle bounds = (Rectangle)piece[1];
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb))
            {
                Check(Paint(overlay, bitmap, bounds, true) == null, "primera imagen debe ser completa"); Same(overlay, bitmap);
                Check(Paint(overlay, bitmap, bounds, false).Length == 0, "datos intactos repintan");
                IList list = (IList)Get(overlay, "Results"); object album = Get(list[1], "AlbumRef"); string path = (string)Get(album, "CoverPath");
                ((IDictionary)B.GetField("enMemoria", F).GetValue(null))[path] = B.GetField("portadaComun", F).GetValue(null);
                Rectangle[] regions = Paint(overlay, bitmap, bounds, false); Same(overlay, bitmap);
                long bytes = 0; foreach (Rectangle r in regions) bytes += r.Width * (long)r.Height * 4;
                Check(regions.Length == 1 && bytes < bitmap.Width * (long)bitmap.Height * 2, "una portada sube demasiada superficie");
                Console.WriteLine("  OK portada " + size + ": " + bytes + " bytes, entera " + bitmap.Width * (long)bitmap.Height * 4);
                Set(list[1], "Display", "titulo con cola 7"); regions = Paint(overlay, bitmap, bounds, false); Same(overlay, bitmap);
                Check(regions.Length == 1, "marca de cola no actualiza su tarjeta");
                Set(list[2], "Display", "titulo SONANDO 5"); Paint(overlay, bitmap, bounds, false); Same(overlay, bitmap);
                object track = Get(list[2], "TrackRef"); Set(track, "Title", "Un titulo nuevo y completo"); Paint(overlay, bitmap, bounds, false); Same(overlay, bitmap);
                Set(overlay, "Accent", Color.CornflowerBlue); Check(Paint(overlay, bitmap, bounds, false) == null, "otro color requiere imagen completa"); Same(overlay, bitmap);
                Set(overlay, "Query", "otra consulta"); Check(Paint(overlay, bitmap, bounds, false) == null, "otra consulta usa regiones viejas"); Same(overlay, bitmap);
                Check(Paint(overlay, bitmap, bounds, true) == null, "textura/buffer nuevo debe reconstruirse completo");
            }
        }
    }
    static void Subidas()
    {
        using (Control panel = (Control)Static(H, "Panel", 1024, 768)) using (Bitmap bitmap = new Bitmap(300, 120, PixelFormat.Format32bppPArgb))
        {
            Set(panel, "GetPiezaCambios", new Func<int, int, Rectangle[]>(Dirty));
            Locks.Clear(); Call(panel, "SubePieza", 4, bitmap); Check(Locks.Count == 1, "textura nueva no sube completa");
            IntPtr tex = ((IntPtr[])Get(panel, "piezaTex"))[4]; Static(H, "PorTrozos", tex);
            ((int[])Get(panel, "piezaVer"))[4] = 1; changes = new Rectangle[] { new Rectangle(20, 10, 70, 40) };
            using (Graphics g = Graphics.FromImage(bitmap)) g.FillRectangle(Brushes.Red, changes[0]);
            Locks.Clear(); Call(panel, "SubePieza", 4, bitmap); Check(Locks.Count == 1 && (Rectangle)Get(Locks[0], "Zona") == changes[0], "no sube solo el recuadro");
            Check((int)Static(H, "DistintosEnTextura", tex, bitmap) == 0, "subida regional altera pixels");
            changes = new Rectangle[0]; Locks.Clear(); Call(panel, "SubePieza", 4, bitmap); Check(Locks.Count == 0, "actualizacion vacia toca tarjeta");
            Marshal.WriteIntPtr(tex, (IntPtr)H.GetField("vtTex", F).GetValue(null));
            ((int[])Get(panel, "piezaVer"))[4] = 0; Locks.Clear(); Call(panel, "SubePieza", 4, bitmap);
            Check(Locks.Count == 1 && ((Rectangle)Get(Locks[0], "Zona")).IsEmpty, "version antigua necesita subida completa");
            ((IntPtr[])Get(panel, "piezaTex"))[4] = IntPtr.Zero; Locks.Clear(); Call(panel, "SubePieza", 4, bitmap);
            Check(Locks.Count == 1, "reinicio no sube completa");
        }
    }
    // En Linux (Mono) la rocola copia memoria con RtlMoveMemory, de Windows: se carga la COPIA con su .config que
    // prepara franjareloj (Cargable), igual que franjareloj, buscaportadas y dibujoligero. Sin ella ninguna subida
    // llegaba a la textura y cada pasada lo volvia a subir todo. La carpeta de la copia se borra al acabar.
    [STAThread] static int Main(string[] args)
    {
        try { return Prueba((string)Static(H, "Cargable", args.Length > 0 ? args[0] : "rocola.exe")); }
        finally
        {
            string copia = (string)H.GetField("carpetaCopia", F).GetValue(null);
            if (copia != null) try { System.IO.Directory.Delete(copia, true); } catch { }
        }
    }
    static int Prueba(string ruta)
    {
        app = Assembly.LoadFrom(ruta);
        foreach (Type helper in new Type[] { H, B })
            foreach (string field in new string[] { "TPanel", "TStar", "TDev", "TD3D" })
                Init(helper, field, field == "TPanel" ? "Rocola.GpuPanel" : field == "TStar" ? "Rocola.GpuStarfield" : field == "TDev" ? "Rocola.D3D9Device" : "Rocola.D3D9");
        Init(B, "TF", "Rocola.Form1"); Type form = app.GetType("Rocola.Form1");
        foreach (string field in new string[] { "TSO", "TItem", "TAlb", "TTrack", "TKey" })
            B.GetField(field, F).SetValue(null, form.GetNestedType(field == "TSO" ? "SearchOverlay" : field == "TItem" ? "SearchItem" : field == "TAlb" ? "Album" : field == "TTrack" ? "Track" : "SearchKey", F));
        using (Bitmap cover = new Bitmap(300, 300))
        {
            using (Graphics g = Graphics.FromImage(cover)) g.Clear(Color.Orange);
            B.GetField("portadaComun", F).SetValue(null, cover);
            Cintas(); Tarjetas(); Subidas();
        }
        Console.WriteLine("OK " + checks + " verificaciones de cache, pixeles y subidas imitadas; no FPS reales."); return 0;
    }
}

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

// /main:CintasLigeras con performance.cs y leercodigo.cs. Controlador artificial.
// Segundo argumento: medir (microbenchmark) o huella (tambien en la version anterior).
class CintasLigeras
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type helper = typeof(Performance), panel, groupType;
    static Array groups;
    static int checks;
    static bool reference;
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Run(object o, string n, params object[] a) { return Call(o.GetType(), o, n, a); }
    static object Help(string n, params object[] a) { return Call(helper, null, n, a); }
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static int Count(object o, string n)
    { FieldInfo f = o.GetType().GetField(n, B); return f == null ? -1 : (int)f.GetValue(o); }
    static int Textures { get { return ((IDictionary)helper.GetField("texturas", B).GetValue(null)).Count; } }
    static int Released { get { return (int)helper.GetField("soltadas", B).GetValue(null); } }
    static IList List(object o, string n) { return (IList)Get(o, n); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static T[] Groups<T>() { return (T[])(object)groups; }
    static void Bind(Control p)
    {
        FieldInfo f = panel.GetField("GetCintasTexto", B);
        f.SetValue(p, Delegate.CreateDelegate(f.FieldType, typeof(CintasLigeras).GetMethod("Groups", B).MakeGenericMethod(groupType)));
    }
    static object Group() { return Activator.CreateInstance(groupType, true); }
    static void Fill(object group, int[] order, Color color, float x, bool longText)
    {
        using (Bitmap b = new Bitmap(1920, 1080)) using (Graphics g = Graphics.FromImage(b))
        using (Font f = new Font("Tahoma", 18, FontStyle.Bold, GraphicsUnit.Pixel)) using (StringFormat sf = new StringFormat())
        {
            Run(group, "Begin");
            for (int j = 0; j < order.Length; j++)
                Check((bool)Run(group, "Add", g, "Cancion " + order[j] + " " + new string('W', longText ? 170 : 45), f, color,
                    new RectangleF(x, 5 + j * 18, 160, 28), sf), "captura titulo completo " + j);
            Run(group, "End");
        }
    }
    static void Ready(Control p)
    {
        for (int i = 0; i < 100; i++)
        {
            Run(p, "PreparaCintasTexto"); bool all = true;
            foreach (object tile in List(p, "cintaTiles")) if ((IntPtr)Get(tile, "Tex") == IntPtr.Zero) all = false;
            if (all) return;
        }
        throw new Exception("No acaba de cargar las cintas visibles/proximas");
    }
    static void Mapping(Control p)
    {
        IList tiles = List(p, "cintaTiles"); int index = 0;
        if (groups != null) for (int i = 0; i < groups.Length; i++)
        {
            object g = groups.GetValue(i); if (g == null) continue;
            foreach (object run in List(g, "Items"))
            {
                Bitmap[] maps = (Bitmap[])Get(run, "Tiles");
                for (int j = 0; j < maps.Length; j++)
                {
                    object tile = tiles[index++];
                    Check(ReferenceEquals(Get(tile, "Source"), maps[j]) && ReferenceEquals(Get(tile, "Run"), run)
                        && (int)Get(tile, "Group") == i && (int)Get(tile, "Part") == j, "orden, fuente y propietario de cada parte");
                }
            }
        }
        Check(index == tiles.Count && ((IDictionary)Get(p, "cintaByBitmap")).Count == index, "sin partes huerfanas");
    }
    // Huella independiente: vertices, estados D3D y TODOS los pixeles de las texturas enviadas.
    // Born en la pausa inicial elimina diferencias de reloj entre procesos, no modifica el dibujo.
    static string Fingerprint(Control p, object dev, string label)
    {
        foreach (object tile in List(p, "cintaTiles")) Set(Get(tile, "Run"), "Born", Environment.TickCount - 100);
        Help("Captura"); Run(dev, "BeginFrame"); Run(p, "DibujaCintasTexto", false); Run(p, "DibujaCintasTexto", true); Run(dev, "EndFrame");
        IList draws = (IList)Help("Captura");
        using (MemoryStream s = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(s))
        {
            w.Write(draws.Count);
            foreach (object draw in draws)
            {
                w.Write((int)Get(draw, "Tris")); w.Write((int)Get(draw, "Paso")); w.Write((byte[])Get(draw, "Datos"));
                object state = Get(draw, "S"); w.Write((bool)Get(state, "Mezcla"));
                foreach (string n in new string[] { "Origen", "Destino", "Fvf" }) w.Write((uint)Get(state, n));
                foreach (object stage in (Array)Get(state, "E"))
                {
                    foreach (string n in new string[] { "Op", "A1", "A2", "AOp", "AA1", "AA2", "Indice", "Dir" }) w.Write((uint)Get(stage, n));
                    object texture = Help("Lee", (IntPtr)Get(stage, "Tex")); w.Write(texture != null);
                    if (texture != null)
                    { w.Write((int)Get(texture, "W")); w.Write((int)Get(texture, "H")); foreach (int px in (int[])Get(texture, "Px")) w.Write(px); }
                }
            }
            w.Flush(); using (SHA256 sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(s.ToArray())).Replace("-", "");
                Console.WriteLine("HUELLA " + label + " envios=" + draws.Count + " " + hash); return hash;
            }
        }
    }
    static int FailTexture(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared)
    { tex = IntPtr.Zero; return unchecked((int)0x8007000E); }
    static void Regression(int width, int height)
    {
        object dev = Help("Tarjeta", width, height, false, true, true), sf = Help("Cielo", dev, width, height);
        groups = Array.CreateInstance(groupType, 5); object g = Group(); groups.SetValue(g, 1);
        using (Control p = (Control)Help("Panel", width, height, sf))
        {
            Bind(p); Set(p, "uiBotH", 96); Fill(g, new int[] { 0, 1, 2, 3 }, Color.White, 12, true);
            int created = Textures; Run(p, "PreparaCintasTexto"); Mapping(p);
            Check(Textures == created, "barra oculta no sube textos");
            Set(p, "idleBlend", .5f); Set(p, "IdleExpandTarget", true); Run(p, "PreparaCintasTexto");
            foreach (object tile in List(p, "cintaTiles")) if ((bool)Run(p, "CintaVisible", tile, Environment.TickCount))
                Check((IntPtr)Get(tile, "Tex") != IntPtr.Zero, "lo visible se sube sin esperar al presupuesto");
            Ready(p); Mapping(p); int made = Count(p, "CintasListasCalculadas"), reused = Count(p, "CintasListasReusadas"); created = Textures;
            for (int i = 0; i < 60; i++) Run(p, "PreparaCintasTexto");
            Check(Textures == created, "no vuelve a subir texturas listas");
            if (!reference) Check(Count(p, "CintasListasCalculadas") == made && Count(p, "CintasListasReusadas") == reused + 60
                && List(p, "cintaPendientes").Count == 0, "sesenta cuadros sin reconstruir ni revisar cargas terminadas");
            Fingerprint(p, dev, width + "-estable");
            made = Count(p, "CintasListasCalculadas"); Set(p, "idleBlend", 0f); Set(p, "IdleExpandTarget", false);
            Run(p, "PreparaCintasTexto"); Set(p, "idleBlend", .5f); Set(p, "IdleExpandTarget", true); Ready(p);
            Check(Textures == created, "ocultar y reabrir conserva las texturas cargadas");
            if (!reference) Check(Count(p, "CintasListasCalculadas") == made, "el fundido no reconstruye la lista");
            object first = List(g, "Items")[0]; int born = (int)Get(first, "Born");
            Fill(g, new int[] { 0, 1, 2, 3 }, Color.White, 39, true); Ready(p); Mapping(p);
            Check(ReferenceEquals(first, List(g, "Items")[0]) && (int)Get(first, "Born") == born, "mover conserva objeto y reloj");
            if (!reference) Check(Count(p, "CintasListasCalculadas") == made, "mover sin rehacer la lista");
            Fingerprint(p, dev, width + "-movido");
            Fill(g, new int[] { 2, 0, 1, 3 }, Color.White, 39, true); Ready(p); Mapping(p);
            Check(Textures == created && ReferenceEquals(first, List(g, "Items")[1]), "reordenar conserva las texturas existentes");
            Fingerprint(p, dev, width + "-reordenado");
            born = (int)Get(List(g, "Items")[0], "Born"); int released = Released;
            Fill(g, new int[] { 2, 0, 1, 3 }, Color.Cyan, 39, true);
            Check((int)Get(List(g, "Items")[0], "Born") == born, "cambiar color conserva el desplazamiento");
            // Una falta temporal de memoria no debe borrar la cola de reintentos.
            FieldInfo create = dev.GetType().GetField("createTex", B); object original = create.GetValue(dev);
            create.SetValue(dev, Delegate.CreateDelegate(create.FieldType, typeof(CintasLigeras).GetMethod("FailTexture", B)));
            try { Run(p, "PreparaCintasTexto"); }
            finally { create.SetValue(dev, original); }
            foreach (object tile in List(p, "cintaTiles")) Check((IntPtr)Get(tile, "Tex") == IntPtr.Zero, "simula subida fallida");
            // Tras un "sin memoria" la tarjeta espera 200 ms antes de volver a pedir una textura de ese tamano (D3D9Device.CreaTexturaManejada, 20-10-2026):
            // la falta de memoria de este ejemplo es temporal y se acaba aqui.
            System.Threading.Thread.Sleep(260);
            Ready(p); Mapping(p); Check(Released > released, "libera las texturas del color anterior");
            Fingerprint(p, dev, width + "-color-reintentado");
            // La coleccion registra tambien sustituciones directas, aunque no cambie Count.
            IList items = List(g, "Items"); object swapped = items[0]; items[0] = items[1]; items[1] = swapped;
            Ready(p); Mapping(p); Fingerprint(p, dev, width + "-mismo-numero");
            Fill(g, new int[] { 99, 1 }, Color.Yellow, 12, false); Ready(p); Mapping(p);
            string beforeReset = Fingerprint(p, dev, width + "-titulos-nuevos");
            released = Released; int parts = List(p, "cintaTiles").Count;
            Run(p, "SueltaCintasGpu"); Check(Released == released + parts, "reset libera todas las texturas activas una vez");
            Ready(p); Mapping(p); Check(Fingerprint(p, dev, width + "-reset") == beforeReset, "reset recupera el mismo dibujo");
            made = Count(p, "CintasListasCalculadas"); groups = (Array)groups.Clone(); Run(p, "PreparaCintasTexto");
            if (!reference) Check(Count(p, "CintasListasCalculadas") == made, "otro contenedor con los mismos grupos se reutiliza");
            // Grupos de navegacion: retirar o mover la pagina actualiza el propietario.
            groups.SetValue(g, 2); groups.SetValue(null, 1);
            Set(p, "GlassPieces", true);
            Set(p, "GetPiezaRect", new Func<int, Rectangle>(delegate(int part) { return part == 2 ? Rectangle.Empty : new Rectangle(0, 0, width, height); }));
            Ready(p); Mapping(p); Fingerprint(p, dev, width + "-lista");
            groups.SetValue(null, 2); groups.SetValue(g, 4); Set(p, "GlassPieces", false);
            Ready(p); Mapping(p); Fingerprint(p, dev, width + "-vista-previa");
            groups.SetValue(null, 4); groups.SetValue(g, 2); Set(p, "GlassPieces", true);
            Array shortGroups = Array.CreateInstance(groupType, 3); shortGroups.SetValue(g, 2); groups = shortGroups;
            Run(p, "PreparaCintasTexto"); Mapping(p);
            groups.SetValue(Group(), 2); Run(p, "PreparaCintasTexto"); Mapping(p);
            Check(List(p, "cintaTiles").Count == 0, "grupo nuevo vacio retira los textos anteriores");
            ((IDisposable)groups.GetValue(2)).Dispose(); groups.SetValue(g, 2); Ready(p);
            ((IDisposable)g).Dispose(); Run(p, "PreparaCintasTexto"); Mapping(p);
            Check(List(p, "cintaTiles").Count == 0, "desactivar textos libera todas sus partes");
            groups = Array.CreateInstance(groupType, 5); groups.SetValue(g, 1);
            Fill(g, new int[] { 7, 8 }, Color.White, 12, true); Set(p, "idleBlend", 0f); Set(p, "IdleExpandTarget", false);
            created = Textures; Run(p, "PreparaCintasTexto"); ((IDisposable)g).Dispose(); Run(p, "PreparaCintasTexto");
            Check(Textures == created && List(p, "cintaTiles").Count == 0, "retirar antes de cargar no intenta subir bitmaps liberados");
            if (!reference) Check(List(p, "cintaPendientes").Count == 0, "retirada vacia tambien la cola pendiente");
            groups = null; Run(p, "PreparaCintasTexto"); Mapping(p); Run(p, "SueltaCintasGpu");
        }
    }
    static string N(double x) { return x.ToString("F3", CultureInfo.InvariantCulture); }
    static void Benchmark()
    {
        Console.WriteLine("Microbenchmark Linux/Mono, cola GPU ya cargada; sin dibujo, audio ni FPS. Cada muestra promedia 1000 preparaciones.");
        foreach (int count in new int[] { 0, 12, 48 })
        {
            object dev = Help("Tarjeta", 1024, 768, false, true, true), sf = Help("Cielo", dev, 1024, 768);
            groups = Array.CreateInstance(groupType, 5); object g = Group(); groups.SetValue(g, 1);
            using (Control p = (Control)Help("Panel", 1024, 768, sf))
            {
                Bind(p); Set(p, "idleBlend", 1f); Set(p, "IdleExpandTarget", true);
                int[] order = new int[count]; for (int i = 0; i < count; i++) order[i] = i;
                Fill(g, order, Color.White, 12, false); Ready(p);
                Action prepare = (Action)Delegate.CreateDelegate(typeof(Action), p, panel.GetMethod("PreparaCintasTexto", B));
                for (int i = 0; i < 200; i++) prepare();
                int made = Count(p, "CintasListasCalculadas"), reused = Count(p, "CintasListasReusadas");
                double[] batches = new double[31];
                for (int b = 0; b < batches.Length; b++)
                {
                    long start = Stopwatch.GetTimestamp(); for (int i = 0; i < 1000; i++) prepare();
                    batches[b] = (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency / 1000;
                }
                Array.Sort(batches);
                Console.WriteLine("titulos=" + count + " partes=" + List(p, "cintaTiles").Count + " operaciones=31000 mediana_us=" + N(batches[15])
                    + " p95_lote_us=" + N(batches[29]) + " max_lote_us=" + N(batches[30])
                    + " listas_calculadas=" + (made < 0 ? -1 : Count(p, "CintasListasCalculadas") - made)
                    + " listas_reusadas=" + (reused < 0 ? -1 : Count(p, "CintasListasReusadas") - reused));
            }
            ((IDisposable)g).Dispose(); groups = null;
        }
    }
    [STAThread] static int Main(string[] args)
    {
        Assembly app = Assembly.LoadFrom(args[0]); panel = app.GetType("Rocola.GpuPanel"); groupType = app.GetType("Rocola.CintasTexto");
        foreach (string n in new string[] { "TPanel", "TStar", "TDev", "TD3D", "TVtx", "TVtx2", "TForm" })
        {
            string name = n == "TPanel" ? "GpuPanel" : n == "TStar" ? "GpuStarfield" : n == "TDev" ? "D3D9Device" : n == "TForm" ? "Form1" : "D3D9";
            Type t = app.GetType("Rocola." + name); if (n == "TVtx" || n == "TVtx2") t = t.GetNestedType(n == "TVtx" ? "Vtx" : "Vtx2", B);
            helper.GetField(n, B).SetValue(null, t);
        }
        try
        {
            reference = args.Length > 1 && args[1] == "huella";
            if (args.Length > 1 && args[1] == "medir") Benchmark();
            else foreach (int[] wh in new int[][] { new int[] { 1024, 768 }, new int[] { 1280, 1024 }, new int[] { 1920, 1080 } }) Regression(wh[0], wh[1]);
            Console.WriteLine("OK " + checks + " comprobaciones; sin GPU real ni sesion musical."); return 0;
        }
        catch (Exception e) { Console.WriteLine("FALLO " + e); return 1; }
    }
}

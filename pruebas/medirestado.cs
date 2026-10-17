using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// Comparacion sintetica: controlador artificial, sin musica, sin esperas de monitor.
// Compilar con franjareloj.cs y leercodigo.cs /main:MedirEstado. Primer argumento:
// ejecutable exacto. Segundo: original, performance, nuevos o halo. Tercero/cuarto: ancho/alto.
// Quinto opcional: animado (avanza fases de nebulosas 24 de cada 60 cuadros simulados).
// Los intervalos son Start->Start de estas llamadas, NO FPS reales del equipo XP.
class MedirEstado
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type helper = typeof(FranjaReloj), panel, form;
    static Bitmap current, cell;
    static int frame, width = 1024, height = 768;
    static bool animated, halo;
    static object Call(Type t, object o, string n, params object[] a) { return t.GetMethod(n, B).Invoke(o, a); }
    static object Run(object o, string n, params object[] a) { return Call(o.GetType(), o, n, a); }
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object value) { o.GetType().GetField(n, B).SetValue(o, value); }
    static void SetIf(object o, string n, object value)
    { FieldInfo f = o.GetType().GetField(n, B); if (f != null) f.SetValue(o, value); }
    static int Counter(object o, string n)
    { FieldInfo f = o.GetType().GetField(n, B); return f == null ? 0 : (int)f.GetValue(o); }
    static void Background(object p, int i)
    {
        if (!animated) return;
        float phase = (i * 24 / 60) * .005f; Set(p, "NebFase", phase); Set(p, "HazFase", phase * .7f);
    }
    static Bitmap Idle() { return current; }
    static class GridData<T> { internal static T[] Rows; }
    static int Grid<T>(T[] data)
    {
        if (GridData<T>.Rows != null) { Array.Copy(GridData<T>.Rows, data, 18); return 18; }
        for (int i = 0; i < 18; i++)
        {
            object c = Activator.CreateInstance(typeof(T));
            Set(c, "Key", "disco" + i); Set(c, "Bmp", cell); Set(c, "Slot", i);
            Set(c, "X", (float)(12 + (i % 6) * (width - 24) / 6)); Set(c, "Y", (float)(100 + (i / 6) * (height - 300) / 3));
            Set(c, "W", (float)((width - 24) / 6 - 10)); Set(c, "H", (float)((height - 300) / 3 - 10)); Set(c, "A", 1f);
            data[i] = (T)c;
        }
        GridData<T>.Rows = (T[])data.Clone(); return 18;
    }
    static readonly float[] bands = { .7f, .65f, .5f, .4f, .5f, .3f, .2f, .1f };
    static T Aurora<T>()
    {
        object wave = Activator.CreateInstance(typeof(T));
        Set(wave, "Has", true); Set(wave, "B", bands); Set(wave, "Alto", 140f); Set(wave, "BaseY", (float)(height - 96));
        Set(wave, "Dim", .5f); Set(wave, "DimFilo", 1f); Set(wave, "Calma", .4f);
        return (T)wave;
    }
    static Delegate Generic(FieldInfo field, string method)
    {
        Type type = field.FieldType.GetMethod("Invoke").ReturnType;
        if (method == "Grid") type = field.FieldType.GetMethod("Invoke").GetParameters()[0].ParameterType.GetElementType();
        return Delegate.CreateDelegate(field.FieldType, typeof(MedirEstado).GetMethod(method, B).MakeGenericMethod(type));
    }
    static Bitmap Draw(bool opaque, bool changed)
    {
        int n = 12; string[] t = new string[n], a = new string[n]; Image[] covers = new Image[n];
        for (int i = 0; i < n; i++) { t[i] = "Cancion " + i; a[i] = "Artista"; covers[i] = cell; }
        MethodInfo reuse = form.GetMethod("RenderIdlePanelEn", B);
        object[] args = { width, (int)Call(panel, null, "IdlePanelHeight", height), changed ? "Otra cancion" : "Cancion actual", "Artista", "1:00 / 3:00", t, a, covers, n, Color.Teal, false, 0, true, null, 0 };
        if (reuse == null) return (Bitmap)form.GetMethod("RenderIdlePanelGrande", B).Invoke(null, args);
        object[] newer = new object[args.Length + 2]; newer[0] = current;
        Array.Copy(args, 0, newer, 1, args.Length - 1); newer[newer.Length - 2] = opaque; newer[newer.Length - 1] = 0;
        return (Bitmap)reuse.Invoke(null, newer);
    }
    static double Percentile(double[] values, double q)
    { return values[Math.Max(0, Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * q) - 1))]; }
    static void Scenario(bool expanded, bool threeD, bool options, bool newer)
    {
        using (Control p = (Control)Call(helper, null, "Panel", width, height))
        {
            Set(p, "ConLuna", true); Set(p, "LunaActiva", true); Set(p, "Estrella3D", threeD);
            Set(p, "Dim", 1f); Set(p, "ShowAlbum", !expanded); Set(p, "IdleExpandTarget", expanded);
            Set(p, "uiBotH", 96); Set(p, "idleBlend", expanded ? 1f : 0f); Set(p, "IdlePanelVersion", 1);
            p.GetType().GetField("GetIdlePanel", B).SetValue(p, new Func<Bitmap>(Idle));
            FieldInfo grid = panel.GetField("GetGrid", B); grid.SetValue(p, Generic(grid, "Grid"));
            FieldInfo wave = panel.GetField("GetAurora", B); wave.SetValue(p, Generic(wave, "Aurora"));
            if (options) foreach (string n in new string[] { "EstadoOpaco", "AuroraLigera", "ReposoSinReflejo", "ReposoSinEstela", "CoronaCompacta" }) SetIf(p, n, true);
            if (newer) { SetIf(p, "SinGrano", true); SetIf(p, "AuroraSinSuelo", true); }
            SetIf(p, "AuroraHaloCompacto", halo);
            current = null; current = Draw(options, false);
            for (int i = 0; i < 120; i++) { Background(p, i); Run(p, "RenderFrame"); }
            int creations = (int)helper.GetField("creaciones", B).GetValue(null), accepted = 0, maps = 0;
            int computed = Counter(p, "RecortesCalculados"), reused = Counter(p, "RecortesReusados");
            long omitted = 0;
            object stars = Get(p, "stars");
            int skies = Counter(stars, "CielosSuavesCalculados"), skyReuse = Counter(stars, "CielosSuavesReusados");
            int reflections = Counter(p, "ReflejosCalculados"), reflectionReuse = Counter(p, "ReflejosReusados");
            double[] intervals = new double[360]; long previous = Stopwatch.GetTimestamp();
            for (frame = 0; frame <= intervals.Length; frame++)
            {
                long start = Stopwatch.GetTimestamp();
                Background(p, frame + 120);
                if (expanded && frame % 30 == 0)
                {
                    Bitmap old = current; current = Draw(options, (frame / 30) % 2 == 0);
                    if (!ReferenceEquals(current, old)) { maps++; old.Dispose(); }
                    Set(p, "IdlePanelVersion", 2 + frame / 30); Set(p, "IdlePanelDirty", Rectangle.Empty);
                }
                if ((bool)Run(p, "RenderFrame")) accepted++;
                omitted += Counter(stars, "EstrellasOmitidas");
                if (frame > 0) intervals[frame - 1] = (start - previous) * 1000.0 / Stopwatch.Frequency; previous = start;
            }
            int more = (int)helper.GetField("creaciones", B).GetValue(null) - creations;
            Array.Sort(intervals);
            Console.WriteLine((expanded ? "estado" : "rejilla_estatica") + " estrella=" + (threeD ? "3D" : "2D") + " opciones=" + options + " nuevas=" + newer + " halo=" + halo
                + " cuadros_aceptados=" + accepted + " fallidos=" + (intervals.Length + 1 - accepted) + " nuevas_texturas=" + more + " nuevos_mapas=" + maps
                + " recortes_calculados=" + (Counter(p, "RecortesCalculados") - computed)
                + " recortes_reusados=" + (Counter(p, "RecortesReusados") - reused) + " estrellas_omitidas_total=" + omitted
                + " cielos_calculados=" + (Counter(stars, "CielosSuavesCalculados") - skies) + " cielos_reusados=" + (Counter(stars, "CielosSuavesReusados") - skyReuse)
                + " reflejos_calculados=" + (Counter(p, "ReflejosCalculados") - reflections) + " reflejos_reusados=" + (Counter(p, "ReflejosReusados") - reflectionReuse)
                + " p50=" + Percentile(intervals, .5).ToString("F3") + " p95=" + Percentile(intervals, .95).ToString("F3")
                + " p99=" + Percentile(intervals, .99).ToString("F3") + " max=" + Percentile(intervals, 1).ToString("F3") + " ms");
            current.Dispose(); current = null;
            if (accepted != intervals.Length + 1) throw new Exception("Cuadros artificiales rechazados");
        }
    }
    [STAThread] static int Main(string[] args)
    {
        if (args.Length >= 4) { width = int.Parse(args[2]); height = int.Parse(args[3]); }
        animated = args.Length >= 5 && args[4] == "animado";
        bool options = args.Length > 1 && args[1] != "original", newer = args.Length > 1 && (args[1] == "nuevos" || args[1] == "halo");
        halo = args.Length > 1 && args[1] == "halo";
        Console.WriteLine("RESOLUCION " + width + "x" + height + "; controlador artificial, sin audio ni monitor; fases_animadas=" + animated);
        Assembly app = Assembly.LoadFrom(args[0]); form = app.GetType("Rocola.Form1"); panel = app.GetType("Rocola.GpuPanel");
        Console.WriteLine("ensamblado=" + app.Location + " bitmap_reutilizable=" + (form.GetMethod("RenderIdlePanelEn", B) != null));
        foreach (string field in new string[] { "TF", "TPanel", "TStar", "TDev", "TD3D" })
        {
            string name = field == "TF" ? "Form1" : field == "TPanel" ? "GpuPanel" : field == "TStar" ? "GpuStarfield" : field == "TDev" ? "D3D9Device" : "D3D9";
            helper.GetField(field, B).SetValue(null, app.GetType("Rocola." + name));
        }
        cell = new Bitmap(156, 130, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(cell)) g.Clear(Color.DarkSlateBlue);
        try { foreach (bool expanded in new bool[] { false, true }) foreach (bool threeD in new bool[] { false, true }) Scenario(expanded, threeD, options, newer); }
        finally { cell.Dispose(); }
        return 0;
    }
}

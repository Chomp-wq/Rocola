using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// .NET 3.5; leercodigo.cs. Opcional: segundo argumento = ejecutable anterior,
// o "medir" para medir poses por delegado, sin GPU/audio/refresco de monitor.
class ColoresLigeros
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type panel, vertex, oldPanel, oldVertex;
    static int checks;
    static long comparedBytes;
    static int checksum;
    delegate void Tono(ref float r, ref float g, ref float b, float v);
    delegate void Ground(uint[] colors, int points, int column, uint color, float level);
    delegate int Pose<T>(T[] faces, int c0, T[] edges, int f0, float x, float y, float r,
        double yaw, double pitch, double roll, uint color, float alpha, bool echo, float width,
        out int edgeCount, out int best, out float shine);
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Run(object o, string n, params object[] a) { return Call(o.GetType(), o, n, a); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static byte[] Bytes(Array data, int n)
    {
        byte[] b = new byte[n * Marshal.SizeOf(data.GetType().GetElementType())];
        GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        try { Marshal.Copy(pin.AddrOfPinnedObject(), b, 0, b.Length); } finally { pin.Free(); }
        return b;
    }
    static void Same(Array a, Array b, int n, string label)
    {
        byte[] x = Bytes(a, n), y = Bytes(b, n);
        for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) throw new Exception(label + ": byte " + i + " anterior=" + x[i] + " actual=" + y[i]);
        comparedBytes += x.Length; checks++;
    }
    static void GroundColors()
    {
        Tono tone = (Tono)Delegate.CreateDelegate(typeof(Tono), (oldPanel ?? panel).GetMethod("TonoPorNivel", B));
        Func<float, float> level = (Func<float, float>)Delegate.CreateDelegate(typeof(Func<float, float>), (oldPanel ?? panel).GetMethod("AurSueloTono", B));
        Ground ground = (Ground)Delegate.CreateDelegate(typeof(Ground), panel.GetMethod("AurColoresSuelo", B));
        uint[] result = new uint[15]; Random random = new Random(46300);
        uint[] colors = new uint[20016];
        for (int i = 0; i < colors.Length; i++) colors[i] = i < 8 ? (uint)(i * 0x242424)
            : i < 16 ? new uint[] { 0u, 1u, 0xFF0000u, 0x00FF00u, 0x0000FFu, 0xFFFFFFu, 0xFEFFFFu, 0xFEFFFEu }[i - 8] : (uint)random.Next() ^ ((uint)random.Next() << 16);
        foreach (float intensity in new float[] { 0f, .0001f, .03f, .33f, .5f, .9999f, 1f, -1f, 3f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            foreach (uint color in colors)
            {
                for (int i = 0; i < result.Length; i++) result[i] = 0xDEADBEEFu;
                ground(result, 3, 1, color, intensity);
                for (int k = 0; k <= 4; k++)
                {
                    float r = (color >> 16) & 255, g = (color >> 8) & 255, b = color & 255;
                    tone(ref r, ref g, ref b, intensity * level(k / 4f));
                    uint expected = ((uint)(int)r << 16) | ((uint)(int)g << 8) | (uint)(int)b;
                    if (result[k * 3 + 1] != expected) throw new Exception("Suelo distinto color=" + color.ToString("X8") + " nivel=" + intensity + " fila=" + k);
                    if (result[k * 3] != 0xDEADBEEFu || result[k * 3 + 2] != 0xDEADBEEFu) throw new Exception("Escribe otra columna");
                }
            }
            Check(true, "cinco colores exactos y columnas vecinas intactas");
        }
        Check(Codigo.Llama(panel.GetMethod("DrawAurora", B), "AurColoresSuelo"), "DrawAurora utiliza el calculo compartido");
        Console.WriteLine("Suelo: " + colors.Length * 12 * 5 + " colores identicos; negros, grises, saturados y niveles extremos.");
    }
    static void Settings(object p, int mask)
    {
        string[] fields = { "EstrellaDegradado", "EstrellaRefleja", "EstrellaBorde", "EstrellaDestello", "LunaRealista" };
        for (int i = 0; i < fields.Length; i++) Set(p, fields[i], (mask & (1 << i)) != 0);
        Set(p, "SkyTop", 0xFF050C17u); Set(p, "SkyMid", 0xFF193755u); Set(p, "GeneroArgb", 0xFFAE35D8u);
    }
    static void Poses()
    {
        if (oldPanel == null) { Console.WriteLine("Comparacion de poses contra anterior omitida: falta segundo ejecutable."); return; }
        Random random = new Random(1046300);
        using (Control p = (Control)Activator.CreateInstance(panel, true))
        using (Control before = (Control)Activator.CreateInstance(oldPanel, true))
        {
            Array faces = Array.CreateInstance(vertex, 64), edges = Array.CreateInstance(vertex, 184);
            Array oldFaces = Array.CreateInstance(oldVertex, 64), oldEdges = Array.CreateInstance(oldVertex, 184);
            foreach (int[] size in new int[][] { new int[] { 1024, 768 }, new int[] { 1280, 1024 }, new int[] { 1920, 1080 } })
            {
                p.SetBounds(0, 0, size[0], size[1]); before.SetBounds(0, 0, size[0], size[1]);
                for (int mask = 0; mask < 32; mask++)
                {
                    Settings(p, mask); Settings(before, mask);
                    foreach (bool echo in new bool[] { false, true }) for (int i = 0; i < 64; i++)
                    {
                        uint color = i < 5 ? new uint[] { 0u, 0xFFFFFFFFu, 0xFF010101u, 0xFFFF0000u, 0xFF00FF00u }[i] : (uint)random.Next();
                        object[] a = { faces, 2, edges, 3, (float)(random.NextDouble() * size[0]), (float)(random.NextDouble() * size[1]),
                            (float)(10 + random.NextDouble() * 300), random.NextDouble() * 12 - 6, random.NextDouble() * 6 - 3, random.NextDouble() * 6 - 3,
                            color, i < 4 ? new float[] { 0f, .1f, 254.9f, 255f }[i] : (float)(random.NextDouble() * 255), echo, echo ? 1.8f : 1.1f, 0, 0, 0f };
                        object[] b = (object[])a.Clone(); b[0] = oldFaces; b[2] = oldEdges;
                        int n = (int)Run(p, "PoseEstrella", a), oldN = (int)Run(before, "PoseEstrella", b);
                        string label = "pose " + size[0] + " mask=" + mask + " eco=" + echo + " i=" + i;
                        Check(n == oldN && a[14].Equals(b[14]) && a[15].Equals(b[15]) && a[16].Equals(b[16]), label + " recuentos/destello");
                        Same(oldFaces, faces, n + 2, label + " caras"); Same(oldEdges, edges, (int)a[14] + 3, label + " bordes");
                        Check(Get(p, "e3brilloX").Equals(Get(before, "e3brilloX")) && Get(p, "e3brilloY").Equals(Get(before, "e3brilloY")), label + " posicion del brillo");
                    }
                }
                Console.WriteLine("Poses exactas " + size[0] + "x" + size[1] + ": 32 combinaciones de ajustes, 64 orientaciones, estrella y ecos.");
            }
        }
    }
    static void EchoSetting()
    {
        using (Control p = (Control)Activator.CreateInstance(panel, true))
        using (Control fresh = (Control)Activator.CreateInstance(panel, true))
        {
            Array ring = (Array)Get(p, "starEchoes"); object echo = Activator.CreateInstance(ring.GetType().GetElementType());
            Set(echo, "Time", 9.5); Set(echo, "X", 300f); Set(echo, "Y", 200f); Set(echo, "Radius", 75f);
            Set(echo, "Yaw", .7); Set(echo, "Pitch", .2f); Set(echo, "Angle", .1f); Set(echo, "Strength", 1f); Set(echo, "Color", 0xFF63B8F4u);
            ring.SetValue(echo, 0); Set(p, "starEchoHead", 1); Set(p, "starEchoCount", 1); Set(p, "starEchoVisibility", 1f);
            Array faces = Array.CreateInstance(vertex, 60), edges = Array.CreateInstance(vertex, 180);
            foreach (bool smooth in new bool[] { true, true, false, false, true, false, true })
            {
                Set(p, "EstrellaDegradado", smooth); Set(fresh, "EstrellaDegradado", smooth);
                object[] cached = { 10.0, 0, 0 }; Run(p, "PreparaEcos3d", cached);
                float alpha = (float)Call(panel, null, "StarEchoAlpha3d", .5, 1f) * 255f;
                object[] a = { faces, 0, edges, 0, 300f, 200f, 75f, .7, (double).2f, (double).1f, 0xFF63B8F4u, alpha, true, 1.8f, 0, 0, 0f };
                int n = (int)Run(fresh, "PoseEstrella", a);
                Check(n == (int)cached[1] && (int)a[14] == (int)cached[2], "ajuste cambia tambien la estela existente");
                Same(faces, (Array)Get(p, "e3ecos"), n, "eco con degradado=" + smooth);
                Same(edges, (Array)Get(p, "e3ecoFilo"), (int)a[14], "filo de eco con degradado=" + smooth);
            }
        }
    }
    static string N(double value) { return value.ToString("F3", CultureInfo.InvariantCulture); }
    static void Benchmark<T>()
    {
        Console.WriteLine("Linux/Mono; pose 3D completa por delegado. 31 lotes de 500 poses; promedios por lote, no latencias de cuadro ni FPS.");
        using (Control p = (Control)Activator.CreateInstance(panel, true))
        {
            Pose<T> pose = (Pose<T>)Delegate.CreateDelegate(typeof(Pose<T>), p, panel.GetMethod("PoseEstrella", B));
            T[] faces = new T[60], edges = new T[180];
            foreach (int mask in new int[] { 31, 24, 1 }) foreach (bool echo in new bool[] { false, true })
            {
                Settings(p, mask); double[] batches = new double[31]; int edge, best; float shine;
                for (int i = 0; i < 500; i++) pose(faces, 0, edges, 0, 512f, 340f, 80f, i * .013, .22, .1, 0xFF8CABD9u, 255f, echo, 1.1f, out edge, out best, out shine);
                for (int b = 0; b < batches.Length; b++)
                {
                    long start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < 500; i++) checksum += pose(faces, 0, edges, 0, 512f, 340f, 80f, i * .013, .22, .1, 0xFF8CABD9u, 255f, echo, 1.1f, out edge, out best, out shine);
                    batches[b] = (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency / 500;
                }
                Array.Sort(batches);
                Console.WriteLine("mask=" + mask + " eco=" + echo + " poses=15500 mediana_us=" + N(batches[15]) + " p95_lote_us=" + N(batches[29]) + " max_lote_us=" + N(batches[30]));
            }
        }
        Console.WriteLine("checksum=" + checksum);
    }
    [STAThread] static int Main(string[] args)
    {
        Assembly app = Assembly.LoadFrom(args[0]); panel = app.GetType("Rocola.GpuPanel"); vertex = app.GetType("Rocola.D3D9+Vtx");
        try
        {
            if (args.Length > 1 && args[1] == "medir")
            { typeof(ColoresLigeros).GetMethod("Benchmark", B).MakeGenericMethod(vertex).Invoke(null, null); return 0; }
            if (args.Length > 1)
            {
                Assembly old = Assembly.LoadFile(System.IO.Path.GetFullPath(args[1]));
                Check(old != app, "referencia independiente"); oldPanel = old.GetType("Rocola.GpuPanel"); oldVertex = old.GetType("Rocola.D3D9+Vtx");
            }
            GroundColors(); Poses(); EchoSetting();
            Console.WriteLine("OK " + checks + " comprobaciones, " + comparedBytes + " bytes comparados; sin GPU real ni audio."); return 0;
        }
        catch (Exception e) { Console.WriteLine("FALLO " + e); return 1; }
    }
}

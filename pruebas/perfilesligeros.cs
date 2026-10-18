using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

// .NET 3.5, performance.cs y leercodigo.cs; /main:PerfilesLigeros.
// Segundo argumento opcional: ejecutable anterior, "medir" o "huella".
class PerfilesLigeros
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type panel, reference, helper = typeof(Performance);
    static long checks;
    static float checksum;
    delegate float Formula(float[] bands, float x);
    delegate float Column(float[] bands, int column, bool defined);
    [StructLayout(LayoutKind.Explicit)] struct Bits
    { [FieldOffset(0)] internal float Value; [FieldOffset(0)] internal int Raw; }
    static int Raw(float value) { Bits b = new Bits(); b.Value = value; return b.Raw; }
    static object Get(object o, string n) { return o.GetType().GetField(n, B).GetValue(o); }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, B).SetValue(o, v); }
    static object Call(Type t, object o, string n, params object[] a)
    { try { return t.GetMethod(n, B).Invoke(o, a); } catch (TargetInvocationException e) { throw e.InnerException; } }
    static object Run(object o, string n, params object[] a) { return Call(o.GetType(), o, n, a); }
    static object Help(string n, params object[] a) { return Call(helper, null, n, a); }
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static Formula Original(Type t, bool defined)
    { return (Formula)Delegate.CreateDelegate(typeof(Formula), t.GetMethod(defined ? "PerfilDefinido" : "AurPerfil", B)); }
    static void Compare(float[] bands, int column, bool defined, Formula old, Column fast)
    {
        float expected = old(bands, column / 144f), actual = fast(bands, column, defined);
        if (Raw(expected) != Raw(actual)) throw new Exception("Perfil distinto columna=" + column + " definido=" + defined + " anterior=" + Raw(expected) + " nuevo=" + Raw(actual));
        checks++;
    }
    static void Exact()
    {
        Column fast = (Column)Delegate.CreateDelegate(typeof(Column), panel.GetMethod("AurPerfilColumna", B));
        Random random = new Random(4630018);
        float[] extreme = { 0f, -0f, 1f, .000001f, .5f, .99999f, -1f, 2f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.Epsilon, float.MaxValue };
        foreach (bool defined in new bool[] { false, true })
        {
            Formula old = Original(reference ?? panel, defined);
            float[] bands = new float[8];
            for (int sample = 0; sample < 10000; sample++)
            {
                for (int b = 0; b < 8; b++) bands[b] = sample < extreme.Length * 8
                    ? (sample / extreme.Length == b ? extreme[sample % extreme.Length] : 0f)
                    : sample < 1000 ? extreme[random.Next(extreme.Length)] : (float)random.NextDouble();
                float[] copy = (float[])bands.Clone();
                for (int c = 0; c <= 144; c++) Compare(bands, c, defined, old, fast);
                for (int b = 0; b < 8; b++) Check(Raw(bands[b]) == Raw(copy[b]), "No modifica las bandas");
            }
            foreach (int length in new int[] { -1, 0, 1, 2, 7, 8, 9, 16 })
            {
                float[] input = length < 0 ? null : new float[length];
                if (input != null) for (int b = 0; b < length; b++) input[b] = (float)random.NextDouble();
                for (int c = -16; c <= 160; c++) Compare(input, c, defined, old, fast);
            }
        }
        Check(Codigo.Llama(panel.GetMethod("DrawAurora", B), "AurPerfilColumna"), "DrawAurora usa los pesos precalculados");
        Console.WriteLine("PERFILES exactos: 2.900.000 resultados de ocho bandas, mas contratos generales y entradas intactas.");
        Console.WriteLine(reference == null ? "Referencia: formulas originales conservadas en el ejecutable." : "Referencia independiente: " + reference.Assembly.Location);
    }
    static string N(double v) { return v.ToString("F3", CultureInfo.InvariantCulture); }
    static void Measure()
    {
        MethodInfo method = panel.GetMethod("AurPerfilColumna", B);
        Column fast = method == null ? null : (Column)Delegate.CreateDelegate(typeof(Column), method);
        Console.WriteLine("Perfiles de 145 columnas; 31 lotes de 500 muestras, delegado sin reflexion en el tramo medido. Promedios por lote, no FPS.");
        float[][] samples = new float[64][]; Random random = new Random(4630018);
        for (int s = 0; s < samples.Length; s++) { samples[s] = new float[8]; for (int b = 0; b < 8; b++) samples[s][b] = (float)random.NextDouble(); }
        foreach (bool defined in new bool[] { false, true })
        {
            Formula old = Original(panel, defined); double[] times = new double[31];
            for (int batch = -3; batch < 31; batch++)
            {
                long start = Stopwatch.GetTimestamp(); float total = 0;
                if (fast == null) for (int s = 0; s < 500; s++) for (int c = 0; c <= 144; c++) total += old(samples[s % 64], c / 144f);
                else for (int s = 0; s < 500; s++) for (int c = 0; c <= 144; c++) total += fast(samples[s % 64], c, defined);
                double us = (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency / 500;
                checksum += total; if (batch >= 0) times[batch] = us;
            }
            Array.Sort(times);
            Console.WriteLine("definido=" + defined + " tabla=" + (fast != null) + " mediana_us=" + N(times[15]) + " p95_lote_us=" + N(times[29]) + " max_lote_us=" + N(times[30]));
        }
        Console.WriteLine("checksum=" + checksum.ToString("R", CultureInfo.InvariantCulture));
    }
    static void Fingerprints()
    {
        Assembly app = panel.Assembly;
        foreach (string n in new string[] { "TPanel", "TStar", "TDev", "TD3D", "TVtx", "TVtx2", "TForm" })
        {
            string type = n == "TPanel" ? "GpuPanel" : n == "TStar" ? "GpuStarfield" : n == "TDev" ? "D3D9Device" : n == "TForm" ? "Form1" : "D3D9";
            Type t = app.GetType("Rocola." + type); if (n == "TVtx" || n == "TVtx2") t = t.GetNestedType(n == "TVtx" ? "Vtx" : "Vtx2", B);
            helper.GetField(n, B).SetValue(null, t);
        }
        foreach (int[] size in new int[][] { new int[] { 1024, 768 }, new int[] { 1280, 1024 }, new int[] { 1920, 1080 } })
        {
            int w = size[0], h = size[1]; object dev = Help("Tarjeta", w, h, false, true, true), sky = Help("Cielo", dev, w, h);
            using (Control p = (Control)Help("Panel", w, h, sky))
            {
                object wave = Activator.CreateInstance(app.GetType("Rocola.GpuAurora")); float[] bands = new float[8];
                Set(wave, "Has", true); Set(wave, "B", bands); Set(wave, "Alto", h * .19f); Set(wave, "BaseY", h * .75f);
                Set(wave, "Dim", .6f); Set(wave, "DimFilo", .8f);
                Set(wave, "Rgb", 0x00336688u); Set(wave, "Rgb2", 0x00663399u); Set(wave, "RgbA", 0x0088CCFFu); Set(wave, "Rgb2A", 0x00CC88FFu);
                // Reutiliza el panel y cambia estilos/ahorros entre muestras: ninguna tabla debe quedarse en el estilo anterior.
                for (int variant = 0; variant < 32; variant++)
                {
                    Set(p, "AuroraBandas", (variant & 1) != 0); Set(p, "OndasSuaves", (variant & 2) != 0);
                    Set(p, "AuroraLigera", (variant & 4) != 0); Set(p, "AuroraSinSuelo", (variant & 8) != 0); Set(p, "AuroraHaloCompacto", (variant & 16) != 0);
                    Set(wave, "Calma", (variant % 3) * .4f);
                    for (int b = 0; b < 8; b++) bands[b] = (float)(.5 + .5 * Math.Sin(b * 1.1 + variant));
                    Set(Get(p, "waveCadence"), "rate", 0); Set(p, "aurRelojOk", false); Set(p, "aurT", variant * .35);
                    Help("Captura"); Run(dev, "BeginFrame"); Run(p, "DrawAurora", wave); Run(dev, "EndFrame");
                    IList draws = (IList)Help("Captura"); Check(draws.Count == 2, "Relleno y brillo presentes");
                    using (MemoryStream stream = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        foreach (object draw in draws)
                        {
                            writer.Write((int)Get(draw, "Tris")); writer.Write((int)Get(draw, "Paso")); writer.Write((byte[])Get(draw, "Datos"));
                            object state = Get(draw, "S"); writer.Write((bool)Get(state, "Mezcla"));
                            foreach (string n in new string[] { "Origen", "Destino", "Fvf" }) writer.Write((uint)Get(state, n));
                        }
                        writer.Flush(); using (SHA256 sha = SHA256.Create())
                            Console.WriteLine("HUELLA " + w + "x" + h + " variante=" + variant + " " + BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", ""));
                    }
                }
            }
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            panel = Assembly.LoadFrom(args[0]).GetType("Rocola.GpuPanel");
            if (args.Length > 1 && args[1] == "medir") { Measure(); return 0; }
            if (args.Length > 1 && args[1] == "huella") { Fingerprints(); return 0; }
            if (args.Length > 1) { reference = Assembly.LoadFile(Path.GetFullPath(args[1])).GetType("Rocola.GpuPanel"); Check(reference.Assembly != panel.Assembly, "Ensamblado de referencia independiente"); }
            Exact(); Fingerprints(); Console.WriteLine("OK " + checks + " comprobaciones; sin GPU real ni audio."); return 0;
        }
        catch (Exception e) { Console.WriteLine("FALLO " + e); return 1; }
    }
}

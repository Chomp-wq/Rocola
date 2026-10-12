using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Compara el lote completo contra la receta sin cache, incluido el alfa del filo.
// No arranca Direct3D, no abre musica y no es una medicion real de FPS.
class EcosGeometria
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    delegate void Preparador(double now, out int caras, out int bordes);
    static int checks;
    static long bytes;
    static Type gpu, vtx;
    static object Get(object x, string n) { return x.GetType().GetField(n, B).GetValue(x); }
    static void Set(object x, string n, object v) { x.GetType().GetField(n, B).SetValue(x, v); }
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception(text); }
    static byte[] Bits(Array a, int count)
    {
        byte[] data = new byte[count * Marshal.SizeOf(a.GetType().GetElementType())];
        GCHandle pin = GCHandle.Alloc(a, GCHandleType.Pinned);
        try { Marshal.Copy(pin.AddrOfPinnedObject(), data, 0, data.Length); }
        finally { pin.Free(); }
        return data;
    }
    static void Same(Array a, Array b, int count, string label)
    {
        byte[] x = Bits(a, count), y = Bits(b, count);
        Check(x.Length == y.Length, label + " longitud");
        for (int i = 0; i < x.Length; i++)
            if (x[i] != y[i]) throw new Exception(label + " byte distinto " + i);
        bytes += x.Length; checks++;
    }
    static void Echo(Array ring, int slot, double now, int seed)
    {
        object e = Activator.CreateInstance(ring.GetType().GetElementType());
        Set(e, "Time", now); Set(e, "X", 80f + seed * 73.25f); Set(e, "Y", 160f + seed * 19.875f);
        Set(e, "Radius", 12f + seed * 3.125f); Set(e, "Yaw", seed * .713); Set(e, "Pitch", (float)(-.3 + seed * .117));
        Set(e, "Angle", (float)(-.17 + seed * .073)); Set(e, "Strength", .13f + (seed % 8) * .11f);
        uint[] colors = { 0u, 0xFFFFFFu, 0x102030u, 0xFF007Fu, 0x1EF38Cu, 0x895BC2u, 0xFFBA10u };
        Set(e, "Color", 0xFF000000u | colors[seed % colors.Length]); ring.SetValue(e, slot);
    }
    static void Recipe(object panel, Array ring, int head, int count, double now, float visibility,
        Array faces, Array rim, out int n, out int f)
    {
        Type type = panel.GetType(); MethodInfo pose = type.GetMethod("PoseEstrella", B);
        MethodInfo fade = type.GetMethod("StarEchoAlpha3d", B);
        n = f = 0;
        for (int i = 0; i < count; i++)
        {
            object e = ring.GetValue((head - count + i + ring.Length) % ring.Length);
            double age = now - (double)Get(e, "Time");
            object[] fadeArgs = { age, visibility };
            float alpha = (float)fade.Invoke(null, fadeArgs) * (float)Get(e, "Strength");
            if (alpha < .003f) continue;
            object[] a = { faces, n, rim, f, Get(e,"X"), Get(e,"Y"), Get(e,"Radius"), Get(e,"Yaw"),
                (double)(float)Get(e,"Pitch"), (double)(float)Get(e,"Angle"), Get(e,"Color"), alpha * 255f,
                true, 1.8f, 0, 0, 0f };
            int caras = (int)pose.Invoke(panel, a), bordes = (int)a[14];
            n += caras; f += bordes;
        }
    }
    static void Compare(object panel, object reference, Array ring, int head, int count, double now, float visibility)
    {
        Set(panel, "starEchoHead", head); Set(panel, "starEchoCount", count);
        Set(panel, "starEchoVisibility", visibility);
        Type vertex = reference.GetType().Assembly.GetType("Rocola.D3D9+Vtx", true);
        Array faces = Array.CreateInstance(vertex, 720), rim = Array.CreateInstance(vertex, 2160);
        int en, ef; Recipe(reference, ring, head, count, now, visibility, faces, rim, out en, out ef);
        object[] args = { now, 0, 0 }; gpu.GetMethod("PreparaEcos3d", B).Invoke(panel, args);
        Check(en == (int)args[1] && ef == (int)args[2], "mismo numero y orden de vertices");
        Same(faces, (Array)Get(panel, "e3ecos"), en, "caras");
        Same(rim, (Array)Get(panel, "e3ecoFilo"), ef, "filo, extremos transparentes y redondeo");
    }
    static void Cases(object panel, object reference)
    {
        Array ring = (Array)Get(panel, "starEchoes");
        for (int i = 0; i < ring.Length; i++) Echo(ring, i, 10 - i * .037, i);
        foreach (bool detail in new bool[] { false, true })
        {
            Set(panel, "LunaRealista", detail); Set(reference, "LunaRealista", detail);
            foreach (int head in new int[] { 0, 1, 5, 11 })
            foreach (int count in new int[] { 0, 1, 4, 12 })
            foreach (float visibility in new float[] { 0, .004f, .25f, .7f, 1 })
            foreach (double time in new double[] { 9.99, 10, 10.003, 10.03, 10.059, 10.06, 10.5, 11.1999, 11.2, 11.8 })
                Compare(panel, reference, ring, head, count, time, visibility);
        }
        // Cada campo de geometria invalida su slot. Tiempo/fuerza solo cambian el alfa.
        foreach (string field in new string[] { "X", "Y", "Radius", "Yaw", "Pitch", "Angle", "Color", "Time", "Strength" })
        {
            object e = ring.GetValue(3), old = Get(e, field);
            if (old is float) Set(e, field, (float)old + .15f);
            else if (old is double) Set(e, field, (double)old + .1);
            else Set(e, field, (uint)old ^ 0x0057123Fu);
            ring.SetValue(e, 3);
            Compare(panel, reference, ring, 4, 1, 10.5, .85f);
        }
        // El anillo puede reutilizarse desde 2D, reiniciarse, agotarse y cambiar de tamano.
        for (int i = 0; i < 40; i++)
        {
            int slot = i % ring.Length; Echo(ring, slot, 20 + i * .04, i + 12);
            Compare(panel, reference, ring, (slot + 1) % ring.Length, Math.Min(12, i + 1), 20.05 + i * .04, 1);
        }
        gpu.GetMethod("ResetStarEchoes", B).Invoke(panel, null);
        Compare(panel, reference, ring, 0, 0, 50, 1);
        Echo(ring, 0, 50, 43); Compare(panel, reference, ring, 1, 1, 50.1, 1);
        foreach (string name in new string[] { "e3ecoPose", "e3ecoBorde", "e3ecoClave", "e3ecoValida", "e3ecoN", "e3ecoBordeN" })
        {
            object held = Get(panel, name);
            Compare(panel, reference, ring, 1, 1, 50.2, .75f);
            Check(ReferenceEquals(held, Get(panel, name)), "buffers reutilizados: " + name);
        }
    }
    static void Cost(object panel)
    {
        Array ring = (Array)Get(panel, "starEchoes");
        for (int i = 0; i < ring.Length; i++) Echo(ring, i, 100, i + 1);
        Set(panel, "starEchoHead", 0); Set(panel, "starEchoCount", 12); Set(panel, "starEchoVisibility", 1f);
        Preparador prepare = (Preparador)Delegate.CreateDelegate(typeof(Preparador), panel, gpu.GetMethod("PreparaEcos3d", B));
        int n, f; prepare(100.4, out n, out f);
        // Sin Invoke ni arreglos de argumentos por cuadro. Invalidar simula reconstruir la
        // geometria con la MISMA receta, mas el coste de copiar (no es el binario anterior).
        bool[] valid = (bool[])Get(panel, "e3ecoValida");
        for (int i = 0; i < 400; i++) prepare(100.4, out n, out f);
        const int frames = 12000;
        {
            prepare(100.4, out n, out f);
            double totalCached = 0, totalCold = 0;
            for (int run = 0; run < 3; run++)
            foreach (bool cold in new bool[] { (run & 1) != 0, (run & 1) == 0 })
            {
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < frames; i++)
                { if (cold) Array.Clear(valid, 0, valid.Length); prepare(100.4, out n, out f); }
                watch.Stop();
                if (cold) totalCold += watch.Elapsed.TotalMilliseconds; else totalCached += watch.Elapsed.TotalMilliseconds;
            }
            Console.WriteLine("COSTE_SINTETICO Legacy 12 ecos cache_us=" +
                (totalCached * 1000 / (frames * 3)).ToString("0.000") + " reconstruccion_mas_copia_us=" +
                (totalCold * 1000 / (frames * 3)).ToString("0.000") + "; no mide FPS ni tirones reales");
        }
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Assembly a = Assembly.LoadFrom(args[0]); gpu = a.GetType("Rocola.GpuPanel", true); vtx = a.GetType("Rocola.D3D9+Vtx", true);
            Check(Marshal.SizeOf(vtx) == 28, "layout de vertices completo");
            Assembly baseline = args.Length > 1 ? Assembly.LoadFile(System.IO.Path.GetFullPath(args[1])) : a;
            using (Control panel = (Control)Activator.CreateInstance(gpu, true))
            using (Control reference = (Control)Activator.CreateInstance(baseline.GetType("Rocola.GpuPanel", true), true))
            { Cases(panel, reference); Cost(panel); }
            Console.WriteLine("PASS ecosgeometria: " + checks + " comprobaciones, " + bytes + " bytes identicos de vertices; " +
                (args.Length > 1 ? "referencia binaria anterior" : "receta sin cache") + ". Sin HAL/musica real.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

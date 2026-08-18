using System;
using System.Reflection;
using NAudio.Wave;

class Fuente2 : ISampleProvider
{
    private readonly float[] d; private int p; private readonly WaveFormat wf;
    public Fuente2(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    { int n = Math.Min(c, d.Length - p); for (int i = 0; i < n; i++) b[o + i] = d[p + i]; p += n; return n; }
}

// ¿Que frecuencias SALEN del generador de bajo cuando entran DOS notas graves a la vez?
// Las buenas son multiplos exactos de cada nota (el cerebro las funde y "oye" el grave que la bocina no da).
// Las malas son sumas y restas entre las dos notas: no son multiplo de ninguna, y suenan como aspereza.
class Imd
{
    const int FS = 44100, CH = 2;
    static Type T;

    static double NivelDb(float[] x, double hz)
    {
        int n = x.Length / CH;
        double w = 2 * Math.PI * hz / FS, coeff = 2 * Math.Cos(w);
        double s0 = 0, s1 = 0, s2 = 0;
        for (int i = 0; i < n; i++) { s0 = x[i * CH] + coeff * s1 - s2; s2 = s1; s1 = s0; }
        double mag = Math.Sqrt(s1 * s1 + s2 * s2 - coeff * s1 * s2) / (n / 2.0);
        return 20 * Math.Log10(mag + 1e-12);
    }

    static float[] Procesar(float[] e, float amount)
    {
        object bp = Activator.CreateInstance(T, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.CreateInstance,
                                             null, new object[] { new Fuente2(e, FS, CH), amount }, null);
        MethodInfo read = T.GetMethod("Read");
        float[] b = new float[e.Length]; int tot = 0;
        while (tot < b.Length)
        {
            object[] a = { b, tot, Math.Min(4096, b.Length - tot) };
            int n = (int)read.Invoke(bp, a); if (n <= 0) break; tot += n;
        }
        return b;
    }

    static float[] Mezcla(double[] hz, double amp, double seg)
    {
        int n = (int)(seg * FS); float[] x = new float[n * CH];
        for (int i = 0; i < n; i++)
        {
            double v = 0; foreach (double f in hz) v += amp * Math.Sin(2 * Math.PI * f * i / FS);
            for (int c = 0; c < CH; c++) x[i * CH + c] = (float)v;
        }
        return x;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "DeepBassProvider") T = t;
        if (T == null) { Console.WriteLine("no encontre DeepBassProvider"); return 1; }

        // Caso realista: bombo en 45 Hz + nota de bajo en 60 Hz sonando a la vez (una cuarta justa)
        double f1 = 45, f2 = 60;
        float[] ent = Mezcla(new double[] { f1, f2 }, 0.28, 2.0);
        float[] sal = Procesar(ent, 0.30f);
        float[] limpio = Procesar(ent, 0f);   // paso directo: la referencia

        Console.WriteLine("\n  DOS NOTAS GRAVES A LA VEZ: bombo " + f1 + " Hz + bajo " + f2 + " Hz\n");
        Console.WriteLine("  Lo que SALE del generador de armonicos (dB respecto al mas fuerte):\n");
        Console.WriteLine("    frecuencia   nivel     que es");
        object[][] filas = {
            new object[]{  90.0, "armonico util  (2 x 45, hace 'oir' los 45)" },
            new object[]{ 120.0, "armonico util  (2 x 60, hace 'oir' los 60)" },
            new object[]{ 135.0, "armonico util  (3 x 45)" },
            new object[]{ 180.0, "armonico util  (3 x 60)" },
            new object[]{ 105.0, "INTERMODULACION (45+60): no es multiplo de nada" },
            new object[]{  15.0, "INTERMODULACION (60-45): un retumbo sin nota" },
            new object[]{ 165.0, "INTERMODULACION (45+120)" },
            new object[]{ 150.0, "INTERMODULACION (2x45 + 60)" },
        };
        double pico = -999;
        foreach (object[] f in filas) { double d = NivelDb(sal, (double)f[0]) ; if (d > pico) pico = d; }
        double utilMax = -999, maloMax = -999;
        foreach (object[] f in filas)
        {
            double hz = (double)f[0];
            double d = NivelDb(sal, hz) - pico;
            double antes = NivelDb(limpio, hz) - pico;
            bool util = ((string)f[1]).StartsWith("armonico");
            if (util) { if (d > utilMax) utilMax = d; } else { if (d > maloMax) maloMax = d; }
            Console.WriteLine(string.Format("    {0,6:0} Hz   {1,6:0.0}    {2}   (antes del bajo: {3:0.0})", hz, d, f[1], antes));
        }
        Console.WriteLine(string.Format("\n  El peor producto de intermodulacion queda a {0:0.0} dB del armonico mas fuerte.", maloMax - utilMax));

        // UNA sola nota: no hay con quien intermodular
        float[] sola = Procesar(Mezcla(new double[] { f1 }, 0.28, 2.0), 0.30f);
        double p90 = NivelDb(sola, 90), p105 = NivelDb(sola, 105);
        Console.WriteLine(string.Format("  Con UNA sola nota (45 Hz): armonico 90 Hz = {0:0.0} dB, basura en 105 Hz = {1:0.0} dB  ->  {2:0.0} dB de diferencia",
                                        p90, p105, p90 - p105));
        return 0;
    }
}

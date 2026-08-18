using System;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

class FuenteInf : ISampleProvider
{
    private readonly WaveFormat wf; private readonly Random r = new Random(1);
    public FuenteInf(int fs, int ch) { wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c) { for (int i = 0; i < c; i++) b[o + i] = (float)(r.NextDouble() * 0.5 - 0.25); return c; }
}

class BassBench
{
    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        Type T = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "DeepBassProvider") T = t;
        object bp = Activator.CreateInstance(T, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.CreateInstance,
                                             null, new object[] { new FuenteInf(44100, 2), 0.30f }, null);
        MethodInfo read = T.GetMethod("Read");
        float[] buf = new float[4096 * 2];
        object[] a = { buf, 0, 4096 * 2 };
        for (int i = 0; i < 200; i++) read.Invoke(bp, a);   // calentar
        int bloques = 44100 * 20 / 4096;
        double mejor = 1e9;
        for (int k = 0; k < 3; k++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < bloques; i++) read.Invoke(bp, a);
            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds < mejor) mejor = sw.Elapsed.TotalMilliseconds;
        }
        Console.WriteLine(string.Format("bajo psicoacustico actual (1 banda): {0:0.00} ms de CPU por segundo de musica", mejor / 20.0));
        Console.WriteLine(string.Format("   version de 3 bandas, estimada    : {0:0.00} ms  (el generador se triplica; el resto no)", mejor / 20.0 * 2.6));
        return 0;
    }
}

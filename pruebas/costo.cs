using System;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

class Fuente2 : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf;
    public Fuente2(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c) { for (int i = 0; i < c; i++) b[o + i] = d[i % d.Length]; return c; }
}

class Costo
{
    static int Main()
    {
        const int FS = 44100, CH = 2;
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type T = asm.GetType("Rocola.Form1", true).GetNestedType("DeepBassProvider", BindingFlags.NonPublic);
        Random r = new Random(3);
        float[] src = new float[FS * CH];
        for (int i = 0; i < FS; i++)
        {
            float v = (float)(0.35 * Math.Sin(2 * Math.PI * 55 * i / FS) + 0.25 * Math.Sin(2 * Math.PI * 440 * i / FS) + 0.05 * (r.NextDouble() * 2 - 1));
            src[i * CH] = v; src[i * CH + 1] = v * 0.95f;
        }
        object bp = Activator.CreateInstance(T, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.CreateInstance,
            null, new object[] { new Fuente2(src, FS, CH), 0.30f }, null);
        MethodInfo read = T.GetMethod("Read");
        float[] buf = new float[8192];
        // calentar
        for (int i = 0; i < 50; i++) read.Invoke(bp, new object[] { buf, 0, buf.Length });
        int bloques = 3000;   // 3000 * 8192 muestras / 2 canales
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < bloques; i++) read.Invoke(bp, new object[] { buf, 0, buf.Length });
        sw.Stop();
        double segAudio = (double)bloques * buf.Length / CH / FS;
        double ms = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine();
        Console.WriteLine(string.Format("  Procesados {0:0.0} segundos de audio estereo en {1:0} ms", segAudio, ms));
        Console.WriteLine(string.Format("  -> {0:0.000}% de UN nucleo de esta maquina", ms / (segAudio * 1000) * 100));
        Console.WriteLine();
        Console.WriteLine("  (La rocola tiene 2 nucleos mas lentos; aun multiplicando por 10 el costo,");
        Console.WriteLine("   sigue siendo una fraccion de un nucleo.)");
        return 0;
    }
}

// Comparacion reproducible con un ejecutable anterior REAL, no una copia del metodo nuevo.
// mcs -r:NAudio.dll -out:compara-local.exe compara-local.cs
// mono compara-local.exe /ruta/antes.exe /ruta/despues.exe
using System;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;
class ComparaLocal
{
    const BindingFlags B = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    sealed class Fuente : ISampleProvider
    {
        readonly float[] datos; int pos; readonly WaveFormat wf;
        public Fuente(int fs, int ch, double amp)
        {
            wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); datos = new float[4096];
            Random r = new Random(71);
            for (int i = 0; i < datos.Length; i++) datos[i] = (float)((r.NextDouble() * 2 - 1) * amp);
        }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int n)
        {
            for (int i = 0; i < n; i++) { b[o + i] = datos[pos]; pos = (pos + 1) & 4095; }
            return n;
        }
    }
    static ISampleProvider Crea(Assembly a, string etapa, int fs, int ch, double amp)
    {
        Type t = a.GetType("Rocola.Form1+" + etapa, true);
        object[] args = etapa == "SalaLocal" ? new object[] { new Fuente(fs,ch,amp), null, 0.26f, false, false, 4 }
            : new object[] { new Fuente(fs,ch,amp), null, 0.26f, true };
        return (ISampleProvider)Activator.CreateInstance(t, B, null, args, null);
    }
    static double Mide(Assembly a, string etapa, int bloque)
    {
        double mejor = double.MaxValue;
        for (int rep = 0; rep < 5; rep++)
        {
            ISampleProvider p = Crea(a, etapa, 44100, 2, 0.25);
            float[] b = new float[bloque];
            for (int j = 0; j < 100; j++) p.Read(b, 0, b.Length);
            Stopwatch sw = Stopwatch.StartNew();
            int vueltas = 44100 * 2 * 60 / bloque;
            for (int j = 0; j < vueltas; j++) p.Read(b, 0, b.Length);
            sw.Stop(); mejor = Math.Min(mejor, sw.Elapsed.TotalMilliseconds / 60);
        }
        return mejor;
    }
    static int Main(string[] args)
    {
        if (args.Length != 2) { Console.WriteLine("Uso: compara-local.exe antes.exe despues.exe"); return 2; }
        Assembly antes = Assembly.Load(System.IO.File.ReadAllBytes(args[0]));
        Assembly despues = Assembly.Load(System.IO.File.ReadAllBytes(args[1]));
        if (antes == despues) throw new Exception("Se requieren dos ensamblados distintos");
        int casos = 0;
        foreach (string etapa in new string[] { "SalaLocal", "PresenciaSala" })
        {
            foreach (int fs in new int[] { 8000, 44100, 48000, 96000 })
            foreach (int ch in new int[] { 1, 2 })
            foreach (double amp in new double[] { 0, 0.0001, 0.03, 0.25, 0.9, 1.5 })
            foreach (int bloque in new int[] { 1, 777, 4096 })
            {
                ISampleProvider p = Crea(antes, etapa, fs, ch, amp), q = Crea(despues, etapa, fs, ch, amp);
                float[] x = new float[bloque + 4], y = new float[bloque + 4];
                for (int i = 0, fin = Math.Max(20, fs * ch / bloque); i < fin; i++)
                {
                    if (etapa == "SalaLocal" && i == fin / 2)
                    {
                        p.GetType().GetMethod("Ajusta", B).Invoke(p, new object[] { true, true, -5 });
                        q.GetType().GetMethod("Ajusta", B).Invoke(q, new object[] { true, true, -5 });
                    }
                    if (p.Read(x, 2, bloque) != q.Read(y, 2, bloque)) throw new Exception("Longitud distinta");
                    // Igualdad de representacion, incluidos ceros con signo. Sin asignaciones por muestra.
                    byte[] bx = new byte[x.Length * 4], by = new byte[y.Length * 4];
                    Buffer.BlockCopy(x, 0, bx, 0, bx.Length); Buffer.BlockCopy(y, 0, by, 0, by.Length);
                    for (int j = 0; j < bx.Length; j++) if (bx[j] != by[j])
                        throw new Exception(etapa + " cambia muestras: " + fs + "/" + ch + "/" + amp + "/" + bloque);
                }
                casos++;
            }
            foreach (int bloque in new int[] { 64, 4096 })
                Console.WriteLine(etapa + " bloque=" + bloque + ": antes=" + Mide(antes, etapa, bloque).ToString("F3")
                    + " despues=" + Mide(despues, etapa, bloque).ToString("F3") + " ms/s (sintetico Mono, incluye fuente)");
        }
        Console.WriteLine("OK " + casos + " combinaciones con muestras identicas bit a bit");
        return 0;
    }
}

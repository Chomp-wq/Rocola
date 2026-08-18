using System;
using System.Reflection;
using NAudio.Wave;

class F4 : ISampleProvider
{
    private readonly float[] d; private int p; private readonly WaveFormat wf;
    public F4(float[] x, int fs, int ch) { d = x; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); for (int i = 0; i < n; i++) b[o+i] = d[p+i]; p += n; return n; }
}

// ¿Trata igual a todas las notas graves? Con UNA sola banda, el filtro de arriba apaga las notas mas altas,
// asi que un Do grave recibe mucha mas ayuda que un Sol: el bajo suena disparejo de nota a nota.
class Pareja
{
    const int FS = 44100, CH = 2;
    static double Db(float[] x, double hz)
    {
        int n = x.Length / CH;
        double w = 2*Math.PI*hz/FS, k = 2*Math.Cos(w), s1 = 0, s2 = 0, s0;
        for (int i = 0; i < n; i++) { s0 = x[i*CH] + k*s1 - s2; s2 = s1; s1 = s0; }
        return 20*Math.Log10(Math.Sqrt(s1*s1 + s2*s2 - k*s1*s2)/(n/2.0) + 1e-12);
    }
    static Type Tipo(string exe)
    {
        Assembly a = Assembly.LoadFrom(exe);
        Type TF = a.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if (t.Name == "DeepBassProvider") return t;
        return null;
    }
    static double Ayuda(Type T, double f)   // nivel del 2o armonico generado, en dB
    {
        int n = (int)(1.5*FS); float[] x = new float[n*CH];
        for (int i = 0; i < n; i++) { float v = (float)(0.35*Math.Sin(2*Math.PI*f*i/FS)); for (int c = 0; c < CH; c++) x[i*CH+c] = v; }
        object bp = Activator.CreateInstance(T, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.CreateInstance,
                                             null, new object[] { new F4(x, FS, CH), 0.30f }, null);
        MethodInfo rd = T.GetMethod("Read");
        float[] b = new float[x.Length]; int t = 0;
        while (t < b.Length) { object[] a = { b, t, Math.Min(4096, b.Length - t) }; int k = (int)rd.Invoke(bp, a); if (k <= 0) break; t += k; }
        return Db(b, 2*f);
    }
    static int Main()
    {
        Type A = Tipo("unabanda.exe"), B = Tipo("rocola.exe");
        double[] notas = { 35, 41, 49, 58, 65, 73, 82, 92, 104 };   // do1 .. sol2, mas o menos
        Console.WriteLine("\n  AYUDA QUE RECIBE CADA NOTA (nivel del armonico generado, dB)\n");
        Console.WriteLine("      nota    1 banda   3 bandas");
        double[] va = new double[notas.Length], vb = new double[notas.Length];
        for (int i = 0; i < notas.Length; i++)
        {
            va[i] = Ayuda(A, notas[i]); vb[i] = Ayuda(B, notas[i]);
            Console.WriteLine(string.Format("    {0,4:0} Hz    {1,7:0.0}   {2,7:0.0}", notas[i], va[i], vb[i]));
        }
        double ra = Max(va) - Min(va), rb = Max(vb) - Min(vb);
        Console.WriteLine(string.Format("\n    desnivel entre la nota mas y menos favorecida:  1 banda {0:0.0} dB   |   3 bandas {1:0.0} dB", ra, rb));
        return 0;
    }
    static double Max(double[] v) { double m = -1e9; foreach (double x in v) if (x > m) m = x; return m; }
    static double Min(double[] v) { double m = 1e9; foreach (double x in v) if (x < m) m = x; return m; }
}

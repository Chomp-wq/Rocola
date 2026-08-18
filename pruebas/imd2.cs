using System;
using System.Reflection;
using NAudio.Wave;

class F3 : ISampleProvider
{
    private readonly float[] d; private int p; private readonly WaveFormat wf;
    public F3(float[] x, int fs, int ch) { d = x; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); for (int i = 0; i < n; i++) b[o+i] = d[p+i]; p += n; return n; }
}

// Compara UNA banda contra TRES: para cada pareja de notas graves simultaneas, cuanto sobresale el peor
// producto de intermodulacion por encima del armonico util mas fuerte. Menos es mejor.
class Imd2
{
    const int FS = 44100, CH = 2;
    static double Db(float[] x, double hz)
    {
        int n = x.Length / CH;
        double w = 2*Math.PI*hz/FS, k = 2*Math.Cos(w), s1 = 0, s2 = 0, s0;
        for (int i = 0; i < n; i++) { s0 = x[i*CH] + k*s1 - s2; s2 = s1; s1 = s0; }
        return 20*Math.Log10(Math.Sqrt(s1*s1 + s2*s2 - k*s1*s2)/(n/2.0) + 1e-12);
    }
    static float[] Proc(Type T, float[] e, float amt)
    {
        object bp = Activator.CreateInstance(T, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.CreateInstance,
                                             null, new object[] { new F3(e, FS, CH), amt }, null);
        MethodInfo rd = T.GetMethod("Read");
        float[] b = new float[e.Length]; int t = 0;
        while (t < b.Length) { object[] a = { b, t, Math.Min(4096, b.Length - t) }; int n = (int)rd.Invoke(bp, a); if (n <= 0) break; t += n; }
        return b;
    }
    static float[] Mez(double f1, double f2, double amp, double seg)
    {
        int n = (int)(seg*FS); float[] x = new float[n*CH];
        for (int i = 0; i < n; i++) { double v = amp*Math.Sin(2*Math.PI*f1*i/FS) + amp*Math.Sin(2*Math.PI*f2*i/FS); for (int c = 0; c < CH; c++) x[i*CH+c] = (float)v; }
        return x;
    }
    static Type Tipo(string exe)
    {
        Assembly a = Assembly.LoadFrom(exe);
        Type TF = a.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)) if (t.Name == "DeepBassProvider") return t;
        return null;
    }
    // sobresaliente del peor producto de intermodulacion sobre el mejor armonico util (dB)
    static double Sucio(Type T, double f1, double f2)
    {
        float[] y = Proc(T, Mez(f1, f2, 0.28, 2.0), 0.30f);
        double[] utiles = { 2*f1, 3*f1, 2*f2, 3*f2 };
        double[] malos  = { f1+f2, f2-f1, 2*f1+f2, f1+2*f2, 2*f2-f1, 2*f1-f2 };
        double bu = -999, bm = -999;
        foreach (double h in utiles) { if (h < 20 || h > 400) continue; double d = Db(y, h); if (d > bu) bu = d; }
        foreach (double h in malos)
        {
            if (h < 20 || h > 400) continue;
            bool esArmonico = false;                       // si cae justo en un armonico, no es basura
            foreach (double u in utiles) if (Math.Abs(h - u) < 0.6) esArmonico = true;
            if (esArmonico) continue;
            double d = Db(y, h); if (d > bm) bm = d;
        }
        return bm - bu;
    }
    static int Main()
    {
        Type A = Tipo("unabanda.exe"), B = Tipo("rocola.exe");
        if (A == null || B == null) { Console.WriteLine("no encontre el generador"); return 1; }
        double[][] casos = {
            new double[]{45,60,0},  new double[]{45,67.5,0}, new double[]{45,90,0},
            new double[]{50,75,0},  new double[]{40,120,0},  new double[]{55,110,0},
            new double[]{45,55,0},  new double[]{60,160,0},
        };
        string[] nom = { "cuarta (45+60)", "quinta (45+67)", "octava (45+90)", "quinta (50+75)",
                         "doceava (40+120)", "octava (55+110)", "muy juntas (45+55)", "lejos (60+160)" };
        Console.WriteLine("\n  Cuanto SOBRESALE la basura por encima del armonico util (menos = mas limpio)\n");
        Console.WriteLine("    intervalo             1 banda    3 bandas     mejora");
        double sumMej = 0; int n = 0, mejoraron = 0;
        for (int i = 0; i < casos.Length; i++)
        {
            double a = Sucio(A, casos[i][0], casos[i][1]), b = Sucio(B, casos[i][0], casos[i][1]);
            double mej = a - b; sumMej += mej; n++; if (mej > 0.5) mejoraron++;
            Console.WriteLine(string.Format("    {0,-20} {1,7:+0.0;-0.0}    {2,7:+0.0;-0.0}    {3,6:+0.0;-0.0} dB", nom[i], a, b, mej));
        }
        Console.WriteLine(string.Format("\n    promedio de mejora: {0:0.0} dB   (mejoraron {1} de {2})", sumMej/n, mejoraron, n));
        return 0;
    }
}

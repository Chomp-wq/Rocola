using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;

class Bench
{
    const int FS = 44100, CH = 2;
    static Type TF; static object F; static MethodInfo REP;
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }

    static double Mide(int segundos)
    {
        int n = FS * segundos;
        float[] buf = new float[4096 * CH];
        Random r = new Random(7);
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)(r.NextDouble() * 0.6 - 0.3);
        Stopwatch sw = Stopwatch.StartNew();
        int hecho = 0;
        while (hecho < n) { REP.Invoke(F, new object[] { buf, 0, 4096 * CH, FS, CH }); hecho += 4096; }
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    static float aurora = 0.55f;
    static int Main(string[] args)
    {
        if (args.Length > 0) aurora = float.Parse(args[0]);
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        SetFld("levelLock", new object());
        SetFld("levelQ", Activator.CreateInstance(TF.GetField("levelQ", BindingFlags.Instance|BindingFlags.NonPublic).FieldType));
        SetFld("nowPlaying", null);
        SetFld("specVis", new float[8]);
        SetFld("specNow", new float[8]);
        SetFld("auroraAmount", aurora);
        REP = TF.GetMethod("ReportAudioBlock", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        Mide(4);   // calentar el JIT
        double ms = 0; int reps = 3;
        for (int k = 0; k < reps; k++) { double t = Mide(20); if (ms == 0 || t < ms) ms = t; }
        Console.WriteLine(string.Format("ReportAudioBlock: {0:0.0} ms por 20 s de audio  =  {1:0.00} ms de CPU por segundo de musica ({2:0.000}% de un nucleo)", ms, ms/20.0, ms/20.0/10.0));
        return 0;
    }
}

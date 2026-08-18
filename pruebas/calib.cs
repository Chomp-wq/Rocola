using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

class Calib
{
    const int FS = 44100, CH = 2;
    static Type TF; static object F; static MethodInfo REP;
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }

    // devuelve el rms CRUDO por banda (antes de dB/inclinacion): asi calibro con numeros, no a ojo
    static float[] Bandas(float[] mono)
    {
        SetFld("specLp", null); SetFld("specCoef", null); SetFld("specRate", 0);
        SetFld("levelQ", Activator.CreateInstance(TF.GetField("levelQ", BindingFlags.Instance|BindingFlags.NonPublic).FieldType));
        float[] buf = new float[4096 * CH];
        int hecho = 0;
        while (hecho < mono.Length)
        {
            int m = Math.Min(4096, mono.Length - hecho);
            for (int i = 0; i < m; i++) { buf[i*CH] = mono[hecho+i]; buf[i*CH+1] = mono[hecho+i]; }
            REP.Invoke(F, new object[] { buf, 0, m*CH, FS, CH });
            hecho += m;
        }
        object q = Fld("levelQ"); Type tq = q.GetType();
        int cnt = (int)tq.GetProperty("Count").GetValue(q, null);
        MethodInfo deq = tq.GetMethod("Dequeue");
        object ult = null; for (int i = 0; i < cnt; i++) ult = deq.Invoke(q, null);
        return (float[])ult.GetType().GetField("Esp").GetValue(ult);
    }

    static float[] Rosa(int n, int seed)   // ruido ROSA: misma energia por octava (asi suena la musica)
    {
        Random r = new Random(seed); float[] x = new float[n];
        double b0=0,b1=0,b2=0,b3=0,b4=0,b5=0,b6=0;
        for (int i = 0; i < n; i++)
        {
            double w = r.NextDouble()*2-1;
            b0=0.99886*b0+w*0.0555179; b1=0.99332*b1+w*0.0750759; b2=0.96900*b2+w*0.1538520;
            b3=0.86650*b3+w*0.3104856; b4=0.55000*b4+w*0.5329522; b5=-0.7616*b5-w*0.0168980;
            double v=(b0+b1+b2+b3+b4+b5+b6+w*0.5362)*0.11; b6=w*0.115926;
            x[i]=(float)Math.Max(-1,Math.Min(1,v));
        }
        return x;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        SetFld("levelLock", new object()); SetFld("nowPlaying", null);
        SetFld("specVis", new float[8]); SetFld("specNow", new float[8]); SetFld("auroraAmount", 0.55f);
        REP = TF.GetMethod("ReportAudioBlock", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

        // normalizar al volumen al que la rocola NIVELA todas las canciones (normTarget = 0.26 RMS):
        // ese es el punto de operacion real, no un nivel cualquiera.
        float[] rosa = Rosa(FS*2, 5);
        double sq = 0; foreach (float v in rosa) sq += (double)v*v;
        float k = (float)(0.26 / Math.Sqrt(sq/rosa.Length));
        for (int i = 0; i < rosa.Length; i++) rosa[i] *= k;
        float[] b = Bandas(rosa);
        Console.WriteLine("\n  RUIDO ROSA (energia pareja por octava = como se reparte la musica real)");
        Console.WriteLine("  banda   dB crudo   inclinacion que la dejaria PLANA");
        double refDb = 20*Math.Log10(b[0]+1e-12);
        string[] nom = { "sub","grave","bajo-med","medio","med-alto","presencia","brillo","aire" };
        int[] sug = new int[8];
        for (int i = 0; i < 8; i++)
        {
            double db = 20*Math.Log10(b[i]+1e-12);
            sug[i] = (int)Math.Round(refDb - db);
            Console.WriteLine(string.Format("  {0,-10} {1,7:0.0}    {2,5:+0;-0} dB", nom[i], db, sug[i]));
        }
        Console.Write("\n  -> SpecTilt sugerida = { ");
        for (int i = 0; i < 8; i++) Console.Write(sug[i] + "f" + (i<7 ? ", " : " "));
        Console.WriteLine("};");

        // tono puro: cuanta fuga deja en las bandas de arriba (el caso peor)
        float[] t = new float[FS*2];
        for (int i = 0; i < t.Length; i++) t[i] = (float)(0.45*Math.Sin(2*Math.PI*240*i/FS));
        float[] bt = Bandas(t);
        Console.WriteLine("\n  TONO PURO de 240 Hz (caso peor de fuga): dB de cada banda respecto a la suya");
        double pico = 0; foreach (float v in bt) pico = Math.Max(pico, v);
        for (int i = 0; i < 8; i++)
            Console.WriteLine(string.Format("  {0,-10} {1,7:0.0} dB", nom[i], 20*Math.Log10(bt[i]/pico+1e-12)));
        return 0;
    }
}

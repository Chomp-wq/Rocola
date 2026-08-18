using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

class Real
{
    const int FS = 44100, CH = 2;
    static Type TF; static object F; static MethodInfo REP, AVZ;
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        F = FormatterServices.GetUninitializedObject(TF);
        SetFld("levelLock", new object()); SetFld("nowPlaying", null);
        SetFld("specVis", new float[8]); SetFld("specNow", new float[8]);
        SetFld("levelQ", Activator.CreateInstance(TF.GetField("levelQ", BindingFlags.Instance|BindingFlags.NonPublic).FieldType));
        REP = TF.GetMethod("ReportAudioBlock", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        AVZ = TF.GetMethod("AvanzaAurora", BindingFlags.Instance|BindingFlags.NonPublic);

        // "cancion": linea de bajo + acordes + charles, con un golpe de bombo cada 0.5 s
        int n = FS * 3; float[] mono = new float[n];
        Random r = new Random(11);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / FS;
            double v = 0.55*Math.Sin(2*Math.PI*55*t) + 0.30*Math.Sin(2*Math.PI*110*t)
                     + 0.22*Math.Sin(2*Math.PI*330*t+0.6) + 0.16*Math.Sin(2*Math.PI*494*t+1.2)
                     + 0.07*Math.Sin(2*Math.PI*2600*t) + 0.05*(r.NextDouble()*2-1);
            int f = i % (FS/2);
            v += 0.85*Math.Exp(-f/(FS*0.008));                  // bombo
            if (f > FS/4) v += 0.10*(r.NextDouble()*2-1)*Math.Exp(-(f-FS/4)/(FS*0.02));  // charles
            mono[i] = (float)v;
        }
        double sq=0; foreach(float v in mono) sq += (double)v*v;
        float k=(float)(0.26/Math.Sqrt(sq/n));
        for (int i=0;i<n;i++) mono[i]*=k;                        // al volumen al que la rocola nivela

        float[] buf = new float[4096*CH];
        List<string> cuadros = new List<string>();
        int hecho=0;
        while (hecho < n)
        {
            int m = Math.Min(4096, n-hecho);
            for (int i=0;i<m;i++){ buf[i*CH]=mono[hecho+i]; buf[i*CH+1]=mono[hecho+i]; }
            REP.Invoke(F, new object[]{ buf,0,m*CH,FS,CH });
            hecho+=m;
            // pasar lo medido a "lo que se oye" y avanzar la curva (como hace el temporizador)
            object q=Fld("levelQ"); Type tq=q.GetType();
            int cnt=(int)tq.GetProperty("Count").GetValue(q,null);
            MethodInfo deq=tq.GetMethod("Dequeue");
            object ult=null; for(int i=0;i<cnt;i++) ult=deq.Invoke(q,null);
            if (ult!=null){ float[] e=(float[])ult.GetType().GetField("Esp").GetValue(ult); if(e!=null) Array.Copy(e,(float[])Fld("specNow"),8); }
            AVZ.Invoke(F,null);
            float[] vis=(float[])Fld("specVis");
            string s=""; for(int b=0;b<8;b++) s += vis[b].ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + (b<7?",":"");
            cuadros.Add(s);
        }
        File.WriteAllLines("aurora_real.csv", cuadros.ToArray());
        Console.WriteLine("cuadros capturados: " + cuadros.Count);
        Console.WriteLine("ultimo: " + cuadros[cuadros.Count-1]);
        return 0;
    }
}

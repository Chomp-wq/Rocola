using System;
using System.Diagnostics;

// A/B del nucleo del analisis de espectro: version actual (por muestra del stream entrelazado)
// contra la version por CUADRO (L+R juntos): misma respuesta en Hz, la mitad de trabajo.
class Bench2
{
    const int FS = 44100, CH = 2, SEG = 20;
    static readonly float[] Cortes = { 60f, 150f, 350f, 800f, 1800f, 4000f, 9000f };

    static float[] Coef(float rate)
    { float[] c = new float[7]; for (int k = 0; k < 7; k++) c[k] = (float)(1.0 - Math.Exp(-2.0 * Math.PI * Cortes[k] / rate)); return c; }

    static double Actual(float[] buf, int reps)
    {
        float[] sc = Coef(FS * CH), sl = new float[21];
        double[] sSum = new double[8];
        Stopwatch sw = Stopwatch.StartNew();
        for (int r = 0; r < reps; r++)
        for (int i = 0; i < buf.Length; i++)
        {
            float v = buf[i];
            float ant = 0f;
            for (int k = 0; k < 7; k++)
            {
                int q = k * 3; float x = v;
                for (int pp = 0; pp < 3; pp++) { sl[q + pp] += (x - sl[q + pp]) * sc[k]; x = sl[q + pp]; }
                float bnd = x - ant; ant = x;
                sSum[k] += bnd * bnd;
            }
            float top = v - ant; sSum[7] += top * top;
        }
        sw.Stop(); GC.KeepAlive(sSum); return sw.Elapsed.TotalMilliseconds;
    }

    static double PorCuadro(float[] buf, int reps)
    {
        float[] sc = Coef(FS), sl = new float[21];
        double[] sSum = new double[8];
        Stopwatch sw = Stopwatch.StartNew();
        for (int r = 0; r < reps; r++)
        for (int i = 0; i + 1 < buf.Length; i += CH)
        {
            float v = (buf[i] + buf[i + 1]) * 0.5f;
            float ant = 0f;
            for (int k = 0; k < 7; k++)
            {
                int q = k * 3; float x = v;
                for (int pp = 0; pp < 3; pp++) { sl[q + pp] += (x - sl[q + pp]) * sc[k]; x = sl[q + pp]; }
                float bnd = x - ant; ant = x;
                sSum[k] += bnd * bnd;
            }
            float top = v - ant; sSum[7] += top * top;
        }
        sw.Stop(); GC.KeepAlive(sSum); return sw.Elapsed.TotalMilliseconds;
    }

    // Igual que PorCuadro pero con el estado en VARIABLES sueltas (sin indexar el arreglo 42 veces por muestra)
    static double PorCuadroLocal(float[] buf, int reps)
    {
        float[] scA = Coef(FS);
        float c0=scA[0],c1=scA[1],c2=scA[2],c3=scA[3],c4=scA[4],c5=scA[5],c6=scA[6];
        float a0=0,a1=0,a2=0, b0=0,b1=0,b2=0, d0=0,d1=0,d2=0, e0=0,e1=0,e2=0, f0=0,f1=0,f2=0, g0=0,g1=0,g2=0, h0=0,h1=0,h2=0;
        double s0=0,s1=0,s2=0,s3=0,s4=0,s5=0,s6=0,s7=0;
        Stopwatch sw = Stopwatch.StartNew();
        for (int r = 0; r < reps; r++)
        for (int i = 0; i + 1 < buf.Length; i += CH)
        {
            float v = (buf[i] + buf[i + 1]) * 0.5f;
            a0 += (v-a0)*c0; a1 += (a0-a1)*c0; a2 += (a1-a2)*c0;
            b0 += (v-b0)*c1; b1 += (b0-b1)*c1; b2 += (b1-b2)*c1;
            d0 += (v-d0)*c2; d1 += (d0-d1)*c2; d2 += (d1-d2)*c2;
            e0 += (v-e0)*c3; e1 += (e0-e1)*c3; e2 += (e1-e2)*c3;
            f0 += (v-f0)*c4; f1 += (f0-f1)*c4; f2 += (f1-f2)*c4;
            g0 += (v-g0)*c5; g1 += (g0-g1)*c5; g2 += (g1-g2)*c5;
            h0 += (v-h0)*c6; h1 += (h0-h1)*c6; h2 += (h1-h2)*c6;
            float t0=a2, t1=b2-a2, t2=d2-b2, t3=e2-d2, t4=f2-e2, t5=g2-f2, t6=h2-g2, t7=v-h2;
            s0+=t0*t0; s1+=t1*t1; s2+=t2*t2; s3+=t3*t3; s4+=t4*t4; s5+=t5*t5; s6+=t6*t6; s7+=t7*t7;
        }
        sw.Stop(); GC.KeepAlive(s0+s7); return sw.Elapsed.TotalMilliseconds;
    }

    static int Main()
    {
        float[] buf = new float[FS * CH];
        Random r = new Random(7);
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)(r.NextDouble() * 0.6 - 0.3);
        Actual(buf,1); PorCuadro(buf,1); PorCuadroLocal(buf,1);   // calentar
        double a=1e9,b=1e9,c=1e9;
        for (int k=0;k<3;k++){ double x=Actual(buf,SEG); if(x<a)a=x; x=PorCuadro(buf,SEG); if(x<b)b=x; x=PorCuadroLocal(buf,SEG); if(x<c)c=x; }
        Console.WriteLine(string.Format("actual (por muestra entrelazada) : {0,7:0.0} ms / {1} s audio = {2:0.00} ms por segundo", a, SEG, a/SEG));
        Console.WriteLine(string.Format("por CUADRO (L+R)                 : {0,7:0.0} ms                = {1:0.00} ms por segundo  ({2:0.0}x mas rapido)", b, b/SEG, a/b));
        Console.WriteLine(string.Format("por CUADRO + estado en locales   : {0,7:0.0} ms                = {1:0.00} ms por segundo  ({2:0.0}x mas rapido)", c, c/SEG, a/c));
        return 0;
    }
}

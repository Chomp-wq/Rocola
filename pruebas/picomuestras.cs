using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// EL LIMITADOR DE PICO REAL CON LECTURAS MENORES QUE UN CUADRO (Rocola/PicoReal.cs, 22-10-2026).
// Pedirle menos de un cuadro (una muestra en estereo) devolvia 0, y quien lo lee (Encadenado, SinPausa.cs) toma 0 por el FIN
// de la cancion. Ahora el cuadro se lee entero aparte, se entrega la parte pedida y el resto sale en la lectura siguiente
// (como GraveMono y SalaLocal). Se comprueba, con uno, dos y seis canales:
//   1) pedir de a una muestra (o de a menos de un cuadro, o a trozos raros) da, bit a bit, lo mismo que pedir todo junto;
//   2) nunca devuelve 0 antes del final de verdad, y al final devuelve 0 (una vez agotado todo, incluida la cola);
//   3) lo que ya funcionaba (lecturas de un cuadro o mas) no cambia: el mismo resultado que de siempre con el origen partido.
class FuenteQ : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos, k; private readonly int[] trozos;
    public FuenteQ(float[] datos, int fs, int ch, int[] trozos) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); this.trozos = trozos; }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    {
        if (trozos != null && trozos.Length > 0) c = Math.Min(c, trozos[k++ % trozos.Length]);
        int n = Math.Min(c, d.Length - pos);
        if (n <= 0) return 0;
        Array.Copy(d, pos, b, o, n); pos += n; return n;
    }
}
class PicoMuestras
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TLim; static float techo;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static bool Igual(float[] a, float[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (BitConverter.ToInt32(BitConverter.GetBytes(a[i]), 0) != BitConverter.ToInt32(BitConverter.GetBytes(b[i]), 0)) return false;
        return true;
    }
    static float[] Musica(int fs, int ch, double seg)
    {
        int n = (int)(fs * seg); float[] x = new float[n * ch]; Random r = new Random(3);
        for (int i = 0; i < n; i++)
            for (int c = 0; c < ch; c++)
            {
                double v = 0.9 * Math.Sin(2 * Math.PI * (110 + 55 * c) * i / fs) * (i % 7000 < 300 ? 1.4 : 0.6) + 0.05 * (r.NextDouble() * 2 - 1);
                x[i * ch + c] = (float)Math.Max(-1.0, Math.Min(1.0, v));
            }
        return x;
    }
    static ISampleProvider Lim(float[] x, int fs, int ch, int[] trozos) { return (ISampleProvider)Activator.CreateInstance(TLim, T, null, new object[] { new FuenteQ(x, fs, ch, trozos), techo, 3.0 }, null); }
    // Lee todo con una lista de tamanos de peticion que se repite; devuelve la salida y cuenta los ceros que no eran el final.
    static float[] Lee(ISampleProvider p, int[] pedidos, out int cerosDeMas, out int cerosFinales)
    {
        List<float> r = new List<float>(); float[] buf = new float[40000]; int k = 0; cerosDeMas = 0; cerosFinales = 0;
        while (true)
        {
            int pide = pedidos[k++ % pedidos.Length];
            int n = p.Read(buf, 17, pide);
            if (n < 0 || n > pide) throw new Exception("devolvio " + n + " pidiendo " + pide);
            if (n == 0)
            {
                // Un cero solo vale como final si TODAS las siguientes tambien lo son (incluida una de un cuadro entero).
                int m = p.Read(buf, 17, 4096);
                if (m == 0) { cerosFinales++; break; }
                cerosDeMas++;
                for (int i = 0; i < m; i++) r.Add(buf[17 + i]);
                continue;
            }
            for (int i = 0; i < n; i++) r.Add(buf[17 + i]);
            if (r.Count > 5000000) throw new Exception("no se acaba nunca");
        }
        return r.ToArray();
    }
    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type tf = a.GetType("Rocola.Form1", true);
        TLim = tf.GetNestedType("LimitadorPicoReal", T);
        techo = (float)tf.GetField("TechoPicoReal", T).GetRawConstantValue();
        if (TLim == null) { Check(false, "no esta el limitador de pico real"); return 1; }
        foreach (int ch in new int[] { 1, 2, 6 })
        {
            int fs = 44100; float[] x = Musica(fs, ch, 1.2);
            int c1, f1, c2, f2, c3, f3;
            float[] grande = Lee(Lim(x, fs, ch, null), new int[] { 4096 * ch }, out c1, out f1);
            float[] unaA = Lee(Lim(x, fs, ch, null), new int[] { 1 }, out c2, out f2);
            Check(c1 == 0 && f1 == 1 && Igual(grande, unaA) && c2 == 0 && f2 == 1,
                  ch + " canales: pedir de a UNA muestra da, bit a bit, lo mismo que pedir todo junto (" + grande.Length + " muestras), sin ceros antes del final");
            int[] raros = ch == 1 ? new int[] { 1, 3, 1000, 2, 77 } : new int[] { 1, ch - 1, ch + 1, 1000 + 1, 2 * ch - 1, 3, 77 * ch + 1 };
            float[] rara = Lee(Lim(x, fs, ch, null), raros, out c3, out f3);
            Check(c3 == 0 && Igual(grande, rara), ch + " canales: peticiones de tamanos raros (menos de un cuadro, cuadro y medio, impares) dan lo mismo");
            // Con el origen que ademas entrega a trozos partidos.
            int[] partidos = new int[] { 3, 1, ch * 5 + 1, 7, 1000, 2 };
            int c4, f4;
            float[] sinP = Lee(Lim(x, fs, ch, partidos), new int[] { 4096 * ch }, out c4, out f4);
            float[] conP = Lee(Lim(x, fs, ch, partidos), new int[] { 1, 5 * ch + 2, 3 }, out c4, out f4);
            Check(Igual(sinP, conP) && c4 == 0, ch + " canales: con el origen partido tambien da lo mismo");
        }
        {   // Un origen vacio: el fin llega a la primera (0), tambien pidiendo una sola muestra.
            int c, f; float[] v = Lee(Lim(new float[0], 44100, 2, null), new int[] { 1 }, out c, out f);
            Check(v.Length == 0 && f == 1 && c == 0, "un origen vacio acaba a la primera, pidiendo de a una muestra");
            float[] uno = new float[] { 0.5f, -0.5f }; int c5, f5, c6, f6;
            float[] vv = Lee(Lim(uno, 44100, 2, null), new int[] { 1 }, out c5, out f5), vg = Lee(Lim(uno, 44100, 2, null), new int[] { 4096 }, out c6, out f6);
            Check(Igual(vv, vg) && f5 == 1 && c5 == 0, "un origen de un solo cuadro sale igual pidiendo de a una muestra que todo junto (" + vv.Length + " muestras con su cola)");
        }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}

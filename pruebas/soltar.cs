using System;
using System.Reflection;
using NAudio.Wave;
// EL LIMITADOR ESPERA ANTES DE SOLTAR (PicoReal.cs, LimitadorPicoReal).
//
// Tras bajar, la ganancia espera 20 ms quieta y luego suelta en 100 ms (antes soltaba en 120 ms en cuanto el pico
// salia de los 3 ms de pre-escucha, y con un bajo fuerte subia y bajaba con cada ciclo). Aqui, con el limitador de la
// rocola de verdad:
//   - un bajo sostenido por encima del techo sale limpio;
//   - tras un golpe suelto la ganancia vuelve algo antes que con el de antes;
//   - en canciones de prueba ya niveladas limita casi lo mismo que antes (lo de antes, medido con este mismo banco y
//     el limitador de 120 ms: tiempo limitando 8/33/17 % y ganancia moviendose 4,3/14,0/8,5 dB/s);
//   - lo que no llega al techo sale intacto y nada pasa del techo.
// Son senales sinteticas: miden la cuenta, no el oido.
class Soltar
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, L;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static float Techo { get { return (float)TF.GetField("TechoPicoReal", T).GetValue(null); } }

    sealed class Fuente : ISampleProvider
    {
        readonly float[] d; int p; readonly WaveFormat wf;
        public Fuente(float[] x, int tasa) { d = x; wf = WaveFormat.CreateIeeeFloatWaveFormat(tasa, 2); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); Array.Copy(d, p, b, o, n); p += n; return n; }
    }
    // Pasa 'x' (estereo) por el limitador. Devuelve la salida ya alineada con la entrada y la ganancia cada 10 ms (dB).
    static float[] Pasa(float[] x, int tasa, out double[] grDb)
    {
        object lim = Activator.CreateInstance(L, T, null, new object[] { new Fuente(x, tasa), Techo, 3.0 }, null);
        int retraso = (int)L.GetProperty("DelayFrames", T).GetValue(lim, null);
        MethodInfo rd = L.GetMethod("Read"); FieldInfo env = L.GetField("env", T);
        int bloque = tasa / 100 * 2;
        float[] y = new float[x.Length + retraso * 2 + bloque];
        double[] gr = new double[y.Length / bloque + 2];
        float[] b = new float[bloque]; object[] a = { b, 0, bloque };
        int o = 0, k = 0, n;
        while ((n = (int)rd.Invoke(lim, a)) > 0 && o + n <= y.Length) { Array.Copy(b, 0, y, o, n); o += n; gr[k++] = -20 * Math.Log10((float)env.GetValue(lim)); }
        Array.Resize(ref gr, k);
        grDb = gr;
        float[] al = new float[x.Length];
        Array.Copy(y, retraso * 2, al, 0, Math.Min(al.Length, y.Length - retraso * 2));
        return al;
    }
    // Lo que queda al quitar la fundamental (canal izquierdo, del segundo 1 al 2), en dB respecto del total.
    static double Distorsion(float[] y, double f, int tasa)
    {
        int a = tasa, n = tasa; double re = 0, im = 0;
        for (int i = 0; i < n; i++) { double w = 2 * Math.PI * f * (a + i) / tasa; re += y[2 * (a + i)] * Math.Cos(w); im += y[2 * (a + i)] * Math.Sin(w); }
        re *= 2.0 / n; im *= 2.0 / n;
        double res = 0, tot = 0;
        for (int i = 0; i < n; i++) { double w = 2 * Math.PI * f * (a + i) / tasa; double v = y[2 * (a + i)], fu = re * Math.Cos(w) + im * Math.Sin(w); res += (v - fu) * (v - fu); tot += v * v; }
        return 10 * Math.Log10(res / tot);
    }
    static float[] Tono(double f, double amp, int tasa, double seg)
    {
        int n = (int)(tasa * seg); float[] x = new float[n * 2];
        for (int i = 0; i < n; i++) { float v = (float)(amp * Math.Sin(2 * Math.PI * f * i / tasa)); x[2 * i] = v; x[2 * i + 1] = v; }
        return x;
    }
    // La cancion de prueba de nivelpicos.cs (colchon y golpes graves), ya subida lo que le da el nivelado nuevo.
    static float[] Cancion(double picoDb, double golpes, int semilla, int seg, float g)
    {
        int fr = 44100 * seg; float[] x = new float[fr * 2]; Random r = new Random(semilla);
        double lp = 0, lp2 = 0;
        for (int i = 0; i < fr; i++) { double w = r.NextDouble() * 2 - 1; lp += (w - lp) * 0.08; lp2 += (lp - lp2) * 0.3; x[2 * i] = (float)lp2; x[2 * i + 1] = (float)(lp2 * 0.9 + 0.1 * lp); }
        int per = (int)(44100 / golpes);
        for (int s = 0; s < fr; s += per)
            for (int k = 0; k < 4410 && s + k < fr; k++)
            {
                double e = Math.Exp(-k / 600.0), v = e * (Math.Sin(2 * Math.PI * 70 * k / 44100.0) * 0.7 + (r.NextDouble() * 2 - 1) * 0.3 * Math.Exp(-k / 150.0));
                x[2 * (s + k)] += (float)v; x[2 * (s + k) + 1] += (float)v;
            }
        double pk = 0; foreach (float v in x) pk = Math.Max(pk, Math.Abs(v));
        float kp = (float)(Math.Pow(10, picoDb / 20) / pk) * g; for (int i = 0; i < x.Length; i++) x[i] *= kp;
        return x;
    }

    static void ElBajo()
    {
        Console.WriteLine("\n  1) UN BAJO SOSTENIDO 6 dB POR ENCIMA DEL TECHO\n");
        foreach (int tasa in new int[] { 44100, 48000 })
        {
            string s = ""; bool bien = true;
            foreach (double f in new double[] { 40, 60, 100, 200 })
            {
                double[] gr; float[] y = Pasa(Tono(f, Techo * 2.0, tasa, 3), tasa, out gr);
                double d = Distorsion(y, f, tasa);
                s += F(f, "0") + " Hz: " + F(d, "0.0") + " dB  ";
                if (d > -70) bien = false;
            }
            Check(bien, "a " + tasa + " Hz sale limpio (lo que no es el tono, -70 dB o menos): " + s);
        }
    }
    static void UnGolpe()
    {
        Console.WriteLine("\n  2) UN GOLPE SUELTO\n");
        // Silencio de fondo a -30 dBFS y un golpe de 10 ms que pasa 6 dB del techo.
        int tasa = 44100; float[] x = Tono(1000, 0.03, tasa, 2);
        for (int i = tasa / 2; i < tasa / 2 + 441; i++) { x[2 * i] = Techo * 2f * (float)Math.Sin(2 * Math.PI * 200 * i / tasa); x[2 * i + 1] = x[2 * i]; }
        double[] gr; Pasa(x, tasa, out gr);
        int fin = -1; for (int k = 50; k < gr.Length; k++) if (gr[k] > 0.5) fin = k;
        double ms = (fin - 51) * 10.0;
        Check(ms < 250, "tras un golpe de 10 ms que pasa 6 dB, la ganancia vuelve a menos de 0,5 dB en " + F(ms, "0") + " ms (antes, 250 ms)");
        double tope = 0; for (int k = 0; k < gr.Length; k++) tope = Math.Max(tope, gr[k]);
        Check(tope > 5.5, "y aun asi el golpe se baja lo que hace falta (" + F(tope, "0.0") + " dB)");
    }
    static void Canciones()
    {
        Console.WriteLine("\n  3) CANCIONES DE PRUEBA YA NIVELADAS (60 s)\n");
        // pico, golpes/s, semilla, subida del nivelado nuevo (nivelpicos.cs) y lo que se medio con el soltar de 120 ms.
        double[][] c = { new double[] { -1, 1, 4, 1.66, 8, 4.3 }, new double[] { -0.1, 8, 3, 1.33, 33, 14.0 }, new double[] { -0.5, 2, 1, 1.445, 17, 8.5 } };
        string[] nom = { "golpes sueltos", "golpes seguidos", "a medias" };
        bool menos = true, igual = true;
        for (int i = 0; i < c.Length; i++)
        {
            double[] gr; Pasa(Cancion(c[i][0], c[i][1], (int)c[i][2], 60, (float)c[i][3]), 44100, out gr);
            int n1 = 0; double mueve = 0;
            for (int k = 0; k < gr.Length; k++) { if (gr[k] > 1) n1++; if (k > 0) mueve += Math.Abs(gr[k] - gr[k - 1]); }
            double pct = 100.0 * n1 / gr.Length, bombeo = mueve / (gr.Length / 100.0);
            Info(nom[i].PadRight(16) + " limita (>1 dB) el " + F(pct, "0") + " % del tiempo (antes " + F(c[i][4], "0") + " %); la ganancia se mueve "
                 + F(bombeo, "0.0") + " dB/s (antes " + F(c[i][5], "0.0") + ")");
            if (!(pct <= c[i][4] + 6)) menos = false;
            if (!(bombeo <= c[i][5] * 1.06)) igual = false;
        }
        Check(menos, "en las tres limita casi el mismo rato que antes (como mucho 6 puntos mas: la espera es de 20 ms)");
        Check(igual, "y la ganancia no se mueve mas que antes (como mucho un 6 %: lo que se mueve es el bombeo)");
    }
    static void Intacto()
    {
        Console.WriteLine("\n  4) LO DE SIEMPRE\n");
        int tasa = 44100; float[] x = Tono(440, 0.5, tasa, 1);
        double[] gr; float[] y = Pasa(x, tasa, out gr);
        bool igual = true; for (int i = 0; i < x.Length; i++) if (y[i] != x[i]) { igual = false; break; }
        Check(igual, "lo que no llega al techo sale intacto, bit a bit (solo retrasado)");
        float[] z = Cancion(-0.1, 8, 3, 20, 3f);
        y = Pasa(z, tasa, out gr);
        double max = 0; foreach (float v in y) max = Math.Max(max, Math.Abs(v));
        Check(max <= Techo + 1e-6, "ninguna muestra pasa del techo, ni subiendo 9,5 dB una cancion con golpes (" + F(20 * Math.Log10(max), "0.00") + " dBFS)");
        Check((double)L.GetField("SoltarS", T).GetValue(null) == 0.10 && (double)L.GetField("EsperaS", T).GetValue(null) == 0.02,
              "los tiempos son los medidos: espera 20 ms y suelta en 100 ms");
    }
    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        L = TF.GetNestedType("LimitadorPicoReal", T);
        if (L == null || L.GetField("EsperaS", T) == null) { Check(false, "el limitador no tiene los dos tiempos de soltar"); return 1; }
        try { ElBajo(); } catch (Exception e) { Check(false, "el bajo: " + e); }
        try { UnGolpe(); } catch (Exception e) { Check(false, "un golpe: " + e); }
        try { Canciones(); } catch (Exception e) { Check(false, "canciones: " + e); }
        try { Intacto(); } catch (Exception e) { Check(false, "lo de siempre: " + e); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos == 0 ? 0 : 1;
    }
}

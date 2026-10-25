using System;
using System.Collections.Generic;
using System.Reflection;
using NAudio.Wave;

// T9 (25-10-2026): SUAVIZAR LAS "ESES" (Eses.cs).
//
// Lo que se vigila:
//   1) una voz con eses (rafagas de ruido de 4 a 9 kHz sobre un tono con armonicos): la banda de las eses baja de 3 a 6 dB en las
//      rafagas, y entre rafagas la voz sale bit a bit;
//   2) musica pareja (acordes, ruido rosa de banda ancha): casi nunca actua, y lo que baja es menos de 0,5 dB;
//   3) apagada, o con una senal sin agudos, sale bit a bit lo que entra;
//   4) da lo mismo se lea como se lea (de 1 a 7 muestras);
//   5) "sin mejoras" la deja pasar; encender y apagar en vivo no da salto; en disco continuo la siguiente sigue con la anterior;
//   6) en la cadena: la lleva siempre, apagada de fabrica, y el interruptor la enciende en vivo;
//   7) lo que cuesta.
// Son senales sinteticas: dicen que la cuenta hace lo que promete, no como suena una voz de verdad (eso se oye en el bar).
class Eses
{
    const BindingFlags T = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Type TF, TE;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    const int Fs = 44100;

    sealed class Fuente : ISampleProvider
    {
        readonly float[] d; int p; readonly WaveFormat wf;
        public Fuente(float[] x, int ch) { d = x; wf = WaveFormat.CreateIeeeFloatWaveFormat(Fs, ch); }
        public WaveFormat WaveFormat { get { return wf; } }
        public int Read(float[] b, int o, int c) { int n = Math.Min(c, d.Length - p); Array.Copy(d, p, b, o, n); p += n; return n; }
    }
    static ISampleProvider Etapa(float[] x, bool on) { return (ISampleProvider)Activator.CreateInstance(TE, T, null, new object[] { new Fuente(x, 2), on }, null); }
    static float[] Lee(ISampleProvider p, int largo, Random trozos)
    {
        float[] y = new float[largo]; int n = 0; float[] b = new float[8192];
        while (n < largo)
        {
            int pide = trozos == null ? 4096 : 1 + trozos.Next(7);
            int r = p.Read(b, 0, Math.Min(pide, largo - n)); if (r <= 0) break;
            Array.Copy(b, 0, y, n, r); n += r;
        }
        return y;
    }

    // Un paso-banda de prueba (Butterworth de 2.o orden en serie, alto y bajo) para medir la energia de 5 a 9 kHz, aparte de la etapa.
    static double[] Banda(float[] x, int canal, double desde, double hasta)
    {
        int n = x.Length / 2; double[] y = new double[n];
        double[] hp = Biquad(false, desde), lp = Biquad(true, hasta);
        double a1 = 0, a2 = 0, b1 = 0, b2 = 0, c1 = 0, c2 = 0, d1 = 0, d2 = 0;
        for (int i = 0; i < n; i++)
        {
            double v = x[2 * i + canal];
            double u = hp[0] * v + hp[1] * a1 + hp[2] * a2 - hp[3] * b1 - hp[4] * b2; a2 = a1; a1 = v; b2 = b1; b1 = u;
            double w = lp[0] * u + lp[1] * c1 + lp[2] * c2 - lp[3] * d1 - lp[4] * d2; c2 = c1; c1 = u; d2 = d1; d1 = w;
            y[i] = w;
        }
        return y;
    }
    static double[] Biquad(bool bajo, double f)
    {
        double w = 2 * Math.PI * f / Fs, al = Math.Sin(w) / (2 * 0.7071), cs = Math.Cos(w), a0 = 1 + al;
        if (bajo) return new double[] { (1 - cs) / 2 / a0, (1 - cs) / a0, (1 - cs) / 2 / a0, -2 * cs / a0, (1 - al) / a0 };
        return new double[] { (1 + cs) / 2 / a0, -(1 + cs) / a0, (1 + cs) / 2 / a0, -2 * cs / a0, (1 - al) / a0 };
    }
    static double EnergiaDb(double[] y, int desde, int largo) { double s = 0; for (int i = desde; i < desde + largo; i++) s += y[i] * y[i]; return 10 * Math.Log10(s / largo + 1e-30); }

    // La voz: un tono de 150 Hz con armonicos hasta 4 kHz a unos -20 dBFS, y cada 600 ms una "s" de 120 ms (ruido de 4 a 9 kHz).
    static float[] Voz(double seg, double sDb, int semilla)
    {
        int n = (int)(seg * Fs); float[] x = new float[2 * n]; Random r = new Random(semilla);
        double[] hp = Biquad(false, 4000), lp = Biquad(true, 9000);
        double a1 = 0, a2 = 0, b1 = 0, b2 = 0, c1 = 0, c2 = 0, d1 = 0, d2 = 0, ks = Math.Pow(10, sDb / 20) * 3.0;
        for (int i = 0; i < n; i++)
        {
            double v = 0;
            for (int k = 1; k * 150 < 4000; k++) v += Math.Sin(2 * Math.PI * 150 * k * i / Fs) / k;
            v *= 0.06;
            double ruido = r.NextDouble() * 2 - 1;
            double u = hp[0] * ruido + hp[1] * a1 + hp[2] * a2 - hp[3] * b1 - hp[4] * b2; a2 = a1; a1 = ruido; b2 = b1; b1 = u;
            double w = lp[0] * u + lp[1] * c1 + lp[2] * c2 - lp[3] * d1 - lp[4] * d2; c2 = c1; c1 = u; d2 = d1; d1 = w;
            if (EnS(i)) v += w * ks;
            x[2 * i] = (float)v; x[2 * i + 1] = (float)v;
        }
        return x;
    }
    static bool EnS(int i) { int t = i % (int)(0.6 * Fs); return t >= (int)(0.3 * Fs) && t < (int)(0.42 * Fs); }

    static void LaVoz()
    {
        Console.WriteLine("\n  1) UNA VOZ CON ESES\n");
        float[] x = Voz(6, -16, 1);
        float[] y = Lee(Etapa(x, true), x.Length, null);
        double[] bx = Banda(x, 0, 5000, 9000), by = Banda(y, 0, 5000, 9000);
        double peor = 0, mejor = 99; int s0 = (int)(0.6 * Fs);
        for (int k = 1; k < 9; k++)
        {
            int ini = k * s0 + (int)(0.31 * Fs), largo = (int)(0.1 * Fs);
            double baja = EnergiaDb(bx, ini, largo) - EnergiaDb(by, ini, largo);
            peor = Math.Max(peor, baja); mejor = Math.Min(mejor, baja);
        }
        Check(mejor >= 3.0 && peor <= 6.5, "en las eses, la banda de 5 a 9 kHz baja de " + F(mejor, "0.0") + " a " + F(peor, "0.0") + " dB");
        // Entre eses (con la suelta ya hecha), la voz sale bit a bit.
        bool igual = true; int cuenta = 0;
        for (int i = 0; i < x.Length / 2; i++)
        {
            int t = i % s0;
            if (t < (int)(0.22 * Fs) || t >= (int)(0.29 * Fs)) continue;   // de 0,4 a 0,47 s despues de cada "s": ya suelta del todo
            cuenta++;
            if (x[2 * i] != y[2 * i] || x[2 * i + 1] != y[2 * i + 1]) { igual = false; break; }
        }
        Check(igual && cuenta > 0, "entre una \"s\" y la siguiente, la voz sale bit a bit");
    }

    static float[] Rosa(double seg, double db, int semilla)
    {
        int n = (int)(seg * Fs); float[] x = new float[2 * n]; Random r = new Random(semilla);
        double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, k = Math.Pow(10, db / 20) * 0.25;
        for (int i = 0; i < n; i++)
        {
            double w = r.NextDouble() * 2 - 1;
            b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520;
            b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
            double p = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362; b6 = w * 0.115926;
            x[2 * i] = (float)(p * k); x[2 * i + 1] = (float)(p * k * 0.9);
        }
        return x;
    }
    static float[] Acordes(double seg, int semilla)
    {
        int n = (int)(seg * Fs); float[] x = new float[2 * n]; Random r = new Random(semilla);
        double[] notas = { 110.0, 164.8, 220.0, 329.6, 440.0, 659.3, 880.0, 1318.5, 1760.0, 2637.0, 3520.0 };
        for (int i = 0; i < n; i++)
        {
            double v = 0; for (int k = 0; k < notas.Length; k++) v += Math.Sin(2 * Math.PI * notas[k] * i / Fs + k) / (1 + k * 0.5);
            v = v * 0.08 + (r.NextDouble() - 0.5) * 0.01;
            x[2 * i] = (float)v; x[2 * i + 1] = (float)(v * 0.95);
        }
        return x;
    }
    static void LaMusica()
    {
        Console.WriteLine("\n  2) MUSICA PAREJA\n");
        foreach (bool rosa in new bool[] { true, false })
        {
            float[] x = rosa ? Rosa(8, -14, 2) : Acordes(8, 3);
            float[] y = Lee(Etapa(x, true), x.Length, null);
            int distintas = 0; double peor = 0;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) distintas++;
            double[] bx = Banda(x, 0, 5000, 9000), by = Banda(y, 0, 5000, 9000);
            for (int ini = Fs; ini + Fs / 10 < x.Length / 2; ini += Fs / 10) peor = Math.Max(peor, EnergiaDb(bx, ini, Fs / 10) - EnergiaDb(by, ini, Fs / 10));
            Check(peor < 0.5 && distintas < x.Length * 0.02, (rosa ? "ruido rosa de banda ancha" : "acordes hasta 3,5 kHz") + ": baja como mucho " + F(peor, "0.00") + " dB, y toca el " + F(100.0 * distintas / x.Length, "0.0") + " % de las muestras");
        }
    }

    static void BitABit()
    {
        Console.WriteLine("\n  3) APAGADA, Y SIN AGUDOS\n");
        float[] x = Voz(3, -16, 4);
        float[] y = Lee(Etapa(x, false), x.Length, null);
        bool igual = true; for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) { igual = false; break; }
        Check(igual, "apagada: sale bit a bit lo que entra");
        float[] g = new float[2 * Fs * 3]; for (int i = 0; i < g.Length / 2; i++) { float v = (float)(0.3 * Math.Sin(2 * Math.PI * 220 * i / Fs)); g[2 * i] = v; g[2 * i + 1] = v; }
        y = Lee(Etapa(g, true), g.Length, null);
        igual = true; for (int i = 0; i < g.Length; i++) if (g[i] != y[i]) { igual = false; break; }
        Check(igual, "encendida, con una senal sin agudos (220 Hz): sale bit a bit");
    }

    static void Lecturas()
    {
        Console.WriteLine("\n  4) SE LEA COMO SE LEA\n");
        float[] x = Voz(4, -16, 5);
        float[] y = Lee(Etapa(x, true), x.Length, null), z = Lee(Etapa(x, true), x.Length, new Random(9));
        bool igual = true; for (int i = 0; i < x.Length; i++) if (y[i] != z[i]) { igual = false; break; }
        Check(igual, "leida de 1 a 7 muestras a la vez da los mismos bits que en bloques");
    }

    static void EnVivo()
    {
        Console.WriteLine("\n  5) SIN MEJORAS, EN VIVO Y EN DISCO CONTINUO\n");
        float[] x = Voz(4, -16, 6);
        ISampleProvider e = Etapa(x, true); TE.GetField("Pasa", T).SetValue(e, true);
        float[] y = Lee(e, x.Length, null);
        bool igual = true; for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) { igual = false; break; }
        Check(igual, "con \"Escuchar sin mejoras\" pasa tal cual");
        // Encender y apagar en vivo: lo que sale nunca salta mas que la entrada.
        e = Etapa(x, false); MethodInfo aj = TE.GetMethod("Ajusta", T);
        float[] b = new float[Fs / 2]; float[] z = new float[x.Length]; int n = 0, vuelta = 0;
        while (n < x.Length) { int r = e.Read(b, 0, Math.Min(b.Length, x.Length - n)); if (r <= 0) break; Array.Copy(b, 0, z, n, r); n += r; vuelta++; if (vuelta == 3) aj.Invoke(e, new object[] { true }); if (vuelta == 9) aj.Invoke(e, new object[] { false }); }
        double saltoX = 0, saltoZ = 0;
        for (int i = 2; i < x.Length; i += 2) { saltoX = Math.Max(saltoX, Math.Abs(x[i] - x[i - 2])); saltoZ = Math.Max(saltoZ, Math.Abs(z[i] - z[i - 2])); }
        Check(saltoZ <= saltoX * 1.02, "encender y apagar en vivo: el mayor salto entre muestras " + F(saltoZ, "0.0000") + " (la entrada, " + F(saltoX, "0.0000") + ")");
        // Disco continuo: dos pistas; la segunda sigue con la primera y sale bit a bit como una sola etapa.
        float[] todo = Lee(Etapa(x, true), x.Length, null);
        int corte = (int)(1.37 * Fs) * 2;
        float[] p1 = new float[corte], p2 = new float[x.Length - corte];
        Array.Copy(x, 0, p1, 0, corte); Array.Copy(x, corte, p2, 0, p2.Length);
        ISampleProvider e1 = Etapa(p1, true), e2 = Etapa(p2, true);
        float[] q1 = Lee(e1, p1.Length, null);
        TE.GetMethod("Sigue", T).Invoke(e2, new object[] { e1 });
        float[] q2 = Lee(e2, p2.Length, null);
        igual = true; for (int i = 0; i < q1.Length && igual; i++) if (q1[i] != todo[i]) igual = false;
        for (int i = 0; i < q2.Length && igual; i++) if (q2[i] != todo[corte + i]) igual = false;
        Check(igual, "disco continuo: la siguiente sigue con la anterior y sale bit a bit como una sola");
    }

    static void EnLaCadena()
    {
        Console.WriteLine("\n  6) EN LA CADENA\n");
        Check(!(bool)TF.GetField("suavizaEses", T).GetValue(null), "apagada de fabrica");
        Check(Codigo.Llamadas(TF.GetMethod("ArmaCadena", T)).Exists(delegate(string s) { return s.StartsWith("SuavizaEses"); }), "ArmaCadena la pone en la cadena");
        Check(Codigo.Llama(TF.GetMethod("AplicaBocinas", T), "SuavizaEses.Ajusta"), "y el interruptor llega en vivo a la que suena (AplicaBocinas)");
        Check(Codigo.Llama(TF.GetMethod("SigueA", T), "SuavizaEses.Sigue"), "y en disco continuo sigue con la anterior (SigueA)");
    }

    static void LoQueCuesta()
    {
        Console.WriteLine("\n  7) LO QUE CUESTA\n");
        float[] x = Voz(20, -16, 7);
        double mejor = double.MaxValue;
        for (int vez = 0; vez < 3; vez++)
        {
            ISampleProvider e = Etapa(x, true); float[] b = new float[4096];
            System.Diagnostics.Stopwatch w = System.Diagnostics.Stopwatch.StartNew();
            while (e.Read(b, 0, b.Length) > 0) { }
            mejor = Math.Min(mejor, w.Elapsed.TotalMilliseconds);
        }
        double ns = mejor * 1e6 / (x.Length / 2.0);
        Console.WriteLine("        encendida: " + F(ns, "0.0") + " ns por cuadro estereo (Mono, esta maquina; apagada no se calcula)");
        Check(ns < 200, "cuesta menos de 200 ns por cuadro");
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = asm.GetType("Rocola.Form1", true); TE = asm.GetType("Rocola.SuavizaEses", false);
        if (TE == null) { Check(false, "no esta Rocola.SuavizaEses"); return 1; }
        try { LaVoz(); } catch (Exception e) { Check(false, "la voz: " + e); }
        try { LaMusica(); } catch (Exception e) { Check(false, "la musica: " + e); }
        try { BitABit(); } catch (Exception e) { Check(false, "bit a bit: " + e); }
        try { Lecturas(); } catch (Exception e) { Check(false, "lecturas: " + e); }
        try { EnVivo(); } catch (Exception e) { Check(false, "en vivo: " + e); }
        try { EnLaCadena(); } catch (Exception e) { Check(false, "en la cadena: " + e); }
        try { LoQueCuesta(); } catch (Exception e) { Check(false, "lo que cuesta: " + e); }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN (" + oks + " comprobaciones)" : "\n  FALLOS: " + fallos);
        return fallos;
    }
}

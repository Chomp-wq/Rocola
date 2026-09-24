using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// PICOS REALES ENTRE MUESTRAS: QUE LO QUE RECONSTRUYE EL DAC NO PASE DEL TECHO.
//
// El limitador de siempre (LookaheadLimiter) solo miraba las muestras. El nuevo (LimitadorPicoReal, en
// Rocola/PicoReal.cs) mira la onda reconstruida con un detector 4x de 24 coeficientes por fase y una
// parabola. Esta bateria comprueba:
//
//   1) el detector: su filtro, casos al estilo de EBU Tech 3341 (tonos de un cuarto, un sexto y un octavo
//      de la frecuencia de muestreo con la fase peor) y contra una referencia INDEPENDIENTE de mayor
//      precision (pruebas/ref_picoreal.py: interpolacion de banda limitada exacta, 32x);
//   2) el limitador contra esa referencia: la onda de salida no pasa de 0 dBTP y el margen que queda,
//      MEDIDO (no supuesto), a 22,05/44,1/48/96 kHz, mono, estereo y seis canales; y lo que dejaba pasar
//      el limitador viejo con las mismas senales;
//   3) lo que no pasa del techo sale intacto (solo retrasado);
//   4) cola, silencio, archivos cortos, saltos bruscos (como los de un salto de posicion) y final de cancion;
//      lecturas que parten un cuadro;
//   5) la misma ganancia para todos los canales (la imagen estereo no se mueve) y sin distorsion ni bombeo;
//   6) la cadena de verdad (nivelado, realce y grave sintetico) con musica sintetica fuerte;
//   7) el cableado: es el ultimo paso, tambien despues del remuestreo en la salida moderna;
//   8) lo que cuesta, frente al limitador viejo.
//
// Lo que NO prueba: como suena (escucha A/B a volumen igualado) ni el DAC de verdad.
class FuenteP : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos, k; private readonly int[] trozos;
    public FuenteP(float[] datos, int fs, int ch, int[] trozos) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); this.trozos = trozos; }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    {
        if (trozos != null && trozos.Length > 0) c = Math.Min(c, trozos[k++ % trozos.Length]);
        int n = Math.Min(c, d.Length - pos);
        if (n <= 0) return 0;
        Array.Copy(d, pos, b, o, n); pos += n; return n;
    }
}

class PicoReal
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TDet, TLim, TViejo;
    static float techo;
    static int fallos, oks;
    static string dir;
    static bool hayPython;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static double Db(double x) { return 20 * Math.Log10(Math.Max(1e-12, x)); }

    // ---------------------------------------------------------------------------------------------
    //  Herramientas
    // ---------------------------------------------------------------------------------------------
    static object Crea(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }
    static int Retardo(object lim) { return (int)lim.GetType().GetProperty("DelayFrames", T).GetValue(lim, null); }
    // Pasa una senal por un limitador y devuelve TODA la salida (con su retardo y su cola).
    static float[] Limita(Type t, float[] x, int fs, int ch, int[] trozos, int bloque, out int retardo)
    {
        object lim = Crea(t, new FuenteP(x, fs, ch, trozos), t == TLim ? techo : 0.98f, 3.0);
        retardo = Retardo(lim);
        ISampleProvider p = (ISampleProvider)lim;
        List<float> r = new List<float>(x.Length + retardo * ch);
        float[] buf = new float[bloque];
        int n;
        while ((n = p.Read(buf, 0, buf.Length)) > 0) { for (int i = 0; i < n; i++) r.Add(buf[i]); if (r.Count > x.Length + retardo * ch + 100000) break; }
        return r.ToArray();
    }
    static float[] Limita(Type t, float[] x, int fs, int ch) { int d; return Limita(t, x, fs, ch, null, 4096 * ch, out d); }

    static string Python(string args, out int codigo)
    {
        codigo = -1;
        string py = Environment.GetEnvironmentVariable("ROCOLA_TEST_PYTHON");
        if (string.IsNullOrEmpty(py)) py = "python3";
        try
        {
            ProcessStartInfo si = new ProcessStartInfo(py, args);
            si.UseShellExecute = false; si.RedirectStandardOutput = true; si.RedirectStandardError = true; si.CreateNoWindow = true;
            using (Process p = Process.Start(si))
            {
                string o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
                p.WaitForExit(); codigo = p.ExitCode; return o;
            }
        }
        catch { return null; }
    }
    // Pico real de la referencia independiente (NaN si no hay Python).
    static double Referencia(float[] x, int fs, int ch)
    {
        if (!hayPython) return double.NaN;
        string ruta = Path.Combine(dir, "pico.wav");
        using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, ch))) w.WriteSamples(x, 0, x.Length);
        int cod; string r = Python("ref_picoreal.py \"" + ruta + "\"", out cod);
        double v = double.NaN;
        if (r != null && r.StartsWith("PICO ")) double.TryParse(r.Substring(5).Split(' ')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
        return v;
    }
    // Lo que lee el detector de la rocola en una senal mono (umbral casi cero: calcula siempre la rejilla).
    delegate float Empuja(float x);
    static double Detector(float[] x, int ch, int canal)
    {
        object d = Crea(TDet, 1e-12f);
        Empuja e = (Empuja)Delegate.CreateDelegate(typeof(Empuja), d, TDet.GetMethod("Empuja", T));
        float m = 0;
        for (int i = canal; i < x.Length; i += ch) { float v = e(x[i]); if (v > m) m = v; }
        for (int i = 0; i < 64; i++) { float v = e(0f); if (v > m) m = v; }
        return m;
    }

    // ---------------------------------------------------------------------------------------------
    //  Senales (todas empiezan y acaban con fundido salvo que se diga: un corte en seco TAMBIEN tiene
    //  picos entre muestras, y eso se prueba aparte, con los saltos)
    // ---------------------------------------------------------------------------------------------
    static double Fundido(long i, long n, int fs)
    {
        long f = fs / 100;   // 10 ms
        if (i < f) return 0.5 - 0.5 * Math.Cos(Math.PI * i / f);
        if (i >= n - f) return 0.5 - 0.5 * Math.Cos(Math.PI * (n - 1 - i) / f);
        return 1;
    }
    static float[] Seno(int fs, int ch, double f, double amp, double fase, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        for (long i = 0; i < n; i++)
        {
            float v = (float)(amp * Fundido(i, n, fs) * Math.Sin(2 * Math.PI * f * i / fs + fase));
            for (int c = 0; c < ch; c++) x[i * ch + c] = v;
        }
        return x;
    }
    // Suma de armonicos hasta 0,4535 fs (20 kHz a 44,1): cuadrada o sierra de banda limitada (Gibbs).
    static float[] Armonicos(int fs, int ch, double f0, double amp, bool cuadrada, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        int hmax = (int)(0.4535 * fs / f0);
        for (long i = 0; i < n; i++)
        {
            double v = 0;
            for (int h = 1; h <= hmax; h++) { if (cuadrada && (h % 2) == 0) continue; v += Math.Sin(2 * Math.PI * h * f0 * i / fs + 0.3 * h) / h; }
            v *= amp * Fundido(i, n, fs);
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)(c == 1 ? -v : v);
        }
        return x;
    }
    // Impulsos de banda limitada en posiciones fraccionarias, uno cada 1000 muestras.
    static float[] Impulsos(int fs, int ch, double amp, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        Random r = new Random(5);
        for (long c0 = 600; c0 + 600 < n; c0 += 1000)
        {
            double centro = c0 + r.NextDouble(); double fc = 0.40 + 0.05 * r.NextDouble();
            for (long i = c0 - 500; i < c0 + 500; i++)
            {
                double t = i - centro, w = 0.5 + 0.5 * Math.Cos(Math.PI * t / 500);
                double s = Math.Abs(t) < 1e-9 ? 1 : Math.Sin(2 * Math.PI * fc * t) / (2 * Math.PI * fc * t);
                for (int c = 0; c < ch; c++) x[i * ch + c] += (float)(amp * w * s);
            }
        }
        return x;
    }
    // "Musica" sintetica: bajo, acordes y platillos (ruido filtrado), sumados sin limite de banda cuidado.
    static float[] Musica(int fs, int ch, double amp, double seg, uint semilla)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        uint s = semilla; double hp = 0, prev = 0;
        for (long i = 0; i < n; i++)
        {
            double t = (double)i / fs;
            double v = 0.5 * Math.Sin(2 * Math.PI * 55 * t) + 0.3 * Math.Sin(2 * Math.PI * 220 * t + Math.Sin(3 * t))
                     + 0.2 * Math.Sin(2 * Math.PI * 330 * t) + 0.15 * Math.Sin(2 * Math.PI * 1760 * t);
            s = s * 1664525u + 1013904223u; double ru = ((s >> 8) / 16777216.0) - 0.5;
            hp = 0.9 * (hp + ru - prev); prev = ru;             // ruido agudo: platillos
            double golpe = ((i % (fs / 2)) < fs / 50) ? Math.Exp(-(i % (fs / 2)) / (fs * 0.004)) : 0;
            v += 0.6 * hp * (0.3 + golpe);
            v *= amp * Fundido(i, n, fs);
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)(c == 1 ? v * 0.8 + 0.1 * hp : v);
        }
        return x;
    }
    // Musica "tipo MP3": lo mismo, pero todo por debajo de un corte (16 o 19 kHz), como sale un MP3 o un AAC:
    // el codificador quita lo de arriba. Los platillos son una nube de 120 tonos al azar entre 4 kHz y el corte.
    static float[] MusicaMp3(int fs, int ch, double amp, double seg, double corte, uint semilla)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        Random r = new Random((int)semilla);
        int nt = 120; double[] fr = new double[nt], fa = new double[nt];
        for (int k = 0; k < nt; k++) { fr[k] = 4000 + r.NextDouble() * (corte - 4000); fa[k] = r.NextDouble() * 6.28; }
        double[] w = new double[nt]; double[] fase = new double[nt];
        for (int k = 0; k < nt; k++) { w[k] = 2 * Math.PI * fr[k] / fs; fase[k] = fa[k]; }
        for (long i = 0; i < n; i++)
        {
            double t = (double)i / fs;
            double v = 0.5 * Math.Sin(2 * Math.PI * 55 * t) + 0.3 * Math.Sin(2 * Math.PI * 220 * t + Math.Sin(3 * t))
                     + 0.2 * Math.Sin(2 * Math.PI * 330 * t) + 0.15 * Math.Sin(2 * Math.PI * 1760 * t);
            double pl = 0;
            for (int k = 0; k < nt; k++) pl += Math.Sin(w[k] * i + fase[k]);
            double golpe = ((i % (fs / 2)) < fs / 50) ? Math.Exp(-(i % (fs / 2)) / (fs * 0.004)) : 0;
            v += 0.05 * pl * (0.3 + golpe);
            v *= amp * Fundido(i, n, fs);
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)(c == 1 ? v * 0.8 : v);
        }
        return x;
    }
    // Saltos bruscos de un extremo al otro (lo que deja un salto de posicion a media cancion).
    static float[] Saltos(int fs, int ch, double amp, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        for (long i = 0; i < n; i++)
        {
            double v = ((i / 777) % 2 == 0 ? amp : -amp) * (0.7 + 0.3 * Math.Sin(2 * Math.PI * 3000 * i / fs));
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)v;
        }
        return x;
    }
    static float Pico(float[] x) { float m = 0; foreach (float v in x) { float a = Math.Abs(v); if (a > m) m = a; } return m; }

    // ---------------------------------------------------------------------------------------------
    //  1) EL DETECTOR
    // ---------------------------------------------------------------------------------------------
    static void ElDetector()
    {
        Console.WriteLine("\n  1) EL DETECTOR: FILTRO, CASOS DE LIBRO Y REFERENCIA\n");
        double[][] fases = (double[][])TDet.GetField("Fases", T).GetValue(null);
        int taps = (int)TDet.GetField("Taps", T).GetValue(null);
        bool bien = fases.Length == 4 && fases[0].Length == taps + 1;
        for (int i = 0; bien && i < fases[0].Length; i++) if (Math.Abs(fases[0][i] - (i == taps / 2 ? 1.0 : 0.0)) > 1e-12) bien = false;
        double espejo = 0, simetria = 0;
        for (int i = 0; i < taps; i++) { espejo = Math.Max(espejo, Math.Abs(fases[3][i] - fases[1][taps - 1 - i])); simetria = Math.Max(simetria, Math.Abs(fases[2][i] - fases[2][taps - 1 - i])); }
        Check(bien, "la fase 0 es la propia muestra: la rejilla pasa por las muestras originales");
        Check(espejo < 1e-12 && simetria < 1e-12, "fases de 1/4 y 3/4 en espejo y la de 1/2 simetrica (por eso bastan 36 multiplicaciones por muestra)");
        float g = (float)TDet.GetField("GananciaMaxima", T).GetValue(null);
        Check(Math.Abs(g - 2.2988f) < 0.001f, "la mayor ganancia de una fase es " + F(g, "0.000") + ": por debajo de 0,98/" + F(g, "0.00") + " = " + F(0.98 / g, "0.000") + " no hace falta calcular");
        // El bucle por bloques de la rocola hace la misma cuenta que el de muestra a muestra.
        {
            float[] x = Musica(44100, 1, 1.2, 0.5, 4);
            object d1 = Crea(TDet, 0.98f), d2 = Crea(TDet, 0.98f);
            Empuja e = (Empuja)Delegate.CreateDelegate(typeof(Empuja), d1, TDet.GetMethod("Empuja", T));
            float[] uno = new float[x.Length], bloque = new float[x.Length];
            for (int i = 0; i < x.Length; i++) uno[i] = e(x[i]);
            MethodInfo mb = TDet.GetMethod("Bloque", T);
            float[] tmp = new float[1000];
            for (int i = 0; i < x.Length; i += 1000)
            {
                int n = Math.Min(1000, x.Length - i);
                mb.Invoke(d2, new object[] { x, i, 1, n, tmp, true });
                Array.Copy(tmp, 0, bloque, i, n);
            }
            bool igual = true;
            for (int i = 0; i < x.Length; i++) if (uno[i] != bloque[i]) { igual = false; break; }
            Check(igual, "el detector por bloques da lo mismo que muestra a muestra, bit a bit");
        }
        // Casos al estilo de EBU Tech 3341 (15 a 19): tono de fs/4, fs/6 y fs/8 con la fase peor. La verdad es
        // analitica (la amplitud del tono), asi que no depende de ninguna implementacion.
        double[][] casos = { new double[] { 4, 0.5, 0 }, new double[] { 4, 0.5, 45 }, new double[] { 6, 0.5, 60 }, new double[] { 8, 0.5, 67.5 }, new double[] { 4, 1.41, 45 } };
        string linea = ""; bool enTol = true;
        foreach (double[] c in casos)
        {
            float[] x = Seno(48000, 1, 48000 / c[0], c[1], c[2] * Math.PI / 180, 1.0);
            double d = Db(Detector(x, 1, 0)) - Db(c[1]);
            linea += "fs/" + c[0] + " " + F(c[2], "0.#") + "o: " + F(d, "+0.000;-0.000") + "  ";
            if (d > 0.2 || d < -0.4) enTol = false;
        }
        Info("detector menos la verdad, en dB: " + linea);
        Check(enTol, "los cinco dentro de la tolerancia de EBU Tech 3341 para pico real (+0,2 / -0,4 dB)");
        // El caso que el limitador viejo no ve: muestras en 0,707 y onda en 1.
        float[] clave = Seno(44100, 1, 44100 / 4.0, 1.0, Math.PI / 4, 1.0);
        double lee = Detector(clave, 1, 0);
        Check(Pico(clave) < 0.71f && Math.Abs(Db(lee)) < 0.05, "tono de fs/4 desfasado 45 grados: muestras en " + F(Pico(clave), "0.000") + ", el detector lee " + F(lee, "0.000"));
        if (!hayPython) { Check(true, "(sin Python con numpy: la comparacion con la referencia independiente se salta y se dice)"); return; }
        // Contra la referencia, con senales limitadas a 20 kHz (a 44,1) y a 21,8 kHz (a 48).
        double peor = 0, demas = 0; string cual = "", cualMas = "";
        foreach (int fs in new int[] { 44100, 48000 })
        {
            List<KeyValuePair<string, float[]>> ss = new List<KeyValuePair<string, float[]>>();
            ss.Add(new KeyValuePair<string, float[]>("cuadrada 1 kHz", Armonicos(fs, 1, 1000, 0.5, true, 0.3)));
            ss.Add(new KeyValuePair<string, float[]>("sierra 3,1 kHz", Armonicos(fs, 1, 3100, 0.5, false, 0.3)));
            ss.Add(new KeyValuePair<string, float[]>("impulsos fraccionarios", Impulsos(fs, 1, 0.8, 0.5)));
            ss.Add(new KeyValuePair<string, float[]>("tono de 19 kHz", Seno(fs, 1, 19000, 0.8, 0.77, 0.3)));
            ss.Add(new KeyValuePair<string, float[]>("tono de 15 kHz", Seno(fs, 1, 15000, 0.8, 2.1, 0.3)));
            foreach (KeyValuePair<string, float[]> s in ss)
            {
                double v = Referencia(s.Value, fs, 1), d = Db(Detector(s.Value, 1, 0)) - Db(v);
                if (d < peor) { peor = d; cual = s.Key + " a " + fs; }
                if (d > demas) { demas = d; cualMas = s.Key + " a " + fs; }
            }
        }
        Check(peor > -0.12, "contra la referencia (32x), en senales de banda limitada, el detector se queda corto como mucho " + F(-peor, "0.000") + " dB (" + cual + ")");
        Check(demas < 0.2, "y lee de mas como mucho " + F(demas, "0.000") + " dB (" + cualMas + "): eso solo limita un poco antes, nunca deja pasar");
    }

    // ---------------------------------------------------------------------------------------------
    //  2) EL LIMITADOR CONTRA LA REFERENCIA
    // ---------------------------------------------------------------------------------------------
    static void ContraLaReferencia()
    {
        Console.WriteLine("\n  2) LA ONDA QUE SALE, MEDIDA CON LA REFERENCIA (techo operativo " + F(Db(techo), "0.000") + " dB)\n");
        if (!hayPython) { Check(true, "(sin Python con numpy: la medida de la onda de salida se salta y se dice)"); return; }
        double peorNuevo = -99, peorViejo = -99; string cualNuevo = "", cualViejo = "";
        double peorNyq = -99; string cualNyq = "";
        foreach (int fs in new int[] { 22050, 44100, 48000, 96000 })
            foreach (int ch in new int[] { 1, 2, 6 })
            {
                if (ch == 6 && fs != 48000) continue;
                List<KeyValuePair<string, float[]>> ss = new List<KeyValuePair<string, float[]>>();
                ss.Add(new KeyValuePair<string, float[]>("fs/4 a 45 grados (+3 dB entre muestras)", Seno(fs, ch, fs / 4.0, 1.25, Math.PI / 4, 0.4)));
                ss.Add(new KeyValuePair<string, float[]>("cuadrada 1 kHz fuerte", Armonicos(fs, ch, 1000, 1.2, true, 0.4)));
                ss.Add(new KeyValuePair<string, float[]>("sierra 2,7 kHz fuerte", Armonicos(fs, ch, 2700, 1.3, false, 0.4)));
                ss.Add(new KeyValuePair<string, float[]>("impulsos a 1,5", Impulsos(fs, ch, 1.5, 0.4)));
                ss.Add(new KeyValuePair<string, float[]>("tono de 0,43 fs", Seno(fs, ch, 0.43 * fs, 1.1, 1.0, 0.4)));
                foreach (KeyValuePair<string, float[]> s in ss)
                {
                    double tpN = Db(Referencia(Limita(TLim, s.Value, fs, ch), fs, ch));
                    double tpV = Db(Referencia(Limita(TViejo, s.Value, fs, ch), fs, ch));
                    if (tpN > peorNuevo) { peorNuevo = tpN; cualNuevo = s.Key + ", " + fs + " Hz, " + ch + " can."; }
                    if (tpV > peorViejo) { peorViejo = tpV; cualViejo = s.Key + ", " + fs + " Hz, " + ch + " can."; }
                }
                // Contenido hasta Nyquist (saltos en seco y musica recortada): se mide aparte.
                float[][] extremos = { Saltos(fs, ch, 0.95, 0.3), Musica(fs, ch, 2.5, 0.6, 7) };
                string[] nombres = { "saltos en seco", "ruido sin filtrar a +8 dB" };
                for (int k = 0; k < extremos.Length; k++)
                {
                    double tp = Db(Referencia(Limita(TLim, extremos[k], fs, ch), fs, ch));
                    if (tp > peorNyq) { peorNyq = tp; cualNyq = nombres[k] + ", " + fs + " Hz, " + ch + " can."; }
                }
            }
        Info("limitador VIEJO (solo muestras): la onda llega hasta " + F(peorViejo, "+0.000;-0.000") + " dBTP (" + cualViejo + ")");
        Info("limitador NUEVO: hasta " + F(peorNuevo, "+0.000;-0.000") + " dBTP (" + cualNuevo + ")");
        Info("con contenido hasta Nyquist (saltos en seco y ruido sin filtrar): hasta " + F(peorNyq, "+0.000;-0.000") + " dBTP (" + cualNyq + ")");
        Check(peorViejo > 0.5, "el limitador viejo SI dejaba pasar la onda por encima de 0 dBTP con estas senales");
        Check(peorNuevo <= 0.0, "con el nuevo, en senales de banda limitada, la onda no pasa de 0 dBTP a ninguna frecuencia de muestreo ni con 1, 2 o 6 canales");
        Check(peorNuevo <= Db(techo) + 0.12, "y queda dentro del techo mas el error medido del detector: margen medido " + F(-peorNuevo, "0.000") + " dB hasta 0 dBTP");
        // El margen cubre estos casos, no constituye una garantia para cualquier senal/DAC.
        Check(peorNyq <= 0.0 && peorNyq < peorViejo - 1.0, "con contenido fuerte hasta Nyquist (saltos en seco, ruido sin filtrar) queda bajo 0 dBTP en esta bateria: "
              + F(peorNyq, "+0.000;-0.000") + " dBTP, frente a " + F(peorViejo, "+0.000;-0.000") + " del viejo");
    }

    // ---------------------------------------------------------------------------------------------
    //  3) LO QUE NO PASA DEL TECHO SALE INTACTO
    // ---------------------------------------------------------------------------------------------
    static void Intacto()
    {
        Console.WriteLine("\n  3) LO QUE NO PASA DEL TECHO SALE INTACTO (solo retrasado)\n");
        foreach (int ch in new int[] { 1, 2 })
        {
            float[] x = Musica(44100, ch, 0.35, 1.0, 3);   // picos reales muy por debajo de 0,98
            int ret; float[] y = Limita(TLim, x, 44100, ch, null, 4096 * ch, out ret);
            bool igual = y.Length == x.Length + ret * ch;
            for (int i = 0; igual && i < ret * ch; i++) if (y[i] != 0f) igual = false;
            for (int i = 0; igual && i < x.Length; i++) if (y[ret * ch + i] != x[i]) igual = false;
            Check(igual, (ch == 1 ? "mono" : "estereo") + ": salida bit a bit igual a la entrada, retrasada " + ret + " cuadros (" + F(ret * 1000.0 / 44100, "0.00") + " ms)");
        }
    }

    // ---------------------------------------------------------------------------------------------
    //  4) COLA, SILENCIO, CORTOS, SALTOS, FINAL Y LECTURAS PARTIDAS
    // ---------------------------------------------------------------------------------------------
    static bool Finitos(float[] y) { foreach (float v in y) if (float.IsNaN(v) || float.IsInfinity(v)) return false; return true; }
    static void Bordes()
    {
        Console.WriteLine("\n  4) COLA, SILENCIO, ARCHIVOS CORTOS, SALTOS, FINAL Y LECTURAS PARTIDAS\n");
        int fs = 44100, ch = 2, ret;
        float[] sil = new float[fs * ch];
        float[] y = Limita(TLim, sil, fs, ch, null, 4096, out ret);
        bool ceros = y.Length == sil.Length + ret * ch; foreach (float v in y) if (v != 0f) ceros = false;
        Check(ceros, "silencio: sale silencio exacto, con su cola, sin inventar nada");
        y = Limita(TLim, new float[0], fs, ch, null, 4096, out ret);
        Check(y.Length == 0, "fuente vacia: no hay cola");
        foreach (int largo in new int[] { 1, 7, 100, 200 })
        {
            float[] c = new float[largo * ch];
            for (int i = 0; i < c.Length; i++) c[i] = (float)(1.6 * Math.Sin(i * 0.9));
            y = Limita(TLim, c, fs, ch, null, 514, out ret);
            Check(y.Length == c.Length + ret * ch && Finitos(y) && Pico(y) <= techo + 1e-6f,
                  "archivo de " + largo + " cuadros (menos que la espera del limitador): sale entero, con su cola, y dentro del techo");
        }
        // Final de cancion a todo volumen: el ultimo pico tambien se limita al vaciar.
        float[] fin = Seno(fs, ch, 1000, 1.5, 0, 0.2);
        for (int i = fin.Length - 20 * ch; i < fin.Length; i++) fin[i] = (i % 2 == 0) ? 1.9f : -1.9f;
        y = Limita(TLim, fin, fs, ch, null, 4096, out ret);
        Check(y.Length == fin.Length + ret * ch && Pico(y) <= techo + 1e-6f, "un final a todo volumen se limita tambien mientras se vacia la cola");
        // Saltos bruscos: sin NaN, dentro del techo de muestras.
        y = Limita(TLim, Saltos(fs, ch, 1.4, 0.5), fs, ch);
        Check(Finitos(y) && Pico(y) <= techo + 1e-6f, "saltos bruscos de +1,4 a -1,4: todo finito y dentro del techo");
        // Lecturas partidas: el origen entrega trozos que parten cuadros; la salida es la misma bit a bit.
        float[] m = Musica(fs, 6, 1.5, 0.5, 9);
        float[] a = Limita(TLim, m, fs, 6, null, 4096 * 6, out ret);
        float[] b = Limita(TLim, m, fs, 6, new int[] { 1, 5, 7, 3000, 11, 2 }, 4096 * 6, out ret);
        float[] c2 = Limita(TLim, m, fs, 6, null, 6 * 37, out ret);
        bool igual = a.Length == b.Length && a.Length == c2.Length;
        for (int i = 0; igual && i < a.Length; i++) if (a[i] != b[i] || a[i] != c2[i]) igual = false;
        Check(igual, "seis canales con un origen que parte cuadros (1, 5, 7... muestras) y lecturas chicas: la misma salida bit a bit");
    }

    // ---------------------------------------------------------------------------------------------
    //  5) CANALES ENLAZADOS, SIN DISTORSION NI BOMBEO
    // ---------------------------------------------------------------------------------------------
    static double Thd(float[] x, int ini, int ch, double hz, int fs)
    {
        int n = 0; double sr = 0, si = 0, tot = 0;
        for (int i = ini; i < x.Length; i += ch)
        {
            double t = (double)n / fs, v = x[i];
            sr += v * Math.Cos(2 * Math.PI * hz * t); si += v * Math.Sin(2 * Math.PI * hz * t); tot += v * v; n++;
        }
        double ef = 2.0 * (sr * sr + si * si) / n;
        return Math.Sqrt(Math.Max(0, tot - ef) / tot);
    }
    static void Enlazados()
    {
        Console.WriteLine("\n  5) LA MISMA GANANCIA PARA TODOS LOS CANALES, SIN DISTORSION NI BOMBEO\n");
        int fs = 44100, ret;
        // Izquierdo fuerte y derecho flojo: la ganancia tiene que ser la misma en los dos, cuadro a cuadro.
        float[] x = new float[fs * 2];
        for (int i = 0; i < fs; i++)
        {
            double t = (double)i / fs;
            x[2 * i] = (float)(1.6 * Math.Sin(2 * Math.PI * 440 * t) * Fundido(i, fs, fs));
            x[2 * i + 1] = (float)(0.05 * Math.Sin(2 * Math.PI * 330 * t) * Fundido(i, fs, fs));
        }
        float[] y = Limita(TLim, x, fs, 2, null, 8192, out ret);
        double peor = 0; int vistos = 0;
        for (int i = 0; i < fs; i++)
        {
            double inL = x[2 * i], inR = x[2 * i + 1], oL = y[2 * (i + ret)], oR = y[2 * (i + ret) + 1];
            if (Math.Abs(inL) < 1e-3 || Math.Abs(inR) < 1e-3) continue;
            peor = Math.Max(peor, Math.Abs(oL / inL - oR / inR)); vistos++;
        }
        Check(vistos > 1000 && peor < 1e-6, "izquierdo fuerte y derecho flojo: la misma ganancia en los dos canales en " + vistos + " cuadros (diferencia " + peor.ToString("0.0e0") + "): la imagen no se mueve");
        // Tonos sostenidos por encima del techo: se bajan, no se recortan.
        foreach (double amp in new double[] { 1.2, 2.0, 3.5 })
        {
            float[] s = Seno(fs, 2, 200, amp, 0, 2.0);
            float[] o = Limita(TLim, s, fs, 2);
            // Solo la zona estable: de 0,8 s a 1,8 s (lejos del fundido de entrada, del de salida y de la cola).
            float[] zona = new float[fs * 2];
            Array.Copy(o, (int)(0.8 * fs) * 2, zona, 0, zona.Length);
            double thd = Thd(zona, 0, 2, 200, fs);
            Check(thd < 0.005, "tono de 200 Hz a " + F(amp, "0.0") + ": sale limpio (" + F(thd * 100, "0.000") + " % de distorsion)");
        }
        // Bombeo: con un tono fuerte y estable, la ganancia no oscila una vez asentada.
        float[] est = Seno(fs, 1, 1000, 1.8, 0, 2.0);
        float[] oe = Limita(TLim, est, fs, 1, null, 4096, out ret);
        double gmin = 9, gmax = 0;
        for (int i = fs / 2; i < fs * 3 / 2; i++)
        {
            double inv = est[i]; if (Math.Abs(inv) < 0.3) continue;
            double g = oe[i + ret] / inv; gmin = Math.Min(gmin, g); gmax = Math.Max(gmax, g);
        }
        Check(gmax - gmin < 1e-3 * gmax, "un tono fuerte y estable: la ganancia asentada no se mueve (varia " + F(Db(gmax / gmin), "0.0000") + " dB): no bombea");
    }

    // ---------------------------------------------------------------------------------------------
    //  6) LA CADENA DE VERDAD
    // ---------------------------------------------------------------------------------------------
    static float[] Cadena(Type tLim, float[] x, int fs, int ch)
    {
        object owner = Codigo.FormSinEncender(TF);
        TF.GetField("auroraAmount", T).SetValue(owner, 0f);
        ISampleProvider cad = new FuenteP(x, fs, ch, null);
        object tap = Crea(TF.GetNestedType("LevelTap", T), cad, owner);
        Type tt = tap.GetType();
        tt.GetField("Gain").SetValue(tap, 1.6f); tt.GetField("Master").SetValue(tap, 1.2f); tt.GetField("Excite").SetValue(tap, 0.6f);
        cad = (ISampleProvider)tap;
        object bajo = Crea(TF.GetNestedType("DeepBassProvider", T), cad, 0.8f);
        cad = (ISampleProvider)bajo;
        object lim = Crea(tLim, cad, tLim == TLim ? techo : 0.98f, 3.0);
        ISampleProvider p = (ISampleProvider)lim;
        List<float> r = new List<float>();
        float[] buf = new float[4096 * ch]; int n;
        while ((n = p.Read(buf, 0, buf.Length)) > 0) for (int i = 0; i < n; i++) r.Add(buf[i]);
        return r.ToArray();
    }
    static void LaCadena()
    {
        Console.WriteLine("\n  6) LA CADENA DE VERDAD: NIVELADO, REALCE Y GRAVE SINTETICO, CON MUSICA FUERTE\n");
        if (!hayPython) { Check(true, "(sin Python con numpy: se salta y se dice)"); return; }
        double peorN = -99, peorV = -99, extN = -99, extV = -99;
        foreach (int fs in new int[] { 44100, 48000 })
            foreach (double corte in new double[] { 16000, 19000 })
            {
                float[] m = MusicaMp3(fs, 2, 0.9, 1.5, corte, 21);
                peorN = Math.Max(peorN, Db(Referencia(Cadena(TLim, m, fs, 2), fs, 2)));
                peorV = Math.Max(peorV, Db(Referencia(Cadena(TViejo, m, fs, 2), fs, 2)));
            }
        float[] ext = Musica(44100, 2, 0.9, 1.5, 21);
        extN = Db(Referencia(Cadena(TLim, ext, 44100, 2), 44100, 2));
        extV = Db(Referencia(Cadena(TViejo, ext, 44100, 2), 44100, 2));
        Info("musica tipo MP3 (hasta 16 y 19 kHz) con nivelado +4 dB, realce al 60 % y grave al 80 %: viejo " + F(peorV, "+0.000;-0.000") + " dBTP, nuevo " + F(peorN, "+0.000;-0.000") + " dBTP");
        Info("la misma con platillos de ruido blanco hasta Nyquist (extremo, no es un MP3): viejo " + F(extV, "+0.000;-0.000") + " dBTP, nuevo " + F(extN, "+0.000;-0.000") + " dBTP");
        Check(peorN <= 0.0, "con la cadena de verdad y musica tipo MP3, la onda que sale no pasa de 0 dBTP (margen medido " + F(-peorN, "0.000") + " dB)");
        Check(extN < extV - 0.5 && extN <= 0.0, "y en el extremo con ruido hasta Nyquist, bajo 0 dBTP en esta bateria");
    }

    // ---------------------------------------------------------------------------------------------
    //  7) EL CABLEADO
    // ---------------------------------------------------------------------------------------------
    static void Cableado()
    {
        Console.WriteLine("\n  7) ES EL ULTIMO PASO DE LA CADENA\n");
        // La cadena de cada cancion se arma en ArmaCadena: la usan el arranque de siempre (StartOpenedTrack) y el
        // relevo sin pausa, asi que las dos llevan el mismo limitador.
        List<string> arranca = Codigo.ConAyudantes(TF, "ArmaCadena"), empieza = Codigo.ConAyudantes(TF, "StartOpenedTrack");
        Check(Codigo.Llama(empieza, "ArmaCadena") && Codigo.Llama(arranca, "LimitadorPicoReal..ctor")
              && !Codigo.Llama(arranca, "LookaheadLimiter..ctor") && !Codigo.Llama(empieza, "LookaheadLimiter..ctor"),
              "la cancion arranca con el limitador de pico real (y ya no con el de muestras)");
        MethodInfo wasapi = TF.GetMethod("PreparaSalidaWasapi", T);
        if (wasapi != null)
            Check(Codigo.Llama(wasapi, "LimitadorPicoReal..ctor") && !Codigo.Llama(wasapi, "LookaheadLimiter..ctor"),
                  "salida moderna: despues de remuestrear, otra vez el de pico real (remuestrear saca a la luz lo que caia entre muestras)");
        else Check(true, "(esta rama no tiene salida moderna: el limitador de la cancion es el ultimo paso antes de los 16 bits)");
        Check(Codigo.Llama(arranca, "LimitadorPicoReal.get_DelayFrames"),
              "su retardo (pre-escucha + espera del detector) entra en la cuenta del fundido final (y en el cebado que se descarta en el relevo sin pausa)");
    }

    // Misma implementacion, dos techos: aisla el efecto del margen nuevo.
    static void MargenOperativo()
    {
        if (!hayPython) { Check(false, "el margen requiere Python/numpy y referencia independiente"); return; }
        float actual = techo;
        double anterior = -99, nuevo = -99, extremo = -99;
        try
        {
            float[] x = Musica(44100, 2, 0.9, 1.5, 21);
            techo = 0.98f;
            anterior = Db(Referencia(Cadena(TLim, x, 44100, 2), 44100, 2));
            techo = actual;
            nuevo = Db(Referencia(Cadena(TLim, x, 44100, 2), 44100, 2));
            foreach (int fs in new int[] { 22050, 44100, 48000, 96000 })
                foreach (double fr in new double[] { .47, .49, .499 })
                {
                    x = Seno(fs, 2, fs * fr, 1.5, .7, .2);
                    extremo = Math.Max(extremo, Db(Referencia(Limita(TLim, x, fs, 2), fs, 2)));
                }
            foreach (uint seed in new uint[] { 13, 42, 99 })
            {
                x = Musica(48000, 2, 2.5, .4, seed);
                extremo = Math.Max(extremo, Db(Referencia(Limita(TLim, x, 48000, 2), 48000, 2)));
            }
        }
        finally { techo = actual; }
        Info("mismo limitador/cadena: antes " + F(anterior, "0.000") + " dBTP, ahora " + F(nuevo, "0.000") + " dBTP");
        Info("tonos 0,47/0,49/0,499 fs y tres ruidos adicionales: " + F(extremo, "0.000") + " dBTP");
        Check(anterior > 0 && nuevo < 0 && anterior - nuevo > .45, "el margen elimina el exceso anterior en el caso de regresion sin cambiar el FIR");
        Check(extremo <= 0, "casos adicionales cerca de Nyquist por debajo de 0 dBTP");
    }

    // ---------------------------------------------------------------------------------------------
    //  8) LO QUE CUESTA
    // ---------------------------------------------------------------------------------------------
    static void Coste()
    {
        Console.WriteLine("\n  8) LO QUE CUESTA (esta maquina; no es la del bar)\n");
        int fs = 44100, ch = 2;
        float[][] senales = { Musica(fs, ch, 0.3, 10, 1), Musica(fs, ch, 1.6, 10, 2) };
        string[] nombres = { "musica a -10 dB (sin limitar)", "musica a +4 dB (limitando)" };
        double peorBloque = 0;
        for (int k = 0; k < 2; k++)
        {
            double[] ns = new double[2];
            Type[] tipos = { TViejo, TLim };
            for (int t = 0; t < 2; t++)
            {
                double mejor = 1e9;
                for (int vuelta = 0; vuelta < 4; vuelta++)
                {
                    object lim = Crea(tipos[t], new FuenteP(senales[k], fs, ch, null), tipos[t] == TLim ? techo : 0.98f, 3.0);
                    ISampleProvider p = (ISampleProvider)lim;
                    float[] buf = new float[4096 * ch];
                    Stopwatch sw = Stopwatch.StartNew(); double peor = 0;
                    long t0 = Stopwatch.GetTimestamp();
                    while (p.Read(buf, 0, buf.Length) > 0)
                    {
                        long t1 = Stopwatch.GetTimestamp();
                        double ms = (t1 - t0) * 1000.0 / Stopwatch.Frequency; t0 = t1;
                        if (vuelta > 0 && t == 1 && ms > peor) peor = ms;
                    }
                    sw.Stop();
                    if (vuelta > 0) { mejor = Math.Min(mejor, sw.Elapsed.TotalMilliseconds); if (t == 1) peorBloque = Math.Max(peorBloque, peor); }
                }
                ns[t] = mejor * 1e6 / senales[k].Length;
            }
            Info(nombres[k] + ": viejo " + F(ns[0], "0.0") + " ns por muestra, nuevo " + F(ns[1], "0.0") + " ns por muestra ("
                 + F(ns[1] * fs * ch / 1e6, "0.00") + " ms por segundo de musica estereo)");
            Check(ns[1] * fs * ch / 1e6 < 20, "el nuevo cuesta menos de 20 ms por segundo de musica (" + nombres[k] + ")");
        }
        Info("peor bloque de 4096 cuadros (93 ms de musica) con el nuevo: " + F(peorBloque, "0.000") + " ms");
    }

    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        TDet = a.GetType("Rocola.DetectorPicoReal", false);
        TLim = TF.GetNestedType("LimitadorPicoReal", T);
        TViejo = TF.GetNestedType("LookaheadLimiter", T);
        techo = (float)TF.GetField("TechoPicoReal", T).GetRawConstantValue();
        Check(techo == 0.88f, "techo operativo comun: 0,88; no cambia el contrato de otros techos");
        if (TDet == null || TLim == null || TViejo == null) { Check(false, "no estan el detector de pico real, su limitador o el limitador viejo"); return 1; }
        int cod; string o = Python("-c \"import numpy\"", out cod);
        hayPython = o != null && cod == 0;
        dir = Path.Combine(Path.GetTempPath(), "rocola-picoreal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ElDetector();
            ContraLaReferencia();
            Intacto();
            Bordes();
            Enlazados();
            LaCadena();
            MargenOperativo();
            Cableado();
            Coste();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}

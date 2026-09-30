using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NAudio.Wave;

// EL REALCE DE AGUDOS SIN PLEGADO (etapa 3 de audio).
//
// El realce satura la banda alta y crea armonicos nuevos; a 44,1 kHz los que pasan de Nyquist se plegaban y volvian
// como tonos que no son armonicos de nada. Ahora (Rocola/Realce.cs) la parte no lineal se calcula a 2x con un filtro
// de media banda, y la parte lineal y el seco se retrasan lo mismo. Esta bateria comprueba:
//
//   1) el filtro: coeficientes, retraso, banda de paso y de rechazo MEDIDAS;
//   2) realce apagado al empezar o a mitad: seco intacto, retrasado 31 cuadros exactos;
//   3) que todo sale alineado: con senal chica o sin nada que plegar, lo mismo que el realce de antes, retrasado;
//   4) el plegado, contra una referencia INDEPENDIENTE (pruebas/ref_realce.py: saturacion a 16x con interpolacion
//      exacta), en tonos, dos tonos, barrido, multitono y musica tipo MP3, a 44,1 y 48 kHz; y el de antes, igual;
//   5) mono, estereo y seis canales: cada canal es suyo (nada cruzado) y el silencio sigue en silencio;
//   6) cualquier tamano de lectura, tambien de una muestra o partiendo cuadros: bit a bit lo mismo;
//   7) el final: la cola de 31 cuadros sale entera, archivos cortisimos, fuente vacia, NaN e infinitos;
//   8) subirlo a media cancion desde cero conserva el 2x, la historia y la latencia;
//   9) el grave sintetico, medido aparte: su plegado (sin sobremuestrear) contra la misma referencia;
//  10) el cableado (el retraso entra en la cuenta del fundido; leer no reserva memoria);
//  11) lo que cuesta, por bloque de 50 ms, con el peor bloque.
//
// Lo que NO prueba: como suena (escucha A/B a volumen igualado) ni el coste en el equipo del bar o en XP.
class FuenteR : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos, k; private readonly int[] trozos;
    public FuenteR(float[] datos, int fs, int ch, int[] trozos) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); this.trozos = trozos; }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    {
        if (trozos != null && trozos.Length > 0) c = Math.Min(c, trozos[k++ % trozos.Length]);
        int n = Math.Min(c, d.Length - pos);
        if (n <= 0) return 0;
        Array.Copy(d, pos, b, o, n); pos += n; return n;
    }
}

class RealceBateria
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TTap, TRe, TBajo;
    static int fallos, oks, D;
    static string dir;
    static bool hayPython;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static double Db(double x) { return 20 * Math.Log10(Math.Max(1e-15, x)); }
    static object Crea(Type t, params object[] a) { return Activator.CreateInstance(t, T, null, a, null); }

    // ---------------------------------------------------------------------------------------------
    //  Herramientas
    // ---------------------------------------------------------------------------------------------
    static object Tap(float[] x, int fs, int ch, int[] trozos, float excite)
    {
        object owner = Codigo.FormSinEncender(TF);
        TF.GetField("auroraAmount", T).SetValue(owner, 0f);
        object tap = Crea(TTap, new FuenteR(x, fs, ch, trozos), owner);
        TTap.GetField("Excite").SetValue(tap, excite);
        return tap;
    }
    static int Retardo(object tap) { return (int)TTap.GetProperty("DelayFrames", T).GetValue(tap, null); }
    // Lee TODO lo que da (con su cola). cambio: tras cuantas muestras se ejecuta 'accion' (una vez).
    static float[] LeeTodo(object tap, int bloque, int cambio, Action<object> accion)
    {
        ISampleProvider p = (ISampleProvider)tap;
        List<float> r = new List<float>();
        float[] buf = new float[bloque];
        int n, hechas = 0; bool hecho = accion == null;
        while (true)
        {
            int pide = bloque;
            if (!hecho && cambio - hechas > 0) pide = Math.Min(pide, cambio - hechas);
            if (!hecho && hechas >= cambio) { accion(tap); hecho = true; }
            n = p.Read(buf, 0, pide);
            if (n <= 0) break;
            for (int i = 0; i < n; i++) r.Add(buf[i]);
            hechas += n;
            if (r.Count > 50000000) break;
        }
        return r.ToArray();
    }
    static float[] Realza(float[] x, int fs, int ch, float excite, int[] trozos, int bloque)
    {
        return LeeTodo(Tap(x, fs, ch, trozos, excite), bloque, 0, null);
    }
    // El realce de ANTES (una muestra por vez, sin sobremuestrear), por canal: v + TanhRapida(2,5h)*0,4*(excite*0,5).
    delegate float Fn(float x);
    static Fn tanhR;
    static float[] RealceViejo(float[] x, int fs, int ch, float excite)
    {
        float polo = (float)TF.GetMethod("ExciterPole", T).Invoke(null, new object[] { fs });
        float[] y = new float[x.Length]; float[] lp = new float[ch];
        float mix = excite * .5f;
        for (int i = 0; i < x.Length; i++)
        {
            int c = i % ch; float v = x[i];
            float l = (lp[c] += (v - lp[c]) * polo); float h = v - l;
            y[i] = v + tanhR(h * 2.5f) * .4f * mix;
        }
        return y;
    }
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
    static void Wav(string ruta, float[] x, int fs, int ch)
    {
        using (WaveFileWriter w = new WaveFileWriter(ruta, WaveFormat.CreateIeeeFloatWaveFormat(fs, ch))) w.WriteSamples(x, 0, x.Length);
    }
    // Aliasing medido por la referencia: [PICO, TERCIO, DBFS, DESEADO, BANDA] del canal 0 (NaN si no hay Python).
    static double[] Plegado(float[] entrada, float[] salida, int fs, float excite, int retraso)
    {
        double[] r = { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN };
        if (!hayPython) return r;
        string a = Path.Combine(dir, "entra.wav"), b = Path.Combine(dir, "sale.wav");
        Wav(a, entrada, fs, 1); Wav(b, salida, fs, 1);
        int cod; string o = Python("ref_realce.py realce \"" + a + "\" \"" + b + "\" " + F(excite, "0.######") + " " + retraso, out cod);
        if (o == null || cod != 0) return r;
        string[] p = o.Trim().Split(' ');
        string[] claves = { "PICO", "TERCIO", "DBFS", "DESEADO", "BANDA" };
        for (int i = 0; i + 1 < p.Length; i++)
        {
            double v;
            if (!double.TryParse(p[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) continue;
            int k = Array.IndexOf(claves, p[i]); if (k >= 0) r[k] = v;
        }
        return r;
    }

    // ---------------------------------------------------------------------------------------------
    //  Senales
    // ---------------------------------------------------------------------------------------------
    static double Fundido(long i, long n, int fs)
    {
        long f = fs / 100;
        if (i < f) return 0.5 - 0.5 * Math.Cos(Math.PI * i / f);
        if (i >= n - f) return 0.5 - 0.5 * Math.Cos(Math.PI * (n - 1 - i) / f);
        return 1;
    }
    static float[] Tonos(int fs, int ch, double[] fr, double amp, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        for (long i = 0; i < n; i++)
        {
            double v = 0;
            for (int k = 0; k < fr.Length; k++) v += Math.Sin(2 * Math.PI * fr[k] * i / fs + 0.7 * k);
            v *= amp * Fundido(i, n, fs);
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)v;
        }
        return x;
    }
    static float[] Barrido(int fs, double f0, double f1, double amp, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n]; double T0 = seg, k = Math.Log(f1 / f0);
        for (long i = 0; i < n; i++)
        {
            double t = (double)i / fs;
            x[i] = (float)(amp * Fundido(i, n, fs) * Math.Sin(2 * Math.PI * f0 * T0 / k * (Math.Exp(t / T0 * k) - 1)));
        }
        return x;
    }
    static float[] Multitono(int fs, double f0, double f1, int cuantos, double pico, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n]; double[] fr = new double[cuantos];
        for (int k = 0; k < cuantos; k++) fr[k] = f0 * Math.Pow(f1 / f0, k / (double)(cuantos - 1));
        double m = 0; double[] v = new double[n];
        for (long i = 0; i < n; i++) { double s = 0; for (int k = 0; k < cuantos; k++) s += Math.Sin(2 * Math.PI * fr[k] * i / fs + 1.3 * k * k); v[i] = s; m = Math.Max(m, Math.Abs(s)); }
        for (long i = 0; i < n; i++) x[i] = (float)(pico * Fundido(i, n, fs) * v[i] / m);
        return x;
    }
    // Musica "tipo MP3": grave y medios, y platillos hechos de 120 tonos al azar entre 3 kHz y el corte, con golpes.
    static float[] MusicaMp3(int fs, int ch, double amp, double seg, double corte, int semilla)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        Random r = new Random(semilla);
        int nt = 120; double[] w = new double[nt], fase = new double[nt];
        for (int k = 0; k < nt; k++) { w[k] = 2 * Math.PI * (3000 + r.NextDouble() * (corte - 3000)) / fs; fase[k] = r.NextDouble() * 6.28; }
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
    static float[] Canal(float[] x, int ch, int c) { float[] y = new float[x.Length / ch]; for (int i = 0; i < y.Length; i++) y[i] = x[i * ch + c]; return y; }

    // ---------------------------------------------------------------------------------------------
    //  1) EL FILTRO
    // ---------------------------------------------------------------------------------------------
    static void ElFiltro()
    {
        Console.WriteLine("\n  1) EL FILTRO DE MEDIA BANDA\n");
        double[] C = (double[])TRe.GetField("C", T).GetValue(null);
        int pares = (int)TRe.GetField("Pares", T).GetValue(null), largo = (int)TRe.GetField("Largo", T).GetValue(null);
        double suma = 0; bool finitos = true;
        foreach (double c in C) { suma += c; if (double.IsNaN(c) || double.IsInfinity(c)) finitos = false; }
        Check(C.Length == pares && largo == 4 * pares - 1 && D == 2 * pares - 1 && D == 31,
              "63 coeficientes a 2x (16 pares a media muestra), retraso de 31 muestras de la frecuencia de trabajo");
        Check(finitos && Math.Abs(2 * suma - 1) < 1e-12, "la muestra a medio camino (cada coeficiente, con su espejo) suma 1: la continua pasa entera (" + F(2 * suma - 1, "0.0e0") + ")");
        // Respuesta del filtro entero (a 2x): centro 1, lados C en los coeficientes pares, simetricos.
        double[] h = new double[largo]; h[(largo - 1) / 2] = 1.0;
        for (int t = 0; t < 2 * pares; t++) h[2 * t] = t < pares ? C[t] : C[2 * pares - 1 - t];
        double paso = 0, rechazo = -999;
        for (int k = 0; k <= 2000; k++)
        {
            double f = k / 2000.0;   // en veces la frecuencia de trabajo (fs), de 0 a 1 (Nyquist de 2x)
            double re = 0, im = 0;
            for (int n = 0; n < largo; n++) { re += h[n] * Math.Cos(Math.PI * f * n); im -= h[n] * Math.Sin(Math.PI * f * n); }
            double g = Math.Sqrt(re * re + im * im) / 2.0;
            if (f <= 0.40) paso = Math.Max(paso, Math.Abs(Db(g)));
            if (f >= 0.60) rechazo = Math.Max(rechazo, Db(g));
        }
        Info("banda de paso hasta 0,40 fs (17,6 kHz a 44,1): se aparta " + F(paso, "0.0000") + " dB; rechazo desde 0,60 fs (26,5 kHz): " + F(rechazo, "0.0") + " dB");
        Check(paso < 0.01, "banda de paso plana hasta 0,40 fs (menos de 0,01 dB)");
        Check(rechazo < -69, "rechazo de mas de 69 dB desde 0,60 fs, donde caen las imagenes y lo que se plegaria");
    }

    // ---------------------------------------------------------------------------------------------
    //  2) PASO DIRECTO Y RETRASO EXACTO
    // ---------------------------------------------------------------------------------------------
    static void PasoDirecto()
    {
        Console.WriteLine("\n  2) APAGADO: INTACTO; SI SE APAGA A MEDIA CANCION, INTACTO Y RETRASADO 31 CUADROS\n");
        bool directo = true, largoBien = true, retrasoBien = true, apagado = true, retardos = true;
        foreach (int fs in new int[] { 22050, 44100, 48000, 96000 })
            foreach (int ch in new int[] { 1, 2, 6 })
            {
                float[] x = MusicaMp3(fs, ch, 0.7, 0.4, Math.Min(19000, 0.45 * fs), fs + ch);
                object t0 = Tap(x, fs, ch, null, 0f);
                if (Retardo(t0) != D) retardos = false;
                float[] y0 = LeeTodo(t0, 1024 * ch, 0, null);
                if (y0.Length != x.Length + D * ch) directo = false;
                else for (int i = 0; i < y0.Length; i++)
                    if (y0[i] != (i < D * ch ? 0f : x[i - D * ch])) { directo = false; break; }
                object t1 = Tap(x, fs, ch, null, 0.35f);
                if (Retardo(t1) != D) retardos = false;
                int corte = (int)(0.1 * fs) * ch;
                float[] y1 = LeeTodo(t1, 1024 * ch, corte, delegate(object tp) { TTap.GetField("Excite").SetValue(tp, 0f); });
                if (y1.Length != x.Length + D * ch) largoBien = false;
                // Tras la rampa (20 ms), lo que sale es la entrada de hace 31 cuadros, bit a bit.
                int desde = corte + ((int)(0.02 * fs) + D + 2) * ch;
                for (int i = desde; i < y1.Length && i - D * ch < x.Length; i++) if (y1[i] != x[i - D * ch]) { apagado = false; break; }
                // Los primeros 31 cuadros no traen nada del seco: solo, si acaso, el anticipo del filtro de la
                // saturacion (el filtro es simetrico), que con la entrada que sube desde cero es casi nada.
                for (int i = 0; i < D * ch; i++) if (Math.Abs(y1[i]) > 1e-6f) { retrasoBien = false; break; }
            }
        Check(retardos, "DelayFrames: siempre 31, independientemente del realce inicial");
        Check(directo, "empieza apagado: seco intacto bit a bit tras 31 cuadros, con cola exacta (4 frecuencias, 1/2/6 canales)");
        Check(retrasoBien && largoBien, "empieza encendido: los primeros 31 cuadros no traen nada del seco y al final salen 31 cuadros de mas (la cola)");
        Check(apagado, "apagado a media cancion: en cuanto acaba la rampa, sale el seco intacto de hace 31 cuadros, bit a bit");
    }

    // ---------------------------------------------------------------------------------------------
    //  3) ALINEADO
    // ---------------------------------------------------------------------------------------------
    static void Alineado()
    {
        Console.WriteLine("\n  3) EL SECO, LA PARTE LINEAL Y LA SATURACION SALEN ALINEADOS\n");
        double peorChico = 0, peorMedio = 0, efecto = 0;
        foreach (int fs in new int[] { 44100, 48000 })
            foreach (float ex in new float[] { 0.35f, 1f })
            {
                // Senal chica: la saturacion casi no actua, asi que lo nuevo debe ser el realce de antes, retrasado.
                float[] chico = MusicaMp3(fs, 1, 0.001, 0.3, 19000, 7);
                float[] a = Realza(chico, fs, 1, ex, null, 4096), b = RealceViejo(chico, fs, 1, ex);
                if (a.Length != b.Length + D) { peorChico = peorMedio = 1; continue; }   // sin su cola: ni se compara
                for (int i = 0; i < b.Length; i++) peorChico = Math.Max(peorChico, Math.Abs(a[i + D] - b[i]));
                // Senal fuerte pero sin nada que plegar (tonos de 1 a 3 kHz: sus armonicos caben): lo mismo.
                float[] medio = Tonos(fs, 1, new double[] { 1100, 2300, 3100 }, 0.3, 0.3);
                a = Realza(medio, fs, 1, ex, null, 4096); b = RealceViejo(medio, fs, 1, ex);
                if (a.Length != b.Length + D) { peorMedio = 1; continue; }
                for (int i = 0; i < b.Length; i++) { peorMedio = Math.Max(peorMedio, Math.Abs(a[i + D] - b[i])); efecto = Math.Max(efecto, Math.Abs(b[i] - medio[i])); }
            }
        Info("senal a -60 dB: se aparta del realce de antes (retrasado) como mucho " + F(peorChico, "0.0e0"));
        Info("tonos de 1 a 3 kHz fuertes: como mucho " + F(peorMedio, "0.0e0") + ", con un realce que mueve la onda hasta " + F(efecto, "0.000"));
        Check(peorChico < 2e-7, "con senal chica es el realce de antes, retrasado 31 cuadros exactos (ni una muestra de desfase)");
        Check(peorMedio < efecto * 2e-3, "con senal fuerte sin nada que plegar, lo mismo (menos de 0,2 % del efecto): la saturacion esta en su sitio");
    }

    // ---------------------------------------------------------------------------------------------
    //  4) EL PLEGADO, CONTRA LA REFERENCIA
    // ---------------------------------------------------------------------------------------------
    static void ContraLaReferencia()
    {
        Console.WriteLine("\n  4) EL PLEGADO, CONTRA UNA REFERENCIA INDEPENDIENTE (saturacion a 16x, exacta)\n");
        if (!hayPython) { Check(true, "(sin Python con numpy: se salta y se dice)"); return; }
        Info("tonos: el mayor producto plegado por debajo de 16 kHz respecto al tono; musica y multitono: el peor tercio");
        Info("de octava (plegado/senal en esa banda) y, entre corchetes, el nivel absoluto de la peor banda (dBFS).");
        Info("Antes = el realce de una muestra por vez; ahora = 2x. En dB.");
        // [0] realce de fabrica (0,35), [1] al maximo (1,0)
        double[] tono = { -999, -999 }, tonoNyq = { -999, -999 }, musica = { -999, -999 }, banda = { -999, -999 }, bandaAntes = { -999, -999 };
        double mejoraTono = 999, mejoraMus = 999;
        foreach (int fs in new int[] { 44100, 48000 })
            foreach (float ex in new float[] { 0.35f, 1f })
            {
                int q = ex < 0.5f ? 0 : 1;
                List<string> fila = new List<string>();
                double[] fr = { 11000, 15000, 17000, 19000, 20000 };
                foreach (double f in fr)
                {
                    float[] x = Tonos(fs, 1, new double[] { f }, 0.5, 0.75);
                    double[] ahora = Plegado(x, Realza(x, fs, 1, ex, null, 4096), fs, ex, D);
                    double[] antes = Plegado(x, RealceViejo(x, fs, 1, ex), fs, ex, 0);
                    fila.Add(F(f / 1000, "0") + "k " + F(antes[0], "0") + "/" + F(ahora[0], "0"));
                    if (f < 19000) tono[q] = Math.Max(tono[q], ahora[0]); else tonoNyq[q] = Math.Max(tonoNyq[q], ahora[0]);
                    mejoraTono = Math.Min(mejoraTono, antes[0] - ahora[0]);
                }
                Info(F(fs / 1000.0, "0.0") + " kHz realce " + F(ex, "0.00") + " | tonos a -6 dB, antes/ahora: " + string.Join("  ", fila.ToArray()));
                fila.Clear();
                string[] nombres = { "dos tonos 19+20k", "barrido 1-21k", "multitono 2-20k", "musica hasta 16k", "musica hasta 19k" };
                float[][] senales = { Tonos(fs, 1, new double[] { 19000, 20000 }, 0.35, 0.75), Barrido(fs, 1000, 21000, 0.9, 0.75),
                                      Multitono(fs, 2000, 20000, 24, 0.8, 0.75), Canal(MusicaMp3(fs, 1, 0.9, 0.75, 16000, 3), 1, 0),
                                      Canal(MusicaMp3(fs, 1, 0.9, 0.75, 19000, 4), 1, 0) };
                for (int k = 0; k < senales.Length; k++)
                {
                    float[] x = senales[k];
                    double[] ahora = Plegado(x, Realza(x, fs, 1, ex, null, 4096), fs, ex, D);
                    double[] antes = Plegado(x, RealceViejo(x, fs, 1, ex), fs, ex, 0);
                    if (k < 2) { fila.Add(nombres[k] + " " + F(antes[0], "0") + "/" + F(ahora[0], "0")); if (k == 0) tonoNyq[q] = Math.Max(tonoNyq[q], ahora[0]); else tono[q] = Math.Max(tono[q], ahora[0]); }
                    else fila.Add(nombres[k] + " " + F(antes[1], "0") + "/" + F(ahora[1], "0") + " [" + F(antes[4], "0") + "/" + F(ahora[4], "0") + "]");
                    if (k >= 3) { musica[q] = Math.Max(musica[q], ahora[1]); banda[q] = Math.Max(banda[q], ahora[4]); bandaAntes[q] = Math.Max(bandaAntes[q], antes[4]); mejoraMus = Math.Min(mejoraMus, antes[1] - ahora[1]); }
                }
                Info("      " + string.Join(" | ", fila.ToArray()));
            }
        Check(tono[0] <= -73 && tono[1] <= -63,
              "tonos de 11 a 17 kHz y barrido: lo plegado queda a " + F(tono[0], "0.0") + " dB del tono con el realce de fabrica y a " + F(tono[1], "0.0") + " al maximo (antes, -48 y -39)");
        Check(tonoNyq[0] <= -66 && tonoNyq[1] <= -56,
              "tonos pegados a Nyquist (19, 20 y 19+20 kHz), el caso dificil de un 2x: " + F(tonoNyq[0], "0.0") + " y " + F(tonoNyq[1], "0.0") + " dB");
        Check(mejoraTono >= 15, "en todos los tonos, al menos 15 dB menos plegado que antes (el que menos: " + F(mejoraTono, "0.0") + " dB)");
        Check(musica[0] <= -52 && musica[1] <= -43 && mejoraMus >= 25,
              "musica tipo MP3: peor tercio de octava a " + F(musica[0], "0.0") + " dB (fabrica) y " + F(musica[1], "0.0") + " (maximo), 25 dB o mas por debajo de antes (" + F(mejoraMus, "0.0") + ")");
        Check(banda[0] <= -100 && banda[1] <= -90 && bandaAntes[0] - banda[0] >= 20 && bandaAntes[1] - banda[1] >= 20,
              "y en nivel absoluto la banda con mas plegado queda en " + F(banda[0], "0.0") + " / " + F(banda[1], "0.0") + " dBFS (antes " + F(bandaAntes[0], "0.0") + " / " + F(bandaAntes[1], "0.0") + ")");
    }

    // ---------------------------------------------------------------------------------------------
    //  5) CANALES
    // ---------------------------------------------------------------------------------------------
    static void Canales()
    {
        Console.WriteLine("\n  5) CADA CANAL ES SUYO\n");
        int fs = 44100, ch = 6; long n = fs / 3;
        float[] x = new float[n * ch];
        double[] fr = { 15000, 0, 9000, 19500, 5000, 12000 };   // el canal 1, en silencio
        for (long i = 0; i < n; i++) for (int c = 0; c < ch; c++) x[i * ch + c] = fr[c] == 0 ? 0f : (float)(0.6 * Math.Sin(2 * Math.PI * fr[c] * i / fs + c));
        float[] y = Realza(x, fs, ch, 0.6f, null, 1024 * ch);
        bool iguales = true, mudo = true;
        for (int c = 0; c < ch; c++)
        {
            float[] solo = Realza(Canal(x, ch, c), fs, 1, 0.6f, null, 1024);
            float[] deY = Canal(y, ch, c);
            if (solo.Length != deY.Length) { iguales = false; continue; }
            for (int i = 0; i < solo.Length; i++) if (solo[i] != deY[i]) { iguales = false; break; }
            if (fr[c] == 0) foreach (float v in deY) if (v != 0f) { mudo = false; break; }
        }
        Check(iguales, "seis canales: cada uno sale bit a bit como si fuera solo (nada se cruza de un canal a otro)");
        Check(mudo, "el canal en silencio sigue en silencio exacto, con 15 y 19,5 kHz fuertes al lado");
    }

    // ---------------------------------------------------------------------------------------------
    //  6) TAMANOS DE LECTURA
    // ---------------------------------------------------------------------------------------------
    static void Bloques()
    {
        Console.WriteLine("\n  6) CUALQUIER TAMANO DE LECTURA DA LO MISMO\n");
        bool iguales = true; int casos = 0;
        foreach (int ch in new int[] { 1, 2, 6 })
        {
            float[] x = MusicaMp3(48000, ch, 0.8, 0.3, 19000, 11 + ch);
            float[] refe = Realza(x, 48000, ch, 0.5f, null, 1 << 16);
            int[][] trozos = { null, new int[] { 1 }, new int[] { 7 }, new int[] { 511, 1 }, new int[] { 513 }, new int[] { 3 * ch + 1 }, new int[] { 512 * ch }, new int[] { 1024 * ch + 3, 5 } };
            int[] bloques = { 1, 5, 512 * ch, 4096 * ch, 2205 * ch };
            foreach (int[] t in trozos)
                foreach (int b in bloques)
                {
                    if (t != null && t.Length == 1 && t[0] == 1 && b == 1 && ch == 6) continue;
                    float[] y = Realza(x, 48000, ch, 0.5f, t, b);
                    casos++;
                    if (y.Length != refe.Length) { iguales = false; continue; }
                    for (int i = 0; i < y.Length; i++) if (y[i] != refe[i]) { iguales = false; break; }
                }
        }
        Check(iguales, casos + " combinaciones de trozos de la fuente y lecturas (de 1 muestra, partiendo cuadros, de 512 justos...): bit a bit lo mismo, misma cola");
    }

    // ---------------------------------------------------------------------------------------------
    //  7) EL FINAL Y LOS BORDES
    // ---------------------------------------------------------------------------------------------
    static void Bordes()
    {
        Console.WriteLine("\n  7) LA COLA, LOS ARCHIVOS CORTISIMOS Y LA BASURA\n");
        int fs = 44100, ch = 2;
        float[] largo = MusicaMp3(fs, ch, 0.9, 0.2, 19000, 5);
        bool colas = true;
        foreach (int cuadros in new int[] { 1, 7, 30, 31, 32, 100, 511, 512, 513, 4000 })
        {
            float[] x = new float[cuadros * ch]; Array.Copy(largo, x, x.Length);
            // la cuenta sin final: la misma senal seguida de silencio, sin cortar
            float[] conSilencio = new float[(cuadros + D + 64) * ch]; Array.Copy(x, conSilencio, x.Length);
            float[] y = Realza(x, fs, ch, 0.5f, null, 1024);
            float[] z = Realza(conSilencio, fs, ch, 0.5f, null, 1024);
            if (y.Length != (cuadros + D) * ch) { colas = false; continue; }
            for (int i = 0; i < y.Length; i++) if (y[i] != z[i]) { colas = false; break; }
        }
        Check(colas, "de 1 a 4000 cuadros: sale todo mas los 31 retenidos, igual bit a bit que si la cancion siguiera en silencio");
        object t = Tap(new float[0], fs, ch, null, 0.5f);
        float[] vacio = LeeTodo(t, 1024, 0, null);
        Check(vacio.Length == 0, "fuente vacia: no sale nada (no se inventa una cola de silencio)");
        float[] fuerte = new float[2000 * ch]; for (int i = 0; i < fuerte.Length; i++) fuerte[i] = (i / ch) % 2 == 0 ? 0.95f : -0.95f;
        t = Tap(fuerte, fs, ch, null, 1f);
        float[] yf = LeeTodo(t, 700, 0, null);
        double cola = 0; for (int i = fuerte.Length; i < yf.Length; i++) cola = Math.Max(cola, Math.Abs(yf[i]));
        float[] mas = new float[64];
        int n1 = ((ISampleProvider)t).Read(mas, 0, 64), n2 = ((ISampleProvider)t).Read(mas, 0, 64);
        Check(yf.Length == fuerte.Length + D * ch && cola > 0.5 && n1 == 0 && n2 == 0,
              "acaba a todo volumen: la cola sale con su nivel (" + F(cola, "0.00") + ") y despues solo ceros de fin, las veces que se pida");
        float[] mudo = new float[3000 * ch];
        float[] ym = Realza(mudo, fs, ch, 1f, null, 999);
        bool silencio = ym.Length == mudo.Length + D * ch; foreach (float v in ym) if (v != 0f) silencio = false;
        Check(silencio, "silencio entra, silencio exacto sale (con la cola en silencio)");
        float[] basura = MusicaMp3(fs, ch, 0.5, 0.2, 19000, 9);
        for (int i = 0; i < basura.Length; i += 37) basura[i] = (i % 3 == 0) ? float.NaN : (i % 3 == 1 ? float.PositiveInfinity : -1e30f);
        float[] yb = Realza(basura, fs, ch, 1f, null, 1024);
        bool sanos = yb.Length == basura.Length + D * ch; double pico = 0;
        foreach (float v in yb) { if (float.IsNaN(v) || float.IsInfinity(v)) sanos = false; pico = Math.Max(pico, Math.Abs(v)); }
        Check(sanos && pico < 2, "NaN, infinitos y valores absurdos en la entrada: ni uno sale (pico " + F(pico, "0.00") + ")");
    }

    // ---------------------------------------------------------------------------------------------
    //  8) SUBIRLO A MEDIA CANCION DESDE CERO
    // ---------------------------------------------------------------------------------------------
    static void AMediaCancion()
    {
        Console.WriteLine("\n  8) SUBIRLO A MEDIA CANCION DESDE CERO: SIN SALTO EN EL TIEMPO\n");
        int fs = 44100, ch = 2;
        float[] x = MusicaMp3(fs, ch, 0.8, 0.5, 19000, 21);
        int cambio = fs / 5 * ch;
        object t = Tap(x, fs, ch, null, 0f);
        float[] y = LeeTodo(t, 512 * ch, cambio, delegate(object tp) { TTap.GetField("Excite").SetValue(tp, 0.5f); });
        bool antes = y.Length == x.Length + D * ch;
        for (int i = 0; i < cambio && antes; i++) if (y[i] != (i < D * ch ? 0f : x[i - D * ch])) antes = false;
        double dif = 0; for (int i = cambio; i < x.Length; i++) dif = Math.Max(dif, Math.Abs(y[i] - x[i - D * ch]));
        Check(antes && dif > 0.005, "el realce entra desde cero y conserva seco, latencia y cola de 31 cuadros");
        Check((int)TTap.GetField("modoRealce", T).GetValue(t) == 2, "activado a mitad tambien usa 2x; no abandona calidad");
        // Referencia con FIR siempre calientes: mezcla subnormal antes del cambio, misma mezcla real despues.
        // No comparte el cebado barato con el objeto probado; obliga al camino FIR en cada bloque.
        bool historia = true; double error = 0;
        foreach (int bloque in new int[] { 1, 7, 31, 257, 512 })
        {
            object frio = Crea(TRe, 1, .55f), caliente = Crea(TRe, 1, .55f);
            float[] a = Canal(x, ch, 0), b = (float[])a.Clone(), ka = new float[a.Length], kb = new float[a.Length];
            for (int i = 0; i < a.Length; i++) { ka[i] = i < fs / 5 ? 0f : .1f; kb[i] = i < fs / 5 ? 1e-30f : .1f; }
            MethodInfo procesa = TRe.GetMethod("Bloque", T);
            for (int s = 0; s < a.Length; s += bloque)
            {
                int n = Math.Min(bloque, a.Length - s);
                procesa.Invoke(frio, new object[] { 0, a, s, 1, n, ka, s });
                procesa.Invoke(caliente, new object[] { 0, b, s, 1, n, kb, s });
            }
            for (int i = fs / 5; i < a.Length; i++) error = Math.Max(error, Math.Abs(a[i] - b[i]));
            if (error > 1e-7) historia = false;
        }
        Check(historia, "historia al activar igual a FIR calientes: error " + F(error, "0.0e0") + ", bloques 1/7/31/257/512");
    }

    // ---------------------------------------------------------------------------------------------
    //  9) EL GRAVE SINTETICO, APARTE
    // ---------------------------------------------------------------------------------------------
    static void ElGrave()
    {
        Console.WriteLine("\n  9) EL GRAVE SINTETICO: SU PLEGADO, MEDIDO APARTE (no se sobremuestrea)\n");
        if (!hayPython) { Check(true, "(sin Python con numpy: se salta y se dice)"); return; }
        double peor = -999, espejo = -999; List<string> fila = new List<string>();
        foreach (int fs in new int[] { 44100, 48000 })
        {
            float[][] s = { Tonos(fs, 2, new double[] { 30 }, 0.5, 1.5), Tonos(fs, 2, new double[] { 55 }, 0.5, 1.5), Tonos(fs, 2, new double[] { 100 }, 0.5, 1.5),
                            Tonos(fs, 2, new double[] { 41, 55 }, 0.3, 1.5), Bombo(fs, 2, 1.5), MusicaMp3(fs, 2, 0.9, 1.5, 19000, 8) };
            string[] nm = { "30 Hz", "55 Hz", "100 Hz", "41+55 Hz", "bombo", "musica" };
            for (int k = 0; k < s.Length; k++)
            {
                object b = Crea(TBajo, new FuenteR(s[k], fs, 2, null), 0.8f);
                float[] y = new float[s[k].Length]; ISampleProvider p = (ISampleProvider)b;
                int at = 0, n; while (at < y.Length && (n = p.Read(y, at, Math.Min(4096, y.Length - at))) > 0) at += n;
                string a = Path.Combine(dir, "g_in.wav"), o = Path.Combine(dir, "g_out.wav");
                Wav(a, s[k], fs, 2); Wav(o, y, fs, 2);
                int cod; string r = Python("ref_realce.py grave \"" + a + "\" \"" + o + "\" 0.8 1", out cod);   // 1: el grave de cada cuadro se suma al siguiente
                double al = double.NaN, es = double.NaN;
                if (r != null && cod == 0)
                {
                    string[] p2 = r.Trim().Split(' ');
                    for (int i = 0; i + 1 < p2.Length; i++)
                    {
                        if (p2[i] == "ALIAS") double.TryParse(p2[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out al);
                        if (p2[i] == "ESPEJO") double.TryParse(p2[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out es);
                    }
                }
                peor = Math.Max(peor, double.IsNaN(al) ? 999 : al); espejo = Math.Max(espejo, double.IsNaN(es) ? 999 : es);
                if (fs == 44100) fila.Add(nm[k] + " " + F(al, "0"));
            }
        }
        Info("plegado respecto a los armonicos que genera, a 44,1 kHz: " + string.Join(" | ", fila.ToArray()) + " dB");
        Check(peor <= -85, "el grave sintetico pliega como mucho " + F(peor, "0.0") + " dB por debajo de lo que genera: no necesita sobremuestreo");
        Check(espejo <= -90, "y la referencia rehace la misma cuenta que la rocola (se aparta " + F(espejo, "0.0") + " dB): se mide lo que de verdad sale");
    }
    static float[] Bombo(int fs, int ch, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        for (long i = 0; i < n; i++)
        {
            double tl = (i % (fs / 2)) / (double)fs;
            float v = (float)(0.9 * Math.Sin(2 * Math.PI * 55 * tl) * Math.Exp(-tl / 0.18) * (1 - Math.Exp(-tl / 0.0005)) * Fundido(i, n, fs));
            for (int c = 0; c < ch; c++) x[i * ch + c] = v;
        }
        return x;
    }

    // ---------------------------------------------------------------------------------------------
    //  10) EL CABLEADO
    // ---------------------------------------------------------------------------------------------
    static void Cableado()
    {
        Console.WriteLine("\n  10) EL CABLEADO\n");
        // La cadena se arma en ArmaCadena (arranque de siempre y relevo sin pausa).
        List<string> arranca = Codigo.ConAyudantes(TF, "ArmaCadena");
        Check(Codigo.Llama(arranca, "LevelTap.get_DelayFrames") && Codigo.Llama(Codigo.ConAyudantes(TF, "StartOpenedTrack"), "ArmaCadena"),
              "al arrancar la cancion, el retraso del realce entra en la cuenta del fundido final (y en el cebado que se descarta en el relevo sin pausa)");
        MethodInfo leer = TTap.GetMethod("Read", T);
        Check(Codigo.Llama(leer, "ProcesaConRealce") && Codigo.Llama(leer, "VaciaRealce"), "leer pasa por el realce 2x y, al acabar la fuente, vacia lo retenido");
        bool sinReservas = true;
        foreach (MethodInfo m in new MethodInfo[] { leer, TTap.GetMethod("ProcesaConRealce", T), TTap.GetMethod("VaciaRealce", T), TRe.GetMethod("Bloque", T) })
            foreach (Codigo.Paso p in Codigo.Pasos(m)) if (p.Op == 0x8D || p.Op == 0x73) sinReservas = false;
        Check(sinReservas, "leer, el realce y el vaciado no reservan memoria (ni arreglos ni objetos): todo se reserva al construir");
        Check(!Codigo.Llama(TBajo.GetMethod("Read", T), "RealceSobremuestreado.Bloque"), "el grave sintetico no se sobremuestrea (medido: no le hace falta)");
    }

    // ---------------------------------------------------------------------------------------------
    //  11) LO QUE CUESTA
    // ---------------------------------------------------------------------------------------------
    static void Coste()
    {
        Console.WriteLine("\n  11) LO QUE CUESTA (esta maquina; no es el equipo 2C/2T ni XP)\n");
        int fs = 44100, ch = 2, bloque = 2205 * ch;   // 50 ms
        float[] x = MusicaMp3(fs, ch, 0.8, 10, 19000, 31);
        string[] nombres = { "realce apagado (historia y seco)", "realce 2x activado a mitad", "realce 2x desde inicio" };
        double[] ns = new double[3], peor = new double[3];
        for (int modo = 0; modo < 3; modo++)
        {
            double mejor = 1e9, peorB = 0;
            for (int vuelta = 0; vuelta < 4; vuelta++)
            {
                object t = Tap(x, fs, ch, null, modo == 2 ? 0.35f : 0f);
                ISampleProvider p = (ISampleProvider)t;
                float[] buf = new float[bloque];
                if (modo == 1) { p.Read(buf, 0, bloque); TTap.GetField("Excite").SetValue(t, 0.35f); }
                Stopwatch sw = Stopwatch.StartNew(); long t0 = Stopwatch.GetTimestamp(); double pb = 0;
                while (p.Read(buf, 0, bloque) > 0)
                {
                    long t1 = Stopwatch.GetTimestamp(); double ms = (t1 - t0) * 1000.0 / Stopwatch.Frequency; t0 = t1;
                    if (ms > pb) pb = ms;
                }
                sw.Stop();
                if (vuelta > 0) { mejor = Math.Min(mejor, sw.Elapsed.TotalMilliseconds); peorB = Math.Max(peorB, pb); }
            }
            ns[modo] = mejor * 1e6 / x.Length; peor[modo] = peorB;
            Info(nombres[modo] + ": " + F(ns[modo], "0.0") + " ns por muestra, " + F(ns[modo] * fs * ch / 1e6, "0.00") + " ms por segundo de musica estereo; peor bloque de 50 ms: " + F(peorB, "0.000") + " ms");
        }
        Info("(todo LevelTap: seguridad contra NaN, medida del nivel, ganancia, rampas y realce)");
        Check(ns[2] * fs * ch / 1e6 < 25, "el realce 2x cuesta menos de 25 ms por segundo de musica estereo en esta maquina");
        Check(peor[2] < 10, "y ningun bloque de 50 ms pasa de 10 ms (" + F(peor[2], "0.000") + ")");
    }

    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        TTap = TF.GetNestedType("LevelTap", T);
        TRe = a.GetType("Rocola.RealceSobremuestreado", false);
        TBajo = TF.GetNestedType("DeepBassProvider", T);
        if (TTap == null || TRe == null || TBajo == null) { Check(false, "no estan LevelTap, el realce sobremuestreado o el grave sintetico"); return 1; }
        D = (int)TRe.GetField("Retraso", T).GetValue(null);
        tanhR = (Fn)Delegate.CreateDelegate(typeof(Fn), TF.GetMethod("TanhRapida", T));
        int cod; string o = Python("-c \"import numpy\"", out cod);
        hayPython = o != null && cod == 0;
        dir = Path.Combine(Path.GetTempPath(), "rocola-realce-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ElFiltro();
            PasoDirecto();
            Alineado();
            ContraLaReferencia();
            Canales();
            Bloques();
            Bordes();
            AMediaCancion();
            ElGrave();
            Cableado();
            Coste();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}

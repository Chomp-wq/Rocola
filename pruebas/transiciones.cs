using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

// LAS TRANSICIONES QUE QUEDABAN: EL GRAVE SINTETICO Y EL DSEE (etapa 4 de audio).
//
// Las rampas de LevelTap (volumen, realce, EQ) ya estaban probadas en audiosuave.cs. Aqui lo que no cubrian:
//
//   1) grave sintetico (DeepBassProvider): la perilla en vivo va en rampa de 20 ms hasta el valor exacto; al
//      empezar la cancion, la mezcla pedida desde la primera muestra (sin fundido); apagado sale intacto; al
//      volver a encenderlo sus filtros NO traen estado viejo; cambios repetidos y valores rotos; cualquier
//      tamano de lectura (tambien partiendo cuadros) da lo mismo; si la cancion empieza en cero, ni se calcula;
//   2) DSEE (SpectralTap): apagarlo a media cancion ya no se come la musica que tenia en su colchon; encenderlo
//      no la repite ni la corre; la banda recreada entra y sale con fundido; al empezar la cancion no hay
//      fundido; alternarlo muchas veces seguidas no pierde ni una muestra; la cancion puede acabar a media
//      transicion; con el encendido fijo, cualquier tamano de lectura da lo mismo.
//
// Lo que NO prueba: como se oye (escucha A/B a volumen igualado).
class FuenteT : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos, k; private readonly int[] trozos;
    public FuenteT(float[] datos, int fs, int ch, int[] trozos) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); this.trozos = trozos; }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    {
        if (trozos != null && trozos.Length > 0) c = Math.Min(c, trozos[k++ % trozos.Length]);
        int n = Math.Min(c, d.Length - pos);
        if (n <= 0) return 0;
        Array.Copy(d, pos, b, o, n); pos += n; return n;
    }
}

class Transiciones
{
    const BindingFlags T = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TBajo, TSp;
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static object Get(object o, string campo) { return o.GetType().GetField(campo, T).GetValue(o); }

    // Lee todo; antes de cada lectura llama a 'antes(proveedor, muestras ya leidas)'.
    static float[] LeeTodo(ISampleProvider p, int[] bloques, Action<object, long> antes, int largoMax)
    {
        List<float> r = new List<float>();
        float[] buf = new float[1 << 16];
        long hechas = 0; int k = 0;
        while (true)
        {
            if (antes != null) antes(p, hechas);
            int pide = bloques[k++ % bloques.Length];
            int n = p.Read(buf, 0, pide);
            if (n <= 0) break;
            for (int i = 0; i < n; i++) r.Add(buf[i]);
            hechas += n;
            if (r.Count > largoMax) break;
        }
        return r.ToArray();
    }

    // ---------------------------------------------------------------------------------------------
    //  Senales
    // ---------------------------------------------------------------------------------------------
    // Musica "tipo MP3" cortada en 16 kHz (para que el DSEE tenga que recrear), con grave de verdad.
    static float[] Musica(int fs, int ch, double seg, double corte, int semilla) { return Musica(fs, ch, seg, corte, semilla, true); }
    static float[] Musica(int fs, int ch, double seg, double corte, int semilla, bool golpes)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        Random r = new Random(semilla);
        int nt = 90; double[] w = new double[nt], fase = new double[nt], amp = new double[nt];
        for (int k = 0; k < nt; k++) { w[k] = 2 * Math.PI * (2000 + r.NextDouble() * (corte - 2200)) / fs; fase[k] = r.NextDouble() * 6.28; amp[k] = 0.3 + r.NextDouble(); }
        for (long i = 0; i < n; i++)
        {
            double t = (double)i / fs;
            double v = 0.45 * Math.Sin(2 * Math.PI * 45 * t) + 0.25 * Math.Sin(2 * Math.PI * 110 * t + Math.Sin(2 * t))
                     + 0.15 * Math.Sin(2 * Math.PI * 440 * t) + 0.1 * Math.Sin(2 * Math.PI * 1320 * t);
            double pl = 0;
            for (int k = 0; k < nt; k++) pl += amp[k] * Math.Sin(w[k] * i + fase[k]);
            double golpe = (golpes && (i % (fs / 2)) < fs / 40) ? Math.Exp(-(i % (fs / 2)) / (fs * 0.006)) : 0;
            v += 0.012 * pl * (0.5 + golpe);
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)(c == 1 ? v * 0.85 : (c >= 2 ? v * (0.6 + 0.1 * c) : v));
        }
        return x;
    }

    // ---------------------------------------------------------------------------------------------
    //  1) EL GRAVE SINTETICO
    // ---------------------------------------------------------------------------------------------
    static object Bajo(float[] x, int fs, int ch, float amount, int[] trozos)
    {
        return Activator.CreateInstance(TBajo, T, null, new object[] { new FuenteT(x, fs, ch, trozos), amount }, null);
    }
    static void PonAmount(object b, float v) { TBajo.GetField("Amount").SetValue(b, v); }
    // Lo que el grave agrego, leido en el canal 1, que va en SILENCIO: ahi la salida es exactamente lo agregado
    // (restar la entrada en float mete redondeos del mismo orden que lo que se quiere medir).
    static float[] Agregado(float[] y, int ch) { float[] a = new float[y.Length / ch]; for (int i = 0; i < a.Length; i++) a[i] = y[i * ch + 1]; return a; }
    static float[] ConCanalMudo(float[] x, int ch) { float[] y = (float[])x.Clone(); for (int i = 0; i < y.Length / ch; i++) y[i * ch + 1] = 0f; return y; }

    static void ElGrave()
    {
        Console.WriteLine("\n  1) EL GRAVE SINTETICO: LA PERILLA EN VIVO\n");
        int fs = 44100, ch = 2, rampa = (int)(fs * 0.020);
        float[] x = ConCanalMudo(Musica(fs, ch, 3.0, 16000, 1), ch);
        int[] b50 = { 2205 * ch };
        // Referencia: la perilla fija al maximo. Sus filtros siguen la musica sin parar; lo que agrega es su
        // grave por 0,85. Como los filtros no dependen de la mezcla, cualquier otra corrida con los filtros
        // vivos agrega EXACTAMENTE ese grave por su mezcla de cada momento.
        float[] yRef = LeeTodo((ISampleProvider)Bajo(x, fs, ch, 1f, null), b50, null, int.MaxValue);
        float[] aRef = Agregado(yRef, ch);

        // a) empieza en cero: intacto y sin calcular nada
        object b0 = Bajo(x, fs, ch, 0f, null);
        float[] y0 = LeeTodo((ISampleProvider)b0, b50, null, int.MaxValue);
        bool intacto = y0.Length == x.Length; for (int i = 0; i < x.Length && intacto; i++) if (y0[i] != x[i]) intacto = false;
        Check(intacto && !(bool)Get(b0, "vivo"), "empieza en cero: sale intacta bit a bit y sus filtros ni se calculan (cero coste)");

        // b) empieza encendido: la mezcla pedida desde la primera muestra
        object b1 = Bajo(x, fs, ch, 0.6f, null);
        float[] buf = new float[2205 * ch]; ((ISampleProvider)b1).Read(buf, 0, 16);
        Check((float)Get(b1, "mix") == 0.6f * 0.85f && (int)Get(b1, "quedan") == 0,
              "empieza encendido: desde la primera muestra con la mezcla pedida, sin rampa ni fundido que debilite el golpe");

        // c) cambio en vivo: rampa de 20 ms hasta el valor pedido exacto; d) apagar y volver a encender
        long cambio1 = (long)(0.8 * fs) * ch, cambio2 = (long)(1.6 * fs) * ch, cambio3 = (long)(2.2 * fs) * ch;
        object bv = Bajo(x, fs, ch, 0.3f, null);
        float[] yv = LeeTodo((ISampleProvider)bv, b50, delegate(object p, long hechas)
        {
            if (hechas == cambio1) PonAmount(p, 0.8f);
            if (hechas == cambio2) PonAmount(p, 0f);
            if (hechas == cambio3) PonAmount(p, 0.6f);
        }, int.MaxValue);
        float[] av = Agregado(yv, ch);
        // la mezcla de cada cuadro, vista desde fuera: lo agregado entre lo agregado de la referencia
        double peorPaso = 0, antes = 0, final1 = 0, final3 = 0; int medidas = 0; bool apagadoExacto = true, ceroExacto = true;
        long f1 = cambio1 / ch, f2 = cambio2 / ch, f3 = cambio3 / ch;
        double prev = double.NaN;
        for (long f = 1; f < av.Length; f++)
        {
            if (Math.Abs(aRef[f]) < 1e-6) { prev = double.NaN; continue; }
            double m = av[f] / aRef[f] * 0.85;   // la mezcla que se uso
            if (!double.IsNaN(prev)) peorPaso = Math.Max(peorPaso, Math.Abs(m - prev));
            prev = m; medidas++;
            if (f < f1) antes = Math.Max(antes, Math.Abs(m - 0.3 * 0.85));
            if (f > f1 + rampa + 2 && f < f2) final1 = Math.Max(final1, Math.Abs(m - 0.8 * 0.85));
            if (f > f3 + rampa + 2) final3 = Math.Max(final3, Math.Abs(m - 0.6 * 0.85));
        }
        for (long f = f2 + rampa + 2; f < f3; f++) if (yv[f * ch] != x[f * ch] || yv[f * ch + 1] != x[f * ch + 1]) { apagadoExacto = false; break; }
        for (long f = f2 + rampa + 2; f < f3; f++) if (av[f] != 0f) { ceroExacto = false; break; }
        double pasoMax = 0.85 * 0.8 / rampa;   // el cambio mas grande (de 0,8 a 0) repartido en la rampa
        Info("mezcla medida desde fuera: antes del cambio se aparta " + F(antes, "0.0e0") + "; mayor paso entre dos cuadros " + F(peorPaso, "0.0e0")
             + " (rampa de 20 ms: " + F(pasoMax, "0.0e0") + " como mucho); al acabar las rampas, " + F(final1, "0.0e0") + " y " + F(final3, "0.0e0"));
        Check(medidas > fs && antes < 1e-5, "antes de tocar la perilla, la mezcla es la de siempre");
        Check(peorPaso <= pasoMax * 1.02 + 1e-6, "al moverla, ningun escalon: la mezcla cambia en rampa de 20 ms, cuadro a cuadro");
        Check(final1 < 1e-5 && final3 < 1e-5, "y llega al valor pedido EXACTO (0,8 y luego 0,6)");
        Check(apagadoExacto && ceroExacto, "apagada: en cuanto acaba la rampa, sale intacta bit a bit");
        Check(final3 < 1e-5, "vuelta a encender: sus filtros siguieron la musica (lo agregado es el grave de verdad por su mezcla, sin estado viejo)");

        // NaN e infinito en la perilla, solos: no mueven nada
        object bn = Bajo(x, fs, ch, 0.6f, null);
        ((ISampleProvider)bn).Read(buf, 0, buf.Length);
        PonAmount(bn, float.NaN); ((ISampleProvider)bn).Read(buf, 0, buf.Length);
        bool nanQuieto = (float)Get(bn, "mix") == 0.6f * 0.85f && (int)Get(bn, "quedan") == 0;
        PonAmount(bn, float.PositiveInfinity); ((ISampleProvider)bn).Read(buf, 0, buf.Length);
        nanQuieto = nanQuieto && (float)Get(bn, "mix") == 0.6f * 0.85f && (int)Get(bn, "quedan") == 0;
        Check(nanQuieto, "un NaN o un infinito en la perilla no mueven la mezcla");

        // e) cambios repetidos, lecturas partidas y valores rotos
        Random r = new Random(5);
        object br = Bajo(x, fs, ch, 0.5f, new int[] { 1, 3, 1024, 7, 2205 * ch + 1 });
        float ultimo = 0.5f; bool sanos = true;
        float[] yr = LeeTodo((ISampleProvider)br, new int[] { 997, 64, 2 * 2205 * ch + 1 }, delegate(object p, long hechas)
        {
            if (hechas > x.Length - 20000) return;              // el ultimo cambio, con tiempo de sobra para su rampa
            double u = r.NextDouble();
            if (u < 0.25) { ultimo = (float)r.NextDouble(); PonAmount(p, ultimo); }
            else if (u < 0.30) PonAmount(p, float.NaN);       // un valor roto: se ignora
            else if (u < 0.35) { ultimo = 0f; PonAmount(p, 0f); }
        }, int.MaxValue);
        foreach (float v in yr) if (float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 4f) sanos = false;
        float mixFinal = (float)Get(br, "mix");
        Check(sanos && yr.Length == x.Length, "cientos de cambios seguidos, NaN en la perilla y lecturas partidas: nada roto, ninguna muestra de mas ni de menos");
        Check(mixFinal == (ultimo > 0.001f ? ultimo * 0.85f : 0f) && (int)Get(br, "quedan") == 0,
              "y termina EXACTO en el ultimo valor pedido (los NaN no movieron nada)");

        // f) cualquier tamano de lectura da lo mismo (perilla fija), tambien partiendo cuadros
        bool iguales = true; int casos = 0;
        foreach (int c in new int[] { 1, 2, 6 })
        {
            float[] xc = Musica(48000, c, 0.5, 16000, 2 + c);
            float[] refe = LeeTodo((ISampleProvider)Bajo(xc, 48000, c, 0.7f, null), new int[] { 4096 * c }, null, int.MaxValue);
            int[][] trozos = { new int[] { 1 }, new int[] { 7 }, new int[] { c * 5 + 1, 2 }, new int[] { 4096 * c + 1 } };
            int[][] lecturas = { new int[] { 1 }, new int[] { 3 }, new int[] { 2205 * c }, new int[] { 13, 4096 } };
            foreach (int[] t in trozos) foreach (int[] l in lecturas)
            {
                if (t[0] == 1 && l[0] == 1 && c == 6) continue;
                float[] y = LeeTodo((ISampleProvider)Bajo(xc, 48000, c, 0.7f, t), l, null, int.MaxValue);
                casos++;
                if (y.Length != refe.Length) { iguales = false; continue; }
                for (int i = 0; i < y.Length; i++) if (y[i] != refe[i]) { iguales = false; break; }
            }
        }
        Check(iguales, casos + " combinaciones de lecturas (de una muestra, partiendo cuadros, 1/2/6 canales): bit a bit lo mismo");

        // g) centrado: el mismo grave en todos los canales
        float[] x6 = Musica(fs, 6, 0.5, 16000, 9);
        float[] y6 = LeeTodo((ISampleProvider)Bajo(x6, fs, 6, 0.8f, null), new int[] { 4096 * 6 }, null, int.MaxValue);
        double difCanal = 0;
        for (int i = 0; i < y6.Length / 6; i++) { double a0 = y6[i * 6] - x6[i * 6]; for (int c = 1; c < 6; c++) difCanal = Math.Max(difCanal, Math.Abs((y6[i * 6 + c] - x6[i * 6 + c]) - a0)); }
        Check(difCanal < 1e-6, "el mismo grave en los seis canales (se aparta " + F(difCanal, "0.0e0") + ", solo redondeo)");

        // h) sin reservas al leer
        bool sinReservas = true;
        foreach (Codigo.Paso p in Codigo.Pasos(TBajo.GetMethod("Read", T))) if (p.Op == 0x8D || p.Op == 0x73) sinReservas = false;
        Check(sinReservas, "leer no reserva memoria");
    }

    // ---------------------------------------------------------------------------------------------
    //  2) EL DSEE
    // ---------------------------------------------------------------------------------------------
    static object Dsee(float[] x, int fs, int ch, int cuadro, bool activo, int[] trozos)
    {
        ISampleProvider fu = new FuenteT(x, fs, ch, trozos);
        ConstructorInfo c2 = TSp.GetConstructor(T, null, new Type[] { typeof(ISampleProvider), typeof(int) }, null);
        object sp = c2 != null ? c2.Invoke(new object[] { fu, cuadro }) : TSp.GetConstructor(T, null, new Type[] { typeof(ISampleProvider) }, null).Invoke(new object[] { fu });
        TSp.GetField("Active").SetValue(sp, activo);
        return sp;
    }
    static void Activa(object sp, bool v) { TSp.GetField("Active").SetValue(sp, v); }
    // Conmuta en 'corte'; si 'sinFundido', antes pone el paso del fundido en 1 (la misma cuenta, de golpe).
    static float[] Conmuta(float[] x, int fs, int ch, int cuadro, bool inicial, long corte, bool sinFundido)
    {
        object sp = Dsee(x, fs, ch, cuadro, inicial, null);
        return LeeTodo((ISampleProvider)sp, new int[] { (fs / 20) * ch }, delegate(object p, long h)
        {
            if (h > 0 && sinFundido) TSp.GetField("pasoRegen", T).SetValue(p, 1f);
            if (h == corte) Activa(p, !inicial);
        }, x.Length + 100000);
    }
    static int Cuadro(object sp) { return (int)TSp.GetField("Frame", T).GetValue(sp); }

    // Paso-bajo FIR (Blackman, 255 coeficientes, corte 8 kHz): se queda con lo de abajo y tira lo recreado (>16 kHz).
    static double[] PasoBajo(int fs)
    {
        int N = 255, c = N / 2; double[] h = new double[N]; double fc = 8000.0 / fs, s = 0;
        for (int n = 0; n < N; n++)
        {
            double t = n - c, sinc = t == 0 ? 2 * fc : Math.Sin(2 * Math.PI * fc * t) / (Math.PI * t);
            double w = 0.42 - 0.5 * Math.Cos(2 * Math.PI * n / (N - 1)) + 0.08 * Math.Cos(4 * Math.PI * n / (N - 1));
            h[n] = sinc * w; s += h[n];
        }
        for (int n = 0; n < N; n++) h[n] /= s;
        return h;
    }
    // Energia de (y - x) por debajo de 8 kHz respecto a la de x, en un tramo. El DSEE solo recrea por encima de su
    // corte: si ahi abajo aparece diferencia es que el audio se perdio, se repitio o se corrio en el tiempo.
    static double BajoDiferencia(float[] y, float[] x, int ch, int fs, long desde, long hasta)
    {
        double peor = -999;
        for (int c = 0; c < ch; c++) peor = Math.Max(peor, BajoDiferencia(y, x, ch, c, fs, desde, hasta));
        return peor;
    }
    static double BajoDiferencia(float[] y, float[] x, int ch, int canal, int fs, long desde, long hasta)
    {
        double[] h = PasoBajo(fs); int N = h.Length;
        double e = 0, ex = 0;
        for (long f = desde; f < hasta; f++)
        {
            double acc = 0;
            for (int k = 0; k < N; k++) { long g = f - N / 2 + k; if (g >= 0 && g < x.Length / ch && g * ch + canal < y.Length) acc += h[k] * (y[g * ch + canal] - x[g * ch + canal]); }
            e += acc * acc; ex += (double)x[f * ch + canal] * x[f * ch + canal];
        }
        return 10 * Math.Log10(Math.Max(e, 1e-30) / Math.Max(ex, 1e-30));
    }
    // Nivel de lo recreado (y - x) en ventanas de 5 ms.
    static double[] Envolvente(float[] y, float[] x, int ch, int fs, long desde, long hasta)
    {
        int v = fs / 200; List<double> r = new List<double>();
        for (long f0 = desde; f0 + v <= hasta; f0 += v)
        {
            double e = 0; for (long f = f0; f < f0 + v && f * ch < y.Length; f++) { double d = y[f * ch] - x[f * ch]; e += d * d; }
            r.Add(Math.Sqrt(e / v));
        }
        return r.ToArray();
    }
    // El fundido visto por dentro: el valor de 'regen' tras cada lectura corta, desde que se toca el interruptor.
    static List<float> Recorrido(object sp, float[] x, int ch, long desde, long hasta, bool encender)
    {
        ISampleProvider p = (ISampleProvider)sp; List<float> r = new List<float>();
        float[] b = new float[64 * ch]; long h = 0;
        while (h < hasta)
        {
            if (h >= desde && (bool)TSp.GetField("Active").GetValue(sp) != encender) Activa(sp, encender);
            int n = p.Read(b, 0, b.Length); if (n <= 0) break; h += n;
            if (h > desde) r.Add((float)Get(sp, "regen"));
        }
        return r;
    }
    // Tonos quietos (3, 6,1 y 8,3 kHz): el DSEE recrea su octava de arriba, y esa energia es estable en el tiempo,
    // asi que se puede ver desde fuera cuanto entra al encender.
    static float[] Quietos(int fs, int ch, double seg)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        for (long i = 0; i < n; i++)
        {
            double v = 0.3 * Math.Sin(2 * Math.PI * 3000.0 * i / fs) + 0.2 * Math.Sin(2 * Math.PI * 6100.0 * i / fs + 1) + 0.15 * Math.Sin(2 * Math.PI * 8300.0 * i / fs + 2);
            for (int c = 0; c < ch; c++) x[i * ch + c] = (float)v;
        }
        return x;
    }
    static double Energia(float[] y, float[] x, int ch, long f0, long f1)
    {
        double e = 0; for (long f = f0; f < f1; f++) { double d = y[f * ch] - x[f * ch]; e += d * d; } return e / Math.Max(1, f1 - f0);
    }

    static void ElDsee()
    {
        Console.WriteLine("\n  2) EL DSEE: ENCENDERLO Y APAGARLO EN VIVO\n");
        foreach (int cuadro in new int[] { 2048, 4096 })
            foreach (int fs in new int[] { 44100, 48000 })
            {
                int ch = 2;
                float[] x = Musica(fs, ch, 4.0, 16000, fs + cuadro, false);
                int[] b50 = { (fs / 20) * ch };
                object prueba = Dsee(x, fs, ch, cuadro, true, null);
                int cuadroReal = Cuadro(prueba);
                if (cuadroReal != cuadro) { Info("(esta rama solo tiene cuadros de " + cuadroReal + ": se salta " + cuadro + ")"); continue; }
                string que = cuadro + " puntos, " + F(fs / 1000.0, "0.0") + " kHz: ";

                // a) empieza encendido: sin fundido
                float[] yOn = LeeTodo((ISampleProvider)prueba, b50, null, x.Length + 100000);
                object pr = Dsee(x, fs, ch, cuadro, true, null); ((ISampleProvider)pr).Read(new float[b50[0]], 0, b50[0]);
                Check(yOn.Length == x.Length && (float)Get(pr, "regen") == 1f && (bool)TSp.GetProperty("Mejorando", T).GetValue(pr, null),
                      que + "empieza encendido: recrea desde el principio, sin fundido, y entrega todas las muestras");

                // b) apagarlo a media cancion
                long corte = (long)(1.5 * fs) * ch;
                float[] yOff = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, true, null), b50, delegate(object p, long h) { if (h == corte) Activa(p, false); }, x.Length + 100000);
                long fin = corte / ch + (long)(0.3 * fs);
                bool exacto = yOff.Length == x.Length;
                for (long i = fin * ch; i < yOff.Length && exacto; i++) if (yOff[i] != x[i]) exacto = false;
                double bajoOff = BajoDiferencia(yOff, x, ch, fs, corte / ch - fs / 5, fin);
                List<float> rOff = Recorrido(Dsee(x, fs, ch, cuadro, true, null), x, ch, corte, corte + (long)(0.4 * fs) * ch, false);
                float pasoR = (float)Get(pr, "pasoRegen"); int hop = (int)TSp.GetField("Hop", T).GetValue(pr);
                double msPaso = hop * 1000.0 / fs, msFundido = msPaso / pasoR;
                bool bajaBien = rOff.Count > 0 && rOff[rOff.Count - 1] == 0f; float maxBaja = 0f;
                for (int k = 1; k < rOff.Count; k++) { if (rOff[k] > rOff[k - 1]) bajaBien = false; maxBaja = Math.Max(maxBaja, rOff[k - 1] - rOff[k]); }
                Check(exacto, que + "apagado a media cancion: ni una muestra perdida; acabado el fundido sale la entrada tal cual, en su sitio");
                Check(bajoOff < -60, que + "  en la transicion no se corre nada: por debajo de 8 kHz la salida es la entrada (" + F(bajoOff, "0") + " dB)");
                float[] yOffB = Conmuta(x, fs, ch, cuadro, true, corte, true);
                bool mismoFinal = yOffB.Length == yOff.Length; for (long i = fin * ch; i < yOff.Length && mismoFinal; i++) if (yOff[i] != yOffB[i]) mismoFinal = false;
                Check(bajaBien && maxBaja <= pasoR + 1e-6 && msFundido >= 60,
                      que + "  la banda recreada se va con fundido de " + F(msFundido, "0") + " ms (" + F(pasoR, "0.00") + " por ventana de " + F(msPaso, "0") + " ms), sin saltos");
                Check(mismoFinal, que + "  y acaba igual, bit a bit, que apagandolo de golpe: el fundido no cambia el resultado final");

                // c) encenderlo a media cancion
                float[] yIn = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, false, null), b50, delegate(object p, long h) { if (h == corte) Activa(p, true); }, x.Length + 100000);
                object pc = Dsee(x, fs, ch, cuadro, false, null); float[] bc = new float[b50[0]];
                for (long h = 0; h < corte; h += b50[0]) ((ISampleProvider)pc).Read(bc, 0, b50[0]);
                Activa(pc, true); int hopC = (int)TSp.GetField("Hop", T).GetValue(pc);
                for (int k = 0; k < 3 * hopC * ch; k += 64 * ch) ((ISampleProvider)pc).Read(bc, 0, 64 * ch);
                float cutPrimero = (float)Get(pc, "cutHz");
                Check(cutPrimero > 14000f && cutPrimero < 17500f,
                      que + "encendido a media cancion, el corte se mide con la primera ventana de musica (" + F(cutPrimero, "0") + " Hz, la musica corta en 16 kHz): la ventana de arranque no lo falsea");
                bool antesIgual = yIn.Length == x.Length;
                for (long i = 0; i < corte && antesIgual; i++) if (yIn[i] != x[i]) antesIgual = false;
                double bajoIn = BajoDiferencia(yIn, x, ch, fs, corte / ch, corte / ch + fs / 2);
                List<float> rIn = Recorrido(Dsee(x, fs, ch, cuadro, false, null), x, ch, corte, corte + (long)(0.4 * fs) * ch, true);
                bool subeBien = rIn.Count > 0 && rIn[0] <= 2 * pasoR + 1e-6 && rIn[rIn.Count - 1] == 1f; float maxSube = 0f;
                for (int k = 1; k < rIn.Count; k++) { if (rIn[k] < rIn[k - 1]) subeBien = false; maxSube = Math.Max(maxSube, rIn[k] - rIn[k - 1]); }
                // desde fuera: contra el MISMO DSEE encendido de golpe (misma rejilla de ventanas), lo recreado en
                // los primeros 40 ms; y acabado el fundido, los dos bit a bit iguales
                float[] yInB = Conmuta(x, fs, ch, cuadro, false, corte, true);
                long s0 = corte / ch;
                double eIni = Energia(yIn, x, ch, s0, s0 + fs * 40 / 1000), eReg = Energia(yInB, x, ch, s0, s0 + fs * 40 / 1000);
                bool igualLuego = yInB.Length == yIn.Length; for (long i = (s0 + fs / 2) * ch; i < yIn.Length && igualLuego; i++) if (yIn[i] != yInB[i]) igualLuego = false;
                Check(antesIgual && bajoIn < -60, que + "encendido a media cancion: antes, intacta; despues, por debajo de 8 kHz la misma entrada (" + F(bajoIn, "0") + " dB): nada repetido ni corrido");
                Check(subeBien && maxSube <= pasoR + 1e-6,
                      que + "  la banda recreada entra con fundido, paso a paso (" + F(pasoR, "0.00") + " por ventana, " + rIn.Count + " lecturas)");
                Check(eReg > 0 && eIni < 0.35 * eReg && igualLuego,
                      que + "  en los primeros 40 ms recrea el " + F(eReg > 0 ? eIni / eReg * 100 : 0, "0") + " % de lo que recrearia encendido de golpe; acabado el fundido, bit a bit lo mismo");

                // d) alternarlo muchas veces seguidas, con lecturas y trozos partidos
                Random r = new Random(cuadro + fs);
                bool estado = true; long proximo = 0;
                float[] yAlt = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, true, new int[] { 4096, 1001, 3 }), new int[] { 441 * ch + 1, 2000 * ch, 17 },
                    delegate(object p, long h)
                    {
                        if (h >= proximo && h < (long)(3.0 * fs) * ch) { estado = !estado; Activa(p, estado); proximo = h + (long)((0.005 + r.NextDouble() * 0.06) * fs) * ch; }
                        if (h >= (long)(3.0 * fs) * ch) Activa(p, false);
                    }, x.Length + 100000);
                bool largoAlt = yAlt.Length == x.Length, sanoAlt = true;
                foreach (float v in yAlt) if (float.IsNaN(v) || float.IsInfinity(v)) sanoAlt = false;
                double bajoAlt = BajoDiferencia(yAlt, x, ch, fs, fs / 2, (long)(3.2 * fs));
                bool finAlt = largoAlt;
                for (long i = (long)(3.5 * fs) * ch; i < yAlt.Length && finAlt; i++) if (yAlt[i] != x[i]) finAlt = false;
                Check(largoAlt && sanoAlt && bajoAlt < -60 && finAlt,
                      que + "alternado cada 5-65 ms durante 3 s, con lecturas que parten cuadros: nada perdido, repetido ni cruzado (" + F(bajoAlt, "0") + " dB bajo 8 kHz); al final, intacta");

                // e) la cancion acaba a media transicion
                long casiFin = (long)(3.97 * fs) * ch;
                float[] yFin = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, true, null), b50, delegate(object p, long h) { if (h >= casiFin) Activa(p, false); }, x.Length + 100000);
                Check(yFin.Length == x.Length, que + "la cancion acaba mientras se apaga: sale entera, ni una muestra de mas ni de menos");

                // f) con el encendido fijo, el tamano de las lecturas no cambia nada, ni con cuadros partidos
                bool iguales = true;
                foreach (int[] t in new int[][] { new int[] { 1, 4096 }, new int[] { 3 * ch + 1 } })
                    foreach (int[] l in new int[][] { new int[] { 7 }, new int[] { 4096 * ch + 2 } })
                    {
                        float[] y = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, true, t), l, null, x.Length + 100000);
                        if (y.Length != yOn.Length) { iguales = false; continue; }
                        for (int i = 0; i < y.Length; i++) if (y[i] != yOn[i]) { iguales = false; break; }
                    }
                Check(iguales, que + "con el encendido fijo, lecturas y trozos de cualquier tamano (tambien partiendo cuadros): bit a bit lo mismo");
            }
        // Mono y tres canales (el tercero va solo, fuera de las parejas de la FFT): lo mismo, canal por canal.
        foreach (int ch in new int[] { 1, 3 })
        {
            int fs = 44100, cuadro = 2048;
            float[] x = Musica(fs, ch, 3.0, 16000, 40 + ch, false);
            int[] b50 = { (fs / 20) * ch };
            long corte = (long)(1.2 * fs) * ch;
            string que = ch + (ch == 1 ? " canal: " : " canales: ");
            float[] yOn = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, true, null), b50, null, x.Length + 100000);
            float[] yOff = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, true, null), b50, delegate(object p, long h) { if (h == corte) Activa(p, false); }, x.Length + 100000);
            long fin = corte / ch + (long)(0.3 * fs);
            bool exacto = yOff.Length == x.Length;
            for (long i = fin * ch; i < yOff.Length && exacto; i++) if (yOff[i] != x[i]) exacto = false;
            double bajo = BajoDiferencia(yOff, x, ch, fs, corte / ch - fs / 5, fin);
            Check(exacto && bajo < -60, que + "apagado a media cancion: nada perdido ni corrido en ningun canal (" + F(bajo, "0") + " dB) y luego intacta");
            Random r = new Random(ch); bool estado = true; long proximo = 0;
            float[] yAlt = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, true, new int[] { 4096, 1001, 5 }), new int[] { 441 * ch + 1, 1777, 19 },
                delegate(object p, long h)
                {
                    if (h >= proximo && h < (long)(2.2 * fs) * ch) { estado = !estado; Activa(p, estado); proximo = h + (long)((0.005 + r.NextDouble() * 0.06) * fs) * ch; }
                    if (h >= (long)(2.2 * fs) * ch) Activa(p, false);
                }, x.Length + 100000);
            bool finAlt = yAlt.Length == x.Length;
            for (long i = (long)(2.6 * fs) * ch; i < yAlt.Length && finAlt; i++) if (yAlt[i] != x[i]) finAlt = false;
            double bajoAlt = BajoDiferencia(yAlt, x, ch, fs, fs / 2, (long)(2.4 * fs));
            Check(finAlt && bajoAlt < -60, que + "alternado con lecturas que parten cuadros: nada perdido, repetido ni cruzado (" + F(bajoAlt, "0") + " dB) y al final intacta");
            bool iguales = true;
            foreach (int[] t in new int[][] { new int[] { 1, 4096 }, new int[] { 2 * ch + 1 } })
            {
                float[] y = LeeTodo((ISampleProvider)Dsee(x, fs, ch, cuadro, true, t), new int[] { 7 }, null, x.Length + 100000);
                if (y.Length != yOn.Length) { iguales = false; continue; }
                for (int i = 0; i < y.Length; i++) if (y[i] != yOn[i]) { iguales = false; break; }
            }
            Check(iguales, que + "con el encendido fijo, lecturas y trozos partidos: bit a bit lo mismo");
        }
        bool sinReservas = true;
        foreach (string m in new string[] { "Read", "ProcessFrame", "EmpiezaStft" })
        {
            MethodInfo mi = TSp.GetMethod(m, T);
            if (mi == null) { sinReservas = false; continue; }
            foreach (Codigo.Paso p in Codigo.Pasos(mi)) if (p.Op == 0x8D || p.Op == 0x73) sinReservas = false;
        }
        Check(sinReservas, "leer, procesar una ventana y ponerlo en marcha no reservan memoria");
    }

    // ---------------------------------------------------------------------------------------------
    //  3) LO QUE CUESTA
    // ---------------------------------------------------------------------------------------------
    static void Coste()
    {
        Console.WriteLine("\n  3) LO QUE CUESTA (esta maquina, Mono)\n");
        int fs = 44100, ch = 2, bloque = 2205 * ch;
        float[] x = Musica(fs, ch, 8, 16000, 3);
        string[] nombres = { "grave apagado desde el principio", "grave encendido", "grave apagado a media cancion (filtros vivos)" };
        double[] ns = new double[3];
        for (int k = 0; k < 3; k++)
        {
            double mejor = 1e9;
            for (int vuelta = 0; vuelta < 4; vuelta++)
            {
                object b = Bajo(x, fs, ch, k == 0 ? 0f : 0.5f, null);
                ISampleProvider p = (ISampleProvider)b; float[] buf = new float[bloque];
                if (k == 2) { p.Read(buf, 0, bloque); PonAmount(b, 0f); }
                Stopwatch sw = Stopwatch.StartNew();
                while (p.Read(buf, 0, bloque) > 0) { }
                sw.Stop();
                if (vuelta > 0) mejor = Math.Min(mejor, sw.Elapsed.TotalMilliseconds);
            }
            ns[k] = mejor * 1e6 / x.Length;
            Info(nombres[k] + ": " + F(ns[k], "0.0") + " ns por muestra (" + F(ns[k] * fs * ch / 1e6, "0.00") + " ms por segundo de musica estereo)");
        }
        Check(ns[0] < ns[1] * 0.3, "empezar en cero sigue siendo gratis frente a tenerlo encendido");
        Check(ns[2] <= ns[1] * 1.25, "en cero tras usarlo cuesta lo que encendido, no mas: sigue la musica para volver sin estado viejo (la siguiente cancion que empiece en cero vuelve a no costar nada)");
    }

    static int Main(string[] args)
    {
        Assembly a = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        TF = a.GetType("Rocola.Form1", true);
        TBajo = TF.GetNestedType("DeepBassProvider", T);
        TSp = TF.GetNestedType("SpectralTap", T);
        if (TBajo == null || TSp == null) { Check(false, "no estan el grave sintetico o el DSEE"); return 1; }
        ElGrave();
        ElDsee();
        Coste();
        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}

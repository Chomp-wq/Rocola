using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using NAudio.Wave;

// EL RECONSTRUCTOR DE AGUDOS (el DSEE): QUE SIGA SONANDO IGUAL, Y QUE CUESTE LA MITAD.
//
// Es lo mas caro de toda la cadena de sonido -el 73% del coste- y corre en el MISMO hilo que dibuja la
// pantalla, asi que cada milisegundo suyo es tiempo robado a los cuadros.
//
// El cambio: la musica es una senal REAL, y el espectro de una senal real es simetrico, o sea que media FFT
// compleja se estaba tirando a la basura. Metiendo el canal izquierdo en la parte real y el derecho en la
// imaginaria, UNA sola FFT trae los dos espectros, que luego se desenredan con esa misma simetria. Cuatro
// FFT por ventana pasan a ser dos.
//
// Eso es exacto sobre el papel, pero un error de un signo en el desenredo METERIA UN CANAL DENTRO DEL OTRO,
// que es justo lo que no se puede permitir. Por eso aqui no se comprueba "que suene parecido": se comprueban
// invariantes que un error de esos rompe a lo bestia.
class FuenteD : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public FuenteD(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    {
        int n = 0;
        while (n < c && pos < d.Length) { b[o + n] = d[pos]; pos++; n++; }
        return n;
    }
}

class Dsee
{
    const int FS = 44100;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TSbr;

    static object Crea(params object[] args)
    {
        foreach (ConstructorInfo ci in TSbr.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            ParameterInfo[] ps = ci.GetParameters();
            if (ps.Length < args.Length) continue;
            bool encaja = true;
            for (int i = 0; i < args.Length; i++)
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { encaja = false; break; }
            if (!encaja) continue;
            object[] full = new object[ps.Length];
            Array.Copy(args, full, args.Length);
            for (int i = args.Length; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != typeof(int)) { encaja = false; break; }
                full[i] = 2048;   // la rama de tarjeta grafica pide ademas el tamano de ventana
            }
            if (encaja) { try { return ci.Invoke(full); } catch { } }
        }
        return null;
    }

    // Una tanda de musica de mentira con la forma que importa: mucha energia abajo, poca arriba y un
    // ACANTILADO a 16 kHz (lo que deja un MP3 de 128k). Sin acantilado el DSEE no reconstruye nada.
    static float[] Mono(int muestras, int semilla, float amplitud)
    {
        float[] d = new float[muestras];
        Random r = new Random(semilla);
        double[] fase = new double[40];
        for (int k = 0; k < 40; k++) fase[k] = r.NextDouble() * 6.283;
        for (int i = 0; i < muestras; i++)
        {
            double t = (double)i / FS, v = 0;
            for (int k = 0; k < 40; k++)
            {
                double hz = 55.0 * Math.Pow(1.18, k);
                if (hz > 16000) break;
                v += Math.Sin(2 * Math.PI * hz * t + fase[k]) * (0.9 / (1.0 + hz / 300.0));
            }
            float s = (float)(v * 0.3 * amplitud);
            d[i] = (s > 1f) ? 1f : (s < -1f ? -1f : s);
        }
        return d;
    }

    static float[] Entrelaza(float[][] canales)
    {
        int ch = canales.Length, n = canales[0].Length;
        float[] d = new float[n * ch];
        for (int i = 0; i < n; i++) for (int c = 0; c < ch; c++) d[i * ch + c] = canales[c][i];
        return d;
    }

    // Pasa la musica por el reconstructor y devuelve la salida por canales.
    static float[][] Pasa(float[] entrelazado, int ch, bool activo, out object tap)
    {
        ISampleProvider fu = new FuenteD(entrelazado, FS, ch);
        tap = Crea(fu);
        ISampleProvider p = (ISampleProvider)tap;
        TSbr.GetField("Active").SetValue(tap, activo);
        List<float> salida = new List<float>(entrelazado.Length);
        float[] buf = new float[4096];
        int got;
        while ((got = p.Read(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < got; i++) salida.Add(buf[i]);
            // Un proveedor roto que inventase una cola infinita no puede colgar la bateria.
            if (salida.Count > entrelazado.Length + 8192 * ch) break;
        }
        int hecho = salida.Count;
        float[][] r = new float[ch][];
        for (int c = 0; c < ch; c++) { r[c] = new float[hecho / ch]; for (int i = 0; i < hecho / ch; i++) r[c][i] = salida[i * ch + c]; }
        return r;
    }

    static double MaxDif(float[] a, float[] b)
    {
        int n = Math.Min(a.Length, b.Length);
        double m = 0;
        for (int i = 0; i < n; i++) { double d = Math.Abs(a[i] - b[i]); if (d > m) m = d; }
        return m;
    }
    static double MaxAbs(float[] a) { double m = 0; for (int i = 0; i < a.Length; i++) { double d = Math.Abs(a[i]); if (d > m) m = d; } return m; }

    static double Mide(int veces, Action a)
    {
        a(); a();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    // Que metodos llama otro metodo, leyendo su codigo compilado. Sirve para comprobar que una funcion
    // sigue ENCHUFADA donde debe: un ayudante puede existir, estar documentado y no llamarlo nadie.

    // ---------- Para MEDIR lo que se oye en la banda recreada (seccion 11) ----------
    // Una FFT de base 2 en doble precision, solo para medir: la del reconstructor es la suya.
    static void FftD(double[] re, double[] im, bool inversa)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
        }
        for (int largo = 2; largo <= n; largo <<= 1)
        {
            int mitad = largo >> 1;
            double ang = 2 * Math.PI / largo * (inversa ? 1 : -1);
            for (int k = 0; k < mitad; k++)
            {
                double cr = Math.Cos(ang * k), ci = Math.Sin(ang * k);
                for (int i = k; i < n; i += largo)
                {
                    int b = i + mitad;
                    double xr = re[b] * cr - im[b] * ci, xi = re[b] * ci + im[b] * cr;
                    re[b] = re[i] - xr; im[b] = im[i] - xi; re[i] += xr; im[i] += xi;
                }
            }
        }
        if (inversa) for (int i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }
    }

    // Deja una senal (largo potencia de 2) solo entre hzMin y hzMax, con NADA encima: como la deja un codec.
    static double[] Recorta(double[] x, double hzMin, double hzMax)
    {
        int n = x.Length; double[] re = (double[])x.Clone(), im = new double[n];
        FftD(re, im, false);
        for (int k = 0; k < n; k++)
        {
            double hz = (double)(k <= n / 2 ? k : n - k) * FS / n;
            if (hz < hzMin || hz > hzMax) { re[k] = 0; im[k] = 0; }
        }
        FftD(re, im, true);
        return re;
    }
    // Ruido blanco reproducible entre 200 Hz y hzMax, a ese RMS.
    static float[] RuidoHasta(int n, int semilla, double hzMax, double rms)
    {
        Random r = new Random(semilla);
        double[] g = new double[n];
        for (int i = 0; i < n; i++)
        { double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble(); g[i] = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2); }
        double[] re = Recorta(g, 200, hzMax);
        double s = 0; for (int i = 0; i < n; i++) s += re[i] * re[i];
        double esc = rms / Math.Sqrt(s / n);
        float[] x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(re[i] * esc);
        return x;
    }

    // Potencia media por bin (ventanas de 8192 con solape de la mitad), desde la muestra 'desde'.
    static double[] Psd(float[] y, int desde)
    {
        const int n = 8192; double[] acc = new double[n / 2 + 1]; int veces = 0;
        double[] re = new double[n], im = new double[n];
        for (int i = desde; i + n <= y.Length; i += n / 2)
        {
            for (int k = 0; k < n; k++) { re[k] = y[i + k] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * k / n)); im[k] = 0; }
            FftD(re, im, false);
            for (int k = 0; k <= n / 2; k++) acc[k] += re[k] * re[k] + im[k] * im[k];
            veces++;
        }
        for (int k = 0; k <= n / 2; k++) acc[k] /= Math.Max(1, veces);
        return acc;
    }
    // dB de la MEDIA de una banda, y de su MEDIANA (el piso de ruido: un tono no la mueve).
    static double Nivel(double[] psd, double lo, double hi)
    {
        double s = 0; int c = 0;
        for (int k = (int)Math.Ceiling(lo * 8192 / FS); k * (double)FS / 8192 < hi; k++) { s += psd[k]; c++; }
        return 10 * Math.Log10(s / c + 1e-30);
    }
    // El piso de ruido de la banda recreada contra otro, por 24avos de octava de 12,2 a 20,4 kHz: lo que mas se
    // aparta. Tan finos para que una sub-banda que se hunda no quede escondida en la mediana de una banda ancha.
    static double PeorPiso(double[] a, double[] b)
    {
        double peor = 0;
        for (double lo = 12200; lo < 20400; lo *= Math.Pow(2, 1 / 24.0))
        {
            double hi = Math.Min(20400, lo * Math.Pow(2, 1 / 24.0));
            peor = Math.Max(peor, Math.Abs(Piso(a, lo, hi) - Piso(b, lo, hi)));
        }
        return peor;
    }
    static double Piso(double[] psd, double lo, double hi)
    {
        List<double> v = new List<double>();
        for (int k = (int)Math.Ceiling(lo * 8192 / FS); k * (double)FS / 8192 < hi; k++) v.Add(psd[k]);
        v.Sort(); return 10 * Math.Log10(v[v.Count / 2] + 1e-30);
    }

    // Cuanto de la energia de un tono recreado es el tono y cuanto bandas laterales: el pico mas fuerte entre
    // 12,2 y 20,4 kHz, la energia a +-2 Hz de el entre la de +-250 Hz (ventana Blackman-Harris de 2^17).
    static double Pureza(float[] y, int desde, out double f0)
    {
        int n = 1 << 17; double[] re = new double[n], im = new double[n];
        for (int k = 0; k < n; k++)
        {
            double a = 2 * Math.PI * k / (n - 1);
            re[k] = y[desde + k] * (0.35875 - 0.48829 * Math.Cos(a) + 0.14128 * Math.Cos(2 * a) - 0.01168 * Math.Cos(3 * a));
        }
        FftD(re, im, false);
        double hz = (double)FS / n; int lo = (int)(12200 / hz), hi = (int)(20400 / hz), kp = lo; double pm = 0;
        for (int k = lo; k < hi; k++) { double p = re[k] * re[k] + im[k] * im[k]; if (p > pm) { pm = p; kp = k; } }
        f0 = kp * hz;
        double limpio = 0, total = 0; int ancho = (int)(250 / hz);
        for (int k = kp - ancho; k <= kp + ancho; k++)
        { double p = re[k] * re[k] + im[k] * im[k]; total += p; if (Math.Abs(k - kp) * hz <= 2.0) limpio += p; }
        return 100 * limpio / total;
    }

    // Cuanto salta (dB, desviacion) la energia de una banda de una ventana de ~46 ms a la siguiente.
    static double Vaiven(float[] y, int desde, double lo, double hi)
    {
        const int n = 2048; double[] re = new double[n], im = new double[n];
        double prev = double.NaN, s = 0, s2 = 0; int c = 0;
        for (int i = desde; i + n <= y.Length; i += n / 2)
        {
            for (int k = 0; k < n; k++) { re[k] = y[i + k] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * k / n)); im[k] = 0; }
            FftD(re, im, false);
            double e = 0; for (int k = (int)(lo * n / FS); k < (int)(hi * n / FS); k++) e += re[k] * re[k] + im[k] * im[k];
            double db = 10 * Math.Log10(e + 1e-30);
            if (!double.IsNaN(prev)) { double d = db - prev; s += d; s2 += d * d; c++; }
            prev = db;
        }
        double m = s / c; return Math.Sqrt(Math.Max(0, s2 / c - m * m));
    }

    // Un reconstructor mono con la ventana pedida (4096 solo existe en la rama que reproduce con ella).
    static object CreaVentana(ISampleProvider fu, int ventana)
    {
        foreach (ConstructorInfo ci in TSbr.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            ParameterInfo[] ps = ci.GetParameters();
            if (ps.Length == 2 && ps[0].ParameterType.IsInstanceOfType(fu) && ps[1].ParameterType == typeof(int))
            {
                object t = ci.Invoke(new object[] { fu, ventana });
                return (int)TSbr.GetProperty("FrameLen").GetValue(t, null) == ventana ? t : null;
            }
        }
        return ventana == 2048 ? Crea(fu) : null;
    }
    // Pasa un mono (o un intercalado de 'canales') por el reconstructor encendido, de 1024 en 1024 muestras, y
    // anota el corte con el que recrea despues de cada lectura (cortes[i]; -1 si esta version no tiene ese campo).
    static float[] PasaVentana(float[] x, int ventana, out object tap, List<int> cortes)
    { return PasaVentana(x, ventana, out tap, cortes, 1); }
    static float[] PasaVentana(float[] x, int ventana, out object tap, List<int> cortes, int canales)
    {
        tap = CreaVentana(new FuenteD(x, FS, canales), ventana);
        if (tap == null) return null;
        TSbr.GetField("Active").SetValue(tap, true);
        FieldInfo fCorte = TSbr.GetField("corteRecreado", BindingFlags.Instance | BindingFlags.NonPublic);
        ISampleProvider p = (ISampleProvider)tap;
        float[] y = new float[x.Length]; float[] buf = new float[1024]; int hecho = 0, got;
        while (hecho < y.Length && (got = p.Read(buf, 0, Math.Min(buf.Length, y.Length - hecho))) > 0)
        {
            Array.Copy(buf, 0, y, hecho, got); hecho += got;
            if (cortes != null) cortes.Add(fCorte != null ? (int)fCorte.GetValue(tap) : -1);
        }
        return y;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        TSbr = TF.GetNestedType("SpectralTap", BindingFlags.NonPublic | BindingFlags.Public);
        Check(TSbr != null, "el reconstructor de agudos esta donde tiene que estar");
        if (TSbr == null) return 1;
        FieldInfo fCut = TSbr.GetField("cutHz", BindingFlags.Instance | BindingFlags.NonPublic);

        Console.WriteLine("\n  0) EL ULTIMO PEDAZO NO SE PIERDE\n");
        // La FFT trabaja en saltos de 1024 cuadros. Una cancion casi nunca termina justo ahi: el ultimo
        // pedazo debe rellenarse con ceros para procesarlo, pero publicarse con su largo REAL. Antes, todo
        // lo que quedaba por debajo de un salto desaparecia al llegar EOF.
        foreach (int cuadros in new int[] { 1, 17, 1023, 1024, 1025, 2047, 2051, 3073 })
        {
            float[] corto = new float[cuadros * 2];
            for (int i = 0; i < corto.Length; i++) corto[i] = (float)((i % 97) - 48) / 1000f;
            object tapCorto;
            float[][] saleCorto = Pasa(corto, 2, true, out tapCorto);
            Check(saleCorto[0].Length == cuadros && saleCorto[1].Length == cuadros,
                  cuadros + " cuadros entran y salen completos, aunque el final no llene una ventana");
        }

        const int N = FS * 3;                     // 3 segundos
        float[] L = Mono(N, 11, 1.0f);
        float[] mitad = new float[N]; for (int i = 0; i < N; i++) mitad[i] = L[i] * 0.5f;
        float[] silencio = new float[N];
        object t0;

        Console.WriteLine("\n  1) APAGADO ES UN PASE DIRECTO (bit a bit)\n");
        float[] est = Entrelaza(new float[][] { L, mitad });
        float[][] off = Pasa(est, 2, false, out t0);
        Check(MaxDif(off[0], L) == 0.0 && MaxDif(off[1], mitad) == 0.0,
              "con el DSEE apagado la musica sale IDENTICA, sin tocar un solo bit");

        Console.WriteLine("\n  2) NO SE CUELA UN CANAL EN EL OTRO\n");
        // Con los dos canales dentro de la MISMA FFT, un error de signo al desenredarlos meteria el izquierdo
        // dentro del derecho. Se manda musica por la izquierda y SILENCIO por la derecha: si algo se cuela,
        // la derecha deja de estar callada y se ve al instante.
        float[][] sd = Pasa(Entrelaza(new float[][] { L, silencio }), 2, true, out t0);
        double fuga = MaxAbs(sd[1]), senal = MaxAbs(sd[0]);
        Console.WriteLine("     izquierda con musica: pico " + senal.ToString("0.0000")
                          + "   derecha en silencio: pico " + fuga.ToString("0.00000000"));
        Check(senal > 0.05, "por la izquierda sale musica (si no, esta prueba no probaria nada)");
        Check(fuga < 1e-6, "por la derecha no se cuela NADA de la izquierda");

        // Y al reves, por si el error fuera del otro lado.
        float[][] ds = Pasa(Entrelaza(new float[][] { silencio, L }), 2, true, out t0);
        Console.WriteLine("     al reves: derecha con musica pico " + MaxAbs(ds[1]).ToString("0.0000")
                          + "   izquierda en silencio pico " + MaxAbs(ds[0]).ToString("0.00000000"));
        Check(MaxAbs(ds[0]) < 1e-6, "y por la izquierda tampoco se cuela nada de la derecha");

        Console.WriteLine("\n  3) CADA CANAL SE TRATA POR SU CUENTA\n");
        // La derecha lleva la MISMA musica a la mitad de volumen. El reconstructor mide proporciones, asi que
        // debe devolver exactamente la mitad. Si los canales se mezclaran, esta proporcion se rompe.
        float[][] mix = Pasa(Entrelaza(new float[][] { L, mitad }), 2, true, out t0);
        double peor = 0;
        for (int i = 0; i < mix[0].Length; i++) { double d = Math.Abs(mix[1][i] - mix[0][i] * 0.5); if (d > peor) peor = d; }
        Console.WriteLine("     la derecha deberia ser la mitad exacta de la izquierda; se desvia como mucho " + peor.ToString("0.00000000"));
        Check(peor < 1e-5, "el canal a mitad de volumen sale a la mitad exacta (no hay mezcla entre canales)");

        Console.WriteLine("\n  4) ESTEREO Y MONO DAN LO MISMO\n");
        // El camino de dos canales (una FFT para los dos) y el de un canal suelto son CODIGO DISTINTO.
        // Con la misma musica tienen que dar el mismo resultado: asi cada uno vigila al otro.
        float[][] mo = Pasa(L, 1, true, out t0);
        float[][] es2 = Pasa(Entrelaza(new float[][] { L, L }), 2, true, out t0);
        double dif = MaxDif(mo[0], es2[0]);
        // El limite es la DECIMA PARTE del ultimo bit de 16, que es la resolucion real de la tarjeta de
        // sonido: por debajo de ahi no hay nada que oir. No se pide que salga bit a bit igual porque con los
        // dos canales dentro de una misma FFT el redondeo de los flotantes ya no puede recorrer el mismo
        // camino; lo que se exige es que la diferencia se quede muy por debajo de lo audible.
        double lsb = 1.0 / 32768;
        Console.WriteLine("     mismo audio por mono y por estereo: se desvian como mucho " + dif.ToString("0.00000000")
                          + "   (el ultimo bit de 16 vale " + lsb.ToString("0.00000000") + ")");
        Check(dif < lsb / 10, "el camino de estereo da lo mismo que el de mono, muy por debajo del ultimo bit");
        Check(MaxDif(es2[0], es2[1]) < lsb / 10, "y los dos canales de un estereo identico salen practicamente identicos");

        Console.WriteLine("\n  5) UNA CANCION COMPLETA NO SE TOCA\n");
        // Sin acantilado (WAV o 320k) el DSEE debe pasar la musica INTACTA: no inventar agudos donde ya los hay.
        float[] completa = new float[N];
        Random rr = new Random(7);
        for (int i = 0; i < N; i++)
        {
            double t = (double)i / FS, v = 0;
            for (int k = 0; k < 60; k++) { double hz = 55.0 * Math.Pow(1.105, k); if (hz > 21000) break; v += Math.Sin(2 * Math.PI * hz * t + k) * (0.9 / (1.0 + hz / 2000.0)); }
            v += (rr.NextDouble() - 0.5) * 0.05;
            completa[i] = (float)Math.Max(-1, Math.Min(1, v * 0.2));
        }
        float[][] cs = Pasa(Entrelaza(new float[][] { completa, completa }), 2, true, out t0);
        float cutC = (float)fCut.GetValue(t0);
        // El solape-y-suma NO desplaza la musica: las dos ventanas raiz-de-Hann suman exactamente 1, asi que
        // la muestra n sale en el sitio n. Solo se saltan las primeras, que aun no recibieron sus dos solapes.
        int cola = 4096;
        double difC = 0;
        for (int i = cola; i < cs[0].Length - cola; i++) { double d = Math.Abs(cs[0][i] - completa[i]); if (d > difC) difC = d; }
        Console.WriteLine("     acantilado medido " + cutC.ToString("0") + " Hz (>=19300 = completa)   se desvia como mucho " + difC.ToString("0.0000000"));
        Check(cutC >= 19300f, "una cancion con todos sus agudos se reconoce como completa");
        Check(difC < 1e-4, "y sale tal cual entro: no se le inventa nada");

        Console.WriteLine("\n  6) A UNA CANCION POBRE SI SE LE RECONSTRUYE\n");
        float[][] pobre = Pasa(Entrelaza(new float[][] { L, L }), 2, true, out t0);
        float cutP = (float)fCut.GetValue(t0);
        PropertyInfo pMej = TSbr.GetProperty("Mejorando");
        bool mejorando = (bool)pMej.GetValue(t0, null);
        Console.WriteLine("     acantilado medido " + cutP.ToString("0") + " Hz (la musica de prueba corta a 16000)");
        Check(cutP > 14000f && cutP < 19300f, "se localiza el acantilado del codec donde de verdad esta");
        Check(mejorando, "y la cancion queda marcada como MEJORADA");
        Check(MaxDif(pobre[0], L) > 1e-4, "la salida no es igual a la entrada: de verdad se le anadio algo arriba");

        Console.WriteLine("\n  7) LO QUE CUESTA\n");
        int bloque = FS * 2 / 20;                 // 50 ms de estereo, que es el bloque real de la rocola
        float[] buf = new float[bloque];
        object tOn, tOff;
        ISampleProvider pOn = (ISampleProvider)(tOn = null);
        float[] largo = Entrelaza(new float[][] { Mono(FS * 30, 3, 1f), Mono(FS * 30, 4, 0.9f) });
        ISampleProvider fuOn = new FuenteD(largo, FS, 2);
        tOn = Crea(fuOn); TSbr.GetField("Active").SetValue(tOn, true); pOn = (ISampleProvider)tOn;
        ISampleProvider fuOff = new FuenteD(largo, FS, 2);
        tOff = Crea(fuOff); TSbr.GetField("Active").SetValue(tOff, false);
        ISampleProvider pOff = (ISampleProvider)tOff;
        double msOn = Mide(60, delegate { pOn.Read(buf, 0, bloque); });
        double msOff = Mide(60, delegate { pOff.Read(buf, 0, bloque); });
        Console.WriteLine("     bloque de 50 ms: encendido " + msOn.ToString("0.000") + " ms, apagado " + msOff.ToString("0.000") + " ms");
        Console.WriteLine("     por segundo de musica: " + (msOn * 20).ToString("0.00") + " ms  ("
                          + (msOn * 20 / 10).ToString("0.0") + "% de un nucleo en esta maquina)");
        Check(msOff < msOn * 0.2, "apagado es de verdad un pase directo (cuesta una fraccion de encendido)");

        Console.WriteLine("\n  8) EL REALCE DE AGUDOS NO EMPUJA DOS VECES LO MISMO\n");
        // El realce le da MAS brillo a una cancion dañada justamente por estar dañada. El reconstructor le
        // re-crea la banda por la misma razon. Y en la cadena el realce va DESPUES, asi que si los dos van a
        // su aire acaba echandole brillo encima a unos agudos que ya no faltaban.
        object F = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF);
        FieldInfo fEq = TF.GetField("eqCache", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fOn = TF.GetField("sbrOn", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fAll = TF.GetField("sbrAllowed", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mExc = TF.GetMethod("ExciteFactorFor", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fEq == null || fOn == null || fAll == null || mExc == null)
        {
            Check(false, "se puede consultar cuanto realce le toca a cada cancion");
        }
        else
        {
            int iCut = (int)TF.GetField("EqCut", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int len = (int)TF.GetField("EqLen", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            System.Collections.Generic.Dictionary<string, float[]> cache = new System.Collections.Generic.Dictionary<string, float[]>();
            fEq.SetValue(F, cache);
            fAll.SetValue(F, true);
            Func<string, float, bool, float> factor = delegate (string ruta, float corte, bool dsee)
            {
                float[] c = new float[len];
                for (int i = 0; i < len; i++) c[i] = -30f;
                c[iCut] = corte;
                cache[ruta] = c;
                fOn.SetValue(F, dsee);
                return (float)mExc.Invoke(F, new object[] { ruta });
            };
            float danadaSin = factor("a.mp3", 15000f, false), danadaCon = factor("a.mp3", 15000f, true);
            float mediaSin = factor("b.mp3", 17000f, false), mediaCon = factor("b.mp3", 17000f, true);
            float sanaSin = factor("c.mp3", 20000f, false), sanaCon = factor("c.mp3", 20000f, true);
            Console.WriteLine("     realce que le toca a cada cancion (reconstructor apagado / encendido):");
            Console.WriteLine("        cortada a 15000 Hz (muy dañada): " + danadaSin.ToString("0.00") + "  /  " + danadaCon.ToString("0.00"));
            Console.WriteLine("        cortada a 17000 Hz:              " + mediaSin.ToString("0.00") + "  /  " + mediaCon.ToString("0.00"));
            Console.WriteLine("        completa (20000 Hz):             " + sanaSin.ToString("0.00") + "  /  " + sanaCon.ToString("0.00"));
            Check(danadaSin > 1.2f, "con el reconstructor apagado, una cancion dañada si recibe realce de sobra");
            Check(danadaCon < danadaSin, "con el reconstructor encendido recibe MENOS: los agudos ya se los devolvio el");
            Check(danadaCon > sanaCon, "pero no se le quita del todo (lo reconstruido no es el detalle original)");
            Check(Math.Abs(sanaSin - sanaCon) < 0.001f, "y a una cancion COMPLETA no le cambia nada: el reconstructor ni la toca");
        }

        Console.WriteLine("\n  9) LA TANGENTE RAPIDA DEL REALCE SIGUE ENCHUFADA\n");
        // El realce de agudos satura la banda alta con una tangente hiperbolica, UNA VEZ POR MUESTRA Y CANAL
        // (88.200 veces por segundo). Hay una version aproximada bastante mas barata hecha justo para eso...
        // que estaba escrita, documentada y SIN QUE LA LLAMARA NADIE: el realce seguia usando la de la
        // libreria matematica. Aqui se comprueba las dos cosas: que se parece lo suficiente, y que de verdad
        // se llama desde donde toca.
        MethodInfo mTanh = TF.GetMethod("TanhRapida", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check(mTanh != null, "existe la tangente rapida");
        if (mTanh != null)
        {
            double peorT = 0; float xPeor = 0;
            for (float x = -6f; x <= 6f; x += 0.002f)
            {
                float ap = (float)mTanh.Invoke(null, new object[] { x });
                double d = Math.Abs(ap - Math.Tanh(x));
                if (d > peorT) { peorT = d; xPeor = x; }
            }
            Console.WriteLine("     se aparta de la de verdad como mucho " + peorT.ToString("0.0000") + " (en x=" + xPeor.ToString("0.00") + ")");
            Check(peorT < 0.03, "dibuja practicamente la misma curva");
            Check((float)mTanh.Invoke(null, new object[] { float.NaN }) == -1f, "y un NaN no la atraviesa (un archivo roto no deja el sonido pegado)");

            Type TLvl = TF.GetNestedType("LevelTap", BindingFlags.NonPublic | BindingFlags.Public);
            MethodInfo mRead = (TLvl != null) ? TLvl.GetMethod("Read") : null;
            Check(mRead != null && Codigo.Llama(mRead, "TanhRapida"), "el realce de agudos LA LLAMA de verdad");
            Check(mRead != null && !Codigo.Llama(mRead, "Tanh"), "y ya no llama a la de la libreria");

            // La red neuronal SI debe seguir con la de verdad: se entreno con ella.
            Type TRed = TF.GetNestedType("RedChica", BindingFlags.NonPublic | BindingFlags.Public);
            MethodInfo mPred = (TRed != null) ? TRed.GetMethod("Predict") : null;
            Check(mPred != null && Codigo.Llama(mPred, "Tanh"), "la red neuronal, en cambio, sigue con la tangente exacta (se entreno con ella)");
        }

        // ============================================================================================
        Console.WriteLine("\n  10) EL MODELO QUE SE ENTREGA CON LA ROCOLA SIRVE\n");
        // El modelo se entrena FUERA, en otra PC, y vuelve como un archivo suelto. Si viniera de otro
        // entrenamiento, a medias, o con otra forma, la rocola simplemente no lo cargaria y seguiria con su
        // curva fija: no se rompe nada, pero el dueno se queda sin lo que estuvo horas entrenando y sin saber
        // por que. Al entrenar sale ademas un archivo de vectores de prueba -entradas y las salidas que dio el
        // entrenador-, asi que aqui se pasan por la red DE LA ROCOLA y se comprueba que dan lo mismo. Eso
        // valida las dos cosas a la vez: que el modelo esta entero y que la rocola lo calcula igual que Python.
        {
            string mod = Path.Combine("..", Path.Combine("ia", "rocola_ia.bin"));
            string vec = Path.Combine("..", Path.Combine("ia", "ia_vectores_prueba.txt"));
            Check(File.Exists(mod), "se entrega un modelo entrenado junto a la rocola");
            Check(File.Exists(vec), "y los vectores de prueba con los que comprobarlo");
            Type TRed2 = TF.GetNestedType("RedChica", BindingFlags.NonPublic | BindingFlags.Public);
            if (File.Exists(mod) && File.Exists(vec) && TRed2 != null)
            {
                object red = Activator.CreateInstance(TRed2, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                                      null, new object[] { 28, 12 }, null);
                TRed2.GetMethod("TryLoad").Invoke(red, new object[] { Path.GetFullPath(mod) });
                bool cargo = (bool)TRed2.GetField("Loaded").GetValue(red);
                Check(cargo, "la rocola lo carga (si no, se quedaria con su curva fija y nadie se enteraria)");
                if (cargo)
                {
                    MethodInfo mP = TRed2.GetMethod("Predict");
                    int n = 0; double peorVec = 0;
                    foreach (string linea in File.ReadAllLines(vec))
                    {
                        int bar = linea.IndexOf('|'); if (bar <= 0) continue;
                        string[] ent = linea.Substring(0, bar).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        string[] sal = linea.Substring(bar + 1).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (ent.Length != 28 || sal.Length != 12) continue;
                        float[] x = new float[28], y = new float[12];
                        for (int i = 0; i < 28; i++) x[i] = float.Parse(ent[i], CultureInfo.InvariantCulture);
                        mP.Invoke(red, new object[] { x, y });
                        for (int i = 0; i < 12; i++)
                        { double d = Math.Abs(y[i] - double.Parse(sal[i], CultureInfo.InvariantCulture)); if (d > peorVec) peorVec = d; }
                        n++;
                    }
                    // Desde el entrenador nuevo, la primera linea dice de QUE modelo son (su SHA-256). Si esta,
                    // tiene que ser el que hay al lado: unos vectores de otro modelo no prueban este.
                    string huella = null;
                    foreach (string l in File.ReadAllLines(vec))
                        if (l.StartsWith("#") && l.IndexOf("sha256=") > 0)
                        { huella = l.Substring(l.IndexOf("sha256=") + 7).Split(' ')[0].Trim(); break; }
                    if (huella == null)
                        Console.WriteLine("     (vectores del formato anterior: no dicen de que modelo son)");
                    else
                    {
                        string real;
                        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                            real = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(mod))).Replace("-", "").ToLowerInvariant();
                        Check(real == huella, "los vectores dicen ser de este mismo modelo (SHA-256 " + huella.Substring(0, Math.Min(12, huella.Length)) + "...)");
                    }
                    Console.WriteLine("     " + n + " vectores de prueba; la mayor diferencia con lo que dijo el entrenador: " + peorVec.ToString("0.0000000"));
                    Check(n >= 8, "hay vectores de prueba suficientes para fiarse");
                    Check(peorVec < 0.001, "y la rocola calcula lo MISMO que el entrenador: el modelo llego entero");
                }
            }
        }

        // ============================================================================================
        Console.WriteLine("\n  11) LA COPIA POR BLOQUES: UN TONO SALE LIMPIO, CON EL BRILLO DE LA CURVA Y SIN TEMBLOR\n");
        // Antes cada bin de arriba copiaba al de la mitad de su frecuencia, con el corte bailando de una ventana a
        // otra y la escala medida en 6 bins sin suavizar: un tono de 7 kHz recreado salia con bandas laterales casi
        // tan fuertes como el (54 % limpio con ventanas de 2048, 11 % con las de 4096). Ahora se copia por bloques
        // con la fase seguida entre ventanas (23 de septiembre de 2026). Aqui se mide lo que se oye: la limpieza del
        // tono, el nivel contra la curva de diseno, el ruido alrededor de un tono, los golpes entre silencios, el
        // temblor y que el corte usado no baile. Con la version de antes, estas comprobaciones fallan.
        {
            const int NS = 1 << 18;                   // ~6 s
            int desde = (int)(0.7 * FS);              // el arranque del STFT y del corte, fuera
            float[] ruido = RuidoHasta(NS, 5, 12000, 0.03);
            // Para los golpes, un ruido INCLINADO (9 dB menos por encima de 9 kHz: la costura queda mas baja que el
            // arranque de la fuente) y, entre golpes, un hilo de 11,78 kHz a -50 dBFS justo en la costura: en las
            // pausas la escala que se mediria seria la del tope, mucho mas alta que la de los golpes.
            double[] gT = new double[NS]; Random rT = new Random(9);
            for (int i = 0; i < NS; i++) { double u1 = 1.0 - rT.NextDouble(), u2 = rT.NextDouble(); gT[i] = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2); }
            double[] ruidoT = Recorta(gT, 200, 12000), agudo = Recorta(gT, 9000, 12000);
            double sT = 0; for (int i = 0; i < NS; i++) { ruidoT[i] -= agudo[i] * (1 - Math.Pow(10, -9 / 20.0)); sT += ruidoT[i] * ruidoT[i]; }
            double escT = 0.03 / Math.Sqrt(sT / NS);
            float[] tono = new float[NS]; double[] golpes = new double[NS], seguido = new double[NS];
            for (int i = 0; i < NS; i++)
            {
                double t = (double)i / FS;
                tono[i] = ruido[i] + (float)(0.3 * Math.Sin(2 * Math.PI * 7000 * t));
                // charles en corcheas: golpes de ruido cada 250 ms (2 ms de subida, 15 ms de caida y CERRADOS a los
                // 65 ms), y entre golpes solo un bajo que nunca calla: en las pausas no queda nada de 4 a 8 kHz, como en
                // un MP3 con charles y bajo. Todo cortado en 12 kHz DESPUES, como lo deja el codec (el arranque de un
                // golpe tambien tiene agudos, y el MP3 se los quita).
                double enGolpe = t % 0.25, sube = enGolpe < 0.002 ? 0.5 - 0.5 * Math.Cos(Math.PI * enGolpe / 0.002) : 1.0;
                double cierra = enGolpe < 0.06 ? 1.0 : (enGolpe < 0.065 ? 0.5 + 0.5 * Math.Cos(Math.PI * (enGolpe - 0.06) / 0.005) : 0.0);
                double fondo = 0.2 * Math.Sin(2 * Math.PI * 100 * t) + 0.003 * Math.Sin(2 * Math.PI * 11780 * t);
                golpes[i] = ruidoT[i] * escT * 4 * sube * cierra * Math.Exp(-enGolpe / 0.015) + fondo;
                seguido[i] = ruidoT[i] * escT + fondo;
            }
            double[] golpesC = Recorta(golpes, 20, 12000), seguidoC = Recorta(seguido, 20, 12000);
            float[] rafagas = new float[NS], continuo = new float[NS], rafagas2 = new float[NS * 2];
            for (int i = 0; i < NS; i++)
            { rafagas[i] = (float)golpesC[i]; continuo[i] = (float)seguidoC[i]; rafagas2[2 * i] = rafagas[i]; rafagas2[2 * i + 1] = rafagas[i] * 0.7f; }
            double[] pEnt = Psd(ruido, desde);
            double costura = Nivel(pEnt, 11300, 11800);

            // Cuanto suena la fuente sin picos ni huecos (FuenteSuave): la mediana de cada banda de 1/12 de octava
            // con sus dos vecinas. Primero la mediana sola, con numeros conocidos...
            MethodInfo mMed = TSbr.GetMethod("Mediana3", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            MethodInfo mFue = TSbr.GetMethod("FuenteSuave", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Check(mMed != null && mFue != null, "estan las piezas de la fuente sin picos ni huecos (Mediana3, FuenteSuave)");
            if (mMed != null && mFue != null)
            {
                double[] m12 = new double[12]; for (int i = 0; i < 12; i++) m12[i] = 10 * (i + 1);
                m12[6] = 1000; m12[3] = 0;   // un pico y un hueco sueltos
                float[] esperado = { 20, 20, 20, 30, 50, 60, 80, 90, 90, 100, 110, 110 };
                bool bien = true;
                for (int i = 0; i < 12; i++) if ((float)mMed.Invoke(null, new object[] { m12, i }) != esperado[i]) bien = false;
                Check(bien, "la mediana de cada banda es la de ella y sus dos vecinas (y en los extremos, la de las tres del extremo)");
                // ...y despues la fuente entera: espectro plano bajo el corte, con un tono (x1000 de energia) y una
                // banda entera en silencio. Ni el tono ni el hueco aparecen en lo que se lee.
                object an = Activator.CreateInstance(TSbr, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { FS, 1 }, null);
                float[] reA = (float[])TSbr.GetField("re", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(an);
                float[] imA = (float[])TSbr.GetField("im", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(an);
                int corte = 560, k0 = corte / 2;
                for (int k = 0; k < reA.Length; k++) { reA[k] = k < corte ? 1f : 0f; imA[k] = 0f; }
                reA[(int)(k0 * Math.Pow(2, 5.5 / 12))] = (float)Math.Sqrt(1000);                                   // el tono
                for (int k = (int)Math.Ceiling(k0 * Math.Pow(2, 8 / 12.0)); k < (int)(k0 * Math.Pow(2, 9 / 12.0)); k++) reA[k] = 0f;   // el hueco
                mFue.Invoke(an, new object[] { corte });
                float[] fsu = (float[])TSbr.GetField("fuenteSuave", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(an);
                double lo = double.MaxValue, hi = 0;
                for (int k = k0; k < corte; k++) { lo = Math.Min(lo, fsu[k]); hi = Math.Max(hi, fsu[k]); }
                Console.WriteLine("     fuente plana (1) con un tono x1000 y una banda muda: lo que se lee va de " + lo.ToString("0.000") + " a " + hi.ToString("0.000"));
                Check(lo >= 0.9 && hi <= 1.1, "un tono o un hueco sueltos en la fuente no cambian cuanto se lee que suena (+-10 %)");
            }
            foreach (int ventana in new int[] { 2048, 4096 })
            {
                object tR, tT, tG; List<int> cortes = new List<int>();
                float[] yR = PasaVentana(ruido, ventana, out tR, cortes);
                if (yR == null) { Console.WriteLine("     (ventana de " + ventana + ": no existe en esta rama)"); continue; }
                float[] yT = PasaVentana(tono, ventana, out tT, null);
                List<int> cortesG = new List<int>(), cortesG2 = new List<int>();
                float[] yG = PasaVentana(rafagas, ventana, out tG, cortesG);
                object tS, tG2; float[] yS = PasaVentana(continuo, ventana, out tS, null);
                PasaVentana(rafagas2, ventana, out tG2, cortesG2, 2);
                Console.WriteLine("     --- ventana de " + ventana + " ---");

                double f0, pu = Pureza(yT, desde, out f0);
                Console.WriteLine("     tono de 7 kHz recreado en " + f0.ToString("0") + " Hz: limpio el " + pu.ToString("0.0") + " % de su energia (+-2 Hz entre +-250 Hz)");
                Check(pu >= 90, "un tono estable recreado sale como UN tono, sin bandas laterales (ventana " + ventana + ")");

                // El nivel: la curva de diseno es la costura por 0,85 (colchon) al nivel al que sonaba la copia de
                // antes (3/8 de potencia) y ~-9 dB por octava desde el corte.
                double[] pR = Psd(yR, desde);
                float cutHz = (float)fCut.GetValue(tR);
                double bin = (double)FS / ventana, fc = cortes.Count > 0 && cortes[cortes.Count - 1] > 0 ? cortes[cortes.Count - 1] * bin : cutHz;
                double dis0 = 20 * Math.Log10(0.85) + 10 * Math.Log10(3.0 / 8.0);
                double arr0 = Nivel(pR, fc + 100, fc + 400) - costura, esp0 = dis0 - 9 * Math.Log(fc + 250, 2) + 9 * Math.Log(fc, 2);
                double arr1 = Nivel(pR, 19800, 20400) - costura, esp1 = dis0 - 9 * Math.Log(20100 / fc, 2);
                Console.WriteLine("     ruido cortado en 12 kHz: justo encima del corte " + arr0.ToString("0.0") + " dB contra la costura (curva: "
                                  + esp0.ToString("0.0") + "); a 20 kHz " + arr1.ToString("0.0") + " dB (curva: " + esp1.ToString("0.0") + ")");
                Check(Math.Abs(arr0 - esp0) <= 1.5 && Math.Abs(arr1 - esp1) <= 1.5,
                      "la banda recreada sigue la curva de diseno a +-1,5 dB, arriba del corte y a 20 kHz (ventana " + ventana + ")");

                // Con un tono fuerte en la fuente, el ruido recreado de ALREDEDOR es el mismo que sin el tono.
                double[] pT = Psd(yT, desde);
                double peorPiso = PeorPiso(pT, pR);
                Console.WriteLine("     ruido recreado alrededor del tono contra el mismo ruido sin tono: se aparta como mucho " + peorPiso.ToString("0.0") + " dB (24avo de octava)");
                Check(peorPiso <= 2.0, "un tono fuerte no infla ni apaga el ruido recreado de alrededor (ventana " + ventana + ")");

                // Golpes entre silencios: la escala de las ventanas casi mudas no se lleva al golpe siguiente.
                double[] pG = Psd(yG, desde), pGe = Psd(rafagas, desde), pS = Psd(yS, desde), pSe = Psd(continuo, desde);
                double relR = Nivel(pS, 12500, 20000) - Nivel(pSe, 6000, 11500);
                double relG = Nivel(pG, 12500, 20000) - Nivel(pGe, 6000, 11500);
                Console.WriteLine("     agudos recreados contra la fuente: el mismo ruido seguido " + relR.ToString("0.0") + " dB, en golpes de charles " + relG.ToString("0.0") + " dB");
                Check(Math.Abs(relG - relR) <= 2.0, "los golpes entre silencios se recrean al mismo nivel que el ruido seguido (ventana " + ventana + ")");
                int cambiosG = 0;
                for (int i = 1; i < cortesG.Count; i++) if ((i + 1) * 1024 > desde && cortesG[i] != cortesG[i - 1]) cambiosG++;
                Console.WriteLine("     con los golpes, el corte usado cambio " + cambiosG + " veces (acantilado medido al final: "
                                  + ((float)fCut.GetValue(tG)).ToString("0") + " Hz)");
                Check(cortesG.Count > 0 && cortesG[0] >= 0 && cambiosG == 0, "y en las pausas con solo el bajo el corte no se va hacia arriba (ventana " + ventana + ")");
                int cambiosG2 = 0;
                for (int i = 1; i < cortesG2.Count; i++) if ((i + 1) * 512 > desde && cortesG2[i] != cortesG2[i - 1]) cambiosG2++;
                Check(cortesG2.Count > 0 && cortesG2[0] >= 0 && cambiosG2 == 0, "lo mismo en estereo, hasta el final de la cancion (ventana " + ventana + ")");

                // Temblor: con ruido estable, la banda recreada no salta mas de una ventana a otra que la fuente.
                double vR = Vaiven(yR, desde, fc * 1.03, 20000), vF = Vaiven(ruido, desde, 6200, 11500);
                Console.WriteLine("     salto de una ventana a otra: banda recreada " + vR.ToString("0.00") + " dB, la fuente " + vF.ToString("0.00") + " dB");
                Check(vR <= vF * 1.5 + 0.3, "la banda recreada no tiembla mas que la musica de la que sale (ventana " + ventana + ")");

                // El corte con el que se recrea no baila con una senal estable.
                int cambios = 0, vistos = 0;
                for (int i = 1; i < cortes.Count; i++) if ((i + 1) * 1024 > desde) { vistos++; if (cortes[i] != cortes[i - 1]) cambios++; }
                Console.WriteLine("     corte usado: " + (cortes.Count > 0 ? cortes[cortes.Count - 1] * bin : 0).ToString("0") + " Hz; cambios en "
                                  + vistos + " lecturas: " + cambios);
                Check(cortes.Count > 0 && cortes[0] >= 0 && cambios == 0, "con una senal estable, el corte con el que se recrea no baila (ventana " + ventana + ")");
            }

            // La IA propia pone la envolvente, pero la copia es la misma: su tono tambien sale limpio. Y su envolvente
            // tambien lee la fuente sin picos (FuenteSuave): con un modelo de prueba que dice SIEMPRE lo mismo (asi un
            // tono no le cambia la prediccion), el ruido recreado alrededor del tono es el mismo que sin el. La IA de
            // fabrica no sirve para eso: el tono cambia lo que ve y, con razon, lo que predice.
            string mod = Path.Combine("..", Path.Combine("ia", "rocola_ia.bin"));
            FieldInfo fNeural = TF.GetField("NeuralEnv", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            object red = fNeural != null ? fNeural.GetValue(null) : null;
            if (red != null && File.Exists(mod))
            {
                object tC; float[] yC = PasaVentana(tono, 2048, out tC, null);
                red.GetType().GetMethod("TryLoad").Invoke(red, new object[] { Path.GetFullPath(mod) });
                FieldInfo fCargo = red.GetType().GetField("Loaded");
                bool cargo = (bool)fCargo.GetValue(red);
                object tN; float[] yN = PasaVentana(tono, 2048, out tN, null);
                fCargo.SetValue(red, false);   // el resto de la bateria sigue con la curva clasica
                double fN, puN = Pureza(yN, desde, out fN);
                Console.WriteLine("     con la IA propia: tono recreado en " + fN.ToString("0") + " Hz, limpio el " + puN.ToString("0.0") + " %");
                Check(cargo && MaxDif(yN, yC) > 1e-5, "la IA propia se usa de verdad (su envolvente no es la de la curva)");
                Check(puN >= 90, "y con la IA el tono recreado tambien sale limpio: la copia es la misma");

                string tmpIa = Path.Combine(Path.GetTempPath(), "rocola_dsee_ia_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tmpIa);
                try
                {
                    string fija = Path.Combine(tmpIa, "fija.bin");
                    using (BinaryWriter w = new BinaryWriter(File.Create(fija)))
                    {
                        w.Write(System.Text.Encoding.ASCII.GetBytes("RIA1")); w.Write(1); w.Write(12); w.Write(28);
                        for (int i = 0; i < 12 * 28; i++) w.Write(0f);
                        for (int j = 0; j < 12; j++) w.Write(-0.4f - 0.1f * j);   // bajando hacia arriba, como la musica
                    }
                    red.GetType().GetMethod("TryLoad").Invoke(red, new object[] { fija });
                    bool cargoF = (bool)fCargo.GetValue(red);
                    object tF1, tF2; float[] yF1 = PasaVentana(tono, 2048, out tF1, null), yF2 = PasaVentana(ruido, 2048, out tF2, null);
                    fCargo.SetValue(red, false);
                    double peorN = PeorPiso(Psd(yF1, desde), Psd(yF2, desde));
                    Console.WriteLine("     con una IA que dice siempre lo mismo, el ruido recreado alrededor del tono contra el mismo ruido sin tono: "
                                      + peorN.ToString("0.0") + " dB");
                    Check(cargoF && peorN <= 2.0, "y la envolvente de la IA tampoco se deja mover por un tono: lee la fuente sin picos");
                }
                finally { try { Directory.Delete(tmpIa, true); } catch { } }
            }
            else Check(false, "esta la IA propia de fabrica para probar su camino");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

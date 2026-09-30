using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

// EL REMUESTREADOR DE LA SALIDA MODERNA (rama HD; etapa 6 de audio)
//
// Por WASAPI compartido, Windows mezcla a SU frecuencia -casi siempre 48 kHz- y casi toda la musica viene a
// 44,1 kHz. La rocola no deja que Windows convierta despues del limitador: convierte ella misma, antes del
// limitador final (SalidaRemuestreada, con el remuestreador DMO de Windows Media a traves de NAudio). Aqui
// se MIDE ese remuestreador con senales de contenido conocido:
//
//   respuesta   tonos de 1 a 20 kHz y un barrido: cuanto baja cada frecuencia
//   ruido       potencia por bandas: lo que entra frente a lo que sale
//   plegado     al bajar de frecuencia, lo que habia por encima de la nueva mitad y vuelve plegado
//   imagenes    al subir, lo que aparece por encima de la banda original
//   retraso     donde cae un impulso
//   duracion    cuantas muestras salen
//   final       si la ultima rafaga de la cancion sale entera
//   canales     lo de un canal no pasa al otro
//   coste       milisegundos por segundo de musica
//
// Se mide la calidad maxima que ahora elige la rocola (60), comparada con el DMO anterior (30), y el fallback
// si configurar COM falla antes de consumir entrada. El DMO es de Windows: donde no lo hay se dice y la bateria
// solo comprueba que sus medidas miden bien, con dos remuestreadores de referencia escritos aqui: uno de
// alta precision (polifasico en doble precision, 512 coeficientes por fase) y uno malo a proposito
// (interpolacion lineal). Si las medidas no separan esos dos, no sirven para juzgar al DMO.
//
// Lo que NO dice: como suena en el DAC. Esto es la cuenta digital; el aparato se mide con el aparato.

// La calidad del remuestreador de Windows Media (wmcodecdsp.h). Declarada aqui, con [PreserveSig]: el metodo
// nativo solo recibe la calidad y devuelve el HRESULT.
[ComImport, Guid("E7E9984F-F09F-4da4-903F-6E2E0EFE56B5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropsRemuestreo
{
    [PreserveSig] int SetHalfFilterLength(int calidad);
    [PreserveSig] int SetUserChannelMtx([In] float[] matriz);
}

class FuenteMem : ISampleProvider
{
    private readonly float[] d; private readonly WaveFormat wf; private int pos;
    public FuenteMem(float[] datos, int fs, int ch) { d = datos; wf = WaveFormat.CreateIeeeFloatWaveFormat(fs, ch); }
    public WaveFormat WaveFormat { get { return wf; } }
    public int Read(float[] b, int o, int c)
    {
        int n = Math.Min(c, d.Length - pos);
        if (n <= 0) return 0;
        for (int i = 0; i < n; i++) b[o + i] = d[pos + i];
        pos += n;
        return n;
    }
}

class Remuestreo
{
    static int fallos, oks;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string F(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
    static string Db(double v) { return double.IsNegativeInfinity(v) || v < -200 ? "<-200" : F(v, "0.00"); }
    static string Db0(double v) { return double.IsNegativeInfinity(v) || v < -200 ? "<-200" : F(v, "0"); }
    static double ADb(double r) { return r <= 0 ? double.NegativeInfinity : 20 * Math.Log10(r); }

    // Un remuestreador: todo lo que entra (intercalado) y lo que sale, entero.
    delegate float[] Conversor(float[] x, int ch, int fsIn, int fsOut);

    // =============================================================================================
    //  LOS DE REFERENCIA
    // =============================================================================================
    static int Mcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }
    static double I0(double x)
    {
        double s = 1, t = 1, y = x * x / 4;
        for (int k = 1; k < 200; k++) { t *= y / ((double)k * k); s += t; if (t < 1e-17 * s) break; }
        return s;
    }
    // Polifasico exacto (razon racional), en doble precision: la salida m cae en la posicion m*fsIn/fsOut de la
    // entrada. Ventana de Kaiser (beta 12, unos 120 dB) de 'mitad' muestras de la frecuencia menor a cada lado,
    // corte al 97 % de la mitad de la frecuencia menor. Sin retraso: el nucleo esta centrado.
    static float[] Referencia(float[] x, int ch, int fsIn, int fsOut)
    {
        const int mitad = 256; const double beta = 12;
        int g = Mcd(fsIn, fsOut), p = fsIn / g, q = fsOut / g;   // posicion de la salida m = m*p/q muestras de entrada
        int fmin = Math.Min(fsIn, fsOut);
        double fc = 0.5 * fmin * 0.97;
        double T = (double)mitad / fmin;                  // media ventana, en segundos
        int anchoIn = (int)Math.Ceiling(T * fsIn);         // muestras de entrada a cada lado
        int taps = 2 * anchoIn + 2;
        double[][] fases = new double[q][];
        double i0b = I0(beta);
        for (int r = 0; r < q; r++)
        {
            // salida con resto r: posicion = base + r*p/q (fraccion r*p mod q / q)
            double frac = (double)((long)r * p % q) / q;
            double[] h = new double[taps];
            for (int j = 0; j < taps; j++)
            {
                int k = j - anchoIn;                        // muestra de entrada relativa a floor(posicion)
                double dt = (frac - k) / fsIn;              // tiempo de la salida menos el de la entrada
                double u = dt / T;
                if (Math.Abs(u) >= 1) { h[j] = 0; continue; }
                double a = 2 * fc * dt;
                double sinc = Math.Abs(a) < 1e-12 ? 1 : Math.Sin(Math.PI * a) / (Math.PI * a);
                h[j] = 2 * fc / fsIn * sinc * I0(beta * Math.Sqrt(1 - u * u)) / i0b;
            }
            fases[r] = h;
        }
        long nIn = x.Length / ch;
        long nOut = (nIn * fsOut + fsIn - 1) / fsIn;
        float[] y = new float[nOut * ch];
        for (long m = 0; m < nOut; m++)
        {
            long num = m * p;
            long bas = num / q; int r = (int)(m % q);
            double[] h = fases[r];
            for (int c = 0; c < ch; c++)
            {
                double s = 0;
                for (int j = 0; j < taps; j++)
                {
                    long k = bas + (j - anchoIn);
                    if (k < 0 || k >= nIn) continue;
                    s += h[j] * x[k * ch + c];
                }
                y[m * ch + c] = (float)s;
            }
        }
        return y;
    }
    // El malo a proposito: interpolacion lineal (deja pasar imagenes y plegado, y apaga los agudos).
    static float[] Lineal(float[] x, int ch, int fsIn, int fsOut)
    {
        long nIn = x.Length / ch, nOut = (nIn * fsOut + fsIn - 1) / fsIn;
        float[] y = new float[nOut * ch];
        for (long m = 0; m < nOut; m++)
        {
            double t = (double)m * fsIn / fsOut; long k = (long)Math.Floor(t); double fr = t - k;
            for (int c = 0; c < ch; c++)
            {
                double a = k < nIn ? x[k * ch + c] : 0, b = k + 1 < nIn ? x[(k + 1) * ch + c] : 0;
                y[m * ch + c] = (float)(a + (b - a) * fr);
            }
        }
        return y;
    }

    // =============================================================================================
    //  EL DE LA ROCOLA: SalidaRemuestreada (DMO de Windows Media)
    // =============================================================================================
    static Type TSR;
    static int ultimaCalidad;
    const BindingFlags T = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    // 0 = produccion (60); -1 = fallback solicitado; -2 = fallo despues de liberar recursos; 1..60 = referencia explicita.
    static bool RechazaCalidad(ResamplerDmoStream rs) { return false; }
    static bool FallaConRecursos(ResamplerDmoStream rs) {
        object dmo = typeof(ResamplerDmoStream).GetField("dmoResampler", T).GetValue(rs);
        ((NAudio.Dmo.DmoResampler)dmo).MediaObject.FreeStreamingResources();
        throw new InvalidOperationException("Fallo COM sintetico despues de liberar recursos");
    }
    static Conversor Dmo(int mitadFiltro)
    {
        return delegate(float[] x, int ch, int fsIn, int fsOut)
        {
            object sr = Activator.CreateInstance(TSR, T, null, new object[] { new FuenteMem(x, fsIn, ch), fsOut }, null);
            FieldInfo hook = TSR.GetField("ConfiguraPrueba", T | BindingFlags.Static);
            try
            {
                if (mitadFiltro < 0) hook.SetValue(null, mitadFiltro == -1 ?
                    new Func<ResamplerDmoStream, bool>(RechazaCalidad) : new Func<ResamplerDmoStream, bool>(FallaConRecursos));
                if (mitadFiltro > 0) PonCalidad(sr, mitadFiltro);
                ISampleProvider p = (ISampleProvider)sr;
                long esperado = ((long)(x.Length / ch) * fsOut + fsIn - 1) / fsIn * ch;
                List<float> y = new List<float>((int)esperado + 8192);
                float[] buf = new float[4096 * ch];
                int n;
                while ((n = p.Read(buf, 0, buf.Length)) > 0) for (int i = 0; i < n; i++) y.Add(buf[i]);
                ultimaCalidad = (int)TSR.GetProperty("CalidadAplicada", T).GetValue(sr, null);
                return y.ToArray();
            }
            finally { hook.SetValue(null, null); ((IDisposable)sr).Dispose(); }
        };
    }
    // La calidad del DMO (IWMResamplerProps::SetHalfFilterLength, 1 a 60) no la expone NAudio: se arma el DMO
    // aqui, igual que lo arma SalidaRemuestreada, se le pone la calidad y se le entrega ya hecho.
    static void PonCalidad(object sr, int mitad)
    {
        ISampleProvider padded = (ISampleProvider)TSR.GetField("padded", T).GetValue(sr);
        WaveFormat fmt = (WaveFormat)TSR.GetField("format", T).GetValue(sr);
        ResamplerDmoStream rs = new ResamplerDmoStream(new SampleToWaveProvider(padded), fmt);
        object dmo = typeof(ResamplerDmoStream).GetField("dmoResampler", T).GetValue(rs);
        IPropsRemuestreo props = (IPropsRemuestreo)dmo.GetType().GetField("mediaComObject", T).GetValue(dmo);
        NAudio.Dmo.MediaObject mo = ((NAudio.Dmo.DmoResampler)dmo).MediaObject;
        try { mo.FreeStreamingResources(); } catch { }
        int hr = props.SetHalfFilterLength(mitad);
        if (hr != 0) throw new InvalidOperationException("SetHalfFilterLength(" + mitad + ") devolvio 0x" + hr.ToString("X8"));
        try { mo.AllocateStreamingResources(); } catch { }
        TSR.GetField("resampler", T).SetValue(sr, rs);
        TSR.GetField("samples", T).SetValue(sr, new WaveToSampleProvider(rs));
    }
    static string PorQueNoHayDmo()
    {
        try { Dmo(0)(new float[4410 * 2], 2, 44100, 48000); return null; }
        catch (Exception ex)
        {
            Exception e = ex; while (e.InnerException != null && (e is TargetInvocationException || e is TypeInitializationException)) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }
    }

    // =============================================================================================
    //  LAS MEDIDAS
    // =============================================================================================
    // Ajuste por minimos cuadrados de un tono de frecuencia conocida en y[desde..hasta) (un canal): amplitud,
    // fase (modelo A*sin(w*n + fase)) y lo que sobra (RMS del resto).
    static double[] Tono(float[] y, int ch, int canal, int fs, double f, long desde, long hasta)
    {
        double w = 2 * Math.PI * f / fs;
        double scc = 0, sss = 0, scs = 0, sxc = 0, sxs = 0;
        for (long n = desde; n < hasta; n++)
        {
            double c = Math.Cos(w * n), s = Math.Sin(w * n), v = y[n * ch + canal];
            scc += c * c; sss += s * s; scs += c * s; sxc += v * c; sxs += v * s;
        }
        double det = scc * sss - scs * scs;
        double a = (sxc * sss - sxs * scs) / det, b = (sxs * scc - sxc * scs) / det;   // v ~ a*cos + b*sin
        double r2 = 0;
        for (long n = desde; n < hasta; n++)
        {
            double e = y[n * ch + canal] - (a * Math.Cos(w * n) + b * Math.Sin(w * n));
            r2 += e * e;
        }
        return new double[] { Math.Sqrt(a * a + b * b), Math.Atan2(a, b), Math.Sqrt(r2 / (hasta - desde)) };
    }
    static double Rms(float[] y, int ch, int canal, long desde, long hasta)
    {
        double s = 0; for (long n = desde; n < hasta; n++) { double v = y[n * ch + canal]; s += v * v; }
        return Math.Sqrt(s / Math.Max(1, hasta - desde));
    }
    static float[] Seno(int fs, int ch, double seg, double f, double ampL, double ampR, double faseR)
    {
        long n = (long)(seg * fs); float[] x = new float[n * ch];
        for (long i = 0; i < n; i++)
        {
            double t = 2 * Math.PI * f * i / fs;
            x[i * ch] = (float)(ampL * Math.Sin(t));
            if (ch > 1) x[i * ch + 1] = (float)(ampR * Math.Sin(t + faseR));
        }
        return x;
    }
    static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
            if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -2 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = a + len / 2;
                    double xr = re[b] * cr - im[b] * ci, xi = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - xr; im[b] = im[a] - xi; re[a] += xr; im[a] += xi;
                    double t = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = t;
                }
            }
        }
    }
    // Densidad de potencia (Welch, Hann, 8192 puntos, mitad solapada) de un canal; por bin.
    static double[] Densidad(float[] y, int ch, int canal, long desde, long hasta)
    {
        const int N = 8192; double[] acc = new double[N / 2 + 1]; int veces = 0;
        double[] ven = new double[N]; for (int i = 0; i < N; i++) ven[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N);
        for (long s = desde; s + N <= hasta; s += N / 2)
        {
            double[] re = new double[N], im = new double[N];
            for (int i = 0; i < N; i++) re[i] = y[(s + i) * ch + canal] * ven[i];
            Fft(re, im);
            for (int k = 0; k <= N / 2; k++) acc[k] += re[k] * re[k] + im[k] * im[k];
            veces++;
        }
        for (int k = 0; k <= N / 2; k++) acc[k] /= Math.Max(1, veces);
        return acc;
    }
    static double Banda(double[] psd, int fs, double f0, double f1)
    {
        double s = 0; int n = 0, N = (psd.Length - 1) * 2;
        for (int k = 0; k < psd.Length; k++) { double f = (double)k * fs / N; if (f >= f0 && f < f1) { s += psd[k]; n++; } }
        return n > 0 ? s / n : 0;
    }

    sealed class Medida
    {
        public string Nombre; public int FsIn, FsOut;
        public List<double[]> Tonos = new List<double[]>();   // f, ganancia L dB, ganancia R dB, resto dB
        public double PeorGanancia18k, Resto1k, PeorResto;
        public double Menos1dB, Menos3dB;                     // barrido: donde cae 1 y 3 dB (Hz)
        public double DesvioBarrido;                          // barrido: mayor desvio entre 100 Hz y 15 kHz (dB)
        public double PeorRuido;                              // ruido: mayor desvio por bandas hasta 15 kHz (dB)
        public double Plegado = double.NegativeInfinity;      // al bajar: lo peor que vuelve plegado (dB)
        public double Imagenes = double.NegativeInfinity;     // al subir: lo que sale por encima de la banda original (dB)
        public double RetrasoMs, EcoLejos;                    // impulso: retraso y lo que suena a mas de 3 ms (dB)
        public long SobranCuadros;                            // duracion: cuadros de mas (o de menos, negativo)
        public double Final;                                  // final: energia de la ultima rafaga que sale (1 = entera)
        public double Cruce;                                  // canales: lo que pasa de uno al otro (dB)
        public double MsPorSegundo;                           // coste
        public bool Finitos = true;
    }

    static bool TodoFinito(float[] y) { foreach (float v in y) if (float.IsNaN(v) || float.IsInfinity(v)) return false; return true; }

    static Medida Mide(string nombre, Conversor conv, int fsIn, int fsOut, bool coste)
    {
        Medida m = new Medida(); m.Nombre = nombre; m.FsIn = fsIn; m.FsOut = fsOut;
        int ch = 2, fmin = Math.Min(fsIn, fsOut);
        // 1) Tonos: L a 0,5 y R a 0,25 con otra fase (para ver tambien que no se cruzan).
        m.PeorGanancia18k = 0; m.PeorResto = double.NegativeInfinity;
        foreach (double f in new double[] { 1000, 5000, 10000, 15000, 18000, 19000, 20000 })
        {
            if (f > 0.47 * fmin) continue;
            float[] y = conv(Seno(fsIn, ch, 0.6, f, 0.5, 0.25, 0.3), ch, fsIn, fsOut);
            if (!TodoFinito(y)) m.Finitos = false;
            long a = (long)(0.2 * fsOut), b = (long)(0.5 * fsOut);
            double[] l = Tono(y, ch, 0, fsOut, f, a, b), r = Tono(y, ch, 1, fsOut, f, a, b);
            double gL = ADb(l[0] / 0.5), gR = ADb(r[0] / 0.25), resto = ADb(l[2] / (l[0] / Math.Sqrt(2)));
            m.Tonos.Add(new double[] { f, gL, gR, resto });
            if (f <= 18000) m.PeorGanancia18k = Math.Max(m.PeorGanancia18k, Math.Max(Math.Abs(gL), Math.Abs(gR)));
            if (f == 1000) m.Resto1k = resto;
            m.PeorResto = Math.Max(m.PeorResto, resto);
            // 2) Al subir: lo que sale por encima de la mitad de la frecuencia original (imagenes).
            if (fsOut > fsIn && f >= 15000)
            {
                double[] psd = Densidad(y, ch, 0, a, b);
                double todo = Banda(psd, fsOut, 0, fsOut / 2.0), arriba = Banda(psd, fsOut, fsIn / 2.0 + 300, fsOut / 2.0);
                int N = (psd.Length - 1) * 2;
                double nArriba = 0, nTodo = 0;
                for (int k = 0; k < psd.Length; k++) { double fk = (double)k * fsOut / N; nTodo++; if (fk >= fsIn / 2.0 + 300 && fk < fsOut / 2.0) nArriba++; }
                double im = (arriba * nArriba) / Math.Max(1e-300, todo * nTodo);
                m.Imagenes = Math.Max(m.Imagenes, 10 * Math.Log10(Math.Max(im, 1e-30)));
            }
        }
        // 3) Barrido logaritmico de 20 Hz a casi la mitad de la frecuencia menor: la respuesta, continua.
        {
            double seg = 1.5, f0 = 20, f1 = 0.49 * fmin, K = seg / Math.Log(f1 / f0);
            long n = (long)(seg * fsIn); float[] x = new float[n * ch];
            for (long i = 0; i < n; i++)
            {
                double t = (double)i / fsIn, fase = 2 * Math.PI * f0 * K * (Math.Exp(t / K) - 1);
                float v = (float)(0.5 * Math.Sin(fase)); x[i * ch] = v; x[i * ch + 1] = v;
            }
            float[] y = conv(x, ch, fsIn, fsOut);
            if (!TodoFinito(y)) m.Finitos = false;
            m.Menos1dB = m.Menos3dB = f1; m.DesvioBarrido = 0;
            bool c1 = false, c3 = false;
            int vent = fsOut / 200;   // 5 ms
            for (long s = vent; s + vent <= y.Length / ch - vent; s += vent)
            {
                double t = (s + vent / 2.0) / fsOut, fi = f0 * Math.Exp(t / K);
                if (fi < 100) continue;
                // Contra la ENTRADA en el mismo tramo de tiempo: un tramo corto de un tono grave no tiene un
                // valor eficaz fijo (depende de la fase), pero entrada y salida comparten ese mismo tramo.
                long i0 = (long)Math.Round((double)s * fsIn / fsOut), i1 = (long)Math.Round((double)(s + vent) * fsIn / fsOut);
                double r = ADb(Rms(y, ch, 0, s, s + vent) / Math.Max(1e-30, Rms(x, ch, 0, i0, Math.Min(i1, n))));
                if (fi <= 15000) m.DesvioBarrido = Math.Max(m.DesvioBarrido, Math.Abs(r));
                if (!c1 && r < -1) { m.Menos1dB = fi; c1 = true; }
                if (!c3 && r < -3) { m.Menos3dB = fi; c3 = true; }
            }
        }
        // 4) Ruido blanco: potencia por bandas, lo que sale frente a lo que entra (por hercio).
        {
            Random rnd = new Random(7); long n = (long)(2.0 * fsIn); float[] x = new float[n * ch];
            for (int i = 0; i < x.Length; i++) x[i] = (float)(rnd.NextDouble() - 0.5);
            float[] y = conv(x, ch, fsIn, fsOut);
            if (!TodoFinito(y)) m.Finitos = false;
            double[] pi = Densidad(x, ch, 0, 0, n), po = Densidad(y, ch, 0, 0, y.Length / ch);
            m.PeorRuido = 0;
            double[][] bandas = { new double[] { 100, 1000 }, new double[] { 1000, 5000 }, new double[] { 5000, 10000 }, new double[] { 10000, 15000 } };
            foreach (double[] bd in bandas)
            {
                // por hercio: la densidad por bin se escala con la frecuencia de muestreo (bins de fs/N hercios)
                double din = Banda(pi, fsIn, bd[0], bd[1]) / fsIn, dout = Banda(po, fsOut, bd[0], bd[1]) / fsOut;
                m.PeorRuido = Math.Max(m.PeorRuido, Math.Abs(10 * Math.Log10(dout / din)));
            }
        }
        // 5) Al bajar: tonos por encima de la nueva mitad (no deberian salir: vuelven plegados si salen).
        if (fsIn > fsOut)
            foreach (double d in new double[] { 500, 1500, 4000 })
            {
                double f = fsOut / 2.0 + d; if (f > 0.49 * fsIn) continue;
                float[] y = conv(Seno(fsIn, ch, 0.6, f, 0.5, 0.5, 0), ch, fsIn, fsOut);
                if (!TodoFinito(y)) m.Finitos = false;
                double r = ADb(Rms(y, ch, 0, (long)(0.2 * fsOut), (long)(0.5 * fsOut)) / (0.5 / Math.Sqrt(2)));
                m.Plegado = Math.Max(m.Plegado, r);
            }
        // 6) Impulso a medio segundo: donde cae (retraso) y lo que suena lejos de el (a mas de 3 ms).
        {
            long n = fsIn; float[] x = new float[n * ch]; long k0 = fsIn / 2;
            x[k0 * ch] = 0.9f; x[k0 * ch + 1] = 0.45f;
            float[] y = conv(x, ch, fsIn, fsOut);
            if (!TodoFinito(y)) m.Finitos = false;
            long pico = 0; double max = -1;
            for (long i = 0; i < y.Length / ch; i++) { double v = Math.Abs(y[i * ch]); if (v > max) { max = v; pico = i; } }
            double fino = pico;
            if (pico > 0 && pico + 1 < y.Length / ch)
            {
                double ym = y[(pico - 1) * ch], y0 = y[pico * ch], yp = y[(pico + 1) * ch];
                double den = ym - 2 * y0 + yp; if (Math.Abs(den) > 1e-12) fino = pico + 0.5 * (ym - yp) / den;
            }
            double esperado = (double)k0 * fsOut / fsIn;
            m.RetrasoMs = 1000.0 * (fino - esperado) / fsOut;
            long lejos = (long)(0.003 * fsOut); double eco = 0;
            for (long i = 0; i < y.Length / ch; i++) if (Math.Abs(i - pico) > lejos) eco = Math.Max(eco, Math.Abs(y[i * ch]));
            m.EcoLejos = ADb(eco / Math.Max(max, 1e-30));
        }
        // 7) Duracion y final: 1,2345 s de silencio que acaban con 8 ms de un tono de 1 kHz.
        {
            long n = (long)Math.Round(1.2345 * fsIn); float[] x = new float[n * ch];
            long r0 = n - (long)(0.008 * fsIn);
            for (long i = r0; i < n; i++) { float v = (float)(0.5 * Math.Sin(2 * Math.PI * 1000.0 * (i - r0) / fsIn)); x[i * ch] = v; x[i * ch + 1] = v; }
            float[] y = conv(x, ch, fsIn, fsOut);
            if (!TodoFinito(y)) m.Finitos = false;
            long nOut = y.Length / ch, debe = (n * fsOut + fsIn - 1) / fsIn;
            m.SobranCuadros = nOut - debe;
            double eIn = 0; for (long i = r0; i < n; i++) eIn += (double)x[i * ch] * x[i * ch];
            double eOut = 0; long desde = Math.Max(0, nOut - (long)(0.012 * fsOut));
            for (long i = desde; i < nOut; i++) eOut += (double)y[i * ch] * y[i * ch];
            m.Final = (eOut / fsOut) / (eIn / fsIn);
        }
        // 8) Canales: un tono solo en el izquierdo; lo que aparece en el derecho.
        {
            float[] y = conv(Seno(fsIn, ch, 0.5, 1000, 0.5, 0, 0), ch, fsIn, fsOut);
            double l = Rms(y, ch, 0, (long)(0.1 * fsOut), (long)(0.4 * fsOut)), r = Rms(y, ch, 1, (long)(0.1 * fsOut), (long)(0.4 * fsOut));
            m.Cruce = ADb(r / Math.Max(l, 1e-30));
        }
        // 9) Coste: 10 s de ruido estereo.
        if (coste)
        {
            Random rnd = new Random(3); float[] x = new float[10 * fsIn * ch];
            for (int i = 0; i < x.Length; i++) x[i] = (float)(rnd.NextDouble() - 0.5) * 0.5f;
            conv(new float[fsIn / 10 * ch], ch, fsIn, fsOut);   // calentar (el JIT, el DMO)
            Stopwatch sw = Stopwatch.StartNew();
            conv(x, ch, fsIn, fsOut);
            m.MsPorSegundo = sw.Elapsed.TotalMilliseconds / 10.0;
        }
        return m;
    }

    static void Imprime(Medida m)
    {
        Console.WriteLine("     " + m.Nombre + ", " + F(m.FsIn / 1000.0, "0.#") + " -> " + F(m.FsOut / 1000.0, "0.#") + " kHz");
        System.Text.StringBuilder sb = new System.Text.StringBuilder("        tonos (kHz: dB, resto dB):");
        foreach (double[] t in m.Tonos) sb.Append(" " + F(t[0] / 1000, "0") + ": " + Db(t[1]) + " (" + Db0(t[3]) + ")");
        Console.WriteLine(sb.ToString());
        Console.WriteLine("        barrido: desvio hasta 15 kHz " + F(m.DesvioBarrido, "0.00") + " dB; cae 1 dB en " + F(m.Menos1dB / 1000, "0.0")
                          + " kHz y 3 dB en " + F(m.Menos3dB / 1000, "0.0") + " kHz;  ruido por bandas hasta 15 kHz: " + F(m.PeorRuido, "0.00") + " dB");
        if (m.FsIn > m.FsOut) Console.WriteLine("        plegado (lo de encima de " + F(m.FsOut / 2000.0, "0.00") + " kHz que vuelve): " + Db0(m.Plegado) + " dB");
        if (m.FsOut > m.FsIn) Console.WriteLine("        imagenes (por encima de " + F(m.FsIn / 2000.0, "0.00") + " kHz, tonos de 15-20 kHz): " + Db0(m.Imagenes) + " dB");
        Console.WriteLine("        retraso " + F(m.RetrasoMs, "0.000") + " ms; eco a mas de 3 ms " + Db0(m.EcoLejos) + " dB; duracion "
                          + (m.SobranCuadros == 0 ? "exacta" : (m.SobranCuadros > 0 ? "+" : "") + m.SobranCuadros + " cuadros")
                          + "; ultima rafaga " + F(m.Final, "0.0000") + "; cruce de canales " + Db0(m.Cruce) + " dB"
                          + (m.MsPorSegundo > 0 ? "; coste " + F(m.MsPorSegundo, "0.00") + " ms por segundo" : ""));
    }

    static readonly int[][] Conversiones = { new int[] { 44100, 48000 }, new int[] { 48000, 44100 }, new int[] { 44100, 96000 }, new int[] { 44100, 192000 }, new int[] { 48000, 96000 } };

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        TSR = TF.GetNestedType("SalidaRemuestreada", T);

        Console.WriteLine("\n  1) LAS MEDIDAS MIDEN BIEN (con dos remuestreadores de referencia)\n");
        Medida refe = Mide("referencia de alta precision", Referencia, 44100, 48000, false);
        Medida mala = Mide("interpolacion lineal (mala a proposito)", Lineal, 44100, 48000, false);
        Imprime(refe); Imprime(mala);
        Check(refe.Finitos && refe.PeorGanancia18k < 0.01 && refe.PeorResto < -100 && refe.DesvioBarrido < 0.05 && refe.PeorRuido < 0.2,
              "la de referencia sale plana hasta 20 kHz (" + F(refe.PeorGanancia18k, "0.000") + " dB) y limpia (resto " + Db0(refe.PeorResto) + " dB)");
        Check(Math.Abs(refe.RetrasoMs) < 0.002 && refe.SobranCuadros == 0 && Math.Abs(refe.Final - 1) < 0.02 && refe.Cruce < -140 && refe.Imagenes < -90,
              "y en su sitio: retraso " + F(refe.RetrasoMs, "0.0000") + " ms, duracion exacta, la ultima rafaga entera (" + F(refe.Final, "0.0000") + "), sin cruce ni imagenes");
        Check(mala.PeorResto > refe.PeorResto + 40 && mala.Imagenes > refe.Imagenes + 40 && mala.Menos1dB < refe.Menos1dB - 3000,
              "las medidas separan lo bueno de lo malo: la lineal deja " + Db0(mala.PeorResto) + " dB de resto e imagenes a " + Db0(mala.Imagenes)
              + " dB, y a 1 dB cae en " + F(mala.Menos1dB / 1000, "0.0") + " kHz");
        Medida refeBaja = Mide("referencia de alta precision", Referencia, 48000, 44100, false);
        Medida malaBaja = Mide("interpolacion lineal", Lineal, 48000, 44100, false);
        Check(refeBaja.Plegado < -90 && malaBaja.Plegado > refeBaja.Plegado + 40,
              "al bajar de 48 a 44,1 kHz la medida del plegado tambien los separa (" + Db0(refeBaja.Plegado) + " frente a " + Db0(malaBaja.Plegado) + " dB)");

        Console.WriteLine("\n  2) EL DE LA ROCOLA: EL DMO DE WINDOWS MEDIA (SalidaRemuestreada)\n");
        if (TSR == null)
        {
            Info("(esta rama no tiene salida moderna: el volumen y la frecuencia los pone WaveOut)");
            Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
            return fallos;
        }
        string sinDmo = PorQueNoHayDmo();
        if (sinDmo != null)
        {
            Info("NO SE PUEDE MEDIR AQUI: el remuestreador DMO es un componente de Windows (" + sinDmo + ").");
            Info("Esta misma bateria lo mide en Windows: validar-audio-mejoras.ps1 la corre, o a mano con");
            Info("remuestreo.exe Rocola.exe. Las medidas de arriba demuestran que la vara de medir es buena.");
        }
        else
        {
            float[] fixture = Seno(44100, 2, .125, 19000, .5, .25, .3);
            float[] normal = Dmo(0)(fixture, 2, 44100, 48000);
            Check(ultimaCalidad == 60, "Produccion aplica realmente DMO 60 antes de leer entrada");
            float[] anterior = Dmo(30)(fixture, 2, 44100, 48000);
            foreach (int fallo in new int[] { -1, -2 })
            {
                float[] recuperado = Dmo(fallo)(fixture, 2, 44100, 48000);
                bool igual = recuperado.Length == anterior.Length;
                for (int i = 0; i < anterior.Length && igual; i++) if (recuperado[i] != anterior[i]) igual = false;
                Check(igual && ultimaCalidad == 30, "COM falla (" + fallo + "): DMO nuevo 30, PCM/cola identicos al anterior");
            }
            Check(normal.Length == anterior.Length, "Calidad 60 mantiene duracion exacta frente a 30");
            foreach (int[] cv in Conversiones)
            {
                string q = F(cv[0] / 1000.0, "0.#") + " -> " + F(cv[1] / 1000.0, "0.#") + " kHz";
                Medida d;
                try { d = Mide("DMO como lo usa la rocola (calidad 60)", Dmo(0), cv[0], cv[1], true); }
                catch (Exception ex) { Check(false, q + ": el remuestreador de la rocola fallo: " + ex.GetBaseException().Message); continue; }
                Imprime(d);
                Check(d.Finitos && d.SobranCuadros == 0, q + ": duracion exacta y sin valores rotos");
                Check(Math.Abs(d.Final - 1) < 0.02, q + ": la ultima rafaga de la cancion sale entera (" + F(d.Final, "0.0000") + ")");
                Check(d.Cruce < -100, q + ": lo de un canal no pasa al otro (" + Db0(d.Cruce) + " dB)");
                Check(Math.Abs(d.Tonos[0][1]) < 0.2 && d.Resto1k < -60, q + ": un tono de 1 kHz sale a su nivel (" + Db(d.Tonos[0][1]) + " dB) y limpio (" + Db0(d.Resto1k) + " dB)");
                Medida d30 = null;
                try { d30 = Mide("DMO anterior (30)", Dmo(30), cv[0], cv[1], true); }
                catch (Exception ex) { Info("la referencia 30 no se pudo poner: " + ex.GetBaseException().Message); }
                if (d30 != null) Imprime(d30);
            }
            Info("El coste es de esta maquina, no del equipo 2C/2T. Solo se instancia cuando hace falta cambiar tasa.");
        }
        Console.WriteLine(fallos == 0 ? ("\n  TODO BIEN (" + oks + " comprobaciones)") : ("\n  FALLOS: " + fallos));
        return fallos;
    }
}

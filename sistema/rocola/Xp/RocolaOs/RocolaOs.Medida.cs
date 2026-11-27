// LA MEDIDA NUEVA: EL MEDIDOR (cadena de sonido nueva, CADENA_NUEVA.md §1.2; los datos en RocolaOs.MedidaNueva.cs). Puro: recibe cuadros
// estereo en coma flotante y al cerrar da lo medido. Lo usan la medida de fondo y la de la cola (RocolaOs.MedidaTabla.cs, la cancion entera
// por la cadena a ganancia 1) y la medida en vivo de lo que no se pudo medir antes (Spotify, o una cancion que entro sin medir).
//   - SONORIDAD (ITU-R BS.1770-4): la ponderacion K de la norma a cualquier tasa (los coeficientes de MedidorBs1770.Estante y PasoAlto, en
//     doble), la energia de cada 100 ms, bloques de 400 ms con 75 % de solape, puerta absoluta de -70 LUFS y relativa de -10 LU. Los dos
//     canales con peso 1 (Delante izquierdo y derecho).
//   - LRA (EBU Tech 3342): la sonoridad de 3 s cada 100 ms, puerta absoluta de -70 LUFS y relativa de -20 LU, del percentil 10 al 95 (con
//     las ventanas ordenadas: exacto, sin casillas).
//   - PICO REAL (BS.1770-4 anexo 2): sobremuestreo 4x con un FIR de fase lineal (seno cardinal con ventana de Kaiser de beta 6, 20 coeficientes
//     por fase, longitud impar: los puntos caen en 0, 1/4, 1/2 y 3/4 de cada intervalo; la fase 0 es la muestra). Para no gastar, un trozo de
//     256 cuadros cuyas muestras (con las vecinas que usa el filtro) no pueden dar mas que el pico real que ya se tiene (por la suma de los
//     valores absolutos de cada fase) no se calcula: el resultado es el mismo.
//   - PICO DE MUESTRA y ESPECTRO en tercios de octava (una FFT de Hann de 4096 cada 8192 cuadros, potencia de los dos canales).
// RocolaOsCrudo mide lo que sale del decodificador para los ARREGLOS: la continua de cada canal, la energia de cada uno y la correlacion del
// grave (un paso bajo de Butterworth de 2.o orden a 150 Hz).
using System;
using System.Collections.Generic;

namespace Rocola
{
    internal sealed class RocolaOsMedidor
    {
        internal const int TpFase = 20;                 // coeficientes por fase del sobremuestreo (plano hasta 0,4 de la tasa: 17,6 kHz a 44,1)
        internal const int TpTrozo = 256;
        internal const int FftN = 4096, FftCada = 8192;
        private readonly int tasa;
        private readonly double b0, b1, b2, a1, a2, r1, r2;
        private double lx1, lx2, ls1, ls2, lt1, lt2, rx1, rx2, rs1, rs2, rt1, rt2;
        private double subE; private long subN, finSub, cuadros; private int indiceSub;
        private readonly List<double> subE100 = new List<double>(4096);
        private readonly List<int> subN100 = new List<int>(4096);
        private float pico;                             // de muestra (lineal)
        private float tp;                               // pico real (lineal)
        // el sobremuestreo
        private static float[][] fases;                 // [3][TpFase]: las fases 1, 2 y 3 (la 0 es la muestra)
        private static float sumaAbs;                   // lo mas que puede dar una fase sobre la muestra mas alta de su ventana
        private readonly float[] hL, hR;                // la historia de cada canal (TpFase - 1 + trozo)
        private int hN;                                 // cuadros en el trozo de ahora (detras de la historia)
        // el espectro
        private static int[] binBanda;                  // la banda de cada bin (para FftN y una tasa: se rehace si cambia)
        private static int binTasa;
        private static float[] hann;
        private readonly double[] bandas = new double[RocolaOsMedidaNueva.Bandas];
        private readonly float[] fL = new float[FftN], fR = new float[FftN];
        private int fN; private long hastaFft;
        private readonly double[] re = new double[FftN], im = new double[FftN];
        internal long Cuadros { get { return cuadros; } }
        internal int Tasa { get { return tasa; } }

        internal RocolaOsMedidor(int tasaMuestreo)
        {
            tasa = Math.Max(8000, tasaMuestreo);
            double[] e1 = MedidorBs1770.Estante(tasa), e2 = MedidorBs1770.PasoAlto(tasa);
            b0 = e1[0]; b1 = e1[1]; b2 = e1[2]; a1 = e1[3]; a2 = e1[4]; r1 = e2[3]; r2 = e2[4];
            finSub = tasa / 10;
            Fases();
            hL = new float[TpFase - 1 + TpTrozo]; hR = new float[TpFase - 1 + TpTrozo];
            Espectro(tasa);
        }

        private static readonly object cerrojo = new object();
        private static void Fases()
        {
            lock (cerrojo)
            {
                if (fases != null) return;
                // un seno cardinal con el corte en la mitad de la tasa de antes (ceros en los multiplos de 4: la fase 0 es la muestra), Kaiser de beta 6,
                // longitud 4 * TpFase + 1 centrada
                int L = 4 * TpFase + 1, c = L / 2;
                double beta = 6.0, i0 = I0(beta);
                double[] h = new double[L];
                for (int k = 0; k < L; k++)
                {
                    double t = (k - c) / 4.0, u = (k - c) / (double)c;
                    double s = t == 0 ? 1.0 : Math.Sin(Math.PI * t) / (Math.PI * t);
                    h[k] = s * I0(beta * Math.Sqrt(Math.Max(0.0, 1.0 - u * u))) / i0;
                }
                // y[4n + p] = suma_j x[n - j] h[4j + p + desfase]: con la longitud impar, la fase p tiene los coeficientes h[c + p - 4m] para
                // los m que caen dentro; se guardan como «x[n + a] * f[a]» con a de -(TpFase/2 - 1) a TpFase/2 (la fase p mira la muestra n y
                // las de alrededor); se usa la ventana x[n - TpFase/2 + 1 .. n + TpFase/2].
                float[][] f = new float[3][];
                float mx = 0f;
                for (int p = 1; p <= 3; p++)
                {
                    f[p - 1] = new float[TpFase];
                    double sa = 0, sum = 0;
                    for (int a = 0; a < TpFase; a++)
                    {
                        int off = a - (TpFase / 2 - 1);            // la muestra n + off
                        int k = c + p - 4 * off;                    // el coeficiente que le toca (punto n + p/4)
                        double v = (k >= 0 && k < L) ? h[k] : 0.0;
                        f[p - 1][a] = (float)v; sa += Math.Abs(v); sum += v;
                    }
                    // que cada fase sume 1 (la continua pasa entera)
                    for (int a = 0; a < TpFase; a++) f[p - 1][a] = (float)(f[p - 1][a] / sum);
                    sa /= Math.Abs(sum);
                    if (sa > mx) mx = (float)sa;
                }
                sumaAbs = mx;
                fases = f;
            }
        }
        private static double I0(double x)
        {
            double s = 1.0, t = 1.0;
            for (int k = 1; k < 200; k++) { double v = x / (2.0 * k); t *= v * v; s += t; if (t < 1e-16 * s) break; }
            return s;
        }
        private static void Espectro(int t)
        {
            lock (cerrojo)
            {
                if (binBanda != null && binTasa == t) return;
                int[] bb = new int[FftN / 2 + 1];
                for (int k = 0; k <= FftN / 2; k++)
                {
                    double f = k * (double)t / FftN;
                    int b = -1;
                    if (f > 0) { double x = 3.0 * Math.Log(f / 1000.0, 2.0) + 16.0; int q = (int)Math.Round(x); if (q >= 0 && q < RocolaOsMedidaNueva.Bandas) b = q; }
                    bb[k] = b;
                }
                float[] w = new float[FftN];
                for (int i = 0; i < FftN; i++) w[i] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / FftN));
                hann = w; binBanda = bb; binTasa = t;
            }
        }

        // cuadros estereo entrelazados (n = muestras, par)
        internal void Procesa(float[] x, int o, int n)
        {
            int fin = o + (n & ~1);
            double B0 = b0, B1 = b1, B2 = b2, A1 = a1, A2 = a2, R1 = r1, R2 = r2;
            float pk = pico;
            for (int i = o; i < fin; i += 2)
            {
                float l = x[i], r = x[i + 1];
                float al = l < 0 ? -l : l, ar = r < 0 ? -r : r;
                if (al > pk) pk = al; if (ar > pk) pk = ar;
                double s = B0 * l + B1 * lx1 + B2 * lx2 - A1 * ls1 - A2 * ls2;
                double t = s - 2.0 * ls1 + ls2 - R1 * lt1 - R2 * lt2;
                lx2 = lx1; lx1 = l; ls2 = ls1; ls1 = s; lt2 = lt1; lt1 = t;
                double e = t * t;
                s = B0 * r + B1 * rx1 + B2 * rx2 - A1 * rs1 - A2 * rs2;
                t = s - 2.0 * rs1 + rs2 - R1 * rt1 - R2 * rt2;
                rx2 = rx1; rx1 = r; rs2 = rs1; rs1 = s; rt2 = rt1; rt1 = t;
                subE += e + t * t; subN++;
                // el pico real (por trozos) y el espectro
                hL[TpFase - 1 + hN] = l; hR[TpFase - 1 + hN] = r;
                if (++hN == TpTrozo) Trozo();
                if (cuadros >= hastaFft) { fL[fN] = l; fR[fN] = r; if (++fN == FftN) { Fft(); fN = 0; hastaFft = cuadros + FftCada - FftN; } }
                if (++cuadros == finSub) CierraSub();
            }
            pico = pk;
        }
        private void CierraSub()
        {
            subE100.Add(subE); subN100.Add((int)subN); subE = 0; subN = 0;
            if (Math.Abs(ls1) < 1e-25) { ls1 = 0; ls2 = 0; lt1 = 0; lt2 = 0; }
            if (Math.Abs(rs1) < 1e-25) { rs1 = 0; rs2 = 0; rt1 = 0; rt2 = 0; }
            indiceSub++;
            finSub = (long)(indiceSub + 1) * tasa / 10;
        }
        // el pico real de un trozo (la historia + hN cuadros)
        private void Trozo()
        {
            int n = hN, h = TpFase - 1;
            // las muestras que tocan los puntos de este trozo: de las del trozo con su ventana (las que faltan por delante se ven en el siguiente)
            float mx = 0f;
            for (int i = 0; i < h + n; i++) { float a = Math.Abs(hL[i]); if (a > mx) mx = a; a = Math.Abs(hR[i]); if (a > mx) mx = a; }
            float t = tp;
            if (mx * sumaAbs > t)
            {
                float[][] f = fases;
                // el punto j + p/4 (j desde 0 en el trozo) usa la ventana x[j .. j + TpFase - 1] del arreglo (la historia delante): de W en W
                // puntos con vectores (W = Vector<float>.Count), y los ultimos uno a uno
                int W = System.Numerics.Vector<float>.Count;
                System.Numerics.Vector<float> vmax = System.Numerics.Vector<float>.Zero;
                for (int ch = 0; ch < 2; ch++)
                {
                    float[] x = ch == 0 ? hL : hR;
                    for (int p = 0; p < 3; p++)
                    {
                        float[] c = f[p];
                        int b = 0;
                        for (; b + W <= n; b += W)
                        {
                            System.Numerics.Vector<float> acc = System.Numerics.Vector<float>.Zero;
                            for (int a = 0; a < TpFase; a++) acc += new System.Numerics.Vector<float>(c[a]) * new System.Numerics.Vector<float>(x, b + a);
                            vmax = System.Numerics.Vector.Max(vmax, System.Numerics.Vector.Abs(acc));
                        }
                        for (; b < n; b++)
                        {
                            float sv = 0f;
                            for (int a = 0; a < TpFase; a++) sv += x[b + a] * c[a];
                            if (sv < 0) sv = -sv;
                            if (sv > t) t = sv;
                        }
                    }
                }
                for (int k = 0; k < W; k++) if (vmax[k] > t) t = vmax[k];
                tp = t;
            }
            // la historia: los ultimos h cuadros
            Array.Copy(hL, n, hL, 0, h); Array.Copy(hR, n, hR, 0, h);
            hN = 0;
        }
        private void Fft()
        {
            float[] w = hann; int[] bb = binBanda;
            for (int ch = 0; ch < 2; ch++)
            {
                float[] s = ch == 0 ? fL : fR;
                for (int i = 0; i < FftN; i++) { re[i] = s[i] * w[i]; im[i] = 0; }
                RocolaOsFft.Hace(re, im, false);
                for (int k = 1; k <= FftN / 2; k++)
                {
                    int b = bb[k];
                    if (b >= 0) bandas[b] += re[k] * re[k] + im[k] * im[k];
                }
            }
        }

        // LO MEDIDO (con lo que falte del ultimo trozo). 'm' se rellena: Lufs, TpDb, PicoDb, Lra, Tercios, Segundos.
        internal void Cierra(RocolaOsMedidaNueva m)
        {
            // el ultimo trozo, con ceros detras (la cancion se acaba ahi)
            for (int k = 0; k < TpFase / 2; k++) { hL[TpFase - 1 + hN] = 0f; hR[TpFase - 1 + hN] = 0f; if (++hN == TpTrozo) Trozo(); }
            if (hN > 0) Trozo();
            // (un subbloque de 100 ms a medias no cuenta: BS.1770 solo con bloques enteros)
            m.Segundos = cuadros / (double)tasa;
            m.PicoDb = Db(pico);
            m.TpDb = Math.Max(m.PicoDb, Db(tp));
            m.Lufs = Integrada();
            m.Lra = Lra();
            double tot = 0; for (int b = 0; b < bandas.Length; b++) tot += bandas[b];
            if (tot > 0)
            {
                float[] t = new float[bandas.Length];
                for (int b = 0; b < bandas.Length; b++) t[b] = (float)Math.Max(-99.0, 10.0 * Math.Log10(Math.Max(1e-30, bandas[b] / tot)));
                m.Tercios = t;
            }
            else m.Tercios = null;
        }
        internal static double Db(float lin) { return lin > 1e-10f ? 20.0 * Math.Log10(lin) : -200.0; }
        private double Bloque(int k)   // la potencia del bloque de 400 ms que acaba en el subbloque k (k >= 3)
        {
            double e = 0; long n = 0;
            for (int j = k - 3; j <= k; j++) { e += subE100[j]; n += subN100[j]; }
            return n > 0 ? e / n : 0;
        }
        internal double Integrada()
        {
            int nb = subE100.Count;
            if (nb < 4) return double.NegativeInfinity;
            double abs = MedidorBs1770.Potencia(MedidorBs1770.PuertaAbsoluta);
            double s = 0; int n = 0;
            for (int k = 3; k < nb; k++) { double p = Bloque(k); if (p > abs) { s += p; n++; } }
            if (n == 0) return double.NegativeInfinity;
            double rel = Math.Max(abs, s / n * Math.Pow(10.0, MedidorBs1770.PuertaRelativa / 10.0));
            s = 0; n = 0;
            for (int k = 3; k < nb; k++) { double p = Bloque(k); if (p > rel) { s += p; n++; } }
            return n > 0 ? MedidorBs1770.Lkfs(s / n) : double.NegativeInfinity;
        }
        internal double Lra()
        {
            int nb = subE100.Count;
            if (nb < 30) return -1.0;
            double abs = MedidorBs1770.Potencia(MedidorBs1770.PuertaAbsoluta);
            List<double> v = new List<double>();
            double e = 0; long n = 0;
            for (int k = 0; k < nb; k++)
            {
                e += subE100[k]; n += subN100[k];
                if (k >= 30) { e -= subE100[k - 30]; n -= subN100[k - 30]; }
                if (k >= 29 && n > 0) { double p = e / n; if (p > abs) v.Add(p); }
            }
            if (v.Count == 0) return -1.0;
            double s = 0; foreach (double p in v) s += p;
            double rel = s / v.Count * Math.Pow(10.0, MedidorBs1770.LraPuertaRelativa / 10.0);
            List<double> q = new List<double>();
            foreach (double p in v) if (p > rel) q.Add(MedidorBs1770.Lkfs(p));
            if (q.Count == 0) return -1.0;
            q.Sort();
            double bajo = q[(int)Math.Round(0.10 * (q.Count - 1))], alto = q[(int)Math.Round(0.95 * (q.Count - 1))];
            return alto - bajo;
        }
    }

    // LO QUE SALE DEL DECODIFICADOR, PARA LOS ARREGLOS FIJOS
    internal sealed class RocolaOsCrudo
    {
        internal const double MudoDb = 30.0;            // un canal 30 dB o mas por debajo de la cancion entera
        internal const double InvertidoR = -0.6;        // la correlacion del grave, de -0,6 o menos
        internal const double GraveMinimo = 1e-5;       // (-50 dBFS) con grave de verdad
        internal const double ContinuaMinima = 0.00316; // (-50 dBFS, como DcHay de ArreglaGrabacion) menos no se resta (no se oye y costaria una segunda pasada)
        private double sL, sR, eL, eR, bLR, bLL, bRR;
        private long n;
        private readonly double c0, c1, c2, d1, d2;
        private double l1, l2, ly1, ly2, r1, r2, ry1, ry2;
        internal RocolaOsCrudo(int tasa)
        {
            double w = Math.Tan(Math.PI * 150.0 / Math.Max(8000, tasa)), q = Math.Sqrt(0.5), k = w * w, a0 = 1 + w / q + k;
            c0 = k / a0; c1 = 2 * k / a0; c2 = k / a0; d1 = 2 * (k - 1) / a0; d2 = (1 - w / q + k) / a0;
        }
        internal void Procesa(float[] x, int o, int cuantos)
        {
            int fin = o + (cuantos & ~1);
            for (int i = o; i < fin; i += 2)
            {
                double l = x[i], r = x[i + 1];
                sL += l; sR += r; eL += l * l; eR += r * r;
                double yl = c0 * l + c1 * l1 + c2 * l2 - d1 * ly1 - d2 * ly2; l2 = l1; l1 = l; ly2 = ly1; ly1 = yl;
                double yr = c0 * r + c1 * r1 + c2 * r2 - d1 * ry1 - d2 * ry2; r2 = r1; r1 = r; ry2 = ry1; ry1 = yr;
                bLR += yl * yr; bLL += yl * yl; bRR += yr * yr;
                n++;
            }
            if (Math.Abs(ly1) < 1e-25) { ly1 = ly2 = 0; } if (Math.Abs(ry1) < 1e-25) { ry1 = ry2 = 0; }
        }
        internal long Cuadros { get { return n; } }
        // los arreglos que necesita
        internal void Arreglos(RocolaOsMedidaNueva m)
        {
            m.CanalMudo = -1; m.Invertido = false; m.ContinuaL = 0f; m.ContinuaR = 0f;
            if (n < 4410) return;
            double el = eL / n, er = eR / n, tot = 0.5 * (el + er);
            double umbral = tot * Math.Pow(10.0, -MudoDb / 10.0);
            if (tot > 1e-8)
            {
                if (el <= umbral && er > el) m.CanalMudo = 0;
                else if (er <= umbral && el > er) m.CanalMudo = 1;
            }
            double cl = sL / n, cr = sR / n;
            if (Math.Abs(cl) >= ContinuaMinima) m.ContinuaL = (float)cl;
            if (Math.Abs(cr) >= ContinuaMinima) m.ContinuaR = (float)cr;
            if (m.CanalMudo < 0 && (bLL + bRR) / (2.0 * n) > GraveMinimo)
            {
                double rr = bLR / Math.Sqrt(Math.Max(1e-30, bLL * bRR));
                if (rr <= InvertidoR) m.Invertido = true;
            }
            if (m.CanalMudo == 0) m.ContinuaL = 0f;   // (lo de un canal mudo no se resta: se copia el otro)
            if (m.CanalMudo == 1) m.ContinuaR = 0f;
        }
        internal double Correlacion { get { return bLR / Math.Sqrt(Math.Max(1e-30, bLL * bRR)); } }
    }

    // UNA FFT COMPLEJA DE BASE 2 (en doble, en su sitio; la inversa divide entre n). Para el medidor y el grave al centro.
    internal static class RocolaOsFft
    {
        private static readonly object cerrojo = new object();
        private static readonly Dictionary<int, double[]> tablas = new Dictionary<int, double[]>();
        private static readonly Dictionary<int, int[]> inversos = new Dictionary<int, int[]>();
        private static void Tablas(int n, out double[] t, out int[] br)
        {
            lock (cerrojo)
            {
                if (!tablas.TryGetValue(n, out t))
                {
                    t = new double[n];   // cos de 0..n/2-1 y sen de 0..n/2-1
                    for (int k = 0; k < n / 2; k++) { t[k] = Math.Cos(2 * Math.PI * k / n); t[n / 2 + k] = Math.Sin(2 * Math.PI * k / n); }
                    tablas[n] = t;
                    int bits = 0; while ((1 << bits) < n) bits++;
                    int[] b = new int[n];
                    for (int i = 0; i < n; i++) { int r = 0; for (int q = 0; q < bits; q++) if ((i & (1 << q)) != 0) r |= 1 << (bits - 1 - q); b[i] = r; }
                    inversos[n] = b;
                }
                br = inversos[n];
            }
        }
        internal static void Hace(double[] re, double[] im, bool inversa)
        {
            int n = re.Length;
            double[] t; int[] br;
            Tablas(n, out t, out br);
            for (int i = 0; i < n; i++) { int j = br[i]; if (j > i) { double a = re[i]; re[i] = re[j]; re[j] = a; a = im[i]; im[i] = im[j]; im[j] = a; } }
            int h2 = n / 2;
            for (int size = 2; size <= n; size <<= 1)
            {
                int half = size >> 1, paso = n / size;
                for (int i = 0; i < n; i += size)
                    for (int j = i, k = 0; j < i + half; j++, k += paso)
                    {
                        double wr = t[k], wi = inversa ? t[h2 + k] : -t[h2 + k];
                        int b = j + half;
                        double tr = re[b] * wr - im[b] * wi, ti = re[b] * wi + im[b] * wr;
                        re[b] = re[j] - tr; im[b] = im[j] - ti; re[j] += tr; im[j] += ti;
                    }
            }
            if (inversa) { double s = 1.0 / n; for (int i = 0; i < n; i++) { re[i] *= s; im[i] *= s; } }
        }
    }
}

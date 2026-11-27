// ROCOLA OS (la cadena nueva del billar, B; CADENA_NUEVA.md §3, CADENA_NUEVA_B.md): LA FFT REAL de las piezas del billar, en coma flotante
// DOBLE. Una FFT compleja de base 2 de N/2 puntos hace la de N muestras reales (las pares en la parte real, las impares en la imaginaria) y se
// separa con las tablas de giro de N: la mitad de trabajo que la compleja de N. Las tablas (los giros y el orden de los bits) se comparten entre
// todas las etapas del mismo N (Tablas.De, con su cerrojo solo al hacerlas); cada etapa lleva sus propios arreglos de trabajo (Trabajo), asi
// que dos etapas en dos hilos (la que suena y la que se mide de fondo) no se pisan.
// Sin nada de NAudio ni de Form1: solo cuentas (la prueba --prueba-billar2 la mide contra la DFT directa).
using System;
using System.Collections.Generic;

namespace Rocola
{
    internal sealed class RocolaOsBillar2Fft
    {
        // LAS TABLAS de un N (potencia de 2, de 16 en adelante): para la compleja de H = N/2, el orden de los bits y los giros e^(-2 pi i k / H);
        // para separar la real, los giros e^(-2 pi i k / N) de k = 0..N/2.
        internal sealed class Tablas
        {
            internal readonly int N, H;
            internal readonly int[] Bits;
            internal readonly double[] CosH, SinH;     // k = 0..H/2-1: cos y sen de 2 pi k / H
            internal readonly double[] CosN, SinN;     // k = 0..N/2: cos y sen de 2 pi k / N
            private Tablas(int n)
            {
                N = n; H = n / 2;
                Bits = new int[H];
                for (int i = 1, j = 0; i < H; i++)
                {
                    int bit = H >> 1;
                    for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                    j |= bit; Bits[i] = j;
                }
                CosH = new double[Math.Max(1, H / 2)]; SinH = new double[Math.Max(1, H / 2)];
                for (int k = 0; k < H / 2; k++) { double a = 2.0 * Math.PI * k / H; CosH[k] = Math.Cos(a); SinH[k] = Math.Sin(a); }
                CosN = new double[H + 1]; SinN = new double[H + 1];
                for (int k = 0; k <= H; k++) { double a = 2.0 * Math.PI * k / N; CosN[k] = Math.Cos(a); SinN[k] = Math.Sin(a); }
            }
            private static readonly Dictionary<int, Tablas> hechas = new Dictionary<int, Tablas>();
            internal static Tablas De(int n)
            {
                if (n < 16 || (n & (n - 1)) != 0) throw new ArgumentException("N debe ser potencia de 2 (16 o mas)");
                lock (hechas)
                {
                    Tablas t;
                    if (!hechas.TryGetValue(n, out t)) { t = new Tablas(n); hechas[n] = t; }
                    return t;
                }
            }
        }

        private readonly Tablas t;
        private readonly double[] zr, zi;   // la compleja de H (trabajo)
        internal int N { get { return t.N; } }
        internal RocolaOsBillar2Fft(int n) { t = Tablas.De(n); zr = new double[t.H]; zi = new double[t.H]; }

        // LA COMPLEJA DE H, en su sitio (sin escalar; inversa: con el giro conjugado y dividida entre H)
        private void Compleja(bool inversa)
        {
            int h = t.H;
            double[] r = zr, m = zi;
            int[] br = t.Bits;
            for (int i = 1; i < h; i++)
            {
                int j = br[i];
                if (j > i) { double a = r[i]; r[i] = r[j]; r[j] = a; a = m[i]; m[i] = m[j]; m[j] = a; }
            }
            double[] tc = t.CosH, ts = t.SinH;
            double sg = inversa ? 1.0 : -1.0;   // e^(-i...) adelante, e^(+i...) atras
            for (int size = 2; size <= h; size <<= 1)
            {
                int half = size >> 1, paso = h / size;
                for (int i = 0; i < h; i += size)
                    for (int j = i, k = 0; j < i + half; j++, k += paso)
                    {
                        double wr = tc[k], wi = sg * ts[k];
                        int b = j + half;
                        double rb = r[b], mb = m[b];
                        double tr = rb * wr - mb * wi, ti = rb * wi + mb * wr;
                        r[b] = r[j] - tr; m[b] = m[j] - ti;
                        r[j] += tr; m[j] += ti;
                    }
            }
            if (inversa) { double s = 1.0 / h; for (int i = 0; i < h; i++) { r[i] *= s; m[i] *= s; } }
        }

        // ADELANTE: x (N reales, desde 'desde') -> su espectro de k = 0..N/2 en re e im (N/2 + 1 cada uno). X[k] = suma x[n] e^(-2 pi i k n / N).
        internal void Adelante(double[] x, int desde, double[] re, double[] im)
        {
            int h = t.H;
            for (int n = 0, j = desde; n < h; n++, j += 2) { zr[n] = x[j]; zi[n] = x[j + 1]; }
            Compleja(false);
            double[] cn = t.CosN, sn = t.SinN;
            // k = 0 y k = H: reales
            re[0] = zr[0] + zi[0]; im[0] = 0.0;
            re[h] = zr[0] - zi[0]; im[h] = 0.0;
            for (int k = 1; k < h; k++)
            {
                int q = h - k;
                double akr = zr[k], aki = zi[k], bkr = zr[q], bki = -zi[q];   // Z[k] y conj Z[H-k]
                double er = 0.5 * (akr + bkr), ei = 0.5 * (aki + bki);     // pares
                double dr = 0.5 * (akr - bkr), di = 0.5 * (aki - bki);     // (Z[k] - conj Z[H-k]) / 2
                double or_ = di, oi = -dr;                                  // impares: lo de arriba entre i
                double c = cn[k], s = -sn[k];                               // W^k = e^(-2 pi i k / N)
                re[k] = er + (or_ * c - oi * s); im[k] = ei + (or_ * s + oi * c);
            }
        }

        // ATRAS: el espectro de k = 0..N/2 (de una senal real) -> sus N muestras en y (desde 'desde'), dividido entre N.
        internal void Atras(double[] re, double[] im, double[] y, int desde)
        {
            int h = t.H;
            double[] cn = t.CosN, sn = t.SinN;
            for (int k = 0; k < h; k++)
            {
                int q = h - k;
                double xr = re[k], xi = im[k], yr = re[q], yi = -im[q];   // X[k] y conj X[H-k]
                double er = 0.5 * (xr + yr), ei = 0.5 * (xi + yi);
                double dr = 0.5 * (xr - yr), di = 0.5 * (xi - yi);
                double c = cn[k], s = sn[k];                              // W^-k = e^(+2 pi i k / N)
                double or_ = dr * c - di * s, oi = dr * s + di * c;
                zr[k] = er - oi; zi[k] = ei + or_;                       // Z = E + i O
            }
            Compleja(true);
            for (int n = 0, j = desde; n < h; n++, j += 2) { y[j] = zr[n]; y[j + 1] = zi[n]; }
        }
    }
}

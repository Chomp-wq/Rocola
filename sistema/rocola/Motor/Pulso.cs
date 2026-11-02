// EL PULSO DE LA MUSICA PARA LA PANTALLA (en XP, PublicaBloqueDe y la cola de trozos con su marca de tiempo: el pulso va CON lo que se
// oye). Se mide a la salida, sin tocar el sonido (pasa tal cual): por cada bloque de 1024 cuadros, la fuerza de los graves (< ~150 Hz),
// los medios y los agudos (> ~3 kHz), con filtros de un polo. Se guarda con el cuadro en que empieza, y la pantalla pide el del cuadro
// que se esta oyendo (lo entregado menos el colchon de la tarjeta).
using System;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        private sealed class MedidorPulso : ISampleProvider
        {
            private readonly ISampleProvider src;
            private readonly int ch;
            private readonly float aLo, aHi;
            private float lo, hiLp, ant;
            private double sLo, sMid, sHi; private int nb; private long cuadro;
            internal const int Bloque = 1024, Anillo = 256;
            internal readonly long[] Cuadros = new long[Anillo];
            internal readonly float[] Lo = new float[Anillo], Mid = new float[Anillo], Hi = new float[Anillo];
            // EL ESPECTRO (MejorasVisuales.cs): por bloque, 16 bandas en escala logaritmica de 40 Hz a 16 kHz, de una FFT de 1024 puntos
            // con ventana de Hann sobre el mono del bloque. Solo mide: el sonido pasa tal cual.
            internal readonly float[] Bandas = new float[Anillo * BandasEspectro];
            private readonly float[] mono = new float[Bloque], re = new float[Bloque], im = new float[Bloque], hann = new float[Bloque];
            private readonly int[] bandaDesde = new int[BandasEspectro + 1];
            internal volatile int Escritos;
            internal MedidorPulso(ISampleProvider s)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                float fs = s.WaveFormat.SampleRate;
                aLo = 1f - (float)Math.Exp(-2 * Math.PI * 150 / fs); aHi = 1f - (float)Math.Exp(-2 * Math.PI * 3000 / fs);
                for (int i = 0; i < Bloque; i++) hann[i] = 0.5f - 0.5f * (float)Math.Cos(2 * Math.PI * i / (Bloque - 1));
                for (int b = 0; b <= BandasEspectro; b++)
                {
                    double f = 40.0 * Math.Pow(16000.0 / 40.0, b / (double)BandasEspectro);
                    bandaDesde[b] = Math.Max(1, Math.Min(Bloque / 2, (int)Math.Round(f * Bloque / fs)));
                }
                for (int b = 1; b <= BandasEspectro; b++) if (bandaDesde[b] <= bandaDesde[b - 1]) bandaDesde[b] = Math.Min(Bloque / 2, bandaDesde[b - 1] + 1);
            }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> b)
            {
                int n = src.Read(b);
                for (int i = 0; i + ch - 1 < n; i += ch)
                {
                    float x = 0; for (int c = 0; c < ch; c++) x += b[i + c]; x /= ch;
                    lo += aLo * (x - lo); hiLp += aHi * (x - hiLp);
                    float h = x - hiLp, m = hiLp - lo;
                    sLo += lo * lo; sMid += m * m; sHi += h * h;
                    mono[nb] = x;
                    if (++nb == Bloque)
                    {
                        int k = Escritos % Anillo;
                        Espectro(k);
                        Cuadros[k] = cuadro; Lo[k] = (float)Math.Sqrt(sLo / Bloque); Mid[k] = (float)Math.Sqrt(sMid / Bloque); Hi[k] = (float)Math.Sqrt(sHi / Bloque);
                        Escritos++; cuadro += Bloque; nb = 0; sLo = sMid = sHi = 0;
                    }
                }
                return n;
            }
            private void Espectro(int k)
            {
                for (int i = 0; i < Bloque; i++) { re[i] = mono[i] * hann[i]; im[i] = 0f; }
                Fft(re, im);
                for (int b = 0; b < BandasEspectro; b++)
                {
                    double s = 0; int a = bandaDesde[b], z = Math.Max(a + 1, bandaDesde[b + 1]);
                    for (int i = a; i < z && i < Bloque / 2; i++) s += re[i] * re[i] + im[i] * im[i];
                    double rms = Math.Sqrt(s / Math.Max(1, z - a)) * 4.0 / Bloque;   // ~ la amplitud de un seno en la banda
                    float db = (float)(20 * Math.Log10(Math.Max(1e-7, rms)));
                    Bandas[k * BandasEspectro + b] = Math.Max(0f, Math.Min(1f, (db + 60f) / 54f));   // de -60 a -6 dBFS
                }
            }
            // FFT de base 2, en su sitio
            private static void Fft(float[] re, float[] im)
            {
                int n = re.Length;
                for (int i = 1, j = 0; i < n; i++)
                {
                    int bit = n >> 1;
                    for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                    j ^= bit;
                    if (i < j) { float t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
                }
                for (int len = 2; len <= n; len <<= 1)
                {
                    double ang = -2 * Math.PI / len; float wr = (float)Math.Cos(ang), wi = (float)Math.Sin(ang);
                    for (int i = 0; i < n; i += len)
                    {
                        float cr = 1f, ci = 0f;
                        for (int j = 0; j < len / 2; j++)
                        {
                            int a = i + j, b = a + len / 2;
                            float tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                            re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti;
                            float ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr;
                        }
                    }
                }
            }
        }
        private MedidorPulso pulso;

        // El pulso en el cuadro que se oye (0 a ~1 cada uno). Sin musica, cero.
        public void Pulso(long cuadroOido, out float graves, out float medios, out float agudos)
        {
            graves = medios = agudos = 0f;
            MedidorPulso p = pulso; if (p == null) return;
            int e = p.Escritos; if (e == 0) return;
            int mejor = -1;
            for (int j = 0; j < Math.Min(e, MedidorPulso.Anillo); j++)
            {
                int k = (e - 1 - j) % MedidorPulso.Anillo;
                if (p.Cuadros[k] <= cuadroOido) { mejor = k; break; }
            }
            if (mejor < 0) return;
            graves = Math.Min(1f, p.Lo[mejor] * 3.5f); medios = Math.Min(1f, p.Mid[mejor] * 4f); agudos = Math.Min(1f, p.Hi[mejor] * 10f);
        }
    }
}

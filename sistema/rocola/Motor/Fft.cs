// La FFT de la cadena (el grave en mono y el subgrave en fase lineal), sacada tal cual de Rocola/Optimizaciones.cs.
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        private sealed class AudioFftPlan
        {
            internal readonly float[] Window, Cos, Sin, SinNeg;
            internal readonly int[] Reverse;
            internal AudioFftPlan(int size)
            {
                Window = new float[size]; Reverse = new int[size];
                Cos = new float[size / 2]; Sin = new float[size / 2]; SinNeg = new float[size / 2];
                for (int i = 0; i < size; i++) Window[i] = (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / size));
                for (int i = 1, j = 0; i < size; i++)
                {
                    int bit = size >> 1;
                    for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                    j |= bit; Reverse[i] = j;
                }
                for (int k = 0; k < size / 2; k++)
                {
                    Cos[k] = (float)Math.Cos(2.0 * Math.PI * k / size);
                    Sin[k] = (float)Math.Sin(2.0 * Math.PI * k / size);
                    SinNeg[k] = -Sin[k];
                }
            }
        }
        private static readonly object fftPlanLock = new object();
        private static AudioFftPlan fft2048, fft4096;
        private static readonly Dictionary<int, AudioFftPlan> fftGrandes = new Dictionary<int, AudioFftPlan>();
        // Las bandas del EQ y su campana (Rocola/Form1.Fondo.cs), y el recorte de siempre (Rocola/Form1.cs).
        internal static readonly float[] EqCentros = { 50f, 158f, 400f, 1000f, 2500f, 5600f, 10000f, 15800f };
        internal static float[] EqPeak(float fc, int rate, float dB)
        {
            double w = 2.0 * Math.PI * fc / Math.Max(8000, rate);
            if (w >= Math.PI * 0.95 || (dB > -0.1f && dB < 0.1f)) return null;   // sin correccion = ni se crea (identidad exacta)
            double A = Math.Pow(10.0, dB / 40.0), al = Math.Sin(w) / 2.2, cw = Math.Cos(w);   // Q = 1.1
            double a0 = 1.0 + al / A;
            return new float[] { (float)((1.0 + al * A) / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al * A) / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al / A) / a0) };
        }
        private static AudioFftPlan GetAudioFftPlan(int size)
        {
            bool grande = size >= 8192 && size <= 65536 && (size & (size - 1)) == 0;
            if (size != 2048 && size != 4096 && !grande) return new AudioFftPlan(size);
            lock (fftPlanLock)
            {
                if (size == 2048) return fft2048 ?? (fft2048 = new AudioFftPlan(size));
                if (size == 4096) return fft4096 ?? (fft4096 = new AudioFftPlan(size));
                AudioFftPlan p;
                if (!fftGrandes.TryGetValue(size, out p)) { p = new AudioFftPlan(size); fftGrandes[size] = p; }
                return p;
            }
        }
        private static float Clampf(float v, float lo, float hi) { return !(v > lo) ? lo : (v > hi ? hi : v); }
    }
}

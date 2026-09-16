using System;

namespace Rocola
{
    public partial class Form1
    {
        // Optimizaciones automaticas: no son preferencias visuales de Ctrl+F3.
        [ThreadStatic] private static Func<bool> measureCancellation;
        private bool MeasureFileResponsive(string path, bool aFondo, Func<bool> cancel,
            out float rms, out float durSec, out float[] eqCurve)
        {
            Func<bool> previous = measureCancellation;
            measureCancellation = cancel;
            try { return MeasureFile(path, aFondo, out rms, out durSec, out eqCurve); }
            finally { measureCancellation = previous; }
        }
        private static bool MeasurementCancelled()
        {
            Func<bool> cancel = measureCancellation;
            return cancel != null && cancel();
        }

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
        private static AudioFftPlan GetAudioFftPlan(int size)
        {
            if (size != 2048 && size != 4096) return new AudioFftPlan(size);
            lock (fftPlanLock)
            {
                if (size == 2048) return fft2048 ?? (fft2048 = new AudioFftPlan(size));
                return fft4096 ?? (fft4096 = new AudioFftPlan(size));
            }
        }

    }
}

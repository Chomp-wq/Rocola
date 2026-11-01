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
            internal volatile int Escritos;
            internal MedidorPulso(ISampleProvider s)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                float fs = s.WaveFormat.SampleRate;
                aLo = 1f - (float)Math.Exp(-2 * Math.PI * 150 / fs); aHi = 1f - (float)Math.Exp(-2 * Math.PI * 3000 / fs);
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
                    if (++nb == Bloque)
                    {
                        int k = Escritos % Anillo;
                        Cuadros[k] = cuadro; Lo[k] = (float)Math.Sqrt(sLo / Bloque); Mid[k] = (float)Math.Sqrt(sMid / Bloque); Hi[k] = (float)Math.Sqrt(sHi / Bloque);
                        Escritos++; cuadro += Bloque; nb = 0; sLo = sMid = sHi = 0;
                    }
                }
                return n;
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

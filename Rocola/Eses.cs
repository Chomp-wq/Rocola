// SUAVIZAR LAS "ESES" (T9, 25 de octubre de 2026).
//
// Con los agudos del local subidos, la presencia, el realce o el DSEE, las "s" y las "ch" de algunas voces se vuelven chillonas, sobre
// todo en los MP3 de 128 kbps. Esto baja solo la banda de las eses (un paso-banda centrado en 6,5 kHz, de unos 4,4 a 9,6 kHz a -3 dB)
// y solo en el instante en que la "s" manda: cuando esa banda pasa del 45 % de la energia de todo lo que suena (envolventes de 5 ms) y
// esta por encima de -40 dBFS. Baja (0,45 / fraccion) al cuadrado, como un compresor de 2:1 de esa fraccion (con la "s" en el 60 % de la
// energia, unos 5 dB), como mucho 6 dB, en 1 ms, y suelta en 60 ms. El resto del brillo queda igual.
//
// Lo que sale es la entrada menos la parte de la banda que sobra: y = x - (1 - g) * banda. Mientras no baja nada (g = 1, casi siempre),
// sale BIT A BIT lo que entra; al soltar, cuando le falta menos de una milesima (-0,009 dB) vuelve a 1 exacto, unos 0,4 s despues de la
// "s". Apagado (de fabrica, "suaviza_eses=" en Ctrl+F3 -> Sonido -> El
// local y sus bocinas -> Tono y presencia del local), ni se calcula. Va despues del local y la presencia (que suben los agudos) y antes
// del bajo psicoacustico y del limitador. Lecturas partidas, "sin mejoras" (Pasa), disco continuo (Sigue) y cebado, como PresenciaSala.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class SuavizaEses : ISampleProvider
    {
        internal const double CentroHz = 6500.0, Q = 0.9;
        internal const double EnvolventeMs = 5.0, AtaqueMs = 1.0, SueltaMs = 60.0;
        internal const double Umbral = 0.45;                 // la banda de las eses, fraccion de la energia de todo
        internal const double MaxBajaDb = 6.0, PisoDb = -40.0;
        private readonly ISampleProvider src;
        private readonly int ch, tasa;
        private readonly bool vale;                          // a esta frecuencia cabe la banda (por debajo de 0,45 de la tasa)
        private readonly double b0, a1, a2;                  // paso-banda (Audio EQ Cookbook, 0 dB en el centro): b1 = 0, b2 = -b0
        private readonly double[] est;                       // por canal: x1, x2, y1, y2
        private readonly double kEnv, kAta, kSue, gMin, piso;
        private double eB, eT, pb, pt, g = 1.0;
        private int canal;
        private volatile bool encendido;
        internal volatile bool Pasa;                         // "Escuchar sin mejoras" (S4)
        private volatile SuavizaEses anterior;
        private bool empezo;
        private int cebadoPorPasar;
        internal int Cebado;                                 // el cebado de delante (lo pone ArmaCadena): tras heredar, pasa tal cual
        private double bajaMax;                              // lo que mas bajo desde la ultima vez que se pregunto (S5)

        internal SuavizaEses(ISampleProvider s, bool encender)
        {
            src = s; ch = Math.Max(1, s.WaveFormat.Channels); tasa = s.WaveFormat.SampleRate;
            vale = CentroHz * 1.5 < 0.45 * tasa;
            double w = 2.0 * Math.PI * Math.Min(CentroHz, 0.4 * tasa) / tasa, alfa = Math.Sin(w) / (2.0 * Q), a0 = 1.0 + alfa;
            b0 = alfa / a0; a1 = -2.0 * Math.Cos(w) / a0; a2 = (1.0 - alfa) / a0;
            est = new double[4 * ch];
            kEnv = 1.0 - Math.Exp(-1.0 / (EnvolventeMs * 0.001 * tasa));
            kAta = 1.0 - Math.Exp(-1.0 / (AtaqueMs * 0.001 * tasa));
            kSue = 1.0 - Math.Exp(-1.0 / (SueltaMs * 0.001 * tasa));
            gMin = Math.Pow(10.0, -MaxBajaDb / 20.0);
            piso = Math.Pow(10.0, PisoDb / 10.0) * ch;   // la potencia de la banda (suma de los canales) a -40 dBFS en cada uno
            encendido = encender;
        }
        public WaveFormat WaveFormat { get { return src.WaveFormat; } }
        internal void Ajusta(bool encender) { encendido = encender; }
        internal bool Encendido { get { return encendido; } }
        // Lo que mas bajo desde la ultima vez que se pregunto, en dB (0 = nada). Lo lee la pantalla (S5).
        internal double BajaDb { get { double b = bajaMax; bajaMax = 0; return b > 0 ? -20.0 * Math.Log10(1.0 - b) : 0.0; } }

        internal void Sigue(SuavizaEses previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
        private void Hereda()
        {
            SuavizaEses a = anterior; anterior = null;
            if (a == null || a.ch != ch || a.tasa != tasa) return;
            Array.Copy(a.est, est, est.Length); eB = a.eB; eT = a.eT; pb = a.pb; pt = a.pt; g = a.g; canal = a.canal;
            cebadoPorPasar = Math.Max(0, Cebado) * ch;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int n = src.Read(buffer, offset, count);
            if (!vale || n <= 0) return n;
            if (!empezo) { empezo = true; Hereda(); }
            int o = offset, m = n;
            if (cebadoPorPasar > 0)
            {   // el cebado de la siguiente del mismo disco: sin tocar lo heredado (el relevo lo tira)
                int salta = Math.Min(m, cebadoPorPasar); cebadoPorPasar -= salta;
                canal = (canal + salta % ch) % ch; o += salta; m -= salta;
                if (m == 0) return n;
            }
            bool on = encendido && !Pasa;
            if (!on && g == 1.0) { canal = (canal + m % ch) % ch; return n; }   // apagado y en 1: ni se calcula
            int c = canal; double gg = g, b = eB, t = eT, sb = pb, st = pt, bm = bajaMax;
            for (int i = o; i < o + m; i++)
            {
                double x = buffer[i];
                int e = 4 * c;
                double y = b0 * (x - est[e + 1]) - a1 * est[e + 2] - a2 * est[e + 3];
                est[e + 1] = est[e]; est[e] = x; est[e + 3] = est[e + 2]; est[e + 2] = y;
                if (gg != 1.0) buffer[i] = (float)(x - (1.0 - gg) * y);
                sb += y * y; st += x * x;
                if (++c < ch) continue;
                c = 0;
                b += kEnv * (sb - b); t += kEnv * (st - t); sb = 0; st = 0;
                double meta = 1.0;
                if (on && b > piso && b > Umbral * t) { meta = Umbral * t / b; meta *= meta; if (meta < gMin) meta = gMin; }
                if (meta < gg) gg += kAta * (meta - gg);
                else { gg += kSue * (meta - gg); if (1.0 - gg < 1e-3) gg = 1.0; }
                if (1.0 - gg > bm) bm = 1.0 - gg;
            }
            canal = c; g = gg; eB = b; eT = t; pb = sb; pt = st; bajaMax = bm;
            return n;
        }
    }
}

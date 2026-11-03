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

        // EN EL MODO BILLAR (CadenaBillar.cs) la "s" se compara con lo que suena por encima de 1 kHz (la voz, las guitarras, los platillos), no con
        // TODO: en una mezcla con bajo el grave se lleva casi toda la energia y la banda de las eses no llegaba nunca al 45 % del total, asi que
        // la etapa no actuaba justo en la musica que mas suena en el billar (medido: cero bajada en pop y reggaeton). Con la referencia por
        // encima de 1 kHz, una vocal (formantes de 0,5 a 3 kHz) queda lejos del umbral y una "s" lo pasa con o sin bajo debajo.
        internal const double ReferenciaBillarHz = 1000.0;
        // Y POR ARRIBA, HASTA EL CORTE DE LA CANCION, SI EL DSEE LA RECREA (3-11-2026): con el DSEE en el billar («solo en las muy
        // cortadas»), lo que el DSEE recrea por encima del corte entraba en la referencia como si fuera voz, la «s» quedaba en menos
        // fraccion y se suavizaba menos: medido, de 5 a 9 kHz salia hasta 1,9 dB mas fuerte que sin el DSEE. Ahora, SOLO en esas canciones
        // (topeHz > 0: el corte medido; Cadena.cs), la referencia es lo que suena entre 1 kHz y el corte: lo recreado ya no la mueve. En
        // las demas no se pone el tope y sale bit a bit como antes (con tope en 11 kHz para todas, una cancion completa se suavizaba hasta
        // 3,8 dB mas: es otro cambio, no se hizo).
        private readonly double lb0, lb1, lb2, la1, la2;     // paso-bajo de la referencia (solo en el billar)
        private readonly double[] refL;                      // su memoria por canal
        private readonly bool billar;
        private readonly double hb0, hb1, hb2, ha1, ha2;     // paso-alto de la referencia (solo en el billar)
        private readonly double[] ref_;                      // su memoria por canal: x1, x2, y1, y2
        internal SuavizaEses(ISampleProvider s, bool encender) : this(s, encender, false) { }
        internal SuavizaEses(ISampleProvider s, bool encender, bool enBillar) : this(s, encender, enBillar, 0.0) { }
        private readonly bool conTope;
        internal SuavizaEses(ISampleProvider s, bool encender, bool enBillar, double topeHz)
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
            billar = enBillar;
            ref_ = new double[4 * ch];
            double wr = 2.0 * Math.PI * Math.Min(ReferenciaBillarHz, 0.4 * tasa) / tasa, cr = Math.Cos(wr), alr = Math.Sin(wr) / (2.0 * 0.7071), a0r = 1.0 + alr;
            hb0 = (1.0 + cr) / 2.0 / a0r; hb1 = -(1.0 + cr) / a0r; hb2 = hb0; ha1 = -2.0 * cr / a0r; ha2 = (1.0 - alr) / a0r;
            refL = new double[4 * ch];
            conTope = enBillar && topeHz > ReferenciaBillarHz * 2;
            double wl = 2.0 * Math.PI * Math.Min(conTope ? topeHz : 0.45 * tasa, 0.45 * tasa) / tasa, cl = Math.Cos(wl), all = Math.Sin(wl) / (2.0 * 0.7071), a0l = 1.0 + all;
            lb0 = (1.0 - cl) / 2.0 / a0l; lb1 = (1.0 - cl) / a0l; lb2 = lb0; la1 = -2.0 * cl / a0l; la2 = (1.0 - all) / a0l;
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
            if (a.billar == billar) { Array.Copy(a.ref_, ref_, ref_.Length); if (a.conTope && conTope) Array.Copy(a.refL, refL, refL.Length); }
            cebadoPorPasar = Math.Max(0, Cebado) * ch;
        }

        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
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
                sb += y * y;
                if (billar)
                {
                    double r = hb0 * x + hb1 * ref_[e] + hb2 * ref_[e + 1] - ha1 * ref_[e + 2] - ha2 * ref_[e + 3];
                    if (r < 1e-30 && r > -1e-30) r = 0.0;
                    ref_[e + 1] = ref_[e]; ref_[e] = x; ref_[e + 3] = ref_[e + 2]; ref_[e + 2] = r;
                    if (conTope)
                    {
                        double q = lb0 * r + lb1 * refL[e] + lb2 * refL[e + 1] - la1 * refL[e + 2] - la2 * refL[e + 3];
                        if (q < 1e-30 && q > -1e-30) q = 0.0;
                        refL[e + 1] = refL[e]; refL[e] = r; refL[e + 3] = refL[e + 2]; refL[e + 2] = q;
                        r = q;
                    }
                    st += r * r;
                }
                else st += x * x;
                if (++c < ch) continue;
                c = 0;
                b += kEnv * (sb - b); t += kEnv * (st - t); sb = 0; st = 0;
                if (b < 1e-30) b = 0.0;   // (28-10-2026) en el silencio se quedaba en un desnormal fijo (kEnv * b redondea a 0) y costaba en
                if (t < 1e-30) t = 0.0;   // cada muestra para siempre; el piso (-40 dB) queda muy por encima: lo que suena no cambia
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

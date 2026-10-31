// ATAQUE MAS CLARO (27 de octubre de 2026): menos cola detras de cada golpe y, si hay sitio, un poco mas de ataque.
//
// En un local que rebota, lo que se come la claridad de una voz o de una caja no es el golpe sino lo que viene detras: la cola que se
// arrastra y se mezcla con la nota siguiente. Esta etapa compara dos envolventes de lo que suena por encima de 120 Hz -una rapida y
// una lenta- y actua segun su diferencia d = 20 log10(rapida / lenta):
//   - COLA. Si d baja de -2 dB (la envolvente rapida ya cayo bien por debajo de la lenta: el golpe paso y queda la cola), se baja
//     0,35 dB por cada dB que falte, como mucho 3 dB. Un tono parejo (d = 0) no se toca.
//   - ATAQUE. Si d pasa de +2 dB (la rapida subio por encima de la lenta: un golpe que llega), se sube 0,3 dB por cada dB de mas,
//     hasta 1,5 dB, SOLO si hay sitio debajo del techo del limitador (TechoPicoReal): se recuerda el pico de los ultimos instantes
//     (0,5 s enteros y luego bajando con 1 s de constante, como la presencia) y la subida nunca lo lleva mas arriba. En una cancion
//     que ya llega al techo (nivelada al limite) casi nunca hay sitio y esta parte no hace nada; la cola si.
//
// COMO. Un paso-alto de 120 Hz por canal (Butterworth de dos polos); el detector oye la suma mono de los dos pasa-altos. Las dos
// envolventes son de pico con la MISMA razon entre ataque y suelta (10) -asi un tono o un ruido parejo da la misma envolvente
// en las dos y d = 0-: la rapida con 1 ms de ataque y 10 ms de suelta, la lenta con 25 ms y 250 ms. Por debajo de -55 dBFS la
// envolvente lenta no se mide (no se levanta el soplido de la grabacion ni se baja el silencio). La ganancia se aplica SOLO a lo que
// pasa del paso-alto (salida = entrada + (G - 1) * pasoalto): el grave por debajo de 120 Hz no se mueve, asi el bombo no bombea. Una
// sola ganancia para los dos canales (la imagen no se mueve), cuenta nueva cada 32 cuadros, con 5 ms de suavizado para entrar y
// salir de la cola o del ataque, 60 ms para dejar la subida del ataque y 2 ms para dejar la cola (el golpe siguiente no debe
// encontrar la puerta a medio cerrar).
//
// Va detras de la presencia (el compresor) y delante de suavizar las eses, el bajo psicoacustico y el limitador. Apagado (de fabrica)
// la etapa no toca ni una muestra; si se apaga a media cola o a media subida, la ganancia vuelve a 1 en 40 ms. «Escuchar sin mejoras»
// (S4) la quita mientras dura. Lecturas partidas, disco continuo (Sigue) y cebado, como las demas (EtapaPorCuadros.cs). Cuesta unos
// 1,2 us por cuadro.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class AtaqueClaro : EtapaPorCuadros
    {
        internal const double CorteHz = 120.0, Q = 0.7071;
        internal const double RapidaAtaqueMs = 1.0, RapidaSueltaMs = 10.0, LentaAtaqueMs = 25.0, LentaSueltaMs = 250.0;
        internal const double ColaDesdeDb = -2.0, ColaPorDb = 0.35, ColaMaxDb = 3.0;
        internal const double AtaqueDesdeDb = 2.0, AtaquePorDb = 0.3, AtaqueMaxDb = 1.5;
        internal const double SuavizadoMs = 5.0, SubeColaMs = 2.0, SueltaAtaqueMs = 60.0, ApagaMs = 40.0;
        internal const double PisoLentaDb = -55.0, PicoRecuerdaSeg = 0.5, PicoSeg = 1.0, MargenDb = 0.1;
        internal const int Trozo = 32;

        private readonly double techo, piso, kRA, kRS, kLA, kLS, kSuave, kSubeCola, kSueltaAtaque, pasoApaga, kPico;
        private readonly int picoRecuerda;
        private readonly double b0, b1, b2, a1, a2;              // el paso-alto (a0 = 1)
        private readonly double[] est = new double[8];           // por canal: x1, x2, y1, y2
        private volatile bool encendido;
        // Memoria que sigue la musica.
        private double ef, el;                                   // envolvente rapida y lenta (amplitud del paso-alto, mono)
        private double gdb;                                      // ganancia que se aplica al paso-alto (dB)
        private double g = 1.0, dg, gPlan = 1.0;                 // ganancia lineal de este cuadro, su paso por cuadro y a donde llega al acabar el bloque
        private double picoEnv, picoBloque;
        private int picoQueda, cf;
        private bool primado;
        private double bajaMax, subeMax;                         // lo que mas bajo y subio desde la ultima mirada (S5)
        private volatile AtaqueClaro anterior;

        internal AtaqueClaro(ISampleProvider s, bool encender) : this(s, encender, Form1.TechoPicoReal) { }
        internal AtaqueClaro(ISampleProvider s, bool encender, double techoLineal) : base(s)
        {
            encendido = encender;
            techo = techoLineal * 0.985;
            piso = Math.Pow(10.0, PisoLentaDb / 20.0);
            if (!vale) return;
            kRA = 1.0 - Math.Exp(-1.0 / (RapidaAtaqueMs * 0.001 * tasa)); kRS = 1.0 - Math.Exp(-1.0 / (RapidaSueltaMs * 0.001 * tasa));
            kLA = 1.0 - Math.Exp(-1.0 / (LentaAtaqueMs * 0.001 * tasa)); kLS = 1.0 - Math.Exp(-1.0 / (LentaSueltaMs * 0.001 * tasa));
            kSuave = 1.0 - Math.Exp(-(double)Trozo / (SuavizadoMs * 0.001 * tasa));
            kSubeCola = 1.0 - Math.Exp(-(double)Trozo / (SubeColaMs * 0.001 * tasa));
            kSueltaAtaque = 1.0 - Math.Exp(-(double)Trozo / (SueltaAtaqueMs * 0.001 * tasa));
            pasoApaga = ColaMaxDb * Trozo / (ApagaMs * 0.001 * tasa);   // de lo mas hondo a 0 en ApagaMs
            kPico = Math.Exp(-(double)Trozo / (PicoSeg * tasa));
            picoRecuerda = (int)(PicoRecuerdaSeg * tasa / Trozo);
            double w = 2.0 * Math.PI * CorteHz / tasa, c = Math.Cos(w), al = Math.Sin(w) / (2.0 * Q), a0 = 1.0 + al;
            b0 = (1.0 + c) / 2.0 / a0; b1 = -(1.0 + c) / a0; b2 = b0; a1 = -2.0 * c / a0; a2 = (1.0 - al) / a0;
        }

        internal bool Encendido { get { return encendido; } }
        internal double GananciaDb { get { return gdb; } }
        internal double PicoRecordado { get { return picoEnv; } }
        internal void Ajusta(bool encender) { if (vale) encendido = encender; }
        // Lo que mas bajo (negativo) y mas subio (positivo) la ganancia desde la ultima vez que se pregunto, en dB. Lo lee la pantalla (S5).
        internal void Toma(out double bajo, out double alto) { bajo = bajaMax; alto = subeMax; bajaMax = 0; subeMax = 0; }

        internal void Sigue(AtaqueClaro previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
        protected override bool Hereda()
        {
            AtaqueClaro a = anterior; anterior = null;
            if (a == null || !a.vale || !a.primado || a.tasa != tasa || a.canales != canales) return false;
            Array.Copy(a.est, est, est.Length);
            ef = a.ef; el = a.el; gdb = a.gdb; g = a.g; dg = a.dg; gPlan = a.gPlan;
            picoEnv = a.picoEnv; picoBloque = a.picoBloque; picoQueda = a.picoQueda; cf = a.cf; primado = true;
            return true;
        }

        protected override bool Quieta()
        {
            if (encendido || gdb != 0.0 || g != 1.0 || dg != 0.0) return false;
            primado = false;   // al encender de nuevo se vuelve a medir desde cero
            return true;
        }

        // La ganancia (dB) que pide d, con el sitio que deja el techo para la subida.
        private double Meta(double d)
        {
            if (d < ColaDesdeDb) { double c = ColaPorDb * (d - ColaDesdeDb); return c < -ColaMaxDb ? -ColaMaxDb : c; }
            if (d > AtaqueDesdeDb)
            {
                double s = AtaquePorDb * (d - AtaqueDesdeDb);
                if (s > AtaqueMaxDb) s = AtaqueMaxDb;
                if (picoEnv > 1e-9)
                {
                    double sitio = 20.0 * Math.Log10(techo / picoEnv) - MargenDb;
                    if (sitio < s) s = sitio;
                }
                return s < 0.02 ? 0.0 : s;
            }
            return 0.0;
        }

        protected override void Proceso(float[] b, int o, int cuadros)
        {
            bool on = encendido, est2 = canales == 2;
            double[] s = est;
            if (!primado)
            {   // la memoria del paso-alto parte del continuo de la primera muestra (un paso-alto en reposo da 0); las envolventes, de cero
                double p0 = b[o], p1 = est2 ? b[o + 1] : 0.0;
                s[0] = s[1] = p0; s[2] = s[3] = 0.0;
                if (est2) { s[4] = s[5] = p1; s[6] = s[7] = 0.0; }
                ef = 0.0; el = 0.0; gdb = 0.0; g = 1.0; dg = 0.0; gPlan = 1.0; picoEnv = 0.0; picoBloque = 0.0; picoQueda = 0; cf = 0;
                primado = true;
            }
            double eef = ef, eel = el, gg = g, d = dg, pb = picoBloque;
            int cc = cf;
            for (int f = 0, i = o; f < cuadros; f++, i += canales)
            {
                double x0 = b[i];
                double h0 = b0 * x0 + b1 * s[0] + b2 * s[1] - a1 * s[2] - a2 * s[3];
                if (h0 < 1e-30 && h0 > -1e-30) h0 = 0.0;
                s[1] = s[0]; s[0] = x0; s[3] = s[2]; s[2] = h0;
                double x1 = 0.0, h1 = 0.0, m = h0;
                double ax = x0 < 0 ? -x0 : x0;
                if (est2)
                {
                    x1 = b[i + 1];
                    h1 = b0 * x1 + b1 * s[4] + b2 * s[5] - a1 * s[6] - a2 * s[7];
                    if (h1 < 1e-30 && h1 > -1e-30) h1 = 0.0;
                    s[5] = s[4]; s[4] = x1; s[7] = s[6]; s[6] = h1;
                    m = 0.5 * (h0 + h1);
                    double ay = x1 < 0 ? -x1 : x1; if (ay > ax) ax = ay;
                }
                if (ax > pb) pb = ax;
                if (m < 0) m = -m;
                eef += (m > eef ? kRA : kRS) * (m - eef);
                eel += (m > eel ? kLA : kLS) * (m - eel);
                if (gg != 1.0)
                {
                    double k = gg - 1.0;
                    b[i] = (float)(x0 + k * h0);
                    if (est2) b[i + 1] = (float)(x1 + k * h1);
                }
                gg += d;
                if (++cc < Trozo) continue;
                cc = 0;
                // Un bloque entero: la envolvente mas el sitio bajo el techo dan la ganancia que toca, y un paso por cuadro hacia ella.
                if (eef < 1e-20) eef = 0.0;
                if (eel < 1e-20) eel = 0.0;
                if (pb >= picoEnv) { picoEnv = pb; picoQueda = picoRecuerda; }
                else if (picoQueda > 0) picoQueda--;
                else picoEnv *= kPico;
                pb = 0.0;
                double meta = 0.0;
                if (on && eel > piso) meta = Meta(20.0 * Math.Log10((eef + 1e-12) / eel));
                if (on)
                {
                    // Entrar a la cola o al ataque: 5 ms. Dejar la subida: 60 ms. Dejar la cola cuando llega el golpe siguiente: 2 ms, para que su
                    // primer ataque no pase por una puerta a medio cerrar.
                    double paso = (meta < gdb && gdb > 0.0) ? kSueltaAtaque : (meta > gdb && gdb < 0.0 ? kSubeCola : kSuave);
                    gdb += (meta - gdb) * paso;
                    if (gdb < 0.0005 && gdb > -0.0005 && meta == 0.0) gdb = 0.0;
                }
                else if (gdb != 0.0)
                {   // apagando: a 0 en ApagaMs, sin pasar de largo
                    gdb = gdb > 0.0 ? (gdb <= pasoApaga ? 0.0 : gdb - pasoApaga) : (-gdb <= pasoApaga ? 0.0 : gdb + pasoApaga);
                }
                if (gdb < bajaMax) bajaMax = gdb;
                if (gdb > subeMax) subeMax = gdb;
                gg = gPlan;   // llego a lo planeado, exacto
                gPlan = gdb == 0.0 ? 1.0 : Math.Pow(10.0, gdb / 20.0);
                d = (gPlan - gg) / Trozo;
            }
            ef = eef; el = eel; g = gg; dg = d; picoBloque = pb; cf = cc;
        }
    }
}

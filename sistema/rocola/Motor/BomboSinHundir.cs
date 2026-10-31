// QUE EL BOMBO NO HUNDA LA VOZ (27 de octubre de 2026): el bajo, con su propio margen, delante del limitador de pico real.
//
// El limitador de pico real (PicoReal.cs) baja la mezcla ENTERA, la voz incluida, cuando lo unico que pasa del techo es el bombo
// o el bajo. En un billar, con el bajo cargado y la voz arriba, eso se oye como una voz que se hunde a cada golpe de bombo. Esta
// etapa lo arregla en el origen: cuando el bajo es lo que hace pasar la senal del techo, baja SOLO el bajo lo que haga falta (como
// mucho 6 dB) y deja al resto de la mezcla en su sitio. Lo que aun asi no cabe, lo recorta el limitador de siempre, que va detras.
//
// COMO SE DECIDE (por canal, con una sola ganancia para los dos):
//   - bajo = paso-bajo de 140 Hz (Butterworth de dos polos); resto = paso-alto de 200 Hz (idem), con su envolvente de pico (cae con 5 ms
//     de constante). El oido de la etapa es la suma del bajo de este instante y lo que el resto lleva en los ultimos milisegundos: si
//     esa suma pasa del techo (TechoPicoReal), la ganancia que le toca al bajo es g = (techo - resto) / |bajo|: el bajo recibe solo el
//     margen que el resto le deja, con un piso de 0,5 (-6 dB). Mirar la envolvente del resto y no su valor de este instante es lo que
//     hace que la peticion dure todo el ciclo del bajo (con el valor de este instante solo pedia en el cuadro en que los dos coincidian,
//     y eso dura menos que el ataque).
//   - SOLO SI DE VERDAD PASO DEL TECHO. Esa cuenta es una cota (supone que el pico del bajo y el pico del resto coinciden): la etapa solo la
//     usa si alguna muestra de los ultimos 30 ms paso de verdad del techo. Mientras nada pase, la salida es la entrada, bit a bit, aunque la
//     cota diga que podria pasar. (Medido en tres temas sinteticos: sin esta condicion el bombo bajaba hasta 1,6 dB sin que el limitador
//     fuera a tocar nada.)
//   - SOLO SI EL BAJO MANDA. Si la que pasa del techo es la voz o los agudos (el resto solo ya pasa del techo, o el bajo es menos del 40 %
//     de la suma), bajar el bajo no arreglaria nada y solo quitaria cuerpo: no se toca.
//   - TIEMPOS. Ataque de 0,5 ms (sin mirar hacia adelante: la etapa no anade ningun retardo), ESPERA de 20 ms con la ganancia quieta
//     y suelta de 80 ms. La espera es la leccion de pruebas/soltar.cs: soltar a media vuelta de un ciclo de 50 Hz hace subir y bajar la
//     ganancia con la onda y el bajo sale distorsionado; con 20 ms quieta una nota sostenida sale con una ganancia constante.
//
// COMO SE BAJA EL BAJO. Con un estante de graves (RBJ, 140 Hz, Q 0,7071) cuya ganancia es g, uno por canal, con los coeficientes
// recalculados cada 4 cuadros mientras g cambia. Se penso primero en el reparto COMPLEMENTARIO (salida = entrada - (1 - g) * paso-bajo, o sea
// resto + g * bajo) y no sirve: un paso-bajo causal gira la fase del grave, y entrada - paso-bajo deja pasar mucho del propio bajo (a 60 Hz
// el "resto" de ese reparto lleva el 62 % de la senal) y sube hasta 1 dB entre 150 y 300 Hz. Con g = 0,5 solo bajaba 3,4 dB a 60 Hz y
// 0,9 dB a 100 Hz (calculado con la respuesta del filtro). El estante baja g entero por debajo de ~80 Hz, la mitad a 140 Hz y casi nada
// por encima de 400 Hz, y no sube nada en ningun sitio.
//
// Mientras g = 1 (casi siempre) la muestra sale BIT A BIT lo que entra, tambien con la etapa encendida: no se calcula ningun estante ni
// se recombina nada. Al soltar, cuando le falta menos de una diezmilesima (-0,0009 dB) vuelve a 1 exacto. Apagada (de fabrica) ni se
// calcula; si se apaga a media bajada, la ganancia vuelve a 1 en 40 ms, sin escalon. «Escuchar sin mejoras» (S4) la deja como esta: es
// de la familia del limitador, que tampoco se quita. Va detras del bajo psicoacustico (DeepBass, que es quien anade los armonicos) y
// justo delante del limitador.
//
// Lecturas partidas, disco continuo (Sigue) y cebado, como PresenciaSala (EtapaPorCuadros.cs). Cuesta poco mas de 1 us por cuadro.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class BomboSinHundir : EtapaPorCuadros
    {
        internal const double CorteHz = 140.0, RestoHz = 200.0, Q = 0.7071;
        internal const double AtaqueMs = 0.5, EsperaMs = 20.0, SueltaMs = 80.0, ApagaMs = 40.0, RestoMs = 5.0, CalientaMs = 20.0;
        internal const double MaxBajaDb = 6.0;
        internal const double MemoriaMs = 30.0;               // cuanto se recuerda que una muestra paso del techo
        internal const double PesoMin = 0.4;                 // el bajo tiene que ser al menos el 40 % de la suma para tocarse
        internal const int CadaCuadros = 4;                  // cada cuantos cuadros se recalcula el estante mientras g cambia

        private readonly double techo, gMin, kAta, kSue, kResto, pasoApaga;
        private readonly int espera, memoria;
        private readonly double lb0, lb1, lb2, la1, la2;     // el paso-bajo del bajo (a0 = 1)
        private readonly double hb0, hb1, hb2, ha1, ha2;     // el paso-alto del resto (a0 = 1)
        private readonly double seno, coseno;                // del estante
        private readonly double[] est = new double[8];       // paso-bajo, por canal: x1, x2, y1, y2
        private readonly double[] alt = new double[8];       // paso-alto, por canal
        private readonly double[] sh = new double[8];        // estante, por canal
        private double env0, env1;                           // envolvente de pico del resto, por canal
        private double sb0 = 1, sb1, sb2, sa1, sa2;          // el estante ahora (a0 = 1)
        private volatile bool encendido;
        private double g = 1.0, meta = 1.0;                  // la ganancia y a donde va (lo mas hondo que se pidio en la espera)
        private int esperando, cuentaCoef, calienta, sinPasar;        // calienta: cuadros que faltan para que los filtros ya sigan la musica
        private bool primado;
        private double bajaMax;                              // lo que mas bajo el bajo desde la ultima mirada (S5)
        private volatile BomboSinHundir anterior;

        internal BomboSinHundir(ISampleProvider s, bool encender) : this(s, encender, Form1.TechoPicoReal) { }
        internal BomboSinHundir(ISampleProvider s, bool encender, double techoLineal) : base(s)
        {
            encendido = encender;
            techo = techoLineal;
            gMin = Math.Pow(10.0, -MaxBajaDb / 20.0);
            kAta = 1.0 - Math.Exp(-1.0 / (AtaqueMs * 0.001 * tasa));
            kSue = 1.0 - Math.Exp(-1.0 / (SueltaMs * 0.001 * tasa));
            kResto = Math.Exp(-1.0 / (RestoMs * 0.001 * tasa));
            espera = (int)(EsperaMs * 0.001 * tasa);
            memoria = (int)(MemoriaMs * 0.001 * tasa);
            pasoApaga = (1.0 - gMin) / (ApagaMs * 0.001 * tasa);   // de lo mas bajo a 1 en ApagaMs
            double w = 2.0 * Math.PI * CorteHz / tasa, c = Math.Cos(w), al = Math.Sin(w) / (2.0 * Q), a0 = 1.0 + al;
            lb0 = (1.0 - c) / 2.0 / a0; lb1 = (1.0 - c) / a0; lb2 = lb0; la1 = -2.0 * c / a0; la2 = (1.0 - al) / a0;
            seno = Math.Sin(w); coseno = c;
            w = 2.0 * Math.PI * Math.Min(RestoHz, 0.4 * tasa) / tasa; c = Math.Cos(w); al = Math.Sin(w) / (2.0 * Q); a0 = 1.0 + al;
            hb0 = (1.0 + c) / 2.0 / a0; hb1 = -(1.0 + c) / a0; hb2 = hb0; ha1 = -2.0 * c / a0; ha2 = (1.0 - al) / a0;
        }

        internal bool Encendido { get { return encendido; } }
        internal double GananciaAhora { get { return g; } }
        internal void Ajusta(bool encender) { if (vale) encendido = encender; }
        // Lo que mas bajo el bajo desde la ultima vez que se pregunto, en dB (0 = nada). Lo lee la pantalla (S5).
        internal double BajaDb { get { double b = bajaMax; bajaMax = 0; return b > 0 ? -20.0 * Math.Log10(1.0 - b) : 0.0; } }

        internal void Sigue(BomboSinHundir previa) { if (previa != null && !object.ReferenceEquals(previa, this)) anterior = previa; }
        protected override bool Hereda()
        {
            BomboSinHundir a = anterior; anterior = null;
            if (a == null || !a.vale || !a.primado || a.tasa != tasa || a.canales != canales) return false;
            Array.Copy(a.est, est, est.Length); Array.Copy(a.alt, alt, alt.Length); Array.Copy(a.sh, sh, sh.Length);
            env0 = a.env0; env1 = a.env1; sb0 = a.sb0; sb1 = a.sb1; sb2 = a.sb2; sa1 = a.sa1; sa2 = a.sa2;
            g = a.g; meta = a.meta; esperando = a.esperando; cuentaCoef = a.cuentaCoef; calienta = a.calienta; sinPasar = a.sinPasar; primado = true;
            return true;
        }

        protected override bool Quieta()
        {
            if (encendido || g != 1.0) return false;
            primado = false;   // al encender de nuevo parte de la memoria del momento, no de la de hace rato
            return true;
        }

        // El estante de graves de ganancia 'gl' (lineal) a CorteHz (RBJ), normalizado (a0 = 1).
        private void Estante(double gl)
        {
            double A = Math.Sqrt(gl), k = 2.0 * Math.Sqrt(A) * (seno / (2.0 * Q));
            double Am = A - 1.0, Ap = A + 1.0, a0 = Ap + Am * coseno + k;
            sb0 = A * (Ap - Am * coseno + k) / a0; sb1 = 2.0 * A * (Am - Ap * coseno) / a0; sb2 = A * (Ap - Am * coseno - k) / a0;
            sa1 = -2.0 * (Am + Ap * coseno) / a0; sa2 = (Ap + Am * coseno - k) / a0;
        }

        // Cuanta ganancia le pide al bajo este canal (1 = nada): el margen que le deja el resto, solo si el bajo manda.
        private double Demanda(double bajo, double resto)
        {
            double ab = bajo < 0 ? -bajo : bajo, suma = ab + resto;
            if (suma <= techo || resto >= techo || ab < suma * PesoMin) return 1.0;
            double d = (techo - resto) / ab;
            return d < gMin ? gMin : d;
        }

        protected override void Proceso(float[] b, int o, int cuadros)
        {
            bool on = encendido, est2 = canales == 2;
            double[] s = est, h = alt, e = sh;
            double gg = g, mt = meta, bm = bajaMax, ev0 = env0, ev1 = env1;
            int esp = esperando, cc = cuentaCoef, cal = calienta, sp = sinPasar;
            if (!primado)
            {   // la memoria de los filtros parte del continuo de la primera muestra (a 0 daria un escalon al encender en pleno golpe)
                double p0 = b[o], p1 = est2 ? b[o + 1] : 0.0;
                s[0] = s[1] = s[2] = s[3] = p0; e[0] = e[1] = e[2] = e[3] = p0;
                h[0] = h[1] = p0; h[2] = h[3] = 0.0;
                if (est2) { s[4] = s[5] = s[6] = s[7] = p1; e[4] = e[5] = e[6] = e[7] = p1; h[4] = h[5] = p1; h[6] = h[7] = 0.0; }
                ev0 = ev1 = 0.0; cc = 0; cal = (int)(CalientaMs * 0.001 * tasa); sp = memoria;
                primado = true;
            }
            for (int i = o, fin = o + cuadros * canales; i < fin; i += canales)
            {
                double x0 = b[i];
                double y0 = lb0 * x0 + lb1 * s[0] + lb2 * s[1] - la1 * s[2] - la2 * s[3];
                if (y0 < 1e-30 && y0 > -1e-30) y0 = 0.0;
                s[1] = s[0]; s[0] = x0; s[3] = s[2]; s[2] = y0;
                double dem = 1.0;
                if (on)
                {
                    double r0 = hb0 * x0 + hb1 * h[0] + hb2 * h[1] - ha1 * h[2] - ha2 * h[3];
                    if (r0 < 1e-30 && r0 > -1e-30) r0 = 0.0;
                    h[1] = h[0]; h[0] = x0; h[3] = h[2]; h[2] = r0;
                    if (r0 < 0) r0 = -r0;
                    ev0 = r0 > ev0 ? r0 : ev0 * kResto;
                    if (ev0 < 1e-20) ev0 = 0.0;
                    dem = Demanda(y0, ev0);
                }
                double x1 = 0.0;
                if (est2)
                {
                    x1 = b[i + 1];
                    double y1 = lb0 * x1 + lb1 * s[4] + lb2 * s[5] - la1 * s[6] - la2 * s[7];
                    if (y1 < 1e-30 && y1 > -1e-30) y1 = 0.0;
                    s[5] = s[4]; s[4] = x1; s[7] = s[6]; s[6] = y1;
                    if (on)
                    {
                        double r1 = hb0 * x1 + hb1 * h[4] + hb2 * h[5] - ha1 * h[6] - ha2 * h[7];
                        if (r1 < 1e-30 && r1 > -1e-30) r1 = 0.0;
                        h[5] = h[4]; h[4] = x1; h[7] = h[6]; h[6] = r1;
                        if (r1 < 0) r1 = -r1;
                        ev1 = r1 > ev1 ? r1 : ev1 * kResto;
                        if (ev1 < 1e-20) ev1 = 0.0;
                        double d1 = Demanda(y1, ev1);
                        if (d1 < dem) dem = d1;
                    }
                }
                double ax = x0 < 0 ? -x0 : x0; if (est2) { double a1x = x1 < 0 ? -x1 : x1; if (a1x > ax) ax = a1x; }
                if (ax > techo) sp = 0; else if (sp < memoria) sp++;
                if (sp >= memoria) dem = 1.0;   // nada paso del techo en los ultimos 30 ms: no hay nada que arreglar
                if (cal > 0) { cal--; dem = 1.0; }   // los primeros milisegundos el paso-bajo aun arrastra el continuo con el que se cebo: no es bajo
                if (on)
                {
                    // Lo mas hondo que se pidio se queda como meta durante la espera, y se refresca cada vez que se vuelve a pedir lo
                    // mismo o mas: un pico de bajo dura menos que el ataque, y sin la meta la ganancia no llegaria a donde hace falta.
                    if (dem < 1.0)
                    {
                        if (dem < mt) mt = dem;
                        if (dem <= mt + 1e-3) esp = espera;
                    }
                    if (esp > 0) esp--; else mt = 1.0;
                    if (gg > mt) gg += kAta * (mt - gg);
                    else if (esp == 0 && gg < 1.0) { gg += kSue * (1.0 - gg); if (1.0 - gg < 1e-4) gg = 1.0; }
                }
                else { mt = 1.0; if (gg < 1.0) { esp = 0; gg += pasoApaga; if (gg > 1.0) gg = 1.0; } }
                if (gg != 1.0)
                {
                    if (--cc < 0) { Estante(gg); cc = CadaCuadros - 1; }
                    double v = sb0 * x0 + sb1 * e[0] + sb2 * e[1] - sa1 * e[2] - sa2 * e[3];
                    if (v < 1e-30 && v > -1e-30) v = 0.0;
                    e[1] = e[0]; e[0] = x0; e[3] = e[2]; e[2] = v;
                    b[i] = (float)v;
                    if (est2)
                    {
                        v = sb0 * x1 + sb1 * e[4] + sb2 * e[5] - sa1 * e[6] - sa2 * e[7];
                        if (v < 1e-30 && v > -1e-30) v = 0.0;
                        e[5] = e[4]; e[4] = x1; e[7] = e[6]; e[6] = v;
                        b[i + 1] = (float)v;
                    }
                    double k = 1.0 - gg;
                    if (k > bm) bm = k;
                }
                else
                {   // sin bajar nada la salida es la entrada, y la memoria del estante la sigue (y = x): al empezar a bajar no hay escalon
                    cc = 0;
                    e[1] = e[0]; e[0] = x0; e[3] = e[2]; e[2] = x0;
                    if (est2) { e[5] = e[4]; e[4] = x1; e[7] = e[6]; e[6] = x1; }
                }
            }
            g = gg; meta = mt; esperando = esp; bajaMax = bm; env0 = ev0; env1 = ev1; cuentaCoef = cc; calienta = cal; sinPasar = sp;
        }
    }
}

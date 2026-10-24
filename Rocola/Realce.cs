// EL REALCE DE AGUDOS, SOBREMUESTREADO SOLO DONDE HACE FALTA
//
// El realce (LevelTap) separa la banda alta con un paso-alto de un polo y la satura suave: out = v + k*tanh(2,5*h).
// La saturacion crea armonicos NUEVOS, que es justo lo que se busca (el "aire" que el MP3 recorto). Pero a 44,1 kHz
// los armonicos que pasan de 22 kHz no desaparecen: se PLIEGAN y vuelven como tonos que no son armonicos de nada.
// Un platillo en 15 kHz saturado da su tercer armonico en 45 kHz, y a 44,1 kHz eso reaparece en 0,9 kHz. Medido
// contra una referencia sin plegado (pruebas/ref_realce.py, pruebas/realce.cs): con el realce de fabrica, un tono de
// 11 a 20 kHz a -6 dB dejaba productos plegados 47 a 50 dB por debajo de el; en musica tipo MP3 que llega a 19 kHz,
// el peor tercio de octava quedaba a solo 27 dB de lo que suena en esa banda (18 con el realce al maximo).
//
// Aqui la saturacion se calcula al DOBLE de frecuencia, pero solo su parte NO lineal:
//     tanh(2,5h) = 2,5h + resto(h)
// La parte lineal (2,5h) no crea nada nuevo y no hay que filtrarla (filtrarla le quitaria agudos al realce); el
// resto es lo que fabrica armonicos, y ese se sube a 2x, se satura alli y se baja con un filtro de media banda
// (63 coeficientes, Kaiser 7) que se queda con lo que cabe y quita lo que se plegaria. El filtro retrasa 31
// muestras (0,7 ms a 44,1 kHz); la parte lineal y el sonido seco se retrasan EXACTAMENTE lo mismo, asi que todo
// sale alineado: es el mismo realce de siempre, con sus armonicos, sin el plegado. Medido igual: los tonos quedan
// entre -69 dB (20 kHz, pegado a Nyquist) y -126 dB; la musica tipo MP3 que llega a 19 kHz, a -55/-59 dB en su
// peor tercio de octava (-103 dBFS en nivel absoluto), y la que llega a 16 kHz, a -89/-95 dB.
//
// Por que 2x y no 4x (medido, no supuesto): lo que queda a 2x viene de dos sitios. Uno, la imagen de lo que hay
// muy cerca de Nyquist (19-22 kHz), que un 4x no arregla (depende del primer filtro). Otro, los productos de quinto
// orden de lo fuerte por encima de 13 kHz: ahi un 4x bien hecho (63 + 15 coeficientes) si gana, 11 a 13 dB en la
// peor banda de la musica que llega a 19 kHz y hasta -100 dB en tonos puros fuertes, pero cuesta 1,6 a 1,75 veces
// (81-88 frente a 48-50 ns por muestra y canal, medido) y esa banda ya esta a -103 dBFS. Un 4x con el segundo
// filtro corto resulto PEOR que este 2x. Para Modest igual: el mismo 2x.
//
// Coste (Mono, esta maquina): el nucleo, 48-50 ns por muestra y canal; LevelTap entero, 74 ns (el realce de antes,
// 29; sin realce, 12). En la cadena completa, un bloque de 50 ms pasa de 1,01 a 1,20 ms. Las dos sumas del filtro
// estan desenrolladas a mano: con un bucle, el JIT comprueba los limites del arreglo en cada coeficiente y cuesta
// el doble (98 ns).
//
// El grave sintetico (DeepBassProvider) NO se sobremuestrea: medido igual, su plegado queda entre 102 y 129 dB por
// debajo de los armonicos que genera (trabaja por debajo de 320 Hz; sus productos no llegan ni cerca de Nyquist).
//
// El retraso seco es siempre de 31 cuadros, tambien si empieza apagado. Los FIR solo se calculan
// con mezcla no nula; la historia barata de entrada permite cebarlos al encender sin saltar en el tiempo
// ni volver al realce sin sobremuestreo. Al acabar se vacia la misma cola en todos los modos.
//
// DISCO CONTINUO (24-10-2026). La pista siguiente del mismo disco empezaba el realce de cero: el paso-alto de un polo en 0 (la
// primera muestra salia como si fuera toda agudos) y la historia de 62 muestras vacia. Medido en la cadena entera, con un disco
// partido en dos: un escalon en el cambio de 0,011 con la musica a 0,25 de pico (lo que mas quedaba, una vez arreglado el resto).
// Ahora la anterior guarda, al acabar su musica y antes de vaciar su cola, el estado del realce y el del EQ, y la siguiente sigue
// con ellos (LevelTap.Sigue, desde SigueA). Los 31 cuadros que la siguiente saca primero son la cola de la anterior otra vez, y
// el relevo los tira (son su cebado); lo que viene despues es la siguiente con la historia de verdad.
using System;

namespace Rocola
{
    internal sealed class RealceSobremuestreado
    {
        internal const int Pares = 16;                     // pares de coeficientes de la fase de media muestra
        internal const int Largo = 4 * Pares - 1;          // 63 coeficientes del filtro de media banda a 2x
        internal const double Beta = 7.0;                  // ventana de Kaiser
        internal const int Retraso = 2 * Pares - 1;        // 31 muestras de la frecuencia de trabajo
        internal const int Trozo = 512;                    // cuadros por pasada: memoria fija, sin reservas al leer
        // C[t] multiplica h[m-t] y su espejo h[m-(2*Pares-1-t)]: la muestra a medio camino. Contando cada uno dos
        // veces suman 1 (la continua pasa entera). La fase de las muestras de siempre es la propia muestra: el filtro
        // no la toca.
        internal static readonly double[] C;

        static RealceSobremuestreado()
        {
            int n = 2 * Pares, centro = (Largo - 1) / 2;
            double[] lado = new double[n];
            double suma = 0, i0b = I0(Beta);
            for (int t = 0; t < n; t++)
            {
                int k = 2 * t;                                  // coeficiente k del filtro entero (los pares)
                double x = (k - centro) / 2.0;                  // siempre media muestra: sinc nunca es 0 aqui
                double s = Math.Sin(Math.PI * x) / (Math.PI * x);
                double r = 2.0 * k / (Largo - 1) - 1.0;
                lado[t] = s * I0(Beta * Math.Sqrt(Math.Max(0.0, 1.0 - r * r))) / i0b;
                suma += lado[t];
            }
            C = new double[Pares];
            for (int t = 0; t < Pares; t++) C[t] = lado[t] / suma;
        }

        private static double I0(double x)
        {
            double s = 1, t = 1;
            for (int k = 1; k < 60; k++) { t *= (x / (2 * k)) * (x / (2 * k)); s += t; if (t < 1e-17 * s) break; }
            return s;
        }

        // Lo que la saturacion AGREGA a la parte lineal: TanhRapida(2,5u) - 2,5u, la misma curva, en double.
        internal static double Resto(double u)
        {
            double x = 2.5 * u;
            if (!(x > -3.0)) return -1.0 - x;
            if (x > 3.0) return 1.0 - x;
            double x2 = x * x;
            return x * (27.0 + x2) / (27.0 + 9.0 * x2) - x;
        }

        // Por canal: los Retraso valores anteriores y luego el trozo en curso, de la banda alta (xh), del seco
        // (xv), del resto a media muestra (xg) y del resto en la muestra (xn).
        private readonly double[][] xh, xv, xg, xn;
        private readonly float[] lp;
        private readonly bool[] listo;
        private readonly float polo;
        private readonly double c0, c1, c2, c3, c4, c5, c6, c7, c8, c9, c10, c11, c12, c13, c14, c15;
        private readonly double d0, d1, d2, d3, d4, d5, d6, d7, d8, d9, d10, d11, d12, d13, d14, d15;
        // Lo guardado al acabar la musica (Guarda), para la pista siguiente del mismo disco (Toma).
        private readonly double[][] fh, fv, fg, fn;
        private readonly float[] flp;
        private readonly bool[] flisto;

        internal RealceSobremuestreado(int canales, float polo)
        {
            canales = Math.Max(1, canales);
            this.polo = polo;
            lp = new float[canales]; listo = new bool[canales];
            xh = new double[canales][]; xv = new double[canales][]; xg = new double[canales][]; xn = new double[canales][];
            fh = new double[canales][]; fv = new double[canales][]; fg = new double[canales][]; fn = new double[canales][];
            flp = new float[canales]; flisto = new bool[canales];
            for (int c = 0; c < canales; c++)
            {
                xh[c] = new double[2 * Retraso + Trozo]; xv[c] = new double[2 * Retraso + Trozo];
                xg[c] = new double[2 * Retraso + Trozo]; xn[c] = new double[2 * Retraso + Trozo];
                fh[c] = new double[2 * Retraso]; fv[c] = new double[2 * Retraso]; fg[c] = new double[2 * Retraso]; fn[c] = new double[2 * Retraso];
            }
            double[] k = C;
            c0 = k[0]; c1 = k[1]; c2 = k[2]; c3 = k[3]; c4 = k[4]; c5 = k[5]; c6 = k[6]; c7 = k[7];
            c8 = k[8]; c9 = k[9]; c10 = k[10]; c11 = k[11]; c12 = k[12]; c13 = k[13]; c14 = k[14]; c15 = k[15];
            d0 = .5 * k[0]; d1 = .5 * k[1]; d2 = .5 * k[2]; d3 = .5 * k[3]; d4 = .5 * k[4]; d5 = .5 * k[5]; d6 = .5 * k[6]; d7 = .5 * k[7];
            d8 = .5 * k[8]; d9 = .5 * k[9]; d10 = .5 * k[10]; d11 = .5 * k[11]; d12 = .5 * k[12]; d13 = .5 * k[13]; d14 = .5 * k[14]; d15 = .5 * k[15];
        }

        // Entre trozos, la historia de cada canal son los primeros 2 * Retraso valores de cada arreglo, mas el paso-bajo y si los restos
        // ya estaban hechos. Guarda la copia; Toma la de otra (la pista anterior del mismo disco) como propia.
        internal void Guarda()
        {
            const int H = 2 * Retraso;
            for (int c = 0; c < lp.Length; c++)
            {
                Array.Copy(xh[c], fh[c], H); Array.Copy(xv[c], fv[c], H); Array.Copy(xg[c], fg[c], H); Array.Copy(xn[c], fn[c], H);
                flp[c] = lp[c]; flisto[c] = listo[c];
            }
        }
        internal void Toma(RealceSobremuestreado a)
        {
            const int H = 2 * Retraso;
            if (a == null || a.lp.Length != lp.Length || a.polo != polo) return;
            for (int c = 0; c < lp.Length; c++)
            {
                Array.Copy(a.fh[c], xh[c], H); Array.Copy(a.fv[c], xv[c], H); Array.Copy(a.fg[c], xg[c], H); Array.Copy(a.fn[c], xn[c], H);
                lp[c] = a.flp[c]; listo[c] = a.flisto[c];
            }
        }

        private void CalculaResto(double[] h, double[] g, double[] nn, int desde, int hasta)
        {
            double[] xh = h;
            double c0 = this.c0, c1 = this.c1, c2 = this.c2, c3 = this.c3, c4 = this.c4, c5 = this.c5, c6 = this.c6, c7 = this.c7;
            double c8 = this.c8, c9 = this.c9, c10 = this.c10, c11 = this.c11, c12 = this.c12, c13 = this.c13, c14 = this.c14, c15 = this.c15;
            for (int a = desde; a < hasta; a++)
            {
                int z = a - Retraso;
                double y = c0 * (xh[a] + xh[z]) + c1 * (xh[a - 1] + xh[z + 1]) + c2 * (xh[a - 2] + xh[z + 2]) + c3 * (xh[a - 3] + xh[z + 3]) +
                           c4 * (xh[a - 4] + xh[z + 4]) + c5 * (xh[a - 5] + xh[z + 5]) + c6 * (xh[a - 6] + xh[z + 6]) + c7 * (xh[a - 7] + xh[z + 7]) +
                           c8 * (xh[a - 8] + xh[z + 8]) + c9 * (xh[a - 9] + xh[z + 9]) + c10 * (xh[a - 10] + xh[z + 10]) + c11 * (xh[a - 11] + xh[z + 11]) +
                           c12 * (xh[a - 12] + xh[z + 12]) + c13 * (xh[a - 13] + xh[z + 13]) + c14 * (xh[a - 14] + xh[z + 14]) + c15 * (xh[a - 15] + xh[z + 15]);
                g[a] = Resto(y);
                nn[a] = Resto(xh[a - (Pares - 1)]);
            }
        }

        // Un canal: n muestras (n <= Trozo) desde buf[ini], cada salto. ks[kIni + j*salto] es la mezcla de cada una.
        // Sale en su sitio: el seco de hace Retraso muestras mas el realce de esa misma muestra, ya sin plegado.
        internal void Bloque(int canal, float[] buf, int ini, int salto, int n, float[] ks, int kIni)
        {
            const int H = 2 * Retraso;
            double[] xh = this.xh[canal], xv = this.xv[canal], xg = this.xg[canal], xn = this.xn[canal];
            float l = lp[canal], p = polo;
            bool activo = false;
            for (int q = kIni, j = 0; j < n; j++, q += salto) if (ks[q] != 0f) { activo = true; break; }
            for (int j = 0, i = ini; j < n; j++, i += salto)
            {
                float v = buf[i];
                l += (v - l) * p;
                if (l < Desnormales.TopeFloat && l > -Desnormales.TopeFloat) l = 0f;   // se apaga en pocas muestras: aqui, en cada una (Desnormales.cs)
                xh[H + j] = v - l; xv[H + j] = v;
            }
            lp[canal] = l;
            if (!activo)
            {
                for (int j = 0, i = ini; j < n; j++, i += salto) buf[i] = (float)xv[j + Retraso];
                listo[canal] = false;
                Array.Copy(xh, n, xh, 0, H); Array.Copy(xv, n, xv, 0, H);
                return;
            }
            // Reconstruir los restos de los 31 cuadros anteriores con la historia completa,
            // no con ceros: el primer cuadro tras activar tiene el mismo filtro que el continuo.
            if (!listo[canal]) CalculaResto(xh, xg, xn, H - Retraso, H);
            CalculaResto(xh, xg, xn, H, H + n);
            listo[canal] = true;
            double d0 = this.d0, d1 = this.d1, d2 = this.d2, d3 = this.d3, d4 = this.d4, d5 = this.d5, d6 = this.d6, d7 = this.d7;
            double d8 = this.d8, d9 = this.d9, d10 = this.d10, d11 = this.d11, d12 = this.d12, d13 = this.d13, d14 = this.d14, d15 = this.d15;
            // Bajar a 1x con el mismo filtro (a la mitad) y juntar con la parte lineal y el seco, todos de hace Retraso.
            for (int j = 0, i = ini, q = kIni; j < n; j++, i += salto, q += salto)
            {
                int a = j + H, z = a - Retraso;
                double w = .5 * xn[a - Pares] +
                           d0 * (xg[a] + xg[z]) + d1 * (xg[a - 1] + xg[z + 1]) + d2 * (xg[a - 2] + xg[z + 2]) + d3 * (xg[a - 3] + xg[z + 3]) +
                           d4 * (xg[a - 4] + xg[z + 4]) + d5 * (xg[a - 5] + xg[z + 5]) + d6 * (xg[a - 6] + xg[z + 6]) + d7 * (xg[a - 7] + xg[z + 7]) +
                           d8 * (xg[a - 8] + xg[z + 8]) + d9 * (xg[a - 9] + xg[z + 9]) + d10 * (xg[a - 10] + xg[z + 10]) + d11 * (xg[a - 11] + xg[z + 11]) +
                           d12 * (xg[a - 12] + xg[z + 12]) + d13 * (xg[a - 13] + xg[z + 13]) + d14 * (xg[a - 14] + xg[z + 14]) + d15 * (xg[a - 15] + xg[z + 15]);
                buf[i] = (float)xv[z] + ks[q] * (float)(2.5 * xh[z] + w);
            }
            Array.Copy(xh, n, xh, 0, H); Array.Copy(xv, n, xv, 0, H);
            Array.Copy(xg, n, xg, 0, H); Array.Copy(xn, n, xn, 0, H);
        }
    }

    public partial class Form1
    {
        private partial class LevelTap
        {
            // Siempre 2x con retraso estable; apagado usa solo la historia y el seco.
            private int modoRealce;
            private RealceSobremuestreado realce;   // se reserva al construir: leer no reserva nada
            private float[] realceK;                // la mezcla del realce de cada muestra del trozo
            private bool vaciando;                  // la fuente ya acabo: se entrega lo retenido
            private int porVaciar;                  // muestras retenidas que faltan por entregar
            // DISCO CONTINUO (ver la cabecera): lo de esta al acabar su musica, y la anterior del mismo disco, de la que seguir.
            private volatile LevelTap anterior;
            private bool finGuardado;
            private float[] finEq;                  // la memoria del EQ al acabar
            private int finMascara = -1;            // que bandas llevaba (-1: sin EQ)
            // El cebado de delante (el des-recorte y el siseo; lo pone ArmaCadena): tras heredar pasa tal cual, sin entrar en el realce.
            internal int Cebado;
            private int cebadoPorPasar;

            private void PreparaRealce()
            {
                realce = new RealceSobremuestreado(audioChannels, exciterPole);
                realceK = new float[RealceSobremuestreado.Trozo * audioChannels];
                finEq = new float[Math.Max(1, audioChannels) * 32];
            }

            // La pista siguiente del mismo disco sigue con el realce y el EQ de esta. Lo llama la pantalla al preparar el relevo (SigueA);
            // se toma en la primera lectura con musica, en el hilo del sonido, cuando la anterior ya acabo.
            internal void Sigue(LevelTap previa) { if (previa != null && !ReferenceEquals(previa, this)) anterior = previa; }
            // Al acabar la musica, antes de vaciar la cola (que mete ceros): lo que la siguiente necesita para seguir.
            private void GuardaFin()
            {
                realce.Guarda();
                PreparedEq e = currentEq;
                if (e != null && e.State.Length == finEq.Length) { Array.Copy(e.State, finEq, finEq.Length); finMascara = e.Mascara; }
                else finMascara = -1;
                finGuardado = true;
            }
            // En la primera lectura: el realce de la anterior, y su memoria del EQ si lleva las mismas bandas.
            private void HeredaFin()
            {
                LevelTap a = anterior; anterior = null;
                if (a == null || !a.finGuardado || a.audioChannels != audioChannels || a.WaveFormat.SampleRate != WaveFormat.SampleRate) return;
                realce.Toma(a.realce);
                cebadoPorPasar = Math.Max(0, Cebado) * audioChannels;
                PreparedEq e = currentEq;
                if (e != null && a.finMascara == e.Mascara && e.State.Length == a.finEq.Length) Array.Copy(a.finEq, e.State, e.State.Length);
            }

            // El modo no depende de la intensidad inicial ni de los cambios en vivo.
            private void FijaModoRealce(float excite)
            {
                modoRealce = 2;
            }

            // Cuadros que esta etapa retiene y entrega al final (para la cuenta del fundido). Fija el modo.
            public int DelayFrames
            {
                get { FijaModoRealce(Excite); return RealceSobremuestreado.Retraso; }
            }

            // Ganancia, EQ y rampas muestra a muestra como siempre (primera pasada); el realce, por canal y por
            // trozos (segunda pasada), con la mezcla que tocaba en cada muestra.
            private void ProcesaConRealce(float[] buffer, int offset, int n, float g, float ex, PreparedEq prepared)
            {
                int ch = audioChannels, fin = offset + n;
                for (int s = offset; s < fin; )
                {
                    int m = Math.Min(fin - s, RealceSobremuestreado.Trozo * ch);
                    int primero = audioChannel;
                    if (primero == 0 && m % ch == 0 && PasadaQuieta(g, ex, prepared)) PrimeraPasadaQuieta(buffer, s, m / ch, ch);
                    else
                    for (int i = s; i < s + m; i++)
                    {
                        if (audioChannel == 0) AdvanceAudioControls(g, ex, prepared);
                        float input = buffer[i] * gainRamp.Current;
                        float v = canalesMusicales[audioChannel] ? ApplyPreparedEq(currentEq, input, audioChannel) : input;
                        if (eqMix < 1f && canalesMusicales[audioChannel])
                        {
                            float old = ApplyPreparedEq(previousEq, input, audioChannel);
                            v = old + (v - old) * eqMix;
                        }
                        buffer[i] = v;
                        realceK[i - s] = canalesMusicales[audioChannel] ? exciteRamp.Current * .5f * .4f : 0f;
                        if (++audioChannel >= ch) audioChannel = 0;
                    }
                    for (int c = 0; c < ch; c++)
                    {
                        int desde = (c - primero + ch) % ch;
                        if (desde >= m) continue;
                        realce.Bloque(c, buffer, s + desde, ch, (m - desde + ch - 1) / ch, realceK, desde);
                    }
                    s += m;
                }
            }

            // LA PRIMERA PASADA CUANDO NADA CAMBIA (22-10-2026). Ganancia, EQ y mezcla del realce van muestra a muestra con cuatro cosas por cuadro (las
            // rampas, el cruce de EQ, la limpieza de restos) y, en cada muestra, el recorrido de las bandas del EQ por el arreglo de estados. Pero casi
            // siempre NADA cambia: las rampas ya llegaron, el EQ es el mismo y no hay cruce en curso, o sea que lo unico que hace cada cuadro es contar
            // hasta 64 para la limpieza de restos diminutos (Desnormales.cs). Entonces el trozo se hace por bandas: la ganancia (una constante), cada
            // banda del EQ de punta a punta del trozo con su memoria y sus coeficientes en variables (los dos canales de un estereo a la vez) y la mezcla
            // del realce (una constante). Es la MISMA cuenta de cada muestra, con las mismas operaciones en el mismo orden: cada banda solo depende de su
            // memoria y de la salida de la anterior para esa muestra, asi que el orden entre muestras y bandas no importa; y la limpieza cae en los mismos
            // cuadros (cada trozo se parte donde toca limpiar). Si algo cambia (una rampa, un cruce, otro EQ, un trozo que empieza a mitad de un cuadro) se
            // hace como siempre, cuadro a cuadro. La bateria 'eqporbandas' lo compara bit a bit con eso.
            internal static bool SinBandas = false;   // las pruebas: true = siempre cuadro a cuadro
            internal static long TrozosPorBandas;     // las pruebas: cuantos trozos se hicieron por bandas
            private bool PasadaQuieta(float g, float ex, PreparedEq prepared)
            {
                return !SinBandas && gainRamp.Remaining <= 0 && g == gainRamp.Target && exciteRamp.Remaining <= 0 && ex == exciteRamp.Target
                    && !(exciteRamp.Current == 0 && ex > 0) && eqFadeLeft == 0 && eqMix >= 1f && SameEq(currentEq, prepared);
            }
            private void PrimeraPasadaQuieta(float[] buffer, int s, int cuadros, int ch)
            {
                TrozosPorBandas++;
                float c = gainRamp.Current;
                float k = exciteRamp.Current * .5f * .4f;
                PreparedEq eq = currentEq;
                bool hayBandas = eq != null && eq.Active > 0;
                for (int hecho = 0; hecho < cuadros; )
                {
                    // el principio del cuadro (AdvanceAudioControls): solo cuenta, y cada 64 limpia los restos
                    if (++limpia == Desnormales.Cada)
                    {
                        limpia = 0;
                        if (currentEq != null) Desnormales.Limpia(currentEq.State);
                        if (previousEq != null) Desnormales.Limpia(previousEq.State);
                        Desnormales.Limpia(exLp);
                    }
                    // los cuadros que siguen sin llegar a 64: este y los siguientes hasta que la cuenta vuelva a tocar
                    int tanda = Math.Min(cuadros - hecho, Desnormales.Cada - limpia);
                    int ini = s + hecho * ch, fin = ini + tanda * ch;
                    if (c != 1f) for (int i = ini; i < fin; i++) buffer[i] = buffer[i] * c;   // x * 1 es x, exacto
                    if (hayBandas) BandasDelTrozo(buffer, ini, fin, ch, eq);
                    int ki = ini - s;
                    for (int i = ini; i < fin; i++, ki++) realceK[ki] = canalesMusicales[(i - s) % ch] ? k : 0f;
                    limpia += tanda - 1;
                    hecho += tanda;
                }
            }
            // El EQ en cascada de un trozo de cuadros enteros, de dos en dos bandas (la salida de una es la entrada de la siguiente, para esa misma muestra, en una
            // variable y no en el arreglo); cada banda con su memoria en variables, y los dos canales de un estereo a la vez. La banda b solo depende de su memoria y
            // de la salida de la b - 1: se puede hacer una banda (o dos) de punta a punta del trozo y luego las siguientes. Cada salida es un float (se redondea
            // igual que al guardarla en el arreglo) y cada cuenta, la de siempre: cf[0]*x + cf[1]*x1 + cf[2]*x2 - cf[3]*y1 - cf[4]*y2.
            private void BandasDelTrozo(float[] buffer, int ini, int fin, int ch, PreparedEq eq)
            {
                float[] st = eq.State;
                bool[] mus = canalesMusicales;
                bool estereo = ch == 2 && mus[0] && mus[1];
                int activas = eq.Active, b = 0;
                for (; b + 1 < activas; b += 2)
                {
                    float[] cf = eq.Coefficients[b], cg = eq.Coefficients[b + 1];
                    if (estereo) DosBandasEstereo(buffer, ini, fin, st, cf, cg, b * 4, 32 + b * 4);
                    else
                        for (int c = 0; c < ch; c++)
                            if (mus[c]) DosBandasUnCanal(buffer, ini + c, fin, ch, st, cf, cg, c * 32 + b * 4);   // el LFE no pasa por el EQ
                }
                if (b < activas)
                {
                    float[] cf = eq.Coefficients[b];
                    if (estereo) UnaBandaEstereo(buffer, ini, fin, st, cf, b * 4, 32 + b * 4);
                    else
                        for (int c = 0; c < ch; c++)
                            if (mus[c]) UnaBandaUnCanal(buffer, ini + c, fin, ch, st, cf, c * 32 + b * 4);
                }
            }
            private static void UnaBandaEstereo(float[] buffer, int ini, int fin, float[] st, float[] cf, int qa, int qb)
            {
                float k0 = cf[0], k1 = cf[1], k2 = cf[2], k3 = cf[3], k4 = cf[4];
                float a0 = st[qa], a1 = st[qa + 1], a2 = st[qa + 2], a3 = st[qa + 3];
                float b0 = st[qb], b1 = st[qb + 1], b2 = st[qb + 2], b3 = st[qb + 3];
                for (int i = ini; i < fin; i += 2)
                {
                    float x = buffer[i];
                    float o = k0 * x + k1 * a0 + k2 * a1 - k3 * a2 - k4 * a3;
                    a1 = a0; a0 = x; a3 = a2; a2 = o; buffer[i] = o;
                    float x2 = buffer[i + 1];
                    float o2 = k0 * x2 + k1 * b0 + k2 * b1 - k3 * b2 - k4 * b3;
                    b1 = b0; b0 = x2; b3 = b2; b2 = o2; buffer[i + 1] = o2;
                }
                st[qa] = a0; st[qa + 1] = a1; st[qa + 2] = a2; st[qa + 3] = a3;
                st[qb] = b0; st[qb + 1] = b1; st[qb + 2] = b2; st[qb + 3] = b3;
            }
            private static void DosBandasEstereo(float[] buffer, int ini, int fin, float[] st, float[] cf, float[] cg, int qa, int qb)
            {
                float k0 = cf[0], k1 = cf[1], k2 = cf[2], k3 = cf[3], k4 = cf[4];
                float m0 = cg[0], m1 = cg[1], m2 = cg[2], m3 = cg[3], m4 = cg[4];
                float a0 = st[qa], a1 = st[qa + 1], a2 = st[qa + 2], a3 = st[qa + 3];
                float b0 = st[qb], b1 = st[qb + 1], b2 = st[qb + 2], b3 = st[qb + 3];
                float c0 = st[qa + 4], c1 = st[qa + 5], c2 = st[qa + 6], c3 = st[qa + 7];
                float d0 = st[qb + 4], d1 = st[qb + 5], d2 = st[qb + 6], d3 = st[qb + 7];
                for (int i = ini; i < fin; i += 2)
                {
                    float x = buffer[i];
                    float o = k0 * x + k1 * a0 + k2 * a1 - k3 * a2 - k4 * a3;
                    a1 = a0; a0 = x; a3 = a2; a2 = o;
                    float p = m0 * o + m1 * c0 + m2 * c1 - m3 * c2 - m4 * c3;
                    c1 = c0; c0 = o; c3 = c2; c2 = p; buffer[i] = p;
                    float x2 = buffer[i + 1];
                    float o2 = k0 * x2 + k1 * b0 + k2 * b1 - k3 * b2 - k4 * b3;
                    b1 = b0; b0 = x2; b3 = b2; b2 = o2;
                    float p2 = m0 * o2 + m1 * d0 + m2 * d1 - m3 * d2 - m4 * d3;
                    d1 = d0; d0 = o2; d3 = d2; d2 = p2; buffer[i + 1] = p2;
                }
                st[qa] = a0; st[qa + 1] = a1; st[qa + 2] = a2; st[qa + 3] = a3;
                st[qb] = b0; st[qb + 1] = b1; st[qb + 2] = b2; st[qb + 3] = b3;
                st[qa + 4] = c0; st[qa + 5] = c1; st[qa + 6] = c2; st[qa + 7] = c3;
                st[qb + 4] = d0; st[qb + 5] = d1; st[qb + 6] = d2; st[qb + 7] = d3;
            }
            private static void UnaBandaUnCanal(float[] buffer, int ini, int fin, int paso, float[] st, float[] cf, int q)
            {
                float k0 = cf[0], k1 = cf[1], k2 = cf[2], k3 = cf[3], k4 = cf[4];
                float a0 = st[q], a1 = st[q + 1], a2 = st[q + 2], a3 = st[q + 3];
                for (int i = ini; i < fin; i += paso)
                {
                    float x = buffer[i];
                    float o = k0 * x + k1 * a0 + k2 * a1 - k3 * a2 - k4 * a3;
                    a1 = a0; a0 = x; a3 = a2; a2 = o; buffer[i] = o;
                }
                st[q] = a0; st[q + 1] = a1; st[q + 2] = a2; st[q + 3] = a3;
            }
            private static void DosBandasUnCanal(float[] buffer, int ini, int fin, int paso, float[] st, float[] cf, float[] cg, int q)
            {
                float k0 = cf[0], k1 = cf[1], k2 = cf[2], k3 = cf[3], k4 = cf[4];
                float m0 = cg[0], m1 = cg[1], m2 = cg[2], m3 = cg[3], m4 = cg[4];
                float a0 = st[q], a1 = st[q + 1], a2 = st[q + 2], a3 = st[q + 3];
                float c0 = st[q + 4], c1 = st[q + 5], c2 = st[q + 6], c3 = st[q + 7];
                for (int i = ini; i < fin; i += paso)
                {
                    float x = buffer[i];
                    float o = k0 * x + k1 * a0 + k2 * a1 - k3 * a2 - k4 * a3;
                    a1 = a0; a0 = x; a3 = a2; a2 = o;
                    float p = m0 * o + m1 * c0 + m2 * c1 - m3 * c2 - m4 * c3;
                    c1 = c0; c0 = o; c3 = c2; c2 = p; buffer[i] = p;
                }
                st[q] = a0; st[q + 1] = a1; st[q + 2] = a2; st[q + 3] = a3;
                st[q + 4] = c0; st[q + 5] = c1; st[q + 6] = c2; st[q + 7] = c3;
            }

            // La fuente se acabo: lo que quedo retenido sale entero (silencio de entrada, la misma cuenta).
            private int VaciaRealce(float[] buffer, int offset, int count)
            {
                int m = Math.Min(count, porVaciar);
                if (m <= 0) return 0;
                // A MANO, NO CON Array.Clear: lo que llega como float[] puede ser la vista que NAudio pone sobre su
                // byte[] (SampleToWaveProvider, la salida en coma flotante). Array.Clear mira el tipo de verdad y
                // borraria BYTES en otro sitio: el final de la cancion salia con restos de la vuelta anterior.
                for (int i = 0; i < m; i++) buffer[offset + i] = 0f;
                float g, ex;
                Objetivos(out g, out ex);
                ProcesaConRealce(buffer, offset, m, g, ex, pendingEq);
                porVaciar -= m;
                return m;
            }
        }
    }
}

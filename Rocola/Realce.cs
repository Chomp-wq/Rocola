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

        internal RealceSobremuestreado(int canales, float polo)
        {
            canales = Math.Max(1, canales);
            this.polo = polo;
            lp = new float[canales]; listo = new bool[canales];
            xh = new double[canales][]; xv = new double[canales][]; xg = new double[canales][]; xn = new double[canales][];
            for (int c = 0; c < canales; c++)
            {
                xh[c] = new double[2 * Retraso + Trozo]; xv[c] = new double[2 * Retraso + Trozo];
                xg[c] = new double[2 * Retraso + Trozo]; xn[c] = new double[2 * Retraso + Trozo];
            }
            double[] k = C;
            c0 = k[0]; c1 = k[1]; c2 = k[2]; c3 = k[3]; c4 = k[4]; c5 = k[5]; c6 = k[6]; c7 = k[7];
            c8 = k[8]; c9 = k[9]; c10 = k[10]; c11 = k[11]; c12 = k[12]; c13 = k[13]; c14 = k[14]; c15 = k[15];
            d0 = .5 * k[0]; d1 = .5 * k[1]; d2 = .5 * k[2]; d3 = .5 * k[3]; d4 = .5 * k[4]; d5 = .5 * k[5]; d6 = .5 * k[6]; d7 = .5 * k[7];
            d8 = .5 * k[8]; d9 = .5 * k[9]; d10 = .5 * k[10]; d11 = .5 * k[11]; d12 = .5 * k[12]; d13 = .5 * k[13]; d14 = .5 * k[14]; d15 = .5 * k[15];
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

            private void PreparaRealce()
            {
                realce = new RealceSobremuestreado(audioChannels, exciterPole);
                realceK = new float[RealceSobremuestreado.Trozo * audioChannels];
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

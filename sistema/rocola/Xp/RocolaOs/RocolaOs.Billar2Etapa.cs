// ROCOLA OS (la cadena nueva del billar, B; CADENA_NUEVA.md §3, CADENA_NUEVA_B.md): EL TRAMO B, una sola etapa (ISampleProvider de NAudio 3)
// con las piezas del billar en su orden: MONO COMPLETO -> PASO ALTO (y el corte de noche) -> LA CURVA DEL BILLAR -> [BAJO VIRTUAL]. Lo que entra
// es lo que dejan los arreglos fijos de la medida (el canal mudo copiado, la continua, el zumbido); lo que sale va al ecualizador del dueno
// (memoria del billar) y a la ganancia de la cancion (las dos del marco, RocolaOs.CadenaNueva*.cs). Nada aqui sigue a la senal: la etapa entera
// es LINEAL E INVARIANTE salvo el bajo virtual, que es homogeneo de grado 1 (RocolaOs.Billar2Bajo.cs): con la musica a·x sale a·salida.
//
// EL MONO: m = (L + s·R) / 2 a los dos canales, con s = -1 si la medida dijo que un canal viene al reves (voltear: CADENA_NUEVA_B.md §1, por
// que la ley de -6 dB). Desde ahi todo es UN canal: la mitad de trabajo, y lo que sale es el mismo en L y R (bit a bit).
// EL PASO ALTO Y LA CURVA: un solo FIR de fase lineal (RocolaOsBillar2Diseno), por FFT de N con solapar y guardar: la ventana tiene los cuadros
// [p - K, p + B + K) y salen los B de en medio, [p, p + B). LEE POR DELANTE: lo que sale va en su sitio (sin retardo, como el grave en mono de
// hoy); la primera lectura pide B + K cuadros (unos 136 ms a 80 Hz y 44,1 kHz).
// LA NOCHE: si el corte de noche es mayor que el paso alto, el diseno trae el filtro de noche (el mismo con el paso alto en el corte de noche) y
// se sale (1 - m)·dia + m·noche, con m = la mezcla que pone el modo noche (GraveMono.NocheMezcla: 0 de dia, 1 con la noche entera; su bajada de
// una hora), leida una vez por bloque y llevada en linea recta de un bloque al otro (sin escalones). Con m en 0 o 1 una sola FFT inversa.
// EL BAJO VIRTUAL (si va): la fuente (lo que el paso alto quita) sale de la misma FFT del bloque con otra inversa; |fuente| por sus filtros y por
// la mezcla se suma a lo de arriba.
//
// DISCO CONTINUO (Sigue): la pista siguiente del mismo disco, sin fundido, empieza con los ultimos K cuadros de esta en la ventana (el filtro sigue
// donde iba) y con el estado de los filtros del bajo virtual. Un filtro de fase lineal necesita, para sus ultimos K cuadros, los primeros K de la
// que sigue (la mitad de delante del filtro): sin ellos, la cola de esta sale como si despues viniera silencio, y el paso alto deja pasar la mitad
// del grave que quita en esos 50 ms (medido: hasta 5 dB sobre el eficaz de lo que sale, con musica de mucho grave) y la curva le quita a la
// siguiente sus agudos del primer milisegundo (-14 dB: un chasquido).
// EL MODO EXACTO (el de siempre si se llama a Sigue antes de que esta acabe; quien prepara el relevo lo hace al armar la siguiente): al acabar su
// origen, esta lee del origen de la siguiente sus primeros K cuadros (en el hilo del sonido, el mismo que despues lee la siguiente), los usa para
// sus ultimos cuadros y se los PASA a la siguiente, que empieza por ellos sin volverlos a leer. Lo que sale es igual, cuadro a cuadro, que con las
// dos pistas pegadas en una sola (la prueba: 1e-7). No se hace si la siguiente lleva cebado, ya empezo, o es de otro diseno; ExactoAlFinal = false
// lo apaga (por si algo mas leyera la siguiente antes de que suene).
// SI NO (Sigue tarde, o el modo exacto no se pudo): EL EMPALME. Lo que le falto a la ultima de esta en el cuadro -1 es e = suma de h[1 + j]·x[j],
// j = 0..K-1, con x esta pista; los primeros 25 ms de esta salen con -e·(1 - t/25 ms): el salto del cuadro -1 al 0 es el de la musica, sin
// escalon (lo mismo con la fuente del bajo virtual); la cola de la anterior ya salio como salio. Con fundido, o sin la de antes, se empieza de
// cero (la ventana con silencio delante).
// EL CEBADO (Cebado, lo pone quien arma): cuadros que traen al empezar las etapas de delante que el relevo tira; tras heredar pasan tal cual.
using System;
using System.Threading;

namespace Rocola
{
    internal sealed class RocolaOsBillar2Tramo : IRocolaOsPieza   // (el contrato de las piezas: RocolaOs.CadenaNuevaPieza.cs, CADENA_NUEVA.md §8.1)
    {
        private readonly NAudio.Wave.ISampleProvider src;
        private readonly int canales, tasa;
        private readonly bool pasa;                  // lo que no se puede (mas de dos canales, menos de 8 kHz): tal cual
        internal readonly RocolaOsBillar2Diseno D;
        private readonly bool voltear;
        private readonly Func<double> mezclaNoche;   // null: siempre de dia (la medida de la cancion)
        private readonly RocolaOsBillar2Fft fft;
        private readonly int K, N, B;
        private readonly double[] win, re, im, pr, pi, yd, yn, yb;
        private readonly float[] lee, cola;
        private int colaI, colaN;
        private long leidos, emitidos;
        private bool primero = true, acabo, finHecho, empezo;
        private readonly double[] fin;               // los ultimos K cuadros (en mono) al acabar: para la siguiente del mismo disco
        private volatile RocolaOsBillar2Tramo anterior, siguiente;
        private bool union; private int unionN; private double eD, eB;
        // EL MODO EXACTO: en la de antes, lo que leyo del origen de la siguiente (intercalado, tal cual) y en mono (para su ventana); en la siguiente,
        // eso mismo pendiente de entregar como lo primero de su origen
        internal bool ExactoAlFinal = true;
        // (27-11-2026) la preparada que XP retira sin empezar (SiguienteConElInterruptor, «saltar los silencios» en los ultimos 12 s) suelta su lector
        // en el hilo de la pantalla: la anterior no debe estar leyendo su origen en ese momento. Retira y la lectura del adelanto van bajo este candado
        // (el de la siguiente): o el adelanto ya se leyo entero, o ya no se lee. Lo llama RocolaOsCadenaNuevaNoSigue (gancho g463) antes del Dispose.
        private readonly object candadoOrigen = new object();
        internal void Retira() { lock (candadoOrigen) ExactoAlFinal = false; }
        private float[] adelanto; private int adelantoN; private bool adelantoAcabo, adelantoVisto;
        private double[] adelantoMono; private int adelantoMonoN;
        private volatile RocolaOsBillar2Tramo adelantoPara;
        private float[] pendiente; private int pendI, pendN; private bool pendAcabo;
        internal bool Exacto { get; private set; }
        private double mAntes = -1.0, mFin;          // la mezcla de noche del bloque anterior (-1: aun no) y la del ultimo cuadro
        private readonly RocolaOsBillar2Bajo.Estado bajo;
        internal int Cebado;                         // (lo pone quien arma, antes de la primera lectura)
        private int cebadoPorPasar;
        internal long Bloques;                       // (para la vista y las pruebas)
        internal bool Heredo { get; private set; }
        internal double EmpalmeDia { get { return eD; } }
        internal double EmpalmeBajo { get { return eB; } }
        internal int LeePorDelante { get { return pasa ? 0 : K; } }

        internal RocolaOsBillar2Tramo(NAudio.Wave.ISampleProvider s, RocolaOsBillar2Diseno d, bool voltearDerecho, Func<double> mezcla)
        {
            src = s;
            canales = s.WaveFormat.Channels; tasa = s.WaveFormat.SampleRate;
            pasa = d == null || d.Tasa != tasa || (canales != 1 && canales != 2) || tasa < 8000;
            D = d; voltear = voltearDerecho && canales == 2; mezclaNoche = d != null && d.Noche != null ? mezcla : null;
            if (pasa) return;
            K = d.K; N = d.N; B = d.B;
            fft = new RocolaOsBillar2Fft(N);
            win = new double[N]; re = new double[N / 2 + 1]; im = new double[N / 2 + 1]; pr = new double[N / 2 + 1]; pi = new double[N / 2 + 1];
            yd = new double[N];
            if (d.Noche != null) yn = new double[N];
            if (d.Fuente != null) { yb = new double[N]; bajo = new RocolaOsBillar2Bajo.Estado(); }
            lee = new float[(B + K) * canales];
            cola = new float[B * canales];
            fin = new double[K];
        }

        public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }

        // LA SIGUIENTE PISTA DEL MISMO DISCO sigue con esta (lo llama quien prepara el relevo, en cualquier hilo; se toma en la primera lectura)
        public void Sigue(IRocolaOsPieza anterior) { Sigue(anterior as RocolaOsBillar2Tramo); }   // (el marco: la misma pieza de la cadena que acaba)
        internal void Sigue(RocolaOsBillar2Tramo previa)
        {
            if (previa == null || object.ReferenceEquals(previa, this)) return;
            anterior = previa;
            previa.siguiente = this;   // (para el modo exacto: si aun no acaba, al acabar lee los primeros K cuadros de esta)
        }
        internal bool Acabo { get { return acabo && emitidos >= leidos; } }

        private void Hereda()
        {
            RocolaOsBillar2Tramo a = anterior; anterior = null;
            if (a == null || a.pasa || pasa || !a.finHecho || !object.ReferenceEquals(a.D, D) || a.canales != canales) return;
            Array.Copy(a.fin, 0, win, 0, K);   // lo de antes del primer cuadro: el final de la anterior
            if (object.ReferenceEquals(a.adelantoPara, this) && a.adelanto != null)
            {   // EL MODO EXACTO: la anterior ya leyo nuestros primeros cuadros (y salio con ellos): empezamos por ellos, sin empalme
                pendiente = a.adelanto; pendI = 0; pendN = a.adelantoN; pendAcabo = a.adelantoAcabo;
                Exacto = true;
            }
            else union = true;
            if (bajo != null && a.bajo != null) bajo.CopiaDe(a.bajo);
            mAntes = a.mFin >= 0 ? a.mFin : -1.0;
            cebadoPorPasar = Math.Max(0, Cebado) * canales;
            Heredo = true;
        }

        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
        public int Read(float[] buffer, int offset, int count)
        {
            if (count <= 0) return 0;   // (no tomar el relevo hasta la primera lectura de audio)
            if (pasa) return src.Read(buffer, offset, count);
            if (!empezo) { empezo = true; Hereda(); }
            int hechos = 0;
            if (cebadoPorPasar > 0)
            {   // tras la anterior del mismo disco, el cebado pasa tal cual: el filtro sigue justo donde acabo su musica
                int n = src.Read(buffer, offset, Math.Min(count, cebadoPorPasar));
                if (n <= 0) cebadoPorPasar = 0;
                else { cebadoPorPasar -= n; hechos = n; if (cebadoPorPasar > 0) return hechos; }
            }
            while (hechos < count)
            {
                if (colaN == 0 && !Bloque()) break;
                int k = Math.Min(colaN, count - hechos), d = offset + hechos, q = colaI;
                float[] c = cola;
                for (int i = 0; i < k; i++) buffer[d + i] = c[q + i];   // (a mano, como el grave en mono: 'buffer' puede ser una vista)
                colaI += k; colaN -= k; hechos += k;
            }
            return hechos;
        }

        // lee del origen hasta 'cuadros' cuadros enteros (si llega uno partido, se sigue leyendo; al final, medio cuadro se descarta)
        private int LeeCuadros(int cuadros)
        {
            int quiero = cuadros * canales, tiene = 0;
            if (pendN > 0)
            {   // (modo exacto) lo que la anterior ya leyo de nuestro origen va primero
                int k = Math.Min(pendN, quiero);
                Array.Copy(pendiente, pendI, lee, 0, k);
                pendI += k; pendN -= k; tiene = k;
                if (pendN == 0) { pendiente = null; if (pendAcabo) acabo = true; }
            }
            while (tiene < quiero && !acabo)
            {
                int r = src.Read(lee, tiene, quiero - tiene);
                if (r <= 0) { acabo = true; break; }
                tiene += r;
            }
            return tiene / canales;
        }

        // UN BLOQUE: se lee lo que falta de la ventana (el primero, B + K: lee por delante), en mono; la FFT; por el filtro de dia (y el de noche,
        // y la fuente del bajo virtual) y de vuelta; salen los B cuadros de en medio; la ventana avanza B.
        private bool Bloque()
        {
            if (acabo && emitidos >= leidos) return false;
            int desde = primero ? K : 2 * K, quiere = primero ? B + K : B;
            int got = LeeCuadros(quiere);
            double[] w = win;
            float[] l = lee;
            if (canales == 2)
            {
                double s = voltear ? -0.5 : 0.5;
                for (int f = 0, j = 0; f < got; f++, j += 2)
                {
                    double v = 0.5 * l[j] + s * l[j + 1];
                    w[desde + f] = double.IsFinite(v) ? v : 0.0;
                }
            }
            else for (int f = 0; f < got; f++) { double v = l[f]; w[desde + f] = double.IsFinite(v) ? v : 0.0; }
            for (int f = desde + got; f < N; f++) w[f] = 0.0;
            bool fuePrimero = primero;
            primero = false;
            long p = emitidos;
            leidos += got;
            if (acabo && !adelantoVisto) { adelantoVisto = true; LeeAdelanto(); }
            if (adelantoMonoN > 0)
            {   // (modo exacto) despues del ultimo cuadro de esta, los primeros de la siguiente: el cuadro n esta en n - p + K
                long b0 = leidos - p + K;
                for (int j = 0; j < adelantoMonoN; j++) { long i = b0 + j; if (i >= N) break; if (i >= 0) w[i] = adelantoMono[j]; }
            }
            if (acabo && !finHecho)
            {   // los ultimos K cuadros de la cancion (en mono), por si la siguiente del mismo disco sigue sin fundido; el cuadro n esta en n - p + K
                finHecho = true;
                for (int k = 0; k < K; k++) { long n = leidos - K + k; long i = n - p + K; fin[k] = i >= 0 && i < N ? w[i] : 0.0; }
            }
            int salen = (int)Math.Min((long)B, leidos - p);
            if (salen <= 0) return false;

            fft.Adelante(w, 0, re, im);
            // la mezcla de noche de este bloque (de la del anterior a la de ahora, en linea recta)
            double m1 = 0.0, m0 = 0.0;
            if (mezclaNoche != null)
            {
                double m = 0.0;
                try { m = mezclaNoche(); } catch { }
                m1 = double.IsNaN(m) ? 0.0 : Math.Max(0.0, Math.Min(1.0, m));
                m0 = mAntes < 0 ? m1 : mAntes;
            }
            bool conDia = m0 < 1.0 || m1 < 1.0, conNoche = yn != null && (m0 > 0.0 || m1 > 0.0);
            if (conDia) Producto(D.Dia, yd);
            if (conNoche) Producto(D.Noche, yn);
            if (yb != null) Producto(D.Fuente, yb);

            // EL EMPALME con la anterior del mismo disco: lo que le falto en su ultimo cuadro (la mitad de delante del filtro con esta pista)
            if (union && fuePrimero)
            {
                union = false; unionN = Math.Max(1, (int)Math.Round(0.025 * tasa));
                double sd = 0.0, sn = 0.0, sb = 0.0;
                double[] td = D.TapsDia, tn = D.TapsNoche, tb = D.TapsFuente;
                for (int j = 0; j + 1 <= K; j++)
                {
                    double x = w[K + j];
                    sd += td[j + 1] * x;
                    if (tn != null) sn += tn[j + 1] * x;
                    if (tb != null) sb += tb[j + 1] * x;
                }
                eD = tn != null ? sd + m0 * (sn - sd) : sd;
                eB = sb;
            }
            else if (!fuePrimero) unionN = 0;

            float[] c = cola;
            double kb = D.MezclaBajo;
            double paso = salen > 1 ? (m1 - m0) / salen : 0.0;
            int un = unionN;
            double iu = un > 0 ? 1.0 / un : 0.0;
            for (int t = 0, q = 0; t < salen; t++)
            {
                int i = K + t;
                double y;
                if (!conNoche) y = yd[i];
                else if (!conDia) y = yn[i];
                else { double m = m0 + paso * t; y = yd[i] + m * (yn[i] - yd[i]); }
                if (t < un) y -= (1.0 - t * iu) * eD;
                if (yb != null)
                {
                    double f = yb[i];
                    if (t < un) f -= (1.0 - t * iu) * eB;
                    y += kb * bajo.Uno(D.Bajo, f);
                }
                float v = (float)y;
                if (canales == 2) { c[q] = v; c[q + 1] = v; q += 2; } else c[q++] = v;
            }
            if (bajo != null) bajo.Limpia();
            mAntes = m1; mFin = m1;
            // la ventana avanza un bloque: sus ultimos 2K cuadros pasan al principio
            Array.Copy(w, B, w, 0, 2 * K);
            emitidos += salen; colaI = 0; colaN = salen * canales;
            Bloques++;
            return true;
        }

        // EL MODO EXACTO, al acabar el origen de esta: los primeros K cuadros de la siguiente (si ya se sabe cual es y se puede)
        private void LeeAdelanto()
        {
            RocolaOsBillar2Tramo s = siguiente;
            if (s == null || !ExactoAlFinal || !s.ExactoAlFinal || s.pasa || s.empezo || s.Cebado > 0 || !object.ReferenceEquals(s.D, D) || s.canales != canales) return;
            lock (s.candadoOrigen) try
            {
                if (!s.ExactoAlFinal) return;   // (la retiraron mientras tanto: su lector se suelta o ya se solto)
                float[] b = new float[K * canales];
                int tiene = 0; bool fin = false;
                while (tiene < b.Length) { int r = s.src.Read(b, tiene, b.Length - tiene); if (r <= 0) { fin = true; break; } tiene += r; }
                double[] m = new double[K];
                int cuadros = tiene / canales;
                if (canales == 2) { double sg = s.voltear ? -0.5 : 0.5; for (int f = 0; f < cuadros; f++) { double v = 0.5 * b[2 * f] + sg * b[2 * f + 1]; m[f] = double.IsFinite(v) ? v : 0.0; } }
                else for (int f = 0; f < cuadros; f++) { double v = b[f]; m[f] = double.IsFinite(v) ? v : 0.0; }
                adelanto = b; adelantoN = tiene; adelantoAcabo = fin; adelantoMono = m; adelantoMonoN = cuadros;
                adelantoPara = s;
            }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsBillar2.Nombre, "leer el principio de la pista siguiente", ex); }
        }

        // el espectro del bloque por un filtro (real) y de vuelta al tiempo
        private void Producto(double[] h, double[] y)
        {
            int n = N / 2;
            for (int k = 0; k <= n; k++) { double g = h[k]; pr[k] = re[k] * g; pi[k] = im[k] * g; }
            fft.Atras(pr, pi, y, 0);
        }
    }
}

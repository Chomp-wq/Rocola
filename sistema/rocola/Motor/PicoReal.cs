// PICOS REALES ENTRE MUESTRAS (true-peak)
//
// El limitador de siempre (LookaheadLimiter) mira las MUESTRAS: garantiza que ninguna pase de 0,98. Pero lo
// que sale por el altavoz no son muestras sueltas: el convertidor del equipo (el DAC) reconstruye la onda
// continua que pasa por ellas, y esa onda puede subir ENTRE dos muestras bastante mas que cualquiera de las
// dos. El ejemplo de libro: un tono a un cuarto de la frecuencia de muestreo, desfasado 45 grados, tiene
// todas sus muestras en 0,707 y la onda real llega a 1,0 (+3 dB). El limitador de muestras no lo ve; el
// DAC si, y si no le queda margen, recorta. En musica muy comprimida (la de la "guerra del volumen") esos
// picos entre muestras son el pan de cada dia, y el realce, el DSEE y el grave sintetico los alimentan.
//
// Aqui se mide la onda REAL: se reconstruye cuatro veces mas fina que las muestras (un filtro de
// interpolacion de 97 coeficientes, 24 por fase, ventana Kaiser beta 6) y, donde hay un maximo, se afina
// con una parabola por los tres puntos de la rejilla. Medido contra una reconstruccion de referencia 64
// veces mas fina (pruebas/ref_picoreal.py): en senales limitadas a 0,4535 veces la frecuencia de muestreo
// (20 kHz a 44,1 kHz) este detector se queda corto como mucho 0,10 dB; con 16 coeficientes por fase eran
// 0,32 dB y con 12, 0,82 dB. Por encima de esa banda (lo que queda hasta Nyquist) se queda mas corto: ahi
// la musica apenas tiene energia.
//
// El filtro es simetrico: la fase de media muestra es simetrica y las de un cuarto y tres cuartos son una el
// espejo de la otra. Asi, las tres fases salen con 36 multiplicaciones por muestra y canal en vez de 72. Y
// si ninguna de las 24 muestras que entran en la cuenta puede producir un pico por encima del techo (todas
// por debajo del techo dividido entre la ganancia maxima del filtro, 2,30), ni se calcula: basta con saber
// que no pasa.
//
// Solo se DETECTA sobremuestreado; la senal no se sobremuestrea ni se toca aqui. El limitador de abajo aplica
// una ganancia normal, a la frecuencia de siempre, igual para todos los canales.
using System;

namespace Rocola
{
    internal sealed class DetectorPicoReal
    {
        internal const int Factor = 4;       // rejilla cuatro veces mas fina
        internal const int Taps = 24;        // coeficientes por fase
        internal const double Beta = 6.0;    // ventana de Kaiser
        // La rejilla del intervalo que se calcula ahora empieza en la muestra que entro hace Retraso muestras.
        internal const int Retraso = Taps / 2;
        // Coeficientes (a0 = 1, fase 0 = la propia muestra). P y M: semisuma y semidiferencia de la fase de un
        // cuarto con su espejo; Q: la fase de media muestra doblada (par simetrico).
        private static readonly double[] P, M, Q;
        internal static readonly double[][] Fases;   // las cuatro fases, para las pruebas
        internal static readonly float GananciaMaxima; // la mayor suma de |coeficientes| de una fase (2,30)

        static DetectorPicoReal()
        {
            int N = Factor * Taps + 1, c = Factor * Taps / 2;
            double[] h = new double[N];
            double i0b = I0(Beta);
            for (int n = 0; n < N; n++)
            {
                double t = (n - c) / (double)Factor;
                double s = (n == c) ? 1.0 : Math.Sin(Math.PI * t) / (Math.PI * t);
                double r = 2.0 * n / (N - 1) - 1.0;
                h[n] = s * I0(Beta * Math.Sqrt(Math.Max(0.0, 1.0 - r * r))) / i0b;
            }
            Fases = new double[Factor][];
            double peor = 0;
            for (int q = 0; q < Factor; q++)
            {
                int largo = (q == 0) ? Taps + 1 : Taps;
                double[] f = new double[largo];
                double suma = 0, abs = 0;
                for (int i = 0; i < largo; i++) { f[i] = h[q + Factor * i]; suma += f[i]; }
                for (int i = 0; i < largo; i++) { f[i] /= suma; abs += Math.Abs(f[i]); }   // ganancia 1 en continua, en cada fase
                Fases[q] = f;
                if (abs > peor) peor = abs;
            }
            GananciaMaxima = (float)peor;
            int mitad = Taps / 2;
            P = new double[mitad]; M = new double[mitad]; Q = new double[mitad];
            for (int i = 0; i < mitad; i++)
            {
                P[i] = 0.5 * (Fases[1][i] + Fases[1][Taps - 1 - i]);
                M[i] = 0.5 * (Fases[1][i] - Fases[1][Taps - 1 - i]);
                Q[i] = Fases[2][i];
            }
        }
        private static double I0(double x)
        {
            double s = 1, t = 1;
            for (int k = 1; k < 64; k++) { t *= (x / (2 * k)) * (x / (2 * k)); s += t; if (t < 1e-17 * s) break; }
            return s;
        }

        // Historia de las ultimas Taps muestras, escrita dos veces para leerla siempre seguida.
        private readonly double[] hist = new double[2 * Taps];
        private int pos;
        private readonly float umbral;       // por debajo, ninguna fase puede pasar del techo
        private int desdeAlta = int.MaxValue / 2;   // muestras desde la ultima que supero el umbral
        private float previo;                // el ultimo punto de la rejilla del intervalo anterior (|y3|)

        internal DetectorPicoReal(float techo)
        {
            umbral = techo / GananciaMaxima;
        }
        // Disco continuo (el limitador de la pista siguiente sigue con el de la anterior): toda la memoria de otro.
        internal void CopiaDe(DetectorPicoReal a)
        {
            Array.Copy(a.hist, hist, hist.Length); pos = a.pos; desdeAlta = a.desdeAlta; previo = a.previo;
        }

        // LO MISMO QUE Empuja, PARA UN BLOQUE ENTERO de un canal (muestras separadas por 'salto'), con el estado
        // en variables locales: sin una llamada por muestra. La cuenta es identica (la bateria lo compara bit a
        // bit). 'picos[f]' se queda con el mayor entre lo que ya tenia y este canal, salvo en el primero.
        internal void Bloque(float[] datos, int inicio, int salto, int n, float[] picos, bool primero)
        {
            double[] h = hist;
            // Los 36 coeficientes en variables: el filtro va desenrollado (ver abajo).
            double p0 = P[0], p1 = P[1], p2 = P[2], p3 = P[3], p4 = P[4], p5 = P[5], p6 = P[6], p7 = P[7], p8 = P[8], p9 = P[9], p10 = P[10], p11 = P[11];
            double m0 = M[0], m1 = M[1], m2 = M[2], m3 = M[3], m4 = M[4], m5 = M[5], m6 = M[6], m7 = M[7], m8 = M[8], m9 = M[9], m10 = M[10], m11 = M[11];
            double q0 = Q[0], q1 = Q[1], q2 = Q[2], q3 = Q[3], q4 = Q[4], q5 = Q[5], q6 = Q[6], q7 = Q[7], q8 = Q[8], q9 = Q[9], q10 = Q[10], q11 = Q[11];
            int p = pos, alta = desdeAlta; float prev = previo, um = umbral;
            const int T = Taps, R = Retraso;
            for (int f = 0, k = inicio; f < n; f++, k += salto)
            {
                float x = datos == null ? 0f : datos[k];
                p--; if (p < 0) p = T - 1;
                h[p] = x; h[p + T] = x;
                float ax = x < 0f ? -x : x;
                if (ax > um) alta = 0; else if (alta < int.MaxValue / 2) alta++;
                float a0 = (float)Math.Abs(h[p + R]), a4 = (float)Math.Abs(h[p + R - 1]);
                float pico;
                if (alta >= T)
                {
                    prev = a0;
                    pico = a0 > a4 ? a0 : a4;
                }
                else
                {
                    // Las doce parejas del filtro, DESENROLLADAS a mano (T = 24): las mismas sumas en el mismo orden que el bucle
                    // de Empuja (i = 0..11, u, v y w acumulando de una en una) y la misma suma a + b para u y w. Con un bucle, el
                    // JIT comprueba los limites del arreglo y lee el coeficiente del arreglo en cada vuelta.
                    double u = 0, v = 0, w = 0, a, b, s;
                    a = h[p]; b = h[p + 23]; s = a + b; u += p0 * s; v += m0 * (a - b); w += q0 * s;
                    a = h[p + 1]; b = h[p + 22]; s = a + b; u += p1 * s; v += m1 * (a - b); w += q1 * s;
                    a = h[p + 2]; b = h[p + 21]; s = a + b; u += p2 * s; v += m2 * (a - b); w += q2 * s;
                    a = h[p + 3]; b = h[p + 20]; s = a + b; u += p3 * s; v += m3 * (a - b); w += q3 * s;
                    a = h[p + 4]; b = h[p + 19]; s = a + b; u += p4 * s; v += m4 * (a - b); w += q4 * s;
                    a = h[p + 5]; b = h[p + 18]; s = a + b; u += p5 * s; v += m5 * (a - b); w += q5 * s;
                    a = h[p + 6]; b = h[p + 17]; s = a + b; u += p6 * s; v += m6 * (a - b); w += q6 * s;
                    a = h[p + 7]; b = h[p + 16]; s = a + b; u += p7 * s; v += m7 * (a - b); w += q7 * s;
                    a = h[p + 8]; b = h[p + 15]; s = a + b; u += p8 * s; v += m8 * (a - b); w += q8 * s;
                    a = h[p + 9]; b = h[p + 14]; s = a + b; u += p9 * s; v += m9 * (a - b); w += q9 * s;
                    a = h[p + 10]; b = h[p + 13]; s = a + b; u += p10 * s; v += m10 * (a - b); w += q10 * s;
                    a = h[p + 11]; b = h[p + 12]; s = a + b; u += p11 * s; v += m11 * (a - b); w += q11 * s;
                    float g1 = (float)Math.Abs(u + v), g2 = (float)Math.Abs(w), g3 = (float)Math.Abs(u - v);
                    pico = a0;
                    if (g1 > pico) pico = g1; if (g2 > pico) pico = g2; if (g3 > pico) pico = g3; if (a4 > pico) pico = a4;
                    // Afina (abajo) cuatro veces, ESCRITA EN SU SITIO: la misma cuenta con las mismas operaciones; son cuatro llamadas por muestra que el JIT
                    // no expande. Afina(y0, y1, y2, pico): si y1 es un maximo de los tres puntos y la parabola abre hacia abajo, el vertice cuenta.
                    if (!(a0 < prev || a0 < g1))
                    {
                        float den = prev - 2f * a0 + g1;
                        if (den < 0f)
                        {
                            float d = 0.5f * (prev - g1) / den;
                            float vv = a0 - 0.25f * (prev - g1) * d;
                            if (vv > pico) pico = vv;
                        }
                    }
                    if (!(g1 < a0 || g1 < g2))
                    {
                        float den = a0 - 2f * g1 + g2;
                        if (den < 0f)
                        {
                            float d = 0.5f * (a0 - g2) / den;
                            float vv = g1 - 0.25f * (a0 - g2) * d;
                            if (vv > pico) pico = vv;
                        }
                    }
                    if (!(g2 < g1 || g2 < g3))
                    {
                        float den = g1 - 2f * g2 + g3;
                        if (den < 0f)
                        {
                            float d = 0.5f * (g1 - g3) / den;
                            float vv = g2 - 0.25f * (g1 - g3) * d;
                            if (vv > pico) pico = vv;
                        }
                    }
                    if (!(g3 < g2 || g3 < a4))
                    {
                        float den = g2 - 2f * g3 + a4;
                        if (den < 0f)
                        {
                            float d = 0.5f * (g2 - a4) / den;
                            float vv = g3 - 0.25f * (g2 - a4) * d;
                            if (vv > pico) pico = vv;
                        }
                    }
                    prev = g3;
                }
                if (primero || pico > picos[f]) picos[f] = pico;
            }
            pos = p; desdeAlta = alta; previo = prev;
        }
    }

    public partial class Form1
    {
        // Techo operativo: ~0,94 dB adicionales frente al antiguo 0,98.
        // Cubre los excesos medidos del detector 4x sin triplicar su FIR.
        // No es una atenuacion fija: lo que no necesita limitar sigue intacto.
        // Referencia independiente y limites: pruebas/picoreal.cs.
        internal const float TechoPicoReal = 0.88f;

        // LIMITADOR DE PICO REAL. El mismo limitador con pre-escucha de siempre (misma ganancia para todos los
        // canales, ataque que llega antes que el pico), pero el pico que manda es el de la onda
        // reconstruida, no el de las muestras.
        //
        // ESPERA ANTES DE SOLTAR (28/09). Antes soltaba en 120 ms en cuanto el pico salia de la pre-escucha (3 ms). Con un
        // bajo por encima del techo eso es media vuelta de cada ciclo: la ganancia subia y bajaba con la onda y el bajo
        // salia distorsionado. Ahora, tras bajar, espera 20 ms quieta (un ciclo de 50 Hz) y luego suelta en 100 ms.
        // Medido (pruebas/soltar.cs): un bajo sostenido 6 dB por encima del techo sale con -76 dB de distorsion a
        // 40 Hz, -76 a 60 y -74 a 100 (antes -44, -55 y -72); tras un golpe suelto vuelve en 220 ms (antes 250); en
        // canciones de prueba ya niveladas limita casi lo mismo que antes. Se probo tambien soltar "segun la musica"
        // (rapido tras un golpe, lento si limita mucho rato): no ganaba en todo y no se puso. Anade Retraso muestras de espera (0,27 ms a 44,1 kHz) para que el
        // detector vea el pico entero antes de decidir, y al terminar la cancion vacia tambien eso.
        //
        // Lo que no pasa del techo sale tal cual (solo retrasado): la ganancia se queda en 1 exacto.
        //
        // DISCO CONTINUO (24-10-2026). La pista siguiente del mismo disco empezaba con el limitador de cero (sin bajar nada): si la
        // anterior acababa limitando, en el cambio la ganancia saltaba de golpe (medido en la cadena entera, con la musica a tope:
        // un escalon de 0,03 a 0,14). Ahora la anterior guarda su estado al acabar su musica, antes de vaciarse, y la siguiente
        // sigue con el (Sigue, desde SigueA): su retardo lleva la cola de la anterior otra vez (el relevo la tira, es su cebado) y
        // la musica de la siguiente sale con la ganancia que venia y la pre-escucha de verdad. El cebado de delante pasa tal cual.
        private sealed class LimitadorPicoReal : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, look, lag, total;
            private readonly float ceiling, atk, rel;
            // QUE RESPIRA MENOS (quinta tanda del modo billar, 27-10-2026; solo en el billar): ver RespiraSueltaLentaS.
            private readonly bool respira;
            private readonly float relLenta, kActividad;
            private float actividad, factividad;
            internal const double SoltarS = 0.10, EsperaS = 0.02;   // soltar (constante de tiempo) y espera antes de soltar
            private readonly int espera;   // cuadros
            private int esperando;
            private readonly float[] delay;             // 'total' cuadros: pre-escucha + espera del detector
            private readonly DetectorPicoReal[] det;    // uno por canal
            private readonly float[] picoCuadro;        // pico real de los ultimos look+1 cuadros resueltos
            private readonly float[] peaks;
            private readonly long[] peakFrames;
            private int peakHead, peakCount;
            private long frameIndex, paso;
            private int dpos, spos;   // posiciones en los anillos (sin divisiones por muestra)
            private bool srcEnded, hadInput;
            private int drainFrames;
            private float env = 1f;
            private float ultimoIntervalo;
            private readonly float[] resto;             // un cuadro que el origen dejo partido
            private int restoN;
            private readonly float[] sale;              // un cuadro entero leido aparte cuando piden menos de uno (ver Read)
            private int saleIdx, saleN;
            // Disco continuo: la anterior de la que seguir, el cebado de delante (lo pone ArmaCadena) y lo guardado al acabar la musica.
            private volatile LimitadorPicoReal anterior;
            internal int Cebado;
            private int cebadoPorPasar;                 // cuadros
            private bool empezo, finGuardado;
            private readonly DetectorPicoReal[] fdet;
            private readonly float[] fdelay, fpico, fpeaks;
            private readonly long[] fpeakFrames;
            private int fpeakHead, fpeakCount, fdpos, fspos, fesperando;
            private long fframeIndex, fpaso;
            private float fenv, fultimo;
            public LimitadorPicoReal(NAudio.Wave.ISampleProvider s, float ceilingLin, double lookMs) : this(s, ceilingLin, lookMs, false) { }
            // SUELTA SEGUN LA MUSICA, EN EL BILLAR. En una cancion que el limitador trabaja seguido (una grabacion vieja aplastada, una muy
            // cargada de grave), soltar siempre en 100 ms hace que la ganancia suba y baje con cada golpe: se oye como un bombeo. Con 'respira'
            // se lleva la cuenta de cuanto rato esta bajando (la fraccion del ultimo segundo, mas o menos) y la suelta se alarga con ella, de
            // 100 ms (golpes sueltos: igual que siempre) hasta RespiraSueltaLentaS (bajando todo el rato). Sin 'respira', bit a bit como siempre.
            internal const double RespiraSueltaLentaS = 0.4, RespiraActividadS = 1.0;
            internal const float RespiraUmbral = 0.98f;   // por debajo de esto (0,18 dB) cuenta como que esta bajando
            public LimitadorPicoReal(NAudio.Wave.ISampleProvider s, float ceilingLin, double lookMs, bool respiraBillar)
            {
                respira = respiraBillar;
                src = s; ch = Math.Max(1, s.WaveFormat.Channels); ceiling = ceilingLin;
                int rate = s.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                look = Math.Max(16, (int)(lookMs * 0.001 * rate));
                lag = DetectorPicoReal.Retraso;
                total = look + lag;
                delay = new float[total * ch];
                picoCuadro = new float[look + 1];
                det = new DetectorPicoReal[ch];
                for (int c = 0; c < ch; c++) det[c] = new DetectorPicoReal(ceilingLin);
                peaks = new float[look + 1]; peakFrames = new long[look + 1];
                atk = 1f - (float)Math.Exp(-9.0 / look);
                rel = 1f - (float)Math.Exp(-1.0 / (SoltarS * rate));
                relLenta = 1f - (float)Math.Exp(-1.0 / (RespiraSueltaLentaS * rate));
                kActividad = 1f - (float)Math.Exp(-1.0 / (RespiraActividadS * rate));
                espera = (int)(EsperaS * rate);
                resto = new float[ch];
                sale = new float[ch];
                fdet = new DetectorPicoReal[ch];
                for (int c = 0; c < ch; c++) fdet[c] = new DetectorPicoReal(ceilingLin);
                fdelay = new float[delay.Length]; fpico = new float[picoCuadro.Length]; fpeaks = new float[peaks.Length]; fpeakFrames = new long[peakFrames.Length];
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int DelayFrames { get { return total; } }

            internal void Sigue(LimitadorPicoReal previa) { if (previa != null && !ReferenceEquals(previa, this)) anterior = previa; }
            // Al acabar la musica (antes de vaciar la pre-escucha con silencio): todo lo que hace falta para seguir.
            private void GuardaFin()
            {
                finGuardado = true;
                for (int c = 0; c < ch; c++) fdet[c].CopiaDe(det[c]);
                Array.Copy(delay, fdelay, delay.Length); Array.Copy(picoCuadro, fpico, fpico.Length);
                Array.Copy(peaks, fpeaks, fpeaks.Length); Array.Copy(peakFrames, fpeakFrames, fpeakFrames.Length);
                fpeakHead = peakHead; fpeakCount = peakCount; fdpos = dpos; fspos = spos; fesperando = esperando;
                fframeIndex = frameIndex; fpaso = paso; fenv = env; fultimo = ultimoIntervalo; factividad = actividad;
            }
            // En la primera lectura: el limitador de la anterior del mismo disco, como lo dejo su musica.
            private void Hereda()
            {
                LimitadorPicoReal a = anterior; anterior = null;
                if (a == null || !a.finGuardado || a.ch != ch || a.total != total || a.look != look || a.ceiling != ceiling) return;
                for (int c = 0; c < ch; c++) det[c].CopiaDe(a.fdet[c]);
                Array.Copy(a.fdelay, delay, delay.Length); Array.Copy(a.fpico, picoCuadro, picoCuadro.Length);
                Array.Copy(a.fpeaks, peaks, peaks.Length); Array.Copy(a.fpeakFrames, peakFrames, peakFrames.Length);
                peakHead = a.fpeakHead; peakCount = a.fpeakCount; dpos = a.fdpos; spos = a.fspos; esperando = a.fesperando;
                frameIndex = a.fframeIndex; paso = a.fpaso; env = a.fenv; ultimoIntervalo = a.fultimo;
                if (respira && a.respira) actividad = a.factividad;
                hadInput = true;
                cebadoPorPasar = Math.Max(0, Cebado);
            }

            // Igual que en LookaheadLimiter: cola monotona del maximo de la ventana de pre-escucha.
            private void AdvanceGain(float peak)
            {
                long oldest = frameIndex - look;
                int L = peaks.Length;
                while (peakCount > 0 && peakFrames[peakHead] < oldest)
                { peakHead++; if (peakHead == L) peakHead = 0; peakCount--; }
                while (peakCount > 0)
                {
                    int last = peakHead + peakCount - 1; if (last >= L) last -= L;
                    if (peaks[last] > peak) break;
                    peakCount--;
                }
                int tail = peakHead + peakCount; if (tail >= L) tail -= L;
                peaks[tail] = peak; peakFrames[tail] = frameIndex++; peakCount++;
                float max = peaks[peakHead];
                float target = max > ceiling ? ceiling / max : 1f;
                if (target < env) { env += atk * (target - env); esperando = espera; }
                else if (esperando > 0) esperando--;
                else env += (respira ? rel + (relLenta - rel) * actividad : rel) * (target - env);
                if (respira) { actividad += kActividad * ((env < RespiraUmbral ? 1f : 0f) - actividad); if (actividad < 1e-6f) actividad = 0f; }   // sin desnormales tras un rato sin limitar
            }

            private float[] intervalos = new float[0];   // pico real de cada intervalo del bloque (el mayor de los canales)

            // LO QUE ENSENA CTRL+F3 (S5): cuanto bajo el limitador como mucho y el nivel que salio, desde la ultima vez que se
            // pregunto. Solo se apuntan: la salida es la misma bit a bit. (Se leen desde la pantalla; un valor a medio
            // escribir solo desvia una lectura de un medidor.)
            private float minEnv = 1f, sumaCuadrados;
            private int muestrasMedidas;
            internal float TomaBajadaDb()
            {
                float m = minEnv; minEnv = 1f;
                return m < 1f ? (float)(20.0 * Math.Log10(Math.Max(m, 1e-6f))) : 0f;
            }
            internal float TomaNivelDb()
            {
                float s2 = sumaCuadrados; int n = muestrasMedidas; sumaCuadrados = 0f; muestrasMedidas = 0;
                return n > 0 && s2 > 0f ? (float)(10.0 * Math.Log10(s2 / n)) : -120f;
            }

            // Todo un bloque: primero el detector de cada canal (en su bucle apretado), luego la ganancia.
            private void Bloque(float[] entrada, int o, float[] salida, int so, int frames)
            {
                if (intervalos.Length < frames) intervalos = new float[Math.Max(frames, 1024)];
                for (int c = 0; c < ch; c++) det[c].Bloque(entrada, o + c, ch, frames, intervalos, c == 0);
                for (int f = 0; f < frames; f++) Paso(entrada, o + f * ch, salida, so + f * ch, intervalos[f]);
            }

            // UN PASO. Entra el cuadro 'paso' (o silencio, al vaciar) y sale el cuadro paso - total.
            //   - El detector resuelve el INTERVALO que empieza en paso - lag (entre esa muestra y la siguiente).
            //   - El cuadro s = paso - lag se queda con el mayor de sus dos intervalos, el de antes y el de despues:
            //     un pico que cae entre dos muestras cuenta para las dos.
            //   - Ese pico entra en la ventana de pre-escucha; la ventana del cuadro que sale, j = s - look, cubre
            //     justo [j, j + look]. O sea: la pre-escucha es la de siempre y la espera del detector va APARTE.
            private void Paso(float[] entrada, int o, float[] salida, int so, float intervalo)
            {
                float pc = intervalo > ultimoIntervalo ? intervalo : ultimoIntervalo;
                ultimoIntervalo = intervalo;
                AdvanceGain(pc);
                // picoCuadro es un anillo de look+1 ranuras: el cuadro s = paso - lag va a la ranura paso mod
                // (look+1), y el que sale, j = s - look, se escribio hace look pasos: esta en la ranura SIGUIENTE.
                bool sale = paso >= total;
                int R = picoCuadro.Length;
                picoCuadro[spos] = pc;
                if (sale)
                {
                    int jp = spos + 1; if (jp == R) jp = 0;
                    float peak = picoCuadro[jp];
                    // La exponencial se acerca al objetivo sin alcanzarlo del todo: lo que falte se corrige con la
                    // MISMA ganancia para todos los canales, nunca recortando uno.
                    if (peak > 0f && peak * env > ceiling) env = (ceiling / peak) * 0.9999999f;
                    if (env < minEnv) minEnv = env;
                }
                spos++; if (spos == R) spos = 0;
                int di = dpos * ch;
                for (int c = 0; c < ch; c++)
                {
                    float outv = delay[di + c] * env;
                    delay[di + c] = (entrada == null) ? 0f : entrada[o + c];
                    salida[so + c] = sale ? outv : 0f;
                    if (sale) sumaCuadrados += outv * outv;
                }
                if (sale) muestrasMedidas += ch;
                dpos++; if (dpos == total) dpos = 0;
                paso++;
            }

            private int Drain(float[] buffer, int offset, int count)
            {
                if (!finGuardado && hadInput) GuardaFin();   // (antes del silencio de vaciar)
                int frames = Math.Min(drainFrames, count / ch);
                if (frames > 0) Bloque(null, 0, buffer, offset, frames);
                drainFrames -= frames;
                return frames * ch;
            }

            // Pedir menos de un cuadro (una muestra en estereo) NO es el fin: devolver 0 lo tomaba por tal el Encadenado. Se lee el
            // cuadro entero aparte, se entrega la parte pedida y el resto sale en la lectura siguiente (como GraveMono y SalaLocal).
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (count <= 0) return 0;
                if (!empezo) { empezo = true; Hereda(); }
                int hechos = 0;
                if (saleN > 0)
                {
                    int k = Math.Min(count, saleN);
                    for (int i = 0; i < k; i++) buffer[offset + i] = sale[saleIdx + i];
                    saleIdx += k; saleN -= k; hechos = k;
                    if (saleN > 0) return hechos;
                }
                int faltan = count - hechos;
                if (faltan >= ch)
                {
                    int n = Lee(buffer, offset + hechos, faltan);
                    return n > 0 ? hechos + n : hechos;
                }
                if (faltan > 0)
                {
                    int n = Lee(sale, 0, ch);
                    if (n <= 0) return hechos;
                    int k = Math.Min(faltan, n);
                    for (int i = 0; i < k; i++) buffer[offset + hechos + i] = sale[i];
                    saleIdx = k; saleN = n - k; hechos += k;
                }
                return hechos;
            }

            private int Lee(float[] buffer, int offset, int count)
            {
                if (count < ch) return 0;   // (solo lo llama Read con al menos un cuadro)
                int want = count - count % ch;
                while (true)
                {
                    if (srcEnded) return Drain(buffer, offset, count);
                    // Si el origen dejo un cuadro partido en la vuelta anterior, va delante: nada se pierde ni se cruza.
                    int tiene = restoN;
                    for (int i = 0; i < restoN; i++) buffer[offset + i] = resto[i];
                    restoN = 0;
                    bool fin = false;
                    while (tiene < want)
                    {
                        int r = src.Read(buffer, offset + tiene, want - tiene);
                        if (r <= 0) { fin = true; break; }
                        tiene += r;
                        if (tiene % ch == 0) break;
                    }
                    int frames = tiene / ch;
                    if (fin)
                    {
                        // Al final, un trozo de cuadro no se puede completar: se descarta. Lo demas sale y luego se vacia
                        // la espera del detector Y la pre-escucha: las ultimas muestras no se cortan.
                        srcEnded = true;
                        drainFrames = (hadInput || frames > 0) ? total : 0;
                    }
                    else
                    {
                        restoN = tiene - frames * ch;
                        for (int i = 0; i < restoN; i++) resto[i] = buffer[offset + frames * ch + i];
                    }
                    if (frames > 0)
                    {
                        hadInput = true;
                        int crudo = 0;   // tras heredar, el cebado de delante sale tal cual (el relevo lo tira)
                        if (cebadoPorPasar > 0) { crudo = Math.Min(frames, cebadoPorPasar); cebadoPorPasar -= crudo; }
                        if (frames > crudo) Bloque(buffer, offset + crudo * ch, buffer, offset + crudo * ch, frames - crudo);
                        return frames * ch;
                    }
                }
            }
        }
    }
}

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

        // Entra una muestra; devuelve el pico de la onda reconstruida en el intervalo que empieza Retraso
        // muestras atras (entre esa muestra y la siguiente, ambas incluidas). Si no puede pasar del techo, lo que
        // devuelve es solo el mayor de sus dos extremos: sirve igual, porque por debajo del techo no manda.
        internal float Empuja(float x)
        {
            pos--; if (pos < 0) pos = Taps - 1;
            hist[pos] = x; hist[pos + Taps] = x;
            float ax = x < 0f ? -x : x;
            if (ax > umbral) desdeAlta = 0; else if (desdeAlta < int.MaxValue / 2) desdeAlta++;
            // hist[pos + i] = x[m - i], i = 0..Taps-1. El intervalo es el de x[m - Retraso] .. x[m - Retraso + 1].
            float a0 = (float)Math.Abs(hist[pos + Retraso]), a4 = (float)Math.Abs(hist[pos + Retraso - 1]);
            if (desdeAlta >= Taps)
            {
                previo = a0;   // la rejilla fina no se calculo: para la parabola de la vuelta siguiente vale el extremo
                return a0 > a4 ? a0 : a4;
            }
            double u = 0, v = 0, w = 0;
            int mitad = Taps / 2;
            for (int i = 0; i < mitad; i++)
            {
                double a = hist[pos + i], b = hist[pos + Taps - 1 - i];
                u += P[i] * (a + b);
                v += M[i] * (a - b);
                w += Q[i] * (a + b);
            }
            float g1 = (float)Math.Abs(u + v), g2 = (float)Math.Abs(w), g3 = (float)Math.Abs(u - v);
            float pico = a0;
            if (g1 > pico) pico = g1; if (g2 > pico) pico = g2; if (g3 > pico) pico = g3; if (a4 > pico) pico = a4;
            // Afinar con una parabola donde la rejilla tiene un maximo: el pico de verdad suele caer entre puntos.
            pico = Afina(previo, a0, g1, pico);
            pico = Afina(a0, g1, g2, pico);
            pico = Afina(g1, g2, g3, pico);
            pico = Afina(g2, g3, a4, pico);
            previo = g3;
            return pico;
        }
        // LO MISMO QUE Empuja, PARA UN BLOQUE ENTERO de un canal (muestras separadas por 'salto'), con el estado
        // en variables locales: sin una llamada por muestra. La cuenta es identica (la bateria lo compara bit a
        // bit). 'picos[f]' se queda con el mayor entre lo que ya tenia y este canal, salvo en el primero.
        internal void Bloque(float[] datos, int inicio, int salto, int n, float[] picos, bool primero)
        {
            double[] h = hist, cp = P, cm = M, cq = Q;
            int p = pos, alta = desdeAlta; float prev = previo, um = umbral;
            const int T = Taps, R = Retraso, mitad = Taps / 2;
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
                    double u = 0, v = 0, w = 0;
                    for (int i = 0; i < mitad; i++)
                    {
                        double a = h[p + i], b = h[p + T - 1 - i];
                        u += cp[i] * (a + b);
                        v += cm[i] * (a - b);
                        w += cq[i] * (a + b);
                    }
                    float g1 = (float)Math.Abs(u + v), g2 = (float)Math.Abs(w), g3 = (float)Math.Abs(u - v);
                    pico = a0;
                    if (g1 > pico) pico = g1; if (g2 > pico) pico = g2; if (g3 > pico) pico = g3; if (a4 > pico) pico = a4;
                    pico = Afina(prev, a0, g1, pico);
                    pico = Afina(a0, g1, g2, pico);
                    pico = Afina(g1, g2, g3, pico);
                    pico = Afina(g2, g3, a4, pico);
                    prev = g3;
                }
                if (primero || pico > picos[f]) picos[f] = pico;
            }
            pos = p; desdeAlta = alta; previo = prev;
        }
        private static float Afina(float y0, float y1, float y2, float pico)
        {
            if (y1 < y0 || y1 < y2) return pico;
            float den = y0 - 2f * y1 + y2;
            if (!(den < 0f)) return pico;
            float d = 0.5f * (y0 - y2) / den;
            float v = y1 - 0.25f * (y0 - y2) * d;
            return v > pico ? v : pico;
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
        private sealed class LimitadorPicoReal : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, look, lag, total;
            private readonly float ceiling, atk, rel;
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
            public LimitadorPicoReal(NAudio.Wave.ISampleProvider s, float ceilingLin, double lookMs)
            {
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
                espera = (int)(EsperaS * rate);
                resto = new float[ch];
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int DelayFrames { get { return total; } }

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
                else env += rel * (target - env);
            }

            private float[] intervalos = new float[0];   // pico real de cada intervalo del bloque (el mayor de los canales)

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
                }
                spos++; if (spos == R) spos = 0;
                int di = dpos * ch;
                for (int c = 0; c < ch; c++)
                {
                    float outv = delay[di + c] * env;
                    delay[di + c] = (entrada == null) ? 0f : entrada[o + c];
                    salida[so + c] = sale ? outv : 0f;
                }
                dpos++; if (dpos == total) dpos = 0;
                paso++;
            }

            private int Drain(float[] buffer, int offset, int count)
            {
                int frames = Math.Min(drainFrames, count / ch);
                if (frames > 0) Bloque(null, 0, buffer, offset, frames);
                drainFrames -= frames;
                return frames * ch;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                if (count < ch) return 0;   // una lectura vacia no marca fin del origen
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
                        Bloque(buffer, offset, buffer, offset, frames);
                        return frames * ch;
                    }
                }
            }
        }
    }
}

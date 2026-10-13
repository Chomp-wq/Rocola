// QUITAR EL SISEO DE LAS GRABACIONES VIEJAS (I2; las dos ramas; 13 de octubre de 2026).
//
// Muchas cumbias, boleros y pasados de casete traen un siseo de fondo (la cinta, el vinilo, la primera digitalizacion).
// Con el realce de brillo encima se nota mas, sobre todo en las intros y los pasajes suaves. Esto lo baja con cuidado.
//
// COMO LO RECONOCE. El siseo es un ruido que esta SIEMPRE igual en los agudos: no sube y baja con la musica. Por cada
// banda de agudos (de 3 kHz para arriba) se apunta lo mas bajo que llego la banda en cada trozo de 0,75 s; si en los
// ultimos cuatro trozos ese minimo fue casi el mismo (dentro de 3 dB) y no es silencio digital, ahi hay un piso fijo.
// Los platillos y las voces van y vienen: su minimo cambia de un trozo a otro y no cuentan como piso. Y solo se actua
// si el piso fijo esta en casi todas las bandas a la vez (el siseo es de banda ancha). Lo aprende CADA CANCION mientras
// suena: las primeras 3 s escucha y no toca nada.
//
// QUE HACE. En cada banda con siseo, cuando lo que suena esta cerca del piso (a menos de 6 dB) se baja, como mucho
// 6 dB; lo que suena claramente por encima del piso (la musica) no se toca. La ganancia baja despacio y sube deprisa,
// para que un golpe de platillo pase entero. Por encima de 3 kHz y nada mas: los graves y la voz quedan como estan.
//
// LO QUE NO TOCA, SALE IGUAL. Se trabaja por trozos de 1024 muestras con solape del 50 % (ventana seno, que con su
// pareja suma exactamente uno) y no se reconstruye la senal: se calcula SOLO lo que se quita y se resta de la original
// retrasada. Donde no se quita nada no se resta nada, y la muestra sale con los mismos bits (con 11,6 ms de retraso, que
// la cadena cuenta como el de las demas etapas). Mono y estereo; con mas canales pasa tal cual.
//
// Interruptor en Ctrl+F3 (Sonido -> Mejoras con IA), apagado de fabrica: "quita_siseo=". Se mira al abrir cada cancion.
// Coste: una transformada de 1024 por canal cada 512 muestras, y otra de vuelta solo en los trozos donde quita algo.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class QuitaSiseo : ISampleProvider
    {
        internal const double DesdeHz = 3000.0;          // solo los agudos
        internal const float GananciaMinima = 0.5f;      // -6 dB como mucho
        internal const double TrozoSeg = 0.75;           // cada cuanto se apunta el minimo de cada banda
        internal const int Trozos = 4;                   // cuantos minimos seguidos tienen que coincidir (3 s)
        internal const double Coinciden = 2.0;           // "casi el mismo": el mayor no pasa del doble del menor (3 dB)
        internal const double PisoMinimo = 1e-9;         // un piso mas bajo (-90 dB) es silencio limpio: no es siseo
        internal const double PisoMaximo = 1e-4;         // uno mas alto (-40 dB) ya es musica, no siseo
        internal const double Quieto = 0.5;              // la banda tiene que pasar al menos la mitad del trozo pegada a su piso
        internal const double BandasConSiseo = 0.7;      // en cuantas bandas tiene que estar el piso fijo a la vez
        private const double Sesgo = 1.5;                // el minimo de una potencia suavizada queda por debajo de la media

        private readonly ISampleProvider src;
        private readonly int ch, n, h, nb;
        private readonly bool activo;
        private readonly double[] ventana, cosT, senT;
        private readonly int[] inversa;                  // permutacion de bits de la transformada
        private readonly int[] bandaIni, bandaFin;       // bins de cada banda [ini, fin)
        private readonly double[][] entrada, quita;      // por canal: las ultimas n muestras y lo que se les quita (solapado)
        private readonly double[] re, im, pisoBanda;
        private readonly double[][] xr, xi;             // por canal: su espectro de este trozo (hasta n/2)
        private readonly bool[] quitaCanal;
        private readonly double[][] suave, minTrozo;     // por canal y banda: potencia suavizada y minimo del trozo actual
        private readonly int[][] cerca;                  // por canal y banda: cuadros del trozo actual pegados al piso
        private readonly bool[][] quieta;                // por canal y banda: el ultimo trozo paso la mitad pegado al piso
        private readonly double[][][] minimos;           // por canal, banda y trozo: los ultimos minimos apuntados
        private readonly float[][] ganancia;             // por canal y banda: la ganancia que se esta aplicando
        private readonly int framesPorTrozo;
        private int framesEnTrozo, trozosVistos, trozoActual;
        private readonly float[] lee, sale;              // h cuadros entrelazados: lo leido y lo que esta listo
        private int saleN, salePos;
        private bool acabo;
        private long dentro, fuera;                      // cuadros leidos del origen y entregados (salen dentro + h)
        internal long TrozosConQuita;                    // cuantos trozos (por canal) llevaron alguna rebaja (las pruebas)

        public QuitaSiseo(ISampleProvider fuente)
        {
            src = fuente;
            WaveFormat f = fuente.WaveFormat;
            ch = f.Channels;
            int tasa = f.SampleRate;
            n = tasa > 48000 ? 2048 : 1024; h = n / 2;
            activo = (ch == 1 || ch == 2) && tasa >= 16000;
            lee = new float[h * Math.Max(1, ch)]; sale = new float[h * Math.Max(1, ch)];
            if (!activo) return;
            ventana = new double[n];
            for (int i = 0; i < n; i++) ventana[i] = Math.Sin(Math.PI * i / n);   // seno al cuadrado con solape de la mitad: suma uno
            cosT = new double[n / 2]; senT = new double[n / 2];
            for (int i = 0; i < n / 2; i++) { cosT[i] = Math.Cos(2 * Math.PI * i / n); senT[i] = Math.Sin(2 * Math.PI * i / n); }
            inversa = new int[n];
            int bits = 0; while ((1 << bits) < n) bits++;
            for (int i = 0; i < n; i++) { int r = 0; for (int b = 0; b < bits; b++) if ((i & (1 << b)) != 0) r |= 1 << (bits - 1 - b); inversa[i] = r; }
            // Bandas de un tercio de octava desde 3 kHz hasta casi Nyquist (sin el ultimo bin).
            System.Collections.Generic.List<int> ini = new System.Collections.Generic.List<int>(), fin = new System.Collections.Generic.List<int>();
            double hz = DesdeHz, porBin = (double)tasa / n;
            while (true)
            {
                int a = (int)Math.Ceiling(hz / porBin), b = (int)Math.Ceiling(hz * Math.Pow(2, 1.0 / 3) / porBin);
                if (a >= n / 2 - 1) break;
                if (b > n / 2 - 1) b = n / 2 - 1;
                if (b - a < 2) b = Math.Min(n / 2 - 1, a + 2);
                ini.Add(a); fin.Add(b); hz = b * porBin;
                if (b >= n / 2 - 1) break;
            }
            nb = ini.Count; bandaIni = ini.ToArray(); bandaFin = fin.ToArray();
            entrada = new double[ch][]; quita = new double[ch][]; suave = new double[ch][]; minTrozo = new double[ch][];
            minimos = new double[ch][][]; ganancia = new float[ch][]; cerca = new int[ch][]; quieta = new bool[ch][];
            for (int c = 0; c < ch; c++)
            {
                entrada[c] = new double[n]; quita[c] = new double[n]; suave[c] = new double[nb]; minTrozo[c] = new double[nb];
                minimos[c] = new double[nb][]; ganancia[c] = new float[nb]; cerca[c] = new int[nb]; quieta[c] = new bool[nb];
                for (int b = 0; b < nb; b++) { minimos[c][b] = new double[Trozos]; ganancia[c][b] = 1f; minTrozo[c][b] = double.MaxValue; }
            }
            re = new double[n]; im = new double[n]; pisoBanda = new double[nb]; quitaCanal = new bool[ch];
            xr = new double[ch][]; xi = new double[ch][];
            for (int c = 0; c < ch; c++) { xr[c] = new double[n / 2 + 1]; xi[c] = new double[n / 2 + 1]; }
            framesPorTrozo = Math.Max(1, (int)Math.Round(TrozoSeg * tasa / h));
        }

        public WaveFormat WaveFormat { get { return src.WaveFormat; } }
        // Lo que retrasa la senal (en cuadros): la cadena lo suma al de las demas etapas.
        public int DelayFrames { get { return activo ? h : 0; } }

        public int Read(float[] buffer, int offset, int count)
        {
            if (!activo) return src.Read(buffer, offset, count);
            int hechos = 0;
            while (hechos < count)
            {
                if (salePos < saleN)
                {
                    int k = Math.Min(saleN - salePos, count - hechos);
                    for (int i = 0; i < k; i++) buffer[offset + hechos + i] = sale[salePos + i];
                    salePos += k; hechos += k;
                    continue;
                }
                if (!Siguiente()) break;
            }
            return hechos;
        }

        // Lee h cuadros (o lo que quede), los pasa y deja listos h cuadros. Salen exactamente los que entraron mas h (el
        // retraso): al acabar el origen se empuja con ceros hasta soltar la cola.
        private bool Siguiente()
        {
            int quiero = h * ch, tiene = 0;
            if (!acabo)
                while (tiene < quiero)
                {
                    int r = src.Read(lee, tiene, quiero - tiene);
                    if (r <= 0) { acabo = true; break; }
                    tiene += r;
                }
            int cuadros = tiene / ch;
            tiene = cuadros * ch;   // medio cuadro suelto al final del origen: no se puede completar (se descarta)
            dentro += cuadros;
            long quedan = dentro + h - fuera;
            if (quedan <= 0) return false;
            int salen = (int)Math.Min(h, quedan);
            for (int i = tiene; i < quiero; i++) lee[i] = 0f;   // detras del final, ceros: empujan la cola
            Trozo();
            Avanza();
            // Sale lo mas viejo: la muestra de hace h cuadros, menos lo que se le quita.
            for (int f = 0; f < salen; f++)
                for (int c = 0; c < ch; c++)
                    sale[f * ch + c] = (float)(entrada[c][f] - quita[c][f]);
            // Corre el solape para el siguiente trozo.
            for (int c = 0; c < ch; c++)
            {
                double[] e = entrada[c], q = quita[c];
                Array.Copy(e, h, e, 0, h);
                Array.Copy(q, h, q, 0, h);
                for (int i = h; i < n; i++) q[i] = 0.0;
            }
            fuera += salen;
            saleN = salen * ch; salePos = 0;
            return true;
        }

        // Un trozo de n muestras de los dos canales en UNA transformada: el izquierdo va de parte real y el derecho de
        // imaginaria, y se separan por simetria. Lo mismo de vuelta: lo que se le quita a cada uno sale en una inversa.
        private void Trozo()
        {
            for (int c = 0; c < ch; c++)
            {
                double[] e = entrada[c];
                for (int i = 0; i < h; i++) e[h + i] = lee[i * ch + c];   // la mitad nueva (la vieja ya esta corrida)
            }
            double[] eL = entrada[0], eR = ch == 2 ? entrada[1] : null;
            for (int i = 0; i < n; i++) { re[i] = eL[i] * ventana[i]; im[i] = eR != null ? eR[i] * ventana[i] : 0.0; }
            Transforma(false);
            int m = n / 2;
            for (int k = 0; k <= m; k++)
            {
                int kk = (n - k) & (n - 1);
                double a = re[k], b = im[k], c2 = re[kk], d = im[kk];
                if (eR == null) { xr[0][k] = a; xi[0][k] = b; continue; }
                xr[0][k] = 0.5 * (a + c2); xi[0][k] = 0.5 * (b - d);      // izquierdo: (Z[k] + conj Z[n-k]) / 2
                xr[1][k] = 0.5 * (b + d); xi[1][k] = -0.5 * (a - c2);     // derecho:   (Z[k] - conj Z[n-k]) / 2i
            }
            bool algo = false;
            for (int c = 0; c < ch; c++) { quitaCanal[c] = Ganancias(c); algo |= quitaCanal[c]; }
            if (!algo) return;   // no se quita nada: no se resta nada, y las muestras salen con sus mismos bits
            // Lo que se quita a cada canal: (1 - g) de cada bin de sus bandas con rebaja; fuera de las bandas, nada.
            for (int k = 0; k < n; k++) { re[k] = 0.0; im[k] = 0.0; }
            for (int c = 0; c < ch; c++)
            {
                if (!quitaCanal[c]) continue;
                TrozosConQuita++;
                float[] g = ganancia[c];
                for (int b = 0; b < nb; b++)
                {
                    double f = 1.0 - g[b];
                    if (f == 0.0) continue;
                    for (int k = bandaIni[b]; k < bandaFin[b]; k++)
                    {
                        double rr = xr[c][k] * f, ri = xi[c][k] * f;   // R[k]; su espejo R[n-k] = conj R[k]
                        if (c == 0) { re[k] += rr; im[k] += ri; re[n - k] += rr; im[n - k] -= ri; }
                        else { re[k] -= ri; im[k] += rr; re[n - k] += ri; im[n - k] += rr; }   // + i R[k] y + i conj R[k]
                    }
                }
            }
            Transforma(true);
            double escala = 1.0 / n;
            for (int c = 0; c < ch; c++)
            {
                if (!quitaCanal[c]) continue;   // el otro canal no se toca: ni sus restos de redondeo
                double[] q = quita[c], z = c == 0 ? re : im;
                for (int i = 0; i < n; i++) q[i] += z[i] * escala * ventana[i];
            }
        }

        // Las ganancias de las bandas de un canal con su espectro (xr/xi) de este trozo. True si alguna baja algo.
        private bool Ganancias(int c)
        {
            double[] s = suave[c], mt = minTrozo[c];
            float[] g = ganancia[c];
            int[] cc = cerca[c]; bool[] qu = quieta[c];
            bool listo = trozosVistos >= Trozos;
            // El piso de cada banda: el menor de los ultimos minimos, si todos coinciden (dentro de 3 dB), esta entre
            // -90 y -40 dB y la banda paso el ultimo trozo pegada a el. 0 = esa banda no tiene piso fijo.
            double[] piso = pisoBanda;
            int fijas = 0;
            for (int b = 0; b < nb; b++)
            {
                piso[b] = 0.0;
                if (!listo) continue;
                double lo = double.MaxValue, hi = 0;
                for (int t = 0; t < Trozos; t++) { double v = minimos[c][b][t]; if (v < lo) lo = v; if (v > hi) hi = v; }
                if (lo < PisoMinimo || lo > PisoMaximo || hi > Coinciden * lo) continue;
                piso[b] = lo * Sesgo;
                if (qu[b]) fijas++;
            }
            bool hayPiso = fijas >= BandasConSiseo * nb;   // el siseo es de banda ancha: en casi todas a la vez
            bool quitaAlgo = false;
            double norma = 2.0 / n;   // potencia por muestra: con la ventana seno, |X|^2 de un ruido blanco es n/2 su varianza
            double[] xrc = xr[c], xic = xi[c];
            for (int b = 0; b < nb; b++)
            {
                double p = 0;
                for (int k = bandaIni[b]; k < bandaFin[b]; k++) p += xrc[k] * xrc[k] + xic[k] * xic[k];
                p *= norma / (bandaFin[b] - bandaIni[b]);
                s[b] = s[b] == 0 ? p : 0.7 * s[b] + 0.3 * p;
                if (s[b] < Desnormales.TopeDouble) s[b] = 0.0;   // silencio largo: sin restos desnormales (Desnormales.cs)
                if (s[b] < mt[b]) mt[b] = s[b];
                if (piso[b] > 0.0 && s[b] <= 2.0 * piso[b]) cc[b]++;
                float meta = 1f;
                if (hayPiso && piso[b] > 0.0 && qu[b])
                {
                    // A 6 dB o mas sobre el piso, nada; en el piso, la rebaja entera; en medio, en dB, lineal.
                    double sobre = s[b] / piso[b];
                    if (sobre < 4.0)
                    {
                        double x = sobre <= 1.0 ? 0.0 : Math.Log(sobre) / Math.Log(4.0);
                        meta = (float)(GananciaMinima + (1.0 - GananciaMinima) * x);
                    }
                }
                // Baja despacio (unos 60 ms) y vuelve de golpe: el golpe que llega pasa entero.
                float gb = g[b];
                gb = meta < gb ? gb + (meta - gb) * 0.2f : meta;
                if (gb > 0.999f) gb = 1f;
                g[b] = gb;
                if (gb < 1f) quitaAlgo = true;
            }
            return quitaAlgo;
        }

        // El trozo de 0,75 s: al cerrarlo se apunta el minimo de cada banda y se empieza otro.
        private void Avanza()
        {
            if (++framesEnTrozo < framesPorTrozo) return;
            framesEnTrozo = 0;
            for (int c = 0; c < ch; c++)
                for (int b = 0; b < nb; b++)
                {
                    minimos[c][b][trozoActual] = minTrozo[c][b] == double.MaxValue ? 0.0 : minTrozo[c][b];
                    minTrozo[c][b] = double.MaxValue;
                    quieta[c][b] = cerca[c][b] >= Quieto * framesPorTrozo;
                    cerca[c][b] = 0;
                }
            trozoActual = (trozoActual + 1) % Trozos;
            if (trozosVistos < Trozos) trozosVistos++;
        }

        // Transformada de Fourier compleja en el sitio (radix 2), sobre re/im. La inversa sin escalar.
        private void Transforma(bool inversaT)
        {
            for (int i = 0; i < n; i++)
            {
                int j = inversa[i];
                if (j > i) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
            }
            for (int largo = 2; largo <= n; largo <<= 1)
            {
                int mitad = largo >> 1, paso = n / largo;
                for (int i = 0; i < n; i += largo)
                    for (int k = 0; k < mitad; k++)
                    {
                        double wr = cosT[k * paso], wi = inversaT ? senT[k * paso] : -senT[k * paso];
                        int a = i + k, b = a + mitad;
                        double xr = re[b] * wr - im[b] * wi, xi = re[b] * wi + im[b] * wr;
                        re[b] = re[a] - xr; im[b] = im[a] - xi;
                        re[a] += xr; im[a] += xi;
                    }
            }
        }
    }

    public partial class Form1
    {
        // I2 (Ctrl+F3 -> Sonido -> Mejoras con IA; "quita_siseo="): apagado de fabrica. Se mira al armar la cadena de cada
        // cancion (ArmaCadena): vale desde la siguiente.
        private bool quitaSiseo;
    }
}

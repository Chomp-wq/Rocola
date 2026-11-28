// LAS PIEZAS DE LA CADENA NUEVA NORMAL (A) Y DEL MARCO COMUN (CADENA_NUEVA.md §2; el contrato: RocolaOs.CadenaNuevaPieza.cs). Todas son
// IRocolaOsPieza: float, dos canales, sin retardo, con parametros fijos decididos antes de sonar; nada sigue a la musica. Van dentro de Form1
// por lo que necesitan de ella (el pulso de las estrellas, las memorias del ecualizador del dueno, el modo noche).
//   RocolaOsPiezaEntrada        la primera: el pulso visual (ReportAudioBlock, lo que hacia LevelTap con lo crudo) y, si la cancion no esta medida,
//                          lo crudo para los arreglos de la proxima (RocolaOsCrudo). No toca la muestra.
//   RocolaOsArreglosFijos  la continua de cada canal (se resta), el canal invertido (se voltea) y el canal mudo (se le copia el otro): FIJOS,
//                          decididos por la medida. Sin arreglos no toca la muestra. (El zumbido va antes: QuitaZumbido de XP.)
//   RocolaOsTonoParejo     tono_parejo (apagado): la correccion fija de la cancion con 8 filtros que, juntos, dan EXACTAMENTE la curva pedida
//                          en el centro de cada banda (se resuelve el solape: TonoParejoGanancias), suave y de +-3 dB como mucho.
//   RocolaOsGravesCentro   los graves al centro por debajo de 120 Hz: el lado (L-R)/2 pierde su grave con un paso bajo FIR de fase lineal (seno
//                          cardinal con Kaiser, -6 dB en el corte, 60 dB de rechazo: el mismo de GraveMono), por FFT y leyendo por delante. Sin
//                          lo dinamico de GraveMono (ni S3, ni estrecho, ni la vuelta del canal al reves: ese ya lo voltea el arreglo fijo).
//                          Un estereo con los dos canales iguales sale tal cual, bit a bit.
//   RocolaOsMedidorVivo    lo que no se midio antes (Spotify, o una que entro a la cola sin tiempo): se mide mientras suena (RocolaOsMedidor),
//                          sin tocar la muestra; si suena entera, se guarda para la siguiente vez.
//   RocolaOsEqDuenoPieza   el ecualizador del dueno (sus 10 bandas, la memoria normal en la A y la del billar en la B), SIN su preganancia: la
//                          cuenta la ganancia de la cancion. En vivo como el de hoy (rampa de 12 dB en 50 ms), «Sin ecualizador» incluido.
//   RocolaOsGananciaPieza  LA GANANCIA de la cancion (fija toda la cancion) por el factor del modo noche (el reloj: su rampa de una hora, como
//                          hoy, alisada 20 ms). Unica ganancia de la cadena.
// Detras, el corte del grave de noche (GraveMono.DeNoche), los graves de noche (SalaLocal con todo lo demas apagado) y el fundido de XP.
using System;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // ------------------------------------------------------------------ la entrada: el pulso y lo crudo
        private sealed class RocolaOsPiezaEntrada : IRocolaOsPieza
        {
            private readonly ISampleProvider src;
            private readonly Form1 owner;
            private readonly long publicacion;
            private readonly int tasa;
            internal RocolaOsCrudo Crudo;   // null: no se mide (ya esta medida)
            internal RocolaOsPiezaEntrada(ISampleProvider s, Form1 o, bool midiendo)
            {
                src = s; owner = o; tasa = s.WaveFormat.SampleRate;
                publicacion = o != null ? o.publicacionAudio : 0;
                if (midiendo) Crudo = new RocolaOsCrudo(tasa);
            }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                // la red de seguridad de XP (LevelTap): un archivo roto que da NaN o 1e30 no pasa (a cero)
                for (int i = offset; i < offset + n; i++) { float x = buffer[i]; if (!(x >= -8f && x <= 8f)) buffer[i] = 0f; }
                RocolaOsCrudo c = Crudo; if (c != null) c.Procesa(buffer, offset, n);
                Form1 o = owner;
                if (o != null)
                    lock (o.audioDatosLock)
                    {
                        if (publicacion == o.publicacionAudio) o.ReportAudioBlock(buffer, offset, n, tasa, 2);
                    }
                return n;
            }
            public void Sigue(IRocolaOsPieza anterior) { }
        }

        // ------------------------------------------------------------------ los arreglos fijos
        private sealed class RocolaOsArreglosFijos : IRocolaOsPieza
        {
            private readonly ISampleProvider src;
            private readonly float dl, dr;
            private readonly bool inv;
            private readonly int mudo;
            private readonly bool nada;
            internal RocolaOsArreglosFijos(ISampleProvider s, RocolaOsReceta r)
            {
                src = s; dl = r.ContinuaL; dr = r.ContinuaR; inv = r.Invertido; mudo = r.CanalMudo;
                nada = dl == 0f && dr == 0f && !inv && mudo < 0;
            }
            internal bool Nada { get { return nada; } }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0 || nada) return n;
                int fin = offset + (n & ~1);   // (cuadros enteros: lo que pide la cadena es par)
                for (int i = offset; i < fin; i += 2)
                {
                    float l = buffer[i] - dl, r = buffer[i + 1] - dr;
                    if (inv) r = -r;
                    if (mudo == 0) l = r; else if (mudo == 1) r = l;
                    buffer[i] = l; buffer[i + 1] = r;
                }
                return n;
            }
            public void Sigue(IRocolaOsPieza anterior) { }
        }

        // ------------------------------------------------------------------ biquads fijos (tono parejo) y del dueno
        // Un biquad normalizado (b0 b1 b2 a1 a2) en doble: |H| en dB a la frecuencia 'hz'
        internal static double RocolaOsBiquadDb(double[] c, int o, double hz, int tasa) { return EcualizadorDueno.MagDb(c, o, 2.0 * Math.PI * hz / tasa); }
        // una campana RBJ (Q 1,41: una octava) o un estante en los extremos, como el ecualizador del dueno
        internal static void RocolaOsCampana(double hz, double db, double q, int tasa, double[] c, int o)
        {
            double A = Math.Pow(10.0, db / 40.0), w0 = 2.0 * Math.PI * hz / tasa, al = Math.Sin(w0) / (2.0 * q), cs = Math.Cos(w0);
            double a0 = 1.0 + al / A;
            c[o] = (1.0 + al * A) / a0; c[o + 1] = -2.0 * cs / a0; c[o + 2] = (1.0 - al * A) / a0; c[o + 3] = -2.0 * cs / a0; c[o + 4] = (1.0 - al / A) / a0;
        }

        // TONO PAREJO: LAS GANANCIAS DE LOS 8 FILTROS PARA QUE, JUNTOS, DEN LA CURVA PEDIDA. Las campanas de hoy (Q 1,1) se solapan: con +3 dB en dos
        // bandas vecinas, entre ellas sube mas de 3 y en cada centro tambien (la vecina suma). Aqui se resuelve: se empieza con lo pedido y se
        // corrige con lo que de verdad sale en cada centro (unas vueltas: la respuesta en dB es casi lineal en las ganancias), con Q = 1,41 (una
        // octava: el ancho de las bandas de la huella), acotado a +-RocolaOsTonoMaxDb por filtro. Puro.
        internal const double RocolaOsTonoQ = 1.41, RocolaOsTonoMaxDb = 3.0;
        internal static double[] TonoParejoGanancias(float[] pedido, int tasa)
        {
            float[] cen = EqCentros;
            double[] g = new double[8], c = new double[5];
            for (int b = 0; b < 8; b++) g[b] = pedido[b];
            for (int vuelta = 0; vuelta < 30; vuelta++)
            {
                double peor = 0;
                double[] sale = new double[8];
                for (int b = 0; b < 8; b++)
                {
                    if (cen[b] >= 0.45 * tasa) continue;
                    if (Math.Abs(g[b]) < 1e-6) continue;
                    RocolaOsCampana(cen[b], g[b], RocolaOsTonoQ, tasa, c, 0);
                    for (int k = 0; k < 8; k++) if (cen[k] < 0.45 * tasa) sale[k] += RocolaOsBiquadDb(c, 0, cen[k], tasa);
                }
                for (int b = 0; b < 8; b++)
                {
                    if (cen[b] >= 0.45 * tasa) { g[b] = 0; continue; }
                    double err = pedido[b] - sale[b];
                    peor = Math.Max(peor, Math.Abs(err));
                    g[b] = Math.Max(-RocolaOsTonoMaxDb * 1.5, Math.Min(RocolaOsTonoMaxDb * 1.5, g[b] + 0.8 * err));
                }
                if (peor < 0.01) break;
            }
            return g;
        }
        // la respuesta (dB) del tono parejo de una cancion en esas frecuencias (para estimar su efecto en la sonoridad y el pico)
        internal static double[] TonoParejoRespuesta(double[] g, int tasa, double[] hz)
        {
            double[] r = new double[hz.Length], c = new double[5];
            float[] cen = EqCentros;
            for (int b = 0; b < 8; b++)
            {
                if (g == null || Math.Abs(g[b]) < 1e-6 || cen[b] >= 0.45 * tasa) continue;
                RocolaOsCampana(cen[b], g[b], RocolaOsTonoQ, tasa, c, 0);
                for (int i = 0; i < hz.Length; i++) if (hz[i] > 0 && hz[i] < 0.5 * tasa) r[i] += RocolaOsBiquadDb(c, 0, hz[i], tasa);
            }
            return r;
        }

        private sealed class RocolaOsTonoParejo : IRocolaOsPieza
        {
            private readonly ISampleProvider src;
            private readonly int tasa, nf;
            private readonly double[] co;      // nf * 5
            private readonly double[] st;      // 2 canales * nf * 4
            private readonly double[] gDb;
            private volatile RocolaOsTonoParejo anterior;
            private bool empezo;
            internal double[] Ganancias { get { return gDb; } }
            internal RocolaOsTonoParejo(ISampleProvider s, double[] ganancias)
            {
                src = s; tasa = s.WaveFormat.SampleRate; gDb = ganancias;
                int n = 0;
                for (int b = 0; b < 8; b++) if (Math.Abs(ganancias[b]) >= 0.05 && EqCentros[b] < 0.45 * tasa) n++;
                nf = n; co = new double[n * 5]; st = new double[2 * n * 4];
                int k = 0;
                for (int b = 0; b < 8; b++) if (Math.Abs(ganancias[b]) >= 0.05 && EqCentros[b] < 0.45 * tasa) { RocolaOsCampana(EqCentros[b], ganancias[b], RocolaOsTonoQ, tasa, co, k * 5); k++; }
            }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (!empezo) { empezo = true; Hereda(); }
                int n = src.Read(buffer, offset, count);
                if (n <= 0 || nf == 0) return n;
                int fin = offset + (n & ~1);
                for (int i = offset; i < fin; i += 2)
                    for (int ch = 0; ch < 2; ch++)
                    {
                        double v = buffer[i + ch];
                        for (int k = 0; k < nf; k++)
                        {
                            int c5 = k * 5, e = (ch * nf + k) * 4;
                            double y = co[c5] * v + co[c5 + 1] * st[e] + co[c5 + 2] * st[e + 1] - co[c5 + 3] * st[e + 2] - co[c5 + 4] * st[e + 3];
                            if (y < 1e-30 && y > -1e-30) y = 0.0;
                            st[e + 1] = st[e]; st[e] = v; st[e + 3] = st[e + 2]; st[e + 2] = y;
                            v = y;
                        }
                        buffer[i + ch] = (float)v;
                    }
                return n;
            }
            public void Sigue(IRocolaOsPieza a) { RocolaOsTonoParejo p = a as RocolaOsTonoParejo; if (p != null && !ReferenceEquals(p, this)) anterior = p; }
            private void Hereda()
            {
                RocolaOsTonoParejo a = anterior; anterior = null;
                if (a == null || a.tasa != tasa || a.nf != nf) return;
                for (int b = 0; b < 8; b++) if (Math.Abs(a.gDb[b] - gDb[b]) > 1e-9) return;   // otra curva: de cero (el fundido de siempre no hace falta: es 0 a 0)
                Array.Copy(a.st, st, st.Length);
            }
        }

        // ------------------------------------------------------------------ los graves al centro (fase lineal)
        private sealed class RocolaOsGravesCentro : IRocolaOsPieza
        {
            internal const double AtenuacionDb = 60.0, EmpalmeSeg = 0.025;
            private readonly ISampleProvider src;
            private readonly int tasa, K, M, B, nUnion;
            private readonly bool activo;
            internal readonly float Hz;
            private readonly double[] H;                  // el paso bajo en frecuencia (real: fase cero), 0..M-1
            private readonly double[] wl, wr, re, im;     // la ventana [p - K, p + B + K) y la FFT de trabajo
            private readonly float[] lee, cola;
            private int colaI, colaN;
            private long leidos, emitidos;
            private bool primero = true, acabo, empezo, union;
            private readonly double[] finL, finR;         // los ultimos K cuadros (para la siguiente del mismo disco)
            private bool finHecho;
            private volatile RocolaOsGravesCentro anterior;
            internal RocolaOsGravesCentro(ISampleProvider s, float hz)
            {
                src = s; tasa = s.WaveFormat.SampleRate; Hz = hz;
                activo = hz > 0f && hz < 0.2f * tasa && s.WaveFormat.Channels == 2;
                nUnion = Math.Max(1, (int)Math.Round(EmpalmeSeg * tasa));
                if (!activo) return;
                double w0 = 2.0 * Math.PI * hz / tasa;
                K = ((int)Math.Ceiling((AtenuacionDb - 8.0) / (2.285 * w0)) + 1) / 2;
                int m = 2048; while (m < 4 * K) m <<= 1;
                M = m; B = M - 2 * K;
                H = Filtro(tasa, hz, K, M);
                wl = new double[M]; wr = new double[M]; re = new double[M]; im = new double[M];
                lee = new float[2 * (B + K)]; cola = new float[2 * B];
                finL = new double[K]; finR = new double[K];
            }
            private static readonly object cerrojo = new object();
            private static int fTasa, fK, fM; private static float fHz; private static double[] fH;
            private static double[] Filtro(int tasa, float hz, int K, int M)
            {
                lock (cerrojo) if (fH != null && fTasa == tasa && fHz == hz && fK == K && fM == M) return fH;
                double beta = 0.1102 * (AtenuacionDb - 8.7), fc = hz / (double)tasa, i0 = I0(beta), suma = 0;
                double[] h = new double[K + 1];
                for (int n = 0; n <= K; n++)
                {
                    double x = 2.0 * fc * n, u = (double)n / K;
                    double sinc = n == 0 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);
                    h[n] = 2.0 * fc * sinc * I0(beta * Math.Sqrt(Math.Max(0.0, 1.0 - u * u))) / i0;
                    suma += n == 0 ? h[n] : 2.0 * h[n];
                }
                double[] r = new double[M], q = new double[M];
                for (int n = 0; n <= K; n++) { r[n] = h[n] / suma; if (n > 0) r[M - n] = r[n]; }
                RocolaOsFft.Hace(r, q, false);   // (simetrico: q queda en cero; se usa r)
                lock (cerrojo) { fTasa = tasa; fHz = hz; fK = K; fM = M; fH = r; }
                return r;
            }
            private static double I0(double x)
            {
                double s = 1.0, t = 1.0;
                for (int k = 1; k < 200; k++) { double v = x / (2.0 * k); t *= v * v; s += t; if (t < 1e-16 * s) break; }
                return s;
            }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (!activo) return src.Read(buffer, offset, count);
                if (!empezo) { empezo = true; Hereda(); }
                int hechos = 0;
                while (hechos < count)
                {
                    if (colaN == 0 && !Bloque()) break;
                    int k = Math.Min(colaN, count - hechos);
                    Array.Copy(cola, colaI, buffer, offset + hechos, k);
                    colaI += k; colaN -= k; hechos += k;
                }
                return hechos;
            }
            public void Sigue(IRocolaOsPieza a) { RocolaOsGravesCentro p = a as RocolaOsGravesCentro; if (p != null && !ReferenceEquals(p, this)) anterior = p; }
            private void Hereda()
            {
                RocolaOsGravesCentro a = anterior; anterior = null;
                if (a == null || !a.activo || !a.acabo || !a.finHecho || a.tasa != tasa || a.Hz != Hz || a.K != K) return;
                for (int k = 0; k < K; k++) { wl[k] = a.finL[k]; wr[k] = a.finR[k]; }
                union = true;
            }
            private int Enteros(float[] b, int o, int quiero)
            {
                int tiene = 0;
                while (tiene < quiero)
                {
                    int r = src.Read(b, o + tiene, quiero - tiene);
                    if (r <= 0) { acabo = true; break; }
                    tiene += r;
                }
                return tiene & ~1;
            }
            // el grave del lado de toda la ventana (hasta 'hasta': lo de despues cuenta como ceros)
            private void GraveDelLado(int hasta)
            {
                for (int i = 0; i < M; i++) { re[i] = i < hasta ? 0.5 * (wl[i] - wr[i]) : 0.0; im[i] = 0.0; }
                RocolaOsFft.Hace(re, im, false);
                for (int i = 0; i < M; i++) { re[i] *= H[i]; im[i] *= H[i]; }
                RocolaOsFft.Hace(re, im, true);
            }
            private bool Bloque()
            {
                if (acabo && emitidos >= leidos) return false;
                int desde = primero ? K : 2 * K, quiere = primero ? B + K : B;
                int tiene = acabo ? 0 : Enteros(lee, 0, 2 * quiere);
                int got = tiene >> 1;
                for (int f = 0, k = 0; f < got; f++, k += 2) { wl[desde + f] = lee[k]; wr[desde + f] = lee[k + 1]; }
                for (int f = got; f < quiere; f++) { wl[desde + f] = 0.0; wr[desde + f] = 0.0; }
                primero = false;
                long p = emitidos;
                leidos += got;
                if (acabo && !finHecho)
                {
                    finHecho = true;
                    for (int k = 0; k < K; k++)
                    {
                        long nq = leidos - K + k; int w = (int)(nq - p + K);
                        finL[k] = (nq < 0 || w < 0) ? 0.0 : wl[w]; finR[k] = (nq < 0 || w < 0) ? 0.0 : wr[w];
                    }
                }
                int salen = (int)Math.Min((long)B, leidos - p);
                if (salen <= 0) return false;
                int nu = 0; double eu = 0.0;
                if (union)
                {   // EL EMPALME con la anterior del mismo disco: su ultimo cuadro no conocia lo de esta; esa diferencia se lleva a cero en 25 ms
                    union = false; nu = Math.Min(nUnion, salen);
                    GraveDelLado(K); double solo = re[K];
                    GraveDelLado(M); eu = re[K] - solo;
                }
                else GraveDelLado(M);
                double iu = 1.0 / nUnion;
                for (int t = 0, w = K, q = 0; t < salen; t++, w++, q += 2)
                {
                    double l = wl[w], r = wr[w];
                    double m = 0.5 * (l + r), s = 0.5 * (l - r);
                    double lo = re[w];
                    if (t < nu) lo -= (1.0 - t * iu) * eu;
                    double h2 = s - lo;
                    cola[q] = (float)(m + h2); cola[q + 1] = (float)(m - h2);
                }
                Array.Copy(wl, B, wl, 0, 2 * K); Array.Copy(wr, B, wr, 0, 2 * K);
                emitidos += salen; colaI = 0; colaN = 2 * salen;
                return true;
            }
        }

        // ------------------------------------------------------------------ la medida en vivo
        private sealed class RocolaOsMedidorVivo : IRocolaOsPieza
        {
            private readonly ISampleProvider src;
            internal readonly RocolaOsMedidor Medidor;
            internal Action<RocolaOsMedidorVivo> AlAcabar;   // (en el hilo del sonido: solo encola)
            private bool acabado;
            internal RocolaOsMedidorVivo(ISampleProvider s) { src = s; Medidor = new RocolaOsMedidor(s.WaveFormat.SampleRate); }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n > 0) { try { Medidor.Procesa(buffer, offset, n); } catch { } }
                else if (!acabado) { acabado = true; Action<RocolaOsMedidorVivo> a = AlAcabar; if (a != null) { try { a(this); } catch { } } }
                return n;
            }
            public void Sigue(IRocolaOsPieza anterior) { }
        }

        // ------------------------------------------------------------------ el ecualizador del dueno, sin preganancia
        private sealed class RocolaOsEqDuenoPieza : IRocolaOsPieza
        {
            internal const int Bloque = 32;
            private readonly ISampleProvider src;
            private readonly Form1 owner;
            private readonly bool billar;
            private readonly int tasa;
            private readonly double paso1;
            private readonly int[] obj = new int[EcualizadorDueno.Bandas];
            private readonly double[] act = new double[EcualizadorDueno.Bandas], coPara = new double[EcualizadorDueno.Bandas];
            private readonly bool[] corre = new bool[EcualizadorDueno.Bandas], valida = new bool[EcualizadorDueno.Bandas];
            private readonly double[] co = new double[EcualizadorDueno.Bandas * 5];
            private readonly double[] st = new double[2 * EcualizadorDueno.Bandas * 4];
            private bool empezo, algo;
            private volatile RocolaOsEqDuenoPieza anterior;
            internal RocolaOsEqDuenoPieza(ISampleProvider s, Form1 o, bool memoriaBillar, int[] inicial)
            {
                src = s; owner = o; billar = memoriaBillar; tasa = s.WaveFormat.SampleRate;
                paso1 = EcualizadorDueno.RampaDb / (EcualizadorDueno.RampaSeg * Math.Max(1, tasa));
                for (int b = 0; b < EcualizadorDueno.Bandas; b++)
                {
                    valida[b] = EcualizadorDueno.Vale(b, tasa); coPara[b] = double.NaN;
                    int m = inicial != null && b < inicial.Length && valida[b] ? EcualizadorDueno.Acota(inicial[b]) : 0;
                    obj[b] = m; act[b] = m * 0.5; if (m != 0) { corre[b] = true; algo = true; }
                }
            }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            // lo que pide ahora el dueno (sus enteros de medio dB; con «Sin ecualizador», todo en 0)
            private void Mira()
            {
                Form1 o = owner;
                if (o == null) return;
                bool sin = o.EqDuenoSinPuesto;
                int[] mem = billar ? eqDuenoBillar : eqDuenoNormal;
                for (int b = 0; b < EcualizadorDueno.Bandas; b++) obj[b] = (sin || !valida[b]) ? 0 : EcualizadorDueno.Acota(mem[b]);
            }
            public int Read(float[] buffer, int offset, int count)
            {
                if (!empezo) { empezo = true; Hereda(); }
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                Mira();
                if (!algo) { for (int b = 0; b < EcualizadorDueno.Bandas; b++) if (obj[b] != 0) { algo = true; break; } }
                if (!algo) return n;   // todo en 0 desde el principio: tal cual, bit a bit
                int fin = offset + (n & ~1), i = offset;
                while (i < fin)
                {
                    int m = Math.Min(Bloque, (fin - i) / 2);
                    double paso = paso1 * m;
                    for (int k = 0; k < EcualizadorDueno.Bandas; k++)
                    {
                        if (!valida[k]) continue;
                        double a = act[k], meta = obj[k] * 0.5;
                        if (a != meta) { double d = meta - a; if (d > paso) a += paso; else if (d < -paso) a -= paso; else a = meta; act[k] = a; }
                        if (a != 0.0) corre[k] = true;
                        if (corre[k] && a != coPara[k])
                        {
                            if (a == 0.0) { co[k * 5] = 1; co[k * 5 + 1] = 0; co[k * 5 + 2] = 0; co[k * 5 + 3] = 0; co[k * 5 + 4] = 0; }
                            else EcualizadorDueno.Coef(k, a, tasa, co, k * 5);
                            coPara[k] = a;
                        }
                    }
                    for (int f = 0; f < m; f++, i += 2)
                        for (int ch = 0; ch < 2; ch++)
                        {
                            double v = buffer[i + ch];
                            for (int k = 0; k < EcualizadorDueno.Bandas; k++)
                            {
                                if (!corre[k]) continue;
                                int c5 = k * 5, e = (ch * EcualizadorDueno.Bandas + k) * 4;
                                double y = co[c5] * v + co[c5 + 1] * st[e] + co[c5 + 2] * st[e + 1] - co[c5 + 3] * st[e + 2] - co[c5 + 4] * st[e + 3];
                                if (y < 1e-30 && y > -1e-30) y = 0.0;
                                st[e + 1] = st[e]; st[e] = v; st[e + 3] = st[e + 2]; st[e + 2] = y;
                                v = y;
                            }
                            buffer[i + ch] = (float)v;
                        }
                }
                return n;
            }
            public void Sigue(IRocolaOsPieza a) { RocolaOsEqDuenoPieza p = a as RocolaOsEqDuenoPieza; if (p != null && !ReferenceEquals(p, this)) anterior = p; }
            private void Hereda()
            {
                RocolaOsEqDuenoPieza a = anterior; anterior = null;
                if (a == null || a.tasa != tasa || a.billar != billar) return;
                Array.Copy(a.act, act, act.Length); Array.Copy(a.coPara, coPara, coPara.Length); Array.Copy(a.co, co, co.Length);
                Array.Copy(a.st, st, st.Length); Array.Copy(a.corre, corre, corre.Length); algo = a.algo || algo;
            }
            // lo que hace ahora (para la hoja): el maximo que sube
            internal int[] Pedido { get { return (int[])obj.Clone(); } }
        }

        // ------------------------------------------------------------------ la ganancia (y el factor del modo noche)
        private sealed class RocolaOsGananciaPieza : IRocolaOsPieza
        {
            internal const double RampaSeg = 0.020;
            private readonly ISampleProvider src;
            private readonly Form1 owner;
            internal readonly float Ganancia;          // lineal, fija toda la cancion
            private float actual = float.NaN;          // lo que lleva ahora (ganancia por noche), muestra a muestra
            private readonly float pasoMax;            // de un extremo al otro en 20 ms
            private volatile RocolaOsGananciaPieza anterior;
            private bool empezo;
            internal RocolaOsGananciaPieza(ISampleProvider s, Form1 o, double db)
            {
                src = s; owner = o;
                Ganancia = (float)Math.Pow(10.0, db / 20.0);
                pasoMax = (float)(1.0 / (RampaSeg * Math.Max(1, s.WaveFormat.SampleRate)));
            }
            internal float Noche { get { Form1 o = owner; if (o == null) return 1f; float f = o.FactorNoche(); return (f > 0f && f <= 4f) ? f : 1f; } }
            public WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (!empezo) { empezo = true; Hereda(); }
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                float meta = Ganancia * Noche;
                if (float.IsNaN(actual)) actual = meta;
                int fin = offset + (n & ~1);
                if (actual == meta) { for (int i = offset; i < fin; i++) buffer[i] *= meta; return n; }
                // el modo noche (el reloj) o el empalme con la anterior del mismo disco: hacia la meta en 20 ms por cada vez la ganancia
                float paso = pasoMax * Math.Max(meta, actual);
                for (int i = offset; i < fin; i += 2)
                {
                    if (actual < meta) { actual += paso; if (actual > meta) actual = meta; }
                    else if (actual > meta) { actual -= paso; if (actual < meta) actual = meta; }
                    buffer[i] *= actual; buffer[i + 1] *= actual;
                }
                return n;
            }
            public void Sigue(IRocolaOsPieza a) { RocolaOsGananciaPieza p = a as RocolaOsGananciaPieza; if (p != null && !ReferenceEquals(p, this)) anterior = p; }
            private void Hereda() { RocolaOsGananciaPieza a = anterior; anterior = null; if (a != null && !float.IsNaN(a.actual)) actual = a.actual; }
        }
    }
}

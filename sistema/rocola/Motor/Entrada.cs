// LA ENTRADA DE LA CADENA (Rocola/AudioControles.cs: FuenteOriginal, y la medida del original) y la red del DSEE (Rocola/Form1.Ia.cs:
// RedChica), tal cual de XP.
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        private int kwLimpia, eqLimpia;   // cuadros desde la ultima limpieza de restos diminutos (Desnormales.cs)
        // La misma lectura del decoder sirve para reproducir y aprender el original.
        // No se relee ni se duplica FFT; los bloques procesados solo alimentan el pulso visual.
        private sealed class FuenteOriginal : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly Form1 owner;
            internal LevelTap Tap;
            private float[] corteAnillo;
            private int corteEscribe;
            private long corteMuestras;
            internal long CuadrosOriginales { get { return corteMuestras / Math.Max(1, WaveFormat.Channels); } }
            // Solo si Declip esta delante del DSEE: dos ventanas bastan para el
            // Hop pedido por SpectralTap y el retardo real de Declip. No relee PCM.
            internal void PreparaCorte(int ventana, int retardo)
            {
                corteAnillo = new float[checked((2 * ventana + retardo) * WaveFormat.Channels)];
                corteEscribe = 0; corteMuestras = 0;
            }
            internal bool CuadroOriginal(long fin, int canal, float[] real, float[] imaginaria, float[] ventana)
            {
                if (corteAnillo == null || fin < ventana.Length) return false;
                int canales = WaveFormat.Channels;
                long inicio = (fin - ventana.Length) * canales;
                if (fin * canales > corteMuestras || inicio < corteMuestras - corteAnillo.Length) return false;
                int p = (int)(inicio % corteAnillo.Length) + canal;
                bool pareja = canal + 1 < canales;
                for (int i = 0; i < ventana.Length; i++)
                {
                    if (p >= corteAnillo.Length) p -= corteAnillo.Length;
                    real[i] = corteAnillo[p] * ventana[i];
                    imaginaria[i] = pareja ? corteAnillo[p + 1] * ventana[i] : 0f;
                    p += canales;
                }
                return true;
            }
            internal FuenteOriginal(NAudio.Wave.ISampleProvider src, Form1 owner) { this.src = src; this.owner = owner; }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                LimpiaMuestras(buffer, offset, n);
                if (corteAnillo != null)
                {
                    // El buffer puede ser WaveBuffer (float sobre byte[]): no Array.Copy.
                    for (int i = offset; i < offset + n; i++)
                    { corteAnillo[corteEscribe++] = buffer[i]; if (corteEscribe == corteAnillo.Length) corteEscribe = 0; }
                    corteMuestras += n;
                }
                if (n > 0 && Tap != null) owner.MideEntradaOriginal(Tap, buffer, offset, n);
                return n;
            }
        }
        private void MideEntradaOriginal(LevelTap tap, float[] buffer, int offset, int n)
        {
            lock (audioDatosLock)
            {
                if (tap.Publicacion != publicacionAudio) return;
                MedidaDe(tap);
                MideBloqueOriginal(buffer, offset, n, tap.WaveFormat.SampleRate, tap.WaveFormat.Channels);
                if (verifMidiendo) VerifMide(buffer, offset, n, tap.WaveFormat.SampleRate, tap.WaveFormat.Channels);   // C (SustitucionSpotify.cs)
            }
        }
        private void MideBloqueOriginal(float[] buffer, int offset, int count, int rate, int ch)
        {
            // NIVELADO PERCEPTUAL (K-weighting, tipo LUFS/BS.1770): en vez del RMS crudo, mide la sonoridad
            // PESADA por como la oye el humano (realza agudos ~+4 dB, quita sub-graves) via 2 biquads/canal.
            // Asi una cancion con mucho bajo (que el RMS crudo sobre-estima) y una brillante quedan de verdad
            // al mismo volumen PERCIBIDO. Solo durante el aprendizaje de esta cancion.
            if (curMeasurePath != null)
            {
                int ch3 = ch < 1 ? 1 : ch;
                if (kwRate != rate || kwCoef1 == null || kwCanales != ch3 || kwSt == null)
                {
                    kwRate = rate; kwCanales = ch3; kwCanal = 0;
                    kwCoef1 = EqShelfHi(1681.974f, rate, 3.99984f, 0.70717f);   // "head" shelf de BS.1770
                    kwCoef2 = EqHighPass(38.1355f, rate, 0.50033f);             // RLB high-pass
                    kwSt = new float[ch3 * 8];
                }
                float[] st = kwSt, c1 = kwCoef1, c2 = kwCoef2;
                double sumK = 0; long nK = 0;
                // MUESTRA A MUESTRA, recordando el canal que toca entre bloques. Antes se recorria por cuadros
                // enteros: si una lectura partia un cuadro (un canal en este bloque y el otro en el siguiente),
                // ese trozo se tiraba y, peor, todo el bloque siguiente se leia corrido un canal: el izquierdo
                // pasaba por el filtro del derecho. Con cuadros enteros la cuenta es la misma que antes, bit a bit.
                int c = kwCanal; if (c >= ch3) c = 0;
                int i = offset, finK = offset + count;
                if (ch3 == 2)
                {
                    // Estereo (casi toda la musica): el cuadro que quedo partido, muestra a muestra; los cuadros enteros de golpe
                    // (KCuadrosEstereo, con los coeficientes y la memoria de los dos filtros en variables); y el trozo de cuadro
                    // del final, muestra a muestra. Las mismas cuentas en el mismo orden: los mismos bits.
                    if (c == 1 && i < finK) { KMuestraAMuestra(buffer, i, i + 1, ch3, ref c, c1, c2, st, ref sumK, ref nK); i++; }
                    int cuadrosK = (finK - i) / 2;
                    if (cuadrosK > 0) { KCuadrosEstereo(buffer, i, cuadrosK, c1, c2, st, ref sumK); nK += 2L * cuadrosK; i += 2 * cuadrosK; }
                }
                if (i < finK) KMuestraAMuestra(buffer, i, finK, ch3, ref c, c1, c2, st, ref sumK, ref nK);
                kwCanal = c;
                curSumSq += sumK; curSamples += nK;   // energia K-pesada (misma escala que el RMS de antes)
            }
            // EQ AUTOMATICO: potencia por banda y canal, solo mientras falta una huella actual.
            if (eqMeasPath != null)
            {
                int ch2 = ch < 1 ? 1 : ch;
                if (eqMeasRate != rate || eqMeasCoef == null || eqMeasSt == null || eqMeasSt.Length != ch2 * 32)
                {
                    eqMeasRate = rate; eqMeasCoef = new float[8][]; eqMeasSt = new float[ch2 * 32];
                    for (int b = 0; b < 8; b++) eqMeasCoef[b] = EqBpf(EqCentros[b], rate);
                    eqPendN = 0;
                }
                if (eqPend == null || eqPend.Length != ch2) { eqPend = new float[ch2]; eqPendN = 0; }
                float[] st = eqMeasSt; double[] sums = eqMeasSum;
                int i = offset, fin = offset + count;
                // Lo mismo con la huella tonal: el cuadro que quedo partido en el bloque anterior se completa
                // con el principio de este, y el que quede partido al final se guarda para el siguiente.
                if (eqPendN > 0)
                {
                    while (eqPendN < ch2 && i < fin) eqPend[eqPendN++] = buffer[i++];
                    if (eqPendN == ch2) { AccumulateEqFrame(eqPend, 0, ch2, eqMeasCoef, st, sums); eqMeasN++; eqPendN = 0; }
                }
                if (eqPendN == 0)
                {
                    // Los cuadros enteros del bloque, banda por banda (HuellaDelTrozo, la del repaso): la misma suma de cada banda en el mismo orden y la
                    // limpieza de restos diminutos (Desnormales.cs) en los mismos cuadros que cuadro a cuadro: 'contados' es el contador de limpieza,
                    // que cuenta cuadros desde la ultima. Mismos bits, y la mitad de tiempo.
                    int cuadros = (fin - i) / ch2;
                    if (cuadros > 0)
                    {
                        HuellaDelTrozoDesde(buffer, i, cuadros, ch2, eqMeasCoef, st, sums, eqLimpia);
                        eqMeasN += cuadros; eqLimpia = (int)((eqLimpia + (long)cuadros) & (Desnormales.Cada - 1));
                        i += cuadros * ch2;
                    }
                    while (i < fin) eqPend[eqPendN++] = buffer[i++];
                }
            }
        }
        private sealed class RedChica
        {
            private readonly object modeloLock = new object();
            public volatile bool Loaded;
            private float[][] W, B;            // pesos (fila-mayor) y sesgos por capa
            private int[] rOut, cIn;           // dimensiones por capa
            private float[] buf1, buf2;        // buffers de trabajo (sin asignar por ventana)
            private readonly int nIn, nOut;    // contrato: cuantas entradas y cuantas salidas debe tener el modelo
            public RedChica(int entradas, int salidas) { nIn = entradas; nOut = salidas; }

            public void TryLoad(string path)
            {
                try
                {
                    if (!File.Exists(path)) return;
                    byte[] d;
                    // Ocho capas de 512x512 caben en menos de 9 MiB. No cargar
                    // archivos arbitrariamente grandes antes de validar el contrato.
                    using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (file.Length < 12 || file.Length > 9 * 1024 * 1024) return;
                        using (BinaryReader input = new BinaryReader(file))
                        {
                            d = input.ReadBytes((int)file.Length);
                            if (d.Length != file.Length) return;
                        }
                    }
                    if (d.Length < 12 || d[0] != (byte)'R' || d[1] != (byte)'I' || d[2] != (byte)'A' || d[3] != (byte)'1') return;
                    int p = 4;
                    int nl = BitConverter.ToInt32(d, p); p += 4;
                    if (nl < 1 || nl > 8) return;
                    float[][] w = new float[nl][], b = new float[nl][];
                    int[] ro = new int[nl], ci = new int[nl]; int maxDim = 0;
                    for (int L = 0; L < nl; L++)
                    {
                        if (d.Length - p < 8) return;
                        int r = BitConverter.ToInt32(d, p); p += 4;
                        int c = BitConverter.ToInt32(d, p); p += 4;
                        if (r < 1 || r > 512 || c < 1 || c > 512) return;
                        if (d.Length - p < (r * c + r) * 4) return;
                        w[L] = new float[r * c]; b[L] = new float[r];
                        for (int i = 0; i < r * c; i++)
                        { float v = BitConverter.ToSingle(d, p); p += 4; if (float.IsNaN(v) || float.IsInfinity(v)) return; w[L][i] = v; }
                        for (int i = 0; i < r; i++)
                        { float v = BitConverter.ToSingle(d, p); p += 4; if (float.IsNaN(v) || float.IsInfinity(v)) return; b[L][i] = v; }
                        ro[L] = r; ci[L] = c; if (r > maxDim) maxDim = r; if (c > maxDim) maxDim = c;
                    }
                    if (p != d.Length || ci[0] != nIn || ro[nl - 1] != nOut) return;   // contrato exacto, sin bytes sobrantes
                    for (int L = 1; L < nl; L++) if (ci[L] != ro[L - 1]) return;
                    float[] work1 = new float[maxDim], work2 = new float[maxDim];
                    lock (modeloLock)
                    {
                        buf1 = work1; buf2 = work2;
                        W = w; B = b; rOut = ro; cIn = ci;
                        Loaded = true;
                    }
                }
                catch { }   // una recarga invalida no destruye el ultimo modelo valido
            }

            // Sin red (la comparacion a ciegas de la IA nueva, si no habia ninguna puesta): vuelve la curva clasica.
            public void Descarga() { lock (modeloLock) { Loaded = false; W = null; B = null; } }

            // y = red(x). Capas ocultas con tanh; la de salida lineal. Devuelve false si no hay modelo.
            public bool Predict(float[] x, float[] y)
            {
                lock (modeloLock)
                {
                    float[][] w = W, b = B;
                    if (!Loaded || w == null || x == null || y == null || x.Length < nIn || y.Length < nOut) return false;
                    for (int i = 0; i < nIn; i++) if (float.IsNaN(x[i]) || float.IsInfinity(x[i])) return false;
                    float[] cur = x, nxt = buf1;
                    int curN = cIn[0];
                    for (int L = 0; L < w.Length; L++)
                    {
                        int r = rOut[L]; float[] wl = w[L], bl = b[L];
                        float[] dst = nxt;   // no publicar una salida parcial si falla otra neurona
                        for (int i = 0; i < r; i++)
                        {
                            float s = bl[i]; int off = i * curN;
                            for (int j = 0; j < curN; j++) s += wl[off + j] * cur[j];
                            if (float.IsNaN(s) || float.IsInfinity(s)) return false;
                            dst[i] = (L == w.Length - 1) ? s : (float)Math.Tanh(s);
                        }
                        cur = dst; curN = r;
                        nxt = (cur == buf1) ? buf2 : buf1;   // alterna buffers (la entrada x no se toca)
                    }
                    for (int i = 0; i < nOut; i++) y[i] = cur[i];
                    return true;
                }
            }
        }
    }
}

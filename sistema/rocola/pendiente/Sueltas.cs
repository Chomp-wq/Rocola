// CLASES SUELTAS QUE LA CADENA NECESITA, sacadas tal cual de la rocola de XP (Rocola/): AudioFftPlan (Optimizaciones.cs), RedChica
// (Form1.Ia.cs, la red del DSEE), FuenteOriginal (AudioControles.cs), GuardadoReciente (Form1.Datos.cs) y MedidaCancion (SinPausa.cs).
using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        private sealed class AudioFftPlan
        {
            internal readonly float[] Window, Cos, Sin, SinNeg;
            internal readonly int[] Reverse;
            internal AudioFftPlan(int size)
            {
                Window = new float[size]; Reverse = new int[size];
                Cos = new float[size / 2]; Sin = new float[size / 2]; SinNeg = new float[size / 2];
                for (int i = 0; i < size; i++) Window[i] = (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / size));
                for (int i = 1, j = 0; i < size; i++)
                {
                    int bit = size >> 1;
                    for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                    j |= bit; Reverse[i] = j;
                }
                for (int k = 0; k < size / 2; k++)
                {
                    Cos[k] = (float)Math.Cos(2.0 * Math.PI * k / size);
                    Sin[k] = (float)Math.Sin(2.0 * Math.PI * k / size);
                    SinNeg[k] = -Sin[k];
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
        // Como maximo un escritor activo y una instantanea pendiente por tabla.
        // El trabajador sigue registrado en el drenaje de cierre mientras queden solicitudes.
        private sealed class GuardadoReciente { public Action Pending; public bool Running; }
    }

    // ==============================================================================================
    //  LA MEDIDA EN VIVO DE UNA CANCION QUE ENTRA POR RELEVO
    //
    //  La sonoridad y la huella tonal se aprenden oyendo, bloque a bloque, en campos de la rocola que son de
    //  "la cancion que se esta leyendo". Con dos canciones seguidas por la misma salida, la de detras empieza
    //  a leerse en el hilo del sonido antes de que la pantalla se entere. Lo que cada una debe medir va aqui,
    //  preparado de antemano (con su memoria ya reservada): en el PRIMER bloque de la nueva se guarda lo medido
    //  de la anterior en 'Cierre' y se estrena lo de esta. La pantalla cierra 'Cierre' cuando llega el relevo.
    // ==============================================================================================
    internal sealed class MedidaCancion
    {
        internal sealed class Cierre
        {
            internal volatile bool Listo;         // lo lleno el hilo del sonido: ya se puede cerrar
            internal object Fuente;
            internal string Ruta; internal double SumSq; internal long Muestras; internal int Tasa, Canales;
            internal string EqRuta; internal double[] EqSumas; internal long EqN; internal int EqTasa;
        }
        internal readonly string Ruta;            // sonoridad que se aprende (null = ninguna)
        internal object Fuente;
        internal readonly string EqRuta;          // huella tonal que se aprende (null = ninguna)
        internal readonly double[] EqSumas = new double[8];
        internal readonly Cierre CierreAnterior = new Cierre();
        internal MedidaCancion(string ruta, string eqRuta) { Ruta = ruta; EqRuta = eqRuta; }
    }
}

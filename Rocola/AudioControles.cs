using System;
namespace Rocola
{
    // NAudio 1.9 expone SubFormat, pero no dwChannelMask. Solo se consulta/fija
    // al construir una etapa; nunca por muestra. No se inventa layout multicanal.
    internal static class FormatoAudio
    {
        private static readonly System.Reflection.FieldInfo campoMascara =
            typeof(NAudio.Wave.WaveFormatExtensible).GetField("dwChannelMask",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        internal static bool Valida(int mascara, int canales)
        {
            if (mascara == 0 || (mascara & ~0x3FFFF) != 0) return false;
            int n = 0; for (int m = mascara; m != 0; m &= m - 1) n++;
            return n == canales;
        }
        internal static int Mascara(NAudio.Wave.WaveFormat formato)
        {
            if (formato == null) return 0;
            NAudio.Wave.WaveFormatExtensible ex = formato as NAudio.Wave.WaveFormatExtensible;
            if (ex != null && campoMascara != null)
            {
                int m = (int)campoMascara.GetValue(ex);
                return Valida(m, formato.Channels) ? m : 0;
            }
            // Los formatos exactos de salida tambien exponen su mascara.
            System.Reflection.PropertyInfo p = formato.GetType().GetProperty("Mascara",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null) { int m = (int)p.GetValue(formato, null); return Valida(m, formato.Channels) ? m : 0; }
            return formato.Channels == 1 ? 4 : (formato.Channels == 2 ? 3 : 0);
        }
        internal static NAudio.Wave.WaveFormat Flotante(int hz, int canales, int mascara)
        {
            if ((canales == 1 && mascara == 4) || (canales == 2 && mascara == 3))
                return NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(hz, canales);
            NAudio.Wave.WaveFormatExtensible f = new NAudio.Wave.WaveFormatExtensible(hz, 32, canales);
            if (campoMascara == null) throw new NotSupportedException("NAudio no conserva la mascara multicanal");
            campoMascara.SetValue(f, Valida(mascara, canales) ? mascara : 0);
            return f;
        }
        internal static NAudio.Wave.WaveFormat Pcm(int hz, int bits, int canales, int mascara)
        {
            if ((canales == 1 && mascara == 4) || (canales == 2 && mascara == 3))
                return new NAudio.Wave.WaveFormat(hz, bits, canales);
            NAudio.Wave.WaveFormatExtensible f = new NAudio.Wave.WaveFormatExtensible(hz, bits, canales);
            if (campoMascara == null) throw new NotSupportedException("NAudio no conserva la mascara multicanal");
            campoMascara.SetValue(f, Valida(mascara, canales) ? mascara : 0);
            return f;
        }
        internal static NAudio.Wave.WaveFormat DeArchivo(NAudio.Wave.WaveFormat f, string ruta)
        {
            int mascara = Mascara(f);
            if (ruta != null && ruta.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                int tipo, canales, m;
                if (DisposicionBs1770.LeeFmtWav(ruta, out tipo, out canales, out m) && canales == f.Channels)
                    mascara = Valida(m, canales) ? m : (tipo == 0xFFFE ? 0 : (canales <= 2 ? Mascara(f) : 0));
            }
            return Flotante(f.SampleRate, f.Channels, mascara);
        }
        internal static bool[] Musicales(NAudio.Wave.WaveFormat f)
        {
            int mascara = Mascara(f), ch = Math.Max(1, f.Channels);
            bool[] p = new bool[ch];
            if (mascara == 0) { for (int c = 0; c < ch; c++) p[c] = true; return p; }
            int i = 0;
            for (int bit = 1; i < ch && bit <= 0x20000; bit <<= 1)
                if ((mascara & bit) != 0) p[i++] = bit != DisposicionBs1770.LFE;
            return p;
        }
    }


    // WAV PCM/float no necesita ACM, tampoco con WAVE_FORMAT_EXTENSIBLE.
    // NAudio 1.9 lee ese fmt como WaveFormatExtraData: ToStandardWaveFormat no lo normaliza.
    // Solo se adapta la descripcion; bytes, precision, canales y orden siguen intactos.
    internal sealed class WavDirecto : NAudio.Wave.WaveStream
    {
        private static readonly Guid pcm = new Guid("00000001-0000-0010-8000-00aa00389b71");
        private static readonly Guid ieee = new Guid("00000003-0000-0010-8000-00aa00389b71");
        private readonly NAudio.Wave.WaveFileReader lector;
        private readonly System.IO.Stream archivo;
        private readonly NAudio.Wave.WaveFormat formato;
        private readonly object sincroniza = new object();
        private readonly byte[] cuadro;
        private int pendiente, fin;
        private bool cerrado;

        internal static NAudio.Wave.WaveStream Abre(string ruta)
        {
            if (ruta == null || !ruta.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return null;
            System.IO.Stream a = null;
            NAudio.Wave.WaveFileReader r = null;
            try
            {
                // Poseer el archivo desde antes de construir el lector: cerrar tambien si falla la cabecera.
                a = System.IO.File.OpenRead(ruta);
                r = new NAudio.Wave.WaveFileReader(a);
                NAudio.Wave.WaveFormat f = Estandar(r.WaveFormat);
                if (f == null) return null; // WAV comprimido: conservar el camino de codecs anterior.
                WavDirecto resultado = new WavDirecto(r, a, f);
                r = null; a = null;
                return resultado;
            }
            finally
            {
                try { if (r != null) r.Dispose(); }
                finally { if (a != null) a.Dispose(); }
            }
        }
        private static NAudio.Wave.WaveFormat Estandar(NAudio.Wave.WaveFormat f)
        {
            NAudio.Wave.WaveFormatEncoding tipo = f.Encoding;
            int bits = f.BitsPerSample;
            if (tipo == NAudio.Wave.WaveFormatEncoding.Extensible)
            {
                NAudio.Wave.WaveFormatExtraData ex = f as NAudio.Wave.WaveFormatExtraData;
                if (ex == null || f.ExtraSize < 22 || ex.ExtraData.Length < 22)
                    throw new System.IO.InvalidDataException("Cabecera WAV extensible incompleta");
                byte[] guid = new byte[16];
                Buffer.BlockCopy(ex.ExtraData, 6, guid, 0, 16);
                Guid sub = new Guid(guid);
                if (sub == pcm) tipo = NAudio.Wave.WaveFormatEncoding.Pcm;
                else if (sub == ieee) tipo = NAudio.Wave.WaveFormatEncoding.IeeeFloat;
                else return null; // No confundir otros subformatos con PCM por el numero de bits.
                int validos = BitConverter.ToUInt16(ex.ExtraData, 0);
                if (validos < 1 || validos > bits ||
                    (tipo == NAudio.Wave.WaveFormatEncoding.IeeeFloat && validos != bits))
                    throw new System.IO.InvalidDataException("Precision WAV extensible invalida");
                // PCM de 20/24 bits en un contenedor mayor va alineado a la izquierda.
                // El conversor PCM del contenedor conserva la amplitud; no desplazar ni renormalizar.
            }
            bool entero = tipo == NAudio.Wave.WaveFormatEncoding.Pcm;
            bool flotante = tipo == NAudio.Wave.WaveFormatEncoding.IeeeFloat;
            if (!entero && !flotante) return null;
            if ((entero && bits != 8 && bits != 16 && bits != 24 && bits != 32) ||
                (flotante && bits != 32 && bits != 64)) return null;
            int alineacion = (bits / 8) * f.Channels;
            if (f.SampleRate < 1 || f.Channels < 1 || alineacion < 1 ||
                f.BlockAlign != alineacion || (long)f.SampleRate * alineacion != f.AverageBytesPerSecond)
                throw new System.IO.InvalidDataException("Alineacion WAV invalida");
            return NAudio.Wave.WaveFormat.CreateCustomFormat(tipo, f.SampleRate, f.Channels,
                f.AverageBytesPerSecond, f.BlockAlign, bits);
        }
        private WavDirecto(NAudio.Wave.WaveFileReader lector, System.IO.Stream archivo, NAudio.Wave.WaveFormat formato)
        {
            if (lector.Length % formato.BlockAlign != 0 || lector.Length > archivo.Length - archivo.Position)
                throw new System.IO.InvalidDataException("Datos WAV truncados o cuadro incompleto");
            this.lector = lector; this.archivo = archivo; this.formato = formato;
            cuadro = new byte[formato.BlockAlign]; // Un cuadro, no la cancion entera ni un buffer por lectura.
        }
        private void Abierto() { if (cerrado) throw new ObjectDisposedException("WavDirecto"); }
        public override NAudio.Wave.WaveFormat WaveFormat { get { return formato; } }
        public override long Length { get { return lector.Length; } }
        public override long Position
        {
            get { lock (sincroniza) { Abierto(); return lector.Position - (fin - pendiente); } }
            set { lock (sincroniza) { Abierto(); lector.Position = Math.Max(0L, value); pendiente = fin = 0; } }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset > buffer.Length || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException("count");
            lock (sincroniza)
            {
                Abierto();
                int n = Math.Min(count, fin - pendiente);
                if (n > 0) { Buffer.BlockCopy(cuadro, pendiente, buffer, offset, n); pendiente += n; }
                int alineacion = formato.BlockAlign;
                int completos = (count - n) / alineacion * alineacion;
                if (completos > 0)
                {
                    int leidos = lector.Read(buffer, offset + n, completos);
                    if (leidos % alineacion != 0) throw new System.IO.EndOfStreamException("Cuadro WAV truncado");
                    n += leidos;
                    if (leidos < completos) return n;
                }
                if (n < count)
                {
                    int leidos = lector.Read(cuadro, 0, alineacion);
                    if (leidos == 0) return n;
                    if (leidos != alineacion) throw new System.IO.EndOfStreamException("Cuadro WAV truncado");
                    int toma = count - n;
                    Buffer.BlockCopy(cuadro, 0, buffer, offset + n, toma);
                    pendiente = toma; fin = alineacion; n += toma;
                }
                return n;
            }
        }
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing) lock (sincroniza)
                {
                    if (!cerrado)
                    {
                        cerrado = true;
                        try { lector.Dispose(); } finally { archivo.Dispose(); }
                    }
                }
            }
            finally { base.Dispose(disposing); }
        }
    }

    public partial class Form1
    {
        private static void SaltaCuadro(NAudio.Wave.WaveStream lector, TimeSpan donde)
        {
            long cuadros = Math.Max(0L, (long)Math.Round(donde.TotalSeconds * lector.WaveFormat.SampleRate));
            lector.Position = Math.Min(lector.Length, cuadros * lector.WaveFormat.BlockAlign);
        }
        // WaveStream.CurrentTime usa FromSeconds y en CLR 2 redondea a milisegundos.
        // Para mapas musicales/seek no perder hasta 48 cuadros: convertir posiciones a ticks.
        private static TimeSpan TiempoLector(NAudio.Wave.WaveStream lector, bool total)
        {
            long bytes = total ? lector.Length : lector.Position;
            long cuadros = bytes / lector.WaveFormat.BlockAlign;
            return TimeSpan.FromTicks((long)Math.Round((double)cuadros * TimeSpan.TicksPerSecond / lector.WaveFormat.SampleRate));
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
        private static void LimpiaMuestras(float[] buffer, int offset, int n)
        {
            for (int i = offset; i < offset + n; i++)
            { float x = buffer[i]; if (!(x >= -4f && x <= 4f)) buffer[i] = 0f; }
        }
        private void MideEntradaOriginal(LevelTap tap, float[] buffer, int offset, int n)
        {
            lock (audioDatosLock)
            {
                if (tap.Publicacion != publicacionAudio) return;
                MedidaDe(tap);
                MideBloqueOriginal(buffer, offset, n, tap.WaveFormat.SampleRate, tap.WaveFormat.Channels);
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
                for (int i = offset; i < offset + count; i++)
                {
                    int b = c * 8; float x = buffer[i];
                    float y1 = c1[0]*x + c1[1]*st[b] + c1[2]*st[b+1] - c1[3]*st[b+2] - c1[4]*st[b+3];
                    st[b+1]=st[b]; st[b]=x; st[b+3]=st[b+2]; st[b+2]=y1;
                    float y2 = c2[0]*y1 + c2[1]*st[b+4] + c2[2]*st[b+5] - c2[3]*st[b+6] - c2[4]*st[b+7];
                    st[b+5]=st[b+4]; st[b+4]=y1; st[b+7]=st[b+6]; st[b+6]=y2;
                    sumK += y2 * y2; nK++;
                    if (++c >= ch3) c = 0;
                }
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
                    for (; i + ch2 <= fin; i += ch2)
                    {
                        AccumulateEqFrame(buffer, i, ch2, eqMeasCoef, st, sums);
                        eqMeasN++;
                    }
                    while (i < fin) eqPend[eqPendN++] = buffer[i++];
                }
            }
        }

        // Potencia media de los canales, NO potencia de su mezcla: evita cancelar
        // instrumentos abiertos/en contrafase. Estados independientes; cero reservas.
        private static void AccumulateEqFrame(float[] buffer, int offset, int channels,
            float[][] coefficients, float[] state, double[] sums)
        {
            double weight = 1.0 / channels;
            for (int c = 0; c < channels; c++)
            {
                float input = buffer[offset + c];
                for (int b = 0; b < 8; b++)
                {
                    float[] cf = coefficients[b]; if (cf == null) continue;
                    int q = c * 32 + b * 4;
                    float output = cf[0]*input + cf[1]*state[q] + cf[2]*state[q+1] - cf[3]*state[q+2] - cf[4]*state[q+3];
                    state[q+1]=state[q]; state[q]=input; state[q+3]=state[q+2]; state[q+2]=output;
                    sums[b] += (output * output) * weight;
                }
            }
        }
        private bool HasStereoEq(string path)
        {
            System.Collections.Generic.Dictionary<string, float[]> eqCache = lecturaPlan != null ? lecturaPlan.Eq : this.eqCache;
            float[] curve;
            return path != null && eqCache.TryGetValue(path, out curve) &&
                curve != null && curve.Length > EqFingerprint && curve[EqFingerprint] == EqVersionOriginal;
        }
        private bool NeedsStereoEqRefresh(string path)
        {
            System.Collections.Generic.Dictionary<string, float[]> eqCache = lecturaPlan != null ? lecturaPlan.Eq : this.eqCache;
            // Una duracion sin huella puede corresponder a un archivo demasiado corto.
            // No volver a decodificarlo para siempre: migrar solo huellas existentes.
            return eqCache.ContainsKey(path) && !HasStereoEq(path);
        }
        // Valores calculados al construir la etapa, nunca trigonometria ni reservas por muestra.
        internal static float ExciterPole(int rate)
        {
            if (rate <= 0 || rate == 44100) return .55f;
            return (float)(1 - Math.Pow(.45, 44100.0 / rate));
        }
        private partial class LevelTap
        {
            private struct AudioRamp
            {
                internal float Current, Target, Delta;
                internal int Remaining;
                internal void Init(float value) { Current=Target=value; Delta=0; Remaining=0; }
                internal void Aim(float value, int frames)
                {
                    if(value==Target)return;
                    Target=value;Remaining=frames;Delta=(Target-Current)/frames;
                }
                internal void Step()
                {
                    if(Remaining<=0)return;
                    Current+=Delta;
                    if(--Remaining==0)Current=Target;
                }
            }
            private readonly int audioChannels, rampFrames, eqFadeFrames;
            private readonly float exciterPole;
            private readonly bool[] canalesMusicales;
            internal bool MideOriginal;
            private AudioRamp gainRamp, exciteRamp;
            private bool audioReady;
            private int audioChannel, eqFadeLeft;
            private float eqMix=1f;
            private PreparedEq currentEq, previousEq;

            private static bool SameEq(PreparedEq a, PreparedEq b)
            { return ReferenceEquals(a,b) || (a!=null && b!=null && ReferenceEquals(a.Curve,b.Curve)); }

            private void AdvanceAudioControls(float gain, float excite, PreparedEq requested)
            {
                gainRamp.Aim(gain,rampFrames);gainRamp.Step();
                if(exciteRamp.Current==0 && excite>0)Array.Clear(exLp,0,exLp.Length);
                exciteRamp.Aim(excite,rampFrames);exciteRamp.Step();
                // Solo dos cascadas como maximo. Un cambio durante la mezcla espera su fin;
                // se toma la ultima peticion, no se acumulan filtros ni estados.
                if(eqFadeLeft==0 && !SameEq(currentEq,requested))
                {
                    previousEq=currentEq;currentEq=requested;eqFadeLeft=eqFadeFrames;
                }
                if(eqFadeLeft>0)
                {
                    eqMix=(eqFadeFrames-eqFadeLeft+1f)/eqFadeFrames;
                    if(--eqFadeLeft==0){eqMix=1;previousEq=null;}
                }
            }
            private static float ApplyPreparedEq(PreparedEq eq, float input, int channel)
            {
                if(eq==null)return input;
                float[] state=eq.State;
                for(int b=0;b<eq.Active;b++)
                {
                    float[] cf=eq.Coefficients[b];int q=channel*32+b*4;
                    float output=cf[0]*input+cf[1]*state[q]+cf[2]*state[q+1]-cf[3]*state[q+2]-cf[4]*state[q+3];
                    state[q+1]=state[q];state[q]=input;state[q+3]=state[q+2];state[q+2]=output;
                    input=output;
                }
                return input;
            }
        }
    }
}

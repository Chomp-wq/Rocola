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
        private int kwLimpia, eqLimpia;   // cuadros desde la ultima limpieza de restos diminutos (Desnormales.cs)
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

        // LA SONORIDAD K DEL APRENDIZAJE EN VIVO, por trozos (20-10-2026). Es la cuenta de siempre de MideBloqueOriginal -dos biquads por canal, la suma
        // de los cuadrados de la salida del segundo, la limpieza de restos diminutos (Desnormales.cs) detras del cuadro que cuenta 64- en dos formas:
        // muestra a muestra con la memoria en el arreglo (el cuadro partido de los extremos, el mono y los canales de mas) y, para los cuadros
        // enteros de estereo, con coeficientes y memoria en variables. Mismas cuentas en el mismo orden: mismos bits (bateria entradaligera).
        private void KMuestraAMuestra(float[] buffer, int desde, int hasta, int ch3, ref int c, float[] c1, float[] c2, float[] st, ref double sumK, ref long nK)
        {
            double suma = sumK; long cuenta = nK; int canal = c;
            for (int i = desde; i < hasta; i++)
            {
                int b = canal * 8; float x = buffer[i];
                float y1 = c1[0]*x + c1[1]*st[b] + c1[2]*st[b+1] - c1[3]*st[b+2] - c1[4]*st[b+3];
                st[b+1]=st[b]; st[b]=x; st[b+3]=st[b+2]; st[b+2]=y1;
                float y2 = c2[0]*y1 + c2[1]*st[b+4] + c2[2]*st[b+5] - c2[3]*st[b+6] - c2[4]*st[b+7];
                st[b+5]=st[b+4]; st[b+4]=y1; st[b+7]=st[b+6]; st[b+6]=y2;
                suma += y2 * y2; cuenta++;
                if (++canal >= ch3)
                {
                    canal = 0;
                    if (++kwLimpia == Desnormales.Cada) { kwLimpia = 0; Desnormales.Limpia(st); }   // restos diminutos a cero (Desnormales.cs)
                }
            }
            sumK = suma; nK = cuenta; c = canal;
        }
        // Cuadros ENTEROS de estereo (llega con el canal 0 por tocar y lo deja en el 0). Los tramos acaban en el cuadro que cuenta 64.
        private void KCuadrosEstereo(float[] buffer, int desde, int cuadros, float[] c1, float[] c2, float[] st, ref double sumK)
        {
            float p0 = c1[0], p1 = c1[1], p2 = c1[2], p3 = c1[3], p4 = c1[4];
            float q0 = c2[0], q1 = c2[1], q2 = c2[2], q3 = c2[3], q4 = c2[4];
            double suma = sumK;
            float lx1 = st[0], lx2 = st[1], ly1 = st[2], ly2 = st[3], lz1 = st[4], lz2 = st[5], lw1 = st[6], lw2 = st[7];
            float rx1 = st[8], rx2 = st[9], ry1 = st[10], ry2 = st[11], rz1 = st[12], rz2 = st[13], rw1 = st[14], rw2 = st[15];
            int f = 0, limpia = kwLimpia;
            while (f < cuadros)
            {
                int fin = f + (Desnormales.Cada - limpia); bool cierra = true;
                if (fin > cuadros) { fin = cuadros; cierra = false; }
                limpia += fin - f;
                for (int i = desde + f * 2; f < fin; f++, i += 2)
                {
                    float x = buffer[i];
                    float y1 = p0 * x + p1 * lx1 + p2 * lx2 - p3 * ly1 - p4 * ly2;
                    lx2 = lx1; lx1 = x; ly2 = ly1; ly1 = y1;
                    float y2 = q0 * y1 + q1 * lz1 + q2 * lz2 - q3 * lw1 - q4 * lw2;
                    lz2 = lz1; lz1 = y1; lw2 = lw1; lw1 = y2;
                    suma += y2 * y2;
                    float x2 = buffer[i + 1];
                    float u1 = p0 * x2 + p1 * rx1 + p2 * rx2 - p3 * ry1 - p4 * ry2;
                    rx2 = rx1; rx1 = x2; ry2 = ry1; ry1 = u1;
                    float u2 = q0 * u1 + q1 * rz1 + q2 * rz2 - q3 * rw1 - q4 * rw2;
                    rz2 = rz1; rz1 = u1; rw2 = rw1; rw1 = u2;
                    suma += u2 * u2;
                }
                if (cierra)
                {
                    limpia = 0;
                    lx1 = Desnormales.Limpia(lx1); lx2 = Desnormales.Limpia(lx2); ly1 = Desnormales.Limpia(ly1); ly2 = Desnormales.Limpia(ly2);
                    lz1 = Desnormales.Limpia(lz1); lz2 = Desnormales.Limpia(lz2); lw1 = Desnormales.Limpia(lw1); lw2 = Desnormales.Limpia(lw2);
                    rx1 = Desnormales.Limpia(rx1); rx2 = Desnormales.Limpia(rx2); ry1 = Desnormales.Limpia(ry1); ry2 = Desnormales.Limpia(ry2);
                    rz1 = Desnormales.Limpia(rz1); rz2 = Desnormales.Limpia(rz2); rw1 = Desnormales.Limpia(rw1); rw2 = Desnormales.Limpia(rw2);
                }
            }
            st[0] = lx1; st[1] = lx2; st[2] = ly1; st[3] = ly2; st[4] = lz1; st[5] = lz2; st[6] = lw1; st[7] = lw2;
            st[8] = rx1; st[9] = rx2; st[10] = ry1; st[11] = ry2; st[12] = rz1; st[13] = rz2; st[14] = rw1; st[15] = rw2;
            kwLimpia = limpia; sumK = suma;
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
        // LA HUELLA DE UN TROZO ENTERO (13-10-2026). Lo mismo que AccumulateEqFrame cuadro a cuadro, pero banda por banda:
        // cada banda es un filtro aparte, asi que se le pueden pasar todos los cuadros del trozo seguidos, con sus
        // coeficientes y su memoria en variables y no en arreglos. La suma de cada banda recibe lo mismo y en el mismo orden
        // (cuadro a cuadro, el izquierdo antes que el derecho), y la limpieza de restos diminutos (Desnormales.cs) cae en
        // los mismos cuadros que antes: cada 64 contados desde el principio de la cancion ('contados' = los que iban antes
        // de este trozo). Mismos numeros, bit a bit, en la mitad de tiempo: es lo que mas pesa del repaso a fondo.
        // Con mas de dos canales, cuadro a cuadro como siempre.
        private static void HuellaDelTrozo(float[] buffer, int cuadros, int channels, float[][] coefficients, float[] state, double[] sums, long contados)
        { HuellaDelTrozoDesde(buffer, 0, cuadros, channels, coefficients, state, sums, contados); }
        // Lo mismo desde la posicion 'desde' del arreglo (el aprendizaje en vivo de MideBloqueOriginal lee de en medio de un bloque).
        private static void HuellaDelTrozoDesde(float[] buffer, int desde, int cuadros, int channels, float[][] coefficients, float[] state, double[] sums, long contados)
        {
            const int m = Desnormales.Cada - 1;
            if (channels != 1 && channels != 2)
            {
                for (int f = 0; f < cuadros; f++)
                {
                    AccumulateEqFrame(buffer, desde + f * channels, channels, coefficients, state, sums);
                    if (((contados + f + 1) & m) == 0) Desnormales.Limpia(state);
                }
                return;
            }
            double weight = 1.0 / channels;
            for (int b = 0; b < 8; b++)
            {
                float[] cf = coefficients[b]; if (cf == null) continue;
                float k0 = cf[0], k1 = cf[1], k2 = cf[2], k3 = cf[3], k4 = cf[4];
                int qa = b * 4, qb = 32 + b * 4;
                float a0 = state[qa], a1 = state[qa + 1], a2 = state[qa + 2], a3 = state[qa + 3];
                double suma = sums[b];
                if (channels == 1)
                {
                    for (int f = 0; f < cuadros; f++)
                    {
                        float x = buffer[desde + f];
                        float o = k0 * x + k1 * a0 + k2 * a1 - k3 * a2 - k4 * a3;
                        a1 = a0; a0 = x; a3 = a2; a2 = o;
                        suma += (o * o) * weight;
                        if (((contados + f + 1) & m) == 0)
                        { a0 = Desnormales.Limpia(a0); a1 = Desnormales.Limpia(a1); a2 = Desnormales.Limpia(a2); a3 = Desnormales.Limpia(a3); }
                    }
                }
                else
                {
                    float b0 = state[qb], b1 = state[qb + 1], b2 = state[qb + 2], b3 = state[qb + 3];
                    for (int f = 0, i = desde; f < cuadros; f++, i += 2)
                    {
                        float x = buffer[i];
                        float o = k0 * x + k1 * a0 + k2 * a1 - k3 * a2 - k4 * a3;
                        a1 = a0; a0 = x; a3 = a2; a2 = o;
                        suma += (o * o) * weight;
                        float x2 = buffer[i + 1];
                        float o2 = k0 * x2 + k1 * b0 + k2 * b1 - k3 * b2 - k4 * b3;
                        b1 = b0; b0 = x2; b3 = b2; b2 = o2;
                        suma += (o2 * o2) * weight;
                        if (((contados + f + 1) & m) == 0)
                        {
                            a0 = Desnormales.Limpia(a0); a1 = Desnormales.Limpia(a1); a2 = Desnormales.Limpia(a2); a3 = Desnormales.Limpia(a3);
                            b0 = Desnormales.Limpia(b0); b1 = Desnormales.Limpia(b1); b2 = Desnormales.Limpia(b2); b3 = Desnormales.Limpia(b3);
                        }
                    }
                    state[qb] = b0; state[qb + 1] = b1; state[qb + 2] = b2; state[qb + 3] = b3;
                }
                state[qa] = a0; state[qa + 1] = a1; state[qa + 2] = a2; state[qa + 3] = a3;
                sums[b] = suma;
            }
            // Las posiciones del arreglo que no son de ninguna banda con coeficientes (bandas nulas, canales de mas) se
            // limpiaban igual: siguen en cero, que es como empiezan.
        }
        // LA SONORIDAD K Y LOS RECORTES DE UN TROZO (13-10-2026), fuera del bucle de cada cuadro de MeasureFile: la misma
        // cuenta, cuadro a cuadro y canal a canal (la suma es una para todos los canales: su orden no se toca), con los
        // coeficientes en variables. La limpieza de restos (Desnormales.cs) va ANTES del cuadro que hace un multiplo de 64
        // contados, como iba en el bucle. Mismos numeros, bit a bit.
        private static void SonoridadDelTrozo(float[] buf, int cuadros, int ch, float[] c1, float[] c2, float[] st,
                                              ref double sumSq, ref long nSamp, ref long clipN, int[] clipRun, long contados)
        {
            const int m = Desnormales.Cada - 1;
            float p0 = c1[0], p1 = c1[1], p2 = c1[2], p3 = c1[3], p4 = c1[4];
            float q0 = c2[0], q1 = c2[1], q2 = c2[2], q3 = c2[3], q4 = c2[4];
            double suma = sumSq; long muestras = nSamp, recortes = clipN;
            for (int f = 0, i = 0; f < cuadros; f++, i += ch)
            {
                if (((contados + f + 1) & m) == 0) Desnormales.Limpia(st);
                for (int c = 0; c < ch; c++)
                {
                    int b = c * 8; float x = buf[i + c];
                    float y1 = p0 * x + p1 * st[b] + p2 * st[b + 1] - p3 * st[b + 2] - p4 * st[b + 3];
                    st[b + 1] = st[b]; st[b] = x; st[b + 3] = st[b + 2]; st[b + 2] = y1;
                    float y2 = q0 * y1 + q1 * st[b + 4] + q2 * st[b + 5] - q3 * st[b + 6] - q4 * st[b + 7];
                    st[b + 5] = st[b + 4]; st[b + 4] = y1; st[b + 7] = st[b + 6]; st[b + 6] = y2;
                    suma += y2 * y2; muestras++;
                    float ax = x < 0f ? -x : x;
                    if (ax >= 0.985f) { clipRun[c]++; if (clipRun[c] == 3) recortes += 3; else if (clipRun[c] > 3) recortes++; }
                    else clipRun[c] = 0;
                }
            }
            sumSq = suma; nSamp = muestras; clipN = recortes;
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
            private int limpia;   // cuadros desde la ultima limpieza de restos diminutos (Desnormales.cs)
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
                // En el silencio, los restos diminutos del EQ y del realce a cero (Desnormales.cs): cada 64 cuadros.
                if(++limpia==Desnormales.Cada)
                {
                    limpia=0;
                    if(currentEq!=null)Desnormales.Limpia(currentEq.State);
                    if(previousEq!=null)Desnormales.Limpia(previousEq.State);
                    Desnormales.Limpia(exLp);
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

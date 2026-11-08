// EL LECTOR DE MP3 DE LA ROCOLA DE XP: Mp3FileReader de NAudio 1.9, traducido (traduccion de XP, 7-11-2026).
// La rocola de XP lee los MP3 con Mp3FileReader de NAudio 1.9 (y el descompresor ACM de Windows). En Linux va NAudio 3 (NAudio.Core), y su
// lector (Mp3FileReaderBase) ya no hace lo mismo:
//   - dos saltos en menos de 30 ms lo ponen en «modo de arrastre»: durante 50 ms cada lectura devuelve SILENCIO como si fuera la cancion, y
//     sin avanzar (medido con rocola-xp --prueba-aot y --prueba-mp3: los bytes leidos, todos en cero). Cualquier camino de XP que salte dos
//     veces seguidas leeria silencio; en el repaso no se noto (sus tramos van separados por 20 s de lectura: mismas medidas);
//   - el indice de cuadros lo hace poco a poco mientras lee: al abrir, Length (la duracion) puede ser una estimacion que cambia despues, y un
//     cuadro con otra frecuencia salta a media cancion en vez de al abrir (XP lo resuelve al abrir: el camino tolerante de Form1.Sonido);
//   - acepta un cuadro solo si el siguiente tambien lo parece (en cada cuadro lee dos cabeceras por delante) y salta etiquetas ID3 en medio:
//     un cuadro bueno antes de un trozo roto se pierde, y el ultimo antes de una etiqueta APE larga tambien;
//   - el salto al final deja el indice en el ultimo cuadro (1.9 lo deja donde estaba) y CurrentTime se alinea a muestras enteras.
// Este es el de NAudio 1.9 linea por linea: el indice entero al abrir, la cuenta de Read y de Position (sobrantes, cuadros dobles, 3 cuadros
// de calentamiento al saltar), la etiqueta ID3v1, el arreglo del segundo cuadro y los mismos mensajes, con los mismos nombres de campos que
// en NAudio 1.9 (Mp3Ligero.cs los busca por reflexion; ver Aot.cs). Los cuadros que se descomprimen siguen siendo Mp3Frame de NAudio 3 (asi
// los reciben libmpg123 y NLayer), pero cada cuadro se busca como en 1.9 (la cabecera se valida como 1.9, byte a byte) y solo despues se le
// da a NAudio 3 ese cuadro y nada mas: sus comprobaciones de mas no cambian cual es el cuadro.
using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    public class Mp3FileReader : WaveStream
    {
        private readonly WaveFormat waveFormat;
        private Stream mp3Stream;
        private readonly long mp3DataLength;
        private readonly long dataStartPosition;

        // El formato MP3 (no el de salida: ese es WaveFormat)
        public Mp3WaveFormat Mp3WaveFormat { get; private set; }

        private readonly XingHeader xingHeader;
        private readonly bool ownInputStream;

        private List<Mp3Index> tableOfContents;
        private int tocIndex;

        private long totalSamples;
        private readonly int bytesPerSample;
        private readonly int bytesPerDecodedFrame;

        private IMp3FrameDecompressor decompressor;

        private readonly byte[] decompressBuffer;
        private int decompressBufferOffset;
        private int decompressLeftovers;
        private bool repositionedFlag;

        private long position;   // donde va lo descomprimido

        private readonly object repositionLock = new object();

        // (de la traduccion) el cuadro que se le da a NAudio 3, y la lectura con Span hecha con la de siempre
        private readonly UnCuadro cuadro = new UnCuadro();
        private readonly Func<byte[], int, int, int> leeArreglo;

        public Mp3FileReader(string mp3FileName) : this(File.OpenRead(mp3FileName), CreateAcmFrameDecompressor, true) { }
        public Mp3FileReader(string mp3FileName, FrameDecompressorBuilder frameDecompressorBuilder) : this(File.OpenRead(mp3FileName), frameDecompressorBuilder, true) { }
        // Desde un flujo: NO lo cierra al acabar
        public Mp3FileReader(Stream inputStream) : this(inputStream, CreateAcmFrameDecompressor, false) { }
        public Mp3FileReader(Stream inputStream, FrameDecompressorBuilder frameDecompressorBuilder) : this(inputStream, frameDecompressorBuilder, false) { }

        private Mp3FileReader(Stream inputStream, FrameDecompressorBuilder frameDecompressorBuilder, bool ownInputStream)
        {
            if (inputStream == null) throw new ArgumentNullException("inputStream");
            if (frameDecompressorBuilder == null) throw new ArgumentNullException("frameDecompressorBuilder");
            leeArreglo = Read;
            this.ownInputStream = ownInputStream;
            try
            {
                mp3Stream = inputStream;
                Id3v2Tag = LeeId3v2(mp3Stream);

                dataStartPosition = mp3Stream.Position;
                long donde;
                Mp3Frame firstFrame = CargaCuadro(mp3Stream, true, out donde);
                if (firstFrame == null)
                    throw new InvalidDataException("Invalid MP3 file - no MP3 Frames Detected");
                double bitRate = firstFrame.BitRate;
                xingHeader = LoadXingHeader(firstFrame);
                // si hay cabecera Xing, se salta al descomprimir
                if (xingHeader != null) dataStartPosition = mp3Stream.Position;

                // el arreglo de NAudio para los archivos que dicen cambiar de frecuencia en el segundo cuadro
                Mp3Frame secondFrame = CargaCuadro(mp3Stream, true, out donde);
                if (secondFrame != null &&
                    (secondFrame.SampleRate != firstFrame.SampleRate ||
                     secondFrame.ChannelMode != firstFrame.ChannelMode))
                {
                    // el primero era una cabecera VBR/LAME que no se reconocio: el de verdad es el segundo
                    dataStartPosition = donde;
                    firstFrame = secondFrame;
                }

                mp3DataLength = mp3Stream.Length - dataStartPosition;

                // la etiqueta ID3v1 del final
                mp3Stream.Position = mp3Stream.Length - 128;
                byte[] tag = new byte[128];
#pragma warning disable CA2022   // (1.9 la lee asi, de una sola vez)
                mp3Stream.Read(tag, 0, 128);
#pragma warning restore CA2022
                if (tag[0] == 'T' && tag[1] == 'A' && tag[2] == 'G')
                {
                    Id3v1Tag = tag;
                    mp3DataLength -= 128;
                }

                mp3Stream.Position = dataStartPosition;

                // un formato provisional antes de saber la tasa de bits de verdad
                Mp3WaveFormat = new Mp3WaveFormat(firstFrame.SampleRate,
                    firstFrame.ChannelMode == ChannelMode.Mono ? 1 : 2, firstFrame.FrameLength, (int)bitRate);

                CreateTableOfContents();
                tocIndex = 0;

                // [kbit/s] = [kbits] / [s] = [bits] / [ms] (en sonido, 1 kbit = 1000 bits), en doble para no redondear
                bitRate = (mp3DataLength * 8.0 / TotalSeconds());

                mp3Stream.Position = dataStartPosition;

                // ya con la tasa de bits de verdad
                Mp3WaveFormat = new Mp3WaveFormat(firstFrame.SampleRate,
                    firstFrame.ChannelMode == ChannelMode.Mono ? 1 : 2, firstFrame.FrameLength, EnteroComoXp(bitRate));
                decompressor = frameDecompressorBuilder(Mp3WaveFormat);
                waveFormat = decompressor.OutputFormat;
                bytesPerSample = (decompressor.OutputFormat.BitsPerSample) / 8 * decompressor.OutputFormat.Channels;
                // ningun cuadro MP3 tiene mas de 1152 muestras
                bytesPerDecodedFrame = 1152 * bytesPerSample;
                // algunos MP3 los dan dobles
                decompressBuffer = new byte[bytesPerDecodedFrame * 2];
            }
            catch (Exception)
            {
                if (ownInputStream) inputStream.Dispose();
                throw;
            }
        }

        // Lo que arma el descompresor de cuadros
        public delegate IMp3FrameDecompressor FrameDecompressorBuilder(WaveFormat mp3Format);

        // El descompresor «de Windows» (en XP, el ACM): aqui libmpg123, la biblioteca de MP3 del sistema, que como el ACM entrega enteros de
        // 16 bits cuadro a cuadro (NAudioXp.cs). Sin libmpg123, NLayer.
        public static IMp3FrameDecompressor CreateAcmFrameDecompressor(WaveFormat mp3Format)
        {
            IMp3FrameDecompressor d = DescompresorMpg123.Crea(mp3Format);
            return d ?? new DescompresorNLayer16(mp3Format);
        }

        private void CreateTableOfContents()
        {
            try
            {
                // una idea de cuantos cuadros hay (400 bytes por cuadro) para que la lista casi no crezca
                tableOfContents = new List<Mp3Index>((int)(mp3DataLength / 400));
                Cabecera frame;
                bool hay;
                do
                {
                    Mp3Index index = new Mp3Index();
                    index.FilePosition = mp3Stream.Position;
                    index.SamplePosition = totalSamples;
                    hay = ReadNextHeader(out frame);
                    if (hay)
                    {
                        ValidateFrameFormat(frame);

                        totalSamples += frame.SampleCount;
                        index.SampleCount = frame.SampleCount;
                        index.ByteCount = (int)(mp3Stream.Position - index.FilePosition);
                        tableOfContents.Add(index);
                    }
                } while (hay);
            }
            catch (EndOfStreamException)
            {
                // no tiene por que ser un problema
            }
        }

        private void ValidateFrameFormat(Cabecera frame)
        {
            if (frame.SampleRate != Mp3WaveFormat.SampleRate)
            {
                string message =
                    String.Format(
                        "Got a frame at sample rate {0}, in an MP3 with sample rate {1}. Mp3FileReader does not support sample rate changes.",
                        frame.SampleRate, Mp3WaveFormat.SampleRate);
                throw new InvalidOperationException(message);
            }
            int channels = frame.ChannelMode == ChannelMode.Mono ? 1 : 2;
            if (channels != Mp3WaveFormat.Channels)
            {
                string message =
                    String.Format(
                        "Got a frame with channel mode {0}, in an MP3 with {1} channels. Mp3FileReader does not support changes to channel count.",
                        frame.ChannelMode, Mp3WaveFormat.Channels);
                throw new InvalidOperationException(message);
            }
        }

        // La duracion del archivo en segundos
        private double TotalSeconds()
        {
            return (double)totalSamples / Mp3WaveFormat.SampleRate;
        }
        // (int) de un doble como en XP (.NET 2 en x86): sin cuadros la tasa sale infinita o NaN y XP daba int.MinValue; .NET 10 satura
        private static int EnteroComoXp(double v) { return double.IsNaN(v) || v >= 2147483648.0 || v < -2147483648.0 ? int.MinValue : (int)v; }

        public Id3v2Tag Id3v2Tag { get; private set; }

        public byte[] Id3v1Tag { get; private set; }

        // El siguiente cuadro (null al final)
        public Mp3Frame ReadNextFrame()
        {
            Mp3Frame frame = ReadNextFrame(true);
            if (frame != null) position += frame.SampleCount * bytesPerSample;
            return frame;
        }

        // (readData = true; con false, ReadNextHeader: el indice no necesita el cuadro de NAudio, solo su cabecera)
        private Mp3Frame ReadNextFrame(bool readData)
        {
            Mp3Frame frame = null;
            try
            {
                long donde;
                frame = CargaCuadro(mp3Stream, readData, out donde);
                if (frame != null)
                {
                    tocIndex++;
                }
            }
            catch (EndOfStreamException)
            {
                // se acabo el archivo a medio cuadro: por ahora se calla
            }
            return frame;
        }
        private bool ReadNextHeader(out Cabecera frame)
        {
            bool hay = false;
            frame = new Cabecera();
            try
            {
                long donde;
                hay = BuscaCabecera(mp3Stream, false, out frame, out donde);
                if (hay)
                {
                    tocIndex++;
                }
            }
            catch (EndOfStreamException)
            {
            }
            return hay;
        }

        // Lo que se puede leer descomprimido con Read (0 si no se sabe)
        public override long Length
        {
            get
            {
                return totalSamples * bytesPerSample;
            }
        }

        public override WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }

        public override long Position
        {
            get
            {
                return position;
            }
            set
            {
                lock (repositionLock)
                {
                    value = Math.Max(Math.Min(value, Length), 0);
                    long samplePosition = value / bytesPerSample;
                    Mp3Index mp3Index = null;
                    for (int index = 0; index < tableOfContents.Count; index++)
                    {
                        if (tableOfContents[index].SamplePosition + tableOfContents[index].SampleCount > samplePosition)
                        {
                            mp3Index = tableOfContents[index];
                            tocIndex = index;
                            break;
                        }
                    }

                    decompressBufferOffset = 0;
                    decompressLeftovers = 0;
                    repositionedFlag = true;

                    if (mp3Index != null)
                    {
                        // el salto
                        mp3Stream.Position = mp3Index.FilePosition;

                        // desde donde se copia lo descomprimido (lo llena Read)
                        long frameOffset = samplePosition - mp3Index.SamplePosition;
                        if (frameOffset > 0)
                        {
                            decompressBufferOffset = (int)frameOffset * bytesPerSample;
                        }
                    }
                    else
                    {
                        // al final de los datos
                        mp3Stream.Position = mp3DataLength + dataStartPosition;
                    }

                    position = value;
                }
            }
        }

        // Lo de WaveStream de NAudio 1.9: sin alinear a muestras enteras (NAudio 3 alinea)
        public override TimeSpan CurrentTime
        {
            get { return TimeSpan.FromSeconds((double)Position / WaveFormat.AverageBytesPerSecond); }
            set { Position = (long)(value.TotalSeconds * WaveFormat.AverageBytesPerSecond); }
        }

        // Lee PCM descomprimido del MP3
        public override int Read(byte[] sampleBuffer, int offset, int numBytes)
        {
            int bytesRead = 0;
            lock (repositionLock)
            {
                if (decompressLeftovers != 0)
                {
                    int toCopy = Math.Min(decompressLeftovers, numBytes);
                    Array.Copy(decompressBuffer, decompressBufferOffset, sampleBuffer, offset, toCopy);
                    decompressLeftovers -= toCopy;
                    if (decompressLeftovers == 0)
                    {
                        decompressBufferOffset = 0;
                    }
                    else
                    {
                        decompressBufferOffset += toCopy;
                    }
                    bytesRead += toCopy;
                    offset += toCopy;
                }

                int targetTocIndex = tocIndex;   // el cuadro donde esta lo pedido

                if (repositionedFlag)
                {
                    decompressor.Reset();

                    // Unos cuadros antes, para que el descompresor recien vaciado se caliente: sin eso, la primera mitad del cuadro sale
                    // atenuada (le falta el solape del cuadro anterior). Al principio del archivo no hay calentamiento.
                    tocIndex = Math.Max(0, tocIndex - 3);
                    mp3Stream.Position = tableOfContents[tocIndex].FilePosition;

                    repositionedFlag = false;
                }

                while (bytesRead < numBytes)
                {
                    Mp3Frame frame = ReadNextFrame(true);   // lectura interna: no mueve position
                    if (frame != null)
                    {
                        int decompressed = decompressor.DecompressFrame(frame, decompressBuffer, 0);

                        if (tocIndex <= targetTocIndex || decompressed == 0)
                        {
                            // los de calentamiento (y el primero tras vaciar, que casi nunca da nada)
                            continue;
                        }
                        // el primero despues del pedido viene doble: se queda la segunda mitad
                        if (tocIndex == targetTocIndex + 1 && decompressed == bytesPerDecodedFrame * 2)
                        {
                            Array.Copy(decompressBuffer, bytesPerDecodedFrame, decompressBuffer, 0, bytesPerDecodedFrame);
                            decompressed = bytesPerDecodedFrame;
                        }

                        int toCopy = Math.Min(decompressed - decompressBufferOffset, numBytes - bytesRead);
                        Array.Copy(decompressBuffer, decompressBufferOffset, sampleBuffer, offset, toCopy);
                        if ((toCopy + decompressBufferOffset) < decompressed)
                        {
                            decompressBufferOffset = toCopy + decompressBufferOffset;
                            decompressLeftovers = decompressed - decompressBufferOffset;
                        }
                        else
                        {
                            // sin sobrantes
                            decompressBufferOffset = 0;
                            decompressLeftovers = 0;
                        }
                        offset += toCopy;
                        bytesRead += toCopy;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            System.Diagnostics.Debug.Assert(bytesRead <= numBytes, "MP3 File Reader read too much");
            position += bytesRead;
            return bytesRead;
        }

        // NAudio 3 lee con Span (IWaveProvider): la lectura de siempre, la de arriba (o la de quien la sustituya)
        public override int Read(Span<byte> buffer) { return Puente.LeeBytes(leeArreglo, buffer); }

        public XingHeader XingHeader
        {
            get { return xingHeader; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (mp3Stream != null)
                {
                    if (ownInputStream)
                    {
                        mp3Stream.Dispose();
                    }
                    mp3Stream = null;
                }
                if (decompressor != null)
                {
                    decompressor.Dispose();
                    decompressor = null;
                }
            }
            base.Dispose(disposing);
        }

        // ---------------- Lo de NAudio 1.9 que el lector usa por dentro ----------------

        // Una entrada del indice (Mp3Index de NAudio 1.9)
        private sealed class Mp3Index
        {
            public long FilePosition { get; set; }
            public long SamplePosition { get; set; }
            public int SampleCount { get; set; }
            public int ByteCount { get; set; }
        }

        // Lo que dice la cabecera de un cuadro (Mp3Frame.IsValidHeader de NAudio 1.9)
        private struct Cabecera
        {
            public int SampleRate, FrameLength, SampleCount;
            public ChannelMode ChannelMode;
            public bool EnfasisReservado;   // (NAudio 3 rechaza ese valor; 1.9 no lo miraba)
        }
        private static readonly int[,,] bitRates = new int[,,] {
            {
                // MPEG 1
                { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448 },   // capa I
                { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 },      // capa II
                { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 },       // capa III
            },
            {
                // MPEG 2 y 2.5
                { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256 },      // capa I
                { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 },           // capa II
                { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 },           // capa III (igual que la II)
            }
        };
        private static readonly int[,] samplesPerFrame = new int[,] {
            { 384, 1152, 1152 },   // MPEG 1: capas I, II, III
            { 384, 1152, 576 }     // MPEG 2 y 2.5
        };
        private static readonly int[] sampleRatesVersion1 = new int[] { 44100, 48000, 32000 };
        private static readonly int[] sampleRatesVersion2 = new int[] { 22050, 24000, 16000 };
        private static readonly int[] sampleRatesVersion25 = new int[] { 11025, 12000, 8000 };
        private const int MaxFrameLength = 16 * 1024;

        private static bool IsValidHeader(byte[] headerBytes, out Cabecera frame)
        {
            frame = new Cabecera();
            if ((headerBytes[0] == 0xFF) && ((headerBytes[1] & 0xE0) == 0xE0))
            {
                MpegVersion version = (MpegVersion)((headerBytes[1] & 0x18) >> 3);
                if (version == MpegVersion.Reserved) return false;
                MpegLayer layer = (MpegLayer)((headerBytes[1] & 0x06) >> 1);
                if (layer == MpegLayer.Reserved) return false;
                int layerIndex = layer == MpegLayer.Layer1 ? 0 : layer == MpegLayer.Layer2 ? 1 : 2;
                int bitRateIndex = (headerBytes[2] & 0xF0) >> 4;
                if (bitRateIndex == 15) return false;   // indice que no vale
                int versionIndex = version == MpegVersion.Version1 ? 0 : 1;
                int bitRate = bitRates[versionIndex, layerIndex, bitRateIndex] * 1000;
                if (bitRate == 0) return false;
                int sampleFrequencyIndex = (headerBytes[2] & 0x0C) >> 2;
                if (sampleFrequencyIndex == 3) return false;

                if (version == MpegVersion.Version1) frame.SampleRate = sampleRatesVersion1[sampleFrequencyIndex];
                else if (version == MpegVersion.Version2) frame.SampleRate = sampleRatesVersion2[sampleFrequencyIndex];
                else frame.SampleRate = sampleRatesVersion25[sampleFrequencyIndex];   // MPEG 2.5

                bool padding = (headerBytes[2] & 0x02) == 0x02;
                frame.ChannelMode = (ChannelMode)((headerBytes[3] & 0xC0) >> 6);
                int channelExtension = (headerBytes[3] & 0x30) >> 4;
                if (channelExtension != 0 && frame.ChannelMode != ChannelMode.JointStereo) return false;
                frame.EnfasisReservado = (headerBytes[3] & 0x03) == 2;

                int nPadding = padding ? 1 : 0;
                frame.SampleCount = samplesPerFrame[versionIndex, layerIndex];
                int coefficient = frame.SampleCount / 8;
                if (layer == MpegLayer.Layer1) frame.FrameLength = (coefficient * bitRate / frame.SampleRate + nPadding) * 4;
                else frame.FrameLength = (coefficient * bitRate) / frame.SampleRate + nPadding;

                if (frame.FrameLength > MaxFrameLength) return false;
                return true;
            }
            return false;
        }

        // Mp3Frame.LoadFromStream de NAudio 1.9: desde donde esta el flujo, la primera cabecera que vale (byte a byte); con readData, el cuadro
        // entero se lee en 'cuadro' (y si el archivo se acaba antes, EndOfStreamException); sin readData, se salta. false = no quedan cuadros.
        private readonly byte[] headerBytes = new byte[4];
        private bool BuscaCabecera(Stream input, bool readData, out Cabecera frame, out long fileOffset)
        {
            fileOffset = input.Position;
            byte[] h = headerBytes;
            frame = new Cabecera();
            int bytesRead = input.Read(h, 0, h.Length);
            if (bytesRead < h.Length)
            {
                return false;   // se acabo: no hay mas cuadros
            }
            while (!IsValidHeader(h, out frame))
            {
                // un byte mas adelante
                h[0] = h[1];
                h[1] = h[2];
                h[2] = h[3];
                bytesRead = input.Read(h, 3, 1);
                if (bytesRead < 1)
                {
                    return false;
                }
                fileOffset++;
            }

            int bytesRequired = frame.FrameLength - 4;
            if (readData)
            {
                byte[] d = cuadro.Datos;
                Array.Copy(h, d, 4);
                bytesRead = input.Read(d, 4, bytesRequired);
                if (bytesRead < bytesRequired)
                {
                    throw new EndOfStreamException("Unexpected end of stream before frame complete");
                }
                if (frame.EnfasisReservado) d[3] = (byte)(d[3] & 0xFC);   // (solo para que NAudio 3 lo acepte; CargaCuadro lo devuelve como estaba)
                cuadro.Pon(frame.FrameLength);
            }
            else
            {
                input.Position += bytesRequired;
            }
            return true;
        }
        private Mp3Frame CargaCuadro(Stream input, bool readData, out long fileOffset)
        {
            Cabecera c;
            if (!BuscaCabecera(input, readData, out c, out fileOffset)) return null;
            if (!readData) return null;
            Mp3Frame f = Mp3Frame.LoadFromStream(cuadro, true);
            if (f != null && c.EnfasisReservado) f.RawData[3] = headerBytes[3];   // los bytes del archivo, tal cual (como en 1.9)
            return f;
        }

        // XingHeader.LoadXingHeader de NAudio 1.9: solo la cabecera «Xing» (la de los VBR). NAudio 3 reconoce tambien «Info» (la que LAME pone
        // en los CBR) y salta ese cuadro; 1.9 no: lo cuenta como musica (un cuadro callado) salvo que el segundo cuadro diga otra frecuencia u
        // otros canales (el arreglo del segundo cuadro). Medido con un MP3 mono de 22 kHz: 1151 cuadros con NAudio 3, 1152 con 1.9.
        private static XingHeader LoadXingHeader(Mp3Frame frame)
        {
            int offset;
            if (frame.MpegVersion == MpegVersion.Version1) offset = frame.ChannelMode != ChannelMode.Mono ? 32 + 4 : 17 + 4;
            else if (frame.MpegVersion == MpegVersion.Version2) offset = frame.ChannelMode != ChannelMode.Mono ? 17 + 4 : 9 + 4;
            else return null;
            byte[] r = frame.RawData;
            if (r[offset + 0] == 'X' && r[offset + 1] == 'i' && r[offset + 2] == 'n' && r[offset + 3] == 'g') return XingHeader.LoadXingHeader(frame);
            return null;
        }

        // La etiqueta ID3v2 del principio: el objeto lo arma NAudio 3, pero donde acaba es la cuenta de NAudio 1.9 (que con cabecera
        // extendida salta 4 bytes de mas y no comprueba los tamanos), para empezar a buscar el primer cuadro en el mismo sitio que XP.
        private static Id3v2Tag LeeId3v2(Stream input)
        {
            long inicio = input.Position;
            Id3v2Tag tag = null;
            try { tag = Id3v2Tag.ReadTag(input); } catch (Exception) { }   // (el objeto es solo informativo: si NAudio 3 no lo lee, null)
            input.Position = inicio;
            byte[] h = new byte[10];
            int n = 0, r;
            while (n < h.Length && (r = input.Read(h, n, h.Length - n)) > 0) n += r;
            if (n >= 3 && h[0] == 'I' && h[1] == 'D' && h[2] == '3')
            {
                if (n < 10) throw new IndexOutOfRangeException();   // (1.9 leia la cabecera de 10 bytes sin mirar si estaba entera)
                long fin = inicio + n;
                if ((h[5] & 0x40) == 0x40) fin += 4;                                                 // la cabecera extendida
                fin += h[6] * (1 << 21) + h[7] * (1 << 14) + h[8] * (1 << 7) + h[9];                 // los datos (synchsafe)
                if ((h[5] & 0x10) == 0x10) fin += 10;                                                // el pie
                input.Position = Math.Min(fin, input.Length);
            }
            else input.Position = inicio;
            return tag;
        }

        // Un solo cuadro visto como flujo, para que NAudio 3 arme su Mp3Frame con ese cuadro y nada mas. Sin poder buscar (CanSeek = false):
        // asi NAudio no busca etiquetas ID3 ni mira cabeceras por delante (Mp3Frame.LoadFromStream: TrySkipTag, IsLikelyAudioFrameSequence).
        private sealed class UnCuadro : Stream
        {
            internal readonly byte[] Datos = new byte[MaxFrameLength + 4];
            private int largo, pos;
            internal void Pon(int n) { largo = n; pos = 0; }
            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return largo; } }
            public override long Position { get { return pos; } set { pos = (int)Math.Max(0, Math.Min(value, largo)); } }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int k = Math.Min(count, largo - pos);
                if (k <= 0) return 0;
                Buffer.BlockCopy(Datos, pos, buffer, offset, k); pos += k;
                return k;
            }
            public override int Read(Span<byte> buffer)
            {
                int k = Math.Min(buffer.Length, largo - pos);
                if (k <= 0) return 0;
                Datos.AsSpan(pos, k).CopyTo(buffer); pos += k;
                return k;
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        }
    }
}

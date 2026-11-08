// EL LECTOR DE LO QUE NO ES UN MP3 NI UN WAV DE SIEMPRE (port nativo de XP, 8-11-2026).
//
// En XP, FileAudioSource y LectorMedida (Form1.Sonido.cs) abren los MP3 con su lector y los WAV PCM o de coma flotante directo (WavDirecto);
// lo demas va al AudioFileReader de NAudio 1.9, que hace esto (NAudio, (c) Mark Heath y colaboradores, licencia MIT):
//   - .wav comprimido (ADPCM, ley A o mu, GSM, MP3 dentro de un WAV...): WaveFileReader y, por el ACM de Windows,
//     WaveFormatConversionStream.CreatePcmStream (el formato PCM que sugiere el codec: 16 bits, la misma frecuencia y los mismos canales) con
//     BlockAlignReductionStream; si XP no tiene codec para ese formato, acmFormatSuggest falla (MmException) y no abre;
//   - .mp3: Mp3FileReader; .aiff / .aif: AiffFileReader;
//   - lo demas: MediaFoundationReader. Windows XP NO tiene Media Foundation (es de Vista): en el A4 eso no abre nunca (no esta mfplat.dll).
// Despues, un SampleChannel (coma flotante, con su volumen), y las cuentas de largo y posicion en bytes de esa coma flotante.
//
// La traduccion anterior lo imitaba con otra cosa (Plataforma/AudioXp.cs: AudioFileReader con ffmpeg para todo lo demas, que en el A4 con
// XP no abre). Aqui, el codigo de XP llama a este lector, que hace lo mismo que el de NAudio 1.9 con los lectores de NAudio.Core que no son
// de Windows (WaveFileReader, AiffFileReader, SampleChannel), el lector de MP3 de XP (Plataforma/Mp3Xp.cs, con su descompresor nativo) y:
//   - en lugar del ACM, ffmpeg (el programa, que trae Rocola OS) descomprime a PCM de 16 bits con la misma frecuencia y los mismos canales,
//     solo los formatos que XP trae de fabrica con codec ACM (Microsoft ADPCM, IMA ADPCM, CCITT ley A y mu, GSM 6.10, MPEG Layer-3,
//     TrueSpeech, G.723.1, Windows Media Audio 1 y 2); los demas no abren, como en XP;
//   - en lugar de Media Foundation, nada: no abre, como en el A4 (FormatoSinLector, que la rocola trata como XP trata ese fallo: «no se
//     pudo abrir», sin darla por danada).
// Lo que no puede salir igual: el largo de un WAV comprimido es una estimacion del codec (acmStreamSize en XP: aqui la misma cuenta en los
// ADPCM, el GSM y la ley A y mu -bloques enteros por muestras de cada bloque-, y en los demas la proporcion de los bytes por segundo), y
// ffmpeg y el ACM pueden redondear distinto en los codecs con perdida.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    // No hay con que abrir ese formato (en XP: MmException de acmFormatSuggest, o la falta de Media Foundation en Windows XP)
    internal sealed class FormatoSinLector : Exception
    {
        internal FormatoSinLector(string mensaje) : base(mensaje) { }
    }

    internal sealed class LectorAudio : WaveStream, ISampleProvider
    {
        private WaveStream lector;                 // (readerStream de NAudio) el que da las posiciones
        private readonly SampleChannel canal;      // (sampleChannel)
        private readonly int bytesDestino, bytesOrigen;
        private readonly long largo;
        private readonly object cerrojo = new object();

        internal LectorAudio(string ruta)
        {
            Abre(ruta);
            bytesOrigen = (lector.WaveFormat.BitsPerSample / 8) * lector.WaveFormat.Channels;
            canal = new SampleChannel(lector, false);
            bytesDestino = 4 * canal.WaveFormat.Channels;
            largo = HaciaDestino(lector.Length);
        }
        private void Abre(string ruta)
        {
            if (ruta.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                lector = new WaveFileReader(ruta);
                if (lector.WaveFormat.Encoding != WaveFormatEncoding.Pcm && lector.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                {
                    WaveFileReader comprimido = (WaveFileReader)lector;
                    lector = null;
                    try { lector = new DescompresionWav(ruta, comprimido); }   // (en XP: el ACM y BlockAlignReductionStream)
                    finally { comprimido.Dispose(); }
                }
            }
            else if (ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                lector = new Mp3FileReader(ruta);
            else if (ruta.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase) || ruta.EndsWith(".aif", StringComparison.OrdinalIgnoreCase))
                lector = new AiffFileReader(ruta);
            else
                throw new FormatoSinLector("Windows XP no tiene Media Foundation: " + Path.GetExtension(ruta) + " no se puede abrir");
        }

        public override WaveFormat WaveFormat { get { return canal.WaveFormat; } }
        public override long Length { get { return largo; } }
        public override long Position
        {
            get { return HaciaDestino(lector.Position); }
            set { lock (cerrojo) { lector.Position = HaciaOrigen(value); } }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            // (NAudio 1.9 lee en la vista flotante del mismo arreglo; aqui en uno de apoyo y se copia: los mismos bytes)
            int n = count / 4;
            if (apoyo.Length < n) apoyo = new float[n];
            int leidas = Read(apoyo, 0, n);
            Buffer.BlockCopy(apoyo, 0, buffer, offset, leidas * 4);
            return leidas * 4;
        }
        private float[] apoyo = new float[0];
        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
        public int Read(float[] buffer, int offset, int count) { lock (cerrojo) return canal.Read(buffer, offset, count); }
        public float Volume { get { return canal.Volume; } set { canal.Volume = value; } }
        private long HaciaDestino(long origen) { return bytesDestino * (origen / bytesOrigen); }
        private long HaciaOrigen(long destino) { return bytesOrigen * (destino / bytesDestino); }
        protected override void Dispose(bool disposing)
        {
            if (disposing && lector != null) { lector.Dispose(); lector = null; }
            base.Dispose(disposing);
        }
    }

    // UN WAV COMPRIMIDO, DESCOMPRIMIDO A PCM DE 16 BITS (en XP: el ACM de Windows con el codec del formato). ffmpeg por una tuberia; saltar es
    // volver a abrirlo desde ahi (-ss).
    internal sealed class DescompresionWav : WaveStream
    {
        // Los formatos con codec ACM en un Windows XP de fabrica (Panel de control > Sonidos > Hardware > Codecs de audio)
        private static readonly int[] DeXp = { 0x0002, 0x0011, 0x0006, 0x0007, 0x0031, 0x0055, 0x0022, 0x0042, 0x0160, 0x0161 };
        private readonly string ruta;
        private readonly WaveFormat pcm;
        private readonly long largo;
        private Process p; private Stream salida;
        private long posicion;   // bytes de PCM dados desde el principio
        internal DescompresionWav(string ruta, WaveFileReader archivo)
        {
            WaveFormat comprimido = archivo.WaveFormat;
            int etiqueta = (int)comprimido.Encoding;
            if (Array.IndexOf(DeXp, etiqueta) < 0)
                throw new FormatoSinLector("Windows XP no tiene codec para el formato " + comprimido.Encoding + " (0x" + etiqueta.ToString("X4") + ")");
            this.ruta = ruta;
            pcm = new WaveFormat(comprimido.SampleRate, 16, comprimido.Channels);   // lo que sugiere el codec: 16 bits, misma frecuencia y canales
            largo = Largo(archivo, pcm);
            Arranca(0);
        }
        // Lo que dara descomprimido (en XP, acmStreamSize del codec): los ADPCM y el GSM, bloques enteros por las muestras de cada bloque; la ley
        // A y mu, el doble (de 8 a 16 bits); los demas, por los bytes por segundo del archivo (o, si no los dice, por el bloque «fact»)
        private static long Largo(WaveFileReader archivo, WaveFormat pcm)
        {
            WaveFormat f = archivo.WaveFormat;
            long datos = archivo.Length;
            int porBloque = 0;
            if (f is AdpcmWaveFormat) porBloque = ((AdpcmWaveFormat)f).SamplesPerBlock;
            else if (f is Gsm610WaveFormat) porBloque = ((Gsm610WaveFormat)f).SamplesPerBlock;
            else if (f.Encoding == WaveFormatEncoding.ImaAdpcm && f is WaveFormatExtraData && f.ExtraSize >= 2)
            { byte[] e = ((WaveFormatExtraData)f).ExtraData; porBloque = e[0] | (e[1] << 8); }
            long l;
            if (porBloque > 0 && f.BlockAlign > 0) l = datos / f.BlockAlign * porBloque * pcm.BlockAlign;
            else if (f.Encoding == WaveFormatEncoding.ALaw || f.Encoding == WaveFormatEncoding.MuLaw) l = datos * 2;
            else if (f.AverageBytesPerSecond > 0) l = (long)(datos * ((double)pcm.AverageBytesPerSecond / f.AverageBytesPerSecond));
            else l = Fact(archivo) * pcm.BlockAlign;
            return l - l % pcm.BlockAlign;
        }
        private static long Fact(WaveFileReader archivo)
        {
            try
            {
                foreach (RiffChunk c in archivo.Chunks)
                    if (c.IdentifierAsString == "fact") { byte[] d = archivo.Chunks.GetData(c); if (d.Length >= 4) return BitConverter.ToUInt32(d, 0); }
            }
            catch { }
            return 0;
        }
        private void Arranca(long desde)
        {
            Cierra();
            double segundos = (double)desde / pcm.AverageBytesPerSecond;
            ProcessStartInfo pi = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = false, CreateNoWindow = true };
            foreach (string a in new[] { "-v", "quiet", "-nostdin", "-ss", segundos.ToString("0.######", CultureInfo.InvariantCulture), "-i", ruta, "-vn",
                "-f", "s16le", "-ac", pcm.Channels.ToString(CultureInfo.InvariantCulture), "-ar", pcm.SampleRate.ToString(CultureInfo.InvariantCulture), "-" })
                pi.ArgumentList.Add(a);
            try { p = Process.Start(pi); }
            catch (Exception ex) { throw new FormatoSinLector("sin ffmpeg no se puede descomprimir este WAV: " + ex.Message); }
            salida = p.StandardOutput.BaseStream;
            posicion = desde;
        }
        public override WaveFormat WaveFormat { get { return pcm; } }
        public override long Length { get { return largo; } }
        public override long Position
        {
            get { return posicion; }
            set { long v = Math.Max(0, Math.Min(largo, value)); Arranca(v - v % pcm.BlockAlign); }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (salida == null) return 0;
            int total = 0;
            while (total < count) { int n = salida.Read(buffer, offset + total, count - total); if (n <= 0) break; total += n; }
            posicion += total;
            return total;
        }
        private void Cierra()
        {
            try { if (p != null && !p.HasExited) p.Kill(); } catch { }
            try { if (p != null) p.Dispose(); } catch { }
            p = null; salida = null;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Cierra();
            base.Dispose(disposing);
        }
    }
}

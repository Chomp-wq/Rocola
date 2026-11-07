// LO QUE EN XP DA NAUDIO CON WINDOWS, EN LINUX (traduccion de XP, fase 1). La rocola de XP usa NAudio 1.9 entero; en Linux va NAudio 3
// (NAudio.Core, la parte sin Windows), como el motor de Rocola OS. Aqui se traduce lo que cambia:
//  - Puente: en NAudio 3 ISampleProvider lee con Span<float>; las etapas de XP leen con (float[], offset, count). Cada etapa gana la
//    lectura con Span que llama a la suya (una linea, «NAudio 3 (Puente.cs)»), y las llamadas de siempre a la etapa de delante
//    (src.Read(buffer, offset, count)) siguen valiendo con la extension de abajo. Igual que sistema/rocola/Motor/Puente.cs.
//  - Mp3FileReader: el lector de MP3 de NAudio (Mp3FileReaderBase, el mismo codigo: tabla de cuadros, duracion, saltos) con el
//    descompresor de Windows (ACM) traducido a libmpg123, la biblioteca nativa de MP3 del sistema (como Motor/Mpg123.cs), que como el ACM
//    entrega enteros de 16 bits cuadro a cuadro. Sin libmpg123, NLayer (managed).
using System;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Rocola
{
    internal static class Puente
    {
        // Una lectura con Span hecha con la lectura de siempre, sobre un arreglo de apoyo del hilo. UNO POR NIVEL: una etapa lee de la de
        // delante dentro de su propia lectura (con un solo apoyo, la de delante escribiria en el que la de detras esta usando).
        [ThreadStatic] private static float[][] apoyos;
        [ThreadStatic] private static int nivel;
        internal static int Lee(Func<float[], int, int, int> lee, Span<float> destino)
        {
            float[][] t = apoyos ?? (apoyos = new float[8][]);
            int k = nivel;
            if (k >= t.Length) { Array.Resize(ref t, t.Length * 2); apoyos = t; }
            float[] a = t[k];
            if (a == null || a.Length < destino.Length) t[k] = a = new float[Math.Max(destino.Length, 8192)];
            nivel = k + 1;
            try
            {
                int n = lee(a, 0, destino.Length);
                if (n > 0) a.AsSpan(0, n).CopyTo(destino);
                return n;
            }
            finally { nivel = k; }
        }
        [ThreadStatic] private static byte[][] apoyosBytes;
        [ThreadStatic] private static int nivelBytes;
        internal static int LeeBytes(Func<byte[], int, int, int> lee, Span<byte> destino)
        {
            byte[][] t = apoyosBytes ?? (apoyosBytes = new byte[8][]);
            int k = nivelBytes;
            if (k >= t.Length) { Array.Resize(ref t, t.Length * 2); apoyosBytes = t; }
            byte[] a = t[k];
            if (a == null || a.Length < destino.Length) t[k] = a = new byte[Math.Max(destino.Length, 16384)];
            nivelBytes = k + 1;
            try
            {
                int n = lee(a, 0, destino.Length);
                if (n > 0) a.AsSpan(0, n).CopyTo(destino);
                return n;
            }
            finally { nivelBytes = k; }
        }
        // La lectura de siempre sobre cualquier ISampleProvider / IWaveProvider de NAudio 3.
        internal static int Read(this ISampleProvider p, float[] buffer, int offset, int count) { return p.Read(buffer.AsSpan(offset, count)); }
        internal static int Read(this IWaveProvider p, byte[] buffer, int offset, int count) { return p.Read(buffer.AsSpan(offset, count)); }
    }

    // EL LECTOR DE MP3 DE LA ROCOLA DE XP: el de NAudio (Mp3FileReaderBase) con el descompresor que diga quien lo abre; el «de Windows»
    // (CreateAcmFrameDecompressor) es aqui el de libmpg123.
    public class Mp3FileReader : Mp3FileReaderBase
    {
        public Mp3FileReader(string mp3FileName) : base(mp3FileName, CreateAcmFrameDecompressor) { }
        public Mp3FileReader(string mp3FileName, FrameDecompressorBuilder frameDecompressorBuilder) : base(mp3FileName, frameDecompressorBuilder) { }
        public Mp3FileReader(Stream inputStream) : base(inputStream, CreateAcmFrameDecompressor) { }
        public Mp3FileReader(Stream inputStream, FrameDecompressorBuilder frameDecompressorBuilder) : base(inputStream, frameDecompressorBuilder) { }
        public static IMp3FrameDecompressor CreateAcmFrameDecompressor(WaveFormat mp3Format)
        {
            IMp3FrameDecompressor d = DescompresorMpg123.Crea(mp3Format);
            return d ?? new DescompresorNLayer16(mp3Format);
        }
    }

    // EL DESCOMPRESOR NATIVO: libmpg123 en modo «alimentar». Cada cuadro de MP3 que da Mp3FileReaderBase entra entero y sale lo que mpg123
    // tiene listo, en enteros de 16 bits (como el ACM de Windows). Reset (al saltar) vacia lo que tenia dentro.
    internal sealed unsafe class DescompresorMpg123 : IMp3FrameDecompressor
    {
        private const string Lib = "libmpg123.so.0";
        [DllImport(Lib)] private static extern int mpg123_init();
        [DllImport(Lib)] private static extern IntPtr mpg123_new(IntPtr decoder, out int error);
        [DllImport(Lib)] private static extern int mpg123_param(IntPtr mh, int type, long value, double fvalue);
        [DllImport(Lib)] private static extern int mpg123_format_none(IntPtr mh);
        [DllImport(Lib)] private static extern int mpg123_format(IntPtr mh, long rate, int channels, int encodings);
        [DllImport(Lib)] private static extern int mpg123_open_feed(IntPtr mh);
        [DllImport(Lib)] private static extern int mpg123_decode(IntPtr mh, byte* inmemory, UIntPtr inmemsize, byte* outmemory, UIntPtr outmemsize, out UIntPtr done);
        [DllImport(Lib)] private static extern int mpg123_close(IntPtr mh);
        [DllImport(Lib)] private static extern void mpg123_delete(IntPtr mh);
        private const int ADD_FLAGS = 2, REMOVE_FLAGS = 13, RESYNC_LIMIT = 14, QUIET = 0x20, GAPLESS = 0x40, ENC_SIGNED_16 = 0xD0;
        private const int OK = 0, NEED_MORE = -10, NEW_FORMAT = -11;
        private static int estado;   // 0 sin probar, 1 lista, -1 no esta
        private static readonly object candado = new object();
        private static bool Lista()
        {
            if (estado != 0) return estado > 0;
            lock (candado)
            {
                if (estado == 0)
                {
                    try { estado = mpg123_init() == OK ? 1 : -1; } catch { estado = -1; }
                    if (Environment.GetEnvironmentVariable("ROCOLA_SIN_MPG123") == "1") estado = -1;
                }
                return estado > 0;
            }
        }
        private IntPtr mh;
        private readonly WaveFormat salida;
        internal static DescompresorMpg123 Crea(WaveFormat mp3)
        {
            if (!Lista()) return null;
            DescompresorMpg123 d = new DescompresorMpg123(mp3);
            return d.mh == IntPtr.Zero ? null : d;
        }
        private DescompresorMpg123(WaveFormat mp3)
        {
            salida = new WaveFormat(mp3.SampleRate, 16, mp3.Channels);
            Abre();
        }
        private void Abre()
        {
            int err; IntPtr h = mpg123_new(IntPtr.Zero, out err);
            if (h == IntPtr.Zero) return;
            mpg123_param(h, ADD_FLAGS, QUIET, 0);
            mpg123_param(h, REMOVE_FLAGS, GAPLESS, 0);   // el relleno del codificador lo quita la rocola (SinRelleno), como con el ACM
            mpg123_param(h, RESYNC_LIMIT, -1, 0);
            mpg123_format_none(h);
            mpg123_format(h, salida.SampleRate, salida.Channels, ENC_SIGNED_16);
            if (mpg123_open_feed(h) != OK) { mpg123_delete(h); return; }
            mh = h;
        }
        public WaveFormat OutputFormat { get { return salida; } }
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            if (mh == IntPtr.Zero || frame == null) return 0;
            int cabe = dest.Length - destOffset, total = 0;
            fixed (byte* f = frame.RawData)
            fixed (byte* d = dest)
            {
                UIntPtr hecho;
                int rc = mpg123_decode(mh, f, (UIntPtr)frame.FrameLength, d + destOffset, (UIntPtr)cabe, out hecho);
                total += (int)(ulong)hecho;
                // el primer cuadro avisa del formato: se vuelve a pedir lo que ya tiene, sin darle mas
                while ((rc == NEW_FORMAT || rc == OK) && total < cabe)
                {
                    rc = mpg123_decode(mh, null, UIntPtr.Zero, d + destOffset + total, (UIntPtr)(cabe - total), out hecho);
                    if ((ulong)hecho == 0 && rc != NEW_FORMAT) break;
                    total += (int)(ulong)hecho;
                }
            }
            return total;
        }
        public void Reset()
        {
            if (mh != IntPtr.Zero) { mpg123_close(mh); mpg123_delete(mh); mh = IntPtr.Zero; }
            Abre();
        }
        public void Dispose()
        {
            if (mh != IntPtr.Zero) { try { mpg123_close(mh); mpg123_delete(mh); } catch { } mh = IntPtr.Zero; }
        }
    }

    // SIN LIBMPG123 (una PC de pruebas): NLayer, cuadro a cuadro, en enteros de 16 bits.
    internal sealed class DescompresorNLayer16 : IMp3FrameDecompressor
    {
        private readonly NLayer.MpegFrameDecoder dec = new NLayer.MpegFrameDecoder();
        private readonly WaveFormat salida;
        private readonly float[] apoyo = new float[1152 * 2];
        private readonly CuadroNLayer cuadro = new CuadroNLayer();
        public DescompresorNLayer16(WaveFormat mp3) { salida = new WaveFormat(mp3.SampleRate, 16, mp3.Channels); }
        public WaveFormat OutputFormat { get { return salida; } }
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            cuadro.Pon(frame);
            int n = dec.DecodeFrame(cuadro, apoyo, 0);
            int bytes = Math.Min(n, (dest.Length - destOffset) / 2);
            for (int i = 0; i < bytes; i++)
            {
                float v = apoyo[i] * 32768f;
                short s = v >= 32767f ? (short)32767 : v <= -32768f ? (short)-32768 : (short)Math.Round(v);
                dest[destOffset + 2 * i] = (byte)s; dest[destOffset + 2 * i + 1] = (byte)(s >> 8);
            }
            return bytes * 2;
        }
        public void Reset() { dec.Reset(); }
        public void Dispose() { }

        // un cuadro de NAudio visto como cuadro de NLayer
        private sealed class CuadroNLayer : NLayer.IMpegFrame
        {
            private Mp3Frame f; private int bit;
            public void Pon(Mp3Frame frame) { f = frame; bit = 0; }
            public int SampleRate { get { return f.SampleRate; } }
            public int SampleRateIndex { get { return (f.RawData[2] >> 2) & 3; } }
            public int FrameLength { get { return f.FrameLength; } }
            public int BitRate { get { return f.BitRate; } }
            public NLayer.MpegVersion Version { get { return f.MpegVersion == MpegVersion.Version1 ? NLayer.MpegVersion.Version1 : f.MpegVersion == MpegVersion.Version2 ? NLayer.MpegVersion.Version2 : NLayer.MpegVersion.Version25; } }
            public NLayer.MpegLayer Layer { get { return f.MpegLayer == MpegLayer.Layer1 ? NLayer.MpegLayer.LayerI : f.MpegLayer == MpegLayer.Layer2 ? NLayer.MpegLayer.LayerII : NLayer.MpegLayer.LayerIII; } }
            public NLayer.MpegChannelMode ChannelMode { get { return (NLayer.MpegChannelMode)(int)f.ChannelMode; } }
            public int ChannelModeExtension { get { return f.ChannelExtension; } }
            public int SampleCount { get { return f.SampleCount; } }
            public int BitRateIndex { get { return f.BitRateIndex; } }
            public bool IsCopyrighted { get { return f.Copyright; } }
            public bool HasCrc { get { return f.CrcPresent; } }
            public bool IsCorrupted { get { return false; } }
            public void Reset() { bit = 0; }
            public int ReadBits(int bitCount)
            {
                if (bitCount < 1 || bitCount > 32) throw new ArgumentOutOfRangeException("bitCount");
                int v = 0;
                for (int i = 0; i < bitCount; i++)
                {
                    int byteIdx = bit >> 3;
                    int b = byteIdx < f.FrameLength ? (f.RawData[byteIdx] >> (7 - (bit & 7))) & 1 : 0;
                    v = (v << 1) | b; bit++;
                }
                return v;
            }
        }
    }
}

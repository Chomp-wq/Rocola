// LOS DESCOMPRESORES DE MP3 (port nativo de XP, 8-11-2026). En XP, cada lector de MP3 (el Mp3FileReader de NAudio 1.9) descomprime los cuadros
// con el ACM de Windows (Mp3FileReader.CreateAcmFrameDecompressor: el codec «MPEG Layer-3» de Fraunhofer que trae XP), que entrega enteros de
// 16 bits cuadro a cuadro. El codigo de XP pide aqui el suyo directo (DescompresorMp3.Crea, en Form1.Sonido.cs y CancionEnMemoria.cs):
//   - libmpg123, la biblioteca nativa de MP3 (Rocola OS la trae: BR2_PACKAGE_MPG123; la usa tambien el motor de Linux, Motor/Mpg123.cs),
//     en modo «alimentar», con enteros de 16 bits como el ACM;
//   - sin libmpg123 (una PC de pruebas, o ROCOLA_SIN_MPG123=1), NLayer en enteros de 16 bits.
// Se queda libmpg123 delante, como en la referencia (aef2211): es lo que suena igual muestra a muestra que ella, y es lo que se eligio para
// Rocola OS el 2-11-2026 (configs/rocola_a4_defconfig: «NLayer se comia un cuadro en algunos y es ~8 veces mas lento»). NLayer sigue en lo
// que XP ya hacia con el (la coma flotante de FlotanteNLayer.cs).
using System;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Rocola
{
    internal static class DescompresorMp3
    {
        // El descompresor de un MP3 con ese formato (en XP: Mp3FileReader.CreateAcmFrameDecompressor, el ACM de Windows)
        internal static IMp3FrameDecompressor Crea(WaveFormat mp3)
        {
            IMp3FrameDecompressor d = DescompresorMpg123.Crea(mp3);
            return d ?? new DescompresorNLayer16(mp3);
        }
    }

    // EL DESCOMPRESOR NATIVO: libmpg123 en modo «alimentar». Cada cuadro de MP3 que da el lector (Mp3Xp.cs) entra entero y sale lo que mpg123
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

    // SIN LIBMPG123 (una PC de pruebas): NLayer, cuadro a cuadro, en enteros de 16 bits. Cada cuadro se le da a NLayer con el adaptador de XP
    // (CuadroNLayer, Codigo/FlotanteNLayer.cs: los bits de detras de la cabecera y del CRC). (La referencia, aef2211, traia aqui un adaptador
    // propio que leia desde la cabecera: NLayer se salia del arreglo en el primer cuadro y ninguna cancion sonaba sin libmpg123; comprobado
    // con ROCOLA_SIN_MPG123=1 el 8-11-2026.)
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
    }
}

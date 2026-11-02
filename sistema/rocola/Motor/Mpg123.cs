// EL DESCOMPRESOR DE MP3 (Rocola OS, 2-11-2026): libmpg123, la biblioteca nativa de MP3 (en el sistema con BR2_PACKAGE_MPG123).
// Dos motivos: NLayer 3.0.0 se COME un cuadro entero (26 ms, con un chasquido) en algunos archivos a media cancion (medido contra
// ffmpeg: uno en cada archivo de prueba, en sitios distintos), y en C# descomprimir era casi todo lo que tardaba el repaso (unas 8
// veces mas lento). mpg123 da las mismas muestras que ffmpeg; fuera de ese cuadro, las mismas que NLayer (a 3e-7).
// Lo usa LectorCancion (Lector.cs) para sonar y para medir. Si la biblioteca no esta, el archivo no abre o es de frecuencias
// mezcladas (Mp3DeUnaFrecuencia), va NLayer como antes. Sin el gapless de mpg123 (quitaria el relleno del codificador, que la rocola
// quita por su cuenta: SinRelleno), asi las muestras caen en el mismo sitio que con NLayer.
using System;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Rocola
{
    internal sealed unsafe class Mpg123Lector : IDisposable
    {
        private const string Lib = "libmpg123.so.0";
        [DllImport(Lib)] private static extern int mpg123_init();
        [DllImport(Lib)] private static extern IntPtr mpg123_new(IntPtr decoder, out int error);
        [DllImport(Lib)] private static extern int mpg123_param(IntPtr mh, int type, long value, double fvalue);
        [DllImport(Lib)] private static extern int mpg123_format_none(IntPtr mh);
        [DllImport(Lib)] private static extern int mpg123_format(IntPtr mh, long rate, int channels, int encodings);
        [DllImport(Lib)] private static extern int mpg123_open(IntPtr mh, byte* path);
        [DllImport(Lib)] private static extern int mpg123_getformat(IntPtr mh, out long rate, out int channels, out int encoding);
        [DllImport(Lib)] private static extern int mpg123_scan(IntPtr mh);
        [DllImport(Lib)] private static extern long mpg123_length(IntPtr mh);   // off_t: 64 bits en x86-64
        [DllImport(Lib)] private static extern long mpg123_seek(IntPtr mh, long sampleoff, int whence);
        [DllImport(Lib)] private static extern long mpg123_tell(IntPtr mh);
        [DllImport(Lib)] private static extern int mpg123_read(IntPtr mh, void* outmem, UIntPtr size, out UIntPtr done);
        [DllImport(Lib)] private static extern int mpg123_close(IntPtr mh);
        [DllImport(Lib)] private static extern void mpg123_delete(IntPtr mh);

        private const int ADD_FLAGS = 2, REMOVE_FLAGS = 13, QUIET = 0x20, GAPLESS = 0x40, FORCE_FLOAT = 0x400, ENC_FLOAT_32 = 0x200;
        private const int OK = 0, NEW_FORMAT = -11, DONE = -12;

        private static readonly object candado = new object();
        private static int estado;   // 0 sin probar, 1 lista, -1 no esta (se mide con NLayer)
        private static bool Lista()
        {
            if (estado != 0) return estado > 0;
            lock (candado)
            {
                if (estado == 0)
                {
                    try { estado = mpg123_init() == OK ? 1 : -1; }
                    catch { estado = -1; }   // DllNotFoundException, EntryPointNotFoundException
                    if (Environment.GetEnvironmentVariable("ROCOLA_SIN_MPG123") == "1") estado = -1;   // prueba: todo con NLayer, para comparar
                }
                return estado > 0;
            }
        }

        private IntPtr mh;
        private readonly WaveFormat formato;
        private readonly TimeSpan duracion;

        private Mpg123Lector(IntPtr mh, int tasa, int canales, long muestras)
        {
            this.mh = mh;
            formato = WaveFormat.CreateIeeeFloatWaveFormat(tasa, canales);
            duracion = TimeSpan.FromSeconds(muestras > 0 ? (double)muestras / tasa : 0);
        }

        // null = que se mida con NLayer.
        internal static Mpg123Lector Abre(string ruta)
        {
            if (!Lista() || !ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return null;
            IntPtr h = IntPtr.Zero;
            try
            {
                int err; h = mpg123_new(IntPtr.Zero, out err);
                if (h == IntPtr.Zero) return null;
                mpg123_param(h, ADD_FLAGS, QUIET | FORCE_FLOAT, 0);
                mpg123_param(h, REMOVE_FLAGS, GAPLESS, 0);
                mpg123_format_none(h);
                foreach (int r in new[] { 8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000 }) mpg123_format(h, r, 1 | 2, ENC_FLOAT_32);
                byte[] b = System.Text.Encoding.UTF8.GetBytes(ruta + "\0");
                int rc; fixed (byte* p = b) rc = mpg123_open(h, p);
                if (rc != OK) { mpg123_delete(h); return null; }
                long tasa; int ch, enc;
                if (mpg123_getformat(h, out tasa, out ch, out enc) != OK || enc != ENC_FLOAT_32 || ch < 1 || ch > 2 || tasa < 8000) { mpg123_close(h); mpg123_delete(h); return null; }
                mpg123_scan(h);   // la duracion exacta y el indice para saltar a los tramos
                long n = mpg123_length(h);
                Mpg123Lector l = new Mpg123Lector(h, (int)tasa, ch, n);
                h = IntPtr.Zero;
                return l;
            }
            catch { if (h != IntPtr.Zero) { try { mpg123_close(h); mpg123_delete(h); } catch { } } return null; }
        }

        internal WaveFormat WaveFormat { get { return formato; } }
        internal TimeSpan Duracion { get { return duracion; } }
        internal TimeSpan Posicion
        {
            get { return mh == IntPtr.Zero ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Max(0L, mpg123_tell(mh)) / (double)formato.SampleRate); }
            set { acabo = false; if (mh != IntPtr.Zero) mpg123_seek(mh, (long)(value.TotalSeconds * formato.SampleRate), 0); }
        }

        private bool acabo;
        internal int Read(Span<float> d)
        {
            if (mh == IntPtr.Zero || acabo) return 0;
            int ch = formato.Channels;
            int pide = d.Length - d.Length % ch, total = 0;
            fixed (float* p = d)
                while (total < pide)
                {
                    UIntPtr hecho;
                    int rc = mpg123_read(mh, p + total, (UIntPtr)((pide - total) * 4), out hecho);
                    total += (int)((ulong)hecho / 4);
                    if (rc == NEW_FORMAT) continue;
                    if (rc != OK) { acabo = true; break; }   // DONE o un error: hasta aqui
                    if ((ulong)hecho == 0) break;
                }
            return total;
        }

        public void Dispose()
        {
            if (mh == IntPtr.Zero) return;
            mpg123_close(mh); mpg123_delete(mh); mh = IntPtr.Zero;
        }
    }
}

// LA CADENA NUEVA: TODO EL MP3 (CADENA_NUEVA.md §1.3). Hoy (Nativo/DescompresorMp3.cs) libmpg123 da enteros de 16 bits: lo que pasa de 0 dBFS
// se recorta y todo se redondea antes del nivelado (medido: un master a -8,7 LUFS llega a +3,0..+3,6 dBFS en coma flotante, y en 16 bits recorta
// de 700 a 3900 muestras cada 30 s). Para la cadena nueva:
//   - RocolaOsDescompresorFlotante: libmpg123 en coma flotante de 32 bits (MPG123_ENC_FLOAT_32, como hacia el motor retirado Motor/Mpg123.cs),
//     en modo «alimentar» con los cuadros del lector de XP (Plataforma/Mp3Xp.cs: el mismo indice, los mismos saltos); sin libmpg123 (o sin su
//     salida en coma flotante: se prueba una vez con el MP3 de medir de RellenoMp3), NLayer en coma flotante (MotorNLayer de FlotanteNLayer.cs).
//     Ni recorte ni redondeo: lo que da el descompresor llega tal cual a la cadena. El primer cuadro, si es una cabecera (Xing, Info o VBRI), no
//     se le da al descompresor: no es musica (y asi el retraso del descompresor es el mismo en todos los MP3).
//   - SIN HUECOS (RocolaOsGapless) con las TRES marcas, en MPEG-1, MPEG-2 y MPEG-2.5: la etiqueta LAME (Xing/Info, con su CRC, como hoy en
//     RellenoMp3.cs, pero tambien en MPEG-2/2.5), el iTunSMPB de iTunes (en un COMM de la etiqueta ID3v2: retraso, relleno y muestras de la
//     musica) y la cabecera VBRI de Fraunhofer (solo dice el retraso: se quita el principio y el final queda como viene). Se recorta con la
//     FuenteSinRelleno de XP (asi saltar los silencios largos, el fundido y la duracion siguen como hoy): fuera el retraso del codificador mas
//     el del descompresor (MEDIDO con este mismo descompresor y el MP3 de medir de RellenoMp3, una vez por sesion) y corte al final de la
//     musica. Solo si lo que dice cuadra con lo que hay en el archivo (como RellenoMp3.Cuadra, dos cuadros de margen).
//   - R1 (el relleno de los huecos, agudos_mp3) va en coma flotante: RocolaOsRellenoHuecos ya suma en coma flotante si el descompresor la da.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NAudio.Wave;

namespace Rocola
{
    // LIBMPG123 EN COMA FLOTANTE (o NLayer en coma flotante si no esta)
    internal sealed unsafe class RocolaOsDescompresorFlotante : IMp3FrameDecompressor
    {
        private const string Lib = "libmpg123.so.0";
        [DllImport(Lib)] private static extern int mpg123_init();
        [DllImport(Lib)] private static extern IntPtr mpg123_new(IntPtr decoder, out int error);
        [DllImport(Lib)] private static extern int mpg123_param(IntPtr mh, int type, long value, double fvalue);
        [DllImport(Lib)] private static extern int mpg123_format_none(IntPtr mh);
        [DllImport(Lib)] private static extern int mpg123_format(IntPtr mh, long rate, int channels, int encodings);
        [DllImport(Lib)] private static extern int mpg123_open_feed(IntPtr mh);
        [DllImport(Lib)] private static extern int mpg123_decode(IntPtr mh, byte* inmemory, UIntPtr inmemsize, byte* outmemory, UIntPtr outmemsize, out UIntPtr done);
        [DllImport(Lib)] private static extern int mpg123_getformat(IntPtr mh, out long rate, out int channels, out int encoding);
        [DllImport(Lib)] private static extern int mpg123_close(IntPtr mh);
        [DllImport(Lib)] private static extern void mpg123_delete(IntPtr mh);
        private const int ADD_FLAGS = 2, REMOVE_FLAGS = 13, RESYNC_LIMIT = 14, QUIET = 0x20, GAPLESS = 0x40, FORCE_FLOAT = 0x400, ENC_FLOAT_32 = 0x200;
        private const int OK = 0, NEW_FORMAT = -11;
        private static int estado;   // 0 sin probar, 1 libmpg123 en coma flotante, -1 NLayer
        private static readonly object candado = new object();
        internal static string Motor { get { return Lista() ? "libmpg123 (coma flotante)" : "NLayer (coma flotante)"; } }
        internal static bool ConMpg123 { get { return Lista(); } }
        private static bool Lista()
        {
            if (estado != 0) return estado > 0;
            lock (candado)
            {
                if (estado == 0)
                {
                    int e = -1;
                    try { if (Environment.GetEnvironmentVariable("ROCOLA_SIN_MPG123") != "1" && mpg123_init() == OK && PruebaFlotante()) e = 1; } catch { e = -1; }
                    estado = e;
                }
                return estado > 0;
            }
        }
        // ¿Esta libmpg123 da coma flotante? (una compilada solo con enteros no) Con el MP3 de medir de RellenoMp3: algo sale y en coma flotante.
        private static bool PruebaFlotante()
        {
            IntPtr h = Abre(44100, 2);
            if (h == IntPtr.Zero) return false;
            try
            {
                byte[] clip = RellenoMp3.Clip();
                byte[] o = new byte[1 << 20];
                int total = 0;
                fixed (byte* pc = clip) fixed (byte* po = o)
                {
                    UIntPtr hecho;
                    int rc = mpg123_decode(h, pc, (UIntPtr)clip.Length, po, (UIntPtr)o.Length, out hecho);
                    total += (int)(ulong)hecho;
                    while ((rc == NEW_FORMAT || rc == OK) && total < o.Length)
                    {
                        rc = mpg123_decode(h, null, UIntPtr.Zero, po + total, (UIntPtr)(o.Length - total), out hecho);
                        if ((ulong)hecho == 0 && rc != NEW_FORMAT) break;
                        total += (int)(ulong)hecho;
                    }
                }
                long tasa; int ch, enc;
                return total > 0 && mpg123_getformat(h, out tasa, out ch, out enc) == OK && enc == ENC_FLOAT_32;
            }
            finally { mpg123_close(h); mpg123_delete(h); }
        }
        private static IntPtr Abre(int tasa, int canales)
        {
            int err; IntPtr h = mpg123_new(IntPtr.Zero, out err);
            if (h == IntPtr.Zero) return IntPtr.Zero;
            mpg123_param(h, ADD_FLAGS, QUIET | FORCE_FLOAT, 0);
            mpg123_param(h, REMOVE_FLAGS, GAPLESS, 0);   // el retraso y el relleno los quita la rocola (RocolaOsGapless), como hoy
            mpg123_param(h, RESYNC_LIMIT, -1, 0);
            mpg123_format_none(h);
            if (mpg123_format(h, tasa, canales, ENC_FLOAT_32) != OK || mpg123_open_feed(h) != OK) { mpg123_delete(h); return IntPtr.Zero; }
            return h;
        }

        private IntPtr mh;
        private readonly MotorNLayer nlayer;
        private readonly WaveFormat salida;
        private readonly int tasa, canales;
        private bool primero = true;
        internal int CabecerasSaltadas;   // cuadros de cabecera (Xing, Info, VBRI) que no se le dieron
        internal RocolaOsDescompresorFlotante(WaveFormat mp3)
        {
            tasa = mp3.SampleRate; canales = mp3.Channels;
            salida = WaveFormat.CreateIeeeFloatWaveFormat(tasa, canales);
            if (Lista()) mh = Abre(tasa, canales);
            if (mh == IntPtr.Zero) nlayer = new MotorNLayer(mp3);
        }
        public WaveFormat OutputFormat { get { return salida; } }
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            if (frame == null) return 0;
            if (primero)
            {
                primero = false;
                if (RocolaOsGapless.EsCabecera(frame.RawData, 0, frame.FrameLength)) { CabecerasSaltadas++; return 0; }
            }
            if (nlayer != null) return nlayer.DecompressFrame(frame, dest, destOffset);
            if (mh == IntPtr.Zero) return 0;
            int cabe = dest.Length - destOffset, total = 0;
            fixed (byte* f = frame.RawData)
            fixed (byte* d = dest)
            {
                UIntPtr hecho;
                int rc = mpg123_decode(mh, f, (UIntPtr)frame.FrameLength, d + destOffset, (UIntPtr)cabe, out hecho);
                total += (int)(ulong)hecho;
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
            // (un salto: el lector vuelve a dar cuadros desde unos antes; la cabecera solo se salta al principio del archivo)
            if (nlayer != null) { nlayer.Reset(); return; }
            if (mh != IntPtr.Zero) { mpg123_close(mh); mpg123_delete(mh); mh = IntPtr.Zero; }
            if (Lista()) mh = Abre(tasa, canales);
        }
        public void Dispose()
        {
            if (mh != IntPtr.Zero) { try { mpg123_close(mh); mpg123_delete(mh); } catch { } mh = IntPtr.Zero; }
        }
    }

    // LO QUE DICE UN MP3 DE SU RETRASO Y SU RELLENO (las tres marcas)
    internal struct RocolaOsMarcaGapless
    {
        internal string Marca;          // «LAME», «iTunSMPB», «VBRI» (null: ninguna)
        internal int Retraso;           // el del codificador (muestras por canal)
        internal int Relleno;           // el del final (-1: no se sabe; VBRI)
        internal long Muestras;         // las de la musica (-1: no se sabe; VBRI)
        internal long Cuadros;          // los cuadros que dice la marca (-1: no lo dice)
        internal int Tasa, PorCuadro;   // del primer cuadro
        internal int Version;           // 1, 2 o 25
    }

    internal static class RocolaOsGapless
    {
        internal const int ExtraMaximo = 4608;

        // ¿el cuadro que empieza en b[i] es una cabecera (Xing, Info o VBRI) en vez de musica?
        internal static bool EsCabecera(byte[] b, int i, int n)
        {
            int side, x;
            if (!Cuadro(b, i, n, out side)) return false;
            bool crc = (b[i + 1] & 1) == 0;
            foreach (int o in new int[] { i + 4 + side, i + 4 + (crc ? 2 : 0) + side })
                if (o + 4 <= n && (Es(b, o, "Xing") || Es(b, o, "Info"))) return true;
            x = i + 4 + 32;
            return x + 4 <= n && Es(b, x, "VBRI");
        }
        private static bool Es(byte[] b, int o, string s) { for (int k = 0; k < 4; k++) if (b[o + k] != s[k]) return false; return true; }
        // una cabecera de capa III: su version (1, 2 o 25), tasa, muestras por cuadro y lo que mide su informacion lateral
        private static bool Cuadro(byte[] b, int i, int n, out int side)
        {
            side = 0;
            if (i + 4 > n || b[i] != 0xFF || (b[i + 1] & 0xE0) != 0xE0) return false;
            int ver = (b[i + 1] >> 3) & 3, capa = (b[i + 1] >> 1) & 3;
            if (ver == 1 || capa != 1) return false;   // (1: reservada) solo capa III
            int tasaIdx = (b[i + 2] >> 2) & 3, br = b[i + 2] >> 4;
            if (tasaIdx == 3 || br == 0 || br == 15) return false;
            bool mono = ((b[i + 3] >> 6) & 3) == 3;
            side = ver == 3 ? (mono ? 17 : 32) : (mono ? 9 : 17);
            return true;
        }
        private static int TasaDe(byte[] b, int i)
        {
            int ver = (b[i + 1] >> 3) & 3, idx = (b[i + 2] >> 2) & 3;
            int[] t1 = { 44100, 48000, 32000 };
            int t = idx < 3 ? t1[idx] : 0;
            return ver == 3 ? t : ver == 2 ? t / 2 : t / 4;
        }

        // LEE LAS MARCAS de un archivo (los primeros bytes: la etiqueta ID3v2 y el primer cuadro). LAME manda; si no, iTunSMPB; si no, VBRI.
        internal static bool Lee(string ruta, out RocolaOsMarcaGapless m)
        {
            m = new RocolaOsMarcaGapless();
            try
            {
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    byte[] h = new byte[10];
                    long ini = 0; byte[] id3 = null;
                    if (LeeTodo(fs, h, 10) == 10 && h[0] == 'I' && h[1] == 'D' && h[2] == '3')
                    {
                        int tam = (h[6] & 0x7F) << 21 | (h[7] & 0x7F) << 14 | (h[8] & 0x7F) << 7 | (h[9] & 0x7F);
                        ini = 10 + tam + ((h[5] & 0x10) != 0 ? 10 : 0);
                        if (tam > 0 && tam <= 512 * 1024) { id3 = new byte[10 + tam]; Array.Copy(h, id3, 10); fs.Position = 10; LeeTodo(fs, id3, 10, tam); }
                    }
                    if (ini >= fs.Length) return false;
                    fs.Position = ini;
                    byte[] d = new byte[8192];
                    int n = LeeTodo(fs, d, 0, d.Length);
                    return Lee(id3, d, n, out m);
                }
            }
            catch { m = new RocolaOsMarcaGapless(); return false; }
        }
        internal static bool Lee(byte[] id3, byte[] d, int n, out RocolaOsMarcaGapless m)
        {
            m = new RocolaOsMarcaGapless();
            int i = -1, side = 0;
            for (int k = 0; k + 4 <= n && k < 4096; k++) if (Cuadro(d, k, n, out side)) { i = k; break; }
            if (i < 0) return false;
            int ver = (d[i + 1] >> 3) & 3;
            m.Tasa = TasaDe(d, i); m.PorCuadro = ver == 3 ? 1152 : 576; m.Version = ver == 3 ? 1 : ver == 2 ? 2 : 25;
            if (Lame(d, i, n, side, ref m)) return true;
            if (id3 != null && ITunes(id3, ref m)) return true;
            if (Vbri(d, i, n, ref m)) return true;
            return false;
        }
        // LA ETIQUETA LAME (como RellenoMp3.Cuadro, en las tres versiones): Xing/Info con la cuenta de cuadros, el nombre de LAME o de ffmpeg y su CRC
        private static bool Lame(byte[] d, int i, int n, int side, ref RocolaOsMarcaGapless m)
        {
            bool crc = (d[i + 1] & 1) == 0;
            int x = i + 4 + (crc ? 2 : 0) + side;
            if (x + 8 > n) return false;
            if (!(Es(d, x, "Xing") || Es(d, x, "Info")))
            {
                x = i + 4 + side;   // (hay quien no cuenta el CRC)
                if (x + 8 > n || !(Es(d, x, "Xing") || Es(d, x, "Info"))) return false;
            }
            int flags = Be32(d, x + 4), p = x + 8;
            if ((flags & 1) == 0 || p + 4 > n) return false;
            long cuadros = (uint)Be32(d, p); p += 4;
            if ((flags & 2) != 0) p += 4;
            if ((flags & 4) != 0) p += 100;
            if ((flags & 8) != 0) p += 4;
            if (p + 36 > n) return false;
            bool nombre = (d[p] == 'L' && d[p + 1] == 'A' && d[p + 2] == 'M' && d[p + 3] == 'E')
                       || (d[p] == 'L' && d[p + 1] == 'a' && d[p + 2] == 'v' && (d[p + 3] == 'c' || d[p + 3] == 'f'));
            if (!nombre || i + 190 > n) return false;
            int guardado = (d[p + 34] << 8) | d[p + 35];
            byte[] cab = new byte[190];
            Array.Copy(d, i, cab, 0, 190);
            if (p + 34 - i < 190) cab[p + 34 - i] = 0;
            if (p + 35 - i < 190) cab[p + 35 - i] = 0;
            if (RellenoMp3.Crc16(cab, 0, 190) != guardado) return false;
            int retraso = (d[p + 21] << 4) | (d[p + 22] >> 4), relleno = ((d[p + 22] & 0x0F) << 8) | d[p + 23];
            long muestras = cuadros * m.PorCuadro - retraso - relleno;
            if (cuadros <= 0 || muestras <= 0) return false;
            m.Marca = "LAME"; m.Retraso = retraso; m.Relleno = relleno; m.Cuadros = cuadros; m.Muestras = muestras;
            return true;
        }
        // LA CABECERA VBRI DE FRAUNHOFER: 32 bytes detras de la cabecera del cuadro; version, retraso, calidad, bytes y cuadros. Sin relleno.
        private static bool Vbri(byte[] d, int i, int n, ref RocolaOsMarcaGapless m)
        {
            int x = i + 4 + 32;
            if (x + 18 > n || !Es(d, x, "VBRI")) return false;
            int retraso = (d[x + 6] << 8) | d[x + 7];
            long cuadros = (uint)Be32(d, x + 14);
            if (retraso <= 0 || retraso > 4608) return false;
            m.Marca = "VBRI"; m.Retraso = retraso; m.Relleno = -1; m.Muestras = -1; m.Cuadros = cuadros > 0 ? cuadros : -1;
            return true;
        }
        // EL iTunSMPB DE iTUNES: un COMM (ID3v2.3/2.4) o COM (2.2) con la descripcion «iTunSMPB» y el texto « 00000000 RETRASO RELLENO MUESTRAS ...»
        // en hexadecimal
        internal static bool ITunes(byte[] t, ref RocolaOsMarcaGapless m)
        {
            try
            {
                if (t == null || t.Length < 10 || t[0] != 'I' || t[1] != 'D' || t[2] != '3') return false;
                int v = t[3];
                int fin = Math.Min(t.Length, 10 + ((t[6] & 0x7F) << 21 | (t[7] & 0x7F) << 14 | (t[8] & 0x7F) << 7 | (t[9] & 0x7F)));
                int p = 10;
                if ((t[5] & 0x40) != 0 && v >= 3)
                {   // la cabecera extendida
                    int ext = v == 4 ? ((t[p] & 0x7F) << 21 | (t[p + 1] & 0x7F) << 14 | (t[p + 2] & 0x7F) << 7 | (t[p + 3] & 0x7F)) : (Be32(t, p) + 4);
                    p += ext;
                }
                int cab = v == 2 ? 6 : 10;
                while (p + cab <= fin)
                {
                    if (t[p] == 0) break;
                    string id = v == 2 ? Encoding.ASCII.GetString(t, p, 3) : Encoding.ASCII.GetString(t, p, 4);
                    int tam = v == 2 ? (t[p + 3] << 16 | t[p + 4] << 8 | t[p + 5])
                            : v == 4 ? ((t[p + 4] & 0x7F) << 21 | (t[p + 5] & 0x7F) << 14 | (t[p + 6] & 0x7F) << 7 | (t[p + 7] & 0x7F)) : Be32(t, p + 4);
                    int datos = p + cab;
                    if (tam <= 0 || datos + tam > fin) break;
                    if ((id == "COMM" || id == "COM") && tam > 5)
                    {
                        int enc = t[datos];
                        string desc, texto;
                        if (Comentario(t, datos + 4, datos + tam, enc, out desc, out texto) && desc == "iTunSMPB" && SmPb(texto, ref m)) return true;
                    }
                    p = datos + tam;
                }
            }
            catch { }
            return false;
        }
        private static bool Comentario(byte[] t, int p, int fin, int enc, out string desc, out string texto)
        {
            desc = texto = null;
            bool ancho = enc == 1 || enc == 2;
            int q = p;
            if (ancho) { while (q + 1 < fin && !(t[q] == 0 && t[q + 1] == 0)) q += 2; }
            else { while (q < fin && t[q] != 0) q++; }
            if (q >= fin) return false;
            desc = Texto(t, p, q - p, enc).Trim();
            int r = q + (ancho ? 2 : 1);
            texto = Texto(t, r, fin - r, enc).TrimEnd('\0').Trim();
            return true;
        }
        private static string Texto(byte[] t, int p, int n, int enc)
        {
            if (n <= 0) return "";
            if (enc == 0) return Encoding.Latin1.GetString(t, p, n);
            if (enc == 3) return Encoding.UTF8.GetString(t, p, n);
            if (enc == 2) return Encoding.BigEndianUnicode.GetString(t, p, n & ~1);
            // UTF-16 con su marca
            if (n >= 2 && t[p] == 0xFE && t[p + 1] == 0xFF) return Encoding.BigEndianUnicode.GetString(t, p + 2, (n - 2) & ~1);
            if (n >= 2 && t[p] == 0xFF && t[p + 1] == 0xFE) return Encoding.Unicode.GetString(t, p + 2, (n - 2) & ~1);
            return Encoding.Unicode.GetString(t, p, n & ~1);
        }
        internal static bool SmPb(string texto, ref RocolaOsMarcaGapless m)
        {
            if (texto == null) return false;
            string[] c = texto.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (c.Length < 4) return false;
            long retraso, relleno, muestras;
            if (!long.TryParse(c[1], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out retraso)
                || !long.TryParse(c[2], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out relleno)
                || !long.TryParse(c[3], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out muestras)) return false;
            if (retraso < 0 || retraso > 8192 || relleno < 0 || relleno > 8192 || muestras <= 0) return false;
            m.Marca = "iTunSMPB"; m.Retraso = (int)retraso; m.Relleno = (int)relleno; m.Muestras = muestras; m.Cuadros = -1;
            return true;
        }
        private static int Be32(byte[] d, int p) { return (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3]; }
        private static int LeeTodo(Stream s, byte[] b, int n) { return LeeTodo(s, b, 0, n); }
        private static int LeeTodo(Stream s, byte[] b, int o, int n)
        {
            int hecho = 0;
            while (hecho < n) { int r = s.Read(b, o + hecho, n - hecho); if (r <= 0) break; hecho += r; }
            return hecho;
        }

        // ¿Cuadra con lo que hay en el archivo? 'cuadrosLector' = los cuadros que cuenta el lector de XP (con o sin la cabecera). LAME y VBRI dicen
        // sus cuadros (dos de margen, como RellenoMp3.Cuadra); iTunSMPB, el retraso, el relleno y la musica: tienen que sumar lo que hay.
        internal static bool Cuadra(RocolaOsMarcaGapless m, long cuadrosLector)
        {
            if (m.Marca == null || m.PorCuadro <= 0 || cuadrosLector <= 0) return false;
            if (m.Cuadros > 0) return Math.Abs(cuadrosLector - m.Cuadros) <= 2;
            if (m.Muestras > 0 && m.Relleno >= 0)
            {
                double dice = (m.Retraso + m.Relleno + m.Muestras) / (double)m.PorCuadro;
                return Math.Abs(cuadrosLector - dice) <= 2.0;
            }
            return true;
        }
    }

    public partial class Form1
    {
        // EL RETRASO DEL DESCOMPRESOR EN COMA FLOTANTE (como MideRelleno de XP, por este mismo camino): int.MinValue sin medir; -1 no se pudo
        private static int rocolaOsExtraFlotante = int.MinValue;
        private static readonly object rocolaOsExtraCerrojo = new object();
        internal static int RocolaOsExtraFlotante()
        {
            lock (rocolaOsExtraCerrojo)
            {
                if (rocolaOsExtraFlotante != int.MinValue) return rocolaOsExtraFlotante;
                int extra = -1; string porque = "";
                try
                {
                    byte[] clip = RellenoMp3.Clip();
                    EtiquetaLame e;
                    if (!RellenoMp3.Lee(clip, clip.Length, out e)) porque = "el MP3 de medir no tiene etiqueta LAME";
                    else
                    {
                        using (MemoryStream ms = new MemoryStream(clip))
                        using (Mp3FileReader r = new Mp3FileReader(ms, delegate (WaveFormat wf) { return new RocolaOsDescompresorFlotante(wf); }))
                        {
                            NAudio.Wave.SampleProviders.SampleChannel sp = new NAudio.Wave.SampleProviders.SampleChannel(r, false);
                            int ch = sp.WaveFormat.Channels;
                            float[] x = new float[(RellenoMp3.ClicMuestras + 4 * 1152 + RellenoMp3.ExtraMaximo + 4096) * ch];
                            int n = 0, k;
                            while (n < x.Length && (k = sp.Read(x, n, x.Length - n)) > 0) n += k;
                            extra = RellenoMp3.ExtraDelClic(x, n, ch, e.Retraso, out porque);
                        }
                    }
                }
                catch (Exception ex) { porque = ex.GetType().Name + ": " + ex.Message; extra = -1; }
                rocolaOsExtraFlotante = extra;
                RocolaOs.Apunta(RocolaOsCadenasNuevas.Nombre, "el descompresor de la cadena nueva: " + RocolaOsDescompresorFlotante.Motor
                    + (extra >= 0 ? ", retrasa " + extra + " muestras (sin huecos: en uso)" : ", sin huecos NO (" + porque + ")"));
                return extra;
            }
        }

        // EL MP3 EN COMA FLOTANTE PARA QUE SUENE (como FileAudioSource de XP: desde la memoria si el calentador ya lo leyo, si no del archivo; el
        // lector ligero de XP). Con agudos_mp3 y una cancion que trae huecos, el relleno de los huecos (R1) va dentro, en coma flotante.
        private sealed class RocolaOsFuenteFlotante : IAudioSource
        {
            private readonly WaveFormat formato;
            private readonly Mp3FileReader mp3;
            private readonly NAudio.Wave.SampleProviders.SampleChannel canal;
            private MemoryStream enMemoria;
            private readonly Stream propio;
            internal readonly bool ConRelleno;
            internal RocolaOsDescompresorFlotante Descompresor;
            internal RocolaOsFuenteFlotante(string path, bool conRelleno, bool desdeMemoria)
            {
                IMp3FrameDecompressor hecho = null; bool rel = false;
                Mp3FileReader.FrameDecompressorBuilder armador = delegate (WaveFormat wf)
                {
                    RocolaOsDescompresorFlotante d = new RocolaOsDescompresorFlotante(wf);
                    Descompresor = d; hecho = d;
                    if (conRelleno && RocolaOsHuecos.TraeHuecos(path))
                    {
                        try { hecho = new RocolaOsRellenoHuecos(d, (uint)StringComparer.Ordinal.GetHashCode(path) | 1u); rel = true; }
                        catch { hecho = d; }
                    }
                    return hecho;
                };
                MemoryStream ms = desdeMemoria ? CancionEnMemoria(path) : null;
                Mp3FileReader m = null; Stream archivo = null;
                try
                {
                    if (ms != null)
                    {
                        try { m = LectorMp3Ligero.Posible ? LectorMp3Ligero.Abre(ms, armador) : new Mp3FileReader(ms, armador); }
                        catch { if (hecho != null) { try { hecho.Dispose(); } catch { } } hecho = null; m = null; ms = null; }
                    }
                    if (m == null)
                    {
                        archivo = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536);
                        m = LectorMp3Ligero.Posible ? LectorMp3Ligero.Abre(archivo, armador) : new Mp3FileReader(archivo, armador);
                    }
                    canal = new NAudio.Wave.SampleProviders.SampleChannel(m, false);
                    mp3 = m; m = null; enMemoria = ms; propio = archivo; archivo = null;
                }
                finally
                {
                    if (m != null) { try { m.Dispose(); } catch { } }
                    else if (mp3 == null && hecho != null) { try { hecho.Dispose(); } catch { } }
                    if (archivo != null) { try { archivo.Dispose(); } catch { } }
                }
                ConRelleno = rel;
                formato = FormatoAudio.DeArchivo(canal.WaveFormat, path);
            }
            public WaveFormat WaveFormat { get { return formato; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count) { return canal.Read(buffer, offset, count); }
            public float Volume { set { canal.Volume = value; } }
            public TimeSpan CurrentTime { get { return TiempoLector(mp3, false); } }
            public TimeSpan TotalTime { get { return TiempoLector(mp3, true); } }
            public void Salta(TimeSpan donde) { SaltaCuadro(mp3, donde); }
            public void Dispose()
            {
                try { mp3.Dispose(); } catch { }
                if (propio != null) { try { propio.Dispose(); } catch { } }
                enMemoria = null;
            }
        }

        // SIN HUECOS: la fuente ya en dos canales y sellada, recortada con la FuenteSinRelleno de XP si alguna de las tres marcas cuadra (y el
        // descompresor se pudo medir). 'como' dice que marca (para la bitacora y las pruebas).
        private static IAudioSource RocolaOsSinHuecos(string path, IAudioSource s, out string como)
        {
            como = "sin marca";
            try
            {
                RocolaOsMarcaGapless m;
                if (!RocolaOsGapless.Lee(path, out m)) return s;
                como = m.Marca;
                if (m.Tasa != s.WaveFormat.SampleRate) { como += " (otra tasa: no)"; return s; }
                long cuadros = (long)Math.Round(s.TotalTime.TotalSeconds * m.Tasa / m.PorCuadro);
                if (!RocolaOsGapless.Cuadra(m, cuadros)) { como += " (no cuadra con el archivo: no)"; return s; }
                int extra = RocolaOsExtraFlotante();
                if (extra < 0) { como += " (sin medir el descompresor: no)"; return s; }
                long fuera = m.Retraso + extra;
                long musica = m.Muestras > 0 ? m.Muestras : Math.Max(0L, (long)Math.Round(s.TotalTime.TotalSeconds * m.Tasa) - fuera);
                if (musica <= 0) { como += " (sin musica: no)"; return s; }
                return new FuenteSinRelleno(s, fuera, musica);
            }
            catch (Exception ex) { como = "error " + ex.GetType().Name; return s; }
        }
    }
}

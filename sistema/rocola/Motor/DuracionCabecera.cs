// LO QUE DURA CADA CANCION, LEIDO DE SU CABECERA (de Form1.Biblioteca.cs de XP: DuracionPorCabecera, tal cual). XP lo hace en su paseo de
// fondo al arrancar (PublicaDuraciones); aqui, al leer la biblioteca, para las que aun no tienen duracion y con un tope de tiempo: unos
// kilobytes por archivo. Lo que se averigua va a durFast y se guarda en su archivo, como en XP; la siguiente vez ya no se lee. Las que no
// caben en el tope (o no son MP3 con cabecera util) siguen sin duracion hasta que el repaso las mida, igual que en XP.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        private const int TopeDuracionesMs = 4000;
        private void DuracionesQueFaltan(List<Track> pistas)
        {
            Stopwatch t = Stopwatch.StartNew();
            int nuevas = 0;
            foreach (Track tr in pistas)
            {
                if (tr == null || tr.Path == null || SegundosDe(tr.Path) > 0) continue;
                if (t.ElapsedMilliseconds > TopeDuracionesMs) break;
                float seg = DuracionPorCabecera(tr.Path);
                if (seg <= 0.01f) continue;
                durFast[tr.Path] = seg; nuevas++;
                lock (durFastLock) durFastPend.Add(seg.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + tr.Path);
            }
            if (nuevas > 0) SaveDurFast();
        }
        private static readonly int[] BrMpeg1 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
        private static readonly int[] BrMpeg2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };
        private static readonly int[] SrMpeg1 = { 44100, 48000, 32000, 0 };
        private static readonly int[] SrMpeg2 = { 22050, 24000, 16000, 0 };
        private static readonly int[] SrMpeg25 = { 11025, 12000, 8000, 0 };
        private static float DuracionPorCabecera(string ruta)
        {
            try
            {
                if (Path.GetExtension(ruta).ToLowerInvariant() != ".mp3") return 0f;   // solo MP3; el resto, por el camino lento
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
                {
                    long largo = fs.Length;
                    if (largo < 256) return 0f;
                    byte[] cab = new byte[10];
                    if (fs.Read(cab, 0, 10) != 10) return 0f;
                    // 1) LA ETIQUETA DE TEXTO del principio (ID3v2), que puede llevar dentro la caratula y
                    //    medir cientos de kilobytes. Su tamano viene en la propia cabecera: se salta de golpe.
                    long ini = 0;
                    if (cab[0] == (byte)'I' && cab[1] == (byte)'D' && cab[2] == (byte)'3')
                    {
                        // El tamano va en 4 bytes de 7 bits (el bit alto de cada uno no cuenta).
                        int tam = ((cab[6] & 0x7F) << 21) + ((cab[7] & 0x7F) << 14)
                                + ((cab[8] & 0x7F) << 7) + (cab[9] & 0x7F);
                        ini = 10 + tam;
                        if ((cab[5] & 0x10) != 0) ini += 10;   // con pie de etiqueta, diez bytes mas
                        if (ini <= 0 || ini >= largo) return 0f;
                    }
                    // 2) EL PRIMER FRAME. Se leen unos kilobytes desde ahi y se busca la marca de sincronismo.
                    //    Se tolera basura por delante (hay archivos con relleno entre la etiqueta y el audio).
                    fs.Position = ini;
                    byte[] buf = new byte[8192];
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n < 64) return 0f;
                    for (int i = 0; i + 4 <= n; i++)
                    {
                        if (buf[i] != 0xFF || (buf[i + 1] & 0xE0) != 0xE0) continue;
                        int verBits = (buf[i + 1] >> 3) & 3;      // 3=MPEG1  2=MPEG2  0=MPEG2.5  (1 no existe)
                        int capa = (buf[i + 1] >> 1) & 3;         // 1 = capa III
                        if (verBits == 1 || capa != 1) continue;
                        int iBr = (buf[i + 2] >> 4) & 15, iSr = (buf[i + 2] >> 2) & 3;
                        int pad = (buf[i + 2] >> 1) & 1, modo = (buf[i + 3] >> 6) & 3;
                        bool mpeg1 = (verBits == 3);
                        int br = (mpeg1 ? BrMpeg1[iBr] : BrMpeg2[iBr]) * 1000;
                        int sr = mpeg1 ? SrMpeg1[iSr] : (verBits == 2 ? SrMpeg2[iSr] : SrMpeg25[iSr]);
                        if (br <= 0 || sr <= 0) continue;          // valores reservados: no era un frame
                        int porFrame = mpeg1 ? 1152 : 576;         // muestras que trae cada frame
                        bool mono = (modo == 3);
                        // 3) LA CUENTA EXACTA DE FRAMES, si el archivo la trae. Va justo detras de la
                        //    informacion lateral del primer frame, cuyo tamano depende de version y canales.
                        int lado = mpeg1 ? (mono ? 17 : 32) : (mono ? 9 : 17);
                        int xi = i + 4 + lado;
                        if (xi + 12 <= n)
                        {
                            bool xing = (buf[xi] == (byte)'X' && buf[xi + 1] == (byte)'i' && buf[xi + 2] == (byte)'n' && buf[xi + 3] == (byte)'g')
                                     || (buf[xi] == (byte)'I' && buf[xi + 1] == (byte)'n' && buf[xi + 2] == (byte)'f' && buf[xi + 3] == (byte)'o');
                            if (xing && (buf[xi + 7] & 1) != 0)
                            {
                                long frames = ((long)buf[xi + 8] << 24) + ((long)buf[xi + 9] << 16) + ((long)buf[xi + 10] << 8) + buf[xi + 11];
                                if (frames > 0)
                                {
                                    double seg = (double)frames * porFrame / sr;
                                    if (seg > 0.05 && seg < 36000) return (float)seg;
                                }
                            }
                        }
                        int vi = i + 4 + 32;   // VBRI (Fraunhofer) va siempre a 32 bytes del frame
                        if (vi + 18 <= n && buf[vi] == (byte)'V' && buf[vi + 1] == (byte)'B' && buf[vi + 2] == (byte)'R' && buf[vi + 3] == (byte)'I')
                        {
                            long frames = ((long)buf[vi + 14] << 24) + ((long)buf[vi + 15] << 16) + ((long)buf[vi + 16] << 8) + buf[vi + 17];
                            if (frames > 0)
                            {
                                double seg = (double)frames * porFrame / sr;
                                if (seg > 0.05 && seg < 36000) return (float)seg;
                            }
                        }
                        // 4) SIN ETIQUETA: si el archivo es de tasa FIJA la cuenta es una division. Se comprueba
                        //    que el frame siguiente cae donde debe y trae la misma tasa; si no, puede ser de tasa
                        //    variable sin etiqueta y ahi no se puede adivinar: que lo mida el camino lento.
                        int tamFrame = (mpeg1 ? 144 : 72) * br / sr + pad;
                        if (tamFrame < 24) continue;
                        int sig = i + tamFrame;
                        if (sig + 4 > n) return 0f;
                        if (buf[sig] != 0xFF || (buf[sig + 1] & 0xE0) != 0xE0) return 0f;
                        if (((buf[sig + 2] >> 4) & 15) != iBr) return 0f;   // otra tasa -> variable sin etiqueta
                        long fin = largo;
                        // La etiqueta de texto del FINAL (ID3v1) son 128 bytes que no son audio.
                        if (largo > 128 + ini)
                        {
                            fs.Position = largo - 128;
                            byte[] tres = new byte[3];
                            if (fs.Read(tres, 0, 3) == 3 && tres[0] == (byte)'T' && tres[1] == (byte)'A' && tres[2] == (byte)'G') fin = largo - 128;
                        }
                        long audio = fin - (ini + i);
                        if (audio <= 0) return 0f;
                        double s2 = (double)audio * 8.0 / br;
                        if (s2 > 0.05 && s2 < 36000) return (float)s2;
                        return 0f;
                    }
                }
            }
            catch { }
            return 0f;
        }
    }
}

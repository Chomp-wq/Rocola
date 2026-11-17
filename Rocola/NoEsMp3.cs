using System;
using System.IO;
namespace Rocola
{
    // LOS «MP3» QUE NO LO SON (17-11-2026). En las bibliotecas de bar hay archivos de otro formato (un M4A/AAC, un OGG, un FLAC,
    // un WMA, un WAV) con el nombre cambiado a .mp3. El lector de MP3 busca cuadros en cualquier cosa; al fallar, OpenAudio caia a
    // BuildCleanMp3, que copia «los cuadros de la frecuencia dominante»: en un archivo que no es MP3 son falsos y sonaba RUIDO casi
    // al maximo por las bocinas (y el repaso podia medir basura). Ahora se mira la cabecera (sin cargar el archivo): si es de otro
    // formato, OpenAudio y LectorMedida lanzan InvalidDataException ANTES de abrir nada, y por el camino de siempre (AudioStartFailed)
    // se devuelve el credito, se marca danada y se anota en rocola_incompatibles.txt para convertirla. Los MP3 de verdad (y el MP3
    // dentro de un WAV, formato 0x55, que el lector de MP3 si toca bien) no cambian: devuelven null. Ante cualquier duda, null.
    internal static class NoEsMp3
    {
        // El nombre del formato real («M4A/AAC», «OGG», «FLAC», «WMA», «WAV») o null si es MP3 o no se sabe.
        internal static string OtroFormato(string ruta)
        {
            try
            {
                if (string.IsNullOrEmpty(ruta)) return null;
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] h = new byte[16];
                    if (Lee(fs, h, 16) < 10) return null;
                    long inicio = 0;
                    if (h[0] == 'I' && h[1] == 'D' && h[2] == '3')
                    {
                        long tam = ((h[6] & 0x7F) << 21) | ((h[7] & 0x7F) << 14) | ((h[8] & 0x7F) << 7) | (h[9] & 0x7F);
                        inicio = 10 + tam + ((h[5] & 0x10) != 0 ? 10 : 0);
                        if (inicio >= fs.Length) return null;
                        fs.Seek(inicio, SeekOrigin.Begin);
                        if (Lee(fs, h, 16) < 12) return null;
                    }
                    else fs.Seek(16, SeekOrigin.Begin);
                    if (h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p') return "M4A/AAC";
                    if (h[0] == 'O' && h[1] == 'g' && h[2] == 'g' && h[3] == 'S') return "OGG";
                    if (h[0] == 'f' && h[1] == 'L' && h[2] == 'a' && h[3] == 'C') return "FLAC";
                    if (h[0] == 0x30 && h[1] == 0x26 && h[2] == 0xB2 && h[3] == 0x75 && h[4] == 0x8E && h[5] == 0x66) return "WMA";
                    if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'A' && h[10] == 'V' && h[11] == 'E')
                        return FormatoWav(fs, inicio + 12, fs.Length);
                    return null;
                }
            }
            catch { return null; }
        }

        // Recorre los bloques del WAV hasta «fmt »: 1, 3 o 0xFFFE = «WAV»; otro (0x55 = MP3 dentro) = null.
        private static string FormatoWav(FileStream fs, long desde, long largo)
        {
            byte[] b = new byte[8];
            long pos = desde;
            for (int i = 0; i < 64 && pos + 8 <= largo; i++)
            {
                fs.Seek(pos, SeekOrigin.Begin);
                if (Lee(fs, b, 8) < 8) return null;
                long tam = (uint)(b[4] | (b[5] << 8) | (b[6] << 16) | (b[7] << 24));
                if (b[0] == 'f' && b[1] == 'm' && b[2] == 't' && b[3] == ' ')
                {
                    if (Lee(fs, b, 2) < 2) return null;
                    int fmt = b[0] | (b[1] << 8);
                    return (fmt == 1 || fmt == 3 || fmt == 0xFFFE) ? "WAV" : null;
                }
                pos += 8 + tam + (tam & 1);
            }
            return null;
        }

        private static int Lee(Stream s, byte[] b, int n)
        {
            int t = 0;
            while (t < n) { int r = s.Read(b, t, n - t); if (r <= 0) break; t += r; }
            return t;
        }

        // Lanza si el .mp3 es de otro formato (lo llama OpenAudio y LectorMedida: el mismo mensaje y la misma excepcion).
        internal static void Rechaza(string ruta)
        {
            if (ruta == null || !ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return;
            string otro = OtroFormato(ruta);
            if (otro != null) throw new InvalidDataException("no es un MP3: es " + otro + " con otro nombre");
        }
    }
}

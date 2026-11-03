// DONDE ESTAN LAS LETRAS. La rocola de XP dibuja con las letras de Windows: Microsoft Sans Serif (la de los controles; no trae negrita y
// GDI+ la engorda) y Trebuchet MS. Rocola OS las toma del disco de XP del A4 (rocola-discos las copia a /rocola/datos/letras la primera
// vez); si no estan, Arial y, al final, DejaVu Sans (la del sistema).
using System;
using System.Collections.Generic;
using System.IO;

namespace Rocola.Pantalla
{
    internal static class Letras
    {
        internal static readonly string[] Ui = { "micross.ttf", "arial.ttf", "DejaVuSans.ttf" };
        internal static readonly string[] UiNegrita = { "micross.ttf*", "arialbd.ttf", "DejaVuSans-Bold.ttf" };   // * = negrita sintetica
        internal static readonly string[] Trebuchet = { "trebuc.ttf" };
        internal static readonly string[] TrebuchetNegrita = { "trebucbd.ttf" };
        // LA LETRA NUEVA (L1, decision del 3-11-2026): Outfit para los titulos y Plus Jakarta Sans para el texto, las dos libres (SIL OFL);
        // Rocola OS las lleva en /usr/share/rocola/letras (sistema/board/rocola/rootfs_overlay), cada grosor en su archivo.
        internal static readonly string[] L1Texto = { "PlusJakartaSans-Regular.ttf" };
        internal static readonly string[] L1Medio = { "PlusJakartaSans-Medium.ttf" };
        internal static readonly string[] L1Negrita = { "PlusJakartaSans-Bold.ttf" };
        internal static readonly string[] L1Titulo = { "Outfit-Bold.ttf" };
        internal static readonly string[] L1TituloMedio = { "Outfit-Medium.ttf" };
        private static Dictionary<string, string> indice;
        private static IEnumerable<string> Carpetas()
        {
            string e = Environment.GetEnvironmentVariable("ROCOLA_LETRAS");
            if (!string.IsNullOrEmpty(e)) yield return e;
            string datos = Environment.GetEnvironmentVariable("ROCOLA_DATOS");
            if (!string.IsNullOrEmpty(datos)) yield return Path.Combine(datos, "letras");
            yield return "/rocola/datos/letras";
            yield return "/usr/share/rocola/letras";
            // al probar desde el codigo: la carpeta del sistema que va en la imagen
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                string c = Path.Combine(dir, "sistema", "board", "rocola", "rootfs_overlay", "usr", "share", "rocola", "letras");
                if (Directory.Exists(c)) { yield return c; break; }
                dir = Path.GetDirectoryName(dir.TrimEnd('/'));
            }
            yield return "/usr/share/fonts";
            yield return "/usr/local/share/fonts";
        }
        // El primero que haya de la lista (sin distinguir mayusculas); null si ninguno.
        // Cada archivo se lee una vez: el mismo arreglo cada vez (las letras guardan su fuente ya preparada por arreglo: Letra.Fuente).
        private static readonly Dictionary<string, byte[]> leidos = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private static byte[] Archivo(string ruta) { byte[] b; if (!leidos.TryGetValue(ruta, out b)) leidos[ruta] = b = File.ReadAllBytes(ruta); return b; }
        internal static byte[] Lee(string[] nombres, out bool sintetica)
        {
            sintetica = false;
            if (indice == null)
            {
                indice = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string c in Carpetas())
                    try { if (Directory.Exists(c)) foreach (string f in Directory.GetFiles(c, "*.*", SearchOption.AllDirectories)) { string n = Path.GetFileName(f); if (!indice.ContainsKey(n)) indice[n] = f; } }
                    catch { }
            }
            foreach (string n0 in nombres)
            {
                bool s = n0.EndsWith("*"); string n = s ? n0.Substring(0, n0.Length - 1) : n0;
                string r; if (indice.TryGetValue(n, out r)) try { sintetica = s; return Archivo(r); } catch { }
            }
            return null;
        }
    }
}

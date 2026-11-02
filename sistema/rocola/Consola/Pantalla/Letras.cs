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
        private static Dictionary<string, string> indice;
        private static IEnumerable<string> Carpetas()
        {
            string e = Environment.GetEnvironmentVariable("ROCOLA_LETRAS");
            if (!string.IsNullOrEmpty(e)) yield return e;
            string datos = Environment.GetEnvironmentVariable("ROCOLA_DATOS");
            if (!string.IsNullOrEmpty(datos)) yield return Path.Combine(datos, "letras");
            yield return "/rocola/datos/letras";
            yield return "/usr/share/fonts";
            yield return "/usr/local/share/fonts";
        }
        // El primero que haya de la lista (sin distinguir mayusculas); null si ninguno.
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
                string r; if (indice.TryGetValue(n, out r)) try { sintetica = s; return File.ReadAllBytes(r); } catch { }
            }
            return null;
        }
    }
}

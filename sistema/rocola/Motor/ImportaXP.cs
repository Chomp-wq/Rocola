// TRAER LO QUE LA ROCOLA DE XP YA APRENDIO (Rocola OS). La primera vez que Rocola OS encuentra la carpeta de datos de XP (rocola-discos
// la busca en los otros discos, montados en solo lectura) copia a /rocola/datos sus tablas: la sonoridad y las medidas de cada cancion,
// el volumen, el EQ, el sello de cada archivo, las duraciones, las repasadas a fondo, el volumen parejo y la bitacora del billar, las
// estadisticas, los botones, la configuracion y los modelos de IA. Las rutas de Windows ("D:\Rocola\musica\...") se traducen a las de
// Rocola OS ("/rocola/discos/sdb1/Rocola/musica/...") buscando el archivo en los discos montados; las canciones que ya no estan se
// quedan fuera. Una vez: deja rocola_importado_xp.txt con lo que hizo, y nunca pisa un archivo que Rocola OS ya tenga. No escribe nada
// en el disco de XP. El estado (creditos y cola) no se trae: es del dia.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Rocola
{
    public partial class Form1
    {
        private static readonly string[] TablasXP = {
            "rocola_sonoridad.txt", "rocola_volumen.txt", "rocola_eq.txt", "rocola_fuentes.txt", "rocola_duracion.txt", "rocola_afondo.txt",
            "rocola_billar_nivel.txt", "rocola_billar_bitacora.txt", "rocola_stats.txt", "rocola_teclas.txt", "rocola_config.txt" };
        private static readonly Regex RutaWindows = new Regex(@"[A-Za-z]:\\[^|\t\r\n]*", RegexOptions.Compiled);

        // carpetaXp: la de los datos de XP (donde esta su rocola_config.txt); discos: donde estan montados los discos (sus raices).
        public string ImportaDeXP(string carpetaXp, IList<string> discos)
        {
            string marca = RutaDatos("rocola_importado_xp.txt");
            if (carpetaXp == null || !Directory.Exists(carpetaXp) || File.Exists(marca)) return null;
            Dictionary<string, string> hechas = new Dictionary<string, string>(StringComparer.Ordinal);   // ruta de Windows -> de Linux (o null)
            Dictionary<char, string> unidad = new Dictionary<char, string>();                           // la letra de unidad -> el disco montado
            Func<string, string> traduce = delegate (string w)
            {
                string r;
                if (hechas.TryGetValue(w, out r)) return r;
                string resto = w.Substring(3).Replace('\\', '/').TrimEnd('/', ' ');
                char u = char.ToUpperInvariant(w[0]);
                string disco; r = null;
                if (unidad.TryGetValue(u, out disco)) { string c = Path.Combine(disco, resto); if (File.Exists(c) || Directory.Exists(c)) r = c; }
                if (r == null)
                    foreach (string d in discos)
                    {
                        string c = Path.Combine(d, resto);
                        if (File.Exists(c) || Directory.Exists(c)) { r = c; if (!unidad.ContainsKey(u)) unidad[u] = d; break; }
                    }
                hechas[w] = r;
                return r;
            };
            StringBuilder informe = new StringBuilder();
            informe.Append("Importado de la rocola de XP (").Append(carpetaXp).Append(") el ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\r\n");
            foreach (string nombre in TablasXP)
            {
                string origen = Path.Combine(carpetaXp, nombre), destino = RutaDatos(nombre);
                if (!File.Exists(origen)) continue;
                if (File.Exists(destino)) { informe.Append(nombre).Append(": ya habia uno de Rocola OS, no se toca\r\n"); continue; }
                int lineas = 0, traducidas = 0, fuera = 0;
                StringBuilder sb = new StringBuilder();
                try
                {
                    foreach (string linea in LeeLineasXP(origen))
                    {
                        lineas++;
                        string l = linea; bool quitar = false; int n = 0;
                        l = RutaWindows.Replace(l, delegate (Match m)
                        {
                            string t = traduce(m.Value.TrimEnd());
                            if (t == null) { quitar = true; return m.Value; }
                            n++; return t;
                        });
                        if (quitar) { fuera++; continue; }   // una cancion (o carpeta) que ya no esta
                        if (n > 0) traducidas++;
                        sb.Append(l).Append("\r\n");
                    }
                    EscribeYa(destino, sb.ToString());
                    informe.Append(nombre).Append(": ").Append(lineas).Append(" lineas, ").Append(traducidas).Append(" rutas traducidas, ").Append(fuera).Append(" fuera (ya no estan)\r\n");
                }
                catch (Exception ex) { informe.Append(nombre).Append(": no se pudo (").Append(ex.Message).Append(")\r\n"); }
            }
            // Los modelos de IA (IAS\ y los sueltos), tal cual.
            int modelos = 0;
            foreach (string dir in new[] { Path.Combine(carpetaXp, NombreCarpetaIas), carpetaXp })
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (string f in Directory.GetFiles(dir, "rocola_*.bin"))
                    {
                        string n = Path.GetFileName(f);
                        if (n.StartsWith("rocola_ia_datos", StringComparison.OrdinalIgnoreCase)) continue;   // el material de entrenar: no
                        string d = Path.Combine(CarpetaIas, n);
                        if (File.Exists(d)) continue;
                        Directory.CreateDirectory(CarpetaIas); File.Copy(f, d); modelos++;
                    }
                }
                catch { }
            informe.Append("modelos de IA: ").Append(modelos).Append("\r\n");
            foreach (KeyValuePair<char, string> kv in unidad) informe.Append("unidad ").Append(kv.Key).Append(": -> ").Append(kv.Value).Append("\r\n");
            try { EscribeYa(marca, informe.ToString()); } catch { }
            return informe.ToString();
        }

        // Escribir YA (antes de leer las tablas): a un archivo de paso, al disco (fsync) y cambio de nombre de un tiron.
        private static void EscribeYa(string ruta, string texto)
        {
            string paso = ruta + ".importando";
            using (FileStream f = new FileStream(paso, FileMode.Create, FileAccess.Write))
            {
                byte[] b = new UTF8Encoding(false).GetBytes(texto);
                f.Write(b, 0, b.Length); f.Flush(true);
            }
            File.Move(paso, ruta, true);
        }

        // Las lineas de un archivo de XP: en UTF-8 (lo que escribe la rocola); si no lo es, en Windows-1252 (por si alguno se escribio a mano).
        private static IEnumerable<string> LeeLineasXP(string ruta)
        {
            byte[] b = File.ReadAllBytes(ruta);
            string t;
            try { t = new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException) { t = Latin1De1252(b); }
            if (t.Length > 0 && t[0] == '\uFEFF') t = t.Substring(1);
            return t.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        }
        private static string Latin1De1252(byte[] b)
        {
            const string altos = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F\u0090‘’“”•–—˜™š›œ\u009DžŸ";
            char[] c = new char[b.Length];
            for (int i = 0; i < b.Length; i++) c[i] = b[i] >= 0x80 && b[i] < 0xA0 ? altos[b[i] - 0x80] : (char)b[i];
            return new string(c);
        }
    }
}

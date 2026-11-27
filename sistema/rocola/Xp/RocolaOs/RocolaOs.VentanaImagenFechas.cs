// ROCOLA OS (fase 11): LAS FECHAS PROPIAS DE LA HOJA «IMAGEN» DE CTRL+F4 (FASE11_hoja-imagen.md): leer, agregar y quitar renglones de
// rocola_fechas.txt (el formato, en RocolaOs.CieloFiestas.cs: «DD-MM;tipo;texto»). Solo texto: no pinta nada. La hoja agrega cumpleanos y
// aniversarios y quita cualquier renglon; los demas tipos (fiesta, mensaje, fuegos, quitar) los deja tal cual y se editan en Ctrl+F4 -> Archivos.
// El archivo se cambia entero y de una vez (se escribe aparte y se renombra: un apagon deja el de antes o el nuevo); los comentarios («#»), los
// renglones vacios y los que no valen se conservan donde estaban. El hilo del cielo lo vuelve a leer solo cuando cambia (RevisaFechas).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rocola
{
    internal static class RocolaOsImagenFechas
    {
        internal const int MaxTexto = 60;   // el nombre de un cumpleanos o aniversario (el archivo admite 120; en la hoja caben 60)

        // UN RENGLON QUE SE VE EN LA LISTA: su sitio en el archivo (el renglon, desde 0) y como se lee
        internal sealed class Entrada { internal int Renglon; internal string Texto; internal bool Vale; }

        // LOS RENGLONES DE LA LISTA de un texto: los que no son vacios ni comentarios
        internal static List<Entrada> Entradas(string texto)
        {
            List<Entrada> r = new List<Entrada>();
            string[] ls = Lineas(texto);
            for (int i = 0; i < ls.Length; i++)
            {
                string l = ls[i].Trim();
                if (i == 0 && l.Length > 0 && l[0] == '﻿') l = l.Substring(1).Trim();
                if (l.Length == 0 || l[0] == '#') continue;
                string[] p = l.Split(new[] { ';' }, 3);
                int d, m;
                bool vale = p.Length >= 2 && RocolaOsFechasArchivo.Fecha(p[0].Trim(), out d, out m) && Array.IndexOf(RocolaOsFechasArchivo.Tipos, p[1].Trim().ToLowerInvariant()) >= 0;
                string t;
                if (vale)
                {
                    RocolaOsFechasArchivo.Fecha(p[0].Trim(), out d, out m);
                    t = Dos(d) + "-" + Dos(m) + " · " + Tipo(p[1].Trim().ToLowerInvariant()) + (p.Length > 2 && p[2].Trim().Length > 0 ? " · " + p[2].Trim() : "");
                }
                else t = l + " (no vale)";
                r.Add(new Entrada { Renglon = i, Texto = t, Vale = vale });
            }
            return r;
        }
        internal static string Tipo(string t)
        {
            switch (t)
            {
                case "cumple": return "cumpleaños";
                case "aniversario": return "aniversario";
                case "fiesta": return "fiesta";
                case "quitar": return "quita una fiesta";
                case "mensaje": return "mensaje";
                case "fuegos": return "fuegos";
                default: return t;
            }
        }
        private static string Dos(int n) { return n.ToString("00", CultureInfo.InvariantCulture); }
        private static string[] Lineas(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return new string[0];
            string[] ls = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            // (el ultimo vacio de un texto que acaba en salto no es un renglon)
            if (ls.Length > 0 && ls[ls.Length - 1].Length == 0) Array.Resize(ref ls, ls.Length - 1);
            return ls;
        }

        // LO QUE SE PUEDE ESCRIBIR: null si vale, o por que no (para la hoja)
        internal static string NombreNoVale(string nombre)
        {
            string n = (nombre ?? "").Trim();
            if (n.Length == 0) return "Escribe un nombre.";
            if (n.Length > MaxTexto) return "Cabe un nombre de " + MaxTexto + " letras como mucho.";
            if (n.IndexOfAny(new[] { ';', '\n', '\r', '|', '#' }) >= 0 || n[0] == '﻿') return "Sin punto y coma, barra ni numeral.";
            return null;
        }
        // «DD-MM» (tambien «D-M» y «D/M»): null si vale, o por que no
        internal static string FechaNoVale(string f, out string normal)
        {
            normal = null;
            int d, m;
            if (!RocolaOsFechasArchivo.Fecha((f ?? "").Trim(), out d, out m)) return "Escribe el día y el mes, como 15-03.";
            normal = Dos(d) + "-" + Dos(m);
            return null;
        }

        // AGREGA UN RENGLON al final (tipo: «cumple» o «aniversario»); devuelve el texto nuevo, o null si no vale
        internal static string Agrega(string texto, string tipo, string fecha, string nombre)
        {
            string normal;
            if (tipo != "cumple" && tipo != "aniversario") return null;
            if (FechaNoVale(fecha, out normal) != null || NombreNoVale(nombre) != null) return null;
            StringBuilder sb = new StringBuilder(texto ?? "");
            if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');
            sb.Append(normal).Append(';').Append(tipo).Append(';').Append(nombre.Trim()).Append('\n');
            return sb.ToString();
        }
        // QUITA EL RENGLON n (el de la lista: su sitio en el archivo); devuelve el texto nuevo, o null si no hay tal renglon
        internal static string Quita(string texto, int renglon)
        {
            string[] ls = Lineas(texto);
            if (renglon < 0 || renglon >= ls.Length) return null;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ls.Length; i++) if (i != renglon) sb.Append(ls[i]).Append('\n');
            return sb.ToString();
        }

        // LEER y GUARDAR el archivo (null si no esta). Se guarda aparte y se renombra; false si no se pudo.
        internal static string Lee()
        {
            try { string r = RocolaOs.Datos(RocolaOsCielo.ArchivoFechas); return File.Exists(r) ? File.ReadAllText(r, Encoding.UTF8) : null; }
            catch (Exception ex) { RocolaOs.Falla("ventana", "leer " + RocolaOsCielo.ArchivoFechas, ex); return null; }
        }
        internal static bool Guarda(string texto)
        {
            string ruta = RocolaOs.Datos(RocolaOsCielo.ArchivoFechas), tmp = ruta + ".nuevo";
            try
            {
                File.WriteAllText(tmp, texto ?? "", new UTF8Encoding(false));
                File.Move(tmp, ruta, true);
                return true;
            }
            catch (Exception ex)
            {
                RocolaOs.Falla("ventana", "guardar " + RocolaOsCielo.ArchivoFechas, ex);
                try { File.Delete(tmp); } catch { }
                return false;
            }
        }
    }
}

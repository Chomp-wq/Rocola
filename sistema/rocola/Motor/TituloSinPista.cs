// EL TITULO SIN EL NUMERO DE PISTA DELANTE ("03 - Titulo" -> "Titulo"): de AcabadoGpu.cs de XP, tal cual (la tarjeta del buscador).
using System;

namespace Rocola
{
    public partial class Form1
    {
        // Solo presentacion: nunca cambia rutas, etiquetas, orden ni claves de busqueda.
        internal static string TituloSinPista(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            int i = 0;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            if (i < title.Length && title[i] == '-') i++;
            int start = i;
            while (i < title.Length && title[i] >= '0' && title[i] <= '9') i++;
            int digits = i - start;
            if (digits < 1 || digits > 3) return title;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            if (i >= title.Length) return title;
            char separator = title[i++];
            if (separator != '-' && separator != '.' && separator != '–' && separator != '—') return title;
            // Un numero decimal no es un numero de pista.
            if (separator == '.' && i < title.Length && char.IsDigit(title[i])) return title;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            return i < title.Length ? title.Substring(i) : title;
        }
    }
}

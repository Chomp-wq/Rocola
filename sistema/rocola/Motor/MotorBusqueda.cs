// Motor textual, sin dibujo, disco, hilos ni cambios en las etiquetas originales.
// Las claves se preparan al construir el indice; por candidato no se crean strings.
using System;
using System.Globalization;
using System.Text;

namespace Rocola
{
    internal static class MotorBusqueda
    {
        internal static bool PrefijoPista(string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            int i = 0;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            if (i < title.Length && title[i] == '-') i++;
            int start = i;
            while (i < title.Length && title[i] >= '0' && title[i] <= '9') i++;
            if (i == start || i - start > 3) return false;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            if (i >= title.Length) return false;
            char c = title[i++];
            if (c != '-' && c != '.' && c != '\u2013' && c != '\u2014') return false;
            if (c == '.' && i < title.Length && char.IsDigit(title[i])) return false;
            while (i < title.Length && char.IsWhiteSpace(title[i])) i++;
            return i < title.Length;
        }
        internal static string Normaliza(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            bool simple = true, ascii = true;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c > 127) ascii = false;
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                    (c == ' ' && i > 0 && i + 1 < text.Length && text[i - 1] != ' ')))
                { simple = false; }
            }
            if (simple) return text;
            string decomposed = text;
            if (!ascii)
            {
                decomposed = text.ToLowerInvariant();
                try { decomposed = decomposed.Normalize(NormalizationForm.FormD); }
                catch (ArgumentException) { } // Una etiqueta con un surrogate roto no debe tumbar la busqueda.
                decomposed = Acentos.Quita(decomposed);   // (sin las tablas de idiomas, Normalize no separa los acentos: Acentos.cs)
            }
            StringBuilder result = new StringBuilder(decomposed.Length);
            bool space = false;
            for (int i = 0; i < decomposed.Length; i++)
            {
                char c = decomposed[i];
                if (c >= 'A' && c <= 'Z') c = (char)(c + ('a' - 'A'));
                if (c > 127 && CharUnicodeInfo.GetUnicodeCategory(decomposed, i) == UnicodeCategory.NonSpacingMark) continue;
                // Don't / Don’t / dont y O'Connor / oconnor son la misma clave.
                if (c == '\'' || c == '\u2019' || c == '\u2018' || c == '\u02bc') continue;
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || (c > 127 && char.IsLetterOrDigit(decomposed, i)))
                {
                    if (space && result.Length > 0) result.Append(' ');
                    result.Append(c);
                    if (char.IsHighSurrogate(c) && i + 1 < decomposed.Length && char.IsLowSurrogate(decomposed[i + 1]))
                        result.Append(decomposed[++i]);
                    space = false;
                }
                else space = true;
            }
            return result.ToString();
        }

        // La puntuacion del titulo no debe depender de lo largo que sea el artista.
        // Offset/length apuntan dentro de la misma clave: no duplicamos todos los titulos.
        internal static int Relevancia(string key, int start, int length, string query, int original)
        {
            if (original <= 0 || length <= 0 || start < 0 || start + length > key.Length) return original;
            if (length == query.Length && string.CompareOrdinal(key, start, query, 0, length) == 0) return 12000;
            // Una/dos letras siguen favoreciendo el artista, como antes: no llenar
            // las 40 casillas de titulos que empiezan por una letra muy comun.
            if (query.Length >= 3 && length >= query.Length && string.CompareOrdinal(key, start, query, 0, query.Length) == 0)
                return Math.Max(original, 7000 - Math.Min(length, 500));
            return original;
        }

        // Una sola errata: insercion, omision, sustitucion o dos letras vecinas invertidas.
        // Comparacion lineal sobre un tramo; nada de matrices o Substring por cancion.
        internal static bool UnaErrata(string text, int start, int length, string query)
        {
            int n = query.Length;
            if (Math.Abs(length - n) > 1) return false;
            int i = 0, j = 0;
            while (i < length && j < n && text[start + i] == query[j]) { i++; j++; }
            if (i == length || j == n) return true;
            if (length == n)
            {
                if (i + 1 < length && text[start + i] == query[j + 1] && text[start + i + 1] == query[j])
                { i += 2; j += 2; }
                else { i++; j++; }
            }
            else if (length > n) i++;
            else j++;
            while (i < length && j < n && text[start + i] == query[j]) { i++; j++; }
            return i == length && j == n;
        }

        internal static bool PermiteErrata(string query, string[] tokens)
        {
            if (query.Length > 128 || (tokens != null && tokens.Length > 8)) return false;
            if (tokens == null) return query.Length >= 4 && query.Length <= 64;
            for (int i = 0; i < tokens.Length; i++)
                if (tokens[i].Length >= 4 && tokens[i].Length <= 64) return true;
            return false;
        }

        internal static int RelevanciaAproximada(string key, int start, int length, string query, string[] tokens, int original)
        {
            if (original > 0 && tokens == null && length > 0 && start >= 0 && start + length <= key.Length &&
                UnaErrata(key, start, length, query)) return 750;
            return original;
        }

        internal static int LetrasImprescindibles(string query)
        {
            // Una errata solo puede eliminar una aparicion. Si una clase de letra
            // aparece dos veces, no puede faltar del candidato. Mismos bits que MarcaLetras.
            int seen = 0, required = 0;
            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];
                if (c == ' ') continue;
                int bit = c >= 'a' && c <= 'z' ? 1 << (c - 'a') : c >= '0' && c <= '9' ? 1 << 26 : 1 << 27;
                required |= seen & bit; seen |= bit;
            }
            return required;
        }

        internal static int Aproximado(string key, string query, string[] tokens)
        {
            int count = tokens == null ? 1 : tokens.Length;
            bool corrected = false;
            int position = 0;
            for (int t = 0; t < count; t++)
            {
                string token = tokens == null ? query : tokens[t];
                if (key.IndexOf(token, StringComparison.Ordinal) >= 0) continue;
                if (corrected || token.Length < 4 || token.Length > 64) return 0;
                bool found = false;
                for (int start = 0; start < key.Length; )
                {
                    int end = key.IndexOf(' ', start);
                    if (end < 0) end = key.Length;
                    if (UnaErrata(key, start, end - start, token))
                    { found = true; position = start; break; }
                    start = end + 1;
                }
                if (!found) return 0;
                corrected = true;
            }
            return corrected ? 500 - Math.Min(position, 200) : 0;
        }
    }
}

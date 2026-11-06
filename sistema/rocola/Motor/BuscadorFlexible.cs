// EL BUSCADOR FLEXIBLE (peticion del dueno del 7-11-2026: «necesito que mejoremos el algoritmo de busqueda al maximo, para que cualquiera
// que intente encontrar una cancion pueda hacerlo»). La busqueda de XP (Buscador.cs) sigue igual y manda: lo que casa letra por letra
// sale primero, en su orden. Cuando eso da menos de 40, esta segunda pasada rellena con lo que casa «de oido», con faltas o con palabras
// de sobra, siempre DEBAJO de lo exacto:
//  1. COMO SUENA: cada clave del indice tiene tambien su version fonetica en espanol (b/v, s/c/z, ll/y, h muda, qu/k, ge/je, ch/sh,
//     ph/f, letras dobles): «bicente fernandes» -> Vicente Fernandez, «yorar» -> Llorar, «kiero» -> Quiero, «shakira» = «chakira».
//  2. FALTAS: por palabra, 1 letra mal (cambiada, de mas, de menos o dos al reves) en palabras de 4 a 6 letras y 2 en las de 7 o mas;
//     tambien contra el principio de una palabra (mientras se escribe).
//  3. PALABRAS DE SOBRA: «el», «la», «de», «the»... no se exigen; con 3 o mas palabras basta que casen dos de cada tres.
//  4. PEGADAS O SEPARADAS: «lacucaracha» encuentra «La Cucaracha» y al reves.
//  5. EL DISCO Y EL GENERO de cada cancion cuentan (menos que el titulo y el artista): «cumbia» encuentra las cumbias etiquetadas.
// Y en todo el buscador, a igualdad, lo mas pedido primero (Popular).
using System;
using System.Collections.Generic;
using System.Text;

namespace Rocola
{
    internal static class Fonetica
    {
        // Palabras que no se exigen (si la busqueda tiene otras).
        private static readonly HashSet<string> Vacias = new HashSet<string>(StringComparer.Ordinal)
        {
            "el", "la", "lo", "los", "las", "de", "del", "y", "e", "a", "al", "en", "un", "una", "unos", "unas", "con", "por", "para",
            "mi", "mis", "tu", "tus", "su", "sus", "que", "se", "te", "me", "o", "u",
            "the", "an", "of", "and", "to", "in", "on", "my", "your", "feat", "ft", "featuring", "vs", "remix", "version", "en vivo", "live"
        };
        internal static bool EsVacia(string palabraFonetica) { return Vacias.Contains(palabraFonetica); }

        static Fonetica()
        {   // las vacias, tambien en su forma fonetica (como se comparan)
            List<string> l = new List<string>(Vacias);
            foreach (string w in l) Vacias.Add(De(w));
        }

        private static bool Vocal(char c) { return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u'; }

        // La clave como suena. Entra ya normalizada (MotorBusqueda.Normaliza: minusculas sin acentos, palabras separadas por un espacio).
        internal static string De(string k)
        {
            if (string.IsNullOrEmpty(k)) return "";
            StringBuilder b = new StringBuilder(k.Length);
            int n = k.Length;
            for (int i = 0; i < n; i++)
            {
                char c = k[i], s = i + 1 < n ? k[i + 1] : '\0', s2 = i + 2 < n ? k[i + 2] : '\0';
                bool inicio = i == 0 || k[i - 1] == ' ';
                char o;
                switch (c)
                {
                    case 'h':
                        continue;   // muda (ch, sh, ph y th se tratan con la letra de antes)
                    case 'v': o = 'b'; break;
                    case 'w': o = 'u'; break;
                    case 'z': o = 's'; break;
                    case 'c':
                        if (s == 'h') { o = 'x'; i++; break; }                 // ch -> x (y sh -> x: «shakira» = «chakira»)
                        if (s == 'e' || s == 'i') { o = 's'; break; }          // ce, ci -> se, si
                        if (s == 'k') continue;                                 // ck -> k
                        o = 'k'; break;
                    case 'q':
                        o = 'k';
                        if (s == 'u' && (s2 == 'e' || s2 == 'i')) i++;         // que, qui -> ke, ki
                        break;
                    case 'k': o = 'k'; break;
                    case 'g':
                        if (s == 'e' || s == 'i') { o = 'j'; break; }          // ge, gi -> je, ji
                        if (s == 'u' && (s2 == 'e' || s2 == 'i')) { o = 'g'; i++; break; }   // gue, gui -> ge, gi (la u no suena)
                        o = 'g'; break;
                    case 's':
                        if (s == 'h') { o = 'x'; i++; break; }
                        if (inicio && s == 'c' && (s2 == 'e' || s2 == 'i')) { o = 's'; i++; break; }   // «scena» = «sena»
                        o = 's'; break;
                    case 'p':
                        if (s == 'h') { o = 'f'; i++; break; }
                        if (inicio && s == 's') continue;                       // «psicodelico» = «sicodelico»
                        o = 'p'; break;
                    case 't':
                        if (s == 'h') { o = 't'; i++; break; }
                        o = 't'; break;
                    case 'l':
                        if (s == 'l') { o = 'y'; i++; break; }                  // ll -> y («llorar» = «yorar»)
                        o = 'l'; break;
                    case 'y':
                        o = Vocal(s) ? 'y' : 'i';   // como vocal (al final o antes de consonante) -> i: «rey» = «rei»; «yo», «ya»: y
                        break;
                    case 'n':
                        o = (s == 'b' || s == 'v' || s == 'p') ? 'm' : 'n';    // «inbierno» = «invierno» = «imbierno»
                        break;
                    case 'x':
                        o = inicio ? 's' : 'x'; break;                          // «xochitl» ~ «sochitl»
                    default: o = c; break;
                }
                // letras dobles: una (rr, ss, nn, tt...; «jhonny» = «joni»)
                if (b.Length > 0 && b[b.Length - 1] == o && o != ' ') continue;
                b.Append(o);
            }
            return b.ToString();
        }
        internal static string SinEspacios(string s) { return s.IndexOf(' ') < 0 ? s : s.Replace(" ", ""); }
        // Que letras tiene (una por bit; como MarcaLetras): para descartar sin comparar las entradas a las que les faltan demasiadas.
        internal static int Letras(string s)
        {
            int m = 0;
            if (s == null) return 0;
            for (int i = 0; i < s.Length; i++) { char c = s[i]; if (c >= 'a' && c <= 'z') m |= 1 << (c - 'a'); else if (c >= '0' && c <= '9') m |= 1 << 26; else if (c != ' ') m |= 1 << 27; }
            return m;
        }
        internal static int Bits(int x) { return System.Numerics.BitOperations.PopCount((uint)x); }

        // Distancia entre dos palabras (cambiada, de mas, de menos o dos vecinas al reves), con tope: si pasa de 'max', max + 1.
        internal static int Distancia(string a, int aLen, string b, int bLen, int max)
        {
            if (Math.Abs(aLen - bLen) > max) return max + 1;
            if (aLen > 64 || bLen > 64) return max + 1;
            Span<int> p2 = stackalloc int[bLen + 1], p1 = stackalloc int[bLen + 1], cur = stackalloc int[bLen + 1];
            for (int j = 0; j <= bLen; j++) p1[j] = j;
            for (int i = 1; i <= aLen; i++)
            {
                cur[0] = i; int fila = i;
                for (int j = 1; j <= bLen; j++)
                {
                    int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = Math.Min(Math.Min(p1[j] + 1, cur[j - 1] + 1), p1[j - 1] + costo);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) v = Math.Min(v, p2[j - 2] + 1);
                    cur[j] = v; if (v < fila) fila = v;
                }
                if (fila > max) return max + 1;
                Span<int> t = p2; p2 = p1; p1 = cur; cur = t;
            }
            return p1[bLen];
        }
        internal static int Tolerancia(int largo) { return largo >= 7 ? 2 : largo >= 4 ? 1 : 0; }

        // Lo bien que casa una palabra buscada con una palabra de la biblioteca (0: nada; 100: igual).
        internal static int ValorPalabra(string t, string w, int tol)
        {
            if (w.Length == 0) return 0;
            if (w == t) return 100;
            if (w.Length > t.Length && w.StartsWith(t, StringComparison.Ordinal)) return t.Length >= 2 ? 85 : 50;   // a medio escribir
            if (t.Length >= 3 && w.Length > t.Length && w.IndexOf(t, 1, StringComparison.Ordinal) > 0) return 60;
            if (tol == 0) return 0;
            if (Math.Abs(w.Length - t.Length) <= tol)
            {
                int d = Distancia(t, t.Length, w, w.Length, tol);
                if (d <= tol) return d == 1 ? 55 : 40;
            }
            if (w.Length > t.Length + tol)
            {   // con faltas, pero a medio escribir: contra el principio de la palabra
                int d = Distancia(t, t.Length, w, t.Length, tol);
                if (d <= tol) return d == 1 ? 45 : 32;
            }
            return 0;
        }
        // Lo bien que casa una palabra de la busqueda con alguna de las palabras de la clave (0: nada; 100: igual).
        internal static int MejorPalabra(string t, string[] palabras)
        {
            if (palabras == null) return 0;
            int mejor = 0, tol = Tolerancia(t.Length);
            for (int i = 0; i < palabras.Length; i++)
            {
                string w = palabras[i];
                if (w.Length == 0) continue;
                int v = 0;
                if (w == t) return 100;
                if (w.Length > t.Length && w.StartsWith(t, StringComparison.Ordinal)) v = t.Length >= 2 ? 85 : 50;   // a medio escribir
                else if (t.Length >= 3 && w.IndexOf(t, StringComparison.Ordinal) > 0) v = 60;
                else if (tol > 0)
                {
                    if (Math.Abs(w.Length - t.Length) <= tol)
                    {
                        int d = Distancia(t, t.Length, w, w.Length, tol);
                        if (d <= tol) v = d == 1 ? 55 : 40;
                    }
                    if (v == 0 && w.Length > t.Length + tol)
                    {   // con faltas, pero a medio escribir: contra el principio de la palabra
                        int d = Distancia(t, t.Length, w, t.Length, tol);
                        if (d <= tol) v = d == 1 ? 45 : 32;
                    }
                }
                if (v > mejor) mejor = v;
            }
            return mejor;
        }
    }

    public partial class Form1
    {
        private int selloBusqueda;

        // Las claves foneticas de una entrada del indice (al armarlo; TramoDeClaves).
        private static void ClavesFoneticas(SearchItem si)
        {
            si.Fon = Fonetica.De(si.Key);
            si.Pegado = Fonetica.SinEspacios(si.Fon);
            si.Palabras = si.Fon.Length == 0 ? new string[0] : si.Fon.Split(' ');
            if (si.Kind == 2)
            {   // la cancion tambien por su disco y su genero (si ya se leyo de la etiqueta)
                string mas = (si.AlbumRef != null ? si.AlbumRef.Name : "") + " " + (si.TrackRef != null && !string.IsNullOrEmpty(si.TrackRef.Genre) ? si.TrackRef.Genre : "");
                string fm = Fonetica.De(MotorBusqueda.Normaliza(mas));
                si.Mas = fm.Length == 0 ? null : fm.Split(' ');
            }
            si.FonMask = Fonetica.Letras(si.Fon); si.MasMask = si.Mas == null ? 0 : Fonetica.Letras(string.Join(" ", si.Mas));
        }

        // LO MAS PEDIDO, A IGUALDAD: hasta 150 puntos (los escalones de la busqueda van de miles en miles: solo desempata).
        private int Popular(SearchItem si)
        {
            int n = 0;
            try
            {
                if (si.Kind == 2 && si.TrackRef != null) songCounts.TryGetValue(si.TrackRef.Path, out n);
                else
                {
                    string ar = si.Kind == 0 ? si.Display : si.AlbumRef != null ? si.AlbumRef.Artist : null;
                    if (ar != null) { artistCounts.TryGetValue(ar, out n); if (si.Kind == 1) n /= 2; }
                }
            }
            catch { n = 0; }
            if (n <= 0) return 0;
            return Math.Min(150, (int)(30 * Math.Log(1 + n, 2)));
        }

        // ---- EL INDICE DE PALABRAS (se arma con el del buscador: BuildSearchIndex) ----
        // Cada palabra fonetica distinta de la biblioteca, con las entradas que la tienen (en su titulo o artista; aparte, en su disco o
        // genero). La busqueda compara cada palabra buscada con estas palabras DISTINTAS (muchas menos que entradas por palabras) y salta
        // directo a las entradas que las tienen. Lo de cada palabra buscada se recuerda: al escribir la siguiente, la de antes ya esta.
        private Dictionary<string, List<SearchItem>> palabrasPrincipales, palabrasMas;
        private string[] palabrasDistintas;
        private readonly Dictionary<string, KeyValuePair<string, int>[]> valoresDePalabra = new Dictionary<string, KeyValuePair<string, int>[]>(StringComparer.Ordinal);
        private void IndicePalabras()
        {
            Dictionary<string, List<SearchItem>> pp = new Dictionary<string, List<SearchItem>>(StringComparer.Ordinal), pm = new Dictionary<string, List<SearchItem>>(StringComparer.Ordinal);
            foreach (SearchItem si in searchIndex)
            {
                if (si.Palabras != null) foreach (string w in si.Palabras) Apunta(pp, w, si);
                if (si.Mas != null) foreach (string w in si.Mas) Apunta(pm, w, si);
            }
            HashSet<string> todas = new HashSet<string>(pp.Keys, StringComparer.Ordinal);
            foreach (string w in pm.Keys) todas.Add(w);
            string[] l = new string[todas.Count]; todas.CopyTo(l);
            lock (valoresDePalabra) { valoresDePalabra.Clear(); palabrasPrincipales = pp; palabrasMas = pm; palabrasDistintas = l; }
            if (letrasMp3.Count > 0) IndiceLetras(true);   // (y las letras de los MP3, con las entradas nuevas)
        }
        private static void Apunta(Dictionary<string, List<SearchItem>> d, string w, SearchItem si)
        {
            if (w.Length == 0) return;
            if (!d.TryGetValue(w, out List<SearchItem> l)) { l = new List<SearchItem>(2); d[w] = l; }
            if (l.Count == 0 || !object.ReferenceEquals(l[l.Count - 1], si)) l.Add(si);   // (la misma palabra dos veces en una entrada: una)
        }
        // Las palabras de la biblioteca que casan con una palabra buscada, y cuanto (recordado por palabra).
        private KeyValuePair<string, int>[] ValoresDe(string tok)
        {
            lock (valoresDePalabra) if (valoresDePalabra.TryGetValue(tok, out KeyValuePair<string, int>[] ya)) return ya;
            string[] ws = palabrasDistintas;
            List<KeyValuePair<string, int>> r = new List<KeyValuePair<string, int>>();
            int tol = Fonetica.Tolerancia(tok.Length);
            for (int i = 0; i < ws.Length; i++)
            {
                int v = Fonetica.ValorPalabra(tok, ws[i], tol);
                if (v > 0) r.Add(new KeyValuePair<string, int>(ws[i], v));
            }
            KeyValuePair<string, int>[] a = r.ToArray();
            lock (valoresDePalabra) { if (valoresDePalabra.Count > 256) valoresDePalabra.Clear(); valoresDePalabra[tok] = a; }
            return a;
        }

        // La segunda pasada: anade a 'matches' lo que casa de forma flexible y no estaba ya (sello). Puntos de 100 a 700.
        private void BuscaFlexible(string q, List<SearchItem> matches)
        {
            if (palabrasDistintas == null) return;
            string fq = Fonetica.De(q);
            string pegada = Fonetica.SinEspacios(fq);
            if (pegada.Length < 3) return;
            string[] todas = fq.Split(' ');
            List<string> utiles = new List<string>(todas.Length);
            foreach (string t in todas) if (t.Length > 0 && !Fonetica.EsVacia(t) && !utiles.Contains(t)) utiles.Add(t);   // (sin repetidas: «bidi bidi»)
            if (utiles.Count == 0) foreach (string t in todas) if (t.Length > 0 && !utiles.Contains(t)) utiles.Add(t);
            if (utiles.Count == 0 || utiles.Count > 8) return;
            int sig = utiles.Count, hacen = sig <= 2 ? sig : (sig * 2 + 2) / 3;
            int sello = selloBusqueda, exactos = matches.Count;
            // por cada entrada que tenga alguna palabra que case: lo mejor que casa cada palabra buscada
            Dictionary<SearchItem, int[]> acc = new Dictionary<SearchItem, int[]>();
            for (int t = 0; t < sig; t++)
            {
                foreach (KeyValuePair<string, int> wv in ValoresDe(utiles[t]))
                {
                    if (palabrasPrincipales.TryGetValue(wv.Key, out List<SearchItem> l)) Suma(acc, l, t, sig, wv.Value, sello);
                    if (palabrasMas.TryGetValue(wv.Key, out List<SearchItem> lm)) Suma(acc, lm, t, sig, wv.Value * 6 / 10, sello);
                }
            }
            foreach (KeyValuePair<SearchItem, int[]> kv in acc)
            {
                int suma = 0, casan = 0, bien = 0, minimo = 100;
                for (int t = 0; t < sig; t++)
                {
                    int v = kv.Value[t];
                    if (v > 0) { casan++; suma += v; if (v >= 85) bien++; if (v < minimo) minimo = v; }
                }
                if (casan < hacen) continue;
                // PRECISION: con una palabra, vale cualquier parecido (40 o mas); con varias, que una case bien (igual o a medio escribir) o que
                // casen TODAS con poca falta (55 o mas). Si no, una sola falta en una palabra comun traia cientos de entradas que nadie busco.
                if (sig >= 2 && bien == 0 && !(casan == sig && minimo >= 55)) continue;
                // y si lo exacto ya encontro varias (5 o mas), de lo flexible solo lo muy parecido (sin faltas: suena igual o a medio escribir)
                if (exactos >= 5 && minimo < 85) continue;
                int sc = 100 + (suma * 5) / sig - (sig - casan) * 60;   // mejor cuanto mas y mejor casan; menos por cada una que falta
                SearchItem si = kv.Key;
                si.Score = Math.Max(1, Math.Min(690, sc)) + Popular(si); si.Marca = sello; matches.Add(si);
            }
            // PEGADAS O SEPARADAS: «lacucaracha» <-> «la cucaracha» (la busqueda sin espacios dentro de la clave sin espacios)
            if (pegada.Length >= 5)
            {
                bool varias = todas.Length > 1;
                List<SearchItem> idx = searchIndex;
                for (int i = 0; i < idx.Count; i++)
                {
                    SearchItem si = idx[i];
                    if (si.Marca == sello || si.Pegado == null || si.Kind > 2 || (!varias && si.Palabras.Length < 2)) continue;
                    int at = si.Pegado.IndexOf(pegada, StringComparison.Ordinal);
                    if (at < 0) continue;
                    si.Score = 700 - Math.Min(at, 100) + Popular(si); si.Marca = sello; matches.Add(si);
                }
            }
        }
        private static void Suma(Dictionary<SearchItem, int[]> acc, List<SearchItem> l, int t, int sig, int v, int sello)
        {
            for (int i = 0; i < l.Count; i++)
            {
                SearchItem si = l[i];
                if (si.Marca == sello || si.Kind > 2) continue;   // (ya esta por lo exacto)
                if (!acc.TryGetValue(si, out int[] a)) { a = new int[sig]; acc[si] = a; }
                if (v > a[t]) a[t] = v;
            }
        }
    }
}

// EL PARECIDO DE NOMBRES DE DISCO Y DE ARTISTA (Rocola OS, la pared con el catalogo; peticion del dueno del 27-11-2026: «si tengo un disco en mi
// musica, por ejemplo "Bad" de Michael Jackson, su version de Spotify no debe salir en la pared»). Antes la pared comparaba el nombre del disco
// SOLO igual (sin acentos ni mayusculas): «Bad (Remastered 2015)», «Bad (Deluxe Edition)» o «Bad - 25th Anniversary Edition» de Spotify se
// repetian junto a tu «Bad». Aqui, la CLAVE de un disco y la de un artista, para decir «es el mismo»:
//   - disco: sin acentos ni mayusculas ni signos (MotorBusqueda.Normaliza, la de XP) y SIN LA EDICION: lo que va entre parentesis o corchetes y
//     habla de la edicion («Remastered 2015», «Deluxe», «Special Edition», «25th Anniversary», «Expanded», «Bonus Tracks», «Mono», «Reissue»...),
//     lo que va tras « - » o «:» si habla de la edicion, y las palabras de edicion sueltas al final («Bad Deluxe Edition», «Abbey Road Remastered
//     2019»). Lo que no habla de la edicion se queda («Greatest Hits (Vol. 2)» no es «Greatest Hits»; «1989» o «Songs of 2020» no pierden su
//     numero: un ano solo se quita junto a una palabra de edicion); nunca se queda sin nada.
//   - artista: el PRINCIPAL: sin acentos ni mayusculas, sin «the» delante y cortado en el primer «feat.», «ft.», «featuring», «con», «with»,
//     «vs» o punto y coma («Michael Jackson feat. Siedah Garrett» es «Michael Jackson»; «Simon & Garfunkel», «Los Tigres del Norte», «Earth, Wind & Fire»,
//     «Crosby, Stills & Nash», «Tyler, The Creator» y «AC/DC» siguen enteros: un duo o un grupo no es su primer nombre; 27-11-2026, revision: antes
//     cortaba tambien en coma y barra). El corte es solo para COMPARAR: el nombre que se muestra y el que ordena es el de verdad.
// Es una funcion pura de cadenas (se prueba sola: escena «nombres» de Pruebas/RocolaOs/pared-generos).
using System;
using System.Collections.Generic;
using System.Text;

namespace Rocola
{
    internal static class RocolaOsPgNombres
    {
        // palabras que dicen «esto es una edicion, no otro disco» (ya normalizadas: minusculas y sin acentos)
        private static readonly HashSet<string> Edicion = new HashSet<string>(StringComparer.Ordinal)
        {
            "remastered", "remaster", "remasterizado", "remasterizada", "remasterisado", "remastering", "remasterizacion",
            "deluxe", "delux", "edition", "edicion", "editions", "anniversary", "aniversario", "special", "especial", "expanded", "ampliada",
            "bonus", "tracks", "track", "version", "reissue", "reissued", "collectors", "collector", "collector's", "legacy", "platinum", "mono", "stereo",
            "super", "digital", "explicit", "clean", "extended", "limited", "anos", "years", "year", "original", "mix", "mixes", "disc", "cd",
            "japan", "japanese", "us", "uk", "international", "internacional", "standard", "estandar", "complete", "completa", "definitive",
            "ultimate", "gold", "silver", "diamond", "tour", "bonustracks", "restored", "restaurado", "remix",   // (revision 27-11-2026: sin «sessions»: «Live Sessions» no es «Live»)
        };
        // las que SOLAS bastan para decir «edicion» dentro de un parentesis o tras « - » (las demas, de la lista de arriba, solo ayudan: «Original»,
        // «Mix», «Tour», «US», «Complete»... no son una edicion por si solas: «Original Masters» o «The Tour»)
        private static readonly HashSet<string> Fuertes = new HashSet<string>(StringComparer.Ordinal)
        {
            "remastered", "remaster", "remasterizado", "remasterizada", "remasterisado", "remastering", "remasterizacion",
            "deluxe", "delux", "edition", "edicion", "editions", "anniversary", "aniversario", "expanded", "ampliada", "bonus", "bonustracks",
            "reissue", "reissued", "collectors", "collector", "collector's", "legacy", "platinum", "mono", "stereo", "explicit", "extended",
            "restored", "restaurado", "definitive", "ultimate",
        };

        // los tokens de un texto sin acentos ni mayusculas ni signos (la Normaliza de XP: lo que no es letra o cifra, un espacio)
        private static string[] Tokens(string s)
        {
            string n = MotorBusqueda.Normaliza(s ?? "");
            return n.Length == 0 ? new string[0] : n.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }
        private static bool EsAno(string t) { return t.Length == 4 && (t.StartsWith("19", StringComparison.Ordinal) || t.StartsWith("20", StringComparison.Ordinal)) && IsDigits(t); }
        private static bool IsDigits(string t) { foreach (char c in t) if (c < '0' || c > '9') return false; return t.Length > 0; }
        // «25th», «30o», «1st», «2nd»: un ordinal (de una edicion de aniversario)
        private static bool EsOrdinal(string t)
        {
            if (t.Length < 3 || t[0] < '0' || t[0] > '9') return false;
            int i = 0; while (i < t.Length && t[i] >= '0' && t[i] <= '9') i++;
            string r = t.Substring(i);
            return r == "th" || r == "st" || r == "nd" || r == "rd" || r == "o" || r == "a" || r == "ro" || r == "do";
        }
        private static bool EsAnos(string t) { return t == "years" || t == "year" || t == "anos"; }
        private static bool EsPalabraDeEdicion(string t) { return Edicion.Contains(t) || EsOrdinal(t) || EsAno(t) || IsDigits(t) && t.Length <= 2; }
        private static bool HablaDeEdicion(string[] t)
        {
            if (t.Length == 0) return false;
            bool fuerte = false;
            foreach (string x in t)
            {
                if (Fuertes.Contains(x)) fuerte = true;
                else if (!EsPalabraDeEdicion(x) && x != "the" && x != "of" && x != "de" && x != "y" && x != "and" && x != "with" && x != "con" && x != "from") return false;
            }
            return fuerte;
        }

        // LA CLAVE DE UN DISCO (sin la edicion); vacia solo si el nombre no tiene letras ni cifras
        internal static string ClaveDisco(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "";
            // 1) lo que va entre parentesis o corchetes y habla de la edicion, fuera (y el resto, tal cual)
            StringBuilder sb = new StringBuilder(nombre.Length);
            int i = 0;
            while (i < nombre.Length)
            {
                char c = nombre[i];
                char cierra = c == '(' ? ')' : c == '[' ? ']' : c == '{' ? '}' : '\0';
                if (cierra != '\0')
                {
                    int j = nombre.IndexOf(cierra, i + 1);
                    if (j > 0)
                    {
                        string dentro = nombre.Substring(i + 1, j - i - 1);
                        if (HablaDeEdicion(Tokens(dentro))) { sb.Append(' '); i = j + 1; continue; }
                    }
                }
                sb.Append(c);
                i++;
            }
            string t = sb.ToString();
            // 2) lo que va tras « - » o «: » y habla de la edicion («Thriller - 25th Anniversary Edition»), fuera
            int corte = -1;
            foreach (string sep in new[] { " - ", " – ", " — ", ": " })
            {
                int p = t.IndexOf(sep, StringComparison.Ordinal);
                if (p > 0 && (corte < 0 || p < corte) && HablaDeEdicion(Tokens(t.Substring(p + sep.Length)))) corte = p;
            }
            if (corte > 0) t = t.Substring(0, corte);
            // 3) las palabras de edicion sueltas al final: la corrida del final (con anos y ordinales) solo se quita si trae una palabra de edicion
            //    y deja algo
            List<string> tk = new List<string>(Tokens(t));
            int fin = tk.Count;
            // («years», «year» y «anos» solo son de edicion tras un numero: «25 Years Deluxe»; «Ten Years» no es «Ten»)
            while (fin > 1 && ((EsPalabraDeEdicion(tk[fin - 1]) && !(EsAnos(tk[fin - 1]) && !(fin > 2 && (IsDigits(tk[fin - 2]) || EsOrdinal(tk[fin - 2]))))) || (fin > 2 && (tk[fin - 1] == "the" || tk[fin - 1] == "of" || tk[fin - 1] == "de")))) fin--;
            if (fin < tk.Count)
            {
                bool hay = false;
                // (27-11-2026, revision) solo una palabra FUERTE (deluxe, remaster, edicion de aniversario...) quita la cola; las demas solo extienden la
                // corrida («Live Sessions» no es «Live», «Ten Years» no es «Ten», «Greatest Hits Gold» no es «Greatest Hits»)
                for (int k = fin; k < tk.Count; k++) if (Fuertes.Contains(tk[k])) { hay = true; break; }
                if (hay) tk.RemoveRange(fin, tk.Count - fin);
            }
            // 4) sin «the» al principio
            if (tk.Count > 1 && tk[0] == "the") tk.RemoveAt(0);
            return string.Join(" ", tk.ToArray());
        }

        // (pruebas) ROCOLA_NOMBRES_PRUEBA=ARCHIVO: lee casos «tipo<TAB>a<TAB>b<TAB>esperado» (disco: 1 si a y b son el mismo disco; artista: lo mismo;
        // marcas: «marcas<TAB>disco<TAB>artista<TAB>genero<TAB>marcas esperadas», de RocolaOs.ParedExclusiones.cs) y escribe ARCHIVO.salida con BIEN o MAL por caso.
        // Pruebas/RocolaOs/pared-generos, escenas «nombres» y «exclusiones».
        internal static void Prueba(string ruta)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                int bien = 0, mal = 0;
                foreach (string l in System.IO.File.ReadAllLines(ruta, Encoding.UTF8))
                {
                    if (l.Trim().Length == 0 || l.StartsWith("#", StringComparison.Ordinal)) continue;
                    string[] c = l.Split('\t');
                    string t = c[0], a = c.Length > 1 ? c[1] : "", b = c.Length > 2 ? c[2] : "", esp = c.Length > 3 ? c[3] : "", sale, quiero = esp;
                    if (t == "disco") sale = ClaveDisco(a) == ClaveDisco(b) && ClaveDisco(a).Length > 0 ? "1" : "0";
                    else if (t == "artista") sale = ClaveArtista(a) == ClaveArtista(b) && ClaveArtista(a).Length > 0 ? "1" : "0";
                    else if (t == "marcas") { sale = RocolaOsPgExclusiones.Marcas(a, b, c.Length > 3 ? c[3] : ""); quiero = c.Length > 4 ? c[4] : ""; }
                    else continue;
                    bool ok = sale == quiero;
                    if (ok) bien++; else mal++;
                    sb.Append(ok ? "BIEN" : "MAL").Append('\t').Append(l).Append('\t').Append("sale ").Append(sale).Append('\n');
                }
                sb.Append("TOTAL\t").Append(bien.ToString()).Append(" bien\t").Append(mal.ToString()).Append(" mal\n");
                System.IO.File.WriteAllText(ruta + ".salida", sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { try { System.IO.File.WriteAllText(ruta + ".salida", "ERROR " + ex.GetType().Name + " " + ex.Message + "\n"); } catch { } }
        }

        // LA CLAVE DEL ARTISTA PRINCIPAL
        internal static string ClaveArtista(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "";
            string n = nombre;
            int corte = n.Length;
            foreach (string sep in new[] { " feat.", " feat ", " ft.", " ft ", " featuring ", " con ", " with ", " vs. ", " vs ", ";" })
            {
                int p = n.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (p > 0 && p < corte) corte = p;
            }
            n = n.Substring(0, corte);
            string k = MotorBusqueda.Normaliza(n);
            if (k.StartsWith("the ", StringComparison.Ordinal) && k.Length > 4) k = k.Substring(4);
            return k;
        }
    }
}

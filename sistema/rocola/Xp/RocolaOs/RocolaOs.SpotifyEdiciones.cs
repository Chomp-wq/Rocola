// LAS EDICIONES REPETIDAS DE UN DISCO Y «CANCIONES DE ARTISTA» FILTRADO (Rocola OS, modo Spotify; mejoras que el dueno eligio el 27-11-2026:
// «sin ediciones repetidas en las paredes» y «"Canciones de" filtrado»; FASE11_paredes-buscador.md, ultima seccion).
//
// EDICIONES. Spotify da el mismo disco muchas veces: «Abbey Road», «Abbey Road (Remastered 2009)», «Abbey Road (Super Deluxe Edition)»,
// «Rumours - Expanded Edition»... En las paredes del buscador (artista solo de Spotify, la pared unida, «Todos los discos de») salian todas
// juntas. La CLAVE DE EDICION de un nombre es el nombre sin lo que dice que es una edicion (Remastered, Deluxe, Edition, Aniversario, Expanded,
// Special, Collector's, Bonus, Reissue, de lujo...), en parentesis, corchetes, tras « - » o al final, y normalizado (sin acentos ni signos).
// Solo se quita lo que SOLO dice edicion: «(Taylor's Version)», «(Live)», «(Unplugged)», «(From the Motion Picture)» son otro disco y se quedan.
// De los discos que comparten clave en una pared queda UNO: el ORIGINAL (el de nombre sin marcas de edicion) si esta; si no, el que mas
// canciones trae (Spotify da total_tracks); a igualdad, el primero que llego (los discos de un artista vienen del mas nuevo al mas viejo).
// Decision: el original y no el mas completo, porque con la «Deluxe» a la vista un cliente pide canciones de relleno (versiones, demos) en vez
// del disco que busca; las extras estan en «Canciones de ARTISTA». En la pared unida tambien se quitan los discos de Spotify que son una edicion
// de uno TUYO (tu «Abbey Road» y el «Abbey Road (Remastered)» de Spotify son el mismo disco).
//
// CANCIONES DE ARTISTA. La busqueda de Spotify type=track con artist:"nombre" trae versiones de otros y colaboraciones donde el artista ni
// aparece. Ahora «Canciones de ARTISTA» solo lleva las canciones donde el artista esta entre los artistas de la pista (por su id si se sabe;
// si no, por su nombre sin acentos), sea el principal o uno invitado.
// Todo esto es solo con el modo Spotify (las paredes del buscador no existen sin el).
using System;
using System.Collections.Generic;
using System.Text;

namespace Rocola
{
    internal static class RocolaOsSpotifyEdiciones
    {
        // las palabras que, solas, dicen «edicion» (en lo normalizado): con al menos una de las PRINCIPALES, el grupo entero es una marca de edicion
        private static readonly HashSet<string> Principales = new HashSet<string>(StringComparer.Ordinal) {
            "remaster", "remastered", "remasterizado", "remasterizada", "remasterizados", "remasterizadas", "remasterizacion", "deluxe", "edition", "editions",
            "edicion", "ediciones", "anniversary", "aniversario", "expanded", "special", "collector", "collectors", "bonus", "reissue", "reedicion", "lujo",
            "legacy", "platinum", "standard", "ultimate" };
        // las que acompanan (no dicen edicion solas)
        private static readonly HashSet<string> Acompanan = new HashSet<string>(StringComparer.Ordinal) {
            "version", "versions", "tracks", "track", "the", "de", "del", "la", "el", "of", "and", "y", "with", "con", "super", "limited", "digital", "explicit",
            "clean", "mono", "stereo", "estereo", "complete", "completa", "original", "new", "nueva", "extended", "extendida", "anos", "years", "year", "ano",
            "re", "master", "cd", "disc" };
        private static readonly HashSet<string> Vacias = new HashSet<string>(StringComparer.Ordinal) { "the", "de", "del", "la", "el", "of", "and", "y", "with", "con", "new", "nueva" };

        private static bool EsNumero(string w) { return w.Length > 0 && w[0] >= '0' && w[0] <= '9'; }
        private static bool EsPalabraDeEdicion(string w, out bool principal)
        {
            principal = Principales.Contains(w);
            return principal || Acompanan.Contains(w) || EsNumero(w);   // (25th, 2009, 50: numeros y ordinales)
        }
        // ¿el texto (un grupo entre parentesis o una cola « - ...») solo dice edicion?
        private static bool SoloDiceEdicion(string texto)
        {
            string n = MotorBusqueda.Normaliza(texto);
            if (n.Length == 0) return false;
            bool hay = false;
            foreach (string w in n.Split(' '))
            {
                if (w.Length == 0) continue;
                bool p;
                if (!EsPalabraDeEdicion(w, out p)) return false;
                if (p) hay = true;
            }
            return hay;
        }
        // LA CLAVE DE EDICION de un nombre de disco: sin acentos ni signos y sin lo que solo dice edicion
        internal static string Clave(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "";
            // 1) los grupos entre parentesis o corchetes que solo dicen edicion
            StringBuilder b = new StringBuilder(nombre.Length), grupo = new StringBuilder();
            int prof = 0;
            foreach (char ch in nombre)
            {
                if (ch == '(' || ch == '[')
                {
                    if (prof > 0) grupo.Append(ch);
                    else grupo.Clear();
                    prof++; continue;
                }
                if ((ch == ')' || ch == ']') && prof > 0)
                {
                    prof--;
                    if (prof > 0) { grupo.Append(ch); continue; }
                    string g = grupo.ToString();
                    if (!SoloDiceEdicion(g)) b.Append(' ').Append(g).Append(' ');   // (otra cosa: se queda en la clave)
                    continue;
                }
                if (prof > 0) grupo.Append(ch); else b.Append(ch);
            }
            if (prof > 0) b.Append(' ').Append(grupo);   // (parentesis sin cerrar: tal cual)
            string s = b.ToString();
            // 2) la cola « - ...» que solo dice edicion
            int g2 = s.LastIndexOf(" - ", StringComparison.Ordinal);
            if (g2 > 0 && SoloDiceEdicion(s.Substring(g2 + 3))) s = s.Substring(0, g2);
            string n = MotorBusqueda.Normaliza(s);
            // 3) palabras de edicion al final sin marca («Rumours Deluxe Edition», «Thriller 25th Anniversary Edition»): la cola de palabras de
            // edicion pegada al final, si trae una principal; sin los articulos ni los numeritos sueltos con que empieza («Fearless 2 Deluxe»)
            string[] w = n.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int ini = w.Length;
            bool principal = false;
            while (ini > 1)
            {
                bool p;
                if (!EsPalabraDeEdicion(w[ini - 1], out p)) break;
                if (p) principal = true;
                ini--;
            }
            if (!principal) return n;
            while (ini < w.Length)
            {
                bool p;
                EsPalabraDeEdicion(w[ini], out p);
                if (p) break;
                if (Vacias.Contains(w[ini]) || (EsNumero(w[ini]) && w[ini].Length < 4 && char.IsDigit(w[ini][w[ini].Length - 1]))) ini++; else break;
            }
            return ini >= 1 ? string.Join(" ", w, 0, ini) : n;
        }
        // ¿el nombre es el ORIGINAL (no dice que sea una edicion)?
        internal static bool EsOriginal(string nombre)
        {
            return MotorBusqueda.Normaliza(nombre ?? "") == Clave(nombre);
        }
        // ¿«nuevo» es mejor que «actual» (los dos de la misma clave)? El original, si no el de mas canciones; a igualdad, el actual
        internal static bool Mejor(RocolaOsSpotifyCatalogoDisco nuevo, RocolaOsSpotifyCatalogoDisco actual)
        {
            if (nuevo == null) return false;
            if (actual == null) return true;
            bool on = EsOriginal(nuevo.Nombre), oa = EsOriginal(actual.Nombre);
            if (on != oa) return on;
            if (on) return false;   // (dos originales con la misma clave: el primero)
            return nuevo.NumCanciones > actual.NumCanciones;
        }

        // ---------- «Canciones de ARTISTA» ----------
        // ¿la cancion es del artista (su id si se sabe, o su nombre)? Esta entre sus artistas: el principal o uno invitado
        internal static bool EsDeArtista(RocolaOsSpotifyCatalogoCancion c, string artistaId, string artistaNombre)
        {
            if (c == null) return false;
            if (!string.IsNullOrEmpty(artistaId) && c.ArtistaIds != null && c.ArtistaIds.Length > 0)
            {
                foreach (string id in c.ArtistaIds) if (string.Equals(id, artistaId, StringComparison.Ordinal)) return true;
                return false;
            }
            string n = MotorBusqueda.Normaliza(artistaNombre ?? "");
            if (n.Length == 0) return true;   // (sin saber de quien: no se filtra)
            foreach (string a in (c.Artista ?? "").Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)) if (MotorBusqueda.Normaliza(a) == n) return true;
            return (" " + MotorBusqueda.Normaliza(c.Artista ?? "") + " ").Contains(" " + n + " ");
        }
    }
}

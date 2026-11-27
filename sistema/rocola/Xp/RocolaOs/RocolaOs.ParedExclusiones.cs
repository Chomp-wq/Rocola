// LO QUE NO VA EN LA PARED DE DISCOS (Rocola OS, la pared con el catalogo; peticion del dueno del 27-11-2026, al devolver la pared de Spotify:
// «musica de peliculas [bandas sonoras], de videojuegos y cosas de ese tipo (tambien musicales, infantiles, karaoke, para dormir o meditar, y
// discos sin portada) NO van en la pared de discos; solo se encuentran con el buscador»). Los discos del catalogo se MARCAN con letras:
//   s  cine y series (bandas sonoras, «Original Motion Picture Soundtrack», OST, BSO, score)
//   j  videojuegos
//   m  musicales y teatro (reparto original, Broadway)
//   k  karaoke y pistas para cantar
//   i  infantil
//   d  para dormir, meditar o relajarse (ruido blanco, lluvia, yoga, nanas...)
//   h  hablado (comedia, audiolibros...)
// y un disco marcado, o de un artista de esos generos, o sin portada, NO entra en la pared (el buscador no cambia: ahi se encuentra todo).
//
// LAS MARCAS salen de DOS sitios que dicen lo mismo:
//   1. el catalogo nuevo (sistema/catalogo/arma_catalogo.py, columna 7 de cada «D»: sus tipos secundarios de MusicBrainz y estas mismas
//      palabras del nombre del disco o del artista y el genero del artista); un rocola-xp VIEJO ignora esa columna;
//   2. aqui, por LAS PALABRAS DEL NOMBRE y el genero del artista, tambien con el catalogo YA PUBLICADO (sin la columna).
// Las tablas de palabras son las mismas de arma_catalogo.py (MARCAS_PALABRAS): si cambia una, cambia la otra (la escena «exclusiones» y
// sistema/catalogo/prueba/comprueba.py comparan las dos con el mismo archivo, exclusiones.tsv).
// Las frases van ya normalizadas (minusculas, sin acentos, sin signos: la Normaliza de XP) y valen como palabras enteras: «ost» no marca «lost».
// (27-11-2026, revision) Las palabras sueltas ambiguas no valen en cualquier parte (ver PalabrasDisco) y un ARTISTA solo se marca por su nombre entero
// o casi (PalabrasArtista): «The Soundtrack of Our Lives», «Lullaby for the Working Class» o el «Binaural» de Pearl Jam ya no se quedan fuera.
using System;
using System.Collections.Generic;
using System.Text;

namespace Rocola
{
    internal static class RocolaOsPgExclusiones
    {
        internal const string Letras = "sjmkidh";

        // (marca, frases) de un NOMBRE DE DISCO. (27-11-2026, revision) Solo frases que dicen la cosa por si solas: las palabras sueltas ambiguas
        // («soundtrack», «ost», «lullaby», «yoga», «meditation», «binaural», «relaxation», «mindfulness», «asmr»...) NO valen en cualquier parte
        // (el disco «Binaural» de Pearl Jam, «Lullaby for the Working Class», «Songs from the Game» son discos normales): las de cine («soundtrack»,
        // «ost», «bso») valen si son TODO el nombre o van entre parentesis o corchetes o solas tras un guion o dos puntos (AmbiguasS, abajo)
        private static readonly KeyValuePair<char, string[]>[] PalabrasDisco =
        {
            new KeyValuePair<char, string[]>('s', new[] { "original soundtrack", "official soundtrack", "soundtrack album", "soundtrack from", "soundtrack to", "film soundtrack",
                "movie soundtrack", "tv soundtrack", "television soundtrack", "series soundtrack", "game soundtrack", "game ost", "banda sonora", "bandas sonoras",
                "original score", "film score", "game score", "motion picture soundtrack", "music from the motion picture", "original motion picture", "motion picture score",
                "from the motion picture", "music from the film", "music from the movie", "music from the original", "inspired by the film", "inspired by the motion picture",
                "from the netflix", "original television", "music from the tv", "music from the series", "original series",
                "musica de la pelicula", "musica original de la pelicula", "banda sonora original" }),
            new KeyValuePair<char, string[]>('j', new[] { "video game", "video games", "videogame", "videogames", "videojuego", "videojuegos", "game soundtrack", "game ost",
                "game score", "music from the game", "game music" }),
            new KeyValuePair<char, string[]>('m', new[] { "original cast", "cast recording", "cast album", "broadway cast", "original broadway", "original london cast",
                "studio cast", "musical soundtrack", "the musical", "a new musical", "original off broadway", "highlights from the musical" }),
            new KeyValuePair<char, string[]>('k', new[] { "karaoke", "backing track", "backing tracks", "sing along", "singalong" }),
            new KeyValuePair<char, string[]>('i', new[] { "nursery rhymes", "nursery rhyme", "canciones infantiles", "cancion infantil", "para ninos", "for kids", "kids songs",
                "children s songs", "children s music", "songs for children", "baby songs", "musica infantil", "cantos infantiles", "kidz bop" }),
            new KeyValuePair<char, string[]>('d', new[] { "sleep music", "music for sleep", "music for sleeping", "deep sleep", "sleep sounds", "sleep aid", "sleeping music",
                "sleep meditation", "meditation music", "music for meditation", "guided meditation", "meditacion guiada", "musica para meditar", "para meditar", "para dormir",
                "relaxing music", "relaxation music", "music for relaxation", "musica relajante", "sonidos relajantes", "relajacion y meditacion", "white noise", "ruido blanco",
                "rain sounds", "nature sounds", "sounds of nature", "sonidos de la naturaleza", "ocean waves", "binaural beats", "binaural sleep", "mindfulness meditation",
                "mindfulness music", "yoga music", "music for yoga", "musica para yoga", "yoga and meditation", "lullaby music", "baby lullabies", "lullabies for babies",
                "lullabies for kids", "lullaby renditions", "canciones de cuna", "baby sleep", "spa music", "healing music", "asmr sounds" }),
        };
        // las palabras de cine que NO valen sueltas en cualquier parte del nombre del disco (ver arriba)
        private static readonly string[] AmbiguasS = { "soundtrack", "soundtracks", "ost", "o s t", "bso" };
        // NOMBRES DE ARTISTA (27-11-2026, revision: nunca una palabra suelta DENTRO de un nombre; «The Soundtrack of Our Lives» es un grupo de rock): el
        // nombre entero (sin «the» delante) es una de estas frases, o una de ellas seguida solo de palabras de relleno («Karaoke Band», «Sleep Music
        // Academy»); los canales infantiles de marca valen con lo que sigan («Kidz Bop Kids», «Baby Einstein Music Box Orchestra»)
        private static readonly KeyValuePair<char, string[]>[] PalabrasArtista =
        {
            new KeyValuePair<char, string[]>('s', new[] { "soundtrack", "soundtracks", "ost", "original cast", "film score", "original soundtrack", "movie soundtrack", "tv soundtrack" }),
            new KeyValuePair<char, string[]>('k', new[] { "karaoke", "backing tracks", "sing along", "karaoke version", "karaoke hits" }),
            new KeyValuePair<char, string[]>('i', new[] { "nursery rhymes", "kids songs", "children s songs" }),
            new KeyValuePair<char, string[]>('d', new[] { "sleep music", "white noise", "rain sounds", "nature sounds", "meditation music", "relaxing music", "relaxation music", "yoga music", "lullaby", "lullabies" }),
        };
        private static readonly string[] MarcasInfantiles = { "cocomelon", "pinkfong", "baby shark", "super simple songs", "little baby bum", "baby einstein", "kidz bop" };
        private static readonly HashSet<string> Relleno = new HashSet<string>(StringComparer.Ordinal)
        {
            "band", "kids", "hits", "version", "tracks", "studio", "channel", "academy", "music", "collective", "club", "group", "orchestra", "zone", "therapy", "kingdom", "baby", "babies", "sounds",
        };

        // el genero de XP del artista que lo saca de la pared entero (todos sus discos del catalogo)
        internal static char MarcaDeGenero(string genero)
        {
            switch (genero)
            {
                case "Soundtrack": return 's';
                case "Karaoke": return 'k';
                case "Infantil": return 'i';
                case "Hablado": return 'h';
                default: return '\0';
            }
        }

        private static string Pad(string s)
        {
            string n = MotorBusqueda.Normaliza(s ?? "");
            return n.Length == 0 ? "" : " " + n + " ";
        }
        private static void Busca(string padded, KeyValuePair<char, string[]>[] tabla, StringBuilder marcas)
        {
            if (padded.Length == 0) return;
            foreach (KeyValuePair<char, string[]> m in tabla)
            {
                if (marcas.ToString().IndexOf(m.Key) >= 0) continue;
                foreach (string f in m.Value)
                    if (padded.IndexOf(" " + f + " ", StringComparison.Ordinal) >= 0) { marcas.Append(m.Key); break; }
            }
        }
        private static bool EsAmbiguaS(string tramoNormalizado) { foreach (string w in AmbiguasS) if (tramoNormalizado == w) return true; return false; }
        private static bool ContieneAmbiguaS(string padded) { foreach (string w in AmbiguasS) if (padded.IndexOf(" " + w + " ", StringComparison.Ordinal) >= 0) return true; return false; }
        private static readonly System.Text.RegularExpressions.Regex Entre = new System.Text.RegularExpressions.Regex(@"[\(\[\{]([^\)\]\}]*)[\)\]\}]");
        // «soundtrack», «ost», «bso» en un disco: solo si es TODO el nombre, va entre parentesis o corchetes, o es lo unico tras « - » o «: » («Bad (OST)», «Bad - OST»)
        private static bool CineSuelta(string disco)
        {
            if (string.IsNullOrEmpty(disco)) return false;
            if (EsAmbiguaS(MotorBusqueda.Normaliza(disco))) return true;
            foreach (System.Text.RegularExpressions.Match m in Entre.Matches(disco)) if (ContieneAmbiguaS(Pad(m.Groups[1].Value))) return true;
            foreach (string sep in new[] { " - ", " \u2013 ", " \u2014 ", ": " })
            {
                int p = disco.LastIndexOf(sep, StringComparison.Ordinal);
                if (p >= 0 && EsAmbiguaS(MotorBusqueda.Normaliza(disco.Substring(p + sep.Length)))) return true;
            }
            return false;
        }
        // un NOMBRE DE ARTISTA entero (ver PalabrasArtista): sus marcas
        private static void BuscaArtista(string artista, StringBuilder marcas)
        {
            string n = MotorBusqueda.Normaliza(artista ?? "");
            if (n.StartsWith("the ", StringComparison.Ordinal)) n = n.Substring(4);
            if (n.Length == 0) return;
            foreach (KeyValuePair<char, string[]> m in PalabrasArtista)
            {
                if (marcas.ToString().IndexOf(m.Key) >= 0) continue;
                foreach (string f in m.Value)
                {
                    if (n == f) { marcas.Append(m.Key); break; }
                    if (n.StartsWith(f + " ", StringComparison.Ordinal))
                    {
                        bool todo = true;
                        foreach (string w in n.Substring(f.Length + 1).Split(' ')) if (!Relleno.Contains(w)) { todo = false; break; }
                        if (todo) { marcas.Append(m.Key); break; }
                    }
                }
            }
            if (marcas.ToString().IndexOf('i') < 0)
                foreach (string b in MarcasInfantiles) if (n == b || n.StartsWith(b + " ", StringComparison.Ordinal)) { marcas.Append('i'); break; }
        }

        // LAS MARCAS de un disco del catalogo por lo que se sabe sin la columna del archivo: las palabras de su nombre, las del nombre de su
        // artista y el genero del artista. Letras sin repetir, en el orden de Letras; "" si ninguna.
        internal static string Marcas(string disco, string artista, string generoArtista)
        {
            StringBuilder sb = new StringBuilder(2);
            Busca(Pad(disco), PalabrasDisco, sb);
            if (sb.ToString().IndexOf('s') < 0 && CineSuelta(disco)) sb.Append('s');
            BuscaArtista(artista, sb);
            char g = MarcaDeGenero(generoArtista);
            if (g != '\0' && sb.ToString().IndexOf(g) < 0) sb.Append(g);
            return Ordena(sb.ToString());
        }
        // lo mismo para el ARTISTA entero (su nombre y su genero): si da algo, ninguno de sus discos va
        internal static string MarcasArtista(string artista, string generoArtista)
        {
            StringBuilder sb = new StringBuilder(2);
            BuscaArtista(artista, sb);
            char g = MarcaDeGenero(generoArtista);
            if (g != '\0' && sb.ToString().IndexOf(g) < 0) sb.Append(g);
            return Ordena(sb.ToString());
        }
        // junta las del archivo (columna 7) con las de aqui: sin repetir y en orden; solo letras conocidas
        internal static string Junta(string delArchivo, string deAqui)
        {
            StringBuilder sb = new StringBuilder(4);
            foreach (string s in new[] { delArchivo ?? "", deAqui ?? "" })
                foreach (char c in s) if (Letras.IndexOf(c) >= 0 && sb.ToString().IndexOf(c) < 0) sb.Append(c);
            return Ordena(sb.ToString());
        }
        private static string Ordena(string s)
        {
            if (s.Length < 2) return s;
            StringBuilder o = new StringBuilder(s.Length);
            foreach (char c in Letras) if (s.IndexOf(c) >= 0) o.Append(c);
            return o.ToString();
        }
    }
}

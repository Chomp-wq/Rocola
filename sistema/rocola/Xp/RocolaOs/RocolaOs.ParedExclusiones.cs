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
using System;
using System.Collections.Generic;
using System.Text;

namespace Rocola
{
    internal static class RocolaOsPgExclusiones
    {
        internal const string Letras = "sjmkidh";

        // (marca, frases) de un NOMBRE DE DISCO
        private static readonly KeyValuePair<char, string[]>[] PalabrasDisco =
        {
            new KeyValuePair<char, string[]>('s', new[] { "soundtrack", "soundtracks", "ost", "o s t", "banda sonora", "bandas sonoras", "bso", "original score", "film score",
                "motion picture", "music from the film", "music from the movie", "music from the original", "inspired by the film", "inspired by the motion picture",
                "from the netflix", "television soundtrack", "original television", "music from the tv", "music from the series", "original series",
                "musica de la pelicula", "musica original de la pelicula", "banda sonora original" }),
            new KeyValuePair<char, string[]>('j', new[] { "video game", "video games", "videogame", "videogames", "videojuego", "videojuegos", "game soundtrack", "game ost",
                "game score", "original game", "from the game", "music from the game", "game music" }),
            new KeyValuePair<char, string[]>('m', new[] { "original cast", "cast recording", "cast album", "broadway cast", "original broadway", "original london cast",
                "studio cast", "musical soundtrack", "the musical", "a new musical", "original off broadway", "highlights from the musical" }),
            new KeyValuePair<char, string[]>('k', new[] { "karaoke", "backing track", "backing tracks", "sing along", "singalong" }),
            new KeyValuePair<char, string[]>('i', new[] { "nursery rhymes", "nursery rhyme", "canciones infantiles", "cancion infantil", "para ninos", "for kids", "kids songs",
                "children s songs", "children s music", "songs for children", "baby songs", "musica infantil", "cantos infantiles", "kidz bop" }),
            new KeyValuePair<char, string[]>('d', new[] { "sleep music", "music for sleep", "music for sleeping", "deep sleep", "sleep sounds", "sleep aid", "sleeping music",
                "sleep meditation", "meditation", "meditacion", "para dormir", "relaxing music", "relaxation", "relajante", "relajacion", "white noise", "ruido blanco",
                "rain sounds", "nature sounds", "sounds of nature", "sonidos de la naturaleza", "ocean waves", "binaural", "mindfulness", "yoga", "lullaby", "lullabies",
                "canciones de cuna", "baby sleep", "spa music", "healing music", "asmr" }),
        };
        // (marca, frases) de un NOMBRE DE ARTISTA: los de arriba y los de los canales infantiles y de pistas
        private static readonly KeyValuePair<char, string[]>[] PalabrasArtista =
        {
            new KeyValuePair<char, string[]>('s', new[] { "soundtrack", "ost", "original cast", "film score" }),
            new KeyValuePair<char, string[]>('k', new[] { "karaoke", "backing tracks", "sing along" }),
            new KeyValuePair<char, string[]>('i', new[] { "cocomelon", "pinkfong", "baby shark", "super simple songs", "little baby bum", "baby einstein", "kidz bop", "nursery rhymes" }),
            new KeyValuePair<char, string[]>('d', new[] { "sleep music", "white noise", "rain sounds", "nature sounds", "meditation music", "relaxing music", "lullaby" }),
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

        // LAS MARCAS de un disco del catalogo por lo que se sabe sin la columna del archivo: las palabras de su nombre, las del nombre de su
        // artista y el genero del artista. Letras sin repetir, en el orden de Letras; "" si ninguna.
        internal static string Marcas(string disco, string artista, string generoArtista)
        {
            StringBuilder sb = new StringBuilder(2);
            Busca(Pad(disco), PalabrasDisco, sb);
            Busca(Pad(artista), PalabrasArtista, sb);
            char g = MarcaDeGenero(generoArtista);
            if (g != '\0' && sb.ToString().IndexOf(g) < 0) sb.Append(g);
            return Ordena(sb.ToString());
        }
        // lo mismo para el ARTISTA entero (su nombre y su genero): si da algo, ninguno de sus discos va
        internal static string MarcasArtista(string artista, string generoArtista)
        {
            StringBuilder sb = new StringBuilder(2);
            Busca(Pad(artista), PalabrasArtista, sb);
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

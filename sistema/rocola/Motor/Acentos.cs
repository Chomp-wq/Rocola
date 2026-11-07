// LAS LETRAS SIN ACENTO (7-11-2026). Rocola OS corre sin las tablas de idiomas (InvariantGlobalization, para el A4: menos memoria y
// arranque mas rapido), y alli string.Normalize(FormD) no separa nada: «Canción» seguia siendo «Canción». Por eso el buscador no
// encontraba «cancion», los discos que empiezan con «Á» o «Ñ» se iban a la letra «#» y «Corazón» no casaba con «Corazon» de Spotify.
// Esta tabla hace lo que hacia la normalizacion en la rocola de XP para las letras que salen en la musica: Latin-1 y latin extendido A
// a su letra base (á -> a, Ñ -> N, ł -> l, ő -> o...). Lo que no es letra se queda igual.
namespace Rocola
{
    public static class Acentos
    {
        private const string Latin1 = "AAAAAAACEEEEIIIIDNOOOOO×OUUUUYÞsaaaaaaaceeeeiiiidnooooo÷ouuuuyþy";   // de U+00C0 a U+00FF
        private const string LatinA = "AaAaAaCcCcCcCcDdDdEeEeEeEeEeGgGgGgGgHhHhIiIiIiIiIiIiJjKkkLlLlLlLlLlNnNnNnnNnOoOoOoOoRrRrRrSsSsSsSsTtTtTtUuUuUuUuUuUuWwYyYZzZzZzs";   // de U+0100 a U+017F
        public static char Base(char c)
        {
            if (c < 'À') return c;
            if (c <= 'ÿ') return Latin1[c - 0xC0];
            if (c < 'ƀ') return LatinA[c - 0x100];
            return c;
        }
        // El texto con cada letra en su base (sin crear nada nuevo si no hay que cambiar).
        public static string Quita(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            int i = 0;
            while (i < s.Length && Base(s[i]) == s[i]) i++;
            if (i == s.Length) return s;
            char[] a = s.ToCharArray();
            for (; i < a.Length; i++) a[i] = Base(a[i]);
            return new string(a);
        }
    }
}

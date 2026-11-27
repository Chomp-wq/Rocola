#pragma warning disable CS0649
using System;
namespace Rocola
{
    // lo minimo de los tipos del catalogo que SpotifyEdiciones.cs nombra
    internal class RocolaOsSpotifyCatalogoDisco { public string Nombre; public int NumCanciones; }
    internal class RocolaOsSpotifyCatalogoCancion { public string Artista; public string[] ArtistaIds; }
    internal static class Prueba
    {
        static int mal;
        static void Igual(string a, string b, bool deben)
        {
            string ca = RocolaOsSpotifyEdiciones.Clave(a), cb = RocolaOsSpotifyEdiciones.Clave(b);
            bool ok = (ca == cb) == deben;
            if (!ok) mal++;
            Console.WriteLine((ok ? "BIEN  " : "MAL   ") + "«" + a + "» -> «" + ca + "»  y  «" + b + "» -> «" + cb + "»  (" + (deben ? "misma" : "distinta") + " clave)");
        }
        static int Main()
        {
            Igual("Greatest Hits Vol 1 Deluxe", "Greatest Hits Vol 2 Deluxe", false);
            Igual("Now 20 Deluxe", "Now 21 Deluxe", false);
            Igual("Now 20 Deluxe", "Now 20", true);
            Igual("Greatest Hits Vol 1 Deluxe Edition", "Greatest Hits Vol 1", true);
            Igual("Greatest Hits Vol. 2 (Deluxe)", "Greatest Hits Vol. 1 (Deluxe)", false);
            Igual("Abbey Road (Remastered 2009)", "Abbey Road", true);
            Igual("Rumours - Expanded Edition", "Rumours", true);
            Igual("Eco Total (Taylor's Version)", "Eco Total", false);
            Console.WriteLine(mal == 0 ? "TODO BIEN" : mal + " MAL");
            return mal == 0 ? 0 : 1;
        }
    }
}

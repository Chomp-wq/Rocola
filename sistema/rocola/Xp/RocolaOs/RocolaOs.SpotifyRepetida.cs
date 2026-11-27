// NO SE REPITE LA MISMA CANCION AUNQUE UNA SEA DE SPOTIFY (22-11-2026; FASE11_spotify-nivela.md). El dueno: «si se pone una cancion de
// Spotify, luego se puede poner repetida una que este guardada de manera local en el disco, y sabes que no esta permitido repetir canciones».
// La regla de XP (AddItem, Form1.Ajustes.cs: no entra la misma que la ultima en sonar o en la cola, «Esa ya va seguida», sin cobrar) solo
// comparaba rutas: una de Spotify («spotify:track:ID») y tu MP3 de la misma grabacion pasaban como distintas. Con el modo Spotify, el gancho g230
// las cuenta como la misma, en los dos sentidos:
//   - si tu cancion tiene pareja en la sustitucion invisible (rocola_spotify_pares.txt, RocolaOs.Calidad.cs), es la misma si es esa uri; si la
//     pareja es otra uri (24-11-2026) se sigue con la regla de abajo, salvo que la pareja este confirmada como otra grabacion (Verif -1);
//   - si no, por lo que se sabe de las dos: el titulo base y las marcas de version (las reglas de la pareja: TituloBase y MarcasDe; «- Remastered
//     2012» o «(Remasterizado)» no cuentan, «(En vivo)» solo si las dos lo traen), el artista principal (MismoArtista) y la duracion a 3 s
//     (la tuya, la del repaso; sin ella no se dan por iguales);
//   - dos de Spotify con distinta uri siguen como en XP (distintas): lo pedido es Spotify contra tu MP3, y dos de Spotify del mismo titulo y
//     artista con poca diferencia de duracion pueden ser canciones distintas (las pruebas del nucleo lo hacen a proposito).
// Sin Rocola OS o sin el modo Spotify: false (XP tal cual).
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        // g230 (Codigo/Form1.Ajustes.cs, AddItem): true si «a» e «it» son la misma grabacion aunque sus rutas no lo sean
        private bool RocolaOsMismaCancion(PlayItem a, PlayItem it)
        {
            if (!RocolaOs.Activo || !RocolaOsSpotify.Encendido || a == null || it == null || a.IsVideo || it.IsVideo || a.Song == null || it.Song == null) return false;
            string pa = a.Song.Path, pb = it.Song.Path;
            if (pa == null || pb == null) return false;
            bool sa = RocolaOsSpotify.EsUriDeCancion(pa), sb = RocolaOsSpotify.EsUriDeCancion(pb);
            if (sa == sb) return false;   // dos de XP o dos de Spotify: lo de XP (la ruta)
            string porQue;
            Track local = sa ? it.Song : a.Song, sp = sa ? a.Song : it.Song;
            bool misma = RocolaOsMismaLocalSpotify(local, sp, out porQue);
            if (misma) RocolaOsSpotify.Apunta("no entra: «" + it.Song.Title + "» ya va seguida (" + porQue + ": " + pa + " y " + pb + ")");
            return misma;
        }
        private static void RocolaOsDatosSpotify(Track t, out string titulo, out string artista, out int durMs)
        {
            titulo = t.Title ?? ""; artista = t.Artist ?? ""; durMs = 0;
            RocolaOsSpotifyPista p;
            if (RocolaOsSpotify.Pistas.TryGetValue(t.Path, out p) && p != null)
            {
                if (!string.IsNullOrEmpty(p.Titulo)) titulo = p.Titulo;
                if (!string.IsNullOrEmpty(p.Artista)) artista = p.Artista;
                durMs = p.DuracionMs;
            }
        }
        private bool RocolaOsMismaLocalSpotify(Track local, Track sp, out string porQue)
        {
            RocolaOsCalidad.Pareja par;
            // (23-11-2026, revision) una pareja que la forma del volumen dijo que es OTRA grabacion (Verif -1) no cuenta como la misma
            if (RocolaOsCalidad.Pares.TryGetValue(local.Path, out par) && par != null && par.Id != null)
            {
                if (par.Verif < 0 && string.Equals(par.Uri, sp.Path, StringComparison.Ordinal)) { porQue = "otra grabacion (medida)"; return false; }
                if (string.Equals(par.Uri, sp.Path, StringComparison.Ordinal)) { porQue = "su pareja de la sustitucion"; return true; }
                // (24-11-2026, revision de mejoras) su pareja es OTRA uri: antes se devolvia false sin mirar mas, y la misma grabacion en otra edicion
                // del disco (Spotify tiene varias uris de una cancion) se podia repetir. Solo si la pareja esta confirmada como OTRA grabacion
                // (Verif -1) esta se descarta; si no, cae a la regla de titulo base, marcas, artista y duracion
                if (par.Verif < 0) { porQue = "su pareja es otra grabacion (medida)"; return false; }
            }
            string ts, ars; int ds;
            RocolaOsDatosSpotify(sp, out ts, out ars, out ds);
            return RocolaOsMismaPorDatos(local, ts, ars, ds, out porQue);
        }
        // (27-11-2026) lo de arriba, por los datos de la cancion de Spotify (titulo, artista y duracion): el buscador la usa con lo que dio la busqueda,
        // antes de que la cancion sea una pista de la rocola (RocolaOsSpotifyYaEstaEnLocal, RocolaOs.SpotifyCatalogo.cs)
        private bool RocolaOsMismaPorDatos(Track local, string ts, string ars, int ds, out string porQue)
        {
            int dl = (int)Math.Round(SegundosDe(local.Path) * 1000.0);
            porQue = "mismo titulo, artista y duracion";
            if (ds <= 0 || dl <= 0 || Math.Abs(ds - dl) > RocolaOsSpotify.CopiaToleranciaMs) return false;
            string bl = RocolaOsCalidad.TituloBase(local.Title), bs = RocolaOsCalidad.TituloBase(ts);
            if (bl.Length == 0 || bl != bs || RocolaOsCalidad.MarcasDe(local.Title) != RocolaOsCalidad.MarcasDe(ts)) return false;
            return RocolaOsCalidad.MismoArtista(local.Artist, RocolaOsCalidad.PartesArtista(ars)) || RocolaOsCalidad.MismoArtista(ars, RocolaOsCalidad.PartesArtista(local.Artist));
        }
    }
}

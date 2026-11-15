// LA SUSTITUCION INVISIBLE (fase 11, funcion «calidad_spotify»; FASE11_sustitucion.md): LA BUSQUEDA DE LA PAREJA EN LA API WEB. Una parte del
// cliente del catalogo (RocolaOs.SpotifyCatalogoApi.cs, grupo catalogo): su hilo, su cola de UNA peticion a la vez, su llave de la app y su
// freno de 429. La pide RocolaOs.Calidad.cs SOLO cuando una cancion tuya que suena peor entra a la cola (pagada), con prioridad BAJA (detras de
// lo que alguien espera: buscar, abrir; Alguien = false, como lo de la cuenta) y nunca de fondo.
//   1) con el ISRC del MP3 (marco TSRC):  GET /search?q=isrc:XXXXXXXXXXXX&type=track&limit=10&market=MX
//   2) si no trae ISRC o (1) no dio nada:  GET /search?q=track:"TITULO" artist:"ARTISTA"&type=track&limit=10&market=MX
// Una peticion, dos como mucho. Con 429 (o frenando de antes) no se pregunta nada (Pide lo mira antes de cada una). Lo que se lee de cada
// cancion: su id, su nombre, sus artistas, su duracion, su disco (nombre y tipo: album, single o compilation) y su ISRC (external_ids, que
// volvio en marzo de 2026). Las reglas de la pareja (B) van en RocolaOs.CalidadReglas.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    // UNA CANCION DE SPOTIFY CANDIDATA A PAREJA (lo que contesta la busqueda, leido)
    internal sealed class RocolaOsCalidadCandidata
    {
        internal string Id, Titulo, Disco, TipoDisco, Isrc;
        internal List<string> Artistas = new List<string>();
        internal int DuracionMs;
    }
    // LO QUE CONTESTA EL PEDIDO «pareja»
    internal sealed class RocolaOsCalidadRespuesta
    {
        internal bool PorIsrc;                         // la lista es de la busqueda por ISRC
        internal List<RocolaOsCalidadCandidata> Candidatas = new List<RocolaOsCalidadCandidata>();
        internal int Peticiones;                       // las que costo (1 o 2; los reintentos de la red cuentan aparte en el catalogo)
    }

    internal static partial class RocolaOsSpotifyCatalogoApi
    {
        private static long peticionesPareja;
        internal static long PeticionesPareja { get { return Interlocked.Read(ref peticionesPareja); } }
        private const char Sep = '\u0001';

        // PIDE LA PAREJA (desde cualquier hilo): con prioridad baja; «listo» corre en el hilo del catalogo con la respuesta
        // (RocolaOsCalidadRespuesta) o el error. Sin titulo ni ISRC no pide nada.
        internal static void BuscaPareja(string isrc, string titulo, string artista, Action<object, string> listo)
        {
            Encola(new Pedido { Que = "pareja", Clave = (isrc ?? "") + Sep + (titulo ?? "") + Sep + (artista ?? ""), Alguien = false, Listo = listo });
        }

        private static object AtiendePareja(Config c, string clave, out string error)
        {
            error = null;
            string[] k = (clave ?? "").Split(Sep);
            string isrc = k.Length > 0 ? k[0] : "", titulo = k.Length > 1 ? k[1] : "", artista = k.Length > 2 ? k[2] : "";
            string m = Uri.EscapeDataString(c.Mercado);
            RocolaOsCalidadRespuesta r = new RocolaOsCalidadRespuesta();
            if (isrc.Length == 12)
            {
                long antes = PeticionesPareja;
                JsonDocument d = Pide(c, false, "/search?q=" + Uri.EscapeDataString("isrc:" + isrc) + "&type=track&limit=10&market=" + m, ref peticionesPareja, true, out error);
                r.Peticiones += (int)(PeticionesPareja - antes);
                if (d == null) return null;
                using (d) LeeCandidatas(d.RootElement, r.Candidatas);
                // solo valen las que traen ESE ISRC (la de verdad busca por el campo; aqui se comprueba)
                r.Candidatas.RemoveAll(x => !string.Equals(x.Isrc, isrc, StringComparison.Ordinal));
                if (r.Candidatas.Count > 0) { r.PorIsrc = true; return r; }
            }
            if (titulo.Trim().Length == 0 || artista.Trim().Length == 0) return r;
            {
                long antes = PeticionesPareja;
                string q = "track:\"" + Comillas(titulo) + "\" artist:\"" + Comillas(artista) + "\"";
                JsonDocument d = Pide(c, false, "/search?q=" + Uri.EscapeDataString(q) + "&type=track&limit=10&market=" + m, ref peticionesPareja, true, out error);
                r.Peticiones += (int)(PeticionesPareja - antes);
                if (d == null) return null;
                using (d) LeeCandidatas(d.RootElement, r.Candidatas);
            }
            return r;
        }
        // (dentro de las comillas de la consulta no van comillas)
        private static string Comillas(string s) { return (s ?? "").Replace('"', ' ').Trim(); }

        private static void LeeCandidatas(JsonElement raiz, List<RocolaOsCalidadCandidata> l)
        {
            JsonElement s, items;
            if (!raiz.TryGetProperty("tracks", out s) || s.ValueKind != JsonValueKind.Object || !s.TryGetProperty("items", out items) || items.ValueKind != JsonValueKind.Array) return;
            foreach (JsonElement t in items.EnumerateArray())
            {
                if (t.ValueKind != JsonValueKind.Object || !SePuede(t)) continue;
                string id = Texto(t, "id");
                if (string.IsNullOrEmpty(id) || RocolaOsSpotify.IdBase62("spotify:track:" + id, "spotify:track:").Length == 0) continue;
                RocolaOsCalidadCandidata x = new RocolaOsCalidadCandidata { Id = id, Titulo = Texto(t, "name") ?? "", DuracionMs = Entero(t, "duration_ms") };
                JsonElement a;
                if (t.TryGetProperty("artists", out a) && a.ValueKind == JsonValueKind.Array)
                    foreach (JsonElement y in a.EnumerateArray()) { string n = Texto(y, "name"); if (!string.IsNullOrEmpty(n)) x.Artistas.Add(n); }
                JsonElement al;
                if (t.TryGetProperty("album", out al) && al.ValueKind == JsonValueKind.Object) { x.Disco = Texto(al, "name") ?? ""; x.TipoDisco = Texto(al, "album_type") ?? ""; }
                JsonElement ei;
                if (t.TryGetProperty("external_ids", out ei) && ei.ValueKind == JsonValueKind.Object) x.Isrc = RocolaOsCalidad.LimpiaIsrc(Texto(ei, "isrc"));
                l.Add(x);
            }
        }
    }
}

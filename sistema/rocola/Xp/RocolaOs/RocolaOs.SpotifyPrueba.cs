// SPOTIFY EN LA ROCOLA (fase 11, grupo spotify-xp): UN DOBLE DEL CATALOGO, SOLO PARA LAS PRUEBAS (Pruebas/RocolaOs/spotify-xp/). No hace
// nada en la rocola: es una funcion de Rocola OS de mentira, «spotify-prueba», que solo va con ROCOLA_OS=lista que la nombre (SoloEnLaLista:
// con «1» nunca, diga lo que diga rocola_os.txt). En su latido (hilo de la pantalla) lee $ROCOLA_RUN/rocola-xp/spotify/prueba-pide, un
// renglon por pedido, y lo hace con I6 como lo haria el catalogo:
//   pide URI|TITULO|ARTISTA|DISCO|MS[|PORTADA_URL[|DISCO_URI]]    RocolaOsSpotifyPide (como PONER en un disco)
//   busca URI|TITULO|ARTISTA|DISCO|MS[|PORTADA_URL[|DISCO_URI]]   RocolaOsSpotifyPide(p, true) (como desde el buscador)
//   portada URI|TITULO|ARTISTA|DISCO|MS|PORTADA_URL[|DISCO_URI]   RocolaOsSpotifyPortada (como el catalogo al ensenarla)
// y apunta lo que paso en prueba-pide.log (con RocolaOsSpotifyListo y los creditos de XP antes y despues).
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Rocola
{
    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraSpotifyPrueba()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "spotify-prueba",
                Que = "el doble del catalogo de Spotify, solo para las pruebas (ROCOLA_OS=...,spotify-prueba)",
                DeFabrica = false,
                SoloEnLaLista = true,
                Latido = f => f.RocolaOsLatidoSpotifyPrueba(),
            });
        }
        private void RocolaOsLatidoSpotifyPrueba()
        {
            string ruta = RocolaOsSpotify.RunSpotify("prueba-pide");
            if (!File.Exists(ruta)) return;
            string[] ls;
            try { ls = File.ReadAllLines(ruta); File.Delete(ruta); } catch { return; }
            StringBuilder log = new StringBuilder();
            foreach (string l0 in ls)
            {
                string l = l0.Trim();
                if (l.Length == 0) continue;
                int sp = l.IndexOf(' ');
                string que = sp > 0 ? l.Substring(0, sp) : l, resto = sp > 0 ? l.Substring(sp + 1) : "";
                string[] c = resto.Split('|');
                if ((que != "pide" && que != "busca" && que != "portada") || c.Length < 5) { log.Append("no entiendo: ").Append(l).Append('\n'); continue; }
                int ms; int.TryParse(c[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                RocolaOsSpotifyPista p = new RocolaOsSpotifyPista { Uri = c[0], Titulo = c[1], Artista = c[2], Disco = c[3], DuracionMs = ms, PortadaUrl = c.Length > 5 && c[5].Length > 0 ? c[5] : null, DiscoUri = c.Length > 6 ? c[6] : null };
                if (que == "portada")
                {
                    string r = RocolaOsSpotifyPortada(p);
                    log.Append("portada ").Append(p.Uri).Append(' ').Append(r ?? "(pedida)").Append('\n');
                    continue;
                }
                int antes = credits;
                bool listo = RocolaOsSpotifyListo;
                bool entro = RocolaOsSpotifyPide(p, que == "busca");
                log.Append(que).Append(' ').Append(p.Uri).Append(" listo=").Append(listo ? 1 : 0).Append(" entro=").Append(entro ? 1 : 0)
                   .Append(" creditos ").Append(antes.ToString(CultureInfo.InvariantCulture)).Append("->").Append(credits.ToString(CultureInfo.InvariantCulture))
                   .Append(" cola=").Append(queue.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            try { File.AppendAllText(ruta + ".log", log.ToString()); } catch { }
        }
    }
}

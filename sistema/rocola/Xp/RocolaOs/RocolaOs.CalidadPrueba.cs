// LA SUSTITUCION INVISIBLE: UN DOBLE PARA LAS PRUEBAS (Pruebas/RocolaOs/sustitucion/), como «spotify-prueba» del nucleo. No hace nada en la
// rocola: la funcion «calidad-prueba» solo va con ROCOLA_OS=lista que la nombre (SoloEnLaLista). En su latido (hilo de la pantalla) lee
// $ROCOLA_RUN/rocola-xp/spotify/prueba-xp, un renglon por orden, y apunta lo que paso en prueba-xp.log:
//   pon FINAL     pide la cancion tuya cuya ruta acaba en FINAL con AddSong de XP (lo mismo que PONER en su disco: cobro, turno y avisos)
//   mira          lo que la rocola da por sonando: la ruta, el titulo, el artista, el disco y la portada de nowPlaying (lo que pinta XP), y de
//                 que clase es el lector (de XP o la version de Spotify)
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
        internal static void RocolaOsRegistraCalidadPrueba()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "calidad-prueba",
                Que = "el doble de la sustitucion invisible, solo para las pruebas (ROCOLA_OS=...,calidad-prueba)",
                DeFabrica = false,
                SoloEnLaLista = true,
                Latido = f => f.RocolaOsLatidoCalidadPrueba(),
            });
        }
        private void RocolaOsLatidoCalidadPrueba()
        {
            string ruta = RocolaOsSpotify.RunSpotify("prueba-xp");
            if (!File.Exists(ruta)) return;
            string[] ls;
            try { ls = File.ReadAllLines(ruta); File.Delete(ruta); } catch { return; }
            StringBuilder log = new StringBuilder();
            foreach (string l0 in ls)
            {
                string l = l0.Trim();
                if (l.Length == 0) continue;
                if (l == "mira")
                {
                    PlayItem np = nowPlaying;
                    string lector = reader == null ? "ninguno" : reader is RocolaOsFuenteCalidad ? "spotify" : "xp";
                    if (np == null || np.Song == null) log.Append("mira nada lector=").Append(lector).Append('\n');
                    else log.Append("mira ").Append(np.Song.Path).Append(" | ").Append(np.Song.Title).Append(" | ").Append(np.Song.Artist).Append(" | ").Append(np.Song.Album)
                            .Append(" | ").Append(np.Disc != null ? np.Disc.Name + " | " + np.Disc.CoverPath : "- | -").Append(" | lector=").Append(lector)
                            .Append(" creditos=").Append(credits.ToString(CultureInfo.InvariantCulture)).Append(" cola=").Append(queue.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
                    continue;
                }
                if (!l.StartsWith("pon ", StringComparison.Ordinal)) { log.Append("no entiendo: ").Append(l).Append('\n'); continue; }
                string fin = l.Substring(4);
                Track t = null; Album d = null;
                foreach (Album a in albums)
                {
                    if (a == null || a.Tracks == null) continue;
                    foreach (Track x in a.Tracks) if (x != null && x.Path != null && x.Path.EndsWith(fin, StringComparison.Ordinal)) { t = x; d = a; break; }
                    if (t != null) break;
                }
                if (t == null) { log.Append("pon ").Append(fin).Append(" no esta en la biblioteca\n"); continue; }
                int antes = credits;
                AddSong(t, d);
                log.Append("pon ").Append(fin).Append(" creditos ").Append(antes.ToString(CultureInfo.InvariantCulture)).Append("->").Append(credits.ToString(CultureInfo.InvariantCulture))
                   .Append(" cola=").Append(queue.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            try { File.AppendAllText(ruta + ".log", log.ToString()); } catch { }
        }
    }
}

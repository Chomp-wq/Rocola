// EL DOBLE DE I6 (fase 11, grupo catalogo): SOLO mientras el nucleo (grupo spotify-xp) no esta juntado. AL JUNTAR EL NUCLEO SE BORRA ESTE
// ARCHIVO ENTERO: el nucleo da lo mismo de verdad (la funcion «spotify», RocolaOsSpotifyPista, RocolaOsSpotifyPide y RocolaOsSpotifyListo;
// FASE11_catalogo.md, «La junta con el nucleo»).
//
// Lo que hace el doble, para que el catalogo se pueda probar sin Soloist:
//   - registra la funcion «spotify» (I3: spotify=1 la enciende; de fabrica APAGADA) con su clave spotify_nombre (de fabrica «Rocola»);
//   - RocolaOsSpotifyListo: si ROCOLA_SPOTIFY_DOBLE_LISTO no es «0» (las pruebas lo ponen para ver que sin sesion no se pide nada);
//   - RocolaOsSpotifyPide: lo de XP al pedir con PONER (los creditos, «Inserta una moneda» sin ellos, el aviso de la cancion agregada con su
//     turno), SIN meter nada en la cola de XP (eso es del nucleo: con una ruta «spotify:» XP no sabria tocarla), y apunta cada pedido en
//     ROCOLA_RUN/rocola-xp/spotify/doble-pedidos (una linea: uri, titulo, artista, disco, duracion, portada, disco_uri) para las pruebas.
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Rocola
{
    // I6: una cancion de Spotify que pide el catalogo (la clase la da el nucleo)
    internal sealed class RocolaOsSpotifyPista
    {
        internal string Uri, Titulo, Artista, Disco, PortadaUrl, DiscoUri;
        internal int DuracionMs;
    }

    internal static class RocolaOsSpotifyCatalogoDoble
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = "spotify",
                Que = "el modo Spotify (DOBLE del catalogo: se borra al juntar el nucleo)",
                DeFabrica = false,
                Claves = new RocolaOsClave[] { RocolaOsClave.Texto("spotify_nombre", "Rocola", null, false, "el nombre de la bocina de Spotify") },
            });
        }
        internal static int Pedidas;
    }

    public partial class Form1
    {
        // I6: Soloist con sesion (el doble: ROCOLA_SPOTIFY_DOBLE_LISTO, de fabrica si)
        internal bool RocolaOsSpotifyListo
        {
            get
            {
                string v = Environment.GetEnvironmentVariable("ROCOLA_SPOTIFY_DOBLE_LISTO");
                return RocolaOs.Encendida("spotify") && v != "0";
            }
        }

        // I6: pedir una de Spotify como XP pide una suya con PONER (en el hilo de la pantalla); true si entro
        internal bool RocolaOsSpotifyPide(RocolaOsSpotifyPista p)
        {
            if (p == null || !RocolaOs.Encendida("spotify")) return false;
            if (credits <= 0)
            {
                if (pastCred != null) { pastCred.Aviso(); pastCred.Invalidate(); }
                if (searchOpen) ShowSearchToast("Inserta una moneda", false);
                else ShowToast("Inserta una moneda", System.Drawing.Color.FromArgb(200, 130, 35));
                RocolaOsSpotifyDobleApunta("sin_credito", p);
                return false;
            }
            credits--;
            UpdateCredits();
            RocolaOsSpotifyCatalogoDoble.Pedidas++;
            turnoPuesto = queue.Count + (nowPlaying != null ? 1 : 0) + RocolaOsSpotifyCatalogoDoble.Pedidas;
            turnoSegs = 0;
            ShowSongAddedToast(searchOpen);
            RocolaOsSpotifyDobleApunta("pedida", p);
            return true;
        }
        private static void RocolaOsSpotifyDobleApunta(string que, RocolaOsSpotifyPista p)
        {
            try
            {
                string d = RocolaOs.Run("rocola-xp/spotify");
                Directory.CreateDirectory(d);
                StringBuilder sb = new StringBuilder();
                sb.Append(que).Append('\t').Append(p.Uri).Append('\t').Append(p.Titulo).Append('\t').Append(p.Artista).Append('\t').Append(p.Disco).Append('\t')
                  .Append(p.DuracionMs.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(p.PortadaUrl).Append('\t').Append(p.DiscoUri).Append('\n');
                File.AppendAllText(Path.Combine(d, "doble-pedidos"), sb.ToString());
            }
            catch { }
        }
    }
}

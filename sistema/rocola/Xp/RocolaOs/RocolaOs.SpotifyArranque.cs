// SIN SILENCIO AL EMPEZAR UNA DE SPOTIFY (fase 11, funcion «spotify_arranque», encendida de fabrica; FASE11_spotify-nivela.md, seccion 8).
// Peticion del dueno del 27-11-2026 (de las mejoras de Spotify que eligio: «silencio al empezar»). A veces una cancion de Spotify tarda en
// sonar de verdad: Soloist entrega primero silencio digital (ceros) y lo que se oye despues es lo mismo con ese hueco delante. Medido con el
// Soloist falso (SOLOIST_FALSO_SILENCIO=MS: los primeros MS ms de cada cancion en ceros): sin esta funcion, el silencio se oye entero.
//
// DE DONDE VIENE (lo que se midio en la fuente y en el anillo, RocolaOs.SpotifyFuente.cs y RocolaOs.SpotifyAnillo.cs):
//   - el arranque sin hueco de la fuente (ArranqueSinHuecoMs, unos 1,3 s): XP lee de golpe su primer segundo y su colchon, y la fuente no
//     toca el anillo hasta haberlo entregado (en silencio) para que el anillo no se seque. Eso es de la tarjeta de XP y se queda (quitarlo
//     deja un hueco DENTRO de la cancion); no es lo que se recorta aqui;
//   - el silencio digital que Soloist trae al principio de la cancion: este SI. El anillo da lo que hay a la velocidad del lector, asi que
//     consumirlo sin entregarlo lo adelanta: la musica real empieza cuando llega, no tras su silencio.
// QUE HACE: la fuente (RocolaOsFuenteSpotify.RecortaSilencioInicial), en su hilo y sin esperar, descarta lo primero que llega mientras sea
// silencio (todo cuadro <= UmbralSilencio, -80 dBFS: ni el principio mas suave de una balada), hasta TopeSilencioMs en total; la musica sale
// tal cual desde su primer cuadro, con un fundido de 1 ms que evita el chasquido si empieza lejos de cero. Lo que pasa del tope es de la
// grabacion (un silencio de verdad, una pista oculta) y suena. Una retomada (volvio el aparato de sonido) o lo que sigue a un corte no se
// recorta (ya sonaron). Sirve a las de Spotify pedidas y a las tuyas sustituidas (que usan la misma fuente). Apagada (spotify_arranque=0 en
// rocola_os.txt, desde el siguiente arranque), sin el modo Spotify o sin Rocola OS: como antes, bit a bit.
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Rocola
{
    internal static class RocolaOsSpotifyArranque
    {
        internal const string Nombre = "spotify_arranque";
        internal const float UmbralSilencio = 0.0001f;   // -80 dBFS
        internal const int TopeSilencioMs = 1500;
        private static int encendida;
        internal static bool Va { get { return Volatile.Read(ref encendida) != 0 && RocolaOsSpotify.Encendido; } }
        internal static void Arranca() { Volatile.Write(ref encendida, 1); }
        internal static long Canciones, Milisegundos;
        // una linea de bitacora por cancion (la fuente la llama una vez, al empezar la musica o al llegar al tope)
        internal static void Recortado(string uri, double ms, bool tope, long desdeLaLecturaMs, double entregadosMs)
        {
            Interlocked.Increment(ref Canciones); Interlocked.Add(ref Milisegundos, (long)ms);
            RocolaOsSpotify.Apunta("arranque de " + uri + ": " + ms.ToString("0", CultureInfo.InvariantCulture) + " ms de silencio digital al principio quitados"
                + (tope ? " (el tope: " + TopeSilencioMs.ToString(CultureInfo.InvariantCulture) + " ms; lo que sigue es de la grabacion)" : "")
                + "; a los " + desdeLaLecturaMs.ToString(CultureInfo.InvariantCulture) + " ms de la primera lectura, con " + entregadosMs.ToString("0", CultureInfo.InvariantCulture) + " ms ya entregados");
        }
    }

    public partial class Form1
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraSpotifyArranque()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsSpotifyArranque.Nombre,
                Que = "sin el silencio digital del principio de las canciones de Spotify (hasta 1,5 s; necesita «spotify»)",
                DeFabrica = true,
                Arranca = f => RocolaOsSpotifyArranque.Arranca(),
            });
        }
    }
}

// LO QUE SE QUEDA ABIERTO DEL BUSCADOR SE CIERRA SOLO (Rocola OS, modo Spotify; el dueno, 22-11-2026: «si un cliente se queda dentro de una
// pared de discos temporal de Spotify, despues de cierto tiempo se debe cerrar eso y el buscador, como debe de ser, y tambien si alguien se
// queda dentro de un disco, despues de 30 segundos se saldra automaticamente de el»; FASE11_paredes-buscador.md).
// Una pared del buscador a los 60 s sin tocar nada (como el buscador de XP) y un disco de Spotify abierto desde el buscador o desde una de
// esas paredes a los 30 s (como un disco de XP) cierran TODO (la pila de paredes y el buscador, sin abrirlo otra vez): la rocola queda en la
// pared de XP en la letra, la pagina y la eleccion de antes de abrir el buscador (g248, g249). Con las excepciones de XP salvo el video
// (23-11-2026: con un video puesto tambien se cierra): mantenimiento, la ventana de ajustes (y el asistente a la vista, que se despide solo).
// Un disco de XP sigue saliendo a su pared a los 30 s (XP), y en Rocola OS tambien con un video puesto (23-11-2026).
using System;

namespace Rocola
{
    public partial class Form1
    {
        internal const int RocolaOsParedIdleSecs = 60;     // como el buscador de XP

        // la vuelta de mas abajo de la pila: lo de XP de antes de abrir el buscador
        private static RocolaOsSpotifyVuelta RocolaOsBaseDe(RocolaOsSpotifyVuelta v)
        {
            int n = 0;
            while (v != null && v.Pared != null && v.Pared.Vuelta != null && n++ < 8) v = v.Pared.Vuelta;
            return v;
        }
        // las excepciones de XP (y el asistente a la vista, que se despide solo a los 45 s). (23-11-2026, el dueno: «en cualquier situacion donde el
        // buscador o algo relacionado a el se quede abierto, el buscador debera cerrarse por inactividad») el video YA NO es excepcion: con un
        // video puesto una pared del buscador o un disco abierto desde el se quedaban abiertos para siempre (el disco de XP se queda: es de XP)
        private bool RocolaOsInactivoVale()
        {
            return RocolaOs.Activo && !maintMode && (lblConfig == null || !lblConfig.Visible) && !asistenteVisible;
        }
        // CIERRA TODO: la pila de paredes y discos del buscador, sin abrir el buscador; la pared de XP como estaba antes de abrirlo
        private void RocolaOsSpotifyCierraTodo(RocolaOsSpotifyVuelta v, string porque)
        {
            RocolaOsSpotifyVuelta b = RocolaOsBaseDe(v);
            rocolaOsSpVueltaDisco = null; rocolaOsSpVueltaAlbum = null;
            if (rocolaOsSpPared != null) rocolaOsSpPared.PaginaPub = -1;
            rocolaOsSpPared = null;
            if (searchOpen) CloseSearch();
            char letra = b != null ? b.Letra : currentLetter;
            if (letra == RocolaOsSpotifyParedMarca) letra = RocolaOsSpotifyPrimeraLetra();
            currentLetter = letra;
            page = b != null && b.Letra == letra ? b.Pagina : 0;
            selAlbumIndex = b != null && b.Letra == letra ? b.Elegido : 0;
            if (panelSongs.Visible) { BackToAlbums(); AbortDiscTrans(); }
            if (letterBar != null) Letter_Refresh();
            if (!libBuilding) RefreshAlbums();
            RocolaOs.Apunta("spotify_catalogo", "se cierra todo lo del buscador (" + porque + "): la pared de XP, letra " + letra);
        }
        // g248 (Form1.cs, el disco abierto a los 30 s sin tocar nada): un disco de Spotify abierto desde el buscador o desde una pared del
        // buscador no vuelve a ellos: se cierra todo. true = lo atendio; si no (y de XP), BackToAlbums de XP.
        private bool RocolaOsSpotifyDiscoInactivo()
        {
            if (!RocolaOs.Activo) return false;
            RocolaOsSpotifyVuelta v = rocolaOsSpVueltaDisco;
            if (v != null && ReferenceEquals(currentAlbum, rocolaOsSpVueltaAlbum)) { RocolaOsSpotifyCierraTodo(v, "un disco del buscador, 30 s sin tocar nada"); return true; }
            if (currentLetter == RocolaOsSpotifyParedMarca && rocolaOsSpPared != null && rocolaOsSpPared.Vuelta != null)
            { RocolaOsSpotifyCierraTodo(rocolaOsSpPared.Vuelta, "un disco de una pared del buscador, 30 s sin tocar nada"); return true; }
            return false;
        }
        // g249 (Form1.cs, el reloj de la pantalla, despues del disco): una pared del buscador a los 60 s sin tocar nada se cierra toda.
        // true = la cerro (y ese latido no sigue); de XP, false.
        private bool RocolaOsSpotifyParedInactiva()
        {
            if (!RocolaOs.Activo || searchOpen) return false;
            if (panelSongs != null && panelSongs.Visible)
            {
                // el disco va por su lado (g248), salvo con un video puesto: ahi el reloj del disco de XP no corre. (23-11-2026, el dueno: «Si un
                // usuario se queda dentro de un disco, se debe salir de el a los 30 segundos por inactividad») con video tambien sale a los 30 s:
                // el del buscador cierra todo y cualquier otro vuelve a su pared, como XP sin video
                if (!videoOn || !RocolaOsInactivoVale() || (DateTime.Now - lastInput).TotalSeconds < DiscoIdleSecs) return false;
                if (!RocolaOsSpotifyDiscoInactivo()) BackToAlbums();
                return true;
            }
            if (currentLetter != RocolaOsSpotifyParedMarca || rocolaOsSpPared == null) return false;
            if (!RocolaOsInactivoVale() || (DateTime.Now - lastInput).TotalSeconds < RocolaOsParedIdleSecs) return false;
            RocolaOsSpotifyCierraTodo(rocolaOsSpPared.Vuelta, "una pared del buscador, 60 s sin tocar nada");
            return true;
        }
    }
}

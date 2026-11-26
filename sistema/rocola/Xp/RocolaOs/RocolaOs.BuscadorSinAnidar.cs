// NI OTRO BUSCADOR NI EL ASISTENTE DENTRO DEL BUSCADOR (Rocola OS, modo Spotify; el dueno, 26-11-2026: «se puede abrir otro buscador dentro de
// una pared de discos temporal, y dentro del buscador se puede abrir el asistente, tampoco se debe poder abrir el asistente dentro de una pared
// de discos temporal»). Dentro de lo del buscador (el buscador abierto, una pared del buscador, un disco abierto desde ella o desde el buscador)
// ROJO+ABAJO y la casilla AYUDA ya no abren la estrella, y en las paredes y discos del buscador ROJO+ARRIBA ya no abre OTRO buscador (cerrar el
// que esta abierto sigue igual). Sin avisos: simplemente no se abre. Lo que ya estaba abierto (la estrella guiando hacia el buscador) sigue igual.
// Ganchos: g279 (Form1.cs, ROJO+ARRIBA) y g280 (AsistenteRocola.cs, AbreAsistente). Sin Rocola OS, XP tal cual.
using System;

namespace Rocola
{
    public partial class Form1
    {
        // una pared del buscador a la vista (o un disco abierto desde ella), o un disco abierto desde el buscador con su vuelta puesta (las mismas
        // condiciones de RocolaOsSpotifyDiscoInactivo). NO incluye el buscador abierto.
        internal bool RocolaOsEnLoDelBuscador()
        {
            if (!RocolaOs.Activo) return false;
            if (currentLetter == RocolaOsSpotifyParedMarca && rocolaOsSpPared != null) return true;
            return rocolaOsSpVueltaDisco != null && ReferenceEquals(currentAlbum, rocolaOsSpVueltaAlbum);
        }
        // g279: ROJO+ARRIBA con el buscador cerrado no abre otro dentro de lo del buscador (true = no abrir)
        internal bool RocolaOsSinBuscadorNuevo() { return RocolaOsEnLoDelBuscador(); }
        // g280: la estrella no se ABRE con el buscador abierto ni dentro de lo del buscador
        internal bool RocolaOsSinAsistente() { return RocolaOs.Activo && (searchOpen || RocolaOsEnLoDelBuscador()); }
    }
}

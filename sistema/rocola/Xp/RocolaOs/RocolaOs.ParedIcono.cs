// LA LETRA ♫ DE SPOTIFY CON SU ICONO (Rocola OS; peticion del dueno del 27-11-2026, al devolver la pared de Spotify: «la letra ♫ vuelve al final del
// abecedario, pero la casilla lleva el ICONO de Spotify en vez del texto, y debe leerse bien en 1024x768»). La barra de letras de XP escribe cada
// letra con su fuente; la casilla de la letra de Spotify (RocolaOsSpotifyMarca, «♫») no se escribe: en su sitio va el circulo verde con las tres ondas
// negras que ya dibuja la cabecera de las paredes del buscador (RocolaOsIconoSpotify, RocolaOs.ParedLetrero.cs), del tamano de la casilla.
//
// Ganchos (Codigo/Form1.Vistas.cs, LetterBar): g390 Grabada (la letra de la tira, tambien deslizandose la capsula y bajo la lupa), g391 la letra
// elegida (en su capsula) y g392 la elegida bajo la lupa de la tarjeta. Cada uno pregunta aqui y, si la letra es la ♫ con el modo Spotify,
// dibuja el icono y dice que ya lo hizo; en cualquier otra letra, o sin el modo, devuelve false y XP escribe la suya como siempre.
using System;
using System.Drawing;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // ¿es la letra de Spotify? (solo la casilla de la barra: un texto de una sola ♫)
        private static bool RocolaOsLetraEsSpotify(string texto)
        {
            return texto != null && texto.Length == 1 && texto[0] == RocolaOsSpotifyMarca && RocolaOs.Activo && RocolaOsSpotifyLetraEnLaPared;
        }

        // El diametro del icono dentro de la casilla de una letra (r: la casilla). Cabe en la capsula de la elegida (la casilla menos 3,5 px por
        // lado, con medio pixel de aire) y en 1024x768 (casilla de ~34 x 28) mide 22 px: se lee de lejos. Funcion pura: la prueba mide tambien su cuenta.
        internal static float RocolaOsLetraIconoDiametro(float ancho, float alto)
        {
            float d = Math.Min(ancho - 7f, alto - 6f);
            return Math.Max(8f, d);
        }

        // g390-g392: el icono en lugar del texto de la casilla; true = ya esta puesto (alfa: 0 a 255)
        // escala: lo que la lupa de la tarjeta crece la casilla (g392: la letra elegida, 1,45 como mucho; el lienzo ya viene agrandado): el icono se achica lo mismo
        // para que, ya agrandado, siga cabiendo en la capsula (el texto de una letra lo limita TopeLupa; el icono no tiene texto que medir)
        private static bool RocolaOsLetraIcono(SKCanvas g, string texto, RectangleF casilla, int alfa, bool copia, bool medio, float escala = 1f)
        {
            if (!RocolaOsLetraEsSpotify(texto)) return false;
            float d = RocolaOsLetraIconoDiametro(casilla.Width, casilla.Height) / Math.Max(1f, escala);
            RocolaOsIconoSpotify(g, casilla.X + casilla.Width / 2f, casilla.Y + casilla.Height / 2f, d / 2f, copia, medio, Math.Max(0, Math.Min(255, alfa)));
            return true;
        }
    }
}

// «LAS MEJORES CANCIONES DE...» EN EL BUSCADOR: LA PARTE DE LA CASILLA Y EL TECLADO (Rocola OS, modo Spotify; el dueno, 22-11-2026: «Ahora en el
// buscador estara la de buscar las mejores canciones de cualquier artista, hazlo intuitivo con un boton que ponga en la barra de busqueda LAS
// MEJORES CANCIONES DE - El cliente escribe el nombre del artista»). Una parte del SearchOverlay de XP (el buscador con portadas,
// Codigo/BuscadorPortadasGpu.cs): el PREFIJO FIJO de la casilla (una pastilla delante de lo escrito, que la tecla BORRAR no borra), el color del
// atajo y la etiqueta de la tarjeta. La logica (el atajo, la tarjeta, el disco y las canciones tuyas) esta en RocolaOs.Mejores.cs.
// Ganchos: g400 a g410 (GANCHOS.tsv). Sin el modo Spotify y la llave de la API, ningun atajo, ningun prefijo: el buscador de XP tal cual.
using System;
using System.Drawing;
using SkiaSharp;
using Rocola.Nativo;

namespace Rocola
{
    public partial class Form1
    {
        private partial class SearchOverlay
        {
            // null = sin prefijo (el buscador de XP); si no, el texto de la pastilla («LAS MEJORES CANCIONES DE»). Lo pone y quita Form1
            // (RocolaOs.Mejores.cs) y lo quita tambien REGRESAR con nada escrito (RocolaOsQuitaPrefijo, g405).
            public string Prefijo;
            public bool RocolaOsHayChip;                  // el atajo «LAS MEJORES CANCIONES DE…» es el primero de la fila (g400)
            public Action<bool> RocolaOsAlPrefijo;        // avisa a Form1 cuando REGRESAR lo quita

            // g405: REGRESAR con nada escrito y el prefijo puesto lo quita. true = lo quito (y no hace nada mas)
            internal bool RocolaOsQuitaPrefijo()
            {
                if (Prefijo == null || Escrito.Length > 0) return false;
                Prefijo = null;
                if (RocolaOsAlPrefijo != null) RocolaOsAlPrefijo(false);
                Invalidate();
                return true;
            }
            // g406: la tecla BORRAR del teclado en pantalla con nada escrito y el prefijo puesto: no hace nada (el prefijo no se borra con ella)
            internal bool RocolaOsBorrarNoQuita() { return Prefijo != null && Escrito.Length == 0; }

            // g403: lo que pide la casilla vacia (con el prefijo, solo el nombre del artista)
            internal string RocolaOsInvita(string deXp) { return Prefijo != null ? "Escribe el nombre del artista" : deXp; }

            // g409 y g410: el atajo «LAS MEJORES CANCIONES DE…» (el primero de la fila) en ambar, mas fuerte con el prefijo puesto;
            // los demas atajos, el color de XP. cual: 0 = el fondo, 1 = el borde
            internal Color RocolaOsColorAtajo(int i, Color deXp, int cual)
            {
                if (i != 0 || !RocolaOsHayChip) return deXp;
                // el ambar de «LO MEJOR» y de SONANDO: se ve aunque el color del buscador sea el azul de los demas atajos
                if (cual == 0) return Color.FromArgb(Prefijo != null ? 150 : 90, SonAmbar);
                return Color.FromArgb(240, SonAmbar);
            }

            // g408: lo que dice la etiqueta de la tarjeta «LAS MEJORES CANCIONES DE ARTISTA» (null = ÁLBUM, la de XP)
            internal static string RocolaOsMejoresTipo(SearchItem it)
            {
                return it != null && it.AlbumRef != null && RocolaOsMejoresAlbumes.Contains(it.AlbumRef) ? "LO MEJOR" : null;
            }

            // g402: la pastilla del prefijo, delante de lo escrito, centrada en la casilla. Devuelve donde empieza lo escrito (sin prefijo, la x que le dan).
            // Con la letra de las teclas con palabra (la de BORRAR: 20 pt a escala 1) para que se lea sin comerse la casilla.
            private float RocolaOsPintaPrefijo(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Rectangle qb, int cy, float x, Color v)
            {
                if (Prefijo == null) return x;
                float s = rep.S;
                float w = Mide(ajuste, Prefijo, fnTeclaPal);
                int pad = Px(14, s);
                int alto = Math.Max(12, Math.Min(qb.Height - Px(22, s), (int)Math.Ceiling(fnTeclaPal.AltoRenglon) + Px(10, s)));
                Rectangle r = new Rectangle((int)Math.Round(x), cy - alto / 2, (int)Math.Ceiling(w) + 2 * pad, alto);
                using (SKPath gp = ContornoRedondo(r, Math.Max(2, alto / 2)))
                {
                    pincelS.Color = Color.FromArgb(80, v).Sk(); Pixel.Rellena(g, gp, pincelS, suave, medio, copia);
                    plumaS.Color = Color.FromArgb(230, v).Sk(); plumaS.StrokeWidth = 1f; Pixel.Traza(g, gp, plumaS, suave, medio, copia);
                }
                pincelS.Color = Lighten(v, .85).Sk();
                TextoGdiPlus.Escribe(g, Prefijo, fnTeclaPal, ajuste, pincelS, r, sfCentro, copia, medio);
                return r.Right + Px(12, s);
            }

            // tras poner el prefijo con el atajo: la seleccion pasa al teclado (a la fila de las letras, la tecla que queda debajo del atajo)
            internal void RocolaOsAlTeclado()
            {
                AseguraReparto();
                float cx = (SelChip >= 0 && SelChip < atajoRects.Count) ? atajoRects[SelChip].X + atajoRects[SelChip].Width / 2f : 0f;
                EnChips = false; FocusResults = false;
                SelRow = Math.Max(0, Math.Min(1, rows.Count - 1));
                SelCol = TeclaMasCerca(SelRow, cx);
                Invalidate();
            }
            // los resultados se vuelven a armar con lo que ya esta escrito (al poner o quitar el prefijo aparece o se va la tarjeta)
            internal void RocolaOsRehaz()
            {
                Query = "\u0001";
                PosaConsulta();
            }
        }
    }
}

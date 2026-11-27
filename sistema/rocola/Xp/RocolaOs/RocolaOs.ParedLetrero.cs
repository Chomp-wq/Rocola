// LAS PAREDES DEL BUSCADOR SIN EL ABECEDARIO (Rocola OS, modo Spotify; peticion del dueno del 22-11-2026: «si nos metiamos a un artista que
// era de Spotify, se abriria una pared de discos que solo existe dentro del buscador [...] pero sigue apareciendo la barra de abecedario, eso
// va a confundir a la gente, debe salir un letrero que diga regresar al buscador y el boton rojo, y el anuncio Discos-tal artista y al lado un
// icono de Spotify»).
//
// En una pared del buscador (la letra que la barra no tiene: RocolaOsSpotifyParedMarca; la de un artista solo de Spotify, la unida de tus artistas y
// «Todos los discos de...», RocolaOs.SpotifyCatalogoParedes.cs) la barra de letras de XP no se dibuja ni se puede tocar con el raton: en su
// franja va, centrado, el boton rojo como lo dibuja el pie de XP (CtrlDot de Form1.Pared.cs, el mismo rojo de HelpBar) y «REGRESAR AL
// BUSCADOR» con la letra del pie (sin remates, negrita), mas grande a la medida de la franja, sobre una capsula oscura translucida para que se
// lea sobre el cielo. REGRESAR (el rojo) ya vuelve al buscador (g53).
// La cabecera de esas paredes dice «DISCOS · ARTISTA» (o «DISCOS · «lo escrito»») y junto al texto un icono de Spotify dibujado aqui (circulo
// verde #1DB954 con las tres ondas negras; nada de archivos de imagen), del alto de la letra de la cabecera.
//
// Ganchos (Codigo/Form1.Vistas.cs): g240 LetterBar.RenderContent (el letrero en vez de la barra), g241 LetterBar.GetTopGlow (sin el dorado
// de TOP), g242 LetterBar.Raton (sin clics), g243 y g244 HeaderView.RenderContent (el sitio del icono al medir y el icono al dibujar).
// Sin Rocola OS, o fuera de esas paredes, cada gancho deja lo de XP tal cual (Current nunca es la marca de la pared; la cabecera no es la de
// una pared).
using System;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // la cabecera de la pared del buscador que se mira (la pone RocolaOsSpotifyParedMira; el icono va solo junto a ese texto)
        internal static string RocolaOsParedCabecera;
        internal static bool RocolaOsParedConIcono = true;   // (23-11-2026) la pared de un artista que Spotify no conoce (solo tus discos) no lleva el icono de Spotify
        private static readonly Color RocolaOsParedRojo = Color.FromArgb(228, 72, 72);        // el rojo de HelpBar (Form1.cs)
        private static readonly Color RocolaOsParedTexto = Color.FromArgb(228, 234, 250);     // el texto de HelpBar
        private static readonly Color RocolaOsSpotifyVerde = Color.FromArgb(0x1D, 0xB9, 0x54);
        private static Rocola.Nativo.Letra rocolaOsParedLetra; private static int rocolaOsParedLetraH = -1;

        // ¿la barra esta en una pared del buscador? (de XP, nunca: su Current es una letra, la estrella o el video)
        private static bool RocolaOsParedSinLetras(char actual)
        {
            return actual == RocolaOsSpotifyParedMarca && RocolaOs.Activo;
        }

        // g262 (Form1.cs, LayoutControls): en una pared del buscador NO van las casillas de BUSCAR ni de AYUDA, ni su latido ni la interrogacion
        // que bota (23-11-2026, el dueno: «dentro de una pared de discos temporal, no debe aparecer la celda de buscador ni la de ayuda, ni sus
        // brillos ni nada relacionado»): BUSCAR queda escondida y con ella AYUDA (AcomodaAyuda), y la cabecera se queda con su sitio. Todo lo
        // que la tarjeta les pone encima mira su Visible. Las teclas no cambian (el rojo ya vuelve al buscador; ROJO+ABAJO, la ayuda de XP).
        private bool RocolaOsParedSinCasillas() { return RocolaOsParedSinLetras(currentLetter); }
        // g263 (Form1.Biblioteca.cs, RefreshAlbums): al entrar o salir de una pared del buscador, la fila de arriba se acomoda otra vez (una vez
        // por cambio; tambien desde el latido del catalogo por si la letra cambio sin RefreshAlbums)
        private bool rocolaOsParedSinCasillasPuesto;
        private void RocolaOsParedCasillasAlDia()
        {
            bool quiere = RocolaOsParedSinCasillas();
            if (quiere == rocolaOsParedSinCasillasPuesto) return;
            rocolaOsParedSinCasillasPuesto = quiere;
            LayoutControls(); Invalidate();
        }

        // g240: en una pared del buscador, en la franja de la barra, el letrero (y la barra no se dibuja: true)
        private static bool RocolaOsParedLetrero(char actual, SKCanvas g, AjusteLetra ajuste, bool copia, bool medio, int w, int h)
        {
            if (!RocolaOsParedSinLetras(actual)) return false;
            if (w < 8 || h < 8) return true;
            bool suave = true;
            if (rocolaOsParedLetra == null || rocolaOsParedLetraH != h)
            {
                if (rocolaOsParedLetra != null) rocolaOsParedLetra.Dispose();
                float pts = Math.Max(8f, Math.Min(18f, h * 0.30f));   // 40 px de barra (1024x768): 12 puntos
                rocolaOsParedLetra = Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, pts, true);
                rocolaOsParedLetraH = h;
            }
            const string texto = "REGRESAR AL BUSCADOR";
            int d = Math.Max(12, Math.Min(28, (int)(h * 0.52f)));
            float gap = Math.Max(7f, h * 0.22f);
            SizeF sz = TextoGdiPlus.Mide(texto, rocolaOsParedLetra, ajuste, new SizeF(0, 0), null);
            float padX = h * 0.45f, alto = Math.Min(h - 4f, Math.Max(d, sz.Height) + h * 0.28f);
            // DELANTE, EL ANUNCIO: el icono de Spotify y «DISCOS · ARTISTA» (la cabecera de la pared; en 1024x768 la de arriba se encoge para
            // caber junto al pie, y aqui se lee del tamano del letrero). Si no cabe entero en la franja, solo el letrero (la cabecera lo sigue diciendo).
            string titulo = RocolaOsParedCabecera;
            float sep = gap * 2.2f, anchoTit = 0f;
            if (!string.IsNullOrEmpty(titulo)) anchoTit = (RocolaOsParedConIcono ? d + gap : 0f) + TextoGdiPlus.Mide(titulo, rocolaOsParedLetra, ajuste, new SizeF(0, 0), null).Width + sep;
            if (anchoTit > 0f && anchoTit + d + gap + sz.Width + 2 * padX > w - 8) { titulo = null; anchoTit = 0f; }
            float total = anchoTit + d + gap + sz.Width;
            float x0 = (w - total) / 2f; if (x0 < 4f) x0 = 4f;
            int cy = h / 2;
            // la capsula: oscura y translucida, con un filo tenue (como el riel de la barra)
            RectangleF caps = new RectangleF(x0 - padX, cy - alto / 2f, total + 2 * padX, alto);
            using (SKPath pc = Redondeado(caps, alto / 2f))
            using (SKPaint fondo = new SKPaint { Color = Color.FromArgb(150, 8, 12, 32).Sk() })
            using (SKPaint filo = new SKPaint { Color = Color.FromArgb(90, 150, 175, 225).Sk(), StrokeWidth = 1.2f })
            {
                Pixel.Rellena(g, pc, fondo, suave, medio, copia);
                Pixel.Traza(g, pc, filo, suave, medio, copia);
            }
            float x = x0;
            if (titulo != null)
            {
                if (RocolaOsParedConIcono) RocolaOsIconoSpotify(g, x + d / 2f, cy, d / 2f, copia, medio);
                x = CtrlTxt(g, suave, ajuste, copia, medio, rocolaOsParedLetra, titulo, RocolaOsParedConIcono ? x + d + gap : x, cy, Color.White);
                using (SKPaint raya = new SKPaint { Color = Color.FromArgb(110, 150, 175, 225).Sk(), StrokeWidth = 1f })
                    Pixel.Linea(g, x + sep / 2f, cy - alto * 0.32f, x + sep / 2f, cy + alto * 0.32f, raya, suave, medio, copia);
                x += sep;
            }
            x = CtrlDot(g, suave, ajuste, copia, medio, x, cy, d, RocolaOsParedRojo);
            CtrlTxt(g, suave, ajuste, copia, medio, rocolaOsParedLetra, texto, x + gap, cy, RocolaOsParedTexto);
            return true;
        }

        // g243: lo que ocupa el icono (con su aire) junto a la cabecera de una pared del buscador; 0 en lo demas
        private static float RocolaOsParedIconoAncho(string seccion, int h)
        {
            if (!RocolaOs.Activo || !RocolaOsParedConIcono || seccion == null || seccion != RocolaOsParedCabecera) return 0f;
            return h * 0.62f + h * 0.30f;
        }
        // g244: el icono de Spotify despues del texto de la cabecera (x: donde acaba el texto); devuelve lo que ocupa (0 si no va)
        private static float RocolaOsParedIcono(SKCanvas g, string seccion, float x, int h, bool copia, bool medio)
        {
            float ancho = RocolaOsParedIconoAncho(seccion, h);
            if (ancho <= 0f) return 0f;
            float dia = h * 0.62f;
            RocolaOsIconoSpotify(g, x + h * 0.30f + dia / 2f, h / 2f, dia / 2f, copia, medio);
            return ancho;
        }

        // EL ICONO DE SPOTIFY: el circulo verde y las tres ondas negras (de mas ancha arriba a mas angosta abajo, combadas hacia arriba)
        // (27-11-2026) alfa: 255 entero; menos, apagado (la letra ♫ de la barra, cuando no esta disponible o aparece con un fundido)
        internal static void RocolaOsIconoSpotify(SKCanvas g, float cx, float cy, float r, bool copia, bool medio, int alfa = 255)
        {
            if (r < 2f || alfa <= 0) return;
            bool suave = true;
            using (SKPaint verde = new SKPaint { Color = Color.FromArgb(alfa, RocolaOsSpotifyVerde).Sk() })
                Pixel.RellenaOvalo(g, new RectangleF(cx - r, cy - r, 2 * r, 2 * r), verde, suave, medio, copia);
            float[] dy = { -0.30f, 0.02f, 0.31f }, ancho = { 1.18f, 1.00f, 0.80f }, grueso = { 0.19f, 0.16f, 0.13f };
            // (27-11-2026) CHICO (la casilla de la letra ♫ de la barra, ~22 px en 1024x768, y la cabecera de las paredes del buscador): las ondas mas
            // separadas y mas gruesas, para que cada una se vea como una onda y no como una mancha
            if (r < 14f) { dy = new[] { -0.36f, 0.0f, 0.36f }; ancho = new[] { 1.30f, 1.10f, 0.88f }; grueso = new[] { 0.215f, 0.19f, 0.16f }; }
            for (int i = 0; i < 3; i++)
            {
                float y = cy + dy[i] * r, mitad = ancho[i] * r / 2f, comba = 0.20f * r;
                using (SKPath p = new SKPath())
                using (SKPaint negro = new SKPaint { Color = Color.FromArgb(alfa, 0, 0, 0).Sk(), StrokeWidth = grueso[i] * r, StrokeCap = SKStrokeCap.Round })
                {
                    p.MoveTo(cx - mitad, y + comba * 0.55f);
                    p.QuadTo(cx, y - comba, cx + mitad, y + comba * 0.75f);
                    Pixel.Traza(g, p, negro, suave, medio, copia);
                }
            }
        }

        private static SKPath Redondeado(RectangleF r, float radio)
        {
            SKPath p = new SKPath();
            p.AddRoundRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom), radio, radio);
            return p;
        }
    }
}

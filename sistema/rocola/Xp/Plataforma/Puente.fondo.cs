// EL PUENTE DEL GRUPO «FONDO», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Form1.Fondo.cs (el cielo del
// dibujo por procesador -Starfield-, la luna, el grano, el contorno redondeado y el color de las portadas) dibuja con SkiaSharp directo y las
// funciones nativas (Xp/Nativo). Aqui van las firmas de XP de lo que todavia piden archivos que dibujan con la capa que imita a GDI+
// (Dibujo2D.cs). La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto queda: cada parte se
// borra en cuanto se traduzcan los que la llaman, y todo al final, con Dibujo2D.cs y Puente.cs (PORT_fondo.md dice cual es cual). La firma
// de XP de RoundedPath (un GraphicsPath) se borro el 12-11-2026 (limpieza-puentes): ya nadie la pide. Ninguna le
// cambia el estado al Graphics de quien llama (en XP, las tres del cielo lo dejan como estaba: BlitSkyAndStars devuelve la copia).
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // ---- ColoresDePortada: la firma de XP (un Bitmap), para Form1.Portadas.cs y ColorDePortadaDe (los dos la reciben de LoadCover y de
        // la cache de portadas, aun sin traducir). Los MISMOS puntos (PuenteDibujo.Mapa). Una portada ya soltada lanza aqui al leer su ancho,
        // como en XP (que lo lee antes de mirar ningun punto); la traducida, con un SKBitmap soltado, cerraria la rocola. Se quita cuando
        // Form1.Portadas.cs y Form1.Biblioteca.cs (LoadCover) den un SKBitmap.
        private static Color ColoresDePortada(Bitmap b, out Color uno, out Color dos)
        {
            if (b != null) _ = b.Width;
            return ColoresDePortada(PuenteDibujo.Mapa(b), out uno, out dos);
        }

        // ---- El cielo del dibujo por procesador: las firmas de XP de Draw (Form1.Pared.cs, la atraccion), (DrawCached se quito con form1-sin-form: el fondo de la ventana, Form1.cs, y el panel
        // de la lista del disco, BgPanel, la llaman directo) y DrawReactive (Form1.Pared.cs y Form1.Buscador.cs). El lienzo del Graphics (con su matriz y su recorte)
        // y su estado: el suavizado, el filtro, el medio pixel y la copia. Se quitan cuando esos archivos (y los OnPaint) se traduzcan.
        private partial class Starfield
        {
            public void Draw(Graphics g, int w, int h, Color skyTop, Color skyBottom, Color tint, double phase, bool shooting, float pulse)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                Draw(PuenteDibujo.Lienzo(g), e.Suave, e.Filtro, e.Medio, e.Copia, w, h, skyTop, skyBottom, tint, phase, shooting, pulse);
            }
            public void DrawReactive(Graphics g, int w, int h, Color skyTop, Color skyBottom, float pLo, float pMid, float pHi)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                DrawReactive(PuenteDibujo.Lienzo(g), e.Suave, e.Filtro, e.Medio, e.Copia, w, h, skyTop, skyBottom, pLo, pMid, pHi);
            }
        }
    }
}

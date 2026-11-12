// EL PUENTE DEL GRUPO «ASISTENTE», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 9-11-2026). Los archivos del grupo
// (PulidoGpu.cs: el degradado tramado de los paneles; AyudaCasillaGpu.cs: la casilla de AYUDA; AsistenteRocola.cs y AsistenteGuiaRocola.cs: los
// globos de la estrella y los botones dibujados; AsistenteEstrellaGpu.cs: la subida del globo) pasan a dibujar con SkiaSharp directo y las
// funciones nativas (Xp/Nativo); cada uno, al traducirse, pone aqui su parte: donde lo llama o lo recibe codigo que todavia dibuja con la capa
// que imita a GDI+ (Dibujo2D.cs), pasa por ella. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada
// de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs
// (PORT_asistente.md dice cual es cual).
#if ROCOLA_GPU
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // (PulidoGpu.cs: la firma de XP de DegradadoV, con el Brush de GDI+, se quito con el grupo directas-tanda2 (12-11-2026): EstadoReposoGpu.cs y
    // AcabadoGpu.cs llaman directo a la traducida, DegradadoV(r, c0, c1, false), que da el shader con el medio pixel de fabrica de su lienzo)

    // ================================================================ AyudaCasillaGpu.cs (y los globos de AsistenteRocola.cs y AsistenteGuiaRocola.cs)
    public partial class Form1
    {
        private partial class AyudaView
        {
            // ---- LA FIRMA DE XP DE RenderContent (con un Graphics), PARA OnPaint (AyudaCasillaGpu.cs, de WinForms) Y DrawCtrlOverlay
            // (Form1.Video.cs: la imagen de la interfaz con tarjeta): el lienzo del Graphics (con su matriz y su recorte) y su copia y su medio
            // pixel. Despues, lo que XP le deja puesto al Graphics: RenderContent pone al empezar el suavizado y el modo de letra (AntiAlias los
            // dos), si la casilla tiene medida para pintarse (las mismas cuentas de su primera linea); si no, no lo toca.
            public void RenderContent(Graphics g, bool forGpu)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Copia, e.Medio, forGpu);
                if (!(Width < 30 || Height < 12)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAlias; }
            }
        }

        // (CtrlDotAsistente, CtrlTxtAsistente y CtrlTriAsistente, con su GraphicsAsistente, se quitaron al traducirse Form1.Pared.cs, grupo
        // pared: los globos, la guia y la casilla llaman directo a CtrlDot, CtrlTxt y CtrlTri con su lienzo y su estado)
    }
}
#endif

// EL PUENTE DEL GRUPO «BUSCADOR-PORTADAS», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 9-11-2026). BuscadorPortadasGpu.cs
// (el buscador con portadas: el teclado compacto, los atajos, la casilla de lo escrito y las tarjetas con su portada) ya dibuja con SkiaSharp
// directo y las funciones nativas (Xp/Nativo); donde lo llama o lo recibe codigo que todavia dibuja con la capa que imita a GDI+
// (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto queda:
// cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs
// (PORT_buscador-portadas.md dice cual es cual).
//   - Las firmas de XP de lo que llaman los archivos sin traducir: RenderNuevo y DibujaPiezaVivaNueva (Form1.Buscador.cs: RenderContent y
//     DibujaPiezaViva); la de PintaCartasConCambios (Form1.Video.cs: EnsureUiPieza) se quito al traducirse Form1.Video.cs. Le pasan a la traducida el lienzo del Graphics y su estado
//     (PuenteDibujo.Lienzo y Estado: suavizado, modo de letra, copia, filtro, medio pixel y la matriz del HDC) y le dejan al Graphics lo que
//     XP le dejaba cambiado: RenderNuevo y DibujaPiezaVivaNueva le ponian SmoothingMode = AntiAlias al empezar (y no lo devolvian), asi
//     que el puente se lo pone antes de llamar; PintaCartasConCambios no le cambia nada (su Save/Restore es del lienzo).
//   - Lo que la traducida le pide a lo que aun no lo esta (pinta con un Graphics y una Font): CtrlPad, CtrlTxt y CtrlDot (Form1.Pared.cs), y
//     FlechaArriba y DrawToast (Form1.Buscador.cs). Un Graphics sobre el MISMO lienzo (con su matriz y su recorte) con el estado del que
//     llama, y la Font de la Letra (los mismos datos: familia, tamano, estilo y unidad). Ninguna de las cinco le cambia el estado al Graphics
//     (solo pintan con su pincel y su pluma), asi que no hay nada que leer despues. Del estado, lo que no usan va con su valor de fabrica: el
//     filtro (no pintan imagenes) y, salvo DrawToast, la matriz del HDC (solo la mira el texto de GDI, TextRenderer, y solo DrawToast lo usa;
//     a DrawToast se le pasa la del que llama).
#if ROCOLA_GPU
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        private partial class SearchOverlay
        {
            // ---- LAS FIRMAS DE XP
            // RenderContent (Form1.Buscador.cs) con forGpu: la imagen grande del buscador con portadas (la pinta EnsureUiOverlay de
            // Form1.Video.cs y la precalienta PrecalientaBuscadorGpu de PrecargaPaginaGpu.cs)
            private void RenderNuevo(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;   // lo que XP le pone al empezar (y le deja puesto)
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderNuevo(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Filtro, e.Medio, e.Hdc);
            }
            // DibujaPiezaViva (Form1.Buscador.cs): las imagenes chicas (la tecla o el atajo elegido, la casilla, la tarjeta elegida y las
            // tarjetas), que pinta EnsureUiPieza de Form1.Video.cs
            private void DibujaPiezaVivaNueva(Graphics g, int cual, int dx, int dy)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;   // lo que XP le pone al empezar (y le deja puesto)
                EstadoLienzo e = PuenteDibujo.Estado(g);
                DibujaPiezaVivaNueva(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Filtro, e.Medio, cual, dx, dy);
            }
            // (La firma de XP de PintaCartasConCambios, para EnsureUiPieza de Form1.Video.cs, se quito con el grupo video-form1: ya la llama directo.)

            // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA
            // un Graphics sobre el mismo lienzo con el estado del que llama (el filtro, el de fabrica de un Graphics; la matriz del HDC, la dada)
            private static Graphics GraphicsPortadas(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, SKMatrix hdc)
            { return PuenteDibujo.Graphics(g, new EstadoLienzo(suave, ajuste, copia, Filtro.Bilineal, medio, hdc)); }
            // CtrlPad, CtrlDot y CtrlTxt (Form1.Pared.cs, la barra de ayudas: la cruceta, los botones y sus palabras) y FlechaArriba
            // (Form1.Buscador.cs): pintan con el pincel y la pluma de la capa; CtrlTxt mide y escribe con GDI+ (el modo de letra del lienzo)
            private static float CtrlPadPortadas(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz)
            {
                using (Graphics gg = GraphicsPortadas(g, suave, ajuste, copia, medio, SKMatrix.Identity))
                    return CtrlPad(gg, x, cy, sz);
            }
            private static float CtrlDotPortadas(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)
            {
                using (Graphics gg = GraphicsPortadas(g, suave, ajuste, copia, medio, SKMatrix.Identity))
                    return CtrlDot(gg, x, cy, d, c);
            }
            private static float CtrlTxtPortadas(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Rocola.Nativo.Letra f, string s, float x, int cy, Color c)
            {
                using (Graphics gg = GraphicsPortadas(g, suave, ajuste, copia, medio, SKMatrix.Identity))
                using (Font ff = PuenteDibujo.Font(f))
                    return CtrlTxt(gg, ff, s, x, cy, c);
            }
            private static float FlechaArribaPortadas(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)
            {
                using (Graphics gg = GraphicsPortadas(g, suave, ajuste, copia, medio, SKMatrix.Identity))
                    return FlechaArriba(gg, x, cy, d, c);
            }
            // DrawToast (Form1.Buscador.cs): el aviso del buscador; su texto es de GDI (TextRenderer), que pinta con la matriz del HDC del
            // que llama y no con la del lienzo
            private void DrawToastPortadas(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, SKMatrix hdc, int W, int H)
            {
                using (Graphics gg = GraphicsPortadas(g, suave, ajuste, copia, medio, hdc))
                    DrawToast(gg, W, H);
            }
        }
    }
}
#endif

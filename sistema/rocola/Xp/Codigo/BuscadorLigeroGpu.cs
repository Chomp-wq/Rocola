// EL BUSCADOR, MAS LIGERO PARA LA TARJETA (rendimiento, 24-09-2026). Solo con tarjeta grafica, automatico y sin
// interruptor: lo que se ve es exactamente lo mismo. Las tres cosas son de la imagen grande del buscador, la de pantalla
// completa: a 1920x1080 son 7 MB, y en la tarjeta una textura de 8 MB (la potencia de dos que la contiene).
//
//  1) SUBIR SOLO LO QUE CAMBIO. Al posar una consulta cambian las tarjetas de la derecha; al entrar o salir un aviso,
//     el aviso; al pedir una cancion, sus marcas. La imagen se sigue haciendo entera, como siempre -no cambia como se
//     dibuja nada-, pero a la tarjeta solo se le manda el trozo que cambio. Que trozo es lo sabe el formulario
//     (Form1.Video.cs, ZonaBuscador) y lo dice por GetUiCambio. Si no lo sabe, si la textura no tiene justo la version
//     anterior o si la subida por trozos falla, se sube entera como siempre.
//  2) CERRAR SIN COPIAR. Al cerrar, la ventana del buscador se va encogiendo con la imagen que tenia. Antes esa imagen
//     se copiaba (7 MB) y se volvia a subir a otra textura (8 MB), justo en el momento de cerrar. Ahora la textura que
//     ya la tiene pasa a ser la de la ventana: la misma imagen, sin copiarla ni subirla.
//  3) ABRIR SIN CREAR. Al abrir, la imagen grande necesita una textura grande, y se le pedia al driver una nueva (crear
//     texturas es de lo que a veces tarda, ver PrecalientaAnimaciones). La de la ultima ventana que se cerro mide lo
//     mismo: si ya no se esta dibujando, se reutiliza.
#if ROCOLA_GPU
using System;
using System.Drawing; using SkiaSharp;   // EN LINUX: este archivo no dibuja: solo le pasa a la tarjeta la imagen de la interfaz (un SKBitmap, Bgra8888 premultiplicado como el Format32bppPArgb de XP; la hace Form1.Video.cs)

namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        // Que trozo de la imagen de la UI cambio desde la version que se le pasa (la que tiene la textura). Vacio: no se
        // sabe, se sube entera. Lo pone el formulario.
        public Func<int, Rectangle> GetUiCambio;

        // Sube la imagen de la UI a su textura: solo el trozo que cambio si se sabe; si no, entera, reutilizando la textura
        // de la ultima ventana si mide lo que hace falta.
        private void SubeUi(SKBitmap ub, GpuWin gw)   // EN LINUX: Bitmap -> SKBitmap: la imagen de la interfaz (uiOverlay, Form1.Video.cs), el mismo objeto que da GetUi; va tal cual a UpdateOverlayRegion y SubeReponiendo, como en XP
        {
            if (ub == null) return;
            if (uiTex != IntPtr.Zero && GetUiCambio != null && ub.Width == uiBmpW && ub.Height == uiBmpH)
            {
                Rectangle zona = GetUiCambio(uiVer);
                if (!zona.IsEmpty && new Rectangle(0, 0, ub.Width, ub.Height).Contains(zona) && stars.UpdateOverlayRegion(uiTex, ub, zona))
                    return;   // 1) solo lo que cambio
            }
            if ((uiTex == IntPtr.Zero || ub.Width != uiBmpW || ub.Height != uiBmpH) && winTex != IntPtr.Zero
                && winBw == ub.Width && winBh == ub.Height && !VentanaEnUso(gw))
            {
                // 3) la textura de la ultima ventana mide lo mismo: se reutiliza (se le sube la imagen encima)
                if (uiTex != IntPtr.Zero) Repone(uiTex, uiBmpW, uiBmpH);   // la de antes, al repuesto (RespuestaGpu.cs)
                uiTex = winTex; uiBmpW = winBw; uiBmpH = winBh; uiU = winU; uiV = winV;
                winTex = IntPtr.Zero; winLastBmp = null; winBw = winBh = 0;
            }
            uiTex = SubeReponiendo(uiTex, ref uiBmpW, ref uiBmpH, ub, out uiU, out uiV);   // otra medida: del repuesto (RespuestaGpu.cs)
        }
        private static bool VentanaEnUso(GpuWin gw) { return gw.Has && (gw.Bmp != null || gw.Robada); }

        // 2) Al cerrar el buscador. Si la textura de la UI tiene justo la version 'version' de la imagen, pasa a ser la de
        // la ventana que se encoge, y la UI se queda sin textura (el cuadro siguiente sube la de navegacion, como ya hacia:
        // mide otra cosa). Devuelve false si no la tiene; entonces el formulario hace la copia de siempre.
        internal bool RobaUiParaVentana(int version)
        {
            if (uiTex == IntPtr.Zero || version != uiVer || uiBmpW < 1 || uiBmpH < 1) return false;   // (sin interfaz: uiTex es cero)
            if (winTex != IntPtr.Zero) Tarjeta.Suelta(winTex);
            winTex = uiTex; winBw = uiBmpW; winBh = uiBmpH; winU = uiU; winV = uiV; winLastBmp = null;
            uiTex = IntPtr.Zero; uiBmpW = uiBmpH = 0; uiTeselas = null;
            return true;
        }
    }
}
#endif

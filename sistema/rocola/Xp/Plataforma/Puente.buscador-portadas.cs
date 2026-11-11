// EL PUENTE DEL GRUPO «BUSCADOR-PORTADAS», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 9-11-2026). BuscadorPortadasGpu.cs
// (el buscador con portadas: el teclado compacto, los atajos, la casilla de lo escrito y las tarjetas con su portada) ya dibuja con SkiaSharp
// directo y las funciones nativas (Xp/Nativo); donde lo llama o lo recibe codigo que todavia dibuja con la capa que imita a GDI+
// (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto queda:
// cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs
// (PORT_buscador-portadas.md dice cual es cual).
//   - La firma de XP de lo que llama un archivo sin traducir: PintaCartasConCambios (Form1.Video.cs: EnsureUiPieza). Le pasa a la traducida
//     el lienzo del Graphics y su estado (PuenteDibujo.Lienzo y Estado: suavizado, modo de letra, copia, filtro, medio pixel y la matriz del
//     HDC); no le cambia nada al Graphics (su Save/Restore es del lienzo). (Las de RenderNuevo y DibujaPiezaVivaNueva se quitaron al
//     traducirse Form1.Buscador.cs, grupo buscador: RenderContent y DibujaPiezaViva las llaman directo.)
//   - Lo que la traducida le pide a lo que aun no lo esta (pinta con un Graphics y una Font): CtrlPad, CtrlTxt y CtrlDot (Form1.Pared.cs). Un
//     Graphics sobre el MISMO lienzo (con su matriz y su recorte) con el estado del que llama, y la Font de la Letra (los mismos datos:
//     familia, tamano, estilo y unidad). Ninguna de las tres le cambia el estado al Graphics (solo pintan con su pincel y su pluma), asi que
//     no hay nada que leer despues. Del estado, lo que no usan va con su valor de fabrica: el filtro (no pintan imagenes) y la matriz del HDC
//     (solo la mira el texto de GDI, TextRenderer). (FlechaArriba y DrawToast, de Form1.Buscador.cs, ya se llaman directo: grupo buscador.)
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
            // ---- LA FIRMA DE XP
            // EnsureUiPieza (Form1.Video.cs): la pieza de las tarjetas, entera o solo lo que cambio
            internal Rectangle[] PintaCartasConCambios(Graphics g, int dx, int dy, bool entera)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                return PintaCartasConCambios(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Filtro, e.Medio, dx, dy, entera);
            }

            // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA
            // (CtrlPadPortadas, CtrlDotPortadas y CtrlTxtPortadas se quitaron al traducirse Form1.Pared.cs, grupo pared; FlechaArribaPortadas y
            // DrawToastPortadas, al traducirse Form1.Buscador.cs, grupo buscador: BuscadorPortadasGpu.cs llama directo con su lienzo y su estado)
        }
    }
}
#endif

// EL PUENTE DEL GRUPO «VISTAS-A», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). La primera parte de
// Form1.Vistas.cs (hasta la linea 997: SongPagerBar, la barra de paginas de la lista, y LetterBar, la barra de letras con su tira guardada y su
// lupa) ya dibuja con SkiaSharp directo y las funciones nativas (Xp/Nativo); donde la llama o donde llama ella a codigo que todavia dibuja con
// la capa que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no
// cambian. Nada de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y
// Puente.cs (PORT_vistas-a.md dice cual es cual).
//   - Las firmas de XP de RenderContent (con un Graphics), para OnPaint de cada barra (de WinForms: el dibujo por procesador) y DrawCtrlOverlay
//     de Form1.Video.cs (la imagen de la interfaz con tarjeta). Son delgadas: pasan a la traducida el lienzo del Graphics (con su matriz y su
//     recorte) y su estado (PuenteDibujo.Estado), y le dejan al Graphics lo que XP le deja puesto sin devolverlo: la barra de paginas, el
//     suavizado y el modo de letra (AntiAlias y ClearTypeGridFit); la de letras, el suavizado (AntiAlias); cada una solo si pasa de su primera
//     salida (las mismas cuentas de XP). Se le ponen ANTES de llamar a la traducida: asi el Graphics queda igual que en XP tambien si el
//     dibujo lanza una excepcion a medias.
//   - Lo que la traducida le pide a lo que no lo esta y pinta con un Graphics (CtrlDirLeft, CtrlDirRight y CtrlTxt de Form1.Pared.cs): un
//     Graphics sobre el MISMO lienzo con el estado del que llama (GraphicsVistasA).
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // ================================================================ SongPagerBar (la barra de paginas de la lista de canciones)
        private partial class SongPagerBar
        {
            // ---- LA FIRMA DE XP DE RenderContent, PARA OnPaint (Form1.Vistas.cs, de WinForms) Y DrawCtrlOverlay (Form1.Video.cs)
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                // lo que XP le deja al Graphics: tras pintar el fondo, si hay mas de una pagina y la barra tiene medida (su linea 129)
                if (!(Pages <= 1 || Width < 60 || Height < 10)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; }
                RenderContent(PuenteDibujo.Lienzo(g), e.Suave, e.Copia, e.Medio);
            }
        }

        // ================================================================ LetterBar (la barra de letras)
        private partial class LetterBar
        {
            // ---- LAS FIRMAS DE XP DE RenderContent, PARA OnPaint (Form1.Vistas.cs, de WinForms) Y DrawCtrlOverlay (Form1.Video.cs)
            public void RenderContent(Graphics g) { RenderContent(g, false); }
            public void RenderContent(Graphics g, bool forGpu)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                // lo que XP le deja al Graphics: el suavizado, si la barra tiene medida y letras (su linea 631)
                if (!(Width < 4 || Height < 4 || Letters == null || Letters.Length == 0)) g.SmoothingMode = SmoothingMode.AntiAlias;
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Filtro, e.Medio, forGpu);
            }
        }

        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA: CtrlDirLeft, CtrlDirRight y CtrlTxt (Form1.Pared.cs) pintan con un Graphics:
        // uno sobre el MISMO lienzo con el estado del que llama (GraphicsVistasA, abajo), y CtrlTxt con la Font de esa Letra (los mismos datos:
        // familia, tamano, unidad y estilo; se suelta al acabar, porque CtrlTxt no se la queda). Ninguna de las tres le cambia el estado al
        // Graphics. Cuando Form1.Pared.cs se traduzca, la llamada pierde el «VistasA» (con el lienzo y el estado como aqui) y estas tres se borran.
        private static float CtrlDirLeftVistasA(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz, Color c)
        { return CtrlDirLeft(PuenteDibujo.GraphicsVistasA(g, suave, ajuste, copia, medio), x, cy, sz, c); }
        private static float CtrlDirRightVistasA(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz, Color c)
        { return CtrlDirRight(PuenteDibujo.GraphicsVistasA(g, suave, ajuste, copia, medio), x, cy, sz, c); }
        private static float CtrlTxtVistasA(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Rocola.Nativo.Letra f, string s, float x, int cy, Color c)
        {
            using (Font ff = PuenteDibujo.Font(f))
                return CtrlTxt(PuenteDibujo.GraphicsVistasA(g, suave, ajuste, copia, medio), ff, s, x, cy, c);
        }
    }

    internal static partial class PuenteDibujo
    {
        // un Graphics sobre el lienzo de un archivo traducido del grupo, con su estado, para llamar a uno que no lo esta: el filtro, el de fabrica de
        // un Graphics (Bilinear: lo que se llama con el no pinta imagenes); la matriz del HDC, la identidad (lo que se llama con el no usa
        // TextRenderer, el unico que la mira). No suelta el lienzo.
        internal static Graphics GraphicsVistasA(SKCanvas c, bool suave, AjusteLetra ajuste, bool copia, bool medio)
        { return Graphics(c, new EstadoLienzo(suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity)); }
    }
}

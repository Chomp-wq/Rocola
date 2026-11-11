// EL PUENTE DEL GRUPO «VIDEO-FORM1», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Form1.Video.cs (la imagen de la
// interfaz que se sube a la tarjeta: EnsureUiOverlay, EnsureUiBotOverlay, EnsureUiPieza, DrawCtrlOverlay, DrawLabelOverlay; y el precalentado,
// EnSeco) y Form1.cs (la barra de ayuda, HelpBar) ya dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo); donde los llama o donde
// llaman ellos a codigo que todavia dibuja con la capa que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no
// esta traducido, y los que llaman no cambian. Nada de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al
// final, con Dibujo2D.cs y Puente.cs (PORT_video-form1.md dice cual es cual).
//   - Las firmas de XP de lo que llaman los archivos sin traducir con un Graphics: HelpBar.RenderContent(Graphics, bool) (su OnPaint de WinForms)
//     y EnSeco(Control, Action<Graphics>) (PrecargaPaginaGpu.cs y Form1.Portadas.cs). Delgadas: el lienzo del Graphics y su estado a la traducida,
//     y despues le dejan al Graphics lo que XP le dejaba cambiado.
//   - Lo que la traducida le pide a lo que no lo esta y pinta con un Graphics: las vistas que pinta DrawCtrlOverlay (Form1.Vistas.cs,
//     DiscoVivoGpu.cs, Form1.Buscador.cs), RenderContent y DibujaPiezaViva del buscador (Form1.Buscador.cs) y las formas de la barra de ayuda
//     (GlowAround, CtrlPad, CtrlTxt y CtrlDot de Form1.Pared.cs): un Graphics sobre el MISMO lienzo (con su matriz y su recorte) con el estado
//     del que llama (GraphicsVideoForm1).
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
        // ---- LA FIRMA DE XP DE EnSeco, PARA EL PRECALENTADO QUE AUN DIBUJA CON UN Graphics (PrecargaPaginaGpu.cs y Form1.Portadas.cs)
        // La traducida hace el mapa de usar y tirar, su lienzo y el estado (AntiAlias y AntiAlias; lo demas, lo de fabrica de un Graphics) y aqui
        // se le da a lo que llama un Graphics sobre ese lienzo con ese estado y la matriz del HDC de un mapa de bits (la identidad), como el
        // Graphics.FromImage de XP. Lo que lo llamado le cambie al estado se queda en ese Graphics, que se tira con el mapa (como en XP).
        private bool EnSeco(Control c, Action<Graphics> dibuja)
        {
            return EnSeco(c, delegate (SKCanvas g, bool suave, AjusteLetra ajuste)
            { dibuja(PuenteDibujo.GraphicsVideoForm1(g, suave, ajuste, false, Filtro.Bilineal, false)); });
        }

        // ---- LA FIRMA DE XP DE HelpBar.RenderContent (con un Graphics), PARA SU OnPaint (WinForms, el dibujo por procesador, aun sin traducir)
        // El lienzo del Graphics (con su matriz y su recorte) y lo que pesa de su estado (el modo de letra, la copia y el medio pixel; el filtro y
        // la matriz del HDC no los mira nada de lo que pinta). Despues, lo que XP le deja puesto al Graphics: RenderContent pone al empezar el
        // suavizado en AntiAlias y no lo devuelve.
        private partial class HelpBar
        {
            public void RenderContent(Graphics g, bool forGpu)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio, forGpu);
                g.SmoothingMode = SmoothingMode.AntiAlias;
            }
        }

        // ---- LO QUE LA BARRA DE AYUDA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA: GlowAround, CtrlPad, CtrlTxt y CtrlDot (Form1.Pared.cs) pintan
        // con un Graphics: uno sobre el MISMO lienzo con el estado del que llama (GraphicsVideoForm1; el filtro, el de fabrica: no pintan imagenes;
        // la matriz del HDC, la identidad: no usan TextRenderer, el unico que la mira), y CtrlTxt con la Font de esa Letra (los mismos datos:
        // familia, tamano, unidad y estilo; se suelta al acabar, porque CtrlTxt no se la queda). Ninguna de las cuatro le cambia el estado al
        // Graphics (solo pintan con su pincel y su pluma, y miden), asi que no hay nada que leer despues. Cuando Form1.Pared.cs se traduzca, la
        // llamada pierde el «VideoForm1» (con el lienzo y el estado como aqui) y estas cuatro se borran.
        private static void GlowAroundVideoForm1(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float leftX, float cy, float symW, float level, Color c)
        { GlowAround(PuenteDibujo.GraphicsVideoForm1(g, suave, ajuste, copia, Filtro.Bilineal, medio), leftX, cy, symW, level, c); }
        private static float CtrlPadVideoForm1(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz)
        { return CtrlPad(PuenteDibujo.GraphicsVideoForm1(g, suave, ajuste, copia, Filtro.Bilineal, medio), x, cy, sz); }
        private static float CtrlDotVideoForm1(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)
        { return CtrlDot(PuenteDibujo.GraphicsVideoForm1(g, suave, ajuste, copia, Filtro.Bilineal, medio), x, cy, d, c); }
        private static float CtrlTxtVideoForm1(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Rocola.Nativo.Letra f, string s, float x, int cy, Color c)
        {
            using (Font ff = PuenteDibujo.Font(f))
                return CtrlTxt(PuenteDibujo.GraphicsVideoForm1(g, suave, ajuste, copia, Filtro.Bilineal, medio), ff, s, x, cy, c);
        }
    }

    internal static partial class PuenteDibujo
    {
        // Un Graphics sobre el lienzo de un archivo traducido del grupo, con su estado, para llamar a uno que no lo esta: la matriz del HDC, la de un
        // mapa de bits (la identidad): todo lo que pasa por aqui pinta en un mapa (uiOverlay, uiBotOverlay, las piezas, el mapa de EnSeco, la
        // franja de lejos), y el texto de GDI (TextRenderer) de las vistas pinta con ella y no con la traslacion del lienzo, como en XP. No suelta el
        // lienzo. Lo que lo llamado le cambie al estado se queda en este Graphics (quien lo llama, en XP, lo deshacia con su Restore o lo tiraba).
        internal static Graphics GraphicsVideoForm1(SKCanvas c, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio)
        { return Graphics(c, new EstadoLienzo(suave, ajuste, copia, filtro, medio, SKMatrix.Identity)); }
    }
}
#endif

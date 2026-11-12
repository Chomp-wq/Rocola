// EL PUENTE DEL GRUPO «VIDEO-FORM1», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Form1.Video.cs (la imagen de la
// interfaz que se sube a la tarjeta: EnsureUiOverlay, EnsureUiBotOverlay, EnsureUiPieza, DrawCtrlOverlay, DrawLabelOverlay; y el precalentado,
// EnSeco) y Form1.cs (la barra de ayuda, HelpBar) ya dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo); donde los llama o donde
// llaman ellos a codigo que todavia dibuja con la capa que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no
// esta traducido, y los que llaman no cambian. Nada de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al
// final, con Dibujo2D.cs y Puente.cs (PORT_video-form1.md y PORT_directas-buscador-video.md dicen cual es cual).
//   - La firma de XP de lo que llama un archivo sin traducir con un Graphics: EnSeco(vista, Action<Graphics>) (el precalentado de la atraccion,
//     Form1.Portadas.cs, que pinta con el OnPaint de WinForms). Delgada: el lienzo del Graphics y su estado a la traducida. (La de
//     HelpBar.RenderContent(Graphics, bool), para su OnPaint, se borro con la tanda 6, grupo armado-vistas: la barra ya no hereda de Control y
//     su Pinta llama directo a la traducida.)
//   - Desde la tanda 4 (grupo directas-buscador-video, 12-11-2026) Form1.Video.cs y Form1.cs llaman DIRECTO a lo ya traducido: las vistas de
//     DrawCtrlOverlay, RenderContent y DibujaPiezaViva del buscador, las formas de la barra de ayuda (Form1.Pared.cs) y el buscador y la lista
//     del precalentado; aqui solo queda el Graphics sobre un lienzo traducido para el EnSeco de la atraccion (GraphicsVideoForm1).
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
        // ---- LA FIRMA DE XP DE EnSeco, PARA EL PRECALENTADO QUE AUN DIBUJA CON UN Graphics (la atraccion de Form1.Portadas.cs, por su OnPaint)
        // La traducida hace el mapa de usar y tirar, su lienzo y el estado (AntiAlias y AntiAlias; lo demas, lo de fabrica de un Graphics) y aqui
        // se le da a lo que llama un Graphics sobre ese lienzo con ese estado y la matriz del HDC de un mapa de bits (la identidad), como el
        // Graphics.FromImage de XP. Lo que lo llamado le cambie al estado se queda en ese Graphics, que se tira con el mapa (como en XP).
        private bool EnSeco(IVista c, Action<Graphics> dibuja)   // (armado-vistas: la vista como IVista, como la traducida; la atraccion lo es)
        {
            return EnSeco(c, delegate (SKCanvas g, bool suave, AjusteLetra ajuste)
            { dibuja(PuenteDibujo.GraphicsVideoForm1(g, suave, ajuste, false, Filtro.Bilineal, false)); });
        }
    }

    internal static partial class PuenteDibujo
    {
        // Un Graphics sobre el lienzo de un archivo traducido del grupo, con su estado, para llamar a uno que no lo esta (ya solo el EnSeco de la
        // atraccion): la matriz del HDC, la de un mapa de bits (la identidad): pinta en el mapa de EnSeco, y el texto de GDI (TextRenderer) pinta con
        // ella y no con la traslacion del lienzo, como en XP. No suelta el lienzo. Lo que lo llamado le cambie al estado se queda en este Graphics (quien lo llama, en XP, lo deshacia con su Restore o lo tiraba).
        internal static Graphics GraphicsVideoForm1(SKCanvas c, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio)
        { return Graphics(c, new EstadoLienzo(suave, ajuste, copia, filtro, medio, SKMatrix.Identity)); }
    }
}
#endif

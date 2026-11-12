// EL PUENTE DEL GRUPO «BUSCADOR», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026; lo que queda tras la tanda 4,
// grupo directas-buscador-video, 12-11-2026). Form1.Buscador.cs (la casilla de BUSCAR, BuscaView, y el buscador de siempre, SearchOverlay: el
// teclado, los resultados, el cubo de la vista previa, el aviso) ya dibuja con SkiaSharp directo y las funciones nativas (Xp/Nativo), y llama
// directo a lo ya traducido de otros archivos (las ayudas de los botones y el cubo de Form1.Pared.cs, el cielo y ContornoRedondo de
// Form1.Fondo.cs); a ella la llaman directo DrawCtrlOverlay, EnsureUiOverlay y EnsureUiPieza (Form1.Video.cs) y los precalentados
// (Form1.Portadas.cs y PrecargaPaginaGpu.cs). Aqui solo queda lo que llama el OnPaint de WinForms (la capa de ventanas, aun sin traducir) con un
// Graphics: las firmas de XP de BuscaView.RenderContent(Graphics) y SearchOverlay.RenderContent(Graphics, Rectangle, bool). Le pasan a la
// traducida el lienzo del Graphics y su estado (PuenteDibujo.Lienzo y Estado: suavizado, modo de letra, copia, filtro, medio pixel y la matriz
// del HDC) y despues le dejan al Graphics lo que XP le dejaba cambiado: el suavizado (SmoothingMode = AntiAlias, que las dos ponen y no
// devuelven, cuando llegan a esa linea) y, en SearchOverlay.RenderContent, el filtro (InterpolationMode = Bilinear de DrawPreviewCube, el cubo
// del dibujo por procesador). Se borran cuando se traduzcan los OnPaint (oleada 6 de PLAN_TANDA4_A_N7.md), y todo al final, con Dibujo2D.cs y
// Puente.cs (PORT_buscador.md y PORT_directas-buscador-video.md dicen cual es cual).
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        private partial class BuscaView
        {
            // ---- LA FIRMA DE XP (Form1.Buscador.cs, 233): OnPaint (con ClearTypeGridFit), que en XP llamaba a RenderContent(g, false). La de
            // dos parametros (para DrawCtrlOverlay de Form1.Video.cs, forGpu) ya no hace falta: DrawCtrlOverlay llama directo a la traducida.
            public void RenderContent(Graphics g)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio, false);
                if (Width >= 30 && Height >= 12) g.SmoothingMode = SmoothingMode.AntiAlias;   // lo que XP le pone (despues del primer return) y le deja puesto
            }
        }

        private partial class SearchOverlay
        {
            // ---- LA FIRMA DE XP (Form1.Buscador.cs, 938): OnPaint (EnsureUiOverlay de Form1.Video.cs y los precalentados de Form1.Portadas.cs
            // y PrecargaPaginaGpu.cs ya llaman directo a la traducida). XP le deja puesto el suavizado (despues del primer return; RenderNuevo
            // tambien lo pone) y el filtro que ponga DrawPreviewCube (la traducida lo devuelve por ref).
            public void RenderContent(Graphics g, Rectangle clip, bool forGpu)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                Filtro filtro = e.Filtro;
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, ref filtro, e.Medio, e.Hdc, clip, forGpu);
                if (Width >= 60 && Height >= 60) g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = (InterpolationMode)(int)filtro;
            }
        }
    }
}

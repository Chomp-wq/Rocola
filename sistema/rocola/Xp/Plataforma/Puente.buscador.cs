// EL PUENTE DEL GRUPO «BUSCADOR», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Form1.Buscador.cs (la casilla
// de BUSCAR, BuscaView, y el buscador de siempre, SearchOverlay: el teclado, los resultados, el cubo de la vista previa, el aviso) ya dibuja con
// SkiaSharp directo y las funciones nativas (Xp/Nativo); donde lo llama o lo recibe codigo que todavia dibuja con la capa que imita a GDI+
// (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto queda:
// cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs (PORT_buscador.md dice
// cual es cual).
//   - Las firmas de XP de lo que llaman los archivos sin traducir con un Graphics: BuscaView.RenderContent (OnPaint y DrawCtrlOverlay de
//     Form1.Video.cs), SearchOverlay.RenderContent (OnPaint, EnsureUiOverlay de Form1.Video.cs y los precalentados de Form1.Portadas.cs y
//     PrecargaPaginaGpu.cs) y SearchOverlay.DibujaPiezaViva (EnsureUiPieza de Form1.Video.cs). Le pasan a la traducida el lienzo del Graphics y
//     su estado (PuenteDibujo.Lienzo y Estado: suavizado, modo de letra, copia, filtro, medio pixel y la matriz del HDC) y despues le dejan al
//     Graphics lo que XP le dejaba cambiado: el suavizado (SmoothingMode = AntiAlias, que las tres ponen y no devuelven, cuando llegan a esa
//     linea) y, en SearchOverlay.RenderContent, el filtro (InterpolationMode = Bilinear de DrawPreviewCube, el cubo del dibujo por procesador).
//   - Lo que la traducida le pide a lo que aun no lo esta (pinta con un Graphics y una Font): CtrlTxt, CtrlDot, CtrlTri, CtrlPad, GlowAround,
//     CtrlDirLeft y Cube.Draw (Form1.Pared.cs) y Starfield.DrawReactive (el cielo, Form1.Fondo.cs). Un Graphics sobre el MISMO lienzo (con su
//     matriz y su recorte) con el estado del que llama, y la Font de la Letra (los mismos datos: familia, tamano, estilo y unidad). Ninguna le
//     cambia el estado al Graphics sin devolverlo (las de Form1.Pared.cs solo pintan con su pincel y su pluma; DrawReactive pone la copia para
//     el cielo y la devuelve), asi que no hay nada que leer despues. Lo que no usan va con su valor de fabrica: el filtro de las que no pintan
//     imagenes y la matriz del HDC de todas (ninguna escribe con el texto de GDI, TextRenderer).
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
            // ---- LAS FIRMAS DE XP (Form1.Buscador.cs, 233 y 237): OnPaint (con ClearTypeGridFit) y DrawCtrlOverlay de Form1.Video.cs (forGpu)
            public void RenderContent(Graphics g) { RenderContent(g, false); }
            public void RenderContent(Graphics g, bool forGpu)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, e.Medio, forGpu);
                if (Width >= 30 && Height >= 12) g.SmoothingMode = SmoothingMode.AntiAlias;   // lo que XP le pone (despues del primer return) y le deja puesto
            }
        }

        private partial class SearchOverlay
        {
            // ---- LA FIRMA DE XP (Form1.Buscador.cs, 938): OnPaint, EnsureUiOverlay (Form1.Video.cs) y los precalentados (Form1.Portadas.cs y
            // PrecargaPaginaGpu.cs). XP le deja puesto el suavizado (despues del primer return; RenderNuevo tambien lo pone) y el filtro que ponga
            // DrawPreviewCube (la traducida lo devuelve por ref).
            public void RenderContent(Graphics g, Rectangle clip, bool forGpu)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                Filtro filtro = e.Filtro;
                RenderContent(PuenteDibujo.Lienzo(g), e.Letra, e.Copia, ref filtro, e.Medio, e.Hdc, clip, forGpu);
                if (Width >= 60 && Height >= 60) g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = (InterpolationMode)(int)filtro;
            }
#if ROCOLA_GPU
            // ---- LA FIRMA DE XP (Form1.Buscador.cs, 876): EnsureUiPieza (Form1.Video.cs). XP le pone SmoothingMode = AntiAlias (y se lo deja) en
            // tres caminos: el buscador con portadas (al empezar DibujaPiezaVivaNueva), la casilla de lo escrito y la tecla elegida (despues de
            // sus comprobaciones). La traducida lo devuelve en 'suave' (ref), que no distingue AntiAlias de HighQuality: aqui se mira, ANTES de
            // dibujar (dibujar no cambia nada de esto), si XP llega a esa linea, para dejarle al Graphics exactamente lo de XP.
            public void DibujaPiezaViva(Graphics g, int cual, int dx, int dy)
            {
                bool pone = Nuevo || (cual < 2 && (cual == PiezaEscrito || (cual == PiezaTecla && !FocusResults && SelRow >= 0 && SelRow < rows.Count && SelCol >= 0 && SelCol < rows[SelRow].Count)));
                EstadoLienzo e = PuenteDibujo.Estado(g);
                bool suave = e.Suave;
                DibujaPiezaViva(PuenteDibujo.Lienzo(g), ref suave, e.Letra, e.Copia, e.Filtro, e.Medio, e.Hdc, cual, dx, dy);
                if (pone) g.SmoothingMode = SmoothingMode.AntiAlias;
            }
#endif
        }

        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA
        // un Graphics sobre el mismo lienzo con el estado del que llama
        private static Graphics GraphicsBuscador(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, SKMatrix hdc)
        { return PuenteDibujo.Graphics(g, new EstadoLienzo(suave, ajuste, copia, filtro, medio, hdc)); }
        // CtrlTxt, CtrlDot, CtrlTri, CtrlPad, GlowAround y CtrlDirLeft (Form1.Pared.cs): los simbolos de los botones y sus palabras; pintan con el
        // pincel y la pluma de la capa (CtrlTxt mide y escribe con GDI+, con el modo de letra del lienzo); no pintan imagenes (el filtro, el de
        // fabrica de un Graphics) ni escriben con GDI (la matriz del HDC, la identidad)
        private static float CtrlTxtBuscador(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Rocola.Nativo.Letra f, string s, float x, int cy, Color c)
        {
            using (Graphics gg = GraphicsBuscador(g, suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity))
            using (Font ff = PuenteDibujo.Font(f))
                return CtrlTxt(gg, ff, s, x, cy, c);
        }
        private static float CtrlDotBuscador(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)
        {
            using (Graphics gg = GraphicsBuscador(g, suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity))
                return CtrlDot(gg, x, cy, d, c);
        }
        private static void CtrlTriBuscador(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Color c, PointF a, PointF b, PointF d)
        {
            using (Graphics gg = GraphicsBuscador(g, suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity))
                CtrlTri(gg, c, a, b, d);
        }
        private static float CtrlPadBuscador(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz)
        {
            using (Graphics gg = GraphicsBuscador(g, suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity))
                return CtrlPad(gg, x, cy, sz);
        }
        private static void GlowAroundBuscador(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float leftX, float cy, float symW, float level, Color c)
        {
            using (Graphics gg = GraphicsBuscador(g, suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity))
                GlowAround(gg, leftX, cy, symW, level, c);
        }
        private static float CtrlDirLeftBuscador(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int sz, Color c)
        {
            using (Graphics gg = GraphicsBuscador(g, suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity))
                return CtrlDirLeft(gg, x, cy, sz, c);
        }
        // Cube.Draw (Form1.Pared.cs): el cubo de la vista previa del dibujo por procesador; pinta las portadas con el filtro del que llama
        private static void CubeDrawBuscador(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, SKMatrix hdc, Rectangle area, Image cover, double angle)
        {
            using (Graphics gg = GraphicsBuscador(g, suave, ajuste, copia, filtro, medio, hdc))
                Cube.Draw(gg, area, cover, angle);
        }
        // Starfield.DrawReactive (Form1.Fondo.cs): el cielo del buscador del dibujo por procesador; mira el recorte del lienzo (ClipBounds) y
        // pinta mapas con el filtro del que llama
        private static void DrawReactiveBuscador(Starfield sky, SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio, SKMatrix hdc,
                                                 int w, int h, Color skyTop, Color skyBottom, float pLo, float pMid, float pHi)
        {
            using (Graphics gg = GraphicsBuscador(g, suave, ajuste, copia, filtro, medio, hdc))
                sky.DrawReactive(gg, w, h, skyTop, skyBottom, pLo, pMid, pHi);
        }
    }
}

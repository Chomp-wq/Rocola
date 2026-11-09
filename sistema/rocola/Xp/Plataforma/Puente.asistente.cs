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
    // ================================================================ PulidoGpu.cs
    public partial class Form1
    {
        // ---- LA FIRMA DE XP DE DegradadoV (devuelve un Brush de GDI+), PARA QUIEN TODAVIA LO PIDE ASI: EstadoReposoGpu.cs y AcabadoGpu.cs (ya
        // traducidos, lo pasan a un SKPaint con PuenteDibujo.Pincel(b, false)). La traducida da el shader del pincel, y un shader no tiene vuelta
        // a un pincel de GDI+ (lo mismo que BrochaReposo y BrochaSuena con los pinceles con shader): aqui va el camino de XP (PulidoGpu.cs,
        // 142-177) con los pinceles de GDI+, sobre el MISMO guardado de cuatro mapas (degradados, degradadoTurno y su cerrojo) y con las MISMAS
        // cuentas (DegradadosGuardados, DegradadosReusados) que la traducida: los dos caminos se reparten los mapas como en XP. El TextureBrush
        // pinta un mapa del guardado prestado (PuenteDibujo.Bitmap: el dueno es el guardado, como el Bitmap de XP), y el de los tamanos de mas de
        // 2048, uno que se suelta al volver (como en XP y en la capa). Se borra cuando esos dos llamen a la traducida, DegradadoV(r, c0, c1,
        // false), que da el shader con el medio pixel de fabrica de su lienzo.
        internal static Rocola.Dibujo2D.Brush DegradadoV(Rectangle r, Color c0, Color c1)
        {
            if (!InterfazTramada || r.Height < 2 || r.Width < 1) return new LinearGradientBrush(r, c0, c1, 90f);
            if (r.Height > 2048) { using (Rocola.Dibujo2D.Bitmap b = PuenteDibujo.Entrega(HorneaMapaDegradado(r.Height, c0, c1))) return CopiaDegradadoAsistente(b, r.Location); }
            lock (degradadoCerrojo)
            {
                int a = c0.ToArgb(), b = c1.ToArgb();
                for (int i = 0; i < degradados.Length; i++)
                {
                    DegradadoGuardado d = degradados[i];
                    if (d == null || d.Alto != r.Height || d.Inicio != a || d.Fin != b) continue;
                    TextureBrush copia = CopiaDegradadoAsistente(PuenteDibujo.Bitmap(d.Mapa, PixelFormat.Format32bppArgb), r.Location);
                    DegradadosReusados++; return copia;
                }
                SKBitmap fuente = HorneaMapaDegradado(r.Height, c0, c1);
                TextureBrush nuevo;
                try { nuevo = CopiaDegradadoAsistente(PuenteDibujo.Bitmap(fuente, PixelFormat.Format32bppArgb), r.Location); }
                catch { fuente.Dispose(); throw; }
                DegradadoGuardado anterior = degradados[degradadoTurno];
                degradados[degradadoTurno] = new DegradadoGuardado { Alto = r.Height, Inicio = a, Fin = b, Mapa = fuente };
                degradadoTurno = (degradadoTurno + 1) % degradados.Length;
                if (anterior != null) anterior.Mapa.Dispose();
                DegradadosGuardados++; return nuevo;
            }
        }
        // el CopiaDegradado de XP con el pincel de GDI+ (el de la traducida da el shader)
        private static TextureBrush CopiaDegradadoAsistente(Rocola.Dibujo2D.Bitmap fuente, Point origen)
        {
            TextureBrush copia = new TextureBrush(fuente, WrapMode.Tile);
            try { copia.TranslateTransform(origen.X, origen.Y); return copia; }
            catch { copia.Dispose(); throw; }
        }
    }

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

        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA: CtrlDot, CtrlTxt y CtrlTri (Form1.Pared.cs) pintan con un Graphics: uno sobre
        // el MISMO lienzo con el estado del que llama (GraphicsAsistente, abajo), y CtrlTxt con la Font de esa Letra (los mismos datos: familia,
        // tamano, unidad y estilo; se suelta al acabar, porque CtrlTxt no se la queda). Ninguna de las tres le cambia el estado al Graphics.
        // Cuando Form1.Pared.cs se traduzca, la llamada pierde el «Asistente» (con el lienzo y el estado como aqui) y estas tres se borran.
        private static float CtrlDotAsistente(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, float x, int cy, int d, Color c)
        { return CtrlDot(PuenteDibujo.GraphicsAsistente(g, suave, ajuste, copia, medio), x, cy, d, c); }
        private static float CtrlTxtAsistente(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Rocola.Nativo.Letra f, string s, float x, int cy, Color c)
        {
            using (Font ff = PuenteDibujo.Font(f))
                return CtrlTxt(PuenteDibujo.GraphicsAsistente(g, suave, ajuste, copia, medio), ff, s, x, cy, c);
        }
        private static void CtrlTriAsistente(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, Color c, PointF a, PointF b, PointF d)
        { CtrlTri(PuenteDibujo.GraphicsAsistente(g, suave, ajuste, copia, medio), c, a, b, d); }
    }

    internal static partial class PuenteDibujo
    {
        // un Graphics sobre el lienzo de un archivo traducido del grupo, con su estado, para llamar a uno que no lo esta: el filtro, el de fabrica de
        // un Graphics (Bilinear: lo que se llama con el no pinta imagenes); la matriz del HDC, la identidad (la de un mapa de bits: los globos de la
        // estrella; en la casilla pintada por OnPaint seria la de su sitio en la pantalla, pero lo que se llama con el no usa TextRenderer, el
        // unico que la mira). No suelta el lienzo.
        internal static Graphics GraphicsAsistente(SKCanvas c, bool suave, AjusteLetra ajuste, bool copia, bool medio)
        { return Graphics(c, new EstadoLienzo(suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity)); }
    }
}
#endif

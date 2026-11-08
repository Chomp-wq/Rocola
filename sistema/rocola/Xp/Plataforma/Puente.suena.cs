// EL PUENTE DEL GRUPO «SUENA», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 8-11-2026). SuenaVivoGpu.cs (lo que suena
// en grande, la moneda que cae, la invitacion sin creditos y los avisos) ya dibuja con SkiaSharp directo y las funciones nativas (Xp/Nativo);
// donde lo llama o lo recibe codigo que todavia dibuja con la capa que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La regla: el puente va del
// lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto queda: cada parte se borra en cuanto se traduzca el archivo que
// la necesita, y todo al final, con Dibujo2D.cs y Puente.cs (PORT_suena.md dice cual es cual).
//   - Las firmas de XP de lo que llaman PrecargaPaginaGpu.cs (la precarga de las texturas: TamanoAviso, PintaAviso, PintaInvita, TamanoMas
//     y PintaMas, con Bitmap y Font) y EstadoReposoGpu.cs (el panel de reposo: PintaTituloGrande y PintaMinutosCola, con su Graphics, sus
//     Font, sus Brush y su StringFormat). Son delgadas: pasan a la traducida los mismos puntos (PuenteDibujo.Mapa), la misma Letra, el
//     color o el shader de cada pincel, el MISMO FormatoTexto y el lienzo con su estado; la traducida no le cambia el estado al Graphics.
//   - Lo que la traducida le pide a lo que no lo esta: AnchoTexto (Form1.Ajustes.cs), que mide con una Font, y PintaTextoMovil
//     (TextoMovilGpu.cs), que pinta con un Graphics, una Font, un Brush y un StringFormat.
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
        // ---- LAS FIRMAS DE XP PARA PrecargaPaginaGpu.cs (case 6: el aviso, la invitacion, la moneda y su "+4 creditos")
        internal static Size TamanoAviso(int anchoPantalla, string titulo, string detalle, Font fTit, Font fDet)
        { return TamanoAviso(anchoPantalla, titulo, detalle, PuenteDibujo.Letra(fTit), PuenteDibujo.Letra(fDet)); }
        internal static void PintaAviso(Bitmap bmp, TipoAviso tipo, string titulo, string detalle, Font fTit, Font fDet)
        { PintaAviso(PuenteDibujo.Mapa(bmp), tipo, titulo, detalle, PuenteDibujo.Letra(fTit), PuenteDibujo.Letra(fDet)); }
        internal static void PintaInvita(Bitmap bmp, string cancion, bool esVideo, int creditosPorMoneda, Font fTit, Font fTxt, Font fPie, out PointF ranura, out float radio)
        { PintaInvita(PuenteDibujo.Mapa(bmp), cancion, esVideo, creditosPorMoneda, PuenteDibujo.Letra(fTit), PuenteDibujo.Letra(fTxt), PuenteDibujo.Letra(fPie), out ranura, out radio); }
        internal static Size TamanoMas(string txt, Font f) { return TamanoMas(txt, PuenteDibujo.Letra(f)); }
        internal static void PintaMas(Bitmap bmp, string txt, Font f) { PintaMas(PuenteDibujo.Mapa(bmp), txt, PuenteDibujo.Letra(f)); }

        // ---- LAS FIRMAS DE XP PARA EstadoReposoGpu.cs (RenderIdlePanelEn): el lienzo del Graphics y su estado, los pinceles en SKPaint (su
        // color o su shader, con el medio pixel del Graphics; un pincel nulo no pinta, como en GDI+), la misma Letra y el mismo FormatoTexto
        internal static void PintaTituloGrande(Graphics g, ReposoGrande x, string title, string artist, Font big, Font normal, Font small,
            Brush white, Brush color, StringFormat line, int tx, int tw)
        {
            EstadoLienzo e = PuenteDibujo.Estado(g);
            SKPaint pw = white != null ? PuenteDibujo.Pincel(white, e.Medio) : null, pc = color != null ? PuenteDibujo.Pincel(color, e.Medio) : null;
            try
            {
                PintaTituloGrande(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, x, title, artist, PuenteDibujo.Letra(big), PuenteDibujo.Letra(normal),
                    PuenteDibujo.Letra(small), pw, pc, PuenteDibujo.Formato(line), tx, tw);
            }
            finally { PuenteDibujo.SueltaPincelSuena(pw); PuenteDibujo.SueltaPincelSuena(pc); }
        }
        internal static void PintaMinutosCola(Graphics g, string minutos, string artista, Font f, Brush tenue, bool estrecha,
            float x, float y, float w, float h, StringFormat line)
        {
            EstadoLienzo e = PuenteDibujo.Estado(g);
            SKPaint pt = tenue != null ? PuenteDibujo.Pincel(tenue, e.Medio) : null;
            try { PintaMinutosCola(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, minutos, artista, PuenteDibujo.Letra(f), pt, estrecha, x, y, w, h, PuenteDibujo.Formato(line)); }
            finally { PuenteDibujo.SueltaPincelSuena(pt); }
        }

        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA
        // AnchoTexto (Form1.Ajustes.cs) mide con una Font: la de esa Letra (la misma familia, tamano, unidad y estilo), soltada al acabar
        private static int AnchoTextoSuena(string t, Rocola.Nativo.Letra l)
        {
            using (Font f = PuenteDibujo.Font(l)) return AnchoTexto(t, f);
        }
        // PintaTextoMovil (TextoMovilGpu.cs) pinta con un Graphics: uno sobre el mismo lienzo con el estado del que llama (el filtro, que el
        // texto no usa, el de fabrica de un Graphics; la matriz del HDC, la de un mapa de bits: la identidad, porque quien llama, el panel de
        // reposo, pinta en un mapa), la Font de la Letra, un SolidBrush del color del pincel (PintaTextoMovil mira si es un SolidBrush para
        // llevarse su color a la cinta) y el MISMO FormatoTexto
        private static void PintaTextoMovilSuena(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, string text, Rocola.Nativo.Letra f, SKPaint brush,
            RectangleF rect, FormatoTexto sf)
        {
            using (Graphics gg = PuenteDibujo.Graphics(g, new EstadoLienzo(suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity)))
            using (Font ff = PuenteDibujo.Font(f))
                PintaTextoMovil(gg, text, ff, PuenteDibujo.BrochaSuena(brush), rect, PuenteDibujo.StringFormat(sf));
        }
    }

    internal static partial class PuenteDibujo
    {
        // un pincel de color (SKPaint sin shader) de un archivo traducido para uno que no lo esta y lo pide como Brush: el SolidBrush de ese
        // color (el mismo ARGB). Uno con shader (un degradado o una textura) no tiene vuelta: XP solo pasa por aqui pinceles de color
        // (EstadoReposoGpu.cs: white, color y muted, todos SolidBrush), asi que si llegara uno se dice en vez de pintar otra cosa.
        internal static Brush BrochaSuena(SKPaint p)
        {
            if (p == null) return null;
            if (p.Shader != null) throw new NotSupportedException("PuenteDibujo.BrochaSuena: un pincel con shader no se puede pasar a GDI+ (solo de color)");
            return new SolidBrush(ColorSk.DeSk(p.Color));
        }
        // suelta un pincel hecho con Pincel(Brush, medio): su shader (si lo tiene) y el
        internal static void SueltaPincelSuena(SKPaint p)
        {
            if (p == null) return;
            SKShader s = p.Shader; p.Shader = null; if (s != null) s.Dispose();
            p.Dispose();
        }
    }
}
#endif

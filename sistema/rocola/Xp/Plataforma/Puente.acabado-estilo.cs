// EL PUENTE DEL GRUPO «ACABADO-ESTILO», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 2 del dibujo, 8-11-2026). GuiaRojoGpu.cs (la guia
// del rojo mantenido) y EstiloGpu.cs (la franja de lo que suena, de lejos) ya dibujan con SkiaSharp directo y las funciones nativas
// (Xp/Nativo); donde los llama o donde llaman ellos a codigo que todavia dibuja con la capa que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La
// regla: el puente va del lado de lo que no esta traducido, y los que llaman no cambian. Nada de esto queda: cada parte se borra en cuanto se
// traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y Puente.cs (PORT_acabado-estilo.md dice cual es cual).
//   - Las firmas de XP de lo que llaman con un Graphics Form1.Video.cs (EnsureUiOverlay: DrawGuiaRojo; EnsureUiBotOverlay: FranjaLejos,
//     PintaTituloCompacto y PintaSiguienteCompacto) y TextoMovilGpu.cs (PintaArtistaCompacto: PintaRenglonLejos, con su Font). Son delgadas:
//     pasan a la traducida el lienzo del Graphics (con su matriz y su recorte) y su estado (PuenteDibujo.Estado), la misma Letra de la Font y
//     la misma alineacion. Ninguna traducida le cambia el estado al Graphics (lo que guarda, lo devuelve), asi que no hay nada que devolverle.
//   - Lo que la traducida le pide a lo que no lo esta y pinta con un Graphics (TextoConCinta, PintaArtistaCompacto, DrawLabelOverlay,
//     DrawCtrlOverlay): un Graphics sobre el MISMO lienzo con el estado del que llama (GraphicsAcabado).
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
        // ---- LA FIRMA DE XP PARA Form1.Video.cs (EnsureUiOverlay: la imagen de la interfaz), de GuiaRojoGpu.cs
        private void DrawGuiaRojo(Graphics g)
        { EstadoLienzo e = PuenteDibujo.Estado(g); DrawGuiaRojo(PuenteDibujo.Lienzo(g), e.Suave, e.Copia, e.Medio); }

        // ---- LAS FIRMAS DE XP PARA Form1.Video.cs (EnsureUiBotOverlay: la franja de abajo) Y TextoMovilGpu.cs (PintaArtistaCompacto), de EstiloGpu.cs
        private bool FranjaLejos(Graphics g)
        { EstadoLienzo e = PuenteDibujo.Estado(g); return FranjaLejos(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio); }
        private void PintaTituloCompacto(Graphics g, bool grande)
        { EstadoLienzo e = PuenteDibujo.Estado(g); PintaTituloCompacto(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, grande); }
        private void PintaSiguienteCompacto(Graphics g)
        { EstadoLienzo e = PuenteDibujo.Estado(g); PintaSiguienteCompacto(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio); }
        // (la letra: la misma Letra de la Font, sin copia; StringAlignment y Alinea tienen los mismos valores)
        private static void PintaRenglonLejos(Graphics g, string texto, Font f, Color c, RectangleF r, StringAlignment al)
        { EstadoLienzo e = PuenteDibujo.Estado(g); PintaRenglonLejos(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, texto, PuenteDibujo.Letra(f), c, r, (Alinea)(int)al); }

        // ---- LO QUE LA TRADUCIDA LE PIDE A LO QUE AUN NO LO ESTA
        // TextoConCinta (TextoMovilGpu.cs: el texto que no cabe se lleva a una cinta que la tarjeta desliza) pide un Graphics (mira su recorte, su
        // matriz y su modo de letra), una Font (su familia, tamano, estilo y unidad van en la clave de la cinta) y un StringFormat: el Graphics de
        // GraphicsAcabado, la Font de esa Letra (los mismos datos; se suelta al acabar: la cinta no se la queda) y el MISMO FormatoTexto
        private static bool TextoConCintaAcabado(SKCanvas g, bool suave, AjusteLetra ajuste, bool copia, bool medio, string text, Rocola.Nativo.Letra f, Color color,
            RectangleF rect, FormatoTexto sf)
        {
            using (Font ff = PuenteDibujo.Font(f))
                return TextoConCinta(PuenteDibujo.GraphicsAcabado(g, suave, ajuste, copia, medio), text, ff, color, rect, PuenteDibujo.StringFormat(sf));
        }
    }

    internal static partial class PuenteDibujo
    {
        // un Graphics sobre el lienzo de un archivo traducido del grupo, con su estado, para llamar a uno que no lo esta: el filtro, el de fabrica de
        // un Graphics (Bilinear: ni la franja ni lo que pinta lo llamado con este lo cambian; y lo llamado no pinta imagenes con el); la matriz del
        // HDC, la de un mapa de bits: la identidad (la franja pinta en un mapa: uiBotOverlay de Form1.Video.cs; y lo llamado no usa
        // TextRenderer). No suelta el lienzo.
        internal static Graphics GraphicsAcabado(SKCanvas c, bool suave, AjusteLetra ajuste, bool copia, bool medio)
        { return Graphics(c, new EstadoLienzo(suave, ajuste, copia, Filtro.Bilineal, medio, SKMatrix.Identity)); }
    }
}
#endif

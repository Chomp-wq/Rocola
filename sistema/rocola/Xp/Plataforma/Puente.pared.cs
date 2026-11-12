// EL PUENTE DEL GRUPO «PARED», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Form1.Pared.cs (la pared de
// discos, el cubo, las ayudas de los botones, la atraccion y el cubo de la cancion), CaratulasSeparadasGpu.cs, PrecargaPaginaGpu.cs y
// PaletaCaratulaGpu.cs ya dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo); donde los llama codigo que todavia dibuja con la
// capa que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no
// cambian. Nada de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y
// Puente.cs (PORT_pared.md dice cual es cual).
//   - Las firmas de XP (con un Graphics, una Font, una Image o un Bitmap de la capa) de lo que llaman los archivos sin traducir:
//     Cube.Draw (Form1.Buscador.cs), GlowAround, CtrlDot, CtrlTxt, CtrlPad (Form1.cs y Form1.Buscador.cs), CtrlTri (Form1.Buscador.cs),
//     CtrlDirLeft y CtrlDirRight (Form1.Buscador.cs y Form1.Vistas.cs), CB (Form1.Vistas.cs), SueltaBitmaps (Form1.Vistas.cs) y
//     ExtractCoverPalette (Form1.Fondo.cs y Form1.Portadas.cs). Son delgadas: le pasan a la traducida el lienzo del Graphics (con su matriz y
//     su recorte) y su estado (PuenteDibujo.Lienzo y Estado), la misma Letra y los mismos puntos (PuenteDibujo.Letra y Mapa). Ninguna de las
//     traducidas le cambia el estado al Graphics (tampoco en XP: solo pintan), asi que no hay nada que devolverle.
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
        // ---- Cube.Draw, para Form1.Buscador.cs (el cubo de la vista previa del buscador clasico): el lienzo y el estado del Graphics
        // (suavizado, copia, filtro y medio pixel) y los puntos de la portada
        private static partial class Cube
        {
            public static void Draw(Graphics g, Rectangle bounds, Image cover, double angle)
            {
                EstadoLienzo e = PuenteDibujo.Estado(g);
                Draw(PuenteDibujo.Lienzo(g), e.Suave, e.Copia, e.Filtro, e.Medio, bounds, PuenteDibujo.Mapa(cover), angle);
            }
        }

        // ---- LAS AYUDAS DE LOS BOTONES (el halo, el punto, la palabra, el triangulo, la cruceta y las dos flechas), para Form1.cs (HelpBar),
        // Form1.Buscador.cs y Form1.Vistas.cs
        private static void GlowAround(Graphics g, float leftX, float cy, float symW, float level, Color c)
        { EstadoLienzo e = PuenteDibujo.Estado(g); GlowAround(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, leftX, cy, symW, level, c); }
        private static float CtrlDot(Graphics g, float x, int cy, int d, Color c)
        { EstadoLienzo e = PuenteDibujo.Estado(g); return CtrlDot(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, x, cy, d, c); }
        private static float CtrlTxt(Graphics g, Font f, string s, float x, int cy, Color c)
        { EstadoLienzo e = PuenteDibujo.Estado(g); return CtrlTxt(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, PuenteDibujo.Letra(f), s, x, cy, c); }
        private static void CtrlTri(Graphics g, Color c, PointF a, PointF b, PointF d)
        { EstadoLienzo e = PuenteDibujo.Estado(g); CtrlTri(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, c, a, b, d); }
        private static float CtrlPad(Graphics g, float x, int cy, int sz)
        { EstadoLienzo e = PuenteDibujo.Estado(g); return CtrlPad(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, x, cy, sz); }
        private static float CtrlDirLeft(Graphics g, float x, int cy, int sz, Color c)
        { EstadoLienzo e = PuenteDibujo.Estado(g); return CtrlDirLeft(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, x, cy, sz, c); }
        private static float CtrlDirRight(Graphics g, float x, int cy, int sz, Color c)
        { EstadoLienzo e = PuenteDibujo.Estado(g); return CtrlDirRight(PuenteDibujo.Lienzo(g), e.Suave, e.Letra, e.Copia, e.Medio, x, cy, sz, c); }

#if ROCOLA_GPU
        // ---- ExtractCoverPalette CON UN Bitmap DE LA CAPA, para Form1.Fondo.cs y Form1.Portadas.cs (los colores de una portada al entrar
        // en la cache): los mismos puntos y el formato que tiene ese Bitmap (de el depende como se leen: PaletaCaratulaGpu.cs)
        internal static Color[] ExtractCoverPalette(Bitmap bitmap)
        {
            if (bitmap == null) return new Color[0];
            return ExtractCoverPalette(PuenteDibujo.Mapa(bitmap), PuenteDibujo.FormatoMapaPared(bitmap.PixelFormat));
        }
#endif
    }

    internal static partial class PuenteDibujo
    {
        // el PixelFormat de un Bitmap de la capa como el dato nativo (Nativo/FormatoMapa.cs)
        internal static FormatoMapa FormatoMapaPared(PixelFormat f)
        {
            return f == PixelFormat.Format32bppPArgb ? FormatoMapa.PArgb32 : f == PixelFormat.Format32bppArgb ? FormatoMapa.Argb32
                 : f == PixelFormat.Format32bppRgb ? FormatoMapa.Rgb32 : f == PixelFormat.Format24bppRgb ? FormatoMapa.Rgb24 : FormatoMapa.Otro;
        }
    }
}

// EL PUENTE DEL GRUPO «PARED», SOLO MIENTRAS DURA LA TRADUCCION (port nativo, tanda 3 del dibujo, 11-11-2026). Form1.Pared.cs (la pared de
// discos, el cubo, las ayudas de los botones, la atraccion y el cubo de la cancion), CaratulasSeparadasGpu.cs, PrecargaPaginaGpu.cs y
// PaletaCaratulaGpu.cs ya dibujan con SkiaSharp directo y las funciones nativas (Xp/Nativo); donde los llama codigo que todavia dibuja con la
// capa que imita a GDI+ (Dibujo2D.cs), pasa por aqui. La regla: el puente va del lado de lo que no esta traducido, y los que llaman no
// cambian. Nada de esto queda: cada parte se borra en cuanto se traduzca el archivo que la necesita, y todo al final, con Dibujo2D.cs y
// Puente.cs (PORT_pared.md dice cual es cual).
//   - Queda solo ExtractCoverPalette con un Bitmap de la capa (Form1.Fondo.cs y Form1.Portadas.cs la llaman aun con un Bitmap): delgada,
//     le pasa a la traducida los mismos puntos (PuenteDibujo.Mapa) y el formato que tiene ese Bitmap. Las firmas de XP de Cube.Draw,
//     GlowAround, CtrlDot, CtrlTxt, CtrlPad, CtrlTri, CtrlDirLeft y CtrlDirRight (con un Graphics) se borraron el 12-11-2026 (grupo
//     limpieza-puentes): ya nadie las llamaba, todos los llamadores llaman directo a las traducidas con el lienzo y su estado.
using System;
using System.Drawing;
using Rocola.Dibujo2D;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    public partial class Form1
    {
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

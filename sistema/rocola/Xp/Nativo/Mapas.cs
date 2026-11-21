// LOS MAPAS DE BITS DE LA ROCOLA Y SUS COLORES, NATIVOS (port nativo, el dibujo; 8-11-2026). Lo que en XP era new Bitmap(w, h,
// PixelFormat.Format32bppPArgb) es un SKBitmap Bgra8888 premultiplicado y VACIO (GDI+ lo entrega transparente; sin borrarlo quedaria lo que
// hubiera en esa memoria). Color, Rectangle y los demas tipos sencillos son los de .NET (System.Drawing.Primitives), no de GDI+.
using System;
using System.Drawing;
using SkiaSharp;

namespace Rocola.Nativo
{
    public static class Mapas
    {
        // new Bitmap(w, h, ...): al menos de 1x1 (como Dibujo2D), transparente
        public static SKBitmap Nuevo(int w, int h)
        {
            SKBitmap b = new SKBitmap(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Bgra8888, SKAlphaType.Premul));
            b.Erase(SKColors.Transparent);
            return b;
        }
        // Bitmap.Clone(zona, formato): ese trozo, copiado punto por punto
        public static SKBitmap Copia(SKBitmap b, Rectangle r)
        {
            SKBitmap c = new SKBitmap(new SKImageInfo(r.Width, r.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            if (CopiaDirecta(b, r, c)) return c;
            using (SKCanvas cv = new SKCanvas(c)) { cv.Clear(SKColors.Transparent); cv.DrawBitmap(b, new SKRect(r.Left, r.Top, r.Right, r.Bottom), new SKRect(0, 0, r.Width, r.Height)); }
            return c;
        }
        // (rapido-caratulas, 20-11-2026) Pintar un trozo de un BGRA premultiplicado del mismo tamano en otro (sin escalar, en sitios enteros y
        // todo dentro) sobre transparente es copiar sus bytes: fila a fila, sin lienzo. Cualquier otro caso (otro formato, un trozo que se sale),
        // pintado como siempre. Los mismos puntos (Pruebas/RapidoCaratulas: lee-igual).
        private static unsafe bool CopiaDirecta(SKBitmap b, Rectangle r, SKBitmap c)
        {
            if (b == null || b.ColorType != SKColorType.Bgra8888 || b.AlphaType != SKAlphaType.Premul) return false;
            if (r.Width <= 0 || r.Height <= 0 || r.X < 0 || r.Y < 0 || r.Right > b.Width || r.Bottom > b.Height) return false;
            byte* o = (byte*)b.GetPixels(), d = (byte*)c.GetPixels();
            if (o == null || d == null) return false;
            int ro = b.RowBytes, rd = c.RowBytes; long fila = (long)r.Width * 4;
            for (int y = 0; y < r.Height; y++)
                Buffer.MemoryCopy(o + (long)(r.Y + y) * ro + (long)r.X * 4, d + (long)y * rd, rd, fila);
            c.NotifyPixelsChanged();
            return true;
        }
    }

    // EL COLOR DE .NET EN SKIA: c.Sk() (y el de Skia en .NET, para GetPixel)
    public static class ColorSk
    {
        public static SKColor Sk(this Color c) { return new SKColor(c.R, c.G, c.B, c.A); }
        public static Color DeSk(SKColor c) { return Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue); }
    }
}

// LAS IMAGENES COMO LAS PINTA, LEE Y GUARDA GDI+, NATIVAS (port nativo, el dibujo; 8-11-2026): DrawImage y DrawImageUnscaled (con
// ImageAttributes: matriz de color, envoltura y clave de color), Image.FromStream y Save de XP. Sin estado: el filtro (InterpolationMode), el
// medio pixel (PixelOffsetMode Half o HighQuality) y la copia (CompositingMode.SourceCopy) llegan como parametros; la matriz y el recorte son
// los del SKCanvas. Los mapas son SKBitmap Bgra8888 premultiplicados (como Format32bppPArgb).
// Los algoritmos son los de Plataforma/Dibujo2D.cs (revision del 8-11-2026), movidos aqui.
using System;
using System.Drawing;
using System.IO;
using SkiaSharp;

namespace Rocola.Nativo
{
    // los filtros de XP (los valores de InterpolationMode de GDI+, con nombre propio). DeFabrica, Baja y Bilineal son el bilineal de siempre;
    // Alta, BilinealAlta y BicubicaAlta prefiltran al achicar.
    public enum Filtro { DeFabrica = 0, Baja = 1, Alta = 2, Bilineal = 3, Bicubico = 4, Vecino = 5, BilinealAlta = 6, BicubicaAlta = 7 }
    // lo de fuera de la imagen (los valores de WrapMode de GDI+): Transparente es WrapMode.Clamp, el de fabrica de DrawImage
    public enum Envoltura { Repite = 0, EspejoX = 1, EspejoY = 2, Espejo = 3, Transparente = 4 }
    // lo que GDI+ diria del archivo leido (Image.PixelFormat y Flags): un JPEG (o un PNG sin alfa) es de 24 bits sin alfa, un GIF de paleta, y
    // lo que trae alfa de 32 bits con alfa
    public enum FormatoImagen { Argb32 = 0, Rgb24 = 1, Paleta8 = 2 }

    // LA MATRIZ DE COLOR DE GDI+ (ColorMatrix): 5x5, el color como fila (r g b a 1) por la matriz; Matrix33 es [3][3]
    public static class MatrizColor
    {
        public static float[][] Identidad() { float[][] m = new float[5][]; for (int i = 0; i < 5; i++) { m[i] = new float[5]; m[i][i] = 1f; } return m; }
        // GDI+ multiplica el color como fila (r g b a 1) por la matriz; Skia, la matriz por la columna; los dos con el desplazamiento en 0..1
        public static float[] Skia(float[][] M)
        {
            float[] s = new float[20];
            for (int fila = 0; fila < 4; fila++)
            {
                for (int col = 0; col < 4; col++) s[fila * 5 + col] = M[col][fila];
                s[fila * 5 + 4] = M[4][fila];
            }
            return s;
        }
    }

    public static class Imagenes
    {
        // la envoltura de GDI+ en Skia: Clamp no repite nada (fuera, transparente: Decal); Tile repite; TileFlipX/Y/XY repiten en espejo
        internal static SKShaderTileMode Modo(Envoltura w, bool x)
        {
            if (w == Envoltura.Transparente) return SKShaderTileMode.Decal;
            bool espejo = w == Envoltura.Espejo || (x ? w == Envoltura.EspejoX : w == Envoltura.EspejoY);
            return espejo ? SKShaderTileMode.Mirror : SKShaderTileMode.Repeat;
        }
        // HighQualityBilinear, HighQualityBicubic y High de GDI+ "prefiltran" al achicar: una portada de 500 px achicada a 20 px sale
        // lisa (Form1.Vistas.cs: "sin que salga a granos"); un bilineal o un bicubico de Skia sin mas toma cuatro o dieciseis puntos y
        // sale a granos. Al achicar, esos modos van con mipmaps (cada nivel es la mitad, promediada). Bilinear y Bicubic no prefiltran,
        // como en GDI+.
        private static SKSamplingOptions Muestreo(SKCanvas C, Filtro f, SKRect origen, SKRect destino)
        {
            bool previo = f == Filtro.BilinealAlta || f == Filtro.BicubicaAlta || f == Filtro.Alta;
            if (previo)
            {
                SKMatrix m = C.TotalMatrix;
                float ex = (float)Math.Sqrt(m.ScaleX * m.ScaleX + m.SkewY * m.SkewY), ey = (float)Math.Sqrt(m.SkewX * m.SkewX + m.ScaleY * m.ScaleY);
                if (destino.Width * ex < origen.Width * 0.99f || destino.Height * ey < origen.Height * 0.99f)
                    return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            }
            switch (f)
            {
                case Filtro.Vecino: return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
                case Filtro.BicubicaAlta: case Filtro.Bicubico: case Filtro.Alta: return new SKSamplingOptions(SKCubicResampler.Mitchell);
                default: return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
            }
        }
        // DIBUJAR UNA IMAGEN COMO GDI+ (revision del 8-11-2026): el trozo 'origen' de 'img' en la caja 'destino' (en las coordenadas del
        // lienzo). 'matriz': la ColorMatrix de GDI+ (null: ninguna); 'claveBaja'/'claveAlta': SetColorKey (sin clave: null).
        //   - que punto de la imagen toca a cada pixel: con PixelOffsetMode de fabrica GDI+ pone los centros de los pixeles de la pantalla
        //     Y los de los puntos de la imagen en las coordenadas enteras (el centro del punto 0 cae en el borde de la caja); Skia, en el
        //     medio. Sin escalar es lo mismo; al estirar o achicar, la imagen de GDI+ va medio punto de la imagen hacia arriba y a la
        //     izquierda respecto a la de Skia (la neblina de 256x192 estirada a 1024x768, pixel y medio). Con Half o HighQuality, como Skia;
        //   - lo de fuera de la imagen, al interpolar en los bordes: lo que diga la envoltura (ImageAttributes.SetWrapMode); sin ella,
        //     WrapMode.Clamp, que en GDI+ es TRANSPARENTE: al estirar, el borde de abajo y el de la derecha se desvanecen (con Half, todos).
        //     Es lo que XP esquiva en las miniaturas con TileFlipXY (Form1.Portadas.cs: "el filtro mezclaba el borde con lo de fuera y lo
        //     dejaba semitransparente"). Skia repetia el borde. Dentro de la imagen se usan sus puntos, aunque esten fuera del trozo pedido;
        //   - los pixeles que cubre: los que tienen el centro dentro de la caja (con los centros en los enteros la caja va corrida medio
        //     pixel menos 1/32, para que una caja en coordenadas enteras cubra lo mismo que en Skia), sin suavizar el borde;
        //   - una copia exacta (el mismo tamano, en pixeles enteros) va punto por punto, sin filtro (sale igual y es mas rapida).
        public static void Pinta(SKCanvas C, SKBitmap img, SKRect origen, SKRect destino, Filtro filtro, bool medio = false, bool copia = false,
                                 Envoltura envoltura = Envoltura.Transparente, float[][] matriz = null, Color? claveBaja = null, Color? claveAlta = null)
        {
            if (img == null) return;
            if (origen.Width <= 0 || origen.Height <= 0 || destino.Width == 0 || destino.Height == 0) return;
            SKBitmap fuente = img, conClave = null;
            int zx = 0, zy = 0;   // donde empieza 'fuente' dentro de la imagen (la copia con la clave es un trozo)
            try
            {
                if (claveBaja.HasValue && claveAlta.HasValue)
                {
                    // la clave va antes que todo (como GDI+): en una copia del trozo que se pinta, con dos puntos alrededor (los que mira el filtro)
                    SKRectI o = SKRectI.Intersect(new SKRectI((int)Math.Floor(origen.Left) - 2, (int)Math.Floor(origen.Top) - 2, (int)Math.Ceiling(origen.Right) + 2, (int)Math.Ceiling(origen.Bottom) + 2), new SKRectI(0, 0, img.Width, img.Height));
                    if (o.Width <= 0 || o.Height <= 0) return;
                    conClave = ConClave(img, o, claveBaja.Value, claveAlta.Value);
                    fuente = conClave; zx = o.Left; zy = o.Top;
                }
                using (SKPaint p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill })
                using (SKImage si = SKImage.FromPixels(fuente.Info, fuente.GetPixels(), fuente.RowBytes))
                {
                    if (si == null) return;
                    if (copia) p.BlendMode = SKBlendMode.Src;
                    if (matriz != null) p.ColorFilter = SKColorFilter.CreateColorMatrix(MatrizColor.Skia(matriz));
                    SKMatrix ctm = C.TotalMatrix;
                    float kx = destino.Width / origen.Width, ky = destino.Height / origen.Height;
                    if (kx == 1f && ky == 1f && ctm.ScaleX == 1f && ctm.ScaleY == 1f && ctm.SkewX == 0f && ctm.SkewY == 0f && ctm.Persp0 == 0f && ctm.Persp1 == 0f
                        && Entero(destino.Left + ctm.TransX) && Entero(destino.Top + ctm.TransY) && Entero(origen.Left) && Entero(origen.Top))
                    {
                        C.DrawImage(si, new SKRect(origen.Left - zx, origen.Top - zy, origen.Right - zx, origen.Bottom - zy), destino, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), p);
                        return;
                    }
                    SKSamplingOptions muestreo = Muestreo(C, filtro, origen, destino);
                    SKShaderTileMode tx = Modo(envoltura, true), ty = Modo(envoltura, false);
                    // de la imagen a la caja de destino (en las coordenadas del dibujo), y del trozo a la imagen
                    SKMatrix d = SKMatrix.CreateTranslation(destino.Left - origen.Left * kx, destino.Top - origen.Top * ky).PreConcat(SKMatrix.CreateScale(kx, ky));
                    SKMatrix z = SKMatrix.CreateTranslation(zx, zy);
                    if (medio) { PintaImagen(C, si, fuente, tx, ty, muestreo, ctm, d.PreConcat(z), destino, p); return; }
                    // pantalla e imagen medio punto atras: del trozo a la pantalla = T(0,5) * ctm * d * T(-0,5) * z; y la caja, corrida
                    // medio pixel menos 1/32 en la pantalla (el lienzo se corre para pintarla, y el dibujo de la imagen se compensa)
                    SKMatrix total = SKMatrix.CreateTranslation(0.5f, 0.5f).PreConcat(ctm).PreConcat(d).PreConcat(SKMatrix.CreateTranslation(-0.5f, -0.5f)).PreConcat(z);
                    SKMatrix corrida = SKMatrix.CreateTranslation(15f / 32f, 15f / 32f).PreConcat(ctm), inv;
                    if (!corrida.TryInvert(out inv)) return;
                    int n = C.Save();
                    try { C.SetMatrix(in corrida); PintaImagen(C, si, fuente, tx, ty, muestreo, corrida, inv.PreConcat(total), destino, p); }
                    finally { C.RestoreToCount(n); }
                }
            }
            finally { if (conClave != null) conClave.Dispose(); }
        }
        // PINTA LA CAJA CON LA IMAGEN (local: de la imagen al lienzo; lienzo: la matriz del lienzo ahora). Lo transparente de fuera (Decal)
        // cuesta en Skia cinco veces mas que repetir el borde (Clamp): 12 ms en vez de 2,3 para estirar 256x192 a 1024x768. Como los dos
        // solo se distinguen donde el filtro toca fuera de la imagen, el centro se pinta con Clamp y solo la franja del borde con Decal
        // (dos recortes complementarios: cada pixel se pinta una vez, y sale igual que todo con Decal).
        private static void PintaImagen(SKCanvas C, SKImage si, SKBitmap fuente, SKShaderTileMode tx, SKShaderTileMode ty, SKSamplingOptions muestreo, SKMatrix lienzo, SKMatrix local, SKRect destino, SKPaint p)
        {
            SKRect caja = destino.Standardized;
            SKMatrix e = lienzo.PreConcat(local);   // de la imagen a la pantalla
            bool recto = e.SkewX == 0f && e.SkewY == 0f && e.Persp0 == 0f && e.Persp1 == 0f && lienzo.SkewX == 0f && lienzo.SkewY == 0f;
            if ((tx == SKShaderTileMode.Decal || ty == SKShaderTileMode.Decal) && recto)
            {
                // hasta donde mira el filtro alrededor de cada punto (en puntos de la imagen): bilineal 1, bicubico 2, mas lo que el punto
                // que se muestrea dista del que decide si el pixel se pinta (medio pixel de la pantalla, en puntos de la imagen), y con
                // mipmaps lo que mide un punto del nivel mas chico que se use
                float escala = Math.Min(Math.Abs(e.ScaleX), Math.Abs(e.ScaleY));
                float m = (muestreo.UseCubic ? 2.5f : 1.5f) + (escala > 0f ? 1f / escala : 0f);
                if (muestreo.Mipmap != SKMipmapMode.None && escala > 0f && escala < 1f) m *= 4f / escala;
                float ix0 = tx == SKShaderTileMode.Decal ? m : -1e6f, ix1 = tx == SKShaderTileMode.Decal ? fuente.Width - m : 1e6f;
                float iy0 = ty == SKShaderTileMode.Decal ? m : -1e6f, iy1 = ty == SKShaderTileMode.Decal ? fuente.Height - m : 1e6f;
                SKMatrix inv;
                if (ix1 > ix0 && iy1 > iy0 && lienzo.TryInvert(out inv))
                {
                    SKRect dentro = inv.MapRect(e.MapRect(new SKRect(ix0, iy0, ix1, iy1)));   // el centro, en las coordenadas del lienzo
                    SKShaderTileMode cx = tx == SKShaderTileMode.Decal ? SKShaderTileMode.Clamp : tx, cy = ty == SKShaderTileMode.Decal ? SKShaderTileMode.Clamp : ty;
                    int n = C.Save();
                    try { C.ClipRect(dentro, SKClipOperation.Intersect, false); using (SKShader sh = si.ToShader(cx, cy, muestreo, local)) { p.Shader = sh; C.DrawRect(caja, p); } }
                    finally { C.RestoreToCount(n); p.Shader = null; }
                    n = C.Save();
                    try { C.ClipRect(dentro, SKClipOperation.Difference, false); using (SKShader sh = si.ToShader(tx, ty, muestreo, local)) { p.Shader = sh; C.DrawRect(caja, p); } }
                    finally { C.RestoreToCount(n); p.Shader = null; }
                    return;
                }
            }
            using (SKShader sh = si.ToShader(tx, ty, muestreo, local)) { p.Shader = sh; C.DrawRect(caja, p); p.Shader = null; }
        }
        private static bool Entero(float v) { return Math.Abs(v - Math.Round(v)) < 1e-4; }
        private static unsafe SKBitmap ConClave(SKBitmap b, SKRectI o, Color bajo, Color alto)
        {
            SKBitmap t = new SKBitmap(new SKImageInfo(o.Width, o.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (SKCanvas c = new SKCanvas(t)) { c.Clear(SKColors.Transparent); using (SKPaint p = new SKPaint { BlendMode = SKBlendMode.Src }) c.DrawBitmap(b, o, new SKRect(0, 0, o.Width, o.Height), p); }
            byte* px = (byte*)t.GetPixels(); int paso = t.RowBytes;
            for (int y = 0; y < o.Height; y++)
            {
                byte* f = px + (long)y * paso;
                for (int x = 0; x < o.Width; x++, f += 4)
                {
                    int a = f[3]; if (a == 0) continue;
                    int bb = f[0], g = f[1], r = f[2];
                    if (a < 255) { bb = (bb * 255 + a / 2) / a; g = (g * 255 + a / 2) / a; r = (r * 255 + a / 2) / a; }   // sin premultiplicar, como lo ve GDI+
                    // con un punto de holgura: Skia, al mezclar un borde suavizado de un color sobre ese mismo color, redondea
                    // hacia abajo (49 -> 48) y GDI+ no; sin ella el fondo de la lista del disco (pintado dos veces) dejaba su
                    // primera fila y su primera columna sin quitar: una raya oscura encima de la cancion elegida
                    if (r >= bajo.R - 1 && r <= alto.R + 1 && g >= bajo.G - 1 && g <= alto.G + 1 && bb >= bajo.B - 1 && bb <= alto.B + 1) { f[0] = f[1] = f[2] = f[3] = 0; }
                }
            }
            return t;
        }

        // LEER UNA IMAGEN (Image.FromStream de XP): los puntos en BGRA premultiplicado y, como GDI+, el formato que dice el archivo: un JPEG (o
        // un PNG sin alfa) es de 24 bits sin alfa, un GIF de paleta, y lo que trae alfa de 32 bits con alfa (la rocola lo mira para guardar
        // sus miniaturas). Lo que no es una imagen lanza ArgumentException.
        public static SKBitmap Lee(byte[] datos, out FormatoImagen formato)
        {
            formato = FormatoImagen.Argb32;
            try
            {
                using (SKCodec codec = SKCodec.Create(new SKMemoryStream(datos)))
                    if (codec != null)
                    {
                        if (codec.EncodedFormat == SKEncodedImageFormat.Gif) formato = FormatoImagen.Paleta8;
                        else if (codec.Info.AlphaType == SKAlphaType.Opaque) formato = FormatoImagen.Rgb24;
                    }
            }
            catch { }
            using (SKBitmap b = SKBitmap.Decode(datos))
            {
                if (b == null) throw new ArgumentException("no es una imagen");
                SKBitmap c = new SKBitmap(new SKImageInfo(b.Width, b.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                using (SKCanvas cv = new SKCanvas(c)) { cv.Clear(SKColors.Transparent); cv.DrawBitmap(b, 0, 0); }
                return c;
            }
        }
        public static SKBitmap Lee(Stream s, out FormatoImagen formato)
        {
            using (MemoryStream ms = new MemoryStream()) { s.CopyTo(ms); return Lee(ms.ToArray(), out formato); }
        }
        // GUARDAR (Image.Save de XP): en JPEG con su calidad (75 la de fabrica del codificador de GDI+) o en PNG
        public static void GuardaJpeg(SKBitmap b, string ruta, int calidad = 75) { using (FileStream fs = File.Create(ruta)) Guarda(b, fs, true, calidad); }
        public static void GuardaPng(SKBitmap b, string ruta) { using (FileStream fs = File.Create(ruta)) Guarda(b, fs, false, 92); }
        public static void GuardaPng(SKBitmap b, Stream s) { Guarda(b, s, false, 92); }
        internal static void Guarda(SKBitmap b, Stream s, bool jpeg, int calidad)
        {
            using (SKImage i = SKImage.FromBitmap(b))
            using (SKData d = i.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, calidad)) d.SaveTo(s);
        }
    }
}

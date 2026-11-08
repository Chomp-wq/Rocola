// LOS PINCELES DE GDI+ COMO SHADERS DE SKIA, NATIVOS (port nativo, el dibujo; 8-11-2026): el degradado lineal (LinearGradientBrush), el de
// un trazo (PathGradientBrush) y la textura (TextureBrush) de XP. Lo que cuenta es la geometria de GDI+: el angulo y el modo escalable, que
// el lineal se repite (WrapMode.Tile de fabrica; en Skia hay que pedirlo), el Blend al reves en el de trazo y la textura que con Clamp es
// transparente y va medio punto atras. El shader va en el Shader de un SKPaint; Pixel y TextoGdiPlus le ponen el color opaco y lo
// compensan para que el color de cada pixel sea el de su centro, como GDI+.
// Los algoritmos son los de Plataforma/Dibujo2D.cs (revision del 8-11-2026), movidos aqui.
using System;
using System.Drawing;
using SkiaSharp;

namespace Rocola.Nativo
{
    public static class Degradado
    {
        // LinearGradientBrush(caja, a, b, LinearGradientMode): los modos son angulos escalables (Horizontal 0, Vertical 90, ForwardDiagonal 45 y
        // BackwardDiagonal 135; GdipCreateLineBrushFromRect)
        public const float Horizontal = 0f, Vertical = 90f, Diagonal = 45f, DiagonalInversa = 135f;

        // LinearGradientBrush(a, b, ca, cb): de un punto a otro, repetido fuera. GDI+ no lo hace de un punto a si mismo (OutOfMemory).
        public static SKShader Lineal(PointF a, PointF b, Color ca, Color cb)
        {
            Extremos(a, b);
            return LinealCon(a, b, new[] { ColorSk.Sk(ca), ColorSk.Sk(cb) }, null, SKShaderTileMode.Repeat);
        }
        // LinearGradientBrush(caja, ca, cb, angulo[, escalable]); con un modo: Lineal(caja, ca, cb, Degradado.Vertical, true)
        public static SKShader Lineal(RectangleF caja, Color ca, Color cb, float angulo, bool escalable = false)
        {
            PointF p0, p1; Extremos(caja, angulo, escalable, out p0, out p1);
            return LinealCon(p0, p1, new[] { ColorSk.Sk(ca), ColorSk.Sk(cb) }, null, SKShaderTileMode.Repeat);
        }
        // el mismo con InterpolationColors (los colores y sus posiciones de 0 a 1 en lugar de los dos de la caja)
        public static SKShader Lineal(RectangleF caja, Color[] colores, float[] posiciones, float angulo, bool escalable)
        {
            PointF p0, p1; Extremos(caja, angulo, escalable, out p0, out p1);
            SKColor[] cols = new SKColor[colores.Length]; for (int i = 0; i < cols.Length; i++) cols[i] = ColorSk.Sk(colores[i]);
            return LinealCon(p0, p1, cols, (float[])posiciones.Clone(), SKShaderTileMode.Repeat);
        }
        // (GDI+ no hace un degradado de un punto a si mismo: OutOfMemory, que .NET lanza)
        public static void Extremos(PointF a, PointF b) { if (a.X == b.X && a.Y == b.Y) throw new OutOfMemoryException("Out of memory."); }
        // GDI+ (GdipCreateLineBrushFromRectWithAngle): la linea del degradado pasa por el centro de la caja con ese angulo, y sus extremos son
        // la proyeccion de las esquinas; "escalable": tan(angulo) se multiplica por ancho/alto (las rayas de un color van de esquina a esquina
        // por la otra diagonal). En una caja sin ancho o sin alto, OutOfMemory.
        public static void Extremos(RectangleF r, float angulo, bool escalable, out PointF p0, out PointF p1)
        {
            if (r.Width == 0 || r.Height == 0) throw new OutOfMemoryException("Out of memory.");
            double a = angulo % 360.0; if (a < 0) a += 360.0;
            double t;
            if (escalable)
            {
                double mas = 0;
                while (a >= 90) { a -= 180; mas += Math.PI; }
                t = (a != 90 && a != -90) ? Math.Atan((r.Width / (double)r.Height) * Math.Tan(a * Math.PI / 180.0)) : a * Math.PI / 180.0;
                t += mas;
            }
            else t = a * Math.PI / 180.0;
            double cs = Math.Cos(t), sn = Math.Sin(t);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            double medio = (Math.Abs(r.Width * cs) + Math.Abs(r.Height * sn)) / 2.0;
            p0 = new PointF((float)(cx - cs * medio), (float)(cy - sn * medio)); p1 = new PointF((float)(cx + cs * medio), (float)(cy + sn * medio));
        }
        // el degradado de p0 a p1 con esos colores y posiciones (null: repartidos) y esa envoltura
        public static SKShader LinealCon(PointF p0, PointF p1, SKColor[] cols, float[] pos, SKShaderTileMode modo)
        {
            return SKShader.CreateLinearGradient(new SKPoint(p0.X, p0.Y), new SKPoint(p1.X, p1.Y), cols, pos, modo);
        }
        // el color a una parte 't' (0 a 1) del camino de a a b (lo que da un factor del Blend de GDI+)
        public static Color Mezcla(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb((int)Math.Round(a.A + (b.A - a.A) * t), (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        // EL DEGRADADO DE UN TRAZO (PathGradientBrush): del color del centro al de la orilla. La rocola lo usa con elipses (halos y brillos):
        // aqui es un degradado radial desde el centro hasta la orilla de la elipse, estirado a su forma. Con el centro corrido (CenterPoint
        // fuera del medio: la moneda que cae, la luna) GDI+ va en linea recta del centro a cada punto de la orilla, y la orilla entera lleva
        // el color de fuera: eso es un degradado conico de dos puntos (de radio 0 en el centro al circulo de la elipse), no un circulo
        // alrededor del centro corrido (que se pasaba por un lado y no llegaba a la orilla por el otro). 'caja' = los limites del trazo
        // (GraphicsPath.GetBounds); el centro de fabrica es el medio de la caja. 'factores' y 'posiciones' = el Blend de GDI+, de la orilla
        // (posicion 0) al centro (1): cuanto del color del centro hay a esa distancia (null: sin Blend).
        public static SKShader DeTrazo(RectangleF caja, PointF centro, Color colorCentro, Color colorOrilla, float[] factores = null, float[] posiciones = null)
        {
            Color fuera = colorOrilla, CenterColor = colorCentro; PointF CenterPoint = centro;
            float rx = Math.Max(0.5f, caja.Width / 2f), ry = Math.Max(0.5f, caja.Height / 2f);
            float ex = caja.X + caja.Width / 2f, ey = caja.Y + caja.Height / 2f;   // el medio de la elipse
            bool corrido = Math.Abs(CenterPoint.X - ex) > 0.01f || Math.Abs(CenterPoint.Y - ey) > 0.01f;
            SKMatrix m = corrido ? SKMatrix.CreateScale(1f, ry / rx, ex, ey) : SKMatrix.CreateScale(1f, ry / rx, CenterPoint.X, CenterPoint.Y);
            SKColor[] cols; float[] pos = null;
            if (factores != null && posiciones != null && factores.Length >= 2)
            {
                int n = factores.Length; cols = new SKColor[n]; pos = new float[n];
                for (int i = 0; i < n; i++)   // Skia va del centro (0) a la orilla (1): al reves que el Blend de GDI+
                {
                    int j = n - 1 - i;
                    pos[i] = 1f - posiciones[j];
                    cols[i] = ColorSk.Sk(Mezcla(fuera, CenterColor, factores[j]));
                }
            }
            else cols = new[] { ColorSk.Sk(CenterColor), ColorSk.Sk(fuera) };
            if (corrido)   // en el espacio del circulo (y estirada a rx alrededor del medio): del centro corrido al circulo entero
                return SKShader.CreateTwoPointConicalGradient(new SKPoint(CenterPoint.X, ey + (CenterPoint.Y - ey) * rx / ry), 0f,
                    new SKPoint(ex, ey), rx, cols, pos, SKShaderTileMode.Clamp, m);
            return SKShader.CreateRadialGradient(new SKPoint(CenterPoint.X, CenterPoint.Y), rx, cols, pos, SKShaderTileMode.Clamp, m);
        }

        // LA TEXTURA (TextureBrush(imagen, envoltura) y su TranslateTransform: 'matriz'). WrapMode.Clamp de GDI+ no repite nada: fuera de la
        // imagen no se pinta (transparente; en Skia es Decal, no Clamp, que estira el borde). Con los centros de los pixeles en las
        // coordenadas enteras (sin medio pixel: 'medio' falso), el punto de la textura que toca a cada pixel es el de su centro (la textura va
        // medio punto atras: asi una textura sin girar ni escalar cae punto por punto, como en GDI+). 'medio' tiene que ser el del dibujo en
        // que se usa.
        public static SKShader Textura(SKBitmap imagen, Envoltura envoltura, SKMatrix matriz, bool medio)
        {
            SKShaderTileMode tx = Imagenes.Modo(envoltura, true), ty = Imagenes.Modo(envoltura, false);
            SKMatrix m = !medio ? matriz.PreConcat(SKMatrix.CreateTranslation(-0.5f, -0.5f)) : matriz;
            return SKShader.CreateBitmap(imagen, tx, ty, m);
        }
    }
}

// LAS FORMAS COMO LAS PINTA GDI+, NATIVAS (port nativo, el dibujo; 8-11-2026). El codigo de XP traducido pinta con SKCanvas, SKPath y SKPaint
// directamente y llama a esto donde GDI+ hace las cuentas distinto que Skia: el centro de los pixeles, la pluma de un pixel como minimo, el
// limite de esquinas y el color del pincel en cada pixel. Sin estado de dibujo: lo que en XP era estado del Graphics llega como parametro
// ('suave' = SmoothingMode AntiAlias o HighQuality; 'medio' = PixelOffsetMode Half o HighQuality; 'copia' = CompositingMode.SourceCopy); la
// matriz y el recorte son los del SKCanvas. El pincel (SKPaint con su Color, o su Shader de Degradado) queda como estaba.
// Los algoritmos son los del Dibujo2D.cs de la capa de la transicion (ya borrada; sigue en la referencia) (revision del 8-11-2026), movidos aqui.
using System;
using System.Drawing;
using SkiaSharp;

namespace Rocola.Nativo
{
    public static class Pixel
    {
        // EL MEDIO PIXEL DE GDI+ AL SUAVIZAR (revision del 7-11-2026). Con PixelOffsetMode de fabrica (Default, HighSpeed o None) GDI+ pone
        // el centro de cada pixel en las coordenadas ENTERAS ("pixel centers have integer coordinates"); Skia, en el medio (i + 0.5). Con
        // suavizado eso se ve: en XP una raya de 1 en una coordenada entera sale nitida y el borde de un relleno en una entera sale a medias;
        // en Skia salia al reves (la raya del riel de las letras, en dos filas a medias). Las formas suavizadas se corren medio pixel en la
        // pantalla (despues de la transformacion, donde lo hace GDI+), como libgdiplus con Cairo. Con Half o HighQuality no. El texto no se
        // toca (las imagenes van aparte: Imagenes.Pinta).
        // Y SIN SUAVIZAR (revision del 8-11-2026): GDI+ pinta los pixeles cuyo centro (en los enteros) cae dentro de la forma, con las
        // coordenadas a dieciseisavos de pixel: un relleno desde x = 10,3 empieza en el pixel 11; Skia redondea y empezaba en el 10. Con las
        // formas sin suavizar el lienzo se corre 15/32 de pixel: Skia cubre entonces lo mismo que GDI+ (en las coordenadas enteras, lo mismo
        // que sin correr). Los pinceles se compensan (Compensa), para que su color siga siendo el del centro del pixel.
        // lo que se corre el lienzo, en pixeles de la pantalla: medio con suavizado, 15/32 sin el, nada con Half o HighQuality
        internal static float Corrimiento(bool suave, bool medio) { return medio ? 0f : suave ? 0.5f : 15f / 32f; }
        // corre el lienzo 'd' pixeles de la pantalla (devuelve la cuenta para volver; -1 si no hizo falta)
        internal static int Corre(SKCanvas c, float d)
        {
            if (d == 0f) return -1;
            int n = c.Save();
            SKMatrix m = SKMatrix.CreateTranslation(d, d).PreConcat(c.TotalMatrix);
            c.SetMatrix(in m);
            return n;
        }
        internal static void Vuelve(SKCanvas c, int n) { if (n >= 0) c.RestoreToCount(n); }
        // EL COLOR DE UN PINCEL (degradado, textura) EN CADA PIXEL ES EL DE SU CENTRO. En GDI+ (sin medio pixel) ese centro esta en las
        // coordenadas enteras; Skia lo toma en el medio (i + 0,5): un degradado salia medio pixel corrido. El lienzo ya va corrido 'corrido'
        // (Corre); lo que falte hasta medio pixel se corre aqui el dibujo del pincel, en la pantalla. Devuelve el shader nuevo (null: no hace
        // falta; el de antes vale tal cual).
        internal static SKShader Compensa(SKCanvas c, SKShader s, bool medio, float corrido)
        {
            if (s == null || medio) return null;
            float e = 0.5f - corrido;
            if (e == 0f) return null;
            SKMatrix ctm = c.TotalMatrix, inv;
            if (!ctm.TryInvert(out inv)) return null;
            return s.WithLocalMatrix(inv.PreConcat(SKMatrix.CreateTranslation(e, e)).PreConcat(ctm));
        }
        // pone en 'p' el color o el shader de 'pincel' como GDI+: con shader, el color opaco (que no lo tina) y el shader compensado
        internal static void ConPincel(SKCanvas c, SKPaint p, SKPaint pincel, bool medio, float corrido)
        {
            if (pincel.Shader == null) { p.Shader = null; p.Color = pincel.Color; return; }
            p.Color = SKColors.Black;
            SKShader s = Compensa(c, pincel.Shader, medio, corrido);
            p.Shader = s ?? pincel.Shader;
        }

        // ---- lo que se le pone al pincel para pintar y se le quita despues (el pincel del codigo queda como estaba)
        private struct Puesto
        {
            internal int Cuenta; internal bool Aa; internal SKPaintStyle Estilo; internal SKShader Shader, Nuevo; internal SKColor Color;
            internal float Ancho, Esquinas; internal SKBlendMode Mezcla;
        }
        private static Puesto Pon(SKCanvas c, SKPaint p, bool suave, bool medio, bool copia, bool trazo, float corrido)
        {
            Puesto x = new Puesto { Aa = p.IsAntialias, Estilo = p.Style, Shader = p.Shader, Color = p.Color, Ancho = p.StrokeWidth, Esquinas = p.StrokeMiter, Mezcla = p.BlendMode };
            x.Cuenta = Corre(c, corrido);
            p.IsAntialias = suave;
            p.Style = trazo ? SKPaintStyle.Stroke : SKPaintStyle.Fill;
            if (x.Shader != null) { p.Color = SKColors.Black; x.Nuevo = Compensa(c, x.Shader, medio, corrido); if (x.Nuevo != null) p.Shader = x.Nuevo; }
            if (trazo)
            {
                p.StrokeWidth = x.Ancho <= 1f ? Math.Max(x.Ancho, 1f) : x.Ancho;   // GDI+: una pluma de 0 o 1 es de un pixel
                p.StrokeMiter = 10f;   // el limite de las esquinas en punta de GDI+ (MiterLimit de fabrica); el de Skia es 4 y las achataba antes
            }
            if (copia) p.BlendMode = SKBlendMode.Src;
            return x;
        }
        private static void Quita(SKCanvas c, SKPaint p, ref Puesto x)
        {
            p.IsAntialias = x.Aa; p.Style = x.Estilo; p.Color = x.Color; p.StrokeWidth = x.Ancho; p.StrokeMiter = x.Esquinas; p.BlendMode = x.Mezcla;
            if (x.Nuevo != null) { p.Shader = x.Shader; x.Nuevo.Dispose(); }
            Vuelve(c, x.Cuenta);
        }
        private static SKRect R(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }

        // ---- rellenos (FillPath, FillRectangle, FillEllipse, FillPolygon, FillPie y FillRegion de XP)
        public static void Rellena(SKCanvas c, SKPath forma, SKPaint pincel, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pincel, suave, medio, copia, false, Corrimiento(suave, medio)); try { c.DrawPath(forma, pincel); } finally { Quita(c, pincel, ref x); } }
        public static void Rellena(SKCanvas c, RectangleF r, SKPaint pincel, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pincel, suave, medio, copia, false, Corrimiento(suave, medio)); try { c.DrawRect(R(r), pincel); } finally { Quita(c, pincel, ref x); } }
        public static void RellenaOvalo(SKCanvas c, RectangleF r, SKPaint pincel, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pincel, suave, medio, copia, false, Corrimiento(suave, medio)); try { c.DrawOval(R(r), pincel); } finally { Quita(c, pincel, ref x); } }
        // FillPolygon: 'alterno' = FillMode.Alternate (el de fabrica); con menos de tres puntos no pinta nada
        public static void RellenaPoligono(SKCanvas c, PointF[] puntos, SKPaint pincel, bool suave, bool alterno = true, bool medio = false, bool copia = false)
        {
            if (puntos == null || puntos.Length < 3) return;
            using (SKPath sp = Poligono(puntos, alterno)) Rellena(c, sp, pincel, suave, medio, copia);
        }
        public static void RellenaPorcion(SKCanvas c, RectangleF r, float inicio, float barrido, SKPaint pincel, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pincel, suave, medio, copia, false, Corrimiento(suave, medio)); try { c.DrawArc(R(r), inicio, barrido, true, pincel); } finally { Quita(c, pincel, ref x); } }
        // LAS REGIONES DE GDI+ SON DE PIXELES ENTEROS: FillRegion no se suaviza aunque SmoothingMode lo pida (la region se pasa a
        // rectangulos de pixeles, como el recorte). La luna de la cabecera de la atraccion (AtraccionGpu.cs, Luna) sale asi en XP.
        public static void RellenaRegion(SKCanvas c, SKPath region, SKPaint pincel, bool medio = false, bool copia = false)
        {
            float d = !medio ? 15f / 32f : 0f;
            Puesto x = Pon(c, pincel, false, medio, copia, false, d);
            try { c.DrawPath(region, pincel); } finally { Quita(c, pincel, ref x); }
        }

        // ---- trazos (DrawPath, DrawRectangle, DrawEllipse, DrawLine, DrawLines, DrawPolygon, DrawArc y DrawBezier de XP). La pluma es un
        // SKPaint con su Color (o Shader), su StrokeWidth y, si XP las pone, StrokeJoin y StrokeCap; aqui se le pone el minimo de un pixel y el
        // limite de esquinas 10 de GDI+.
        public static void Traza(SKCanvas c, SKPath forma, SKPaint pluma, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pluma, suave, medio, copia, true, Corrimiento(suave, medio)); try { c.DrawPath(forma, pluma); } finally { Quita(c, pluma, ref x); } }
        public static void TrazaRect(SKCanvas c, RectangleF r, SKPaint pluma, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pluma, suave, medio, copia, true, Corrimiento(suave, medio)); try { c.DrawRect(R(r), pluma); } finally { Quita(c, pluma, ref x); } }
        public static void TrazaOvalo(SKCanvas c, RectangleF r, SKPaint pluma, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pluma, suave, medio, copia, true, Corrimiento(suave, medio)); try { c.DrawOval(R(r), pluma); } finally { Quita(c, pluma, ref x); } }
        public static void Linea(SKCanvas c, float x1, float y1, float x2, float y2, SKPaint pluma, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pluma, suave, medio, copia, true, Corrimiento(suave, medio)); try { c.DrawLine(x1, y1, x2, y2, pluma); } finally { Quita(c, pluma, ref x); } }
        // DrawLines: los puntos unidos (sin cerrar); con menos de dos no pinta nada
        public static void Lineas(SKCanvas c, PointF[] puntos, SKPaint pluma, bool suave, bool medio = false, bool copia = false)
        {
            if (puntos == null || puntos.Length < 2) return;
            using (SKPath sp = new SKPath())
            {
                sp.MoveTo(puntos[0].X, puntos[0].Y); for (int i = 1; i < puntos.Length; i++) sp.LineTo(puntos[i].X, puntos[i].Y);
                Traza(c, sp, pluma, suave, medio, copia);
            }
        }
        // DrawPolygon: los puntos unidos y cerrados; con menos de dos no pinta nada
        public static void TrazaPoligono(SKCanvas c, PointF[] puntos, SKPaint pluma, bool suave, bool medio = false, bool copia = false)
        {
            if (puntos == null || puntos.Length < 2) return;
            using (SKPath sp = Poligono(puntos, true)) Traza(c, sp, pluma, suave, medio, copia);
        }
        public static void Arco(SKCanvas c, RectangleF r, float inicio, float barrido, SKPaint pluma, bool suave, bool medio = false, bool copia = false)
        { Puesto x = Pon(c, pluma, suave, medio, copia, true, Corrimiento(suave, medio)); try { c.DrawArc(R(r), inicio, barrido, false, pluma); } finally { Quita(c, pluma, ref x); } }
        public static void Bezier(SKCanvas c, PointF a, PointF b, PointF c2, PointF d, SKPaint pluma, bool suave, bool medio = false, bool copia = false)
        {
            using (SKPath sp = new SKPath()) { sp.MoveTo(a.X, a.Y); sp.CubicTo(b.X, b.Y, c2.X, c2.Y, d.X, d.Y); Traza(c, sp, pluma, suave, medio, copia); }
        }
        // el poligono cerrado de FillPolygon y DrawPolygon (alterno = FillMode.Alternate, EvenOdd; si no, Winding)
        public static SKPath Poligono(PointF[] ps, bool alterno)
        {
            SKPath sp = new SKPath { FillType = alterno ? SKPathFillType.EvenOdd : SKPathFillType.Winding };
            sp.MoveTo(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) sp.LineTo(ps[i].X, ps[i].Y); sp.Close();
            return sp;
        }
    }
}

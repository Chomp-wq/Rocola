// EL DIBUJO DE LA ROCOLA DE XP, NATIVO EN LINUX (traduccion del programa de XP, fase 1; decision del dueno del 7-11-2026). El programa de
// XP pinta con GDI+ (System.Drawing de Windows). Aqui estan las mismas operaciones que el usa -las de System.Drawing que aparecen en su
// codigo, ni una mas-, hechas con SkiaSharp (la libreria de dibujo de Chrome y Android): asi cada archivo de XP se traduce cambiando lo
// minimo y se puede comparar linea por linea con el original. No es Windows debajo: es dibujo nativo de Linux con el comportamiento de
// GDI+ que la rocola necesita (sobre todo el del texto: el margen de 1/6 de letra, el alto del renglon de las tablas de la letra, el
// corte con puntos suspensivos y el ajuste a los pixeles).
//
// Los tipos sencillos (Color, Rectangle, RectangleF, Point, PointF, Size, SizeF) son los de .NET (System.Drawing.Primitives, que si
// existen en Linux). Los demas viven aqui, en Rocola.Dibujo2D.
//
// PORT NATIVO (paso D2 del dibujo, 8-11-2026): las reglas de GDI+ y de GDI ya NO viven aqui sino en las funciones nativas (Xp/Nativo:
// Letras, TextoGdiPlus, TextoGdi, Pixel, Degradado, Imagenes, Pixeles, RecorteLienzo, Mapas), que llama tanto esta capa (mientras dure la
// traduccion) como el codigo de XP ya traducido a SkiaSharp directo: cada regla en un solo sitio. Esto queda como una capa fina que pasa
// las llamadas de la API de GDI+ a esas funciones, y se borra al final de la traduccion.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola.Dibujo2D
{
    // ---------------------------------------------------------------- los modos (los mismos nombres y valores que GDI+)
    public enum SmoothingMode { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, AntiAlias = 4 }
    public enum TextRenderingHint { SystemDefault = 0, SingleBitPerPixelGridFit = 1, SingleBitPerPixel = 2, AntiAliasGridFit = 3, AntiAlias = 4, ClearTypeGridFit = 5 }
    public enum CompositingMode { SourceOver = 0, SourceCopy = 1 }
    public enum CompositingQuality { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, GammaCorrected = 3, AssumeLinear = 4 }
    public enum InterpolationMode { Invalid = -1, Default = 0, Low = 1, High = 2, Bilinear = 3, Bicubic = 4, NearestNeighbor = 5, HighQualityBilinear = 6, HighQualityBicubic = 7 }
    public enum PixelOffsetMode { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, Half = 4 }
    public enum WrapMode { Tile = 0, TileFlipX = 1, TileFlipY = 2, TileFlipXY = 3, Clamp = 4 }
    public enum LinearGradientMode { Horizontal = 0, Vertical = 1, ForwardDiagonal = 2, BackwardDiagonal = 3 }
    public enum DashStyle { Solid = 0, Dash = 1, Dot = 2, DashDot = 3, DashDotDot = 4, Custom = 5 }
    public enum LineCap { Flat = 0, Square = 1, Round = 2, Triangle = 3, NoAnchor = 16, SquareAnchor = 17, RoundAnchor = 18, DiamondAnchor = 19, ArrowAnchor = 20 }
    public enum LineJoin { Miter = 0, Bevel = 1, Round = 2, MiterClipped = 3 }
    public enum PenAlignment { Center = 0, Inset = 1, Outset = 2, Left = 3, Right = 4 }
    public enum FillMode { Alternate = 0, Winding = 1 }
    public enum CombineMode { Replace = 0, Intersect = 1, Union = 2, Xor = 3, Exclude = 4, Complement = 5 }
    public enum GraphicsUnit { World = 0, Display = 1, Pixel = 2, Point = 3, Inch = 4, Document = 5, Millimeter = 6 }
    public enum PixelFormat { Indexed = 65536, Alpha = 262144, PAlpha = 524288, Format8bppIndexed = 198659, Format24bppRgb = 137224, Format32bppRgb = 139273, Format32bppArgb = 2498570, Format32bppPArgb = 925707 }
    [Flags] public enum ImageFlags { None = 0, Scalable = 1, HasAlpha = 2, HasTranslucent = 4, PartiallyScalable = 8, ColorSpaceRgb = 16, ColorSpaceCmyk = 32, ColorSpaceGray = 64, ColorSpaceYcbcr = 128, ColorSpaceYcck = 256, HasRealDpi = 4096, HasRealPixelSize = 8192, ReadOnly = 65536, Caching = 131072 }
    public enum ColorMatrixFlag { Default = 0, SkipGrays = 1, AltGrays = 2 }
    public enum ColorAdjustType { Default = 0, Bitmap = 1, Brush = 2, Pen = 3, Text = 4, Count = 5, Any = 6 }
    public enum ImageLockMode { ReadOnly = 1, WriteOnly = 2, ReadWrite = 3, UserInputBuffer = 4 }
    [Flags] public enum FontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
    public enum StringAlignment { Near = 0, Center = 1, Far = 2 }
    [Flags] public enum StringFormatFlags { DirectionRightToLeft = 1, DirectionVertical = 2, FitBlackBox = 4, DisplayFormatControl = 32, NoFontFallback = 1024, MeasureTrailingSpaces = 2048, NoWrap = 4096, LineLimit = 8192, NoClip = 16384 }
    public enum StringTrimming { None = 0, Character = 1, Word = 2, EllipsisCharacter = 3, EllipsisWord = 4, EllipsisPath = 5 }
    public enum HotkeyPrefix { None = 0, Show = 1, Hide = 2 }
    [Flags] public enum TextFormatFlags { Default = 0, Left = 0, Top = 0, HorizontalCenter = 1, Right = 2, VerticalCenter = 4, Bottom = 8, WordBreak = 16, SingleLine = 32, ExpandTabs = 64, NoClipping = 256, ExternalLeading = 512, NoPrefix = 2048, Internal = 4096, TextBoxControl = 8192, PathEllipsis = 16384, EndEllipsis = 32768, ModifyString = 65536, RightToLeft = 131072, WordEllipsis = 262144, NoFullWidthCharacterBreak = 524288, HidePrefix = 1048576, PrefixOnly = 2097152, PreserveGraphicsClipping = 16777216, PreserveGraphicsTranslateTransform = 33554432, NoPadding = 268435456, LeftAndRightPadding = 536870912 }

    // ---------------------------------------------------------------- las letras
    public enum GenericFontFamilies { Serif = 0, SansSerif = 1, Monospace = 2 }
    public sealed class FontFamily : IDisposable
    {
        public readonly string Name;
        // COMO GDI+: pedir por su nombre una familia que no esta instalada lanza ArgumentException. La rocola de XP prueba asi Segoe UI,
        // Tahoma y Verdana para Ctrl+F3 (FamiliaUi): en Windows XP no hay Segoe UI y queda Tahoma. Instalada = sus letras estan en la
        // carpeta de las letras de XP (Letras.Instalada); sin esa carpeta no hay ninguna y queda la generica, como antes.
        public FontFamily(string nombre) { Name = Letras.Familia(nombre); }
        internal FontFamily(string nombre, bool sinMirar) { Name = nombre; }
        public FontFamily(GenericFontFamilies g) { Name = g == GenericFontFamilies.Serif ? Letras.ConRemates : g == GenericFontFamilies.Monospace ? Letras.AnchoFijo : Letras.SinRemates; }
        public void Dispose() { }
        // En Windows XP la generica sin remates es Microsoft Sans Serif; la de ancho fijo, Courier New; la de remates, Times New Roman.
        public static FontFamily GenericSansSerif { get { return new FontFamily(Letras.SinRemates, true); } }
        public static FontFamily GenericMonospace { get { return new FontFamily(Letras.AnchoFijo, true); } }
        public static FontFamily GenericSerif { get { return new FontFamily(Letras.ConRemates, true); } }
        // new Font("una que no esta", ...) no lanza: GDI+ pone Microsoft Sans Serif (la generica) en su lugar
        internal static FontFamily DeFont(string nombre) { return new FontFamily(Letras.Resuelve(nombre), true); }
        public override string ToString() { return Name; }
    }

    public sealed class Font : IDisposable
    {
        public readonly FontFamily FontFamily;
        public readonly float Size;          // en la unidad de Unit (de fabrica, puntos)
        public readonly FontStyle Style;
        public readonly GraphicsUnit Unit;
        internal readonly Letra L;           // la letra nativa (Nativo/Letras.cs): su cara, sus medidas y su SKFont por modo de letra
        public Font(string familia, float tam) : this(FontFamily.DeFont(familia), tam, FontStyle.Regular, GraphicsUnit.Point) { }
        public Font(string familia, float tam, FontStyle estilo) : this(FontFamily.DeFont(familia), tam, estilo, GraphicsUnit.Point) { }
        public Font(string familia, float tam, FontStyle estilo, GraphicsUnit unidad) : this(FontFamily.DeFont(familia), tam, estilo, unidad) { }
        public Font(FontFamily familia, float tam) : this(familia, tam, FontStyle.Regular, GraphicsUnit.Point) { }
        public Font(FontFamily familia, float tam, FontStyle estilo) : this(familia, tam, estilo, GraphicsUnit.Point) { }
        public Font(FontFamily familia, float tam, GraphicsUnit unidad) : this(familia, tam, FontStyle.Regular, unidad) { }
        public Font(Font f, FontStyle estilo) : this(f.FontFamily, f.Size, estilo, f.Unit) { }
        public Font(FontFamily familia, float tam, FontStyle estilo, GraphicsUnit unidad)
        {
            FontFamily = familia ?? FontFamily.GenericSansSerif; Size = tam; Style = estilo; Unit = unidad;
            bool b = (estilo & FontStyle.Bold) != 0, i = (estilo & FontStyle.Italic) != 0, u = (estilo & FontStyle.Underline) != 0;
            // el tamano en pixeles (a 96 puntos por pulgada, como la rocola en XP): en pixeles los que XP da asi; en puntos los de fabrica;
            // las demas unidades (XP no las usa) pasadas a pixeles
            switch (unidad)
            {
                case GraphicsUnit.Pixel: case GraphicsUnit.World: case GraphicsUnit.Display: L = Letra.EnPixeles(FontFamily.Name, tam, b, i, u); break;
                case GraphicsUnit.Inch: L = Letra.EnPixeles(FontFamily.Name, tam * 96f, b, i, u); break;
                case GraphicsUnit.Document: L = Letra.EnPixeles(FontFamily.Name, tam * 96f / 300f, b, i, u); break;
                case GraphicsUnit.Millimeter: L = Letra.EnPixeles(FontFamily.Name, tam * 96f / 25.4f, b, i, u); break;
                default: L = Letra.EnPuntos(FontFamily.Name, tam, b, i, u); break;   // puntos
            }
        }
        public string Name { get { return FontFamily.Name; } }
        public bool Bold { get { return (Style & FontStyle.Bold) != 0; } }
        public bool Italic { get { return (Style & FontStyle.Italic) != 0; } }
        public bool Underline { get { return (Style & FontStyle.Underline) != 0; } }
        public float SizeInPixels { get { return L.Px; } }
        // (.NET: con la unidad en puntos devuelve Size tal cual; si no, el alto en pixeles pasado a puntos a 96 por pulgada)
        public float SizeInPoints { get { return L.Puntos; } }
        // el alto del renglon en pixeles (GetHeight de GDI+) y el mismo redondeado hacia arriba (Font.Height)
        public float GetHeight() { return L.AltoRenglon; }
        public float GetHeight(Graphics g) { return GetHeight(); }
        public int Height { get { return L.AltoEntero; } }
        public void Dispose() { L.Dispose(); }
    }

    // ---------------------------------------------------------------- pinceles y plumas
    public abstract class Brush : IDisposable
    {
        // el color o el shader del pincel en 'p' (Nativo/Degradado.cs); centros: el dibujo va con los centros de los pixeles en las
        // coordenadas enteras (PixelOffsetMode de fabrica, None o HighSpeed). Pixel y TextoGdiPlus le ponen el resto, como GDI+.
        internal abstract void Pon(SKPaint p, bool centros);
        public virtual void Dispose() { }
        public abstract Brush Clone();
    }
    public sealed class SolidBrush : Brush
    {
        public Color Color;
        public SolidBrush(Color c) { Color = c; }
        internal override void Pon(SKPaint p, bool centros) { p.Shader = null; p.Color = Color.Sk(); }
        public override Brush Clone() { return new SolidBrush(Color); }
    }
    public sealed class Blend { public float[] Factors, Positions; public Blend() { } public Blend(int n) { Factors = new float[n]; Positions = new float[n]; } }
    public sealed class ColorBlend { public Color[] Colors; public float[] Positions; public ColorBlend() { } public ColorBlend(int n) { Colors = new Color[n]; Positions = new float[n]; } }
    public sealed class LinearGradientBrush : Brush
    {
        private PointF p0, p1; private Color c0, c1;
        public ColorBlend InterpolationColors;
        public Blend Blend;
        public WrapMode WrapMode = WrapMode.Tile;
        public bool GammaCorrection;
        private RectangleF caja;
        // (GDI+ no hace un degradado de un punto a si mismo ni en una caja sin ancho o sin alto: OutOfMemory, que .NET lanza)
        public LinearGradientBrush(PointF a, PointF b, Color ca, Color cb)
        {
            Degradado.Extremos(a, b);   // (GDI+ lanza OutOfMemory con un punto a si mismo)
            p0 = a; p1 = b; c0 = ca; c1 = cb; caja = RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }
        public LinearGradientBrush(Point a, Point b, Color ca, Color cb) : this((PointF)a, (PointF)b, ca, cb) { }
        // LOS MODOS SON ANGULOS ESCALABLES (Degradado.Horizontal, Vertical, Diagonal y DiagonalInversa)
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, LinearGradientMode m)
            : this(r, ca, cb, m == LinearGradientMode.Vertical ? Degradado.Vertical : m == LinearGradientMode.ForwardDiagonal ? Degradado.Diagonal : m == LinearGradientMode.BackwardDiagonal ? Degradado.DiagonalInversa : Degradado.Horizontal, true) { }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, LinearGradientMode m) : this((RectangleF)r, ca, cb, m) { }
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, float angulo) : this(r, ca, cb, angulo, false) { }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, float angulo) : this((RectangleF)r, ca, cb, angulo, false) { }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, float angulo, bool escalable) : this((RectangleF)r, ca, cb, angulo, escalable) { }
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, float angulo, bool escalable)
        {
            Degradado.Extremos(r, angulo, escalable, out p0, out p1);   // (y OutOfMemory en una caja sin ancho o sin alto)
            caja = r; c0 = ca; c1 = cb;
        }
        public Color[] LinearColors { get { return new[] { c0, c1 }; } set { c0 = value[0]; c1 = value[1]; } }
        public RectangleF Rectangle { get { return caja; } }
        internal override void Pon(SKPaint p, bool centros)
        {
            SKColor[] cols; float[] pos;
            if (InterpolationColors != null && InterpolationColors.Colors != null && InterpolationColors.Colors.Length >= 2)
            {
                cols = new SKColor[InterpolationColors.Colors.Length]; for (int i = 0; i < cols.Length; i++) cols[i] = InterpolationColors.Colors[i].Sk();
                pos = (float[])InterpolationColors.Positions.Clone();
            }
            else if (Blend != null && Blend.Factors != null && Blend.Factors.Length >= 2)
            {
                cols = new SKColor[Blend.Factors.Length]; pos = (float[])Blend.Positions.Clone();
                for (int i = 0; i < cols.Length; i++) cols[i] = Degradado.Mezcla(c0, c1, Blend.Factors[i]).Sk();
            }
            else { cols = new[] { c0.Sk(), c1.Sk() }; pos = null; }
            SKShaderTileMode tm = WrapMode == WrapMode.Clamp ? SKShaderTileMode.Clamp : WrapMode == WrapMode.Tile ? SKShaderTileMode.Repeat : SKShaderTileMode.Mirror;
            p.Shader = Degradado.LinealCon(p0, p1, cols, pos, tm);
            p.Color = SKColors.Black;
        }
        public override Brush Clone() { LinearGradientBrush b = (LinearGradientBrush)MemberwiseClone(); return b; }
    }
    // EL DEGRADADO DE UN TRAZO (PathGradientBrush): del color del centro al de la orilla. La rocola lo usa con elipses (halos y brillos):
    // aqui es un degradado radial desde el centro hasta la orilla de la elipse, estirado a su forma. Con el centro corrido (CenterPoint
    // fuera del medio: la moneda que cae, la luna) GDI+ va en linea recta del centro a cada punto de la orilla, y la orilla entera lleva
    // el color de fuera: eso es un degradado conico de dos puntos (de radio 0 en el centro al circulo de la elipse), no un circulo
    // alrededor del centro corrido (que se pasaba por un lado y no llegaba a la orilla por el otro).
    public sealed class PathGradientBrush : Brush
    {
        private readonly RectangleF caja;
        public Color CenterColor = Color.White;
        public Blend Blend;   // de la orilla (posicion 0) al centro (1): cuanto del color del centro hay a esa distancia
        public Color[] SurroundColors = { Color.White };
        public PointF CenterPoint;
        public WrapMode WrapMode = WrapMode.Clamp;
        public PathGradientBrush(GraphicsPath path) { caja = path.GetBounds(); CenterPoint = new PointF(caja.X + caja.Width / 2f, caja.Y + caja.Height / 2f); }
        internal override void Pon(SKPaint p, bool centros)
        {
            Color fuera = SurroundColors != null && SurroundColors.Length > 0 ? SurroundColors[0] : Color.Transparent;
            p.Shader = Degradado.DeTrazo(caja, CenterPoint, CenterColor, fuera, Blend != null ? Blend.Factors : null, Blend != null ? Blend.Positions : null);
            p.Color = SKColors.Black;
        }
        public override Brush Clone() { return (PathGradientBrush)MemberwiseClone(); }
    }
    public sealed class TextureBrush : Brush
    {
        private readonly Image img; public WrapMode WrapMode;
        private SKMatrix mat = SKMatrix.Identity;
        public TextureBrush(Image i) : this(i, WrapMode.Tile) { }
        public TextureBrush(Image i, WrapMode w) { img = i; WrapMode = w; }
        public void TranslateTransform(float dx, float dy) { mat = mat.PreConcat(SKMatrix.CreateTranslation(dx, dy)); }
        public void ResetTransform() { mat = SKMatrix.Identity; }
        // WrapMode.Clamp de GDI+ no repite nada: fuera de la imagen no se pinta (transparente; en Skia es Decal, no Clamp, que estira el
        // borde). Con los centros de los pixeles en las coordenadas enteras, el punto de la textura que toca a cada pixel es el de su
        // centro (la textura va medio punto atras: asi una textura sin girar ni escalar cae punto por punto, como en GDI+).
        internal override void Pon(SKPaint p, bool centros)
        {
            p.Shader = Degradado.Textura(img.Sk, (Envoltura)(int)WrapMode, mat, !centros);
            p.Color = SKColors.Black;
        }
        public override Brush Clone() { return (TextureBrush)MemberwiseClone(); }
    }
    public static class Brushes
    {
        public static Brush White { get { return new SolidBrush(Color.White); } }
        public static Brush Black { get { return new SolidBrush(Color.Black); } }
        public static Brush Transparent { get { return new SolidBrush(Color.Transparent); } }
    }
    public sealed class Pen : IDisposable
    {
        public Color Color; public float Width;
        public DashStyle DashStyle = DashStyle.Solid;
        public LineJoin LineJoin = LineJoin.Miter;
        public LineCap StartCap = LineCap.Flat, EndCap = LineCap.Flat;
        public PenAlignment Alignment = PenAlignment.Center;
        public float[] DashPattern;
        public Brush Brush;
        public Pen(Color c) : this(c, 1f) { }
        public Pen(Color c, float w) { Color = c; Width = w; }
        public Pen(Brush b, float w) { Brush = b; Width = w; SolidBrush s = b as SolidBrush; Color = s != null ? s.Color : Color.White; }
        public void SetLineCap(LineCap a, LineCap b, int dash) { StartCap = a; EndCap = b; }
        public void Dispose() { }
        // la pluma en 'p' (Nativo/Pixel.cs, Traza y los demas, le ponen el minimo de un pixel, el limite de esquinas 10 y el suavizado)
        internal void Pon(SKPaint p, bool centros)
        {
            if (Brush != null) Brush.Pon(p, centros); else { p.Shader = null; p.Color = Color.Sk(); }
            p.StrokeWidth = Width;
            p.StrokeJoin = LineJoin == LineJoin.Round ? SKStrokeJoin.Round : LineJoin == LineJoin.Bevel ? SKStrokeJoin.Bevel : SKStrokeJoin.Miter;
            p.StrokeCap = StartCap == LineCap.Round || EndCap == LineCap.Round ? SKStrokeCap.Round : StartCap == LineCap.Square || EndCap == LineCap.Square ? SKStrokeCap.Square : SKStrokeCap.Butt;
            float w = Math.Max(1f, Width);
            switch (DashStyle)
            {
                case DashStyle.Dash: p.PathEffect = SKPathEffect.CreateDash(new[] { 3 * w, w }, 0); break;
                case DashStyle.Dot: p.PathEffect = SKPathEffect.CreateDash(new[] { w, w }, 0); break;
                case DashStyle.DashDot: p.PathEffect = SKPathEffect.CreateDash(new[] { 3 * w, w, w, w }, 0); break;
                case DashStyle.Custom: if (DashPattern != null && DashPattern.Length >= 2) { float[] d = new float[DashPattern.Length]; for (int i = 0; i < d.Length; i++) d[i] = DashPattern[i] * w; p.PathEffect = SKPathEffect.CreateDash(d, 0); } break;
                default: p.PathEffect = null; break;
            }
        }
    }

    // ---------------------------------------------------------------- los trazos
    public sealed class GraphicsPath : IDisposable
    {
        internal SKPath P = new SKPath();
        private bool figuraNueva = true;
        public FillMode FillMode { get { return P.FillType == SKPathFillType.EvenOdd ? FillMode.Alternate : FillMode.Winding; } set { P.FillType = value == FillMode.Alternate ? SKPathFillType.EvenOdd : SKPathFillType.Winding; } }
        public GraphicsPath() { P.FillType = SKPathFillType.EvenOdd; }   // GDI+: Alternate de fabrica
        public GraphicsPath(FillMode m) : this() { FillMode = m; }
        private void Hasta(float x, float y) { if (figuraNueva || P.PointCount == 0) { P.MoveTo(x, y); figuraNueva = false; } else P.LineTo(x, y); }
        public void AddLine(float x1, float y1, float x2, float y2) { Hasta(x1, y1); P.LineTo(x2, y2); }
        public void AddLine(int x1, int y1, int x2, int y2) { AddLine((float)x1, y1, x2, y2); }
        public void AddLine(PointF a, PointF b) { AddLine(a.X, a.Y, b.X, b.Y); }
        public void AddLine(Point a, Point b) { AddLine(a.X, a.Y, b.X, b.Y); }
        public void AddLines(PointF[] ps) { if (ps == null || ps.Length == 0) return; Hasta(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) P.LineTo(ps[i].X, ps[i].Y); }
        public void AddLines(Point[] ps) { if (ps == null || ps.Length == 0) return; Hasta(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) P.LineTo(ps[i].X, ps[i].Y); }
        // GDI+: el arco se une con una linea al punto anterior de la figura (si lo hay)
        public void AddArc(float x, float y, float w, float h, float inicio, float barrido)
        {
            SKRect r = new SKRect(x, y, x + w, y + h);
            if (figuraNueva || P.PointCount == 0) { P.ArcTo(r, inicio, barrido, true); figuraNueva = false; }
            else P.ArcTo(r, inicio, barrido, false);
        }
        public void AddArc(RectangleF r, float inicio, float barrido) { AddArc(r.X, r.Y, r.Width, r.Height, inicio, barrido); }
        public void AddArc(Rectangle r, float inicio, float barrido) { AddArc(r.X, r.Y, r.Width, r.Height, inicio, barrido); }
        public void AddArc(int x, int y, int w, int h, float inicio, float barrido) { AddArc((float)x, y, w, h, inicio, barrido); }
        public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) { Hasta(x1, y1); P.CubicTo(x2, y2, x3, y3, x4, y4); }
        public void AddBezier(PointF a, PointF b, PointF c, PointF d) { AddBezier(a.X, a.Y, b.X, b.Y, c.X, c.Y, d.X, d.Y); }
        public void AddRectangle(RectangleF r) { P.AddRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom)); figuraNueva = true; }
        public void AddRectangle(Rectangle r) { AddRectangle((RectangleF)r); }
        public void AddEllipse(float x, float y, float w, float h) { P.AddOval(new SKRect(x, y, x + w, y + h)); figuraNueva = true; }
        public void AddEllipse(RectangleF r) { AddEllipse(r.X, r.Y, r.Width, r.Height); }
        public void AddEllipse(Rectangle r) { AddEllipse(r.X, r.Y, r.Width, r.Height); }
        public void AddEllipse(int x, int y, int w, int h) { AddEllipse((float)x, y, w, h); }
        public void AddPolygon(PointF[] ps) { if (ps == null || ps.Length < 2) return; P.MoveTo(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) P.LineTo(ps[i].X, ps[i].Y); P.Close(); figuraNueva = true; }
        public void AddPolygon(Point[] ps) { if (ps == null || ps.Length < 2) return; P.MoveTo(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) P.LineTo(ps[i].X, ps[i].Y); P.Close(); figuraNueva = true; }
        public void AddPie(float x, float y, float w, float h, float inicio, float barrido) { SKRect r = new SKRect(x, y, x + w, y + h); P.MoveTo(r.MidX, r.MidY); P.ArcTo(r, inicio, barrido, false); P.Close(); figuraNueva = true; }
        public void AddPath(GraphicsPath o, bool unir) { P.AddPath(o.P, unir ? SKPathAddMode.Extend : SKPathAddMode.Append); figuraNueva = !unir; }
        // EL TEXTO HECHO TRAZO (la interrogacion de la ayuda): la letra a ese tamano en pixeles, acomodada en la caja como DrawString
        public void AddString(string s, FontFamily fam, int estilo, float emPx, RectangleF caja, StringFormat sf)
        {
            using (Font f = new Font(fam, emPx, (FontStyle)estilo, GraphicsUnit.Pixel))
                TextoGdiPlus.Trazo(P, s, f.L, caja, sf != null ? sf.F : null);
            figuraNueva = true;
        }
        public void CloseFigure() { P.Close(); figuraNueva = true; }
        public void CloseAllFigures() { P.Close(); figuraNueva = true; }
        public void StartFigure() { figuraNueva = true; }
        public void Reset() { P.Reset(); figuraNueva = true; }
        public RectangleF GetBounds() { SKRect b = P.Bounds; return RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom); }
        public void Transform(Matrix m) { P.Transform(m.M); }
        public bool IsVisible(float x, float y) { return P.Contains(x, y); }
        public bool IsVisible(PointF p) { return P.Contains(p.X, p.Y); }
        public int PointCount { get { return P.PointCount; } }
        public object Clone() { GraphicsPath g = new GraphicsPath(); g.P = new SKPath(P); g.figuraNueva = figuraNueva; return g; }
        public void Dispose() { P.Dispose(); }
    }
    public sealed class Matrix : IDisposable
    {
        internal SKMatrix M = SKMatrix.Identity;
        public Matrix() { }
        public Matrix(float m11, float m12, float m21, float m22, float dx, float dy) { M = new SKMatrix(m11, m21, dx, m12, m22, dy, 0, 0, 1); }
        public void Translate(float dx, float dy) { M = M.PreConcat(SKMatrix.CreateTranslation(dx, dy)); }
        public void Scale(float sx, float sy) { M = M.PreConcat(SKMatrix.CreateScale(sx, sy)); }
        public void Rotate(float grados) { M = M.PreConcat(SKMatrix.CreateRotationDegrees(grados)); }
        public void RotateAt(float grados, PointF c) { M = M.PreConcat(SKMatrix.CreateRotationDegrees(grados, c.X, c.Y)); }
        public void TransformPoints(PointF[] pts) { if (pts == null) return; for (int i = 0; i < pts.Length; i++) { SKPoint q = M.MapPoint(pts[i].X, pts[i].Y); pts[i] = new PointF(q.X, q.Y); } }
        public void TransformPoints(Point[] pts) { if (pts == null) return; for (int i = 0; i < pts.Length; i++) { SKPoint q = M.MapPoint(pts[i].X, pts[i].Y); pts[i] = new Point((int)Math.Round(q.X), (int)Math.Round(q.Y)); } }
        public void Dispose() { }
    }
    public sealed class Region : IDisposable
    {
        internal SKPath P;
        public Region(GraphicsPath gp) { P = new SKPath(gp.P); }
        public Region(RectangleF r) { P = new SKPath(); P.AddRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom)); }
        public void Exclude(GraphicsPath gp) { SKPath r = P.Op(gp.P, SKPathOp.Difference); if (r != null) P = r; }
        public void Exclude(RectangleF rc) { using (SKPath o = new SKPath()) { o.AddRect(new SKRect(rc.Left, rc.Top, rc.Right, rc.Bottom)); SKPath r = P.Op(o, SKPathOp.Difference); if (r != null) P = r; } }
        public void Intersect(GraphicsPath gp) { SKPath r = P.Op(gp.P, SKPathOp.Intersect); if (r != null) P = r; }
        public void Union(GraphicsPath gp) { SKPath r = P.Op(gp.P, SKPathOp.Union); if (r != null) P = r; }
        public void Dispose() { P.Dispose(); }
    }

    // ---------------------------------------------------------------- el formato del texto
    public sealed class StringFormat : IDisposable
    {
        // EL FORMATO DE VERDAD ES UN FormatoTexto (Nativo/TextoGdiPlus.cs): esto solo le pone los nombres de GDI+. Las banderas van tal cual
        // (los mismos valores): FormatFlags = X deja solo X y |= X la anade, como en GDI+.
        internal readonly FormatoTexto F;
        public StringAlignment Alignment { get { return (StringAlignment)F.Horizontal; } set { F.Horizontal = (Alinea)value; } }
        public StringAlignment LineAlignment { get { return (StringAlignment)F.Vertical; } set { F.Vertical = (Alinea)value; } }
        public StringFormatFlags FormatFlags { get { return (StringFormatFlags)F.Banderas; } set { F.Banderas = (BanderasTexto)value; } }
        public StringTrimming Trimming { get { return (StringTrimming)F.Corte; } set { F.Corte = (CorteTexto)value; } }
        public HotkeyPrefix HotkeyPrefix = HotkeyPrefix.None;   // (el texto de GDI+ de la rocola no lo usa)
        internal bool Tipografico { get { return F.SinMargen; } }   // GenericTypographic (y sus copias): sin el margen de 1/6 de letra
        public StringFormat() { F = new FormatoTexto(); }
        public StringFormat(StringFormatFlags f) { F = new FormatoTexto((BanderasTexto)f); }
        public StringFormat(StringFormat o) { F = new FormatoTexto(o.F); HotkeyPrefix = o.HotkeyPrefix; }
        internal StringFormat(FormatoTexto f) { F = f; }   // (PuenteDibujo: el mismo FormatoTexto, no una copia)
        // LOS DOS FORMATOS GENERICOS SON UN SOLO OBJETO CADA UNO, COMPARTIDO (revision del 8-11-2026; ver FormatoTexto): el StringFormat de
        // GenericTypographic envuelve a FormatoTexto.TipograficoCompartido, asi que lo que le cambie un archivo con GDI+ lo ve uno ya traducido
        // que use FormatoTexto.TipograficoCompartido, y al reves (mientras unos archivos esten traducidos y otros no).
        private static readonly StringFormat genericoNormal = new StringFormat(FormatoTexto.NormalCompartido);
        private static readonly StringFormat genericoTipografico = new StringFormat(FormatoTexto.TipograficoCompartido);
        public static StringFormat GenericDefault { get { return genericoNormal; } }
        public static StringFormat GenericTypographic { get { return genericoTipografico; } }
        // el de DrawString y MeasureString sin formato (null): el de fabrica de GDI+, aparte del generico (que el codigo puede cambiar)
        internal static readonly StringFormat SinFormato = new StringFormat(FormatoTexto.SinFormato);
        public object Clone() { return new StringFormat(this); }
        public void Dispose() { }
    }

    // ---------------------------------------------------------------- las imagenes
    public sealed class ColorMatrix
    {
        public readonly float[][] M;
        public ColorMatrix() { M = new float[5][]; for (int i = 0; i < 5; i++) { M[i] = new float[5]; M[i][i] = 1f; } }
        public ColorMatrix(float[][] m) { M = new float[5][]; for (int i = 0; i < 5; i++) { M[i] = new float[5]; for (int j = 0; j < 5; j++) M[i][j] = m[i][j]; } }
        public float this[int fila, int col] { get { return M[fila][col]; } set { M[fila][col] = value; } }
        public float Matrix33 { get { return M[3][3]; } set { M[3][3] = value; } }
        public float Matrix00 { get { return M[0][0]; } set { M[0][0] = value; } }
        public float Matrix11 { get { return M[1][1]; } set { M[1][1] = value; } }
        public float Matrix22 { get { return M[2][2]; } set { M[2][2] = value; } }
        public float Matrix40 { get { return M[4][0]; } set { M[4][0] = value; } }
        public float Matrix41 { get { return M[4][1]; } set { M[4][1] = value; } }
        public float Matrix42 { get { return M[4][2]; } set { M[4][2] = value; } }
        // (su paso a Skia: Nativo/Imagenes.cs, MatrizColor.Skia)
    }
    public sealed class ImageAttributes : IDisposable
    {
        internal ColorMatrix Matriz; internal WrapMode Envoltura = WrapMode.Clamp;
        public void SetColorMatrix(ColorMatrix m) { Matriz = m; }
        public void SetColorMatrix(ColorMatrix m, ColorMatrixFlag flags, ColorAdjustType tipo) { Matriz = m; }
        public void SetWrapMode(WrapMode w) { Envoltura = w; }
        // la clave de color de GDI+: los puntos cuyo rojo, verde y azul caen entre los de 'bajo' y 'alto' quedan transparentes
        internal bool HayClave; internal Color ClaveBaja, ClaveAlta;
        public void SetColorKey(Color bajo, Color alto) { HayClave = true; ClaveBaja = bajo; ClaveAlta = alto; }
        public void SetColorKey(Color bajo, Color alto, ColorAdjustType tipo) { SetColorKey(bajo, alto); }
        public void ClearColorKey() { HayClave = false; }
        public void ClearColorKey(ColorAdjustType tipo) { HayClave = false; }
        public void Dispose() { }
    }
    public sealed class BitmapData { public IntPtr Scan0; public int Stride, Width, Height; public PixelFormat PixelFormat; internal bool directo; internal IntPtr memoria; internal ImageLockMode modo; internal Rectangle zona; }
    public sealed class ImageFormat { public readonly string N; private ImageFormat(string n) { N = n; } public static readonly ImageFormat Png = new ImageFormat("png"), Jpeg = new ImageFormat("jpeg"), Bmp = new ImageFormat("bmp"); }
    // LOS CODIFICADORES DE IMAGEN DE GDI+ (las miniaturas en JPEG con su calidad): el de JPEG y el de PNG, con Skia
    public sealed class ImageCodecInfo
    {
        public string MimeType { get; private set; }
        public string FormatDescription { get; private set; }
        internal ImageFormat Formato;
        private ImageCodecInfo() { }
        public static ImageCodecInfo[] GetImageEncoders()
        {
            return new[] { new ImageCodecInfo { MimeType = "image/jpeg", FormatDescription = "JPEG", Formato = ImageFormat.Jpeg }, new ImageCodecInfo { MimeType = "image/png", FormatDescription = "PNG", Formato = ImageFormat.Png } };
        }
    }
    public sealed class Encoder { public readonly string Nombre; private Encoder(string n) { Nombre = n; } public static readonly Encoder Quality = new Encoder("Quality"); }
    public sealed class EncoderParameter : IDisposable
    {
        public readonly Encoder Encoder; public readonly long Valor;
        public EncoderParameter(Encoder encoder, long value) { Encoder = encoder; Valor = value; }
        public void Dispose() { }
    }
    public sealed class EncoderParameters : IDisposable
    {
        public EncoderParameter[] Param;
        public EncoderParameters(int n) { Param = new EncoderParameter[n]; }
        public void Dispose() { }
    }

    public abstract class Image : IDisposable
    {
        internal SKBitmap Sk;   // los puntos (BGRA premultiplicado, como Format32bppPArgb)
        public int Width { get { return Sk.Width; } }
        public int Height { get { return Sk.Height; } }
        public Size Size { get { return new Size(Sk.Width, Sk.Height); } }
        public PixelFormat PixelFormat { get; protected set; }
        // ImageFlags de GDI+: si la imagen trae canal alfa (las que se leen de un archivo: lo dice su formato; las hechas aqui, si tienen alfa)
        public int Flags { get { return TraeAlfa ? (int)ImageFlags.HasAlpha : 0; } }
        internal bool TraeAlfa = true;
        public static bool IsAlphaPixelFormat(PixelFormat f) { return (f & PixelFormat.Alpha) != 0 || (f & PixelFormat.PAlpha) != 0; }
        public object Tag;
        public static Image FromStream(Stream s)
        {
            using (MemoryStream ms = new MemoryStream()) { s.CopyTo(ms); return Bitmap.Decodifica(ms.ToArray()); }
        }
        public static Image FromStream(Stream s, bool usarColor, bool validar) { return FromStream(s); }
        public static Image FromFile(string ruta) { return Bitmap.Decodifica(File.ReadAllBytes(ruta)); }
        internal bool Ajeno;   // (PuenteDibujo) los puntos son de otro: al soltar la imagen no se sueltan
        public virtual void Dispose() { if (Sk != null) { if (!Ajeno) Sk.Dispose(); Sk = null; } }
        public void Save(string ruta) { Save(ruta, ruta.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png); }
        public void Save(string ruta, ImageFormat f) { using (FileStream fs = File.Create(ruta)) Save(fs, f); }
        public void Save(Stream s, ImageFormat f) { Guarda(s, f, 92); }
        public void Save(string ruta, ImageCodecInfo codec, EncoderParameters ep)
        {
            int calidad = 75;   // la de fabrica del codificador JPEG de GDI+
            if (ep != null && ep.Param != null) foreach (EncoderParameter p in ep.Param) if (p != null && p.Encoder == Encoder.Quality) calidad = (int)Math.Max(0, Math.Min(100, p.Valor));
            using (FileStream fs = File.Create(ruta)) Guarda(fs, codec != null ? codec.Formato : ImageFormat.Png, calidad);
        }
        private void Guarda(Stream s, ImageFormat f, int calidad) { Imagenes.Guarda(Sk, s, f == ImageFormat.Jpeg, calidad); }
    }
    public sealed class Bitmap : Image
    {
        public Bitmap(int w, int h) : this(w, h, PixelFormat.Format32bppArgb) { }
        public Bitmap(int w, int h, PixelFormat f)
        {
            Sk = Mapas.Nuevo(w, h); PixelFormat = f; TraeAlfa = IsAlphaPixelFormat(f);
        }
        public Bitmap(Image origen) : this(origen, origen.Width, origen.Height) { }
        public Bitmap(Image origen, Size s) : this(origen, s.Width, s.Height) { }
        public Bitmap(Image origen, int w, int h) : this(w, h, PixelFormat.Format32bppArgb)
        {
            // (.NET: Graphics.FromImage, Clear(Transparent) y DrawImage con lo de fabrica, Bilinear; no HighQualityBicubic)
            using (Graphics g = Graphics.FromImage(this)) { g.Clear(Color.Transparent); g.DrawImage(origen, 0, 0, w, h); }
        }
        internal Bitmap(SKBitmap b, PixelFormat f) { Sk = b; PixelFormat = f; }
        // LEER UNA IMAGEN: los puntos en BGRA premultiplicado y, como GDI+, el formato que dice el archivo: un JPEG (o un PNG sin alfa) es
        // de 24 bits sin alfa, un GIF de paleta, y lo que trae alfa de 32 bits con alfa (la rocola lo mira para guardar sus miniaturas)
        internal static Bitmap Decodifica(byte[] datos)
        {
            FormatoImagen fa;
            SKBitmap c = Imagenes.Lee(datos, out fa);
            PixelFormat f = fa == FormatoImagen.Paleta8 ? PixelFormat.Format8bppIndexed : fa == FormatoImagen.Rgb24 ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
            Bitmap r = new Bitmap(c, f); r.TraeAlfa = fa == FormatoImagen.Argb32;
            return r;
        }
        public Bitmap Clone(Rectangle r, PixelFormat f) { return new Bitmap(Mapas.Copia(Sk, r), f); }
        public object Clone() { return Clone(new Rectangle(0, 0, Width, Height), PixelFormat); }
        public Color GetPixel(int x, int y) { SKColor c = Sk.GetPixel(x, y); return Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue); }
        public void SetPixel(int x, int y, Color c) { Sk.SetPixel(x, y, c.Sk()); }
        // LOS PUNTOS PARA LEERLOS O ESCRIBIRLOS (LockBits). En Format32bppPArgb, que es como se guardan aqui, el puntero a los puntos mismos
        // desde la esquina del trozo y con el paso de la imagen (como GDI+ con su propio formato); en los demas, una copia convertida fuera
        // del monton de .NET (Nativo/Pixeles.cs), que se devuelve a la imagen al soltarla si era para escribir.
        private static FormatoPuntos Puntos(PixelFormat f) { return f == PixelFormat.Format24bppRgb ? FormatoPuntos.Rgb24 : f == PixelFormat.Format32bppRgb ? FormatoPuntos.Rgb32 : FormatoPuntos.Argb32; }
        public unsafe BitmapData LockBits(Rectangle r, ImageLockMode modo, PixelFormat f)
        {
            BitmapData d = new BitmapData { Width = r.Width, Height = r.Height, PixelFormat = f, modo = modo, zona = r };
            int rb = Sk.RowBytes; byte* o = (byte*)Sk.GetPixels();
            if (f == PixelFormat.Format32bppPArgb) { d.Stride = rb; d.Scan0 = (IntPtr)(o + (long)r.Y * rb + (long)r.X * 4); d.directo = true; return d; }
            FormatoPuntos fp = Puntos(f);
            int stride = Pixeles.Paso(fp, r.Width);
            d.Stride = stride; d.memoria = (IntPtr)NativeMemory.Alloc((nuint)Math.Max(1L, (long)stride * r.Height)); d.Scan0 = d.memoria;
            Pixeles.Lee(Sk, r, fp, d.memoria, stride);
            return d;
        }
        public unsafe void UnlockBits(BitmapData d)
        {
            if (d == null) return;
            if (d.directo) { if ((d.modo & ImageLockMode.WriteOnly) != 0) Sk.NotifyPixelsChanged(); d.directo = false; return; }
            if (d.memoria == IntPtr.Zero) return;
            if ((d.modo & ImageLockMode.WriteOnly) != 0) Pixeles.Escribe(Sk, d.zona, Puntos(d.PixelFormat), d.memoria, d.Stride);
            NativeMemory.Free((void*)d.memoria); d.memoria = IntPtr.Zero;
        }
    }

    // ---------------------------------------------------------------- el lienzo (Graphics)
    public sealed class GraphicsState { internal int Cuenta; internal SmoothingMode S; internal TextRenderingHint T; internal CompositingMode C; internal InterpolationMode I; internal PixelOffsetMode P; internal RectangleF? Clip; internal SKMatrix Base; }

    public sealed class Graphics : IDisposable
    {
        internal readonly SKCanvas C;
        private readonly bool propio;
        public SmoothingMode SmoothingMode = SmoothingMode.None;
        public TextRenderingHint TextRenderingHint = TextRenderingHint.SystemDefault;
        public CompositingMode CompositingMode = CompositingMode.SourceOver;
        public CompositingQuality CompositingQuality = CompositingQuality.Default;
        public InterpolationMode InterpolationMode = InterpolationMode.Bilinear;
        public PixelOffsetMode PixelOffsetMode = PixelOffsetMode.Default;
        public float DpiX { get { return 96f; } }
        public float DpiY { get { return 96f; } }
        private int baseCuenta;      // la cuenta de Skia antes del recorte de SetClip (para poder cambiarlo)
        private bool conRecorte;
        private RectangleF? recorte;  // el recorte de SetClip en coordenadas del dibujo (para ClipBounds)
        // LA MATRIZ DEL HDC: la de los pixeles de verdad donde pinta GDI (TextRenderer), sin la traslacion que el codigo le pone despues al
        // Graphics. En un mapa de bits, ninguna; en un control que pinta Ventanas.Escritorio, la de su sitio (SellaRecorte).
        private SKMatrix baseMatriz;
        internal Graphics(SKCanvas c, bool suyo) { C = c; propio = suyo; baseCuenta = c.SaveCount; baseMatriz = c.TotalMatrix; }
        // (PuenteDibujo) un lienzo con la matriz del HDC dada: la del sitio donde pinta GDI, que puede no ser la que lleva ahora el lienzo
        internal Graphics(SKCanvas c, SKMatrix hdc) { C = c; propio = false; baseCuenta = c.SaveCount; baseMatriz = hdc; }
        public static Graphics FromImage(Image i) { return new Graphics(new SKCanvas(i.Sk), true); }
        public void Dispose() { if (propio) { C.Flush(); C.Dispose(); } }
        public void Flush() { C.Flush(); }

        internal static SKColor Sk(Color c) { return c.Sk(); }
        private bool Suave { get { return SmoothingMode == SmoothingMode.AntiAlias || SmoothingMode == SmoothingMode.HighQuality; } }
        // los centros de los pixeles en las coordenadas enteras: PixelOffsetMode de fabrica (Default, HighSpeed o None); con Half o
        // HighQuality, en el medio, como en Skia (el 'medio' de las funciones nativas)
        private bool Centros { get { return PixelOffsetMode != PixelOffsetMode.Half && PixelOffsetMode != PixelOffsetMode.HighQuality; } }
        private bool Medio { get { return !Centros; } }
        private bool Copia { get { return CompositingMode == CompositingMode.SourceCopy; } }
        // EL PINCEL Y LA PLUMA DE GDI+ EN UN SKPaint (solo su color o su shader y, la pluma, su ancho, esquinas, puntas y guiones): lo que
        // GDI+ hace distinto que Skia al pintar con ellos (el medio pixel, el color en el centro del pixel, el minimo de un pixel, el limite
        // de esquinas, la copia) lo ponen las funciones nativas (Nativo/Pixel.cs, TextoGdiPlus.cs)
        private SKPaint Pincel(Brush b) { SKPaint p = new SKPaint(); b.Pon(p, Centros); return p; }
        private SKPaint Pluma(Pen pen) { SKPaint p = new SKPaint(); pen.Pon(p, Centros); return p; }
        private static void Suelta(SKPaint p) { SKShader s = p.Shader; p.Shader = null; if (s != null) s.Dispose(); SKPathEffect e = p.PathEffect; p.PathEffect = null; if (e != null) e.Dispose(); p.Dispose(); }
        private static SKRect R(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }

        public void Clear(Color c) { C.Clear(Sk(c)); }

        // --- el estado
        public GraphicsState Save()
        {
            GraphicsState s = new GraphicsState { Cuenta = C.Save(), S = SmoothingMode, T = TextRenderingHint, C = CompositingMode, I = InterpolationMode, P = PixelOffsetMode, Clip = recorte, Base = baseMatriz };
            return s;
        }
        public void Restore(GraphicsState s)
        {
            if (s == null) return;
            C.RestoreToCount(s.Cuenta);
            SmoothingMode = s.S; TextRenderingHint = s.T; CompositingMode = s.C; InterpolationMode = s.I; PixelOffsetMode = s.P; recorte = s.Clip; baseMatriz = s.Base;
            conRecorte = false; baseCuenta = C.SaveCount;
        }
        public void TranslateTransform(float dx, float dy) { C.Translate(dx, dy); }
        public void ScaleTransform(float sx, float sy) { C.Scale(sx, sy); }
        public void RotateTransform(float grados) { C.RotateDegrees(grados); }
        // LA TRANSFORMACION DEL CODIGO VA SOBRE LA DEL HDC: en WinForms cada control pinta en su propio HDC, con el origen en su esquina,
        // y dentro de OnPaint Graphics.Transform empieza en la identidad. Aqui el control lleva su sitio en la matriz de la base
        // (SellaRecorte): Transform la devuelve y la pone sin ella, y ResetTransform vuelve a ella (no al origen del escritorio).
        public void ResetTransform() { C.SetMatrix(in baseMatriz); }
        public Matrix Transform
        {
            get { SKMatrix inv; return new Matrix { M = baseMatriz.TryInvert(out inv) ? inv.PreConcat(C.TotalMatrix) : C.TotalMatrix }; }
            set { SKMatrix m = baseMatriz.PreConcat(value.M); C.SetMatrix(in m); }
        }

        // --- el recorte (SetClip cambia el de antes; Intersect, Exclude y los demas lo combinan). Las regiones de recorte de GDI+ son
        // siempre de pixeles enteros, sin suavizar, aunque el dibujo vaya suavizado (una portadita recortada en redondo sale con las
        // esquinas en escalera en XP): aqui igual.
        public void SetClip(RectangleF r) { SetClip(r, CombineMode.Replace); }
        public void SetClip(Rectangle r) { SetClip((RectangleF)r, CombineMode.Replace); }
        public void SetClip(Rectangle r, CombineMode m) { SetClip((RectangleF)r, m); }
        public void SetClip(RectangleF r, CombineMode m)
        {
            if (m == CombineMode.Replace) QuitaRecorte();
            if (!conRecorte) { baseCuenta = C.Save(); conRecorte = true; }
            if (m == CombineMode.Exclude) C.ClipRect(R(r), SKClipOperation.Difference, false);
            else C.ClipRect(R(r), SKClipOperation.Intersect, false);
            recorte = r;
        }
        public void SetClip(GraphicsPath gp) { SetClip(gp, CombineMode.Replace); }
        public void SetClip(GraphicsPath gp, CombineMode m)
        {
            if (m == CombineMode.Replace) QuitaRecorte();
            if (!conRecorte) { baseCuenta = C.Save(); conRecorte = true; }
            C.ClipPath(gp.P, m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, false);
            recorte = gp.GetBounds();
        }
        public void SetClip(Region rg, CombineMode m)
        {
            if (m == CombineMode.Replace) QuitaRecorte();
            if (!conRecorte) { baseCuenta = C.Save(); conRecorte = true; }
            C.ClipPath(rg.P, m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, false);
        }
        // (Ventanas.Escritorio) el recorte de ahora pasa a ser la base, como la region de la ventana de un control en Windows: lo que el
        // control haga despues con SetClip o ResetClip se queda dentro de el
        internal void SellaRecorte() { conRecorte = false; recorte = null; baseCuenta = C.SaveCount; baseMatriz = C.TotalMatrix; }
        public void IntersectClip(RectangleF r) { SetClip(r, CombineMode.Intersect); }
        public void IntersectClip(Rectangle r) { SetClip((RectangleF)r, CombineMode.Intersect); }
        public void ExcludeClip(Rectangle r) { SetClip((RectangleF)r, CombineMode.Exclude); }
        public void ResetClip() { QuitaRecorte(); }
        private void QuitaRecorte()
        {
            if (!conRecorte) return;
            RecorteLienzo.Quita(C, baseCuenta);   // (el recorte se va; la posicion se queda)
            conRecorte = false; recorte = null;
        }
        // LOS LIMITES DEL RECORTE, EXACTOS (RecorteLienzo.Limites)
        public RectangleF ClipBounds { get { return RecorteLienzo.Limites(C); } }
        public RectangleF VisibleClipBounds { get { return ClipBounds; } }

        // --- formas (Nativo/Pixel.cs)
        public void FillRectangle(Brush b, RectangleF r) { SKPaint p = Pincel(b); try { Pixel.Rellena(C, r, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void FillRectangle(Brush b, Rectangle r) { FillRectangle(b, (RectangleF)r); }
        public void FillRectangle(Brush b, float x, float y, float w, float h) { FillRectangle(b, new RectangleF(x, y, w, h)); }
        public void FillRectangle(Brush b, int x, int y, int w, int h) { FillRectangle(b, new RectangleF(x, y, w, h)); }
        public void FillRectangles(Brush b, RectangleF[] rs) { foreach (RectangleF r in rs) FillRectangle(b, r); }
        public void DrawRectangle(Pen pen, RectangleF r) { SKPaint p = Pluma(pen); try { Pixel.TrazaRect(C, r, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void DrawRectangle(Pen pen, Rectangle r) { DrawRectangle(pen, (RectangleF)r); }
        public void DrawRectangle(Pen pen, float x, float y, float w, float h) { DrawRectangle(pen, new RectangleF(x, y, w, h)); }
        public void DrawRectangle(Pen pen, int x, int y, int w, int h) { DrawRectangle(pen, new RectangleF(x, y, w, h)); }
        public void FillEllipse(Brush b, RectangleF r) { SKPaint p = Pincel(b); try { Pixel.RellenaOvalo(C, r, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void FillEllipse(Brush b, Rectangle r) { FillEllipse(b, (RectangleF)r); }
        public void FillEllipse(Brush b, float x, float y, float w, float h) { FillEllipse(b, new RectangleF(x, y, w, h)); }
        public void FillEllipse(Brush b, int x, int y, int w, int h) { FillEllipse(b, new RectangleF(x, y, w, h)); }
        public void DrawEllipse(Pen pen, RectangleF r) { SKPaint p = Pluma(pen); try { Pixel.TrazaOvalo(C, r, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void DrawEllipse(Pen pen, Rectangle r) { DrawEllipse(pen, (RectangleF)r); }
        public void DrawEllipse(Pen pen, float x, float y, float w, float h) { DrawEllipse(pen, new RectangleF(x, y, w, h)); }
        public void DrawEllipse(Pen pen, int x, int y, int w, int h) { DrawEllipse(pen, new RectangleF(x, y, w, h)); }
        public void FillPath(Brush b, GraphicsPath gp) { SKPaint p = Pincel(b); try { Pixel.Rellena(C, gp.P, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void DrawPath(Pen pen, GraphicsPath gp) { SKPaint p = Pluma(pen); try { Pixel.Traza(C, gp.P, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void FillRegion(Brush b, Region rg) { SKPaint p = Pincel(b); try { Pixel.RellenaRegion(C, rg.P, p, Medio, Copia); } finally { Suelta(p); } }
        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2) { SKPaint p = Pluma(pen); try { Pixel.Linea(C, x1, y1, x2, y2, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) { DrawLine(pen, (float)x1, y1, x2, y2); }
        public void DrawLine(Pen pen, PointF a, PointF b) { DrawLine(pen, a.X, a.Y, b.X, b.Y); }
        public void DrawLine(Pen pen, Point a, Point b) { DrawLine(pen, a.X, a.Y, b.X, b.Y); }
        public void DrawLines(Pen pen, PointF[] ps) { if (ps == null || ps.Length < 2) return; SKPaint p = Pluma(pen); try { Pixel.Lineas(C, ps, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void DrawLines(Pen pen, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; DrawLines(pen, f); }
        public void FillPolygon(Brush b, PointF[] ps) { FillPolygon(b, ps, FillMode.Alternate); }
        public void FillPolygon(Brush b, PointF[] ps, FillMode m) { if (ps == null || ps.Length < 3) return; SKPaint p = Pincel(b); try { Pixel.RellenaPoligono(C, ps, p, Suave, m == FillMode.Alternate, Medio, Copia); } finally { Suelta(p); } }
        public void FillPolygon(Brush b, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; FillPolygon(b, f); }
        public void DrawPolygon(Pen pen, PointF[] ps) { if (ps == null || ps.Length < 2) return; SKPaint p = Pluma(pen); try { Pixel.TrazaPoligono(C, ps, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void DrawPolygon(Pen pen, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; DrawPolygon(pen, f); }
        public void DrawArc(Pen pen, RectangleF r, float inicio, float barrido) { SKPaint p = Pluma(pen); try { Pixel.Arco(C, r, inicio, barrido, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void DrawArc(Pen pen, Rectangle r, float inicio, float barrido) { DrawArc(pen, (RectangleF)r, inicio, barrido); }
        public void DrawArc(Pen pen, float x, float y, float w, float h, float inicio, float barrido) { DrawArc(pen, new RectangleF(x, y, w, h), inicio, barrido); }
        public void FillPie(Brush b, RectangleF r, float inicio, float barrido) { SKPaint p = Pincel(b); try { Pixel.RellenaPorcion(C, r, inicio, barrido, p, Suave, Medio, Copia); } finally { Suelta(p); } }
        public void FillPie(Brush b, float x, float y, float w, float h, float inicio, float barrido) { FillPie(b, new RectangleF(x, y, w, h), inicio, barrido); }
        public void DrawBezier(Pen pen, PointF a, PointF b, PointF c, PointF d) { SKPaint p = Pluma(pen); try { Pixel.Bezier(C, a, b, c, d, p, Suave, Medio, Copia); } finally { Suelta(p); } }

        // --- imagenes (Nativo/Imagenes.cs, Pinta: el DrawImage de GDI+ con el filtro, el medio pixel, la copia y los ImageAttributes)
        private void Imagen(Image img, SKRect origen, SKRect destino, ImageAttributes ia)
        {
            if (img == null || img.Sk == null) return;
            Imagenes.Pinta(C, img.Sk, origen, destino, (Filtro)(int)InterpolationMode, Medio, Copia,
                ia != null ? (Envoltura)(int)ia.Envoltura : Envoltura.Transparente, ia != null && ia.Matriz != null ? ia.Matriz.M : null,
                ia != null && ia.HayClave ? ia.ClaveBaja : (Color?)null, ia != null && ia.HayClave ? ia.ClaveAlta : (Color?)null);
        }
        public void DrawImage(Image img, int x, int y) { if (img != null) Imagen(img, new SKRect(0, 0, img.Width, img.Height), new SKRect(x, y, x + img.Width, y + img.Height), null); }
        public void DrawImage(Image img, float x, float y) { if (img != null) Imagen(img, new SKRect(0, 0, img.Width, img.Height), new SKRect(x, y, x + img.Width, y + img.Height), null); }
        public void DrawImage(Image img, Point p) { DrawImage(img, p.X, p.Y); }
        public void DrawImage(Image img, PointF p) { DrawImage(img, p.X, p.Y); }
        public void DrawImageUnscaled(Image img, int x, int y) { DrawImage(img, x, y); }
        public void DrawImageUnscaled(Image img, Point p) { DrawImage(img, p.X, p.Y); }
        public void DrawImage(Image img, Rectangle d) { DrawImage(img, (RectangleF)d); }
        public void DrawImage(Image img, RectangleF d) { if (img != null) Imagen(img, new SKRect(0, 0, img.Width, img.Height), R(d), null); }
        public void DrawImage(Image img, float x, float y, float w, float h) { DrawImage(img, new RectangleF(x, y, w, h)); }
        public void DrawImage(Image img, int x, int y, int w, int h) { DrawImage(img, new RectangleF(x, y, w, h)); }
        public void DrawImage(Image img, Rectangle d, Rectangle o, GraphicsUnit u) { DrawImage(img, (RectangleF)d, (RectangleF)o, u); }
        public void DrawImage(Image img, RectangleF d, RectangleF o, GraphicsUnit u) { Imagen(img, R(o), R(d), null); }
        public void DrawImage(Image img, Rectangle d, int ox, int oy, int ow, int oh, GraphicsUnit u) { Imagen(img, new SKRect(ox, oy, ox + ow, oy + oh), R(d), null); }
        public void DrawImage(Image img, Rectangle d, int ox, int oy, int ow, int oh, GraphicsUnit u, ImageAttributes ia) { Imagen(img, new SKRect(ox, oy, ox + ow, oy + oh), R(d), ia); }
        public void DrawImage(Image img, Rectangle d, float ox, float oy, float ow, float oh, GraphicsUnit u, ImageAttributes ia) { Imagen(img, new SKRect(ox, oy, ox + ow, oy + oh), R(d), ia); }
        public void DrawImage(Image img, PointF[] destino, RectangleF o, GraphicsUnit u, ImageAttributes ia)
        {
            // un paralelogramo: arriba izquierda, arriba derecha, abajo izquierda (como GDI+)
            if (img == null || destino == null || destino.Length < 3) return;
            SKMatrix m = new SKMatrix((destino[1].X - destino[0].X) / o.Width, (destino[2].X - destino[0].X) / o.Height, destino[0].X,
                                      (destino[1].Y - destino[0].Y) / o.Width, (destino[2].Y - destino[0].Y) / o.Height, destino[0].Y, 0, 0, 1);
            int n = C.Save(); C.Concat(in m);
            Imagen(img, R(o), new SKRect(0, 0, o.Width, o.Height), ia);
            C.RestoreToCount(n);
        }

        // --- el texto de GDI+ (DrawString y MeasureString), con las sobrecargas de .NET: con un punto la caja es (x, y, 0, 0), sin
        // formato es el de fabrica, y MeasureString con un ancho mide en una caja de ese ancho y 999999 de alto (Graphics.cs)
        public void DrawString(string s, Font f, Brush b, float x, float y) { Escribe(s, f, b, new RectangleF(x, y, 0, 0), null); }
        public void DrawString(string s, Font f, Brush b, PointF p) { Escribe(s, f, b, new RectangleF(p.X, p.Y, 0, 0), null); }
        public void DrawString(string s, Font f, Brush b, float x, float y, StringFormat sf) { Escribe(s, f, b, new RectangleF(x, y, 0, 0), sf); }
        public void DrawString(string s, Font f, Brush b, PointF p, StringFormat sf) { Escribe(s, f, b, new RectangleF(p.X, p.Y, 0, 0), sf); }
        public void DrawString(string s, Font f, Brush b, RectangleF r) { Escribe(s, f, b, r, null); }
        public void DrawString(string s, Font f, Brush b, RectangleF r, StringFormat sf) { Escribe(s, f, b, r, sf); }
        public void DrawString(string s, Font f, Brush b, Rectangle r, StringFormat sf) { Escribe(s, f, b, r, sf); }
        private void Escribe(string s, Font f, Brush b, RectangleF r, StringFormat sf)
        {
            if (string.IsNullOrEmpty(s) || f == null || b == null) return;
            SKPaint p = Pincel(b);
            try { TextoGdiPlus.Escribe(C, s, f.L, (AjusteLetra)(int)TextRenderingHint, p, r, (sf ?? StringFormat.SinFormato).F, Copia, Medio); }
            finally { Suelta(p); }
        }
        public SizeF MeasureString(string s, Font f) { return MeasureString(s, f, new SizeF(0, 0), null); }
        public SizeF MeasureString(string s, Font f, int ancho) { return MeasureString(s, f, new SizeF(ancho, 999999f), null); }
        public SizeF MeasureString(string s, Font f, int ancho, StringFormat sf) { return MeasureString(s, f, new SizeF(ancho, 999999f), sf); }
        public SizeF MeasureString(string s, Font f, SizeF caja) { return MeasureString(s, f, caja, null); }
        public SizeF MeasureString(string s, Font f, PointF p, StringFormat sf) { return MeasureString(s, f, new SizeF(0, 0), sf); }
        public SizeF MeasureString(string s, Font f, SizeF caja, StringFormat sf) { int letras, lineas; return MeasureString(s, f, caja, sf, out letras, out lineas); }
        public SizeF MeasureString(string s, Font f, SizeF caja, StringFormat sf, out int letras, out int lineas)
        {
            letras = lineas = 0;
            if (string.IsNullOrEmpty(s)) return new SizeF(0, 0);
            if (f == null) throw new ArgumentNullException("font");
            return TextoGdiPlus.Mide(s, f.L, (AjusteLetra)(int)TextRenderingHint, caja, (sf ?? StringFormat.SinFormato).F, out letras, out lineas);
        }

        // la matriz del HDC (para el texto de GDI de TextRenderer: Nativo/TextoGdi.cs)
        internal SKMatrix MatrizHdc { get { return baseMatriz; } }
    }

    // ---------------------------------------------------------------- el texto de GDI (TextRenderer)
    // TEXTRENDERER ES GDI, NO GDI+ (revision del 8-11-2026). WinForms pinta y mide con DrawTextEx de user32 y una letra de GDI (la fuente
    // de dotnet/winforms, MIT, que sigue a .NET Framework: TextRenderer.cs, TextExtensions.cs, FontCache.Data.cs). Aqui igual, en
    // Nativo/TextoGdi.cs (TextoGdi.Escribe y Mide): LetraGdi es la letra de GDI y DrawTextGdi el DrawTextEx de user32 como lo replica Wine
    // (dlls/user32/text.c, con sus pruebas contra Windows). Esta clase solo pone las sobrecargas de WinForms:
    //   - DrawText con rectangulo y sin banderas centra en los dos sentidos (HorizontalCenter | VerticalCenter); con un punto, no; la caja
    //     de un punto es (x, y, int.MaxValue, int.MaxValue). MeasureText sin banderas lleva Bottom.
    //   - Margenes (GlyphOverhangPadding, el de fabrica): ceil(alto/6) a la izquierda y ceil(alto/6 * 1,5) a la derecha, con el alto de
    //     la letra de GDI (tmHeight); LeftAndRightPadding ceil(2*alto/6) y ceil(alto/6 * 2,5); NoPadding, nada.
    //   - Varios renglones centrados o abajo los alinea WinForms (AdjustForVerticalAlignment): y = arriba + alto/2 - altoTexto/2, con
    //     divisiones enteras, y si no caben, arriba.
    //   - Ni texto vacio ni color Transparent pintan nada. GDI no hace caso a la traslacion que el codigo le puso al Graphics (WinForms
    //     solo la pasa con PreserveGraphicsTranslateTransform): se pinta con la matriz del HDC (Graphics.MatrizHdc). El recorte del codigo
    //     (SetClip) si se queda puesto, porque no se puede quitar del lienzo a medias; XP no llama a TextRenderer con uno puesto.
    //   - El color va opaco (GDI no tiene alfa) y sobre lo que haya (no hace caso a CompositingMode).
    public static class TextRenderer
    {
        private static readonly Size MaxSize = new Size(int.MaxValue, int.MaxValue);
        public static void DrawText(Graphics g, string s, Font f, Rectangle r, Color c) { DrawText(g, s, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }
        public static void DrawText(Graphics g, string s, Font f, Point p, Color c) { DrawText(g, s, f, new Rectangle(p, MaxSize), c, TextFormatFlags.Default); }
        public static void DrawText(Graphics g, string s, Font f, Point p, Color c, TextFormatFlags fl) { DrawText(g, s, f, new Rectangle(p, MaxSize), c, fl); }
        public static void DrawText(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags fl)
        {
            if (g == null) throw new ArgumentNullException("dc");
            if (string.IsNullOrEmpty(s) || c == Color.Transparent || f == null) return;
            bool alias = g.TextRenderingHint == TextRenderingHint.SingleBitPerPixel || g.TextRenderingHint == TextRenderingHint.SingleBitPerPixelGridFit;
            TextoGdi.Escribe(g.C, g.MatrizHdc, s, f.L, r, c, (BanderasGdi)fl, alias);
        }
        public static Size MeasureText(string s, Font f) { return Mide(s, f, MaxSize, TextFormatFlags.Bottom); }
        public static Size MeasureText(string s, Font f, Size caja) { return Mide(s, f, caja, TextFormatFlags.Bottom); }
        public static Size MeasureText(string s, Font f, Size caja, TextFormatFlags fl) { return Mide(s, f, caja, fl); }
        public static Size MeasureText(Graphics g, string s, Font f) { return Mide(s, f, MaxSize, TextFormatFlags.Bottom); }
        public static Size MeasureText(Graphics g, string s, Font f, Size caja) { return Mide(s, f, caja, TextFormatFlags.Bottom); }
        public static Size MeasureText(Graphics g, string s, Font f, Size caja, TextFormatFlags fl) { return Mide(s, f, caja, fl); }
        private static Size Mide(string s, Font f, Size caja, TextFormatFlags fl) { return TextoGdi.Mide(s, f != null ? f.L : null, caja, (BanderasGdi)fl); }
    }
}

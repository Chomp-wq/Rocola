// EL DIBUJO DE LA ROCOLA DE XP, NATIVO EN LINUX (traduccion del programa de XP, fase 1; decision del dueno del 7-11-2026). El programa de
// XP pinta con GDI+ (System.Drawing de Windows). Aqui estan las mismas operaciones que el usa -las de System.Drawing que aparecen en su
// codigo, ni una mas-, hechas con SkiaSharp (la libreria de dibujo de Chrome y Android): asi cada archivo de XP se traduce cambiando lo
// minimo y se puede comparar linea por linea con el original. No es Windows debajo: es dibujo nativo de Linux con el comportamiento de
// GDI+ que la rocola necesita (sobre todo el del texto: el margen de 1/6 de letra, el alto del renglon de las tablas de la letra, el
// corte con puntos suspensivos y el ajuste a los pixeles).
//
// Los tipos sencillos (Color, Rectangle, RectangleF, Point, PointF, Size, SizeF) son los de .NET (System.Drawing.Primitives, que si
// existen en Linux). Los demas viven aqui, en Rocola.Dibujo2D.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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
    public enum PixelFormat { Format24bppRgb = 137224, Format32bppRgb = 139273, Format32bppArgb = 2498570, Format32bppPArgb = 925707 }
    public enum ImageLockMode { ReadOnly = 1, WriteOnly = 2, ReadWrite = 3, UserInputBuffer = 4 }
    [Flags] public enum FontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
    public enum StringAlignment { Near = 0, Center = 1, Far = 2 }
    [Flags] public enum StringFormatFlags { DirectionRightToLeft = 1, DirectionVertical = 2, FitBlackBox = 4, DisplayFormatControl = 32, NoFontFallback = 1024, MeasureTrailingSpaces = 2048, NoWrap = 4096, LineLimit = 8192, NoClip = 16384 }
    public enum StringTrimming { None = 0, Character = 1, Word = 2, EllipsisCharacter = 3, EllipsisWord = 4, EllipsisPath = 5 }
    public enum HotkeyPrefix { None = 0, Show = 1, Hide = 2 }
    [Flags] public enum TextFormatFlags { Default = 0, Left = 0, Top = 0, HorizontalCenter = 1, Right = 2, VerticalCenter = 4, Bottom = 8, WordBreak = 16, SingleLine = 32, ExpandTabs = 64, NoClipping = 256, ExternalLeading = 512, NoPrefix = 2048, Internal = 4096, TextBoxControl = 8192, PathEllipsis = 16384, EndEllipsis = 32768, ModifyString = 65536, RightToLeft = 131072, WordEllipsis = 262144, NoFullWidthCharacterBreak = 524288, HidePrefix = 1048576, PrefixOnly = 2097152, PreserveGraphicsClipping = 16777216, PreserveGraphicsTranslateTransform = 33554432, NoPadding = 268435456, LeftAndRightPadding = 536870912 }

    // ---------------------------------------------------------------- las letras
    public sealed class FontFamily
    {
        public readonly string Name;
        public FontFamily(string nombre) { Name = nombre; }
        // En Windows XP la generica sin remates es Microsoft Sans Serif; la de ancho fijo, Courier New; la de remates, Times New Roman.
        public static FontFamily GenericSansSerif { get { return new FontFamily("Microsoft Sans Serif"); } }
        public static FontFamily GenericMonospace { get { return new FontFamily("Courier New"); } }
        public static FontFamily GenericSerif { get { return new FontFamily("Times New Roman"); } }
        public override string ToString() { return Name; }
    }

    public sealed class Font : IDisposable
    {
        public readonly FontFamily FontFamily;
        public readonly float Size;          // en la unidad de Unit (de fabrica, puntos)
        public readonly FontStyle Style;
        public readonly GraphicsUnit Unit;
        public readonly Letras.Letra Cara; // la letra de verdad (y si la negrita hay que inventarla)
        public Font(string familia, float tam) : this(new FontFamily(familia), tam, FontStyle.Regular, GraphicsUnit.Point) { }
        public Font(string familia, float tam, FontStyle estilo) : this(new FontFamily(familia), tam, estilo, GraphicsUnit.Point) { }
        public Font(string familia, float tam, FontStyle estilo, GraphicsUnit unidad) : this(new FontFamily(familia), tam, estilo, unidad) { }
        public Font(FontFamily familia, float tam) : this(familia, tam, FontStyle.Regular, GraphicsUnit.Point) { }
        public Font(FontFamily familia, float tam, FontStyle estilo) : this(familia, tam, estilo, GraphicsUnit.Point) { }
        public Font(FontFamily familia, float tam, GraphicsUnit unidad) : this(familia, tam, FontStyle.Regular, unidad) { }
        public Font(Font f, FontStyle estilo) : this(f.FontFamily, f.Size, estilo, f.Unit) { }
        public Font(FontFamily familia, float tam, FontStyle estilo, GraphicsUnit unidad)
        {
            FontFamily = familia ?? FontFamily.GenericSansSerif; Size = tam; Style = estilo; Unit = unidad;
            Cara = Letras.Busca(FontFamily.Name, estilo);
        }
        public string Name { get { return FontFamily.Name; } }
        public bool Bold { get { return (Style & FontStyle.Bold) != 0; } }
        public bool Italic { get { return (Style & FontStyle.Italic) != 0; } }
        public bool Underline { get { return (Style & FontStyle.Underline) != 0; } }
        // el tamano en pixeles (a 96 puntos por pulgada, como la rocola en XP)
        public float SizeInPixels
        {
            get
            {
                switch (Unit)
                {
                    case GraphicsUnit.Pixel: case GraphicsUnit.World: case GraphicsUnit.Display: return Size;
                    case GraphicsUnit.Inch: return Size * 96f;
                    case GraphicsUnit.Document: return Size * 96f / 300f;
                    case GraphicsUnit.Millimeter: return Size * 96f / 25.4f;
                    default: return Size * 96f / 72f;   // puntos
                }
            }
        }
        public float SizeInPoints { get { return SizeInPixels * 72f / 96f; } }
        // el alto del renglon en pixeles (GetHeight de GDI+) y el mismo redondeado hacia arriba (Font.Height)
        public float GetHeight() { return Cara.Renglon * SizeInPixels; }
        public float GetHeight(Graphics g) { return GetHeight(); }
        public int Height { get { return (int)Math.Ceiling(GetHeight()); } }
        public void Dispose() { }
        // la letra de Skia con el ajuste que pide el modo de texto
        internal SKFont Skia(TextRenderingHint h)
        {
            SKFont f = new SKFont(Cara.Tipo, SizeInPixels);
            f.Embolden = Cara.NegritaInventada;
            if (Cara.CursivaInventada) f.SkewX = -0.25f;
            bool ajustada = h == TextRenderingHint.SystemDefault || h == TextRenderingHint.AntiAliasGridFit || h == TextRenderingHint.ClearTypeGridFit || h == TextRenderingHint.SingleBitPerPixelGridFit;
            f.Edging = h == TextRenderingHint.SingleBitPerPixel || h == TextRenderingHint.SingleBitPerPixelGridFit ? SKFontEdging.Alias : SKFontEdging.Antialias;
            f.Hinting = ajustada ? SKFontHinting.Normal : SKFontHinting.Slight;
            f.Subpixel = !ajustada;
            f.LinearMetrics = !ajustada;
            return f;
        }
    }

    // DONDE ESTAN LAS LETRAS. Las de la rocola de XP, copiadas del disco de Windows (rocola-discos: /rocola/datos/letras; ROCOLA_LETRAS_XP
    // manda); si no estan (la memoria de prueba, una PC ajena), las libres que haya, con DejaVu Sans primero (la que usa Mono para las
    // referencias de las pruebas). La negrita y la cursiva que la familia no trae se inventan como GDI+ (mas gruesa, inclinada).
    public static class Letras
    {
        public sealed class Letra
        {
            public SKTypeface Tipo; public bool NegritaInventada, CursivaInventada;
            public float Ascenso, Descenso, Renglon;   // en "em" (multiplicar por el tamano en pixeles)
        }
        private static readonly Dictionary<string, Letra> hechas = new Dictionary<string, Letra>(StringComparer.OrdinalIgnoreCase);
        // familia -> archivos (normal, negrita, cursiva, negrita cursiva) en la carpeta de Windows
        private static readonly Dictionary<string, string[]> archivos = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Microsoft Sans Serif", new[] { "micross.ttf", null, null, null } },
            { "Trebuchet MS", new[] { "trebuc.ttf", "trebucbd.ttf", "trebucit.ttf", "trebucbi.ttf" } },
            { "Tahoma", new[] { "tahoma.ttf", "tahomabd.ttf", null, null } },
            { "Segoe UI", new[] { "segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf" } },
            { "Verdana", new[] { "verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf" } },
            { "Arial", new[] { "arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf" } },
            { "Courier New", new[] { "cour.ttf", "courbd.ttf", "couri.ttf", "courbi.ttf" } },
            { "Times New Roman", new[] { "times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf" } },
        };
        public static string[] Carpetas()
        {
            List<string> l = new List<string>();
            string e = Environment.GetEnvironmentVariable("ROCOLA_LETRAS_XP");
            if (!string.IsNullOrEmpty(e)) l.Add(e);
            string d = Environment.GetEnvironmentVariable("ROCOLA_DATOS");
            if (!string.IsNullOrEmpty(d)) l.Add(Path.Combine(d, "letras"));
            l.Add("/rocola/datos/letras");
            return l.ToArray();
        }
        private static string EnCarpetas(string nombre)
        {
            if (nombre == null) return null;
            foreach (string c in Carpetas())
            {
                try
                {
                    if (!Directory.Exists(c)) continue;
                    foreach (string f in Directory.GetFiles(c)) if (string.Equals(Path.GetFileName(f), nombre, StringComparison.OrdinalIgnoreCase)) return f;
                }
                catch { }
            }
            return null;
        }
        // las de repuesto (libres), por orden
        private static readonly string[] Repuesto = { "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf" };
        private static readonly string[] RepuestoNegrita = { "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf" };
        private static readonly string[] RepuestoFijo = { "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", "/usr/share/fonts/dejavu/DejaVuSansMono.ttf" };
        private static readonly string[] RepuestoFijoNegrita = { "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf", "/usr/share/fonts/dejavu/DejaVuSansMono-Bold.ttf" };
        private static string Primero(string[] l) { foreach (string f in l) if (File.Exists(f)) return f; return null; }

        public static Letra Busca(string familia, FontStyle estilo)
        {
            bool b = (estilo & FontStyle.Bold) != 0, i = (estilo & FontStyle.Italic) != 0;
            string clave = familia + "|" + (b ? "b" : "") + (i ? "i" : "");
            lock (hechas)
            {
                Letra l;
                if (hechas.TryGetValue(clave, out l)) return l;
                l = new Letra();
                string[] a; string ruta = null; bool tieneB = false, tieneI = false;
                if (archivos.TryGetValue(familia ?? "", out a))
                {
                    int k = (b ? 1 : 0) + (i ? 2 : 0);
                    // la variante pedida; si no esta, la mas parecida (y se inventa lo que falte)
                    int[] orden = k == 3 ? new[] { 3, 1, 2, 0 } : k == 2 ? new[] { 2, 0 } : k == 1 ? new[] { 1, 0 } : new[] { 0 };
                    foreach (int o in orden)
                    {
                        string f = EnCarpetas(a[o]);
                        if (f != null) { ruta = f; tieneB = (o & 1) != 0; tieneI = (o & 2) != 0; break; }
                    }
                }
                if (ruta == null)
                {
                    bool fija = string.Equals(familia, "Courier New", StringComparison.OrdinalIgnoreCase);
                    string rb = Primero(fija ? RepuestoFijoNegrita : RepuestoNegrita);
                    if (b && rb != null) { ruta = rb; tieneB = true; }
                    else ruta = Primero(fija ? RepuestoFijo : Repuesto);
                }
                l.Tipo = ruta != null ? SKTypeface.FromFile(ruta) : SKTypeface.Default;
                if (l.Tipo == null) l.Tipo = SKTypeface.Default;
                l.NegritaInventada = b && !tieneB;
                l.CursivaInventada = i && !tieneI;
                Metricas(l);
                hechas[clave] = l;
                return l;
            }
        }
        // EL ALTO DEL RENGLON COMO GDI+: ascenso y descenso de la tabla OS/2 (usWinAscent, usWinDescent) y el renglon con el hueco de
        // hhea (GetLineSpacing de FontFamily), todo partido por las unidades del em (head).
        private static void Metricas(Letra l)
        {
            int upem = U16(l.Tipo, "head", 18);
            if (upem <= 0) upem = l.Tipo.UnitsPerEm > 0 ? l.Tipo.UnitsPerEm : 2048;
            int winA = U16(l.Tipo, "OS/2", 74), winD = U16(l.Tipo, "OS/2", 76);
            int hA = S16(l.Tipo, "hhea", 4), hD = S16(l.Tipo, "hhea", 6), hG = S16(l.Tipo, "hhea", 8);
            if (winA <= 0 && winD <= 0) { winA = hA; winD = -hD; }
            int renglon = winA + winD + Math.Max(0, hG - ((winA + winD) - (hA - hD)));
            l.Ascenso = winA / (float)upem; l.Descenso = winD / (float)upem; l.Renglon = renglon / (float)upem;
        }
        private static uint Tag(string t) { return ((uint)t[0] << 24) | ((uint)t[1] << 16) | ((uint)t[2] << 8) | t[3]; }
        private static int U16(SKTypeface tf, string tabla, int at)
        {
            try { byte[] d = tf.GetTableData(Tag(tabla)); if (d == null || d.Length < at + 2) return 0; return (d[at] << 8) | d[at + 1]; } catch { return 0; }
        }
        private static int S16(SKTypeface tf, string tabla, int at) { int v = U16(tf, tabla, at); return v >= 32768 ? v - 65536 : v; }
    }

    // ---------------------------------------------------------------- pinceles y plumas
    public abstract class Brush : IDisposable
    {
        internal abstract void Pon(SKPaint p);
        public virtual void Dispose() { }
        public abstract Brush Clone();
    }
    public sealed class SolidBrush : Brush
    {
        public Color Color;
        public SolidBrush(Color c) { Color = c; }
        internal override void Pon(SKPaint p) { p.Shader = null; p.Color = Graphics.Sk(Color); }
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
        public LinearGradientBrush(PointF a, PointF b, Color ca, Color cb) { p0 = a; p1 = b; c0 = ca; c1 = cb; caja = RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)); }
        public LinearGradientBrush(Point a, Point b, Color ca, Color cb) : this((PointF)a, (PointF)b, ca, cb) { }
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, LinearGradientMode m) { caja = r; c0 = ca; c1 = cb; PorModo(r, m); }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, LinearGradientMode m) : this((RectangleF)r, ca, cb, m) { }
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, float angulo) : this(r, ca, cb, angulo, false) { }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, float angulo) : this((RectangleF)r, ca, cb, angulo, false) { }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, float angulo, bool escalable) : this((RectangleF)r, ca, cb, angulo, escalable) { }
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, float angulo, bool escalable)
        {
            // GDI+: la linea del degradado pasa por el centro de la caja con ese angulo, y sus extremos son las esquinas que tocan
            caja = r; c0 = ca; c1 = cb;
            double t = angulo * Math.PI / 180.0, cs = Math.Cos(t), sn = Math.Sin(t);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            double medio = (Math.Abs(r.Width * cs) + Math.Abs(r.Height * sn)) / 2.0;
            p0 = new PointF((float)(cx - cs * medio), (float)(cy - sn * medio)); p1 = new PointF((float)(cx + cs * medio), (float)(cy + sn * medio));
        }
        private void PorModo(RectangleF r, LinearGradientMode m)
        {
            switch (m)
            {
                case LinearGradientMode.Vertical: p0 = new PointF(r.X, r.Y); p1 = new PointF(r.X, r.Bottom); break;
                case LinearGradientMode.ForwardDiagonal: p0 = new PointF(r.X, r.Y); p1 = new PointF(r.Right, r.Bottom); break;
                case LinearGradientMode.BackwardDiagonal: p0 = new PointF(r.Right, r.Y); p1 = new PointF(r.X, r.Bottom); break;
                default: p0 = new PointF(r.X, r.Y); p1 = new PointF(r.Right, r.Y); break;
            }
        }
        public Color[] LinearColors { get { return new[] { c0, c1 }; } set { c0 = value[0]; c1 = value[1]; } }
        public RectangleF Rectangle { get { return caja; } }
        internal override void Pon(SKPaint p)
        {
            SKColor[] cols; float[] pos;
            if (InterpolationColors != null && InterpolationColors.Colors != null && InterpolationColors.Colors.Length >= 2)
            {
                cols = new SKColor[InterpolationColors.Colors.Length]; for (int i = 0; i < cols.Length; i++) cols[i] = Graphics.Sk(InterpolationColors.Colors[i]);
                pos = (float[])InterpolationColors.Positions.Clone();
            }
            else if (Blend != null && Blend.Factors != null && Blend.Factors.Length >= 2)
            {
                cols = new SKColor[Blend.Factors.Length]; pos = (float[])Blend.Positions.Clone();
                for (int i = 0; i < cols.Length; i++) cols[i] = Graphics.Sk(Mezcla(c0, c1, Blend.Factors[i]));
            }
            else { cols = new[] { Graphics.Sk(c0), Graphics.Sk(c1) }; pos = null; }
            SKShaderTileMode tm = WrapMode == WrapMode.Clamp ? SKShaderTileMode.Clamp : WrapMode == WrapMode.Tile ? SKShaderTileMode.Repeat : SKShaderTileMode.Mirror;
            p.Shader = SKShader.CreateLinearGradient(new SKPoint(p0.X, p0.Y), new SKPoint(p1.X, p1.Y), cols, pos, tm);
            p.Color = SKColors.Black;
        }
        internal static Color Mezcla(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb((int)Math.Round(a.A + (b.A - a.A) * t), (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }
        public override Brush Clone() { LinearGradientBrush b = (LinearGradientBrush)MemberwiseClone(); return b; }
    }
    // EL DEGRADADO DE UN TRAZO (PathGradientBrush): del color del centro al de la orilla. La rocola lo usa con elipses (halos y brillos):
    // aqui es un degradado radial desde el centro hasta la orilla de la elipse, estirado a su forma.
    public sealed class PathGradientBrush : Brush
    {
        private readonly RectangleF caja;
        public Color CenterColor = Color.White;
        public Color[] SurroundColors = { Color.White };
        public PointF CenterPoint;
        public WrapMode WrapMode = WrapMode.Clamp;
        public PathGradientBrush(GraphicsPath path) { caja = path.GetBounds(); CenterPoint = new PointF(caja.X + caja.Width / 2f, caja.Y + caja.Height / 2f); }
        internal override void Pon(SKPaint p)
        {
            Color fuera = SurroundColors != null && SurroundColors.Length > 0 ? SurroundColors[0] : Color.Transparent;
            float rx = Math.Max(0.5f, caja.Width / 2f), ry = Math.Max(0.5f, caja.Height / 2f);
            SKMatrix m = SKMatrix.CreateScale(1f, ry / rx, CenterPoint.X, CenterPoint.Y);
            p.Shader = SKShader.CreateRadialGradient(new SKPoint(CenterPoint.X, CenterPoint.Y), rx, new[] { Graphics.Sk(CenterColor), Graphics.Sk(fuera) }, null, SKShaderTileMode.Clamp, m);
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
        internal override void Pon(SKPaint p)
        {
            SKShaderTileMode tx = WrapMode == WrapMode.Clamp ? SKShaderTileMode.Clamp : (WrapMode == WrapMode.TileFlipX || WrapMode == WrapMode.TileFlipXY) ? SKShaderTileMode.Mirror : SKShaderTileMode.Repeat;
            SKShaderTileMode ty = WrapMode == WrapMode.Clamp ? SKShaderTileMode.Clamp : (WrapMode == WrapMode.TileFlipY || WrapMode == WrapMode.TileFlipXY) ? SKShaderTileMode.Mirror : SKShaderTileMode.Repeat;
            p.Shader = SKShader.CreateBitmap(img.Sk, tx, ty, mat);
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
        internal void Pon(SKPaint p, bool suave)
        {
            p.Style = SKPaintStyle.Stroke;
            if (Brush != null) Brush.Pon(p); else { p.Shader = null; p.Color = Graphics.Sk(Color); }
            p.StrokeWidth = Width <= 1f ? Math.Max(Width, 1f) : Width;   // GDI+: una pluma de 0 o 1 es de un pixel
            p.StrokeJoin = LineJoin == LineJoin.Round ? SKStrokeJoin.Round : LineJoin == LineJoin.Bevel ? SKStrokeJoin.Bevel : SKStrokeJoin.Miter;
            p.StrokeCap = StartCap == LineCap.Round || EndCap == LineCap.Round ? SKStrokeCap.Round : StartCap == LineCap.Square || EndCap == LineCap.Square ? SKStrokeCap.Square : SKStrokeCap.Butt;
            p.IsAntialias = suave;
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
            Font f = new Font(fam, emPx, (FontStyle)estilo, GraphicsUnit.Pixel);
            foreach (Texto.Pieza pz in Texto.Acomoda(s, f, caja, sf, TextRenderingHint.AntiAlias, false))
                using (SKFont sk = f.Skia(TextRenderingHint.AntiAlias))
                using (SKPath tp = sk.GetTextPath(pz.Cadena, new SKPoint(pz.X, pz.Base)))
                    P.AddPath(tp);
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
        public StringAlignment Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near;
        public StringFormatFlags FormatFlags;
        public StringTrimming Trimming = StringTrimming.Character;
        public HotkeyPrefix HotkeyPrefix = HotkeyPrefix.None;
        internal bool Tipografico;   // GenericTypographic: sin el margen de 1/6 de letra
        public StringFormat() { }
        public StringFormat(StringFormatFlags f) { FormatFlags = f; }
        public StringFormat(StringFormat o) { Alignment = o.Alignment; LineAlignment = o.LineAlignment; FormatFlags = o.FormatFlags; Trimming = o.Trimming; HotkeyPrefix = o.HotkeyPrefix; Tipografico = o.Tipografico; }
        public static StringFormat GenericDefault { get { return new StringFormat(); } }
        public static StringFormat GenericTypographic { get { return new StringFormat { FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.NoClip, Trimming = StringTrimming.None, Tipografico = true }; } }
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
        // GDI+ multiplica el color como fila (r g b a 1) por la matriz; Skia, la matriz por la columna, con el desplazamiento en 0..255
        internal float[] Skia()
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
    public sealed class ImageAttributes : IDisposable
    {
        internal ColorMatrix Matriz; internal WrapMode Envoltura = WrapMode.Clamp;
        public void SetColorMatrix(ColorMatrix m) { Matriz = m; }
        public void SetColorMatrix(ColorMatrix m, int flags, int tipo) { Matriz = m; }
        public void SetWrapMode(WrapMode w) { Envoltura = w; }
        public void Dispose() { }
    }
    public sealed class BitmapData { public IntPtr Scan0; public int Stride, Width, Height; public PixelFormat PixelFormat; internal byte[] copia; internal GCHandle fijo; internal ImageLockMode modo; internal Rectangle zona; }
    public sealed class ImageFormat { public readonly string N; private ImageFormat(string n) { N = n; } public static readonly ImageFormat Png = new ImageFormat("png"), Jpeg = new ImageFormat("jpeg"), Bmp = new ImageFormat("bmp"); }

    public abstract class Image : IDisposable
    {
        internal SKBitmap Sk;   // los puntos (BGRA premultiplicado, como Format32bppPArgb)
        public int Width { get { return Sk.Width; } }
        public int Height { get { return Sk.Height; } }
        public Size Size { get { return new Size(Sk.Width, Sk.Height); } }
        public PixelFormat PixelFormat { get; protected set; }
        public object Tag;
        public static Image FromStream(Stream s)
        {
            using (MemoryStream ms = new MemoryStream()) { s.CopyTo(ms); return Bitmap.Decodifica(ms.ToArray()); }
        }
        public static Image FromStream(Stream s, bool usarColor, bool validar) { return FromStream(s); }
        public static Image FromFile(string ruta) { return Bitmap.Decodifica(File.ReadAllBytes(ruta)); }
        public virtual void Dispose() { if (Sk != null) { Sk.Dispose(); Sk = null; } }
        public void Save(string ruta) { Save(ruta, ruta.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png); }
        public void Save(string ruta, ImageFormat f) { using (FileStream fs = File.Create(ruta)) Save(fs, f); }
        public void Save(Stream s, ImageFormat f)
        {
            using (SKImage i = SKImage.FromBitmap(Sk))
            using (SKData d = i.Encode(f == ImageFormat.Jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 92)) d.SaveTo(s);
        }
    }
    public sealed class Bitmap : Image
    {
        public Bitmap(int w, int h) : this(w, h, PixelFormat.Format32bppArgb) { }
        public Bitmap(int w, int h, PixelFormat f)
        {
            Sk = new SKBitmap(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Bgra8888, SKAlphaType.Premul));
            Sk.Erase(SKColors.Transparent); PixelFormat = f;
        }
        public Bitmap(Image origen) : this(origen, origen.Width, origen.Height) { }
        public Bitmap(Image origen, Size s) : this(origen, s.Width, s.Height) { }
        public Bitmap(Image origen, int w, int h) : this(w, h, PixelFormat.Format32bppArgb)
        {
            using (Graphics g = Graphics.FromImage(this)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(origen, new Rectangle(0, 0, w, h)); }
        }
        internal Bitmap(SKBitmap b, PixelFormat f) { Sk = b; PixelFormat = f; }
        internal static Bitmap Decodifica(byte[] datos)
        {
            using (SKBitmap b = SKBitmap.Decode(datos))
            {
                if (b == null) throw new ArgumentException("no es una imagen");
                SKBitmap c = new SKBitmap(new SKImageInfo(b.Width, b.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                using (SKCanvas cv = new SKCanvas(c)) { cv.Clear(SKColors.Transparent); cv.DrawBitmap(b, 0, 0); }
                return new Bitmap(c, PixelFormat.Format32bppArgb);
            }
        }
        public Bitmap Clone(Rectangle r, PixelFormat f)
        {
            SKBitmap c = new SKBitmap(new SKImageInfo(r.Width, r.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (SKCanvas cv = new SKCanvas(c)) { cv.Clear(SKColors.Transparent); cv.DrawBitmap(Sk, new SKRect(r.Left, r.Top, r.Right, r.Bottom), new SKRect(0, 0, r.Width, r.Height)); }
            return new Bitmap(c, f);
        }
        public object Clone() { return Clone(new Rectangle(0, 0, Width, Height), PixelFormat); }
        public Color GetPixel(int x, int y) { SKColor c = Sk.GetPixel(x, y); return Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue); }
        public void SetPixel(int x, int y, Color c) { Sk.SetPixel(x, y, Graphics.Sk(c)); }
        // LOS PUNTOS PARA LA TARJETA (o para mirarlos): premultiplicados tal cual; si se piden sin premultiplicar, una copia que se
        // devuelve al soltarla (si era para escribir)
        public BitmapData LockBits(Rectangle r, ImageLockMode modo, PixelFormat f)
        {
            BitmapData d = new BitmapData { Width = r.Width, Height = r.Height, PixelFormat = f, modo = modo, zona = r };
            bool directo = f == PixelFormat.Format32bppPArgb && r.X == 0 && r.Y == 0 && r.Width == Width;
            if (directo)
            {
                d.Stride = Sk.RowBytes; d.Scan0 = Sk.GetPixels() + r.Y * Sk.RowBytes;
                return d;
            }
            int bpp = f == PixelFormat.Format24bppRgb ? 3 : 4, stride = ((r.Width * bpp) + 3) & ~3;
            d.copia = new byte[stride * r.Height]; d.Stride = stride;
            IntPtr pix = Sk.GetPixels(); int rb = Sk.RowBytes;
            unsafe
            {
                byte* o = (byte*)pix;
                for (int y = 0; y < r.Height; y++)
                    for (int x = 0; x < r.Width; x++)
                    {
                        byte* s = o + (r.Y + y) * rb + (r.X + x) * 4;
                        int k = y * stride + x * bpp; byte a = s[3];
                        if (f == PixelFormat.Format32bppPArgb) { d.copia[k] = s[0]; d.copia[k + 1] = s[1]; d.copia[k + 2] = s[2]; d.copia[k + 3] = a; }
                        else
                        {
                            byte b0 = s[0], g0 = s[1], r0 = s[2];
                            if (a != 0 && a != 255) { b0 = (byte)Math.Min(255, b0 * 255 / a); g0 = (byte)Math.Min(255, g0 * 255 / a); r0 = (byte)Math.Min(255, r0 * 255 / a); }
                            d.copia[k] = b0; d.copia[k + 1] = g0; d.copia[k + 2] = r0;
                            if (bpp == 4) d.copia[k + 3] = f == PixelFormat.Format32bppRgb ? (byte)255 : a;
                        }
                    }
            }
            d.fijo = GCHandle.Alloc(d.copia, GCHandleType.Pinned); d.Scan0 = d.fijo.AddrOfPinnedObject();
            return d;
        }
        public void UnlockBits(BitmapData d)
        {
            if (d == null || d.copia == null) return;
            if ((d.modo & ImageLockMode.WriteOnly) != 0)
            {
                int bpp = d.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4; Rectangle r = d.zona;
                unsafe
                {
                    byte* o = (byte*)Sk.GetPixels(); int rb = Sk.RowBytes;
                    for (int y = 0; y < r.Height; y++)
                        for (int x = 0; x < r.Width; x++)
                        {
                            byte* t = o + (r.Y + y) * rb + (r.X + x) * 4; int k = y * d.Stride + x * bpp;
                            byte a = bpp == 4 && d.PixelFormat != PixelFormat.Format32bppRgb ? d.copia[k + 3] : (byte)255;
                            if (d.PixelFormat == PixelFormat.Format32bppPArgb) { t[0] = d.copia[k]; t[1] = d.copia[k + 1]; t[2] = d.copia[k + 2]; t[3] = a; }
                            else { t[0] = (byte)(d.copia[k] * a / 255); t[1] = (byte)(d.copia[k + 1] * a / 255); t[2] = (byte)(d.copia[k + 2] * a / 255); t[3] = a; }
                        }
                }
                Sk.NotifyPixelsChanged();
            }
            d.fijo.Free(); d.copia = null;
        }
    }

    // ---------------------------------------------------------------- el lienzo (Graphics)
    public sealed class GraphicsState { internal int Cuenta; internal SmoothingMode S; internal TextRenderingHint T; internal CompositingMode C; internal InterpolationMode I; internal PixelOffsetMode P; internal RectangleF? Clip; }

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
        internal Graphics(SKCanvas c, bool suyo) { C = c; propio = suyo; baseCuenta = c.SaveCount; }
        public static Graphics FromImage(Image i) { return new Graphics(new SKCanvas(i.Sk), true); }
        public void Dispose() { if (propio) { C.Flush(); C.Dispose(); } }
        public void Flush() { C.Flush(); }

        internal static SKColor Sk(Color c) { return new SKColor(c.R, c.G, c.B, c.A); }
        private bool Suave { get { return SmoothingMode == SmoothingMode.AntiAlias || SmoothingMode == SmoothingMode.HighQuality; } }
        private SKPaint Relleno(Brush b)
        {
            SKPaint p = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = Suave };
            b.Pon(p);
            if (CompositingMode == CompositingMode.SourceCopy) p.BlendMode = SKBlendMode.Src;
            return p;
        }
        private SKPaint Trazo(Pen pen)
        {
            SKPaint p = new SKPaint(); pen.Pon(p, Suave);
            if (CompositingMode == CompositingMode.SourceCopy) p.BlendMode = SKBlendMode.Src;
            return p;
        }
        private static SKRect R(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }
        // GDI+ sin suavizado y sin desplazamiento de medio pixel pinta los bordes en los pixeles enteros: igual que Skia sin antialias

        public void Clear(Color c) { C.Clear(Sk(c)); }

        // --- el estado
        public GraphicsState Save()
        {
            GraphicsState s = new GraphicsState { Cuenta = C.Save(), S = SmoothingMode, T = TextRenderingHint, C = CompositingMode, I = InterpolationMode, P = PixelOffsetMode, Clip = recorte };
            return s;
        }
        public void Restore(GraphicsState s)
        {
            if (s == null) return;
            C.RestoreToCount(s.Cuenta);
            SmoothingMode = s.S; TextRenderingHint = s.T; CompositingMode = s.C; InterpolationMode = s.I; PixelOffsetMode = s.P; recorte = s.Clip;
            conRecorte = false; baseCuenta = C.SaveCount;
        }
        public void TranslateTransform(float dx, float dy) { C.Translate(dx, dy); }
        public void ScaleTransform(float sx, float sy) { C.Scale(sx, sy); }
        public void RotateTransform(float grados) { C.RotateDegrees(grados); }
        public void ResetTransform() { C.ResetMatrix(); }
        public Matrix Transform { get { return new Matrix { M = C.TotalMatrix }; } set { C.SetMatrix(value.M); } }

        // --- el recorte (SetClip cambia el de antes; Intersect, Exclude y los demas lo combinan)
        public void SetClip(RectangleF r) { SetClip(r, CombineMode.Replace); }
        public void SetClip(Rectangle r) { SetClip((RectangleF)r, CombineMode.Replace); }
        public void SetClip(Rectangle r, CombineMode m) { SetClip((RectangleF)r, m); }
        public void SetClip(RectangleF r, CombineMode m)
        {
            if (m == CombineMode.Replace) QuitaRecorte();
            if (!conRecorte) { baseCuenta = C.Save(); conRecorte = true; }
            if (m == CombineMode.Exclude) C.ClipRect(R(r), SKClipOperation.Difference, Suave);
            else C.ClipRect(R(r), SKClipOperation.Intersect, false);
            recorte = r;
        }
        public void SetClip(GraphicsPath gp) { SetClip(gp, CombineMode.Replace); }
        public void SetClip(GraphicsPath gp, CombineMode m)
        {
            if (m == CombineMode.Replace) QuitaRecorte();
            if (!conRecorte) { baseCuenta = C.Save(); conRecorte = true; }
            C.ClipPath(gp.P, m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, Suave);
            recorte = gp.GetBounds();
        }
        public void SetClip(Region rg, CombineMode m)
        {
            if (m == CombineMode.Replace) QuitaRecorte();
            if (!conRecorte) { baseCuenta = C.Save(); conRecorte = true; }
            C.ClipPath(rg.P, m == CombineMode.Exclude ? SKClipOperation.Difference : SKClipOperation.Intersect, Suave);
        }
        public void IntersectClip(RectangleF r) { SetClip(r, CombineMode.Intersect); }
        public void IntersectClip(Rectangle r) { SetClip((RectangleF)r, CombineMode.Intersect); }
        public void ExcludeClip(Rectangle r) { SetClip((RectangleF)r, CombineMode.Exclude); }
        public void ResetClip() { QuitaRecorte(); }
        private void QuitaRecorte()
        {
            if (!conRecorte) return;
            SKMatrix m = C.TotalMatrix;
            C.RestoreToCount(baseCuenta); C.SetMatrix(m);   // (el recorte se va; la posicion se queda)
            conRecorte = false; recorte = null;
        }
        public RectangleF ClipBounds { get { SKRect b = C.LocalClipBounds; return RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom); } }
        public RectangleF VisibleClipBounds { get { return ClipBounds; } }

        // --- formas
        public void FillRectangle(Brush b, RectangleF r) { using (SKPaint p = Relleno(b)) { if (!Suave) p.IsAntialias = false; C.DrawRect(R(r), p); } }
        public void FillRectangle(Brush b, Rectangle r) { FillRectangle(b, (RectangleF)r); }
        public void FillRectangle(Brush b, float x, float y, float w, float h) { FillRectangle(b, new RectangleF(x, y, w, h)); }
        public void FillRectangle(Brush b, int x, int y, int w, int h) { FillRectangle(b, new RectangleF(x, y, w, h)); }
        public void FillRectangles(Brush b, RectangleF[] rs) { foreach (RectangleF r in rs) FillRectangle(b, r); }
        public void DrawRectangle(Pen pen, RectangleF r) { using (SKPaint p = Trazo(pen)) C.DrawRect(R(r), p); }
        public void DrawRectangle(Pen pen, Rectangle r) { DrawRectangle(pen, (RectangleF)r); }
        public void DrawRectangle(Pen pen, float x, float y, float w, float h) { DrawRectangle(pen, new RectangleF(x, y, w, h)); }
        public void DrawRectangle(Pen pen, int x, int y, int w, int h) { DrawRectangle(pen, new RectangleF(x, y, w, h)); }
        public void FillEllipse(Brush b, RectangleF r) { using (SKPaint p = Relleno(b)) C.DrawOval(R(r), p); }
        public void FillEllipse(Brush b, Rectangle r) { FillEllipse(b, (RectangleF)r); }
        public void FillEllipse(Brush b, float x, float y, float w, float h) { FillEllipse(b, new RectangleF(x, y, w, h)); }
        public void FillEllipse(Brush b, int x, int y, int w, int h) { FillEllipse(b, new RectangleF(x, y, w, h)); }
        public void DrawEllipse(Pen pen, RectangleF r) { using (SKPaint p = Trazo(pen)) C.DrawOval(R(r), p); }
        public void DrawEllipse(Pen pen, Rectangle r) { DrawEllipse(pen, (RectangleF)r); }
        public void DrawEllipse(Pen pen, float x, float y, float w, float h) { DrawEllipse(pen, new RectangleF(x, y, w, h)); }
        public void DrawEllipse(Pen pen, int x, int y, int w, int h) { DrawEllipse(pen, new RectangleF(x, y, w, h)); }
        public void FillPath(Brush b, GraphicsPath gp) { using (SKPaint p = Relleno(b)) C.DrawPath(gp.P, p); }
        public void DrawPath(Pen pen, GraphicsPath gp) { using (SKPaint p = Trazo(pen)) C.DrawPath(gp.P, p); }
        public void FillRegion(Brush b, Region rg) { using (SKPaint p = Relleno(b)) C.DrawPath(rg.P, p); }
        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2) { using (SKPaint p = Trazo(pen)) C.DrawLine(x1, y1, x2, y2, p); }
        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) { DrawLine(pen, (float)x1, y1, x2, y2); }
        public void DrawLine(Pen pen, PointF a, PointF b) { DrawLine(pen, a.X, a.Y, b.X, b.Y); }
        public void DrawLine(Pen pen, Point a, Point b) { DrawLine(pen, a.X, a.Y, b.X, b.Y); }
        public void DrawLines(Pen pen, PointF[] ps) { if (ps == null || ps.Length < 2) return; using (SKPath sp = new SKPath()) { sp.MoveTo(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) sp.LineTo(ps[i].X, ps[i].Y); using (SKPaint p = Trazo(pen)) C.DrawPath(sp, p); } }
        public void DrawLines(Pen pen, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; DrawLines(pen, f); }
        private static SKPath Poligono(PointF[] ps, FillMode m) { SKPath sp = new SKPath { FillType = m == FillMode.Alternate ? SKPathFillType.EvenOdd : SKPathFillType.Winding }; sp.MoveTo(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) sp.LineTo(ps[i].X, ps[i].Y); sp.Close(); return sp; }
        public void FillPolygon(Brush b, PointF[] ps) { FillPolygon(b, ps, FillMode.Alternate); }
        public void FillPolygon(Brush b, PointF[] ps, FillMode m) { if (ps == null || ps.Length < 3) return; using (SKPath sp = Poligono(ps, m)) using (SKPaint p = Relleno(b)) C.DrawPath(sp, p); }
        public void FillPolygon(Brush b, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; FillPolygon(b, f); }
        public void DrawPolygon(Pen pen, PointF[] ps) { if (ps == null || ps.Length < 2) return; using (SKPath sp = Poligono(ps, FillMode.Alternate)) using (SKPaint p = Trazo(pen)) C.DrawPath(sp, p); }
        public void DrawPolygon(Pen pen, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; DrawPolygon(pen, f); }
        public void DrawArc(Pen pen, RectangleF r, float inicio, float barrido) { using (SKPaint p = Trazo(pen)) C.DrawArc(R(r), inicio, barrido, false, p); }
        public void DrawArc(Pen pen, Rectangle r, float inicio, float barrido) { DrawArc(pen, (RectangleF)r, inicio, barrido); }
        public void DrawArc(Pen pen, float x, float y, float w, float h, float inicio, float barrido) { DrawArc(pen, new RectangleF(x, y, w, h), inicio, barrido); }
        public void FillPie(Brush b, RectangleF r, float inicio, float barrido) { using (SKPaint p = Relleno(b)) C.DrawArc(R(r), inicio, barrido, true, p); }
        public void FillPie(Brush b, float x, float y, float w, float h, float inicio, float barrido) { FillPie(b, new RectangleF(x, y, w, h), inicio, barrido); }
        public void DrawBezier(Pen pen, PointF a, PointF b, PointF c, PointF d) { using (SKPath sp = new SKPath()) { sp.MoveTo(a.X, a.Y); sp.CubicTo(b.X, b.Y, c.X, c.Y, d.X, d.Y); using (SKPaint p = Trazo(pen)) C.DrawPath(sp, p); } }

        // --- imagenes
        private SKSamplingOptions Muestreo()
        {
            switch (InterpolationMode)
            {
                case InterpolationMode.NearestNeighbor: return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
                case InterpolationMode.HighQualityBicubic: case InterpolationMode.Bicubic: case InterpolationMode.High: return new SKSamplingOptions(SKCubicResampler.Mitchell);
                default: return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
            }
        }
        private void Imagen(Image img, SKRect origen, SKRect destino, ImageAttributes ia)
        {
            if (img == null || img.Sk == null) return;
            using (SKPaint p = new SKPaint { IsAntialias = false })
            {
                if (CompositingMode == CompositingMode.SourceCopy) p.BlendMode = SKBlendMode.Src;
                if (ia != null && ia.Matriz != null) p.ColorFilter = SKColorFilter.CreateColorMatrix(ia.Matriz.Skia());
                using (SKImage si = SKImage.FromBitmap(img.Sk)) C.DrawImage(si, origen, destino, Muestreo(), p);
            }
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

        // --- el texto de GDI+ (DrawString y MeasureString)
        public void DrawString(string s, Font f, Brush b, float x, float y) { DrawString(s, f, b, new PointF(x, y), null); }
        public void DrawString(string s, Font f, Brush b, PointF p) { DrawString(s, f, b, p, null); }
        public void DrawString(string s, Font f, Brush b, float x, float y, StringFormat sf) { DrawString(s, f, b, new PointF(x, y), sf); }
        public void DrawString(string s, Font f, Brush b, PointF p, StringFormat sf) { Escribe(s, f, b, new RectangleF(p.X, p.Y, 0, 0), sf, true); }
        public void DrawString(string s, Font f, Brush b, RectangleF r) { Escribe(s, f, b, r, null, false); }
        public void DrawString(string s, Font f, Brush b, RectangleF r, StringFormat sf) { Escribe(s, f, b, r, sf, false); }
        public void DrawString(string s, Font f, Brush b, Rectangle r, StringFormat sf) { Escribe(s, f, b, r, sf, false); }
        private void Escribe(string s, Font f, Brush b, RectangleF r, StringFormat sf, bool enPunto)
        {
            if (string.IsNullOrEmpty(s) || f == null || b == null) return;
            sf = sf ?? StringFormat.GenericDefault;
            List<Texto.Pieza> pz = Texto.Acomoda(s, f, r, sf, TextRenderingHint, enPunto);
            bool recorta = !enPunto && (sf.FormatFlags & StringFormatFlags.NoClip) == 0 && r.Width > 0 && r.Height > 0;
            int n = 0; if (recorta) { n = C.Save(); C.ClipRect(R(r)); }
            using (SKFont sk = f.Skia(TextRenderingHint))
            using (SKPaint p = Relleno(b))
            {
                p.IsAntialias = TextRenderingHint != TextRenderingHint.SingleBitPerPixel && TextRenderingHint != TextRenderingHint.SingleBitPerPixelGridFit;
                foreach (Texto.Pieza x in pz)
                {
                    C.DrawText(x.Cadena, x.X, x.Base, sk, p);
                    if (f.Underline) { float grosor = Math.Max(1f, f.SizeInPixels / 14f); C.DrawRect(new SKRect(x.X, x.Base + grosor, x.X + x.Ancho, x.Base + 2 * grosor), p); }
                }
            }
            if (recorta) C.RestoreToCount(n);
        }
        public SizeF MeasureString(string s, Font f) { return Texto.Mide(s, f, new SizeF(0, 0), null, TextRenderingHint); }
        public SizeF MeasureString(string s, Font f, int ancho) { return Texto.Mide(s, f, new SizeF(ancho, 0), null, TextRenderingHint); }
        public SizeF MeasureString(string s, Font f, int ancho, StringFormat sf) { return Texto.Mide(s, f, new SizeF(ancho, 0), sf, TextRenderingHint); }
        public SizeF MeasureString(string s, Font f, SizeF caja) { return Texto.Mide(s, f, caja, null, TextRenderingHint); }
        public SizeF MeasureString(string s, Font f, SizeF caja, StringFormat sf) { return Texto.Mide(s, f, caja, sf, TextRenderingHint); }
        public SizeF MeasureString(string s, Font f, PointF p, StringFormat sf) { return Texto.Mide(s, f, new SizeF(0, 0), sf, TextRenderingHint); }
    }

    // ---------------------------------------------------------------- el texto de GDI (TextRenderer)
    public static class TextRenderer
    {
        private static StringFormat Formato(TextFormatFlags fl)
        {
            StringFormat sf = new StringFormat();
            sf.Alignment = (fl & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : (fl & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near;
            sf.LineAlignment = (fl & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : (fl & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far : StringAlignment.Near;
            if ((fl & TextFormatFlags.WordBreak) == 0) sf.FormatFlags |= StringFormatFlags.NoWrap;
            if ((fl & TextFormatFlags.NoClipping) != 0) sf.FormatFlags |= StringFormatFlags.NoClip;
            sf.Trimming = (fl & TextFormatFlags.EndEllipsis) != 0 ? StringTrimming.EllipsisCharacter : (fl & TextFormatFlags.WordEllipsis) != 0 ? StringTrimming.EllipsisWord : StringTrimming.None;
            // GDI: sin NoPadding deja un sexto de letra a los lados (como el margen de GDI+); con NoPadding, nada
            sf.Tipografico = (fl & TextFormatFlags.NoPadding) != 0;
            return sf;
        }
        public static void DrawText(Graphics g, string s, Font f, Rectangle r, Color c) { DrawText(g, s, f, r, c, TextFormatFlags.Default); }
        public static void DrawText(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags fl)
        {
            using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, (RectangleF)r, Formato(fl));
        }
        public static void DrawText(Graphics g, string s, Font f, Point p, Color c) { DrawText(g, s, f, p, c, TextFormatFlags.Default); }
        public static void DrawText(Graphics g, string s, Font f, Point p, Color c, TextFormatFlags fl)
        {
            using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, new PointF(p.X, p.Y), Formato(fl));
        }
        public static Size MeasureText(string s, Font f) { return MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.Default); }
        public static Size MeasureText(string s, Font f, Size caja) { return MeasureText(s, f, caja, TextFormatFlags.Default); }
        public static Size MeasureText(string s, Font f, Size caja, TextFormatFlags fl)
        {
            SizeF m = Texto.Mide(s, f, new SizeF(caja.Width == int.MaxValue ? 0 : caja.Width, 0), Formato(fl), TextRenderingHint.SystemDefault);
            return new Size((int)Math.Ceiling(m.Width), (int)Math.Ceiling(m.Height));
        }
        public static Size MeasureText(Graphics g, string s, Font f, Size caja, TextFormatFlags fl) { return MeasureText(s, f, caja, fl); }
    }

    // ---------------------------------------------------------------- como acomoda el texto GDI+
    // EL TEXTO COMO GDI+: un margen de 1/6 de la letra a cada lado (salvo GenericTypographic o NoPadding), renglones del alto de la letra
    // (Font.GetHeight), corte de palabras al ancho de la caja (salvo NoWrap), el corte con "..." de EllipsisCharacter/Word y la alineacion
    // de cada renglon y del bloque. La primera linea va con su base en lo alto de la caja + el ascenso de la letra.
    public static class Texto
    {
        public struct Pieza { public string Cadena; public float X, Base, Ancho; }
        private static float Ancho(SKFont f, string s) { return string.IsNullOrEmpty(s) ? 0f : f.MeasureText(s); }
        private static float Margen(Font f, StringFormat sf) { return sf != null && sf.Tipografico ? 0f : f.SizeInPixels / 6f; }
        private static List<string> Renglones(string s, SKFont sk, float anchoUtil, bool sinCorte)
        {
            List<string> l = new List<string>();
            string[] parrafos = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string par in parrafos)
            {
                if (sinCorte || anchoUtil <= 0) { l.Add(par); continue; }
                if (Ancho(sk, par) <= anchoUtil) { l.Add(par); continue; }
                // por palabras; una palabra que no cabe sola se parte por letras
                string resto = par;
                while (resto.Length > 0)
                {
                    if (Ancho(sk, resto) <= anchoUtil) { l.Add(resto); break; }
                    int corte = -1;
                    for (int i = 1; i <= resto.Length; i++)
                    {
                        if (i < resto.Length && resto[i] != ' ') continue;
                        if (Ancho(sk, resto.Substring(0, i).TrimEnd()) <= anchoUtil) corte = i; else break;
                    }
                    if (corte <= 0)
                    {
                        int k = 1; while (k < resto.Length && Ancho(sk, resto.Substring(0, k + 1)) <= anchoUtil) k++;
                        corte = k;
                    }
                    l.Add(resto.Substring(0, corte).TrimEnd());
                    resto = resto.Substring(corte).TrimStart(' ');
                }
            }
            return l;
        }
        private static string ConPuntos(string s, SKFont sk, float anchoUtil, bool porPalabra)
        {
            if (Ancho(sk, s) <= anchoUtil) return s;
            const string P = "…";
            for (int n = s.Length - 1; n > 0; n--)
            {
                string c = s.Substring(0, n);
                if (porPalabra) { int e = c.LastIndexOf(' '); if (e > 0) c = c.Substring(0, e); }
                c = c.TrimEnd();
                if (Ancho(sk, c + P) <= anchoUtil) return c + P;
            }
            return P;
        }
        private static string Cortada(string s, SKFont sk, float anchoUtil)
        {
            if (Ancho(sk, s) <= anchoUtil) return s;
            int k = 0; while (k < s.Length && Ancho(sk, s.Substring(0, k + 1)) <= anchoUtil) k++;
            return s.Substring(0, k);
        }
        public static List<Pieza> Acomoda(string s, Font f, RectangleF caja, StringFormat sf, TextRenderingHint h, bool enPunto)
        {
            List<Pieza> r = new List<Pieza>();
            if (string.IsNullOrEmpty(s)) return r;
            sf = sf ?? StringFormat.GenericDefault;
            using (SKFont sk = f.Skia(h))
            {
                float m = Margen(f, sf), alto = f.GetHeight(), asc = f.Cara.Ascenso * f.SizeInPixels;
                bool sinCaja = enPunto || caja.Width <= 0;
                bool noWrap = sinCaja || (sf.FormatFlags & StringFormatFlags.NoWrap) != 0;
                float util = sinCaja ? 0f : Math.Max(0f, caja.Width - 2 * m);
                List<string> rs = Renglones(s, sk, util, noWrap);
                // cuantos renglones caben (los que no, no se pintan; con LineLimit, tampoco el que asoma a medias)
                int caben = rs.Count;
                if (!enPunto && caja.Height > 0)
                {
                    int enteros = Math.Max(1, (int)Math.Floor(caja.Height / alto + 1e-3));
                    bool limite = (sf.FormatFlags & StringFormatFlags.LineLimit) != 0;
                    int visibles = limite ? enteros : (int)Math.Ceiling(caja.Height / alto - 1e-3);
                    if (rs.Count > Math.Max(1, visibles)) caben = Math.Max(1, visibles);
                    if (caben < rs.Count && caben >= 1 && (sf.Trimming == StringTrimming.EllipsisCharacter || sf.Trimming == StringTrimming.EllipsisWord))
                        rs[Math.Min(caben, enteros) - 1] = ConPuntos(rs[Math.Min(caben, enteros) - 1] + "…", sk, util, sf.Trimming == StringTrimming.EllipsisWord);
                    if (caben < rs.Count) rs.RemoveRange(caben, rs.Count - caben);
                }
                if (!sinCaja)
                    for (int i = 0; i < rs.Count; i++)
                    {
                        if (sf.Trimming == StringTrimming.EllipsisCharacter || sf.Trimming == StringTrimming.EllipsisWord) rs[i] = ConPuntos(rs[i], sk, util, sf.Trimming == StringTrimming.EllipsisWord);
                        else if (noWrap && (sf.Trimming == StringTrimming.Character || sf.Trimming == StringTrimming.Word) && (sf.FormatFlags & StringFormatFlags.NoClip) == 0) rs[i] = Cortada(rs[i], sk, util);
                    }
                float altoBloque = rs.Count * alto;
                float y0 = caja.Y;
                if (!enPunto && caja.Height > 0)
                {
                    if (sf.LineAlignment == StringAlignment.Center) y0 = caja.Y + (caja.Height - altoBloque) / 2f;
                    else if (sf.LineAlignment == StringAlignment.Far) y0 = caja.Bottom - altoBloque;
                }
                else if (enPunto)
                {
                    if (sf.LineAlignment == StringAlignment.Center) y0 = caja.Y - altoBloque / 2f;
                    else if (sf.LineAlignment == StringAlignment.Far) y0 = caja.Y - altoBloque;
                }
                for (int i = 0; i < rs.Count; i++)
                {
                    string t = rs[i]; float w = Ancho(sk, t), x;
                    if (sinCaja)
                    {
                        if (sf.Alignment == StringAlignment.Center) x = caja.X - w / 2f;
                        else if (sf.Alignment == StringAlignment.Far) x = caja.X - w - m;
                        else x = caja.X + m;
                    }
                    else
                    {
                        if (sf.Alignment == StringAlignment.Center) x = caja.X + (caja.Width - w) / 2f;
                        else if (sf.Alignment == StringAlignment.Far) x = caja.Right - m - w;
                        else x = caja.X + m;
                    }
                    r.Add(new Pieza { Cadena = t, X = x, Base = y0 + i * alto + asc, Ancho = w });
                }
            }
            return r;
        }
        public static SizeF Mide(string s, Font f, SizeF caja, StringFormat sf, TextRenderingHint h)
        {
            if (string.IsNullOrEmpty(s)) return new SizeF(0, 0);
            sf = sf ?? StringFormat.GenericDefault;
            using (SKFont sk = f.Skia(h))
            {
                float m = Margen(f, sf), alto = f.GetHeight();
                bool noWrap = caja.Width <= 0 || (sf.FormatFlags & StringFormatFlags.NoWrap) != 0;
                List<string> rs = Renglones(s, sk, caja.Width > 0 ? Math.Max(0f, caja.Width - 2 * m) : 0f, noWrap);
                bool cuentaEspacios = (sf.FormatFlags & StringFormatFlags.MeasureTrailingSpaces) != 0;
                float w = 0f;
                foreach (string t in rs) w = Math.Max(w, Ancho(sk, cuentaEspacios ? t : t.TrimEnd(' ')));
                int n = rs.Count;
                if (caja.Height > 0) n = Math.Min(n, Math.Max(1, (int)Math.Ceiling(caja.Height / alto - 1e-3)));
                float ancho = w + 2 * m;
                if (caja.Width > 0) ancho = Math.Min(ancho, caja.Width);
                return new SizeF(ancho, n * alto);
            }
        }
    }
}

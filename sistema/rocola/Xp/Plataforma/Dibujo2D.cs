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
using System.Text;
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
        public FontFamily(string nombre) { if (!Letras.Instalada(nombre)) throw new ArgumentException("Font '" + nombre + "' cannot be found."); Name = nombre; }
        internal FontFamily(string nombre, bool sinMirar) { Name = nombre; }
        public FontFamily(GenericFontFamilies g) { Name = g == GenericFontFamilies.Serif ? "Times New Roman" : g == GenericFontFamilies.Monospace ? "Courier New" : "Microsoft Sans Serif"; }
        public void Dispose() { }
        // En Windows XP la generica sin remates es Microsoft Sans Serif; la de ancho fijo, Courier New; la de remates, Times New Roman.
        public static FontFamily GenericSansSerif { get { return new FontFamily("Microsoft Sans Serif", true); } }
        public static FontFamily GenericMonospace { get { return new FontFamily("Courier New", true); } }
        public static FontFamily GenericSerif { get { return new FontFamily("Times New Roman", true); } }
        // new Font("una que no esta", ...) no lanza: GDI+ pone Microsoft Sans Serif (la generica) en su lugar
        internal static FontFamily DeFont(string nombre) { return new FontFamily(nombre != null && Letras.Instalada(nombre) ? nombre : "Microsoft Sans Serif", true); }
        public override string ToString() { return Name; }
    }

    public sealed class Font : IDisposable
    {
        public readonly FontFamily FontFamily;
        public readonly float Size;          // en la unidad de Unit (de fabrica, puntos)
        public readonly FontStyle Style;
        public readonly GraphicsUnit Unit;
        public readonly Letras.Letra Cara; // la letra de verdad (y si la negrita hay que inventarla)
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
        // (.NET: con la unidad en puntos devuelve Size tal cual; si no, el alto en pixeles pasado a puntos a 96 por pulgada)
        public float SizeInPoints { get { return Unit == GraphicsUnit.Point ? Size : SizeInPixels * 72f / 96f; } }
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
        // Si la familia esta "instalada": alguna de sus letras esta en la carpeta de las letras de XP (se mira una vez por familia)
        private static readonly Dictionary<string, bool> instaladas = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public static bool Instalada(string familia)
        {
            if (string.IsNullOrEmpty(familia)) return false;
            lock (instaladas)
            {
                bool si;
                if (instaladas.TryGetValue(familia, out si)) return si;
                string[] a;
                if (archivos.TryGetValue(familia, out a)) foreach (string f in a) if (EnCarpetas(f) != null) { si = true; break; }
                instaladas[familia] = si;
                return si;
            }
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
        // una tabla de la letra (de la letra de GDI de TextRenderer: VDMX y hdmx), o null
        internal static byte[] Tabla(SKTypeface tf, string tabla) { try { return tf.GetTableData(Tag(tabla)); } catch { return null; } }
        internal static int U16(SKTypeface tf, string tabla, int at)
        {
            try { byte[] d = tf.GetTableData(Tag(tabla)); if (d == null || d.Length < at + 2) return 0; return (d[at] << 8) | d[at + 1]; } catch { return 0; }
        }
        internal static int S16(SKTypeface tf, string tabla, int at) { int v = U16(tf, tabla, at); return v >= 32768 ? v - 65536 : v; }
    }

    // ---------------------------------------------------------------- pinceles y plumas
    public abstract class Brush : IDisposable
    {
        // centros: el dibujo va con los centros de los pixeles en las coordenadas enteras (PixelOffsetMode de fabrica, None o HighSpeed)
        internal abstract void Pon(SKPaint p, bool centros);
        public virtual void Dispose() { }
        public abstract Brush Clone();
    }
    public sealed class SolidBrush : Brush
    {
        public Color Color;
        public SolidBrush(Color c) { Color = c; }
        internal override void Pon(SKPaint p, bool centros) { p.Shader = null; p.Color = Graphics.Sk(Color); }
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
            if (a.X == b.X && a.Y == b.Y) throw new OutOfMemoryException("Out of memory.");
            p0 = a; p1 = b; c0 = ca; c1 = cb; caja = RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }
        public LinearGradientBrush(Point a, Point b, Color ca, Color cb) : this((PointF)a, (PointF)b, ca, cb) { }
        // LOS MODOS SON ANGULOS (GdipCreateLineBrushFromRect): Horizontal 0, Vertical 90, ForwardDiagonal 45 y BackwardDiagonal 135, los
        // dos ultimos "escalables" (el angulo se ajusta a la proporcion de la caja: las rayas de un color van de esquina a esquina por la
        // otra diagonal, no perpendiculares a la diagonal del degradado).
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, LinearGradientMode m)
            : this(r, ca, cb, m == LinearGradientMode.Vertical ? 90f : m == LinearGradientMode.ForwardDiagonal ? 45f : m == LinearGradientMode.BackwardDiagonal ? 135f : 0f, true) { }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, LinearGradientMode m) : this((RectangleF)r, ca, cb, m) { }
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, float angulo) : this(r, ca, cb, angulo, false) { }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, float angulo) : this((RectangleF)r, ca, cb, angulo, false) { }
        public LinearGradientBrush(Rectangle r, Color ca, Color cb, float angulo, bool escalable) : this((RectangleF)r, ca, cb, angulo, escalable) { }
        public LinearGradientBrush(RectangleF r, Color ca, Color cb, float angulo, bool escalable)
        {
            // GDI+ (GdipCreateLineBrushFromRectWithAngle): la linea del degradado pasa por el centro de la caja con ese angulo, y sus
            // extremos son la proyeccion de las esquinas; "escalable": tan(angulo) se multiplica por ancho/alto
            if (r.Width == 0 || r.Height == 0) throw new OutOfMemoryException("Out of memory.");
            caja = r; c0 = ca; c1 = cb;
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
        public Color[] LinearColors { get { return new[] { c0, c1 }; } set { c0 = value[0]; c1 = value[1]; } }
        public RectangleF Rectangle { get { return caja; } }
        internal override void Pon(SKPaint p, bool centros)
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
            float rx = Math.Max(0.5f, caja.Width / 2f), ry = Math.Max(0.5f, caja.Height / 2f);
            float ex = caja.X + caja.Width / 2f, ey = caja.Y + caja.Height / 2f;   // el medio de la elipse
            bool corrido = Math.Abs(CenterPoint.X - ex) > 0.01f || Math.Abs(CenterPoint.Y - ey) > 0.01f;
            SKMatrix m = corrido ? SKMatrix.CreateScale(1f, ry / rx, ex, ey) : SKMatrix.CreateScale(1f, ry / rx, CenterPoint.X, CenterPoint.Y);
            SKColor[] cols; float[] pos = null;
            if (Blend != null && Blend.Factors != null && Blend.Positions != null && Blend.Factors.Length >= 2)
            {
                int n = Blend.Factors.Length; cols = new SKColor[n]; pos = new float[n];
                for (int i = 0; i < n; i++)   // Skia va del centro (0) a la orilla (1): al reves que el Blend de GDI+
                {
                    int j = n - 1 - i;
                    pos[i] = 1f - Blend.Positions[j];
                    cols[i] = Graphics.Sk(LinearGradientBrush.Mezcla(fuera, CenterColor, Blend.Factors[j]));
                }
            }
            else cols = new[] { Graphics.Sk(CenterColor), Graphics.Sk(fuera) };
            if (corrido)   // en el espacio del circulo (y estirada a rx alrededor del medio): del centro corrido al circulo entero
                p.Shader = SKShader.CreateTwoPointConicalGradient(new SKPoint(CenterPoint.X, ey + (CenterPoint.Y - ey) * rx / ry), 0f,
                    new SKPoint(ex, ey), rx, cols, pos, SKShaderTileMode.Clamp, m);
            else p.Shader = SKShader.CreateRadialGradient(new SKPoint(CenterPoint.X, CenterPoint.Y), rx, cols, pos, SKShaderTileMode.Clamp, m);
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
            SKShaderTileMode tx = Graphics.Envoltura(WrapMode, true), ty = Graphics.Envoltura(WrapMode, false);
            SKMatrix m = centros ? mat.PreConcat(SKMatrix.CreateTranslation(-0.5f, -0.5f)) : mat;
            p.Shader = SKShader.CreateBitmap(img.Sk, tx, ty, m);
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
        internal void Pon(SKPaint p, bool suave, bool centros)
        {
            p.Style = SKPaintStyle.Stroke;
            if (Brush != null) Brush.Pon(p, centros); else { p.Shader = null; p.Color = Graphics.Sk(Color); }
            p.StrokeWidth = Width <= 1f ? Math.Max(Width, 1f) : Width;   // GDI+: una pluma de 0 o 1 es de un pixel
            p.StrokeJoin = LineJoin == LineJoin.Round ? SKStrokeJoin.Round : LineJoin == LineJoin.Bevel ? SKStrokeJoin.Bevel : SKStrokeJoin.Miter;
            p.StrokeMiter = 10f;   // el limite de las esquinas en punta de GDI+ (MiterLimit de fabrica); el de Skia es 4 y las achataba antes
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
            using (SKFont sk = f.Skia(TextRenderingHint.AntiAlias))
                foreach (Texto.Pieza pz in Texto.Piezas(s, f, caja, sf, sk))
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
        public StringAlignment Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near;
        public StringFormatFlags FormatFlags;
        public StringTrimming Trimming = StringTrimming.Character;
        public HotkeyPrefix HotkeyPrefix = HotkeyPrefix.None;
        internal bool Tipografico;   // GenericTypographic (y sus copias): sin el margen de 1/6 de letra
        public StringFormat() { }
        public StringFormat(StringFormatFlags f) { FormatFlags = f; }
        public StringFormat(StringFormat o) { Alignment = o.Alignment; LineAlignment = o.LineAlignment; FormatFlags = o.FormatFlags; Trimming = o.Trimming; HotkeyPrefix = o.HotkeyPrefix; Tipografico = o.Tipografico; }
        // LOS DOS FORMATOS GENERICOS SON UN SOLO OBJETO CADA UNO, COMPARTIDO (revision del 8-11-2026). En GDI+,
        // GdipStringFormatGetGenericDefault y GdipStringFormatGetGenericTypographic devuelven siempre el mismo objeto; .NET lo envuelve sin
        // copiarlo (StringFormat.cs de System.Drawing) y su Dispose no lo borra (prueba test_getgenerictypographic de Wine, que pasa en
        // Windows: se le cambian las banderas, se suelta, se vuelve a pedir y sale cambiado). Lo que el codigo le cambie a
        // StringFormat.GenericTypographic se queda para todos los que lo pidan despues, y las copias que se hagan de el salen con el cambio.
        // XP lo hace: EstadoReposoGpu.cs (la cola del estado extendido) le pone Center, Center y NoWrap al tipografico, y desde ese momento
        // Espaciado (AtraccionGpu.cs) y los formatos del buscador de portadas (BuscadorPortadasGpu.cs, si se crean despues) los heredan.
        // El tipografico trae NoFitBlackBox (FitBlackBox en .NET), LineLimit y NoClip, y Trimming None; el normal, nada y Character.
        private static readonly StringFormat genericoNormal = new StringFormat();
        private static readonly StringFormat genericoTipografico = new StringFormat { FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.LineLimit | StringFormatFlags.NoClip, Trimming = StringTrimming.None, Tipografico = true };
        public static StringFormat GenericDefault { get { return genericoNormal; } }
        public static StringFormat GenericTypographic { get { return genericoTipografico; } }
        // el de DrawString y MeasureString sin formato (null): el de fabrica de GDI+, aparte del generico (que el codigo puede cambiar)
        internal static readonly StringFormat SinFormato = new StringFormat();
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
        // GDI+ multiplica el color como fila (r g b a 1) por la matriz; Skia, la matriz por la columna; los dos con el desplazamiento en 0..1
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
        public virtual void Dispose() { if (Sk != null) { Sk.Dispose(); Sk = null; } }
        public void Save(string ruta) { Save(ruta, ruta.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png); }
        public void Save(string ruta, ImageFormat f) { using (FileStream fs = File.Create(ruta)) Save(fs, f); }
        public void Save(Stream s, ImageFormat f) { Guarda(s, f, 92); }
        public void Save(string ruta, ImageCodecInfo codec, EncoderParameters ep)
        {
            int calidad = 75;   // la de fabrica del codificador JPEG de GDI+
            if (ep != null && ep.Param != null) foreach (EncoderParameter p in ep.Param) if (p != null && p.Encoder == Encoder.Quality) calidad = (int)Math.Max(0, Math.Min(100, p.Valor));
            using (FileStream fs = File.Create(ruta)) Guarda(fs, codec != null ? codec.Formato : ImageFormat.Png, calidad);
        }
        private void Guarda(Stream s, ImageFormat f, int calidad)
        {
            using (SKImage i = SKImage.FromBitmap(Sk))
            using (SKData d = i.Encode(f == ImageFormat.Jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, calidad)) d.SaveTo(s);
        }
    }
    public sealed class Bitmap : Image
    {
        public Bitmap(int w, int h) : this(w, h, PixelFormat.Format32bppArgb) { }
        public Bitmap(int w, int h, PixelFormat f)
        {
            Sk = new SKBitmap(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Bgra8888, SKAlphaType.Premul));
            Sk.Erase(SKColors.Transparent); PixelFormat = f; TraeAlfa = IsAlphaPixelFormat(f);
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
            PixelFormat f = PixelFormat.Format32bppArgb; bool alfa = true;
            try
            {
                using (SKCodec codec = SKCodec.Create(new SKMemoryStream(datos)))
                    if (codec != null)
                    {
                        if (codec.EncodedFormat == SKEncodedImageFormat.Gif) { f = PixelFormat.Format8bppIndexed; alfa = false; }
                        else if (codec.Info.AlphaType == SKAlphaType.Opaque) { f = PixelFormat.Format24bppRgb; alfa = false; }
                    }
            }
            catch { }
            using (SKBitmap b = SKBitmap.Decode(datos))
            {
                if (b == null) throw new ArgumentException("no es una imagen");
                SKBitmap c = new SKBitmap(new SKImageInfo(b.Width, b.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                using (SKCanvas cv = new SKCanvas(c)) { cv.Clear(SKColors.Transparent); cv.DrawBitmap(b, 0, 0); }
                Bitmap r = new Bitmap(c, f); r.TraeAlfa = alfa;
                return r;
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
        // LOS PUNTOS PARA LEERLOS O ESCRIBIRLOS (LockBits). En Format32bppPArgb, que es como se guardan aqui, el puntero a los puntos mismos
        // desde la esquina del trozo y con el paso de la imagen (como GDI+ con su propio formato); en los demas, una copia convertida fuera
        // del monton de .NET, que se devuelve a la imagen al soltarla si era para escribir. La conversion va con tablas (revision del
        // 8-11-2026: antes dividia punto a punto y en el A4 costaba): sin premultiplicar = min(255, c*255/a), premultiplicar = c*a/255,
        // los dos truncando, lo mismo que antes.
        private static readonly byte[] tablaQuita = Tabla(true), tablaPon = Tabla(false);
        private static byte[] Tabla(bool quita)
        {
            byte[] t = new byte[65536];
            for (int a = 0; a < 256; a++)
                for (int c = 0; c < 256; c++)
                    t[(a << 8) | c] = quita ? (byte)(a == 0 || a == 255 ? c : Math.Min(255, c * 255 / a)) : (byte)(c * a / 255);
            return t;
        }
        public unsafe BitmapData LockBits(Rectangle r, ImageLockMode modo, PixelFormat f)
        {
            BitmapData d = new BitmapData { Width = r.Width, Height = r.Height, PixelFormat = f, modo = modo, zona = r };
            int rb = Sk.RowBytes; byte* o = (byte*)Sk.GetPixels();
            if (f == PixelFormat.Format32bppPArgb) { d.Stride = rb; d.Scan0 = (IntPtr)(o + (long)r.Y * rb + (long)r.X * 4); d.directo = true; return d; }
            int bpp = f == PixelFormat.Format24bppRgb ? 3 : 4, stride = ((r.Width * bpp) + 3) & ~3;
            d.Stride = stride; d.memoria = (IntPtr)NativeMemory.Alloc((nuint)Math.Max(1L, (long)stride * r.Height)); d.Scan0 = d.memoria;
            bool rgb = f == PixelFormat.Format32bppRgb;
            fixed (byte* q = tablaQuita)
                for (int y = 0; y < r.Height; y++)
                {
                    uint* s = (uint*)(o + (long)(r.Y + y) * rb + (long)r.X * 4);
                    byte* t = (byte*)d.memoria + (long)y * stride;
                    if (bpp == 4)
                    {
                        uint* tt = (uint*)t;
                        for (int x = 0; x < r.Width; x++)
                        {
                            uint v = s[x], a = v >> 24;
                            if (a != 255 && a != 0) { byte* qa = q + (a << 8); v = (a << 24) | ((uint)qa[(v >> 16) & 255] << 16) | ((uint)qa[(v >> 8) & 255] << 8) | qa[v & 255]; }
                            tt[x] = rgb ? v | 0xFF000000u : v;
                        }
                    }
                    else
                        for (int x = 0; x < r.Width; x++)
                        {
                            uint v = s[x]; byte* qa = q + ((v >> 24) << 8);
                            t[x * 3] = qa[v & 255]; t[x * 3 + 1] = qa[(v >> 8) & 255]; t[x * 3 + 2] = qa[(v >> 16) & 255];
                        }
                }
            return d;
        }
        public unsafe void UnlockBits(BitmapData d)
        {
            if (d == null) return;
            if (d.directo) { if ((d.modo & ImageLockMode.WriteOnly) != 0) Sk.NotifyPixelsChanged(); d.directo = false; return; }
            if (d.memoria == IntPtr.Zero) return;
            if ((d.modo & ImageLockMode.WriteOnly) != 0)
            {
                int bpp = d.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4; Rectangle r = d.zona;
                byte* o = (byte*)Sk.GetPixels(); int rb = Sk.RowBytes;
                bool rgb = d.PixelFormat == PixelFormat.Format32bppRgb;
                fixed (byte* pp = tablaPon)
                    for (int y = 0; y < r.Height; y++)
                    {
                        uint* t = (uint*)(o + (long)(r.Y + y) * rb + (long)r.X * 4);
                        byte* s = (byte*)d.memoria + (long)y * d.Stride;
                        if (bpp == 4)
                        {
                            uint* ss = (uint*)s;
                            for (int x = 0; x < r.Width; x++)
                            {
                                uint v = ss[x]; if (rgb) v |= 0xFF000000u;
                                uint a = v >> 24;
                                if (a != 255) { byte* pa = pp + (a << 8); v = (a << 24) | ((uint)pa[(v >> 16) & 255] << 16) | ((uint)pa[(v >> 8) & 255] << 8) | pa[v & 255]; }
                                t[x] = v;
                            }
                        }
                        else
                            for (int x = 0; x < r.Width; x++) t[x] = 0xFF000000u | ((uint)s[x * 3 + 2] << 16) | ((uint)s[x * 3 + 1] << 8) | s[x * 3];
                    }
                Sk.NotifyPixelsChanged();
            }
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
        public static Graphics FromImage(Image i) { return new Graphics(new SKCanvas(i.Sk), true); }
        public void Dispose() { if (propio) { C.Flush(); C.Dispose(); } }
        public void Flush() { C.Flush(); }

        internal static SKColor Sk(Color c) { return new SKColor(c.R, c.G, c.B, c.A); }
        private bool Suave { get { return SmoothingMode == SmoothingMode.AntiAlias || SmoothingMode == SmoothingMode.HighQuality; } }
        // los centros de los pixeles en las coordenadas enteras: PixelOffsetMode de fabrica (Default, HighSpeed o None); con Half o
        // HighQuality, en el medio, como en Skia
        private bool Centros { get { return PixelOffsetMode != PixelOffsetMode.Half && PixelOffsetMode != PixelOffsetMode.HighQuality; } }
        // el pincel de una forma; 'corrido': lo que el lienzo ya va corrido (Corre)
        private SKPaint Relleno(Brush b) { return Relleno(b, Corrimiento); }
        private SKPaint Relleno(Brush b, float corrido)
        {
            SKPaint p = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = Suave };
            b.Pon(p, Centros);
            AjustaPincel(p, corrido);
            if (CompositingMode == CompositingMode.SourceCopy) p.BlendMode = SKBlendMode.Src;
            return p;
        }
        private SKPaint Trazo(Pen pen)
        {
            SKPaint p = new SKPaint(); pen.Pon(p, Suave, Centros);
            AjustaPincel(p, Corrimiento);
            if (CompositingMode == CompositingMode.SourceCopy) p.BlendMode = SKBlendMode.Src;
            return p;
        }
        // EL COLOR DE UN PINCEL (degradado, textura) EN CADA PIXEL ES EL DE SU CENTRO. En GDI+ (sin medio pixel) ese centro esta en las
        // coordenadas enteras; Skia lo toma en el medio (i + 0,5): un degradado salia medio pixel corrido. El lienzo ya va corrido lo que
        // diga 'corrido' (Corre); lo que falte hasta medio pixel se corre aqui el dibujo del pincel, en la pantalla.
        private void AjustaPincel(SKPaint p, float corrido)
        {
            if (p.Shader == null || !Centros) return;
            float e = 0.5f - corrido;
            if (e == 0f) return;
            SKMatrix ctm = C.TotalMatrix, inv;
            if (!ctm.TryInvert(out inv)) return;
            SKShader viejo = p.Shader;
            p.Shader = viejo.WithLocalMatrix(inv.PreConcat(SKMatrix.CreateTranslation(e, e)).PreConcat(ctm));
            viejo.Dispose();
        }
        // la envoltura de GDI+ en Skia: Clamp no repite nada (fuera, transparente: Decal); Tile repite; TileFlipX/Y/XY repiten en espejo
        internal static SKShaderTileMode Envoltura(WrapMode w, bool x)
        {
            if (w == WrapMode.Clamp) return SKShaderTileMode.Decal;
            bool espejo = w == WrapMode.TileFlipXY || (x ? w == WrapMode.TileFlipX : w == WrapMode.TileFlipY);
            return espejo ? SKShaderTileMode.Mirror : SKShaderTileMode.Repeat;
        }
        private static SKRect R(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }
        // EL MEDIO PIXEL DE GDI+ AL SUAVIZAR (revision del 7-11-2026). Con PixelOffsetMode de fabrica (Default, HighSpeed o None) GDI+ pone
        // el centro de cada pixel en las coordenadas ENTERAS ("pixel centers have integer coordinates"); Skia, en el medio (i + 0.5). Con
        // suavizado eso se ve: en XP una raya de 1 en una coordenada entera sale nitida y el borde de un relleno en una entera sale a medias;
        // en Skia salia al reves (la raya del riel de las letras, en dos filas a medias). Las formas suavizadas se corren medio pixel en la
        // pantalla (despues de la transformacion, donde lo hace GDI+), como libgdiplus con Cairo. Con Half o HighQuality no. El texto no se
        // toca (las imagenes van aparte: Imagen).
        // Y SIN SUAVIZAR (revision del 8-11-2026): GDI+ pinta los pixeles cuyo centro (en los enteros) cae dentro de la forma, con las
        // coordenadas a dieciseisavos de pixel: un relleno desde x = 10,3 empieza en el pixel 11; Skia redondea y empezaba en el 10. Con las
        // formas sin suavizar el lienzo se corre 15/32 de pixel: Skia cubre entonces lo mismo que GDI+ (en las coordenadas enteras, lo mismo
        // que sin correr). Los pinceles se compensan (AjustaPincel), para que su color siga siendo el del centro del pixel.
        private struct Corrido : IDisposable
        {
            private readonly SKCanvas c; private readonly int n;
            internal Corrido(SKCanvas c, int n) { this.c = c; this.n = n; }
            public void Dispose() { if (c != null) c.RestoreToCount(n); }
        }
        // lo que Corre corre el lienzo, en pixeles de la pantalla: medio con suavizado, 15/32 sin el, nada con Half o HighQuality
        private float Corrimiento { get { return !Centros ? 0f : Suave ? 0.5f : 15f / 32f; } }
        private Corrido Corre() { return Corre(Corrimiento); }
        private Corrido Corre(float d)
        {
            if (d == 0f) return default(Corrido);
            int n = C.Save();
            SKMatrix m = SKMatrix.CreateTranslation(d, d).PreConcat(C.TotalMatrix);
            C.SetMatrix(in m);
            return new Corrido(C, n);
        }

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
            SKMatrix m = C.TotalMatrix;
            C.RestoreToCount(baseCuenta); C.SetMatrix(m);   // (el recorte se va; la posicion se queda)
            conRecorte = false; recorte = null;
        }
        // LOS LIMITES DEL RECORTE, EXACTOS. LocalClipBounds de Skia los agranda un pixel por cada lado (por si el recorte fuera suavizado);
        // GDI+ no. Con ese pixel de mas, el renglon de al lado de un recorte "tocaba" el recorte: la lista del disco, al pintar el renglon
        // elegido, se quedaba tambien con la cinta del renglon de abajo (TextoMovilGpu.cs) y ese renglon salia escrito dos veces, encimado.
        public RectangleF ClipBounds
        {
            get
            {
                SKRectI d = C.DeviceClipBounds; SKMatrix inv;
                if (d.IsEmpty || !C.TotalMatrix.TryInvert(out inv)) return RectangleF.Empty;
                SKRect b = inv.MapRect(new SKRect(d.Left, d.Top, d.Right, d.Bottom));
                return RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom);
            }
        }
        public RectangleF VisibleClipBounds { get { return ClipBounds; } }

        // --- formas
        public void FillRectangle(Brush b, RectangleF r) { using (Corre()) using (SKPaint p = Relleno(b)) { if (!Suave) p.IsAntialias = false; C.DrawRect(R(r), p); } }
        public void FillRectangle(Brush b, Rectangle r) { FillRectangle(b, (RectangleF)r); }
        public void FillRectangle(Brush b, float x, float y, float w, float h) { FillRectangle(b, new RectangleF(x, y, w, h)); }
        public void FillRectangle(Brush b, int x, int y, int w, int h) { FillRectangle(b, new RectangleF(x, y, w, h)); }
        public void FillRectangles(Brush b, RectangleF[] rs) { foreach (RectangleF r in rs) FillRectangle(b, r); }
        public void DrawRectangle(Pen pen, RectangleF r) { using (Corre()) using (SKPaint p = Trazo(pen)) C.DrawRect(R(r), p); }
        public void DrawRectangle(Pen pen, Rectangle r) { DrawRectangle(pen, (RectangleF)r); }
        public void DrawRectangle(Pen pen, float x, float y, float w, float h) { DrawRectangle(pen, new RectangleF(x, y, w, h)); }
        public void DrawRectangle(Pen pen, int x, int y, int w, int h) { DrawRectangle(pen, new RectangleF(x, y, w, h)); }
        public void FillEllipse(Brush b, RectangleF r) { using (Corre()) using (SKPaint p = Relleno(b)) C.DrawOval(R(r), p); }
        public void FillEllipse(Brush b, Rectangle r) { FillEllipse(b, (RectangleF)r); }
        public void FillEllipse(Brush b, float x, float y, float w, float h) { FillEllipse(b, new RectangleF(x, y, w, h)); }
        public void FillEllipse(Brush b, int x, int y, int w, int h) { FillEllipse(b, new RectangleF(x, y, w, h)); }
        public void DrawEllipse(Pen pen, RectangleF r) { using (Corre()) using (SKPaint p = Trazo(pen)) C.DrawOval(R(r), p); }
        public void DrawEllipse(Pen pen, Rectangle r) { DrawEllipse(pen, (RectangleF)r); }
        public void DrawEllipse(Pen pen, float x, float y, float w, float h) { DrawEllipse(pen, new RectangleF(x, y, w, h)); }
        public void DrawEllipse(Pen pen, int x, int y, int w, int h) { DrawEllipse(pen, new RectangleF(x, y, w, h)); }
        public void FillPath(Brush b, GraphicsPath gp) { using (Corre()) using (SKPaint p = Relleno(b)) C.DrawPath(gp.P, p); }
        public void DrawPath(Pen pen, GraphicsPath gp) { using (Corre()) using (SKPaint p = Trazo(pen)) C.DrawPath(gp.P, p); }
        // LAS REGIONES DE GDI+ SON DE PIXELES ENTEROS: FillRegion no se suaviza aunque SmoothingMode lo pida (la region se pasa a
        // rectangulos de pixeles, como el recorte). La luna de la cabecera de la atraccion (AtraccionGpu.cs, Luna) sale asi en XP.
        public void FillRegion(Brush b, Region rg)
        {
            float d = Centros ? 15f / 32f : 0f;
            using (Corre(d)) using (SKPaint p = Relleno(b, d)) { p.IsAntialias = false; C.DrawPath(rg.P, p); }
        }
        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2) { using (Corre()) using (SKPaint p = Trazo(pen)) C.DrawLine(x1, y1, x2, y2, p); }
        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) { DrawLine(pen, (float)x1, y1, x2, y2); }
        public void DrawLine(Pen pen, PointF a, PointF b) { DrawLine(pen, a.X, a.Y, b.X, b.Y); }
        public void DrawLine(Pen pen, Point a, Point b) { DrawLine(pen, a.X, a.Y, b.X, b.Y); }
        public void DrawLines(Pen pen, PointF[] ps) { if (ps == null || ps.Length < 2) return; using (SKPath sp = new SKPath()) { sp.MoveTo(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) sp.LineTo(ps[i].X, ps[i].Y); using (Corre()) using (SKPaint p = Trazo(pen)) C.DrawPath(sp, p); } }
        public void DrawLines(Pen pen, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; DrawLines(pen, f); }
        private static SKPath Poligono(PointF[] ps, FillMode m) { SKPath sp = new SKPath { FillType = m == FillMode.Alternate ? SKPathFillType.EvenOdd : SKPathFillType.Winding }; sp.MoveTo(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) sp.LineTo(ps[i].X, ps[i].Y); sp.Close(); return sp; }
        public void FillPolygon(Brush b, PointF[] ps) { FillPolygon(b, ps, FillMode.Alternate); }
        public void FillPolygon(Brush b, PointF[] ps, FillMode m) { if (ps == null || ps.Length < 3) return; using (SKPath sp = Poligono(ps, m)) using (Corre()) using (SKPaint p = Relleno(b)) C.DrawPath(sp, p); }
        public void FillPolygon(Brush b, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; FillPolygon(b, f); }
        public void DrawPolygon(Pen pen, PointF[] ps) { if (ps == null || ps.Length < 2) return; using (SKPath sp = Poligono(ps, FillMode.Alternate)) using (Corre()) using (SKPaint p = Trazo(pen)) C.DrawPath(sp, p); }
        public void DrawPolygon(Pen pen, Point[] ps) { if (ps == null) return; PointF[] f = new PointF[ps.Length]; for (int i = 0; i < ps.Length; i++) f[i] = ps[i]; DrawPolygon(pen, f); }
        public void DrawArc(Pen pen, RectangleF r, float inicio, float barrido) { using (Corre()) using (SKPaint p = Trazo(pen)) C.DrawArc(R(r), inicio, barrido, false, p); }
        public void DrawArc(Pen pen, Rectangle r, float inicio, float barrido) { DrawArc(pen, (RectangleF)r, inicio, barrido); }
        public void DrawArc(Pen pen, float x, float y, float w, float h, float inicio, float barrido) { DrawArc(pen, new RectangleF(x, y, w, h), inicio, barrido); }
        public void FillPie(Brush b, RectangleF r, float inicio, float barrido) { using (Corre()) using (SKPaint p = Relleno(b)) C.DrawArc(R(r), inicio, barrido, true, p); }
        public void FillPie(Brush b, float x, float y, float w, float h, float inicio, float barrido) { FillPie(b, new RectangleF(x, y, w, h), inicio, barrido); }
        public void DrawBezier(Pen pen, PointF a, PointF b, PointF c, PointF d) { using (SKPath sp = new SKPath()) { sp.MoveTo(a.X, a.Y); sp.CubicTo(b.X, b.Y, c.X, c.Y, d.X, d.Y); using (Corre()) using (SKPaint p = Trazo(pen)) C.DrawPath(sp, p); } }

        // --- imagenes
        // HighQualityBilinear, HighQualityBicubic y High de GDI+ "prefiltran" al achicar: una portada de 500 px achicada a 20 px sale
        // lisa (Form1.Vistas.cs: "sin que salga a granos"); un bilineal o un bicubico de Skia sin mas toma cuatro o dieciseis puntos y
        // sale a granos. Al achicar, esos modos van con mipmaps (cada nivel es la mitad, promediada). Bilinear y Bicubic no prefiltran,
        // como en GDI+.
        private SKSamplingOptions Muestreo(SKRect origen, SKRect destino)
        {
            bool previo = InterpolationMode == InterpolationMode.HighQualityBilinear || InterpolationMode == InterpolationMode.HighQualityBicubic || InterpolationMode == InterpolationMode.High;
            if (previo)
            {
                SKMatrix m = C.TotalMatrix;
                float ex = (float)Math.Sqrt(m.ScaleX * m.ScaleX + m.SkewY * m.SkewY), ey = (float)Math.Sqrt(m.SkewX * m.SkewX + m.ScaleY * m.ScaleY);
                if (destino.Width * ex < origen.Width * 0.99f || destino.Height * ey < origen.Height * 0.99f)
                    return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            }
            switch (InterpolationMode)
            {
                case InterpolationMode.NearestNeighbor: return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
                case InterpolationMode.HighQualityBicubic: case InterpolationMode.Bicubic: case InterpolationMode.High: return new SKSamplingOptions(SKCubicResampler.Mitchell);
                default: return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
            }
        }
        // DIBUJAR UNA IMAGEN COMO GDI+ (revision del 8-11-2026):
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
        private void Imagen(Image img, SKRect origen, SKRect destino, ImageAttributes ia)
        {
            if (img == null || img.Sk == null) return;
            if (origen.Width <= 0 || origen.Height <= 0 || destino.Width == 0 || destino.Height == 0) return;
            SKBitmap fuente = img.Sk, conClave = null;
            int zx = 0, zy = 0;   // donde empieza 'fuente' dentro de la imagen (la copia con la clave es un trozo)
            try
            {
                if (ia != null && ia.HayClave)
                {
                    // la clave va antes que todo (como GDI+): en una copia del trozo que se pinta, con dos puntos alrededor (los que mira el filtro)
                    SKRectI o = SKRectI.Intersect(new SKRectI((int)Math.Floor(origen.Left) - 2, (int)Math.Floor(origen.Top) - 2, (int)Math.Ceiling(origen.Right) + 2, (int)Math.Ceiling(origen.Bottom) + 2), new SKRectI(0, 0, img.Width, img.Height));
                    if (o.Width <= 0 || o.Height <= 0) return;
                    conClave = ConClave(img.Sk, o, ia.ClaveBaja, ia.ClaveAlta);
                    fuente = conClave; zx = o.Left; zy = o.Top;
                }
                using (SKPaint p = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill })
                using (SKImage si = SKImage.FromPixels(fuente.Info, fuente.GetPixels(), fuente.RowBytes))
                {
                    if (si == null) return;
                    if (CompositingMode == CompositingMode.SourceCopy) p.BlendMode = SKBlendMode.Src;
                    if (ia != null && ia.Matriz != null) p.ColorFilter = SKColorFilter.CreateColorMatrix(ia.Matriz.Skia());
                    SKMatrix ctm = C.TotalMatrix;
                    float kx = destino.Width / origen.Width, ky = destino.Height / origen.Height;
                    if (kx == 1f && ky == 1f && ctm.ScaleX == 1f && ctm.ScaleY == 1f && ctm.SkewX == 0f && ctm.SkewY == 0f && ctm.Persp0 == 0f && ctm.Persp1 == 0f
                        && Entero(destino.Left + ctm.TransX) && Entero(destino.Top + ctm.TransY) && Entero(origen.Left) && Entero(origen.Top))
                    {
                        C.DrawImage(si, new SKRect(origen.Left - zx, origen.Top - zy, origen.Right - zx, origen.Bottom - zy), destino, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), p);
                        return;
                    }
                    SKSamplingOptions muestreo = Muestreo(origen, destino);
                    WrapMode w = ia != null ? ia.Envoltura : WrapMode.Clamp;
                    SKShaderTileMode tx = Envoltura(w, true), ty = Envoltura(w, false);
                    // de la imagen a la caja de destino (en las coordenadas del dibujo), y del trozo a la imagen
                    SKMatrix d = SKMatrix.CreateTranslation(destino.Left - origen.Left * kx, destino.Top - origen.Top * ky).PreConcat(SKMatrix.CreateScale(kx, ky));
                    SKMatrix z = SKMatrix.CreateTranslation(zx, zy);
                    if (!Centros) { PintaImagen(si, fuente, tx, ty, muestreo, ctm, d.PreConcat(z), destino, p); return; }
                    // pantalla e imagen medio punto atras: del trozo a la pantalla = T(0,5) * ctm * d * T(-0,5) * z; y la caja, corrida
                    // medio pixel menos 1/32 en la pantalla (el lienzo se corre para pintarla, y el dibujo de la imagen se compensa)
                    SKMatrix total = SKMatrix.CreateTranslation(0.5f, 0.5f).PreConcat(ctm).PreConcat(d).PreConcat(SKMatrix.CreateTranslation(-0.5f, -0.5f)).PreConcat(z);
                    SKMatrix corrida = SKMatrix.CreateTranslation(15f / 32f, 15f / 32f).PreConcat(ctm), inv;
                    if (!corrida.TryInvert(out inv)) return;
                    int n = C.Save();
                    try { C.SetMatrix(in corrida); PintaImagen(si, fuente, tx, ty, muestreo, corrida, inv.PreConcat(total), destino, p); }
                    finally { C.RestoreToCount(n); }
                }
            }
            finally { if (conClave != null) conClave.Dispose(); }
        }
        // PINTA LA CAJA CON LA IMAGEN (local: de la imagen al lienzo; lienzo: la matriz del lienzo ahora). Lo transparente de fuera (Decal)
        // cuesta en Skia cinco veces mas que repetir el borde (Clamp): 12 ms en vez de 2,3 para estirar 256x192 a 1024x768. Como los dos
        // solo se distinguen donde el filtro toca fuera de la imagen, el centro se pinta con Clamp y solo la franja del borde con Decal
        // (dos recortes complementarios: cada pixel se pinta una vez, y sale igual que todo con Decal).
        private void PintaImagen(SKImage si, SKBitmap fuente, SKShaderTileMode tx, SKShaderTileMode ty, SKSamplingOptions muestreo, SKMatrix lienzo, SKMatrix local, SKRect destino, SKPaint p)
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
            sf = sf ?? StringFormat.SinFormato;
            bool noClip = (sf.FormatFlags & StringFormatFlags.NoClip) != 0;
            // una caja con medida negativa borra el texto (salvo NoClip), como en GDI+ (Wine, GdipDrawString)
            if (!noClip && r.Width != 0 && r.Height != 0 && (r.Width < 0 || r.Height < 0)) return;
            using (SKFont sk = f.Skia(TextRenderingHint))
            {
                Texto.Acomodo a = Texto.Acomoda(s, f, r, sf, sk);
                if (a.Piezas.Count == 0) return;
                bool recorta = !noClip && r.Width > 0 && r.Height > 0;
                int n = 0; if (recorta) { n = C.Save(); C.ClipRect(R(r)); }
                using (SKPaint p = Relleno(b, 0f))
                {
                    p.IsAntialias = TextRenderingHint != TextRenderingHint.SingleBitPerPixel && TextRenderingHint != TextRenderingHint.SingleBitPerPixelGridFit;
                    foreach (Texto.Pieza x in a.Piezas)
                    {
                        C.DrawText(x.Cadena, x.X, x.Base, sk, p);
                        if (f.Underline) { float grosor = Math.Max(1f, f.SizeInPixels / 14f); C.DrawRect(new SKRect(x.X, x.Base + grosor, x.X + x.Ancho, x.Base + 2 * grosor), p); }
                    }
                }
                if (recorta) C.RestoreToCount(n);
            }
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
            using (SKFont sk = f.Skia(TextRenderingHint))
                return Texto.Mide(Texto.Acomoda(s, f, new RectangleF(0, 0, caja.Width, caja.Height), sf ?? StringFormat.SinFormato, sk), caja, sf ?? StringFormat.SinFormato, out letras, out lineas);
        }

        // EL TEXTO DE GDI (TextRenderer): los trozos que da DrawTextGdi, pintados en los pixeles del HDC (la matriz de la base, no la que
        // el codigo le puso al Graphics), cada letra en su pixel entero con el ancho de GDI, opaco, sobre lo que haya; la negrita inventada
        // de GDI es la letra pintada otra vez un pixel a la derecha, y la letra del "&" lleva su raya de un pixel debajo.
        internal void TextoGdi(LetraGdi l, List<DrawTextGdi.Trozo> trozos, SKRectI? caja, Color c, bool alias, bool letras, bool rayas)
        {
            if (trozos.Count == 0) return;
            int n = C.Save();
            try
            {
                C.SetMatrix(in baseMatriz);
                if (caja.HasValue) C.ClipRect(new SKRect(caja.Value.Left, caja.Value.Top, caja.Value.Right, caja.Value.Bottom), SKClipOperation.Intersect, false);
                SKFont sk = alias ? l.SkAlias : l.Sk;
                using (SKPaint p = new SKPaint { Color = new SKColor(c.R, c.G, c.B, 255), IsAntialias = !alias, Style = SKPaintStyle.Fill })
                    foreach (DrawTextGdi.Trozo t in trozos)
                    {
                        if (letras && t.Cadena.Length > 0)
                        {
                            ushort[] glifos; SKPoint[] pos;
                            l.Posiciones(t.Cadena, t.X, t.Y + l.Asc, out glifos, out pos);
                            if (glifos.Length > 0)
                                using (SKTextBlobBuilder bb = new SKTextBlobBuilder())
                                {
                                    bb.AddPositionedRun(glifos, sk, pos);
                                    using (SKTextBlob blob = bb.Build())
                                        if (blob != null)
                                        {
                                            C.DrawText(blob, 0, 0, p);
                                            if (l.Negrita) C.DrawText(blob, 1, 0, p);
                                        }
                                }
                        }
                        if (rayas && t.Prefijo >= 0 && t.Prefijo < t.Cadena.Length)
                        {
                            // TEXT_DrawUnderscore: de x + ancho(antes) a x + ancho(con la letra) - 1, sin el ultimo punto, en y + ascenso + 1
                            int x0 = t.X + l.Extension(t.Cadena, 0, t.Prefijo), x1 = t.X + l.Extension(t.Cadena, 0, t.Prefijo + 1) - 1, yr = t.Y + l.Asc + 1;
                            if (caja.HasValue)
                            {
                                SKRectI k = caja.Value;
                                if (x0 > k.Right || x1 < k.Left || yr < k.Top || yr > k.Bottom) continue;
                                if (x0 < k.Left) x0 = k.Left; if (x1 > k.Right) x1 = k.Right;
                            }
                            if (x1 > x0) C.DrawRect(new SKRect(x0, yr, x1, yr + 1), p);
                        }
                    }
            }
            finally { C.RestoreToCount(n); }
        }
    }

    // ---------------------------------------------------------------- el texto de GDI (TextRenderer)
    // TEXTRENDERER ES GDI, NO GDI+ (revision del 8-11-2026). WinForms pinta y mide con DrawTextEx de user32 y una letra de GDI (la fuente
    // de dotnet/winforms, MIT, que sigue a .NET Framework: TextRenderer.cs, TextExtensions.cs, FontCache.Data.cs). Aqui igual: LetraGdi
    // es la letra de GDI y DrawTextGdi el DrawTextEx de user32 como lo replica Wine (dlls/user32/text.c, con sus pruebas contra Windows).
    //   - DrawText con rectangulo y sin banderas centra en los dos sentidos (HorizontalCenter | VerticalCenter); con un punto, no; la caja
    //     de un punto es (x, y, int.MaxValue, int.MaxValue). MeasureText sin banderas lleva Bottom.
    //   - Margenes (GlyphOverhangPadding, el de fabrica): ceil(alto/6) a la izquierda y ceil(alto/6 * 1,5) a la derecha, con el alto de
    //     la letra de GDI (tmHeight); LeftAndRightPadding ceil(2*alto/6) y ceil(alto/6 * 2,5); NoPadding, nada.
    //   - Varios renglones centrados o abajo los alinea WinForms (AdjustForVerticalAlignment): y = arriba + alto/2 - altoTexto/2, con
    //     divisiones enteras, y si no caben, arriba.
    //   - Ni texto vacio ni color Transparent pintan nada. GDI no hace caso a la traslacion que el codigo le puso al Graphics (WinForms
    //     solo la pasa con PreserveGraphicsTranslateTransform): se pinta con la matriz del HDC (Graphics.TextoGdi). El recorte del codigo
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
            LetraGdi l = LetraGdi.De(f);
            int dt, iz, de; Separa(fl, l, out dt, out iz, out de);
            r = AjustaVertical(l, s, r, dt, iz, de);
            if (r.Width == int.MaxValue) r.Width -= r.X;    // (TextExtensions.DrawText: la caja sin fin de un punto)
            if (r.Height == int.MaxValue) r.Height -= r.Y;
            DrawTextGdi.Rect rc = new DrawTextGdi.Rect(r.Left, r.Top, r.Right, r.Bottom);
            List<DrawTextGdi.Trozo> trozos = new List<DrawTextGdi.Trozo>();
            DrawTextGdi.Hace(l, s, ref rc, dt, iz, de, trozos);
            bool alias = g.TextRenderingHint == TextRenderingHint.SingleBitPerPixel || g.TextRenderingHint == TextRenderingHint.SingleBitPerPixelGridFit;
            SKRectI? caja = (dt & DrawTextGdi.DT_NOCLIP) == 0 ? new SKRectI(r.Left, r.Top, r.Right, r.Bottom) : (SKRectI?)null;
            g.TextoGdi(l, trozos, caja, c, alias, (dt & DrawTextGdi.DT_PREFIXONLY) == 0, (dt & DrawTextGdi.DT_HIDEPREFIX) == 0);
        }
        public static Size MeasureText(string s, Font f) { return Mide(s, f, MaxSize, TextFormatFlags.Bottom); }
        public static Size MeasureText(string s, Font f, Size caja) { return Mide(s, f, caja, TextFormatFlags.Bottom); }
        public static Size MeasureText(string s, Font f, Size caja, TextFormatFlags fl) { return Mide(s, f, caja, fl); }
        public static Size MeasureText(Graphics g, string s, Font f) { return Mide(s, f, MaxSize, TextFormatFlags.Bottom); }
        public static Size MeasureText(Graphics g, string s, Font f, Size caja) { return Mide(s, f, caja, TextFormatFlags.Bottom); }
        public static Size MeasureText(Graphics g, string s, Font f, Size caja, TextFormatFlags fl) { return Mide(s, f, caja, fl); }
        // las banderas de GDI y los margenes (TextExtensions.SplitTextFormatFlags y GetTextMargins)
        private static void Separa(TextFormatFlags fl, LetraGdi l, out int dt, out int iz, out int de)
        {
            dt = (int)((uint)fl & 0x00FFFFFFu);   // (GdiUnsupportedFlagMask: lo de WinForms no llega a GDI)
            iz = de = 0;
            float m = l.Alto / 6f;
            if ((fl & TextFormatFlags.LeftAndRightPadding) != 0) { iz = (int)Math.Ceiling(2 * m); de = (int)Math.Ceiling(m * 2.5f); }
            else if ((fl & TextFormatFlags.NoPadding) == 0) { iz = (int)Math.Ceiling(m); de = (int)Math.Ceiling(m * 1.5f); }
        }
        // TextExtensions.AdjustForVerticalAlignment: GDI solo centra o baja un renglon (SingleLine); con varios lo hace WinForms
        private static Rectangle AjustaVertical(LetraGdi l, string s, Rectangle r, int dt, int iz, int de)
        {
            bool arriba = (dt & DrawTextGdi.DT_BOTTOM) == 0 && (dt & DrawTextGdi.DT_VCENTER) == 0;
            if (arriba || (dt & DrawTextGdi.DT_SINGLELINE) != 0 || (dt & DrawTextGdi.DT_CALCRECT) != 0) return r;
            DrawTextGdi.Rect rc = new DrawTextGdi.Rect(r.Left, r.Top, unchecked(r.X + r.Width), unchecked(r.Y + r.Height));
            int altoTexto = DrawTextGdi.Hace(l, s, ref rc, dt | DrawTextGdi.DT_CALCRECT, iz, de, null);
            if (altoTexto > r.Height) return r;
            if ((dt & DrawTextGdi.DT_VCENTER) != 0) r.Y = r.Top + r.Height / 2 - altoTexto / 2;
            else r.Y = unchecked(r.Y + r.Height) - altoTexto;
            return r;
        }
        // TextExtensions.MeasureText
        private static Size Mide(string s, Font f, Size caja, TextFormatFlags fl)
        {
            if (string.IsNullOrEmpty(s) || f == null) return Size.Empty;
            LetraGdi l = LetraGdi.De(f);
            int dt, iz, de; Separa(fl, l, out dt, out iz, out de);
            int minimo = 1 + iz + de;
            if (caja.Width <= minimo) caja.Width = minimo;
            if (caja.Height <= 0) caja.Height = 1;
            if (caja.Height == int.MaxValue && (dt & DrawTextGdi.DT_SINGLELINE) != 0) dt &= ~(DrawTextGdi.DT_BOTTOM | DrawTextGdi.DT_VCENTER);
            if (caja.Width == int.MaxValue) dt &= ~DrawTextGdi.DT_WORDBREAK;
            DrawTextGdi.Rect rc = new DrawTextGdi.Rect(0, 0, caja.Width, caja.Height);
            DrawTextGdi.Hace(l, s, ref rc, dt | DrawTextGdi.DT_CALCRECT, iz, de, null);
            return new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
        }
    }

    // LA LETRA DE GDI (la de TextRenderer; FontCache.Data.FromFont de WinForms y el motor de letras de GDI como lo replica Wine,
    // dlls/win32u/freetype.c). Es otra que la de GDI+:
    //   - de alto, ceil(96 * SizeInPoints / 72) pixeles por em (lfHeight negativo): 8,25 pt = 11 px, 10 pt = 14 px, 7,5 pt = 10 px;
    //   - ascenso y descenso (tmAscent, tmDescent) de la tabla VDMX si la trae para ese tamano; si no, los de OS/2 (usWinAscent y
    //     usWinDescent) a esa escala y redondeados; tmHeight es la suma (el alto del renglon) y tmExternalLeading el hueco de hhea;
    //   - el ancho de cada letra en pixeles enteros: el de la tabla hdmx para ese tamano si la trae (los anchos ajustados que guarda la
    //     letra para GDI), si no el de la letra ajustada; la negrita inventada (la familia sin negrita: Microsoft Sans Serif) suma 1 pixel
    //     a cada letra y a tmAveCharWidth y no cambia el alto (prueba test_fake_bold_font de Wine, que pasa en Windows).
    internal sealed class LetraGdi
    {
        internal SKTypeface Tipo; internal bool Negrita, Cursiva;
        internal int Ppem, Asc, Desc, Alto, Ext, Medio;
        internal SKFont Sk, SkAlias;          // la letra ajustada a Ppem, suavizada y sin suavizar (no se cambian: varios hilos la usan)
        private byte[] hdmx; private int hdmxIni, hdmxGlifos;
        private readonly Dictionary<int, int> anchos = new Dictionary<int, int>();
        private readonly Dictionary<int, ushort> glifos = new Dictionary<int, ushort>();
        private static readonly Dictionary<string, LetraGdi> hechas = new Dictionary<string, LetraGdi>(StringComparer.OrdinalIgnoreCase);

        internal static LetraGdi De(Font f)
        {
            int ppem = (int)Math.Ceiling(96f * f.SizeInPoints / 72f);   // (ScaleHelper.InitialSystemDpi * font.SizeInPoints / 72)
            if (ppem < 1) ppem = 1;
            string clave = f.FontFamily.Name + "|" + (f.Bold ? "b" : "") + (f.Italic ? "i" : "") + "|" + ppem;
            lock (hechas)
            {
                LetraGdi l;
                if (hechas.TryGetValue(clave, out l)) return l;
                l = new LetraGdi(Letras.Busca(f.FontFamily.Name, f.Style), ppem);
                hechas[clave] = l;
                return l;
            }
        }
        private LetraGdi(Letras.Letra cara, int ppem)
        {
            Tipo = cara.Tipo; Negrita = cara.NegritaInventada; Cursiva = cara.CursivaInventada; Ppem = ppem;
            int upem = Letras.U16(Tipo, "head", 18);
            if (upem <= 0) upem = Tipo.UnitsPerEm > 0 ? Tipo.UnitsPerEm : 2048;
            int winA = Letras.U16(Tipo, "OS/2", 74), winD = Letras.U16(Tipo, "OS/2", 76);
            int hA = Letras.S16(Tipo, "hhea", 4), hD = Letras.S16(Tipo, "hhea", 6), hG = Letras.S16(Tipo, "hhea", 8);
            int ascU = winA, descU = winD;
            if (winA + winD == 0) { ascU = hA; descU = -hD; }
            long escala = ((long)ppem * 65536 + upem / 2) / upem;   // em_scale = MulDiv(ppem, 1 << 16, unitsPerEm)
            int vMax, vMin;
            if (Vdmx(Tipo, ppem, out vMax, out vMin)) { Asc = vMax; Desc = -vMin; }
            else { Asc = Escala(ascU, escala); Desc = Escala(descU, escala); }
            Alto = Asc + Desc;
            Ext = Math.Max(0, Escala(hG - ((ascU + descU) - (hA - hD)), escala));
            Medio = Escala(Letras.S16(Tipo, "OS/2", 2), escala) + (Negrita ? 1 : 0);
            Hdmx(ppem);
            Sk = Nueva(SKFontEdging.Antialias); SkAlias = Nueva(SKFontEdging.Alias);
        }
        private SKFont Nueva(SKFontEdging borde)
        {
            SKFont f = new SKFont(Tipo, Ppem) { Hinting = SKFontHinting.Normal, Subpixel = false, LinearMetrics = false, Edging = borde };
            if (Cursiva) f.SkewX = -0.25f;
            return f;
        }
        // FT_MulFix(y, em_scale): redondeado al entero mas cercano (la mitad hacia afuera)
        private static int Escala(int y, long escala) { long v = (long)Math.Abs(y) * escala; int r = (int)((v + 0x8000) >> 16); return y < 0 ? -r : r; }
        // VDMX (load_VDMX de Wine): el grupo de la proporcion 1:1 (o la de cualquier proporcion) y, en el, el renglon de ese ppem
        private static bool Vdmx(SKTypeface tf, int ppem, out int yMax, out int yMin)
        {
            yMax = yMin = 0;
            byte[] t = Letras.Tabla(tf, "VDMX");
            if (t == null || t.Length < 6) return false;
            int nRat = (t[4] << 8) | t[5];
            int grupo = -1;
            for (int i = 0; i < nRat; i++)
            {
                int o = 6 + 4 * i; if (o + 4 > t.Length) return false;
                byte cs = t[o], xr = t[o + 1], y0 = t[o + 2], y1 = t[o + 3];
                if (cs == 0) continue;   // (Wine se salta los de bCharSet 0)
                if ((xr == 0 && y0 == 0 && y1 == 0) || (xr == 1 && y0 <= 1 && y1 >= 1))
                {
                    int oo = 6 + 4 * nRat + 2 * i; if (oo + 2 > t.Length) return false;
                    grupo = (t[oo] << 8) | t[oo + 1];
                    break;
                }
            }
            if (grupo < 0 || grupo + 4 > t.Length) return false;
            int recs = (t[grupo] << 8) | t[grupo + 1], desde = t[grupo + 2], hasta = t[grupo + 3];
            if (ppem < desde || ppem > hasta) return false;
            for (int i = 0; i < recs; i++)
            {
                int o = grupo + 4 + 6 * i; if (o + 6 > t.Length) return false;
                int alto = (t[o] << 8) | t[o + 1];
                if (alto > ppem) return false;
                if (alto == ppem) { yMax = (short)((t[o + 2] << 8) | t[o + 3]); yMin = (short)((t[o + 4] << 8) | t[o + 5]); return true; }
            }
            return false;
        }
        // hdmx: un registro por tamano con el ancho en pixeles de cada glifo
        private void Hdmx(int ppem)
        {
            byte[] t = Letras.Tabla(Tipo, "hdmx");
            if (t == null || t.Length < 8) return;
            int n = (short)((t[2] << 8) | t[3]), tam = (t[4] << 24) | (t[5] << 16) | (t[6] << 8) | t[7];
            int glifosLetra = Letras.U16(Tipo, "maxp", 4);
            if (tam < 2 || glifosLetra <= 0) return;
            for (int i = 0; i < n; i++)
            {
                int o = 8 + i * tam;
                if (o + 2 > t.Length) return;
                if (t[o] == ppem) { hdmx = t; hdmxIni = o + 2; hdmxGlifos = Math.Min(glifosLetra, Math.Min(tam - 2, t.Length - o - 2)); return; }
            }
        }
        internal ushort Glifo(int cp)
        {
            lock (glifos)
            {
                ushort g;
                if (!glifos.TryGetValue(cp, out g)) { g = Sk.GetGlyph(cp); glifos[cp] = g; }
                return g;
            }
        }
        internal int Ancho(int cp)
        {
            lock (anchos)
            {
                int w;
                if (anchos.TryGetValue(cp, out w)) return w;
                ushort g = Glifo(cp);
                if (hdmx != null && g < hdmxGlifos) w = hdmx[hdmxIni + g];
                else { float[] a = Sk.GetGlyphWidths(new ushort[] { g }); w = (int)Math.Round(a.Length > 0 ? a[0] : 0f); }
                if (Negrita) w += 1;
                anchos[cp] = w;
                return w;
            }
        }
        // el ancho de s[ini .. ini+n) (GetTextExtentPoint32: la suma de los anchos, sin interletraje)
        internal int Extension(string s, int ini, int n)
        {
            int w = 0;
            for (int i = ini; i < ini + n && i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { w += Ancho(char.ConvertToUtf32(c, s[i + 1])); i++; }
                else w += Ancho(c);
            }
            return w;
        }
        // GetTextExtentExPoint: cuantas letras caben en 'max' (comparado sin signo: con un maximo negativo caben todas) y el ancho de todo
        internal int Cabe(string s, int max, out int caben)
        {
            int w = 0; caben = -1;
            for (int i = 0; i < s.Length; i++)
            {
                w += char.IsLowSurrogate(s[i]) && i > 0 && char.IsHighSurrogate(s[i - 1]) ? 0 : (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? Ancho(char.ConvertToUtf32(s[i], s[i + 1])) : Ancho(s[i]));
                if (caben < 0 && (uint)w > (uint)max) caben = i;
            }
            if (caben < 0) caben = s.Length;
            return w;
        }
        // los glifos de s y donde va cada uno (en pixeles enteros, con los anchos de GDI)
        internal void Posiciones(string s, int x, int y, out ushort[] g, out SKPoint[] pos)
        {
            List<ushort> lg = new List<ushort>(s.Length); List<SKPoint> lp = new List<SKPoint>(s.Length);
            int px = x;
            for (int i = 0; i < s.Length; i++)
            {
                int cp = s[i];
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { cp = char.ConvertToUtf32(s[i], s[i + 1]); i++; }
                lg.Add(Glifo(cp)); lp.Add(new SKPoint(px, y));
                px += Ancho(cp);
            }
            g = lg.ToArray(); pos = lp.ToArray();
        }
    }

    // EL DrawTextEx DE USER32, como lo replica Wine (dlls/user32/text.c: DrawTextExW, TEXT_NextLineW, TEXT_WordBreak, TEXT_Ellipsify,
    // TEXT_SkipChars), con la letra de GDI de LetraGdi. Lo que hace:
    //   - un renglon por parrafo; con DT_WORDBREAK se parte donde se puede (ScriptBreak: despues de los espacios y de un guion) y, si la
    //     palabra no cabe sola, NO se parte (se sale, y quiza lleve puntos) salvo con DT_EDITCONTROL; alineado a la izquierda, los
    //     espacios del final que caben se quedan en el renglon (cuentan en su ancho); centrado o a la derecha, todos menos uno;
    //   - DT_END_ELLIPSIS: "..." (tres puntos) en el renglon que no cabe y no tiene mas detras, y en el ultimo que se ve si queda texto;
    //     DT_WORD_ELLIPSIS en todo renglon que no cabe; el texto mas largo que cabe con los puntos (los tres puntos solos si no cabe nada);
    //   - el "&" no se pinta y subraya la letra que le sigue; "&&" es un "&"; un "&" al final se pinta (DT_NOPREFIX: todo tal cual);
    //   - el ultimo renglon es el que ya no cabe entero (o, con DT_EDITCONTROL, el que deja sin sitio al siguiente) y se pinta recortado;
    //     con DT_CALCRECT (medir) no hay recorte y salen todos;
    //   - alineado con divisiones enteras: centrado x = (izquierda + margen + derecha - margen - ancho) / 2; DT_SINGLELINE con DT_VCENTER
    //     y = arriba + alto/2 - altoLetra/2;
    //   - los tabuladores (DT_EXPANDTABS) cada 8 anchos medios.
    // Sin DT_MODIFYSTRING, DT_PATH_ELLIPSIS ni derecha a izquierda (la rocola no los usa).
    internal static class DrawTextGdi
    {
        internal const int DT_CENTER = 1, DT_RIGHT = 2, DT_VCENTER = 4, DT_BOTTOM = 8, DT_WORDBREAK = 0x10, DT_SINGLELINE = 0x20, DT_EXPANDTABS = 0x40,
            DT_NOCLIP = 0x100, DT_EXTERNALLEADING = 0x200, DT_CALCRECT = 0x400, DT_NOPREFIX = 0x800, DT_EDITCONTROL = 0x2000, DT_PATH_ELLIPSIS = 0x4000,
            DT_END_ELLIPSIS = 0x8000, DT_WORD_ELLIPSIS = 0x40000, DT_HIDEPREFIX = 0x100000, DT_PREFIXONLY = 0x200000;
        internal struct Rect { public int Left, Top, Right, Bottom; public Rect(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; } }
        internal struct Trozo { public string Cadena; public int X, Y, Prefijo; }
        private const string Puntos = "...";

        // DrawTextExW: devuelve el alto del texto (1 si no hay); con DT_CALCRECT deja en rc la medida
        internal static int Hace(LetraGdi l, string str, ref Rect rc, int flags, int iz, int de, List<Trozo> salida)
        {
            if ((flags & DT_SINGLELINE) != 0) flags &= ~DT_WORDBREAK;
            int lh = (flags & DT_EXTERNALLEADING) != 0 ? l.Alto + l.Ext : l.Alto;
            int x = rc.Left, y = rc.Top, ancho = unchecked(rc.Right - rc.Left);
            int count = str.Length;
            if (count == 0)
            {
                if ((flags & DT_CALCRECT) != 0) { rc.Right = rc.Left; rc.Bottom = (flags & DT_SINGLELINE) != 0 ? rc.Top + lh : rc.Top; }
                return lh;
            }
            ancho -= iz + de;
            if ((flags & (DT_CENTER | DT_RIGHT)) == 0) x += iz;
            int tab = Math.Max(1, l.Medio * 8);
            if ((flags & DT_CALCRECT) != 0) flags |= DT_NOCLIP;
            int pos = 0, maxAncho = 0;
            bool ultimo;
            StringBuilder linea = new StringBuilder();
            do
            {
                ultimo = (flags & DT_NOCLIP) == 0 && y + ((flags & DT_EDITCONTROL) != 0 ? 2 * lh - 1 : lh) > rc.Bottom;
                linea.Length = 0;
                int cx, cy, prefijo;
                Renglon(l, str, ref pos, ref count, linea, ancho, flags, ultimo, tab, out cx, out cy, out prefijo);
                if ((flags & DT_CENTER) != 0) x = unchecked(rc.Left + iz + rc.Right - de - cx) / 2;
                else if ((flags & DT_RIGHT) != 0) x = rc.Right - cx - de;
                if ((flags & DT_SINGLELINE) != 0)
                {
                    if ((flags & DT_VCENTER) != 0) y = rc.Top + unchecked(rc.Bottom - rc.Top) / 2 - cy / 2;
                    else if ((flags & DT_BOTTOM) != 0) y = rc.Bottom - cy;
                }
                if ((flags & DT_CALCRECT) == 0) { if (salida != null) Trozos(l, linea.ToString(), x, y, flags, tab, prefijo, salida); }
                else if (cx > maxAncho) maxAncho = cx;
                y += lh;
            }
            while (count > 0 && !ultimo);
            if ((flags & DT_CALCRECT) != 0) { rc.Right = rc.Left + maxAncho + iz + de; rc.Bottom = y; }
            int alto = y - rc.Top;
            return alto == 0 ? 1 : alto;
        }
        // el renglon en sus trozos entre tabuladores (el bucle de ExtTextOut de DrawTextExW)
        private static void Trozos(LetraGdi l, string linea, int x, int y, int flags, int tab, int prefijo, List<Trozo> salida)
        {
            int xseg = x, i = 0, n = linea.Length;
            while (n > 0)
            {
                int lenSeg = n;
                if ((flags & DT_EXPANDTABS) != 0) { int p = i; while (p < i + n && linea[p] != '\t') p++; lenSeg = p - i; }
                string seg = linea.Substring(i, lenSeg);
                salida.Add(new Trozo { Cadena = seg, X = xseg, Y = y, Prefijo = prefijo >= 0 && prefijo < lenSeg ? prefijo : -1 });
                n -= lenSeg; i += lenSeg;
                if (n > 0)
                {
                    n--; i++;   // el tabulador
                    xseg += ((l.Extension(seg, 0, seg.Length) / tab) + 1) * tab;
                    if (prefijo != -1) prefijo = prefijo < lenSeg ? -1 : prefijo - lenSeg;
                }
            }
        }
        // TEXT_NextLineW: el siguiente renglon (hasta el salto, o hasta donde se parta), sus puntos y la letra subrayada
        private static void Renglon(LetraGdi l, string str, ref int i, ref int count, StringBuilder dest, int width, int format, bool ultimo, int tab,
                                    out int cx, out int cy, out int prefijo)
        {
            int plen = 0; prefijo = -1; cy = 0;
            bool expande = (format & DT_EXPANDTABS) != 0, prefijos = (format & DT_NOPREFIX) == 0;
            while (count > 0)
            {
                if (str[i] == '\t' && expande)
                {
                    plen = ((plen / tab) + 1) * tab;
                    count--; dest.Append(str[i++]);
                    while (count > 0 && str[i] == '\t') { plen += tab; count--; dest.Append(str[i++]); }
                }
                int segI = i, segCount = count, segJ = dest.Length;
                while (count > 0 && (str[i] != '\t' || !expande) && ((str[i] != '\r' && str[i] != '\n') || (format & DT_SINGLELINE) != 0))
                {
                    if (!prefijos || count <= 1) { count--; dest.Append(str[i++]); continue; }
                    if (str[i] == '&')
                    {
                        count--; i++;   // el "&" no se pinta
                        if (str[i] == '&') { count--; dest.Append(str[i++]); }
                        else if (prefijo == -1 || prefijo >= segJ) prefijo = dest.Length;
                    }
                    else { count--; dest.Append(str[i++]); }
                }
                string seg = dest.ToString(segJ, dest.Length - segJ);
                int jSeg = seg.Length;
                int maxSeg = width - plen;
                int numFit;
                int sizeCx = l.Cabe(seg, maxSeg, out numFit);
                bool cortada = false, cabe = numFit >= jSeg;
                if (!cabe && (format & DT_WORDBREAK) != 0)
                {
                    int usadas;
                    PartePalabra(l, seg, ref jSeg, format, numFit, out usadas, out sizeCx);
                    seg = seg.Substring(0, jSeg);
                    cabe = sizeCx <= maxSeg;
                    Salta(str, ref count, ref i, segCount, segI, i - segI, usadas, prefijos);
                    cortada = true;
                }
                int antes = jSeg;
                bool puntos = false;
                if ((!cabe && (format & DT_WORD_ELLIPSIS) != 0)
                    || ((format & DT_END_ELLIPSIS) != 0 && ((ultimo && count > 0) || (QuedaNadaOSalto(str, i, count) && !cabe))))
                {
                    seg = PonPuntos(l, seg, maxSeg, out sizeCx, out antes);
                    puntos = true;
                }
                if (expande && puntos)
                {
                    if ((format & DT_SINGLELINE) != 0) count = 0;
                    else while (count > 0 && str[i] != '\r' && str[i] != '\n') { count--; i++; }
                }
                dest.Length = segJ; dest.Append(seg);
                if (prefijo >= segJ + antes) prefijo = -1;   // (la letra subrayada se fue con el corte; Wine la vuelve a buscar con TEXT_Reprefix)
                plen += sizeCx;
                if (l.Alto > cy) cy = l.Alto;
                if (cortada) break;
                if (count == 0) break;
                if (str[i] == '\r' || str[i] == '\n')
                {
                    count--; i++;
                    if (count > 0 && (str[i] == '\r' || str[i] == '\n') && str[i] != str[i - 1]) { count--; i++; }
                    break;
                }
                // (si no, era un tabulador y sigue el renglon)
            }
            cx = plen;
        }
        private static bool Blanco(char c) { return c == ' ' || c == '\t' || c == '\u3000'; }
        // ScriptBreak (fSoftBreak): se puede partir antes de s[k] despues de los espacios y despues de un guion (no antes de una cifra)
        private static bool Corte(string s, int k)
        {
            if (k <= 0 || k >= s.Length) return false;
            char a = s[k - 1], b = s[k];
            if (Blanco(b)) return false;
            if (Blanco(a)) return true;
            if ((a == '-' || a == '\u2010') && k >= 2 && !Blanco(s[k - 2]) && !char.IsDigit(b)) return true;
            return a == '\u2013' || a == '\u2014';
        }
        // TEXT_WordBreak
        private static void PartePalabra(LetraGdi l, string seg, ref int len, int format, int charsFit, out int usadas, out int cx)
        {
            int p = charsFit, i = charsFit;
            bool cabePalabra = true;
            if (charsFit == 0) cabePalabra = false;
            else if (Corte(seg, i)) p--;
            else
            {
                while (i > 0 && !Blanco(seg[(--i) + 1])) p--;
                p--;
                cabePalabra = i != 0 || Blanco(seg[i + 1]);
            }
            if (cabePalabra)
            {
                if ((format & (DT_RIGHT | DT_CENTER)) == 0 || seg[p] != ' ') p++;
                bool sigueEspacio = p < len && seg[p] == ' ';
                len = p; usadas = p; if (sigueEspacio) usadas++;
            }
            else if ((format & (DT_EDITCONTROL | DT_WORD_ELLIPSIS | DT_PATH_ELLIPSIS)) == DT_EDITCONTROL)
            {
                if (charsFit == 0) charsFit++;
                len = charsFit; usadas = charsFit;   // la palabra se parte en la ultima letra que cabe
            }
            else
            {
                int q = charsFit; while (q < len && seg[q] != ' ') q++;   // la palabra entera, aunque no quepa
                usadas = q; if (q < len) usadas++;
                len = q;
            }
            cx = l.Extension(seg, 0, len);
        }
        // TEXT_SkipChars: avanza en la cadena original 'n' letras del renglon (cada "&" de prefijo se salta tambien)
        private static void Salta(string str, ref int count, ref int i, int segCount, int segI, int max, int n, bool prefijos)
        {
            int s = segI;
            if (prefijos)
            {
                max -= n;
                while (n-- > 0)
                {
                    if (str[s] == '&' && max-- > 0) s++;
                    s++;
                }
                count = segCount - (s - segI);
            }
            else { s += n; count = segCount - n; }
            i = s;
        }
        // remainder_is_none_or_newline
        private static bool QuedaNadaOSalto(string str, int i, int n)
        {
            if (n == 0) return true;
            if (str[i] != '\n' && str[i] != '\r') return false;
            if (--n == 0) return true;
            if (str[i] == str[i + 1]) return false;
            i++;
            if (str[i] != '\r' && str[i] != '\n') return false;
            return --n == 0;
        }
        // TEXT_Ellipsify: lo mas largo del principio que cabe con "..." detras (los tres puntos solos si no cabe nada)
        private static string PonPuntos(LetraGdi l, string seg, int width, out int cx, out int antes)
        {
            int len = seg.Length;
            if (len > 0 && l.Extension(seg, 0, len) > width)
            {
                int lo = 0, hi = len;
                while (lo < hi) { int mid = (lo + hi) / 2; if (l.Extension(seg, 0, mid) > width) hi = mid; else lo = mid + 1; }
                len = hi;
            }
            string r;
            for (;;)
            {
                r = seg.Substring(0, len) + Puntos;
                cx = l.Extension(r, 0, r.Length);
                if (len == 0 || cx <= width) break;
                len--;
            }
            antes = len;
            return r;
        }
    }

    // ---------------------------------------------------------------- como acomoda el texto GDI+
    // EL TEXTO COMO GDI+ (revisado el 8-11-2026 con la documentacion de StringFormat y las pruebas de Wine que pasan en Windows,
    // dlls/gdiplus/tests/graphics.c: test_measure_string y test_string_functions):
    //   - un margen de 1/6 de la letra a cada lado, salvo los formatos tipograficos; el sitio para el texto es la caja menos los dos (si
    //     no queda medio pixel, no cabe nada);
    //   - un texto cabe si su ancho no pasa de ese sitio mas 0,005 pixeles (con 0,004 de mas cabe, con 0,006 no);
    //   - los renglones se parten despues de los espacios (que se quedan colgando al final, sin contar) y despues de un guion; una
    //     palabra que no cabe sola se parte por letras; un salto de renglon al final no hace otro renglon;
    //   - renglones del alto de la letra (Font.GetHeight), con la base en lo alto del renglon mas el ascenso;
    //   - se ven los renglones que caben y el que asoma a medias; con LineLimit solo los enteros, y ninguno si la caja es mas baja que un
    //     renglon (no se pinta nada);
    //   - el recorte (Trimming) va en cada renglon con NoWrap que no cabe de ancho y, si no cabe todo el texto, en el ultimo renglon que
    //     se ve, que sigue con el resto de su parrafo: Character lo llena con las letras que caben ("LO MAS SONAD"), Word hasta la ultima
    //     palabra entera, EllipsisCharacter y EllipsisWord igual pero con "..." detras (y lo ponen aunque el renglon quepa, si queda texto
    //     sin ver); con NoClip, Character y Word no recortan;
    //   - la caja sin ancho (0) no parte renglones y alinea respecto a X; sin alto, respecto a Y;
    //   - MeasureString mide eso mismo: el renglon mas ancho (sin los espacios del final, salvo MeasureTrailingSpaces) mas los margenes, y
    //     de alto los renglones que se ven, cortado al alto de la caja salvo con NoClip; las letras que caben cuentan el salto de renglon.
    public static class Texto
    {
        public struct Pieza { public string Cadena; public float X, Base, Ancho; }
        public sealed class Acomodo { public readonly List<Pieza> Piezas = new List<Pieza>(); public float Margen, Alto, AnchoMayor; public int Letras; }
        private const float Holgura = 0.005f;
        private const string Puntos = "\u2026";
        private static bool Blanco(char c) { return c == ' ' || c == '\t'; }
        // se puede partir el renglon antes de s[k] (dentro de su parrafo [p0, p1))
        private static bool Corte(string s, int p0, int k)
        {
            char a = s[k - 1], b = s[k];
            if (Blanco(b)) return false;
            if (Blanco(a)) return true;
            if ((a == '-' || a == '\u2010') && k - 1 > p0 && !Blanco(s[k - 2]) && !char.IsDigit(b)) return true;
            return a == '\u2013' || a == '\u2014';
        }
        // los anchos acumulados de cada letra (los de sus glifos; la segunda mitad de un par sustituto no suma)
        private static float[] Sumas(SKFont sk, string s)
        {
            ushort[] g = sk.GetGlyphs(s);
            float[] w = g.Length > 0 ? sk.GetGlyphWidths(g) : new float[0];
            float[] suma = new float[s.Length + 1];
            int k = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float a = 0f;
                if (!(char.IsLowSurrogate(s[i]) && i > 0 && char.IsHighSurrogate(s[i - 1])) && k < w.Length) a = w[k++];
                suma[i + 1] = suma[i] + a;
            }
            return suma;
        }
        private static float AnchoSin(float[] suma, string s, int i, int j) { while (j > i && Blanco(s[j - 1])) j--; return suma[j] - suma[i]; }
        private static bool Cabe(float w, float util) { return w <= util + Holgura; }
        // el mayor corte k (i < k <= p1) en que s[i..k) cabe; -1 si ni la primera palabra cabe
        private static int HastaDondeCabe(float[] suma, string s, int p0, int i, int p1, float util, bool porPalabra)
        {
            int mejor = -1;
            for (int k = i + 1; k <= p1; k++)
            {
                if (porPalabra && k < p1 && !Corte(s, p0, k)) continue;
                if (Cabe(AnchoSin(suma, s, i, k), util)) mejor = k; else break;   // (el ancho solo crece)
            }
            return mejor;
        }
        // las letras que caben desde i (al menos una; sin partir un par sustituto)
        private static int LetrasQueCaben(float[] suma, string s, int i, int p1, float util)
        {
            int k = i + 1;
            while (k < p1 && Cabe(suma[k + 1] - suma[i], util)) k++;
            if (k < p1 && char.IsLowSurrogate(s[k]) && k > i + 1) k--;
            return k;
        }
        // EL RENGLON RECORTADO como dice Trimming, desde i hasta como mucho p1 (el fin del parrafo). 'falta': queda texto sin ver despues
        // de este renglon (los modos con puntos los ponen aunque el renglon quepa). Devuelve la cadena a pintar y hasta donde llega
        // (sin recortar, hasta donde llegaba el renglon: 'finSig', con el salto si lo lleva).
        private static string Recorta(string s, float[] suma, SKFont sk, int p0, int i, int finNormal, int finSig, int p1, float util, StringTrimming t, bool noClip, bool falta, out int fin)
        {
            fin = finSig;
            switch (t)
            {
                case StringTrimming.None: return s.Substring(i, finNormal - i);
                case StringTrimming.Character:
                case StringTrimming.Word:
                    {
                        if (noClip) return s.Substring(i, finNormal - i);
                        int k = HastaDondeCabe(suma, s, p0, i, p1, util, t == StringTrimming.Word);
                        if (k < 0) k = t == StringTrimming.Word ? finNormal : LetrasQueCaben(suma, s, i, p1, util);
                        fin = k;
                        return s.Substring(i, k - i);
                    }
                default:   // EllipsisCharacter, EllipsisWord, EllipsisPath
                    {
                        if (!falta && Cabe(AnchoSin(suma, s, i, p1), util)) return s.Substring(i, p1 - i);
                        float wp = sk.MeasureText(Puntos);
                        bool porPalabra = t == StringTrimming.EllipsisWord;
                        int mejor = i;
                        for (int k = i + 1; k <= p1; k++)
                        {
                            if (porPalabra && k < p1 && !Corte(s, p0, k)) continue;
                            if (Cabe(AnchoSin(suma, s, i, k) + wp, util)) mejor = k; else break;
                        }
                        if (mejor == i && porPalabra)   // ni una palabra entera: por letras
                            for (int k = i + 1; k <= p1; k++) { if (Cabe(suma[k] - suma[i] + wp, util)) mejor = k; else break; }
                        fin = mejor;
                        return s.Substring(i, mejor - i).TrimEnd(' ', '\t') + Puntos;
                    }
            }
        }
        // ACOMODA EL TEXTO EN LA CAJA (o desde el punto, si la caja no tiene ancho o alto): los renglones que se ven, recortados, y donde
        // va cada uno
        public static Acomodo Acomoda(string s, Font f, RectangleF caja, StringFormat sf, SKFont sk)
        {
            Acomodo a = new Acomodo();
            if (string.IsNullOrEmpty(s)) return a;
            float m = sf.Tipografico ? 0f : f.SizeInPixels / 6f;
            float alto = f.GetHeight(), asc = f.Cara.Ascenso * f.SizeInPixels;
            a.Margen = m; a.Alto = alto;
            bool anchoLibre = !(caja.Width > 0f), altoLibre = !(caja.Height > 0f);
            float util = float.MaxValue;
            if (!anchoLibre)
            {
                util = caja.Width - 2 * m;
                if (caja.Width >= 0.5f && util < 0.5f) return a;   // no queda sitio para el texto (GdipDrawString/GdipMeasureString)
            }
            bool noWrap = anchoLibre || (sf.FormatFlags & StringFormatFlags.NoWrap) != 0;
            bool noClip = (sf.FormatFlags & StringFormatFlags.NoClip) != 0;
            bool espacios = (sf.FormatFlags & StringFormatFlags.MeasureTrailingSpaces) != 0;
            float[] suma = Sumas(sk, s);
            // 1) los parrafos y sus renglones: {inicio, fin (con los espacios que cuelgan), fin del parrafo, siguiente, inicio del parrafo}
            List<int[]> rs = new List<int[]>();
            int pos = 0;
            while (pos < s.Length)
            {
                int p1 = pos; while (p1 < s.Length && s[p1] != '\n' && s[p1] != '\r') p1++;
                int sig = p1;
                if (sig < s.Length) sig += (s[sig] == '\r' && sig + 1 < s.Length && s[sig + 1] == '\n') ? 2 : 1;
                if (noWrap || pos == p1) rs.Add(new[] { pos, p1, p1, sig, pos });
                else
                    for (int i = pos; i < p1;)
                    {
                        int k = HastaDondeCabe(suma, s, pos, i, p1, util, true);
                        if (k < 0) k = LetrasQueCaben(suma, s, i, p1, util);
                        rs.Add(new[] { i, k, p1, k >= p1 ? sig : k, pos });
                        i = k;
                    }
                pos = sig;
            }
            // 2) cuantos se ven
            int n = rs.Count, visibles = n;
            if (!altoLibre)
            {
                if ((sf.FormatFlags & StringFormatFlags.LineLimit) != 0) visibles = Math.Min(n, (int)Math.Floor(caja.Height / alto + 1e-3f));
                else visibles = Math.Min(n, Math.Max(1, (int)Math.Ceiling(caja.Height / alto - 1e-3f)));
            }
            if (visibles <= 0) return a;
            bool desborda = visibles < n;
            // 3) cada renglon, recortado si hace falta, y su ancho (el de alinear y el de medir)
            string[] cad = new string[visibles]; float[] anchos = new float[visibles];
            for (int v = 0; v < visibles; v++)
            {
                int[] r = rs[v];
                int i = r[0], fin = r[1], p1 = r[2], fin2 = r[3], p0 = r[4];
                string c;
                bool ultimo = v == visibles - 1;
                if (ultimo && desborda) c = Recorta(s, suma, sk, p0, i, fin, fin2, p1, util, sf.Trimming, noClip, true, out fin2);
                else if (noWrap && !anchoLibre && !Cabe(AnchoSin(suma, s, i, p1), util)) c = Recorta(s, suma, sk, p0, i, fin, fin2, p1, util, sf.Trimming, noClip, false, out fin2);
                else c = s.Substring(i, fin - i);
                cad[v] = c;
                float w = string.IsNullOrEmpty(c) ? 0f : sk.MeasureText(espacios ? c : c.TrimEnd(' ', '\t'));
                anchos[v] = w;
                if (w > a.AnchoMayor) a.AnchoMayor = w;
                a.Letras = Math.Max(a.Letras, fin2);
            }
            // 4) donde va cada uno
            float altoBloque = visibles * alto, y0 = caja.Y;
            if (altoLibre) { if (sf.LineAlignment == StringAlignment.Center) y0 = caja.Y - altoBloque / 2f; else if (sf.LineAlignment == StringAlignment.Far) y0 = caja.Y - altoBloque; }
            else { if (sf.LineAlignment == StringAlignment.Center) y0 = caja.Y + (caja.Height - altoBloque) / 2f; else if (sf.LineAlignment == StringAlignment.Far) y0 = caja.Y + caja.Height - altoBloque; }
            for (int v = 0; v < visibles; v++)
            {
                float w = anchos[v], x;
                if (anchoLibre) x = sf.Alignment == StringAlignment.Center ? caja.X - w / 2f : sf.Alignment == StringAlignment.Far ? caja.X - w - m : caja.X + m;
                else x = sf.Alignment == StringAlignment.Center ? caja.X + (caja.Width - w) / 2f : sf.Alignment == StringAlignment.Far ? caja.Right - m - w : caja.X + m;
                a.Piezas.Add(new Pieza { Cadena = cad[v], X = x, Base = y0 + v * alto + asc, Ancho = w });
            }
            return a;
        }
        // MeasureString: el renglon mas ancho mas los margenes; de alto los renglones, cortado al alto de la caja salvo con NoClip
        internal static SizeF Mide(Acomodo a, SizeF caja, StringFormat sf, out int letras, out int lineas)
        {
            letras = a.Letras; lineas = a.Piezas.Count;
            if (lineas == 0) return new SizeF(0, 0);
            float ancho = a.AnchoMayor + 2 * a.Margen, alto = lineas * a.Alto;
            if (caja.Width > 0 && ancho > caja.Width) ancho = caja.Width;
            if (caja.Height > 0 && (sf.FormatFlags & StringFormatFlags.NoClip) == 0 && alto > caja.Height) alto = caja.Height;
            return new SizeF(ancho, alto);
        }
        // (GraphicsPath.AddString) los renglones y donde va cada uno
        internal static List<Pieza> Piezas(string s, Font f, RectangleF caja, StringFormat sf, SKFont sk) { return Acomoda(s, f, caja, sf ?? StringFormat.SinFormato, sk).Piezas; }
    }
}

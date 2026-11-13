// LO QUE LOS ARNESES USAN DEL DIBUJO EN LA REFERENCIA CONGELADA (port nativo, oleada 8b, 13-11-2026): la misma clase Pru que PruNativo.cs, la
// de la capa de GDI+ (Rocola.Dibujo2D) que tiene la referencia. Solo se compila para la referencia (la carpeta Fuente sin Nativo/); el programa
// nuevo no la lleva y ya no nombra esa capa en ningun arnes. Mismos nombres y mismas cuentas que PruNativo.cs.
using System;
using System.Drawing;
using System.Reflection;
using Rocola;
using Rocola.Dibujo2D;
using SkiaSharp;

namespace Banco
{
    internal sealed class Lz : IDisposable
    {
        public readonly object Mapa;
        public readonly Graphics G;
        public Lz(Bitmap b) { Mapa = b; G = Graphics.FromImage(b); }
        public object Objeto { get { return G; } }
        public void Dispose() { G.Dispose(); }
        public ModoSuave Suavizado { get { return (ModoSuave)(int)G.SmoothingMode; } set { G.SmoothingMode = (SmoothingMode)(int)value; } }
        public ModoLetra Letra { get { return (ModoLetra)(int)G.TextRenderingHint; } set { G.TextRenderingHint = (TextRenderingHint)(int)value; } }
        public ModoCopia Copia { get { return (ModoCopia)(int)G.CompositingMode; } set { G.CompositingMode = (CompositingMode)(int)value; } }
        public ModoFiltro Filtro { get { return (ModoFiltro)(int)G.InterpolationMode; } set { G.InterpolationMode = (InterpolationMode)(int)value; } }
        public ModoPixel Pixel { get { return (ModoPixel)(int)G.PixelOffsetMode; } set { G.PixelOffsetMode = (PixelOffsetMode)(int)value; } }
        public void Limpia(Color c) { G.Clear(c); }
        public void Rellena(Color c, float x, float y, float w, float h) { using (SolidBrush sb = new SolidBrush(c)) G.FillRectangle(sb, x, y, w, h); }
        public void Escribe(string s, Fte f, Color c, RectangleF r, Fmt sf)
        {
            using (SolidBrush br = new SolidBrush(c)) G.DrawString(s, f.F, br, r, sf != null ? sf.S : null);
        }
        public void RellenaRuta(object ruta, Color c) { using (SolidBrush sb = new SolidBrush(c)) G.FillPath(sb, (GraphicsPath)ruta); }
        public void TrazaRuta(object ruta, Color c, float ancho) { using (Pen pn = new Pen(c, ancho)) G.DrawPath(pn, (GraphicsPath)ruta); }
        public object Guarda() { return G.Save(); }
        public void Restaura(object o) { G.Restore((GraphicsState)o); }
        public float[] Matriz6()
        {
            PointF[] p = { new PointF(0, 0), new PointF(1, 0), new PointF(0, 1) };
            using (Matrix t = G.Transform) t.TransformPoints(p);
            return new[] { p[1].X - p[0].X, p[1].Y - p[0].Y, p[2].X - p[0].X, p[2].Y - p[0].Y, p[0].X, p[0].Y };
        }
        public void Traslada(float dx, float dy) { G.TranslateTransform(dx, dy); }
        public void Escala(float sx, float sy) { G.ScaleTransform(sx, sy); }
        public void Gira(float grados) { G.RotateTransform(grados); }
        public void Recorta(RectangleF r) { G.SetClip(r); }
        public void Recorta(Rectangle r) { G.SetClip(r); }
        public void Intersecta(RectangleF r) { G.IntersectClip(r); }
        public void Sella() { typeof(Graphics).GetMethod("SellaRecorte", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(G, null); }
        public RectangleF LimitesRecorte { get { return G.ClipBounds; } }
        public PointF[] Transformados() { PointF[] p = { new PointF(0, 0), new PointF(1, 1) }; G.Transform.TransformPoints(p); return p; }
        public object[] Estado() { throw new InvalidOperationException("la referencia no tiene lienzo con estado"); }
        // ---- (segunda parte de la oleada 8b) lo que piden los nueve arneses que faltaban
        public void RecortaElipse(float x, float y, float w, float h) { using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(x, y, w, h); G.SetClip(gp); } }
        internal Lz(Graphics g) { Mapa = null; G = g; }   // (un Graphics que da el programa: no se suelta)
        public SizeF Mide(string s, Fte f) { return G.MeasureString(s, f != null ? f.F : null); }
        public RectangleF LimitesVisibles { get { return G.VisibleClipBounds; } }
        public void RellenaOvalo(Color c, float x, float y, float w, float h) { using (SolidBrush sb = new SolidBrush(c)) G.FillEllipse(sb, x, y, w, h); }
        public void EscribeEn(string s, Fte f, Color c, float x, float y) { using (SolidBrush br = new SolidBrush(c)) G.DrawString(s, f.F, br, x, y); }
        public void EscribeGdi(string s, Fte f, Rectangle r, Color c, int banderas) { TextRenderer.DrawText(G, s, f != null ? f.F : null, r, c, (TextFormatFlags)banderas); }
        public SKMatrix MatrizCruda { get { return G.C.TotalMatrix; } }
        public int Cuenta { get { return G.C.SaveCount; } }
        public void RellenaDegradado(Rectangle caja, Color c1, Color c2, float angulo, float x, float y, float w, float h) { using (LinearGradientBrush lg = new LinearGradientBrush(caja, c1, c2, angulo)) G.FillRectangle(lg, x, y, w, h); }
        public void Linea(Color c, float ancho, float x1, float y1, float x2, float y2) { using (Pen p = new Pen(c, ancho)) G.DrawLine(p, x1, y1, x2, y2); }
        public void SinRecorte() { G.ResetClip(); }
        public void RellenaCon(object pincel, object ruta) { G.FillPath((Brush)pincel, (GraphicsPath)ruta); }
        public void RellenaCon(object pincel, Rectangle r) { G.FillRectangle((Brush)pincel, r); }
        public void RellenaTextura(object mapa, float dx, float dy, Rectangle r) { using (TextureBrush t = new TextureBrush((Image)mapa, WrapMode.Tile)) { t.TranslateTransform(dx, dy); G.FillRectangle(t, r); } }
    }

    internal sealed class Fte : IDisposable
    {
        public readonly Font F;
        public Fte(string familia, float tam, int estilo = 0, string unidad = "Point") { F = new Font(familia, tam, (FontStyle)estilo, unidad == "Pixel" ? GraphicsUnit.Pixel : GraphicsUnit.Point); }
        private Fte(Font f) { F = f; }
        public static Fte De(object o) { return o == null ? null : new Fte((Font)o); }
        public object Obj { get { return F; } }
        public string Nombre { get { return F.Name; } }
        public float Tamano { get { return F.Size; } }
        public string Unidad { get { return F.Unit.ToString(); } }
        public bool Negrita { get { return F.Bold; } }
        public string Estilo { get { return F.Style.ToString(); } }
        public float AltoRenglon { get { return F.GetHeight(); } }
        public int Alto { get { return F.Height; } }
        public float Puntos { get { return F.SizeInPoints; } }
        public void Dispose() { F.Dispose(); }
        // ---- (segunda parte de la oleada 8b)
        public static Fte Generica(float tam, int estilo = 0, string unidad = "Point") { return new Fte(new Font(FontFamily.GenericSansSerif, tam, (FontStyle)estilo, unidad == "Pixel" ? GraphicsUnit.Pixel : GraphicsUnit.Point)); }
    }

    internal sealed class Fmt
    {
        public readonly StringFormat S;
        private Fmt(StringFormat s) { S = s; }
        public Fmt() { S = new StringFormat(); }
        public Fmt(int banderas) { S = new StringFormat((StringFormatFlags)banderas); }
        private static readonly Fmt tip = new Fmt(StringFormat.GenericTypographic), gen = new Fmt(StringFormat.GenericDefault);
        public static Fmt Tipografico { get { return tip; } }
        public static Fmt Generico { get { return gen; } }
        public object Obj { get { return S; } }
        public ModoAlinea Horizontal { get { return (ModoAlinea)(int)S.Alignment; } set { S.Alignment = (StringAlignment)(int)value; } }
        public ModoAlinea Vertical { get { return (ModoAlinea)(int)S.LineAlignment; } set { S.LineAlignment = (StringAlignment)(int)value; } }
        public int Banderas { get { return (int)S.FormatFlags; } set { S.FormatFlags = (StringFormatFlags)value; } }
        public ModoCorte Corte { get { return (ModoCorte)(int)S.Trimming; } set { S.Trimming = (StringTrimming)(int)value; } }
        public static Fmt De(object o) { if (o == null) return null; StringFormat s = (StringFormat)o; return ReferenceEquals(s, StringFormat.GenericTypographic) ? tip : ReferenceEquals(s, StringFormat.GenericDefault) ? gen : new Fmt(s); }
        public bool EsTipografico { get { return ReferenceEquals(S, StringFormat.GenericTypographic); } }
    }

    internal static class Pru
    {
        public const bool Capa = true;   // (esta es la de la referencia)
        public static readonly Type TMapa = typeof(Bitmap);
        public static readonly Type TLienzo = typeof(Graphics);
        public static object NuevoMapa(int w, int h, string formato) { return new Bitmap(w, h, Formato(formato)); }
        public static object DeSk(SKBitmap b, string formato) { return b == null ? null : new Bitmap(b, Formato(formato)); }
        public static object Aprecia(object o, string formato) { return o as Bitmap ?? new Bitmap((SKBitmap)o, Formato(formato)); }
        public static bool EsCapa(object o) { return o is Image; }
        // Image.IsAlphaPixelFormat(f) || (f & PixelFormat.Indexed) != 0
        public static bool TieneAlfa(string formato) { PixelFormat f = Formato(formato); return Image.IsAlphaPixelFormat(f) || (f & PixelFormat.Indexed) != 0; }
        public static SKBitmap Sk(object o) { return o is Bitmap ? ((Bitmap)o).Sk : o as SKBitmap; }
        public static string Formato(object o) { return ((Bitmap)o).PixelFormat.ToString(); }
        private static PixelFormat Formato(string n) { return (PixelFormat)Enum.Parse(typeof(PixelFormat), n); }
        public static void Suelta(object o) { IDisposable d = o as IDisposable; if (d != null) d.Dispose(); }
        public static void Pon(object mapa, int x, int y, Color c) { ((Bitmap)mapa).SetPixel(x, y, c); }
        public static unsafe byte[] SinPremultiplicar(object o)
        {
            Bitmap b = (Bitmap)o; SKBitmap s = b.Sk;
            BitmapData d = b.LockBits(new Rectangle(0, 0, s.Width, s.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] sub = new byte[s.Width * s.Height * 4];
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) sub[y * s.Width * 4 + x] = ((byte*)d.Scan0)[y * d.Stride + x];
            b.UnlockBits(d);
            return sub;
        }
        public static string NombreEstilo(int e) { return ((FontStyle)e).ToString(); }
        public static object Brocha(Color c) { return new SolidBrush(c); }
        public static int Marcas(object o) { return ((Bitmap)o).Flags; }
        public static SKPaint PincelLiso(Color c) { return new SKPaint { Color = new SKColor(c.R, c.G, c.B, c.A) }; }
        public static string InfoRuta(object ruta, Func<RectangleF, string> r) { GraphicsPath p = (GraphicsPath)ruta; return (" puntos=" + p.PointCount + " tipo=" + p.FillMode + " caja=" + r(p.GetBounds())).Replace("Alternate", "EvenOdd"); }
        public static void OnPaint(System.Reflection.MethodInfo onPaint, object control, Lz g, Rectangle r) { using (PaintEventArgs pe = new PaintEventArgs(g.G, r)) onPaint.Invoke(control, new object[] { pe }); }
        public static unsafe void EscribePuntos(object mapa, uint[] argb)
        {
            Bitmap b = (Bitmap)mapa; int w = b.Width, h = b.Height;
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) ((uint*)((byte*)d.Scan0 + y * d.Stride))[x] = argb[y * w + x];
            b.UnlockBits(d);
        }
        public static Lz Lienzo(object mapa) { return new Lz((Bitmap)mapa); }
        // ---- (segunda parte de la oleada 8b)
        public static object Rotulo() { return new Rocola.Ventanas.Label(); }
        public static Lz LienzoSobre(SKCanvas c, bool suave, int ajuste) { throw new InvalidOperationException("la referencia no da un lienzo de Skia"); }
        public static Delegate AccionLienzo(Action<Lz> a) { return (Action<Graphics>)(g => a(new Lz(g))); }
        public static object FamiliaDeFuente(object fuente) { return ((Font)fuente).FontFamily; }
        public static object Familia(string nombre) { return new FontFamily(nombre); }
        public static object FamiliaGenerica() { return FontFamily.GenericSansSerif; }
        public static string NombreDeFamilia(object familia) { return ((FontFamily)familia).Name; }
        public static void AnadeLinea(object ruta, float x1, float y1, float x2, float y2) { ((GraphicsPath)ruta).AddLine(x1, y1, x2, y2); }
        public static string NombreFamilia(string nombre) { return new FontFamily(nombre).Name; }
        public static object Lee(byte[] datos) { using (System.IO.MemoryStream ms = new System.IO.MemoryStream(datos)) return Image.FromStream(ms); }
        public static object FormatoNativo(object mapa) { throw new InvalidOperationException("la referencia no tiene FormatoMapa"); }
        // la medida de una Form1 sin construir: en la referencia Form1 hereda de Form y es el campo 'bounds' de Control
        public static void MedidaDeForm1(object form, int w, int h)
        {
            for (Type t = form.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField("bounds", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly); if (fi != null) { fi.SetValue(form, new Rectangle(0, 0, w, h)); return; } }
            throw new MissingFieldException(form.GetType().Name, "bounds");
        }
        public static SKPath Ruta(object o) { return o as SKPath ?? ((GraphicsPath)o).P; }
        public static object BrochaDegradado(RectangleF caja, Color c1, Color c2, float angulo) { return new LinearGradientBrush(caja, c1, c2, angulo); }
        public static object BrochaTextura(object mapa, bool medio) { return new TextureBrush((Image)mapa, WrapMode.Tile); }
        public static void SueltaBrocha(object o) { IDisposable d = o as IDisposable; if (d != null) d.Dispose(); }
        public static unsafe void EscribeCrudos(object mapa, uint[] pargb)
        {
            Bitmap b = (Bitmap)mapa; int w = b.Width, h = b.Height;
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) ((uint*)((byte*)d.Scan0 + (long)y * d.Stride))[x] = pargb[y * w + x];
            b.UnlockBits(d);
        }
        public static object RutaElipse(RectangleF r) { GraphicsPath gp = new GraphicsPath(); gp.AddEllipse(r); return gp; }
    }
}

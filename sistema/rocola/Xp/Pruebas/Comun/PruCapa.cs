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
    }

    internal static class Pru
    {
        public const bool Capa = true;   // (esta es la de la referencia)
        public static readonly Type TMapa = typeof(Bitmap);
        public static object NuevoMapa(int w, int h, string formato) { return new Bitmap(w, h, Formato(formato)); }
        public static object DeSk(SKBitmap b, string formato) { return b == null ? null : new Bitmap(b, Formato(formato)); }
        public static object Aprecia(object o, string formato) { return o as Bitmap ?? new Bitmap((SKBitmap)o, Formato(formato)); }
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
        public static Lz Lienzo(object mapa) { return new Lz((Bitmap)mapa); }
    }
}

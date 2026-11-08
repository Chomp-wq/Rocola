// (HERRAMIENTA de Pruebas/TrazaDibujo: prepara.sh la copia a la copia instrumentada de la referencia; no va en el programa) LA TRAZA POR LINEA DE XP: cada llamada a GDI+ desde
// el codigo (Codigo/ y Plataforma/, no las de dentro de Dibujo2D.cs) con su archivo:linea, la operacion, los argumentos y el estado del
// Graphics (suavizado, modo de letra, copia, filtro, medio pixel, matriz, matriz del HDC, recorte y destino). Se junta en memoria por
// (archivo, linea, operacion, estado) y se escribe en ROCOLA_TRAZA_GDI cada 5 s y al salir.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using SkiaSharp;

namespace Rocola.Dibujo2D
{
    internal static class Traza
    {
        private static readonly string ruta = Environment.GetEnvironmentVariable("ROCOLA_TRAZA_GDI");
        internal static readonly bool On = !string.IsNullOrEmpty(ruta);
        private sealed class E { public long N; public readonly List<string> Args = new List<string>(); public double Primera, Ultima; }
        private static readonly Dictionary<string, E> tabla = new Dictionary<string, E>();
        private static readonly System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
        private static System.Threading.Timer cada;
        static Traza()
        {
            if (!On) return;
            AppDomain.CurrentDomain.ProcessExit += delegate { Vuelca(); };
            cada = new System.Threading.Timer(delegate { Vuelca(); }, null, 5000, 5000);
        }
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private static string N(float v) { return v.ToString("0.###", ci); }
        private static string N(double v) { return v.ToString("0.###", ci); }
        private static bool Ent(float v) { return Math.Abs(v - Math.Round(v)) < 1e-4; }
        private static string Mat(SKMatrix m)
        {
            if (m.Persp0 != 0 || m.Persp1 != 0) return "P";
            if (m.SkewX != 0 || m.SkewY != 0) return "R";
            if (m.ScaleX != 1 || m.ScaleY != 1) return "S";
            if (m.TransX == 0 && m.TransY == 0) return "I";
            return Ent(m.TransX) && Ent(m.TransY) ? "T" : "Tf";
        }
        internal static string Estado(Graphics g)
        {
            SKMatrix total = g.C.TotalMatrix, inv, rel = total;
            if (g.baseMatriz.TryInvert(out inv)) rel = inv.PreConcat(total);
            SKRectI d = g.C.DeviceClipBounds;
            string destino = g.Destino.Width > 0 ? "mapa" : "lienzo";
            bool recortado = g.Destino.Width > 0 && (d.Left > 0 || d.Top > 0 || d.Right < g.Destino.Width || d.Bottom < g.Destino.Height);
            return "s=" + g.SmoothingMode + " t=" + g.TextRenderingHint + " c=" + g.CompositingMode + " i=" + g.InterpolationMode + " p=" + g.PixelOffsetMode +
                " m=" + Mat(rel) + " b=" + Mat(g.baseMatriz) + " r=" + (g.conRecorte || g.recorte.HasValue ? "si" : recortado ? "hdc" : "no") + " d=" + destino;
        }
        private static string Color(Color c) { return "#" + c.ToArgb().ToString("X8"); }
        internal static string Arg(object o)
        {
            if (o == null) return "null";
            string s = o as string;
            if (s != null) { s = s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "\\|"); return "\"" + (s.Length > 40 ? s.Substring(0, 40) + "..." : s) + "\""; }
            if (o is float) return N((float)o);
            if (o is double) return N((double)o);
            if (o is int || o is long || o is bool) return Convert.ToString(o, ci);
            if (o is Color) return Color((Color)o);
            if (o is RectangleF) { RectangleF r = (RectangleF)o; return "RF(" + N(r.X) + "," + N(r.Y) + "," + N(r.Width) + "," + N(r.Height) + ")"; }
            if (o is Rectangle) { Rectangle r = (Rectangle)o; return "R(" + r.X + "," + r.Y + "," + r.Width + "," + r.Height + ")"; }
            if (o is PointF) { PointF p = (PointF)o; return "PF(" + N(p.X) + "," + N(p.Y) + ")"; }
            if (o is Point) { Point p = (Point)o; return "P(" + p.X + "," + p.Y + ")"; }
            if (o is SizeF) { SizeF p = (SizeF)o; return "SF(" + N(p.Width) + "," + N(p.Height) + ")"; }
            if (o is Size) { Size p = (Size)o; return "S(" + p.Width + "," + p.Height + ")"; }
            SolidBrush sb = o as SolidBrush; if (sb != null) return "SB(" + Color(sb.Color) + ")";
            LinearGradientBrush lg = o as LinearGradientBrush;
            if (lg != null) return "LGB(" + Color(lg.LinearColors[0]) + "," + Color(lg.LinearColors[1]) + ",wrap=" + lg.WrapMode + (lg.InterpolationColors != null ? ",ic=" + lg.InterpolationColors.Colors.Length : "") + (lg.Blend != null ? ",blend" : "") + ")";
            PathGradientBrush pg = o as PathGradientBrush;
            if (pg != null) return "PGB(c=" + Color(pg.CenterColor) + ",o=" + (pg.SurroundColors != null && pg.SurroundColors.Length > 0 ? Color(pg.SurroundColors[0]) : "-") + (pg.Blend != null ? ",blend" + pg.Blend.Factors.Length : "") + ")";
            TextureBrush tb = o as TextureBrush; if (tb != null) return "TB(" + tb.WrapMode + ")";
            Pen pen = o as Pen;
            if (pen != null) return "Pen(" + Color(pen.Color) + ",w=" + N(pen.Width) + ",j=" + pen.LineJoin + ",c=" + pen.StartCap + "/" + pen.EndCap + (pen.DashStyle != DashStyle.Solid ? ",dash=" + pen.DashStyle : "") + (pen.Brush != null ? ",brush" : "") + ")";
            Font f = o as Font; if (f != null) return "F(" + f.FontFamily.Name + "," + N(f.Size) + "," + f.Style + "," + f.Unit + ")";
            FontFamily ff = o as FontFamily; if (ff != null) return "FF(" + ff.Name + ")";
            StringFormat sf = o as StringFormat;
            if (sf != null) return "FMT(" + sf.Alignment + "," + sf.LineAlignment + ",[" + sf.FormatFlags + "]," + sf.Trimming + (sf.Tipografico ? ",tip" : "") + (ReferenceEquals(sf, StringFormat.GenericTypographic) ? ",GT" : ReferenceEquals(sf, StringFormat.GenericDefault) ? ",GD" : "") + ")";
            Image im = o as Image; if (im != null) return "IMG(" + (im.Sk != null ? im.Width + "x" + im.Height : "?") + "," + im.PixelFormat + ")";
            ImageAttributes ia = o as ImageAttributes; if (ia != null) return "IA(" + (ia.Matriz != null ? "mat" : "") + ",wrap=" + ia.Envoltura + (ia.HayClave ? ",clave" : "") + ")";
            GraphicsPath gp = o as GraphicsPath; if (gp != null) return "GP(" + gp.PointCount + ")";
            if (o is Region) return "Rgn";
            PointF[] pfs = o as PointF[]; if (pfs != null) return "PF[" + pfs.Length + "]";
            Point[] ps = o as Point[]; if (ps != null) return "P[" + ps.Length + "]";
            if (o is Enum) return o.ToString().Replace(", ", "|");
            if (o is Stream) return "stream";
            return o.GetType().Name;
        }
        internal static void Op(Graphics g, string op, string cf, int cl, params object[] a)
        {
            if (!On || cf == null || cf.EndsWith("Dibujo2D.cs", StringComparison.Ordinal)) return;
            int k = cf.LastIndexOf("/Xp/", StringComparison.Ordinal);
            string archivo = k >= 0 ? cf.Substring(k + 4) : cf;
            string estado = g != null ? Estado(g) : "-";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(Arg(a[i])); }
            string clave = archivo + "\t" + cl + "\t" + op + "\t" + estado;
            double t = reloj.Elapsed.TotalSeconds;
            lock (tabla)
            {
                E e;
                if (!tabla.TryGetValue(clave, out e)) { e = new E(); e.Primera = t; tabla[clave] = e; }
                e.N++; e.Ultima = t;
                string args = sb.ToString();
                if (e.Args.Count < 4 && !e.Args.Contains(args)) e.Args.Add(args);
            }
        }
        private static void Vuelca()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                lock (tabla)
                    foreach (KeyValuePair<string, E> p in tabla)
                        sb.Append(p.Key).Append('\t').Append(p.Value.N).Append('\t').Append(N(p.Value.Primera)).Append('\t').Append(N(p.Value.Ultima)).Append('\t').Append(string.Join(" || ", p.Value.Args)).Append('\n');
                File.WriteAllText(ruta + ".tmp", sb.ToString());
                File.Move(ruta + ".tmp", ruta, true);
            }
            catch { }
        }
    }
}

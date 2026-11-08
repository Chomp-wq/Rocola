// BANCO DEL DIBUJO: cada caso hecho con la API que imita a GDI+ (Rocola.Dibujo2D). Se compila dos veces: con el Dibujo2D.cs congelado de
// la referencia (Congelado/) y con el de la copia de trabajo, que llama a las funciones nativas (Nuevo/). Las dos tienen que dar lo mismo,
// byte a byte. Solo la API publica (y Image.Sk para leer los puntos tal cual: el archivo se compila dentro del mismo ensamblado).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using Rocola.Dibujo2D;

namespace Banco
{
    internal static class EjecutaGdi
    {
#if NATIVO
        // ejecutor "puente" (paso D3): lo mismo, pero el lienzo, las imagenes, las letras y los formatos pasan por PuenteDibujo (Plataforma/
        // Puente.cs), como cuando un archivo ya traducido y uno sin traducir se pasan esas cosas: tiene que dar lo mismo que "gdi"
        internal static bool PorPuente;
#endif
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private static string R(float v) { return v.ToString("R", ci); }
        private static string R(RectangleF v) { return "(" + R(v.X) + "," + R(v.Y) + "," + R(v.Width) + "," + R(v.Height) + ")"; }

        internal static unsafe byte[] Bytes(Image i)
        {
            if (i == null) return new byte[0];
            byte[] b = new byte[i.Width * i.Height * 4];
            byte* p = (byte*)i.Sk.GetPixels(); int rb = i.Sk.RowBytes;
            for (int y = 0; y < i.Height; y++) for (int x = 0; x < i.Width * 4; x++) b[y * i.Width * 4 + x] = p[y * rb + x];
            return b;
        }
        internal static unsafe Bitmap Mapa(ImagenD d)
        {
            PixelFormat f = d.Formato == 0 ? PixelFormat.Format32bppPArgb : d.Formato == 1 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
#if NATIVO
            Bitmap b = PorPuente ? Rocola.PuenteDibujo.Bitmap(Rocola.Nativo.Mapas.Nuevo(d.W, d.H), f) : new Bitmap(d.W, d.H, f);
#else
            Bitmap b = new Bitmap(d.W, d.H, f);
#endif
            BitmapData bd = b.LockBits(new Rectangle(0, 0, d.W, d.H), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            for (int y = 0; y < d.H; y++) { uint* fila = (uint*)((byte*)bd.Scan0 + y * bd.Stride); for (int x = 0; x < d.W; x++) fila[x] = d.Px[y * d.W + x]; }
            b.UnlockBits(bd);
            return b;
        }
        private static GraphicsPath Ruta(RutaD rd)
        {
            GraphicsPath gp = new GraphicsPath();
            if (rd.Relleno == 1) gp.FillMode = FillMode.Winding;
            foreach (object[] p in rd.Pasos)
            {
                switch ((string)p[0])
                {
                    case "Arco": gp.AddArc((float)p[1], (float)p[2], (float)p[3], (float)p[4], (float)p[5], (float)p[6]); break;
                    case "Linea": gp.AddLine((float)p[1], (float)p[2], (float)p[3], (float)p[4]); break;
                    case "Bezier": gp.AddBezier((float)p[1], (float)p[2], (float)p[3], (float)p[4], (float)p[5], (float)p[6], (float)p[7], (float)p[8]); break;
                    case "Rect": gp.AddRectangle((RectangleF)p[1]); break;
                    case "Elipse": gp.AddEllipse((float)p[1], (float)p[2], (float)p[3], (float)p[4]); break;
                    case "Poligono": gp.AddPolygon((PointF[])p[1]); break;
                    case "Porcion": gp.AddPie((float)p[1], (float)p[2], (float)p[3], (float)p[4], (float)p[5], (float)p[6]); break;
                    case "Cierra": gp.CloseFigure(); break;
                    case "Empieza": gp.StartFigure(); break;
                }
            }
            return gp;
        }
        internal static Brush Pincel(PincelD p)
        {
            LinearGradientBrush lg = null;
            switch (p.Tipo)
            {
                case 0: return new SolidBrush(p.C1);
                case 1: lg = new LinearGradientBrush(p.P0, p.P1, p.C1, p.C2); break;
                case 2: lg = new LinearGradientBrush(p.Caja, p.C1, p.C2, (LinearGradientMode)p.Modo); break;
                case 3: lg = p.Escalable ? new LinearGradientBrush(p.Caja, p.C1, p.C2, p.Angulo, true) : new LinearGradientBrush(p.Caja, p.C1, p.C2, p.Angulo); break;
                case 4:
                    {
                        GraphicsPath gp = new GraphicsPath();
                        if (p.FormaRuta == 0) gp.AddEllipse(p.Caja);
                        else if (p.FormaRuta == 1) gp.AddRectangle(p.Caja);
                        else gp.AddPolygon(new[] { new PointF(p.Caja.X, p.Caja.Y), new PointF(p.Caja.Right, p.Caja.Y + p.Caja.Height / 3), new PointF(p.Caja.X + p.Caja.Width / 2, p.Caja.Bottom) });
                        PathGradientBrush pg = new PathGradientBrush(gp);
                        pg.CenterColor = p.C1; pg.SurroundColors = new Color[] { p.C2 };
                        if (p.Corrido) pg.CenterPoint = p.Centro;
                        if (p.BlendF != null) { Blend bl = new Blend(p.BlendF.Length); Array.Copy(p.BlendF, bl.Factors, p.BlendF.Length); Array.Copy(p.BlendP, bl.Positions, p.BlendP.Length); pg.Blend = bl; }
                        return pg;
                    }
                default:
                    {
                        TextureBrush tb = new TextureBrush(Mapa(p.Img), (WrapMode)p.Wrap);
                        if (p.TX != 0 || p.TY != 0) tb.TranslateTransform(p.TX, p.TY);
                        return tb;
                    }
            }
            if (p.BlendF != null) { Blend bl = new Blend(p.BlendF.Length); Array.Copy(p.BlendF, bl.Factors, p.BlendF.Length); Array.Copy(p.BlendP, bl.Positions, p.BlendP.Length); lg.Blend = bl; }
            if (p.IcC != null) { ColorBlend cb = new ColorBlend(p.IcC.Length); Array.Copy(p.IcC, cb.Colors, p.IcC.Length); Array.Copy(p.IcP, cb.Positions, p.IcP.Length); lg.InterpolationColors = cb; }
            if (p.Wrap >= 0) lg.WrapMode = (WrapMode)p.Wrap;
            return lg;
        }
        private static Pen Pluma(PlumaD d)
        {
            Pen pen = d.Pincel != null ? new Pen(Pincel(d.Pincel), d.W) : new Pen(d.C, d.W);
            pen.LineJoin = (LineJoin)d.Join; pen.StartCap = (LineCap)d.CapIni; pen.EndCap = (LineCap)d.CapFin; pen.DashStyle = (DashStyle)d.Dash;
            if (d.Patron != null) pen.DashPattern = d.Patron;
            return pen;
        }
        internal static Font Letra(LetraD l)
        {
#if NATIVO
            // (la Letra solo sabe de puntos y pixeles, las unidades de XP: con las demas la letra va tal cual)
            if (PorPuente) { Font f0 = LetraGdi(l); return f0.Unit == GraphicsUnit.Point || f0.Unit == GraphicsUnit.Pixel ? Rocola.PuenteDibujo.Font(Rocola.PuenteDibujo.Letra(f0)) : f0; }
            return LetraGdi(l);
        }
        private static Font LetraGdi(LetraD l)
        {
#endif
            FontStyle st = (FontStyle)l.Estilo; GraphicsUnit u = (GraphicsUnit)l.Unidad;
            if (l.Generica >= 0)
            {
                FontFamily fam = l.Generica == 0 ? FontFamily.GenericSerif : l.Generica == 1 ? FontFamily.GenericSansSerif : FontFamily.GenericMonospace;
                switch (l.Ctor) { case 0: return new Font(fam, l.Tam, st, u); case 1: return new Font(fam, l.Tam, st); case 2: return new Font(fam, l.Tam); default: return new Font(fam, l.Tam, u); }
            }
            switch (l.Ctor) { case 0: return new Font(l.Familia, l.Tam, st, u); case 1: return new Font(l.Familia, l.Tam, st); case 2: return new Font(l.Familia, l.Tam); default: return new Font(new Font(l.Familia, l.Tam, FontStyle.Regular, u), st); }
        }
        // los dos genericos son compartidos (como en GDI+): cada caso empieza con ellos como vienen de fabrica
        internal static void GenericosDeFabrica()
        {
            StringFormat t = StringFormat.GenericTypographic;
            t.Alignment = StringAlignment.Near; t.LineAlignment = StringAlignment.Near; t.Trimming = StringTrimming.None;
            t.FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.LineLimit | StringFormatFlags.NoClip;
            StringFormat d = StringFormat.GenericDefault;
            d.Alignment = StringAlignment.Near; d.LineAlignment = StringAlignment.Near; d.Trimming = StringTrimming.Character; d.FormatFlags = 0;
        }
        internal static StringFormat Formato(FormatoD f)
        {
#if NATIVO
            StringFormat r = FormatoGdi(f);
            return PorPuente ? Rocola.PuenteDibujo.StringFormat(Rocola.PuenteDibujo.Formato(r)) : r;
        }
        private static StringFormat FormatoGdi(FormatoD f)
        {
#endif
            StringFormat sf;
            switch (f.Tipo)
            {
                case 0: return null;
                case 1: sf = new StringFormat(); break;
                case 2: sf = StringFormat.GenericTypographic; break;
                case 3: sf = StringFormat.GenericDefault; break;
                case 4: sf = new StringFormat(StringFormat.GenericTypographic); break;
                default: sf = new StringFormat((StringFormatFlags)f.Flags); break;
            }
            if (f.CambiaAl)
            {
                sf.Alignment = (StringAlignment)f.Al; sf.LineAlignment = (StringAlignment)f.LAl; sf.Trimming = (StringTrimming)f.Trim;
                if (f.FlagsConO) sf.FormatFlags |= (StringFormatFlags)f.Flags; else sf.FormatFlags = (StringFormatFlags)f.Flags;
            }
            return sf;
        }
        private static void PonEstado(Graphics g, EstadoD e)
        {
            g.SmoothingMode = (SmoothingMode)e.Suave; g.TextRenderingHint = (TextRenderingHint)e.Letra; g.CompositingMode = (CompositingMode)e.Copia;
            g.InterpolationMode = (InterpolationMode)e.Filtro; g.PixelOffsetMode = (PixelOffsetMode)e.Medio; g.CompositingQuality = (CompositingQuality)e.Calidad;
            foreach (object[] m in e.Matriz)
            {
                if ((string)m[0] == "T") g.TranslateTransform((float)m[1], (float)m[2]);
                else if ((string)m[0] == "S") g.ScaleTransform((float)m[1], (float)m[2]);
                else g.RotateTransform((float)m[1]);
            }
            foreach (object[] k in e.Recortes)
            {
                switch ((string)k[0])
                {
                    case "R": g.SetClip((RectangleF)k[1], (CombineMode)(int)k[2]); break;
                    case "P": using (GraphicsPath gp = Ruta((RutaD)k[1])) g.SetClip(gp, (CombineMode)(int)k[2]); break;
                    case "I": g.IntersectClip((RectangleF)k[1]); break;
                    case "X": g.ExcludeClip((Rectangle)k[1]); break;
                    case "Q": g.ResetClip(); break;
                    default: if ((int)k[2] == 0) g.SetClip((Rectangle)k[1]); else g.SetClip((Rectangle)k[1], CombineMode.Intersect); break;
                }
            }
        }

        internal static byte[] Hace(Caso c, StringBuilder v)
        {
            GenericosDeFabrica();
            using (Bitmap b = new Bitmap(c.W, c.H, c.FormatoLienzo == 0 ? PixelFormat.Format32bppPArgb : PixelFormat.Format32bppArgb))
            {
#if NATIVO
                SkiaSharp.SKCanvas lienzo = PorPuente ? new SkiaSharp.SKCanvas(Rocola.PuenteDibujo.Mapa(b)) : null;
                using (Graphics g = PorPuente ? Rocola.PuenteDibujo.Graphics(lienzo, SkiaSharp.SKMatrix.Identity) : Graphics.FromImage(b))
#else
                using (Graphics g = Graphics.FromImage(b))
#endif
                {
                    if (c.Fondo.HasValue) g.Clear(c.Fondo.Value);
                    PonEstado(g, c.Estado);
                    foreach (object[] op in c.Ops)
                    {
                        try { Op(g, op, v); }
                        catch (Exception ex) { v.Append(" EXC:" + ex.GetType().Name); }
                    }
                }
#if NATIVO
                if (lienzo != null) { lienzo.Flush(); lienzo.Dispose(); }
#endif
                return Bytes(b);
            }
        }

        private static Rectangle E(RectangleF r) { return new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height); }
        private static Point[] E(PointF[] p) { Point[] q = new Point[p.Length]; for (int i = 0; i < p.Length; i++) q[i] = new Point((int)p[i].X, (int)p[i].Y); return q; }

        private static void Op(Graphics g, object[] op, StringBuilder v)
        {
            string n = (string)op[0];
            switch (n)
            {
                case "DrawString":
                    {
                        int k = (int)op[1]; string s = (string)op[2]; RectangleF r = (RectangleF)op[5];
                        using (Font f = Letra((LetraD)op[3])) using (Brush br = Pincel((PincelD)op[4]))
                        {
                            StringFormat sf = Formato((FormatoD)op[6]);
                            switch (k)
                            {
                                case 0: g.DrawString(s, f, br, r.X, r.Y); break;
                                case 1: g.DrawString(s, f, br, r.Location); break;
                                case 2: g.DrawString(s, f, br, r.X, r.Y, sf); break;
                                case 3: g.DrawString(s, f, br, r.Location, sf); break;
                                case 4: g.DrawString(s, f, br, r); break;
                                case 5: g.DrawString(s, f, br, r, sf); break;
                                default: g.DrawString(s, f, br, E(r), sf); break;
                            }
                        }
                        break;
                    }
                case "Compartido":
                    {
                        FormatoD fd = (FormatoD)op[5]; StringFormat t = StringFormat.GenericTypographic;
                        t.Alignment = (StringAlignment)fd.Al; t.LineAlignment = (StringAlignment)fd.LAl;
                        if (fd.FlagsConO) t.FormatFlags |= (StringFormatFlags)fd.Flags; else t.FormatFlags = (StringFormatFlags)fd.Flags;
                        using (Font f = Letra((LetraD)op[2])) using (Brush br = Pincel((PincelD)op[3]))
                        {
                            RectangleF r = (RectangleF)op[4];
                            g.DrawString((string)op[1], f, br, r, StringFormat.GenericTypographic);
                            g.DrawString((string)op[1], f, br, r.X, r.Y, StringFormat.GenericTypographic);
                            SizeF m = g.MeasureString((string)op[1], f, PointF.Empty, StringFormat.GenericTypographic);
                            StringFormat copia = new StringFormat(StringFormat.GenericTypographic);
                            SizeF m2 = g.MeasureString((string)op[1], f, r.Size, copia);
                            v.Append(" compartido=" + R(m.Width) + "x" + R(m.Height) + "/" + R(m2.Width) + "x" + R(m2.Height));
                        }
                        break;
                    }
                case "MeasureString":
                    {
                        int k = (int)op[1]; string s = (string)op[2];
                        using (Font f = Letra((LetraD)op[3]))
                        {
                            StringFormat sf = Formato((FormatoD)op[4]);
                            SizeF caja = new SizeF((float)op[5], (float)op[6]); int ancho = (int)op[7];
                            SizeF m; int letras = -1, lineas = -1;
                            switch (k)
                            {
                                case 0: m = g.MeasureString(s, f); break;
                                case 1: m = g.MeasureString(s, f, ancho); break;
                                case 2: m = g.MeasureString(s, f, ancho, sf); break;
                                case 3: m = g.MeasureString(s, f, caja); break;
                                case 4: m = g.MeasureString(s, f, new PointF(caja.Width, caja.Height), sf); break;
                                case 5: m = g.MeasureString(s, f, caja, sf); break;
                                default: m = g.MeasureString(s, f, caja, sf, out letras, out lineas); break;
                            }
                            v.Append(" mide=" + R(m.Width) + "x" + R(m.Height) + "/" + letras + "/" + lineas);
                        }
                        break;
                    }
                case "DrawText":
                    {
                        int k = (int)op[1]; string s = (string)op[2]; Rectangle r = (Rectangle)op[4]; Point p = (Point)op[5]; Color col = (Color)op[6];
                        TextFormatFlags fl = (TextFormatFlags)(int)op[7];
                        using (Font f = Letra((LetraD)op[3]))
                            switch (k)
                            {
                                case 0: TextRenderer.DrawText(g, s, f, r, col); break;
                                case 1: TextRenderer.DrawText(g, s, f, p, col); break;
                                case 2: TextRenderer.DrawText(g, s, f, p, col, fl); break;
                                default: TextRenderer.DrawText(g, s, f, r, col, fl); break;
                            }
                        break;
                    }
                case "MeasureText":
                    {
                        int k = (int)op[1]; string s = (string)op[2]; Size caja = (Size)op[4]; TextFormatFlags fl = (TextFormatFlags)(int)op[5];
                        using (Font f = Letra((LetraD)op[3]))
                        {
                            Size m;
                            switch (k)
                            {
                                case 0: m = TextRenderer.MeasureText(s, f); break;
                                case 1: m = TextRenderer.MeasureText(s, f, caja); break;
                                case 2: m = TextRenderer.MeasureText(s, f, caja, fl); break;
                                case 3: m = TextRenderer.MeasureText(g, s, f); break;
                                case 4: m = TextRenderer.MeasureText(g, s, f, caja); break;
                                default: m = TextRenderer.MeasureText(g, s, f, caja, fl); break;
                            }
                            v.Append(" midegdi=" + m.Width + "x" + m.Height);
                        }
                        break;
                    }
                case "DrawImage": Imagen(g, op, v); break;
                case "LockBits": Puntos(op, v); break;
                case "FromStream":
                    {
                        int tipo = (int)op[1]; ImagenD im = (ImagenD)op[2];
                        byte[] datos = tipo == 5 ? new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 } : Casos.Archivo(im, tipo, (int)op[3]);
                        using (MemoryStream ms = new MemoryStream(datos))
                        using (Image i = tipo % 2 == 0 ? Image.FromStream(ms, false, false) : Image.FromStream(ms))
                        {
                            v.Append(" lee=" + i.Width + "x" + i.Height + "/" + i.PixelFormat + "/" + i.Flags + "/" + Image.IsAlphaPixelFormat(i.PixelFormat) + "/" + ((i.PixelFormat & PixelFormat.Indexed) != 0));
                            v.Append(" puntos=" + Programa.Huella(Bytes(i)));
                        }
                        break;
                    }
                case "Save":
                    {
                        int tipo = (int)op[1]; string ruta = Path.Combine(Path.GetTempPath(), "banco-dibujo-" + Environment.ProcessId + ".img");
                        using (Bitmap b = Mapa((ImagenD)op[2]))
                        {
                            byte[] datos;
                            if (tipo == 2) using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); datos = ms.ToArray(); }
                            else
                            {
                                if (tipo == 0) b.Save(ruta, ImageFormat.Png);
                                else
                                {
                                    ImageCodecInfo codec = null;
                                    foreach (ImageCodecInfo e in ImageCodecInfo.GetImageEncoders()) if (e.MimeType == (tipo == 1 ? "image/jpeg" : "image/png")) codec = e;
                                    using (EncoderParameters ep = new EncoderParameters(1)) { ep.Param[0] = new EncoderParameter(Rocola.Dibujo2D.Encoder.Quality, (long)(int)op[3]); b.Save(ruta, codec, tipo == 1 ? ep : null); }
                                }
                                datos = File.ReadAllBytes(ruta); File.Delete(ruta);
                            }
                            v.Append(" archivo=" + datos.Length + "/" + Programa.Huella(datos));
                        }
                        break;
                    }
                case "ClipBounds":
                    {
                        int k = (int)op[3]; RectangleF caja = (RectangleF)op[1];
                        RectangleF cb;
                        switch (k)
                        {
                            case 0: cb = g.ClipBounds; break;
                            case 1: cb = g.VisibleClipBounds; break;
                            case 2: g.SetClip(caja); cb = g.ClipBounds; break;
                            case 3: using (GraphicsPath gp = Ruta((RutaD)op[2])) g.SetClip(gp, CombineMode.Intersect); cb = g.ClipBounds; break;
                            case 4: g.IntersectClip(caja); cb = g.VisibleClipBounds; break;
                            default: g.ResetClip(); cb = g.ClipBounds; break;
                        }
                        v.Append(" recorte=" + R(cb));
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(160, 200, 40, 90))) g.FillRectangle(sb, -1000, -1000, 3000, 3000);
                        break;
                    }
                case "Letra":
                    {
                        using (Font f = Letra((LetraD)op[1]))
                            v.Append(" letra=" + f.Name + "/" + R(f.Size) + "/" + f.Unit + "/" + f.Style + "/" + f.Bold + f.Italic + f.Underline + "/" + R(f.SizeInPoints) + "/" + R(f.GetHeight()) + "/" + R(f.GetHeight(g)) + "/" + f.Height + "/" + f.FontFamily.Name);
                        try { using (FontFamily ff = new FontFamily((string)op[2])) v.Append(" familia=" + ff.Name); }
                        catch (Exception ex) { v.Append(" familia:" + ex.GetType().Name); }
                        break;
                    }
                case "AddString":
                    {
                        using (Font f = Letra((LetraD)op[2])) using (GraphicsPath gp = new GraphicsPath()) using (Brush br = Pincel((PincelD)op[5])) using (Pen pen = Pluma((PlumaD)op[6]))
                        {
                            gp.AddString((string)op[1], f.FontFamily, (int)f.Style, f.Size, (RectangleF)op[3], Formato((FormatoD)op[4]));
                            int k = (int)op[7];
                            if (k != 1) g.FillPath(br, gp);
                            if (k != 0) g.DrawPath(pen, gp);
                            v.Append(" puntosruta=" + gp.PointCount + " caja=" + R(gp.GetBounds()));
                        }
                        break;
                    }
                case "Estado":
                    {
                        int k = (int)op[4]; RectangleF caja = (RectangleF)op[2];
                        using (Brush br = Pincel((PincelD)op[1]))
                        {
                            GraphicsState st = g.Save();
                            PonEstado(g, (EstadoD)op[3]);
                            g.FillRectangle(br, caja);
                            using (Font f = Letra((LetraD)op[6])) g.DrawString((string)op[5], f, br, caja, Formato((FormatoD)op[7]));
                            if (k == 1) { Matrix m = g.Transform; g.ResetTransform(); g.FillRectangle(br, caja); g.Transform = m; }
                            if (k == 3) { Matrix m = g.Transform; PointF[] pts = { caja.Location, new PointF(caja.Right, caja.Bottom) }; m.TransformPoints(pts); v.Append(" puntos=" + R(pts[0].X) + "," + R(pts[0].Y) + "," + R(pts[1].X) + "," + R(pts[1].Y)); }
                            g.Restore(st);
                            if (k == 2) g.ResetTransform();
                            g.FillEllipse(br, caja);
                            v.Append(" clip=" + R(g.ClipBounds));
                        }
                        break;
                    }
                default: Forma(g, op, v); break;
            }
        }

        private static void Forma(Graphics g, object[] op, StringBuilder v)
        {
            string n = (string)op[0]; int k = (int)op[1];
            RectangleF r = (RectangleF)op[4]; PointF[] picos = (PointF[])op[6]; float a1 = (float)op[8], a2 = (float)op[9];
            using (Brush br = Pincel((PincelD)op[2])) using (Pen pen = Pluma((PlumaD)op[3]))
            {
                switch (n)
                {
                    case "FillRect":
                        if (k == 0) g.FillRectangle(br, r); else if (k == 1) g.FillRectangle(br, E(r)); else if (k == 2) g.FillRectangle(br, r.X, r.Y, r.Width, r.Height); else g.FillRectangle(br, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                        break;
                    case "DrawRect":
                        if (k == 0) g.DrawRectangle(pen, r); else if (k == 1) g.DrawRectangle(pen, E(r)); else if (k == 2) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height); else g.DrawRectangle(pen, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                        break;
                    case "FillEllipse":
                        if (k == 0) g.FillEllipse(br, r); else if (k == 1) g.FillEllipse(br, E(r)); else if (k == 2) g.FillEllipse(br, r.X, r.Y, r.Width, r.Height); else g.FillEllipse(br, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                        break;
                    case "DrawEllipse":
                        if (k == 0) g.DrawEllipse(pen, r); else if (k == 1) g.DrawEllipse(pen, E(r)); else if (k == 2) g.DrawEllipse(pen, r.X, r.Y, r.Width, r.Height); else g.DrawEllipse(pen, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                        break;
                    case "FillPath": using (GraphicsPath gp = Ruta((RutaD)op[5])) g.FillPath(br, gp); break;
                    case "DrawPath": using (GraphicsPath gp = Ruta((RutaD)op[5])) g.DrawPath(pen, gp); break;
                    case "FillPolygon":
                        if (k == 0) g.FillPolygon(br, picos); else if (k == 2) g.FillPolygon(br, E(picos)); else g.FillPolygon(br, picos, (FillMode)(int)op[7]);
                        break;
                    case "DrawPolygon": if (k < 2) g.DrawPolygon(pen, picos); else g.DrawPolygon(pen, E(picos)); break;
                    case "DrawLine":
                        if (k == 0) g.DrawLine(pen, r.X, r.Y, r.Right, r.Bottom); else if (k == 1) g.DrawLine(pen, (int)r.X, (int)r.Y, (int)r.Right, (int)r.Bottom);
                        else if (k == 2) g.DrawLine(pen, picos[0], picos[1]); else g.DrawLine(pen, new Point((int)picos[0].X, (int)picos[0].Y), new Point((int)picos[1].X, (int)picos[1].Y));
                        break;
                    case "DrawLines": if (k < 2) g.DrawLines(pen, picos); else g.DrawLines(pen, E(picos)); break;
                    case "DrawArc":
                        if (k == 0) g.DrawArc(pen, r, a1, a2); else if (k == 1) g.DrawArc(pen, E(r), a1, a2); else g.DrawArc(pen, r.X, r.Y, r.Width, r.Height, a1, a2);
                        break;
                    case "FillPie": if (k < 2) g.FillPie(br, r, a1, a2); else g.FillPie(br, r.X, r.Y, r.Width, r.Height, a1, a2); break;
                    case "DrawBezier": g.DrawBezier(pen, picos[0], picos[1], picos[2 % picos.Length], picos[3 % picos.Length]); break;
                    case "FillRegion":
                        using (GraphicsPath gp = Ruta((RutaD)op[5])) using (GraphicsPath gp2 = Ruta((RutaD)op[10])) using (Region rg = new Region(gp))
                        {
                            if (k == 0) rg.Exclude(gp2); else if (k == 1) rg.Intersect(gp2); else if (k == 2) rg.Union(gp2); else rg.Exclude(r);
                            g.FillRegion(br, rg);
                        }
                        break;
                    case "Clear": g.Clear((Color)op[11]); break;
                }
            }
        }

        private static void Imagen(Graphics g, object[] op, StringBuilder v)
        {
            int k = (int)op[1]; ImagenD im = (ImagenD)op[2]; RectangleF d = (RectangleF)op[3], o = (RectangleF)op[4]; Point p = (Point)op[5];
            float[][] m = (float[][])op[6]; int env = (int)op[7]; bool clave = (bool)op[8]; Color baja = (Color)op[9], alta = (Color)op[10]; PointF[] para = (PointF[])op[11];
            using (Bitmap b = Mapa(im))
            using (ImageAttributes ia = new ImageAttributes())
            {
                bool conIa = m != null || env >= 0 || clave;
                if (m != null) { ColorMatrix cm = new ColorMatrix(m); if ((k & 1) == 0) ia.SetColorMatrix(cm); else ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap); }
                if (env >= 0) ia.SetWrapMode((WrapMode)env);
                if (clave) ia.SetColorKey(baja, alta, ColorAdjustType.Bitmap);
                switch (k)
                {
                    case 0: g.DrawImage(b, p.X, p.Y); break;
                    case 1: g.DrawImage(b, d.X, d.Y); break;
                    case 2: g.DrawImage(b, p); break;
                    case 3: g.DrawImage(b, d.Location); break;
                    case 4: g.DrawImageUnscaled(b, p.X, p.Y); break;
                    case 5: g.DrawImageUnscaled(b, p); break;
                    case 6: g.DrawImage(b, E(d)); break;
                    case 7: g.DrawImage(b, d); break;
                    case 8: g.DrawImage(b, d.X, d.Y, d.Width, d.Height); break;
                    case 9: g.DrawImage(b, (int)d.X, (int)d.Y, (int)d.Width, (int)d.Height); break;
                    case 10: g.DrawImage(b, E(d), E(o), GraphicsUnit.Pixel); break;
                    case 11: g.DrawImage(b, d, o, GraphicsUnit.Pixel); break;
                    case 12:
                        if (conIa) g.DrawImage(b, E(d), (int)o.X, (int)o.Y, (int)o.Width, (int)o.Height, GraphicsUnit.Pixel, ia);
                        else g.DrawImage(b, E(d), (int)o.X, (int)o.Y, (int)o.Width, (int)o.Height, GraphicsUnit.Pixel);
                        break;
                    case 13: g.DrawImage(b, E(d), o.X, o.Y, o.Width, o.Height, GraphicsUnit.Pixel, ia); break;
                    default: g.DrawImage(b, para, o, GraphicsUnit.Pixel, ia); break;
                }
            }
        }

        private static unsafe void Puntos(object[] op, StringBuilder v)
        {
            ImagenD im = (ImagenD)op[1]; Rectangle zona = (Rectangle)op[2]; int fmt = (int)op[3], modo = (int)op[4]; uint[] datos = (uint[])op[5];
            int gx = (int)op[6], gy = (int)op[7]; Color sc = (Color)op[8];
            PixelFormat f = fmt == 0 ? PixelFormat.Format32bppPArgb : fmt == 1 ? PixelFormat.Format32bppArgb : fmt == 2 ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppRgb;
            using (Bitmap b = Mapa(im))
            {
                BitmapData bd = b.LockBits(zona, (ImageLockMode)modo, f);
                int bpp = fmt == 2 ? 3 : 4;
                byte[] leido = new byte[zona.Width * zona.Height * bpp];
                for (int y = 0; y < zona.Height; y++) for (int x = 0; x < zona.Width * bpp; x++) leido[y * zona.Width * bpp + x] = ((byte*)bd.Scan0)[y * bd.Stride + x];
                v.Append(" paso=" + (bd.Stride >= zona.Width * bpp) + " leido=" + Programa.Huella(leido));
                if ((modo & 2) != 0)
                    for (int y = 0; y < zona.Height; y++)
                        for (int x = 0; x < zona.Width; x++)
                        {
                            uint d = datos[y * zona.Width + x]; byte* q = (byte*)bd.Scan0 + y * bd.Stride + x * bpp;
                            if (fmt == 0) { uint a = d >> 24; d = (a << 24) | (Math.Min((d >> 16) & 255, a) << 16) | (Math.Min((d >> 8) & 255, a) << 8) | Math.Min(d & 255, a); }   // premultiplicado valido
                            for (int i = 0; i < bpp; i++) q[i] = (byte)(d >> (8 * i));
                        }
                b.UnlockBits(bd);
                v.Append(" tras=" + Programa.Huella(Bytes(b)));
                Color c = b.GetPixel(gx, gy); v.Append(" get=" + c.ToArgb().ToString("X8"));
                b.SetPixel(gx, gy, sc); v.Append(" set=" + Programa.Huella(Bytes(b)) + "/" + b.GetPixel(gx, gy).ToArgb().ToString("X8"));
                using (Bitmap cl = b.Clone(zona, f)) v.Append(" clon=" + cl.PixelFormat + "/" + Programa.Huella(Bytes(cl)));
                using (Bitmap esc = new Bitmap(b, Math.Max(1, zona.Width * 2), Math.Max(1, zona.Height / 2 + 1))) v.Append(" escala=" + Programa.Huella(Bytes(esc)));
            }
        }
    }
}

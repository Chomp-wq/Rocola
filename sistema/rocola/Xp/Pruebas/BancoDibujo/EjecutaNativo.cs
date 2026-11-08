// BANCO DEL DIBUJO: cada caso hecho como lo hara el codigo de XP traducido, con SkiaSharp directo y las funciones nativas (Xp/Nativo): sin
// Graphics, Font, Brush, Pen, Bitmap ni StringFormat. Es tambien la receta de la traduccion (la tabla de dibujo.md): el estado del Graphics
// pasa a variables (suave, ajuste, copia, filtro, medio), el recorte de SetClip a un Save del lienzo, los pinceles a SKPaint con su Color o
// su Shader de Degradado, las letras a Letra, los formatos a FormatoTexto. Tiene que dar lo mismo que EjecutaGdi, byte a byte.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using Rocola.Nativo;
using SkiaSharp;

namespace Banco
{
    internal static unsafe class EjecutaNativo
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private static string R(float v) { return v.ToString("R", ci); }
        private static string R(RectangleF v) { return "(" + R(v.X) + "," + R(v.Y) + "," + R(v.Width) + "," + R(v.Height) + ")"; }
        private static readonly string[] NombresFormato = { "Format32bppPArgb", "Format32bppArgb", "Format24bppRgb", "Format32bppRgb" };

        // EL ESTADO QUE EN XP LLEVABA EL Graphics, A LA VISTA
        private sealed class Lienzo
        {
            public SKCanvas C; public bool Suave, Copia, Medio; public AjusteLetra Ajuste; public Filtro Filtro; public int Suavizado, Medios;
            public int BaseCuenta; public bool ConRecorte;
            // SetClip con Replace (y ResetClip): el recorte de antes se va (RecorteLienzo.Quita conserva la matriz) y el nuevo va en su Save
            public void Quita() { if (!ConRecorte) return; RecorteLienzo.Quita(C, BaseCuenta); ConRecorte = false; }
            public void Recorta(SKRect r, int modo) { if (modo == 0) Quita(); if (!ConRecorte) { BaseCuenta = C.Save(); ConRecorte = true; } C.ClipRect(r, modo == 4 ? SKClipOperation.Difference : SKClipOperation.Intersect, false); }
            public void Recorta(SKPath p, int modo) { if (modo == 0) Quita(); if (!ConRecorte) { BaseCuenta = C.Save(); ConRecorte = true; } C.ClipPath(p, modo == 4 ? SKClipOperation.Difference : SKClipOperation.Intersect, false); }
        }
        private static void PonEstado(Lienzo l, EstadoD e)
        {
            l.Suavizado = e.Suave; l.Suave = e.Suave == 4 || e.Suave == 2;   // SmoothingMode AntiAlias o HighQuality
            l.Ajuste = (AjusteLetra)e.Letra; l.Copia = e.Copia == 1; l.Filtro = (Filtro)e.Filtro;
            l.Medios = e.Medio; l.Medio = e.Medio == 4 || e.Medio == 2;      // PixelOffsetMode Half o HighQuality
            foreach (object[] m in e.Matriz)
            {
                if ((string)m[0] == "T") l.C.Translate((float)m[1], (float)m[2]);
                else if ((string)m[0] == "S") l.C.Scale((float)m[1], (float)m[2]);
                else l.C.RotateDegrees((float)m[1]);
            }
            foreach (object[] k in e.Recortes)
            {
                switch ((string)k[0])
                {
                    case "R": l.Recorta(Sk((RectangleF)k[1]), (int)k[2]); break;
                    case "P": using (SKPath p = Ruta((RutaD)k[1])) l.Recorta(p, (int)k[2]); break;
                    case "I": l.Recorta(Sk((RectangleF)k[1]), 1); break;
                    case "X": l.Recorta(Sk((Rectangle)k[1]), 4); break;
                    case "Q": l.Quita(); break;
                    default: l.Recorta(Sk((Rectangle)k[1]), (int)k[2] == 0 ? 0 : 1); break;
                }
            }
        }
        private static SKRect Sk(RectangleF r) { return new SKRect(r.Left, r.Top, r.Right, r.Bottom); }

        internal static byte[] Bytes(SKBitmap b)
        {
            byte[] r = new byte[b.Width * b.Height * 4];
            byte* p = (byte*)b.GetPixels(); int rb = b.RowBytes;
            for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width * 4; x++) r[y * b.Width * 4 + x] = p[y * rb + x];
            return r;
        }
        internal static SKBitmap Mapa(ImagenD d)
        {
            SKBitmap b = Mapas.Nuevo(d.W, d.H);
            uint* p = (uint*)b.GetPixels(); int paso = b.RowBytes / 4;
            for (int y = 0; y < d.H; y++) for (int x = 0; x < d.W; x++) p[y * paso + x] = d.Px[y * d.W + x];
            b.NotifyPixelsChanged();
            return b;
        }
        // GraphicsPath de XP -> SKPath (EvenOdd de fabrica; un arco o una linea siguen la figura con una linea, salvo al empezar figura)
        private static SKPath Ruta(RutaD rd)
        {
            SKPath P = new SKPath { FillType = rd.Relleno == 1 ? SKPathFillType.Winding : SKPathFillType.EvenOdd };
            bool nueva = true;
            foreach (object[] p in rd.Pasos)
            {
                switch ((string)p[0])
                {
                    case "Arco":
                        {
                            float x = (float)p[1], y = (float)p[2], w = (float)p[3], h = (float)p[4];
                            P.ArcTo(new SKRect(x, y, x + w, y + h), (float)p[5], (float)p[6], nueva || P.PointCount == 0); nueva = false;
                            break;
                        }
                    case "Linea":
                        if (nueva || P.PointCount == 0) { P.MoveTo((float)p[1], (float)p[2]); nueva = false; } else P.LineTo((float)p[1], (float)p[2]);
                        P.LineTo((float)p[3], (float)p[4]); break;
                    case "Bezier":
                        if (nueva || P.PointCount == 0) { P.MoveTo((float)p[1], (float)p[2]); nueva = false; } else P.LineTo((float)p[1], (float)p[2]);
                        P.CubicTo((float)p[3], (float)p[4], (float)p[5], (float)p[6], (float)p[7], (float)p[8]); break;
                    case "Rect": { RectangleF r = (RectangleF)p[1]; P.AddRect(Sk(r)); nueva = true; break; }
                    case "Elipse": { float x = (float)p[1], y = (float)p[2]; P.AddOval(new SKRect(x, y, x + (float)p[3], y + (float)p[4])); nueva = true; break; }
                    case "Poligono":
                        {
                            PointF[] ps = (PointF[])p[1];
                            if (ps != null && ps.Length >= 2) { P.MoveTo(ps[0].X, ps[0].Y); for (int i = 1; i < ps.Length; i++) P.LineTo(ps[i].X, ps[i].Y); P.Close(); nueva = true; }
                            break;
                        }
                    case "Porcion":
                        {
                            float x = (float)p[1], y = (float)p[2]; SKRect r = new SKRect(x, y, x + (float)p[3], y + (float)p[4]);
                            P.MoveTo(r.MidX, r.MidY); P.ArcTo(r, (float)p[5], (float)p[6], false); P.Close(); nueva = true; break;
                        }
                    case "Cierra": P.Close(); nueva = true; break;
                    case "Empieza": nueva = true; break;
                }
            }
            return P;
        }
        // EL PINCEL DE XP EN UN SKPaint: su color, o el shader de Degradado (la textura con el medio pixel del dibujo en que se usa)
        private static SKPaint Pincel(PincelD p, bool medio)
        {
            SKPaint r = new SKPaint();
            switch (p.Tipo)
            {
                case 0: r.Color = p.C1.Sk(); return r;
                case 4:
                    {
                        using (SKPath gp = new SKPath { FillType = SKPathFillType.EvenOdd })
                        {
                            if (p.FormaRuta == 0) gp.AddOval(Sk(p.Caja));
                            else if (p.FormaRuta == 1) gp.AddRect(Sk(p.Caja));
                            else { gp.MoveTo(p.Caja.X, p.Caja.Y); gp.LineTo(p.Caja.Right, p.Caja.Y + p.Caja.Height / 3); gp.LineTo(p.Caja.X + p.Caja.Width / 2, p.Caja.Bottom); gp.Close(); }
                            SKRect b = gp.Bounds; RectangleF caja = RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom);
                            PointF centro = p.Corrido ? p.Centro : new PointF(caja.X + caja.Width / 2f, caja.Y + caja.Height / 2f);
                            r.Shader = Degradado.DeTrazo(caja, centro, p.C1, p.C2, p.BlendF, p.BlendP);
                        }
                        return r;
                    }
                case 5:
                    using (SKBitmap img = Mapa(p.Img))
                    {
                        SKMatrix m = SKMatrix.Identity; if (p.TX != 0 || p.TY != 0) m = m.PreConcat(SKMatrix.CreateTranslation(p.TX, p.TY));
                        r.Shader = Degradado.Textura(img, (Envoltura)p.Wrap, m, medio);
                    }
                    return r;
            }
            // lineales: sin extras, las de Degradado; con Blend, envoltura o colores de los dos puntos, la cuenta de LinearGradientBrush
            PointF p0, p1;
            if (p.Tipo == 1) { Degradado.Extremos(p.P0, p.P1); p0 = p.P0; p1 = p.P1; }
            else
            {
                float ang = p.Tipo == 2 ? (p.Modo == 1 ? Degradado.Vertical : p.Modo == 2 ? Degradado.Diagonal : p.Modo == 3 ? Degradado.DiagonalInversa : Degradado.Horizontal) : p.Angulo;
                bool esc = p.Tipo == 2 || p.Escalable;
                if (p.IcC == null && p.BlendF == null && p.Wrap < 0) { r.Shader = Degradado.Lineal(p.Caja, p.C1, p.C2, ang, esc); return r; }
                if (p.IcC != null && p.Wrap < 0) { r.Shader = Degradado.Lineal(p.Caja, p.IcC, p.IcP, ang, esc); return r; }
                Degradado.Extremos(p.Caja, ang, esc, out p0, out p1);
            }
            if (p.Tipo == 1 && p.IcC == null && p.BlendF == null && p.Wrap < 0) { r.Shader = Degradado.Lineal(p0, p1, p.C1, p.C2); return r; }
            SKColor[] cols; float[] pos;
            if (p.IcC != null) { cols = new SKColor[p.IcC.Length]; for (int i = 0; i < cols.Length; i++) cols[i] = p.IcC[i].Sk(); pos = (float[])p.IcP.Clone(); }
            else if (p.BlendF != null) { cols = new SKColor[p.BlendF.Length]; pos = (float[])p.BlendP.Clone(); for (int i = 0; i < cols.Length; i++) cols[i] = Degradado.Mezcla(p.C1, p.C2, p.BlendF[i]).Sk(); }
            else { cols = new[] { p.C1.Sk(), p.C2.Sk() }; pos = null; }
            int w = p.Wrap < 0 ? 0 : p.Wrap;
            r.Shader = Degradado.LinealCon(p0, p1, cols, pos, w == 4 ? SKShaderTileMode.Clamp : w == 0 ? SKShaderTileMode.Repeat : SKShaderTileMode.Mirror);
            return r;
        }
        // LA PLUMA: color (o pincel), ancho tal cual (Pixel le pone el minimo de un pixel y el limite de esquinas), esquinas, puntas y guiones
        private static SKPaint Pluma(PlumaD d, bool medio)
        {
            SKPaint p = d.Pincel != null ? Pincel(d.Pincel, medio) : new SKPaint { Color = d.C.Sk() };
            p.StrokeWidth = d.W;
            p.StrokeJoin = d.Join == 2 ? SKStrokeJoin.Round : d.Join == 1 ? SKStrokeJoin.Bevel : SKStrokeJoin.Miter;
            p.StrokeCap = d.CapIni == 2 || d.CapFin == 2 ? SKStrokeCap.Round : d.CapIni == 1 || d.CapFin == 1 ? SKStrokeCap.Square : SKStrokeCap.Butt;
            float w = Math.Max(1f, d.W);
            switch (d.Dash)
            {
                case 1: p.PathEffect = SKPathEffect.CreateDash(new[] { 3 * w, w }, 0); break;
                case 2: p.PathEffect = SKPathEffect.CreateDash(new[] { w, w }, 0); break;
                case 3: p.PathEffect = SKPathEffect.CreateDash(new[] { 3 * w, w, w, w }, 0); break;
                case 5: if (d.Patron != null && d.Patron.Length >= 2) { float[] q = new float[d.Patron.Length]; for (int i = 0; i < q.Length; i++) q[i] = d.Patron[i] * w; p.PathEffect = SKPathEffect.CreateDash(q, 0); } break;
            }
            return p;
        }
        private static void Suelta(SKPaint p) { SKShader s = p.Shader; p.Shader = null; if (s != null) s.Dispose(); SKPathEffect e = p.PathEffect; p.PathEffect = null; if (e != null) e.Dispose(); p.Dispose(); }
        // LA LETRA: la familia como la resuelve GDI+ (new Font("nombre") pone Microsoft Sans Serif si no esta; las genericas), el tamano y la
        // unidad como los da XP (las unidades que XP no usa, pasadas a pixeles)
        private static Letra Letra(LetraD l, out string familia, out float tam, out int unidad, out int estilo)
        {
            familia = l.Generica >= 0 ? (l.Generica == 0 ? Letras.ConRemates : l.Generica == 1 ? Letras.SinRemates : Letras.AnchoFijo) : Letras.Resuelve(l.Familia);
            tam = l.Tam; unidad = 3; estilo = 0;
            switch (l.Ctor) { case 0: unidad = l.Unidad; estilo = l.Estilo; break; case 1: estilo = l.Estilo; break; case 2: break; default: if (l.Generica >= 0) unidad = l.Unidad; else { unidad = l.Unidad; estilo = l.Estilo; } break; }
            bool b = (estilo & 1) != 0, i = (estilo & 2) != 0, u = (estilo & 4) != 0;
            switch (unidad)
            {
                case 2: case 0: case 1: return Rocola.Nativo.Letra.EnPixeles(familia, tam, b, i, u);
                case 4: return Rocola.Nativo.Letra.EnPixeles(familia, tam * 96f, b, i, u);
                case 5: return Rocola.Nativo.Letra.EnPixeles(familia, tam * 96f / 300f, b, i, u);
                case 6: return Rocola.Nativo.Letra.EnPixeles(familia, tam * 96f / 25.4f, b, i, u);
                default: return Rocola.Nativo.Letra.EnPuntos(familia, tam, b, i, u);
            }
        }
        private static Letra Letra(LetraD l) { string f; float t; int u, e; return Letra(l, out f, out t, out u, out e); }
        internal static void GenericosDeFabrica()
        {
            FormatoTexto t = FormatoTexto.TipograficoCompartido;
            t.Horizontal = Alinea.Cerca; t.Vertical = Alinea.Cerca; t.Corte = CorteTexto.Nada; t.Banderas = BanderasTexto.CajaNegra | BanderasTexto.SoloEnteros | BanderasTexto.SinRecortar;
            FormatoTexto d = FormatoTexto.NormalCompartido;
            d.Horizontal = Alinea.Cerca; d.Vertical = Alinea.Cerca; d.Corte = CorteTexto.PorLetra; d.Banderas = 0;
        }
        private static FormatoTexto Formato(FormatoD f)
        {
            FormatoTexto sf;
            switch (f.Tipo)
            {
                case 0: return null;
                case 1: sf = new FormatoTexto(); break;
                case 2: sf = FormatoTexto.TipograficoCompartido; break;
                case 3: sf = FormatoTexto.NormalCompartido; break;
                case 4: sf = new FormatoTexto(FormatoTexto.TipograficoCompartido); break;
                default: sf = new FormatoTexto((BanderasTexto)f.Flags); break;
            }
            if (f.CambiaAl)
            {
                sf.Horizontal = (Alinea)f.Al; sf.Vertical = (Alinea)f.LAl; sf.Corte = (CorteTexto)f.Trim;
                if (f.FlagsConO) sf.Banderas |= (BanderasTexto)f.Flags; else sf.Banderas = (BanderasTexto)f.Flags;
            }
            return sf;
        }

        internal static byte[] Hace(Caso c, StringBuilder v)
        {
            GenericosDeFabrica();
            using (SKBitmap b = Mapas.Nuevo(c.W, c.H))
            {
                using (SKCanvas cv = new SKCanvas(b))
                {
                    Lienzo l = new Lienzo { C = cv, Suavizado = 3, Ajuste = AjusteLetra.DelSistema, Filtro = Filtro.Bilineal, BaseCuenta = cv.SaveCount };
                    if (c.Fondo.HasValue) cv.Clear(c.Fondo.Value.Sk());
                    PonEstado(l, c.Estado);
                    foreach (object[] op in c.Ops)
                    {
                        try { Op(l, op, v); }
                        catch (Exception ex) { v.Append(" EXC:" + ex.GetType().Name); }
                    }
                    cv.Flush();
                }
                return Bytes(b);
            }
        }

        private static Rectangle E(RectangleF r) { return new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height); }
        private static PointF[] Ent(PointF[] p) { PointF[] q = new PointF[p.Length]; for (int i = 0; i < p.Length; i++) q[i] = new PointF((int)p[i].X, (int)p[i].Y); return q; }

        private static void Op(Lienzo l, object[] op, StringBuilder v)
        {
            SKCanvas g = l.C;
            switch ((string)op[0])
            {
                case "DrawString":
                    {
                        int k = (int)op[1]; string s = (string)op[2]; RectangleF r = (RectangleF)op[5];
                        using (Letra f = Letra((LetraD)op[3]))
                        {
                            SKPaint br = Pincel((PincelD)op[4], l.Medio);
                            try
                            {
                                FormatoTexto sf = Formato((FormatoD)op[6]);
                                RectangleF caja; FormatoTexto fmt;
                                switch (k)
                                {
                                    case 0: case 1: caja = new RectangleF(r.X, r.Y, 0, 0); fmt = null; break;
                                    case 2: case 3: caja = new RectangleF(r.X, r.Y, 0, 0); fmt = sf; break;
                                    case 4: caja = r; fmt = null; break;
                                    case 5: caja = r; fmt = sf; break;
                                    default: caja = E(r); fmt = sf; break;
                                }
                                TextoGdiPlus.Escribe(g, s, f, l.Ajuste, br, caja, fmt, l.Copia, l.Medio);
                            }
                            finally { Suelta(br); }
                        }
                        break;
                    }
                case "Compartido":
                    {
                        FormatoD fd = (FormatoD)op[5]; FormatoTexto t = FormatoTexto.TipograficoCompartido;
                        t.Horizontal = (Alinea)fd.Al; t.Vertical = (Alinea)fd.LAl;
                        if (fd.FlagsConO) t.Banderas |= (BanderasTexto)fd.Flags; else t.Banderas = (BanderasTexto)fd.Flags;
                        using (Letra f = Letra((LetraD)op[2]))
                        {
                            SKPaint br = Pincel((PincelD)op[3], l.Medio);
                            try
                            {
                                RectangleF r = (RectangleF)op[4];
                                TextoGdiPlus.Escribe(g, (string)op[1], f, l.Ajuste, br, r, FormatoTexto.TipograficoCompartido, l.Copia, l.Medio);
                                TextoGdiPlus.Escribe(g, (string)op[1], f, l.Ajuste, br, new RectangleF(r.X, r.Y, 0, 0), FormatoTexto.TipograficoCompartido, l.Copia, l.Medio);
                                SizeF m = TextoGdiPlus.Mide((string)op[1], f, l.Ajuste, SizeF.Empty, FormatoTexto.TipograficoCompartido);
                                FormatoTexto copia = new FormatoTexto(FormatoTexto.TipograficoCompartido);
                                SizeF m2 = TextoGdiPlus.Mide((string)op[1], f, l.Ajuste, r.Size, copia);
                                v.Append(" compartido=" + R(m.Width) + "x" + R(m.Height) + "/" + R(m2.Width) + "x" + R(m2.Height));
                            }
                            finally { Suelta(br); }
                        }
                        break;
                    }
                case "MeasureString":
                    {
                        int k = (int)op[1]; string s = (string)op[2];
                        using (Letra f = Letra((LetraD)op[3]))
                        {
                            FormatoTexto sf = Formato((FormatoD)op[4]);
                            SizeF caja = new SizeF((float)op[5], (float)op[6]); int ancho = (int)op[7];
                            SizeF m; int letras = -1, lineas = -1;
                            switch (k)
                            {
                                case 0: m = TextoGdiPlus.Mide(s, f, l.Ajuste, SizeF.Empty, null); break;
                                case 1: m = TextoGdiPlus.Mide(s, f, l.Ajuste, new SizeF(ancho, 999999f), null); break;
                                case 2: m = TextoGdiPlus.Mide(s, f, l.Ajuste, new SizeF(ancho, 999999f), sf); break;
                                case 3: m = TextoGdiPlus.Mide(s, f, l.Ajuste, caja, null); break;
                                case 4: m = TextoGdiPlus.Mide(s, f, l.Ajuste, SizeF.Empty, sf); break;
                                case 5: m = TextoGdiPlus.Mide(s, f, l.Ajuste, caja, sf); break;
                                default: m = TextoGdiPlus.Mide(s, f, l.Ajuste, caja, sf, out letras, out lineas); break;
                            }
                            v.Append(" mide=" + R(m.Width) + "x" + R(m.Height) + "/" + letras + "/" + lineas);
                        }
                        break;
                    }
                case "DrawText":
                    {
                        int k = (int)op[1]; string s = (string)op[2]; Rectangle r = (Rectangle)op[4]; Point p = (Point)op[5]; Color col = (Color)op[6];
                        BanderasGdi fl = (BanderasGdi)(int)op[7];
                        bool alias = l.Ajuste == AjusteLetra.SinSuavizar || l.Ajuste == AjusteLetra.SinSuavizarAjustada;
                        using (Letra f = Letra((LetraD)op[3]))
                            switch (k)
                            {
                                case 0: TextoGdi.Escribe(g, s, f, r, col, BanderasGdi.Centrado | BanderasGdi.EnMedio, alias); break;
                                case 1: TextoGdi.Escribe(g, s, f, p, col, BanderasGdi.Izquierda, alias); break;
                                case 2: TextoGdi.Escribe(g, s, f, p, col, fl, alias); break;
                                default: TextoGdi.Escribe(g, s, f, r, col, fl, alias); break;
                            }
                        break;
                    }
                case "MeasureText":
                    {
                        int k = (int)op[1]; string s = (string)op[2]; Size caja = (Size)op[4]; BanderasGdi fl = (BanderasGdi)(int)op[5];
                        using (Letra f = Letra((LetraD)op[3]))
                        {
                            Size m;
                            switch (k)
                            {
                                case 0: case 3: m = TextoGdi.Mide(s, f); break;
                                case 1: case 4: m = TextoGdi.Mide(s, f, caja); break;
                                default: m = TextoGdi.Mide(s, f, caja, fl); break;
                            }
                            v.Append(" midegdi=" + m.Width + "x" + m.Height);
                        }
                        break;
                    }
                case "DrawImage": Imagen(l, op, v); break;
                case "LockBits": Puntos(op, v); break;
                case "FromStream":
                    {
                        int tipo = (int)op[1]; ImagenD im = (ImagenD)op[2];
                        byte[] datos = tipo == 5 ? new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 } : Casos.Archivo(im, tipo, (int)op[3]);
                        FormatoImagen fa;
                        using (SKBitmap i = Imagenes.Lee(datos, out fa))
                        {
                            string pf = fa == FormatoImagen.Paleta8 ? "Format8bppIndexed" : fa == FormatoImagen.Rgb24 ? "Format24bppRgb" : "Format32bppArgb";
                            v.Append(" lee=" + i.Width + "x" + i.Height + "/" + pf + "/" + (fa == FormatoImagen.Argb32 ? 2 : 0) + "/" + (fa == FormatoImagen.Argb32) + "/" + (fa == FormatoImagen.Paleta8));
                            v.Append(" puntos=" + Programa.Huella(Bytes(i)));
                        }
                        break;
                    }
                case "Save":
                    {
                        int tipo = (int)op[1]; string ruta = Path.Combine(Path.GetTempPath(), "banco-dibujo-" + Environment.ProcessId + ".img");
                        using (SKBitmap b = Mapa((ImagenD)op[2]))
                        {
                            byte[] datos;
                            if (tipo == 2) using (MemoryStream ms = new MemoryStream()) { Imagenes.GuardaPng(b, ms); datos = ms.ToArray(); }
                            else
                            {
                                if (tipo == 0) Imagenes.GuardaPng(b, ruta);
                                else if (tipo == 1) Imagenes.GuardaJpeg(b, ruta, (int)Math.Max(0, Math.Min(100, (long)(int)op[3])));
                                else using (FileStream fs = File.Create(ruta)) Imagenes.Guarda(b, fs, false, 75);   // (PNG por el codificador: la calidad de fabrica)
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
                            case 0: case 1: cb = RecorteLienzo.Limites(g); break;
                            case 2: l.Recorta(Sk(caja), 0); cb = RecorteLienzo.Limites(g); break;
                            case 3: using (SKPath gp = Ruta((RutaD)op[2])) l.Recorta(gp, 1); cb = RecorteLienzo.Limites(g); break;
                            case 4: l.Recorta(Sk(caja), 1); cb = RecorteLienzo.Limites(g); break;
                            default: l.Quita(); cb = RecorteLienzo.Limites(g); break;
                        }
                        v.Append(" recorte=" + R(cb));
                        using (SKPaint p = new SKPaint { Color = Color.FromArgb(160, 200, 40, 90).Sk() }) Pixel.Rellena(g, new RectangleF(-1000, -1000, 3000, 3000), p, l.Suave, l.Medio, l.Copia);
                        break;
                    }
                case "Letra":
                    {
                        LetraD ld = (LetraD)op[1]; string familia; float tam; int unidad, estilo;
                        using (Letra f = Letra(ld, out familia, out tam, out unidad, out estilo))
                        {
                            string[] unidades = { "World", "Display", "Pixel", "Point", "Inch", "Document", "Millimeter" };
                            v.Append(" letra=" + familia + "/" + R(tam) + "/" + unidades[unidad] + "/" + Estilo(estilo) + "/" + ((estilo & 1) != 0) + ((estilo & 2) != 0) + ((estilo & 4) != 0) + "/" + R(f.Puntos) + "/" + R(f.AltoRenglon) + "/" + R(f.AltoRenglon) + "/" + f.AltoEntero + "/" + familia);
                        }
                        try { v.Append(" familia=" + Letras.Familia((string)op[2])); }
                        catch (Exception ex) { v.Append(" familia:" + ex.GetType().Name); }
                        break;
                    }
                case "AddString":
                    {
                        LetraD ld = (LetraD)op[2]; string familia; float tam; int unidad, estilo;
                        using (Letra f0 = Letra(ld, out familia, out tam, out unidad, out estilo))
                        using (Letra f = Rocola.Nativo.Letra.EnPixeles(familia, tam, (estilo & 1) != 0, (estilo & 2) != 0, (estilo & 4) != 0))
                        using (SKPath gp = new SKPath { FillType = SKPathFillType.EvenOdd })
                        {
                            // (como en XP: el pincel y la pluma se hacen antes que el trazo; si uno lanza, el formato no se toca)
                            SKPaint br = Pincel((PincelD)op[5], l.Medio), pen = null;
                            try
                            {
                                pen = Pluma((PlumaD)op[6], l.Medio);
                                TextoGdiPlus.Trazo(gp, (string)op[1], f, (RectangleF)op[3], Formato((FormatoD)op[4]));
                                int k = (int)op[7];
                                if (k != 1) Pixel.Rellena(g, gp, br, l.Suave, l.Medio, l.Copia);
                                if (k != 0) Pixel.Traza(g, gp, pen, l.Suave, l.Medio, l.Copia);
                            }
                            finally { Suelta(br); if (pen != null) Suelta(pen); }
                            SKRect b = gp.Bounds;
                            v.Append(" puntosruta=" + gp.PointCount + " caja=" + R(RectangleF.FromLTRB(b.Left, b.Top, b.Right, b.Bottom)));
                        }
                        break;
                    }
                case "Estado":
                    {
                        int k = (int)op[4]; RectangleF caja = (RectangleF)op[2];
                        // GraphicsState st = g.Save(): el lienzo y el estado a la vista
                        int st = g.Save(); bool s0 = l.Suave, c0 = l.Copia, m0 = l.Medio; AjusteLetra a0 = l.Ajuste; Filtro f0 = l.Filtro; int su0 = l.Suavizado, me0 = l.Medios;
                        PonEstado(l, (EstadoD)op[3]);
                        SKPaint br = Pincel((PincelD)op[1], l.Medio);
                        try
                        {
                            Pixel.Rellena(g, caja, br, l.Suave, l.Medio, l.Copia);
                            using (Letra f = Letra((LetraD)op[6])) TextoGdiPlus.Escribe(g, (string)op[5], f, l.Ajuste, br, caja, Formato((FormatoD)op[7]), l.Copia, l.Medio);
                            if (k == 1) { SKMatrix m = g.TotalMatrix; g.ResetMatrix(); Pixel.Rellena(g, caja, br, l.Suave, l.Medio, l.Copia); g.SetMatrix(in m); }
                            if (k == 3) { SKMatrix m = g.TotalMatrix; SKPoint q0 = m.MapPoint(caja.X, caja.Y), q1 = m.MapPoint(caja.Right, caja.Bottom); v.Append(" puntos=" + R(q0.X) + "," + R(q0.Y) + "," + R(q1.X) + "," + R(q1.Y)); }
                        }
                        finally { Suelta(br); }
                        // g.Restore(st)
                        g.RestoreToCount(st); l.Suave = s0; l.Copia = c0; l.Medio = m0; l.Ajuste = a0; l.Filtro = f0; l.Suavizado = su0; l.Medios = me0; l.ConRecorte = false; l.BaseCuenta = g.SaveCount;
                        if (k == 2) g.ResetMatrix();
                        // (el pincel se vuelve a hacer con el medio pixel de ahora: la textura de XP lo toma al pintar)
                        SKPaint br2 = Pincel((PincelD)op[1], l.Medio);
                        try { Pixel.RellenaOvalo(g, caja, br2, l.Suave, l.Medio, l.Copia); } finally { Suelta(br2); }
                        v.Append(" clip=" + R(RecorteLienzo.Limites(g)));
                        break;
                    }
                default: Forma(l, op, v); break;
            }
        }
        private static string Estilo(int e)
        {
            if (e == 0) return "Regular";
            List<string> l = new List<string>();
            if ((e & 1) != 0) l.Add("Bold"); if ((e & 2) != 0) l.Add("Italic"); if ((e & 4) != 0) l.Add("Underline"); if ((e & 8) != 0) l.Add("Strikeout");
            return string.Join(", ", l);
        }

        private static void Forma(Lienzo l, object[] op, StringBuilder v)
        {
            SKCanvas g = l.C; string n = (string)op[0]; int k = (int)op[1];
            RectangleF r = (RectangleF)op[4]; PointF[] picos = (PointF[])op[6]; float a1 = (float)op[8], a2 = (float)op[9];
            bool s = l.Suave, m = l.Medio, cp = l.Copia;
            SKPaint br = null, pen = null;
            try
            {
                br = Pincel((PincelD)op[2], m); pen = Pluma((PlumaD)op[3], m);
                switch (n)
                {
                    case "FillRect": Pixel.Rellena(g, k == 1 || k == 3 ? (RectangleF)E(r) : r, br, s, m, cp); break;
                    case "DrawRect": Pixel.TrazaRect(g, k == 1 || k == 3 ? (RectangleF)E(r) : r, pen, s, m, cp); break;
                    case "FillEllipse": Pixel.RellenaOvalo(g, k == 1 || k == 3 ? (RectangleF)E(r) : r, br, s, m, cp); break;
                    case "DrawEllipse": Pixel.TrazaOvalo(g, k == 1 || k == 3 ? (RectangleF)E(r) : r, pen, s, m, cp); break;
                    case "FillPath": using (SKPath gp = Ruta((RutaD)op[5])) Pixel.Rellena(g, gp, br, s, m, cp); break;
                    case "DrawPath": using (SKPath gp = Ruta((RutaD)op[5])) Pixel.Traza(g, gp, pen, s, m, cp); break;
                    case "FillPolygon":
                        if (k == 0) Pixel.RellenaPoligono(g, picos, br, s, true, m, cp);
                        else if (k == 2) Pixel.RellenaPoligono(g, Ent(picos), br, s, true, m, cp);
                        else Pixel.RellenaPoligono(g, picos, br, s, (int)op[7] == 0, m, cp);
                        break;
                    case "DrawPolygon": Pixel.TrazaPoligono(g, k < 2 ? picos : Ent(picos), pen, s, m, cp); break;
                    case "DrawLine":
                        if (k == 0) Pixel.Linea(g, r.X, r.Y, r.Right, r.Bottom, pen, s, m, cp);
                        else if (k == 1) Pixel.Linea(g, (int)r.X, (int)r.Y, (int)r.Right, (int)r.Bottom, pen, s, m, cp);
                        else if (k == 2) Pixel.Linea(g, picos[0].X, picos[0].Y, picos[1].X, picos[1].Y, pen, s, m, cp);
                        else Pixel.Linea(g, (int)picos[0].X, (int)picos[0].Y, (int)picos[1].X, (int)picos[1].Y, pen, s, m, cp);
                        break;
                    case "DrawLines": Pixel.Lineas(g, k < 2 ? picos : Ent(picos), pen, s, m, cp); break;
                    case "DrawArc": Pixel.Arco(g, k == 1 ? (RectangleF)E(r) : r, a1, a2, pen, s, m, cp); break;
                    case "FillPie": Pixel.RellenaPorcion(g, r, a1, a2, br, s, m, cp); break;
                    case "DrawBezier": Pixel.Bezier(g, picos[0], picos[1], picos[2 % picos.Length], picos[3 % picos.Length], pen, s, m, cp); break;
                    case "FillRegion":
                        using (SKPath gp = Ruta((RutaD)op[5])) using (SKPath gp2 = Ruta((RutaD)op[10]))
                        {
                            SKPath rg = new SKPath(gp), res;
                            if (k == 3) { using (SKPath o = new SKPath()) { o.AddRect(Sk(r)); res = rg.Op(o, SKPathOp.Difference); } }
                            else res = rg.Op(gp2, k == 0 ? SKPathOp.Difference : k == 1 ? SKPathOp.Intersect : SKPathOp.Union);
                            if (res != null) { rg.Dispose(); rg = res; }
                            Pixel.RellenaRegion(g, rg, br, m, cp);
                            rg.Dispose();
                        }
                        break;
                    case "Clear": g.Clear(((Color)op[11]).Sk()); break;
                }
            }
            finally { if (br != null) Suelta(br); if (pen != null) Suelta(pen); }
        }

        private static void Imagen(Lienzo l, object[] op, StringBuilder v)
        {
            SKCanvas g = l.C;
            int k = (int)op[1]; ImagenD im = (ImagenD)op[2]; RectangleF d = (RectangleF)op[3], o = (RectangleF)op[4]; Point p = (Point)op[5];
            float[][] m = (float[][])op[6]; int env = (int)op[7]; bool clave = (bool)op[8]; Color baja = (Color)op[9], alta = (Color)op[10]; PointF[] para = (PointF[])op[11];
            using (SKBitmap b = Mapa(im))
            {
                bool conIa = m != null || env >= 0 || clave;
                Envoltura e = env >= 0 ? (Envoltura)env : Envoltura.Transparente;
                Color? cb = clave ? baja : (Color?)null, ca = clave ? alta : (Color?)null;
                SKRect todo = new SKRect(0, 0, b.Width, b.Height);
                switch (k)
                {
                    case 0: case 4: Imagenes.Pinta(g, b, todo, new SKRect(p.X, p.Y, p.X + b.Width, p.Y + b.Height), l.Filtro, l.Medio, l.Copia); break;
                    case 1: case 3: Imagenes.Pinta(g, b, todo, new SKRect(d.X, d.Y, d.X + b.Width, d.Y + b.Height), l.Filtro, l.Medio, l.Copia); break;
                    case 2: case 5: Imagenes.Pinta(g, b, todo, new SKRect(p.X, p.Y, p.X + b.Width, p.Y + b.Height), l.Filtro, l.Medio, l.Copia); break;
                    case 6: case 9: Imagenes.Pinta(g, b, todo, Sk(E(d)), l.Filtro, l.Medio, l.Copia); break;
                    case 7: case 8: Imagenes.Pinta(g, b, todo, Sk(d), l.Filtro, l.Medio, l.Copia); break;
                    case 10: Imagenes.Pinta(g, b, Sk(E(o)), Sk(E(d)), l.Filtro, l.Medio, l.Copia); break;
                    case 11: Imagenes.Pinta(g, b, Sk(o), Sk(d), l.Filtro, l.Medio, l.Copia); break;
                    case 12:
                        {
                            SKRect or = new SKRect((int)o.X, (int)o.Y, (int)o.X + (int)o.Width, (int)o.Y + (int)o.Height);
                            if (conIa) Imagenes.Pinta(g, b, or, Sk(E(d)), l.Filtro, l.Medio, l.Copia, e, m, cb, ca);
                            else Imagenes.Pinta(g, b, or, Sk(E(d)), l.Filtro, l.Medio, l.Copia);
                            break;
                        }
                    case 13: Imagenes.Pinta(g, b, new SKRect(o.X, o.Y, o.X + o.Width, o.Y + o.Height), Sk(E(d)), l.Filtro, l.Medio, l.Copia, e, m, cb, ca); break;
                    default:
                        {
                            // un paralelogramo: arriba izquierda, arriba derecha, abajo izquierda (como GDI+)
                            SKMatrix mm = new SKMatrix((para[1].X - para[0].X) / o.Width, (para[2].X - para[0].X) / o.Height, para[0].X,
                                                       (para[1].Y - para[0].Y) / o.Width, (para[2].Y - para[0].Y) / o.Height, para[0].Y, 0, 0, 1);
                            int n = g.Save(); g.Concat(in mm);
                            Imagenes.Pinta(g, b, Sk(o), new SKRect(0, 0, o.Width, o.Height), l.Filtro, l.Medio, l.Copia, e, m, cb, ca);
                            g.RestoreToCount(n);
                            break;
                        }
                }
            }
        }

        private static void Puntos(object[] op, StringBuilder v)
        {
            ImagenD im = (ImagenD)op[1]; Rectangle zona = (Rectangle)op[2]; int fmt = (int)op[3], modo = (int)op[4]; uint[] datos = (uint[])op[5];
            int gx = (int)op[6], gy = (int)op[7]; Color sc = (Color)op[8];
            using (SKBitmap b = Mapa(im))
            {
                int bpp = fmt == 2 ? 3 : 4;
                byte* scan; int paso; IntPtr memoria = IntPtr.Zero;
                FormatoPuntos fp = fmt == 2 ? FormatoPuntos.Rgb24 : fmt == 3 ? FormatoPuntos.Rgb32 : FormatoPuntos.Argb32;
                if (fmt == 0) { paso = b.RowBytes; scan = (byte*)b.GetPixels() + (long)zona.Y * paso + (long)zona.X * 4; }   // PArgb: los puntos mismos
                else
                {
                    paso = Pixeles.Paso(fp, zona.Width);
                    memoria = (IntPtr)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)Math.Max(1L, (long)paso * zona.Height));
                    scan = (byte*)memoria;
                    Pixeles.Lee(b, zona, fp, memoria, paso);
                }
                byte[] leido = new byte[zona.Width * zona.Height * bpp];
                for (int y = 0; y < zona.Height; y++) for (int x = 0; x < zona.Width * bpp; x++) leido[y * zona.Width * bpp + x] = scan[y * paso + x];
                v.Append(" paso=" + (paso >= zona.Width * bpp) + " leido=" + Programa.Huella(leido));
                if ((modo & 2) != 0)
                    for (int y = 0; y < zona.Height; y++)
                        for (int x = 0; x < zona.Width; x++)
                        {
                            uint d = datos[y * zona.Width + x]; byte* q = scan + y * paso + x * bpp;
                            if (fmt == 0) { uint a = d >> 24; d = (a << 24) | (Math.Min((d >> 16) & 255, a) << 16) | (Math.Min((d >> 8) & 255, a) << 8) | Math.Min(d & 255, a); }
                            for (int i = 0; i < bpp; i++) q[i] = (byte)(d >> (8 * i));
                        }
                if (fmt == 0) { if ((modo & 2) != 0) b.NotifyPixelsChanged(); }
                else { if ((modo & 2) != 0) Pixeles.Escribe(b, zona, fp, memoria, paso); System.Runtime.InteropServices.NativeMemory.Free((void*)memoria); }
                v.Append(" tras=" + Programa.Huella(Bytes(b)));
                Color c = ColorSk.DeSk(b.GetPixel(gx, gy)); v.Append(" get=" + c.ToArgb().ToString("X8"));
                b.SetPixel(gx, gy, sc.Sk()); v.Append(" set=" + Programa.Huella(Bytes(b)) + "/" + ColorSk.DeSk(b.GetPixel(gx, gy)).ToArgb().ToString("X8"));
                using (SKBitmap cl = Mapas.Copia(b, zona)) v.Append(" clon=" + NombresFormato[fmt] + "/" + Programa.Huella(Bytes(cl)));
                int ew = Math.Max(1, zona.Width * 2), eh = Math.Max(1, zona.Height / 2 + 1);
                using (SKBitmap esc = Mapas.Nuevo(ew, eh))
                {
                    using (SKCanvas cv = new SKCanvas(esc)) { cv.Clear(SKColors.Transparent); Imagenes.Pinta(cv, b, new SKRect(0, 0, b.Width, b.Height), new SKRect(0, 0, ew, eh), Filtro.Bilineal); }
                    v.Append(" escala=" + Programa.Huella(Bytes(esc)));
                }
            }
        }
    }
}

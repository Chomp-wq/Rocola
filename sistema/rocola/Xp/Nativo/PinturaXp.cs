// LO QUE PINTAN LAS HOJAS Y SUS PIEZAS COMO EN WINDOWS XP (port nativo de las ventanas de XP, ventanas2, paso H): el marco de las
// ventanas de XP (tema Luna), los colores que calcula WinForms para sus controles (ControlPaint), la barra de desplazamiento de XP, la
// «X» roja de .NET y lo que GDI+ hace con un pincel, una pluma o un trazo, todo con SkiaSharp y las funciones nativas del dibujo
// (Nativo/Pixel.cs, Degradado.cs, TextoGdi.cs). Se PORTO de la capa de la transicion (en la referencia, Plataforma/Ventanas.Escritorio.cs: PintaMarco;
// Ventanas.Control.cs: ColoresXp, BarraXp, PintaExcepcion), llamando a las mismas funciones nativas con lo mismo que les pasaba la capa
// por su Graphics de fabrica (sin suavizar, PixelOffsetMode de fabrica, sin copia; suavizado donde la capa ponia SmoothingMode.AntiAlias),
// para que cada punto salga igual que en la referencia.
using System;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    internal static class PinturaXp
    {
        // ---------- lo que GDI+ hace con un pincel o una pluma de un color (SolidBrush y Pen de GDI+: Dibujo2D, Brush.Pon y Pen.Pon) ----------
        internal static SKPaint Pincel(Color c) { SKPaint p = new SKPaint(); p.Shader = null; p.Color = c.Sk(); return p; }
        internal static SKPaint Pluma(Color c, float ancho)
        {
            SKPaint p = new SKPaint(); p.Shader = null; p.Color = c.Sk();
            p.StrokeWidth = ancho; p.StrokeJoin = SKStrokeJoin.Miter; p.StrokeCap = SKStrokeCap.Butt; p.PathEffect = null;
            return p;
        }
        // LinearGradientBrush(caja, c1, c2, LinearGradientMode): el angulo escalable del modo, repetido fuera (WrapMode.Tile de fabrica)
        internal static SKPaint Degradado(Rectangle caja, Color c1, Color c2, bool vertical)
        {
            SKPaint p = new SKPaint();
            p.Shader = Rocola.Nativo.Degradado.Lineal((RectangleF)caja, c1, c2, vertical ? Rocola.Nativo.Degradado.Vertical : Rocola.Nativo.Degradado.Horizontal, true);
            p.Color = SKColors.Black;
            return p;
        }
        internal static void Suelta(SKPaint p) { SKShader s = p.Shader; p.Shader = null; if (s != null) s.Dispose(); p.Dispose(); }
        // FillRectangle(SolidBrush, rect) y DrawRectangle(Pen, x, y, w, h) sin suavizar
        internal static void Rellena(SKCanvas g, RectangleF r, Color c) { SKPaint p = Pincel(c); try { Pixel.Rellena(g, r, p, false); } finally { Suelta(p); } }
        internal static void Rellena(SKCanvas g, Rectangle r, Color c) { Rellena(g, (RectangleF)r, c); }
        internal static void TrazaRect(SKCanvas g, RectangleF r, Color c, float ancho, bool suave) { SKPaint p = Pluma(c, ancho); try { Pixel.TrazaRect(g, r, p, suave); } finally { Suelta(p); } }
        internal static void Linea(SKCanvas g, float x1, float y1, float x2, float y2, Color c, float ancho, bool suave) { SKPaint p = Pluma(c, ancho); try { Pixel.Linea(g, x1, y1, x2, y2, p, suave); } finally { Suelta(p); } }

        // ---------- un trazo como GraphicsPath de GDI+ (Dibujo2D, GraphicsPath): de fabrica Alternate (EvenOdd); un arco o una linea que
        // siguen a otro se unen a el con una linea; CloseFigure cierra la figura y la siguiente empieza otra ----------
        internal sealed class Trazo : IDisposable
        {
            internal readonly SKPath P = new SKPath();
            private bool nueva = true;
            internal Trazo() { P.FillType = SKPathFillType.EvenOdd; }
            private void Hasta(float x, float y) { if (nueva || P.PointCount == 0) { P.MoveTo(x, y); nueva = false; } else P.LineTo(x, y); }
            internal void Arco(float x, float y, float w, float h, float inicio, float barrido)
            {
                SKRect r = new SKRect(x, y, x + w, y + h);
                if (nueva || P.PointCount == 0) { P.ArcTo(r, inicio, barrido, true); nueva = false; }
                else P.ArcTo(r, inicio, barrido, false);
            }
            internal void Linea(float x1, float y1, float x2, float y2) { Hasta(x1, y1); P.LineTo(x2, y2); }
            internal void Cierra() { P.Close(); nueva = true; }
            public void Dispose() { P.Dispose(); }
        }
        // EL RECTANGULO DE ESQUINAS REDONDAS (ColoresXp.Redondo de la capa: cuatro arcos y cerrado)
        internal static Trazo Redondo(RectangleF r, float radio)
        {
            Trazo gp = new Trazo(); float d = radio * 2;
            gp.Arco(r.X, r.Y, d, d, 180, 90); gp.Arco(r.Right - d, r.Y, d, d, 270, 90);
            gp.Arco(r.Right - d, r.Bottom - d, d, d, 0, 90); gp.Arco(r.X, r.Bottom - d, d, d, 90, 90);
            gp.Cierra(); return gp;
        }

        // ---------- LA «X» ROJA DE .NET (Control.PaintException, Control.cs:8869-8889): el control que trono al pintar ----------
        internal static void ExcepcionAlPintar(SKCanvas g, Rectangle cr)
        {
            Rectangle r = cr;
            r.X++; r.Y++; r.Width--; r.Height--;
            TrazaRect(g, new RectangleF(r.X, r.Y, r.Width - 1, r.Height - 1), Color.Red, 2, false);
            r.Inflate(-1, -1);
            Rellena(g, r, Color.White);
            Linea(g, cr.Left, cr.Top, cr.Right, cr.Bottom, Color.Red, 2, false);
            Linea(g, cr.Left, cr.Bottom, cr.Right, cr.Top, Color.Red, 2, false);
        }

        // ---------- EL TEXTO DE GDI DE LOS CONTROLES (TextRenderer.DrawText, Nativo/TextoGdi.cs) con la matriz del HDC de la pieza ----------
        internal static void Texto(SKCanvas g, SKMatrix hdc, string s, Letra f, Rectangle r, Color c, BanderasGdi b) { TextoGdi.Escribe(g, hdc, s, f, r, c, b, false); }
        // (ControlPaint.CreateTextFormatFlags de la alineacion, la Label.DeAlineacion de la capa)
        internal static BanderasGdi DeAlineacion(AlineaTexto a)
        {
            BanderasGdi f = BanderasGdi.Izquierda;
            if ((a & (AlineaTexto.TopCenter | AlineaTexto.MiddleCenter | AlineaTexto.BottomCenter)) != 0) f |= BanderasGdi.Centrado;
            if ((a & (AlineaTexto.TopRight | AlineaTexto.MiddleRight | AlineaTexto.BottomRight)) != 0) f |= BanderasGdi.Derecha;
            if ((a & (AlineaTexto.MiddleLeft | AlineaTexto.MiddleCenter | AlineaTexto.MiddleRight)) != 0) f |= BanderasGdi.EnMedio;
            if ((a & (AlineaTexto.BottomLeft | AlineaTexto.BottomCenter | AlineaTexto.BottomRight)) != 0) f |= BanderasGdi.Abajo;
            return f;
        }
        // el «&» de un texto con UseMnemonic, como la capa lo quitaba antes de pintarlo (Label y Button): «&&» es un «&»
        internal static string SinPrefijo(string t) { return t.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&"); }

        // ---------- LA BARRA DE TITULO DE LAS HOJAS (la de Windows XP con el tema Luna): las esquinas de arriba redondas, el titulo con su
        // sombra y la «X» de cerrar (normal, con el raton encima o apretada); mas clara si la hoja no es la activa (PintaMarco de la capa) ----------
        internal static void Marco(SKCanvas g, int ancho, int alto0, int marco, int altoTitulo, bool activa, string titulo, Rectangle x, int estadoX)
        {
            int m = marco, alto = altoTitulo + m;
            Color c1 = activa ? Color.FromArgb(0, 88, 238) : Color.FromArgb(150, 178, 238), c2 = activa ? Color.FromArgb(0, 64, 208) : Color.FromArgb(122, 150, 223);
            Color borde = activa ? Color.FromArgb(0, 72, 224) : Color.FromArgb(126, 154, 228);
            using (Trazo gp = new Trazo())
            {
                const int r = 7;
                gp.Arco(0, 0, 2 * r, 2 * r, 180, 90);
                gp.Arco(ancho - 2 * r, 0, 2 * r, 2 * r, 270, 90);
                gp.Linea(ancho, r, ancho, alto);
                gp.Linea(ancho, alto, 0, alto);
                gp.Cierra();
                SKPaint b = Degradado(new Rectangle(0, 0, ancho, alto), c1, c2, true);
                try { Pixel.Rellena(g, gp.P, b, true); } finally { Suelta(b); }
            }
            Rellena(g, new RectangleF(0, alto, m, alto0 - alto), borde);
            Rellena(g, new RectangleF(ancho - m, alto, m, alto0 - alto), borde);
            Rellena(g, new RectangleF(0, alto0 - m, ancho, m), borde);
            int textoHasta = x.Left - 4;
            using (Letra letra = Letra.EnPuntos(Letras.Resuelve("Trebuchet MS"), 10f, true, false, false))
            {
                Rectangle rt = new Rectangle(8, m, Math.Max(0, textoHasta - 8), altoTitulo);
                BanderasGdi fl = BanderasGdi.EnMedio | BanderasGdi.Izquierda | BanderasGdi.PuntosAlFinal | BanderasGdi.UnRenglon | BanderasGdi.SinPrefijo;
                if (activa) { Rectangle rs = rt; rs.Offset(1, 1); Texto(g, SKMatrix.Identity, titulo, letra, rs, Color.FromArgb(10, 24, 131), fl); }
                Texto(g, SKMatrix.Identity, titulo, letra, rt, activa ? Color.White : Color.FromArgb(216, 228, 248), fl);
            }
            int e = estadoX;
            Color a1 = e == 2 ? Color.FromArgb(178, 52, 28) : e == 1 ? Color.FromArgb(240, 132, 104) : Color.FromArgb(226, 98, 70);
            Color a2 = e == 2 ? Color.FromArgb(206, 84, 56) : e == 1 ? Color.FromArgb(214, 80, 50) : Color.FromArgb(196, 58, 30);
            if (!activa) { a1 = Color.FromArgb(198, 150, 160); a2 = Color.FromArgb(178, 128, 140); }
            using (Trazo gp = new Trazo())
            {
                const int r = 3;
                gp.Arco(x.Left, x.Top, 2 * r, 2 * r, 180, 90);
                gp.Arco(x.Right - 1 - 2 * r, x.Top, 2 * r, 2 * r, 270, 90);
                gp.Arco(x.Right - 1 - 2 * r, x.Bottom - 1 - 2 * r, 2 * r, 2 * r, 0, 90);
                gp.Arco(x.Left, x.Bottom - 1 - 2 * r, 2 * r, 2 * r, 90, 90);
                gp.Cierra();
                SKPaint b = Degradado(x, a1, a2, true);
                try { Pixel.Rellena(g, gp.P, b, true); } finally { Suelta(b); }
                SKPaint p = Pluma(Color.FromArgb(activa ? 235 : 180, 255, 255, 255), 1f);
                try { Pixel.Traza(g, gp.P, p, true); } finally { Suelta(p); }
            }
            Linea(g, x.Left + 6.5f, x.Top + 6.5f, x.Right - 7.5f, x.Bottom - 7.5f, Color.White, 2f, true);
            Linea(g, x.Right - 7.5f, x.Top + 6.5f, x.Left + 6.5f, x.Bottom - 7.5f, Color.White, 2f, true);
        }

        // ---------- LA BARRA DE DESPLAZAMIENTO DE XP (tema Luna): el canal claro, las flechas y el pulgar azul claro redondeados, con su flecha
        // y sus rayas (BarraXp de la capa). 17 puntos: SM_CXVSCROLL y SM_CYHSCROLL de XP ----------
        internal const int Barra = 17;
        internal static void BarraDesplazamiento(SKCanvas g, Rectangle r, bool vertical, Rectangle pulgar)
        {
            Rellena(g, r, Color.FromArgb(243, 241, 236));
            int n = Barra;
            Rectangle a = vertical ? new Rectangle(r.X, r.Y, r.Width, n) : new Rectangle(r.X, r.Y, n, r.Height);
            Rectangle z = vertical ? new Rectangle(r.X, r.Bottom - n, r.Width, n) : new Rectangle(r.Right - n, r.Y, n, r.Height);
            PiezaDeBarra(g, a, vertical); PiezaDeBarra(g, z, vertical); PiezaDeBarra(g, pulgar, vertical);
            SKPaint f = Pincel(Color.FromArgb(77, 97, 133));
            try
            {
                float cx = a.X + a.Width / 2f, cy = a.Y + a.Height / 2f, dx = z.X + z.Width / 2f, dy = z.Y + z.Height / 2f;
                if (vertical)
                {
                    Pixel.RellenaPoligono(g, new PointF[] { new PointF(cx - 4, cy + 2), new PointF(cx + 4, cy + 2), new PointF(cx, cy - 2) }, f, true);
                    Pixel.RellenaPoligono(g, new PointF[] { new PointF(dx - 4, dy - 2), new PointF(dx + 4, dy - 2), new PointF(dx, dy + 2) }, f, true);
                }
                else
                {
                    Pixel.RellenaPoligono(g, new PointF[] { new PointF(cx + 2, cy - 4), new PointF(cx + 2, cy + 4), new PointF(cx - 2, cy) }, f, true);
                    Pixel.RellenaPoligono(g, new PointF[] { new PointF(dx - 2, dy - 4), new PointF(dx - 2, dy + 4), new PointF(dx + 2, dy) }, f, true);
                }
            }
            finally { Suelta(f); }
            float px = pulgar.X + pulgar.Width / 2f, py = pulgar.Y + pulgar.Height / 2f;
            for (int k = -3; k <= 3; k += 2)
                if (vertical) { if (pulgar.Height > 14) Linea(g, px - 3, py + k, px + 3, py + k, Color.FromArgb(140, 166, 222), 1f, false); }
                else if (pulgar.Width > 14) Linea(g, px + k, py - 3, px + k, py + 3, Color.FromArgb(140, 166, 222), 1f, false);
        }
        private static void PiezaDeBarra(SKCanvas g, Rectangle r, bool vertical)
        {
            if (r.Width <= 2 || r.Height <= 2) return;
            RectangleF rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
            using (Trazo gp = Redondo(rf, 2f))
            {
                SKPaint b = Degradado(r, Color.FromArgb(225, 234, 254), Color.FromArgb(193, 211, 251), !vertical);
                try { Pixel.Rellena(g, gp.P, b, true); } finally { Suelta(b); }
                SKPaint p = Pluma(Color.FromArgb(180, 200, 246), 1f);
                try { Pixel.Traza(g, gp.P, p, true); } finally { Suelta(p); }
            }
        }

        // ---------- LOS COLORES QUE CALCULA WINFORMS PARA PINTAR SUS CONTROLES (ControlPaint, con su HLS de 0 a 240; ColoresXp de la capa) ----------
        internal static Color Ajusta(Color c, float f) { return Color.FromArgb(Math.Min(255, (int)(f * c.R)), Math.Min(255, (int)(f * c.G)), Math.Min(255, (int)(f * c.B))); }
        internal static Color Dark(Color c) { Hls h = new Hls(c); int cero = h.Luma(-333); return h.Color(cero - (int)(cero * 0.5f)); }
        // TextRenderer.DisabledTextColor: ControlDark de XP, o el fondo oscurecido si el fondo es mas oscuro que el gris de las ventanas
        internal static Color ColorEnGris(Color fondo)
        {
            if (fondo.A > 0 && new Hls(fondo).L < new Hls(CajaPieza.ColorDeFondo).L) return Dark(fondo);
            return Color.FromArgb(172, 168, 153);
        }
        internal static Color LightLight(Color c) { Hls h = new Hls(c); int uno = h.Luma(500); return h.Color(h.L + (int)((uno - h.L) * 1.0f)); }
        private struct Hls
        {
            private const int Max = 240, RgbMax = 255, Indefinido = Max * 2 / 3;
            internal int H, L, S;
            internal Hls(Color c)
            {
                int r = c.R, g = c.G, b = c.B, max = Math.Max(Math.Max(r, g), b), min = Math.Min(Math.Min(r, g), b), suma = max + min, dif = max - min;
                L = (suma * Max + RgbMax) / (2 * RgbMax);
                if (dif == 0) { S = 0; H = Indefinido; return; }
                S = L <= Max / 2 ? (dif * Max + suma / 2) / suma : (dif * Max + (2 * RgbMax - suma) / 2) / (2 * RgbMax - suma);
                int dr = ((max - r) * (Max / 6) + dif / 2) / dif, dg = ((max - g) * (Max / 6) + dif / 2) / dif, db = ((max - b) * (Max / 6) + dif / 2) / dif;
                H = r == max ? db - dg : g == max ? Max / 3 + dr - db : 2 * Max / 3 + dg - dr;
                if (H < 0) H += Max;
                if (H > Max) H -= Max;
            }
            internal int Luma(int n) { return n > 0 ? (int)((L * (1000L - n) + (Max + 1L) * n) / 1000) : (int)((L * (n + 1000L)) / 1000); }
            internal Color Color(int lum)
            {
                if (S == 0) { int v = lum * RgbMax / Max; return System.Drawing.Color.FromArgb(v, v, v); }
                int m2 = lum <= Max / 2 ? (lum * (Max + S) + Max / 2) / Max : lum + S - (lum * S + Max / 2) / Max, m1 = 2 * lum - m2;
                return System.Drawing.Color.FromArgb(Byte(m1, m2, H + Max / 3), Byte(m1, m2, H), Byte(m1, m2, H - Max / 3));
            }
            private static int Byte(int n1, int n2, int h) { return (byte)((Tono(n1, n2, h) * RgbMax + Max / 2) / Max); }
            private static int Tono(int n1, int n2, int h)
            {
                if (h < 0) h += Max;
                if (h > Max) h -= Max;
                if (h < Max / 6) return n1 + ((n2 - n1) * h + Max / 12) / (Max / 6);
                if (h < Max / 2) return n2;
                if (h < Max * 2 / 3) return n1 + ((n2 - n1) * (Max * 2 / 3 - h) + Max / 12) / (Max / 6);
                return n1;
            }
        }
    }
}

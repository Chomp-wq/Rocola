// EL TEXTO DE GDI (TextRenderer de WinForms en XP), NATIVO (port nativo, el dibujo; 8-11-2026): la letra de GDI (LetraGdi), el DrawTextEx de
// user32 (DrawTextGdi) y el pintado en los pixeles del HDC. Los algoritmos son los de Plataforma/Dibujo2D.cs (revision del 8-11-2026),
// movidos aqui tal cual; Dibujo2D los llama mientras dure la traduccion.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using SkiaSharp;

namespace Rocola.Nativo
{
    // las banderas de TextRenderer (los valores de TextFormatFlags de WinForms, con nombre propio): Centrado = HorizontalCenter,
    // EnMedio = VerticalCenter, Abajo = Bottom, Partir = WordBreak, UnRenglon = SingleLine, Tabuladores = ExpandTabs, SinRecorte = NoClipping,
    // RenglonExterno = ExternalLeading, SinPrefijo = NoPrefix, CajaTexto = TextBoxControl, PuntosAlFinal = EndEllipsis,
    // PuntosPorPalabra = WordEllipsis, OcultaPrefijo = HidePrefix, SoloPrefijo = PrefixOnly, SinMargen = NoPadding,
    // MargenAncho = LeftAndRightPadding
    [Flags]
    public enum BanderasGdi
    {
        Izquierda = 0, Arriba = 0, Centrado = 1, Derecha = 2, EnMedio = 4, Abajo = 8, Partir = 16, UnRenglon = 32, Tabuladores = 64, SinRecorte = 256,
        RenglonExterno = 512, SinPrefijo = 2048, Interno = 4096, CajaTexto = 8192, PuntosRuta = 16384, PuntosAlFinal = 32768, ModificaCadena = 65536,
        DerechaAIzquierda = 131072, PuntosPorPalabra = 262144, SinCorteAncho = 524288, OcultaPrefijo = 1048576, SoloPrefijo = 2097152,
        ConservaRecorte = 16777216, ConservaTraslacion = 33554432, SinMargen = 268435456, MargenAncho = 536870912
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
    public static class TextoGdi
    {
        private static readonly Size SinFin = new Size(int.MaxValue, int.MaxValue);
        // TextRenderer.DrawText(g, s, letra, caja, color, banderas). GDI pinta con la matriz del HDC, no con la traslacion que el codigo le
        // puso al lienzo (XP cuenta con eso: Form1.Buscador.cs, DrawOneKey mueve la caja con kr.Offset): en un mapa de bits es la identidad;
        // en un lienzo que no empieza en el origen, la de su sitio ('hdc'). 'sinSuavizar': el modo de letra es SingleBitPerPixel(GridFit).
        // (Sin banderas, DrawText con caja es Centrado | EnMedio y con un punto Izquierda | Arriba.)
        public static void Escribe(SKCanvas c, string s, Letra f, Rectangle r, Color color, BanderasGdi banderas, bool sinSuavizar = false) { Escribe(c, SKMatrix.Identity, s, f, r, color, banderas, sinSuavizar); }
        public static void Escribe(SKCanvas c, string s, Letra f, Point p, Color color, BanderasGdi banderas, bool sinSuavizar = false) { Escribe(c, SKMatrix.Identity, s, f, new Rectangle(p, SinFin), color, banderas, sinSuavizar); }
        public static void Escribe(SKCanvas c, SKMatrix hdc, string s, Letra f, Rectangle r, Color color, BanderasGdi banderas, bool sinSuavizar)
        {
            if (c == null) throw new ArgumentNullException("dc");
            if (string.IsNullOrEmpty(s) || color == Color.Transparent || f == null) return;
            LetraGdi l = LetraGdi.De(f);
            int dt, iz, de; Separa(banderas, l, out dt, out iz, out de);
            r = AjustaVertical(l, s, r, dt, iz, de);
            if (r.Width == int.MaxValue) r.Width -= r.X;    // (TextExtensions.DrawText: la caja sin fin de un punto)
            if (r.Height == int.MaxValue) r.Height -= r.Y;
            DrawTextGdi.Rect rc = new DrawTextGdi.Rect(r.Left, r.Top, r.Right, r.Bottom);
            List<DrawTextGdi.Trozo> trozos = new List<DrawTextGdi.Trozo>();
            DrawTextGdi.Hace(l, s, ref rc, dt, iz, de, trozos);
            SKRectI? caja = (dt & DrawTextGdi.DT_NOCLIP) == 0 ? new SKRectI(r.Left, r.Top, r.Right, r.Bottom) : (SKRectI?)null;
            Pinta(c, hdc, l, trozos, caja, color, sinSuavizar, (dt & DrawTextGdi.DT_PREFIXONLY) == 0, (dt & DrawTextGdi.DT_HIDEPREFIX) == 0);
        }
        // TextRenderer.MeasureText(s, letra): sin banderas es Abajo; con una caja y banderas, TextExtensions.MeasureText
        public static Size Mide(string s, Letra f) { return Mide(s, f, SinFin, BanderasGdi.Abajo); }
        public static Size Mide(string s, Letra f, Size caja) { return Mide(s, f, caja, BanderasGdi.Abajo); }
        public static Size Mide(string s, Letra f, Size caja, BanderasGdi banderas)
        {
            if (string.IsNullOrEmpty(s) || f == null) return Size.Empty;
            LetraGdi l = LetraGdi.De(f);
            int dt, iz, de; Separa(banderas, l, out dt, out iz, out de);
            int minimo = 1 + iz + de;
            if (caja.Width <= minimo) caja.Width = minimo;
            if (caja.Height <= 0) caja.Height = 1;
            if (caja.Height == int.MaxValue && (dt & DrawTextGdi.DT_SINGLELINE) != 0) dt &= ~(DrawTextGdi.DT_BOTTOM | DrawTextGdi.DT_VCENTER);
            if (caja.Width == int.MaxValue) dt &= ~DrawTextGdi.DT_WORDBREAK;
            DrawTextGdi.Rect rc = new DrawTextGdi.Rect(0, 0, caja.Width, caja.Height);
            DrawTextGdi.Hace(l, s, ref rc, dt | DrawTextGdi.DT_CALCRECT, iz, de, null);
            return new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
        }
        // las banderas de GDI y los margenes (TextExtensions.SplitTextFormatFlags y GetTextMargins)
        private static void Separa(BanderasGdi fl, LetraGdi l, out int dt, out int iz, out int de)
        {
            dt = (int)((uint)fl & 0x00FFFFFFu);   // (GdiUnsupportedFlagMask: lo de WinForms no llega a GDI)
            iz = de = 0;
            float m = l.Alto / 6f;
            if ((fl & BanderasGdi.MargenAncho) != 0) { iz = (int)Math.Ceiling(2 * m); de = (int)Math.Ceiling(m * 2.5f); }
            else if ((fl & BanderasGdi.SinMargen) == 0) { iz = (int)Math.Ceiling(m); de = (int)Math.Ceiling(m * 1.5f); }
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

        // EL TEXTO DE GDI (TextRenderer): los trozos que da DrawTextGdi, pintados en los pixeles del HDC (la matriz de la base, no la que
        // el codigo le puso al Graphics), cada letra en su pixel entero con el ancho de GDI, opaco, sobre lo que haya; la negrita inventada
        // de GDI es la letra pintada otra vez un pixel a la derecha, y la letra del "&" lleva su raya de un pixel debajo.
        private static void Pinta(SKCanvas C, SKMatrix baseMatriz, LetraGdi l, List<DrawTextGdi.Trozo> trozos, SKRectI? caja, Color c, bool alias, bool letras, bool rayas)
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

        internal static LetraGdi De(Letra f)
        {
            int ppem = (int)Math.Ceiling(96f * f.Puntos / 72f);   // (ScaleHelper.InitialSystemDpi * font.SizeInPoints / 72)
            if (ppem < 1) ppem = 1;
            string clave = f.Familia + "|" + (f.Negrita ? "b" : "") + (f.Cursiva ? "i" : "") + "|" + ppem;
            lock (hechas)
            {
                LetraGdi l;
                if (hechas.TryGetValue(clave, out l)) return l;
                l = new LetraGdi(Letras.Busca(f.Familia, f.Negrita, f.Cursiva), ppem);
                hechas[clave] = l;
                return l;
            }
        }
        private LetraGdi(Cara cara, int ppem)
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
}

// EL TEXTO DE GDI+ (DrawString y MeasureString de XP), NATIVO (port nativo, el dibujo; 8-11-2026). Skia solo pinta una tira de letras en
// una linea base; como GDI+ acomoda un texto en su caja (margenes, cortes, renglones, recorte con puntos, alineado) lo hace esto, con las
// reglas del Dibujo2D.cs de la capa de la transicion (ya borrada; sigue en la referencia) (revisadas el 8-11-2026 con la documentacion de StringFormat y las pruebas de Wine que pasan en Windows,
// dlls/gdiplus/tests/graphics.c: test_measure_string y test_string_functions), movidas aqui. Sin estado de dibujo: el modo de letra, el
// pincel, la copia y el medio pixel llegan como parametros; la matriz y el recorte son los del SKCanvas.
using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Rocola.Nativo
{
    // las alineaciones y los recortes de un formato (los valores de StringAlignment y StringTrimming de GDI+, con nombre propio)
    public enum Alinea { Cerca = 0, Centro = 1, Lejos = 2 }
    public enum CorteTexto { Nada = 0, PorLetra = 1, PorPalabra = 2, PuntosPorLetra = 3, PuntosPorPalabra = 4, PuntosRuta = 5 }
    // las banderas que la rocola usa (los valores de StringFormatFlags de GDI+). Se ponen como en XP: "= SinPartir" deja SOLO esa (apaga las
    // demas) y "|= SinPartir" la anade.
    [Flags]
    public enum BanderasTexto { Ninguna = 0, CajaNegra = 4, ContarEspacios = 2048, SinPartir = 4096, SoloEnteros = 8192, SinRecortar = 16384 }

    // COMO ACOMODA GDI+ UN TEXTO EN SU CAJA: lo que XP le pone a su StringFormat.
    public sealed class FormatoTexto
    {
        public Alinea Horizontal = Alinea.Cerca, Vertical = Alinea.Cerca;
        public BanderasTexto Banderas;
        public CorteTexto Corte = CorteTexto.PorLetra;
        public bool SinMargen;   // los tipograficos (GenericTypographic y sus copias): sin el margen de 1/6 de letra
        public FormatoTexto() { }
        public FormatoTexto(BanderasTexto b) { Banderas = b; }
        // new StringFormat(otro): copia todo, tambien que sea tipografico
        public FormatoTexto(FormatoTexto o) { Horizontal = o.Horizontal; Vertical = o.Vertical; Banderas = o.Banderas; Corte = o.Corte; SinMargen = o.SinMargen; }
        // LOS DOS FORMATOS GENERICOS DE GDI+ SON UN SOLO OBJETO CADA UNO, COMPARTIDO (revision del 8-11-2026). En GDI+,
        // GdipStringFormatGetGenericDefault y GdipStringFormatGetGenericTypographic devuelven siempre el mismo objeto; .NET lo envuelve sin
        // copiarlo y su Dispose no lo borra (prueba test_getgenerictypographic de Wine, que pasa en Windows). Lo que el codigo le cambie se queda
        // para todos los que lo pidan despues, y las copias salen con el cambio: XP lo hace (EstadoReposoGpu.cs le pone Center, Center y NoWrap
        // al tipografico, y desde ese momento Espaciado de AtraccionGpu.cs y los del buscador de portadas lo heredan). El tipografico trae
        // FitBlackBox, LineLimit y NoClip y no recorta; el normal, nada y PorLetra.
        public static readonly FormatoTexto TipograficoCompartido = new FormatoTexto { Banderas = BanderasTexto.CajaNegra | BanderasTexto.SoloEnteros | BanderasTexto.SinRecortar, Corte = CorteTexto.Nada, SinMargen = true };
        public static readonly FormatoTexto NormalCompartido = new FormatoTexto();
        // el de DrawString y MeasureString sin formato (null): el de fabrica de GDI+, aparte del generico (que el codigo puede cambiar)
        public static readonly FormatoTexto SinFormato = new FormatoTexto();
    }

    public static class TextoGdiPlus
    {
        // DRAWSTRING DE GDI+: 's' con la letra 'f' y el modo de letra 'ajuste', en la caja 'r' (sin ancho o sin alto: desde el punto), con el
        // pincel de XP (color, o degradado o textura en su Shader), el formato (null = el de fabrica), 'copia' = CompositingMode.SourceCopy y
        // 'medio' = PixelOffsetMode Half o HighQuality (con el de fabrica el color del pincel en cada pixel es el de su centro, en los enteros).
        public static void Escribe(SKCanvas c, string s, Letra f, AjusteLetra ajuste, SKPaint pincel, RectangleF r, FormatoTexto sf, bool copia = false, bool medio = false)
        {
            if (string.IsNullOrEmpty(s) || f == null || pincel == null) return;
            sf = sf ?? FormatoTexto.SinFormato;
            bool noClip = (sf.Banderas & BanderasTexto.SinRecortar) != 0;
            // una caja con medida negativa borra el texto (salvo NoClip), como en GDI+ (Wine, GdipDrawString)
            if (!noClip && r.Width != 0 && r.Height != 0 && (r.Width < 0 || r.Height < 0)) return;
            SKFont sk = f.Skia(ajuste);
            Acomodo a = Acomoda(s, f, r, sf, sk);
            if (a.Piezas.Count == 0) return;
            bool recorta = !noClip && r.Width > 0 && r.Height > 0;
            int n = 0; if (recorta) { n = c.Save(); c.ClipRect(new SKRect(r.Left, r.Top, r.Right, r.Bottom)); }
            using (SKPaint p = new SKPaint { Style = SKPaintStyle.Fill })
            {
                Pixel.ConPincel(c, p, pincel, medio, 0f);
                if (copia) p.BlendMode = SKBlendMode.Src;
                p.IsAntialias = ajuste != AjusteLetra.SinSuavizar && ajuste != AjusteLetra.SinSuavizarAjustada;
                foreach (Pieza x in a.Piezas)
                {
                    c.DrawText(x.Cadena, x.X, x.Base, sk, p);
                    if (f.Subrayada) { float grosor = Math.Max(1f, f.Px / 14f); c.DrawRect(new SKRect(x.X, x.Base + grosor, x.X + x.Ancho, x.Base + 2 * grosor), p); }
                }
                if (p.Shader != null && !ReferenceEquals(p.Shader, pincel.Shader)) p.Shader.Dispose();
            }
            if (!f.Guarda(ajuste)) sk.Dispose();
            if (recorta) c.RestoreToCount(n);
        }
        // MEASURESTRING DE GDI+: con una caja (0, 0) mide sin limite; con un ancho, en una caja de ese ancho y 999999 de alto (el
        // MeasureString(s, f, ancho) de .NET). 'letras' y 'lineas' como sus out.
        public static SizeF Mide(string s, Letra f, AjusteLetra ajuste, SizeF caja, FormatoTexto sf) { int letras, lineas; return Mide(s, f, ajuste, caja, sf, out letras, out lineas); }
        public static SizeF Mide(string s, Letra f, AjusteLetra ajuste, SizeF caja, FormatoTexto sf, out int letras, out int lineas)
        {
            letras = lineas = 0;
            if (string.IsNullOrEmpty(s)) return new SizeF(0, 0);
            if (f == null) throw new ArgumentNullException("font");
            sf = sf ?? FormatoTexto.SinFormato;
            SKFont sk = f.Skia(ajuste);
            try { return MideAcomodo(Acomoda(s, f, new RectangleF(0, 0, caja.Width, caja.Height), sf, sk), caja, sf, out letras, out lineas); }
            finally { if (!f.Guarda(ajuste)) sk.Dispose(); }
        }
        // EL TEXTO HECHO TRAZO (GraphicsPath.AddString de XP: la interrogacion de la ayuda): los contornos de las letras de 'f' (en pixeles),
        // acomodados en la caja como DrawString, se anaden a 'destino'
        public static void Trazo(SKPath destino, string s, Letra f, RectangleF caja, FormatoTexto sf)
        {
            SKFont sk = f.Skia(AjusteLetra.Suave);
            foreach (Pieza pz in Acomoda(s, f, caja, sf ?? FormatoTexto.SinFormato, sk).Piezas)
                using (SKPath tp = sk.GetTextPath(pz.Cadena, new SKPoint(pz.X, pz.Base)))
                    destino.AddPath(tp);
        }

        // ---------------------------------------------------------------- como acomoda el texto GDI+
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
        internal struct Pieza { public string Cadena; public float X, Base, Ancho; }
        internal sealed class Acomodo { public readonly List<Pieza> Piezas = new List<Pieza>(); public float Margen, Alto, AnchoMayor; public int Letras; }
        private const float Holgura = 0.005f;
        private const string Puntos = "…";
        private static bool Blanco(char c) { return c == ' ' || c == '\t'; }
        // se puede partir el renglon antes de s[k] (dentro de su parrafo [p0, p1))
        private static bool PuedeCortar(string s, int p0, int k)
        {
            char a = s[k - 1], b = s[k];
            if (Blanco(b)) return false;
            if (Blanco(a)) return true;
            if ((a == '-' || a == '‐') && k - 1 > p0 && !Blanco(s[k - 2]) && !char.IsDigit(b)) return true;
            return a == '–' || a == '—';
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
                if (porPalabra && k < p1 && !PuedeCortar(s, p0, k)) continue;
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
        // EL RENGLON RECORTADO como dice el corte, desde i hasta como mucho p1 (el fin del parrafo). 'falta': queda texto sin ver despues
        // de este renglon (los modos con puntos los ponen aunque el renglon quepa). Devuelve la cadena a pintar y hasta donde llega
        // (sin recortar, hasta donde llegaba el renglon: 'finSig', con el salto si lo lleva).
        private static string Recorta(string s, float[] suma, SKFont sk, int p0, int i, int finNormal, int finSig, int p1, float util, CorteTexto t, bool noClip, bool falta, out int fin)
        {
            fin = finSig;
            switch (t)
            {
                case CorteTexto.Nada: return s.Substring(i, finNormal - i);
                case CorteTexto.PorLetra:
                case CorteTexto.PorPalabra:
                    {
                        if (noClip) return s.Substring(i, finNormal - i);
                        int k = HastaDondeCabe(suma, s, p0, i, p1, util, t == CorteTexto.PorPalabra);
                        if (k < 0) k = t == CorteTexto.PorPalabra ? finNormal : LetrasQueCaben(suma, s, i, p1, util);
                        fin = k;
                        return s.Substring(i, k - i);
                    }
                default:   // PuntosPorLetra, PuntosPorPalabra, PuntosRuta
                    {
                        if (!falta && Cabe(AnchoSin(suma, s, i, p1), util)) return s.Substring(i, p1 - i);
                        float wp = sk.MeasureText(Puntos);
                        bool porPalabra = t == CorteTexto.PuntosPorPalabra;
                        int mejor = i;
                        for (int k = i + 1; k <= p1; k++)
                        {
                            if (porPalabra && k < p1 && !PuedeCortar(s, p0, k)) continue;
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
        internal static Acomodo Acomoda(string s, Letra f, RectangleF caja, FormatoTexto sf, SKFont sk)
        {
            Acomodo a = new Acomodo();
            if (string.IsNullOrEmpty(s)) return a;
            float m = sf.SinMargen ? 0f : f.Px / 6f;
            float alto = f.AltoRenglon, asc = f.Cara.Ascenso * f.Px;
            a.Margen = m; a.Alto = alto;
            bool anchoLibre = !(caja.Width > 0f), altoLibre = !(caja.Height > 0f);
            float util = float.MaxValue;
            if (!anchoLibre)
            {
                util = caja.Width - 2 * m;
                if (caja.Width >= 0.5f && util < 0.5f) return a;   // no queda sitio para el texto (GdipDrawString/GdipMeasureString)
            }
            bool noWrap = anchoLibre || (sf.Banderas & BanderasTexto.SinPartir) != 0;
            bool noClip = (sf.Banderas & BanderasTexto.SinRecortar) != 0;
            bool espacios = (sf.Banderas & BanderasTexto.ContarEspacios) != 0;
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
                if ((sf.Banderas & BanderasTexto.SoloEnteros) != 0) visibles = Math.Min(n, (int)Math.Floor(caja.Height / alto + 1e-3f));
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
                if (ultimo && desborda) c = Recorta(s, suma, sk, p0, i, fin, fin2, p1, util, sf.Corte, noClip, true, out fin2);
                else if (noWrap && !anchoLibre && !Cabe(AnchoSin(suma, s, i, p1), util)) c = Recorta(s, suma, sk, p0, i, fin, fin2, p1, util, sf.Corte, noClip, false, out fin2);
                else c = s.Substring(i, fin - i);
                cad[v] = c;
                float w = string.IsNullOrEmpty(c) ? 0f : sk.MeasureText(espacios ? c : c.TrimEnd(' ', '\t'));
                anchos[v] = w;
                if (w > a.AnchoMayor) a.AnchoMayor = w;
                a.Letras = Math.Max(a.Letras, fin2);
            }
            // 4) donde va cada uno
            float altoBloque = visibles * alto, y0 = caja.Y;
            if (altoLibre) { if (sf.Vertical == Alinea.Centro) y0 = caja.Y - altoBloque / 2f; else if (sf.Vertical == Alinea.Lejos) y0 = caja.Y - altoBloque; }
            else { if (sf.Vertical == Alinea.Centro) y0 = caja.Y + (caja.Height - altoBloque) / 2f; else if (sf.Vertical == Alinea.Lejos) y0 = caja.Y + caja.Height - altoBloque; }
            for (int v = 0; v < visibles; v++)
            {
                float w = anchos[v], x;
                if (anchoLibre) x = sf.Horizontal == Alinea.Centro ? caja.X - w / 2f : sf.Horizontal == Alinea.Lejos ? caja.X - w - m : caja.X + m;
                else x = sf.Horizontal == Alinea.Centro ? caja.X + (caja.Width - w) / 2f : sf.Horizontal == Alinea.Lejos ? caja.Right - m - w : caja.X + m;
                a.Piezas.Add(new Pieza { Cadena = cad[v], X = x, Base = y0 + v * alto + asc, Ancho = w });
            }
            return a;
        }
        // MeasureString: el renglon mas ancho mas los margenes; de alto los renglones, cortado al alto de la caja salvo con NoClip
        private static SizeF MideAcomodo(Acomodo a, SizeF caja, FormatoTexto sf, out int letras, out int lineas)
        {
            letras = a.Letras; lineas = a.Piezas.Count;
            if (lineas == 0) return new SizeF(0, 0);
            float ancho = a.AnchoMayor + 2 * a.Margen, alto = lineas * a.Alto;
            if (caja.Width > 0 && ancho > caja.Width) ancho = caja.Width;
            if (caja.Height > 0 && (sf.Banderas & BanderasTexto.SinRecortar) == 0 && alto > caja.Height) alto = caja.Height;
            return new SizeF(ancho, alto);
        }
    }
}

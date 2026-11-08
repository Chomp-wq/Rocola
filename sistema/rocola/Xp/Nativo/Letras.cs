// LAS LETRAS DE LA ROCOLA DE XP, NATIVAS (port nativo, el dibujo; 8-11-2026). El codigo de XP pide letras por familia, tamano, unidad y
// estilo (en XP, System.Drawing.Font); aqui una Letra son solo esos datos mas la cara que les toca (Cara: el archivo de letra, la negrita o la
// cursiva que haya que inventar y las medidas del renglon), y da la letra de Skia (SKFont) con el ajuste que pide el modo de letra. Lo que
// XP lee de una letra para colocar cosas (el alto del renglon, el tamano en puntos) sale de aqui con las mismas cuentas que GDI+.
// Los algoritmos son los de Plataforma/Dibujo2D.cs (revision del 8-11-2026), movidos aqui: Dibujo2D los llama mientras dure la traduccion.
using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Rocola.Nativo
{
    // como pide el texto el modo de letra de XP (los valores de TextRenderingHint de GDI+, con nombre propio)
    public enum AjusteLetra { DelSistema = 0, SinSuavizarAjustada = 1, SinSuavizar = 2, SuaveAjustada = 3, Suave = 4, ClearTypeAjustada = 5 }

    // LA CARA DE UNA LETRA: el archivo de letra que le toca a una familia y un estilo, si la negrita o la cursiva hay que inventarla (la familia
    // no la trae) y el ascenso, el descenso y el renglon en "em" (multiplicar por el tamano en pixeles)
    public sealed class Cara
    {
        public SKTypeface Tipo; public bool NegritaInventada, CursivaInventada;
        public float Ascenso, Descenso, Renglon;
    }

    // LA LETRA QUE PIDE XP: la familia como la resuelve GDI+ (Letras.Resuelve, Letras.Familia o una generica), el tamano y su unidad tal como
    // los dio XP (en puntos o en pixeles: ceil(96*pt/72) de la letra de GDI se calcula con el tamano de XP, no con los pixeles redondeados),
    // y el estilo pedido. Guarda sus SKFont por modo de letra (Skia); al soltarla (Dispose) se sueltan, y si se vuelve a usar se rehacen.
    public sealed class Letra : IDisposable
    {
        public readonly string Familia;
        public readonly float Tamano;          // en pixeles si UnidadPixel; si no, en puntos
        public readonly bool UnidadPixel, Negrita, Cursiva, Subrayada;
        public readonly Cara Cara;
        private readonly SKFont[] sk = new SKFont[6];
        private Letra(string familia, float tamano, bool enPx, bool negrita, bool cursiva, bool subrayada)
        {
            Familia = familia; Tamano = tamano; UnidadPixel = enPx; Negrita = negrita; Cursiva = cursiva; Subrayada = subrayada;
            Cara = Letras.Busca(familia, negrita, cursiva);
        }
        // new Font(familia, pt, estilo) y new Font(familia, px, estilo, GraphicsUnit.Pixel) de XP; 'familia' ya resuelta
        public static Letra EnPuntos(string familia, float pt, bool negrita = false, bool cursiva = false, bool subrayada = false) { return new Letra(familia, pt, false, negrita, cursiva, subrayada); }
        public static Letra EnPixeles(string familia, float px, bool negrita = false, bool cursiva = false, bool subrayada = false) { return new Letra(familia, px, true, negrita, cursiva, subrayada); }
        // el tamano en pixeles a 96 puntos por pulgada (como la rocola en XP) y en puntos (Font.SizeInPoints)
        public float Px { get { return UnidadPixel ? Tamano : Tamano * 96f / 72f; } }
        public float Puntos { get { return UnidadPixel ? Px * 72f / 96f : Tamano; } }
        // el alto del renglon en pixeles (Font.GetHeight de GDI+) y el mismo redondeado hacia arriba (Font.Height)
        public float AltoRenglon { get { return Cara.Renglon * Px; } }
        public int AltoEntero { get { return (int)Math.Ceiling(AltoRenglon); } }
        // LA LETRA DE SKIA CON EL AJUSTE QUE PIDE EL MODO DE LETRA (lo que GDI+ hace con TextRenderingHint): ajustada a los pixeles con
        // SystemDefault y los GridFit, libre con los demas; sin suavizar con SingleBitPerPixel*. No se cambia despues de hecha (la usan varios
        // hilos a la vez; Skia la lee sin tocarla).
        public SKFont Skia(AjusteLetra h)
        {
            int k = (int)h;
            if (k < 0 || k >= sk.Length) return Nueva(h);   // (un modo que no existe: como antes, una cada vez; quien la pide la suelta)
            lock (sk)
            {
                if (sk[k] == null) sk[k] = Nueva(h);
                return sk[k];
            }
        }
        // (para quien la necesita suelta: la suya, que suelta el)
        public SKFont Nueva(AjusteLetra h)
        {
            SKFont f = new SKFont(Cara.Tipo, Px);
            f.Embolden = Cara.NegritaInventada;
            if (Cara.CursivaInventada) f.SkewX = -0.25f;
            bool ajustada = h == AjusteLetra.DelSistema || h == AjusteLetra.SuaveAjustada || h == AjusteLetra.ClearTypeAjustada || h == AjusteLetra.SinSuavizarAjustada;
            f.Edging = h == AjusteLetra.SinSuavizar || h == AjusteLetra.SinSuavizarAjustada ? SKFontEdging.Alias : SKFontEdging.Antialias;
            f.Hinting = ajustada ? SKFontHinting.Normal : SKFontHinting.Slight;
            f.Subpixel = !ajustada;
            f.LinearMetrics = !ajustada;
            return f;
        }
        public void Dispose()
        {
            lock (sk) for (int i = 0; i < sk.Length; i++) if (sk[i] != null) { sk[i].Dispose(); sk[i] = null; }
        }
        // (las que siempre piden un modo que no existe no se guardan)
        internal bool Guarda(AjusteLetra h) { return (int)h >= 0 && (int)h < sk.Length; }
    }

    // DONDE ESTAN LAS LETRAS. Las de la rocola de XP, copiadas del disco de Windows (rocola-discos: /rocola/datos/letras; ROCOLA_LETRAS_XP
    // manda); si no estan (la memoria de prueba, una PC ajena), las libres que haya, con DejaVu Sans primero (la que usa Mono para las
    // referencias de las pruebas). La negrita y la cursiva que la familia no trae se inventan como GDI+ (mas gruesa, inclinada).
    public static class Letras
    {
        // las genericas de GDI+ en Windows XP: sin remates Microsoft Sans Serif, de ancho fijo Courier New, con remates Times New Roman
        public const string SinRemates = "Microsoft Sans Serif", AnchoFijo = "Courier New", ConRemates = "Times New Roman";
        private static readonly Dictionary<string, Cara> hechas = new Dictionary<string, Cara>(StringComparer.OrdinalIgnoreCase);
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
        // COMO GDI+: pedir por su nombre una familia que no esta instalada (new FontFamily(nombre)) lanza ArgumentException. La rocola de XP
        // prueba asi Segoe UI, Tahoma y Verdana para Ctrl+F3 (FamiliaUi): en Windows XP no hay Segoe UI y queda Tahoma.
        public static string Familia(string nombre) { if (!Instalada(nombre)) throw new ArgumentException("Font '" + nombre + "' cannot be found."); return nombre; }
        // new Font("una que no esta", ...) no lanza: GDI+ pone Microsoft Sans Serif (la generica) en su lugar
        public static string Resuelve(string nombre) { return nombre != null && Instalada(nombre) ? nombre : SinRemates; }
        // las de repuesto (libres), por orden
        private static readonly string[] Repuesto = { "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf" };
        private static readonly string[] RepuestoNegrita = { "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf" };
        private static readonly string[] RepuestoFijo = { "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", "/usr/share/fonts/dejavu/DejaVuSansMono.ttf" };
        private static readonly string[] RepuestoFijoNegrita = { "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf", "/usr/share/fonts/dejavu/DejaVuSansMono-Bold.ttf" };
        private static string Primero(string[] l) { foreach (string f in l) if (File.Exists(f)) return f; return null; }

        public static Cara Busca(string familia, bool b, bool i)
        {
            string clave = familia + "|" + (b ? "b" : "") + (i ? "i" : "");
            lock (hechas)
            {
                Cara l;
                if (hechas.TryGetValue(clave, out l)) return l;
                l = new Cara();
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
        private static void Metricas(Cara l)
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
        // una tabla de la letra (de la letra de GDI de TextoGdi: VDMX y hdmx), o null
        internal static byte[] Tabla(SKTypeface tf, string tabla) { try { return tf.GetTableData(Tag(tabla)); } catch { return null; } }
        internal static int U16(SKTypeface tf, string tabla, int at)
        {
            try { byte[] d = tf.GetTableData(Tag(tabla)); if (d == null || d.Length < at + 2) return 0; return (d[at] << 8) | d[at + 1]; } catch { return 0; }
        }
        internal static int S16(SKTypeface tf, string tabla, int at) { int v = U16(tf, tabla, at); return v >= 32768 ? v - 65536 : v; }
    }
}

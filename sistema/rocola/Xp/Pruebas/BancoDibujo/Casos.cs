// BANCO DEL DIBUJO (port nativo, pista del dibujo): los casos al azar, como DATOS (sin tipos de GDI+ ni de Rocola.Nativo). Cada ejecutor
// (EjecutaGdi.cs con la API de Dibujo2D, EjecutaNativo.cs con las funciones nativas) hace el mismo caso con lo suyo y da los bytes de la
// imagen y los valores que devuelve. Los enteros de modos y banderas llevan los valores de GDI+ (SmoothingMode, StringFormatFlags...).
// Mismo generador y misma semilla = los mismos casos en las dos compilaciones (System.Random con semilla es determinista).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using SkiaSharp;

namespace Banco
{
    // ---------------------------------------------------------------- los datos de un caso
    internal sealed class ImagenD { public int W, H, Formato; public uint[] Px; }   // Px: BGRA premultiplicado (como Format32bppPArgb), fila a fila
    internal sealed class PincelD
    {
        public int Tipo;                  // 0 color, 1 lineal de dos puntos, 2 lineal de caja con modo, 3 lineal de caja con angulo, 4 de trazo (elipse), 5 textura
        public Color C1, C2; public PointF P0, P1; public RectangleF Caja; public int Modo; public float Angulo; public bool Escalable;
        public float[] BlendF, BlendP; public Color[] IcC; public float[] IcP; public int Wrap = -1;
        public bool Corrido; public PointF Centro; public int FormaRuta;   // de trazo: 0 elipse, 1 rectangulo, 2 poligono
        public ImagenD Img; public float TX, TY;
    }
    internal sealed class PlumaD { public Color C; public float W; public int Join, CapIni, CapFin, Dash; public float[] Patron; public PincelD Pincel; }
    internal sealed class LetraD { public string Familia; public int Generica = -1; public float Tam; public int Estilo; public int Unidad = 3; public int Ctor; }
    internal sealed class FormatoD
    {
        public int Tipo;                  // 0 null, 1 new StringFormat(), 2 GenericTypographic (EL compartido), 3 GenericDefault, 4 new StringFormat(GenericTypographic), 5 new StringFormat(flags)
        public int Al, LAl, Flags, Trim; public bool FlagsConO;   // FlagsConO: |= en vez de =
        public bool CambiaAl;
    }
    internal sealed class RutaD { public List<object[]> Pasos = new List<object[]>(); public int Relleno; }   // object[]: {nombre, args...}
    internal sealed class EstadoD
    {
        public int Suave = 3, Letra = 0, Copia = 0, Filtro = 3, Medio = 0, Calidad = 0;   // los de fabrica de GDI+ (Dibujo2D): None, SystemDefault, SourceOver, Bilinear, Default
        public List<object[]> Matriz = new List<object[]>();          // {"T", dx, dy} {"S", sx, sy} {"R", g}
        public List<object[]> Recortes = new List<object[]>();        // {"R", RectangleF, modo} {"P", RutaD, modo} {"I", RectangleF} {"X", Rectangle} {"Q"} (ResetClip)
    }
    internal sealed class Caso
    {
        public int Num, W, H, FormatoLienzo; public Color? Fondo; public EstadoD Estado = new EstadoD();
        public List<object[]> Ops = new List<object[]>();             // {nombre, args...}
        public string Clase;
    }

    internal static class Casos
    {
        private static Random r;
        private static float F(double a, double b) { return (float)(a + r.NextDouble() * (b - a)); }
        private static int I(int a, int b) { return r.Next(a, b); }   // [a, b)
        private static bool B(double p = 0.5) { return r.NextDouble() < p; }
        private static T Uno<T>(params T[] l) { return l[r.Next(l.Length)]; }
        private static float Redondo(float v) { int k = r.Next(4); return k == 0 ? (float)Math.Round(v) : k == 1 ? (float)Math.Round(v * 2) / 2f : k == 2 ? (float)Math.Round(v * 4) / 4f : v; }

        internal static Color Col(bool alfa = true)
        {
            int a = !alfa ? 255 : Uno(255, 255, 0, 1, 32, 128, 200, I(0, 256));
            return Color.FromArgb(a, I(0, 256), I(0, 256), I(0, 256));
        }
        // una caja: con medidas a veces cero, negativas o con decimales
        internal static RectangleF Caja(int w, int h)
        {
            float x = Redondo(F(-w * 0.2, w * 0.9)), y = Redondo(F(-h * 0.2, h * 0.9));
            float cw = Redondo(F(0, w * 1.1)), ch = Redondo(F(0, h * 1.1));
            int k = I(0, 14);
            if (k == 0) cw = 0; else if (k == 1) ch = 0; else if (k == 2) cw = -cw; else if (k == 3) ch = -ch; else if (k == 4) { cw = 0; ch = 0; }
            return new RectangleF(x, y, cw, ch);
        }
        internal static Rectangle CajaEntera(int w, int h) { RectangleF c = Caja(w, h); return new Rectangle((int)c.X, (int)c.Y, (int)c.Width, (int)c.Height); }

        // ---- textos: acentos, ene, emoji, tabuladores, saltos, "&", espacios al final, guiones y palabras largas
        private static readonly string[] Trozos = { "a", "e", "o", "Cancion", "LO MAS SONADO", "Ángel", "ñandú", "Ñ", "ü", "&", "&&", "& ", " ", "  ", "\t", "\n", "\r\n", "-", "–", "—",
            "Los Ángeles Azules", "versión", "🎵", "😀", "x", "W", "i", "1234567890", "Supercalifragilisticoespialidoso", "(en vivo)", "«Bikina»", "...", "…", "a-b", "-7", "　", "M", "~" };
        internal static string Texto()
        {
            int k = I(0, 12);
            if (k == 0) return "";
            if (k == 1) return Uno("A H O R A   S U E N A", "Inserta una moneda", "01 Cancion Larga con un titulo bastante largo para el estado extendido", "BORRAR", "ESPACIO", "Créditos 152", "&Archivo", "Fin&", "Tab\tTab\tTab", "dos\nrenglones", "  espacios al final   ");
            StringBuilder sb = new StringBuilder();
            int n = I(1, 14);
            for (int i = 0; i < n; i++) sb.Append(Trozos[I(0, Trozos.Length)]);
            return sb.ToString();
        }

        // ---- letras: las de XP (las que no hay aqui pasan a DejaVu), una que no existe y las genericas; 7,5, 8,25, 9,5 pt y kh*0,42
        internal static LetraD Letra()
        {
            LetraD l = new LetraD();
            int k = I(0, 12);
            if (k < 2) l.Generica = I(0, 3);
            else l.Familia = Uno("Microsoft Sans Serif", "Trebuchet MS", "Tahoma", "Segoe UI", "Arial", "Courier New", "Times New Roman", "Verdana", "No Existe", "DejaVu Sans");
            float kh = F(18, 60);
            l.Tam = Uno(7.5f, 8.25f, 9f, 9.5f, 10f, 11.5f, 11.9f, 12f, 15f, 18.2f, kh * 0.42f, F(5, 40), 1f, 0.5f);
            l.Estilo = Uno(0, 0, 1, 1, 2, 3, 4, 5);
            l.Unidad = Uno(3, 3, 2, 2, 2, 0, 1, 4, 5, 6);   // Point, Pixel, World, Display, Inch, Document, Millimeter
            if (l.Unidad == 4) l.Tam = F(0.08, 0.4); else if (l.Unidad == 5) l.Tam = F(20, 120); else if (l.Unidad == 6) l.Tam = F(2, 9);
            l.Ctor = I(0, 4);
            return l;
        }
        internal static FormatoD Formato()
        {
            FormatoD f = new FormatoD();
            f.Tipo = Uno(0, 1, 1, 1, 2, 3, 4, 5);
            f.Al = I(0, 3); f.LAl = I(0, 3); f.Trim = I(0, 6);
            int[] bits = { 1, 4, 2048, 4096, 8192, 16384 };
            foreach (int b in bits) if (B(0.3)) f.Flags |= b;
            f.FlagsConO = B(); f.CambiaAl = B(0.8);
            return f;
        }

        // ---- imagenes: medidas, alfa y formato al azar (los puntos premultiplicados)
        internal static ImagenD Imagen(int maxW = 96, int maxH = 96)
        {
            ImagenD im = new ImagenD();
            im.W = Uno(1, 2, 3, I(1, maxW), I(1, maxW), I(8, maxW)); im.H = Uno(1, 2, 3, I(1, maxH), I(1, maxH), I(8, maxH));
            im.Formato = Uno(0, 1, 2);   // PArgb, Argb, 24 bits
            im.Px = new uint[im.W * im.H];
            int modo = I(0, 5);   // 0 opaca, 1 alfa al azar, 2 degradado de alfa, 3 transparente con manchas, 4 un color (para la clave)
            Color c0 = Col(false), c1 = Col(false);
            for (int y = 0; y < im.H; y++)
                for (int x = 0; x < im.W; x++)
                {
                    int a, rr, gg, bb;
                    double t = im.W > 1 ? x / (double)(im.W - 1) : 0, u = im.H > 1 ? y / (double)(im.H - 1) : 0;
                    rr = (int)(c0.R + (c1.R - c0.R) * t); gg = (int)(c0.G + (c1.G - c0.G) * u); bb = I(0, 256);
                    switch (modo)
                    {
                        case 0: a = 255; break;
                        case 1: a = I(0, 256); break;
                        case 2: a = (int)(255 * t); break;
                        case 3: a = B(0.2) ? I(1, 256) : 0; break;
                        default: a = 255; rr = c0.R; gg = c0.G; bb = c0.B; if (B(0.3)) { rr = c1.R; gg = c1.G; bb = c1.B; } break;
                    }
                    if (im.Formato == 2) a = 255;
                    uint pr = (uint)(rr * a / 255), pg = (uint)(gg * a / 255), pb = (uint)(bb * a / 255);
                    im.Px[y * im.W + x] = ((uint)a << 24) | (pr << 16) | (pg << 8) | pb;
                }
            return im;
        }

        // ---- pinceles y plumas
        internal static PincelD Pincel(int w, int h, bool conTextura = true)
        {
            PincelD p = new PincelD();
            p.Tipo = Uno(0, 0, 0, 1, 2, 2, 3, 3, 4, 4, conTextura ? 5 : 0);
            p.C1 = Col(); p.C2 = Col();
            switch (p.Tipo)
            {
                case 1:
                    p.P0 = new PointF(Redondo(F(-10, w)), Redondo(F(-10, h))); p.P1 = new PointF(Redondo(F(-10, w)), Redondo(F(-10, h)));
                    if (B(0.05)) p.P1 = p.P0;   // GDI+ lanza
                    break;
                case 2: case 3:
                    p.Caja = new RectangleF(Redondo(F(-10, w)), Redondo(F(-10, h)), Redondo(F(0, w)), Redondo(F(0, h)));
                    if (B(0.05)) p.Caja.Width = 0;   // GDI+ lanza
                    p.Modo = I(0, 4); p.Angulo = Uno(0f, 45f, 90f, 135f, 180f, 270f, -30f, F(-720, 720)); p.Escalable = B();
                    break;
                case 4:
                    p.Caja = new RectangleF(Redondo(F(-10, w)), Redondo(F(-10, h)), Redondo(F(1, w)), Redondo(F(1, h)));
                    p.FormaRuta = Uno(0, 0, 0, 1, 2);
                    p.Corrido = B(); p.Centro = new PointF(p.Caja.X + F(0, p.Caja.Width), p.Caja.Y + F(0, p.Caja.Height));
                    break;
                case 5:
                    p.Img = Imagen(40, 40); p.Wrap = I(0, 5); if (B()) { p.TX = Redondo(F(-20, 20)); p.TY = Redondo(F(-20, 20)); }
                    break;
            }
            if (p.Tipo >= 1 && p.Tipo <= 4)
            {
                int k = I(0, 6);
                if (k == 0 || (p.Tipo == 4 && k < 3))
                {
                    int n = I(2, 6); p.BlendF = new float[n]; p.BlendP = new float[n];
                    for (int i = 0; i < n; i++) { p.BlendF[i] = F(0, 1); p.BlendP[i] = i == 0 ? 0f : i == n - 1 ? 1f : F(p.BlendP[i - 1], 1); }
                }
                else if (k == 1 && p.Tipo != 4)
                {
                    int n = I(2, 6); p.IcC = new Color[n]; p.IcP = new float[n];
                    for (int i = 0; i < n; i++) { p.IcC[i] = Col(); p.IcP[i] = i == 0 ? 0f : i == n - 1 ? 1f : F(p.IcP[i - 1], 1); }
                }
                if (p.Tipo != 4 && B(0.3)) p.Wrap = Uno(0, 1, 2, 3);   // (Clamp no vale en un lineal de GDI+)
            }
            return p;
        }
        internal static PlumaD Pluma(int w, int h)
        {
            PlumaD p = new PlumaD();
            p.C = Col(); p.W = Uno(0f, 0.5f, 1f, 1f, 1.4f, 1.7f, 2f, 2.8f, 3f, F(0, 14));
            p.Join = I(0, 4); p.CapIni = Uno(0, 0, 1, 2, 2, 3); p.CapFin = B(0.7) ? p.CapIni : Uno(0, 1, 2, 3);
            p.Dash = Uno(0, 0, 0, 0, 1, 2, 3, 4, 5);
            if (p.Dash == 5) { int n = I(1, 3) * 2; p.Patron = new float[n]; for (int i = 0; i < n; i++) p.Patron[i] = F(0.5, 4); }
            if (B(0.15)) p.Pincel = Pincel(w, h, false);
            return p;
        }
        internal static RutaD Ruta(int w, int h)
        {
            RutaD rt = new RutaD(); rt.Relleno = I(0, 2);
            int n = I(1, 6);
            for (int i = 0; i < n; i++)
            {
                switch (I(0, 10))
                {
                    case 0: case 1:   // el rectangulo redondo de XP (Form1.Fondo.cs RoundedPath): cuatro arcos y cerrar
                        {
                            RectangleF c = new RectangleF(Redondo(F(-5, w * 0.8)), Redondo(F(-5, h * 0.8)), Redondo(F(4, w)), Redondo(F(4, h)));
                            float d = Math.Max(1f, Math.Min(Redondo(F(1, 30)), Math.Min(c.Width, c.Height)));
                            rt.Pasos.Add(new object[] { "Arco", c.X, c.Y, d, d, 180f, 90f });
                            rt.Pasos.Add(new object[] { "Arco", c.Right - d, c.Y, d, d, 270f, 90f });
                            rt.Pasos.Add(new object[] { "Arco", c.Right - d, c.Bottom - d, d, d, 0f, 90f });
                            rt.Pasos.Add(new object[] { "Arco", c.X, c.Bottom - d, d, d, 90f, 90f });
                            rt.Pasos.Add(new object[] { "Cierra" });
                            break;
                        }
                    case 2: rt.Pasos.Add(new object[] { "Arco", Redondo(F(-5, w)), Redondo(F(-5, h)), Redondo(F(1, w)), Redondo(F(1, h)), F(-360, 360), F(-360, 360) }); break;
                    case 3: rt.Pasos.Add(new object[] { "Linea", Redondo(F(-5, w)), Redondo(F(-5, h)), Redondo(F(-5, w)), Redondo(F(-5, h)) }); break;
                    case 4: rt.Pasos.Add(new object[] { "Bezier", F(0, w), F(0, h), F(0, w), F(0, h), F(0, w), F(0, h), F(0, w), F(0, h) }); break;
                    case 5: rt.Pasos.Add(new object[] { "Rect", Caja(w, h) }); break;
                    case 6: rt.Pasos.Add(new object[] { "Elipse", Redondo(F(-5, w)), Redondo(F(-5, h)), Redondo(F(0, w)), Redondo(F(0, h)) }); break;
                    case 7: rt.Pasos.Add(new object[] { "Poligono", Picos(w, h) }); break;
                    case 8: rt.Pasos.Add(new object[] { "Porcion", Redondo(F(0, w)), Redondo(F(0, h)), Redondo(F(1, w)), Redondo(F(1, h)), F(-360, 360), F(-360, 360) }); break;
                    default: rt.Pasos.Add(new object[] { B() ? "Cierra" : "Empieza" }); break;
                }
            }
            return rt;
        }
        // poligonos con picos (para el limite de esquinas en punta)
        internal static PointF[] Picos(int w, int h)
        {
            int n = I(3, 9); PointF[] p = new PointF[n];
            float cx = F(0, w), cy = F(0, h);
            for (int i = 0; i < n; i++)
            {
                double a = i * 2 * Math.PI / n + F(-0.3, 0.3), rr = (i % 2 == 0 ? F(w * 0.2, w * 0.6) : F(1, w * 0.15));
                p[i] = new PointF(Redondo((float)(cx + Math.Cos(a) * rr)), Redondo((float)(cy + Math.Sin(a) * rr)));
            }
            return p;
        }

        // ---- el estado del Graphics: suavizado, modo de letra, copia, filtro, medio pixel, traslaciones, escalas, giros y recortes
        internal static EstadoD Estado(int w, int h, bool conGiro = true)
        {
            EstadoD e = new EstadoD();
            e.Suave = Uno(3, 4, 4, 0, 1, 2);
            e.Letra = I(0, 6);
            e.Copia = B(0.2) ? 1 : 0;
            e.Filtro = Uno(3, 3, 6, 6, 5, 7, 4, 1, 2, 0);
            e.Medio = Uno(0, 0, 0, 3, 1, 4, 2);
            e.Calidad = I(0, 5);
            int k = I(0, 8);
            if (k >= 3) e.Matriz.Add(new object[] { "T", Redondo(F(-20, 20)), Redondo(F(-20, 20)) });
            if (k >= 5) e.Matriz.Add(new object[] { "S", Uno(0.5f, 2f, F(0.3, 2.5)), Uno(0.5f, 2f, F(0.3, 2.5)) });
            if (k == 7 && conGiro) e.Matriz.Add(new object[] { "R", F(-180, 180) });
            if (B(0.3)) e.Matriz.Add(new object[] { "T", Redondo(F(-10, 10)), Redondo(F(-10, 10)) });
            int nr = Uno(0, 0, 0, 1, 1, 2, 3);
            for (int i = 0; i < nr; i++)
            {
                int t = I(0, 9);
                if (t <= 2) e.Recortes.Add(new object[] { "R", Caja(w, h), Uno(0, 1, 1, 4) });
                else if (t == 3) e.Recortes.Add(new object[] { "P", Ruta(w, h), Uno(0, 1, 4) });
                else if (t == 4) e.Recortes.Add(new object[] { "I", Caja(w, h) });
                else if (t == 5) e.Recortes.Add(new object[] { "X", CajaEntera(w, h) });
                else if (t == 6) e.Recortes.Add(new object[] { "Q" });
                else e.Recortes.Add(new object[] { "Ri", CajaEntera(w, h), Uno(0, 1) });
            }
            return e;
        }

        // ---- los casos
        internal static List<Caso> Genera(int n, int semilla, string solo)
        {
            r = new Random(semilla);
            List<Caso> l = new List<Caso>();
            string[] clases = { "texto", "mide", "gdi", "midegdi", "forma", "forma", "forma", "imagen", "imagen", "puntos", "lee", "guarda", "recorte", "letra", "cadena", "estado" };
            for (int i = 0; i < n; i++)
            {
                Caso c = new Caso { Num = i };
                c.Clase = clases[I(0, clases.Length)];
                c.W = Uno(32, 64, 100, 128, I(16, 200)); c.H = Uno(32, 48, 64, 100, I(16, 160));
                c.FormatoLienzo = Uno(0, 0, 1);
                if (B(0.6)) c.Fondo = Col();
                c.Estado = Estado(c.W, c.H, c.Clase != "gdi");
                int nops = c.Clase == "estado" ? 1 : I(1, 4);
                for (int k = 0; k < nops; k++) c.Ops.Add(Op(c));
                if (solo == null || solo == c.Clase) l.Add(c); else i--;
            }
            return l;
        }
        private static object[] Op(Caso c)
        {
            int w = c.W, h = c.H;
            switch (c.Clase)
            {
                case "texto":
                    {
                        // un tercio: el tipografico COMPARTIDO de GDI+ cambiado por su nombre y vuelto a pedir (como EstadoReposoGpu y Espaciado)
                        if (B(0.33)) return new object[] { "Compartido", Texto(), Letra(), Pincel(w, h), Caja(w, h), Formato() };
                        int k = I(0, 7);
                        return new object[] { "DrawString", k, Texto(), Letra(), Pincel(w, h), Caja(w, h), Formato() };
                    }
                case "mide": return new object[] { "MeasureString", I(0, 7), Texto(), Letra(), Formato(), Redondo(F(0, w)), Redondo(F(0, h)), I(-5, w) };
                case "gdi":
                    {
                        int fl = 0; int[] bits = { 1, 2, 4, 8, 16, 32, 64, 256, 512, 2048, 8192, 32768, 262144, 1048576, 2097152, 268435456, 536870912 };
                        foreach (int b in bits) if (B(0.15)) fl |= b;
                        Color col = Uno(Col(), Col(), Color.Transparent, Color.FromArgb(0, 255, 255, 255), Color.White);
                        return new object[] { "DrawText", I(0, 4), Texto(), Letra(), CajaEntera(w, h), new Point(I(-10, w), I(-10, h)), col, fl };
                    }
                case "midegdi":
                    {
                        int fl = 0; int[] bits = { 1, 2, 4, 8, 16, 32, 64, 256, 512, 2048, 8192, 32768, 262144, 268435456, 536870912 };
                        foreach (int b in bits) if (B(0.15)) fl |= b;
                        return new object[] { "MeasureText", I(0, 6), Texto(), Letra(), new Size(Uno(0, I(-5, w), int.MaxValue, I(1, 400)), Uno(0, I(-5, h), int.MaxValue, I(1, 400))), fl };
                    }
                case "forma":
                    {
                        string[] ops = { "FillRect", "DrawRect", "FillEllipse", "DrawEllipse", "FillPath", "DrawPath", "FillPolygon", "DrawPolygon", "DrawLine", "DrawLines", "DrawArc", "FillPie", "DrawBezier", "FillRegion", "Clear" };
                        string o = ops[I(0, ops.Length)];
                        return new object[] { o, I(0, 4), Pincel(w, h), Pluma(w, h), Caja(w, h), Ruta(w, h), Picos(w, h), I(0, 2), F(-360, 360), F(-360, 360), Ruta(w, h), Col() };
                    }
                case "imagen":
                    {
                        float[][] m = null; int k = I(0, 5);
                        if (k == 1) { m = Identidad(); m[3][3] = F(0, 1); }
                        else if (k == 2) { m = new float[5][]; for (int i = 0; i < 5; i++) { m[i] = new float[5]; for (int j = 0; j < 5; j++) m[i][j] = i == j ? F(0.5, 1.2) : (B(0.3) ? F(-0.3, 0.3) : 0f); } }
                        ImagenD im = Imagen(); Color baja = Col(false), alta = baja;
                        if (B()) { uint px = im.Px[I(0, im.Px.Length)]; int a = (int)(px >> 24); if (a > 0) baja = Color.FromArgb(255, (int)((px >> 16) & 255) * 255 / a, (int)((px >> 8) & 255) * 255 / a, (int)(px & 255) * 255 / a); alta = Color.FromArgb(255, Math.Min(255, baja.R + I(0, 3)), Math.Min(255, baja.G + I(0, 3)), Math.Min(255, baja.B + I(0, 3))); }
                        return new object[] { "DrawImage", I(0, 15), im, Caja(w, h), new RectangleF(Redondo(F(-3, im.W)), Redondo(F(-3, im.H)), Redondo(F(-1, im.W + 2)), Redondo(F(-1, im.H + 2))), new Point(I(-10, w), I(-10, h)), m, Uno(-1, -1, 0, 1, 2, 3, 4), B(0.2), baja, alta, new PointF[] { new PointF(F(-10, w), F(-10, h)), new PointF(F(-10, w), F(-10, h)), new PointF(F(-10, w), F(-10, h)) } };
                    }
                case "puntos":
                    {
                        ImagenD im = Imagen(80, 80);
                        Rectangle zona = new Rectangle(I(0, im.W), I(0, im.H), 0, 0); zona.Width = I(1, im.W - zona.X + 1); zona.Height = I(1, im.H - zona.Y + 1);
                        uint[] datos = new uint[zona.Width * zona.Height]; for (int i = 0; i < datos.Length; i++) datos[i] = (uint)r.Next() ^ ((uint)r.Next() << 16);
                        return new object[] { "LockBits", im, zona, Uno(0, 1, 2, 3), Uno(1, 2, 3), datos, I(0, im.W), I(0, im.H), Col() };
                    }
                case "lee": return new object[] { "FromStream", I(0, 6), Imagen(60, 60), I(1, 101) };
                case "guarda": return new object[] { "Save", I(0, 4), Imagen(60, 60), I(0, 101) };
                case "recorte": return new object[] { "ClipBounds", Caja(w, h), Ruta(w, h), I(0, 6) };
                case "letra": return new object[] { "Letra", Letra(), Uno("Segoe UI", "Tahoma", "Verdana", "Microsoft Sans Serif", "No Existe", "Arial") };
                case "cadena": return new object[] { "AddString", Texto(), Letra(), Caja(w, h), Formato(), Pincel(w, h), Pluma(w, h), I(0, 3) };
                default: return new object[] { "Estado", Pincel(w, h), Caja(w, h), Estado(w, h), I(0, 4), Texto(), Letra(), Formato() };
            }
        }
        internal static float[][] Identidad() { float[][] m = new float[5][]; for (int i = 0; i < 5; i++) { m[i] = new float[5]; m[i][i] = 1f; } return m; }

        // ---- archivos de imagen para leer: JPEG, PNG (opaco y con alfa) por Skia, y GIF y BMP hechos a mano
        internal static byte[] Archivo(ImagenD im, int tipo, int calidad)
        {
            if (tipo == 3) return Gif(im);
            if (tipo == 4) return Bmp(im);
            using (SKBitmap b = new SKBitmap(new SKImageInfo(im.W, im.H, SKColorType.Bgra8888, tipo == 2 ? SKAlphaType.Opaque : SKAlphaType.Premul)))
            {
                unsafe { uint* p = (uint*)b.GetPixels(); for (int i = 0; i < im.Px.Length; i++) p[i] = tipo == 2 ? (im.Px[i] | 0xFF000000u) : im.Px[i]; }
                b.NotifyPixelsChanged();
                using (SKData d = b.Encode(tipo == 0 ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, calidad)) return d.ToArray();
            }
        }
        private static byte[] Bmp(ImagenD im)
        {
            int fila = (im.W * 3 + 3) & ~3, tam = 54 + fila * im.H;
            byte[] b = new byte[tam];
            b[0] = (byte)'B'; b[1] = (byte)'M'; BitConverter.GetBytes(tam).CopyTo(b, 2); BitConverter.GetBytes(54).CopyTo(b, 10);
            BitConverter.GetBytes(40).CopyTo(b, 14); BitConverter.GetBytes(im.W).CopyTo(b, 18); BitConverter.GetBytes(im.H).CopyTo(b, 22);
            b[26] = 1; b[28] = 24;
            for (int y = 0; y < im.H; y++)
                for (int x = 0; x < im.W; x++)
                {
                    uint v = im.Px[(im.H - 1 - y) * im.W + x]; int o = 54 + y * fila + x * 3;
                    b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16);
                }
            return b;
        }
        // un GIF de 2x2 o 3x3 con su paleta y, a veces, un color transparente
        private static byte[] Gif(ImagenD im)
        {
            List<byte> g = new List<byte>(Encoding.ASCII.GetBytes("GIF89a"));
            g.AddRange(new byte[] { 2, 0, 2, 0, 0x81, 0, 0 });   // 2x2, paleta global de 4 colores
            for (int i = 0; i < 4; i++) { uint v = im.Px[i % im.Px.Length]; g.Add((byte)(v >> 16)); g.Add((byte)(v >> 8)); g.Add((byte)v); }
            if ((im.W & 1) == 1) g.AddRange(new byte[] { 0x21, 0xF9, 4, 1, 0, 0, 2, 0 });   // el color 2 transparente
            g.AddRange(new byte[] { 0x2C, 0, 0, 0, 0, 2, 0, 2, 0, 0 });
            g.AddRange(new byte[] { 2, 3, 0x44, 0x34, 0x05, 0 });   // LZW de 2 bits: borrado, 0, 1, 2 (3 bits), 3 y fin (4 bits)
            g.Add(0x3B);
            return g.ToArray();
        }
    }
}

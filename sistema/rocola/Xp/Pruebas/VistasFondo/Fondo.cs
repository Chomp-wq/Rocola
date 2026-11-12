// ARNES DEL GRUPO «FONDO» (port nativo, tanda 3 del dibujo, 11-11-2026): lo que dibuja Form1.Fondo.cs con datos fijos, la huella de cada mapa
// y lo que devuelve cada llamada. Sobre todo el cielo del dibujo por procesador (Starfield: Draw, DrawCached, DrawReactive y el OnPaint de
// BgPanel), que los recorridos casi no pasan (solo «botones» y «video» lo pintan, y siempre con el mismo estado), y los hornos (el grano, la
// luna, los resplandores), el contorno redondeado (RoundedPath) y el color de las portadas (ColoresDePortada). Se compila con el programa de
// una carpeta Xp (VistasFondo.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales linea por linea.
//   - Las llamadas son las de XP (Graphics, Bitmap): en la referencia van a la capa congelada; en la traduccion, las del cielo, RoundedPath y
//     ColoresDePortada(Bitmap) van por el puente (Plataforma/Puente.fondo.cs) a SkiaSharp directo. En una segunda pasada («directa») la
//     traduccion se llama DIRECTO (el lienzo y su estado, el SKPath, el SKBitmap), como la llamaran los archivos que la usan cuando se
//     traduzcan; en la referencia esa pasada es la de XP otra vez.
//   - Lo que depende del reloj (Environment.TickCount) se fija por reflexion antes de cada llamada, igual en los dos programas: el azar de las
//     fugaces (rnd) con semilla, cuando se rehace el cuadro (lastBuild, RefrescoMs), el paso de las fugaces (lastTick, nextShoot), la deriva
//     (driftLastMs, driftX, driftY) y la capa del titileo (layers: las ocho entradas apuntan a la capa elegida mientras dura la llamada).
//   - El estado del Graphics del que llama va de ocho maneras (el de fabrica, suavizado, copia y medio pixel, recorte parcial, la base y el
//     recorte del HDC como en un OnPaint, escala, giro y traslacion con decimales).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet VistasFondo.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using Rocola.Ventanas;
using SkiaSharp;

namespace BancoFondo
{
    internal static class Fondo
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta;
        private static int n, directas;
        private static string R(float v) { return v.ToString("R", ci); }
        private static string R(RectangleF r) { return "(" + R(r.X) + "," + R(r.Y) + "," + R(r.Width) + "," + R(r.Height) + ")"; }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static string Exc(Exception x) { return "EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }
        private static string C(Color c) { return c.A + "." + c.R + "." + c.G + "." + c.B; }

        // los puntos de un mapa (un Bitmap de la capa o un SKBitmap de la traduccion): tal cual (premultiplicados) y como los sube la tarjeta
        private static unsafe string Puntos(string nombre, object o)
        {
            if (o == null) return "null";
            Bitmap b = o as Bitmap ?? new Bitmap((SKBitmap)o, PixelFormat.Format32bppPArgb);
            SKBitmap s = b.Sk; byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            BitmapData d = b.LockBits(new Rectangle(0, 0, s.Width, s.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] sub = new byte[s.Width * s.Height * 4];
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) sub[y * s.Width * 4 + x] = ((byte*)d.Scan0)[y * d.Stride + x];
            b.UnlockBits(d);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }
        private static void Suelta(object o) { IDisposable d = o as IDisposable; if (d != null) d.Dispose(); }
        private static string Mat(Matrix m) { PointF[] p = { new PointF(0, 0), new PointF(1, 1) }; m.TransformPoints(p); return R(p[0].X) + "," + R(p[0].Y) + ";" + R(p[1].X) + "," + R(p[1].Y); }

        // ---- el estado del Graphics del que llama
        private sealed class Estado { public string Nombre; public SmoothingMode S; public CompositingMode C; public PixelOffsetMode P; public InterpolationMode I; public int Trans; public int Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "fabrica", S = SmoothingMode.None, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, I = InterpolationMode.Bilinear },
            new Estado { Nombre = "suave", S = SmoothingMode.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, I = InterpolationMode.Bilinear },
            new Estado { Nombre = "hdc", S = SmoothingMode.None, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, I = InterpolationMode.Bilinear, Trans = 6, Recorte = 2 },
            new Estado { Nombre = "copia-medio-vecino", S = SmoothingMode.HighQuality, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Half, I = InterpolationMode.NearestNeighbor },
            new Estado { Nombre = "parcial", S = SmoothingMode.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, I = InterpolationMode.Bilinear, Recorte = 1 },
            new Estado { Nombre = "escala-bicubico", S = SmoothingMode.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.HighQuality, I = InterpolationMode.HighQualityBicubic, Trans = 2 },
            new Estado { Nombre = "gira", S = SmoothingMode.None, C = CompositingMode.SourceOver, P = PixelOffsetMode.None, I = InterpolationMode.Bilinear, Trans = 3 },
            new Estado { Nombre = "decimales-copia", S = SmoothingMode.AntiAlias, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Default, I = InterpolationMode.HighQualityBilinear, Trans = 5 },
        };
        private static MethodInfo sella = typeof(Graphics).GetMethod("SellaRecorte", Todo);
        private static Graphics Prepara(Bitmap b, Estado e, int w, int h)
        {
            Graphics g = Graphics.FromImage(b);
            g.Clear(Color.FromArgb(120, 30, 60, 90));
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(200, 250, 20, 60))) g.FillRectangle(sb, 0, b.Height / 3, b.Width, 9);
            g.SmoothingMode = e.S; g.CompositingMode = e.C; g.PixelOffsetMode = e.P; g.InterpolationMode = e.I;
            if (e.Trans == 2) g.ScaleTransform(1.3f, 1.3f); else if (e.Trans == 3) g.RotateTransform(7f);
            else if (e.Trans == 5) g.TranslateTransform(0.5f, 0.25f); else if (e.Trans == 6) g.TranslateTransform(7, 5);
            if (e.Recorte == 1) g.SetClip(new Rectangle(w / 4, h / 3, w / 3, h / 4));   // repintado parcial (la casilla del cubo)
            else if (e.Recorte == 2) { g.SetClip(new Rectangle(0, 0, w, h)); sella.Invoke(g, null); }   // como lo da Ventanas.Escritorio a un OnPaint
            return g;
        }
        private static string Deja(Graphics g) { return " deja=" + g.SmoothingMode + "/" + g.CompositingMode + "/" + g.PixelOffsetMode + "/" + g.InterpolationMode + " m=" + Mat(g.Transform) + " recorte=" + R(g.ClipBounds); }

        // ---- la traduccion, por reflexion
        private static Type Puente { get { return typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo"); } }
        private static bool Traducida { get { return Puente != null && Puente.GetMethod("Estado", Todo, null, new[] { typeof(Graphics) }, null) != null && Sf.GetMethod("Draw", Todo, null, new[] { typeof(SKCanvas), typeof(bool), FiltroT, typeof(bool), typeof(bool), typeof(int), typeof(int), typeof(Color), typeof(Color), typeof(Color), typeof(double), typeof(bool), typeof(float) }, null) != null; } }
        private static Type FiltroT { get { return typeof(Form1).Assembly.GetType("Rocola.Nativo.Filtro"); } }
        private static object Por(string metodo, Type tipo, object o) { return Puente.GetMethod(metodo, Todo, null, new[] { tipo }, null).Invoke(null, new[] { o }); }
        // el lienzo y su estado (lienzo, suave, filtro, medio, copia) de un Graphics: lo que un archivo traducido lleva en sus variables
        private static object[] EstadoDe(Graphics g)
        {
            object e = Por("Estado", typeof(Graphics), g); Type te = e.GetType();
            return new[] { Por("Lienzo", typeof(Graphics), g), te.GetField("Suave").GetValue(e), te.GetField("Filtro").GetValue(e), te.GetField("Medio").GetValue(e), te.GetField("Copia").GetValue(e) };
        }
        private static MethodInfo Metodo(Type t, string nombre, params Type[] tipos) { return t.GetMethod(nombre, Todo, null, tipos, null); }

        // ================================================================ LOS HORNOS
        private static void Hornos()
        {
            object o = null; string ex = "";
            try { o = typeof(Form1).GetMethod("BakeRuido", Todo).Invoke(null, null); } catch (Exception x) { ex = " " + Exc(x); }
            Linea("ruido", Puntos("ruido", o) + ex); Suelta(o);
            foreach (int lado in new[] { 64, 100, 257, 512 })
            {
                o = null; ex = "";
                try { o = typeof(Form1).GetMethod("BakeRuidoGrande", Todo).Invoke(null, new object[] { lado }); } catch (Exception x) { ex = " " + Exc(x); }
                Linea("ruido-grande-" + lado, Puntos("ruido-grande-" + lado, o) + ex); Suelta(o);
            }
            foreach (int alto in new[] { 16, 48, 64, 100, 437, 480, 600, 768, 1024, 1080, 1440 })
            {
                o = null; ex = "";
                try { o = typeof(Form1).GetMethod("BakeLuna", Todo).Invoke(null, new object[] { alto }); } catch (Exception x) { ex = " " + Exc(x); }
                Linea("luna-" + alto, Puntos("luna-" + alto, o) + ex); Suelta(o);
            }
            MethodInfo glow = Sf.GetMethod("BakeGlow", Todo);
            int k = 0;
            foreach (int size in new[] { 3, 8, 16, 33, 64 })
                foreach (int pico in new[] { 0, 60, 255 })
                    foreach (Color c in new[] { Color.White, Color.FromArgb(255, 180, 110), Color.FromArgb(128, 150, 235, 255) })
                    {
                        string nombre = "resplandor-" + (k++);
                        o = null; ex = "";
                        try { o = glow.Invoke(null, new object[] { size, pico, c }); } catch (Exception x) { ex = " " + Exc(x); }
                        Linea(nombre, Puntos(nombre, o) + ex); Suelta(o);
                    }
            // la paleta de resplandores compartida (EnsureGlow la hornea una vez)
            Sf.GetMethod("EnsureGlow", Todo).Invoke(null, null);
            Array pal = (Array)Sf.GetField("glowPal", Todo).GetValue(null);
            for (int i = 0; i < pal.Length; i++) Linea("resplandor-paleta-" + i, Puntos("resplandor-paleta-" + i, pal.GetValue(i)));
        }

        // ================================================================ EL CONTORNO REDONDEADO (RoundedPath)
        // la firma de XP de RoundedPath (un GraphicsPath) solo esta en la referencia: la traduccion la quito el 12-11-2026 (limpieza-puentes, nadie
        // la llamaba) y aqui se hace lo que hacia el puente: un GraphicsPath de la capa que se hace dueno del SKPath de ContornoRedondo
        private static GraphicsPath ComoRoundedPath(SKPath p) { GraphicsPath gp = new GraphicsPath(); gp.P.Dispose(); gp.P = p; return gp; }
        private static void Contornos()
        {
            MethodInfo rp = Metodo(typeof(Form1), "RoundedPath", typeof(Rectangle), typeof(int));
            MethodInfo cr = Metodo(typeof(Form1), "ContornoRedondo", typeof(Rectangle), typeof(int));
            Rectangle[] rs = { new Rectangle(4, 3, 40, 18), new Rectangle(2, 2, 60, 60), new Rectangle(10, 5, 3, 30), new Rectangle(0, 0, 1, 1), new Rectangle(5, 6, 50, 7), new Rectangle(-3, 2, 70, 25), new Rectangle(3, 4, 0, 9) };
            int[] radios = { -2, 0, 1, 2, 3, 7, 14, 40 };
            int k = 0;
            foreach (bool directa in new[] { false, true })
                foreach (Estado e in Estados)
                    foreach (Rectangle r in rs)
                        foreach (int rad in radios)
                        {
                            string nombre = "contorno-" + (directa ? "directa-" : "") + e.Nombre + "-" + (k++);
                            using (Bitmap b = new Bitmap(80, 72, PixelFormat.Format32bppPArgb))
                            {
                                string ex = "", deja = "", info = "";
                                using (Graphics g = Prepara(b, e, 80, 72))
                                using (SolidBrush sb = new SolidBrush(Color.FromArgb(170, 240, 200, 40)))
                                using (Pen pn = new Pen(Color.FromArgb(230, 20, 230, 250), 1.6f))
                                {
                                    try
                                    {
                                        if (directa && cr != null)
                                        {
                                            directas++;
                                            using (SKPath p = (SKPath)cr.Invoke(null, new object[] { r, rad }))
                                            {
                                                object[] st = EstadoDe(g);
                                                Type px = typeof(Form1).Assembly.GetType("Rocola.Nativo.Pixel");
                                                using (SKPaint relleno = new SKPaint { Color = new SKColor(240, 200, 40, 170) })
                                                using (SKPaint pluma = new SKPaint { Color = new SKColor(20, 230, 250, 230), StrokeWidth = 1.6f })
                                                {
                                                    Metodo(px, "Rellena", typeof(SKCanvas), typeof(SKPath), typeof(SKPaint), typeof(bool), typeof(bool), typeof(bool)).Invoke(null, new object[] { st[0], p, relleno, st[1], st[3], st[4] });
                                                    Metodo(px, "Traza", typeof(SKCanvas), typeof(SKPath), typeof(SKPaint), typeof(bool), typeof(bool), typeof(bool)).Invoke(null, new object[] { st[0], p, pluma, st[1], st[3], st[4] });
                                                }
                                                info = " puntos=" + p.PointCount + " tipo=" + p.FillType + " caja=" + R(new RectangleF(p.Bounds.Left, p.Bounds.Top, p.Bounds.Width, p.Bounds.Height));
                                            }
                                        }
                                        else
                                            using (GraphicsPath p = rp != null ? (GraphicsPath)rp.Invoke(null, new object[] { r, rad }) : ComoRoundedPath((SKPath)cr.Invoke(null, new object[] { r, rad })))
                                            {
                                                g.FillPath(sb, p); g.DrawPath(pn, p);
                                                RectangleF caja = p.GetBounds();
                                                info = " puntos=" + p.PointCount + " tipo=" + p.FillMode + " caja=" + R(caja);
                                                info = info.Replace("Alternate", "EvenOdd");   // el mismo relleno con el nombre de cada lado
                                            }
                                    }
                                    catch (Exception x) { ex = " " + Exc(x); }
                                    deja = Deja(g);
                                }
                                Linea(nombre, Puntos(nombre, b) + info + deja + ex);
                            }
                        }
        }

        // ================================================================ EL COLOR DE LAS PORTADAS (ColoresDePortada, ColorDePortada)
        private static Bitmap Portada(int w, int h, int clase)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            Random r = new Random(1000 + clase * 37 + w * 3 + h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    if (clase == 0) c = Color.FromArgb(255, (x * 255) / Math.Max(1, w - 1), (y * 255) / Math.Max(1, h - 1), 90);              // degradado
                    else if (clase == 1) c = (x < w / 2) ? Color.FromArgb(255, 220, 30, 40) : Color.FromArgb(255, 30, 200, 60);               // dos colores
                    else if (clase == 2) c = Color.FromArgb(r.Next(256), r.Next(256), r.Next(256), r.Next(256));                              // ruido con alfa
                    else if (clase == 3) { int v = r.Next(40); c = Color.FromArgb(255, 100 + v, 100 + v, 100 + v); }                         // gris
                    else if (clase == 4) c = Color.FromArgb(255, r.Next(30), r.Next(30), r.Next(30));                                         // negro
                    else c = Color.FromArgb(r.Next(2) == 0 ? 0 : 255, 40 + r.Next(60), 60 + r.Next(80), 200 + r.Next(55));                   // azul con huecos
                    b.SetPixel(x, y, c);
                }
            return b;
        }
        private static void Portadas()
        {
            MethodInfo xp = Metodo(typeof(Form1), "ColoresDePortada", typeof(Bitmap), typeof(Color).MakeByRefType(), typeof(Color).MakeByRefType());
            MethodInfo sk = Metodo(typeof(Form1), "ColoresDePortada", typeof(SKBitmap), typeof(Color).MakeByRefType(), typeof(Color).MakeByRefType());
            MethodInfo uno = Metodo(typeof(Form1), "ColorDePortada", typeof(Bitmap)) ?? Metodo(typeof(Form1), "ColorDePortada", typeof(SKBitmap));
            int[][] medidas = { new[] { 1, 1 }, new[] { 7, 5 }, new[] { 32, 32 }, new[] { 33, 31 }, new[] { 100, 100 }, new[] { 300, 297 }, new[] { 640, 64 } };
            for (int clase = 0; clase < 6; clase++)
                foreach (int[] m in medidas)
                    using (Bitmap b = Portada(m[0], m[1], clase))
                    {
                        string nombre = "portada-" + clase + "-" + m[0] + "x" + m[1];
                        object[] a = { b, null, null };
                        string r1 = C((Color)xp.Invoke(null, a)) + " " + C((Color)a[1]) + " " + C((Color)a[2]);
                        string r2 = r1;
                        if (sk != null)
                        {
                            directas++;
                            object[] a2 = { Por("Mapa", typeof(Image), b), null, null };
                            r2 = C((Color)sk.Invoke(null, a2)) + " " + C((Color)a2[1]) + " " + C((Color)a2[2]);
                        }
                        object arg = uno.GetParameters()[0].ParameterType == typeof(Bitmap) ? (object)b : Por("Mapa", typeof(Image), b);
                        string r3 = C((Color)uno.Invoke(null, new[] { arg }));
                        Linea(nombre, r1 + " | " + r2 + " | " + r3);
                    }
            // sin portada y una ya soltada
            object[] nada = { null, null, null };
            Linea("portada-null", C((Color)xp.Invoke(null, nada)) + " " + C((Color)nada[1]) + " " + C((Color)nada[2]));
            Bitmap suelta = Portada(20, 20, 1); suelta.Dispose();
            string exs = "";
            try { object[] s = { suelta, null, null }; xp.Invoke(null, s); exs = "sin excepcion"; } catch (Exception x) { exs = Exc(x); }
            Linea("portada-soltada", exs);
        }

        // ================================================================ EL CIELO DEL DIBUJO POR PROCESADOR (Starfield)
        private static Type Sf { get { return typeof(Form1).GetNestedType("Starfield", BindingFlags.NonPublic); } }
        private static void Pon(object sf, string campo, object v) { FieldInfo f = Sf.GetField(campo, Todo); f.SetValue(sf, v); }
        private static object Lee(object sf, string campo) { return Sf.GetField(campo, Todo).GetValue(sf); }
        private static object Nuevo(int semilla)
        {
            object sf = Activator.CreateInstance(Sf, true);
            Pon(sf, "rnd", new Random(semilla));
            return sf;
        }
        // lo que depende del reloj, fijado justo antes de llamar: rehacer el cuadro (o no), el paso de las fugaces (0 o el tope), si sale una
        // fugaz nueva, la deriva sin paso (o con el tope)
        private static void Reloj(object sf, bool rehace, bool pasoFugaz, bool nuevaFugaz, bool pasoDeriva)
        {
            int ahora = Environment.TickCount;
            Pon(sf, "RefrescoMs", rehace ? 40 : int.MaxValue);
            Pon(sf, "lastBuild", rehace ? ahora - 100000 : ahora - 1);
            Pon(sf, "lastTick", pasoFugaz ? ahora - 100000 : 0);
            Pon(sf, "nextShoot", nuevaFugaz ? ahora - 1 : ahora + 100000000);
            Pon(sf, "driftLastMs", pasoDeriva ? ahora - 100000 : 0);
        }
        // la capa del titileo: las ocho entradas apuntan a la capa k mientras dura la llamada
        private static Array capasDe;
        private static void FijaCapa(object sf, int k)
        {
            Array a = (Array)Lee(sf, "layers");
            if (a == null) return;
            Array c = Array.CreateInstance(a.GetType().GetElementType(), a.Length);
            for (int i = 0; i < a.Length; i++) c.SetValue(a.GetValue(k % a.Length), i);
            Pon(sf, "layers", c); capasDe = a;
        }
        private static void SueltaCapa(object sf) { if (capasDe != null) Pon(sf, "layers", capasDe); capasDe = null; }
        private static string Fugaces(object sf)
        {
            Array s = (Array)Lee(sf, "shoots"); StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                object e = s.GetValue(i); Type t = e.GetType();
                if (!(bool)t.GetField("On").GetValue(e)) { sb.Append(" -"); continue; }
                sb.Append(" " + R((float)t.GetField("X").GetValue(e)) + "," + R((float)t.GetField("Y").GetValue(e)) + "," + R((float)t.GetField("Life").GetValue(e)));
            }
            return sb.ToString();
        }

        private sealed class Cielo { public string Nombre; public int W, H, FullW, FullH, OffX, OffY; public bool Varied, Quiet, Luna = true; public float Density = 1f, Dim = 1f, Fase, Corte = -1; public Color Mid = Color.Empty, Acento = Color.Empty, Neb1 = Color.Empty, Neb2 = Color.Empty; }
        private static readonly Cielo[] Cielos = {
            new Cielo { Nombre = "chico", W = 64, H = 48 },
            new Cielo { Nombre = "atraccion", W = 320, H = 240, Varied = true, Density = 1.4f, Fase = 0.7f },
            new Cielo { Nombre = "franjas", W = 400, H = 300, Quiet = true, Density = 0.6f, Mid = Color.FromArgb(255, 10, 20, 70), Acento = Color.FromArgb(255, 200, 60, 90) },
            new Cielo { Nombre = "ventana", W = 300, H = 180, FullW = 512, FullH = 384, OffX = 100, OffY = 120, Dim = 0.45f, Neb1 = Color.FromArgb(255, 90, 40, 160), Neb2 = Color.FromArgb(255, 30, 140, 120), Fase = 2.1f },
            new Cielo { Nombre = "sin-luna-corte", W = 256, H = 200, Luna = false, Corte = 0.62f, Acento = Color.FromArgb(255, 40, 120, 220) },
            new Cielo { Nombre = "grande", W = 1024, H = 768, Mid = Color.FromArgb(255, 8, 16, 52), Acento = Color.FromArgb(255, 250, 150, 40), Fase = 4.4f },
        };
        private static float corteFabrica = float.NaN;
        private static void Configura(object sf, Cielo c)
        {
            FieldInfo corte = typeof(Form1).GetField("cieloCorte", Todo);
            if (float.IsNaN(corteFabrica)) corteFabrica = (float)corte.GetValue(null);
            corte.SetValue(null, c.Corte >= 0 ? c.Corte : corteFabrica);
            Pon(sf, "Varied", c.Varied); Pon(sf, "QuietBands", c.Quiet); Pon(sf, "Density", c.Density); Pon(sf, "Dim", c.Dim);
            Pon(sf, "SkyFullW", c.FullW); Pon(sf, "SkyFullH", c.FullH); Pon(sf, "SkyOffsetX", c.OffX); Pon(sf, "SkyOffsetY", c.OffY);
            Pon(sf, "SkyMid", c.Mid); Pon(sf, "Acento", c.Acento); Pon(sf, "NebUno", c.Neb1); Pon(sf, "NebDos", c.Neb2);
            Pon(sf, "HazFase", c.Fase); Pon(sf, "ConLuna", c.Luna);
        }
        // las cachés internas del cielo, por si una llamada sale distinta: donde empieza a separarse
        private static string Caches(string nombre, object sf)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string campo in new[] { "cache", "skyCache", "hazeMask", "nebMask", "hazeFull", "luna", "lunaTenida", "capaAtenuada" })
            {
                object o = Lee(sf, campo);
                sb.Append(" " + campo + "=" + (o == null ? "null" : Puntos(nombre + "-" + campo, o).Split('\t')[1]));
            }
            Array l = (Array)Lee(sf, "layers");
            if (l != null) for (int i = 0; i < l.Length; i++) sb.Append(" capa" + i + "=" + Puntos(nombre + "-capa" + i, l.GetValue(i)).Split('\t')[1]);
            return sb.ToString();
        }

        private enum Que { Draw, Cached, Reactive }
        // una llamada del cielo con el Graphics de un estado; directa: en la traduccion, al SKCanvas con el estado en variables
        private static void Llama(object sf, Que que, Graphics g, bool directa, int w, int h, Color top, Color bot, Color tint, double fase, bool fugaces, float pulso, float lo, float mid, float hi)
        {
            if (directa && Traducida)
            {
                directas++;
                object[] st = EstadoDe(g);
                if (que == Que.Draw)
                    Metodo(Sf, "Draw", typeof(SKCanvas), typeof(bool), FiltroT, typeof(bool), typeof(bool), typeof(int), typeof(int), typeof(Color), typeof(Color), typeof(Color), typeof(double), typeof(bool), typeof(float))
                        .Invoke(sf, new object[] { st[0], st[1], st[2], st[3], st[4], w, h, top, bot, tint, fase, fugaces, pulso });
                else if (que == Que.Cached)
                    Metodo(Sf, "DrawCached", typeof(SKCanvas), FiltroT, typeof(bool), typeof(bool), typeof(int), typeof(int), typeof(Color), typeof(Color))
                        .Invoke(sf, new object[] { st[0], st[2], st[3], st[4], w, h, top, bot });
                else
                    Metodo(Sf, "DrawReactive", typeof(SKCanvas), typeof(bool), FiltroT, typeof(bool), typeof(bool), typeof(int), typeof(int), typeof(Color), typeof(Color), typeof(float), typeof(float), typeof(float))
                        .Invoke(sf, new object[] { st[0], st[1], st[2], st[3], st[4], w, h, top, bot, lo, mid, hi });
                return;
            }
            if (que == Que.Draw)
                Metodo(Sf, "Draw", typeof(Graphics), typeof(int), typeof(int), typeof(Color), typeof(Color), typeof(Color), typeof(double), typeof(bool), typeof(float))
                    .Invoke(sf, new object[] { g, w, h, top, bot, tint, fase, fugaces, pulso });
            else if (que == Que.Cached)
                Metodo(Sf, "DrawCached", typeof(Graphics), typeof(int), typeof(int), typeof(Color), typeof(Color)).Invoke(sf, new object[] { g, w, h, top, bot });
            else
                Metodo(Sf, "DrawReactive", typeof(Graphics), typeof(int), typeof(int), typeof(Color), typeof(Color), typeof(float), typeof(float), typeof(float))
                    .Invoke(sf, new object[] { g, w, h, top, bot, lo, mid, hi });
        }

        private static readonly Color[][] Colores = {
            new[] { Color.FromArgb(255, 2, 4, 14), Color.FromArgb(255, 40, 30, 90), Color.FromArgb(255, 255, 220, 160) },
            new[] { Color.FromArgb(255, 0, 0, 0), Color.FromArgb(255, 120, 20, 60), Color.FromArgb(255, 160, 255, 200) },
            new[] { Color.FromArgb(255, 10, 30, 60), Color.FromArgb(200, 70, 90, 30), Color.FromArgb(255, 255, 255, 255) },
        };
        private static void Cielos_()
        {
            foreach (bool directa in new[] { false, true })
                foreach (Cielo c in Cielos)
                    foreach (Que que in new[] { Que.Draw, Que.Cached, Que.Reactive })
                    {
                        object sf = Nuevo(4242 + c.W);
                        Configura(sf, c);
                        int bw = c.W + 16, bh = c.H + 12;
                        // calentar: arma el patron, las capas y el cielo horneado a esta medida (lo que sale no se mira)
                        using (Bitmap b = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb))
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            Reloj(sf, true, false, false, false);
                            Llama(sf, que, g, false, c.W, c.H, Colores[0][0], Colores[0][1], Colores[0][2], 0.0, false, 0f, 0f, 0f, 0f);
                        }
                        int k = 0;
                        foreach (Estado e in Estados)
                            for (int v = 0; v < 6; v++)
                            {
                                Color[] col = Colores[v % Colores.Length];
                                string nombre = "cielo-" + (directa ? "directa-" : "") + que + "-" + c.Nombre + "-" + e.Nombre + "-" + v + "-" + (k++);
                                bool rehace = v != 3, pasoFugaz = v >= 4, nueva = v == 2 || v == 5, pasoDeriva = v == 4;
                                bool fug = v >= 2;
                                float pulso = (v == 1 || v == 4) ? 0.6f : (v == 5 ? 0.03f : 0f);
                                float lo = (v == 1) ? 0.9f : (v == 4 ? 0.3f : 0f), mid = (v == 4) ? 0.5f : (v == 5 ? 0.05f : 0f), hi = (v >= 4) ? 0.7f : 0f;
                                if (v == 4) { Pon(sf, "driftX", 37.5f); Pon(sf, "driftY", 211.25f); Pon(sf, "driftEnv", 0.8f); }
                                using (Bitmap b = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb))
                                {
                                    string ex = "", deja = "";
                                    using (Graphics g = Prepara(b, e, c.W, c.H))
                                    {
                                        Reloj(sf, rehace, pasoFugaz, nueva, pasoDeriva);
                                        FijaCapa(sf, v * 3 + k);
                                        try { Llama(sf, que, g, directa, c.W, c.H, col[0], col[2], col[1], 0.37 * v + 0.11 * k, fug, pulso, lo, mid, hi); }
                                        catch (Exception xx) { ex = " " + Exc(xx); }
                                        SueltaCapa(sf);
                                        deja = Deja(g);
                                    }
                                    string extra = "";
                                    if (e.Nombre == "fabrica" && v == 5) extra = Caches(nombre, sf);
                                    Linea(nombre, Puntos(nombre, b) + deja + " fugaces=" + Fugaces(sf) + " deriva=" + R((float)Lee(sf, "driftX")) + "," + R((float)Lee(sf, "driftY")) + extra + ex);
                                }
                            }
                        Suelta(Lee(sf, "cache"));
                    }
        }

        // ================================================================ EL PANEL DEL DISCO (BgPanel.OnPaint de XP; Pinta en la traduccion)
        // (panel-del-disco) BgPanel ya no es un Control: el sitio se lo da SetBounds (de su CajaVista) y se pinta con Pinta(SKCanvas), el
        // lienzo del mismo Graphics; la referencia sigue siendo el Panel de WinForms con su OnPaint. Las dos pintan en el mismo Graphics.
        private static void Panel_()
        {
            Type t = typeof(Form1).GetNestedType("BgPanel", BindingFlags.NonPublic);
            MethodInfo onPaint = t.GetMethod("OnPaint", Todo);
            MethodInfo pinta = t.GetMethod("Pinta", Todo, null, new[] { typeof(SKCanvas) }, null);   // la traducida (sin OnPaint)
            MethodInfo setBounds = t.GetMethod("SetBounds", Todo, null, new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null);
            int k = 0;
            foreach (int[] m in new[] { new[] { 200, 150, 0, 0 }, new[] { 300, 220, 40, 60 }, new[] { 640, 400, 12, 90 } })
                foreach (float dim in new[] { 1f, 0.45f })
                    foreach (Color acento in new[] { Color.FromArgb(255, 200, 60, 90), Color.FromArgb(255, 30, 140, 220) })
                        foreach (bool recorte in new[] { false, true })
                        {
                            string nombre = "panel-" + m[0] + "x" + m[1] + "-" + R(dim) + "-" + (k++);
                            object sf = Nuevo(77);
                            object c = Activator.CreateInstance(t, true);
                            setBounds.Invoke(c, new object[] { m[2], m[3], m[0], m[1] });
                            t.GetField("Sky", Todo).SetValue(c, sf); t.GetField("Accent", Todo).SetValue(c, acento); t.GetField("BgDim", Todo).SetValue(c, dim);
                            using (Bitmap b = new Bitmap(m[0] + 20, m[1] + 14, PixelFormat.Format32bppPArgb))
                            {
                                string ex = "", deja = "";
                                // calentar (las capas, a la medida) y despues la pintura que se mira, con la capa fija
                                for (int pasada = 0; pasada < 2; pasada++)
                                    using (Graphics g = Graphics.FromImage(b))
                                    {
                                        g.Clear(Color.FromArgb(120, 30, 60, 90));
                                        g.TranslateTransform(9, 5);
                                        if (recorte) g.SetClip(new Rectangle(0, 0, m[0] - 30, m[1] - 20)); else g.SetClip(new Rectangle(0, 0, m[0], m[1]));
                                        sella.Invoke(g, null);
                                        Reloj(sf, true, false, false, false);
                                        if (pasada == 1) FijaCapa(sf, k);
                                        try
                                        {
                                            if (pinta != null) pinta.Invoke(c, new object[] { Por("Lienzo", typeof(Graphics), g) });
                                            else using (PaintEventArgs pe = new PaintEventArgs(g, new Rectangle(0, 0, m[0], m[1]))) onPaint.Invoke(c, new object[] { pe });
                                        }
                                        catch (Exception xx) { ex = " " + Exc(xx); }
                                        SueltaCapa(sf);
                                        deja = Deja(g);
                                    }
                                Linea(nombre, Puntos(nombre, b) + deja + ex);
                            }
                            Suelta(c);
                        }
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            Linea("inicio", "traducida=" + Traducida);
            Hornos();
            Contornos();
            Portadas();
            Cielos_();
            Panel_();
            Console.Error.WriteLine("fondo: " + n + " lineas, " + directas + " llamadas directas a la traducida");
            return 0;
        }
    }
}

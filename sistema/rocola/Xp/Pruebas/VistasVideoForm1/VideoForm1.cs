// ARNES DEL GRUPO «VIDEO-FORM1» (port nativo, tanda 3 del dibujo, 11-11-2026): lo que dibujan Form1.Video.cs (DrawLabelOverlay, las etiquetas en la
// imagen de la interfaz; DrawCtrlOverlay, las vistas en ella; EnSeco, el precalentado; PartirNow) y Form1.cs (la barra de ayuda, HelpBar: su
// dibujo con tarjeta y por procesador, y su ancho) con datos fijos, la huella de cada mapa y lo que devuelve cada llamada. Se compila con el
// programa de una carpeta Xp (VistasVideoForm1.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales
// linea por linea.
//   - Las llamadas de la referencia son las de XP (Graphics, Font). En la traduccion, las que ya no tienen la firma de XP (DrawLabelOverlay,
//     DrawCtrlOverlay, PartirNow) se llaman como las llama la traduccion: con el lienzo y el estado del Graphics del que llama en variables
//     (lo que da PuenteDibujo.Estado de ese mismo Graphics), y una Letra en vez de la Font; las que la conservan en el puente (HelpBar con un
//     Graphics, para su OnPaint; EnSeco con un Action<Graphics>, para el precalentado) por esa firma, y la barra de ayuda tambien DIRECTO.
//   - El estado del Graphics del que llama va de ocho maneras (suavizado, los modos de letra, copia, medio pixel, traslacion entera y con
//     decimales, escala, giro y recorte); despues de cada llamada se apunta el estado que le queda al Graphics (en la traduccion no se toca:
//     tiene que ser el mismo que deja el Restore de XP).
//   - El texto de GDI (TextRenderer) de las vistas sin traducir por el Graphics del puente (GraphicsVideoForm1): la matriz del HDC.
//   - Lo privado va por reflexion (la referencia no tiene PuenteDibujo ni Rocola.Nativo: tampoco se nombran aqui).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasVideoForm1.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using Rocola.Ventanas;
using SkiaSharp;

namespace BancoVideoForm1
{
    internal static class VideoForm1
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

        // los puntos de un mapa: tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb: sin premultiplicar)
        private static unsafe string Puntos(string nombre, Bitmap b)
        {
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
        private static string Mat(Matrix m) { PointF[] p = { new PointF(0, 0), new PointF(1, 1) }; m.TransformPoints(p); return R(p[0].X) + "," + R(p[0].Y) + ";" + R(p[1].X) + "," + R(p[1].Y); }
        // el estado que le queda al Graphics (lo que XP deja puesto despues de una llamada)
        private static string EstadoG(Graphics g)
        {
            return "s=" + g.SmoothingMode + " t=" + g.TextRenderingHint + " c=" + g.CompositingMode + " i=" + g.InterpolationMode + " p=" + g.PixelOffsetMode
                + " m=" + Mat(g.Transform) + " r=" + R(g.ClipBounds);
        }

        // ---- el estado del Graphics del que llama (los ocho de Pruebas/VistasReposo y VistasAsistente)
        private sealed class Estado { public string Nombre; public SmoothingMode S; public TextRenderingHint T; public CompositingMode C; public PixelOffsetMode P; public int Trans; public bool Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "xp", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "sin-suavizar", S = SmoothingMode.None, T = TextRenderingHint.SystemDefault, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "copia-medio-traslada", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala", S = SmoothingMode.AntiAlias, T = TextRenderingHint.ClearTypeGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.HighQuality, Trans = 2 },
            new Estado { Nombre = "unbit-gira", S = SmoothingMode.AntiAlias, T = TextRenderingHint.SingleBitPerPixelGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = SmoothingMode.None, T = TextRenderingHint.SingleBitPerPixel, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Default, Recorte = true },
            new Estado { Nombre = "traslada-recorte", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 4, Recorte = true },
            new Estado { Nombre = "suave-sin-ajuste", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.None, Trans = 5 },
        };
        private static Graphics Prepara(Bitmap b, Estado e)
        {
            Graphics g = Graphics.FromImage(b);
            g.Clear(Color.FromArgb(120, 30, 60, 90));
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(200, 250, 20, 60))) g.FillRectangle(sb, 0, 25, b.Width, 9);
            g.SmoothingMode = e.S; g.TextRenderingHint = e.T; g.CompositingMode = e.C; g.PixelOffsetMode = e.P;
            if (e.Trans == 1) g.TranslateTransform(3.5f, 2.25f); else if (e.Trans == 2) g.ScaleTransform(1.3f, 1.3f); else if (e.Trans == 3) g.RotateTransform(7f);
            else if (e.Trans == 4) g.TranslateTransform(0, -12); else if (e.Trans == 5) g.TranslateTransform(0.5f, 0f);
            if (e.Recorte) g.SetClip(new RectangleF(10.5f, 6, 220, 50));
            return g;
        }

        // ---- la traduccion, por reflexion
        private static MethodInfo Metodo(Type t, string nombre, Type primero, int parametros = -1)
        {
            foreach (MethodInfo m in t.GetMethods(Todo))
                if (m.Name == nombre && m.GetParameters().Length > 0 && (primero == null || m.GetParameters()[0].ParameterType == primero) && (parametros < 0 || m.GetParameters().Length == parametros)) return m;
            return null;
        }
        private static Type Puente { get { return typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo"); } }
        private static object Por(string metodo, Type tipo, object o) { return Puente.GetMethod(metodo, Todo, null, new[] { tipo }, null).Invoke(null, new[] { o }); }
        // el lienzo y el estado de un Graphics (lienzo, suave, letra, copia, filtro, medio), como los lleva en sus variables un archivo traducido
        private static object[] EstadoDe(Graphics g)
        {
            object e = Por("Estado", typeof(Graphics), g); Type te = e.GetType();
            return new[] { Por("Lienzo", typeof(Graphics), g), te.GetField("Suave").GetValue(e), te.GetField("Letra").GetValue(e), te.GetField("Copia").GetValue(e), te.GetField("Filtro").GetValue(e), te.GetField("Medio").GetValue(e) };
        }
        private static object[] Junta(object[] a, params object[] b) { object[] r = new object[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }
        private static Type Anidado(string nombre) { return typeof(Form1).GetNestedType(nombre, Todo); }
        private static void Pon(object o, string campo, object v)
        {
            Type t = o.GetType();
            while (t != null)
            {
                FieldInfo f = t.GetField(campo, Todo | BindingFlags.DeclaredOnly); if (f != null) { f.SetValue(o, v); return; }
                PropertyInfo p = t.GetProperty(campo, Todo | BindingFlags.DeclaredOnly); if (p != null && p.CanWrite) { p.SetValue(o, v); return; }
                t = t.BaseType;
            }
            throw new Exception("no hay " + campo);
        }

        // ================================================================ LAS ETIQUETAS (DrawLabelOverlay)
        private static void Etiquetas()
        {
            MethodInfo trad4 = Metodo(typeof(Form1), "DrawLabelOverlay", typeof(SKCanvas), 7), trad2 = Metodo(typeof(Form1), "DrawLabelOverlay", typeof(SKCanvas), 5);
            MethodInfo xp4 = Metodo(typeof(Form1), "DrawLabelOverlay", typeof(Graphics), 4), xp2 = Metodo(typeof(Form1), "DrawLabelOverlay", typeof(Graphics), 2);
            bool trad = trad4 != null;
            ContentAlignment[] alin = { ContentAlignment.TopLeft, ContentAlignment.TopCenter, ContentAlignment.TopRight, ContentAlignment.MiddleLeft, ContentAlignment.MiddleCenter,
                ContentAlignment.MiddleRight, ContentAlignment.BottomLeft, ContentAlignment.BottomCenter, ContentAlignment.BottomRight };
            string[] textos = { "Reproduciendo: Canción de prueba", "Ángel Ñandú y la orquesta que no cabe nunca en su renglón", "0:07 / 2:30", "", "A&B — «x»" };
            Font[] letras = { new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold), new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold, GraphicsUnit.Pixel), new Font("Microsoft Sans Serif", 11.5f, FontStyle.Bold) };
            int k = 0;
            foreach (Estado e in Estados)
                for (int a = 0; a < alin.Length; a++)
                    for (int t = 0; t < textos.Length; t++)
                    {
                        if ((a + t) % 2 == 1 && t != 1) continue;   // (no todas las combinaciones: unas 30 por estado)
                        Label l = new Label(); l.Text = textos[t]; l.Font = letras[(a + t) % letras.Length]; l.ForeColor = Color.FromArgb(255, 255, 220, 120); l.TextAlign = alin[a];
                        l.Bounds = new Rectangle(6 + a, 4 + t, 150 + 9 * t, 22 + a);
                        int caso = k++ % 4;   // 0: la etiqueta (2 argumentos); 1: con color; 2: caja angosta; 3: caja sin ancho
                        Color color = caso == 0 ? l.ForeColor : Color.FromArgb(200, 120, 230, 255);
                        RectangleF caja = caso == 2 ? new RectangleF(l.Left + 0.5f, l.Top, 61.25f, l.Height) : caso == 3 ? new RectangleF(l.Left, l.Top, 0f, l.Height) : (RectangleF)l.Bounds;
                        string nombre = "etiqueta-" + e.Nombre + "-" + a + "-" + t + "-" + caso;
                        using (Bitmap b = new Bitmap(240, 64, PixelFormat.Format32bppPArgb))
                        {
                            string ex = "", st;
                            using (Graphics g = Prepara(b, e))
                            {
                                try
                                {
                                    if (trad)
                                    {
                                        object[] s = EstadoDe(g);   // (lienzo, suave, letra, copia, filtro, medio): DrawLabelOverlay pide el lienzo, la letra, la copia y el medio
                                        if (caso == 0) trad2.Invoke(null, new[] { s[0], s[2], s[3], s[5], l });
                                        else trad4.Invoke(null, new[] { s[0], s[2], s[3], s[5], l, color, caja });
                                    }
                                    else if (caso == 0) xp2.Invoke(null, new object[] { g, l });
                                    else xp4.Invoke(null, new object[] { g, l, color, caja });
                                }
                                catch (Exception x) { ex = " " + Exc(x); }
                                st = EstadoG(g);
                            }
                            Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                        }
                    }
            foreach (Font f in letras) f.Dispose();
        }

        // ================================================================ LAS VISTAS EN LA IMAGEN DE LA INTERFAZ (DrawCtrlOverlay)
        private static Control Vista(int cual, int w, int h)
        {
            Control c;
            switch (cual)
            {
                case 0: c = (Control)Activator.CreateInstance(Anidado("HelpBar"), true); break;
                case 1: c = (Control)Activator.CreateInstance(Anidado("HelpBar"), true); Pon(c, "Justa", true); break;
                case 2: c = (Control)Activator.CreateInstance(Anidado("ProgressView"), true); Pon(c, "Value", 0.37); Pon(c, "Accent", Color.FromArgb(240, 120, 40)); break;
                case 3: c = (Control)Activator.CreateInstance(Anidado("ProgressView"), true); Pon(c, "Value", 0.81); Pon(c, "Neon", true); break;
                case 4: c = (Control)Activator.CreateInstance(Anidado("GeneroView"), true); Pon(c, "Texto", "Cumbia"); Pon(c, "Tinte", Color.FromArgb(60, 200, 120)); break;
                case 5: c = (Control)Activator.CreateInstance(Anidado("LetterBar"), true); break;
                default: c = (Control)Activator.CreateInstance(Anidado("PastillaView"), true); Pon(c, "Etiqueta", "CRÉDITOS"); Pon(c, "Valor", "12"); break;
            }
            c.Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold);
            c.Bounds = new Rectangle(7 + cual, 3 + cual % 3, w, h);
            return c;
        }
        private static void Vistas()
        {
            MethodInfo trad = Metodo(typeof(Form1), "DrawCtrlOverlay", typeof(SKCanvas)), xp = Metodo(typeof(Form1), "DrawCtrlOverlay", typeof(Graphics));
            int[][] medidas = { new[] { 300, 30 }, new[] { 220, 24 }, new[] { 410, 40 } };
            foreach (Estado e in Estados)
                for (int cual = 0; cual <= 6; cual++)
                    foreach (int[] m in medidas)
                    {
                        string nombre = "vista-" + e.Nombre + "-" + cual + "-" + m[0] + "x" + m[1];
                        Control c = null; string ex = "", st = "";
                        using (Bitmap b = new Bitmap(440, 64, PixelFormat.Format32bppPArgb))
                        {
                            try
                            {
                                c = Vista(cual, m[0], m[1]);
                                using (Graphics g = Prepara(b, e))
                                {
                                    try
                                    {
                                        if (trad != null) trad.Invoke(null, Junta(EstadoDe(g), c));
                                        else xp.Invoke(null, new object[] { g, c });
                                    }
                                    catch (Exception x) { ex = " " + Exc(x); }
                                    st = EstadoG(g);
                                }
                            }
                            catch (Exception x) { ex += " vista " + Exc(x); }
                            Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                        }
                    }
        }

        // ================================================================ LA BARRA DE AYUDA: por procesador (OnPaint, con un Graphics), directo, el halo y su ancho
        private static void Ayuda()
        {
            Type hb = Anidado("HelpBar");
            MethodInfo xp = hb.GetMethod("RenderContent", Todo, null, new[] { typeof(Graphics), typeof(bool) }, null);
            MethodInfo trad = Metodo(hb, "RenderContent", typeof(SKCanvas), 5);
            foreach (Estado e in Estados)
                foreach (bool justa in new[] { false, true })
                    foreach (bool forGpu in new[] { false, true })
                        foreach (int alto in new[] { 18, 28, 37 })
                        {
                            Control c = (Control)Activator.CreateInstance(hb, true); Pon(c, "Justa", justa); c.Bounds = new Rectangle(0, 0, 330, alto);
                            string nombre = "ayuda-" + e.Nombre + "-" + justa + "-" + forGpu + "-" + alto;
                            // la firma de XP (la que llama su OnPaint): en la traduccion, por el puente
                            using (Bitmap b = new Bitmap(360, 48, PixelFormat.Format32bppPArgb))
                            {
                                string ex = "", st;
                                using (Graphics g = Prepara(b, e))
                                {
                                    try { xp.Invoke(c, new object[] { g, forGpu }); } catch (Exception x) { ex = " " + Exc(x); }
                                    st = EstadoG(g);
                                }
                                Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                            }
                            // y directo, como la llama DrawCtrlOverlay traducida (en la referencia, la de XP otra vez)
                            using (Bitmap b = new Bitmap(360, 48, PixelFormat.Format32bppPArgb))
                            {
                                string ex = "", st;
                                using (Graphics g = Prepara(b, e))
                                {
                                    try
                                    {
                                        if (trad != null) { object[] s = EstadoDe(g); trad.Invoke(c, new[] { s[0], s[2], s[3], s[5], (object)forGpu }); directas++; g.SmoothingMode = SmoothingMode.AntiAlias; }   // (lo que deja la de XP: el suavizado)
                                        else xp.Invoke(c, new object[] { g, forGpu });
                                    }
                                    catch (Exception x) { ex = " " + Exc(x); }
                                    st = EstadoG(g);
                                }
                                Linea(nombre + "-directo", Puntos(nombre + "-directo", b) + ex + " " + st);
                            }
                        }
            // el halo del boton pulsado (GlowAround por el puente de la barra traducida) con niveles fijos: el de la barra depende del reloj
            MethodInfo glowXp = Metodo(typeof(Form1), "GlowAround", typeof(Graphics)), glowTr = Metodo(typeof(Form1), "GlowAroundVideoForm1", typeof(SKCanvas)) ?? Metodo(typeof(Form1), "GlowAround", typeof(SKCanvas));   // (la de XP por su firma: con Form1.Pared.cs traducido hay dos GlowAround)
            float[] niveles = { 0f, 0.02f, 0.03f, 0.5f, 1f };
            foreach (Estado e in Estados)
                foreach (float lv in niveles)
                {
                    string nombre = "halo-" + e.Nombre + "-" + R(lv);
                    using (Bitmap b = new Bitmap(120, 64, PixelFormat.Format32bppPArgb))
                    {
                        string ex = "", st;
                        using (Graphics g = Prepara(b, e))
                        {
                            try
                            {
                                if (glowTr != null) { object[] s = EstadoDe(g); glowTr.Invoke(null, new[] { s[0], s[1], s[2], s[3], s[5], 20.5f, 30f, 24f, lv, (object)Color.FromArgb(242, 206, 72) }); }
                                else glowXp.Invoke(null, new object[] { g, 20.5f, 30f, 24f, lv, Color.FromArgb(242, 206, 72) });
                            }
                            catch (Exception x) { ex = " " + Exc(x); }
                            st = EstadoG(g);
                        }
                        Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                    }
                }
            // el ancho de la barra (HelpBar.Ancho y AnchoJusta: mide con un Graphics de usar y tirar)
            MethodInfo ancho = hb.GetMethod("Ancho", Todo), anchoJ = hb.GetMethod("AnchoJusta", Todo);
            StringBuilder sb = new StringBuilder();
            for (int alto = 6; alto <= 64; alto += 3) sb.Append(alto).Append(':').Append(ancho.Invoke(null, new object[] { alto })).Append('/').Append(anchoJ.Invoke(null, new object[] { alto })).Append(' ');
            Linea("ayuda-anchos", sb.ToString());
        }

        // ================================================================ PartirNow (nadie la llama, tampoco en XP): con la Font en XP, con la Letra en la traduccion
        private static void Partir()
        {
            MethodInfo m = typeof(Form1).GetMethod("PartirNow", Todo);
            bool trad = m.GetParameters()[1].ParameterType != typeof(Font);
            string[] textos = { "", "Corto", "Una cancion con un titulo bastante largo que no cabe", "Supercalifragilisticoespialidosoyalgomasquenocabe", "Ángel Ñandú — «la de siempre» & más", "a b c d e f g h i j k l m n o p" };
            Font[] letras = { new Font(FontFamily.GenericSansSerif, 11.5f, FontStyle.Bold), new Font("Microsoft Sans Serif", 13f, FontStyle.Regular, GraphicsUnit.Pixel) };
            foreach (Font f in letras)
                foreach (string t in textos)
                    foreach (int[] w in new[] { new[] { 300, 120 }, new[] { 180, 60 }, new[] { 90, 19 }, new[] { 60, 25 }, new[] { 1000, 400 } })
                    {
                        object[] a = { t, trad ? Por("Letra", typeof(Font), f) : f, w[0], w[1], null, null };
                        string ex = "";
                        try { m.Invoke(null, a); } catch (Exception x) { ex = " " + Exc(x); }
                        Linea("partir-" + f.Size + "-" + t.Length + "-" + w[0] + "x" + w[1], "[" + a[4] + "] [" + a[5] + "]" + ex);
                    }
        }

        // ================================================================ EnSeco (el precalentado): el estado del Graphics que le da a lo que llama
        private static void Seco()
        {
            object form = RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            MethodInfo m = typeof(Form1).GetMethod("EnSeco", Todo, null, new[] { typeof(Control), typeof(Action<Graphics>) }, null);
            int[][] medidas = { new[] { 7, 40 }, new[] { 40, 7 }, new[] { 8, 8 }, new[] { 333, 121 }, new[] { 1024, 600 } };
            foreach (int[] md in medidas)
            {
                Control c = new Label(); c.Bounds = new Rectangle(3, 4, md[0], md[1]);
                string visto = "nada";
                Action<Graphics> dibuja = delegate (Graphics g)
                {
                    using (Font f = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold))
                    {
                        SizeF s = g.MeasureString("Entrar / Poner", f);
                        visto = EstadoG(g) + " mide=" + R(s.Width) + "x" + R(s.Height) + " visible=" + R(g.VisibleClipBounds);
                        using (SolidBrush b = new SolidBrush(Color.Red)) { g.FillEllipse(b, 1, 1, 5, 5); g.DrawString("x", f, b, 0, 0); }
                    }
                };
                object r = m.Invoke(form, new object[] { c, dibuja });
                Linea("enseco-" + md[0] + "x" + md[1], r + " " + visto);
            }
        }

        // ================================================================ EL TEXTO DE GDI POR EL PUENTE (GraphicsVideoForm1): la matriz del HDC
        // Las vistas sin traducir que se pintan en la imagen de la interfaz (el teclado y el aviso del buscador de siempre, por RenderContent y
        // DibujaPiezaViva) escriben con TextRenderer, que pinta con la matriz del HDC y no con la del lienzo. Los recorridos no pasan por ahi
        // (la traza no tiene ningun TextRenderer.DrawText del codigo de XP): aqui, el mismo texto en el Graphics de XP (un mapa de bits: HDC
        // identidad) y en el que da el puente sobre el lienzo con el mismo estado y la misma matriz.
        private static void TextoGdi()
        {
            MethodInfo gvf = Puente != null ? Puente.GetMethod("GraphicsVideoForm1", Todo) : null;
            Font[] letras = { new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold), new Font("Microsoft Sans Serif", 16f, FontStyle.Regular, GraphicsUnit.Pixel) };
            TextFormatFlags[] banderas = { TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis };
            int k = 0;
            foreach (Estado e in Estados)
                foreach (Font f in letras)
                    foreach (TextFormatFlags fl in banderas)
                    {
                        string nombre = "textogdi-" + e.Nombre + "-" + (k++);
                        using (Bitmap b = new Bitmap(200, 60, PixelFormat.Format32bppPArgb))
                        {
                            string ex = "", st;
                            using (Graphics g = Prepara(b, e))
                            {
                                try
                                {
                                    g.TranslateTransform(13f, 5f);   // (como DrawCtrlOverlay y EnsureUiOverlay antes de llamar a la vista)
                                    Graphics gg = g;
                                    if (gvf != null) { object[] s = EstadoDe(g); gg = (Graphics)gvf.Invoke(null, new[] { s[0], s[1], s[2], s[3], s[4], s[5] }); directas++; }
                                    TextRenderer.DrawText(gg, "Tecla Ñ & más texto", f, new Rectangle(4, 6, 150, 30), Color.FromArgb(255, 250, 240, 90), fl);
                                }
                                catch (Exception x) { ex = " " + Exc(x); }
                                st = EstadoG(g);
                            }
                            Linea(nombre, Puntos(nombre, b) + ex + " " + st);
                        }
                    }
            foreach (Font f in letras) f.Dispose();
        }

        public static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "vuelca") { carpeta = args[1]; System.IO.Directory.CreateDirectory(carpeta); }
            Etiquetas();
            Vistas();
            Ayuda();
            Partir();
            Seco();
            TextoGdi();
            Console.Error.WriteLine(n + " lineas; " + directas + " llamadas directas a lo traducido");
            return 0;
        }
    }
}

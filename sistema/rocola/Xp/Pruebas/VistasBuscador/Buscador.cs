// ARNES DEL GRUPO «BUSCADOR» (port nativo, tanda 3 del dibujo, 11-11-2026): lo que dibuja Form1.Buscador.cs (la casilla de BUSCAR, BuscaView,
// y el buscador de siempre, SearchOverlay sin portadas), con datos fijos, la huella de cada mapa y lo que devuelve cada llamada. Se compila con
// el programa de una carpeta Xp (VistasBuscador.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen que ser
// iguales linea por linea. Los recorridos solo pasan por la casilla de BUSCAR y FlechaArriba (con tarjeta el buscador es el de portadas): esto
// pasa por todo lo demas.
//   - Lo que se llama es lo que llama el codigo sin traducir, con su firma de XP: BuscaView.RenderContent(g) y (g, forGpu) (OnPaint y
//     DrawCtrlOverlay), AnchoJusto (la medida con su letra), SearchOverlay.RenderContent(g, caja, forGpu) (OnPaint: el dibujo por procesador
//     con el teclado, los resultados, el cubo de la portada y el aviso; EnsureUiOverlay: sin la casilla ni la tecla elegida) y
//     PiezaViva/DibujaPiezaViva (las imagenes chicas: la tecla elegida, la casilla, y con la tarjeta aparte la tarjeta elegida y el texto de
//     la vista previa, DrawExtraGpu de AcabadoGpu.cs). En la referencia van a GDI+ (la capa congelada) y en la traduccion al puente
//     (Plataforma/Puente.buscador.cs) y de ahi a SkiaSharp directo.
//   - Contenidos: vacio, lo escrito con resultados (lo tecleado resaltado, EN COLA, SONANDO, generos, discos), mas resultados de los que caben,
//     el foco en los resultados (cancion, disco, artista con varias portadas y la mezcla de dos, sin portada), la tecla elegida en varias
//     filas (BORRAR, ESPACIO), la casilla con texto que no cabe, el aviso, la tarjeta aparte (SeparateGpu, con la de servicio), la cola que
//     cambia (solo se retocan las tarjetas que cambiaron) y el cielo (Starfield, con un recorte parcial: la capa de estrellas fija).
//   - El estado del Graphics del que llama va de once maneras (sin suavizar, copia, medio pixel, los modos de letra, cuatro filtros,
//     traslacion entera y con decimales, escala, giro y recorte). Despues de cada llamada se apunta el estado que le queda al Graphics.
//   - En la traduccion, las traducidas se llaman tambien DIRECTO (el lienzo y su estado), como las llamaran Form1.Video.cs y Form1.Portadas.cs
//     cuando se traduzcan; en la referencia esa linea es la llamada de XP otra vez.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet VistasBuscador.dll [vuelca CARPETA]   (RAPIDO=1: menos estados, para los controles negativos)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using SkiaSharp;

namespace BancoBuscador
{
    internal static class Buscador
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta;
        private static int n, directas, excepciones;
        private static string R(float v) { return v.ToString("R", ci); }
        private static string R(RectangleF r) { return "(" + R(r.X) + "," + R(r.Y) + "," + R(r.Width) + "," + R(r.Height) + ")"; }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }

        private static readonly Type SO = typeof(Form1).GetNestedType("SearchOverlay", BindingFlags.NonPublic);
        private static readonly Type BV = typeof(Form1).GetNestedType("BuscaView", BindingFlags.NonPublic);
        private static readonly Type TItem = typeof(Form1).GetNestedType("SearchItem", BindingFlags.NonPublic);
        private static readonly Type TAlbum = typeof(Form1).GetNestedType("Album", BindingFlags.NonPublic);
        private static readonly Type TTrack = typeof(Form1).GetNestedType("Track", BindingFlags.NonPublic);
        private static readonly Type TSky = typeof(Form1).GetNestedType("Starfield", BindingFlags.NonPublic);

        // los puntos de un mapa: tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb: sin premultiplicar)
        private static unsafe string Puntos(string nombre, Bitmap b)
        {
            SKBitmap s = b.Sk; if (s == null) return "soltado";
            byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            BitmapData d = b.LockBits(new Rectangle(0, 0, s.Width, s.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] sub = new byte[s.Width * s.Height * 4];
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) sub[y * s.Width * 4 + x] = ((byte*)d.Scan0)[y * d.Stride + x];
            b.UnlockBits(d);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }

        // ---- el estado del Graphics del que llama
        private sealed class Estado
        {
            public string Nombre; public SmoothingMode S; public TextRenderingHint T; public CompositingMode C; public PixelOffsetMode P;
            public InterpolationMode I = InterpolationMode.Bilinear; public int Trans; public bool Recorte;
        }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "onpaint", S = SmoothingMode.None, T = TextRenderingHint.ClearTypeGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "xp", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "precalienta", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "sin-suavizar", S = SmoothingMode.None, T = TextRenderingHint.SystemDefault, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, I = InterpolationMode.NearestNeighbor },
            new Estado { Nombre = "copia-medio-traslada", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala", S = SmoothingMode.AntiAlias, T = TextRenderingHint.ClearTypeGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.HighQuality, Trans = 2, I = InterpolationMode.HighQualityBicubic },
            new Estado { Nombre = "unbit-gira", S = SmoothingMode.AntiAlias, T = TextRenderingHint.SingleBitPerPixelGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = SmoothingMode.None, T = TextRenderingHint.SingleBitPerPixel, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Default, Recorte = true },
            new Estado { Nombre = "traslada-recorte", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 4, Recorte = true, I = InterpolationMode.HighQualityBilinear },
            new Estado { Nombre = "suave-sin-ajuste", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.None, Trans = 5, I = InterpolationMode.Bicubic },
            new Estado { Nombre = "ui-trasladada", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 6, I = InterpolationMode.Low },
        };
        private static Graphics Prepara(Bitmap b, Estado e, bool fondo)
        {
            Graphics g = Graphics.FromImage(b);
            if (fondo)
            {
                g.Clear(Color.FromArgb(120, 30, 60, 90));
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(200, 250, 20, 60))) g.FillRectangle(sb, 0, b.Height / 3, b.Width, 9);
            }
            else g.Clear(Color.Transparent);
            g.SmoothingMode = e.S; g.TextRenderingHint = e.T; g.CompositingMode = e.C; g.PixelOffsetMode = e.P; g.InterpolationMode = e.I;
            if (e.Trans == 1) g.TranslateTransform(3.5f, 2.25f); else if (e.Trans == 2) g.ScaleTransform(1.3f, 1.3f); else if (e.Trans == 3) g.RotateTransform(7f);
            else if (e.Trans == 4) g.TranslateTransform(0, -40); else if (e.Trans == 5) g.TranslateTransform(0.5f, 0f); else if (e.Trans == 6) g.TranslateTransform(3, 5);
            if (e.Recorte) g.SetClip(new RectangleF(30.5f, 8, b.Width * 0.6f, b.Height * 0.7f));
            return g;
        }
        private static string Queda(Graphics g)
        {
            float[] m = MatrizDe(g);
            return " queda=" + g.SmoothingMode + "/" + g.TextRenderingHint + "/" + g.CompositingMode + "/" + g.InterpolationMode + "/" + g.PixelOffsetMode
                + "/" + R(m[0]) + "," + R(m[1]) + "," + R(m[2]) + "," + R(m[3]) + "," + R(m[4]) + "," + R(m[5]) + "/" + R(g.ClipBounds);
        }
        private static float[] MatrizDe(Graphics g)
        {
            PointF[] p = { new PointF(0, 0), new PointF(1, 0), new PointF(0, 1) };
            using (Matrix t = g.Transform) t.TransformPoints(p);
            return new[] { p[1].X - p[0].X, p[1].Y - p[0].Y, p[2].X - p[0].X, p[2].Y - p[0].Y, p[0].X, p[0].Y };
        }

        // ---- reflexion (las vistas, SearchItem, Album y Track son privados de Form1)
        private static object Nuevo(Type t) { return Activator.CreateInstance(t, true); }
        private static void Pon(object o, string nombre, object v)
        {
            FieldInfo f = o.GetType().GetField(nombre, Todo);
            if (f != null) { f.SetValue(o, v); return; }
            o.GetType().GetProperty(nombre, Todo).SetValue(o, v);
        }
        private static object Lee(object o, string nombre)
        {
            FieldInfo f = o.GetType().GetField(nombre, Todo);
            return f != null ? f.GetValue(o) : o.GetType().GetProperty(nombre, Todo).GetValue(o);
        }
        private static MethodInfo Metodo(Type t, string nombre, params Type[] tipos) { return t.GetMethod(nombre, Todo, null, tipos, null); }
        private static Exception Interna(Exception x) { return x is TargetInvocationException && x.InnerException != null ? x.InnerException : x; }
        // la traducida, llamada directo; null en la referencia (no la hay)
        private static MethodInfo Nativa(Type t, string nombre)
        {
            foreach (MethodInfo m in t.GetMethods(Todo)) if (m.Name == nombre && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(SKCanvas)) return m;
            return null;
        }
        private static Type Puente { get { return typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo"); } }
        private static object Por(string metodo, Type tipo, object o) { return Puente.GetMethod(metodo, Todo, null, new[] { tipo }, null).Invoke(null, new[] { o }); }
        // el lienzo y su estado (suave, letra, copia, filtro, medio, hdc) de un Graphics, por el puente de la traduccion
        private static object[] EstadoDe(Graphics g)
        {
            object e = Por("Estado", typeof(Graphics), g); Type te = e.GetType();
            return new[] { Por("Lienzo", typeof(Graphics), g), te.GetField("Suave").GetValue(e), te.GetField("Letra").GetValue(e), te.GetField("Copia").GetValue(e),
                           te.GetField("Filtro").GetValue(e), te.GetField("Medio").GetValue(e), te.GetField("Hdc").GetValue(e) };
        }

        // ---- las portadas de prueba: puntos al azar con semilla fija (opacas o con alfa), escritas con LockBits como XP
        private static unsafe Bitmap Portada(int w, int h, int semilla, bool alfa)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Random r = new Random(semilla);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = alfa ? (x * 7 + y * 3 + semilla * 11) & 255 : 255;
                    ((uint*)((byte*)d.Scan0 + y * d.Stride))[x] = ((uint)a << 24) | (uint)((r.Next(256) << 16) | (((x * 255) / Math.Max(1, w - 1)) << 8) | ((y * 255) / Math.Max(1, h - 1)));
                }
            b.UnlockBits(d);
            return b;
        }

        // ---- la biblioteca de prueba
        private static readonly Dictionary<string, Image> portadas = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static object Disco(string nombre, string artista, string portada)
        {
            object a = Nuevo(TAlbum);
            Pon(a, "Name", nombre); Pon(a, "Artist", artista); Pon(a, "CoverPath", portada); Pon(a, "Folder", "/musica/" + nombre);
            return a;
        }
        private static object Item(int kind, string display, object album, object track, object[] albums)
        {
            object it = Nuevo(TItem);
            Pon(it, "Kind", kind); Pon(it, "Display", display); Pon(it, "Key", (display ?? "").ToLowerInvariant()); Pon(it, "AlbumRef", album); Pon(it, "TrackRef", track);
            if (albums != null) { IList l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlbum)); foreach (object x in albums) l.Add(x); Pon(it, "Albums", l); }
            return it;
        }
        private static object Cancion(string titulo, string artista, object album, string ruta)
        {
            object c = Nuevo(TTrack);
            Pon(c, "Path", ruta); Pon(c, "Title", titulo); Pon(c, "Artist", artista); Pon(c, "Album", album != null ? Lee(album, "Name") : null);
            return c;
        }
        private static IList Lista(IEnumerable<object> items)
        {
            IList l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem));
            foreach (object x in items) l.Add(x);
            return l;
        }
        // un delegado del tipo del campo (Func<SearchItem, ...> o Func<Album, ...>, de tipos privados) que llama a 'f' con object
        private static Delegate Delegado(string campo, Delegate f)
        {
            Type t = SO.GetField(campo, Todo).FieldType;
            Type[] a = t.GetGenericArguments();
            System.Linq.Expressions.ParameterExpression p = System.Linq.Expressions.Expression.Parameter(a[0], "x");
            System.Linq.Expressions.Expression cuerpo = System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(f), System.Linq.Expressions.Expression.Convert(p, typeof(object)));
            return System.Linq.Expressions.Expression.Lambda(t, cuerpo, p).Compile();
        }

        // ---- un contenido del buscador
        private sealed class Contenido
        {
            public string Nombre, Escrito = "", Query = "", Toast; public List<object> Res = new List<object>();
            public bool Foco, Separa; public int Sel, Fila = 1, Col; public double Phase, Cubo = 0.4;
        }
        private static object NuevoBuscador(int w, int h, Color acento)
        {
            object so = Nuevo(SO);
            Pon(so, "Size", new Size(w, h));
            Pon(so, "Accent", acento);
            Metodo(SO, "PonNuevo", typeof(bool)).Invoke(so, new object[] { false });
            Pon(so, "GetCover", Delegado("GetCover", (Func<object, Image>)(a => { string p = (string)Lee(a, "CoverPath"); Image i; return p != null && portadas.TryGetValue(p, out i) ? i : null; })));
            Pon(so, "GetGenero", Delegado("GetGenero", (Func<object, string>)(it => { string d = (string)Lee(it, "Display") ?? ""; int k = d.Length % 5; return k == 0 ? "Karaoke" : k == 1 ? "Rock" : k == 2 ? "" : k == 3 ? null : "Música Regional Mexicana"; })));
            Pon(so, "GetDisco", Delegado("GetDisco", (Func<object, string>)(it => { object a = Lee(it, "AlbumRef"); return a != null ? (string)Lee(a, "Name") : ""; })));
            Pon(so, "GetQueuePos", Delegado("GetQueuePos", (Func<object, int>)(it => { string d = (string)Lee(it, "Display") ?? ""; return d.StartsWith("La B") ? 3 : d.Length == 11 ? 12 : 0; })));
            Pon(so, "GetSonando", Delegado("GetSonando", (Func<object, bool>)(it => ((string)Lee(it, "Display") ?? "").Contains("Bikina"))));
            return so;
        }
        private static void PonContenido(object so, Contenido c)
        {
            Pon(so, "Results", Lista(c.Res));
            Pon(so, "Escrito", c.Escrito); Pon(so, "Query", c.Query);
            Pon(so, "FocusResults", c.Foco); Pon(so, "SelResult", c.Sel); Pon(so, "SelRow", c.Fila); Pon(so, "SelCol", c.Col);
            Pon(so, "Phase", c.Phase); Pon(so, "CubeAngle", c.Cubo); Pon(so, "SeparateGpu", c.Separa);
            Pon(so, "ToastOn", c.Toast != null); Pon(so, "ToastMsg", c.Toast ?? ""); Pon(so, "ToastBack", Color.FromArgb(238, 40, 165, 90));
        }

        // ---- LA IMAGEN GRANDE: RenderContent(g, caja, forGpu) (o directo, la traducida)
        private static void Grande(string nombre, object so, Estado e, bool fondo, bool forGpu, Rectangle caja, bool directo, object cielo)
        {
            int w = ((Size)Lee(so, "Size")).Width, h = ((Size)Lee(so, "Size")).Height;
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            {
                string ex = "", queda = "";
                MethodInfo m = directo ? Nativa(SO, "RenderContent") : null;
                try
                {
                    using (Graphics g = Prepara(b, e, fondo))
                    {
                        if (cielo != null) { g.SetClip(new Rectangle(20, 20, w / 2, h / 3)); TSky.GetField("shownLayer", Todo).SetValue(cielo, 0); }
                        if (m == null) { Metodo(SO, "RenderContent", typeof(Graphics), typeof(Rectangle), typeof(bool)).Invoke(so, new object[] { g, caja, forGpu }); queda = directo ? " filtro=" + (int)g.InterpolationMode : Queda(g); }
                        else
                        {
                            directas++; object[] es = EstadoDe(g);
                            object[] args = { es[0], es[2], es[3], es[4], es[5], es[6], caja, forGpu };
                            m.Invoke(so, args);
                            queda = " filtro=" + (int)args[3];   // (lo que la referencia apunta del Graphics: el filtro que le deja)
                        }
                    }
                }
                catch (Exception x) { ex = " EXC:" + Interna(x).GetType().Name; excepciones++; }
                Linea(nombre, Puntos(nombre, b) + ex + queda + " resultados=" + ((IList)Lee(so, "resultRects")).Count);
            }
        }
        // las imagenes chicas (0 la tecla elegida, 1 la casilla, 2 la tarjeta elegida y 3 el texto de la vista previa, con la tarjeta aparte),
        // como EnsureUiPieza: el sitio (PiezaViva) y la imagen de esa medida
        private static void Piezas(string nombre, object so, Estado e, bool directo)
        {
            for (int cual = 0; cual <= 3; cual++)
            {
                object[] args = { cual, null };
                bool hay = (bool)Metodo(SO, "PiezaViva", typeof(int), typeof(Rectangle).MakeByRefType()).Invoke(so, args);
                Rectangle donde = (Rectangle)args[1];
                string nom = nombre + "-p" + cual;
                if (!hay || donde.Width < 1 || donde.Height < 1) { Linea(nom, "sin pieza " + donde); continue; }
                using (Bitmap b = new Bitmap(donde.Width, donde.Height, PixelFormat.Format32bppPArgb))
                {
                    string ex = "", queda = "";
                    try
                    {
                        using (Graphics g = Prepara(b, e, false))
                        {
                            MethodInfo m = directo ? Nativa(SO, "DibujaPiezaViva") : null;
                            if (m == null) { Metodo(SO, "DibujaPiezaViva", typeof(Graphics), typeof(int), typeof(int), typeof(int)).Invoke(so, new object[] { g, cual, -donde.X, -donde.Y }); queda = directo ? " suave=" + (g.SmoothingMode == SmoothingMode.AntiAlias || g.SmoothingMode == SmoothingMode.HighQuality) : Queda(g); }
                            else
                            {
                                directas++; object[] es = EstadoDe(g);
                                object[] a2 = { es[0], es[1], es[2], es[3], es[4], es[5], es[6], cual, -donde.X, -donde.Y };
                                m.Invoke(so, a2);
                                queda = " suave=" + a2[1];   // (la referencia apunta el suavizado que le deja al Graphics)
                            }
                        }
                    }
                    catch (Exception x) { ex = " EXC:" + Interna(x).GetType().Name; excepciones++; }
                    Linea(nom, donde + "\t" + Puntos(nom, b) + ex + queda);
                }
            }
        }

        // ---- LA CASILLA DE BUSCAR (BuscaView)
        private static void Casilla(string nombre, object bv, Estado e, int forma, bool directo)
        {
            Size t = (Size)Lee(bv, "Size");
            using (Bitmap b = new Bitmap(Math.Max(1, t.Width), Math.Max(1, t.Height), PixelFormat.Format32bppPArgb))
            {
                string ex = "", queda = "";
                MethodInfo m = directo ? Nativa(BV, "RenderContent") : null;
                try
                {
                    using (Graphics g = Prepara(b, e, forma == 2))
                    {
                        if (m == null)
                        {
                            if (forma == 0) Metodo(BV, "RenderContent", typeof(Graphics)).Invoke(bv, new object[] { g });
                            else Metodo(BV, "RenderContent", typeof(Graphics), typeof(bool)).Invoke(bv, new object[] { g, forma == 1 });
                            if (!directo) queda = Queda(g);
                        }
                        else { directas++; object[] es = EstadoDe(g); m.Invoke(bv, new object[] { es[0], es[2], es[3], es[5], forma == 1 }); }
                    }
                }
                catch (Exception x) { ex = " EXC:" + Interna(x).GetType().Name; excepciones++; }
                object tecla = BV.GetField("Tecla", Todo) != null ? Lee(bv, "Tecla") : null;
                Linea(nombre, Puntos(nombre, b) + ex + queda + " tecla=" + tecla);
            }
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            bool rapido = Environment.GetEnvironmentVariable("RAPIDO") == "1";
            Estado[] estados = rapido ? new[] { Estados[0], Estados[3], Estados[4], Estados[7] } : Estados;

            // 1) LA CASILLA DE BUSCAR: siete altos (uno que no llega), cerrada (late) y abierta, tres maneras (OnPaint, para la tarjeta, sin
            // tarjeta) y la medida con su letra (AnchoJusto)
            foreach (int alto in new[] { 11, 12, 20, 26, 34, 52, 80 })
            {
                int ancho = (int)Metodo(BV, "Ancho", typeof(int)).Invoke(null, new object[] { alto });
                MethodInfo justo = Metodo(BV, "AnchoJusto", typeof(int));
                Linea("casilla-" + alto + "-medidas", "ancho=" + ancho + (justo != null ? " justo=" + justo.Invoke(null, new object[] { alto }) : ""));
                foreach (bool abierto in new[] { false, true })
                    foreach (double fase in new[] { 0.0, 1.3 })
                    {
                        object bv = Nuevo(BV);
                        Pon(bv, "Size", new Size(alto == 11 ? 25 : ancho, alto)); Pon(bv, "Abierto", abierto); Pon(bv, "Phase", fase);
                        foreach (Estado e in estados)
                            for (int forma = 0; forma < 3; forma++)
                                Casilla("casilla-" + alto + "-" + (abierto ? "abierta" : "cerrada") + "-f" + fase.ToString(ci) + "-" + e.Nombre + "-m" + forma, bv, e, forma, false);
                        foreach (Estado e in new[] { Estados[0], Estados[4] })
                            Casilla("casilla-directo-" + alto + "-" + (abierto ? "abierta" : "cerrada") + "-f" + fase.ToString(ci) + "-" + e.Nombre, bv, e, 1, true);
                        ((IDisposable)bv).Dispose();
                    }
            }

            // 2) EL BUSCADOR DE SIEMPRE
            portadas["/p/opaca.jpg"] = Portada(600, 600, 1, false);
            portadas["/p/alfa.png"] = Portada(300, 300, 2, true);
            portadas["/p/ancha.jpg"] = Portada(1600, 1000, 3, false);
            portadas["/p/alta.png"] = Portada(500, 900, 4, true);
            object d1 = Disco("Grandes Exitos", "Los Bukis", "/p/opaca.jpg");
            object d2 = Disco("Ángel de amor (versión ñandú) & más, edición de lujo remasterizada", "Ñandú y su Orquesta", "/p/alfa.png");
            object d3 = Disco("Sin Portada", "Artista X", "/p/falta.jpg");
            object d4 = Disco("Videos de Fiesta", "Varios", "/p/ancha.jpg");
            object d5 = Disco("Alta y Delgada", "La Banda Alta", "/p/alta.png");
            List<object> todos = new List<object>
            {
                Item(2, "La Bikina", d1, Cancion("03 - La Bikina", "Rubén Fuentes", d1, "/musica/a/03.mp3"), null),
                Item(0, "Los Bukis", null, null, new[] { d1, d2, d4 }),
                Item(1, "Grandes Exitos", d1, null, null),
                Item(2, "Ángel de amor (versión ñandú) & más, un titulo larguisimo que no cabe", d2, Cancion("-07 Ángel de amor", "Ñandú y su Orquesta", d2, "/musica/b/07.mp3"), null),
                Item(1, "Sin Portada", d3, null, null),
                Item(0, "La Banda Alta", d5, null, new[] { d5 }),
                Item(2, "La cancion sin artista", d3, Cancion("La cancion sin artista", "", d3, "/musica/c/01.mp3"), null),
                Item(1, "Videos de Fiesta", d4, null, null),
                Item(0, "Artista sin discos", d3, null, null),
                Item(2, "La-la-la & la", null, Cancion("La-la-la & la", "Nadie", null, "/musica/e/01.mp3"), null),
            };
            for (int i = 0; i < 30; i++) todos.Add(Item(i % 3, "Relleno " + i + (i % 2 == 0 ? " la" : ""), i % 4 == 0 ? d1 : i % 4 == 1 ? d2 : i % 4 == 2 ? d3 : d5, i % 3 == 2 ? Cancion("Relleno " + i, "Artista " + i, d1, "/musica/r/" + i + ".mp3") : null, i % 3 == 0 ? new[] { d1, d2 } : null));
            object servicio = Item(3, "Clave de servicio", null, null, null);

            List<Contenido> contenidos = new List<Contenido>
            {
                new Contenido { Nombre = "vacio" },
                new Contenido { Nombre = "escrito", Escrito = "la bik", Query = "la", Res = todos.GetRange(0, 7), Fila = 2, Col = 3 },
                new Contenido { Nombre = "muchos", Escrito = "la", Query = "la", Res = todos, Fila = 0, Col = 0, Phase = 0.7 },
                new Contenido { Nombre = "foco-cancion", Escrito = "la", Query = "la", Res = todos, Foco = true, Sel = 0 },
                new Contenido { Nombre = "foco-artista", Escrito = "bukis", Query = "bukis", Res = todos.GetRange(1, 6), Foco = true, Sel = 0, Phase = 0.5, Cubo = 1.1 },
                new Contenido { Nombre = "foco-mezcla", Escrito = "bukis", Query = "bukis", Res = todos.GetRange(1, 6), Foco = true, Sel = 0, Phase = 2.8, Cubo = 2.3 },
                new Contenido { Nombre = "foco-mezcla2", Escrito = "bukis", Query = "bukis", Res = todos.GetRange(1, 6), Foco = true, Sel = 0, Phase = 6.55 },
                new Contenido { Nombre = "foco-sinportada", Escrito = "sin", Query = "sin", Res = todos.GetRange(3, 5), Foco = true, Sel = 1 },
                new Contenido { Nombre = "foco-alta", Escrito = "alta", Query = "alta", Res = todos.GetRange(5, 1), Foco = true, Sel = 0, Cubo = 0.0 },
                new Contenido { Nombre = "borrar", Fila = 4, Col = 1, Escrito = "ñ", Query = "ñ", Res = todos.GetRange(2, 1) },
                new Contenido { Nombre = "espacio", Fila = 4, Col = 0, Escrito = "la ", Query = "la", Res = todos.GetRange(0, 3), Phase = 0.6 },
                new Contenido { Nombre = "largo", Escrito = "Ángel de amor de la versión ñandú & más, y otra cosa que no cabe en la casilla de ninguna manera posible", Query = "angel", Res = new List<object>(), Fila = 3, Col = 9 },
                new Contenido { Nombre = "aviso", Escrito = "1234", Query = "1234", Res = todos.GetRange(0, 2), Toast = "Pusiste «La Bikina», va la 3.ª en la cola" },
                new Contenido { Nombre = "aparte", Escrito = "la", Query = "la", Res = todos, Foco = true, Sel = 3, Separa = true },
                new Contenido { Nombre = "aparte-artista", Escrito = "bukis", Query = "bukis", Res = todos.GetRange(1, 6), Foco = true, Sel = 0, Separa = true },
                new Contenido { Nombre = "servicio", Escrito = "1234", Query = "1234", Res = new List<object> { servicio, todos[0] }, Foco = true, Sel = 0, Separa = true },
                new Contenido { Nombre = "servicio-cpu", Escrito = "1234", Query = "1234", Res = new List<object> { servicio, todos[0] }, Foco = false },
            };
            Color[] acentos = { Color.FromArgb(120, 160, 255), Color.FromArgb(3, 5, 4), Color.FromArgb(255, 221, 132) };

            // 2a) LA PANTALLA DE SIEMPRE (1024x768): los estados, los contenidos y dos colores; por procesador (OnPaint: con la casilla) y para la
            // tarjeta (sin ella), con la caja entera y con una caja parcial; las imagenes chicas; y la cola que cambia (solo se retocan las tarjetas)
            int caso = 0;
            foreach (Estado e in estados)
                for (int ac = 0; ac < (rapido ? 1 : 2); ac++)
                {
                    object so = NuevoBuscador(1024, 768, acentos[ac == 0 ? 0 : 1]);
                    foreach (Contenido c in contenidos)
                    {
                        PonContenido(so, c);
                        string nom = "s1024-" + e.Nombre + "-a" + ac + "-" + c.Nombre;
                        Grande(nom + "-cpu", so, e, caso % 3 == 1, false, new Rectangle(0, 0, 1024, 768), false, null);
                        Grande(nom + "-gpu", so, e, caso % 3 == 2, true, new Rectangle(0, 0, 1024, 768), false, null);
                        if (caso % 2 == 0) Grande(nom + "-parcial", so, e, false, false, new Rectangle(0, 0, 500, 120), false, null);
                        Piezas(nom, so, e, false);
                        if (caso % 4 == 3)
                        {
                            Pon(so, "ColaVer", (int)Lee(so, "ColaVer") + 1);
                            Pon(so, "GetQueuePos", Delegado("GetQueuePos", (Func<object, int>)(it => ((string)Lee(it, "Display") ?? "").Length % 3)));
                            Grande(nom + "-cola", so, e, false, false, new Rectangle(0, 0, 1024, 768), false, null);
                        }
                        caso++;
                    }
                }

            // 2b) OTRAS PANTALLAS (de 640x480 a 1920x1080), un estado
            foreach (Size t in new[] { new Size(640, 480), new Size(800, 600), new Size(1366, 768), new Size(1920, 1080), new Size(70, 70), new Size(59, 400) })
            {
                object so = NuevoBuscador(t.Width, t.Height, acentos[2]);
                foreach (Contenido c in contenidos)
                {
                    PonContenido(so, c);
                    string nom = "s" + t.Width + "x" + t.Height + "-" + c.Nombre;
                    Grande(nom + "-cpu", so, Estados[0], false, false, new Rectangle(0, 0, t.Width, t.Height), false, null);
                    Grande(nom + "-gpu", so, Estados[1], false, true, new Rectangle(0, 0, t.Width, t.Height), false, null);
                    Piezas(nom, so, Estados[1], false);
                }
            }

            // 2c) EL CIELO (Starfield.DrawReactive, por procesador) con un recorte parcial (sin las fugaces) y la capa de estrellas fija
            if (!rapido)
            {
                object cielo = Nuevo(TSky);
                foreach (Estado e in new[] { Estados[0], Estados[1], Estados[3], Estados[4] })
                {
                    object so = NuevoBuscador(1024, 768, acentos[0]);
                    Pon(so, "Sky", cielo); Pon(so, "PulseLo", 0f); Pon(so, "PulseMid", 0f); Pon(so, "PulseHi", 0f);
                    foreach (Contenido c in new[] { contenidos[0], contenidos[3] })
                    {
                        PonContenido(so, c);
                        Grande("cielo-" + e.Nombre + "-" + c.Nombre, so, e, false, false, new Rectangle(0, 0, 1024, 768), false, cielo);
                    }
                }
            }

            // 3) DIRECTO (en la traduccion, las traducidas con el lienzo y su estado; en la referencia, la llamada de XP otra vez)
            foreach (Estado e in rapido ? new[] { Estados[3] } : new[] { Estados[0], Estados[1], Estados[4], Estados[5], Estados[8] })
            {
                object so = NuevoBuscador(1024, 768, acentos[2]);
                foreach (Contenido c in contenidos)
                {
                    PonContenido(so, c);
                    string nom = "directo-" + e.Nombre + "-" + c.Nombre;
                    Grande(nom + "-cpu", so, e, true, false, new Rectangle(0, 0, 1024, 768), true, null);
                    Grande(nom + "-gpu", so, e, false, true, new Rectangle(0, 0, 1024, 768), true, null);
                    Piezas(nom, so, e, true);
                }
            }

            // 4) EL HALO DE LOS SIMBOLOS DE LA BARRA DE AYUDA (GlowAround, Form1.Pared.cs; revision): en RenderContent su brillo depende de los
            // milisegundos desde la pulsacion (InstrFlash), asi que alli casi siempre sale apagado. Aqui se llama aparte con niveles fijos: en la
            // traduccion por su puente (GlowAroundBuscador, con el lienzo y el estado del Graphics), en la referencia la de XP con el Graphics
            MethodInfo halo = typeof(Form1).GetMethod("GlowAroundBuscador", Todo);
            MethodInfo haloXp = Metodo(typeof(Form1), "GlowAround", typeof(Graphics), typeof(float), typeof(float), typeof(float), typeof(float), typeof(Color));
            foreach (Estado e in estados)
                foreach (float nivel in new[] { 0.01f, 0.35f, 1f })
                    foreach (Color col in new[] { Color.FromArgb(228, 72, 72), Color.White })
                    {
                        string nom = "halo-" + e.Nombre + "-" + nivel.ToString(ci) + "-" + col.ToArgb().ToString("x8");
                        using (Bitmap b = new Bitmap(90, 64, PixelFormat.Format32bppPArgb))
                        {
                            string ex = "", queda = "";
                            try
                            {
                                using (Graphics g = Prepara(b, e, true))
                                {
                                    if (halo == null) haloXp.Invoke(null, new object[] { g, 20.5f, 31f, 22f, nivel, col });
                                    else { directas++; object[] es = EstadoDe(g); halo.Invoke(null, new object[] { es[0], es[1], es[2], es[3], es[5], 20.5f, 31f, 22f, nivel, col }); }
                                    queda = Queda(g);
                                }
                            }
                            catch (Exception x) { ex = " EXC:" + Interna(x).GetType().Name; excepciones++; }
                            Linea(nom, Puntos(nom, b) + ex + queda);
                        }
                    }

            Linea("fin", "lineas=" + n);
            Console.Error.WriteLine("lineas " + n + ", directas " + directas + ", excepciones " + excepciones);
            return 0;
        }
    }
}

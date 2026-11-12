// ARNES DEL GRUPO «BUSCADOR-PORTADAS» (port nativo, tanda 2 del dibujo, 9-11-2026): lo que dibuja BuscadorPortadasGpu.cs (el buscador con
// portadas), con datos fijos, la huella de cada mapa y lo que devuelve cada llamada. Se compila con el programa de una carpeta Xp
// (VistasBuscadorPortadas.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales linea por linea.
//   - Lo que se llama es lo que llama el codigo sin traducir, con su firma de XP: RenderContent(g, caja, true) (Form1.Buscador.cs, que con el
//     buscador nuevo va a RenderNuevo), PiezaViva y DibujaPiezaViva (las imagenes chicas: la tecla o el atajo elegido, la casilla, la tarjeta
//     elegida) y PintaCartasConCambios (la pieza de las tarjetas, entera y por trozos), como EnsureUiOverlay y EnsureUiPieza (Form1.Video.cs);
//     en la referencia van a GDI+ (la capa congelada) y en la traduccion al puente (Plataforma/Puente.buscador-portadas.cs) y de ahi a SkiaSharp
//     directo. Y la miniatura de una portada (Escala).
//   - Contenidos: la casilla vacia (el cursor y la invitacion), con lo escrito que cabe y que no (el final con puntos), con la cuenta de los
//     resultados, con un atajo puesto; los atajos (tambien elegidos); el teclado con la tecla elegida (y BORRAR); las tarjetas de artista,
//     disco, cancion y la de servicio, con portada (opaca, con alfa, ancha, alta, diminuta) o sin ella (las iniciales sobre el degradado del
//     genero), con lo tecleado resaltado, SONANDO y EN COLA, la elegida (con y sin la captura de las cintas), la barrita cuando no caben, sin
//     tarjetas (los tres avisos); el aviso del buscador (DrawToast); tres colores de seccion (uno tan oscuro que Vivo usa el de repuesto); seis
//     pantallas (de la escala 0,6 a la 2); y el tipografico compartido tal cual y cambiado como lo deja EstadoReposoGpu.cs.
//   - El estado del Graphics del que llama va de once maneras (el de EnsureUiOverlay y EnsureUiPieza, el del precalentado, sin suavizar, copia,
//     medio pixel, los modos de letra, cuatro filtros, traslacion entera y con decimales, escala, giro y recorte). Despues de cada llamada se
//     apunta el estado que le queda al Graphics (lo que XP le deja cambiado).
//   - En la traduccion, las traducidas se llaman tambien DIRECTO (el lienzo y su estado), como las llamaran Form1.Buscador.cs y Form1.Video.cs
//     cuando se traduzcan; en la referencia esa linea es la llamada de XP otra vez.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet VistasBuscadorPortadas.dll [vuelca CARPETA]   (RAPIDO=1: cuatro estados, un color y dos pantallas mas, para
//   los controles negativos de control.sh)
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

namespace BancoBuscadorPortadas
{
    internal static class BuscadorPortadas
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
        private static readonly Type TItem = typeof(Form1).GetNestedType("SearchItem", BindingFlags.NonPublic);
        private static readonly Type TAlbum = typeof(Form1).GetNestedType("Album", BindingFlags.NonPublic);
        private static readonly Type TTrack = typeof(Form1).GetNestedType("Track", BindingFlags.NonPublic);

        // los puntos de un mapa (un Bitmap de la capa o un SKBitmap de la traduccion): tal cual (premultiplicados) y como los sube la tarjeta
        // (LockBits Format32bppArgb: sin premultiplicar)
        private static unsafe string Puntos(string nombre, object o)
        {
            if (o == null) return "null";
            Bitmap b = o as Bitmap ?? new Bitmap((SKBitmap)o, PixelFormat.Format32bppPArgb);
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
            new Estado { Nombre = "xp", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "precalienta", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "sin-suavizar", S = SmoothingMode.None, T = TextRenderingHint.SystemDefault, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, I = InterpolationMode.NearestNeighbor },
            new Estado { Nombre = "copia-medio-traslada", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala", S = SmoothingMode.AntiAlias, T = TextRenderingHint.ClearTypeGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.HighQuality, Trans = 2, I = InterpolationMode.HighQualityBicubic },
            new Estado { Nombre = "unbit-gira", S = SmoothingMode.AntiAlias, T = TextRenderingHint.SingleBitPerPixelGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = SmoothingMode.None, T = TextRenderingHint.SingleBitPerPixel, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Default, Recorte = true },
            new Estado { Nombre = "traslada-recorte", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 4, Recorte = true, I = InterpolationMode.HighQualityBilinear },
            new Estado { Nombre = "suave-sin-ajuste", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.None, Trans = 5, I = InterpolationMode.Bicubic },
            new Estado { Nombre = "ui-trasladada", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 6 },
            new Estado { Nombre = "bajo-filtro", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.HighSpeed, I = InterpolationMode.Low },
        };
        // un mapa (transparente, como lo hacen EnsureUiOverlay y EnsureUiPieza, o con fondo para ver la copia y el alfa) y el Graphics con ese estado
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
        // lo que le queda al Graphics despues de la llamada (lo que XP le deja cambiado)
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

        // ---- reflexion (SearchOverlay, SearchItem, Album y Track son privados de Form1)
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
        private static MethodInfo Metodo(string nombre, params Type[] tipos) { return SO.GetMethod(nombre, Todo, null, tipos, null); }
        private static Exception Interna(Exception x) { return x is TargetInvocationException && x.InnerException != null ? x.InnerException : x; }
        // la traducida, llamada directo; null en la referencia (no la hay)
        private static MethodInfo Nativa(string nombre)
        {
            foreach (MethodInfo m in SO.GetMethods(Todo)) if (m.Name == nombre && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(SKCanvas)) return m;
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

        // ---- LA FIRMA DE XP DE DibujaPiezaViva(Graphics, cual, dx, dy): desde la tanda 4 (grupo directas-buscador-video) ya no esta en la
        // traduccion (EnsureUiPieza de Form1.Video.cs llama directo a la traducida). Para seguir comparando lo que le queda al Graphics con la
        // referencia, aqui se hace lo que hacia su puente: la traducida con el lienzo y el estado del Graphics y, si XP llega a una de sus lineas
        // SmoothingMode = AntiAlias (mirado ANTES de dibujar, como el puente: dibujar no lo cambia), se le pone al Graphics.
        private static void PiezaSinPuente(object so, Graphics g, int cual, int dx, int dy)
        {
            Type t = so.GetType();
            int escrito = (int)t.GetField("PiezaEscrito", Todo).GetValue(null), tecla = (int)t.GetField("PiezaTecla", Todo).GetValue(null);
            int fila = (int)Lee(so, "SelRow"), col = (int)Lee(so, "SelCol");
            IList filas = (IList)Lee(so, "rows");
            bool pone = (bool)Lee(so, "Nuevo") || (cual < 2 && (cual == escrito || (cual == tecla && !(bool)Lee(so, "FocusResults") && fila >= 0 && fila < filas.Count && col >= 0 && col < ((IList)filas[fila]).Count)));
            MethodInfo m = null;
            foreach (MethodInfo x in t.GetMethods(Todo)) if (x.Name == "DibujaPiezaViva" && x.GetParameters().Length > 0 && x.GetParameters()[0].ParameterType == typeof(SKCanvas)) m = x;
            object[] es = EstadoDe(g);
            m.Invoke(so, new object[] { es[0], es[1], es[2], es[3], es[4], es[5], es[6], cual, dx, dy });
            if (pone) g.SmoothingMode = SmoothingMode.AntiAlias;
        }

        // ---- las cintas del texto que se desliza (TextoMovilGpu.cs): lo que se llevo la captura (la tarjeta elegida, con el interruptor)
        private static FieldInfo Captura { get { return typeof(Form1).GetField("capturandoCintas", Todo); } }
        private static CintasTexto Captura0() { CintasTexto c = new CintasTexto(); c.Begin(); Captura.SetValue(null, c); return c; }
        private static void Captura1(CintasTexto c) { Captura.SetValue(null, null); if (c != null) c.End(); }
        private static string Cintas(string nombre, CintasTexto c)
        {
            if (c == null) return "";
            StringBuilder sb = new StringBuilder(" cintas=" + c.Items.Count);
            int k = 0;
            foreach (CintaTexto t in c.Items)
            {
                sb.Append(" [" + t.Key + " id=" + t.Identity + " rect=" + R(t.Rect) + " ancho=" + t.Width + " teselas=" + (t.Tiles != null ? t.Tiles.Length : -1));
                if (t.Tiles != null) foreach (object b in (IEnumerable)t.Tiles) sb.Append(" " + Puntos(nombre + "-cinta" + (k++), b).Replace('\t', ' '));
                sb.Append("]");
            }
            return sb.ToString();
        }
        // el tipografico compartido (EstadoReposoGpu.cs le cambia la alineacion y NoWrap: lo heredan las copias que se hagan despues)
        private static string Tipografico()
        {
            StringFormat t = StringFormat.GenericTypographic;
            return " tipografico=" + t.Alignment + "/" + t.LineAlignment + "/" + (int)t.FormatFlags + "/" + t.Trimming;
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
        private static readonly List<object> discos = new List<object>(), canciones = new List<object>();
        private static object Disco(string nombre, string artista, string portada, int pistas, int videos)
        {
            object a = Nuevo(TAlbum);
            Pon(a, "Name", nombre); Pon(a, "Artist", artista); Pon(a, "CoverPath", portada); Pon(a, "Folder", "/musica/" + nombre);
            IList t = (IList)Lee(a, "Tracks"); IList v = (IList)Lee(a, "VideoFiles");
            for (int i = 0; i < pistas; i++)
            {
                object c = Nuevo(TTrack);
                Pon(c, "Path", "/musica/" + nombre + "/" + (i + 1).ToString("00") + ".mp3"); Pon(c, "Title", (i + 1).ToString("00") + " - Pista " + (i + 1)); Pon(c, "Artist", artista); Pon(c, "Album", nombre);
                t.Add(c);
            }
            for (int i = 0; i < videos; i++) v.Add("/videos/" + nombre + "/" + i + ".mpg");
            discos.Add(a);
            return a;
        }
        private static object Item(int kind, string display, object album, object track, object[] albums)
        {
            object it = Nuevo(TItem);
            Pon(it, "Kind", kind); Pon(it, "Display", display); Pon(it, "AlbumRef", album); Pon(it, "TrackRef", track);
            if (albums != null) { IList l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlbum)); foreach (object x in albums) l.Add(x); Pon(it, "Albums", l); }
            return it;
        }
        private static object Cancion(string titulo, string artista, object album, string ruta)
        {
            object c = Nuevo(TTrack);
            Pon(c, "Path", ruta); Pon(c, "Title", titulo); Pon(c, "Artist", artista); Pon(c, "Album", album != null ? Lee(album, "Name") : null);
            canciones.Add(c);
            return c;
        }
        private static IList Lista(IEnumerable<object> items)
        {
            IList l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem));
            foreach (object x in items) l.Add(x);
            return l;
        }

        // ---- un contenido del buscador
        private sealed class Contenido
        {
            public string Nombre, Escrito = "", Query = "", Modo, Toast; public List<object> Res = new List<object>();
            public bool Foco, Chips; public int Sel, Fila = 1, Col, Chip, Total = -1, Scroll; public bool TotalDeQuery = true;
        }

        private static object Buscador(int w, int h, Color acento, string[] atajos)
        {
            object so = Nuevo(SO);
            Pon(so, "Size", new Size(w, h));
            Pon(so, "Accent", acento);
            Metodo("PonNuevo", typeof(bool)).Invoke(so, new object[] { true });
            Pon(so, "GetCoverYa", Delegado("GetCoverYa", (Func<object, Image>)(a => { string p = (string)Lee(a, "CoverPath"); Image i; return p != null && portadas.TryGetValue(p, out i) ? i : null; })));
            Pon(so, "GetGenero", Delegado("GetGenero", (Func<object, string>)(it => { string d = (string)Lee(it, "Display") ?? ""; int k = d.Length % 6; return k == 0 ? "Karaoke" : k == 1 ? "Rock" : k == 2 ? "" : k == 3 ? null : k == 4 ? "Música Regional Mexicana" : "Cumbia"; })));
            Pon(so, "GetQueuePos", Delegado("GetQueuePos", (Func<object, int>)(it => { string d = (string)Lee(it, "Display") ?? ""; return d.StartsWith("La B") ? 3 : d.Length == 11 ? 12 : d.Length == 9 ? 1 : 0; })));
            Pon(so, "GetSonando", Delegado("GetSonando", (Func<object, bool>)(it => ((string)Lee(it, "Display") ?? "").Contains("Bikina"))));
            Pon(so, "GetDuracion", Delegado("GetDuracion", (Func<object, string>)(it => ((int)Lee(it, "Kind") == 2 && ((string)Lee(it, "Display") ?? "").Length % 2 == 0) ? "3:45" : "")));
            Metodo("PonAtajos", typeof(List<string>)).Invoke(so, new object[] { new List<string>(atajos) });
            return so;
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
        private static void PonContenido(object so, Contenido c)
        {
            Pon(so, "Results", Lista(c.Res));
            Pon(so, "Escrito", c.Escrito); Pon(so, "Query", c.Query); Pon(so, "ModoAtajo", c.Modo);
            Pon(so, "TotalResultados", c.Total >= 0 ? c.Total : c.Res.Count); Pon(so, "TotalDe", c.TotalDeQuery ? c.Query : null);
            Pon(so, "FocusResults", c.Foco); Pon(so, "SelResult", c.Sel); Pon(so, "SelRow", c.Fila); Pon(so, "SelCol", c.Col);
            Pon(so, "EnChips", c.Chips); Pon(so, "SelChip", c.Chip); Pon(so, "resScroll", c.Scroll);
            Pon(so, "ToastOn", c.Toast != null); Pon(so, "ToastMsg", c.Toast ?? ""); Pon(so, "ToastBack", Color.FromArgb(238, 40, 165, 90));
        }

        // ---- LA IMAGEN GRANDE: RenderContent(g, caja, true) como EnsureUiOverlay (o directo, la traducida)
        private static void Grande(string nombre, object so, Estado e, bool fondo, bool directo)
        {
            int w = ((Size)Lee(so, "Size")).Width, h = ((Size)Lee(so, "Size")).Height;
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            {
                string ex = "", queda = "";
                MethodInfo m = directo ? Nativa("RenderNuevo") : null;
                try
                {
                    using (Graphics g = Prepara(b, e, fondo))
                    {
                        if (m == null) { Metodo("RenderContent", typeof(Graphics), typeof(Rectangle), typeof(bool)).Invoke(so, new object[] { g, new Rectangle(0, 0, w, h), true }); if (!directo) queda = Queda(g); }
                        else { directas++; object[] es = EstadoDe(g); m.Invoke(so, new object[] { es[0], es[2], es[3], es[4], es[5], es[6] }); }
                    }
                }
                catch (Exception x) { ex = " EXC:" + Interna(x).GetType().Name; excepciones++; }
                StringBuilder at = new StringBuilder(" atajos=");
                foreach (Rectangle r in (IEnumerable)Lee(so, "atajoRects")) at.Append(r.X + "," + r.Y + "," + r.Width + "," + r.Height + ";");
                Linea(nombre, Puntos(nombre, b) + ex + queda + at + " hechos=" + Lee(so, "HorneadosHechos"));
            }
        }

        // ---- LAS IMAGENES CHICAS (0 la tecla o el atajo elegido, 1 la casilla, 2 la tarjeta elegida) y la pieza de las tarjetas (4), como
        // EnsureUiPieza: el sitio (PiezaViva), y la imagen de esa medida; la 4 primero entera y despues por trozos (en el mismo mapa) tras
        // 'cambia', si se da
        private static void Piezas(string nombre, object so, Estado e, bool cintas, bool directo, Action cambia)
        {
            for (int cual = 0; cual <= 4; cual++)
            {
                object[] args = { cual, null };
                bool hay = (bool)Metodo("PiezaViva", typeof(int), typeof(Rectangle).MakeByRefType()).Invoke(so, args);
                Rectangle donde = (Rectangle)args[1];
                string nom = nombre + "-p" + cual;
                if (!hay || donde.Width < 1 || donde.Height < 1) { Linea(nom, "sin pieza " + donde); continue; }
                using (Bitmap b = new Bitmap(donde.Width, donde.Height, PixelFormat.Format32bppPArgb))
                {
                    CintasTexto cap = cintas && cual == 2 ? Captura0() : null;
                    string ex = "", queda = "", trozos = "";
                    try
                    {
                        using (Graphics g = Prepara(b, e, false))
                        {
                            if (cual == 4)
                            {
                                trozos = " entera=" + Cartas(so, g, -donde.X, -donde.Y, true, directo);
                                if (cambia != null)
                                {
                                    cambia();
                                    string antes = Puntos(nom + "-e", b).Replace('\t', ' ');
                                    trozos += " [" + antes + "] trozos=" + Cartas(so, g, -donde.X, -donde.Y, false, directo);
                                }
                            }
                            else
                            {
                                MethodInfo m = directo ? Nativa("DibujaPiezaVivaNueva") : null;
                                if (m == null && Metodo("DibujaPiezaViva", typeof(Graphics), typeof(int), typeof(int), typeof(int)) == null) PiezaSinPuente(so, g, cual, -donde.X, -donde.Y);   // (tanda 4: la traduccion ya no tiene la firma de XP)
                                else if (m == null) Metodo("DibujaPiezaViva", typeof(Graphics), typeof(int), typeof(int), typeof(int)).Invoke(so, new object[] { g, cual, -donde.X, -donde.Y });
                                else { directas++; object[] es = EstadoDe(g); m.Invoke(so, new object[] { es[0], es[2], es[3], es[4], es[5], cual, -donde.X, -donde.Y }); }
                            }
                            if (!directo) queda = Queda(g);
                        }
                    }
                    catch (Exception x) { ex = " EXC:" + Interna(x).GetType().Name; excepciones++; }
                    finally { Captura1(cap); }
                    Linea(nom, donde + "\t" + Puntos(nom, b) + ex + trozos + queda + Cintas(nom, cap) + LlenaCartas(so, cual));
                    if (cap != null) cap.Dispose();
                }
            }
        }
        private static string Cartas(object so, Graphics g, int dx, int dy, bool entera, bool directo)
        {
            MethodInfo m = directo ? Nativa("PintaCartasConCambios") : null;
            // la firma de XP de PintaCartasConCambios (con un Graphics) se quito del puente al traducirse Form1.Video.cs (grupo video-form1): donde
            // ya no esta (la traduccion), la llamada de XP es la traducida con el lienzo y el estado del Graphics, como hacia el puente
            MethodInfo xp = m == null ? Metodo("PintaCartasConCambios", typeof(Graphics), typeof(int), typeof(int), typeof(bool)) : null;
            object r;
            // (la traduccion ya no tiene la firma de XP desde la junta de video-form1: EnsureUiPieza la llama directo; la traducida con el estado
            // del Graphics, que XP no le cambia -su Save/Restore es del lienzo-)
            if (m == null && xp == null) { object[] es = EstadoDe(g); r = Nativa("PintaCartasConCambios").Invoke(so, new object[] { es[0], es[1], es[2], es[3], es[4], es[5], dx, dy, entera }); }
            else if (m == null) r = xp.Invoke(so, new object[] { g, dx, dy, entera });
            else { directas++; object[] es = EstadoDe(g); r = m.Invoke(so, new object[] { es[0], es[1], es[2], es[3], es[4], es[5], dx, dy, entera }); }
            if (r == null) return "null";
            StringBuilder sb = new StringBuilder();
            foreach (Rectangle x in (Rectangle[])r) sb.Append(x.X + "," + x.Y + "," + x.Width + "," + x.Height + ";");
            return "[" + sb + "]";
        }
        // las tarjetas de la imagen para la tarjeta grafica (LlenaCartas), con todas ya entradas
        private static string LlenaCartas(object so, int cual)
        {
            if (cual != 4) return "";
            CartaGpu[] buf = new CartaGpu[24];
            object[] args = { buf, 1e15, 0f };
            int k = (int)SO.GetMethod("LlenaCartas", Todo).Invoke(so, args);
            StringBuilder sb = new StringBuilder(" cartas=" + k + " elegida=" + R((float)args[2]) + ":");
            for (int i = 0; i < k; i++) sb.Append(" " + R(buf[i].X0) + "," + R(buf[i].Y0) + "," + R(buf[i].X1) + "," + R(buf[i].Y1) + "," + R(buf[i].Alfa) + "," + R(buf[i].Dy));
            return sb.ToString();
        }

        // ---- la miniatura de una portada (Escala): en la referencia recibe Image y da Bitmap; en la traduccion, SKBitmap
        private static void Miniatura(string nombre, Image img, int lado)
        {
            MethodInfo m = SO.GetMethod("Escala", Todo);
            object arg = m.GetParameters()[0].ParameterType == typeof(SKBitmap) ? Por("Mapa", typeof(Image), img) : (object)img;
            string ex = ""; object r = null;
            try { r = m.Invoke(null, new object[] { arg, lado }); }
            catch (Exception x) { ex = " EXC:" + Interna(x).GetType().Name; excepciones++; }
            Linea(nombre, (r != null ? Puntos(nombre, r) : "null") + ex);
            if (r is IDisposable) ((IDisposable)r).Dispose();
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            bool rapido = Environment.GetEnvironmentVariable("RAPIDO") == "1";   // (los controles negativos: menos estados, colores y pantallas)
            Linea("inicio", Tipografico());

            // la biblioteca: portadas opaca, con alfa, ancha, alta, diminuta y una que se suelta; discos con y sin portada (y la que no esta en
            // memoria), de videos y de una pista; canciones con acentos, ene y &; un artista con sus discos y la tarjeta de servicio
            portadas["/p/opaca.jpg"] = Portada(600, 600, 1, false);
            portadas["/p/alfa.png"] = Portada(300, 300, 2, true);
            portadas["/p/ancha.jpg"] = Portada(1600, 1000, 3, false);
            portadas["/p/alta.png"] = Portada(500, 900, 4, true);
            portadas["/p/diminuta.gif"] = Portada(3, 3, 5, false);
            Bitmap soltada = Portada(40, 40, 6, false); soltada.Dispose(); portadas["/p/soltada.jpg"] = soltada;
            object d1 = Disco("Grandes Exitos", "Los Bukis", "/p/opaca.jpg", 12, 0);
            object d2 = Disco("Ángel de amor (versión ñandú) & más, edición de lujo remasterizada", "Ñandú y su Orquesta", "/p/alfa.png", 1, 0);
            object d3 = Disco("Sin Portada en Memoria", "Artista X", "/p/falta.jpg", 5, 0);
            object d4 = Disco("Sin Portada", "", null, 0, 0);
            object d5 = Disco("Videos de Fiesta", "Varios", "/p/ancha.jpg", 0, 3);
            object d6 = Disco("Alta y Delgada", "La Banda Alta", "/p/alta.png", 1, 1);
            object d7 = Disco("Mini", "El Diminuto", "/p/diminuta.gif", 2, 0);
            object d8 = Disco("Soltada", "Artista Soltado", "/p/soltada.jpg", 3, 0);
            List<object> todos = new List<object>
            {
                Item(0, "Los Bukis", null, null, new[] { d4, d1, d3 }),
                Item(1, "Grandes Exitos", d1, null, null),
                Item(2, "La Bikina", d1, Cancion("03 - La Bikina", "Rubén Fuentes", d1, "/musica/a/03.mp3"), null),
                Item(2, "Ángel de amor", d2, Cancion("-07 Ángel de amor (versión ñandú) & más, un titulo larguisimo que no cabe", "Ñandú y su Orquesta", d2, "/musica/b/07.mp3"), null),
                Item(1, "Sin Portada en Memoria", d3, null, null),
                Item(1, "", d4, null, null),
                Item(1, "Videos de Fiesta", d5, null, null),
                Item(2, "La cancion sin artista", d3, Cancion("La cancion sin artista", "", d3, "/musica/c/01.mp3"), null),
                Item(0, "La Banda Alta", null, null, new[] { d6 }),
                Item(2, "Mini cancion", d7, Cancion("01 Mini cancion", null, d7, "/musica/d/01.mp3"), null),
                Item(1, "Soltada", d8, null, null),
                Item(0, "Artista sin discos", d1, null, null),
                Item(2, "La-la-la & la", null, Cancion(null, "Nadie", null, "/musica/e/01.mp3"), null),
            };
            for (int i = 0; i < 18; i++) todos.Add(Item(i % 3, "Relleno " + i + (i % 2 == 0 ? " la" : ""), i % 4 == 0 ? d1 : i % 4 == 1 ? d2 : i % 4 == 2 ? d3 : d6, i % 3 == 2 ? Cancion("Relleno " + i, "Artista " + i, d1, "/musica/r/" + i + ".mp3") : null, i % 3 == 0 ? new[] { d1, d2 } : null));
            object servicio = Item(3, "Clave de servicio", null, null, null);

            string[] atajos = { "Lo más pedido hoy", "Videos", "Karaoke", "Buscaste: La Bikina", "Buscaste: Ángel De Amor…" };
            List<Contenido> contenidos = new List<Contenido>
            {
                new Contenido { Nombre = "vacio" },
                new Contenido { Nombre = "escrito", Escrito = "la bik", Query = "la", Res = todos.GetRange(0, 7), Total = 1234 },
                new Contenido { Nombre = "foco", Escrito = "la", Query = "la", Res = todos, Foco = true, Sel = 3, Total = 31 },
                new Contenido { Nombre = "foco-abajo", Escrito = "la", Query = "la", Res = todos, Foco = true, Sel = 13, Scroll = 4 },
                new Contenido { Nombre = "atajo", Modo = "Lo más pedido hoy", Res = todos.GetRange(2, 5), Foco = true, Sel = 0 },
                new Contenido { Nombre = "largo", Escrito = "Ángel de amor de la versión ñandú & más, y otra cosa que no cabe en la casilla de ninguna manera posible", Query = "Ángel de amor de la versión ñandú & más", Res = new List<object>() },
                new Contenido { Nombre = "chips", Chips = true, Chip = 2, Escrito = "x", Query = "x", Res = todos.GetRange(10, 3), TotalDeQuery = false },
                new Contenido { Nombre = "borrar", Fila = 4, Col = 1, Escrito = "ñ", Query = "ñ", Res = todos.GetRange(1, 1) },
                new Contenido { Nombre = "servicio", Escrito = "1234", Query = "1234", Res = new List<object> { servicio }, Foco = true, Toast = "Pusiste «La Bikina», va la 3.ª en la cola" },
                new Contenido { Nombre = "atajo-vacio", Modo = "Karaoke", Res = new List<object>(), Fila = 0, Col = 9 },
            };
            Color[] acentos = { Color.FromArgb(120, 160, 255), Color.FromArgb(3, 5, 4), Color.FromArgb(255, 221, 132) };

            // 1) LA PANTALLA DE SIEMPRE (1024x768): los once estados, los diez contenidos y los tres colores; la pieza de las tarjetas, entera y
            // despues por trozos (una portada que llega, la cola que cambia); la tarjeta elegida con y sin la captura de las cintas
            int caso = 0;
            foreach (Estado e in rapido ? new[] { Estados[0], Estados[2], Estados[3], Estados[6] } : Estados)
                for (int ac = 0; ac < (rapido ? 1 : acentos.Length); ac++)
                {
                    object so = Buscador(1024, 768, acentos[ac], atajos);
                    foreach (Contenido c in contenidos)
                    {
                        PonContenido(so, c);
                        string nom = "s1024-" + e.Nombre + "-a" + ac + "-" + c.Nombre;
                        Grande(nom, so, e, caso % 3 == 1, false);
                        Action cambia = null;
                        if (caso % 2 == 0) cambia = delegate { portadas["/p/falta.jpg"] = portadas["/p/alfa.png"]; };
                        else cambia = delegate { Pon(so, "ColaVer", (int)Lee(so, "ColaVer") + 1); Pon(so, "GetQueuePos", Delegado("GetQueuePos", (Func<object, int>)(it => ((string)Lee(it, "Display") ?? "").Length % 3))); };
                        Piezas(nom, so, e, caso % 4 < 2, false, cambia);
                        portadas.Remove("/p/falta.jpg");
                        caso++;
                    }
                    Linea("s1024-" + e.Nombre + "-a" + ac + "-hornos", "hechos=" + Lee(so, "HorneadosHechos") + " guardados=" + SO.GetMethod("HorneadosGuardados", Todo).Invoke(so, null));
                    SO.GetMethod("SueltaHorneados", Todo).Invoke(so, null);
                }

            // 2) OTRAS PANTALLAS (de la escala 0,6 a la 2): dos estados
            foreach (Size t in rapido ? new[] { new Size(640, 480), new Size(1920, 1080) } : new[] { new Size(640, 480), new Size(800, 600), new Size(1280, 720), new Size(1366, 768), new Size(1920, 1080), new Size(2560, 1440) })
                foreach (Estado e in rapido ? new[] { Estados[0] } : new[] { Estados[0], Estados[3] })
                {
                    object so = Buscador(t.Width, t.Height, acentos[(t.Width / 10) % 3], atajos);
                    foreach (Contenido c in contenidos)
                    {
                        PonContenido(so, c);
                        string nom = "s" + t.Width + "x" + t.Height + "-" + e.Nombre + "-" + c.Nombre;
                        Grande(nom, so, e, false, false);
                        Piezas(nom, so, e, true, false, delegate { Pon(so, "PortadasVer", (int)Lee(so, "PortadasVer") + 1); });
                    }
                }

            // 3) EL TIPOGRAFICO COMPARTIDO CAMBIADO como lo deja EstadoReposoGpu.cs (Center, Center y NoWrap): un buscador nuevo hace sus formatos
            // con el (AseguraFuentes copia el tipografico tal como este); despues se deja como estaba
            {
                StringFormat t = StringFormat.GenericTypographic;
                StringAlignment al = t.Alignment, la = t.LineAlignment; StringFormatFlags ff = t.FormatFlags;
                t.Alignment = StringAlignment.Center; t.LineAlignment = StringAlignment.Center; t.FormatFlags |= StringFormatFlags.NoWrap;
                Linea("tipografico-cambiado", Tipografico());
                object so = Buscador(1024, 768, acentos[0], atajos);
                foreach (Contenido c in contenidos)
                {
                    PonContenido(so, c);
                    string nom = "tipografico-" + c.Nombre;
                    Grande(nom, so, Estados[0], false, false);
                    Piezas(nom, so, Estados[0], true, false, null);
                }
                t.Alignment = al; t.LineAlignment = la; t.FormatFlags = ff;
                Linea("tipografico-vuelto", Tipografico());
            }

            // 4) DIRECTO (en la traduccion, las traducidas con el lienzo y su estado; en la referencia, la llamada de XP otra vez)
            foreach (Estado e in rapido ? new[] { Estados[2] } : new[] { Estados[0], Estados[2], Estados[3], Estados[4], Estados[7] })
            {
                object so = Buscador(1024, 768, acentos[2], atajos);
                foreach (Contenido c in contenidos)
                {
                    PonContenido(so, c);
                    string nom = "directo-" + e.Nombre + "-" + c.Nombre;
                    Grande(nom, so, e, true, true);
                    Piezas(nom, so, e, true, true, delegate { Pon(so, "DatosVer", (int)Lee(so, "DatosVer") + 1); });
                }
            }

            // 5) LA MINIATURA DE UNA PORTADA (Escala): las cinco portadas a cinco lados, y la soltada
            foreach (KeyValuePair<string, Image> p in portadas)
                foreach (int lado in new[] { 67, 4, 128, 1, 301 })
                    Miniatura("escala-" + System.IO.Path.GetFileNameWithoutExtension(p.Key) + "-" + lado, p.Value, lado);

            Linea("fin", Tipografico() + " lineas=" + n);
            Console.Error.WriteLine("lineas " + n + ", directas " + directas + ", excepciones " + excepciones);
            return 0;
        }
    }
}

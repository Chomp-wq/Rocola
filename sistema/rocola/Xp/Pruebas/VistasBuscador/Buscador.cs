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
//     (Plataforma/Puente.buscador.cs) y de ahi a SkiaSharp directo. Desde la tanda 4 (grupo directas-buscador-video) la traduccion ya no
//     tiene las firmas de XP de BuscaView.RenderContent(g, forGpu) ni de DibujaPiezaViva(g, ...) (DrawCtrlOverlay y EnsureUiPieza llaman
//     directo a las traducidas): aqui se llama la traducida y se le deja al Graphics lo que le dejaba XP (CasillaSinPuente, PiezaSinPuente).
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
using Banco;
using Rocola;
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
        private static unsafe string Puntos(string nombre, object b)
        {
            SKBitmap s = Pru.Sk(b); if (s == null) return "soltado";
            byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }

        // ---- el estado del Graphics del que llama
        private sealed class Estado
        {
            public string Nombre; public ModoSuave S; public ModoLetra T; public ModoCopia C; public ModoPixel P;
            public ModoFiltro I = ModoFiltro.Bilinear; public int Trans; public bool Recorte;
        }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "onpaint", S = ModoSuave.None, T = ModoLetra.ClearTypeGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "xp", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "precalienta", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "sin-suavizar", S = ModoSuave.None, T = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.NearestNeighbor },
            new Estado { Nombre = "copia-medio-traslada", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceCopy, P = ModoPixel.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala", S = ModoSuave.AntiAlias, T = ModoLetra.ClearTypeGridFit, C = ModoCopia.SourceOver, P = ModoPixel.HighQuality, Trans = 2, I = ModoFiltro.HighQualityBicubic },
            new Estado { Nombre = "unbit-gira", S = ModoSuave.AntiAlias, T = ModoLetra.SingleBitPerPixelGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = ModoSuave.None, T = ModoLetra.SingleBitPerPixel, C = ModoCopia.SourceCopy, P = ModoPixel.Default, Recorte = true },
            new Estado { Nombre = "traslada-recorte", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 4, Recorte = true, I = ModoFiltro.HighQualityBilinear },
            new Estado { Nombre = "suave-sin-ajuste", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.None, Trans = 5, I = ModoFiltro.Bicubic },
            new Estado { Nombre = "ui-trasladada", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 6, I = ModoFiltro.Low },
        };
        private static Lz Prepara(object b, Estado e, bool fondo)
        {
            Lz g = Pru.Lienzo(b);
            if (fondo)
            {
                g.Limpia(Color.FromArgb(120, 30, 60, 90));
                g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, Pru.Sk(b).Height / 3, Pru.Sk(b).Width, 9);
            }
            else g.Limpia(Color.Transparent);
            g.Suavizado = e.S; g.Letra = e.T; g.Copia = e.C; g.Pixel = e.P; g.Filtro = e.I;
            if (e.Trans == 1) g.Traslada(3.5f, 2.25f); else if (e.Trans == 2) g.Escala(1.3f, 1.3f); else if (e.Trans == 3) g.Gira(7f);
            else if (e.Trans == 4) g.Traslada(0, -40); else if (e.Trans == 5) g.Traslada(0.5f, 0f); else if (e.Trans == 6) g.Traslada(3, 5);
            if (e.Recorte) g.Recorta(new RectangleF(30.5f, 8, Pru.Sk(b).Width * 0.6f, Pru.Sk(b).Height * 0.7f));
            return g;
        }
        private static string Queda(Lz g)
        {
            float[] m = g.Matriz6();
            return " queda=" + g.Suavizado + "/" + g.Letra + "/" + g.Copia + "/" + g.Filtro + "/" + g.Pixel
                + "/" + R(m[0]) + "," + R(m[1]) + "," + R(m[2]) + "," + R(m[3]) + "," + R(m[4]) + "," + R(m[5]) + "/" + R(g.LimitesRecorte);
        }

        // ---- reflexion (las vistas, SearchItem, Album y Track son privados de Form1)
        private static object Nuevo(Type t) { return Activator.CreateInstance(t, true); }
        // (cache-portadas, 12-11-2026; oleada 8b) los mapas de prueba son objetos de Pru (el Bitmap de la capa en la referencia, el SKBitmap en el
        // programa nuevo, que es lo que guarda su cache de portadas): lo que el arnes da como portada, o un delegado que la devuelve, ya es del tipo
        // de cada lado y solo hay que adaptar el tipo de lo que devuelve el delegado
        private static object Adapta(Type destino, object valor)
        {
            if (valor == null || destino.IsInstanceOfType(valor)) return valor;
            if (typeof(Delegate).IsAssignableFrom(destino) && valor is Delegate)
            {
                MethodInfo inv = destino.GetMethod("Invoke"), de = valor.GetType().GetMethod("Invoke");
                ParameterInfo[] ips = inv.GetParameters(), dps = de.GetParameters();
                System.Linq.Expressions.ParameterExpression[] ps = new System.Linq.Expressions.ParameterExpression[ips.Length];
                System.Linq.Expressions.Expression[] args = new System.Linq.Expressions.Expression[ips.Length];
                for (int i = 0; i < ips.Length; i++) { ps[i] = System.Linq.Expressions.Expression.Parameter(ips[i].ParameterType, "p" + i); args[i] = System.Linq.Expressions.Expression.Convert(ps[i], dps[i].ParameterType); }
                System.Linq.Expressions.Expression cuerpo = System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(valor), args);
                if (inv.ReturnType != de.ReturnType) cuerpo = System.Linq.Expressions.Expression.Convert(cuerpo, inv.ReturnType);
                return System.Linq.Expressions.Expression.Lambda(destino, cuerpo, ps).Compile();
            }
            return valor;
        }
        private static void Pon(object o, string nombre, object v)
        {
            FieldInfo f = o.GetType().GetField(nombre, Todo);
            if (f != null) { f.SetValue(o, Adapta(f.FieldType, v)); return; }
            o.GetType().GetProperty(nombre, Todo).SetValue(o, v);
        }
        private static object Lee(object o, string nombre)
        {
            FieldInfo f = o.GetType().GetField(nombre, Todo);
            return f != null ? f.GetValue(o) : o.GetType().GetProperty(nombre, Todo).GetValue(o);
        }
        private static MethodInfo Metodo(Type t, string nombre, params Type[] tipos) { return t.GetMethod(nombre, Todo, null, tipos, null); }
        // (vistas-buscador) la medida de una vista: en la referencia (Control) su Size; en la traduccion (sin herencia) SetBounds en (0, 0) y
        // Width/Height de su CajaVista, que es lo mismo que Control.Size con la esquina en (0, 0)
        private static void PonMedida(object o, int w, int h)
        {
            PropertyInfo s = o.GetType().GetProperty("Size", Todo);
            if (s != null) { s.SetValue(o, new Size(w, h)); return; }
            o.GetType().GetMethod("SetBounds", new[] { typeof(int), typeof(int), typeof(int), typeof(int) }).Invoke(o, new object[] { 0, 0, w, h });
        }
        private static Size Medida(object o)
        {
            PropertyInfo s = o.GetType().GetProperty("Size", Todo);
            return s != null ? (Size)s.GetValue(o) : new Size((int)Lee(o, "Width"), (int)Lee(o, "Height"));
        }
        private static Exception Interna(Exception x) { return x is TargetInvocationException && x.InnerException != null ? x.InnerException : x; }
        // la traducida, llamada directo; null en la referencia (no la hay)
        private static MethodInfo Nativa(Type t, string nombre)
        {
            foreach (MethodInfo m in t.GetMethods(Todo)) if (m.Name == nombre && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(SKCanvas)) return m;
            return null;
        }
        // el lienzo y su estado (lienzo, suave, letra, copia, filtro, medio, hdc) de un lienzo de prueba, en el orden de las traducidas
        private static object[] EstadoDe(Lz g) { return g.Estado(); }

        // ---- las portadas de prueba: puntos al azar con semilla fija (opacas o con alfa), escritas con LockBits como XP
        private static unsafe object Portada(int w, int h, int semilla, bool alfa)
        {
            object b = Pru.NuevoMapa(w, h, "Format32bppArgb");
            uint[] puntos = new uint[w * h];
            Random r = new Random(semilla);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = alfa ? (x * 7 + y * 3 + semilla * 11) & 255 : 255;
                    puntos[y * w + x] = ((uint)a << 24) | (uint)((r.Next(256) << 16) | (((x * 255) / Math.Max(1, w - 1)) << 8) | ((y * 255) / Math.Max(1, h - 1)));
                }
            Pru.EscribePuntos(b, puntos);
            return b;
        }

        // ---- la biblioteca de prueba
        private static readonly Dictionary<string, object> portadas = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
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
            if (a[1] != f.GetType().GetMethod("Invoke").ReturnType) cuerpo = System.Linq.Expressions.Expression.Convert(cuerpo, a[1]);   // (cache-portadas) la portada de la cache, SKBitmap en la traduccion
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
            PonMedida(so, w, h);
            Pon(so, "Accent", acento);
            Metodo(SO, "PonNuevo", typeof(bool)).Invoke(so, new object[] { false });
            Pon(so, "GetCover", Delegado("GetCover", (Func<object, object>)(a => { string p = (string)Lee(a, "CoverPath"); object i; return p != null && portadas.TryGetValue(p, out i) ? i : null; })));
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
            int w = Medida(so).Width, h = Medida(so).Height;
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(w, h, "Format32bppPArgb"))
            {
                string ex = "", queda = "";
                MethodInfo m = directo ? Nativa(SO, "RenderContent") : null;
                try
                {
                    using (Lz g = Prepara(b, e, fondo))
                    {
                        if (cielo != null) { g.Recorta(new Rectangle(20, 20, w / 2, h / 3)); TSky.GetField("shownLayer", Todo).SetValue(cielo, 0); }
                        if (m == null) { GrandeSinPuente(so, g, caja, forGpu); queda = directo ? " filtro=" + (int)g.Filtro : Queda(g); }
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
        // (vistas-buscador) LA FIRMA DE XP DE SearchOverlay.RenderContent(Graphics, Rectangle, bool) (la de su OnPaint): desde la oleada 6c ya no
        // esta en la traduccion (Pinta llama a la traducida). Aqui se hace lo que hacia su puente: la traducida con el lienzo y el estado del
        // Graphics y despues lo que XP le dejaba al Graphics: el suavizado (si pasa de su primer return, 60x60) y el filtro que le devuelve
        private static void GrandeSinPuente(object so, Lz g, Rectangle caja, bool forGpu)
        {
            MethodInfo xp = Pru.Capa ? Metodo(SO, "RenderContent", Pru.TLienzo, typeof(Rectangle), typeof(bool)) : null;
            if (xp != null) { xp.Invoke(so, new object[] { g.Objeto, caja, forGpu }); return; }
            object[] es = EstadoDe(g);
            object[] args = { es[0], es[2], es[3], es[4], es[5], es[6], caja, forGpu };
            Nativa(SO, "RenderContent").Invoke(so, args);
            Size t = Medida(so);
            if (t.Width >= 60 && t.Height >= 60) g.Suavizado = ModoSuave.AntiAlias;
            g.Filtro = (ModoFiltro)(int)args[3];
        }

        // (vistas-buscador) SU PINTURA DE UN WM_PAINT, COMO LA DA LA CAPA: Escritorio.PintaArbol/PintaSolo trasladan el Graphics a su sitio, lo
        // recortan a ella, guardan el estado y sellan el recorte (la matriz del HDC); despues PintaFondoYContenido (OnPaintBackground de fabrica
        // y OnPaint). En la referencia eso mismo (el control de la capa); en la traduccion, lo que hace su hueco: Pinta en el lienzo de ese mismo
        // Graphics (el fondo de fabrica de su hueco no pinta nada)
        private static void PintaComoLaCapa(string nombre, object vista, int x, int y, int w, int h, Size lienzo)
        {
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(lienzo.Width, lienzo.Height, "Format32bppPArgb"))
            {
                string ex = "";
                MethodInfo pinta = vista.GetType().GetMethod("Pinta", Todo, null, new[] { typeof(SKCanvas) }, null);
                MethodInfo pfc = pinta == null ? vista.GetType().GetMethod("PintaFondoYContenido", Todo) : null;
                try
                {
                    using (Lz g = Pru.Lienzo(b))
                    {
                        g.Limpia(Color.FromArgb(120, 30, 60, 90));
                        g.Traslada(x, y); g.Intersecta(new Rectangle(0, 0, w, h));
                        object st = g.Guarda();
                        g.Sella();
                        if (pinta != null) { directas++; pinta.Invoke(vista, new[] { g.Estado()[0] }); }
                        else pfc.Invoke(vista, new object[] { g.Objeto, new Rectangle(0, 0, w, h) });
                        g.Restaura(st);
                    }
                }
                catch (Exception xx) { ex = " EXC:" + Interna(xx).GetType().Name; excepciones++; }
                Linea(nombre, Puntos(nombre, b) + ex);
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
                using (IDisposable b = (IDisposable)Pru.NuevoMapa(donde.Width, donde.Height, "Format32bppPArgb"))
                {
                    string ex = "", queda = "";
                    try
                    {
                        using (Lz g = Prepara(b, e, false))
                        {
                            MethodInfo m = directo ? Nativa(SO, "DibujaPiezaViva") : null;
                            MethodInfo xp = m == null && Pru.Capa ? Metodo(SO, "DibujaPiezaViva", Pru.TLienzo, typeof(int), typeof(int), typeof(int)) : null;
                            if (m == null && xp == null) { PiezaSinPuente(so, g, cual, -donde.X, -donde.Y); queda = directo ? " suave=" + (g.Suavizado == ModoSuave.AntiAlias || g.Suavizado == ModoSuave.HighQuality) : Queda(g); }
                            else if (m == null) { xp.Invoke(so, new object[] { g.Objeto, cual, -donde.X, -donde.Y }); queda = directo ? " suave=" + (g.Suavizado == ModoSuave.AntiAlias || g.Suavizado == ModoSuave.HighQuality) : Queda(g); }
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

        // ---- LA FIRMA DE XP DE DibujaPiezaViva(Graphics, cual, dx, dy): desde la tanda 4 (grupo directas-buscador-video) ya no esta en la
        // traduccion (EnsureUiPieza de Form1.Video.cs llama directo a la traducida). Para seguir comparando lo que le queda al Graphics con la
        // referencia, aqui se hace lo que hacia su puente: la traducida con el lienzo y el estado del Graphics y, si XP llega a una de sus lineas
        // SmoothingMode = AntiAlias (mirado ANTES de dibujar, como el puente: dibujar no lo cambia), se le pone al Graphics.
        private static void PiezaSinPuente(object so, Lz g, int cual, int dx, int dy)
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
            if (pone) g.Suavizado = ModoSuave.AntiAlias;
        }

        // ---- LA FIRMA DE XP DE BuscaView.RenderContent(Graphics, forGpu): tampoco esta ya en la traduccion (DrawCtrlOverlay llama directo a la
        // traducida). forGpu = false es la de un parametro (RenderContent(g) { RenderContent(g, false); } en XP, que sigue en el puente para
        // OnPaint); forGpu = true, la traducida con el lienzo y el estado del Graphics y despues lo que le dejaba XP (el suavizado, si pasa de su
        // primer return).
        private static void CasillaSinPuente(object bv, Lz g, bool forGpu)
        {
            MethodInfo xp1 = Pru.Capa ? Metodo(BV, "RenderContent", Pru.TLienzo) : null;
            if (!forGpu && xp1 != null) { xp1.Invoke(bv, new object[] { g.Objeto }); return; }
            object[] es = EstadoDe(g);
            Nativa(BV, "RenderContent").Invoke(bv, new object[] { es[0], es[2], es[3], es[5], forGpu });
            Size t = Medida(bv);
            if (t.Width >= 30 && t.Height >= 12) g.Suavizado = ModoSuave.AntiAlias;
        }

        // ---- LA CASILLA DE BUSCAR (BuscaView)
        private static void Casilla(string nombre, object bv, Estado e, int forma, bool directo)
        {
            Size t = Medida(bv);
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(Math.Max(1, t.Width), Math.Max(1, t.Height), "Format32bppPArgb"))
            {
                string ex = "", queda = "";
                MethodInfo m = directo ? Nativa(BV, "RenderContent") : null;
                try
                {
                    using (Lz g = Prepara(b, e, forma == 2))
                    {
                        if (m == null)
                        {
                            if (forma == 0) CasillaSinPuente(bv, g, false);
                            else if (Pru.Capa && Metodo(BV, "RenderContent", Pru.TLienzo, typeof(bool)) != null) Metodo(BV, "RenderContent", Pru.TLienzo, typeof(bool)).Invoke(bv, new object[] { g.Objeto, forma == 1 });
                            else CasillaSinPuente(bv, g, forma == 1);
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
                        PonMedida(bv, alto == 11 ? 25 : ancho, alto); Pon(bv, "Abierto", abierto); Pon(bv, "Phase", fase);
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
            // traduccion la traducida de Form1.Pared.cs, directo (como la llama Form1.Buscador.cs desde la tanda 4; antes por su puente,
            // GlowAroundBuscador), con el lienzo y el estado del Graphics; en la referencia la de XP con el Graphics
            MethodInfo halo = Nativa(typeof(Form1), "GlowAround");
            MethodInfo haloXp = Pru.Capa ? Metodo(typeof(Form1), "GlowAround", Pru.TLienzo, typeof(float), typeof(float), typeof(float), typeof(float), typeof(Color)) : null;
            foreach (Estado e in estados)
                foreach (float nivel in new[] { 0.01f, 0.35f, 1f })
                    foreach (Color col in new[] { Color.FromArgb(228, 72, 72), Color.White })
                    {
                        string nom = "halo-" + e.Nombre + "-" + nivel.ToString(ci) + "-" + col.ToArgb().ToString("x8");
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(90, 64, "Format32bppPArgb"))
                        {
                            string ex = "", queda = "";
                            try
                            {
                                using (Lz g = Prepara(b, e, true))
                                {
                                    if (halo == null) haloXp.Invoke(null, new object[] { g.Objeto, 20.5f, 31f, 22f, nivel, col });
                                    else { directas++; object[] es = EstadoDe(g); halo.Invoke(null, new object[] { es[0], es[1], es[2], es[3], es[5], 20.5f, 31f, 22f, nivel, col }); }
                                    queda = Queda(g);
                                }
                            }
                            catch (Exception x) { ex = " EXC:" + Interna(x).GetType().Name; excepciones++; }
                            Linea(nom, Puntos(nom, b) + ex + queda);
                        }
                    }

            // 5) (vistas-buscador) LA PINTURA DE UN WM_PAINT (OnPaint de XP contra Pinta): la casilla (tres altos, cerrada y abierta) y el
            // buscador (tres contenidos y dos medidas), en su sitio (0, 0) y corrida y recortada como en la ventana
            foreach (int alto in new[] { 11, 26, 52 })
                foreach (bool abierto in new[] { false, true })
                    foreach (int sitio in new[] { 0, 1 })
                    {
                        int ancho = (int)Metodo(BV, "Ancho", typeof(int)).Invoke(null, new object[] { alto });
                        object bv = Nuevo(BV);
                        int w = alto == 11 ? 25 : ancho;
                        PonMedida(bv, w, alto); Pon(bv, "Abierto", abierto); Pon(bv, "Phase", 0.4);
                        PintaComoLaCapa("pinta-casilla-" + alto + "-" + (abierto ? "abierta" : "cerrada") + "-" + sitio, bv, sitio == 0 ? 0 : 7, sitio == 0 ? 0 : 3, w, alto, new Size(w + 20, alto + 10));
                        ((IDisposable)bv).Dispose();
                    }
            foreach (Size t in new[] { new Size(1024, 768), new Size(640, 480), new Size(59, 400) })
            {
                object so = NuevoBuscador(t.Width, t.Height, acentos[0]);
                foreach (Contenido c in new[] { contenidos[0], contenidos[3], contenidos[12] })
                    foreach (int sitio in new[] { 0, 1 })
                    {
                        PonContenido(so, c);
                        PintaComoLaCapa("pinta-buscador-" + t.Width + "x" + t.Height + "-" + c.Nombre + "-" + sitio, so, sitio == 0 ? 0 : 5, sitio == 0 ? 0 : 9, t.Width, t.Height, new Size(t.Width + 12, t.Height + 14));
                    }
            }

            Linea("fin", "lineas=" + n);
            Console.Error.WriteLine("lineas " + n + ", directas " + directas + ", excepciones " + excepciones);
            return 0;
        }
    }
}

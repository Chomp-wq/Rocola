// ARNES DEL GRUPO «ASISTENTE» (port nativo, tanda 2 del dibujo, 9-11-2026): lo que dibujan AsistenteRocola.cs y AsistenteGuiaRocola.cs (los
// globos de la estrella, su contorno, las teclas y los botones dibujados), AyudaCasillaGpu.cs (la casilla de AYUDA: su ancho, su dibujo por
// procesador y con tarjeta, la flecha de abajo) y PulidoGpu.cs (el degradado tramado, DegradadoV), con datos fijos, la huella de cada mapa y lo
// que devuelve cada llamada. Se compila con el programa de una carpeta Xp (VistasAsistente.csproj): la salida de la referencia congelada y la de
// la copia de trabajo tienen que ser iguales linea por linea.
//   - En la referencia las llamadas son las de XP (Graphics, Bitmap, Font, Brush) y van a GDI+ (la capa congelada); en la traduccion se
//     llaman como las llama la traduccion (los globos dan SKBitmap, el contorno un SKPath, las teclas, la flecha y la casilla piden el lienzo
//     y su estado, DegradadoV da un shader). El estado del lienzo del que llama va de ocho maneras (suavizado, los modos de letra, copia, medio
//     pixel, traslacion entera y con decimales, escala, giro y recorte).
//   - Lo privado (la casilla, el horno del degradado) va por reflexion.
//   - Varios casos salen dos veces (la segunda, «directo»): en la traduccion las dos van a la traducida; en la referencia, a la de XP.
//   - (Oleada 8b, 13-11-2026) El arnes ya no nombra la capa de GDI+ ni la de WinForms: los mapas, el lienzo, las letras, los trazos, los
//     pinceles de la referencia, el formato tipografico y el OnPaint van por Pruebas/Comun (Pru, Lz, Fte, Fmt: la capa en la referencia
//     congelada y lo nativo en el programa nuevo).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasAsistente.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Text;
using Banco;
using Rocola;
using SkiaSharp;

namespace BancoAsistente
{
    internal static class Asistente
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta;
        private static int n, directas;   // directas: las llamadas a la traducida sin la firma de XP (en la referencia, ninguna)
        private static string R(float v) { return v.ToString("R", ci); }
        private static string R(RectangleF r) { return "(" + R(r.X) + "," + R(r.Y) + "," + R(r.Width) + "," + R(r.Height) + ")"; }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static string Exc(Exception x) { return "EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }

        // los puntos de un mapa (un Bitmap de la capa o un SKBitmap de la traduccion): tal cual (premultiplicados) y como los sube la tarjeta
        // (LockBits Format32bppArgb: sin premultiplicar)
        private static unsafe string Puntos(string nombre, object o)
        {
            if (o == null) return "null";
            object b = Pru.Aprecia(o, "Format32bppPArgb");
            SKBitmap s = Pru.Sk(b); byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }
        private static void Suelta(object o) { IDisposable d = o as IDisposable; if (d != null) d.Dispose(); }
        // una matriz, por donde lleva el origen y el punto (1, 1)
        private static string Mat(PointF[] p) { return R(p[0].X) + "," + R(p[0].Y) + ";" + R(p[1].X) + "," + R(p[1].Y); }

        // ---- el estado del Graphics del que llama (los ocho de Pruebas/VistasReposo)
        private sealed class Estado { public string Nombre; public ModoSuave S; public ModoLetra T; public ModoCopia C; public ModoPixel P; public int Trans; public bool Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "xp", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "sin-suavizar", S = ModoSuave.None, T = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "copia-medio-traslada", S = ModoSuave.HighQuality, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceCopy, P = ModoPixel.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala", S = ModoSuave.AntiAlias, T = ModoLetra.ClearTypeGridFit, C = ModoCopia.SourceOver, P = ModoPixel.HighQuality, Trans = 2 },
            new Estado { Nombre = "unbit-gira", S = ModoSuave.AntiAlias, T = ModoLetra.SingleBitPerPixelGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = ModoSuave.None, T = ModoLetra.SingleBitPerPixel, C = ModoCopia.SourceCopy, P = ModoPixel.Default, Recorte = true },
            new Estado { Nombre = "traslada-recorte", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 4, Recorte = true },
            new Estado { Nombre = "suave-sin-ajuste", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.None, Trans = 5 },
        };
        // un mapa con fondo (para ver la copia y el alfa) y el Graphics con ese estado
        private static Lz Prepara(object b, Estado e)
        {
            Lz g = Pru.Lienzo(b);
            g.Limpia(Color.FromArgb(120, 30, 60, 90));
            g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, 25, Pru.Sk(b).Width, 9);
            g.Suavizado = e.S; g.Letra = e.T; g.Copia = e.C; g.Pixel = e.P;
            if (e.Trans == 1) g.Traslada(3.5f, 2.25f); else if (e.Trans == 2) g.Escala(1.3f, 1.3f); else if (e.Trans == 3) g.Gira(7f);
            else if (e.Trans == 4) g.Traslada(0, -12); else if (e.Trans == 5) g.Traslada(0.5f, 0f);
            if (e.Recorte) g.Recorta(new RectangleF(10.5f, 6, 120, 40));
            return g;
        }

        // ---- la traduccion, por reflexion (en la referencia no esta)
        private static MethodInfo Metodo(Type t, string nombre, Type primero, int parametros = -1)
        {
            foreach (MethodInfo m in t.GetMethods(Todo))
                if (m.Name == nombre && m.GetParameters().Length > 0 && (primero == null || m.GetParameters()[0].ParameterType == primero) && (parametros < 0 || m.GetParameters().Length == parametros)) return m;
            return null;
        }
        // el lienzo y su estado (lienzo, suave, letra, copia, medio): lo que un archivo traducido lleva en sus variables cuando llama con el
        // mismo dibujo
        private static object[] EstadoDe(Lz g)
        {
            object[] e = g.Estado();   // (el lienzo, suave, letra, copia, filtro, medio)
            return new[] { e[0], e[1], e[2], e[3], e[5] };
        }
        private static object[] Junta(object[] a, params object[] b) { object[] r = new object[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }

        // ================================================================ LOS GLOBOS DE LA ESTRELLA
        private static void Globo(string nombre, Func<object> haz)
        {
            object o = null; string ex = "";
            try { o = haz(); } catch (Exception x) { ex = " " + Exc(x); }
            Linea(nombre, Puntos(nombre, o) + ex + " tipografico=" + Fmt.Tipografico.Horizontal + "/" + Fmt.Tipografico.Banderas);
            Suelta(o);
        }
        private static void Globos()
        {
            int[][] pantallas = { new[] { 1024, 768 }, new[] { 1280, 1024 }, new[] { 800, 600 }, new[] { 1920, 1080 }, new[] { 640, 480 }, new[] { 480, 437 }, new[] { 2560, 1440 } };
            foreach (bool tramada in new[] { true, false })
            {
                Form1.InterfazTramada = tramada;
                string t = tramada ? "tramada" : "lisa";
                foreach (int[] p in pantallas)
                {
                    Size pr = GpuPanel.AsistentePanelRect(p[0], p[1]).Size, gt = GpuPanel.AsistenteGuiaTam(p[0], p[1]);
                    string s = p[0] + "x" + p[1] + "-" + t;
                    for (int sel = 0; sel <= 3; sel++) { int k = sel; Globo("pregunta-" + s + "-" + k, () => Form1.PintaPanelAsistente(pr.Width, pr.Height, 0, 0, k)); }
                    Globo("adios-" + s, () => Form1.PintaAdiosAsistente(pr.Width, pr.Height));
                    // la guia: cada paso con sus textos y sus teclas, como los arma PintaGuiaActual
                    for (int paso = 0; paso <= 13; paso++)
                    {
                        string titulo, texto, teclas; Form1.TextosGuia(paso, out titulo, out texto, out teclas);
                        string arriba = paso % 3 == 0 ? "¡MUY BIEN! SIGUIENTE PASO" : paso % 3 == 1 ? "PASO " + paso + " DE 5" : "SIGAMOS";
                        Globo("guia-" + s + "-" + paso, () => Form1.PintaGuiaAsistente(gt.Width, gt.Height, arriba, titulo, texto, teclas, false));
                    }
                    Globo("guia-logro-" + s, () => Form1.PintaGuiaAsistente(gt.Width, gt.Height, "¡LO LOGRASTE!", "¡Tu canción está en la cola!", "Ya va en camino. Si me necesitas otra vez: ROJO + ABAJO.", "", true));
                    Globo("guia-logro2-" + s, () => Form1.PintaGuiaAsistente(gt.Width, gt.Height, "¡LO LOGRASTE!", "¡Ya sabes moverte!", "Cuando quieras, pon una canción. Si me necesitas: ROJO + ABAJO.", null, true));
                }
                // medidas raras: el globo de un rectangulo (menos de 64), justo 64, angostos (las opciones no caben y la letra se achica), de 1x1
                int[][] raras = { new[] { 63, 200 }, new[] { 200, 63 }, new[] { 64, 64 }, new[] { 150, 300 }, new[] { 220, 260 }, new[] { 1, 1 }, new[] { 0, -5 }, new[] { 900, 120 }, new[] { 333, 517 } };
                foreach (int[] r in raras)
                {
                    string s = r[0] + "x" + r[1] + "-" + t;
                    Globo("pregunta-" + s, () => Form1.PintaPanelAsistente(r[0], r[1], 7, 9, 1));
                    Globo("adios-" + s, () => Form1.PintaAdiosAsistente(r[0], r[1]));
                    Globo("guia-" + s, () => Form1.PintaGuiaAsistente(r[0], r[1], null, null, null, "M B+U/V L R D U  +/", false));
                    Globo("guia-moneda-" + s, () => Form1.PintaGuiaAsistente(r[0], r[1], "PASO 2 DE 3", "Mete una moneda", "Cada canción cuesta 1 crédito. Inserta una moneda y seguimos.", "M", true));
                }
            }
            Form1.InterfazTramada = true;
        }

        // ================================================================ EL CONTORNO DEL GLOBO (AsistenteGloboCamino): verbos, puntos y pesos
        private static void Contornos()
        {
            int[][] medidas = { new[] { 63, 200 }, new[] { 64, 64 }, new[] { 420, 300 }, new[] { 417, 333 }, new[] { 1000, 64 }, new[] { 64, 900 }, new[] { 1, 1 }, new[] { 0, 0 }, new[] { 811, 247 } };
            foreach (int[] m in medidas)
            {
                object o = Form1.AsistenteGloboCamino(m[0], m[1]);
                SKPath p = Pru.Ruta(o);
                StringBuilder sb = new StringBuilder();
                using (SKPath.RawIterator it = p.CreateRawIterator())
                {
                    SKPoint[] pts = new SKPoint[4]; SKPathVerb v;
                    while ((v = it.Next(pts)) != SKPathVerb.Done)
                    {
                        sb.Append((int)v).Append(':');
                        int k = v == SKPathVerb.Move ? 1 : v == SKPathVerb.Line ? 2 : v == SKPathVerb.Quad || v == SKPathVerb.Conic ? 3 : v == SKPathVerb.Cubic ? 4 : 0;
                        for (int i = 0; i < k; i++) sb.Append(BitConverter.SingleToInt32Bits(pts[i].X)).Append(',').Append(BitConverter.SingleToInt32Bits(pts[i].Y)).Append(';');
                        if (v == SKPathVerb.Conic) sb.Append('w').Append(BitConverter.SingleToInt32Bits(it.ConicWeight()));
                        sb.Append('|');
                    }
                }
                SKRect b = p.Bounds;
                Linea("contorno-" + m[0] + "x" + m[1], "relleno=" + p.FillType + " puntos=" + p.PointCount + " verbos=" + p.VerbCount + " caja=" + R(b.Left) + "," + R(b.Top) + "," + R(b.Right) + "," + R(b.Bottom)
                    + " huella=" + Huella(Encoding.ASCII.GetBytes(sb.ToString())));
                Suelta(o);
            }
        }

        // ================================================================ LAS TECLAS DIBUJADAS (TeclaFlecha, PintaTeclas)
        private static void Teclas()
        {
            MethodInfo flecha = Metodo(typeof(Form1), "TeclaFlecha", null), teclas = Metodo(typeof(Form1), "PintaTeclas", null);
            bool trad = flecha.GetParameters()[0].ParameterType == typeof(SKCanvas);
            if (trad) directas += 0;   // (en la traduccion estas dos solo existen con el lienzo y su estado: no tienen firma de XP en el puente)
            Color[] colores = { Color.FromArgb(228, 72, 72), Color.FromArgb(242, 206, 72), Color.FromArgb(104, 112, 134), Color.FromArgb(128, 70, 200, 105) };
            int k = 0;
            foreach (Estado e in Estados)
            {
                foreach (int d in new[] { 12, 20, 31 })
                    for (int dir = 0; dir < 4; dir++)
                    {
                        string nombre = "flecha-" + e.Nombre + "-" + (k++);
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(160, 70, "Format32bppPArgb"))
                        {
                            string ex = "";
                            using (Lz g = Prepara(b, e))
                            {
                                float x = dir % 2 == 0 ? 10.5f : 23f; int cy = 30 + dir;
                                try
                                {
                                    if (trad) flecha.Invoke(null, Junta(EstadoDe(g), x, cy, d, colores[(d + dir) % 4], dir));
                                    else flecha.Invoke(null, new object[] { g.Objeto, x, cy, d, colores[(d + dir) % 4], dir });
                                }
                                catch (Exception xx) { ex = " " + Exc(xx); }
                            }
                            Linea(nombre, Puntos(nombre, b) + ex);
                        }
                    }
                foreach (string ts in new[] { "L/R U/D V", "M", "B+U", "B+L/R", "V", "B", "U/D V", "  +/", "MMM BV", "" })
                    foreach (float tam in new[] { 18.5f, 34f })
                    {
                        string nombre = "teclas-" + e.Nombre + "-" + (k++);
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(420, 70, "Format32bppPArgb"))
                        using (Fte f = new Fte("Trebuchet MS", tam, 1, "Pixel"))
                        {
                            string ex = "", fin = "";
                            using (Lz g = Prepara(b, e))
                            {
                                int d = Math.Max(14, (int)(tam * 1.1f));
                                try
                                {
                                    object r = trad ? teclas.Invoke(null, Junta(EstadoDe(g), ts, 6.25f, 36, d, f.Obj))
                                                    : teclas.Invoke(null, new object[] { g.Objeto, ts, 6.25f, 36, d, f.Obj });
                                    fin = " fin=" + R((float)r);
                                }
                                catch (Exception xx) { ex = " " + Exc(xx); }
                            }
                            Linea(nombre, Puntos(nombre, b) + fin + ex);
                        }
                    }
            }
        }

        // ================================================================ LA CASILLA DE AYUDA (AyudaView)
        // el sitio de la casilla: SetBounds(x, y, ancho, alto) de la casilla, por reflexion (en XP la da Control; en la traduccion, su CajaVista)
        private static void PonSitio(object c, int x, int y, int w, int h) { c.GetType().GetMethod("SetBounds", new[] { typeof(int), typeof(int), typeof(int), typeof(int) }).Invoke(c, new object[] { x, y, w, h }); }
        private static Type Casilla { get { return typeof(Form1).GetNestedType("AyudaView", BindingFlags.NonPublic); } }
        private static void Casillas()
        {
            Type t = Casilla;
            MethodInfo ancho = t.GetMethod("Ancho", Todo);
            // lo que mide (una vez por alto: lo recuerda), de varios altos y en los dos tamanos
            foreach (int alto in new[] { 8, 10, 12, 17, 22, 26, 28, 30, 30, 33, 36, 41, 48, 60, 12 })
                foreach (bool compacta in new[] { false, true })
                    Linea("casilla-ancho-" + alto + "-" + compacta, "" + (int)ancho.Invoke(null, new object[] { alto, compacta }));

            MethodInfo xp = Pru.Capa ? Metodo(t, "RenderContent", Pru.TLienzo) : null, nativa = Metodo(t, "RenderContent", typeof(SKCanvas));
            FieldInfo compactaF = t.GetField("Compacta"), abiertaF = t.GetField("Abierta"), teclaF = t.GetField("Tecla");
            int[][] medidas = { new[] { 160, 36 }, new[] { 120, 28 }, new[] { 205, 33 }, new[] { 29, 36 }, new[] { 200, 11 }, new[] { 30, 12 }, new[] { 96, 46 } };
            int k = 0;
            foreach (Estado e in Estados)
                foreach (int[] m in medidas)
                    for (int v = 0; v < 4; v++)
                    {
                        bool compacta = v == 1 || v == 3, abierta = v >= 2;
                        foreach (bool forGpu in new[] { false, true })
                            foreach (bool directo in new[] { false, true })
                            {
                                string nombre = "casilla-" + (directo ? "directo-" : "") + e.Nombre + "-" + m[0] + "x" + m[1] + "-" + v + (forGpu ? "-gpu" : "-cpu") + "-" + (k++);
                                object c = Activator.CreateInstance(t, true);
                                PonSitio(c, 4, 3, m[0], m[1]);
                                compactaF.SetValue(c, compacta); abiertaF.SetValue(c, abierta);
                                using (IDisposable b = (IDisposable)Pru.NuevoMapa(m[0] + 30, Math.Max(m[1] + 24, 70), "Format32bppPArgb"))
                                {
                                    string ex = "", deja = "";
                                    using (Lz g = Prepara(b, e))
                                    {
                                        g.Traslada(4, 3);   // en su sitio, como DrawCtrlOverlay (Form1.Video.cs)
                                        try
                                        {
                                            if (directo && nativa != null)
                                            {
                                                directas++;
                                                object[] es = EstadoDe(g);
                                                nativa.Invoke(c, new object[] { es[0], es[3], es[4], forGpu });
                                            }
                                            else
                                            {
                                                if (xp != null) xp.Invoke(c, new object[] { g.Objeto, forGpu });
                                                else
                                                {   // la traduccion ya no tiene la firma de XP con un Graphics (se quito con vista-ayuda): la traducida con el estado del Graphics, y lo que la de XP le dejaba al Graphics (el suavizado y el modo de letra, si la casilla tiene medida para pintarse)
                                                    object[] es = EstadoDe(g);
                                                    nativa.Invoke(c, new object[] { es[0], es[3], es[4], forGpu });
                                                    if (!(m[0] < 30 || m[1] < 12)) { g.Suavizado = ModoSuave.AntiAlias; g.Letra = ModoLetra.AntiAlias; }
                                                }
                                                if (!directo) deja = " deja=" + g.Suavizado + "/" + g.Letra + "/" + g.Copia + "/" + g.Pixel;
                                            }
                                        }
                                        catch (Exception xx) { ex = " " + Exc(xx); }
                                    }
                                    Rectangle tecla = (Rectangle)teclaF.GetValue(c);
                                    Linea(nombre, Puntos(nombre, b) + deja + " tecla=" + tecla.X + "," + tecla.Y + "," + tecla.Width + "," + tecla.Height + ex);
                                }
                                ((IDisposable)c).Dispose();
                            }
                    }

            // (revision) LA CASILLA POR OnPaint, COMO LA PINTA EL ESCRITORIO DE WINFORMS (PintaSolo): el Graphics llega con la matriz de su sitio en la
            // del HDC (SellaRecorte: Transform es la identidad y la traslacion queda en la base, «m=I b=T r=hdc» en la tabla de estado por
            // linea) y con el recorte de su ventana sellado. Es la linea de WinForms OnPaint(PaintEventArgs e) { RenderContent(e.Graphics,
            // false); }, que en la traduccion entra por la firma de XP del puente; lo que la traducida le pide a CtrlTxt, CtrlDot y CtrlTri
            // va con otro Graphics sobre el mismo lienzo, con la identidad como matriz del HDC.
            MethodInfo onPaint = Pru.Capa ? t.GetMethod("OnPaint", Todo) : null, pinta = t.GetMethod("Pinta", Todo, null, new[] { typeof(SKCanvas) }, null);   // (OnPaint(PaintEventArgs): solo la referencia)
            k = 0;
            foreach (Estado e in Estados)
                foreach (int[] m in medidas)
                    for (int v = 0; v < 4; v++)
                    {
                        string nombre = "casilla-onpaint-" + e.Nombre + "-" + m[0] + "x" + m[1] + "-" + v + "-" + (k++);
                        object c = Activator.CreateInstance(t, true);
                        PonSitio(c, 9, 5, m[0], m[1]);
                        compactaF.SetValue(c, v == 1 || v == 3); abiertaF.SetValue(c, v >= 2);
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(m[0] + 30, Math.Max(m[1] + 24, 70), "Format32bppPArgb"))
                        {
                            string ex = "", deja = "";
                            using (Lz g = Prepara(b, e))
                            {
                                g.Traslada(9, 5);
                                if (v % 2 == 0) g.Recorta(new Rectangle(0, 0, m[0] - 7, m[1]));   // la parte de su ventana que hay que pintar
                                g.Sella();
                                PointF[] antes = g.Transformados();
                                try
                                {
                                    if (onPaint != null) { Pru.OnPaint(onPaint, c, g, new Rectangle(0, 0, m[0], m[1])); }
                                    else
                                    {   // la traduccion: Pinta(SKCanvas) (IVista) pinta en el estado de fabrica de un WM_PAINT (sin copia ni medio pixel); con un Graphics de otro estado, la traducida con su estado, como la llamaba el puente; y lo que dejaba OnPaint (el suavizado y el modo de letra)
                                        object[] es = EstadoDe(g);
                                        if (!(bool)es[3] && !(bool)es[4]) { pinta.Invoke(c, new object[] { es[0] }); directas++; }
                                        else nativa.Invoke(c, new object[] { es[0], es[3], es[4], false });
                                        if (!(m[0] < 30 || m[1] < 12)) { g.Suavizado = ModoSuave.AntiAlias; g.Letra = ModoLetra.AntiAlias; }
                                    }
                                }
                                catch (Exception xx) { ex = " " + Exc(xx); }
                                PointF[] despues = g.Transformados(); RectangleF rc = g.LimitesRecorte;
                                deja = " deja=" + g.Suavizado + "/" + g.Letra + "/" + g.Copia + "/" + g.Pixel
                                    + " m=" + Mat(antes) + ">" + Mat(despues) + " recorte=" + R(rc);
                            }
                            Rectangle tecla = (Rectangle)teclaF.GetValue(c);
                            Linea(nombre, Puntos(nombre, b) + deja + " tecla=" + tecla.X + "," + tecla.Y + "," + tecla.Width + "," + tecla.Height + ex);
                        }
                        ((IDisposable)c).Dispose();
                    }

            // (vista-ayuda) SU PINTURA POR PROCESADOR, COMO LA DE LA AYUDA DE VistasVideoForm1 (ayuda-pinta): en XP OnPaint(PaintEventArgs) con el Graphics
            // de un WM_PAINT (el de fabrica: Graphics.FromImage, trasladado a su sitio y recortado a el); en la traduccion, Pinta(SKCanvas) (IVista), que
            // pinta su hueco o la pantalla, en el lienzo de ese mismo Graphics
            k = 0;
            foreach (int[] m in medidas)
                for (int v = 0; v < 4; v++)
                    foreach (int sitio in new[] { 0, 1 })
                    {
                        string nombre = "casilla-pinta-" + m[0] + "x" + m[1] + "-" + v + "-" + sitio;
                        object c = Activator.CreateInstance(t, true);
                        PonSitio(c, 0, 0, m[0], m[1]);
                        compactaF.SetValue(c, v == 1 || v == 3); abiertaF.SetValue(c, v >= 2);
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(m[0] + 30, Math.Max(m[1] + 24, 70), "Format32bppPArgb"))
                        {
                            string ex = "";
                            using (Lz g = Pru.Lienzo(b))
                            {
                                g.Limpia(Color.FromArgb(120, 30, 60, 90));
                                if (sitio == 1) { g.Traslada(9, 4); g.Intersecta(new Rectangle(0, 0, m[0], m[1])); }   // (como Escritorio.PintaArbol)
                                try
                                {
                                    if (pinta != null) { pinta.Invoke(c, new[] { g.Estado()[0] }); directas++; }
                                    else Pru.OnPaint(onPaint, c, g, new Rectangle(0, 0, m[0], m[1]));
                                }
                                catch (Exception xx) { ex = " " + Exc(xx); }
                            }
                            Rectangle tecla = (Rectangle)teclaF.GetValue(c);
                            Linea(nombre, Puntos(nombre, b) + " tecla=" + tecla.X + "," + tecla.Y + "," + tecla.Width + "," + tecla.Height + ex);
                        }
                        ((IDisposable)c).Dispose();
                    }

            // la flecha de abajo, sola
            MethodInfo flecha = Metodo(t, "FlechaAbajo", null);
            bool trad = flecha.GetParameters()[0].ParameterType == typeof(SKCanvas);
            k = 0;
            foreach (Estado e in Estados)
                foreach (int d in new[] { 10, 18, 27 })
                {
                    string nombre = "casilla-flecha-" + e.Nombre + "-" + (k++);
                    using (IDisposable b = (IDisposable)Pru.NuevoMapa(90, 60, "Format32bppPArgb"))
                    {
                        string ex = "";
                        using (Lz g = Prepara(b, e))
                        {
                            try
                            {
                                if (trad) flecha.Invoke(null, Junta(EstadoDe(g), 11.75f, 30, d, Color.FromArgb(228, 72, 72)));
                                else flecha.Invoke(null, new object[] { g.Objeto, 11.75f, 30, d, Color.FromArgb(228, 72, 72) });
                            }
                            catch (Exception xx) { ex = " " + Exc(xx); }
                        }
                        Linea(nombre, Puntos(nombre, b) + ex);
                    }
                }
        }

        // ================================================================ EL DEGRADADO TRAMADO (DegradadoV)
        // En la referencia, con la firma de XP (un Brush); en la traduccion, la traducida (el shader con el medio pixel del lienzo en un
        // SKPaint, pintado con Nativo/Pixel.cs como lo pinta la traduccion). Cada caso sale dos veces (la segunda, «directo»). Las dos comparten
        // el guardado de cuatro mapas: las cuentas del final tienen que salir iguales.
        private static readonly Color[][] Colores = {
            new[] { Color.FromArgb(252, 24, 42, 74), Color.FromArgb(248, 10, 18, 37) },   // los globos de la pregunta y la despedida
            new[] { Color.FromArgb(250, 26, 44, 78), Color.FromArgb(246, 10, 18, 37) },   // el de la guia
            new[] { Color.FromArgb(232, 12, 19, 42), Color.FromArgb(248, 7, 12, 29) },    // el panel de reposo
            new[] { Color.FromArgb(205, 26, 37, 59), Color.FromArgb(218, 9, 16, 31) },    // el vidrio de la lista del disco
            new[] { Color.FromArgb(255, 200, 30, 10), Color.FromArgb(0, 10, 200, 30) },   // de opaco a transparente
            new[] { Color.FromArgb(0, 0, 0, 0), Color.FromArgb(1, 255, 255, 255) },
        };
        private static void Degradado(string nombre, Estado e, Rectangle r, Color c0, Color c1, bool directo, bool ruta)
        {
            Type px = typeof(Form1).Assembly.GetType("Rocola.Nativo.Pixel");
            MethodInfo nativa = directo ? Metodo(typeof(Form1), "DegradadoV", typeof(Rectangle), 4) : null;
            // la firma de XP (un Brush): en la referencia; en la traduccion ya no esta (directas-tanda2) y va la traducida, como la directa
            MethodInfo xp = Metodo(typeof(Form1), "DegradadoV", typeof(Rectangle), 3);
            if (!directo && xp == null) nativa = Metodo(typeof(Form1), "DegradadoV", typeof(Rectangle), 4);
            int alto = Math.Min(Math.Max(1, r.Height), 300);
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(Math.Max(1, r.Width) + 30, alto + 24, "Format32bppPArgb"))
            {
                string ex = "";
                using (Lz g = Prepara(b, e))
                using (IDisposable gp = (IDisposable)Pru.RutaElipse(new RectangleF(r.X + 1.5f, r.Y + 2.25f, r.Width * .8f, Math.Min(r.Height, alto) * .9f)))
                {
                    try
                    {
                        if (nativa != null)
                        {
                            directas++;
                            object[] es = EstadoDe(g);
                            SKShader s = (SKShader)nativa.Invoke(null, new object[] { r, c0, c1, es[4] });
                            using (SKPaint p = new SKPaint { Shader = s })
                            {
                                if (ruta) px.GetMethod("Rellena", new[] { typeof(SKCanvas), typeof(SKPath), typeof(SKPaint), typeof(bool), typeof(bool), typeof(bool) }).Invoke(null, new[] { es[0], Pru.Ruta(gp), p, es[1], es[4], es[3] });
                                else px.GetMethod("Rellena", new[] { typeof(SKCanvas), typeof(RectangleF), typeof(SKPaint), typeof(bool), typeof(bool), typeof(bool) }).Invoke(null, new[] { es[0], (RectangleF)r, p, es[1], es[4], es[3] });
                            }
                            s.Dispose();
                        }
                        else
                            using (IDisposable br = (IDisposable)xp.Invoke(null, new object[] { r, c0, c1 }))   // (el Brush de XP: solo la referencia llega aqui)
                            {
                                if (ruta) g.RellenaCon(br, gp); else g.RellenaCon(br, r);
                            }
                    }
                    catch (Exception xx) { ex = " " + Exc(xx); }
                }
                Linea(nombre, Puntos(nombre, b) + ex);
            }
        }
        // los de mas de 2 048 de alto: GDI+ los pinta (su TextureBrush copia el mapa), la capa no (suelta el mapa antes de pintar: la referencia
        // lanza ArgumentNullException al pintar, y la firma de XP del puente tambien, como ella). La traducida directa hace lo de GDI+: se compara
        // con lo que haria GDI+ con el mapa vivo (el mismo horno y el TextureBrush de XP con su traslacion, sin soltar el mapa hasta despues)
        private static void Grande(string nombre, Estado e, Rectangle r, Color c0, Color c1)
        {
            MethodInfo nativa = Metodo(typeof(Form1), "DegradadoV", typeof(Rectangle), 4);
            MethodInfo horno = typeof(Form1).GetMethod("HorneaMapaDegradado", Todo);
            Type px = typeof(Form1).Assembly.GetType("Rocola.Nativo.Pixel");
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(r.Width + 30, 300, "Format32bppPArgb"))
            {
                string ex = "";
                using (Lz g = Prepara(b, e))
                {
                    try
                    {
                        if (nativa != null)
                        {
                            directas++;
                            object[] es = EstadoDe(g);
                            SKShader s = (SKShader)nativa.Invoke(null, new object[] { r, c0, c1, es[4] });
                            using (SKPaint p = new SKPaint { Shader = s })
                                px.GetMethod("Rellena", new[] { typeof(SKCanvas), typeof(RectangleF), typeof(SKPaint), typeof(bool), typeof(bool), typeof(bool) }).Invoke(null, new[] { es[0], (RectangleF)r, p, es[1], es[4], es[3] });
                            s.Dispose();
                        }
                        else
                        {
                            object mapa = horno.Invoke(null, new object[] { r.Height, c0, c1 });
                            g.RellenaTextura(mapa, r.X, r.Y, r);   // (un TextureBrush con el mapa repetido, trasladado a la esquina de la caja)
                            Pru.Suelta(mapa);
                        }
                    }
                    catch (Exception xx) { ex = " " + Exc(xx); }
                }
                Linea(nombre, Puntos(nombre, b) + ex);
            }
        }
        // (revision) LA VIDA DEL PINCEL: en XP el TextureBrush de GDI+ se queda con su copia del mapa («el llamador recibe su propio pincel»),
        // asi que sigue pintando aunque el guardado de cuatro mapas de la vuelta y suelte el suyo. La traducida da el shader: se pide, se
        // piden otros cinco altos (el guardado da la vuelta y suelta su mapa), se fuerza una recoleccion y DESPUES se pinta con el. En la
        // referencia (la capa no copia el mapa en el pincel) se pinta al momento, que es lo que GDI+ pinta, y despues se piden los mismos
        // cinco: las cuentas del guardado tienen que quedar iguales.
        private static void Vida(string nombre, Estado e, Rectangle r, Color c0, Color c1)
        {
            MethodInfo nativa = Metodo(typeof(Form1), "DegradadoV", typeof(Rectangle), 4);
            Type px = typeof(Form1).Assembly.GetType("Rocola.Nativo.Pixel");
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(r.Width + 30, r.Height + 24, "Format32bppPArgb"))
            {
                string ex = "";
                using (Lz g = Prepara(b, e))
                {
                    try
                    {
                        if (nativa != null)
                        {
                            directas++;
                            object[] es = EstadoDe(g);
                            SKShader s = (SKShader)nativa.Invoke(null, new object[] { r, c0, c1, es[4] });
                            for (int i = 1; i <= 5; i++) ((SKShader)nativa.Invoke(null, new object[] { new Rectangle(r.X, r.Y, r.Width, r.Height + i), c1, c0, es[4] })).Dispose();
                            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                            using (SKPaint p = new SKPaint { Shader = s })
                                px.GetMethod("Rellena", new[] { typeof(SKCanvas), typeof(RectangleF), typeof(SKPaint), typeof(bool), typeof(bool), typeof(bool) }).Invoke(null, new[] { es[0], (RectangleF)r, p, es[1], es[4], es[3] });
                            s.Dispose();
                        }
                        else
                        {
                            MethodInfo xp = Metodo(typeof(Form1), "DegradadoV", typeof(Rectangle), 3);   // (la firma de XP: solo la referencia llega aqui)
                            using (IDisposable br = (IDisposable)xp.Invoke(null, new object[] { r, c0, c1 })) g.RellenaCon(br, r);
                            for (int i = 1; i <= 5; i++) ((IDisposable)xp.Invoke(null, new object[] { new Rectangle(r.X, r.Y, r.Width, r.Height + i), c1, c0 })).Dispose();
                        }
                    }
                    catch (Exception xx) { ex = " " + Exc(xx); }
                }
                Linea(nombre, Puntos(nombre, b) + ex + " guardado=" + Form1.DegradadosGuardados + "/" + Form1.DegradadosReusados);
            }
        }
        private static void Degradados()
        {
            Linea("degradados-antes", Form1.DegradadosGuardados + "/" + Form1.DegradadosReusados);
            // medidas: la lineal (alto 1), sin ancho (GDI+ lanza OutOfMemory), sin alto, la tramada de 2, varias, la misma otra vez (se reusa),
            // movida de su esquina (la traslacion), 2048 justo; y tantas distintas que el guardado de cuatro da la vuelta
            Rectangle[] cajas = { new Rectangle(0, 0, 50, 1), new Rectangle(0, 0, 0, 20), new Rectangle(0, 0, 40, 0), new Rectangle(0, 0, 40, 2), new Rectangle(0, 0, 40, 37),
                                  new Rectangle(0, 0, 300, 340), new Rectangle(0, 0, 41, 37), new Rectangle(7, 3, 61, 37), new Rectangle(-5, 9, 33, 211), new Rectangle(0, 0, 60, 2048),
                                  new Rectangle(0, 0, 44, 140), new Rectangle(0, 0, 44, 141), new Rectangle(0, 0, 44, 142), new Rectangle(0, 0, 44, 143), new Rectangle(0, 0, 40, 37) };
            int k = 0;
            foreach (bool tramada in new[] { true, false, true })
            {
                Form1.InterfazTramada = tramada;
                for (int i = 0; i < cajas.Length; i++)
                {
                    Estado e = Estados[(i + k) % Estados.Length];
                    Color[] c = Colores[(i + k / 3) % Colores.Length];
                    Degradado("degradado-" + e.Nombre + "-" + k, e, cajas[i], c[0], c[1], false, i % 3 == 1);
                    Degradado("degradado-directo-" + e.Nombre + "-" + k, e, cajas[i], c[0], c[1], true, i % 3 == 1);
                    Linea("degradados-" + k, Form1.DegradadosGuardados + "/" + Form1.DegradadosReusados);
                    k++;
                }
            }
            // todos los estados con una caja tramada (el medio pixel cambia el shader de la textura)
            Form1.InterfazTramada = true;
            foreach (Estado e in Estados)
                foreach (bool directo in new[] { false, true })
                {
                    Degradado("degradado-estado-" + (directo ? "directo-" : "") + e.Nombre, e, new Rectangle(2, 1, 120, 50), Colores[0][0], Colores[0][1], directo, false);
                    Degradado("degradado-estado-ruta-" + (directo ? "directo-" : "") + e.Nombre, e, new Rectangle(2, 1, 120, 50), Colores[4][0], Colores[4][1], directo, true);
                }
            // los de mas de 2 048: la traducida contra GDI+ con el mapa vivo. (Hasta directas-tanda2 tambien por la firma de XP del puente, que lanzaba
            // ArgumentNullException al pintar como la capa de la referencia; esa firma ya no esta: sus dos llamadores, EstadoReposoGpu.cs y
            // AcabadoGpu.cs, llaman a la traducida, que los pinta como GDI+. Las lineas degradado-grande-xp-* se quitaron de las dos salidas.)
            foreach (Estado e in new[] { Estados[0], Estados[2], Estados[3] })
            {
                Grande("degradado-grande-" + e.Nombre, e, new Rectangle(3, 2, 50, 3000), Colores[1][0], Colores[1][1]);
            }
            // (revision) el pincel despues de que el guardado suelte su mapa
            Form1.InterfazTramada = true;
            foreach (Estado e in Estados)
                Vida("degradado-vida-" + e.Nombre, e, new Rectangle(5, 4, 90, 600 + 10 * Array.IndexOf(Estados, e)), Colores[Array.IndexOf(Estados, e) % Colores.Length][0], Colores[Array.IndexOf(Estados, e) % Colores.Length][1]);
            Linea("degradados-fin", Form1.DegradadosGuardados + "/" + Form1.DegradadosReusados);
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            Linea("inicio", "tipografico=" + Fmt.Tipografico.Horizontal + "/" + Fmt.Tipografico.Banderas);
            Globos();
            Contornos();
            Teclas();
            Casillas();
            Degradados();
            Console.Error.WriteLine("asistente: " + n + " lineas, " + directas + " llamadas directas a la traducida");
            return 0;
        }
    }
}

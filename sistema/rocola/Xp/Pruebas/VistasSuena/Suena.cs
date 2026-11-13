// ARNES DEL GRUPO «SUENA» (port nativo, tanda 2 del dibujo, 8-11-2026): lo que dibuja SuenaVivoGpu.cs con datos fijos, la huella de cada
// mapa y lo que devuelve cada llamada. Se compila con el programa de una carpeta Xp (VistasSuena.csproj): la salida de la referencia congelada
// y la de la copia de trabajo tienen que ser iguales linea por linea.
//   - Las llamadas son las de XP (Graphics, Bitmap, Font, Brush, StringFormat): en la referencia van a GDI+ (la capa congelada) y en la
//     traduccion a SkiaSharp directo. Desde el grupo directas-tanda2 (12-11-2026) la traduccion ya no tiene esas firmas (se fueron con
//     Plataforma/Puente.suena.cs y Puente.reposo.cs: sus llamadores ya estaban traducidos): van por reflexion (Xp*, abajo), a la de XP si la
//     hay (la referencia) o a la traducida con lo mismo que le pasaba aquel puente (PuenteDibujo.Mapa, Letra, Pincel, Formato y el lienzo con su
//     estado). Lo que se dibuja: el aviso de los cuatro tipos (con y sin
//     renglon, textos que no caben), la invitacion, el "+4 creditos", la moneda, y el titulo grande y los minutos de la cola del panel de
//     reposo con ocho estados del Graphics (suavizado, modo de letra, copia, medio pixel, traslacion, escala, giro y recorte), con y sin la
//     cinta del texto que se desliza (TextoMovilGpu.cs: la que pinta PintaTextoMovil, que la traducida llama directo).
//   - Lo que XP llama sin esas firmas va por reflexion: HorneaTituloReposo (el titulo que se desliza) y XNumero (donde aterriza la moneda).
//   - En la traduccion, PintaTituloGrande y PintaMinutosCola se llaman tambien DIRECTO (el lienzo y su estado, la Letra, un SKPaint de cada
//     color y el FormatoTexto), como los llamara EstadoReposoGpu.cs cuando se traduzca; en la referencia esa linea es la de XP otra vez.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasSuena.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Banco;
using Rocola;
using SkiaSharp;

namespace BancoSuena
{
    internal static class Suena
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta;
        private static int n, directas;   // directas: las llamadas a la traducida sin el puente (en la referencia, ninguna)
        private static string R(float v) { return v.ToString("R", ci); }
        private static string R(RectangleF r) { return "(" + R(r.X) + "," + R(r.Y) + "," + R(r.Width) + "," + R(r.Height) + ")"; }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }

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

        // ---- el estado del Graphics del que llama
        private sealed class Estado { public string Nombre; public ModoSuave S; public ModoLetra T; public ModoCopia C; public ModoPixel P; public int Trans; public bool Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "xp", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "sin-suavizar", S = ModoSuave.None, T = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "copia-medio-traslada", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceCopy, P = ModoPixel.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala", S = ModoSuave.AntiAlias, T = ModoLetra.ClearTypeGridFit, C = ModoCopia.SourceOver, P = ModoPixel.HighQuality, Trans = 2 },
            new Estado { Nombre = "unbit-gira", S = ModoSuave.AntiAlias, T = ModoLetra.SingleBitPerPixelGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = ModoSuave.None, T = ModoLetra.SingleBitPerPixel, C = ModoCopia.SourceCopy, P = ModoPixel.Default, Recorte = true },
            new Estado { Nombre = "medio-traslada-recorte", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 4, Recorte = true },
            new Estado { Nombre = "suave-sin-ajuste", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.None },
        };
        // un mapa con fondo (para ver la copia y el alfa) y el Graphics con ese estado
        private static Lz Prepara(object b, Estado e)
        {
            Lz g = Pru.Lienzo(b);
            g.Limpia(Color.FromArgb(120, 30, 60, 90));
            g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, 60, Pru.Sk(b).Width, 9);
            g.Suavizado = e.S; g.Letra = e.T; g.Copia = e.C; g.Pixel = e.P;
            if (e.Trans == 1) g.Traslada(3.5f, 2.25f); else if (e.Trans == 2) g.Escala(1.3f, 1.3f); else if (e.Trans == 3) g.Gira(7f); else if (e.Trans == 4) g.Traslada(0.5f, 0f);
            if (e.Recorte) g.Recorta(new RectangleF(30.5f, 10, 600, 120));
            return g;
        }

        // ---- la cinta del texto que se desliza (TextoMovilGpu.cs): lo que se llevo PintaTextoMovil
        private static FieldInfo Captura { get { return typeof(Form1).GetField("capturandoCintas", Todo); } }
        private static string Cintas(string nombre, CintasTexto c)
        {
            if (c == null) return "";
            StringBuilder sb = new StringBuilder(" cintas=" + c.Items.Count);
            int k = 0;
            foreach (CintaTexto t in c.Items)
            {
                sb.Append(" [" + t.Key + " rect=" + R(t.Rect) + " ancho=" + t.Width);
                // (object: las teselas son Bitmap en la referencia y en esta rama, y SKBitmap cuando TextoMovilGpu.cs este traducido; Puntos lee las dos)
                if (t.Tiles != null) foreach (object b in t.Tiles) sb.Append(" " + Puntos(nombre + "-cinta" + (k++), b).Replace('\t', ' '));
                sb.Append("]");
            }
            return sb.ToString();
        }

        // ---- la traducida, llamada directo (como la llamara EstadoReposoGpu.cs traducido); null si no la hay (la referencia)
        private static MethodInfo Nativa(string nombre)
        {
            foreach (MethodInfo m in typeof(Form1).GetMethods(Todo)) if (m.Name == nombre && m.GetParameters()[0].ParameterType == typeof(SKCanvas)) return m;
            return null;
        }
        // el lienzo y su estado, en el orden de los parametros de las traducidas: lienzo, suave, letra, copia, medio pixel
        private static object[] EstadoDe(Lz g)
        {
            object[] e = g.Estado();
            return new[] { e[0], e[1], e[2], e[3], e[5] };
        }

        // ---- el titulo grande del panel de reposo (PintaTituloGrande)
        private static void TituloGrande(string nombre, Estado e, string title, string artist, string genero, int tx, int tw, bool cinta, bool directo)
        {
            int w = 1024, h = 300;
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(w, h, "Format32bppPArgb"))
            using (Fte small = new Fte("Trebuchet MS", 15 * scale, 1, "Pixel"))
            using (Fte normal = new Fte("Trebuchet MS", 20 * scale, 1, "Pixel"))
            using (Fte big = XpFuenteTituloReposo(w, h))
            {
                object white = Pru.Brocha(Color.FromArgb(240, 245, 255)), color = Pru.Brocha(Color.FromArgb(150, 210, 255));
                Fmt line = new Fmt();
                line.Corte = ModoCorte.EllipsisCharacter; line.Banderas = 4096;
                ReposoGrande x = new ReposoGrande(); x.Genero = genero; x.GeneroColor = Color.FromArgb(230, 90, 160);
                CintasTexto cintas = cinta ? new CintasTexto() : null;
                if (cintas != null) { cintas.Begin(); Captura.SetValue(null, cintas); }
                try
                {
                    using (Lz g = Prepara(b, e))
                    {
                        if (directo && !Pru.Capa) directas++;   // (la traducida se llama igual con «directo» o sin el: no hay otra)
                        XpConGraphics("PintaTituloGrande", g, x, title, artist, big, normal, small, white, color, line, tx, tw);
                    }
                }
                finally { if (cintas != null) { Captura.SetValue(null, null); cintas.End(); } Pru.Suelta(white); Pru.Suelta(color); }
                Linea(nombre, Puntos(nombre, b) + "\tfuera=" + x.TituloFuera + " caja=" + R(x.TituloCaja) + " genero=" + R(x.GeneroCaja) + Cintas(nombre, cintas));
                if (cintas != null) cintas.Dispose();
            }
        }

        // ---- los minutos de la cola (PintaMinutosCola)
        private static void MinutosCola(string nombre, Estado e, string minutos, string artista, bool estrecha, float x, float y, float w, float h, bool cinta, bool directo)
        {
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(700, 160, "Format32bppPArgb"))
            using (Fte f = new Fte("Trebuchet MS", 15.077f, 1, "Pixel"))
            {
                object tenue = Pru.Brocha(Color.FromArgb(157, 179, 209));
                Fmt line = new Fmt();
                line.Corte = ModoCorte.EllipsisCharacter; line.Banderas = 4096;
                CintasTexto cintas = cinta ? new CintasTexto() : null;
                if (cintas != null) { cintas.Begin(); Captura.SetValue(null, cintas); }
                try
                {
                    using (Lz g = Prepara(b, e))
                    {
                        if (directo && !Pru.Capa) directas++;
                        XpConGraphics("PintaMinutosCola", g, minutos, artista, f, tenue, estrecha, x, y, w, h, line);
                    }
                }
                finally { if (cintas != null) { Captura.SetValue(null, null); cintas.End(); } Pru.Suelta(tenue); }
                Linea(nombre, Puntos(nombre, b) + Cintas(nombre, cintas));
                if (cintas != null) cintas.Dispose();
            }
        }

        // ---- el titulo que se desliza, en su imagen (HorneaTituloReposo, privado de Form1: en una Form1 sin construir, que solo usa sus campos)
        private static void TituloReposo(string nombre, object form, string titulo, Fte f)
        {
            MethodInfo m = typeof(Form1).GetMethod("HorneaTituloReposo", Todo);
            m.Invoke(form, new object[] { titulo, f.Obj });
            object bmp = typeof(Form1).GetField("tituloReposoBmp", Todo).GetValue(form);
            Linea(nombre, Puntos(nombre, bmp) + "\tver=" + typeof(Form1).GetField("tituloReposoVer", Todo).GetValue(form) + " alto=" + typeof(Form1).GetField("tituloReposoAlto", Todo).GetValue(form)
                + " de=" + typeof(Form1).GetField("tituloReposoDe", Todo).GetValue(form));
        }

        // ---- donde cae el numero de la capsula (XNumero de PastillaView, privada de Form1)
        private static void Numero(string nombre, string et, string va, int w, int h, bool conPortadas)
        {
            Type pv = typeof(Form1).GetNestedType("PastillaView", BindingFlags.NonPublic);
            object v = Activator.CreateInstance(pv, true);
            pv.GetField("Etiqueta").SetValue(v, et); pv.GetField("Valor").SetValue(v, va); pv.GetField("ConPortadas").SetValue(v, conPortadas);
            PropertyInfo ps = pv.GetProperty("Size");   // (desde vistas-disco la traduccion no tiene Size: el mismo sitio por Bounds, en 0,0 como el de fabrica)
            if (ps != null) ps.SetValue(v, new Size(w, h)); else pv.GetProperty("Bounds").SetValue(v, new Rectangle(0, 0, w, h));
            Linea(nombre, R((float)pv.GetMethod("XNumero", Todo).Invoke(v, null)));
        }

        // ---- LAS FIRMAS DE XP POR REFLEXION (desde directas-tanda2 la traduccion ya no las tiene). 'cual' es la posicion del argumento que dice
        // cual es cual (un mapa o una fuente). Se llama el metodo de ese nombre que tenga ahi el tipo de la referencia (Bitmap o Font) o el del
        // programa nuevo (SKBitmap o Letra): los mapas van tal cual y cada fuente por su objeto (Fte.Obj: la Font o la Letra). Los out vuelven en 'args'.
        private static MethodInfo PorTipo(string nombre, int cual, bool deFuente)
        {
            foreach (MethodInfo m in typeof(Form1).GetMethods(Todo))
            {
                if (m.Name != nombre || m.GetParameters().Length <= cual) continue;
                Type t = m.GetParameters()[cual].ParameterType;
                if (deFuente ? (t.Name == "Font" || t.Name == "Letra") : t == Pru.TMapa) return m;
            }
            return null;
        }
        private static object Xp(string nombre, int cual, params object[] args)
        {
            MethodInfo xp = PorTipo(nombre, cual, args[cual] is Fte);
            object[] t = (object[])args.Clone();
            for (int i = 0; i < t.Length; i++) if (t[i] is Fte) t[i] = ((Fte)t[i]).Obj;
            object r = xp.Invoke(null, t);
            for (int i = 0; i < t.Length; i++) if (args[i] == null) args[i] = t[i];   // los out
            return r;
        }
        // FuenteTituloReposo: la de XP (la referencia) o la Letra de LetraTituloReposo (el programa nuevo)
        private static Fte XpFuenteTituloReposo(int w, int h)
        {
            MethodInfo xp = typeof(Form1).GetMethod("FuenteTituloReposo", Todo);
            if (xp != null) return Fte.De(xp.Invoke(null, new object[] { w, h }));
            return Fte.De(typeof(Form1).GetMethod("LetraTituloReposo", Todo).Invoke(null, new object[] { w, h }));
        }
        // PintaTituloGrande y PintaMinutosCola con un lienzo: la de XP con el Graphics (la referencia) o la traducida con el lienzo y su estado
        // (suavizado, modo de letra, copia y medio pixel), la Letra de cada fuente, cada pincel liso en su SKPaint y el MISMO FormatoTexto
        private static void XpConGraphics(string nombre, Lz g, params object[] args)
        {
            object[] t;
            MethodInfo m = null;
            foreach (MethodInfo x in typeof(Form1).GetMethods(Todo)) if (x.Name == nombre && x.GetParameters().Length > 0 && x.GetParameters()[0].ParameterType == (Pru.Capa ? Pru.TLienzo : typeof(SKCanvas))) m = x;
            if (Pru.Capa) { t = new object[args.Length + 1]; t[0] = g.Objeto; }
            else { object[] es = EstadoDe(g); t = new object[args.Length + 5]; Array.Copy(es, t, 5); }
            int d = t.Length - args.Length;
            for (int i = 0; i < args.Length; i++) t[i + d] = args[i] is Fte ? ((Fte)args[i]).Obj : args[i] is Fmt ? ((Fmt)args[i]).Obj : args[i];
            m.Invoke(null, t);
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }

            // EL AVISO: los cuatro tipos, con y sin renglon, textos que no caben, tres anchos de pantalla y dos familias
            string[][] avisos = {
                new[] { "Aviso de relleno", "con su renglon" },
                new[] { "Pusiste «02 Ángel de amor (versión ñandú) & más»", "va la 3.ª en la cola · en ~2 min" },
                new[] { "¡Botones configurados!", "" },
                new[] { "No se pudo reproducir la cancion que pediste porque el archivo esta danado y no se puede abrir de ninguna manera", "credito devuelto: vuelve a intentarlo con otra cancion de la misma pagina o del mismo disco" },
                new[] { "", null },
                new[] { null, "solo el renglon" } };
            int k = 0;
            foreach (string fam in new[] { "Microsoft Sans Serif", "Trebuchet MS" })
                using (Fte tit = new Fte(fam, 14f, 1))
                using (Fte det = new Fte(fam, 11f, 1))
                    foreach (int ancho in new[] { 1024, 640, 1920 })
                        foreach (string[] av in avisos)
                            foreach (Form1.TipoAviso tipo in new[] { Form1.TipoAviso.Hecho, Form1.TipoAviso.Cuidado, Form1.TipoAviso.Error, Form1.TipoAviso.Novedad })
                            {
                                Size tam = (Size)Xp("TamanoAviso", 3, ancho, av[0], av[1], tit, det);
                                using (IDisposable b = (IDisposable)Pru.NuevoMapa(tam.Width, tam.Height, "Format32bppPArgb"))
                                {
                                    Xp("PintaAviso", 0, b, tipo, av[0], av[1], tit, det);
                                    string nom = "aviso-" + (k++);
                                    Linea(nom, tam.Width + "x" + tam.Height + "\t" + Puntos(nom, b));
                                }
                            }

            // LA INVITACION: anchos, canciones (vacia, con acentos, que no cabe), video o cancion, creditos por moneda
            k = 0;
            using (Fte tit = new Fte("Microsoft Sans Serif", 17f, 1))
            using (Fte txt = new Fte("Microsoft Sans Serif", 12.5f, 1))
            using (Fte pie = new Fte("Microsoft Sans Serif", 10.5f, 1))
                foreach (int ancho in new[] { 1024, 800, 420, 2000, 300 })
                    foreach (string cancion in new[] { "Cancion de relleno", "", "02 Ángel de amor (versión ñandú) & más con un titulo larguisimo que no cabe en los dos renglones de ninguna manera", null })
                        foreach (bool video in new[] { false, true })
                            foreach (int cpm in new[] { 4, 1 })
                            {
                                Size t = Form1.TamanoInvita(ancho);
                                using (IDisposable b = (IDisposable)Pru.NuevoMapa(t.Width, t.Height, "Format32bppPArgb"))
                                {
                                    object[] args = { b, cancion, video, cpm, tit, txt, pie, null, null };
                                    Xp("PintaInvita", 0, args);
                                    PointF ranura = (PointF)args[7]; float radio = (float)args[8];
                                    string nom = "invita-" + (k++);
                                    Linea(nom, t.Width + "x" + t.Height + "\t" + Puntos(nom, b) + "\tranura=" + R(ranura.X) + "," + R(ranura.Y) + " radio=" + R(radio));
                                }
                            }

            // EL "+4 CREDITOS"
            k = 0;
            foreach (float px in new[] { 14f, 11f, 20.3f })
                using (Fte mas = new Fte("Microsoft Sans Serif", px, 1, "Pixel"))
                    foreach (string txt in new[] { "+4 créditos", "+1 crédito", "+12 créditos" })
                    {
                        Size m = (Size)Xp("TamanoMas", 1, txt, mas);
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(m.Width, m.Height, "Format32bppPArgb"))
                        {
                            Xp("PintaMas", 0, b, txt, mas);
                            string nom = "mas-" + (k++);
                            Linea(nom, m.Width + "x" + m.Height + "\t" + Puntos(nom, b));
                        }
                    }

            // LA MONEDA (el Bitmap que se lleva Upload: sus puntos, su formato y sus marcas)
            // (interfaz-a-skbitmap, 12-11-2026: en la traduccion BakeMoneda da un SKBitmap -va por reflexion-; su formato se escribe como el de
            // GDI+ que le toca: Bgra8888 premultiplicado es Format32bppPArgb, con alfa -ImageFlags.HasAlpha-; otro formato se escribe tal cual y sale distinto)
            foreach (int lado in new[] { 96, 48, 128, 17, 200 })
            {
                object mo = typeof(GpuPanel).GetMethod("BakeMoneda", Todo).Invoke(null, new object[] { lado });
                SKBitmap sm = mo as SKBitmap;
                string formato = Pru.EsCapa(mo) ? Pru.Formato(mo) + " flags=" + Pru.Marcas(mo)
                    : (sm.ColorType == SKColorType.Bgra8888 && sm.AlphaType == SKAlphaType.Premul ? "Format32bppPArgb" + " flags=" + 2 : sm.ColorType + "/" + sm.AlphaType);
                Linea("moneda-" + lado, Puntos("moneda-" + lado, mo) + "\t" + formato);
                ((IDisposable)mo).Dispose();
            }

            // EL TITULO QUE SE DESLIZA, en su imagen: tres tamanos de letra, titulos con acentos, vacio y repetido (no se rehace)
            k = 0;
            foreach (int[] wh in new[] { new[] { 1024, 260 }, new[] { 1920, 400 }, new[] { 640, 200 } })
                using (Fte f = XpFuenteTituloReposo(wh[0], wh[1]))
                {
                    object form = RuntimeHelpers.GetUninitializedObject(typeof(Form1));
                    foreach (string titulo in new[] { "01 Cancion Larga con un titulo bastante largo para que no quepa en su caja", "02 Ángel de amor (versión ñandú) & más", "", "Corto", "Corto" })
                        TituloReposo("titulo-" + (k++), form, titulo, f);
                }

            // EL TITULO GRANDE DEL PANEL DE REPOSO: ocho estados del Graphics, titulos que caben y que no, con y sin genero (al lado y abajo)
            k = 0;
            string[][] titulos = {
                new[] { "Corto", "Disco Largo", "" },
                new[] { "01 Cancion Larga con un titulo bastante largo para que no quepa", "Artista Muy Largo con Nombre y Apellidos y su Orquesta Tipica", "Rock" },
                new[] { "02 Ángel de amor (versión ñandú) & más", "Ñandú", "Música Regional Mexicana" },
                new[] { "Titulo", "Artista", "Pop" } };
            foreach (Estado e in Estados)
                foreach (string[] t in titulos)
                    foreach (int tw in new[] { 400, 160 })
                        TituloGrande("grande-" + e.Nombre + "-" + (k++), e, t[0], t[1], t[2], 260, tw, false, false);
            // con la cinta del texto que se desliza (el artista que no cabe se va a la cinta, por PintaTextoMovil)
            foreach (Estado e in new[] { Estados[0], Estados[2], Estados[6] })
                foreach (string[] t in titulos)
                    TituloGrande("grande-cinta-" + e.Nombre + "-" + (k++), e, t[0], t[1], t[2], 260, 300, true, false);
            // la traducida llamada directo (en la referencia, otra vez la de XP)
            foreach (Estado e in Estados)
                foreach (string[] t in titulos)
                    TituloGrande("grande-directo-" + e.Nombre + "-" + (k++), e, t[0], t[1], t[2], 260, 300, e.Nombre == "xp", true);

            // LOS MINUTOS DE LA COLA: estrecha, ancha con sitio, ancha sin sitio (wm > 60 %), artista que no cabe (a la cinta)
            k = 0;
            foreach (Estado e in Estados)
                foreach (object[] c in new[] {
                    new object[] { "en ~2 min", "Disco Largo", true, 300f },
                    new object[] { "en ~2 min", "Disco Largo", false, 300f },
                    new object[] { "en ~123 min", "Artista", false, 90f },
                    new object[] { "ya casi", "Artista Muy Largo con Nombre y Apellidos y su Orquesta Tipica de Siempre", false, 260f },
                    new object[] { "en ~6 min", "", false, 200f } })
                {
                    MinutosCola("minutos-" + e.Nombre + "-" + (k++), e, (string)c[0], (string)c[1], (bool)c[2], 40.5f, 30.25f, (float)c[3], 19.55f, false, false);
                    MinutosCola("minutos-cinta-" + e.Nombre + "-" + (k++), e, (string)c[0], (string)c[1], (bool)c[2], 40.5f, 30.25f, (float)c[3], 19.55f, true, false);
                    MinutosCola("minutos-directo-" + e.Nombre + "-" + (k++), e, (string)c[0], (string)c[1], (bool)c[2], 40.5f, 30.25f, (float)c[3], 19.55f, true, true);
                }

            // DONDE CAE EL NUMERO DE LA CAPSULA
            Numero("numero-0", "Inserta una moneda", "", 300, 40, false);
            Numero("numero-1", "Creditos", "4", 200, 36, false);
            Numero("numero-2", "Creditos", "12", 250, 50, false);
            Numero("numero-3", "En cola", "3", 400, 44, true);
            Numero("numero-4", "Creditos", "4", 6, 6, false);
            Numero("numero-5", "Créditos ñ", "123", 120, 30, false);

            Console.Error.WriteLine("suena: " + n + " lineas, " + directas + " llamadas directas a la traducida");
            return 0;
        }
    }
}

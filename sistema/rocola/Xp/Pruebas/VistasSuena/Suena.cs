// ARNES DEL GRUPO «SUENA» (port nativo, tanda 2 del dibujo, 8-11-2026): lo que dibuja SuenaVivoGpu.cs con datos fijos, la huella de cada
// mapa y lo que devuelve cada llamada. Se compila con el programa de una carpeta Xp (VistasSuena.csproj): la salida de la referencia congelada
// y la de la copia de trabajo tienen que ser iguales linea por linea.
//   - Las llamadas son las de XP (Graphics, Bitmap, Font, Brush, StringFormat): en la referencia van a GDI+ (la capa congelada) y en la
//     traduccion a las firmas del puente (Plataforma/Puente.suena.cs) y de ahi a SkiaSharp directo: el aviso de los cuatro tipos (con y sin
//     renglon, textos que no caben), la invitacion, el "+4 creditos", la moneda, y el titulo grande y los minutos de la cola del panel de
//     reposo con ocho estados del Graphics (suavizado, modo de letra, copia, medio pixel, traslacion, escala, giro y recorte), con y sin la
//     cinta del texto que se desliza (TextoMovilGpu.cs: la que pinta PintaTextoMovil, por el puente).
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
using Rocola;
using Rocola.Dibujo2D;
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

        // ---- el estado del Graphics del que llama
        private sealed class Estado { public string Nombre; public SmoothingMode S; public TextRenderingHint T; public CompositingMode C; public PixelOffsetMode P; public int Trans; public bool Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "xp", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "sin-suavizar", S = SmoothingMode.None, T = TextRenderingHint.SystemDefault, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default },
            new Estado { Nombre = "copia-medio-traslada", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala", S = SmoothingMode.AntiAlias, T = TextRenderingHint.ClearTypeGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.HighQuality, Trans = 2 },
            new Estado { Nombre = "unbit-gira", S = SmoothingMode.AntiAlias, T = TextRenderingHint.SingleBitPerPixelGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = SmoothingMode.None, T = TextRenderingHint.SingleBitPerPixel, C = CompositingMode.SourceCopy, P = PixelOffsetMode.Default, Recorte = true },
            new Estado { Nombre = "medio-traslada-recorte", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 4, Recorte = true },
            new Estado { Nombre = "suave-sin-ajuste", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.None },
        };
        // un mapa con fondo (para ver la copia y el alfa) y el Graphics con ese estado
        private static Graphics Prepara(Bitmap b, Estado e)
        {
            Graphics g = Graphics.FromImage(b);
            g.Clear(Color.FromArgb(120, 30, 60, 90));
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(200, 250, 20, 60))) g.FillRectangle(sb, 0, 60, b.Width, 9);
            g.SmoothingMode = e.S; g.TextRenderingHint = e.T; g.CompositingMode = e.C; g.PixelOffsetMode = e.P;
            if (e.Trans == 1) g.TranslateTransform(3.5f, 2.25f); else if (e.Trans == 2) g.ScaleTransform(1.3f, 1.3f); else if (e.Trans == 3) g.RotateTransform(7f); else if (e.Trans == 4) g.TranslateTransform(0.5f, 0f);
            if (e.Recorte) g.SetClip(new RectangleF(30.5f, 10, 600, 120));
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
        private static Type Puente { get { return typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo"); } }
        private static object Por(string metodo, Type tipo, object o) { return Puente.GetMethod(metodo, Todo, null, new[] { tipo }, null).Invoke(null, new[] { o }); }
        private static object[] EstadoDe(Graphics g)
        {
            object e = Por("Estado", typeof(Graphics), g); Type te = e.GetType();
            return new[] { Por("Lienzo", typeof(Graphics), g), te.GetField("Suave").GetValue(e), te.GetField("Letra").GetValue(e), te.GetField("Copia").GetValue(e), te.GetField("Medio").GetValue(e) };
        }
        private static SKPaint Color2(Color c) { return new SKPaint { Color = new SKColor(c.R, c.G, c.B, c.A) }; }

        // ---- el titulo grande del panel de reposo (PintaTituloGrande)
        private static void TituloGrande(string nombre, Estado e, string title, string artist, string genero, int tx, int tw, bool cinta, bool directo)
        {
            int w = 1024, h = 300;
            float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Font small = new Font("Trebuchet MS", 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font normal = new Font("Trebuchet MS", 20 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font big = Form1.FuenteTituloReposo(w, h))
            using (SolidBrush white = new SolidBrush(Color.FromArgb(240, 245, 255)))
            using (SolidBrush color = new SolidBrush(Color.FromArgb(150, 210, 255)))
            using (StringFormat line = new StringFormat())
            {
                line.Trimming = StringTrimming.EllipsisCharacter; line.FormatFlags = StringFormatFlags.NoWrap;
                ReposoGrande x = new ReposoGrande(); x.Genero = genero; x.GeneroColor = Color.FromArgb(230, 90, 160);
                CintasTexto cintas = cinta ? new CintasTexto() : null;
                if (cintas != null) { cintas.Begin(); Captura.SetValue(null, cintas); }
                try
                {
                    using (Graphics g = Prepara(b, e))
                    {
                        MethodInfo m = directo ? Nativa("PintaTituloGrande") : null;
                        if (m == null) Form1.PintaTituloGrande(g, x, title, artist, big, normal, small, white, color, line, tx, tw);
                        else
                            using (SKPaint pw = Color2(white.Color)) using (SKPaint pc = Color2(color.Color))
                            {
                                directas++;
                                object[] es = EstadoDe(g);
                                m.Invoke(null, new object[] { es[0], es[1], es[2], es[3], es[4], x, title, artist, Por("Letra", typeof(Font), big), Por("Letra", typeof(Font), normal),
                                    Por("Letra", typeof(Font), small), pw, pc, Por("Formato", typeof(StringFormat), line), tx, tw });
                            }
                    }
                }
                finally { if (cintas != null) { Captura.SetValue(null, null); cintas.End(); } }
                Linea(nombre, Puntos(nombre, b) + "\tfuera=" + x.TituloFuera + " caja=" + R(x.TituloCaja) + " genero=" + R(x.GeneroCaja) + Cintas(nombre, cintas));
                if (cintas != null) cintas.Dispose();
            }
        }

        // ---- los minutos de la cola (PintaMinutosCola)
        private static void MinutosCola(string nombre, Estado e, string minutos, string artista, bool estrecha, float x, float y, float w, float h, bool cinta, bool directo)
        {
            using (Bitmap b = new Bitmap(700, 160, PixelFormat.Format32bppPArgb))
            using (Font f = new Font("Trebuchet MS", 15.077f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush tenue = new SolidBrush(Color.FromArgb(157, 179, 209)))
            using (StringFormat line = new StringFormat())
            {
                line.Trimming = StringTrimming.EllipsisCharacter; line.FormatFlags = StringFormatFlags.NoWrap;
                CintasTexto cintas = cinta ? new CintasTexto() : null;
                if (cintas != null) { cintas.Begin(); Captura.SetValue(null, cintas); }
                try
                {
                    using (Graphics g = Prepara(b, e))
                    {
                        MethodInfo m = directo ? Nativa("PintaMinutosCola") : null;
                        if (m == null) Form1.PintaMinutosCola(g, minutos, artista, f, tenue, estrecha, x, y, w, h, line);
                        else
                            using (SKPaint pt = Color2(tenue.Color))
                            {
                                directas++;
                                object[] es = EstadoDe(g);
                                m.Invoke(null, new object[] { es[0], es[1], es[2], es[3], es[4], minutos, artista, Por("Letra", typeof(Font), f), pt, estrecha, x, y, w, h, Por("Formato", typeof(StringFormat), line) });
                            }
                    }
                }
                finally { if (cintas != null) { Captura.SetValue(null, null); cintas.End(); } }
                Linea(nombre, Puntos(nombre, b) + Cintas(nombre, cintas));
                if (cintas != null) cintas.Dispose();
            }
        }

        // ---- el titulo que se desliza, en su imagen (HorneaTituloReposo, privado de Form1: en una Form1 sin construir, que solo usa sus campos)
        private static void TituloReposo(string nombre, object form, string titulo, Font f)
        {
            MethodInfo m = typeof(Form1).GetMethod("HorneaTituloReposo", Todo);
            object letra = m.GetParameters()[1].ParameterType == typeof(Font) ? (object)f : Por("Letra", typeof(Font), f);
            m.Invoke(form, new object[] { titulo, letra });
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
            pv.GetProperty("Size").SetValue(v, new Size(w, h));
            Linea(nombre, R((float)pv.GetMethod("XNumero", Todo).Invoke(v, null)));
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
                using (Font tit = new Font(fam, 14f, FontStyle.Bold))
                using (Font det = new Font(fam, 11f, FontStyle.Bold))
                    foreach (int ancho in new[] { 1024, 640, 1920 })
                        foreach (string[] av in avisos)
                            foreach (Form1.TipoAviso tipo in new[] { Form1.TipoAviso.Hecho, Form1.TipoAviso.Cuidado, Form1.TipoAviso.Error, Form1.TipoAviso.Novedad })
                            {
                                Size tam = Form1.TamanoAviso(ancho, av[0], av[1], tit, det);
                                using (Bitmap b = new Bitmap(tam.Width, tam.Height, PixelFormat.Format32bppPArgb))
                                {
                                    Form1.PintaAviso(b, tipo, av[0], av[1], tit, det);
                                    string nom = "aviso-" + (k++);
                                    Linea(nom, tam.Width + "x" + tam.Height + "\t" + Puntos(nom, b));
                                }
                            }

            // LA INVITACION: anchos, canciones (vacia, con acentos, que no cabe), video o cancion, creditos por moneda
            k = 0;
            using (Font tit = new Font("Microsoft Sans Serif", 17f, FontStyle.Bold))
            using (Font txt = new Font("Microsoft Sans Serif", 12.5f, FontStyle.Bold))
            using (Font pie = new Font("Microsoft Sans Serif", 10.5f, FontStyle.Bold))
                foreach (int ancho in new[] { 1024, 800, 420, 2000, 300 })
                    foreach (string cancion in new[] { "Cancion de relleno", "", "02 Ángel de amor (versión ñandú) & más con un titulo larguisimo que no cabe en los dos renglones de ninguna manera", null })
                        foreach (bool video in new[] { false, true })
                            foreach (int cpm in new[] { 4, 1 })
                            {
                                Size t = Form1.TamanoInvita(ancho);
                                using (Bitmap b = new Bitmap(t.Width, t.Height, PixelFormat.Format32bppPArgb))
                                {
                                    PointF ranura; float radio;
                                    Form1.PintaInvita(b, cancion, video, cpm, tit, txt, pie, out ranura, out radio);
                                    string nom = "invita-" + (k++);
                                    Linea(nom, t.Width + "x" + t.Height + "\t" + Puntos(nom, b) + "\tranura=" + R(ranura.X) + "," + R(ranura.Y) + " radio=" + R(radio));
                                }
                            }

            // EL "+4 CREDITOS"
            k = 0;
            foreach (float px in new[] { 14f, 11f, 20.3f })
                using (Font mas = new Font("Microsoft Sans Serif", px, FontStyle.Bold, GraphicsUnit.Pixel))
                    foreach (string txt in new[] { "+4 créditos", "+1 crédito", "+12 créditos" })
                    {
                        Size m = Form1.TamanoMas(txt, mas);
                        using (Bitmap b = new Bitmap(m.Width, m.Height, PixelFormat.Format32bppPArgb))
                        {
                            Form1.PintaMas(b, txt, mas);
                            string nom = "mas-" + (k++);
                            Linea(nom, m.Width + "x" + m.Height + "\t" + Puntos(nom, b));
                        }
                    }

            // LA MONEDA (el Bitmap que se lleva Upload: sus puntos, su formato y sus marcas)
            foreach (int lado in new[] { 96, 48, 128, 17, 200 })
                using (Bitmap b = GpuPanel.BakeMoneda(lado))
                    Linea("moneda-" + lado, Puntos("moneda-" + lado, b) + "\t" + b.PixelFormat + " flags=" + b.Flags);

            // EL TITULO QUE SE DESLIZA, en su imagen: tres tamanos de letra, titulos con acentos, vacio y repetido (no se rehace)
            k = 0;
            foreach (int[] wh in new[] { new[] { 1024, 260 }, new[] { 1920, 400 }, new[] { 640, 200 } })
                using (Font f = Form1.FuenteTituloReposo(wh[0], wh[1]))
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

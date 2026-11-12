// ARNES DEL GRUPO «REPOSO» (port nativo, tanda 2 del dibujo, 8-11-2026): lo que dibujan EstadoReposoGpu.cs (el panel del estado extendido y
// su reloj), TextoMovilGpu.cs (las cintas del texto que se desliza y el texto que se mueve) y FranjaLigeraGpu.cs (el recuadro de la franja),
// con datos fijos, la huella de cada mapa y lo que devuelve cada llamada. Se compila con el programa de una carpeta Xp (VistasReposo.csproj):
// la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales linea por linea.
//   - Las llamadas son las de XP (Graphics, Bitmap, Image, Font, Brush, StringFormat): en la referencia van a GDI+ (la capa congelada) y en
//     la traduccion a las firmas del puente (Plataforma/Puente.reposo.cs) y de ahi a SkiaSharp directo. El estado del Graphics del que llama
//     va de ocho maneras (suavizado, los modos de letra, copia, medio pixel, traslacion entera y con decimales, escala, giro y recorte), con y
//     sin la captura de las cintas puesta.
//   - Lo privado (EmpiezaRecuadro, PintaArtistaCompacto) va por reflexion, con la firma de XP.
//   - En la traduccion, las traducidas se llaman tambien DIRECTO (el lienzo y su estado, la Letra, un SKPaint, el FormatoTexto, los SKBitmap),
//     como las llamaran los archivos que las usan cuando se traduzcan; en la referencia esa linea es la de XP otra vez.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasReposo.dll [vuelca CARPETA]
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using SkiaSharp;

namespace BancoReposo
{
    internal static class Reposo
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
            new Estado { Nombre = "traslada-recorte", S = SmoothingMode.AntiAlias, T = TextRenderingHint.AntiAliasGridFit, C = CompositingMode.SourceOver, P = PixelOffsetMode.Default, Trans = 4, Recorte = true },
            new Estado { Nombre = "suave-sin-ajuste", S = SmoothingMode.HighQuality, T = TextRenderingHint.AntiAlias, C = CompositingMode.SourceOver, P = PixelOffsetMode.None, Trans = 5 },
        };
        // un mapa con fondo (para ver la copia y el alfa) y el Graphics con ese estado
        private static Graphics Prepara(Bitmap b, Estado e)
        {
            Graphics g = Graphics.FromImage(b);
            g.Clear(Color.FromArgb(120, 30, 60, 90));
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(200, 250, 20, 60))) g.FillRectangle(sb, 0, 25, b.Width, 9);
            g.SmoothingMode = e.S; g.TextRenderingHint = e.T; g.CompositingMode = e.C; g.PixelOffsetMode = e.P;
            if (e.Trans == 1) g.TranslateTransform(3.5f, 2.25f); else if (e.Trans == 2) g.ScaleTransform(1.3f, 1.3f); else if (e.Trans == 3) g.RotateTransform(7f);
            else if (e.Trans == 4) g.TranslateTransform(0, -40); else if (e.Trans == 5) g.TranslateTransform(0.5f, 0f);
            if (e.Recorte) g.SetClip(new RectangleF(30.5f, 8, 300, 50));
            return g;
        }

        // ---- las cintas del texto que se desliza (TextoMovilGpu.cs): lo que se llevo la captura
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
        // el tipografico compartido (EstadoReposoGpu.cs le cambia la alineacion y NoWrap: lo heredan todos)
        private static string Tipografico()
        {
            StringFormat t = StringFormat.GenericTypographic;
            return " tipografico=" + t.Alignment + "/" + t.LineAlignment + "/" + (int)t.FormatFlags + "/" + t.Trimming;
        }

        // ---- la traducida, llamada directo; null si no la hay (la referencia)
        private static MethodInfo Nativa(string nombre, Type primero)
        {
            foreach (MethodInfo m in typeof(Form1).GetMethods(Todo)) if (m.Name == nombre && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == primero) return m;
            return null;
        }
        private static Type Puente { get { return typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo"); } }
        private static object Por(string metodo, Type tipo, object o) { return Puente.GetMethod(metodo, Todo, null, new[] { tipo }, null).Invoke(null, new[] { o }); }
        // el lienzo y su estado (suave, letra, copia, filtro, medio, hdc) de un Graphics, por el puente de la traduccion
        private static object[] EstadoDe(Graphics g)
        {
            object e = Por("Estado", typeof(Graphics), g); Type te = e.GetType();
            return new[] { Por("Lienzo", typeof(Graphics), g), te.GetField("Suave").GetValue(e), te.GetField("Letra").GetValue(e), te.GetField("Copia").GetValue(e), te.GetField("Medio").GetValue(e) };
        }
        private static SKPaint Pincel(Brush b, bool medio)
        {
            if (b == null) return null;
            return (SKPaint)Puente.GetMethod("Pincel", Todo, null, new[] { typeof(Brush), typeof(bool) }, null).Invoke(null, new object[] { b, medio });
        }
        private static void Suelta(SKPaint p) { if (p == null) return; SKShader s = p.Shader; p.Shader = null; if (s != null) s.Dispose(); p.Dispose(); }

        // ---- una portada de prueba: puntos al azar con semilla fija (opaca o con alfa), escritos con LockBits como XP
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

        // ---- EL PANEL DE REPOSO (RenderIdlePanelEn), por la firma de XP
        private sealed class Panel
        {
            public string Titulo, Artista, Hora; public int N; public bool Doce; public int Insignia; public ReposoGrande Extra; public bool Transparente, Opaco; public Color Acento; public bool ConPortadas, Tramada;
        }
        private static ReposoGrande Extra(string genero, Color c, int[] minutos) { ReposoGrande x = new ReposoGrande(); x.Genero = genero; x.GeneroColor = c; x.Minutos = minutos; return x; }
        private static string ExtraDe(ReposoGrande x) { return x == null ? "" : " fuera=" + x.TituloFuera + " caja=" + R(x.TituloCaja) + " genero=" + R(x.GeneroCaja); }
        private static Image[] Portadas(int n, bool con, List<Bitmap> todas)
        {
            Image[] c = new Image[n];
            if (!con) return c;
            for (int i = 0; i < n; i++)
            {
                if (i % 4 == 1) continue;   // algunas sin portada (se pinta el cuadrito del color)
                Bitmap b = i % 4 == 0 ? Portada(256, 256, i + 1, false) : i % 4 == 2 ? Portada(64, 48, i + 1, true) : Portada(600, 600, i + 1, false);
                todas.Add(b); c[i] = b;
            }
            return c;
        }
        private static Bitmap HazPanel(string nombre, Bitmap reusa, int w, int h, Panel p, bool cinta, bool directo, List<Bitmap> todas)
        {
            string[] qt = new string[p.N], qa = new string[p.N];
            for (int i = 0; i < p.N; i++)
            {
                qt[i] = i % 5 == 3 ? null : i % 5 == 1 ? "02 Ángel de amor (versión ñandú) & más, la " + (i + 1) + "ª de la cola, que no cabe" : "Cancion de relleno " + (i + 1);
                qa[i] = i % 4 == 2 ? null : i % 4 == 3 ? "Artista Muy Largo con Nombre y Apellidos y su Orquesta Tipica" : "Artista de relleno";
            }
            Image[] covers = Portadas(p.N, p.ConPortadas, todas);
            Form1.InterfazTramada = p.Tramada;
            CintasTexto c = cinta ? Captura0() : null;
            int comienzo; Bitmap r;
            try
            {
                MethodInfo m = directo ? Nativa("RenderIdlePanelEn", typeof(SKBitmap)) : null;
                if (m == null) r = Form1.RenderIdlePanelEn(reusa, w, h, p.Titulo, p.Artista, p.Hora, qt, qa, covers, p.N, p.Acento, p.Transparente, p.Insignia, p.Doce, p.Extra, p.Opaco, out comienzo);
                else
                {
                    directas++;
                    SKBitmap[] mapas = new SKBitmap[covers.Length];
                    for (int i = 0; i < covers.Length; i++) mapas[i] = covers[i] != null ? covers[i].Sk : null;
                    object[] a = { reusa != null ? reusa.Sk : null, w, h, p.Titulo, p.Artista, p.Hora, qt, qa, mapas, p.N, p.Acento, p.Transparente, p.Insignia, p.Doce, p.Extra, p.Opaco, 0 };
                    SKBitmap sk = (SKBitmap)m.Invoke(null, a);
                    comienzo = (int)a[16];
                    r = reusa != null && ReferenceEquals(sk, reusa.Sk) ? reusa : new Bitmap(sk, PixelFormat.Format32bppPArgb);
                }
            }
            finally { Captura1(c); }
            Linea(nombre, Puntos(nombre, r) + "\tcomienzo=" + comienzo + " mismo=" + ReferenceEquals(r, reusa) + " formato=" + r.PixelFormat + " flags=" + r.Flags + ExtraDe(p.Extra) + Cintas(nombre, c) + Tipografico());
            if (c != null) c.Dispose();
            return r;
        }

        // ---- EL TEXTO QUE SE MUEVE (PintaTextoMovil), LA CINTA (TextoConCinta) Y LA DE LA VISTA PREVIA (CintaVistaPrevia)
        private static readonly string[] Textos = { "Cancion de relleno 1", "02 Ángel de amor (versión ñandú) & más, con un titulo larguisimo que no cabe", "", "Disco Largo" };
        private static StringFormat Formato(int k)
        {
            switch (k % 4)
            {
                case 0: { StringFormat f = new StringFormat(); f.Trimming = StringTrimming.EllipsisCharacter; f.FormatFlags = StringFormatFlags.NoWrap; return f; }
                case 1: { StringFormat f = new StringFormat(); f.Trimming = StringTrimming.EllipsisWord; f.FormatFlags = StringFormatFlags.LineLimit; f.LineAlignment = StringAlignment.Center; return f; }
                case 2: { StringFormat f = new StringFormat(); f.Alignment = StringAlignment.Far; f.LineAlignment = StringAlignment.Far; return f; }
                default: return StringFormat.GenericTypographic;   // el compartido (PintaTextoMovil lo copia para la cinta; no lo cambia)
            }
        }
        private static Brush Pincel(int k, List<Bitmap> todas)
        {
            switch (k % 5)
            {
                case 0: return new SolidBrush(Color.FromArgb(240, 245, 255));
                case 1: return new SolidBrush(Color.FromArgb(128, 157, 179, 209));
                case 2: return new LinearGradientBrush(new RectangleF(0, 0, 300, 40), Color.FromArgb(255, 250, 200, 40), Color.FromArgb(180, 40, 90, 230), 0f);
                case 3: { Bitmap t = Portada(8, 8, 77, true); todas.Add(t); return new TextureBrush(t, WrapMode.Tile); }
                default: return null;
            }
        }
        private static Font Letra(int k)
        {
            switch (k % 3)
            {
                case 0: return new Font("Trebuchet MS", 18.846f, FontStyle.Bold, GraphicsUnit.Pixel);
                case 1: return new Font("Microsoft Sans Serif", 17.25f, FontStyle.Bold);
                default: return new Font("Tahoma", 11.2f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);
            }
        }
        private static readonly RectangleF[] Cajas = { new RectangleF(20.5f, 10.25f, 300, 40), new RectangleF(0, 0, 60, 20), new RectangleF(10, 5, 11.5f, 7.5f), new RectangleF(40, 30, 900, 19) };

        private static void TextoMovil(string nombre, Estado e, string texto, int k, bool cinta, bool directo, List<Bitmap> todas)
        {
            using (Bitmap b = new Bitmap(700, 120, PixelFormat.Format32bppPArgb))
            using (Font f = Letra(k))
            {
                Brush br = Pincel(k, todas); StringFormat sf = Formato(k / 2); RectangleF caja = Cajas[(k / 3) % Cajas.Length];
                CintasTexto c = cinta ? Captura0() : null;
                string ex = "";
                try
                {
                    using (Graphics g = Prepara(b, e))
                    {
                        MethodInfo m = directo ? Nativa("PintaTextoMovil", typeof(SKCanvas)) : null;
                        if (m == null) Form1.PintaTextoMovil(g, texto, f, br, caja, sf);
                        else
                        {
                            directas++;
                            object[] es = EstadoDe(g);
                            SKPaint p = Pincel(br, (bool)es[4]);
                            try { m.Invoke(null, new object[] { es[0], es[2], es[3], es[4], texto, Por("Letra", typeof(Font), f), p, caja, Por("Formato", typeof(StringFormat), sf) }); }
                            finally { Suelta(p); }
                        }
                    }
                }
                catch (Exception x) { ex = " EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }
                finally { Captura1(c); }
                Linea(nombre, Puntos(nombre, b) + ex + Cintas(nombre, c));
                if (c != null) c.Dispose();
                if (br != null) br.Dispose();
            }
        }
        private static void ConCinta(string nombre, Estado e, string texto, int k, bool cinta, bool vista, bool directo)
        {
            using (Bitmap b = new Bitmap(700, 120, PixelFormat.Format32bppPArgb))
            using (Font f = Letra(k))
            {
                StringFormat sf = Formato(k); RectangleF caja = Cajas[k % Cajas.Length]; Color col = k % 2 == 0 ? Color.FromArgb(240, 245, 255) : Color.FromArgb(200, 125, 235, 165);
                CintasTexto c = cinta ? Captura0() : null;
                string ex = ""; bool r = false;
                try
                {
                    using (Graphics g = Prepara(b, e))
                    {
                        MethodInfo m = directo ? Nativa(vista ? "CintaVistaPrevia" : "TextoConCinta", typeof(SKCanvas)) : null;
                        // la firma de XP de CintaVistaPrevia (con un Graphics) se quito del puente al traducirse Form1.Buscador.cs (grupo buscador,
                        // revision): donde ya no esta (la traduccion), la llamada de XP es la traducida con el estado del Graphics, como hacia el puente
                        MethodInfo xpVista = vista && m == null ? typeof(Form1).GetMethod("CintaVistaPrevia", Todo, null, new[] { typeof(Graphics), typeof(string), typeof(Font), typeof(Color), typeof(Rectangle) }, null) : null;
                        if (vista && m == null && xpVista == null) m = Nativa("CintaVistaPrevia", typeof(SKCanvas));
                        if (m == null) r = vista ? (bool)xpVista.Invoke(null, new object[] { g, texto, f, col, Rectangle.Round(caja) }) : Form1.TextoConCinta(g, texto, f, col, caja, sf);
                        else
                        {
                            directas++;
                            object[] es = EstadoDe(g);
                            r = vista ? (bool)m.Invoke(null, new object[] { es[0], es[2], texto, Por("Letra", typeof(Font), f), col, Rectangle.Round(caja) })
                                      : (bool)m.Invoke(null, new object[] { es[0], es[2], texto, Por("Letra", typeof(Font), f), col, caja, Por("Formato", typeof(StringFormat), sf) });
                        }
                    }
                }
                catch (Exception x) { ex = " EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }
                finally { Captura1(c); }
                Linea(nombre, Puntos(nombre, b) + "\tcinta=" + r + ex + Cintas(nombre, c));
                if (c != null) c.Dispose();
            }
        }
        // una tanda en el mismo grupo: la misma cinta otra vez (no se rehace), otra que la empuja, una de mas de 1024 (varias teselas), una de
        // mas de 256 de alto (no va), cincuenta seguidas (el tope de 48) y la de antes otra vez
        private static void Tanda(string nombre)
        {
            using (Bitmap b = new Bitmap(1200, 300, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            using (Font f = new Font("Trebuchet MS", 30f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font fg = new Font("Trebuchet MS", 90f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                sf.FormatFlags = StringFormatFlags.NoWrap; sf.LineAlignment = StringAlignment.Center;
                CintasTexto c = new CintasTexto();
                string largo = "Un titulo larguisimo que no cabe en su caja";
                StringBuilder sb = new StringBuilder();
                for (int vuelta = 0; vuelta < 3; vuelta++)
                {
                    c.Begin(); Captura.SetValue(null, c);
                    sb.Append(" v" + vuelta + ":");
                    sb.Append(Form1.TextoConCinta(g, largo + " A", f, Color.White, new RectangleF(10, 10, 200, 40), sf) ? "1" : "0");
                    if (vuelta == 1) sb.Append(Form1.TextoConCinta(g, largo + " B", f, Color.White, new RectangleF(10, 60, 200, 40), sf) ? "1" : "0");
                    sb.Append(Form1.TextoConCinta(g, largo + " A", f, Color.White, new RectangleF(10, 10, 200, 40), sf) ? "1" : "0");
                    sb.Append(Form1.TextoConCinta(g, largo + largo + largo + largo, fg, Color.FromArgb(200, 255, 230, 120), new RectangleF(5, 100, 400, 100), sf) ? "1" : "0");
                    sb.Append(Form1.TextoConCinta(g, largo, f, Color.White, new RectangleF(5, 0, 300, 257), sf) ? "1" : "0");
                    if (vuelta == 2) for (int i = 0; i < 50; i++) sb.Append(Form1.TextoConCinta(g, largo + " " + i, f, Color.White, new RectangleF(10, 10, 100, 40), sf) ? "1" : "0");
                    Captura.SetValue(null, null); c.End();
                    sb.Append(" n=" + c.Items.Count + " ver=" + c.Version);
                }
                Linea(nombre, sb.ToString() + Cintas(nombre, c));
                c.Dispose();
            }
        }

        // ---- EL ARTISTA DE LA FRANJA (PintaArtistaCompacto, privada de Form1: en una Form1 sin construir, con la etiqueta lblNow)
        private static void Artista(string nombre, Estado e, string texto, ContentAlignment al, int ancho, bool mejorada, bool cinta, bool directo)
        {
            object form = RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            Label l = new Label(); l.Text = texto; l.Font = new Font("Microsoft Sans Serif", 11.5f, FontStyle.Bold); l.ForeColor = Color.FromArgb(225, 236, 255); l.TextAlign = al;
            l.Bounds = new Rectangle(14, 40, ancho, 26);
            typeof(Form1).GetField("lblNow", Todo).SetValue(form, l);
            if (mejorada) { Label m = new Label(); m.Visible = true; m.Bounds = new Rectangle(14 + ancho / 2, 40, 60, 20); typeof(Form1).GetField("lblMejorada", Todo).SetValue(form, m); }
            using (Bitmap b = new Bitmap(700, 120, PixelFormat.Format32bppPArgb))
            {
                CintasTexto c = cinta ? Captura0() : null;
                string ex = "";
                try
                {
                    using (Graphics g = Prepara(b, e))
                    {
                        MethodInfo m = directo ? Nativa("PintaArtistaCompacto", typeof(SKCanvas)) : null;
                        if (m == null) typeof(Form1).GetMethod("PintaArtistaCompacto", Todo, null, new[] { typeof(Graphics) }, null).Invoke(form, new object[] { g });
                        else { directas++; object[] es = EstadoDe(g); m.Invoke(form, new object[] { es[0], es[2], es[3], es[4] }); }
                    }
                }
                catch (Exception x) { ex = " EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }
                finally { Captura1(c); }
                Linea(nombre, Puntos(nombre, b) + ex + Cintas(nombre, c));
                if (c != null) c.Dispose();
            }
        }

        // ---- EL RECUADRO DE LA FRANJA (EmpiezaRecuadro, privada de Form1) y lo que se pinta despues con el mismo Graphics: dentro del recuadro,
        // con otro recorte que reemplaza, sin recorte, y dentro de un Save/Restore
        private static void Recuadro(string nombre, Estado e, Rectangle zona, bool directo)
        {
            using (Bitmap b = new Bitmap(400, 100, PixelFormat.Format32bppPArgb))
            using (Graphics g = Prepara(b, e))
            using (SolidBrush rojo = new SolidBrush(Color.FromArgb(150, 255, 40, 40)))
            using (SolidBrush verde = new SolidBrush(Color.FromArgb(150, 40, 255, 40)))
            using (Font f = new Font("Microsoft Sans Serif", 14f, FontStyle.Bold))
            {
                StringBuilder sb = new StringBuilder();
                MethodInfo m = directo ? Nativa("EmpiezaRecuadro", typeof(SKCanvas)) : null;
                // la firma de XP de EmpiezaRecuadro (con un Graphics) se quito del puente al traducirse Form1.Video.cs (grupo video-form1): donde ya
                // no esta (la traduccion), la llamada de XP es la traducida con lo que hacia el puente (el SetClip del Graphics y su estado)
                MethodInfo xp = m == null ? typeof(Form1).GetMethod("EmpiezaRecuadro", Todo, null, new[] { typeof(Graphics), typeof(Rectangle) }, null) : null;
                if (m == null && xp != null) xp.Invoke(null, new object[] { g, zona });
                else
                {
                    // directo: como lo hara Form1.Video.cs traducido, el lienzo nuevo sin recorte; el resto sigue por el Graphics, que se entera del
                    // recorte con su SetClip (lo que hace el puente)
                    if (m != null) directas++; else m = Nativa("EmpiezaRecuadro", typeof(SKCanvas));
                    g.SetClip(zona); object[] es = EstadoDe(g); m.Invoke(null, new object[] { es[0], es[1], es[4], zona });
                }
                sb.Append(" recuadro=" + Puntos(nombre + "-0", b).Replace('\t', ' ') + " recorte=" + R(g.ClipBounds));
                g.FillRectangle(rojo, 0, 0, 400, 100);
                g.DrawString("Texto dentro del recuadro", f, verde, new RectangleF(0, 0, 400, 100));
                sb.Append(" dentro=" + Puntos(nombre + "-1", b).Replace('\t', ' '));
                g.SetClip(new Rectangle(5, 60, 120, 30));
                g.FillRectangle(verde, 0, 0, 400, 100);
                sb.Append(" otro=" + Puntos(nombre + "-2", b).Replace('\t', ' ') + " recorte=" + R(g.ClipBounds));
                GraphicsState st = g.Save();
                g.SetClip(new Rectangle(200, 0, 50, 100));
                g.FillRectangle(rojo, 0, 0, 400, 100);
                g.Restore(st);
                g.FillRectangle(rojo, 0, 50, 400, 10);
                sb.Append(" guardado=" + Puntos(nombre + "-3", b).Replace('\t', ' ') + " recorte=" + R(g.ClipBounds));
                g.ResetClip();
                g.FillRectangle(verde, 0, 95, 400, 5);
                sb.Append(" sin=" + Puntos(nombre + "-4", b).Replace('\t', ' ') + " recorte=" + R(g.ClipBounds));
                Linea(nombre, sb.ToString());
            }
        }

        // ---- EL RELOJ DEL PANEL (DrawIdleClock), directo: en la traduccion con el lienzo y su modo de letra; en la referencia, la de XP con
        // un Graphics como lo dejan los que la llaman (AntiAlias, AntiAliasGridFit) y con el recorte de UpdateIdleClockRegion
        private static void Reloj(string nombre, int w, int h, string hora, bool recorte)
        {
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(200, 12, 19, 42));
                if (recorte) g.SetClip(new Rectangle(200, h - 34, 130, 34));
                g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                MethodInfo m = Nativa("DrawIdleClock", typeof(SKCanvas));
                int r;
                if (m == null) r = (int)typeof(Form1).GetMethod("DrawIdleClock", Todo, null, new[] { typeof(Graphics), typeof(int), typeof(int), typeof(string) }, null).Invoke(null, new object[] { g, w, h, hora });
                else { directas++; object[] es = EstadoDe(g); r = (int)m.Invoke(null, new object[] { es[0], es[2], w, h, hora }); }
                Linea(nombre, Puntos(nombre, b) + "\tderecha=" + r);
            }
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            List<Bitmap> todas = new List<Bitmap>();
            Linea("inicio", Tipografico());

            // EL PANEL DE REPOSO: seis pantallas, seis contenidos (titulos que caben y que no, nulos, acentos, la cola de 0 a 12, la docena, las
            // insignias, lo que suena en grande con su genero y sus minutos, transparente, opaco, con y sin portadas, la interfaz tramada o no),
            // con y sin la captura de las cintas, y el mapa que se vuelve a usar (del mismo tamano y de otro)
            Panel[] paneles = {
                new Panel { Titulo = "Titulo de relleno", Artista = "Artista de relleno", Hora = "1:23 / 4:56", N = 3, Acento = Color.FromArgb(120, 160, 255), ConPortadas = true, Tramada = true },
                new Panel { Titulo = "02 Ángel de amor (versión ñandú) & más, con un titulo larguisimo que no cabe en su caja de ninguna manera", Artista = "Artista Muy Largo con Nombre y Apellidos y su Orquesta Tipica de Siempre",
                            Hora = "", N = 12, Doce = true, Insignia = 1, Extra = Extra("Música Regional Mexicana", Color.FromArgb(230, 90, 160), new[] { 0, 3, -1, 12, 25, 31, 40, 52, 60, 71, 80, 95 }), Transparente = true,
                            Acento = Color.FromArgb(230, 90, 160), ConPortadas = true, Tramada = true },
                new Panel { Titulo = null, Artista = null, Hora = null, N = 0, Insignia = 2, Opaco = true, Acento = Color.FromArgb(255, 221, 132), Tramada = true },
                new Panel { Titulo = "Corto", Artista = "Ñandú", Hora = "0:07 / 2:30", N = 1, Doce = true, Extra = Extra("", Color.FromArgb(90, 200, 120), new[] { 5 }), Acento = Color.FromArgb(90, 200, 120), ConPortadas = true, Tramada = false },
                new Panel { Titulo = "Titulo", Artista = "Artista", Hora = "59:59 / 1:00:00", N = 7, Doce = true, Insignia = 2, Extra = Extra("Rock", Color.FromArgb(200, 60, 40), new[] { 1, 2, 3, 4, 5, 6, 7 }), Transparente = true, Opaco = true,
                            Acento = Color.FromArgb(40, 40, 40), ConPortadas = true, Tramada = false },
                new Panel { Titulo = "Titulo de relleno", Artista = "Artista de relleno", Hora = "0:00 / 0:00", N = 2, Insignia = 1, Extra = Extra("Pop", Color.FromArgb(200, 120, 40), null), Acento = Color.FromArgb(200, 120, 40), Tramada = true },
            };
            int[][] pantallas = { new[] { 1024, 768 }, new[] { 1280, 1024 }, new[] { 800, 600 }, new[] { 1920, 1080 }, new[] { 480, 437 }, new[] { 2560, 1440 } };
            int k = 0;
            foreach (int[] pa in pantallas)
            {
                int w = pa[0], h = GpuPanel.IdlePanelHeight(pa[1]);
                for (int j = 0; j < paneles.Length; j++)
                    foreach (bool cinta in new[] { false, true })
                        using (Bitmap r = HazPanel("panel-" + w + "x" + h + "-" + j + (cinta ? "-cinta" : ""), null, w, h, paneles[j], cinta, false, todas)) { }
                // el mapa que se vuelve a usar: del mismo tamano (el mismo objeto) y de otro (uno nuevo)
                Bitmap p1 = HazPanel("panel-reusa-" + (k++), null, w, h, paneles[0], false, false, todas);
                Bitmap p2 = HazPanel("panel-reusa-" + (k++), p1, w, h, paneles[1], true, false, todas);
                Bitmap p3 = HazPanel("panel-reusa-" + (k++), p2, w, h + 1, paneles[5], false, false, todas);
                if (!ReferenceEquals(p2, p1)) p2.Dispose();
                p1.Dispose(); p3.Dispose();
                // la traducida, directo
                for (int j = 0; j < paneles.Length; j++)
                    using (Bitmap r = HazPanel("panel-directo-" + w + "x" + h + "-" + j, null, w, h, paneles[j], j % 2 == 1, true, todas)) { }
            }
            Form1.InterfazTramada = true;

            // LA LETRA DEL TITULO (FuenteTituloReposo, la que pide SuenaVivoGpu.cs para el titulo que se desliza): sus datos en cada pantalla
            foreach (int[] pa in pantallas)
                using (Font f = Form1.FuenteTituloReposo(pa[0], GpuPanel.IdlePanelHeight(pa[1])))
                    Linea("letra-titulo-" + pa[0] + "x" + pa[1], f.Name + "\t" + R(f.Size) + "\t" + f.Style + "\t" + f.Unit + "\t" + f.Height + "\t" + R(f.GetHeight()) + "\t" + R(f.SizeInPoints));

            // EL RELOJ: la franja de abajo copiada a su mapa, el reloj entero y por trozos (otra hora, mas larga, vacia, con el borde de antes
            // mas a la derecha o en cero), y trozos sueltos copiados a otro sitio
            k = 0;
            foreach (int[] pa in pantallas)
            {
                int w = pa[0], ph = GpuPanel.IdlePanelHeight(pa[1]);
                int comienzo;
                using (Bitmap panel = Form1.RenderIdlePanelEn(null, w, ph, "Titulo de relleno", "Artista de relleno", "", new string[3], new string[3], new Image[3], 3,
                    Color.FromArgb(120, 160, 255), false, 0, false, null, false, out comienzo))
                using (Bitmap debajo = new Bitmap(w, 34, PixelFormat.Format32bppPArgb))
                {
                    Form1.CopiaRegionReposo(panel, new Rectangle(0, ph - 34, w, 34), debajo, Point.Empty);
                    Linea("reloj-debajo-" + w, Puntos("reloj-debajo-" + w, debajo));
                    int ultimo = Form1.UpdateIdleClock(panel, debajo, "0:00 / 0:00");
                    Linea("reloj-entero-" + w, Puntos("reloj-entero-" + w, panel) + "\tderecha=" + ultimo);
                    foreach (string t in new[] { "0:01 / 3:00", "10:59 / 3:00:00", "", "9:59 / 9:59", null, "1:00 / 2:00" })
                    {
                        Rectangle zona;
                        int r = Form1.UpdateIdleClockRegion(panel, debajo, t, ultimo, out zona);
                        string nom = "reloj-trozo-" + w + "-" + (k++);
                        Linea(nom, Puntos(nom, panel) + "\tderecha=" + r + " zona=" + zona);
                        ultimo = r;
                    }
                    foreach (int antes in new[] { 0, w })
                    {
                        Rectangle zona;
                        int r = Form1.UpdateIdleClockRegion(panel, debajo, "3:33 / 4:44", antes, out zona);
                        string nom = "reloj-borde-" + w + "-" + (k++);
                        Linea(nom, Puntos(nom, panel) + "\tderecha=" + r + " zona=" + zona);
                    }
                    using (Bitmap otro = Portada(97, 53, 5, true))
                    {
                        Form1.CopiaRegionReposo(panel, new Rectangle(5, 3, 17, 9), otro, new Point(2, 4));
                        Form1.CopiaRegionReposo(debajo, new Rectangle(w - 40, 0, 40, 34), otro, new Point(57, 19));
                        Linea("copia-" + w, Puntos("copia-" + w, otro));
                    }
                }
            }
            foreach (int[] pa in pantallas)
                foreach (string hora in new[] { "1:23 / 4:56", "", "123:45 / 678:90 con un texto largo que no cabe" })
                    foreach (bool rec in new[] { false, true })
                        Reloj("reloj-directo-" + pa[0] + "-" + (k++), pa[0], GpuPanel.IdlePanelHeight(pa[1]), hora, rec);

            // EL TEXTO QUE SE MUEVE: ocho estados del Graphics, cuatro textos, cinco pinceles (de color, con alfa, degradado, textura y ninguno),
            // cuatro formatos (el compartido incluido), cuatro cajas (una demasiado chica para la cinta) y tres letras, con y sin la captura;
            // y la traducida directo
            k = 0;
            foreach (Estado e in Estados)
                foreach (string t in Textos)
                    for (int v = 0; v < 6; v++)
                    {
                        TextoMovil("movil-" + e.Nombre + "-" + k, e, t, k, false, false, todas);
                        TextoMovil("movil-cinta-" + e.Nombre + "-" + k, e, t, k, true, false, todas);
                        TextoMovil("movil-directo-" + e.Nombre + "-" + k, e, t, k, v % 2 == 0, true, todas);
                        k++;
                    }
            // un formato nulo con la captura (en XP, NullReferenceException al copiarlo)
            using (Bitmap b = new Bitmap(300, 60, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            using (Font f = Letra(0))
            using (SolidBrush br = new SolidBrush(Color.White))
            {
                CintasTexto c = Captura0(); string ex = "";
                try { Form1.PintaTextoMovil(g, Textos[1], f, br, Cajas[0], null); } catch (Exception x) { ex = " EXC:" + x.GetType().Name; } finally { Captura1(c); }
                Linea("movil-nulo", Puntos("movil-nulo", b) + ex + Cintas("movil-nulo", c)); c.Dispose();
            }

            // LA CINTA Y LA DE LA VISTA PREVIA: ocho estados, cuatro textos, con y sin la captura, y directo
            k = 0;
            foreach (Estado e in Estados)
                foreach (string t in Textos)
                    for (int v = 0; v < 4; v++)
                    {
                        ConCinta("cinta-" + e.Nombre + "-" + k, e, t, k, true, false, false);
                        ConCinta("cinta-sin-" + e.Nombre + "-" + k, e, t, k, false, false, false);
                        ConCinta("cinta-directo-" + e.Nombre + "-" + k, e, t, k, true, false, true);
                        ConCinta("vista-" + e.Nombre + "-" + k, e, t, k, true, true, false);
                        ConCinta("vista-directo-" + e.Nombre + "-" + k, e, t, k, v % 2 == 0, true, true);
                        k++;
                    }
            Tanda("tanda");

            // EL ARTISTA DE LA FRANJA: ocho estados, textos que caben y que no, alineaciones, con y sin el letrero MEJORADA, con y sin captura
            k = 0;
            ContentAlignment[] als = { ContentAlignment.MiddleLeft, ContentAlignment.TopLeft, ContentAlignment.BottomRight, ContentAlignment.MiddleCenter };
            foreach (Estado e in Estados)
                foreach (string t in new[] { "Artista de relleno", "Artista Muy Largo con Nombre y Apellidos y su Orquesta Tipica de Siempre", "" })
                    for (int v = 0; v < 4; v++)
                    {
                        Artista("artista-" + e.Nombre + "-" + k, e, t, als[v], v % 2 == 0 ? 420 : 160, v >= 2, false, false);
                        Artista("artista-cinta-" + e.Nombre + "-" + k, e, t, als[v], v % 2 == 0 ? 420 : 160, v >= 2, true, false);
                        Artista("artista-directo-" + e.Nombre + "-" + k, e, t, als[v], v % 2 == 0 ? 420 : 160, v >= 2, v % 2 == 1, true);
                        k++;
                    }

            // EL RECUADRO DE LA FRANJA: ocho estados, tres zonas (una entera, una en el borde, una de un pixel)
            k = 0;
            foreach (Estado e in Estados)
                foreach (Rectangle z in new[] { new Rectangle(30, 10, 140, 28), new Rectangle(300, 70, 120, 40), new Rectangle(7, 7, 1, 1) })
                {
                    Recuadro("recuadro-" + e.Nombre + "-" + k, e, z, false);
                    Recuadro("recuadro-directo-" + e.Nombre + "-" + k, e, z, true);
                    k++;
                }

            Linea("fin", Tipografico() + " degradados=" + Form1.DegradadosGuardados + "/" + Form1.DegradadosReusados);
            foreach (Bitmap b in todas) b.Dispose();
            Console.Error.WriteLine("reposo: " + n + " lineas, " + directas + " llamadas directas a la traducida");
            return 0;
        }
    }
}

// ARNES DEL GRUPO «VISTAS-B» (port nativo, tanda 3 del dibujo, 11-11-2026): las vistas de la segunda mitad de Form1.Vistas.cs (HeaderView,
// PastillaView, GeneroView, ProgressView, SongListView y sus hornos), llamadas con datos fijos y con varios estados del Graphics. Se compila
// con el programa entero de una carpeta Xp (VistasVistasB.csproj, -p:Fuente=...): la referencia congelada (GDI+ de la capa), la base de la
// tanda y esta copia; la salida (una linea por caso: los puntos tal cual y como los sube la tarjeta, lo que devuelve y el estado en que queda
// el Graphics) tiene que salir igual linea por linea (arnes.sh).
// Las vistas se llaman como las llama el codigo de XP que todavia no esta traducido (su OnPaint, DrawCtrlOverlay de Form1.Video.cs, EnSeco):
// RenderContent con un Graphics. En la referencia es la funcion de XP; en la traduccion (desde vistas-disco, que quito esas firmas del puente),
// lo que hacia el puente: la traducida con el lienzo y el estado de ese Graphics. Ademas, el OnPaint de XP contra Pinta (casos -onpaint).
// Los hornos (privados) y los campos, por reflexion.
// Lo que depende del reloj (la moneda que cae, el numero que rueda, la sacudida, la llegada a la cola, el hojeo de la lista) se pinta con el
// reloj quieto: ver ConReloj.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet VistasVistasB.dll [vuelca CARPETA] [solo PREFIJO]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using SkiaSharp;

namespace Banco
{
    internal static class VistasB
    {
        private const BindingFlags T = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta, solo;
        private static int n;

        // ---------------------------------------------------------------- lo que sale
        private static bool Toca(string nombre) { return solo == null || nombre.StartsWith(solo, StringComparison.Ordinal); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static unsafe byte[] Crudos(SKBitmap s)
        {
            byte[] px = new byte[s.Width * s.Height * 4]; byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            return px;
        }
        // los puntos de un mapa (el Bitmap de la capa o el SKBitmap de lo traducido): tal cual (premultiplicados) y como los sube la tarjeta
        // (LockBits Format32bppArgb, sin premultiplicar)
        private static unsafe string Puntos(object o, string nombre)
        {
            if (o == null) return "null";
            Bitmap b = o as Bitmap;
            if (b == null) b = new Bitmap((SKBitmap)o, PixelFormat.Format32bppArgb);   // (solo para leerlo: no se suelta, los puntos son del SKBitmap)
            SKBitmap s = b.Sk; byte[] px = Crudos(s);
            BitmapData d = b.LockBits(new Rectangle(0, 0, s.Width, s.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] sub = new byte[s.Width * s.Height * 4];
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) sub[y * s.Width * 4 + x] = ((byte*)d.Scan0)[y * d.Stride + x];
            b.UnlockBits(d);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre.Replace('/', '_') + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + s.ColorType + "/" + s.AlphaType + "\t" + Huella(px) + "\t" + Huella(sub);
        }
        private static void Apunta(string nombre, string detalle, object mapa) { Linea(nombre, detalle + "\t" + Puntos(mapa, nombre)); }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static string R(float v) { return v.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
        private static string Exc(Exception e) { while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException; return "EXC:" + e.GetType().Name; }

        // ---------------------------------------------------------------- los datos
        private static readonly string[] Familias = { "Microsoft Sans Serif", "Trebuchet MS", "Tahoma", "Times New Roman" };
        private static readonly TextRenderingHint[] Ajustes = { TextRenderingHint.AntiAliasGridFit, TextRenderingHint.SystemDefault, TextRenderingHint.AntiAlias, TextRenderingHint.SingleBitPerPixelGridFit, TextRenderingHint.ClearTypeGridFit, TextRenderingHint.SingleBitPerPixel };
        private static readonly Color[] Acentos = { Color.FromArgb(255, 110, 150, 235), Color.FromArgb(255, 235, 90, 60), Color.FromArgb(255, 40, 30, 60), Color.FromArgb(255, 125, 235, 165), Color.FromArgb(255, 250, 250, 250) };

        // un mapa como el de la imagen de la interfaz (Format32bppPArgb) con un fondo: transparente, a medias o con una franja
        private static Bitmap Mapa(int w, int h, int fondo)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                if (fondo == 0) g.Clear(Color.Transparent);
                else
                {
                    g.Clear(fondo == 1 ? Color.FromArgb(120, 30, 60, 90) : Color.FromArgb(255, 12, 16, 30));
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(200, 250, 20, 60))) g.FillRectangle(sb, 0, h / 3, w, Math.Max(1, h / 7));
                }
            }
            return b;
        }
        // una portada inventada: opaca o con alfa, con manchas
        private static Bitmap Portada(int w, int h, int semilla, bool alfa)
        {
            Random r = new Random(semilla);
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(alfa ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(255, r.Next(256), r.Next(256), r.Next(256)));
                for (int i = 0; i < 9; i++)
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(alfa ? 60 + r.Next(196) : 255, r.Next(256), r.Next(256), r.Next(256))))
                    {
                        if (i % 2 == 0) g.FillRectangle(sb, r.Next(w), r.Next(h), 1 + r.Next(w / 2), 1 + r.Next(h / 2));
                        else g.FillEllipse(sb, r.Next(w) - w / 4, r.Next(h) - h / 4, 1 + r.Next(w), 1 + r.Next(h));
                    }
            }
            return b;
        }
        // EL ESTADO DEL GRAPHICS: 0 = el de la imagen de la interfaz con tarjeta (AntiAlias, AntiAliasGridFit, trasladado: DrawCtrlOverlay);
        // 1 = el de un OnPaint (uno nuevo: sin suavizar, SystemDefault, trasladado a su sitio); si no, al azar: suavizado, modo de letra,
        // copia, medio pixel, filtro, traslacion entera o con decimales, escala, giro y recortes (que reemplaza, que se suma, de un trazo)
        private static string PonEstado(Graphics g, Random r, int w, int h, int cual)
        {
            bool tipico = cual == 0, nuevo = cual == 1;
            SmoothingMode s = tipico ? SmoothingMode.AntiAlias : nuevo ? SmoothingMode.None : new[] { SmoothingMode.AntiAlias, SmoothingMode.None, SmoothingMode.HighQuality, SmoothingMode.Default }[r.Next(4)];
            TextRenderingHint t = tipico ? TextRenderingHint.AntiAliasGridFit : nuevo ? TextRenderingHint.SystemDefault : Ajustes[r.Next(Ajustes.Length)];
            CompositingMode c = tipico || nuevo || r.Next(3) > 0 ? CompositingMode.SourceOver : CompositingMode.SourceCopy;
            PixelOffsetMode p = tipico || nuevo || r.Next(3) > 0 ? PixelOffsetMode.Default : new[] { PixelOffsetMode.Half, PixelOffsetMode.HighQuality, PixelOffsetMode.None }[r.Next(3)];
            InterpolationMode im = tipico || nuevo || r.Next(2) > 0 ? InterpolationMode.Bilinear : new[] { InterpolationMode.NearestNeighbor, InterpolationMode.HighQualityBicubic, InterpolationMode.HighQualityBilinear, InterpolationMode.Low }[r.Next(4)];
            g.SmoothingMode = s; g.TextRenderingHint = t; g.CompositingMode = c; g.PixelOffsetMode = p; g.InterpolationMode = im;
            int tr = tipico || nuevo ? 1 : r.Next(5), cl = tipico || nuevo ? 0 : r.Next(4);
            float dx = 0, dy = 0;
            switch (tr)
            {
                case 1: dx = r.Next(0, 9); dy = r.Next(0, 9); g.TranslateTransform(dx, dy); break;
                case 2: dx = r.Next(0, 60) / 8f; dy = r.Next(0, 30) / 4f; g.TranslateTransform(dx, dy); break;
                case 3: g.ScaleTransform(1.3f, 0.9f); break;
                case 4: g.TranslateTransform(6, 6); g.RotateTransform(r.Next(-8, 8)); break;
            }
            string recorte = "-";
            if (cl == 1) { Rectangle k = new Rectangle(r.Next(w / 3), r.Next(h / 3), w / 2 + r.Next(w / 2), h / 2 + r.Next(h / 2)); g.SetClip(k); recorte = "R" + k; }
            else if (cl == 2) { g.SetClip(new Rectangle(2, 2, w - 4, h - 4)); Rectangle k = new Rectangle(r.Next(w / 2), 0, w, h - r.Next(h / 2)); g.SetClip(k, CombineMode.Intersect); recorte = "I" + k; }
            else if (cl == 3) { using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(-w / 4f, -h / 4f, w * 1.4f, h * 1.5f); g.SetClip(gp); } recorte = "P"; }
            return s + "," + t + "," + c + "," + p + "," + im + ",tr" + tr + "(" + R(dx) + "," + R(dy) + ")," + recorte;
        }
        // el estado en que queda el Graphics despues de la vista (lo que le deja puesto, y que el Save/Restore vuelva a su sitio)
        private static string Queda(Graphics g)
        {
            SKMatrix m = g.C.TotalMatrix; RectangleF cb = g.ClipBounds;
            return g.SmoothingMode + "," + g.TextRenderingHint + "," + g.CompositingMode + "," + g.PixelOffsetMode + "," + g.InterpolationMode + ",m" + R(m.ScaleX) + ";" + R(m.SkewX) + ";" + R(m.TransX) + ";" + R(m.SkewY) + ";" + R(m.ScaleY) + ";" + R(m.TransY) + ";n" + g.C.SaveCount
                + ",c" + R(cb.X) + ";" + R(cb.Y) + ";" + R(cb.Width) + ";" + R(cb.Height);
        }

        // ---------------------------------------------------------------- reflexion
        private static Type Tipo(string nombre) { return typeof(Form1).GetNestedType(nombre, BindingFlags.NonPublic | BindingFlags.Public); }
        private static object Anidada(string nombre) { return Activator.CreateInstance(Tipo(nombre), true); }
        private static FieldInfo Campo(object o, string campo)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(campo, T | BindingFlags.DeclaredOnly); if (fi != null) return fi; }
            throw new MissingFieldException(o.GetType().Name, campo);
        }
        // (cache-portadas, 12-11-2026) la cache de portadas de la traduccion guarda SKBitmap: lo que el arnes da como Image de la capa (una
        // portada, un arreglo de portadas o un delegado que devuelve una) se le da a la traduccion como sus puntos (Image.Sk: el mismo SKBitmap
        // para la misma Image, asi que la identidad que XP compara se conserva); a la referencia, tal cual
        public static SKBitmap SkDe(Rocola.Dibujo2D.Image i) { return i != null ? i.Sk : null; }
        private static object Adapta(Type destino, object valor)
        {
            if (valor == null || destino.IsInstanceOfType(valor)) return valor;
            if (destino == typeof(SKBitmap) && valor is Rocola.Dibujo2D.Image) return ((Rocola.Dibujo2D.Image)valor).Sk;
            if (destino == typeof(SKBitmap[]) && valor is Rocola.Dibujo2D.Image[]) { Rocola.Dibujo2D.Image[] a = (Rocola.Dibujo2D.Image[])valor; SKBitmap[] r = new SKBitmap[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = SkDe(a[i]); return r; }
            if (typeof(Delegate).IsAssignableFrom(destino) && valor is Delegate)
            {
                MethodInfo inv = destino.GetMethod("Invoke"), de = valor.GetType().GetMethod("Invoke");
                ParameterInfo[] ips = inv.GetParameters(), dps = de.GetParameters();
                System.Linq.Expressions.ParameterExpression[] ps = new System.Linq.Expressions.ParameterExpression[ips.Length];
                System.Linq.Expressions.Expression[] args = new System.Linq.Expressions.Expression[ips.Length];
                for (int i = 0; i < ips.Length; i++) { ps[i] = System.Linq.Expressions.Expression.Parameter(ips[i].ParameterType, "p" + i); args[i] = System.Linq.Expressions.Expression.Convert(ps[i], dps[i].ParameterType); }
                System.Linq.Expressions.Expression cuerpo = System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(valor), args);
                if (inv.ReturnType == typeof(SKBitmap) && typeof(Rocola.Dibujo2D.Image).IsAssignableFrom(de.ReturnType)) cuerpo = System.Linq.Expressions.Expression.Call(typeof(VistasB).GetMethod("SkDe"), cuerpo);
                else if (inv.ReturnType != de.ReturnType) cuerpo = System.Linq.Expressions.Expression.Convert(cuerpo, inv.ReturnType);
                return System.Linq.Expressions.Expression.Lambda(destino, cuerpo, ps).Compile();
            }
            return valor;
        }
        private static void Pon(object o, string campo, object v) { FieldInfo f = Campo(o, campo); f.SetValue(o, Adapta(f.FieldType, v)); }
        private static object Lee(object o, string campo) { return Campo(o, campo).GetValue(o); }
        // la firma de XP con un Graphics: en la referencia, la de XP. En la traduccion (vistas-disco, 12-11-2026: las vistas ya no heredan de Control
        // y sus firmas con un Graphics se quitaron del puente) lo mismo que hacia el puente: la traducida con el lienzo y el estado del Graphics, y
        // despues lo que XP le deja puesto (el suavizado, con las mismas cuentas)
        private static Action<Graphics> ConGraphics(object o, string nombre)
        {
            MethodInfo xp = o.GetType().GetMethod(nombre, T, null, new[] { typeof(Graphics) }, null);
            if (xp != null) return delegate (Graphics g) { xp.Invoke(o, new object[] { g }); };
            return delegate (Graphics g) { ComoElPuente(o, nombre, g, false); };
        }
        private static Type PuenteT { get { return typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo"); } }
        private static int traducidas, pintas;
        // (pinta = true: Pinta, la de la pantalla nativa, que en XP era su OnPaint: RenderContent con el estado de fabrica de un WM_PAINT)
        private static void ComoElPuente(object o, string nombre, Graphics g, bool pinta)
        {
            object e = PuenteT.GetMethod("Estado", T, null, new[] { typeof(Graphics) }, null).Invoke(null, new object[] { g });
            object lienzo = PuenteT.GetMethod("Lienzo", T, null, new[] { typeof(Graphics) }, null).Invoke(null, new object[] { g });
            Func<string, object> c = delegate (string f) { return e.GetType().GetField(f).GetValue(e); };
            object[] args; string tipo = o.GetType().Name;
            if (tipo == "HeaderView" || tipo == "GeneroView") args = new[] { lienzo, c("Letra"), c("Copia"), c("Medio") };
            else if (tipo == "PastillaView") args = new[] { lienzo, c("Letra"), c("Copia"), c("Filtro"), c("Medio") };
            else if (tipo == "ProgressView") args = new[] { lienzo, c("Copia"), c("Medio") };
            else args = new[] { lienzo, c("Copia"), c("Filtro"), c("Medio") };
            if (pinta) { pintas++; o.GetType().GetMethod("Pinta", T, null, new[] { typeof(SKCanvas) }, null).Invoke(o, new[] { lienzo }); }
            else
            {
                traducidas++;
                MethodInfo tr = null;
                foreach (MethodInfo m in o.GetType().GetMethods(T)) if (m.Name == nombre && m.GetParameters().Length == args.Length && m.GetParameters()[0].ParameterType == typeof(SKCanvas)) tr = m;
                tr.Invoke(o, args);
            }
            int w = Medida(o, "Width"), h = Medida(o, "Height");
            bool deja = tipo == "HeaderView" || tipo == "PastillaView" ? !(w < 8 || h < 8) : tipo == "GeneroView" ? !(w < 6 || h < 6 || ((string)Lee(o, "texto")).Length == 0) : tipo == "ProgressView";
            if (deja) g.SmoothingMode = SmoothingMode.AntiAlias;
        }
        // (vistas-disco) lo que XP usa de la vista y le daba Control (la referencia) o su CajaVista y sus propiedades (la traduccion), por reflexion
        private static void PonProp(object o, string p, object v) { PropertyInfo pi = o.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance); pi.SetValue(o, LetraSiHace(pi.PropertyType, v)); }
        // (rotulos-y-letras) la traduccion pide una Letra donde la referencia pide la Font (la Font de las vistas): la de esa Font (Font.L, la
        // misma que da PuenteDibujo.Letra: misma Font, misma Letra); su familia y su tamano se leen de cualquiera de las dos
        private static object LetraSiHace(Type destino, object v)
        {
            if (v is Font && destino.Name == "Letra") return typeof(Font).GetField("L", T).GetValue(v);
            return v;
        }
        private static string FamiliaDe(object f) { Font fo = f as Font; return fo != null ? fo.Name : (string)f.GetType().GetField("Familia").GetValue(f); }
        private static float TamanoDe(object f) { Font fo = f as Font; return fo != null ? fo.Size : (float)f.GetType().GetField("Tamano").GetValue(f); }
        private static object LeeProp(object o, string p) { return o.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance).GetValue(o); }
        private static int Medida(object o, string p) { return (int)LeeProp(o, p); }
        // EL DIBUJO DE UN WM_PAINT (vistas-disco): en la referencia, su OnPaint con el Graphics como lo da el escritorio (nuevo, en su sitio de la
        // matriz y con su recorte sellado); en la traduccion, Pinta con el lienzo de ese Graphics (lo que la pantalla le da), y lo que XP le dejaba
        // al Graphics. No toca el azar de los demas casos.
        private static void PintaComoOnPaint(string nombre, string detalle, object vista, int w, int h, int fondo)
        {
            if (!Toca(nombre)) return;
            Bitmap b = Mapa(w + 44, h + 30, fondo); string res, queda = "";
            using (Graphics g = Graphics.FromImage(b))
            {
                g.TranslateTransform(13, 9); g.SetClip(new Rectangle(0, 0, w, h)); g.SellaRecorte();
                try
                {
                    MethodInfo op = vista.GetType().GetMethod("OnPaint", T);
                    if (op != null) using (PaintEventArgs pe = new PaintEventArgs(g, new Rectangle(0, 0, w, h))) op.Invoke(vista, new object[] { pe });
                    else ComoElPuente(vista, "RenderContent", g, true);
                    res = "bien"; queda = Queda(g);
                }
                catch (Exception e) { res = Exc(e); }
            }
            Apunta(nombre, detalle + "|onpaint|" + res + "|" + queda, b);
            b.Dispose();
        }
        // pinta una vista en un mapa con fondo, con el estado 'cual', y apunta los puntos y como queda el Graphics
        private static void Pinta(string nombre, string detalle, object vista, string metodo, int w, int h, int fondo, int cual, Random r)
        {
            if (!Toca(nombre)) { r.Next(); return; }
            Bitmap b = Mapa(w + 44, h + 30, fondo); string res, estado, queda = "";
            using (Graphics g = Graphics.FromImage(b))
            {
                estado = PonEstado(g, r, w + 44, h + 30, cual);
                try { ConGraphics(vista, metodo)(g); res = "bien"; queda = Queda(g); } catch (Exception e) { res = Exc(e); }
            }
            Apunta(nombre, detalle + "|" + estado + "|" + res + "|" + queda, b);
            b.Dispose();
        }
        // LO QUE DEPENDE DEL RELOJ. Las vistas leen Environment.TickCount una vez, al empezar a pintar: 'pon(ahora)' deja sus relojes a los
        // milisegundos que toque de 'ahora'. 'prepara' hace el mapa y su Graphics con su estado (lo que tarda), y 'pinta' llama a la vista y
        // dice lo que salio. Primero una vez para calentar (lo que se hornea la primera vez no cae en el instante medido); despues, hasta 15
        // veces: se prepara, se espera a que empiece un milisegundo, se ponen los relojes y se pinta (lo que va antes de leer el reloj tarda
        // microsegundos); vale lo que salga dos veces.
        private static string ConReloj(Action<int> pon, Action prepara, Func<string> pinta)
        {
            prepara(); pon(Environment.TickCount); pinta();
            Dictionary<string, int> vistos = new Dictionary<string, int>();
            for (int k = 0; k < 15; k++)
            {
                prepara();
                int t0 = Environment.TickCount; while (Environment.TickCount == t0) { }
                pon(Environment.TickCount);
                string s = pinta();
                int v; vistos.TryGetValue(s, out v); vistos[s] = v + 1;
                if (v + 1 >= 2) return s;
            }
            return "RELOJ:sin dos iguales";
        }

        // ---------------------------------------------------------------- HeaderView
        private static void Encabezados(Random r)
        {
            string[] secciones = { "LO MAS SONADO", "LETRA W", "LETRA Ñ", "VIDEO", "ÉXITOS DE LOS 80 & MÁS — una sección con el nombre muy largo que no cabe", "", null, "A" };
            int[][] paginas = { new[] { 0, 1 }, new[] { 0, 3 }, new[] { 2, 3 }, new[] { 11, 12 }, new[] { 5, 13 }, new[] { 17, 18 }, new[] { 119, 120 }, new[] { -1, 4 }, new[] { 9, 4 }, new[] { 0, 0 }, new[] { 3, 12 } };
            int[][] medidas = { new[] { 420, 28 }, new[] { 300, 28 }, new[] { 200, 28 }, new[] { 150, 28 }, new[] { 640, 44 }, new[] { 90, 20 }, new[] { 7, 28 }, new[] { 300, 7 }, new[] { 520, 36 } };
            int caso = 0;
            foreach (int[] m in medidas)
                foreach (int[] pg in paginas)
                {
                    object v = Anidada("HeaderView");
                    string sec = secciones[caso % secciones.Length];
                    PonProp(v, "Font", new Font(Familias[caso % Familias.Length], caso % 3 == 0 ? 12.5f : caso % 3 == 1 ? 10f : 16f, caso % 5 == 4 ? FontStyle.Regular : FontStyle.Bold));
                    PonProp(v, "Bounds", new Rectangle(0, 0, m[0], m[1]));
                    Pon(v, "Section", sec); Pon(v, "Page", pg[0]); Pon(v, "Total", pg[1]); Pon(v, "Accent", Acentos[caso % Acentos.Length]);
                    string det = m[0] + "x" + m[1] + "|" + (sec ?? "null") + "|" + pg[0] + "/" + pg[1] + "|" + FamiliaDe(LeeProp(v, "Font")) + " " + R(TamanoDe(LeeProp(v, "Font")));
                    for (int k = 0; k < 5; k++) Pinta("encabezado-" + caso + "-" + k, det, v, "RenderContent", m[0], m[1], (caso + k) % 3, Math.Min(k, 2), r);
                    PintaComoOnPaint("encabezado-" + caso + "-onpaint", det, v, m[0], m[1], caso % 3);
                    if (Toca("encabezado-" + caso + "-pide"))
                    {
                        MethodInfo pide = v.GetType().GetMethod("AnchoPedido", T);
                        if (pide != null) Linea("encabezado-" + caso + "-pide", m[1] + "|" + pide.Invoke(v, new object[] { m[1] }) + "|" + pide.Invoke(v, new object[] { m[1] }) + "|" + pide.Invoke(v, new object[] { m[1] + 6 }));
                    }
                    ((IDisposable)v).Dispose(); caso++;
                }
        }

        // ---------------------------------------------------------------- PastillaView
        private static object Capsula(int caso, int w, int h, Bitmap[] tapas)
        {
            object v = Anidada("PastillaView");
            PonProp(v, "Font", new Font(Familias[caso % Familias.Length], 12.5f, FontStyle.Bold));
            PonProp(v, "Bounds", new Rectangle(0, 0, w, h));
            string[] etiquetas = { "Inserta una moneda", "Créditos", "En cola", "", null, "Ñandú & Cía" };
            string[] valores = { "", "3", "12", "0", null, "128" };
            Pon(v, "Etiqueta", etiquetas[caso % etiquetas.Length]); Pon(v, "Valor", valores[(caso / 2) % valores.Length]);
            Pon(v, "Acento", Acentos[caso % Acentos.Length]);
            bool con = caso % 3 != 0; Pon(v, "ConPortadas", con);
            if (con && caso % 4 != 1)
            {
                Image[] p = new Image[caso % 5 == 2 ? 2 : 3];
                for (int i = 0; i < p.Length; i++) p[i] = (i + caso) % 4 == 3 ? null : tapas[(i + caso) % tapas.Length];
                Pon(v, "Portadas", p);
            }
            Pon(v, "Mas", caso % 4 == 2 ? 2 : caso % 7 == 3 ? 14 : 0);
            return v;
        }
        private static void Capsulas(Random r)
        {
            Bitmap[] tapas = { Portada(600, 600, 3, false), Portada(300, 300, 5, true), Portada(160, 100, 9, false), Portada(20, 20, 13, true) };
            int[][] medidas = { new[] { 170, 34 }, new[] { 330, 34 }, new[] { 150, 26 }, new[] { 420, 52 }, new[] { 7, 34 }, new[] { 60, 9 } };
            Type tp = Tipo("PastillaView");
            // 1) quieta, por RenderContent (OnPaint) y por RenderContentGpu (DrawCtrlOverlay)
            for (int caso = 0; caso < 36; caso++)
            {
                int[] m = medidas[caso % medidas.Length];
                object v = Capsula(caso, m[0], m[1], tapas);
                string det = m[0] + "x" + m[1] + "|" + (Lee(v, "Etiqueta") ?? "null") + "|" + (Lee(v, "Valor") ?? "null") + "|" + Lee(v, "ConPortadas") + "|" + Lee(v, "Mas");
                for (int k = 0; k < 5; k++) Pinta("capsula-" + caso + "-" + k, det, v, k % 2 == 1 ? "RenderContent" : "RenderContentGpu", m[0], m[1], (caso + k) % 3, Math.Min(k, 2), r);
                PintaComoOnPaint("capsula-" + caso + "-onpaint", det, v, m[0], m[1], caso % 3);
                if (Toca("capsula-" + caso + "-mide"))
                {
                    string res;
                    try
                    {
                        MethodInfo ancho = tp.GetMethod("Ancho", T, null, new[] { typeof(string), typeof(string), typeof(int), typeof(FontFamily), typeof(bool) }, null);
                        if (ancho != null)
                            res = ancho.Invoke(null, new object[] { Lee(v, "Etiqueta"), Lee(v, "Valor"), m[1], ((Font)LeeProp(v, "Font")).FontFamily, Lee(v, "ConPortadas") }) + "|"
                                + ancho.Invoke(null, new object[] { Lee(v, "Etiqueta"), Lee(v, "Valor"), m[1] + 9, caso % 2 == 0 ? null : new FontFamily("Times New Roman"), true });
                        else
                        {
                            // (rotulos-y-letras) la traduccion ya no tiene la firma con la FontFamily (Puente.vistas-b.cs, borrado): sus llamadores le dan la
                            // familia por su nombre; la nula, la generica sin remates (como la tomaba ese puente: new Font(null, ...) de la capa)
                            ancho = tp.GetMethod("Ancho", T, null, new[] { typeof(string), typeof(string), typeof(int), typeof(string), typeof(bool) }, null);
                            res = ancho.Invoke(null, new object[] { Lee(v, "Etiqueta"), Lee(v, "Valor"), m[1], FamiliaDe(LeeProp(v, "Font")), Lee(v, "ConPortadas") }) + "|"
                                + ancho.Invoke(null, new object[] { Lee(v, "Etiqueta"), Lee(v, "Valor"), m[1] + 9, caso % 2 == 0 ? "Microsoft Sans Serif" : new FontFamily("Times New Roman").Name, true });
                        }
                        MethodInfo xn = tp.GetMethod("XNumero", T);
                        if (xn != null) res += "|" + R((float)xn.Invoke(v, null));
                    }
                    catch (Exception e) { res = Exc(e); }
                    Linea("capsula-" + caso + "-mide", res);
                }
                ((IDisposable)v).Dispose();
            }
            // 2) con el reloj: la moneda que cae (y su destello y el numero que rueda), la sacudida del aviso, la llegada a la cola (el saltito,
            // el destello y el rodillo: solo por RenderContentGpu) y el golpe de la moneda grande
            int[] dts = { 0, 60, 200, 330, 400, 419, 420, 470, 560, 650, 700, 930, 941, 2000, -50 };
            for (int caso = 0; caso < 15; caso++)
                for (int modo = 0; modo < 5; modo++)
                {
                    string nombre = "capsula-reloj-" + caso + "-" + modo;
                    if (!Toca(nombre)) continue;
                    int[] m = medidas[(caso + modo) % 4];
                    object v = Capsula(caso * 3 + modo + 1, m[0], m[1], tapas);
                    int dt = dts[caso];
                    bool gpu = modo >= 2; int cual = (caso + modo) % 3; int fondo = (caso + modo) % 3;
                    FieldInfo fMon = Campo(v, "monTick"), fRod = Campo(v, "rodTick"), fAvi = Campo(v, "aviTick"), fDe = Campo(v, "rodDe");
                    FieldInfo fLlega = v.GetType().GetField("llegaTick", T), fLlegaDe = v.GetType().GetField("llegaDe", T), fGrande = v.GetType().GetField("MonedaGrande", T);
                    Action<int> pon = delegate (int ahora)
                    {
                        int q = ahora - dt; if (q == 0) q = 1;
                        fMon.SetValue(v, 0); fRod.SetValue(v, 0); fAvi.SetValue(v, 0); fDe.SetValue(v, caso % 2 == 0 ? "7" : "");
                        if (fLlega != null) fLlega.SetValue(v, 0);
                        if (fGrande != null) fGrande.SetValue(v, modo == 4);
                        switch (modo)
                        {
                            case 0: fMon.SetValue(v, q); fRod.SetValue(v, q + 420); break;          // la moneda (como Moneda())
                            case 1: fAvi.SetValue(v, q); break;                                      // el aviso de sin creditos
                            case 2: if (fLlega != null) { fLlega.SetValue(v, q); fLlegaDe.SetValue(v, "41"); } break;   // la cancion que llega a la cola
                            case 3: fMon.SetValue(v, q); fRod.SetValue(v, q + 420); fAvi.SetValue(v, q + 100); break;   // moneda y aviso a la vez
                            case 4: fMon.SetValue(v, q); fRod.SetValue(v, q + 420); break;          // la moneda grande de la tarjeta: sin monedita y con golpe
                        }
                    };
                    Random semilla = new Random(1000 + caso * 7 + modo);
                    int sem = semilla.Next(); Action<Graphics> metodo = ConGraphics(v, gpu ? "RenderContentGpu" : "RenderContent");
                    Bitmap b = null; Graphics g = null; string estado = null;
                    string hecho = ConReloj(pon, delegate
                    {
                        b = Mapa(m[0] + 44, m[1] + 30, fondo); g = Graphics.FromImage(b);
                        estado = PonEstado(g, new Random(sem), m[0] + 44, m[1] + 30, cual);
                    }, delegate
                    {
                        string res, queda = "";
                        try { metodo(g); res = "bien"; queda = Queda(g); } catch (Exception e) { res = Exc(e); }
                        g.Dispose();
                        string s = estado + "|" + res + "|" + queda + "\t" + Puntos(b, nombre);
                        b.Dispose(); return s;
                    });
                    Linea(nombre, m[0] + "x" + m[1] + "|dt " + dt + "|" + hecho);
                    ((IDisposable)v).Dispose();
                }
        }

        // ---------------------------------------------------------------- GeneroView
        private static void Generos(Random r)
        {
            string[] textos = { "Rock en español", "CUMBIA", "Ñ", "Baladas & boleros — de los 80", "", "Pop" };
            int[][] medidas = { new[] { 110, 18 }, new[] { 64, 22 }, new[] { 200, 30 }, new[] { 5, 18 }, new[] { 90, 6 }, new[] { 40, 14 } };
            for (int caso = 0; caso < 24; caso++)
            {
                object v = Anidada("GeneroView");
                PonProp(v, "Font", new Font(Familias[caso % Familias.Length], caso % 2 == 0 ? 10f : 13.5f, caso % 3 == 2 ? FontStyle.Italic : FontStyle.Bold));
                int[] m = medidas[caso % medidas.Length];
                PonProp(v, "Bounds", new Rectangle(0, 0, m[0], m[1]));
                string txt = textos[(caso / 2) % textos.Length];
                v.GetType().GetProperty("Texto", T).SetValue(v, txt); Pon(v, "Tinte", Acentos[caso % Acentos.Length]);
                for (int k = 0; k < 5; k++) Pinta("genero-" + caso + "-" + k, m[0] + "x" + m[1] + "|" + txt, v, "RenderContent", m[0], m[1], (caso + k) % 3, Math.Min(k, 2), r);
                PintaComoOnPaint("genero-" + caso + "-onpaint", m[0] + "x" + m[1] + "|" + txt, v, m[0], m[1], caso % 3);
                if (Toca("genero-" + caso + "-pide")) Linea("genero-" + caso + "-pide", v.GetType().GetMethod("AnchoQuePide", T).Invoke(v, null) + "|" + v.GetType().GetMethod("AltoQuePide", T).Invoke(v, null));
                ((IDisposable)v).Dispose();
            }
        }

        // ---------------------------------------------------------------- ProgressView
        private static void Barras(Random r)
        {
            int[][] medidas = { new[] { 624, 8 }, new[] { 300, 12 }, new[] { 120, 5 }, new[] { 3, 8 }, new[] { 200, 3 }, new[] { 400, 20 }, new[] { 9, 4 } };
            double[] valores = { 0, 0.004, 0.02, 0.33, 0.5, 0.999, 1, 2, double.NaN };
            for (int caso = 0; caso < 42; caso++)
            {
                object v = Anidada("ProgressView");
                int[] m = medidas[caso % medidas.Length];
                PonProp(v, "Bounds", new Rectangle(0, 0, m[0], m[1]));
                bool neon = caso % 2 == 1; double val = valores[caso % valores.Length];
                v.GetType().GetProperty("Neon", T).SetValue(v, neon); v.GetType().GetProperty("Value", T).SetValue(v, val); v.GetType().GetProperty("Accent", T).SetValue(v, Acentos[caso % Acentos.Length]);
                for (int k = 0; k < 7; k++) Pinta("barra-" + caso + "-" + k, m[0] + "x" + m[1] + "|" + neon + "|" + val.ToString("R", System.Globalization.CultureInfo.InvariantCulture), v, "RenderContent", m[0], m[1], (caso + k) % 3, Math.Min(k, 2), r);
                PintaComoOnPaint("barra-" + caso + "-onpaint", m[0] + "x" + m[1] + "|" + neon, v, m[0], m[1], caso % 3);
                ((IDisposable)v).Dispose();
            }
        }

        // ---------------------------------------------------------------- SongListView
        private static readonly string[] GenerosLista = { "Rock", "", "Cumbia", "Rock en español", null, "Baladas" };
        private static object NuevaLista(int caso, int w, int h, int filas, int rowH, Font letra, int sel, int inicio, Color acento)
        {
            object l = Anidada("SongListView");
            PonProp(l, "Bounds", new Rectangle(0, 0, w, h)); PonProp(l, "Font", letra);
            Pon(l, "RowH", rowH); Pon(l, "Sel", sel); Pon(l, "PageStart", inicio); Pon(l, "Accent", acento);
            Pon(l, "GetCount", (Func<int>)(() => filas));
            Pon(l, "GetText", (Func<int, string>)(i => (i + 1).ToString("00") + " " + (i % 3 == 0 ? "Ángel de amor (versión en vivo) — Los Ñandúes & Cía, una larga" : i % 3 == 1 ? "Cancion " + i : "Otra & mas")));
            Pon(l, "GetRight", (Func<int, string>)(i => i % 4 == 3 ? "" : (2 + i % 5) + ":" + (10 + i * 7 % 50).ToString("00")));
            Pon(l, "GetQueuePos", (Func<int, int>)(i => i == 3 ? 1 : i == 6 ? 12 : i == 5 && caso % 2 == 0 ? 2 : 0));
            Pon(l, "GetSonando", (Func<int, bool>)(i => i == 5 || (i == 1001 && caso % 3 == 0)));
            Pon(l, "GetGenero", (Func<int, string>)(i => GenerosLista[(i + caso) % GenerosLista.Length]));
            return l;
        }
        private static void Listas(Random r)
        {
            Type tl = Tipo("SongListView");
            MethodInfo capa = tl.GetMethod("BakeGpuListLayer", T), hojea = tl.GetMethod("StartSlide", T), olvida = tl.GetMethod("OlvidaPagina", T);
            FieldInfo fVivo = tl.GetField("Vivo", T), fEspera = tl.GetField("GetEspera", T), fGpu = tl.GetField("pintandoGpu", T);
            int[][] medidas = { new[] { 556, 464, 34 }, new[] { 250, 464, 30 }, new[] { 556, 468, 36 }, new[] { 120, 90, 30 }, new[] { 800, 400, 52 }, new[] { 3, 300, 30 }, new[] { 420, 200, 22 } };
            for (int caso = 0; caso < 42; caso++)
            {
                int[] m = medidas[caso % medidas.Length];
                Font letra = new Font(Familias[(caso / 7) % Familias.Length], caso % 3 == 0 ? 14.25f : caso % 3 == 1 ? 12f : 17f, FontStyle.Bold);
                int filas = new[] { 20, 5, 40, 0 }[caso % 4], rpp = Math.Max(1, m[1] / m[2]);
                int ultima = Math.Max(0, Math.Min(filas, rpp) - 1);
                int sel = caso % 5 == 0 ? 0 : caso % 5 == 1 ? ultima : caso % 5 == 2 ? -1 : r.Next(ultima + 1), inicio = (caso % 6 == 5 && filas > rpp) ? rpp : 0;
                if (inicio > 0) sel += inicio;
                Color acento = Color.FromArgb(r.Next(256), r.Next(256), r.Next(256));
                object l = NuevaLista(caso, m[0], m[1], filas, m[2], letra, sel, inicio, acento);
                PonProp(l, "BackColor", caso % 2 == 0 ? Color.FromArgb(31, 34, 52) : Color.FromArgb(12, 20, 40));
                int vivo = (caso / 2) % 8;
                if (fVivo != null) fVivo.SetValue(l, vivo);
                if (fEspera != null) fEspera.SetValue(l, (Func<int, int>)(pos => pos == 1 ? 0 : pos == 2 ? -1 : pos * 4 - 3));
                if (fGpu != null) fGpu.SetValue(l, caso % 3 != 2);
                string pre = "lista-" + caso, det = m[0] + "x" + m[1] + "/" + m[2] + "|" + filas + "|sel " + sel + "|ini " + inicio + "|vivo " + vivo + "|" + letra.Name + " " + R(letra.Size);
                // 1) la lista como la pinta su OnPaint o la imagen de la interfaz: la pagina horneada, copiada
                for (int k = 0; k < 5; k++) Pinta(pre + "-" + k, det, l, "RenderContent", m[0], m[1], (caso + k) % 3, Math.Min(k, 2), r);
                PintaComoOnPaint(pre + "-onpaint", det, l, m[0], m[1], caso % 3);
                if (Toca(pre + "-horno")) Apunta(pre + "-horno", "hornoPag", Lee(l, "hornoPag"));
                // 2) los hornos: los circulitos, las cifras, las pastillas de las marcas y las del genero
                if (Toca(pre + "-hornos"))
                {
                    foreach (string campo in new[] { "bmpCirc", "bmpCircCola", "bmpCircSon" }) Apunta(pre + "-hornos-" + campo, campo, Lee(l, campo));
                    foreach (string campo in new[] { "bmpDig", "bmpDigCola", "bmpDigSon" })
                    {
                        Array a = (Array)Lee(l, campo);
                        if (a == null) { Linea(pre + "-hornos-" + campo, "null"); continue; }
                        for (int d = 0; d < a.Length; d += (caso % 4 == 0 ? 1 : 4)) Apunta(pre + "-hornos-" + campo + d, campo, a.GetValue(d));
                    }
                    Linea(pre + "-hornos-dig", Lee(l, "digW") + "x" + Lee(l, "digH") + "|" + Lee(l, "hornoLado") + "|" + R((float)Lee(l, "hornoCuerpo")));
                    if (Lee(l, "fCola") != null)
                    {
                        foreach (string gen in new[] { "Rock", "Cumbia", "Rock en español", "Ñ & más", "" })
                        {
                            object pg; string res = "bien";
                            try { pg = tl.GetMethod("HorneaGenero", T).Invoke(l, new object[] { gen }); } catch (Exception e) { pg = null; res = Exc(e); }
                            Apunta(pre + "-hornos-genero-" + gen, res, pg);
                        }
                        foreach (int ancho in new[] { 70, 3, 131 })
                        {
                            Apunta(pre + "-hornos-pastilla-v" + ancho, "verde", tl.GetMethod("Pastilla", T).Invoke(l, new object[] { false, ancho, (int)Lee(l, "hornoPastAlto") }));
                            Apunta(pre + "-hornos-pastilla-a" + ancho, "ambar", tl.GetMethod("Pastilla", T).Invoke(l, new object[] { true, ancho, (int)Lee(l, "hornoPastAlto") }));
                        }
                    }
                }
                // 3) las capas de la tarjeta (AcabadoGpu.cs: llaman a PintaPagina con un lienzo nuevo; la de la fila elegida, dos veces)
                if (capa != null && m[0] >= 4 && Toca(pre + "-capa"))
                {
                    int selAntes = (int)Lee(l, "Sel");
                    try
                    {
                        object c0 = capa.Invoke(l, new object[] { false }); Apunta(pre + "-capa", "sin seleccion", c0); ((IDisposable)c0).Dispose();
                        if (selAntes >= 0 && filas > 0)
                        {
                            object c1 = capa.Invoke(l, new object[] { true }); Apunta(pre + "-capa-fila", "sel " + selAntes, c1); ((IDisposable)c1).Dispose();
                            Apunta(pre + "-capa-horno", "hornoPag", Lee(l, "hornoPag"));
                        }
                    }
                    catch (Exception e) { Linea(pre + "-capa", Exc(e)); }
                }
                // 4) el hojeo: la pagina que se va y la que entra, con el reloj quieto
                if (filas > rpp && m[0] >= 8)
                    foreach (int dt in new[] { 0, 40, 115, 229, 230 })
                    {
                        string nombre = pre + "-hojeo-" + dt;
                        if (!Toca(nombre)) continue;
                        int dir = dt % 2 == 0 ? 1 : -1;
                        Pon(l, "PageStart", 0); Pon(l, "Sel", 0);
                        ConGraphicsEnMapa(l, m[0], m[1]);   // hornea la pagina de la que se sale
                        hojea.Invoke(l, new object[] { dir }); Pon(l, "PageStart", rpp); Pon(l, "Sel", rpp);
                        FieldInfo st = Campo(l, "slideTick"); int sem = 5000 + caso * 11 + dt;
                        Bitmap b = null; Graphics g = null; string estado = null; Action<Graphics> metodo = ConGraphics(l, "RenderContent");
                        string hecho = ConReloj(delegate (int ahora) { st.SetValue(l, ahora - dt); }, delegate
                        {
                            b = Mapa(m[0] + 44, m[1] + 30, dt % 3); g = Graphics.FromImage(b);
                            estado = PonEstado(g, new Random(sem), m[0] + 44, m[1] + 30, dt == 0 ? 0 : dt == 229 ? 1 : 2);
                        }, delegate
                        {
                            string res, queda = "";
                            try { metodo(g); res = "bien"; queda = Queda(g); } catch (Exception e) { res = Exc(e); }
                            g.Dispose();
                            string s = estado + "|" + res + "|" + queda + "\t" + Puntos(b, nombre);
                            b.Dispose(); return s;
                        });
                        Linea(nombre, "dir " + dir + "|" + hecho);
                        olvida.Invoke(l, null);
                    }
                ((IDisposable)l).Dispose();
            }
            // los hornos sueltos (privados y estaticos)
            MethodInfo circ = tl.GetMethod("HorneaCirculo", T), past = tl.GetMethod("HorneaPastilla", T);
            foreach (int lado in new[] { 1, 3, 12, 22, 23, 40 })
                foreach (bool fuerte in new[] { false, true })
                { object b = circ.Invoke(null, new object[] { lado, Acentos[lado % Acentos.Length], fuerte }); Apunta("horno-circulo-" + lado + "-" + fuerte, "", b); ((IDisposable)b).Dispose(); }
            foreach (int[] wh in new[] { new[] { 2, 2 }, new[] { 70, 18 }, new[] { 131, 24 }, new[] { 20, 40 }, new[] { 300, 19 } })
            { object b = past.Invoke(null, new object[] { wh[0], wh[1], Acentos[wh[0] % Acentos.Length] }); Apunta("horno-pastilla-" + wh[0] + "x" + wh[1], "", b); ((IDisposable)b).Dispose(); }
        }
        private static void ConGraphicsEnMapa(object vista, int w, int h)
        {
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb)) using (Graphics g = Graphics.FromImage(b)) ConGraphics(vista, "RenderContent")(g);
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2)
            {
                if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
                else if (a[i] == "solo") solo = a[i + 1];
            }
            Encabezados(new Random(7));
            Capsulas(new Random(11));
            Generos(new Random(13));
            Barras(new Random(17));
            Listas(new Random(19));
            Console.Error.WriteLine("vistas vistas-b: " + n + " casos; " + traducidas + " por la traducida (la firma de XP ya no esta); " + pintas + " por Pinta (el OnPaint de XP)");
            return 0;
        }
    }
}

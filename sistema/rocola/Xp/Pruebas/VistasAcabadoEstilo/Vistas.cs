// ARNES DEL GRUPO «ACABADO-ESTILO» (port nativo, tanda 2 del dibujo, 8-11-2026): lo de AcabadoGpu.cs, EstiloGpu.cs, GuiaRojoGpu.cs y
// BuscadorLigeroGpu.cs que los recorridos no pasan (o pasan con un solo estado), llamado con datos fijos y con varios estados del Graphics.
// Se compila con el programa entero de una carpeta Xp (VistasAcabadoEstilo.csproj, -p:Fuente=...): la referencia congelada (GDI+ de la
// capa), la base de la tanda y esta copia; la salida (una linea por caso: los puntos tal cual y como los sube la tarjeta, lo que devuelve,
// las cintas que deja) tiene que salir igual linea por linea (compara.sh).
// Las funciones se llaman como las llama el codigo de XP que todavia no esta traducido: con un Graphics (en la referencia, la funcion de XP;
// en la traduccion, la firma de XP que deja el puente) o, si la traducida ya no tiene esa firma, con el lienzo y el estado de ese Graphics
// (PuenteDibujo, por reflexion: la referencia no lo tiene), como se lo pasaria quien la llama.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet VistasAcabadoEstilo.dll [vuelca CARPETA] [solo PREFIJO]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using SkiaSharp;
using Label = Rocola.Ventanas.Label;

namespace Banco
{
    internal static class VistasAcabado
    {
        private const BindingFlags T = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta, solo;
        private static int n;
        private static readonly Type tPuente = typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo");   // (solo en la traduccion)

        // ---------------------------------------------------------------- lo que sale
        private static bool Toca(string nombre) { return solo == null || nombre.StartsWith(solo, StringComparison.Ordinal); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        // los puntos de un mapa (el Bitmap de la capa o el SKBitmap de lo traducido): tal cual (premultiplicados) y como los sube la tarjeta
        // (LockBits Format32bppArgb, sin premultiplicar)
        private static unsafe string Puntos(object o, string nombre)
        {
            if (o == null) return "null";
            Bitmap b = o as Bitmap;
            if (b == null) b = new Bitmap((SKBitmap)o, PixelFormat.Format32bppArgb);   // (solo para leerlo: no se suelta, los puntos son del SKBitmap)
            SKBitmap s = b.Sk; byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            BitmapData d = b.LockBits(new Rectangle(0, 0, s.Width, s.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] sub = new byte[s.Width * s.Height * 4];
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) sub[y * s.Width * 4 + x] = ((byte*)d.Scan0)[y * d.Stride + x];
            b.UnlockBits(d);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre.Replace('/', '_') + ".png"), png.ToArray());
            string r = s.Width + "x" + s.Height + "\t" + s.ColorType + "/" + s.AlphaType + "\t" + Huella(px) + "\t" + Huella(sub);
            return r;
        }
        private static void Apunta(string nombre, string detalle, object mapa) { Linea(nombre, detalle + "\t" + Puntos(mapa, nombre)); }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static string Huella(string s) { return Huella(Encoding.UTF8.GetBytes(s ?? "\u0000null")); }
        private static string R(float v) { return v.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "/" + BitConverter.SingleToInt32Bits(v).ToString("x8"); }
        private static string Exc(Exception e) { while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException; return "EXC:" + e.GetType().Name; }

        // ---------------------------------------------------------------- los datos
        private static Font[] Fuentes()
        {
            return new[] {
                new Font("Microsoft Sans Serif", 21.75f, FontStyle.Bold),                          // el titulo de lejos (15 * 1,45)
                new Font("Microsoft Sans Serif", 11.5f, FontStyle.Bold),                           // el artista
                new Font("Microsoft Sans Serif", 10.5f, FontStyle.Bold),                           // lo que sigue
                new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold, GraphicsUnit.Pixel),    // la tarjeta: A CONTINUACION
                new Font(FontFamily.GenericSansSerif, 20f, FontStyle.Bold, GraphicsUnit.Pixel),    // y lo que sigue en ella
                new Font("Tahoma", 9.5f),                                                          // (si no esta, Microsoft Sans Serif)
                new Font("Times New Roman", 15f, FontStyle.Italic | FontStyle.Underline),
                new Font("Trebuchet MS", 17.25f, FontStyle.Bold),
            };
        }
        private static readonly string[] Textos = {
            "Nada reproduciendose", "02 Ángel de amor (versión en vivo) — Los Ñandúes & Cía", "A CONTINUACIÓN",
            "SIGUE  ·  Artista con nombre largo - Titulo muy largo que no cabe en la caja para nada", "", "Ñ",
            "😀 emoji 🎵 y mas 😀😀😀😀😀😀😀😀😀😀😀😀", "  espacios al final   ", "Tab\tdentro y\nsalto", null };
        private static readonly Color[] Colores = { Color.FromArgb(125, 235, 165), Color.FromArgb(214, 224, 246), Color.FromArgb(128, 255, 255, 255), Color.FromArgb(169, 191, 224), Color.FromArgb(0, 255, 255, 255) };
        private static readonly TextRenderingHint[] Ajustes = { TextRenderingHint.AntiAliasGridFit, TextRenderingHint.SystemDefault, TextRenderingHint.AntiAlias, TextRenderingHint.SingleBitPerPixelGridFit, TextRenderingHint.ClearTypeGridFit, TextRenderingHint.SingleBitPerPixel };

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
        // EL ESTADO DEL GRAPHICS: la mitad de las veces el de los recorridos (AntiAlias, AntiAliasGridFit, SourceOver, Bilinear, Default,
        // trasladado a coordenadas del form); si no, al azar: suavizado, modo de letra, copia, medio pixel, traslacion entera o con decimales,
        // escala, giro y recortes (SetClip que reemplaza, que se suma, o de un trazo)
        private static string PonEstado(Graphics g, Random r, int w, int h, bool texto)
        {
            bool tipico = r.Next(2) == 0;
            SmoothingMode s = tipico ? SmoothingMode.AntiAlias : new[] { SmoothingMode.AntiAlias, SmoothingMode.None, SmoothingMode.HighQuality, SmoothingMode.Default }[r.Next(4)];
            TextRenderingHint t = tipico ? TextRenderingHint.AntiAliasGridFit : Ajustes[r.Next(Ajustes.Length)];
            CompositingMode c = tipico || r.Next(3) > 0 ? CompositingMode.SourceOver : CompositingMode.SourceCopy;
            PixelOffsetMode p = tipico || r.Next(3) > 0 ? PixelOffsetMode.Default : new[] { PixelOffsetMode.Half, PixelOffsetMode.HighQuality, PixelOffsetMode.None }[r.Next(3)];
            InterpolationMode im = tipico || r.Next(2) > 0 ? InterpolationMode.Bilinear : new[] { InterpolationMode.NearestNeighbor, InterpolationMode.HighQualityBicubic, InterpolationMode.HighQualityBilinear }[r.Next(3)];
            g.SmoothingMode = s; g.TextRenderingHint = t; g.CompositingMode = c; g.PixelOffsetMode = p; g.InterpolationMode = im;
            int tr = tipico ? 1 : r.Next(5), cl = r.Next(4);
            float dx = 0, dy = 0;
            switch (tr)
            {
                case 1: dx = r.Next(-3, 4); dy = r.Next(-3, 4); g.TranslateTransform(dx, dy); break;
                case 2: dx = r.Next(-30, 30) / 8f; dy = r.Next(-30, 30) / 4f; g.TranslateTransform(dx, dy); break;
                case 3: g.ScaleTransform(1.3f, 0.9f); break;
                case 4: g.RotateTransform(r.Next(-12, 12)); break;
            }
            string recorte = "-";
            if (cl == 1) { Rectangle k = new Rectangle(r.Next(w / 3), r.Next(h / 3), w / 2 + r.Next(w / 2), h / 2 + r.Next(h / 2)); g.SetClip(k); recorte = "R" + k; }
            else if (cl == 2) { g.SetClip(new Rectangle(2, 2, w - 4, h - 4)); Rectangle k = new Rectangle(r.Next(w / 2), 0, w, h - r.Next(h / 2)); g.SetClip(k, CombineMode.Intersect); recorte = "I" + k; }
            else if (cl == 3) { using (GraphicsPath gp = new GraphicsPath()) { gp.AddEllipse(-w / 4f, -h / 4f, w * 1.2f, h * 1.3f); g.SetClip(gp); } recorte = "P"; }
            return s + "," + t + "," + c + "," + p + "," + im + ",tr" + tr + "(" + dx + "," + dy + ")," + recorte + (texto ? "" : "");
        }

        // ---------------------------------------------------------------- llamar como lo llama el codigo sin traducir
        private static object Estado(Graphics g) { return tPuente.GetMethod("Estado", T).Invoke(null, new object[] { g }); }
        private static object Lienzo(Graphics g) { return tPuente.GetMethod("Lienzo", T).Invoke(null, new object[] { g }); }
        private static object LetraDe(Font f) { return tPuente.GetMethod("Letra", T, null, new[] { typeof(Font) }, null).Invoke(null, new object[] { f }); }
        private static object Convierte(object v, Type t)
        {
            if (v == null || t.IsInstanceOfType(v)) return v;
            if (t.IsEnum) return Enum.ToObject(t, Convert.ToInt32(v));
            if (v is Font) return LetraDe((Font)v);
            return v;
        }
        // 'nombre' de 't': si tiene la firma de XP (Graphics y 'resto'), con 'g'; si no, la traducida con el lienzo de 'g' y su estado (los
        // parametros del estado van por su nombre: suave, ajuste, copia, filtro, medio; si hay varias traducidas con ese nombre, como
        // DrawLabelOverlay, la que despues del estado lleva tantos como 'resto')
        private static bool DeEstado(string nn) { return nn == "suave" || nn == "ajuste" || nn == "copia" || nn == "filtro" || nn == "medio"; }
        private static object Llama(Type t, string nombre, object inst, Graphics g, params object[] resto)
        {
            MethodInfo con = null, sin = null;
            foreach (MethodInfo m in t.GetMethods(T))
            {
                if (m.Name != nombre) continue;
                ParameterInfo[] p = m.GetParameters();
                if (p.Length == resto.Length + 1 && p[0].ParameterType == typeof(Graphics)) con = m;
                else if (p.Length > 0 && p[0].ParameterType == typeof(SKCanvas))
                {
                    int j = 1; while (j < p.Length && DeEstado(p[j].Name)) j++;
                    if (p.Length - j == resto.Length) sin = m;
                }
            }
            if (con != null) { object[] a = new object[resto.Length + 1]; a[0] = g; Array.Copy(resto, 0, a, 1, resto.Length); return con.Invoke(inst, a); }
            ParameterInfo[] ps = sin.GetParameters(); object e = Estado(g); Type te = e.GetType();
            object[] b = new object[ps.Length]; b[0] = Lienzo(g); int k = 1, i = 0;
            for (; k < ps.Length; k++)
            {
                string nn = ps[k].Name;
                if (nn == "suave") b[k] = te.GetField("Suave").GetValue(e);
                else if (nn == "ajuste") b[k] = te.GetField("Letra").GetValue(e);
                else if (nn == "copia") b[k] = te.GetField("Copia").GetValue(e);
                else if (nn == "filtro") b[k] = te.GetField("Filtro").GetValue(e);
                else if (nn == "medio") b[k] = te.GetField("Medio").GetValue(e);
                else break;
            }
            for (; k < ps.Length; k++) b[k] = Convierte(resto[i++], ps[k].ParameterType);
            return sin.Invoke(inst, b);
        }
        // las que solo miden (RecortaLejos, EscalaTituloLejos): de XP (Graphics, texto, Font, ...) o traducidas (AjusteLetra, texto, Letra, ...)
        private static object Mide(string nombre, Graphics g, string texto, Font f, params object[] resto)
        {
            MethodInfo m = typeof(Form1).GetMethod(nombre, T);
            ParameterInfo[] p = m.GetParameters(); object[] a = new object[p.Length];
            if (p[0].ParameterType == typeof(Graphics)) { a[0] = g; a[2] = f; }
            else { a[0] = Enum.ToObject(p[0].ParameterType, (int)g.TextRenderingHint); a[2] = f != null ? LetraDe(f) : null; }
            a[1] = texto; for (int i = 0; i < resto.Length; i++) a[3 + i] = resto[i];
            return m.Invoke(null, a);
        }
        private static MethodInfo Metodo(Type t, string nombre, params Type[] tipos) { return t.GetMethod(nombre, T, null, tipos, null); }
        private static void Pon(object o, string campo, object v)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(campo, T | BindingFlags.DeclaredOnly); if (fi != null) { fi.SetValue(o, v); return; } }
            throw new MissingFieldException(o.GetType().Name, campo);
        }
        private static object Lee(object o, string campo)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(campo, T | BindingFlags.DeclaredOnly); if (fi != null) return fi.GetValue(o); }
            throw new MissingFieldException(o.GetType().Name, campo);
        }
        private static object Anidada(string nombre) { return Activator.CreateInstance(typeof(Form1).GetNestedType(nombre, BindingFlags.NonPublic | BindingFlags.Public), true); }
        // las cintas de texto (TextoMovilGpu.cs): con 'grupo' puesto, el texto que no cabe se va a una cinta
        private static readonly FieldInfo fCintas = typeof(Form1).GetField("capturandoCintas", T);
        private static string Cintas(CintasTexto grupo, string nombre)
        {
            if (grupo == null) return "";
            StringBuilder sb = new StringBuilder("\tcintas=" + grupo.Items.Count);
            int k = 0;
            foreach (CintaTexto c in grupo.Items)
            {
                sb.Append("\t[" + Huella(c.Key) + "|" + Huella(c.Identity) + "|" + c.Width + "|" + R(c.Rect.X) + "," + R(c.Rect.Y) + "," + R(c.Rect.Width) + "," + R(c.Rect.Height));
                for (int i = 0; i < c.Tiles.Length; i++) sb.Append("|" + Puntos(c.Tiles[i], nombre + "-cinta" + k + "-" + i).Replace('\t', ' '));
                sb.Append(']'); k++;
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- EstiloGpu.cs
        private static void Renglones(Random r)
        {
            Font[] fs = Fuentes();
            for (int caso = 0; caso < 700; caso++)
            {
                Font f = fs[r.Next(fs.Length)]; string texto = Textos[r.Next(Textos.Length)];
                RectangleF[] cajas = { new RectangleF(5, 6, 180, 30), new RectangleF(10.5f, 4.25f, 90, 22), new RectangleF(0, 0, 40, 12), new RectangleF(3, 20, 300, 60),
                    new RectangleF(2, 2, 0, 20), new RectangleF(5, 5, -10, 20), new RectangleF(1, 1, 318, 118), new RectangleF(20, 30, 230.6f, 40.4f), new RectangleF(7, 40, 120, 8) };
                RectangleF caja = cajas[r.Next(cajas.Length)];
                StringAlignment al = (StringAlignment)r.Next(3); Color c = Colores[r.Next(Colores.Length)]; int fondo = r.Next(3);
                bool cinta = r.Next(4) == 0;
                string nombre = "renglon-" + caso;
                Bitmap b = Mapa(320, 120, fondo); string res, estado;
                CintasTexto grupo = cinta ? new CintasTexto() : null;
                using (Graphics g = Graphics.FromImage(b))
                {
                    estado = PonEstado(g, r, 320, 120, true);
                    if (grupo != null) { grupo.Begin(); fCintas.SetValue(null, grupo); }
                    try { Metodo(typeof(Form1), "PintaRenglonLejos", typeof(Graphics), typeof(string), typeof(Font), typeof(Color), typeof(RectangleF), typeof(StringAlignment)).Invoke(null, new object[] { g, texto, f, c, caja, al }); res = "ok"; }
                    catch (Exception e) { res = Exc(e); }
                    finally { fCintas.SetValue(null, null); }
                }
                if (Toca(nombre)) Apunta(nombre, estado + "|" + f.Name + "," + f.Size + "," + f.Style + "," + f.Unit + "|" + Huella(texto) + "|" + caja + "|" + al + "|" + c.ToArgb().ToString("x8") + "|" + fondo + "|" + res, b);
                if (grupo != null && Toca(nombre)) Linea(nombre + "-cintas", Cintas(grupo, nombre));
                if (grupo != null) grupo.Dispose();
                b.Dispose();
            }
            foreach (Font f in fs) f.Dispose();
        }
        private static void Recortes(Random r)
        {
            Font[] fs = Fuentes(); float[] anchos = { 0f, 5f, 37.5f, 80f, 150f, 400f };
            using (Bitmap b = new Bitmap(8, 8, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
                foreach (TextRenderingHint t in Ajustes)
                {
                    g.TextRenderingHint = t;
                    for (int i = 0; i < fs.Length; i++)
                        for (int j = 0; j < Textos.Length; j++)
                            foreach (float ancho in anchos)
                            {
                                string nombre = "recorta-" + t + "-" + i + "-" + j + "-" + ancho;
                                if (!Toca(nombre)) continue;
                                string res;
                                try { string s = (string)Mide("RecortaLejos", g, Textos[j], fs[i], ancho); res = s == null ? "null" : s.Length + ":" + Huella(s); }
                                catch (Exception e) { res = Exc(e); }
                                Linea(nombre, res);
                            }
                }
            foreach (Font f in fs) f.Dispose();
        }
        private static void Escalas(Random r)
        {
            Font[] fs = Fuentes(); float[] anchos = { 50f, 200f, 450f }, altos = { 10f, 25f, 40f, 60f };
            using (Bitmap b = new Bitmap(8, 8, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
                for (int caso = 0; caso < 1500; caso++)
                {
                    TextRenderingHint t = Ajustes[r.Next(Ajustes.Length)]; g.TextRenderingHint = t;
                    int i = r.Next(fs.Length), j = r.Next(Textos.Length); float ancho = anchos[r.Next(anchos.Length)], alto = altos[r.Next(altos.Length)];
                    bool sinLetra = r.Next(40) == 0;
                    string nombre = "escala-" + caso;
                    if (!Toca(nombre)) continue;
                    string res;
                    try { res = R((float)Mide("EscalaTituloLejos", g, Textos[j], sinLetra ? null : fs[i], ancho, alto)); }
                    catch (Exception e) { res = Exc(e); }
                    Linea(nombre, t + "|" + i + "|" + j + "|" + ancho + "|" + alto + "|" + sinLetra + "|" + res);
                }
            foreach (Font f in fs) f.Dispose();
        }
        private static void Tarjetas(Random r)
        {
            Rectangle[] cajas = { new Rectangle(10, 10, 180, 28), new Rectangle(5, 5, 300, 100), new Rectangle(0, 0, 400, 60), new Rectangle(3, 4, 200, 120),
                new Rectangle(1, 1, 179, 27), new Rectangle(10, 10, 50, 10), new Rectangle(20, 12, 260, 45), new Rectangle(8, 30, 330, 70) };
            for (int caso = 0; caso < 240; caso++)
            {
                Rectangle caja = cajas[r.Next(cajas.Length)]; string texto = Textos[r.Next(Textos.Length)] ?? "x"; int fondo = r.Next(3);
                bool cinta = r.Next(4) == 0;
                string nombre = "tarjeta-" + caso;
                Bitmap b = Mapa(420, 140, fondo); string res, estado;
                CintasTexto grupo = cinta ? new CintasTexto() : null;
                using (Graphics g = Graphics.FromImage(b))
                {
                    estado = PonEstado(g, r, 420, 140, true);
                    if (grupo != null) { grupo.Begin(); fCintas.SetValue(null, grupo); }
                    try { Llama(typeof(Form1), "PintaTarjetaSiguiente", null, g, texto, caja); res = "ok"; }
                    catch (Exception e) { res = Exc(e); }
                    finally { fCintas.SetValue(null, null); }
                }
                if (Toca(nombre)) Apunta(nombre, estado + "|" + Huella(texto) + "|" + caja + "|" + fondo + "|" + res, b);
                if (grupo != null && Toca(nombre)) Linea(nombre + "-cintas", Cintas(grupo, nombre));
                if (grupo != null) grupo.Dispose();
                b.Dispose();
            }
        }

        // ---------------------------------------------------------------- GuiaRojoGpu.cs
        private static void Guias(Random r)
        {
            Rectangle[] barras = { new Rectangle(30, 100, 260, 30), new Rectangle(40, 60, 200, 1), new Rectangle(1, 110, 1, 40), new Rectangle(24, 120, 280, 24), new Rectangle(60, 80, 140, 50) };
            Rectangle[] casillas = { Rectangle.Empty, new Rectangle(5, 5, 70, 50), new Rectangle(240, 5, 70, 50), new Rectangle(100, 10, 2, 2), new Rectangle(12, 20, 90, 60) };
            Rectangle[] teclas = { Rectangle.Empty, new Rectangle(20, 20, 24, 24), new Rectangle(8, 8, 3, 3), new Rectangle(250, 12, 30, 30), new Rectangle(30, 30, 4, 4), new Rectangle(14, 26, 17, 17) };
            for (int caso = 0; caso < 300; caso++)
            {
                int cual = r.Next(6);   // 0-3: PintaGuiaRojoAyuda; 4: PintaGuiaRojo; 5: ResaltaTecla
                Rectangle barra = barras[r.Next(barras.Length)], busca = casillas[r.Next(casillas.Length)], ayuda = casillas[r.Next(casillas.Length)];
                Rectangle tb = teclas[r.Next(teclas.Length)], ta = teclas[r.Next(teclas.Length)]; bool arriba = r.Next(2) == 0; int fondo = r.Next(3);
                string nombre = "guia-" + caso;
                Bitmap b = Mapa(320, 160, fondo); string res, estado;
                using (Graphics g = Graphics.FromImage(b))
                {
                    estado = PonEstado(g, r, 320, 160, false);
                    try
                    {
                        if (cual <= 3) Llama(typeof(Form1), "PintaGuiaRojoAyuda", null, g, barra, busca, ayuda, tb, ta);
                        else if (cual == 4) Llama(typeof(Form1), "PintaGuiaRojo", null, g, barra, busca);
                        else Llama(typeof(Form1), "ResaltaTecla", null, g, tb.IsEmpty ? new Rectangle(10, 10, 20, 20) : tb, arriba);
                        res = "ok";
                    }
                    catch (Exception e) { res = Exc(e); }
                }
                if (Toca(nombre)) Apunta(nombre, estado + "|" + cual + "|" + barra + busca + ayuda + tb + ta + "|" + arriba + "|" + fondo + "|" + res, b);
                b.Dispose();
            }
        }

        // ---------------------------------------------------------------- AcabadoGpu.cs: el vidrio, la barra de paginas y la lista
        private static void Vidrios()
        {
            int[][] medidas = { new[] { 556, 464 }, new[] { 556, 468 }, new[] { 250, 464 }, new[] { 100, 30 }, new[] { 7, 9 }, new[] { 4, 4 }, new[] { 3, 3 }, new[] { 1, 1 } };
            bool antes = Form1.InterfazTramada;
            foreach (bool tramada in new[] { true, false })
            {
                Form1.InterfazTramada = tramada;
                foreach (int[] m in medidas)
                    foreach (bool neutral in new[] { false, true })
                    {
                        string nombre = "vidrio-" + (tramada ? "tramado" : "liso") + "-" + m[0] + "x" + m[1] + (neutral ? "-neutral" : "");
                        if (!Toca(nombre)) continue;
                        object mapa = null; string res = "ok";
                        try { mapa = typeof(Form1).GetMethod("BakeSongGlassTint", T).Invoke(null, new object[] { m[0], m[1], neutral }); }
                        catch (Exception e) { res = Exc(e); }
                        Apunta(nombre, res, mapa);
                        if (mapa is IDisposable) ((IDisposable)mapa).Dispose();
                    }
                foreach (int[] m in new[] { new[] { 556, 464 }, new[] { 30, 20 } })
                {
                    string nombre = "vidrio-solo-" + (tramada ? "tramado" : "liso") + "-" + m[0] + "x" + m[1];
                    if (!Toca(nombre)) continue;
                    object mapa = typeof(Form1).GetMethod("BakeSongGlass", T).Invoke(null, new object[] { m[0], m[1] });
                    Apunta(nombre, "ok", mapa);
                    ((IDisposable)mapa).Dispose();
                }
            }
            Form1.InterfazTramada = antes;
        }
        // un Form1 sin construir (sin ventana, sin sonido, sin biblioteca): solo los campos que pide cada funcion
        private static object Form1Vacio(int w, int h)
        {
            object f = RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            Pon(f, "bounds", new Rectangle(0, 0, w, h));   // ClientSize (sin borde: FormBorderStyle.None)
            return f;
        }
        private static void Paginas(Random r)
        {
            object f = Form1Vacio(1024, 768);
            Rectangle[] cajas = { new Rectangle(10, 6, 556, 34), new Rectangle(0, 0, 800, 46), new Rectangle(20, 8, 300, 30), new Rectangle(5, 4, 120, 20),
                new Rectangle(4, 2, 50, 30), new Rectangle(12, 10, 556, 20), new Rectangle(30, 0, 640, 52), new Rectangle(2, 3, 450, 24) };
            Color[] acentos = { Color.FromArgb(110, 120, 160), Color.FromArgb(255, 60, 40), Color.FromArgb(20, 200, 120), Color.FromArgb(0, 0, 0) };
            for (int caso = 0; caso < 160; caso++)
            {
                object pager = Anidada("SongPagerBar");
                Rectangle caja = cajas[r.Next(cajas.Length)];
                int paginas = new[] { 1, 2, 5, 12 }[r.Next(4)], pagina = r.Next(paginas);
                ((Control)pager).Bounds = caja;
                Pon(pager, "Pages", paginas); pager.GetType().GetProperty("Page", T).SetValue(pager, pagina);
                Color acento = acentos[r.Next(acentos.Length)];
                Pon(f, "songPager", pager); Pon(f, "bgAccentTarget", acento);
                int fondo = r.Next(3);
                string nombre = "paginas-" + caso;
                Bitmap b = Mapa(900, 70, fondo); string res, estado;
                using (Graphics g = Graphics.FromImage(b))
                {
                    estado = PonEstado(g, r, 900, 70, true);
                    try { Llama(typeof(Form1), "DrawSongPagerGpu", f, g); res = "ok"; }   // (la firma de XP se quito con el grupo video-form1: en la traduccion, la traducida con el estado de g)
                    catch (Exception e) { res = Exc(e); }
                }
                if (Toca(nombre)) Apunta(nombre, estado + "|" + caja + "|" + paginas + "|" + pagina + "|" + acento.ToArgb().ToString("x8") + "|" + fondo + "|" + res, b);
                b.Dispose(); ((Control)pager).Dispose();
            }
        }
        // LA LISTA DEL DISCO (SongListView): su pagina horneada la pinta PintaPagina (Form1.Vistas.cs, igual en las tres); aqui, las capas de
        // la tarjeta, el vidrio, el dibujo en la imagen de la interfaz y el hojeo (con el reloj quieto: ver ConReloj)
        private static object NuevaLista(int w, int h, int filas, int rowH, Font letra, int sel, int inicio, Color acento)
        {
            object l = Anidada("SongListView"); Control c = (Control)l;
            c.Bounds = new Rectangle(0, 0, w, h); c.Font = letra;
            Pon(l, "RowH", rowH); Pon(l, "Sel", sel); Pon(l, "PageStart", inicio); Pon(l, "Accent", acento);
            Pon(l, "GetCount", (Func<int>)(() => filas));
            Pon(l, "GetText", (Func<int, string>)(i => (i + 1).ToString("00") + " " + (i % 3 == 0 ? "Ángel de amor (versión en vivo) — Los Ñandúes & Cía, una larga" : i % 3 == 1 ? "Cancion " + i : "Otra & mas")));
            Pon(l, "GetRight", (Func<int, string>)(i => i % 4 == 3 ? "" : (2 + i % 5) + ":" + (10 + i * 7 % 50).ToString("00")));
            Pon(l, "GetQueuePos", (Func<int, int>)(i => i == 3 ? 1 : i == 6 ? 12 : 0));
            Pon(l, "GetSonando", (Func<int, bool>)(i => i == 5));
            Pon(l, "GetGenero", (Func<int, string>)(i => i % 2 == 0 ? "Rock" : ""));
            return l;
        }
        // DesplazaAhora (Form1.Vistas.cs) lee Environment.TickCount al empezar a pintar: el hojeo se pinta a 'dt' ms de su comienzo. Primero una
        // vez para calentar (lo que se hornea la primera vez -la pagina que entra, el vidrio- no cae en el instante medido); despues tres veces,
        // cada una al empezar un milisegundo (lo que va antes de DesplazaAhora tarda microsegundos), y vale la imagen que salga al menos dos.
        private static Bitmap ConReloj(object lista, int dt, Func<Bitmap> hacer)
        {
            FieldInfo st = lista.GetType().GetField("slideTick", T);
            st.SetValue(lista, Environment.TickCount - dt); hacer().Dispose();
            List<Bitmap> hechos = new List<Bitmap>(); List<string> huellas = new List<string>();
            for (int k = 0; k < 3; k++)
            {
                int t0 = Environment.TickCount; while (Environment.TickCount == t0) { }
                st.SetValue(lista, Environment.TickCount - dt);
                Bitmap b = hacer(); hechos.Add(b); huellas.Add(HuellaRapida(b));
            }
            int elegido = huellas[0] == huellas[1] || huellas[0] == huellas[2] ? 0 : huellas[1] == huellas[2] ? 1 : -1;
            for (int k = 0; k < 3; k++) if (k != elegido) hechos[k].Dispose();
            if (elegido < 0) throw new TimeoutException("sin dos iguales");
            return hechos[elegido];
        }
        private static unsafe string HuellaRapida(Bitmap b)
        {
            SKBitmap s = b.Sk; byte[] px = new byte[s.Width * s.Height * 4]; byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            return Huella(px);
        }
        private static void Listas(Random r)
        {
            Type tl = typeof(Form1).GetNestedType("SongListView", BindingFlags.NonPublic);
            MethodInfo capa = tl.GetMethod("BakeGpuListLayer", T), previa = tl.GetMethod("BakeGpuPrevious", T);
            // RenderTransparentGpuTint y RenderOpaqueGpu: la firma de XP se quito con el grupo video-form1; en la traduccion, la traducida con el estado de g (Llama)
            MethodInfo hojea = tl.GetMethod("StartGpuSlide", T);
            int[][] medidas = { new[] { 556, 464, 34 }, new[] { 250, 464, 30 }, new[] { 556, 468, 36 }, new[] { 120, 90, 30 } };
            for (int caso = 0; caso < 24; caso++)
            {
                int[] m = medidas[caso % medidas.Length];
                Font letra = new Font("Microsoft Sans Serif", caso % 3 == 0 ? 14.25f : caso % 3 == 1 ? 12f : 17f, FontStyle.Bold);
                int filas = new[] { 20, 5, 40 }[caso % 3], rpp = Math.Max(1, m[1] / m[2]);
                int sel = r.Next(Math.Min(filas, rpp)), inicio = 0;
                Color acento = Color.FromArgb(r.Next(256), r.Next(256), r.Next(256));
                object l = NuevaLista(m[0], m[1], filas, m[2], letra, sel, inicio, acento);
                ((Control)l).BackColor = caso % 2 == 0 ? Color.FromArgb(31, 34, 52) : Color.FromArgb(12, 20, 40);
                bool cintas = caso % 4 == 1;
                string pre = "lista-" + caso;
                // 1) la capa de la pagina sin seleccion y la de la fila elegida (dos veces: la segunda restaura la anterior en la instantanea)
                CintasTexto grupo = cintas ? new CintasTexto() : null;
                if (grupo != null) { grupo.Begin(); fCintas.SetValue(null, grupo); }
                try
                {
                    object c0 = Invoca(capa, l, false); Apunta(pre + "-capa", m[0] + "x" + m[1] + "|" + filas + "|" + sel, c0); Suelta(c0);
                    object c1 = Invoca(capa, l, true); Apunta(pre + "-fila", "sel " + sel, c1); Suelta(c1);
                    Apunta(pre + "-horno1", "hornoPag", Lee(l, "hornoPag"));
                    int otra = Math.Min(Math.Min(filas, rpp) - 1, sel + 1); Pon(l, "Sel", otra);
                    object c2 = Invoca(capa, l, true); Apunta(pre + "-fila2", "sel " + otra, c2); Suelta(c2);
                    Apunta(pre + "-horno2", "hornoPag", Lee(l, "hornoPag"));
                    Apunta(pre + "-instantanea", "gpuSnapshotRow", Lee(l, "gpuSnapshotRow"));
                }
                finally { fCintas.SetValue(null, null); }
                if (grupo != null) { Linea(pre + "-cintas", Cintas(grupo, pre)); grupo.Dispose(); }
                // 2) en la imagen de la interfaz, sin hojear: el vidrio (neutro o con tinte) o el velo, y la opaca
                foreach (int k in new[] { 0, 1, 2, 3, 4 })
                {
                    string nombre = pre + "-dibujo" + k;
                    Bitmap b = Mapa(m[0] + 40, m[1] + 30, k % 3); string res, estado;
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        estado = PonEstado(g, r, m[0] + 40, m[1] + 30, false);
                        try
                        {
                            if (k < 4) Llama(tl, "RenderTransparentGpuTint", l, g, k >= 2, k % 2 == 1, Color.FromArgb(r.Next(256), r.Next(256), r.Next(256)));
                            else Llama(tl, "RenderOpaqueGpu", l, g);
                            res = "ok";
                        }
                        catch (Exception e) { res = Exc(e); }
                    }
                    if (Toca(nombre)) Apunta(nombre, estado + "|" + res, b);
                    b.Dispose();
                }
                // 3) hojeando: la pagina que se va y la que entra (de lado o de arriba abajo), la capa de la que se va, y su dibujo
                if (filas > rpp)
                {
                    bool vertical = caso % 2 == 1;
                    Pon(l, "PageStart", rpp); Pon(l, "Sel", rpp);
                    hojea.Invoke(l, new object[] { 1, vertical });
                    foreach (int dt in new[] { 10, 100, 200 })
                    {
                        string nombre = pre + "-hojea" + dt;
                        try
                        {
                            // la capa de la pagina que se va (BakeGpuPrevious: solo mira que se este hojeando, no cuanto)
                            l.GetType().GetField("slideTick", T).SetValue(l, Environment.TickCount - dt);
                            object p = Invoca(previa, l); Apunta(nombre + "-previa", "vertical " + vertical, p); Suelta(p);
                            string est = ""; int dtt = dt;
                            Bitmap b = ConReloj(l, dt, () =>
                            {
                                Bitmap bb = Mapa(m[0] + 40, m[1] + 30, 2);
                                using (Graphics g = Graphics.FromImage(bb))
                                {
                                    est = PonEstado(g, new Random(dtt), m[0] + 40, m[1] + 30, false);
                                    if (dtt == 100) Llama(tl, "RenderOpaqueGpu", l, g);
                                    else Llama(tl, "RenderTransparentGpuTint", l, g, dtt == 200, dtt == 200, Color.FromArgb(200, 180, 90));
                                }
                                return bb;
                            });
                            Apunta(nombre, est, b); b.Dispose();
                        }
                        catch (Exception e) { Linea(nombre, Exc(e)); }
                    }
                }
                ((Control)l).Dispose(); letra.Dispose();
            }
        }
        private static object Invoca(MethodInfo m, object o, params object[] a) { return m.Invoke(o, a); }
        private static void Suelta(object o) { IDisposable d = o as IDisposable; if (d != null) d.Dispose(); }

        // ---------------------------------------------------------------- la franja de lejos entera (FranjaLejos y los de siempre)
        private static Label Etiqueta(Rectangle caja, string texto, float puntos, Color color, ContentAlignment al)
        {
            Label l = new Label(); l.Bounds = caja; l.Text = texto; l.Font = new Font("Microsoft Sans Serif", puntos, FontStyle.Bold); l.ForeColor = color; l.TextAlign = al;
            return l;
        }
        private static void Franjas(Random r)
        {
            // las firmas de XP de FranjaLejos, PintaTituloCompacto, PintaSiguienteCompacto, DrawLabelOverlay, DrawCtrlOverlay y EmpiezaRecuadro se
            // quitaron con el grupo video-form1 (Form1.Video.cs las llama directo): en la traduccion, la traducida con el lienzo y el estado de g
            // (Llama), como hacia el puente; EmpiezaRecuadro, ademas, con el SetClip que el puente le ponia antes al Graphics
            MethodInfo recuadro = Metodo(typeof(Form1), "EmpiezaRecuadro", typeof(Graphics), typeof(Rectangle));
            string[] titulos = { "Nada reproduciendose", "Ángel de amor (versión en vivo extendida para el baile)", "Corta", "Un titulo larguisimo que no cabe ni con la letra mas chica de todas las que hay" };
            string[] artistas = { "", "Los Ñandúes & Cía", "Artista con un nombre muy largo que se come el renglon entero y mas" };
            string[] siguientes = { "Sigue:  -", "Sigue:  Grupo - Otra cancion", "Sigue:  Un artista largo - Un titulo larguisimo que no cabe en la tarjeta lateral", "" };
            for (int caso = 0; caso < 120; caso++)
            {
                int w = caso % 3 == 2 ? 800 : 1024, top = 668;
                object f = Form1Vacio(w, 768);
                Pon(f, "letraLejos", caso % 5 != 4); Pon(f, "gpuEnabled", true); Pon(f, "gpuPanel", RuntimeHelpers.GetUninitializedObject(typeof(GpuPanel)));
                Label nowTop = Etiqueta(new Rectangle(108, 674, 503, 22 + r.Next(3) * 4), titulos[r.Next(titulos.Length)], 15f, Color.FromArgb(125, 235, 165), ContentAlignment.BottomLeft);
                Label now = Etiqueta(new Rectangle(108, 700, 400 + r.Next(3) * 60, 20), artistas[r.Next(artistas.Length)], 11.5f, Color.FromArgb(125, 235, 165), ContentAlignment.MiddleLeft);
                Label next = Etiqueta(new Rectangle(108, 722, 500, 18), siguientes[r.Next(siguientes.Length)], 10.5f, Color.FromArgb(150, 200, 255), ContentAlignment.MiddleLeft);
                Label time = Etiqueta(new Rectangle(w - 260, 742 - r.Next(3) * 8, 120, 22), "1:23 / 4:56", 13f, Color.White, ContentAlignment.MiddleLeft);
                Label mej = Etiqueta(new Rectangle(560, 700, 90, 22), "MEJORADA", 12f, Color.FromArgb(240, 200, 90), ContentAlignment.MiddleLeft); mej.Visible = r.Next(3) == 0;
                object genero = Anidada("GeneroView"); Control gc = (Control)genero; gc.Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold);
                genero.GetType().GetProperty("Texto", T).SetValue(genero, r.Next(2) == 0 ? "Rock en español" : ""); gc.Bounds = new Rectangle(520, 702, 110, 18); gc.Visible = r.Next(2) == 0;
                object cred = Anidada("PastillaView"), cola = Anidada("PastillaView"), prog = Anidada("ProgressView");
                ((Control)cred).Bounds = new Rectangle(w - 380, 676, 170, 34); ((Control)cola).Bounds = new Rectangle(w - (r.Next(2) == 0 ? 200 : 330), 676, 150, 34);
                ((Control)prog).Bounds = new Rectangle(108, 750, w - 400, 8);
                Pon(f, "lblNowTop", nowTop); Pon(f, "lblNow", now); Pon(f, "lblNext", next); Pon(f, "lblTime", time); Pon(f, "lblMejorada", mej);
                Pon(f, "pastGenero", genero); Pon(f, "pastCred", cred); Pon(f, "pastCola", cola); Pon(f, "progress", prog);
                Rectangle zona = r.Next(3) == 0 ? new Rectangle(r.Next(300), r.Next(40), 300 + r.Next(400), 30 + r.Next(60)) : Rectangle.Empty;
                bool cinta = r.Next(3) == 0;
                string nombre = "franja-" + caso;
                Bitmap b = Mapa(w, 100, 0); string res;
                CintasTexto grupo = cinta ? new CintasTexto() : null;
                using (Graphics g = Graphics.FromImage(b))
                {
                    if (grupo != null) { grupo.Begin(); fCintas.SetValue(null, grupo); }
                    try
                    {
                        // como EnsureUiBotOverlay (Form1.Video.cs)
                        if (!zona.IsEmpty)
                        {
                            if (recuadro != null) recuadro.Invoke(null, new object[] { g, zona });
                            else { g.SetClip(zona); Llama(typeof(Form1), "EmpiezaRecuadro", null, g, zona); }
                        }
                        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.TranslateTransform(0, -top);
                        if (!(bool)Llama(typeof(Form1), "FranjaLejos", f, g))
                        {
                            Llama(typeof(Form1), "PintaTituloCompacto", f, g, false); Llama(typeof(Form1), "PintaArtistaCompacto", f, g);
                            if (mej.Visible) Llama(typeof(Form1), "DrawLabelOverlay", null, g, mej);
                            if (gc.Visible) Llama(typeof(Form1), "DrawCtrlOverlay", null, g, gc);
                            Llama(typeof(Form1), "PintaSiguienteCompacto", f, g);
                        }
                        Llama(typeof(Form1), "DrawLabelOverlay", null, g, time);
                        // lo que sigue con lo que deja la franja en el Graphics
                        using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillRectangle(sb, 2, top + 2, 30, 6);
                        res = "ok";
                    }
                    catch (Exception e) { res = Exc(e); }
                    finally { fCintas.SetValue(null, null); }
                }
                if (Toca(nombre)) Apunta(nombre, w + "|" + zona + "|" + Huella(nowTop.Text + "|" + now.Text + "|" + next.Text) + "|" + nowTop.Height + "|" + mej.Visible + gc.Visible + "|" + res, b);
                if (grupo != null && Toca(nombre)) Linea(nombre + "-cintas", Cintas(grupo, nombre));
                if (grupo != null) grupo.Dispose();
                b.Dispose();
            }
        }

        // ---------------------------------------------------------------- BuscadorLigeroGpu.cs no dibuja: su IL, con los nombres resueltos
        private static Dictionary<short, OpCode> ops;
        private static string Il(MethodInfo m)
        {
            if (ops == null)
            {
                ops = new Dictionary<short, OpCode>();
                foreach (FieldInfo fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) { OpCode o = (OpCode)fi.GetValue(null); ops[o.Value] = o; }
            }
            MethodBody cuerpo = m.GetMethodBody(); byte[] il = cuerpo.GetILAsByteArray(); Module mod = m.Module;
            Type[] ga = m.DeclaringType.IsGenericType ? m.DeclaringType.GetGenericArguments() : null, gm = m.IsGenericMethod ? m.GetGenericArguments() : null;
            StringBuilder sb = new StringBuilder(m.ToString() + "|");
            foreach (LocalVariableInfo lv in cuerpo.LocalVariables) sb.Append(lv.LocalType.FullName).Append(',');
            sb.Append('|');
            int i = 0;
            while (i < il.Length)
            {
                short v = il[i] == 0xFE ? (short)(0xFE00 | il[i + 1]) : il[i]; i += il[i] == 0xFE ? 2 : 1;
                OpCode o = ops[v]; sb.Append(o.Name);
                switch (o.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: sb.Append(' ').Append((sbyte)il[i]); i += 1; break;
                    case OperandType.InlineVar: sb.Append(' ').Append(BitConverter.ToInt16(il, i)); i += 2; break;
                    case OperandType.InlineI: case OperandType.InlineBrTarget: sb.Append(' ').Append(BitConverter.ToInt32(il, i)); i += 4; break;
                    case OperandType.ShortInlineR: sb.Append(' ').Append(BitConverter.ToSingle(il, i).ToString("R")); i += 4; break;
                    case OperandType.InlineI8: sb.Append(' ').Append(BitConverter.ToInt64(il, i)); i += 8; break;
                    case OperandType.InlineR: sb.Append(' ').Append(BitConverter.ToDouble(il, i).ToString("R")); i += 8; break;
                    case OperandType.InlineString: sb.Append(" \"").Append(mod.ResolveString(BitConverter.ToInt32(il, i))).Append('"'); i += 4; break;
                    case OperandType.InlineSig: sb.Append(" sig"); i += 4; break;
                    case OperandType.InlineSwitch:
                        { int k = BitConverter.ToInt32(il, i); i += 4; sb.Append(" [").Append(k); for (int j = 0; j < k; j++) { sb.Append(',').Append(BitConverter.ToInt32(il, i)); i += 4; } sb.Append(']'); break; }
                    default:
                        { MemberInfo mi = mod.ResolveMember(BitConverter.ToInt32(il, i), ga, gm); i += 4; sb.Append(' ').Append(mi.DeclaringType != null ? mi.DeclaringType.FullName + "::" : "").Append(mi); break; }
                }
                sb.Append(';');
            }
            return sb.ToString();
        }
        private static void Ils()
        {
            foreach (string nombre in new[] { "SubeUi", "RobaUiParaVentana", "VentanaEnUso" })
            {
                if (!Toca("il-" + nombre)) continue;
                MethodInfo m = typeof(GpuPanel).GetMethod(nombre, T);
                string il = Il(m);
                Linea("il-" + nombre, il.Length + "\t" + Huella(il));
                if (carpeta != null) System.IO.File.WriteAllText(System.IO.Path.Combine(carpeta, "il-" + nombre + ".txt"), il.Replace(";", ";\n"));
            }
            FieldInfo c = typeof(GpuPanel).GetField("GetUiCambio", T);
            if (Toca("il-GetUiCambio")) Linea("il-GetUiCambio", c.FieldType.FullName);
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2)
            {
                if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
                else if (a[i] == "solo") solo = a[i + 1];
            }
            Renglones(new Random(7));
            Recortes(new Random(11));
            Escalas(new Random(13));
            Tarjetas(new Random(17));
            Guias(new Random(19));
            Vidrios();
            Paginas(new Random(23));
            Listas(new Random(29));
            Franjas(new Random(31));
            Ils();
            Console.Error.WriteLine("vistas acabado-estilo: " + n + " casos");
            return 0;
        }
    }
}

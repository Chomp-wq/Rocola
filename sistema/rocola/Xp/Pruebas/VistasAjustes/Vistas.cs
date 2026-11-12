// ARNES DE VENTANAS2, PASO C (port nativo, 10-11-2026): el dibujo con GDI+ de Form1.Ajustes.cs que el paso tradujo a SkiaSharp y que las
// escenas de Ctrl+F3 no pasan (no son de la hoja: los usa la pantalla): las medidas de texto AnchoTexto (con y sin Graphics) y AnchoToast,
// el formato de CreaSfDibuja y el aviso de siempre (PintaToast y PintaToastHasta; con «Avisos que se entienden» de fabrica, el que sale es
// el de SuenaVivoGpu.cs y este no pasa por los recorridos). Se compila con el programa entero de una carpeta Xp (VistasAjustes.csproj,
// -p:Fuente=...): la referencia congelada (GDI+ de la capa) y esta copia; la salida (una linea por caso: lo que devuelve la funcion y los
// puntos del mapa, tal cual y como los sube la tarjeta) tiene que salir igual linea por linea (compara.sh).
// Cada funcion se llama como la llama el codigo de XP que todavia no esta traducido: con Graphics, Font, StringAlignment y Bitmap (en la
// referencia, la funcion de XP; en la traduccion, la firma de XP que deja Plataforma/Puente.ventanas2.cs; CreaSfDibuja ya no la tiene, y se
// hace lo que hacia su puente: la traducida con Alinea y su FormatoTexto como StringFormat, por reflexion). El aviso, que en la traduccion ya
// no tiene esa firma, con el SKBitmap del mismo mapa y la Letra de la misma Font (PuenteDibujo, por reflexion: la referencia no lo tiene).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet VistasAjustes.dll [vuelca CARPETA]
using System;
using System.Drawing;
using System.Reflection;
using System.Text;
using Rocola;
using Rocola.Dibujo2D;
using SkiaSharp;

namespace Banco
{
    internal static class VistasAjustes
    {
        private const BindingFlags T = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta; private static int n;
        private static readonly Type tPuente = typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo");   // (solo en la traduccion)

        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static string Exc(Exception e) { while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException; return "EXC:" + e.GetType().Name; }
        // los puntos de un mapa: tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb, sin premultiplicar)
        private static unsafe string Puntos(Bitmap b, string nombre)
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

        private static Font[] Fuentes()
        {
            return new[] {
                new Font("Microsoft Sans Serif", 14f, FontStyle.Bold),                             // lblToast (Bold(14f), Form1.cs)
                new Font("Microsoft Sans Serif", 11.9f, FontStyle.Bold),                           // la cabecera
                new Font("Microsoft Sans Serif", 8.25f),                                           // la de fabrica de XP
                new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold, GraphicsUnit.Pixel),
                new Font("Tahoma", 9.5f),                                                          // (si no esta, Microsoft Sans Serif)
                new Font("Times New Roman", 15f, FontStyle.Italic | FontStyle.Underline),
                new Font("Trebuchet MS", 21.75f, FontStyle.Bold),
            };
        }
        private static readonly string[] Textos = {
            "Inserta una moneda", "LO MAS SONADO", "LETRA W", "12/12", "pagina 3 de 12", "¡Botones configurados!",
            "No se pudo abrir el video: credito devuelto", "02 Ángel de amor (versión en vivo) — Los Ñandúes & Cía", "", "Ñ", "  espacios al final   ",
            "Tab\tdentro y\nsalto", "Un aviso tan largo que no cabe en la pastilla de seiscientos veinte puntos ni queriendo, y acaba en puntos suspensivos", "00:00", null };
        private static readonly Color[] Fondos = { Color.FromArgb(40, 165, 90), Color.FromArgb(200, 130, 35), Color.FromArgb(190, 60, 60), Color.FromArgb(128, 20, 40, 90), Color.FromArgb(0, 0, 0, 0) };
        private static readonly TextRenderingHint[] Ajustes = { TextRenderingHint.SystemDefault, TextRenderingHint.SingleBitPerPixelGridFit, TextRenderingHint.SingleBitPerPixel,
            TextRenderingHint.AntiAliasGridFit, TextRenderingHint.AntiAlias, TextRenderingHint.ClearTypeGridFit };
        private static MethodInfo Metodo(string nombre, params Type[] tipos) { return typeof(Form1).GetMethod(nombre, T, null, tipos, null); }
        // (rotulos-y-letras) las firmas de XP con la Font de AnchoTexto(string, Font) y AnchoToast(string, Font) estaban en
        // Plataforma/Puente.ventanas2.cs (borrado: sus llamadores ya pasan su Letra): en la traduccion, la traducida con la Letra de esa Font
        // (Font.L, la misma que da PuenteDibujo.Letra; la nula, nula); en la referencia, la de XP
        private static Func<string, Font, object> ConFont(string nombre)
        {
            MethodInfo m = Metodo(nombre, typeof(string), typeof(Font));
            if (m != null) return (t, f) => m.Invoke(null, new object[] { t, f });
            MethodInfo l = null;
            foreach (MethodInfo mi in typeof(Form1).GetMethods(T)) { ParameterInfo[] p = mi.GetParameters(); if (mi.Name == nombre && p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType.Name == "Letra") l = mi; }
            return (t, f) => l.Invoke(null, new object[] { t, f == null ? null : typeof(Font).GetField("L", T).GetValue(f) });
        }

        // AnchoTexto(Graphics, string, Font) de XP: en la referencia, la de XP por reflexion; en la traduccion ya no existe (la quito el grupo
        // limpieza-puentes el 12-11-2026: nadie la llamaba) y se hace lo que hacia el puente: sin Graphics, sin letra o sin texto, 0; del Graphics,
        // su modo de letra (TextRenderingHint), y la Letra de la Font, a la traducida AnchoTexto(AjusteLetra, string, Letra)
        private static object AnchoConGraphics(MethodInfo con, Graphics g, string t, Font f)
        {
            if (con != null) return con.Invoke(null, new object[] { g, t, f });
            if (g == null || f == null || string.IsNullOrEmpty(t)) return 0;
            Type puente = typeof(Form1).Assembly.GetType("Rocola.PuenteDibujo");
            foreach (MethodInfo m in typeof(Form1).GetMethods(T))
            {
                ParameterInfo[] p = m.GetParameters();
                if (m.Name != "AnchoTexto" || p.Length != 3 || p[0].ParameterType.Name != "AjusteLetra") continue;
                object letra = puente.GetMethod("Letra", T, null, new[] { typeof(Font) }, null).Invoke(null, new object[] { f });
                return m.Invoke(null, new object[] { Enum.ToObject(p[0].ParameterType, (int)g.TextRenderingHint), t, letra });
            }
            throw new InvalidOperationException("falta AnchoTexto(AjusteLetra, string, Letra)");
        }

        // AnchoTexto(Graphics, string, Font): con cada modo de letra, con suavizado, traslacion, escala y recorte (no deben cambiar la medida), y
        // sin Graphics, sin letra y sin texto (0); AnchoTexto(string, Font)
        private static void Anchos()
        {
            Font[] fs = Fuentes(); MethodInfo con = Metodo("AnchoTexto", typeof(Graphics), typeof(string), typeof(Font)), sin = ConFont("AnchoTexto");
            using (Bitmap b = new Bitmap(64, 32, PixelFormat.Format32bppPArgb))
                for (int fi = 0; fi < fs.Length; fi++) for (int ti = 0; ti < Textos.Length; ti++)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int k = 0; k < Ajustes.Length * 2; k++)
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            g.TextRenderingHint = Ajustes[k % Ajustes.Length];
                            if (k >= Ajustes.Length) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TranslateTransform(3.5f, 2f); g.ScaleTransform(1.3f, 0.8f); g.SetClip(new Rectangle(1, 1, 10, 10)); g.PixelOffsetMode = PixelOffsetMode.Half; }
                            try { sb.Append(AnchoConGraphics(con, g, Textos[ti], fs[fi])).Append(' '); } catch (Exception e) { sb.Append(Exc(e)).Append(' '); }
                        }
                    string s2; try { s2 = sin(Textos[ti], fs[fi]).ToString(); } catch (Exception e) { s2 = Exc(e); }
                    Linea("ancho-" + fi + "-" + ti, sb.ToString().TrimEnd() + "\tsin=" + s2);
                }
            using (Bitmap b = new Bitmap(4, 4)) using (Graphics g = Graphics.FromImage(b))
                Linea("ancho-nulos", AnchoConGraphics(con, null, "hola", fs[0]) + " " + AnchoConGraphics(con, g, "hola", null) + " " + AnchoConGraphics(con, g, "", fs[0])
                    + " " + sin("hola", null) + " " + sin(null, fs[0]));
        }

        // CreaSfDibuja(StringAlignment): lo que trae y un texto pintado con el (centrado en vertical, sin partir, sin recortar, y con una caja
        // mas baja que la letra); antes y DESPUES de que alguien le cambie cosas al tipografico compartido (como EstadoReposoGpu.cs)
        private static void Formatos()
        {
            Font[] fs = Fuentes(); MethodInfo m = Metodo("CreaSfDibuja", typeof(StringAlignment));
            // en la traduccion ya no queda la firma de XP (Form1.Vistas.cs, el ultimo que la pedia, llama a la de Alinea: grupo directas-vistas):
            // se hace lo que hacia su puente, CreaSfDibuja((Alinea)(int)al) y su FormatoTexto como StringFormat (PuenteDibujo.StringFormat)
            MethodInfo mTr = null, aSf = null;
            if (m == null)
            {
                foreach (MethodInfo x in typeof(Form1).GetMethods(T)) if (x.Name == "CreaSfDibuja" && x.GetParameters().Length == 1) mTr = x;
                aSf = tPuente.GetMethod("StringFormat", T, null, new[] { mTr.ReturnType }, null);
            }
            for (int vuelta = 0; vuelta < 2; vuelta++)
            {
                if (vuelta == 1) { StringFormat t = StringFormat.GenericTypographic; t.Alignment = StringAlignment.Center; t.LineAlignment = StringAlignment.Center; t.FormatFlags |= StringFormatFlags.NoWrap; }
                foreach (StringAlignment al in new[] { StringAlignment.Near, StringAlignment.Center, StringAlignment.Far })
                {
                    StringFormat sf = m != null ? (StringFormat)m.Invoke(null, new object[] { al })
                        : (StringFormat)aSf.Invoke(null, new object[] { mTr.Invoke(null, new object[] { Enum.ToObject(mTr.GetParameters()[0].ParameterType, (int)al) }) });
                    Linea("formato-" + vuelta + "-" + al, sf.Alignment + "," + sf.LineAlignment + "," + (int)sf.FormatFlags + "," + sf.Trimming + ",otro=" + !ReferenceEquals(sf, StringFormat.GenericTypographic));
                    for (int fi = 0; fi < fs.Length; fi += 2) for (int caja = 0; caja < 3; caja++)
                        using (Bitmap b = new Bitmap(220, 40, PixelFormat.Format32bppPArgb))
                        {
                            using (Graphics g = Graphics.FromImage(b))
                            using (SolidBrush br = new SolidBrush(Color.FromArgb(230, 240, 250, 255)))
                            {
                                g.Clear(Color.FromArgb(255, 12, 16, 30)); g.TextRenderingHint = Ajustes[(fi + caja) % Ajustes.Length];
                                RectangleF r = caja == 0 ? new RectangleF(4, 4, 212, 32) : caja == 1 ? new RectangleF(10, 14, 60, 6) : new RectangleF(30.5f, 2.25f, 150, 36);
                                g.DrawString("LO MAS SONADO  12/12", fs[fi], br, r, sf);
                            }
                            Linea("formato-" + vuelta + "-" + al + "-" + fi + "-" + caja, Puntos(b, "formato-" + vuelta + "-" + al + "-" + fi + "-" + caja));
                        }
                }
            }
        }

        // el aviso de siempre: AnchoToast y PintaToast / PintaToastHasta sobre un mapa de 620x48 que ya traia algo (el mapa se reusa)
        private static void Avisos()
        {
            Font[] fs = Fuentes(); Func<string, Font, object> ancho = ConFont("AnchoToast");
            MethodInfo pinta = null, hasta = null;
            foreach (MethodInfo m in typeof(Form1).GetMethods(T)) { if (m.Name == "PintaToast") pinta = m; else if (m.Name == "PintaToastHasta") hasta = m; }
            bool deXp = pinta.GetParameters()[0].ParameterType == typeof(Bitmap);
            int[] maximos = { 620, 596, 400, 260, 100, -24 };
            for (int fi = 0; fi < fs.Length; fi++) for (int ti = 0; ti < Textos.Length; ti++)
            {
                string a; try { a = ancho(Textos[ti], fs[fi]).ToString(); } catch (Exception e) { a = Exc(e); }
                Linea("aviso-ancho-" + fi + "-" + ti, a);
                if (fi > 2 && ti % 3 != 0) continue;
                for (int k = 0; k <= maximos.Length; k++)
                {
                    if (fi > 0 && k > 1 && k < maximos.Length) continue;
                    string nombre = "aviso-" + fi + "-" + ti + "-" + k; Color fondo = Fondos[(fi + ti + k) % Fondos.Length];
                    using (Bitmap b = new Bitmap(620, 48, PixelFormat.Format32bppPArgb))
                    {
                        using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.FromArgb(200, 250, 20, 60));   // lo de la vez anterior: el aviso lo borra
                        object mapa = deXp ? (object)b : b.Sk;
                        object letra = deXp ? (object)fs[fi] : tPuente.GetMethod("Letra", T, null, new[] { typeof(Font) }, null).Invoke(null, new object[] { fs[fi] });
                        string r = "";
                        try
                        {
                            if (k == 0) pinta.Invoke(null, new object[] { mapa, Textos[ti], fondo, letra });
                            else hasta.Invoke(null, new object[] { mapa, Textos[ti], fondo, letra, maximos[k - 1] });
                        }
                        catch (Exception e) { r = Exc(e) + "\t"; }
                        Linea(nombre, r + Puntos(b, nombre));
                    }
                }
            }
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            Anchos();
            Avisos();
            Formatos();   // (al final: cambia el tipografico compartido)
            Console.Error.WriteLine("vistas ajustes: " + n + " casos");
            return 0;
        }
    }
}

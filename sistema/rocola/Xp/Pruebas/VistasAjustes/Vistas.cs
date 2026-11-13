// ARNES DE VENTANAS2, PASO C (port nativo, 10-11-2026): el dibujo con GDI+ de Form1.Ajustes.cs que el paso tradujo a SkiaSharp y que las
// escenas de Ctrl+F3 no pasan (no son de la hoja: los usa la pantalla): las medidas de texto AnchoTexto (con y sin Graphics) y AnchoToast,
// el formato de CreaSfDibuja y el aviso de siempre (PintaToast y PintaToastHasta; con «Avisos que se entienden» de fabrica, el que sale es
// el de SuenaVivoGpu.cs y este no pasa por los recorridos). Se compila con el programa entero de una carpeta Xp (VistasAjustes.csproj,
// -p:Fuente=...): la referencia congelada (GDI+ de la capa) y esta copia; la salida (una linea por caso: lo que devuelve la funcion y los
// puntos del mapa, tal cual y como los sube la tarjeta) tiene que salir igual linea por linea (compara.sh).
// Cada funcion se llama como la llama el codigo de XP que todavia no esta traducido: con Graphics, Font, StringAlignment y Bitmap (en la
// referencia, la funcion de XP; en la traduccion, la firma de XP que deja Plataforma/Puente.ventanas2.cs; CreaSfDibuja ya no la tiene, y se
// hace lo que hacia su puente: la traducida con Alinea y su FormatoTexto como StringFormat, por reflexion). El aviso, que en la traduccion ya
// no tiene esa firma, con el SKBitmap del mismo mapa y la Letra de la misma Font (por Pru, Pruebas/Comun: la referencia no lo tiene).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet VistasAjustes.dll [vuelca CARPETA]
using System;
using System.Drawing;
using System.Reflection;
using System.Text;
using Banco;
using Rocola;
using SkiaSharp;

namespace Banco
{
    internal static class VistasAjustes
    {
        private const BindingFlags T = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta; private static int n;

        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static string Exc(Exception e) { while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException; return "EXC:" + e.GetType().Name; }
        // los puntos de un mapa: tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb, sin premultiplicar)
        private static unsafe string Puntos(object b, string nombre)
        {
            SKBitmap s = Pru.Sk(b); byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }

        private static Fte[] Fuentes()
        {
            return new[] {
                new Fte("Microsoft Sans Serif", 14f, 1),                             // lblToast (Bold(14f), Form1.cs)
                new Fte("Microsoft Sans Serif", 11.9f, 1),                           // la cabecera
                new Fte("Microsoft Sans Serif", 8.25f),                              // la de fabrica de XP
                new Fte("Microsoft Sans Serif", 13f, 1, "Pixel"),                    // (la generica sin remates de XP)
                new Fte("Tahoma", 9.5f),                                             // (si no esta, Microsoft Sans Serif)
                new Fte("Times New Roman", 15f, 2 | 4),
                new Fte("Trebuchet MS", 21.75f, 1),
            };
        }
        private static readonly string[] Textos = {
            "Inserta una moneda", "LO MAS SONADO", "LETRA W", "12/12", "pagina 3 de 12", "¡Botones configurados!",
            "No se pudo abrir el video: credito devuelto", "02 Ángel de amor (versión en vivo) — Los Ñandúes & Cía", "", "Ñ", "  espacios al final   ",
            "Tab\tdentro y\nsalto", "Un aviso tan largo que no cabe en la pastilla de seiscientos veinte puntos ni queriendo, y acaba en puntos suspensivos", "00:00", null };
        private static readonly Color[] Fondos = { Color.FromArgb(40, 165, 90), Color.FromArgb(200, 130, 35), Color.FromArgb(190, 60, 60), Color.FromArgb(128, 20, 40, 90), Color.FromArgb(0, 0, 0, 0) };
        private static readonly ModoLetra[] Ajustes = { ModoLetra.SystemDefault, ModoLetra.SingleBitPerPixelGridFit, ModoLetra.SingleBitPerPixel,
            ModoLetra.AntiAliasGridFit, ModoLetra.AntiAlias, ModoLetra.ClearTypeGridFit };
        private static MethodInfo Metodo(string nombre, params Type[] tipos) { return typeof(Form1).GetMethod(nombre, T, null, tipos, null); }
        // (rotulos-y-letras) las firmas de XP con la Font de AnchoTexto(string, Font) y AnchoToast(string, Font) estaban en
        // Plataforma/Puente.ventanas2.cs (borrado: sus llamadores ya pasan su Letra): la de XP en la referencia (con la Font) y la traducida en
        // el programa nuevo (con la Letra de esa fuente; la nula, nula)
        private static Func<string, Fte, object> ConFont(string nombre)
        {
            MethodInfo l = null;
            foreach (MethodInfo mi in typeof(Form1).GetMethods(T)) { ParameterInfo[] p = mi.GetParameters(); if (mi.Name == nombre && p.Length == 2 && p[0].ParameterType == typeof(string) && (p[1].ParameterType.Name == "Font" || p[1].ParameterType.Name == "Letra")) l = mi; }
            return (t, f) => l.Invoke(null, new object[] { t, f == null ? null : f.Obj });
        }

        // AnchoTexto(Graphics, string, Font) de XP: en la referencia, la de XP por reflexion; en el programa nuevo ya no existe (la quito el grupo
        // limpieza-puentes el 12-11-2026: nadie la llamaba) y se hace lo que hacia el puente: sin lienzo, sin letra o sin texto, 0; del lienzo,
        // su modo de letra, y la Letra de la fuente, a la traducida AnchoTexto(AjusteLetra, string, Letra)
        private static object AnchoConGraphics(MethodInfo con, Lz g, string t, Fte f)
        {
            if (con != null) return con.Invoke(null, new object[] { g != null ? g.Objeto : null, t, f != null ? f.Obj : null });
            if (g == null || f == null || string.IsNullOrEmpty(t)) return 0;
            foreach (MethodInfo m in typeof(Form1).GetMethods(T))
            {
                ParameterInfo[] p = m.GetParameters();
                if (m.Name != "AnchoTexto" || p.Length != 3 || p[0].ParameterType.Name != "AjusteLetra") continue;
                return m.Invoke(null, new object[] { Enum.ToObject(p[0].ParameterType, (int)g.Letra), t, f.Obj });
            }
            throw new InvalidOperationException("falta AnchoTexto(AjusteLetra, string, Letra)");
        }

        // AnchoTexto(Graphics, string, Font): con cada modo de letra, con suavizado, traslacion, escala y recorte (no deben cambiar la medida), y
        // sin Graphics, sin letra y sin texto (0); AnchoTexto(string, Font)
        private static void Anchos()
        {
            Fte[] fs = Fuentes(); MethodInfo con = null; Func<string, Fte, object> sin = ConFont("AnchoTexto");
            foreach (MethodInfo mi in typeof(Form1).GetMethods(T)) { ParameterInfo[] p = mi.GetParameters(); if (mi.Name == "AnchoTexto" && p.Length == 3 && p[0].ParameterType.Name == "Graphics") con = mi; }
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(64, 32, "Format32bppPArgb"))
                for (int fi = 0; fi < fs.Length; fi++) for (int ti = 0; ti < Textos.Length; ti++)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int k = 0; k < Ajustes.Length * 2; k++)
                        using (Lz g = Pru.Lienzo(b))
                        {
                            g.Letra = Ajustes[k % Ajustes.Length];
                            if (k >= Ajustes.Length) { g.Suavizado = ModoSuave.AntiAlias; g.Traslada(3.5f, 2f); g.Escala(1.3f, 0.8f); g.Recorta(new Rectangle(1, 1, 10, 10)); g.Pixel = ModoPixel.Half; }
                            try { sb.Append(AnchoConGraphics(con, g, Textos[ti], fs[fi])).Append(' '); } catch (Exception e) { sb.Append(Exc(e)).Append(' '); }
                        }
                    string s2; try { s2 = sin(Textos[ti], fs[fi]).ToString(); } catch (Exception e) { s2 = Exc(e); }
                    Linea("ancho-" + fi + "-" + ti, sb.ToString().TrimEnd() + "\tsin=" + s2);
                }
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(4, 4, "Format32bppArgb")) using (Lz g = Pru.Lienzo(b))
                Linea("ancho-nulos", AnchoConGraphics(con, null, "hola", fs[0]) + " " + AnchoConGraphics(con, g, "hola", null) + " " + AnchoConGraphics(con, g, "", fs[0])
                    + " " + sin("hola", null) + " " + sin(null, fs[0]));
        }

        // CreaSfDibuja(StringAlignment): lo que trae y un texto pintado con el (centrado en vertical, sin partir, sin recortar, y con una caja
        // mas baja que la letra); antes y DESPUES de que alguien le cambie cosas al tipografico compartido (como EstadoReposoGpu.cs)
        private static void Formatos()
        {
            Fte[] fs = Fuentes(); MethodInfo m = null;
            // la de XP (CreaSfDibuja(StringAlignment), la referencia) o la traducida (CreaSfDibuja(Alinea): Form1.Vistas.cs la llama directo)
            foreach (MethodInfo x in typeof(Form1).GetMethods(T)) if (x.Name == "CreaSfDibuja" && x.GetParameters().Length == 1) m = x;
            for (int vuelta = 0; vuelta < 2; vuelta++)
            {
                if (vuelta == 1) { Fmt t = Fmt.Tipografico; t.Horizontal = ModoAlinea.Center; t.Vertical = ModoAlinea.Center; t.Banderas |= 4096; }
                foreach (ModoAlinea al in new[] { ModoAlinea.Near, ModoAlinea.Center, ModoAlinea.Far })
                {
                    Fmt sf = Fmt.De(m.Invoke(null, new object[] { Enum.ToObject(m.GetParameters()[0].ParameterType, (int)al) }));
                    Linea("formato-" + vuelta + "-" + al, sf.Horizontal + "," + sf.Vertical + "," + sf.Banderas + "," + sf.Corte + ",otro=" + !sf.EsTipografico);
                    for (int fi = 0; fi < fs.Length; fi += 2) for (int caja = 0; caja < 3; caja++)
                        using (IDisposable b = (IDisposable)Pru.NuevoMapa(220, 40, "Format32bppPArgb"))
                        {
                            using (Lz g = Pru.Lienzo(b))
                            {
                                g.Limpia(Color.FromArgb(255, 12, 16, 30)); g.Letra = Ajustes[(fi + caja) % Ajustes.Length];
                                RectangleF r = caja == 0 ? new RectangleF(4, 4, 212, 32) : caja == 1 ? new RectangleF(10, 14, 60, 6) : new RectangleF(30.5f, 2.25f, 150, 36);
                                g.Escribe("LO MAS SONADO  12/12", fs[fi], Color.FromArgb(230, 240, 250, 255), r, sf);
                            }
                            Linea("formato-" + vuelta + "-" + al + "-" + fi + "-" + caja, Puntos(b, "formato-" + vuelta + "-" + al + "-" + fi + "-" + caja));
                        }
                }
            }
        }

        // el aviso de siempre: AnchoToast y PintaToast / PintaToastHasta sobre un mapa de 620x48 que ya traia algo (el mapa se reusa)
        private static void Avisos()
        {
            Fte[] fs = Fuentes(); Func<string, Fte, object> ancho = ConFont("AnchoToast");
            MethodInfo pinta = null, hasta = null;
            foreach (MethodInfo m in typeof(Form1).GetMethods(T)) { if (m.Name == "PintaToast") pinta = m; else if (m.Name == "PintaToastHasta") hasta = m; }
            bool deXp = Pru.Capa;   // (la de XP pide un Bitmap de la capa; la traducida, un SKBitmap)
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
                    using (IDisposable b = (IDisposable)Pru.NuevoMapa(620, 48, "Format32bppPArgb"))
                    {
                        using (Lz g = Pru.Lienzo(b)) g.Limpia(Color.FromArgb(200, 250, 20, 60));   // lo de la vez anterior: el aviso lo borra
                        object mapa = deXp ? (object)b : Pru.Sk(b);
                        object letra = fs[fi].Obj;
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

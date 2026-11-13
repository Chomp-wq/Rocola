// ARNES DEL GRUPO «PANTALLA-VIVA» (port nativo, tanda 2 del dibujo, 8-11-2026): lo que dibujan PantallaVivaGpu.cs y DiscoVivoGpu.cs con datos
// fijos, la huella de cada mapa (tal cual y como lo sube la tarjeta) y lo que devuelve cada llamada. Se compila con el programa entero de una
// carpeta Xp (VistasPantallaViva.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales linea por
// linea (arnes.sh).
//   - En la referencia las funciones se llaman con la firma de XP (Bitmap, Image, Font, FontFamily, Graphics); en la traduccion, por
//     reflexion, a la traducida con los mismos puntos, la misma letra y el nombre de la familia (la nula, la generica sin remates).
//   - PintaFoco, PintaFicha y PastillaSonandoEq salen dos veces (la segunda, «directo»): en la traduccion las dos van a la traducida; en la
//     referencia, a la de XP.
//   - (Oleada 8b, 13-11-2026) El arnes ya no nombra la capa de GDI+: los mapas, el lienzo, las letras, las familias y los trazos van por
//     Pruebas/Comun (Pru, Lz, Fte: la capa en la referencia congelada y lo nativo en el programa nuevo).
//   - La lista del disco (SongListView): PintaPagina (Form1.Vistas.cs, igual en los dos) con la lista grande, el ecualizador y los minutos,
//     con ocho estados del lienzo: pasa por AnchoDuracion, PlanPaginaGpu, PlanFilaGpu, AnchoSonando y PastillaSonandoEq y AnchoTexto
//     (Form1.Ajustes.cs).
//   - Redondo (con su GraphicsPath, en la referencia; en la traduccion, RutaRedonda, el SKPath) y Pildora: los puntos del trazo, uno por uno, y pintados.
//   ROCOLA_PANTALLA=fuera:64x64 dotnet VistasPantallaViva.dll [vuelca CARPETA] [solo PREFIJO]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using Banco;
using Rocola;
using SkiaSharp;

namespace BancoPantallaViva
{
    internal static class PantallaViva
    {
        private const BindingFlags T = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private static string carpeta, solo;
        private static int n, directas;   // directas: las llamadas a la traducida sin el puente (en la referencia, ninguna)

        // ---------------------------------------------------------------- lo que sale
        private static bool Toca(string nombre) { return solo == null || nombre.StartsWith(solo, StringComparison.Ordinal); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static string R(float v) { return v.ToString("R", ci) + "/" + BitConverter.SingleToInt32Bits(v).ToString("x8"); }
        private static string Exc(Exception e) { while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException; return "EXC:" + e.GetType().Name; }
        // los puntos de un mapa (un Bitmap de la capa o un SKBitmap): tal cual (premultiplicados) y como los sube la tarjeta (LockBits
        // Format32bppArgb, sin premultiplicar)
        private static unsafe string Puntos(object o, string nombre)
        {
            if (o == null) return "null";
            object b = Pru.Aprecia(o, "Format32bppArgb");   // (solo para leerlo: no se suelta, los puntos son del SKBitmap)
            SKBitmap s = Pru.Sk(b); byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre.Replace('/', '_') + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + s.ColorType + "/" + s.AlphaType + "\t" + Huella(px) + "\t" + Huella(sub);
        }
        // un trazo (el SKPath de un GraphicsPath de la capa, o el SKPath de la traducida): su relleno, sus verbos y sus puntos, bit a bit
        private static string Trazo(SKPath p)
        {
            if (p == null) return "null";
            StringBuilder sb = new StringBuilder(p.FillType + " n=" + p.PointCount + " ");
            using (SKPath.RawIterator it = p.CreateRawIterator())
            {
                SKPoint[] q = new SKPoint[4]; SKPathVerb v;
                while ((v = it.Next(q)) != SKPathVerb.Done)
                {
                    sb.Append(v.ToString()[0]);
                    int k = v == SKPathVerb.Move ? 1 : v == SKPathVerb.Line ? 2 : v == SKPathVerb.Quad ? 3 : v == SKPathVerb.Conic ? 3 : v == SKPathVerb.Cubic ? 4 : 0;
                    for (int i = v == SKPathVerb.Move ? 0 : 1; i < k; i++) sb.Append(BitConverter.SingleToInt32Bits(q[i].X).ToString("x8")).Append(',').Append(BitConverter.SingleToInt32Bits(q[i].Y).ToString("x8")).Append(';');
                    if (v == SKPathVerb.Conic) sb.Append('w').Append(BitConverter.SingleToInt32Bits(it.ConicWeight()).ToString("x8"));
                }
            }
            return Huella(Encoding.ASCII.GetBytes(sb.ToString())) + " " + sb.ToString().Substring(0, Math.Min(60, sb.Length));
        }

        // ---------------------------------------------------------------- el estado del Graphics del que llama
        private sealed class Estado { public string Nombre; public ModoSuave S; public ModoLetra Tr; public ModoCopia C; public ModoPixel P; public ModoFiltro I; public int Trans; public int Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "xp", S = ModoSuave.AntiAlias, Tr = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.Bilinear },
            new Estado { Nombre = "gridfit-traslada", S = ModoSuave.AntiAlias, Tr = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.Bilinear, Trans = 1 },
            new Estado { Nombre = "sin-suavizar", S = ModoSuave.None, Tr = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.NearestNeighbor },
            new Estado { Nombre = "copia-medio-decimales", S = ModoSuave.HighQuality, Tr = ModoLetra.AntiAlias, C = ModoCopia.SourceCopy, P = ModoPixel.Half, I = ModoFiltro.HighQualityBicubic, Trans = 2 },
            new Estado { Nombre = "cleartype-escala", S = ModoSuave.AntiAlias, Tr = ModoLetra.ClearTypeGridFit, C = ModoCopia.SourceOver, P = ModoPixel.HighQuality, I = ModoFiltro.HighQualityBilinear, Trans = 3 },
            new Estado { Nombre = "unbit-gira", S = ModoSuave.AntiAlias, Tr = ModoLetra.SingleBitPerPixelGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.Bilinear, Trans = 4 },
            new Estado { Nombre = "unbit-copia-recorte", S = ModoSuave.None, Tr = ModoLetra.SingleBitPerPixel, C = ModoCopia.SourceCopy, P = ModoPixel.Default, I = ModoFiltro.Bilinear, Recorte = 1 },
            new Estado { Nombre = "suave-recorte-trazo", S = ModoSuave.HighQuality, Tr = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.None, I = ModoFiltro.HighQualityBilinear, Trans = 1, Recorte = 2 },
        };
        private static void PonEstado(Lz g, Estado e, int w, int h)
        {
            g.Suavizado = e.S; g.Letra = e.Tr; g.Copia = e.C; g.Pixel = e.P; g.Filtro = e.I;
            if (e.Trans == 1) g.Traslada(2f, 3f); else if (e.Trans == 2) g.Traslada(3.5f, 2.25f); else if (e.Trans == 3) g.Escala(1.3f, 0.9f); else if (e.Trans == 4) g.Gira(5f);
            if (e.Recorte == 1) g.Recorta(new RectangleF(30.5f, 10, w * 0.7f, h * 0.8f));
            else if (e.Recorte == 2) g.RecortaElipse(-w / 4f, -h / 4f, w * 1.2f, h * 1.3f);
        }
        // un mapa (Format32bppPArgb) con fondo: transparente, a medias o con una franja
        private static object Mapa(int w, int h, int fondo)
        {
            object b = Pru.NuevoMapa(w, h, "Format32bppPArgb");
            using (Lz g = Pru.Lienzo(b))
            {
                if (fondo == 0) g.Limpia(Color.Transparent);
                else
                {
                    g.Limpia(fondo == 1 ? Color.FromArgb(120, 30, 60, 90) : Color.FromArgb(255, 12, 16, 30));
                    g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, h / 3, w, Math.Max(1, h / 7));
                }
            }
            return b;
        }
        // una portada de prueba: degradados, rayas y, si 'alfa', una orilla transparente (los puntos sin premultiplicar, como LockBits de
        // Format32bppArgb para escribir)
        private static object Portada(int w, int h, int semilla, bool alfa)
        {
            object b = Pru.NuevoMapa(w, h, "Format32bppArgb");
            Random r = new Random(semilla);
            int c0 = r.Next(256), c1 = r.Next(256), c2 = r.Next(256);
            uint[] d = new uint[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = alfa ? Math.Min(255, (x * 255) / Math.Max(1, w - 1) + ((y * 7) % 40)) : 255;
                    int rr = (c0 + x * 3 + ((y / 8) % 2) * 60) & 255, gg = (c1 + y * 2 + ((x / 11) % 2) * 90) & 255, bb = (c2 + (x ^ y)) & 255;
                    d[y * w + x] = ((uint)a << 24) | ((uint)rr << 16) | ((uint)gg << 8) | (uint)bb;
                }
            Pru.EscribePuntos(b, d);
            return b;
        }
        private static object Gris(int w, int h, int luz, int alfa)
        {
            object b = Pru.NuevoMapa(w, h, "Format32bppArgb");
            using (Lz g = Pru.Lienzo(b)) g.Limpia(Color.FromArgb(alfa, luz, luz, luz + (luz > 200 ? 0 : 3)));
            return b;
        }
        private static int Ancho(object mapa) { return Pru.Sk(mapa).Width; }
        private static int Alto(object mapa) { return Pru.Sk(mapa).Height; }

        // ---------------------------------------------------------------- llamar por reflexion (lo que cambia de firma)
        // (Lz.Estado: el lienzo, suave, letra, copia, filtro, medio)
        private static object Lienzo(Lz g) { return g.Estado()[0]; }
        private static object[] EstadoDe(Lz g) { return g.Estado(); }
        private static object Campo(object[] e, string c) { return e[c == "Suave" ? 1 : c == "Letra" ? 2 : c == "Copia" ? 3 : c == "Filtro" ? 4 : 5]; }
        private static MethodInfo Con(Type t, string nombre, Type primero)
        {
            foreach (MethodInfo m in t.GetMethods(T)) if (m.Name == nombre && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == primero) return m;
            return null;
        }
        // la firma de XP (un Bitmap y una FontFamily) de PintaFoco o PintaFicha: en la referencia, la de XP, por reflexion; en la traduccion no
        // existe, asi que va a la traducida con los mismos puntos y el nombre de la familia (la nula, que va la ultima, es la generica sin
        // remates, como la tomaba new Font(null, ...))
        private static object FirmaXp(Type t, string nombre, MethodInfo directa, object[] a)
        {
            MethodInfo xp = Pru.Capa ? Con(t, nombre, Pru.TMapa) : null;
            if (xp != null) return xp.Invoke(null, a);
            object[] c = (object[])a.Clone();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == null && i == c.Length - 1) c[i] = FamiliaGenerica();
                else if (i == c.Length - 1) c[i] = Pru.NombreDeFamilia(c[i]);
            }
            return directa.Invoke(null, c);
        }
        private static object Llama(MethodInfo m, object inst, object[] a) { return m.Invoke(inst, a); }
        // el valor de 'v' en el tipo de un parametro: los mapas del arnes son ya los de cada lado; una letra del arnes (Fte) va por la suya
        private static object Convierte(object v, Type t)
        {
            if (v == null) return null;
            if (t.IsInstanceOfType(v)) return v;
            if (v is Fte) return ((Fte)v).Obj;
            return v;
        }
        private static object[] Convierte(MethodInfo m, object[] a)
        {
            ParameterInfo[] p = m.GetParameters(); object[] b = new object[a.Length];
            for (int i = 0; i < a.Length; i++) b[i] = Convierte(a[i], p[i].ParameterType.IsByRef ? p[i].ParameterType.GetElementType() : p[i].ParameterType);
            return b;
        }

        // ================================================================ el letrero de la portada elegida (PintaFoco)
        // (las familias: Pru.Familia, la FontFamily de la referencia o su nombre en el programa nuevo; la que no esta instalada lanza y no entra)
        private static object[] Familias()
        {
            List<object> l = new List<object> { Pru.FamiliaGenerica(), null };
            foreach (string s in new[] { "Segoe UI", "Tahoma", "Verdana", "Trebuchet MS" })
                try { l.Add(Pru.Familia(s)); } catch (ArgumentException) { }
            return l.ToArray();
        }
        private static void Focos()
        {
            object[] fams = Familias();
            int[] lados = { 64, 96, 128, 150, 192, 256, 30 };
            string[][] textos = {
                new[] { Form1.TextoFoco(12, 0, 38 * 60), Form1.TextoFoco(12, 0, -1) },
                new[] { Form1.TextoFoco(1, 0, 50), Form1.TextoFoco(1, 0, -1) },
                new[] { Form1.TextoFoco(150, 0, 3 * 3600 + 125), Form1.TextoFoco(150, 0, -1) },
                new[] { Form1.TextoFoco(0, 3, -1), null },
                new[] { "02 Ángel de amor (versión ñandú) & más · 3 h 05 min", "02 Ángel" },
                new[] { "Un texto larguisimo que no cabe ni con la letra mas chica de todas, de verdad", "Un texto larguisimo que no cabe ni con la letra mas chica de todas, de verdad" },
                new[] { "😀 · 1 min", "" },
            };
            MethodInfo directa = Con(typeof(Form1), "PintaFoco", typeof(SKBitmap));
            int k = 0;
            foreach (int lado in lados)
                foreach (int cw in new[] { lado + 24, lado * 2, 100 })
                {
                    int w = Form1.AnchoFoco(lado, cw), h = Form1.AltoFoco(lado);
                    for (int t = 0; t < textos.Length; t++)
                    {
                        object fam = fams[(k++) % fams.Length];
                        string nombre = "foco-" + lado + "-" + cw + "-" + t;
                        if (!Toca(nombre)) continue;
                        string fn = fam == null ? "null" : Pru.NombreDeFamilia(fam);
                        object b = Mapa(w, h, t % 3); string res;
                        try { res = (string)FirmaXp(typeof(Form1), "PintaFoco", directa, new object[] { b, textos[t][0], textos[t][1], fam }); } catch (Exception e) { res = Exc(e); }
                        Linea(nombre, fn + "\t" + res + "\t" + Puntos(b, nombre));
                        Pru.Suelta(b);
                        // la traducida, directo (en la referencia, la de XP otra vez)
                        object b2 = Mapa(w, h, (t + 1) % 3);
                        try
                        {
                            if (directa != null) { directas++; res = (string)directa.Invoke(null, new object[] { Pru.Sk(b2), textos[t][0], textos[t][1], fam == null ? FamiliaGenerica() : Pru.NombreDeFamilia(fam) }); }
                            else res = (string)FirmaXp(typeof(Form1), "PintaFoco", null, new object[] { b2, textos[t][0], textos[t][1], fam ?? Pru.FamiliaGenerica() });
                        }
                        catch (Exception e) { res = Exc(e); }
                        Linea(nombre + "-directo", fn + "\t" + res + "\t" + Puntos(b2, nombre + "-directo"));
                        Pru.Suelta(b2);
                    }
                }
        }
        private static string FamiliaGenerica() { return Pru.NombreDeFamilia(Pru.FamiliaGenerica()); }

        // ================================================================ la ficha rapida (PintaFicha)
        private static void Fichas()
        {
            object[] fams = Familias();
            Size[] tams = { Form1.TamanoFicha(128, 170), Form1.TamanoFicha(170, 188), Form1.TamanoFicha(240, 300), Form1.TamanoFicha(100, 100), Form1.TamanoFicha(320, 240), new Size(260, 120) };
            object[] tapas = { null, Portada(256, 256, 3, false), Portada(300, 300, 5, true), Portada(1600, 1000, 7, false), Portada(1, 1, 9, false), Gris(64, 64, 128, 255) };
            string[] artistas = { "Los Ejemplos", null, "Artista de nombre larguisimo con acentos: Ñandú, Ángel & Cía (en vivo)", "" };
            string[] discos = { "Larga Duracion", "", "Disco de relleno con un nombre que no cabe en la tarjeta" };
            string[] generos = { "Pop", "", "Rock en español", null };
            Color[] colores = { Color.FromArgb(120, 160, 255), Color.FromArgb(230, 90, 160), Color.FromArgb(0, 0, 0), Color.FromArgb(255, 255, 255) };
            string[][] filas = { new string[0], new[] { "Cancion uno" }, new[] { "01 Cancion Larga con un titulo bastante largo que no cabe", "02 Ángel de amor (versión ñandú) & más", null },
                                 new[] { "a", "b", "c" } };
            string[][] durs = { null, new[] { "3:41", "4:02", "2:58" }, new[] { null, "", "12:58" }, new[] { "3:41" } };
            int[][] veces = { null, new[] { 5, 3, 1 }, new[] { 1, 0, 0 }, new[] { 120, 99, 1 } };
            MethodInfo directa = Con(typeof(Form1), "PintaFicha", typeof(SKBitmap));
            int k = 0;
            for (int i = 0; i < 48; i++)
            {
                Size tam = tams[i % tams.Length];
                object tapa = tapas[(i / 2) % tapas.Length];
                object fam = fams[(k++) % fams.Length];
                string art = artistas[i % artistas.Length], dis = discos[(i / 3) % discos.Length], gen = generos[(i / 4) % generos.Length];
                Color col = colores[(i / 5) % colores.Length];
                int enCola = new[] { 0, 1, 7 }[(i / 2) % 3];
                bool pedidas = i % 3 != 2;
                string[] fl = filas[(i / 3) % filas.Length]; string[] du = durs[(i / 4) % durs.Length]; int[] ve = veces[(i / 6) % veces.Length];
                int total = new[] { 0, 12, 2 }[i % 3];
                string resumen = new[] { Form1.TextoFoco(12, 0, 38 * 60), Form1.TextoFoco(2, 0, 300), "", null }[(i / 7) % 4];
                string nombre = "ficha-" + i;
                if (!Toca(nombre)) continue;
                string desc = tam.Width + "x" + tam.Height + "|" + (tapa == null ? "sin" : Ancho(tapa) + "x" + Alto(tapa)) + "|" + (fam == null ? "null" : Pru.NombreDeFamilia(fam)) + "|" + enCola + "|" + pedidas + "|" + fl.Length + "|" + total;
                object b = Mapa(tam.Width, tam.Height, i % 3); string res = "ok";
                try { FirmaXp(typeof(Form1), "PintaFicha", directa, new object[] { b, tapa, art, dis, resumen, gen, col, enCola, pedidas, fl, du, ve, total, fam }); } catch (Exception e) { res = Exc(e); }
                Linea(nombre, desc + "\t" + res + "\t" + Puntos(b, nombre));
                Pru.Suelta(b);
                object b2 = Mapa(tam.Width, tam.Height, (i + 1) % 3); res = "ok";
                try
                {
                    if (directa != null) { directas++; directa.Invoke(null, new object[] { Pru.Sk(b2), Pru.Sk(tapa), art, dis, resumen, gen, col, enCola, pedidas, fl, du, ve, total, fam == null ? FamiliaGenerica() : Pru.NombreDeFamilia(fam) }); }
                    else FirmaXp(typeof(Form1), "PintaFicha", null, new object[] { b2, tapa, art, dis, resumen, gen, col, enCola, pedidas, fl, du, ve, total, fam ?? Pru.FamiliaGenerica() });
                }
                catch (Exception e) { res = Exc(e); }
                Linea(nombre + "-directo", desc + "\t" + res + "\t" + Puntos(b2, nombre + "-directo"));
                Pru.Suelta(b2);
            }
            foreach (object t in tapas) Pru.Suelta(t);
        }

        // ================================================================ el boleto (PintaBoleto, TamanoBoleto)
        private static Fte[] LetrasLista()   // (el estilo: 1 negrita, 2 cursiva)
        {
            return new[] {
                new Fte("Microsoft Sans Serif", 13f, 1),          // la de siempre (CuerpoGrande de la de 30: la de fabrica)
                new Fte("Microsoft Sans Serif", 17f, 1),          // la grande de 1024x768 (51 / 3)
                Fte.Generica(26.666666f, 1), // la grande de 80 px de fila
                new Fte("Tahoma", 14.25f),
                new Fte("Trebuchet MS", 9.75f, 1 | 2),
            };
        }
        private static void Boletos()
        {
            Fte[] letras = LetrasLista();
            MethodInfo pinta = typeof(Form1).GetMethod("PintaBoleto", T), mide = typeof(Form1).GetMethod("TamanoBoleto", T);
            object[] tapas = { null, Portada(256, 256, 11, false), Portada(300, 300, 13, true), Portada(1600, 1000, 17, false), Portada(2, 3, 19, true) };
            string[] titulos = { "02 Ángel de amor (versión ñandú) & más", "01 Cancion Larga con un titulo bastante largo que no cabe en el boleto para nada", "", null, "Video de prueba", "Ñ" };
            int[] filas = { 30, 44, 46, 60, 80, 10 };
            int[] anchos = { 556, 250, 900, 60 };
            for (int i = 0; i < 60; i++)
            {
                Fte f = letras[i % letras.Length]; string tit = titulos[(i / 2) % titulos.Length]; int fila = filas[(i / 3) % filas.Length]; int ancho = anchos[(i / 5) % anchos.Length];
                object tapa = tapas[(i / 4) % tapas.Length];
                string nombre = "boleto-" + i;
                if (!Toca(nombre)) continue;
                object tam;
                try { tam = Llama(mide, null, Convierte(mide, new object[] { fila, tit, f, ancho })); } catch (Exception e) { tam = Exc(e); }
                Linea(nombre + "-tam", fila + "|" + ancho + "|" + f.Nombre + "|" + f.Tamano + "\t" + tam);
                if (!(tam is Size)) continue;
                Size s = (Size)tam;
                object b = Mapa(s.Width, s.Height, i % 3); string res = "ok";
                try { Llama(pinta, null, Convierte(pinta, new object[] { b, tapa, tit, f })); } catch (Exception e) { res = Exc(e); }
                Linea(nombre, (tapa == null ? "sin" : Ancho(tapa) + "x" + Alto(tapa)) + "\t" + res + "\t" + Puntos(b, nombre));
                Pru.Suelta(b);
            }
            foreach (object t in tapas) Pru.Suelta(t);
            foreach (Fte f in letras) f.Dispose();
        }

        // ================================================================ el color de una portada (ColorDePortada)
        private static void Colores()
        {
            MethodInfo m = typeof(GpuPanel).GetMethod("ColorDePortada", T);
            List<KeyValuePair<string, object>> l = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("null", null),
                new KeyValuePair<string, object>("1x1", Portada(1, 1, 1, false)),
                new KeyValuePair<string, object>("2x2", Portada(2, 2, 2, false)),
                new KeyValuePair<string, object>("1x40", Portada(1, 40, 2, false)),
                new KeyValuePair<string, object>("gris", Gris(64, 64, 128, 255)),
                new KeyValuePair<string, object>("negro", Gris(64, 64, 10, 255)),
                new KeyValuePair<string, object>("blanco", Gris(64, 64, 255, 255)),
                new KeyValuePair<string, object>("transparente", Gris(64, 64, 200, 0)),
                new KeyValuePair<string, object>("medio-alfa", Gris(64, 64, 200, 90)),
            };
            for (int s = 0; s < 24; s++) l.Add(new KeyValuePair<string, object>("portada" + s, Portada(new[] { 6, 64, 256, 600, 37 }[s % 5], new[] { 6, 64, 256, 600, 91 }[(s / 2) % 5], 100 + s, s % 3 == 1)));
            uint[] siNo = { 0xFF8FB4FFu, 0xFF000000u, 0x00FFFFFFu };
            int k = 0;
            foreach (KeyValuePair<string, object> p in l)
            {
                string nombre = "color-" + p.Key;
                if (!Toca(nombre)) { k++; continue; }
                uint sn = siNo[(k++) % siNo.Length];
                object[] a = Convierte(m, new object[] { p.Value, sn, 0u });
                string res;
                try { bool ok = (bool)m.Invoke(null, a); res = ok + " " + ((uint)a[2]).ToString("x8"); } catch (Exception e) { res = Exc(e); }
                Linea(nombre, sn.ToString("x8") + "\t" + res);
                Pru.Suelta(p.Value);
            }
        }

        // ================================================================ los trazos (Redondo, Pildora)
        private static void Rutas()
        {
            MethodInfo redondo = Pru.Capa ? typeof(Form1).GetMethod("Redondo", T, null, new[] { typeof(RectangleF), typeof(float) }, null) : null;
            // en la traduccion no hay Redondo (con su GraphicsPath): se pide RutaRedonda (el SKPath, que se suelta al acabar)
            MethodInfo rutaRedonda = redondo == null ? typeof(Form1).GetMethod("RutaRedonda", T, null, new[] { typeof(RectangleF), typeof(float) }, null) : null;
            MethodInfo pildora = typeof(Form1).GetMethod("Pildora", T);
            RectangleF[] cajas = { new RectangleF(1, 1, 309, 248), new RectangleF(2f, 2f, 339.5f, 41.5f), new RectangleF(17.461f, 17.461f, 69.843f, 69.843f), new RectangleF(0.5f, 0.5f, 140.25f, 24.5f),
                                   new RectangleF(10.3f, 7.7f, 0.4f, 30f), new RectangleF(5, 5, 0, 0), new RectangleF(-3.25f, 4.125f, 51.5f, 13.75f), new RectangleF(100.4f, 58.93f, 98.663f, 20.735f),
                                   new RectangleF(8.36f, 7.36f, 31.28f, 31.28f), new RectangleF(3, 4, -20, 15) };
            float[] radios = { 16f, 7.36f, 0f, 0.4f, 12.88f, 1000f, -3f };
            for (int i = 0; i < cajas.Length; i++)
                for (int j = 0; j < radios.Length; j++)
                {
                    string nombre = "redondo-" + i + "-" + j;
                    if (!Toca(nombre)) continue;
                    using (IDisposable gp = (IDisposable)(redondo != null ? redondo.Invoke(null, new object[] { cajas[i], radios[j] }) : rutaRedonda.Invoke(null, new object[] { cajas[i], radios[j] })))
                    {
                        string t = Trazo(Pru.Ruta(gp));
                        object b = Mapa(360, 280, j % 3);
                        using (Lz g = Pru.Lienzo(b))
                        {
                            PonEstado(g, Estados[(i + j) % Estados.Length], 360, 280);
                            g.RellenaRuta(gp, Color.FromArgb(214, 8, 12, 30));
                            g.TrazaRuta(gp, Color.FromArgb(150, 139, 167, 227), 1.5f);
                            Pru.AnadeLinea(gp, 0, 0, 30, 20);   // lo que se le anade despues empieza figura aparte (la cerrada queda como estaba)
                            g.TrazaRuta(gp, Color.FromArgb(255, 255, 255, 0), 1f);
                        }
                        Linea(nombre, t + "\t" + Trazo(Pru.Ruta(gp)) + "\t" + Puntos(b, nombre));
                        Pru.Suelta(b);
                    }
                }
            for (int i = 0; i < cajas.Length; i++)
            {
                string nombre = "pildora-" + i;
                if (!Toca(nombre)) continue;
                // (en la referencia da un GraphicsPath; en la traduccion, el SKPath: se comparan sus puntos, bit a bit; pintado, lo pasan los focos y
                // las fichas, por Etiqueta y PintaFoco)
                object o = pildora.Invoke(null, new object[] { cajas[i] });
                SKPath p = Pru.Ruta(o);
                Linea(nombre, Trazo(p));
                ((IDisposable)o).Dispose();
            }
        }

        // ================================================================ la lista del disco (SongListView: PintaPagina con lo vivo)
        private static void Pon(object o, string campo, object v)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(campo, T | BindingFlags.DeclaredOnly); if (fi != null) { fi.SetValue(o, v); return; } }
            throw new MissingFieldException(o.GetType().Name, campo);
        }
        private static void PonProp(object o, string prop, object v) { o.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance).SetValue(o, v); }
        private static object Lee(object o, string campo)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType) { FieldInfo fi = t.GetField(campo, T | BindingFlags.DeclaredOnly); if (fi != null) return fi.GetValue(o); }
            throw new MissingFieldException(o.GetType().Name, campo);
        }
        // (rotulos-y-letras) las propiedades de la lista por su nombre: en la referencia es un Control de la capa; en la traduccion, una vista sin
        // herencia (vistas-disco) con su Letra (una letra del arnes, Fte, va por la suya: la Font o la Letra)
        private static void PonP(object o, string p, object v)
        {
            PropertyInfo pi = o.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance);
            if (v is Fte) v = ((Fte)v).Obj;
            pi.SetValue(o, v);
        }
        private static void SueltaLista(object l) { IDisposable d = l as IDisposable; if (d != null) d.Dispose(); }
        private static object NuevaLista(int w, int h, int filas, int rowH, Fte letra, int sel, int inicio, Color acento, int vivo, int caso)
        {
            object l = Activator.CreateInstance(typeof(Form1).GetNestedType("SongListView", BindingFlags.NonPublic), true);
            PonP(l, "Bounds", new Rectangle(0, 0, w, h)); PonP(l, "Font", letra);
            Pon(l, "RowH", rowH); Pon(l, "Sel", sel); Pon(l, "PageStart", inicio); Pon(l, "Accent", acento); Pon(l, "Vivo", vivo);
            Pon(l, "GetCount", (Func<int>)(() => filas));
            Pon(l, "GetText", (Func<int, string>)(i => (i + 1).ToString("00") + " " + (i % 3 == 0 ? "Ángel de amor (versión en vivo) — Los Ñandúes & Cía, una larga" : i % 3 == 1 ? "Cancion " + i : "Otra & mas")));
            Pon(l, "GetRight", (Func<int, string>)(i => i % 4 == 3 ? "" : (2 + i % 5) + ":" + (10 + i * 7 % 50).ToString("00")));
            Pon(l, "GetQueuePos", (Func<int, int>)(i => i == 3 ? 1 : i == 6 ? 12 : i == 7 + caso % 3 ? 2 : 0));
            Pon(l, "GetSonando", (Func<int, bool>)(i => i == 5 || (caso % 4 == 1 && i == 1) || (caso % 5 == 2 && i == 3)));
            Pon(l, "GetGenero", (Func<int, string>)(i => caso % 6 == 5 ? "" : i % 2 == 0 ? "Rock" : i % 3 == 0 ? "Rock en español y algo mas" : ""));
            Pon(l, "GetEspera", (Func<int, int>)(p => caso % 7 == 3 ? -1 : p == 1 ? 0 : p == 2 ? 3 + caso % 4 : p * 4 - 1));
            return l;
        }
        private static void Listas()
        {
            Type tl = typeof(Form1).GetNestedType("SongListView", BindingFlags.NonPublic);
            MethodInfo pinta = tl.GetMethod("PintaPagina", T);
            MethodInfo pastillaXp = Pru.Capa ? Con(tl, "PastillaSonandoEq", Pru.TLienzo) : null, pastillaSk = Con(tl, "PastillaSonandoEq", typeof(SKCanvas));
            int[][] medidas = { new[] { 556, 464, 30 }, new[] { 556, 464, 51 }, new[] { 250, 464, 44 }, new[] { 900, 640, 71 }, new[] { 556, 468, 80 }, new[] { 120, 90, 30 } };
            Fte[] letras = LetrasLista();
            for (int caso = 0; caso < 48; caso++)
            {
                int[] m = medidas[caso % medidas.Length];
                int vivo = new[] { 7, 1, 2, 4, 3, 5, 6, 7 }[(caso / 2) % 8];
                Fte letra = (vivo & 1) != 0 ? new Fte("Microsoft Sans Serif", Form1.CuerpoGrande(m[2]), 1) : letras[caso % letras.Length];
                int filas = new[] { 20, 5, 40, 9 }[caso % 4], rpp = Math.Max(1, m[1] / m[2]);
                int inicio = caso % 5 == 4 && filas > rpp ? rpp : 0;
                int sel = inicio + (caso % Math.Max(1, Math.Min(filas - inicio, rpp)));
                Color acento = Color.FromArgb((caso * 53) & 255, (caso * 97) & 255, (caso * 31) & 255);
                object l = NuevaLista(m[0], m[1], filas, m[2], letra, sel, inicio, acento, vivo, caso);
                PonP(l, "BackColor", caso % 2 == 0 ? Color.FromArgb(31, 34, 52) : Color.FromArgb(12, 20, 40));
                Estado e = Estados[caso % Estados.Length];
                string nombre = "lista-" + caso;
                if (!Toca(nombre)) { SueltaLista(l); continue; }
                object b = Mapa(m[0] + 20, m[1] + 20, caso % 3); string res = "ok";
                using (Lz g = Pru.Lienzo(b))
                {
                    PonEstado(g, e, m[0] + 20, m[1] + 20);
                    try
                    {
                        // (PintaPagina se tradujo con el grupo vistas-b: en la traduccion recibe el lienzo y su estado)
                        if (Pru.Capa) pinta.Invoke(l, new object[] { g.Objeto, inicio });
                        else { object[] est = EstadoDe(g); pinta.Invoke(l, new object[] { Lienzo(g), Campo(est, "Suave"), Campo(est, "Letra"), Campo(est, "Copia"), Campo(est, "Filtro"), Campo(est, "Medio"), inicio }); }
                    }
                    catch (Exception x) { res = Exc(x); }
                }
                string eq = Lee(l, "eqIdx") + "|" + R((float)Lee(l, "eqX")) + "|" + R((float)Lee(l, "eqW")) + "|" + R((float)Lee(l, "eqH")) + "|" + Lee(l, "eqBarra") + "|" + Lee(l, "eqHueco")
                          + "|" + Lee(l, "generosFuera") + "|" + Lee(l, "durAncho") + "|" + R((float)Lee(l, "durCuerpo"));
                Linea(nombre, e.Nombre + "|" + m[0] + "x" + m[1] + "/" + m[2] + "|vivo" + vivo + "|" + letra.Tamano + "|" + inicio + "|" + sel + "\t" + res + "\t" + eq + "\t" + Puntos(b, nombre));
                Pru.Suelta(b);
                // la pastilla de SONANDO con el hueco del ecualizador, sola (con fCola, colaB y cenFmt ya hechos por PintaPagina): por la firma de
                // XP y, en la traduccion, directo con el lienzo y el estado
                if (res == "ok" && Lee(l, "fCola") != null)
                {
                    int hCola = new[] { 24, 18, 36, 13 }[caso % 4];
                    for (int via = 0; via < 2; via++)
                    {
                        object c = Mapa(m[0], Math.Max(40, hCola * 2), (caso + via) % 3); string rr; float xMarca = m[0] - 14 - caso;
                        using (Lz g = Pru.Lienzo(c))
                        {
                            PonEstado(g, Estados[(caso + 3) % Estados.Length], m[0], Math.Max(40, hCola * 2));
                            try
                            {
                                object[] a;
                                if ((via == 1 || pastillaXp == null) && pastillaSk != null)   // (sin la firma de XP, quitada del puente con el grupo vistas-b: las dos, directo)
                                {
                                    directas++;
                                    object[] est = EstadoDe(g);
                                    a = new object[] { Lienzo(g), Campo(est, "Letra"), Campo(est, "Filtro"), Campo(est, "Copia"), Campo(est, "Medio"), 5, xMarca, hCola * 0.5f + 3.25f, hCola };
                                    rr = pastillaSk.Invoke(l, a) + "|" + R((float)a[6]);
                                }
                                else { a = new object[] { g.Objeto, 5, xMarca, hCola * 0.5f + 3.25f, hCola }; rr = pastillaXp.Invoke(l, a) + "|" + R((float)a[2]); }
                            }
                            catch (Exception x) { rr = Exc(x); }
                        }
                        string eq2 = Lee(l, "eqIdx") + "|" + R((float)Lee(l, "eqX")) + "|" + R((float)Lee(l, "eqW")) + "|" + R((float)Lee(l, "eqH"));
                        Linea(nombre + "-pastilla" + via, hCola + "\t" + rr + "\t" + eq2 + "\t" + Puntos(c, nombre + "-pastilla" + via));
                        Pru.Suelta(c);
                    }
                }
                SueltaLista(l);
                if ((vivo & 1) != 0) letra.Dispose();
            }
            foreach (Fte f in letras) f.Dispose();
        }

        // ================================================================
        public static int Main(string[] args)
        {
            for (int i = 0; i + 1 < args.Length; i += 2) { if (args[i] == "vuelca") { carpeta = args[i + 1]; System.IO.Directory.CreateDirectory(carpeta); } else if (args[i] == "solo") solo = args[i + 1]; }
            Focos(); Fichas(); Boletos(); Colores(); Rutas(); Listas();
            Console.Error.WriteLine(n + " lineas, " + directas + " directas");
            return 0;
        }
    }
}

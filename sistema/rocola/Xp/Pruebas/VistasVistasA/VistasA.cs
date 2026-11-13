// ARNES DEL GRUPO «VISTAS-A» (port nativo, tanda 3 del dibujo, 11-11-2026): lo que dibuja la primera parte de Form1.Vistas.cs (hasta la linea
// 997): SongPagerBar (la barra de paginas de la lista: los recorridos NO la pasan, con tarjeta la pinta DrawSongPagerGpu) y LetterBar (la barra
// de letras: su dibujo por procesador, OnPaint, que los recorridos casi no pasan; su tira guardada; la capsula de neon; la estrella dorada; y la
// lupa), con datos fijos, la huella de cada mapa y el estado que le queda al Graphics. Se compila con el programa de una carpeta Xp
// (VistasVistasA.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales linea por linea.
//   - Las llamadas son las de XP (RenderContent con un Graphics, OnPaint): en la referencia van a GDI+ (la capa congelada); en la traduccion
//     (desde vistas-disco, sin la firma de XP en el puente ni OnPaint) van a la traducida con el estado del Graphics, y el OnPaint a Pinta. El estado del Graphics del que llama va de once maneras (suavizado, los
//     modos de letra, copia, medio pixel, filtro, traslacion entera y con decimales, escala, giro y recorte).
//   - Las dos barras son clases privadas de Form1: van por reflexion.
//   - En la traduccion, cada caso se hace ADEMAS llamando DIRECTO a la traducida (el lienzo y su estado como parametros), como la llamaran
//     Form1.Video.cs y las ventanas cuando se traduzcan, sobre una barra y un mapa nuevos e iguales; en la referencia esa linea es la de XP otra vez.
//   - Lo que depende del reloj se fija: el letrero que viaja de la barra de paginas (Environment.TickCount: se repite hasta que el dibujo entero
//     cae en el mismo milisegundo) y la capsula que viaja de la barra de letras (un cronometro: se echa a andar sin moverla de casilla y se repite
//     hasta que el paso leido es tan pequeno que el blanco de la letra sale con alfa 0; lo dice un comparador puesto en las letras disponibles,
//     que la barra consulta justo despues de leer el paso).
//   - (Oleada 8b, 13-11-2026) El arnes ya no nombra la capa de GDI+ ni la de WinForms: los mapas, el lienzo, las letras, el formato
//     tipografico y el OnPaint de la referencia van por Pruebas/Comun (Pru, Lz, Fte, Fmt: la capa en la referencia congelada y lo nativo en el
//     programa nuevo).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasVistasA.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using Banco;
using Rocola;
using SkiaSharp;

namespace BancoVistasA
{
    internal static class VistasA
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta;
        private static int n, directas, reintentos, sinTiempo;
        private static string Huella(byte[] b) { byte[] h = System.Security.Cryptography.SHA256.HashData(b); StringBuilder sb = new StringBuilder(); for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2")); return sb.ToString(); }
        private static void Linea(string nombre, string que) { n++; Console.WriteLine(nombre + "\t" + que); }
        private static string Exc(Exception x) { return "EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }

        // los puntos de un mapa: tal cual (premultiplicados) y como los sube la tarjeta (LockBits Format32bppArgb: sin premultiplicar)
        private static unsafe string Puntos(string nombre, object b)
        {
            SKBitmap s = Pru.Sk(b); byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre.Replace('/', '_') + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }
        // el estado que le queda al Graphics (lo que XP le deja puesto sin devolverlo) y el tipografico compartido (nadie debe cambiarlo aqui)
        private static string Queda(Lz g)
        {
            return "queda=" + g.Suavizado + "/" + g.Letra + "/" + g.Copia + "/" + g.Filtro + "/" + g.Pixel
                + " tip=" + Fmt.Tipografico.Horizontal + "/" + Fmt.Tipografico.Banderas;
        }

        // ---- el estado del Graphics del que llama
        private sealed class Estado { public string Nombre; public ModoSuave S; public ModoLetra T; public ModoCopia C; public ModoPixel P; public ModoFiltro I = ModoFiltro.Bilinear; public int Trans; public bool Recorte; }
        private static readonly Estado[] Estados = {
            // los dos de la tabla de estado por linea: la imagen de la interfaz con tarjeta (EnsureUiOverlay) y el dibujo por procesador (OnPaint)
            new Estado { Nombre = "tarjeta", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, Trans = 6 },
            new Estado { Nombre = "procesador", S = ModoSuave.None, T = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "xp-suave", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "copia-medio-traslada", S = ModoSuave.HighQuality, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceCopy, P = ModoPixel.Half, Trans = 1 },
            new Estado { Nombre = "cleartype-escala-vecino", S = ModoSuave.AntiAlias, T = ModoLetra.ClearTypeGridFit, C = ModoCopia.SourceOver, P = ModoPixel.HighQuality, I = ModoFiltro.NearestNeighbor, Trans = 2 },
            new Estado { Nombre = "unbit-gira-bicubica", S = ModoSuave.AntiAlias, T = ModoLetra.SingleBitPerPixelGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.HighQualityBicubic, Trans = 3 },
            new Estado { Nombre = "unbit-copia-recorte", S = ModoSuave.None, T = ModoLetra.SingleBitPerPixel, C = ModoCopia.SourceCopy, P = ModoPixel.Default, Recorte = true },
            new Estado { Nombre = "traslada-recorte-alta", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.HighQualityBilinear, Trans = 4, Recorte = true },
            new Estado { Nombre = "suave-sin-ajuste-decimal", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceOver, P = ModoPixel.None, Trans = 5 },
            new Estado { Nombre = "escala-decimal-bilineal", S = ModoSuave.None, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.Bilinear, Trans = 7 },
            new Estado { Nombre = "unbit-gira-vecino", S = ModoSuave.AntiAlias, T = ModoLetra.SingleBitPerPixelGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default, I = ModoFiltro.NearestNeighbor, Trans = 3 },
        };
        // un mapa con fondo (para ver la copia y el alfa) y el Graphics con ese estado
        private static Lz Prepara(object b, Estado e)
        {
            Lz g = Pru.Lienzo(b);
            g.Limpia(Color.FromArgb(120, 30, 60, 90));
            g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, 17, Pru.Sk(b).Width, 9);
            g.Suavizado = e.S; g.Letra = e.T; g.Copia = e.C; g.Pixel = e.P; g.Filtro = e.I;
            if (e.Trans == 1) g.Traslada(3.5f, 2.25f); else if (e.Trans == 2) g.Escala(1.3f, 1.3f); else if (e.Trans == 3) g.Gira(2f);
            else if (e.Trans == 4) g.Traslada(0, -7); else if (e.Trans == 5) g.Traslada(0.5f, 0f); else if (e.Trans == 6) g.Traslada(11, 6);
            else if (e.Trans == 7) { g.Traslada(2.3f, 1.7f); g.Escala(0.9f, 1.1f); }
            if (e.Recorte) g.Recorta(new RectangleF(10.5f, 4, 420, 30));
            return g;
        }

        // ---- la traduccion, por reflexion (en la referencia no esta)
        // la traducida de 'nombre' (su primer parametro es el lienzo de Skia), con 'cuantos' parametros
        private static MethodInfo Traducida(Type t, string nombre, int cuantos)
        {
            foreach (MethodInfo m in t.GetMethods(Todo))
                if (m.Name == nombre && m.GetParameters().Length == cuantos && m.GetParameters()[0].ParameterType == typeof(SKCanvas)) return m;
            return null;
        }
        private static MethodInfo DeXp(Type t, string nombre, int cuantos)
        {
            foreach (MethodInfo m in t.GetMethods(Todo))
                if (Pru.Capa && m.Name == nombre && m.GetParameters().Length == cuantos && m.GetParameters()[0].ParameterType == Pru.TLienzo) return m;
            return null;
        }
        // los valores del estado del lienzo como los lleva un archivo traducido (Lz.Estado: el lienzo, suave, letra, copia, filtro, medio)
        private static object Campo(object[] e, string c) { return e[c == "Suave" ? 1 : c == "Letra" ? 2 : c == "Copia" ? 3 : c == "Filtro" ? 4 : 5]; }
        // (vistas-disco, 12-11-2026) las dos barras ya no heredan de Control en la traduccion (son vistas de la pantalla nativa: IVista con su
        // CajaVista): su sitio, su fondo, su letra y su medida van por reflexion (de Control en la referencia, suyos en la traduccion), y su OnPaint
        // de XP, que en la traduccion ya no existe, es su Pinta con el lienzo del Graphics (lo que la pantalla le da), y despues lo que XP le dejaba
        // puesto al Graphics (cosa del que llama, como en las directas)
        private static int pintas;
        private static void PonSitio(object o, int x, int y, int w, int h) { o.GetType().GetMethod("SetBounds", Todo, null, new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null).Invoke(o, new object[] { x, y, w, h }); }
        private static void PonProp(object o, string p, object v) { PropertyInfo pi = o.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance); pi.SetValue(o, LetraSiHace(pi.PropertyType, v)); }
        // (rotulos-y-letras) la traduccion pide una Letra donde la referencia pide la Font (la Font de las vistas): una letra del arnes (Fte) va
        // por la suya
        private static object LetraSiHace(Type destino, object v)
        {
            if (v is Fte) return ((Fte)v).Obj;
            return v;
        }
        private static int Medida(object o, string p) { return (int)o.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance).GetValue(o); }
        private static void PorOnPaint(object o, Lz g, Rectangle r, Action queda)
        {
            MethodInfo op = Pru.Capa ? o.GetType().GetMethod("OnPaint", Todo) : null;   // (OnPaint(PaintEventArgs): solo la referencia)
            if (op != null) { Pru.OnPaint(op, o, g, r); return; }
            pintas++;
            try { o.GetType().GetMethod("Pinta", Todo, null, new[] { typeof(SKCanvas) }, null).Invoke(o, new[] { g.Estado()[0] }); }
            finally { queda(); }
        }

        // ================================================================ SongPagerBar: la barra de paginas
        private static Type Paginas { get { return typeof(Form1).GetNestedType("SongPagerBar", BindingFlags.NonPublic); } }
        private sealed class CasoPaginas { public int W, H, Pages, Page; public Color Fondo; public int Viaje = -1; public int Desde; }
        private static object HazPaginas(CasoPaginas c)
        {
            Type t = Paginas;
            object o = Activator.CreateInstance(t, true);
            PonSitio(o, 7, 5, c.W, c.H); PonProp(o, "BackColor", c.Fondo);
            t.GetField("Pages", Todo).SetValue(o, c.Pages);
            t.GetMethod("PonSinAnimar", Todo).Invoke(o, new object[] { c.Page });
            return o;
        }
        // pinta la barra en 'g' por la firma de XP o, en la traduccion y con directa, por la traducida con el lienzo y el estado de 'g' (y despues lo
        // que XP le deja al Graphics, que es cosa del que llama)
        private static void PintaPaginas(object o, Lz g, bool directa)
        {
            Type t = Paginas; MethodInfo tr = directa ? Traducida(t, "RenderContent", 4) : null;
            if (tr == null) { MethodInfo xp = DeXp(t, "RenderContent", 1); if (xp != null) { xp.Invoke(o, new object[] { g.Objeto }); return; } tr = Traducida(t, "RenderContent", 4); }   // (vistas-disco: en la traduccion ya no hay firma de XP: la traducida con el estado del Graphics y lo que XP le dejaba)
            object[] e = g.Estado(); directas++;
            bool sale = (int)t.GetField("Pages", Todo).GetValue(o) <= 1 || Medida(o, "Width") < 60 || Medida(o, "Height") < 10;
            try { tr.Invoke(o, new[] { e[0], Campo(e, "Suave"), Campo(e, "Copia"), Campo(e, "Medio") }); }
            finally { if (!sale) { g.Suavizado = ModoSuave.AntiAlias; g.Letra = ModoLetra.ClearTypeGridFit; } }
        }
        // con el letrero viajando: se pone el instante en que empezo (hace 'ms' milisegundos) y se pinta; vale si al acabar el reloj sigue en el
        // mismo milisegundo (asi lo que leyo la barra es exactamente 'ms')
        private static string UnaPaginas(string nombre, CasoPaginas c, Estado e, bool directa)
        {
            Type t = Paginas;
            for (int intento = 0; intento < 2000; intento++)
            {
                object o = HazPaginas(c);
                using (IDisposable b = (IDisposable)Pru.NuevoMapa(c.W + 40, c.H + 30, "Format32bppPArgb"))
                {
                    Lz g = Prepara(b, e);
                    string ex = "";
                    try
                    {
                        g.Traslada(7, 5);
                        int t0 = 0;
                        if (c.Viaje >= 0)
                        {
                            t.GetField("pagAnt", Todo).SetValue(o, c.Desde); t.GetField("animDir", Todo).SetValue(o, c.Page > c.Desde ? 1f : -1f);
                            t0 = Environment.TickCount; while (Environment.TickCount == t0) { }   // al principio de un milisegundo
                            t0 = Environment.TickCount; t.GetField("animTick", Todo).SetValue(o, t0 - c.Viaje);
                        }
                        PintaPaginas(o, g, directa);
                        if (c.Viaje >= 0 && Environment.TickCount != t0) { reintentos++; g.Dispose(); ((IDisposable)o).Dispose(); continue; }
                    }
                    catch (Exception x) { ex = " " + Exc(x); }
                    string r = Puntos(nombre, b) + "\t" + Queda(g) + ex;
                    g.Dispose(); ((IDisposable)o).Dispose();
                    return r;
                }
            }
            sinTiempo++; return "SIN-TIEMPO";
        }
        private static void BarraPaginas()
        {
            Color panel = Color.FromArgb(255, 38, 56, 96), vidrio = Color.FromArgb(255, 20, 30, 60);   // (un control sin SupportsTransparentBackColor no admite un fondo con alfa)
            int[][] medidas = { new[] { 760, 24 }, new[] { 500, 30 }, new[] { 1200, 40 }, new[] { 321, 21 }, new[] { 61, 10 }, new[] { 59, 24 }, new[] { 300, 9 }, new[] { 140, 26 } };
            int[][] paginas = { new[] { 1, 0 }, new[] { 2, 0 }, new[] { 2, 1 }, new[] { 3, 1 }, new[] { 12, 0 }, new[] { 12, 5 }, new[] { 12, 11 }, new[] { 120, 98 } };
            foreach (int[] m in medidas)
                foreach (int[] p in paginas)
                    foreach (Estado e in Estados)
                    {
                        // (no todas las combinaciones: todas las medidas con dos estados y la medida de la rocola con todos)
                        if (!(m[0] == 760 || e.Nombre == "tarjeta" || e.Nombre == "procesador" || (m[0] == 500 && e.Recorte))) continue;
                        CasoPaginas c = new CasoPaginas { W = m[0], H = m[1], Pages = p[0], Page = p[1], Fondo = (p[0] % 3 == 0) ? vidrio : panel };
                        string s = "paginas/" + m[0] + "x" + m[1] + "/" + (p[1] + 1) + "de" + p[0] + "/" + e.Nombre;
                        Linea(s, UnaPaginas(s, c, e, false));
                        Linea(s + "/directa", UnaPaginas(s + "-directa", c, e, true));
                    }
            // el letrero viajando: a 0, 1, 60, 150 y 229 ms de los 230, hacia delante y hacia atras
            foreach (int ms in new[] { 0, 1, 60, 150, 229 })
                foreach (int[] v in new[] { new[] { 5, 4 }, new[] { 5, 6 }, new[] { 0, 1 }, new[] { 11, 10 } })
                    foreach (Estado e in Estados)
                    {
                        if (!(e.Nombre == "tarjeta" || e.Nombre == "procesador" || e.Nombre == "copia-medio-traslada" || (ms == 60 && v[0] == 5))) continue;
                        CasoPaginas c = new CasoPaginas { W = 760, H = 24, Pages = 12, Page = v[0], Fondo = panel, Viaje = ms, Desde = v[1] };
                        string s = "paginas-viaje/" + ms + "ms/" + (v[1] + 1) + "a" + (v[0] + 1) + "/" + e.Nombre;
                        Linea(s, UnaPaginas(s, c, e, false));
                        Linea(s + "/directa", UnaPaginas(s + "-directa", c, e, true));
                    }
            // dos veces seguidas en el mismo Graphics (la segunda lo recibe como lo dejo la primera) y la letra guardada entre llamadas
            foreach (Estado e in Estados)
            {
                object o = HazPaginas(new CasoPaginas { W = 760, H = 24, Pages = 4, Page = 2, Fondo = vidrio });
                using (IDisposable b = (IDisposable)Pru.NuevoMapa(800, 90, "Format32bppPArgb"))
                using (Lz g = Prepara(b, e))
                {
                    string ex = "";
                    try { g.Traslada(7, 5); PintaPaginas(o, g, false); g.Traslada(0, 30); Paginas.GetMethod("PonSinAnimar", Todo).Invoke(o, new object[] { 3 }); PintaPaginas(o, g, false); }
                    catch (Exception x) { ex = " " + Exc(x); }
                    Linea("paginas-dos/" + e.Nombre, Puntos("paginas-dos-" + e.Nombre, b) + "\t" + Queda(g) + ex);
                }
                ((IDisposable)o).Dispose();
            }
            // el dibujo por procesador: OnPaint, con el Graphics como lo da el escritorio (su sitio en la matriz del HDC y su recorte sellados)
            foreach (int[] p in paginas)
            {
                object o = HazPaginas(new CasoPaginas { W = 760, H = 24, Pages = p[0], Page = p[1], Fondo = panel });
                using (IDisposable b = (IDisposable)Pru.NuevoMapa(800, 60, "Format32bppPArgb"))
                using (Lz g = Pru.Lienzo(b))
                {
                    string ex = "";
                    g.Limpia(Color.FromArgb(255, 12, 18, 40));
                    g.Traslada(13, 9); g.Recorta(new Rectangle(0, 0, 760, 24)); g.Sella();
                    try { PorOnPaint(o, g, new Rectangle(0, 0, 760, 24), delegate { if (!(p[0] <= 1)) { g.Suavizado = ModoSuave.AntiAlias; g.Letra = ModoLetra.ClearTypeGridFit; } }); }
                    catch (Exception x) { ex = " " + Exc(x); }
                    Linea("paginas-onpaint/" + (p[1] + 1) + "de" + p[0], Puntos("paginas-onpaint-" + (p[1] + 1) + "de" + p[0], b) + "\t" + Queda(g) + ex);
                }
                ((IDisposable)o).Dispose();
            }
        }

        // ================================================================ LetterBar: la barra de letras
        private static Type Letras_ { get { return typeof(Form1).GetNestedType("LetterBar", BindingFlags.NonPublic); } }
        private const string Todas = "★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
        // el comparador de las letras disponibles: apunta cuanto llevaba el cronometro de la capsula la primera vez que la barra pregunta por una
        // letra despues de armarlo (lo pregunta justo despues de leer el paso)
        private sealed class Chivato : IEqualityComparer<char>
        {
            public System.Diagnostics.Stopwatch Reloj; public double Visto = -1;
            public bool Equals(char a, char b) { return a == b; }
            public int GetHashCode(char c) { if (Reloj != null && Visto < 0) Visto = Reloj.Elapsed.TotalMilliseconds; return c; }
        }
        private sealed class CasoLetras
        {
            public int W = 956, H = 40; public string Letters = Todas; public string Hay = Todas; public bool HayNulo; public int Cuentas = 1; public int Tintes = 1;
            public char Actual = 'L'; public bool Corta = true, Lupa, ForGpu = true; public double Fase; public string Familia; public bool Viaja;
        }
        private static Dictionary<char, int> HazCuentas(int modo, string letras)
        {
            if (modo == 0) return null;
            Dictionary<char, int> d = new Dictionary<char, int>();
            if (modo == 3) return d;   // un diccionario sin nada: no hay numeros que ensenar
            for (int i = 0; i < letras.Length; i++)
            {
                int v = modo == 1 ? new[] { 147, 0, 9, 2300, 12, 80, 9949, 1, 33, 10, 0, 518, 9950, 61, 7, 1000, 44, 999, 3, 76, 205, 12345, 5, 18, 0, 2, 31, 9, 4 }[i % 29]
                                  : new[] { 5, 3, 1, 1234567, 10000, 9999, 0, 42 }[i % 8];
                if (v > 0 || i % 2 == 0) d[letras[i]] = v;
            }
            return d;
        }
        private static Dictionary<char, Color> HazTintes(int modo, string letras)
        {
            if (modo == 0) return null;
            Dictionary<char, Color> d = new Dictionary<char, Color>();
            Color[] cs = { Color.FromArgb(150, 20, 20), Color.FromArgb(70, 30, 120), Color.FromArgb(240, 200, 60), Color.FromArgb(40, 160, 90), Color.FromArgb(255, 255, 255), Color.FromArgb(0, 0, 0), Color.FromArgb(30, 120, 220) };
            for (int i = 0; i < letras.Length; i++) if (i % 4 != 3) d[letras[i]] = cs[i % cs.Length];
            return d;
        }
        private static object HazLetras(CasoLetras c, out Chivato chivato)
        {
            Type t = Letras_;
            object o = Activator.CreateInstance(t, true);
            PonSitio(o, 9, 4, c.W, c.H);
            if (c.Familia != null) PonProp(o, "Font", new Fte(c.Familia, 9f));
            t.GetField("Letters", Todo).SetValue(o, c.Letters);
            chivato = new Chivato();
            HashSet<char> hay = null;
            if (!c.HayNulo) { hay = new HashSet<char>(chivato); foreach (char ch in c.Hay) hay.Add(ch); }
            t.GetField("Available", Todo).SetValue(o, hay);
            t.GetField("Counts", Todo).SetValue(o, HazCuentas(c.Cuentas, c.Letters ?? ""));
            t.GetField("Tints", Todo).SetValue(o, HazTintes(c.Tintes, c.Letters ?? ""));
            t.GetProperty("CuentaCorta", Todo).SetValue(o, c.Corta);
            t.GetField("cur", Todo).SetValue(o, c.Actual);   // (sin deslizar: el campo, no la propiedad)
            t.GetField("Phase", Todo).SetValue(o, c.Fase);
            FieldInfo lupa = t.GetField("LupaGpu", Todo); if (lupa != null) lupa.SetValue(o, c.Lupa);
            return o;
        }
        private static void PintaLetras(object o, Lz g, bool forGpu, bool directa)
        {
            Type t = Letras_; MethodInfo tr = directa ? Traducida(t, "RenderContent", 6) : null;
            if (tr == null) { MethodInfo xp = DeXp(t, "RenderContent", 2); if (xp != null) { xp.Invoke(o, new object[] { g.Objeto, forGpu }); return; } tr = Traducida(t, "RenderContent", 6); }   // (vistas-disco: en la traduccion ya no hay firma de XP: la traducida con el estado del Graphics y lo que XP le dejaba)
            object[] e = g.Estado(); directas++;
            string letras = (string)t.GetField("Letters", Todo).GetValue(o);
            bool sale = Medida(o, "Width") < 4 || Medida(o, "Height") < 4 || letras == null || letras.Length == 0;
            try { tr.Invoke(o, new[] { e[0], Campo(e, "Letra"), Campo(e, "Copia"), Campo(e, "Filtro"), Campo(e, "Medio"), forGpu }); }
            finally { if (!sale) g.Suavizado = ModoSuave.AntiAlias; }
        }
        private static string UnaLetras(string nombre, CasoLetras c, Estado e, bool directa)
        {
            Type t = Letras_;
            for (int intento = 0; intento < 1500; intento++)
            {
                Chivato chivato;
                object o = HazLetras(c, out chivato);
                using (IDisposable b = (IDisposable)Pru.NuevoMapa(c.W + 40, c.H + 30, "Format32bppPArgb"))
                {
                    Lz g = Prepara(b, e);
                    string ex = "";
                    try
                    {
                        g.Traslada(9, 4);
                        if (c.Viaja)
                        {
                            // se calienta (los cuerpos de letra y la tira guardada) en un mapa aparte, con el mismo estado; despues la capsula
                            // «viaja» de su casilla a su casilla (no se mueve) con el cronometro recien echado a andar
                            using (IDisposable b0 = (IDisposable)Pru.NuevoMapa(c.W + 40, c.H + 30, "Format32bppPArgb"))
                            using (Lz g0 = Prepara(b0, e)) { g0.Traslada(9, 4); PintaLetras(o, g0, c.ForGpu, directa); PintaLetras(o, g0, c.ForGpu, directa); }
                            System.Diagnostics.Stopwatch reloj = (System.Diagnostics.Stopwatch)t.GetField("animReloj", Todo).GetValue(o);
                            t.GetField("animDesde", Todo).SetValue(o, c.Actual);
                            chivato.Visto = -1; chivato.Reloj = reloj;
                            reloj.Reset(); reloj.Start();
                        }
                        PintaLetras(o, g, c.ForGpu, directa);
                        // paso = 1 - (1 - ms/190)^3 < 1/255 (el blanco de la letra con alfa 0) mientras ms < 0,248
                        if (c.Viaja && !(chivato.Visto >= 0 && chivato.Visto < 0.2)) { reintentos++; g.Dispose(); continue; }
                    }
                    catch (Exception x) { ex = " " + Exc(x); }
                    string r = Puntos(nombre, b) + "\t" + Queda(g) + ex;
                    g.Dispose();
                    return r;
                }
            }
            sinTiempo++; return "SIN-TIEMPO";
        }
        private static string Nombre(CasoLetras c)
        {
            return c.W + "x" + c.H + "/" + (c.Letters == null ? "nulas" : c.Letters.Length.ToString(ci)) + "letras/hay" + (c.HayNulo ? "nulo" : c.Hay.Length.ToString(ci)) + "/cuentas" + c.Cuentas + (c.Corta ? "k" : "")
                + "/tintes" + c.Tintes + "/en" + (c.Actual == '★' ? "TOP" : c.Actual == '►' ? "VIDEO" : c.Actual.ToString()) + (c.ForGpu ? "/tarjeta" : "/procesador-fase" + c.Fase.ToString("R", ci))
                + (c.Lupa ? "/lupa" : "") + (c.Familia != null ? "/" + c.Familia.Replace(" ", "") : "") + (c.Viaja ? "/viaja" : "");
        }
        private static void Caso(CasoLetras c, Func<Estado, bool> cuales)
        {
            foreach (Estado e in Estados)
            {
                if (cuales != null && !cuales(e)) continue;
                string s = "letras/" + Nombre(c) + "/" + e.Nombre;
                Linea(s, UnaLetras(s, c, e, false));
                Linea(s + "/directa", UnaLetras(s + "-directa", c, e, true));
            }
        }
        // (con el giro y el filtro bicubico, pintar la tira tarda mas de lo que deja el reloj de la capsula: el giro va con el del vecino)
        private static bool SinBicubica(Estado e) { return e.Nombre != "unbit-gira-bicubica"; }
        private static bool Dos(Estado e) { return e.Nombre == "tarjeta" || e.Nombre == "procesador"; }
        private static bool Cuatro(Estado e) { return Dos(e) || e.Nombre == "copia-medio-traslada" || e.Nombre == "traslada-recorte-alta"; }
        private static void BarraLetras()
        {
            const string sinTop = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ►", pocas = "★L►", algunas = "★#ACDELMNPRSTV►", sinEstrella = "#ACDELMNPRSTV", seis = "★AMRZ►";
            // la de la rocola (956x40 con tarjeta; la letra L) con todos los estados: sin y con lupa, con tarjeta y por procesador
            foreach (bool lupa in new[] { false, true })
            {
                Caso(new CasoLetras { Lupa = lupa }, null);
                Caso(new CasoLetras { Lupa = lupa, ForGpu = false, Fase = 1.3 }, null);
                Caso(new CasoLetras { Lupa = lupa, Actual = '★' }, null);   // la estrella elegida: la capsula dorada
                Caso(new CasoLetras { Lupa = lupa, Actual = '►' }, Cuatro);   // VIDEO elegido
                Caso(new CasoLetras { Lupa = lupa, Actual = '#', Hay = algunas }, Cuatro);
                Caso(new CasoLetras { Lupa = lupa, Actual = 'B', Hay = sinEstrella }, Cuatro);   // la estrella apagada va en la tira; B no tiene canciones
                Caso(new CasoLetras { Lupa = lupa, Actual = 'q' }, Dos);   // una letra que no esta en la barra
                Caso(new CasoLetras { Lupa = lupa, HayNulo = true, Cuentas = 0, Tintes = 0 }, Cuatro);
                Caso(new CasoLetras { Lupa = lupa, Cuentas = 2, Corta = false }, Cuatro);   // sin miles abreviados
                Caso(new CasoLetras { Lupa = lupa, Cuentas = 2, Corta = true, Tintes = 0 }, Dos);
                Caso(new CasoLetras { Lupa = lupa, Cuentas = 3 }, Dos);   // ningun numero que ensenar: las letras ocupan todo el alto
                // otras medidas: con un video al lado (la mitad: VIDEO no cabe y va el simbolo; sin sitio para los numeros), pantalla ancha, baja,
                // diminuta, y las tres letras del asistente de botones (318,667 por casilla)
                Caso(new CasoLetras { Lupa = lupa, W = 478 }, Cuatro);
                Caso(new CasoLetras { Lupa = lupa, W = 478, Actual = '►', ForGpu = false, Fase = 0.4 }, Dos);
                Caso(new CasoLetras { Lupa = lupa, W = 1880, H = 74, Actual = 'S' }, Cuatro);
                Caso(new CasoLetras { Lupa = lupa, W = 996, H = 33, Actual = 'A' }, Dos);
                Caso(new CasoLetras { Lupa = lupa, W = 200, H = 20, Actual = 'C' }, Dos);
                Caso(new CasoLetras { Lupa = lupa, W = 3, H = 40 }, Dos);
                Caso(new CasoLetras { Lupa = lupa, W = 956, H = 3 }, Dos);
                Caso(new CasoLetras { Lupa = lupa, Letters = pocas, Hay = pocas, Cuentas = 2 }, Cuatro);
                Caso(new CasoLetras { Lupa = lupa, Letters = pocas, Hay = "L", Cuentas = 2, ForGpu = false, Fase = 2.9 }, Dos);
                Caso(new CasoLetras { Lupa = lupa, Letters = sinTop, Hay = sinTop, Actual = 'Z' }, Dos);
                // (revision) una barra tan baja que el tope de la lupa lo pone el ALTO de la letra (EscalaDe: los 96 puntos por pulgada de g.DpiY), no el ancho
                Caso(new CasoLetras { Lupa = lupa, W = 300, H = 8, Letters = pocas, Hay = pocas, Cuentas = 0 }, Dos);
                Caso(new CasoLetras { Lupa = lupa, W = 300, H = 9, Letters = pocas, Hay = pocas, Cuentas = 0, Actual = '►' }, Dos);
                Caso(new CasoLetras { Lupa = lupa, Letters = "", Hay = "" }, Dos);
                Caso(new CasoLetras { Lupa = lupa, Letters = null, Hay = "" }, Dos);
                // otra letra en el control (la familia sale de la del control)
                Caso(new CasoLetras { Lupa = lupa, Familia = "Arial", Actual = 'M' }, Cuatro);
                Caso(new CasoLetras { Lupa = lupa, Familia = "Times New Roman", Actual = 'W', W = 640 }, Dos);
                // la capsula viajando (el paso recien empezado): la letra elegida se pinta grabada, con su color, y el blanco con alfa 0
                // (la de la rocola, con los estados en que la tira se copia punto por punto; y una barra chica -la tira se pinta antes de leer
                // el paso, y con filtro, escala o giro una de 956 no da tiempo- con todos)
                Caso(new CasoLetras { Lupa = lupa, Viaja = true }, Dos);
                Caso(new CasoLetras { Lupa = lupa, Viaja = true, W = 230, Letters = seis, Hay = seis, Actual = 'M' }, SinBicubica);
                Caso(new CasoLetras { Lupa = lupa, Viaja = true, W = 230, Letters = seis, Hay = "★AM►", Actual = '►', Tintes = 0 }, SinBicubica);
                Caso(new CasoLetras { Lupa = lupa, Viaja = true, Actual = '►', Hay = algunas }, Dos);
                Caso(new CasoLetras { Lupa = lupa, Viaja = true, Actual = 'B', Hay = sinEstrella, ForGpu = false, Fase = 0.7 }, Dos);
            }
            // el pulso del dibujo por procesador: varias fases
            foreach (double fase in new[] { 0.0, 0.31, 0.714, 1.07, 2.0, 3.3 })
            {
                Caso(new CasoLetras { ForGpu = false, Fase = fase, W = 996 }, Dos);
                Caso(new CasoLetras { ForGpu = false, Fase = fase, W = 996, Actual = '★' }, Dos);
            }
            // UNA MISMA BARRA, varias veces: los cuerpos de letra se eligen con el modo de letra del primero que la pinta y se guardan; la tira
            // se rehace al cambiar la letra, las disponibles, los datos o la medida; la lupa, tambien al moverse
            foreach (bool lupa in new[] { false, true })
                foreach (bool directa in new[] { false, true })
                    foreach (int orden in new[] { 0, 1 })
                    {
                        Type t = Letras_; Chivato ch;
                        object o = HazLetras(new CasoLetras { Lupa = lupa }, out ch);
                        string s = "letras-seguidas/" + (lupa ? "lupa/" : "") + (orden == 0 ? "unbit-primero" : "tarjeta-primero") + (directa ? "/directa" : "");
                        int paso = 0;
                        Action<Estado, bool, Action> pinta = delegate (Estado e, bool forGpu, Action antes)
                        {
                            if (antes != null) antes();
                            using (IDisposable b = (IDisposable)Pru.NuevoMapa(Medida(o, "Width") + 40, Medida(o, "Height") + 30, "Format32bppPArgb"))
                            using (Lz g = Prepara(b, e))
                            {
                                string ex = "";
                                try { g.Traslada(9, 4); PintaLetras(o, g, forGpu, directa); } catch (Exception x) { ex = " " + Exc(x); }
                                paso++;
                                Linea(s + "/" + paso, Puntos((s + "-" + paso), b) + "\t" + Queda(g) + ex);
                            }
                        };
                        Estado unbit = Estados[5], tarjeta = Estados[0], procesador = Estados[1], copia = Estados[3];
                        pinta(orden == 0 ? unbit : tarjeta, true, null);
                        pinta(orden == 0 ? tarjeta : unbit, true, null);
                        pinta(procesador, false, null);
                        pinta(tarjeta, true, delegate { t.GetField("cur", Todo).SetValue(o, 'R'); });
                        pinta(tarjeta, true, delegate { t.GetField("Counts", Todo).SetValue(o, HazCuentas(2, Todas)); });
                        pinta(copia, true, delegate { t.GetField("Tints", Todo).SetValue(o, null); });
                        pinta(tarjeta, true, delegate { ((HashSet<char>)t.GetField("Available", Todo).GetValue(o)).Remove('R'); });
                        pinta(tarjeta, true, delegate { PonSitio(o, 9, 4, 700, 46); });
                        pinta(procesador, false, delegate { t.GetProperty("CuentaCorta", Todo).SetValue(o, false); });
                        pinta(tarjeta, true, delegate { t.GetField("cur", Todo).SetValue(o, '★'); });
                        pinta(tarjeta, true, delegate { FieldInfo f = t.GetField("LupaGpu", Todo); if (f != null) f.SetValue(o, !lupa); });
                        pinta(tarjeta, true, delegate { t.GetField("Letters", Todo).SetValue(o, "★AR►"); t.GetField("cur", Todo).SetValue(o, 'A'); });
                        pinta(unbit, true, delegate { PonProp(o, "Font", new Fte("Courier New", 12f, 2)); PonSitio(o, 9, 4, 701, 46); });
                    }
            // el dibujo por procesador: OnPaint, con el Graphics como lo da el escritorio (su sitio en la matriz del HDC y su recorte sellados)
            foreach (char actual in new[] { 'L', '★', '►', 'B' })
                foreach (double fase in new[] { 0.0, 1.9 })
                {
                    Chivato ch;
                    object o = HazLetras(new CasoLetras { W = 996, Actual = actual, Fase = fase, ForGpu = false, Corta = false }, out ch);
                    using (IDisposable b = (IDisposable)Pru.NuevoMapa(1030, 70, "Format32bppPArgb"))
                    using (Lz g = Pru.Lienzo(b))
                    {
                        string ex = "";
                        g.Limpia(Color.FromArgb(255, 12, 18, 40));
                        g.Traslada(14, 11); g.Recorta(new Rectangle(0, 0, 996, 40)); g.Sella();
                        try { PorOnPaint(o, g, new Rectangle(0, 0, 996, 40), delegate { g.Suavizado = ModoSuave.AntiAlias; }); }   // (la barra de 996x40 con letras: XP le deja el suavizado)
                        catch (Exception x) { ex = " " + Exc(x); }
                        string s = "letras-onpaint/" + (actual == '★' ? "TOP" : actual == '►' ? "VIDEO" : actual.ToString()) + "/fase" + fase.ToString("R", ci);
                        Linea(s, Puntos(s, b) + "\t" + Queda(g) + ex);
                    }
                }
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            string solo = null; for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "solo") solo = a[i + 1];
            Linea("inicio", "tip=" + Fmt.Tipografico.Horizontal + "/" + Fmt.Tipografico.Banderas);
            if (solo == null || solo == "paginas") BarraPaginas();
            if (solo == null || solo == "letras") BarraLetras();
            Linea("fin", "tip=" + Fmt.Tipografico.Horizontal + "/" + Fmt.Tipografico.Banderas);
            Console.Error.WriteLine(n + " lineas; " + directas + " llamadas directas a la traduccion; " + pintas + " por Pinta (el OnPaint de XP); " + reintentos + " reintentos por el reloj; " + sinTiempo + " sin conseguir el instante");
            return sinTiempo == 0 ? 0 : 3;
        }
    }
}

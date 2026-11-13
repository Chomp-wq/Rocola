// ARNES DEL GRUPO «REPOSO» (port nativo, tanda 2 del dibujo, 8-11-2026): lo que dibujan EstadoReposoGpu.cs (el panel del estado extendido y
// su reloj), TextoMovilGpu.cs (las cintas del texto que se desliza y el texto que se mueve) y FranjaLigeraGpu.cs (el recuadro de la franja),
// con datos fijos, la huella de cada mapa y lo que devuelve cada llamada. Se compila con el programa de una carpeta Xp (VistasReposo.csproj):
// la salida de la referencia congelada y la de la copia de trabajo tienen que ser iguales linea por linea.
//   - En la referencia las llamadas son las de XP (Graphics, Bitmap, Image, Font, Brush, StringFormat) y van a GDI+ (la capa congelada); en
//     la traduccion, a la traducida (el lienzo y su estado, la Letra, un SKPaint, el FormatoTexto, los SKBitmap). Todo por reflexion (la
//     clase Xp, abajo). El estado del lienzo del que llama va de ocho maneras (suavizado, los modos de letra, copia, medio pixel, traslacion
//     entera y con decimales, escala, giro y recorte), con y sin la captura de las cintas puesta.
//   - Muchos casos salen dos veces (la segunda, «directo»): en la traduccion las dos van a la traducida; en la referencia, a la de XP.
//   - (Oleada 8b, 13-11-2026) El arnes ya no nombra la capa de GDI+ ni la de WinForms: los mapas, el lienzo, las letras, los formatos, los
//     pinceles y los rotulos van por Pruebas/Comun (Pru, Lz, Fte, Fmt: la capa en la referencia congelada y lo nativo en el programa nuevo).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasReposo.dll [vuelca CARPETA]
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Banco;
using Rocola;
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
            object b = Pru.Aprecia(o, "Format32bppPArgb");
            SKBitmap s = Pru.Sk(b); byte[] px = new byte[s.Width * s.Height * 4];
            byte* p = (byte*)s.GetPixels();
            for (int y = 0; y < s.Height; y++) for (int x = 0; x < s.Width * 4; x++) px[y * s.Width * 4 + x] = p[y * s.RowBytes + x];
            byte[] sub = Pru.SinPremultiplicar(b);
            if (carpeta != null) using (SKData png = s.Encode(SKEncodedImageFormat.Png, 100)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), png.ToArray());
            return s.Width + "x" + s.Height + "\t" + Huella(px) + "\t" + Huella(sub);
        }

        // ---- el estado del Graphics del que llama
        private sealed class Estado { public string Nombre; public ModoSuave S; public ModoLetra T; public ModoCopia C; public ModoPixel P; public int Trans; public bool Recorte; public bool Medio { get { return P == ModoPixel.Half || P == ModoPixel.HighQuality; } } }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "xp", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "sin-suavizar", S = ModoSuave.None, T = ModoLetra.SystemDefault, C = ModoCopia.SourceOver, P = ModoPixel.Default },
            new Estado { Nombre = "copia-medio-traslada", S = ModoSuave.HighQuality, T = ModoLetra.AntiAlias, C = ModoCopia.SourceCopy, P = ModoPixel.Half, Trans = 1 },
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
            else if (e.Trans == 4) g.Traslada(0, -40); else if (e.Trans == 5) g.Traslada(0.5f, 0f);
            if (e.Recorte) g.Recorta(new RectangleF(30.5f, 8, 300, 50));
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
            Fmt t = Fmt.Tipografico;
            return " tipografico=" + t.Horizontal + "/" + t.Vertical + "/" + t.Banderas + "/" + t.Corte;
        }

        // ---- una funcion del programa por su nombre y el tipo de su primer parametro; null si no la hay
        private static MethodInfo Nativa(string nombre, Type primero)
        {
            foreach (MethodInfo m in typeof(Form1).GetMethods(Todo)) if (m.Name == nombre && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == primero) return m;
            return null;
        }
        // el lienzo y su estado (suave, letra, copia, medio)
        private static object[] EstadoDe(Lz g)
        {
            object[] e = g.Estado();   // (el lienzo, suave, letra, copia, filtro, medio)
            return new[] { e[0], e[1], e[2], e[3], e[5] };
        }

        // ---- LAS FIRMAS DE XP, POR REFLEXION. Cada una llama a la de XP en la referencia congelada (con el Graphics, la Font, el Brush, el
        // StringFormat y los Bitmap de su capa) y, en el programa nuevo, a la traducida: el lienzo y su estado, la Letra, el pincel en un
        // SKPaint (el del arnes: Pru.Brocha y los demas), el MISMO FormatoTexto y los puntos de cada mapa (los mismos). Una excepcion de dentro
        // sale tal cual (no envuelta por la reflexion), como de una llamada directa.
        private static class Xp
        {
            private static object Llama(MethodInfo m, object o, object[] a)
            {
                try { return m.Invoke(o, a); }
                catch (TargetInvocationException x) when (x.InnerException != null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(x.InnerException).Throw(); throw; }
            }
            private static object O(Fte f) { return f != null ? f.Obj : null; }
            private static object O(Fmt sf) { return sf != null ? sf.Obj : null; }

            internal static void PintaTextoMovil(Lz g, string text, Fte f, object brush, RectangleF rect, Fmt sf)
            {
                if (Pru.Capa) { Llama(Nativa("PintaTextoMovil", Pru.TLienzo), null, new object[] { g.Objeto, text, O(f), brush, rect, O(sf) }); return; }
                object[] es = EstadoDe(g);
                Llama(Nativa("PintaTextoMovil", typeof(SKCanvas)), null, new object[] { es[0], es[2], es[3], es[4], text, O(f), brush, rect, O(sf) });
            }
            internal static bool TextoConCinta(Lz g, string text, Fte f, Color color, RectangleF rect, Fmt sf)
            {
                if (Pru.Capa) return (bool)Llama(Nativa("TextoConCinta", Pru.TLienzo), null, new object[] { g.Objeto, text, O(f), color, rect, O(sf) });
                object[] es = EstadoDe(g);
                return (bool)Llama(Nativa("TextoConCinta", typeof(SKCanvas)), null, new object[] { es[0], es[2], text, O(f), color, rect, O(sf) });
            }
            internal static void PintaArtistaCompacto(object form, Lz g)
            {
                if (Pru.Capa) { Llama(Nativa("PintaArtistaCompacto", Pru.TLienzo), form, new object[] { g.Objeto }); return; }
                object[] es = EstadoDe(g);
                Llama(Nativa("PintaArtistaCompacto", typeof(SKCanvas)), form, new object[] { es[0], es[2], es[3], es[4] });
            }
            // FuenteTituloReposo: la Font de XP (la referencia) o la Letra de LetraTituloReposo (el programa nuevo)
            internal static Fte FuenteTituloReposo(int w, int h)
            {
                MethodInfo xp = typeof(Form1).GetMethod("FuenteTituloReposo", Todo);
                if (xp != null) return Fte.De(Llama(xp, null, new object[] { w, h }));
                return Fte.De(Llama(typeof(Form1).GetMethod("LetraTituloReposo", Todo), null, new object[] { w, h }));
            }
            // (las portadas van en un arreglo del tipo que pida cada lado: Image[] o SKBitmap[]; el mapa que devuelve, si es otro que el que
            // se le dio, lleva en el programa nuevo el formato con que XP lo crea, Format32bppPArgb)
            internal static object RenderIdlePanelEn(object bitmap, int w, int h, string title, string artist, string time,
                string[] qt, string[] qa, object[] covers, int count, Color accent, bool transparent, int badgeKind, bool dozen,
                ReposoGrande extra, bool opaque, out int progressStart)
            {
                MethodInfo m = Nativa("RenderIdlePanelEn", Pru.TMapa);
                Array mapas = null;
                if (covers != null) { mapas = Array.CreateInstance(m.GetParameters()[8].ParameterType.GetElementType(), covers.Length); for (int i = 0; i < covers.Length; i++) mapas.SetValue(covers[i], i); }
                object[] a = { bitmap, w, h, title, artist, time, qt, qa, mapas, count, accent, transparent, badgeKind, dozen, extra, opaque, 0 };
                object r = Llama(m, null, a);
                progressStart = (int)a[16];
                if (!Pru.Capa && !ReferenceEquals(r, bitmap)) Pru.DeSk((SKBitmap)r, "Format32bppPArgb");
                return r;
            }
            internal static void CopiaRegionReposo(object source, Rectangle area, object target, Point position)
            {
                Llama(Nativa("CopiaRegionReposo", Pru.TMapa), null, new object[] { source, area, target, position });
            }
            internal static int UpdateIdleClock(object panel, object underlay, string time)
            {
                return (int)Llama(Nativa("UpdateIdleClock", Pru.TMapa), null, new object[] { panel, underlay, time });
            }
            internal static int UpdateIdleClockRegion(object panel, object underlay, string time, int previousRight, out Rectangle dirty)
            {
                MethodInfo m = Nativa("UpdateIdleClockRegion", Pru.TMapa);
                object[] a = { panel, underlay, time, previousRight, Rectangle.Empty };
                int r = (int)Llama(m, null, a);
                dirty = (Rectangle)a[4]; return r;
            }
        }

        // ---- una portada de prueba: puntos al azar con semilla fija (opaca o con alfa), escritos sin premultiplicar (como con LockBits de
        // Format32bppArgb en XP)
        private static object Portada(int w, int h, int semilla, bool alfa)
        {
            object b = Pru.NuevoMapa(w, h, "Format32bppArgb");
            uint[] d = new uint[w * h];
            Random r = new Random(semilla);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = alfa ? (x * 7 + y * 3 + semilla * 11) & 255 : 255;
                    d[y * w + x] = ((uint)a << 24) | (uint)((r.Next(256) << 16) | (((x * 255) / Math.Max(1, w - 1)) << 8) | ((y * 255) / Math.Max(1, h - 1)));
                }
            Pru.EscribePuntos(b, d);
            return b;
        }

        // ---- EL PANEL DE REPOSO (RenderIdlePanelEn), por la firma de XP
        private sealed class Panel
        {
            public string Titulo, Artista, Hora; public int N; public bool Doce; public int Insignia; public ReposoGrande Extra; public bool Transparente, Opaco; public Color Acento; public bool ConPortadas, Tramada;
        }
        private static ReposoGrande Extra(string genero, Color c, int[] minutos) { ReposoGrande x = new ReposoGrande(); x.Genero = genero; x.GeneroColor = c; x.Minutos = minutos; return x; }
        private static string ExtraDe(ReposoGrande x) { return x == null ? "" : " fuera=" + x.TituloFuera + " caja=" + R(x.TituloCaja) + " genero=" + R(x.GeneroCaja); }
        private static object[] Portadas(int n, bool con, List<object> todas)
        {
            object[] c = new object[n];
            if (!con) return c;
            for (int i = 0; i < n; i++)
            {
                if (i % 4 == 1) continue;   // algunas sin portada (se pinta el cuadrito del color)
                object b = i % 4 == 0 ? Portada(256, 256, i + 1, false) : i % 4 == 2 ? Portada(64, 48, i + 1, true) : Portada(600, 600, i + 1, false);
                todas.Add(b); c[i] = b;
            }
            return c;
        }
        private static object HazPanel(string nombre, object reusa, int w, int h, Panel p, bool cinta, bool directo, List<object> todas)
        {
            string[] qt = new string[p.N], qa = new string[p.N];
            for (int i = 0; i < p.N; i++)
            {
                qt[i] = i % 5 == 3 ? null : i % 5 == 1 ? "02 Ángel de amor (versión ñandú) & más, la " + (i + 1) + "ª de la cola, que no cabe" : "Cancion de relleno " + (i + 1);
                qa[i] = i % 4 == 2 ? null : i % 4 == 3 ? "Artista Muy Largo con Nombre y Apellidos y su Orquesta Tipica" : "Artista de relleno";
            }
            object[] covers = Portadas(p.N, p.ConPortadas, todas);
            Form1.InterfazTramada = p.Tramada;
            CintasTexto c = cinta ? Captura0() : null;
            int comienzo; object r;
            try
            {
                if (directo && !Pru.Capa) directas++;   // (la traducida se llama igual con «directo» o sin el: no hay otra)
                r = Xp.RenderIdlePanelEn(reusa, w, h, p.Titulo, p.Artista, p.Hora, qt, qa, covers, p.N, p.Acento, p.Transparente, p.Insignia, p.Doce, p.Extra, p.Opaco, out comienzo);
            }
            finally { Captura1(c); }
            Linea(nombre, Puntos(nombre, r) + "\tcomienzo=" + comienzo + " mismo=" + ReferenceEquals(r, reusa) + " formato=" + Pru.Formato(r) + " flags=" + Pru.Marcas(r) + ExtraDe(p.Extra) + Cintas(nombre, c) + Tipografico());
            if (c != null) c.Dispose();
            return r;
        }

        // ---- EL TEXTO QUE SE MUEVE (PintaTextoMovil), LA CINTA (TextoConCinta) Y LA DE LA VISTA PREVIA (CintaVistaPrevia)
        private static readonly string[] Textos = { "Cancion de relleno 1", "02 Ángel de amor (versión ñandú) & más, con un titulo larguisimo que no cabe", "", "Disco Largo" };
        // (las banderas, por su numero: NoWrap 4096, LineLimit 8192)
        private static Fmt Formato(int k)
        {
            switch (k % 4)
            {
                case 0: { Fmt f = new Fmt(); f.Corte = ModoCorte.EllipsisCharacter; f.Banderas = 4096; return f; }
                case 1: { Fmt f = new Fmt(); f.Corte = ModoCorte.EllipsisWord; f.Banderas = 8192; f.Vertical = ModoAlinea.Center; return f; }
                case 2: { Fmt f = new Fmt(); f.Horizontal = ModoAlinea.Far; f.Vertical = ModoAlinea.Far; return f; }
                default: return Fmt.Tipografico;   // el compartido (PintaTextoMovil lo copia para la cinta; no lo cambia)
            }
        }
        // (el pincel: un Brush en la referencia, un SKPaint en el programa nuevo; el de textura, con el medio pixel del lienzo en que se pinta)
        private static object Pincel(int k, List<object> todas, bool medio)
        {
            switch (k % 5)
            {
                case 0: return Pru.Brocha(Color.FromArgb(240, 245, 255));
                case 1: return Pru.Brocha(Color.FromArgb(128, 157, 179, 209));
                case 2: return Pru.BrochaDegradado(new RectangleF(0, 0, 300, 40), Color.FromArgb(255, 250, 200, 40), Color.FromArgb(180, 40, 90, 230), 0f);
                case 3: { object t = Portada(8, 8, 77, true); todas.Add(t); return Pru.BrochaTextura(t, medio); }
                default: return null;
            }
        }
        private static Fte Letra(int k)   // (el estilo: 1 negrita, 2 cursiva)
        {
            switch (k % 3)
            {
                case 0: return new Fte("Trebuchet MS", 18.846f, 1, "Pixel");
                case 1: return new Fte("Microsoft Sans Serif", 17.25f, 1);
                default: return new Fte("Tahoma", 11.2f, 1 | 2, "Pixel");
            }
        }
        private static readonly RectangleF[] Cajas = { new RectangleF(20.5f, 10.25f, 300, 40), new RectangleF(0, 0, 60, 20), new RectangleF(10, 5, 11.5f, 7.5f), new RectangleF(40, 30, 900, 19) };

        private static void TextoMovil(string nombre, Estado e, string texto, int k, bool cinta, bool directo, List<object> todas)
        {
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(700, 120, "Format32bppPArgb"))
            using (Fte f = Letra(k))
            {
                object br = Pincel(k, todas, e.Medio); Fmt sf = Formato(k / 2); RectangleF caja = Cajas[(k / 3) % Cajas.Length];
                CintasTexto c = cinta ? Captura0() : null;
                string ex = "";
                try
                {
                    using (Lz g = Prepara(b, e))
                    {
                        if (directo && !Pru.Capa) directas++;   // (la traducida se llama igual con «directo» o sin el)
                        Xp.PintaTextoMovil(g, texto, f, br, caja, sf);
                    }
                }
                catch (Exception x) { ex = " EXC:" + (x is TargetInvocationException && x.InnerException != null ? x.InnerException : x).GetType().Name; }
                finally { Captura1(c); }
                Linea(nombre, Puntos(nombre, b) + ex + Cintas(nombre, c));
                if (c != null) c.Dispose();
                Pru.SueltaBrocha(br);
            }
        }
        private static void ConCinta(string nombre, Estado e, string texto, int k, bool cinta, bool vista, bool directo)
        {
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(700, 120, "Format32bppPArgb"))
            using (Fte f = Letra(k))
            {
                Fmt sf = Formato(k); RectangleF caja = Cajas[k % Cajas.Length]; Color col = k % 2 == 0 ? Color.FromArgb(240, 245, 255) : Color.FromArgb(200, 125, 235, 165);
                CintasTexto c = cinta ? Captura0() : null;
                string ex = ""; bool r = false;
                try
                {
                    using (Lz g = Prepara(b, e))
                    {
                        MethodInfo m = directo ? Nativa(vista ? "CintaVistaPrevia" : "TextoConCinta", typeof(SKCanvas)) : null;
                        // la firma de XP de CintaVistaPrevia (con un Graphics) solo esta en la referencia: en la traduccion la llamada de XP es la
                        // traducida con el estado del lienzo
                        MethodInfo xpVista = vista && m == null && Pru.Capa ? Nativa("CintaVistaPrevia", Pru.TLienzo) : null;
                        if (vista && m == null && xpVista == null) m = Nativa("CintaVistaPrevia", typeof(SKCanvas));
                        if (m == null) r = vista ? (bool)xpVista.Invoke(null, new object[] { g.Objeto, texto, f.Obj, col, Rectangle.Round(caja) }) : Xp.TextoConCinta(g, texto, f, col, caja, sf);
                        else
                        {
                            directas++;
                            object[] es = EstadoDe(g);
                            r = vista ? (bool)m.Invoke(null, new object[] { es[0], es[2], texto, f.Obj, col, Rectangle.Round(caja) })
                                      : (bool)m.Invoke(null, new object[] { es[0], es[2], texto, f.Obj, col, caja, sf.Obj });
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
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(1200, 300, "Format32bppPArgb"))
            using (Lz g = Pru.Lienzo(b))
            using (Fte f = new Fte("Trebuchet MS", 30f, 1, "Pixel"))
            using (Fte fg = new Fte("Trebuchet MS", 90f, 1, "Pixel"))
            {
                Fmt sf = new Fmt();
                g.Letra = ModoLetra.AntiAliasGridFit;
                sf.Banderas = 4096; sf.Vertical = ModoAlinea.Center;   // (NoWrap)
                CintasTexto c = new CintasTexto();
                string largo = "Un titulo larguisimo que no cabe en su caja";
                StringBuilder sb = new StringBuilder();
                for (int vuelta = 0; vuelta < 3; vuelta++)
                {
                    c.Begin(); Captura.SetValue(null, c);
                    sb.Append(" v" + vuelta + ":");
                    sb.Append(Xp.TextoConCinta(g, largo + " A", f, Color.White, new RectangleF(10, 10, 200, 40), sf) ? "1" : "0");
                    if (vuelta == 1) sb.Append(Xp.TextoConCinta(g, largo + " B", f, Color.White, new RectangleF(10, 60, 200, 40), sf) ? "1" : "0");
                    sb.Append(Xp.TextoConCinta(g, largo + " A", f, Color.White, new RectangleF(10, 10, 200, 40), sf) ? "1" : "0");
                    sb.Append(Xp.TextoConCinta(g, largo + largo + largo + largo, fg, Color.FromArgb(200, 255, 230, 120), new RectangleF(5, 100, 400, 100), sf) ? "1" : "0");
                    sb.Append(Xp.TextoConCinta(g, largo, f, Color.White, new RectangleF(5, 0, 300, 257), sf) ? "1" : "0");
                    if (vuelta == 2) for (int i = 0; i < 50; i++) sb.Append(Xp.TextoConCinta(g, largo + " " + i, f, Color.White, new RectangleF(10, 10, 100, 40), sf) ? "1" : "0");
                    Captura.SetValue(null, null); c.End();
                    sb.Append(" n=" + c.Items.Count + " ver=" + c.Version);
                }
                Linea(nombre, sb.ToString() + Cintas(nombre, c));
                c.Dispose();
            }
        }

        // (rotulos-y-letras) EL ROTULO: en la referencia, el Label de la capa; en la traduccion, RotuloVista (Nativo/RotuloVista.cs), con sus
        // propiedades por su nombre (una letra del arnes, Fte, va por la suya; la alineacion, por su numero: el de ContentAlignment, que es el
        // de AlineaTexto)
        private static readonly Type tRotulo = typeof(Form1).Assembly.GetType("Rocola.RotuloVista");
        private static object NuevoRotulo() { return tRotulo != null ? Activator.CreateInstance(tRotulo) : Pru.Rotulo(); }
        private static void PonP(object o, string p, object v)
        {
            PropertyInfo pi = o.GetType().GetProperty(p, BindingFlags.Public | BindingFlags.Instance);
            if (v is Fte) v = ((Fte)v).Obj;
            else if (v is int && pi.PropertyType.IsEnum) v = Enum.ToObject(pi.PropertyType, (int)v);
            pi.SetValue(o, v);
        }
        // ---- EL ARTISTA DE LA FRANJA (PintaArtistaCompacto, privada de Form1: en una Form1 sin construir, con la etiqueta lblNow)
        private static void Artista(string nombre, Estado e, string texto, int al, int ancho, bool mejorada, bool cinta, bool directo)
        {
            object form = RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            object l = NuevoRotulo(); PonP(l, "Text", texto); PonP(l, "Font", new Fte("Microsoft Sans Serif", 11.5f, 1)); PonP(l, "ForeColor", Color.FromArgb(225, 236, 255)); PonP(l, "TextAlign", al);
            PonP(l, "Bounds", new Rectangle(14, 40, ancho, 26));
            typeof(Form1).GetField("lblNow", Todo).SetValue(form, l);
            if (mejorada) { object m = NuevoRotulo(); PonP(m, "Visible", true); PonP(m, "Bounds", new Rectangle(14 + ancho / 2, 40, 60, 20)); typeof(Form1).GetField("lblMejorada", Todo).SetValue(form, m); }
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(700, 120, "Format32bppPArgb"))
            {
                CintasTexto c = cinta ? Captura0() : null;
                string ex = "";
                try
                {
                    using (Lz g = Prepara(b, e))
                    {
                        MethodInfo m = directo ? Nativa("PintaArtistaCompacto", typeof(SKCanvas)) : null;
                        if (m == null) Xp.PintaArtistaCompacto(form, g);
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
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(400, 100, "Format32bppPArgb"))
            using (Lz g = Prepara(b, e))
            using (Fte f = new Fte("Microsoft Sans Serif", 14f, 1))
            {
                Color rojo = Color.FromArgb(150, 255, 40, 40), verde = Color.FromArgb(150, 40, 255, 40);
                StringBuilder sb = new StringBuilder();
                MethodInfo m = directo ? Nativa("EmpiezaRecuadro", typeof(SKCanvas)) : null;
                // la firma de XP de EmpiezaRecuadro (con un Graphics) solo esta en la referencia: en la traduccion la llamada de XP es la traducida
                // con el recorte puesto antes en el lienzo y su estado
                MethodInfo xp = m == null && Pru.Capa ? Nativa("EmpiezaRecuadro", Pru.TLienzo) : null;
                if (m == null && xp != null) xp.Invoke(null, new object[] { g.Objeto, zona });
                else
                {
                    // directo: como lo hace Form1.Video.cs traducido, el lienzo nuevo sin recorte; el resto sigue por el lienzo de prueba, que se
                    // entera del recorte al ponerselo aqui
                    if (m != null) directas++; else m = Nativa("EmpiezaRecuadro", typeof(SKCanvas));
                    g.Recorta(zona); object[] es = EstadoDe(g); m.Invoke(null, new object[] { es[0], es[1], es[4], zona });
                }
                sb.Append(" recuadro=" + Puntos(nombre + "-0", b).Replace('\t', ' ') + " recorte=" + R(g.LimitesRecorte));
                g.Rellena(rojo, 0, 0, 400, 100);
                g.Escribe("Texto dentro del recuadro", f, verde, new RectangleF(0, 0, 400, 100), null);
                sb.Append(" dentro=" + Puntos(nombre + "-1", b).Replace('\t', ' '));
                g.Recorta(new Rectangle(5, 60, 120, 30));
                g.Rellena(verde, 0, 0, 400, 100);
                sb.Append(" otro=" + Puntos(nombre + "-2", b).Replace('\t', ' ') + " recorte=" + R(g.LimitesRecorte));
                object st = g.Guarda();
                g.Recorta(new Rectangle(200, 0, 50, 100));
                g.Rellena(rojo, 0, 0, 400, 100);
                g.Restaura(st);
                g.Rellena(rojo, 0, 50, 400, 10);
                sb.Append(" guardado=" + Puntos(nombre + "-3", b).Replace('\t', ' ') + " recorte=" + R(g.LimitesRecorte));
                g.SinRecorte();
                g.Rellena(verde, 0, 95, 400, 5);
                sb.Append(" sin=" + Puntos(nombre + "-4", b).Replace('\t', ' ') + " recorte=" + R(g.LimitesRecorte));
                Linea(nombre, sb.ToString());
            }
        }

        // ---- EL RELOJ DEL PANEL (DrawIdleClock), directo: en la traduccion con el lienzo y su modo de letra; en la referencia, la de XP con
        // un Graphics como lo dejan los que la llaman (AntiAlias, AntiAliasGridFit) y con el recorte de UpdateIdleClockRegion
        private static void Reloj(string nombre, int w, int h, string hora, bool recorte)
        {
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(w, h, "Format32bppPArgb"))
            using (Lz g = Pru.Lienzo(b))
            {
                g.Limpia(Color.FromArgb(200, 12, 19, 42));
                if (recorte) g.Recorta(new Rectangle(200, h - 34, 130, 34));
                g.Suavizado = ModoSuave.AntiAlias; g.Letra = ModoLetra.AntiAliasGridFit;
                MethodInfo m = Nativa("DrawIdleClock", typeof(SKCanvas));
                int r;
                if (m == null) r = (int)Nativa("DrawIdleClock", Pru.TLienzo).Invoke(null, new object[] { g.Objeto, w, h, hora });
                else { directas++; object[] es = EstadoDe(g); r = (int)m.Invoke(null, new object[] { es[0], es[2], w, h, hora }); }
                Linea(nombre, Puntos(nombre, b) + "\tderecha=" + r);
            }
        }

        public static int Main(string[] a)
        {
            for (int i = 0; i + 1 < a.Length; i += 2) if (a[i] == "vuelca") { carpeta = a[i + 1]; System.IO.Directory.CreateDirectory(carpeta); }
            List<object> todas = new List<object>();
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
                        Pru.Suelta(HazPanel("panel-" + w + "x" + h + "-" + j + (cinta ? "-cinta" : ""), null, w, h, paneles[j], cinta, false, todas));
                // el mapa que se vuelve a usar: del mismo tamano (el mismo objeto) y de otro (uno nuevo)
                object p1 = HazPanel("panel-reusa-" + (k++), null, w, h, paneles[0], false, false, todas);
                object p2 = HazPanel("panel-reusa-" + (k++), p1, w, h, paneles[1], true, false, todas);
                object p3 = HazPanel("panel-reusa-" + (k++), p2, w, h + 1, paneles[5], false, false, todas);
                if (!ReferenceEquals(p2, p1)) Pru.Suelta(p2);
                Pru.Suelta(p1); Pru.Suelta(p3);
                // la traducida, directo
                for (int j = 0; j < paneles.Length; j++)
                    Pru.Suelta(HazPanel("panel-directo-" + w + "x" + h + "-" + j, null, w, h, paneles[j], j % 2 == 1, true, todas));
            }
            Form1.InterfazTramada = true;

            // LA LETRA DEL TITULO (FuenteTituloReposo, la que pide SuenaVivoGpu.cs para el titulo que se desliza): sus datos en cada pantalla
            foreach (int[] pa in pantallas)
                using (Fte f = Xp.FuenteTituloReposo(pa[0], GpuPanel.IdlePanelHeight(pa[1])))
                    Linea("letra-titulo-" + pa[0] + "x" + pa[1], f.Nombre + "\t" + R(f.Tamano) + "\t" + f.Estilo + "\t" + f.Unidad + "\t" + f.Alto + "\t" + R(f.AltoRenglon) + "\t" + R(f.Puntos));

            // EL RELOJ: la franja de abajo copiada a su mapa, el reloj entero y por trozos (otra hora, mas larga, vacia, con el borde de antes
            // mas a la derecha o en cero), y trozos sueltos copiados a otro sitio
            k = 0;
            foreach (int[] pa in pantallas)
            {
                int w = pa[0], ph = GpuPanel.IdlePanelHeight(pa[1]);
                int comienzo;
                using (IDisposable panel = (IDisposable)Xp.RenderIdlePanelEn(null, w, ph, "Titulo de relleno", "Artista de relleno", "", new string[3], new string[3], new object[3], 3,
                    Color.FromArgb(120, 160, 255), false, 0, false, null, false, out comienzo))
                using (IDisposable debajo = (IDisposable)Pru.NuevoMapa(w, 34, "Format32bppPArgb"))
                {
                    Xp.CopiaRegionReposo(panel, new Rectangle(0, ph - 34, w, 34), debajo, Point.Empty);
                    Linea("reloj-debajo-" + w, Puntos("reloj-debajo-" + w, debajo));
                    int ultimo = Xp.UpdateIdleClock(panel, debajo, "0:00 / 0:00");
                    Linea("reloj-entero-" + w, Puntos("reloj-entero-" + w, panel) + "\tderecha=" + ultimo);
                    foreach (string t in new[] { "0:01 / 3:00", "10:59 / 3:00:00", "", "9:59 / 9:59", null, "1:00 / 2:00" })
                    {
                        Rectangle zona;
                        int r = Xp.UpdateIdleClockRegion(panel, debajo, t, ultimo, out zona);
                        string nom = "reloj-trozo-" + w + "-" + (k++);
                        Linea(nom, Puntos(nom, panel) + "\tderecha=" + r + " zona=" + zona);
                        ultimo = r;
                    }
                    foreach (int antes in new[] { 0, w })
                    {
                        Rectangle zona;
                        int r = Xp.UpdateIdleClockRegion(panel, debajo, "3:33 / 4:44", antes, out zona);
                        string nom = "reloj-borde-" + w + "-" + (k++);
                        Linea(nom, Puntos(nom, panel) + "\tderecha=" + r + " zona=" + zona);
                    }
                    using (IDisposable otro = (IDisposable)Portada(97, 53, 5, true))
                    {
                        Xp.CopiaRegionReposo(panel, new Rectangle(5, 3, 17, 9), otro, new Point(2, 4));
                        Xp.CopiaRegionReposo(debajo, new Rectangle(w - 40, 0, 40, 34), otro, new Point(57, 19));
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
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(300, 60, "Format32bppPArgb"))
            using (Lz g = Pru.Lienzo(b))
            using (Fte f = Letra(0))
            {
                object br = Pru.Brocha(Color.White);
                CintasTexto c = Captura0(); string ex = "";
                try { Xp.PintaTextoMovil(g, Textos[1], f, br, Cajas[0], null); } catch (Exception x) { ex = " EXC:" + x.GetType().Name; } finally { Captura1(c); }
                Linea("movil-nulo", Puntos("movil-nulo", b) + ex + Cintas("movil-nulo", c)); c.Dispose();
                Pru.SueltaBrocha(br);
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
            int[] als = { 16, 1, 1024, 32 };   // (los numeros de ContentAlignment: MiddleLeft, TopLeft, BottomRight y MiddleCenter)
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
            foreach (object b in todas) Pru.Suelta(b);
            Console.Error.WriteLine("reposo: " + n + " lineas, " + directas + " llamadas directas a la traducida");
            return 0;
        }
    }
}

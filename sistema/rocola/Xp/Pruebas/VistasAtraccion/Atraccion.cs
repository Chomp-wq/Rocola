// ARNES DEL GRUPO «ATRACCION» (port nativo, tanda 2 del dibujo, 8-11-2026): lo que dibuja AtraccionGpu.cs (la ficha del escaparate, los
// mensajes, la lista de lo mas pedido esta noche y sus piezas) con datos fijos, la huella de cada mapa y lo que devuelve cada llamada. Se
// compila con el programa de una carpeta Xp (VistasAtraccion.csproj): la salida de la referencia congelada y la de la copia de trabajo tienen
// que ser iguales linea por linea.
//   - Los hornos (HorneaFichaVitrina, HorneaMensajes y HorneaTop) se llaman igual en las dos (solo cambia el tipo de los mapas) y se compara
//     su mapa, tal cual y como lo sube la tarjeta (LockBits Format32bppArgb: sin premultiplicar), y lo que devuelven.
//   - Sus piezas (Letra, Renglon, EscalaQueCabe, AcortaPorPalabras, AcortaCita, DisponPastillas, AnchoPastilla, Pastilla, PastillaGenero,
//     TextoCentrado, Espaciado, EspaciadoEntero, Tinta, Luna y Tapa) se llaman por reflexion: en la referencia con el Graphics, la Font, el
//     Brush y el StringFormat de la capa que imita a GDI+ (la congelada); en la traduccion con el lienzo y su estado (suavizado, modo de
//     letra, filtro), la Letra, un SKPaint del color, el MISMO FormatoTexto y los mismos puntos, como las llaman los hornos traducidos. Con
//     varios estados del lienzo (los tres de XP y otros: los seis modos de letra, suavizado, filtros, traslacion entera y con decimales,
//     escala, giro y recorte), letras en pixeles, en puntos y con estilos, y el tipografico compartido de fabrica y cambiado como lo deja
//     EstadoReposoGpu.cs (Center, Center y NoWrap).
//   - (Oleada 8b, 13-11-2026) El arnes ya no nombra la capa de GDI+: los mapas, el lienzo, las letras, los formatos y los pinceles van por
//     Pruebas/Comun (Pru, Lz, Fte, Fmt: la capa en la referencia congelada y lo nativo en el programa nuevo).
//   ROCOLA_PANTALLA=fuera:64x64 dotnet bin/Release/net10.0/VistasAtraccion.dll [vuelca CARPETA]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using Banco;
using Rocola;
using SkiaSharp;

namespace BancoAtraccion
{
    internal static class Atraccion
    {
        private static readonly CultureInfo ci = CultureInfo.InvariantCulture;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private static string carpeta;
        private static int n, directas, mapas;   // directas: las llamadas a una traducida con su lienzo y su estado (en la referencia, ninguna)
        private static readonly HashSet<string> distintos = new HashSet<string>();
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
            string h = Huella(px); mapas++; distintos.Add(h);
            return s.Width + "x" + s.Height + "\t" + h + "\t" + Huella(sub);
        }

        // ---- el estado del Graphics del que llama: los tres de XP (la ficha, los mensajes y la lista) y otros
        private sealed class Estado { public string Nombre; public ModoSuave S; public ModoLetra T; public ModoFiltro I; public int Trans; public bool Recorte; }
        private static readonly Estado[] Estados = {
            new Estado { Nombre = "ficha", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, I = ModoFiltro.Bilinear },
            new Estado { Nombre = "mensajes", S = ModoSuave.None, T = ModoLetra.AntiAliasGridFit, I = ModoFiltro.Bilinear },
            new Estado { Nombre = "lista", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAliasGridFit, I = ModoFiltro.HighQualityBilinear },
            new Estado { Nombre = "sistema-traslada", S = ModoSuave.None, T = ModoLetra.SystemDefault, I = ModoFiltro.Bilinear, Trans = 1 },
            new Estado { Nombre = "unbitajustada-escala", S = ModoSuave.HighQuality, T = ModoLetra.SingleBitPerPixelGridFit, I = ModoFiltro.NearestNeighbor, Trans = 2 },
            new Estado { Nombre = "unbit-gira", S = ModoSuave.AntiAlias, T = ModoLetra.SingleBitPerPixel, I = ModoFiltro.HighQualityBicubic, Trans = 3 },
            new Estado { Nombre = "suave-decimales-recorte", S = ModoSuave.AntiAlias, T = ModoLetra.AntiAlias, I = ModoFiltro.Bicubic, Trans = 4, Recorte = true },
            new Estado { Nombre = "cleartype-recorte", S = ModoSuave.None, T = ModoLetra.ClearTypeGridFit, I = ModoFiltro.Bilinear, Recorte = true },
        };
        // un mapa con fondo (para ver el alfa) y el Graphics con ese estado (la copia y el medio pixel, los de fabrica, como en los hornos)
        private static Lz Prepara(object b, Estado e, bool recorte)
        {
            Lz g = Pru.Lienzo(b); int bw = Pru.Sk(b).Width, bh = Pru.Sk(b).Height;
            g.Limpia(Color.FromArgb(120, 30, 60, 90));
            g.Rellena(Color.FromArgb(200, 250, 20, 60), 0, 20, bw, 9);
            g.Suavizado = e.S; g.Letra = e.T; g.Filtro = e.I;
            if (e.Trans == 1) g.Traslada(3, 2); else if (e.Trans == 2) g.Escala(1.3f, 1.3f); else if (e.Trans == 3) g.Gira(7f); else if (e.Trans == 4) g.Traslada(3.5f, 2.25f);
            if (e.Recorte && recorte) g.Recorta(new RectangleF(10.5f, 4, bw - 30, bh - 12));
            return g;
        }

        // ---- la llamada: la firma de XP (con su Graphics) o la traducida (su lienzo y su estado), por reflexion
        // (un pincel liso de ese color: cada llamada recibe el suyo, un SolidBrush en la referencia y un SKPaint en el programa nuevo)
        private sealed class Liso { public readonly Color C; public Liso(Color c) { C = c; } }
        // un parametro del estado del Graphics en la traducida: el lienzo, el suavizado, el modo de letra, el filtro, la copia o el medio pixel
        private static bool DeEstado(ParameterInfo p)
        {
            Type t = p.ParameterType;
            return t == Pru.TLienzo || t == typeof(SKCanvas) || t.Name == "AjusteLetra" || t.Name == "Filtro" || (t == typeof(bool) && (p.Name == "suave" || p.Name == "copia" || p.Name == "medio"));
        }
        // el metodo de Form1 con ese nombre y tantos parametros, sin contar los del estado, como la firma de XP sin su Graphics
        private static MethodInfo Metodo(string nombre, int sinEstado)
        {
            foreach (MethodInfo m in typeof(Form1).GetMethods(Todo))
            {
                if (m.Name != nombre) continue;
                int k = 0; foreach (ParameterInfo p in m.GetParameters()) if (!DeEstado(p)) k++;
                if (k == sinEstado) return m;
            }
            throw new Exception("no esta " + nombre + "/" + sinEstado);
        }
        private static object Convierte(object v, Type t, List<IDisposable> suelta)
        {
            if (v == null || t.IsInstanceOfType(v)) return v;
            if (v is Fte) return ((Fte)v).Obj;   // (la Font de la referencia o la Letra del programa nuevo)
            if (v is Liso) { object p = Pru.Brocha(((Liso)v).C); suelta.Add((IDisposable)p); return p; }
            if (v is Fmt) return ((Fmt)v).Obj;   // (el StringFormat o el MISMO FormatoTexto)
            if (v is object[] && t.IsArray)   // (las portadas: un arreglo del tipo que pida cada lado, Image[] o SKBitmap[])
            {
                object[] a = (object[])v; Array r = Array.CreateInstance(t.GetElementType(), a.Length);
                for (int i = 0; i < a.Length; i++) r.SetValue(a[i], i);
                return r;
            }
            throw new Exception("no se pasar " + v.GetType().Name + " a " + t.Name);
        }
        // xp: los argumentos de la firma de XP (el lienzo de prueba donde XP pasa su Graphics; null en los out). Devuelve lo que devuelve y deja en 'args' los out.
        private static object Llama(string nombre, object[] xp, out object[] args)
        {
            int sinEstado = 0; foreach (object o in xp) if (!(o is Lz)) sinEstado++;
            MethodInfo m = Metodo(nombre, sinEstado);
            ParameterInfo[] ps = m.GetParameters();
            args = new object[ps.Length];
            List<IDisposable> suelta = new List<IDisposable>();
            int k = 0; Lz g = null; object[] est = null; bool directa = false;
            for (int i = 0; i < ps.Length; i++)
            {
                Type t = ps[i].ParameterType; if (t.IsByRef) t = t.GetElementType();
                if (Pru.Capa && t == Pru.TLienzo) { args[i] = ((Lz)xp[k++]).Objeto; continue; }
                if (DeEstado(ps[i]))
                {
                    directa = true;
                    if (g == null) { g = (Lz)xp[k++]; est = g.Estado(); }   // (el lienzo, suave, letra, copia, filtro, medio)
                    if (t == typeof(SKCanvas)) args[i] = est[0];
                    else if (t.Name == "AjusteLetra") args[i] = est[2];
                    else if (t.Name == "Filtro") args[i] = est[4];
                    else if (ps[i].Name == "suave") args[i] = est[1];
                    else if (ps[i].Name == "copia") args[i] = est[3];
                    else args[i] = est[5];
                    continue;
                }
                args[i] = Convierte(xp[k++], t, suelta);
            }
            if (directa) directas++;
            try { return m.Invoke(null, args); }
            catch (TargetInvocationException ex) { return "EXC:" + ex.InnerException.GetType().Name; }
            finally { foreach (IDisposable d in suelta) d.Dispose(); }
        }
        private static string Valor(object o)
        {
            if (o == null) return "null";
            if (o is float) return R((float)o);
            if (o is PointF[]) { StringBuilder sb = new StringBuilder(); foreach (PointF p in (PointF[])o) sb.Append("(" + R(p.X) + "," + R(p.Y) + ")"); return sb.ToString(); }
            if (o is float[]) { StringBuilder sb = new StringBuilder(); foreach (float f in (float[])o) sb.Append(R(f) + ";"); return sb.ToString(); }
            return Convert.ToString(o, ci);
        }

        // ---- datos fijos
        private static Fte Fuente(float px) { return new Fte("Trebuchet MS", px, 1, "Pixel"); }   // la de Form1.Letra de XP (1: negrita)
        private static Fte[] Letras()   // (el estilo: 1 negrita, 2 cursiva, 4 subrayada)
        {
            return new[] { Fuente(14.592f), Fuente(25.344f), Fuente(47.616f), Fuente(21.345f), Fuente(11f), new Fte("Trebuchet MS", 12f, 1),
                new Fte("Trebuchet MS", 20f, 2 | 4, "Pixel"), new Fte("Una Que No Esta", 18f, 0, "Pixel") };
        }
        private static readonly string[] Textos = {
            "Otra Banda", "Los Tigres del Norte - Jefe de Jefes (En Vivo Desde el Auditorio Nacional)", "Supercalifragilisticoespialidosoextraordinario",
            "Canción & Niño — Ñandú", "", "¡Elige tu canción! Hay 12,345 esperándote", "La favorita de esta noche: «Amor Eterno (En Vivo Desde Bellas Artes)»",
            "La favorita de esta noche: «Corta»", "Uno «sin cierre y largo, largo, largo, largo, largo", "«»", " espacios  al final  ", "Una moneda = 4 canciones. ¡Tú pones la música!" };
        // (las banderas, por su numero: FitBlackBox 4, NoWrap 4096, LineLimit 8192, NoClip 16384)
        private static Fmt[] Formatos()
        {
            Fmt una = new Fmt(); una.Corte = ModoCorte.EllipsisCharacter; una.Banderas = 4096;   // la de la ficha
            Fmt unaC = new Fmt(); unaC.Corte = ModoCorte.EllipsisCharacter; unaC.Banderas = 4096; unaC.Vertical = ModoAlinea.Center;   // mensajes y lista
            return new[] { una, unaC };
        }
        // el tipografico compartido de fabrica, o como lo deja EstadoReposoGpu.cs (en la traduccion es el mismo objeto que usa el programa)
        private static void Tipografico(bool cambiado)
        {
            Fmt t = Fmt.Tipografico;
            if (cambiado) { t.Horizontal = ModoAlinea.Center; t.Vertical = ModoAlinea.Center; t.Banderas |= 4096; }
            else { t.Horizontal = ModoAlinea.Near; t.Vertical = ModoAlinea.Near; t.Banderas = 4 | 8192 | 16384; t.Corte = ModoCorte.None; }
        }
        private static string DeTipografico() { Fmt t = Fmt.Tipografico; return t.Horizontal + "/" + t.Vertical + "/" + t.Banderas + "/" + t.Corte; }

        // portadas: opaca, con alfa, grande y chica (hechas punto a punto, ya premultiplicadas, como con LockBits de Format32bppPArgb: las
        // mismas en las dos)
        private static object Portada(int w, int h, int tipo)
        {
            object b = Pru.NuevoMapa(w, h, "Format32bppPArgb");
            uint[] d = new uint[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int r = (x * 255 / Math.Max(1, w - 1)), g = (y * 255 / Math.Max(1, h - 1)), bl = ((x / 7 + y / 5) % 2) * 200 + 30;
                    int a = tipo == 1 ? Math.Min(255, (x + y) * 255 / Math.Max(1, w + h - 2) + ((x / 9) % 2) * 40) : 255;
                    uint px = ((uint)a << 24) | ((uint)(r * a / 255) << 16) | ((uint)(g * a / 255) << 8) | (uint)(bl * a / 255);
                    d[y * w + x] = px;
                }
            Pru.EscribeCrudos(b, d);
            return b;
        }

        // ---- 1. la letra de la atraccion (Form1.Letra)
        private static void Letra()
        {
            MethodInfo m = typeof(Form1).GetMethod("Letra", Todo, null, new[] { typeof(float) }, null);
            foreach (float px in new[] { 11f, 14.592f, 25.344f, 47.616f, 0.5f, 120f })
            {
                object l = m.Invoke(null, new object[] { px });
                // (la Font de la referencia o la Letra del programa nuevo, mirada como Fte)
                Fte f = Fte.De(l);
                string s = f.Nombre + "|" + R(f.Tamano) + "|" + f.Negrita + "|" + (f.Unidad == "Pixel") + "|" + R(f.AltoRenglon);
                Linea("letra-" + R(px), s);
                ((IDisposable)l).Dispose();
            }
        }

        // ---- 2. la ficha del escaparate (HorneaFichaVitrina)
        private static FichaVitrina Ficha(string nombre, string artista, string genero, Color gc, int canciones, int videos, int pedidas)
        { FichaVitrina d = new FichaVitrina(); d.Nombre = nombre; d.Artista = artista; d.Genero = genero; d.GeneroColor = gc; d.Canciones = canciones; d.Videos = videos; d.Pedidas = pedidas; return d; }
        private static void Fichas(string tipo)
        {
            FichaVitrina[] fs = {
                Ficha("", "", "", Color.Empty, 0, 0, 0),
                Ficha("Los Ejemplos", "Banda de Prueba", "Regional", Color.FromArgb(233, 180, 128), 12, 0, 3),
                Ficha("Los Tigres del Norte - Jefe de Jefes (Edicion Especial de Coleccion)", "Los Tigres del Norte", "Norteña", Color.FromArgb(120, 200, 90), 24, 2, 1),
                Ficha("Un nombre de disco larguisimo que no cabe ni en tres renglones aunque se achique la letra hasta el setenta y ocho por ciento de su tamano original, de verdad", "", "Rock", Color.FromArgb(200, 60, 60), 1, 0, 0),
                Ficha("Corto", "Un artista con un nombre muy largo que no cabe en el renglon de la ficha ni achicando la letra al setenta y cinco", "", Color.Empty, 0, 3, 57),
                Ficha("Canción & Niño — Ñandú", "José Alfredo Jiménez", "Música Ranchera Tradicional de Siempre", Color.FromArgb(255, 210, 60), 1234, 0, 12345),
                Ficha("Supercalifragilisticoespialidosoextraordinariamentelarguisimo", "Solo", "Pop", Color.FromArgb(150, 120, 255), 2, 0, 1),
            };
            int[][] medidas = { new[] { 462, 768 }, new[] { 300, 600 }, new[] { 40, 480 }, new[] { 700, 1080 }, new[] { 1000, 1440 }, new[] { 120, 300 } };
            for (int i = 0; i < fs.Length; i++)
                foreach (int[] md in medidas)
                {
                    object b = Form1.HorneaFichaVitrina(fs[i], md[0], md[1]);
                    Linea("ficha-" + tipo + "-" + i + "-" + md[0] + "x" + md[1], Puntos("ficha-" + tipo + "-" + i + "-" + md[0] + "x" + md[1], b));
                    ((IDisposable)b).Dispose();
                }
        }

        // ---- 3. los mensajes (HorneaMensajes)
        private static void Mensajes()
        {
            List<string>[] ls = {
                Form1.MensajesAtraccion(8, 4, null), Form1.MensajesAtraccion(12345, 1, "Amor Eterno (En Vivo Desde el Palacio de Bellas Artes, Mexico)"),
                Form1.MensajesAtraccion(0, 2, "Corta"), new List<string>(), new List<string> { "Supercalifragilisticoespialidosoextraordinariamentelarguisimoyotravez" },
                new List<string> { "La favorita de esta noche: «Supercalifragilisticoespialidosoextraordinariamentelarguisimo»", "«»", "Uno «sin cierre" },
            };
            int[][] medidas = { new[] { 462, 768 }, new[] { 200, 480 }, new[] { 40, 600 }, new[] { 900, 1080 } };
            for (int i = 0; i < ls.Length; i++)
                foreach (int[] md in medidas)
                {
                    float[] anchos;
                    object b = Form1.HorneaMensajes(ls[i], md[0], md[1], out anchos);
                    Linea("mensajes-" + i + "-" + md[0] + "x" + md[1], Puntos("mensajes-" + i + "-" + md[0] + "x" + md[1], b) + "\tanchos=" + Valor(anchos));
                    ((IDisposable)b).Dispose();
                }
        }

        // ---- 4. la lista de lo mas pedido esta noche (HorneaTop): la geometria de HorneaLaLista
        private static FilaNoche Fila(string titulo, string artista, int veces, Color c) { FilaNoche f = new FilaNoche(); f.Titulo = titulo; f.Artista = artista; f.Veces = veces; f.Color = c; f.Ruta = "r"; f.Portada = "p"; return f; }
        private static void Top()
        {
            List<FilaNoche> cinco = new List<FilaNoche> {
                Fila("Amor Eterno", "Rocío Dúrcal", 7, Color.FromArgb(233, 180, 128)), Fila("La Bikina", "Mariachi Vargas de Tecalitlán", 5, Color.FromArgb(120, 200, 90)),
                Fila("El Rey", "José Alfredo Jiménez", 3, Color.FromArgb(96, 110, 170)),
                Fila("Un titulo larguisimo que no cabe ni con la letra al sesenta y dos por ciento de su tamano, ni de chiste", "", 2, Color.FromArgb(200, 60, 60)),
                Fila("Nada", "Los Ejemplos", 1, Color.FromArgb(150, 120, 255)) };
            List<FilaNoche> tres = new List<FilaNoche> { Fila("Uno", "A", 1, Color.FromArgb(255, 210, 60)), Fila("Dos", "B-c d", 1, Color.FromArgb(10, 20, 30)), Fila("Tres", ".-_ ", 1, Color.FromArgb(255, 255, 255)) };
            object opaca = Portada(64, 64, 0), alfa = Portada(80, 60, 1), grande = Portada(1600, 1000, 0), chica = Portada(7, 9, 1);
            object[][] portadas = { new object[5], new object[] { opaca, alfa, null, grande, chica }, new object[] { chica, grande } };
            int[][] pantallas = { new[] { 1024, 768 }, new[] { 800, 600 }, new[] { 1280, 1024 }, new[] { 1920, 1080 }, new[] { 640, 480 } };
            int k = 0;
            foreach (List<FilaNoche> filas in new[] { cinco, tres })
                foreach (object[] por in portadas)
                    foreach (int[] p in pantallas)
                    {
                        RectangleF pag; float cabW, cabH, filasY, filaH;
                        Form1.GeometriaTop(p[0], p[1], out pag, out cabW, out cabH, out filasY, out filaH);
                        object[] args;
                        object b = Llama("HorneaTop", new object[] { filas, por, filas.Count == 5 ? 18 : 1, (int)pag.Width, (int)pag.Height, cabW, cabH, filasY, filaH, null, null, null, null }, out args);
                        string nom = "lista-" + (k++) + "-" + filas.Count + "-" + p[0] + "x" + p[1];
                        Linea(nom, Puntos(nom, b) + "\tbarra=" + Valor(args[9]) + "," + Valor(args[10]) + "," + Valor(args[11]) + "," + Valor(args[12]));
                        ((IDisposable)b).Dispose();
                    }
            foreach (object b in new[] { opaca, alfa, grande, chica }) Pru.Suelta(b);
        }

        // ---- 5. donde hay tinta (Tinta)
        private static void Tintas()
        {
            int k = 0;
            foreach (int[] md in new[] { new[] { 1, 1 }, new[] { 504, 76 }, new[] { 33, 17 } })
                for (int caso = 0; caso < 4; caso++)
                {
                    using (IDisposable b = (IDisposable)Pru.NuevoMapa(md[0], md[1], "Format32bppPArgb"))
                    {
                        using (Lz g = Pru.Lienzo(b))
                        {
                            g.Limpia(Color.Transparent);
                            if (caso == 1) { g.Letra = ModoLetra.AntiAliasGridFit; using (Fte f = Fuente(14.592f)) g.EscribeEn("EN ESTA ROCOLA", f, Color.FromArgb(207, 224, 255), 17, 15); }
                            else if (caso == 2) { g.Rellena(Color.FromArgb(1, 255, 255, 255), md[0] - 1, md[1] - 1, 1, 1); }
                            else if (caso == 3) { g.Suavizado = ModoSuave.AntiAlias; g.RellenaOvalo(Color.FromArgb(40, 255, 0, 0), 2.3f, 1.7f, md[0] * .4f, md[1] * .6f); }
                        }
                        object[] args;
                        object r = Llama("Tinta", new object[] { b, null, null, null, null }, out args);
                        Linea("tinta-" + (k++), Valor(r) + " " + Valor(args[1]) + "," + Valor(args[2]) + "," + Valor(args[3]) + "," + Valor(args[4]));
                    }
                }
        }

        // ---- 6. las piezas, con cada estado del Graphics
        private static void Piezas(string tipo)
        {
            Fte[] fs = Letras(); Fmt[] sfs = Formatos();
            int k = 0;
            foreach (Estado e in Estados)
            {
                // las que miden: el modo de letra del Graphics
                using (IDisposable b = (IDisposable)Pru.NuevoMapa(8, 8, "Format32bppPArgb"))
                using (Lz g = Prepara(b, e, false))
                {
                    foreach (Fte f in fs)
                        foreach (Fmt sf in sfs)
                            foreach (string t in Textos)
                                foreach (float ancho in new[] { 462f, 200f, 90f, 30f, 0f })
                                {
                                    object[] a;
                                    string q = Valor(Llama("EscalaQueCabe", new object[] { g, t, f, ancho, sf }, out a)) + "|" + Valor(Llama("EscalaQueCabe", new object[] { g, t, f, ancho, sf, .62f }, out a))
                                        + "|" + Valor(Llama("AcortaPorPalabras", new object[] { g, t, f, ancho, sf }, out a)) + "|" + Valor(Llama("AcortaCita", new object[] { g, t, f, ancho, sf }, out a));
                                    Linea("mide-" + tipo + "-" + e.Nombre + "-" + (k++), q);
                                }
                    foreach (Fte f in fs)
                    {
                        object[] a;
                        string q = Valor(Llama("AnchoPastilla", new object[] { g, "Regional", f, 26.774f }, out a)) + "|" + Valor(Llama("AnchoPastilla", new object[] { g, "pedido 3 veces", f, 30f }, out a));
                        foreach (float ancho in new[] { 462f, 160f, 40f })
                        {
                            Llama("DisponPastillas", new object[] { g, new List<string> { "Música Ranchera", "pedido 12 veces", "1,234 canciones", "otra" }, f, 26.774f, ancho, null }, out a);
                            q += "|" + Valor(a[a.Length - 1]);
                            q += "|" + Valor(Llama("DisponPastillas", new object[] { g, new List<string> { "Pop", "2 canciones" }, f, 20f, ancho, null }, out a));
                        }
                        Linea("pastillas-mide-" + tipo + "-" + e.Nombre + "-" + (k++), q);
                    }
                }
                // las que pintan
                int j = 0;
                foreach (Fte f in new[] { fs[0], fs[1], fs[5], fs[6] })
                {
                    foreach (Fmt sf in sfs)
                        foreach (string t in new[] { Textos[0], Textos[1], Textos[2], Textos[3], Textos[10] })
                            foreach (RectangleF r in new[] { new RectangleF(0, 10, 462, 31.5f), new RectangleF(4.5f, 3.25f, 120, 26), new RectangleF(0, 0, 30, 40) })
                                Pinta("renglon-" + tipo + "-" + e.Nombre + "-" + (j++), e, 480, 64, g =>
                                {
                                    object[] a;
                                    Liso br = new Liso(Color.FromArgb(255, 226, 176));
                                        return Valor(Llama("Renglon", new object[] { g, t, f, br, r, sf }, out a)) + "|" + Valor(Llama("Renglon", new object[] { g, t, f, br, new RectangleF(r.X, r.Y + 20, r.Width, r.Height), sf, .62f }, out a));
                                });
                    foreach (string t in new[] { "Regional", "pedido 1 vez", "Música Ranchera Tradicional de Siempre" })
                        foreach (float x in new[] { 0f, 118.5f, 400f })
                            Pinta("pastilla-" + tipo + "-" + e.Nombre + "-" + (j++), e, 480, 64, g =>
                            {
                                object[] a;
                                return Valor(Llama("Pastilla", new object[] { g, t, f, Color.FromArgb(150, 200, 255), x, 3.5f, 26.774f, 462f }, out a)) + "|"
                                    + Valor(Llama("PastillaGenero", new object[] { g, t, f, Color.FromArgb(233, 180, 128), x, 33f, 26.774f, 462f }, out a));
                            });
                    foreach (string t in new[] { "LB", "3 canciones", "" })
                        Pinta("centrado-" + tipo + "-" + e.Nombre + "-" + (j++), e, 200, 64, g =>
                        {
                            object[] a;
                            Llama("TextoCentrado", new object[] { g, t, f, Color.FromArgb(235, 255, 255, 255), new RectangleF(10.5f, 4, 150, 50) }, out a);
                            return "-";
                        });
                    foreach (string t in new[] { "EN ESTA ROCOLA", "A H O R A", "Ñ&" })
                    {
                        Pinta("espaciado-" + tipo + "-" + e.Nombre + "-" + (j++), e, 480, 80, g =>
                        {
                            object[] a;
                            Liso br = new Liso(Color.FromArgb(207, 224, 255)); Llama("Espaciado", new object[] { g, t, f, br, 21f, 21f, f.Tamano * .22f }, out a);
                            return "-";
                        });
                        foreach (int ancho in new[] { 462, 60 })
                            Pinta("espaciadoentero-" + tipo + "-" + e.Nombre + "-" + (j++), e, 480, 80, g =>
                            {
                                object[] a;
                                Liso br = new Liso(Color.FromArgb(207, 224, 255)); Llama("EspaciadoEntero", new object[] { g, t, f, br, ancho, f.AltoRenglon, f.Tamano * .22f }, out a);
                                return "-";
                            });
                    }
                }
                foreach (float r in new[] { 8.4f, 20.123f, 3f })
                    Pinta("luna-" + tipo + "-" + e.Nombre + "-" + (j++), e, 120, 80, g => { object[] a; Llama("Luna", new object[] { g, r, r * 1.15f, 33.6f, r }, out a); return "-"; });
            }
        }
        // un mapa con el fondo de Prepara, la pieza encima y su huella
        private static void Pinta(string nombre, Estado e, int w, int h, Func<Lz, string> pieza)
        {
            using (IDisposable b = (IDisposable)Pru.NuevoMapa(w, h, "Format32bppPArgb"))
            {
                string r;
                using (Lz g = Prepara(b, e, true)) r = pieza(g);
                Linea(nombre, Puntos(nombre, b) + "\t" + r);
            }
        }

        // ---- 7. la tapa de cada fila (Tapa): sin recorte en el Graphics (HorneaTop no pone ninguno, y la tapa pone el suyo)
        private static void Tapas()
        {
            object opaca = Portada(64, 64, 0), alfa = Portada(80, 60, 1), grande = Portada(1600, 1000, 0), chica = Portada(7, 9, 1);
            int j = 0;
            foreach (Estado e in Estados)
                foreach (object por in new[] { null, opaca, alfa, grande, chica })
                    foreach (FilaNoche f in new[] { Fila("Amor Eterno", "Rocío Dúrcal", 7, Color.FromArgb(233, 180, 128)), Fila("Sin artista aqui", "", 1, Color.FromArgb(10, 20, 30)) })
                        foreach (RectangleF r in new[] { new RectangleF(48.6f, 6.3f, 61.44f, 61.44f), new RectangleF(10, 10, 30, 30) })
                            Pinta("tapa-" + e.Nombre + "-" + (j++), new Estado { Nombre = e.Nombre, S = e.S, T = e.T, I = e.I, Trans = e.Trans }, 160, 80, g =>
                            {
                                object[] a;
                                object x = Llama("Tapa", new object[] { g, r, por, f }, out a);
                                return Valor(x);
                            });
            foreach (object b in new[] { opaca, alfa, grande, chica }) Pru.Suelta(b);
        }

        public static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "vuelca") { carpeta = args[1]; System.IO.Directory.CreateDirectory(carpeta); }
            Tipografico(false);
            Linea("tipografico-inicio", DeTipografico());
            Letra();
            Fichas("fabrica");
            Mensajes();
            Top();
            Tintas();
            Piezas("fabrica");
            Tapas();
            // el tipografico compartido como lo deja el panel de reposo (EstadoReposoGpu.cs): lo heredan Espaciado (su copia) y AnchoPastilla
            Tipografico(true);
            Linea("tipografico-cambiado", DeTipografico());
            Fichas("reposo");
            Piezas("reposo");
            Linea("tipografico-fin", DeTipografico());
            Console.Error.WriteLine("atraccion: " + n + " lineas, " + mapas + " mapas (" + distintos.Count + " distintos), " + directas + " llamadas directas a la traducida");
        }
    }
}

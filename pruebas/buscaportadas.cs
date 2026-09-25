using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;

// EL BUSCADOR CON PORTADAS (Rocola/BuscadorPortadasGpu.cs): la idea "Teclado compacto y resultados con portada" del
// lienzo de bocetos. Solo con tarjeta grafica y con su interruptor en Ctrl+F3 -> Pantalla.
//
//  1) EL REPARTO. En seis pantallas (de 800x600 a 1920x1080): la casilla, los atajos, el teclado compacto, las tarjetas
//     y las ayudas caben y no se pisan; las teclas miden lo mismo; las tarjetas van en dos columnas.
//  2) MOVERSE. Veinte mil pasos al azar con atajos, teclado y tarjetas: la seleccion siempre es valida y siempre se ve
//     (la tarjeta elegida nunca queda fuera al bajar de fila); y los pasos concretos (de la fila de numeros a los
//     atajos, del final de una fila a las tarjetas y de vuelta a la fila que tiene al lado).
//  3) PULSAR Y TOCAR: el verde y el dedo hacen lo de siempre en teclas y tarjetas, y en un atajo, el atajo.
//  4) LA IMAGEN GRANDE solo lleva lo quieto: con otra consulta, otros resultados o la seleccion en otro sitio sale
//     igual, pixel a pixel. Por eso su firma no los mira y teclear no la rehace.
//  5) LAS IMAGENES CHICAS. Fuera de las tarjetas, la de las tarjetas es transparente del todo: dibujarla por tarjetas
//     da lo mismo que entera. Y la tarjeta, la tecla y el atajo elegidos tapan entero lo que tienen debajo.
//  6) LA ENTRADA: con otra lista entran las tarjetas que cambiaron, una tras otra; las que siguen igual, quietas;
//     bajando de fila, ya puestas.
//  7) LOS ATAJOS, con el formulario y datos de verdad: lo mas pedido hoy (la cuenta de la noche), videos, karaoke (el
//     genero) y "Buscaste" (las dos ultimas, sin repetir, de esta noche y nunca la clave de servicio).
//  8) LAS PORTADAS: solo se piden con la vista quieta, solo las que se ven, una vez; al llegar se rehace.
//  9) LA TARJETA IMITADA: las tarjetas van por trozos y debajo de la elegida, que entra con la suya. Y con la ventana del
//     buscador: al abrir suben con ella; al cerrar, ella se queda con sus texturas y bajan juntas; al acabar, se sueltan.
// 10) CTRL+F3: el interruptor, su preferencia y "de fabrica".
// 11) ENCHUFADO donde toca, y sin el interruptor todo por el camino de siempre.
// 12) LO QUIETO, HORNEADO: el resplandor, los fondos y los bordes se dibujan una vez y se pegan. Pegar da los mismos
//     pixeles que dibujar; moverse y teclear no hornean nada; otro color no reutiliza lo del anterior; se guardan pocas.
// 13) LAS CINTAS DE LA TARJETA ELEGIDA: con los titulos deslizantes, el nombre y el renglon que no caben van en su cinta
//     (y no quietos); apagado o si cabe, quietos. En la tarjeta imitada, encima de la elegida, entrando con ella y con la
//     ventana al abrir y al cerrar; con el buscador de siempre, las de su vista previa tambien van con la ventana.
//
// Sin tarjeta grafica solo se mira lo que vive en los dos perfiles (la preferencia y la pestana): el dibujo por
// procesador no sabe nada de esto.
class BuscaPortadas
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    static Type TF, TSO, TItem, TAlb, TTrack, TPanel, TStar, TDev, TD3D, TCarta, TKey;

    static FieldInfo Campo(Type t, string n)
    {
        for (; t != null; t = t.BaseType) { FieldInfo f = t.GetField(n, TODO | BindingFlags.DeclaredOnly); if (f != null) return f; }
        return null;
    }
    static void Pon(object o, string n, object v)
    {
        FieldInfo f = Campo(o.GetType(), n);
        if (f != null) { f.SetValue(o, v); return; }
        PropertyInfo p = o.GetType().GetProperty(n, TODO);
        if (p != null && p.CanWrite) { p.SetValue(o, v, null); return; }
        throw new Exception("falta el campo " + n);
    }
    static object Toma(object o, string n)
    {
        FieldInfo f = Campo(o.GetType(), n);
        if (f != null) return f.GetValue(o);
        PropertyInfo p = o.GetType().GetProperty(n, TODO);
        if (p != null) return p.GetValue(o, null);
        throw new Exception("falta el campo " + n);
    }
    static MethodInfo Metodo(Type t, string n)
    {
        for (; t != null; t = t.BaseType) { MethodInfo m = t.GetMethod(n, TODO | BindingFlags.DeclaredOnly); if (m != null) return m; }
        return null;
    }
    static MethodInfo MetodoN(Type t, string n, int args)
    {
        for (; t != null; t = t.BaseType)
            foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly))
                if (m.Name == n && m.GetParameters().Length == args) return m;
        return null;
    }
    static object Llama(object o, string n, params object[] a)
    {
        MethodInfo m = MetodoN(o.GetType(), n, a.Length);
        if (m == null) throw new Exception("falta " + o.GetType().Name + "." + n);
        try { return m.Invoke(o, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static object LlamaS(Type t, string n, params object[] a)
    {
        MethodInfo m = MetodoN(t, n, a.Length);
        if (m == null) throw new Exception("falta " + t.Name + "." + n);
        try { return m.Invoke(null, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    // ======================= LOS DATOS =======================
    static object Album(string nombre, string artista, string carpeta, int canciones, string portada)
    {
        object a = Activator.CreateInstance(TAlb, true);
        Pon(a, "Name", nombre); Pon(a, "Artist", artista); Pon(a, "Folder", carpeta); Pon(a, "CoverPath", portada);
        IList tr = (IList)Toma(a, "Tracks");
        for (int i = 0; i < canciones; i++)
        {
            object t = Activator.CreateInstance(TTrack, true);
            Pon(t, "Title", nombre + " " + i); Pon(t, "Artist", artista); Pon(t, "Path", carpeta + "/" + i + ".mp3");
            tr.Add(t);
        }
        return a;
    }
    static object Item(int kind, string display, object album, object track)
    {
        object it = Activator.CreateInstance(TItem, true);
        Pon(it, "Kind", kind); Pon(it, "Display", display); Pon(it, "Key", display.ToLowerInvariant()); Pon(it, "AlbumRef", album);
        if (track != null) Pon(it, "TrackRef", track);
        if (kind == 0)
        {
            IList l = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
            l.Add(album); Pon(it, "Albums", l);
        }
        return it;
    }
    static IList Lista() { return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem)); }
    static readonly string[] Nombres = { "Vicente Fernandez", "Los Bukis", "Los Tigres del Norte", "Juan Gabriel", "Rocio Durcal",
                                         "Jose Jose", "Bronco", "Mana", "Selena", "Marco Antonio Solis", "Un titulo muchisimo mas largo de lo que cabe en ninguna tarjeta" };
    static IList Resultados(string semilla, int n)
    {
        IList r = Lista();
        for (int i = 0; i < n; i++)
        {
            string art = Nombres[(i + semilla.Length) % Nombres.Length];
            object a = Album("Disco " + semilla + i, art, "/musica/" + art + "/" + semilla + i, 3 + i % 9, (i % 3 == 0) ? null : "/portadas/" + semilla + i + ".jpg");
            IList tr = (IList)Toma(a, "Tracks");
            int k = i % 3;
            r.Add(Item(k, k == 0 ? art : k == 1 ? art + " - Disco " + semilla + i : art + " - Cancion " + semilla + i, a, k == 2 ? tr[0] : null));
        }
        return r;
    }
    // Las portadas "en memoria": todas menos las que se dejan fuera a proposito.
    static readonly Dictionary<string, Image> enMemoria = new Dictionary<string, Image>();
    static readonly List<string> pedidas = new List<string>();
    static bool todasEnMemoria = true;
    static Bitmap portadaComun;
    static Image Ya<T>(T a)
    {
        string k = (string)TAlb.GetField("CoverPath").GetValue(a);
        if (k == null) return null;
        Image i; if (enMemoria.TryGetValue(k, out i)) return i;
        return todasEnMemoria ? portadaComun : null;
    }
    static Image Pide<T>(T a)
    {
        string k = (string)TAlb.GetField("CoverPath").GetValue(a);
        if (k != null) pedidas.Add(k);
        return Ya(a);
    }
    static int Cola<T>(T it) { string d = (string)TItem.GetField("Display").GetValue(it); return d.EndsWith("7") ? 3 : 0; }
    static bool Suena<T>(T it) { string d = (string)TItem.GetField("Display").GetValue(it); return d.EndsWith("5"); }
    static string Gen<T>(T it) { return "Ranchera"; }
    static string Dur<T>(T it) { return "3:02"; }
    static readonly List<int> atajosPulsados = new List<int>();
    static readonly List<object> elegidos = new List<object>();
    static void Atajo(int k) { atajosPulsados.Add(k); }
    static void Elige<T>(T it) { elegidos.Add(it); }

    static Control Buscador(int W, int H, int resultados)
    {
        Control so = (Control)Activator.CreateInstance(TSO, true);
        so.SetBounds(0, 0, W, H - 86);   // SearchOverlayRect
        Pon(so, "GetCoverYa", Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TAlb, typeof(Image)), typeof(BuscaPortadas).GetMethod("Ya", TODO).MakeGenericMethod(TAlb)));
        Pon(so, "GetCover", Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TAlb, typeof(Image)), typeof(BuscaPortadas).GetMethod("Pide", TODO).MakeGenericMethod(TAlb)));
        Pon(so, "GetQueuePos", Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(int)), typeof(BuscaPortadas).GetMethod("Cola", TODO).MakeGenericMethod(TItem)));
        Pon(so, "GetSonando", Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(bool)), typeof(BuscaPortadas).GetMethod("Suena", TODO).MakeGenericMethod(TItem)));
        Pon(so, "GetGenero", Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(string)), typeof(BuscaPortadas).GetMethod("Gen", TODO).MakeGenericMethod(TItem)));
        Pon(so, "GetDuracion", Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(string)), typeof(BuscaPortadas).GetMethod("Dur", TODO).MakeGenericMethod(TItem)));
        Pon(so, "OnChoose", Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(TItem), typeof(BuscaPortadas).GetMethod("Elige", TODO).MakeGenericMethod(TItem)));
        Pon(so, "OnAtajo", (Action<int>)Atajo);
        Pon(so, "Accent", Color.FromArgb(215, 120, 25));
        Pon(so, "SeparateGpu", true);
        Llama(so, "PonNuevo", true);
        Llama(so, "PonAtajos", new List<string> { "Lo m\u00e1s pedido hoy", "Videos", "Karaoke", "Buscaste: Tigres", "Buscaste: Bukis" });
        Pon(so, "Results", Resultados("vi", resultados));
        Pon(so, "Query", "VI"); Pon(so, "Escrito", "VI");
        Pon(so, "TotalResultados", resultados); Pon(so, "TotalDe", "VI");
        return so;
    }
    static object Reparto(Control so) { return Llama(so, "AseguraReparto"); }
    static Rectangle R(object rep, string n) { return (Rectangle)rep.GetType().GetField(n).GetValue(rep); }
    static int I(object rep, string n) { return (int)rep.GetType().GetField(n).GetValue(rep); }
    static IList Filas(Control so) { return (IList)Toma(so, "rows"); }
    static RectangleF TeclaR(Control so, int f, int c) { IList fila = (IList)Filas(so)[f]; return (RectangleF)TKey.GetField("R").GetValue(fila[c]); }
    static List<Rectangle> Atajos(Control so) { return new List<Rectangle>((IEnumerable<Rectangle>)Toma(so, "atajoRects")); }

    static Bitmap Grande(Control so)
    {
        Bitmap b = new Bitmap(so.Width, so.Height, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            Llama(so, "RenderContent", g, new Rectangle(0, 0, so.Width, so.Height), true);
        }
        return b;
    }
    static Bitmap Pieza(Control so, int cual, out Rectangle donde)
    {
        object[] a = new object[] { cual, Rectangle.Empty };
        bool viva = (bool)MetodoN(TSO, "PiezaViva", 2).Invoke(so, a);
        donde = (Rectangle)a[1];
        if (!viva || donde.Width < 1 || donde.Height < 1) return null;
        Bitmap b = new Bitmap(donde.Width, donde.Height, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            Llama(so, "DibujaPiezaViva", g, cual, -donde.X, -donde.Y);
        }
        return b;
    }
    static int[] Pixeles(Bitmap b)
    {
        int w = b.Width, h = b.Height; int[] px = new int[w * h];
        BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { for (int y = 0; y < h; y++) Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), px, y * w, w); }
        finally { b.UnlockBits(bd); }
        return px;
    }
    static int Distintos(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return -1;
        int[] pa = Pixeles(a), pb = Pixeles(b); int n = 0;
        for (int i = 0; i < pa.Length; i++) if (pa[i] != pb[i]) n++;
        return n;
    }
    static Array Cartas(Control so, double ahora, out int n, out float elegida)
    {
        Array buf = Array.CreateInstance(TCarta, 24);
        object[] a = new object[] { buf, ahora, 0f };
        n = (int)MetodoN(TSO, "LlenaCartas", 3).Invoke(so, a);
        elegida = (float)a[2];
        return buf;
    }
    static float CF(object c, string n) { return (float)TCarta.GetField(n).GetValue(c); }
    static double Ahora() { return (double)LlamaS(TPanel, "AhoraMs"); }

    // ======================= 1) EL REPARTO =======================
    static readonly int[][] Pantallas = { new int[] { 800, 600 }, new int[] { 1024, 768 }, new int[] { 1280, 720 }, new int[] { 1366, 768 }, new int[] { 1280, 1024 }, new int[] { 1920, 1080 } };
    static void ElReparto()
    {
        Console.WriteLine("\n  1) EL REPARTO: TODO CABE Y NADA SE PISA, EN SEIS PANTALLAS\n");
        int malas = 0; string primero = null; string resumen = "";
        foreach (int[] p in Pantallas)
        {
            Control so = Buscador(p[0], p[1], 40);
            object rep = Reparto(so);
            int W = so.Width, H = so.Height;
            Rectangle caja = R(rep, "Caja"), atj = R(rep, "Atajos"), tec = R(rep, "Teclado"), res = R(rep, "Resultados"), barra = R(rep, "Barra"), ayu = R(rep, "Ayudas");
            Rectangle todo = new Rectangle(0, 0, W, H);
            List<string> q = new List<string>();
            foreach (Rectangle r in new Rectangle[] { caja, atj, tec, res, barra, ayu }) if (!todo.Contains(r)) q.Add("algo se sale: " + r);
            Rectangle[] zonas = { caja, atj, tec, res, ayu };
            for (int i = 0; i < zonas.Length; i++) for (int j = i + 1; j < zonas.Length; j++) if (zonas[i].IntersectsWith(zonas[j])) q.Add("se pisan " + zonas[i] + " y " + zonas[j]);
            if (barra.IntersectsWith(res)) q.Add("la barrita pisa las tarjetas");
            // Las teclas: dentro del teclado, sin pisarse, y las de letras y numeros del mismo tamano (compacto).
            IList filas = Filas(so);
            List<RectangleF> teclas = new List<RectangleF>();
            float lado = TeclaR(so, 0, 0).Width;
            for (int f = 0; f < filas.Count; f++)
                for (int c = 0; c < ((IList)filas[f]).Count; c++)
                {
                    RectangleF k = TeclaR(so, f, c); teclas.Add(k);
                    if (!RectangleF.Inflate(tec, 0.5f, 0.5f).Contains(k)) q.Add("tecla fuera del teclado " + f + "," + c);
                    if (f < 4 && (Math.Abs(k.Width - lado) > 0.01f || Math.Abs(k.Height - lado) > 0.01f)) q.Add("tecla de otro tamano " + f + "," + c);
                }
            for (int i = 0; i < teclas.Count; i++) for (int j = i + 1; j < teclas.Count; j++) if (teclas[i].IntersectsWith(teclas[j])) q.Add("teclas que se pisan");
            // Los atajos: en su fila, sin pisarse; los que no caben, fuera.
            List<Rectangle> at = Atajos(so);
            for (int i = 0; i < at.Count; i++)
            {
                if (!atj.Contains(at[i])) q.Add("atajo fuera de su fila");
                for (int j = i + 1; j < at.Count; j++) if (at[i].IntersectsWith(at[j])) q.Add("atajos que se pisan");
            }
            // Las tarjetas: dos columnas, dentro de su zona, sin pisarse.
            int filasV = I(rep, "Filas"); int n = 0;
            List<Rectangle> cs = new List<Rectangle>();
            for (int i = 0; i < 40; i++) { Rectangle c = (Rectangle)Llama(so, "CartaRect", i); if (!c.IsEmpty) { cs.Add(c); n++; if (!res.Contains(c)) q.Add("tarjeta fuera de su zona"); } }
            for (int i = 0; i < cs.Count; i++) for (int j = i + 1; j < cs.Count; j++) if (cs[i].IntersectsWith(cs[j])) q.Add("tarjetas que se pisan");
            if (n != filasV * 2) q.Add("se ven " + n + " tarjetas y caben " + (filasV * 2));
            if (cs.Count >= 2 && cs[0].Y != cs[1].Y) q.Add("no van en dos columnas");
            if (cs.Count >= 1 && tec.Top != cs[0].Top) q.Add("las tarjetas no empiezan a la altura del teclado");
            if (q.Count > 0) { malas++; if (primero == null) primero = p[0] + "x" + p[1] + ": " + q[0]; }
            resumen += " " + p[0] + "x" + p[1] + "=" + (int)lado + "px/" + n;
            so.Dispose();
        }
        Exige(malas == 0, "en las seis pantallas todo cabe y nada se pisa (teclas de lado/tarjetas a la vista:" + resumen + ")" + (primero != null ? " -- " + primero : ""));
        // Los atajos que no caben se quedan fuera (los ultimos), sin salirse de su fila.
        Control ch = Buscador(800, 600, 0);
        Llama(ch, "PonAtajos", new List<string> { "Lo m\u00e1s pedido hoy", "Videos", "Karaoke", "Buscaste: Juan Gabriel Y Un\u2026", "Buscaste: Marco Antonio So\u2026", "Buscaste: Los Tigres Del N\u2026" });
        object repc = Reparto(ch);
        List<Rectangle> atc = Atajos(ch); bool dentroFila = true;
        foreach (Rectangle r in atc) if (!R(repc, "Atajos").Contains(r)) dentroFila = false;
        Exige(atc.Count >= 3 && atc.Count < 6 && dentroFila, "a 800x600, con seis atajos largos solo salen los " + atc.Count + " primeros que caben, y ninguno se sale de su fila");
        ch.Dispose();
        // El teclado es compacto: no crece con la altura, solo con la escala del boceto.
        Control a = Buscador(1280, 960, 0), b = Buscador(1280, 1024, 0);
        Reparto(a); Reparto(b);
        Exige(TeclaR(a, 0, 0).Width == TeclaR(b, 0, 0).Width && TeclaR(a, 0, 0).Width == 50 && TeclaR(b, 0, 0).Height == 50,
              "compacto: a 1280 de ancho la tecla mide 50 px con 960 y con 1024 de alto, como en el boceto (el de siempre la estira hasta llenar media pantalla)");
        a.Dispose(); b.Dispose();
    }

    // ======================= 2) MOVERSE =======================
    static string Invariante(Control so)
    {
        IList filas = Filas(so);
        bool enChips = (bool)Toma(so, "EnChips"), foco = (bool)Toma(so, "FocusResults");
        int selRow = (int)Toma(so, "SelRow"), selCol = (int)Toma(so, "SelCol"), selChip = (int)Toma(so, "SelChip"), selRes = (int)Toma(so, "SelResult");
        int n = (int)Llama(so, "CartasTotales");
        if (enChips && foco) return "atajos y tarjetas a la vez";
        if (enChips) { if (selChip < 0 || selChip >= Atajos(so).Count) return "atajo fuera de rango " + selChip; }
        else if (foco)
        {
            if (selRes < 0 || selRes >= n) return "tarjeta fuera de rango " + selRes + "/" + n;
            if (((Rectangle)Llama(so, "CartaRect", selRes)).IsEmpty) return "la tarjeta elegida no se ve (" + selRes + ", fila " + Toma(so, "resScroll") + ")";
        }
        else if (selRow < 0 || selRow >= filas.Count || selCol < 0 || selCol >= ((IList)filas[selRow]).Count) return "tecla fuera de rango " + selRow + "," + selCol;
        int scroll = (int)Toma(so, "resScroll"), filasV = I(Reparto(so), "Filas");
        if (scroll < 0 || scroll > Math.Max(0, (n + 1) / 2 - filasV)) return "las tarjetas bajaron de mas: " + scroll;
        // Hay imagen chica de lo elegido, y solo una de las dos
        Rectangle d0, d2; Bitmap p0 = Pieza(so, 0, out d0), p2 = Pieza(so, 2, out d2);
        bool hay0 = p0 != null, hay2 = p2 != null;
        if (p0 != null) p0.Dispose(); if (p2 != null) p2.Dispose();
        if (foco != hay2 || foco == hay0) return "imagen chica de lo elegido: tecla/atajo " + hay0 + ", tarjeta " + hay2 + " (tarjetas enfocadas: " + foco + ")";
        return null;
    }
    static void Moverse()
    {
        Console.WriteLine("\n  2) MOVERSE: LA SELECCION SIEMPRE VALE Y SIEMPRE SE VE\n");
        Random rnd = new Random(20260924);
        int pasos = 0, malos = 0; string primero = null; int[] visitas = new int[3]; int bajadas = 0;
        int[] cuantos = { 0, 1, 7, 12, 40 };
        foreach (int[] p in new int[][] { new int[] { 1024, 768 }, new int[] { 1920, 1080 }, new int[] { 800, 600 } })
            foreach (int nres in cuantos)
                foreach (bool conAtajos in new bool[] { true, false })
                {
                    Control so = Buscador(p[0], p[1], nres);
                    if (!conAtajos) Llama(so, "PonAtajos", new List<string>());
                    for (int k = 0; k < 450; k++)
                    {
                        int m = rnd.Next(4), dx = 0, dy = 0;
                        if (m == 0) dx = -1; else if (m == 1) dx = 1; else if (m == 2) dy = -1; else dy = 1;
                        int antes = (int)Toma(so, "resScroll");
                        Llama(so, "MoveSel", dx, dy);
                        if ((int)Toma(so, "resScroll") != antes) bajadas++;
                        pasos++;
                        if ((bool)Toma(so, "EnChips")) visitas[0]++; else if ((bool)Toma(so, "FocusResults")) visitas[2]++; else visitas[1]++;
                        string q = Invariante(so);
                        if (q != null) { malos++; if (primero == null) primero = p[0] + "x" + p[1] + ", " + nres + " resultados" + (conAtajos ? "" : ", sin atajos") + ", paso " + k + ": " + q; break; }
                    }
                    so.Dispose();
                }
        Exige(malos == 0 && visitas[0] > 0 && visitas[1] > 0 && visitas[2] > 0 && bajadas > 0,
              pasos + " pasos al azar (atajos " + visitas[0] + ", teclado " + visitas[1] + ", tarjetas " + visitas[2] + "; " + bajadas + " veces bajaron o subieron las filas): la seleccion siempre vale, se ve y tiene su imagen chica" + (primero != null ? " -- " + primero : ""));

        // Los pasos concretos, a 1280x720 con 40 resultados.
        Control s = Buscador(1280, 720, 40);
        Reparto(s);
        Pon(s, "SelRow", 0); Pon(s, "SelCol", 4);
        Llama(s, "MoveSel", 0, -1);
        List<Rectangle> at = Atajos(s);
        float cx = TeclaR(s, 0, 4).X + TeclaR(s, 0, 4).Width / 2f;
        int mejor = 0; for (int i = 1; i < at.Count; i++) if (Math.Abs(at[i].X + at[i].Width / 2f - cx) < Math.Abs(at[mejor].X + at[mejor].Width / 2f - cx)) mejor = i;
        Exige((bool)Toma(s, "EnChips") && (int)Toma(s, "SelChip") == mejor, "desde la fila de los numeros, arriba lleva al atajo que tiene encima (el " + mejor + ")");
        Llama(s, "MoveSel", 1, 0); Llama(s, "MoveSel", 0, 1);
        Exige(!(bool)Toma(s, "EnChips") && (int)Toma(s, "SelRow") == 0, "y abajo vuelve a la fila de los numeros");
        bool alLado = true; int sel = 0;
        for (int f = 0; f < 5; f++)
        {
            Pon(s, "FocusResults", false); Pon(s, "EnChips", false);
            Pon(s, "SelRow", f); Pon(s, "SelCol", ((IList)Filas(s)[f]).Count - 1);   // al final de su fila
            Llama(s, "MoveSel", 1, 0);
            sel = (int)Toma(s, "SelResult");
            RectangleF kr = TeclaR(s, f, ((IList)Filas(s)[f]).Count - 1);
            int mejorC = 0; float dmin = float.MaxValue;
            for (int i = 0; i < 40; i += 2)
            {
                Rectangle c = (Rectangle)Llama(s, "CartaRect", i);
                if (c.IsEmpty) continue;
                float dd = Math.Abs(c.Y + c.Height / 2f - (kr.Y + kr.Height / 2f));
                if (dd < dmin) { dmin = dd; mejorC = i; }
            }
            if (!(bool)Toma(s, "FocusResults") || sel != mejorC) alLado = false;
        }
        Exige(alLado, "al final de cada fila, a la derecha se pasa a las tarjetas: a la de la primera columna que tiene justo al lado");
        Pon(s, "FocusResults", false); Pon(s, "SelRow", 2); Pon(s, "SelCol", 9);
        Llama(s, "MoveSel", 1, 0); sel = (int)Toma(s, "SelResult");
        bool vuelta = true;
        for (int i = 0; i < 2 * I(Reparto(s), "Filas"); i += 2)
        {
            Pon(s, "FocusResults", true); Pon(s, "SelResult", i);
            Rectangle c = (Rectangle)Llama(s, "CartaRect", i);
            Llama(s, "MoveSel", -1, 0);
            int fr = (int)Toma(s, "SelRow"), mejorF = 0; float dmin = float.MaxValue;
            for (int f = 0; f < 5; f++) { RectangleF k0 = TeclaR(s, f, 0); float dd = Math.Abs(k0.Y + k0.Height / 2f - (c.Y + c.Height / 2f)); if (dd < dmin) { dmin = dd; mejorF = f; } }
            if ((bool)Toma(s, "FocusResults") || fr != mejorF || (int)Toma(s, "SelCol") != ((IList)Filas(s)[fr]).Count - 1) vuelta = false;
        }
        Exige(vuelta, "y a la izquierda, desde cada tarjeta de la primera columna vuelve al teclado: al final de la fila que tiene justo al lado");
        // Bajar hasta la ultima fila de tarjetas: las filas bajan y la elegida siempre se ve.
        Pon(s, "FocusResults", true); Pon(s, "SelResult", 0); Pon(s, "resScroll", 0);
        bool siempre = true;
        for (int k = 0; k < 25; k++) { Llama(s, "MoveSel", 0, 1); if (((Rectangle)Llama(s, "CartaRect", (int)Toma(s, "SelResult"))).IsEmpty) siempre = false; }
        int ultimo = (int)Toma(s, "SelResult"), scroll = (int)Toma(s, "resScroll");
        Exige(siempre && ultimo == 38 && scroll == 20 - I(Reparto(s), "Filas"), "bajando se llega a la ultima fila (la 20) y las tarjetas bajan con la seleccion: " + scroll + " filas");
        for (int k = 0; k < 25; k++) Llama(s, "MoveSel", 0, -1);
        Exige((int)Toma(s, "SelResult") == 0 && (int)Toma(s, "resScroll") == 0, "y subiendo, vuelven arriba");
        s.Dispose();
    }

    // ======================= 3) PULSAR Y TOCAR =======================
    static void PulsarYTocar()
    {
        Console.WriteLine("\n  3) PULSAR Y TOCAR\n");
        Control s = Buscador(1280, 720, 12);
        Reparto(s);
        atajosPulsados.Clear(); elegidos.Clear();
        Pon(s, "EnChips", true); Pon(s, "SelChip", 2);
        Llama(s, "PressSelected");
        Exige(atajosPulsados.Count == 1 && atajosPulsados[0] == 2 && elegidos.Count == 0, "el verde en un atajo lo usa (el 2)");
        Pon(s, "EnChips", false); Pon(s, "FocusResults", true); Pon(s, "SelResult", 3);
        IList res = (IList)Toma(s, "Results");
        Llama(s, "PressSelected");
        Exige(elegidos.Count == 1 && ReferenceEquals(elegidos[0], res[3]), "el verde en una tarjeta la elige, como siempre");
        Pon(s, "FocusResults", false); Pon(s, "SelRow", 1); Pon(s, "SelCol", 2); Pon(s, "Escrito", "");
        Llama(s, "PressSelected");
        Exige((string)Toma(s, "Escrito") == "E", "el verde en una tecla la escribe");
        // Tocar con el dedo (o el raton): cada tecla, cada atajo y cada tarjeta que se ve.
        Pon(s, "Escrito", "");
        IList filas = Filas(s); string esperado = ""; bool teclasBien = true;
        for (int f = 0; f < 4; f++)
            for (int c = 0; c < ((IList)filas[f]).Count; c++)
            {
                RectangleF k = TeclaR(s, f, c);
                Pon(s, "FocusResults", f == 1);   // tambien con las tarjetas enfocadas: el teclado siempre se ve
                Llama(s, "ClickAt", new Point((int)(k.X + k.Width / 2), (int)(k.Y + k.Height / 2)));
                esperado += (string)TKey.GetField("Label").GetValue(((IList)filas[f])[c]);
                if ((bool)Toma(s, "FocusResults")) teclasBien = false;
            }
        Exige(teclasBien && (string)Toma(s, "Escrito") == esperado, "tocar cada tecla la escribe, tambien con las tarjetas enfocadas (" + esperado.Length + " teclas)");
        atajosPulsados.Clear();
        List<Rectangle> at = Atajos(s);
        for (int i = 0; i < at.Count; i++) Llama(s, "ClickAt", new Point(at[i].X + at[i].Width / 2, at[i].Y + at[i].Height / 2));
        bool orden = atajosPulsados.Count == at.Count; for (int i = 0; orden && i < at.Count; i++) if (atajosPulsados[i] != i) orden = false;
        Exige(orden && at.Count == 5, "tocar cada atajo lo usa (" + at.Count + ")");
        elegidos.Clear(); int vistas = 0; bool cartasBien = true;
        for (int i = 0; i < 12; i++)
        {
            Rectangle c = (Rectangle)Llama(s, "CartaRect", i);
            if (c.IsEmpty) continue;
            vistas++;
            Llama(s, "ClickAt", new Point(c.X + c.Width / 2, c.Y + c.Height / 2));
            if (elegidos.Count != vistas || !ReferenceEquals(elegidos[vistas - 1], res[i])) cartasBien = false;
        }
        Exige(cartasBien && vistas >= 6, "tocar cada tarjeta que se ve la elige (" + vistas + ")");
        // El destello de la tecla escrita (como en el boceto): con el teclado de la pantalla o con uno de verdad.
        Control s2 = Buscador(1280, 720, 0);
        Reparto(s2);
        MethodInfo mDest = MetodoN(TSO, "GetDestelloTecla", 5);
        double t0 = Ahora();
        Llama(s2, "TypeChar", 'e');
        object[] a = new object[] { t0 + 30, 0f, 0f, 0f, 0 };
        bool hay = (bool)mDest.Invoke(s2, a);
        RectangleF ke = TeclaR(s2, 1, 2);
        bool enE = hay && Math.Abs((float)a[1] - (ke.X + ke.Width / 2f)) < 0.5f && Math.Abs((float)a[2] - (ke.Y + ke.Height / 2f)) < 0.5f;
        int alfa0 = (int)((uint)(int)a[4] >> 24);
        a = new object[] { t0 + 250, 0f, 0f, 0f, 0 };
        bool sigue = (bool)mDest.Invoke(s2, a); int alfa1 = (int)((uint)(int)a[4] >> 24);
        a = new object[] { t0 + 2000, 0f, 0f, 0f, 0 };
        bool apagado = !(bool)mDest.Invoke(s2, a);
        Exige(enE && sigue && alfa1 < alfa0 && apagado, "al escribir una E (en minuscula, con un teclado de verdad) su tecla se enciende y se apaga sola: " + alfa0 + " -> " + alfa1 + " -> nada");
        Llama(s2, "Backspace");
        a = new object[] { Ahora() + 10, 0f, 0f, 0f, 0 };
        bool borra = (bool)mDest.Invoke(s2, a);
        RectangleF kb = TeclaR(s2, 4, 1);
        Exige(borra && Math.Abs((float)a[1] - (kb.X + kb.Width / 2f)) < 0.5f && ((uint)(int)a[4] & 0xFFFFFF) == 0xE44848, "y al borrar, la de BORRAR, en rojo");
        Llama(s2, "PonNuevo", false);
        Llama(s2, "TypeChar", 'x');
        a = new object[] { Ahora() + 10, 0f, 0f, 0f, 0 };
        Exige(!(bool)mDest.Invoke(s2, a), "con el buscador de siempre no hay destello");
        s2.Dispose();
    }

    // ======================= 4) LA IMAGEN GRANDE =======================
    static void LaImagenGrande()
    {
        Console.WriteLine("\n  4) LA IMAGEN GRANDE SOLO LLEVA LO QUIETO\n");
        int malas = 0; string primero = null; int casos = 0;
        foreach (int[] p in new int[][] { new int[] { 1024, 768 }, new int[] { 1920, 1080 } })
        {
            Control s = Buscador(p[0], p[1], 40);
            Bitmap base0 = Grande(s);
            long f0 = (long)Llama(s, "FirmaFija");
            string[] que = { "otra consulta y otros resultados", "sin resultados", "las tarjetas enfocadas", "un atajo elegido", "otra tecla elegida", "las tarjetas bajadas", "lo escrito a medias" };
            for (int c = 0; c < que.Length; c++)
            {
                if (c == 0) { Pon(s, "Query", "LOS"); Pon(s, "Results", Resultados("los", 23)); }
                else if (c == 1) { Pon(s, "Query", "ZZZ"); Pon(s, "Results", Lista()); }
                else if (c == 2) { Pon(s, "Results", Resultados("vi", 40)); Pon(s, "FocusResults", true); Pon(s, "SelResult", 5); }
                else if (c == 3) { Pon(s, "FocusResults", false); Pon(s, "EnChips", true); Pon(s, "SelChip", 3); }
                else if (c == 4) { Pon(s, "EnChips", false); Pon(s, "SelRow", 3); Pon(s, "SelCol", 5); }
                else if (c == 5) { Pon(s, "resScroll", 3); }
                else Pon(s, "Escrito", "VICENTE FER");
                Bitmap b = Grande(s);
                int d = Distintos(base0, b); long f = (long)Llama(s, "FirmaFija");
                casos++;
                if (d != 0 || f != f0) { malas++; if (primero == null) primero = p[0] + "x" + p[1] + ", " + que[c] + ": " + d + " pixeles distintos" + (f != f0 ? " y la firma cambio" : ""); }
                b.Dispose();
            }
            // Y lo que si la cambia, cambia la firma.
            long fa; Llama(s, "PonAtajos", new List<string> { "Videos" }); fa = (long)Llama(s, "FirmaFija");
            Pon(s, "Accent", Color.FromArgb(40, 90, 200)); long fb = (long)Llama(s, "FirmaFija");
            bool cambia = fa != f0 && fb != fa;
            if (!cambia) { malas++; if (primero == null) primero = "otros atajos u otro color no cambian la firma"; }
            base0.Dispose(); s.Dispose();
        }
        Exige(malas == 0, casos + " cambios de consulta, resultados y seleccion: la imagen grande sale igual pixel a pixel y su firma no se mueve; con otros atajos u otro color, si" + (primero != null ? " -- " + primero : ""));
    }

    // ======================= 5) LAS IMAGENES CHICAS =======================
    static bool Transparente(int argb) { return ((uint)argb >> 24) == 0; }
    // Un pixel (alfa normal) encima de otro opaco, como mezcla la tarjeta grafica.
    static int Mezcla(int s, int d)
    {
        uint a = (uint)s >> 24; uint r = 0xFF000000u;
        for (int sh = 0; sh <= 16; sh += 8)
        {
            uint cs = ((uint)s >> sh) & 0xFF, cd = ((uint)d >> sh) & 0xFF;
            r |= ((cs * a + cd * (255 - a) + 127) / 255) << sh;
        }
        return unchecked((int)r);
    }
    // El interior de una forma redondeada de radio 'rad', sin sus esquinas (donde el suavizado deja ver lo de debajo).
    static bool Interior(Rectangle r, int rad, int x, int y)
    {
        bool enX = x >= r.Left + rad && x < r.Right - rad && y >= r.Top + 2 && y < r.Bottom - 2;
        bool enY = y >= r.Top + rad && y < r.Bottom - rad && x >= r.Left + 2 && x < r.Right - 2;
        return enX || enY;
    }
    static void LasImagenesChicas()
    {
        Console.WriteLine("\n  5) LAS IMAGENES CHICAS\n");
        int malasFuera = 0, malasEntera = 0; string pf = null, pe = null; long fueraPx = 0;
        foreach (int[] p in Pantallas)
        {
            Control s = Buscador(p[0], p[1], 40);
            Rectangle d4; Bitmap b4 = Pieza(s, 4, out d4);
            int n; float el; Array cs = Cartas(s, Ahora() + 10000, out n, out el);
            // Fuera de los trozos de las tarjetas (y la barrita), todo transparente.
            bool[] dentro = new bool[b4.Width * b4.Height];
            for (int i = 0; i < n; i++)
            {
                object c = cs.GetValue(i);
                for (int y = (int)CF(c, "Y0"); y < (int)CF(c, "Y1"); y++) for (int x = (int)CF(c, "X0"); x < (int)CF(c, "X1"); x++)
                    if (x >= 0 && y >= 0 && x < b4.Width && y < b4.Height) dentro[y * b4.Width + x] = true;
            }
            int[] px = Pixeles(b4); int fuera = 0;
            for (int i = 0; i < px.Length; i++) if (!dentro[i] && !Transparente(px[i])) fuera++;
            fueraPx += fuera;
            if (fuera > 0 || n == 0) { malasFuera++; if (pf == null) pf = p[0] + "x" + p[1] + ": " + fuera + " pixeles con algo fuera de las tarjetas (" + n + " trozos)"; }
            // Por trozos (todos ya entrados) = entera: encima de un cielo cualquiera, pixel a pixel.
            int d = 0;
            for (int i = 0; i < n; i++)
            {
                object c = cs.GetValue(i);
                if (CF(c, "Alfa") < 0.999f || CF(c, "Dy") != 0f) { malasEntera++; if (pe == null) pe = "una tarjeta sin acabar de entrar a los 10 s"; }
            }
            for (int i = 0; i < px.Length; i++)
            {
                int cielo = unchecked((int)0xFF0A1428);
                int entera = Mezcla(px[i], cielo), trozos = dentro[i] ? Mezcla(px[i], cielo) : cielo;
                if (entera != trozos) d++;
            }
            if (d != 0) { malasEntera++; if (pe == null) pe = p[0] + "x" + p[1] + ": " + d + " pixeles distintos"; }
            b4.Dispose(); s.Dispose();
        }
        Exige(malasFuera == 0, "fuera de las tarjetas (y de la barrita) la imagen de las tarjetas es transparente del todo, en las seis pantallas" + (pf != null ? " -- " + pf : ""));
        Exige(malasEntera == 0, "asi que dibujarla tarjeta a tarjeta da la misma pantalla que entera: 0 pixeles distintos" + (pe != null ? " -- " + pe : ""));

        // Lo elegido tapa entero lo que tiene debajo: su trozo es opaco donde esta lo de debajo.
        Control s2 = Buscador(1280, 720, 12);
        string[] nom = { "la tecla elegida", "el atajo elegido", "la tarjeta elegida", "BORRAR elegida" };
        for (int k = 0; k < 4; k++)
        {
            Pon(s2, "EnChips", k == 1); Pon(s2, "FocusResults", k == 2);
            if (k == 0) { Pon(s2, "SelRow", 2); Pon(s2, "SelCol", 4); }
            if (k == 1) Pon(s2, "SelChip", 1);
            if (k == 2) Pon(s2, "SelResult", 3);
            if (k == 3) { Pon(s2, "SelRow", 4); Pon(s2, "SelCol", 1); }
            int cual = (k == 2) ? 2 : 0;
            Rectangle d; Bitmap pz = Pieza(s2, cual, out d);
            Rectangle abajo = (k == 0 || k == 3) ? Rectangle.Round(TeclaR(s2, (int)Toma(s2, "SelRow"), (int)Toma(s2, "SelCol"))) : (k == 1) ? Atajos(s2)[1] : (Rectangle)Llama(s2, "CartaRect", 3);
            // Lo de debajo, en la imagen grande o en la de las tarjetas
            int huecos = 0, total = 0;
            if (pz != null)
            {
                int[] px = Pixeles(pz);
                int rad = (k == 1) ? abajo.Height / 2 + 1 : 16;   // el atajo es una pastilla: sus puntas son redondas enteras
                for (int y = abajo.Top; y < abajo.Bottom; y++)
                    for (int x = abajo.Left; x < abajo.Right; x++)
                    {
                        if (!Interior(abajo, rad, x, y)) continue;
                        int lx = x - d.X, ly = y - d.Y; total++;
                        if (lx < 0 || ly < 0 || lx >= pz.Width || ly >= pz.Height || ((uint)px[ly * pz.Width + lx] >> 24) != 255) huecos++;
                    }
                pz.Dispose();
            }
            Exige(pz != null && total > 0 && huecos == 0, nom[k] + " es opaca encima de lo de debajo: lo tapa entero y no se mezcla dos veces" + (huecos > 0 ? " -- " + huecos + " de " + total + " pixeles sin tapar" : ""));
        }
        s2.Dispose();
    }

    // ======================= 6) LA ENTRADA =======================
    static void LaEntrada()
    {
        Console.WriteLine("\n  6) LA ENTRADA DE LAS TARJETAS\n");
        bool mono = true; float prev = -1;
        for (int i = 0; i <= 300; i++) { float e = (float)LlamaS(TSO, "Entrada", (double)i); if (e < prev) mono = false; prev = e; }
        Exige(mono && (float)LlamaS(TSO, "Entrada", -5.0) == 0f && (float)LlamaS(TSO, "Entrada", 240.0) == 1f && (float)LlamaS(TSO, "Entrada", 120.0) > 0.5f,
              "cada tarjeta entra en 240 ms: de 0 a 1, sin volver atras, y frenando al final");
        Control s = Buscador(1280, 720, 12);
        Rectangle d; Bitmap b = Pieza(s, 4, out d); b.Dispose();
        double t0 = Ahora();
        int n; float el;
        // Justo al llegar: la primera empieza ya y las demas despues, una tras otra (60 ms).
        Array despues = Cartas(s, t0 + 90, out n, out el);
        float a0 = CF(despues.GetValue(0), "Alfa"), a1 = CF(despues.GetValue(1), "Alfa"), a2 = CF(despues.GetValue(2), "Alfa");
        float dy0 = CF(despues.GetValue(0), "Dy"), dy2 = CF(despues.GetValue(2), "Dy");
        Exige(a0 > a1 && a1 > a2 && a2 == 0f && dy0 < dy2 && dy2 > 0, "con la primera lista entran una tras otra: a los 90 ms, " + a0.ToString("0.00") + ", " + a1.ToString("0.00") + " y " + a2.ToString("0.00") + " (y las de atras, mas abajo)");
        Array fin = Cartas(s, t0 + 2000, out n, out el);
        bool todas = n > 0; for (int i = 0; i < n; i++) if (CF(fin.GetValue(i), "Alfa") != 1f || CF(fin.GetValue(i), "Dy") != 0f) todas = false;
        Exige(todas, "y a los dos segundos todas quietas en su sitio");
        System.Threading.Thread.Sleep(700);   // de verdad: que las primeras hayan acabado de entrar
        // Otra lista con las tres primeras iguales: esas no vuelven a entrar.
        IList vieja = (IList)Toma(s, "Results"), nueva = Lista();
        for (int i = 0; i < 3; i++) nueva.Add(vieja[i]);
        IList otras = Resultados("zz", 9); foreach (object o in otras) nueva.Add(o);
        Pon(s, "Results", nueva);
        b = Pieza(s, 4, out d); b.Dispose();
        double t1 = Ahora();
        Array cs2 = Cartas(s, t1 + 30, out n, out el);
        bool quietas = CF(cs2.GetValue(0), "Alfa") == 1f && CF(cs2.GetValue(1), "Alfa") == 1f && CF(cs2.GetValue(2), "Alfa") == 1f;
        bool entran = CF(cs2.GetValue(3), "Alfa") < 1f && CF(cs2.GetValue(4), "Alfa") == 0f;
        Exige(quietas && entran, "con otra lista, las que siguen iguales se quedan quietas y solo entran las que cambiaron");
        // Bajar de fila: ya puestas.
        Pon(s, "FocusResults", true); Pon(s, "SelResult", 0);
        Pon(s, "resScroll", 1);
        b = Pieza(s, 4, out d); b.Dispose();
        Array cs3 = Cartas(s, Ahora(), out n, out el);
        bool puestas = n > 0; for (int i = 0; i < n; i++) if (CF(cs3.GetValue(i), "Alfa") != 1f) puestas = false;
        Exige(puestas, "bajando de fila, las tarjetas ya estan puestas: no se hace esperar a quien baja");
        // La elegida entra con la suya.
        Pon(s, "resScroll", 0); Pon(s, "SelResult", 1);
        IList otra = Lista(); foreach (object o in Resultados("ww", 12)) otra.Add(o);
        Pon(s, "Results", otra);
        b = Pieza(s, 4, out d); b.Dispose();
        double t2 = Ahora();
        Cartas(s, t2 + 100, out n, out el);
        Array cs4 = Cartas(s, t2 + 100, out n, out el);
        Exige(el > 0f && el < 1f && Math.Abs(el - CF(cs4.GetValue(1), "Alfa")) < 1e-4, "y la tarjeta elegida (que va aparte) entra a la vez que la suya: " + el.ToString("0.00"));
        s.Dispose();
    }

    // ======================= 7) LOS ATAJOS, CON EL FORMULARIO =======================
    static void LosAtajos(object form)
    {
        Console.WriteLine("\n  7) LOS ATAJOS, CON DATOS DE VERDAD\n");
        Control so = Buscador(1280, 720, 0);
        Pon(form, "searchOverlay", so); Pon(form, "searchOpen", true);
        Pon(form, "gpuEnabled", true); Pon(form, "gpuPanel", Activator.CreateInstance(TPanel, true));
        Pon(form, "buscadorPortadas", true);
        string noche = (string)LlamaS(TF, "ClaveNoche", DateTime.Now);
        Pon(form, "nocheLeida", true); Pon(form, "nocheClave", noche);
        Pon(form, "nocheCuenta", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        Pon(form, "nocheOrden", new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase));
        IList indice = Lista();
        object kar = Album("Karaoke Exitos Vol 1", "Varios", "/musica/Karaoke/Exitos", 30, null);
        object kar2 = Album("Canta Karaoke", "Varios", "/musica/Otros/Canta", 10, null);
        object norm = Album("Grandes Exitos", "Bronco", "/musica/Bronco/Grandes", 12, null);
        foreach (object a in new object[] { kar, kar2, norm })
        {
            indice.Add(Item(1, (string)Toma(a, "Artist") + " - " + (string)Toma(a, "Name"), a, null));
            foreach (object t in (IList)Toma(a, "Tracks")) indice.Add(Item(2, (string)Toma(t, "Artist") + " - " + (string)Toma(t, "Title"), a, t));
        }
        Pon(form, "searchIndex", indice);
        IList videos = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        Pon(form, "videoAlbums", videos);
        Llama(form, "AjustaBuscadorPortadas");
        Exige((bool)Toma(so, "Nuevo"), "con la tarjeta y el interruptor, manda el buscador nuevo");
        Pon(form, "karaokeLista", null);
        Llama(form, "ArmaAtajos");
        List<string> textos = new List<string>((IEnumerable<string>)Toma(so, "atajos"));
        Exige(textos.Count == 1 && textos[0] == "Karaoke", "sin nada pedido, sin videos y sin busquedas solo sale Karaoke, que si hay (" + string.Join(" | ", textos.ToArray()) + ")");
        // Lo mas pedido hoy: la cuenta de la noche, de mas a menos, solo lo que sigue en la biblioteca.
        Dictionary<string, int> cuenta = (Dictionary<string, int>)Toma(form, "nocheCuenta");
        Dictionary<string, long> orden = (Dictionary<string, long>)Toma(form, "nocheOrden");
        IList tn = (IList)Toma(norm, "Tracks");
        cuenta[(string)Toma(tn[3], "Path")] = 5; orden[(string)Toma(tn[3], "Path")] = 1;
        cuenta[(string)Toma(tn[7], "Path")] = 2; orden[(string)Toma(tn[7], "Path")] = 2;
        cuenta["/ya/no/existe.mp3"] = 9; orden["/ya/no/existe.mp3"] = 3;
        cuenta[(string)Toma(tn[1], "Path")] = 2; orden[(string)Toma(tn[1], "Path")] = 4;
        videos.Add(Album("Videos 80", "Varios", "/videos/80", 0, null));
        Llama(form, "ArmaAtajos");
        textos = new List<string>((IEnumerable<string>)Toma(so, "atajos"));
        Exige(textos.Count == 3 && textos[0] == "Lo m\u00e1s pedido hoy" && textos[1] == "Videos" && textos[2] == "Karaoke", "con canciones pedidas esta noche y videos: lo mas pedido, videos y karaoke, en ese orden");
        IList ped = (IList)Llama(form, "PedidasHoy");
        bool bienOrden = ped.Count == 3 && ReferenceEquals(Toma(ped[0], "TrackRef"), tn[3]) && ReferenceEquals(Toma(ped[1], "TrackRef"), tn[1]) && ReferenceEquals(Toma(ped[2], "TrackRef"), tn[7]);
        Exige(bienOrden, "lo mas pedido: de mas a menos pedida (y a igualdad, la ultima pedida primero); la que ya no esta en la biblioteca no sale");
        IList ks = (IList)Llama(form, "DiscosKaraoke");
        Exige(ks.Count == 2 && ReferenceEquals(Toma(ks[0], "AlbumRef"), kar2) && ReferenceEquals(Toma(ks[1], "AlbumRef"), kar), "karaoke: los discos cuyo genero es Karaoke (por su nombre o su carpeta), por nombre");
        // Usar un atajo que pone resultados.
        Llama(form, "UsaAtajo", 0);
        Exige((string)Toma(so, "ModoAtajo") == "Lo m\u00e1s pedido hoy" && ((IList)Toma(so, "Results")).Count == 3 && (bool)Toma(so, "FocusResults") && (string)Llama(so, "TextoCuenta") == "3 resultados",
              "al usarlo, sus canciones salen como tarjetas, la seleccion salta a la primera y la casilla dice cual es y cuantas");
        Llama(so, "Backspace");
        Exige(Toma(so, "ModoAtajo") == null && ((IList)Toma(so, "Results")).Count == 0 && !(bool)Toma(so, "FocusResults"), "borrar sin nada escrito sale del atajo");
        // Buscaste: las dos ultimas con las que se eligio algo, sin repetir, la mas nueva primero.
        IList res = Resultados("x", 3);
        string[] buscado = { "los tigres", "bukis", "LOS TIGRES", "Juan Gabriel y una busqueda muy larga" };
        foreach (string q in buscado) { Pon(so, "Query", q); Llama(form, "ApuntaBusquedaGpu", res[2]); }
        textos = new List<string>((IEnumerable<string>)Toma(so, "atajos"));
        Exige(textos.Count == 5 && textos[3] == "Buscaste: Juan Gabriel Y Un\u2026" && textos[4] == "Buscaste: Los Tigres",
              "\"Buscaste\": las dos ultimas, sin repetir, la mas nueva primero, con mayusculas y cortada si es larga (" + textos[3] + " | " + textos[4] + ")");
        ((List<string>)Toma(form, "buscadas")).Clear();
        Pon(so, "Query", "bronco"); Llama(form, "ApuntaBusquedaGpu", res[2]);
        Pon(so, "Query", "BRONCO "); Llama(form, "ApuntaBusquedaGpu", res[2]);
        List<string> una = new List<string>((IEnumerable<string>)Toma(form, "buscadas"));
        Exige(una.Count == 1 && una[0] == "BRONCO", "la misma busqueda con otras mayusculas no se repite: queda una, la ultima (" + string.Join(" | ", una.ToArray()) + ")");
        Pon(so, "Query", "CHOMPS21"); Llama(form, "ApuntaBusquedaGpu", res[2]);
        Pon(so, "Query", "chomps21"); Llama(form, "ApuntaBusquedaGpu", res[2]);
        textos = new List<string>((IEnumerable<string>)Toma(so, "atajos"));
        bool sinClave = true; foreach (string x in textos) if (x.ToUpperInvariant().Contains("CHOMPS")) sinClave = false;
        List<string> busc = new List<string>((IEnumerable<string>)Toma(form, "buscadas"));
        foreach (string x in busc) if (x.ToUpperInvariant().Contains("CHOMPS")) sinClave = false;
        Exige(sinClave, "la clave de servicio nunca se apunta, ni en mayusculas ni en minusculas");
        object servicio = Item(3, "+4 cr\u00e9ditos", null, null);
        Pon(so, "Query", "rocio"); Llama(form, "ApuntaBusquedaGpu", servicio);
        Pon(so, "Query", "r"); Llama(form, "ApuntaBusquedaGpu", res[0]);
        busc = new List<string>((IEnumerable<string>)Toma(form, "buscadas"));
        Exige(!busc.Contains("rocio") && !busc.Contains("r"), "ni lo que acaba en la tarjeta de servicio, ni una letra suelta");
        Pon(so, "ModoAtajo", "Karaoke"); Pon(so, "Query", "karaoke x"); Llama(form, "ApuntaBusquedaGpu", res[1]);
        busc = new List<string>((IEnumerable<string>)Toma(form, "buscadas"));
        Pon(so, "ModoAtajo", null);
        Exige(!busc.Contains("karaoke x"), "ni lo que puso un atajo");
        Pon(form, "buscadasNoche", "1999-01-01");
        Llama(form, "ArmaAtajos");
        textos = new List<string>((IEnumerable<string>)Toma(so, "atajos"));
        bool sinViejas = true; foreach (string x in textos) if (x.StartsWith("Buscaste")) sinViejas = false;
        Exige(sinViejas, "las busquedas de otra noche ya no salen");
        // Buscaste: vuelve a buscar y salta a los resultados.
        Pon(form, "buscadasNoche", noche);
        ((List<string>)Toma(form, "buscadas")).Clear(); ((List<string>)Toma(form, "buscadas")).Add("bronco");
        Llama(form, "ArmaAtajos");
        string consultada = null;
        Pon(so, "OnQueryChanged", (Action<string>)delegate (string q) { consultada = q; Pon(so, "Results", Resultados("br", 5)); });
        Pon(so, "Escrito", "algo"); Pon(so, "Query", "algo");
        Llama(form, "UsaAtajo", 3);
        Exige(consultada == "bronco" && (string)Toma(so, "Escrito") == "bronco" && (bool)Toma(so, "FocusResults") && (int)Toma(so, "SelResult") == 0,
              "\"Buscaste: Bronco\" vuelve a buscar \"bronco\" y la seleccion salta a la primera tarjeta");
        Pon(form, "searchOpen", false);
        so.Dispose();
    }

    // Los prefijos de pista ("03 - Dreamer") no salen en la tarjeta, como en el resto del dibujo por tarjeta; los numeros
    // que son parte del nombre ("99 Luftballons", "1979"), si.
    static string TituloDeTarjeta(Control so, object it)
    {
        object[] a = new object[] { it, null, Color.Empty, null, null };
        MetodoN(TSO, "DatosCarta", 5).Invoke(so, a);
        return (string)a[3];
    }
    static void LosPrefijos()
    {
        Control so = Buscador(1280, 720, 4);
        object alb = Album("Disco", "Ozzy", "/m/ozzy", 3, null);
        string[][] casos = { new string[] { "03 - Dreamer", "Dreamer" }, new string[] { "02-Crazy Train", "Crazy Train" },
                             new string[] { "01. Titulo", "Titulo" }, new string[] { "99 Luftballons", "99 Luftballons" },
                             new string[] { "1979", "1979" }, new string[] { "3.14 Pi", "3.14 Pi" } };
        int malas = 0; string pm = null;
        foreach (string[] c in casos)
        {
            object tr = Activator.CreateInstance(TTrack, true);
            TTrack.GetField("Title").SetValue(tr, c[0]); TTrack.GetField("Artist").SetValue(tr, "Ozzy"); TTrack.GetField("Path").SetValue(tr, "/m/" + c[0] + ".mp3");
            string sale = TituloDeTarjeta(so, Item(2, "Ozzy - " + c[0], alb, tr));
            if (sale != c[1]) { malas++; if (pm == null) pm = "'" + c[0] + "' sale '" + sale + "'"; }
        }
        Exige(malas == 0, "en la tarjeta de una cancion no sale su numero de pista (03 - Dreamer -> Dreamer), y si los numeros del nombre (99 Luftballons, 1979)" + (pm != null ? " -- " + pm : ""));
        so.Dispose();
    }

    // ======================= 8) LAS PORTADAS =======================
    static void LasPortadas(object form)
    {
        Console.WriteLine("\n  8) LAS PORTADAS: SE PIDEN CON LA VISTA QUIETA, SOLO LAS QUE SE VEN Y UNA VEZ\n");
        todasEnMemoria = false; enMemoria.Clear(); pedidas.Clear();
        Control s = Buscador(1280, 720, 40);
        Rectangle d; Bitmap b = Pieza(s, 4, out d); b.Dispose();
        int filasV = I(Reparto(s), "Filas");
        Llama(s, "VersionCartas");
        Exige(pedidas.Count == 0, "recien hechas las tarjetas no se pide ninguna al disco (quien teclea deprisa no encarga nada)");
        System.Threading.Thread.Sleep(300);
        Llama(s, "VersionCartas");
        int visibles = 0, conPortada = 0;
        IList res = (IList)Toma(s, "Results");
        for (int i = 0; i < filasV * 2; i++) { visibles++; if (Toma(Toma(res[i], "AlbumRef"), "CoverPath") != null) conPortada++; }
        bool soloVistas = true;
        foreach (string k in pedidas) { bool vista = false; for (int i = 0; i < filasV * 2; i++) if (k == (string)Toma(Toma(res[i], "AlbumRef"), "CoverPath")) vista = true; if (!vista) soloVistas = false; }
        Exige(pedidas.Count == conPortada && soloVistas, "con la vista quieta un cuarto de segundo, se piden las " + pedidas.Count + " que faltan de las " + visibles + " tarjetas que se ven (y ninguna de las " + (40 - visibles) + " de mas abajo)");
        int antes = pedidas.Count;
        System.Threading.Thread.Sleep(300); Llama(s, "VersionCartas");
        Exige(pedidas.Count == antes, "y una sola vez");
        // Llega una: se rehace, pero juntando las que llegan seguidas.
        string llega = pedidas[0];
        enMemoria[llega] = portadaComun;
        int v0 = (int)Llama(s, "VersionCartas");
        Exige((bool)Llama(s, "EsperaPortada", llega) && !(bool)Llama(s, "EsperaPortada", "/otra/cualquiera.jpg"), "la tarjeta sabe que portada esperaba");
        Pon(s, "PortadasVer", (int)Toma(s, "PortadasVer") + 1);
        int v1 = (int)Llama(s, "VersionCartas");
        Pon(s, "PortadasVer", (int)Toma(s, "PortadasVer") + 1);
        int v2 = (int)Llama(s, "VersionCartas");
        System.Threading.Thread.Sleep(150);
        int v3 = (int)Llama(s, "VersionCartas");
        Exige(v1 == v0 + 1 && v2 == v1 && v3 == v2 + 1, "al llegar portadas las tarjetas se rehacen, pero no dos veces seguidas en menos de 120 ms (se juntan)");
        b = Pieza(s, 4, out d); b.Dispose();
        Exige(!(bool)Llama(s, "EsperaPortada", llega), "y ya no la espera");
        // Las miniaturas no crecen sin fin.
        todasEnMemoria = true;
        for (int k = 0; k < 20; k++) { Pon(s, "Results", Resultados("m" + k, 40)); b = Pieza(s, 4, out d); b.Dispose(); }
        IDictionary minis = (IDictionary)Toma(s, "miniaturas");
        Exige(minis.Count <= 64, "las portadas ya escaladas se guardan, pero como mucho 64 (" + minis.Count + ")");
        s.Dispose();
        // El formulario: una portada que llega y la esperaba una tarjeta, sube la version; otra, no.
        Control so = Buscador(1280, 720, 12);
        todasEnMemoria = false; enMemoria.Clear();
        b = Pieza(so, 4, out d); b.Dispose();
        Pon(form, "searchOverlay", so); Pon(form, "searchOpen", true);
        string esperada = null;
        IList r2 = (IList)Toma(so, "Results");
        for (int i = 0; i < r2.Count && esperada == null; i++) esperada = (string)Toma(Toma(r2[i], "AlbumRef"), "CoverPath");
        int pv = (int)Toma(so, "PortadasVer");
        Llama(form, "PortadaLlegoBuscador", "/no/la/espera/nadie.jpg");
        int pv1 = (int)Toma(so, "PortadasVer");
        Llama(form, "PortadaLlegoBuscador", esperada);
        Exige(pv1 == pv && (int)Toma(so, "PortadasVer") == pv + 1, "cuando llega del disco una portada que esperaba una tarjeta, las tarjetas se rehacen; si no, nada");
        Pon(form, "searchOpen", false); todasEnMemoria = true;
        so.Dispose();
    }

    // ======================= 9) LA TARJETA IMITADA =======================
    static uint Nada(IntPtr self) { return 0; }
    static uint Memoria(IntPtr self) { return 256u * 1024 * 1024; }
    static readonly List<object> vivos = new List<object>();
    static IntPtr Puntero(string tipo, string metodo)
    {
        Delegate del = Delegate.CreateDelegate(TD3D.GetNestedType(tipo, TODO), typeof(BuscaPortadas).GetMethod(metodo, TODO));
        vivos.Add(del); return Marshal.GetFunctionPointerForDelegate(del);
    }
    sealed class Envio { public int Tris; public byte[] Datos; public IntPtr Tex; public bool ConTextura; }
    static readonly List<Envio> envios = new List<Envio>();
    sealed class Estado { public uint OpColor = 4; public IntPtr Tex; }
    static readonly Dictionary<long, Estado> estados = new Dictionary<long, Estado>();
    static Estado EstadoDe(IntPtr self) { Estado e; if (!estados.TryGetValue(self.ToInt64(), out e)) { e = new Estado(); estados[self.ToInt64()] = e; } return e; }
    static int DrawUp(IntPtr self, uint prim, uint count, IntPtr data, uint stride)
    {
        Estado st = EstadoDe(self);
        Envio e = new Envio(); e.Tris = (int)count; e.Datos = new byte[count * 3 * stride]; Marshal.Copy(data, e.Datos, 0, e.Datos.Length);
        e.Tex = st.Tex; e.ConTextura = st.OpColor == 4; envios.Add(e); return 0;
    }
    static int SetRS(IntPtr self, uint a, uint b) { return 0; }
    static int SetU1(IntPtr self, uint a) { return 0; }
    static int SetTSS(IntPtr self, uint stage, uint type, uint val) { if (type == 1) EstadoDe(self).OpColor = val; return 0; }
    static int SetSamp(IntPtr self, uint stage, uint type, uint val) { return 0; }
    static int SetTex(IntPtr self, uint stage, IntPtr t) { EstadoDe(self).Tex = t; return 0; }
    static int Hr(IntPtr self) { return 0; }
    static int Present(IntPtr self, IntPtr a, IntPtr b, IntPtr c, IntPtr d) { return 0; }
    static void Enchufa(object d, string campo, string metodo)
    {
        FieldInfo f = Campo(TDev, campo);
        Delegate del = Delegate.CreateDelegate(f.FieldType, typeof(BuscaPortadas).GetMethod(metodo, TODO));
        vivos.Add(del); f.SetValue(d, del);
    }
    sealed class TexImitada { public int W, H; public IntPtr Mem; }
    static readonly Dictionary<long, TexImitada> creadas = new Dictionary<long, TexImitada>();
    static IntPtr vtTex; static int offPitch, offBits;
    static Delegate AlVuelo(string tipo, string destino, bool conRect)
    {
        Type td = TD3D.GetNestedType(tipo, TODO);
        ParameterInfo[] ps = td.GetMethod("Invoke").GetParameters();
        Type[] pt = new Type[ps.Length]; for (int i = 0; i < ps.Length; i++) pt[i] = ps[i].ParameterType;
        DynamicMethod dm = new DynamicMethod(destino + "Imitado", typeof(int), pt, typeof(BuscaPortadas).Module, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Conv_U); il.Emit(OpCodes.Ldarg_3);
        if (conRect) il.Emit(OpCodes.Conv_U);
        il.Emit(OpCodes.Call, typeof(BuscaPortadas).GetMethod(destino, TODO));
        il.Emit(OpCodes.Ret);
        Delegate d = dm.CreateDelegate(td); vivos.Add(d); return d;
    }
    static void PreparaTexturas()
    {
        if (vtTex != IntPtr.Zero) return;
        Type tlr = TD3D.GetNestedType("LockedRect", TODO);
        offPitch = (int)Marshal.OffsetOf(tlr, "Pitch"); offBits = (int)Marshal.OffsetOf(tlr, "Bits");
        vtTex = Marshal.AllocHGlobal(64 * IntPtr.Size);
        IntPtr nada = Puntero("UintSelf", "Nada"), suelta = Puntero("UintSelf", "Suelta"), desb = Puntero("UnlockRectDel", "Desbloquea");
        for (int i = 0; i < 64; i++) Marshal.WriteIntPtr(vtTex, i * IntPtr.Size, nada);
        Marshal.WriteIntPtr(vtTex, 2 * IntPtr.Size, suelta);
        Marshal.WriteIntPtr(vtTex, 20 * IntPtr.Size, desb);
        Marshal.WriteIntPtr(vtTex, 19 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(AlVuelo("LockRectDel", "Bloquea", false)));
    }
    static int CreaTex(IntPtr self, uint w, uint h, uint levels, uint usage, uint format, uint pool, out IntPtr tex, IntPtr shared)
    {
        PreparaTexturas();
        IntPtr o = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(o, vtTex);
        TexImitada t = new TexImitada(); t.W = (int)w; t.H = (int)h; t.Mem = Marshal.AllocHGlobal((int)(w * h * 4));
        creadas[o.ToInt64()] = t; tex = o;
        return 0;
    }
    static int Bloquea(IntPtr self, IntPtr lr, IntPtr rect)
    {
        TexImitada t = creadas[self.ToInt64()];
        Marshal.WriteInt32(lr, offPitch, t.W * 4); Marshal.WriteIntPtr(lr, offBits, t.Mem);
        return 0;
    }
    static int Desbloquea(IntPtr self, uint level) { return 0; }
    static uint Suelta(IntPtr self) { TexImitada t; if (creadas.TryGetValue(self.ToInt64(), out t)) { creadas.Remove(self.ToInt64()); Marshal.FreeHGlobal(t.Mem); } return 0; }
    static object Tarjeta(int w, int h)
    {
        PreparaTexturas();
        IntPtr vt = Marshal.AllocHGlobal(128 * IntPtr.Size);
        for (int i = 0; i < 128; i++) Marshal.WriteIntPtr(vt, i * IntPtr.Size, Puntero("UintSelf", "Nada"));
        Marshal.WriteIntPtr(vt, 4 * IntPtr.Size, Puntero("UintSelf", "Memoria"));
        IntPtr obj = Marshal.AllocHGlobal(16); Marshal.WriteIntPtr(obj, vt);
        object d = FormatterServices.GetUninitializedObject(TDev);
        Pon(d, "dev", obj); Pon(d, "stTex", new IntPtr(-1));
        object pp = Activator.CreateInstance(TD3D.GetNestedType("PresentParams", TODO));
        pp.GetType().GetField("BackBufferWidth").SetValue(pp, (uint)w); pp.GetType().GetField("BackBufferHeight").SetValue(pp, (uint)h);
        Pon(d, "pp", pp);
        Enchufa(d, "createTex", "CreaTex"); Enchufa(d, "setRS", "SetRS"); Enchufa(d, "setFVF", "SetU1"); Enchufa(d, "setTSS", "SetTSS");
        Enchufa(d, "setSamp", "SetSamp"); Enchufa(d, "setTex", "SetTex"); Enchufa(d, "drawUp", "DrawUp"); Enchufa(d, "present", "Present");
        Enchufa(d, "begin", "Hr"); Enchufa(d, "end", "Hr"); Enchufa(d, "testCoop", "Hr");
        string[] arr = { "cubeFaceV", "cubeBatchV", "cbx", "cbz", "cbo" }; int[] lar = { 6, 24, 4, 4, 4 };
        for (int i = 0; i < arr.Length; i++) { FieldInfo f = Campo(TDev, arr[i]); if (f != null && f.GetValue(d) == null) f.SetValue(d, Array.CreateInstance(f.FieldType.GetElementType(), lar[i])); }
        if (!(bool)Llama(d, "InitStates")) throw new Exception("la tarjeta imitada no arranca");
        return d;
    }
    static Control Panel(int w, int h)
    {
        object sf = Activator.CreateInstance(TStar, TODO, null, new object[] { IntPtr.Zero, w, h, 4321 }, null);
        Pon(sf, "gpu", Tarjeta(w, h)); Pon(sf, "seed0", 4321); Llama(sf, "BuildStars", w, h, 4321);
        Control p = (Control)Activator.CreateInstance(TPanel, true);
        p.SetBounds(0, 0, w, h); Pon(p, "stars", sf);
        return p;
    }
    static List<Envio> Cuadro(Control p)
    {
        envios.Clear();
        if (!(bool)Llama(p, "RenderFrame")) throw new Exception("el cuadro no se presenta");
        List<Envio> r = new List<Envio>(envios); envios.Clear(); return r;
    }
    static float VF(byte[] d, int vert, int off) { return BitConverter.ToSingle(d, vert * 28 + off); }
    static uint VC(byte[] d, int vert) { return BitConverter.ToUInt32(d, vert * 28 + 16); }
    static Bitmap[] piezas = new Bitmap[5]; static Rectangle[] piezasRect = new Rectangle[5]; static int[] piezasVer = new int[5];
    static Control soGpu; static double relojCartas;
    static int LlenaGpu(Array buf, out float el)
    {
        object[] a = new object[] { buf, relojCartas, 0f };
        int n = (int)MetodoN(TSO, "LlenaCartas", 3).Invoke(soGpu, a);
        el = (float)a[2]; return n;
    }
    static int FillGpu<T>(T[] buf, out float el) { return LlenaGpu(buf, out el); }
    static void LaTarjetaImitada()
    {
        Console.WriteLine("\n  9) LA TARJETA IMITADA: LAS TARJETAS VAN POR TROZOS, DEBAJO DE LA ELEGIDA\n");
        const int W = 1280, H = 720;
        Control p = Panel(W, H);
        Pon(p, "TransicionesTacto", false);   // sin el fundido de la seleccion: aqui se mira solo la entrada
        soGpu = Buscador(W, H, 12);
        Pon(soGpu, "FocusResults", true); Pon(soGpu, "SelResult", 3);
        for (int k = 0; k < 5; k++)
        {
            Rectangle d; piezas[k] = (k == 3) ? null : Pieza(soGpu, k, out d);
            piezasRect[k] = d; piezasVer[k] = 1;
        }
        Pon(p, "PiezasN", 5);
        Pon(p, "GetPieza", (Func<int, Bitmap>)delegate (int i) { return piezas[i]; });
        Pon(p, "GetPiezaRect", (Func<int, Rectangle>)delegate (int i) { return piezasRect[i]; });
        Pon(p, "GetPiezaVersion", (Func<int, int>)delegate (int i) { return piezasVer[i]; });
        Pon(p, "GetCartas", Delegate.CreateDelegate(Campo(TPanel, "GetCartas").FieldType, typeof(BuscaPortadas).GetMethod("FillGpu", TODO).MakeGenericMethod(TCarta)));
        relojCartas = Ahora() + 60000;
        Cuadro(p);
        List<Envio> es = Cuadro(p);
        IntPtr[] tex = (IntPtr[])Toma(p, "piezaTex");
        int i4 = -1, i2 = -1, i0 = -1;
        for (int i = 0; i < es.Count; i++) { if (es[i].Tex == tex[4] && es[i].ConTextura) i4 = i; if (es[i].Tex == tex[2] && es[i].ConTextura) i2 = i; if (es[i].Tex == tex[0] && es[i].ConTextura) i0 = i; }
        int n; float el; Array cs = Cartas(soGpu, relojCartas, out n, out el);
        int vistas = 2 * I(Reparto(soGpu), "Filas");
        Exige(i4 >= 0 && es[i4].Tris == 2 * n && n == vistas + 1, "las tarjetas van en UN envio de " + (i4 >= 0 ? es[i4].Tris / 2 : 0) + " trozos (las " + vistas + " que se ven y la barrita), no un rectangulo entero");
        Exige(i4 >= 0 && i2 > i4 && i0 < 0, "y van ANTES que la tarjeta elegida, que se dibuja encima (con las tarjetas enfocadas no hay tecla elegida)");
        // Cada trozo, en su sitio y con su trozo de la textura.
        bool sitio = i4 >= 0;
        if (sitio)
        {
            int[] tw = (int[])Toma(p, "piezaW"), th = (int[])Toma(p, "piezaH"); float[] pu = (float[])Toma(p, "piezaU"), pv = (float[])Toma(p, "piezaV");
            for (int k = 0; k < n && sitio; k++)
            {
                object c = cs.GetValue(k);
                float x0 = piezasRect[4].X + CF(c, "X0") - 0.5f, y0 = piezasRect[4].Y + CF(c, "Y0") - 0.5f;
                float u0 = CF(c, "X0") / tw[4] * pu[4], v1 = CF(c, "Y1") / th[4] * pv[4];
                byte[] dd = es[i4].Datos;
                if (Math.Abs(VF(dd, k * 6, 0) - x0) > 1e-3 || Math.Abs(VF(dd, k * 6, 4) - y0) > 1e-3 || Math.Abs(VF(dd, k * 6, 20) - u0) > 1e-6 || Math.Abs(VF(dd, k * 6 + 2, 24) - v1) > 1e-6) sitio = false;
                if ((VC(dd, k * 6) >> 24) != 255) sitio = false;
            }
        }
        Exige(sitio, "cada trozo va en su sitio de la pantalla, con su trozo de la textura y del todo visible cuando ya entro");
        // Entrando: la transparencia de cada una, y la elegida con la suya.
        relojCartas = Ahora() - 100000;   // cuando aun no han empezado a entrar
        Pon(soGpu, "Results", Resultados("ot", 12)); Pon(soGpu, "SelResult", 1);
        Rectangle d4; piezas[4] = Pieza(soGpu, 4, out d4); piezasRect[4] = d4; piezasVer[4]++;
        Rectangle d2; piezas[2] = Pieza(soGpu, 2, out d2); piezasRect[2] = d2; piezasVer[2]++;
        relojCartas = Ahora() + 100;
        Cuadro(p); es = Cuadro(p);
        tex = (IntPtr[])Toma(p, "piezaTex");
        i4 = -1; i2 = -1;
        for (int i = 0; i < es.Count; i++) { if (es[i].Tex == tex[4] && es[i].ConTextura) i4 = i; if (es[i].Tex == tex[2] && es[i].ConTextura) i2 = i; }
        cs = Cartas(soGpu, relojCartas, out n, out el);
        bool alfas = i4 >= 0 && i2 >= 0;
        int m = 0;
        if (alfas)
            for (int k = 0; k < n; k++)
            {
                int a = (int)(CF(cs.GetValue(k), "Alfa") * 255f + 0.5f);
                if (a <= 0) continue;
                if (Math.Abs((int)(VC(es[i4].Datos, m * 6) >> 24) - a) > 1) alfas = false;
                m++;
            }
        uint a2 = alfas ? (VC(es[i2].Datos, 0) >> 24) : 0;
        Exige(alfas && es[i4].Tris == 2 * m && m < n, "entrando, cada trozo lleva su transparencia (las que aun no empezaron, ni se mandan: " + m + " de " + n + ")");
        Exige(alfas && Math.Abs((int)a2 - (int)(el * 255f)) <= 2 && el < 1f, "y la tarjeta elegida, la misma que la suya (" + a2 + " de 255)");
        // Sin la pieza de las tarjetas (el buscador de siempre), la elegida no hereda nada.
        piezas[4] = null; piezasVer[4]++;
        Cuadro(p); es = Cuadro(p);
        tex = (IntPtr[])Toma(p, "piezaTex");
        i2 = -1; for (int i = 0; i < es.Count; i++) if (es[i].Tex == tex[2] && es[i].ConTextura) i2 = i;
        Exige(i2 >= 0 && (VC(es[i2].Datos, 0) >> 24) == 255 && (float)Toma(p, "CartaElegidaAlfa") == 1f, "sin la pieza de las tarjetas, la tarjeta elegida va entera como siempre");
        LaVentana(p);
        soGpu.Dispose();
    }
    // LA VENTANA DEL BUSCADOR sube al abrir y baja al cerrar (BuildWinState). Aqui la casilla, lo elegido y las tarjetas
    // van en piezas: tienen que ir con ella, o subiria sin ellas y saldrian de golpe al llegar.
    static object ventana;
    static T Ventana<T>() { return (T)ventana; }
    static bool piezasSuben;
    static object Ven(bool has, bool ocultaUi, Bitmap bmp, float dy)
    {
        Type tw = TF.Assembly.GetType("Rocola.GpuWin", true);
        object w = Activator.CreateInstance(tw);
        tw.GetField("Has").SetValue(w, has); tw.GetField("HideUi").SetValue(w, ocultaUi); tw.GetField("Bmp").SetValue(w, bmp);
        tw.GetField("Robada").SetValue(w, false); tw.GetField("Scale").SetValue(w, 1f); tw.GetField("ScaleY").SetValue(w, 1f);
        tw.GetField("Dy").SetValue(w, dy); tw.GetField("Alpha").SetValue(w, 1f);
        return w;
    }
    static int Busca(List<Envio> es, IntPtr tex) { for (int i = 0; i < es.Count; i++) if (es[i].Tex == tex && es[i].ConTextura) return i; return -1; }
    // Lo mas alto del primer rectangulo de un envio (sus seis vertices; la tarjeta corre medio pixel, como D3D9 pide).
    static float Arriba(Envio e) { float y = float.MaxValue; for (int v = 0; v < 6; v++) y = Math.Min(y, VF(e.Datos, v, 4)); return y; }
    static void LaVentana(Control p)
    {
        Type tw = TF.Assembly.GetType("Rocola.GpuWin", true);
        Pon(p, "GetWin", Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(tw), typeof(BuscaPortadas).GetMethod("Ventana", TODO).MakeGenericMethod(tw)));
        Pon(p, "GetPiezasSuben", (Func<bool>)delegate { return piezasSuben; });
        // Todo quieto, con la casilla, la tarjeta elegida y las tarjetas: donde van sin ventana.
        Pon(soGpu, "Results", Resultados("vi", 12)); Pon(soGpu, "FocusResults", true); Pon(soGpu, "SelResult", 3);
        for (int k = 0; k < 5; k++) { Rectangle d; piezas[k] = (k == 3) ? null : Pieza(soGpu, k, out d); piezasRect[k] = d; piezasVer[k]++; }
        relojCartas = Ahora() + 60000;
        ventana = Ven(false, false, null, 0f);
        Cuadro(p); List<Envio> quieto = Cuadro(p);
        IntPtr[] tex = (IntPtr[])((IntPtr[])Toma(p, "piezaTex")).Clone();
        int q1 = Busca(quieto, tex[1]), q2 = Busca(quieto, tex[2]), q4 = Busca(quieto, tex[4]);
        const float dy = 137f;
        // ABRIENDO: la ventana es la propia imagen de la UI, corrida; encima, las piezas corridas lo mismo.
        piezasSuben = true; ventana = Ven(true, true, null, dy);
        List<Envio> abre = Cuadro(p);
        int a1 = Busca(abre, tex[1]), a2 = Busca(abre, tex[2]), a4 = Busca(abre, tex[4]);
        bool corridas = q1 >= 0 && q2 >= 0 && q4 >= 0 && a1 >= 0 && a2 >= 0 && a4 >= 0 && a4 < a2
            && Math.Abs(Arriba(abre[a1]) - Arriba(quieto[q1]) - dy) < 1e-3
            && Math.Abs(Arriba(abre[a2]) - Arriba(quieto[q2]) - dy) < 1e-3
            && Math.Abs(Arriba(abre[a4]) - Arriba(quieto[q4]) - dy) < 1e-3
            && Math.Abs(VF(abre[a4].Datos, 0, 0) - VF(quieto[q4].Datos, 0, 0)) < 1e-3 && abre[a4].Tris == quieto[q4].Tris;
        Exige(corridas, "al abrir, la casilla, la tarjeta elegida y las tarjetas suben con la ventana: las mismas, " + dy + " px mas abajo y las tarjetas debajo de la elegida");
        piezasSuben = false;
        List<Envio> abreViejo = Cuadro(p);
        Exige(Busca(abreViejo, tex[1]) < 0 && Busca(abreViejo, tex[2]) < 0 && Busca(abreViejo, tex[4]) < 0,
              "y si el formulario dice que no hay buscador que vaya con la ventana, no se dibuja ninguna pieza con ella");
        // CERRANDO: la ventana se queda con las texturas ya subidas de las piezas, sin copiar nada.
        Llama(p, "RobaPiezasParaVentana");
        IntPtr[] ahora = (IntPtr[])Toma(p, "piezaTex");
        int enVentana = (int)Llama(p, "PiezasEnLaVentana");
        Exige(enVentana == 3 && ahora[1] == IntPtr.Zero && ahora[2] == IntPtr.Zero && ahora[4] == IntPtr.Zero && creadas.ContainsKey(tex[4].ToInt64()),
              "al cerrar, la ventana se queda con las texturas de la casilla, la tarjeta elegida y las tarjetas (" + enVentana + "), sin copiar ni subir nada");
        // Si la ventana no llega a salir (el buscador sigue abierto), las piezas se vuelven a subir y lo que se llevo se suelta.
        ventana = Ven(false, false, null, 0f);
        List<Envio> sigue = Cuadro(p);
        IntPtr[] otra = (IntPtr[])((IntPtr[])Toma(p, "piezaTex")).Clone();
        Exige(otra[1] != IntPtr.Zero && otra[4] != IntPtr.Zero && Busca(sigue, otra[1]) >= 0 && Busca(sigue, otra[4]) >= 0
              && (int)Llama(p, "PiezasEnLaVentana") == 0 && !creadas.ContainsKey(tex[4].ToInt64()),
              "si la ventana no llega a salir, las piezas se vuelven a subir y se ven, y lo que se llevo se suelta");
        tex = otra;
        Llama(p, "RobaPiezasParaVentana");
        Rectangle[] sitio = (Rectangle[])piezasRect.Clone();   // donde estaban (la ventana se las lleva de ahi)
        for (int k = 0; k < 5; k++) { piezas[k] = null; piezasRect[k] = Rectangle.Empty; piezasVer[k]++; }   // el buscador ya se cerro: sus piezas no estan
        Bitmap foto = new Bitmap(64, 64, PixelFormat.Format32bppPArgb);
        ventana = Ven(true, false, foto, 50f);
        List<Envio> cierra = Cuadro(p);
        int c1 = Busca(cierra, tex[1]), c2 = Busca(cierra, tex[2]), c4 = Busca(cierra, tex[4]);
        bool bajan = c1 >= 0 && c2 >= 0 && c4 >= 0 && c4 < c2 && c4 < c1
            && Math.Abs(Arriba(cierra[c1]) - (sitio[1].Y + 50f - .5f)) < 1e-3
            && Math.Abs(Arriba(cierra[c2]) - (sitio[2].Y + 50f - .5f)) < 1e-3
            && Math.Abs(Arriba(cierra[c4]) - (sitio[4].Y + 50f - .5f)) < 1e-3;
        Exige(bajan, "y bajan con ella: 50 px mas abajo, con las tarjetas debajo de lo demas");
        ventana = Ven(false, false, null, 0f);
        Cuadro(p);
        Exige((int)Llama(p, "PiezasEnLaVentana") == 0 && !creadas.ContainsKey(tex[4].ToInt64()) && !creadas.ContainsKey(tex[1].ToInt64()),
              "y al acabar la ventana se sueltan");
        foto.Dispose();
        Pon(p, "GetWin", null);
    }

    // ======================= 10) CTRL+F3 =======================
    static Control Busca(Control c, string texto)
    {
        if (c.Text == texto) return c;
        foreach (Control h in c.Controls) { Control r = Busca(h, texto); if (r != null) return r; }
        return null;
    }
    static void CtrlF3()
    {
        Console.WriteLine("\n  10) CTRL+F3 -> PANTALLA -> EL BUSCADOR\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_buscaportadas_" + Environment.TickCount);
        Directory.CreateDirectory(dir);
        object owner = Codigo.FormSinEncender(TF);
        Pon(owner, "carpetaDatos", dir);
        if (TPanel != null) { Pon(owner, "gpuPanel", Activator.CreateInstance(TPanel, true)); Pon(owner, "gpuEnabled", true); }
        Control so = (Control)Activator.CreateInstance(TSO, true);
        Pon(owner, "searchOverlay", so);
        using (Font fo = new Font(FontFamily.GenericSansSerif, 9))
        using (Panel page = (Panel)Metodo(TF, "PaginaPantalla").Invoke(owner, new object[] { fo, fo, fo, Color.White, Color.Gray, Color.Gray }))
        {
            Control sw = Busca(page, "Buscador con portadas (GPU)");
            Exige(sw != null, "la pestana Pantalla tiene su interruptor");
            if (sw == null) return;
            Control hoja = sw.Parent.Parent;
            List<Control> hojas = new List<Control>(); List<Control> botones = new List<Control>();
            foreach (Control c in page.Controls) { if (c.GetType() == typeof(Panel)) hojas.Add(c); else if (c.GetType().Name == "BotonPlano" && c.Top < 40) botones.Add(c); }
            int k = hojas.IndexOf(hoja);
            bool cabe = true;
            foreach (Control card in hoja.Controls)
            {
                if (card.Right > hoja.Width || card.Bottom > hoja.Height) cabe = false;
                foreach (Control otra in hoja.Controls) if (otra != card && otra.Bounds.IntersectsWith(card.Bounds)) cabe = false;
                foreach (Control x in card.Controls) { if (x.Right > card.Width || x.Bottom > card.Height) cabe = false; foreach (Control y in card.Controls) if (x != y && x.Bounds.IntersectsWith(y.Bounds)) cabe = false; }
            }
            Exige(k == 2 && hojas.Count == 3 && botones.Count == 3 && cabe, "va en la hoja del estilo (\"" + (botones.Count > 2 ? botones[2].Text : "") + "\"), en su tarjeta, y todo cabe sin taparse");
            PropertyInfo marcado = sw.GetType().GetProperty("Checked", TODO);
            bool guarda = true, aplica = true;
            foreach (bool v in new bool[] { false, true, false })
            {
                marcado.SetValue(sw, v, null);
                Metodo(TF, "SaveConfigPrefs").Invoke(owner, null);
                Codigo.EsperaGuardado(TF, owner);
                object leido = Codigo.FormSinEncender(TF); Metodo(TF, "LoadConfigPrefs").Invoke(leido, null);
                if ((bool)Toma(leido, "buscadorPortadas") != v || (bool)Toma(owner, "buscadorPortadas") != v) guarda = false;
                if (TPanel != null && (bool)Toma(so, "Nuevo") != v) aplica = false;
            }
            Exige(guarda, "se guarda en rocola_config.txt (buscador_portadas=) y se lee igual");
            if (TPanel != null) Exige(aplica, "y se aplica al momento: encendido manda el buscador nuevo, apagado el de siempre");
            Control reset = Busca(page, "Pantalla como de fabrica");
            Pon(owner, "buscadorPortadas", false);
            try { typeof(Control).GetMethod("OnClick", TODO).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            Exige(reset != null && (bool)Toma(owner, "buscadorPortadas") && (bool)marcado.GetValue(sw, null), "\"Pantalla como de fabrica\" lo vuelve a encender (la preferencia y el interruptor)");
        }
        try { Directory.Delete(dir, true); } catch { }
        string fuente = Path.Combine(Path.Combine("..", "Rocola"), "BuscadorPortadasGpu.cs");
        string txt = File.Exists(fuente) ? File.ReadAllText(fuente) : "";
        Exige(txt.IndexOf("private bool buscadorPortadas = true;") >= 0, "de fabrica viene encendido");
        so.Dispose();
    }

    // ======================= 11) ENCHUFADO =======================
    static void Enchufado()
    {
        Console.WriteLine("\n  11) ENCHUFADO DONDE TOCA, Y SIN EL INTERRUPTOR, POR EL CAMINO DE SIEMPRE\n");
        string[][] llamadas = {
            new string[] { "SearchOverlay", "RenderContent", "RenderNuevo" }, new string[] { "SearchOverlay", "PiezaViva", "PiezaVivaNueva" },
            new string[] { "SearchOverlay", "DibujaPiezaViva", "DibujaPiezaVivaNueva" }, new string[] { "SearchOverlay", "MoveSel", "MoveSelNuevo" },
            new string[] { "SearchOverlay", "PressSelected", "PressNuevo" }, new string[] { "SearchOverlay", "OnMouseClick", "ClickNuevo" },
            new string[] { "SearchOverlay", "Changed", "AlCambiarConsulta" }, new string[] { "SearchOverlay", "Backspace", "SalDeAtajo" },
            new string[] { "Form1", "EnsureUiPieza", "FirmaPieza" }, new string[] { "Form1", "UiSig", "FirmaFija" },
            new string[] { "Form1", "FirmasBuscador", "FirmaFija" }, new string[] { "Form1", "OpenSearch", "AlAbrirBuscador" },
            new string[] { "Form1", "RecomputeResults", "CuentaResultados" }, new string[] { "Form1", "ChooseSearchResult", "ApuntaBusqueda" },
            new string[] { "Form1", "ApplyArrivedCover", "PortadaLlegoBuscador" }, new string[] { "Form1", "SearchCubeRect", "PortadaElegidaBuscador" },
            new string[] { "Form1", "AplicaEfectosGpu", "AjustaBuscadorPortadas" }, new string[] { "Form1", "UsaAtajo", "CloseSearch" },
            new string[] { "GpuPanel", "RenderFrame", "DibujaCartasPrimero" }, new string[] { "Form1", "FillUiGlows", "GetDestelloTecla" },
            new string[] { "SearchOverlay", "TypeChar", "MarcaTecla" }, new string[] { "SearchOverlay", "Backspace", "MarcaTecla" },
            new string[] { "GpuPanel", "RenderFrame", "PiezasConLaVentana" }, new string[] { "Form1", "StartWinTrans", "RobaPiezasParaVentana" },
            new string[] { "GpuPanel", "StopGpu", "SueltaPiezasVentana" }, new string[] { "Form1", "EnsureUiPieza", "PiezaVistaPreviaBuscador" },
            new string[] { "GpuPanel", "DibujaCintasTexto", "CintaDeLista" }, new string[] { "GpuPanel", "DibujaCintasTexto", "OrigenVistaPrevia" },
            new string[] { "GpuPanel", "DibujaCintasTexto", "AlfaVistaPrevia" },
        };
        int malas = 0; string primera = null;
        foreach (string[] l in llamadas)
        {
            Type t = l[0] == "SearchOverlay" ? TSO : l[0] == "Form1" ? TF : TPanel;
            MethodBase m = Metodo(t, l[1]);
            if (m == null || !Codigo.Llama(m, l[2])) { malas++; if (primera == null) primera = l[0] + "." + l[1] + " no llama a " + l[2]; }
        }
        Exige(malas == 0, llamadas.Length + " enganches: el reparto, las piezas, moverse, pulsar, tocar, las firmas, los atajos, las portadas, el dibujo y el destello" + (primera != null ? " -- " + primera : ""));
        // Cada enganche del buscador pregunta primero si manda el nuevo: sin el interruptor, el camino de siempre.
        string[] miran = { "RenderContent", "PiezaViva", "DibujaPiezaViva", "MoveSel", "PressSelected", "OnMouseClick", "GetPreviewCube", "Changed", "Backspace", "TypeChar" };
        int sinMirar = 0; string cual = null;
        foreach (string n in miran) { MethodBase m = Metodo(TSO, n); if (m == null || !Codigo.Llama(m, "get_Nuevo")) { sinMirar++; if (cual == null) cual = n; } }
        Exige(sinMirar == 0, "y cada uno mira el interruptor antes: apagado (o sin tarjeta) sigue el camino de siempre" + (cual != null ? " -- " + cual + " no lo mira" : ""));
        // Nuevo solo con tarjeta: AjustaBuscadorPortadas pregunta por gpuEnabled y el panel.
        List<MethodBase> ms = Codigo.MetodosConAyudantes(TF, "BuscadorPortadasActivo");
        Exige(Codigo.Llama(Metodo(TF, "AjustaBuscadorPortadas"), "BuscadorPortadasActivo") && Codigo.LeeCampo(ms, "gpuEnabled") && Codigo.LeeCampo(ms, "gpuPanel") && Codigo.LeeCampo(ms, "buscadorPortadas"),
              "manda el nuevo solo con el interruptor Y la tarjeta dibujando");
        Exige(Codigo.Llama(Metodo(TF, "ApplyAlbumGpu"), "AjustaBuscadorPortadas"), "y se vuelve a mirar cada vez que la tarjeta se enciende o se apaga (al apagarse, vuelve el de siempre)");
        Exige((int)TF.GetField("NumPiezas", TODO).GetValue(null) == 5, "hay una pieza mas, la 4, para las tarjetas");
        // Las piezas de los DOS buscadores van con la ventana: el formulario se lo dice al panel, y al cerrar se las lleva
        // sin preguntar cual es.
        Exige(Codigo.EscribeCampo(Codigo.TodosLosMetodos(TF), "GetPiezasSuben") && Codigo.EscribeCampo(Codigo.TodosLosMetodos(TF), "GetPiezaVistaPrevia")
              && !Codigo.Llama(Metodo(TF, "StartWinTrans"), "get_Nuevo"),
              "el formulario le dice al panel cuando las piezas van con la ventana y cual es la vista previa, y al cerrar se las lleva con los dos buscadores");
        object f = Codigo.FormSinEncender(TF);
        Control so = (Control)Activator.CreateInstance(TSO, true);
        Pon(f, "searchOverlay", so);
        bool[] r = new bool[3]; int[] v = new int[2];
        Pon(f, "searchOpen", true); Llama(so, "PonNuevo", true); r[0] = (bool)Llama(f, "PiezasSubenConLaVentana"); v[0] = (int)Llama(f, "PiezaVistaPreviaBuscador");
        Llama(so, "PonNuevo", false); r[1] = (bool)Llama(f, "PiezasSubenConLaVentana"); v[1] = (int)Llama(f, "PiezaVistaPreviaBuscador");
        Llama(so, "PonNuevo", true); Pon(f, "searchOpen", false); r[2] = (bool)Llama(f, "PiezasSubenConLaVentana");
        Exige(r[0] && r[1] && !r[2], "y van con ella con los dos buscadores abiertos (el nuevo y el de siempre); cerrado, no");
        Exige(v[0] == 2 && v[1] == 3, "lo que se desliza de la vista previa es la tarjeta elegida en el nuevo (pieza " + v[0] + ") y el texto de debajo del cubo en el de siempre (pieza " + v[1] + ")");
        so.Dispose();
    }

    // ======================= 12) LO QUIETO, HORNEADO =======================
    static int K(string n) { return (int)TSO.GetField(n, TODO).GetValue(null); }
    static float Escala(Control so) { object rep = Reparto(so); return (float)rep.GetType().GetField("S").GetValue(rep); }
    static Bitmap Lienzo(int w, int h, out Graphics g)
    {
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        g = Graphics.FromImage(b);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        return b;
    }
    // Pegar lo horneado (sin hornear y ya horneado) contra dibujarlo capa a capa, en el mismo sitio de un lienzo con
    // sitio de sobra a la derecha y abajo: si algo se saliera de lo horneado, ahi se veria.
    static int PegarContraDibujar(Control so, int tipo, Size tam, Color c1, Color c2)
    {
        float s = Escala(so);
        Llama(so, "EnsurePaintRes");   // los pinceles (lo hace siempre quien dibuja una imagen)
        int m = (int)LlamaS(TSO, "MargenQuieto", tipo, tam.Width, tam.Height, s);
        Rectangle r = new Rectangle(m, m, tam.Width, tam.Height);
        int W = tam.Width + 2 * m + 40, H = tam.Height + 2 * m + 40;
        Graphics ga, gb, gc;
        Bitmap a = Lienzo(W, H, out ga), b = Lienzo(W, H, out gb), c = Lienzo(W, H, out gc);
        Llama(so, "PegaQuieto", ga, tipo, r, c1, c2);
        Llama(so, "DibujaQuieto", gb, tipo, r, c1, c2, s);
        Llama(so, "PegaQuieto", gc, tipo, r, c1, c2);
        ga.Dispose(); gb.Dispose(); gc.Dispose();
        int d = Distintos(a, b), d2 = Distintos(a, c);
        a.Dispose(); b.Dispose(); c.Dispose();
        return (d < 0 || d2 < 0) ? 1 : d + d2;
    }
    static int PintaPiezas(Control so)
    {
        int n = 0;
        for (int cual = 0; cual < 5; cual++) { Rectangle d; Bitmap b = Pieza(so, cual, out d); if (b != null) { n++; b.Dispose(); } }
        return n;
    }
    // Cada tecla, cada atajo, la casilla y la tarjeta elegida, una vez.
    static void Calienta(Control so)
    {
        IList filas = Filas(so);
        Pon(so, "FocusResults", false); Pon(so, "EnChips", false);
        for (int f = 0; f < filas.Count; f++)
            for (int c = 0; c < ((IList)filas[f]).Count; c++) { Pon(so, "SelRow", f); Pon(so, "SelCol", c); PintaPiezas(so); }
        Pon(so, "EnChips", true);
        for (int i = 0; i < Atajos(so).Count; i++) { Pon(so, "SelChip", i); PintaPiezas(so); }
        Pon(so, "EnChips", false); Pon(so, "FocusResults", true); Pon(so, "SelResult", 0); PintaPiezas(so);
        Pon(so, "FocusResults", false); Pon(so, "SelRow", 1); Pon(so, "SelCol", 0);
    }
    static void LoQuieto()
    {
        Console.WriteLine("\n  12) LO QUIETO, HORNEADO: SE DIBUJA UNA VEZ Y SE PEGA, CON LOS MISMOS PIXELES\n");
        int qT = K("QuietoTecla"), qA = K("QuietoAtajo"), qC = K("QuietoCaja"), qN = K("QuietoCarta"), qE = K("QuietoCartaElegida");
        int tope = K("HorneadosMax");
        // Pegar = dibujar, en tres pantallas y con dos colores.
        int malas = 0, casos = 0; string pm = null;
        foreach (int[] p in new int[][] { new int[] { 800, 600 }, new int[] { 1280, 720 }, new int[] { 1920, 1080 } })
        {
            Control so = Buscador(p[0], p[1], 40);
            object rep = Reparto(so);
            Rectangle tecla = Rectangle.Round(TeclaR(so, 1, 0)), espacio = Rectangle.Round(TeclaR(so, 4, 0)), borrar = Rectangle.Round(TeclaR(so, 4, 1));
            List<Rectangle> at = Atajos(so);
            Rectangle carta = (Rectangle)Llama(so, "CartaRect", 0);
            foreach (Color acento in new Color[] { Color.FromArgb(215, 120, 25), Color.FromArgb(70, 140, 230) })
            {
                Color v = (Color)LlamaS(TSO, "Vivo", acento), claro = Color.FromArgb(255, Math.Min(255, v.R + 60), Math.Min(255, v.G + 60), Math.Min(255, v.B + 60));
                object[][] cs = {
                    new object[] { qT, tecla.Size, v, claro, "tecla" }, new object[] { qT, espacio.Size, v, claro, "ESPACIO" },
                    new object[] { qT, borrar.Size, Color.FromArgb(228, 72, 72), Color.FromArgb(240, 150, 150), "BORRAR" },
                    new object[] { qA, at[0].Size, v, v, "primer atajo" }, new object[] { qA, at[at.Count - 1].Size, v, v, "ultimo atajo" },
                    new object[] { qC, R(rep, "Caja").Size, v, v, "casilla" },
                    new object[] { qN, carta.Size, v, v, "tarjeta" }, new object[] { qE, carta.Size, v, v, "tarjeta elegida" } };
                foreach (object[] c in cs)
                {
                    casos++;
                    int d = PegarContraDibujar(so, (int)c[0], (Size)c[1], (Color)c[2], (Color)c[3]);
                    if (d != 0) { malas++; if (pm == null) pm = p[0] + "x" + p[1] + ", " + c[4] + ": " + d + " pixeles distintos"; }
                }
            }
            so.Dispose();
        }
        Exige(malas == 0, "pegar lo horneado da los mismos pixeles que dibujarlo capa a capa, y nada se sale de lo horneado: teclas (ESPACIO y BORRAR), atajos, la casilla y las tarjetas, elegida o no, con dos colores y en tres pantallas (" + casos + " casos)" + (pm != null ? " -- " + pm : ""));

        // Lo horneado de lo elegido cae en el mismo sitio de su imagen chica que si se dibujara: su margen es el de la imagen.
        {
            Control so = Buscador(1280, 720, 40);
            object rep = Reparto(so); float s = Escala(so);
            int malasM = 0; string pmm = null; Rectangle d; Bitmap bz;
            Pon(so, "EnChips", false); Pon(so, "FocusResults", false);
            foreach (int[] fc in new int[][] { new int[] { 2, 3 }, new int[] { 4, 0 }, new int[] { 4, 1 } })
            {
                Pon(so, "SelRow", fc[0]); Pon(so, "SelCol", fc[1]);
                bz = Pieza(so, 0, out d); if (bz != null) bz.Dispose();
                Rectangle k = Rectangle.Round(TeclaR(so, fc[0], fc[1]));
                int m = (int)LlamaS(TSO, "MargenQuieto", qT, k.Width, k.Height, s);
                if (bz == null || d != Rectangle.Inflate(k, m, m)) { malasM++; if (pmm == null) pmm = "la tecla " + fc[0] + "," + fc[1] + ": imagen " + d + ", horneado " + Rectangle.Inflate(k, m, m); }
            }
            Pon(so, "EnChips", true); Pon(so, "SelChip", 2);
            bz = Pieza(so, 0, out d); if (bz != null) bz.Dispose();
            Rectangle ca = Atajos(so)[2]; int ma = (int)LlamaS(TSO, "MargenQuieto", qA, ca.Width, ca.Height, s);
            if (bz == null || d != Rectangle.Inflate(ca, ma, ma)) { malasM++; if (pmm == null) pmm = "el atajo: imagen " + d + ", horneado " + Rectangle.Inflate(ca, ma, ma); }
            Pon(so, "EnChips", false);
            bz = Pieza(so, 1, out d); if (bz != null) bz.Dispose();
            Rectangle cj = R(rep, "Caja"); int mc = (int)LlamaS(TSO, "MargenQuieto", qC, cj.Width, cj.Height, s);
            if (bz == null || d != Rectangle.Inflate(cj, mc, mc)) { malasM++; if (pmm == null) pmm = "la casilla: imagen " + d + ", horneado " + Rectangle.Inflate(cj, mc, mc); }
            Pon(so, "FocusResults", true); Pon(so, "SelResult", 3);
            bz = Pieza(so, 2, out d); if (bz != null) bz.Dispose();
            Rectangle ct = (Rectangle)Llama(so, "CartaRect", 3); int me = (int)LlamaS(TSO, "MargenQuieto", qE, ct.Width, ct.Height, s);
            if (bz == null || d != Rectangle.Inflate(ct, me, me)) { malasM++; if (pmm == null) pmm = "la tarjeta elegida: imagen " + d + ", horneado " + Rectangle.Inflate(ct, me, me); }
            Exige(malasM == 0, "y lo horneado de la tecla (tambien ESPACIO y BORRAR), el atajo, la casilla y la tarjeta elegidos cae en el mismo sitio de su imagen chica que si se dibujara, asi que su imagen sale igual pixel a pixel" + (pmm != null ? " -- " + pmm : ""));
            so.Dispose();
        }

        // Moverse y teclear no hornea nada: todo sale de lo horneado. Y se guardan pocas.
        {
            Control so = Buscador(1280, 720, 40);
            Calienta(so);
            int antes = (int)Toma(so, "HorneadosHechos"), piezas = 0, maxG = 0;
            Random rnd = new Random(11);
            for (int i = 0; i < 400; i++)
            {
                int q = rnd.Next(7);
                if (q < 4) Llama(so, "MoveSel", new int[] { 1, -1, 0, 0 }[q], new int[] { 0, 0, 1, -1 }[q]);
                else if (q < 6) Llama(so, "TypeChar", (char)('A' + rnd.Next(26)));
                else Llama(so, "Backspace");
                piezas += PintaPiezas(so);
                maxG = Math.Max(maxG, (int)Llama(so, "HorneadosGuardados"));
            }
            int hechos = (int)Toma(so, "HorneadosHechos") - antes;
            Exige(hechos == 0 && piezas > 1000, "moverse por atajos, teclado y tarjetas, bajar de fila, teclear y borrar no hornea nada nuevo: todo sale de lo horneado (" + piezas + " imagenes chicas, " + hechos + " horneados nuevos)");
            Exige(maxG >= 8 && maxG <= tope, "y se guardan pocas: " + maxG + " (el tope es " + tope + ")");
            // Muchos colores seguidos: nunca mas del tope.
            int maxC = 0;
            for (int i = 0; i < 40; i++)
            {
                Pon(so, "Accent", Color.FromArgb(40 + i * 5, 100, 220 - i * 4));
                PintaPiezas(so);
                maxC = Math.Max(maxC, (int)Llama(so, "HorneadosGuardados"));
            }
            Exige(maxC <= tope, "con cuarenta colores seguidos tampoco pasan del tope: " + maxC);
            // Otro tamano de pantalla: lo horneado para el anterior se suelta.
            so.SetBounds(0, 0, 1920, 1080 - 86);
            PintaPiezas(so);
            float s2 = Escala(so); int otras = 0;
            IList hs = (IList)Toma(so, "horneados");
            foreach (object h in hs) if ((float)h.GetType().GetField("S").GetValue(h) != s2) otras++;
            Exige(hs.Count > 0 && otras == 0, "al cambiar el tamano de la pantalla se suelta lo horneado para el anterior (quedan " + hs.Count + ", " + otras + " de antes)");
            Llama(so, "PonNuevo", false);
            Exige(((IList)Toma(so, "horneados")).Count == 0, "y con el interruptor apagado no queda nada horneado");
            so.Dispose();
        }

        // Con otro color de seccion no se reutiliza lo horneado del anterior.
        {
            Color otro = Color.FromArgb(70, 140, 230);
            Control usado = Buscador(1280, 720, 40), nuevo = Buscador(1280, 720, 40);
            Calienta(usado);
            Pon(usado, "Accent", otro); Pon(nuevo, "Accent", otro);
            int malasC = 0; string pc = null;
            for (int k = 0; k < 4; k++)
            {
                foreach (Control so in new Control[] { usado, nuevo })
                {
                    Pon(so, "EnChips", k == 1); Pon(so, "FocusResults", k == 2);
                    Pon(so, "SelRow", k == 3 ? 4 : 2); Pon(so, "SelCol", k == 3 ? 1 : 4); Pon(so, "SelChip", 1); Pon(so, "SelResult", 3);
                }
                for (int cual = 0; cual < 5; cual++)
                {
                    Rectangle da, db;
                    Bitmap a = Pieza(usado, cual, out da), b = Pieza(nuevo, cual, out db);
                    int d = (a == null || b == null) ? ((a == null) == (b == null) ? 0 : 1) : Distintos(a, b);
                    if (d != 0) { malasC++; if (pc == null) pc = "imagen " + cual + " en el caso " + k + ": " + d + " pixeles distintos"; }
                    if (a != null) a.Dispose(); if (b != null) b.Dispose();
                }
            }
            Exige(malasC == 0, "con otro color de seccion no se reutiliza lo horneado del anterior: la tecla, el atajo, BORRAR, la casilla y las tarjetas salen como en un buscador recien abierto con ese color" + (pc != null ? " -- " + pc : ""));
            usado.Dispose(); nuevo.Dispose();
        }
    }

    // ======================= 13) LAS CINTAS DE LA TARJETA ELEGIDA =======================
    static object devuelto;
    static T Devuelve<T>() { return (T)devuelto; }
    static int vistaPrevia = 2;
    static int Vista() { return vistaPrevia; }
    // Un formulario sin encender con el buscador con portadas abierto y, elegida, una cancion cuyo nombre y renglon no caben.
    static object FormConCintas(out Control so)
    {
        object f = Codigo.FormSinEncender(TF);
        int np = (int)TF.GetField("NumPiezas", TODO).GetValue(null);
        Pon(f, "uiPieza", new Bitmap[np]); Pon(f, "uiPiezaRect", new Rectangle[np]); Pon(f, "uiPiezaSig", new long[np]); Pon(f, "uiPiezaVer", new int[np]);
        so = Buscador(1280, 720, 4);
        Pon(so, "GetDuracion", null);   // sin la duracion, el renglon lleva el disco, que es largo
        object alb = Album("Las Mas Grandes Rancheras de Todos los Tiempos, en Vivo desde el Palenque", "Vicente Fernandez", "/m/vf", 3, null);
        object tr = Activator.CreateInstance(TTrack, true);
        TTrack.GetField("Title").SetValue(tr, "El Rey de la Cancion Ranchera Mas Larga que Cabe en una Tarjeta Grande");
        TTrack.GetField("Artist").SetValue(tr, "Vicente Fernandez"); TTrack.GetField("Path").SetValue(tr, "/m/vf/rey.mp3");
        IList res = Lista();
        res.Add(Item(0, "Vicente Fernandez", Album("Hoy", "Vicente Fernandez", "/m/vf2", 3, null), null));
        res.Add(Item(2, "Vicente Fernandez - El Rey", alb, tr));
        Pon(so, "Results", res); Pon(so, "TotalResultados", 2);
        Pon(so, "FocusResults", true); Pon(so, "SelResult", 1);
        Pon(f, "searchOverlay", so); Pon(f, "searchOpen", true); Pon(f, "gpuEnabled", true); Pon(f, "textosDeslizantes", true);
        return f;
    }
    static IList Cintas4(object f)
    {
        Array g = (Array)Toma(f, "cintasTexto");
        if (g == null || g.GetValue(4) == null) return new ArrayList();
        return (IList)Toma(g.GetValue(4), "Items");
    }
    static RectangleF RectCinta(object run) { return (RectangleF)Toma(run, "Rect"); }
    // Cuantos pixeles casi blancos (la letra del nombre) hay dentro de un recuadro de una imagen.
    static int Blancos(Bitmap b, RectangleF r)
    {
        int[] px = Pixeles(b); int n = 0;
        for (int y = Math.Max(0, (int)r.Top); y < Math.Min(b.Height, (int)Math.Ceiling(r.Bottom)); y++)
            for (int x = Math.Max(0, (int)r.Left); x < Math.Min(b.Width, (int)Math.Ceiling(r.Right)); x++)
            {
                int c = px[y * b.Width + x];
                if (((c >> 16) & 255) > 200 && ((c >> 8) & 255) > 200 && (c & 255) > 200) n++;
            }
        return n;
    }
    static void LasCintas()
    {
        Console.WriteLine("\n  13) LA TARJETA ELEGIDA DESLIZA LO QUE NO CABE (TITULOS LARGOS DESLIZANTES)\n");
        Control so; object f = FormConCintas(out so);
        Bitmap[] up = (Bitmap[])Toma(f, "uiPieza"); Rectangle[] ur = (Rectangle[])Toma(f, "uiPiezaRect");
        // Con el interruptor: el nombre y el renglon, que no caben, van en su cinta y no en la imagen.
        int v0 = (int)Llama(f, "EnsureUiPieza", 2);
        IList cs = Cintas4(f);
        Bitmap conCinta = (up[2] != null) ? (Bitmap)up[2].Clone() : null;
        RectangleF rt = cs.Count > 0 ? RectCinta(cs[0]) : RectangleF.Empty, rs = cs.Count > 1 ? RectCinta(cs[1]) : RectangleF.Empty;
        // La columna del texto, sacada del reparto (no de la cinta): del sitio del texto al borde de la tarjeta, menos su margen.
        float sEsc = Escala(so);
        Rectangle cartaEnPieza = (Rectangle)Llama(so, "CartaRect", 1); cartaEnPieza.Offset(-ur[2].X, -ur[2].Y);
        float columna = cartaEnPieza.Right - (int)LlamaS(TSO, "Px", 10f, sEsc) - rt.X;
        bool dentro = conCinta != null && cs.Count == 2 && rt.Left >= 0 && rt.Right <= conCinta.Width + 1 && rt.Width == rs.Width
                      && Math.Abs(rt.Width - (columna + 1f)) < 0.01f && rs.Top > rt.Top;
        int blancosCinta = (conCinta != null && !rt.IsEmpty) ? Blancos(conCinta, rt) : -1;
        Exige(dentro && blancosCinta == 0, "con los titulos deslizantes, el nombre y el renglon de la tarjeta elegida que no caben van en su cinta (" + cs.Count + ", de " + rt.Width + " px, dentro de su imagen) y no se dibujan quietos (" + blancosCinta + " pixeles de letra)");
        // Apagado: quietos, cortados, y la imagen se rehace (su firma mira el interruptor).
        Pon(f, "textosDeslizantes", false);
        int v1 = (int)Llama(f, "EnsureUiPieza", 2);
        int nApagado = Cintas4(f).Count, blancosQuieto = (up[2] != null && !rt.IsEmpty) ? Blancos(up[2], rt) : -1;
        Exige(v1 != v0 && nApagado == 0 && blancosQuieto > 20, "sin ellos, el nombre y el renglon quietos y cortados, como siempre (" + blancosQuieto + " pixeles de letra), y cambiar el interruptor rehace la imagen");
        // Lo que cabe no se desliza.
        Pon(f, "textosDeslizantes", true); Pon(so, "SelResult", 0);
        Llama(f, "EnsureUiPieza", 2);
        Exige(Cintas4(f).Count == 0, "y lo que cabe (un artista corto) no se desliza: quieto, con lo tecleado resaltado");
        // Y lo que cabe justo, a menos de una letra del borde, tampoco: la cinta corta donde corta el renglon quieto.
        {
            object tr = TItem.GetField("TrackRef").GetValue(((IList)Toma(so, "Results"))[1]);
            string largo = (string)TTrack.GetField("Title").GetValue(tr);
            Graphics med = (Graphics)Llama(so, "Medidor");
            Font fnt = (Font)Toma(so, "fnTitulo");
            float ancho = columna, w = 0f;
            string justo = "";
            for (int k = largo.Length; k > 0; k--)
            {
                string pre = largo.Substring(0, k).TrimEnd();
                w = (float)Llama(so, "Mide", med, pre, fnt);
                if (w <= ancho) { justo = pre; break; }
            }
            TTrack.GetField("Title").SetValue(tr, justo);
            Pon(so, "SelResult", 1); Llama(f, "EnsureUiPieza", 2);
            IList cj = Cintas4(f);
            bool soloRenglon = cj.Count == 1 && RectCinta(cj[0]).Top > rt.Top;
            Exige(justo.Length > 0 && ancho - w < 12f && soloRenglon, "y lo que cabe justo (" + w.ToString("0.0") + " de " + ancho.ToString("0.0") + " px) tampoco: se desliza solo el renglon, que no cabe");
            TTrack.GetField("Title").SetValue(tr, largo);
        }
        // Otra vez la larga, para lo que sigue.
        Pon(so, "SelResult", 1); Llama(f, "EnsureUiPieza", 2);
        cs = Cintas4(f);

        // LA TARJETA IMITADA: las cintas encima de la tarjeta elegida, con su transparencia.
        Control p = Panel(1280, 720);
        Pon(p, "TransicionesTacto", false);
        soGpu = so; relojCartas = Ahora() + 60000;
        for (int k = 0; k < 5; k++) { piezas[k] = (k == 2) ? up[2] : null; piezasRect[k] = (k == 2) ? ur[2] : Rectangle.Empty; piezasVer[k]++; }
        Pon(p, "PiezasN", 5);
        Pon(p, "GetPieza", (Func<int, Bitmap>)delegate (int i) { return piezas[i]; });
        Pon(p, "GetPiezaRect", (Func<int, Rectangle>)delegate (int i) { return piezasRect[i]; });
        Pon(p, "GetPiezaVersion", (Func<int, int>)delegate (int i) { return piezasVer[i]; });
        Pon(p, "GetCartas", Delegate.CreateDelegate(Campo(TPanel, "GetCartas").FieldType, typeof(BuscaPortadas).GetMethod("FillGpu", TODO).MakeGenericMethod(TCarta)));
        Type tcs = TF.Assembly.GetType("Rocola.CintasTexto", true), arr = tcs.MakeArrayType();
        devuelto = Toma(f, "cintasTexto");
        Pon(p, "GetCintasTexto", Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(arr), typeof(BuscaPortadas).GetMethod("Devuelve", TODO).MakeGenericMethod(arr)));
        vistaPrevia = 2;
        Pon(p, "GetPiezaVistaPrevia", (Func<int>)Vista);
        Cuadro(p); List<Envio> es = Cuadro(p);
        IList tiles = (IList)Toma(p, "cintaTiles");
        IntPtr t0 = tiles.Count > 0 ? (IntPtr)Toma(tiles[0], "Tex") : IntPtr.Zero;
        RectangleF r0 = tiles.Count > 0 ? RectCinta(Toma(tiles[0], "Run")) : RectangleF.Empty;
        IntPtr[] tex = (IntPtr[])Toma(p, "piezaTex");
        int ic = Busca(es, t0), i2 = Busca(es, tex[2]);
        float izq = float.MaxValue; if (ic >= 0) for (int vv = 0; vv < 6; vv++) izq = Math.Min(izq, VF(es[ic].Datos, vv, 0));
        bool encima = ic >= 0 && i2 >= 0 && ic > i2 && Math.Abs(izq - (piezasRect[2].X + r0.X - .5f)) < 1e-3 && Math.Abs(Arriba(es[ic]) - (piezasRect[2].Y + r0.Y - .5f)) < 1e-3
                      && (VC(es[ic].Datos, 0) >> 24) == (VC(es[i2].Datos, 0) >> 24);
        Exige(encima, "en la tarjeta grafica las cintas van encima de la tarjeta elegida, en el sitio del nombre y con la misma transparencia (" + tiles.Count + " trozos)");
        float arribaQuieto = ic >= 0 ? Arriba(es[ic]) : 0f;
        vistaPrevia = 3;
        List<Envio> viejo = Cuadro(p);
        Exige(Busca(viejo, t0) < 0, "y con el buscador de siempre esas cintas son del texto de debajo del cubo (pieza 3): sin el, no se dibujan en la tarjeta");
        vistaPrevia = 2;
        // Con el vidrio de la lista de canciones (dentro de un disco) las de la vista previa no van, como antes.
        Pon(p, "GetGlassMode", (Func<bool>)delegate { return true; });
        List<Envio> vidrio = Cuadro(p);
        Pon(p, "GetGlassMode", (Func<bool>)delegate { return false; });
        Cuadro(p);
        Exige(Busca(vidrio, t0) < 0, "y con el vidrio de la lista de canciones (dentro de un disco) no se dibujan, como antes");
        // Entrando: la cinta entra con la tarjeta elegida.
        relojCartas = Ahora() - 100000;
        IList otra = Lista(); foreach (object it in (IList)Toma(so, "Results")) otra.Add(it);
        Pon(so, "Results", otra);
        Rectangle d4; piezas[4] = Pieza(so, 4, out d4); piezasRect[4] = d4; piezasVer[4]++;
        relojCartas = Ahora() + 100;
        Cuadro(p); es = Cuadro(p);
        tex = (IntPtr[])Toma(p, "piezaTex");
        ic = Busca(es, t0); i2 = Busca(es, tex[2]);
        uint ac = ic >= 0 ? VC(es[ic].Datos, 0) >> 24 : 999, a2 = i2 >= 0 ? VC(es[i2].Datos, 0) >> 24 : 0;
        Exige(ic >= 0 && i2 >= 0 && a2 < 250 && Math.Abs((int)ac - (int)a2) <= 1, "entrando, la cinta entra con la tarjeta elegida (" + ac + " y " + a2 + " de 255)");
        piezas[4] = null; piezasVer[4]++; relojCartas = Ahora() + 60000;

        // CON LA VENTANA: al abrir y al cerrar, las cintas van con su tarjeta.
        Type tw = TF.Assembly.GetType("Rocola.GpuWin", true);
        Pon(p, "GetWin", Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(tw), typeof(BuscaPortadas).GetMethod("Ventana", TODO).MakeGenericMethod(tw)));
        Pon(p, "GetPiezasSuben", (Func<bool>)delegate { return piezasSuben; });
        ventana = Ven(false, false, null, 0f);
        Cuadro(p);
        piezasSuben = true; ventana = Ven(true, true, null, 137f);
        List<Envio> abre = Cuadro(p);
        ic = Busca(abre, t0);
        Exige(ic >= 0 && Math.Abs(Arriba(abre[ic]) - (arribaQuieto + 137f)) < 1e-3, "al abrir, las cintas suben con la ventana, 137 px mas abajo, encima de su tarjeta");
        ventana = Ven(false, false, null, 0f); Cuadro(p);
        Llama(p, "RobaPiezasParaVentana");
        for (int k = 0; k < 5; k++) { piezas[k] = null; piezasRect[k] = Rectangle.Empty; piezasVer[k]++; }   // el buscador se cerro
        Bitmap foto = new Bitmap(64, 64, PixelFormat.Format32bppPArgb);
        ventana = Ven(true, false, foto, 50f);
        List<Envio> cierra = Cuadro(p);
        ic = Busca(cierra, t0);
        Exige(ic >= 0 && Math.Abs(Arriba(cierra[ic]) - (arribaQuieto + 50f)) < 1e-3, "al cerrar, bajan con la tarjeta que se llevo la ventana (50 px mas abajo)");
        ventana = Ven(false, false, null, 0f); Cuadro(p);
        // Con el buscador de siempre, las del texto de debajo del cubo (pieza 3), igual.
        vistaPrevia = 3;
        piezas[3] = up[2]; piezasRect[3] = ur[2]; piezasVer[3]++;
        Cuadro(p);
        Llama(p, "RobaPiezasParaVentana");
        for (int k = 0; k < 5; k++) { piezas[k] = null; piezasRect[k] = Rectangle.Empty; piezasVer[k]++; }
        ventana = Ven(true, false, foto, 50f);
        cierra = Cuadro(p);
        ic = Busca(cierra, t0);
        Exige(ic >= 0 && Math.Abs(Arriba(cierra[ic]) - (arribaQuieto + 50f)) < 1e-3, "y con el buscador de siempre, las cintas de su vista previa bajan tambien con ella");
        ventana = Ven(false, false, null, 0f); Cuadro(p);
        Pon(p, "GetWin", null);
        foto.Dispose(); if (conCinta != null) conCinta.Dispose();
        vistaPrevia = 2;
    }

    // En Linux (Mono) la rocola copia memoria con RtlMoveMemory, de Windows: se carga una COPIA con su .config que la lleva
    // a memmove (como en 'dibujoligero'). No se toca el ejecutable de las pruebas y la carpeta se borra al acabar.
    static string carpetaCopia;
    static string Cargable(string ruta)
    {
        if (Type.GetType("Mono.Runtime") == null || Environment.OSVersion.Platform == PlatformID.Win32NT) return ruta;
        string dir = Path.Combine(Path.GetTempPath(), "rocola_buscaportadas_" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Directory.CreateDirectory(dir);
        string destino = Path.Combine(dir, Path.GetFileName(ruta));
        File.Copy(ruta, destino, true);
        string naudio = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ruta)), "NAudio.dll");
        if (File.Exists(naudio)) File.Copy(naudio, Path.Combine(dir, "NAudio.dll"), true);
        File.WriteAllText(destino + ".config", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <dllmap dll=\"kernel32.dll\">\n" +
                          "    <dllentry dll=\"libc.so.6\" name=\"RtlMoveMemory\" target=\"memmove\"/>\n  </dllmap>\n</configuration>\n");
        carpetaCopia = dir;
        return destino;
    }
    static int Main(string[] argv)
    {
        try { return Prueba(argv); }
        finally { if (carpetaCopia != null) try { Directory.Delete(carpetaCopia, true); } catch { } }
    }
    static int Prueba(string[] argv)
    {
        Assembly a = Assembly.LoadFrom(Cargable((argv.Length > 0) ? argv[0] : "rocola.exe"));
        TF = a.GetType("Rocola.Form1");
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SearchOverlay") TSO = t; if (t.Name == "SearchItem") TItem = t; if (t.Name == "Album") TAlb = t;
            if (t.Name == "Track") TTrack = t; if (t.Name == "SearchKey") TKey = t;
        }
        TPanel = a.GetType("Rocola.GpuPanel");
        if (TPanel == null)
        {
            // Sin tarjeta grafica: el buscador de siempre, sin nada del nuevo, y la preferencia igual (la configuracion es una).
            Exige(TSO.GetProperty("Nuevo", TODO) == null && Metodo(TSO, "RenderNuevo") == null && Metodo(TSO, "MoveSelNuevo") == null,
                  "sin tarjeta grafica el buscador no tiene nada del nuevo: el dibujo por procesador, como siempre");
            try { CtrlF3(); } catch (Exception e) { Mal("ctrl+f3: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
            Console.WriteLine(fallos == 0 ? "\n  todo bien" : ("\n  " + fallos + " fallos"));
            return fallos;
        }
        TStar = a.GetType("Rocola.GpuStarfield"); TDev = a.GetType("Rocola.D3D9Device"); TD3D = a.GetType("Rocola.D3D9"); TCarta = a.GetType("Rocola.CartaGpu");
        if (TSO == null || TItem == null || TAlb == null || TTrack == null || TCarta == null || TKey == null || Metodo(TSO, "RenderNuevo") == null)
        { Mal("faltan piezas del buscador con portadas (CartaGpu, RenderNuevo...)"); return 1; }
        portadaComun = new Bitmap(300, 300, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portadaComun)) { pg.Clear(Color.FromArgb(255, 150, 60, 30)); for (int i = 0; i < 30; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(120, 250, 200 - i * 3, 90 + i * 4))) pg.FillEllipse(sb, i * 9, i * 8, 70, 70); }
        object form = Codigo.FormSinEncender(TF);
        try { ElReparto(); } catch (Exception e) { Mal("el reparto: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { Moverse(); } catch (Exception e) { Mal("moverse: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { PulsarYTocar(); } catch (Exception e) { Mal("pulsar y tocar: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LaImagenGrande(); } catch (Exception e) { Mal("la imagen grande: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LasImagenesChicas(); } catch (Exception e) { Mal("las imagenes chicas: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LaEntrada(); } catch (Exception e) { Mal("la entrada: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LosAtajos(form); } catch (Exception e) { Mal("los atajos: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LosPrefijos(); } catch (Exception e) { Mal("los prefijos: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LasPortadas(form); } catch (Exception e) { Mal("las portadas: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LaTarjetaImitada(); } catch (Exception e) { Mal("la tarjeta imitada: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { CtrlF3(); } catch (Exception e) { Mal("ctrl+f3: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { Enchufado(); } catch (Exception e) { Mal("enchufado: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LoQuieto(); } catch (Exception e) { Mal("lo quieto: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        try { LasCintas(); } catch (Exception e) { Mal("las cintas: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
        Console.WriteLine(fallos == 0 ? "\n  todo bien" : ("\n  " + fallos + " fallos"));
        return fallos;
    }
}

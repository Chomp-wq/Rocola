using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// LA PANTALLA VIVA, TANDA 4: EL MODO ATRACCION (Rocola/AtraccionGpu.cs; solo tarjeta grafica)
//
// Lo que vigila:
//  1) SIN TARJETA GRAFICA NO CAMBIA NADA. Los enganches no hacen nada (ni se lleva la cuenta de la noche ni se escribe
//     su archivo), y con la tarjeta y las ideas apagadas la atraccion es la de siempre: las mismas portadas en el mismo
//     ciclo (la misma cuenta que AttractView), el cubo en su sitio y girando como siempre, y sus dos textos de siempre.
//  2) LA CUENTA DE LA NOCHE: desde las 8 de la manana, solo canciones, acotada, las mas pedidas primero (y a igualdad la
//     ultima), el archivo rocola_noche.txt (se guarda solo si cambio, se lee igual, el de otra noche no vale, las lineas
//     rotas se saltan, y si falta el bueno se lee la copia), y que se lleva al pedir, cada minuto y al cerrar.
//  3) EL ESCAPARATE: el cubo a la izquierda sin salirse ni tapar el texto, en todas las pantallas; se mece sin girar;
//     cada disco se queda el doble; la ficha dice el disco que ensena el cubo (a la vez) y cambia en dos tiempos; lo que
//     lleva la ficha (nombre en dos renglones, artista, genero, veces pedido, canciones); los mensajes (el precio de una
//     moneda de verdad, lo mas pedido solo si toca), uno a la vez y con su respiro; y lo que se hornea, una cosa por vuelta.
//  4) LO MAS PEDIDO ESTA NOCHE: cada 30 s durante 15, con un paso suave; el cubo a la esquina sin cruzarse con la lista;
//     las filas una a una y sus barras en proporcion; hacen falta tres canciones; la lista horneada (puestos, portadas o
//     su tapa, "N veces"); y sin escaparate, los textos de siempre se apagan mientras esta la lista.
//  5) Los enganches: al entrar y salir de la atraccion, el cubo de la tarjeta, el reloj y el cuadro de la tarjeta.
//  6) Ctrl+F3 -> Pantalla: la tercera hoja, los dos interruptores, sus 4 combinaciones y el boton de fabrica.
//  7) Lo que cuesta cada cosa (Mono: para comparar entre ellas, no son los milisegundos del bar).
class Atraccion
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, gpu, atrT, fichaT, filaT, itemT, trackT, albumT, viewT;
    static Assembly asm;
    static int fallos, oks;
    static string dirPng;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string Fm(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    static FieldInfo Campo(Type t, string n) { for (Type x = t; x != null; x = x.BaseType) { FieldInfo fi = x.GetField(n, F | BindingFlags.DeclaredOnly); if (fi != null) return fi; } return null; }
    static object Get(object o, string n) { return Campo(o.GetType(), n).GetValue(o); }
    static void Set(object o, string n, object v) { Campo(o.GetType(), n).SetValue(o, v); }
    static MethodInfo M(Type t, string n) { return t.GetMethod(n, F); }
    static object St(Type t, string n, params object[] a) { return M(t, n).Invoke(null, a); }
    static object Run(object o, string n, params object[] a) { return o.GetType().GetMethod(n, F).Invoke(o, a); }
    static T Cte<T>(Type t, string n) { return (T)t.GetField(n, F).GetValue(null); }
    static Control Find(Control p, string name) { foreach (Control c in p.Controls) { if (c.Text == name) return c; Control r = Find(c, name); if (r != null) return r; } return null; }
    // Cuantos pixeles de un color parecido (del mismo tono y bastante opacos) hay en una caja.
    static int Parecidos(Bitmap b, Rectangle r, Color c, int tol)
    {
        int n = 0;
        r.Intersect(new Rectangle(0, 0, b.Width, b.Height));
        for (int y = r.Top; y < r.Bottom; y++)
            for (int x = r.Left; x < r.Right; x++)
            {
                Color p = b.GetPixel(x, y);
                if (p.A < 140) continue;
                // premultiplicado: se deshace para comparar el color
                int rr = p.R * 255 / Math.Max(1, (int)p.A), gg = p.G * 255 / Math.Max(1, (int)p.A), bb = p.B * 255 / Math.Max(1, (int)p.A);
                if (Math.Abs(rr - c.R) <= tol && Math.Abs(gg - c.G) <= tol && Math.Abs(bb - c.B) <= tol) n++;
            }
        return n;
    }
    static int Opacos(Bitmap b, Rectangle r)
    {
        int n = 0; r.Intersect(new Rectangle(0, 0, b.Width, b.Height));
        for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) if (b.GetPixel(x, y).A > 100) n++;
        return n;
    }
    static Bitmap Liso(Color c, int lado) { Bitmap bm = new Bitmap(lado, lado); using (Graphics gg = Graphics.FromImage(bm)) gg.Clear(c); return bm; }
    static object Form() { object f = Codigo.FormSinEncender(form); Codigo.PonCerrojos(f); Codigo.PonColecciones(f); return f; }
    static void Tamano(object f, int w, int h)
    {
        FieldInfo cs = typeof(Control).GetField("client_size", F), cr = typeof(Control).GetField("client_rect", F), bo = typeof(Control).GetField("bounds", F);
        if (cs != null) cs.SetValue(f, new Size(w, h));
        if (cr != null) cr.SetValue(f, new Rectangle(0, 0, w, h));
        if (bo != null) bo.SetValue(f, new Rectangle(0, 0, w, h));
    }
    static object ConTarjeta()
    {
        object f = Form();
        Set(f, "gpuPanel", Activator.CreateInstance(gpu, true)); Set(f, "gpuEnabled", true);
        Set(f, "escaparate", true); Set(f, "topNoche", true);   // armada sin constructor: lo de fabrica se pone a mano
        Tamano(f, 1024, 768);
        return f;
    }
    static object Pista(string ruta, string titulo, string artista, string genero)
    {
        object t = Activator.CreateInstance(trackT, true);
        Set(t, "Path", ruta); Set(t, "Title", titulo); Set(t, "Artist", artista); Set(t, "Genre", genero);
        return t;
    }
    static object Item(object pista, bool video)
    {
        object it = Activator.CreateInstance(itemT, true);
        Set(it, "IsVideo", video);
        if (video) Set(it, "VideoPath", "c:/v/video.mp4"); else Set(it, "Song", pista);
        return it;
    }
    static object Disco(string nombre, string artista, string portada, params object[] pistas)
    {
        object a = Activator.CreateInstance(albumT, true);
        Set(a, "Name", nombre); Set(a, "Artist", artista); Set(a, "CoverPath", portada); Set(a, "Folder", "c:/m/" + nombre);
        IList l = (IList)Get(a, "Tracks"); foreach (object p in pistas) l.Add(p);
        return a;
    }
    // Una biblioteca chica: 6 discos con portada y sus canciones.
    static List<object> Biblioteca(object f)
    {
        IList albums = (IList)Get(f, "albums"); IList tracks = (IList)Get(f, "tracks");
        string[] art = { "Vicente Fernandez", "Juan Gabriel", "Los Tigres del Norte", "Los Angeles Azules", "Los Bukis", "Ana Gabriel" };
        string[] gen = { "Ranchera", "Balada", "Norteño", "Cumbia", "Grupera", "Balada" };
        List<object> todas = new List<object>();
        for (int d = 0; d < art.Length; d++)
        {
            object[] ps = new object[4];
            for (int k = 0; k < 4; k++) { ps[k] = Pista("c:/m/" + art[d] + "/" + k + ".mp3", art[d].Split(' ')[0] + " cancion " + k, art[d], gen[d]); tracks.Add(ps[k]); todas.Add(ps[k]); }
            albums.Add(Disco("Lo mejor de " + art[d], art[d], "c:/m/" + art[d] + "/cover.jpg", ps));
        }
        return todas;
    }
    static object Vista(object f, int portadas)
    {
        object v = Activator.CreateInstance(viewT, true);
        List<string> cov = new List<string>();
        for (int i = 0; i < portadas; i++) cov.Add("c:/m/" + (new string[] { "Vicente Fernandez", "Juan Gabriel", "Los Tigres del Norte", "Los Angeles Azules", "Los Bukis", "Ana Gabriel" })[i % 6] + "/cover.jpg");
        Set(v, "CycleCovers", cov);
        Dictionary<string, Image> img = new Dictionary<string, Image>();
        Color[] cs = { Color.SteelBlue, Color.SeaGreen, Color.Crimson, Color.Goldenrod, Color.Orchid, Color.Teal };
        for (int i = 0; i < cov.Count; i++) if (!img.ContainsKey(cov[i])) img[cov[i]] = Liso(cs[i % cs.Length], 40);
        Func<string, Image> get = delegate(string p) { Image r; return img.TryGetValue(p, out r) ? r : null; };
        Set(v, "GetCoverByPath", get);
        viewT.GetProperty("SongCount", F).SetValue(v, 12480, null);
        Set(f, "attractView", v);
        return v;
    }

    // ================================================================================================
    //  1) SIN TARJETA GRAFICA, O CON LAS IDEAS APAGADAS: LA ATRACCION DE SIEMPRE
    // ================================================================================================
    static void Siempre()
    {
        Console.WriteLine("\n  1) CON LAS IDEAS APAGADAS, LA ATRACCION DE SIEMPRE\n");
        // El ciclo de portadas es la misma cuenta que la del cubo de siempre (AttractView.GetCoverPair).
        object f = ConTarjeta(); Biblioteca(f);
        object v = Vista(f, 5);
        MethodInfo par = M(viewT, "GetCoverPair");
        bool igual = true; int probadas = 0;
        foreach (int m in new int[] { 1, 2, 5 })
        {
            Vista(f, m); v = Get(f, "attractView");
            List<string> cov = (List<string>)Get(v, "CycleCovers");
            for (double fase = 0; fase < 90; fase += .37)
            {
                Set(v, "Phase", fase);
                object[] a = { null, null, 0f }; par.Invoke(v, a);
                object[] c = { fase, m, 4.2, 3.0, 0, 0f }; St(form, "CicloPortadas", c);
                int idx = (int)c[4];
                Func<string, Image> get = (Func<string, Image>)Get(v, "GetCoverByPath");
                if (a[0] != get(cov[idx]) || (float)a[2] != (float)c[5]) igual = false;
                probadas++;
            }
        }
        Check(igual, "el ciclo de portadas es la misma cuenta que la del cubo de siempre (" + probadas + " momentos, con 1, 2 y 5 portadas)");
        // Con el escaparate apagado: el cubo, sus portadas y su giro, los de siempre.
        f = ConTarjeta(); Biblioteca(f); v = Vista(f, 5);
        Set(f, "escaparate", false); Set(f, "topNoche", false); Set(f, "attractOn", true);
        Run(f, "EscaparateEntraGpu");
        Set(v, "Phase", 12.3); Set(v, "Angle", 2.25);
        object[] p1 = { null, null, 0f }; Run(f, "ParCuboAtraccion", p1);
        object[] p0 = { null, null, 0f }; par.Invoke(v, p0);
        RectangleF r = (RectangleF)Run(f, "CuboAtraccionAhora"), r0 = (RectangleF)Run(f, "AttractCubeRect");
        Check(p1[0] == p0[0] && p1[1] == p0[1] && (float)p1[2] == (float)p0[2] && r == r0 && (double)Run(f, "AnguloAtraccion") == 2.25,
              "con las dos ideas apagadas, el cubo es el de siempre: las mismas portadas, en su sitio y girando igual");
        object st = Run(f, "BuildAtraccionState");
        Check(!(bool)Get(st, "Has") && (float)Get(st, "UiAlfa") == 1f && (float)Run(f, "UiAlfaAtraccion") == 1f,
              "y la tarjeta no pinta nada nuevo: los dos textos de siempre, enteros");
        // Los textos de siempre solo se quitan con el escaparate; su firma lo sabe (se rehace al cambiar).
        Check(Codigo.Llama(M(form, "EnsureUiOverlay"), "EscaparateActivo") && Codigo.Llama(M(form, "UiSig"), "EscaparateActivo")
              && Codigo.Llama(M(form, "FillUiGlows"), "EscaparateActivo") && Codigo.Llama(M(form, "FillUiGlows"), "UiAlfaAtraccion"),
              "los dos textos de siempre y su respiro solo se quitan con el escaparate, y la textura se rehace al cambiar");
    }
    static void SinTarjeta()
    {
        object fc = Codigo.FormSinEncender(form); Codigo.PonCerrojos(fc); Codigo.PonColecciones(fc);
        bool vacios = true;
        foreach (string n in new string[] { "CuentaNoche", "GuardaNoche", "EscaparateEntra", "EscaparateSale" })
            if (Codigo.Llamadas(M(form, n)).Count != 0) vacios = false;
        Check(vacios && form.GetField("nocheCuenta", F) == null && asm.GetType("Rocola.GpuAtraccion") == null,
              "los enganches no hacen nada: ni cuenta de la noche ni escaparate");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_atraccion_cpu_" + Environment.TickCount); Directory.CreateDirectory(dir);
        Set(fc, "carpetaDatos", dir); Set(fc, "letterAvailable", null);
        object p = Pista("c:/m/x/a.mp3", "A", "X", "Cumbia");
        Run(fc, "RecordPlay", Item(p, false)); Run(fc, "RecordPlay", Item(p, false));
        Run(fc, "GuardaNoche", false);
        IDictionary sc = (IDictionary)Get(fc, "songCounts");
        Check((int)sc["c:/m/x/a.mp3"] == 2 && !File.Exists(Path.Combine(dir, "rocola_noche.txt")) && Codigo.Llama(M(form, "RecordPlay"), "CuentaNoche"),
              "pedir cuenta como siempre (lo mas sonado), y no se escribe rocola_noche.txt");
        try { Directory.Delete(dir, true); } catch { }
    }

    // ================================================================================================
    //  2) LA CUENTA DE LA NOCHE
    // ================================================================================================
    static void Noche()
    {
        Console.WriteLine("\n  2) LA CUENTA DE LA NOCHE\n");
        MethodInfo clave = M(form, "ClaveNoche");
        string k1 = (string)clave.Invoke(null, new object[] { new DateTime(2026, 9, 25, 7, 59, 0) });
        string k2 = (string)clave.Invoke(null, new object[] { new DateTime(2026, 9, 25, 8, 0, 0) });
        string k3 = (string)clave.Invoke(null, new object[] { new DateTime(2026, 9, 24, 23, 30, 0) });
        Check(k1 == "2026-09-24" && k2 == "2026-09-25" && k3 == "2026-09-24", "la noche va de las 8 de la mañana a las 8 de la mañana (a las 7:59 aun es la de ayer)");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_atraccion_" + Environment.TickCount); Directory.CreateDirectory(dir);
        object f = ConTarjeta(); Set(f, "carpetaDatos", dir);
        List<object> ps = Biblioteca(f);
        MethodInfo cuenta = M(form, "CuentaNocheGpu");
        DateTime t = new DateTime(2026, 9, 24, 21, 0, 0);
        cuenta.Invoke(f, new object[] { Item(ps[0], false), t }); cuenta.Invoke(f, new object[] { Item(ps[0], false), t.AddMinutes(5) });
        cuenta.Invoke(f, new object[] { Item(null, true), t.AddMinutes(6) });
        cuenta.Invoke(f, new object[] { Item(ps[1], false), t.AddMinutes(7) });
        IDictionary nc = (IDictionary)Get(f, "nocheCuenta");
        Check(nc.Count == 2 && (int)nc[Get(ps[0], "Path")] == 2 && (int)nc[Get(ps[1], "Path")] == 1, "cuenta las canciones pedidas (los videos no)");
        cuenta.Invoke(f, new object[] { Item(ps[2], false), t.AddHours(12) });   // las 9 de la mañana del dia siguiente: otra noche
        Check(nc.Count == 1 && (int)nc[Get(ps[2], "Path")] == 1, "a las 8 de la mañana empieza otra noche, desde cero");
        // Acotada.
        int tope = Cte<int>(form, "NocheTope");
        object fg = ConTarjeta(); Set(fg, "carpetaDatos", dir);
        for (int i = 0; i < tope + 5; i++) cuenta.Invoke(fg, new object[] { Item(Pista("c:/m/tope/" + i + ".mp3", "c" + i, "a", ""), false), t });
        IDictionary ng = (IDictionary)Get(fg, "nocheCuenta");
        cuenta.Invoke(fg, new object[] { Item(Pista("c:/m/tope/0.mp3", "c0", "a", ""), false), t });
        Check(ng.Count == tope && (int)ng["c:/m/tope/0.mp3"] == 2, "como mucho " + tope + " canciones distintas (y las que ya estan siguen contando)");
        // Las mas pedidas: mas veces primero; si empatan, la pedida mas tarde.
        object fo = ConTarjeta(); Set(fo, "carpetaDatos", dir);
        string[] orden = { "a", "b", "c", "a", "d", "c", "e" };   // a:2 c:2 (c despues) b:1 d:1 e:1 (e la ultima)
        for (int i = 0; i < orden.Length; i++) cuenta.Invoke(fo, new object[] { Item(Pista("c:/m/o/" + orden[i] + ".mp3", orden[i], "x", ""), false), t.AddMinutes(i) });
        List<string> top = (List<string>)St(form, "MasPedidasNoche", Get(fo, "nocheCuenta"), Get(fo, "nocheOrden"), 4);
        Check(top.Count == 4 && top[0].EndsWith("c.mp3") && top[1].EndsWith("a.mp3") && top[2].EndsWith("e.mp3") && top[3].EndsWith("d.mp3"),
              "las mas pedidas primero y, si empatan, la ultima pedida (" + string.Join(", ", top.ConvertAll(delegate(string s) { return Path.GetFileNameWithoutExtension(s); }).ToArray()) + ")");
        // El archivo: se guarda, se lee igual (con su orden), y solo si cambio.
        Set(fo, "nocheClave", (string)clave.Invoke(null, new object[] { DateTime.Now }));   // la de hoy, para leerla de vuelta ahora
        Run(fo, "GuardaNocheGpu", false);
        string archivo = Path.Combine(dir, "rocola_noche.txt");
        string[] lineas = File.Exists(archivo) ? File.ReadAllLines(archivo) : new string[0];
        Check(lineas.Length == 6 && lineas[0] == "noche=" + (string)Get(fo, "nocheClave") && lineas[5] == "1|c:/m/o/e.mp3",
              "se guarda en rocola_noche.txt: la noche arriba y \"veces|ruta\", de la pedida hace mas tiempo a la ultima");
        object fl = ConTarjeta(); Set(fl, "carpetaDatos", dir);
        Run(fl, "LeeNocheSiFalta", DateTime.Now);
        List<string> top2 = (List<string>)St(form, "MasPedidasNoche", Get(fl, "nocheCuenta"), Get(fl, "nocheOrden"), 4);
        Check(string.Join("|", top2.ToArray()) == string.Join("|", top.ToArray()) && ((IDictionary)Get(fl, "nocheCuenta")).Count == 5,
              "y se lee igual al volver a abrir la rocola (las mismas, en el mismo orden)");
        File.Delete(archivo);
        Run(fo, "GuardaNocheGpu", false);
        Check(!File.Exists(archivo), "si no cambio nada, no se vuelve a escribir");
        // Otra noche, lineas rotas, la copia.
        File.WriteAllText(archivo, "noche=2001-01-01\r\n3|c:/m/viejo.mp3\r\n");
        object fv = ConTarjeta(); Set(fv, "carpetaDatos", dir); Run(fv, "LeeNocheSiFalta", DateTime.Now);
        Check(((IDictionary)Get(fv, "nocheCuenta")).Count == 0, "la de otra noche no vale: se empieza de cero");
        string hoy = (string)clave.Invoke(null, new object[] { DateTime.Now });
        File.WriteAllText(archivo, "noche=" + hoy + "\r\nx|\r\n|c:/sin.mp3\r\nabc|c:/letras.mp3\r\n0|c:/cero.mp3\r\n-3|c:/menos.mp3\r\n2|c:/bien.mp3\r\n");
        object fr = ConTarjeta(); Set(fr, "carpetaDatos", dir); Run(fr, "LeeNocheSiFalta", DateTime.Now);
        IDictionary nr = (IDictionary)Get(fr, "nocheCuenta");
        Check(nr.Count == 1 && (int)nr["c:/bien.mp3"] == 2, "las lineas rotas se saltan y las buenas se leen");
        File.Delete(archivo); File.WriteAllText(archivo + ".bak", "noche=" + hoy + "\r\n4|c:/copia.mp3\r\n");
        object fb = ConTarjeta(); Set(fb, "carpetaDatos", dir); Run(fb, "LeeNocheSiFalta", DateTime.Now);
        Check(((IDictionary)Get(fb, "nocheCuenta")).Count == 1, "si el archivo bueno falta (un apagon a media escritura), se lee la copia");
        // Los enganches: al pedir, cada minuto y al cerrar (antes de esperar a los guardados).
        List<string> cierre = Codigo.Repasa(M(form, "OnFormClosed")), cierre2 = Codigo.Repasa(M(form, "GuardaTodoAlCerrar"));
        // (la espera de los guardados se llama distinto en cada rama: EsperaGuardados en HD, WaitDataWrites en Modest)
        int espera = Math.Max(cierre.IndexOf("EsperaGuardados"), cierre.IndexOf("WaitDataWrites")), espera2 = Math.Max(cierre2.IndexOf("EsperaGuardados"), cierre2.IndexOf("WaitDataWrites"));
        Check(Codigo.Llama(M(form, "RecordPlay"), "CuentaNoche") && Codigo.Llama(M(form, "StatsTimer_Tick"), "GuardaNoche") && Codigo.Llama(M(form, "GuardaNocheGpu"), "GuardaDetras")
              && cierre.IndexOf("GuardaNoche") >= 0 && cierre.IndexOf("GuardaNoche") < espera
              && cierre2.IndexOf("GuardaNoche") >= 0 && cierre2.IndexOf("GuardaNoche") < espera2,
              "se cuenta al pedir, se guarda cada minuto si cambio y al cerrar (por los dos caminos, antes de esperar a los guardados)");
        try { Directory.Delete(dir, true); } catch { }
    }

    // ================================================================================================
    //  3) EL ESCAPARATE
    // ================================================================================================
    static void Escaparate()
    {
        Console.WriteLine("\n  3) EL ESCAPARATE\n");
        // Donde va cada cosa, en las pantallas de siempre.
        bool bien = true; string peor = "";
        object fx = Activator.CreateInstance(fichaT, true);
        Set(fx, "Nombre", "Jefe de Jefes (Edicion Especial de Aniversario, Remasterizada)"); Set(fx, "Artista", "Los Tigres del Norte con la Banda Sinaloense");
        Set(fx, "Genero", "Regional"); Set(fx, "GeneroColor", Color.FromArgb(215, 120, 25)); Set(fx, "Canciones", 24); Set(fx, "Pedidas", 131);
        foreach (Size s in new Size[] { new Size(800, 600), new Size(1024, 768), new Size(1280, 720), new Size(1366, 768), new Size(1920, 1080) })
        {
            RectangleF c = (RectangleF)St(form, "CuboEscaparate", s.Width, s.Height), col = (RectangleF)St(form, "ColumnaEscaparate", s.Width, s.Height);
            int mh = (int)St(form, "AltoMensaje", s.Height);
            using (Bitmap b = (Bitmap)St(form, "HorneaFichaVitrina", fx, (int)col.Width, s.Height))
            {
                bool ok = c.X >= 0 && c.Y >= 0 && c.Bottom <= s.Height && col.X >= c.Right + s.Width * .04f && col.Right <= s.Width && col.Width >= 150
                          && b.Height <= col.Height - mh && b.Width == (int)col.Width;
                if (!ok) { bien = false; peor += " " + s.Width + "x" + s.Height; }
            }
        }
        Check(bien, "el cubo a la izquierda y el texto a su derecha, sin salirse ni tocarse, y la ficha mas larga cabe encima del mensaje (5 pantallas)" + peor);
        // Cada disco se queda casi el doble (hay que leer su nombre), y el cambio es a la vez que el de la portada.
        double ciclo = Cte<double>(form, "VitrinaCiclo"), quieto = Cte<double>(form, "VitrinaQuieto");
        Check(ciclo >= 4.2 * 1.9 && quieto >= 3.0 * 2.2 && ciclo - quieto > 1.0, "cada disco se queda " + Fm(ciclo, "0.0") + " de la fase (el de siempre, 4,2), con " + Fm(ciclo - quieto, "0.0") + " de cambio");
        // La ficha es la del disco que ensena el cubo: en todo momento.
        object f = ConTarjeta(); Biblioteca(f); object v = Vista(f, 6);
        Set(f, "attractOn", true);
        Run(f, "EscaparateEntraGpu");
        IList vit = (IList)Get(f, "vitrina");
        Check(vit.Count == 6 && (string)Get(vit[2], "Nombre") == "Lo mejor de Los Tigres del Norte" && (int)Get(vit[0], "Canciones") == 4,
              "al entrar, se apunta un disco por cada portada del cubo, en el mismo orden");
        Check(Get(f, "msgsBmp") != null && ((Bitmap[])Get(f, "vitBmp"))[0] != null, "la primera ficha y los mensajes, horneados ya (salen mientras el cubo llega)");
        bool sincro = true; int vistos = 0; Func<string, Image> get = (Func<string, Image>)Get(v, "GetCoverByPath"); List<string> cov = (List<string>)Get(v, "CycleCovers");
        for (double fase = 0; fase < 60; fase += .45)
        {
            Set(v, "Phase", fase);
            for (int k = 0; k < 3; k++) Run(f, "AtraccionTic");
            object[] pr = { null, null, 0f }; Run(f, "ParCuboAtraccion", pr);
            object st = Run(f, "BuildAtraccionState");
            Bitmap fa = (Bitmap)Get(st, "Ficha");
            int[] idx = (int[])Get(f, "vitIdx"); Bitmap[] bm = (Bitmap[])Get(f, "vitBmp");
            int cual = -1; for (int s = 0; s < 2; s++) if (bm[s] == fa) cual = idx[s];
            if (cual < 0 || pr[0] != get(cov[cual])) sincro = false;
            vistos++;
        }
        Check(sincro, "la ficha dice el disco que ensena el cubo, siempre (" + vistos + " momentos, con el reloj horneando una cosa por vuelta)");
        // El cambio de ficha, en dos tiempos: nunca las dos a la vez.
        MethodInfo funde = M(form, "FundeFichas");
        bool dosTiempos = true; float ant = 1f;
        for (float x = 0; x <= 1.0001f; x += .01f)
        {
            object[] a = { x, 0f, 0f, 0f, 0f }; funde.Invoke(null, a);
            float va = (float)a[1], vdy = (float)a[2], na = (float)a[3], ndy = (float)a[4];
            if (Math.Min(va, na) > .02f || va > ant + 1e-4f || vdy > 0f || ndy < 0f) dosTiempos = false;
            ant = va;
        }
        object[] a0 = { 0f, 0f, 0f, 0f, 0f }, a1 = { 1f, 0f, 0f, 0f, 0f }; funde.Invoke(null, a0); funde.Invoke(null, a1);
        Check(dosTiempos && (float)a0[1] == 1f && (float)a0[3] == 0f && (float)a1[1] == 0f && (float)a1[3] == 1f,
              "al cambiar de disco, la ficha vieja sube y se va y luego llega la nueva desde abajo: nunca las dos encima");
        // Lo que lleva la ficha.
        object d1 = Activator.CreateInstance(fichaT, true);
        Set(d1, "Nombre", "El Rey"); Set(d1, "Artista", "Vicente Fernandez"); Set(d1, "Genero", "Ranchera"); Set(d1, "GeneroColor", Color.FromArgb(200, 40, 60)); Set(d1, "Canciones", 12); Set(d1, "Pedidas", 0);
        using (Bitmap corto = (Bitmap)St(form, "HorneaFichaVitrina", d1, 450, 768))
        using (Bitmap largo = (Bitmap)St(form, "HorneaFichaVitrina", fx, 450, 768))
        {
            corto.Save(Path.Combine(dirPng, "ficha_corta.png"), ImageFormat.Png); largo.Save(Path.Combine(dirPng, "ficha_larga.png"), ImageFormat.Png);
            float titL; using (Font ft = new Font("Trebuchet MS", Math.Max(24f, 768 * .062f), FontStyle.Bold, GraphicsUnit.Pixel)) titL = ft.GetHeight();
            Check(largo.Height - corto.Height >= titL * .8f, "un nombre largo va en dos renglones (la ficha crece " + (largo.Height - corto.Height) + " px) y uno corto en uno");
            int blanco = Parecidos(corto, new Rectangle(0, 0, 450, corto.Height), Color.White, 12), verde = Parecidos(corto, new Rectangle(0, 0, 450, corto.Height), Color.FromArgb(120, 230, 150), 16);
            int ambarC = Parecidos(corto, new Rectangle(0, 0, 450, corto.Height), Color.FromArgb(255, 198, 104), 20), ambarL = Parecidos(largo, new Rectangle(0, 0, 450, largo.Height), Color.FromArgb(255, 198, 104), 20);
            int azul = Parecidos(corto, new Rectangle(0, 0, 450, corto.Height), Color.FromArgb(150, 200, 255), 20);
            Info("pixeles: nombre " + blanco + ", artista " + verde + ", canciones " + azul + ", \"pedido\" " + ambarC + " / " + ambarL);
            Check(blanco > 300 && verde > 100 && azul > 30 && ambarC < 5 && ambarL > 300,   // (el texto del genero, anaranjado, ya da unos 50)
                  "la ficha lleva el nombre en blanco, el artista en verde, las canciones en azul y \"pedido N veces\" solo si se ha pedido");
        }
        Check((string)St(form, "TextoPedidoDisco", 1) == "pedido 1 vez" && (string)St(form, "TextoPedidoDisco", 7) == "pedido 7 veces" && (string)St(form, "TextoPedidoDisco", 0) == ""
              && (string)St(form, "TextoCanciones", 1, 0) == "1 canción" && (string)St(form, "TextoCanciones", 0, 3) == "3 videos" && (string)St(form, "TextoVeces", 1) == "1 vez",
              "y los textos en singular y plural");
        // La ficha desde un disco: genero de sus canciones (sin "Otros"), artista, veces pedidas.
        IDictionary sc = (IDictionary)Get(f, "songCounts");
        IList albums = (IList)Get(f, "albums");
        object al = albums[0]; IList tr = (IList)Get(al, "Tracks");
        sc[(string)Get(tr[0], "Path")] = 5; sc[(string)Get(tr[1], "Path")] = 2;
        Set(al, "Artist", "");
        object fd = Run(f, "FichaDe", al);
        string ranchera = (string)St(form, "GenreLabel", "Ranchera");
        Check((int)Get(fd, "Pedidas") == 7 && (string)Get(fd, "Artista") == "Vicente Fernandez" && (string)Get(fd, "Genero") == ranchera && (int)Get(fd, "Canciones") == 4,
              "del disco: cuantas veces se han pedido sus canciones (7), su artista (el de sus canciones si el disco no lo dice) y su genero (el de sus canciones: " + ranchera + ")");
        ((IDictionary)Get(f, "discTag"))[(string)Get(al, "Folder")] = "Cumbia";
        Check((string)Get(Run(f, "FichaDe", al), "Genero") == "Cumbia", "y si la rocola ya sabe el genero del disco, manda ese");
        // Los mensajes.
        Set(f, "topNoche", false); Run(f, "ArmaMensajes");
        List<string> ms = (List<string>)Get(f, "mensajes");
        int cpc = Cte<int>(form, "CreditsPerCoin");
        bool precio = false, busca = false, noche = false, elige = false;
        foreach (string m in ms) { if (m.Contains("una moneda = " + cpc + " canciones")) precio = true; if (m.Contains("botón rojo")) busca = true; if (m.Contains("esta noche")) noche = true; if (m.Contains((12480).ToString("#,0"))) elige = true; }
        Check(ms.Count == 3 && precio && busca && elige && !noche, "los mensajes: como elegir (entre " + (12480).ToString("#,0") + " canciones), lo que da una moneda de verdad (" + cpc + ") y como buscar");
        MethodInfo fm = M(form, "FaseMensaje");
        bool uno = true, sube = true; int cambios = 0, antK = -1;
        for (double t = 0; t < 30000; t += 16.7)
        {
            object[] a = { t, 3, 0, 0f, 0f }; fm.Invoke(null, a);
            int k = (int)a[2]; float al2 = (float)a[3], dy = (float)a[4];
            if (k != antK) { cambios++; antK = k; }
            double s = t % 5000;
            if (s < 400 && dy < 0f) sube = false; if (s > 4600 && dy > 0f) sube = false;
            if (s > 450 && s < 4550 && al2 < .999f) uno = false;
        }
        object[] ini = { 5001.0, 3, 0, 0f, 0f }, fin = { 9999.0, 3, 0, 0f, 0f }; fm.Invoke(null, ini); fm.Invoke(null, fin);
        Check(uno && sube && cambios == 6 && (float)ini[3] < .05f && (float)fin[3] < .05f, "se turnan cada 5 s, uno a la vez: entra desde abajo, se queda y se va hacia arriba (nunca de golpe)");
        // El cubo se mece: nunca gira del todo, y sin saltos.
        MethodInfo mece = M(form, "MeceCubo");
        float maxA = 0f, maxPaso = 0f, antA = 0f;
        for (double t = 0; t < 20000; t += 16.7) { float x = (float)mece.Invoke(null, new object[] { t }); maxA = Math.Max(maxA, Math.Abs(x)); maxPaso = Math.Max(maxPaso, Math.Abs(x - antA)); antA = x; }
        Check(maxA <= .29f && maxA > .2f && maxPaso < .01f, "el cubo se mece ±" + Fm(maxA * 180 / Math.PI, "0") + " grados en vez de girar: la portada casi siempre de frente, sin saltos");
        Set(v, "Angle", 7.5);
        Check(Math.Abs((double)Run(f, "AnguloAtraccion")) <= .29, "y con el escaparate el cubo no usa el giro de siempre");
        // El estado: aparece mientras el cubo llega, y el respiro dorado detras del mensaje.
        Set(f, "atrDesde", (double)St(gpu, "AhoraMs"));
        object e0 = Run(f, "BuildAtraccionState");
        Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - 2500);
        object e1 = Run(f, "BuildAtraccionState");
        Check((float)Get(e0, "VitrinaA") < .05f && Math.Abs((float)Get(e1, "VitrinaA") - 1f) < 1e-4f && (uint)Get(e1, "BrilloArgb") >> 24 > 0
              && Math.Abs((float)Get(e1, "BrilloY") - ((float)Get(e1, "MsgY") + (float)Get(e1, "MsgH") * .5f + (float)Get(e1, "MsgDy"))) < .5f,
              "la ficha aparece mientras el cubo llega (0,7 s) y el mensaje lleva su respiro dorado detras, como el de siempre");
        // Una cosa por vuelta del reloj.
        object fh = ConTarjeta(); List<object> psh = Biblioteca(fh); Vista(fh, 6);
        string dirh = Path.Combine(Path.GetTempPath(), "rocola_atraccion_h_" + Environment.TickCount); Directory.CreateDirectory(dirh); Set(fh, "carpetaDatos", dirh);
        for (int i = 0; i < 4; i++) M(form, "CuentaNocheGpu").Invoke(fh, new object[] { Item(psh[i * 3], false), DateTime.Now });
        Set(fh, "attractOn", true); Run(fh, "EscaparateEntraGpu");
        ((Bitmap)Get(fh, "msgsBmp")).Dispose(); Set(fh, "msgsBmp", null);   // pendientes a la vez: la ficha que viene, los mensajes y la lista
        int maxHornea = 0, total = 0;
        for (int k = 0; k < 8; k++)
        {
            int a = (int)Get(fh, "vitSeq") + (int)Get(fh, "msgsVer") + (int)Get(fh, "topVer");
            Run(fh, "AtraccionTic");
            int hizo = (int)Get(fh, "vitSeq") + (int)Get(fh, "msgsVer") + (int)Get(fh, "topVer") - a;
            maxHornea = Math.Max(maxHornea, hizo); total += hizo;
        }
        Check(maxHornea == 1 && total == 3, "con tres cosas pendientes, el reloj hornea una por vuelta (no se amontona el trabajo en un cuadro)");
        // Al salir, se suelta todo.
        Run(fh, "EscaparateSaleGpu");
        Bitmap[] vb = (Bitmap[])Get(fh, "vitBmp");
        Check(vb[0] == null && vb[1] == null && Get(fh, "msgsBmp") == null && Get(fh, "topBmp") == null && !(bool)Get(fh, "atrVivo"),
              "al salir de la atraccion se suelta lo horneado");
        try { Directory.Delete(dirh, true); } catch { }
    }

    // ================================================================================================
    //  4) LO MAS PEDIDO ESTA NOCHE
    // ================================================================================================
    static void Top()
    {
        Console.WriteLine("\n  4) LO MAS PEDIDO ESTA NOCHE\n");
        MethodInfo mz = M(form, "MezclaTop");
        int vms = Cte<int>(form, "VitrinaMs"), tms = Cte<int>(form, "TopMs"), pms = Cte<int>(form, "PaginaMs");
        bool forma = true; float maxPaso = 0f, ant = 0f;
        for (double t = 0; t < 3 * (vms + tms); t += 16.7)
        {
            float m = (float)mz.Invoke(null, new object[] { t, true });
            double c = t % (vms + tms);
            if (c < vms && m != 0f) forma = false;
            if (c > vms + pms && c < vms + tms - pms && m != 1f) forma = false;
            maxPaso = Math.Max(maxPaso, Math.Abs(m - ant)); ant = m;
        }
        bool nunca = true; for (double t = 0; t < 100000; t += 333) if ((float)mz.Invoke(null, new object[] { t, false }) != 0f) nunca = false;
        Check(forma && nunca && maxPaso < .04f, "cada " + (vms / 1000) + " s de escaparate, " + (tms / 1000) + " s de lista, con un paso suave (como mucho " + Fm(maxPaso * 100, "0.0") + " % por cuadro); sin lista, nunca");
        // Filas una a una, cabecera antes, y el cubo ya en su esquina.
        MethodInfo ft = M(form, "FilaTop");
        double[] llega = new double[5]; bool monotona = true, barraDespues = true;
        for (int i = 0; i < 5; i++)
        {
            float antA = -1f; llega[i] = -1;
            for (double tt = 0; tt < 4000; tt += 10)
            {
                object[] a = { tt, i, 0f, 0f, 0f }; ft.Invoke(null, a);
                float al = (float)a[2], dx = (float)a[3], ba = (float)a[4];
                if (al < antA - 1e-5f) monotona = false; antA = al;
                if (al > .999f && llega[i] < 0) llega[i] = tt;
                if (ba > 0f && al < .05f) barraDespues = false;
                if (dx > 0f || dx < -Cte<float>(form, "FilaCorre") - .01f) monotona = false;
            }
        }
        bool escalon = true; for (int i = 1; i < 5; i++) if (!(llega[i] > llega[i - 1])) escalon = false;
        double cab = -1; for (double tt = 0; tt < 3000; tt += 10) if ((float)St(form, "CabeceraTop", tt) > 0f) { cab = tt; break; }
        double fila0 = -1; for (double tt = 0; tt < 3000; tt += 10) { object[] a = { tt, 0, 0f, 0f, 0f }; ft.Invoke(null, a); if ((float)a[2] > 0f) { fila0 = tt; break; } }
        float mCab = (float)mz.Invoke(null, new object[] { (double)vms + cab, true });
        Check(monotona && escalon && barraDespues && llega[4] < 2500, "las filas entran una detras de otra desde la izquierda y su barra crece despues (la quinta, a los " + Fm(llega[4] / 1000, "0.0") + " s)");
        Check(cab >= 0 && fila0 > cab && mCab > .88f, "la cabecera sale antes que las filas, y las dos cuando el cubo ya casi llego a su esquina (" + Fm(mCab * 100, "0") + " % del paso)");
        // Al irse y volver: nunca encima del cubo que pasa.
        bool limpio = true;
        for (float m = 0; m <= 1.0001f; m += .01f)
        {
            float sale = (float)St(form, "SalePagina", m), vit = (float)St(form, "VitrinaConTop", m);
            if (m <= .5f && sale > 0f) limpio = false;
            if (m >= .17f && vit > 0f) limpio = false;
        }
        Check(limpio && (float)St(form, "SalePagina", 1f) == 1f && (float)St(form, "VitrinaConTop", 0f) == 1f,
              "al ir y volver, la lista y el escaparate se apagan mientras el cubo pasa por encima");
        // Donde va la lista: debajo del cubo chico, sin salirse.
        bool dentro = true; string mal = "";
        foreach (Size s in new Size[] { new Size(800, 600), new Size(1024, 768), new Size(1280, 720), new Size(1366, 768), new Size(1920, 1080) })
        {
            object[] g = { s.Width, s.Height, null, null, null, null, null }; St(form, "GeometriaTop", g);
            RectangleF pag = (RectangleF)g[2]; float cabW = (float)g[3], cabH = (float)g[4], filasY = (float)g[5], filaH = (float)g[6];
            RectangleF cubo = (RectangleF)St(form, "CuboEsquina", s.Width, s.Height);
            bool ok = pag.X >= 0 && pag.Right <= s.Width && pag.Bottom <= s.Height && pag.Y + filasY >= cubo.Bottom && pag.X + cabW <= cubo.X && Math.Abs(cabH - cubo.Height) < .5f && filaH >= 30f && cubo.Right <= s.Width;
            if (!ok) { dentro = false; mal += " " + s.Width + "x" + s.Height; }
        }
        Check(dentro, "la cabecera al lado del cubo chico y las filas debajo, sin salirse (5 pantallas)" + mal);
        // Las de la noche, buscadas en la biblioteca.
        string dir = Path.Combine(Path.GetTempPath(), "rocola_atraccion_top_" + Environment.TickCount); Directory.CreateDirectory(dir);
        object f = ConTarjeta(); Set(f, "carpetaDatos", dir); List<object> ps = Biblioteca(f); Vista(f, 6);
        MethodInfo cuenta = M(form, "CuentaNocheGpu");
        DateTime ahora = DateTime.Now;
        cuenta.Invoke(f, new object[] { Item(ps[0], false), ahora }); cuenta.Invoke(f, new object[] { Item(ps[4], false), ahora });
        Set(f, "attractOn", true); Run(f, "EscaparateEntraGpu");
        Check(((IList)Get(f, "topFilas")).Count == 0 && !(bool)Run(f, "HayTopNoche"), "con dos canciones pedidas no hay lista (hacen falta tres)");
        cuenta.Invoke(f, new object[] { Item(Pista("c:/ya/no/esta.mp3", "Borrada", "Nadie", ""), false), ahora });
        Run(f, "EscaparateEntraGpu");
        Check(((IList)Get(f, "topFilas")).Count == 0, "ni con tres, si una ya no esta en la biblioteca (quedan dos que ensenar)");
        for (int i = 0; i < 3; i++) cuenta.Invoke(f, new object[] { Item(ps[4], false), ahora });
        cuenta.Invoke(f, new object[] { Item(ps[9], false), ahora });
        cuenta.Invoke(f, new object[] { Item(Pista("c:/borrada.mp3", "Borrada", "Nadie", ""), false), ahora });
        cuenta.Invoke(f, new object[] { Item(Pista("c:/borrada.mp3", "Borrada", "Nadie", ""), false), ahora });
        Run(f, "EscaparateEntraGpu");
        Check(!(bool)Run(f, "HayTopNoche") && (float)St(form, "MezclaTop", (double)(vms + 5000), (bool)Run(f, "HayTopNoche")) == 0f,
              "hasta que la lista esta horneada no sale (nunca un hueco en su lugar)");
        IList filas = (IList)Get(f, "topFilas");
        Check(filas.Count == 3 && (int)Get(filas[0], "Veces") == 4 && (string)Get(filas[0], "Titulo") == (string)Get(ps[4], "Title") && (string)Get(filas[0], "Portada") == "c:/m/Juan Gabriel/cover.jpg"
              && (int)Get(f, "topTotal") == 9, "las mas pedidas, con su titulo, su artista y la portada de su disco; la que ya no esta en la biblioteca se salta");
        List<string> ms = (List<string>)Get(f, "mensajes"); bool hay = false; foreach (string m in ms) if (m == "Lo más pedido esta noche: «" + (string)Get(ps[4], "Title") + "»") hay = true;
        Check(hay && ms.Count == 4, "y el escaparate lo anuncia en sus mensajes");
        Set(f, "topNoche", false); Run(f, "ArmaMensajes");
        bool calla = true; foreach (string m in (List<string>)Get(f, "mensajes")) if (m.Contains("esta noche")) calla = false;
        Set(f, "topNoche", true); Run(f, "ArmaMensajes");
        Check(calla, "con la idea apagada no la anuncia, aunque haya lista");
        for (int k = 0; k < 4; k++) Run(f, "AtraccionTic");
        Check(Get(f, "topBmp") != null && (bool)Run(f, "HayTopNoche"), "la lista se hornea en el reloj, mucho antes de que le toque");
        // La lista horneada.
        Bitmap tb = (Bitmap)Get(f, "topBmp");
        tb.Save(Path.Combine(dirPng, "lista.png"), ImageFormat.Png);
        object[] g2 = { 1024, 768, null, null, null, null, null }; St(form, "GeometriaTop", g2);
        float fy = (float)g2[5], fh = (float)g2[6];
        int amarillo = Parecidos(tb, new Rectangle(0, (int)fy, (int)(fh * .8f), (int)fh), Color.FromArgb(242, 206, 72), 18);
        int puesto2 = Parecidos(tb, new Rectangle(0, (int)(fy + fh), (int)(fh * .8f), (int)fh), Color.FromArgb(242, 206, 72), 18);
        int ambar = Parecidos(tb, new Rectangle(tb.Width - (int)(fh * 1.1f), (int)fy, (int)(fh * 1.1f), (int)fh * 3), Color.FromArgb(255, 198, 104), 20);
        int tapa = Opacos(tb, new Rectangle((int)(fh * .9f), (int)(fy + fh * .2f), (int)(fh * .7f), (int)(fh * .6f)));
        Info("pixeles: el 1 en amarillo " + amarillo + " (el 2: " + puesto2 + "), \"N veces\" " + ambar + ", portada de la primera " + tapa);
        Check(amarillo > 40 && puesto2 < 5 && ambar > 60 && tapa > (int)(fh * .7f * fh * .6f * .9f),
              "la lista: el primer puesto en amarillo, \"N veces\" en ambar y la portada (o su tapa de color si aun no llega)");
        // En pleno, desde la tarjeta: las barras en proporcion, y el cubo en la esquina.
        Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - (vms + 5000));
        object st = Run(f, "BuildAtraccionState");
        float[] largo = (float[])Get(st, "BarraLargo"), barra = (float[])Get(st, "Barra"), fa = (float[])Get(st, "FilaA");
        Check((bool)Get(st, "Top") && (int)Get(st, "Filas") == 3 && largo[0] == 1f && Math.Abs(largo[1] - .25f) < 1e-4f && barra[2] > .99f && fa[2] > .99f && (float)Get(st, "UiAlfa") == 0f,
              "en pleno: las tres filas, cada barra en proporcion a sus veces (la primera entera, 1 de 4 = un cuarto)");
        RectangleF cuboAhora = (RectangleF)Run(f, "CuboAtraccionAhora"), esq = (RectangleF)St(form, "CuboEsquina", 1024, 768);
        Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - 5000);
        RectangleF cubo0 = (RectangleF)Run(f, "CuboAtraccionAhora"), vitr = (RectangleF)St(form, "CuboEscaparate", 1024, 768);
        Check(cuboAhora == esq && cubo0 == vitr, "el cubo va a la esquina con la lista y vuelve a su sitio con el escaparate");
        bool continuo = true; RectangleF antR = RectangleF.Empty;
        for (double t = vms - 200; t < vms + 1200; t += 16.7)
        {
            Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - t);
            RectangleF r = (RectangleF)Run(f, "CuboAtraccionAhora");
            if (antR != RectangleF.Empty && (Math.Abs(r.X - antR.X) > 30 || Math.Abs(r.Width - antR.Width) > 12)) continuo = false;
            antR = r;
        }
        Check(continuo, "y va y viene sin saltos");
        // Sin escaparate: la atraccion de siempre, y sus dos textos se apagan mientras esta la lista.
        Set(f, "escaparate", false);
        Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - (vms + 5000));
        object sc = Run(f, "BuildAtraccionState");
        RectangleF cuboC = (RectangleF)Run(f, "CuboAtraccionAhora");
        Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - 5000);
        object sc0 = Run(f, "BuildAtraccionState");
        Check((bool)Get(sc, "Top") && !(bool)Get(sc, "Vitrina") && (float)Get(sc, "UiAlfa") == 0f && (float)Get(sc0, "UiAlfa") == 1f && cuboC == esq
              && (RectangleF)Run(f, "CuboAtraccionAhora") == (RectangleF)Run(f, "AttractCubeRect"),
              "sin escaparate: el cubo de siempre en su sitio de siempre, y para la lista se va a la esquina y los dos textos de siempre se apagan");
        // Y su respiro dorado se apaga con ellos (con el escaparate, el de siempre no sale: va con su mensaje).
        Array buf = Array.CreateInstance(gpu.GetNestedType("UiGlow", F), 10);
        MethodInfo glows = M(form, "FillUiGlows");
        uint[] alfa = new uint[3]; int[] cuantos = new int[3]; double[] cuando = { 5000, vms + 450, vms + 5000 };
        for (int k = 0; k < 3; k++)
        {
            Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - cuando[k]);
            cuantos[k] = (int)glows.Invoke(f, new object[] { buf });
            alfa[k] = cuantos[k] > 0 ? (uint)buf.GetValue(0).GetType().GetField("Argb").GetValue(buf.GetValue(0)) >> 24 : 0u;
        }
        Set(f, "escaparate", true); Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - 5000);
        int conEsc = (int)glows.Invoke(f, new object[] { buf });
        Check(cuantos[0] == 1 && alfa[0] > 20 && Math.Abs((int)alfa[1] - (int)alfa[0] / 2) <= 1 && cuantos[2] == 0 && conEsc == 0,
              "el respiro de siempre se apaga con su texto (" + alfa[0] + " -> " + alfa[1] + " -> nada), y con el escaparate no sale");
        try { Directory.Delete(dir, true); } catch { }
    }

    // ================================================================================================
    //  5) LOS ENGANCHES
    // ================================================================================================
    static void Enganches()
    {
        Console.WriteLine("\n  5) LOS ENGANCHES\n");
        List<string> entra = Codigo.Repasa(M(form, "EnterAttract")), sale = Codigo.Repasa(M(form, "ExitAttract"));
        Check(entra.IndexOf("EscaparateEntra") >= 0 && entra.IndexOf("EscaparateEntra") < entra.IndexOf("StartDiscTrans") && entra.Contains("CuboAtraccionAhora") && entra.Contains("AnguloAtraccion"),
              "al entrar, se apunta lo que se va a ensenar ANTES de que la portada vuele al cubo, y vuela a donde esta el cubo");
        int iSuelta = sale.IndexOf("EscaparateSale");
        Check(sale.IndexOf("ParCuboAtraccion") >= 0 && sale.IndexOf("CuboAtraccionAhora") >= 0 && sale.IndexOf("AnguloAtraccion") >= 0
              && iSuelta > sale.IndexOf("ParCuboAtraccion") && iSuelta > sale.IndexOf("CuboAtraccionAhora") && iSuelta > sale.IndexOf("AnguloAtraccion"),
              "al salir, el cubo vuela desde donde estaba y con la portada que tenia (se leen antes de soltar lo horneado)");
        MethodInfo cubo = M(form, "BuildGpuCubeState");
        Check(Codigo.Llama(cubo, "ParCuboAtraccion") && Codigo.Llama(cubo, "CuboAtraccionAhora") && Codigo.Llama(cubo, "AnguloAtraccion"), "el cubo de la tarjeta sale de ahi mismo");
        Check(Codigo.Llama(M(form, "AnimTimer_Tick"), "AtraccionTic"), "el reloj de la pantalla hornea lo que haga falta");
        List<string> cuadro = Codigo.Repasa(M(gpu, "RenderFrame"));
        int iPrep = cuadro.IndexOf("PreparaAtraccion"), iBegin = cuadro.IndexOf("Begin"), iDib = cuadro.IndexOf("DibujaAtraccion");
        Check(iPrep >= 0 && iPrep < iBegin && iDib > iBegin, "la tarjeta sube lo horneado antes de empezar el cuadro y lo dibuja despues de la interfaz");
        Check(Codigo.LeeCampo(Codigo.MetodosConAyudantes(gpu, "RenderFrame"), "AtrUiAlfa") && Codigo.Llama(M(gpu, "StopGpu"), "SueltaAtraccion") && Codigo.Llama(M(gpu, "PreparaAtraccion"), "SueltaAtraccion"),
              "los textos de siempre se apagan con la lista, y las texturas se sueltan al salir y al parar la tarjeta");
        Check(Codigo.EscribeCampo(Codigo.MetodosConAyudantes(form, "ApplyAlbumGpu"), "GetAtraccion"), "al encender la tarjeta, se le dice a quien pedirle lo de la atraccion");
    }

    // ================================================================================================
    //  6) Ctrl+F3 -> PANTALLA
    // ================================================================================================
    static void Ajustes()
    {
        Console.WriteLine("\n  6) CTRL+F3 -> PANTALLA -> LA ATRACCION Y EL ESTILO\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_atraccion_aj_" + Environment.TickCount);
        Directory.CreateDirectory(dir);
        object owner = Codigo.FormSinEncender(form);
        Set(owner, "carpetaDatos", dir);
        if (gpu != null) { Set(owner, "gpuPanel", Activator.CreateInstance(gpu, true)); Set(owner, "gpuEnabled", true); }
        string[] nom = { "El escaparate (GPU)", "Lo más pedido esta noche (GPU)" };
        string[] campo = { "escaparate", "topNoche" };
        using (Font fo = new Font("Segoe UI", 9))
        using (Panel page = (Panel)form.GetMethod("PaginaPantalla", F).Invoke(owner, new object[] { fo, fo, fo, Color.White, Color.Gray, Color.Gray }))
        {
            Control[] sw = new Control[2]; bool estan = true;
            for (int i = 0; i < 2; i++) { sw[i] = Find(page, nom[i]); if (sw[i] == null) estan = false; }
            Check(estan, "la pestaña Pantalla tiene los dos nuevos");
            if (!estan) return;
            List<Control> hojas = new List<Control>(); List<Control> botones = new List<Control>();
            foreach (Control c in page.Controls) { if (c.GetType() == typeof(Panel)) hojas.Add(c); else if (c.GetType().Name == "BotonPlano" && c.Top < 40) botones.Add(c); }
            Control hojaAtr = sw[0].Parent.Parent;
            int k = hojas.IndexOf(hojaAtr);
            Check(hojas.Count == 3 && botones.Count == 3 && k == 2 && botones[2].Text == "La atracción y el estilo" && !hojaAtr.Visible,
                  "en su hoja, la tercera (\"" + (botones.Count > 2 ? botones[2].Text : "") + "\"), que no es la que se ve al abrir");
            typeof(Control).GetMethod("OnClick", F).Invoke(botones[2], new object[] { EventArgs.Empty });
            bool sola = hojaAtr.Visible; foreach (Control h in hojas) if (h != hojaAtr && h.Visible) sola = false;
            typeof(Control).GetMethod("OnClick", F).Invoke(botones[0], new object[] { EventArgs.Empty });
            Check(sola && !hojaAtr.Visible && hojas[0].Visible, "su boton la ensena (y solo a ella)");
            bool cabe = true;
            foreach (Control card in hojaAtr.Controls)
            {
                if (card.Right > hojaAtr.Width || card.Bottom > hojaAtr.Height) cabe = false;
                foreach (Control x in card.Controls) { if (x.Right > card.Width || x.Bottom > card.Height) cabe = false; foreach (Control y in card.Controls) if (x != y && x.Bounds.IntersectsWith(y.Bounds)) cabe = false; }
            }
            Check(cabe, "todo cabe en su tarjeta sin taparse");
            PropertyInfo marcado = sw[0].GetType().GetProperty("Checked", F);
            bool guarda = true;
            for (int mask = 3; mask >= 0; mask--)
            {
                for (int i = 0; i < 2; i++) marcado.SetValue(sw[i], (mask & (1 << i)) != 0, null);
                form.GetMethod("SaveConfigPrefs", F).Invoke(owner, null);
                Codigo.EsperaGuardado(form, owner);
                object leido = Codigo.FormSinEncender(form); form.GetMethod("LoadConfigPrefs", F).Invoke(leido, null);
                for (int i = 0; i < 2; i++) if ((bool)Get(leido, campo[i]) != ((mask & (1 << i)) != 0) || (bool)Get(owner, campo[i]) != ((mask & (1 << i)) != 0)) guarda = false;
            }
            Check(guarda, "las 4 combinaciones se guardan en rocola_config.txt y se leen igual");
            Control reset = Find(page, "Pantalla como de fabrica");
            try { typeof(Control).GetMethod("OnClick", F).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            bool todos = true; for (int i = 0; i < 2; i++) if (!(bool)Get(owner, campo[i]) || !(bool)marcado.GetValue(sw[i], null)) todos = false;
            for (int i = 0; i < 2; i++) Set(owner, campo[i], false);
            try { typeof(Control).GetMethod("OnClick", F).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            bool puestas = true; for (int i = 0; i < 2; i++) if (!(bool)Get(owner, campo[i])) puestas = false;
            Check(reset != null && todos && puestas, "\"Pantalla como de fabrica\" los vuelve a encender (las preferencias, no solo los interruptores)");
        }
        try { Directory.Delete(dir, true); } catch { }
        string fuente = Path.Combine(Path.Combine("..", "Rocola"), "AtraccionGpu.cs");
        string txt = File.Exists(fuente) ? File.ReadAllText(fuente) : "";
        bool fabrica = txt.Length > 0; foreach (string c in campo) if (txt.IndexOf("private bool " + c + " = true;") < 0) fabrica = false;
        Check(fabrica, "de fabrica vienen encendidos");
    }

    // ================================================================================================
    //  7) LO QUE CUESTA
    // ================================================================================================
    static double Mide(int veces, Action a)
    {
        a();
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < veces; i++) a();
        return sw.Elapsed.TotalMilliseconds / veces;
    }
    static void Coste()
    {
        Console.WriteLine("\n  7) LO QUE CUESTA\n");
        object fx = Activator.CreateInstance(fichaT, true);
        Set(fx, "Nombre", "Jefe de Jefes (Edicion Especial)"); Set(fx, "Artista", "Los Tigres del Norte"); Set(fx, "Genero", "Regional");
        Set(fx, "GeneroColor", Color.FromArgb(215, 120, 25)); Set(fx, "Canciones", 24); Set(fx, "Pedidas", 31);
        double ficha = Mide(10, delegate { ((Bitmap)St(form, "HorneaFichaVitrina", fx, 450, 768)).Dispose(); });
        double ficha19 = Mide(10, delegate { ((Bitmap)St(form, "HorneaFichaVitrina", fx, 1000, 1080)).Dispose(); });
        string dir = Path.Combine(Path.GetTempPath(), "rocola_atraccion_coste_" + Environment.TickCount); Directory.CreateDirectory(dir);
        object f = ConTarjeta(); Set(f, "carpetaDatos", dir); List<object> ps = Biblioteca(f); Vista(f, 6);
        MethodInfo cuenta = M(form, "CuentaNocheGpu");
        for (int i = 0; i < 9; i++) cuenta.Invoke(f, new object[] { Item(ps[i * 2], false), DateTime.Now });
        Set(f, "attractOn", true); Run(f, "EscaparateEntraGpu");
        double msgs = Mide(10, delegate { Run(f, "HorneaLosMensajes", 1024, 768); });
        double top = Mide(5, delegate { Run(f, "HorneaLaLista", 1024, 768); });
        Tamano(f, 1920, 1080);
        double top19 = Mide(5, delegate { Run(f, "HorneaLaLista", 1920, 1080); });
        Tamano(f, 1024, 768); Run(f, "HorneaLaLista", 1024, 768);
        double entra = Mide(5, delegate { Run(f, "EscaparateEntraGpu"); });
        for (int k = 0; k < 4; k++) Run(f, "AtraccionTic");
        Set(f, "atrDesde", (double)St(gpu, "AhoraMs") - 32000);
        double vacio = Mide(2000, delegate { Run(f, "EscaparateActivo"); Run(f, "EscaparateActivo"); Run(f, "EscaparateActivo"); }) * 1000.0;
        double cuadro = Mide(2000, delegate { Run(f, "BuildAtraccionState"); Run(f, "CuboAtraccionAhora"); Run(f, "AnguloAtraccion"); }) * 1000.0;
        Info("hornear la ficha: " + Fm(ficha, "0.00") + " ms (1920x1080: " + Fm(ficha19, "0.00") + " ms), cada ~8 s; los mensajes: " + Fm(msgs, "0.00") + " ms, una vez");
        Info("la lista de la noche: " + Fm(top, "0.00") + " ms (1920x1080: " + Fm(top19, "0.00") + " ms), una vez por atraccion; entrar (apuntar y hornear lo primero): " + Fm(entra, "0.00") + " ms");
        double neto = cuadro - vacio;
        Info("por cuadro, lo que calcula la rocola para la tarjeta: " + (neto < 5 ? "menos de 5 us (no se distingue del ruido de la medida)" : Fm(neto, "0.0") + " us")
             + " (medido " + Fm(cuadro, "0.0") + " us, contando las tres llamadas por reflexion, que solas son " + Fm(vacio, "0.0") + " us)");
        Check(ficha19 < 40 && top19 < 80 && entra < 60, "hornear es cosa de milisegundos y solo pasa al entrar o al cambiar de disco");
        Check(neto < 50, "y por cuadro no llega a 50 microsegundos");
        try { Directory.Delete(dir, true); } catch { }
    }

    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        form = asm.GetType("Rocola.Form1"); gpu = asm.GetType("Rocola.GpuPanel");
        itemT = form.GetNestedType("PlayItem", F); trackT = form.GetNestedType("Track", F); albumT = form.GetNestedType("Album", F); viewT = form.GetNestedType("AttractView", F);
        dirPng = Path.Combine(Path.GetTempPath(), "rocola_atraccion_png"); Directory.CreateDirectory(dirPng);
        Console.WriteLine("  perfil: " + (gpu != null ? "con tarjeta grafica" : "sin tarjeta grafica (solo la pestaña de ajustes y los enganches quietos)"));
        if (gpu != null)
        {
            atrT = asm.GetType("Rocola.GpuAtraccion"); fichaT = asm.GetType("Rocola.FichaVitrina"); filaT = asm.GetType("Rocola.FilaNoche");
            Check(atrT != null && fichaT != null && filaT != null && gpu.GetField("GetAtraccion", F) != null, "la tarjeta pide a la rocola lo de la atraccion, con lo suyo");
            Siempre(); Noche(); Escaparate(); Top(); Enganches();
        }
        else
        {
            Console.WriteLine("\n  1) SIN TARJETA GRAFICA\n");
            SinTarjeta();
        }
        Ajustes();
        if (gpu != null) Coste();
        Console.WriteLine("\n  imagenes para mirar: " + dirPng);
        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN (" + oks + " comprobaciones)" : fallos + " FALLO(S)"));
        return fallos;
    }
}

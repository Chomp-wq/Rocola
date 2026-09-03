using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// EN EL BUSCADOR TAMBIEN: CUAL YA PEDISTE Y CUAL SUENA.
//
// Un cliente escribe "Depeche" y le salen cuarenta canciones. Dentro del disco la rocola SI se lo dice -la
// lista lleva "EN COLA 3" en verde y "SONANDO" en ambar desde hace tiempo-, pero en el buscador no habia
// nada: cuarenta renglones iguales, y a los diez minutos vuelve a pedir la misma que ya pidio. Es
// exactamente la queja que resolvio el palomito de la pared, en la otra pantalla donde se elige.
//
// Y SE DICE CON LAS MISMAS PALABRAS Y LOS MISMOS COLORES que dentro del disco. Verde = la vas a oir, ambar =
// la estas oyendo. Una rocola que llame a lo mismo de dos maneras obliga al cliente a aprenderse dos.
//
// LO QUE ESTA BATERIA VIGILA:
//
//   1) QUE SE VEA. No que el codigo este: que al pedir una cancion, los pixeles de esa tarjeta CAMBIEN. Es
//      la unica forma de saber que la marca sale, y es donde se han escondido los fallos de este tipo.
//   2) QUE SEA VERDE, del mismo verde. Y que la de "suena ahora" sea de la otra familia de color.
//   3) QUE HAYA UN SOLO SITIO QUE DIGA ESOS DOS COLORES. Con una copia en cada pantalla, el dia que alguien
//      cambie uno tendriamos dos verdes distintos y nadie se enteraria hasta verlas seguidas.
//   4) QUE SE CONTESTE CON LAS MISMAS TABLAS que la lista del disco, no con una copia que se desparejaria.
//   5) QUE EL INTERRUPTOR DE Ctrl+F3 LAS APAGUE. Es el mismo adorno que el palomito de la pared, asi que se
//      apaga con el mismo boton: dos interruptores para lo mismo seria peor que ninguno.
//   6) QUE AL PEDIR UNA CANCION SE ENTERE. Las tarjetas van horneadas a un mapa de bits que se guarda; sin
//      avisar, la marca no saldria hasta teclear otra letra. Y con tarjeta grafica, lo mismo con su textura.
//   7) QUE NO CUESTE. Cuarenta tarjetas por tecla: el fondo de la pastilla se hornea una vez, no cuarenta.
class Pedidas
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TSO, TItem, TAlb, TTrack;

    static MethodBase Met(Type t, string n)
    {
        if (t == null) return null;
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static string Fam()
    {
        foreach (FontFamily f in FontFamily.Families)
            if (f.Name == "DejaVu Sans" || f.Name == "Liberation Sans" || f.Name == "Arial") return f.Name;
        return FontFamily.GenericSansSerif.Name;
    }

    static readonly string[] Nombres = {
        "Depeche Mode - Home", "Depeche Mode - Halo", "Depeche Mode - Stripped", "Depeche Mode - Useless",
        "Depeche Mode - Shine", "Depeche Mode - Judas", "Depeche Mode - Macro", "Depeche Mode - Peace" };

    // Cuales estan pedidas: lo pone la prueba antes de cada dibujo.
    static Dictionary<int, int> cola = new Dictionary<int, int>();
    static int sonando = -1;
    static IList resultados;
    static int IndiceDe(object it)
    {
        for (int i = 0; i < resultados.Count; i++) if (ReferenceEquals(resultados[i], it)) return i;
        return -1;
    }
    static int Pos<T>(T it) { int i = IndiceDe(it); int p; return (i >= 0 && cola.TryGetValue(i, out p)) ? p : 0; }
    static bool Suena<T>(T it) { return IndiceDe(it) == sonando; }

    static Control Monta(Assembly asm, int w, int h)
    {
        Control so = (Control)Activator.CreateInstance(TSO, true);
        so.SetBounds(0, 0, w, h);
        so.Font = new Font(Fam(), 12f, FontStyle.Bold);

        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Ultra");
        TAlb.GetField("Name").SetValue(alb, "Ultra (Deluxe)");
        TAlb.GetField("Artist").SetValue(alb, "Depeche Mode");

        Type tLista = typeof(List<>).MakeGenericType(TItem);
        IList res = (IList)Activator.CreateInstance(tLista);
        for (int i = 0; i < Nombres.Length; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            object tk = Activator.CreateInstance(TTrack, true);
            TTrack.GetField("Path").SetValue(tk, @"C:\Musica\Ultra\" + i + ".mp3");
            TItem.GetField("Kind").SetValue(it, 2);   // cancion
            TItem.GetField("Display").SetValue(it, Nombres[i]);
            TItem.GetField("Key").SetValue(it, Nombres[i].ToLowerInvariant());
            TItem.GetField("Score").SetValue(it, 5000 - i);
            TItem.GetField("AlbumRef").SetValue(it, alb);
            TItem.GetField("TrackRef").SetValue(it, tk);
            res.Add(it);
        }
        resultados = res;
        TSO.GetField("Results").SetValue(so, res);
        TSO.GetField("Query").SetValue(so, "Depeche");
        FieldInfo fEs = TSO.GetField("Escrito");
        if (fEs != null) fEs.SetValue(so, "Depeche");
        TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
        TSO.GetField("FocusResults").SetValue(so, true);
        TSO.GetField("SelResult").SetValue(so, 0);

        FieldInfo fq = TSO.GetField("GetQueuePos"), fs = TSO.GetField("GetSonando");
        if (fq != null)
        {
            Type td = typeof(Func<,>).MakeGenericType(TItem, typeof(int));
            MethodInfo mi = typeof(Pedidas).GetMethod("Pos", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem);
            fq.SetValue(so, Delegate.CreateDelegate(td, mi));
        }
        if (fs != null)
        {
            Type td = typeof(Func<,>).MakeGenericType(TItem, typeof(bool));
            MethodInfo mi = typeof(Pedidas).GetMethod("Suena", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem);
            fs.SetValue(so, Delegate.CreateDelegate(td, mi));
        }
        return so;
    }

    static int ver = 0;
    static Bitmap Pinta(Control so, int w, int h)
    {
        // Cada dibujo con una cola distinta: si no, el cache de tarjetas devolveria la de antes y esta
        // prueba no estaria mirando nada.
        FieldInfo fc = TSO.GetField("ColaVer");
        if (fc != null) fc.SetValue(so, ++ver);
        MethodInfo mR = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(255, 8, 12, 36));
            mR.Invoke(so, new object[] { g, new Rectangle(0, 0, w, h), false });
        }
        return b;
    }

    // Cuantos pixeles cambian entre dos pantallas, y cuantos de ellos tiran a un color dado.
    static int Distintos(Bitmap a, Bitmap b, Color tira, out int delColor)
    {
        int n = 0; delColor = 0;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                if (p.ToArgb() == q.ToArgb()) continue;
                n++;
                // "tira a" ese color: ese canal manda claramente sobre los otros dos, como en el original.
                if (Parecido(q, tira)) delColor++;
            }
        return n;
    }
    static bool Parecido(Color c, Color m)
    {
        int mx = Math.Max(m.R, Math.Max(m.G, m.B));
        // El canal que manda en el color de la marca tiene que mandar tambien en el pixel, y con holgura.
        if (mx == m.G) return c.G > c.R + 12 && c.G > c.B + 12;
        if (mx == m.R) return c.R > c.B + 12 && c.G > c.B + 12;   // el ambar es rojo+verde
        return c.B > c.R + 12 && c.B > c.G + 12;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SearchOverlay") TSO = t;
            if (t.Name == "SearchItem") TItem = t;
            if (t.Name == "Album") TAlb = t;
            if (t.Name == "Track") TTrack = t;
        }
        if (TSO == null || TItem == null || TAlb == null || TTrack == null)
        { Console.WriteLine("  FALLO  no se encontro el buscador"); return 1; }

        Console.WriteLine("\n  1) LAS PIEZAS\n");
        Check(TSO.GetField("GetQueuePos") != null, "el buscador puede preguntar si una cancion ya esta pedida (GetQueuePos)");
        Check(TSO.GetField("GetSonando") != null, "y cual esta sonando (GetSonando)");
        Check(TSO.GetField("ColaVer") != null, "y se entera de que la cola cambio (ColaVer)");
        if (TSO.GetField("GetQueuePos") == null || TSO.GetField("ColaVer") == null)
        { Console.WriteLine("\n  " + Math.Max(fallos, 1) + " FALLO(S)"); return Math.Max(fallos, 1); }

        Console.WriteLine("\n  2) Y SE VE: LOS PIXELES CAMBIAN\n");
        const int W = 1024, H = 700;
        Control so = Monta(asm, W, H);
        Color verde = (Color)TF.GetField("ColaVerde", TODO).GetValue(null);
        Color ambar = (Color)TF.GetField("SonAmbar", TODO).GetValue(null);
        int deColor;
        cola.Clear(); sonando = -1;
        Bitmap limpia = Pinta(so, W, H);
        cola[2] = 3;                       // la tercera cancion, pedida y en el puesto 3
        Bitmap conCola = Pinta(so, W, H);
        int n1 = Distintos(limpia, conCola, verde, out deColor);
        Console.WriteLine("     al pedir una cancion cambian " + n1 + " pixeles, " + deColor + " de ellos verdes");
        Check(n1 > 200, "pedir una cancion cambia lo que se ve en el buscador");
        Check(deColor > 60, "y lo que aparece es VERDE: la marca de \"ya la pediste\"");

        cola.Clear(); sonando = 4;
        Bitmap conSon = Pinta(so, W, H);
        int n2 = Distintos(limpia, conSon, ambar, out deColor);
        Console.WriteLine("     y con una sonando cambian " + n2 + " pixeles, " + deColor + " de ellos ambar");
        Check(n2 > 200, "y la que suena tambien se ve");
        Check(deColor > 60, "y esa es AMBAR: de otra familia de color, para distinguirlas sin leer");

        // LAS DOS A LA VEZ. Una cancion puede estar sonando Y estar pedida otra vez, y el cliente quiere
        // saber las dos cosas. Pero en una tarjeta estrecha no caben las dos marcas MAS el renglon que dice
        // de que disco es, y ahi hay que elegir.
        //
        // MANDA EL VERDE. El cliente esta eligiendo cancion: lo que le hace falta saber es que ESA ya la
        // pidio, que es justo lo que evita que la pida dos veces. Que suene ahora ya se lo dice la franja de
        // abajo, con el nombre y la barra de avance.
        cola.Clear(); cola[4] = 1; sonando = 4;
        Bitmap ambas = Pinta(so, W, H);
        int nv, na;
        Distintos(limpia, ambas, verde, out nv);
        Distintos(limpia, ambas, ambar, out na);
        Console.WriteLine("     en la tarjeta estrecha, sonando Y pedida: " + nv + " pixeles verdes y " + na + " ambar");
        Check(nv > 60, "si solo cabe una, sale la verde: la que evita que se pida dos veces");

        // Y EN UNA PANTALLA GRANDE, LAS DOS. La del bar es de 1920: ahi la tarjeta mide mas del doble y las
        // dos marcas caben con sitio de sobra.
        Control ancho = Monta(asm, 1920, 1080);
        cola.Clear(); sonando = -1;
        Bitmap g0 = Pinta(ancho, 1920, 1080);
        int anchoTarj = 0;
        FieldInfo fR2 = TSO.GetField("resultRects", TODO);
        if (fR2 != null)
        {
            IList rects2 = (IList)fR2.GetValue(ancho);
            if (rects2 != null && rects2.Count > 0)
            {
                object kv2 = rects2[0];
                anchoTarj = (int)((RectangleF)kv2.GetType().GetProperty("Key").GetValue(kv2, null)).Width;
            }
        }
        cola[4] = 1; sonando = 4;
        Bitmap g1 = Pinta(ancho, 1920, 1080);
        int nv2, na2;
        Distintos(g0, g1, verde, out nv2);
        Distintos(g0, g1, ambar, out na2);
        Console.WriteLine("     en 1920 la tarjeta mide " + anchoTarj + " px: " + nv2 + " pixeles verdes y " + na2 + " ambar");
        Check(nv2 > 60 && na2 > 60, "en la pantalla del bar salen LAS DOS: la estas oyendo Y la vas a volver a oir");
        g0.Dispose(); g1.Dispose();

        Console.WriteLine("\n  3) LOS DOS COLORES, EN UN SOLO SITIO\n");
        // Antes vivian dentro de la lista del disco. Ahora los usan dos pantallas, asi que si cada una se
        // quedara con su copia acabariamos con dos verdes distintos.
        int cuantos = 0;
        foreach (Type t in asm.GetTypes())
        {
            foreach (FieldInfo f in t.GetFields(TODO | BindingFlags.DeclaredOnly))
                if (f.Name == "ColaVerde") cuantos++;
            foreach (Type x in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (FieldInfo f in x.GetFields(TODO | BindingFlags.DeclaredOnly))
                    if (f.Name == "ColaVerde") cuantos++;
        }
        Console.WriteLine("     el verde de \"ya la pediste\" esta escrito en " + cuantos + " sitio(s)");
        Check(cuantos == 1, "un solo sitio dice de que verde es, y las dos pantallas lo piden ahi");

        Console.WriteLine("\n  4) SE CONTESTA CON LAS MISMAS TABLAS QUE LA LISTA DEL DISCO\n");
        // Si el buscador se armara su propia cuenta de quien esta en la cola, el dia que alguien arregle una
        // se olvidaria de la otra y las dos pantallas dirian cosas distintas.
        List<MethodBase> ui = Codigo.MetodosConAyudantes(TF, "BuildUi");
        Check(Codigo.LeeCampo(ui, "colaPos"), "el buscador mira la MISMA tabla de la cola (colaPos)");
        Check(Codigo.LeeCampo(ui, "sonandoKey"), "y el MISMO apunte de cual suena (sonandoKey)");

        Console.WriteLine("\n  5) Y EL INTERRUPTOR DE Ctrl+F3 LAS APAGA\n");
        // Es el mismo adorno que el palomito de la pared: se apaga con el mismo boton.
        Check(Codigo.LeeCampo(ui, "marcaColaOn"), "las marcas del buscador respetan el interruptor del palomito");

        Console.WriteLine("\n  6) Y AL PEDIR UNA CANCION, SE ENTERA\n");
        MethodBase refresca = Met(TF, "RefrescaCola");
        Check(refresca != null, "hay un solo sitio que rehace la cola (RefrescaCola)");
        int lee, esc;
        Codigo.UsosDeCampo(refresca, "ColaVer", out lee, out esc);
        Check(esc > 0, "y avisa al buscador de que la cola cambio");
        // Y ese aviso tiene que entrar en la firma del cache: si no, avisar no serviria de nada.
        MethodBase pinta = null;
        foreach (MethodInfo m in TSO.GetMethods(TODO | BindingFlags.DeclaredOnly))
            if (m.Name == "DrawResults" || m.Name == "RenderContent") { }
        foreach (MethodInfo m in TSO.GetMethods(TODO | BindingFlags.DeclaredOnly))
        {
            Codigo.UsosDeCampo(m, "resCacheCola", out lee, out esc);
            if (lee > 0) pinta = m;
        }
        Check(pinta != null, "y el cache de las tarjetas lleva la cola en su firma");
        // Con tarjeta grafica la pantalla del buscador va a una textura con su propia firma.
        MethodBase firma = Met(TF, "UiSig");
        if (firma == null)
            Console.WriteLine("     (esta version no dibuja por tarjeta grafica: no hay textura de pantalla)");
        else
        {
            Codigo.UsosDeCampo(firma, "colaVer", out lee, out esc);
            Check(lee > 0, "y con tarjeta grafica, la textura de la pantalla tambien");
        }

        Console.WriteLine("\n  7) Y NO CUESTA CUARENTA VECES\n");
        // Cuarenta tarjetas en pantalla y una consulta nueva en cada tecla: el fondo redondeado de la
        // pastilla se hornea UNA vez por texto y se guarda, como ya se hacia con la del genero.
        bool horno = false;
        foreach (FieldInfo f in TSO.GetFields(TODO | BindingFlags.DeclaredOnly))
            if (f.Name == "hornoMarca") horno = true;
        Check(horno, "el fondo de la pastilla se hornea una vez por texto, no en cada tarjeta");

        limpia.Dispose(); conCola.Dispose(); conSon.Dispose(); ambas.Dispose();
        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}

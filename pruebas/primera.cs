using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LAS ANIMACIONES TIENEN QUE IR FLUIDAS DESDE LA PRIMERA VEZ.
//
// Casi todo lo que la rocola dibuja se HORNEA la primera vez que hace falta: el teclado del buscador son
// 40 teclas con su degradado, su contorno y su letra; la lista de un disco se guarda hecha para poder
// deslizarla; la pared tiene el contorno de la seleccion horneado aparte... Y todo eso se hornea DENTRO
// del primer cuadro de la animacion, que es justo el peor momento: el cliente pulsa, la rocola se para a
// hornear, y el movimiento arranca a tirones. A partir de la segunda vez va fino.
//
// Peor todavia: la primera vez que se pasa de pagina dentro de un disco NO HAY ANIMACION. El deslizamiento
// necesita la pagina anterior ya horneada para sacarla por un lado, y la primera vez no hay ninguna, asi
// que la pagina cambia de golpe (ver SongListView.StartSlide: "sin nada horneado no hay de donde salir").
//
// Aqui se mide, pieza por pieza, lo que cuesta EL PRIMER cuadro contra lo que cuesta uno normal. Lo que
// salga muy por encima es lo que hay que precalentar; lo que salga igual no se toca, que precalentar
// tambien gasta memoria y en la rocola del bar y en Puppy eso cuenta.
class Primera
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PUB = BindingFlags.Instance | BindingFlags.Public;
    const double Cuadro = 16.6;   // lo que dura un cuadro a 60 por segundo

    static Type TF;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static void Pon(Type t, object o, string campo, object v)
    {
        FieldInfo f = t.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(o, v);
    }

    static Image portada;
    static Image Tapa<T>(T a) { return portada; }
    static string Gen<T>(T a) { return "rock"; }

    static Bitmap Lienzo(int w, int h) { return new Bitmap(Math.Max(8, w), Math.Max(8, h), PixelFormat.Format32bppPArgb); }

    // ---------- LA MEDIDA ----------
    // La 1a vez: un control RECIEN hecho y UN SOLO dibujo. Se repite con controles nuevos y se toma el
    // minimo, que es lo mas cerca del coste real (la maquina esta compartida).
    // En marcha: el MISMO control, ya caliente, dibujando una y otra vez.
    class Fila { public string Nombre; public double Uno, Marcha; public bool Medida; public string Nota = ""; }
    static readonly List<Fila> tabla = new List<Fila>();

    static Fila Mide(string nombre, Func<object> crea, Action<object> pinta)
    {
        Fila f = new Fila(); f.Nombre = nombre;
        tabla.Add(f);
        try
        {
            double uno = double.MaxValue;
            for (int i = 0; i < 7; i++)
            {
                object c = crea();
                Stopwatch sw = Stopwatch.StartNew();
                pinta(c);
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms < uno) uno = ms;
            }
            object w = crea();
            pinta(w); pinta(w); pinta(w);
            double mejor = double.MaxValue;
            for (int t = 0; t < 7; t++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < 12; i++) pinta(w);
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds / 12;
                if (ms < mejor) mejor = ms;
            }
            f.Uno = uno; f.Marcha = mejor; f.Medida = true;
        }
        catch (Exception ex)
        {
            f.Nota = "no se pudo medir (" + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")";
        }
        Pinta(f);
        return f;
    }

    static int cuantosPagan = 0;
    static readonly List<string> quePagan = new List<string>();

    // PAGA precalentar si el primer cuadro se lleva por delante mas de un cuadro entero de mas, o si cuesta
    // mas del doble que uno normal y la diferencia se nota (mas de 2 ms).
    static void Pinta(Fila f)
    {
        if (!f.Medida) { Console.WriteLine(string.Format("     {0,-30} {1}", f.Nombre, f.Nota)); return; }
        double demas = f.Uno - f.Marcha;
        bool paga = (demas > Cuadro) || (f.Uno > f.Marcha * 2 && demas > 2.0);
        if (paga) { cuantosPagan++; quePagan.Add(f.Nombre); }
        Console.WriteLine(string.Format("     {0,-30} {1,7:0.00} ms {2,7:0.00} ms {3,7:0.00} ms  {4}",
                          f.Nombre, f.Uno, f.Marcha, demas, paga ? "SI  <-- se traba" : "no, ya va fina"));
    }

    // ---------- LOS CONTROLES, MONTADOS COMO EN LA ROCOLA ----------
    static Font Fuente(float pt) { return new Font(FontFamily.GenericSansSerif, pt, FontStyle.Bold); }

    static Control MontaLista(int w, int h)
    {
        Type T = Nes("SongListView");
        Control c = (Control)Activator.CreateInstance(T, true);
        c.SetBounds(0, 0, w, h); c.Font = Fuente(11f);
        Pon(T, c, "RowH", 38);
        Pon(T, c, "Accent", Color.FromArgb(150, 110, 220));
        Pon(T, c, "GetCount", (Func<int>)delegate { return 60; });
        Pon(T, c, "GetText", (Func<int, string>)delegate (int i) { return "Cancion numero " + (i + 1) + " con un titulo largo"; });
        Pon(T, c, "GetRight", (Func<int, string>)delegate (int i) { return "3:4" + (i % 10); });
        Pon(T, c, "GetQueuePos", (Func<int, int>)delegate (int i) { return (i % 7 == 0) ? 1 : (i % 11 == 0) ? -1 : 0; });
        Pon(T, c, "GetGenero", (Func<int, string>)delegate (int i) { return (i % 3 == 0) ? "Regional" : "Pop"; });
        return c;
    }

    static Control MontaPared(int w, int h)
    {
        Type T = Nes("AlbumView"), TAlb = Nes("Album");
        Control c = (Control)Activator.CreateInstance(T, true);
        c.SetBounds(0, 0, w, h); c.Font = Fuente(10f);
        Type tf = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        T.GetField("GetCover").SetValue(c, Delegate.CreateDelegate(tf,
            typeof(Primera).GetMethod("Tapa", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
        IList pag = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        for (int i = 0; i < 18; i++)
        {
            object a = Activator.CreateInstance(TAlb, true);
            TAlb.GetField("Folder").SetValue(a, @"C:\Musica\Disco" + i);
            TAlb.GetField("Name").SetValue(a, "Disco numero " + i + " con nombre largo");
            TAlb.GetField("Artist").SetValue(a, "Artista " + i);
            TAlb.GetField("Letter").SetValue(a, 'A');
            TAlb.GetField("CoverPath").SetValue(a, @"C:\Musica\Disco" + i + @"\cover.jpg");
            pag.Add(a);
        }
        T.GetField("Page").SetValue(c, pag);
        return c;
    }

    static Control MontaBuscador(int w, int h)
    {
        Type T = Nes("SearchOverlay"), TItem = Nes("SearchItem"), TAlb = Nes("Album");
        Control c = (Control)Activator.CreateInstance(T, true);
        c.SetBounds(0, 0, w, h); c.Font = Fuente(12f);
        IList res = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem));
        string[] nom = { "Depeche Mode - Enjoy The Silence", "Jose Jose - El Triste", "Metallica - Enter Sandman",
                         "Juan Gabriel - Amor Eterno", "Bronco - Que No Quede Huella" };
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Disco");
        TAlb.GetField("Name").SetValue(alb, "Un disco");
        TAlb.GetField("Artist").SetValue(alb, "Un artista");
        TAlb.GetField("CoverPath").SetValue(alb, @"C:\Musica\Disco\cover.jpg");
        for (int i = 0; i < 40; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            TItem.GetField("Kind").SetValue(it, i % 3);
            TItem.GetField("Display").SetValue(it, nom[i % nom.Length] + " " + i);
            TItem.GetField("Key").SetValue(it, (nom[i % nom.Length] + " " + i).ToLowerInvariant());
            TItem.GetField("Score").SetValue(it, 5000 - i);
            TItem.GetField("AlbumRef").SetValue(it, alb);
            res.Add(it);
        }
        Type tf = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        T.GetField("GetCover").SetValue(c, Delegate.CreateDelegate(tf,
            typeof(Primera).GetMethod("Tapa", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
        FieldInfo fg = T.GetField("GetGenero");
        if (fg != null) fg.SetValue(c, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(string)),
            typeof(Primera).GetMethod("Gen", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem)));
        T.GetField("Results").SetValue(c, res);
        T.GetField("Query").SetValue(c, "de");
        T.GetField("Accent").SetValue(c, Color.FromArgb(178, 152, 255));
        T.GetField("FocusResults").SetValue(c, false);
        T.GetField("SelResult").SetValue(c, 0);
        return c;
    }

    static Control MontaSimple(string tipo, int w, int h, float pt, string[] campos, object[] valores)
    {
        Type T = Nes(tipo);
        if (T == null) return null;
        Control c = (Control)Activator.CreateInstance(T, true);
        c.SetBounds(0, 0, w, h); c.Font = Fuente(pt);
        for (int i = 0; i < campos.Length; i++) Pon(T, c, campos[i], valores[i]);
        return c;
    }

    static Action<object> Pinta(string tipo, int w, int h)
    {
        Type T = Nes(tipo);
        MethodInfo m3 = T.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        MethodInfo m1 = T.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
        Bitmap b = Lienzo(w, h);
        Graphics g = Graphics.FromImage(b);
        object[] a3 = new object[] { g, new Rectangle(0, 0, w, h), false };
        object[] a1 = new object[] { g };
        if (m3 != null) return delegate (object c) { m3.Invoke(c, a3); };
        if (m1 != null) return delegate (object c) { m1.Invoke(c, a1); };
        return null;
    }

    static int Difiere(Bitmap a, Bitmap b, out int peor)
    {
        peor = 0; int n = 0;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                int d = Math.Max(Math.Max(Math.Abs(p.R - q.R), Math.Abs(p.G - q.G)),
                        Math.Max(Math.Abs(p.B - q.B), Math.Abs(p.A - q.A)));
                if (d > 0) { n++; if (d > peor) peor = d; }
            }
        return n;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        portada = new Bitmap(500, 500, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada))
        {
            pg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 60; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) pg.FillEllipse(sb, i * 8, i * 7, 70, 70);
        }

        // ---------- 1) LO QUE CUESTA LA PRIMERA VEZ ----------
        Console.WriteLine("\n  1) LO QUE CUESTA EL PRIMER CUADRO DE CADA COSA, CONTRA UNO NORMAL\n");
        Console.WriteLine(string.Format("     {0,-30} {1,10} {2,10} {3,10}  {4}", "pieza", "1a vez", "en marcha", "de mas", "¿precalentar?"));
        Console.WriteLine("     " + new string('-', 84));

        int W = 1920, H = 1080;
        Mide("el buscador al abrirse", delegate { return MontaBuscador(W, H); }, Pinta("SearchOverlay", W, H));
        Mide("la lista de un disco", delegate { return MontaLista(980, 532); }, Pinta("SongListView", 980, 532));
        {
            Type TAV = Nes("AlbumView");
            MethodInfo mReb = (TAV != null) ? TAV.GetMethod("RebuildCubes", IF) : null;
            if (mReb != null)
            {
                object[] wh = new object[] { W, 700 };
                // UNA sola pasada, que es lo que hace un cuadro: la pared reparte el horneado de sus casillas
                // entre varios cuadros a proposito (cubesPending / BakeAhead), asi que forzar el horneado
                // entero de golpe mediria algo que la rocola no hace nunca.
                Mide("la pared de discos", delegate { return MontaPared(W, 700); },
                     delegate (object c) { mReb.Invoke(c, wh); });
            }
            PropertyInfo pCont = (TAV != null) ? TAV.GetProperty("ContourSpriteBmp", PUB) : null;
            if (pCont != null)
                Mide("el contorno de la seleccion", delegate { return MontaPared(W, 700); },
                     delegate (object c) { object o = pCont.GetValue(c, null); if (o == null) throw new Exception("sin contorno"); });
        }
        if (Nes("LetterBar") != null)
            Mide("la barra del abecedario", delegate { return MontaSimple("LetterBar", W, 40, 9f, new string[0], new object[0]); }, Pinta("LetterBar", W, 40));
        if (Nes("HeaderView") != null)
            Mide("el encabezado de seccion", delegate { return MontaSimple("HeaderView", 520, 34, 9f, new string[] { "Section", "Page", "Total" }, new object[] { "LETRA B", 2, 9 }); }, Pinta("HeaderView", 520, 34));
        if (Nes("PastillaView") != null)
            Mide("la pastilla de creditos", delegate { return MontaSimple("PastillaView", 220, 40, 9f, new string[] { "Etiqueta", "Valor" }, new object[] { "CREDITOS", "3" }); }, Pinta("PastillaView", 220, 40));

        Console.WriteLine("     " + new string('-', 84));
        Console.WriteLine("     un cuadro a 60 por segundo dura " + Cuadro.ToString("0.0") + " ms;  en la rocola del bar hay que multiplicar por 4 o 5");
        Console.WriteLine("     se traban la primera vez: " + cuantosPagan + " de " + tabla.Count);

        // ---------- 2) ABRIR UN DISCO TIRA SU PAGINA, PERO NO LO DEMAS ----------
        Console.WriteLine("\n  2) ABRIR UN DISCO TIRA SU PAGINA HORNEADA, PERO NO LO DEMAS\n");
        // Al abrir un disco, la lista OLVIDA la pagina que tenia (si no, venir de la pagina 3 de otro disco
        // haria "hojear" hasta la 1 del nuevo, que no significa nada). O sea que precalentar la lista NO le
        // ahorra hornear la pagina del disco que se abre... pero SI le ahorra todo lo demas, que es lo caro y
        // no se tira: las fuentes, las cifras de la cola, los circulos y las pastillas de genero.
        //
        // Y ojo: la primera pagina SI se anima. La lista se dibuja al abrir el disco, antes de que nadie
        // pueda pasar de pagina, asi que cuando llega el momento ya hay pagina horneada de donde salir.
        Type TLis = Nes("SongListView");
        MethodInfo mSlide = (TLis != null) ? TLis.GetMethod("StartSlide", PUB) : null;
        PropertyInfo pSliding = (TLis != null) ? TLis.GetProperty("Sliding", PUB) : null;
        MethodInfo mOlvida = (TLis != null) ? TLis.GetMethod("OlvidaPagina", PUB) : null;
        Check(mSlide != null && pSliding != null && mOlvida != null, "la lista de un disco sabe deslizarse y sabe olvidar su pagina");
        if (mSlide != null && pSliding != null && mOlvida != null)
        {
            Action<object> pintaL = Pinta("SongListView", 980, 532);
            // El camino de verdad: se abre el disco (olvida), se dibuja, y recien entonces se pasa de pagina.
            Control lista = MontaLista(980, 532);
            pintaL(lista);
            mOlvida.Invoke(lista, null);
            pintaL(lista);
            mSlide.Invoke(lista, new object[] { 1 });
            Check((bool)pSliding.GetValue(lista, null), "abriendo un disco y pasando de pagina, la primera SI se desliza");

            // Y lo que gana precalentar: una lista fria contra una ya calentada a la que se le acaba de tirar
            // la pagina (que es como llega la lista al abrir un disco).
            double fria = double.MaxValue, tibia = double.MaxValue;
            for (int i = 0; i < 7; i++)
            {
                Control f1 = MontaLista(980, 532);
                Stopwatch sw = Stopwatch.StartNew(); pintaL(f1); sw.Stop();
                if (sw.Elapsed.TotalMilliseconds < fria) fria = sw.Elapsed.TotalMilliseconds;
                Control f2 = MontaLista(980, 532);
                pintaL(f2);                              // esto es lo que hace el precalentado
                mOlvida.Invoke(f2, null);                // y esto lo que hace abrir un disco
                sw = Stopwatch.StartNew(); pintaL(f2); sw.Stop();
                if (sw.Elapsed.TotalMilliseconds < tibia) tibia = sw.Elapsed.TotalMilliseconds;
            }
            Console.WriteLine("     abrir un disco con la lista FRIA ......... " + fria.ToString("0.00") + " ms");
            Console.WriteLine("     abrir un disco con la lista PRECALENTADA . " + tibia.ToString("0.00") + " ms");
            Console.WriteLine("     (lo que se ahorra: " + (fria - tibia).ToString("0.00") + " ms;  en la del bar, 4 o 5 veces mas)");
            Check(tibia < fria, "precalentar la lista ahorra trabajo al abrir un disco, aunque su pagina se tire");
        }

        // ---------- 3) PRECALENTAR NO PUEDE CAMBIAR NADA DE LO QUE SE VE ----------
        Console.WriteLine("\n  3) Y PRECALENTAR NO CAMBIA NI UN PIXEL DE LO QUE SE VE\n");
        // Es la comprobacion que manda: dibujar antes "en seco" solo puede adelantar trabajo, nunca cambiar
        // el resultado. Se dibuja cada pieza recien hecha y ya precalentada, y se comparan.
        string[] tipos = { "SearchOverlay", "SongListView" };
        int[] anchos = { 1024, 980 };
        int[] altos = { 768, 532 };
        Func<int, Control>[] montas = {
            delegate (int i) { return MontaBuscador(1024, 768); },
            delegate (int i) { return MontaLista(980, 532); } };
        for (int i = 0; i < tipos.Length; i++)
        {
            Type T = Nes(tipos[i]);
            if (T == null) continue;
            MethodInfo m3 = T.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
            MethodInfo m1 = T.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            int w = anchos[i], h = altos[i];
            Bitmap frio = Lienzo(w, h), cal = Lienzo(w, h);
            Control a = montas[i](0), b = montas[i](0);
            using (Graphics g = Graphics.FromImage(frio))
                if (m3 != null) m3.Invoke(a, new object[] { g, new Rectangle(0, 0, w, h), false }); else m1.Invoke(a, new object[] { g });
            using (Bitmap seco = Lienzo(w, h))
            using (Graphics sg = Graphics.FromImage(seco))
                if (m3 != null) m3.Invoke(b, new object[] { sg, new Rectangle(0, 0, w, h), false }); else m1.Invoke(b, new object[] { sg });
            using (Graphics g = Graphics.FromImage(cal))
                if (m3 != null) m3.Invoke(b, new object[] { g, new Rectangle(0, 0, w, h), false }); else m1.Invoke(b, new object[] { g });
            int peor; int dif = Difiere(frio, cal, out peor);
            Console.WriteLine("     " + tipos[i].PadRight(16) + " pixeles distintos: " + dif.ToString("#,0") + "   el peor, " + peor + " niveles de 255");
            Check(dif == 0, "  " + tipos[i] + ": sale igual recien abierto que ya precalentado");
            frio.Dispose(); cal.Dispose();
        }

        // ---------- 4) Y LA ROCOLA LO HACE SOLA, EN SEGUNDO PLANO ----------
        Console.WriteLine("\n  4) Y LA ROCOLA PRECALIENTA SOLA, SIN QUITARLE LA MAQUINA A NADIE\n");
        MethodInfo mPre = TF.GetMethod("PrecalientaUnPoco", IF);
        Check(mPre != null, "la rocola tiene un precalentado que va de a poquito");
        MethodInfo mTick = TF.GetMethod("AnimTimer_Tick", IF);
        Check(mTick != null && Codigo.Llama(mTick, "PrecalientaUnPoco"), "y lo va gastando en el latido que ya corre (no en un hilo aparte)");
        if (mPre != null)
        {
            int lee, esc;
            Codigo.UsosDeCampo(mPre, "lastInputTick", out lee, out esc);
            Check(lee > 0, "se para si hay alguien tocando botones (mismo criterio que el precalentador de portadas)");
            // LO QUE TIENE QUE CALENTAR: las pantallas que al encender NO se ven y que se traban la primera
            // vez. La pared de discos y la barra de letras tambien cuestan su primera vez, pero es que ELLAS
            // SON la pantalla de arranque: ese cuadro se paga igual y adelantarlo no adelanta nada.
            List<string> falta = new List<string>();
            foreach (string campo in new string[] { "searchOverlay", "songList", "attractView" })
            {
                int l2, e2; Codigo.UsosDeCampo(mPre, campo, out l2, out e2);
                if (l2 == 0) falta.Add(campo);
            }
            if (falta.Count > 0) Console.WriteLine("     no los calienta: " + string.Join(", ", falta.ToArray()));
            Check(falta.Count == 0, "calienta las pantallas que al encender no se ven (buscador, lista de disco, reposo)");

            // Y LO QUE NO TIENE QUE CALENTAR: lo que se midio y no gana nada. Precalentar tambien gasta
            // memoria, y en la rocola del bar y en Puppy eso cuenta.
            List<string> demas = new List<string>();
            foreach (string campo in new string[] { "headerView", "pastCred", "pastCola" })
            {
                int l2, e2; Codigo.UsosDeCampo(mPre, campo, out l2, out e2);
                if (l2 > 0) demas.Add(campo);
            }
            if (demas.Count > 0) Console.WriteLine("     calienta de mas: " + string.Join(", ", demas.ToArray()));
            Check(demas.Count == 0, "y NO calienta lo que se midio y no gana nada (encabezado y pastillas: 0.06 ms)");

            // Y que se vuelva a calentar si cambia la resolucion: lo horneado esta al tamano de antes.
            MethodInfo mResize = TF.GetMethod("OnResize", IF);
            Check(mResize != null && Codigo.Llama(mResize, "VuelveAPrecalentar"),
                  "y vuelve a calentar si cambia la resolucion (lo horneado esta al tamano de antes)");
        }

        if (quePagan.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("     lo que se traba hoy la primera vez:");
            foreach (string q in quePagan) Console.WriteLine("       - " + q);
        }
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}

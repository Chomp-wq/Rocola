using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// ESCRIBIR EN EL BUSCADOR NO PUEDE COSTAR MEDIA PANTALLA.
//
// Al teclear, la rocola perdia imagenes por segundo, y cuanto mas grande el monitor peor. Medido: en una
// pantalla de 1920x1080, cada letra costaba 18.6 ms de dibujo, o sea que sola ya se comia media docena de
// cuadros. La causa eran dos cosas, y las dos estaban SOLO en el buscador:
//
//   1) El buscador se armaba SU PROPIO cielo estrellado y lo redibujaba entero -las seiscientas estrellas,
//      una a una, con su halo- en cada repintado. El resto del programa (el fondo, la rejilla de discos, el
//      interior del disco) hace tiempo que copia un cielo ya horneado y le pone encima solo lo que se mueve.
//      El buscador era el unico que se habia quedado atras. Ademas, al ser SU cielo y no el de todos, las
//      estrellas saltaban de sitio al abrirlo, y ocupaba otro juego entero de mapas de bits en memoria.
//
//   2) Cada letra mandaba repintar la pantalla ENTERA, incluido el teclado, que no cambia nunca mientras se
//      escribe. Ahora solo se marcan las dos zonas que si cambian: la caja de lo escrito y la columna de
//      resultados.
//
// Lo que se comprueba aqui: que el cielo del buscador es EL MISMO que el del resto y ya no se redibuja, que
// eso de verdad cuesta una fraccion de lo de antes, y -esto es el riesgo NUEVO- que no repintar el teclado
// no deja nada congelado: todo lo que cambia al escribir tiene que caer dentro de esas dos zonas.
class Escribe
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TSO, TItem, TAlb, TStar;
    static Bitmap laPortada;
    static Image Cov<T>(T a) { return laPortada; }
    static string Gen<T>(T it) { return "Balada"; }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static readonly string[] Nombres = {
        "Depeche Mode - Enjoy The Silence", "Jose Jose - El Triste", "Los Angeles Azules - Nunca Es Suficiente",
        "Metallica - Enter Sandman", "Juan Gabriel - Amor Eterno", "Bronco - Que No Quede Huella",
        "Rocio Durcal - Amor Eterno", "Mana - Rayando El Sol", "Amor a la Mexicana", "Amorcito Corazon" };

    static Control Monta(int w, int h, string consulta)
    {
        Control so = (Control)Activator.CreateInstance(TSO, true);
        so.SetBounds(0, 0, w, h);
        so.Font = new Font(Fam(), 12f, FontStyle.Bold);
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Grabaciones\Uno");
        TAlb.GetField("Name").SetValue(alb, "Un disco");
        TAlb.GetField("Artist").SetValue(alb, "Un artista");
        IList res = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem));
        for (int i = 0; i < 40; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            TItem.GetField("Kind").SetValue(it, i % 3);
            TItem.GetField("Display").SetValue(it, Nombres[i % Nombres.Length] + " " + i);
            TItem.GetField("Key").SetValue(it, (Nombres[i % Nombres.Length] + " " + i).ToLowerInvariant());
            TItem.GetField("Score").SetValue(it, 5000 - i);
            TItem.GetField("AlbumRef").SetValue(it, alb);
            res.Add(it);
        }
        TSO.GetField("Results").SetValue(so, res);
        TSO.GetField("Query").SetValue(so, consulta);
        TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
        TSO.GetField("FocusResults").SetValue(so, false);
        TSO.GetField("SelResult").SetValue(so, 0);
        Type tfc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        TSO.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(tfc,
            typeof(Escribe).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
        FieldInfo fg = TSO.GetField("GetGenero");
        if (fg != null) fg.SetValue(so, Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(TItem, typeof(string)),
            typeof(Escribe).GetMethod("Gen", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem)));
        return so;
    }

    static double Mide(int veces, Action a)
    {
        a(); a();
        double mejor = double.MaxValue;
        for (int t = 0; t < 5; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
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
            if (t.Name == "Starfield") TStar = t;
        }
        if (TSO == null || TStar == null) { Check(false, "estan el buscador y el campo de estrellas"); return 1; }
        laPortada = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(laPortada)) pg.Clear(Color.FromArgb(255, 30, 40, 90));
        MethodInfo mR = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });

        // ---------- 1) EL CIELO DEL BUSCADOR ES EL DE TODOS ----------
        Console.WriteLine("\n  1) EL BUSCADOR YA NO SE ARMA SU PROPIO CIELO\n");
        Check(TSO.GetField("stars", BindingFlags.Instance | BindingFlags.NonPublic) == null,
              "el buscador ya no lleva un campo de estrellas para el solo");
        FieldInfo fSky = TSO.GetField("Sky");
        Check(fSky != null && fSky.FieldType == TStar,
              "usa el campo COMPARTIDO con el fondo, la rejilla y el interior del disco");
        if (fSky == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        // Y de verdad lo USA: se le da un campo, se pinta el buscador, y despues ese campo tiene que haber
        // quedado con sus CAPAS YA HORNEADAS. Eso es la prueba de las dos cosas a la vez:
        //   - que el buscador pinta de ESE campo y no de uno escondido suyo (si no, seguiria sin capas);
        //   - y que pinta por el camino de copiar lo ya horneado, porque el camino viejo -dibujar estrella
        //     por estrella- no hornea ninguna capa, deja ese cajon vacio.
        int W = 1024, H = 768;
        object cielo = Activator.CreateInstance(TStar, true);
        FieldInfo fD = TStar.GetField("Density", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fD != null) fD.SetValue(cielo, 1f);
        FieldInfo fCapas = TStar.GetField("layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Control so = Monta(W, H, "amor");
        fSky.SetValue(so, cielo);
        Color arriba = (Color)TF.GetMethod("SkyColor", BindingFlags.Static | BindingFlags.NonPublic)
                                .Invoke(null, new object[] { Color.FromArgb(178, 152, 255) });
        Color abajo = (Color)TF.GetMethod("SkyColor2", BindingFlags.Static | BindingFlags.NonPublic)
                                .Invoke(null, new object[] { Color.FromArgb(178, 152, 255) });
        Check(fCapas != null && fCapas.GetValue(cielo) == null, "el campo empieza sin capas horneadas");
        using (Bitmap busc = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(busc))
            mR.Invoke(so, new object[] { g, new Rectangle(0, 0, W, H), false });
        Array capas = (fCapas != null) ? (Array)fCapas.GetValue(cielo) : null;
        Console.WriteLine("     capas de titileo horneadas en el campo compartido: " + ((capas == null) ? 0 : capas.Length));
        Check(ReferenceEquals(fSky.GetValue(so), cielo) && capas != null && capas.Length > 1,
              "y pinta copiando el cielo ya horneado, no dibujando las estrellas una a una");

        // ---------- 2) Y CUESTA UNA FRACCION DE LO DE ANTES ----------
        // No se mide en milisegundos sueltos -varian con la carga- sino contra lo que costaba antes: dibujar
        // el cielo en vivo, estrella por estrella, que es exactamente lo que hacia el buscador y lo que sigue
        // haciendo la pantalla de atraccion. Las dos medidas se toman en el mismo momento.
        Console.WriteLine("\n  2) PINTAR SU FONDO CUESTA UNA FRACCION DE LO DE ANTES\n");
        MethodInfo mVivo = TStar.GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo fLast = TStar.GetField("lastBuild", BindingFlags.Instance | BindingFlags.NonPublic);
        Rectangle todo = new Rectangle(0, 0, W, H);
        using (Bitmap dst = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(dst))
        {
            object[] argCpu = new object[] { g, todo, false };
            object[] argGpu = new object[] { g, todo, true };
            double tConFondo = Mide(10, delegate { mR.Invoke(so, argCpu); });
            double tSinFondo = Mide(10, delegate { mR.Invoke(so, argGpu); });
            double tFondo = tConFondo - tSinFondo;
            object[] argVivo = new object[] { g, W, H, arriba, abajo, Color.White, 0.0, false, 0f };
            double tVivo = Mide(4, delegate { if (fLast != null) fLast.SetValue(cielo, 0); mVivo.Invoke(cielo, argVivo); });
            Console.WriteLine("     el fondo del buscador ....... " + tFondo.ToString("0.00") + " ms");
            Console.WriteLine("     dibujarlo en vivo, como antes " + tVivo.ToString("0.00") + " ms");
            Check(tVivo > 0.05 && tFondo < tVivo * 0.45,
                  "pintar el fondo del buscador cuesta menos de la mitad que redibujarlo estrella por estrella");
        }

        // ---------- 3) EL RIESGO NUEVO: QUE NO SE QUEDE NADA CONGELADO ----------
        // Al escribir ya no se repinta el teclado. Eso solo vale si TODO lo que cambia al escribir cae dentro
        // de las dos zonas que si se repintan. Se dibujan dos cuadros con dos consultas distintas -sin cielo,
        // para que el titileo no ensucie la comparacion- y se miran los pixeles que cambian.
        Console.WriteLine("\n  3) NO REPINTAR EL TECLADO NO DEJA NADA CONGELADO\n");
        MethodInfo mZonas = TSO.GetMethod("ZonasAlEscribir", BindingFlags.Instance | BindingFlags.Public);
        Check(mZonas != null, "el programa dice EL MISMO cuales son las dos zonas que repinta al escribir");
        if (mZonas != null)
        {
            int[,] tam = { { 1024, 768 }, { 1366, 768 }, { 1920, 1080 }, { 800, 600 } };
            int fuera = 0, casos = 0;
            for (int t = 0; t < tam.GetLength(0); t++)
            {
                int w = tam[t, 0], h = tam[t, 1];
                Control s2 = Monta(w, h, "amo");     // sin cielo a proposito: Sky se queda en nulo
                object[] arg = new object[] { null, null };
                bool hay = (bool)mZonas.Invoke(s2, arg);
                if (!hay) continue;
                Rectangle zA = (Rectangle)arg[0], zD = (Rectangle)arg[1];
                Bitmap a1 = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                Bitmap a2 = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                Rectangle tt = new Rectangle(0, 0, w, h);
                using (Graphics g = Graphics.FromImage(a1))
                { g.Clear(Color.FromArgb(255, 8, 12, 36)); mR.Invoke(s2, new object[] { g, tt, false }); }
                TSO.GetField("Query").SetValue(s2, "amor");
                using (Graphics g = Graphics.FromImage(a2))
                { g.Clear(Color.FromArgb(255, 8, 12, 36)); mR.Invoke(s2, new object[] { g, tt, false }); }
                int malos = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (zA.Contains(x, y) || zD.Contains(x, y)) continue;
                        if (a1.GetPixel(x, y).ToArgb() != a2.GetPixel(x, y).ToArgb()) malos++;
                    }
                Console.WriteLine("     " + (w + "x" + h).PadRight(10) + " cambian fuera de las dos zonas: " + malos);
                if (malos > 0) fuera++;
                casos++;
                a1.Dispose(); a2.Dispose(); s2.Dispose();
            }
            Check(casos > 0 && fuera == 0, "al escribir una letra, todo lo que cambia cae dentro de las dos zonas");

            // Y que las zonas dejen fuera de verdad el teclado: si taparan la pantalla entera no se ahorraria nada.
            Control s3 = Monta(1920, 1080, "amor");
            object[] ar3 = new object[] { null, null };
            mZonas.Invoke(s3, ar3);
            Rectangle za = (Rectangle)ar3[0], zd = (Rectangle)ar3[1];
            int M = 30, kbW = (int)((1920 - 2 * M) * 0.52);
            Rectangle teclado = new Rectangle(M, M + 70 + 28 + 4, kbW, 1080 - (M + 70 + 28) - M - 8);
            Check(!za.IntersectsWith(teclado) && !zd.IntersectsWith(teclado),
                  "y el teclado se queda fuera: es lo que no hay que volver a copiar");
            s3.Dispose();
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

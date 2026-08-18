using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// QUE PASA AL PASAR RAPIDO DE PAGINA EN LA REJILLA DE DISCOS.
//
// Cada disco (su caratula ya escalada + su nombre ya maquetado) se hornea a un bitmap UNA vez y luego pasar
// de pagina es solo copiar. Eso es rapido... hasta que el cajon de bitmaps se llena. Aqui se simula a alguien
// hojeando pagina tras pagina y se mide lo que cuesta CADA pagina, para ver si alguna sale disparada.
class Pagina
{
    static Type TF, TAV, TAlb;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int COLS = 6, ROWS = 3, PAGESIZE = COLS * ROWS;

    static object NuevoAlbum(int n, Image cover)
    {
        object a = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(a, @"C:\Musica\Disco" + n);
        TAlb.GetField("Name").SetValue(a, "Disco numero " + n + " con nombre largo");
        TAlb.GetField("Artist").SetValue(a, "Artista " + (n % 40));
        TAlb.GetField("Letter").SetValue(a, (char)('A' + (n % 26)));
        TAlb.GetField("CoverPath").SetValue(a, @"C:\Musica\Disco" + n + @"\cover.jpg");
        return a;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "AlbumView") TAV = t;
            if (t.Name == "Album") TAlb = t;
        }
        Check(TAV != null && TAlb != null, "la rejilla de discos esta donde tiene que estar");
        if (TAV == null || TAlb == null) return 1;

        const int W = 1024, H = 460;                 // la rejilla a 1024x768, que es la pantalla del bar
        const int NPAG = 30;                         // treinta paginas: 540 discos
        // Una caratula de mentira, pero del tamano de una de verdad: lo caro de hornear es RE-ESCALARLA.
        Bitmap portada = new Bitmap(500, 500, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada))
        {
            pg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 60; i++) pg.FillEllipse(new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i)), i * 8, i * 7, 70, 70);
        }

        List<object> discos = new List<object>();
        for (int i = 0; i < NPAG * PAGESIZE; i++) discos.Add(NuevoAlbum(i, portada));

        Control av = (Control)Activator.CreateInstance(TAV, true);
        av.SetBounds(0, 0, W, H);
        av.Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold);
        TAV.GetField("GetCover").SetValue(av, MakeGetCover(portada));
        FieldInfo fPage = TAV.GetField("Page");
        Type tListAlb = typeof(List<>).MakeGenericType(TAlb);
        MethodInfo mRebuild = TAV.GetMethod("RebuildCubes", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mRebuild != null, "se puede pedir a la rejilla que componga una pagina");
        if (mRebuild == null) return 1;
        FieldInfo fPend = TAV.GetField("cubesPending", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fCache = TAV.GetField("cellCache", BindingFlags.Instance | BindingFlags.NonPublic);

        avG = av; fPageG = fPage; tListAlbG = tListAlb; mRebuildG = mRebuild; fPendG = fPend; discosG = discos;

        // CALENTAMIENTO: las primerisimas paginas salen lentas porque el compilador esta traduciendo el
        // codigo a maquina por primera vez, no porque hojear cueste. Se componen unas cuantas y se vacia el
        // cajon, para que la medida sea del trabajo de verdad y no del arranque.
        for (int p = 0; p < 4; p++) PonPagina(p, W, H);
        MethodInfo mReset = TAV.GetMethod("ResetCellCache", BindingFlags.Instance | BindingFlags.Public);
        if (mReset != null) mReset.Invoke(av, null);

        Console.WriteLine("\n  HOJEANDO " + NPAG + " PAGINAS SEGUIDAS (rejilla de " + COLS + "x" + ROWS + " a " + W + "x" + H + ")\n");
        Console.WriteLine("     pagina | ms      | discos en el cajon");
        Console.WriteLine("     " + new string('-', 46));
        double[] ms = new double[NPAG];
        int[] enCache = new int[NPAG];
        for (int p = 0; p < NPAG; p++)
        {
            ms[p] = PonPagina(p, W, H);
            enCache[p] = (fCache != null) ? ((ICollection)fCache.GetValue(av)).Count : -1;
        }
        // La primera pagina siempre cuesta (no hay nada horneado). Lo interesante es de la 2a en adelante.
        double baseMs = 0; int nb = 0;
        for (int p = 1; p < NPAG; p++) { baseMs += ms[p]; nb++; }
        baseMs /= Math.Max(nb, 1);
        double peor = 0; int peorP = -1;
        for (int p = 0; p < NPAG; p++)
        {
            bool pico = (p > 0 && ms[p] > baseMs * 2.0);
            if (p > 0 && ms[p] > peor) { peor = ms[p]; peorP = p; }
            Console.WriteLine(string.Format("     {0,6} | {1,7:0.00} | {2,4}   {3}", p + 1, ms[p], enCache[p], pico ? "<<< TIRON" : ""));
        }
        Console.WriteLine();
        Console.WriteLine("     media de la 2a pagina en adelante: " + baseMs.ToString("0.00") + " ms");
        Console.WriteLine("     la peor: pagina " + (peorP + 1) + " con " + peor.ToString("0.00") + " ms  ->  "
                          + (peor / Math.Max(baseMs, 0.001)).ToString("0.0") + " veces la media");

        // Volver ATRAS tiene que ser instantaneo: esas paginas ya estan horneadas.
        Console.WriteLine("\n  Y AHORA VOLVIENDO HACIA ATRAS (esas paginas ya se vieron)\n");
        double atras = 0; double peorAtras = 0;
        for (int p = NPAG - 2; p >= NPAG - 8 && p >= 0; p--)
        {
            double t = PonPagina(p, W, H);
            atras += t; if (t > peorAtras) peorAtras = t;
            Console.WriteLine(string.Format("     pagina {0,3}: {1,7:0.00} ms", p + 1, t));
        }
        // LO QUE DE VERDAD HACE EL CLIENTE: avanza y se vuelve. La pagina que acaba de dejar TIENE que
        // salir al momento; si el cajon de casillas se vacia de golpe, una de cada seis veces no sale.
        Console.WriteLine("\n  AVANZAR UNA PAGINA Y VOLVER A LA ANTERIOR, TREINTA VECES\n");
        double peorVuelta = 0; int peorVueltaP = -1, lentas = 0;
        for (int p = 1; p < NPAG; p++)
        {
            PonPagina(p, W, H);                    // avanza
            double t = PonPagina(p - 1, W, H);     // y se vuelve: esa pagina se acaba de ver
            if (t > peorVuelta) { peorVuelta = t; peorVueltaP = p; }
            if (t > 1.0) { lentas++; Console.WriteLine(string.Format("     volver a la pagina {0,3}: {1,7:0.00} ms   <<< HUBO QUE REHACERLA", p, t)); }
        }
        Console.WriteLine("     veces que volver atras NO fue instantaneo: " + lentas + " de " + (NPAG - 1));
        Console.WriteLine("     la peor: " + peorVuelta.ToString("0.00") + " ms (volviendo a la pagina " + peorVueltaP + ")");
        // Se admite UNA: la primerisima pagina se horneo al principio de la prueba y para cuando se vuelve a
        // ella ya han pasado treinta paginas, asi que es justo que se haya soltado.
        Check(lentas <= 1, "volver a la pagina que se acaba de dejar sale al momento (antes fallaba 1 de cada 6 veces)");

        Console.WriteLine();
        Check(peor < baseMs * 3.0, "hojeando hacia delante ninguna pagina cuesta el triple que las demas"
              + "   [la peor fue " + (peor / Math.Max(baseMs, 0.001)).ToString("0.0") + "x]");
        Check(peorAtras < baseMs * 3.0, "volviendo atras las paginas ya vistas siguen saliendo al momento"
              + "   [la peor fue " + (peorAtras / Math.Max(baseMs, 0.001)).ToString("0.0") + "x]");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }

    // Compone una pagina ENTERA (el programa reparte el horneado en varios repintados para no congelarse;
    // aqui se insiste hasta que la pagina esta completa, que es lo que ve el cliente).
    static Control avG; static FieldInfo fPageG, fPendG; static Type tListAlbG; static MethodInfo mRebuildG;
    static List<object> discosG;
    static double PonPagina(int p, int W, int H)
    {
        IList lista = (IList)Activator.CreateInstance(tListAlbG);
        for (int i = 0; i < PAGESIZE; i++) lista.Add(discosG[p * PAGESIZE + i]);
        fPageG.SetValue(avG, lista);
        Stopwatch sw = Stopwatch.StartNew();
        int vueltas = 0;
        do { mRebuildG.Invoke(avG, new object[] { W, H }); vueltas++; }
        while (fPendG != null && (bool)fPendG.GetValue(avG) && vueltas < 40);
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    // Delegado Func<Album,Image> construido a mano porque Album es un tipo privado del programa.
    static Image laPortada;
    static object MakeGetCover(Image im)
    {
        laPortada = im;
        Type tFunc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        MethodInfo mi = typeof(Pagina).GetMethod("Portada", BindingFlags.Static | BindingFlags.NonPublic);
        return Delegate.CreateDelegate(tFunc, mi.MakeGenericMethod(TAlb));
    }
    static Image Portada<T>(T a) { return laPortada; }
}

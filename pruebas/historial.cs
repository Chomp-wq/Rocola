using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// ¿CUANTAS PAGINAS DE LA PARED DE DISCOS COMPENSA GUARDAR EN MEMORIA?
//
// Pasar de pagina es rapido porque cada disco se hornea UNA vez -su caratula ya escalada y su nombre ya
// maquetado- y despues pasar de pagina es solo copiar. Ese horneado se guarda en un cajon acotado; cuando
// se llena, salen las mas viejas. La pregunta es que se siente cuando alguien VUELVE a una pagina que ya
// se salio del cajon, y a partir de que distancia.
//
// Aqui se hojea hacia delante y luego se vuelve a paginas cada vez mas lejanas, midiendo cada una. El
// escalon -si lo hay- dice exactamente cuanto historial merece la pena pagar.
class Historial
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TAV, TAlb;
    static Image portada;
    static Image Tapa<T>(T a) { return portada; }
    const int COLS = 6, ROWS = 3, PAGESIZE = COLS * ROWS;

    static Control NuevaRejilla(int w, int h)
    {
        Control av = (Control)Activator.CreateInstance(TAV, true);
        av.SetBounds(0, 0, w, h);
        av.Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold);
        Type tFunc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        TAV.GetField("GetCover").SetValue(av, Delegate.CreateDelegate(tFunc,
            typeof(Historial).GetMethod("Tapa", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
        return av;
    }

    static List<object> discos = new List<object>();
    static object NuevoAlbum(int n)
    {
        object a = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(a, @"C:\Musica\Disco" + n);
        TAlb.GetField("Name").SetValue(a, "Disco numero " + n + " con nombre largo");
        TAlb.GetField("Artist").SetValue(a, "Artista " + (n % 40));
        TAlb.GetField("Letter").SetValue(a, (char)('A' + (n % 26)));
        TAlb.GetField("CoverPath").SetValue(a, @"C:\Musica\Disco" + n + @"\cover.jpg");
        return a;
    }

    // Compone una pagina ENTERA (la rejilla reparte el horneado en varias pasadas; aqui se insiste hasta
    // que esta completa, que es lo que acaba viendo el cliente) y devuelve lo que costo.
    static double PonPagina(Control av, MethodInfo mRebuild, FieldInfo fPend, FieldInfo fPage, Type tLista, int p, int w, int h)
    {
        IList lista = (IList)Activator.CreateInstance(tLista);
        for (int i = 0; i < PAGESIZE; i++) lista.Add(discos[(p * PAGESIZE + i) % discos.Count]);
        fPage.SetValue(av, lista);
        Stopwatch sw = Stopwatch.StartNew();
        int vueltas = 0;
        do { mRebuild.Invoke(av, new object[] { w, h }); vueltas++; }
        while (fPend != null && (bool)fPend.GetValue(av) && vueltas < 40);
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
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
        if (TAV == null || TAlb == null) { Console.WriteLine("no encuentro la pared de discos"); return 1; }

        portada = new Bitmap(500, 500, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada))
        {
            pg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 60; i++) pg.FillEllipse(new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i)), i * 8, i * 7, 70, 70);
        }
        const int PAGS = 140;
        for (int i = 0; i < PAGS * PAGESIZE; i++) discos.Add(NuevoAlbum(i));

        Type tLista = typeof(List<>).MakeGenericType(TAlb);
        FieldInfo fPage = TAV.GetField("Page");
        MethodInfo mRebuild = TAV.GetMethod("RebuildCubes", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fPend = TAV.GetField("cubesPending", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fCache = TAV.GetField("cellCache", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fCap = TAV.GetField("capCeldas", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mRebuild == null) { Console.WriteLine("no se puede pedir a la rejilla que componga"); return 1; }

        int W = 1024, H = 460;
        if (argv.Length > 2) { W = int.Parse(argv[1]); H = int.Parse(argv[2]); }
        int tope = -1;   // se lee del propio objeto mas abajo, ya con su tamano puesto
        Console.WriteLine("\n  PARED DE DISCOS A " + W + "x" + H + "   (18 discos por pagina)");

        Control av = NuevaRejilla(W, H);
        // calentar: que el traductor a codigo maquina haga su trabajo antes de cronometrar
        for (int p = 0; p < 4; p++) PonPagina(av, mRebuild, fPend, fPage, tLista, p, W, H);
        if (fCap != null) tope = (int)fCap.GetValue(av);
        // CUANTAS PAGINAS HAY QUE METER para dejar el cajon SIN la que se va a medir. Tiene que ser mas que
        // su tope: cebando con menos, la pagina medida sobrevive de una repeticion a la siguiente y se acaba
        // midiendo el caso facil (salia todo instantaneo, incluso volviendo 130 paginas atras).
        int ceba = (tope > 0 ? tope / PAGESIZE : 6) + 4;
        Console.WriteLine("  el cajon guarda " + tope + " casillas = " + (tope / PAGESIZE)
                          + " paginas de historial   (se ceba con " + ceba + " para vaciarlo)\n");

        Console.WriteLine("  1) HOJEANDO HACIA DELANTE (cada pagina es nueva: hay que hornearla)\n");
        double sumaNueva = 0; int nNueva = 0, peorNueva = 0;
        double peor = 0;
        for (int p = 0; p < PAGS; p++)
        {
            double t = PonPagina(av, mRebuild, fPend, fPage, tLista, p, W, H);
            if (p < 4) continue;
            sumaNueva += t; nNueva++;
            if (t > peor) { peor = t; peorNueva = p; }
        }
        Console.WriteLine("     " + PAGS + " paginas nuevas seguidas: " + (sumaNueva / nNueva).ToString("0.00")
                          + " ms de media   (la peor " + peor.ToString("0.00") + " ms, en la " + peorNueva + ")");
        int enCajon = (fCache != null) ? ((ICollection)fCache.GetValue(av)).Count : -1;
        Console.WriteLine("     casillas en el cajon al acabar: " + enCajon);

        Console.WriteLine("\n  2) VOLVIENDO ATRAS: LO QUE CUESTA SEGUN LO LEJOS QUE SE VUELVA\n");
        Console.WriteLine("     {0,10}  {1,14}  {2}", "vuelve a", "cuesta", "");
        int[] atras = { 1, 2, 3, 4, 6, 8, 12, 20, 40, 80, 130 };
        double refCerca = 0;
        foreach (int d in atras)
        {
            int p = PAGS - 1 - d; if (p < 0) continue;
            // Se vuelve a la pagina p desde el final, tal cual haria alguien que retrocede de golpe.
            //
            // OJO CON MEDIR ESTO: si se repite la medida y se toma la mas rapida, a partir de la segunda
            // vuelta la pagina YA esta en el cajon y se mide el caso facil -salia todo a 0.22 ms, incluso
            // volviendo 130 paginas atras, que es imposible-. Por eso cada repeticion vuelve a llenar el
            // cajon con paginas nuevas de sobra (mas que su tope) y solo despues se cronometra UNA vez.
            double[] tomas = new double[3];
            for (int rep = 0; rep < 3; rep++)
            {
                for (int q = PAGS - ceba; q < PAGS; q++) PonPagina(av, mRebuild, fPend, fPage, tLista, q, W, H);
                tomas[rep] = PonPagina(av, mRebuild, fPend, fPage, tLista, p, W, H);
            }
            Array.Sort(tomas);
            double mejorT = tomas[1];   // la de en medio: ni la mas rapida ni un tropiezo de la maquina
            if (d == 1) refCerca = mejorT;
            string nota = "";
            if (refCerca > 0.001 && mejorT > refCerca * 2.5) nota = "  <- ya no estaba en el cajon";
            Console.WriteLine("     {0,7} atras  {1,11:0.00} ms {2}", d, mejorT, nota);
        }

        Console.WriteLine("\n  3) EL COMPROMISO: MAS HISTORIAL CONTRA HOJEAR HACIA DELANTE\n");
        // Un cajon mas grande hace instantaneo volver atras... pero tambien hay que podarlo, y podar es
        // tirar casillas -y liberar un mapa de bits cuesta-. Se barre el presupuesto para ver donde esta
        // el punto en el que se gana atras sin pagarlo delante, que es el caso comun.
        FieldInfo fMb = TF.GetField("cacheCeldasMb", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo fPag = TF.GetField("CeldasHistorialPaginas", BindingFlags.Static | BindingFlags.NonPublic);
        if (fMb == null || fPag == null) Console.WriteLine("     (no se puede ajustar el historial: se salta)");
        else
        {
            int pagAntes = (int)fPag.GetValue(null);
            Console.WriteLine("     {0,10}  {1,16}  {2,16}", "historial", "hojear adelante", "volver 40 atras");
            Console.WriteLine("     " + new string('-', 50));
            foreach (int pag in new int[] { 5, 12, 24, 50, 100 })
            {
                fMb.SetValue(null, 4096);      // techo alto: que mande el numero de paginas
                fPag.SetValue(null, pag);
                Control a2 = NuevaRejilla(W, H);
                for (int p = 0; p < 6; p++) PonPagina(a2, mRebuild, fPend, fPage, tLista, p, W, H);   // calentar y fijar el tope
                double suma = 0; int n2 = 0;
                for (int p = 0; p < PAGS; p++)
                { double t = PonPagina(a2, mRebuild, fPend, fPage, tLista, p, W, H); if (p >= 6) { suma += t; n2++; } }
                int cajon = (fCache != null) ? ((ICollection)fCache.GetValue(a2)).Count : -1;
                int ceba2 = ((fCap != null ? (int)fCap.GetValue(a2) : 108) / PAGESIZE) + 4;
                // y volver 40 atras, con el cajon como quedo (cebando de sobra: ver arriba)
                double[] tt = new double[3];
                for (int rep = 0; rep < 3; rep++)
                {
                    for (int q = PAGS - ceba2; q < PAGS; q++) PonPagina(a2, mRebuild, fPend, fPage, tLista, q, W, H);
                    tt[rep] = PonPagina(a2, mRebuild, fPend, fPage, tLista, PAGS - 41, W, H);
                }
                Array.Sort(tt);
                Console.WriteLine("     {0,7} pag  {1,13:0.00} ms  {2,13:0.00} ms{3}", cajon / PAGESIZE, suma / n2, tt[1],
                                  (cajon / PAGESIZE >= 40) ? "   <- volver 40 atras ya cae dentro" : "");
                a2.Dispose();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            }
            fPag.SetValue(null, pagAntes); fMb.SetValue(null, -1);
        }

        Console.WriteLine("\n  4) EL CAJON SE PORTA BIEN\n");
        // Lo que hay que exigirle: que respete su tope (si no, la memoria crece sin freno con el uso), que
        // guarde de verdad las doce paginas que se eligieron, y que volver a una que sigue dentro sea
        // muchisimo mas barato que volver a una que ya salio -que es TODO el motivo de que exista-.
        Control a3 = NuevaRejilla(W, H);
        for (int p = 0; p < 60; p++) PonPagina(a3, mRebuild, fPend, fPage, tLista, p, W, H);
        int lleno = (fCache != null) ? ((ICollection)fCache.GetValue(a3)).Count : -1;
        int topeReal = (fCap != null) ? (int)fCap.GetValue(a3) : -1;
        Console.WriteLine("     tras 60 paginas: " + lleno + " casillas en el cajon, con tope " + topeReal);
        Check(lleno <= topeReal + PAGESIZE, "el cajon respeta su tope por muchas paginas que pasen");
        Check(topeReal >= 6 * PAGESIZE, "y nunca guarda menos historial que antes (seis paginas)");

        // dentro contra fuera, con el cebado bien hecho
        int cebaR = topeReal / PAGESIZE + 4;
        for (int q = 60 - cebaR; q < 60; q++) PonPagina(a3, mRebuild, fPend, fPage, tLista, q, W, H);
        double dentro = PonPagina(a3, mRebuild, fPend, fPage, tLista, 59 - 2, W, H);
        for (int q = 60 - cebaR; q < 60; q++) PonPagina(a3, mRebuild, fPend, fPage, tLista, q, W, H);
        double fuera = PonPagina(a3, mRebuild, fPend, fPage, tLista, 0, W, H);
        Console.WriteLine("     volver a una que sigue dentro: " + dentro.ToString("0.00")
                          + " ms    a una que ya salio: " + fuera.ToString("0.00") + " ms");
        Check(dentro * 2.5 < fuera, "volver a una pagina guardada es MUCHO mas barato que rehacerla");
        a3.Dispose();

        Console.WriteLine("\n  5) LO QUE OCUPA\n");
        // El tamano de una casilla sale de la propia rejilla: ancho/6 por alto/3, en color de 4 bytes.
        long porCasilla = (long)(W / COLS) * (H / ROWS) * 4;
        Console.WriteLine("     una casilla a este tamano: " + (porCasilla / 1024) + " KB   ->  una pagina de 18: "
                          + (porCasilla * PAGESIZE / 1024 / 1024.0).ToString("0.0") + " MB");
        Console.WriteLine();
        Console.WriteLine("     {0,10}  {1,12}", "paginas", "memoria");
        foreach (int pg in new int[] { 6, 12, 25, 50, 100 })
            Console.WriteLine("     {0,10}  {1,9:0} MB {2}", pg, porCasilla * PAGESIZE * pg / 1024.0 / 1024.0,
                              pg == (tope > 0 ? tope / PAGESIZE : 6) ? "  <- lo de ahora" : "");
        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

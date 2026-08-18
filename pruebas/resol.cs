using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// CAMBIAR DE RESOLUCION CON LA ROCOLA ABIERTA.
//
// EL SINTOMA: si se cambia la resolucion de Windows con la rocola encendida, pasar de pagina se vuelve
// lentisimo -llega a congelarse si se deja el boton pulsado-. Arrancando la rocola YA en esa resolucion no
// pasa nada. O sea: no es que la resolucion nueva sea mas cara, es que algo queda mal despues del cambio.
//
// Aqui se reproduce: se hojea a un tamano, se cambia de tamano, se vuelve a hojear, y se compara contra una
// rejilla RECIEN CREADA a ese mismo tamano (que es lo que pasa al reiniciar). Si el cambio de tamano dejara
// algo mal, la primera saldria mucho mas lenta que la segunda.
class Resol
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
            typeof(Resol).GetMethod("Tapa", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
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
    // que esta completa, que es lo que acaba viendo el cliente).
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

    // Hojea 'n' paginas seguidas y devuelve el coste MEDIO (sin contar la primera, que siempre cuesta).
    static double Hojea(Control av, MethodInfo mRebuild, FieldInfo fPend, FieldInfo fPage, Type tLista,
                        int n, int w, int h, out double peor)
    {
        peor = 0; double suma = 0; int cuenta = 0;
        for (int p = 0; p < n; p++)
        {
            double t = PonPagina(av, mRebuild, fPend, fPage, tLista, p, w, h);
            if (p == 0) continue;
            suma += t; cuenta++;
            if (t > peor) peor = t;
        }
        return suma / Math.Max(1, cuenta);
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
        Check(TAV != null && TAlb != null, "la pared de discos esta donde tiene que estar");
        if (TAV == null || TAlb == null) return 1;

        portada = new Bitmap(500, 500, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada))
        {
            pg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 60; i++) pg.FillEllipse(new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i)), i * 8, i * 7, 70, 70);
        }
        for (int i = 0; i < 40 * PAGESIZE; i++) discos.Add(NuevoAlbum(i));

        Type tLista = typeof(List<>).MakeGenericType(TAlb);
        FieldInfo fPage = TAV.GetField("Page");
        MethodInfo mRebuild = TAV.GetMethod("RebuildCubes", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fPend = TAV.GetField("cubesPending", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fCache = TAV.GetField("cellCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mRebuild != null, "se le puede pedir a la rejilla que componga una pagina");
        if (mRebuild == null) return 1;

        const int W1 = 1024, H1 = 460;    // la pantalla del bar
        const int W2 = 1600, H2 = 760;    // y una mas grande, como al cambiar de resolucion

        Console.WriteLine("\n  1) HOJEAR AL TAMANO DE SIEMPRE\n");
        Control av = NuevaRejilla(W1, H1);
        double peor1;
        Hojea(av, mRebuild, fPend, fPage, tLista, 4, W1, H1, out peor1);        // calentar (traducir a maquina)
        double ms1 = Hojea(av, mRebuild, fPend, fPage, tLista, 8, W1, H1, out peor1);
        Console.WriteLine("     " + W1 + "x" + H1 + ":  " + ms1.ToString("0.00") + " ms por pagina  (la peor " + peor1.ToString("0.00") + ")");

        Console.WriteLine("\n  2) SE CAMBIA LA RESOLUCION CON LA ROCOLA ABIERTA\n");
        av.SetBounds(0, 0, W2, H2);
        double peor2;
        // Al cambiar de tamano el cajon se vacia (las celdas horneadas ya no encajan) y las primeras paginas
        // se rehornean: eso es lo correcto y cuesta una vez. Se le da el MISMO calentamiento que a la recien
        // creada de mas abajo, para comparar lo mismo con lo mismo y no el arranque de una contra el ritmo de
        // crucero de la otra.
        Hojea(av, mRebuild, fPend, fPage, tLista, 4, W2, H2, out peor2);
        double ms2 = Hojea(av, mRebuild, fPend, fPage, tLista, 8, W2, H2, out peor2);
        int enCache2 = (fCache != null) ? ((ICollection)fCache.GetValue(av)).Count : -1;
        Console.WriteLine("     " + W2 + "x" + H2 + " tras el cambio:  " + ms2.ToString("0.00") + " ms por pagina  (la peor "
                          + peor2.ToString("0.00") + ", " + enCache2 + " celdas en el cajon)");

        Console.WriteLine("\n  3) Y COMO SERIA REINICIANDO YA EN ESA RESOLUCION\n");
        Control av2 = NuevaRejilla(W2, H2);
        double peor3;
        Hojea(av2, mRebuild, fPend, fPage, tLista, 4, W2, H2, out peor3);       // calentar
        double ms3 = Hojea(av2, mRebuild, fPend, fPage, tLista, 8, W2, H2, out peor3);
        int enCache3 = (fCache != null) ? ((ICollection)fCache.GetValue(av2)).Count : -1;
        Console.WriteLine("     " + W2 + "x" + H2 + " recien arrancada: " + ms3.ToString("0.00") + " ms por pagina  (la peor "
                          + peor3.ToString("0.00") + ", " + enCache3 + " celdas en el cajon)");

        Console.WriteLine();
        Console.WriteLine("     despues de cambiar vs recien arrancada:  " + (ms2 / Math.Max(ms3, 0.001)).ToString("0.00") + " veces");
        Check(ms2 < ms3 * 1.35, "hojear despues de cambiar de resolucion cuesta lo mismo que recien arrancada");
        Check(enCache2 <= enCache3 + PAGESIZE, "y el cajon de celdas no se queda con las del tamano viejo");

        Console.WriteLine("\n  4) NADA SE QUEDA COLGADO AL CAMBIAR DE TAMANO\n");
        // Ir y venir entre dos tamanos muchas veces: si algo no se soltara, la memoria subiria sin parar.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long m0 = GC.GetTotalMemory(true);
        for (int k = 0; k < 12; k++)
        {
            int w = (k % 2 == 0) ? W2 : W1, h = (k % 2 == 0) ? H2 : H1;
            av.SetBounds(0, 0, w, h);
            Hojea(av, mRebuild, fPend, fPage, tLista, 3, w, h, out peor2);
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long m1 = GC.GetTotalMemory(true);
        Console.WriteLine("     doce cambios de resolucion hojeando: la memoria paso de "
                          + (m0 / 1024 / 1024) + " MB a " + (m1 / 1024 / 1024) + " MB");
        Check(m1 < m0 + 40L * 1024 * 1024, "doce cambios de resolucion no dejan memoria colgada");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

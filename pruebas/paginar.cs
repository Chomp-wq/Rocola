using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// PASAR PAGINAS RAPIDO, que es cuando mas se nota el bajon.
//
// LO QUE SE MIDIO ANTES DE TOCAR NADA, porque la primera sospecha era otra: hornear las 18 casillas de una
// pagina cuesta 2.9 ms a 1280x1024, y bajarlas a la mitad ahorra 1.2. O sea que el DIBUJO no era el problema
// -y encima ya se reparte con un tope de 6 ms por cuadro-. Lo caro esta en otros dos sitios:
//
//   1) CADA CASILLA NUEVA SE SUBE A LA TARJETA como una textura de 256x256, unos 200 KB, dieciocho por
//      pagina. A media resolucion la textura baja a 128x128: LA CUARTA PARTE de los bytes. Ahi si paga.
//
//   2) Y HAY TRABAJO QUE SE HACE DOS VECES. Cuando se pasa a una pagina cuyas portadas aun no estan
//      cargadas, la casilla se hornea y se sube con el disco vacio; cuando la portada llega, se vuelve a
//      hornear y a subir entera. Pasar rapido es justo el caso en que las portadas NUNCA estan listas, asi
//      que el momento mas apretado era el que hacia el doble de trabajo.
//
// LAS DOS COSAS SOLO OCURREN MIENTRAS LA PAGINA SE ESTA MOVIENDO, que es cuando no se distingue el detalle.
// En cuanto para, la casilla vuelve a su resolucion entera y con su portada.
//
// Y LA TRAMPA QUE VIGILA ESTA BATERIA. Para hacer una casilla mas chica lo que sale solo es pasarle las
// medidas partidas por dos. Eso esta MAL: el disco menguaria, pero la letra del rotulo tiene tamaño FIJO, no
// depende de las medidas, asi que el nombre saldria enorme y cortado. Hay que encoger el DIBUJO entero. Aqui
// se comprueba comparando las dos maneras contra la casilla de verdad: la buena se parece, la ingenua no.
class Paginar
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PUB = BindingFlags.Instance | BindingFlags.Public;

    static Type TF, TAV, TAlb, TStar, TCel;
    static Image portada;
    static bool hayPortada = true;
    static Image Tapa<T>(T a) { return hayPortada ? portada : null; }

    static double Med(int veces, Action a)
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

    static Control MontaPared(int w, int h)
    {
        Control av = (Control)Activator.CreateInstance(TAV, true);
        av.SetBounds(0, 0, w, h);
        av.Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold);
        Type tf = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        TAV.GetField("GetCover").SetValue(av, Delegate.CreateDelegate(tf,
            typeof(Paginar).GetMethod("Tapa", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
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
        TAV.GetField("Page").SetValue(av, pag);
        TAV.GetField("Sky").SetValue(av, Activator.CreateInstance(TStar, true));
        TAV.GetField("Selected").SetValue(av, 7);
        return av;
    }

    // Cuanto se diferencian dos mapas del MISMO tamano, en promedio por canal (0 = iguales).
    static double Difiere(Bitmap a, Bitmap b)
    {
        long suma = 0; int n = 0;
        for (int y = 0; y < a.Height; y += 2)
            for (int x = 0; x < a.Width; x += 2)
            {
                Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                suma += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B) + Math.Abs(p.A - q.A);
                n += 4;
            }
        return (n == 0) ? 0 : (double)suma / n;
    }

    // Estira un mapa chico al tamano del grande, como hace la tarjeta grafica con la textura.
    static Bitmap Estira(Bitmap chico, int w, int h)
    {
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(chico, new Rectangle(0, 0, w, h), new Rectangle(0, 0, chico.Width, chico.Height), GraphicsUnit.Pixel);
        }
        return b;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes()) if (t.Name == "Form1") TF = t;
        if (TF == null) { Console.WriteLine("  FALLO  no se encontro Form1"); return 1; }
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "AlbumView") TAV = t;
            if (t.Name == "Album") TAlb = t;
            if (t.Name == "Starfield") TStar = t;
            if (t.Name == "GpuGridCell") TCel = t;
        }
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuGridCell") TCel = t;
        if (TAV == null) { Console.WriteLine("  FALLO  no se encontro la pared de discos"); return 1; }

        Bitmap p = new Bitmap(256, 256, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(p))
        { g.Clear(Color.FromArgb(255, 40, 90, 160)); g.FillEllipse(Brushes.Orange, 30, 30, 190, 190); }
        portada = p;

        int W = 1280, H = 1024;
        Control av = MontaPared(W, H);
        int cw = W / 6, ch = H / 3;
        MethodInfo mMet = TAV.GetMethod("CellMetrics", BindingFlags.Static | BindingFlags.NonPublic);
        object[] arg = { cw, ch, 0, 0, 0 };
        mMet.Invoke(null, arg);
        int side = (int)arg[2], labelH = (int)arg[3], gap = (int)arg[4];
        object album = ((IList)TAV.GetField("Page").GetValue(av))[0];

        Console.WriteLine("\n  1) SE PUEDE HORNEAR UNA CASILLA MAS CHICA\n");
        MethodInfo mBakeEsc = null;
        foreach (MethodInfo m in TAV.GetMethods(IF | BindingFlags.DeclaredOnly))
            if (m.Name == "BakeCell" && m.GetParameters().Length == 7) mBakeEsc = m;
        Check(mBakeEsc != null, "BakeCell sabe hornear a escala (siete argumentos, el ultimo la escala)");
        MethodInfo mBake = null;
        foreach (MethodInfo m in TAV.GetMethods(IF | BindingFlags.DeclaredOnly))
            if (m.Name == "BakeCell" && m.GetParameters().Length == 6) mBake = m;
        if (mBakeEsc == null || mBake == null)
        {
            Check(false, "sin eso no se puede comprobar nada mas");
            Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
            return fallos;
        }

        Bitmap entera = (Bitmap)mBake.Invoke(av, new object[] { album, cw, side, labelH, gap, portada });
        Bitmap chica = (Bitmap)mBakeEsc.Invoke(av, new object[] { album, cw, side, labelH, gap, portada, 0.5f });
        Check(chica.Width < entera.Width && chica.Height < entera.Height,
              "la chica mide menos (" + chica.Width + "x" + chica.Height + " contra " + entera.Width + "x" + entera.Height + ")");

        Console.WriteLine("\n  2) Y SE PARECE A LA DE VERDAD: SE ENCOGE EL DIBUJO, NO LAS MEDIDAS\n");
        // La manera INGENUA -pasarle las medidas partidas por dos- encoge el disco pero NO la letra del
        // rotulo, que tiene tamano fijo: el nombre sale enorme y cortado. Se comparan las dos contra la
        // casilla de verdad, estiradas como las estiraria la tarjeta. La buena tiene que parecerse
        // CLARAMENTE mas; si algun dia alguien "simplifica" esto pasando medidas, este numero lo delata.
        Bitmap ingenua = (Bitmap)mBake.Invoke(av, new object[] { album, cw / 2, side / 2, labelH / 2, gap / 2, portada });
        using (Bitmap eChica = Estira(chica, entera.Width, entera.Height))
        using (Bitmap eIngenua = Estira(ingenua, entera.Width, entera.Height))
        {
            double dBuena = Difiere(entera, eChica), dMala = Difiere(entera, eIngenua);
            Console.WriteLine(string.Format("     encogiendo el dibujo: {0:0.0} de diferencia;  pasando medidas: {1:0.0}", dBuena, dMala));
            Check(dBuena * 1.5 < dMala, "encogiendo el DIBUJO se parece bastante mas que pasando las medidas");
        }
        ingenua.Dispose();

        Console.WriteLine("\n  3) LO QUE AHORRA\n");
        double msE = Med(30, delegate { ((Bitmap)mBake.Invoke(av, new object[] { album, cw, side, labelH, gap, portada })).Dispose(); });
        double msC = Med(30, delegate { ((Bitmap)mBakeEsc.Invoke(av, new object[] { album, cw, side, labelH, gap, portada, 0.5f })).Dispose(); });
        long bytesE = (long)entera.Width * entera.Height * 4, bytesC = (long)chica.Width * chica.Height * 4;
        Console.WriteLine(string.Format("     dibujo:   {0:0.00} ms entera, {1:0.00} ms chica   ({2:0.0} contra {3:0.0} ms las 18)", msE, msC, msE * 18, msC * 18));
        Console.WriteLine(string.Format("     textura:  {0} KB entera, {1} KB chica   ({2} contra {3} KB las 18)",
                          bytesE / 1024, bytesC / 1024, bytesE * 18 / 1024, bytesC * 18 / 1024));
        // Lo que de verdad importa: los bytes que van a la tarjeta. El dibujo ya se sabia que era poco.
        Check(bytesC * 3 <= bytesE, "a la tarjeta van como mucho un TERCIO de los bytes (medido: la cuarta parte)");
        entera.Dispose(); chica.Dispose();

        Console.WriteLine("\n  4) MIENTRAS SE DESLIZA, LAS CASILLAS VAN CHICAS\n");
        MethodInfo mGrid = TAV.GetMethod("BuildGpuGrid", PUB | BindingFlags.DeclaredOnly);
        MethodInfo mSlide = TAV.GetMethod("StartSlide", PUB | BindingFlags.DeclaredOnly);
        if (mGrid == null || TCel == null)
        {
            Console.WriteLine("     (ejecutable sin tarjeta grafica: esta parte no aplica)");
            Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
            return fallos;
        }
        Array buf = Array.CreateInstance(TCel, 32);
        FieldInfo fBmp = TCel.GetField("Bmp"), fW = TCel.GetField("W");
        FieldInfo fSlideTick = TAV.GetField("slideTick", IF);

        // Quieta: casillas a resolucion entera.
        fSlideTick.SetValue(av, -1);
        hayPortada = true;
        mGrid.Invoke(av, new object[] { buf });
        mGrid.Invoke(av, new object[] { buf });   // segunda vuelta: ya con presupuesto para todas
        mGrid.Invoke(av, new object[] { buf });
        Bitmap q0 = (Bitmap)fBmp.GetValue(buf.GetValue(0));
        float anchoQuad = Convert.ToSingle(fW.GetValue(buf.GetValue(0)));
        Check(q0 != null && q0.Width > cw * 0.9, "quieta, la casilla va a resolucion entera (" + (q0 == null ? 0 : q0.Width) + " px)");

        // Deslizando: mas chicas, PERO el hueco en pantalla es el mismo (no se mueve nada de sitio).
        mSlide.Invoke(av, new object[] { 1, W });
        for (int k = 0; k < 4; k++) mGrid.Invoke(av, new object[] { buf });
        Bitmap q1 = (Bitmap)fBmp.GetValue(buf.GetValue(0));
        float anchoQuad2 = Convert.ToSingle(fW.GetValue(buf.GetValue(0)));
        Check(q1 != null && q1.Width < q0.Width, "deslizando, la casilla va mas chica (" + (q1 == null ? 0 : q1.Width) + " px)");
        Check(anchoQuad == anchoQuad2, "y ocupa EXACTAMENTE el mismo hueco en pantalla: no se mueve nada de sitio");

        Console.WriteLine("\n  5) Y NO SE REHACE EL TRABAJO DOS VECES\n");
        // Deslizando con las portadas SIN cargar -que es lo que pasa pasando rapido-, y de pronto llega la
        // portada. Antes eso volvia a hornear y a subir la casilla entera en el peor momento. Ahora se
        // aplaza: mientras la pagina se mueve, se sigue viendo la que hay.
        MethodInfo mReset = TAV.GetMethod("ResetCellCache", PUB | BindingFlags.DeclaredOnly);
        mReset.Invoke(av, null);
        fSlideTick.SetValue(av, -1);
        hayPortada = false;
        mSlide.Invoke(av, new object[] { 1, W });
        for (int k = 0; k < 4; k++) mGrid.Invoke(av, new object[] { buf });
        Bitmap sinTapa = (Bitmap)fBmp.GetValue(buf.GetValue(0));
        Check(sinTapa != null, "deslizando sin portadas, la casilla se ve igual (con el disco vacio)");

        hayPortada = true;   // la portada acaba de llegar, y la pagina SIGUE moviendose
        for (int k = 0; k < 3; k++) mGrid.Invoke(av, new object[] { buf });
        Bitmap trasTapa = (Bitmap)fBmp.GetValue(buf.GetValue(0));
        Check(ReferenceEquals(sinTapa, trasTapa),
              "y aunque llegue la portada NO se rehace mientras se mueve: el momento apretado no hace el doble de trabajo");

        // En cuanto para, si que se rehace: la portada tiene que acabar viendose.
        fSlideTick.SetValue(av, -1);
        for (int k = 0; k < 4; k++) mGrid.Invoke(av, new object[] { buf });
        Bitmap parada = (Bitmap)fBmp.GetValue(buf.GetValue(0));
        Check(!ReferenceEquals(sinTapa, parada), "y en cuanto para, se rehace con su portada y a resolucion entera");
        Check(parada != null && parada.Width > cw * 0.9, "a resolucion entera de verdad (" + (parada == null ? 0 : parada.Width) + " px)");

        Console.WriteLine("\n  6) NUNCA UN HUECO\n");
        // Si no queda presupuesto y no hay casilla de esta resolucion, se echa mano de la OTRA antes que
        // dejar el sitio vacio: la tarjeta la estira y se ve un pelo blanda un cuadro, que es infinitamente
        // mejor que un agujero en la pared.
        mSlide.Invoke(av, new object[] { -1, W });
        mGrid.Invoke(av, new object[] { buf });   // UNA sola vuelta: el presupuesto no da para las 18
        int vacias = 0;
        for (int i = 0; i < 18; i++) if (fBmp.GetValue(buf.GetValue(i)) == null) vacias++;
        Check(vacias == 0, "en el primer cuadro de un deslizamiento no queda ni una casilla vacia (quedaron " + vacias + ")");

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}

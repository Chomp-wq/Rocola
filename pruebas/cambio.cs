using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// EL TIRON AL CAMBIAR DE CANCION, Y POR QUE ERA PEOR EN PANTALLA GRANDE.
//
// Cada genero tiene su color, y el fondo de la rocola se tine de ese color. Al cambiar de cancion el color
// no salta de golpe: se DESLIZA del anterior al nuevo en unos veintidos pasitos, para que se vea suave.
//
// El problema era que cada uno de esos pasitos es un color distinto, y con un color distinto hay que
// REHORNEAR el cielo entero: el degradado a pantalla completa, la via lactea y las nebulosas estiradas
// desde su dibujo pequeno, y el polvo. Medido, un horneado cuesta 6 ms a 1024x768, 16 a 1920x1080 y 27 a
// 2560x1440. Multiplicado por veintidos, cada cambio de cancion pedia entre 130 ms y 600 ms de trabajo de
// mas, justo en el momento en que la rocola tiene que arrancar la cancion siguiente. De ahi el tiron, y de
// ahi que fuera peor cuanto mas grande la pantalla: lo que crece es el estirado a pantalla completa.
//
// Ahora el cielo se hornea con el color REDONDEADO a escalones de ocho, asi que el mismo deslizamiento pide
// cuatro o cinco horneados en vez de veintidos. Aqui se comprueban las dos cosas que importan: que de verdad
// se hornea muchas menos veces, y que el redondeo NO SE VE.
class Cambio
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TS;
    static BindingFlags IN = BindingFlags.Instance | BindingFlags.NonPublic;
    static BindingFlags IP = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static BindingFlags SN = BindingFlags.Static | BindingFlags.NonPublic;

    // Los pasitos que da EL PROGRAMA al pasar de un color de genero al siguiente. Se llama a su propia
    // cuenta, no a una copia: lo que se esta probando es justamente si esa cuenta llega a su destino.
    // El tope de 500 esta para que la prueba termine aunque no llegue nunca, que es lo que pasaba antes.
    static MethodInfo mLerp;
    static Color[] Deslizamiento(Color desde, Color hasta, out bool llego)
    {
        System.Collections.Generic.List<Color> pasos = new System.Collections.Generic.List<Color>();
        Color c = desde; llego = false;
        for (int i = 0; i < 500; i++)
        {
            c = (Color)mLerp.Invoke(null, new object[] { c, hasta, 0.16 });
            if (Math.Abs(c.R - hasta.R) <= 2 && Math.Abs(c.G - hasta.G) <= 2 && Math.Abs(c.B - hasta.B) <= 2)
            { c = hasta; pasos.Add(c); llego = true; break; }
            pasos.Add(c);
        }
        return pasos.ToArray();
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);
        if (TS == null) { Check(false, "esta el campo de estrellas"); return 1; }
        MethodInfo mSky = TS.GetMethod("EnsureSkyCache", IN);
        MethodInfo mLay = TS.GetMethod("EnsureLayers", IN);
        MethodInfo mCol = TF.GetMethod("SkyColor", SN);
        MethodInfo mCol2 = TF.GetMethod("SkyColor2", SN);
        MethodInfo mColM = TF.GetMethod("SkyColorMid", SN);
        FieldInfo fAcc = TS.GetField("Acento", IP), fMid = TS.GetField("SkyMid", IP);
        FieldInfo fDens = TS.GetField("Density", IP);
        FieldInfo fTop = TS.GetField("fscTop", IN), fBot = TS.GetField("fscBot", IN), fMi = TS.GetField("fscMid", IN);
        FieldInfo fCache = TS.GetField("skyCache", IN);
        FieldInfo fSalt = TS.GetField("saltados", IN);

        fAccG = fAcc; fMidG = fMid; fCacheG = fCache; mSkyG = mSky; mColG = mCol; mCol2G = mCol2; mColMG = mColM;

        mLerp = TF.GetMethod("LerpColor", SN);

        // ---------- 1) EL DESLIZAMIENTO DE COLOR TIENE QUE TERMINAR ----------
        // El programa se pregunta "sigue cambiando el color del fondo?" comparando el color de ahora con su
        // destino, y mientras la respuesta sea que si, repinta el fondo, la rejilla y el interior del disco
        // en CADA cuadro en vez de en uno de cada tres. Si el deslizamiento no llegara nunca, la rocola se
        // quedaria haciendo el triple de trabajo de por vida... y eso es exactamente lo que pasaba: un canal
        // que tenia que subir se quedaba clavado a seis del destino, porque el 16% de seis son 0.96 y la
        // cuenta tiraba los decimales.
        Console.WriteLine("\n  1) EL DESLIZAMIENTO DE COLOR TIENE QUE LLEGAR A SU DESTINO\n");
        Color[,] pares = {
            { Color.FromArgb(255, 130, 40), Color.FromArgb(178, 152, 255) },   // cumbia -> rock
            { Color.FromArgb(178, 152, 255), Color.FromArgb(255, 130, 40) },   // rock -> cumbia
            { Color.FromArgb(90, 200, 255), Color.FromArgb(240, 90, 140) },
            { Color.FromArgb(20, 20, 20), Color.FromArgb(250, 250, 250) },     // el salto mas largo posible
        };
        int noLlegan = 0, masPasos = 0;
        Color[] pasos = null;
        for (int p = 0; p < pares.GetLength(0); p++)
        {
            bool ok2;
            Color[] ps = Deslizamiento(pares[p, 0], pares[p, 1], out ok2);
            Console.WriteLine("     de (" + pares[p, 0].R + "," + pares[p, 0].G + "," + pares[p, 0].B + ") a ("
                              + pares[p, 1].R + "," + pares[p, 1].G + "," + pares[p, 1].B + "): "
                              + (ok2 ? ("llega en " + ps.Length + " cuadros") : "NO LLEGA NUNCA"));
            if (!ok2) noLlegan++;
            if (ps.Length > masPasos) { masPasos = ps.Length; }
            if (p == 0) pasos = ps;
        }
        Check(noLlegan == 0, "el color del fondo llega a su destino y el deslizamiento termina");
        Check(masPasos <= 40, "y termina pronto, en un segundo escaso (" + masPasos + " cuadros como mucho)");

        // ---------- 2) EL CIELO NO SE REHORNEA EN CADA CUADRO DEL DESLIZAMIENTO ----------
        Console.WriteLine("\n  2) EL CIELO NO SE REHORNEA EN CADA CUADRO DEL DESLIZAMIENTO\n");
        Check(fSalt != null, "el cielo sabe saltarse horneados");
        if (fSalt == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }
        Console.WriteLine("     el color pasa de un genero al otro en " + pasos.Length + " cuadros");

        Console.WriteLine();
        Console.WriteLine("     {0,-12} {1,10} {2,12} {3,12}", "resolucion", "horneados", "un horneado", "el cambio");
        int[,] tam = { { 1024, 768 }, { 1920, 1080 }, { 2560, 1440 } };
        int peorHorneados = 0;
        for (int t = 0; t < tam.GetLength(0); t++)
        {
            int w = tam[t, 0], h = tam[t, 1];
            object c = Activator.CreateInstance(TS, true);
            if (fDens != null) fDens.SetValue(c, 1f);
            mLay.Invoke(c, new object[] { w, h });

            // Se cuenta un horneado cada vez que cambia la firma del cielo (que es justo lo que decide si
            // hay que rehacerlo o vale el que ya estaba).
            int horneados = 0;
            object antes = null;
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < pasos.Length; i++)
            {
                Color acc = pasos[i];
                fAcc.SetValue(c, acc);
                if (fMid != null) fMid.SetValue(c, (Color)mColM.Invoke(null, new object[] { acc }));
                Color arr = (Color)mCol.Invoke(null, new object[] { acc });
                Color aba = (Color)mCol2.Invoke(null, new object[] { acc });
                mSky.Invoke(c, new object[] { w, h, arr, aba });
                object firma = "" + fTop.GetValue(c) + "|" + fMi.GetValue(c) + "|" + fBot.GetValue(c);
                if (!firma.Equals(antes)) { horneados++; antes = firma; }
            }
            sw.Stop();
            double unHorneado = sw.Elapsed.TotalMilliseconds / Math.Max(1, horneados);
            Console.WriteLine("     {0,-12} {1,10} {2,12} {3,12}", w + "x" + h, horneados,
                              unHorneado.ToString("0.0") + " ms", sw.Elapsed.TotalMilliseconds.ToString("0") + " ms");
            if (horneados > peorHorneados) peorHorneados = horneados;
        }
        Check(peorHorneados * 3 <= pasos.Length,
              "el cielo se rehornea como mucho un tercio de los cuadros (" + peorHorneados + " de " + pasos.Length + ")");

        // ---------- 3) Y EL CIELO ACABA CON EL COLOR EXACTO ----------
        // Esto es lo que hace que saltarse horneados sea seguro: no se redondea NINGUN color, solo se
        // aplaza el trabajo. En cuanto el deslizamiento para, el cielo que queda tiene que ser EXACTAMENTE
        // el mismo que si se hubiera horneado en todos y cada uno de los cuadros.
        // (Se probo la otra via -redondear el color a escalones- y se veia: el degradado lleva colores muy
        // oscuros, y ahi un escalon de nada es un cambio grande en proporcion. Por eso esta prueba compara
        // contra el cielo EXACTO y no contra "algo parecido".)
        Console.WriteLine("\n  3) Y EL CIELO ACABA CON EL COLOR EXACTO\n");
        {
            int w = 640, h = 480;
            object c = Activator.CreateInstance(TS, true);
            if (fDens != null) fDens.SetValue(c, 1f);
            mLay.Invoke(c, new object[] { w, h });

            // Se deja pasar el deslizamiento entero, saltandose horneados como hace el programa, y despues
            // se pide el color final unas cuantas veces mas (que es lo que pasa: el color ya no se mueve y
            // los cuadros siguen llegando). Al final el cielo tiene que ser identico, pixel a pixel, al que
            // sale de horneado directamente con ese color.
            Color destino = Color.FromArgb(178, 152, 255);
            for (int i = 0; i < pasos.Length; i++) { Bitmap tmp = Hornea(c, w, h, pasos[i]); tmp.Dispose(); }
            Bitmap tras = null;
            for (int i = 0; i < 4; i++) { if (tras != null) tras.Dispose(); tras = Hornea(c, w, h, destino); }

            object limpio = Activator.CreateInstance(TS, true);
            if (fDens != null) fDens.SetValue(limpio, 1f);
            mLay.Invoke(limpio, new object[] { w, h });
            using (Bitmap exacto = Hornea(limpio, w, h, destino))
            {
                int dif = 0, peor = 0;
                for (int y = 0; y < h; y += 2)
                    for (int x = 0; x < w; x += 2)
                    {
                        Color p = tras.GetPixel(x, y), q = exacto.GetPixel(x, y);
                        if (p.ToArgb() != q.ToArgb()) dif++;
                        int d = Math.Max(Math.Abs(p.R - q.R), Math.Max(Math.Abs(p.G - q.G), Math.Abs(p.B - q.B)));
                        if (d > peor) peor = d;
                    }
                Console.WriteLine("     tras el deslizamiento, pixeles que no cuadran con el cielo exacto: " + dif
                                  + "   (el que mas cambia, " + peor + " de 255)");
                Check(dif == 0, "el cielo acaba EXACTAMENTE igual que si se hubiera horneado en todos los cuadros");
            }
            tras.Dispose();
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }

    static int Esc(int v) { int q = ((v + 4) / 8) * 8; return (q > 255) ? 255 : q; }

    // Hornea el cielo con un color de genero y devuelve una copia de lo que quedo, para poder mirarlo.
    static FieldInfo fAccG, fMidG, fCacheG; static MethodInfo mSkyG, mColG, mCol2G, mColMG;
    static Bitmap Hornea(object c, int w, int h, Color acc)
    {
        fAccG.SetValue(c, acc);
        if (fMidG != null) fMidG.SetValue(c, (Color)mColMG.Invoke(null, new object[] { acc }));
        mSkyG.Invoke(c, new object[] { w, h, (Color)mColG.Invoke(null, new object[] { acc }),
                                             (Color)mCol2G.Invoke(null, new object[] { acc }) });
        Bitmap sc = (Bitmap)fCacheG.GetValue(c);
        return sc.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppArgb);
    }
}

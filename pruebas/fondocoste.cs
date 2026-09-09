using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

// CUANTO CUESTA EL FONDO, PIEZA POR PIEZA.
//
// "Busca optimizaciones para todos los elementos del fondo". El fondo son seis cosas encimadas -el cielo
// en degradado, la via lactea, las dos nebulosas, el campo de estrellas, las fugaces y la aurora del
// horizonte- y cada una se paga en un sitio distinto: unas se hornean una vez y otras se rehacen en cada
// cuadro.
//
// La tanda pasada se sospecho de la escritura en disco por el tiron al poner una cancion, se midio, y NO
// era: eran seis decimas de milisegundo. Lo caro estaba en otro lado. Optimizar sin medir es arreglar lo
// que uno se imagina.
//
// LO QUE SE MIDE AQUI, y por que importa cada uno:
//
//   1) HORNEAR EL CIELO. Una vez por cancion (y en cada cuadro de la respiracion, si se deja). Es lo que
//      hace que cambiar de cancion se note en una maquina lenta.
//   2) HORNEAR LAS OCHO CAPAS DE TITILEO, y lo que ocupan. Son ocho mapas de bits A PANTALLA COMPLETA:
//      en una pantalla grande eso es mucha memoria, y una maquina de bar no va sobrada.
//   3) COPIAR EL FONDO, por cuadro. Dos copias de pantalla completa, sesenta veces por segundo.
//   4) ARMAR LOS VERTICES DE LAS ESTRELLAS, por cuadro, en el camino de tarjeta grafica. Ahi hay un seno
//      por estrella y por cuadro, y hasta mil cuatrocientas estrellas.
//
// Y ADEMAS: CUANTO SE AHORRA BAJANDO LA ESCALA. El deslizador nuevo hornea y copia el fondo mas chico y lo
// estira al pintarlo. Aqui se mide lo que ahorra de verdad y lo que cuesta estirarlo, que es lo unico que
// permite decir si el estirado sale gratis o no.
class FondoCoste
{
    // Y COMO AHORA ES DE INSTANCIA, HAY QUE DARLE UNA. Se fabrica un panel SIN ENCENDER -sin constructor,
    // sin ventana, sin tarjeta- que es todo lo que hace falta para armar vertices en memoria. Si el metodo
    // vuelve a ser estatico algun dia, esto sigue valiendo: Invoke con instancia lo ignora.
    static object DueNo(MethodInfo m, Type t)
    { return m.IsStatic ? null : Codigo.FormSinEncender(t); }

    static object duenoVerts;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TS;

    static double Mediana(List<double> v) { v.Sort(); return v[v.Count / 2]; }

    // Un campo de estrellas listo para dibujar a WxH.
    static object NuevoCampo(int w, int h)
    {
        object sf = Activator.CreateInstance(TS, true);
        FieldInfo fD = TS.GetField("Density", TODO);
        if (fD != null) fD.SetValue(sf, 1f);
        FieldInfo fW = TS.GetField("SkyFullW", TODO), fH = TS.GetField("SkyFullH", TODO);
        if (fW != null) fW.SetValue(sf, w);
        if (fH != null) fH.SetValue(sf, h);
        return sf;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);
        if (TS == null) { Console.WriteLine("  FALLO  no se encontro el campo de estrellas"); return 1; }

        const int W = 1920, H = 1080;
        MethodInfo mDraw = TS.GetMethod("DrawCached", new Type[] { typeof(Graphics), typeof(int), typeof(int), typeof(Color), typeof(Color) });
        MethodInfo mLayers = TS.GetMethod("EnsureLayers", TODO);
        MethodInfo mSky = TS.GetMethod("EnsureSkyCache", TODO);
        MethodInfo mBlit = null;
        foreach (MethodInfo m in TS.GetMethods(TODO | BindingFlags.DeclaredOnly))
            if (m.Name == "BlitSkyAndStars" && m.GetParameters().Length == 4) mBlit = m;
        Check(mDraw != null && mLayers != null && mSky != null && mBlit != null, "las piezas del fondo se pueden pedir por separado");
        if (mDraw == null || mLayers == null || mSky == null || mBlit == null)
        { Console.WriteLine("\n  " + Math.Max(fallos, 1) + " FALLO(S)"); return Math.Max(fallos, 1); }

        Color arriba = Color.FromArgb(255, 13, 8, 12), abajo = Color.FromArgb(255, 102, 54, 66);
        FieldInfo fMid = TS.GetField("SkyMid", TODO), fAc = TS.GetField("Acento", TODO);
        FieldInfo fN1 = TS.GetField("NebUno", TODO), fN2 = TS.GetField("NebDos", TODO);

        Console.WriteLine("\n  1) HORNEAR EL CIELO (degradado + via lactea + las dos nebulosas)\n");
        object sf = NuevoCampo(W, H);
        if (fMid != null) fMid.SetValue(sf, Color.FromArgb(255, 33, 45, 97));
        if (fAc != null) fAc.SetValue(sf, Color.FromArgb(255, 225, 95, 75));
        if (fN1 != null) fN1.SetValue(sf, Color.FromArgb(255, 182, 107, 165));
        if (fN2 != null) fN2.SetValue(sf, Color.FromArgb(255, 87, 157, 232));
        using (Bitmap lienzo = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(lienzo))
        {
            mDraw.Invoke(sf, new object[] { g, W, H, arriba, abajo });   // la primera arma todo
            // OJO: el horneado se salta tres de cada cuatro llamadas a proposito -para que el deslizamiento
            // de color al cambiar de cancion no rehornee en cada cuadro-. Si no se le quita ese freno, esto
            // mediria el atajo y no el trabajo, y saldria cero.
            FieldInfo fSalt = TS.GetField("saltados", TODO);
            List<double> v = new List<double>();
            for (int k = 0; k < 5; k++)
            {
                // Un acento distinto cada vez: es lo que obliga a rehornear (la firma del cache).
                if (fAc != null) fAc.SetValue(sf, Color.FromArgb(255, 200 + k, 95, 75));
                if (fSalt != null) fSalt.SetValue(sf, 3);
                Stopwatch w = Stopwatch.StartNew();
                mSky.Invoke(sf, new object[] { W, H, arriba, abajo });
                w.Stop();
                v.Add(w.Elapsed.TotalMilliseconds);
            }
            double msCielo = Mediana(v);
            Console.WriteLine("     hornear el cielo a " + W + "x" + H + ": " + msCielo.ToString("0.0") + " ms");
            Console.WriteLine("     (en la maquina del bar hay que multiplicar por 4 o 5)");
            Check(msCielo > 0, "la medida del cielo salio");

            Console.WriteLine("\n  2) LAS OCHO CAPAS DE TITILEO: LO QUE TARDAN Y LO QUE OCUPAN\n");
            FieldInfo fCapas = TS.GetField("layers", TODO);
            object sf2 = NuevoCampo(W, H);
            Stopwatch w2 = Stopwatch.StartNew();
            mLayers.Invoke(sf2, new object[] { W, H });
            w2.Stop();
            Array capas = (fCapas != null) ? (Array)fCapas.GetValue(sf2) : null;
            int n = (capas != null) ? capas.Length : 0;
            long bytes = (long)n * W * H * 4;
            Console.WriteLine("     " + n + " capas a " + W + "x" + H + ": " + w2.Elapsed.TotalMilliseconds.ToString("0") + " ms de horneado");
            Console.WriteLine("     y " + (bytes / (1024 * 1024)) + " MB de memoria, que en una maquina de bar no sobran");
            Check(n > 0, "las capas de titileo se hornean");

            Console.WriteLine("\n  3) COPIAR EL FONDO, EN CADA CUADRO\n");
            mDraw.Invoke(sf2, new object[] { g, W, H, arriba, abajo });
            List<double> v3 = new List<double>();
            for (int k = 0; k < 15; k++)
            {
                Stopwatch w3 = Stopwatch.StartNew();
                mBlit.Invoke(sf2, new object[] { g, W, H, true });
                w3.Stop();
                v3.Add(w3.Elapsed.TotalMilliseconds);
            }
            double msCopia = Mediana(v3);

            Console.WriteLine("     copiar cielo + una capa de estrellas: " + msCopia.ToString("0.00") + " ms por cuadro");
            Console.WriteLine("     o sea " + (msCopia * 24).ToString("0.0") + " ms de cada mil a 24 cuadros por segundo");
            Check(msCopia > 0, "la medida de la copia salio");
        }

        Console.WriteLine("\n  3b) Y NO, BAJAR LA RESOLUCION DEL FONDO NO LO ACELERA\n");
        // SE PROBO, Y ESTA ES LA MEDIDA. Se pidio un mando para bajar la resolucion del fondo y estirarlo al
        // pintarlo, que es lo que hacen los juegos. Aqui NO sirve, y conviene que quede escrito para que no
        // se vuelva a intentar.
        //
        // Se hornearon el cielo y las capas de estrellas mas chicos y se estiraron al copiar. Comparado
        // INTERCALANDO las dos versiones y quedandose con la mediana de 31 pasadas de cada una -medir
        // primero todas las de una y luego todas las de la otra daba "26% mas rapido" en una tanda y "6% mas
        // lento" en la siguiente, con el mismo codigo-:
        //
        //     copiar el fondo por cuadro, a 1920x1080
        //        a tamano natural                    2.85 ms
        //        con el cielo al 70%, estirandolo    2.98 ms      un 4% MAS
        //
        // Y por piezas, para saber donde se va:
        //
        //        las estrellas a tamano natural      0.72 ms
        //        las estrellas al 70%, estiradas     5.09 ms      SIETE veces mas
        //        los tres modos de estirar, al 70%:  vecino mas cercano 2.06, bilineal 4.98,
        //                                            bilineal de calidad 174 (sesenta veces mas)
        //
        // LA RAZON: el cielo se copia TAPANDO lo que hay debajo y las estrellas se copian CON
        // TRANSPARENCIA. Estirar una copia con transparencia obliga al sistema a leer el destino,
        // remuestrear el origen y mezclar, pixel a pixel; y aunque el cielo estirado cuesta lo mismo, no
        // ahorra nada porque lo que se copia es igual de grande. Esta libreria de dibujo no tiene un camino
        // rapido para estirar, y sin uno, bajar la resolucion solo quita calidad.
        //
        // Lo que SI baja el coste del fondo en una maquina justa son las perillas que ya existen en
        // Ctrl+F3 -> Imagen: "Cuantas estrellas" y "Nieblas y nebulosas".
        //
        // Lo que se vigila aqui es que nadie lo vuelva a intentar sin medirlo: las capas de estrellas se
        // hornean al tamano de la pantalla, punto.
        FieldInfo fCapas3 = TS.GetField("layers", TODO);
        object sfE = NuevoCampo(W, H);
        mLayers.Invoke(sfE, new object[] { W, H });
        Bitmap capa0 = (Bitmap)((Array)fCapas3.GetValue(sfE)).GetValue(0);
        Console.WriteLine("     la capa de estrellas mide " + capa0.Width + "x" + capa0.Height
                          + ", la pantalla " + W + "x" + H);
        Check(capa0.Width == W && capa0.Height == H, "las estrellas se hornean al tamano de la pantalla");
        Check(TS.GetField("Escala", TODO) == null, "y no hay ninguna escala del fondo: se probo y salia peor");

        Console.WriteLine("\n  4) Y CON TARJETA GRAFICA: LOS VERTICES DE LAS ESTRELLAS, POR CUADRO\n");
        // Aqui esta el seno por estrella y por cuadro. Con mil cuatrocientas estrellas a 180 cuadros por
        // segundo son 252.000 senos por segundo, y DOS veces mientras cambia el patron.
        Type TGpu = null;
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuStarfield") TGpu = t;
        if (TGpu == null)
            Console.WriteLine("     (esta version no dibuja por tarjeta grafica: no hay vertices que armar)");
        else
        {
            // ESTATICO O DE INSTANCIA, DA IGUAL: lo que esta prueba vigila es que los vertices de las
            // estrellas se armen en UN SOLO SITIO, no como esta declarado ese sitio. Desde que el dibujo
            // mira ajustes propios del panel -las estrellas finas, por ejemplo- el metodo dejo de ser
            // estatico, y preguntar solo por los estaticos daba por roto justo el cambio que lo mejora.
            MethodInfo mVerts = TGpu.GetMethod("BuildStarVerts", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            duenoVerts = (mVerts != null) ? DueNo(mVerts, TGpu) : null;
            Check(mVerts != null, "los vertices de las estrellas se arman en un solo sitio (BuildStarVerts)");
            if (mVerts != null)
            {
                const int NE = 1400;
                // Cuantas siluetas distintas trae el atlas: pasarle una que no existe se sale del array.
                FieldInfo fAtlas = TGpu.GetField("AtlasU0", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                int formas = (fAtlas != null) ? ((Array)fAtlas.GetValue(null)).Length : 1;
                if (formas < 1) formas = 1;
                Console.WriteLine("     el atlas trae " + formas + " silueta(s) de estrella");
                Type TVtx = mVerts.GetParameters()[0].ParameterType.GetElementType();
                Array buf = Array.CreateInstance(TVtx, NE * 6);
                float[] xs = new float[NE], ys = new float[NE], ph = new float[NE], spd = new float[NE], sz = new float[NE];
                int[] kd = new int[NE], fm = new int[NE];
                uint[] col = new uint[NE];
                Random r = new Random(7);
                for (int i = 0; i < NE; i++)
                {
                    xs[i] = (float)(r.NextDouble() * 1920); ys[i] = (float)(r.NextDouble() * 1080);
                    ph[i] = (float)(r.NextDouble() * 6.28); spd[i] = 0.5f + (float)r.NextDouble();
                    sz[i] = 1f + (float)r.NextDouble() * 2f; kd[i] = i % 3;
                    fm[i] = i % formas;   // las siluetas que de verdad tiene el atlas, no un numero inventado
                    col[i] = 0xFFFFFFFF;
                }
                object[] arg = { buf, xs, ys, ph, spd, sz, kd, fm, col, NE, 0.0, 1f, 0.3f, new float[3], new float[3], 1920f, 1080f };
                mVerts.Invoke(duenoVerts, arg);   // la primera calienta
                List<double> v4 = new List<double>();
                for (int k = 0; k < 21; k++)
                {
                    arg[10] = (double)k * 0.04;
                    Stopwatch w4 = Stopwatch.StartNew();
                    for (int rep = 0; rep < 10; rep++) mVerts.Invoke(duenoVerts, arg);
                    w4.Stop();
                    v4.Add(w4.Elapsed.TotalMilliseconds / 10.0);
                }
                double msVerts = Mediana(v4);
                Console.WriteLine("     armar " + NE + " estrellas: " + msVerts.ToString("0.000") + " ms por cuadro");
                Console.WriteLine("     a 180 cuadros por segundo son " + (msVerts * 180).ToString("0") + " ms de cada mil");
                Console.WriteLine("     y el DOBLE durante el segundo en que cambia el patron, una vez por minuto");
                Check(msVerts > 0, "la medida de los vertices salio");
                // El titileo tiene que salir IGUAL que con Math.Sin: si se cambia por una tabla, aqui se ve.
                MethodInfo mTit = TGpu.GetMethod("Titileo", BindingFlags.Static | BindingFlags.NonPublic);
                // Y QUE RECIBA UN DOUBLE. En un bar la rocola no se apaga nunca, y el reloj del campo de
                // estrellas se lleva en double justo por eso: en float, a los pocos dias encendida se queda
                // sin decimales y el titileo va a saltos. La primera version de esta tabla recibia un float
                // -o sea que tiraba el double justo antes de usarlo- y volvia a meter ese fallo.
                if (mTit != null)
                {
                    ParameterInfo[] pt = mTit.GetParameters();
                    Check(pt.Length == 1 && pt[0].ParameterType == typeof(double),
                          "el titileo recibe el reloj en double: en float se va a saltos a los pocos dias");
                }
                if (mTit == null)
                    Console.WriteLine("     (el titileo todavia se calcula con Math.Sin en cada estrella)");
                else
                {
                    double peor = 0;
                    for (int k = 0; k < 20000; k++)
                    {
                        double x = k * 0.0007;
                        double tabla = Convert.ToDouble(mTit.Invoke(null, new object[] { (float)x }));
                        double real = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(x));
                        double d = Math.Abs(tabla - real) * 255.0;
                        if (d > peor) peor = d;
                    }
                    Console.WriteLine("     el titileo por tabla se aparta como mucho " + peor.ToString("0.00") + " niveles de 255 del real");
                    Check(peor < 1.0, "y eso es menos de un nivel: invisible, que era la regla");
                    // Y CON LA ROCOLA LLEVANDO SEMANAS ENCENDIDA. Se le da el reloj que tendria a los 30
                    // dias y se mira que el titileo SIGA MOVIENDOSE cuadro a cuadro, en vez de quedarse
                    // clavado en el mismo valor porque el numero ya no tiene decimales.
                    double treinta = 30.0 * 24 * 3600;          // segundos en 30 dias
                    double paso = 1.0 / 180.0;                  // un cuadro a 180 por segundo
                    int quietos = 0; double antes = -1;
                    for (int k = 0; k < 400; k++)
                    {
                        double v = Convert.ToDouble(mTit.Invoke(null, new object[] { (treinta + k * paso) * 0.8 }));
                        if (k > 0 && v == antes) quietos++;
                        antes = v;
                    }
                    Console.WriteLine("     con la rocola 30 dias encendida, el titileo se queda clavado "
                                      + quietos + " cuadros de 400");
                    Check(quietos < 20, "a los 30 dias el titileo sigue moviendose, no va a saltos");
                }

                Console.WriteLine("\n  4b) Y EL CRUCE DE PATRONES NO DIBUJA LO QUE YA NO SE VE\n");
                // Mientras se cruzan los dos patrones -1.3 s, una vez por minuto- se arman y se dibujan DOS
                // juegos de estrellas: hasta 2800 por cuadro. Pero la ultima parte de ese cruce no se ve: el
                // brillo acaba siendo un numero de 0 a 255, asi que en cuanto lo que queda del patron viejo
                // baja de medio nivel, todas sus estrellas salen ya transparentes del todo.
                FieldInfo fMin = TGpu.GetField("FadeMinimo", TODO);
                Check(fMin != null, "hay un minimo por debajo del cual no se dibuja (FadeMinimo)");
                if (fMin != null)
                {
                    float min = Convert.ToSingle(fMin.GetValue(null));
                    Console.WriteLine("     no se dibuja por debajo de " + (min * 255).ToString("0.0") + " niveles de 255");
                    Check(min > 0f && min * 255f < 1f, "y ese minimo es MENOS de un nivel: lo que se corta no se veia");
                    Console.WriteLine("     mientras cruza son dos juegos: " + (msVerts * 2).ToString("0.000")
                                      + " ms por cuadro en vez de " + msVerts.ToString("0.000"));
                    Console.WriteLine("     y el ultimo tramo del cruce vuelve a costar uno solo");
                }
                {

                }
            }
        }

        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN" : fallos + " FALLO(S)"));
        return fallos;
    }
}

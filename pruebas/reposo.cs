using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LA PANTALLA DE REPOSO ES LA QUE MAS HORAS PASA ENCENDIDA.
//
// En un bar, la rocola esta la mayor parte del tiempo sin que nadie la toque: se ve la pantalla de reposo,
// con el cielo estrellado y el cubo gigante girando. Y esa pantalla era la mas cara de todo el programa,
// porque REHACIA EL CIELO ENTERO -las seiscientas estrellas, una a una, con su halo- veinticuatro veces por
// segundo. El resto del programa hace tiempo que titila copiando capas ya horneadas, cinco veces por segundo.
//
// Ahora el cielo de reposo se rehace tambien cinco veces por segundo, o sea al MISMO ritmo que el resto. Lo
// unico que se pierde es que las estrellas titilan mas despacio, que es exactamente lo que ya hacian en las
// demas pantallas.
//
// Eso solo se puede hacer si las ESTRELLAS FUGACES salen del horneado. Una fugaz cruza la pantalla en un
// segundo: si viajara dentro de lo que se hornea, avanzaria de golpe cinco veces y se veria a trompicones.
// Van dibujadas en vivo por encima, y aqui se comprueba que de verdad es asi.
class Reposo
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

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
        Type TF = asm.GetType("Rocola.Form1", true);
        Type TStar = null, TAtr = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "Starfield") TStar = t;
            if (t.Name == "AttractView") TAtr = t;
        }
        if (TStar == null) { Check(false, "esta el campo de estrellas"); return 1; }

        // ---------- 1) EL CIELO DE REPOSO SE REHACE AL RITMO DEL RESTO ----------
        Console.WriteLine("\n  1) EL CIELO DE REPOSO SE REHACE AL RITMO DEL RESTO DEL PROGRAMA\n");
        FieldInfo fRef = TStar.GetField("RefrescoMs", BindingFlags.Instance | BindingFlags.Public);
        Check(fRef != null, "se puede decir cada cuanto se rehace el cielo dibujado en vivo");
        if (fRef == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        if (TAtr != null)
        {
            object av = Activator.CreateInstance(TAtr, true);
            FieldInfo fAs = TAtr.GetField("attractStars", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fAs != null)
            {
                int ms = (int)fRef.GetValue(fAs.GetValue(av));
                Console.WriteLine("     la pantalla de reposo rehace su cielo cada " + ms + " ms");
                Check(ms >= 150, "la de reposo lo rehace de tarde en tarde, no en cada cuadro");
            }
            ((IDisposable)av).Dispose();
        }

        // ---------- 2) Y ESO SE NOTA EN LO QUE CUESTA ----------
        // Un segundo de pantalla son 24 cuadros. Se mide lo que cuesta UN cuadro de los que rehacen el cielo
        // y lo que cuesta uno de los que solo lo copian, y de ahi sale el segundo entero: con el ritmo de
        // antes se rehacia en los 24 (el cuadro dura 41 ms y el limite eran 40); con el de ahora, en 5.
        // Se mide asi y no dejando correr un segundo de verdad porque un segundo real en una maquina
        // compartida mide sobre todo lo que estuvieran haciendo los demas.
        Console.WriteLine("\n  2) Y ESO SE NOTA EN LO QUE CUESTA UN SEGUNDO DE PANTALLA\n");
        MethodInfo mDraw = TStar.GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo fVar = TStar.GetField("Varied", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fDens = TStar.GetField("Density", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        int[,] tam = { { 1024, 768 }, { 1920, 1080 } };
        for (int t = 0; t < tam.GetLength(0); t++)
        {
            int w = tam[t, 0], h = tam[t, 1];
            object cielo = Activator.CreateInstance(TStar, true);
            if (fVar != null) fVar.SetValue(cielo, true);
            if (fDens != null) fDens.SetValue(cielo, 1f);
            using (Bitmap dst = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(dst))
            {
                object[] ar = new object[] { g, w, h, Color.FromArgb(255, 6, 8, 26), Color.FromArgb(255, 18, 20, 54),
                                             Color.White, 0.0, true, 0f };
                mDraw.Invoke(cielo, ar);   // calentar (arma el patron)
                FieldInfo fLast = TStar.GetField("lastBuild", BindingFlags.Instance | BindingFlags.NonPublic);
                // Para medir el cuadro que SOLO COPIA se pone un plazo enorme; para medir el que REHACE se
                // pone un plazo de cero, que obliga a rehacerlo siempre. Atrasar el reloj no basta: con el
                // plazo enorme puesto, el cielo no se rehace por muy atrasado que se ponga.
                fRef.SetValue(cielo, 1000000);
                double tCopia = Mide(10, delegate { mDraw.Invoke(cielo, ar); });
                fRef.SetValue(cielo, 0);
                double tRehace = Mide(4, delegate { fLast.SetValue(cielo, 0); mDraw.Invoke(cielo, ar); });
                double antes = 24 * tRehace;                     // se rehacia en los 24 cuadros
                double ahora = 5 * tRehace + 19 * tCopia;        // ahora, en 5 de los 24
                Console.WriteLine("     " + (w + "x" + h).PadRight(10) + " rehacer el cielo " + tRehace.ToString("0.00")
                                  + " ms, copiarlo " + tCopia.ToString("0.00") + " ms  ->  un segundo de reposo: antes "
                                  + antes.ToString("0.0") + " ms, ahora " + ahora.ToString("0.0")
                                  + " ms   (x" + (antes / ahora).ToString("0.0") + ")");
                Check(antes / ahora >= 2.0, "a " + w + "x" + h + ", un segundo de pantalla de reposo cuesta al menos la mitad");
            }
        }

        // ---------- 3) LAS FUGACES SIGUEN YENDO FLUIDAS ----------
        // Se congela el cielo -no se le deja rehacerse- y se dibujan dos cuadros seguidos. Si la fugaz
        // viviera dentro de lo horneado, los dos cuadros saldrian IDENTICOS y la fugaz iria a trompicones.
        // Al ir dibujada en vivo por encima, tiene que haber avanzado.
        Console.WriteLine("\n  3) LAS FUGACES NO VAN DENTRO DE LO QUE SE HORNEA\n");
        Check(TStar.GetMethod("RenderInto", BindingFlags.Instance | BindingFlags.NonPublic) != null,
              "el cielo se hornea aparte de lo que se dibuja encima");
        {
            int w = 640, h = 480;
            object cielo = Activator.CreateInstance(TStar, true);
            if (fDens != null) fDens.SetValue(cielo, 1f);
            Bitmap b1 = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            Bitmap b2 = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            object[] ar = new object[] { null, w, h, Color.FromArgb(255, 6, 8, 26), Color.FromArgb(255, 18, 20, 54),
                                         Color.White, 0.0, true, 0f };
            using (Graphics g = Graphics.FromImage(b1)) { ar[0] = g; mDraw.Invoke(cielo, ar); }   // aqui sale la 1a fugaz
            fRef.SetValue(cielo, 1000000);   // a partir de ahora el cielo NO se rehace: solo se copia
            // OJO con el orden: la fugaz se DIBUJA donde esta y DESPUES avanza, o sea que el cuadro de
            // despues de una espera la ensena todavia donde estaba. Hacen falta dos cuadros DESPUES de la
            // espera para verla en dos sitios distintos.
            //
            // Y SE INSISTE HASTA VERLA, en vez de mirar dos cuadros y ya. Antes bastaba con dos: la fugaz
            // nacia DENTRO de la pantalla, asi que se veia desde el primer momento. Ahora nace FUERA -pegada
            // al borde por donde entra, para que le quede pantalla por delante y de tiempo a verla- y tarda
            // una o dos decimas en asomar; las de las gordas, que van lentas, algo mas. Con dos cuadros la
            // prueba salia bien o mal segun la suerte, que es peor que no tenerla.
            int distintos = 0;
            for (int intento = 0; intento < 24 && distintos == 0; intento++)
            {
                System.Threading.Thread.Sleep(60);
                using (Graphics g = Graphics.FromImage(b1)) { ar[0] = g; mDraw.Invoke(cielo, ar); }
                System.Threading.Thread.Sleep(60);
                using (Graphics g = Graphics.FromImage(b2)) { ar[0] = g; mDraw.Invoke(cielo, ar); }
                for (int y = 0; y < h; y += 2)
                    for (int x = 0; x < w; x += 2)
                        if (b1.GetPixel(x, y).ToArgb() != b2.GetPixel(x, y).ToArgb()) distintos++;
            }
            Console.WriteLine("     con el cielo congelado, pixeles que aun se mueven: " + distintos);
            Check(distintos > 0, "la fugaz avanza aunque el cielo no se rehaga: va dibujada en vivo");
            b1.Dispose(); b2.Dispose();
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

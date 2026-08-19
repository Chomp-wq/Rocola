using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// LAS ESTRELLAS DEL FONDO: QUE HAYA VARIEDAD, Y QUE NO CUESTE MAS.
//
// EL CIELO: QUE PAREZCA UN CIELO, Y QUE NO CUESTE MAS.
//
// Una estrella de verdad no tiene forma: es un PUNTO de luz. Se probaron siluetas dibujadas -un destello de
// cuatro puntas, una cruz, una rafaga de ocho rayos, la estrella de cinco puntas- y ninguna parecia el cielo:
// parecian pegatinas. Ahora cada estrella es un halo suave con un nucleo diminuto y muy brillante, y solo un
// punado -las mas fuertes- llevan las cuatro rayitas cruzadas que hace el diafragma de una camara.
//
// Lo que se comprueba aqui, que es lo que puede romperse sin que nadie lo note:
//
//   1) Que el cielo tenga MUCHAS estrellas y casi todas debiles, agrupadas en vez de espolvoreadas con
//      regularidad, y con las rayitas en muy pocas.
//   2) Que el color siga siendo de estrella (de azul-blanco a naranja) y que el fuerte se lo queden las
//      grandes, para que el fondo no se vuelva confeti.
//   3) Y sobre todo: QUE NO CUESTE MAS. El polvo -tres cuartas partes del cielo- se hornea UNA vez en el
//      bitmap del cielo y no ocho veces en las capas de titileo. Por eso se puede tener el doble de
//      estrellas y que hornear siga costando menos que antes.
class Estrellas
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
        Type TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);
        Check(TS != null, "el campo de estrellas esta donde tiene que estar");
        if (TS == null) return 1;

        Console.WriteLine("\n  1) UN CIELO, NO UN PUÑADO DE PEGATINAS\n");

        // Las siluetas dibujadas no vuelven: se probaron todas y ninguna parecia el cielo.
        Check(TS.GetMethod("DrawCruz", BindingFlags.Instance | BindingFlags.NonPublic) == null
              && TS.GetMethod("DrawRafaga", BindingFlags.Instance | BindingFlags.NonPublic) == null
              && TS.GetMethod("DrawSparkle", BindingFlags.Instance | BindingFlags.NonPublic) == null
              && TS.GetMethod("DrawEstrella", BindingFlags.Instance | BindingFlags.NonPublic) == null,
              "no hay siluetas dibujadas: cada estrella es un punto de luz");
        Check(TS.GetMethod("DrawRayos", BindingFlags.Instance | BindingFlags.NonPublic) != null
              && TS.GetMethod("DrawNucleo", BindingFlags.Instance | BindingFlags.NonPublic) != null,
              "hay nucleo y rayitas cruzadas");

        object sf = Activator.CreateInstance(TS, true);
        FieldInfo fDens = TS.GetField("Density", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fDens != null) fDens.SetValue(sf, 1f);
        MethodInfo mEnsure = TS.GetMethod("Ensure", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fStars = TS.GetField("stars", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mEnsure != null && fStars != null, "se le puede pedir que arme su patron de estrellas");
        if (mEnsure == null || fStars == null) return 1;
        mEnsure.Invoke(sf, new object[] { 1280, 800 });
        Array estrellas = (Array)fStars.GetValue(sf);
        Type TStar = estrellas.GetType().GetElementType();
        FieldInfo fKind = TStar.GetField("Kind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fBri = TStar.GetField("Bri", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fRay = TStar.GetField("Rayos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fX = TStar.GetField("X", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fY = TStar.GetField("Y", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fR = TStar.GetField("R", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fG = TStar.GetField("G", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fB = TStar.GetField("B", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(fBri != null && fRay != null, "cada estrella lleva su brillo y si tiene rayitas");
        if (fBri == null || fRay == null) return 1;

        int grandesColor = 0, grandes = 0, polvoColor = 0, polvo = 0, medianas = 0, rayos = 0, debiles = 0;
        int verdes = 0;
        float[] xs = new float[estrellas.Length], ys = new float[estrellas.Length];
        for (int i = 0; i < estrellas.Length; i++)
        {
            object st = estrellas.GetValue(i);
            int k = Convert.ToInt32(fKind.GetValue(st));
            int bri = Convert.ToInt32(fBri.GetValue(st));
            if ((bool)fRay.GetValue(st)) rayos++;
            if (bri < 110) debiles++;
            xs[i] = Convert.ToSingle(fX.GetValue(st)); ys[i] = Convert.ToSingle(fY.GetValue(st));
            int r = Convert.ToInt32(fR.GetValue(st)), g = Convert.ToInt32(fG.GetValue(st)), bb = Convert.ToInt32(fB.GetValue(st));
            // Una estrella de verdad NUNCA es verde ni violeta: su color va de azul-blanco a naranja, o sea
            // que el verde nunca puede ser el canal que mas manda.
            if (g > r + 8 && g > bb + 8) verdes++;
            int max = Math.Max(r, Math.Max(g, bb)), min = Math.Min(r, Math.Min(g, bb));
            bool conColor = (max - min) > 45;
            if (k == 2) { grandes++; if (conColor) grandesColor++; }
            if (k == 1) medianas++;
            if (k == 0) { polvo++; if (conColor) polvoColor++; }
        }
        Console.WriteLine("     " + estrellas.Length + " estrellas:  " + grandes + " grandes, " + medianas
                          + " medianas y " + polvo + " de polvo   (" + rayos + " con rayitas)");
        Check(estrellas.Length >= 400, "hay MUCHAS estrellas, no un punado (" + estrellas.Length + ")");
        Check(polvo * 2 > estrellas.Length, "la mayoria son polvo del fondo (" + polvo + " de " + estrellas.Length + ")");
        Check(debiles * 2 > estrellas.Length, "y la mayoria son debiles: " + debiles + " por debajo de la mitad de brillo");
        Check(rayos > 0 && rayos <= 16, "las rayitas las llevan MUY pocas (" + rayos + "), no ciento y pico");
        Check(verdes == 0, "ninguna estrella es verde ni violeta: el color va de azul-blanco a naranja");

        // AGRUPADAS, no espolvoreadas con regularidad. Se mide con la distancia de cada estrella a su vecina
        // mas cercana: repartidas al azar da una media conocida, y agrupadas da bastante menos.
        double suma = 0;
        for (int i = 0; i < xs.Length; i++)
        {
            double mejor = double.MaxValue;
            for (int j = 0; j < xs.Length; j++)
            {
                if (i == j) continue;
                double dx = xs[i] - xs[j], dy = ys[i] - ys[j], d = dx * dx + dy * dy;
                if (d < mejor) mejor = d;
            }
            suma += Math.Sqrt(mejor);
        }
        double media = suma / xs.Length;
        double alAzar = 0.5 / Math.Sqrt(xs.Length);   // lo que daria un reparto al azar en un cuadrado de lado 1
        Console.WriteLine("     vecina mas cercana: " + media.ToString("0.0000") + "   (al azar seria " + alAzar.ToString("0.0000") + ")");
        Check(media < alAzar * 0.92, "las estrellas se AGRUPAN, no van espolvoreadas parejo");

        Console.WriteLine("\n  2) EL COLOR FUERTE SE LO QUEDAN LAS GRANDES\n");
        Console.WriteLine("     grandes con color: " + grandesColor + " de " + grandes
                          + "     polvo con color: " + polvoColor + " de " + polvo);
        Check(grandes > 0 && grandesColor * 3 > grandes, "un tercio largo de las grandes llevan color de verdad");
        Check(polvo > 0 && polvoColor * 2 < polvo, "y menos de la mitad del polvo: el fondo no se ensucia");

        Console.WriteLine("\n  3) Y NO CUESTA MAS QUE ANTES\n");

        // LA CLAVE DE QUE ESTO SEA GRATIS: el polvo se hornea UNA vez en el bitmap del cielo, y solo las
        // pocas que titilan entran en las ocho capas. Lo que se hornea ocho veces tiene que seguir siendo un
        // punado; si alguien mete el polvo en las capas, hornear se multiplica por ocho y la rocola da un
        // tiron al arrancar y en cada cambio de resolucion.
        int enCapas = grandes + medianas;
        Console.WriteLine("     se hornean 8 veces: " + enCapas + " estrellas de " + estrellas.Length
                          + "   (el polvo, " + polvo + ", va una sola vez en el cielo)");
        Check(enCapas * 4 < estrellas.Length, "lo que se hornea ocho veces es menos de la cuarta parte del cielo");
        Check(enCapas <= 160, "y en numeros absolutos sigue siendo un punado (" + enCapas + ")");

        MethodInfo mDraw = TS.GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        using (Bitmap b2 = new Bitmap(1024, 620, PixelFormat.Format32bppPArgb))
        using (Graphics g2 = Graphics.FromImage(b2))
        {
            g2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            object[] a = new object[] { g2, 1024, 620, Color.FromArgb(6, 8, 26), Color.FromArgb(26, 44, 90),
                                        Color.FromArgb(120, 160, 255), 0.0, false, 0f };
            double ms = Mide(120, delegate { mDraw.Invoke(sf, a); });
            Console.WriteLine("     dibujar el cielo entero: " + ms.ToString("0.000") + " ms   ("
                              + (ms * 24).ToString("0.0") + " ms por segundo a 24 cuadros)");
            Check(ms < 2.0, "dibujar el fondo sigue costando menos de 2 ms (es un blit de lo ya horneado)");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// LAS ESTRELLAS DEL FONDO: QUE HAYA VARIEDAD, Y QUE NO CUESTE MAS.
//
// LA ESTRELLA DEL CIELO ES LA DE CINCO PUNTAS, la que dibuja todo el mundo cuando le dicen "una estrella".
// Se probaron otras siluetas -un destello de cuatro puntas, una cruz de brazos finos, una rafaga de ocho
// rayos- y ninguna pegaba con la rocola: la cruz se leia como un monton de signos de MAS repartidos por el
// cielo, y las otras dos como chispas o copos de nieve. La variedad la ponen el TAMANO y el COLOR.
//
// Aqui no se mira ningun campo por dentro -eso se podria cambiar y la bateria seguiria pasando-: se DIBUJA
// una estrella y se le CUENTAN LAS PUNTAS mirando los pixeles. Ademas se comprueba que el polvo del fondo
// se queda en puntos palidos, que el color fuerte se lo quedan las pocas grandes (para que el cielo no se
// vuelva un confeti) y que dibujarlas no cuesta mas que antes.
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

        Console.WriteLine("\n  1) LA SILUETA ES UNA ESTRELLA DE CINCO PUNTAS\n");

        // Se dibuja UNA estrella grande sobre negro y se le cuentan las puntas: se recorre un circulo
        // alrededor del centro y se cuentan los tramos con tinta. Una estrella de cinco puntas da cinco.
        MethodInfo mEstrella = TS.GetMethod("DrawEstrella", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mEstrella != null, "la rocola sabe dibujar la estrella");
        if (mEstrella != null)
        {
            object dib = Activator.CreateInstance(TS, true);
            const int LADO = 200, CEN = LADO / 2, RAD = 70;
            using (Bitmap b1 = new Bitmap(LADO, LADO, PixelFormat.Format32bppArgb))
            {
                using (Graphics g1 = Graphics.FromImage(b1))
                using (SolidBrush br = new SolidBrush(Color.White))
                {
                    g1.Clear(Color.Black);
                    g1.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    mEstrella.Invoke(dib, new object[] { g1, br, CEN, CEN, RAD });
                }
                b1.Save("estrella.png", ImageFormat.Png);
                // El circulo se mira a 0.80 del radio: por ahi pasan solo los brazos, no el cuerpo central.
                int puntas = 0; bool dentro = false; bool primero = false;
                for (int i = 0; i <= 720; i++)
                {
                    double ang = -Math.PI / 2 + i * Math.PI / 360.0;
                    int x = CEN + (int)(Math.Cos(ang) * RAD * 0.80), y = CEN + (int)(Math.Sin(ang) * RAD * 0.80);
                    bool hay = (x >= 0 && y >= 0 && x < LADO && y < LADO && b1.GetPixel(x, y).R > 128);
                    if (i == 0) primero = hay;
                    if (hay && !dentro) puntas++;
                    dentro = hay;
                }
                if (primero && dentro) puntas--;   // el brazo de arriba se contaria dos veces al cerrar la vuelta
                Console.WriteLine("     brazos contados alrededor de la estrella: " + puntas);
                Check(puntas == 5, "la estrella tiene CINCO puntas (cuenta " + puntas + ")");
                // Y mira hacia ARRIBA, como la de toda la vida: justo encima del centro tiene que haber tinta.
                Check(b1.GetPixel(CEN, CEN - (int)(RAD * 0.80)).R > 128, "y con una punta hacia arriba");
                Console.WriteLine("     (dibujada en pruebas/estrella.png, por si se quiere mirar)");
            }
        }

        // Las siluetas viejas no vuelven: se probaron y no pegaban.
        Check(TS.GetMethod("DrawCruz", BindingFlags.Instance | BindingFlags.NonPublic) == null
              && TS.GetMethod("DrawRafaga", BindingFlags.Instance | BindingFlags.NonPublic) == null
              && TS.GetMethod("DrawSparkle", BindingFlags.Instance | BindingFlags.NonPublic) == null,
              "las siluetas viejas -cruz, rafaga y destello de cuatro puntas- no han vuelto");

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
        FieldInfo fR = TStar.GetField("R", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fG = TStar.GetField("G", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fB = TStar.GetField("B", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        int grandesColor = 0, grandes = 0, polvoColor = 0, polvo = 0, medianas = 0;
        for (int i = 0; i < estrellas.Length; i++)
        {
            object st = estrellas.GetValue(i);
            int k = Convert.ToInt32(fKind.GetValue(st));
            int r = Convert.ToInt32(fR.GetValue(st)), g = Convert.ToInt32(fG.GetValue(st)), bb = Convert.ToInt32(fB.GetValue(st));
            int max = Math.Max(r, Math.Max(g, bb)), min = Math.Min(r, Math.Min(g, bb));
            bool conColor = (max - min) > 60;   // se aparta bastante del blanco
            if (k == 2) { grandes++; if (conColor) grandesColor++; }
            if (k == 1) medianas++;
            if (k == 0) { polvo++; if (conColor) polvoColor++; }
        }
        Console.WriteLine("     de " + estrellas.Length + " estrellas: " + grandes + " grandes, " + medianas
                          + " medianas y " + polvo + " de polvo");
        Check(grandes > 0 && medianas > 0, "hay estrellas de las dos tallas que llevan silueta");
        Check(polvo > estrellas.Length / 2, "la mayoria siguen siendo polvo: el cielo no se vuelve confeti");

        Console.WriteLine("\n  2) EL COLOR FUERTE SE LO QUEDAN LAS GRANDES\n");
        Console.WriteLine("     grandes con color: " + grandesColor + " de " + grandes
                          + "     polvo con color: " + polvoColor + " de " + polvo);
        Check(grandes > 0 && grandesColor * 2 > grandes, "mas de la mitad de las grandes llevan color de verdad");
        Check(polvo > 0 && polvoColor * 4 < polvo, "y menos de la cuarta parte del polvo lleva color: el fondo no se ensucia");

        Console.WriteLine("\n  3) LA VARIEDAD NO CUESTA MAS\n");
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

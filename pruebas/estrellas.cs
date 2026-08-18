using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// LAS ESTRELLAS DEL FONDO: QUE HAYA VARIEDAD, Y QUE NO CUESTE MAS.
//
// El cielo tenia una sola silueta -un destello de cuatro puntas- y por eso se veia siempre igual. Ahora hay
// cuatro: destello, cruz, rafaga y punto. Aqui se comprueba que de verdad salen las cuatro, que el reparto
// es el pensado (el polvo de estrellas se queda en puntos palidos y el color fuerte se lo quedan las pocas
// grandes, para que el fondo no se vuelva un confeti) y que dibujarlas no cuesta mas que antes.
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

        Console.WriteLine("\n  1) SALEN LAS CUATRO SILUETAS\n");
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
        FieldInfo fForma = TStar.GetField("Forma", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fKind = TStar.GetField("Kind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fR = TStar.GetField("R", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fG = TStar.GetField("G", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo fB = TStar.GetField("B", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(fForma != null, "cada estrella lleva apuntada su silueta");
        if (fForma == null) return 1;

        int[] cuenta = new int[4];
        int grandesColor = 0, grandes = 0, polvoColor = 0, polvo = 0;
        for (int i = 0; i < estrellas.Length; i++)
        {
            object s = estrellas.GetValue(i);
            int f = Convert.ToInt32(fForma.GetValue(s));
            if (f >= 0 && f < 4) cuenta[f]++;
            int k = Convert.ToInt32(fKind.GetValue(s));
            int r = Convert.ToInt32(fR.GetValue(s)), g = Convert.ToInt32(fG.GetValue(s)), b = Convert.ToInt32(fB.GetValue(s));
            int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            bool conColor = (max - min) > 60;   // se aparta bastante del blanco
            if (k == 2) { grandes++; if (conColor) grandesColor++; }
            if (k == 0) { polvo++; if (conColor) polvoColor++; }
        }
        string[] nom = { "destello", "cruz", "rafaga", "punto" };
        for (int i = 0; i < 4; i++)
            Console.WriteLine("     " + nom[i].PadRight(10) + cuenta[i].ToString().PadLeft(4) + "  ("
                              + (100.0 * cuenta[i] / estrellas.Length).ToString("0") + "%)");
        Check(cuenta[0] > 0 && cuenta[1] > 0 && cuenta[2] > 0 && cuenta[3] > 0, "aparecen las CUATRO siluetas, no una sola");
        Check(cuenta[3] > estrellas.Length / 2, "la mayoria siguen siendo puntos: el polvo de estrellas no se vuelve confeti");
        Check(cuenta[1] > 0 && cuenta[2] > 0, "hay cruces y rafagas, que son las siluetas nuevas");

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

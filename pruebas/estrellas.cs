using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// LAS ESTRELLAS DEL FONDO: QUE HAYA VARIEDAD, Y QUE NO CUESTE MAS.
//
// El cielo tenia una sola silueta -un destello de cuatro puntas- y por eso se veia siempre igual. Ahora hay
// tres: destello, rafaga y punto. Hubo una cuarta, una cruz de brazos finos, que se llevaba mas de la mitad
// de las estrellas medianas: en pantalla se leia como un monton de signos de MAS repartidos por el cielo y
// quedaba fea, asi que se quito. Aqui se comprueba que salen las tres, que la CRUZ no ha vuelto, que el
// reparto es el pensado (el polvo se queda en puntos palidos y el color fuerte se lo quedan las pocas
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

        Console.WriteLine("\n  1) SALEN LAS TRES SILUETAS (Y LA CRUZ NO VUELVE)\n");
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

        int[] cuenta = new int[8];   // de sobra: si volviera a haber mas siluetas, se cuentan igual
        int grandesColor = 0, grandes = 0, polvoColor = 0, polvo = 0;
        for (int i = 0; i < estrellas.Length; i++)
        {
            object s = estrellas.GetValue(i);
            int f = Convert.ToInt32(fForma.GetValue(s));
            if (f >= 0 && f < cuenta.Length) cuenta[f]++;
            int k = Convert.ToInt32(fKind.GetValue(s));
            int r = Convert.ToInt32(fR.GetValue(s)), g = Convert.ToInt32(fG.GetValue(s)), b = Convert.ToInt32(fB.GetValue(s));
            int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            bool conColor = (max - min) > 60;   // se aparta bastante del blanco
            if (k == 2) { grandes++; if (conColor) grandesColor++; }
            if (k == 0) { polvo++; if (conColor) polvoColor++; }
        }
        // Los numeros salen del propio ejecutable, no de una lista escrita aqui: si algun dia se anade o se
        // quita una silueta, esta bateria lo ensena en vez de mentir con los nombres viejos.
        Type TSF = TS;
        int DESTELLO = Convert.ToInt32(TSF.GetField("FormaDestello", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null));
        int RAFAGA = Convert.ToInt32(TSF.GetField("FormaRafaga", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null));
        int PUNTO = Convert.ToInt32(TSF.GetField("FormaPunto", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null));
        string[] nom = new string[cuenta.Length];
        for (int i = 0; i < nom.Length; i++) nom[i] = "silueta " + i;
        nom[DESTELLO] = "destello"; nom[RAFAGA] = "rafaga"; nom[PUNTO] = "punto";
        for (int i = 0; i < cuenta.Length; i++)
            if (cuenta[i] > 0)
                Console.WriteLine("     " + nom[i].PadRight(10) + cuenta[i].ToString().PadLeft(4) + "  ("
                                  + (100.0 * cuenta[i] / estrellas.Length).ToString("0") + "%)");
        Check(cuenta[DESTELLO] > 0 && cuenta[RAFAGA] > 0 && cuenta[PUNTO] > 0, "aparecen las TRES siluetas, no una sola");
        Check(cuenta[PUNTO] > estrellas.Length / 2, "la mayoria siguen siendo puntos: el polvo de estrellas no se vuelve confeti");
        // LA CRUZ NO VUELVE. Se quito porque quedaba fea, y una silueta fea se cuela sola en cuanto alguien
        // toque el reparto: aqui se vigila que no exista ni como constante ni dibujada.
        Check(TSF.GetField("FormaCruz", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null
              && TSF.GetMethod("DrawCruz", BindingFlags.Instance | BindingFlags.NonPublic) == null,
              "la cruz de dos palos no ha vuelto");
        // Y la rafaga -la silueta trabajada, la de los ocho rayos- tiene que salir DE VERDAD, no ser una
        // rareza que aparece una vez cada pantalla.
        int conForma = cuenta[DESTELLO] + cuenta[RAFAGA];
        Console.WriteLine("     de las que llevan silueta, la rafaga es el " + (100 * cuenta[RAFAGA] / Math.Max(1, conForma)) + "%");
        Check(cuenta[RAFAGA] * 100 >= conForma * 35, "la rafaga es al menos un tercio de las estrellas con silueta");

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

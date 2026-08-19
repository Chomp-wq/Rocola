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
// parecian pegatinas. Ahora cada estrella es un halo con un nucleo diminuto y muy brillante, mas las cuatro
// rayitas cruzadas que hace el diafragma de una camara.
//
// LA REGLA QUE MANDA: una bola redonda solo pasa por estrella si es DIMINUTA. Durante un tiempo las rayitas
// las llevaban solo diez o doce y las otras ciento y pico se quedaban de disco borroso con un punto dentro:
// se veian como puntos gordos amontonados, no como un cielo. Ahora o eres polvo de UN pixel, o llevas
// silueta. Y las rayitas van escaladas -la mediana las lleva cortas y tenues, la grande largas y marcadas-,
// que es lo que evita que sesenta estrellas con cruz parezcan purpurina.
//
// Lo que se comprueba aqui, que es lo que puede romperse sin que nadie lo note:
//
//   1) Que el cielo tenga MUCHAS estrellas y casi todas debiles, REPARTIDAS POR IGUAL por todo el fondo
//      -sin hileras pegadas a los bordes y sin amontonarse en manchas, tambien en pantalla ancha-, y que
//      ninguna de las que se ven sea una bola redonda.
//   2) Que el color siga siendo de estrella (de azul-blanco a naranja) y que la inmensa mayoria del cielo
//      sea casi blanco, para que el fondo no se vuelva confeti.
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

    // Cuanto se parecen entre si los trozos del cielo: la variacion entre casillas dividida por lo que se
    // espera de media en cada una. Un reparto al azar da 1; con grumos sube.
    static double Grumo(int[] celdas, int total)
    {
        double media = (double)total / celdas.Length, v = 0;
        for (int i = 0; i < celdas.Length; i++) { double d = celdas[i] - media; v += d * d; }
        return (v / celdas.Length) / media;
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
        FieldInfo fSize = TStar.GetField("Size", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
        // NADA QUE SE VEA PUEDE SER UNA BOLA REDONDA. Esta es la regla que sustituye a la de antes ("las
        // rayitas solo para diez o doce"). Con aquella, las ochenta y pico medianas se quedaban de disco
        // borroso con un punto dentro: puntos gordos, que es justo lo que se veia mal. Una bola redonda solo
        // pasa por estrella si es DIMINUTA; en cuanto se le nota el tamano tiene que tener silueta.
        int redondasGordas = 0, polvoGordo = 0;
        for (int i = 0; i < estrellas.Length; i++)
        {
            object st = estrellas.GetValue(i);
            int k = Convert.ToInt32(fKind.GetValue(st));
            int tam = Convert.ToInt32(fSize.GetValue(st));
            if (k != 0 && !(bool)fRay.GetValue(st)) redondasGordas++;
            if (k == 0 && tam > 1) polvoGordo++;
        }
        Check(redondasGordas == 0, "ninguna estrella que se vea es una bola redonda (" + redondasGordas + " lo son)");
        Check(polvoGordo == 0, "y lo unico redondo que queda -el polvo- mide UN pixel (" + polvoGordo + " miden mas)");
        // Y el freno de siempre: si las que llevan silueta pasaran de ser una minoria, el cielo dejaria de
        // ser un cielo y pareceria purpurina. Con el reparto de ahora salen alrededor del 13%.
        Check(rayos * 5 < estrellas.Length, "las que llevan silueta siguen siendo una minoria ("
              + rayos + " de " + estrellas.Length + ")");
        Check(verdes == 0, "ninguna estrella es verde ni violeta: el color va de azul-blanco a naranja");

        // REPARTIDAS POR IGUAL POR TODO EL FONDO, Y SIN HILERAS EN LOS BORDES.
        //
        // Aqui habia antes lo contrario: se exigia que las estrellas SE AGRUPARAN. Y agrupadas estaban, pero
        // demasiado y por el motivo equivocado. Dos cosas iban mal y las dos se veian:
        //
        //   - La estrella que caia FUERA de la pantalla se pegaba al borde en vez de volver a tirarse. Como
        //     los grumos son campanas, muchas caian fuera, y acababan TODAS en la misma linea: el 15% del
        //     cielo formaba cuatro hileras pegadas a los cantos.
        //   - El tamano del grumo iba en fraccion de la pantalla, asi que en una pantalla ancha salia
        //     estirado a lo ancho: bandas horizontales en vez de grumos.
        //
        // Por eso ahora se mide EN VARIAS FORMAS DE PANTALLA, incluida una ancha: los dos fallos se disparan
        // justo ahi. Se comprueban dos cosas por cada una:
        //
        //   1) Cuantas acaban en la orilla. Si el reparto es parejo tienen que ser las que toquen por area
        //      -la franja del 1% de cada lado es el 4% de la pantalla-, ni una mas.
        //   2) Cuanto se parecen entre si los trozos del cielo: se parte en una rejilla y se compara lo
        //      llena que esta cada casilla. Un reparto al azar da 1. Muy por encima significa amontonadas;
        //      muy por debajo significa espolvoreadas con regularidad de papel pintado, que tampoco vale.
        Console.WriteLine();
        Console.WriteLine("     {0,-12} {1,8} {2,10} {3,10}", "pantalla", "estrel.", "en orilla", "grumos");
        int[,] formas = { { 1024, 768 }, { 1920, 1080 }, { 2560, 1080 } };
        int malOrilla = 0, malGrumo = 0;
        double grumoAzar = 0;
        for (int f = 0; f < formas.GetLength(0); f++)
        {
            int fw = formas[f, 0], fh = formas[f, 1];
            mEnsure.Invoke(sf, new object[] { fw, fh });
            Array es2 = (Array)fStars.GetValue(sf);
            int n2 = es2.Length, orilla = 0;
            int CX = 16, CY = 10;
            int[] celdas = new int[CX * CY];
            for (int i = 0; i < n2; i++)
            {
                object st = es2.GetValue(i);
                float x = Convert.ToSingle(fX.GetValue(st)), y = Convert.ToSingle(fY.GetValue(st));
                if (x <= 0.01f || x >= 0.99f || y <= 0.01f || y >= 0.99f) orilla++;
                int cx = (int)(x * CX); if (cx < 0) cx = 0; if (cx >= CX) cx = CX - 1;
                int cy = (int)(y * CY); if (cy < 0) cy = 0; if (cy >= CY) cy = CY - 1;
                celdas[cy * CX + cx]++;
            }
            double grumo = Grumo(celdas, n2);
            // Para no fiarse de que el "1" del reparto al azar sea el que dice la teoria, se mide de verdad:
            // se tiran las MISMAS estrellas al azar sobre la misma rejilla y se saca el mismo numero.
            int[] control = new int[CX * CY];
            Random rc = new Random(1234);
            for (int i = 0; i < n2; i++) control[rc.Next(CY) * CX + rc.Next(CX)]++;
            grumoAzar = Grumo(control, n2);
            Console.WriteLine("     {0,-12} {1,8} {2,10} {3,10}", fw + "x" + fh, n2,
                              (100.0 * orilla / n2).ToString("0.0") + "%", grumo.ToString("0.00"));
            if (orilla * 100 > n2 * 5) malOrilla++;                 // mas del 5%: hay hilera pegada al canto
            if (grumo > 2.0 || grumo < 0.5) malGrumo++;             // ni amontonadas ni papel pintado
        }
        Console.WriteLine("     (las mismas estrellas tiradas al azar dan " + grumoAzar.ToString("0.00") + " de grumos)");
        Check(malOrilla == 0, "no se amontonan en los bordes: en la orilla hay las que tocan por area, no mas");
        Check(malGrumo == 0, "y quedan repartidas por igual: ni amontonadas ni con regularidad de papel pintado");
        mEnsure.Invoke(sf, new object[] { 1280, 800 });   // se deja como estaba para lo que viene despues

        Console.WriteLine("\n  2) UN CIELO CASI BLANCO, CON UNAS POCAS DE COLOR\n");
        Console.WriteLine("     con color de verdad:  " + grandesColor + " de " + grandes + " grandes,  "
                          + polvoColor + " de " + polvo + " de polvo");
        // Aqui habia antes un "el color fuerte se lo quedan las grandes" que NUNCA fue verdad: el color se
        // sortea igual para todas, sin mirar el tamano, asi que las grandes y el polvo salen al mismo
        // porcentaje (32% y 30% en la ultima medida). Pasaba por suerte, porque grandes hay veinticinco y con
        // tan pocas el porcentaje baila. Lo que el codigo SI garantiza es la paleta por temperatura: la
        // inmensa mayoria del cielo casi blanco y solo una minoria tirando a azul o naranja. Eso se mide
        // sobre el cielo entero, donde hay bastantes para que el numero no baile.
        int conColorTot = grandesColor + polvoColor;
        Check(conColorTot * 5 < estrellas.Length * 2, "la mayoria del cielo es casi blanco: solo "
              + conColorTot + " de " + estrellas.Length + " llevan color de verdad");
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

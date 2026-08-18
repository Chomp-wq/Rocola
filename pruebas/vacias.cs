using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// LAS LETRAS QUE NO TIENEN NADA DETRAS NO SE DIBUJAN.
//
// La barra del abecedario llevaba SIEMPRE las 29 casillas -estrella, #, la A a la Z y video-, tuvieran algo
// dentro o no. Las vacias salian apagadas: ocupaban su sitio en la pantalla para no decir nada, y de paso le
// robaban ancho a las que si tenian discos. En una barra de 486 px (la mitad de la pantalla, que es lo que
// queda cuando hay un video puesto al lado) eso son 16.8 px por casilla, y con tan poco hueco las letras
// tienen que encogerse hasta casi no leerse.
//
// Aqui se comprueban las dos cosas:
//   1) que la rocola le entrega a la barra SOLO las letras con contenido (se llama a RecalculaLetras de
//      verdad, con una biblioteca a medias, y se mira que letras quedaron);
//   2) que eso se NOTA en la pantalla: se dibuja la barra de verdad y se cuentan los grupos de tinta -o sea
//      las letras que se ven- y lo que mide cada una.
//
// Como se separa el texto del riel: la barra se dibuja sobre un lienzo transparente y se mira el ALFA. El
// riel de vidrio y su brillo no pasan de 154; el nucleo de una letra llega a 255. El corte en 190 los
// separa sin discusion (el mismo que usa la bateria 'abc', medido con sonda.cs).
class Vacias
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const int ALTO = 40;
    const int CORTE = 190;   // alfa a partir del cual es TEXTO y no riel

    static Type TF, TLet;
    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }
    static void Pon(Type t, object o, string campo, object v)
    {
        FieldInfo f = t.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null) throw new Exception("no existe el campo " + campo);
        f.SetValue(o, v);
    }
    static object Lee(Type t, object o, string campo)
    {
        FieldInfo f = t.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return (f == null) ? null : f.GetValue(o);
    }

    // La barra DE VERDAD, dibujada sobre transparente. La casilla ELEGIDA se deja en un caracter que no
    // esta en ninguna de las dos tiras: su capsula de neon tiene un contorno que tambien pasa de 190 de
    // alfa y se contaria como si fuera tinta de una letra (de hecho asi salio la primera vez: 31 manchones
    // donde hay 29 letras, porque los dos bordes de la capsula del '#' contaban aparte).
    static Bitmap Dibuja(string letras, HashSet<char> disp, int ancho)
    {
        Control bar = (Control)Activator.CreateInstance(TLet, true);
        bar.SetBounds(0, 0, ancho, ALTO);
        bar.Font = new Font(Fam(), 9f);
        Pon(TLet, bar, "cur", '\0');
        Pon(TLet, bar, "Letters", letras);
        Pon(TLet, bar, "Available", disp);
        Pon(TLet, bar, "Counts", null);
        Pon(TLet, bar, "Tints", null);
        Pon(TLet, bar, "Phase", 0.6d);
        Bitmap bmp = new Bitmap(ancho, ALTO, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TLet.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(bar, new object[] { g });
        }
        return bmp;
    }

    // En cuantas CASILLAS hay algo pintado. La barra reparte su ancho en tantas casillas como letras
    // lleve, asi que esto dice cuantas de ellas se estan usando para ensenar algo.
    static int CasillasConTinta(Bitmap b, int n)
    {
        float slot = (float)b.Width / n;
        int usadas = 0;
        for (int i = 0; i < n; i++)
        {
            int x0 = (int)(i * slot), x1 = (int)((i + 1) * slot);
            bool tinta = false;
            for (int x = x0; x < x1 && x < b.Width && !tinta; x++)
                for (int y = 0; y < b.Height && !tinta; y++)
                    if (b.GetPixel(x, y).A >= CORTE) tinta = true;
            if (tinta) usadas++;
        }
        return usadas;
    }

    // LO ANCHA QUE ES CADA CASILLA, medido en la pantalla y sin saber nada de como se reparte la barra por
    // dentro: se localizan los manchones de tinta, se saca el centro de cada uno y se mira cuanto hay del
    // centro de uno al del siguiente. Se toma la MEDIANA porque alguna palabra -"VIDEO" en la casilla de los
    // videos, cuando cabe entera- se parte en varios manchones y sus huecos son mucho mas chicos.
    static float SeparacionEntreCasillas(Bitmap b)
    {
        int w = b.Width, h = b.Height;
        List<float> centros = new List<float>();
        long sx = 0; int np = 0, hueco = 0; bool dentro = false;
        for (int x = 0; x <= w; x++)
        {
            int enCol = 0;
            if (x < w) for (int y = 0; y < h; y++) if (b.GetPixel(x, y).A >= CORTE) enCol++;
            if (enCol > 0) { if (!dentro) { dentro = true; sx = 0; np = 0; } sx += (long)x * enCol; np += enCol; hueco = 0; }
            else if (dentro) { hueco++; if (hueco >= 2 || x == w) { dentro = false; if (np > 0) centros.Add((float)sx / np); } }
        }
        if (centros.Count < 2) return 0f;
        List<float> huecos = new List<float>();
        for (int i = 1; i < centros.Count; i++) huecos.Add(centros[i] - centros[i - 1]);
        huecos.Sort();
        return huecos[huecos.Count / 2];
    }

    // Lo que mide DE ALTO una letra concreta. Se mira solo dentro de SU casilla, que es la que le toca segun
    // en que puesto de la tira este: comparar el manchon mas alto de toda la barra no valdria, porque la
    // estrella y el triangulo del video son mas altos que una letra y solo estan en una de las dos tiras.
    static int AltoDe(Bitmap b, string letras, char c)
    {
        int i = letras.IndexOf(c);
        if (i < 0) return 0;
        float slot = (float)b.Width / letras.Length;
        int x0 = (int)(i * slot), x1 = (int)((i + 1) * slot);
        int arriba = b.Height, abajo = -1;
        for (int x = x0; x < x1 && x < b.Width; x++)
            for (int y = 0; y < b.Height; y++)
                if (b.GetPixel(x, y).A >= CORTE) { if (y < arriba) arriba = y; if (y > abajo) abajo = y; }
        return (abajo >= arriba) ? (abajo - arriba + 1) : 0;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TLet = TF.GetNestedType("LetterBar", BindingFlags.NonPublic | BindingFlags.Public);
        Type TAlb = TF.GetNestedType("Album", BindingFlags.NonPublic | BindingFlags.Public);

        // Una biblioteca a medias: solo hay discos en estas ocho letras. Ni estrella (lo mas sonado) ni
        // video ni '#': tampoco tienen nada.
        const string HAY = "ABCJMRSZ";
        const string TODAS = "★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";

        Console.WriteLine();
        Console.WriteLine("  UNA BIBLIOTECA CON DISCOS EN OCHO LETRAS (" + HAY + ")");
        Console.WriteLine();

        // ---------- 1) LO QUE LA ROCOLA LE ENTREGA A LA BARRA ----------
        // Se arma un Form1 vacio (sin constructor: no queremos ventanas ni relojes) y se le ponen a mano las
        // cuatro cosas que RecalculaLetras mira. Luego se le pide que recalcule, como hace al terminar de
        // leer la biblioteca, y se mira con que tira de letras se quedo la barra.
        object f = FormatterServices.GetUninitializedObject(TF);
        Control barra = (Control)Activator.CreateInstance(TLet, true);
        barra.SetBounds(0, 0, 996, ALTO);
        barra.Font = new Font(Fam(), 9f);
        Pon(TF, f, "letterBar", barra);
        Pon(TF, f, "libBuilding", false);
        IDictionary porLetra = (IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(char), typeof(List<>).MakeGenericType(TAlb)));
        IList unos = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        unos.Add(Activator.CreateInstance(TAlb, true));
        HashSet<char> hay = new HashSet<char>();
        foreach (char c in HAY) { porLetra[c] = unos; hay.Add(c); }
        Pon(TF, f, "byLetter", porLetra);
        Pon(TF, f, "letterTag", Activator.CreateInstance(typeof(Dictionary<char, string>)));
        Pon(TF, f, "letterAvailable", hay);

        MethodInfo mRec = TF.GetMethod("RecalculaLetras", BindingFlags.Instance | BindingFlags.NonPublic);
        try { mRec.Invoke(f, null); }
        catch (Exception ex) { Console.WriteLine("  no se pudo llamar a RecalculaLetras: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message)); return 1; }

        string tira = (string)Lee(TLet, barra, "Letters");
        Console.WriteLine("  la barra se quedo con:  \"" + tira + "\"   (" + tira.Length + " casillas)");
        Console.WriteLine("  antes llevaba siempre:  \"" + TODAS + "\"   (" + TODAS.Length + " casillas)");
        Console.WriteLine();
        Check(tira == HAY, "solo van las letras con discos, y en el orden de siempre");

        // Y en cuanto aparece contenido nuevo -por ejemplo la seccion de videos- su casilla tiene que
        // volver a la barra sola, sin reiniciar nada.
        hay.Add('►');
        mRec.Invoke(f, null);
        string tira2 = (string)Lee(TLet, barra, "Letters");
        Check(tira2 == HAY + "►", "al aparecer los videos, su casilla vuelve a la barra");
        hay.Remove('►');
        mRec.Invoke(f, null);

        // ---------- 2) QUE ESO SE VEA ----------
        Console.WriteLine();
        Console.WriteLine("  {0,-32} {1,9} {2,9} {3,11} {4,10}", "la barra dibujada", "casillas", "con algo", "de ancho", "la M mide");
        Console.WriteLine("  " + new string('-', 76));
        int[] anchos = { 996, 486 };
        string[] comoSeLlama = { "pantalla entera (996 px)", "con un video al lado (486 px)" };
        int[] altoAntes = new int[anchos.Length];
        int[] altoAhora = new int[anchos.Length];
        for (int k = 0; k < anchos.Length; k++)
        {
            int gA, hA, gN, hN; float sA, sN;
            using (Bitmap b = Dibuja(TODAS, hay, anchos[k])) { gA = CasillasConTinta(b, TODAS.Length); hA = AltoDe(b, TODAS, 'M'); sA = SeparacionEntreCasillas(b); }
            using (Bitmap b = Dibuja(tira, hay, anchos[k])) { gN = CasillasConTinta(b, tira.Length); hN = AltoDe(b, tira, 'M'); sN = SeparacionEntreCasillas(b); }
            altoAntes[k] = hA; altoAhora[k] = hN;
            Console.WriteLine("  {0,-32} {1,9} {2,9} {3,11} {4,10}", comoSeLlama[k] + "  antes", TODAS.Length, gA, sA.ToString("0.0") + " px", hA + " px");
            Console.WriteLine("  {0,-32} {1,9} {2,9} {3,11} {4,10}", comoSeLlama[k] + "  ahora", tira.Length, gN, sN.ToString("0.0") + " px", hN + " px");
            Check(gA == TODAS.Length, "antes se pintaban las " + TODAS.Length + " casillas a " + anchos[k] + " px, hubiera o no discos (se pintan " + gA + ")");
            Check(gN == tira.Length, "ahora se pintan solo las " + tira.Length + " que tienen discos (se pintan " + gN + ")");
            Check(sN > sA * 2f, "y cada casilla es mas ancha: " + sA.ToString("0.0") + " px -> " + sN.ToString("0.0") + " px");
        }

        // Con la barra estrecha, el cuerpo de la letra lo limitaba el ancho de la casilla: al quitar las
        // vacias, cada una tiene mas hueco y la letra crece de verdad. (Con la pantalla entera ya cabia
        // holgada, asi que ahi lo que crece es la casilla, no la letra.)
        Console.WriteLine();
        Check(altoAhora[1] > altoAntes[1],
              "con un video al lado, la letra crece: " + altoAntes[1] + " px -> " + altoAhora[1] + " px");
        Check(altoAhora[0] >= altoAntes[0],
              "con la pantalla entera la letra no encoge: " + altoAntes[0] + " px -> " + altoAhora[0] + " px");

        // Y el caso feo: una biblioteca vacia (o a medio leer) no puede dejar la barra SIN casillas, porque
        // el ancho de casilla se saca dividiendo entre cuantas hay.
        hay.Clear();
        mRec.Invoke(f, null);
        string tira0 = (string)Lee(TLet, barra, "Letters");
        Check(tira0 != null && tira0.Length > 0, "con la biblioteca vacia la barra no se queda sin casillas (lleva \"" + tira0 + "\")");
        using (Bitmap b = Dibuja(tira0, hay, 996)) Check(CasillasConTinta(b, tira0.Length) == tira0.Length, "y se dibuja sin reventar");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

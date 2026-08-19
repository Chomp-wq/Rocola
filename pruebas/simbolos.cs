using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// LOS SIMBOLOS DE LA BARRA SE TIENEN QUE VER EN LA MAQUINA DEL BAR.
//
// En la barra de letras, la primera casilla -LO MAS SONADO- no lleva una letra: lleva una estrella. Y en la
// rocola del bar, esa estrella no se veia. En su sitio salia un rectangulo vacio.
//
// El motivo es que la estrella no se DIBUJABA: se ESCRIBIA, como se escribe una A, pidiendole a la letra de
// Windows el simbolo "estrella negra". La rocola pide la letra Segoe UI, que en Windows XP no existe, y
// Windows le da Tahoma. Y Tahoma de XP trae el juego de simbolos WGL4, donde la estrella NO esta. Cuando a
// Windows le piden un simbolo que no tiene, pinta un rectangulo vacio: eso es lo que se veia en el bar. En la
// otra rocola si se veia, porque ahi la letra si lo trae, y por eso no se noto.
//
// Lo mismo pasaba con el triangulito de "SONANDO" del buscador: habia dos triangulos casi identicos y estaba
// usado el que NO esta en WGL4.
//
// La solucion es no depender de eso: la estrella se dibuja con lineas, como ya se dibujan las capsulas y el
// riel de la barra. Y se comprueba dibujando LA BARRA DOS VECES con dos letras de Windows cuyas estrellas se
// ven distintas, y mirando que la casilla de LO MAS SONADO salga IGUAL con las dos: si saliera distinta es
// que la estrella sigue viniendo de la letra de Windows, y entonces en la maquina que no la trae seguiria
// saliendo el rectangulo.
//
// (Aqui no se puede reproducir el rectangulo tal cual: estas pruebas corren en Linux, que cuando le falta un
// simbolo lo va a buscar a otra letra en vez de pintar el hueco. Por eso la prueba no mira "sale el
// rectangulo", que aqui no saldria nunca, sino la causa: que el dibujo dependa de la letra que toque.)
class Simbolos
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TLet;
    const int W = 1024, H = 44;
    const string LETRAS = "\u2605#ABCDEFGHIJKLMNOPQRSTUVWXYZ\u25BA";
    const char ESTRELLA = '\u2605';

    // Dos letras de Windows cuyas estrellas se ven CLARAMENTE distintas. Con eso basta: si el dibujo de la
    // barra cambia al cambiar de letra, es que la estrella se la esta pidiendo a la letra.
    static readonly string[] UNA = { "DejaVu Sans", "Liberation Sans", "Tahoma", "Verdana" };
    static readonly string[] OTRA = { "FreeSerif", "Unifont", "WenQuanYi Zen Hei", "IPAGothic", "Times New Roman" };

    static FieldInfo Campo(string n) { return TLet.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); }
    static void Pon(object o, string n, object v) { FieldInfo f = Campo(n); if (f != null) f.SetValue(o, v); }

    static string Elige(string[] cuales)
    {
        for (int i = 0; i < cuales.Length; i++)
            try { FontFamily f = new FontFamily(cuales[i]); f.Dispose(); return cuales[i]; } catch { }
        return null;
    }

    static float slotAncho, bandaAlto;

    // Cuantos pixeles ocupa la estrella ESCRITA con esa letra de Windows (nada que ver con la rocola:
    // es solo para saber si las dos letras la dibujan distinta).
    static int TintaDelSimbolo(string familia)
    {
        int n = 0;
        using (FontFamily ff = new FontFamily(familia))
        using (Bitmap b = new Bitmap(48, 48, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(b))
        using (Font f = new Font(ff, 22f, FontStyle.Bold))
        using (StringFormat sf = new StringFormat())
        {
            g.Clear(Color.Black);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
            g.DrawString(ESTRELLA.ToString(), f, Brushes.White, new RectangleF(0, 0, 48, 48), sf);
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 48; x++)
                    if ((b.GetPixel(x, y).ToArgb() & 0xFFFFFF) != 0) n++;
        }
        return n;
    }

    // Dibuja la barra entera. 'hueca' quita la estrella de la primera casilla dejandola vacia, para poder
    // restar los dos dibujos y quedarnos con la tinta de la estrella y nada mas.
    static Bitmap Pinta(string familia, char actual, bool paraGpu, bool hueca, bool topDisponible)
    {
        Control lb = (Control)Activator.CreateInstance(TLet, true);
        lb.Font = new Font(new FontFamily(familia), 9f);
        lb.SetBounds(0, 0, W, H);

        string letras = hueca ? (" " + LETRAS.Substring(1)) : LETRAS;
        HashSet<char> hay = new HashSet<char>();
        foreach (char c in "#ABCDEFGHIJKLM") hay.Add(c);
        if (!hueca && topDisponible) hay.Add(ESTRELLA);
        Dictionary<char, int> cuentas = new Dictionary<char, int>();
        int k = 0; foreach (char c in letras) cuentas[c] = 300 + (++k) * 37;

        Pon(lb, "Letters", letras); Pon(lb, "Available", hay); Pon(lb, "Counts", cuentas);
        Pon(lb, "cur", actual); Pon(lb, "Phase", 0.6d);

        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(255, 10, 14, 40));
            TLet.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) })
                .Invoke(lb, new object[] { g, paraGpu });
        }
        if (slotAncho <= 0f)
        {
            slotAncho = (float)W / letras.Length;
            MethodInfo mAlto = TLet.GetMethod("AltoCuenta", BindingFlags.Instance | BindingFlags.NonPublic);
            bandaAlto = H - (float)mAlto.Invoke(lb, new object[] { slotAncho, H });
        }
        lb.Dispose();
        return b;
    }

    // Cuantos pixeles cambian entre dos dibujos dentro de la casilla numero 'casilla' (solo la banda de
    // arriba, donde va el simbolo: debajo van los numeros, que si dependen de la letra de Windows).
    static int Distintos(Bitmap a, Bitmap b, int casilla, bool[,] marca)
    {
        int x0 = (int)(casilla * slotAncho), x1 = (int)((casilla + 1) * slotAncho) - 1;
        int y1 = (int)bandaAlto;
        int n = 0;
        for (int y = 0; y < y1; y++)
            for (int x = x0; x <= x1 && x < W; x++)
                if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb())
                { n++; if (marca != null) marca[x, y] = true; }
        return n;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TLet = TF.GetNestedType("LetterBar", BindingFlags.NonPublic | BindingFlags.Public);

        Console.WriteLine("\n  1) HAY CON QUE PROBARLO\n");
        Check(TLet != null, "esta la barra de letras");
        if (TLet == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }
        string conEstr = Elige(UNA), sinEstr = Elige(OTRA);
        Check(conEstr != null && sinEstr != null, "hay dos letras de Windows con las que probar");
        if (conEstr == null || sinEstr == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }
        Console.WriteLine("     se prueba con " + conEstr + " y con " + sinEstr);

        // ESTO ES LO QUE LE DA DIENTES A LA PRUEBA. Se escribe la estrella a pelo con las dos letras: si las
        // dos la dibujaran igual, lo de mas abajo saldria bien aunque no se hubiera arreglado nada.
        int aTinta = TintaDelSimbolo(conEstr), bTinta = TintaDelSimbolo(sinEstr);
        Console.WriteLine("     escrita a pelo, la estrella ocupa " + aTinta + " y " + bTinta + " pixeles segun la letra");
        Check(Math.Abs(aTinta - bTinta) > 15,
              "las dos escriben la estrella de forma bien distinta (si no, esta prueba no probaria nada)");

        using (Bitmap a = Pinta(conEstr, 'C', false, false, true))
        using (Bitmap b = Pinta(sinEstr, 'C', false, false, true))
        {
            int dif = Distintos(a, b, 6, null);   // casilla de una letra normal
            Console.WriteLine("     y en una casilla de letra normal cambian " + dif + " pixeles al cambiar de letra");
            Check(dif > 40, "y las letras normales tambien salen distintas, como tiene que ser");
        }

        // =========================================================================================
        Console.WriteLine("\n  2) LA ESTRELLA SALE IGUAL AUNQUE LA LETRA DE WINDOWS NO LA TENGA\n");
        // Esto es el fallo del bar, tal cual: la de la izquierda es la maquina que la trae y la de la
        // derecha la que no. Antes la de la derecha pintaba el rectangulo vacio.
        string[] queCaso = { "sin elegir, pulsando sola", "elegida, con su capsula dorada", "apagada (todavia no hay estadisticas)" };
        char[] actualDe = { 'C', ESTRELLA, 'C' };
        bool[] topHay = { true, true, false };
        for (int c = 0; c < 3; c++)
        {
            using (Bitmap a = Pinta(conEstr, actualDe[c], false, false, topHay[c]))
            using (Bitmap b = Pinta(sinEstr, actualDe[c], false, false, topHay[c]))
            {
                int dif = Distintos(a, b, 0, null);
                Console.WriteLine("     " + queCaso[c] + ": " + dif + " pixeles de diferencia");
                Check(dif == 0, "la estrella " + queCaso[c] + " se ve IGUAL con las dos");
            }
        }

        // Y lo mismo en la barra que se hornea para la tarjeta grafica.
        using (Bitmap a = Pinta(conEstr, 'C', true, false, true))
        using (Bitmap b = Pinta(sinEstr, 'C', true, false, true))
        {
            int dif = Distintos(a, b, 0, null);
            Console.WriteLine("     en la barra horneada para tarjeta grafica: " + dif + " pixeles de diferencia");
            Check(dif == 0, "y tambien sale igual en la barra que se hornea para la tarjeta grafica");
        }

        // =========================================================================================
        Console.WriteLine("\n  3) Y LO QUE SE VE ES UNA ESTRELLA, NO UN RECTANGULO NI UN HUECO\n");
        // Restando el dibujo con estrella y el dibujo con la casilla vacia queda la tinta de la estrella y
        // nada mas. Una estrella tiene el centro lleno y las cuatro esquinas de su recuadro vacias; el
        // rectangulo de Windows es justo al reves.
        bool[,] marca = new bool[W, H];
        int tinta;
        using (Bitmap a = Pinta(sinEstr, 'C', false, false, true))
        using (Bitmap b = Pinta(sinEstr, 'C', false, true, true))
            tinta = Distintos(a, b, 0, marca);
        Console.WriteLine("     la estrella ocupa " + tinta + " pixeles");
        Check(tinta > 120, "en la casilla de LO MAS SONADO se dibuja algo (y no es poca cosa)");

        int mx0 = W, my0 = H, mx1 = -1, my1 = -1;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (marca[x, y]) { if (x < mx0) mx0 = x; if (x > mx1) mx1 = x; if (y < my0) my0 = y; if (y > my1) my1 = y; }
        int an = mx1 - mx0 + 1, al = my1 - my0 + 1;
        Console.WriteLine("     y su recuadro mide " + an + "x" + al + " pixeles, o sea el "
                          + (al * 100 / bandaAlto).ToString("0") + "% del alto de su casilla");
        Check(an > 8 && al > 8, "y ocupa un buen trozo de su casilla, no cuatro pixeles sueltos");
        // Y el tamano de siempre: ni una mota que no se vea desde la barra del bar, ni tan grande que se
        // salga de su casilla y toque el riel.
        Check(al > bandaAlto * 0.40f && al < bandaAlto * 0.72f, "y es del tamano de siempre dentro de su casilla");

        int lado = Math.Max(2, Math.Min(an, al) * 16 / 100);
        double centro = Trozo(marca, mx0 + (an - lado) / 2, my0 + (al - lado) / 2, lado, lado);
        double e1 = Trozo(marca, mx0, my0, lado, lado);
        double e2 = Trozo(marca, mx1 - lado + 1, my0, lado, lado);
        double e3 = Trozo(marca, mx0, my1 - lado + 1, lado, lado);
        double e4 = Trozo(marca, mx1 - lado + 1, my1 - lado + 1, lado, lado);
        double peor = Math.Max(Math.Max(e1, e2), Math.Max(e3, e4));
        Console.WriteLine("     el centro esta lleno al " + (centro * 100).ToString("0") + "% y la esquina mas llena al "
                          + (peor * 100).ToString("0") + "%");
        Check(centro > 0.85, "tiene el centro macizo (el rectangulo de Windows lo tiene hueco)");
        Check(peor < 0.10, "y las esquinas de su recuadro vacias (el rectangulo de Windows las tiene pintadas)");

        // =========================================================================================
        Console.WriteLine("\n  4) Y NO QUEDA NINGUN OTRO SIMBOLO QUE XP NO SEPA DIBUJAR\n");
        Check(TLet.GetField("fTop", BindingFlags.Instance | BindingFlags.NonPublic) == null,
              "la barra ya ni siquiera guarda una letra de Windows para la estrella");
        string fuente = Path.Combine("..", Path.Combine("Rocola", "Form1.cs"));
        string txt = File.Exists(fuente) ? File.ReadAllText(fuente) : "";
        Check(txt.Length > 0, "se puede leer el codigo de la rocola para repasarlo");
        int malo = Cuenta(txt, "\\u25B6") + Cuenta(txt, "\\u25b6");
        Console.WriteLine("     triangulos de los que XP no sabe dibujar: " + malo);
        Check(malo == 0, "el triangulito de SONANDO es el que XP SI sabe dibujar (hay dos casi iguales)");
        int escrita = Cuenta(txt, "DrawString(\"\\u2605\"");
        Console.WriteLine("     veces que la estrella se escribe en vez de dibujarse: " + escrita);
        Check(escrita == 0, "y la estrella ya no se le pide a Windows en ningun sitio");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }

    static double Trozo(bool[,] m, int x, int y, int an, int al)
    {
        int n = 0, t = 0;
        for (int j = y; j < y + al; j++)
            for (int i = x; i < x + an; i++)
            { if (i < 0 || j < 0 || i >= W || j >= H) continue; t++; if (m[i, j]) n++; }
        return (t == 0) ? 0.0 : (double)n / t;
    }

    static int Cuenta(string s, string q)
    {
        int n = 0, i = 0;
        for (; ; ) { i = s.IndexOf(q, i, StringComparison.Ordinal); if (i < 0) break; n++; i += q.Length; }
        return n;
    }
}

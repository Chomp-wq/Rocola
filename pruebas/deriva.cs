using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Threading;

// EL FONDO DE ESTRELLAS TIENE QUE DESLIZARSE LISO MIENTRAS SUENA UNA CANCION.
//
// Al sonar musica, el campo de estrellas entero se desliza despacio en diagonal y se envuelve por los bordes:
// eso es lo que hace que el fondo parezca vivo y sin final. En el bar no se veia asi. Se quedaba quieto y de
// vez en cuando pegaba un salto, a tirones.
//
// Y no era que fuese lento: es que EN LA PANTALLA PRINCIPAL NO SE MOVIA NADA. La cuenta de la deriva estaba
// escrita y hasta comentada -"en la vista de discos solo desplaza el blit de la capa de estrellas"- pero el
// dibujado de esa vista no la usaba en ninguna parte, y ademas solo se avanzaba desde el dibujado del modo de
// reposo, que es otro campo distinto. O sea: en la pantalla que esta encendida mientras suena la musica, la
// deriva no avanzaba ni se aplicaba. Lo unico que se movia era el titileo, que va cambiando de cuadro cinco
// veces por segundo: quieto doscientas milesimas, salto, quieto otras doscientas. Exactamente lo que se veia.
//
// Aqui se comprueban las dos mitades: que la deriva avanza con el RELOJ -no con cuantas veces se dibuje, que
// es lo que la haria ir a tirones- y que el fondo dibujado de verdad se corre con ella, envolviendo por los
// bordes y sin costura.
class Deriva
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TSky;
    static object cielo;
    static FieldInfo Campo(string n) { return TSky.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); }
    static float Flo(string n) { return (float)Campo(n).GetValue(cielo); }
    static void Pon(string n, object v) { Campo(n).SetValue(cielo, v); }

    const int W = 320, H = 240;

    // Dibuja SOLO las estrellas con la deriva que se le diga. El cielo -el degradado, la via lactea y las
    // nebulosas- se queda quieto a proposito: eso esta lejisimos y no se desliza. Lo que tiene que deslizarse
    // es el campo de estrellas, y es lo que se mira aqui.
    static Bitmap Pinta(float dx, float dy)
    {
        Pon("driftX", dx); Pon("driftY", dy);
        FieldInfo fCapas = Campo("layers");
        Bitmap[] capas = (Bitmap[])fCapas.GetValue(cielo);
        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.Black);
            MethodInfo mBlit = TSky.GetMethod("BlitEstrellas", BindingFlags.Instance | BindingFlags.NonPublic);
            mBlit.Invoke(cielo, new object[] { g, capas[0], W, H, 0, 0 });
        }
        return b;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TSky = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);
        Console.WriteLine("\n  1) LA DERIVA AVANZA CON EL RELOJ, NO CON LOS DIBUJOS\n");
        Check(TSky != null, "esta el campo de estrellas");
        if (TSky == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }
        MethodInfo mAv = TSky.GetMethod("AvanzaDeriva", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(mAv != null, "y la deriva se puede avanzar aparte del dibujado (antes solo avanzaba al dibujar)");
        if (mAv == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        cielo = Activator.CreateInstance(TSky, true);
        Pon("SkyFullW", W); Pon("SkyFullH", H); Pon("SkyOffsetX", 0); Pon("SkyOffsetY", 0);
        Pon("Dim", 1f);
        // El cielo, de un solo color: asi lo unico que puede cambiar entre dos dibujos son las estrellas.
        Color liso = Color.FromArgb(255, 10, 12, 30);
        TSky.GetMethod("DrawCached").Invoke(cielo, new object[] { Graphics.FromImage(new Bitmap(W, H)), W, H, liso, liso });

        // --- avanza igual aunque se le llame a ritmos MUY distintos ---
        // Este es el corazon del asunto: si la deriva dependiera de cuantas veces se la llama, con la maquina
        // ocupada -que es cuando bajan los cuadros- el fondo se quedaria atras y luego pegaria el salto.
        double[] recorrido = new double[2];
        for (int modo = 0; modo < 2; modo++)
        {
            Pon("driftX", 0f); Pon("driftY", 0f); Pon("driftEnv", 1f);
            Campo("driftLastMs").SetValue(cielo, 0);
            mAv.Invoke(cielo, new object[] { 1f });          // primera llamada: solo pone el reloj en marcha
            int veces = (modo == 0) ? 40 : 4;                // 40 dibujos... o solo 4, como una maquina ahogada
            int espera = 400 / veces;
            for (int i = 0; i < veces; i++) { Thread.Sleep(espera); mAv.Invoke(cielo, new object[] { 1f }); }
            recorrido[modo] = Flo("driftX");
        }
        Console.WriteLine("     con 40 dibujos en 400 ms avanza " + recorrido[0].ToString("0.00") + " px");
        Console.WriteLine("     con  4 dibujos en 400 ms avanza " + recorrido[1].ToString("0.00") + " px");
        Check(recorrido[0] > 3.0, "con la musica sonando el campo avanza de verdad");
        double desvio = Math.Abs(recorrido[0] - recorrido[1]) / Math.Max(0.001, recorrido[0]);
        Check(desvio < 0.15, "y avanza LO MISMO se dibuje mucho o poco: por eso va liso y no a tirones");

        // --- sin musica, quieto ---
        Pon("driftX", 0f); Pon("driftY", 0f); Pon("driftEnv", 0f);
        Campo("driftLastMs").SetValue(cielo, 0);
        mAv.Invoke(cielo, new object[] { 0f });
        for (int i = 0; i < 20; i++) { Thread.Sleep(15); mAv.Invoke(cielo, new object[] { 0f }); }
        Console.WriteLine("     sin musica, en 300 ms avanza " + Flo("driftX").ToString("0.000") + " px");
        Check(Flo("driftX") < 0.01f, "y sin musica se queda quieto (el fondo solo respira cuando suena algo)");

        // =========================================================================================
        Console.WriteLine("\n  2) Y EL FONDO QUE SE DIBUJA SE CORRE DE VERDAD CON ELLA\n");
        // Esta es la mitad que faltaba: la cuenta existia pero el dibujado no la miraba.
        using (Bitmap a = Pinta(0f, 0f))
        using (Bitmap b = Pinta(37f, 21f))
        {
            int distintos = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) distintos++;
            Console.WriteLine("     al correr la deriva 37x21 px cambian " + distintos + " pixeles de " + (W * H));
            Check(distintos > W * H / 100, "mover la deriva MUEVE el fondo (antes no lo tocaba en absoluto)");

            // Y no se mueve de cualquier manera: tiene que ser el MISMO campo corrido, envolviendo por los
            // bordes. Si hubiera costura, borde negro o estrellas perdidas, esto no cuadraria.
            int malos = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int ox = ((x - 37) % W + W) % W, oy = ((y - 21) % H + H) % H;
                    if (a.GetPixel(ox, oy).ToArgb() != b.GetPixel(x, y).ToArgb()) malos++;
                }
            Console.WriteLine("     pixeles que no cuadran con el campo corrido: " + malos);
            Check(malos == 0, "y es el MISMO campo corrido, envolviendo por los bordes: ni costura ni huecos");
        }

        // Con la deriva justo encima del borde -el caso feo, cuando la ventana cae a caballo de la costura-
        // tiene que salir igual de bien.
        using (Bitmap a = Pinta(0f, 0f))
        using (Bitmap b = Pinta(W - 5f, H - 3f))
        {
            int malos = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int ox = ((x - (W - 5)) % W + W) % W, oy = ((y - (H - 3)) % H + H) % H;
                    if (a.GetPixel(ox, oy).ToArgb() != b.GetPixel(x, y).ToArgb()) malos++;
                }
            Console.WriteLine("     con la costura dentro de la pantalla, pixeles que no cuadran: " + malos);
            Check(malos == 0, "tambien cuadra con la costura metida en medio de la pantalla");
        }

        // =========================================================================================
        Console.WriteLine("\n  3) Y LA ROCOLA LA AVANZA UNA VEZ POR LATIDO\n");
        // Si nadie la llamara, todo lo de arriba estaria bien y el fondo seguiria clavado.
        bool laLlama = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (Codigo.Llama(m, "AvanzaDeriva")) laLlama = true;
        Check(laLlama, "la rocola avanza la deriva desde su latido de animacion, no desde el dibujado");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

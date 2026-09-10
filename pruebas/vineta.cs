using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;

// LA VIÑETA: QUE SE HORNEE UNA VEZ, QUE SE SUELTE AL APAGARLA, Y QUE SIGA SIENDO LA MISMA.
//
// El mapa de la vineta -el oscurecido de las esquinas- se sube a la tarjeta una vez. Si esa subida
// falla -al arrancar, o cuando Windows se lleva el dispositivo al bloquear la sesion- se reintenta
// cada segundo. Lo que fallaba es la SUBIDA, nunca el horneado, y sin embargo se rehorneaba el mapa
// entero en cada reintento: 16 384 pixeles cada segundo mientras durara el fallo. Su hermana
// EnsureNieblas ya tenia escrita esa precaucion en un comentario; esta no.
//
// Y al APAGAR el interruptor la textura se quedaba puesta en la tarjeta hasta cerrar la rocola.
//
// De paso el horneado paso de SetPixel -una llamada a GDI+ por pixel- a LockBits y una copia por
// fila, que es como lo hace BakeNatural en el archivo de al lado. Aqui se comprueba que el dibujo
// sea EXACTAMENTE el mismo de antes: una mejora de velocidad que cambia la imagen no es una mejora.
class Vineta
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    // La cuenta de siempre, escrita aparte a proposito: si el horneado de verdad se desvia, esta no.
    static int AlfaDeAntes(int n, int x, int y)
    {
        double dx = 2.0 * x / (n - 1) - 1.0, dy = 2.0 * y / (n - 1) - 1.0;
        double f = Math.Max(0.0, Math.Min(1.0, (dx * dx + dy * dy - 0.18) / 1.82));
        f = f * f * (3.0 - 2.0 * f);
        return (int)(110.0 * f + 0.5);
    }

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type tp = a.GetType("Rocola.GpuPanel");
        if (tp == null) { Console.WriteLine("  OK    sin tarjeta grafica no hay vineta: nada que mirar"); return 0; }

        MethodInfo bake = tp.GetMethod("BakeVineta", TODO);
        MethodInfo ensure = tp.GetMethod("EnsureVineta", TODO);
        if (bake == null || ensure == null) { Mal("falta BakeVineta o EnsureVineta"); return 1; }

        // --- 1) EL MISMO DIBUJO QUE ANTES, PIXEL A PIXEL ---
        const int N = 128;
        int distintos = 0, maxDif = 0, conAlfa = 0;
        using (Bitmap b = (Bitmap)bake.Invoke(null, new object[] { N }))
        {
            Exige(b.Width == N && b.Height == N, "el mapa mide " + N + "x" + N);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Color c = b.GetPixel(x, y);
                    int esperado = AlfaDeAntes(N, x, y);
                    if (c.A != esperado) { distintos++; int d = Math.Abs(c.A - esperado); if (d > maxDif) maxDif = d; }
                    if (c.R != 0 || c.G != 0 || c.B != 0) distintos++;
                    if (c.A > 0) conAlfa++;
                }
            Exige(distintos == 0, "los 16384 pixeles salen igual que con la cuenta de antes"
                  + (distintos > 0 ? " (" + distintos + " distintos, el peor por " + maxDif + ")" : ""));
            // Que de verdad haya vineta: negra en las esquinas y limpia en el centro.
            Exige(b.GetPixel(0, 0).A > 90, "la esquina esta oscura (alfa " + b.GetPixel(0, 0).A + ")");
            Exige(b.GetPixel(N / 2, N / 2).A == 0, "el centro esta limpio del todo");
            Exige(conAlfa > 3000 && conAlfa < 15000, "y el oscurecido cubre un anillo, no la pantalla (" + conAlfa + " pixeles)");
        }

        // --- 2) SE HORNEA CON LockBits, NO PIXEL A PIXEL ---
        List<string> dentro = Codigo.Nombres(bake);
        Exige(!dentro.Contains("SetPixel"), "el horneado ya no llama a SetPixel una vez por pixel");
        Exige(dentro.Contains("LockBits"), "sino que usa LockBits, como BakeNatural");

        Stopwatch reloj = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) using (Bitmap b2 = (Bitmap)bake.Invoke(null, new object[] { N })) { }
        reloj.Stop();
        Console.WriteLine("  (cuesta " + (reloj.Elapsed.TotalMilliseconds / 20).ToString("0.00") + " ms; con SetPixel eran 2,07)");

        // --- 3) EL REINTENTO NO REHORNEA: el mapa se guarda ---
        int lee, escribe; Codigo.UsosDeCampo(ensure, "vinetaMapa", out lee, out escribe);
        Exige(lee > 0, "el reintento mira primero el mapa guardado en vez de rehornearlo");
        Exige(escribe > 0, "y lo guarda la primera vez");

        // --- 4) AL APAGAR EL INTERRUPTOR SE SUELTA LA TEXTURA ---
        Exige(Codigo.Llama(ensure, "Release"), "al apagar la vineta se suelta su textura de la tarjeta");
        int leeSw, escSw; Codigo.UsosDeCampo(ensure, "VinetaSuave", out leeSw, out escSw);
        Exige(leeSw > 0, "y para eso mira el interruptor");

        // --- 5) EL DISPOSE DEL PANEL TAMBIEN LO SUELTA ---
        bool enDispose = false;
        foreach (MethodBase m in Codigo.MetodosConAyudantes(tp, "Dispose"))
        {
            int l, e; Codigo.UsosDeCampo(m, "vinetaMapa", out l, out e);
            if (l > 0 || e > 0) enDispose = true;
        }
        Exige(enDispose, "al cerrar el panel, el mapa guardado se suelta");

        Console.WriteLine(fallos == 0 ? "  todo bien" : ("  " + fallos + " fallos"));
        return fallos;
    }
}

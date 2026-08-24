using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// EL FONDO POR TARJETA GRAFICA, EN LINUX: QUE PINTE, Y QUE PINTE LO QUE ES.
//
// La bateria 'opengl' comprueba el CAMINO: que OpenGL se puede colgar de la ventana que hizo Mono. Esta
// comprueba el DIBUJO: se enciende el fondo de estrellas de verdad -el mismo GpuStarfield que usa la
// rocola-, se le pide un cuadro y se leen los pixeles que salieron.
//
// Lo que se mira son las tres cosas que se pueden traducir MAL de Direct3D a OpenGL sin que salte nada:
//
//   1) EL COLOR AL REVES. El color de cada vertice va guardado como 0xAARRGGBB, que en memoria queda
//      B,G,R,A. Si al pasarselo a OpenGL se le dice que lo lea de frente, el rojo y el azul quedan
//      cambiados: el cielo azul de la rocola saldria rojizo. Se pide un cielo ROJO y se comprueba que
//      sale rojo, que es la unica forma de que un cambio de esos no pase desapercibido.
//   2) LA PANTALLA AL REVES. En OpenGL el (0,0) esta ABAJO a la izquierda, y en el dibujado de la rocola
//      esta ARRIBA. Si la camara se cuadra mal, el cielo sale de cabeza. Se pide un degradado con el
//      color claro arriba y se comprueba que arriba esta el claro.
//   3) EL MEDIO PIXEL. El dibujado resta 0.5 a lo que tiene que quedar clavado al pixel, porque Direct3D
//      lo pedia. Se sube una textura de cuadros de un pixel y se pega a tamano natural: si el medio pixel
//      esta bien puesto, cada cuadro de la textura cae en un pixel de la pantalla y los colores salen
//      limpios; si no, salen mezclados con el vecino (medio gris en vez de blanco y negro).
//
// Y antes de nada, una condicion previa: que el dispositivo haya arrancado. Si no, no se prueba nada y
// hay que decirlo, no dar por bueno un cero.
class Gl
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    [DllImport("libGL.so.1")] static extern void glReadPixels(int x, int y, int w, int h, uint fmt, uint tipo, byte[] datos);
    [DllImport("libGL.so.1")] static extern void glFinish();
    const uint GL_BGRA = 0x80E1, GL_UNSIGNED_BYTE = 0x1401;

    const int W = 256, H = 192;
    static Type TSF, TDev;
    static object campo;

    // Lee la ventana entera. glReadPixels devuelve las filas de ABAJO hacia arriba, asi que se le da la
    // vuelta al leerlas: a partir de aqui el (0,0) es el de siempre, arriba a la izquierda.
    static Color[,] Foto()
    {
        byte[] px = new byte[W * H * 4];
        glFinish();
        glReadPixels(0, 0, W, H, GL_BGRA, GL_UNSIGNED_BYTE, px);
        Color[,] c = new Color[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int o = ((H - 1 - y) * W + x) * 4;
                c[x, y] = Color.FromArgb(px[o + 3], px[o + 2], px[o + 1], px[o + 0]);
            }
        return c;
    }

    static object Dev { get { return TSF.GetField("gpu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(campo); } }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";   // en la tanda, "rocola.exe" ES el perfil con tarjeta
        Assembly asm = Assembly.LoadFrom(exe);
        TSF = asm.GetType("Rocola.GpuStarfield", true);
        TDev = asm.GetType("Rocola.D3D9Device", true);
        Check(TSF != null && TDev != null, "el fondo por tarjeta grafica y su dispositivo estan en su sitio");

        Form f = new Form();
        f.FormBorderStyle = FormBorderStyle.None; f.ClientSize = new Size(W, H);
        f.Show();
        Application.DoEvents();

        Console.WriteLine("\n  0) EL DISPOSITIVO ARRANCA  (si no, lo de abajo no probaria nada)\n");
        campo = Activator.CreateInstance(TSF, new object[] { f.Handle, W, H, 1234 });
        bool ok = (bool)TSF.GetProperty("Ok").GetValue(campo, null);
        Check(ok, "OpenGL arranco dentro de la ventana de la rocola");
        if (!ok) { Console.WriteLine("\n  " + (fallos + 1) + " FALLO(S)\n"); return fallos + 1; }
        PropertyInfo pQuien = TDev.GetProperty("Quien");
        if (pQuien != null) Console.WriteLine("     dibuja: " + pQuien.GetValue(Dev, null));

        MethodInfo mSky = TSF.GetMethod("SetSky");
        MethodInfo mBegin = TSF.GetMethod("Begin");
        MethodInfo mEnd = TSF.GetMethod("End");

        Console.WriteLine("\n  1) EL COLOR SALE COMO SE PIDE, NO CON EL ROJO Y EL AZUL CAMBIADOS\n");
        mSky.Invoke(campo, new object[] { 0xFFC00000u, 0xFFC00000u, 0xFFC00000u });   // rojo de punta a punta
        mBegin.Invoke(campo, new object[] { W, H, 0f, 0f });
        Color[,] rojo = Foto();
        mEnd.Invoke(campo, null);
        Color m = rojo[W / 2, H / 2];
        Console.WriteLine("     pedido rojo (192,0,0)  ->  salio (" + m.R + "," + m.G + "," + m.B + ")");
        Check(m.R > 150 && m.B < 60, "un cielo rojo sale ROJO (si el color fuera al reves, saldria azul)");

        // Y al reves, para que no cuele una casualidad: un cielo azul tiene que salir azul.
        mSky.Invoke(campo, new object[] { 0xFF0000C0u, 0xFF0000C0u, 0xFF0000C0u });
        mBegin.Invoke(campo, new object[] { W, H, 0f, 0f });
        Color[,] azul = Foto();
        mEnd.Invoke(campo, null);
        Color m2 = azul[W / 2, H / 2];
        Console.WriteLine("     pedido azul (0,0,192)  ->  salio (" + m2.R + "," + m2.G + "," + m2.B + ")");
        Check(m2.B > 150 && m2.R < 60, "y un cielo azul sale AZUL");

        Console.WriteLine("\n  2) LA PANTALLA NO SALE DE CABEZA\n");
        // Claro arriba, oscuro abajo. Si la camara estuviera al reves saldria al reves.
        mSky.Invoke(campo, new object[] { 0xFF00E000u, 0xFF007000u, 0xFF001000u });
        mBegin.Invoke(campo, new object[] { W, H, 0f, 0f });
        Color[,] grad = Foto();
        mEnd.Invoke(campo, null);
        int arriba = grad[W / 2, 3].G, abajo = grad[W / 2, H - 4].G;
        Console.WriteLine("     verde pedido claro arriba (224) y oscuro abajo (16)  ->  arriba " + arriba + ", abajo " + abajo);
        Check(arriba > abajo + 80, "lo claro sale ARRIBA y lo oscuro abajo, como se pidio");

        Console.WriteLine("\n  3) UNA TEXTURA A TAMANO NATURAL CAE PIXEL CON PIXEL\n");
        // Un tablero de cuadros de UN pixel: es el peor caso posible. Si la textura cae medio pixel
        // corrida, cada pixel de pantalla mezcla dos cuadros y todo sale gris; si cae clavada, se leen
        // blancos y negros limpios. Se cuenta cuantos pixeles quedaron en el medio.
        const int TW = 64;
        Bitmap tab = new Bitmap(TW, TW, PixelFormat.Format32bppArgb);
        for (int y = 0; y < TW; y++)
            for (int x = 0; x < TW; x++)
                tab.SetPixel(x, y, (((x + y) & 1) == 0) ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 0, 0, 0));
        object[] arg = new object[] { tab, 0f, 0f };
        IntPtr tex = (IntPtr)TSF.GetMethod("CreateOverlay").Invoke(campo, arg);
        float uMax = (float)arg[1], vMax = (float)arg[2];
        Check(tex != IntPtr.Zero, "la textura se subio a la tarjeta");

        // El cuadrangulo, con el MISMO medio pixel que usa el dibujado de la rocola (SetV en D3D9Star.cs).
        Type TV = asm.GetType("Rocola.D3D9+Vtx", false) ?? asm.GetType("Rocola.D3D9").GetNestedType("Vtx");
        Array quad = Array.CreateInstance(TV, 6);
        int X0 = 32, Y0 = 32;
        PonQuad(TV, quad, X0, Y0, X0 + TW, Y0 + TW, uMax, vMax);
        mSky.Invoke(campo, new object[] { 0xFF000000u, 0xFF000000u, 0xFF000000u });
        mBegin.Invoke(campo, new object[] { W, H, 0f, 0f });
        TSF.GetMethod("DrawQuad").Invoke(campo, new object[] { tex, quad });
        Color[,] cuadros = Foto();
        mEnd.Invoke(campo, null);

        int limpios = 0, medios = 0, malos = 0;
        for (int y = 2; y < TW - 2; y++)
            for (int x = 2; x < TW - 2; x++)
            {
                int g = cuadros[X0 + x, Y0 + y].G;
                bool blanco = (((x + y) & 1) == 0);
                if (g > 200 || g < 55) { if ((blanco && g > 200) || (!blanco && g < 55)) limpios++; else malos++; }
                else medios++;
            }
        int total = limpios + medios + malos;
        Console.WriteLine("     de " + total + " pixeles del tablero: " + limpios + " limpios, " + medios
                          + " a medias, " + malos + " al reves");
        Check(limpios * 100 / Math.Max(1, total) >= 90,
              "la textura cae clavada al pixel (el medio pixel de Direct3D quedo bien traducido)");

        Console.WriteLine("\n  4) SUMAR LUZ SUMA, Y TAPAR TAPA\n");
        // Dos mallas del mismo gris, una encima de otra. Tapando, el resultado es ese gris; sumando, sale
        // el doble. Si la mezcla estuviera mal puesta, las estrellas y la aurora no brillarian.
        Array m1 = Array.CreateInstance(TV, 6);
        PonQuad(TV, m1, 10, 10, 60, 60, 0f, 0f, 0xFF404040u);
        mSky.Invoke(campo, new object[] { 0xFF000000u, 0xFF000000u, 0xFF000000u });
        mBegin.Invoke(campo, new object[] { W, H, 0f, 0f });
        TSF.GetMethod("DrawMesh").Invoke(campo, new object[] { m1, 2 });
        TSF.GetMethod("DrawMesh").Invoke(campo, new object[] { m1, 2 });
        Color[,] tapa = Foto();
        mEnd.Invoke(campo, null);

        mBegin.Invoke(campo, new object[] { W, H, 0f, 0f });
        TSF.GetMethod("DrawMeshAdd").Invoke(campo, new object[] { m1, 2 });
        TSF.GetMethod("DrawMeshAdd").Invoke(campo, new object[] { m1, 2 });
        Color[,] suma = Foto();
        mEnd.Invoke(campo, null);

        int gTapa = tapa[35, 35].G, gSuma = suma[35, 35].G;
        Console.WriteLine("     gris 64 pintado dos veces:  tapando " + gTapa + ", sumando " + gSuma);
        Check(gTapa >= 54 && gTapa <= 74, "pintar dos veces TAPANDO deja el mismo gris");
        Check(gSuma >= gTapa * 2 - 20, "y pintar dos veces SUMANDO deja el doble de luz");

        TSF.GetMethod("Dispose").Invoke(campo, null);
        f.Close();
        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    // Un cuadrangulo (2 triangulos) con el medio pixel del dibujado de la rocola.
    static void PonQuad(Type TV, Array a, float x0, float y0, float x1, float y1, float uM, float vM) { PonQuad(TV, a, x0, y0, x1, y1, uM, vM, 0xFFFFFFFFu); }
    static void PonQuad(Type TV, Array a, float x0, float y0, float x1, float y1, float uM, float vM, uint col)
    {
        V(TV, a, 0, x0, y0, col, 0, 0); V(TV, a, 1, x1, y0, col, uM, 0); V(TV, a, 2, x1, y1, col, uM, vM);
        V(TV, a, 3, x0, y0, col, 0, 0); V(TV, a, 4, x1, y1, col, uM, vM); V(TV, a, 5, x0, y1, col, 0, vM);
    }
    static void V(Type TV, Array a, int i, float x, float y, uint c, float u, float v)
    {
        object vx = Activator.CreateInstance(TV);
        TV.GetField("X").SetValue(vx, x - 0.5f); TV.GetField("Y").SetValue(vx, y - 0.5f);
        TV.GetField("Z").SetValue(vx, 0f); TV.GetField("Rhw").SetValue(vx, 1f);
        TV.GetField("Color").SetValue(vx, c); TV.GetField("U").SetValue(vx, u); TV.GetField("V").SetValue(vx, v);
        a.SetValue(vx, i);
    }
}

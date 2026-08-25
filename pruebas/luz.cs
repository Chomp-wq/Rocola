using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// LA MEZCLA DE LUZ, Y LAS DOS COSAS QUE LA TARJETA PUEDE DAR O NO.
//
// Tres cosas que Linux permite pedirle a la tarjeta grafica y que en Windows no estaban:
//
//   LA MEZCLA DE LUZ CORRECTA. Los numeros que guardan un color no van en linea recta con la cantidad
//   de luz: estan curvados a proposito, porque el ojo distingue mejor entre dos oscuros que entre dos
//   claros. Eso esta bien para guardar un color, pero es una trampa para SUMARLO, y la rocola suma
//   mucha luz: cada estrella pone su halo encima del cielo, la aurora el suyo, las fugaces el suyo.
//   Sumando los numeros curvados, el centro de un halo se quema a blanco de golpe y el resplandor de
//   alrededor se queda corto: por eso se ven "de plastico".
//
//   EL SUAVIZADO DE BORDES y ESPERAR A LA PANTALLA. Estas dos no se pueden probar aqui: esta maquina
//   no tiene tarjeta grafica -OpenGL lo hace el procesador- y no ofrece ninguna de las dos. Lo que SI
//   se comprueba, y no es poco, es que la rocola las pide bien, se repliega sin romperse cuando no
//   estan, y DICE LA VERDAD sobre lo que consiguio. Prometer un suavizado que no esta seria peor que
//   no tenerlo, porque nadie iria a mirar por que se sigue viendo la escalera.
class Luz
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    [DllImport("libGL.so.1")] static extern void glReadPixels(int x, int y, int w, int h, uint f, uint t, byte[] d);
    [DllImport("libGL.so.1")] static extern void glFinish();
    const uint GL_BGRA = 0x80E1, GL_UNSIGNED_BYTE = 0x1401;
    const int W = 512, H = 384;

    static Type TSF, TDev;

    static Color[,] Foto()
    {
        byte[] px = new byte[W * H * 4];
        glFinish(); glReadPixels(0, 0, W, H, GL_BGRA, GL_UNSIGNED_BYTE, px);
        Color[,] c = new Color[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            { int o = ((H - 1 - y) * W + x) * 4; c[x, y] = Color.FromArgb(255, px[o + 2], px[o + 1], px[o + 0]); }
        return c;
    }

    // Dibuja el MISMO cuadro -cielo negro y un solo halo en medio- con o sin la mezcla correcta.
    static Color[,] Cuadro(bool luzReal, out bool loHizo, out string quien)
    {
        TDev.GetField("LuzReal", BindingFlags.Static | BindingFlags.Public).SetValue(null, luzReal);
        Form f = new Form(); f.FormBorderStyle = FormBorderStyle.None; f.ClientSize = new Size(W, H);
        f.Show(); Application.DoEvents();
        object c = Activator.CreateInstance(TSF, new object[] { f.Handle, W, H, 7 });
        loHizo = false; quien = "";
        if (!(bool)TSF.GetProperty("Ok").GetValue(c, null)) { f.Close(); return null; }
        object dev = TSF.GetField("gpu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);
        loHizo = (bool)TDev.GetProperty("ConLuzReal").GetValue(dev, null);
        quien = (string)TDev.GetProperty("Quien").GetValue(dev, null);
        TSF.GetMethod("SetSky").Invoke(c, new object[] { 0xFF000000u, 0xFF000000u, 0xFF000000u });
        TSF.GetMethod("Begin").Invoke(c, new object[] { W, H, 0f, 0f });
        TSF.GetMethod("DrawGlow").Invoke(c, new object[] { W * 0.5f, H * 0.5f, 120f, 0xC080C0FFu });
        Color[,] foto = Foto();
        TSF.GetMethod("End").Invoke(c, null);
        TSF.GetMethod("Dispose").Invoke(c, null); f.Close(); Application.DoEvents();
        return foto;
    }

    // Cuanta luz hay en un anillo alrededor del centro. Es donde se nota como se suma: si la suma se
    // hace con los numeros curvados, el resplandor de alrededor se queda corto.
    static long LuzEnElAnillo(Color[,] c, double r0, double r1)
    {
        long total = 0; int cx = W / 2, cy = H / 2;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d >= r0 && d < r1) total += c[x, y].G;
            }
        return total;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TSF = asm.GetType("Rocola.GpuStarfield", true);
        TDev = asm.GetType("Rocola.D3D9Device", true);

        Console.WriteLine("\n  1) LA ROCOLA DICE LA VERDAD SOBRE LO QUE CONSIGUIO\n");
        bool hizo; string quien;
        Color[,] sin = Cuadro(false, out hizo, out quien);
        Check(sin != null, "el fondo por tarjeta arranca (si no, no se prueba nada)");
        if (sin == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }
        Console.WriteLine("     " + quien);
        Check(!hizo, "apagada de fabrica, la mezcla de luz NO se enciende sola");

        // Lo que dice tiene que coincidir con lo que hay de verdad. Se le pregunta a OpenGL por su
        // cuenta, no a la rocola: si la rocola dijera "suavizado x4" en una maquina que no lo ofrece,
        // nadie iria a mirar por que se sigue viendo la escalera.
        bool diceSuave = quien.IndexOf("bordes suavizados", StringComparison.Ordinal) >= 0;
        bool diceSinc = quien.IndexOf("  |  sincronizada", StringComparison.Ordinal) >= 0;
        bool haySuave = HaySuavizadoDeVerdad();
        Console.WriteLine("     suavizado: la rocola dice " + (diceSuave ? "que si" : "que no")
                          + ", y esta maquina " + (haySuave ? "si lo ofrece" : "NO lo ofrece"));
        Check(diceSuave == haySuave, "lo que dice del suavizado de bordes es lo que hay de verdad");
        Console.WriteLine("     sincronizar con la pantalla: la rocola dice " + (diceSinc ? "que si" : "que no"));

        Console.WriteLine("\n  2) Y CON LA MEZCLA CORRECTA, LA LUZ SE REPARTE EN VEZ DE QUEMARSE\n");
        bool hizo2;
        Color[,] con = Cuadro(true, out hizo2, out quien);
        Check(con != null && hizo2, "encendida, la mezcla de luz correcta SI se enciende");
        if (con == null || !hizo2) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        // El cielo de fondo casi no puede cambiar: si cambiara, no se estaria arreglando la SUMA,
        // se estaria cambiando el color de todo.
        Color fSin = sin[8, 8], fCon = con[8, 8];
        Console.WriteLine("     esquina sin halo:  antes " + Txt(fSin) + "   despues " + Txt(fCon));
        Check(Math.Abs(fSin.R - fCon.R) <= 4 && Math.Abs(fSin.G - fCon.G) <= 4 && Math.Abs(fSin.B - fCon.B) <= 4,
              "el cielo de fondo se queda como estaba (esto arregla la SUMA, no el color)");

        long anilloSin = LuzEnElAnillo(sin, 60, 110), anilloCon = LuzEnElAnillo(con, 60, 110);
        long centroSin = LuzEnElAnillo(sin, 0, 20), centroCon = LuzEnElAnillo(con, 0, 20);
        double repartoSin = (double)anilloSin / centroSin, repartoCon = (double)anilloCon / centroCon;
        Console.WriteLine("     luz en el resplandor de alrededor: antes " + anilloSin + "   despues " + anilloCon);
        Console.WriteLine("     luz en el centro del halo:         antes " + centroSin + "   despues " + centroCon);
        Console.WriteLine("     resplandor por cada unidad de nucleo: antes " + repartoSin.ToString("0.0")
                          + "   despues " + repartoCon.ToString("0.0"));

        // LO QUE SE COMPRUEBA, Y POR QUE ASI. Que la luz total suba no es un fallo: es justo lo que se
        // estaba perdiendo -sumando numeros curvados, lo debil se quedaba MAS corto que lo fuerte-. Lo
        // que dice si el halo esta bien hecho es la PROPORCION entre el resplandor de alrededor y el
        // nucleo: si el resplandor crece mas que el nucleo, el halo se abre en vez de concentrarse en un
        // punto quemado, que es exactamente el aspecto de plastico que se queria quitar.
        Check(repartoCon > repartoSin * 13 / 10,
              "el resplandor crece mas que el nucleo: el halo se abre en vez de quemarse en un punto");

        Console.WriteLine("\n  3) Y EN LA PANTALLA DE VERDAD NO SE QUEMA MAS  (que es lo que importa)\n");
        // Un halo suelto y enorme sobre negro es un caso de laboratorio, no lo que se ve en el bar. Lo que
        // se mide aqui es la pantalla de verdad -el cielo con su campo de estrellas, con el latido al
        // maximo, que es el peor caso normal- y se cuenta cuantos pixeles se van a BLANCO PURO. Un pixel
        // blanco puro ya no tiene color: ahi se perdio la estrella y quedo una mota.
        //
        // Resulto al reves de lo que uno esperaria: con la mezcla correcta se queman MENOS, no mas. Al
        // sumar bien, la luz de cada estrella se reparte por su halo en vez de amontonarse en el nucleo.
        int blancosSin = Blancos(Pantalla(false)), blancosCon = Blancos(Pantalla(true));
        Console.WriteLine("     pixeles en blanco puro:  antes " + blancosSin + "   despues " + blancosCon);
        Check(blancosCon <= blancosSin,
              "en la pantalla de verdad no se quema ni un pixel mas (de hecho se queman menos)");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }

    static string Txt(Color c) { return "(" + c.R + "," + c.G + "," + c.B + ")"; }

    // La pantalla de verdad: el cielo de la rocola con su campo de estrellas y el latido al maximo.
    static Color[,] Pantalla(bool luzReal)
    {
        TDev.GetField("LuzReal", BindingFlags.Static | BindingFlags.Public).SetValue(null, luzReal);
        Form f = new Form(); f.FormBorderStyle = FormBorderStyle.None; f.ClientSize = new Size(W, H);
        f.Show(); Application.DoEvents();
        object c = Activator.CreateInstance(TSF, new object[] { f.Handle, W, H, 20250824 });
        if (!(bool)TSF.GetProperty("Ok").GetValue(c, null)) { f.Close(); return null; }
        TSF.GetMethod("SetSky").Invoke(c, new object[] { 0xFF06081Au, 0xFF101C3Au, 0xFF1A2C5Au });
        for (int i = 0; i < 10; i++) TSF.GetMethod("Render").Invoke(c, new object[] { W, H, 1f });
        TSF.GetMethod("Begin").Invoke(c, new object[] { W, H, 1f, 1f });
        Color[,] foto = Foto();
        TSF.GetMethod("End").Invoke(c, null);
        TSF.GetMethod("Dispose").Invoke(c, null); f.Close(); Application.DoEvents();
        return foto;
    }

    static int Blancos(Color[,] c)
    {
        if (c == null) return -1;
        int n = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (c[x, y].R >= 255 && c[x, y].G >= 255 && c[x, y].B >= 255) n++;
        return n;
    }

    // Se le pregunta a OpenGL, no a la rocola: ¿hay alguna forma de pantalla con varias muestras por
    // pixel? Asi la comprobacion de arriba no es la rocola dandose la razon a si misma.
    [DllImport("libGL.so.1")] static extern IntPtr glXChooseFBConfig(IntPtr dpy, int pantalla, int[] att, out int n);
    [DllImport("libGL.so.1")] static extern int glXGetFBConfigAttrib(IntPtr dpy, IntPtr cfg, int att, out int v);
    [DllImport("libX11.so.6")] static extern IntPtr XOpenDisplay(IntPtr n);
    [DllImport("libX11.so.6")] static extern int XCloseDisplay(IntPtr d);
    const int GLX_SAMPLES = 100001;

    static bool HaySuavizadoDeVerdad()
    {
        IntPtr d = IntPtr.Zero;
        try
        {
            d = XOpenDisplay(IntPtr.Zero);
            if (d == IntPtr.Zero) return false;
            int n;
            IntPtr cfgs = glXChooseFBConfig(d, 0, null, out n);
            if (cfgs == IntPtr.Zero || n <= 0) return false;
            for (int i = 0; i < n; i++)
            {
                IntPtr cfg = Marshal.ReadIntPtr(cfgs, i * IntPtr.Size);
                int v;
                if (glXGetFBConfigAttrib(d, cfg, GLX_SAMPLES, out v) == 0 && v > 1) return true;
            }
            return false;
        }
        catch { return false; }
        finally { if (d != IntPtr.Zero) try { XCloseDisplay(d); } catch { } }
    }
}

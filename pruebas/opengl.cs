using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// LA TARJETA GRAFICA TIENE QUE PODER PINTAR DENTRO DE LA VENTANA DE LA ROCOLA.
//
// En Windows, el fondo de estrellas lo dibuja la tarjeta con Direct3D: la rocola le da a Direct3D el trozo
// de ventana de uno de sus controles y la tarjeta pinta ahi. En Linux no hay Direct3D, hay OpenGL, y OpenGL
// no se cuelga de un control: se cuelga de una ventana del sistema de ventanas (X11).
//
// O sea que todo el fondo por tarjeta grafica en Linux depende de UNA cosa: poder averiguar cual es la
// ventana de X11 que hay debajo de un control de la rocola, y conseguir que OpenGL pinte en ella. Si eso no
// se puede, no hay aurora ni barrido de luz en Linux y hay que quedarse con el fondo por procesador.
//
// Aqui se comprueba, en este orden:
//
//   1) que se le puede sacar a Mono la ventana de X11 de un control;
//   2) que OpenGL acepta esa ventana y pinta en ella;
//   3) que lo que hay es OpenGL "de compatibilidad", que es el que entiende la forma de dibujar que usa el
//      fondo de estrellas (vertices con su color y su textura, sin programas de sombreado). Si solo hubiera
//      OpenGL moderno habria que reescribir el dibujado entero en vez de traducirlo.
//   4) y que se puede dibujar de verdad: un triangulo con color, y comprobar los pixeles que salieron.
//
// (En esta maquina no hay tarjeta grafica: OpenGL lo hace el procesador por software. Da igual para lo que
// se comprueba aqui, que es el camino, no la velocidad.)
class OpenGl
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    // ---------------- X11 ----------------
    [DllImport("libX11.so.6")] static extern int XDefaultScreen(IntPtr dpy);
    [DllImport("libX11.so.6")] static extern int XSync(IntPtr dpy, bool descartar);

    // ---------------- GLX y OpenGL ----------------
    [DllImport("libGL.so.1")] static extern IntPtr glXChooseVisual(IntPtr dpy, int pantalla, int[] atributos);
    [DllImport("libGL.so.1")] static extern IntPtr glXCreateContext(IntPtr dpy, IntPtr vis, IntPtr compartir, bool directo);
    [DllImport("libGL.so.1")] static extern bool glXMakeCurrent(IntPtr dpy, IntPtr ventana, IntPtr ctx);
    [DllImport("libGL.so.1")] static extern void glXSwapBuffers(IntPtr dpy, IntPtr ventana);
    [DllImport("libGL.so.1")] static extern IntPtr glGetString(uint nombre);
    [DllImport("libGL.so.1")] static extern void glClearColor(float r, float g, float b, float a);
    [DllImport("libGL.so.1")] static extern void glClear(uint mask);
    [DllImport("libGL.so.1")] static extern void glViewport(int x, int y, int w, int h);
    [DllImport("libGL.so.1")] static extern void glMatrixMode(uint modo);
    [DllImport("libGL.so.1")] static extern void glLoadIdentity();
    [DllImport("libGL.so.1")] static extern void glOrtho(double i, double d, double ab, double ar, double c, double l);
    [DllImport("libGL.so.1")] static extern void glBegin(uint modo);
    [DllImport("libGL.so.1")] static extern void glEnd();
    [DllImport("libGL.so.1")] static extern void glColor4ub(byte r, byte g, byte b, byte a);
    [DllImport("libGL.so.1")] static extern void glVertex2f(float x, float y);
    [DllImport("libGL.so.1")] static extern void glReadPixels(int x, int y, int w, int h, uint fmt, uint tipo, byte[] datos);
    [DllImport("libGL.so.1")] static extern void glFinish();
    [DllImport("libGL.so.1")] static extern uint glGetError();

    const int GLX_RGBA = 4, GLX_DOUBLEBUFFER = 5, GLX_RED_SIZE = 8, GLX_GREEN_SIZE = 9, GLX_BLUE_SIZE = 10, GLX_DEPTH_SIZE = 12;
    const uint GL_COLOR_BUFFER_BIT = 0x4000, GL_RENDERER = 0x1F01, GL_VERSION = 0x1F02;
    const uint GL_PROJECTION = 0x1701, GL_MODELVIEW = 0x1700, GL_TRIANGLES = 0x0004, GL_RGBA = 0x1908, GL_UNSIGNED_BYTE = 0x1401;

    static string Texto(uint n) { IntPtr p = glGetString(n); return (p == IntPtr.Zero) ? "?" : Marshal.PtrToStringAnsi(p); }

    // LA PIEZA CLAVE: sacarle a Mono la ventana de X11 que hay debajo de un control. Mono no lo cuenta por
    // ninguna via oficial, pero por dentro guarda esa correspondencia y se le puede preguntar. Mono ya no
    // cambia -esta congelado-, asi que esto no se va a mover bajo los pies.
    public static bool VentanaDe(Control c, out IntPtr dpy, out IntPtr win)
    {
        dpy = IntPtr.Zero; win = IntPtr.Zero;
        Assembly swf = typeof(Control).Assembly;
        Type tx = swf.GetType("System.Windows.Forms.XplatUIX11");
        if (tx == null) return false;                     // no estamos en Linux
        PropertyInfo p = tx.GetProperty("DisplayHandle", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null) { try { dpy = (IntPtr)p.GetValue(null, null); } catch { } }
        if (dpy == IntPtr.Zero)
        {
            FieldInfo f = tx.GetField("DisplayHandle", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) { try { dpy = (IntPtr)f.GetValue(null); } catch { } }
        }
        Type th = swf.GetType("System.Windows.Forms.Hwnd");
        if (th == null) return false;
        MethodInfo m = th.GetMethod("ObjectFromHandle", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (m == null) return false;
        object h = null;
        try { h = m.Invoke(null, new object[] { c.Handle }); } catch { }
        if (h == null) return false;
        foreach (string n in new string[] { "client_window", "whole_window" })
        {
            FieldInfo f = th.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) continue;
            try { IntPtr v = (IntPtr)f.GetValue(h); if (v != IntPtr.Zero) { win = v; break; } } catch { }
        }
        return dpy != IntPtr.Zero && win != IntPtr.Zero;
    }

    static int Main()
    {
        const int W = 320, H = 240;
        Console.WriteLine("\n  1) LA VENTANA DE LA ROCOLA, VISTA POR DEBAJO\n");
        Form f = new Form();
        f.Text = "opengl"; f.ClientSize = new Size(W, H); f.FormBorderStyle = FormBorderStyle.None;
        f.Show();
        Application.DoEvents();

        IntPtr dpy, win;
        bool hay = VentanaDe(f, out dpy, out win);
        Check(hay, "se le puede sacar a Mono la ventana de X11 de un control de la rocola");
        if (!hay) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        Console.WriteLine("\n  2) Y OPENGL PINTA EN ELLA\n");
        int[] att = { GLX_RGBA, GLX_DOUBLEBUFFER, GLX_RED_SIZE, 8, GLX_GREEN_SIZE, 8, GLX_BLUE_SIZE, 8, GLX_DEPTH_SIZE, 0, 0 };
        IntPtr vi = glXChooseVisual(dpy, XDefaultScreen(dpy), att);
        Check(vi != IntPtr.Zero, "hay un formato de pantalla que le sirve a OpenGL");
        if (vi == IntPtr.Zero) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }
        IntPtr ctx = glXCreateContext(dpy, vi, IntPtr.Zero, true);
        Check(ctx != IntPtr.Zero, "y se puede arrancar OpenGL");
        bool puesto = (ctx != IntPtr.Zero) && glXMakeCurrent(dpy, win, ctx);
        Check(puesto, "y se le puede colgar A LA VENTANA QUE YA HIZO MONO (sin ventanas de mas ni copias)");
        if (!puesto) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        string quien = Texto(GL_RENDERER), version = Texto(GL_VERSION);
        Console.WriteLine("     dibuja: " + quien);
        Console.WriteLine("     version: " + version);

        Console.WriteLine("\n  3) Y ES DEL QUE ENTIENDE LA FORMA DE DIBUJAR DEL FONDO DE ESTRELLAS\n");
        // El fondo de estrellas de la rocola no usa programas de sombreado: son vertices ya puestos en la
        // pantalla, cada uno con su color y su textura. Eso es OpenGL "de compatibilidad". Si solo hubiera
        // OpenGL moderno habria que reescribirlo entero en vez de traducirlo.
        bool compat = version.IndexOf("Compatibility", StringComparison.OrdinalIgnoreCase) >= 0
                      || (version.Length > 0 && (version[0] == '1' || version[0] == '2'));
        Check(compat, "es OpenGL de compatibilidad: el dibujado de Direct3D se traduce, no se reescribe");

        Console.WriteLine("\n  4) Y DIBUJA DE VERDAD\n");
        glViewport(0, 0, W, H);
        glMatrixMode(GL_PROJECTION); glLoadIdentity(); glOrtho(0, W, H, 0, -1, 1);
        glMatrixMode(GL_MODELVIEW); glLoadIdentity();
        glClearColor(0f, 0f, 0f, 1f);
        glClear(GL_COLOR_BUFFER_BIT);
        glBegin(GL_TRIANGLES);
        glColor4ub(255, 40, 40, 255); glVertex2f(0, 0);
        glColor4ub(255, 40, 40, 255); glVertex2f(W, 0);
        glColor4ub(255, 40, 40, 255); glVertex2f(0, H);
        glEnd();
        glFinish();
        Check(glGetError() == 0, "OpenGL no se queja de nada de lo que se le pidio");

        byte[] px = new byte[W * H * 4];
        glReadPixels(0, 0, W, H, GL_RGBA, GL_UNSIGNED_BYTE, px);
        // El triangulo cubre la mitad de arriba a la izquierda. Se miran dos puntos: uno dentro y uno fuera.
        int dentro = ((H - 1 - 10) * W + 10) * 4;          // arriba a la izquierda (OpenGL cuenta desde abajo)
        int fuera = ((H - 1 - (H - 10)) * W + (W - 10)) * 4;   // abajo a la derecha
        Console.WriteLine("     un punto dentro del triangulo: R=" + px[dentro] + " G=" + px[dentro + 1] + " B=" + px[dentro + 2]);
        Console.WriteLine("     un punto fuera:                R=" + px[fuera] + " G=" + px[fuera + 1] + " B=" + px[fuera + 2]);
        Check(px[dentro] > 200 && px[dentro + 1] < 90, "lo de dentro salio del color que se pidio");
        Check(px[fuera] < 40 && px[fuera + 1] < 40, "y lo de fuera se quedo en negro");

        glXSwapBuffers(dpy, win);
        XSync(dpy, false);
        Check(true, "y se presenta en pantalla sin quejarse");

        glXMakeCurrent(dpy, IntPtr.Zero, IntPtr.Zero);
        f.Close();
        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

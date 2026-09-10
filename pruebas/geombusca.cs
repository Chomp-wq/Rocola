using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// EL SITIO DEL TECLADO Y DE LA PORTADA SE CALCULA EN UN SOLO LUGAR.
//
// En el buscador, el teclado y la vista previa de la cancion elegida ocupan el MISMO hueco. Ese hueco
// lo decidia el pintado de Form1.Buscador.cs... y lo volvia a decidir, a mano y en otro archivo, el
// trozo que dibuja la tarjeta grafica aparte (AcabadoGpu.cs), con el 128 escrito tal cual en vez de
// M+70+28 y el margen otra vez como 30.
//
// Daban lo mismo, asi que no habia nada roto HOY. Lo que no habia era nada que los atara: el dia que
// se toque el margen o el alto de la cabecera se mueve todo menos el trozo de la tarjeta, y no avisa
// nadie -no revienta, no sale en la tanda: simplemente el panel queda descolocado en la pantalla del
// bar-. Esta bateria es ese pasador. Comprueba dos cosas:
//
//   1. que las dos cuentas den lo mismo en varios tamaños de pantalla, y
//   2. que haya UNA SOLA cuenta: que el trozo de la tarjeta PIDA la region en vez de rehacerla.
//
// La segunda es la que importa, y es la que fallaba antes del arreglo.
class Geombusca
{
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int fallos;
    static void Bien(string q) { Console.WriteLine("  OK    " + q); }
    static void Mal(string q) { Console.WriteLine("  FALLO " + q); fallos++; }
    static void Exige(bool c, string q) { if (c) Bien(q); else Mal(q); }

    // El reparto, escrito aparte a proposito: si el del programa se desvia, este no.
    static Rectangle RegionDeMano(int w, int h)
    {
        int M = 30, contentY = M + 70 + 28, contentH = h - contentY - M;
        return new Rectangle(M, contentY, (int)((w - 2 * M) * 0.52), contentH);
    }
    static Rectangle PortadaDeMano(Rectangle region)
    {
        int side = (int)(Math.Min(region.Width, region.Height) * 0.62);
        if (side < 40) side = 40;
        int cx = region.X + region.Width / 2;
        int top = region.Y + (region.Height - side) / 2 - 24;
        if (top < region.Y + 6) top = region.Y + 6;
        return new Rectangle(cx - side / 2, top, side, side);
    }

    static int Main(string[] argv)
    {
        Assembly a = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type tf = a.GetType("Rocola.Form1");
        if (tf == null) { Mal("no encuentro Rocola.Form1"); return 1; }
        Type to = null;
        foreach (Type t in Codigo.ConLosDeDentro(tf)) if (t.Name == "SearchOverlay") to = t;
        if (to == null) { Mal("no encuentro SearchOverlay"); return 1; }

        MethodInfo region = to.GetMethod("RegionTeclado", TODO);
        MethodInfo portada = to.GetMethod("CajaPortada", TODO);
        Exige(region != null, "existe una sola cuenta para la region del teclado (RegionTeclado)");
        Exige(portada != null, "y una sola para la caja de la portada (CajaPortada)");
        if (region == null || portada == null) return fallos;

        // --- 1) LAS DOS CUENTAS DAN LO MISMO, en las pantallas que puede haber ---
        int[,] pantallas = { { 1024, 768 }, { 1280, 1024 }, { 1366, 768 }, { 1920, 1080 }, { 800, 600 } };
        object ov = Activator.CreateInstance(to, true);
        System.Windows.Forms.Control ctl = (System.Windows.Forms.Control)ov;
        for (int i = 0; i < pantallas.GetLength(0); i++)
        {
            int w = pantallas[i, 0], h = pantallas[i, 1];
            ctl.Size = new Size(w, h);
            Rectangle real = (Rectangle)region.Invoke(ov, null);
            Rectangle mano = RegionDeMano(w, h);
            Exige(real == mano, "en " + w + "x" + h + " el teclado va en " + mano + (real == mano ? "" : " y sale " + real));
            Rectangle rp = (Rectangle)portada.Invoke(null, new object[] { real });
            Exige(rp == PortadaDeMano(mano), "en " + w + "x" + h + " la portada va en " + PortadaDeMano(mano));
        }

        // --- 2) Y HAY UNA SOLA: el trozo de la tarjeta la PIDE, no la rehace ---
        MethodInfo extra = to.GetMethod("ExtraPreviewGeometry", TODO);
        if (extra == null) { Console.WriteLine("  OK    sin tarjeta grafica no existe ExtraPreviewGeometry"); }
        else
        {
            List<string> dentro = Codigo.Nombres(extra);
            Exige(dentro.Contains("RegionTeclado"), "el trozo de la tarjeta PIDE la region en vez de rehacerla a mano");
            Exige(dentro.Contains("CajaPortada"), "y pide tambien la caja de la portada");
        }
        bool usada = false;
        foreach (MethodBase m in Codigo.MetodosConAyudantes(to, "ComputePreview"))
            if (Codigo.Llama(m, "CajaPortada")) usada = true;
        Exige(usada, "y la vista previa de siempre usa esa misma caja");

        Console.WriteLine(fallos == 0 ? "  todo bien" : ("  " + fallos + " fallos"));
        return fallos;
    }
}

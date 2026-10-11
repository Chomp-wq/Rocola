using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LA CASILLA DE AYUDA Y LO QUE DIBUJA LA GUIA (11-10-2026), solo con tarjeta grafica.
// Comprueba, sin abrir Direct3D: que la casilla mide y se dibuja con su interrogacion y sus dos botones (rojo y
// flecha roja de abajo), que la interrogacion animada no se sale ni da NaN, que el globo de la guia cabe en la
// pantalla y no tapa lo que senala (en muchas pantallas y objetivos), que el marco y la flecha dorada quedan
// donde deben y no desbordan sus arreglos, y que todo esta enchufado. Con una carpeta como segundo argumento
// deja un dibujo de la geometria. No es una captura de la tarjeta ni mide FPS.
class AyudaGuia
{
    const BindingFlags B = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Assembly asm; static Type form, gpu, vtx;
    static int ok, mal;
    static void Check(bool c, string s) { if (c) ok++; else { mal++; if (mal < 30) Console.WriteLine("  FALLO " + s); } }
    static object Run(Type t, string n, params object[] a)
    {
        foreach (MethodInfo m in t.GetMethods(B))
            if (m.Name == n && m.GetParameters().Length == a.Length)
                try { return m.Invoke(null, a); } catch (TargetInvocationException e) { throw e.InnerException; }
        throw new Exception("falta " + t.Name + "." + n);
    }
    static float F(object v, string f) { return (float)vtx.GetField(f).GetValue(v); }
    static bool Finito(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(args[0]);
        form = asm.GetType("Rocola.Form1", true); gpu = asm.GetType("Rocola.GpuPanel", false);
        if (gpu == null || form.GetNestedType("AyudaView", B) == null)
        {
            Check(form.GetNestedType("AyudaView", B) == null, "sin tarjeta grafica no hay casilla de AYUDA");
            Console.WriteLine("  OK ayudaguia: perfil sin tarjeta grafica, sin casilla ni guia (" + ok + ")");
            return mal;
        }
        vtx = asm.GetType("Rocola.D3D9+Vtx", true);
        Casilla(); Interrogacion(); Globo(args.Length > 1 ? args[1] : null); GloboQuieto(); Marco(); Flecha(); Enchufes();
        Console.WriteLine(mal == 0 ? "  OK ayudaguia: " + ok + " comprobaciones (casilla, interrogacion, globo, marco, flecha; sin HAL ni FPS)"
                                   : "  FALLO ayudaguia: " + mal + " de " + (ok + mal));
        return mal;
    }

    static void Casilla()
    {
        Type av = form.GetNestedType("AyudaView", B);
        foreach (int alto in new int[] { 44, 56, 76 })
            foreach (bool comp in new bool[] { false, true })
            {
                int w = (int)av.GetMethod("Ancho", B).Invoke(null, new object[] { alto, comp });
                Check(w > 80 && w < 420, "ancho razonable de la casilla (" + alto + "," + comp + "): " + w);
                Control c = (Control)Activator.CreateInstance(av);
                av.GetField("Compacta", B).SetValue(c, comp); c.Width = w; c.Height = alto;
                using (Bitmap b = new Bitmap(w, alto, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); av.GetMethod("RenderContent", B).Invoke(c, new object[] { g, false }); }
                    RectangleF ic = (RectangleF)av.GetMethod("Icono", B).Invoke(c, null);
                    Check(ic.Left > 0 && ic.Right < w && ic.Top >= 0 && ic.Bottom <= alto, "el signo cabe en la casilla");
                    int oro = 0, rojo = 0;
                    for (int y = 0; y < alto; y++) for (int x = 0; x < w; x++)
                    {
                        Color p = b.GetPixel(x, y); if (p.A < 200) continue;
                        if (p.R > 200 && p.G > 140 && p.B < 120) oro++;
                        if (p.R > 200 && p.G < 110 && p.B < 110) rojo++;
                    }
                    Check(oro > alto, "la interrogacion es dorada (" + oro + ")");
                    Check(rojo > alto / 2, "lleva el boton rojo y la flecha roja dibujados (" + rojo + ")");
                }
                c.Dispose();
            }
    }

    static void Interrogacion()
    {
        using (Bitmap q = (Bitmap)Run(gpu, "BakeInterrogacion", 64))
        {
            Check(q.Width == 64 && q.GetPixel(0, 0).A == 0, "textura de la interrogacion con cantos transparentes");
            int visibles = 0; for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) if (q.GetPixel(x, y).A > 128) visibles++;
            Check(visibles > 200 && visibles < 64 * 64 / 2, "la interrogacion se ve y no es un bloque (" + visibles + ")");
        }
        bool rechaza = false; try { Run(gpu, "BakeInterrogacion", 2); } catch (ArgumentOutOfRangeException) { rechaza = true; }
        Check(rechaza, "tamano invalido rechazado");
        float minX = 9, maxX = -9; bool gira = false;
        for (int i = 0; i < 2000; i++)
        {
            object[] a = { i * .01, 0f, 0f, 0f, 0f, 0f }; Run(gpu, "AnimaInterrogacion", a);
            float bota = (float)a[1], giro = (float)a[2], ancho = (float)a[3], esc = (float)a[4], brillo = (float)a[5];
            Check(Finito(bota) && Finito(giro) && Finito(ancho) && Finito(esc) && Finito(brillo), "animacion finita");
            Check(bota <= 0 && bota >= -.11f && Math.Abs(giro) < .2f && esc > .9f && esc < 1.1f && brillo >= 0 && brillo <= 1, "animacion acotada");
            Check(Math.Abs(ancho) >= .079f && Math.Abs(ancho) <= 1f, "la vuelta de moneda nunca se aplasta a cero");
            minX = Math.Min(minX, ancho); maxX = Math.Max(maxX, ancho); if (ancho < 0) gira = true;
        }
        Check(gira && maxX > .99f, "de vez en cuando da la vuelta de moneda");
        foreach (double raro in new double[] { double.NaN, double.PositiveInfinity, -1e9 })
        {
            object[] a = { raro, 0f, 0f, 0f, 0f, 0f }; Run(gpu, "AnimaInterrogacion", a);
            Check(Finito((float)a[1]) && Finito((float)a[3]), "reloj raro sin NaN");
        }
    }

    static void Globo(string carpeta)
    {
        Size[] pantallas = { new Size(640, 480), new Size(1024, 768), new Size(1280, 720), new Size(1920, 1080), new Size(3440, 1440), new Size(3840, 2160) };
        Random r = new Random(7);
        Bitmap dibujo = carpeta != null ? new Bitmap(1024, 768, PixelFormat.Format32bppArgb) : null;
        Graphics gd = dibujo != null ? Graphics.FromImage(dibujo) : null;
        if (gd != null) { gd.Clear(Color.FromArgb(10, 20, 48)); gd.SmoothingMode = SmoothingMode.AntiAlias; }
        foreach (Size s in pantallas)
        {
            Size tam = (Size)Run(gpu, "AsistenteGuiaTam", s.Width, s.Height);
            Check(tam.Width > 0 && tam.Height > 0 && tam.Width <= s.Width && tam.Height <= s.Height * .3f + 1, "globo pequeno en " + s);
            float radio = (float)Run(gpu, "AsistenteGuiaRadio", tam.Height);
            for (int i = 0; i < 60; i++)
            {
                RectangleF o = i == 0 ? RectangleF.Empty : new RectangleF((float)r.NextDouble() * s.Width * .85f, (float)r.NextDouble() * s.Height * .8f,
                    20 + (float)r.NextDouble() * s.Width * .15f, 20 + (float)r.NextDouble() * s.Height * .12f);
                Rectangle g = (Rectangle)Run(gpu, "AsistenteGuiaRect", s.Width, s.Height, o);
                Check(g.Left >= 0 && g.Top >= 0 && g.Right <= s.Width && g.Bottom <= s.Height, "el globo cabe en la pantalla " + s);
                float ex = g.Left - radio * 1.4f - 6;
                Check(ex - radio * 1.35f >= -2, "la estrella (a la izquierda del globo) no se sale: " + s);
                if (o.Width > 0 && o.Width < s.Width * .3f && o.Height < s.Height * .25f)
                {
                    RectangleF cruce = RectangleF.Intersect(g, o);
                    Check(cruce.Width <= 0 || cruce.Height <= 0, "un objetivo pequeno nunca queda tapado por el globo: " + s + " " + o);
                }
                if (gd != null && s.Width == 1024 && i < 6)
                {
                    using (Pen p = new Pen(Color.FromArgb(200, 255, 200, 80), 2)) gd.DrawRectangle(p, o.X, o.Y, o.Width, o.Height);
                    using (Pen p = new Pen(Color.FromArgb(160, 140, 210, 255), 2)) gd.DrawRectangle(p, g);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(160, 220, 230, 255))) gd.FillEllipse(b, ex - radio, g.Top + g.Height * .46f - radio, radio * 2, radio * 2);
                }
            }
        }
        if (dibujo != null)
        {
            gd.Dispose();
            string ruta = System.IO.Path.Combine(carpeta, "ayudaguia-globos.png");
            dibujo.Save(ruta, ImageFormat.Png); dibujo.Dispose();
            Console.WriteLine("  dibujo: " + ruta);
        }
    }

    // El globo con memoria: no cruza la pantalla cada vez que el objetivo pasa del centro, y se muda solo cuando
    // taparia lo que hay que mirar (o la estrella lo taparia).
    static void GloboQuieto()
    {
        Size[] pantallas = { new Size(640, 480), new Size(1024, 768), new Size(1920, 1080), new Size(3840, 2160) };
        Random r = new Random(11);
        foreach (Size s in pantallas)
        {
            float radio = (float)Run(gpu, "AsistenteGuiaRadio", ((Size)Run(gpu, "AsistenteGuiaTam", s.Width, s.Height)).Height);
            // Un disco elegido que recorre la fila de arriba de punta a punta (el cliente moviendose): el globo
            // empieza en su esquina y, mientras no lo tape, no se mueve.
            Rectangle actual = (Rectangle)Run(gpu, "AsistenteGuiaRect", s.Width, s.Height, new RectangleF(s.Width * .1f, s.Height * .2f, s.Width * .1f, s.Height * .15f));
            int mudanzas = 0;
            for (int paso = 0; paso <= 40; paso++)
            {
                RectangleF o = new RectangleF(s.Width * (.05f + .8f * paso / 40f), s.Height * .2f, s.Width * .1f, s.Height * .15f);
                Rectangle sig = (Rectangle)Run(gpu, "AsistenteGuiaRectSigue", s.Width, s.Height, o, actual);
                if (sig != actual) mudanzas++;
                Check(Tapa(sig, radio, o) <= 0f, "el globo con memoria no tapa el disco que recorre la fila: " + s + " paso " + paso);
                actual = sig;
            }
            Check(mudanzas <= 1, "el globo no cruza la pantalla a cada disco (" + mudanzas + " mudanzas en " + s + ")");
            // Objetivos al azar: si la esquina de ahora no tapa, se queda; si tapa, se muda a una que no tape
            // (cuando el objetivo es pequeno) y siempre dentro de la pantalla.
            for (int i = 0; i < 200; i++)
            {
                RectangleF o = new RectangleF((float)r.NextDouble() * s.Width * .85f, (float)r.NextDouble() * s.Height * .8f,
                    20 + (float)r.NextDouble() * s.Width * .15f, 20 + (float)r.NextDouble() * s.Height * .12f);
                Rectangle sig = (Rectangle)Run(gpu, "AsistenteGuiaRectSigue", s.Width, s.Height, o, actual);
                Check(sig.Left >= 0 && sig.Top >= 0 && sig.Right <= s.Width && sig.Bottom <= s.Height, "el globo con memoria cabe en " + s);
                if (Tapa(actual, radio, o) <= 0f) Check(sig == actual, "si no tapa, el globo se queda donde esta: " + s);
                else if (o.Width < s.Width * .3f && o.Height < s.Height * .25f)
                    Check(Tapa(sig, radio, o) <= 0f, "si tapa, se muda a una esquina libre: " + s + " " + o);
                actual = sig;
            }
            // Un rectangulo que no es ninguna esquina (o de otro tamano de pantalla) no se conserva.
            Rectangle raro = new Rectangle(3, 5, actual.Width, actual.Height);
            Check((Rectangle)Run(gpu, "AsistenteGuiaRectSigue", s.Width, s.Height, RectangleF.Empty, raro) != raro, "fuera de esquina, el globo vuelve a la suya: " + s);
            Rectangle otro = new Rectangle(actual.X, actual.Y, actual.Width + 7, actual.Height);
            Check(((Rectangle)Run(gpu, "AsistenteGuiaRectSigue", s.Width, s.Height, RectangleF.Empty, otro)).Size != otro.Size, "otro tamano de pantalla, otro globo: " + s);
        }
    }
    static float Tapa(Rectangle g, float radio, RectangleF o)
    {
        RectangleF ocupa = new RectangleF(g.Left - radio * 2.9f, g.Top - radio * .2f, g.Width + radio * 2.9f, g.Height + radio * .4f);
        RectangleF c = RectangleF.Intersect(ocupa, RectangleF.Inflate(o, 10, 10));
        return c.Width > 0 && c.Height > 0 ? c.Width * c.Height : 0f;
    }

    static void Marco()
    {
        Array v = Array.CreateInstance(vtx, 72);
        RectangleF o = new RectangleF(100, 80, 200, 60);
        int n = (int)Run(gpu, "MarcoGuia", v, o, 3f, 12f, 1f);
        Check(n == 72, "marco completo en 72 vertices");
        for (int i = 0; i < n; i++)
        {
            float x = F(v.GetValue(i), "X"), y = F(v.GetValue(i), "Y");
            Check(Finito(x) && Finito(y) && x > o.Left - 20 && x < o.Right + 20 && y > o.Top - 20 && y < o.Bottom + 20, "el marco rodea el objetivo");
        }
        Check((int)Run(gpu, "MarcoGuia", v, o, 3f, 12f, 0f) == 0 && (int)Run(gpu, "MarcoGuia", Array.CreateInstance(vtx, 71), o, 3f, 12f, 1f) == 0
            && (int)Run(gpu, "MarcoGuia", v, RectangleF.Empty, 3f, 12f, 1f) == 0, "sin fuerza, sin sitio o con un arreglo corto no dibuja");
    }

    static void Flecha()
    {
        object[] e = { new PointF(10, 10), new RectangleF(100, 100, 50, 40), PointF.Empty };
        bool entra = (bool)Run(gpu, "EntradaRect", e); PointF pe = (PointF)e[2];
        Check(entra && (Math.Abs(pe.X - 100) < .5f || Math.Abs(pe.Y - 100) < .5f), "la punta queda en el borde del objetivo");
        e = new object[] { new PointF(120, 120), new RectangleF(100, 100, 50, 40), PointF.Empty };
        Check(!(bool)Run(gpu, "EntradaRect", e), "si ya esta encima, no hay flecha");
        Array v = Array.CreateInstance(vtx, 36);
        int n = (int)Run(gpu, "FlechaGuia", v, 300f, 200f, 1f, 0f, 60f, 1f);
        Check(n == 18, "flecha con borde: dos capas de cabeza y cuerpo (" + n + ")");
        for (int i = 0; i < n; i++)
        {
            float x = F(v.GetValue(i), "X"), y = F(v.GetValue(i), "Y");
            Check(x <= 304 && x >= 236 && Math.Abs(y - 200) < 30, "la flecha va hacia su punta y no mas alla");
        }
        Check((int)Run(gpu, "FlechaGuia", Array.CreateInstance(vtx, 35), 300f, 200f, 1f, 0f, 60f, 1f) == 0
            && (int)Run(gpu, "FlechaGuia", v, float.NaN, 200f, 1f, 0f, 60f, 1f) == 0, "arreglo corto o NaN: no dibuja");
    }

    static bool Llama(Type t, string metodo, string que)
    {
        foreach (MethodInfo m in t.GetMethods(B)) if (m.Name == metodo && Codigo.Llama(m, que)) return true;
        return false;
    }
    static void Enchufes()
    {
        Check(Llama(gpu, "RenderFrame", "GpuPanel.PreparaAyudaIcono") && Llama(gpu, "RenderFrame", "GpuPanel.DibujaInterrogacion"), "la interrogacion se prepara antes y se dibuja con la interfaz");
        Check(!Llama(gpu, "DibujaInterrogacion", "GpuPanel.BakeInterrogacion"), "no se hornea al dibujar");
        Check(Llama(form, "LayoutControls", "Form1.AcomodaAyuda"), "la casilla se acomoda al lado de BUSCAR");
        Check(Llama(form, "DrawGuiaRojo", "Form1.PintaGuiaRojoAyuda") && form.GetMethod("GuiaRojoFlechaAyuda", B) != null, "la guia del rojo senala tambien la ayuda");
        Check(Llama(gpu, "AvanzaAsistente", "GpuPanel.AvanzaGlobosGuia") && Llama(gpu, "AvanzaAsistente", "GpuPanel.AsistentePoseFrente"), "la estrella sigue a su globo");
        foreach (string m in new string[] { "DibujaFocoGuia", "DibujaFlechaGuia", "MarcoGuia", "FlechaGuia", "DibujaInterrogacion", "AvanzaGlobosGuia" })
            foreach (Codigo.Paso p in Codigo.Pasos(gpu.GetMethod(m, B))) Check(p.Op != 0x8D, "sin reservar arreglos al dibujar: " + m);
    }
}

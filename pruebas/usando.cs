using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LA ROCOLA MIENTRAS SUENA MUSICA Y ALGUIEN LA ESTA USANDO.
//
// Ese es el momento que aprieta: la maquina esta descomprimiendo el audio, reconstruyendole los agudos y
// nivelandolo, y ADEMAS alguien esta pulsando flechas buscando su cancion. Todo lo que sobre en el dibujo se
// lo esta quitando al sonido. Las demas baterias miden la rocola QUIETA; esta la mide EN USO.
//
// Lo que se encontro mirando el codigo: cada pulsacion de flecha en la pared de discos repinta LA PARED
// ENTERA -el cielo estrellado de fondo y las 18 casillas con sus portadas y sus letreros- cuando lo unico
// que cambia son DOS casillas: la que dejas y la que eliges. Con el freno de flechas eso son hasta 25
// repintados completos por segundo.
//
// Y la rocola ya sabe hacerlo bien en el mismo archivo: AnimTick, para el giro del cubo, dice "invalida lo
// MINIMO: en cuadros intermedios solo la casilla del cubo que gira". Al movimiento por flechas nunca se le
// aplico.
class Usando
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PUB = BindingFlags.Instance | BindingFlags.Public;

    static Type TF, TAV, TAlb, TStar;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static Image portada;
    static Image Tapa<T>(T a) { return portada; }

    static double Mide(int veces, Action a)
    {
        a(); a();
        double mejor = double.MaxValue;
        for (int t = 0; t < 7; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    // La pared de discos como en la rocola: con su cielo de fondo y con MUSICA SONANDO (el pulso es lo que
    // enciende el latido del fondo; sin el, el fondo ni se entera y la medida saldria de mentira).
    static Control MontaPared(int w, int h) { return MontaPared(w, h, null); }

    // cieloCompartido: para comparar pixel a pixel hay que darle a las dos paredes EL MISMO cielo, y
    // congelado. El cielo titila 5 veces por segundo por su cuenta, asi que dos dibujos separados por unas
    // decimas ya salen distintos aunque el programa este bien: eso no es lo que se esta midiendo.
    static Control MontaPared(int w, int h, object cieloCompartido)
    {
        Control av = (Control)Activator.CreateInstance(TAV, true);
        av.SetBounds(0, 0, w, h);
        av.Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold);
        Type tf = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        TAV.GetField("GetCover").SetValue(av, Delegate.CreateDelegate(tf,
            typeof(Usando).GetMethod("Tapa", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
        IList pag = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
        for (int i = 0; i < 18; i++)
        {
            object a = Activator.CreateInstance(TAlb, true);
            TAlb.GetField("Folder").SetValue(a, @"C:\Musica\Disco" + i);
            TAlb.GetField("Name").SetValue(a, "Disco numero " + i + " con nombre largo");
            TAlb.GetField("Artist").SetValue(a, "Artista " + i);
            TAlb.GetField("Letter").SetValue(a, 'A');
            TAlb.GetField("CoverPath").SetValue(a, @"C:\Musica\Disco" + i + @"\cover.jpg");
            pag.Add(a);
        }
        TAV.GetField("Page").SetValue(av, pag);
        TAV.GetField("Sky").SetValue(av, cieloCompartido ?? Activator.CreateInstance(TStar, true));
        TAV.GetField("Selected").SetValue(av, 7);
        // MUSICA SONANDO: el pulso por bandas que hace latir el fondo.
        foreach (string p in new string[] { "PulseLo", "PulseMid", "PulseHi" })
        { FieldInfo f = TAV.GetField(p); if (f != null) f.SetValue(av, 0.45f); }
        return av;
    }

    static MethodInfo mRender, mMove;

    // Dibuja la pared con el recorte puesto: el recorte es justo lo que decide si se repinta todo o solo
    // un trozo (la propia pared mira g.ClipBounds para saberlo).
    static void Pinta(Control av, Graphics g, Rectangle recorte)
    {
        g.ResetClip();
        if (recorte.Width > 0 && recorte.Height > 0) g.SetClip(recorte);
        Rectangle cl = Rectangle.Round(g.ClipBounds);
        using (PaintEventArgs pe = new PaintEventArgs(g, cl)) mRender.Invoke(av, new object[] { pe });
    }
    static void Pinta(Control av, Graphics g) { Pinta(av, g, Rectangle.Empty); }

    static int Difiere(Bitmap a, Bitmap b, out int peor, out Rectangle zona)
    {
        peor = 0; int n = 0;
        int x0 = a.Width, y0 = a.Height, x1 = -1, y1 = -1;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                int d = Math.Max(Math.Max(Math.Abs(p.R - q.R), Math.Abs(p.G - q.G)),
                        Math.Max(Math.Abs(p.B - q.B), Math.Abs(p.A - q.A)));
                if (d <= 0) continue;
                n++; if (d > peor) peor = d;
                if (x < x0) x0 = x; if (x > x1) x1 = x;
                if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
        zona = (n == 0) ? Rectangle.Empty : Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
        return n;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TAV = Nes("AlbumView"); TAlb = Nes("Album"); TStar = Nes("Starfield");
        Check(TAV != null && TAlb != null && TStar != null, "la pared de discos y el cielo estan donde tienen que estar");
        if (TAV == null || TAlb == null || TStar == null) return Math.Max(1, fallos);
        // La pared no tiene un RenderContent aparte: se pinta por su propio OnPaint, que es lo que hace la
        // pantalla de verdad. Se le pide ese, con su recorte, que es justo lo que se quiere medir.
        mRender = TAV.GetMethod("OnPaint", IF);
        mMove = TAV.GetMethod("MoveSelectionTo", PUB);
        Check(mRender != null && mMove != null, "se le puede pedir que se dibuje y que mueva la seleccion");
        if (mRender == null || mMove == null) return Math.Max(1, fallos);

        portada = new Bitmap(500, 500, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada))
        {
            pg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 60; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) pg.FillEllipse(sb, i * 8, i * 7, 70, 70);
        }

        // ---------- 1) LA ROCOLA SABE DECIR QUE PARTE CAMBIA AL MOVERSE ----------
        Console.WriteLine("\n  1) AL MOVER LA SELECCION, QUE PARTE DE LA PARED CAMBIA\n");
        MethodInfo mRect = TAV.GetMethod("RectSeleccion", PUB) ?? TAV.GetMethod("RectSeleccion", IF);
        Check(mRect != null, "la pared sabe decir que trozo cambia al mover la seleccion (RectSeleccion)");
        MethodInfo mSet = TF.GetMethod("SetSelectedLocal", IF);
        Check(mSet != null && Codigo.Llama(mSet, "RectSeleccion"),
              "y al mover con las flechas se repinta SOLO ese trozo, no la pared entera");

        // ---------- 2) LO QUE CUESTA CADA PULSACION ----------
        Console.WriteLine("\n  2) LO QUE CUESTA CADA PULSACION DE FLECHA, CON MUSICA SONANDO\n");
        Console.WriteLine(string.Format("     {0,-12} {1,14} {2,14} {3,10}  {4}", "pantalla", "la pared entera", "solo lo que cambia", "gana", "pantalla de mas"));
        Console.WriteLine("     " + new string('-', 78));
        int[] anchos = { 1024, 1280 };
        int[] altos = { 768, 1024 };
        double peorRazon = 0;
        for (int k = 0; k < anchos.Length; k++)
        {
            int W = anchos[k], H = altos[k];
            Control av = MontaPared(W, H);
            Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            Graphics g = Graphics.FromImage(b);
            Pinta(av, g); Pinta(av, g);   // que quede todo horneado antes de medir

            int sel = 7;
            double entera = Mide(40, delegate
            {
                sel = (sel == 7) ? 8 : 7;
                mMove.Invoke(av, new object[] { sel, 1 });
                Pinta(av, g);
            });

            Rectangle trozo = Rectangle.Empty;
            double solo = -1;
            if (mRect != null)
            {
                solo = Mide(40, delegate
                {
                    sel = (sel == 7) ? 8 : 7;
                    mMove.Invoke(av, new object[] { sel, 1 });
                    Pinta(av, g, (Rectangle)mRect.Invoke(av, null));
                });
                mMove.Invoke(av, new object[] { 8, 1 });
                trozo = (Rectangle)mRect.Invoke(av, null);
            }
            else
            {
                // Sin el arreglo no hay a quien preguntarle: se usa el trozo que de verdad cambia, calculado
                // aqui a mano, para poder ENSENAR lo que se ganaria.
                MethodInfo mSpin = TAV.GetMethod("SpinRect", IF);
                mMove.Invoke(av, new object[] { 8, 1 });
                trozo = (mSpin != null) ? (Rectangle)mSpin.Invoke(av, null) : new Rectangle(0, 0, W / 3, H / 3);
                Rectangle tr = trozo;
                solo = Mide(40, delegate
                {
                    sel = (sel == 7) ? 8 : 7;
                    mMove.Invoke(av, new object[] { sel, 1 });
                    Pinta(av, g, tr);
                });
            }
            double razon = entera / Math.Max(0.0001, solo);
            if (razon > peorRazon) peorRazon = razon;
            double demas = 100.0 * (1.0 - (double)trozo.Width * trozo.Height / ((double)W * H));
            Console.WriteLine(string.Format("     {0,-12} {1,11:0.00} ms {2,11:0.00} ms {3,9} {4,10:0}%",
                              W + "x" + H, entera, solo, "x" + razon.ToString("0.0"), demas));
            g.Dispose(); b.Dispose();
        }
        Console.WriteLine("     " + new string('-', 78));
        Console.WriteLine("     con el freno de flechas son hasta 25 pulsaciones por segundo, y en la del bar todo esto");
        Console.WriteLine("     cuesta 4 o 5 veces mas");
        Check(peorRazon >= 2.0, "repintar solo lo que cambia cuesta menos de la mitad que repintar la pared entera");

        // ---------- 3) Y TIENE QUE SALIR IDENTICO ----------
        Console.WriteLine("\n  3) Y REPINTANDO SOLO ESE TROZO, LA PARED SALE IGUAL\n");
        // Es la que manda. Se dibuja la pared con la seleccion en A; se mueve a B y se repinta SOLO el trozo;
        // y se compara contra la pared dibujada entera con la seleccion ya en B. Si el trozo se queda corto
        // -por ejemplo si deja fuera el LETRERO del disco, que tambien cambia al elegirlo- aqui salta.
        if (mRect != null)
        {
            // PARA COMPARAR PIXELES HAY QUE PARAR LOS RELOJES DEL CIELO. Dos cosas del fondo cambian solas
            // con el tiempo y no tienen nada que ver con lo que se esta midiendo: el TITILEO (rota entre varias
            // capas horneadas, 5 veces por segundo) y las ESTRELLAS FUGACES (una cada 7-16 segundos). Sin
            // pararlas, dos dibujos separados por unas decimas ya salen distintos aunque el programa este bien.
            FieldInfo fCapas = TStar.GetField("layers", IF);
            FieldInfo fProx = TStar.GetField("nextShoot", IF);
            FieldInfo fShoots = TStar.GetField("shoots", IF);
            for (int k = 0; k < anchos.Length; k++)
            {
                int W = anchos[k], H = altos[k];
                object cielo = Activator.CreateInstance(TStar, true);
                Control a1 = MontaPared(W, H, cielo), a2 = MontaPared(W, H, cielo);
                using (Bitmap tmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
                using (Graphics tg = Graphics.FromImage(tmp)) Pinta(a1, tg);   // un dibujo para que el cielo se arme
                if (fCapas != null)
                {
                    Array cap = (Array)fCapas.GetValue(cielo);
                    if (cap != null && cap.Length > 1)
                    {
                        Array una = Array.CreateInstance(cap.GetType().GetElementType(), 1);
                        una.SetValue(cap.GetValue(0), 0);
                        fCapas.SetValue(cielo, una);   // una sola capa: el titileo se queda quieto
                    }
                }
                if (fProx != null) fProx.SetValue(cielo, Environment.TickCount + 600000);   // sin fugaces nuevas
                if (fShoots != null)
                {
                    Array sh = (Array)fShoots.GetValue(cielo);
                    if (sh != null) fShoots.SetValue(cielo, Array.CreateInstance(sh.GetType().GetElementType(), sh.Length));
                }
                Bitmap entera = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
                Bitmap partes = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(entera))
                {
                    mMove.Invoke(a1, new object[] { 8, 1 });
                    Pinta(a1, g); Pinta(a1, g);
                }
                using (Graphics g = Graphics.FromImage(partes))
                {
                    Pinta(a2, g); Pinta(a2, g);                       // la pared con la seleccion en 7
                    mMove.Invoke(a2, new object[] { 8, 1 });          // el cliente pulsa la flecha
                    Pinta(a2, g, (Rectangle)mRect.Invoke(a2, null));  // y se repinta SOLO ese trozo
                }
                int peor; Rectangle zona;
                int dif = Difiere(entera, partes, out peor, out zona);
                Console.WriteLine("     " + W + "x" + H + ":  pixeles distintos " + dif.ToString("#,0")
                                  + "   el peor, " + peor + " niveles de 255"
                                  + (dif > 0 ? ("   se quedaron viejos en " + zona) : ""));
                Check(dif == 0, "  la pared sale EXACTAMENTE igual   [" + W + "x" + H + "]");
                entera.Dispose(); partes.Dispose();
            }
        }

        // ---------- 4) LAS SOBRAS DE UN CUADRO MIENTRAS SE NAVEGA ----------
        Console.WriteLine("\n  4) LO QUE RESERVA UN CUADRO MIENTRAS ALGUIEN SE MUEVE\n");
        // 'sobras' vigila que un cuadro EN REPOSO no reserve ni un byte. Pero el cuadro que importa es el de
        // cuando se usa: cada reserva acaba en una recogida de basura, y eso es un tiron justo mientras
        // alguien se esta moviendo.
        {
            int W = 1024, H = 768;
            Control av = MontaPared(W, H);
            using (Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                int sel = 7;
                for (int i = 0; i < 30; i++) { sel = (sel == 7) ? 8 : 7; mMove.Invoke(av, new object[] { sel, 1 }); Pinta(av, g); }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long a0 = GC.GetTotalMemory(true);
                const int N = 60;
                for (int i = 0; i < N; i++) { sel = (sel == 7) ? 8 : 7; mMove.Invoke(av, new object[] { sel, 1 }); Pinta(av, g); }
                long a1 = GC.GetTotalMemory(false);
                long porCuadro = (a1 - a0) / N;
                Console.WriteLine("     un cuadro navegando reserva " + porCuadro.ToString("#,0") + " bytes"
                                  + "   (a 25 por segundo: " + (porCuadro * 25 / 1024).ToString("#,0") + " KB por segundo)");
                // El movimiento por reflexion tambien reserva algo; se admite un poco de margen.
                Check(porCuadro < 8192, "navegando, un cuadro no deja un monton de sobras para el recolector");
            }
        }

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}

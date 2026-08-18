using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// POR QUE EL BUSCADOR VA PEOR CON MUSICA SONANDO.
//
// Con musica, el fondo estrellado LATE: cada estrella agranda su resplandor al ritmo del bombo y el campo
// entero se desplaza. Eso obliga a redibujar el fondo COMPLETO, y el buscador lo redibuja veinticuatro veces
// por segundo a pantalla completa. Aqui se mide ese fondo con y sin musica, y a los tamanos de pantalla de
// las dos maquinas, para ver si es eso lo que se lleva el tiempo.
class Sonando
{
    static void Fila(string q, double ms) { Console.WriteLine(string.Format("     {0,-50} {1,8:0.00} ms   {2,5:0.0}% de un cuadro de 24/seg (41,7 ms)", q, ms, 100.0 * ms / 41.666)); }

    static double Mide(int veces, Action a)
    {
        a(); a();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor = double.MaxValue;
        for (int t = 0; t < 5; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        return mejor;
    }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        Type TSO = null, TItem = null, TAlb = null, TStar = null, TCubeBox = null;
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SearchOverlay") TSO = t;
            if (t.Name == "SearchItem") TItem = t;
            if (t.Name == "Album") TAlb = t;
            if (t.Name == "Starfield") TStar = t;
            if (t.Name == "CubeBox") TCubeBox = t;
        }
        Console.WriteLine("\n  midiendo: " + exe);

        Bitmap portada = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada))
        {
            pg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 40; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) pg.FillEllipse(sb, i * 7, i * 6, 60, 60);
        }

        // ---------- 1) EL FONDO ESTRELLADO A PANTALLA COMPLETA ----------
        Console.WriteLine("\n  1) EL FONDO ESTRELLADO DEL BUSCADOR, REHECHO ENTERO\n");
        MethodInfo mDraw = TStar.GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo fLast = TStar.GetField("lastBuild", BindingFlags.Instance | BindingFlags.NonPublic);
        int[,] pantallas = { { 1024, 768 }, { 1366, 768 }, { 1920, 1080 } };
        for (int p = 0; p < pantallas.GetLength(0); p++)
        {
            int w = pantallas[p, 0], h = pantallas[p, 1];
            object st = Activator.CreateInstance(TStar, true);
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                double[] fase = new double[] { 0 };
                // Se fuerza la reconstruccion completa en cada llamada (es lo que pasa 24 veces por segundo
                // cuando el buscador esta abierto: el buscador se repinta entero en cada cuadro).
                Action<float> uno = delegate (float pulso)
                {
                    fase[0] += 0.045;
                    fLast.SetValue(st, Environment.TickCount - 10000);
                    mDraw.Invoke(st, new object[] { g, w, h, Color.FromArgb(255, 6, 8, 26), Color.FromArgb(255, 26, 44, 90),
                                                    Color.FromArgb(178, 152, 255), fase[0], true, pulso });
                };
                double sin = Mide(25, delegate { uno(0f); });
                double con = Mide(25, delegate { uno(1f); });
                Console.WriteLine(string.Format("     {0}x{1}:   en silencio {2,6:0.00} ms      con musica {3,6:0.00} ms   ({4:0.0}x)   -> {5:0}% y {6:0}% de un cuadro",
                                                w, h, sin, con, con / Math.Max(sin, 0.001), 100.0 * sin / 41.666, 100.0 * con / 41.666));
            }
        }

        // ---------- 2) EL CUBO QUE GIRA ----------
        Console.WriteLine("\n  2) LOS CUBOS QUE GIRAN (solo giran con algo sonando o con el buscador abierto)\n");
        Type TCube = TF.GetNestedType("Cube", BindingFlags.NonPublic | BindingFlags.Public);
        if (TCube != null)
        {
            MethodInfo mCube = TCube.GetMethod("Draw", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (mCube != null)
            {
                int[] lados = { 90, 190, 320 };
                using (Bitmap b = new Bitmap(700, 700, PixelFormat.Format32bppPArgb))
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    foreach (int lado in lados)
                    {
                        double[] ang = new double[] { 0 };
                        object[] ar = new object[] { g, new Rectangle(10, 10, lado, lado), (Image)portada, 0.0 };
                        double t = Mide(60, delegate { ang[0] += 0.06; ar[3] = ang[0]; mCube.Invoke(null, ar); });
                        Fila("un cubo de " + lado + " px con su caratula", t);
                    }
                }
            }
        }

        // ---------- 3) EL BUSCADOR ENTERO, TAL CUAL SE REPINTA CADA CUADRO ----------
        Console.WriteLine("\n  3) EL BUSCADOR ENTERO EN CADA CUADRO (version sin tarjeta grafica)\n");
        for (int p = 0; p < pantallas.GetLength(0); p++)
        {
            int w = pantallas[p, 0], h = pantallas[p, 1];
            Control so = (Control)Activator.CreateInstance(TSO, true);
            so.SetBounds(0, 0, w, h);
            so.Font = new Font(Fam(), 12f, FontStyle.Bold);
            object alb = Activator.CreateInstance(TAlb, true);
            TAlb.GetField("Folder").SetValue(alb, "D"); TAlb.GetField("Name").SetValue(alb, "Un disco");
            TAlb.GetField("Artist").SetValue(alb, "Un artista"); TAlb.GetField("CoverPath").SetValue(alb, "c.jpg");
            Type tl = typeof(List<>).MakeGenericType(TItem);
            IList res = (IList)Activator.CreateInstance(tl);
            for (int i = 0; i < 40; i++)
            {
                object it = Activator.CreateInstance(TItem, true);
                TItem.GetField("Kind").SetValue(it, i % 3);
                TItem.GetField("Display").SetValue(it, "Depeche Mode - Cancion numero " + i);
                TItem.GetField("Key").SetValue(it, "depeche mode - cancion numero " + i);
                TItem.GetField("AlbumRef").SetValue(it, alb);
                res.Add(it);
            }
            TSO.GetField("Results").SetValue(so, res);
            TSO.GetField("Query").SetValue(so, "de");
            TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
            TSO.GetField("FocusResults").SetValue(so, true);
            TSO.GetField("SelResult").SetValue(so, 3);
            laPortada = portada;
            Type tfc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
            TSO.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(tfc, typeof(Sonando).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
            FieldInfo fYa = TSO.GetField("GetCoverYa");
            if (fYa != null) fYa.SetValue(so, Delegate.CreateDelegate(tfc, typeof(Sonando).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
            FieldInfo fPulse = TSO.GetField("AudioPulse"), fPhase = TSO.GetField("Phase"), fAng = TSO.GetField("CubeAngle");
            FieldInfo fStars = TSO.GetField("stars", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo mR = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });

            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                object[] ar = new object[] { g, new Rectangle(0, 0, w, h), false };
                double[] ph = new double[] { 0 };
                Action<float> cuadro = delegate (float pulso)
                {
                    ph[0] += 0.045;
                    fPhase.SetValue(so, ph[0]);
                    fPulse.SetValue(so, pulso);
                    fAng.SetValue(so, (double)fAng.GetValue(so) + 0.075);
                    object st = fStars.GetValue(so);
                    if (st != null && fLast != null) fLast.SetValue(st, Environment.TickCount - 10000);   // como si hubiera pasado un cuadro
                    mR.Invoke(so, ar);
                };
                double sin = Mide(20, delegate { cuadro(0f); });
                double con = Mide(20, delegate { cuadro(1f); });
                // Y el cuadro PARCIAL: solo la franja de las ayudas y la casilla del cubo, que es lo unico que
                // se mueve. Es lo que se repinta dos de cada tres veces desde que se reparte el trabajo.
                double parcial = con;
                MethodInfo mViv = TSO.GetMethod("InvalidarVivo", BindingFlags.Instance | BindingFlags.Public);
                if (mViv != null)
                {
                    // Los MISMOS trozos que marca el programa: la franja de ayudas y la casilla del cubo.
                    int M2 = 30, kbW2 = (int)((w - 2 * M2) * 0.52);
                    int der2 = Math.Min(w, M2 + kbW2 + 10);
                    Region zona = new Region(new Rectangle(0, M2 + 46, der2, 112));
                    Rectangle viva = new Rectangle(0, M2 + 46, der2, 112);
                    FieldInfo fCubo = TSO.GetField("cubeRect", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (fCubo != null)
                    {
                        Rectangle cr = (Rectangle)fCubo.GetValue(so);
                        if (cr.Width > 1) { cr.Inflate(26, 84); zona.Union(cr); viva = Rectangle.Union(viva, cr); }
                    }
                    object[] arP = new object[] { g, viva, false };
                    parcial = Mide(20, delegate
                    {
                        ph[0] += 0.045; fPhase.SetValue(so, ph[0]); fPulse.SetValue(so, 1f);
                        fAng.SetValue(so, (double)fAng.GetValue(so) + 0.075);
                        object st2 = fStars.GetValue(so);
                        if (st2 != null && fLast != null) fLast.SetValue(st2, Environment.TickCount - 10000);
                        g.Clip = zona;          // solo los trozos, no el rectangulo que los engloba
                        mR.Invoke(so, arP);
                        g.ResetClip();
                    });
                }
                double medio = (con + 2 * parcial) / 3.0;
                Console.WriteLine(string.Format("     {0}x{1}:   silencio {2,6:0.00}   musica {3,6:0.00}   parcial {4,6:0.00}   -> media por cuadro {5,6:0.00} ms ({6:0}%)",
                                                w, h, sin, con, parcial, medio, 100.0 * medio / 41.666));
            }
        }
        // ---------- 4) LA BASURA QUE DEJA CADA REPINTADO ----------
        // Cada objeto que se crea y se tira en un repintado va al monton del recolector. Si en cada cuadro se
        // dejan cientos de kilobytes, el recolector entra a limpiar cada pocos segundos y ESO se ve como un
        // tiron seco. Aqui se mide cuanta memoria queda por repintado.
        Console.WriteLine("\n  4) LA BASURA QUE DEJA CADA REPINTADO\n");
        {
            int w = 1024, h = 768;
            Control so = (Control)Activator.CreateInstance(TSO, true);
            so.SetBounds(0, 0, w, h);
            so.Font = new Font(Fam(), 12f, FontStyle.Bold);
            object alb = Activator.CreateInstance(TAlb, true);
            TAlb.GetField("Folder").SetValue(alb, "D"); TAlb.GetField("Name").SetValue(alb, "Un disco");
            TAlb.GetField("Artist").SetValue(alb, "Un artista"); TAlb.GetField("CoverPath").SetValue(alb, "c.jpg");
            Type tl = typeof(List<>).MakeGenericType(TItem);
            IList res = (IList)Activator.CreateInstance(tl);
            for (int i = 0; i < 40; i++)
            {
                object it = Activator.CreateInstance(TItem, true);
                TItem.GetField("Kind").SetValue(it, i % 3);
                TItem.GetField("Display").SetValue(it, "Depeche Mode - Cancion numero " + i);
                TItem.GetField("Key").SetValue(it, "depeche mode - cancion numero " + i);
                TItem.GetField("AlbumRef").SetValue(it, alb);
                res.Add(it);
            }
            TSO.GetField("Results").SetValue(so, res);
            TSO.GetField("Query").SetValue(so, "de");
            TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
            TSO.GetField("FocusResults").SetValue(so, true);
            TSO.GetField("SelResult").SetValue(so, 3);
            laPortada = portada;
            Type tfc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
            TSO.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(tfc, typeof(Sonando).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
            FieldInfo fYa2 = TSO.GetField("GetCoverYa");
            if (fYa2 != null) fYa2.SetValue(so, Delegate.CreateDelegate(tfc, typeof(Sonando).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
            FieldInfo fPh2 = TSO.GetField("Phase"), fAn2 = TSO.GetField("CubeAngle");
            FieldInfo fSt2 = TSO.GetField("stars", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo mR2 = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                object[] ar = new object[] { g, new Rectangle(0, 0, w, h), false };
                double ph = 0;
                for (int i = 0; i < 30; i++) { ph += 0.045; fPh2.SetValue(so, ph); mR2.Invoke(so, ar); }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long antes = GC.GetTotalMemory(false);
                int g0 = GC.CollectionCount(0);
                const int N = 300;
                for (int i = 0; i < N; i++)
                {
                    ph += 0.045; fPh2.SetValue(so, ph);
                    fAn2.SetValue(so, (double)fAn2.GetValue(so) + 0.075);
                    object st = fSt2.GetValue(so);
                    if (st != null && fLast != null) fLast.SetValue(st, Environment.TickCount - 10000);
                    mR2.Invoke(so, ar);
                }
                long despues = GC.GetTotalMemory(false);
                int g0b = GC.CollectionCount(0);
                Console.WriteLine("     basura por repintado: " + ((despues - antes) / N) + " bytes   (y " + (g0b - g0) + " limpiezas del recolector en " + N + " repintados)");
                Console.WriteLine("     a 24 repintados por segundo eso son " + (((despues - antes) / N) * 24 / 1024) + " KB por segundo");
            }
        }

        Console.WriteLine("\n  (un cuadro son 41,7 ms: el buscador se repinta 24 veces por segundo)");
        Console.WriteLine("  (y el ordenador del bar es unas 4 veces mas lento que esta maquina)\n");
        return 0;
    }

    static Bitmap laPortada;
    static Image Cov<T>(T a) { return laPortada; }
}

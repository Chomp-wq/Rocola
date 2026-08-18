using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// BANCO DE RENDIMIENTO. Mide lo que de verdad cuesta cada cuadro, en microsegundos y en basura generada.
//
// La rocola de la casa dibuja a 60 cuadros por segundo: cada cuadro tiene 16.666 microsegundos de presupuesto
// (16 ms). La del bar es un Pentium con dos nucleos, asi que ahi cada microsegundo que se ahorre cuenta doble.
// Lo que se mide aqui no es "rapido o lento" en abstracto: es QUE PARTE del presupuesto se lleva cada cosa.
class Perf
{
    static Type tForm, tBarra, tCab, tLista, tProg;
    static FontFamily fam;

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    class Medida { public double Us; public long Bytes; }

    // Corre 'veces' iteraciones y devuelve microsegundos por iteracion y bytes de basura por iteracion.
    // Se corren VARIAS tandas y se toma la MEJOR. Medir tiempos en una maquina compartida es ruidoso: si otro
    // proceso se lleva el turno a mitad de una tanda, esa tanda sale el triple de lenta. El promedio se lo
    // traga; el MINIMO no, porque el minimo es "lo que cuesta cuando nadie estorba", que es lo que se quiere
    // comparar entre el antes y el despues.
    const int TANDAS = 7;
    static Medida Mide(int veces, Action a)
    {
        a(); a();                                   // calentar (JIT y caches)
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double mejor = double.MaxValue;
        long bytes = 0;
        for (int t = 0; t < TANDAS; t++)
        {
            long b0 = GC.GetTotalMemory(false);
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            long b1 = GC.GetTotalMemory(false);
            double us = sw.Elapsed.TotalMilliseconds * 1000.0 / veces;
            if (us < mejor) { mejor = us; bytes = (b1 - b0) / veces; }
        }
        Medida m = new Medida();
        m.Us = mejor; m.Bytes = bytes;
        return m;
    }

    static void Fila(string que, Medida m)
    {
        double pct = m.Us / 16666.0 * 100.0;
        Console.WriteLine(string.Format("     {0,-46} {1,9:0.0} us  {2,8} B   {3,6:0.00}% del cuadro",
                                        que, m.Us, m.Bytes, pct));
    }

    static string LETRAS = "★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";

    static int Main(string[] argv)
    {
        fam = Fam();
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Console.WriteLine("\n  midiendo: " + exe);
        Assembly asm = Assembly.LoadFrom(exe);
        tForm = asm.GetType("Rocola.Form1", true);
        foreach (Type t in tForm.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "LetterBar") tBarra = t;
            if (t.Name == "HeaderView") tCab = t;
            if (t.Name == "SongListView") tLista = t;
            if (t.Name == "ProgressView") tProg = t;
        }

        Console.WriteLine("\n  PRESUPUESTO: 16666 us por cuadro a 60 por segundo\n");

        // ---------- la barra de letras, en reposo (lo que hace CADA cuadro sin que nada cambie) ----------
        {
            Control bar = (Control)Activator.CreateInstance(tBarra, true);
            bar.SetBounds(0, 0, 996, 40);
            bar.Font = new Font(fam, 9f);
            tBarra.GetField("Letters").SetValue(bar, LETRAS);
            tBarra.GetProperty("Current").SetValue(bar, 'M', null);
            System.Threading.Thread.Sleep(240);
            HashSet<char> disp = new HashSet<char>();
            Dictionary<char, int> cu = new Dictionary<char, int>();
            Dictionary<char, Color> ti = new Dictionary<char, Color>();
            foreach (char c in LETRAS) { disp.Add(c); cu[c] = 137; ti[c] = Color.FromArgb(225, 120, 30); }
            tBarra.GetField("Available").SetValue(bar, disp);
            tBarra.GetField("Counts").SetValue(bar, cu);
            tBarra.GetField("Tints").SetValue(bar, ti);
            MethodInfo rc = tBarra.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
            MethodInfo sig = tBarra.GetMethod("DatosSig");
            using (Bitmap bmp = new Bitmap(996, 40, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                object[] args = new object[] { g, true };
                Console.WriteLine("  LA BARRA DE LETRAS, en reposo (la tira ya esta cacheada)\n");
                Fila("dibujarla entera", Mide(600, delegate { rc.Invoke(bar, args); }));
                Fila("solo su firma de datos (DatosSig)", Mide(20000, delegate { sig.Invoke(bar, null); }));
            }
        }

        // ---------- el encabezado ----------
        {
            Control c = (Control)Activator.CreateInstance(tCab, true);
            c.SetBounds(0, 0, 500, 34);
            c.Font = new Font(fam, 9f);
            tCab.GetField("Section").SetValue(c, "LETRA A");
            tCab.GetField("Page").SetValue(c, 2);
            tCab.GetField("Total").SetValue(c, 9);
            tCab.GetField("Accent").SetValue(c, Color.FromArgb(225, 120, 30));
            MethodInfo rc = tCab.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            using (Bitmap bmp = new Bitmap(500, 34, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                object[] args = new object[] { g };
                Console.WriteLine("\n  EL ENCABEZADO DE LA SECCION\n");
                Fila("dibujarlo entero", Mide(2000, delegate { rc.Invoke(c, args); }));
            }
        }

        // ---------- la lista de canciones (dentro de un disco) ----------
        {
            Control c = (Control)Activator.CreateInstance(tLista, true);
            c.SetBounds(0, 0, 620, 400);
            c.Font = new Font(fam, 11f, FontStyle.Bold);
            tLista.GetField("RowH").SetValue(c, 34);
            tLista.GetField("Sel").SetValue(c, 3);
            tLista.GetField("Accent").SetValue(c, Color.FromArgb(178, 152, 255));
            tLista.GetField("GetCount").SetValue(c, new Func<int>(delegate { return 200; }));
            tLista.GetField("GetText").SetValue(c, new Func<int, string>(delegate (int i) { return "Cancion numero " + i; }));
            tLista.GetField("GetRight").SetValue(c, new Func<int, string>(delegate (int i) { return "4:32"; }));
            FieldInfo fq = tLista.GetField("GetQueuePos");
            if (fq != null) fq.SetValue(c, new Func<int, int>(delegate (int i) { return (i % 4 == 1) ? (i / 4 + 1) : 0; }));
            // El genero de cada cancion, como en el disco de verdad: casi siempre el mismo (el del disco),
            // y de vez en cuando otro (los discos de LO MAS SONADO mezclan canciones de discos distintos).
            FieldInfo fg = tLista.GetField("GetGenero");
            if (fg != null) fg.SetValue(c, new Func<int, string>(delegate (int i) {
                return (i % 7 == 3) ? "Rock" : (i % 11 == 5) ? "Cumbia" : "Balada"; }));
            MethodInfo rc = tLista.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            using (Bitmap bmp = new Bitmap(620, 400, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                object[] args = new object[] { g };
                Console.WriteLine("\n  LA LISTA DE CANCIONES (11 filas visibles)\n");
                Fila("dibujarla entera", Mide(400, delegate { rc.Invoke(c, args); }));
            }
        }

        // ---------- la barra de duracion ----------
        {
            Control c = (Control)Activator.CreateInstance(tProg, true);
            c.SetBounds(0, 0, 600, 12);
            tProg.GetProperty("Value").SetValue(c, 0.67, null);
            tProg.GetProperty("Accent").SetValue(c, Color.FromArgb(178, 152, 255), null);
            MethodInfo rc = tProg.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            using (Bitmap bmp = new Bitmap(600, 12, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                object[] args = new object[] { g };
                Console.WriteLine("\n  LA BARRA DE DURACION\n");
                Fila("dibujarla", Mide(4000, delegate { rc.Invoke(c, args); }));
            }
        }

        // ---------- el fondo: la aurora ----------
        {
            object f = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(tForm);
            FieldInfo fSpec = tForm.GetField("specVis", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo mAvanza = tForm.GetMethod("AvanzaAurora", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fSpec != null && mAvanza != null)
            {
                FieldInfo fBands = tForm.GetField("SpecBands", BindingFlags.Static | BindingFlags.NonPublic);
                int n = (fBands != null) ? (int)fBands.GetValue(null) : 64;
                foreach (FieldInfo fi in tForm.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (fi.FieldType == typeof(float[]) && fi.GetValue(f) == null) fi.SetValue(f, new float[n]);
                    if (fi.FieldType == typeof(double[]) && fi.GetValue(f) == null) fi.SetValue(f, new double[n]);
                }
                float[] sv = (float[])fSpec.GetValue(f);
                Random r = new Random(3);
                for (int i = 0; i < sv.Length; i++) sv[i] = (float)r.NextDouble();
                Console.WriteLine("\n  EL FONDO REACTIVO\n");
                try { Fila("avanzar la aurora un cuadro", Mide(20000, delegate { mAvanza.Invoke(f, null); })); }
                catch (Exception e) { Console.WriteLine("     (no se pudo medir: " + e.GetType().Name + ")"); }
            }
        }

        // ---------- LO QUE DE VERDAD CORRE CADA CUADRO: armar los vertices ----------
        // El fondo no se cachea: sus triangulos se rehacen enteros en cada cuadro, porque se mueven. Esto es
        // el nucleo del gasto, y se llama 60 veces por segundo pase lo que pase.
        {
            Type tPanel = null, tD3D = null;
            foreach (Type t in asm.GetTypes())
            {
                if (t.Name == "GpuPanel") tPanel = t;
                if (t.Name == "D3D9") tD3D = t;
            }
            if (tPanel != null && tD3D != null)
            {
                Type tVtx = null;
                foreach (Type t in tD3D.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "Vtx") tVtx = t;
                MethodInfo mMalla = tPanel.GetMethod("AurMalla", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                MethodInfo mBrillo = tPanel.GetMethod("AurBrillo", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                MethodInfo mStars = tPanel.GetMethod("BuildStarVerts", BindingFlags.Static | BindingFlags.NonPublic);
                Console.WriteLine("\n  EL FONDO, VERTICE A VERTICE  (esto SI corre cada cuadro)\n");

                int cols = 144, puntos = cols + 1;
                if (mMalla != null && tVtx != null)
                {
                    // (AurFilas 6 + AurSuelo 4) x 144 columnas x 6 vertices. Con menos, AurMalla sale por su
                    // guarda y devuelve 0: parece rapidisima porque NO HACE NADA. Ojo con eso al medir.
                    int nvM = (6 + 4) * cols * 6;
                    Array v = Array.CreateInstance(tVtx, nvM);
                    float[] h = new float[puntos], a = new float[puntos], r2 = new float[puntos];
                    uint[] c1 = new uint[(6 + 1) * puntos], c2 = new uint[(4 + 1) * puntos];
                    Random rr = new Random(5);
                    for (int i = 0; i < puntos; i++)
                    { h[i] = (float)rr.NextDouble() * 200f; a[i] = (float)rr.NextDouble(); r2[i] = (float)rr.NextDouble(); }
                    for (int i = 0; i < c1.Length; i++) c1[i] = 0xFF3366CCu;
                    for (int i = 0; i < c2.Length; i++) c2[i] = 0xFF6633AAu;
                    object[] args = new object[] { v, h, c1, c2, a, r2, 1024f, 660f, 768f };
                    Medida mm = Mide(3000, delegate { mMalla.Invoke(null, args); });
                    int devuelve = (int)mMalla.Invoke(null, args);
                    Fila("relleno de la aurora (AurMalla, " + devuelve + " vertices)", mm);
                }
                if (mBrillo != null && tVtx != null)
                {
                    FieldInfo fN = tPanel.GetField("AurBrilloVtx", BindingFlags.Static | BindingFlags.NonPublic);
                    int nv = (fN != null) ? (int)fN.GetValue(null) : 7992;
                    Array v = Array.CreateInstance(tVtx, nv);
                    float[] h = new float[puntos], n2 = new float[puntos], g2 = new float[puntos];
                    uint[] cn = new uint[puntos], cl = new uint[puntos];
                    Random rr = new Random(6);
                    for (int i = 0; i < puntos; i++)
                    { h[i] = (float)rr.NextDouble(); n2[i] = (float)rr.NextDouble(); g2[i] = (float)rr.NextDouble();
                      cn[i] = 0xFF66CCFFu; cl[i] = 0xFFCCEEFFu; }
                    object[] args = new object[] { v, h, cn, n2, cl, g2, 1024f, 660f };
                    Fila("neon de la aurora (AurBrillo, " + nv + " vertices)", Mide(3000, delegate { mBrillo.Invoke(null, args); }));
                }
                if (mStars != null && tVtx != null)
                {
                    int n = 900;
                    Array v = Array.CreateInstance(tVtx, n * 6);
                    float[] xs = new float[n], ys = new float[n], ph = new float[n], sp = new float[n], sz = new float[n];
                    int[] kd = new int[n]; uint[] col = new uint[n];
                    Random rr = new Random(7);
                    for (int i = 0; i < n; i++)
                    { xs[i] = (float)rr.NextDouble(); ys[i] = (float)rr.NextDouble(); ph[i] = (float)(rr.NextDouble() * 6);
                      sp[i] = 0.3f; sz[i] = 2f; kd[i] = i % 3; col[i] = 0xFFFFFFFFu; }
                    object[] args = new object[] { v, xs, ys, ph, sp, sz, kd, col, n, 12.3, 1f, 0.4f, 0f, 0f, 1024f, 768f };
                    Fila("las " + n + " estrellas (BuildStarVerts)", Mide(3000, delegate { mStars.Invoke(null, args); }));
                }
            }
        }

        Console.WriteLine("\n  Nota: los tiempos llevan dentro el coste de llamar por reflexion (~1-2 us),");
        Console.WriteLine("  asi que lo que vale es la COMPARACION entre unos y otros, y el antes/despues.\n");
        return 0;
    }
}

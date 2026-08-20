using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// QUE LA ROCOLA NO SE PONGA PESADA JUSTO CUANDO ALGUIEN LA ESTA USANDO.
//
// La maquina del bar tiene dos nucleos y hay que repartirlos entre sonar musica, repasar la biblioteca y
// dibujar la pantalla. Aqui se vigilan las dos cosas que se comian el dibujado sin que se notara mirando el
// codigo, porque las dos eran "una linea que parece inocente".
//
//   1) ATENUAR EL FONDO. Mientras alguien elige cancion con musica sonando, el campo de estrellas se atenua
//      para que no le pelee a las portadas. Se hacia AL PEGAR la capa, con una matriz de color, y eso obliga
//      a la libreria de dibujo a pasar por CADA pixel: veinte veces mas caro que copiar la capa tal cual
//      (5.24 ms contra 0.27 en 1024x768). O sea que la rocola se volvia veinte veces mas pesada EN EL
//      MOMENTO en que tiene que ir mas suelta. Ahora la copia atenuada se hornea una vez -la capa de titileo
//      solo cambia cinco veces por segundo- y se reusa.
//
//   2) LA BARRA DEL ABECEDARIO. De un cuadro a otro solo laten dos casillas: la elegida y la estrellita de LO
//      MAS SONADO. Las otras veintiocho estan en una tira ya horneada que no se mueve. Se repintaba la barra
//      ENTERA veinticuatro veces por segundo por esos dos latidos.
//
// Las dos mejoras tienen que verse EXACTAMENTE igual que antes, y eso es lo que mas se comprueba aqui: no
// vale correr mas a cambio de que se vea distinto.
class Ligera
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TSky;
    static FieldInfo Campo(Type t, string n) { return t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static double Mide(Action a, int veces)
    {
        for (int i = 0; i < 20; i++) a();
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < veces; i++) a();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / veces;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        TSky = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);

        // =========================================================================================
        Console.WriteLine("\n  1) ATENUAR EL FONDO NO PUEDE COSTAR UN OJO DE LA CARA\n");
        Check(TSky != null, "esta el campo de estrellas");
        if (TSky == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        const int W = 1024, H = 768;
        object cielo = Activator.CreateInstance(TSky, true);
        Campo(TSky, "SkyFullW").SetValue(cielo, W); Campo(TSky, "SkyFullH").SetValue(cielo, H);
        Campo(TSky, "SkyOffsetX").SetValue(cielo, 0); Campo(TSky, "SkyOffsetY").SetValue(cielo, 0);
        Campo(TSky, "Dim").SetValue(cielo, 1f);
        Color ct = Color.FromArgb(255, 6, 8, 26), cb = Color.FromArgb(255, 20, 16, 54);
        using (Bitmap cal = new Bitmap(W, H)) using (Graphics gc = Graphics.FromImage(cal))
            TSky.GetMethod("DrawCached").Invoke(cielo, new object[] { gc, W, H, ct, cb });
        Bitmap[] capas = (Bitmap[])Campo(TSky, "layers").GetValue(cielo);
        Check(capas != null && capas.Length > 0, "y tiene sus capas de titileo horneadas");
        if (capas == null || capas.Length == 0) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        MethodInfo mBlit = TSky.GetMethod("BlitEstrellas", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mBlit != null, "y se le puede pedir que pegue las estrellas");
        if (mBlit == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        double sinAtenuar, atenuado;
        using (Bitmap bm = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(bm))
        {
            object[] arg = new object[] { g, capas[0], W, H, 0, 0 };
            Campo(TSky, "Dim").SetValue(cielo, 1f);
            sinAtenuar = Mide(delegate { mBlit.Invoke(cielo, arg); }, 150);
            Campo(TSky, "Dim").SetValue(cielo, 0.45f);
            atenuado = Mide(delegate { mBlit.Invoke(cielo, arg); }, 150);
        }
        double veces = atenuado / Math.Max(0.0001, sinAtenuar);
        Console.WriteLine("     pegar la capa: " + sinAtenuar.ToString("0.000") + " ms sin atenuar, "
                          + atenuado.ToString("0.000") + " ms atenuada  ->  " + veces.ToString("0.0") + " veces mas cara");
        Check(veces < 3.0, "atenuar cuesta practicamente lo mismo que no atenuar (antes: VEINTE veces mas)");

        // Y lo que importa de verdad: que se vea IGUAL. Se compara contra hacerlo a la antigua, con la matriz
        // de color aplicada al pegar, que es lo que se veia en el bar.
        Console.WriteLine("\n     y ademas, se ve igual que antes:\n");
        int peorDif = 0; long distintos = 0;
        foreach (float dim in new float[] { 1f, 0.7f, 0.45f, 0.2f })
        {
            using (Bitmap mio = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
            using (Bitmap viejo = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
            {
                Campo(TSky, "Dim").SetValue(cielo, dim);
                using (Graphics g = Graphics.FromImage(mio)) mBlit.Invoke(cielo, new object[] { g, capas[0], W, H, 0, 0 });
                // a la antigua: matriz de color al pegar
                using (Graphics g = Graphics.FromImage(viejo))
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = dim;
                    ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(capas[0], new Rectangle(0, 0, W, H), 0, 0, W, H, GraphicsUnit.Pixel, ia);
                }
                for (int y = 0; y < H; y += 3)
                    for (int x = 0; x < W; x += 3)
                    {
                        Color a = mio.GetPixel(x, y), b = viejo.GetPixel(x, y);
                        if (a.ToArgb() == b.ToArgb()) continue;
                        distintos++;
                        int e = Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G),
                                Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.A - b.A))));
                        if (e > peorDif) peorDif = e;
                    }
            }
        }
        Console.WriteLine("     con cuatro niveles de atenuacion: " + distintos + " pixeles distintos, la mayor diferencia de color " + peorDif);
        Check(distintos == 0, "sale EXACTAMENTE lo mismo que a la antigua: la mejora no cambia ni un pixel");

        // =========================================================================================
        Console.WriteLine("\n  2) LA BARRA DEL ABECEDARIO SOLO REPINTA LO QUE LATE\n");
        Type TLet = TF.GetNestedType("LetterBar", BindingFlags.NonPublic | BindingFlags.Public);
        MethodInfo mZonas = (TLet != null) ? TLet.GetMethod("ZonasLatido") : null;
        Check(mZonas != null, "la barra sabe decir que trozos suyos cambian de un cuadro a otro");
        if (mZonas == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        const int LW = 992, LH = 40;
        Control lb = (Control)Activator.CreateInstance(TLet, true);
        lb.Font = new Font(Fam(), 9f); lb.SetBounds(0, 0, LW, LH);
        string letras = "★#ABCDGJLMPRSTVZ►";
        HashSet<char> disp = new HashSet<char>(); foreach (char c in letras) disp.Add(c);
        Dictionary<char, int> cuentas = new Dictionary<char, int>();
        Random rr = new Random(7); foreach (char c in letras) cuentas[c] = 12 + rr.Next(180);
        Campo(TLet, "Letters").SetValue(lb, letras); Campo(TLet, "Available").SetValue(lb, disp);
        Campo(TLet, "Counts").SetValue(lb, cuentas); Campo(TLet, "cur").SetValue(lb, 'M');
        MethodInfo mRen = TLet.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });

        object[] zonas = new object[] { Rectangle.Empty, Rectangle.Empty };
        bool hay = (bool)mZonas.Invoke(lb, zonas);
        Rectangle z1 = (Rectangle)zonas[0], z2 = (Rectangle)zonas[1];
        int ancho = (z1.Width > 0 ? z1.Width : 0) + (z2.Width > 0 ? z2.Width : 0);
        Console.WriteLine("     de " + LW + " px de barra, hay que repintar " + ancho + " (" + (ancho * 100 / LW) + "%)");
        Check(hay, "con la barra quieta, dice que solo hay que repintar unos trozos");
        Check(ancho > 0 && ancho < LW / 2, "y esos trozos son menos de la mitad de la barra");

        // LO IMPORTANTE: repintando SOLO esos trozos tiene que quedar la misma imagen que repintando todo.
        // Si el latido se saliera de ellos, quedaria un resto sucio en pantalla.
        int malos = 0;
        for (int paso = 0; paso < 12; paso++)
        {
            using (Bitmap entera = new Bitmap(LW, LH, PixelFormat.Format32bppPArgb))
            using (Bitmap trozos = new Bitmap(LW, LH, PixelFormat.Format32bppPArgb))
            using (Graphics ga = Graphics.FromImage(entera))
            using (Graphics gb = Graphics.FromImage(trozos))
            {
                // el cuadro anterior, tal cual quedo en pantalla
                Campo(TLet, "Phase").SetValue(lb, 0.0d);
                gb.Clear(Color.Transparent); mRen.Invoke(lb, new object[] { gb, false });
                Campo(TLet, "Phase").SetValue(lb, paso * 0.37);
                ga.Clear(Color.Transparent); mRen.Invoke(lb, new object[] { ga, false });   // repintado entero
                object[] zz = new object[] { Rectangle.Empty, Rectangle.Empty };
                if ((bool)mZonas.Invoke(lb, zz))
                {
                    Region reg = new Region(new Rectangle(0, 0, 0, 0));
                    foreach (Rectangle z in new Rectangle[] { (Rectangle)zz[0], (Rectangle)zz[1] })
                        if (z.Width > 0) reg.Union(z);
                    gb.Clip = reg;
                }
                gb.Clear(Color.Transparent);      // Windows borra la zona invalidada antes de repintarla
                mRen.Invoke(lb, new object[] { gb, false });
                for (int y = 0; y < LH; y++)
                    for (int x = 0; x < LW; x++)
                        if (entera.GetPixel(x, y).ToArgb() != trozos.GetPixel(x, y).ToArgb()) malos++;
            }
        }
        Console.WriteLine("     en 12 momentos del latido, pixeles que quedarian mal: " + malos);
        Check(malos == 0, "repintando solo esos trozos queda la MISMA barra: no se ve nada distinto");

        // Y que la rocola lo use de verdad; si no, todo lo de arriba estaria bien y no serviria de nada.
        bool laUsa = false;
        foreach (MethodBase m in Codigo.TodosLosMetodos(TF))
            if (Codigo.Llama(m, "ZonasLatido")) laUsa = true;
        Check(laUsa, "y la rocola pide esos trozos al repintar, no repinta la barra entera");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LO QUE CUESTA REPINTAR LA CASILLA DE BUSCAR.  (Y POR QUE SE DEJO COMO ESTABA.)
//
// La casilla de BUSCAR es el ultimo control que se repinta ENTERO en cada cuadro: veinticuatro veces por
// segundo mientras la rocola esta encendida. La de la barra de ayuda ya solo se repinta mientras dura su
// destello, y la barra de letras solo repinta los dos trozos que laten. Asi que parecia la siguiente.
//
// Se midio, y NO PAGA. Esta herramienta es la medida, guardada para no tener que volver a hacerla:
//
//   1) La casilla cuesta 0.385 ms por cuadro: lo MISMO que la barra de letras entera, que es cuatro veces
//      mas grande. O sea, unos 37 ms por segundo en la maquina del bar: un 3.7% de un nucleo.
//
//   2) De ese tiempo, la capsula -el rellenado y el contorno del recuadro redondeado- se lleva casi un
//      tercio, y depende del latido: no se puede hornear.
//
//   3) Lo unico que NO late es el punto rojo y la tecla de la flecha, que juntos son un 22%. Se probo a
//      hornearlos y pegarlos, y NO sale igual: 180 pixeles cambian, hasta en 2 de 255. No se ve, pero en
//      esta rocola las optimizaciones se aceptan cuando la pantalla sale IDENTICA, y esta no lo hace.
//
//   4) Lo que si es exacto -no repintar cuando el dibujo saldria byte por byte igual- solo se lleva un
//      9.7% de los cuadros, porque el brillo cambia casi en todos. Y reusar la trayectoria, la brocha y la
//      pluma ahorra otro 4% de la capsula.
//
//   Total exacto: un 12% de un 3.7% de un nucleo. No compensa tocar el control que mas se ve por eso.
//
//   Lo unico que daria un salto de verdad seria guardar el dibujo ya hecho de cada brillo. Pero el latido
//   pasa por 140 brillos distintos y cada dibujo son 42 KB: casi 6 MB de imagenes para una casilla, en una
//   maquina de 4 GB. Tampoco.
//
// Se corre a mano:   sh pruebas/todo.sh buscacosto
class BuscaCosto
{
    const int W = 238, H = 44;
    static Graphics g;

    static double Mide(string que, int veces, Action a)
    {
        for (int i = 0; i < Math.Max(5, veces / 10); i++) a();
        double mejor = double.MaxValue;
        for (int t = 0; t < 5; t++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < veces; i++) a();
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / veces;
            if (ms < mejor) mejor = ms;
        }
        Console.WriteLine(string.Format("  {0,-44} {1,8:0.0000} ms/cuadro   {2,6:0.0} ms/s en el bar",
                                        que, mejor, mejor * 24 * 4));
        return mejor;
    }

    static GraphicsPath Redondo(Rectangle r, int rad)
    {
        GraphicsPath p = new GraphicsPath(); int d = rad * 2;
        if (d > r.Height) d = r.Height;
        if (d > r.Width) d = r.Width;
        if (d < 2) d = 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    // El punto rojo y la tecla de la flecha, que son lo unico que no late.
    static void Cola(Graphics gr, float x, int cy, int d)
    {
        using (SolidBrush s = new SolidBrush(Color.FromArgb(228, 72, 72))) gr.FillEllipse(s, x, cy - d / 2f, d, d);
        using (Pen p = new Pen(Color.FromArgb(160, 255, 255, 255), 1.4f)) gr.DrawEllipse(p, x, cy - d / 2f, d, d);
        float xf = x + d + 4 + 11 + 6; int dd = (int)(d * 0.9f);
        Rectangle k = new Rectangle((int)xf, cy - dd / 2, dd, dd);
        using (GraphicsPath gp = Redondo(k, Math.Max(3, dd / 4)))
        {
            using (SolidBrush s = new SolidBrush(Color.FromArgb(70, 228, 72, 72))) gr.FillPath(s, gp);
            using (Pen p = new Pen(Color.FromArgb(215, 228, 72, 72), 1.6f)) gr.DrawPath(p, gp);
        }
        float cx = xf + dd / 2f, t = dd * 0.30f, an = dd * 0.24f;
        using (SolidBrush s = new SolidBrush(Color.FromArgb(228, 72, 72)))
            gr.FillPolygon(s, new PointF[] { new PointF(cx, cy - t), new PointF(cx - an, cy + t * 0.62f), new PointF(cx + an, cy + t * 0.62f) });
    }

    static void Pon(Type t, object o, string n, object v)
    {
        FieldInfo f = t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(o, v);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Type TF = Assembly.LoadFrom(exe).GetType("Rocola.Form1", true);
        Type TBus = TF.GetNestedType("BuscaView", BindingFlags.NonPublic | BindingFlags.Public);
        Type TLet = TF.GetNestedType("LetterBar", BindingFlags.NonPublic | BindingFlags.Public);
        FontFamily fam; try { fam = new FontFamily("DejaVu Sans"); } catch { fam = FontFamily.GenericSansSerif; }

        Bitmap lienzo = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb);
        g = Graphics.FromImage(lienzo);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Console.WriteLine("\n  1) LA CASILLA ENTERA, Y CON QUE COMPARARLA\n");
        Control bv = (Control)Activator.CreateInstance(TBus, true);
        bv.Font = new Font(fam, 9f); bv.SetBounds(0, 0, W, H);
        Pon(TBus, bv, "Phase", 0.6d);
        MethodInfo mR = TBus.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
        object[] arg = new object[] { g, false };
        double todo = Mide("la casilla de BUSCAR entera (238x44)", 2000, delegate { mR.Invoke(bv, arg); });
        Control lb = (Control)Activator.CreateInstance(TLet, true);
        lb.Font = new Font(fam, 9f); lb.SetBounds(0, 0, 1024, H);
        Pon(TLet, lb, "Phase", 0.6d);
        MethodInfo mL = TLet.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
        object[] argL = new object[] { g, false };
        Mide("(referencia: la barra de letras entera, 1024x44)", 500, delegate { mL.Invoke(lb, argL); });

        Console.WriteLine("\n  2) A DONDE SE VA ESE TIEMPO\n");
        Font fTxt = new Font(fam, Math.Max(8f, H * 0.30f), FontStyle.Bold);
        StringFormat sf = new StringFormat();
        sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Center;
        double lat = 0.7; Color azul = Color.FromArgb(90, 190, 255);
        Rectangle r = new Rectangle(0, 0, W - 1, H - 1);
        int d = 22; Color tinta = Color.FromArgb((int)(170 + 85 * lat), 235, 245, 255);
        double cap = Mide("la capsula (rellenar + contorno)   <- LATE", 4000, delegate
        {
            using (GraphicsPath gp = Redondo(r, 14))
            {
                using (SolidBrush s = new SolidBrush(Color.FromArgb((int)(28 + 58 * lat), azul))) g.FillPath(s, gp);
                using (Pen pn = new Pen(Color.FromArgb((int)(110 + 145 * lat), azul), 2f)) g.DrawPath(pn, gp);
            }
        });
        Mide("la lupa                            <- LATE", 8000, delegate
        {
            float aro = (int)(d * 1.15f) * 0.72f;
            using (Pen pn = new Pen(tinta, Math.Max(2f, (int)(d * 1.15f) * 0.13f)))
            {
                g.DrawEllipse(pn, 11, H / 2 - aro / 2f, aro, aro);
                float m = aro * 0.72f;
                g.DrawLine(pn, 11 + aro * 0.85f, H / 2 + aro * 0.35f, 11 + aro * 0.85f + m * 0.5f, H / 2 + aro * 0.35f + m * 0.5f);
            }
        });
        Mide("la palabra BUSCAR y el signo +      <- LATEN", 4000, delegate
        {
            using (SolidBrush s = new SolidBrush(tinta))
            {
                g.DrawString("BUSCAR", fTxt, s, new RectangleF(50, 0, 120, H), sf);
                g.DrawString("+", fTxt, s, new RectangleF(176, 0, 20, H), sf);
            }
        });
        double fija = Mide("el punto rojo y la tecla        <- NO latan", 4000, delegate { Cola(g, 150f, H / 2, d); });
        Console.WriteLine(string.Format("\n     lo que NO late es el {0:0}% del total: es lo unico que se podria hornear",
                                        100.0 * fija / todo));
        Console.WriteLine(string.Format("     y la capsula sola, que si late, ya es el {0:0}%", 100.0 * cap / todo));

        Console.WriteLine("\n  3) CUANTOS CUADROS SALDRIAN IGUALES AL ANTERIOR\n");
        // Lo unico EXACTO que se puede ahorrar: no repintar cuando el dibujo saldria byte por byte igual.
        // Del latido solo salen tres numeros (la transparencia del relleno, la del contorno y la de la
        // tinta); si los tres repiten, el cuadro repite.
        int iguales = 0, total = 0, paf = -1, pap = -1, pat = -1;
        Dictionary<string, bool> estados = new Dictionary<string, bool>();
        for (double ph = 0; ph < 200.0; ph += 0.045)
        {
            double l = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(ph * 2.4));
            int af = (int)(28 + 58 * l), ap = (int)(110 + 145 * l), at = (int)(170 + 85 * l);
            if (total > 0 && af == paf && ap == pap && at == pat) iguales++;
            estados[af + "," + ap + "," + at] = true;
            paf = af; pap = ap; pat = at; total++;
        }
        Console.WriteLine(string.Format("     de {0} cuadros, {1} saldrian identicos al anterior ({2:0.0}%)",
                                        total, iguales, 100.0 * iguales / total));
        Console.WriteLine(string.Format("     y el latido pasa por {0} brillos distintos: guardarlos todos dibujados", estados.Count));
        Console.WriteLine(string.Format("     serian {0:0.0} MB de imagenes para una casilla", estados.Count * W * H * 4 / 1048576.0));

        Console.WriteLine("\n  4) Y HORNEAR LO QUE NO LATE NO SALE IGUAL\n");
        // La unica pieza gorda que se podria hornear son el punto y la tecla. Pegarlos ya hechos NO da los
        // mismos pixeles que dibujarlos encima: al guardar el dibujo, la transparencia se redondea.
        {
            float x0 = 150.375f; int cy = H / 2;
            using (Bitmap a = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
            using (Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
            {
                using (Graphics ga = Graphics.FromImage(a))
                {
                    ga.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush s = new SolidBrush(Color.FromArgb(200, 60, 110, 160))) ga.FillRectangle(s, 0, 0, W, H);
                    Cola(ga, x0, cy, d);
                }
                int ix = (int)Math.Floor(x0); float fr = x0 - ix;
                using (Bitmap est = new Bitmap(W - ix, H, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics ce = Graphics.FromImage(est)) { ce.SmoothingMode = SmoothingMode.AntiAlias; Cola(ce, fr, cy, d); }
                    using (Graphics gb = Graphics.FromImage(b))
                    {
                        gb.SmoothingMode = SmoothingMode.AntiAlias;
                        using (SolidBrush s = new SolidBrush(Color.FromArgb(200, 60, 110, 160))) gb.FillRectangle(s, 0, 0, W, H);
                        gb.DrawImageUnscaled(est, ix, 0);
                    }
                }
                int dif = 0, peor = 0;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                        if (p.ToArgb() == q.ToArgb()) continue;
                        dif++;
                        int e = Math.Max(Math.Max(Math.Abs(p.R - q.R), Math.Abs(p.G - q.G)),
                                         Math.Max(Math.Abs(p.B - q.B), Math.Abs(p.A - q.A)));
                        if (e > peor) peor = e;
                    }
                Console.WriteLine("     pegando la estampa cambian " + dif + " pixeles de " + (W * H)
                                  + ", hasta en " + peor + " de 255");
                Console.WriteLine("     (no se ve, pero aqui las optimizaciones se aceptan cuando sale IDENTICO)");
            }
        }

        Console.WriteLine("\n  CONCLUSION: lo exacto que se puede ahorrar es poco menos de un 12% de 37 ms/s,");
        Console.WriteLine("  o sea medio punto porcentual de un nucleo. Se deja la casilla como esta.\n");
        g.Dispose(); lienzo.Dispose();
        return 0;
    }
}

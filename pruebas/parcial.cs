using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// EL BUSCADOR YA NO SE REPINTA ENTERO EN CADA CUADRO.
//
// Ahora la pantalla completa se rehace una de cada tres veces, y en las otras dos solo se repinta la parte
// que de verdad se mueve: el cubo que gira y la franja de las ayudas, donde vive el destello del boton que
// se acaba de pulsar. Eso solo vale si esa parte CUBRE todo lo que se mueve; si algo se moviera fuera, se
// quedaria congelado dos cuadros de cada tres y se veria a tirones.
//
// Aqui se dibujan dos cuadros seguidos con el fondo CONGELADO (para que solo cambie lo que se anima) y se
// comprueba, mirando los pixeles, que todo lo que cambia cae dentro de la zona que se repinta.
class Parcial
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TSO, TItem, TAlb;
    static Bitmap laPortada;
    static Image Cov<T>(T a) { return laPortada; }
    static string Gen<T>(T it) { return "Balada"; }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    // La zona que el programa repinta en los cuadros parciales (la misma cuenta que hace InvalidarVivo).
    static Rectangle ZonaViva(Control so, int w, int h)
    {
        int M = 30, kbW = (int)((w - 2 * M) * 0.52);
        Rectangle r = new Rectangle(0, Math.Max(0, M + 46), Math.Min(w, M + kbW + 10), 112);
        FieldInfo fc = TSO.GetField("cubeRect", BindingFlags.Instance | BindingFlags.NonPublic);
        Rectangle cr = (fc != null) ? (Rectangle)fc.GetValue(so) : Rectangle.Empty;
        if (cr.Width > 1 && cr.Height > 1) { cr.Inflate(26, 84); r = Rectangle.Union(r, cr); }
        else
        {
            // con el teclado a la vista tambien entra la tecla elegida, que respira
            FieldInfo fRows = TSO.GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo fSR = TSO.GetField("SelRow"), fSC = TSO.GetField("SelCol");
            if (fRows != null)
            {
                IList filas = (IList)fRows.GetValue(so);
                int sr = (int)fSR.GetValue(so), sc = (int)fSC.GetValue(so);
                if (sr >= 0 && sr < filas.Count)
                {
                    IList fila = (IList)filas[sr];
                    if (sc >= 0 && sc < fila.Count)
                    {
                        object k = fila[sc];
                        RectangleF kr = (RectangleF)k.GetType().GetField("R").GetValue(k);
                        Rectangle ki = Rectangle.Round(kr); ki.Inflate(10, 10);
                        r = Rectangle.Union(r, ki);
                    }
                }
            }
        }
        return r;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SearchOverlay") TSO = t;
            if (t.Name == "SearchItem") TItem = t;
            if (t.Name == "Album") TAlb = t;
        }
        Check(TSO != null, "el buscador esta donde tiene que estar");
        Check(TSO != null && TSO.GetMethod("InvalidarVivo", BindingFlags.Instance | BindingFlags.Public) != null,
              "el buscador sabe repintar solo lo que se mueve");
        if (TSO == null) return 1;

        laPortada = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(laPortada))
        {
            pg.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 40; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) pg.FillEllipse(sb, i * 7, i * 6, 60, 60);
        }

        int[,] tam = { { 1024, 768 }, { 1366, 768 }, { 1920, 1080 }, { 800, 600 } };
        MethodInfo mR = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        // EL CIELO SE DEJA FUERA A PROPOSITO. El buscador ya no se arma su propio cielo: usa el COMPARTIDO
        // con el resto del programa (el campo Sky), y si no se le da ninguno simplemente no lo pinta. Eso es
        // justo lo que hace falta aqui: los dos cuadros arrancan del mismo fondo liso y lo unico que puede
        // cambiar entre ellos es lo que se anima por delante, que es lo que se quiere medir.
        // Antes se conseguia lo mismo congelando el cielo a mano; ahora sale gratis y ademas sin depender de
        // en que milisegundo caiga el titileo, que era una fuente de fallos falsos esperando a pasar.

        Console.WriteLine("\n  1) TODO LO QUE SE MUEVE CAE DENTRO DE LA ZONA QUE SE REPINTA\n");
        int fuera = 0, casos = 0; string peor = null; int peorN = 0;
        for (int t = 0; t < tam.GetLength(0); t++)
            for (int modo = 0; modo < 2; modo++)   // 0 = mirando resultados (hay cubo)   1 = tecleando (hay teclado)
            {
                int w = tam[t, 0], h = tam[t, 1];
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
                TSO.GetField("FocusResults").SetValue(so, modo == 0);
                TSO.GetField("SelResult").SetValue(so, 3);
                Type tfc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
                MethodInfo mic = typeof(Parcial).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb);
                TSO.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(tfc, mic));
                FieldInfo fYa = TSO.GetField("GetCoverYa");
                if (fYa != null) fYa.SetValue(so, Delegate.CreateDelegate(tfc, mic));
                FieldInfo fg = TSO.GetField("GetGenero");
                if (fg != null)
                {
                    Type tf2 = typeof(Func<,>).MakeGenericType(TItem, typeof(string));
                    fg.SetValue(so, Delegate.CreateDelegate(tf2, typeof(Parcial).GetMethod("Gen", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem)));
                }
                FieldInfo fPhase = TSO.GetField("Phase"), fAng = TSO.GetField("CubeAngle");
                FieldInfo fLo = TSO.GetField("PulseLo"), fMid = TSO.GetField("PulseMid"), fHi = TSO.GetField("PulseHi");
                // Se dispara el destello del boton, que es lo otro que se anima.
                MethodInfo mFlash = TSO.GetMethod("FlashMove", BindingFlags.Instance | BindingFlags.Public);

                Rectangle todo = new Rectangle(0, 0, w, h);
                Bitmap a1 = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                Bitmap a2 = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                double ph = 1.0;
                if (fLo != null) { fLo.SetValue(so, 0.6f); fMid.SetValue(so, 0.6f); fHi.SetValue(so, 0.6f); }
                if (mFlash != null) mFlash.Invoke(so, new object[] { true });

                // Primer cuadro (el fondo se construye aqui). A partir de ahora NO se deja rehacer el fondo,
                // asi lo unico que puede cambiar entre los dos cuadros es lo que se anima por delante.
                using (Graphics g = Graphics.FromImage(a1))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(255, 8, 12, 36));
                    fPhase.SetValue(so, ph);
                    mR.Invoke(so, new object[] { g, todo, false });
                }
                Rectangle viva = ZonaViva(so, w, h);
                // Segundo cuadro: avanza el giro del cubo y el desvanecido del destello, con el fondo congelado.
                using (Graphics g = Graphics.FromImage(a2))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(255, 8, 12, 36));
                    ph += 0.045; fPhase.SetValue(so, ph);
                    fAng.SetValue(so, (double)fAng.GetValue(so) + 0.075);
                    mR.Invoke(so, new object[] { g, todo, false });
                }

                // Todo lo que cambio tiene que caer dentro de la zona viva. Se ignora el cursor que parpadea
                // en la caja de texto: parpadea una vez por segundo y se refresca en los cuadros completos.
                Rectangle caja = new Rectangle(30, 30, w - 60, 70);
                int malos = 0; int mnx = w, mny = h, mxx = -1, mxy = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (viva.Contains(x, y) || caja.Contains(x, y)) continue;
                        if (a1.GetPixel(x, y).ToArgb() != a2.GetPixel(x, y).ToArgb())
                        {
                            malos++;
                            if (x < mnx) mnx = x; if (y < mny) mny = y;
                            if (x > mxx) mxx = x; if (y > mxy) mxy = y;
                        }
                    }
                if (malos > 0) Console.WriteLine("        (donde: x " + mnx + ".." + mxx + "   y " + mny + ".." + mxy + "   zona viva: " + viva + ")");
                casos++;
                if (malos > 0)
                {
                    fuera++;
                    if (malos > peorN) { peorN = malos; peor = w + "x" + h + (modo == 0 ? " (con cubo)" : " (con teclado)"); }
                }
                Console.WriteLine(string.Format("     {0,4}x{1,-4} {2,-14}  pixeles que se mueven fuera de la zona: {3}",
                                                w, h, modo == 0 ? "con cubo" : "con teclado", malos));
                a1.Dispose(); a2.Dispose();
            }
        Console.WriteLine("     casos probados: " + casos + "   con movimiento fuera de la zona: " + fuera + (peor != null ? ("   peor: " + peorN + " px en " + peor) : ""));
        Check(fuera == 0, "nada de lo que se anima queda fuera de la zona que se repinta");

        // ---------- 2) LA ZONA NO SE MERIENDA MEDIA PANTALLA ----------
        // Si la zona fuera casi toda la pantalla, no se habria ahorrado nada.
        Console.WriteLine("\n  2) Y LA ZONA ES PEQUENA (si no, no se ahorraria nada)\n");
        int grandes = 0;
        for (int t = 0; t < tam.GetLength(0); t++)
        {
            int w = tam[t, 0], h = tam[t, 1];
            Control so = (Control)Activator.CreateInstance(TSO, true);
            so.SetBounds(0, 0, w, h);
            so.Font = new Font(Fam(), 12f, FontStyle.Bold);
            TSO.GetField("FocusResults").SetValue(so, true);
            // sin cubo calculado aun: la zona es solo la franja de ayudas
            Rectangle viva = ZonaViva(so, w, h);
            double pct = 100.0 * viva.Width * viva.Height / (w * h);
            Console.WriteLine(string.Format("     {0,4}x{1,-4}  la zona ocupa el {2,4:0.0}% de la pantalla", w, h, pct));
            if (pct > 45) grandes++;
        }
        Check(grandes == 0, "la zona que se repinta es menos de la mitad de la pantalla");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

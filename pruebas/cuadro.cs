using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// DE QUE SE COMPONE UN CUADRO DE PANTALLA, Y CUANTO CUESTA CUANDO NO HA CAMBIADO NADA.
//
// La rocola redibuja 24 veces por segundo. La mayor parte del tiempo NO ha cambiado nada: nadie ha pulsado
// un boton, la pagina es la misma, el mismo disco sigue seleccionado. Si en ese caso -que es casi todo el
// tiempo- se sigue rehaciendo trabajo, es gasto puro.
//
// Aqui se pinta cada pieza de la pantalla muchas veces SIN cambiar nada entre una y otra, y se mira lo que
// cuesta. Lo que salga caro sin haber cambiado nada es candidato a guardarse hecho.
class Cuadro
{
    static Type TF;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static void Pon(Type t, object o, string campo, object v)
    {
        FieldInfo f = t.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(o, v);
    }

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

    static Image portada;
    static Image Tapa<T>(T a) { return portada; }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);

        int W = 1024, H = 768;
        Bitmap lienzo = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        Graphics gg = Graphics.FromImage(lienzo);
        object[] arg = new object[] { gg };
        double total = 0;

        Console.WriteLine("\n  UN CUADRO A " + W + "x" + H + ", SIN QUE HAYA CAMBIADO NADA");
        Console.WriteLine("  (a 24 cuadros por segundo, un cuadro dura 41.7 ms)\n");
        Console.WriteLine("  {0,-34} {1,12}  {2,10}", "pieza", "por cuadro", "por segundo");
        Console.WriteLine("  " + new string('-', 60));

        // ---------- la pared de discos ----------
        Type TAV = Nes("AlbumView"), TAlb = Nes("Album");
        if (TAV != null && TAlb != null)
        {
            portada = new Bitmap(500, 500, PixelFormat.Format32bppPArgb);
            using (Graphics pg = Graphics.FromImage(portada))
            {
                pg.Clear(Color.FromArgb(255, 30, 40, 90));
                for (int i = 0; i < 60; i++) pg.FillEllipse(new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i)), i * 8, i * 7, 70, 70);
            }
            Control av = (Control)Activator.CreateInstance(TAV, true);
            av.SetBounds(0, 0, W, 460);
            av.Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold);
            Type tFunc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
            TAV.GetField("GetCover").SetValue(av, Delegate.CreateDelegate(tFunc,
                typeof(Cuadro).GetMethod("Tapa", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
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
            MethodInfo mReb = TAV.GetMethod("RebuildCubes", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo fPend = TAV.GetField("cubesPending", BindingFlags.Instance | BindingFlags.NonPublic);
            object[] wh = new object[] { W, 460 };
            int v = 0; do { mReb.Invoke(av, wh); v++; } while (fPend != null && (bool)fPend.GetValue(av) && v < 40);
            double ms = Mide(300, delegate { mReb.Invoke(av, wh); });
            Console.WriteLine("  {0,-34} {1,9:0.000} ms  {2,7:0.0} ms", "pared de discos (misma pagina)", ms, ms * 24);
            total += ms;
        }

        // ---------- la barra del abecedario ----------
        Type TLet = Nes("LetterBar");
        if (TLet != null)
        {
            Control let = (Control)Activator.CreateInstance(TLet, true);
            let.Font = new Font(FontFamily.GenericSansSerif, 9f);
            let.SetBounds(0, 0, W, 40);
            MethodInfo m = TLet.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            if (m != null)
            {
                try
                {
                    double ms = Mide(300, delegate { m.Invoke(let, arg); });
                    Console.WriteLine("  {0,-34} {1,9:0.000} ms  {2,7:0.0} ms", "barra del abecedario", ms, ms * 24);
                    total += ms;
                }
                catch (Exception ex) { Console.WriteLine("  barra del abecedario: no se pudo medir (" + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")"); }
            }
        }

        // ---------- el encabezado ----------
        Type TCab = Nes("HeaderView");
        if (TCab != null)
        {
            Control cab = (Control)Activator.CreateInstance(TCab, true);
            cab.Font = new Font(FontFamily.GenericSansSerif, 9f);
            cab.SetBounds(0, 0, 520, 34);
            TCab.GetField("Section").SetValue(cab, "LETRA B");
            TCab.GetField("Page").SetValue(cab, 2);
            TCab.GetField("Total").SetValue(cab, 9);
            MethodInfo m = TCab.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            double ms = Mide(600, delegate { m.Invoke(cab, arg); });
            Console.WriteLine("  {0,-34} {1,9:0.000} ms  {2,7:0.0} ms", "encabezado de seccion", ms, ms * 24);
            total += ms;
        }

        // ---------- la pastilla de creditos ----------
        Type TPas = Nes("PastillaView");
        if (TPas != null)
        {
            Control pas = (Control)Activator.CreateInstance(TPas, true);
            pas.Font = new Font(FontFamily.GenericSansSerif, 9f);
            pas.SetBounds(0, 0, 220, 40);
            TPas.GetField("Etiqueta").SetValue(pas, "CREDITOS");
            TPas.GetField("Valor").SetValue(pas, "3");
            MethodInfo m = TPas.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            double ms = Mide(600, delegate { m.Invoke(pas, arg); });
            Console.WriteLine("  {0,-34} {1,9:0.000} ms  {2,7:0.0} ms", "pastilla de creditos", ms, ms * 24);
            total += ms;
        }

        // ---------- la lista de canciones de un disco ----------
        Type TLis = Nes("SongListView");
        if (TLis != null)
        {
            Control lis = (Control)Activator.CreateInstance(TLis, true);
            lis.Font = new Font(FontFamily.GenericSansSerif, 11f);
            lis.SetBounds(0, 0, 900, 420);
            Pon(TLis, lis, "GetCount", (Func<int>)delegate { return 24; });
            Pon(TLis, lis, "GetText", (Func<int, string>)delegate (int i) { return "Cancion numero " + i + " de un disco"; });
            MethodInfo m = TLis.GetMethod("RenderContent", new Type[] { typeof(Graphics) });
            try
            {
                double ms = Mide(200, delegate { m.Invoke(lis, arg); });
                Console.WriteLine("  {0,-34} {1,9:0.000} ms  {2,7:0.0} ms", "lista de canciones (en un disco)", ms, ms * 24);
            }
            catch (Exception ex) { Console.WriteLine("  lista de canciones: no se pudo medir (" + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")"); }
        }

        Console.WriteLine("  " + new string('-', 60));
        Console.WriteLine("  {0,-34} {1,9:0.000} ms  {2,7:0.0} ms   ({3:0.0}% del tiempo)",
                          "la pared de discos + las barras", total, total * 24, total * 24 / 10.0);
        Console.WriteLine();
        Console.WriteLine("  (la lista de canciones va aparte: solo se ve DENTRO de un disco, no a la vez que la pared)");
        gg.Dispose(); lienzo.Dispose();
        return 0;
    }
}

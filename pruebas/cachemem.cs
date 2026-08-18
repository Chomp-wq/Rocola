using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// ¿COMPENSA GUARDAR MAS COSAS EN MEMORIA PARA QUE VAYA MAS INSTANTANEO?
//
// Guardar mas en memoria solo ayuda si lo que hoy se hace en su lugar CUESTA. Aqui se mide lo unico que
// la rocola vuelve a hacer una y otra vez segun se navega: reconstruir la caratula de un disco cuando ya
// no esta en la cache. Si eso cuesta poco, agrandar la cache no cambia nada; si cuesta, si.
class CacheMem
{
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

    static int Main()
    {
        const int Px = 256;
        string dir = "_tmp_caratulas";
        Directory.CreateDirectory(dir);

        // Una miniatura como las que la rocola deja en su cache de disco.
        string fThumb = Path.Combine(dir, "thumb.jpg");
        using (Bitmap b = new Bitmap(Px, Px, PixelFormat.Format24bppRgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                Random r = new Random(3);
                g.Clear(Color.FromArgb(40, 30, 70));
                for (int i = 0; i < 120; i++)
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(120, r.Next(256), r.Next(256), r.Next(256))))
                        g.FillEllipse(sb, r.Next(Px), r.Next(Px), r.Next(90), r.Next(90));
            }
            b.Save(fThumb, ImageFormat.Jpeg);
        }
        long tamThumb = new FileInfo(fThumb).Length;

        // Y una portada ORIGINAL de las que trae un disco (grande, como viene en la carpeta).
        string fOrig = Path.Combine(dir, "cover.jpg");
        using (Bitmap b = new Bitmap(1000, 1000, PixelFormat.Format24bppRgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                Random r = new Random(4);
                g.Clear(Color.FromArgb(30, 40, 60));
                for (int i = 0; i < 400; i++)
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(120, r.Next(256), r.Next(256), r.Next(256))))
                        g.FillEllipse(sb, r.Next(1000), r.Next(1000), r.Next(300), r.Next(300));
            }
            b.Save(fOrig, ImageFormat.Jpeg);
        }
        long tamOrig = new FileInfo(fOrig).Length;

        Console.WriteLine("\n  LO QUE CUESTA VOLVER A TRAER UNA CARATULA QUE YA NO ESTA EN LA CACHE\n");
        Console.WriteLine("     miniatura guardada: " + (tamThumb / 1024) + " KB    portada original: " + (tamOrig / 1024) + " KB");

        // (a) el camino normal: la miniatura ya esta hecha en disco
        Bitmap sal = null;
        double msThumb = Mide(60, delegate
        {
            byte[] d = File.ReadAllBytes(fThumb);
            using (MemoryStream ms = new MemoryStream(d))
            using (Image im = Image.FromStream(ms, false, false))
            {
                Bitmap tb = new Bitmap(Px, Px, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(tb))
                { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.DrawImage(im, 0, 0, Px, Px); }
                if (sal != null) sal.Dispose();
                sal = tb;
            }
        });
        if (sal != null) { sal.Dispose(); sal = null; }

        // (b) la primera vez de todas: hay que escalar la portada original
        double msOrig = Mide(20, delegate
        {
            byte[] d = File.ReadAllBytes(fOrig);
            using (MemoryStream ms = new MemoryStream(d))
            using (Image im = Image.FromStream(ms, false, false))
            {
                Bitmap tb = new Bitmap(Px, Px, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(tb))
                { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(im, 0, 0, Px, Px); }
                if (sal != null) sal.Dispose();
                sal = tb;
            }
        });
        if (sal != null) sal.Dispose();

        Console.WriteLine();
        Console.WriteLine("     desde la miniatura ya hecha : " + msThumb.ToString("0.00") + " ms por caratula");
        Console.WriteLine("     desde la portada original   : " + msOrig.ToString("0.00") + " ms por caratula  (solo la primera vez de cada disco)");
        Console.WriteLine();
        Console.WriteLine("     una PAGINA son 18 caratulas:");
        Console.WriteLine("        volver a una pagina ya vista, si ya no esta en la cache: " + (msThumb * 18).ToString("0.0") + " ms");
        Console.WriteLine("        (y eso NO para la pantalla: se hace en segundo plano; lo que se ve es que");
        Console.WriteLine("         las caratulas aparecen con ese retraso en vez de estar ya puestas)");

        Console.WriteLine("\n  LO QUE COSTARIA UNA CACHE MAS GRANDE\n");
        Console.WriteLine("     {0,10}  {1,12}  {2,14}  {3}", "caratulas", "memoria", "paginas", "");
        foreach (int n in new int[] { 220, 360, 540, 720, 1080 })
        {
            long mb = (long)n * Px * Px * 4 / 1024 / 1024;
            Console.WriteLine("     {0,10}  {1,9} MB  {2,11}    {3}", n, mb, n / 18,
                              n == 220 ? "<- lo de ahora" : "");
        }
        Directory.Delete(dir, true);
        return 0;
    }
}

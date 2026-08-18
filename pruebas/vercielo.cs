using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// Saca una imagen del cielo estrellado para poder MIRARLO. Un cambio de estetica no se puede dar por
// bueno leyendo codigo: hay que verlo.
class VerCielo
{
    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        Type TS = TF.GetNestedType("Starfield", BindingFlags.NonPublic | BindingFlags.Public);
        if (TS == null) { Console.WriteLine("no encuentro el campo de estrellas"); return 1; }

        int W = 1000, H = 620;
        object sf = Activator.CreateInstance(TS, true);
        FieldInfo fDens = TS.GetField("Density", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fDens != null) fDens.SetValue(sf, 1f);
        MethodInfo mDraw = TS.GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mDraw == null) { Console.WriteLine("no encuentro Draw"); return 1; }

        using (Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                object[] a = new object[] { g, W, H, Color.FromArgb(6, 8, 26), Color.FromArgb(26, 44, 90),
                                            Color.FromArgb(120, 160, 255), 0.0, false, 0f };
                mDraw.Invoke(sf, a);
            }
            b.Save("cielo.png", ImageFormat.Png);
        }
        // y un recorte grande de la esquina, para ver las siluetas de cerca
        using (Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                object[] a = new object[] { g, W, H, Color.FromArgb(6, 8, 26), Color.FromArgb(26, 44, 90),
                                            Color.FromArgb(120, 160, 255), 0.0, false, 0f };
                mDraw.Invoke(sf, a);
            }
            using (Bitmap z = new Bitmap(560, 350, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g2 = Graphics.FromImage(z))
                {
                    g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g2.DrawImage(b, new Rectangle(0, 0, 560, 350), new Rectangle(40, 40, 280, 175), GraphicsUnit.Pixel);
                }
                z.Save("cielo_zoom.png", ImageFormat.Png);
            }
        }
        Console.WriteLine("hecho: cielo.png y cielo_zoom.png");
        return 0;
    }
}

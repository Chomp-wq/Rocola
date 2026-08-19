using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

// EL CIELO DEL EQUIPO CON TARJETA GRAFICA, PARA MIRARLO.
//
// No es una bateria: no dice OK ni FALLO. El cielo por CPU ya se podia mirar con 'vercielo', pero el de la
// tarjeta grafica no, y es OTRO codigo: alli las estrellas no se dibujan una a una sino que se estampa un
// dibujito -un "cromo"- por estrella, sacado de una lamina con las tres siluetas. Los dos cielos tienen que
// verse IGUAL, y sin poder mirarlos juntos eso no hay manera de saberlo.
//
// Aqui se le pide al ejecutable su reparto de estrellas de verdad y su lamina de siluetas de verdad, y se
// estampa cada cromo donde y del tamano que le toca. No hace falta tarjeta grafica para eso.
//
//    mono vergpu.exe rocola.exe          deja cielo_gpu.png, cielo_gpu_zoom.png y siluetas.png
class VerGpu
{
    // Suma la capa de estrellas al cielo, que es lo que hace la tarjeta: cada color se anade al que ya
    // habia y se corta en 255. Asi una estrella sobre el cielo claro de abajo se ve tan brillante como
    // sobre el oscuro de arriba, en vez de recortarse encima.
    static void Suma(Bitmap fondo, Bitmap capa)
    {
        Rectangle r = new Rectangle(0, 0, fondo.Width, fondo.Height);
        BitmapData df = fondo.LockBits(r, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        BitmapData dc = capa.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int n = fondo.Width * fondo.Height * 4;
        byte[] f = new byte[n], c = new byte[n];
        System.Runtime.InteropServices.Marshal.Copy(df.Scan0, f, 0, n);
        System.Runtime.InteropServices.Marshal.Copy(dc.Scan0, c, 0, n);
        for (int i = 0; i < n; i += 4)
        {
            float a = c[i + 3] / 255f;   // la capa no viene premultiplicada: el alfa dice cuanta luz aporta
            for (int k = 0; k < 3; k++)
            {
                int v = f[i + k] + (int)(c[i + k] * a);
                f[i + k] = (byte)(v > 255 ? 255 : v);
            }
            f[i + 3] = 255;
        }
        System.Runtime.InteropServices.Marshal.Copy(f, 0, df.Scan0, n);
        capa.UnlockBits(dc); fondo.UnlockBits(df);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TG = null;
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuStarfield") { TG = t; break; }
        if (TG == null) { Console.WriteLine("este ejecutable no lleva el cielo de tarjeta grafica (compilalo con -d:ROCOLA_GPU)"); return 1; }
        BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        const int W = 1000, H = 620, CELDA = 64;
        // La lamina de siluetas, tal cual la hornea la rocola.
        MethodInfo mTex = TG.GetMethod("BakeStarTexture", SF);
        Bitmap atlas = (Bitmap)mTex.Invoke(null, new object[] { CELDA });
        atlas.Save("siluetas.png", ImageFormat.Png);

        // El reparto de estrellas, tal cual lo arma la rocola.
        object sf = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TG);
        TG.GetMethod("BuildStars", IF).Invoke(sf, new object[] { W, H, 20260620 });
        float[] sx = (float[])TG.GetField("sx", IF).GetValue(sf);
        float[] sy = (float[])TG.GetField("sy", IF).GetValue(sf);
        float[] sz = (float[])TG.GetField("ssize", IF).GetValue(sf);
        int[] kd = (int[])TG.GetField("skind", IF).GetValue(sf);
        int[] fm = (int[])TG.GetField("sforma", IF).GetValue(sf);
        uint[] col = (uint[])TG.GetField("scol", IF).GetValue(sf);
        int n = (int)TG.GetField("nStars", IF).GetValue(sf);
        int[] cuenta = new int[3];
        for (int i = 0; i < n; i++) cuenta[kd[i]]++;
        Console.WriteLine("  " + n + " estrellas:  " + cuenta[0] + " de polvo, " + cuenta[1] + " medianas, " + cuenta[2] + " grandes");

        // Las estrellas se dibujan SUMANDO LUZ, no tapando lo de debajo: por eso encienden el cielo en vez
        // de recortarse sobre el. Aqui se pintan aparte sobre negro y luego se suman al cielo pixel a pixel.
        // Sin esto la comparacion con el cielo por CPU enganaria: saldrian mas apagadas de lo que se ven.
        using (Bitmap capa = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        using (Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(capa))
            {
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.CompositingQuality = CompositingQuality.HighQuality;
                for (int i = 0; i < n; i++)
                {
                    int f = fm[i];
                    Rectangle src = new Rectangle((f % 2) * CELDA, (f / 2) * CELDA, CELDA, CELDA);
                    float s = sz[i];
                    uint c = col[i];
                    // El color y el brillo van en el vertice: aqui se aplican a la silueta con una matriz.
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ColorMatrix cm = new ColorMatrix();
                        cm.Matrix00 = ((c >> 16) & 0xFF) / 255f;
                        cm.Matrix11 = ((c >> 8) & 0xFF) / 255f;
                        cm.Matrix22 = (c & 0xFF) / 255f;
                        cm.Matrix33 = ((c >> 24) & 0xFF) / 255f;
                        ia.SetColorMatrix(cm);
                        g.DrawImage(atlas, new Rectangle((int)(sx[i] - s), (int)(sy[i] - s), (int)(s * 2), (int)(s * 2)),
                                    src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, ia);
                    }
                }
            }
            using (Graphics g = Graphics.FromImage(b))
            using (LinearGradientBrush lg = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                   Color.FromArgb(255, 6, 8, 26), Color.FromArgb(255, 26, 44, 90), LinearGradientMode.Vertical))
                g.FillRectangle(lg, 0, 0, W, H);
            Suma(b, capa);
            b.Save("cielo_gpu.png", ImageFormat.Png);
            using (Bitmap z = new Bitmap(560, 350, PixelFormat.Format32bppArgb))
            {
                using (Graphics g2 = Graphics.FromImage(z))
                {
                    g2.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g2.DrawImage(b, new Rectangle(0, 0, 560, 350), new Rectangle(40, 40, 280, 175), GraphicsUnit.Pixel);
                }
                z.Save("cielo_gpu_zoom.png", ImageFormat.Png);
            }
        }
        atlas.Dispose();
        Console.WriteLine("  hecho: cielo_gpu.png, cielo_gpu_zoom.png y siluetas.png");
        return 0;
    }
}

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

// Dibuja el aviso REAL (misma funcion que usa la rocola) en varios momentos de su animacion,
// sobre un cielo parecido al del programa, para ver como queda.
class VistaAnim
{
    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        MethodInfo pinta = TF.GetMethod("PintaToast", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo curva = TF.GetMethod("CurvaToast", BindingFlags.Static | BindingFlags.NonPublic);
        int W = (int)TF.GetField("ToastW", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        int H = (int)TF.GetField("ToastH", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        int ms = (int)TF.GetField("ToastMs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Font f = new Font("DejaVu Sans", 14f, FontStyle.Bold);

        object[][] casos = {
            new object[]{ "Credito agregado:  1", Color.FromArgb(40, 165, 90) },
            new object[]{ "Cancion agregada a la cola", Color.FromArgb(40, 165, 90) },
            new object[]{ "Sonido actualizado", Color.FromArgb(40, 130, 90) },
            new object[]{ "No se pudo abrir el video: credito devuelto", Color.FromArgb(200, 80, 70) },
        };
        int[] momentos = { 40, 120, 650, 1180 };
        int fw = 1024, fh = 150 * casos.Length;
        Bitmap outb = new Bitmap(fw, fh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(outb))
        {
            for (int y = 0; y < fh; y++)   // cielo degradado como el de la rocola
            {
                float t = y / (float)fh;
                int r = (int)(8 + 22 * t), gg = (int)(12 + 34 * t), b = (int)(30 + 66 * t);
                using (Pen p = new Pen(Color.FromArgb(255, r, gg, b))) g.DrawLine(p, 0, y, fw, y);
            }
            Bitmap tb = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            for (int k = 0; k < casos.Length; k++)
            {
                pinta.Invoke(null, new object[] { tb, casos[k][0], casos[k][1], f });
                int fila = k * 150;
                using (Font small = new Font("DejaVu Sans", 8f))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    g.DrawString("ms:", small, sb, 8, fila + 60);
                for (int m = 0; m < momentos.Length; m++)
                {
                    object[] p = { momentos[m], 0f, 0f };
                    curva.Invoke(null, p);
                    float a = (float)p[1], dy = (float)p[2];
                    int x = 40 + m * 245;
                    using (Font small = new Font("DejaVu Sans", 8f))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
                        g.DrawString(momentos[m].ToString(), small, sb, x + 80, fila + 60);
                    ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = a;
                    ImageAttributes ia = new ImageAttributes(); ia.SetColorMatrix(cm);
                    Rectangle dst = new Rectangle(x, fila + 20 + (int)dy, (int)(W * 0.55f), (int)(H * 0.55f));
                    g.DrawImage(tb, dst, 0, 0, W, H, GraphicsUnit.Pixel, ia);
                }
            }
            tb.Dispose();
        }
        outb.Save("vistaanim.png", System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine("listo: vistaanim.png");
        return 0;
    }
}

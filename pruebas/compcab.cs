using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
using System.Windows.Forms;

class CompCab
{
    static Control Nuevo(Type TCab, string sec, int page, int total, int w, int h)
    {
        Control c = (Control)Activator.CreateInstance(TCab, true);
        c.Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular);
        c.SetBounds(0, 0, w, h);
        TCab.GetField("Section").SetValue(c, sec);
        TCab.GetField("Page").SetValue(c, page);
        TCab.GetField("Total").SetValue(c, total);
        TCab.GetField("Accent").SetValue(c, Color.FromArgb(240, 150, 190));
        return c;
    }

    static Type Carga(string exe)
    {
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "HeaderView") return t;
        return null;
    }

    static int Main(string[] argv)
    {
        Type antes = Carga("rocola_antes.exe");
        Type ahora = Carga("rocola_cpu.exe");

        string[] secs   = { "LO MAS SONADO", "LETRA B",  "LETRA A" };
        int[] pags      = { 2,               2,          0 };
        int[] tots      = { 18,              18,         9 };
        int W = 470, H = 34;

        int fila = H + 30, alto = 40 + secs.Length * 2 * fila + 40;
        using (Bitmap img = new Bitmap(W + 200, alto, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(img))
            using (Font fr = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold))
            using (Font ft = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold))
            {
                g.Clear(Color.FromArgb(255, 14, 16, 26));
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawString("LA BARRA DE ARRIBA:  antes  /  ahora", ft, new SolidBrush(Color.FromArgb(255, 235, 240, 255)), 16, 12);
                int y = 46;
                for (int i = 0; i < secs.Length; i++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        Type T = (v == 0) ? antes : ahora;
                        g.DrawString((v == 0) ? "antes" : "ahora", fr,
                                     new SolidBrush(v == 0 ? Color.FromArgb(255, 235, 130, 130) : Color.FromArgb(255, 130, 225, 165)), 16, y + 9);
                        // el marco de la caja real que le toca al encabezado
                        using (Pen p = new Pen(Color.FromArgb(60, 130, 150, 200)))
                            g.DrawRectangle(p, 79, y - 1, W + 1, H + 1);
                        Control c = Nuevo(T, secs[i], pags[i], tots[i], W, H);
                        GraphicsState gs = g.Save();
                        g.TranslateTransform(80, y);
                        g.SetClip(new Rectangle(0, 0, W, H));
                        T.GetMethod("RenderContent").Invoke(c, new object[] { g });
                        g.Restore(gs);
                        c.Dispose();
                        y += fila;
                    }
                    y += 10;
                }
            }
            img.Save("cabecera_antes_ahora.png", ImageFormat.Png);
        }
        Console.WriteLine("hecho");
        return 0;
    }
}

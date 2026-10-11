using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.IO;
using System.Windows.Forms;

class GuiaRojo
{
    const BindingFlags B = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static int CheckControls(Control parent)
    {
        int found = 0;
        foreach (Control c in parent.Controls)
        {
            if (c.Left < 0 || c.Top < 0 || c.Right > parent.Width || c.Bottom > parent.Height)
                throw new Exception("Opción recortada: " + c.Text);
            if (c.Text == "Guía al mantener el botón rojo (GPU)") found++;
            found += CheckControls(c);
        }
        return found;
    }
    static int Main(string[] args)
    {
        Type form = Assembly.LoadFrom(args[0]).GetType("Rocola.Form1", true);
        object owner = Codigo.FormSinEncender(form);
        using (Font normal = new Font("Segoe UI", 10f, FontStyle.Bold))
        using (Font small = new Font("Segoe UI", 8.5f))
        using (Panel page = (Panel)form.GetMethod("PaginaPantalla", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner,
            new object[] { normal, small, normal, Color.White, Color.Gray, Color.DimGray }))
            if (CheckControls(page) != 1) throw new Exception("Guía inaccesible o duplicada en ajustes");
        int checks = 0;
        for (int bits = 0; bits < 32; bits++)
        {
            object[] values = new object[5];
            for (int i = 0; i < 5; i++) values[i] = (bits & (1 << i)) != 0;
            bool actual = (bool)form.GetMethod("GuiaRojoActiva", B).Invoke(null, values);
            if (actual != (bits == 15)) throw new Exception("Guía en contexto incorrecto: " + bits);
            checks++;
        }
        foreach (int w in new int[] { 640, 800, 1024, 1920, 3440, 3840 })
        foreach (int h in new int[] { 40, 76, 104 })
        {
            Rectangle bar = new Rectangle(34, 100, w - 68, h);
            foreach (bool right in new bool[] { false, true })
            {
                Rectangle r = (Rectangle)form.GetMethod("GuiaRojoFlecha", B).Invoke(null, new object[] { bar, right });
                if (r.Left < 0 || r.Right > w || r.Top < bar.Top || r.Bottom > bar.Bottom || r.IntersectsWith(bar))
                    throw new Exception("Flecha fuera de espacio o encima de letras");
                checks++;
            }
            using (Bitmap bmp = new Bitmap(w, 100 + h + 10))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(11, 18, 38)); g.SmoothingMode = SmoothingMode.AntiAlias;
                form.GetMethod("PintaGuiaRojo", B).Invoke(null, new object[] { g, bar, new Rectangle(300, 8, 270, 64) });
                if (w == 1024 && h == 40) bmp.Save(Path.Combine(Path.GetDirectoryName(args[0]), "guia-rojo.png"));
            }
        }
        string source = File.ReadAllText(Path.Combine(Path.Combine(Path.Combine(Environment.CurrentDirectory, ".."), "Rocola"), "Form1.Video.cs"));
        if (!source.Contains("sb = Mix(sb, GuiaRojoVisible());") || !source.Contains("n = FillGuiaRojoGlows(buf, n);"))
            throw new Exception("Faltan invalidación de entrada/salida o destellos GPU");
        Console.WriteLine("OK guía roja: " + checks + " estados/geometrías, dibujo aislado; no mide FPS reales.");
        return 0;
    }
}

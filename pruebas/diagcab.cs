using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

class DiagCab
{
    static Type TH;
    static Control Nuevo(string sec, int page, int total, int w, int h)
    {
        Control c = (Control)Activator.CreateInstance(TH, true);
        c.Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular);
        c.SetBounds(0, 0, w, h);
        TH.GetField("Section").SetValue(c, sec);
        TH.GetField("Page").SetValue(c, page);
        TH.GetField("Total").SetValue(c, total);
        TH.GetField("Accent").SetValue(c, Color.FromArgb(240, 150, 190));
        return c;
    }

    static void Mira(string sec, int page, int total, int w, int h)
    {
        Control c = Nuevo(sec, page, total, w, h);
        MethodInfo m = TH.GetMethod("RenderContent");
        using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                m.Invoke(c, new object[] { g });
            }
            int minX = int.MaxValue, maxX = -1;
            // columnas con tinta
            bool[] col = new bool[w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (b.GetPixel(x, y).A > 24) { col[x] = true; if (x < minX) minX = x; if (x > maxX) maxX = x; }
            // huecos: separa los bloques de tinta (pastilla / nombre / puntos / texto)
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int ini = -1, hueco = 0;
            for (int x = 0; x < w; x++)
            {
                if (col[x]) { if (ini < 0) ini = x; hueco = 0; }
                else if (ini >= 0) { hueco++; if (hueco > 6) { sb.Append("[" + ini + ".." + (x - hueco) + "] "); ini = -1; } }
            }
            if (ini >= 0) sb.Append("[" + ini + ".." + (w - 1) + "] ");
            Console.WriteLine("  w=" + w + " h=" + h + "  \"" + sec + "\" pag " + (page + 1) + "/" + total
                              + "   tinta " + minX + ".." + maxX + " (borde " + (w - 1) + ")   bloques: " + sb);
        }
        c.Dispose();
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        Type TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "HeaderView") TH = t;
        if (TH == null) { Console.WriteLine("no encuentro HeaderView"); return 1; }

        Console.WriteLine("\n--- anchos de encabezado reales (alto SIEMPRE 34) ---");
        // ancho = (W - pad) - (pad + helpW + 12), helpW = min(470, ...), pad se saca de la pantalla
        int[] W = { 1024, 1280, 1366, 1600, 1920, 2560, 3840 };
        foreach (int ww in W)
        {
            int pad = Math.Max(8, ww / 100);
            int helpW = Math.Max(120, Math.Min(470, (ww - pad) - pad));
            int headX = pad + helpW + 12;
            Console.WriteLine("   pantalla " + ww + "  ->  encabezado de " + Math.Max(0, (ww - pad) - headX) + " px de ancho");
        }

        Console.WriteLine("\n--- LO MAS SONADO ---");
        foreach (int ww in new int[] { 300, 400, 500, 600, 900, 1400 })
            Mira("LO MAS SONADO", 0, 3, ww, 34);

        Console.WriteLine("\n--- LETRA B con muchas paginas ---");
        foreach (int tot in new int[] { 1, 2, 9, 12, 13, 18, 24, 40 })
            Mira("LETRA B", 2, tot, 500, 34);

        Console.WriteLine("\n--- LETRA B, 18 paginas, a varios anchos ---");
        foreach (int ww in new int[] { 200, 260, 300, 400, 500, 900 })
            Mira("LETRA B", 2, 18, ww, 34);

        Console.WriteLine("\n--- LETRA A, 9 paginas (la que SI se ve bien) ---");
        foreach (int ww in new int[] { 300, 400, 500 })
            Mira("LETRA A", 0, 9, ww, 34);
        return 0;
    }
}

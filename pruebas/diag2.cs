using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

class Diag2
{
    static Type TH;

    // ancho de tinta del NOMBRE tal y como lo dibuja el encabezado (primer bloque tras la pastilla)
    static int NombreTinta(string sec, int page, int total, int w, int h, out int ini, out int fin)
    {
        Control c = (Control)Activator.CreateInstance(TH, true);
        c.Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular);
        c.SetBounds(0, 0, w, h);
        TH.GetField("Section").SetValue(c, sec);
        TH.GetField("Page").SetValue(c, page);
        TH.GetField("Total").SetValue(c, total);
        TH.GetField("Accent").SetValue(c, Color.FromArgb(240, 150, 190));
        ini = -1; fin = -1;
        using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                TH.GetMethod("RenderContent").Invoke(c, new object[] { g });
            }
            bool[] col = new bool[w];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (b.GetPixel(x, y).A > 24) col[x] = true;
            // primer bloque = pastilla; el nombre empieza en el siguiente
            int i = 0; while (i < w && !col[i]) i++;
            while (i < w && col[i]) i++;             // fin de la pastilla
            int hueco = 0; while (i < w && !col[i]) { i++; hueco++; }
            ini = i;
            int ult = -1, seguidos = 0;
            for (; i < w; i++)
            {
                if (col[i]) { ult = i; seguidos = 0; }
                else { seguidos++; if (seguidos > 10 && ult >= 0) break; }   // hueco grande = ya viene el separador
            }
            fin = ult;
        }
        c.Dispose();
        return (fin >= ini && ini >= 0) ? (fin - ini + 1) : 0;
    }

    static int Main(string[] argv)
    {
        Assembly asm = Assembly.LoadFrom((argv.Length > 0) ? argv[0] : "rocola.exe");
        Type TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "HeaderView") TH = t;

        Console.WriteLine("\n=== A) SE CORTA EL NOMBRE? (ancho de tinta del nombre a distintos anchos de caja) ===");
        Console.WriteLine("    si el nombre esta entero, el ancho de tinta es SIEMPRE el mismo\n");
        string[] secs = { "LO MAS SONADO", "LETRA B", "VIDEOS" };
        foreach (string s in secs)
        {
            foreach (int tot in new int[] { 1, 3, 20 })
            {
                int mx = 0; string det = "";
                for (int w = 180; w <= 1400; w += 4)
                {
                    int ini, fin; int tin = NombreTinta(s, 0, tot, w, 34, out ini, out fin);
                    if (tin > mx) mx = tin;
                }
                int cortados = 0; int wPeor = 0; int peor = int.MaxValue;
                for (int w = 180; w <= 1400; w += 4)
                {
                    int ini, fin; int tin = NombreTinta(s, 0, tot, w, 34, out ini, out fin);
                    if (tin < mx - 2 && tin > 0) { cortados++; if (tin < peor) { peor = tin; wPeor = w; } }
                }
                det = (cortados == 0) ? "entero siempre" : (cortados + " anchos lo cortan (el peor: caja de " + wPeor + " px, tinta " + peor + " en vez de " + mx + ")");
                Console.WriteLine("   \"" + s + "\" total=" + tot + "  ancho de tinta max " + mx + "  ->  " + det);
            }
        }

        Console.WriteLine("\n=== B) SE PIERDE EL \"pagina X de Y\"? ===");
        Console.WriteLine("    caja de 522 px (pantalla de 1024) y de 1400 px (pantalla de 1920)\n");
        foreach (int w in new int[] { 522, 1400 })
        {
            for (int tot = 2; tot <= 30; tot++)
            {
                Control c = (Control)Activator.CreateInstance(TH, true);
                c.Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular);
                c.SetBounds(0, 0, w, 34);
                TH.GetField("Section").SetValue(c, "LETRA B");
                TH.GetField("Page").SetValue(c, 2);
                TH.GetField("Total").SetValue(c, tot);
                TH.GetField("Accent").SetValue(c, Color.FromArgb(240, 150, 190));
                int derecha = 0;
                using (Bitmap b = new Bitmap(w, 34, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    { g.Clear(Color.FromArgb(0, 0, 0, 0)); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; TH.GetMethod("RenderContent").Invoke(c, new object[] { g }); }
                    for (int y = 0; y < 34; y++) for (int x = 0; x < w; x++) if (b.GetPixel(x, y).A > 24 && x > derecha) derecha = x;
                }
                c.Dispose();
                int libre = w - 1 - derecha;
                Console.WriteLine("   " + w + " px, " + tot + " paginas:  la tinta acaba en " + derecha + ", quedan " + libre + " px libres"
                                  + (libre > 40 ? "   <-- SOBRA SITIO Y NO PINTA EL TEXTO" : ""));
            }
            Console.WriteLine();
        }
        return 0;
    }
}

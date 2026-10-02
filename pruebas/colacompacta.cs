using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

// Regresion GPU: una cola corta conserva tarjetas compactas en cualquier resolucion.
// La geometria se comparte con el pintado real, para detectar textos/indices separados.
class ColaCompacta
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static Type form;
    static int checks;
    static object Call(string name, params object[] args) { return form.GetMethod(name, F).Invoke(null, args); }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static Rectangle Cell(int w, int h, int i, int n, bool dozen)
    { return (Rectangle)Call("IdleQueueCellCount", w, h, i, n, dozen); }
    static bool Contains(Rectangle r, RectangleF f)
    { return f.Left >= r.Left && f.Top >= r.Top && f.Right <= r.Right + .01f && f.Bottom <= r.Bottom + .01f; }

    static int Main(string[] args)
    {
        if (args.Length < 1) throw new Exception("Indicar el ejecutable GPU compilado.");
        Assembly asm = Assembly.LoadFrom(args[0]); form = asm.GetType("Rocola.Form1", true);
        Type extraType = asm.GetType("Rocola.ReposoGrande", true);
        string output = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "rocola-cola-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        int cases = 0, renders = 0;
        using (Bitmap cover = new Bitmap(32, 32))
        using (Graphics cg = Graphics.FromImage(cover))
        {
            cg.Clear(Color.Lime);
            foreach (int w in new int[] { 480, 640, 800, 1024, 1280, 1499, 1500, 1920, 2560, 3440, 3840 })
            foreach (int h in new int[] { 220, 240, 260, 300, 340 })
            foreach (int count in new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })
            foreach (bool dozen in new bool[] { false, true })
            {
                int visible = Math.Min(dozen ? 12 : 3, count);
                Rectangle sample = Cell(w, h, 0, visible, dozen);
                float scale = Math.Max(.8f, Math.Min(1.3f, h / 260f));
                using (Font title = new Font("Trebuchet MS", dozen ? Math.Max(9, Math.Min(21 * scale, (sample.Height - 8) * .52f)) : 20 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font artist = new Font("Trebuchet MS", dozen ? Math.Max(8, Math.Min(16 * scale, (sample.Height - 8) * .40f)) : 15 * scale, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font index = new Font("Trebuchet MS", Math.Min(artist.Size, ((Rectangle)Call("IdleQueueThumb", sample)).Width / 1.6f), FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    float titleH = title.GetHeight(cg), detailH = artist.GetHeight(cg) + 2;
                    int numberW = (int)Math.Ceiling(cg.MeasureString("12", artist).Width) + 4;
                    for (int i = 0; i < visible; i++)
                    {
                        Rectangle r = Cell(w, h, i, visible, dozen);
                        Rectangle thumb = (Rectangle)Call("IdleQueueThumb", r);
                        RectangleF text = (RectangleF)Call("IdleQueueTextBlock", r, titleH, detailH, numberW);
                        Check(r.Size == sample.Size, "Tarjetas asimetricas");
                        Check(r.X >= (int)(w * .52f) && r.Right <= w - 24 && r.Y >= 42 && r.Bottom <= h - 32, "Tarjeta fuera de panel");
                        if (dozen) Check(r.Width <= 520 && r.Height <= 79, "Tarjeta gigante: " + w + "x" + h + ", cola=" + count);
                        Check(r.Contains(thumb) && thumb.Width >= 20, "Miniatura recortada");
                        Check(Contains(r, text) && text.Left >= thumb.Right + 5, "Texto invade miniatura o tarjeta: " + w + "x" + h);
                        Check(Math.Abs((text.Top + text.Bottom) * .5f - (r.Top + r.Bottom) * .5f) <= 1f, "Texto no esta centrado con la miniatura");
                        if (r.Width < 180)
                        {
                            RectangleF number = (RectangleF)Call("IdleQueueNumberRect", r, index.GetHeight(cg));
                            Check(Contains(thumb, number), "Indice sale de la miniatura");
                            Check(number.Right < text.Left, "Indice invade titulo");
                        }
                        for (int j = 0; j < i; j++) Check(!r.IntersectsWith(Cell(w, h, j, visible, dozen)), "Cruce de tarjetas");
                    }
                }
                string[] titles = new string[visible], artists = new string[visible];
                Image[] covers = new Image[visible];
                for (int i = 0; i < visible; i++) { titles[i] = i == 0 ? "Robot Rock" : "Cancion larga que debe deslizarse completa " + i; artists[i] = "Daft Punk"; covers[i] = cover; }
                foreach (bool eta in new bool[] { false, true })
                {
                    object extra = null;
                    if (eta)
                    {
                        extra = Activator.CreateInstance(extraType, true);
                        int[] minutes = new int[visible]; for (int i = 0; i < visible; i++) minutes[i] = 5 + 5 * i;
                        extraType.GetField("Minutos", F).SetValue(extra, minutes);
                    }
                    object[] parameters = { w, h, "Da Funk", "Daft Punk", "0:17 / 5:28", titles, artists, covers, count, Color.MediumPurple, true, 2, dozen, extra, 0 };
                    using (Bitmap b = (Bitmap)Call("RenderIdlePanelGrande", parameters))
                    {
                        renders++;
                        for (int i = 0; i < visible; i++)
                        {
                            Rectangle thumb = (Rectangle)Call("IdleQueueThumb", Cell(w, h, i, visible, dozen));
                            Check(b.GetPixel(thumb.X + 1, thumb.Y + 1).ToArgb() == Color.Lime.ToArgb(), "Miniatura no llega al pintado real");
                        }
                        if (dozen && eta && (w == 800 || w == 1024 || w == 3440) && (h == 220 || h == 340) && (count == 1 || count == 2 || count == 12))
                        {
                            using (Bitmap preview = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
                            using (Graphics g = Graphics.FromImage(preview))
                            {
                                g.Clear(Color.FromArgb(18, 22, 49)); g.DrawImageUnscaled(b, 0, 0);
                                preview.Save(Path.Combine(output, "cola-" + w + "-" + h + "-" + count + ".png"), ImageFormat.Png);
                            }
                        }
                    }
                }
                cases++;
            }
        }
        Console.WriteLine("  OK    " + checks + " comprobaciones, " + cases + " rejillas (0..12), " + renders + " paneles con/sin espera. Vistas: " + output);
        return 0;
    }
}

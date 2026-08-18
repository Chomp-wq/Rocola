using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

// Aisla la pregunta: si mido con un formato y dibujo con OTRO, cabe el texto en la caja que medi?
class Diag3
{
    static int Tinta(Bitmap b, out int ini)
    {
        ini = -1; int fin = -1;
        for (int x = 0; x < b.Width; x++)
            for (int y = 0; y < b.Height; y++)
                if (b.GetPixel(x, y).A > 24) { if (ini < 0) ini = x; if (x > fin) fin = x; break; }
        return (fin >= 0) ? fin - ini + 1 : 0;
    }

    static void Prueba(string txt, float pt)
    {
        using (Font f = new Font(FontFamily.GenericSansSerif, pt, FontStyle.Bold))
        {
            StringFormat sfMedir = new StringFormat(StringFormat.GenericTypographic);
            sfMedir.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;

            StringFormat sfViejo = new StringFormat();     // el que usa el encabezado HOY (GenericDefault)
            sfViejo.Alignment = StringAlignment.Near; sfViejo.LineAlignment = StringAlignment.Center;
            sfViejo.FormatFlags = StringFormatFlags.NoWrap;

            StringFormat sfNuevo = new StringFormat(StringFormat.GenericTypographic);
            sfNuevo.Alignment = StringAlignment.Near; sfNuevo.LineAlignment = StringAlignment.Center;
            sfNuevo.FormatFlags |= StringFormatFlags.NoWrap;
            sfNuevo.FormatFlags &= ~StringFormatFlags.LineLimit;
            sfNuevo.Trimming = StringTrimming.None;

            float mTipo, mDefecto;
            using (Bitmap tmp = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(tmp))
            {
                mTipo = g.MeasureString(txt, f, int.MaxValue, sfMedir).Width;
                mDefecto = g.MeasureString(txt, f, int.MaxValue, sfViejo).Width;
            }
            int caja = (int)Math.Ceiling(mTipo + pt * 0.34f) + 6;   // lo que le da hoy el encabezado: AnchoTexto + 6

            int tintaViejo, tintaNuevo, tintaLibre, i;
            using (Bitmap b = new Bitmap(400, 40, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                { g.Clear(Color.Transparent); g.TextRenderingHint = TextRenderingHint.AntiAlias; g.SmoothingMode = SmoothingMode.AntiAlias;
                  g.DrawString(txt, f, Brushes.White, new RectangleF(4, 0, caja, 40), sfViejo); }
                tintaViejo = Tinta(b, out i);
            }
            using (Bitmap b = new Bitmap(400, 40, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                { g.Clear(Color.Transparent); g.TextRenderingHint = TextRenderingHint.AntiAlias; g.SmoothingMode = SmoothingMode.AntiAlias;
                  g.DrawString(txt, f, Brushes.White, new RectangleF(4, 0, caja, 40), sfNuevo); }
                tintaNuevo = Tinta(b, out i);
            }
            using (Bitmap b = new Bitmap(400, 40, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                { g.Clear(Color.Transparent); g.TextRenderingHint = TextRenderingHint.AntiAlias; g.SmoothingMode = SmoothingMode.AntiAlias;
                  g.DrawString(txt, f, Brushes.White, new RectangleF(4, 0, 380, 40), sfNuevo); }
                tintaLibre = Tinta(b, out i);
            }
            Console.WriteLine("  \"" + txt + "\" a " + pt.ToString("0.0") + " pt");
            Console.WriteLine("      medido tipografico " + mTipo.ToString("0.0") + " px | medido por defecto " + mDefecto.ToString("0.0")
                              + " px  (diferencia " + (mDefecto - mTipo).ToString("0.0") + ")");
            Console.WriteLine("      caja que le da el encabezado: " + caja + " px");
            Console.WriteLine("      tinta sin caja: " + tintaLibre + "   con formato de HOY: " + tintaViejo
                              + (tintaViejo < tintaLibre - 1 ? "  <-- CORTADO" : "  ok")
                              + "   con formato NUEVO: " + tintaNuevo + (tintaNuevo < tintaLibre - 1 ? "  <-- CORTADO" : "  ok"));
            sfMedir.Dispose(); sfViejo.Dispose(); sfNuevo.Dispose();
        }
    }

    static int Main()
    {
        Console.WriteLine("\n=== medir con un formato y dibujar con otro ===\n");
        foreach (string s in new string[] { "LO MAS SONADO", "LETRA B", "VIDEOS", "Inserta una moneda" })
            foreach (float pt in new float[] { 10.3f, 15.2f, 22f, 30f })
                Prueba(s, pt);
        return 0;
    }
}

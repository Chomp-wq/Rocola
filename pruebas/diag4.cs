using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

class Diag4
{
    static int Tinta(Bitmap b) { int ini=-1,fin=-1; for(int x=0;x<b.Width;x++) for(int y=0;y<b.Height;y++) if(b.GetPixel(x,y).A>24){ if(ini<0)ini=x; if(x>fin)fin=x; break;} return fin>=0?fin-ini+1:0; }
    static int Dibuja(string txt, Font f, StringFormat sf, float caja)
    {
        using (Bitmap b = new Bitmap(500, 44, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            { g.Clear(Color.Transparent); g.TextRenderingHint = TextRenderingHint.AntiAlias;
              g.DrawString(txt, f, Brushes.White, new RectangleF(4, 0, caja, 44), sf); }
            return Tinta(b);
        }
    }
    static int Main()
    {
        string txt = "LO MAS SONADO";
        using (Font f = new Font(FontFamily.GenericSansSerif, 15.2f, FontStyle.Bold))
        {
            StringFormat viejo = new StringFormat();                 // GenericDefault: Trimming = Character
            viejo.Alignment = StringAlignment.Near; viejo.LineAlignment = StringAlignment.Center;
            viejo.FormatFlags = StringFormatFlags.NoWrap;
            StringFormat nuevo = new StringFormat(StringFormat.GenericTypographic);
            nuevo.Alignment = StringAlignment.Near; nuevo.LineAlignment = StringAlignment.Center;
            nuevo.FormatFlags |= StringFormatFlags.NoWrap;
            nuevo.FormatFlags &= ~StringFormatFlags.LineLimit;
            nuevo.Trimming = StringTrimming.None;

            Console.WriteLine("  recorte del formato de HOY = " + viejo.Trimming + " ; del NUEVO = " + nuevo.Trimming);
            Console.WriteLine("  banderas de HOY = " + viejo.FormatFlags + " ; del NUEVO = " + nuevo.FormatFlags);
            int libre = Dibuja(txt, f, nuevo, 480);
            Console.WriteLine("\n  \"" + txt + "\" entero mide " + libre + " px de tinta\n");
            Console.WriteLine("     caja | formato de HOY | formato NUEVO");
            foreach (float caja in new float[] { 191, 185, 175, 160, 140, 120, 100 })
                Console.WriteLine("     " + caja.ToString("000") + "  |  " + Dibuja(txt, f, viejo, caja).ToString("000")
                                  + (Dibuja(txt, f, viejo, caja) < libre - 1 ? " CORTADO" : "        ")
                                  + "  |  " + Dibuja(txt, f, nuevo, caja).ToString("000")
                                  + (Dibuja(txt, f, nuevo, caja) < libre - 1 ? " CORTADO" : "        "));
            viejo.Dispose(); nuevo.Dispose();
        }
        return 0;
    }
}

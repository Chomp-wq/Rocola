using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// IDEAS PARA LA FRANJA DE ABAJO, dibujadas a TAMANO REAL sobre el mismo fondo, para poder elegir mirandolas.
//
// La primera tira es COMO ESTA HOY, con la barra de duracion de verdad sacada del ejecutable y los textos con
// las mismas letras y colores que usa el programa. Las demas son propuestas.
//
// Debajo de cada una va cuanto ALTO ocupa: cada pixel que la franja suelta se lo queda la rejilla de discos, y
// tres pixeles de franja son uno de portada (la rejilla tiene tres filas).
class Abajo
{
    const int W = 1024, PAD = 14;
    static FontFamily fam;
    static Type tProg;

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static readonly Color Verde = Color.FromArgb(120, 230, 165);
    static readonly Color Tenue = Color.FromArgb(150, 165, 200);
    static readonly Color Blanco = Color.FromArgb(236, 242, 255);
    static readonly Color Ambar = Color.FromArgb(255, 196, 96);
    static readonly Color Acento = Color.FromArgb(178, 152, 255);

    const string TITULO = "Depeche Mode - I Feel You";
    const string SIGUE = "Alejandro Fernandez - Que Seas Muy Feliz";
    const string RELOJ = "3:05 / 4:36";

    static void Fondo(Graphics g, int w, int h)
    {
        using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, w, h + 1),
                   Color.FromArgb(26, 20, 54), Color.FromArgb(52, 26, 78), 90f))
            g.FillRectangle(lb, 0, 0, w, h);
        Random r = new Random(11);
        for (int i = 0; i < w * h / 700; i++)
        {
            int x = r.Next(w), y = r.Next(h); int a = 25 + r.Next(60);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 220, 210, 255))) g.FillRectangle(b, x, y, 1, 1);
        }
    }

    // La barra de duracion DE VERDAD, sacada del ejecutable.
    static void Barra(Graphics g, int x, int y, int w, int h, double v, Color acento)
    {
        Control p = (Control)Activator.CreateInstance(tProg, true);
        p.SetBounds(0, 0, w, h);
        tProg.GetProperty("Value").SetValue(p, v, null);
        tProg.GetProperty("Accent").SetValue(p, acento, null);
        GraphicsState st = g.Save();
        g.TranslateTransform(x, y);
        tProg.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(p, new object[] { g });
        g.Restore(st);
    }

    static void Txt(Graphics g, string s, Font f, Color c, float x, float y)
    {
        using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.DrawString(s, f, b, x, y + 1.3f);
        using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
    }
    static float Ancho(Graphics g, string s, Font f) { return g.MeasureString(s, f).Width; }
    // Como una etiqueta de ancho fijo: lo que no cabe se corta con puntos suspensivos. Sin esto la maqueta
    // mentiria, porque el texto se saldria por encima de lo que tiene al lado.
    static void TxtCorta(Graphics g, string s, Font f, Color c, float x, float y, float ancho)
    {
        if (Ancho(g, s, f) > ancho)
        {
            while (s.Length > 1 && Ancho(g, s + "...", f) > ancho) s = s.Substring(0, s.Length - 1);
            s += "...";
        }
        Txt(g, s, f, c, x, y);
    }

    static GraphicsPath Redondo(RectangleF r, float radio)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radio * 2;
        if (d > r.Height) d = r.Height;
        if (d > r.Width) d = r.Width;
        if (d < 2) d = 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Un cuadrito que hace de portada (aqui no hay discos de verdad que poner).
    static void Portada(Graphics g, float x, float y, float lado)
    {
        using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(x, y, lado, lado + 1),
                   Color.FromArgb(70, 40, 110), Color.FromArgb(30, 18, 60), 45f))
        using (GraphicsPath p = Redondo(new RectangleF(x, y, lado, lado), 4))
            g.FillPath(lb, p);
        using (Pen pe = new Pen(Color.FromArgb(120, 190, 170, 255), 1f))
        using (GraphicsPath p = Redondo(new RectangleF(x, y, lado, lado), 4))
            g.DrawPath(pe, p);
    }

    // Una pastilla con texto dentro (creditos, cola...).
    static float Pastilla(Graphics g, float x, float y, float h, string etiq, string val, Font fe, Font fv, Color c)
    {
        float we = Ancho(g, etiq, fe), wv = Ancho(g, val, fv);
        float w = we + wv + h * 0.9f;
        using (GraphicsPath p = Redondo(new RectangleF(x, y, w, h), h / 2f))
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(46, c))) g.FillPath(b, p);
            using (Pen pe = new Pen(Color.FromArgb(90, c), 1f)) g.DrawPath(pe, p);
        }
        using (SolidBrush b = new SolidBrush(Color.FromArgb(210, 200, 210, 235)))
        using (StringFormat sf = new StringFormat())
        {
            sf.LineAlignment = StringAlignment.Center; sf.FormatFlags = StringFormatFlags.NoWrap;
            g.DrawString(etiq, fe, b, new RectangleF(x + h * 0.32f, y, we + 4, h), sf);
            using (SolidBrush b2 = new SolidBrush(c))
                g.DrawString(val, fv, b2, new RectangleF(x + h * 0.32f + we + 6, y, wv + 4, h), sf);
        }
        return w;
    }

    // ================= 0) COMO ESTA HOY =================
    static void Hoy(Graphics g, int h)
    {
        using (Font fNow = new Font(fam, 16f, FontStyle.Bold))
        using (Font fNext = new Font(fam, 13f, FontStyle.Bold))
        using (Font fTime = new Font(fam, 13f, FontStyle.Bold))
        using (Font fCred = new Font(fam, 18f, FontStyle.Bold))
        using (Font fCount = new Font(fam, 13f, FontStyle.Bold))
        using (Font fMej = new Font(fam, 12f, FontStyle.Bold))
        {
            int cubo = 84, rtW = 250;
            int rtX = W - PAD - rtW, cubeX = rtX - 12 - cubo;
            int progH = 22, progY = h - PAD - progH;
            int nowY = progY - 34;
            int leftW = cubeX - 14 - PAD;
            float tw = Ancho(g, "Reproduciendo:  " + TITULO, fNow);
            Txt(g, "Reproduciendo:  " + TITULO, fNow, Verde, PAD, nowY);
            Txt(g, "MEJORADA", fMej, Ambar, PAD + tw + 14, nowY + 5);
            int timeW = 150, nextW = (leftW * 4) / 10, progW = leftW - timeW - nextW - 24;
            Barra(g, PAD, progY, progW, progH, 0.67, Acento);
            Txt(g, RELOJ, fTime, Blanco, PAD + progW + 12, progY - 1);
            TxtCorta(g, "Siguiente:  " + SIGUE, fNext, Tenue, PAD + progW + 12 + timeW + 12, progY - 1, nextW);
            Portada(g, cubeX, nowY + 2, cubo);
            Txt(g, "Creditos:  3", fCred, Verde, rtX, nowY);
            Txt(g, "En cola:  2", fCount, Tenue, rtX, nowY + 38);
        }
    }

    // ================= 1) TARJETA DE LO QUE SUENA =================
    // La portada manda, y a su lado el titulo en dos renglones -cancion arriba, artista abajo- con la barra
    // debajo. Es como se ve en cualquier reproductor moderno, y ademas gana alto: el reloj y "siguiente" se
    // meten dentro de la propia tarjeta en vez de tener renglon propio.
    static void Tarjeta(Graphics g, int h)
    {
        using (Font fTit = new Font(fam, 15f, FontStyle.Bold))
        using (Font fArt = new Font(fam, 11.5f, FontStyle.Bold))
        using (Font fPeq = new Font(fam, 10f, FontStyle.Bold))
        using (Font fVal = new Font(fam, 15f, FontStyle.Bold))
        using (Font fEtq = new Font(fam, 10f, FontStyle.Bold))
        {
            float y0 = 8, cubo = h - 16;
            Portada(g, PAD, y0, cubo);
            float x = PAD + cubo + 14;
            float ph = 26, px = W - PAD;
            float wc = Ancho(g, "3", fVal) + Ancho(g, "Creditos", fEtq) + ph * 0.9f;
            float wq = Ancho(g, "2", fVal) + Ancho(g, "En cola", fEtq) + ph * 0.9f;
            Pastilla(g, px - wc, y0, ph, "Creditos", "3", fEtq, fVal, Verde);
            Pastilla(g, px - wc - 10 - wq, y0, ph, "En cola", "2", fEtq, fVal, Color.FromArgb(140, 205, 255));
            float derecha = px - wc - wq - 24;
            TxtCorta(g, "I Feel You", fTit, Blanco, x, y0, derecha - x);
            TxtCorta(g, "Depeche Mode", fArt, Verde, x, y0 + 25, derecha - x);
            float rw = Ancho(g, RELOJ, fPeq);
            TxtCorta(g, "Sigue:  " + SIGUE, fPeq, Tenue, x, y0 + 46, px - rw - 14 - x);
            float by = y0 + cubo - 14;
            Barra(g, (int)x, (int)by, (int)(px - x - rw - 12), 13, 0.67, Acento);
            Txt(g, RELOJ, fPeq, Blanco, px - rw, by - 3);
        }
    }

    // ================= 2) BARRA DE PROGRESO AL BORDE =================
    // Como en un reproductor de video: la barra de duracion es una linea fina pegada al borde de abajo, de
    // lado a lado, y encima queda todo el texto. Se gana el alto que ocupaba la barra gruesa y su margen.
    static void AlBorde(Graphics g, int h)
    {
        using (Font fNow = new Font(fam, 16f, FontStyle.Bold))
        using (Font fPeq = new Font(fam, 11.5f, FontStyle.Bold))
        using (Font fVal = new Font(fam, 17f, FontStyle.Bold))
        using (Font fEtq = new Font(fam, 10.5f, FontStyle.Bold))
        {
            float cubo = h - 26, y0 = 8;
            Portada(g, PAD, y0, cubo);
            float x = PAD + cubo + 14;
            float ph = 28, px = W - PAD;
            float wc = Ancho(g, "3", fVal) + Ancho(g, "Creditos", fEtq) + ph * 0.9f;
            float wq = Ancho(g, "2", fVal) + Ancho(g, "En cola", fEtq) + ph * 0.9f;
            Pastilla(g, px - wc, y0, ph, "Creditos", "3", fEtq, fVal, Verde);
            Pastilla(g, px - wc - 10 - wq, y0, ph, "En cola", "2", fEtq, fVal, Color.FromArgb(140, 205, 255));
            float rw = Ancho(g, RELOJ, fPeq);
            Txt(g, RELOJ, fPeq, Blanco, px - rw, y0 + 32);
            TxtCorta(g, TITULO, fNow, Blanco, x, y0, px - wc - wq - 24 - x);
            TxtCorta(g, "Sigue:  " + SIGUE, fPeq, Tenue, x, y0 + 30, px - rw - 16 - x);
            // la linea, de lado a lado, pegada abajo
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 10, 14, 34)))
                g.FillRectangle(b, 0, h - 7, W, 7);
            using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, h - 7, W * 0.67f, 8),
                       Color.FromArgb(255, 150, 210, 255), Acento, 0f))
                g.FillRectangle(lb, 0, h - 7, W * 0.67f, 7);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 235, 245, 255)))
                g.FillRectangle(b, W * 0.67f - 2, h - 9, 3, 11);
        }
    }

    // ================= 3) TODO EN UNA LINEA =================
    // Lo minimo: una sola fila. Portada chica, lo que suena, la barra, el reloj y las pastillas. Es la que
    // menos alto ocupa con diferencia, y por tanto la que mas crecer deja a los discos.
    static void UnaLinea(Graphics g, int h)
    {
        using (Font fNow = new Font(fam, 14f, FontStyle.Bold))
        using (Font fPeq = new Font(fam, 10.5f, FontStyle.Bold))
        using (Font fVal = new Font(fam, 14f, FontStyle.Bold))
        using (Font fEtq = new Font(fam, 9.5f, FontStyle.Bold))
        {
            float cubo = h - 16, y0 = 8;
            Portada(g, PAD, y0, cubo);
            float x = PAD + cubo + 12;
            float ph = 24, px = W - PAD;
            float wc = Ancho(g, "3", fVal) + Ancho(g, "Creditos", fEtq) + ph * 0.9f;
            float wq = Ancho(g, "2", fVal) + Ancho(g, "En cola", fEtq) + ph * 0.9f;
            Pastilla(g, px - wc, y0 + 8, ph, "Creditos", "3", fEtq, fVal, Verde);
            Pastilla(g, px - wc - 8 - wq, y0 + 8, ph, "En cola", "2", fEtq, fVal, Color.FromArgb(140, 205, 255));
            float rw = Ancho(g, RELOJ, fPeq);
            float finDer = px - wc - wq - 24;
            float bw = 190, bx = finDer - rw - 10 - bw;
            Barra(g, (int)bx, (int)(y0 + 15), (int)bw, 12, 0.67, Acento);
            Txt(g, RELOJ, fPeq, Blanco, bx + bw + 10, y0 + 11);
            TxtCorta(g, TITULO, fNow, Blanco, x, y0 + 1, bx - 16 - x);
            TxtCorta(g, "sigue: " + SIGUE, fPeq, Tenue, x, y0 + 23, bx - 16 - x);
        }
    }

    // ================= 4) ANILLO ALREDEDOR DE LA PORTADA =================
    // El tiempo deja de ser una barra: es un ANILLO que rodea la portada de lo que suena. Se lee de un
    // vistazo desde lejos, y libera toda la fila que ocupaba la barra.
    static void Anillo(Graphics g, int h)
    {
        using (Font fNow = new Font(fam, 16f, FontStyle.Bold))
        using (Font fPeq = new Font(fam, 11.5f, FontStyle.Bold))
        using (Font fVal = new Font(fam, 17f, FontStyle.Bold))
        using (Font fEtq = new Font(fam, 10.5f, FontStyle.Bold))
        {
            float cubo = h - 22, y0 = 10, cx = PAD + 5 + cubo / 2f, cy = y0 + cubo / 2f;
            float r = cubo / 2f + 6;
            using (Pen pe = new Pen(Color.FromArgb(60, 190, 200, 240), 5f)) g.DrawEllipse(pe, cx - r, cy - r, r * 2, r * 2);
            using (Pen pe = new Pen(Color.FromArgb(90, Acento), 9f))
            { pe.StartCap = LineCap.Round; pe.EndCap = LineCap.Round; g.DrawArc(pe, cx - r, cy - r, r * 2, r * 2, -90, (float)(360 * 0.67)); }
            using (Pen pe = new Pen(Color.FromArgb(245, 190, 225, 255), 4.5f))
            { pe.StartCap = LineCap.Round; pe.EndCap = LineCap.Round; g.DrawArc(pe, cx - r, cy - r, r * 2, r * 2, -90, (float)(360 * 0.67)); }
            Portada(g, cx - cubo / 2f + 3, cy - cubo / 2f + 3, cubo - 6);
            float x = cx + r + 18;
            float ph = 28, px = W - PAD;
            float wc = Ancho(g, "3", fVal) + Ancho(g, "Creditos", fEtq) + ph * 0.9f;
            float wq = Ancho(g, "2", fVal) + Ancho(g, "En cola", fEtq) + ph * 0.9f;
            Pastilla(g, px - wc, y0 + 4, ph, "Creditos", "3", fEtq, fVal, Verde);
            Pastilla(g, px - wc - 10 - wq, y0 + 4, ph, "En cola", "2", fEtq, fVal, Color.FromArgb(140, 205, 255));
            float lim = px - wc - wq - 24 - x;
            TxtCorta(g, TITULO, fNow, Blanco, x, y0 + 2, lim);
            TxtCorta(g, "Sigue:  " + SIGUE, fPeq, Tenue, x, y0 + 30, px - 14 - x);
            Txt(g, RELOJ, fPeq, Blanco, x, y0 + 52);
        }
    }

    delegate void Pintor(Graphics g, int h);
    class Op
    {
        public string Tit, Nota; public int H; public Pintor P;
        public Op(string t, int hh, string n, Pintor p) { Tit = t; H = hh; Nota = n; P = p; }
    }

    static int Main()
    {
        fam = Fam();
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type tf = asm.GetType("Rocola.Form1", true);
        foreach (Type t in tf.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "ProgressView") tProg = t;

        Op[] ops = {
            new Op("COMO ESTA HOY", 114, "la de verdad, con la barra de duracion sacada del ejecutable", Hoy),
            new Op("1. TARJETA", 96, "la portada manda; cancion y artista en dos renglones, y el reloj dentro de la tarjeta", Tarjeta),
            new Op("2. BARRA AL BORDE", 84, "la duracion es una linea fina de lado a lado, pegada al borde, como en un video", AlBorde),
            new Op("3. TODO EN UNA LINEA", 62, "lo minimo: una sola fila. La que mas espacio le deja a los discos", UnaLinea),
            new Op("4. ANILLO", 100, "el tiempo rodea la portada en vez de ser una barra: se lee de un vistazo", Anillo)
        };

        int alto = 62;
        foreach (Op o in ops) alto += 34 + o.H + 22;
        using (Bitmap bmp = new Bitmap(W, alto, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            using (SolidBrush f = new SolidBrush(Color.FromArgb(7, 9, 20))) g.FillRectangle(f, 0, 0, W, alto);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (Font ft = new Font(fam, 13f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 240, 252)))
                g.DrawString("LA FRANJA DE ABAJO  -  ideas, a tamano real", ft, b, 14, 14);
            using (Font fn = new Font(fam, 8.5f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 150, 180)))
                g.DrawString("cada 3 px que suelte la franja son 1 px mas de portada (la rejilla tiene tres filas)", fn, b, 14, 34);
            int y = 62;
            foreach (Op o in ops)
            {
                int gana = (114 - o.H) / 3;
                string extra = (o.H == 114) ? "" : ("  ->  suelta " + (114 - o.H) + " px, la portada pasa de 133 a " + (133 + gana) + " px");
                using (Font ft = new Font(fam, 9.5f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 190, 250)))
                    g.DrawString(o.Tit + "   (" + o.H + " px de alto)" + extra, ft, b, PAD, y);
                using (Font fn = new Font(fam, 8f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 132, 162)))
                    g.DrawString(o.Nota, fn, b, PAD, y + 16);
                using (Bitmap tira = new Bitmap(W, o.H, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics gt = Graphics.FromImage(tira))
                    {
                        gt.SmoothingMode = SmoothingMode.AntiAlias;
                        gt.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        Fondo(gt, W, o.H);
                        o.P(gt, o.H);
                    }
                    g.DrawImageUnscaled(tira, 0, y + 32);
                }
                y += 34 + o.H + 22;
            }
            bmp.Save("franja_abajo.png", ImageFormat.Png);
        }
        Console.WriteLine("listo: franja_abajo.png");
        return 0;
    }
}

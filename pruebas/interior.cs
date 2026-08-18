using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// IDEAS PARA EL INTERIOR DE UN DISCO y para los RESULTADOS DEL BUSCADOR.
//
// La primera tira de cada bloque es COMO ESTA HOY: la lista de verdad, sacada del ejecutable y pintada con su
// propio codigo. Las demas son propuestas dibujadas al mismo tamano y sobre el mismo fondo.
class Interior
{
    const int W = 1024, LW = 620, ROWH = 34, FILAS = 6;
    static FontFamily fam;
    static Type tLista;

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static readonly Color Acento = Color.FromArgb(178, 152, 255);
    static readonly Color Fondo = Color.FromArgb(24, 18, 46);
    static readonly Color Texto = Color.FromArgb(232, 238, 252);
    static readonly Color Tenue = Color.FromArgb(158, 168, 200);
    static readonly Color Verde = Color.FromArgb(120, 230, 165);
    static readonly Color Oro = Color.FromArgb(255, 205, 90);

    static readonly string[] CANCIONES = {
        "Enjoy The Silence", "Personal Jesus", "Never Let Me Down Again",
        "Policy Of Truth", "Walking In My Shoes", "It's No Good"
    };
    static readonly string[] DURA = { "6:12", "4:56", "4:47", "4:55", "5:35", "5:58" };
    static readonly int[] VECES = { 41, 63, 12, 5, 0, 22 };   // cuantas veces se ha puesto cada una
    static readonly int[] COLA = { 0, 1, 0, 0, 2, 0 };        // 0 = no esta en cola; n = puesto n en la cola
    const int SEL = 1;

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

    static void Txt(Graphics g, string s, Font f, Color c, float x, float y, float w, float h, StringAlignment al)
    {
        using (StringFormat sf = new StringFormat())
        {
            sf.Alignment = al; sf.LineAlignment = StringAlignment.Center;
            sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
            if (g.MeasureString(s, f).Width > w)
            {
                while (s.Length > 1 && g.MeasureString(s + "...", f).Width > w) s = s.Substring(0, s.Length - 1);
                s += "...";
            }
            using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, new RectangleF(x, y, w, h), sf);
        }
    }

    static void Seleccion(Graphics g, float y, float w, Color ac)
    {
        RectangleF hl = new RectangleF(4, y + 3, w - 8, ROWH - 6);
        using (GraphicsPath gp = Redondo(hl, 9))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(150, ac)))
        using (Pen p = new Pen(Color.FromArgb(180, Color.FromArgb(255, Math.Min(255, ac.R + 60), Math.Min(255, ac.G + 60), Math.Min(255, ac.B + 60))), 1.4f))
        { g.FillPath(b, gp); g.DrawPath(p, gp); }
    }

    // ================= 0) LA LISTA DE HOY, del ejecutable =================
    static void Hoy(Graphics g)
    {
        Control c = (Control)Activator.CreateInstance(tLista, true);
        c.SetBounds(0, 0, LW, ROWH * FILAS);
        c.Font = new Font(fam, 11f, FontStyle.Bold);
        tLista.GetField("RowH").SetValue(c, ROWH);
        tLista.GetField("Sel").SetValue(c, SEL);
        tLista.GetField("Accent").SetValue(c, Acento);
        tLista.GetField("GetCount").SetValue(c, new Func<int>(delegate { return CANCIONES.Length; }));
        tLista.GetField("GetText").SetValue(c, new Func<int, string>(delegate (int i) { return (i + 1) + ".  " + CANCIONES[i]; }));
        tLista.GetField("GetRight").SetValue(c, new Func<int, string>(delegate (int i) { return DURA[i]; }));
        tLista.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g });
    }

    // ================= 1) NUMERO EN CIRCULO + LO QUE YA ESTA EN COLA =================
    // Cada cancion lleva su numero de pista en un circulito, y la que ya esta pedida se marca con su PUESTO en
    // la cola. Hoy no hay forma de saber si ya la pediste: la vuelves a pedir y gastas credito.
    static void Circulos(Graphics g)
    {
        using (SolidBrush bg = new SolidBrush(Fondo)) g.FillRectangle(bg, 0, 0, LW, ROWH * FILAS);
        using (Font f = new Font(fam, 11f, FontStyle.Bold))
        using (Font fn = new Font(fam, 8.5f, FontStyle.Bold))
        using (Font fc = new Font(fam, 8f, FontStyle.Bold))
            for (int i = 0; i < FILAS; i++)
            {
                float y = i * ROWH;
                if (i == SEL) Seleccion(g, y, LW, Acento);
                float cy = y + ROWH / 2f, r = 11f;
                bool enCola = COLA[i] > 0;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(enCola ? 70 : 34, enCola ? Verde : Color.FromArgb(150, 170, 220))))
                    g.FillEllipse(b, 14, cy - r, r * 2, r * 2);
                using (Pen p = new Pen(Color.FromArgb(enCola ? 150 : 60, enCola ? Verde : Color.FromArgb(150, 170, 220)), 1f))
                    g.DrawEllipse(p, 14, cy - r, r * 2, r * 2);
                Txt(g, (i + 1).ToString(), fn, enCola ? Verde : Tenue, 14, cy - r, r * 2, r * 2, StringAlignment.Center);
                float x = 14 + r * 2 + 12;
                float wDer = 58, xCola = LW - 14 - wDer - 88;
                Txt(g, CANCIONES[i], f, i == SEL ? Color.White : Texto, x, y, xCola - x - 10, ROWH, StringAlignment.Near);
                if (enCola)
                {
                    using (GraphicsPath gp = Redondo(new RectangleF(xCola, cy - 9, 82, 18), 9))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(52, Verde))) g.FillPath(b, gp);
                        using (Pen p = new Pen(Color.FromArgb(110, Verde), 1f)) g.DrawPath(p, gp);
                    }
                    Txt(g, "EN COLA " + COLA[i], fc, Verde, xCola, cy - 9, 82, 18, StringAlignment.Center);
                }
                Txt(g, DURA[i], f, i == SEL ? Color.FromArgb(235, 245, 255) : Color.FromArgb(175, 190, 215),
                    LW - 14 - wDer, y, wDer, ROWH, StringAlignment.Far);
            }
    }

    // ================= 2) CUANTO SE HA PEDIDO CADA UNA =================
    // Una barrita detras del titulo dice cuantas veces se ha puesto esa cancion en el bar. El dato YA se
    // guarda (es el que arma "lo mas sonado"); aqui solo se ensena donde sirve para elegir.
    static void Popular(Graphics g)
    {
        using (SolidBrush bg = new SolidBrush(Fondo)) g.FillRectangle(bg, 0, 0, LW, ROWH * FILAS);
        int max = 0;
        for (int i = 0; i < FILAS; i++) if (VECES[i] > max) max = VECES[i];
        using (Font f = new Font(fam, 11f, FontStyle.Bold))
        using (Font fn = new Font(fam, 8f, FontStyle.Bold))
            for (int i = 0; i < FILAS; i++)
            {
                float y = i * ROWH;
                if (i == SEL) Seleccion(g, y, LW, Acento);
                float wDer = 58, wPop = 92, xPop = LW - 14 - wDer - 12 - wPop;
                Txt(g, (i + 1) + ".  " + CANCIONES[i], f, i == SEL ? Color.White : Texto, 16, y, xPop - 26, ROWH, StringAlignment.Near);
                if (VECES[i] > 0)
                {
                    float frac = (float)VECES[i] / max;
                    float by = y + ROWH / 2f - 3;
                    using (GraphicsPath gp = Redondo(new RectangleF(xPop, by, wPop - 26, 6), 3))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 190, 200, 240))) g.FillPath(b, gp);
                    using (GraphicsPath gp = Redondo(new RectangleF(xPop, by, Math.Max(4f, (wPop - 26) * frac), 6), 3))
                    using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(xPop, by, wPop, 7),
                               Oro, Color.FromArgb(255, 140, 90), 0f)) g.FillPath(lb, gp);
                    Txt(g, VECES[i].ToString(), fn, Oro, xPop + wPop - 24, y, 24, ROWH, StringAlignment.Far);
                }
                Txt(g, DURA[i], f, i == SEL ? Color.FromArgb(235, 245, 255) : Color.FromArgb(175, 190, 215),
                    LW - 14 - wDer, y, wDer, ROWH, StringAlignment.Far);
            }
    }

    // ================= 3) FILAS ALTERNADAS Y SEPARADORES =================
    // Lo mas discreto: una franja apenas mas clara en las filas pares y una raya finisima entre ellas. En una
    // lista de veinte canciones el ojo no se pierde de renglon.
    static void Cebra(Graphics g)
    {
        using (SolidBrush bg = new SolidBrush(Fondo)) g.FillRectangle(bg, 0, 0, LW, ROWH * FILAS);
        using (Font f = new Font(fam, 11f, FontStyle.Bold))
            for (int i = 0; i < FILAS; i++)
            {
                float y = i * ROWH;
                if ((i & 1) == 0)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(16, 190, 205, 255)))
                        g.FillRectangle(b, 0, y, LW, ROWH);
                using (Pen p = new Pen(Color.FromArgb(22, 190, 205, 255)))
                    g.DrawLine(p, 14, y + ROWH - 1, LW - 14, y + ROWH - 1);
                if (i == SEL) Seleccion(g, y, LW, Acento);
                float wDer = 58;
                Txt(g, (i + 1) + ".  " + CANCIONES[i], f, i == SEL ? Color.White : Texto, 16, y, LW - 30 - wDer - 16, ROWH, StringAlignment.Near);
                Txt(g, DURA[i], f, i == SEL ? Color.FromArgb(235, 245, 255) : Color.FromArgb(175, 190, 215),
                    LW - 14 - wDer, y, wDer, ROWH, StringAlignment.Far);
            }
    }

    // ================= 4) TODO JUNTO =================
    static void Todo(Graphics g)
    {
        using (SolidBrush bg = new SolidBrush(Fondo)) g.FillRectangle(bg, 0, 0, LW, ROWH * FILAS);
        int max = 0;
        for (int i = 0; i < FILAS; i++) if (VECES[i] > max) max = VECES[i];
        using (Font f = new Font(fam, 11f, FontStyle.Bold))
        using (Font fn = new Font(fam, 8.5f, FontStyle.Bold))
        using (Font fc = new Font(fam, 8f, FontStyle.Bold))
            for (int i = 0; i < FILAS; i++)
            {
                float y = i * ROWH;
                if ((i & 1) == 0)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(16, 190, 205, 255)))
                        g.FillRectangle(b, 0, y, LW, ROWH);
                if (i == SEL) Seleccion(g, y, LW, Acento);
                float cy = y + ROWH / 2f, r = 11f;
                bool enCola = COLA[i] > 0;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(enCola ? 70 : 34, enCola ? Verde : Color.FromArgb(150, 170, 220))))
                    g.FillEllipse(b, 14, cy - r, r * 2, r * 2);
                using (Pen p = new Pen(Color.FromArgb(enCola ? 150 : 60, enCola ? Verde : Color.FromArgb(150, 170, 220)), 1f))
                    g.DrawEllipse(p, 14, cy - r, r * 2, r * 2);
                Txt(g, (i + 1).ToString(), fn, enCola ? Verde : Tenue, 14, cy - r, r * 2, r * 2, StringAlignment.Center);
                float x = 14 + r * 2 + 12;
                float wDer = 52, wPop = 74, xPop = LW - 12 - wDer - 10 - wPop;
                float xCola = enCola ? (xPop - 90) : xPop;
                Txt(g, CANCIONES[i], f, i == SEL ? Color.White : Texto, x, y, xCola - x - 10, ROWH, StringAlignment.Near);
                if (enCola)
                {
                    using (GraphicsPath gp = Redondo(new RectangleF(xCola, cy - 9, 82, 18), 9))
                    {
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(52, Verde))) g.FillPath(b, gp);
                        using (Pen p = new Pen(Color.FromArgb(110, Verde), 1f)) g.DrawPath(p, gp);
                    }
                    Txt(g, "EN COLA " + COLA[i], fc, Verde, xCola, cy - 9, 82, 18, StringAlignment.Center);
                }
                if (VECES[i] > 0)
                {
                    float frac = (float)VECES[i] / max, by = cy - 3;
                    using (GraphicsPath gp = Redondo(new RectangleF(xPop, by, wPop - 22, 6), 3))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 190, 200, 240))) g.FillPath(b, gp);
                    using (GraphicsPath gp = Redondo(new RectangleF(xPop, by, Math.Max(4f, (wPop - 22) * frac), 6), 3))
                    using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(xPop, by, wPop, 7),
                               Oro, Color.FromArgb(255, 140, 90), 0f)) g.FillPath(lb, gp);
                    Txt(g, VECES[i].ToString(), fc, Oro, xPop + wPop - 20, y, 20, ROWH, StringAlignment.Far);
                }
                Txt(g, DURA[i], f, i == SEL ? Color.FromArgb(235, 245, 255) : Color.FromArgb(175, 190, 215),
                    LW - 12 - wDer, y, wDer, ROWH, StringAlignment.Far);
            }
    }

    // ============================================================================
    //                          RESULTADOS DEL BUSCADOR
    // ============================================================================
    static readonly string[] RES = {
        "Depeche Mode - Enjoy The Silence", "Depeche Mode - Personal Jesus",
        "Depeche Mode - Policy Of Truth", "Deep Purple - Smoke On The Water",
        "Def Leppard - Pour Some Sugar On Me", "Deftones - Change"
    };
    static readonly string[] RESDISCO = { "Violator", "Violator", "Violator", "Machine Head", "Hysteria", "White Pony" };
    static readonly string[] RESGEN = { "Electronica", "Electronica", "Electronica", "Rock", "Rock", "Metal" };
    static readonly Color[] RESCOL = {
        Color.FromArgb(30,175,195), Color.FromArgb(30,175,195), Color.FromArgb(30,175,195),
        Color.FromArgb(215,90,40), Color.FromArgb(215,90,40), Color.FromArgb(205,45,55) };
    const string CONSULTA = "DE";

    static void ResHoy(Graphics g)
    {
        using (SolidBrush bg = new SolidBrush(Fondo)) g.FillRectangle(bg, 0, 0, LW, ROWH * FILAS);
        using (Font f = new Font(fam, 11f, FontStyle.Bold))
            for (int i = 0; i < FILAS; i++)
            {
                float y = i * ROWH;
                if (i == SEL) Seleccion(g, y, LW, Acento);
                Txt(g, RES[i], f, i == SEL ? Color.White : Texto, 16, y, LW - 32, ROWH, StringAlignment.Near);
            }
    }

    // 1) LO QUE COINCIDE, RESALTADO + de que disco es y de que genero
    static void ResRico(Graphics g)
    {
        using (SolidBrush bg = new SolidBrush(Fondo)) g.FillRectangle(bg, 0, 0, LW, ROWH * FILAS);
        using (Font f = new Font(fam, 11f, FontStyle.Bold))
        using (Font fp = new Font(fam, 8.5f, FontStyle.Bold))
        using (Font fg2 = new Font(fam, 7.5f, FontStyle.Bold))
            for (int i = 0; i < FILAS; i++)
            {
                float y = i * ROWH;
                if (i == SEL) Seleccion(g, y, LW, Acento);
                // pastilla de genero a la derecha
                float wg = g.MeasureString(RESGEN[i], fg2).Width + 18;
                using (GraphicsPath gp = Redondo(new RectangleF(LW - 14 - wg, y + ROWH / 2f - 9, wg, 18), 9))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(48, RESCOL[i]))) g.FillPath(b, gp);
                    using (Pen p = new Pen(Color.FromArgb(100, RESCOL[i]), 1f)) g.DrawPath(p, gp);
                }
                Txt(g, RESGEN[i], fg2, Color.FromArgb(255, Math.Min(255, RESCOL[i].R + 60), Math.Min(255, RESCOL[i].G + 60), Math.Min(255, RESCOL[i].B + 60)),
                    LW - 14 - wg, y + ROWH / 2f - 9, wg, 18, StringAlignment.Center);
                // el disco, en gris, detras del titulo
                float wDisc = g.MeasureString(RESDISCO[i], fp).Width + 8;
                Txt(g, RESDISCO[i], fp, Color.FromArgb(130, 142, 175), LW - 14 - wg - 12 - wDisc, y, wDisc, ROWH, StringAlignment.Far);
                // el titulo, con la parte tecleada encendida
                string s = RES[i];
                int k = s.ToUpperInvariant().IndexOf(CONSULTA);
                float x = 16, limite = LW - 14 - wg - 12 - wDisc - 12;
                if (k >= 0)
                {
                    string a = s.Substring(0, k), b2 = s.Substring(k, CONSULTA.Length), c2 = s.Substring(k + CONSULTA.Length);
                    float wa = g.MeasureString(a, f).Width, wb = g.MeasureString(b2, f).Width;
                    Txt(g, a, f, i == SEL ? Color.White : Texto, x, y, wa + 4, ROWH, StringAlignment.Near);
                    using (GraphicsPath gp = Redondo(new RectangleF(x + wa - 2, y + ROWH / 2f - 10, wb + 4, 20), 5))
                    using (SolidBrush bb = new SolidBrush(Color.FromArgb(70, Oro))) g.FillPath(bb, gp);
                    Txt(g, b2, f, Oro, x + wa, y, wb + 4, ROWH, StringAlignment.Near);
                    Txt(g, c2, f, i == SEL ? Color.White : Texto, x + wa + wb, y, limite - x - wa - wb, ROWH, StringAlignment.Near);
                }
                else Txt(g, s, f, Texto, x, y, limite - x, ROWH, StringAlignment.Near);
            }
    }

    // 2) CON PORTADA
    static void ResPortada(Graphics g)
    {
        int rh = ROWH + 12;
        using (SolidBrush bg = new SolidBrush(Fondo)) g.FillRectangle(bg, 0, 0, LW, rh * 4);
        using (Font f = new Font(fam, 11.5f, FontStyle.Bold))
        using (Font fp = new Font(fam, 9f, FontStyle.Bold))
            for (int i = 0; i < 4; i++)
            {
                float y = i * rh;
                if (i == SEL)
                {
                    RectangleF hl = new RectangleF(4, y + 3, LW - 8, rh - 6);
                    using (GraphicsPath gp = Redondo(hl, 9))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, Acento)))
                    using (Pen p = new Pen(Color.FromArgb(180, 210, 190, 255), 1.4f))
                    { g.FillPath(b, gp); g.DrawPath(p, gp); }
                }
                float lado = rh - 12, cx = 14, cy = y + 6;
                using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(cx, cy, lado, lado + 1),
                           Color.FromArgb(RESCOL[i].R / 2, RESCOL[i].G / 2, RESCOL[i].B / 2), Color.FromArgb(24, 16, 48), 45f))
                using (GraphicsPath gp = Redondo(new RectangleF(cx, cy, lado, lado), 4)) g.FillPath(lb, gp);
                using (Pen p = new Pen(Color.FromArgb(110, RESCOL[i]), 1f))
                using (GraphicsPath gp = Redondo(new RectangleF(cx, cy, lado, lado), 4)) g.DrawPath(p, gp);
                float x = cx + lado + 12;
                Txt(g, RES[i], f, i == SEL ? Color.White : Texto, x, y + 4, LW - x - 20, rh / 2f, StringAlignment.Near);
                Txt(g, RESDISCO[i] + "   -   " + RESGEN[i], fp, Color.FromArgb(140, 152, 188), x, y + rh / 2f, LW - x - 20, rh / 2f - 4, StringAlignment.Near);
            }
    }

    delegate void Pintor(Graphics g);
    class Op { public string T, N; public int H; public Pintor P; public Op(string t, int h, string n, Pintor p) { T = t; H = h; N = n; P = p; } }

    static int Main()
    {
        fam = Fam();
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type tf = asm.GetType("Rocola.Form1", true);
        foreach (Type t in tf.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "SongListView") tLista = t;

        Op[] ops = {
            new Op("DENTRO DE UN DISCO  -  COMO ESTA HOY", ROWH * FILAS, "la lista de verdad, dibujada por el propio programa", Hoy),
            new Op("A.  NUMERO EN CIRCULO + LO QUE YA PEDISTE", ROWH * FILAS, "hoy no hay forma de saber si ya pediste una cancion: la vuelves a pedir y gastas credito", Circulos),
            new Op("B.  CUANTO SE HA PEDIDO CADA UNA", ROWH * FILAS, "el dato ya se guarda (es el que arma lo mas sonado); aqui se ensena donde sirve para elegir", Popular),
            new Op("C.  FILAS ALTERNADAS", ROWH * FILAS, "lo mas discreto: en una lista larga el ojo no se pierde de renglon", Cebra),
            new Op("D.  TODO JUNTO", ROWH * FILAS, "circulo + en cola + cuanto se pide + filas alternadas", Todo),
            new Op("BUSCADOR  -  COMO ESTA HOY", ROWH * FILAS, "solo el texto; la lista de verdad, con su codigo", ResHoy),
            new Op("E.  LO TECLEADO ENCENDIDO + DISCO Y GENERO", ROWH * FILAS, "se ve POR QUE sale cada resultado, de que disco es y de que genero", ResRico),
            new Op("F.  CON PORTADA", (ROWH + 12) * 4, "la portada a la izquierda: se reconoce el disco de un vistazo, pero caben menos resultados", ResPortada)
        };

        int alto = 56;
        foreach (Op o in ops) alto += 34 + o.H + 20;
        using (Bitmap bmp = new Bitmap(W, alto, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            using (SolidBrush f = new SolidBrush(Color.FromArgb(7, 9, 20))) g.FillRectangle(f, 0, 0, W, alto);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (Font ft = new Font(fam, 13f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 240, 252)))
                g.DrawString("IDEAS  -  dentro de un disco y resultados del buscador", ft, b, 14, 12);
            using (Font fn = new Font(fam, 8.5f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 150, 180)))
                g.DrawString("tamano real; la busqueda del ejemplo es \"DE\"", fn, b, 14, 32);
            int y = 56;
            foreach (Op o in ops)
            {
                using (Font ft = new Font(fam, 9.5f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 190, 250)))
                    g.DrawString(o.T, ft, b, 14, y);
                using (Font fn = new Font(fam, 8f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 132, 162)))
                    g.DrawString(o.N, fn, b, 14, y + 15);
                using (Bitmap tira = new Bitmap(LW, o.H, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics gt = Graphics.FromImage(tira))
                    {
                        gt.SmoothingMode = SmoothingMode.AntiAlias;
                        gt.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        o.P(gt);
                    }
                    g.DrawImageUnscaled(tira, 14, y + 32);
                }
                y += 34 + o.H + 20;
            }
            bmp.Save("interior.png", ImageFormat.Png);
        }
        Console.WriteLine("listo: interior.png");
        return 0;
    }
}

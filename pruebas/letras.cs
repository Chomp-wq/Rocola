using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// IDEAS PARA LA BARRA DEL ABECEDARIO, dibujadas para poder ELEGIR mirandolas.
//
// La primera tira ("COMO ESTA HOY") NO es un dibujo mio: es la barra de verdad, sacada del ejecutable por
// reflexion y pintada con su propio codigo. Asi la comparacion es honesta y no se compara lo que hay contra
// una version embellecida de lo que hay.
//
// Todas las tiras se pintan al TAMANO REAL que tienen en la rocola (996 x 40 en una pantalla de 1024) y sobre
// el mismo fondo, para que la comparacion valga. Ademas se saca una segunda imagen con un trozo AMPLIADO,
// porque a tamano real el detalle fino casi no se apreciaria en una foto.
class Letras
{
    const int W = 1024, PAD = 14, BARW = W - PAD * 2, BARH = 40;
    const string LETRAS = "★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
    const char ACTUAL = 'M';

    static HashSet<char> disponibles;
    static Dictionary<char, int> cuentas = new Dictionary<char, int>();
    static Dictionary<char, Color> generos = new Dictionary<char, Color>();
    static FontFamily fam;

    static FontFamily Fam()
    {
        string[] pref = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string p in pref) try { return new FontFamily(p); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static void Datos()
    {
        // Un reparto realista: casi todo tiene canciones, unas pocas letras no.
        disponibles = new HashSet<char>();
        string sin = "KQWXY";
        int[] n = { 0, 12, 41, 33, 52, 28, 19, 24, 31, 17, 22, 9, 0, 47, 38, 21, 26, 14, 0, 44, 39, 29, 16, 11, 0, 0, 0, 8 };
        int k = 0;
        foreach (char c in LETRAS)
        {
            if (c == '★') { disponibles.Add(c); cuentas[c] = 25; continue; }
            if (c == '►') { disponibles.Add(c); cuentas[c] = 37; continue; }
            int v = (k < n.Length) ? n[k] : 10; k++;
            if (sin.IndexOf(c) >= 0) v = 0;
            cuentas[c] = v;
            if (v > 0) disponibles.Add(c);
        }
        // Genero que MANDA en cada letra (el color es el mismo lenguaje de color que ya usa la rocola).
        Color cumbia = Color.FromArgb(255, 176, 102), rock = Color.FromArgb(178, 152, 255);
        Color bal = Color.FromArgb(140, 205, 255), nor = Color.FromArgb(120, 230, 165);
        Color reg = Color.FromArgb(255, 140, 170);
        Color[] pal = { cumbia, rock, bal, nor, reg };
        int j = 0;
        foreach (char c in LETRAS) { generos[c] = pal[j % pal.Length]; j++; }
    }

    // ---------- fondo comun ----------
    static void Fondo(Graphics g, int w, int h, int semilla)
    {
        using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                   Color.FromArgb(10, 13, 30), Color.FromArgb(15, 19, 42), 90f))
            g.FillRectangle(lb, 0, 0, w, h);
        Random r = new Random(semilla);
        for (int i = 0; i < w * h / 900; i++)
        {
            int x = r.Next(w), y = r.Next(h); int a = 30 + r.Next(70);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 200, 215, 255))) g.FillRectangle(b, x, y, 1, 1);
        }
    }

    static StringFormat Cen()
    {
        StringFormat s = new StringFormat();
        s.Alignment = StringAlignment.Center; s.LineAlignment = StringAlignment.Center;
        s.FormatFlags = StringFormatFlags.NoWrap;
        return s;
    }
    // El mismo ajuste que hace el programa: el cuerpo mas grande con el que la palabra QUEPA en su casilla.
    static Font FuentePal()
    {
        float hueco = (float)BARW / LETRAS.Length - 2f, cuerpo = 7.5f;
        using (Bitmap b = new Bitmap(4, 4))
        using (Graphics g = Graphics.FromImage(b))
            for (; ; )
            {
                Font f = new Font(fam, cuerpo, FontStyle.Bold);
                if (cuerpo <= 5f || g.MeasureString("VIDEO", f).Width <= hueco) return f;
                f.Dispose(); cuerpo -= 0.5f;
            }
    }
    static string Etiqueta(char c) { return c == '►' ? "VIDEO" : c == '★' ? "TOP" : c.ToString(); }
    static bool Chico(char c) { return c == '►' || c == '★'; }

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

    // ================== 0) LA DE VERDAD, sacada del ejecutable ==================
    static Control barraReal;
    static void PreparaReal()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type tf = asm.GetType("Rocola.Form1", true);
        Type tb = null;
        foreach (Type t in tf.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "LetterBar") tb = t;
        if (tb == null) throw new Exception("no se encontro LetterBar");
        barraReal = (Control)Activator.CreateInstance(tb, true);
        barraReal.SetBounds(0, 0, BARW, BARH);
        barraReal.Font = new Font(fam, 9f);
        tb.GetField("Letters").SetValue(barraReal, LETRAS);
        tb.GetProperty("Current").SetValue(barraReal, ACTUAL, null);
        // Current es una PROPIEDAD y al cambiarla arranca el deslizamiento de la capsula. Aqui se
        // mide la barra QUIETA, asi que se le deja terminar antes de dibujar.
        System.Threading.Thread.Sleep(230);
        tb.GetField("Available").SetValue(barraReal, disponibles);
        tb.GetField("Phase").SetValue(barraReal, 0.6d);
    }
    static void DibujaReal(Graphics g)
    {
        MethodInfo rc = barraReal.GetType().GetMethod("RenderContent", new Type[] { typeof(Graphics) });
        rc.Invoke(barraReal, new object[] { g });
    }

    // ================== 1) RELIEVE ==================
    // Las letras dejan de ser planas: llevan una sombra debajo y un filo claro arriba, como grabadas en
    // cristal. Y entre casilla y casilla, una raya finisima que ORDENA la fila sin llamar la atencion.
    static void Relieve(Graphics g)
    {
        float slot = (float)BARW / LETRAS.Length;
        using (Pen sep = new Pen(Color.FromArgb(26, 150, 175, 225)))
            for (int i = 1; i < LETRAS.Length; i++)
                g.DrawLine(sep, i * slot, 9, i * slot, BARH - 9);
        using (StringFormat sf = Cen())
        using (Font fG = new Font(fam, 10.5f, FontStyle.Bold))
        using (Font fP = FuentePal())
        using (SolidBrush b = new SolidBrush(Color.White))
        {
            for (int i = 0; i < LETRAS.Length; i++)
            {
                char c = LETRAS[i];
                bool hay = disponibles.Contains(c);
                RectangleF r = new RectangleF(i * slot, 0, slot, BARH);
                Font f = Chico(c) ? fP : fG;
                string t = Etiqueta(c);
                b.Color = Color.FromArgb(hay ? 150 : 70, 0, 0, 0);                       // sombra
                g.DrawString(t, f, b, new RectangleF(r.X, r.Y + 1.4f, r.Width, r.Height), sf);
                b.Color = hay ? Color.FromArgb(70, 190, 215, 255) : Color.FromArgb(30, 150, 170, 210);
                g.DrawString(t, f, b, new RectangleF(r.X, r.Y - 1.1f, r.Width, r.Height), sf);   // filo de arriba
                b.Color = hay ? Color.FromArgb(232, 238, 250) : Color.FromArgb(104, 114, 144);
                g.DrawString(t, f, b, r, sf);
            }
        }
        Seleccion(g, slot, false);
    }

    // ================== 2) RIEL DE VIDRIO ==================
    // La fila entera se apoya en un riel de vidrio oscuro con un brillo arriba: deja de flotar sobre el fondo
    // y se lee como una pieza de la maquina, no como texto suelto.
    static void Riel(Graphics g)
    {
        RectangleF riel = new RectangleF(0, 1, BARW, BARH - 2);
        using (GraphicsPath p = Redondo(riel, 12))
        {
            using (LinearGradientBrush lb = new LinearGradientBrush(
                       new RectangleF(riel.X, riel.Y, riel.Width, riel.Height),
                       Color.FromArgb(96, 34, 46, 88), Color.FromArgb(64, 14, 20, 46), 90f))
                g.FillPath(lb, p);
            using (Pen bo = new Pen(Color.FromArgb(58, 140, 180, 255), 1f)) g.DrawPath(bo, p);
        }
        using (GraphicsPath p2 = Redondo(new RectangleF(3, 2.5f, BARW - 6, 13), 7))
        using (LinearGradientBrush hl = new LinearGradientBrush(new RectangleF(3, 2.5f, BARW - 6, 13),
                   Color.FromArgb(34, 190, 215, 255), Color.FromArgb(0, 190, 215, 255), 90f))
            g.FillPath(hl, p2);

        float slot = (float)BARW / LETRAS.Length;
        using (StringFormat sf = Cen())
        using (Font fG = new Font(fam, 10.5f, FontStyle.Bold))
        using (Font fP = FuentePal())
        using (SolidBrush b = new SolidBrush(Color.White))
            for (int i = 0; i < LETRAS.Length; i++)
            {
                char c = LETRAS[i];
                bool hay = disponibles.Contains(c);
                RectangleF r = new RectangleF(i * slot, 0, slot, BARH);
                b.Color = Color.FromArgb(hay ? 130 : 60, 0, 0, 0);
                g.DrawString(Etiqueta(c), Chico(c) ? fP : fG, b, new RectangleF(r.X, r.Y + 1.2f, r.Width, r.Height), sf);
                b.Color = hay ? Color.FromArgb(233, 239, 251) : Color.FromArgb(100, 110, 140);
                g.DrawString(Etiqueta(c), Chico(c) ? fP : fG, b, r, sf);
            }
        Seleccion(g, slot, false);
    }

    // ================== 3) CONTADOR ==================
    // Debajo de cada letra, CUANTAS canciones hay. El cliente ve de un vistazo donde esta lo gordo y deja de
    // entrar a letras vacias. Es el unico cambio que ademas informa.
    static void Contador(Graphics g)
    {
        float slot = (float)BARW / LETRAS.Length;
        using (StringFormat sf = Cen())
        using (Font fG = new Font(fam, 10.5f, FontStyle.Bold))
        using (Font fP = FuentePal())
        using (Font fN = new Font(fam, 6.5f, FontStyle.Bold))
        using (SolidBrush b = new SolidBrush(Color.White))
            for (int i = 0; i < LETRAS.Length; i++)
            {
                char c = LETRAS[i];
                bool hay = disponibles.Contains(c);
                RectangleF r = new RectangleF(i * slot, -6, slot, BARH);
                if (c != ACTUAL)   // la elegida la pinta Seleccion, para no dibujarla dos veces
                {
                    b.Color = Color.FromArgb(hay ? 140 : 60, 0, 0, 0);
                    g.DrawString(Etiqueta(c), Chico(c) ? fP : fG, b, new RectangleF(r.X, r.Y + 1.2f, r.Width, r.Height), sf);
                    b.Color = hay ? Color.FromArgb(232, 238, 250) : Color.FromArgb(100, 110, 140);
                    g.DrawString(Etiqueta(c), Chico(c) ? fP : fG, b, r, sf);
                }
                if (hay)
                {
                    b.Color = Color.FromArgb(190, 128, 150, 200);
                    g.DrawString(cuentas[c].ToString(), fN, b, new RectangleF(i * slot, BARH - 15, slot, 12), sf);
                }
            }
        Seleccion(g, slot, false, 6f);
    }

    // ================== 4) COLOR POR GENERO ==================
    // Cada letra toma el color del genero que MANDA dentro de ella, con el mismo lenguaje de color que ya usa
    // la rocola en el resto de la pantalla. La fila deja de ser blanca y pasa a decir algo.
    static void Genero(Graphics g)
    {
        float slot = (float)BARW / LETRAS.Length;
        using (StringFormat sf = Cen())
        using (Font fG = new Font(fam, 10.5f, FontStyle.Bold))
        using (Font fP = FuentePal())
        using (SolidBrush b = new SolidBrush(Color.White))
            for (int i = 0; i < LETRAS.Length; i++)
            {
                char c = LETRAS[i];
                bool hay = disponibles.Contains(c);
                RectangleF r = new RectangleF(i * slot, 0, slot, BARH);
                Color gen = generos[c];
                b.Color = Color.FromArgb(hay ? 140 : 60, 0, 0, 0);
                g.DrawString(Etiqueta(c), Chico(c) ? fP : fG, b, new RectangleF(r.X, r.Y + 1.2f, r.Width, r.Height), sf);
                b.Color = hay ? Color.FromArgb(245, gen) : Color.FromArgb(88, gen.R, gen.G, gen.B);
                g.DrawString(Etiqueta(c), Chico(c) ? fP : fG, b, r, sf);
                if (hay)   // una pizca del color debajo, para que se lea como familia y no como letras sueltas
                    using (SolidBrush u = new SolidBrush(Color.FromArgb(70, gen)))
                        g.FillRectangle(u, i * slot + slot * 0.28f, BARH - 5, slot * 0.44f, 2);
            }
        Seleccion(g, slot, false);
    }

    // ================== 5) CAPSULA DE NEON ==================
    // La casilla elegida deja de ser una caja azul plana y pasa a ser una capsula de NEON, del mismo material
    // que la aurora del fondo: contorno encendido, halo alrededor y el centro casi vacio.
    static void Neon(Graphics g)
    {
        float slot = (float)BARW / LETRAS.Length;
        using (StringFormat sf = Cen())
        using (Font fG = new Font(fam, 10.5f, FontStyle.Bold))
        using (Font fP = FuentePal())
        using (SolidBrush b = new SolidBrush(Color.White))
            for (int i = 0; i < LETRAS.Length; i++)
            {
                char c = LETRAS[i];
                if (c == ACTUAL) continue;
                bool hay = disponibles.Contains(c);
                RectangleF r = new RectangleF(i * slot, 0, slot, BARH);
                b.Color = Color.FromArgb(hay ? 140 : 60, 0, 0, 0);
                g.DrawString(Etiqueta(c), Chico(c) ? fP : fG, b, new RectangleF(r.X, r.Y + 1.2f, r.Width, r.Height), sf);
                b.Color = hay ? Color.FromArgb(228, 235, 248) : Color.FromArgb(100, 110, 140);
                g.DrawString(Etiqueta(c), Chico(c) ? fP : fG, b, r, sf);
            }
        Seleccion(g, slot, true);
    }

    // ================== 6) COMBINADA ==================
    // Riel de vidrio + relieve + contador + capsula de neon: el conjunto, por si convence entero.
    static void Combinada(Graphics g)
    {
        RectangleF riel = new RectangleF(0, 1, BARW, BARH - 2);
        using (GraphicsPath p = Redondo(riel, 12))
        {
            using (LinearGradientBrush lb = new LinearGradientBrush(
                       new RectangleF(riel.X, riel.Y, riel.Width, riel.Height),
                       Color.FromArgb(96, 34, 46, 88), Color.FromArgb(64, 14, 20, 46), 90f))
                g.FillPath(lb, p);
            using (Pen bo = new Pen(Color.FromArgb(58, 140, 180, 255), 1f)) g.DrawPath(bo, p);
        }
        using (GraphicsPath p2 = Redondo(new RectangleF(3, 2.5f, BARW - 6, 12), 7))
        using (LinearGradientBrush hl = new LinearGradientBrush(new RectangleF(3, 2.5f, BARW - 6, 12),
                   Color.FromArgb(32, 190, 215, 255), Color.FromArgb(0, 190, 215, 255), 90f))
            g.FillPath(hl, p2);

        float slot = (float)BARW / LETRAS.Length;
        using (Pen sep = new Pen(Color.FromArgb(22, 150, 175, 225)))
            for (int i = 1; i < LETRAS.Length; i++)
                g.DrawLine(sep, i * slot, 11, i * slot, BARH - 11);
        using (StringFormat sf = Cen())
        using (Font fG = new Font(fam, 10.5f, FontStyle.Bold))
        using (Font fP = FuentePal())
        using (Font fN = new Font(fam, 6.5f, FontStyle.Bold))
        using (SolidBrush b = new SolidBrush(Color.White))
            for (int i = 0; i < LETRAS.Length; i++)
            {
                char c = LETRAS[i];
                bool hay = disponibles.Contains(c);
                RectangleF r = new RectangleF(i * slot, -5, slot, BARH);
                Font f = Chico(c) ? fP : fG;
                if (c != ACTUAL)   // la elegida la pinta Seleccion; su numero si va aqui
                {
                    b.Color = Color.FromArgb(hay ? 150 : 70, 0, 0, 0);
                    g.DrawString(Etiqueta(c), f, b, new RectangleF(r.X, r.Y + 1.3f, r.Width, r.Height), sf);
                    b.Color = hay ? Color.FromArgb(66, 190, 215, 255) : Color.FromArgb(26, 150, 170, 210);
                    g.DrawString(Etiqueta(c), f, b, new RectangleF(r.X, r.Y - 1f, r.Width, r.Height), sf);
                    b.Color = hay ? Color.FromArgb(233, 239, 251) : Color.FromArgb(100, 110, 140);
                    g.DrawString(Etiqueta(c), f, b, r, sf);
                }
                if (hay)
                {
                    b.Color = Color.FromArgb(175, 126, 148, 198);
                    g.DrawString(cuentas[c].ToString(), fN, b, new RectangleF(i * slot, BARH - 14, slot, 11), sf);
                }
            }
        Seleccion(g, slot, true, 5f);
    }

    // La casilla ELEGIDA. neon=false -> la caja azul de siempre; neon=true -> capsula encendida.
    static void Seleccion(Graphics g, float slot, bool neon) { Seleccion(g, slot, neon, 0f); }
    static void Seleccion(Graphics g, float slot, bool neon, float sube)
    {
        int i = LETRAS.IndexOf(ACTUAL);
        // Con contador debajo la caja se acorta por abajo, para dejar el numero a la vista.
        float alto = BARH - 6 - (sube > 0 ? 12 : 0);
        RectangleF cel = new RectangleF(i * slot + 3, 3, slot - 6, alto);
        RectangleF sl = new RectangleF(i * slot, cel.Y + cel.Height / 2f - BARH / 2f, slot, BARH);
        using (StringFormat sf = Cen())
        using (Font fG = new Font(fam, 10.5f, FontStyle.Bold))
        {
            if (!neon)
            {
                using (GraphicsPath p = Redondo(cel, 8))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(125, 90, 170, 255)))
                using (Pen pe = new Pen(Color.FromArgb(220, 150, 205, 255), 2f))
                { g.FillPath(b, p); g.DrawPath(pe, p); }
                using (SolidBrush w = new SolidBrush(Color.White)) g.DrawString(ACTUAL.ToString(), fG, w, sl, sf);
                return;
            }
            // NEON: halo por fuera (varias pasadas cada vez mas anchas y mas tenues), contorno encendido y
            // centro casi vacio -> el mismo material que la aurora, no una caja pintada.
            RectangleF caja = new RectangleF(cel.X + 0.5f, cel.Y + 0.5f, cel.Width - 1, cel.Height - 1);
            using (GraphicsPath p = Redondo(caja, 9))
            {
                for (int k = 6; k >= 1; k--)
                    using (Pen halo = new Pen(Color.FromArgb(12 + 5 * (6 - k), 120, 190, 255), k * 2.4f))
                    { halo.LineJoin = LineJoin.Round; g.DrawPath(halo, p); }
                using (SolidBrush dentro = new SolidBrush(Color.FromArgb(40, 70, 150, 240))) g.FillPath(dentro, p);
                using (Pen bo = new Pen(Color.FromArgb(150, 130, 200, 255), 2.6f)) { bo.LineJoin = LineJoin.Round; g.DrawPath(bo, p); }
                using (Pen nu = new Pen(Color.FromArgb(240, 225, 242, 255), 1.1f)) { nu.LineJoin = LineJoin.Round; g.DrawPath(nu, p); }
            }
            using (SolidBrush s2 = new SolidBrush(Color.FromArgb(90, 140, 200, 255)))
            {
                g.DrawString(ACTUAL.ToString(), fG, s2, new RectangleF(sl.X - 1, sl.Y, sl.Width, sl.Height), sf);
                g.DrawString(ACTUAL.ToString(), fG, s2, new RectangleF(sl.X + 1, sl.Y, sl.Width, sl.Height), sf);
            }
            using (SolidBrush w = new SolidBrush(Color.White)) g.DrawString(ACTUAL.ToString(), fG, w, sl, sf);
        }
    }

    // ---------------------------------------------------------------
    delegate void Pintor(Graphics g);
    class Opcion
    {
        public string Titulo, Nota; public Pintor P;
        public Opcion(string t, string n, Pintor p) { Titulo = t; Nota = n; P = p; }
    }

    static int Main()
    {
        fam = Fam();
        Datos();
        PreparaReal();

        Opcion[] ops = {
            new Opcion("COMO ESTA HOY", "la barra de verdad, dibujada por el propio programa. Las letras sin canciones YA salen apagadas.", delegate(Graphics g){ DibujaReal(g); }),
            new Opcion("1. RELIEVE", "las letras dejan de ser planas: sombra abajo, filo claro arriba y una raya finisima entre casillas.", Relieve),
            new Opcion("2. RIEL DE VIDRIO", "la fila se apoya en un riel de vidrio con brillo arriba: deja de flotar y se ve como una pieza de la maquina.", Riel),
            new Opcion("3. CONTADOR", "debajo de cada letra, cuantas canciones hay. Es el unico cambio que ademas INFORMA.", Contador),
            new Opcion("4. COLOR POR GENERO", "cada letra toma el color del genero que manda dentro de ella, con el lenguaje de color que ya usa la rocola.", Genero),
            new Opcion("5. CAPSULA DE NEON", "la casilla elegida pasa a ser neon encendido, del mismo material que la aurora del fondo.", Neon),
            new Opcion("6. TODO JUNTO", "riel + relieve + contador + capsula de neon.", Combinada)
        };

        // ---------- imagen 1: TAMANO REAL ----------
        int hTit = 54, bloque = 24 + BARH + 20, alto = hTit + bloque * ops.Length + 16;
        using (Bitmap bmp = new Bitmap(W, alto, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush f = new SolidBrush(Color.FromArgb(7, 9, 20))) g.FillRectangle(f, 0, 0, W, alto);
            using (Font ft = new Font(fam, 13f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 240, 252)))
                g.DrawString("BARRA DEL ABECEDARIO  -  tamano real, tal como se veria en la rocola", ft, b, 14, 14);
            using (Font fn = new Font(fam, 8.5f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 150, 180)))
                g.DrawString("pantalla de 1024 x 768   |   barra de 996 x 40   |   letra elegida: M   |   sin canciones: K Q W X Y", fn, b, 14, 34);

            int y = hTit;
            for (int i = 0; i < ops.Length; i++)
            {
                using (Font ft = new Font(fam, 9.5f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 190, 250)))
                {
                    g.DrawString(ops[i].Titulo, ft, b, PAD, y + 2);
                    float wt = g.MeasureString(ops[i].Titulo, ft).Width;
                    using (Font fn = new Font(fam, 8f))
                    using (SolidBrush b2 = new SolidBrush(Color.FromArgb(122, 132, 160)))
                        g.DrawString(ops[i].Nota, fn, b2, PAD + wt + 14, y + 4);
                }

                using (Bitmap tira = new Bitmap(BARW, BARH, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics gt = Graphics.FromImage(tira))
                    {
                        gt.SmoothingMode = SmoothingMode.AntiAlias;
                        gt.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        Fondo(gt, BARW, BARH, 7 + i);
                        ops[i].P(gt);
                    }
                    g.DrawImageUnscaled(tira, PAD, y + 22);
                }
                y += bloque;
            }
            bmp.Save("abecedario.png", ImageFormat.Png);
        }

        // ---------- imagen 2: TROZO AMPLIADO (x3) ----------
        // De la letra J a la Q, que es donde cae la elegida: asi se ve el detalle fino que a tamano real se
        // pierde en una foto.
        float slot = (float)BARW / LETRAS.Length;
        int i0 = LETRAS.IndexOf('J'), i1 = LETRAS.IndexOf('Q');
        int rx = (int)(i0 * slot), rw = (int)((i1 - i0 + 1) * slot);
        const int Z = 3;
        int bh2 = 22 + BARH * Z + 16, alto2 = 54 + bh2 * ops.Length + 12, w2 = rw * Z + PAD * 2;
        using (Bitmap bmp = new Bitmap(w2, alto2, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            using (SolidBrush f = new SolidBrush(Color.FromArgb(7, 9, 20))) g.FillRectangle(f, 0, 0, w2, alto2);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Font ft = new Font(fam, 13f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 240, 252)))
                g.DrawString("EL MISMO TROZO, AMPLIADO 3 VECES  (de la J a la Q)", ft, b, 14, 14);
            using (Font fn = new Font(fam, 8.5f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 150, 180)))
                g.DrawString("aqui se ve el detalle; en la maquina se vera del tamano de la otra imagen", fn, b, 14, 34);

            int y = 54;
            for (int i = 0; i < ops.Length; i++)
            {
                using (Font ft = new Font(fam, 10f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 190, 250)))
                    g.DrawString(ops[i].Titulo, ft, b, PAD, y);
                using (Bitmap tira = new Bitmap(BARW, BARH, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics gt = Graphics.FromImage(tira))
                    {
                        gt.SmoothingMode = SmoothingMode.AntiAlias;
                        gt.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        Fondo(gt, BARW, BARH, 7 + i);
                        ops[i].P(gt);
                    }
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(tira, new Rectangle(PAD, y + 20, rw * Z, BARH * Z),
                                new Rectangle(rx, 0, rw, BARH), GraphicsUnit.Pixel);
                }
                y += bh2;
            }
            bmp.Save("abecedario_zoom.png", ImageFormat.Png);
        }

        Console.WriteLine("listo: abecedario.png  y  abecedario_zoom.png");
        return 0;
    }
}

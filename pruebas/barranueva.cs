using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LA BARRA NUEVA, dibujada por el propio programa (no es una maqueta): se saca LetterBar del ejecutable, se
// le dan datos como los del bar -cuantas canciones hay detras de cada letra y de que genero son- y se le pide
// que se dibuje. Lo que salga aqui es lo que se vera en la rocola.
class BarraNueva
{
    const int W = 1024, PAD = 14, BARW = W - PAD * 2;
    static int BARH = 40;
    const string LETRAS = "★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static Type tb; static FontFamily fam;

    // Los mismos colores que usa la rocola, pedidos a la propia GenreToColor del ejecutable.
    static MethodInfo genColor;
    static Color Gen(string g) { return (Color)genColor.Invoke(null, new object[] { g }); }

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

    static Bitmap Tira(int ancho, char actual, HashSet<char> disp, Dictionary<char, int> cuentas, Dictionary<char, Color> tintes, int semilla)
    {
        Control bar = (Control)Activator.CreateInstance(tb, true);
        bar.SetBounds(0, 0, ancho, BARH);
        bar.Font = new Font(fam, 9f);
        tb.GetField("Letters").SetValue(bar, LETRAS);
        tb.GetProperty("Current").SetValue(bar, actual, null);
        // Current es una PROPIEDAD y al cambiarla arranca el deslizamiento de la capsula. Aqui se
        // mide la barra QUIETA, asi que se le deja terminar antes de dibujar.
        System.Threading.Thread.Sleep(230);
        tb.GetField("Available").SetValue(bar, disp);
        tb.GetField("Counts").SetValue(bar, cuentas);
        tb.GetField("Tints").SetValue(bar, tintes);
        tb.GetField("Phase").SetValue(bar, 0.6d);
        Bitmap bmp = new Bitmap(ancho, BARH, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Fondo(g, ancho, BARH, semilla);
            tb.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(bar, new object[] { g });
        }
        return bmp;
    }

    static int Main()
    {
        fam = Fam();
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        Type tf = asm.GetType("Rocola.Form1", true);
        foreach (Type t in tf.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == "LetterBar") tb = t;
        genColor = tf.GetMethod("GenreToColor", BindingFlags.Static | BindingFlags.NonPublic);

        // Una biblioteca como la de un bar: regional y cumbia mandando, algo de rock, baladas, reggaeton.
        string[] gen = { "Cumbia", "Rock", "Balada romantica", "Reggaeton", "Banda", "Pop", "Salsa", "Metal",
                         "Norteno", "Bachata", "Electronica", "Corrido" };
        int[] n = { 0, 12, 41, 33, 52, 28, 19, 24, 31, 17, 22, 9, 0, 47, 38, 21, 26, 14, 0, 44, 39, 29, 16, 11, 0, 0, 0, 8 };
        HashSet<char> disp = new HashSet<char>();
        Dictionary<char, int> cuentas = new Dictionary<char, int>();
        Dictionary<char, Color> tintes = new Dictionary<char, Color>();
        int k = 0, gi = 0;
        foreach (char c in LETRAS)
        {
            if (c == '★') { disp.Add(c); cuentas[c] = 25; continue; }
            if (c == '►') { disp.Add(c); cuentas[c] = 37; continue; }
            int v = (k < n.Length) ? n[k] : 10; k++;
            if ("KQWXY".IndexOf(c) >= 0) v = 0;
            if (v > 0)
            {
                disp.Add(c); cuentas[c] = v;
                tintes[c] = Gen(gen[gi % gen.Length]); gi++;
            }
        }

        string[] tit = {
            "PANTALLA ENTERA, letra elegida M",
            "PANTALLA ENTERA, elegida la ESTRELLA (lo mas sonado)",
            "CON UN VIDEO PUESTO AL LADO (la barra se queda a la mitad)"
        };
        int[] anchos = { BARW, BARW, 486 };
        char[] act = { 'M', '★', 'M' };

        int alto = 58 + tit.Length * (24 + BARH + 20) + 10;
        using (Bitmap bmp = new Bitmap(W, alto, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            using (SolidBrush f = new SolidBrush(Color.FromArgb(7, 9, 20))) g.FillRectangle(f, 0, 0, W, alto);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Font ft = new Font(fam, 13f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 240, 252)))
                g.DrawString("LA BARRA NUEVA  -  tamano real, dibujada por el propio programa", ft, b, 14, 14);
            using (Font fn = new Font(fam, 8.5f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 150, 180)))
                g.DrawString("riel de vidrio + relieve + contador + color por genero + capsula de neon + estrellita dorada", fn, b, 14, 34);
            int y = 58;
            for (int i = 0; i < tit.Length; i++)
            {
                using (Font ft = new Font(fam, 9.5f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 190, 250)))
                    g.DrawString(tit[i], ft, b, PAD, y + 2);
                using (Bitmap t2 = Tira(anchos[i], act[i], disp, cuentas, tintes, 7 + i))
                    g.DrawImageUnscaled(t2, PAD, y + 22);
                y += 24 + BARH + 20;
            }
            bmp.Save("barra_nueva.png", ImageFormat.Png);
        }

        // El mismo trozo, ampliado 3 veces
        float slot = (float)BARW / LETRAS.Length;
        int i0 = 0, i1 = LETRAS.IndexOf('H');
        int rx = (int)(i0 * slot), rw = (int)((i1 - i0 + 1) * slot);
        const int Z = 3;
        int w2 = rw * Z + PAD * 2, h2 = 58 + 2 * (22 + BARH * Z + 18);
        using (Bitmap bmp = new Bitmap(w2, h2, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            using (SolidBrush f = new SolidBrush(Color.FromArgb(7, 9, 20))) g.FillRectangle(f, 0, 0, w2, h2);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Font ft = new Font(fam, 13f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 240, 252)))
                g.DrawString("EL MISMO TROZO, AMPLIADO 3 VECES", ft, b, 14, 14);
            using (Font fn = new Font(fam, 8.5f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(140, 150, 180)))
                g.DrawString("de la estrella a la H; en la maquina se vera del tamano de la otra imagen", fn, b, 14, 34);
            // Ojo con la primera: NO es "como estaba antes", es como se ve mientras el paseo de fondo todavia
            // no ha leido los generos -riel y relieve si, color y numeros aun no-. Es un estado real.
            string[] t3 = { "RECIEN ESCANEADA (aun leyendo los generos)", "YA CON SUS DATOS" };
            int y = 58;
            for (int i = 0; i < 2; i++)
            {
                using (Font ft = new Font(fam, 10f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 190, 250)))
                    g.DrawString(t3[i], ft, b, PAD, y);
                Bitmap t2 = (i == 0) ? Tira(BARW, 'M', disp, null, null, 7) : Tira(BARW, 'M', disp, cuentas, tintes, 7);
                using (t2)
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(t2, new Rectangle(PAD, y + 20, rw * Z, BARH * Z), new Rectangle(rx, 0, rw, BARH), GraphicsUnit.Pixel);
                }
                y += 22 + BARH * Z + 18;
            }
            bmp.Save("barra_nueva_zoom.png", ImageFormat.Png);
        }
        Console.WriteLine("listo: barra_nueva.png  y  barra_nueva_zoom.png");
        return 0;
    }
}

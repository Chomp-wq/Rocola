using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

// COMO SE VEN LAS DOS NEBULOSAS. Un cuaderno de bocetos, no una bateria.
//
// Dibuja el cielo de la rocola tal y como queda ahora -las tres paradas del degradado, mas la capa de
// nebulosas por duplicado: una del color del GENERO y otra del de la CARATULA, deslizandose- en varios
// puntos del ciclo, para poder mirarlo antes de instalarlo en el bar.
//
// Usa las MISMAS funciones de la rocola: el dibujo de las nebulosas, los colores del cielo y los dos
// colores de la mezcla. Lo unico de aqui es el marco.
//
//    mono nieblas.exe             deja los PNG en pruebas/nieblas/
class Nieblas
{
    const int W = 900, H = 560;
    const BindingFlags TODO = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type TF, TGpu;
    static MethodInfo mEtiqueta, mColores, mNebBake, mSky, mSkyMid, mSky2;

    static MethodInfo Met(Type t, string n)
    {
        foreach (MethodInfo m in t.GetMethods(TODO | BindingFlags.DeclaredOnly)) if (m.Name == n) return m;
        return null;
    }
    static Color Etiqueta(string g) { return (Color)mEtiqueta.Invoke(null, new object[] { g }); }
    static void Dos(Color gen, Color p1, Color p2, float fase, out Color a, out Color b)
    {
        object[] arg = { gen, p1, p2, fase, null, null };
        mColores.Invoke(null, arg);
        a = (Color)arg[4]; b = (Color)arg[5];
    }

    // El cielo, igual que en la rocola: tres paradas, la de en medio al 42%.
    static Bitmap Cielo(Color acento)
    {
        Color arriba = (Color)mSky.Invoke(null, new object[] { acento });
        Color medio = (Color)mSkyMid.Invoke(null, new object[] { acento });
        Color abajo = (Color)mSky2.Invoke(null, new object[] { acento });
        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 0, W, H + 1), arriba, abajo, LinearGradientMode.Vertical))
        {
            ColorBlend cb = new ColorBlend(3);
            cb.Colors = new Color[] { arriba, medio, abajo };
            cb.Positions = new float[] { 0f, 0.42f, 1f };
            lgb.InterpolationColors = cb;
            g.FillRectangle(lgb, 0, 0, W, H);
        }
        return b;
    }

    // Una pasada de la nebulosa, corrida de su sitio y con su color: lo mismo que hace NieblaDeriva en la
    // tarjeta grafica, pero con pinceles en vez de con triangulos.
    static void Pasada(Graphics g, Bitmap mask, Color c, float alfa, float mx, float my, float dx, float dy)
    {
        using (ImageAttributes ia = new ImageAttributes())
        {
            ColorMatrix cm = new ColorMatrix();
            cm.Matrix00 = c.R / 255f; cm.Matrix11 = c.G / 255f; cm.Matrix22 = c.B / 255f; cm.Matrix33 = alfa;
            ia.SetColorMatrix(cm);
            Rectangle r = new Rectangle((int)(-mx + dx), (int)(-my + dy), (int)(W + 2 * mx), (int)(H + 2 * my));
            g.DrawImage(mask, r, 0, 0, mask.Width, mask.Height, GraphicsUnit.Pixel, ia);
        }
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "Form1") TF = t;
            if (t.Name == "GpuStarfield") TGpu = t;
        }
        if (TF == null || TGpu == null) { Console.WriteLine("  no se encontro Form1 o GpuStarfield"); return 1; }
        mEtiqueta = Met(TF, "ColorDeEtiqueta");
        mColores = Met(TF, "ColoresNebulosa");
        mSky = Met(TF, "SkyColor"); mSkyMid = Met(TF, "SkyColorMid"); mSky2 = Met(TF, "SkyColor2");
        mNebBake = Met(TGpu, "BakeNebulosas");
        if (mColores == null || mNebBake == null) { Console.WriteLine("  falta ColoresNebulosa o BakeNebulosas"); return 1; }
        // Los mismos numeros que usa el dibujo de verdad, leidos de el (no copiados aqui).
        Type TPanel = null;
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuPanel") TPanel = t;
        float alfa = 1f, deriva = 0.06f;
        if (TPanel != null)
        {
            FieldInfo fa = TPanel.GetField("NebAlfa", TODO), fd = TPanel.GetField("NebDeriva", TODO);
            if (fa != null) alfa = Convert.ToSingle(fa.GetValue(null));
            if (fd != null) deriva = Convert.ToSingle(fd.GetValue(null));
        }

        System.IO.Directory.CreateDirectory("nieblas");
        Bitmap mask = (Bitmap)mNebBake.Invoke(null, new object[] { W / 4, H / 4 });

        // ---------- 1) COMO QUEDA, CON DISTINTOS DISCOS ----------
        // Esto es EXACTAMENTE lo que se va a ver: el dibujo ya viene horneado a la fuerza elegida y las dos
        // capas se pintan a tinta entera, igual que en la rocola.
        Bitmap g4 = new Bitmap(W * 2 + 12, H * 2 + 12, PixelFormat.Format32bppArgb);
        string[] nom = { "Rock, sin portada", "Rock con portada azul", "Electronica con portada naranja", "Regional, portada roja y cian" };
        Color[] gen = { Etiqueta("Rock"), Etiqueta("Rock"), Etiqueta("Electronica"), Etiqueta("Regional") };
        Color[] p1 = { SinColor, Color.FromArgb(255, 40, 90, 220), Color.FromArgb(255, 240, 140, 30), Color.FromArgb(255, 230, 40, 40) };
        Color[] p2 = { SinColor, SinColor, SinColor, Color.FromArgb(255, 40, 210, 220) };
        using (Graphics tg = Graphics.FromImage(g4))
        {
            tg.Clear(Color.FromArgb(255, 16, 16, 18));
            for (int c = 0; c < 4; c++)
                using (Bitmap uno = Escena(mask, gen[c], p1[c], p2[c], 1.2f, alfa, deriva, nom[c]))
                    tg.DrawImage(uno, (c % 2) * (W + 12), (c / 2) * (H + 12));
        }
        g4.Save("nieblas/1-distintos-discos.png", ImageFormat.Png);
        g4.Dispose();

        // ---------- 2) COMO SE MUEVE ----------
        float[] cuatro = { 0f, 1.57f, 3.14f, 4.71f };
        string[] momento = { "principio", "un cuarto", "la mitad", "tres cuartos" };
        Rejilla("nieblas/2-como-se-mueve.png", mask, Etiqueta("Regional"),
                Color.FromArgb(255, 230, 40, 40), Color.FromArgb(255, 40, 210, 220),
                cuatro, momento, alfa, deriva, false);

        Console.WriteLine("  nieblas/1-distintos-discos.png  cuatro discos distintos");
        Console.WriteLine("  nieblas/2-como-se-mueve.png     cuatro momentos de la vuelta");
        mask.Dispose();
        return 0;
    }

    static readonly Color SinColor = Color.FromArgb(0, 0, 0, 0);

    // Una pantalla: el cielo mas las dos nebulosas.
    static Bitmap Escena(Bitmap mask, Color gen, Color p1, Color p2, float fase, float fuerza, float deriva, string pie)
    {
        Bitmap cielo = Cielo(gen);
        using (Graphics g = Graphics.FromImage(cielo))
        {
            Color a, b;
            Dos(gen, p1, p2, fase, out a, out b);
            float mx = W * deriva, my = H * deriva;
            float co = (float)Math.Cos(fase), sn = (float)Math.Sin(fase);
            Pasada(g, mask, a, fuerza, mx, my, co * mx, sn * my);
            Pasada(g, mask, b, fuerza, mx, my, -co * mx, -sn * my);
            using (Font f = new Font("DejaVu Sans", 13, FontStyle.Bold))
                g.DrawString(pie, f, Brushes.White, 14, 12);
        }
        return cielo;
    }

    // Cuatro pantallas en rejilla, variando la fuerza o el momento.
    static void Rejilla(string archivo, Bitmap mask, Color gen, Color p1, Color p2,
                        float[] valores, string[] nombres, float fijo, float deriva, bool variaFuerza)
    {
        Bitmap g4 = new Bitmap(W * 2 + 12, H * 2 + 12, PixelFormat.Format32bppArgb);
        using (Graphics tg = Graphics.FromImage(g4))
        {
            tg.Clear(Color.FromArgb(255, 16, 16, 18));
            for (int k = 0; k < 4; k++)
            {
                float fase = variaFuerza ? fijo : valores[k];
                float fuerza = variaFuerza ? valores[k] : fijo;
                using (Bitmap uno = Escena(mask, gen, p1, p2, fase, fuerza, deriva, nombres[k]))
                    tg.DrawImage(uno, (k % 2) * (W + 12), (k / 2) * (H + 12));
            }
        }
        g4.Save(archivo, ImageFormat.Png);
        g4.Dispose();
    }
}

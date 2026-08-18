using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// Dibuja el fondo reactivo TAL CUAL lo pinta la tarjeta, para poder verlo antes de probarlo en la rocola.
// La malla son columnas: entre punto y punto la tarjeta interpola en linea recta, y eso es justo lo que se
// hace aqui pixel a pixel.
class FondoImg
{
    static Type TF, TP;
    static MethodInfo PERF, TONO, SKY, SKYM, SKY2, GIRA, REAC, GPUNTA;
    static float Cte(string n)
    {
        FieldInfo f = TP.GetField(n, BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        object v = f.GetRawConstantValue();
        return (v is int) ? (int)v : (float)v;
    }
    static float Perfil(float[] B, float fx) { return (float)PERF.Invoke(null, new object[]{ B, fx }); }
    static MethodInfo BAJA, SOMBRA, RAYO, STONO, SALFA, RAMPA, TMEZ, LERP;
    static uint TMez(uint a, uint b, float f) { return (uint)TMEZ.Invoke(null, new object[]{ a, b, f }); }
    static uint Lerp(uint a, uint b, float f) { return (uint)LERP.Invoke(null, new object[]{ a, b, f }); }
    static uint Pack(Color c) { return ((uint)c.R << 16) | ((uint)c.G << 8) | (uint)c.B; }
    static float Rampa(float f) { return (float)RAMPA.Invoke(null, new object[]{ f }); }
    static float SueloTono(float f) { return (float)STONO.Invoke(null, new object[]{ f }); }
    static float SueloAlfa(float f) { return (float)SALFA.Invoke(null, new object[]{ f }); }
    static float Rayo(float fx, float t) { return (float)RAYO.Invoke(null, new object[]{ fx, t }); }
    static float Baja(float f) { return (float)BAJA.Invoke(null, new object[]{ f }); }
    static void Tono(ref float r, ref float g, ref float b, float v)
    { object[] a = { r, g, b, v }; TONO.Invoke(null, a); r=(float)a[0]; g=(float)a[1]; b=(float)a[2]; }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuPanel") TP = t;
        PERF = TP.GetMethod("AurPerfil", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        TONO = TP.GetMethod("TonoPorNivel", BindingFlags.Static|BindingFlags.NonPublic);
        BAJA = TP.GetMethod("AurBaja", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        SOMBRA = TF.GetMethod("DrawLabelOverlay", BindingFlags.Static|BindingFlags.NonPublic, null, new Type[]{ typeof(Graphics), typeof(Label) }, null);
        RAYO = TP.GetMethod("AurRayo", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        RAMPA = TP.GetMethod("AurRampa", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        STONO = TP.GetMethod("AurSueloTono", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        SALFA = TP.GetMethod("AurSueloAlfa", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        BindingFlags SF = BindingFlags.Static|BindingFlags.NonPublic;
        SKY = TF.GetMethod("SkyColor", SF); SKYM = TF.GetMethod("SkyColorMid", SF); SKY2 = TF.GetMethod("SkyColor2", SF);
        REAC = TF.GetMethod("ColorReactivo", SF);
        GIRA = TF.GetMethod("GiraTono", SF);
        GPUNTA = TF.GetMethod("GiroPunta", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        TMEZ = TP.GetMethod("AurTonoMezcla", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        LERP = TP.GetMethod("AurLerpRgb", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);

        int cols = (int)Cte("AurCols"), filas = (int)Cte("AurFilas");
        float piso = Cte("AurPiso"), aBase = Cte("AurAlfaBase"), aPico = Cte("AurAlfaPico");
        int suelos = (int)Cte("AurSuelo");

        Color[] acentos = { Color.FromArgb(150,60,190), Color.FromArgb(230,140,40), Color.FromArgb(60,110,225) };
        string[] nombres = { "morado", "naranja", "azul" };
        // tres momentos de una cancion: floja, normal y pegando fuerte
        // ASI SUENA DE VERDAD: con musica normal las 8 bandas quedan casi iguales (el SpecTilt las empareja
        // a proposito). Ese es el caso que hay que mirar, no un espectro de laboratorio muy desigual.
        float[][] momentos = {
            new float[]{ 0.55f,0.52f,0.58f,0.54f,0.56f,0.50f,0.53f,0.49f },
            new float[]{ 0.72f,0.68f,0.74f,0.70f,0.71f,0.66f,0.69f,0.64f },
            new float[]{ 0.95f,0.88f,0.92f,0.86f,0.90f,0.84f,0.87f,0.82f },
        };
        DIM = 0.80f + 0.20f * 0.45f;              // como se ve MIENTRAS SE NAVEGA (que es cuando mas se mira)
        int W = 1400, H = 585;                    // proporcion de pantalla ancha, como la rocola real
        // el horizonte cae justo encima de la linea de "Reproduciendo", igual que en el programa (BotStripTop)
        int baseY = H - 14 - 16 - 26 - 26 - 4;
        float alto = H * 1.02f * 0.55f;           // el mismo alto que usa el programa (espectro=55)

        for (int c = 0; c < acentos.Length; c++)
        {
            using (Bitmap bmp = new Bitmap(W, H * momentos.Length, PixelFormat.Format32bppArgb))
            {
                for (int m = 0; m < momentos.Length; m++)
                {
                    Pinta(bmp, m*H, W, H, baseY, alto, acentos[c], momentos[m], cols, filas, piso, aBase, aPico, suelos, m*3.1f);
                    Franja(bmp, m*H, W, H, baseY);
                }
                bmp.Save("fondo_" + nombres[c] + ".png", ImageFormat.Png);
            }
            Console.WriteLine("fondo_" + nombres[c] + ".png");
        }
        return 0;
    }

    static float DIM = 1f;
    static Color Inv(MethodInfo m, Color a) { return (Color)m.Invoke(null, new object[]{ a }); }
    static Color Gira(Color a, float g, float b) { return (Color)GIRA.Invoke(null, new object[]{ a, g, b }); }

    static void Pinta(Bitmap bmp, int y0, int W, int H, int baseY, float alto, Color acento, float[] B,
                      int cols, int filas, float piso, float aBase, float aPico, int suelos, float t)
    {
        Color cTop = Inv(SKY, acento), cMid = Inv(SKYM, acento), cBot = Inv(SKY2, acento);
        // cielo (mismo degradado de 3 paradas que la rocola)
        for (int y = 0; y < H; y++)
        {
            float f = y / (float)(H-1);
            Color a = (f < 0.5f) ? Mezcla(cTop, cMid, f/0.5f) : Mezcla(cMid, cBot, (f-0.5f)/0.5f);
            for (int x = 0; x < W; x++) bmp.SetPixel(x, y0+y, a);
        }
        Color vivo = Inv(REAC, acento);
        Color g1c = Gira(vivo, -10f, 1.00f), g2c = Gira(vivo, 22f, 0.98f);
        float gp = (float)GPUNTA.Invoke(null, new object[]{ vivo });
        Color g3c = Gira(vivo, gp-12f, 0.98f), g4c = Gira(vivo, gp+18f, 0.96f);
        int puntos = cols + 1;
        float[] hh = new float[puntos], aa = new float[puntos], rz = new float[puntos];
        float[,] rc = new float[filas+1,puntos], gc = new float[filas+1,puntos], bc = new float[filas+1,puntos];
        float[,] rd = new float[suelos+1,puntos], gd = new float[suelos+1,puntos], bd = new float[suelos+1,puntos];
        uint[] rampPie = new uint[filas+1], rampAgu = new uint[filas+1];
        for (int k = 0; k <= filas; k++)
        {
            float f = (float)k/filas;
            rampPie[k] = TMez(Pack(g1c), Pack(g3c), f);
            rampAgu[k] = TMez(Pack(g2c), Pack(g4c), f);
        }
        for (int i = 0; i < puntos; i++)
        {
            float fx = (float)i / cols;
            float v = Perfil(B, fx); if (v > 1f) v = 1f;
            float rayo = Rayo(fx, t); rz[i] = rayo;
            hh[i] = alto * (piso + (1f-piso)*v) * rayo;
            for (int k = 0; k <= filas; k++)
            {
                uint c = Lerp(rampPie[k], rampAgu[k], fx);
                float r = (c>>16)&0xFF, g = (c>>8)&0xFF, b = c&0xFF;
                Tono(ref r, ref g, ref b, v);
                rc[k,i]=r; gc[k,i]=g; bc[k,i]=b;
            }
            uint cp = Lerp(Pack(g1c), Pack(g2c), fx);
            float r0 = (cp>>16)&0xFF, g0 = (cp>>8)&0xFF, b0 = cp&0xFF;
            for (int k = 0; k <= suelos; k++)
            {
                float r3 = r0, g3 = g0, b3 = b0;
                Tono(ref r3, ref g3, ref b3, v*SueloTono((float)k/suelos));
                rd[k,i]=r3; gd[k,i]=g3; bd[k,i]=b3;
            }
            float al = aBase + aPico*v; if (al > 1f) al = 1f;
            aa[i] = al;
        }
        float paso = (float)W / cols;
        for (int x = 0; x < W; x++)
        {
            float xt = x / paso; int i = (int)xt; if (i > cols-1) i = cols-1;
            float f = xt - i;
            float h = hh[i] + (hh[i+1]-hh[i])*f;
            float al = (aa[i] + (aa[i+1]-aa[i])*f) * DIM;
            float ry = rz[i] + (rz[i+1]-rz[i])*f;
            if (h < 1f) continue;
            int yIni = (int)(baseY - h); if (yIni < 0) yIni = 0;
            for (int y = yIni; y < H; y++)
            {
                float r, g, b, a;
                if (y >= baseY)   // SUELO: el color se hunde de tono y de presencia hasta quedarse en el cielo
                {
                    float fs = (H > baseY) ? (y - baseY) / (float)(H - baseY) : 0f;
                    float xk = fs * suelos; int k = Math.Min((int)xk, suelos-1); float u = xk - k;
                    float rk0 = rd[k,i] + (rd[k,i+1]-rd[k,i])*f, rk1 = rd[k+1,i] + (rd[k+1,i+1]-rd[k+1,i])*f;   // el suelo
                    float gk0 = gd[k,i] + (gd[k,i+1]-gd[k,i])*f, gk1 = gd[k+1,i] + (gd[k+1,i+1]-gd[k+1,i])*f;
                    float bk0 = bd[k,i] + (bd[k,i+1]-bd[k,i])*f, bk1 = bd[k+1,i] + (bd[k+1,i+1]-bd[k+1,i])*f;
                    r = rk0 + (rk1-rk0)*u; g = gk0 + (gk1-gk0)*u; b = bk0 + (bk1-bk0)*u;
                    a = al * (SueloAlfa((float)k/suelos)*(1f-u) + SueloAlfa((float)(k+1)/suelos)*u);
                }
                else
                {
                    float fv = (baseY - y) / h; if (fv > 1f) continue;
                    float ff = fv * filas; int j = (int)ff; if (j > filas-1) j = filas-1;
                    float u = ff - j;
                    float mm = Baja((float)j/filas)*(1f-u) + Baja((float)(j+1)/filas)*u;
                    a = al * mm * (1f + (ry-1f)*Rampa(fv));
                    float xk2 = fv * filas; int k2 = Math.Min((int)xk2, filas-1); float u2 = xk2 - k2;
                    float ca0 = rc[k2,i] + (rc[k2,i+1]-rc[k2,i])*f, ca1 = rc[k2+1,i] + (rc[k2+1,i+1]-rc[k2+1,i])*f;
                    float cb0 = gc[k2,i] + (gc[k2,i+1]-gc[k2,i])*f, cb1 = gc[k2+1,i] + (gc[k2+1,i+1]-gc[k2+1,i])*f;
                    float cc0 = bc[k2,i] + (bc[k2,i+1]-bc[k2,i])*f, cc1 = bc[k2+1,i] + (bc[k2+1,i+1]-bc[k2+1,i])*f;
                    r = ca0 + (ca1-ca0)*u2; g = cb0 + (cb1-cb0)*u2; b = cc0 + (cc1-cc0)*u2;
                }
                Color s = bmp.GetPixel(x, y0+y);
                bmp.SetPixel(x, y0+y, Color.FromArgb(255,
                    (int)(s.R + (r - s.R)*a), (int)(s.G + (g - s.G)*a), (int)(s.B + (b - s.B)*a)));
            }
        }
    }
    // La franja de estado REAL encima, con su contorno oscuro: para ver si el texto se sigue leyendo.
    static Label L(string t, Font f, Color c, int x, int y, int w, int h, ContentAlignment a)
    {
        Label l = new Label();
        l.Text = t; l.Font = f; l.ForeColor = c; l.UseMnemonic = false;
        l.SetBounds(x, y, w, h); l.TextAlign = a; l.AutoEllipsis = true;
        return l;
    }
    static void Franja(Bitmap bmp, int y0, int W, int H, int baseY)
    {
        Font fNow = new Font("DejaVu Sans", 13f, FontStyle.Bold);
        Font fNext = new Font("DejaVu Sans", 11f, FontStyle.Bold);
        Font fMej = new Font("DejaVu Sans", 10f, FontStyle.Bold);
        Font fDer = new Font("DejaVu Sans", 12f, FontStyle.Bold);
        int pad = 14, progH = 16;
        int progY = H - pad - progH, nextY = progY - 26, nowY = nextY - 26;
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.TranslateTransform(0, y0);
            SOMBRA.Invoke(null, new object[]{ g, L("Reproduciendo:   Depeche Mode - I Feel You", fNow, Color.FromArgb(125,235,165), pad, nowY, 700, 26, ContentAlignment.TopLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("MEJORADA", fMej, Color.FromArgb(255,205,90), pad+430, nowY+3, 110, 20, ContentAlignment.MiddleLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("Siguiente:   -", fNext, Color.FromArgb(150,200,255), pad, nextY, 700, 22, ContentAlignment.TopLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("Creditos:  7", fDer, Color.FromArgb(120,240,140), W-260, nowY, 240, 24, ContentAlignment.TopLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("En cola:  0", fDer, Color.FromArgb(150,200,255), W-260, nextY, 240, 24, ContentAlignment.TopLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("0:46 / 4:36", fNext, Color.White, W-420, progY-4, 130, 22, ContentAlignment.TopRight) });
            using (SolidBrush b1 = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
            using (SolidBrush b2 = new SolidBrush(Color.FromArgb(210, 190, 120, 255)))
            {
                g.FillRectangle(b1, pad, progY, W-2*pad-300, progH/2);
                g.FillRectangle(b2, pad, progY, (int)((W-2*pad-300)*0.17f), progH/2);
            }
            g.ResetTransform();
        }
    }

    static Color Mezcla(Color a, Color b, float f)
    { return Color.FromArgb((int)(a.R+(b.R-a.R)*f), (int)(a.G+(b.G-a.G)*f), (int)(a.B+(b.B-a.B)*f)); }
}

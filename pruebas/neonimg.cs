using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// Vista previa del fondo DIBUJANDO LA MALLA DE VERDAD. En vez de repintar la aurora a mano (que seria otra
// version del mismo dibujo, y podria mentir), aqui se le piden al propio ejecutable las dos mallas -el
// relleno y el brillo de neon- y se rasterizan triangulo a triangulo con las MISMAS dos mezclas que usa la
// tarjeta: normal para el relleno y SUMANDO luz para el neon. Lo que sale aqui es lo que se vera en pantalla.
class NeonImg
{
    static Type TF, TP, TVtx;
    static FieldInfo fX, fY, fCol;
    static MethodInfo PERF, TONO, SKY, SKYM, SKY2, GIRA, REAC, GPUNTA, RAYO, TMEZ, LERP, MALLA, BRILLO, SOMBRA;

    static float Cte(string n)
    {
        FieldInfo f = TP.GetField(n, BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        object v = f.GetRawConstantValue();
        return (v is int) ? (int)v : (float)v;
    }
    static float Perfil(float[] B, float fx) { return (float)PERF.Invoke(null, new object[]{ B, fx }); }
    static float Rayo(float fx, float t) { return (float)RAYO.Invoke(null, new object[]{ fx, t }); }
    static MethodInfo ONDA;
    static float Onda(float fx, float t) { return (float)ONDA.Invoke(null, new object[]{ fx, t }); }
    static MethodInfo SUAV;
    static void Suaviza(float[] h) { float[] d = new float[h.Length]; SUAV.Invoke(null, new object[]{ h, d }); Array.Copy(d, h, h.Length); }
    static uint TMez(uint a, uint b, float f) { return (uint)TMEZ.Invoke(null, new object[]{ a, b, f }); }
    static uint Lerp(uint a, uint b, float f) { return (uint)LERP.Invoke(null, new object[]{ a, b, f }); }
    static uint Pack(Color c) { return ((uint)c.R << 16) | ((uint)c.G << 8) | (uint)c.B; }
    static Color Inv(MethodInfo m, Color a) { return (Color)m.Invoke(null, new object[]{ a }); }
    static Color Gira(Color a, float g, float b) { return (Color)GIRA.Invoke(null, new object[]{ a, g, b }); }
    static void Tono(ref float r, ref float g, ref float b, float v)
    { object[] a = { r, g, b, v }; TONO.Invoke(null, a); r=(float)a[0]; g=(float)a[1]; b=(float)a[2]; }

    // ---------------- el lienzo, en float para que el sumado no se recorte antes de tiempo ----------------
    static int W, H;
    static float[] px, py2, pz;   // r, g, b  (0..255, puede pasarse de 255 mientras se suma)

    static void Pon(int x, int y, float r, float g, float b)
    { int k = y*W+x; px[k]=r; py2[k]=g; pz[k]=b; }

    // Rasteriza una lista de triangulos (3 vertices seguidos) interpolando color y opacidad como hace la
    // tarjeta.  suma=false -> mezcla normal (tapa);  suma=true -> suma luz (el neon).
    static void Raster(Array vs, int nv, bool suma)
    {
        for (int t = 0; t + 2 < nv; t += 3)
        {
            float[] X = new float[3], Y = new float[3], R = new float[3], G = new float[3], B = new float[3], A = new float[3];
            for (int k = 0; k < 3; k++)
            {
                object v = vs.GetValue(t+k);
                X[k] = (float)fX.GetValue(v) + 0.5f;
                Y[k] = (float)fY.GetValue(v) + 0.5f;
                uint c = (uint)fCol.GetValue(v);
                A[k] = ((c>>24)&0xFF)/255f; R[k] = (c>>16)&0xFF; G[k] = (c>>8)&0xFF; B[k] = c&0xFF;
            }
            float x0 = Math.Min(X[0], Math.Min(X[1], X[2])), x1 = Math.Max(X[0], Math.Max(X[1], X[2]));
            float y0 = Math.Min(Y[0], Math.Min(Y[1], Y[2])), y1 = Math.Max(Y[0], Math.Max(Y[1], Y[2]));
            int ix0 = Math.Max(0, (int)Math.Floor(x0)), ix1 = Math.Min(W-1, (int)Math.Ceiling(x1));
            int iy0 = Math.Max(0, (int)Math.Floor(y0)), iy1 = Math.Min(H-1, (int)Math.Ceiling(y1));
            float den = (Y[1]-Y[2])*(X[0]-X[2]) + (X[2]-X[1])*(Y[0]-Y[2]);
            if (Math.Abs(den) < 1e-9f) continue;
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    // El punto de muestra va corrido un pelo a proposito: si cayera JUSTO en el borde que
                    // comparten dos triangulos, los dos lo darian por suyo y al sumar luz saldrian rayas
                    // verticales falsas donde la columna cae en un pixel exacto. La tarjeta no las tiene
                    // (D3D9 aplica su regla de borde), asi que la vista previa tampoco debe inventarlas.
                    float cx = x + 0.5003f, cy = y + 0.5007f;
                    float l0 = ((Y[1]-Y[2])*(cx-X[2]) + (X[2]-X[1])*(cy-Y[2])) / den;
                    float l1 = ((Y[2]-Y[0])*(cx-X[2]) + (X[0]-X[2])*(cy-Y[2])) / den;
                    float l2 = 1f - l0 - l1;
                    if (l0 < 0f || l1 < 0f || l2 < 0f) continue;
                    float a = l0*A[0] + l1*A[1] + l2*A[2];
                    if (a <= 0.0005f) continue;
                    float r = l0*R[0] + l1*R[1] + l2*R[2];
                    float g = l0*G[0] + l1*G[1] + l2*G[2];
                    float b = l0*B[0] + l1*B[1] + l2*B[2];
                    int k = y*W+x;
                    if (suma) { px[k] += r*a; py2[k] += g*a; pz[k] += b*a; }
                    else { px[k] += (r-px[k])*a; py2[k] += (g-py2[k])*a; pz[k] += (b-pz[k])*a; }
                }
        }
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in asm.GetTypes()) { if (t.Name == "GpuPanel") TP = t; if (t.Name == "Vtx") TVtx = t; }
        fX = TVtx.GetField("X"); fY = TVtx.GetField("Y"); fCol = TVtx.GetField("Color");
        BindingFlags SP = BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        PERF = TP.GetMethod("AurPerfil", SP); TONO = TP.GetMethod("TonoPorNivel", SP);
        RAYO = TP.GetMethod("AurRayo", SP); ONDA = TP.GetMethod("AurOndaAlto", SP); SUAV = TP.GetMethod("AurSuaviza", SP); TMEZ = TP.GetMethod("AurTonoMezcla", SP); LERP = TP.GetMethod("AurLerpRgb", SP);
        MALLA = TP.GetMethod("AurMalla", SP); BRILLO = TP.GetMethod("AurBrillo", SP);
        BindingFlags SF = BindingFlags.Static|BindingFlags.NonPublic;
        SKY = TF.GetMethod("SkyColor", SF); SKYM = TF.GetMethod("SkyColorMid", SF); SKY2 = TF.GetMethod("SkyColor2", SF);
        REAC = TF.GetMethod("ColorReactivo", SF); GIRA = TF.GetMethod("GiraTono", SF);
        GPUNTA = TF.GetMethod("GiroPunta", SP);
        SOMBRA = TF.GetMethod("DrawLabelOverlay", SF, null, new Type[]{ typeof(Graphics), typeof(Label) }, null);
        if (BRILLO == null) { Console.WriteLine("falta AurBrillo"); return 1; }

        int cols = (int)Cte("AurCols"), filas = (int)Cte("AurFilas"), suelos = (int)Cte("AurSuelo");
        float piso = Cte("AurPiso"), aBase = Cte("AurAlfaBase"), aPico = Cte("AurAlfaPico");
        float nBase = Cte("AurNeonBase"), nPico = Cte("AurNeonPico");
        float lBase = Cte("AurLineaBase"), lPico = Cte("AurLineaPico");
        int brilloVtx = (int)Cte("AurBrilloVtx");

        Color[] acentos = { Color.FromArgb(150,60,190), Color.FromArgb(230,140,40), Color.FromArgb(60,110,225) };
        string[] nombres = { "morado", "naranja", "azul" };
        // El primero es el REPOSO: sin musica ninguna. Ahi la aurora no se apaga, se queda baja y tenue con
        // su ondeo lento (ver AurReposo en Form1).
        float[][] momentos = {
            new float[]{ 0f,0f,0f,0f,0f,0f,0f,0f },
            new float[]{ 0.55f,0.52f,0.58f,0.54f,0.56f,0.50f,0.53f,0.49f },
            new float[]{ 0.72f,0.68f,0.74f,0.70f,0.71f,0.66f,0.69f,0.64f },
            new float[]{ 0.95f,0.88f,0.92f,0.86f,0.90f,0.84f,0.87f,0.82f },
        };
        float Reposo = 0.44f;   // AurReposo
        MethodInfo CALMA = TP.GetMethod("AurCalma", SP);
        // Dos filas de imagenes: la de arriba es EN REPOSO (nadie toca nada) y la de abajo, MIENTRAS SE
        // NAVEGA, que es cuando el fondo se serena para cansar menos la vista.
        float[] DIMS = { 1f, 0.36f + 0.64f * 0.45f };
        string[] DIMN = { "reposo", "navegando" };
        float DIM = DIMS[0];
        W = 1920; H = 800;   // pantalla ancha, como la de verdad: ahi es donde cantaban las esquinas
        // el horizonte, con la MISMA cuenta que el programa: BotStripTop (la 1a linea del titulo, menos 4)
        // mas 6, o sea 2 pixeles dentro de esa primera linea
        int progYp = H - 14 - 22, nowTopYp = progYp - 30 - 32 - 31;
        int baseY = nowTopYp + 2;
        float alto = H * 1.02f * 0.55f;

        int puntos = cols + 1;
        for (int c = 0; c < acentos.Length; c++)
          for (int d = 0; d < DIMS.Length; d++)
        {
            DIM = DIMS[d];
            using (Bitmap bmp = new Bitmap(W, H * momentos.Length, PixelFormat.Format32bppArgb))
            {
                for (int m = 0; m < momentos.Length; m++)
                {
                    // en reposo lo que manda es AurReposo, no la presencia llena
                    DIM = (m == 0) ? Reposo * (DIMS[d] / DIMS[0]) : DIMS[d];
                    px = new float[W*H]; py2 = new float[W*H]; pz = new float[W*H];
                    Color acento = acentos[c], B8 = acento;
                    Color cTop = Inv(SKY, B8), cMid = Inv(SKYM, B8), cBot = Inv(SKY2, B8);
                    for (int y = 0; y < H; y++)
                    {
                        float f = y / (float)(H-1);
                        Color a = (f < 0.5f) ? Mezcla(cTop, cMid, f/0.5f) : Mezcla(cMid, cBot, (f-0.5f)/0.5f);
                        for (int x = 0; x < W; x++) Pon(x, y, a.R, a.G, a.B);
                    }

                    // ---- las mismas cuentas por columna que hace DrawAurora ----
                    Color vivo = Inv(REAC, acento);
                    Color g1c = Gira(vivo, -10f, 1.00f), g2c = Gira(vivo, 22f, 0.98f);
                    float gp = (float)GPUNTA.Invoke(null, new object[]{ vivo });
                    Color g3c = Gira(vivo, gp-12f, 0.98f), g4c = Gira(vivo, gp+18f, 0.96f);
                    uint[] rampPie = new uint[filas+1], rampAgu = new uint[filas+1];
                    for (int k = 0; k <= filas; k++)
                    { float f = (float)k/filas; rampPie[k] = TMez(Pack(g1c), Pack(g3c), f); rampAgu[k] = TMez(Pack(g2c), Pack(g4c), f); }

                    float[] hC = new float[puntos], aC = new float[puntos], rC = new float[puntos];
                    float[] nC = new float[puntos], lC = new float[puntos];
                    uint[] cCor = new uint[(filas+1)*puntos], cSu = new uint[(suelos+1)*puntos];
                    uint[] cNeon = new uint[puntos], cLin = new uint[puntos];
                    float t = m * 3.1f;
                    for (int i = 0; i < puntos; i++)
                    {
                        float fx = (float)i/cols;
                        float v = Perfil(momentos[m], fx);
                        if (m == 0) { float vc = (float)CALMA.Invoke(null, new object[]{ fx, t }); if (vc > v) v = vc; }
                        if (v > 1f) v = 1f;
                        float rayo = Rayo(fx, t); rC[i] = rayo;
                        hC[i] = alto * (piso + (1f-piso)*v) * Onda(fx, t);
                        for (int k = 0; k <= filas; k++)
                        {
                            uint cc = Lerp(rampPie[k], rampAgu[k], fx);
                            float r = (cc>>16)&0xFF, g = (cc>>8)&0xFF, b = cc&0xFF;
                            Tono(ref r, ref g, ref b, v);
                            cCor[k*puntos+i] = ((uint)r<<16)|((uint)g<<8)|(uint)b;
                        }
                        cNeon[i] = cCor[filas*puntos+i];
                        uint cp = Lerp(Pack(g1c), Pack(g2c), fx);
                        float r0 = (cp>>16)&0xFF, g0 = (cp>>8)&0xFF, b0 = cp&0xFF;
                        MethodInfo ST = TP.GetMethod("AurSueloTono", SP);
                        for (int k = 0; k <= suelos; k++)
                        {
                            float r3 = r0, g3 = g0, b3 = b0;
                            Tono(ref r3, ref g3, ref b3, v*(float)ST.Invoke(null, new object[]{ (float)k/suelos }));
                            cSu[k*puntos+i] = ((uint)r3<<16)|((uint)g3<<8)|(uint)b3;
                        }
                        float al = (aBase + aPico*v)*DIM; if (al > 1f) al = 1f; aC[i] = al;
                        float ne = (nBase + nPico*v)*rayo*DIM; if (ne > 1f) ne = 1f; nC[i] = ne;
                        cLin[i] = cp;
                        float li = (lBase + lPico*v)*DIM; if (li > 1f) li = 1f; lC[i] = li;
                    }
                    Suaviza(hC); Suaviza(hC); Suaviza(hC);   // igual que el programa: tres pasadas

                    Array vs = Array.CreateInstance(TVtx, (filas+suelos)*cols*6);
                    int nv = (int)MALLA.Invoke(null, new object[]{ vs, hC, cCor, cSu, aC, rC, (float)W, (float)baseY, (float)H });
                    Raster(vs, nv, false);
                    Array vb = Array.CreateInstance(TVtx, brilloVtx);
                    int nb = (int)BRILLO.Invoke(null, new object[]{ vb, hC, cNeon, nC, cLin, lC, (float)W, (float)baseY });
                    Raster(vb, nb, true);
                    if (m == 0 && d == 0) Console.WriteLine("   relleno: " + nv + " vertices    neon: " + nb + " vertices");

                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            int k = y*W+x;
                            bmp.SetPixel(x, m*H+y, Color.FromArgb(255, Sat(px[k]), Sat(py2[k]), Sat(pz[k])));
                        }
                    Franja(bmp, m*H, W, H);
                }
                bmp.Save("neon_" + nombres[c] + "_" + DIMN[d] + ".png", ImageFormat.Png);
            }
            Console.WriteLine("neon_" + nombres[c] + "_" + DIMN[d] + ".png");
        }
        return 0;
    }
    static int Sat(float v) { return (v < 0f) ? 0 : (v > 255f ? 255 : (int)v); }

    static Label L(string t, Font f, Color c, int x, int y, int w, int h, ContentAlignment a)
    {
        Label l = new Label();
        l.Text = t; l.Font = f; l.ForeColor = c; l.UseMnemonic = false;
        l.SetBounds(x, y, w, h); l.TextAlign = a; l.AutoEllipsis = true;
        return l;
    }
    static void Franja(Bitmap bmp, int y0, int W, int H)
    {
        Font fNow = new Font("DejaVu Sans", 16f, FontStyle.Bold);
        Font fNext = new Font("DejaVu Sans", 13f, FontStyle.Bold);
        Font fMej = new Font("DejaVu Sans", 12f, FontStyle.Bold);
        Font fDer = new Font("DejaVu Sans", 18f, FontStyle.Bold);
        int pad = 14, progH = 22;
        int progY = H - pad - progH, nextY = progY - 30, nowY = nextY - 32;
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.TranslateTransform(0, y0);
            SOMBRA.Invoke(null, new object[]{ g, L("Reproduciendo:   Depeche Mode - I Feel You", fNow, Color.FromArgb(125,235,165), pad, nowY, 800, 30, ContentAlignment.TopLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("MEJORADA", fMej, Color.FromArgb(255,205,90), pad+530, nowY+5, 130, 22, ContentAlignment.MiddleLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("Siguiente:   -", fNext, Color.FromArgb(150,200,255), pad, nextY, 800, 26, ContentAlignment.TopLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("Creditos:  7", fDer, Color.FromArgb(255,205,90), W-14-250, nowY+2, 250, 34, ContentAlignment.TopLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("En cola:  0", fNext, Color.FromArgb(150,200,255), W-14-250, nowY+40, 250, 26, ContentAlignment.TopLeft) });
            SOMBRA.Invoke(null, new object[]{ g, L("0:46 / 4:36", fNext, Color.White, W-560, progY-2, 180, 26, ContentAlignment.TopRight) });
            using (SolidBrush b1 = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
            using (SolidBrush b2 = new SolidBrush(Color.FromArgb(210, 190, 120, 255)))
            {
                g.FillRectangle(b1, pad, progY, W-2*pad-370, progH-4);
                g.FillRectangle(b2, pad, progY, (int)((W-2*pad-370)*0.17f), progH-4);
            }
            g.ResetTransform();
        }
    }
    static Color Mezcla(Color a, Color b, float f)
    { return Color.FromArgb((int)(a.R+(b.R-a.R)*f), (int)(a.G+(b.G-a.G)*f), (int)(a.B+(b.B-a.B)*f)); }
}

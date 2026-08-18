using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

// EL FILO DE NEON y LA LINEA DEL HORIZONTE.
//
// El relleno de la aurora se apaga hacia arriba sin dejar borde: la forma se intuia, pero no se veia
// DIBUJADA. Ahora la silueta lleva encima un filo encendido -nucleo claro y resplandor del mismo color a los
// dos lados-, y al pie va una raya finisima de lado a lado sobre la que toda la aurora esta "parada", como el
// anillo que cruza el agujero negro. Las dos cosas se SUMAN a lo que hay (no tapan): eso es lo que las hace
// leerse como neon.
//
// Lo que se comprueba aqui: que el filo siga de verdad a la silueta, que encienda en el centro y se apague en
// los bordes, que sea el MISMO color de la aurora (no una raya postiza), y -lo mas importante- que el
// resplandor de la linea NO le quite legibilidad al texto de la franja de estado.
class Neon
{
    static Type TP, TF, TVtx;
    static FieldInfo fX, fY, fCol;
    static MethodInfo PERF, TONO, RAYO, TMEZ, LERP, MALLA, BRILLO, STONO, SKY, SKYM, SKY2, REAC, GIRA, GPUNTA;
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

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

    static void Hsv(float r, float g, float b, out float h, out float s, out float v)
    {
        float mx = Math.Max(r, Math.Max(g,b)), mn = Math.Min(r, Math.Min(g,b));
        v = mx/255f; float d = mx-mn;
        s = (mx < 0.001f) ? 0f : d/mx;
        if (d < 0.001f) { h = 0f; return; }
        if (mx == r) h = 60f*((((g-b)/d)%6f+6f)%6f);
        else if (mx == g) h = 60f*((b-r)/d+2f);
        else h = 60f*((r-g)/d+4f);
    }
    static float Lum(float r, float g, float b)
    {
        float[] c = { r/255f, g/255f, b/255f }; float[] u = new float[3];
        for (int i = 0; i < 3; i++) u[i] = (c[i] <= 0.03928f) ? (c[i]/12.92f) : (float)Math.Pow((c[i]+0.055)/1.055, 2.4);
        return 0.2126f*u[0] + 0.7152f*u[1] + 0.0722f*u[2];
    }
    static float Contraste(float r1, float g1, float b1, float r2, float g2, float b2)
    {
        float a = Lum(r1,g1,b1), b0 = Lum(r2,g2,b2);
        return (Math.Max(a,b0)+0.05f)/(Math.Min(a,b0)+0.05f);
    }

    // ---------------- un rasterizador chico, para poder MIRAR el resultado en numeros ----------------
    static int W, H, Y0;
    static float[] cr, cg, cb;
    static void Raster(Array vs, int nv, bool suma)
    {
        for (int t = 0; t + 2 < nv; t += 3)
        {
            float[] X = new float[3], Y = new float[3], R = new float[3], G = new float[3], B = new float[3], A = new float[3];
            for (int k = 0; k < 3; k++)
            {
                object v = vs.GetValue(t+k);
                X[k] = (float)fX.GetValue(v) + 0.5f; Y[k] = (float)fY.GetValue(v) + 0.5f;
                uint c = (uint)fCol.GetValue(v);
                A[k] = ((c>>24)&0xFF)/255f; R[k] = (c>>16)&0xFF; G[k] = (c>>8)&0xFF; B[k] = c&0xFF;
            }
            int ix0 = Math.Max(0, (int)Math.Floor(Math.Min(X[0], Math.Min(X[1], X[2]))));
            int ix1 = Math.Min(W-1, (int)Math.Ceiling(Math.Max(X[0], Math.Max(X[1], X[2]))));
            int iy0 = Math.Max(Y0, (int)Math.Floor(Math.Min(Y[0], Math.Min(Y[1], Y[2]))));
            int iy1 = Math.Min(H-1, (int)Math.Ceiling(Math.Max(Y[0], Math.Max(Y[1], Y[2]))));
            float den = (Y[1]-Y[2])*(X[0]-X[2]) + (X[2]-X[1])*(Y[0]-Y[2]);
            if (Math.Abs(den) < 1e-9f) continue;
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    float px = x + 0.5003f, py = y + 0.5007f;   // corrido: que ningun punto caiga en el borde justo
                    float l0 = ((Y[1]-Y[2])*(px-X[2]) + (X[2]-X[1])*(py-Y[2]))/den;
                    float l1 = ((Y[2]-Y[0])*(px-X[2]) + (X[0]-X[2])*(py-Y[2]))/den;
                    float l2 = 1f - l0 - l1;
                    if (l0 < 0f || l1 < 0f || l2 < 0f) continue;
                    float a = l0*A[0]+l1*A[1]+l2*A[2]; if (a <= 0.0005f) continue;
                    float r = l0*R[0]+l1*R[1]+l2*R[2], g = l0*G[0]+l1*G[1]+l2*G[2], b = l0*B[0]+l1*B[1]+l2*B[2];
                    int k = (y-Y0)*W+x;
                    if (suma) { cr[k]+=r*a; cg[k]+=g*a; cb[k]+=b*a; }
                    else { cr[k]+=(r-cr[k])*a; cg[k]+=(g-cg[k])*a; cb[k]+=(b-cb[k])*a; }
                }
        }
    }

    static int cols, filas, suelos, puntos, brilloVtx, lineaCols, contCols;
    static float[] hC, aC, rC, nC, lC;
    static uint[] cCor, cSu, cNeon, cLin;
    static float alto, baseY, DIM;

    // Rellena todo lo que DrawAurora calcula columna a columna, con las mismas cuentas.
    static void Columnas(Color acento, float[] B, float t, float dim)
    {
        float piso = Cte("AurPiso"), aBase = Cte("AurAlfaBase"), aPico = Cte("AurAlfaPico");
        float nBase = Cte("AurNeonBase"), nPico = Cte("AurNeonPico");
        float lBase = Cte("AurLineaBase"), lPico = Cte("AurLineaPico");
        Color vivo = Inv(REAC, acento);
        Color g1c = Gira(vivo, -10f, 1.00f), g2c = Gira(vivo, 22f, 0.98f);
        float gp = (float)GPUNTA.Invoke(null, new object[]{ vivo });
        Color g3c = Gira(vivo, gp-12f, 0.98f), g4c = Gira(vivo, gp+18f, 0.96f);
        uint[] rPie = new uint[filas+1], rAgu = new uint[filas+1];
        for (int k = 0; k <= filas; k++)
        { float f = (float)k/filas; rPie[k] = TMez(Pack(g1c), Pack(g3c), f); rAgu[k] = TMez(Pack(g2c), Pack(g4c), f); }
        for (int i = 0; i < puntos; i++)
        {
            float fx = (float)i/cols;
            float v = Perfil(B, fx); if (v > 1f) v = 1f;
            float rayo = Rayo(fx, t); rC[i] = rayo;
            hC[i] = alto * (piso + (1f-piso)*v) * Onda(fx, t);
            for (int k = 0; k <= filas; k++)
            {
                uint cc = Lerp(rPie[k], rAgu[k], fx);
                float r = (cc>>16)&0xFF, g = (cc>>8)&0xFF, b = cc&0xFF;
                Tono(ref r, ref g, ref b, v);
                cCor[k*puntos+i] = ((uint)r<<16)|((uint)g<<8)|(uint)b;
            }
            cNeon[i] = cCor[filas*puntos+i];
            uint cp = Lerp(Pack(g1c), Pack(g2c), fx);
            float r0 = (cp>>16)&0xFF, g0 = (cp>>8)&0xFF, b0 = cp&0xFF;
            for (int k = 0; k <= suelos; k++)
            {
                float r3=r0, g3=g0, b3=b0;
                Tono(ref r3, ref g3, ref b3, v*(float)STONO.Invoke(null, new object[]{ (float)k/suelos }));
                cSu[k*puntos+i] = ((uint)r3<<16)|((uint)g3<<8)|(uint)b3;
            }
            float al = (aBase + aPico*v)*dim; if (al > 1f) al = 1f; aC[i] = al;
            float ne = (nBase + nPico*v)*rayo*dim; if (ne > 1f) ne = 1f; nC[i] = ne;
            cLin[i] = cp;
            float li = (lBase + lPico*v)*dim; if (li > 1f) li = 1f; lC[i] = li;
        }
        Suaviza(hC); Suaviza(hC); Suaviza(hC);   // igual que el programa: tres pasadas
    }


    // El programa NO asigna el arreglo en cada cuadro: lo crea una vez y lo reusa. Aqui igual, o el reloj
    // acabaria midiendo el reparto de memoria en vez de lo que cuesta armar la malla.
    static Array vbCache; static object[] argsBrillo;
    static Array Brillo(out int nb)
    {
        if (vbCache == null)
        {
            vbCache = Array.CreateInstance(TVtx, brilloVtx);
            argsBrillo = new object[]{ vbCache, hC, cNeon, nC, cLin, lC, (float)W, baseY };
        }
        nb = (int)BRILLO.Invoke(null, argsBrillo);
        return vbCache;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in asm.GetTypes()) { if (t.Name == "GpuPanel") TP = t; if (t.Name == "Vtx") TVtx = t; }
        if (TP == null || TVtx == null) { Console.WriteLine("no hay panel GPU en este ejecutable"); return 0; }
        fX = TVtx.GetField("X"); fY = TVtx.GetField("Y"); fCol = TVtx.GetField("Color");
        BindingFlags SP = BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        PERF = TP.GetMethod("AurPerfil", SP); TONO = TP.GetMethod("TonoPorNivel", SP); RAYO = TP.GetMethod("AurRayo", SP); ONDA = TP.GetMethod("AurOndaAlto", SP); SUAV = TP.GetMethod("AurSuaviza", SP);
        TMEZ = TP.GetMethod("AurTonoMezcla", SP); LERP = TP.GetMethod("AurLerpRgb", SP);
        MALLA = TP.GetMethod("AurMalla", SP); BRILLO = TP.GetMethod("AurBrillo", SP); STONO = TP.GetMethod("AurSueloTono", SP);
        BindingFlags SF = BindingFlags.Static|BindingFlags.NonPublic;
        SKY = TF.GetMethod("SkyColor", SF); SKYM = TF.GetMethod("SkyColorMid", SF); SKY2 = TF.GetMethod("SkyColor2", SF);
        REAC = TF.GetMethod("ColorReactivo", SF); GIRA = TF.GetMethod("GiraTono", SF); GPUNTA = TF.GetMethod("GiroPunta", SP);
        if (BRILLO == null) { Console.WriteLine("falta AurBrillo"); return 1; }

        cols = (int)Cte("AurCols"); filas = (int)Cte("AurFilas"); suelos = (int)Cte("AurSuelo");
        brilloVtx = (int)Cte("AurBrilloVtx"); lineaCols = (int)Cte("AurLineaCols");
        contCols = (int)Cte("AurContCols"); puntos = cols + 1;
        float nNuc = Cte("AurNeonNucleo"), nFue = Cte("AurNeonFuera"), nDen = Cte("AurNeonDentro");
        float nBla = Cte("AurNeonBlanco"), nHal = Cte("AurNeonHalo");
        float lNuc = Cte("AurLineaNucleo"), lArr = Cte("AurLineaArriba"), lAba = Cte("AurLineaAbajo");
        float lAlz = Cte("AurLineaAlza");
        float lHal = Cte("AurLineaHalo");

        W = 1400; H = 585;
        // EL HORIZONTE, donde lo pone el programa de verdad: BaseY = BotStripTop + 6, y BotStripTop es la
        // primera linea del titulo menos 4. O sea: 2 pixeles DENTRO de esa primera linea. Es lo que obliga a
        // levantar la raya encendida; con un horizonte inventado mas abajo esto no se veria.
        int progYy = H - 14 - 22, nextYy = progYy - 30, nowYy = nextYy - 32, nowTopYy = nowYy - 31;
        baseY = nowTopYy + 2;
        alto = H * 1.02f * 0.55f;
        // EN REPOSO, que es cuando la aurora sale ENTERA: es el peor caso para el texto de la franja. Al
        // navegar baja al 65% y todo queda mas holgado, asi que si aqui se lee, alli tambien.
        DIM = 1f;
        hC = new float[puntos]; aC = new float[puntos]; rC = new float[puntos];
        nC = new float[puntos]; lC = new float[puntos];
        cCor = new uint[(filas+1)*puntos]; cSu = new uint[(suelos+1)*puntos];
        cNeon = new uint[puntos]; cLin = new uint[puntos];
        Color acento = Color.FromArgb(150,60,190);
        float[] fuerte = { 0.95f,0.88f,0.92f,0.86f,0.90f,0.84f,0.87f,0.82f };
        float[] flojo  = { 0.20f,0.18f,0.22f,0.19f,0.21f,0.17f,0.20f,0.16f };

        Console.WriteLine("\n  1) EL FILO SIGUE A LA SILUETA, NO VA POR SU CUENTA\n");
        Columnas(acento, fuerte, 0f, DIM);
        int nb; Array vb = Brillo(out nb);
        Check(nb == brilloVtx, "el brillo sale completo: " + nb + " vertices");

        // El NUCLEO del contorno tiene que PASAR por la cresta de cada columna de la cortina...
        int sub = contCols / cols;
        float[] cx = new float[contCols+1], cy = new float[contCols+1];
        float peorDesvio = 0f;
        for (int q = 0; q < contCols; q++)
        {
            int baseIdx = (5*lineaCols + q*4 + 2) * 6;   // las 5 franjas de la raya van primero; la 3a de cada 4 es el nucleo
            object v0 = vb.GetValue(baseIdx);
            cx[q] = (float)fX.GetValue(v0) + 0.5f;
            cy[q] = (float)fY.GetValue(v0) + 0.5f + nNuc;   // el nucleo se dibuja nNuc por encima de la cresta
            if (q % sub != 0) continue;
            float d = Math.Abs(cy[q] - (baseY - hC[q/sub])); if (d > peorDesvio) peorDesvio = d;
        }
        { object vf = vb.GetValue((5*lineaCols + (contCols-1)*4 + 2) * 6 + 1);   // el ultimo punto, del borde derecho
          cx[contCols] = (float)fX.GetValue(vf) + 0.5f; cy[contCols] = (float)fY.GetValue(vf) + 0.5f + nNuc; }
        Console.WriteLine("     el filo se separa de la cresta como mucho " + peorDesvio.ToString("0.000") + " px");
        Check(peorDesvio < 0.01f, "el filo PASA por la cresta en las " + cols + " columnas: dibuja la forma, no otra cosa");

        // ...pero ENTRE una columna y la siguiente no va en recta, sino curvando. Eso es lo que quita las
        // esquinas. Se mide el ANGULO que gira el filo en cada union: cuanto mas chico, mas redondo se ve.
        float giroCurva = 0f, giroRecta = 0f; int peorQ = -1;
        for (int q = 1; q < contCols; q++)
        {
            double a1 = Math.Atan2(cy[q]-cy[q-1], cx[q]-cx[q-1]);
            double a2 = Math.Atan2(cy[q+1]-cy[q], cx[q+1]-cx[q]);
            double g = Math.Abs(a2-a1); if (g > Math.PI) g = 2*Math.PI - g;
            if (g > giroCurva) { giroCurva = (float)g; peorQ = q; }
        }
        // ASI ERA ANTES: la silueta con las vetas finas metidas en la altura, sin redondear, y unida con
        // rectas de columna a columna. Es lo que se veia en pantalla y lo que salia como una sierra.
        float pasoR = W / (float)cols;
        float pisoC = Cte("AurPiso");
        float[] antes = new float[puntos];
        for (int i2 = 0; i2 < puntos; i2++)
        {
            float fx = (float)i2/cols; float vv = Perfil(fuerte, fx); if (vv > 1f) vv = 1f;
            antes[i2] = alto * (pisoC + (1f-pisoC)*vv) * Rayo(fx, 0f);
        }
        for (int i2 = 1; i2 < cols; i2++)
        {
            double a1 = Math.Atan2(antes[i2-1]-antes[i2], pasoR);
            double a2 = Math.Atan2(antes[i2]-antes[i2+1], pasoR);
            double g = Math.Abs(a2-a1); if (g > Math.PI) g = 2*Math.PI - g;
            if (g > giroRecta) giroRecta = (float)g;
        }
        Console.WriteLine(string.Format("     en una pantalla de {0} px: antes (vetas en la altura y rectas de columna a columna) el filo llegaba a girar {1:0.0} grados de golpe;",
                                        W, giroRecta*180/Math.PI));
        Console.WriteLine(string.Format("     por la curva, {0:0.0} grados (lo peor, en x={1:0} de {2})", giroCurva*180/Math.PI, cx[peorQ], W));
        if (Environment.GetEnvironmentVariable("NEON_DUMP") != null)
            for (int q = Math.Max(0,peorQ-4); q <= Math.Min(contCols,peorQ+4); q++)
                Console.WriteLine(string.Format("        q={0,3}  x={1,8:0.00}  y={2,8:0.00}  alturaCurva={3,8:0.00}  hC[{4}]={5,8:0.00}",
                                                q, cx[q], cy[q], baseY-cy[q], q/sub, hC[q/sub]));
        Check(giroCurva < giroRecta * 0.45f, "la esquina mas cerrada se queda en menos de la mitad de lo que era");
        // Y lo que de verdad decide si algo "se ve cuadrado": cuanto se separa la linea DIBUJADA (que son
        // tramos rectos, siempre) de la curva de verdad. Si esa distancia no llega al pixel, no hay poligono
        // que ver, por muy cerrada que sea la vuelta.
        MethodInfo CURVA = TP.GetMethod("AurCurvaH", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        float peorFlecha = 0f;
        if (CURVA != null)
        {
            for (int q = 0; q < contCols; q++)
            {
                float sMed = (q + 0.5f) / sub;                       // el punto medio del tramo, en columnas
                int im = (int)sMed; float um = sMed - im;
                if (im > cols) { im = cols; um = 0f; }
                float hMed = (float)CURVA.Invoke(null, new object[]{ hC, im, um });
                float recta = ((baseY - cy[q]) + (baseY - cy[q+1])) * 0.5f;
                float d = Math.Abs(hMed - recta); if (d > peorFlecha) peorFlecha = d;
            }
            Console.WriteLine("     y la linea dibujada nunca se separa de la curva mas de " + peorFlecha.ToString("0.00") + " px");
        }
        Check(CURVA != null && peorFlecha < 0.5f, "el filo dibujado va a menos de medio pixel de la curva: no hay poligono que ver");
        // y sin desmadrarse: la curva no puede irse lejos de la silueta
        float fuera = 0f;
        for (int q = 0; q < contCols; q++)
        {
            int i2 = q/sub; int i3 = (i2 < cols) ? i2+1 : cols;
            float lo = Math.Min(hC[i2], hC[i3]), hi = Math.Max(hC[i2], hC[i3]);
            float h = baseY - cy[q];
            float d = (h < lo) ? (lo-h) : ((h > hi) ? (h-hi) : 0f);
            if (d > fuera) fuera = d;
        }
        Console.WriteLine("     y nunca se aleja de la silueta mas de " + fuera.ToString("0.0") + " px");
        Check(fuera < 14f, "la curva redondea, pero no se inventa una forma distinta de la que trae la musica");
        // Redondear NO puede aplanar el espectro: la diferencia entre lo mas alto y lo mas bajo tiene que
        // seguir estando ahi, o el fondo dejaria de contar de que va la cancion.
        float[] crudo = new float[puntos];
        for (int i2 = 0; i2 < puntos; i2++)
        {
            float fx = (float)i2/cols; float vv = Perfil(fuerte, fx); if (vv > 1f) vv = 1f;
            crudo[i2] = alto * (Cte("AurPiso") + (1f-Cte("AurPiso"))*vv) * Onda(fx, 0f);
        }
        float rc0 = -1e9f, rc1 = 1e9f, rs0 = -1e9f, rs1 = 1e9f;
        for (int i2 = 0; i2 < puntos; i2++)
        {
            if (crudo[i2] > rc0) rc0 = crudo[i2]; if (crudo[i2] < rc1) rc1 = crudo[i2];
            if (hC[i2] > rs0) rs0 = hC[i2]; if (hC[i2] < rs1) rs1 = hC[i2];
        }
        Console.WriteLine(string.Format("     del pico mas alto al valle mas hondo: {0:0} px antes de redondear, {1:0} despues", rc0-rc1, rs0-rs1));
        Check((rs0-rs1) > (rc0-rc1) * 0.75f, "redondear conserva mas de tres cuartos del relieve: el fondo sigue contando de que va la cancion");

        // Corte vertical por una columna: el brillo tiene que subir al centro y morir en los dos bordes.
        List<float> ys = new List<float>(); List<float> als = new List<float>();
        for (int k = 0; k < nb; k++)
        {
            object v = vb.GetValue(k);
            float x = (float)fX.GetValue(v) + 0.5f;
            if (Math.Abs(x) > 0.01f) continue;   // la columna del borde izquierdo
            ys.Add((float)fY.GetValue(v) + 0.5f);
            als.Add(((uint)fCol.GetValue(v) >> 24)/255f);
        }
        float cresta = baseY - hC[0];
        float aFuera = -1, aNucleo = -1, aDentro = -1, aMedio = -1;
        for (int k = 0; k < ys.Count; k++)
        {
            float dy = ys[k] - cresta;
            if (Math.Abs(dy + nFue) < 0.01f) aFuera = als[k];
            if (Math.Abs(dy + nNuc) < 0.01f) aNucleo = als[k];
            if (Math.Abs(dy - nDen) < 0.01f) aDentro = als[k];
            if (Math.Abs(dy + nFue*Cte("AurNeonQuiebre")) < 0.01f) aMedio = als[k];
        }
        Console.WriteLine(string.Format("     de fuera hacia dentro:  {0:0}px:{1:0}%   {2:0}px:{3:0}%   nucleo:{4:0}%   {5:0}px:{6:0}%",
                                        -nFue, aFuera*100, -nFue*Cte("AurNeonQuiebre"), aMedio*100, aNucleo*100, nDen, aDentro*100));
        Check(aFuera == 0f && aDentro == 0f, "el resplandor MUERE en los dos extremos: no deja canto duro por ningun lado");
        Check(aNucleo > aMedio && aMedio > 0f, "y va encendiendose hacia el centro: el nucleo es lo mas claro");
        Check(Math.Abs(aMedio/aNucleo - nHal) < 0.02f, "el resplandor vale el " + (int)Math.Round(nHal*100) + "% del nucleo: se ve el halo sin comerse la forma");

        Console.WriteLine("\n  2) ES EL COLOR DE LA AURORA, ENCENDIDO. NO UNA RAYA POSTIZA\n");
        // el nucleo del filo: mismo TONO que la punta de la cortina, pero mas claro
        bool mismoTono = true, masClaro = true, noBlanco = true;
        for (int i = 0; i < puntos; i += 17)
        {
            uint cor = cNeon[i];
            float hr = (cor>>16)&0xFF, hg = (cor>>8)&0xFF, hb = cor&0xFF;
            float kr = hr + (255f-hr)*nBla, kg = hg + (255f-hg)*nBla, kb = hb + (255f-hb)*nBla;
            float h1,s1,v1, h2,s2,v2;
            Hsv(hr,hg,hb, out h1, out s1, out v1);
            Hsv(kr,kg,kb, out h2, out s2, out v2);
            float dh = Math.Abs(h1-h2); if (dh > 180f) dh = 360f-dh;
            if (dh > 3f) mismoTono = false;
            if (v2 <= v1 + 0.01f) masClaro = false;
            if (s2 < 0.18f) noBlanco = false;   // sigue siendo color, no blanco
        }
        Check(mismoTono, "el nucleo del filo es EXACTAMENTE el mismo tono que la aurora en ese punto");
        Check(masClaro, "solo que mas claro: es el mismo color encendido, que es como se ve un tubo de neon");
        Check(noBlanco, "y no llega a blanco: conserva su color");

        Console.WriteLine("\n  3) LA LINEA DEL HORIZONTE: SOBRE ELLA SE APOYA TODO\n");
        // Corte vertical de la linea, columna del borde izquierdo (las 5 primeras franjas de cada columna)
        float arribaFuera = -1, arribaNuc = -1, abajoNuc = -1, abajoFuera = -1;
        for (int k = 0; k < ys.Count; k++)
        {
            float dy = ys[k] - (baseY - lAlz);
            if (Math.Abs(dy + lArr) < 0.01f) arribaFuera = als[k];
            if (Math.Abs(dy + lNuc) < 0.01f) arribaNuc = als[k];
            if (Math.Abs(dy - lNuc) < 0.01f) abajoNuc = als[k];
            if (Math.Abs(dy - lAba) < 0.01f) abajoFuera = als[k];
        }
        Console.WriteLine(string.Format("     sube {0:0} px y baja {1:0} px; el nucleo mide {2:0.0} px de alto y vale {3:0}%",
                                        lArr, lAba, lNuc*2, arribaNuc*100));
        Check(arribaFuera == 0f && abajoFuera == 0f, "el resplandor de la linea muere arriba y abajo: no hay borde que se vea");
        Check(Math.Abs(arribaNuc - abajoNuc) < 0.001f, "el nucleo es parejo: una raya, no un degradado");
        Check(lArr > lAba * 2f, "sube MUCHO mas de lo que baja: el brillo va hacia la aurora, no hacia el texto");
        Check(lNuc * 2f <= 3.2f, "y es finisima (" + (lNuc*2).ToString("0.0") + " px): una raya de luz, como la del agujero negro");

        // La linea existe DE LADO A LADO, tambien donde la cancion casi no suena
        Columnas(acento, flojo, 0f, DIM);
        float minLinea = 9f, maxLinea = -9f;
        for (int i = 0; i < puntos; i++) { if (lC[i] < minLinea) minLinea = lC[i]; if (lC[i] > maxLinea) maxLinea = lC[i]; }
        Console.WriteLine("     con la cancion floja la linea sigue al " + (minLinea*100).ToString("0") + "%");
        Check(minLinea > 0.25f, "con la cancion floja la linea SIGUE ahi: es el suelo, no un efecto que aparece y se va");
        float medFloja = 0f; for (int i = 0; i < puntos; i++) medFloja += lC[i]; medFloja /= puntos;
        Columnas(acento, fuerte, 0f, DIM);
        float medFuerte = 0f; for (int i = 0; i < puntos; i++) medFuerte += lC[i]; medFuerte /= puntos;
        Console.WriteLine("     y en el pico sube al " + (medFuerte*100).ToString("0") + "% de media (con la cancion floja era el "
                          + (medFloja*100).ToString("0") + "%)");
        Check(medFuerte > medFloja + 0.15f, "pero late con la musica: en el pico se enciende bastante mas");

        Console.WriteLine("\n  4) SE ATENUA CON TODO LO DEMAS\n");
        Columnas(acento, fuerte, 0f, 0f);
        int nb0; Array vb0 = Brillo(out nb0);
        int vivos = 0;
        for (int k = 0; k < nb0; k++) if ((((uint)fCol.GetValue(vb0.GetValue(k))) >> 24) != 0) vivos++;
        Check(vivos == 0, "con el fondo apagado del todo, el neon tampoco pinta nada (no se queda encendido por su cuenta)");

        Console.WriteLine("\n  5) EL TEXTO DE LA FRANJA DE ESTADO SE SIGUE LEYENDO\n");
        // Se compone de verdad -cielo + relleno + neon- y se mide el contraste del texto contra lo que
        // queda. Se prueba con los TRES colores de genero: el naranja deja el fondo mas claro que el morado o
        // el azul, asi que con uno solo no se sabria nada.
        Color[] generos = { Color.FromArgb(150,60,190), Color.FromArgb(230,140,40), Color.FromArgb(60,110,225) };
        string[] nomGen = { "morado", "naranja", "azul" };
        int progY = progYy, nextY = nextYy, nowY = nowYy, nowTopY = nowTopYy;
        Color verde = Color.FromArgb(125,235,165), azul = Color.FromArgb(150,200,255);
        // La 1a linea del titulo casi siempre va VACIA: solo se usa cuando el titulo no cabe en una sola. Las
        // otras tres llevan texto siempre.
        string[] filaNom = { "1a linea del titulo", "Reproduciendo", "Siguiente", "tiempo / barra" };
        int[] filaY = { nowTopY+8, nowY+12, nextY+10, progY+6 };
        Color[] filaC = { verde, verde, azul, Color.White };
        float peorSiempre = 99f, peorTodo = 99f; string quienSiempre = "", quienTodo = "";
        float peorCaida = 0f; string dondeCaida = "";
        Console.WriteLine("     genero    fila                     sin neon   con neon   lo que quita el neon");
        for (int gg = 0; gg < generos.Length; gg++)
        {
            float[] sinN = new float[filaY.Length], conN = new float[filaY.Length];
            for (int pase = 0; pase < 2; pase++)   // pase 0 = sin el neon; pase 1 = con el neon
            {
                Columnas(generos[gg], fuerte, 0f, DIM);
                Y0 = (int)baseY - 50;
                cr = new float[W*(H-Y0)]; cg = new float[W*(H-Y0)]; cb = new float[W*(H-Y0)];
                Color cMid = Inv(SKYM, generos[gg]), cBot = Inv(SKY2, generos[gg]);
                for (int y = Y0; y < H; y++)
                {
                    float f = (y/(float)(H-1) - 0.5f)/0.5f; if (f < 0f) f = 0f; if (f > 1f) f = 1f;
                    float r = cMid.R + (cBot.R-cMid.R)*f, g = cMid.G + (cBot.G-cMid.G)*f, b = cMid.B + (cBot.B-cMid.B)*f;
                    for (int x = 0; x < W; x++) { int k = (y-Y0)*W+x; cr[k]=r; cg[k]=g; cb[k]=b; }
                }
                Array vm = Array.CreateInstance(TVtx, (filas+suelos)*cols*6);
                int nm = (int)MALLA.Invoke(null, new object[]{ vm, hC, cCor, cSu, aC, rC, (float)W, baseY, (float)H });
                Raster(vm, nm, false);
                if (pase == 1) { int nb2; Array vbb = Brillo(out nb2); Raster(vbb, nb2, true); }
                for (int q = 0; q < filaY.Length; q++)
                {
                    int y = filaY[q]; if (y < Y0) y = Y0; if (y > H-1) y = H-1;
                    float sr=0, sg=0, sb=0; int n2=0;
                    for (int x = 8; x < 760; x++) { int k=(y-Y0)*W+x; sr+=Math.Min(255f,cr[k]); sg+=Math.Min(255f,cg[k]); sb+=Math.Min(255f,cb[k]); n2++; }
                    sr/=n2; sg/=n2; sb/=n2;
                    float ct = Contraste(filaC[q].R, filaC[q].G, filaC[q].B, sr, sg, sb);
                    if (pase == 0) sinN[q] = ct; else conN[q] = ct;
                }
            }
            for (int q = 0; q < filaY.Length; q++)
            {
                float caida = sinN[q] - conN[q];
                Console.WriteLine(string.Format("     {0,-9} {1,-22}   {2,6:0.0}     {3,6:0.0}       {4,6:0.00}",
                                                nomGen[gg], filaNom[q], sinN[q], conN[q], caida));
                if (caida > peorCaida) { peorCaida = caida; dondeCaida = filaNom[q] + " en " + nomGen[gg]; }
                if (conN[q] < peorTodo) { peorTodo = conN[q]; quienTodo = filaNom[q] + " en " + nomGen[gg]; }
                if (q > 0 && conN[q] < peorSiempre) { peorSiempre = conN[q]; quienSiempre = filaNom[q] + " en " + nomGen[gg]; }
            }
        }
        Check(peorCaida <= 0.05f, "el neon NO le quita legibilidad al texto: lo que mas baja el contraste es "
              + peorCaida.ToString("0.00") + " en " + (dondeCaida.Length > 0 ? dondeCaida : "ninguna fila"));
        Check(peorSiempre >= 4.5f, "las filas que SIEMPRE llevan texto quedan en " + peorSiempre.ToString("0.0")
              + ":1 lo peor (" + quienSiempre + "), por encima del 4.5:1 que pide la norma");
        // La 1a linea del titulo cae mas abajo, ya pegada al relleno; ahi la norma pide 3:1 (es texto grande y
        // en negrita) y ademas lleva su propio contorno oscuro.
        Check(peorTodo >= 3f, "y la 1a linea del titulo, que es la mas pegada al color, queda en "
              + peorTodo.ToString("0.0") + ":1: por encima del 3:1 que pide la norma para texto grande en negrita");

        // Y que el nucleo de la linea no caiga NUNCA sobre una fila de texto
        Console.WriteLine(string.Format("     el nucleo de la raya esta en el pixel {0:0}, su resplandor acaba en el {1:0}, y la 1a fila de texto empieza en el {2:0}",
                                        baseY - lAlz, baseY - lAlz + lAba, nowTopY));
        Check(baseY - lAlz + lAba < nowTopY, "el resplandor de la raya se acaba ANTES de donde empieza el texto");

        Console.WriteLine("\n  6) LO QUE CUESTA\n");
        Columnas(acento, fuerte, 0f, DIM);
        { int z0; Brillo(out z0); }                       // que el motor ya lo tenga compilado
        GC.Collect(); GC.WaitForPendingFinalizers();
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        int vueltas = 600;
        for (int q = 0; q < vueltas; q++) { int z; Brillo(out z); }
        sw.Stop();
        double ms = sw.Elapsed.TotalMilliseconds / vueltas;
        Console.WriteLine("     armar el neon cuesta " + ms.ToString("0.000") + " ms por cuadro ("
                          + (ms*60*100/1000).ToString("0.0") + "% de un nucleo a 60 cuadros/s)");
        Check(ms < 0.5, "armarlo cuesta menos de medio milisegundo: no le quita nada al dibujado");
        Console.WriteLine("     " + brilloVtx + " vertices mas por cuadro, y UNA sola llamada de dibujo mas");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

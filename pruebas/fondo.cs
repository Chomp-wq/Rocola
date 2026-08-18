using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

// FONDO REACTIVO del horizonte. Lo importante de esta version: la intensidad se dibuja con TONOS DE LA
// MISMA FAMILIA (morado oscuro -> morado -> morado claro) en vez de tirando a blanco, que era lo que
// quemaba el centro y lavaba el color.
class Fondo
{
    const int FS = 44100, CH = 2;
    static Type TF, TP; static object F; static MethodInfo REP, AVZ, TONO;
    static object Fld(string n) { return TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(F); }
    static void SetFld(string n, object v) { TF.GetField(n, BindingFlags.Instance|BindingFlags.NonPublic).SetValue(F, v); }
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static MethodInfo PERF; static Type TPn;
    static float Perfil(float[] B, float fx) { return (float)PERF.Invoke(null, new object[]{ B, fx }); }
    static float Cte(string n)
    {
        FieldInfo f = TPn.GetField(n, BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        object v = f.GetRawConstantValue();
        return (v is int) ? (int)v : (float)v;
    }

    // contraste entre dos colores tal como lo mide la norma WCAG (1 = iguales, 21 = negro contra blanco)
    static float Lum(float[] c)
    {
        float[] u = new float[3];
        for (int i = 0; i < 3; i++)
        {
            float x = c[i]/255f;
            u[i] = (x <= 0.03928f) ? (x/12.92f) : (float)Math.Pow((x+0.055)/1.055, 2.4);
        }
        return 0.2126f*u[0] + 0.7152f*u[1] + 0.0722f*u[2];
    }
    static float Contraste(float[] a, float[] b)
    {
        float la = Lum(a), lb = Lum(b);
        return (Math.Max(la,lb) + 0.05f) / (Math.Min(la,lb) + 0.05f);
    }

    // viveza (saturacion) de un color empaquetado: 0 = gris, 1 = color puro
    static float Sat(uint c)
    {
        float r = ((c>>16)&0xFF)/255f, g = ((c>>8)&0xFF)/255f, b = (c&0xFF)/255f;
        float mx = Math.Max(r, Math.Max(g,b)), mn = Math.Min(r, Math.Min(g,b));
        return (mx < 0.001f) ? 0f : (mx-mn)/mx;
    }
    static uint Bruto(uint c0, uint c1, float f)
    {
        uint r = (uint)(((c0>>16)&0xFF) + (((int)((c1>>16)&0xFF)-(int)((c0>>16)&0xFF))*f));
        uint g = (uint)(((c0>>8)&0xFF) + (((int)((c1>>8)&0xFF)-(int)((c0>>8)&0xFF))*f));
        uint b = (uint)((c0&0xFF) + (((int)(c1&0xFF)-(int)(c0&0xFF))*f));
        return (r<<16)|(g<<8)|b;
    }

    static float SueTono(MethodInfo m, float f) { return (float)m.Invoke(null, new object[]{ f }); }
    static float SueAlfa(MethodInfo m, float f) { return (float)m.Invoke(null, new object[]{ f }); }
    static float Ray(MethodInfo m, float fx, float t) { return (float)m.Invoke(null, new object[]{ fx, t }); }

    static float[] Tono(float r, float g, float b, float v)
    { object[] a = { r, g, b, v }; TONO.Invoke(null, a); return new float[]{ (float)a[0], (float)a[1], (float)a[2] }; }
    // tono (grados), saturacion y brillo de un color: para comprobar que la familia no cambia
    static void Hsv(float[] c, out float h, out float s, out float v)
    {
        float mx = Math.Max(c[0], Math.Max(c[1], c[2])), mn = Math.Min(c[0], Math.Min(c[1], c[2]));
        v = mx / 255f; float d = mx - mn;
        s = (mx < 0.001f) ? 0f : d / mx;
        if (d < 0.001f) { h = 0f; return; }
        if (mx == c[0]) h = 60f * ((((c[1]-c[2])/d) % 6f + 6f) % 6f);
        else if (mx == c[1]) h = 60f * ((c[2]-c[0])/d + 2f);
        else h = 60f * ((c[0]-c[1])/d + 4f);
    }

    static float[] Bandas(double hz, double amp, double seg)
    {
        SetFld("specLp", null); SetFld("specCoef", null); SetFld("specRate", 0);
        Array.Clear((float[])Fld("specVis"), 0, 8); Array.Clear((float[])Fld("specNow"), 0, 8);
        Array.Clear((float[])Fld("specPiso"), 0, 8); Array.Clear((float[])Fld("specGolpe"), 0, 8);
        SetFld("levelQ", Activator.CreateInstance(TF.GetField("levelQ", BindingFlags.Instance|BindingFlags.NonPublic).FieldType));
        int n = (int)(seg * FS); float[] buf = new float[4096 * CH]; int hecho = 0;
        while (hecho < n)
        {
            int m = Math.Min(4096, n - hecho);
            for (int i = 0; i < m; i++) { float v = (float)(amp * Math.Sin(2*Math.PI*hz*(hecho+i)/FS)); buf[i*CH]=v; buf[i*CH+1]=v; }
            REP.Invoke(F, new object[] { buf, 0, m*CH, FS, CH });
            hecho += m;
        }
        object q = Fld("levelQ"); Type tq = q.GetType();
        int cnt = (int)tq.GetProperty("Count").GetValue(q, null);
        MethodInfo deq = tq.GetMethod("Dequeue"); object ult = null;
        for (int i = 0; i < cnt; i++) ult = deq.Invoke(q, null);
        if (ult != null)
        {
            float[] esp = (float[])ult.GetType().GetField("Esp").GetValue(ult);
            if (esp != null) Array.Copy(esp, (float[])Fld("specNow"), 8);
        }
        for (int i = 0; i < 40; i++) AVZ.Invoke(F, null);
        return (float[])((float[])Fld("specVis")).Clone();
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in asm.GetTypes()) if (t.Name == "GpuPanel") TP = t;
        F = FormatterServices.GetUninitializedObject(TF);
        SetFld("levelLock", new object()); SetFld("nowPlaying", null);
        SetFld("specVis", new float[8]); SetFld("specNow", new float[8]); SetFld("auroraAmount", 0.55f);
        SetFld("specPiso", new float[8]); SetFld("specGolpe", new float[8]);
        SetFld("levelQ", Activator.CreateInstance(TF.GetField("levelQ", BindingFlags.Instance|BindingFlags.NonPublic).FieldType));
        REP = TF.GetMethod("ReportAudioBlock", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        AVZ = TF.GetMethod("AvanzaAurora", BindingFlags.Instance|BindingFlags.NonPublic);
        TONO = (TP != null) ? TP.GetMethod("TonoPorNivel", BindingFlags.Static|BindingFlags.NonPublic) : null;
        TPn = TP;
        PERF = (TP != null) ? TP.GetMethod("AurPerfil", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public) : null;
        if (AVZ == null || TONO == null || PERF == null) { Console.WriteLine("faltan metodos"); return 1; }

        // ---------- ESCALERA DE TONOS ----------
        float[] morado = { 150f, 60f, 190f };   // un morado cualquiera
        Console.WriteLine("\n  ESCALERA DE TONOS sobre un MORADO (150,60,190)\n");
        Console.WriteLine("   intensidad     color         tono   saturacion   brillo");
        float h0 = -1; bool mismoTono = true, subeBrillo = true, bajaSat = true, nuncaBlanco = true;
        float vAnt = -1, sAnt = 9, sat0 = -1;
        for (int k = 0; k <= 4; k++)
        {
            float v = k / 4f;
            float[] c = Tono(morado[0], morado[1], morado[2], v);
            float hh, ss, vv; Hsv(c, out hh, out ss, out vv);
            if (h0 < 0) h0 = hh;
            if (Math.Abs(hh - h0) > 2f) mismoTono = false;
            if (vv < vAnt - 0.001f) subeBrillo = false;
            if (ss > sAnt + 0.001f) bajaSat = false;
            if (sat0 < 0) sat0 = ss;
            // "no llega a blanco" = conserva buena parte de su color, y sigue habiendo distancia entre canales
            if (ss < sat0 * 0.40f || (Math.Max(c[0], Math.Max(c[1], c[2])) - Math.Min(c[0], Math.Min(c[1], c[2]))) < 25f) nuncaBlanco = false;
            vAnt = vv; sAnt = ss;
            Console.WriteLine(string.Format("     {0:0.00}      {1,3:0},{2,3:0},{3,3:0}     {4,5:0.0}      {5,5:0.00}     {6,5:0.00}",
                                            v, c[0], c[1], c[2], hh, ss, vv));
        }
        Check(mismoTono, "el TONO no cambia: sigue siendo el mismo morado en todas las intensidades");
        Check(subeBrillo, "cuanto mas fuerte suena, mas claro sale el morado");
        Check(bajaSat, "y menos saturado, que es lo que distingue un tono claro de uno oscuro");
        Check(nuncaBlanco, "nunca se acerca al blanco: conserva al menos el 40% de su color en el pico");

        // el rango tiene que NOTARSE
        float[] flojo = Tono(morado[0], morado[1], morado[2], 0f);
        float[] fuerte = Tono(morado[0], morado[1], morado[2], 1f);
        float hf, sf, vf, hF, sF, vF;
        Hsv(flojo, out hf, out sf, out vf); Hsv(fuerte, out hF, out sF, out vF);
        Console.WriteLine(string.Format("\n  de lo mas flojo a lo mas fuerte: el brillo sube {0:0.0}x y la saturacion baja {1:0}%", vF/vf, (1-sF/sf)*100));
        Check(vF / vf > 1.8f, "la diferencia entre flojo y fuerte se NOTA (mas del doble de brillo)");

        // otros colores de genero: la familia tampoco cambia
        float[][] otros = { new float[]{230,140,40}, new float[]{60,110,225}, new float[]{40,180,120} };
        bool todosFamilia = true;
        for (int i = 0; i < otros.Length; i++)
        {
            float ha=-1; 
            for (int k = 0; k <= 4; k++)
            {
                float[] c = Tono(otros[i][0], otros[i][1], otros[i][2], k/4f);
                float hh, ss, vv; Hsv(c, out hh, out ss, out vv);
                if (ha < 0) ha = hh; else if (Math.Abs(hh - ha) > 2f) todosFamilia = false;
            }
        }
        Check(todosFamilia, "vale igual para cualquier color de genero (naranja, azul, verde...)");

        // negro y gris: no truena
        float[] neg = Tono(0f, 0f, 0f, 1f);
        Check(neg[0] == 0f && neg[1] == 0f && neg[2] == 0f, "un color negro no truena (se queda como esta)");
        float[] gris = Tono(120f, 120f, 120f, 1f);
        Check(Math.Abs(gris[0]-gris[1]) < 0.01f && Math.Abs(gris[1]-gris[2]) < 0.01f, "un gris sigue siendo gris (no se inventa color)");

        // ---------- QUE EL ESPECTRO SIGA MIDIENDO BIEN ----------
        string[] bn = { "sub","grave","bajo-medio","medio","medio-alto","presencia","brillo","aire" };
        double[] tonos = { 40, 100, 240, 550, 1200, 2800, 6000, 13000 };
        Console.WriteLine("\n  cada tono enciende SU banda:");
        int aciertos = 0;
        for (int k = 0; k < tonos.Length; k++)
        {
            float[] v = Bandas(tonos[k], 0.45, 0.9);
            int mx = 0; for (int b = 1; b < 8; b++) if (v[b] > v[mx]) mx = b;
            string barra = ""; for (int b = 0; b < 8; b++) barra += (v[b] > 0.66f ? "#" : v[b] > 0.33f ? "+" : v[b] > 0.05f ? "." : " ");
            if (Math.Abs(mx - k) <= 1) aciertos++;
            Console.WriteLine(string.Format("   {0,6:0} Hz  [{1}]  {2}", tonos[k], barra, bn[mx]));
        }
        Check(aciertos >= 7, "cada tono enciende su banda o la vecina: " + aciertos + " de 8");
        float[] sil = Bandas(1000, 0.0, 0.6);
        double suma = 0; foreach (float x in sil) suma += x;
        Check(suma < 0.02, "en silencio se apaga sola");

        // con VIDEO no llegan bandas nuevas: no puede quedarse congelada
        Bandas(240, 0.5, 0.8);
        Array.Clear((float[])Fld("specNow"), 0, 8);
        for (int i = 0; i < 90; i++) AVZ.Invoke(F, null);
        float pico = 0f; foreach (float x in (float[])Fld("specVis")) if (x > pico) pico = x;
        Check(pico <= 0.006f, "con un video en pantalla se apaga (no se congela con la ultima cancion)");

        // ---------- REACTIVIDAD: que se MUEVA con la musica, no que se quede en la forma del nivel ----------
        // Una banda que lleva rato sonando igual se asienta; en cuanto PEGA por encima de su promedio, sube.
        Array.Clear((float[])Fld("specVis"), 0, 8); Array.Clear((float[])Fld("specPiso"), 0, 8); Array.Clear((float[])Fld("specGolpe"), 0, 8);
        float[] now = (float[])Fld("specNow");
        for (int b = 0; b < 8; b++) now[b] = 0.05f;
        for (int i = 0; i < 200; i++) AVZ.Invoke(F, null);          // rato sonando parejo -> se asienta
        float asentado = ((float[])Fld("specVis"))[3];
        for (int b = 0; b < 8; b++) now[b] = 0.05f * 2.2f;          // GOLPE en todas las bandas
        AVZ.Invoke(F, null); AVZ.Invoke(F, null);
        float golpe = ((float[])Fld("specVis"))[3];
        for (int b = 0; b < 8; b++) now[b] = 0.05f;
        for (int i = 0; i < 25; i++) AVZ.Invoke(F, null);           // ~1 s despues del golpe
        float vuelta = ((float[])Fld("specVis"))[3];
        Console.WriteLine(string.Format("\n  REACTIVIDAD: sonando parejo {0:0.00}  ->  al pegar {1:0.00}  ->  un segundo despues {2:0.00}", asentado, golpe, vuelta));
        Check(golpe > asentado * 1.25f, "cuando la cancion PEGA, el fondo sube de verdad (no se queda en la forma del nivel)");
        Check(golpe - asentado > 0.10f, "y la subida se NOTA (mas de un 10% de la altura)");
        Check(vuelta < golpe - 0.05f, "y vuelve a bajar: no se queda arriba clavado");
        Check(asentado > 0.05f, "pero nunca desaparece: con la cancion sonando parejo sigue habiendo fondo");

        // ---------- SILUETA: que se distingan las BANDAS ----------
        // El fondo ya no son manchas de luz sumadas: es un relleno de color cuya altura sigue esta silueta.
        int cols = (int)Cte("AurCols"), filas = (int)Cte("AurFilas");
        float piso = Cte("AurPiso"), aBase = Cte("AurAlfaBase"), aPico = Cte("AurAlfaPico");

        // una banda sola: su bulto esta EN SU SITIO y no llega al centro de las vecinas
        bool enSuSitio = true, noSeTraga = true;
        for (int b = 0; b < 8; b++)
        {
            float[] B = new float[8]; B[b] = 1f;
            float mejor = -1f; int arg = 0;
            for (int i = 0; i <= 200; i++) { float v = Perfil(B, i/200f); if (v > mejor) { mejor = v; arg = i; } }
            float centro = b / 7f * 200f;
            if (Math.Abs(arg - centro) > 4f || mejor < 0.99f) enSuSitio = false;
            if (b > 0 && Perfil(B, (b-1)/7f) > 0.001f) noSeTraga = false;
            if (b < 7 && Perfil(B, (b+1)/7f) > 0.001f) noSeTraga = false;
        }
        Check(enSuSitio, "cada banda levanta el color EN SU SITIO y a su altura completa");
        Check(noSeTraga, "una banda a tope NO se traga a sus vecinas: en el centro de la de al lado ya no llega");

        // dos bandas iguales pegadas: entre las dos queda un VALLE visible (asi se distinguen)
        float[] dos = new float[8]; dos[3] = 1f; dos[4] = 1f;
        float valle = Perfil(dos, 3.5f/7f);
        Console.WriteLine(string.Format("\n  dos bandas iguales pegadas: en medio la silueta baja al {0:0}% de su alto", valle*100));
        Check(valle < 0.75f && valle > 0.25f, "entre dos bandas iguales queda un valle que se ve, sin llegar a partirse en dos");

        // los bordes de la pantalla estan cubiertos (la primera y la ultima banda llegan al borde)
        float[] todo = new float[8]; for (int b = 0; b < 8; b++) todo[b] = 0.8f;
        Check(Math.Abs(Perfil(todo, 0f) - 0.8f) < 0.001f && Math.Abs(Perfil(todo, 1f) - 0.8f) < 0.001f,
              "la silueta llega hasta los dos bordes de la pantalla (no se queda corta)");

        // La malla dibuja la silueta con RECTAS entre columna y columna. Aqui se mide cuanto se aparta esa
        // linea quebrada de la curva de verdad: si se aparta poco, en pantalla no se ven esquinas ni facetas.
        float[] pico8 = { 0.9f, 0.1f, 0.7f, 0.2f, 1f, 0.05f, 0.6f, 0.3f };
        float error = 0f;
        for (int i = 0; i < cols; i++)
        {
            float a0 = Perfil(pico8, (float)i/cols), a1 = Perfil(pico8, (float)(i+1)/cols);
            for (int k = 1; k < 8; k++)
            {
                float f = k / 8f;
                float recta = a0 + (a1-a0)*f;
                float real = Perfil(pico8, ((float)i + f)/cols);
                float d = Math.Abs(recta-real); if (d > error) error = d;
            }
        }
        // en pixeles de verdad: pantalla de 768 de alto, con el ajuste de fabrica (espectro=55)
        float altoPx = 768f * 1.02f * 0.55f;
        Console.WriteLine(string.Format("  la silueta se dibuja con {0} columnas y se aparta como mucho {1:0.0} pixeles de la curva real ({2:0.0}%)",
                                        cols, error*altoPx, error*100));
        Check(error * altoPx < 6f, "la silueta sale curva: la linea quebrada no se separa ni 6 pixeles de la curva de verdad");

        // DIBUJO de la silueta, para verla
        Console.WriteLine("\n  asi queda la silueta con un espectro cualquiera (# = relleno de color):\n");
        for (int fila = 9; fila >= 0; fila--)
        {
            string ln = "   ";
            for (int i = 0; i <= 72; i++)
            {
                float v = Perfil(pico8, i/72f);
                float alt = piso + (1f-piso)*v;                 // altura real: nunca baja del suelo
                ln += (alt*10f >= fila + 0.5f) ? "#" : " ";
            }
            Console.WriteLine(ln);
        }

        // ALTURA: crece y encoge de verdad (llena mas o menos pantalla)
        float altoFlojo = piso, altoPico = 1f;
        Console.WriteLine(string.Format("\n  el color ocupa del {0:0}% al {1:0}% de su alto: crece {2:0.0} veces entre lo flojo y el pico",
                                        altoFlojo*100, altoPico*100, altoPico/altoFlojo));
        Check(altoPico / altoFlojo >= 3f, "el relleno crece y encoge lo suficiente para que se note (mas de 3 veces)");
        Check(altoFlojo > 0.05f, "y nunca desaparece del todo: la parte de abajo siempre tiene color");

        // OPACIDAD a lo alto: cuerpo abajo (para que se le vea la FORMA) y desvanecido limpio arriba
        MethodInfo BAJA = TPn.GetMethod("AurBaja", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        float[] mA = new float[filas+1];
        for (int j = 0; j <= filas; j++) mA[j] = (float)BAJA.Invoke(null, new object[]{ (float)j/filas });
        Check(Math.Abs(mA[filas]) < 0.0001f, "arriba del todo el color llega a transparente: el borde se funde, no se corta");
        bool cae = true; for (int j = 1; j <= filas; j++) if (mA[j] > mA[j-1] - 0.0001f) cae = false;
        Check(cae, "de abajo hacia arriba el color se va desvaneciendo sin volver a subir");
        // El relleno LLENA la cortina. Antes se apagaba a media altura y la parte de arriba era una neblina;
        // con el filo de neon dibujando la silueta, ese hueco entre el filo y el color cantaba (se veian las
        // rayas y poco mas). Ahora aguanta hasta arriba y solo el ultimo tramo se apaga, para que el relleno
        // muera sin dejar canto por debajo del filo.
        Check((float)BAJA.Invoke(null, new object[]{ 0.35f }) > 0.6f, "la mitad de abajo conserva CUERPO: por eso se le ve la forma y no queda una neblina");
        Check((float)BAJA.Invoke(null, new object[]{ 0.50f }) > 0.70f, "a MEDIA altura sigue teniendo mas de tres cuartos del color: la cortina se ve llena, no vacia");
        Check((float)BAJA.Invoke(null, new object[]{ 0.75f }) > 0.40f, "y a tres cuartos, todavia mas de un tercio");
        Check((float)BAJA.Invoke(null, new object[]{ 0.97f }) < 0.06f, "el ultimo tramo si se apaga: el relleno no asoma por encima del filo");
        Check((float)BAJA.Invoke(null, new object[]{ 1.00f }) == 0f
              && (float)BAJA.Invoke(null, new object[]{ 0.995f }) < 0.012f,
              "y llega a cero SIN pendiente: por debajo del filo no queda ningun canto duro");
        float opacaPico = (aBase + aPico) , opacaFloja = aBase;
        Console.WriteLine(string.Format("  opacidad del borde de abajo: {0:0}% cuando afloja  ->  {1:0}% en el pico", opacaFloja*100, opacaPico*100));
        // En el pico el color casi tapa el cielo, y es a proposito: ahi abajo el cielo YA es el color del
        // genero apagado, asi que taparlo con el mismo color vivo es justo lo que hace que se note. Lo que no
        // puede es taparlo del todo, y ademas esto es EN REPOSO: en cuanto alguien toca una tecla la aurora
        // baja al 65% para cansar menos la vista, igual que las estrellas.
        Check(opacaPico <= 0.96f && opacaPico > 0.90f, "en el pico el color casi tapa el cielo (a proposito), pero nunca del todo");
        Check(opacaPico - opacaFloja > 0.25f, "y la diferencia entre flojo y fuerte tambien se ve en la opacidad");

        // ---------- SIN COSTURAS: del horizonte al borde de abajo, sin un solo corte ----------
        // La franja de estado (lo que suena, creditos, tiempo) no lleva fondo propio. Por debajo del horizonte
        // el color se hunde -de tono y de presencia- hasta quedarse EXACTAMENTE en el cielo al llegar al borde:
        // asi no hay donde ver una raya y ademas el texto queda sobre oscuro, que es donde se lee.
        MethodInfo MALLA = TPn.GetMethod("AurMalla", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        MethodInfo STONO = TPn.GetMethod("AurSueloTono", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        MethodInfo SALFA = TPn.GetMethod("AurSueloAlfa", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        MethodInfo RAYO = TPn.GetMethod("AurRayo", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        Check(MALLA != null && STONO != null && SALFA != null && RAYO != null, "la malla del fondo se puede armar y comprobar aparte");
        int suelos = (int)Cte("AurSuelo");
        float hondo = Cte("AurHondo");
        if (MALLA != null)
        {
            // el suelo: arranca EXACTAMENTE donde acaba la cortina y muere en el cielo
            Check(Math.Abs(SueTono(STONO, 0f) - 1f) < 0.0001f && Math.Abs(SueAlfa(SALFA, 0f) - 1f) < 0.0001f,
                  "en el horizonte el suelo empieza con el mismo color y la misma presencia que la cortina");
            Check(Math.Abs(SueAlfa(SALFA, 1f)) < 0.0001f, "y al llegar al borde de abajo ya es el cielo: por eso no hay raya que ver");
            bool bajaT = true, bajaA = true;
            for (int k = 1; k <= 20; k++)
            {
                if (SueTono(STONO, k/20f) > SueTono(STONO, (k-1)/20f) + 1e-6f) bajaT = false;
                if (SueAlfa(SALFA, k/20f) > SueAlfa(SALFA, (k-1)/20f) + 1e-6f) bajaA = false;
            }
            Check(bajaT && bajaA, "y entre medias solo se hunde: nunca vuelve a subir");
            Check(SueTono(STONO, 0.25f) < 0.75f && SueAlfa(SALFA, 0.25f) < 0.45f,
                  "y se hunde DEPRISA: al cuarto de la franja ya perdio la mitad del tono y mas de la mitad de la presencia");
            Check(Math.Abs(SueTono(STONO, 1f) - hondo) < 0.0001f, "el tono del suelo acaba hundido (" + (int)Math.Round(hondo*100) + "% del de la cortina)");

            Type TVtx = null;
            foreach (Type t in TPn.Assembly.GetTypes()) if (t.Name == "Vtx") TVtx = t;
            int puntos = cols + 1;
            float anchoP = 1400f, altoPanel = 585f, baseYp = altoPanel - 86f;   // pantalla ancha, como la rocola real
            float[] hC = new float[puntos], aC = new float[puntos], rC = new float[puntos];
            uint[] cCor = new uint[(filas+1)*puntos], cSu = new uint[(suelos+1)*puntos];
            float altoMax = altoPanel * 1.02f * 0.55f;
            for (int i = 0; i < puntos; i++)
            {
                float v = Perfil(pico8, (float)i/cols);
                hC[i] = altoMax * (piso + (1f-piso)*v);
                aC[i] = aBase + aPico*v;
                rC[i] = 1f;
                for (int k = 0; k <= filas; k++) cCor[k*puntos + i] = 0x00A050C0u;
                for (int k = 0; k <= suelos; k++) cSu[k*puntos + i] = 0x00A050C0u;
            }
            Array vs = Array.CreateInstance(TVtx, (filas+suelos)*cols*6);
            int nv = (int)MALLA.Invoke(null, new object[]{ vs, hC, cCor, cSu, aC, rC, anchoP, baseYp, altoPanel });
            Check(nv == (filas+suelos)*cols*6, "la malla sale completa: " + nv + " vertices");

            FieldInfo fX = TVtx.GetField("X"), fY = TVtx.GetField("Y"), fCol = TVtx.GetField("Color");
            float bajo = -1e9f, arriba = 1e9f;
            List<float> ys = new List<float>(); List<int> als = new List<int>();
            for (int k = 0; k < nv; k++)
            {
                object vx = vs.GetValue(k);
                float x = (float)fX.GetValue(vx) + 0.5f, y = (float)fY.GetValue(vx) + 0.5f;
                int a = (int)((uint)fCol.GetValue(vx) >> 24);
                if (y > bajo) bajo = y;
                if (y < arriba) arriba = y;
                if (Math.Abs(x) < 0.01f) { ys.Add(y); als.Add(a); }   // columna del borde izquierdo
            }
            Console.WriteLine(string.Format("\n  la malla va del pixel {0:0} al {1:0} (la pantalla acaba en {2:0}, el horizonte esta en {3:0})",
                                            arriba, bajo, altoPanel, baseYp));
            Check(Math.Abs(bajo - altoPanel) < 0.01f, "el fondo llega HASTA EL BORDE DE ABAJO de la pantalla");

            int aHoriz = -1, aBorde = -1, aArriba = 999; float yArr = 1e9f;
            for (int k = 0; k < ys.Count; k++)
            {
                if (Math.Abs(ys[k] - baseYp) < 0.01f) aHoriz = als[k];
                if (Math.Abs(ys[k] - altoPanel) < 0.01f) aBorde = als[k];
                if (ys[k] < yArr) { yArr = ys[k]; aArriba = als[k]; }
            }
            int aEsperado = (int)((aBase + aPico*Perfil(pico8, 0f))*255f + 0.5f);
            Check(aHoriz == aEsperado, "en el horizonte la cortina y el suelo llevan la MISMA opacidad: ni un salto");
            Check(aBorde == 0, "y en el borde de abajo el fondo ya se fundio con el cielo (no se corta: se acaba)");
            Check(aArriba == 0, "y la punta de arriba tambien es transparente del todo");
        }

        // ---------- LAS CORTINAS: el ondeo que le da aire de aurora ----------
        if (RAYO != null)
        {
            float mn = 9f, mx = -9f, acum = 0f; int n = 0;
            float saltoT = 0f;
            for (int k = 0; k <= cols; k++)
            {
                float r0 = Ray(RAYO, (float)k/cols, 0f), r1 = Ray(RAYO, (float)k/cols, 1f/24f);
                if (r0 < mn) mn = r0; if (r0 > mx) mx = r0;
                acum += r0; n++;
                float dt = Math.Abs(r1 - r0); if (dt > saltoT) saltoT = dt;
            }
            Console.WriteLine(string.Format("\n  ONDEO de las cortinas: va de {0:0.00} a {1:0.00} (media {2:0.00})", mn, mx, acum/n));
            Check(mn > 0.5f && mx < 1.5f, "el ondeo nunca apaga ni dispara una veta: se queda dentro de su margen");
            Check(Math.Abs(acum/n - 1f) < 0.08f, "y de media no cambia el tamano de la cortina, solo la ondula");
            Check(mx - mn > 0.4f, "pero se NOTA: entre la veta mas alta y la mas baja hay mas de un 40%");
            // igual que con la silueta: lo que importa no es el escalon entre columnas sino cuanto se aparta
            // la linea quebrada de la onda de verdad. Si se aparta poco, en pantalla se ve curva.
            float errR = 0f;
            for (int k = 0; k < cols; k++)
            {
                float r0 = Ray(RAYO, (float)k/cols, 0f), r1 = Ray(RAYO, (float)(k+1)/cols, 0f);
                for (int q = 1; q < 8; q++)
                {
                    float u2 = q/8f;
                    float d = Math.Abs((r0 + (r1-r0)*u2) - Ray(RAYO, ((float)k+u2)/cols, 0f));
                    if (d > errR) errR = d;
                }
            }
            Console.WriteLine(string.Format("  el dibujo de la onda se aparta como mucho {0:0.0} pixeles de la curva real; entre dos cuadros seguidos se mueve {1:0.000}", errR*altoPx, saltoT));
            Check(errR * altoPx < 4f, "las vetas salen curvas: la linea quebrada no se separa ni 4 pixeles de la onda de verdad");
            Check(saltoT < 0.03f, "y de un cuadro al siguiente se mueve despacio: ondea, no parpadea");
            // y de verdad SE MUEVE con el tiempo
            float mov = 0f;
            for (int k = 0; k <= cols; k++) { float d = Math.Abs(Ray(RAYO, (float)k/cols, 4f) - Ray(RAYO, (float)k/cols, 0f)); if (d > mov) mov = d; }
            Check(mov > 0.15f, "en cuatro segundos la cortina se ha movido de verdad (no es un dibujo fijo)");

            // El ondeo tiene que llegar tambien al BRILLO, y NO en el horizonte (ahi va el texto de la franja)
            MethodInfo RAMPA = TPn.GetMethod("AurRampa", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            Check(RAMPA != null && (float)RAMPA.Invoke(null, new object[]{ 0f }) == 0f,
                  "en el horizonte el ondeo no toca el brillo: el texto de la franja no parpadea");
            Check(RAMPA != null && (float)RAMPA.Invoke(null, new object[]{ 0.5f }) >= 0.999f,
                  "y de media cortina para arriba se ve entero: ahi es donde se leen las vetas");
            bool subeR = true;
            for (int k = 1; k <= 20; k++)
                if ((float)RAMPA.Invoke(null, new object[]{ k/20f }) < (float)RAMPA.Invoke(null, new object[]{ (k-1)/20f }) - 1e-5f) subeR = false;
            Check(subeR, "y va entrando poco a poco segun sube: nunca de golpe");
        }

        // ---------- EL DEGRADADO DE COLOR NO SE ENSUCIA A MEDIA ALTURA ----------
        // Mezclar dos colores canal a canal atraviesa el centro de la rueda: a media altura el color pierde
        // fuerza y sale gris sucio. Por eso la mezcla se hace girando el tono por el borde (AurTonoMezcla).
        MethodInfo TMEZ = TPn.GetMethod("AurTonoMezcla", BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        Check(TMEZ != null, "la mezcla de tonos existe y se puede comprobar aparte");
        if (TMEZ != null)
        {
            uint[] pares = { 0x00E0A030u, 0x00E03080u,   // naranja -> rosa (el caso feo: por el centro sale marron)
                             0x00B040E0u, 0x00E040A0u,   // morado  -> rosa
                             0x0040A0E0u, 0x008040E0u }; // azul    -> violeta
            string[] nom = { "naranja a rosa", "morado a rosa", "azul a violeta" };
            bool limpio = true, extremos = true;
            for (int p = 0; p < 3; p++)
            {
                uint c0 = pares[p*2], c1 = pares[p*2+1];
                float s0 = Sat(c0), s1 = Sat(c1), peorS = 9f; float fPeor = 0f;
                for (int k = 0; k <= 20; k++)
                {
                    uint c = (uint)TMEZ.Invoke(null, new object[]{ c0, c1, k/20f });
                    float ss = Sat(c);
                    if (ss < peorS) { peorS = ss; fPeor = k/20f; }
                    // y la mezcla a lo bruto, para comparar
                }
                float peorBruto = 9f;
                for (int k = 0; k <= 20; k++)
                {
                    float f = k/20f;
                    uint c = Bruto(c0, c1, f);
                    float ss = Sat(c); if (ss < peorBruto) peorBruto = ss;
                }
                Console.WriteLine(string.Format("     {0,-16} viveza minima por el borde {1:0.00} (en {2:0.00})   |   canal a canal {3:0.00}",
                                                nom[p], peorS, fPeor, peorBruto));
                if (peorS < Math.Min(s0, s1) * 0.90f) limpio = false;
                uint ini = (uint)TMEZ.Invoke(null, new object[]{ c0, c1, 0f });
                uint fin = (uint)TMEZ.Invoke(null, new object[]{ c0, c1, 1f });
                if (ini != c0 || fin != c1) extremos = false;
            }
            Console.WriteLine("");
            Check(limpio, "a media altura el color NO pierde viveza: sigue siendo un color, no un gris sucio");
            Check(extremos, "y en los extremos da exactamente el color del pie y el de la punta");
        }

        // ---------- QUE EL TEXTO DE LA FRANJA SE SIGA LEYENDO ----------
        // El texto va sobre el SUELO de la aurora, donde el color ya se hundio. Se mide el contraste como lo
        // mide la norma (WCAG) en el peor caso: un golpe a tope, en la posicion real de cada linea.
        if (STONO != null)
        {
            float[] vivo = { 185f, 74f, 235f };       // el morado del genero a brillo pleno
            float[] cielo = { 82f, 46f, 128f };       // el cielo de abajo con ese genero
            float aMax = aBase + aPico;
            int franja = 96;                          // alto tipico de la franja de estado
            string[] qn = { "lo que suena", "siguiente", "tiempo / creditos" };
            int[] pos = { 22, 48, 74 };               // a que altura de la franja cae cada linea
            string[] cn = { "verde", "azul claro", "ambar" };
            float[][] txt = { new float[]{125,235,165}, new float[]{150,200,255}, new float[]{255,205,90} };
            Console.WriteLine("\n  con el fondo a tope, contraste del texto de la franja:");
            bool legible = true;
            for (int k = 0; k < pos.Length; k++)
            {
                float fs = pos[k] / (float)franja;
                // como lo interpola la tarjeta: por tramos rectos entre los nudos de las filas del suelo
                float xk = fs * suelos; int j2 = (int)xk; if (j2 > suelos-1) j2 = suelos-1;
                float u = xk - j2;
                float[] c0 = Tono(vivo[0], vivo[1], vivo[2], SueTono(STONO, (float)j2/suelos));
                float[] c1 = Tono(vivo[0], vivo[1], vivo[2], SueTono(STONO, (float)(j2+1)/suelos));
                float a = aMax * (SueAlfa(SALFA, (float)j2/suelos)*(1f-u) + SueAlfa(SALFA, (float)(j2+1)/suelos)*u);
                float[] bg = new float[3];
                for (int q = 0; q < 3; q++) { float col = c0[q] + (c1[q]-c0[q])*u; bg[q] = cielo[q] + (col-cielo[q])*a; }
                float ct = Contraste(txt[k], bg);
                Console.WriteLine(string.Format("     {0,-20} ({1,-10}) {2:0.0} a 1", qn[k], cn[k], ct));
                if (ct < 4f) legible = false;
            }
            Check(legible, "todo el texto de la franja se lee bien (4 a 1 o mas) aunque la aurora este a tope");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

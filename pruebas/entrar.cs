using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// EL QUE NO SABE USAR LA ROCOLA TAMBIEN TIENE QUE PODER PONER SU CANCION.
//
// En el bar la rocola la usa gente que no la ha visto nunca, de noche, con prisa y con los amigos mirando.
// Que las instrucciones estuvieran escritas arriba no bastaba: nadie las lee. Habia dos agujeros gordos.
//
//   1) BUSCAR POR NOMBRE -que es la forma MAS RAPIDA de poner una cancion cuando ya sabes cual quieres- se
//      abria manteniendo el ROJO y apretando el VERDE. Un combo. No estaba escrito en ninguna parte, no
//      habia forma de adivinarlo, y el que llegaba nuevo no se pone a probar parejas de botones. O sea que
//      la funcion mas util del programa era la unica que nadie usaba. Y encima chocaba: el VERDE es el
//      boton de PONER, asi que al que lo descubria por casualidad le sonaba a que iba a poner algo.
//      Ahora hay una CASILLA grande arriba EN MEDIO, con una lupa, que parpadea en azul para que se vea
//      desde la mesa, y con los dos botones que hay que apretar DIBUJADOS al lado: el rojo, un mas, y la
//      flecha de arriba. Y el combo es ROJO + ARRIBA, que no choca con nada.
//
//   2) El que se queda parado sin saber que hacer no tenia a quien preguntar. Ahora, si pasan unos segundos
//      sin que apriete nada, aparece abajo UNA frase -una, la del siguiente paso- con el boton que tiene
//      que apretar dibujado al lado. En cuanto aprieta cualquier cosa, desaparece. Al que ya sabe usarla no
//      le estorba nunca, porque el que sabe no duda: aprieta.
//
// Aqui se comprueban las dos cosas: que la casilla existe, se ve, va en medio y ensena los dos botones de
// verdad; que el combo es ROJO+ARRIBA y ya NO ROJO+VERDE; que dentro del buscador estan los avisos nuevos
// (cerrar con los mismos dos botones, y el rojo dibujado en la tecla de BORRAR, que hace lo mismo); y que
// la guia dice lo que toca en cada momento, aparece sola al dudar y se quita al primer boton.
class Entrar
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static FieldInfo Campo(string n) { return TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic); }
    static MethodInfo Met(string n) { return TF.GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic); }
    static object F;
    static void Set(string n, object v) { Campo(n).SetValue(F, v); }

    // ---- lectura del ejecutable ya compilado: que llamadas hace un metodo, en orden ----
    static int LargoOperando(int op)
    {
        if (op >= 0x2B && op <= 0x37) return 1;          // saltos cortos
        if (op >= 0x38 && op <= 0x44) return 4;          // saltos largos
        switch (op)
        {
            case 0x0E: case 0x10: case 0x11: case 0x12: case 0x13:
            case 0x1F: case 0xDE: return 1;
            case 0x20: case 0x22: case 0x27: case 0x28: case 0x6F: case 0x70: case 0x71:
            case 0x72: case 0x73: case 0x74: case 0x75: case 0x79:
            case 0x7B: case 0x7C: case 0x7D: case 0x7E: case 0x7F: case 0x80:
            case 0x81: case 0x8C: case 0x8D: case 0x8F: case 0xA3: case 0xA4:
            case 0xA5: case 0xC2: case 0xC6: case 0xD0: case 0x29: return 4;
            case 0x21: case 0x23: return 8;
            default: return 0;
        }
    }
    // Las instrucciones de dos bytes (las que empiezan por 0xFE) tambien llevan operando: sin esto el
    // recorrido se desincroniza a la primera y a partir de ahi lee basura.
    static int Largo2(int op2)
    {
        switch (op2)
        {
            case 0x06: case 0x07: case 0x15: case 0x16: case 0x1C: return 4;
            case 0x09: case 0x0A: case 0x0B: case 0x0C: case 0x0D: case 0x0E: return 2;
            case 0x12: case 0x19: return 1;
            default: return 0;
        }
    }
    // Los nombres, en orden, de todo lo que el metodo llama o lee. Sirve para preguntarle al ejecutable
    // "¿que miras justo antes de abrir el buscador?" sin tener que apretar botones de verdad.
    static List<string> Pasos(MethodBase m)
    {
        List<string> r = new List<string>();
        MethodBody b = m.GetMethodBody(); if (b == null) return r;
        byte[] il = b.GetILAsByteArray(); if (il == null) return r;
        Module mod = m.Module; int i = 0;
        while (i < il.Length)
        {
            int op = il[i];
            if (op == 0xFE) { if (i + 1 >= il.Length) break; i += 2 + Largo2(il[i + 1]); continue; }
            if (op == 0x45) { int n = BitConverter.ToInt32(il, i + 1); i += 1 + 4 + 4 * n; continue; }
            int len = LargoOperando(op);
            if ((op == 0x28 || op == 0x6F) && i + 4 < il.Length)          // call / callvirt
            { try { r.Add(mod.ResolveMethod(BitConverter.ToInt32(il, i + 1)).Name); } catch { } }
            else if ((op == 0x7B || op == 0x7E) && i + 4 < il.Length)     // ldfld / ldsfld
            { try { r.Add("$" + mod.ResolveField(BitConverter.ToInt32(il, i + 1)).Name); } catch { } }
            i += 1 + len;
        }
        return r;
    }
    static int Indice(List<string> l, string q) { for (int i = 0; i < l.Count; i++) if (l[i] == q) return i; return -1; }
    // ¿Sale 'que' entre las N cosas que se miran justo antes de 'donde'? (o sea: ¿es su condicion?)
    static bool JustoAntes(List<string> l, string donde, string que, int cuantos)
    {
        int i = Indice(l, donde); if (i < 0) return false;
        for (int k = i - 1; k >= 0 && k >= i - cuantos; k--) if (l[k] == que) return true;
        return false;
    }

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    // ---- contar pixeles de un color en un trozo de lo dibujado ----
    static int Rojos(Bitmap b, int x0, int x1)
    {
        int n = 0;
        for (int y = 0; y < b.Height; y++)
            for (int x = Math.Max(0, x0); x < Math.Min(b.Width, x1); x++)
            { Color c = b.GetPixel(x, y); if (c.R > 140 && c.G < 110 && c.B < 110) n++; }
        return n;
    }
    static int Claros(Bitmap b, int x0, int x1)
    {
        int n = 0;
        for (int y = 0; y < b.Height; y++)
            for (int x = Math.Max(0, x0); x < Math.Min(b.Width, x1); x++)
            { Color c = b.GetPixel(x, y); if (c.R > 170 && c.G > 180 && c.B > 190) n++; }
        return n;
    }
    static long Azul(Bitmap b)
    {
        long s = 0;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            { Color c = b.GetPixel(x, y); if (c.B > c.R) s += c.B - c.R; }
        return s;
    }
    static Bitmap Pinta(Type T, object v, int w, int h, MethodInfo mRender)
    {
        ((Control)v).SetBounds(0, 0, w, h);
        Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(255, 8, 10, 24));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            mRender.Invoke(v, new object[] { g, false });
        }
        return b;
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);

        // =========================================================================================
        Console.WriteLine("\n  1) LA CASILLA DE BUSCAR EXISTE Y SE VE DESDE LA MESA\n");
        Type TBus = Nes("BuscaView");
        Check(TBus != null, "hay una casilla de BUSCAR (antes no habia nada: el buscador era invisible)");
        Check(Campo("buscaView") != null, "y la rocola la tiene puesta en la pantalla");
        if (TBus == null || Campo("buscaView") == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        MethodInfo mAncho = TBus.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo mBusRender = TBus.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
        int filaH = 44;                                   // el alto de la fila de arriba a 1024x768
        int an = (int)mAncho.Invoke(null, new object[] { filaH });
        Console.WriteLine("     con la fila de arriba a " + filaH + " px de alto, la casilla mide " + an + " px de ancha");
        Check(an >= filaH * 5, "es GRANDE: una casilla ancha que se ve desde la mesa, no un iconito escondido");
        Check(an <= filaH * 12, "pero no tanto como para comerse el encabezado de la derecha");

        object bv = Activator.CreateInstance(TBus, true);
        ((Control)bv).Font = new Font(Fam(), 9f, FontStyle.Bold);
        FieldInfo fPhase = TBus.GetField("Phase"), fAbierto = TBus.GetField("Abierto");

        // =========================================================================================
        Console.WriteLine("\n  2) Y ENSENA LOS DOS BOTONES QUE HAY QUE APRETAR, DIBUJADOS\n");
        fPhase.SetValue(bv, Math.PI / 4.8);   // sin(Phase*2.4) = 1 -> encendida del todo
        Bitmap luz = Pinta(TBus, bv, an, filaH, mBusRender);
        luz.Save("busca_casilla.png", ImageFormat.Png);
        int izq = Claros(luz, 0, an / 5);                 // la lupa
        int rojoDer = Rojos(luz, an / 2, an);             // el punto rojo y la tecla de la flecha
        int rojoIzq = Rojos(luz, 0, an / 3);
        Console.WriteLine("     lupa (parte izquierda): " + izq + " px claros     botones (mitad derecha): " + rojoDer + " px rojos");
        Check(izq > 20, "a la izquierda esta la LUPA: se entiende que es para buscar sin leer nada");
        Check(rojoDer > 60, "y a la derecha estan los dos botones ROJOS que hay que apretar");
        Check(rojoIzq < 10, "el rojo esta solo en los botones, no manchando la casilla entera");
        // Los dos botones tienen que estar SEPARADOS (rojo, luego el mas, luego la flecha): si fuera uno
        // solo el cliente apretaria uno y no pasaria nada.
        int corte = an / 2 + (an / 2) / 2;
        int r1 = Rojos(luz, an / 2, corte), r2 = Rojos(luz, corte, an);
        Console.WriteLine("     y son DOS: " + r1 + " px rojos en el primero y " + r2 + " en el segundo");
        Check(r1 > 20 && r2 > 20, "son DOS botones separados (el rojo y la flecha), no un borron");

        // =========================================================================================
        Console.WriteLine("\n  3) PARPADEA PARA QUE LA MIREN, Y SE ESTA QUIETA CUANDO YA CUMPLIO\n");
        fPhase.SetValue(bv, Math.PI / 4.8 + Math.PI / 2.4);   // media vuelta despues: apagada
        Bitmap oscura = Pinta(TBus, bv, an, filaH, mBusRender);
        long aLuz = Azul(luz), aOsc = Azul(oscura);
        Console.WriteLine("     azul encendida: " + aLuz + "     azul apagada: " + aOsc
                          + "   (" + (aOsc > 0 ? (100 * (aLuz - aOsc) / aOsc) : 0) + "% mas)");
        Check(aLuz > aOsc * 12 / 10, "parpadea de verdad: encendida tiene bastante mas azul que apagada");
        fAbierto.SetValue(bv, true);
        Bitmap conBus = Pinta(TBus, bv, an, filaH, mBusRender);
        Check(Math.Abs(Azul(conBus) - aLuz) * 20 < aLuz, "con el buscador YA abierto se queda encendida fija (parpadear alli distraeria)");
        fAbierto.SetValue(bv, false);
        // Con tarjeta grafica el parpadeo lo pone la tarjeta como halo aparte: la casilla dice donde va.
        MethodInfo mLat = TBus.GetMethod("GetLatido");
        Check(mLat != null, "y con tarjeta grafica el parpadeo lo pone ella, sin rehacer la pantalla entera");
        if (mLat != null)
        {
            object[] arg = new object[] { 0f, 0f, 0f };
            bool hay = (bool)mLat.Invoke(bv, arg);
            Check(hay && (float)arg[2] > 10f, "con el buscador cerrado pide su halo, y del tamano de la casilla");
            fAbierto.SetValue(bv, true);
            object[] arg2 = new object[] { 0f, 0f, 0f };
            Check(!(bool)mLat.Invoke(bv, arg2), "y con el buscador abierto ya no lo pide");
            fAbierto.SetValue(bv, false);
        }

        // =========================================================================================
        Console.WriteLine("\n  4) VA ARRIBA EN MEDIO, QUE ES DONDE EL OJO LA BUSCA\n");
        Type THelp = Nes("HelpBar");
        MethodInfo mSitio = TF.GetMethod("SitioBusca", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mSitio != null, "el sitio de la casilla lo decide una sola cuenta");
        if (mSitio != null)
        {
            int[] anchos = { 1024, 1366, 1920, 2560, 3440 };
            bool centradaSiempre = true, dentroSiempre = true;
            foreach (int W in anchos)
            {
                int pad = 16, der = W - pad;
                int ayudaW = (int)THelp.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { 44 });
                int minX = pad + Math.Max(120, Math.Min(ayudaW, der - pad)) + 12;
                int bw = Math.Min(an, Math.Max(0, der - minX));
                int x = (int)mSitio.Invoke(null, new object[] { W, bw, minX, der });
                int centro = x + bw / 2, desvio = Math.Abs(centro - W / 2);
                Console.WriteLine("     " + W + " px de ancho -> la casilla queda a " + desvio + " px del centro");
                if (desvio > 2) centradaSiempre = false;
                if (x < minX || x + bw > der) dentroSiempre = false;
            }
            Check(centradaSiempre, "en toda pantalla normal queda EN EL MEDIO, clavada");
            Check(dentroSiempre, "y nunca pisa las instrucciones ni se sale de la barra");
            // Pantalla angosta: se corre pero no se mete debajo de las instrucciones.
            int xx = (int)mSitio.Invoke(null, new object[] { 800, 300, 420, 700 });
            Check(xx >= 420, "en una pantalla angosta se corre a la derecha, nunca encima de las instrucciones");
        }

        // =========================================================================================
        Console.WriteLine("\n  5) EL COMBO ES ROJO + ARRIBA, Y YA NO ROJO + VERDE\n");
        MethodInfo mCmd = TF.GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
        List<string> pasos = Pasos(mCmd);
        Check(Indice(pasos, "ToggleSearch") >= 0, "al apretar los dos botones se abre/cierra el buscador");
        Check(JustoAntes(pasos, "ToggleSearch", "IsUp", 6), "y los dos botones son el ROJO y el de ARRIBA");
        Check(JustoAntes(pasos, "ToggleSearch", "BackHeld", 6), "con el ROJO MANTENIDO (por eso no se abre solo al moverse)");
        Check(!JustoAntes(pasos, "ToggleSearch", "IsOK", 6),
              "y ya NO es el VERDE: el verde es PONER, y usarlo para salir ponia canciones sin querer");

        // Los simbolos viejos de "asi se entra al buscador", fuera de la barra de instrucciones.
        Check(THelp.GetField("FVolver", BindingFlags.Static | BindingFlags.Public) != null, "la barra de arriba sigue teniendo sus tres avisos");
        Check(THelp.GetField("FBuscar", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null,
              "y ya no lleva el aviso viejo de BUSCAR: eso ahora lo dice la casilla, mucho mas claro");

        // =========================================================================================
        Console.WriteLine("\n  6) DENTRO DEL BUSCADOR, LOS MISMOS DOS BOTONES PARA SALIR\n");
        Type TSO = Nes("SearchOverlay");
        MethodInfo mFlecha = TSO.GetMethod("FlechaArriba", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mFlecha != null, "el aviso de CERRAR dibuja la flecha de arriba");
        List<string> pRender = Pasos(TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) }));
        Check(Indice(pRender, "FlechaArriba") >= 0, "y esa flecha se dibuja de verdad al pintar el buscador");
        List<string> pCmd = Pasos(mCmd);
        Check(JustoAntes(pCmd, "FlashClose", "IsUp", 6), "al apretarlos, el aviso de CERRAR se enciende (asi el cliente ve que acerto)");

        // El boton rojo DENTRO de la tecla de BORRAR: el rojo borra letras, pero eso era un atajo invisible.
        MethodInfo mTecla = TSO.GetMethod("DrawOneKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mTecla != null, "las teclas del buscador se dibujan una a una");
        if (mTecla != null)
        {
            List<string> pk = Pasos(mTecla);
            Check(Indice(pk, "CtrlDot") >= 0, "y en la de BORRAR va dibujado el boton ROJO, que hace lo mismo");
        }

        // =========================================================================================
        Console.WriteLine("\n  7) LA GUIA APARECE SOLA CUANDO EL CLIENTE SE QUEDA PARADO\n");
        Type TGui = Nes("GuiaView");
        Check(TGui != null, "hay una frase de ayuda que aparece sola");
        MethodInfo mToca = Met("GuiaToca"), mTexto = Met("GuiaTexto"), mTick = Met("TickGuia");
        Check(mToca != null && mTexto != null && mTick != null, "y la rocola decide sola cuando sale y que dice");
        if (TGui == null || mToca == null || mTexto == null || mTick == null) { Console.WriteLine("\n  FALLOS: " + fallos); return fallos; }

        F = FormatterServices.GetUninitializedObject(TF);
        int duda = (int)TF.GetField("GuiaDudaMs", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        Console.WriteLine("     se espera " + duda + " ms sin apretar nada antes de dar por hecho que esta dudando");
        Check(duda >= 2000 && duda <= 8000, "espera unos segundos: ni salta encima del que sabe ni llega tarde");

        Set("lastInputTick", Environment.TickCount);
        Check(!(bool)mToca.Invoke(F, null), "recien apretado un boton NO sale (al que sabe no le estorba)");
        Set("lastInputTick", Environment.TickCount - (duda - 400));
        Check(!(bool)mToca.Invoke(F, null), "casi al limite todavia no sale");
        Set("lastInputTick", Environment.TickCount - (duda + 500));
        Check((bool)mToca.Invoke(F, null), "y pasados los segundos sin tocar nada, aparece");

        Set("attractOn", true); Check(!(bool)mToca.Invoke(F, null), "en reposo NO sale: no hay nadie delante"); Set("attractOn", false);
        Set("videoFull", true); Check(!(bool)mToca.Invoke(F, null), "con un video a pantalla completa tampoco: se estaria mirando el video"); Set("videoFull", false);
        Set("libBuilding", true); Check(!(bool)mToca.Invoke(F, null), "ni mientras carga la biblioteca: aun no hay nada que elegir"); Set("libBuilding", false);
        Label cfg = new Label(); Set("lblConfig", cfg);
        Check(!(bool)mToca.Invoke(F, null), "ni con los ajustes abiertos (ahi esta el encargado, no un cliente)");
        cfg.Visible = false; Set("lblConfig", null);
        Check((bool)mToca.Invoke(F, null), "y fuera de esos casos, sale");

        // =========================================================================================
        Console.WriteLine("\n  8) Y DICE EL SIGUIENTE PASO, UNO SOLO, EL DE AHORA MISMO\n");
        object so = Activator.CreateInstance(TSO, true);
        object psongs = Activator.CreateInstance(Nes("BgPanel"), true);
        ((Control)psongs).Visible = false;
        Set("searchOverlay", so); Set("panelSongs", psongs); Set("searchOpen", false);

        Set("credits", 0);
        object[] a1 = new object[1];
        string t1 = (string)mTexto.Invoke(F, a1);
        Console.WriteLine("     sin creditos:            \"" + t1 + "\"  (+ boton " + a1[0] + ")");
        Check((int)a1[0] == 2, "sin creditos lo primero es la MONEDA, dibujada: sin pagar no se puede hacer nada");
        Check(t1.IndexOf("moneda", StringComparison.OrdinalIgnoreCase) >= 0, "y lo dice con esa palabra");

        // Aunque este en el buscador: si no hay credito, lo que toca sigue siendo la moneda.
        Set("searchOpen", true);
        object[] a0 = new object[1]; mTexto.Invoke(F, a0);
        Check((int)a0[0] == 2, "y manda sobre todo lo demas: dentro del buscador y sin credito, sigue siendo la moneda");

        Set("credits", 3);
        object[] a2 = new object[1];
        string t2 = (string)mTexto.Invoke(F, a2);
        Console.WriteLine("     escribiendo el nombre:   \"" + t2 + "\"  (+ boton " + a2[0] + ")");
        Check((int)a2[0] == 1 && t2.IndexOf("Escribe", StringComparison.OrdinalIgnoreCase) >= 0, "en el buscador dice que escriba y apriete el VERDE");

        TSO.GetField("FocusResults").SetValue(so, true);
        object[] a3 = new object[1];
        string t3 = (string)mTexto.Invoke(F, a3);
        Console.WriteLine("     ya sobre un resultado:   \"" + t3 + "\"  (+ boton " + a3[0] + ")");
        Check((int)a3[0] == 1 && t3.IndexOf("poner", StringComparison.OrdinalIgnoreCase) >= 0, "y ya sobre una cancion, que apriete el VERDE para ponerla");

        Set("searchOpen", false);
        object[] a4 = new object[1];
        string t4 = (string)mTexto.Invoke(F, a4);
        Console.WriteLine("     en la pared de discos:   \"" + t4 + "\"  (+ boton " + a4[0] + ")");
        Check((int)a4[0] == 1 && t4.IndexOf("disco", StringComparison.OrdinalIgnoreCase) >= 0, "en la pared de discos, que se mueva hasta uno y apriete el VERDE");

        ((Control)psongs).Visible = true;
        object[] a5 = new object[1];
        string t5 = (string)mTexto.Invoke(F, a5);
        Console.WriteLine("     dentro de un disco:      \"" + t5 + "\"  (+ boton " + a5[0] + ")");
        Check((int)a5[0] == 1 && t5.IndexOf("poner", StringComparison.OrdinalIgnoreCase) >= 0, "y dentro de un disco, que apriete el VERDE para poner la cancion");
        ((Control)psongs).Visible = false;

        // Una frase, no un manual: cortas y con un solo boton dibujado.
        string[] todas = { t1, t2, t3, t4, t5 };
        int larga = 0; foreach (string t in todas) if (t.Length > larga) larga = t.Length;
        Console.WriteLine("     la mas larga tiene " + larga + " letras");
        Check(larga <= 46, "son frases de una linea, no un manual de instrucciones");

        // =========================================================================================
        Console.WriteLine("\n  9) SALE ABAJO EN MEDIO, SIN TAPAR LOS DISCOS, Y SE QUITA AL PRIMER BOTON\n");
        object gv = Activator.CreateInstance(TGui, true);
        ((Control)gv).Font = new Font(Fam(), 9f, FontStyle.Bold);
        Set("guiaView", gv);
        Label lnow = new Label(); lnow.SetBounds(0, 690, 400, 24); Set("lblNow", lnow); Set("lblNowTop", null);
        Set("credits", 3); Set("lastInputTick", Environment.TickCount - (duda + 500));
        MethodInfo mLay = TF.GetMethod("LayoutGuia", BindingFlags.Instance | BindingFlags.NonPublic,
                                       null, new Type[] { typeof(int), typeof(int) }, null);
        Check(mLay != null, "la guia se coloca sola en la pantalla");

        TGui.GetField("Texto").SetValue(gv, "");
        mTick.Invoke(F, null);
        string dice = (string)TGui.GetField("Texto").GetValue(gv);
        Check(dice != null && dice.Length > 0, "tras unos segundos parado, la rocola saca su frase: \"" + dice + "\"");
        Check((int)TGui.GetField("Boton").GetValue(gv) == 1, "con el boton que hay que apretar, dibujado al lado");

        if (mLay != null)
        {
            foreach (int[] pant in new int[][] { new int[] { 1024, 768 }, new int[] { 1920, 1080 }, new int[] { 3440, 1440 } })
            {
                ((Control)gv).Visible = true;
                lnow.SetBounds(0, pant[1] - 78, 400, 24);
                mLay.Invoke(F, new object[] { pant[0], pant[1] });
                Control c = (Control)gv;
                int desvio = Math.Abs((c.Left + c.Width / 2) - pant[0] / 2);
                int franja = pant[1] - 88;
                Console.WriteLine("     " + pant[0] + "x" + pant[1] + " -> " + c.Width + "x" + c.Height
                                  + " a " + desvio + " px del centro, y acaba " + (franja - c.Bottom) + " px por encima de la franja");
                Check(desvio <= 2, "en " + pant[0] + "x" + pant[1] + " sale centrada");
                Check(c.Bottom <= franja, "y por encima de la franja de lo que suena, sin taparla");
                Check(c.Height >= 30 && c.Height * 6 < pant[1], "de un tamano que se lee sin comerse la pantalla");
                Check(c.Width * 10 < pant[0] * 9, "y sin ocupar la pantalla de lado a lado");
            }
            // Pantalla donde la frase no cabe entera: antes de sacarla cortada, no la saca.
            ((Control)gv).Visible = true;
            mLay.Invoke(F, new object[] { 400, 400 });
            Check(!((Control)gv).Visible, "y donde la frase no cabe entera, prefiere no sacarla a sacarla cortada");
        }

        // Al primer boton, fuera.
        ((Control)gv).Visible = true;
        Set("lastInputTick", Environment.TickCount);
        mTick.Invoke(F, null);
        Check(!((Control)gv).Visible, "y en cuanto el cliente aprieta cualquier boton, desaparece");

        // Que se dibuje: no basta con existir.
        MethodInfo mGuiRender = TGui.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
        TGui.GetField("Texto").SetValue(gv, "Mete una moneda para poner tu cancion");
        TGui.GetField("Boton").SetValue(gv, 2);
        int anG = (int)TGui.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public).Invoke(
                      null, new object[] { "Mete una moneda para poner tu cancion", 2, 40, Fam() });
        Bitmap bg = Pinta(TGui, gv, anG, 40, mGuiRender);
        bg.Save("guia_frase.png", ImageFormat.Png);
        Check(Claros(bg, 0, anG) > 100, "la frase se lee (letra clara y grande sobre fondo oscuro)");
        int oro = 0;
        for (int y = 0; y < bg.Height; y++) for (int x = anG / 2; x < anG; x++)
        { Color c = bg.GetPixel(x, y); if (c.R > 170 && c.G > 120 && c.G < 215 && c.B < 130) oro++; }
        Console.WriteLine("     la moneda dibujada ocupa " + oro + " px dorados");
        Check(oro > 60, "y la MONEDA va dibujada al lado: se entiende sin saber leer");

        // =========================================================================================
        Console.WriteLine("\n  10) CON TARJETA GRAFICA TAMBIEN SE VE\n");
        MethodInfo mSig = Met("UiBotSig");
        if (mSig == null) Console.WriteLine("     (este ejecutable es el de sin tarjeta grafica: esta parte no aplica)");
        else
        {
            // La pantalla por tarjeta solo se rehace cuando su "firma" cambia. Si la guia no entrara en la
            // firma, apareceria en la version normal y NO en la de tarjeta grafica: se veria en las pruebas
            // y no en el bar.
            Set("attractOn", false);
            ((Control)gv).Visible = false;
            long sinGuia = (long)mSig.Invoke(F, null);
            ((Control)gv).Visible = true;
            ((Control)gv).SetBounds(100, 600, 400, 40);
            TGui.GetField("Texto").SetValue(gv, "Mete una moneda para poner tu cancion");
            long conGuia = (long)mSig.Invoke(F, null);
            TGui.GetField("Texto").SetValue(gv, "Muevete hasta un disco y aprieta el VERDE");
            long otraFrase = (long)mSig.Invoke(F, null);
            Console.WriteLine("     firma sin guia: " + sinGuia + "     con guia: " + conGuia + "     con otra frase: " + otraFrase);
            Check(sinGuia != conGuia, "al aparecer la guia, la pantalla por tarjeta se entera y se rehace");
            Check(conGuia != otraFrase, "y al cambiar la frase, tambien");
            List<string> pTira = Pasos(Met("EnsureUiBotOverlay"));
            Check(Indice(pTira, "$guiaView") >= 0, "la tira de abajo la lleva dibujada de verdad");
            List<string> pSig = Pasos(Met("UiSig"));
            Check(Indice(pSig, "$buscaView") >= 0, "y la casilla de BUSCAR tambien entra en la pantalla por tarjeta");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

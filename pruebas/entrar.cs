using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
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
//   2) Y dentro del buscador pasaba lo mismo con dos atajos: para SALIR habia que apretar rojo y verde -el
//      verde, que es el de PONER, asi que el que queria irse ponia una cancion sin querer- y el rojo
//      borraba una letra sin que nada lo dijera. Ahora los dos estan dibujados donde se usan.
//
// Aqui se comprueba que la casilla existe, se ve, va en medio y ensena los dos botones de verdad; que el
// combo es ROJO+ARRIBA y ya NO ROJO+VERDE; y que dentro del buscador estan los avisos nuevos: cerrar con
// los mismos dos botones, y el rojo dibujado en la tecla de BORRAR, que hace lo mismo.
class Entrar
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static FieldInfo Campo(string n) { return TF.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic); }
    static MethodInfo Met(string n) { return TF.GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic); }

    // Lo que un metodo llama o lee, en orden, para poder preguntar "¿que mira justo antes de esto?".
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
        List<string> pasos = Codigo.Repasa(mCmd);
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
        List<string> pRender = Codigo.Repasa(TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) }));
        Check(Indice(pRender, "FlechaArriba") >= 0, "y esa flecha se dibuja de verdad al pintar el buscador");
        List<string> pCmd = Codigo.Repasa(mCmd);
        Check(JustoAntes(pCmd, "FlashClose", "IsUp", 6), "al apretarlos, el aviso de CERRAR se enciende (asi el cliente ve que acerto)");

        // El boton rojo DENTRO de la tecla de BORRAR: el rojo borra letras, pero eso era un atajo invisible.
        MethodInfo mTecla = TSO.GetMethod("DrawOneKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mTecla != null, "las teclas del buscador se dibujan una a una");
        if (mTecla != null)
        {
            List<string> pk = Codigo.Repasa(mTecla);
            Check(Indice(pk, "CtrlDot") >= 0, "y en la de BORRAR va dibujado el boton ROJO, que hace lo mismo");
        }

        // =========================================================================================
        Console.WriteLine("\n  7) CON TARJETA GRAFICA TAMBIEN SE VE\n");
        MethodInfo mSig = Met("UiSig");
        if (mSig == null) Console.WriteLine("     (este ejecutable es el de sin tarjeta grafica: esta parte no aplica)");
        else
        {
            // Con tarjeta grafica la pantalla se hornea en una textura y solo se rehace cuando su "firma"
            // cambia. Si la casilla no entrara en la firma, apareceria en la version normal y NO en la de
            // tarjeta grafica: se veria en las pruebas y no en el bar.
            List<string> pSig = Codigo.Repasa(mSig);
            Check(Indice(pSig, "$buscaView") >= 0, "la casilla de BUSCAR entra en la firma de la pantalla por tarjeta");
            List<string> pTex = Codigo.Repasa(Met("EnsureUiOverlay"));
            Check(Indice(pTex, "$buscaView") >= 0, "y se dibuja de verdad en ella");
        }

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "  TODO BIEN" : ("  FALLOS: " + fallos));
        return fallos;
    }
}

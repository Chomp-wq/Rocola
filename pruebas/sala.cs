using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;

// DOS COSAS DE CARA AL CLIENTE, DENTRO DE UN DISCO.
//
// 1) LA QUE SUENA AHORA TIENE MARCA. Las pedidas salian en verde ("EN COLA n") pero la que estaba SONANDO no
//    salia de ninguna manera: en cuanto empieza se saca de la cola, asi que era justo la unica cancion sin
//    marca... y es la que mas falta hace marcar, porque si al cliente se le olvido que la acaba de poner,
//    gasta otro credito en ella. Ahora sale en AMBAR, con un triangulito de play.
//
// 2) EL DISCO SE CIERRA SOLO. Alguien abre un disco, se distrae, y la rocola se queda con SU disco abierto:
//    el siguiente que llega ve una lista suelta y no sabe ni donde esta. A los 30 segundos sin tocar nada
//    vuelve sola a la pared de discos.
class Sala
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    static Type TF, TLista, TPlay, TTrack;


    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SongListView") TLista = t;
            if (t.Name == "PlayItem") TPlay = t;
            if (t.Name == "Track") TTrack = t;
        }
        Check(TLista != null && TPlay != null, "la lista del disco y la ficha de reproduccion estan donde tienen que estar");
        if (TLista == null || TPlay == null) return 1;

        Console.WriteLine("\n  1) LA COLA Y LO QUE SUENA SON DOS COSAS DISTINTAS\n");
        object F = FormatterServices.GetUninitializedObject(TF);
        FieldInfo fCola = TF.GetField("colaPos", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fSon = TF.GetField("sonandoKey", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fQueue = TF.GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fNow = TF.GetField("nowPlaying", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo mRef = TF.GetMethod("RefrescaCola", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(fCola != null && fSon != null && fNow != null && mRef != null,
              "la cola y la que suena se guardan POR SEPARADO (si compartieran dato, una taparia a la otra)");
        if (mRef == null || fSon == null) return 1;
        fCola.SetValue(F, Activator.CreateInstance(typeof(Dictionary<string, int>), new object[] { StringComparer.OrdinalIgnoreCase }));
        // El Form1 se fabrica a mano y sin constructor, asi que los campos que se rellenan solos al
        // nacer llegan vacios: hay que ponerlos aqui uno por uno. 'colaDiscos' -de que DISCOS hay algo
        // pedido, para el palomito de la pared- es uno de ellos.
        FieldInfo fDiscos = TF.GetField("colaDiscos", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fDiscos != null)
            fDiscos.SetValue(F, Activator.CreateInstance(typeof(Dictionary<string, bool>), new object[] { StringComparer.OrdinalIgnoreCase }));
        fQueue.SetValue(F, Activator.CreateInstance(typeof(List<>).MakeGenericType(TPlay)));
        fSon.SetValue(F, "");
        TF.GetField("colaVer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(F, 0);

        string rSon = @"C:\Musica\Disco\suena.mp3", rA = @"C:\Musica\Disco\a.mp3", rB = @"C:\Musica\Disco\b.mp3";
        Func<string, object> ficha = delegate (string ruta)
        {
            object it = FormatterServices.GetUninitializedObject(TPlay);
            object tk = FormatterServices.GetUninitializedObject(TTrack);
            TTrack.GetField("Path").SetValue(tk, ruta);
            TPlay.GetField("Song").SetValue(it, tk);
            TPlay.GetField("IsVideo").SetValue(it, false);
            return it;
        };
        IList q = (IList)fQueue.GetValue(F);
        q.Add(ficha(rA)); q.Add(ficha(rB));
        fNow.SetValue(F, ficha(rSon));
        mRef.Invoke(F, null);
        IDictionary cp = (IDictionary)fCola.GetValue(F);
        Console.WriteLine("     suena: " + (string)fSon.GetValue(F));
        Console.WriteLine("     pedidas: " + rA + " -> " + cp[rA] + ",  " + rB + " -> " + cp[rB]);
        Check((string)fSon.GetValue(F) == rSon, "la que suena queda apuntada aparte");
        Check(!cp.Contains(rSon), "y NO se cuela en la cola (no esta pedida)");
        Check((int)cp[rA] == 1 && (int)cp[rB] == 2, "las pedidas siguen con su puesto de siempre");

        // LO QUE PEDIA EL DUENO: si la que suena esta ADEMAS pedida otra vez, las dos cosas son verdad y las
        // dos tienen que verse. Antes la de la cola tapaba a la de sonando y parecia que no estaba sonando.
        q.Add(ficha(rSon));
        mRef.Invoke(F, null);
        cp = (IDictionary)fCola.GetValue(F);
        Console.WriteLine("     pedida otra vez: suena=" + ((string)fSon.GetValue(F) == rSon) + " y en cola=" + cp[rSon]);
        Check((string)fSon.GetValue(F) == rSon && cp.Contains(rSon) && (int)cp[rSon] > 0,
              "sonando Y pedida otra vez: las DOS cosas quedan apuntadas");

        // Y al pararse todo, las dos marcas se van.
        fNow.SetValue(F, null);
        q.Clear();
        mRef.Invoke(F, null);
        Check(((IDictionary)fCola.GetValue(F)).Count == 0 && ((string)fSon.GetValue(F)).Length == 0,
              "sin nada sonando ni pedido, no queda ninguna marca colgada");
        MethodInfo mNext = TF.GetMethod("PlayNext", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mNext != null && Codigo.Llama(mNext, "RefrescaCola"),
              "y al acabarse la cola se rehacen las marcas (si no, la de \"SONANDO\" se quedaria puesta)");

        Console.WriteLine("\n  2) Y SE VE: CUATRO ESTADOS DISTINTOS, DE UN VISTAZO\n");
        Control lv = (Control)Activator.CreateInstance(TLista, true);
        lv.SetBounds(0, 0, 900, 4 * 34);
        lv.Font = new Font(FontFamily.GenericSansSerif, 10.5f, FontStyle.Bold);
        TLista.GetField("RowH").SetValue(lv, 34);
        TLista.GetField("Accent").SetValue(lv, Color.FromArgb(90, 120, 200));
        TLista.GetField("GetCount").SetValue(lv, new Func<int>(Cuenta));
        TLista.GetField("GetText").SetValue(lv, new Func<int, string>(Texto));
        TLista.GetField("GetQueuePos").SetValue(lv, new Func<int, int>(Puesto));
        TLista.GetField("GetSonando").SetValue(lv, new Func<int, bool>(Suena));
        MethodInfo mRender = TLista.GetMethod("RenderContent", new Type[] { typeof(Graphics) });

        // fila 0 = normal, fila 1 = pedida (verde), fila 2 = sonando (ambar)
        using (Bitmap bmp = new Bitmap(900, 4 * 34, PixelFormat.Format32bppPArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.FromArgb(255, 12, 15, 34)); mRender.Invoke(lv, new object[] { g }); }
            string[] nom = { "normal", "pedida", "sonando", "las dos" };
            Color[] tono = new Color[4];
            for (int fila = 0; fila < 4; fila++) tono[fila] = TonoFila(bmp, fila * 34, 34);
            for (int fila = 0; fila < 4; fila++)
                Console.WriteLine(string.Format("     fila {0} ({1,-8}): R={2,3} G={3,3} B={4,3}", fila,
                                  nom[fila], tono[fila].R, tono[fila].G, tono[fila].B));
            // La pedida tira a VERDE (G manda) y la que suena tira a AMBAR (R manda sobre B). Se miran los
            // pixeles de verdad: es la unica forma de asegurar que se distinguen sin leer la palabra.
            Check(tono[1].G > tono[1].B + 6, "la pedida se lee VERDE");
            Check(tono[2].R > tono[2].B + 20 && tono[2].R >= tono[2].G, "la que suena se lee AMBAR");
            // La diferencia gorda entre verde y ambar esta en el ROJO (49 contra 108), que es justo lo que las
            // hace distinguibles de lejos; por eso se miden los tres canales y no solo dos.
            int dist = Math.Abs(tono[1].R - tono[2].R) + Math.Abs(tono[1].G - tono[2].G) + Math.Abs(tono[1].B - tono[2].B);
            Console.WriteLine("     lo que se separan una de otra: " + dist + " (de 0 a 765)");
            Check(dist > 50, "y las dos marcas NO se parecen: a la distancia a la que se mira una rocola se distinguen sin leer");
            Check(tono[0].R < tono[2].R && tono[0].G < tono[1].G, "la fila sin marca queda mas apagada que las dos");
        }

        Console.WriteLine("\n  2b) Y EL TEXTO CABE DENTRO DE SU PASTILLA\n");
        // ESTE es el fallo que se vio en la pantalla: la pastilla tenia un ancho FIJO, calculado para
        // "EN COLA 12", y al meter "SONANDO" el texto se salia y se leia "SONAND". Aqui se mira, con los
        // pixeles, que las LETRAS quedan dentro del fondo de la pastilla y con aire a los dos lados. Se
        // prueba con varios altos de fila: en una pantalla grande la letra crece y es donde antes fallaba.
        bool cabeSiempre = true;
        foreach (int rh in new int[] { 26, 34, 44, 56 })
        {
            TLista.GetField("RowH").SetValue(lv, rh);
            lv.Font = new Font(FontFamily.GenericSansSerif, rh * 0.31f, FontStyle.Bold);
            lv.SetBounds(0, 0, 900, 4 * rh);
            using (Bitmap b2 = new Bitmap(900, 4 * rh, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(b2)) { g.Clear(Color.FromArgb(255, 12, 15, 34)); mRender.Invoke(lv, new object[] { g }); }
                // fila 2 = solo SONANDO (ambar). Se separan el FONDO de la pastilla (ambar apagado) de las
                // LETRAS (ambar encendido) por su brillo.
                int y0 = 2 * rh + 3, y1 = 3 * rh - 3;
                // La pastilla es la unica cosa AMBAR de la mitad derecha (el circulito del numero tambien es
                // ambar, pero va a la izquierda del todo). Se busca por COLUMNAS: una columna es "de pastilla"
                // si buena parte de su alto es ambar -eso es el relleno-, y es "de letra" si ademas tiene
                // pixeles encendidos.
                int pIzq = int.MaxValue, pDer = -1, tIzq = int.MaxValue, tDer = -1;
                int minAlto = 6;   // la pastilla es bajita a proposito: no se puede pedir media fila de alto
                for (int x = b2.Width / 2; x < b2.Width; x++)
                {
                    int nAmbar = 0, nLetra = 0;
                    for (int y = y0; y < y1; y++)
                    {
                        Color c = b2.GetPixel(x, y);
                        if (c.R <= c.B + 10 || c.R + c.G + c.B <= 110) continue;
                        nAmbar++;
                        if (c.R > 190 && c.G > 130) nLetra++;
                    }
                    if (nAmbar < minAlto) continue;
                    if (x < pIzq) pIzq = x;
                    if (x > pDer) pDer = x;
                    if (nLetra > 0) { if (x < tIzq) tIzq = x; if (x > tDer) tDer = x; }
                }
                bool ok = (pDer > pIzq) && (tDer > tIzq) && (tIzq > pIzq + 1) && (tDer < pDer - 1);
                Console.WriteLine(string.Format("     fila de {0,2} px:  pastilla x{1}..{2}   letras x{3}..{4}   {5}",
                                  rh, pIzq, pDer, tIzq, tDer, ok ? "cabe" : "SE SALE"));
                if (!ok) cabeSiempre = false;
            }
        }
        Check(cabeSiempre, "el texto de la marca cabe dentro de su pastilla, con aire a los dos lados, a cualquier tamano");

        Console.WriteLine("\n  2c) SONANDO Y EN COLA SE VEN LAS DOS A LA VEZ\n");
        // Lo que pidio el dueno: si la cancion que suena esta ademas pedida otra vez, las dos cosas son
        // verdad y el cliente tiene que ver las dos. Antes la verde tapaba a la ambar.
        TLista.GetField("RowH").SetValue(lv, 34);
        lv.Font = new Font(FontFamily.GenericSansSerif, 10.5f, FontStyle.Bold);
        lv.SetBounds(0, 0, 900, 4 * 34);
        using (Bitmap b3 = new Bitmap(900, 4 * 34, PixelFormat.Format32bppPArgb))
        {
            using (Graphics g = Graphics.FromImage(b3)) { g.Clear(Color.FromArgb(255, 12, 15, 34)); mRender.Invoke(lv, new object[] { g }); }
            int verdes = 0, ambares = 0;
            for (int y = 3 * 34 + 4; y < 4 * 34 - 4; y++)
                for (int x = b3.Width / 2; x < b3.Width; x++)
                {
                    Color c = b3.GetPixel(x, y);
                    if (c.R + c.G + c.B < 110) continue;
                    if (c.G > c.R + 12 && c.G > c.B + 6) verdes++;      // la de la cola
                    else if (c.R > c.B + 12 && c.R >= c.G) ambares++;   // la de sonando
                }
            Console.WriteLine("     en la fila que suena Y esta pedida:  " + verdes + " pixeles verdes y " + ambares + " ambar");
            Check(verdes > 60 && ambares > 60, "se ven LAS DOS marcas en la misma fila (antes una tapaba a la otra)");
        }

        Console.WriteLine("\n  2d) LO HORNEADO NO SE ACUMULA CON EL USO\n");
        // Las pastillas se hornean por ANCHO. Si cada puesto de cola horneara la suya, una noche larga
        // dejaria cientos de mapas de bits colgados. Son pocos anchos distintos y se comparten.
        FieldInfo fPastV = TLista.GetField("hornoPastV", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(fPastV != null, "las pastillas horneadas se pueden contar");
        if (fPastV != null)
        {
            using (Bitmap b4 = new Bitmap(900, 4 * 34, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b4))
                for (int k = 1; k <= 120; k++)
                {
                    puestoBase = k;   // van cambiando los puestos de la cola: "EN COLA 1" ... "EN COLA 120"
                    mRender.Invoke(lv, new object[] { g });
                    TLista.GetField("Datos").SetValue(lv, k);   // obliga a rehornear la pagina
                }
            int n = ((ICollection)fPastV.GetValue(lv)).Count;
            Console.WriteLine("     tras 120 puestos de cola distintos: " + n + " pastillas horneadas en total");
            Check(n <= 12, "se comparten por ancho: unos pocos mapas de bits, no uno por cancion");
        }

        Console.WriteLine("\n  3) EL DISCO SE CIERRA SOLO A LOS 30 SEGUNDOS\n");
        FieldInfo fSecs = TF.GetField("DiscoIdleSecs", BindingFlags.Static | BindingFlags.NonPublic);
        Check(fSecs != null, "hay un tiempo de espera para salir del disco");
        int secs = (fSecs != null) ? (int)fSecs.GetRawConstantValue() : -1;
        FieldInfo fAtr = TF.GetField("AttractIdleSecs", BindingFlags.Static | BindingFlags.NonPublic);
        int atr = (fAtr != null) ? (int)fAtr.GetRawConstantValue() : -1;
        Console.WriteLine("     salir del disco: " + secs + " s      entrar al modo atraccion: " + atr + " s");
        Check(secs == 30, "son 30 segundos, como se pidio");
        Check(secs < atr, "y va POR DELANTE del modo atraccion: primero vuelve a la pared, luego entra la atraccion");
        MethodInfo mTick = TF.GetMethod("AnimTimer_Tick", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mTick != null && Codigo.Llama(mTick, "BackToAlbums"),
              "y quien lo hace es el temporizador de siempre (no hace falta ningun hilo nuevo)");

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }

    // El tono MEDIO de lo que se pinto en la mitad derecha de una fila (donde van las marcas), sin contar el
    // fondo: asi se mide el color de la marca y no el del panel.
    static Color TonoFila(Bitmap b, int y0, int h)
    {
        long r = 0, g = 0, bl = 0; int n = 0;
        for (int y = y0 + 4; y < y0 + h - 4; y++)
            for (int x = b.Width / 2; x < b.Width - 4; x++)
            {
                Color c = b.GetPixel(x, y);
                if (c.R + c.G + c.B < 150) continue;   // fondo oscuro: no cuenta
                r += c.R; g += c.G; bl += c.B; n++;
            }
        if (n == 0) return Color.Black;
        return Color.FromArgb((int)(r / n), (int)(g / n), (int)(bl / n));
    }

    // fila 0 = nada, 1 = pedida, 2 = sonando, 3 = sonando Y pedida otra vez
    static int Cuenta() { return 4; }
    static string Texto(int i) { return "Cancion de prueba numero " + (i + 1); }
    static int puestoBase = 2;
    static int Puesto(int i) { return (i == 1) ? 1 : (i == 3) ? puestoBase : 0; }
    static bool Suena(int i) { return i == 2 || i == 3; }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LA BARRA DEL ABECEDARIO: que ninguna casilla se salga de lo suyo NI SE PARTA EN RENGLONES.
//
// De donde sale esta bateria: "VIDEO" mide 36 px y su casilla 34 con la pantalla entera, asi que no cabia; y
// con un video puesto al lado la barra se encoge A LA MITAD (486 px para 29 casillas = 16.8 px cada una) y
// entonces no cabian ni "VIDEO" ni "TOP". Al dibujarlos dentro de su casilla, Windows los PARTIA en varios
// renglones que tampoco cabian de alto, y se veia "VI / D / EO" recortado contra los bordes.
//
// No se comprueba el arreglo por dentro -eso se podria cambiar y la bateria seguiria pasando-: se DIBUJA la
// barra de verdad, sacada del ejecutable, y se miran los pixeles.
//
// COMO SE DISTINGUE EL TEXTO DEL RIEL: la barra se dibuja sobre un lienzo transparente y se mira el ALFA. El
// riel de vidrio y su brillo se quedan en 154 como mucho; el nucleo de una letra llega a 255 y el de un
// numero a ~203. Con el corte en 190 se separan sin discusion (medido con sonda.cs).
class Abc
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const string LETRAS = "★#ABCDEFGHIJKLMNOPQRSTUVWXYZ►";
    const int ALTO = 40;
    const int CORTE = 190;   // alfa a partir del cual es TEXTO y no riel

    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static Type tipoBarra, tipoForm;
    static HashSet<char> disp = new HashSet<char>();
    static Dictionary<char, int> cuentas = new Dictionary<char, int>();
    static Dictionary<char, Color> tintes = new Dictionary<char, Color>();

    static Bitmap Dibuja(int ancho, char actual, bool conDatos)
    {
        Control bar = (Control)Activator.CreateInstance(tipoBarra, true);
        bar.SetBounds(0, 0, ancho, ALTO);
        bar.Font = new Font(Fam(), 9f);
        tipoBarra.GetField("Letters").SetValue(bar, LETRAS);
        tipoBarra.GetProperty("Current").SetValue(bar, actual, null);
        // Current es una PROPIEDAD y al cambiarla arranca el deslizamiento de la capsula. Aqui se
        // mide la barra QUIETA, asi que se le deja terminar antes de dibujar.
        System.Threading.Thread.Sleep(230);
        tipoBarra.GetField("Available").SetValue(bar, disp);
        tipoBarra.GetField("Counts").SetValue(bar, conDatos ? cuentas : null);
        tipoBarra.GetField("Tints").SetValue(bar, conDatos ? tintes : null);
        tipoBarra.GetField("Phase").SetValue(bar, 0.6d);
        Bitmap bmp = new Bitmap(ancho, ALTO, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);
            tipoBarra.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(bar, new object[] { g });
        }
        return bmp;
    }

    class Marco
    {
        public int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1, grupos;
        public bool Hay { get { return x1 >= 0; } }
    }

    // El marco del TEXTO que hay en una franja vertical, y en cuantos grupos de renglones separados cae.
    static Marco Mide(Bitmap b, int xa, int xb)
    {
        Marco m = new Marco();
        bool[] fila = new bool[ALTO];
        for (int y = 0; y < ALTO; y++)
            for (int x = xa; x < xb; x++)
                if (b.GetPixel(x, y).A >= CORTE)
                {
                    fila[y] = true;
                    if (x < m.x0) m.x0 = x;
                    if (x > m.x1) m.x1 = x;
                    if (y < m.y0) m.y0 = y;
                    if (y > m.y1) m.y1 = y;
                }
        int vacias = 99;
        for (int y = 0; y < ALTO; y++)
        {
            if (fila[y]) { if (vacias >= 2) m.grupos++; vacias = 0; }
            else vacias++;
        }
        return m;
    }

    static int Main()
    {
        Assembly asm = Assembly.LoadFrom("rocola.exe");
        tipoForm = asm.GetType("Rocola.Form1", true);
        foreach (Type t in tipoForm.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "LetterBar") tipoBarra = t;
        if (tipoBarra == null) { Console.WriteLine("no se encontro LetterBar"); return 1; }

        MethodInfo gColor = tipoForm.GetMethod("GenreToColor", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo gLabel = tipoForm.GetMethod("GenreLabel", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo eColor = tipoForm.GetMethod("ColorDeEtiqueta", BindingFlags.Static | BindingFlags.NonPublic);

        string[] gen = { "Cumbia", "Rock", "Balada", "Reggaeton", "Banda", "Pop", "Metal", "Salsa" };
        int gi = 0;
        foreach (char c in LETRAS)
        {
            if ("KQWXY".IndexOf(c) >= 0) continue;
            disp.Add(c); cuentas[c] = 8 + gi * 5;
            tintes[c] = (Color)gColor.Invoke(null, new object[] { gen[gi % gen.Length] }); gi++;
        }

        // Los anchos que la barra usa de verdad: pantalla entera, con un video al lado, y el minimo que el
        // programa se permite (220 px), por si alguna vez se abre en una ventana chica.
        int[] anchos = { 996, 800, 486, 320, 220 };
        string[] como = { "pantalla entera (1024)", "pantalla algo mas chica", "CON VIDEO al lado", "muy estrecha", "el minimo que se permite" };

        for (int k = 0; k < anchos.Length; k++)
        {
            int w = anchos[k];
            float slot = (float)w / LETRAS.Length;
            Console.WriteLine(string.Format("\n  {0}   ancho {1} px  ->  {2:0.0} px por casilla", como[k], w, slot));

            using (Bitmap b = Dibuja(w, 'M', true))
            {
                int tocaBorde = 0, partidas = 0, faltan = 0, seSale = 0, conNumero = 0;
                string ejemplo = null;
                for (int i = 0; i < LETRAS.Length; i++)
                {
                    int xa = (int)(i * slot), xb = (int)((i + 1) * slot);
                    if (xb > w) xb = w;
                    if (xb - xa < 2) continue;
                    Marco m = Mide(b, xa, xb);
                    char c = LETRAS[i];
                    bool hay = disp.Contains(c);
                    if (!m.Hay) { if (hay) { faltan++; if (ejemplo == null) ejemplo = c + " no dibuja nada"; } continue; }

                    // El texto no puede pegarse al borde de arriba ni al de abajo: cuando una etiqueta se
                    // partia en renglones, sobresalia y se veia cortada justo ahi.
                    if (m.y0 <= 0 || m.y1 >= ALTO - 1)
                    { tocaBorde++; if (ejemplo == null) ejemplo = c + " toca el borde de arriba/abajo"; }

                    // Como mucho DOS grupos: la letra y, debajo, su numero de canciones. Tres o mas significa
                    // que algo se partio en renglones, que es justo el fallo que se arreglo.
                    if (m.grupos > 2)
                    { partidas++; if (ejemplo == null) ejemplo = c + " cae en " + m.grupos + " grupos de renglones"; }
                    if (m.grupos == 2) conNumero++;

                    // Y el texto de una casilla se queda DENTRO de su casilla.
                    if (m.x0 < xa || m.x1 >= xb)
                    { seSale++; if (ejemplo == null) ejemplo = c + " se sale de su casilla"; }
                }

                Console.WriteLine("     casillas que tocan el borde de arriba/abajo: " + tocaBorde);
                Console.WriteLine("     casillas con mas de dos grupos de renglones: " + partidas);
                Console.WriteLine("     casillas con canciones que no dibujan nada:  " + faltan);
                Console.WriteLine("     casillas cuyo texto se sale de su hueco:     " + seSale);
                Console.WriteLine("     casillas que ademas ensenan su numero:       " + conNumero);
                if (ejemplo != null) Console.WriteLine("     ejemplo: " + ejemplo);
                Check(tocaBorde == 0, "nada se pega al borde de arriba ni al de abajo (ahi se veia recortado)");
                Check(partidas == 0, "nada se parte en renglones (como mucho: letra arriba, numero debajo)");
                Check(faltan == 0, "toda casilla con canciones dibuja su letra");
                Check(seSale == 0, "y ninguna se mete en la casilla de al lado");

                // El numero solo cabe con la pantalla entera; con un video al lado la barra se queda a la
                // mitad y entonces NO debe intentar dibujarlo, porque no se leeria.
                if (w >= 800) Check(conNumero >= 20, "con sitio de sobra, las letras ensenan cuantas canciones hay");
                if (w <= 486) Check(conNumero == 0, "sin sitio, el numero se calla en vez de amontonarse");
            }
        }

        // Y la etiqueta que PROVOCO todo esto: a cada ancho, o cabe la palabra o se pone el simbolo, pero
        // nunca se dibuja algo mas ancho que su casilla. Se dibuja SIN los numeros, para que en su casilla no
        // haya nada mas que la etiqueta y "un solo renglon" signifique exactamente eso.
        Console.WriteLine("\n  LA CASILLA DE VIDEO, ancho por ancho\n");
        for (int k = 0; k < anchos.Length; k++)
        {
            int w = anchos[k];
            float slot = (float)w / LETRAS.Length;
            using (Bitmap b = Dibuja(w, 'M', false))
            {
                int i = LETRAS.Length - 1;
                int xa = (int)(i * slot);
                Marco m = Mide(b, xa, w);
                int ancho2 = m.Hay ? (m.x1 - m.x0 + 1) : 0;
                Console.WriteLine(string.Format("     ancho {0,4}: hueco {1,4:0.0} px, la etiqueta ocupa {2,3} px y cae en {3} grupo(s)",
                                                w, slot, ancho2, m.grupos));
                Check(m.Hay && ancho2 <= (int)slot, "la etiqueta de video cabe en su casilla");
                Check(m.grupos == 1, "y va en un solo renglon");
            }
        }

        // ---------- LA ESTRELLITA ----------
        // "Lo mas sonado" ya no dice TOP: es una estrella dorada. Se comprueba que en su casilla hay algo
        // dorado de verdad (mas rojo y verde que azul) y no un texto blanco.
        Console.WriteLine("\n  LA ESTRELLITA DE LO MAS SONADO\n");
        using (Bitmap b = Dibuja(996, 'M', true))
        {
            float slot = 996f / LETRAS.Length;
            int xa = 0, xb = (int)slot;
            long r = 0, g2 = 0, bl = 0; int n = 0;
            for (int y = 0; y < ALTO; y++)
                for (int x = xa; x < xb; x++)
                {
                    Color c = b.GetPixel(x, y);
                    if (c.A >= CORTE) { r += c.R; g2 += c.G; bl += c.B; n++; }
                }
            Console.WriteLine("     color medio de su casilla: R=" + (n > 0 ? r / n : 0) + " G=" + (n > 0 ? g2 / n : 0) + " B=" + (n > 0 ? bl / n : 0));
            Check(n > 0, "la casilla de lo mas sonado dibuja algo");
            Check(n > 0 && r / n > bl / n + 40 && g2 / n > bl / n + 20, "y ese algo es DORADO, no blanco");
        }

        // ---------- QUE LOS COLORES NO SE DESPAREJEN ----------
        // El color de cada letra se guarda entre arranques como ETIQUETA de genero, y al leerlo se convierte
        // con ColorDeEtiqueta. Ese color TIENE que ser el mismo que GenreToColor le daria al genero crudo; si
        // alguien toca una de las dos tablas y no la otra, los colores de la barra dejarian de corresponder.
        Console.WriteLine("\n  EL COLOR GUARDADO ES EL MISMO QUE EL DEL GENERO\n");
        string[] crudos = {
            "K-Pop", "J-Pop", "anime", "Heavy Metal", "Thrash", "Punk Rock", "Gothic Rock", "Darkwave",
            "Industrial", "Reggaeton", "Perreo", "Hip-Hop", "Rap", "Trap", "Salsa", "Merengue", "Bachata",
            "Cumbia", "Banda", "Nortena", "Mariachi", "Regional Mexicano", "Corrido", "Bolero", "Tango",
            "Flamenco", "Rumba", "Latin Pop", "Reggae", "Ska", "Drum and Bass", "Jungle", "Techno", "House",
            "Trance", "EDM", "Synthwave", "Ambient", "Chillout", "Lo-Fi", "Jazz", "Swing", "Blues", "Soul",
            "Funk", "Gospel", "Christian", "Classical", "Opera", "Piano", "Soundtrack", "Score", "Country",
            "Bluegrass", "Folk", "Acoustic", "Disco", "Oldies", "World", "Vocal", "Comedy", "Spoken Word",
            "Kids", "Infantil", "Pop", "Rock", "Grunge", "Indie", "Alternative"
        };
        int malos = 0; string primero = null;
        for (int i = 0; i < crudos.Length; i++)
        {
            string et = (string)gLabel.Invoke(null, new object[] { crudos[i] });
            Color porGenero = (Color)gColor.Invoke(null, new object[] { crudos[i] });
            Color porEtiqueta = (Color)eColor.Invoke(null, new object[] { et });
            if (porGenero.ToArgb() != porEtiqueta.ToArgb())
            {
                malos++;
                if (primero == null) primero = crudos[i] + " -> etiqueta \"" + et + "\": el genero da "
                    + porGenero.R + "," + porGenero.G + "," + porGenero.B + " y la etiqueta "
                    + porEtiqueta.R + "," + porEtiqueta.G + "," + porEtiqueta.B;
            }
        }
        Console.WriteLine("     generos probados: " + crudos.Length + "   desparejados: " + malos);
        if (primero != null) Console.WriteLine("     primero: " + primero);
        Check(malos == 0, "el color guardado y el del genero coinciden en los " + crudos.Length + " generos probados");

        // ---------- DONDE PINCHAS ES LA LETRA QUE SALE ----------
        // La barra DIBUJA con decimales y antes decidia el pinchazo con division ENTERA: las dos cuentas se
        // iban separando hacia la derecha, asi que en las ultimas casillas pinchabas una letra y te llevaba a
        // otra. Se prueba pinchando en el centro y en los dos bordes de CADA casilla, a todos los anchos.
        Console.WriteLine("\n  DONDE PINCHAS ES LA LETRA QUE SALE\n");
        MethodInfo mClick = tipoBarra.GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fPick = tipoBarra.GetField("OnPick");
        for (int k = 0; k < anchos.Length; k++)
        {
            int w = anchos[k];
            float slot = (float)w / LETRAS.Length;
            Control bar = (Control)Activator.CreateInstance(tipoBarra, true);
            bar.SetBounds(0, 0, w, ALTO);
            bar.Font = new Font(Fam(), 9f);
            tipoBarra.GetField("Letters").SetValue(bar, LETRAS);
            char[] salio = new char[1];
            fPick.SetValue(bar, new Action<char>(delegate (char c) { salio[0] = c; }));
            int fuera = 0; string cual = null;
            for (int i = 0; i < LETRAS.Length; i++)
            {
                // centro de la casilla, y un pelo por dentro de cada borde
                int[] xs = { (int)(i * slot + slot / 2f), (int)(i * slot) + 1, (int)((i + 1) * slot) - 1 };
                for (int j = 0; j < xs.Length; j++)
                {
                    if (xs[j] < 0 || xs[j] >= w) continue;
                    salio[0] = '\0';
                    mClick.Invoke(bar, new object[] { new MouseEventArgs(MouseButtons.Left, 1, xs[j], ALTO / 2, 0) });
                    if (salio[0] != LETRAS[i])
                    {
                        fuera++;
                        if (cual == null) cual = "pinchando en x=" + xs[j] + " (casilla de " + LETRAS[i] + ") sale " + salio[0];
                    }
                }
            }
            Console.WriteLine("     ancho " + w.ToString().PadLeft(4) + ": pinchazos equivocados = " + fuera + (cual != null ? "   " + cual : ""));
            Check(fuera == 0, "a " + w + " px, cada pinchazo cae en la letra que se ve debajo del dedo");
        }

        // ---------- EL DESLIZAMIENTO AL CAMBIAR DE LETRA ----------
        Console.WriteLine("\n  EL DESLIZAMIENTO AL CAMBIAR DE LETRA\n");
        {
            Control bar = (Control)Activator.CreateInstance(tipoBarra, true);
            bar.SetBounds(0, 0, 996, ALTO);
            bar.Font = new Font(Fam(), 9f);
            tipoBarra.GetField("Letters").SetValue(bar, LETRAS);
            PropertyInfo pCur = tipoBarra.GetProperty("Current");
            PropertyInfo pPaso = tipoBarra.GetProperty("AnimPaso");
            PropertyInfo pViva = tipoBarra.GetProperty("AnimViva");
            pCur.SetValue(bar, 'A', null);
            System.Threading.Thread.Sleep(250);
            Check(!(bool)pViva.GetValue(bar, null), "en reposo no hay ninguna animacion corriendo");
            Check(Math.Abs((float)pPaso.GetValue(bar, null) - 1f) < 0.001f, "y el paso vale 1, o sea: ya llego");

            pCur.SetValue(bar, 'T', null);
            float p0 = (float)pPaso.GetValue(bar, null);
            Check((bool)pViva.GetValue(bar, null), "al cambiar de letra arranca el deslizamiento");
            Check(p0 < 0.6f, "que empieza cerca de la casilla que se deja, no de golpe en la de destino");
            System.Threading.Thread.Sleep(60);
            float p1 = (float)pPaso.GetValue(bar, null);
            Check(p1 > p0, "y avanza con el tiempo");
            System.Threading.Thread.Sleep(60);
            float p2 = (float)pPaso.GetValue(bar, null);
            // Frena al llegar: en la segunda mitad avanza MENOS que en la primera.
            Console.WriteLine(string.Format("     paso: {0:0.000} -> {1:0.000} -> {2:0.000}", p0, p1, p2));
            Check((p2 - p1) < (p1 - p0) + 0.001f, "frenando al llegar, no a velocidad constante");
            System.Threading.Thread.Sleep(200);
            Check(!(bool)pViva.GetValue(bar, null) && (float)pPaso.GetValue(bar, null) >= 1f,
                  "y termina sola en menos de un tercio de segundo");

            // Poner la MISMA letra no debe arrancar nada (si no, cada refresco lo re-lanzaria).
            pCur.SetValue(bar, 'T', null);
            Check(!(bool)pViva.GetValue(bar, null), "volver a poner la letra que ya estaba no relanza el deslizamiento");
        }

        // ---------- EL ENCABEZADO ----------
        // Es dibujado, asi que lo que hay que comprobar es que quepa: que ni el nombre de la seccion ni los
        // puntitos ni el "pagina X de Y" se salgan por la derecha.
        Console.WriteLine("\n  EL ENCABEZADO DE LA SECCION\n");
        Type tHead = null;
        foreach (Type t in tipoForm.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            if (t.Name == "HeaderView") tHead = t;
        Check(tHead != null, "el encabezado esta donde tiene que estar");
        if (tHead != null)
        {
            string[] secs = { "LO MAS SONADO", "VIDEOS", "LETRA M", "LETRA W" };
            int[][] pgs = { new int[] { 0, 1 }, new int[] { 2, 7 }, new int[] { 5, 12 }, new int[] { 17, 40 } };
            int[] anchosC = { 996, 486, 300 };
            int[] altosC = { 28, 40, 52 };
            int desbordes = 0; string ej = null;
            for (int a = 0; a < anchosC.Length; a++)
                for (int h2 = 0; h2 < altosC.Length; h2++)
                    for (int i = 0; i < secs.Length; i++)
                        for (int j = 0; j < pgs.Length; j++)
                        {
                            Control c = (Control)Activator.CreateInstance(tHead, true);
                            c.SetBounds(0, 0, anchosC[a], altosC[h2]);
                            c.Font = new Font(Fam(), 9f);
                            tHead.GetField("Section").SetValue(c, secs[i]);
                            tHead.GetField("Page").SetValue(c, pgs[j][0]);
                            tHead.GetField("Total").SetValue(c, pgs[j][1]);
                            tHead.GetField("Accent").SetValue(c, Color.FromArgb(225, 120, 30));
                            // SE LE DA UN LIENZO MAS ANCHO DEL QUE EL CONTROL DICE MEDIR. Asi, si dibujara
                            // fuera de su caja, la tinta apareceria ahi y se veria. Con un lienzo del tamano
                            // justo no se notaria: quedaria recortada y pareceria correcta.
                            int margen = 200;
                            using (Bitmap b = new Bitmap(anchosC[a] + margen, altosC[h2] + 20, PixelFormat.Format32bppArgb))
                            {
                                using (Graphics g = Graphics.FromImage(b))
                                {
                                    g.SmoothingMode = SmoothingMode.AntiAlias;
                                    g.Clear(Color.Transparent);
                                    tHead.GetMethod("RenderContent", new Type[] { typeof(Graphics) }).Invoke(c, new object[] { g });
                                }
                                bool seSale = false, hayTinta = false;
                                for (int y = 0; y < altosC[h2] + 20; y++)
                                    for (int x = 0; x < anchosC[a] + margen; x++)
                                        if (b.GetPixel(x, y).A >= 60)
                                        {
                                            hayTinta = true;
                                            if (x >= anchosC[a] || y >= altosC[h2]) seSale = true;
                                        }
                                bool cabeAlgo = anchosC[a] >= 40 && altosC[h2] >= 8 && secs[i] != null && secs[i].Length > 0 && secs[i].Length <= 20;
                                if (seSale || (cabeAlgo && !hayTinta))
                                {
                                    desbordes++;
                                    if (ej == null) ej = secs[i] + " con " + pgs[j][1] + " paginas en " + anchosC[a] + "x" + altosC[h2]
                                        + (seSale ? " -> pinta FUERA de su caja" : " -> no dibuja nada");
                                }
                            }
                        }
            Console.WriteLine("     combinaciones probadas: " + (anchosC.Length * altosC.Length * secs.Length * pgs.Length) + "   desbordes: " + desbordes);
            if (ej != null) Console.WriteLine("     ejemplo: " + ej);
            Check(desbordes == 0, "el encabezado nunca queda cortado, ni estrecho ni con muchas paginas");
        }

        Console.WriteLine(fallos == 0 ? "\nTODO BIEN" : "\nFALLOS: " + fallos);
        return fallos;
    }
}

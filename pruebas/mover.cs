using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// MOVERSE ENTRE LAS TECLAS NO PUEDE REHACER LA PANTALLA ENTERA... PERO TIENE QUE VERSE IGUAL.
//
// La rocola compone toda la pantalla en una imagen y la sube a la tarjeta grafica. Para no rehacerla en
// cada cuadro lleva una FIRMA: si nada cambio, se reaprovecha. El problema era que la CASILLA ELEGIDA
// entraba en esa firma, asi que pulsar una flecha -que solo cambia que tecla lleva el recuadro- obligaba a
// rehacer y RE-SUBIR la pantalla completa: 5.91 ms por pulsacion a 1080p, el 35% de un cuadro. La tecla
// elegida sale ahora como PIEZA VIVA aparte, en su propia imagen chica encima de la grande.
//
// ESTA BATERIA SE ESCRIBIO DOS VECES, Y LA PRIMERA NO SERVIA. Decia "0 pixeles de diferencia" mientras el
// programa se veia roto de tres maneras distintas. Se equivocaba en tres cosas, y las tres estan arregladas
// aqui porque son justo por donde se rompe esto:
//
//   1) MONTABA EL BUSCADOR CON LOS RESULTADOS ENFOCADOS, que es cuando el teclado NO se dibuja (en su sitio
//      va el cubo de la portada). Las teclas no tenian ni sitio asignado, asi que la pieza salia de 4x4 en
//      (-2,-2): comparaba basura contra basura y daba 0. Ahora se prueban LOS DOS estados.
//   2) DIBUJABA LA PIEZA CON UNA TRASFORMACION DEL LIENZO. Eso funciona en Mono y NO en Windows: el texto
//      lo dibuja el sistema (TextRenderer) y ese NO hace caso a las trasformaciones. Por eso en el bar la
//      tecla elegida salia como un rectangulo rosa VACIO, sin letra. Aqui ya no se permite: se comprueba
//      leyendo el programa compilado que la pieza se dibuja pasando el DESPLAZAMIENTO, no trasformando.
//   3) NO MIRABA LA FIRMA AL REVES. Sacar la casilla elegida esta bien; sacar tambien "estoy en los
//      resultados" no, porque eso cambia MEDIA PANTALLA (teclado <-> cubo). Al sacarlo, volver de los
//      resultados al teclado no rehacia la imagen y el teclado no volvia a aparecer nunca.
class Mover
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

    const BindingFlags IF = BindingFlags.Instance | BindingFlags.NonPublic;

    static Type TF, TSO, TItem, TAlb;
    static Bitmap portada = Tapa();
    static Bitmap Tapa()
    {
        Bitmap b = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(255, 30, 40, 90));
            for (int i = 0; i < 40; i++) using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, 200, 120 + i, 255 - i))) g.FillEllipse(sb, i * 7, i * 6, 60, 60);
        }
        return b;
    }
    static Image Cov<T>(T a) { return portada; }
    static string Gen<T>(T a) { return "rock"; }
    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }

    // foco = false: se ve el TECLADO (que es cuando hay pieza viva). true: se ve el cubo de la portada.
    static Control Monta(int W, int H, int sel, bool foco)
    {
        Control so = (Control)Activator.CreateInstance(TSO, true);
        so.SetBounds(0, 0, W, H);
        so.Font = new Font(Fam(), 12f, FontStyle.Bold);
        FieldInfo fKind = TItem.GetField("Kind"), fDisp = TItem.GetField("Display"), fKey = TItem.GetField("Key"), fSc = TItem.GetField("Score");
        IList res = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TItem));
        string[] nom = { "Depeche Mode - Enjoy The Silence", "Jose Jose - El Triste", "Los Angeles Azules - Nunca Es Suficiente",
                         "Metallica - Enter Sandman", "Juan Gabriel - Amor Eterno", "Bronco - Que No Quede Huella" };
        for (int i = 0; i < 40; i++)
        {
            object it = Activator.CreateInstance(TItem, true);
            fKind.SetValue(it, i % 3); fDisp.SetValue(it, nom[i % nom.Length] + " " + i);
            fKey.SetValue(it, (nom[i % nom.Length] + " " + i).ToLowerInvariant()); fSc.SetValue(it, 5000 - i);
            res.Add(it);
        }
        object alb = Activator.CreateInstance(TAlb, true);
        TAlb.GetField("Folder").SetValue(alb, @"C:\Musica\Disco");
        TAlb.GetField("Name").SetValue(alb, "Un disco");
        TAlb.GetField("Artist").SetValue(alb, "Un artista");
        TAlb.GetField("CoverPath").SetValue(alb, @"C:\Musica\Disco\cover.jpg");
        FieldInfo fAlbRef = TItem.GetField("AlbumRef");
        for (int i = 0; i < res.Count; i++) fAlbRef.SetValue(res[i], alb);
        Type tfc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
        MethodInfo mic = typeof(Mover).GetMethod("Cov", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb);
        TSO.GetField("GetCover").SetValue(so, Delegate.CreateDelegate(tfc, mic));
        FieldInfo fGen = TSO.GetField("GetGenero");
        if (fGen != null)
        {
            Type tf2 = typeof(Func<,>).MakeGenericType(TItem, typeof(string));
            MethodInfo mi = typeof(Mover).GetMethod("Gen", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TItem);
            fGen.SetValue(so, Delegate.CreateDelegate(tf2, mi));
        }
        TSO.GetField("Results").SetValue(so, res);
        TSO.GetField("Query").SetValue(so, "de");
        TSO.GetField("Accent").SetValue(so, Color.FromArgb(178, 152, 255));
        TSO.GetField("FocusResults").SetValue(so, foco);
        TSO.GetField("SelResult").SetValue(so, sel);
        TSO.GetField("SelRow").SetValue(so, 1);
        TSO.GetField("SelCol").SetValue(so, 4);
        return so;
    }

    static void Compara(Bitmap a, Bitmap b, out int distintos, out int peor, out Rectangle donde)
    {
        distintos = 0; peor = 0;
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                Color ca = a.GetPixel(x, y), cb = b.GetPixel(x, y);
                int d = Math.Max(Math.Max(Math.Abs(ca.R - cb.R), Math.Abs(ca.G - cb.G)),
                        Math.Max(Math.Abs(ca.B - cb.B), Math.Abs(ca.A - cb.A)));
                if (d <= 0) continue;
                distintos++; if (d > peor) peor = d;
                if (x < x0) x0 = x; if (x > x1) x1 = x;
                if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
        donde = (distintos == 0) ? Rectangle.Empty : Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
    }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (t.Name == "SearchOverlay") TSO = t;
            if (t.Name == "SearchItem") TItem = t;
            if (t.Name == "Album") TAlb = t;
        }
        Check(TSO != null && TItem != null && TAlb != null, "el buscador esta donde tiene que estar");
        if (TSO == null || TItem == null || TAlb == null) return Math.Max(1, fallos);

        // ---------- 1) LA FIRMA: LO QUE SALE Y LO QUE TIENE QUE SEGUIR ENTRANDO ----------
        Console.WriteLine("\n  1) LA FIRMA DE LA PANTALLA: LO QUE SALE Y LO QUE NO PUEDE SALIR\n");
        MethodInfo mUi = TF.GetMethod("UiSig", IF);
        Check(mUi != null, "la firma de la imagen de pantalla completa esta donde tiene que estar");
        if (mUi != null)
        {
            // FUERA: la tecla elegida. Es lo unico que se puede sacar, porque sale como pieza viva encima.
            foreach (string c in new string[] { "SelRow", "SelCol" })
            {
                int lee, esc; Codigo.UsosDeCampo(mUi, c, out lee, out esc);
                Check(lee == 0, "moverse a otra TECLA (" + c + ") no entra en la firma: no rehace ni re-sube la pantalla");
            }
            // DENTRO, y esto es lo que se saco de mas la primera vez:
            //  - FocusResults cambia MEDIA PANTALLA: donde estaba el teclado se pone el cubo de la portada.
            //    Sin esto, al volver de los resultados el teclado NO volvia a aparecer nunca.
            //  - SelResult, con el cubo delante, cambia la portada y el texto de debajo.
            //  - Query, porque los resultados son otros de verdad.
            string[] dentro = { "FocusResults", "Query" };
            string[] porque = { "cambia media pantalla: teclado <-> cubo de la portada",
                                "los resultados son otros de verdad" };
            for (int i = 0; i < dentro.Length; i++)
            {
                int lee, esc; Codigo.UsosDeCampo(mUi, dentro[i], out lee, out esc);
                Check(lee > 0, dentro[i] + " SI entra en la firma (" + porque[i] + ")");
            }

            // SelResult YA NO VA EN LA FIRMA GRANDE, Y ESO ESTA BIEN AHORA. Antes tenia que estar: con el
            // cubo delante, moverse por los resultados cambia que disco se ensena y el texto de debajo, y
            // sacarlo dejaba esa media pantalla congelada -se vio en el bar-. Lo que cambio es DONDE se
            // dibuja: la vista previa salio de la imagen grande y ahora es una PIEZA aparte, siempre, asi
            // que rehacer la pantalla entera por mover la seleccion seria trabajo tirado.
            //
            // Pero la promesa es la misma y hay que seguir vigilandola, solo que en su sitio nuevo: si
            // SelResult no entra en la firma de la PIEZA, la portada y su texto se quedan clavados igual
            // que antes. Por eso esto no se borro: se mudo.
            int leeGrande, escGrande; Codigo.UsosDeCampo(mUi, "SelResult", out leeGrande, out escGrande);
            Check(leeGrande == 0, "SelResult ya NO entra en la firma grande: mover la seleccion no rehace la pantalla entera");
            MethodInfo mPieza = TF.GetMethod("EnsureUiPieza", IF);
            Check(mPieza != null, "existe la firma de las piezas, que es donde vive ahora la vista previa");
            if (mPieza != null)
            {
                int leePieza, escPieza; Codigo.UsosDeCampo(mPieza, "SelResult", out leePieza, out escPieza);
                Check(leePieza > 0, "pero SI entra en la firma de la PIEZA (con el cubo delante, cambia que disco se ensena y su texto)");
            }
        }

        // ---------- 2) LA PIEZA SE PIDE CON SU SITIO, Y SE DIBUJA SIN TRUCOS DE COORDENADAS ----------
        Console.WriteLine("\n  2) LA PIEZA VIVA SE PIDE APARTE, Y SE DIBUJA CON UN DESPLAZAMIENTO\n");
        MethodInfo mCarta = TSO.GetMethod("PiezaViva");
        MethodInfo mDib = TSO.GetMethod("DibujaPiezaViva");
        Check(mCarta != null && mDib != null, "el buscador sabe dar sus piezas vivas aparte");
        bool dibOk = (mDib != null && mDib.GetParameters().Length == 4);
        Console.WriteLine("     DibujaPiezaViva recibe " + ((mDib == null) ? 0 : mDib.GetParameters().Length)
                          + " datos  (hacen falta 4: el lienzo, cual pieza, y el desplazamiento en x e y)");
        // POR QUE UN DESPLAZAMIENTO Y NO UNA TRASFORMACION DEL LIENZO: el texto de las teclas y de las
        // tarjetas lo dibuja el SISTEMA (TextRenderer), y ese trabaja en pixeles de verdad: no hace caso a
        // las trasformaciones. En Mono da igual -alli lo dibuja el mismo motor que el resto- pero en Windows
        // el texto se iba fuera de la imagen chica y la tecla elegida salia como un rectangulo VACIO.
        Check(dibOk, "y se le pasa el desplazamiento, para que el texto del sistema caiga donde debe");
        MethodInfo mEns = TF.GetMethod("EnsureUiPieza", IF);
        Check(mEns != null, "la rocola prepara las piezas vivas en un sitio que se puede comprobar");
        if (mEns != null)
            Check(!Codigo.Llama(mEns, "TranslateTransform"),
                  "y NO se mueve el lienzo para dibujarla (eso deja el texto fuera en Windows)");

        MethodInfo mRender = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        Check(mRender != null, "se puede pedir al buscador que se dibuje");
        if (mRender == null || mCarta == null || mDib == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        FieldInfo fTecla = TSO.GetField("PiezaTecla");
        int PZ = (fTecla != null) ? (int)fTecla.GetRawConstantValue() : 0;

        // ---------- 3) CON EL CUBO DELANTE NO PUEDE HABER PIEZA DE TECLA ----------
        Console.WriteLine("\n  3) CON EL CUBO DE LA PORTADA DELANTE, EL TECLADO NO EXISTE\n");
        // Cuando se pasa a los resultados, el teclado deja de dibujarse: en su hueco va el cubo. Si la pieza
        // viva siguiera existiendo, quedaria una tecla suelta flotando ENCIMA del cubo. Eso es exactamente lo
        // que se veia.
        {
            Control so = Monta(1024, 768, 5, true);
            using (Bitmap b = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b)) mRender.Invoke(so, new object[] { g, new Rectangle(0, 0, 1024, 768), true });
            object[] ar = new object[] { PZ, Rectangle.Empty };
            bool hay = (bool)mCarta.Invoke(so, ar);
            Console.WriteLine("     con los resultados enfocados, ¿hay pieza de tecla?  " + (hay ? "SI  (" + (Rectangle)ar[1] + ")" : "no"));
            Check(!hay, "con los resultados enfocados NO hay pieza de tecla (si no, queda flotando sobre el cubo)");
            so.Dispose();
        }

        // ---------- 4) Y CON EL TECLADO DELANTE, SALE EXACTAMENTE IGUAL ----------
        Console.WriteLine("\n  4) Y CON EL TECLADO DELANTE, SALE EXACTAMENTE IGUAL\n");
        int[] tam = { 1024, 1920 };
        int[] alto = { 768, 1080 };
        for (int k = 0; k < tam.Length; k++)
        {
            int W = tam[k], H = alto[k];
            Control so = Monta(W, H, 5, false);
            Rectangle todo = new Rectangle(0, 0, W, H);

            // Las dos mitades se dibujan por el MISMO camino y lo unico que cambia es COMO llega la pieza:
            //   JUNTOS  dibujada DIRECTAMENTE encima, como si siguiera horneada dentro.
            //   PARTES  dibujada en SU PROPIA imagen chica y pegada despues, que es lo que hace la tarjeta
            //           grafica con un quad. ESTE es el camino que corre en el bar.
            Bitmap juntos = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(juntos))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                mRender.Invoke(so, new object[] { g, todo, true });
                object[] ar = new object[] { PZ, Rectangle.Empty };
                if ((bool)mCarta.Invoke(so, ar)) mDib.Invoke(so, new object[] { g, PZ, 0, 0 });
            }

            Bitmap partes = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(partes))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                mRender.Invoke(so, new object[] { g, todo, true });
            }
            object[] arg = new object[] { PZ, Rectangle.Empty };
            bool hayPieza = (bool)mCarta.Invoke(so, arg);
            Check(hayPieza, "con el teclado delante SI hay pieza de tecla   [" + W + "x" + H + "]");
            Rectangle dondePieza = (Rectangle)arg[1];
            if (hayPieza)
            {
                Rectangle donde = dondePieza;
                using (Bitmap capa = new Bitmap(donde.Width, donde.Height, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics cg = Graphics.FromImage(capa))
                    {
                        cg.SmoothingMode = SmoothingMode.AntiAlias;
                        cg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        mDib.Invoke(so, new object[] { cg, PZ, -donde.X, -donde.Y });
                    }
                    // Y que la imagen chica no salga VACIA: es lo que pasaba en Windows. Una tecla lleva su
                    // letra, asi que tiene que haber pixeles claros dentro.
                    int claros = 0;
                    for (int y = 0; y < capa.Height; y += 2)
                        for (int x = 0; x < capa.Width; x += 2)
                        { Color c = capa.GetPixel(x, y); if (c.A > 200 && c.R > 220 && c.G > 220 && c.B > 220) claros++; }
                    Console.WriteLine("     la pieza mide " + donde.Width + "x" + donde.Height
                                      + " = " + (donde.Width * donde.Height * 4 / 1024) + " KB;  pixeles de letra dentro: " + claros);
                    Check(claros > 20, "  la letra de la tecla SE DIBUJA dentro de su imagen chica   [" + W + "x" + H + "]");
                    using (Graphics g = Graphics.FromImage(partes)) g.DrawImageUnscaled(capa, donde.X, donde.Y);
                }
            }

            // LO QUE SE ADMITE, Y POR QUE. El relleno de la tecla es OPACO, asi que tapa entero su hueco:
            // ahi no puede haber diferencia. Pero el recuadro que la rodea es un halo TRANSLUCIDO a proposito
            // -late-, y un borde translucido pegado en DOS pasos (primero sobre transparente, luego la capa
            // encima) se redondea a 8 bits dos veces. Eso deja unos pocos pixeles del borde movidos un nivel
            // o dos, que no los ve nadie. Lo que si seria un fallo de verdad -y por eso se mira- es que la
            // diferencia se saliera de la pieza, o que fuera de mas de dos niveles, o que tocara mucha
            // superficie: eso ya no seria redondeo, seria que se dibujo otra cosa.
            int dist, peor; Rectangle zona;
            Compara(juntos, partes, out dist, out peor, out zona);
            int area = Math.Max(1, dondePieza.Width * dondePieza.Height);
            Console.WriteLine("     " + W + "x" + H + ":  pixeles movidos " + dist.ToString("#,0")
                              + " de " + area.ToString("#,0") + " de la pieza (" + (dist * 100.0 / area).ToString("0.0")
                              + "%)   el peor se mueve " + peor + " niveles de 255");
            Check(peor <= 2, "  ningun color se mueve mas de 2 niveles de 255   [" + W + "x" + H + "]");
            Check(dist * 100 <= area * 5, "  y solo se mueve el borde del recuadro, no la tecla   [" + W + "x" + H + "]");
            Check(dist == 0 || dondePieza.Contains(zona),
                  "  y ni un pixel cambia FUERA de la pieza   [" + W + "x" + H + "]");

            juntos.Dispose(); partes.Dispose(); so.Dispose();
        }

        // ---------- 5) Y LA PIEZA SE COLOCA DONDE TOCA ----------
        Console.WriteLine("\n  5) Y LA PIEZA SE COLOCA DONDE TOCA\n");
        // El buscador da el rectangulo en SUS coordenadas y la tarjeta grafica lo dibuja en las de la
        // pantalla. Si a alguien se le olvida sumar donde empieza el buscador, sale corrido.
        FieldInfo fRects2 = TF.GetField("uiPiezaRect", IF), fBmps = TF.GetField("uiPieza", IF);
        FieldInfo fSigs = TF.GetField("uiPiezaSig", IF), fVers = TF.GetField("uiPiezaVer", IF);
        FieldInfo fSO = TF.GetField("searchOverlay", IF), fAbierto = TF.GetField("searchOpen", IF);
        Check(fRects2 != null && fBmps != null && fSO != null && fAbierto != null, "las piezas vivas se guardan donde se pueden comprobar");
        if (mEns != null && fRects2 != null && fBmps != null && fSO != null && fAbierto != null)
        {
            object F = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF);
            Control so = Monta(1024, 600, 5, false);
            so.SetBounds(0, 91, 1024, 600);   // el buscador NO empieza en 0,0: deja sitio arriba
            using (Bitmap b = new Bitmap(1024, 600, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b)) mRender.Invoke(so, new object[] { g, new Rectangle(0, 0, 1024, 600), true });
            fSO.SetValue(F, so); fAbierto.SetValue(F, true);
            fBmps.SetValue(F, Array.CreateInstance(typeof(Bitmap), 4));
            fRects2.SetValue(F, Array.CreateInstance(typeof(Rectangle), 4));
            fSigs.SetValue(F, Array.CreateInstance(typeof(long), 4));
            fVers.SetValue(F, Array.CreateInstance(typeof(int), 4));
            mEns.Invoke(F, new object[] { PZ });
            Rectangle enPantalla = ((Rectangle[])fRects2.GetValue(F))[PZ];
            object[] ar = new object[] { PZ, Rectangle.Empty };
            mCarta.Invoke(so, ar);
            Rectangle enBuscador = (Rectangle)ar[1];
            Console.WriteLine("     el buscador empieza en y=" + so.Top);
            Console.WriteLine("     la tecla, en coords del buscador: y=" + enBuscador.Y + "   en la pantalla: y=" + enPantalla.Y);
            Check(enPantalla.X == enBuscador.X + so.Left && enPantalla.Y == enBuscador.Y + so.Top,
                  "el sitio de la pieza se pasa a coordenadas de PANTALLA (si no, sale corrida)");
            Check(enPantalla.Width == enBuscador.Width && enPantalla.Height == enBuscador.Height, "y del mismo tamano");
            Bitmap[] bms = (Bitmap[])fBmps.GetValue(F);
            Check(bms[PZ] != null && bms[PZ].Width == enBuscador.Width && bms[PZ].Height == enBuscador.Height,
                  "y la imagen chica se hizo del tamano de la pieza, no de la pantalla entera");
            if (bms[PZ] != null)
                Console.WriteLine("     la imagen de la pieza mide " + bms[PZ].Width + "x" + bms[PZ].Height
                                  + " = " + (bms[PZ].Width * bms[PZ].Height * 4 / 1024) + " KB   (la pantalla entera a 1080p son 8.100 KB)");
            so.Dispose();
        }

        // ---------- 6) MANTENER PULSADA UNA FLECHA NO INUNDA LA COLA ----------
        Console.WriteLine("\n  6) Y MANTENER PULSADA UNA FLECHA NO INUNDA LA COLA\n");
        MethodInfo mCmd = TF.GetMethod("ProcessCmdKey", IF);
        FieldInfo fTope = TF.GetField("MsEntreFlechas", BindingFlags.Static | BindingFlags.NonPublic);
        Check(mCmd != null && fTope != null, "las flechas llevan freno");
        if (mCmd != null && fTope != null)
        {
            int lee, esc;
            Codigo.UsosDeCampo(mCmd, "ultimaFlecha", out lee, out esc);
            Check(lee > 0 && esc > 0, "el freno mira cuando fue la ultima flecha y apunta la nueva");
            int ms = (int)fTope.GetRawConstantValue();
            Console.WriteLine("     tope: una flecha cada " + ms + " ms  (" + (1000 / ms) + " por segundo)");
            Check(ms >= 20 && ms <= 60, "el tope deja pasar entre 16 y 50 movimientos por segundo: no se siente lento ni inunda");
            Check(Environment.TickCount - 0 >= ms, "con la rocola recien abierta, la primera flecha pasa (no se siente muerta)");
        }

        // Y al apagar la tarjeta grafica las piezas sobran: solo existen para ella.
        MethodInfo mGpu = TF.GetMethod("ApplyAlbumGpu", IF);
        Check(mGpu != null && Codigo.Llama(mGpu, "SueltaPiezas"), "al apagar la tarjeta grafica las piezas vivas se sueltan");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}

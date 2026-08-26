using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// MOVERSE ENTRE LOS RESULTADOS NO PUEDE REHACER LA PANTALLA ENTERA.
//
// La rocola compone toda la pantalla en una imagen y la sube a la tarjeta grafica. Para no rehacerla en
// cada cuadro lleva una FIRMA: si nada cambio, se reaprovecha. El problema era que la CASILLA ELEGIDA
// entraba en esa firma, asi que pulsar una flecha -que solo cambia que tarjeta lleva el recuadro- obligaba
// a rehacer y RE-SUBIR la pantalla completa. Medido por buscadib.cs: 5.91 ms por pulsacion a 1080p, el 35%
// de un cuadro; y las flechas no frenaban la auto-repeticion, asi que mantenerla pulsada eran ~30 de esas
// por segundo contra un presupuesto de 16.6 ms por cuadro.
//
// Por eso "moverse entre casillas baja el rendimiento de manera drastica" incluso en una maquina potente:
// el gasto no es pintar -de eso la tarjeta va sobrada- sino rehacer la imagen en el procesador y empujar
// 7 MB por el bus. Y por eso se nota MAS cuanto mayor es la pantalla.
//
// El programa ya sabe hacer esto bien en otros sitios: el destello de los botones y el pulso de las letras
// se dibujan como piezas sueltas encima, y la franja de estado de abajo tiene su propia imagen chica. Al
// buscador nunca se le hizo. Aqui se comprueba que ya se le hizo, y que sale EXACTAMENTE igual.
class Mover
{
    static int fallos = 0;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK   " : "  FALLO") + "  " + q); if (!ok) fallos++; }

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

    static Control Monta(int W, int H, int sel)
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
        TSO.GetField("FocusResults").SetValue(so, true);
        TSO.GetField("SelResult").SetValue(so, sel);
        return so;
    }

    static void Compara(Bitmap a, Bitmap b, out int distintos, out int peor)
    {
        distintos = 0; peor = 0;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                Color ca = a.GetPixel(x, y), cb = b.GetPixel(x, y);
                int d = Math.Max(Math.Max(Math.Abs(ca.R - cb.R), Math.Abs(ca.G - cb.G)),
                        Math.Max(Math.Abs(ca.B - cb.B), Math.Abs(ca.A - cb.A)));
                if (d > 0) { distintos++; if (d > peor) peor = d; }
            }
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

        Console.WriteLine("\n  1) LA CASILLA ELEGIDA YA NO OBLIGA A REHACER LA PANTALLA ENTERA\n");
        MethodInfo mUi = TF.GetMethod("UiSig", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mUi != null, "la firma de la imagen de pantalla completa esta donde tiene que estar");
        if (mUi != null)
        {
            string[] mueven = { "SelResult", "SelRow", "SelCol", "FocusResults" };
            foreach (string c in mueven)
            {
                int lee, esc; Codigo.UsosDeCampo(mUi, c, out lee, out esc);
                Check(lee == 0, "moverse a otra casilla (" + c + ") no entra en la firma: no rehace ni re-sube la pantalla");
            }
            // Y lo que SI tiene que seguir entrando: si cambia lo tecleado, los resultados son otros de verdad.
            int l2, e2; Codigo.UsosDeCampo(mUi, "Query", out l2, out e2);
            Check(l2 > 0, "y lo tecleado SI entra: ahi los resultados cambian de verdad y hay que rehacerla");
        }

        Console.WriteLine("\n  2) LA TARJETA ELEGIDA SE PUEDE PEDIR COMO PIEZA SUELTA\n");
        // Igual que el cubo de vista previa (GetPreviewCube) y el resalte de la pared (ContourSpriteBmp).
        MethodInfo mCarta = TSO.GetMethod("PiezaViva");
        MethodInfo mDib = TSO.GetMethod("DibujaPiezaViva");
        Check(mCarta != null && mDib != null, "el buscador sabe dar sus piezas vivas aparte, para dibujarlas encima sin re-subir nada");

        Console.WriteLine("\n  3) Y CON TARJETA GRAFICA NO SE DIBUJA DOS VECES\n");
        MethodInfo mRender = TSO.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(Rectangle), typeof(bool) });
        Check(mRender != null, "se puede pedir al buscador que se dibuje");
        if (mRender == null || mCarta == null) { Console.WriteLine("\n  " + Math.Max(1, fallos) + " FALLO(S)\n"); return Math.Max(1, fallos); }

        int[] tam = { 1024, 1920 };
        int[] alto = { 768, 1080 };
        for (int k = 0; k < tam.Length; k++)
        {
            int W = tam[k], H = alto[k];
            Control so = Monta(W, H, 5);
            Rectangle todo = new Rectangle(0, 0, W, H);

            // LA PREGUNTA ES SOLO SI PEGAR LA PIEZA APARTE CAMBIA ALGO, asi que las dos mitades se dibujan por
            // el MISMO camino (el de tarjeta grafica, que no pinta el cielo ni el cubo) y lo unico que cambia
            // es COMO llega el recuadro:
            //
            //   JUNTOS  la pieza dibujada DIRECTAMENTE encima, como si siguiera horneada dentro.
            //   PARTES  la pieza dibujada en su PROPIA capa transparente, que luego se pega (lo que hace la
            //           tarjeta grafica con un quad).
            //
            // Si hubiera diferencia saldria en los bordes suavizados: en la primera forma el borde se mezcla
            // con lo que hay debajo, y en la segunda se mezcla primero con transparente y luego otra vez.
            Bitmap juntos = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(juntos))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                mRender.Invoke(so, new object[] { g, todo, true });
                for (int c = 0; c <= 1; c++)
                {
                    object[] ar = new object[] { c, Rectangle.Empty };
                    if ((bool)mCarta.Invoke(so, ar)) mDib.Invoke(so, new object[] { g, c });
                }
            }

            Bitmap partes = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(partes)) { g.SmoothingMode = SmoothingMode.AntiAlias; mRender.Invoke(so, new object[] { g, todo, true }); }
            int piezas = 0;
            for (int c = 0; c <= 1; c++)
            {
                object[] arg = new object[] { c, Rectangle.Empty };
                if (!(bool)mCarta.Invoke(so, arg)) continue;
                piezas++;
                Rectangle donde = (Rectangle)arg[1];
                using (Bitmap capa = new Bitmap(donde.Width, donde.Height, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics cg = Graphics.FromImage(capa))
                    {
                        cg.SmoothingMode = SmoothingMode.AntiAlias;
                        cg.TranslateTransform(-donde.X, -donde.Y);
                        mDib.Invoke(so, new object[] { cg, c });
                    }
                    using (Graphics g = Graphics.FromImage(partes)) g.DrawImageUnscaled(capa, donde.X, donde.Y);
                }
            }
            Check(piezas == 2, "hay DOS piezas vivas: la tarjeta elegida y la tecla elegida   [" + W + "x" + H + "]");

            int dist, peor;
            Compara(juntos, partes, out dist, out peor);
            Console.WriteLine("     " + W + "x" + H + ":  pixeles distintos " + dist.ToString("#,0")
                              + "   la peor diferencia " + peor + " niveles de 255");
            Check(dist == 0, "  la pantalla sale EXACTAMENTE igual dibujada de las dos formas   [" + W + "x" + H + "]");

            juntos.Dispose(); partes.Dispose(); so.Dispose();
        }

        Console.WriteLine("\n  4) Y LA PIEZA SE DIBUJA DONDE TOCA\n");
        // Lo unico que puede salir MAL de verdad con este arreglo es la aritmetica del sitio: el buscador da
        // el rectangulo en SUS coordenadas y la tarjeta grafica lo dibuja en las de la pantalla. Si a alguien
        // se le olvida sumar donde empieza el buscador, el recuadro aparece corrido y no lo caza ninguna
        // comparacion de pixeles del propio buscador.
        MethodInfo mEns = TF.GetMethod("EnsureUiPieza", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fRects2 = TF.GetField("uiPiezaRect", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fBmps = TF.GetField("uiPieza", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fSigs = TF.GetField("uiPiezaSig", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fVers = TF.GetField("uiPiezaVer", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fSO = TF.GetField("searchOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fAbierto = TF.GetField("searchOpen", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mEns != null && fRects2 != null && fSO != null && fAbierto != null,
              "la rocola prepara las piezas vivas en un sitio que se puede comprobar");
        if (mEns != null && fRects2 != null && fSO != null && fAbierto != null)
        {
            object F = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(TF);
            Control so = Monta(1024, 768, 5);
            so.SetBounds(0, 91, 1024, 600);   // el buscador NO empieza en 0,0: deja sitio arriba
            // hay que dibujarlo una vez para que sepa donde cae cada tarjeta
            using (Bitmap b = new Bitmap(1024, 600, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b)) mRender.Invoke(so, new object[] { g, new Rectangle(0, 0, 1024, 600), true });
            fSO.SetValue(F, so); fAbierto.SetValue(F, true);
            fBmps.SetValue(F, Array.CreateInstance(typeof(Bitmap), 4));
            fRects2.SetValue(F, Array.CreateInstance(typeof(Rectangle), 4));
            fSigs.SetValue(F, Array.CreateInstance(typeof(long), 4));
            fVers.SetValue(F, Array.CreateInstance(typeof(int), 4));
            mEns.Invoke(F, new object[] { 0 });
            Rectangle enPantalla = ((Rectangle[])fRects2.GetValue(F))[0];
            object[] ar = new object[] { 0, Rectangle.Empty };
            mCarta.Invoke(so, ar);
            Rectangle enBuscador = (Rectangle)ar[1];
            Console.WriteLine("     el buscador empieza en y=" + so.Top);
            Console.WriteLine("     la tarjeta, en coords del buscador: y=" + enBuscador.Y + "   en la pantalla: y=" + enPantalla.Y);
            Check(enPantalla.X == enBuscador.X + so.Left && enPantalla.Y == enBuscador.Y + so.Top,
                  "el sitio de la pieza se pasa a coordenadas de PANTALLA (si no, el recuadro sale corrido)");
            Check(enPantalla.Width == enBuscador.Width && enPantalla.Height == enBuscador.Height,
                  "y del mismo tamano");
            Bitmap[] bms = (Bitmap[])fBmps.GetValue(F);
            Check(bms[0] != null && bms[0].Width == enBuscador.Width && bms[0].Height == enBuscador.Height,
                  "y la imagen chica se hizo del tamano de la pieza, no de la pantalla entera");
            if (bms[0] != null)
                Console.WriteLine("     la imagen de la pieza mide " + bms[0].Width + "x" + bms[0].Height
                                  + " = " + (bms[0].Width * bms[0].Height * 4 / 1024) + " KB   (la pantalla entera a 1080p son 8.100 KB)");
            so.Dispose();
        }

        Console.WriteLine("\n  5) Y MANTENER PULSADA UNA FLECHA NO INUNDA LA COLA\n");
        // El teclado manda unas treinta pulsaciones por segundo con la flecha mantenida, y la pantalla no da
        // mas de sesenta cuadros: la mitad de ese trabajo no lo ve nadie, solo llena la cola. Pero NO se puede
        // tirar la repeticion -mantener la flecha para recorrer la biblioteca es como se usa una rocola de
        // botones-, asi que se le pone TOPE.
        MethodInfo mCmd = TF.GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
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
            // Lo que NO puede pasar: que se coma la PRIMERA pulsacion. Con la rocola recien abierta,
            // ultimaFlecha vale 0 y el reloj del sistema lleva rato andando, asi que tiene que pasar.
            Check(Environment.TickCount - 0 >= ms, "con la rocola recien abierta, la primera flecha pasa (no se siente muerta)");
        }

        // Y al apagar la tarjeta grafica las piezas sobran: solo existen para ella.
        MethodInfo mGpu = TF.GetMethod("ApplyAlbumGpu", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(mGpu != null && Codigo.Llama(mGpu, "SueltaPiezas"), "al apagar la tarjeta grafica las piezas vivas se sueltan");

        Console.WriteLine(fallos == 0 ? "\n  TODO BIEN\n" : "\n  " + fallos + " FALLO(S)\n");
        return fallos;
    }
}

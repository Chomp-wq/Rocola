using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// LA PANTALLA ENTERA, PARA MIRARLA.
//
// No es una bateria: no dice OK ni FALLO. Arma la pantalla de 1024x768 con los controles DE VERDAD sacados
// del ejecutable -las instrucciones, el encabezado, la barra del abecedario, la pared de discos y la franja
// de abajo- y la guarda en un PNG. Sirve para lo que ninguna cuenta sustituye: verla.
//
// Las medidas son las mismas que calcula LayoutControls a 1024x768.
//
//    mono pantalla.exe rocola.exe pantalla.png
class Pantalla
{
    static Type TF;
    static Type Nes(string n) { return TF.GetNestedType(n, BindingFlags.NonPublic | BindingFlags.Public); }
    static void Pon(Type t, object o, string campo, object v)
    {
        FieldInfo f = t.GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(o, v);
    }
    static FontFamily Fam()
    {
        string[] p = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string q in p) try { return new FontFamily(q); } catch { }
        return FontFamily.GenericSansSerif;
    }
    static void Dibuja(Graphics g, Control c, MethodInfo m, object[] extra)
    {
        GraphicsState st = g.Save();
        g.TranslateTransform(c.Left, c.Top);
        object[] arg = new object[1 + (extra == null ? 0 : extra.Length)];
        arg[0] = g;
        if (extra != null) Array.Copy(extra, 0, arg, 1, extra.Length);
        try { m.Invoke(c, arg); } catch (Exception ex) { Console.WriteLine("  (" + c.GetType().Name + ": " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")"); }
        g.Restore(st);
    }
    static Image portada;
    static Image Tapa<T>(T a) { return portada; }

    static int Main(string[] argv)
    {
        string exe = (argv.Length > 0) ? argv[0] : "rocola.exe";
        string sal = (argv.Length > 1) ? argv[1] : "pantalla.png";
        Assembly asm = Assembly.LoadFrom(exe);
        TF = asm.GetType("Rocola.Form1", true);
        FontFamily fam = Fam();

        const int W = 1024, H = 768, pad = 16;
        int contentW = W - pad * 2;
        Bitmap lienzo = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(lienzo);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        // El fondo de verdad lo pinta la propia ventana (cielo estrellado y aurora); aqui va liso, que lo que
        // se quiere mirar es como quedan los controles unos con otros.
        using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                   Color.FromArgb(255, 9, 12, 26), Color.FromArgb(255, 16, 20, 44), LinearGradientMode.Vertical))
            g.FillRectangle(lb, 0, 0, W, H);

        // ---------- la fila de arriba: instrucciones + encabezado ----------
        // La fila de arriba: instrucciones a la izquierda, la casilla de BUSCAR en medio y el encabezado a
        // la derecha. Las mismas cuentas que LayoutControls.
        Type THelp = Nes("HelpBar"), TCab = Nes("HeaderView"), TBus = Nes("BuscaView");
        int filaH = H * 44 / 768; if (filaH < 44) filaH = 44; else if (filaH > 76) filaH = 76;
        int helpW = Math.Max(120, Math.Min(400, (W - pad) - pad));
        if (THelp != null)
        {
            MethodInfo mAnchoAy = THelp.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public);
            if (mAnchoAy != null) helpW = Math.Max(120, Math.Min((int)mAnchoAy.Invoke(null, new object[] { filaH }), (W - pad) - pad));
            Control hb = (Control)Activator.CreateInstance(THelp, true);
            hb.Font = new Font(fam, 9f); hb.SetBounds(pad, 8, helpW, filaH);
            MethodInfo m = THelp.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
            if (m != null) Dibuja(g, hb, m, new object[] { false });
        }
        int busX = pad + helpW + 12, busW = 0;
        if (TBus != null)
        {
            MethodInfo mAnchoBus = TBus.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            busW = Math.Min((int)mAnchoBus.Invoke(null, new object[] { filaH }), Math.Max(0, (W - pad) - busX));
            int centrado = (W - busW) / 2;
            if (centrado > busX) busX = centrado;
            if (busX + busW > W - pad) busX = Math.Max(pad + helpW + 12, W - pad - busW);
            Control bv = (Control)Activator.CreateInstance(TBus, true);
            bv.Font = new Font(fam, 9f); bv.SetBounds(busX, 8, busW, filaH);
            MethodInfo m = TBus.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
            if (m != null) Dibuja(g, bv, m, new object[] { false });
        }
        if (TCab != null)
        {
            int headX = busX + busW + 12;
            Control cab = (Control)Activator.CreateInstance(TCab, true);
            cab.Font = new Font(fam, 9f); cab.SetBounds(headX, 8, Math.Max(0, (W - pad) - headX), 34);
            Pon(TCab, cab, "Section", "LETRA M"); Pon(TCab, cab, "Page", 2); Pon(TCab, cab, "Total", 9);
            Dibuja(g, cab, TCab.GetMethod("RenderContent", new Type[] { typeof(Graphics) }), null);
        }

        // ---------- la barra del abecedario (con la biblioteca a medias: solo las que tienen discos) ----------
        Type TLet = Nes("LetterBar");
        int lettersY = 8 + filaH + 4, lettersH = H * 40 / 768;
        int porCasilla = (contentW * 8) / (29 * 10);
        if (porCasilla > lettersH) lettersH = porCasilla;
        if (lettersH < 40) lettersH = 40; else if (lettersH > 104) lettersH = 104;
        if (TLet != null)
        {
            string letras = "\u2605#ABCDGJLMPRSTVZ\u25BA";   // lo que dejaria LetrasVisibles con esta biblioteca
            Control lb2 = (Control)Activator.CreateInstance(TLet, true);
            lb2.Font = new Font(fam, 9f); lb2.SetBounds(pad, lettersY, contentW, lettersH);
            HashSet<char> disp = new HashSet<char>(); foreach (char c in letras) disp.Add(c);
            Dictionary<char, int> cuentas = new Dictionary<char, int>();
            Random r = new Random(7);
            foreach (char c in letras) cuentas[c] = 12 + r.Next(180);
            Pon(TLet, lb2, "Letters", letras); Pon(TLet, lb2, "Available", disp);
            Pon(TLet, lb2, "Counts", cuentas); Pon(TLet, lb2, "cur", 'M'); Pon(TLet, lb2, "Phase", 0.6d);
            MethodInfo m = TLet.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
            if (m != null) Dibuja(g, lb2, m, new object[] { false });
        }

        // ---------- la franja de abajo ----------
        Font fTit = new Font(fam, 15f, FontStyle.Bold), fArt = new Font(fam, 11.5f, FontStyle.Bold);
        Font fSig = new Font(fam, 10.5f, FontStyle.Bold), fRel = new Font(fam, 13f, FontStyle.Bold);
        int hTit = Math.Max(24, fTit.Height + 1), hArt = Math.Max(18, fArt.Height + 1), hSig = Math.Max(17, fSig.Height + 1);
        int progH = 12, tarjetaH = hTit + hArt + hSig + 4 + progH + 2;
        int tarjetaY = H - pad - tarjetaH, cubeSize = tarjetaH;
        MethodInfo mAltoPast = TF.GetMethod("AltoPastilla", BindingFlags.Static | BindingFlags.NonPublic);
        int pastH = (mAltoPast != null) ? (int)mAltoPast.Invoke(null, new object[] { tarjetaH }) : 26;

        // la caratula de lo que suena
        portada = new Bitmap(360, 360, PixelFormat.Format32bppPArgb);
        using (Graphics pg = Graphics.FromImage(portada))
        {
            pg.Clear(Color.FromArgb(255, 26, 34, 74));
            for (int i = 0; i < 26; i++)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 120 + i * 4, 190 - i * 2, 255)))
                    pg.FillEllipse(b, i * 13 - 40, i * 11 - 30, 150, 150);
        }
        g.DrawImage(portada, new Rectangle(pad, tarjetaY, cubeSize, cubeSize));

        Type TPas = Nes("PastillaView");
        MethodInfo mAncho = TPas.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        int wCred = (int)mAncho.Invoke(null, new object[] { "Creditos", "3", pastH, fam });
        int wCola = (int)mAncho.Invoke(null, new object[] { "En cola", "4", pastH, fam });
        string[] etq = { "Creditos", "En cola" }; string[] val = { "3", "4" };
        Color[] ac = { Color.FromArgb(120, 240, 140), Color.FromArgb(140, 205, 255) };
        int[] px = { W - pad - wCred, W - pad - wCred - 10 - wCola };
        int[] pw = { wCred, wCola };
        for (int k = 0; k < 2; k++)
        {
            Control c = (Control)Activator.CreateInstance(TPas, true);
            c.Font = new Font(fam, 9f); c.SetBounds(px[k], tarjetaY, pw[k], pastH);
            Pon(TPas, c, "Etiqueta", etq[k]); Pon(TPas, c, "Valor", val[k]); Pon(TPas, c, "Acento", ac[k]);
            Dibuja(g, c, TPas.GetMethod("RenderContent", new Type[] { typeof(Graphics) }), null);
        }

        int txtX = pad + cubeSize + 14;
        int mejRowW = Math.Max(120, (W - pad - wCred - wCola - 24) - txtX);
        string titulo = "Mi Gusto Es (En Vivo desde Guadalajara)";
        string artista = "Mariachi Vargas de Tecalitlan";
        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 120, 230, 150)))
        {
            g.DrawString(titulo, fTit, b, new RectangleF(txtX, tarjetaY, mejRowW, hTit), StringFormat.GenericTypographic);
            g.DrawString(artista, fArt, b, new RectangleF(txtX, tarjetaY + hTit, mejRowW, hArt), StringFormat.GenericTypographic);
        }

        // la pastilla del genero, detras del artista
        Type TGen = Nes("GeneroView");
        if (TGen != null)
        {
            Control gv = (Control)Activator.CreateInstance(TGen, true);
            gv.Font = new Font(fam, 10f, FontStyle.Bold);
            TGen.GetProperty("Texto").SetValue(gv, "Regional", null);
            MethodInfo mCol = TF.GetMethod("ColorDeEtiqueta", BindingFlags.Static | BindingFlags.NonPublic);
            Pon(TGen, gv, "Tinte", (Color)mCol.Invoke(null, new object[] { "Regional" }));
            int gw = (int)TGen.GetMethod("AnchoQuePide").Invoke(gv, null);
            int gh = Math.Min((int)TGen.GetMethod("AltoQuePide").Invoke(gv, null), hArt);
            int tw = TextRenderer.MeasureText(artista, fArt).Width;
            gv.SetBounds(txtX + tw + 12, tarjetaY + hTit + (hArt - gh) / 2, gw, gh);
            Dibuja(g, gv, TGen.GetMethod("RenderContent", new Type[] { typeof(Graphics) }), null);
            Console.WriteLine("  pastilla de genero: " + gw + "x" + gh + " px en x=" + gv.Left);
        }
        else Console.WriteLine("  (este ejecutable no tiene pastilla de genero)");

        int timeW = 130, anchoDer = W - pad - txtX;
        int progW = Math.Max(50, anchoDer - timeW - 12);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 150, 200, 255)))
            g.DrawString("Sigue:  Cielito Lindo - Los Cadetes del Norte", fSig, b,
                         new RectangleF(txtX, tarjetaY + hTit + hArt, progW, hSig), StringFormat.GenericTypographic);
        int filaY = tarjetaY + hTit + hArt + hSig + 4;
        Type TProg = Nes("ProgressView");
        if (TProg != null)
        {
            Control pv = (Control)Activator.CreateInstance(TProg, true);
            pv.SetBounds(txtX, filaY, progW, progH);
            TProg.GetProperty("Value").SetValue(pv, 0.42, null);
            TProg.GetProperty("Accent").SetValue(pv, Color.FromArgb(225, 120, 30), null);
            Dibuja(g, pv, TProg.GetMethod("RenderContent", new Type[] { typeof(Graphics) }), null);
        }
        using (SolidBrush b = new SolidBrush(Color.White))
            g.DrawString("1:47 / 4:12", fRel, b, new RectangleF(txtX + progW + 12, filaY - 6, timeW, 24), StringFormat.GenericTypographic);

        // ---------- la pared de discos ----------
        Type TAV = Nes("AlbumView"), TAlb = Nes("Album");
        int albumY = lettersY + lettersH + 10;
        int albumH = Math.Max(120, (tarjetaY - 12) - albumY);
        if (TAV != null && TAlb != null)
        {
            Control av = (Control)Activator.CreateInstance(TAV, true);
            av.Font = new Font(fam, 10f, FontStyle.Bold);
            av.SetBounds(pad, albumY, contentW, albumH);
            Type tFunc = typeof(Func<,>).MakeGenericType(TAlb, typeof(Image));
            TAV.GetField("GetCover").SetValue(av, Delegate.CreateDelegate(tFunc,
                typeof(Pantalla).GetMethod("Tapa", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(TAlb)));
            IList pag = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TAlb));
            string[] art = { "Mariachi Vargas", "Los Tigres del Norte", "Banda El Recodo", "Vicente Fernandez",
                             "Los Bukis", "Juan Gabriel", "Grupo Firme", "Ana Gabriel", "Chalino Sanchez",
                             "Los Angeles Azules", "Marco Antonio Solis", "La Arrolladora" };
            for (int i = 0; i < art.Length; i++)
            {
                object a = Activator.CreateInstance(TAlb, true);
                TAlb.GetField("Folder").SetValue(a, @"C:\Musica\" + art[i]);
                TAlb.GetField("Artist").SetValue(a, art[i]);
                TAlb.GetField("Name").SetValue(a, (i % 3 == 0) ? "Sus Mejores Exitos" : "");
                TAlb.GetField("Letter").SetValue(a, art[i][0]);
                pag.Add(a);
            }
            TAV.GetField("Page").SetValue(av, pag);
            MethodInfo mReb = TAV.GetMethod("RebuildCubes", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo fPend = TAV.GetField("cubesPending", BindingFlags.Instance | BindingFlags.NonPublic);
            object[] wh = new object[] { contentW, albumH };
            int v = 0; do { mReb.Invoke(av, wh); v++; } while (fPend != null && (bool)fPend.GetValue(av) && v < 60);
            MethodInfo mPaint = TAV.GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic);
            GraphicsState st = g.Save();
            g.TranslateTransform(pad, albumY);
            using (PaintEventArgs pe = new PaintEventArgs(g, new Rectangle(0, 0, contentW, albumH)))
                try { mPaint.Invoke(av, new object[] { pe }); }
                catch (Exception ex) { Console.WriteLine("  (pared de discos: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message) + ")"); }
            g.Restore(st);
        }

        // ---------- la guia, la frase que sale sola cuando el cliente se queda parado ----------
        // Se dibuja SIEMPRE en esta foto (en la rocola de verdad solo sale tras unos segundos sin tocar
        // nada): esta foto es justo para ver como queda con todo lo demas.
        Type TGui = Nes("GuiaView");
        if (TGui != null)
        {
            int altoG = H * 40 / 768; if (altoG < 34) altoG = 34; else if (altoG > 66) altoG = 66;
            string frase = "Muevete hasta un disco y aprieta el VERDE";
            int anG = (int)TGui.GetMethod("Ancho", BindingFlags.Static | BindingFlags.Public).Invoke(
                          null, new object[] { frase, 1, altoG, fam });
            Control gui = (Control)Activator.CreateInstance(TGui, true);
            gui.Font = new Font(fam, 9f, FontStyle.Bold);
            Pon(TGui, gui, "Texto", frase); Pon(TGui, gui, "Boton", 1);
            gui.SetBounds((W - anG) / 2, tarjetaY - 10 - altoG - 12, anG, altoG);
            MethodInfo mg = TGui.GetMethod("RenderContent", new Type[] { typeof(Graphics), typeof(bool) });
            if (mg != null) Dibuja(g, gui, mg, new object[] { false });
        }

        Console.WriteLine("  capsulas: " + pastH + " px de alto   tarjeta de abajo: " + tarjetaH + " px");
        Console.WriteLine("  barra del abecedario: " + lettersH + " px de alto, " + contentW + " px de ancho");
        g.Dispose();
        lienzo.Save(sal, ImageFormat.Png);
        Console.WriteLine("  guardado en " + sal);
        return 0;
    }
}

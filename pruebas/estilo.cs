using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// LA PANTALLA VIVA, TANDA 5: EL ESTILO GENERAL (Rocola/EstiloGpu.cs; solo tarjeta grafica)
//
// Lo que vigila:
//  1) LAS ETIQUETAS DE VERDAD NO SE TOCAN. La letra de lejos solo existe en la imagen que hornea la tarjeta: las
//     etiquetas (letra, sitio, color) y el acomodo de la ventana siguen igual, asi que lo que pinte el procesador sale
//     como siempre. Apagada, la franja se hornea como siempre. Sin tarjeta grafica, solo la pestaña.
//  2) LA LETRA PARA LEER DE LEJOS: el titulo crece (lo mas que quepa, entre 1,45 y 1,15), el renglon del artista baja
//     con su pastilla y su letrero (y el brillo del letrero con el), y lo que sigue pasa a ese renglon, a la derecha,
//     en un gris claro y recortado con puntos suspensivos si no cabe. Nada se sale de la franja ni se pisa.
//  3) EL COLOR DEL GENERO EN TODO: el resplandor detras de las portadas (centrado, de su color, respirando con su propio
//     reloj y sin saltos), el pulso de la letra elegida con el color de su genero, y que la tarjeta se entera.
//  4) Ctrl+F3 -> Pantalla: la tarjeta ESTILO GENERAL en la tercera hoja, sus 4 combinaciones y el boton de fabrica.
//  5) Lo que cuesta (Mono: para comparar, no son los milisegundos del bar).
class Estilo
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Type form, gpu, pvT;
    static Assembly asm;
    static int fallos, oks;
    static string dirPng;
    static void Check(bool ok, string q) { Console.WriteLine((ok ? "  OK    " : "  FALLO ") + q); if (ok) oks++; else fallos++; }
    static void Info(string q) { Console.WriteLine("        " + q); }
    static string Fm(double v, string f) { return v.ToString(f, System.Globalization.CultureInfo.InvariantCulture); }
    static FieldInfo Campo(Type t, string n) { for (Type x = t; x != null; x = x.BaseType) { FieldInfo fi = x.GetField(n, F | BindingFlags.DeclaredOnly); if (fi != null) return fi; } return null; }
    static object Get(object o, string n) { return Campo(o.GetType(), n).GetValue(o); }
    static void Set(object o, string n, object v) { Campo(o.GetType(), n).SetValue(o, v); }
    static MethodInfo M(Type t, string n) { return t.GetMethod(n, F); }
    static object St(Type t, string n, params object[] a) { return M(t, n).Invoke(null, a); }
    static object Run(object o, string n, params object[] a) { return o.GetType().GetMethod(n, F).Invoke(o, a); }
    static Control Find(Control p, string name) { foreach (Control c in p.Controls) { if (c.Text == name) return c; Control r = Find(c, name); if (r != null) return r; } return null; }
    static bool Parecido(Color p, Color c, int tol)
    {
        if (p.A < 120) return false;
        int r = p.R * 255 / Math.Max(1, (int)p.A), g = p.G * 255 / Math.Max(1, (int)p.A), b = p.B * 255 / Math.Max(1, (int)p.A);
        return Math.Abs(r - c.R) <= tol && Math.Abs(g - c.G) <= tol && Math.Abs(b - c.B) <= tol;
    }
    static int Cuenta(Bitmap b, Rectangle r, Color c, int tol)
    {
        int n = 0; r.Intersect(new Rectangle(0, 0, b.Width, b.Height));
        for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) if (Parecido(b.GetPixel(x, y), c, tol)) n++;
        return n;
    }
    // Filas (arriba y abajo) donde hay tinta de un color en una caja: el alto de lo escrito.
    static void Alto(Bitmap b, Rectangle r, Color c, int tol, out int y0, out int y1)
    {
        y0 = -1; y1 = -1; r.Intersect(new Rectangle(0, 0, b.Width, b.Height));
        for (int y = r.Top; y < r.Bottom; y++)
            for (int x = r.Left; x < r.Right; x++)
                if (Parecido(b.GetPixel(x, y), c, tol)) { if (y0 < 0) y0 = y; y1 = y; break; }
    }
    static Font Negrita(float pt) { return new Font(FontFamily.GenericSansSerif, pt, FontStyle.Bold); }
    static Label Etiqueta(string texto, float pt, Color c, Rectangle r, ContentAlignment al)
    {
        Label l = new Label(); l.Text = texto; l.Font = Negrita(pt); l.ForeColor = c; l.Bounds = r; l.TextAlign = al; l.UseMnemonic = false;
        return l;
    }
    static readonly Color Blanco = Color.FromArgb(236, 242, 255), Verde = Color.FromArgb(120, 230, 165), GrisSigue = Color.FromArgb(150, 165, 200);
    // Una franja de lo que suena como la acomoda la rocola en 1024x768 (los mismos renglones, medidos con su letra).
    static object Franja(string titulo, bool lejos) { return Franja(titulo, lejos, "Sigue:  Cielito Lindo"); }
    static object Franja(string titulo, bool lejos, string sigue)
    {
        object f = Codigo.FormSinEncender(form); Codigo.PonCerrojos(f); Codigo.PonColecciones(f);
        Set(f, "gpuPanel", Activator.CreateInstance(gpu, true)); Set(f, "gpuEnabled", true);
        Set(f, "letraLejos", lejos); Set(f, "colorGenero", true);
        int txtX = 110, tarjetaY = 668;
        Label tit = Etiqueta(titulo, 15f, Blanco, Rectangle.Empty, ContentAlignment.MiddleLeft);
        int hTit = Math.Max(24, tit.Font.Height + 1);
        Label art = Etiqueta("Vicente Fernandez", 11.5f, Verde, Rectangle.Empty, ContentAlignment.MiddleLeft);
        int hArt = Math.Max(18, art.Font.Height + 1);
        Label sig = Etiqueta(sigue, 10.5f, GrisSigue, Rectangle.Empty, ContentAlignment.MiddleLeft);
        int hSig = Math.Max(17, sig.Font.Height + 1);
        tit.Bounds = new Rectangle(txtX, tarjetaY, 600, hTit);
        art.Bounds = new Rectangle(txtX, tarjetaY + hTit, 400, hArt);
        sig.Bounds = new Rectangle(txtX, tarjetaY + hTit + hArt, 700, hSig);
        int filaY = tarjetaY + hTit + hArt + hSig + 4;
        Label tiempo = Etiqueta("1:47 / 4:12", 13f, Color.White, new Rectangle(txtX + 700 + 12, filaY - 6, 130, 24), ContentAlignment.TopLeft);
        Control cred = (Control)Activator.CreateInstance(pvT, true), cola = (Control)Activator.CreateInstance(pvT, true);
        cred.Bounds = new Rectangle(1024 - 12 - 120, tarjetaY, 120, 36); cola.Bounds = new Rectangle(1024 - 12 - 120 - 10 - 110, tarjetaY, 110, 36);
        Label mej = Etiqueta("MEJORADA", 12f, Color.Silver, new Rectangle(txtX + 180, art.Top, 90, 22), ContentAlignment.MiddleLeft);
        mej.Visible = false;
        Set(f, "lblNowTop", tit); Set(f, "lblNow", art); Set(f, "lblNext", sig); Set(f, "lblTime", tiempo);
        Set(f, "pastCred", cred); Set(f, "pastCola", cola); Set(f, "lblMejorada", mej); Set(f, "pastGenero", null);
        Control progress = (Control)Activator.CreateInstance(Campo(form, "progress").FieldType, true);
        progress.Bounds = new Rectangle(txtX, filaY, 700, 12); Set(f, "progress", progress);
        return f;
    }
    static Bitmap PintaFranja(object f)
    {
        Bitmap b = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            if (!(bool)Run(f, "FranjaLejos", g))
            {
                // la de siempre: los mismos cinco pasos que EnsureUiBotOverlay
                MethodInfo dl = null;
                foreach (MethodInfo m in form.GetMethods(F)) if (m.Name == "DrawLabelOverlay" && m.GetParameters().Length == 2) dl = m;
                dl.Invoke(null, new object[] { g, Get(f, "lblNowTop") });
                dl.Invoke(null, new object[] { g, Get(f, "lblNow") });
                dl.Invoke(null, new object[] { g, Get(f, "lblNext") });
            }
        }
        return b;
    }

    // ================================================================================================
    //  1) LAS ETIQUETAS DE VERDAD NO SE TOCAN
    // ================================================================================================
    static void NoSeTocan()
    {
        Console.WriteLine("\n  1) LAS ETIQUETAS DE VERDAD NO SE TOCAN\n");
        object f = Franja("Volver Volver", true);
        Label tit = (Label)Get(f, "lblNowTop"), art = (Label)Get(f, "lblNow"), sig = (Label)Get(f, "lblNext");
        Rectangle[] antes = { tit.Bounds, art.Bounds, sig.Bounds }; Font[] fAntes = { tit.Font, art.Font, sig.Font }; Color[] cAntes = { tit.ForeColor, art.ForeColor, sig.ForeColor };
        using (Bitmap b = PintaFranja(f)) { }
        bool igual = tit.Bounds == antes[0] && art.Bounds == antes[1] && sig.Bounds == antes[2] && tit.Font == fAntes[0] && art.Font == fAntes[1] && sig.Font == fAntes[2]
                     && tit.ForeColor == cAntes[0] && art.ForeColor == cAntes[1] && sig.ForeColor == cAntes[2];
        Check(igual, "pintar la franja de lejos no toca las etiquetas: ni su letra, ni su sitio, ni su color");
        Check(!Codigo.Llama(M(form, "LayoutControls"), "LetraLejosActiva") && !Codigo.Llama(M(form, "LayoutControls"), "BajaArtistaLejos")
              && !Codigo.Llama(M(form, "PositionMejorada"), "BajaArtistaLejos"),
              "y el acomodo de la ventana no sabe nada de ella: lo que pinte el procesador sale como siempre");
        object fo = Franja("Volver Volver", false);
        bool dijo;
        using (Bitmap b = new Bitmap(10, 10)) using (Graphics g = Graphics.FromImage(b)) dijo = (bool)Run(fo, "FranjaLejos", g);
        Set(fo, "letraLejos", true); Set(fo, "gpuEnabled", false);
        bool sinTarjeta;
        using (Bitmap b = new Bitmap(10, 10)) using (Graphics g = Graphics.FromImage(b)) sinTarjeta = (bool)Run(fo, "FranjaLejos", g);
        Check(!dijo && !sinTarjeta && Codigo.Llama(M(form, "EnsureUiBotOverlay"), "FranjaLejos") && Codigo.Llama(M(form, "UiBotSig"), "LetraLejosActiva"),
              "apagada (o con la tarjeta apagada) la franja se hornea como siempre, y al cambiarla se rehace");
    }

    // ================================================================================================
    //  2) LA LETRA PARA LEER DE LEJOS
    // ================================================================================================
    static void Lejos()
    {
        Console.WriteLine("\n  2) LA LETRA PARA LEER DE LEJOS\n");
        using (Bitmap b1 = new Bitmap(10, 10))
        using (Graphics g = Graphics.FromImage(b1))
        using (Font ft = Negrita(15f))
        {
            MethodInfo esc = M(form, "EscalaTituloLejos");
            string med = "Contrabando y Traicion (En Vivo)";
            float w1 = g.MeasureString(med, ft, PointF.Empty, StringFormat.GenericTypographic).Width;   // su ancho sin crecer
            float corto = (float)esc.Invoke(null, new object[] { g, "El Rey", ft, 600f, 44f });
            float medio = (float)esc.Invoke(null, new object[] { g, med, ft, w1 * 1.32f, 44f });      // cabe hasta 1,32: crece 1,30
            float justo = (float)esc.Invoke(null, new object[] { g, med, ft, w1 * 1.18f, 44f });      // cabe hasta 1,18: crece 1,15
            float largo = (float)esc.Invoke(null, new object[] { g, "Mi Gusto Es (En Vivo desde Guadalajara, Edicion Aniversario Remasterizada)", ft, 600f, 44f });
            float bajo = (float)esc.Invoke(null, new object[] { g, "El Rey", ft, 600f, 26f });
            Info("crece: corto " + Fm(corto, "0.00") + ", mediano " + Fm(medio, "0.00") + " (y " + Fm(justo, "0.00") + " con menos sitio), largo " + Fm(largo, "0.00")
                 + " (con poco alto: " + Fm(bajo, "0.00") + ")");
            Check(Math.Abs(corto - 1.45f) < .001f && Math.Abs(medio - 1.30f) < .01f && Math.Abs(justo - 1.15f) < .01f && Math.Abs(largo - 1.15f) < .001f
                  && bajo <= 26f / ft.GetHeight(g) + .001f && bajo >= 1f,
                  "el titulo crece un 45 %; si no cabe, lo mas que quepa (de 5 en 5 %) hasta un 15 %; y nunca mas alto que su renglon");
        }
        object f0 = Franja("Volver Volver", false), f1 = Franja("Volver Volver", true);
        Label tit = (Label)Get(f1, "lblNowTop"), art = (Label)Get(f1, "lblNow"), sig = (Label)Get(f1, "lblNext"), tiempo = (Label)Get(f1, "lblTime");
        Control cola = (Control)Get(f1, "pastCola"), cred = (Control)Get(f1, "pastCred");
        int baja = (int)St(form, "BajaLejos", art.Bounds, sig.Height, tiempo.Bounds);
        Info("el artista baja " + baja + " px (el renglon de lo que sigue mide " + sig.Height + ")");
        Check(baja >= sig.Height - 4 && baja <= sig.Height && art.Bottom + baja < tiempo.Top,
              "el artista baja casi un renglon, lo que deja la caja del reloj");
        Rectangle renglon = art.Bounds; renglon.Offset(0, baja);
        Rectangle caja = (Rectangle)Run(f1, "CajaSiguienteCompacto");
        using (Bitmap siempre = PintaFranja(f0))
        using (Bitmap lejos = PintaFranja(f1))
        {
            Rectangle strip = new Rectangle(0, 640, 1024, 128);
            using (Bitmap a = siempre.Clone(strip, siempre.PixelFormat)) a.Save(Path.Combine(dirPng, "franja_siempre.png"), ImageFormat.Png);
            using (Bitmap a = lejos.Clone(strip, lejos.PixelFormat)) a.Save(Path.Combine(dirPng, "franja_lejos.png"), ImageFormat.Png);
            int s0, s1, l0, l1;
            Rectangle zonaTit = new Rectangle(tit.Left, tit.Top - 4, caja.Left - tit.Left - 20, tit.Height + sig.Height + 8);
            Alto(siempre, zonaTit, Blanco, 30, out s0, out s1); Alto(lejos, zonaTit, Blanco, 30, out l0, out l1);
            float crece = (l1 - l0 + 1f) / Math.Max(1f, s1 - s0 + 1f);
            Info("alto de las letras del titulo: " + (s1 - s0 + 1) + " px -> " + (l1 - l0 + 1) + " px");
            Check(crece > 1.3f && crece < 1.6f && l1 < art.Top + baja, "el titulo se lee mas grande (" + Fm(crece, "0.00") + " veces) y se queda arriba del artista");
            int a0, a1, b0, b1;
            Rectangle zonaArt = new Rectangle(art.Left, tit.Top, caja.Left - art.Left - 20, 128);
            Alto(siempre, zonaArt, Verde, 30, out a0, out a1); Alto(lejos, zonaArt, Verde, 30, out b0, out b1);
            Check(b0 - a0 == baja && b1 - a1 == baja, "el artista baja justo eso (" + (b0 - a0) + " px), y todo el con el");
            int clara = Cuenta(lejos, caja, Color.FromArgb(214, 224, 246), 10), clara0 = Cuenta(siempre, caja, Color.FromArgb(214, 224, 246), 10);
            int claraFuera = Cuenta(lejos, strip, Color.FromArgb(214, 224, 246), 10) - clara;   // 10: con mas, el blanco del titulo tambien cuenta
            int grisEnLejos = Cuenta(lejos, strip, GrisSigue, 12), grisSiempre = Cuenta(siempre, sig.Bounds, GrisSigue, 12);
            Info("lo que sigue: " + clara + " pixeles claros en su sitio (antes " + clara0 + ", fuera " + claraFuera + "); del gris de antes " + grisEnLejos + " (antes " + grisSiempre + ")");
            Check(clara > 60 && clara0 == 0 && claraFuera == 0 && grisEnLejos == 0 && grisSiempre > 60, "lo siguiente tiene su propia tarjeta legible, sin tinta fuera de ella");
            // Donde queda, medido en la imagen: detras de lo ultimo verde del artista, antes del borde de las capsulas, sin
            // subir a las capsulas ni bajar a la caja del reloj.
            int verdeFin = -1, sx0 = int.MaxValue, sx1 = -1;
            for (int y = caja.Top; y < caja.Bottom; y++)
                for (int x = 0; x < 1024; x++)
                {
                    Color p = lejos.GetPixel(x, y);
                    if (Parecido(p, Verde, 30)) verdeFin = Math.Max(verdeFin, x);
                    if (Parecido(p, Color.FromArgb(214, 224, 246), 10)) { sx0 = Math.Min(sx0, x); sx1 = Math.Max(sx1, x); }
                }
            Info("en el renglon del artista: el verde acaba en x=" + verdeFin + "; lo que sigue va de x=" + sx0 + " a x=" + sx1 + " (su sitio: " + caja.Left + " a " + caja.Right + ")");
            Check(caja.Top == tit.Top && caja.Bottom < ((Control)Get(f1,"progress")).Top && caja.Right < cola.Left && caja.Right < cred.Left
                  && caja.Height >= sig.Font.Height && verdeFin > 0 && caja.Left >= verdeFin + 12 && sx0 >= caja.Left && sx1 < caja.Right,
                  "tarjeta separada del artista, los creditos y el progreso");
        }
        // Lo que lleva el renglon del artista (el artista, su pastilla del genero y el letrero): lo que sigue empieza detras.
        // Como los pone PositionMejorada: el artista, 12 px, la pastilla, 12 px, el letrero.
        object fm = Franja("Volver Volver", true);
        Label artM = (Label)Get(fm, "lblNow"), mm = (Label)Get(fm, "lblMejorada");
        int textoArt = TextRenderer.MeasureText(artM.Text, artM.Font).Width;
        Control gv = (Control)Activator.CreateInstance(Campo(form, "pastGenero").FieldType, true);
        gv.Bounds = new Rectangle(artM.Left + textoArt + 12, artM.Top, 80, artM.Height); gv.Visible = true;
        mm.Bounds = new Rectangle(gv.Right + 12, artM.Top, 90, 22); mm.Visible = true;
        Set(fm, "pastGenero", gv);
        int finM = (int)Run(fm, "FinRenglonArtista");
        mm.Visible = false; int finSinL = (int)Run(fm, "FinRenglonArtista");
        gv.Visible = false; int finSolo = (int)Run(fm, "FinRenglonArtista");
        Check(finM == mm.Right && finSinL == gv.Right && finSolo == artM.Left + Math.Min(artM.Width, textoArt),
              "lo que sigue empieza detras del artista, de su pastilla del genero y del letrero MEJORADA");
        // Lo que sigue, largo: termina en puntos suspensivos y no se sale de su sitio (alineado a la derecha, el recorte de
        // GDI+ no se porta igual en todas partes: en Mono se comia el principio y se salia por la izquierda).
        string sigueLargo = "Sigue:  Cielito Lindo (Version Extendida en Vivo desde el Palenque) - Los Cadetes de Linares con la Banda Sinaloense de los Hermanos Ramirez";
        object fl = Franja("Volver Volver", true, sigueLargo);
        Label artL = (Label)Get(fl, "lblNow"), sigL = (Label)Get(fl, "lblNext");
        Rectangle renglonL = artL.Bounds; renglonL.Offset(0, (int)St(form, "BajaLejos", artL.Bounds, sigL.Height, ((Label)Get(fl, "lblTime")).Bounds));
        Rectangle cajaL = (Rectangle)Run(fl, "CajaSiguienteCompacto");
        using (Bitmap bl = PintaFranja(fl))
        using (Bitmap b1 = new Bitmap(10, 10))
        using (Graphics g1 = Graphics.FromImage(b1))
        {
            Font fs = sigL.Font;
            string corto = (string)St(form, "RecortaLejos", g1, "Sigue:  El Rey", fs, (float)cajaL.Width);
            string cortado = (string)St(form, "RecortaLejos", g1, sigueLargo, fs, (float)cajaL.Width);
            float wCort = g1.MeasureString(cortado, fs, PointF.Empty, StringFormat.GenericTypographic).Width;
            string medio = "Sigue:  Cielito Lindo - Los Cadetes de Linares";
            float wMed = g1.MeasureString(medio, fs, PointF.Empty, StringFormat.GenericTypographic).Width;
            string poco = (string)St(form, "RecortaLejos", g1, medio, fs, wMed / 1.2f);   // se pasa solo un poco
            float wPoco = g1.MeasureString(poco, fs, PointF.Empty, StringFormat.GenericTypographic).Width;
            Info("lo que sigue, largo: \"" + cortado + "\" (" + Fm(wCort, "0") + " de " + cajaL.Width + " px); un poco largo: \"" + poco + "\"");
            Check(corto == "Sigue:  El Rey" && cortado.EndsWith("…") && cortado.Length > 12 && sigueLargo.StartsWith(cortado.Substring(0, cortado.Length - 1)) && wCort <= cajaL.Width
                  && poco.EndsWith("…") && medio.StartsWith(poco.Substring(0, poco.Length - 1)) && wPoco <= wMed / 1.2f,
                  "si no cabe, se recorta por el final con puntos suspensivos (y si cabe, queda tal cual)");
            Rectangle fuera = new Rectangle(0, 640, 1024, 128);
            int dentro = Cuenta(bl, cajaL, Color.FromArgb(214, 224, 246), 10), todo = Cuenta(bl, fuera, Color.FromArgb(214, 224, 246), 10);
            int x0 = int.MaxValue, x1 = -1;
            for (int y = cajaL.Top; y < cajaL.Bottom; y++) for (int x = 0; x < 1024; x++) if (Parecido(bl.GetPixel(x, y), Color.FromArgb(214, 224, 246), 10)) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); }
            Info("lo que sigue, largo, de x=" + x0 + " a x=" + x1 + " (su sitio: " + cajaL.Left + " a " + cajaL.Right + ")");
            Check(dentro > 60 && dentro == todo && x0 >= cajaL.Left && x1 < cajaL.Right, "y el titulo largo queda dentro de la tarjeta con margen");
        }
        // Pantalla estrecha: siguiente vuelve a su propia fila, sin bajar el artista encima.
        object estrecho = Franja("Un titulo largo para pantalla estrecha", true);
        ((Control)Get(estrecho, "pastCola")).Left = 400;
        Check(((Rectangle)Run(estrecho, "CajaSiguienteCompacto")).IsEmpty &&
              (int)Run(estrecho, "BajaArtistaLejos") == 0,
              "sin espacio lateral se conserva una fila para siguiente y no se pisa al artista");
        using (Bitmap b = PintaFranja(estrecho))
        {
            Rectangle r = ((Label)Get(estrecho, "lblNext")).Bounds;
            Check(Cuenta(b, r, Color.FromArgb(214,224,246), 10) > 60,
                  "en pantalla estrecha siguiente sigue visible, no se oculta");
        }
        using (Bitmap b = PintaFranja(estrecho))
        {
            Label t = (Label)Get(estrecho, "lblNowTop");
            int tinta = Cuenta(b, new Rectangle(t.Left, t.Top - 6, t.Width, t.Height + 12), Blanco, 30);
            Info("el titulo sin tarjeta al lado: " + tinta + " pixeles de tinta");
            Check(tinta > 200, "y el titulo se pinta tambien sin tarjeta al lado (su letra llena justo el renglon: GDI+ lo quitaba entero)");
        }
        ((Label)Get(estrecho, "lblNext")).Text = "Sigue: -";
        Check((string)Run(estrecho, "TextoSiguiente") == "" &&
              ((Rectangle)Run(estrecho, "CajaSiguienteCompacto")).IsEmpty,
              "sin canciones en cola no aparece una tarjeta vacia");
        TarjetaBaja();
        // El letrero MEJORADA y su brillo, igual de mas abajo; apagada, donde siempre.
        Label mej = (Label)Get(f1, "lblMejorada");
        Rectangle rm1 = (Rectangle)Run(f1, "RectMejorada"), rm0 = (Rectangle)Run(f0, "RectMejorada");
        Check(rm1.Y == mej.Top + baja && rm0 == ((Label)Get(f0, "lblMejorada")).Bounds && Codigo.Llama(M(form, "FillUiGlows"), "RectMejorada"),
              "el letrero MEJORADA baja con el artista, y su brillo va donde el");
    }

    // LA TARJETA DE LO QUE SIGUE CUANDO EL RENGLON DEL ARTISTA ES LARGO O LAS CAPSULAS CRECEN (1-10-2026). A 1024 de ancho,
    // con el letrero MEJORADA o sin creditos ("Inserta una moneda"), la tarjeta alta no cabia y lo que sigue volvia a su
    // renglon de reserva. Ahora: alta donde ya cabia (en el mismo sitio), baja a la altura del titulo con el artista por
    // debajo, y algo mas a la izquierda si las capsulas crecen.
    static object ConRenglon(string artista, bool genero, bool mejorada, int colaLeft)
    {
        object f = Franja("Enjoy the Silence", true, "Sigue:  Depeche Mode - Everything Counts");
        Label art = (Label)Get(f, "lblNow"), mm = (Label)Get(f, "lblMejorada");
        art.Text = artista;
        int x = art.Left + Math.Min(art.Width, TextRenderer.MeasureText(art.Text, art.Font).Width) + 12;
        if (genero)
        {
            Control gv = (Control)Activator.CreateInstance(Campo(form, "pastGenero").FieldType, true);
            gv.Bounds = new Rectangle(x, art.Top, 60, art.Height); gv.Visible = true; Set(f, "pastGenero", gv);
            x = gv.Right + 12;
        }
        if (mejorada) { mm.ForeColor = Color.FromArgb(255, 200, 60); mm.Bounds = new Rectangle(x, art.Top, 109, 22); mm.Visible = true; }
        Control cola = (Control)Get(f, "pastCola");
        cola.Bounds = new Rectangle(colaLeft, cola.Top, 198, cola.Height);
        // La ventana de 1024x768 de la rocola (el form sin encender mide 0: la regla del 43 % no actuaria).
        foreach (string c in new string[] { "client_size", "clientSize" })
        {
            FieldInfo fi = Campo(typeof(Control), c);
            if (fi != null && fi.FieldType == typeof(Size)) fi.SetValue(f, new Size(1024, 768));
        }
        FieldInfo cr = Campo(typeof(Control), "client_rect");
        if (cr != null && cr.FieldType == typeof(Rectangle)) cr.SetValue(f, new Rectangle(0, 0, 1024, 768));
        // WinForms de Microsoft guarda el mismo estado en dos enteros (Mono usa Size/Rectangle).
        foreach (string c in new string[] { "clientWidth", "_clientWidth", "clientHeight", "_clientHeight" })
        {
            FieldInfo fi = Campo(typeof(Control), c);
            if (fi != null && fi.FieldType == typeof(int)) fi.SetValue(f, c.IndexOf("Width") >= 0 ? 1024 : 768);
        }
        return f;
    }
    // La caja de antes (hasta el 1-10-2026), para comprobar que donde ya cabia queda igual.
    static Rectangle CajaDeAntes(object f)
    {
        Label tit = (Label)Get(f, "lblNowTop");
        int left = Math.Max((int)Run(f, "FinRenglonArtista") + 28, Math.Max(((Form)f).ClientSize.Width * 43 / 100, tit.Left + 240));
        int right = ((Control)Get(f, "pastCola")).Left - 20;
        Rectangle box = new Rectangle(left, tit.Top, Math.Max(0, right - left), Math.Max(0, ((Control)Get(f, "progress")).Top - tit.Top - 4));
        return box.Width >= 180 && box.Height >= 28 ? box : Rectangle.Empty;
    }
    static void TarjetaBaja()
    {
        Color dorado = Color.FromArgb(255, 200, 60), clara = Color.FromArgb(214, 224, 246);
        // 1) Donde ya cabia, la misma caja que antes.
        object f0 = ConRenglon("Depeche Mode", true, false, 660);
        Rectangle c0 = (Rectangle)Run(f0, "CajaSiguienteCompacto");
        Info("ventana del banco: " + ((Form)f0).ClientSize.Width + " px; sin MEJORADA la tarjeta va en " + c0.Left + ".." + c0.Right + " (antes " + CajaDeAntes(f0).Left + ".." + CajaDeAntes(f0).Right + ")");
        Check(((Form)f0).ClientSize.Width == 1024, "el banco tiene la ventana de la rocola: 1024 de ancho");
        Check(!c0.IsEmpty && c0 == CajaDeAntes(f0) && c0.Left == 1024 * 43 / 100, "donde la tarjeta ya cabia, queda exactamente donde estaba (en el 43 % del ancho)");

        // 2) Con MEJORADA: la de antes no cabe; ahora sale la baja, a la altura del titulo, y el artista pasa por debajo.
        object f1 = ConRenglon("Depeche Mode", true, true, 660);
        Label tit = (Label)Get(f1, "lblNowTop"), art = (Label)Get(f1, "lblNow"), mm = (Label)Get(f1, "lblMejorada"), sig = (Label)Get(f1, "lblNext");
        Rectangle c1 = (Rectangle)Run(f1, "CajaSiguienteCompacto");
        int baja = (int)Run(f1, "BajaArtistaLejos");
        Info("con MEJORADA (hasta x=" + mm.Right + "): antes " + (CajaDeAntes(f1).IsEmpty ? "sin tarjeta" : "tarjeta") + "; ahora " + c1.Left + ".." + c1.Right + " x " + c1.Top + ".." + c1.Bottom + "; el artista baja " + baja + " px");
        Check(CajaDeAntes(f1).IsEmpty && !c1.IsEmpty && c1.Top == tit.Top && c1.Bottom <= art.Top + baja - 4 && c1.Width >= 180 && c1.Right == ((Control)Get(f1, "pastCola")).Left - 20 && baja > 0,
              "con el letrero MEJORADA sale la tarjeta baja, a la altura del titulo, en vez del renglon de reserva");
        using (Bitmap b = PintaFranja(f1))
        {
            Rectangle strip = new Rectangle(0, 640, 1024, 128);
            using (Bitmap a = b.Clone(strip, b.PixelFormat)) a.Save(Path.Combine(dirPng, "franja_mejorada.png"), ImageFormat.Png);
            int dentro = Cuenta(b, c1, clara, 10), fuera = Cuenta(b, strip, clara, 10) - dentro;
            int oroArriba = Cuenta(b, new Rectangle(0, 640, 1024, c1.Bottom - 640), dorado, 30), oro = Cuenta(b, strip, dorado, 30);
            int verdeArriba = Cuenta(b, new Rectangle(0, 640, 1024, c1.Bottom - 640), Verde, 30), verde = Cuenta(b, strip, Verde, 30);
            int reserva = Cuenta(b, new Rectangle(sig.Left, sig.Top + baja, c1.Left - sig.Left, sig.Height), clara, 10);
            Info("lo que sigue: " + dentro + " pixeles claros en la tarjeta, " + fuera + " fuera; MEJORADA " + oro + " (" + oroArriba + " a la altura de la tarjeta); artista " + verde + " (" + verdeArriba + ")");
            Check(dentro > 60 && fuera == 0 && reserva == 0, "lo que sigue esta en su tarjeta, y no en el renglon de reserva");
            Check(oro > 30 && oroArriba == 0 && verde > 30 && verdeArriba == 0, "el artista y su letrero MEJORADA van debajo de la tarjeta, sin pisarla");
        }

        // 3) El artista largo y sin letreros: con la tarjeta baja no se corta (va por debajo de ella).
        object fl = ConRenglon("Los Cadetes de Linares con la Banda Sinaloense", false, false, 660);
        Label artL = (Label)Get(fl, "lblNow");
        artL.Width = 400;
        Rectangle cl = (Rectangle)Run(fl, "CajaSiguienteCompacto");
        using (Bitmap b = PintaFranja(fl))
        {
            int x1 = -1;
            for (int y = 640; y < 768; y++) for (int x = 0; x < 1024; x++) if (Parecido(b.GetPixel(x, y), Verde, 30)) x1 = Math.Max(x1, x);
            Info("artista largo: la tarjeta va en " + cl.Left + ".." + cl.Right + " x " + cl.Top + ".." + cl.Bottom + "; el artista llega a x=" + x1);
            Check(!cl.IsEmpty && cl.Bottom < artL.Top + (int)Run(fl, "BajaArtistaLejos") && x1 > cl.Left,
                  "con el artista largo, la tarjeta baja va por encima y el artista no se corta en ella");
        }

        // 4) Sin creditos ("Inserta una moneda" empuja la cola 75 px): la alta se corre a la izquierda, sin quitarle al
        //    titulo sus 240 px; con MEJORADA, la baja.
        object fs = ConRenglon("Depeche Mode", true, false, 585), fsm = ConRenglon("Depeche Mode", true, true, 585);
        Rectangle cs = (Rectangle)Run(fs, "CajaSiguienteCompacto"), csm = (Rectangle)Run(fsm, "CajaSiguienteCompacto");
        Label titS = (Label)Get(fs, "lblNowTop");
        Info("sin creditos: " + cs.Left + ".." + cs.Right + " (alta: " + cs.Height + " px); con MEJORADA " + csm.Left + ".." + csm.Right + " (" + csm.Height + " px)");
        Check(CajaDeAntes(fs).IsEmpty && !cs.IsEmpty && cs.Width >= 180 && cs.Left >= titS.Left + 240 && cs.Left < 1024 * 43 / 100 && !csm.IsEmpty && csm.Height < cs.Height,
              "sin creditos la tarjeta tambien sale: alta mas a la izquierda (el titulo conserva 240 px) o, con MEJORADA, baja");
        using (Bitmap b = PintaFranja(fs))
        {
            int x1 = -1;
            // (fuera de la tarjeta: el claro de lo que sigue tambien cae dentro de la tolerancia del blanco del titulo)
            for (int y = titS.Top - 4; y < titS.Bottom + 20; y++) for (int x = 0; x < 1024; x++) if (!cs.Contains(x, y) && Parecido(b.GetPixel(x, y), Blanco, 30)) x1 = Math.Max(x1, x);
            Check(x1 > 0 && x1 < cs.Left - 10, "y el titulo acaba antes de la tarjeta (x=" + x1 + ", la tarjeta empieza en " + cs.Left + ")");
        }
        Check(Codigo.Llama(M(form, "PintaArtistaCompacto"), "CajaSiguienteForma"), "el renglon del artista solo se corta ante la tarjeta alta");
    }

    // ================================================================================================
    //  3) EL COLOR DEL GENERO EN TODO
    // ================================================================================================
    static void Genero()
    {
        Console.WriteLine("\n  3) EL COLOR DEL GENERO EN TODO\n");
        MethodInfo halo = M(gpu, "HaloGenero");
        uint rojo = 0xFFD72D2Du;
        bool bien = true; float maxPaso = 0f; int antA = -1, amin = 999, amax = 0;
        for (double t = 0; t < 12000; t += 16.7)
        {
            object[] a = { 100f, 600f, 80f, 80f, rojo, t, 0f, 0f, 0f, 0u }; halo.Invoke(null, a);
            float cx = (float)a[6], cy = (float)a[7], r = (float)a[8]; uint c = (uint)a[9];
            int al = (int)(c >> 24);
            if (Math.Abs(cx - 140f) > .01f || Math.Abs(cy - 640f) > .01f || r < 60f || r > 70f || (c & 0xFFFFFFu) != (rojo & 0xFFFFFFu)) bien = false;
            if (antA >= 0) maxPaso = Math.Max(maxPaso, Math.Abs(al - antA));
            antA = al; amin = Math.Min(amin, al); amax = Math.Max(amax, al);
        }
        object[] p0 = { 0f, 0f, 50f, 50f, rojo, 1234.0, 0f, 0f, 0f, 0u }, p1 = { 0f, 0f, 50f, 50f, rojo, 1234.0 + 5000, 0f, 0f, 0f, 0u };
        halo.Invoke(null, p0); halo.Invoke(null, p1);
        Check(bien && amin >= 68 && amax <= 112 && amax - amin > 30 && maxPaso <= 2 && (uint)p0[9] == (uint)p1[9],
              "detras de la portada: centrado con ella, del color del genero, respirando despacio (" + amin + " a " + amax + " de alfa, cada 5 s) y sin saltos");
        Check(halo.GetParameters().Length == 10, "y respira con su propio reloj: no le entra la musica (nada da saltos)");
        // La tarjeta se entera: al aplicar los ajustes y en cada latido.
        object f = Codigo.FormSinEncender(form); Codigo.PonCerrojos(f); Codigo.PonColecciones(f);
        object panel = Activator.CreateInstance(gpu, true);
        Set(f, "gpuPanel", panel); Set(f, "gpuEnabled", true);
        Set(f, "colorGenero", true);
        try { Run(f, "AplicaEfectosGpu"); } catch { }
        bool on = (bool)Get(panel, "GeneroTodo");
        Set(f, "colorGenero", false);
        try { Run(f, "AplicaEfectosGpu"); } catch { }
        bool off = (bool)Get(panel, "GeneroTodo");
        Check(on && !off, "al cambiar el interruptor, la tarjeta se entera");
        Check(Codigo.EscribeCampo(Codigo.MetodosConAyudantes(form, "AnimTimer_Tick"), "GeneroArgb"), "y en cada latido recibe el color del genero de lo que suena");
        List<string> cuadro = Codigo.Repasa(M(gpu, "RenderFrame"));
        int h1 = cuadro.IndexOf("DibujaHaloGenero"), h2 = cuadro.LastIndexOf("DibujaHaloGenero");
        int vin = cuadro.IndexOf("DibujaVinilo"), vinRep = cuadro.IndexOf("DibujaViniloReposo");
        Check(h1 >= 0 && h2 > h1 && vin < 0 && vinRep < 0, "el resplandor de las portadas se conserva sin dibujar vinilos");
        Check(Codigo.LeeCampo(Codigo.MetodosConAyudantes(gpu, "DibujaHaloGenero"), "GeneroTodo"), "y solo con la idea encendida");
        // El pulso de la letra elegida.
        object fl = Codigo.FormSinEncender(form); Codigo.PonCerrojos(fl); Codigo.PonColecciones(fl);
        Set(fl, "gpuPanel", Activator.CreateInstance(gpu, true)); Set(fl, "gpuEnabled", true);
        char letra = 'M';
        Set(fl, "currentLetter", letra);
        ((IDictionary)Get(fl, "letterTag"))[letra] = "Cumbia";
        Color cumbia = (Color)St(form, "ColorDeEtiqueta", "Cumbia");
        Set(fl, "colorGenero", true); Color c1 = (Color)Run(fl, "ColorPulsoLetra");
        Set(fl, "colorGenero", false); Color c0 = (Color)Run(fl, "ColorPulsoLetra");
        Check(c1.ToArgb() == cumbia.ToArgb() && c0.ToArgb() == Color.FromArgb(110, 180, 255).ToArgb() && Codigo.Llama(M(form, "FillUiGlows"), "ColorPulsoLetra"),
              "el pulso de la letra elegida toma el color del genero de esa letra (apagada, el azul de siempre)");
    }

    // ================================================================================================
    //  4) Ctrl+F3 -> PANTALLA
    // ================================================================================================
    static void Ajustes()
    {
        Console.WriteLine("\n  4) CTRL+F3 -> PANTALLA -> LA ATRACCION Y EL ESTILO\n");
        string dir = Path.Combine(Path.GetTempPath(), "rocola_estilo_aj_" + Environment.TickCount);
        Directory.CreateDirectory(dir);
        object owner = Codigo.FormSinEncender(form);
        Set(owner, "carpetaDatos", dir);
        if (gpu != null) { Set(owner, "gpuPanel", Activator.CreateInstance(gpu, true)); Set(owner, "gpuEnabled", true); }
        string[] nom = { "Letra para leer de lejos", "El color del género en todo" };
        string[] campo = { "letraLejos", "colorGenero" };
        using (Font fo = new Font("Segoe UI", 9))
        using (Panel page = (Panel)form.GetMethod("PaginaPantalla", F).Invoke(owner, new object[] { fo, fo, fo, Color.White, Color.Gray, Color.Gray }))
        {
            Control[] sw = new Control[2]; bool estan = true;
            for (int i = 0; i < 2; i++) { sw[i] = Find(page, nom[i]); if (sw[i] == null) estan = false; }
            Check(estan, "la pestaña Pantalla tiene los dos nuevos");
            if (!estan) return;
            Control card = sw[0].Parent, hoja = card.Parent, atr = Find(page, "El escaparate");
            Check(card == sw[1].Parent && atr != null && atr.Parent.Parent == hoja && atr.Parent != card && !card.Bounds.IntersectsWith(atr.Parent.Bounds)
                  && card.Right <= hoja.Width && card.Bottom <= hoja.Height,
                  "en su tarjeta, en la tercera hoja al lado del modo atraccion, sin pisarse");
            bool dentro = true;
            foreach (Control x in card.Controls) { if (x.Right > card.Width || x.Bottom > card.Height) dentro = false; foreach (Control y in card.Controls) if (x != y && x.Bounds.IntersectsWith(y.Bounds)) dentro = false; }
            Check(dentro, "todo cabe en su tarjeta sin taparse");
            PropertyInfo marcado = sw[0].GetType().GetProperty("Checked", F);
            bool guarda = true;
            for (int mask = 3; mask >= 0; mask--)
            {
                for (int i = 0; i < 2; i++) marcado.SetValue(sw[i], (mask & (1 << i)) != 0, null);
                form.GetMethod("SaveConfigPrefs", F).Invoke(owner, null);
                Codigo.EsperaGuardado(form, owner);
                object leido = Codigo.FormSinEncender(form); form.GetMethod("LoadConfigPrefs", F).Invoke(leido, null);
                for (int i = 0; i < 2; i++) if ((bool)Get(leido, campo[i]) != ((mask & (1 << i)) != 0) || (bool)Get(owner, campo[i]) != ((mask & (1 << i)) != 0)) guarda = false;
            }
            Check(guarda, "las 4 combinaciones se guardan en rocola_config.txt y se leen igual");
            Control reset = Find(page, "Pantalla como de fabrica");
            try { typeof(Control).GetMethod("OnClick", F).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            bool todos = true; for (int i = 0; i < 2; i++) if (!(bool)Get(owner, campo[i]) || !(bool)marcado.GetValue(sw[i], null)) todos = false;
            for (int i = 0; i < 2; i++) Set(owner, campo[i], false);
            try { typeof(Control).GetMethod("OnClick", F).Invoke(reset, new object[] { EventArgs.Empty }); } catch { }
            bool puestas = true; for (int i = 0; i < 2; i++) if (!(bool)Get(owner, campo[i])) puestas = false;
            Check(reset != null && todos && puestas, "\"Pantalla como de fabrica\" los vuelve a encender (las preferencias, no solo los interruptores)");
        }
        try { Directory.Delete(dir, true); } catch { }
        string fuente = Path.Combine(Path.Combine("..", "Rocola"), "EstiloGpu.cs");
        string txt = File.Exists(fuente) ? File.ReadAllText(fuente) : "";
        bool fabrica = txt.Length > 0; foreach (string c in campo) if (txt.IndexOf("private bool " + c + " = true;") < 0) fabrica = false;
        Check(fabrica, "de fabrica vienen encendidos");
    }

    // ================================================================================================
    //  5) LO QUE CUESTA
    // ================================================================================================
    static double Mide(int veces, Action a)
    {
        a();
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < veces; i++) a();
        return sw.Elapsed.TotalMilliseconds / veces;
    }
    static void Coste()
    {
        Console.WriteLine("\n  5) LO QUE CUESTA\n");
        object f0 = Franja("Contrabando y Traicion (En Vivo)", false), f1 = Franja("Contrabando y Traicion (En Vivo)", true);
        // Lo mismo que hace EnsureUiBotOverlay con cada una, sin reflexion por medio: la de siempre son tres etiquetas; la de
        // lejos, FranjaLejos. Sobre la misma imagen, que no se crea en cada vuelta (la franja tampoco).
        MethodInfo dl = null;
        foreach (MethodInfo m in form.GetMethods(F)) if (m.Name == "DrawLabelOverlay" && m.GetParameters().Length == 2) dl = m;
        Action<Graphics, Label> etiqueta = (Action<Graphics, Label>)Delegate.CreateDelegate(typeof(Action<Graphics, Label>), dl);
        Func<Graphics, bool> franja = (Func<Graphics, bool>)Delegate.CreateDelegate(typeof(Func<Graphics, bool>), f1, M(form, "FranjaLejos"));
        Label t0 = (Label)Get(f0, "lblNowTop"), a0 = (Label)Get(f0, "lblNow"), s0 = (Label)Get(f0, "lblNext");
        double siempre, lejos;
        using (Bitmap b = new Bitmap(1024, 768, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            siempre = Mide(200, delegate { g.Clear(Color.Transparent); etiqueta(g, t0); etiqueta(g, a0); etiqueta(g, s0); });
            lejos = Mide(200, delegate { g.Clear(Color.Transparent); franja(g); });
        }
        MethodInfo halo = M(gpu, "HaloGenero");
        double h = Mide(5000, delegate { object[] a = { 100f, 600f, 80f, 80f, 0xFFD72D2Du, 1234.0, 0f, 0f, 0f, 0u }; halo.Invoke(null, a); }) * 1000.0;
        Info("hornear la tarjeta de lo que suena (en una imagen de 1024x768, borrarla incluido): de siempre " + Fm(siempre, "0.00") + " ms, de lejos "
             + Fm(lejos, "0.00") + " ms; solo cuando cambia lo que dice");
        Info("el resplandor de la portada, por cuadro: " + Fm(h, "0.0") + " us (con la llamada por reflexion)");
        Check(lejos < siempre + 5, "la franja de lejos no cuesta mas que unos milisegundos de mas, y solo al rehacerse");
        Check(h < 50, "y el resplandor, por cuadro, no llega a 50 microsegundos");
    }

    static int Main(string[] args)
    {
        asm = Assembly.LoadFrom(args.Length > 0 ? args[0] : "rocola.exe");
        form = asm.GetType("Rocola.Form1"); gpu = asm.GetType("Rocola.GpuPanel"); pvT = form.GetNestedType("PastillaView", F);
        dirPng = Path.Combine(Path.GetTempPath(), "rocola_estilo_png"); Directory.CreateDirectory(dirPng);
        Console.WriteLine("  perfil: " + (gpu != null ? "con tarjeta grafica" : "sin tarjeta grafica (solo la pestaña de ajustes)"));
        if (gpu != null)
        {
            Check(form.GetMethod("FranjaLejos", F) != null && gpu.GetField("GeneroTodo", F) != null, "la tarjeta tiene la letra de lejos y el color del genero");
            NoSeTocan(); Lejos(); Genero();
        }
        else
        {
            Console.WriteLine("\n  1) SIN TARJETA GRAFICA\n");
            Check(form.GetMethod("FranjaLejos", F) == null && form.GetField("letraLejos", F) != null && form.GetField("colorGenero", F) != null,
                  "sin tarjeta grafica no hay nada de esto: solo los interruptores, que se guardan igual");
        }
        Ajustes();
        if (gpu != null) Coste();
        Console.WriteLine("\n  imagenes para mirar: " + dirPng);
        Console.WriteLine("\n  " + (fallos == 0 ? "TODO BIEN (" + oks + " comprobaciones)" : fallos + " FALLO(S)"));
        return fallos;
    }
}

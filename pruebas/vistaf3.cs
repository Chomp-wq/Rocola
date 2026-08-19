using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

// Dibuja la ventana de sonido (Ctrl+F3) con las MISMAS piezas y los MISMOS numeros que el programa, para
// poder verla sin arrancar la rocola. Las piezas (Tarjeta, Deslizador, Interruptor, BotonPlano, BarraAvance)
// se sacan del propio ejecutable por reflexion, asi que lo que se ve aqui es lo que se vera alli.
class VistaF3
{
    static Assembly asm; static Type TF;
    static Type T(string n)
    {
        foreach (Type t in TF.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)) if (t.Name == n) return t;
        throw new Exception("falta " + n);
    }
    static object New(string n) { return Activator.CreateInstance(T(n), true); }
    static void F(object o, string campo, object v) { o.GetType().GetField(campo).SetValue(o, v); }
    static void P(object o, string prop, object v) { o.GetType().GetProperty(prop).SetValue(o, v, null); }

    static Font ff(float t, FontStyle s) { return new Font(FamiliaUi(), t, s); }
    static FontFamily FamiliaUi()
    {
        string[] pref = { "Segoe UI", "DejaVu Sans", "Tahoma", "Verdana" };
        foreach (string p in pref) try { return new FontFamily(p); } catch { }
        return FontFamily.GenericSansSerif;
    }

    static Label Lab(int x, int y, int w, int h, string txt, Font f, Color c, ContentAlignment a)
    {
        Label l = new Label();
        l.SetBounds(x, y, w, h); l.BackColor = Color.Transparent; l.ForeColor = c; l.Font = f; l.Text = txt; l.TextAlign = a;
        return l;
    }

    static Control Tarj(int x, int y, int w, int h, string tit, Color ac, Font f)
    {
        Control t = (Control)New("Tarjeta");
        t.SetBounds(x, y, w, h); t.Font = f;
        F(t, "Titulo", tit); F(t, "Acento", ac);
        return t;
    }
    static Control Desl(int x, int y, int w, int min, int max, int val, Color ac, bool en)
    {
        Control d = (Control)New("Deslizador");
        d.SetBounds(x, y, w, 22);
        F(d, "Minimum", min); F(d, "Maximum", max); F(d, "Acento", ac);
        P(d, "Value", val); d.Enabled = en;
        return d;
    }
    static Control Inter(int x, int y, int w, string txt, Color ac, bool on, Font f, Color fore, bool en)
    {
        Control s = (Control)New("Interruptor");
        s.SetBounds(x, y, w, 26); s.Font = f; s.ForeColor = fore; s.Text = txt;
        F(s, "Acento", ac); P(s, "Checked", on); s.Enabled = en;
        return s;
    }
    static Control Boton(int x, int y, int w, string txt, Color rel, Color bor, Font f)
    {
        Control b = (Control)New("BotonPlano");
        b.SetBounds(x, y, w, 38); b.Font = f; b.Text = txt;
        F(b, "Relleno", rel); F(b, "Borde", bor);
        return b;
    }
    static Control Barra(int x, int y, int w, float v, Color ac)
    {
        Control b = (Control)New("BarraAvance");
        b.SetBounds(x, y, w, 8); F(b, "Valor", v); F(b, "Acento", ac);
        return b;
    }

    static Color cVol = Color.FromArgb(120,230,165), cFue = Color.FromArgb(140,205,255), cSub = Color.FromArgb(255,160,140);
    static Color cVid = Color.FromArgb(140,220,232), cCal = Color.FromArgb(178,152,255), cGra = Color.FromArgb(255,176,122);
    static Color cBri = Color.FromArgb(255,214,140), cBib = Color.FromArgb(255,176,102);
    static Color cTexto = Color.FromArgb(222,230,250), cTenue = Color.FromArgb(138,149,182);

    static Font fTit, fSub, fNom, fVal, fDes, fBot, fInt;
    const int anchoFila = 424;

    static void Fila(Control card, int y, string titulo, Color ac, int min, int max, int valor, string valTxt, string desc, bool conAuto, bool auto, bool barraOn)
    {
        int wNom = conAuto ? (anchoFila - 210) : (anchoFila - 100);
        bool gris = !barraOn && !conAuto;   // solo se apaga entero lo que depende del interruptor de nivelar
        card.Controls.Add(Lab(16, y, wNom, 18, titulo, fNom, gris ? Color.FromArgb(96,104,128) : cTexto, ContentAlignment.TopLeft));
        if (conAuto) card.Controls.Add(Inter(16 + wNom + 6, y - 2, 104, "Auto", ac, auto, fInt, cTenue, true));
        card.Controls.Add(Lab(16 + anchoFila - 94, y - 2, 94, 20, valTxt, fVal, gris ? Color.FromArgb(96,104,128) : ac, ContentAlignment.TopRight));
        card.Controls.Add(Lab(16, y + 21, anchoFila, 32, desc, fDes, cTenue, ContentAlignment.TopLeft));
        card.Controls.Add(Desl(16, y + 55, anchoFila, min, max, valor, ac, barraOn));
    }

    static void Pinta(Control padre, Graphics g, int ox, int oy)
    {
        foreach (Control c in padre.Controls)
        {
            if (!c.Visible) continue;
            int x = ox + c.Left, y = oy + c.Top;
            System.Drawing.Drawing2D.GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.SetClip(new Rectangle(0, 0, c.Width, c.Height));
            MethodInfo op = c.GetType().GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic);
            try { op.Invoke(c, new object[]{ new PaintEventArgs(g, new Rectangle(0, 0, c.Width, c.Height)) }); } catch { }
            g.Restore(st);
            Pinta(c, g, x, y);
        }
    }

    static int Main()
    {
        asm = Assembly.LoadFrom("rocola.exe");
        TF = asm.GetType("Rocola.Form1", true);
        fTit = ff(15f, FontStyle.Bold); fSub = ff(9.5f, FontStyle.Regular); fNom = ff(10f, FontStyle.Bold);
        fVal = ff(11f, FontStyle.Bold); fDes = ff(8.5f, FontStyle.Regular); fBot = ff(10f, FontStyle.Bold);
        fInt = ff(9.5f, FontStyle.Bold);

        Form dlg = new Form();
        dlg.FormBorderStyle = FormBorderStyle.None;
        dlg.ClientSize = new Size(960, 704);
        dlg.BackColor = Color.FromArgb(11, 14, 32);
        dlg.Font = fSub;

        dlg.Controls.Add(Lab(22, 12, 700, 28, "Sonido", fTit, Color.FromArgb(232,238,255), ContentAlignment.TopLeft));
        dlg.Controls.Add(Lab(24, 44, 916, 18, "Todo lo que cambies aqui se oye al instante en la cancion que esta sonando.", fSub, cTenue, ContentAlignment.TopLeft));

        Control tVol = Tarj(16, 70, 456, 400, "VOLUMEN PAREJO", cVol, fSub);
        Control tCal = Tarj(488, 70, 456, 400, "CALIDAD DEL SONIDO", cCal, fSub);
        Control tBib = Tarj(16, 480, 928, 160, "TU BIBLIOTECA", cBib, fSub);

        tVol.Controls.Add(Inter(16, 40, 424, "Nivelar el volumen de todas las canciones", cVol, true, fNom, cTexto, true));
        Fila(tVol, 76, "Volumen objetivo", cVol, 0, 100, 62, "62%", "Que tan fuerte queda el conjunto.\nMas alto = todas las canciones suenan mas fuerte.", false, false, true);
        Fila(tVol, 156, "Fuerza del nivelado", cFue, 0, 100, 100, "100%", "Cuanto se suben las canciones bajitas.\nLas que ya suenan fuerte NUNCA se bajan.", false, false, true);
        Fila(tVol, 236, "Subir TODO el volumen", cSub, 0, 12, 3, "+3 dB", "De mas para TODAS, ademas del nivelado.\nPara cuando el local se queda corto de volumen.", false, false, true);
        Fila(tVol, 316, "Volumen de los videos", cVid, 0, 100, 85, "85%", "Los videos no pasan por la cadena de sonido.\nSu volumen se empareja aqui a mano.", false, false, true);

        Fila(tCal, 44, "EQ automatico", cCal, 0, 100, 0, "75%", "Empareja el tono de cada cancion con el de tu biblioteca.\nSolo: mas fuerza cuantas mas canciones medidas (van 4,182).", true, true, false);
        Fila(tCal, 124, "Graves (bajo profundo)", cGra, 0, 100, 30, "36%", "Devuelve el golpe de bajo que el archivo no trae.\nSolo: a cada cancion se le da lo que le falta.   Esta: 42%.", true, true, false);
        Fila(tCal, 204, "Realce de brillo", cBri, 0, 100, 35, "35%", "Devuelve el 'aire' que el MP3 apaga.\nSe le da mas a las canciones peor grabadas.", false, false, true);
        tCal.Controls.Add(Inter(16, 292, 424, "DSEE", Color.FromArgb(150,205,255), false, fNom, cTexto, true));
        tCal.Controls.Add(Lab(16, 322, 424, 68, "Oye cada cancion y ve donde el MP3 le corto los agudos.\nEso lo mide SIEMPRE: de ahi sacan el EQ automatico y los\ngraves automaticos cuanto le falta a cada una.\nEXPERIMENTAL: encendido, ademas los recrea con la IA.", fDes, cTenue, ContentAlignment.TopLeft));

        tBib.Controls.Add(Lab(16, 40, 896, 20, "Calidad:   3,914 completas     1,022 aceptables     286 pobres     141 con los picos recortados", fNom, Color.FromArgb(198,214,244), ContentAlignment.TopLeft));
        tBib.Controls.Add(Lab(16, 62, 896, 48, "Repasando la biblioteca a fondo ahora mismo.\nMaterial para la IA: 2,684,155 ejemplos (409 MB).   La cancion que suena entra en la etapa MEDIA.\nEl detalle, cancion por cancion, queda en rocola_calidad.txt junto al programa.", fDes, cTenue, ContentAlignment.TopLeft));
        tBib.Controls.Add(Boton(16, 114, 300, "Detener el repaso a fondo", Color.FromArgb(126,52,42), Color.FromArgb(196,96,76), fBot));
        tBib.Controls.Add(Inter(332, 118, 286, "Juntar material para la IA", cBib, true, fNom, cTexto, true));
        tBib.Controls.Add(Lab(630, 112, 282, 18, "31,204 de 52,842 canciones   (59%)", fDes, cBib, ContentAlignment.TopRight));
        tBib.Controls.Add(Barra(630, 134, 282, 0.59f, cBib));

        dlg.Controls.Add(tVol); dlg.Controls.Add(tCal); dlg.Controls.Add(tBib);
        dlg.Controls.Add(Boton(16, 652, 220, "Dejar como de fabrica", Color.FromArgb(40,52,98), Color.FromArgb(72,90,150), fBot));
        dlg.Controls.Add(Boton(804, 652, 140, "Listo", Color.FromArgb(30,96,70), Color.FromArgb(72,182,132), fBot));

        dlg.Show();
        Application.DoEvents();
        // Se pinta el arbol a mano: se llama al OnPaint de cada control sobre la MISMA superficie, con el
        // origen trasladado. Asi los fondos transparentes se ven sobre lo que pinto su tarjeta, igual que en
        // pantalla (DrawToBitmap los sacaria en negro).
        using (Bitmap bmp = new Bitmap(dlg.ClientSize.Width, dlg.ClientSize.Height))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(dlg.BackColor);
                Pinta(dlg, g, 0, 0);
            }
            bmp.Save("f3.png", ImageFormat.Png);
        }
        dlg.Close();
        Console.WriteLine("f3.png");
        return 0;
    }
}

// LA PANTALLA NATIVA CON UNA VISTA FALSA (port nativo, tanda 4, grupo pantalla-nativa: W2). Una IVista que no hereda de nada (VistaFalsa,
// con su CajaVista) junto a controles de la capa de la transicion, y la misma escena con un control de la capa en su lugar (el gemelo, que
// la capa mueve con las reglas de WinForms que fijan las 41 pruebas): el orden de composicion, el recorte por los de encima y el reparto
// del raton tienen que salir IGUALES en las dos y como dice WinForms. Despues, sin la capa (como quedara la pantalla al final), las mismas
// reglas con la lista nativa de la ventana, y las teclas a la ventana. Cada prueba dice lo obtenido y, si falla, lo que daria WinForms y su
// sitio en la fuente (dotnet/winforms release/3.0, portada de .NET Framework 4.8).
//   pruebas_vistas.sh PROGRAMA ETIQUETA   (fuera de pantalla: ROCOLA_PANTALLA=fuera:320x240)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Rocola;
using Rocola.Ventanas;
using SkiaSharp;

static class P
{
    static int pasa, falla;
    static readonly List<string> log = new List<string>();
    static void Mira(string nombre, string obtenido, string esperado, string regla)
    {
        bool ok = obtenido == esperado;
        if (ok) pasa++; else falla++;
        Console.WriteLine((ok ? "PASA  " : "FALLA ") + nombre + ": " + obtenido + (ok ? "" : "\n          WinForms: " + esperado + " (" + regla + ")"));
    }
    static string Log() { string s = string.Join(",", log); log.Clear(); return s; }
    static void Bombea(int ms) { Stopwatch t = Stopwatch.StartNew(); do { Application.DoEvents(); Thread.Sleep(1); } while (t.ElapsedMilliseconds < ms); }
    static readonly SKColor Rojo = new SKColor(255, 0, 0), Azul = new SKColor(0, 0, 255), Verde = new SKColor(0, 255, 0), Amarillo = new SKColor(255, 255, 0);
    static bool Xy;   // (panel-del-disco) el clic apunta tambien donde cayo, en las coordenadas de la vista

    // LA VISTA FALSA: una IVista sin herencia, con su caja; se pinta de su color (fondo y contenido) y apunta su raton
    sealed class VistaFalsa : IVista, IDisposable
    {
        public void Dispose() { log.Add(n + ".Dispose"); }   // (armado-vistas: la desecha su hueco al desecharse con su ventana, como WinForms a los hijos)
        readonly CajaVista caja; readonly string n; readonly SKColor? color; public bool ManejaRueda;
        public VistaFalsa(string n, SKColor? color) { this.n = n; this.color = color; caja = new CajaVista(this); }
        public bool Sel = true, Tab = true;   // (vistas-buscador) ControlStyles.Selectable y TabStop
        public bool Seleccionable { get { return Sel; } }
        public bool ParaElTabulador { get { return Tab; } }
        public override string ToString() { return n; }
        public Rectangle Bounds { get { return caja.Bounds; } set { caja.Bounds = value; } }
        public int Left { get { return caja.Left; } set { caja.Left = value; } }
        public int Top { get { return caja.Top; } set { caja.Top = value; } }
        public int Width { get { return caja.Width; } set { caja.Width = value; } }
        public int Height { get { return caja.Height; } set { caja.Height = value; } }
        public void SetBounds(int x, int y, int w, int h) { caja.SetBounds(x, y, w, h); }
        public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } }
        public void Invalidate() { caja.Invalidate(); }
        public void Pinta(SKCanvas g) { if (color != null) using (SKPaint p = new SKPaint { Color = color.Value }) g.DrawRect(0, 0, Width, Height, p); }
        public void Raton(AvisoRaton que, Raton e)
        {
            log.Add(n + "." + que + (que == AvisoRaton.Entra || que == AvisoRaton.Sale ? "" : "(" + e.Clicks + ")") + (Xy && que == AvisoRaton.Clic ? "@" + e.X + "," + e.Y : ""));
            if (que == AvisoRaton.Rueda && ManejaRueda) e.Handled = true;
        }
    }
    // EL GEMELO: un control de la capa que apunta lo mismo (con los nombres de los avisos de la vista), con su color de fondo
    class Gemelo : Control, IVista
    {
        readonly string n;
        public Gemelo(string n) { this.n = n; }
        public Gemelo(string n, bool sel, bool tab) { this.n = n; if (!sel) SetStyle(ControlStyles.Selectable, false); TabStop = tab; }   // (vistas-buscador)
        public override string ToString() { return n; }
        void A(string q, MouseEventArgs e) { log.Add(n + "." + q + (e == null ? "" : "(" + e.Clicks + ")") + (Xy && q == "Clic" ? "@" + e.X + "," + e.Y : "")); }
        protected override void OnMouseDown(MouseEventArgs e) { A("Abajo", e); base.OnMouseDown(e); }
        protected override void OnMouseClick(MouseEventArgs e) { A("Clic", e); base.OnMouseClick(e); }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { A("DobleClic", e); base.OnMouseDoubleClick(e); }
        protected override void OnMouseUp(MouseEventArgs e) { A("Arriba", e); base.OnMouseUp(e); }
        protected override void OnMouseMove(MouseEventArgs e) { A("Mueve", e); base.OnMouseMove(e); }
        protected override void OnMouseEnter(EventArgs e) { A("Entra", null); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { A("Sale", null); base.OnMouseLeave(e); }
        protected override void Dispose(bool disposing) { if (disposing && !IsDisposed) log.Add(n + ".Dispose"); base.Dispose(disposing); }
    }
    class Transparente : Control
    {
        public Transparente() { SetStyle(ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
    }

    static Form m;
    static string Px(int x, int y) { Bombea(160); return Anfitrion.Actual.PuntoEn(x, y).ToString("X8"); }
    static void Va(int x, int y) { PantallaRocola.RatonEn(x, y); }
    static void Clic(bool abajo) { PantallaRocola.RatonBoton(BotonRaton.Left, abajo); }

    [STAThread]
    static int Main()
    {
        Anfitrion.Arranca();   // la pantalla fuera de pantalla y el compositor nativo (como el programa: Plataforma/PruebasLinux.cs)
        PantallaRocola.EscondeFlecha();   // (Cursor.Hide, como la rocola: si no, la flecha tapa los puntos que se leen)
        m = new Form(); m.Text = "principal"; m.FormBorderStyle = FormBorderStyle.None; m.WindowState = FormWindowState.Maximized; m.BackColor = Color.FromArgb(0, 255, 0);
        m.Shown += delegate { try { Pruebas(); } catch (Exception ex) { Console.WriteLine("LA PRUEBA TRONO: " + ex); falla++; } m.Close(); };
        Application.Run(m);
        Console.WriteLine("pasan " + pasa + ", fallan " + falla);
        return falla == 0 ? 0 : 1;
    }

    // UNA ESCENA: el control A (rojo, de la capa) y la vista V (azul: la falsa o su gemelo) agregada DESPUES, que se cruzan en 60..119
    static string[] Escena(bool gemelo)
    {
        List<string> r = new List<string>();
        Bombea(150);
        Gemelo a = new Gemelo("A"); a.BackColor = Color.FromArgb(255, 0, 0); a.SetBounds(20, 20, 100, 100); m.Controls.Add(a);
        IVista v;
        if (gemelo) { Gemelo g = new Gemelo("V"); g.BackColor = Color.FromArgb(0, 0, 255); g.SetBounds(60, 60, 100, 100); v = g; }
        else { VistaFalsa f = new VistaFalsa("V", Azul); f.SetBounds(60, 60, 100, 100); v = f; }
        PantallaRocola.AgregaVista(m, v);   // (Controls.Add de la ventana: al fondo del orden)
        // 1. EL ORDEN: Controls.Add pone al final de la coleccion, que es el fondo (ControlCollection.Add; el [0] arriba)
        r.Add("orden " + Px(80, 80) + " " + Px(140, 140));
        Va(80, 80); Va(140, 140);
        r.Add("raton-mueve " + Log());
        Clic(true); Clic(false); Bombea(650);
        r.Add("raton-clic " + Log());
        Clic(true); Va(80, 80); Clic(false); Bombea(650);
        r.Add("raton-captura " + Log());
        Va(150, 150); log.Clear();
        Clic(true); Clic(false); Clic(true); Clic(false); Bombea(650);
        r.Add("raton-doble " + Log());
        // 2. BringToFront: al [0]
        PantallaRocola.VistaAlFrente(v);
        r.Add("alfrente " + Px(80, 80));
        Va(100, 100); Va(80, 80);
        r.Add("raton-alfrente " + Log());
        // 3. EL RECORTE POR EL DE ENCIMA (WS_CLIPSIBLINGS): un control transparente encima de V ensena el fondo de la ventana, no a V
        Transparente t = new Transparente(); t.SetBounds(130, 130, 60, 60); m.Controls.Add(t); t.BringToFront();
        r.Add("transparente " + Px(145, 145) + " " + Px(170, 170));
        m.Controls.Remove(t); t.Dispose();
        // 4. ESCONDERLA: lo de debajo se ve y el raton es del de debajo
        v.Visible = false;
        r.Add("escondida " + Px(140, 140) + " " + Px(80, 80));
        Va(90, 90); Va(140, 140); Clic(true); Clic(false); Bombea(650);
        r.Add("raton-escondida " + Log());
        v.Visible = true;
        // 5. SetBounds: se mueve con su sitio
        v.SetBounds(150, 150, 50, 50);
        r.Add("setbounds " + Px(140, 140) + " " + Px(160, 160));
        PantallaRocola.QuitaVista(v);
        r.Add("quitada " + Px(160, 160));
        m.Controls.Remove(a); a.Dispose();
        if (v is Gemelo) ((Gemelo)v).Dispose();
        Va(5, 5); Bombea(650); log.Clear();
        return r.ToArray();
    }

    static void Pruebas()
    {
        const string F = "orden de composicion: Control.ControlCollection.Add (al final = el fondo del orden z) y BringToFront (SetChildIndex(this, 0)); " +
                         "raton: WindowFromPoint de la de encima, SetCapture en WmMouseDown (Control.cs:12567-12571), clic solo si se suelta encima (WmMouseUp, Control.cs:12700-12722), doble clic de 500 ms y 4 puntos";
        string[] gem = Escena(true);
        string[] fal = Escena(false);
        string[] esperado =
        {
            "orden FFFF0000 FF0000FF",
            "raton-mueve A.Entra,A.Mueve(0),A.Sale,V.Entra,V.Mueve(0)",
            "raton-clic V.Abajo(1),V.Clic(1),V.Arriba(1)",
            "raton-captura V.Abajo(1),V.Mueve(0),V.Arriba(1),V.Sale,A.Entra",
            "raton-doble V.Abajo(1),V.Clic(1),V.Arriba(1),V.Abajo(2),V.DobleClic(2),V.Arriba(1)",
            "alfrente FF0000FF",
            "raton-alfrente V.Mueve(0),V.Mueve(0)",
            "transparente FF00FF00 FF00FF00",
            "escondida FF00FF00 FFFF0000",
            "raton-escondida V.Sale,A.Entra,A.Mueve(0),A.Sale",
            "setbounds FF00FF00 FF0000FF",
            "quitada FF00FF00",
        };
        for (int i = 0; i < esperado.Length; i++)
        {
            string nombre = esperado[i].Substring(0, esperado[i].IndexOf(' '));
            Mira("capa, gemelo (control de la capa): " + nombre, i < gem.Length ? gem[i] : "(nada)", esperado[i], F);
            Mira("capa, vista falsa en su hueco: " + nombre, i < fal.Length ? fal[i] : "(nada)", i < gem.Length ? gem[i] : esperado[i], "lo mismo que el gemelo");
        }
        string[] gemO = Oyentes(true), falO = Oyentes(false);
        string[] esperadoO =
        {
            "oyentes V.Abajo(1),oye-abajo(20,20,Left,1),V.Clic(1),oye-clic(20,20,Left,1),V.Arriba(1)",
            "foco True VistaFalsa",
            "foco-escondida False",
            "quitada ",
            "desecha-dueno V.Dispose",
        };
        const string FO = "los eventos MouseDown y MouseClick los lanza el On* de fabrica (Control.OnMouseDown/OnMouseClick: Events[EventMouseDown]...), despues de lo que hace la vista; " +
                          "Focus: FocusInternal (CanFocus = visible y habilitada; devuelve Focused); Controls.Remove no desecha, Control.Dispose desecha a los hijos";
        for (int i = 0; i < esperadoO.Length; i++)
        {
            string nombre = esperadoO[i].Substring(0, esperadoO[i].IndexOf(' '));
            // (el gemelo se llama Gemelo en la traza del foco: lo que se compara es el mismo guion con su nombre)
            string esperadoGem = esperadoO[i].Replace("VistaFalsa", "Gemelo");
            Mira("capa, gemelo (control de la capa): " + nombre, i < gemO.Length ? gemO[i] : "(nada)", esperadoGem, FO);
            Mira("capa, vista falsa en su hueco: " + nombre, i < falO.Length ? falO[i] : "(nada)", esperadoO[i], "lo mismo que el gemelo, y la traza la nombra por la vista");
        }
        // (panel-del-disco) UNA VISTA DENTRO DE OTRA, en la capa (con un Control de la capa como panel, que es lo que fija WinForms, y con la
        // vista falsa en su hueco) y, mas abajo, sin la capa (la lista nativa)
        string[] panGem = EscenaPanel(0), panFal = EscenaPanel(1);
        for (int i = 0; i < esperadoP.Length; i++)
        {
            string nombre = esperadoP[i].Substring(0, esperadoP[i].IndexOf(' '));
            Mira("capa, panel con hijas, gemelo (Controls.Add del control): " + nombre, i < panGem.Length ? panGem[i] : "(nada)", esperadoP[i], FP);
            Mira("capa, panel con hijas, vista falsa en su hueco: " + nombre, i < panFal.Length ? panFal[i] : "(nada)", esperadoP[i], "lo mismo que el gemelo");
        }
        // (vistas-buscador) EL FOCO DEL TECLADO ENTRE LAS VISTAS: la casilla de BUSCAR (TabStop = false), el buscador (Selectable y TabStop) y
        // un panel (sin Selectable); con un Control de la capa (lo que fija WinForms) y con la vista falsa en su hueco
        string[] tabGem = EscenaTab(0), tabFal = EscenaTab(1);
        for (int i = 0; i < esperadoT.Length; i++)
        {
            string nombre = esperadoT[i].Substring(0, esperadoT[i].IndexOf(' '));
            // (la capa no sigue a WinForms al esconder la del foco: su ContenedorDelFoco salta la ventana porque es ContainerControl, y en
            // WinForms GetContainerControl solo salta las que dan IsContainerControl, que es false en la Form -Control.cs:2900 y 5825-5840-:
            // en la capa el foco se queda en la escondida. Lo que se compara aqui es que el hueco haga lo mismo que el control de la capa)
            string esperadoCapa = nombre == "esconde-la-del-foco" ? "esconde-la-del-foco X" : esperadoT[i];
            Mira("capa, tabulador, gemelo: " + nombre, i < tabGem.Length ? tabGem[i] : "(nada)", esperadoCapa, nombre == "esconde-la-del-foco" ? "lo que hace la capa (Ventanas.Control.SelectNextIfFocused con ContenedorDelFoco): ver el comentario" : FT);
            Mira("capa, tabulador, vista falsa en su hueco: " + nombre, i < tabFal.Length ? tabFal[i] : "(nada)", i < tabGem.Length ? tabGem[i] : esperadoCapa, "lo mismo que el gemelo");
        }
        // (rotulos-y-letras) EL ROTULO CON FORMA (Control.Region: el aviso sin tarjeta, lblToast) Y EL VIDEO (la capa de textura en una ventana
        // de la capa): con un Label de la capa (lo que fija WinForms), con el rotulo nativo en su hueco y sin la capa
        string[] rotGem = EscenaRotulo(0), rotHue = EscenaRotulo(1), rotNat = EscenaRotulo(2);
        for (int i = 0; i < esperadoR.Length; i++)
        {
            string nombre = esperadoR[i].Substring(0, esperadoR[i].IndexOf(' '));
            Mira("capa, rotulo con forma, Label de la capa: " + nombre, i < rotGem.Length ? rotGem[i] : "(nada)", esperadoR[i], FR);
            Mira("capa, rotulo con forma, RotuloVista en su hueco: " + nombre, i < rotHue.Length ? rotHue[i] : "(nada)", esperadoR[i], "lo mismo que el Label");
            Mira("sin la capa, rotulo con forma: " + nombre, i < rotNat.Length ? rotNat[i] : "(nada)", esperadoR[i], "lo mismo que el Label");
        }
        string[] vidCapa = EscenaVideo(true), vidNat = EscenaVideo(false);
        for (int i = 0; i < esperadoV.Length; i++)
        {
            string nombre = esperadoV[i].Substring(0, esperadoV[i].IndexOf(' '));
            Mira("capa, el video (hueco de textura): " + nombre, i < vidCapa.Length ? vidCapa[i] : "(nada)", esperadoV[i], FV);
            Mira("sin la capa, el video (capa de la lista): " + nombre, i < vidNat.Length ? vidNat[i] : "(nada)", esperadoV[i], FV);
        }
        SinLaCapa();
    }

    // (rotulos-y-letras) A (rojo, 20..119) y encima un rotulo azul en 60..159 con la forma de su mitad izquierda (0..49 de 100): dentro de la
    // forma se ve el rotulo; fuera, lo de debajo (A, o el fondo verde); el raton fuera de la forma es del de debajo
    const string FR = "Control.Region (SetWindowRgn): la ventana es solo su forma: se pinta dentro, recorta a las de debajo solo con ella y fuera de ella el raton es del de debajo (Control.cs, Region)";
    static readonly string[] esperadoR =
    {
        "dentro FF0000FF",
        "fuera-sobre-A FFFF0000",
        "fuera FF00FF00",
        "raton A.Entra,A.Mueve(0),A.Sale",
        "sin-forma FF0000FF FF0000FF",
        "quitado FFFF0000",
    };
    static string[] EscenaRotulo(int modo)
    {
        List<string> r = new List<string>();
        PantallaRocola.ILaCapa capa = PantallaRocola.LaCapa;
        if (modo == 2) { PantallaRocola.LaCapa = null; PantallaRocola.FondoDeLaVentana = delegate (SKCanvas g) { g.Clear(Verde); }; }
        try
        {
            Func<int, int, string> px = modo == 2 ? (Func<int, int, string>)((x, y) => Pinta(x, y)) : Px;
            Bombea(150);
            object a; IVista av = null; Gemelo ag = null;
            if (modo == 2) { VistaFalsa f = new VistaFalsa("A", Rojo); f.SetBounds(20, 20, 100, 100); PantallaRocola.AgregaVista(null, f); a = f; av = f; }
            else { ag = new Gemelo("A"); ag.BackColor = Color.FromArgb(255, 0, 0); ag.SetBounds(20, 20, 100, 100); m.Controls.Add(ag); a = ag; }
            Label l = null; RotuloVista rv = null;
            if (modo == 0)
            {
                l = new Label(); l.BackColor = Color.FromArgb(0, 0, 255); l.SetBounds(60, 60, 100, 100);
                l.Region = new Rocola.Dibujo2D.Region(new RectangleF(0, 0, 50, 100)); m.Controls.Add(l); l.BringToFront();
            }
            else
            {
                rv = new RotuloVista(); rv.BackColor = Color.FromArgb(0, 0, 255); rv.SetBounds(60, 60, 100, 100);
                SKPath f = new SKPath(); f.AddRect(new SKRect(0, 0, 50, 100)); rv.Forma = f;
                PantallaRocola.AgregaVista(modo == 2 ? null : m, rv); PantallaRocola.VistaAlFrente(rv);
            }
            r.Add("dentro " + px(80, 80));
            r.Add("fuera-sobre-A " + px(115, 80));
            r.Add("fuera " + px(140, 140));
            Va(5, 5); log.Clear();
            Va(115, 80); Va(80, 80); Va(140, 140);
            r.Add("raton " + Log());
            if (modo == 0) { l.Region = null; l.Invalidate(); } else rv.Forma = null;   // (la Region de la capa es una propiedad sin mas: no repinta como SetWindowRgn; el rotulo y su hueco si)
            r.Add("sin-forma " + px(115, 80) + " " + px(140, 140));
            if (modo == 0) { m.Controls.Remove(l); l.Dispose(); } else { PantallaRocola.QuitaVista(rv); rv.Dispose(); }
            r.Add("quitado " + px(115, 80));
            if (modo == 2) PantallaRocola.QuitaVista(av); else { m.Controls.Remove(ag); ag.Dispose(); }
            Va(5, 5); Bombea(100); log.Clear();
        }
        finally { if (modo == 2) { PantallaRocola.LaCapa = capa; PantallaRocola.FondoDeLaVentana = null; } }
        return r.ToArray();
    }

    // (rotulos-y-letras) EL VIDEO: el lugar que da la pantalla (VideoEnPantalla.Lugar, Controls.Add del control del WMP en XP) va al fondo del
    // orden de la ventana; AlFrente lo pone arriba; Pon cambia su sitio y si se ve; Quita lo saca. En una ventana de la capa, un hueco de
    // textura (HuecoDelVideo, Plataforma/Anfitrion.cs) en la lista de controles; sin la capa, la capa de textura en la lista nativa.
    const string FV = "el control del WMP de XP: Controls.Add (al final = el fondo), BringToFront (SetChildIndex(this, 0)), SetBounds/Visible y Controls.Remove";
    static readonly string[] esperadoV =
    {
        "agregado fondo 0,0,0x0 visible",
        "alfrente arriba 10,20,30x40 visible tex=7 50x60",
        "escondido 10,20,30x40 oculto",
        "quitado nada",
    };
    static string[] EscenaVideo(bool conCapa)
    {
        List<string> r = new List<string>();
        PantallaRocola.ILaCapa capa = PantallaRocola.LaCapa;
        if (!conCapa) PantallaRocola.LaCapa = null;
        try
        {
            Gemelo g = null; VistaFalsa vf = null;
            if (conCapa) { g = new Gemelo("B"); g.SetBounds(0, 0, 10, 10); m.Controls.Add(g); }
            else { vf = new VistaFalsa("B", null); vf.SetBounds(0, 0, 10, 10); PantallaRocola.AgregaVista(null, vf); }
            ILugarDelVideo lugar = new VideoEnPantalla().Lugar(conCapa ? (object)m : null);
            // donde esta (el ultimo de la lista = el fondo) y como esta: en la capa, el ultimo control de la ventana; sin ella, la lista nativa
            Func<string> como = delegate
            {
                if (conCapa)
                {
                    Control h = null; int idx = -1;
                    for (int i = 0; i < m.Controls.Count; i++) if (m.Controls[i].GetType().Name == "HuecoDelVideo") { h = m.Controls[i]; idx = i; }
                    if (h == null) return "nada";
                    return (idx == m.Controls.Count - 1 ? "fondo" : idx == 0 ? "arriba" : "medio") + " " + h.Left + "," + h.Top + "," + h.Width + "x" + h.Height + " " + (h.VisiblePropio ? "visible" : "oculto")
                        + (h.TexturaGl != 0 ? " tex=" + h.TexturaGl + " " + h.AnchoGl + "x" + h.AltoGl : "");
                }
                object[] l = PantallaRocola.ListaDeLaVentana; CapaDeTextura t = null; int k = -1;
                for (int i = 0; i < l.Length; i++) if (l[i] is CapaDeTextura) { t = (CapaDeTextura)l[i]; k = i; }
                if (t == null) return "nada";
                return (k == l.Length - 1 ? "fondo" : k == 0 ? "arriba" : "medio") + " " + t.Sitio.Left + "," + t.Sitio.Top + "," + t.Sitio.Width + "x" + t.Sitio.Height + " " + (t.Visible ? "visible" : "oculto")
                    + (t.Textura != 0 ? " tex=" + t.Textura + " " + t.AnchoGl + "x" + t.AltoGl : "");
            };
            r.Add("agregado " + como());
            lugar.Pon(new Rectangle(10, 20, 30, 40), true); lugar.Textura(7, 50, 60); lugar.AlFrente();
            r.Add("alfrente " + como());
            lugar.Pon(new Rectangle(10, 20, 30, 40), false);
            r.Add("escondido " + como().Replace(" tex=7 50x60", "").Replace("arriba ", ""));
            lugar.Quita();
            r.Add("quitado " + como());
            if (conCapa) { m.Controls.Remove(g); g.Dispose(); } else PantallaRocola.QuitaVista(vf);
            Bombea(50); log.Clear();
        }
        finally { PantallaRocola.LaCapa = capa; }
        return r.ToArray();
    }

    // (vistas-buscador) EL TABULADOR. Cuatro en este orden de alta (su TabIndex): B (Selectable, TabStop = false: la casilla de BUSCAR), S y X
    // (Selectable y TabStop: el buscador de siempre) y N (sin Selectable: un panel). Tab solo se para en S y X; Mayus+Tab al reves; Focus da
    // el foco aunque no tenga TabStop; las flechas recorren sin mirar TabStop; esconder la del foco lo pasa a la siguiente.
    const string FT = "Form.ProcessTabKey (Form.cs:4995-5015) y ContainerControl.ProcessArrowKey (ContainerControl.cs:1119-1128): SelectNextControl/GetNextControl " +
                      "(Control.cs:10709-10721 y 6177-6351) por TabIndex (ControlCollection.Add), CanSelect (Selectable y visible) y TabStop; Control.Focus no mira TabStop; " +
                      "SetVisibleCore -> SelectNextIfFocused (Control.cs:11221-11228 y 10770-10784)";
    static readonly string[] esperadoT =
    {
        "enfoca S",
        "tab X,S,X,S",
        "mayustab X,S",
        "enfoca-sin-tabstop B",
        "tab-desde-B S",
        "flechas X,B,X",
        "esconde-la-del-foco S",
        "tab-con-una-escondida S",
    };
    static string Foco(int modo)
    {
        if (modo == 2) { object f = PantallaRocola.FocoDeLaVentana; return f == null ? "(nadie)" : f.ToString(); }
        foreach (Control c in m.Controls) if (c.Focused) { object v = Anfitrion.VistaDeHueco(c); return (v ?? c).ToString(); }
        return "(nadie)";
    }
    static string Pulsa(int modo, Tecla t, int veces, bool mayus)
    {
        List<string> r = new List<string>();
        for (int i = 0; i < veces; i++)
        {
            if (mayus) T(Tecla.LShiftKey, true);
            T(t, true); T(t, false);
            if (mayus) T(Tecla.LShiftKey, false);
            Bombea(20); r.Add(Foco(modo));
        }
        return string.Join(",", r);
    }
    static string[] EscenaTab(int modo)
    {
        List<string> r = new List<string>();
        Bombea(100);
        string[] n = { "B", "S", "X", "N" };
        bool[] sel = { true, true, true, false }, tab = { false, true, true, true };
        IVista[] v = new IVista[4];
        for (int i = 0; i < 4; i++)
        {
            if (modo == 0) { Gemelo g = new Gemelo(n[i], sel[i], tab[i]); v[i] = g; }
            else { VistaFalsa f = new VistaFalsa(n[i], null); f.Sel = sel[i]; f.Tab = tab[i]; v[i] = f; }
            v[i].SetBounds(10 + i * 40, 10, 30, 30);
            PantallaRocola.AgregaVista(modo == 2 ? null : (object)m, v[i]);
        }
        Bombea(50);
        PantallaRocola.EnfocaVista(v[1]); Bombea(20);
        r.Add("enfoca " + Foco(modo));
        r.Add("tab " + Pulsa(modo, Tecla.Tab, 4, false));
        r.Add("mayustab " + Pulsa(modo, Tecla.Tab, 2, true));
        PantallaRocola.EnfocaVista(v[0]); Bombea(20);
        r.Add("enfoca-sin-tabstop " + Foco(modo));
        r.Add("tab-desde-B " + Pulsa(modo, Tecla.Tab, 1, false));
        r.Add("flechas " + Pulsa(modo, Tecla.Right, 1, false) + "," + Pulsa(modo, Tecla.Right, 1, false) + "," + Pulsa(modo, Tecla.Left, 1, false));
        v[2].Visible = false; Bombea(20);
        r.Add("esconde-la-del-foco " + Foco(modo));
        r.Add("tab-con-una-escondida " + Pulsa(modo, Tecla.Tab, 1, false));
        v[2].Visible = true;
        for (int i = 0; i < 4; i++) { PantallaRocola.QuitaVista(v[i]); if (v[i] is Gemelo) ((Gemelo)v[i]).Dispose(); }
        if (modo == 2) PantallaRocola.FocoDeLaVentana = null;
        Bombea(50); log.Clear();
        return r.ToArray();
    }

    // (panel-del-disco) LO QUE XP HACE CON EL PANEL DEL DISCO: panelSongs.Controls.Add(...) de dos hijas ANTES de agregar el panel a la ventana
    // (Form1.cs:1244-1247 y 1339), y despues lo de un Panel de WinForms con sus hijos: su posicion es relativa al panel (Control.Bounds en el
    // cliente del padre), se recortan al cliente del panel (son ventanas hijas, WS_CHILD), la primera que se agrega queda arriba
    // (ControlCollection.Add al final; el [0] arriba), BringToFront (SetChildIndex(this, 0)) la sube, el raton va a la ventana hija de mas
    // arriba bajo la flecha (WindowFromPoint / ChildWindowFromPointEx) y con las coordenadas de ELLA, el panel escondido esconde a las hijas
    // (GetVisibleCore) y moverlo las mueve.
    const string FP = "posicion: Control.Bounds (en el cliente del padre); recorte: WS_CHILD; orden: ControlCollection.Add y BringToFront (SetChildIndex(this, 0)); " +
                      "raton: WindowFromPoint / ChildWindowFromPointEx baja a la hija de mas arriba (Control.cs:5806), coordenadas de la hija; GetVisibleCore: la suya y la del padre";
    static readonly string[] esperadoP =
    {
        "posicion FFFFFF00 FFFF0000 FF0000FF",
        "recorte FF00FF00 FF00FF00",
        "orden FFFF0000",
        "raton-mueve P.Entra,P.Mueve(0),P.Sale,C1.Entra,C1.Mueve(0),C1.Sale,C2.Entra,C2.Mueve(0),C2.Sale",
        "raton-clic C2.Abajo(1),C2.Clic(1)@50,50,C2.Arriba(1)",
        "raton-captura C2.Abajo(1),C2.Mueve(0),C2.Arriba(1),C2.Sale,P.Entra",
        "alfrente FF0000FF",
        "mueve-dueno FFFF0000 FF0000FF FF00FF00",
        "escondido FF00FF00 FF00FF00",
        "raton-escondido ",
        "hija-quitada FFFFFF00",
    };
    // modo 0: un Control de la capa de panel y de hijas (el gemelo); 1: vistas falsas en la capa; 2: vistas falsas en la lista nativa (sin la capa)
    static string[] EscenaPanel(int modo)
    {
        List<string> r = new List<string>();
        Func<int, int, string> px = modo == 2 ? (Func<int, int, string>)delegate (int x, int y) { return Pinta(x, y); } : Px;
        Xy = true;
        if (modo != 2) Bombea(150);
        IVista panel, c1, c2;
        if (modo == 0)
        {
            Gemelo p = new Gemelo("P"); p.BackColor = Color.FromArgb(255, 255, 0); p.SetBounds(30, 30, 120, 100);
            Gemelo a = new Gemelo("C1"); a.BackColor = Color.FromArgb(255, 0, 0); a.SetBounds(10, 10, 60, 60);
            Gemelo b = new Gemelo("C2"); b.BackColor = Color.FromArgb(0, 0, 255); b.SetBounds(40, 30, 100, 80);
            panel = p; c1 = a; c2 = b;
        }
        else
        {
            VistaFalsa p = new VistaFalsa("P", Amarillo); p.SetBounds(30, 30, 120, 100);
            VistaFalsa a = new VistaFalsa("C1", Rojo); a.SetBounds(10, 10, 60, 60);
            VistaFalsa b = new VistaFalsa("C2", Azul); b.SetBounds(40, 30, 100, 80);
            panel = p; c1 = a; c2 = b;
        }
        // Form1: las hijas al panel ANTES de ponerlo en la ventana (Controls.Add: la primera, arriba)
        PantallaRocola.AgregaVista(panel, c1); PantallaRocola.AgregaVista(panel, c2);
        PantallaRocola.AgregaVista(modo == 2 ? null : (object)m, panel);
        if (modo == 2) { PantallaRocola.FondoDeLaVentana = delegate (SKCanvas g) { g.Clear(Verde); }; Va(5, 5); }
        r.Add("posicion " + px(35, 35) + " " + px(45, 45) + " " + px(120, 110));
        r.Add("recorte " + px(160, 100) + " " + px(140, 135));
        r.Add("orden " + px(80, 80));
        Va(5, 5); log.Clear();
        Va(35, 35); Va(45, 45); Va(120, 110); Va(160, 100);
        r.Add("raton-mueve " + Log());
        Va(120, 110); log.Clear(); Clic(true); Clic(false); Bombea(650);
        r.Add("raton-clic " + Log());
        Va(120, 110); log.Clear(); Clic(true); Va(35, 35); Clic(false); Bombea(650);
        r.Add("raton-captura " + Log());
        PantallaRocola.VistaAlFrente(c2);
        r.Add("alfrente " + px(80, 80));
        panel.SetBounds(60, 60, 120, 100);
        r.Add("mueve-dueno " + px(75, 75) + " " + px(150, 100) + " " + px(190, 100));
        panel.Visible = false;
        r.Add("escondido " + px(75, 75) + " " + px(150, 100));
        Va(5, 5); log.Clear(); Va(75, 75); Va(150, 100); Clic(true); Clic(false); Bombea(650);
        r.Add("raton-escondido " + Log());
        panel.Visible = true;
        PantallaRocola.QuitaVista(c1);
        r.Add("hija-quitada " + px(75, 75));
        PantallaRocola.QuitaVista(panel);
        if (modo == 0) { PantallaRocola.QuitaVista(c2); ((Gemelo)panel).Dispose(); ((Gemelo)c1).Dispose(); ((Gemelo)c2).Dispose(); }
        Xy = false;
        Va(5, 5); Bombea(650); log.Clear();
        return r.ToArray();
    }

    // (armado-vistas, oleada 3) QUIEN ESCUCHA EL RATON DESDE FUERA (PantallaRocola.OyeRaton: el «ayudaView.MouseClick +=» y el
    // «attractView.MouseDown +=» de Form1.cs), EL FOCO DE UNA VISTA (EnfocaVista: searchOverlay.Focus()) Y QUIEN LA DESECHA
    static string[] Oyentes(bool gemelo)
    {
        List<string> r = new List<string>();
        Bombea(150);
        Panel dueno = new Panel(); dueno.SetBounds(0, 0, 300, 220); m.Controls.Add(dueno);
        IVista v;
        if (gemelo) { Gemelo g = new Gemelo("V"); g.BackColor = Color.FromArgb(0, 0, 255); g.SetBounds(60, 60, 100, 100); v = g; }
        else { VistaFalsa f = new VistaFalsa("V", Azul); f.SetBounds(60, 60, 100, 100); v = f; }
        PantallaRocola.OyeRaton(v, AvisoRaton.Abajo, delegate (Raton e) { log.Add("oye-abajo(" + e.X + "," + e.Y + "," + e.Button + "," + e.Clicks + ")"); });
        PantallaRocola.OyeRaton(v, AvisoRaton.Clic, delegate (Raton e) { log.Add("oye-clic(" + e.X + "," + e.Y + "," + e.Button + "," + e.Clicks + ")"); });
        PantallaRocola.AgregaVista(dueno, v);
        Va(5, 5); Va(80, 80); Bombea(30); log.Clear();
        Clic(true); Clic(false); Bombea(650);
        r.Add("oyentes " + Log());
        bool f1 = PantallaRocola.EnfocaVista(v);
        r.Add("foco " + f1 + " " + TrazaVentanas.Pieza(Escritorio.Foco));
        v.Visible = false; Bombea(30);
        r.Add("foco-escondida " + PantallaRocola.EnfocaVista(v));
        v.Visible = true;
        Va(5, 5); Bombea(30); log.Clear();
        PantallaRocola.QuitaVista(v); Bombea(30);
        r.Add("quitada " + Log());
        PantallaRocola.AgregaVista(dueno, v);
        m.Controls.Remove(dueno); dueno.Dispose(); Bombea(30);
        r.Add("desecha-dueno " + Log());
        return r.ToArray();
    }

    // SIN LA CAPA: la lista nativa de la ventana (PantallaRocola.Vistas.cs) con dos vistas falsas, como quedara al final
    static void SinLaCapa()
    {
        PantallaRocola.ILaCapa capa = PantallaRocola.LaCapa;
        PantallaRocola.LaCapa = null;
        try
        {
            PantallaRocola.FondoDeLaVentana = delegate (SKCanvas g) { g.Clear(Verde); };
            VistaFalsa a = new VistaFalsa("A", Rojo); a.SetBounds(20, 20, 100, 100);
            VistaFalsa v = new VistaFalsa("V", Azul); v.SetBounds(60, 60, 100, 100);
            PantallaRocola.AgregaVista(null, a); PantallaRocola.AgregaVista(null, v);
            Mira("sin la capa: el orden de la lista", PantallaRocola.IndiceDe(a) + " " + PantallaRocola.IndiceDe(v), "0 1", "ControlCollection.Add: al final");
            Mira("sin la capa: composicion", Pinta(80, 80) + " " + Pinta(140, 140) + " " + Pinta(5, 5), "FFFF0000 FF0000FF FF00FF00", "el [0] arriba; el fondo de la ventana debajo de todo");
            // EL RATON, el mismo guion que con la capa
            Va(5, 5); log.Clear();
            Va(80, 80); Va(140, 140);
            Mira("sin la capa: raton-mueve", Log(), "A.Entra,A.Mueve(0),A.Sale,V.Entra,V.Mueve(0)", "WindowFromPoint; WM_MOUSELEAVE y WM_MOUSEMOVE");
            Clic(true); Clic(false); Bombea(650);
            Mira("sin la capa: raton-clic", Log(), "V.Abajo(1),V.Clic(1),V.Arriba(1)", "WmMouseUp: Click/MouseClick y despues MouseUp");
            Clic(true); Va(80, 80); Clic(false); Bombea(650);
            Mira("sin la capa: raton-captura", Log(), "V.Abajo(1),V.Mueve(0),V.Arriba(1),V.Sale,A.Entra", "SetCapture en WmMouseDown; sin clic si se suelta fuera (WindowFromPoint(pt) != Handle)");
            Va(150, 150); log.Clear();
            Clic(true); Clic(false); Clic(true); Clic(false); Bombea(650);
            Mira("sin la capa: raton-doble", Log(), "V.Abajo(1),V.Clic(1),V.Arriba(1),V.Abajo(2),V.DobleClic(2),V.Arriba(1)", "WM_LBUTTONDBLCLK (500 ms, 4 puntos) y STATE_DOUBLECLICKFIRED en WmMouseUp");
            PantallaRocola.VistaAlFrente(v);
            Mira("sin la capa: BringToFront", PantallaRocola.IndiceDe(v) + " " + Pinta(80, 80), "0 FF0000FF", "SetChildIndex(this, 0)");
            Va(100, 100); Va(80, 80);
            Mira("sin la capa: raton-alfrente", Log(), "V.Mueve(0),V.Mueve(0)", "la de encima recibe el raton");
            // el recorte por el de encima: una vista transparente (no pinta nada) encima de V ensena el fondo de la ventana, no a V
            VistaFalsa t = new VistaFalsa("T", null); t.SetBounds(130, 130, 60, 60); PantallaRocola.AgregaVista(null, t); PantallaRocola.VistaAlFrente(t);
            Mira("sin la capa: transparente encima (WS_CLIPSIBLINGS)", Pinta(145, 145) + " " + Pinta(170, 170), "FF00FF00 FF00FF00", "Control.CreateParams, Control.cs:1691: WS_CLIPSIBLINGS");
            PantallaRocola.QuitaVista(t);
            // una capa de textura (la tarjeta, el video) parte los tramos: lo de encima va en otro
            CapaDeTextura tx = new CapaDeTextura { Sitio = new Rectangle(0, 0, 50, 50), Textura = 7, AnchoGl = 50, AltoGl = 50 };
            PantallaRocola.AgregaCapa(tx);   // (al fondo)
            PantallaRocola.VistaAlFrente(tx); PantallaRocola.VistaAlFondo(v);   // orden: tx, a, v
            List<CapaPantalla> l = new List<CapaPantalla>(); PantallaRocola.CapasDeLaVentana(l);
            Mira("sin la capa: tramos con una textura en medio", l.Count + " " + (l.Count == 3 ? l[0].Tipo + "," + l[1].Tipo + "," + l[2].Tipo : ""), "3 vista,tarjeta,vista", "cada ventana hija en su sitio del orden z");
            Mira("sin la capa: la textura recorta lo de debajo", Pinta(30, 30, 0), "FF00FF00", "WS_CLIPSIBLINGS: debajo de la tarjeta no se pinta el control de debajo");
            Va(10, 10); log.Clear(); Clic(true); Clic(false); Bombea(650);
            Mira("sin la capa: el raton sobre la textura", Log(), "", "el GpuPanel se queda con el raton (WindowFromPoint) aunque no lo use");
            PantallaRocola.QuitaVista(tx);
            v.Visible = false;
            Mira("sin la capa: escondida", Pinta(140, 140), "FF00FF00", "Visible = false: ni se pinta ni recibe el raton");
            Va(90, 90); Va(140, 140); log.Clear(); Clic(true); Clic(false); Bombea(650);
            Mira("sin la capa: raton sobre la escondida", Log(), "", "WindowFromPoint salta las que no se ven");
            // LA RUEDA: a la del foco; si no la atiende, a la ventana
            v.Visible = true; PantallaRocola.FocoDeLaVentana = v; string vent = "";
            PantallaRocola.RuedaDeLaVentana = delegate (Raton e) { vent += "ventana(" + e.Delta + ")"; };
            PantallaRocola.RatonRueda(-1);
            Mira("sin la capa: rueda sin atender", Log() + " " + vent, "V.Rueda(0) ventana(-120)", "WmMouseWheel, Control.cs:12748-12764: sin Handled, DefWindowProc la pasa al padre");
            v.ManejaRueda = true; vent = ""; PantallaRocola.RatonRueda(2);
            Mira("sin la capa: rueda atendida", Log() + " " + vent, "V.Rueda(0) ", "con Handled no sigue");
            PantallaRocola.FocoDeLaVentana = null; PantallaRocola.RuedaDeLaVentana = null;
            // (armado-vistas) quien escucha el raton desde fuera, despues de la vista (el evento del On* de fabrica), y el foco de la vista
            PantallaRocola.OyeRaton(v, AvisoRaton.Abajo, delegate (Raton e) { log.Add("oye-abajo(" + e.X + "," + e.Y + "," + e.Button + "," + e.Clicks + ")"); });
            PantallaRocola.OyeRaton(v, AvisoRaton.Clic, delegate (Raton e) { log.Add("oye-clic(" + e.X + "," + e.Y + "," + e.Button + "," + e.Clicks + ")"); });
            PantallaRocola.VistaAlFrente(v); Va(5, 5); Va(80, 80); log.Clear(); Clic(true); Clic(false); Bombea(650);
            Mira("sin la capa: quien escucha el raton", Log(), "V.Abajo(1),oye-abajo(20,20,Left,1),V.Clic(1),oye-clic(20,20,Left,1),V.Arriba(1)", "Control.OnMouseDown/OnMouseClick lanzan el evento despues de lo de la vista");
            bool fo = PantallaRocola.EnfocaVista(v);
            Mira("sin la capa: Focus", fo + " " + (PantallaRocola.FocoDeLaVentana == (object)v), "True True", "FocusInternal: SetFocus y devuelve Focused");
            v.Visible = false; PantallaRocola.FocoDeLaVentana = null;
            Mira("sin la capa: Focus de una escondida", PantallaRocola.EnfocaVista(v) + " " + (PantallaRocola.FocoDeLaVentana == null), "False True", "CanFocus: IsWindowVisible");
            v.Visible = true;
            PantallaRocola.QuitaVista(a); PantallaRocola.QuitaVista(v);
            // (panel-del-disco) una vista dentro de otra en la lista nativa: lo mismo que en la capa
            string[] panNat = EscenaPanel(2);
            for (int i = 0; i < esperadoP.Length; i++)
                Mira("sin la capa, panel con hijas: " + esperadoP[i].Substring(0, esperadoP[i].IndexOf(' ')), i < panNat.Length ? panNat[i] : "(nada)", esperadoP[i], FP);
            Mira("sin la capa, panel con hijas: nada queda en la lista", PantallaRocola.ListaDeLaVentana.Length + "", "0", "Controls.Remove");
            PantallaRocola.FondoDeLaVentana = delegate (SKCanvas g) { g.Clear(Verde); };
            Teclas();
            // (vistas-buscador) el tabulador en la lista nativa: lo mismo que la capa
            PantallaRocola.Teclas = new PantallaRocola.TeclasDeLaVentana { ProcesaMando = delegate (ref PulsoTecla mm, Tecla d) { return false; } };
            try
            {
                string[] tabNat = EscenaTab(2);
                for (int i = 0; i < esperadoT.Length; i++)
                    Mira("sin la capa, tabulador: " + esperadoT[i].Substring(0, esperadoT[i].IndexOf(' ')), i < tabNat.Length ? tabNat[i] : "(nada)", esperadoT[i], FT);
            }
            finally { PantallaRocola.Teclas = null; }
        }
        finally { PantallaRocola.LaCapa = capa; PantallaRocola.FondoDeLaVentana = null; }
    }
    // un punto de lo que compone la lista nativa (el tramo de mas arriba que no es transparente ahi), como 0xAARRGGBB
    static string Pinta(int x, int y, int tramo = -1)
    {
        List<CapaPantalla> l = new List<CapaPantalla>(); PantallaRocola.CapasDeLaVentana(l);
        for (int i = tramo >= 0 ? tramo : l.Count - 1; i >= 0; i--)
        {
            if (l[i].Mapa == null) continue;
            SKColor c = l[i].Mapa.GetPixel(x, y);
            if (c.Alpha != 0 || tramo >= 0) return ((uint)c).ToString("X8");
        }
        return "00000000";
    }

    // LAS TECLAS A LA VENTANA, sin la capa: ProcessCmdKey primero (PreProcessMessage, Control.cs:9275-9300), ProcessDialogKey, KeyDown,
    // el caracter (WM_CHAR de TranslateMessage) a KeyPress salvo SuppressKeyPress, y al soltar KeyUp; Alt+F4 sin atender cierra por la cola
    // (DefWindowProc de WM_SYSKEYDOWN: SC_CLOSE); la repeticion (bit 30 de WM_KEYDOWN) llega en el pulso
    static void Teclas()
    {
        PantallaRocola.Teclas = new PantallaRocola.TeclasDeLaVentana
        {
            ProcesaMando = delegate (ref PulsoTecla m, Tecla d) { log.Add("mando(" + d + (m.Repetida ? ",rep" : "") + ")"); return (d & Tecla.KeyCode) == Tecla.F3; },
            TeclaDeDialogo = delegate (Tecla d) { log.Add("dialogo(" + d + ")"); return false; },
            TeclaAbajo = delegate (PulsoTecla e) { log.Add("abajo(" + e.KeyData + ")"); if (e.KeyCode == Tecla.B) e.SuppressKeyPress = true; },
            Caracter = delegate (PulsoCaracter e) { log.Add("caracter(" + (int)e.KeyChar + ")"); },
            TeclaArriba = delegate (PulsoTecla e) { log.Add("arriba(" + e.KeyData + ")"); },
            CierraPorSistema = delegate { log.Add("cierra"); },
        };
        try
        {
            log.Clear();
            T(Tecla.A, true); T(Tecla.A, false);
            Mira("teclas: una letra", Log(), "mando(A),dialogo(A),abajo(A),caracter(97),arriba(A)", "ProcessCmdKey, ProcessDialogKey, KeyDown, KeyPress y KeyUp");
            T(Tecla.F3, true); T(Tecla.F3, false);
            Mira("teclas: ProcessCmdKey la atiende", Log(), "mando(F3),arriba(F3)", "PreProcessMessage: con true no sigue");
            T(Tecla.B, true); T(Tecla.B, false);
            Mira("teclas: SuppressKeyPress", Log(), "mando(B),dialogo(B),abajo(B),arriba(B)", "KeyEventArgs.SuppressKeyPress: sin WM_CHAR");
            T(Tecla.LShiftKey, true); T(Tecla.A, true); T(Tecla.A, false); T(Tecla.LShiftKey, false);
            Mira("teclas: Mayus y A", Log(), "mando(ShiftKey, Shift),dialogo(ShiftKey, Shift),abajo(ShiftKey, Shift),mando(A, Shift),dialogo(A, Shift),abajo(A, Shift),caracter(65),arriba(A, Shift),arriba(ShiftKey)",
                 "VK_SHIFT con su modificador; KeyUp de Mayus ya sin Shift");
            T(Tecla.C, true); T(Tecla.C, true); T(Tecla.C, false);
            Mira("teclas: la repeticion", Log(), "mando(C),dialogo(C),abajo(C),caracter(99),mando(C,rep),dialogo(C),abajo(C),caracter(99),arriba(C)", "bit 30 de lParam de WM_KEYDOWN");
            T(Tecla.Menu, true); T(Tecla.F4, true); Bombea(30); T(Tecla.F4, false); T(Tecla.Menu, false);
            Mira("teclas: Alt+F4", Log(), "mando(Menu, Alt),dialogo(Menu, Alt),abajo(Menu, Alt),mando(F4, Alt),dialogo(F4, Alt),abajo(F4, Alt),cierra,arriba(F4, Alt),arriba(Menu)", "DefWindowProc de WM_SYSKEYDOWN: SC_CLOSE por la cola");
        }
        finally { PantallaRocola.Teclas = null; }
    }
    static void T(Tecla t, bool abajo) { PantallaRocola.LlegaTecla(t, abajo); }
}

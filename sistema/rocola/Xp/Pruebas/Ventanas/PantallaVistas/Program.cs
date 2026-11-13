// LA LISTA DE LA VENTANA CON VISTAS FALSAS (port nativo, tanda 4, grupo pantalla-nativa: W2; sin la capa desde N7, borrar-capas). Vistas
// que no heredan de nada (VistaFalsa, con su CajaVista) en la lista de la ventana de la pantalla (Nativo/PantallaRocola.Vistas.cs): el orden
// de composicion, el recorte por las de encima, el reparto del raton, el foco y el tabulador, la vista de la tarjeta, el rotulo con forma,
// el video y las teclas a la ventana tienen que salir como dice WinForms. Cada prueba dice lo obtenido y, si falla, lo que daria WinForms y
// su sitio en la fuente (dotnet/winforms release/3.0, portada de .NET Framework 4.8).
// HASTA N7 cada escena se corria tambien con un control de la capa de la transicion en lugar de la vista (el «gemelo», que la capa movia con
// las reglas de WinForms que fijaban las 41 pruebas) y con la vista en un hueco de una ventana de la capa, y las tres tenian que dar lo
// mismo (194 comprobaciones). La capa se borro: quedan las reglas ya fijadas entonces, como comprobaciones de la lista de la ventana contra
// esos mismos valores (los «esperado...» de abajo no cambiaron).
//   pruebas_vistas.sh PROGRAMA ETIQUETA   (fuera de pantalla: ROCOLA_PANTALLA=fuera:320x240)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Rocola;
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
    static void Bombea(int ms) { Stopwatch t = Stopwatch.StartNew(); do { PantallaRocola.Bombea(); Thread.Sleep(1); } while (t.ElapsedMilliseconds < ms); }
    static readonly SKColor Rojo = new SKColor(255, 0, 0), Azul = new SKColor(0, 0, 255), Verde = new SKColor(0, 255, 0), Amarillo = new SKColor(255, 255, 0);
    static bool Xy;   // (panel-del-disco) el clic apunta tambien donde cayo, en las coordenadas de la vista

    // LA VISTA FALSA: una IVista sin herencia, con su caja; se pinta de su color (fondo y contenido) y apunta su raton
    sealed class VistaFalsa : IVista, IDisposable
    {
        public void Dispose() { log.Add(n + ".Dispose"); }   // (armado-vistas: la desecha la pantalla al desecharse su ventana, como WinForms a los hijos)
        readonly CajaVista caja; readonly string n; readonly SKColor? color; public bool ManejaRueda;
        public VistaFalsa(string n, SKColor? color) { this.n = n; this.color = color; caja = new CajaVista(this); }
        public bool Sel = true, Tab = true;   // (vistas-buscador) ControlStyles.Selectable y TabStop
        public bool Seleccionable { get { return Sel; } }
        public bool ParaElTabulador { get { return Tab; } }
        public bool Tarjeta;   // (gpupanel-sin-control) la pinta la tarjeta, como el GpuPanel: su Pinta es el WM_PAINT (RenderFrame) y no pinta nada
        public bool DeLaTarjeta { get { return Tarjeta; } }
        public override string ToString() { return n; }
        public Rectangle Bounds { get { return caja.Bounds; } set { caja.Bounds = value; } }
        public int Left { get { return caja.Left; } set { caja.Left = value; } }
        public int Top { get { return caja.Top; } set { caja.Top = value; } }
        public int Width { get { return caja.Width; } set { caja.Width = value; } }
        public int Height { get { return caja.Height; } set { caja.Height = value; } }
        public void SetBounds(int x, int y, int w, int h) { caja.SetBounds(x, y, w, h); }
        public bool Visible { get { return caja.Visible; } set { caja.Visible = value; } }
        public void Invalidate() { caja.Invalidate(); }
        public void Pinta(SKCanvas g) { if (Tarjeta) { log.Add(n + ".Pinta"); return; } if (color != null) using (SKPaint p = new SKPaint { Color = color.Value }) g.DrawRect(0, 0, Width, Height, p); }
        public void Raton(AvisoRaton que, Raton e)
        {
            log.Add(n + "." + que + (que == AvisoRaton.Entra || que == AvisoRaton.Sale ? "" : "(" + e.Clicks + ")") + (Xy && que == AvisoRaton.Clic ? "@" + e.X + "," + e.Y : ""));
            if (que == AvisoRaton.Rueda && ManejaRueda) e.Handled = true;
        }
    }
    static void Va(int x, int y) { PantallaRocola.RatonEn(x, y); }
    static void Clic(bool abajo) { PantallaRocola.RatonBoton(BotonRaton.Left, abajo); }

    static int Main()
    {
        Anfitrion.Arranca();   // la pantalla fuera de pantalla y el compositor nativo (como el programa: Plataforma/PruebasLinux.cs)
        PantallaRocola.Hilo = Thread.CurrentThread;
        PantallaRocola.EscondeFlecha();   // (Cursor.Hide, como la rocola)
        try { Pruebas(); } catch (Exception ex) { Console.WriteLine("LA PRUEBA TRONO: " + ex); falla++; }
        Console.WriteLine("pasan " + pasa + ", fallan " + falla);
        return falla == 0 ? 0 : 1;
    }

    static void Pruebas()
    {
        // (rotulos-y-letras) EL ROTULO CON FORMA (Control.Region: el aviso sin tarjeta, lblToast) Y EL VIDEO (la capa de textura de la lista)
        string[] rotNat = EscenaRotulo();
        for (int i = 0; i < esperadoR.Length; i++)
        {
            string nombre = esperadoR[i].Substring(0, esperadoR[i].IndexOf(' '));
            Mira("rotulo con forma: " + nombre, i < rotNat.Length ? rotNat[i] : "(nada)", esperadoR[i], FR);
        }
        string[] vidNat = EscenaVideo();
        for (int i = 0; i < esperadoV.Length; i++)
        {
            string nombre = esperadoV[i].Substring(0, esperadoV[i].IndexOf(' '));
            Mira("el video (capa de la lista): " + nombre, i < vidNat.Length ? vidNat[i] : "(nada)", esperadoV[i], FV);
        }
        LaLista();
    }
    static string Busca(string[] r, string nombre) { foreach (string x in r) if (x == nombre || x.StartsWith(nombre + " ")) return x; return "(nada)"; }

    // (gpupanel-sin-control) LA VISTA DE LA TARJETA. Su alta (Controls.Add + BringToFront, Form1.Video.cs:981 y 990), su capa de textura
    // en su sitio, Present en su superficie (el Handle de XP), su WM_PAINT al invalidarla o ensenarla (y no al moverla sin cambiar de medida:
    // sin ResizeRedraw), el raton con quien lo oye desde fuera (MouseMove, MouseDown, MouseClick y MouseWheel de Form1.Video.cs:851-883),
    // FindForm().WindowState y soltar la superficie (la ventana destruida: Present ya no la encuentra)
    const string FTar = "WM_PAINT: Invalidate y ShowWindow lo dan, un movimiento sin cambiar de medida no (sin ResizeRedraw: Control.SetBoundsCore); raton: los On* " +
                        "lanzan MouseMove, MouseDown, MouseClick y MouseWheel despues de lo de la vista (Control.OnMouse*); la rueda va al foco (WmMouseWheel); " +
                        "Present de Direct3D 9 pone el fondo en la ventana del dispositivo (DeviceWindow), y una ventana destruida ya no lo recibe";
    // (gpupanel-sin-control) EL TABULADOR CON LA VISTA DE LA TARJETA: en XP el GpuPanel esta en el ciclo («HelpBar LetterBar AlbumView
    // ProgressView GpuPanel»: Selectable y TabStop de fabrica; Controls.Add le da el TabIndex mayor de sus hermanos mas uno). H y L (las de
    // antes), N (Selectable sin TabStop) y G (la de la tarjeta, al final). Y el TabIndex de una que pasa de un dueno a otro (lo conserva:
    // ControlCollection.Add solo pone el que no tiene) y el Add de una que ya esta (al fondo: SendToBack)
    const string FTT = "Form.ProcessTabKey: SelectNextControl por TabIndex (ControlCollection.Add: el mayor de los hermanos mas uno, solo si no tenia; " +
                       "Control.ControlCollection.cs:82-95) y a igualdad el orden de la coleccion (GetNextControl, Control.cs:6177-6351); Add de una que ya " +
                       "esta: SendToBack (Control.ControlCollection.cs:67-71); Add de una de otro dueno: sale de alli (73-77)";
    static readonly string[] esperadoTT =
    {
        "ciclo L,G,H,L,G",
        "mayustab L,H",
        "pasa-de-dueno A,P,X",
        "add-otra-vez 1",
    };
    static string[] EscenaTabTarjeta()
    {
        List<string> r = new List<string>();
        Bombea(100);
        object ventana = null;   // (sin la ventana de la rocola: la lista de la pantalla)
        string[] n = { "H", "L", "N", "G" };
        bool[] tab = { true, true, false, true };
        IVista[] v = new IVista[4];
        for (int i = 0; i < 4; i++)
        {
            { VistaFalsa f = new VistaFalsa(n[i], null); f.Tab = tab[i]; f.Tarjeta = i == 3; v[i] = f; }
            v[i].SetBounds(10 + i * 40, 10, 30, 30);
            PantallaRocola.AgregaVista(ventana, v[i]);
        }
        Bombea(50);
        PantallaRocola.EnfocaVista(v[0]); Bombea(20);
        r.Add("ciclo " + Pulsa(Tecla.Tab, 5, false));
        r.Add("mayustab " + Pulsa(Tecla.Tab, 2, true));
        for (int i = 0; i < 4; i++) PantallaRocola.QuitaVista(v[i]);
        PantallaRocola.FocoDeLaVentana = null;
        Bombea(50);
        // EL TabIndex QUE SE CONSERVA AL CAMBIAR DE DUENO: A entra en P (TabIndex 0 alli), X y P en la ventana (0 y 1), y A pasa a la ventana
        // con su 0: empata con X y va despues de X por el orden de la lista; el ciclo desde X: A, P, X
        IVista x = new VistaFalsa("X", null), p = new VistaFalsa("P", null), a = new VistaFalsa("A", null);
        x.SetBounds(10, 60, 30, 30); p.SetBounds(50, 60, 60, 60); a.SetBounds(5, 5, 20, 20);
        PantallaRocola.AgregaVista(p, a);
        PantallaRocola.AgregaVista(ventana, x); PantallaRocola.AgregaVista(ventana, p);
        PantallaRocola.AgregaVista(ventana, a);
        Bombea(50);
        PantallaRocola.EnfocaVista(x); Bombea(20);
        r.Add("pasa-de-dueno " + Pulsa(Tecla.Tab, 3, false));
        // Add OTRA VEZ de una que ya esta en la ventana: al fondo
        PantallaRocola.AgregaVista(ventana, x);
        int ix = PantallaRocola.IndiceDe(x);
        ix = ix == PantallaRocola.ListaDeLaVentana.Length - 1 ? 1 : 0;
        r.Add("add-otra-vez " + ix);
        foreach (IVista q in new[] { a, p, x }) PantallaRocola.QuitaVista(q);
        PantallaRocola.FocoDeLaVentana = null;
        Bombea(50); log.Clear();
        return r.ToArray();
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
    static string[] EscenaRotulo()
    {
        List<string> r = new List<string>();
        PantallaRocola.FondoDeLaVentana = delegate (SKCanvas g) { g.Clear(Verde); };
        try
        {
            Bombea(150);
            VistaFalsa av = new VistaFalsa("A", Rojo); av.SetBounds(20, 20, 100, 100); PantallaRocola.AgregaVista(null, av);
            RotuloVista rv = new RotuloVista(); rv.BackColor = Color.FromArgb(0, 0, 255); rv.SetBounds(60, 60, 100, 100);
            SKPath f = new SKPath(); f.AddRect(new SKRect(0, 0, 50, 100)); rv.Forma = f;
            PantallaRocola.AgregaVista(null, rv); PantallaRocola.VistaAlFrente(rv);
            r.Add("dentro " + Pinta(80, 80));
            r.Add("fuera-sobre-A " + Pinta(115, 80));
            r.Add("fuera " + Pinta(140, 140));
            Va(5, 5); log.Clear();
            Va(115, 80); Va(80, 80); Va(140, 140);
            r.Add("raton " + Log());
            rv.Forma = null;
            r.Add("sin-forma " + Pinta(115, 80) + " " + Pinta(140, 140));
            PantallaRocola.QuitaVista(rv); rv.Dispose();
            r.Add("quitado " + Pinta(115, 80));
            PantallaRocola.QuitaVista(av);
            Va(5, 5); Bombea(100); log.Clear();
        }
        finally { PantallaRocola.FondoDeLaVentana = null; }
        return r.ToArray();
    }

    // (rotulos-y-letras) EL VIDEO: el lugar que da la pantalla (VideoEnPantalla.Lugar, Controls.Add del control del WMP en XP) va al fondo del
    // orden de la ventana; AlFrente lo pone arriba; Pon cambia su sitio y si se ve; Quita lo saca: es la capa de textura de la lista.
    const string FV = "el control del WMP de XP: Controls.Add (al final = el fondo), BringToFront (SetChildIndex(this, 0)), SetBounds/Visible y Controls.Remove";
    static readonly string[] esperadoV =
    {
        "agregado fondo 0,0,0x0 visible",
        "alfrente arriba 10,20,30x40 visible tex=7 50x60",
        "escondido 10,20,30x40 oculto",
        "quitado nada",
    };
    static string[] EscenaVideo()
    {
        List<string> r = new List<string>();
        VistaFalsa vf = new VistaFalsa("B", null); vf.SetBounds(0, 0, 10, 10); PantallaRocola.AgregaVista(null, vf);
        ILugarDelVideo lugar = new VideoEnPantalla().Lugar(null);
        // donde esta (el ultimo de la lista = el fondo) y como esta
        Func<string> como = delegate
        {
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
        PantallaRocola.QuitaVista(vf);
        Bombea(50); log.Clear();
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
    static string Foco() { object f = PantallaRocola.FocoDeLaVentana; return f == null ? "(nadie)" : f.ToString(); }
    static string Pulsa(Tecla t, int veces, bool mayus)
    {
        List<string> r = new List<string>();
        for (int i = 0; i < veces; i++)
        {
            if (mayus) T(Tecla.LShiftKey, true);
            T(t, true); T(t, false);
            if (mayus) T(Tecla.LShiftKey, false);
            Bombea(20); r.Add(Foco());
        }
        return string.Join(",", r);
    }
    static string[] EscenaTab()
    {
        List<string> r = new List<string>();
        Bombea(100);
        string[] n = { "B", "S", "X", "N" };
        bool[] sel = { true, true, true, false }, tab = { false, true, true, true };
        IVista[] v = new IVista[4];
        for (int i = 0; i < 4; i++)
        {
            { VistaFalsa f = new VistaFalsa(n[i], null); f.Sel = sel[i]; f.Tab = tab[i]; v[i] = f; }
            v[i].SetBounds(10 + i * 40, 10, 30, 30);
            PantallaRocola.AgregaVista(null, v[i]);
        }
        Bombea(50);
        PantallaRocola.EnfocaVista(v[1]); Bombea(20);
        r.Add("enfoca " + Foco());
        r.Add("tab " + Pulsa(Tecla.Tab, 4, false));
        r.Add("mayustab " + Pulsa(Tecla.Tab, 2, true));
        PantallaRocola.EnfocaVista(v[0]); Bombea(20);
        r.Add("enfoca-sin-tabstop " + Foco());
        r.Add("tab-desde-B " + Pulsa(Tecla.Tab, 1, false));
        r.Add("flechas " + Pulsa(Tecla.Right, 1, false) + "," + Pulsa(Tecla.Right, 1, false) + "," + Pulsa(Tecla.Left, 1, false));
        v[2].Visible = false; Bombea(20);
        r.Add("esconde-la-del-foco " + Foco());
        r.Add("tab-con-una-escondida " + Pulsa(Tecla.Tab, 1, false));
        v[2].Visible = true;
        for (int i = 0; i < 4; i++) PantallaRocola.QuitaVista(v[i]);
        PantallaRocola.FocoDeLaVentana = null;
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
    static string[] EscenaPanel()
    {
        List<string> r = new List<string>();
        Func<int, int, string> px = delegate (int x, int y) { return Pinta(x, y); };
        Xy = true;
        VistaFalsa panel = new VistaFalsa("P", Amarillo); panel.SetBounds(30, 30, 120, 100);
        VistaFalsa c1 = new VistaFalsa("C1", Rojo); c1.SetBounds(10, 10, 60, 60);
        VistaFalsa c2 = new VistaFalsa("C2", Azul); c2.SetBounds(40, 30, 100, 80);
        // Form1: las hijas al panel ANTES de ponerlo en la ventana (Controls.Add: la primera, arriba)
        PantallaRocola.AgregaVista(panel, c1); PantallaRocola.AgregaVista(panel, c2);
        PantallaRocola.AgregaVista(null, panel);
        PantallaRocola.FondoDeLaVentana = delegate (SKCanvas g) { g.Clear(Verde); }; Va(5, 5);
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
        Xy = false;
        Va(5, 5); Bombea(650); log.Clear();
        return r.ToArray();
    }

    // LA LISTA DE LA VENTANA (PantallaRocola.Vistas.cs) con dos vistas falsas
    static void LaLista()
    {
        try
        {
            PantallaRocola.FondoDeLaVentana = delegate (SKCanvas g) { g.Clear(Verde); };
            VistaFalsa a = new VistaFalsa("A", Rojo); a.SetBounds(20, 20, 100, 100);
            VistaFalsa v = new VistaFalsa("V", Azul); v.SetBounds(60, 60, 100, 100);
            PantallaRocola.AgregaVista(null, a); PantallaRocola.AgregaVista(null, v);
            Mira("sin la capa: el orden de la lista", PantallaRocola.IndiceDe(a) + " " + PantallaRocola.IndiceDe(v), "0 1", "ControlCollection.Add: al final");
            Mira("sin la capa: composicion", Pinta(80, 80) + " " + Pinta(140, 140) + " " + Pinta(5, 5), "FFFF0000 FF0000FF FF00FF00", "el [0] arriba; el fondo de la ventana debajo de todo");
            // EL RATON
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
            // SetBounds la mueve con su sitio, y quitada deja ver lo de debajo (las dos reglas que antes se miraban solo con la capa)
            v.SetBounds(150, 150, 50, 50);
            Mira("sin la capa: setbounds", Pinta(140, 140) + " " + Pinta(160, 160), "FF00FF00 FF0000FF", "Control.SetBounds: la ventana hija en su sitio nuevo");
            PantallaRocola.QuitaVista(v);
            Mira("sin la capa: quitada", Pinta(160, 160) + " " + Log(), "FF00FF00 ", "Controls.Remove: ya no se pinta (y no avisa de nada a la vista)");
            PantallaRocola.QuitaVista(a);
            // (panel-del-disco) una vista dentro de otra
            string[] panNat = EscenaPanel();
            for (int i = 0; i < esperadoP.Length; i++)
                Mira("sin la capa, panel con hijas: " + esperadoP[i].Substring(0, esperadoP[i].IndexOf(' ')), i < panNat.Length ? panNat[i] : "(nada)", esperadoP[i], FP);
            Mira("sin la capa, panel con hijas: nada queda en la lista", PantallaRocola.ListaDeLaVentana.Length + "", "0", "Controls.Remove");
            PantallaRocola.FondoDeLaVentana = delegate (SKCanvas g) { g.Clear(Verde); };
            Teclas();
            // (vistas-buscador) el tabulador
            PantallaRocola.Teclas = new PantallaRocola.TeclasDeLaVentana { ProcesaMando = delegate (ref PulsoTecla mm, Tecla d) { return false; } };
            try
            {
                string[] tabNat = EscenaTab();
                for (int i = 0; i < esperadoT.Length; i++)
                    Mira("sin la capa, tabulador: " + esperadoT[i].Substring(0, esperadoT[i].IndexOf(' ')), i < tabNat.Length ? tabNat[i] : "(nada)", esperadoT[i], FT);
            }
            finally { PantallaRocola.Teclas = null; }
            // (gpupanel-sin-control) la vista de la tarjeta en la lista nativa, y el tabulador con ella
            TarjetaEnLaLista();
            PantallaRocola.Teclas = new PantallaRocola.TeclasDeLaVentana { ProcesaMando = delegate (ref PulsoTecla mm, Tecla d) { return false; } };
            try
            {
                string[] ttNat = EscenaTabTarjeta();
                for (int i = 0; i < esperadoTT.Length; i++)
                {
                    string nombre = esperadoTT[i].Substring(0, esperadoTT[i].IndexOf(' '));
                    Mira("sin la capa, tabulador con la de la tarjeta: " + nombre, Busca(ttNat, nombre), esperadoTT[i], FTT);
                }
            }
            finally { PantallaRocola.Teclas = null; }
        }
        finally { PantallaRocola.FondoDeLaVentana = null; }
    }

    // (gpupanel-sin-control) LA VISTA DE LA TARJETA EN LA LISTA NATIVA: V (azul) arriba, G (la de la tarjeta) y A (rojo) abajo. G parte los
    // tramos (vista, tarjeta, vista), recorta a A, recibe Present en su superficie, su WM_PAINT (el pintado: PonElPintado),
    // el raton y los que lo oyen, y la rueda con el foco
    static void TarjetaEnLaLista()
    {
        Func<bool> pu = PantallaRocola.PintaUno, hs = PantallaRocola.HaySucio; Func<double> pp = PantallaRocola.ProximoPintado;
        PantallaRocola.PonElPintado();
        VistaFalsa a = new VistaFalsa("A", Rojo), g = new VistaFalsa("G", null), v = new VistaFalsa("V", Azul);
        try
        {
            g.Tarjeta = true;
            a.SetBounds(20, 20, 100, 100); g.SetBounds(40, 40, 120, 90); v.SetBounds(140, 100, 60, 60);
            PantallaRocola.OyeRaton(g, AvisoRaton.Mueve, delegate (Raton e) { log.Add("oye-mueve(" + e.X + "," + e.Y + ")"); });
            PantallaRocola.OyeRaton(g, AvisoRaton.Abajo, delegate (Raton e) { log.Add("oye-abajo(" + e.X + "," + e.Y + "," + e.Button + "," + e.Clicks + ")"); });
            PantallaRocola.OyeRaton(g, AvisoRaton.Clic, delegate (Raton e) { log.Add("oye-clic(" + e.X + "," + e.Y + "," + e.Button + "," + e.Clicks + ")"); });
            PantallaRocola.OyeRaton(g, AvisoRaton.Rueda, delegate (Raton e) { log.Add("oye-rueda(" + e.Delta + ")"); });
            Va(5, 5); Bombea(30); log.Clear();
            PantallaRocola.AgregaVista(null, v); PantallaRocola.AgregaVista(null, g); PantallaRocola.AgregaVista(null, a);
            Bombea(150);
            Mira("sin la capa, la de la tarjeta: alta", Log(), "G.Pinta", "ShowWindow de la ventana nueva da WM_PAINT");
            List<CapaPantalla> l = new List<CapaPantalla>(); PantallaRocola.CapasDeLaVentana(l);
            CapaPantalla t = l.Count == 3 ? l[1] : null;
            Mira("sin la capa, la de la tarjeta: tramos", l.Count + " " + (l.Count == 3 ? l[0].Tipo + "," + l[1].Tipo + "," + l[2].Tipo : "") + (t != null ? " " + t.Nombre + "@" + t.Rect.X + "," + t.Rect.Y + "," + t.Rect.Width + "x" + t.Rect.Height + "#" + t.Textura : ""),
                 "3 vista,tarjeta,vista VistaFalsa@40,40,120x90#0", "la ventana del dispositivo en su sitio del orden z; sin Present aun, sin fondo que poner");
            Mira("sin la capa, la de la tarjeta: recorta a la de debajo", Pinta(50, 50, 0) + " " + Pinta(30, 30, 0) + " " + Pinta(150, 110), "FF00FF00 FFFF0000 FF0000FF", "WS_CLIPSIBLINGS: debajo de la ventana de la tarjeta no se pinta la de debajo");
            uint tex = TexturasGl.Nueva(120, 90); IntPtr sup = PantallaRocola.SuperficieDe(g);
            bool ok = PantallaRocola.PresentaTarjeta(sup, tex, 120, 90, false);
            l.Clear(); PantallaRocola.CapasDeLaVentana(l);
            Mira("sin la capa, la de la tarjeta: Present", ok + " " + (l.Count == 3 && l[1].Textura == tex && l[1].AnchoGl == 120 && l[1].AltoGl == 90), "True True", "Present pone el fondo del dispositivo en su ventana (DeviceWindow) y se compone");
            Bombea(150); log.Clear();
            g.Invalidate(); Bombea(150);
            Mira("sin la capa, la de la tarjeta: pinta", Log(), "G.Pinta", "Invalidate: WM_PAINT");
            g.Visible = false; Bombea(150);
            Mira("sin la capa, la de la tarjeta: esconde", Log(), "", "escondida no se pinta");
            g.Visible = true; Bombea(150);
            Mira("sin la capa, la de la tarjeta: ensena", Log(), "G.Pinta", "ShowWindow: WM_PAINT");
            g.SetBounds(30, 30, 120, 90); Bombea(150);
            Mira("sin la capa, la de la tarjeta: mueve-sin-medida", Log(), "", "sin ResizeRedraw: moverla no la vuelve a pintar");
            Va(5, 5); Bombea(30); log.Clear();
            Va(70, 70); Va(80, 80); Bombea(30);
            Mira("sin la capa, la de la tarjeta: raton-mueve", Log(), "G.Entra,G.Mueve(0),oye-mueve(40,40),G.Mueve(0),oye-mueve(50,50)", FTar);
            Clic(true); Clic(false); Bombea(650);
            Mira("sin la capa, la de la tarjeta: raton-clic", Log(), "G.Abajo(1),oye-abajo(50,50,Left,1),G.Clic(1),oye-clic(50,50,Left,1),G.Arriba(1)", FTar);
            PantallaRocola.EnfocaVista(g); Bombea(30); log.Clear();
            PantallaRocola.RatonRueda(-1); Bombea(30);
            Mira("sin la capa, la de la tarjeta: rueda", Log(), "G.Rueda(0),oye-rueda(-120)", FTar);
            PantallaRocola.FocoDeLaVentana = null;
            // (revision) quitarla y volverla a poner: la ventana vuelve a ensenarse y recibe su WM_PAINT (y vuelve a su sitio del orden)
            PantallaRocola.QuitaVista(g); Bombea(150); log.Clear();
            PantallaRocola.AgregaVista(null, g); PantallaRocola.VistaAlFondo(a); Bombea(150);
            Mira("sin la capa, la de la tarjeta: quitar y volver a poner", Log(), "G.Pinta", "Controls.Remove y Controls.Add otra vez: SetParent y ShowWindow de la ventana, WM_PAINT");
            Mira("sin la capa, la de la tarjeta: minimizada", PantallaRocola.VentanaMinimizada(g) + "", "False", "la ventana de la rocola es la pantalla: no se minimiza");
            PantallaRocola.OlvidaTarjeta(sup);
            l.Clear(); PantallaRocola.CapasDeLaVentana(l);
            Mira("sin la capa, la de la tarjeta: olvida", l.Count == 3 ? l[1].Textura + "" : "(" + l.Count + ")", "0", "Release del dispositivo: la ventana ya no tiene fondo");
            PantallaRocola.SueltaSuperficie(g);
            Mira("sin la capa, la de la tarjeta: soltada", PantallaRocola.PresentaTarjeta(sup, tex, 120, 90, false) + "", "False", "una ventana destruida ya no recibe Present");
            Mira("sin la capa, la de la tarjeta: soltada sale de la ventana", PantallaRocola.IndiceDe(g) + "", "-1", "(revision) Control.Dispose con disposing: «_parent.Controls.Remove(this)» (Control.cs:5348-5350)");
            TexturasGl.Borra(tex);
        }
        finally
        {
            PantallaRocola.QuitaVista(a); PantallaRocola.QuitaVista(g); PantallaRocola.QuitaVista(v);
            PantallaRocola.PintaUno = pu; PantallaRocola.HaySucio = hs; PantallaRocola.ProximoPintado = pp;
            Va(5, 5); Bombea(100); log.Clear();
        }
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

    // LAS TECLAS A LA VENTANA: ProcessCmdKey primero (PreProcessMessage, Control.cs:9275-9300), ProcessDialogKey, KeyDown,
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

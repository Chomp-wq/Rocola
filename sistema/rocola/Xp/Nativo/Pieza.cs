// LAS PIEZAS DE LAS HOJAS (port nativo de las ventanas de XP, ventanas2, paso H): lo que en XP eran los controles de WinForms de sus
// ventanas (Label, Panel, Button, ListBox; en el paso C, las piezas propias de Ctrl+F3). No imitan a Control y no tienen clase base: cada
// pieza (Rotulo, Grupo, BotonXp, ListaXp...) es una clase de la rocola que guarda lo que tienen en comun en su CAJA (CajaPieza: el sitio,
// el padre, lo heredado, si se ve, si esta activa, el tabulador, el pintado con la «X» roja) y expone de ella SOLO lo que XP usa de esa
// pieza (el inventario: Pruebas/Inventario, /root/rocola-trabajo/port/ventanas/inv/salida). La hoja (Hoja.cs) las mueve con las reglas de
// WinForms que XP supone; cada regla cita su linea de la fuente con que se reviso la capa de la transicion (dotnet/winforms release/3.0,
// portada de .NET Framework 4.8: /tmp/claude-0/agente-winforms/ref; Control.cs y Control.ControlCollection.cs). Las reglas se PORTARON de
// esa capa (en la referencia, Plataforma/Ventanas.Control.cs, revisada contra la fuente), sin su API: sin Handle, sin mensajes de Windows, sin Invoke ni
// ISynchronizeInvoke, sin IWin32Window, sin Dock, Anchor, AutoScroll ni maquetado.
//
//   IPieza       lo que la hoja le pide a cada pieza: su caja y lo que la pieza hace al pintarse, con las teclas, con el raton y con el
//                foco (lo que no hace, de fabrica: nada).
//   CajaPieza    lo comun de cada pieza (no es una clase base: cada pieza tiene la suya).
//   IContenedor  lo que tiene piezas dentro (la hoja y los grupos): de el heredan la letra, los colores, si se ven y si estan activas.
//   Piezas       las piezas de un contenedor, la de mas arriba primero (Controls[0]), con el orden del tabulador de Add.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rocola.Nativo;
using SkiaSharp;

namespace Rocola
{
    // EL RESULTADO DE UNA HOJA (DialogResult): los mismos numeros y nombres (lo escriben las trazas), solo los que salen en las hojas de XP:
    // OK (Aceptar de un aviso y del selector de carpetas), Cancel (la «X», Escape, Alt+F4, Close) y None (aun sin resultado).
    public enum Resultado { None = 0, OK = 1, Cancel = 2 }
    // POR QUE SE CIERRA UNA HOJA (CloseReason): los mismos numeros y nombres (lo escriben las trazas). TaskManagerClosing: un WM_CLOSE que
    // llega de fuera a la ventana de la rocola (en Rocola OS, SIGUSR1: PantallaRocola.cs, «el cierre que pide Rocola OS»)
    public enum RazonCierre { None = 0, WindowsShutDown = 1, TaskManagerClosing = 2, UserClosing = 3, FormOwnerClosing = 5 }
    // LA ALINEACION DEL TEXTO DE UN ROTULO (ContentAlignment): los mismos numeros y nombres
    public enum AlineaTexto { TopLeft = 1, TopCenter = 2, TopRight = 4, MiddleLeft = 16, MiddleCenter = 32, MiddleRight = 64, BottomLeft = 256, BottomCenter = 512, BottomRight = 1024 }
    // EL BORDE DE UNA PIEZA (BorderStyle): los mismos numeros y nombres
    public enum BordeXp { None = 0, FixedSingle = 1, Fixed3D = 2 }
    // LOS BOTONES DEL RATON (MouseButtons): los mismos numeros (las trazas los escriben en hexadecimal)
    [Flags] public enum BotonRaton { None = 0, Left = 0x100000, Right = 0x200000, Middle = 0x400000 }

    // UN AVISO DEL RATON A UNA PIEZA (MouseEventArgs; en la rueda, HandledMouseEventArgs): lo que las piezas leen (Button, Clicks, X, Y,
    // Delta) y Handled, con que la rueda no sigue al padre (Control.WmMouseWheel: si nadie la atiende, DefWindowProc la pasa al padre,
    // Control.cs:12748-12764).
    public sealed class Raton
    {
        public Raton(BotonRaton boton, int clics, int x, int y, int delta) { Button = boton; Clicks = clics; X = x; Y = y; Delta = delta; }
        public BotonRaton Button { get; }
        public int Clicks { get; }
        public int X { get; }
        public int Y { get; }
        public int Delta { get; }
        public bool Handled { get; set; }
    }

    // LO QUE LA HOJA LE PIDE A CADA PIEZA (sin clase base: lo de fabrica de cada aviso es no hacer nada, como los On* de Control)
    public interface IPieza
    {
        CajaPieza Caja { get; }
        // OnPaintBackground (Control.cs:8611-8619, PaintBackColor): el fondo con su color, si lo tiene
        void PintaFondo(SKCanvas g) { CajaPieza.PintaFondoDeFabrica(Caja, g); }
        void PintaContenido(SKCanvas g) { }              // OnPaint
        bool QuiereTecla(Tecla k) { return false; }      // IsInputKey
        void TeclaAbajo(PulsoTecla e) { }                // OnKeyDown
        void TeclaArriba(PulsoTecla e) { }               // OnKeyUp
        void Caracter(PulsoCaracter e) { }               // OnKeyPress
        void RatonAbajo(Raton e) { }                     // OnMouseDown
        void RatonArriba(Raton e) { }                    // OnMouseUp
        void RatonMueve(Raton e) { }                     // OnMouseMove
        void RatonEntra() { }                            // OnMouseEnter
        void RatonSale() { }                             // OnMouseLeave
        void Clic(Raton e) { }                           // OnClick y OnMouseClick (StandardClick)
        void DobleClic(Raton e) { }                      // OnDoubleClick y OnMouseDoubleClick (StandardDoubleClick)
        void Rueda(Raton e) { }                          // OnMouseWheel
        void AlRecibirFoco() { }                         // OnGotFocus
        void AlPerderFoco() { }                          // OnLostFocus
    }

    // LO QUE TIENE PIEZAS DENTRO (la hoja y los grupos)
    public interface IContenedor
    {
        Piezas Controls { get; }
        Hoja HojaDe { get; }
        IContenedor PadreDe { get; }
        bool VisibleDe { get; }        // si se ve (con los de arriba)
        bool EnabledDe { get; }        // si esta activo (con los de arriba)
        Color FondoDe { get; }         // BackColor
        Color TintaDe { get; }         // ForeColor
        Letra LetraDe { get; }         // Font
        void Ensucia();                // hay que repintar
    }

    // LAS PIEZAS DE UN CONTENEDOR (Control.ControlCollection): la primera es la de mas arriba (Controls[0]); se pintan de la ultima a la
    // primera y el raton busca de la primera a la ultima.
    public sealed class Piezas : IEnumerable
    {
        private readonly IContenedor dueno;
        internal readonly List<IPieza> Lista = new List<IPieza>();
        internal Piezas(IContenedor d) { dueno = d; }
        public int Count { get { return Lista.Count; } }
        public IPieza this[int i] { get { return Lista[i]; } }
        // Add (Control.ControlCollection.cs:46-140): una que ya es suya va al fondo (SendToBack, :67-71); la de otro sale de alli; sin
        // TabIndex puesto toma el mayor de sus hermanas + 1 (:83-96); y si con el padre nuevo deja de verse o se ve, VisibleChanged
        // (AssignParent; OnParentVisibleChanged, Control.cs:7759-7765)
        public void Add(IPieza p)
        {
            if (p == null) return;
            CajaPieza c = p.Caja;
            if (c.Padre == dueno) { Lista.Remove(p); Lista.Add(p); dueno.Ensucia(); return; }
            if (c.Padre != null) c.Padre.Controls.Remove(p);
            bool vis0 = c.Visible;
            Lista.Add(p);
            if (c.TabIndexPuesto == -1)
            {
                int sig = 0;
                for (int k = 0; k < Lista.Count - 1; k++) { int t = Lista[k].Caja.TabIndex; if (sig <= t) sig = t + 1; }
                c.TabIndexPuesto = sig;
            }
            c.PonPadre(dueno);
            if (vis0 != c.Visible) c.AvisaVisible();
            dueno.Ensucia();
        }
        // Remove (Control.ControlCollection.cs:335-362 y ContainerControl.AfterControlRemoved, ContainerControl.cs:449-515): si el foco
        // estaba en ella (o dentro), pasa a la siguiente del contenedor del foco, o a nadie
        public void Remove(IPieza p)
        {
            if (p == null || p.Caja.Padre != dueno) return;
            Hoja h = dueno.HojaDe;
            bool conFoco = h != null && h.ContieneFoco(p);
            Lista.Remove(p);
            p.Caja.PonPadre(null);
            dueno.Ensucia();
            if (conFoco) h.FocoSeVaCon(p);
        }
        public bool Contains(IPieza p) { return Lista.Contains(p); }
        public int IndexOf(IPieza p) { return Lista.IndexOf(p); }
        internal void AlFrente(IPieza p) { if (Lista.Remove(p)) Lista.Insert(0, p); }
        public IEnumerator GetEnumerator() { return Lista.ToArray().GetEnumerator(); }
    }

    // LA CAJA DE UNA PIEZA: lo que toda pieza tiene, con las reglas de Control de WinForms que XP supone.
    public sealed class CajaPieza
    {
        internal readonly IPieza Pieza;
        private Rectangle bounds;
        private bool visible = true, enabled = true, disposed, disposing;
        private IContenedor padre;
        private Color backColor = Color.Empty, foreColor = Color.Empty;
        private Letra font;
        private string text = "";
        // EL NOMBRE (Control.Name): XP se lo pone a algunas piezas de Ctrl+F3 y no lo lee nunca; lo usan las pruebas para encontrarlas.
        // (AccessibleName, AccessibleRole y AccessibleDescription, los de los lectores de pantalla de Windows, no estan: ver PORT_VENTANAS2.md, paso C.)
        public string Name { get; set; } = "";
        internal int TabIndexPuesto = -1;      // (tabIndex de WinForms: -1 sin poner, se lee 0)
        internal bool ExcepcionPintando;       // (la «X» roja)
        internal Piezas Hijas;                 // (las de un grupo; las demas, ninguna)

        // LO QUE ES CADA PIEZA (los ControlStyles de su constructor en WinForms): Seleccionable (Selectable: toma el foco con el tabulador
        // y las flechas), Transparente (SupportsTransparentBackColor), ClicNormal y DobleClicNormal (StandardClick y StandardDoubleClick: el
        // clic y el doble clic los da la hoja al soltar), Opaca (Opaque: sin fondo), RepintaConTexto (Label y ButtonBase repintan al
        // cambiar el texto: Label.OnTextChanged, ButtonBase.OnTextChanged), RepintaConMedida (ResizeRedraw) y Mano (Cursor = Cursors.Hand)
        internal bool Seleccionable = true, Transparente, ClicNormal = true, DobleClicNormal = true, Opaca, RepintaConTexto, RepintaConMedida, Mano;

        // LA MEDIDA DE FABRICA (DefaultSize: Label 100x23, Button 75x23, Panel 200x100, ListBox 120x96; Control 0x0), sin avisos
        internal CajaPieza(IPieza pieza, int ancho, int alto)
        {
            Pieza = pieza; bounds = new Rectangle(0, 0, ancho, alto);
            if (PantallaRocola.Hilo == null || PantallaRocola.EnSuHilo) PantallaRocola.PonContexto();   // (InstallIfNeeded al crear un control)
        }

        // ---------- sitio y medida ----------
        public Rectangle Bounds { get { return bounds; } }
        // SetBounds (Control.SetBounds y SetBoundsCore: las medidas negativas son 0; si no cambia nada, nada; ResizeRedraw repinta al cambiar
        // de medida; y el padre se repinta)
        internal void SetBounds(int x, int y, int width, int height)
        {
            if (width < 0) width = 0; if (height < 0) height = 0;
            if (bounds.X == x && bounds.Y == y && bounds.Width == width && bounds.Height == height) return;
            bounds = new Rectangle(x, y, width, height);
            EnsuciaPadre();
        }
        internal Rectangle ClientRectangle { get { return new Rectangle(0, 0, bounds.Width, bounds.Height); } }

        // ---------- el padre ----------
        public IContenedor Padre { get { return padre; } }
        public Hoja Hoja { get { return padre != null ? padre.HojaDe : null; } }
        internal void PonPadre(IContenedor p) { padre = p; EnsuciaPadre(); }

        // ---------- lo heredado: el fondo, el color del texto y la letra (Control.BackColor, ForeColor y Font) ----------
        // Sin valor propio, el del padre (el fondo, solo si esta pieza acepta su transparencia: IsValidBackColor, Control.cs:2940-2947; si
        // no, el gris de las ventanas de XP, COLOR_BTNFACE de Luna)
        public static readonly Color ColorDeFondo = Color.FromArgb(236, 233, 216);
        public Color BackColor
        {
            get
            {
                if (!backColor.IsEmpty) return backColor;
                if (padre != null) { Color c = padre.FondoDe; if (FondoValido(c)) return c; }
                return ColorDeFondo;
            }
            set
            {
                // (un fondo con transparencia en una pieza que no la acepta es un error: Control.BackColor, Control.cs:974-1036, el error en 1022)
                if (!value.IsEmpty && !Transparente && value.A < 255) throw new ArgumentException("Control does not support transparent background colors.");
                Color antes = BackColor;
                backColor = value;
                if (!antes.Equals(BackColor)) Invalidate();
            }
        }
        internal bool FondoValido(Color c) { return c.IsEmpty || Transparente || c.A == 255; }
        public Color ForeColor
        {
            get { if (!foreColor.IsEmpty) return foreColor; return padre != null ? padre.TintaDe : Color.Black; }
            set { Color antes = ForeColor; foreColor = value; if (!antes.Equals(ForeColor)) Invalidate(); }
        }
        // LA LETRA DE FABRICA DE WINDOWS XP: Microsoft Sans Serif de 8,25 puntos (Control.DefaultFont)
        private static Letra letraDeFabrica;
        public static Letra LetraDeFabrica { get { return letraDeFabrica ?? (letraDeFabrica = Letra.EnPuntos(Letras.Resuelve("Microsoft Sans Serif"), 8.25f)); } }
        public Letra Font
        {
            get { if (font != null) return font; return padre != null ? padre.LetraDe : LetraDeFabrica; }
            set { font = value; Invalidate(); }
        }
        // (cambiar el texto no repinta una pieza propia; lo repintan las que lo ensenan: RepintaConTexto)
        public string Text
        {
            get { return text; }
            set { if (value == null) value = ""; if (text == value) return; text = value; if (RepintaConTexto) Invalidate(); }
        }

        // ---------- el tabulador (Control.TabIndex: sin poner vale -1 y se lee 0) ----------
        public int TabIndex
        {
            get { return TabIndexPuesto == -1 ? 0 : TabIndexPuesto; }
            set { if (value < 0) throw new ArgumentOutOfRangeException("TabIndex"); TabIndexPuesto = value; }
        }
        public bool TabStop = true;

        // ---------- si se ve y si esta activa ----------
        // Visible: lo suyo y lo de sus padres (Control.Visible, Control.cs:4027-4031). Al cambiar lo que se ve: si se esconde la del foco (o la
        // que lo contiene), el foco pasa ANTES a la siguiente (SelectNextIfFocused, Control.cs:10770-10784; se mira antes de esconderla, asi
        // que la siguiente puede ser su primera hija); y VisibleChanged, que baja a las hijas que se ven (OnVisibleChanged y
        // OnParentVisibleChanged, Control.cs:7875-7918 y 7759-7765). Esconderla con el padre escondido solo cambia lo suyo (SetVisibleCore).
        public bool VisiblePropio { get { return visible; } }
        public bool Visible
        {
            get { return visible && (padre == null || padre.VisibleDe); }
            set
            {
                if (Visible != value)
                {
                    Hoja h = Hoja;
                    if (!value && h != null) h.SiguienteSiTieneElFoco(Pieza);
                    visible = value;
                    AvisaVisible();
                }
                else if (visible == value) return;
                else visible = value;
                EnsuciaPadre();
            }
        }
        internal event EventHandler VisibleChanged;
        internal void AvisaVisible()
        {
            EventHandler e = VisibleChanged; if (e != null) e(Pieza, EventArgs.Empty);
            if (Hijas != null) foreach (IPieza x in Hijas.Lista.ToArray()) if (x.Caja.Visible && x.Caja.visible) x.Caja.AvisaVisible();
        }
        // Enabled (Control.Enabled, Control.cs:2180-2213): avisa si el valor nuevo no es el que se veia; al apagarla, el foco pasa a la
        // siguiente (SelectNextIfFocused); y se repinta (OnEnabledChanged con UserPaint, Control.cs:7478-7513)
        public bool EnabledPropio { get { return enabled; } }
        public bool Enabled
        {
            get { return enabled && (padre == null || padre.EnabledDe); }
            set
            {
                bool antes = Enabled;
                enabled = value;
                if (antes == value) return;
                Hoja h = Hoja;
                if (!value && h != null) h.SiguienteSiTieneElFoco(Pieza);
                Invalidate();
            }
        }
        // CanSelect (CanSelectCore, Control.cs:4920-4936): Selectable y activa y visible con todos sus padres
        internal bool CanSelect
        {
            get
            {
                if (!Seleccionable) return false;
                for (CajaPieza c = this; c != null; c = c.padre is IPieza ? ((IPieza)c.padre).Caja : null)
                {
                    if (!c.enabled || !c.visible) return false;
                    if (!(c.padre is IPieza) && c.padre != null && (!c.padre.EnabledDe || !c.padre.VisibleDe)) return false;
                }
                return true;
            }
        }

        // ---------- el foco (Control.Focus: SetFocus de Windows, Control.cs:5675-5681) ----------
        public bool Focus() { Hoja h = Hoja; return h != null && h.Enfoca(Pieza); }
        public bool Focused { get { Hoja h = Hoja; return h != null && Hoja.Foco == Pieza; } }

        // ---------- el pintado ----------
        // Invalidate: hay que repintar la hoja (la hoja se pinta entera, como la capa de la referencia: el rectangulo solo acota en Windows)
        public void Invalidate() { Hoja h = Hoja; if (h != null) h.Invalidate(); }
        private void EnsuciaPadre() { if (padre != null) padre.Ensucia(); }
        // WM_PAINT (Control.PaintWithErrorHandling, Control.cs:8989-9037): el fondo (si no es opaca) y el contenido. Si uno de los dos
        // lanza, la pieza queda marcada para siempre y en adelante solo pinta la «X» roja de .NET (PaintException, :8869-8889); la
        // excepcion sigue su camino (la hoja la da a PantallaRocola.Falla: Application.ThreadException).
        internal void PintaFondoYContenido(SKCanvas g)
        {
            if (ExcepcionPintando) { PinturaXp.ExcepcionAlPintar(g, ClientRectangle); return; }
            bool lanzo = true;
            try { if (!Opaca) Pieza.PintaFondo(g); lanzo = false; }
            finally { if (lanzo) { ExcepcionPintando = true; Invalidate(); } }
            lanzo = true;
            try { Pieza.PintaContenido(g); lanzo = false; }
            finally { if (lanzo) { ExcepcionPintando = true; Invalidate(); } }
        }
        internal static void PintaFondoDeFabrica(CajaPieza c, SKCanvas g)
        {
            Color f = c.BackColor;
            if (f.A == 0) return;
            PinturaXp.Rellena(g, c.ClientRectangle, f);
        }

        // ---------- delante de sus hermanas (Control.BringToFront con padre: Controls.SetChildIndex(this, 0), Control.cs:4881-4894) ----------
        internal void BringToFront() { if (padre != null) { padre.Controls.AlFrente(Pieza); padre.Ensucia(); } }

        // ---------- desecharla (Control.Dispose, Control.cs:5296-5395): sale de su padre, se desechan sus hijas y al final Disposed ----------
        public bool IsDisposed { get { return disposed; } }
        public bool Disposing { get { return disposing; } }
        internal event EventHandler Disposed;
        internal void Desecha(Action propio)
        {
            if (disposing || disposed) return;
            disposing = true;
            try
            {
                if (padre != null) padre.Controls.Remove(Pieza);
                if (Hijas != null)
                {
                    IPieza[] l = Hijas.Lista.ToArray(); Hijas.Lista.Clear();
                    foreach (IPieza x in l) { x.Caja.padre = null; IDisposable d = x as IDisposable; if (d != null) d.Dispose(); else x.Caja.Desecha(null); }
                }
                if (propio != null) propio();
                EventHandler e = Disposed; if (e != null) e(Pieza, EventArgs.Empty);
            }
            finally { disposing = false; disposed = true; }
        }
    }
}

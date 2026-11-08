// LAS VENTANAS DE LA ROCOLA (traduccion de XP, fase 1; ver Ventanas.Tipos.cs): el control. Una caja con su sitio, sus hijos y su dibujo
// (el codigo de XP en OnPaint/OnPaintBackground, con Dibujo2D). Como en la rocola de XP: Controls[0] es el de mas al frente; cada uno se
// pinta con OnPaintBackground y OnPaint; las teclas, los temporizadores y lo que otros hilos piden con BeginInvoke van por el hilo de la
// pantalla (Escritorio.cs). No hay ventanas del sistema: «Handle» es solo un numero para reconocer cada control.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;

namespace Rocola.Ventanas
{
    public class Control : Component, ISynchronizeInvoke, IWin32Window, IDuenoDeEncargos
    {
        private static int ultimoHandle = 0x1000;
        private static readonly Dictionary<IntPtr, Control> porHandle = new Dictionary<IntPtr, Control>();
        private Rectangle bounds;
        private bool visible = true, enabled = true, creado, disposed;
        private IntPtr handle;
        private Control parent;
        private Color backColor = Color.Empty, foreColor = Color.Empty;
        private Font font;
        private string text = "";
        private ControlStyles estilos = ControlStyles.UserPaint | ControlStyles.StandardClick | ControlStyles.Selectable | ControlStyles.StandardDoubleClick | ControlStyles.AllPaintingInWmPaint | ControlStyles.UseTextForAccessibility;
        internal bool sucio = true;
        // LO PINTA LA TARJETA (OpenGL ES 2): el control de la rocola de XP que en Windows dibujaba Direct3D 9 (GpuPanel). El escritorio no
        // lo pinta con Dibujo2D: deja su hueco y la tarjeta pinta ahi (Escritorio.Capas).
        public bool Nativa;
        // Lo ultimo que presento la tarjeta en este control (Direct3DaGL, Present): la textura de OpenGL del fondo de su dispositivo y su
        // medida. La pinta el anfitrion en el hueco del control.
        internal uint TexturaGl; internal int AnchoGl, AltoGl;
        internal bool TexturaGlRgba;   // la textura va en el orden de OpenGL (el video de libmpv), no en el de Direct3D
        public ControlCollection Controls { get; }

        // COMO EL CONSTRUCTOR DE WINFORMS: la medida de fabrica de cada clase (DefaultSize: Label 100x23, Button 75x23, Panel 200x100,
        // ListBox 120x96, Form 300x300; Control 0x0), sin avisos; y el contexto de la pantalla en el hilo que lo crea
        // (WindowsFormsSynchronizationContext.InstallIfNeeded), asi SynchronizationContext.Current ya vale en el constructor de Form1.
        public Control()
        {
            Controls = CreateControlsInstance();
            Size d = DefaultSize; bounds = new Rectangle(0, 0, d.Width, d.Height);
            if (Escritorio.HiloUi == null || Thread.CurrentThread == Escritorio.HiloUi) ContextoVentana.Pon();
        }
        public Control(string text) : this() { Text = text; }
        protected virtual ControlCollection CreateControlsInstance() { return new ControlCollection(this); }
        protected virtual Size DefaultSize { get { return Size.Empty; } }

        // ---------- sitio y medida ----------
        public Rectangle Bounds { get { return bounds; } set { SetBounds(value.X, value.Y, value.Width, value.Height); } }
        public Point Location { get { return bounds.Location; } set { SetBounds(value.X, value.Y, bounds.Width, bounds.Height); } }
        public Size Size { get { return bounds.Size; } set { SetBounds(bounds.X, bounds.Y, value.Width, value.Height); } }
        public int Left { get { return bounds.X; } set { SetBounds(value, bounds.Y, bounds.Width, bounds.Height); } }
        public int Top { get { return bounds.Y; } set { SetBounds(bounds.X, value, bounds.Width, bounds.Height); } }
        public int Width { get { return bounds.Width; } set { SetBounds(bounds.X, bounds.Y, value, bounds.Height); } }
        public int Height { get { return bounds.Height; } set { SetBounds(bounds.X, bounds.Y, bounds.Width, value); } }
        public int Right { get { return bounds.Right; } }
        public int Bottom { get { return bounds.Bottom; } }
        public virtual Size ClientSize { get { return ClienteDe(bounds.Size); } set { SetClientSizeCore(value.Width, value.Height); } }
        // (Control.SetClientSizeCore de WinForms: la medida con el borde, y el aviso de ClientSizeChanged otra vez)
        protected virtual void SetClientSizeCore(int x, int y) { Size = TamanoPorCliente(new Size(x, y)); OnClientSizeChanged(EventArgs.Empty); }
        public Rectangle ClientRectangle { get { return new Rectangle(Point.Empty, ClientSize); } }
        public virtual Rectangle DisplayRectangle { get { return ClientRectangle; } }
        internal virtual Size ClienteDe(Size s) { return s; }
        internal virtual Size TamanoPorCliente(Size s) { return s; }
        internal virtual Point OrigenCliente { get { return Point.Empty; } }
        public void SetBounds(int x, int y, int width, int height)
        {
            if (width < 0) width = 0; if (height < 0) height = 0;
            if (bounds.X == x && bounds.Y == y && bounds.Width == width && bounds.Height == height) return;
            SetBoundsCore(x, y, width, height, BoundsSpecified.All);
        }
        public void SetBounds(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if ((specified & BoundsSpecified.X) == 0) x = bounds.X; if ((specified & BoundsSpecified.Y) == 0) y = bounds.Y;
            if ((specified & BoundsSpecified.Width) == 0) width = bounds.Width; if ((specified & BoundsSpecified.Height) == 0) height = bounds.Height;
            SetBounds(x, y, width, height);
        }
        protected virtual void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            Rectangle antes = bounds;
            bounds = new Rectangle(x, y, width, height);
            // (como Control.UpdateBounds de WinForms: OnLocationChanged llama a OnMove y OnSizeChanged a OnResize, antes de sus avisos)
            if (antes.Location != bounds.Location) OnLocationChanged(EventArgs.Empty);
            if (antes.Size != bounds.Size)
            {
                OnSizeChanged(EventArgs.Empty); OnClientSizeChanged(EventArgs.Empty);
                if (GetStyle(ControlStyles.ResizeRedraw)) Invalidate();
            }
            // una ventana que solo se mueve (arrastrada por su barra de titulo) no se vuelve a pintar: su capa cambia de sitio
            if (parent == null && antes.Size == bounds.Size && this is Form) Escritorio.Mueve();
            else EnsuciaPadre();
        }
        public Point PointToScreen(Point p) { Point o = OrigenEnPantalla(); return new Point(p.X + o.X, p.Y + o.Y); }
        public Point PointToClient(Point p) { Point o = OrigenEnPantalla(); return new Point(p.X - o.X, p.Y - o.Y); }
        public Rectangle RectangleToScreen(Rectangle r) { return new Rectangle(PointToScreen(r.Location), r.Size); }
        public Rectangle RectangleToClient(Rectangle r) { return new Rectangle(PointToClient(r.Location), r.Size); }
        internal Point OrigenEnPantalla()
        {
            int x = OrigenCliente.X, y = OrigenCliente.Y;
            for (Control c = this; c != null; c = c.parent)
            {
                x += c.bounds.X; y += c.bounds.Y;
                if (c.parent != null) { x += c.parent.OrigenCliente.X; y += c.parent.OrigenCliente.Y; }
            }
            return new Point(x, y);
        }
        public DockStyle Dock { get; set; }
        public AnchorStyles Anchor { get; set; } = AnchorStyles.Top | AnchorStyles.Left;
        public Padding Margin { get; set; }
        public Padding Padding { get; set; }
        public virtual bool AutoSize { get; set; }
        public Size MinimumSize { get; set; }
        public Size MaximumSize { get; set; }

        // ---------- aspecto ----------
        public static readonly Color ColorDeFondo = Color.FromArgb(236, 233, 216);   // el gris de las ventanas de XP (Luna)
        // LAS PROPIEDADES QUE SE HEREDAN (BackColor, ForeColor, Font), como Control de WinForms: sin valor propio, la del padre (el fondo,
        // solo si este control acepta su transparencia: IsValidBackColor; si no, el gris de las ventanas). Al cambiar la que se ve, su
        // aviso (OnXChanged, que repinta) y los hijos sin valor propio reciben OnParentXChanged. La letra se compara por lo que es
        // (Font.Equals de GDI+: familia, tamano, estilo y unidad), no por el objeto.
        public virtual Color BackColor
        {
            get
            {
                if (!backColor.IsEmpty) return backColor;
                if (parent != null) { Color c = parent.BackColor; if (FondoValido(c)) return c; }
                return ColorDeFondo;
            }
            set
            {
                // (WinForms: un fondo con transparencia en un control que no la acepta es un error)
                if (!value.IsEmpty && !GetStyle(ControlStyles.SupportsTransparentBackColor) && value.A < 255)
                    throw new ArgumentException("Control does not support transparent background colors.");
                Color antes = BackColor;
                backColor = value;
                if (!antes.Equals(BackColor)) OnBackColorChanged(EventArgs.Empty);
            }
        }
        internal bool FondoValido(Color c) { return c.IsEmpty || GetStyle(ControlStyles.SupportsTransparentBackColor) || c.A == 255; }
        public virtual Color ForeColor
        {
            get { if (!foreColor.IsEmpty) return foreColor; return parent != null ? parent.ForeColor : Color.Black; }
            set { Color antes = ForeColor; foreColor = value; if (!antes.Equals(ForeColor)) OnForeColorChanged(EventArgs.Empty); }
        }
        private static Font letraDeFabrica;
        // la letra de fabrica de los controles de Windows XP: Microsoft Sans Serif de 8,25 puntos
        public static Font DefaultFont { get { return letraDeFabrica ?? (letraDeFabrica = new Font("Microsoft Sans Serif", 8.25f)); } }
        public virtual Font Font
        {
            get { if (font != null) return font; return parent != null ? parent.Font : DefaultFont; }
            set
            {
                // (Control.Font de WinForms: la propia solo se cambia si es otra letra -una igual deja el objeto de antes- y el aviso sale
                // si la que se ve no es igual a la nueva: resolved.Equals(value))
                Font antes = Font;
                if (value == null ? font != null : (font == null || !IgualLetra(value, font))) font = value;
                if (value == null || !IgualLetra(antes, value)) OnFontChanged(EventArgs.Empty);
            }
        }
        // Font.Equals de GDI+: la misma familia (GDI+ guarda una por nombre, sin mayusculas), tamano, estilo y unidad
        internal static bool IgualLetra(Font a, Font b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return string.Equals(a.FontFamily.Name, b.FontFamily.Name, StringComparison.OrdinalIgnoreCase) && a.Size == b.Size && a.Style == b.Style && a.Unit == b.Unit;
        }
        // (WinForms: cambiar el texto no repinta un control propio; lo repintan los que lo ensenan: Label, ButtonBase, la barra de
        // titulo del formulario)
        public virtual string Text
        {
            get { return text; }
            set { if (value == null) value = ""; if (text == value) return; text = value; OnTextChanged(EventArgs.Empty); }
        }
        public string Name { get; set; } = "";
        public object Tag { get; set; }
        public Image BackgroundImage { get; set; }
        public ImageLayout BackgroundImageLayout { get; set; }
        public Cursor Cursor { get; set; }
        public RightToLeft RightToLeft { get; set; }
        // EL ORDEN DEL TABULADOR, como WinForms: sin poner, vale -1 (se lee 0) y al meterlo en un padre toma el mayor de sus hermanos + 1
        // (ControlCollection.Add); asi el orden es el de entrada, salvo los que XP pone a mano (HojasFondo, FluidezAnimaciones).
        internal int tabIndex = -1;
        public int TabIndex
        {
            get { return tabIndex == -1 ? 0 : tabIndex; }
            set { if (value < 0) throw new ArgumentOutOfRangeException("TabIndex"); if (tabIndex != value) { tabIndex = value; OnTabIndexChanged(EventArgs.Empty); } }
        }
        public event EventHandler TabIndexChanged;
        protected virtual void OnTabIndexChanged(EventArgs e) { TabIndexChanged?.Invoke(this, e); }
        public bool TabStop { get; set; } = true;
        public bool CausesValidation { get; set; } = true;
        public bool Capture { get; set; }
        public string AccessibleName { get; set; }
        public string AccessibleDescription { get; set; }
        public AccessibleRole AccessibleRole { get; set; }
        public Region Region { get; set; }   // la forma del control (la pastilla del aviso con esquinas redondas)
        protected virtual bool DoubleBuffered { get; set; }
        protected bool ResizeRedraw { get { return GetStyle(ControlStyles.ResizeRedraw); } set { SetStyle(ControlStyles.ResizeRedraw, value); } }
        protected void SetStyle(ControlStyles flag, bool value) { estilos = value ? (estilos | flag) : (estilos & ~flag); }
        protected bool GetStyle(ControlStyles flag) { return (estilos & flag) == flag; }
        protected void UpdateStyles() { }

        // ---------- arbol ----------
        public Control Parent
        {
            get { return parent; }
            set { if (parent == value) return; if (parent != null) parent.Controls.Remove(this); if (value != null) value.Controls.Add(this); }
        }
        // AssignParent de WinForms: el padre nuevo, ParentChanged y, si con el cambian, los avisos de lo heredado (habilitado, visible,
        // letra y colores), como si se hubieran cambiado a mano.
        internal void PonPadre(Control p)
        {
            Font f0 = Font; Color fc0 = ForeColor, bc0 = BackColor; bool en0 = Enabled, vis0 = Visible;
            parent = p;
            OnParentChanged(EventArgs.Empty);
            if (!EnDesecho())
            {
                if (en0 != Enabled) OnEnabledChanged(EventArgs.Empty);
                bool vis1 = Visible;
                if (vis0 != vis1 && !(!vis0 && vis1 && parent == null && !(this is Form))) OnVisibleChanged(EventArgs.Empty);
                if (!IgualLetra(f0, Font)) OnFontChanged(EventArgs.Empty);
                if (!fc0.Equals(ForeColor)) OnForeColorChanged(EventArgs.Empty);
                if (!bc0.Equals(BackColor)) OnBackColorChanged(EventArgs.Empty);
            }
            EnsuciaPadre();
        }
        // GetAnyDisposingInHierarchy: el o algun padre se esta desechando (entonces no se avisa de lo heredado)
        internal bool EnDesecho() { for (Control c = this; c != null; c = c.parent) if (c.Disposing) return true; return false; }
        public Form FindForm() { for (Control c = this; c != null; c = c.parent) { Form f = c as Form; if (f != null) return f; } return null; }
        public Control TopLevelControl { get { Control c = this; while (c.parent != null) c = c.parent; return c; } }
        public bool Contains(Control ctl) { for (Control c = ctl; c != null; c = c.parent) if (c.parent == this) return true; return false; }
        public bool HasChildren { get { return Controls.Count > 0; } }
        public void BringToFront() { if (parent != null) { parent.Controls.AlFrente(this); EnsuciaPadre(); } else Escritorio.AlFrente(this as Form); }
        public void SendToBack() { if (parent != null) { parent.Controls.AlFondo(this); EnsuciaPadre(); } }
        public void SuspendLayout() { }
        public void ResumeLayout() { }
        public void ResumeLayout(bool performLayout) { }
        public void PerformLayout() { }

        // ---------- estado ----------
        public bool Visible
        {
            get { return visible && (parent == null || parent.Visible); }
            set { SetVisibleCore(value); }
        }
        internal bool VisiblePropio { get { return visible; } }
        // COMO WINFORMS (Control.SetVisibleCore): se compara con lo que de verdad se ve (con los padres). Al esconder el que tiene el foco
        // (o al que lo contiene), el foco pasa antes al siguiente en el orden del tabulador (SelectNextIfFocused). El aviso VisibleChanged
        // sale si cambia lo que se ve, y tambien al ensenar un control cuyo padre esta escondido; esconderlo con el padre escondido solo
        // cambia lo suyo, sin aviso.
        protected virtual void SetVisibleCore(bool value)
        {
            if (Visible != value)
            {
                if (!value) SelectNextIfFocused();
                visible = value;
                if (value) CreateControl();
                OnVisibleChanged(EventArgs.Empty);
            }
            else if (visible == value) return;
            else visible = value;
            EnsuciaPadre(); Invalidate();
        }
        public void Show() { Visible = true; }
        public void Hide() { Visible = false; }
        // (Control.Enabled de WinForms: avisa si el valor nuevo no es el que se veia, aunque el padre siga en gris)
        public bool Enabled
        {
            get { return enabled && (parent == null || parent.Enabled); }
            set
            {
                bool antes = Enabled;
                enabled = value;
                if (antes == value) return;
                if (!value) SelectNextIfFocused();   // (como WinForms: el foco no se queda en uno en gris)
                OnEnabledChanged(EventArgs.Empty);
            }
        }
        // LA VENTANA DE CADA CONTROL (su handle), como en WinForms: Handle la crea si no esta (y lanza si el control ya se desecho);
        // CreateControl crea la suya y la de los hijos que se ven (un hijo escondido no tiene ventana hasta que se ensena) y avisa
        // OnCreateControl; DestroyHandle la destruye (al desechar, y al acabar ShowDialog): HandleDestroyed del control y despues de sus
        // hijos, e IsHandleCreated vuelve a false. Lo pedido con BeginInvoke que no se atendio se pierde con ella.
        public IntPtr Handle { get { if (handle == IntPtr.Zero) CreateHandle(); return handle; } }
        internal IntPtr HandleActual { get { return handle; } }   // (sin crearla; se puede leer desde otro hilo)
        long IDuenoDeEncargos.Vida { get { return (long)handle; } }   // (port nativo: los encargos de PantallaRocola viajan con la ventana de ahora)
        internal void PonVisibleSinAviso(bool v) { visible = v; }   // (el bit de visible, sin VisibleChanged: el formulario nuevo nace escondido)
        public bool IsHandleCreated { get { return handle != IntPtr.Zero; } }
        public bool Created { get { return creado; } }
        public bool IsDisposed { get { return disposed; } }
        public bool Disposing { get; private set; }
        public void CreateControl() { CreaControl(false); }
        internal void CreaControl(bool ignoraVisible)
        {
            if (!((!creado && Visible) || ignoraVisible)) return;
            creado = true;
            if (handle == IntPtr.Zero) CreateHandle();
            foreach (Control c in Controls.Lista.ToArray()) c.CreaControl(ignoraVisible);
            OnCreateControl();
        }
        protected virtual void OnCreateControl() { }
        protected virtual void CreateHandle()
        {
            if (disposed) throw new ObjectDisposedException(GetType().Name);
            if (handle != IntPtr.Zero) return;
            lock (porHandle) { handle = new IntPtr(Interlocked.Increment(ref ultimoHandle)); porHandle[handle] = this; }
            if (Escritorio.HiloUi == null) Escritorio.HiloUi = Thread.CurrentThread;
            if (Thread.CurrentThread == Escritorio.HiloUi) ContextoVentana.Pon();
            OnHandleCreated(EventArgs.Empty);
        }
        protected virtual void DestroyHandle()
        {
            if (handle == IntPtr.Zero) return;
            AvisaDestruccion();
            QuitaHandles();
        }
        // WM_DESTROY: al de arriba y despues a sus hijos (el foco, si lo tenia, se va: WM_KILLFOCUS)
        private void AvisaDestruccion()
        {
            Control f = Escritorio.Foco;
            if (f != null && (f == this || Contains(f))) Escritorio.QuitaFoco(f);
            try { OnHandleDestroyed(EventArgs.Empty); } catch (Exception ex) { Application.Avisa(ex); }
            if (!Disposing) creado = false;   // (se podra volver a crear)
            foreach (Control c in Controls.Lista.ToArray()) if (c.handle != IntPtr.Zero) c.AvisaDestruccion();
        }
        // WM_NCDESTROY: ya no hay ventana
        private void QuitaHandles()
        {
            foreach (Control c in Controls.Lista.ToArray()) if (c.handle != IntPtr.Zero) c.QuitaHandles();
            lock (porHandle) porHandle.Remove(handle);
            handle = IntPtr.Zero;
        }
        public static Control FromHandle(IntPtr h) { lock (porHandle) { Control c; return porHandle.TryGetValue(h, out c) ? c : null; } }
        // Control.Dispose de WinForms: se destruye la ventana, sale de su padre, se desechan los hijos (sin padre) y al final Disposed;
        // IsDisposed vale desde que acaba (mientras, Disposing). (Aqui sale del padre antes de destruir la ventana: asi el foco pasa al
        // siguiente por el camino de ControlCollection.Remove, que mira el foco y no el control activo del contenedor.)
        protected override void Dispose(bool disposing)
        {
            if (!disposing) { base.Dispose(disposing); return; }
            if (Disposing || disposed) return;
            Disposing = true;
            try
            {
                if (parent != null) parent.Controls.Remove(this);
                if (handle != IntPtr.Zero) DestroyHandle();
                Control[] hijos = Controls.Lista.ToArray();
                Controls.Lista.Clear();
                foreach (Control c in hijos) { c.parent = null; c.Dispose(); }
                base.Dispose(disposing);
            }
            finally { Disposing = false; disposed = true; }
        }

        // ---------- foco (como WinForms: Focus, Select, el orden del tabulador y SelectNextControl) ----------
        // Focus() es el del raton (SetFocus de Windows); Select() el del teclado (el ActiveControl del contenedor). Un contenedor (el
        // formulario) que recibe el foco se lo da a su control activo, si lo tiene y se ve (ContainerControl.WmSetFocus).
        public bool Focus()
        {
            if (!CanFocus) return false;
            ContainerControl cc = this as ContainerControl;
            Control a = cc != null ? cc.activo : null;
            if (a != null && !a.IsDisposed && cc.Contains(a) && a.Visible) Escritorio.PonFoco(a, false);
            else Escritorio.PonFoco(this, false);
            return ContainsFocus;
        }
        public void Select() { Select(false, true); }
        protected virtual void Select(bool directed, bool forward) { if (CanSelect) Escritorio.PonFoco(this, true); }
        public bool CanFocus { get { return Visible && Enabled && !Escritorio.Deshabilitada(this as Form ?? FindForm()); } }   // (debajo de una hoja modal, no)
        public bool CanSelect
        {
            get
            {
                if (!GetStyle(ControlStyles.Selectable)) return false;
                for (Control c = this; c != null; c = c.parent) if (!c.enabled || !c.visible) return false;
                return true;
            }
        }
        public bool Focused { get { return Escritorio.Foco == this; } }
        public bool ContainsFocus { get { Control f = Escritorio.Foco; return f != null && (f == this || Contains(f)); } }
        // El contenedor que lleva el foco de este control (GetContainerControl de WinForms): normalmente, el formulario.
        internal ContainerControl ContenedorDelFoco()
        {
            Control c = this is ContainerControl ? parent : this;
            while (c != null && !(c is ContainerControl)) c = c.parent;
            return (ContainerControl)c;
        }
        // Al esconder o apagar el que tiene el foco (o al que lo contiene), el foco pasa al siguiente, con vuelta (SelectNextIfFocused). Ojo,
        // como en WinForms: se mira antes de esconderlo, asi que hacia delante el siguiente puede ser su primer hijo.
        private void SelectNextIfFocused()
        {
            if (!ContainsFocus || parent == null) return;
            ContainerControl c = parent.ContenedorDelFoco();
            if (c != null) c.SelectNextControl(this, true, true, true, true);
        }
        public bool SelectNextControl(Control ctl, bool forward, bool tabStopOnly, bool nested, bool wrap)
        {
            Control sig = SiguienteElegible(ctl, forward, tabStopOnly, nested, wrap);
            if (sig == null) return false;
            sig.Select(true, forward);
            return true;
        }
        private Control SiguienteElegible(Control ctl, bool forward, bool tabStopOnly, bool nested, bool wrap)
        {
            if (!Contains(ctl) || (!nested && ctl.parent != this)) ctl = null;
            bool yaDioVuelta = false;
            Control inicio = ctl;
            do
            {
                ctl = GetNextControl(ctl, forward);
                if (ctl == null)
                {
                    if (!wrap) break;
                    if (yaDioVuelta) return null;
                    yaDioVuelta = true;
                }
                else if (ctl.CanSelect && (!tabStopOnly || ctl.TabStop) && (nested || ctl.parent == this)) return ctl;
            }
            while (ctl != inicio);
            return null;
        }
        private static bool ManejaFoco(Control c) { return c is ContainerControl && c.GetStyle(ControlStyles.ContainerControl); }
        // EL SIGUIENTE EN EL ORDEN DEL TABULADOR (Control.GetNextControl de WinForms, paso por paso): hacia delante se entra en los hijos y
        // despues se busca el hermano con el TabIndex siguiente (a igualdad, el que va despues en la lista); hacia atras, el hermano anterior
        // y su ultimo hijo. null al pasar del ultimo.
        public Control GetNextControl(Control ctl, bool forward)
        {
            if (!Contains(ctl)) ctl = this;
            if (forward)
            {
                if (ctl.Controls.Count > 0 && (ctl == this || !ManejaFoco(ctl)))
                {
                    Control primero = ctl.PrimeroEnOrden(true);
                    if (primero != null) return primero;
                }
                while (ctl != this)
                {
                    int objetivo = ctl.tabIndex;
                    bool vistoCtl = false;
                    Control hallado = null;
                    List<Control> hs = ctl.parent.Controls.Lista;
                    for (int c = 0; c < hs.Count; c++)
                    {
                        if (hs[c] != ctl)
                        {
                            if (hs[c].tabIndex >= objetivo && (hallado == null || hallado.tabIndex > hs[c].tabIndex) && (hs[c].tabIndex != objetivo || vistoCtl))
                                hallado = hs[c];
                        }
                        else vistoCtl = true;
                    }
                    if (hallado != null) return hallado;
                    ctl = ctl.parent;
                }
            }
            else
            {
                if (ctl != this)
                {
                    int objetivo = ctl.tabIndex;
                    bool vistoCtl = false;
                    Control hallado = null;
                    Control p = ctl.parent;
                    List<Control> hs = p.Controls.Lista;
                    for (int c = hs.Count - 1; c >= 0; c--)
                    {
                        if (hs[c] != ctl)
                        {
                            if (hs[c].tabIndex <= objetivo && (hallado == null || hallado.tabIndex < hs[c].tabIndex) && (hs[c].tabIndex != objetivo || vistoCtl))
                                hallado = hs[c];
                        }
                        else vistoCtl = true;
                    }
                    if (hallado != null) ctl = hallado;
                    else return p == this ? null : p;
                }
                while (ctl.Controls.Count > 0 && (ctl == this || !ManejaFoco(ctl)))
                {
                    Control ultimo = ctl.PrimeroEnOrden(false);
                    if (ultimo == null) break;
                    ctl = ultimo;
                }
            }
            return ctl == this ? null : ctl;
        }
        private Control PrimeroEnOrden(bool forward)
        {
            List<Control> hs = Controls.Lista;
            Control hallado = null;
            if (forward) { for (int c = 0; c < hs.Count; c++) if (hallado == null || hallado.tabIndex > hs[c].tabIndex) hallado = hs[c]; }
            else { for (int c = hs.Count - 1; c >= 0; c--) if (hallado == null || hallado.tabIndex < hs[c].tabIndex) hallado = hs[c]; }
            return hallado;
        }

        // ---------- pintura ----------
        // (WinForms: Invalidate() es Invalidate(false); con true se repintan tambien los hijos. Aqui lo de Dibujo2D de un control se
        // repinta en su capa; los hijos que pinta la tarjeta -el GpuPanel- tienen su propio WM_PAINT, que se pide solo con true)
        public void Invalidate() { Invalidate(false); }
        public void Invalidate(bool invalidateChildren)
        {
            sucio = true; Escritorio.Ensucia(this);
            if (invalidateChildren) foreach (Control c in Controls.Lista.ToArray()) if (c.Nativa || c.Controls.Count > 0) c.Invalidate(true);
        }
        public void Invalidate(Rectangle rc) { Invalidate(false); }   // (la capa se repinta entera: el rectangulo solo acota en Windows)
        public void Invalidate(Rectangle rc, bool invalidateChildren) { Invalidate(invalidateChildren); }
        public void Update() { Escritorio.PintaYa(this); }
        public virtual void Refresh() { Invalidate(true); Update(); }
        internal void EnsuciaPadre() { if (parent != null) parent.Invalidate(); else Escritorio.Ensucia(this); }
        public Graphics CreateGraphics() { return Graphics.FromImage(Escritorio.LienzoSuelto); }
        protected virtual void OnPaintBackground(PaintEventArgs pevent)
        {
            Color c = BackColor;
            if (c.A == 0) return;
            using (SolidBrush b = new SolidBrush(c)) pevent.Graphics.FillRectangle(b, ClientRectangle);
            if (BackgroundImage != null) pevent.Graphics.DrawImage(BackgroundImage, ClientRectangle);
        }
        protected virtual void OnPaint(PaintEventArgs e) { PaintEventHandler h = Paint; if (h != null) h(this, e); }
        protected void InvokePaint(Control c, PaintEventArgs e) { c.OnPaint(e); }
        protected void InvokePaintBackground(Control c, PaintEventArgs e) { c.OnPaintBackground(e); }
        // WM_PAINT (PaintWithErrorHandling de WinForms): el fondo (si no es Opaque) y el contenido. Si uno de los dos lanza, el control
        // queda marcado para siempre y en adelante solo pinta la «X» roja de .NET (PaintException); la excepcion sigue su camino
        // (ThreadException).
        private bool excepcionPintando;
        internal void PintaFondoYContenido(Graphics g, Rectangle recorte)
        {
            sucio = false;
            using (PaintEventArgs e = new PaintEventArgs(g, recorte))
            {
                if (excepcionPintando) { PintaExcepcion(g); return; }
                bool lanzo = true;
                try
                {
                    if (!GetStyle(ControlStyles.Opaque)) OnPaintBackground(e);
                    lanzo = false;
                }
                finally { if (lanzo) { excepcionPintando = true; Invalidate(); } }
                lanzo = true;
                try { OnPaint(e); lanzo = false; }
                finally { if (lanzo) { excepcionPintando = true; Invalidate(); } }
            }
        }
        private void PintaExcepcion(Graphics g)
        {
            Rectangle cr = ClientRectangle, r = cr;
            r.X++; r.Y++; r.Width--; r.Height--;
            using (Pen p = new Pen(Color.Red, 2))
            {
                g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
                r.Inflate(-1, -1);
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, r);
                g.DrawLine(p, cr.Left, cr.Top, cr.Right, cr.Bottom);
                g.DrawLine(p, cr.Left, cr.Bottom, cr.Right, cr.Top);
            }
        }
        public void DrawToBitmap(Bitmap bitmap, Rectangle targetBounds)
        {
            using (Graphics g = Graphics.FromImage(bitmap)) { g.TranslateTransform(targetBounds.X, targetBounds.Y); Escritorio.PintaArbol(this, g, false); }
        }

        // ---------- hilo ----------
        // INVOKE Y BEGININVOKE, como WinForms (MarshaledInvoke): van por la ventana del control o, si aun no tiene, por la del padre mas
        // cercano que si (FindMarshalingControl); sin ninguna, InvalidOperationException. El pedido viaja con esa ventana: si se
        // destruye antes de atenderlo, se pierde (no corre). InvokeRequired: false si no hay ventana en la que mirar.
        private Control Marshal()
        {
            Control c = this;
            while (c != null && c.handle == IntPtr.Zero) c = c.parent;
            return c ?? this;
        }
        private Control MarshalConVentana()
        {
            Control c = Marshal();
            if (c.handle == IntPtr.Zero) throw new InvalidOperationException("Invoke or BeginInvoke cannot be called on a control until the window handle has been created.");
            return c;
        }
        public bool InvokeRequired
        {
            get
            {
                if (Marshal().handle == IntPtr.Zero) return false;
                return Escritorio.HiloUi != null && Thread.CurrentThread != Escritorio.HiloUi;
            }
        }
        // (pruebas: TrazaVentanas apunta cada encargo con el archivo:linea de su BeginInvoke)
        public IAsyncResult BeginInvoke(Delegate method, [System.Runtime.CompilerServices.CallerFilePath] string archivo = null, [System.Runtime.CompilerServices.CallerLineNumber] int linea = 0)
        { Control m = MarshalConVentana(); return Escritorio.Encola(method, null, false, m, TrazaVentanas.Sitio(archivo, linea)); }
        public IAsyncResult BeginInvoke(Delegate method, params object[] args) { Control m = MarshalConVentana(); return Escritorio.Encola(method, args, false, m); }
        public IAsyncResult BeginInvoke(Action method, [System.Runtime.CompilerServices.CallerFilePath] string archivo = null, [System.Runtime.CompilerServices.CallerLineNumber] int linea = 0) { return BeginInvoke((Delegate)method, archivo, linea); }
        public object Invoke(Delegate method) { return Invoke(method, null); }
        public object Invoke(Delegate method, params object[] args)
        {
            Control m = MarshalConVentana();
            if (!InvokeRequired) return Escritorio.Llama(method, args);
            Escritorio.Pendiente p = Escritorio.Encola(method, args, true, m);
            p.AsyncWaitHandle.WaitOne();
            if (p.Error != null) throw p.Error;
            return p.Resultado;
        }
        public void Invoke(Action method) { Invoke((Delegate)method, null); }
        public T Invoke<T>(Func<T> method) { return (T)Invoke((Delegate)method, null); }
        public object EndInvoke(IAsyncResult asyncResult) { Escritorio.Pendiente p = (Escritorio.Pendiente)asyncResult; p.AsyncWaitHandle.WaitOne(); if (p.Error != null) throw p.Error; return p.Resultado; }

        // ---------- teclas ----------
        protected virtual bool ProcessCmdKey(ref Message msg, Keys keyData) { return parent != null && parent.ProcessCmdKey(ref msg, keyData); }
        protected virtual bool ProcessDialogKey(Keys keyData) { return parent != null && parent.ProcessDialogKey(keyData); }
        protected virtual bool IsInputKey(Keys keyData) { return false; }
        protected virtual void WndProc(ref Message m) { }
        internal bool LlamaProcessCmdKey(ref Message m, Keys k) { return ProcessCmdKey(ref m, k); }
        internal bool LlamaProcessDialogKey(Keys k) { return ProcessDialogKey(k); }
        internal bool LlamaIsInputKey(Keys k) { return IsInputKey(k); }
        internal void LlamaKeyDown(KeyEventArgs e) { OnKeyDown(e); }
        internal void LlamaKeyUp(KeyEventArgs e) { OnKeyUp(e); }
        internal void LlamaKeyPress(KeyPressEventArgs e) { OnKeyPress(e); }
        internal void LlamaGotFocus() { OnGotFocus(EventArgs.Empty); }
        internal void LlamaLostFocus() { OnLostFocus(EventArgs.Empty); }
        internal void LlamaEnter() { OnEnter(EventArgs.Empty); }
        internal void LlamaLeave() { OnLeave(EventArgs.Empty); }
        internal void LlamaMouse(int tipo, MouseEventArgs e)
        {
            switch (tipo)
            {
                case 0: OnMouseDown(e); break;
                case 1: OnClick(e); OnMouseClick(e); OnMouseUp(e); break;                 // soltar dentro (como WinForms: Click antes que MouseUp)
                case 2: OnMouseMove(e); break;
                case 3: OnMouseEnter(EventArgs.Empty); break;
                case 4: OnMouseLeave(EventArgs.Empty); break;
                case 5:                                                                   // soltar el segundo clic (los avisos del doble, con 2)
                    MouseEventArgs d = new MouseEventArgs(e.Button, 2, e.X, e.Y, 0);
                    OnDoubleClick(d); OnMouseDoubleClick(d); OnMouseUp(e); break;
                case 6: OnMouseWheel(e); break;
                case 7: OnMouseUp(e); break;                                              // soltar fuera: sin clic
            }
        }
        internal void LlamaPaint(PaintEventArgs e) { OnPaint(e); }
        public static Keys ModifierKeys { get { return Escritorio.Modificadores; } }
        public static MouseButtons MouseButtons { get { return Escritorio.BotonesRaton; } }
        public static Point MousePosition { get { return Cursor.Position; } }

        // ---------- eventos ----------
        public event EventHandler Click, DoubleClick, Resize, SizeChanged, ClientSizeChanged, LocationChanged, Move, VisibleChanged, EnabledChanged,
            GotFocus, LostFocus, Enter, Leave, TextChanged, FontChanged, BackColorChanged, ForeColorChanged, HandleCreated, HandleDestroyed,
            ParentChanged, MouseEnter, MouseLeave, MouseHover;
        public event PaintEventHandler Paint;
        public event KeyEventHandler KeyDown, KeyUp;
        public event KeyPressEventHandler KeyPress;
        public event MouseEventHandler MouseDown, MouseUp, MouseMove, MouseClick, MouseDoubleClick, MouseWheel;
        public event ControlEventHandler ControlAdded, ControlRemoved;
        protected virtual void OnClick(EventArgs e) { Click?.Invoke(this, e); }
        protected virtual void OnDoubleClick(EventArgs e) { DoubleClick?.Invoke(this, e); }
        protected virtual void OnResize(EventArgs e) { Resize?.Invoke(this, e); }
        protected virtual void OnSizeChanged(EventArgs e) { OnResize(EventArgs.Empty); SizeChanged?.Invoke(this, e); }
        protected virtual void OnClientSizeChanged(EventArgs e) { ClientSizeChanged?.Invoke(this, e); }
        protected virtual void OnLocationChanged(EventArgs e) { OnMove(EventArgs.Empty); LocationChanged?.Invoke(this, e); }
        protected virtual void OnMove(EventArgs e) { Move?.Invoke(this, e); }
        // Como WinForms: el aviso baja a los hijos que se ven (al ensenarse el padre) y a los habilitados por su cuenta.
        protected virtual void OnVisibleChanged(EventArgs e)
        {
            VisibleChanged?.Invoke(this, e);
            foreach (Control c in Controls.Lista.ToArray()) if (c.Visible) c.OnParentVisibleChanged(e);
        }
        protected virtual void OnParentVisibleChanged(EventArgs e) { if (visible) OnVisibleChanged(e); }
        protected virtual void OnEnabledChanged(EventArgs e)
        {
            if (EnDesecho()) return;
            if (IsHandleCreated && GetStyle(ControlStyles.UserPaint)) Invalidate();
            EnabledChanged?.Invoke(this, e);
            foreach (Control c in Controls.Lista.ToArray()) c.OnParentEnabledChanged(e);
        }
        protected virtual void OnParentEnabledChanged(EventArgs e) { if (enabled) OnEnabledChanged(e); }
        protected virtual void OnGotFocus(EventArgs e) { GotFocus?.Invoke(this, e); }
        protected virtual void OnLostFocus(EventArgs e) { LostFocus?.Invoke(this, e); }
        protected virtual void OnEnter(EventArgs e) { Enter?.Invoke(this, e); }
        protected virtual void OnLeave(EventArgs e) { Leave?.Invoke(this, e); }
        protected virtual void OnTextChanged(EventArgs e) { TextChanged?.Invoke(this, e); }
        // (WinForms: repintan, avisan y bajan a los hijos que heredan el valor)
        protected virtual void OnFontChanged(EventArgs e)
        {
            if (EnDesecho()) return;
            Invalidate();
            FontChanged?.Invoke(this, e);
            foreach (Control c in Controls.Lista.ToArray()) c.OnParentFontChanged(e);
        }
        protected virtual void OnBackColorChanged(EventArgs e)
        {
            if (EnDesecho()) return;
            Invalidate();
            BackColorChanged?.Invoke(this, e);
            foreach (Control c in Controls.Lista.ToArray()) c.OnParentBackColorChanged(e);
        }
        protected virtual void OnForeColorChanged(EventArgs e)
        {
            if (EnDesecho()) return;
            Invalidate();
            ForeColorChanged?.Invoke(this, e);
            foreach (Control c in Controls.Lista.ToArray()) c.OnParentForeColorChanged(e);
        }
        protected virtual void OnParentFontChanged(EventArgs e) { if (font == null) OnFontChanged(e); }
        protected virtual void OnParentBackColorChanged(EventArgs e) { if (backColor.IsEmpty) OnBackColorChanged(e); }
        protected virtual void OnParentForeColorChanged(EventArgs e) { if (foreColor.IsEmpty) OnForeColorChanged(e); }
        internal bool TieneEstilo(ControlStyles f) { return GetStyle(f); }
        protected virtual void OnHandleCreated(EventArgs e) { HandleCreated?.Invoke(this, e); }
        protected virtual void OnHandleDestroyed(EventArgs e) { HandleDestroyed?.Invoke(this, e); }
        protected virtual void OnParentChanged(EventArgs e) { ParentChanged?.Invoke(this, e); }
        protected virtual void OnKeyDown(KeyEventArgs e) { KeyDown?.Invoke(this, e); }
        protected virtual void OnKeyUp(KeyEventArgs e) { KeyUp?.Invoke(this, e); }
        protected virtual void OnKeyPress(KeyPressEventArgs e) { KeyPress?.Invoke(this, e); }
        protected virtual void OnMouseDown(MouseEventArgs e) { MouseDown?.Invoke(this, e); }
        protected virtual void OnMouseUp(MouseEventArgs e) { MouseUp?.Invoke(this, e); }
        protected virtual void OnMouseMove(MouseEventArgs e) { MouseMove?.Invoke(this, e); }
        protected virtual void OnMouseClick(MouseEventArgs e) { MouseClick?.Invoke(this, e); }
        protected virtual void OnMouseDoubleClick(MouseEventArgs e) { MouseDoubleClick?.Invoke(this, e); }
        protected virtual void OnMouseWheel(MouseEventArgs e) { MouseWheel?.Invoke(this, e); }
        protected virtual void OnMouseEnter(EventArgs e) { MouseEnter?.Invoke(this, e); }
        protected virtual void OnMouseLeave(EventArgs e) { MouseLeave?.Invoke(this, e); }
        protected virtual void OnMouseHover(EventArgs e) { MouseHover?.Invoke(this, e); }
        protected virtual void OnControlAdded(ControlEventArgs e) { ControlAdded?.Invoke(this, e); }
        protected virtual void OnControlRemoved(ControlEventArgs e) { ControlRemoved?.Invoke(this, e); }
        internal void LlamaControlAdded(Control c) { OnControlAdded(new ControlEventArgs(c)); }
        internal void LlamaControlRemoved(Control c) { OnControlRemoved(new ControlEventArgs(c)); }

        public class ControlCollection : IEnumerable
        {
            private readonly Control dueno;
            internal readonly List<Control> Lista = new List<Control>();
            public ControlCollection(Control owner) { dueno = owner; }
            public Control Owner { get { return dueno; } }
            public int Count { get { return Lista.Count; } }
            public Control this[int index] { get { return Lista[index]; } }
            public Control this[string key] { get { foreach (Control c in Lista) if (c.Name == key) return c; return null; } }
            public virtual void Add(Control value)
            {
                if (value == null) return;
                if (value.parent == dueno) { value.SendToBack(); return; }   // (como WinForms: meterlo otra vez lo manda al fondo)
                if (value.parent != null) value.parent.Controls.Remove(value);
                Lista.Add(value);
                if (value.tabIndex == -1)
                {   // sin TabIndex puesto: el mayor de sus hermanos + 1 (WinForms)
                    int sig = 0;
                    for (int c = 0; c < Lista.Count - 1; c++) { int t = Lista[c].TabIndex; if (sig <= t) sig = t + 1; }
                    value.tabIndex = sig;
                }
                value.PonPadre(dueno);
                if (dueno.creado && value.Visible) value.CreateControl();
                dueno.LlamaControlAdded(value);
            }
            public void AddRange(Control[] controls) { foreach (Control c in controls) Add(c); }
            public virtual void Remove(Control value)
            {
                if (value == null || value.parent != dueno) return;
                bool conFoco = value.ContainsFocus;
                ContainerControl cc = conFoco ? dueno.ContenedorDelFoco() ?? dueno as ContainerControl : null;
                Lista.Remove(value);
                value.PonPadre(null);
                dueno.Invalidate();
                dueno.LlamaControlRemoved(value);
                // el foco se iba con el: al primero que se pueda (ContainerControl.AfterControlRemoved de WinForms)
                if (conFoco && Escritorio.Foco != null && (Escritorio.Foco == value || value.Contains(Escritorio.Foco)))
                    if (cc == null || !cc.SelectNextControl(value, true, true, true, true)) Escritorio.QuitaFoco(value);
            }
            public void RemoveAt(int index) { Remove(Lista[index]); }
            public void Clear() { foreach (Control c in Lista.ToArray()) Remove(c); }
            public bool Contains(Control control) { return Lista.Contains(control); }
            public int IndexOf(Control control) { return Lista.IndexOf(control); }
            public int GetChildIndex(Control child) { return Lista.IndexOf(child); }
            public void SetChildIndex(Control child, int newIndex) { if (Lista.Remove(child)) { if (newIndex < 0 || newIndex > Lista.Count) newIndex = Lista.Count; Lista.Insert(newIndex, child); dueno.Invalidate(); } }
            internal void AlFrente(Control c) { if (Lista.Remove(c)) Lista.Insert(0, c); }
            internal void AlFondo(Control c) { if (Lista.Remove(c)) Lista.Add(c); }
            public IEnumerator GetEnumerator() { return Lista.ToArray().GetEnumerator(); }
        }
    }

    public class ScrollableControl : Control
    {
        public ScrollableControl() { SetStyle(ControlStyles.ContainerControl, true); SetStyle(ControlStyles.AllPaintingInWmPaint, false); }
        public bool AutoScroll { get; set; }
    }
    // EL CONTENEDOR QUE LLEVA EL FOCO (el formulario): recuerda su control activo y atiende las teclas de dialogo como WinForms: Tab y
    // Mayus+Tab recorren los controles en el orden del tabulador (entrando en las tarjetas); las flechas pasan al siguiente dentro del
    // mismo padre (con vuelta y sin entrar en los hijos), salvo en los controles que las quieren (IsInputKey: los deslizadores).
    public class ContainerControl : ScrollableControl
    {
        internal Control activo;
        public Control ActiveControl
        {
            get { Control a = activo; return a != null && !a.IsDisposed && Contains(a) ? a : null; }
            set
            {
                if (value == null) { activo = null; return; }
                if (!Contains(value)) throw new ArgumentException("El control no es de este contenedor");
                activo = value;
                if (ContainsFocus || Escritorio.Activa == FindForm()) Escritorio.PonFoco(value, true);
            }
        }
        public SizeF AutoScaleDimensions { get; set; }
        public AutoScaleMode AutoScaleMode { get; set; }
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & (Keys.Alt | Keys.Control)) == Keys.None)
            {
                Keys k = keyData & Keys.KeyCode;
                if (k == Keys.Tab) { if (ProcessTabKey((keyData & Keys.Shift) == Keys.None)) return true; }
                else if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down) { if (ProcessArrowKey(k == Keys.Right || k == Keys.Down)) return true; }
            }
            return base.ProcessDialogKey(keyData);
        }
        protected virtual bool ProcessTabKey(bool forward) { return SelectNextControl(ActiveControl, forward, true, true, false); }
        internal virtual bool ProcessArrowKey(bool forward)
        {
            Control a = ActiveControl;
            Control grupo = a != null ? a.Parent : this;
            return grupo.SelectNextControl(a, forward, false, false, true);
        }
    }
    public class UserControl : ContainerControl { protected override Size DefaultSize { get { return new Size(150, 150); } } }

    public class Panel : ScrollableControl
    {
        // como el de WinForms: no toma el foco ni entra en el tabulador (sus hijos si)
        public Panel() { TabStop = false; SetStyle(ControlStyles.Selectable, false); SetStyle(ControlStyles.SupportsTransparentBackColor, true); }
        protected override Size DefaultSize { get { return new Size(200, 100); } }
        private BorderStyle borde;
        public BorderStyle BorderStyle { get { return borde; } set { borde = value; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (borde != BorderStyle.None) using (Pen p = new Pen(Color.FromArgb(172, 168, 153))) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // LA ETIQUETA: el texto con la letra, el color y la alineacion; como la de XP, la pinta GDI (TextRenderer: el margen de GDI a los lados
    // y el corte por palabras).
    public class Label : Control
    {
        private ContentAlignment alin = ContentAlignment.TopLeft;
        private bool autoSize;
        public Label() { SetStyle(ControlStyles.Selectable, false); SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true); TabStop = false; }
        protected override Size DefaultSize { get { return new Size(100, 23); } }
        public ContentAlignment TextAlign { get { return alin; } set { alin = value; Invalidate(); } }
        public bool AutoEllipsis { get; set; }
        public bool UseMnemonic { get; set; } = true;
        public bool UseCompatibleTextRendering { get; set; }
        public BorderStyle BorderStyle { get; set; }
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;
        public Image Image { get; set; }
        public override bool AutoSize { get { return autoSize; } set { autoSize = value; if (value) Ajusta(); } }
        public virtual int PreferredWidth { get { return PreferredSize.Width; } }
        public virtual int PreferredHeight { get { return PreferredSize.Height; } }
        public Size PreferredSize { get { return TextRenderer.MeasureText(Texto, Font); } }
        public Size GetPreferredSize(Size proposed) { return PreferredSize; }
        private string Texto { get { return UseMnemonic ? Text.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&") : Text; } }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (autoSize) Ajusta(); Invalidate(); }   // (Label.OnTextChanged)
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (autoSize) Ajusta(); }
        private void Ajusta() { Size = PreferredSize; }
        internal static TextFormatFlags DeAlineacion(ContentAlignment a)
        {
            TextFormatFlags f = TextFormatFlags.Default;
            if ((a & (ContentAlignment.TopCenter | ContentAlignment.MiddleCenter | ContentAlignment.BottomCenter)) != 0) f |= TextFormatFlags.HorizontalCenter;
            if ((a & (ContentAlignment.TopRight | ContentAlignment.MiddleRight | ContentAlignment.BottomRight)) != 0) f |= TextFormatFlags.Right;
            if ((a & (ContentAlignment.MiddleLeft | ContentAlignment.MiddleCenter | ContentAlignment.MiddleRight)) != 0) f |= TextFormatFlags.VerticalCenter;
            if ((a & (ContentAlignment.BottomLeft | ContentAlignment.BottomCenter | ContentAlignment.BottomRight)) != 0) f |= TextFormatFlags.Bottom;
            return f;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image != null) e.Graphics.DrawImage(Image, 0, 0);
            // ControlPaint.CreateTextFormatFlags de WinForms: WordBreak y TextBoxControl (DT_EDITCONTROL: el ultimo renglon que no cabe
            // entero no se pinta); en gris, TextRenderer.DisabledTextColor (ControlDark, o el fondo oscurecido si es mas oscuro)
            TextFormatFlags f = DeAlineacion(alin) | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
            if (AutoEllipsis) f |= TextFormatFlags.EndEllipsis;
            if (!UseMnemonic) f |= TextFormatFlags.NoPrefix;
            Color c = Enabled ? ForeColor : ColoresXp.ColorEnGris(BackColor);
            TextRenderer.DrawText(e.Graphics, Texto, Font, ClientRectangle, c, f);
            if (BorderStyle != BorderStyle.None) using (Pen p = new Pen(Color.FromArgb(172, 168, 153))) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            base.OnPaint(e);
        }
    }

    public class ButtonBase : Control
    {
        // (como el de WinForms: acepta el fondo transparente, repinta al cambiar de medida y al cambiar el texto)
        public ButtonBase() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;
        public bool UseVisualStyleBackColor { get; set; }
        public ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;
        public Image Image { get; set; }
        public FlatButtonAppearance FlatAppearance { get; } = new FlatButtonAppearance();
    }
    public class FlatButtonAppearance { public Color BorderColor { get; set; } public int BorderSize { get; set; } = 1; public Color MouseOverBackColor { get; set; } public Color MouseDownBackColor { get; set; } public Color CheckedBackColor { get; set; } }
    // EL BOTON DE WINFORMS (el de Ctrl+F2 y los avisos): se aprieta con el raton (y toma el foco), con Espacio (al soltar) y con Intro si es
    // el boton por defecto (el que tiene el foco o el AcceptButton: ProcessDialogKey del formulario). Se pinta como en XP: plano
    // (FlatStyle.Flat: ButtonFlatAdapter, con los colores que calcula ControlPaint) o con el tema Luna (el resto).
    public class Button : ButtonBase, IButtonControl
    {
        private bool porDefecto, encima, apretado, espacio;
        // (como el de WinForms: el clic no es el de Control -StandardClick- sino el suyo, en OnMouseUp; y un doble clic son dos clics)
        public Button() { SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false); }
        protected override Size DefaultSize { get { return new Size(75, 23); } }
        public DialogResult DialogResult { get; set; }
        public void NotifyDefault(bool value) { if (porDefecto != value) { porDefecto = value; Invalidate(); } }
        protected bool IsDefault { get { return porDefecto; } }
        public void PerformClick() { if (CanSelect) OnClick(EventArgs.Empty); }
        protected override void OnClick(EventArgs e)
        {
            Form f = FindForm();
            if (f != null) f.DialogResult = DialogResult;   // (como Button.OnClick de WinForms, tambien con None)
            base.OnClick(e);
        }
        protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { encima = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { Focus(); apretado = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool dentro = ClientRectangle.Contains(e.X, e.Y);
            if (apretado && encima != dentro) { encima = dentro; Invalidate(); }
            base.OnMouseMove(e);
        }
        // Button.OnMouseUp de WinForms: el clic es del boton izquierdo, apretado aqui y soltado encima del mismo boton (WindowFromPoint)
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool estaba = apretado;
            if (apretado) { apretado = false; Invalidate(); }
            if (e.Button == MouseButtons.Left && estaba && Escritorio.ControlEn(Escritorio.PosicionRaton) == this) { OnClick(e); OnMouseClick(e); }
            base.OnMouseUp(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyData == Keys.Space) { if (!espacio) { espacio = true; Invalidate(); } e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (espacio)
            {
                espacio = false; Invalidate();
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }
        protected override void OnLostFocus(EventArgs e) { espacio = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        private bool Abajo { get { return espacio || (apretado && encima); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            string t = Text.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&");
            TextFormatFlags fl = Label.DeAlineacion(TextAlign) | TextFormatFlags.WordBreak;
            Rectangle r = ClientRectangle;
            if (FlatStyle == FlatStyle.Flat)
            {
                // ButtonFlatAdapter (PaintUp, PaintOver y PaintDown) con los colores de ButtonBaseAdapter.ColorOptions.Calculate
                Color fondo = BackColor, marco = FlatAppearance.BorderColor.IsEmpty ? ForeColor : FlatAppearance.BorderColor;
                if (Enabled && Abajo)
                {
                    Color luz = ColoresXp.LightLight(BackColor);
                    fondo = !FlatAppearance.MouseDownBackColor.IsEmpty ? FlatAppearance.MouseDownBackColor : ColoresXp.Ajusta(luz, luz.GetBrightness() < .5f ? 1.2f : 0.9f);
                }
                else if (Enabled && encima) fondo = !FlatAppearance.MouseOverBackColor.IsEmpty ? FlatAppearance.MouseOverBackColor : ColoresXp.Ajusta(BackColor, BackColor.GetBrightness() < .5f ? 1.2f : 0.9f);
                using (SolidBrush b = new SolidBrush(fondo)) g.FillRectangle(b, r);
                Rectangle rt = r; rt.Inflate(-2, -2); if (Enabled && Abajo) rt.Offset(1, 1);
                TextRenderer.DrawText(g, t, Font, rt, Enabled ? ForeColor : ColoresXp.Dark(BackColor), fl);
                Rectangle rm = r;
                if (porDefecto && FlatAppearance.BorderSize > 0) { using (Pen p = new Pen(marco)) g.DrawRectangle(p, rm.X, rm.Y, rm.Width - 1, rm.Height - 1); rm.Inflate(-1, -1); }
                for (int k = 0; k < FlatAppearance.BorderSize; k++, rm.Inflate(-1, -1)) using (Pen p = new Pen(marco)) g.DrawRectangle(p, rm.X, rm.Y, rm.Width - 1, rm.Height - 1);
            }
            else
            {
                // el boton de Windows XP con el tema Luna: marco azul oscuro redondeado, cara blanca que se oscurece hacia abajo, anaranjado
                // con el raton encima, azul si es el boton por defecto, y gris si no se puede usar
                SmoothingMode sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF rf = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                using (GraphicsPath gp = ColoresXp.Redondo(rf, 3f))
                {
                    Color c1 = !Enabled ? Color.FromArgb(245, 244, 234) : Abajo ? Color.FromArgb(229, 228, 221) : Color.FromArgb(255, 255, 255);
                    Color c2 = !Enabled ? Color.FromArgb(245, 244, 234) : Abajo ? Color.FromArgb(242, 241, 238) : Color.FromArgb(236, 235, 230);
                    using (LinearGradientBrush b = new LinearGradientBrush(r, c1, c2, LinearGradientMode.Vertical)) g.FillPath(b, gp);
                    if (Enabled && (encima || porDefecto) && !Abajo)
                    {
                        Color e1 = encima ? Color.FromArgb(255, 240, 207) : Color.FromArgb(206, 231, 255), e2 = encima ? Color.FromArgb(229, 151, 0) : Color.FromArgb(105, 130, 238);
                        using (Pen p1 = new Pen(e1)) using (Pen p2 = new Pen(e2, 2f))
                        { g.DrawRectangle(p2, 2f, 2f, Width - 4f, Height - 4f); g.DrawLine(p1, 2f, 1.5f, Width - 2f, 1.5f); }
                    }
                    using (Pen p = new Pen(Enabled ? Color.FromArgb(0, 60, 116) : Color.FromArgb(201, 199, 186))) g.DrawPath(p, gp);
                }
                g.SmoothingMode = sm;
                Rectangle rt = r; rt.Inflate(-3, -3);
                TextRenderer.DrawText(g, t, Font, rt, Enabled ? ForeColor : Color.FromArgb(161, 161, 146), fl);
            }
            base.OnPaint(e);
        }
    }

    // LOS COLORES QUE CALCULA WINFORMS PARA PINTAR SUS CONTROLES (ControlPaint de .NET, con su HLS de 0 a 240): los del boton plano.
    internal static class ColoresXp
    {
        internal static Color Ajusta(Color c, float f) { return Color.FromArgb(Math.Min(255, (int)(f * c.R)), Math.Min(255, (int)(f * c.G)), Math.Min(255, (int)(f * c.B))); }
        internal static Color Dark(Color c) { Hls h = new Hls(c); int cero = h.Luma(-333); return h.Color(cero - (int)(cero * 0.5f)); }
        // TextRenderer.DisabledTextColor: ControlDark de XP, o el fondo oscurecido si el fondo es mas oscuro que el gris de las ventanas
        internal static Color ColorEnGris(Color fondo)
        {
            if (fondo.A > 0 && new Hls(fondo).L < new Hls(Control.ColorDeFondo).L) return Dark(fondo);
            return Color.FromArgb(172, 168, 153);
        }
        internal static Color LightLight(Color c) { Hls h = new Hls(c); int uno = h.Luma(500); return h.Color(h.L + (int)((uno - h.L) * 1.0f)); }
        internal static GraphicsPath Redondo(RectangleF r, float radio)
        {
            GraphicsPath gp = new GraphicsPath(); float d = radio * 2;
            gp.AddArc(r.X, r.Y, d, d, 180, 90); gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure(); return gp;
        }
        private struct Hls
        {
            private const int Max = 240, RgbMax = 255, Indefinido = Max * 2 / 3;
            internal int H, L, S;
            internal Hls(Color c)
            {
                int r = c.R, g = c.G, b = c.B, max = Math.Max(Math.Max(r, g), b), min = Math.Min(Math.Min(r, g), b), suma = max + min, dif = max - min;
                L = (suma * Max + RgbMax) / (2 * RgbMax);
                if (dif == 0) { S = 0; H = Indefinido; return; }
                S = L <= Max / 2 ? (dif * Max + suma / 2) / suma : (dif * Max + (2 * RgbMax - suma) / 2) / (2 * RgbMax - suma);
                int dr = ((max - r) * (Max / 6) + dif / 2) / dif, dg = ((max - g) * (Max / 6) + dif / 2) / dif, db = ((max - b) * (Max / 6) + dif / 2) / dif;
                H = r == max ? db - dg : g == max ? Max / 3 + dr - db : 2 * Max / 3 + dg - dr;
                if (H < 0) H += Max;
                if (H > Max) H -= Max;
            }
            internal int Luma(int n) { return n > 0 ? (int)((L * (1000L - n) + (Max + 1L) * n) / 1000) : (int)((L * (n + 1000L)) / 1000); }
            internal Color Color(int lum)
            {
                if (S == 0) { int v = lum * RgbMax / Max; return System.Drawing.Color.FromArgb(v, v, v); }
                int m2 = lum <= Max / 2 ? (lum * (Max + S) + Max / 2) / Max : lum + S - (lum * S + Max / 2) / Max, m1 = 2 * lum - m2;
                return System.Drawing.Color.FromArgb(Byte(m1, m2, H + Max / 3), Byte(m1, m2, H), Byte(m1, m2, H - Max / 3));
            }
            private static int Byte(int n1, int n2, int h) { return (byte)((Tono(n1, n2, h) * RgbMax + Max / 2) / Max); }
            private static int Tono(int n1, int n2, int h)
            {
                if (h < 0) h += Max;
                if (h > Max) h -= Max;
                if (h < Max / 6) return n1 + ((n2 - n1) * h + Max / 12) / (Max / 6);
                if (h < Max / 2) return n2;
                if (h < Max * 2 / 3) return n1 + ((n2 - n1) * (Max * 2 / 3 - h) + Max / 12) / (Max / 6);
                return n1;
            }
        }
    }

    // LA LISTA DE WINDOWS (la de las carpetas de Ctrl+F2): renglones del alto de la letra, el elegido en el azul de seleccion de XP; se
    // elige con el raton o con las flechas, Inicio, Fin y AvPag/RePag (quiere esas teclas: no mueven el foco), y lleva barras de
    // desplazamiento cuando hace falta (la horizontal solo con HorizontalScrollbar, para las rutas largas). El borde: Fixed3D, el del tema
    // (azul grisaceo); FixedSingle, una linea negra (WS_BORDER).
    public class ListBox : Control
    {
        private int sel = -1, top, desplazaX, altoRenglon;
        internal const int Barra = 17;   // SM_CXVSCROLL / SM_CYHSCROLL de XP
        public ListBox() { Items = new ObjectCollection(this); BackColor = Color.White; }
        protected override Size DefaultSize { get { return new Size(120, 96); } }
        public ObjectCollection Items { get; }
        public int ItemHeight { get { return altoRenglon > 0 ? altoRenglon : Math.Max(1, Font.Height); } set { altoRenglon = value; Invalidate(); } }
        public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;
        public bool IntegralHeight { get; set; } = true;
        public bool HorizontalScrollbar { get; set; }
        public SelectionMode SelectionMode { get; set; } = SelectionMode.One;
        public int SelectedIndex
        {
            get { return sel; }
            set { if (value < -1 || value >= Items.Count) value = -1; if (sel == value) return; sel = value; AVista(); OnSelectedIndexChanged(EventArgs.Empty); Invalidate(); }
        }
        public object SelectedItem { get { return sel >= 0 && sel < Items.Count ? Items[sel] : null; } set { SelectedIndex = Items.IndexOf(value); } }
        public int TopIndex { get { return top; } set { top = Math.Max(0, Math.Min(value, Math.Max(0, Items.Count - Visibles))); Invalidate(); } }
        public event EventHandler SelectedIndexChanged;
        protected virtual void OnSelectedIndexChanged(EventArgs e) { SelectedIndexChanged?.Invoke(this, e); }
        public void BeginUpdate() { }
        public void EndUpdate() { Invalidate(); }
        private int Marco { get { return BorderStyle == BorderStyle.None ? 0 : 1; } }
        private int AnchoMayor()
        {
            int w = 0;
            foreach (object o in Items) w = Math.Max(w, TextRenderer.MeasureText(Convert.ToString(o), Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 3);
            return w;
        }
        // lo que queda para los renglones y si van las barras (la vertical si no caben; la horizontal si un renglon es mas ancho)
        private void Zonas(out Rectangle util, out bool vert, out bool horiz)
        {
            int m = Marco;
            Rectangle r = new Rectangle(m, m, Math.Max(0, Width - 2 * m), Math.Max(0, Height - 2 * m));
            vert = false; horiz = false;
            for (int vuelta = 0; vuelta < 2; vuelta++)
            {
                int w = r.Width - (vert ? Barra : 0), h = r.Height - (horiz ? Barra : 0);
                vert = Items.Count * ItemHeight > h;
                horiz = HorizontalScrollbar && AnchoMayor() > w;
            }
            util = new Rectangle(r.X, r.Y, Math.Max(0, r.Width - (vert ? Barra : 0)), Math.Max(0, r.Height - (horiz ? Barra : 0)));
        }
        private int Visibles { get { Rectangle u; bool v, h; Zonas(out u, out v, out h); return Math.Max(1, u.Height / Math.Max(1, ItemHeight)); } }
        private void AVista() { if (sel < 0) return; int n = Visibles; if (sel < top) top = sel; else if (sel >= top + n) top = sel - n + 1; }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || k == Keys.PageUp || k == Keys.PageDown || k == Keys.Home || k == Keys.End) return true;
            return base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || Items.Count == 0) return;
            int n = Visibles, i;
            switch (e.KeyCode)
            {
                case Keys.Down: i = Math.Min(Items.Count - 1, sel + 1); break;
                case Keys.Up: i = Math.Max(0, sel - 1); break;
                case Keys.PageDown: i = Math.Min(Items.Count - 1, Math.Max(0, sel) + n - 1); break;
                case Keys.PageUp: i = Math.Max(0, sel - n + 1); break;
                case Keys.Home: i = 0; break;
                case Keys.End: i = Items.Count - 1; break;
                case Keys.Left: desplazaX = Math.Max(0, desplazaX - 8); Invalidate(); e.Handled = true; return;
                case Keys.Right: desplazaX += 8; Invalidate(); e.Handled = true; return;
                default: return;
            }
            SelectedIndex = i; e.Handled = true;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            Rectangle u; bool v, h; Zonas(out u, out v, out h);
            if (v && e.X >= u.Right && e.Y < u.Bottom)
            {   // la barra vertical: las flechas de una en una, el canal de pagina en pagina, y el pulgar se arrastra (SB_THUMBTRACK)
                int n = Visibles; Rectangle pv = PulgarV(u);
                if (e.Y < u.Top + Barra) TopIndex = top - 1; else if (e.Y >= u.Bottom - Barra) TopIndex = top + 1;
                else if (pv.Contains(e.X, e.Y)) agarreV = e.Y - pv.Top;
                else TopIndex = e.Y < pv.Top ? top - n : top + n;
            }
            else if (h && e.Y >= u.Bottom && e.X < u.Right)
            {
                int max = Math.Max(0, AnchoMayor() - u.Width); Rectangle ph = PulgarH(u);
                if (e.X < u.Left + Barra) desplazaX -= 8; else if (e.X >= u.Right - Barra) desplazaX += 8;
                else if (ph.Contains(e.X, e.Y)) agarreH = e.X - ph.Left;
                else desplazaX += e.X < ph.Left ? -u.Width : u.Width;
                desplazaX = Math.Max(0, Math.Min(max, desplazaX)); Invalidate();
            }
            else if (u.Contains(e.X, e.Y))
            {
                int i = top + (e.Y - u.Top) / Math.Max(1, ItemHeight);
                if (i < Items.Count) SelectedIndex = i;
            }
            base.OnMouseDown(e);
        }
        // ARRASTRAR EL PULGAR: la lista sigue al pulgar mientras se mueve (la posicion por la proporcion del canal, como la barra de
        // Windows), y al soltar queda donde se dejo
        private int agarreV = -1, agarreH = -1;
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (agarreV >= 0 || agarreH >= 0)
            {
                Rectangle u; bool v, h; Zonas(out u, out v, out h);
                if (agarreV >= 0 && v)
                {
                    Rectangle pv = PulgarV(u); int canal = u.Height - 2 * Barra - pv.Height, sobra = Math.Max(0, Items.Count - Visibles);
                    if (canal > 0) TopIndex = (int)Math.Round((double)(e.Y - agarreV - (u.Top + Barra)) * sobra / canal);
                }
                if (agarreH >= 0 && h)
                {
                    Rectangle ph = PulgarH(u); int canal = u.Width - 2 * Barra - ph.Width, sobra = Math.Max(0, AnchoMayor() - u.Width);
                    if (canal > 0) { desplazaX = Math.Max(0, Math.Min(sobra, (int)Math.Round((double)(e.X - agarreH - (u.Left + Barra)) * sobra / canal))); Invalidate(); }
                }
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { agarreV = agarreH = -1; base.OnMouseUp(e); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            TopIndex = top - e.Delta / 120 * 3;
            HandledMouseEventArgs h = e as HandledMouseEventArgs; if (h != null) h.Handled = true;   // (la lista de Windows se la queda: no sube)
            base.OnMouseWheel(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        private Rectangle PulgarV(Rectangle u)
        {
            int canal = u.Height - 2 * Barra, n = Visibles, total = Math.Max(n, Items.Count);
            int largo = Math.Max(8, canal * n / Math.Max(1, total)), y = u.Top + Barra + (total > n ? (canal - largo) * top / (total - n) : 0);
            return new Rectangle(u.Right, y, Barra, largo);
        }
        private Rectangle PulgarH(Rectangle u)
        {
            int canal = u.Width - 2 * Barra, total = Math.Max(u.Width, AnchoMayor());
            int largo = Math.Max(8, canal * u.Width / Math.Max(1, total)), x = u.Left + Barra + (total > u.Width ? (canal - largo) * desplazaX / (total - u.Width) : 0);
            return new Rectangle(x, u.Bottom, largo, Barra);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle u; bool v, h; Zonas(out u, out v, out h);
            int alto = Math.Max(1, ItemHeight), mayor = AnchoMayor();
            desplazaX = h ? Math.Max(0, Math.Min(desplazaX, mayor - u.Width)) : 0;
            top = Math.Max(0, Math.Min(top, Math.Max(0, Items.Count - Math.Max(1, u.Height / alto))));
            GraphicsState st = g.Save();
            g.IntersectClip(u);
            for (int i = top, y = u.Top; i < Items.Count && y < u.Bottom; i++, y += alto)
            {
                bool elegido = i == sel;
                if (elegido) using (SolidBrush b = new SolidBrush(Color.FromArgb(49, 106, 197))) g.FillRectangle(b, new Rectangle(u.Left, y, u.Width, alto));
                Rectangle rt = new Rectangle(u.Left + 1 - desplazaX, y, Math.Max(u.Width, mayor) + 8, alto);
                TextRenderer.DrawText(g, Convert.ToString(Items[i]), Font, rt, elegido ? Color.White : ForeColor, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
            g.Restore(st);
            if (v) BarraXp.Pinta(g, new Rectangle(u.Right, u.Top, Barra, u.Height), true, PulgarV(u));
            if (h) BarraXp.Pinta(g, new Rectangle(u.Left, u.Bottom, u.Width, Barra), false, PulgarH(u));
            if (v && h) using (SolidBrush b = new SolidBrush(Color.FromArgb(236, 233, 216))) g.FillRectangle(b, u.Right, u.Bottom, Barra, Barra);
            if (BorderStyle != BorderStyle.None)
                using (Pen p = new Pen(BorderStyle == BorderStyle.FixedSingle ? Color.Black : Color.FromArgb(127, 157, 185))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            base.OnPaint(e);
        }
        public class ObjectCollection : IList
        {
            private readonly ListBox dueno; private readonly List<object> l = new List<object>();
            public ObjectCollection(ListBox owner) { dueno = owner; }
            public int Count { get { return l.Count; } }
            public object this[int index] { get { return l[index]; } set { l[index] = value; dueno.Invalidate(); } }
            public int Add(object item) { l.Add(item); dueno.Invalidate(); return l.Count - 1; }
            public void AddRange(object[] items) { l.AddRange(items); dueno.Invalidate(); }
            public void Clear() { l.Clear(); dueno.sel = -1; dueno.Invalidate(); }
            public bool Contains(object value) { return l.Contains(value); }
            public int IndexOf(object value) { return l.IndexOf(value); }
            public void Insert(int index, object value) { l.Insert(index, value); dueno.Invalidate(); }
            public void Remove(object value) { l.Remove(value); dueno.Invalidate(); }
            public void RemoveAt(int index) { l.RemoveAt(index); dueno.Invalidate(); }
            public bool IsFixedSize { get { return false; } }
            public bool IsReadOnly { get { return false; } }
            public bool IsSynchronized { get { return false; } }
            public object SyncRoot { get { return l; } }
            public void CopyTo(Array array, int index) { ((ICollection)l).CopyTo(array, index); }
            public IEnumerator GetEnumerator() { return l.GetEnumerator(); }
        }
    }

    // LA BARRA DE DESPLAZAMIENTO DE XP (tema Luna): el canal claro, las flechas y el pulgar azul claro redondeados, con su flecha y sus rayas.
    internal static class BarraXp
    {
        internal static void Pinta(Graphics g, Rectangle r, bool vertical, Rectangle pulgar)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(243, 241, 236))) g.FillRectangle(b, r);
            int n = ListBox.Barra;
            Rectangle a = vertical ? new Rectangle(r.X, r.Y, r.Width, n) : new Rectangle(r.X, r.Y, n, r.Height);
            Rectangle z = vertical ? new Rectangle(r.X, r.Bottom - n, r.Width, n) : new Rectangle(r.Right - n, r.Y, n, r.Height);
            SmoothingMode sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            Pieza(g, a, vertical); Pieza(g, z, vertical); Pieza(g, pulgar, vertical);
            using (SolidBrush f = new SolidBrush(Color.FromArgb(77, 97, 133)))
            {
                float cx = a.X + a.Width / 2f, cy = a.Y + a.Height / 2f, dx = z.X + z.Width / 2f, dy = z.Y + z.Height / 2f;
                if (vertical)
                {
                    g.FillPolygon(f, new PointF[] { new PointF(cx - 4, cy + 2), new PointF(cx + 4, cy + 2), new PointF(cx, cy - 2) });
                    g.FillPolygon(f, new PointF[] { new PointF(dx - 4, dy - 2), new PointF(dx + 4, dy - 2), new PointF(dx, dy + 2) });
                }
                else
                {
                    g.FillPolygon(f, new PointF[] { new PointF(cx + 2, cy - 4), new PointF(cx + 2, cy + 4), new PointF(cx - 2, cy) });
                    g.FillPolygon(f, new PointF[] { new PointF(dx - 2, dy - 4), new PointF(dx - 2, dy + 4), new PointF(dx + 2, dy) });
                }
            }
            g.SmoothingMode = sm;
            using (Pen p = new Pen(Color.FromArgb(140, 166, 222)))
            {
                float cx = pulgar.X + pulgar.Width / 2f, cy = pulgar.Y + pulgar.Height / 2f;
                for (int k = -3; k <= 3; k += 2)
                    if (vertical) { if (pulgar.Height > 14) g.DrawLine(p, cx - 3, cy + k, cx + 3, cy + k); }
                    else if (pulgar.Width > 14) g.DrawLine(p, cx + k, cy - 3, cx + k, cy + 3);
            }
        }
        private static void Pieza(Graphics g, Rectangle r, bool vertical)
        {
            if (r.Width <= 2 || r.Height <= 2) return;
            RectangleF rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
            using (GraphicsPath gp = ColoresXp.Redondo(rf, 2f))
            using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(225, 234, 254), Color.FromArgb(193, 211, 251), vertical ? LinearGradientMode.Horizontal : LinearGradientMode.Vertical))
            using (Pen p = new Pen(Color.FromArgb(180, 200, 246)))
            { g.FillPath(b, gp); g.DrawPath(p, gp); }
        }
    }
}

// EL CONTROL: una caja con su sitio, sus hijos y su pintura por GDI+. Como en WinForms: Controls[0] es el de mas al frente, cada uno se
// pinta con OnPaintBackground y OnPaint, y los eventos y el foco van por el hilo de la ventana (Escritorio.cs).
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;

namespace System.Windows.Forms
{
    public class Control : Component, ISynchronizeInvoke, IWin32Window
    {
        private static int ultimoHandle = 0x1000;
        private static readonly Dictionary<IntPtr, Control> porHandle = new Dictionary<IntPtr, Control>();
        private Rectangle bounds;
        private bool visible = true, enabled = true, creado, disposed, estilosUserPaint;
        private IntPtr handle;
        private Control parent;
        private Color backColor = Color.Empty, foreColor = Color.Empty;
        private Font font;
        private string text = "";
        private ControlStyles estilos = ControlStyles.UserPaint | ControlStyles.StandardClick | ControlStyles.Selectable | ControlStyles.StandardDoubleClick | ControlStyles.AllPaintingInWmPaint | ControlStyles.UseTextForAccessibility;
        internal bool sucio = true;
        internal bool nativa;   // lo pinta la tarjeta (Direct3D de mentira): el escritorio no lo pinta con GDI+
        public ControlCollection Controls { get; }

        public Control() { Controls = CreateControlsInstance(); }
        public Control(string text) : this() { Text = text; }
        protected virtual ControlCollection CreateControlsInstance() { return new ControlCollection(this); }

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
        public virtual Size ClientSize { get { return ClienteDe(bounds.Size); } set { Size = TamanoPorCliente(value); } }
        public Rectangle ClientRectangle { get { return new Rectangle(Point.Empty, ClientSize); } }
        public virtual Rectangle DisplayRectangle { get { return ClientRectangle; } }
        internal virtual Size ClienteDe(Size s) { return s; }
        internal virtual Size TamanoPorCliente(Size s) { return s; }
        internal virtual Point OrigenCliente { get { return Point.Empty; } }
        public void SetBounds(int x, int y, int width, int height)
        {
            if (width < 0) width = 0; if (height < 0) height = 0;
            Rectangle antes = bounds;
            if (antes.X == x && antes.Y == y && antes.Width == width && antes.Height == height) return;
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
            if (antes.Location != bounds.Location) { OnLocationChanged(EventArgs.Empty); OnMove(EventArgs.Empty); }
            if (antes.Size != bounds.Size)
            {
                OnSizeChanged(EventArgs.Empty); OnResize(EventArgs.Empty); OnClientSizeChanged(EventArgs.Empty);
                if (GetStyle(ControlStyles.ResizeRedraw)) Invalidate();
            }
            EnsuciaPadre();
        }
        public void Scale(SizeF factor) { SetBounds((int)Math.Round(bounds.X * factor.Width), (int)Math.Round(bounds.Y * factor.Height), (int)Math.Round(bounds.Width * factor.Width), (int)Math.Round(bounds.Height * factor.Height)); foreach (Control c in Controls.Lista) c.Scale(factor); }
        public void Scale(float ratio) { Scale(new SizeF(ratio, ratio)); }
        public Point PointToScreen(Point p) { Point o = OrigenEnPantalla(); return new Point(p.X + o.X, p.Y + o.Y); }
        public Point PointToClient(Point p) { Point o = OrigenEnPantalla(); return new Point(p.X - o.X, p.Y - o.Y); }
        public Rectangle RectangleToScreen(Rectangle r) { return new Rectangle(PointToScreen(r.Location), r.Size); }
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
        public virtual Color BackColor
        {
            get { if (!backColor.IsEmpty) return backColor; return parent != null ? parent.BackColor : SystemColors.Control; }
            set { if (backColor == value) return; backColor = value; OnBackColorChanged(EventArgs.Empty); Invalidate(); }
        }
        public virtual Color ForeColor
        {
            get { if (!foreColor.IsEmpty) return foreColor; return parent != null ? parent.ForeColor : SystemColors.ControlText; }
            set { if (foreColor == value) return; foreColor = value; OnForeColorChanged(EventArgs.Empty); Invalidate(); }
        }
        private static Font letraDeFabrica;
        public static Font DefaultFont { get { return letraDeFabrica ?? (letraDeFabrica = new Font("Microsoft Sans Serif", 8.25f)); } }
        public virtual Font Font
        {
            get { if (font != null) return font; return parent != null ? parent.Font : DefaultFont; }
            set { if (ReferenceEquals(font, value)) return; font = value; OnFontChanged(EventArgs.Empty); Invalidate(); }
        }
        public virtual string Text
        {
            get { return text; }
            set { if (value == null) value = ""; if (text == value) return; text = value; OnTextChanged(EventArgs.Empty); Invalidate(); }
        }
        public string Name { get; set; } = "";
        public object Tag { get; set; }
        public Image BackgroundImage { get; set; }
        public ImageLayout BackgroundImageLayout { get; set; }
        public Cursor Cursor { get; set; }
        public RightToLeft RightToLeft { get; set; }
        public Region Region { get; set; }
        public int TabIndex { get; set; }
        public bool TabStop { get; set; } = true;
        public bool CausesValidation { get; set; } = true;
        public bool Capture { get; set; }
        public string AccessibleName { get; set; }
        public string AccessibleDescription { get; set; }
        public AccessibleRole AccessibleRole { get; set; }
        protected virtual bool DoubleBuffered { get; set; }
        protected bool ResizeRedraw { get { return GetStyle(ControlStyles.ResizeRedraw); } set { SetStyle(ControlStyles.ResizeRedraw, value); } }
        protected void SetStyle(ControlStyles flag, bool value) { estilos = value ? (estilos | flag) : (estilos & ~flag); }
        protected bool GetStyle(ControlStyles flag) { return (estilos & flag) == flag; }
        protected void UpdateStyles() { }
        internal bool EstiloOpaco { get { return GetStyle(ControlStyles.Opaque); } }

        // ---------- arbol ----------
        public Control Parent
        {
            get { return parent; }
            set { if (parent == value) return; if (parent != null) parent.Controls.Remove(this); if (value != null) value.Controls.Add(this); }
        }
        internal void PonPadre(Control p) { parent = p; OnParentChanged(EventArgs.Empty); EnsuciaPadre(); }
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
        protected virtual void SetVisibleCore(bool value)
        {
            if (visible == value) return;
            visible = value;
            if (value) CreateControl();
            OnVisibleChanged(EventArgs.Empty);
            EnsuciaPadre(); Invalidate();
            if (!value && Escritorio.Foco != null && (Escritorio.Foco == this || Contains(Escritorio.Foco))) Escritorio.QuitaFoco(this);
        }
        public void Show() { Visible = true; }
        public void Hide() { Visible = false; }
        public bool Enabled
        {
            get { return enabled && (parent == null || parent.Enabled); }
            set { if (enabled == value) return; enabled = value; OnEnabledChanged(EventArgs.Empty); Invalidate(); }
        }
        public IntPtr Handle { get { CreateHandle(); return handle; } }
        public bool IsHandleCreated { get { return handle != IntPtr.Zero; } }
        public bool Created { get { return creado; } }
        public bool IsDisposed { get { return disposed; } }
        public bool Disposing { get; private set; }
        public bool RecreatingHandle { get { return false; } }
        public void CreateControl()
        {
            if (creado || disposed) return;
            creado = true; CreateHandle();
            foreach (Control c in Controls.Lista) if (c.visible) c.CreateControl();
        }
        protected virtual void CreateHandle()
        {
            if (handle != IntPtr.Zero || disposed) return;
            lock (porHandle) { handle = new IntPtr(Interlocked.Increment(ref ultimoHandle)); porHandle[handle] = this; }
            if (Escritorio.HiloUi == null) Escritorio.HiloUi = Thread.CurrentThread;
            if (Thread.CurrentThread == Escritorio.HiloUi) ContextoVentana.Pon();
            OnHandleCreated(EventArgs.Empty);
        }
        public static Control FromHandle(IntPtr h) { lock (porHandle) { Control c; return porHandle.TryGetValue(h, out c) ? c : null; } }
        public static Control FromChildHandle(IntPtr h) { return FromHandle(h); }
        protected override void Dispose(bool disposing)
        {
            if (disposed) return;
            Disposing = true;
            if (disposing)
            {
                Control[] hijos = Controls.Lista.ToArray();
                foreach (Control c in hijos) c.Dispose();
                if (parent != null) parent.Controls.Remove(this);
                if (Escritorio.Foco == this) Escritorio.QuitaFoco(this);
            }
            if (handle != IntPtr.Zero) { OnHandleDestroyed(EventArgs.Empty); lock (porHandle) porHandle.Remove(handle); }
            disposed = true;
            Disposing = false;
            base.Dispose(disposing);
        }

        // ---------- foco ----------
        public bool Focus() { if (!CanFocus) return false; Escritorio.PonFoco(this); return true; }
        public void Select() { Focus(); }
        public bool CanFocus { get { return Visible && Enabled; } }
        public bool CanSelect { get { return CanFocus && GetStyle(ControlStyles.Selectable); } }
        public bool Focused { get { return Escritorio.Foco == this; } }
        public bool ContainsFocus { get { Control f = Escritorio.Foco; return f != null && (f == this || Contains(f)); } }

        // ---------- pintura ----------
        public void Invalidate() { Invalidate(true); }
        public void Invalidate(bool invalidateChildren) { sucio = true; Escritorio.Ensucia(this); }
        public void Invalidate(Rectangle rc) { Invalidate(true); }
        public void Invalidate(Rectangle rc, bool invalidateChildren) { Invalidate(true); }
        public void Invalidate(Region region) { Invalidate(true); }
        public void Update() { Escritorio.PintaYa(this); }
        public virtual void Refresh() { Invalidate(); Update(); }
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
        internal void PintaFondoYContenido(Graphics g, Rectangle recorte)
        {
            using (PaintEventArgs e = new PaintEventArgs(g, recorte))
            {
                if (!GetStyle(ControlStyles.Opaque)) OnPaintBackground(e);
                OnPaint(e);
            }
            sucio = false;
        }
        public void DrawToBitmap(Bitmap bitmap, Rectangle targetBounds)
        {
            using (Graphics g = Graphics.FromImage(bitmap)) { g.TranslateTransform(targetBounds.X, targetBounds.Y); Escritorio.PintaArbol(this, g, false); }
        }

        // ---------- hilo ----------
        public bool InvokeRequired { get { return Escritorio.HiloUi != null && Thread.CurrentThread != Escritorio.HiloUi; } }
        public IAsyncResult BeginInvoke(Delegate method) { return BeginInvoke(method, null); }
        public IAsyncResult BeginInvoke(Delegate method, params object[] args) { return Escritorio.Encola(method, args, false); }
        public IAsyncResult BeginInvoke(Action method) { return Escritorio.Encola(method, null, false); }
        public object Invoke(Delegate method) { return Invoke(method, null); }
        public object Invoke(Delegate method, params object[] args)
        {
            if (!InvokeRequired) return Escritorio.Llama(method, args);
            Escritorio.Pendiente p = Escritorio.Encola(method, args, true);
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
        protected virtual bool ProcessKeyPreview(ref Message m) { return parent != null && parent.ProcessKeyPreview(ref m); }
        protected virtual void WndProc(ref Message m) { DefWndProc(ref m); }
        protected virtual void DefWndProc(ref Message m) { }
        internal bool LlamaProcessCmdKey(ref Message m, Keys k) { return ProcessCmdKey(ref m, k); }
        internal bool LlamaProcessDialogKey(Keys k) { return ProcessDialogKey(k); }
        internal bool LlamaIsInputKey(Keys k) { return IsInputKey(k); }
        internal void LlamaKeyDown(KeyEventArgs e) { OnKeyDown(e); }
        internal void LlamaKeyUp(KeyEventArgs e) { OnKeyUp(e); }
        internal void LlamaKeyPress(KeyPressEventArgs e) { OnKeyPress(e); }
        internal void LlamaGotFocus() { OnGotFocus(EventArgs.Empty); OnEnter(EventArgs.Empty); }
        internal void LlamaLostFocus() { OnLeave(EventArgs.Empty); OnLostFocus(EventArgs.Empty); }
        internal void LlamaMouse(int tipo, MouseEventArgs e)
        {
            switch (tipo)
            {
                case 0: OnMouseDown(e); break;
                case 1: OnMouseUp(e); OnMouseClick(e); OnClick(EventArgs.Empty); break;
                case 2: OnMouseMove(e); break;
                case 3: OnMouseEnter(EventArgs.Empty); break;
                case 4: OnMouseLeave(EventArgs.Empty); break;
                case 5: OnMouseDoubleClick(e); OnDoubleClick(EventArgs.Empty); break;
                case 6: OnMouseWheel(e); break;
            }
        }
        internal void LlamaPaint(PaintEventArgs e) { OnPaint(e); }
        public static Keys ModifierKeys { get { return Escritorio.Modificadores; } }
        public static MouseButtons MouseButtons { get { return MouseButtons.None; } }
        public static Point MousePosition { get { return Cursor.Position; } }

        // ---------- eventos ----------
        public event EventHandler Click, DoubleClick, Resize, SizeChanged, ClientSizeChanged, LocationChanged, Move, VisibleChanged, EnabledChanged,
            GotFocus, LostFocus, Enter, Leave, TextChanged, FontChanged, BackColorChanged, ForeColorChanged, HandleCreated, HandleDestroyed,
            ParentChanged, MouseEnter, MouseLeave, MouseHover, Validated;
        public event PaintEventHandler Paint;
        public event KeyEventHandler KeyDown, KeyUp;
        public event KeyPressEventHandler KeyPress;
        public event MouseEventHandler MouseDown, MouseUp, MouseMove, MouseClick, MouseDoubleClick, MouseWheel;
        public event ControlEventHandler ControlAdded, ControlRemoved;
        protected virtual void OnClick(EventArgs e) { Click?.Invoke(this, e); }
        protected virtual void OnDoubleClick(EventArgs e) { DoubleClick?.Invoke(this, e); }
        protected virtual void OnResize(EventArgs e) { Resize?.Invoke(this, e); }
        protected virtual void OnSizeChanged(EventArgs e) { SizeChanged?.Invoke(this, e); }
        protected virtual void OnClientSizeChanged(EventArgs e) { ClientSizeChanged?.Invoke(this, e); }
        protected virtual void OnLocationChanged(EventArgs e) { LocationChanged?.Invoke(this, e); }
        protected virtual void OnMove(EventArgs e) { Move?.Invoke(this, e); }
        protected virtual void OnVisibleChanged(EventArgs e) { VisibleChanged?.Invoke(this, e); }
        protected virtual void OnEnabledChanged(EventArgs e) { EnabledChanged?.Invoke(this, e); }
        protected virtual void OnGotFocus(EventArgs e) { GotFocus?.Invoke(this, e); }
        protected virtual void OnLostFocus(EventArgs e) { LostFocus?.Invoke(this, e); }
        protected virtual void OnEnter(EventArgs e) { Enter?.Invoke(this, e); }
        protected virtual void OnLeave(EventArgs e) { Leave?.Invoke(this, e); }
        protected virtual void OnTextChanged(EventArgs e) { TextChanged?.Invoke(this, e); }
        protected virtual void OnFontChanged(EventArgs e) { FontChanged?.Invoke(this, e); }
        protected virtual void OnBackColorChanged(EventArgs e) { BackColorChanged?.Invoke(this, e); }
        protected virtual void OnForeColorChanged(EventArgs e) { ForeColorChanged?.Invoke(this, e); }
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
                if (value.parent == dueno) return;
                if (value.parent != null) value.parent.Controls.Remove(value);
                Lista.Add(value);
                value.PonPadre(dueno);
                if (dueno.creado && value.visible) value.CreateControl();
                dueno.LlamaControlAdded(value);
            }
            public void AddRange(Control[] controls) { foreach (Control c in controls) Add(c); }
            public virtual void Remove(Control value)
            {
                if (value == null || !Lista.Remove(value)) return;
                value.PonPadre(null);
                dueno.Invalidate();
                dueno.LlamaControlRemoved(value);
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
            public void CopyTo(Array dest, int index) { ((ICollection)Lista).CopyTo(dest, index); }
        }
    }

    public enum AccessibleRole { Default = -1, None = 0, TitleBar = 1, MenuBar = 2, ScrollBar = 3, Grip = 4, Sound = 5, Cursor = 6, Caret = 7, Alert = 8, Window = 9, Client = 10, MenuPopup = 11, MenuItem = 12, ToolTip = 13, Application = 14, Document = 15, Pane = 16, Chart = 17, Dialog = 18, Border = 19, Grouping = 20, Separator = 21, ToolBar = 22, StatusBar = 23, Table = 24, ColumnHeader = 25, RowHeader = 26, Column = 27, Row = 28, Cell = 29, Link = 30, HelpBalloon = 31, Character = 32, List = 33, ListItem = 34, Outline = 35, OutlineItem = 36, PageTab = 37, PropertyPage = 38, Indicator = 39, Graphic = 40, StaticText = 41, Text = 42, PushButton = 43, CheckButton = 44, RadioButton = 45, ComboBox = 46, DropList = 47, ProgressBar = 48, Dial = 49, HotkeyField = 50, Slider = 51, SpinButton = 52, Diagram = 53, Animation = 54, Equation = 55, ButtonDropDown = 56, ButtonMenu = 57, ButtonDropDownGrid = 58, WhiteSpace = 59, PageTabList = 60, Clock = 61, SplitButton = 62, IpAddress = 63, OutlineButton = 64 }
    [Flags] public enum BoundsSpecified { None = 0, X = 1, Y = 2, Location = 3, Width = 4, Height = 8, Size = 12, All = 15 }
    public struct Padding
    {
        public int Left, Top, Right, Bottom;
        public Padding(int all) { Left = Top = Right = Bottom = all; }
        public Padding(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
        public int All { get { return Left; } set { Left = Top = Right = Bottom = value; } }
        public int Horizontal { get { return Left + Right; } }
        public int Vertical { get { return Top + Bottom; } }
        public static readonly Padding Empty = new Padding(0);
    }
}

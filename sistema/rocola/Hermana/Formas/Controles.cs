// Los controles de siempre que usa la rocola de XP: etiqueta, panel, boton, lista, el temporizador y el hueco del video (AxHost).
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms
{
    public class Panel : ScrollableControl
    {
        private BorderStyle borde;
        public BorderStyle BorderStyle { get { return borde; } set { borde = value; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (borde != BorderStyle.None) e.Graphics.DrawRectangle(SystemPens.ControlDark, 0, 0, Width - 1, Height - 1);
        }
    }
    public class UserControl : ContainerControl { }

    public class Label : Control
    {
        private ContentAlignment alin = ContentAlignment.TopLeft;
        private bool autoSize;
        public Label() { SetStyle(ControlStyles.Selectable, false); TabStop = false; }
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
        public Size PreferredSize { get { Size s = TextRenderer.MeasureText(Texto, Font); return new Size(s.Width, s.Height); } }
        public Size GetPreferredSize(Size proposed) { return PreferredSize; }
        private string Texto { get { return UseMnemonic ? Text.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&") : Text; } }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (autoSize) Ajusta(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (autoSize) Ajusta(); }
        private void Ajusta() { Size = PreferredSize; }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image != null) e.Graphics.DrawImage(Image, 0, 0);
            TextFormatFlags f = TextRenderer.DeAlineacion(alin) | TextFormatFlags.WordBreak;
            if (AutoEllipsis) f |= TextFormatFlags.EndEllipsis;
            if (!UseMnemonic) f |= TextFormatFlags.NoPrefix;
            Color c = Enabled ? ForeColor : SystemColors.GrayText;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, c, f);
            if (BorderStyle != BorderStyle.None) e.Graphics.DrawRectangle(SystemPens.ControlDark, 0, 0, Width - 1, Height - 1);
            base.OnPaint(e);
        }
    }

    public class ButtonBase : Control
    {
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;
        public bool UseVisualStyleBackColor { get; set; }
        public ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;
        public Image Image { get; set; }
        public FlatButtonAppearance FlatAppearance { get; } = new FlatButtonAppearance();
    }
    public class FlatButtonAppearance { public Color BorderColor { get; set; } public int BorderSize { get; set; } = 1; public Color MouseOverBackColor { get; set; } public Color MouseDownBackColor { get; set; } }
    public class Button : ButtonBase, IButtonControl
    {
        public DialogResult DialogResult { get; set; }
        public void NotifyDefault(bool value) { }
        public void PerformClick() { OnClick(EventArgs.Empty); }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (DialogResult != DialogResult.None) { Form f = FindForm(); if (f != null) f.DialogResult = DialogResult; }
        }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (!e.Handled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)) PerformClick(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle r = ClientRectangle; r.Width--; r.Height--;
            e.Graphics.DrawRectangle(Focused ? SystemPens.Highlight : SystemPens.ControlDark, r);
            TextRenderer.DrawText(e.Graphics, Text.Replace("&", ""), Font, ClientRectangle, Enabled ? ForeColor : SystemColors.GrayText, TextRenderer.DeAlineacion(TextAlign));
            base.OnPaint(e);
        }
    }

    public class ListBox : Control
    {
        private int sel = -1;
        public ListBox() { Items = new ObjectCollection(this); BackColor = SystemColors.Window; }
        public ObjectCollection Items { get; }
        public int ItemHeight { get; set; } = 16;
        public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;
        public bool IntegralHeight { get; set; } = true;
        public bool HorizontalScrollbar { get; set; }
        public int SelectedIndex
        {
            get { return sel; }
            set { if (value < -1 || value >= Items.Count) value = -1; if (sel == value) return; sel = value; OnSelectedIndexChanged(EventArgs.Empty); Invalidate(); }
        }
        public object SelectedItem { get { return sel >= 0 && sel < Items.Count ? Items[sel] : null; } set { SelectedIndex = Items.IndexOf(value); } }
        public int TopIndex { get; set; }
        public event EventHandler SelectedIndexChanged;
        protected virtual void OnSelectedIndexChanged(EventArgs e) { SelectedIndexChanged?.Invoke(this, e); }
        public void BeginUpdate() { }
        public void EndUpdate() { Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            if (e.KeyCode == Keys.Down && sel < Items.Count - 1) { SelectedIndex = sel + 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Up && sel > 0) { SelectedIndex = sel - 1; e.Handled = true; }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            int alto = Math.Max(1, ItemHeight);
            int visibles = Math.Max(1, Height / alto);
            if (sel >= 0) { if (sel < TopIndex) TopIndex = sel; if (sel >= TopIndex + visibles) TopIndex = sel - visibles + 1; }
            for (int i = TopIndex, y = 0; i < Items.Count && y < Height; i++, y += alto)
            {
                Rectangle r = new Rectangle(0, y, Width, alto);
                bool elegido = i == sel;
                if (elegido) e.Graphics.FillRectangle(SystemBrushes.Highlight, r);
                TextRenderer.DrawText(e.Graphics, Convert.ToString(Items[i]), Font, r, elegido ? SystemColors.HighlightText : ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            if (BorderStyle != BorderStyle.None) e.Graphics.DrawRectangle(SystemPens.ControlDark, 0, 0, Width - 1, Height - 1);
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

    // EL TEMPORIZADOR DE LA VENTANA: avisa en el hilo de la ventana, como el de Windows (Escritorio lo despierta a su hora).
    public class Timer : Component
    {
        private int intervalo = 100;
        private bool activo;
        internal double Proximo;
        public Timer() { }
        public Timer(IContainer container) : this() { if (container != null) container.Add(this); }
        public int Interval { get { return intervalo; } set { if (value < 1) throw new ArgumentOutOfRangeException("Interval"); intervalo = value; if (activo) Proximo = Escritorio.AhoraMs + intervalo; } }
        public bool Enabled
        {
            get { return activo; }
            set { if (activo == value) return; activo = value; if (value) { Proximo = Escritorio.AhoraMs + intervalo; Escritorio.AgregaTimer(this); } else Escritorio.QuitaTimer(this); }
        }
        public object Tag { get; set; }
        public void Start() { Enabled = true; }
        public void Stop() { Enabled = false; }
        public event EventHandler Tick;
        protected virtual void OnTick(EventArgs e) { Tick?.Invoke(this, e); }
        internal void Dispara() { OnTick(EventArgs.Empty); }
        protected override void Dispose(bool disposing) { Enabled = false; base.Dispose(disposing); }
    }

    // EL HUECO DE UN CONTROL ACTIVEX (en XP, el reproductor de video de Windows Media). Aqui el objeto lo pone quien lo sepa hacer
    // (AxHost.Fabrica, por su clsid); sin ella no hay video y GetOcx devuelve null, como si el control no estuviera instalado.
    public abstract class AxHost : Control, ISupportInitialize
    {
        public static Func<string, AxHost, object> Fabrica;
        private readonly string clsid;
        private object ocx;
        protected AxHost(string clsid) { this.clsid = clsid; }
        protected AxHost(string clsid, int flags) : this(clsid) { }
        protected object GetOcx()
        {
            if (ocx == null && Fabrica != null) try { ocx = Fabrica(clsid, this); } catch { ocx = null; }
            return ocx;
        }
        public void BeginInit() { }
        public void EndInit() { }
        public State OcxState { get; set; }
        protected virtual void AttachInterfaces() { }
        protected override void Dispose(bool disposing) { IDisposable d = ocx as IDisposable; ocx = null; if (d != null) try { d.Dispose(); } catch { } base.Dispose(disposing); }
        public class State { }
    }

    // En la rocola no hay quien navegue carpetas con un raton: se cancela (Ctrl+F2 de XP; en Rocola OS las carpetas van por dir=).
    public class CommonDialog : Component { public DialogResult ShowDialog() { return DialogResult.Cancel; } public DialogResult ShowDialog(IWin32Window owner) { return DialogResult.Cancel; } }
    public sealed class FolderBrowserDialog : CommonDialog
    {
        public string Description { get; set; } = "";
        public string SelectedPath { get; set; } = "";
        public bool ShowNewFolderButton { get; set; } = true;
        public Environment.SpecialFolder RootFolder { get; set; }
    }
}

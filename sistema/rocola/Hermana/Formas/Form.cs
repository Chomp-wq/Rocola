// LA VENTANA. En Rocola OS no hay escritorio: cada Form es una capa de la pantalla (Escritorio.cs). La de la rocola va sin borde y a
// pantalla completa, como en XP; las hojas (Ctrl+F3) llevan su barra de titulo dibujada aqui.
using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms
{
    public class ContainerControl : ScrollableControl
    {
        public Control ActiveControl { get { Control f = Escritorio.Foco; return f != null && Contains(f) ? f : null; } set { if (value != null) value.Focus(); } }
        public SizeF AutoScaleDimensions { get; set; }
        public AutoScaleMode AutoScaleMode { get; set; }
        public bool Validate() { return true; }
    }
    public class ScrollableControl : Control
    {
        public bool AutoScroll { get; set; }
    }

    public class Form : ContainerControl
    {
        internal const int Borde = 3, Titulo = 25;
        private FormBorderStyle borde = FormBorderStyle.Sizable;
        private FormWindowState estado = FormWindowState.Normal;
        private bool cargada, mostrada, cerrando, modal;
        internal bool cerrada;
        public Form() { Visible = false; }
        public FormBorderStyle FormBorderStyle { get { return borde; } set { borde = value; Ajusta(); EnsuciaPadre(); } }
        public FormWindowState WindowState { get { return estado; } set { estado = value; Ajusta(); } }
        public FormStartPosition StartPosition { get; set; } = FormStartPosition.WindowsDefaultLocation;
        public bool TopMost { get; set; }
        public bool KeyPreview { get; set; }
        public bool ShowInTaskbar { get; set; } = true;
        public bool ControlBox { get; set; } = true;
        public bool MaximizeBox { get; set; } = true;
        public bool MinimizeBox { get; set; } = true;
        public bool ShowIcon { get; set; } = true;
        public double Opacity { get; set; } = 1.0;
        public Icon Icon { get; set; }
        public Form Owner { get; set; }
        public IButtonControl AcceptButton { get; set; }
        public IButtonControl CancelButton { get; set; }
        public DialogResult DialogResult { get; set; }
        public bool Modal { get { return modal; } }
        public static Form ActiveForm { get { return Escritorio.Activa; } }
        public bool IsMdiContainer { get; set; }
        internal bool ConBorde { get { return borde != FormBorderStyle.None && estado != FormWindowState.Maximized; } }
        internal override Size ClienteDe(Size s) { return ConBorde ? new Size(Math.Max(0, s.Width - 2 * Borde), Math.Max(0, s.Height - Titulo - 2 * Borde)) : s; }
        internal override Size TamanoPorCliente(Size s) { return ConBorde ? new Size(s.Width + 2 * Borde, s.Height + Titulo + 2 * Borde) : s; }
        internal override Point OrigenCliente { get { return ConBorde ? new Point(Borde, Titulo + Borde) : Point.Empty; } }
        private void Ajusta()
        {
            if (estado == FormWindowState.Maximized) Bounds = new Rectangle(Point.Empty, Escritorio.Pantalla);
        }
        protected override void SetVisibleCore(bool value)
        {
            if (value && !cargada)
            {
                cargada = true;
                CreateControl();
                Ajusta();
                if (StartPosition == FormStartPosition.CenterScreen || (StartPosition == FormStartPosition.CenterParent && Owner == null))
                    Location = new Point((Escritorio.Pantalla.Width - Width) / 2, (Escritorio.Pantalla.Height - Height) / 2);
                else if (StartPosition == FormStartPosition.CenterParent && Owner != null)
                    Location = new Point(Owner.Left + (Owner.Width - Width) / 2, Owner.Top + (Owner.Height - Height) / 2);
                OnLoad(EventArgs.Empty);
            }
            base.SetVisibleCore(value);
            if (value) { Escritorio.Agrega(this); Activate(); if (!mostrada) { mostrada = true; Escritorio.Encola(new Action(delegate { if (!IsDisposed) OnShown(EventArgs.Empty); }), null, false); } }
            else Escritorio.Oculta(this);
        }
        public void Activate() { Escritorio.Activa = this; Escritorio.AlFrente(this); OnActivated(EventArgs.Empty); }
        public void Close()
        {
            if (cerrando || cerrada) return;
            cerrando = true;
            try
            {
                FormClosingEventArgs e = new FormClosingEventArgs(CloseReason.UserClosing, false);
                OnFormClosing(e);
                if (e.Cancel) return;
                cerrada = true;
                Visible = false;
                OnFormClosed(new FormClosedEventArgs(CloseReason.UserClosing));
                if (!modal) Dispose();
            }
            finally { cerrando = false; }
        }
        public DialogResult ShowDialog() { return ShowDialog(null); }
        public DialogResult ShowDialog(IWin32Window owner)
        {
            if (owner is Form) Owner = (Form)owner;
            modal = true; cerrada = false; DialogResult = DialogResult.None;
            Visible = true;
            Escritorio.BucleModal(this);
            modal = false;
            if (DialogResult == DialogResult.None) DialogResult = DialogResult.Cancel;
            return DialogResult;
        }
        internal bool SigueModal { get { return !cerrada && !IsDisposed && DialogResult == DialogResult.None && Visible; } }
        protected override void OnPaintBackground(PaintEventArgs e) { base.OnPaintBackground(e); }
        public event EventHandler Load, Shown, Activated, Deactivate;
        public event FormClosingEventHandler FormClosing;
        public event FormClosedEventHandler FormClosed;
        protected virtual void OnLoad(EventArgs e) { Load?.Invoke(this, e); }
        protected virtual void OnShown(EventArgs e) { Shown?.Invoke(this, e); }
        protected virtual void OnActivated(EventArgs e) { Activated?.Invoke(this, e); }
        protected virtual void OnDeactivate(EventArgs e) { Deactivate?.Invoke(this, e); }
        protected virtual void OnFormClosing(FormClosingEventArgs e) { FormClosing?.Invoke(this, e); }
        protected virtual void OnFormClosed(FormClosedEventArgs e) { FormClosed?.Invoke(this, e); }
        protected override void Dispose(bool disposing) { Escritorio.Quita(this); base.Dispose(disposing); }
    }
    public interface IWin32Window { IntPtr Handle { get; } }
    public interface IButtonControl { DialogResult DialogResult { get; set; } void NotifyDefault(bool value); void PerformClick(); }
}

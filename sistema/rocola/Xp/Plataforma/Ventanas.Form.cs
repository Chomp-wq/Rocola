// LAS VENTANAS DE LA ROCOLA (traduccion de XP, fase 1; ver Ventanas.Tipos.cs): el formulario, la aplicacion, el temporizador y lo demas
// que la rocola de XP usa de WinForms. En Rocola OS no hay escritorio: cada Form es una capa de la pantalla (Escritorio.cs). La de la
// rocola va sin borde y a pantalla completa, como en XP; las hojas (Ctrl+F3) llevan su barra de titulo, dibujada como la de XP.
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;

namespace Rocola.Ventanas
{
    public class Form : ContainerControl
    {
        internal const int Borde = 3, Titulo = 25;
        private FormBorderStyle borde = FormBorderStyle.Sizable;
        private FormWindowState estado = FormWindowState.Normal;
        private bool cargada, mostrada, cerrando, modal;
        internal bool cerrada;
        public Form() { SetVisibleSinCargar(false); }
        private void SetVisibleSinCargar(bool v) { base.SetVisibleCore(v); }
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
        public Form Owner { get; set; }
        public Icon Icon { get; set; }   // el icono de la barra de tareas de XP: en Rocola OS no hay barra (se guarda y no se usa)
        public IButtonControl AcceptButton { get; set; }
        public IButtonControl CancelButton { get; set; }
        public DialogResult DialogResult { get; set; }
        public bool Modal { get { return modal; } }
        public static Form ActiveForm { get { return Escritorio.Activa; } }
        internal bool ConBorde { get { return borde != FormBorderStyle.None && estado != FormWindowState.Maximized; } }
        internal override Size ClienteDe(Size s) { return ConBorde ? new Size(Math.Max(0, s.Width - 2 * Borde), Math.Max(0, s.Height - Titulo - 2 * Borde)) : s; }
        internal override Size TamanoPorCliente(Size s) { return ConBorde ? new Size(s.Width + 2 * Borde, s.Height + Titulo + 2 * Borde) : s; }
        internal override Point OrigenCliente { get { return ConBorde ? new Point(Borde, Titulo + Borde) : Point.Empty; } }
        private void Ajusta() { if (estado == FormWindowState.Maximized) Bounds = new Rectangle(Point.Empty, Escritorio.Pantalla); }
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
            if (value)
            {
                Escritorio.Agrega(this); Activate();
                if (!mostrada) { mostrada = true; Escritorio.Encola(new Action(delegate { if (!IsDisposed) OnShown(EventArgs.Empty); }), null, false); }
            }
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
        // MODAL: como en Windows, mientras la hoja esta abierta sigue todo lo demas (los temporizadores de la rocola, la musica y la
        // pantalla): el bucle de la hoja es el mismo bucle del escritorio, hasta que se cierra.
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

    public sealed class Application
    {
        private Application() { }
        internal static volatile bool Saliendo;
        public static event EventHandler Idle;
        public static event ThreadExceptionEventHandler ThreadException;
        public static event EventHandler ApplicationExit;
        internal static void LlamaIdle() { EventHandler h = Idle; if (h != null) try { h(null, EventArgs.Empty); } catch (Exception ex) { Avisa(ex); } }
        internal static void Avisa(Exception ex)
        {
            ThreadExceptionEventHandler h = ThreadException;
            if (h != null) { try { h(null, new ThreadExceptionEventArgs(ex)); } catch (Exception otra) { Console.Error.WriteLine("[ventanas] " + otra); } }
            else Console.Error.WriteLine("[ventanas] " + ex);
        }
        // la carpeta del programa (en XP, la de Rocola.exe: ahi guarda sus datos si no hay ROCOLA_DATOS)
        public static string StartupPath { get { return AppContext.BaseDirectory.TrimEnd('/', '\\'); } }
        public static string ExecutablePath { get { return Environment.ProcessPath ?? System.IO.Path.Combine(StartupPath, "rocola-xp"); } }
        public static string ProductName { get { return "Rocola"; } }
        public static string ProductVersion { get { return "1.0"; } }
        public static void EnableVisualStyles() { }
        public static void SetCompatibleTextRenderingDefault(bool defaultValue) { }
        public static void SetUnhandledExceptionMode(UnhandledExceptionMode mode) { }
        public static void DoEvents() { Escritorio.Vuelta(false); }
        public static void Exit() { Saliendo = true; Escritorio.Despierta(); }
        public static void ExitThread() { Exit(); }
        public static void Run(Form mainForm)
        {
            Escritorio.HiloUi = Thread.CurrentThread; ContextoVentana.Pon();
            bool cerrada = false;
            mainForm.FormClosed += delegate { cerrada = true; };
            mainForm.Visible = true;
            Escritorio.Bucle(delegate { return !cerrada && !Saliendo && !mainForm.IsDisposed; });
            ApplicationExit?.Invoke(null, EventArgs.Empty);
        }
    }

    // EL TEMPORIZADOR DE LA VENTANA: avisa en el hilo de la pantalla, como el de Windows (el escritorio lo despierta a su hora).
    public class Timer : Component
    {
        private int intervalo = 100;
        private bool activo;
        internal double Proximo;
        public Timer() { }
        public Timer(IContainer container) : this() { if (container != null) container.Add(this); }
        public int Interval
        {
            get { return intervalo; }
            set { if (value < 1) throw new ArgumentOutOfRangeException("Interval"); intervalo = value; if (activo) Proximo = Escritorio.AhoraMs + intervalo; }
        }
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

    // EL ICONO DE UNA VENTANA (en XP, el de la barra de tareas y Alt+Tab). En Rocola OS no hay barra de tareas: no se dibuja.
    public sealed class Icon : IDisposable
    {
        private Icon() { }
        public static Icon ExtractAssociatedIcon(string ruta) { return new Icon(); }
        public void Dispose() { }
    }

    public class Screen
    {
        private Screen() { }
        public static Screen PrimaryScreen { get { return new Screen(); } }
        public static Screen[] AllScreens { get { return new Screen[] { PrimaryScreen }; } }
        public static Screen FromControl(Control c) { return PrimaryScreen; }
        public static Screen FromHandle(IntPtr h) { return PrimaryScreen; }
        public static Screen FromPoint(Point p) { return PrimaryScreen; }
        public Rectangle Bounds { get { return new Rectangle(Point.Empty, Escritorio.Pantalla); } }
        public Rectangle WorkingArea { get { return Bounds; } }
        public string DeviceName { get { return "pantalla"; } }
        public bool Primary { get { return true; } }
        public int BitsPerPixel { get { return 32; } }
    }

    // EL CURSOR: la rocola de XP lo esconde; aqui no hay flecha que esconder (el raton es cosa de Rocola OS, fase 11).
    public sealed class Cursor : IDisposable
    {
        private readonly string nombre;
        internal Cursor(string n) { nombre = n; }
        public static Point Position { get; set; }
        public static Cursor Current { get; set; }
        public static void Hide() { }
        public static void Show() { }
        public static Rectangle Clip { get; set; }
        public void Dispose() { }
        public override string ToString() { return "[Cursor: " + nombre + "]"; }
    }
    public sealed class Cursors
    {
        private Cursors() { }
        public static Cursor Default { get; } = new Cursor("Default");
        public static Cursor Hand { get; } = new Cursor("Hand");
        public static Cursor Arrow { get; } = new Cursor("Arrow");
        public static Cursor WaitCursor { get; } = new Cursor("WaitCursor");
        public static Cursor No { get; } = new Cursor("No");
    }

    // LOS AVISOS DE WINDOWS (MessageBox): en la rocola no hay quien haga clic. Se apuntan (en la salida de errores) y se dan por aceptados.
    public class MessageBox
    {
        private MessageBox() { }
        public static DialogResult Show(string text) { return Show(text, ""); }
        public static DialogResult Show(string text, string caption) { Console.Error.WriteLine("[Aviso] " + caption + ": " + text); return DialogResult.OK; }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) { Show(text, caption); return buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel ? DialogResult.Yes : DialogResult.OK; }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Show(text, caption, buttons); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton b) { return Show(text, caption, buttons); }
        public static DialogResult Show(IWin32Window owner, string text) { return Show(text); }
        public static DialogResult Show(IWin32Window owner, string text, string caption) { return Show(text, caption); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) { return Show(text, caption, buttons); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Show(text, caption, buttons); }
    }

    // EL HUECO DE UN CONTROL ACTIVEX (en XP, el reproductor de video de Windows Media, por su CLSID). En Linux no hay ActiveX: el objeto lo
    // pone quien sepa hacerlo (Fabrica; el video se traduce a mpv en la fase 9). Sin ella, GetOcx da null, como en un XP sin el reproductor.
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
        protected override void Dispose(bool disposing) { IDisposable d = ocx as IDisposable; ocx = null; if (d != null) try { d.Dispose(); } catch { } base.Dispose(disposing); }
    }

    // ELEGIR UNA CARPETA CON EL RATON (Ctrl+F2 de XP): en Rocola OS las carpetas de la musica las encuentra rocola-discos; aqui se cancela.
    public class CommonDialog : Component { public DialogResult ShowDialog() { return DialogResult.Cancel; } public DialogResult ShowDialog(IWin32Window owner) { return DialogResult.Cancel; } }
    public sealed class FolderBrowserDialog : CommonDialog
    {
        public string Description { get; set; } = "";
        public string SelectedPath { get; set; } = "";
        public bool ShowNewFolderButton { get; set; } = true;
        public Environment.SpecialFolder RootFolder { get; set; }
    }

    // Lo que se manda a la pantalla desde otros hilos con SynchronizationContext.Post llega al hilo de la pantalla.
    public sealed class ContextoVentana : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object state) { Escritorio.Encola(new Action(delegate { d(state); }), null, false); }
        public override void Send(SendOrPostCallback d, object state)
        {
            if (Thread.CurrentThread == Escritorio.HiloUi) { d(state); return; }
            Escritorio.Pendiente p = Escritorio.Encola(new Action(delegate { d(state); }), null, true);
            p.AsyncWaitHandle.WaitOne(); if (p.Error != null) throw p.Error;
        }
        public override SynchronizationContext CreateCopy() { return this; }
        internal static void Pon() { if (!(SynchronizationContext.Current is ContextoVentana)) SynchronizationContext.SetSynchronizationContext(new ContextoVentana()); }
    }
}

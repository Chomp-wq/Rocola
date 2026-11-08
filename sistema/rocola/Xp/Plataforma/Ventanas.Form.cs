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
        // EL MARCO DE LAS VENTANAS DE XP (tema Luna, 96 puntos por pulgada), como lo cuenta AdjustWindowRectEx con los estilos que pone
        // Form.CreateParams: la barra de titulo mide SM_CYCAPTION = 26 (con WS_CAPTION: si hay Text o ControlBox); el marco, 3 a cada
        // lado con barra o con WS_EX_DLGMODALFRAME (FixedDialog, o sin icono), 4 si cambia de tamano (WS_THICKFRAME: Sizable) y 1 si es
        // solo WS_BORDER; Fixed3D suma el borde hundido de 2 (WS_EX_CLIENTEDGE). Asi un 300x300 Sizable tiene 292x266 de cliente y un
        // FixedDialog se lleva 6x32. (Las de herramientas, que la rocola no usa, toman la barra normal.)
        internal const int AltoBarra = 26, MarcoFijo = 3, MarcoAncho = 4, BordeHundido = 2;
        private FormBorderStyle borde = FormBorderStyle.Sizable;
        private FormWindowState estado = FormWindowState.Normal;
        private bool cargada, hechoVisible, modal, llamadoClosing, clienteFijado;
        private Size clienteGuardado;
        private Rectangle limitesNormales;
        private Form duenoVentana;   // el dueno de Windows del dialogo (ShowDialog sin dueno: la ventana activa)
        internal CloseReason razonCierre = CloseReason.None;
        public Form() { PonVisibleSinAviso(false); clienteGuardado = ClientSize; }   // (nace escondido, sin VisibleChanged)
        protected override Size DefaultSize { get { return new Size(300, 300); } }
        internal bool ConTitulo { get { return borde != FormBorderStyle.None && (!string.IsNullOrEmpty(Text) || ControlBox); } }
        internal int AltoTitulo { get { return ConTitulo ? AltoBarra : 0; } }
        internal int Marco
        {
            get
            {
                if (borde == FormBorderStyle.None) return 0;
                bool grueso = borde == FormBorderStyle.Sizable || borde == FormBorderStyle.SizableToolWindow;
                bool dlgModal = borde == FormBorderStyle.FixedDialog || (!ShowIcon && (borde == FormBorderStyle.Sizable || borde == FormBorderStyle.Fixed3D || borde == FormBorderStyle.FixedSingle));
                int m = dlgModal || grueso || ConTitulo ? 2 : 0;   // (el marco de fuera)
                if (grueso) m += MarcoAncho - MarcoFijo;
                m += 1;                                            // (WS_BORDER)
                if (borde == FormBorderStyle.Fixed3D) m += BordeHundido;
                return m;
            }
        }
        internal bool ConBorde { get { return Marco > 0 || AltoTitulo > 0; } }
        // (como FormBorderStyle de WinForms: si se fijo ClientSize y la ventana aun no existe, se conserva el cliente)
        public FormBorderStyle FormBorderStyle
        {
            get { return borde; }
            set
            {
                borde = value;
                if (clienteFijado && !IsHandleCreated) SetClientSizeCore(clienteGuardado.Width, clienteGuardado.Height);
                if (IsHandleCreated && Visible && estado == FormWindowState.Maximized) Bounds = Maximizada();
                EnsuciaPadre();
            }
        }
        // EL ESTADO DE LA VENTANA (Form.WindowState de WinForms): antes de crearse solo se guarda; se aplica al crearla (WS_MAXIMIZE) o,
        // ya a la vista, en el momento (ShowWindow). Maximizada ocupa el area de trabajo con el marco por fuera (la barra se ve); al
        // volver a Normal recupera su sitio de antes. (Minimizada: en Rocola OS no hay barra de tareas; se guarda el estado.)
        public FormWindowState WindowState
        {
            get { return estado; }
            set
            {
                FormWindowState antes = estado;
                estado = value;
                if (!IsHandleCreated || !Visible || antes == value) return;
                if (antes == FormWindowState.Normal) limitesNormales = Bounds;
                if (value == FormWindowState.Maximized) Bounds = Maximizada();
                else if (value == FormWindowState.Normal) Bounds = limitesNormales;
            }
        }
        private Rectangle Maximizada()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea; int m = Marco;
            return new Rectangle(wa.X - m, wa.Y - m, wa.Width + 2 * m, wa.Height + 2 * m);
        }
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
        internal Form DuenoVentana { get { return duenoVentana ?? Owner; } }
        public Icon Icon { get; set; }   // el icono de la barra de tareas de XP: en Rocola OS no hay barra (se guarda y no se usa)
        private IButtonControl aceptar, porDefecto;
        // EL BOTON POR DEFECTO (Form.AcceptButton y UpdateDefaultButton de WinForms): el boton que tiene el foco, o si no el de aceptar;
        // Intro lo pulsa (ProcessDialogKey). Escape pulsa el de cancelar.
        public IButtonControl AcceptButton { get { return aceptar; } set { aceptar = value; ActualizaBotonPorDefecto(); } }
        public IButtonControl CancelButton { get; set; }
        internal void ActualizaBotonPorDefecto()
        {
            IButtonControl nuevo = ActiveControl as IButtonControl ?? aceptar;
            if (nuevo == porDefecto) return;
            if (porDefecto != null) porDefecto.NotifyDefault(false);
            porDefecto = nuevo;
            if (nuevo != null) nuevo.NotifyDefault(true);
        }
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & (Keys.Alt | Keys.Control)) == Keys.None)
            {
                Keys k = keyData & Keys.KeyCode;
                if (k == Keys.Enter && porDefecto != null) { if (porDefecto is Control) porDefecto.PerformClick(); return true; }
                if (k == Keys.Escape && CancelButton != null) { CancelButton.PerformClick(); return true; }
            }
            return base.ProcessDialogKey(keyData);
        }
        // En un formulario, Tab da la vuelta (Form.ProcessTabKey)
        protected override bool ProcessTabKey(bool forward) { return SelectNextControl(ActiveControl, forward, true, true, true); }
        // Al activarse (Form.Active de WinForms): el foco vuelve a su control activo si se ve, o a la hoja; sin control activo, al primero
        // del tabulador (asi al abrir Ctrl+F3 el foco esta en la pestana «Sonido», como en XP).
        internal void EnfocaAlActivar()
        {
            if (IsDisposed || !Visible) return;
            Control a = ActiveControl;
            if (a == null) { if (!SelectNextControl(null, true, true, true, false)) Escritorio.PonFoco(this, false); return; }
            if (a.Visible) Escritorio.PonFoco(a, false); else Escritorio.PonFoco(this, false);
        }
        internal void LlamaActivada() { OnActivated(EventArgs.Empty); }
        internal void LlamaDesactivada() { OnDeactivate(EventArgs.Empty); }
        // LA «X» DE CERRAR de la barra de titulo (en coordenadas de la ventana) y como esta: 0 normal, 1 con el raton encima, 2 apretada.
        internal Rectangle CajaCerrar { get { return new Rectangle(Width - 5 - 21, (AltoTitulo + Marco - 21) / 2 + 1, 21, 21); } }
        internal int EstadoCerrar { get; private set; }
        internal void PonEstadoCerrar(int e) { if (EstadoCerrar == e) return; EstadoCerrar = e; Escritorio.Ensucia(this); }
        public DialogResult DialogResult { get; set; }
        public bool Modal { get { return modal; } }
        public static Form ActiveForm { get { return Escritorio.Activa; } }
        internal override Size ClienteDe(Size s) { int m = Marco; return new Size(Math.Max(0, s.Width - 2 * m), Math.Max(0, s.Height - 2 * m - AltoTitulo)); }
        internal override Size TamanoPorCliente(Size s) { int m = Marco; return new Size(s.Width + 2 * m, s.Height + 2 * m + AltoTitulo); }
        internal override Point OrigenCliente { get { int m = Marco; return new Point(m, m + AltoTitulo); } }
        // el cliente de la ultima medida (_clientWidth de WinForms) y si se fijo a mano (FormStateSetClientSize)
        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            base.SetBoundsCore(x, y, width, height, specified);
            clienteGuardado = ClientSize;
        }
        protected override void SetClientSizeCore(int x, int y) { base.SetClientSizeCore(x, y); clienteFijado = true; clienteGuardado = new Size(x, y); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Escritorio.Ensucia(this); }   // (la barra de titulo)

        // CREAR LA VENTANA (CreateParams y CreateWindowEx de un formulario): la medida por el cliente si se fijo, la posicion de
        // StartPosition si no es un dialogo (CenterScreen: centrada en el area de trabajo, nunca por encima ni a la izquierda; con
        // WS_MAXIMIZE no se toca), HandleCreated y, si estaba maximizada, maximizada.
        private void CreaVentana()
        {
            if (clienteFijado) Size = TamanoPorCliente(clienteGuardado);
            if (!modal && StartPosition == FormStartPosition.CenterScreen && estado != FormWindowState.Maximized)
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(Math.Max(wa.X, wa.X + (wa.Width - Width) / 2), Math.Max(wa.Y, wa.Y + (wa.Height - Height) / 2));
            }
            CreateHandle();
            if (estado != FormWindowState.Normal) limitesNormales = Bounds;
            if (estado == FormWindowState.Maximized) Bounds = Maximizada();
        }
        // ENSENARSE (Form.SetVisibleCore de WinForms): la primera vez se crea la ventana; al ensenarse (WM_SHOWWINDOW) se crean los
        // hijos que se ven y, la primera vez, Load (OnCreateControl); despues VisibleChanged, la activacion (con el foco) y Shown, que
        // Load deja en la cola (BeginInvoke). Esconderse y volver a ensenarse no repite Load.
        protected override void SetVisibleCore(bool value)
        {
            if (VisiblePropio == value && DialogResult == DialogResult.OK) return;
            if (VisiblePropio == value && (!value || hechoVisible)) { base.SetVisibleCore(value); return; }
            if (value)
            {
                hechoVisible = true;
                if (!IsHandleCreated) CreaVentana();
                PonVisibleSinAviso(true);
                try { CreaControl(false); } finally { PonVisibleSinAviso(false); }
                if (modal && DialogResult != DialogResult.None) value = false;   // (Load puso DialogResult: el dialogo no se ensena)
            }
            base.SetVisibleCore(value);
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta(value ? "ventana-ve " + TrazaVentanas.Pieza(this) + (modal ? " modal" : "") : "ventana-esconde " + TrazaVentanas.Pieza(this) + " " + DialogResult);
            if (value) { Escritorio.Agrega(this); Activate(); }   // (y el foco: Form.Active)
            else Escritorio.Oculta(this);
        }
        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            if (hechoVisible && !cargada) { cargada = true; OnLoad(EventArgs.Empty); }
        }
        public void Activate() { if (!Visible || Escritorio.Deshabilitada(this)) return; Escritorio.AlFrente(this); Escritorio.ActivaForma(this, true); }
        // CERRAR (Form.Close y WmClose de WinForms): con la ventana creada, WM_CLOSE con CloseReason.UserClosing; si no, Dispose. Un
        // dialogo pone DialogResult.Cancel si no tenia y solo avisa FormClosing (que puede impedirlo); FormClosed lo avisa su bucle al
        // acabar (CheckCloseDialog), y ShowDialog lo esconde y destruye su ventana. Uno que no es dialogo: FormClosing, FormClosed y Dispose.
        public void Close()
        {
            if (IsHandleCreated) { razonCierre = CloseReason.UserClosing; WmClose(); }
            else Dispose();
        }
        // WM_SYSCOMMAND SC_CLOSE: la «X» de la barra de titulo y Alt+F4
        internal void CierraPorSistema() { razonCierre = CloseReason.UserClosing; WmClose(); }
        private void WmClose()
        {
            if (modal)
            {
                if (DialogResult == DialogResult.None) DialogResult = DialogResult.Cancel;
                llamadoClosing = false;
                CheckCloseDialog(true);
                return;
            }
            FormClosingEventArgs e = new FormClosingEventArgs(razonCierre, false);
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closing " + TrazaVentanas.Pieza(this) + " " + razonCierre);
            OnFormClosing(e);
            if (e.Cancel) return;
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closed " + TrazaVentanas.Pieza(this) + " " + razonCierre);
            OnFormClosed(new FormClosedEventArgs(razonCierre));
            Dispose();
        }
        // Form.CheckCloseDialog: si el dialogo ya tiene DialogResult (o se escondio), FormClosing (una vez; si se cancela, DialogResult
        // vuelve a None) y, si no es solo eso, FormClosed. true = el dialogo se acaba.
        internal bool CheckCloseDialog(bool soloClosing)
        {
            if (IsDisposed) return true;
            if (DialogResult == DialogResult.None && Visible) return false;
            try
            {
                FormClosingEventArgs e = new FormClosingEventArgs(razonCierre, false);
                if (!llamadoClosing)
                {
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closing " + TrazaVentanas.Pieza(this) + " " + razonCierre);
                    OnFormClosing(e);
                    if (e.Cancel) DialogResult = DialogResult.None; else llamadoClosing = true;
                }
                if (!soloClosing && DialogResult != DialogResult.None)
                {
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("closed " + TrazaVentanas.Pieza(this) + " " + razonCierre);
                    OnFormClosed(new FormClosedEventArgs(razonCierre));
                    llamadoClosing = false;
                }
            }
            catch (Exception ex) { DialogResult = DialogResult.None; Application.Avisa(ex); }
            return DialogResult != DialogResult.None || !Visible;
        }
        public DialogResult ShowDialog() { return ShowDialog(null); }
        // MODAL (Form.ShowDialog de WinForms): el dueno es el que se da (su ventana de arriba) o, sin el, la ventana activa; mientras
        // tanto sigue todo lo demas (los temporizadores de la rocola, la musica y la pantalla: el bucle de la hoja es el mismo bucle del
        // escritorio) hasta que CheckCloseDialog lo acaba. Al acabar se activa otra vez la que estaba activa, se esconde, se destruye su
        // ventana (IsHandleCreated vuelve a false; otro ShowDialog la crea de nuevo, con Load y Shown) y se devuelve DialogResult.
        public DialogResult ShowDialog(IWin32Window owner)
        {
            if (owner == this) throw new ArgumentException("Forms cannot own themselves or their owners.", "owner");
            if (Visible) throw new InvalidOperationException("Form that is already visible cannot be displayed as a modal dialog box.");
            if (!Enabled) throw new InvalidOperationException("Form that is not enabled cannot be displayed as a modal dialog box.");
            if (modal) throw new InvalidOperationException("Form that is already displayed modally cannot be displayed as a modal dialog box.");
            Control oc = owner as Control;
            if (oc != null) owner = oc.TopLevelControl;
            cargada = false; hechoVisible = false; razonCierre = CloseReason.None;
            Form activa = Escritorio.Activa;
            Form antes = Owner;
            duenoVentana = owner as Form ?? activa;
            if (owner is Form && owner != antes) Owner = (Form)owner;
            try
            {
                modal = true; DialogResult = DialogResult.None; llamadoClosing = false;
                if (!IsHandleCreated) CreaVentana();
                try
                {
                    if (DialogResult == DialogResult.None) { Visible = true; Escritorio.BucleModal(this); }
                }
                finally
                {
                    Form vuelve = activa != null && activa != this && !activa.IsDisposed && activa.Visible ? activa
                                : duenoVentana != null && duenoVentana != this && !duenoVentana.IsDisposed && duenoVentana.Visible ? duenoVentana : null;
                    if (vuelve != null) Escritorio.ActivaForma(vuelve, true);
                    if (!IsDisposed) { SetVisibleCore(false); if (IsHandleCreated) DestroyHandle(); }
                    modal = false;
                }
            }
            finally { Owner = antes; duenoVentana = null; }
            if (TrazaVentanas.Activa) TrazaVentanas.Apunta("dialogo-fin " + TrazaVentanas.Pieza(this) + " " + DialogResult);
            return DialogResult;
        }
        // (el bucle de la hoja: mientras CheckCloseDialog no la acabe)
        internal bool SigueModal { get { return !CheckCloseDialog(false); } }
        public event EventHandler Load, Shown, Activated, Deactivate;
        public event FormClosingEventHandler FormClosing;
        public event FormClosedEventHandler FormClosed;
        // Form.OnLoad de WinForms: un dialogo se centra aqui (CenterParent: en su dueno; CenterScreen: en la pantalla), Load, y Shown a la
        // cola de su ventana
        protected virtual void OnLoad(EventArgs e)
        {
            if (modal)
            {
                if (StartPosition == FormStartPosition.CenterParent) CentraEnDueno();
                else if (StartPosition == FormStartPosition.CenterScreen) CentraEnPantalla();
            }
            Load?.Invoke(this, e);
            if (IsHandleCreated) Escritorio.Encola(new Action(delegate { if (TrazaVentanas.Activa) TrazaVentanas.Apunta("shown " + TrazaVentanas.Pieza(this)); OnShown(EventArgs.Empty); }), null, false, this, "Shown");
        }
        // Form.CenterToParent: en medio de su dueno de Windows ((izq + der - ancho) / 2), sin salirse del area de trabajo
        private void CentraEnDueno()
        {
            Form d = DuenoVentana;
            if (d == null || d.IsDisposed) { CentraEnPantalla(); return; }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea, o = d.Bounds;
            int x = (o.Left + o.Right - Width) / 2, y = (o.Top + o.Bottom - Height) / 2;
            if (x < wa.X) x = wa.X; else if (x + Width > wa.Right) x = wa.Right - Width;
            if (y < wa.Y) y = wa.Y; else if (y + Height > wa.Bottom) y = wa.Bottom - Height;
            Location = new Point(x, y);
        }
        // Form.CenterToScreen
        private void CentraEnPantalla()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(Math.Max(wa.X, wa.X + (wa.Width - Width) / 2), Math.Max(wa.Y, wa.Y + (wa.Height - Height) / 2));
        }
        protected virtual void OnShown(EventArgs e) { Shown?.Invoke(this, e); }
        protected virtual void OnActivated(EventArgs e) { Activated?.Invoke(this, e); }
        protected virtual void OnDeactivate(EventArgs e) { Deactivate?.Invoke(this, e); }
        protected virtual void OnFormClosing(FormClosingEventArgs e) { FormClosing?.Invoke(this, e); }
        protected virtual void OnFormClosed(FormClosedEventArgs e) { FormClosed?.Invoke(this, e); }
        protected override void Dispose(bool disposing) { if (disposing && !IsDisposed) Escritorio.Quita(this); base.Dispose(disposing); }
    }

    public sealed class Application
    {
        private Application() { }
        internal static volatile bool Saliendo;
        public static event EventHandler Idle;
        public static event ThreadExceptionEventHandler ThreadException;
        public static event EventHandler ApplicationExit;
        // (ThreadContext.FDoIdle de WinForms: el que avisa es el hilo. Lo que lance se apunta en ThreadException, como lo de las ventanas;
        // en WinForms saldria de Application.Run)
        internal static void LlamaIdle() { EventHandler h = Idle; if (h != null) try { h(Thread.CurrentThread, EventArgs.Empty); } catch (Exception ex) { Avisa(ex); } }
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
        public static void SetUnhandledExceptionMode(UnhandledExceptionMode mode) { PruebasLinux.DespuesDelInicio(); }   // (pruebas de Linux que necesitan hilos: aqui ya acabo el inicializador del modulo)
        public static void DoEvents() { Escritorio.Bombea(); }   // (todo lo pendiente, sin Idle)
        public static void Exit() { Saliendo = true; Escritorio.Despierta(); }
        public static void ExitThread() { Exit(); }
        internal static Form Principal;   // (pruebas: VolcadoEstado) la ventana de Run
        public static void Run(Form mainForm)
        {
            Escritorio.HiloUi = Thread.CurrentThread; ContextoVentana.Pon();
            Principal = mainForm;
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
        internal readonly string Nombre;   // (pruebas: TrazaVentanas) donde se creo: archivo:linea
        public Timer([System.Runtime.CompilerServices.CallerFilePath] string archivo = null, [System.Runtime.CompilerServices.CallerLineNumber] int linea = 0) { Nombre = TrazaVentanas.Sitio(archivo, linea); }
        public Timer(IContainer container) : this() { if (container != null) container.Add(this); }
        public int Interval
        {
            get { return intervalo; }
            set { if (value < 1) throw new ArgumentOutOfRangeException("Interval"); intervalo = value; if (activo) Proximo = Escritorio.AhoraMs + Math.Max(Escritorio.TimerMinimo, intervalo); }
        }
        public bool Enabled
        {
            get { return activo; }
            set { if (activo == value) return; activo = value; if (value) { Proximo = Escritorio.AhoraMs + Math.Max(Escritorio.TimerMinimo, intervalo); Escritorio.AgregaTimer(this); } else Escritorio.QuitaTimer(this); }
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
        public static Point Position { get { return Escritorio.PosicionRaton; } set { Escritorio.PosicionRaton = value; } }   // (moverla no avisa a nadie, como en Windows)
        public static Cursor Current { get; set; }
        public static void Hide() { Escritorio.VisibilidadCursor--; }   // la cuenta de ShowCursor de Windows
        public static void Show() { Escritorio.VisibilidadCursor++; }
        internal string Nombre { get { return nombre; } }
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

    // LOS AVISOS DE WINDOWS (MessageBox), como los de XP: una hoja modal gris con su icono, el texto y sus botones (en espanol, como el XP
    // del bar). Intro pulsa el boton por defecto (el que tiene el foco) y Escape el de cancelar, o Aceptar si es el unico; con Si/No la
    // «X» no cierra. La rocola espera la respuesta, como en XP (se contesta con PONER, que es Intro). Ademas se apunta en la salida de
    // errores. Pedido desde otro hilo que no es el de la pantalla no se puede ensenar: se apunta y se da por aceptado.
    public class MessageBox
    {
        private MessageBox() { }
        public static DialogResult Show(string text) { return Muestra(null, text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption) { return Muestra(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) { return Muestra(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Muestra(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton b) { return Muestra(null, text, caption, buttons, icon, b); }
        public static DialogResult Show(IWin32Window owner, string text) { return Muestra(owner, text, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption) { return Muestra(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) { return Muestra(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) { return Muestra(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1); }
        private static DialogResult Muestra(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton b)
        {
            Console.Error.WriteLine("[Aviso] " + caption + ": " + text);
            if (Escritorio.HiloUi != null && Thread.CurrentThread != Escritorio.HiloUi)
                return buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel ? DialogResult.Yes : DialogResult.OK;
            using (CajaAviso c = new CajaAviso(text ?? "", caption ?? "", buttons, icon, b)) return c.ShowDialog(owner);
        }
    }

    // LA HOJA DEL AVISO, medida como la de user32 (SoftModalMessageBox): letra de mensajes de XP (Tahoma de 8), margen de 12, 16 entre el icono,
    // el texto y los botones, botones de 75x23 separados 6 y centrados, el texto partido a lo mas en 4/5 del ancho de la pantalla.
    internal sealed class CajaAviso : Form
    {
        private const int Margen = 12, Espacio = 16, EntreBotones = 6, BotonAncho = 75, BotonAlto = 23, Icono = 32;
        private readonly MessageBoxIcon icono;
        private readonly Rectangle sitioIcono;
        internal CajaAviso(string texto, string titulo, MessageBoxButtons botones, MessageBoxIcon icon, MessageBoxDefaultButton porDefecto)
        {
            icono = icon;
            Text = titulo;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Tahoma", 8f);
            string[] nombres; DialogResult[] res;
            switch (botones)
            {
                case MessageBoxButtons.OKCancel: nombres = new[] { "Aceptar", "Cancelar" }; res = new[] { DialogResult.OK, DialogResult.Cancel }; break;
                case MessageBoxButtons.AbortRetryIgnore: nombres = new[] { "Anular", "Reintentar", "Omitir" }; res = new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore }; break;
                case MessageBoxButtons.YesNoCancel: nombres = new[] { "Sí", "No", "Cancelar" }; res = new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel }; break;
                case MessageBoxButtons.YesNo: nombres = new[] { "Sí", "No" }; res = new[] { DialogResult.Yes, DialogResult.No }; break;
                case MessageBoxButtons.RetryCancel: nombres = new[] { "Reintentar", "Cancelar" }; res = new[] { DialogResult.Retry, DialogResult.Cancel }; break;
                default: nombres = new[] { "Aceptar" }; res = new[] { DialogResult.OK }; break;
            }
            bool conIcono = icon != MessageBoxIcon.None;
            Size t = TextRenderer.MeasureText(texto, Font, new Size(Escritorio.Pantalla.Width / 5 * 4, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.ExpandTabs);
            int fila = nombres.Length * (BotonAncho + EntreBotones) - EntreBotones;
            int izq, xBotones, ancho = t.Width + (conIcono ? Icono + Espacio : 0);
            if (fila > ancho) { izq = Margen + (fila - ancho) / 2; xBotones = Margen; }
            else { izq = Margen; xBotones = Margen + ancho / 2 - fila / 2; }
            int yTexto = Math.Max(Margen, Margen + (conIcono ? Icono : 0) / 2 - t.Height / 2);
            sitioIcono = new Rectangle(izq, Margen, Icono, Icono);
            int yBotones = Math.Max(yTexto + t.Height, conIcono ? Margen + Icono : 0) + Espacio;
            ClientSize = new Size(Math.Max(xBotones + fila, izq + ancho) + Margen, yBotones + BotonAlto + Margen);
            Label l = new Label();
            l.SetBounds(izq + (conIcono ? Icono + Espacio : 0), yTexto, t.Width + 1, t.Height);
            l.UseMnemonic = false; l.Text = texto; l.BackColor = Color.Transparent;
            Controls.Add(l);
            Button elegido = null, cancelar = null;
            int cual = Math.Min(nombres.Length - 1, (int)porDefecto / 256);   // Button1 = 0, Button2 = 256, Button3 = 512
            for (int i = 0; i < nombres.Length; i++)
            {
                Button b = new Button();
                b.SetBounds(xBotones + i * (BotonAncho + EntreBotones), yBotones, BotonAncho, BotonAlto);
                b.Text = nombres[i]; b.DialogResult = res[i];   // (como el de WinForms: al pulsarlo, la hoja se cierra con ese resultado)
                Controls.Add(b);
                if (i == cual) elegido = b;
                if (res[i] == DialogResult.Cancel || (nombres.Length == 1 && res[i] == DialogResult.OK)) cancelar = b;
            }
            CancelButton = cancelar;
            ControlBox = true;
            alCerrar = cancelar != null ? cancelar.DialogResult : DialogResult.None;   // (Si/No y Anular/Reintentar/Omitir: la «X» no hace nada)
            if (elegido != null) ActiveControl = elegido;
        }
        private readonly DialogResult alCerrar;
        // la «X» (y Alt+F4: SC_CLOSE; el cierre de un dialogo pone DialogResult.Cancel) es como el boton de cancelar: en un aviso con
        // solo Aceptar devuelve OK, como user32, y con Si/No o Anular/Reintentar/Omitir no cierra. (No se mira CloseReason: en WinForms se
        // queda en UserClosing despues de un cierre cancelado, y el boton que se pulse despues tiene que cerrar.)
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.Cancel && alCerrar != DialogResult.Cancel) { if (alCerrar == DialogResult.None) e.Cancel = true; else DialogResult = alCerrar; }
            base.OnFormClosing(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (icono == MessageBoxIcon.None) return;
            Graphics g = e.Graphics;
            Rectangle r = sitioIcono;
            SmoothingMode sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (icono == MessageBoxIcon.Warning)
            {   // el triangulo amarillo con la admiracion
                PointF[] p = { new PointF(r.X + 16, r.Y + 2), new PointF(r.Right - 1, r.Bottom - 3), new PointF(r.X + 1, r.Bottom - 3) };
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(255, 238, 120), Color.FromArgb(240, 190, 20), LinearGradientMode.Vertical)) g.FillPolygon(b, p);
                using (Pen pn = new Pen(Color.FromArgb(150, 110, 0))) g.DrawPolygon(pn, p);
                using (SolidBrush b = new SolidBrush(Color.Black)) { g.FillRectangle(b, r.X + 14.5f, r.Y + 10, 3, 11); g.FillEllipse(b, r.X + 14.5f, r.Y + 23, 3, 3); }
            }
            else if (icono == MessageBoxIcon.Error)
            {   // el circulo rojo con la equis blanca
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(255, 110, 90), Color.FromArgb(190, 0, 0), LinearGradientMode.Vertical)) g.FillEllipse(b, r.X + 1, r.Y + 1, 30, 30);
                using (Pen pn = new Pen(Color.FromArgb(130, 0, 0))) g.DrawEllipse(pn, r.X + 1, r.Y + 1, 30, 30);
                using (Pen pn = new Pen(Color.White, 3f)) { g.DrawLine(pn, r.X + 10, r.Y + 10, r.X + 22, r.Y + 22); g.DrawLine(pn, r.X + 22, r.Y + 10, r.X + 10, r.Y + 22); }
            }
            else
            {   // informacion (i) y pregunta (?): el circulo azul
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(120, 170, 255), Color.FromArgb(20, 70, 190), LinearGradientMode.Vertical)) g.FillEllipse(b, r.X + 1, r.Y + 1, 30, 30);
                using (Pen pn = new Pen(Color.FromArgb(10, 40, 130))) g.DrawEllipse(pn, r.X + 1, r.Y + 1, 30, 30);
                using (Font f = new Font("Tahoma", 16f, FontStyle.Bold))
                    TextRenderer.DrawText(g, icono == MessageBoxIcon.Question ? "?" : "i", f, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }
            g.SmoothingMode = sm;
        }
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
        public override void Post(SendOrPostCallback d, object state) { Escritorio.Encola(new Action(delegate { d(state); }), null, false, null, "contexto"); }
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

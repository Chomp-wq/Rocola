// LA PANTALLA DE LA ROCOLA: EL TECLADO, LA FLECHA, LA VENTANA DE DELANTE Y EL CONTEXTO DEL HILO (port nativo de las ventanas de XP,
// ventanas2, paso T). Lo que la rocola de XP le pregunta y le pide a Windows y a WinForms sobre las teclas ya atendidas (GetKeyState), la
// flecha del raton y la medida de la pantalla (Cursor.Hide/Show/Position, Screen), la ventana de delante y el foco (GetForegroundWindow,
// SetForegroundWindow, Form.Activate, BringToFront, SetFocus: ForceFocus, Form1.cs) y el SynchronizationContext del hilo de la pantalla
// (por el avisan SalidaPropia.cs y el BackgroundWorker del repaso, Form1.cs). Solo lo que XP usa, con las reglas de Win32 (su
// documentacion) y de WinForms (dotnet/winforms release/3.0, portada de .NET Framework 4.8: Cursor.cs, Form.cs, Control.cs,
// WindowsFormsSynchronizationContext.cs). El estado fisico de las teclas (GetAsyncKeyState) es de quien las lee: TeclasLinux.Fisica.
using System;
using System.Drawing;
using System.Threading;

namespace Rocola
{
    internal static partial class PantallaRocola
    {
        // ---------- las teclas segun lo ya atendido (GetKeyState) ----------
        // Windows lleva el estado de cada tecla segun los WM_KEYDOWN y WM_KEYUP que el hilo ya saco de su cola («The key status returned
        // from this function changes as a thread reads key messages from its message queue», documentacion de GetKeyState): la pantalla
        // lo anota al atender cada tecla, antes de darsela a nadie (durante su propio WM_KEYDOWN ya cuenta como abajo). El bit 30 de
        // WM_KEYDOWN («the previous key state»: ya estaba abajo, la repeticion de la mantenida) sale de ese mismo estado.
        private static readonly bool[] teclasAbajo = new bool[256];
        internal static bool SegunCola(int vk) { return teclasAbajo[vk & 0xFF]; }
        // (en su hilo, al atender la tecla) la anota y dice como estaba antes
        internal static bool AnotaTecla(int vk, bool abajo) { int i = vk & 0xFF; bool antes = teclasAbajo[i]; teclasAbajo[i] = abajo; return antes; }

        // ---------- la pantalla y la flecha (Screen y Cursor) ----------
        // La medida de la pantalla (Screen.PrimaryScreen.Bounds: en Rocola OS la del modo de KMS, o la de fuera de pantalla); la pone el
        // anfitrion al abrirla.
        internal static int Ancho = 1024, Alto = 768;
        // LA FLECHA DEL RATON: se ve si la cuenta de ShowCursor esta en 0 o mas (con raton empieza en 0; Cursor.Hide es ShowCursor(FALSE)
        // y Cursor.Show es ShowCursor(TRUE): Cursor.cs:483-486 y 611-614). Donde esta: la mueve el raton (TeclasLinux) y la rocola
        // (Cursor.Position = SetCursorPos, Cursor.cs:234-242), sin avisar a nadie, como en la referencia.
        private static int cuentaFlecha;
        internal static int CuentaFlecha { get { return cuentaFlecha; } }
        internal static bool FlechaVisible { get { return cuentaFlecha >= 0; } }
        internal static void EscondeFlecha() { cuentaFlecha--; }
        internal static void EnsenaFlecha() { cuentaFlecha++; }
        internal static Point Flecha = new Point(1023, 767);
        internal static void FlechaEn(int x, int y) { Flecha = new Point(x, y); }

        // ---------- la ventana de delante y el foco (ForceFocus, Form1.cs) ----------
        // En la pantalla de la rocola solo hay ventanas de la rocola: la suya y las que ella abre (Ctrl+F3, Ctrl+F2, los avisos, la de
        // prueba F12); no hay otros programas.
        //   Delante    la ventana activa (GetForegroundWindow).
        //   AlFrente   delante y activa, con el foco del teclado donde lo tenia (SetForegroundWindow; Form.Activate es eso mismo,
        //              Form.cs:2869-2882). Una ventana debajo de una hoja modal esta deshabilitada y no se activa.
        //   Encima     solo arriba de las demas, sin activarla (BringToFront de una ventana de arriba: SetWindowPos(HWND_TOP),
        //              Control.cs:4881-4894).
        //   Enfoca     el foco del teclado a esa ventana (SetFocus; la ventana se lo da a su control activo: ContainerControl.WmSetFocus).
        // TRANSICION: mientras las ventanas sean las de la capa (Plataforma/Ventanas.*.cs), las lleva ella y contesta aqui (lo pone
        // Escritorio al empezar).
        internal static Func<object> LaDeDelante;
        internal static Action<object> PonDelante, PonEncima, PonFoco;
        internal static object Delante { get { Func<object> f = LaDeDelante; return f != null ? f() : null; } }
        internal static void AlFrente(object ventana) { Action<object> a = PonDelante; if (a != null) a(ventana); }
        internal static void Encima(object ventana) { Action<object> a = PonEncima; if (a != null) a(ventana); }
        internal static void Enfoca(object ventana) { Action<object> a = PonFoco; if (a != null) a(ventana); }

        // ---------- el SynchronizationContext del hilo de la pantalla ----------
        // En XP, WinForms pone en el hilo de la pantalla su WindowsFormsSynchronizationContext al crear el primer control y al empezar el
        // bucle (InstallIfNeeded, WindowsFormsSynchronizationContext.cs): Post es un BeginInvoke a una ventana de encargos que vive lo que
        // vive el hilo (su MarshalingControl) y Send un Invoke. Lo usan SalidaPropia.cs (avisa por el la parada del sonido) y el
        // BackgroundWorker del repaso (Form1.cs: RunWorkerCompleted). Aqui Post es un encargo de la pantalla sin dueno (no se pierde) y
        // Send, desde otro hilo, espera a que se haga; en el hilo de la pantalla, en el momento.
        internal sealed class Contexto : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state) { EncolaDesde(null, delegate { d(state); }, "contexto", false); }
            public override void Send(SendOrPostCallback d, object state)
            {
                if (Thread.CurrentThread == Hilo) { d(state); return; }
                Encargo e = EncolaDesde(null, delegate { d(state); }, "contexto", true);
                e.SenalHecho.WaitOne();
                if (e.Error != null) throw e.Error;
            }
            public override SynchronizationContext CreateCopy() { return this; }
        }
        // (InstallIfNeeded) el de la pantalla en este hilo, si aun no lo tiene
        internal static void PonContexto() { if (!(SynchronizationContext.Current is Contexto)) SynchronizationContext.SetSynchronizationContext(new Contexto()); }
    }
}

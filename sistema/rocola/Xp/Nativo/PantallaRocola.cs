// LA PANTALLA DE LA ROCOLA (port nativo de las ventanas de XP): el bucle de la rocola en su hilo (el principal), lo que en XP hacian
// Application.Run, Application.Idle, Application.DoEvents, Control.BeginInvoke e InvokeRequired, los temporizadores (Reloj.cs) y el aviso
// de Windows al apagarse (WM_QUERYENDSESSION y WM_ENDSESSION). No imita la API de WinForms: son las reglas que la rocola de XP supone,
// escritas a la vista y citando la fuente con que se reviso la referencia (dotnet/winforms release/3.0, portada de .NET Framework 4.8:
// Application.cs, Control.cs, NativeWindow.cs, Form.cs, Timer.cs) o la documentacion de Win32.
//   (Se llama PantallaRocola y no Pantalla porque Rocola.Pantalla ya es el espacio de nombres de KMS y OpenGL -Consola/Pantalla/Gl.cs y
//   Kms.cs, que no se tocan-: desde el codigo de XP, que esta en el espacio Rocola, «Pantalla.X» seria siempre ese espacio de nombres.)
//
// EL ORDEN (GetMessage de Windows: «posted messages, input messages, WM_PAINT, WM_TIMER»): cada vuelta atiende UNA cosa, la primera que
// haya de:
//   1. un encargo: lo que otro hilo pidio con BeginInvoke (PostMessage), en el orden en que llegaron;
//   2. una entrada: una tecla o el raton (evdev, o las pruebas: TeclasLinux.cs), en orden;
//   3. el pintado de lo sucio (WM_PAINT: Windows lo da cuando no hay encargos ni entrada);
//   4. un reloj vencido (WM_TIMER: Windows lo da al final, cuando no hay nada mas);
// y cuando no queda nada: Libre (Application.Idle) UNA vez y a esperar (WaitMessage) a que llegue algo, venza un reloj o toque pintar;
// Libre vuelve a salir despues de atender algo (ComponentManager.FPushMessageLoop, Application.cs:1838-2000: FDoIdle con la cola vacia y
// WaitMessage). Bombea (DoEvents) atiende todo lo pendiente sin Libre (Application.cs:1960-1969: el bucle de DoEvents sale con la cola
// vacia antes de FDoIdle). Los modales (ShowDialog) son el mismo bucle anidado en el mismo hilo: siguen los relojes, los encargos y Libre.
//
// LAS EXCEPCIONES (la rocola de XP pone SetUnhandledExceptionMode(CatchException) y ThreadException, Program.cs): lo que lanza un
// encargo, una tecla, el raton, un pintado o un reloj se le da a AlFallar (Application.ThreadException) y la rocola sigue:
//   - encargo: InvokeMarshaledCallbacks guarda t.GetBaseException() y, si no es Invoke, Application.OnThreadException (Control.cs:6775-6823);
//   - tecla: PreProcessControlMessageInternal dentro de try/catch -> OnThreadException (Application.cs:3600-3623);
//   - raton, pintado y relojes: pasan por el WndProc de su ventana y NativeWindow.Callback los atrapa -> OnThreadException
//     (NativeWindow.cs:484-504; Control.WndProcException, Control.cs:13713-13717).
// Lo que lanza un manejador de Libre NO se atrapa: FDoIdle (Application.cs:3869-3873) llama al manejador sin try y el bucle
// (RunMessageLoopInner, Application.cs:3348-3420) solo tiene try/finally: sale de Corre como de Application.Run (y de un modal, sube al
// que lo abrio). Decision del dueno del 8-11-2026 (AGENTS.md, «Diseno del port nativo»): como WinForms (el lanzador de Rocola OS la abre
// otra vez); la referencia lo apuntaba y seguia.
//
// EL APAGADO DEL SISTEMA (SIGTERM) es lo que en XP es el apagado de Windows: un encargo (Apagado) en el hilo de la pantalla que llama a
// AlApagar, que cierra las ventanas de la rocola con CloseReason.WindowsShutDown (Form.cs:7208-7211 y WmClose, 6638-6790), y en XP eso
// borra el estado vivo (Form1.Video.cs, OnFormClosing; regla del 8-11-2026). El programa no se corta: sale de Corre cuando la rocola se
// cerro, como en XP sale de Application.Run.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rocola
{
    // LO QUE PUEDE RECIBIR ENCARGOS: en XP, la ventana de un control (BeginInvoke los manda a su Handle). Su vida es el numero de la ventana
    // que tiene ahora (0: sin ventana); un encargo que llega cuando su dueno ya no tiene esa vida (se cerro) no corre: WinForms completa
    // los pendientes con ObjectDisposedException al destruir la ventana, sin ThreadException (Control.cs:5255-5276).
    internal interface IDuenoDeEncargos { long Vida { get; } }

    internal static partial class PantallaRocola
    {
        // ---------- el reloj y el hilo ----------
        // El reloj de la pantalla: milisegundos desde el arranque (el mismo de los relojes, ROCOLA_PULSA, ROCOLA_CAPTURA y las trazas).
        private static readonly Stopwatch reloj = Stopwatch.StartNew();
        internal static double AhoraMs { get { return reloj.Elapsed.TotalMilliseconds; } }
        // El hilo de la pantalla: el que corre el bucle (o el primero que crea una ventana). EnSuHilo es lo contrario de InvokeRequired.
        internal static Thread Hilo;
        internal static bool EnSuHilo { get { Thread h = Hilo; return h == null || Thread.CurrentThread == h; } }
        internal static object Principal;   // (pruebas: VolcadoEstado) la rocola que corre en Corre

        // ---------- los encargos (PostMessage) y la entrada ----------
        internal sealed class Encargo
        {
            internal Action Metodo; internal IDuenoDeEncargos Dueno; internal long Vida; internal string Sitio; internal bool Espera;
            internal Exception Error;
            private volatile bool hecho;
            private ManualResetEvent senalHecho;
            private readonly object c = new object();
            internal bool Hecho { get { return hecho; } }
            internal void Termina() { lock (c) { hecho = true; if (senalHecho != null) senalHecho.Set(); } }
            internal WaitHandle SenalHecho { get { lock (c) { if (senalHecho == null) senalHecho = new ManualResetEvent(hecho); return senalHecho; } } }
        }
        private static readonly object cerrojo = new object();
        private static readonly Queue<Encargo> encargos = new Queue<Encargo>();
        private static readonly Queue<Action> entrada = new Queue<Action>();
        private static readonly AutoResetEvent senal = new AutoResetEvent(false);

        // Un encargo para el hilo de la pantalla (BeginInvoke). Con dueno, viaja con su vida: sin ventana lanza, como WinForms
        // (MarshaledInvoke: «Invoke or BeginInvoke cannot be called on a control until the window handle has been created»).
        internal static Encargo Encola(IDuenoDeEncargos dueno, Action metodo, [CallerFilePath] string archivo = null, [CallerLineNumber] int linea = 0)
        { return EncolaDesde(dueno, metodo, TrazaVentanas.Sitio(archivo, linea), false); }
        internal static Encargo EncolaDesde(IDuenoDeEncargos dueno, Action metodo, string sitio, bool espera)
        {
            Encargo e = new Encargo(); e.Metodo = metodo; e.Sitio = sitio; e.Espera = espera;
            if (dueno != null)
            {
                long v = dueno.Vida;
                if (v == 0) throw new InvalidOperationException("Invoke or BeginInvoke cannot be called on a control until the window handle has been created.");
                e.Dueno = dueno; e.Vida = v;
            }
            lock (cerrojo) encargos.Enqueue(e);
            senal.Set();
            return e;
        }
        // La entrada (teclas y raton) la mandan los hilos de evdev y de las pruebas.
        internal static void Entrada(Action a) { lock (cerrojo) entrada.Enqueue(a); senal.Set(); }
        // Que el bucle mire otra vez (un cuadro nuevo del video, Sal...).
        internal static void Despierta() { senal.Set(); }

        // ---------- los relojes (SetTimer y WM_TIMER; Reloj.cs) ----------
        private static readonly List<Reloj> relojes = new List<Reloj>();
        // en marcha (o otra vez desde ahora: RestartTimer)
        internal static void PonReloj(Reloj r, bool desdeAhora)
        {
            lock (cerrojo) { if (desdeAhora || !relojes.Contains(r)) r.Proximo = AhoraMs + r.Plazo; if (!relojes.Contains(r)) relojes.Add(r); }
            senal.Set();
        }
        internal static void QuitaReloj(Reloj r) { lock (cerrojo) relojes.Remove(r); }
        private static double ProximoReloj()
        {
            double m = double.MaxValue;
            lock (cerrojo) foreach (Reloj r in relojes) if (r.Proximo < m) m = r.Proximo;
            return m;
        }
        // el reloj vencido que toca (el que vencio antes), ya movido a su siguiente vencimiento: los vencimientos que se juntaron salen
        // como uno solo y el siguiente sigue en su sitio (plazo fijo desde el anterior)
        private static Reloj RelojVencido()
        {
            double ahora = AhoraMs; Reloj v = null;
            lock (cerrojo)
            {
                foreach (Reloj r in relojes) if (r.Proximo <= ahora && (v == null || r.Proximo < v.Proximo)) v = r;
                if (v != null) { double p = v.Plazo; do v.Proximo += p; while (v.Proximo <= ahora); }
            }
            return v;
        }

        // ---------- el pintado (WM_PAINT) ----------
        // Lo da quien pinta: mientras dure la transicion, la capa de ventanas (Escritorio: el panel de la tarjeta y la composicion de las
        // hojas); despues, la composicion nativa. PintaUno pinta lo primero que este sucio (true si habia algo); HaySucio lo mira sin
        // pintarlo; ProximoPintado dice cuando habra que pintar aunque no llegue nada (ms del reloj de la pantalla).
        internal static Func<bool> PintaUno, HaySucio;
        internal static Func<double> ProximoPintado;

        // ---------- lo que hay pendiente (PeekMessage con PM_NOREMOVE) ----------
        internal static bool HayPendientes
        {
            get
            {
                lock (cerrojo) { if (encargos.Count > 0 || entrada.Count > 0) return true; }
                Func<bool> s = HaySucio;
                if (s != null && s()) return true;
                return ProximoReloj() <= AhoraMs;
            }
        }
        // Espera hasta ms o hasta que haya algo (MsgWaitForMultipleObjectsEx con QS_ALLINPUT y MWMO_INPUTAVAILABLE, D3D9Star.cs:2404-2416:
        // vuelve en cuanto hay algo en la cola, aunque ya estuviera; si no, al acabar el plazo). true si lo hay. (Una senal que quedo de
        // antes no la corta: se vuelve a mirar y se sigue esperando lo que falte.)
        internal static bool EsperaPendientes(int ms)
        {
            double hasta = AhoraMs + Math.Max(0, ms);
            while (true)
            {
                if (HayPendientes) return true;
                double falta = Math.Min(hasta, ProximoReloj()) - AhoraMs;
                if (falta <= 0) return HayPendientes;
                senal.WaitOne((int)Math.Ceiling(falta));
            }
        }

        // ---------- las excepciones ----------
        // Lo que lanza un encargo, una tecla, el raton, un pintado o un reloj (Application.ThreadException). Sin nadie, a la salida de errores.
        internal static Action<Exception> AlFallar;
        internal static void Falla(Exception ex)
        {
            Action<Exception> f = AlFallar;
            if (f == null) { Console.Error.WriteLine("[pantalla] " + ex); return; }
            try { f(ex); } catch (Exception otra) { Console.Error.WriteLine("[pantalla] " + otra); }
        }
        internal static void Protege(Action a) { try { a(); } catch (Exception ex) { Falla(ex); } }

        // ---------- una cosa (GetMessage y DispatchMessage) ----------
        private static bool UnaCosa()
        {
            Encargo e = null; Action a = null;
            lock (cerrojo) { if (encargos.Count > 0) e = encargos.Dequeue(); else if (entrada.Count > 0) a = entrada.Dequeue(); }
            if (e != null) { Atiende(e); return true; }
            if (a != null) { Protege(a); return true; }
            Func<bool> p = PintaUno;
            if (p != null)
            {
                bool pinto;
                try { pinto = p(); } catch (Exception ex) { Falla(ex); pinto = true; }
                if (pinto) return true;
            }
            Reloj r = RelojVencido();
            if (r != null)
            {
                if (r.Enabled)
                {
                    if (TrazaVentanas.Activa) TrazaVentanas.Apunta("tick " + r.Nombre + " " + r.Interval.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Protege(r.Vence);
                }
                return true;
            }
            return false;
        }
        private static void Atiende(Encargo e)
        {
            bool perdido = e.Dueno != null && e.Dueno.Vida != e.Vida;
            if (perdido)
            {
                e.Error = new ObjectDisposedException(e.Dueno.GetType().Name);   // su ventana ya no esta: no corre
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("encargo-perdido " + (e.Sitio ?? "?"));
            }
            else
            {
                if (TrazaVentanas.Activa) TrazaVentanas.Apunta("encargo " + (e.Sitio ?? "?"));
                try { e.Metodo(); }
                catch (Exception ex) { e.Error = ex.GetBaseException(); }
            }
            e.Termina();
            if (!perdido && e.Error != null && !e.Espera) Falla(e.Error);
        }

        // ---------- el bucle ----------
        private static volatile bool saliendo;
        internal static bool Saliendo { get { return saliendo; } }
        internal static void Sal() { saliendo = true; senal.Set(); }   // (Application.Exit)
        // Application.Idle: con la cola vacia, una vez (el remitente es el hilo, como FDoIdle).
        internal static event EventHandler Libre;

        // EL BUCLE DE LA ROCOLA (Application.Run): en este hilo, hasta que sigue() de falso o Sal.
        internal static void Corre(Func<bool> sigue)
        {
            if (Hilo == null) Hilo = Thread.CurrentThread;
            EscuchaElApagado();
            Bucle(sigue);
        }
        // EL DE UNA HOJA MODAL (ShowDialog): el mismo bucle, anidado, hasta que sigue() de falso.
        internal static void CorreModal(Func<bool> sigue) { Bucle(sigue); }
        // TODO LO PENDIENTE, SIN LIBRE (DoEvents). Solo en su hilo: DoEvents vacia la cola del hilo que lo llama
        // (ThreadContext.FromCurrent, Application.cs:958-961); otro hilo no tiene ventanas en la rocola (la medida de la duracion de los
        // videos, Form1.Biblioteca.cs:1318, lo llama desde el repaso de la biblioteca para el reproductor de XP: alli no hace nada).
        internal static void Bombea() { if (!EnSuHilo) return; while (UnaCosa()) { } }

        private static void Bucle(Func<bool> sigue)
        {
            if (Hilo == null) Hilo = Thread.CurrentThread;
            bool libreDado = false;
            while (sigue())
            {
                VolcadoEstado.Mira(Principal);   // (pruebas: ROCOLA_ESTADO_EN)
                if (UnaCosa()) { libreDado = false; continue; }
                if (!libreDado) { libreDado = true; DaLibre(); continue; }
                Espera();
            }
        }
        // (sin try: lo que lance sale del bucle, como de FDoIdle)
        private static void DaLibre() { EventHandler h = Libre; if (h != null) h(Thread.CurrentThread, EventArgs.Empty); }
        // WaitMessage: hasta que llegue algo, venza un reloj o toque pintar (y, por si acaso, no mas de 100 ms: despertar sin nada que
        // hacer no da otro Libre)
        private static void Espera()
        {
            if (HayPendientes) return;
            double hasta = Math.Min(ProximoReloj(), AhoraMs + 100);
            Func<double> pp = ProximoPintado;
            if (pp != null) hasta = Math.Min(hasta, pp());
            senal.WaitOne(Math.Max(1, (int)Math.Ceiling(hasta - AhoraMs)));
        }

        // ---------- el apagado del sistema ----------
        // Lo que hace la rocola cuando el sistema se apaga (SIGTERM): en XP, Windows manda WM_QUERYENDSESSION y WM_ENDSESSION a sus
        // ventanas y cada una se cierra con CloseReason.WindowsShutDown. Lo pone quien corre la rocola (Corre).
        internal static Action AlApagar;
        private static PosixSignalRegistration avisoApagado;
        private static void EscuchaElApagado()
        {
            if (avisoApagado != null) return;
            try
            {
                avisoApagado = PosixSignalRegistration.Create(PosixSignal.SIGTERM, delegate (PosixSignalContext c)
                {
                    c.Cancel = true;   // el programa no se corta: la rocola se cierra en su hilo y sale de Corre
                    EncolaDesde(null, delegate { Action a = AlApagar; if (a != null) a(); }, "Apagado", false);
                });
            }
            catch (Exception ex) { Console.Error.WriteLine("[pantalla] sin el aviso de apagado (SIGTERM): " + ex.Message); }
        }
    }
}

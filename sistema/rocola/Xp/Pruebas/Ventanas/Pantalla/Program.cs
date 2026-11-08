// LAS REGLAS DE LA PANTALLA NATIVA (Nativo/PantallaRocola.cs y Reloj.cs), una por una, con los mismos archivos del programa (se
// compilan aqui tal cual; TrazaVentanas y VolcadoEstado van de mentira, abajo). Cada prueba dice lo obtenido y, si falla, lo que
// daria WinForms en XP (la regla y su sitio en la fuente: dotnet/winforms release/3.0, portada de .NET Framework 4.8).
//   dotnet run        (o: dotnet bin/.../PruebasPantalla.dll)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rocola
{
    // (de mentira: en el programa son los ganchos de prueba de Plataforma)
    internal static class TrazaVentanas
    {
        internal static readonly bool Activa = false;
        internal static void Apunta(string que) { }
        internal static string Sitio(string archivo, int linea)
        {
            if (string.IsNullOrEmpty(archivo)) return "?";
            int i = Math.Max(archivo.LastIndexOf('/'), archivo.LastIndexOf('\\'));
            return (i >= 0 ? archivo.Substring(i + 1) : archivo) + ":" + linea;
        }
    }
    internal static class VolcadoEstado { internal static void Mira(object principal) { } }

    internal sealed class Dueno : IDuenoDeEncargos { public long Vida { get; set; } }

    internal static class P
    {
        static int pasa, falla;
        static void Mira(string nombre, string obtenido, string esperado, string regla)
        {
            bool ok = obtenido == esperado;
            if (ok) pasa++; else falla++;
            Console.WriteLine((ok ? "PASA  " : "FALLA ") + nombre + ": " + obtenido + (ok ? "" : "\n          WinForms: " + esperado + " (" + regla + ")"));
        }
        static readonly List<string> log = new List<string>();
        static string Log() { lock (log) { string s = string.Join(",", log); log.Clear(); return s; } }
        static void Apunta(string s) { lock (log) log.Add(s); }
        static void Desde(Action a) { Thread t = new Thread(delegate () { a(); }); t.Start(); t.Join(); }   // desde otro hilo
        static void Duerme(int ms) { Thread.Sleep(ms); }
        // el bucle hasta que pase algo o ms
        static void CorreHasta(Func<bool> listo, int ms)
        {
            Stopwatch t = Stopwatch.StartNew();
            PantallaRocola.Corre(delegate { return !listo() && t.ElapsedMilliseconds < ms; });
        }

        [DllImport("libc")] static extern int getpid();
        [DllImport("libc")] static extern int kill(int pid, int sig);

        static int Main()
        {
            PantallaRocola.Hilo = Thread.CurrentThread;
            Exception fallo = null;
            PantallaRocola.AlFallar = delegate (Exception ex) { fallo = ex; Apunta("falla:" + ex.Message); };

            // 1. EL ORDEN de GetMessage: encargos (posted), entrada (input), pintado (WM_PAINT), relojes (WM_TIMER)
            {
                bool sucio = true;
                PantallaRocola.HaySucio = delegate { return sucio; };
                PantallaRocola.PintaUno = delegate { if (!sucio) return false; sucio = false; Apunta("pinta"); return true; };
                Reloj r = new Reloj(); r.Interval = 10; r.Tick += delegate { Apunta("reloj"); r.Stop(); }; r.Start();
                Duerme(25);
                Desde(delegate { PantallaRocola.Entrada(delegate { Apunta("entrada"); }); PantallaRocola.EncolaDesde(null, delegate { Apunta("encargo"); }, "x", false); });
                PantallaRocola.Bombea();
                r.Stop();
                Mira("orden de una vuelta", Log(), "encargo,entrada,pinta,reloj", "GetMessage: posted, input, WM_PAINT, WM_TIMER");
                PantallaRocola.HaySucio = null; PantallaRocola.PintaUno = null;
            }

            // 2. LIBRE una vez cada vez que se vacia la cola, y otra despues de atender algo (FPushMessageLoop, Application.cs:1838-2000)
            {
                int libres = 0;
                EventHandler h = delegate (object s, EventArgs e)
                {
                    libres++; Apunta("libre" + libres + (s == Thread.CurrentThread ? "(hilo)" : "(otro)"));
                    if (libres == 1) PantallaRocola.EncolaDesde(null, delegate { Apunta("encargo"); }, "x", false);
                };
                PantallaRocola.Libre += h;
                CorreHasta(delegate { return false; }, 400);   // (despues del segundo, nada nuevo en 400 ms: no hay otro Libre)
                PantallaRocola.Libre -= h;
                Mira("Libre una vez por vaciado", Log() + " libres=" + libres, "libre1(hilo),encargo,libre2(hilo) libres=2", "FDoIdle con la cola vacia y WaitMessage; el remitente es el hilo");
            }

            // 3. BOMBEA (DoEvents) atiende todo sin Libre (Application.cs:1960-1969)
            {
                int libres = 0; EventHandler h = delegate { libres++; };
                PantallaRocola.Libre += h;
                PantallaRocola.EncolaDesde(null, delegate { Apunta("a"); PantallaRocola.EncolaDesde(null, delegate { Apunta("b"); }, "x", false); }, "x", false);
                PantallaRocola.Bombea();
                PantallaRocola.Libre -= h;
                Mira("Bombea sin Libre", Log() + " libres=" + libres, "a,b libres=0", "DoEvents sale con la cola vacia antes de FDoIdle");
            }

            // 3b. BOMBEA DESDE OTRO HILO no atiende la cola de la pantalla (DoEvents es de la cola del hilo que lo llama)
            {
                PantallaRocola.EncolaDesde(null, delegate { Apunta("encargo:" + (Thread.CurrentThread == PantallaRocola.Hilo ? "su-hilo" : "otro-hilo")); }, "x", false);
                Desde(delegate { PantallaRocola.Bombea(); });
                string antes = Log();
                PantallaRocola.Bombea();
                Mira("Bombea desde otro hilo no toca la cola de la pantalla", "otro:" + antes + " su:" + Log(), "otro: su:encargo:su-hilo", "DoEvents: ThreadContext.FromCurrent, Application.cs:958-961");
            }

            // 4. RELOJES (Timer.cs y WM_TIMER)
            {
                Reloj r = new Reloj();
                Mira("intervalo de fabrica", r.Interval.ToString(), "100", "Timer.cs: _interval = 100");
                string e = "";
                try { r.Interval = 0; } catch (ArgumentOutOfRangeException) { e = "ArgumentOutOfRange"; }
                Mira("Interval = 0 lanza", e, "ArgumentOutOfRange", "Timer.cs:146-151");
                // minimo 10 ms (USER_TIMER_MINIMUM)
                int n = 0; r.Interval = 1; r.Tick += delegate { n++; }; r.Start();
                Stopwatch t = Stopwatch.StartNew();
                CorreHasta(delegate { return false; }, 300);
                r.Stop();
                Mira("minimo de 10 ms: ticks en 300 ms entre 20 y 31", (n >= 20 && n <= 31) ? "si" : "no (" + n + ")", "si", "USER_TIMER_MINIMUM = 10 ms (XP SP2)");
                // vencimientos juntados: uno solo, y el siguiente en su sitio
                Reloj q = new Reloj(); q.Interval = 50; List<double> cuando = new List<double>();
                Stopwatch t0 = Stopwatch.StartNew();
                q.Tick += delegate { cuando.Add(t0.Elapsed.TotalMilliseconds); if (cuando.Count == 1) Thread.Sleep(170); };
                q.Start();
                CorreHasta(delegate { return cuando.Count >= 3; }, 1000);
                q.Stop();
                string pat = cuando.Count >= 3 ? ((cuando[1] - cuando[0] >= 165 && cuando[1] - cuando[0] < 200) ? "junto" : "no-junto:" + (int)(cuando[1] - cuando[0])) + "," + ((cuando[2] >= 245 && cuando[2] <= 275) ? "en-su-sitio" : "corrido:" + (int)cuando[2]) : "pocos";
                Mira("tick de 170 ms con un reloj de 50: uno solo y el siguiente a los 250", pat, "junto,en-su-sitio", "Windows no acumula WM_TIMER; plazo desde el vencimiento anterior");
                // cambiar Interval con el reloj en marcha lo empieza otra vez; el mismo valor no
                Reloj w = new Reloj(); w.Interval = 100; double tick = -1; Stopwatch tw = Stopwatch.StartNew();
                w.Tick += delegate { if (tick < 0) tick = tw.Elapsed.TotalMilliseconds; };
                w.Start(); Duerme(60); w.Interval = 100;   // el mismo: sigue a los 100
                CorreHasta(delegate { return tick >= 0; }, 500);
                w.Stop();
                Mira("Interval igual no reinicia: vence a los ~100", (tick >= 95 && tick < 140) ? "si" : "no (" + (int)tick + ")", "si", "Timer.cs:153: if (_interval != value)");
                tick = -1; tw.Restart(); w.Interval = 100; w.Start(); Duerme(60); w.Interval = 101;   // otro: desde ahora
                CorreHasta(delegate { return tick >= 0; }, 500);
                w.Stop();
                Mira("Interval distinto reinicia: vence a los ~161", (tick >= 155 && tick < 200) ? "si" : "no (" + (int)tick + ")", "si", "RestartTimer: KillTimer y SetTimer (Timer.cs:153-163, 255-259)");
                // remitente
                object rem = null; Reloj z = new Reloj("z", "el timer"); z.Interval = 10; z.Tick += delegate (object s, EventArgs a) { rem = s; z.Stop(); }; z.Start();
                CorreHasta(delegate { return rem != null; }, 500);
                Mira("Tick con su remitente", rem as string, "el timer", "Timer.cs:172, OnTick => _onTimer?.Invoke(this, e)");
            }

            // 5. LAS EXCEPCIONES
            {
                fallo = null;
                PantallaRocola.EncolaDesde(null, delegate { throw new Exception("fuera", new InvalidOperationException("dentro")); }, "x", false);
                PantallaRocola.Bombea();
                Mira("encargo que lanza: ThreadException con GetBaseException", fallo == null ? "nada" : fallo.GetType().Name + ":" + fallo.Message, "InvalidOperationException:dentro", "InvokeMarshaledCallbacks, Control.cs:6800-6823");
                fallo = null;
                PantallaRocola.Encargo en = PantallaRocola.EncolaDesde(null, delegate { throw new ApplicationException("sincrono"); }, "x", true);
                PantallaRocola.Bombea();
                Mira("Invoke que lanza: al que espera, sin ThreadException", (fallo == null ? "sin-ThreadException" : "con") + "," + (en.Error == null ? "nada" : en.Error.Message), "sin-ThreadException,sincrono", "Control.cs:6813-6823: solo si !_synchronous");
                fallo = null;
                PantallaRocola.Entrada(delegate { throw new FormatException("tecla"); });
                PantallaRocola.Bombea();
                Mira("tecla que lanza: ThreadException", fallo == null ? "nada" : fallo.Message, "tecla", "PreProcessMessage en try/catch, Application.cs:3600-3623");
                fallo = null;
                Reloj r = new Reloj(); r.Interval = 10; r.Tick += delegate { r.Stop(); throw new FormatException("reloj"); }; r.Start();
                CorreHasta(delegate { return fallo != null; }, 500);
                Mira("reloj que lanza: ThreadException y sigue", fallo == null ? "nada" : fallo.Message, "reloj", "NativeWindow.Callback, NativeWindow.cs:484-504");
                fallo = null;
                PantallaRocola.PintaUno = delegate { PantallaRocola.PintaUno = null; throw new FormatException("pinta"); };
                PantallaRocola.Bombea();
                Mira("pintado que lanza: ThreadException", fallo == null ? "nada" : fallo.Message, "pinta", "NativeWindow.Callback");
                // Libre que lanza: sale de Corre (FDoIdle sin try, Application.cs:3869-3873; RunMessageLoopInner solo try/finally)
                EventHandler h = delegate { throw new FormatException("libre"); };
                PantallaRocola.Libre += h;
                string sale = "no salio"; fallo = null;
                try { CorreHasta(delegate { return false; }, 300); } catch (FormatException ex) { sale = "salio:" + ex.Message; }
                PantallaRocola.Libre -= h;
                Mira("Libre que lanza: sale de Corre, sin ThreadException", sale + (fallo == null ? "" : ",ThreadException"), "salio:libre", "FDoIdle (Application.cs:3869-3873) sin try");
                // y de un modal, sube al que lo abrio (que la apunta en su ThreadException si es una tecla)
                h = delegate { throw new FormatException("libre-modal"); };
                fallo = null;
                PantallaRocola.Entrada(delegate
                {
                    PantallaRocola.Libre += h;
                    try { PantallaRocola.CorreModal(delegate { return true; }); } finally { PantallaRocola.Libre -= h; }
                });
                PantallaRocola.Bombea();
                Mira("Libre que lanza en un modal: sube a la tecla que lo abrio", fallo == null ? "nada" : fallo.Message, "libre-modal", "ShowDialog -> RunMessageLoopInner try/finally; la tecla, Application.cs:3600-3623");
            }

            // 6. ENCARGOS CON DUENO: sin ventana lanza; si su ventana cambio, se pierde sin ThreadException
            {
                Log();
                Dueno d = new Dueno(); string e = "";
                try { PantallaRocola.Encola(d, delegate { Apunta("no"); }); } catch (InvalidOperationException) { e = "InvalidOperation"; }
                Mira("encargo sin ventana: lanza", e, "InvalidOperation", "MarshaledInvoke: «Invoke or BeginInvoke cannot be called on a control until the window handle has been created»");
                d.Vida = 7; fallo = null;
                PantallaRocola.Encargo en = PantallaRocola.Encola(d, delegate { Apunta("no-corre"); });
                d.Vida = 8;   // la ventana se destruyo (y otra la sustituyo)
                PantallaRocola.Bombea();
                Mira("encargo de una ventana destruida: no corre", Log() + "|" + (en.Error == null ? "" : en.Error.GetType().Name) + "|" + (fallo == null ? "sin-ThreadException" : "con"), "|ObjectDisposedException|sin-ThreadException", "Control.cs:5255-5276: DestroyHandle los completa con ObjectDisposedException");
                en = PantallaRocola.Encola(d, delegate { Apunta("corre"); });
                PantallaRocola.Bombea();
                Mira("encargo de una ventana viva: corre", Log() + (en.Hecho ? "+hecho" : ""), "corre+hecho", "InvokeMarshaledCallbacks");
            }

            // 7. LO PENDIENTE (PeekMessage) Y LA ESPERA (MsgWaitForMultipleObjects)
            {
                PantallaRocola.Bombea();
                Mira("sin nada: no hay pendientes", PantallaRocola.HayPendientes ? "si" : "no", "no", "PeekMessage PM_NOREMOVE");
                Stopwatch t = Stopwatch.StartNew();
                new Thread(delegate () { Thread.Sleep(40); PantallaRocola.EncolaDesde(null, delegate { }, "x", false); }).Start();
                bool hay = PantallaRocola.EsperaPendientes(1000);
                long ms = t.ElapsedMilliseconds;
                Mira("la espera acaba al llegar un encargo", hay && ms >= 30 && ms < 500 ? "si" : "no (" + hay + ", " + ms + " ms)", "si", "MsgWaitForMultipleObjectsEx(QS_ALLINPUT)");
                PantallaRocola.Bombea();
                t.Restart(); hay = PantallaRocola.EsperaPendientes(60); ms = t.ElapsedMilliseconds;
                Mira("sin nada: espera su tiempo y dice que no", !hay && ms >= 55 && ms < 300 ? "si" : "no (" + hay + ", " + ms + " ms)", "si", "WAIT_TIMEOUT");
                Reloj r = new Reloj(); r.Interval = 20; r.Start(); Duerme(30);
                Mira("un reloj vencido es pendiente", PantallaRocola.HayPendientes ? "si" : "no", "si", "WM_TIMER cuenta para PeekMessage");
                r.Stop(); PantallaRocola.Bombea();
            }

            // 8. EL MODAL: dentro siguen los relojes, los encargos y Libre
            {
                int ticks = 0, libres = 0, encargos = 0;
                Reloj r = new Reloj(); r.Interval = 20; r.Tick += delegate { ticks++; }; r.Start();
                EventHandler h = delegate { libres++; };
                PantallaRocola.Entrada(delegate
                {
                    PantallaRocola.Libre += h;
                    new Thread(delegate () { for (int i = 0; i < 3; i++) { Thread.Sleep(20); PantallaRocola.EncolaDesde(null, delegate { encargos++; }, "x", false); } }).Start();
                    Stopwatch t = Stopwatch.StartNew();
                    PantallaRocola.CorreModal(delegate { return t.ElapsedMilliseconds < 250; });
                    PantallaRocola.Libre -= h;
                });
                PantallaRocola.Bombea();
                r.Stop();
                Mira("en el modal siguen relojes, encargos y Libre", (ticks >= 6 ? "relojes" : "pocos-relojes:" + ticks) + "," + (encargos == 3 ? "encargos" : "encargos:" + encargos) + "," + (libres >= 1 ? "libre" : "sin-libre"), "relojes,encargos,libre", "ShowDialog: RunMessageLoopInner(msoloopModalForm), el mismo bucle");
            }

            // 9. EL APAGADO (SIGTERM): un encargo en el hilo de la pantalla que llama a AlApagar; el programa no se corta
            {
                int hilo = -1; bool cerrado = false;
                PantallaRocola.AlApagar = delegate { hilo = Thread.CurrentThread.ManagedThreadId; cerrado = true; };
                Stopwatch t = Stopwatch.StartNew();
                bool enviado = false;
                PantallaRocola.Corre(delegate
                {
                    if (!enviado && t.ElapsedMilliseconds > 50) { enviado = true; kill(getpid(), 15); }
                    return !cerrado && t.ElapsedMilliseconds < 3000;
                });
                Mira("SIGTERM: AlApagar en el hilo de la pantalla y Corre acaba", (cerrado ? "cerrado" : "no") + "," + (hilo == Thread.CurrentThread.ManagedThreadId ? "su-hilo" : "otro-hilo"), "cerrado,su-hilo", "WM_QUERYENDSESSION/WM_ENDSESSION en el hilo de la ventana");
            }

            Console.WriteLine("pasan " + pasa + ", fallan " + falla);
            return falla == 0 ? 0 : 1;
        }
    }
}

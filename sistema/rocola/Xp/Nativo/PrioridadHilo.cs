// LA PRIORIDAD DE LOS HILOS Y DEL PROGRAMA, EN LINUX (port nativo de XP, 8-11-2026).
//
// En XP la rocola sube el programa a «por encima de lo normal» (Program.PrioridadDeLaRocola: Process.PriorityClass) y le pone a sus hilos una
// prioridad (Thread.Priority: 20 sitios que la ponen y 3 que la leen, en 9 archivos: la salida de sonido en la mas alta, la musica por encima
// de lo normal, las portadas y el repaso por debajo o en la minima). En Windows eso es SetPriorityClass y SetThreadPriority, y GetThreadPriority
// la devuelve. En .NET de Linux Thread.Priority no hace nada (medido: el hilo sigue en el nice del programa), Process.PriorityClass solo cambia
// el hilo principal y, con NativeAOT, Thread.Priority de un hilo que ya corre dice siempre Normal (medido: la salida de sonido quedaba en la
// normal). Aqui el codigo de XP pone y lee la prioridad con Pon y De (apuntada en una tabla propia, igual con JIT y con NativeAOT), y se hace
// con setpriority sobre el hilo (su numero, gettid), que en Linux es por hilo:
//
//   LA CORRESPONDENCIA (a la vista):
//     clase del programa (Windows)        nice del programa     nivel del hilo (Windows)          nice del hilo
//     tiempo real                         -19                    la mas alta (Highest, +2)         el del programa - 10
//     alta                                -11                    por encima (AboveNormal, +1)      el del programa - 5
//     por encima de lo normal (la rocola)  -6                    normal (0)                        el del programa
//     normal                                0                    por debajo (BelowNormal, -1)      el del programa + 5
//     por debajo de lo normal              10                    la minima (Lowest, -2)            el del programa + 10
//     inactiva                             19                    (entre -20 y 19)
//   Con la rocola: la salida de sonido -16, la musica -11, la pantalla -6, las portadas y lo que va por debajo -1, el repaso y lo minimo +4.
//   La clase, como la traduce .NET (la tabla de NiceDe). Los niveles: en Windows el planificador atiende siempre primero la prioridad mas
//   alta; el de Linux (CFS) reparte por pesos, 1,25 veces por punto de nice: 5 puntos por nivel son unas 3 veces el procesador por nivel
//   (10 puntos, unas 9 veces), que acerca ese «primero el de arriba» sin dejar sin nada al de abajo.
//
// Lo que no puede ser igual: en Linux un hilo nuevo hereda el nice del hilo que lo crea (tambien los programas que lanza); en Windows nace en
// la prioridad normal del programa. Por eso TODOS los hilos que crea el codigo de XP (new Thread) se ponen al arrancar la suya (Con o
// AlArrancar: la que XP les puso antes de Start o, si no les puso ninguna, la normal), aunque los cree el hilo de la musica. Los que crea
// .NET para su grupo de hilos (ThreadPool, los temporizadores) y los de las bibliotecas (Mesa, libmpv) heredan la del hilo que los pide: en
// la rocola medida estaban todos en la normal (-6). Subir la prioridad (nice negativo) pide root o CAP_SYS_NICE: la rocola de Rocola OS corre
// como root; si no se puede, el hilo sigue con la que tenia (como Windows, que no falla por esto).
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rocola
{
    internal static class PrioridadHilo
    {
        [DllImport("libc", SetLastError = true)] private static extern int setpriority(int cual, uint quien, int nice);
        [DllImport("libc", EntryPoint = "syscall")] private static extern long Llamada(long numero);
        private const int PrioProceso = 0;   // PRIO_PROCESS: con el numero de un hilo, ese hilo
        private const long Gettid = 186;     // x86-64
        private static volatile int baseNice;   // el nice de la clase del programa (0 hasta que la rocola la pone)

        // El nice de un nivel de hilo (en Windows, la prioridad base de la clase mas el nivel)
        internal static int Nice(ThreadPriority p)
        {
            int d;
            switch (p)
            {
                case ThreadPriority.Highest: d = -10; break;
                case ThreadPriority.AboveNormal: d = -5; break;
                case ThreadPriority.BelowNormal: d = 5; break;
                case ThreadPriority.Lowest: d = 10; break;
                default: d = 0; break;
            }
            return Math.Max(-20, Math.Min(19, baseNice + d));
        }
        // El numero del hilo que llama (gettid)
        internal static uint Este() { return (uint)Llamada(Gettid); }

        // Lo que XP le pidio a cada hilo (en Windows lo guarda el sistema: GetThreadPriority). Sin apuntar, la normal (la de un hilo nuevo).
        private static readonly ConditionalWeakTable<Thread, object> pedidas = new ConditionalWeakTable<Thread, object>();
        // La prioridad de un hilo (en XP, t.Priority)
        internal static ThreadPriority De(Thread t)
        {
            object p;
            return pedidas.TryGetValue(t, out p) ? (ThreadPriority)p : ThreadPriority.Normal;
        }
        // Pone la prioridad a un hilo (en XP, t.Priority = p). Al que llama se le pone ya; a uno que aun no arranca, al arrancar (Con).
        internal static void Pon(Thread t, ThreadPriority p)
        {
            t.Priority = p;   // (lo de .NET, como en XP: si el hilo ya acabo, lanza lo mismo)
            pedidas.AddOrUpdate(t, p);
            if (object.ReferenceEquals(t, Thread.CurrentThread)) { try { setpriority(PrioProceso, Este(), Nice(p)); } catch { } }
        }
        // Al empezar un hilo: la prioridad que XP le puso antes de Start, que en Linux no toma solo.
        internal static void AlArrancar() { try { setpriority(PrioProceso, Este(), Nice(De(Thread.CurrentThread))); } catch { } }
        // El cuerpo de un hilo que empieza con la prioridad que XP le puso antes de Start: en Linux no la toma solo.
        internal static ThreadStart Con(ThreadStart cuerpo)
        {
            return delegate { AlArrancar(); cuerpo(); };
        }
        internal static ParameterizedThreadStart Con(ParameterizedThreadStart cuerpo)
        {
            return delegate (object o) { AlArrancar(); cuerpo(o); };
        }

        // LA CLASE DEL PROGRAMA (en XP, Process.PriorityClass = AboveNormal; SetPriorityClass). En Windows es de TODO el proceso: sube o baja la
        // base de todos sus hilos, los de ahora y los que nazcan, y cada uno conserva su diferencia. Aqui el hilo principal va al nice de la
        // clase, con la misma tabla que .NET (tiempo real -19, alta -11, por encima de lo normal -6, normal 0, por debajo de lo normal 10,
        // inactiva 19), y cada uno de los demas hilos de /proc/self/task se mueve lo mismo que el (los de .NET y los de Mesa, que el anfitrion
        // abre antes del Main de XP; el de la cache de sombreadores de Mesa, que Mesa pone en 19 a proposito, queda en 13); los que nazcan
        // despues lo heredan del hilo que los crea. Si el principal no se puede cambiar, lanza Win32Exception como .NET y XP sigue como estaba.
        internal static int NiceDelHilo(uint tid)   // (campo 19 de /proc/self/task/N/stat; int.MinValue si no se pudo)
        {
            try
            {
                string st = File.ReadAllText("/proc/self/task/" + tid + "/stat");
                string[] p = st.Substring(st.LastIndexOf(')') + 2).Split(' ');
                return int.Parse(p[16]);
            }
            catch { return int.MinValue; }
        }
        internal static int NiceDe(ProcessPriorityClass clase)
        {
            switch (clase)
            {
                case ProcessPriorityClass.RealTime: return -19;
                case ProcessPriorityClass.High: return -11;
                case ProcessPriorityClass.AboveNormal: return -6;
                case ProcessPriorityClass.BelowNormal: return 10;
                case ProcessPriorityClass.Idle: return 19;
                default: return 0;
            }
        }
        internal static void DelProceso(ProcessPriorityClass clase)
        {
            int nice = NiceDe(clase);
            uint yo = (uint)Environment.ProcessId;
            int antes = NiceDelHilo(yo);
            if (setpriority(PrioProceso, yo, nice) != 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
            baseNice = nice;
            if (antes == int.MinValue || antes == nice) return;
            string[] hilos;
            try { hilos = Directory.GetDirectories("/proc/self/task"); } catch { return; }
            foreach (string h in hilos)
            {
                uint tid; int n;
                if (!uint.TryParse(Path.GetFileName(h), out tid) || tid == yo || (n = NiceDelHilo(tid)) == int.MinValue) continue;   // (uno que ya acabo)
                setpriority(PrioProceso, tid, Math.Max(-20, Math.Min(19, n + nice - antes)));
            }
        }
    }
}

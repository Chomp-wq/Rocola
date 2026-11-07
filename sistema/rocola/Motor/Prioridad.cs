// LA PRIORIDAD DE LOS HILOS EN LINUX (Rocola OS). En Linux, Thread.Priority de .NET no cambia nada: el hilo del sonido competia de igual a
// igual con el dibujo y con el repaso de la biblioteca (en QEMU sin aceleracion se oia a trozos). Aqui se le pide al nucleo de verdad:
//  - el sonido, en tiempo real (SCHED_FIFO, prioridad 20; la rocola corre como root). Es seguro: el hilo pasa casi todo el tiempo
//    esperando a la tarjeta (snd_pcm_writei). Si no se puede (sin permiso), al menos nice -15;
//  - el repaso (medir la biblioteca en segundo plano), con nice 19: solo usa lo que sobra, como la prioridad baja de XP.
// Se aplica al hilo que llama (en Linux, pid 0 en estas llamadas es el hilo). En otros sistemas no hace nada.
using System;
using System.Runtime.InteropServices;

namespace Rocola
{
    public static class Prioridad
    {
        [StructLayout(LayoutKind.Sequential)] private struct SchedParam { public int Prioridad; }
        [DllImport("libc", SetLastError = true)] private static extern int sched_setscheduler(int pid, int politica, ref SchedParam p);
        [DllImport("libc", SetLastError = true)] private static extern int setpriority(int cual, int quien, int prio);
        private const int SCHED_FIFO = 1, PRIO_PROCESS = 0;

        // Devuelve como quedo ("tiempo real", "nice -15" o "normal"), para el registro.
        internal static string Sonido()
        {
            if (!OperatingSystem.IsLinux()) return "normal";
            try
            {
                SchedParam p = new SchedParam { Prioridad = 20 };
                if (sched_setscheduler(0, SCHED_FIFO, ref p) == 0) return "tiempo real";
                if (setpriority(PRIO_PROCESS, 0, -15) == 0) return "nice -15";
            }
            catch { }
            return "normal";
        }
        // Lo de fondo que alguien esta esperando (las portadas que se ven): nice 5, detras de la pantalla pero antes que el repaso (19).
        public static void Media()
        {
            if (!OperatingSystem.IsLinux()) return;
            try { setpriority(PRIO_PROCESS, 0, 5); } catch { }
        }
        public static void Fondo()
        {
            if (!OperatingSystem.IsLinux()) return;
            try { setpriority(PRIO_PROCESS, 0, 19); } catch { }
        }
    }
}

using System;
using System.Diagnostics;
namespace Rocola
{
    // Presupuestos de trabajo, no de calidad: nunca cambia pixeles ni contenido.
    internal static class CargaTrabajo
    {
        internal static readonly int Procesadores = DetectarProcesadores();
        private static int DetectarProcesadores()
        {
            int count = Math.Max(1, Environment.ProcessorCount);
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    long raw = process.ProcessorAffinity.ToInt64();
                    ulong mask = IntPtr.Size == 4 ? (ulong)unchecked((uint)raw) : unchecked((ulong)raw);
                    int available = 0;
                    while (mask != 0) { available++; mask &= mask - 1; }
                    if (available > 0) count = Math.Min(count, available);
                }
            }
            catch { } // plataformas sin afinidad: conservar el recuento del runtime
            return count;
        }
        internal static int Lectores(int procesadores)
        { return procesadores >= 12 ? Math.Min(8, procesadores / 2) : Math.Max(1, Math.Min(4, procesadores - 1)); }
        internal static long LimiteTicks(int procesadores)
        { return Math.Max(1, (long)(Stopwatch.Frequency * (procesadores <= 2 ? .0015 : .003))); }
        internal static long PresupuestoGpu(int procesadores, int fps, double trabajoMs)
        {
            double baseMs = procesadores <= 2 ? 1.5 : 3.0;
            if (double.IsNaN(trabajoMs) || double.IsInfinity(trabajoMs) || trabajoMs <= 0)
                return LimiteTicks(procesadores);
            // Solo gastar una fraccion del margen real del cuadro anterior. No reduce calidad.
            double margen = 1000.0 / Math.Max(24, Math.Min(400, fps)) * .8 - trabajoMs;
            double ms = Math.Max(.5, Math.Min(procesadores <= 2 ? baseMs : 6.0, margen * .5));
            return Math.Max(1, (long)(Stopwatch.Frequency * ms / 1000.0));
        }
        internal static bool PuedeEmpezar(long transcurrido, long coste, long limite, bool primero)
        {
            // Una pieza visible debe poder progresar incluso si tarda mas que el presupuesto.
            if (primero) return true;
            return transcurrido >= 0 && transcurrido < limite &&
                Math.Max(1, coste) <= limite - transcurrido;
        }
        internal static long AprendeCoste(long anterior, long observado)
        { return Math.Max(Math.Max(1, observado), anterior - anterior / 8); }
    }
}

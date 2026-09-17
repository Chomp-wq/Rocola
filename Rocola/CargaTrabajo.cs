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
        { return Math.Max(1, Math.Min(4, procesadores - 1)); }
        internal static long LimiteTicks(int procesadores)
        { return Math.Max(1, (long)(Stopwatch.Frequency * (procesadores <= 2 ? .0015 : .003))); }
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

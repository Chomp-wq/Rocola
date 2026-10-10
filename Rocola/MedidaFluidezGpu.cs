// Diagnostico de fluidez GPU: memoria fija, sin ordenar ni escribir archivos por cuadro.
#if ROCOLA_GPU
using System;
using System.Globalization;
namespace Rocola
{
    internal sealed class FluidezGpu
    {
        internal const int Capacidad = 512;
        private readonly double[] intervalos = new double[Capacidad], ordenados = new double[Capacidad];
        private readonly double[] preparar = new double[Capacidad], dibujar = new double[Capacidad], presentar = new double[Capacidad];
        private int pos, n;
        private double anterior = -1, informe = -1e9;
        internal double P95, P99, Maximo, PrepararMax, DibujarMax, PresentarMax;
        internal int Version;
        internal void Reinicia()
        { pos = n = 0; anterior = -1; informe = -1e9; P95 = P99 = Maximo = PrepararMax = DibujarMax = PresentarMax = 0; Version++; }
        internal void Toma(double inicio, double prep, double dibujo, double present)
        {
            if (anterior >= 0 && inicio >= anterior)
            {
                intervalos[pos] = inicio - anterior;
                preparar[pos] = Math.Max(0, prep); dibujar[pos] = Math.Max(0, dibujo); presentar[pos] = Math.Max(0, present);
                pos = (pos + 1) % Capacidad; n = Math.Min(Capacidad, n + 1);
            }
            anterior = inicio;
        }
        internal bool Resume(double ahora)
        {
            if (n == 0 || ahora - informe < 1000) return false;
            informe = ahora; Array.Copy(intervalos, ordenados, n); Array.Sort(ordenados, 0, n);
            P95 = ordenados[Math.Min(n - 1, (int)Math.Ceiling(n * .95) - 1)];
            P99 = ordenados[Math.Min(n - 1, (int)Math.Ceiling(n * .99) - 1)]; Maximo = ordenados[n - 1];
            PrepararMax = DibujarMax = PresentarMax = 0;
            for (int i = 0; i < n; i++)
            { PrepararMax = Math.Max(PrepararMax, preparar[i]); DibujarMax = Math.Max(DibujarMax, dibujar[i]); PresentarMax = Math.Max(PresentarMax, presentar[i]); }
            Version++; return true;
        }
        internal string Resumen()
        {
            return "fluidez GPU: p95=" + Numero(P95) + " p99=" + Numero(P99) + " max=" + Numero(Maximo)
                + " ms; preparar/dibujar/presentar max=" + Numero(PrepararMax) + "/" + Numero(DibujarMax) + "/" + Numero(PresentarMax)
                + " ms; cuadros=" + n;
        }
        internal static string Numero(double n) { return n.ToString("0.0", CultureInfo.InvariantCulture); }
    }
}
#endif

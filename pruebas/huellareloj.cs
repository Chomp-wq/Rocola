// El reloj de las huellas de cuadro (solo para pruebas). pruebas/huella-compila.sh sustituye en una COPIA de las fuentes
// Environment.TickCount, Stopwatch.GetTimestamp(), DateTime.Now/UtcNow y los Random sin semilla por los de aqui, y compila
// esta clase junto con ellas. La rocola de verdad no lleva nada de esto: el ejecutable que se entrega no se instrumenta.
// Con el reloj quieto entre cuadro y cuadro, dos ejecutables con el mismo dibujo reciben exactamente los mismos vertices.
namespace Rocola
{
    internal static class HuellaReloj
    {
        public static int Tick = 1000000;
        public static long Ticks = 5000000000L;
        public static System.DateTime Ahora = new System.DateTime(2026, 10, 19, 22, 30, 0);
        public static int Semilla = 12345;
        public static System.Random NuevoRandom() { return new System.Random(Semilla++); }
        public static void Pon(double ms)
        {
            Tick = 1000000 + (int)ms;
            Ticks = 5000000000L + (long)(ms * System.Diagnostics.Stopwatch.Frequency / 1000.0);
        }
    }
}

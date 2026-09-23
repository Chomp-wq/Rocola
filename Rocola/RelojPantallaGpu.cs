// EL RELOJ DE LA PANTALLA: un cuadro por refresco, y lo que se mueve colocado para cuando se va a VER (solo tarjeta
// grafica).
//
// POR QUE. El panel dibujaba un 10 % mas de cuadros de los que refresca la pantalla (66 por segundo en una de 60 Hz),
// cada uno con lo que se mueve colocado para el momento de DIBUJARLO. Windows ensena en cada refresco el ultimo cuadro
// terminado: con 66 para 60, cada ~10 refrescos uno no llega a verse y en ese refresco todo aparece avanzado el doble.
// Unos seis tirones por segundo, que en algo grande y parejo como la estrella se notan mucho.
//
// Colocar cada cuadro para el refresco siguiente, sin mas, no basta: a 66 por segundo los cuadros empiezan en cualquier
// punto entre dos refrescos, y cual llega a verse depende de lo que tarde en dibujarse. Si tarda mas de ~2 ms, unos se
// ven en su refresco y otros en el anterior (simulado en la bateria 'estrella3d': con 4 ms, peor que antes).
//
// LO QUE SE HACE. El compositor de Windows (DwmGetCompositionTimingInfo) dice cada cuanto refresca la pantalla y cuando
// fue un refresco. Con eso el bucle (GpuPanel.OnIdle) empieza cada cuadro poco despues de un refresco ('Tras'), uno por
// refresco, y la estrella se coloca para el refresco siguiente, que es cuando se vera. Cada cuadro tiene casi todo el
// refresco para dibujarse y cada refresco ensena lo mismo de avance (simulado: sin tirones de 0,5 a 12 ms por cuadro,
// a 60, 75 y 144 Hz). De paso se dibujan 60 cuadros por segundo en vez de 66.
//
// Sin compositor (Windows XP, o Vista y 7 con el tema basico), o con un tope de cuadros puesto a mano que no cae en un
// numero entero de refrescos (45 en una pantalla de 60 Hz), todo sigue como siempre.
#if ROCOLA_GPU
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Rocola
{
    internal static class RelojPantalla
    {
        [StructLayout(LayoutKind.Explicit, Size = 292)]
        private struct Tiempos
        {
            [FieldOffset(0)] public uint Tamano;
            [FieldOffset(12)] public ulong Periodo;     // qpcRefreshPeriod
            [FieldOffset(28)] public ulong Refresco;    // qpcVBlank
        }
        [DllImport("dwmapi.dll")] private static extern int DwmGetCompositionTimingInfo(IntPtr hwnd, ref Tiempos t);
        private static bool sinCompositor;              // no hay (XP) o fallo del todo: ya no se pregunta
        private static long periodo, refresco;          // lo ultimo que dijo Windows, en ticks del reloj fino (QPC)
        private static double preguntadoMs = -1e9;
        internal static Func<long[]> Prueba = null;     // solo las pruebas: {periodo, refresco} de mentira

        // La rejilla de refrescos en el reloj de la rocola (GpuPanel.AhoraMs, que es 'ahoraMs'): un refresco y el
        // periodo, en ms. A Windows se le pregunta como mucho cuatro veces por segundo. Falso si no se sabe.
        internal static bool Rejilla(double ahoraMs, out double refrescoMs, out double periodoMs)
        {
            refrescoMs = 0; periodoMs = 0;
            if (!Stopwatch.IsHighResolution) return false;
            long per, refr;
            if (Prueba != null) { long[] p = Prueba(); per = p[0]; refr = p[1]; }
            else
            {
                if (sinCompositor) return false;
                if (ahoraMs - preguntadoMs >= 250.0)
                {
                    preguntadoMs = ahoraMs;
                    try
                    {
                        Tiempos t = new Tiempos(); t.Tamano = 292;
                        if (DwmGetCompositionTimingInfo(IntPtr.Zero, ref t) == 0) { periodo = (long)t.Periodo; refresco = (long)t.Refresco; }
                        else periodo = 0;   // compositor apagado (tema basico): se vuelve a probar en 250 ms
                    }
                    catch { sinCompositor = true; return false; }   // sin dwmapi.dll (XP)
                }
                per = periodo; refr = refresco;
            }
            return EnMs(ahoraMs, Stopwatch.GetTimestamp(), per, refr, Stopwatch.Frequency, out refrescoMs, out periodoMs);
        }

        // La cuenta sola (la usan las pruebas): la rejilla en ticks del reloj fino de Windows ('ahora' es el tick que
        // corresponde a 'ahoraMs'), pasada a ms del reloj de la rocola. Un periodo absurdo (fuera de 20 a 500 Hz) no vale.
        internal static bool EnMs(double ahoraMs, long ahora, long periodo, long refresco, long frecuencia,
            out double refrescoMs, out double periodoMs)
        {
            refrescoMs = 0; periodoMs = 0;
            if (periodo <= 0 || frecuencia <= 0) return false;
            double hz = (double)frecuencia / periodo;
            if (!(hz >= 20.0 && hz <= 500.0)) return false;
            periodoMs = periodo * 1000.0 / frecuencia;
            refrescoMs = ahoraMs + (refresco - ahora) * 1000.0 / frecuencia;
            return true;
        }

        // Cuanto despues de un refresco empieza cada cuadro: lo justo para que el compositor ya se haya llevado el
        // anterior, y el resto del refresco para dibujarlo.
        internal static double Tras(double periodoMs) { return Math.Min(2.0, .12 * periodoMs); }

        // El primer refresco DESPUES de 't': cuando se vera un cuadro que empieza a dibujarse en 't'.
        internal static double Despues(double t, double refrescoMs, double periodoMs)
        {
            double v = refrescoMs + (Math.Floor((t - refrescoMs) / periodoMs) + 1) * periodoMs;
            return v > t ? v : v + periodoMs;
        }

        // Cuando empezar el cuadro que sigue al que empieza en 'ahoraMs': 'Tras' despues de un refresco, uno de cada n
        // refrescos (n = el tope en refrescos; con el automatico, que va un 10 % por encima de la pantalla, uno por
        // refresco). NaN si el tope no cae cerca de un numero entero de refrescos: ese tope se respeta como siempre.
        internal static double Siguiente(double ahoraMs, int topeMs, double refrescoMs, double periodoMs)
        {
            double k = topeMs / periodoMs;
            int n = (int)Math.Round(k); if (n < 1) n = 1;
            if (k > n * 1.15 || (n > 1 && k < n * .85)) return double.NaN;
            double tras = Tras(periodoMs);
            // El hueco de este cuadro: el ultimo 'refresco + tras' que no queda por delante, con 1 ms de margen (el bucle
            // se adelanta hasta 0,6 ms al despertar).
            double hueco = refrescoMs + Math.Floor((ahoraMs + 1.0 - tras - refrescoMs) / periodoMs) * periodoMs + tras;
            return hueco + n * periodoMs;
        }
    }
}
#endif

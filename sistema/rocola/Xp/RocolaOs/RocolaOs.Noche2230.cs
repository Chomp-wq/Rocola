// EL MODO NOCHE DESDE LAS 22:30 (decision del dueno del 22-11-2026: «Haz que el modo noche empiece a actuar desde las 10:30 y termine de
// actuar a las 11:30»). La noche de XP empieza en una hora entera (noche_desde=, 22 de fabrica) y baja en una hora (NocheBajadaMs): con
// Rocola OS empieza ademas a los minutos de noche_desde_min= de rocola_os.txt (30 de fabrica, de 0 a 59), asi que la bajada va de las
// 22:30 a las 23:30. El final (noche_hasta=, las 7) y la madrugada (NV4, desde la 1) no cambian. Gancho g235 (Codigo/Form1.Sonido.cs,
// EsDeNoche). Sin Rocola OS, null: XP tal cual.
using System;

namespace Rocola
{
    public partial class Form1
    {
        internal const string RocolaOsNocheMinClave = "noche_desde_min";
        internal const int RocolaOsNocheMinFabrica = 30;
        private static int rocolaOsNocheMin = -1;       // (se lee una vez del archivo; 0 a 59)
        private static int rocolaOsNocheMinLeidoMs;

        internal static bool? RocolaOsNocheEnRango(DateTime t, int desdeHora, int hastaHora)
        {
            if (!RocolaOs.Activo) return null;
            int ahora = Environment.TickCount;
            if (rocolaOsNocheMin < 0 || unchecked(ahora - rocolaOsNocheMinLeidoMs) > 60000)   // (se relee cada minuto: un cambio con «ajuste» vale sin reabrir)
            {
                int m = RocolaOsNocheMinFabrica;
                try { string v = RocolaOs.Ajuste(RocolaOsNocheMinClave); int x; if (v != null && int.TryParse(v.Trim(), out x)) m = x; } catch { }   // (clave sin declarar: lo del archivo o null)
                if (m < 0 || m > 59) m = RocolaOsNocheMinFabrica;
                rocolaOsNocheMin = m; rocolaOsNocheMinLeidoMs = ahora;
            }
            int a = desdeHora * 60 + rocolaOsNocheMin, b = hastaHora * 60, h = t.Hour * 60 + t.Minute;
            if (a == b) return false;
            if (a < b) return h >= a && h < b;
            return h >= a || h < b;   // cruza la medianoche
        }
    }
}

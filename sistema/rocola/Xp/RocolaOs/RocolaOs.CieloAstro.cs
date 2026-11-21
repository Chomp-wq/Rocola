// EL CIELO VIVO DE ROCOLA OS, NUCLEO (PLAN_CIELO_VIVO.md §3; FASE11_cielo-nucleo.md): EL SOL, LA LUNA Y LAS CIUDADES. Solo cuentas, nada de
// dibujo ni de disco. Traido de sistema/rocola/Motor/CieloClima.cs (el programa anterior: Astro.Sol, Astro.Luna, las 41 ciudades), con su
// nombre propio (RocolaOsAstro) para no chocar con nada de XP. Cuentas de bolsillo (unas decimas de grado): de sobra para un cielo dibujado.
// La hora del amanecer y del anochecer se comprueba en la bateria (Pruebas/RocolaOs/cielo) contra el algoritmo de la NOAA (astral) a +-3 min.
using System;

namespace Rocola
{
    internal static class RocolaOsAstro
    {
        private const double G = Math.PI / 180;
        private static double Dias(DateTime utc) { return (utc - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays; }
        private static void AltAz(double ra, double dec, double d, double lat, double lon, out double alt, out double az)
        {
            double lst = ((280.46061837 + 360.98564736629 * d + lon) % 360 + 360) % 360 * G, H = lst - ra, la = lat * G;
            double sa = Math.Sin(dec) * Math.Sin(la) + Math.Cos(dec) * Math.Cos(la) * Math.Cos(H);
            alt = Math.Asin(Math.Max(-1, Math.Min(1, sa))) / G;
            az = (Math.Atan2(-Math.Cos(dec) * Math.Sin(H), Math.Sin(dec) * Math.Cos(la) - Math.Cos(dec) * Math.Sin(la) * Math.Cos(H)) / G + 360) % 360;
        }
        private static double LongitudSol(double d)
        {
            double g = (357.529 + 0.98560028 * d) * G, q = 280.459 + 0.98564736 * d;
            return (q + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g)) * G;
        }
        // La altura y el rumbo del sol (grados; rumbo desde el norte hacia el este). La altura es la del centro del disco, sin la refraccion.
        internal static void Sol(DateTime utc, double lat, double lon, out double alt, out double az)
        {
            double d = Dias(utc), eps = (23.439 - 0.00000036 * d) * G, ls = LongitudSol(d);
            double ra = Math.Atan2(Math.Cos(eps) * Math.Sin(ls), Math.Cos(ls)), dec = Math.Asin(Math.Sin(eps) * Math.Sin(ls));
            AltAz(ra, dec, d, lat, lon, out alt, out az);
        }
        // La de la luna, y su fase (0 nueva, 0,25 cuarto creciente, 0,5 llena, 0,75 cuarto menguante)
        internal static void Luna(DateTime utc, double lat, double lon, out double alt, out double az, out double fase)
        {
            double d = Dias(utc), eps = (23.439 - 0.00000036 * d) * G;
            double L = 218.316 + 13.176396 * d, M = (134.963 + 13.064993 * d) * G, F = (93.272 + 13.229350 * d) * G;
            double lm = (L + 6.289 * Math.Sin(M)) * G, bm = 5.128 * Math.Sin(F) * G;
            double ra = Math.Atan2(Math.Sin(lm) * Math.Cos(eps) - Math.Tan(bm) * Math.Sin(eps), Math.Cos(lm));
            double dec = Math.Asin(Math.Sin(bm) * Math.Cos(eps) + Math.Cos(bm) * Math.Sin(eps) * Math.Sin(lm));
            AltAz(ra, dec, d, lat, lon, out alt, out az);
            double e = (lm - LongitudSol(d)) / G;
            fase = ((e % 360) + 360) % 360 / 360.0;
        }
        // lo iluminado de la luna (0 nueva, 1 llena)
        internal static double Iluminada(double fase) { return 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * fase)); }

        // LA SALIDA O LA PUESTA DEL SOL de un dia (la fecha local de 'dia', en la zona del sistema): el momento (UTC) en que el borde de arriba
        // toca el horizonte (altura del centro -0,833 grados: el radio y la refraccion, como la NOAA). sale = true la de la manana. null si ese
        // dia no sale o no se pone (no pasa en Mexico). Por biseccion sobre la altura: un minuto de precision sobra.
        internal static DateTime? SalidaOPuesta(DateTime diaLocal, double lat, double lon, bool sale)
        {
            DateTime d0 = new DateTime(diaLocal.Year, diaLocal.Month, diaLocal.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
            // el mediodia solar aproximado del dia: donde la altura es mayor (de 10 en 10 minutos)
            double mejor = double.MinValue; int iMejor = 0;
            for (int i = 0; i <= 144; i++)
            {
                double a, z; Sol(d0.AddMinutes(i * 10), lat, lon, out a, out z);
                if (a > mejor) { mejor = a; iMejor = i; }
            }
            const double Borde = -0.833;
            if (mejor < Borde) return null;
            DateTime medio = d0.AddMinutes(iMejor * 10);
            DateTime lejos = sale ? medio.AddHours(-13) : medio.AddHours(13);
            double al, zl; Sol(lejos, lat, lon, out al, out zl);
            if (al > Borde) return null;
            DateTime a0 = sale ? lejos : medio, a1 = sale ? medio : lejos;   // a0: abajo (manana) o arriba (tarde)
            for (int k = 0; k < 40; k++)
            {
                DateTime m = a0.AddTicks((a1 - a0).Ticks / 2);
                double am, zm; Sol(m, lat, lon, out am, out zm);
                bool arriba = am > Borde;
                if (sale) { if (arriba) a1 = m; else a0 = m; }
                else { if (arriba) a0 = m; else a1 = m; }
                if ((a1 - a0).TotalSeconds < 5) break;
            }
            return a0.AddTicks((a1 - a0).Ticks / 2);
        }
    }

    // LAS CIUDADES DEL BAR (ciudad_bar=: 0 automatica por internet; 1 a 41, una de estas). Las mismas que el programa anterior (Motor/CieloClima.cs),
    // en el mismo orden: el numero guardado vale igual. La 0 lleva las coordenadas de la Ciudad de Mexico: es la que vale mientras no se sepa otra.
    internal static class RocolaOsCiudades
    {
        internal static readonly string[] Nombres = { "Automática (por internet)", "Acapulco", "Aguascalientes", "Campeche", "Cancún", "Chetumal", "Chihuahua",
            "Chilpancingo", "Ciudad de México", "Ciudad Juárez", "Ciudad Victoria", "Colima", "Cuernavaca", "Culiacán", "Durango", "Guadalajara",
            "Guanajuato", "Hermosillo", "La Paz", "León", "Mazatlán", "Mérida", "Mexicali", "Monterrey", "Morelia", "Oaxaca", "Pachuca", "Puebla",
            "Querétaro", "Saltillo", "San Luis Potosí", "Tampico", "Tepic", "Tijuana", "Tlaxcala", "Toluca", "Torreón", "Tuxtla Gutiérrez",
            "Veracruz", "Villahermosa", "Xalapa", "Zacatecas" };
        internal static readonly double[] Lat = { 19.43, 16.85, 21.88, 19.85, 21.16, 18.50, 28.63, 17.55, 19.43, 31.69, 23.74, 19.24, 18.92, 24.80, 24.02, 20.67,
            21.02, 29.07, 24.14, 21.12, 23.25, 20.97, 32.62, 25.69, 19.70, 17.07, 20.10, 19.04, 20.59, 25.42, 22.15, 22.25, 21.50, 32.51, 19.32, 19.29,
            25.54, 16.75, 19.17, 17.99, 19.54, 22.77 };
        internal static readonly double[] Lon = { -99.13, -99.88, -102.29, -90.53, -86.85, -88.30, -106.07, -99.50, -99.13, -106.42, -99.15, -103.72, -99.22,
            -107.39, -104.66, -103.35, -101.26, -110.96, -110.31, -101.68, -106.41, -89.62, -115.45, -100.32, -101.19, -96.72, -98.75, -98.20, -100.39,
            -101.00, -100.98, -97.86, -104.89, -117.04, -98.24, -99.66, -103.41, -93.12, -96.13, -92.93, -96.91, -102.58 };
        internal const int Maxima = 41;   // la ultima de la lista (ciudad_bar= va de 0 a 41)
    }
}

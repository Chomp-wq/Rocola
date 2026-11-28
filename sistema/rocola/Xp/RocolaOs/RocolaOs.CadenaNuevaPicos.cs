// CUANTO SUBE EL PICO REAL CON EL ECUALIZADOR DEL DUENO (cadena de sonido nueva; arreglo a1 del 28-11-2026, CADENA_NUEVA_A.md «Arreglos»).
// La ganancia de la cadena nueva es fija y no hay limitador: el pico real de lo que sale (TP') se estima antes de sonar. Hasta el 28-11-2026 se
// le sumaba a lo medido LO MAXIMO que sube la curva del dueno: con +6 dB en 63 Hz, 6 dB de pico en un bolero (al que de verdad le sube 0,14) y en
// un master aplastado (5,85): las dinamicas quedaban hasta 6 dB mas bajas. Y lo maximo tampoco es una cota: con dos bandas vecinas subidas, el
// pico de un master aplastado sube MAS que la curva (+4 en 31 y +4 en 63: la curva sube 4,2 y el pico 4,5).
// AHORA, dos cotas y la menor:
//   1) LA DE LAS SONDAS (con la medida de la cancion): la curva del dueno es la cascada de sus diez bandas; lo que una banda le SUMA a la musica
//      al subirla G veces (en amplitud) es (G - 1) por lo que deja pasar su sonda (RocolaOsSondasEq: el limite de (H - 1) / (G - 1) cuando la
//      subida tiende a 0, que es la sonda mas ancha: con mas subida la banda se estrecha y pasa menos), asi que, por la desigualdad del
//      triangulo, TP' <= TP + suma (G_b - 1) * P_b, con P_b el pico de la sonda b medido en la cancion entera (RocolaOsMedidor). Un bolero sin
//      graves tiene P_63 pequeno: casi no sube; un master aplastado lo tiene casi en el pico: sube casi todo.
//   2) LA DE LA RESPUESTA AL IMPULSO de la curva entera: |y| <= suma |h| * |x| (tambien entre muestras): rigurosa para cualquier cancion, y
//      cubre la fase de las bandas vecinas (donde lo maximo de la curva se quedaba corto).
//   3) Y LO MAXIMO DE LA CURVA CON 1,5 dB DE MARGEN (MargenMaximoDb): con una curva ancha (todo subido) la 1 y la 2 se pasan de largo (la 1 suma
//      las diez bandas; la 2, la fase de los estantes); lo mas que el pico paso a lo maximo de la curva en lo medido fue 0,9 dB.
// Sin la medida de la cancion (Spotify la primera vez, una sin medir, o medida antes del 28-11-2026 sin las sondas): los picos de las sondas
// TIPICOS de la biblioteca (la mediana de lo medido, relativo a su pico real) en la cota 1; sin nada medido, la 2 sola.
// Lo mismo para la sonoridad sin espectro: el espectro TIPICO de la biblioteca (la mediana de los tercios medidos), y sin nada medido el de un
// ruido rosa (lo mismo en cada tercio), nunca lo maximo de la curva.
// Si el dueno mueve el ecualizador mientras suena una cancion, la pieza del EQ vuelve a estimar con lo suyo y, si con la curva nueva el pico
// pasaria el techo, la ganancia de esa cancion baja lo que haga falta (RocolaOsGananciaPieza.TopeLin; nunca sube de su ganancia fija): bajar lo que
// el dueno sube es accion del dueno.
using System;
using System.Collections.Generic;
using System.Threading;

namespace Rocola
{
    // LAS SONDAS de las diez bandas del ecualizador del dueno (EcualizadorDueno.Hz; las campanas de Q 1,41 y los estantes de Q 0,7071), en
    // biquads normalizados (b0 b1 b2 a1 a2). Campana: el paso banda de Q 1,41 con 0 dB en el centro (H - 1 de la campana RBJ, por (G - 1), cuando G
    // tiende a 1). Estante de graves: (1 + s / (2Q)) / (s^2 + s/Q + 1) (1 en la continua, el mismo limite); el de agudos, su espejo (1 arriba).
    // Por bilineal con la frecuencia del centro conservada.
    internal static class RocolaOsSondasEq
    {
        internal static void Coef(int banda, int tasa, double[] c, int o)
        {
            double w0 = 2.0 * Math.PI * EcualizadorDueno.Hz[banda] / tasa;
            if (banda > 0 && banda < EcualizadorDueno.Bandas - 1)
            {
                double al = Math.Sin(w0) / (2.0 * EcualizadorDueno.Q), a0 = 1.0 + al;
                c[o] = al / a0; c[o + 1] = 0.0; c[o + 2] = -al / a0; c[o + 3] = -2.0 * Math.Cos(w0) / a0; c[o + 4] = (1.0 - al) / a0;
                return;
            }
            double q = EcualizadorDueno.QEstante, K = 1.0 / Math.Tan(w0 / 2.0), h = 1.0 / (2.0 * q);
            double d0 = K * K + K / q + 1.0, d1 = 2.0 - 2.0 * K * K, d2 = K * K - K / q + 1.0;
            double n0, n1, n2;
            if (banda == 0) { n0 = 1.0 + h * K; n1 = 2.0; n2 = 1.0 - h * K; }
            else { n0 = K * K + h * K; n1 = -2.0 * K * K; n2 = K * K - h * K; }
            c[o] = n0 / d0; c[o + 1] = n1 / d0; c[o + 2] = n2 / d0; c[o + 3] = d1 / d0; c[o + 4] = d2 / d0;
        }
    }

    internal static class RocolaOsPicosEq
    {
        // el pico de muestra de una sonda de 8 y 16 kHz puede quedar debajo de su pico entre muestras: un margen para esas dos
        internal static readonly double[] MargenSonda = { 1, 1, 1, 1, 1, 1, 1, 1, 1.1, 1.25 };
        // lo maximo que sube la curva no es una cota (ver arriba), pero con este margen si lo fue en todo lo medido (la que mas, 0,9 dB): con las
        // curvas anchas (todo subido) las dos cotas se pasan de largo y esta es la que manda
        internal const double MargenMaximoDb = 1.5;

        // LA SUMA DE |h| de la curva del dueno (sin preganancia), con su respuesta al impulso (hasta que la cola ya no suma, o 2 s). 1 sin curva.
        internal static double L1(int[] medios, int tasa)
        {
            int nb = 0;
            double[] c = new double[EcualizadorDueno.Bandas * 5];
            int[] cuales = new int[EcualizadorDueno.Bandas];
            for (int b = 0; b < EcualizadorDueno.Bandas; b++)
            {
                int m = medios != null && b < medios.Length ? EcualizadorDueno.Acota(medios[b]) : 0;
                if (m == 0 || !EcualizadorDueno.Vale(b, tasa)) continue;
                EcualizadorDueno.Coef(b, m * 0.5, tasa, c, nb * 5); cuales[nb++] = b;
            }
            if (nb == 0) return 1.0;
            double[] st = new double[nb * 4];
            double suma = 0, cola = 0; int tope = 2 * Math.Max(8000, tasa);
            for (int n = 0; n < tope; n++)
            {
                double v = n == 0 ? 1.0 : 0.0;
                for (int k = 0; k < nb; k++)
                {
                    int o = k * 5, e = k * 4;
                    double y = c[o] * v + c[o + 1] * st[e] + c[o + 2] * st[e + 1] - c[o + 3] * st[e + 2] - c[o + 4] * st[e + 3];
                    st[e + 1] = st[e]; st[e] = v; st[e + 3] = st[e + 2]; st[e + 2] = y;
                    v = y;
                }
                double a = Math.Abs(v); suma += a; cola += a;
                if ((n & 1023) == 1023) { if (n > 4096 && cola < 1e-9 * suma) break; cola = 0; }
            }
            return Math.Max(1.0, suma);
        }

        // CUANTO SUBE (dB, 0 o mas) EL PICO REAL de una cancion con pico real tpDb con la curva 'medios' (medios de dB). 'picos': los de sus sondas
        // (dBFS, medidos con su pico real tpDb) o null; 'tipicos': los de la biblioteca, relativos a su pico real (dB), o null. Puro.
        internal static double SubeDb(int[] medios, int tasa, double tpDb, float[] picos, float[] tipicos)
        {
            bool sube = false;
            if (medios != null) for (int b = 0; b < EcualizadorDueno.Bandas && b < medios.Length; b++) if (medios[b] > 0 && EcualizadorDueno.Vale(b, tasa)) { sube = true; break; }
            if (!sube) return 0.0;   // (solo bajar: como hasta hoy, no se le suma nada)
            double l1 = Math.Min(20.0 * Math.Log10(L1(medios, tasa)), -EcualizadorDueno.PreganDb(medios, tasa) + MargenMaximoDb);
            double t = Math.Pow(10.0, tpDb / 20.0);
            bool conPicos = picos != null && picos.Length >= EcualizadorDueno.Bandas, conTipicos = !conPicos && tipicos != null && tipicos.Length >= EcualizadorDueno.Bandas;
            if (!(t > 0) || (!conPicos && !conTipicos)) return Math.Max(0.0, l1);
            double s = t;
            for (int b = 0; b < EcualizadorDueno.Bandas && b < medios.Length; b++)
            {
                int m = EcualizadorDueno.Acota(medios[b]);
                if (m <= 0 || !EcualizadorDueno.Vale(b, tasa)) continue;
                double p = conPicos ? Math.Pow(10.0, picos[b] / 20.0) : t * Math.Pow(10.0, tipicos[b] / 20.0);
                s += (Math.Pow(10.0, m * 0.5 / 20.0) - 1.0) * p * MargenSonda[b];
            }
            double cota = 20.0 * Math.Log10(s / t);
            return Math.Max(0.0, Math.Min(cota, l1));
        }

        // LO TIPICO DE LA BIBLIOTECA con esa cadena: la mediana de los tercios (dB, relativos al total) y de los picos de las sondas relativos al
        // pico real de cada cancion (dB). null si no hay medidas (con 3 o mas). Se rehace cuando cambia la tabla (como mucho cada 30 s).
        private sealed class Tip { internal int Version = -1; internal long Ms; internal float[] Tercios, Picos; }
        private static readonly Tip[] tipicos = { new Tip(), new Tip() };
        internal const int TipicoMinimo = 3;
        internal static void Tipico(char cadena, out float[] tercios, out float[] picos)
        {
            Tip t = tipicos[cadena == 'B' ? 1 : 0];
            lock (t)
            {
                long ahora = Environment.TickCount64;
                int v = Volatile.Read(ref RocolaOsMedidaTabla.Version);
                if (t.Version != v && (t.Version < 0 || ahora - t.Ms >= 30000))
                {
                    List<float>[] lt = new List<float>[RocolaOsMedidaNueva.Bandas], lp = new List<float>[EcualizadorDueno.Bandas];
                    for (int b = 0; b < lt.Length; b++) lt[b] = new List<float>();
                    for (int b = 0; b < lp.Length; b++) lp[b] = new List<float>();
                    foreach (RocolaOsMedidaNueva m in RocolaOsMedidaTabla.Todas(cadena))
                    {
                        if (m.Tercios != null && m.Tercios.Length == lt.Length) for (int b = 0; b < lt.Length; b++) lt[b].Add(m.Tercios[b]);
                        if (m.PicosEq != null && m.PicosEq.Length == lp.Length && m.TpDb > -150) for (int b = 0; b < lp.Length; b++) lp[b].Add((float)(m.PicosEq[b] - m.TpDb));
                    }
                    t.Tercios = Mediana(lt); t.Picos = Mediana(lp); t.Version = v; t.Ms = ahora;
                }
                tercios = t.Tercios; picos = t.Picos;
            }
        }
        private static float[] Mediana(List<float>[] l)
        {
            if (l[0].Count < TipicoMinimo) return null;
            float[] r = new float[l.Length];
            for (int b = 0; b < l.Length; b++) { List<float> x = l[b]; x.Sort(); int n = x.Count; r[b] = n % 2 == 1 ? x[n / 2] : 0.5f * (x[n / 2 - 1] + x[n / 2]); }
            return r;
        }
        // el espectro de un ruido rosa en tercios (lo mismo en cada uno), relativo al total: lo ultimo sin nada medido
        internal static readonly float[] Rosa = RosaDe();
        private static float[] RosaDe() { float[] r = new float[RocolaOsMedidaNueva.Bandas]; for (int b = 0; b < r.Length; b++) r[b] = (float)(-10.0 * Math.Log10(r.Length)); return r; }
    }
}

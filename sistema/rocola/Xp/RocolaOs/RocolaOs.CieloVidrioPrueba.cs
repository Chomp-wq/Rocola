// EL CIELO VIVO DE ROCOLA OS, VIDRIO: LA ORDEN DE PRUEBA DE LAS CUENTAS (Pruebas/RocolaOs/cielo-vidrio/prueba.sh). Sin la rocola: la geometria
// del contorno, el alfa de los rellenos (con la idea apagada y encendida) y la regla 3 (siempre se lee) como cuenta: la luminancia del vidrio
// sobre el fondo mas claro posible (todo blanco) y el contraste de los textos de XP que van encima.
//   rocola-xp --prueba-vidrio
using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal static class RocolaOsVidrioPrueba
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsOrdenes.Si("--prueba-vidrio", Prueba); }

        private static int malas, buenas;
        private static void Si(string que, bool bien) { Console.WriteLine((bien ? "BIEN  " : "MAL   ") + que); if (bien) buenas++; else malas++; }
        private static string F(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        internal static int Prueba(string[] a)
        {
            // ---- el contorno: 4 esquinas x (SegEsquina + 1) puntos, dentro del rectangulo, y 4 puntos sin esquina redonda
            float[] px = new float[RocolaOsVidrio.MaxPuntos], py = new float[RocolaOsVidrio.MaxPuntos];
            int n = RocolaOsVidrio.Contorno(10f, 20f, 200f, 100f, 16f, px, py);
            bool dentro = true; for (int i = 0; i < n; i++) if (px[i] < 9.99f || px[i] > 210.01f || py[i] < 19.99f || py[i] > 120.01f) dentro = false;
            Si("el contorno redondo: " + n + " puntos (4 x " + (RocolaOsVidrio.SegEsquina + 1) + ") dentro del rectangulo", n == 4 * (RocolaOsVidrio.SegEsquina + 1) && dentro);
            Si("el contorno redondo empieza arriba a la izquierda (esquina 0 en (10, 36))", Math.Abs(px[0] - 10f) < 0.01f && Math.Abs(py[0] - 36f) < 0.01f);
            Si("el contorno redondo toca los cuatro lados (x 10 y 210, y 20 y 120)", Min(px, n) < 10.01f && Max(px, n) > 209.99f && Min(py, n) < 20.01f && Max(py, n) > 119.99f);
            int m = RocolaOsVidrio.Contorno(10f, 20f, 200f, 100f, 0f, px, py);
            Si("sin esquina redonda: 4 puntos (los de la esquina)", m == 4 && px[0] == 10f && py[0] == 20f && px[2] == 210f && py[2] == 120f);
            int p = RocolaOsVidrio.Contorno(0f, 0f, 120f, 40f, 400f, px, py);   // pastilla: el radio se topa a la mitad del alto
            Si("una pastilla (radio mayor que el alto): se topa a la mitad del alto", p == n && Math.Abs(py[0] - 20f) < 0.01f);
            int d1 = RocolaOsVidrio.Contorno(0f, 0f, 100f, 50f, 14f, px, py), d2 = RocolaOsVidrio.Contorno(1.2f, 1.2f, 97.6f, 47.6f, 12.8f, px, py);
            Si("el contorno de dentro (el del filo de luz) tiene los mismos puntos que el de fuera", d1 == d2);

            // ---- el alfa de los rellenos: sin ROCOLA_OS (o sin la foto del cielo) el mismo de XP
            bool igual = RocolaOsVidrio.Alfa(RocolaOsVidrio.Ficha, 232) == 232 && RocolaOsVidrio.Alfa(RocolaOsVidrio.Franja, 248) == 248
                && RocolaOsVidrio.Alfa(RocolaOsVidrio.Lista, 205) == 205 && RocolaOsVidrio.Alfa(RocolaOsVidrio.Buscador, 244) == 244
                && RocolaOsVidrio.Alfa(RocolaOsVidrio.Pastillas, 100) == 100 && RocolaOsVidrio.Alfa(RocolaOsVidrio.Luz, 100) == 100;
            Si("sin la foto del cielo (el vidrio apagado) el alfa de cada relleno es el de XP", igual);
            Si("Con(pieza, color) sin vidrio da el mismo color", RocolaOsVidrio.Con(RocolaOsVidrio.Buscador, System.Drawing.Color.FromArgb(238, 40, 165, 90)).ToArgb() == System.Drawing.Color.FromArgb(238, 40, 165, 90).ToArgb());
            Console.WriteLine("DATO FactorAlfa " + F(RocolaOsVidrio.FactorAlfa) + " BrilloFondo " + F(RocolaOsVidrio.BrilloFondo) + " CadaMs " + F(RocolaOsVidrio.CadaMs));
            Console.WriteLine("DATO alfa de la ficha " + (int)(232 * RocolaOsVidrio.FactorAlfa + 0.5f) + ", de la franja " + (int)(232 * RocolaOsVidrio.FactorAlfa + 0.5f) + "/" + (int)(248 * RocolaOsVidrio.FactorAlfa + 0.5f)
                + ", de la lista " + (int)(205 * RocolaOsVidrio.FactorAlfa + 0.5f) + "/" + (int)(218 * RocolaOsVidrio.FactorAlfa + 0.5f) + ", del aviso " + (int)(244 * RocolaOsVidrio.FactorAlfa + 0.5f));

            // ---- la regla 3 como cuenta: el relleno de cada pieza sobre el fondo MAS CLARO posible (blanco entero) y sobre un cielo de dia (0,17 de luminancia)
            // texto blanco de la ficha (255) y su gris azulado mas tenue (160, 178, 222); el contraste que pide la regla es 4,5:1 al blanco
            double lw = Lum1(255, 255, 255), lg = Lum1(160, 178, 222);
            object[][] piezas =
            {
                new object[] { "la ficha", 12, 18, 42, 232 }, new object[] { "la franja (arriba)", 12, 19, 42, 232 }, new object[] { "la franja (abajo)", 7, 12, 29, 248 },
                new object[] { "la lista (arriba)", 26, 37, 59, 205 }, new object[] { "la lista (abajo)", 9, 16, 31, 218 }, new object[] { "el aviso de novedad", 14, 28, 60, 244 },
                new object[] { "el aviso de exito", 14, 52, 34, 244 },
            };
            foreach (object[] pz in piezas)
            {
                string que = (string)pz[0]; int r = (int)pz[1], g = (int)pz[2], b = (int)pz[3], al = (int)pz[4];
                double lXp = RocolaOsVidrio.LuminanciaVidrio(1.0, r, g, b, al, false), lVi = RocolaOsVidrio.LuminanciaVidrio(1.0, r, g, b, al, true);
                double cw = RocolaOsVidrio.Contraste(lw, lVi), cg = RocolaOsVidrio.Contraste(lg, lVi);
                Console.WriteLine("DATO " + que + " sobre blanco entero: luminancia XP " + F(lXp) + " vidrio " + F(lVi) + "; contraste del blanco " + F(RocolaOsVidrio.Contraste(lw, lXp)) + " -> " + F(cw) + ", del gris azulado " + F(RocolaOsVidrio.Contraste(lg, lXp)) + " -> " + F(cg));
                Si(que + ": el blanco de la letra a 4,5:1 o mas aun sobre un fondo todo blanco (" + F(cw) + ")", cw >= 4.5);
            }
            // lo que la regla 3 mide del cielo: 0,17 de luminancia detras (un gris de sRGB 0,45) como fondo, con el desenfoque a 0,78
            double fondoCielo = 0.45;
            double lFicha = RocolaOsVidrio.LuminanciaVidrio(fondoCielo, 12, 18, 42, 232, true);
            Si("la ficha sobre un cielo claro (gris 0,45): luminancia " + F(lFicha) + " (la regla pide 0,17 o menos)", lFicha <= 0.17);
            Console.WriteLine();
            Console.WriteLine(malas == 0 ? "TODO BIEN (" + buenas + ")" : malas + " MAL de " + (buenas + malas));
            return malas == 0 ? 0 : 1;
        }
        private static double Lum1(int r, int g, int b) { return RocolaOsVidrio.Lum(r / 255.0, g / 255.0, b / 255.0); }
        private static float Min(float[] v, int n) { float m = float.MaxValue; for (int i = 0; i < n; i++) m = Math.Min(m, v[i]); return m; }
        private static float Max(float[] v, int n) { float m = float.MinValue; for (int i = 0; i < n; i++) m = Math.Max(m, v[i]); return m; }
    }
}

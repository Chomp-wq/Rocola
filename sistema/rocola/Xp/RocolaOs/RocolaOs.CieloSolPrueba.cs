// EL CIELO VIVO DE ROCOLA OS, GRUPO «SOL, LUNA Y ESTRELLAS»: LA ORDEN DE PRUEBA DE LAS CUENTAS (Pruebas/RocolaOs/cielo/prueba-sol.sh, parte «orden»).
// Sin la rocola entera (solo las cuentas puras de DN2, DN3 y DN4: la proyeccion, el recorte de las mallas, los umbrales de las estrellas); con la pantalla
// fuera (ROCOLA_PANTALLA=fuera:...: las ordenes de RocolaOs/ corren despues de abrirla). Una linea por comprobacion (BIEN o MAL); sale con 0 si todo bien.
//   rocola-xp --prueba-cielo-sol
using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal static class RocolaOsCieloSolPrueba
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsOrdenes.Si("--prueba-cielo-sol", Prueba); }

        private static int malas, buenas;
        private static void Si(string que, bool bien) { Console.WriteLine((bien ? "BIEN  " : "MAL   ") + que); if (bien) buenas++; else malas++; }

        internal static int Prueba(string[] a)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            // ---- la proyeccion: el sol de Mexico en noviembre (lat 19,43; sale hacia el 110, culmina a 53 al sur, se pone hacia el 250) ----
            const int W = 1024; const float hz = 667f;
            float x, y;
            RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(0, 110), 0, W, hz, out x, out y);
            Si("el sol al salir (alt 0, az 110): a la izquierda (x " + x.ToString("0", ci) + ") y en el horizonte (y " + y.ToString("0", ci) + ")", x < W * 0.25f && Math.Abs(y - hz) < 0.01f);
            RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(0, 250), 0, W, hz, out x, out y);
            Si("el sol al ponerse (alt 0, az 250): a la derecha (x " + x.ToString("0", ci) + ")", x > W * 0.75f);
            RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(53, 180), 53, W, hz, out x, out y);
            Si("el sol a mediodia (alt 53, az 180): al centro (x " + x.ToString("0", ci) + ") y alto (y " + y.ToString("0", ci) + ")", Math.Abs(x - W / 2) < 2f && y < hz * 0.5f);
            // sin saltos por el cenit ni por el norte: el sol de verano al norte del cenit (az 5, alt 80 y 85 y az 355) sigue cerca del centro
            float x1, y1, x2, y2;
            RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(85, 5), 85, W, hz, out x1, out y1);
            RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(85, 355), 85, W, hz, out x2, out y2);
            Si("sin saltos por el norte: az 5 y az 355 a 85 grados quedan a menos de 25 px (" + Math.Abs(x1 - x2).ToString("0.0", ci) + ")", Math.Abs(x1 - x2) < 25f && Math.Abs(y1 - y2) < 1f);
            RocolaOsCieloAstros.Proyecta(0, 90, W, hz, out x, out y);
            Si("el cenit esta a 0,12 del horizonte (y " + y.ToString("0.0", ci) + ")", Math.Abs(y - hz * 0.12f) < 0.5f);
            RocolaOsCieloAstros.Proyecta(0, -3, W, hz, out x, out y);
            Si("bajo el horizonte, mas abajo (y " + y.ToString("0.0", ci) + ")", y > hz);
            // la y crece de forma continua al bajar la altura (sin saltos): de 40 a -5 grados
            bool mono = true; float ant = -1f;
            for (double al = 40; al >= -5; al -= 0.25) { RocolaOsCieloAstros.Proyecta(0.3, al, W, hz, out x, out y); if (y < ant - 1e-3f) mono = false; ant = y; }
            Si("la y crece sin saltos al bajar el astro (de 40 a -5 grados)", mono);
            Si("Paso: 0 antes, 1 despues, 0,5 en medio", RocolaOsCieloAstros.Paso(-9, -6, -0.5) == 0f && RocolaOsCieloAstros.Paso(3, -6, -0.5) == 1f && Math.Abs(RocolaOsCieloAstros.Paso(-3.25, -6, -0.5) - 0.5f) < 1e-3f);

            // ---- las mallas con recorte: ningun vertice sale de la franja; el halo tiene todos sus triangulos sin recorte ----
            int n0 = RocolaOsCieloAstros.ArmaHalo(500, 600, 100, 0xFFC070, 0.5f, 2f, -2e9f, 2e9f);
            Si("el halo sin recorte: " + (n0 / 3) + " triangulos (28 segmentos: 28 + 28 x 7 x 2 = 420)", n0 / 3 == 420);
            int n1 = RocolaOsCieloAstros.ArmaHalo(500, 660, 100, 0xFFC070, 0.5f, 2f, 651f, 667f);
            bool dentro = n1 > 0; Tarjeta.Vtx[] m = RocolaOsCieloAstros.Malla;
            for (int i = 0; i < n1; i++) if (m[i].Y < 651f - 1e-3f || m[i].Y > 667f + 1e-3f) dentro = false;
            Si("el halo recortado a la franja 651-667: " + (n1 / 3) + " triangulos, todos los vertices dentro", dentro);
            int n2 = RocolaOsCieloAstros.ArmaDisco(500, 667, 21, 0xFFF6DC, 1f, -2e9f, 667f);
            bool abajoOk = n2 > 0; m = RocolaOsCieloAstros.Malla;
            for (int i = 0; i < n2; i++) if (m[i].Y > 667f + 1e-3f) abajoOk = false;
            Si("el disco cortado por el horizonte (centro en la raya): " + (n2 / 3) + " triangulos, nada bajo la raya", abajoOk);
            int n3 = RocolaOsCieloAstros.ArmaDisco(500, 700, 21, 0xFFF6DC, 1f, -2e9f, 667f);
            Si("el disco bajo el horizonte (centro 33 px abajo): no queda nada", n3 == 0);
            int n4 = RocolaOsCieloAstros.ArmaDisco(500, 300, 21, 0xFFF6DC, 1f, 651f, 667f);
            Si("el disco lejos de la franja (la pared, el sol alto): no queda nada", n4 == 0);
            // el alfa de los vertices recortados sigue bajando hacia el borde (interpolado): el del centro es el mayor
            RocolaOsCieloAstros.ArmaHalo(500, 660, 100, 0xFFC070, 0.5f, 2f, 651f, 667f); m = RocolaOsCieloAstros.Malla;
            uint amax = 0; for (int i = 0; i < n1; i++) amax = Math.Max(amax, m[i].Color >> 24);
            Si("el alfa mayor del halo es la de su centro (0,5 = " + amax + " de 255)", amax >= 125 && amax <= 128);

            // ---- DN4: los umbrales de las estrellas ----
            bool rango = true, uno = true, cero = true; int encendidas = 0, total = 0, grandesAntes = 0, polvoAntes = 0, ng = 0, np = 0;
            for (int i = 0; i < 4000; i++)
                for (int k = 0; k < 3; k++)
                {
                    float y0 = (i * 37 % 768);
                    float t = RocolaOsCieloEstrellasDN4.Umbral(i, y0, 768, k);
                    if (t < 0f || t > 0.88f + 1e-6f) rango = false;
                    if (RocolaOsCieloEstrellasDN4.Brillo(1f, t) != 1f) uno = false;     // la noche cerrada: todas a 1 exacto (regla 1)
                    if (RocolaOsCieloEstrellasDN4.Brillo(0f, t) != 0f) cero = false;    // de dia: ninguna
                    total++; if (RocolaOsCieloEstrellasDN4.Brillo(0.5f, t) >= 1f) encendidas++;
                    if (k == 2) { ng++; if (t < 0.3f) grandesAntes++; } if (k == 0) { np++; if (t < 0.3f) polvoAntes++; }
                }
            Si("los umbrales entre 0 y 0,88", rango);
            Si("con p = 1 todas las estrellas valen exactamente 1 (la noche de XP, regla 1)", uno);
            Si("con p = 0 ninguna se ve", cero);
            double fr = encendidas / (double)total;
            Si("con p = 0,5, entre el 20 y el 80 % encendidas del todo (" + (fr * 100).ToString("0", ci) + " %)", fr > 0.2 && fr < 0.8);
            Si("las grandes se encienden antes que el polvo (con umbral < 0,3: " + (100.0 * grandesAntes / ng).ToString("0", ci) + " % de las grandes, " + (100.0 * polvoAntes / np).ToString("0", ci) + " % del polvo)", grandesAntes / (double)ng > 0.8 && polvoAntes / (double)np < 0.2);
            bool monoE = true;
            for (int i = 0; i < 500; i++) { float t = RocolaOsCieloEstrellasDN4.Umbral(i, 300f, 768, i % 3), ant2 = -1f; for (float p = 0f; p <= 1f; p += 0.01f) { float b = RocolaOsCieloEstrellasDN4.Brillo(p, t); if (b < ant2 - 1e-6f) monoE = false; ant2 = b; } }
            Si("el brillo de cada estrella crece con la luz sin retrocesos", monoE);
            // la luz de estrellas por la altura del sol (las paletas de §5): crece al bajar el sol de +3 a -12 por la tarde y baja por la manana
            float[] ps = new float[RocolaOsCieloDia.Fases]; RocolaOsCieloFoto f = new RocolaOsCieloFoto(); bool creceTarde = true; float antP = -1f;
            for (double al = 5; al >= -13; al -= 0.5)
            {
                RocolaOsCieloDia.Pesos(al, false, f.Pesos); RocolaOsCieloDia.Mezcla(f);
                if (f.Estrellas < antP - 1e-5f) creceTarde = false; antP = f.Estrellas;
            }
            Si("la luz de estrellas crece al bajar el sol de +5 a -13 grados (0 de dia, 1 con la noche cerrada: " + antP.ToString("0.00", ci) + ")", creceTarde && antP == 1f);
            RocolaOsCieloDia.Pesos(30, false, f.Pesos); RocolaOsCieloDia.Mezcla(f);
            Si("de dia (sol a 30 grados) la luz de estrellas es 0", f.Estrellas == 0f);

            Console.WriteLine((malas == 0 ? "TODO BIEN (" : "HAY " + malas + " MAL de ") + (buenas + malas) + " comprobaciones)");
            return malas == 0 ? 0 : 1;
        }
    }
}

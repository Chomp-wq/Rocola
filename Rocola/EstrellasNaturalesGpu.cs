#if ROCOLA_GPU
using System;

namespace Rocola
{
    // ESTRELLAS Y FUGACES DE ESPACIO NATURAL (ideas 12 a 19 de la presentacion del 11-10-2026).
    //
    // Mismo patron, misma cantidad, mismas formas y la misma llamada de dibujo que Legacy: aqui solo cambia
    // el acabado de cada una. Todo son cuentas por estrella dentro del bucle que ya existe (lecturas de la
    // tabla del titileo, sin senos), sin reservar memoria. Legacy no pasa por aqui.
    internal sealed partial class GpuStarfield
    {
        // 12) El ojo solo ve color en las estrellas brillantes: las debiles se ven casi blancas. Con el
        // brillo de cada estrella (el alfa de su color, de 0.26 a 1) se elige cuanto color conserva.
        private static readonly int[] SatNatural = HazSatNatural();
        private static int[] HazSatNatural()
        {
            int[] t = new int[256];
            for (int a = 0; a < 256; a++)
            {
                float m = (a / 255f - .26f) / .74f; if (m < 0f) m = 0f; else if (m > 1f) m = 1f;
                float k = m * m * (3f - 2f * m);
                t[a] = (int)(256f * (.18f + 1.17f * k) + .5f);   // de casi blanca a un poco mas viva que hoy
            }
            return t;
        }

        // 13) Dos ritmos mezclados: la onda deja de ser regular. Es una lectura mas de la misma tabla.
        internal static float CentelleoNatural(float primero, double segundo)
        { return .62f * primero + .38f * Titileo(segundo + 1.1); }

        internal static uint ColorEstrellaNatural(uint col, int kind, float altura, double t, float fase)
        {
            int sat = SatNatural[(col >> 24) & 255];
            int r = (int)((col >> 16) & 255), g = (int)((col >> 8) & 255), b = (int)(col & 255);
            r = 255 - ((255 - r) * sat >> 8); g = 255 - ((255 - g) * sat >> 8); b = 255 - ((255 - b) * sat >> 8);
            // 14) Las grandes que estan bajas cambian de color al centellear, como Sirio cerca del horizonte:
            // el aire separa los colores. Solo las grandes (unas pocas decenas), de la mitad hacia abajo.
            if (kind == 2 && altura > .5f)
            {
                float w = (altura - .5f) / .3f; if (w > 1f) w = 1f;
                w = w * w * (3f - 2f * w) * .55f;
                r = Destella(r, Titileo(t * 7.1 + fase), w);
                g = Destella(g, Titileo(t * 9.3 + fase + 2.0), w);
                b = Destella(b, Titileo(t * 11.7 + fase + 4.0), w);
            }
            if (r < 0) r = 0; else if (r > 255) r = 255;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            if (b < 0) b = 0; else if (b > 255) b = 255;
            return (col & 0xFF000000u) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }
        private static int Destella(int canal, float onda, float peso)
        {
            // onda va de 0.45 a 1: se centra y se escala para que el canal suba o baje hasta un 40 %.
            float m = 1f + peso * ((onda - .725f) / .275f) * .72f;
            return (int)(canal * m);
        }

        // 19) LLUVIAS DE METEOROS. En las fechas de las lluvias de verdad, parte de las fugaces sale de un
        // mismo punto del cielo (el radiante), mas cuanto mas cerca del maximo. El resto son las de siempre.
        // Cuantas fugaces hay y cada cuanto no cambia: solo de donde salen.
        // Cuadrantidas, Liridas, Eta Acuaridas, Perseidas, Orionidas, Leonidas y Geminidas.
        // Dia del ano contado desde 0 (1 de enero = 0), en un ano sin 29 de febrero.
        private static readonly int[] LluviaIni = { 361, 103, 108, 197, 274, 309, 337 };
        private static readonly int[] LluviaMax = { 2, 111, 125, 223, 293, 320, 347 };
        private static readonly int[] LluviaFin = { 11, 119, 147, 235, 310, 333, 353 };
        private static readonly float[] LluviaX = { .22f, .70f, .84f, .18f, .76f, .30f, .62f };
        private static readonly float[] LluviaY = { .10f, .08f, .16f, .12f, .14f, .09f, .11f };
        internal static float LluviaDeMeteoros(DateTime fecha, out float radX, out float radY)
        {
            radX = .5f; radY = .1f;
            int dia = fecha.DayOfYear - 1;
            if (DateTime.IsLeapYear(fecha.Year) && dia >= 59) dia--;   // 29 de febrero cuenta como el 28
            float mejor = 0f;
            for (int i = 0; i < LluviaMax.Length; i++)
            {
                if (!DentroDeTramo(dia, LluviaIni[i], LluviaFin[i])) continue;
                int d = dia - LluviaMax[i];
                if (d > 182) d -= 365; else if (d < -182) d += 365;
                float k = d / 2.5f, f = .25f + .5f * (float)Math.Exp(-k * k);
                if (f > mejor) { mejor = f; radX = LluviaX[i]; radY = LluviaY[i]; }
            }
            return mejor;
        }
        private static bool DentroDeTramo(int dia, int ini, int fin)
        { return ini <= fin ? dia >= ini && dia <= fin : dia >= ini || dia <= fin; }

        private void ApuntaDesdeRadiante(ref Shoot s, float w, float h)
        {
            float rx, ry;
            float parte = LluviaDeMeteoros(DateTime.Now, out rx, out ry);
            if (!(parte > 0f) || srnd.NextDouble() >= parte) return;
            float vel = (float)Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            double a = .15 * Math.PI + srnd.NextDouble() * .70 * Math.PI;   // siempre hacia abajo, abriendose
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
            float d = (h / 768f) * (30f + (float)srnd.NextDouble() * 130f);
            s.X = w * rx + dx * d; s.Y = h * ry + dy * d;
            s.VX = dx * vel; s.VY = dy * vel;
        }

        // 18) El rastro que deja un bolido: una linea azulada muy tenue que se queda unos instantes y se
        // deshace con el viento. Dos triangulos mas en la misma llamada de las fugaces.
        private bool rastroOn; private float rastroX0, rastroY0, rastroX1, rastroY1; private double rastroMs;
        internal const double RastroVida = 1600;
        private void NotaRastro(Shoot s, double finoMs)
        {
            rastroOn = true; rastroMs = finoMs;
            rastroX1 = s.X; rastroY1 = s.Y;
            rastroX0 = s.X - s.VX * s.Tail * 2.4f; rastroY0 = s.Y - s.VY * s.Tail * 2.4f;
        }
        private int DibujaRastro(D3D9.Vtx[] v, int o, double finoMs)
        {
            if (!rastroOn) return 0;
            double edad = finoMs - rastroMs;
            if (!(edad >= 0) || edad >= RastroVida) { rastroOn = false; return 0; }
            float k = (float)(1 - edad / RastroVida), viento = (float)(edad / 1000.0) * 6f;
            float ex = rastroX1 - rastroX0, ey = rastroY1 - rastroY0, l = (float)Math.Sqrt(ex * ex + ey * ey);
            if (l < 1f) { rastroOn = false; return 0; }
            float px = -ey / l, py = ex / l, ancho = .8f + .9f * (1 - k);
            uint a = (uint)(90f * k * k * dim); if (a > 255) a = 255;
            uint c = (a << 24) | 0x96C8FFu, fuera = 0x0096C8FFu;
            float ox = px * viento, oy = py * viento;
            VShoot(v, o, rastroX0 + ox + px * ancho, rastroY0 + oy + py * ancho, fuera);
            VShoot(v, o + 1, rastroX1 + px * ancho, rastroY1 + py * ancho, c);
            VShoot(v, o + 2, rastroX1 - px * ancho, rastroY1 - py * ancho, c);
            VShoot(v, o + 3, rastroX0 + ox + px * ancho, rastroY0 + oy + py * ancho, fuera);
            VShoot(v, o + 4, rastroX1 - px * ancho, rastroY1 - py * ancho, c);
            VShoot(v, o + 5, rastroX0 + ox - px * ancho, rastroY0 + oy - py * ancho, fuera);
            return 6;
        }

        // 17) La vida de una fugaz de verdad: se enciende, arde y se apaga; los bolidos dan un fogonazo
        // poco antes del final (por encima de su brillo normal). 'vida' va de 1 (recien nacida) a 0.
        internal static float BrilloFugazNatural(float vida, bool bolido)
        {
            if (!(vida > 0f)) return 0f;
            if (vida > 1f) vida = 1f;
            float u = 1f - vida;
            float b = (u < .18f ? u / .18f : 1f) * (1f - Math.Max(0f, (u - .86f) / .14f));
            if (bolido) { float d = (u - .80f) / .05f; b += .45f * (float)Math.Exp(-d * d); }
            return b < 0f ? 0f : b > 1.35f ? 1.35f : b;
        }

        // 33) La luna (LunaRealGpu.cs) la pone el panel; aqui solo se lee para apagar las cercanas.
        public float LunaCx, LunaCy, LunaRadio, LunaLuz;
        private float LuzJuntoALuna(float x, float y)
        {
            if (!(LunaRadio > 0f)) return 1f;
            float dx = x - LunaCx, dy = y - LunaCy, d2 = dx * dx + dy * dy, r = LunaRadio;
            float lejos = r * 7f;
            if (d2 >= lejos * lejos) return 1f;
            float d = (float)Math.Sqrt(d2);
            if (d < r * 1.08f) return 0f;   // detras del disco no se ve ninguna: la luna las tapa
            float f = (d - r * 1.08f) / (lejos - r * 1.08f);
            f = f * f * (3f - 2f * f);
            return 1f - LunaLuz * .80f * (1f - f);
        }
    }
}
#endif

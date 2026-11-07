// EL CIELO SEGUN LA HORA, LA FECHA Y EL CLIMA (decision del 7-11-2026; lo que no es dibujo esta en Motor/CieloClima.cs). Es el cielo de
// siempre, con su estilo dibujado: aqui solo cambian sus colores (las paradas del degradado, las nebulosas, la aurora y el resplandor del
// horizonte) y se le pone encima lo de cada momento: el sol, la luna en su arco, las nubes, la lluvia, la neblina, la nieve, los rayos,
// las fiestas, las lluvias de estrellas, las constelaciones y la medianoche. Todo lo que se mueve es una cuenta del reloj (sin estado
// cuadro a cuadro: el reposo y la ventana tapada dibujan el cielo dos veces y sale igual). Mientras alguien usa la rocola (la viveza de
// ColorVivo.cs) hay menos particulas, sin fugaces, sin constelaciones y los rayos sin destello fuerte.
// Con todo apagado (o de noche, sin clima ni fiesta) el cielo sale como antes: la paleta de noche son las paradas de siempre.
using System;
using Rocola.Pantalla;

namespace Rocola
{
    internal sealed partial class VistaGrafica
    {
        private struct Paleta { public uint Top, S1, Mid, Hor, Bel, Bel2, NebA, NebB; public Color4 AurA, AurB, Glow; }
        private static Paleta Pal(uint top, uint s1, uint mid, uint hor, uint bel, uint bel2, uint na, uint nb, Color4 aa, Color4 ab, Color4 gl)
        { return new Paleta { Top = top, S1 = s1, Mid = mid, Hor = hor, Bel = bel, Bel2 = bel2, NebA = na, NebB = nb, AurA = aa, AurB = ab, Glow = gl }; }
        // la de noche son las paradas de siempre (CieloXp.cs, medidas en la de XP)
        private static readonly Paleta PalNoche = Pal(0x16163A, 0x28285C, 0x2C367D, 0x37408F, 0x223470, 0x16234C, 0x4C8AD8, 0x9A6EE6, new Color4(0.30f, 1f, 0.62f), new Color4(0.36f, 0.86f, 1f), new Color4(0.85f, 0.45f, 0.85f));
        private static readonly Paleta PalMadrugada = Pal(0x0B0B24, 0x171946, 0x1B225C, 0x262F6C, 0x141C40, 0x0C1430, 0x3B5FB0, 0x6A4FC0, new Color4(0.25f, 0.9f, 0.9f), new Color4(0.4f, 0.6f, 1f), new Color4(0.5f, 0.5f, 1f));
        private static readonly Paleta PalAtardecer = Pal(0x1C1C4A, 0x3A2F72, 0x6E4290, 0xE0705A, 0x2A2860, 0x181A42, 0xC070D0, 0xFF7F8F, new Color4(0.95f, 0.5f, 0.85f), new Color4(1f, 0.62f, 0.5f), new Color4(1f, 0.55f, 0.35f));
        private static readonly Paleta PalAmanecer = Pal(0x141A44, 0x2A3070, 0x4A4A8F, 0xC06A8A, 0x262E66, 0x161C48, 0x7A8AD8, 0xC07AB0, new Color4(0.6f, 0.8f, 1f), new Color4(1f, 0.6f, 0.8f), new Color4(1f, 0.6f, 0.72f));
        private static readonly Paleta PalDoradaTarde = Pal(0x2A3F8F, 0x4A63B5, 0x8A86C8, 0xF2A878, 0x3A3F78, 0x22264F, 0xFFC8A0, 0xFF98B8, new Color4(1f, 0.72f, 0.42f), new Color4(1f, 0.55f, 0.62f), new Color4(1f, 0.62f, 0.35f));
        private static readonly Paleta PalDoradaManana = Pal(0x2C4A9A, 0x5A74C0, 0xA08AC0, 0xFFC080, 0x40508A, 0x283466, 0xFFD0A0, 0xFFA0C0, new Color4(1f, 0.85f, 0.6f), new Color4(1f, 0.7f, 0.75f), new Color4(1f, 0.82f, 0.5f));
        private static readonly Paleta PalDia = Pal(0x1E3A8A, 0x3B5FC0, 0x5C84D6, 0x9DBBEA, 0x2C4A9A, 0x1A2F6A, 0xA8C8FF, 0xC8B8FF, new Color4(0.7f, 0.9f, 1f), new Color4(0.85f, 0.85f, 1f), new Color4(1f, 0.95f, 0.8f));
        private Paleta pal = PalNoche;

        private static uint MezclaHex(uint a, uint b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            int r = (int)Math.Round(((a >> 16) & 255) + ((((b >> 16) & 255) - (float)((a >> 16) & 255)) * t)), g = (int)Math.Round(((a >> 8) & 255) + ((((b >> 8) & 255) - (float)((a >> 8) & 255)) * t)), bb = (int)Math.Round((a & 255) + (((b & 255) - (float)(a & 255)) * t));
            return (uint)((r << 16) | (g << 8) | bb);
        }
        private static uint Oscurece(uint a, float f) { return MezclaHex(a, 0, f); }
        private static uint Agrisa(uint a, float t)
        {
            int l = (int)((((a >> 16) & 255) * 0.30f) + (((a >> 8) & 255) * 0.55f) + ((a & 255) * 0.15f));
            uint g = (uint)((l << 16) | (l << 8) | Math.Min(255, l + 12));
            return MezclaHex(a, g, t);
        }
        private static Paleta Mezcla(Paleta a, Paleta b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Pal(MezclaHex(a.Top, b.Top, t), MezclaHex(a.S1, b.S1, t), MezclaHex(a.Mid, b.Mid, t), MezclaHex(a.Hor, b.Hor, t), MezclaHex(a.Bel, b.Bel, t), MezclaHex(a.Bel2, b.Bel2, t),
                MezclaHex(a.NebA, b.NebA, t), MezclaHex(a.NebB, b.NebB, t), Dibujo.Mezcla(a.AurA, b.AurA, t), Dibujo.Mezcla(a.AurB, b.AurB, t), Dibujo.Mezcla(a.Glow, b.Glow, t));
        }
        // un numero de 0 a 1 que siempre es el mismo para (a, b)
        private static float Hs(int a, int b)
        {
            uint x = unchecked((uint)(a * 374761393 + b * 668265263 + 1442695041));
            x = unchecked((x ^ (x >> 13)) * 1274126177u); x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777216f;
        }

        // ---- lo de este cuadro ----
        private DateTime cieloAhora; private double astroT = -100, solAlt = -30, solAz = 250, lunaAlt = 30, lunaAz = 200, lunaFase = 0.3;
        private float oscuridadSol = 1f, verEstrellas = 1f, nebCielo = 1f, auroraCielo = 1f, destelloOnda, relampago; private bool lunaLlena;
        private Fiesta fiesta; private string lluviaNombre; private float lluviaX, lluviaY;
        private Form1.TipoClima clima; private float viento; private Form1 mCielo;

        private void CieloAlDia(Controlador c)
        {
            Form1 m = c.motor; mCielo = m;
            m.ClimaAlDia();
            double t = reloj.Elapsed.TotalSeconds;
            cieloAhora = m.AhoraCielo;
            if (t - astroT > 15)
            {
                astroT = t;
                DateTime utc = cieloAhora.ToUniversalTime();
                Astro.Sol(utc, m.LatBar, m.LonBar, out solAlt, out solAz);
                Astro.Luna(utc, m.LatBar, m.LonBar, out lunaAlt, out lunaAz, out lunaFase);
                lluviaNombre = m.LluviasEstrellas ? CalendarioCielo.LluviaDeEstrellas(cieloAhora, out lluviaX, out lluviaY) : null;
            }
            fiesta = m.FiestasCielo ? CalendarioCielo.De(cieloAhora) : Fiesta.Ninguna;
            clima = m.Clima; viento = m.ClimaViento;
            // cuan de noche es por el sol (0 con el sol a 2 grados bajo el horizonte, 1 a 14)
            float noche = Suave((float)((-2 - solAlt) / 12));
            oscuridadSol = noche;
            // la paleta segun el sol (y de madrugada, mas honda)
            double hh = cieloAhora.Hour + cieloAhora.Minute / 60.0; if (hh < 12) hh += 24;
            float madrugada = Suave((float)((hh - 24.5) / 1.5)) * (1f - Suave((float)(hh - 28.5)));
            bool manana = cieloAhora.Hour < 12;
            Paleta pn = Mezcla(PalNoche, PalMadrugada, madrugada), crep = manana ? PalAmanecer : PalAtardecer, dor = manana ? PalDoradaManana : PalDoradaTarde, ph;
            double s = solAlt;
            if (s >= 10) ph = PalDia;
            else if (s >= 4) ph = Mezcla(dor, PalDia, (float)((s - 4) / 6));
            else if (s >= -2) ph = Mezcla(crep, dor, (float)((s + 2) / 6));
            else if (s >= -9) ph = Mezcla(pn, crep, (float)((s + 9) / 7));
            else ph = pn;
            Paleta p = m.CieloHora ? ph : PalNoche;
            if (m.AuroraHora) { p.AurA = ph.AurA; p.AurB = ph.AurB; } else { p.AurA = PalNoche.AurA; p.AurB = PalNoche.AurB; }
            // el clima: nubes que agrisan, neblina que aclara abajo, tormenta que oscurece
            float nb = 0f, est = 1f;
            switch (clima)
            {
                case Form1.TipoClima.PocoNublado: nb = 0.25f; est = 0.8f; break;
                case Form1.TipoClima.Nublado: nb = 0.65f; est = 0.35f; break;
                case Form1.TipoClima.Neblina: nb = 0.45f; est = 0.25f; break;
                case Form1.TipoClima.Llovizna: nb = 0.6f; est = 0.3f; break;
                case Form1.TipoClima.Lluvia: nb = 0.75f; est = 0.18f; break;
                case Form1.TipoClima.LluviaFuerte: nb = 0.85f; est = 0.12f; break;
                case Form1.TipoClima.Tormenta: nb = 1f; est = 0.12f; break;
                case Form1.TipoClima.Nieve: nb = 0.7f; est = 0.3f; break;
            }
            if (nb > 0f)
            {
                p.Top = Oscurece(Agrisa(p.Top, 0.4f * nb), 0.12f * nb); p.S1 = Oscurece(Agrisa(p.S1, 0.4f * nb), 0.12f * nb); p.Mid = Oscurece(Agrisa(p.Mid, 0.4f * nb), 0.1f * nb);
                p.Hor = Agrisa(p.Hor, 0.35f * nb); p.NebA = Agrisa(p.NebA, 0.5f * nb); p.NebB = Agrisa(p.NebB, 0.5f * nb);
                if (clima == Form1.TipoClima.Neblina) { p.Mid = MezclaHex(p.Mid, 0x6A7298, 0.35f); p.Hor = MezclaHex(p.Hor, 0x8A90B0, 0.45f); p.Bel = MezclaHex(p.Bel, 0x5A6088, 0.4f); }
            }
            // las fiestas tinen el cielo
            if ((fiesta & Fiesta.SanValentin) != 0) { p.Top = MezclaHex(p.Top, 0x6E1E5A, 0.25f); p.S1 = MezclaHex(p.S1, 0xA0306E, 0.2f); p.Mid = MezclaHex(p.Mid, 0xC8508C, 0.18f); p.Hor = MezclaHex(p.Hor, 0xFF7AAA, 0.25f); p.NebA = MezclaHex(p.NebA, 0xFF6EAA, 0.4f); p.NebB = MezclaHex(p.NebB, 0xFF9AC8, 0.4f); p.Glow = new Color4(1f, 0.45f, 0.7f); }
            if ((fiesta & Fiesta.Halloween) != 0) { p.Hor = MezclaHex(p.Hor, 0xFF7A1A, 0.35f); p.NebB = MezclaHex(p.NebB, 0x8A2BE2, 0.4f); p.NebA = MezclaHex(p.NebA, 0xFF8A2A, 0.3f); p.AurA = new Color4(1f, 0.55f, 0.15f); p.AurB = new Color4(0.65f, 0.3f, 1f); }
            else if ((fiesta & Fiesta.Muertos) != 0) { p.Hor = MezclaHex(p.Hor, 0xFF9A1A, 0.28f); p.NebA = MezclaHex(p.NebA, 0xFF9A3A, 0.3f); p.NebB = MezclaHex(p.NebB, 0xE6007E, 0.3f); p.AurA = new Color4(1f, 0.62f, 0.12f); p.AurB = new Color4(0.95f, 0.25f, 0.65f); p.Glow = new Color4(1f, 0.6f, 0.2f); }
            if ((fiesta & (Fiesta.Patrias | Fiesta.Grito)) != 0) { p.AurA = new Color4(0.1f, 0.8f, 0.45f); p.AurB = new Color4(1f, 0.3f, 0.35f); }
            if ((fiesta & Fiesta.Nochebuena) != 0) { p.AurA = new Color4(0.2f, 0.9f, 0.45f); p.AurB = new Color4(1f, 0.32f, 0.35f); p.Glow = new Color4(1f, 0.8f, 0.45f); }
            if ((fiesta & Fiesta.Mujer) != 0) { p.Hor = MezclaHex(p.Hor, 0x9A4FD0, 0.3f); p.NebA = MezclaHex(p.NebA, 0xB070FF, 0.4f); p.NebB = MezclaHex(p.NebB, 0x8A3FC8, 0.4f); p.AurA = new Color4(0.75f, 0.45f, 1f); p.AurB = new Color4(0.95f, 0.55f, 0.95f); }
            if ((fiesta & Fiesta.SanPatricio) != 0) { p.Hor = MezclaHex(p.Hor, 0x2FAA5A, 0.25f); p.NebA = MezclaHex(p.NebA, 0x40D070, 0.4f); p.AurA = new Color4(0.2f, 1f, 0.4f); p.AurB = new Color4(0.6f, 1f, 0.5f); }
            if ((fiesta & Fiesta.DiaDelPadre) != 0) { p.NebA = MezclaHex(p.NebA, 0x3A8AFF, 0.35f); p.AurB = new Color4(0.3f, 0.6f, 1f); }
            if ((fiesta & (Fiesta.CincoDeMayo | Fiesta.Revolucion | Fiesta.Bandera)) != 0) { p.AurA = new Color4(0.1f, 0.8f, 0.45f); p.AurB = new Color4(1f, 0.3f, 0.35f); }
            if ((fiesta & Fiesta.SantaCecilia) != 0) { p.NebB = MezclaHex(p.NebB, 0xFF9AE0, 0.3f); p.AurA = new Color4(0.4f, 1f, 0.8f); p.AurB = new Color4(1f, 0.6f, 0.9f); }
            if ((fiesta & Fiesta.Jacarandas) != 0) { p.Hor = MezclaHex(p.Hor, 0x9B7FD4, 0.2f); p.NebB = MezclaHex(p.NebB, 0xB89BE6, 0.35f); }
            // la luna llena (F3): el cielo un poco mas claro
            lunaLlena = m.LunaLlenaCielo && Math.Abs(lunaFase - 0.5) < 0.035 && (!m.LunaArco || lunaAlt > 0);
            if (lunaLlena) { p.Top = MezclaHex(p.Top, 0x3A4A8A, 0.2f); p.S1 = MezclaHex(p.S1, 0x45559A, 0.18f); p.Mid = MezclaHex(p.Mid, 0x4A5CA8, 0.12f); }
            pal = p;
            verEstrellas = est;
            nebCielo = (0.45f + 0.55f * est) * (m.CieloHora ? 0.35f + 0.65f * noche : 1f);
            auroraCielo = m.AuroraHora ? 0.3f + 0.7f * noche : 1f;
            // la medianoche (H6) y la del ano nuevo: la onda da un destello
            double seg = cieloAhora.TimeOfDay.TotalSeconds;
            destelloOnda = (m.MedianocheCielo || (fiesta & Fiesta.FinDeAnio) != 0) && seg < 6 && viveza > 0.9f ? (float)Math.Exp(-seg / 2.0) : 0f;
            relampago = Relampago(t);
        }
        // Si se ve la estrella numero 'u' (0..1: las de u chico salen primero) con la luz de ahora y el clima.
        private float VisEstrella(float u)
        {
            float o = mCielo != null && mCielo.EstrellasAnochecer ? oscuridadSol : 1f;
            return Math.Max(0f, Math.Min(1f, (o * 1.25f - u * 0.95f) / 0.2f)) * verEstrellas;
        }

        // ---- H2: EL SOL ----
        // Rumbo y altura a la pantalla: el este a la izquierda, el oeste a la derecha (mirando al sur, como se ven salir y ponerse).
        // Por el seno del rumbo (este = izquierda, oeste = derecha, sur o norte = en medio): continuo. Antes era el rumbo de 60 a 300 grados,
        // y en verano (el sol y la luna pasan por el norte en el centro de Mexico) saltaban de una orilla a la otra a mediodia.
        private float XDeRumbo(double az, float W) { return W * (0.5f - 0.42f * (float)Math.Sin(az * Math.PI / 180)); }
        private float YDeAltura(double alt, float H) { float yh = Horizonte(H); return yh - 26 * k - (float)(Math.Sin(alt * Math.PI / 180) / Math.Sin(75 * Math.PI / 180)) * (yh - 150 * k); }
        private void Sol(float W, float H, float alfa)
        {
            if (mCielo == null || !mCielo.SolHora || solAlt < -4) return;
            float yh = Horizonte(H), x = XDeRumbo(solAz, W), y = YDeAltura(solAlt, H) + 26 * k, r = 30 * k;
            float bajo = 1f - Suave((float)(solAlt / 12)), dia = Suave((float)((solAlt + 4) / 6));
            Color4 luz = Dibujo.Mezcla(new Color4(1f, 0.97f, 0.86f), new Color4(1f, 0.62f, 0.32f), bajo), borde = Dibujo.Mezcla(new Color4(1f, 0.9f, 0.6f), new Color4(1f, 0.45f, 0.25f), bajo);
            float a = alfa * dia;
            d.Brillo(x, Math.Min(y, yh), r * 9f, borde.ConAlfa(0.28f * a));
            d.Brillo(x, Math.Min(y, yh), r * 3.5f, luz.ConAlfa(0.45f * a));
            // el disco, solo lo que queda arriba del horizonte (en tiras)
            const int n = 32;
            for (int i = 0; i < n; i++)
            {
                float y0 = y - r + 2 * r * i / n, y1 = y - r + 2 * r * (i + 1) / n;
                if (y0 >= yh) break; y1 = Math.Min(y1, yh);
                float m0 = (y0 + y1) / 2 - y, w = (float)Math.Sqrt(Math.Max(0, r * r - m0 * m0));
                Color4 cc = Dibujo.Mezcla(luz, borde, (y0 - (y - r)) / (2 * r) * 0.8f).ConAlfa(a);
                d.Rect(x - w, y0, 2 * w, y1 - y0 + 0.5f, cc);
            }
            // su luz echada sobre el horizonte, a lo ancho
            float cerca = 1f - Suave((float)(Math.Abs(solAlt) / 8));
            if (cerca > 0) for (int i = -3; i <= 3; i++) d.Brillo(x + i * 120 * k, yh, 150 * k, borde.ConAlfa(0.16f * cerca * a * (1f - Math.Abs(i) / 4f)));
        }

        // ---- H4, H7, F3: LA LUNA (donde va, de que tamano y de que color) ----
        private bool LunaDonde(float W, float H, out float x, out float y, out float r)
        {
            r = 33 * k * (lunaLlena ? 1.3f : 1f);
            if (mCielo == null || !mCielo.LunaArco) { x = W * 0.83f; y = H * 0.22f; return true; }
            x = XDeRumbo(lunaAz, W); y = Math.Max(70 * k, YDeAltura(lunaAlt, H));
            return lunaAlt > -1;
        }
        private Color4 TinteLuna()
        {
            float naranja = 0f;
            if (mCielo != null && mCielo.LunaArco) naranja = 0.45f * (1f - Suave((float)(lunaAlt / 10)));
            if ((fiesta & Fiesta.Halloween) != 0) naranja = Math.Max(naranja, 0.6f);
            return Dibujo.Mezcla(new Color4(1f, 1f, 1f), new Color4(1f, 0.68f, 0.38f), naranja);
        }

        // ---- O2 y O3: LAS NUBES ----
        private Textura[] nubes;
        private Textura HazNube(int semilla)
        {
            const int w = 256, h = 112; byte[] px = new byte[w * h * 4]; float[] den = new float[w * h];
            Random r = new Random(semilla);
            int bolas = 9 + r.Next(5);
            for (int b = 0; b < bolas; b++)
            {
                float u = (float)(0.12 + 0.76 * r.NextDouble()), cx = u * w, rad = (float)((0.13 + 0.11 * r.NextDouble()) * w * (1.1 - Math.Abs(u - 0.5)));
                float cy = h * 0.66f - (float)Math.Sin(u * Math.PI) * h * 0.22f + (float)r.NextDouble() * 6;
                for (int y = Math.Max(0, (int)(cy - rad)); y < Math.Min(h, (int)(cy + rad) + 1); y++)
                    for (int xx = Math.Max(0, (int)(cx - rad)); xx < Math.Min(w, (int)(cx + rad) + 1); xx++)
                    {
                        float dd = ((xx - cx) * (xx - cx) + (y - cy) * (y - cy)) / (rad * rad);
                        if (dd < 1) den[y * w + xx] += (1 - dd) * (1 - dd);
                    }
            }
            for (int y = 0; y < h; y++)
                for (int xx = 0; xx < w; xx++)
                {
                    float dn = den[y * w + xx], a = Math.Min(1f, dn * 1.6f);
                    a *= Math.Min(1f, Math.Min(xx, w - 1 - xx) / 10f) * Math.Min(1f, (h - 1 - y) / 8f);
                    float arriba = y > 0 ? den[(y - 1) * w + xx] : 0, lado = dn - (y > 3 ? den[(y - 4) * w + xx] : 0);
                    float luz = 0.55f + 0.30f * (1f - y / (float)h) + 0.35f * Math.Max(0f, Math.Min(1f, lado * 2.5f)) * (1f - Math.Min(1f, dn));
                    byte v = (byte)Math.Min(255, 255 * luz);
                    int o = (y * w + xx) * 4; px[o] = v; px[o + 1] = v; px[o + 2] = (byte)Math.Min(255, v + 8); px[o + 3] = (byte)(255 * Math.Max(0f, a));
                }
            return d.Sube(px, w, h);
        }
        private void Nubes(float W, float H, double t, float alfa)
        {
            if (mCielo == null) return;
            int n = 0; float op = 0f, oscura = 0f;
            switch (clima)
            {
                case Form1.TipoClima.PocoNublado: n = 6; op = 0.7f; break;
                case Form1.TipoClima.Nublado: n = 10; op = 0.9f; oscura = 0.15f; break;
                case Form1.TipoClima.Neblina: n = 4; op = 0.45f; break;
                case Form1.TipoClima.Llovizna: n = 9; op = 0.85f; oscura = 0.25f; break;
                case Form1.TipoClima.Lluvia: n = 11; op = 0.9f; oscura = 0.35f; break;
                case Form1.TipoClima.LluviaFuerte: case Form1.TipoClima.Tormenta: n = 12; op = 0.95f; oscura = 0.45f; break;
                case Form1.TipoClima.Nieve: n = 9; op = 0.85f; oscura = 0.1f; break;
            }
            if (n == 0 && mCielo.NubesCielo) { n = 3; op = 0.5f; }
            if (n == 0) return;
            if (nubes == null) { nubes = new Textura[4]; for (int i = 0; i < 4; i++) nubes[i] = HazNube(1907 + i * 31); }
            float yh = Horizonte(H);
            Color4 noche = Dibujo.Mezcla(Color4.Hex(pal.Mid), Color4.Hex(0x1A2050), 0.3f), dia = Color4.Hex(0xD6DEF2);
            Color4 baseC = Dibujo.Mezcla(dia, noche, oscuridadSol);
            baseC = Dibujo.Mezcla(baseC, Color4.Hex(0x0C1024), oscura * 0.6f);
            float lx, ly, lr; bool hayLuna = LunaDonde(W, H, out lx, out ly, out lr);
            for (int i = 0; i < n; i++)
            {
                float h1 = Hs(i, 1), h2 = Hs(i, 2), h3 = Hs(i, 3);
                float w = (260 + 260 * h1) * k * (n > 6 ? 1.15f : 1f), hh = w * 0.44f;
                float vel = (5f + viento * 0.35f) * k * (0.75f + 0.5f * h2), largo = W + 2 * w;
                float x = (float)(((h3 * largo * 3.7 + t * vel) % largo + largo) % largo) - w;
                float y = (30 + (yh * 0.48f - 30) * Hs(i, 4)) * (n > 6 ? 0.85f : 1f);
                Color4 c = baseC;
                if (hayLuna)
                {
                    float dl = (float)Math.Sqrt((lx - (x + w / 2)) * (lx - (x + w / 2)) + (ly - (y + hh / 2)) * (ly - (y + hh / 2)));
                    c = Dibujo.Mezcla(c, new Color4(0.86f, 0.9f, 1f), 0.4f * Math.Max(0f, 1f - dl / (w * 1.1f)) * oscuridadSol);
                }
                if (relampago > 0) c = Dibujo.Mezcla(c, new Color4(0.85f, 0.88f, 1f), Math.Min(1f, relampago * 3f));
                d.ImagenTenida(nubes[i % 4], x, y, w, hh, c.ConAlfa(op * alfa * (0.8f + 0.2f * h2)));
            }
        }

        // ---- O3: LLUVIA, NIEVE, NEBLINA Y RAYOS ----
        private float Relampago(double t)
        {
            if (clima != Form1.TipoClima.Tormenta) return 0f;
            int slot = (int)Math.Floor(t / 9.0); if (Hs(slot, 77) < 0.3f) return 0f;
            double edad = t - (slot * 9.0 + Hs(slot, 78) * 5.0);
            if (edad < 0 || edad > 0.7) return 0f;
            float f = (float)Math.Exp(-edad * 9) + (edad > 0.16 ? 0.6f * (float)Math.Exp(-(edad - 0.16) * 10) : 0f);
            return f * (viveza > 0.9f ? 1f : 0.4f);
        }
        private void Clima(float W, float H, double t, float alfa)
        {
            float yh = Horizonte(H), vida = 0.6f + 0.4f * viveza;
            int lluvia = 0; float largo = 0, aLl = 0;
            switch (clima)
            {
                case Form1.TipoClima.Llovizna: lluvia = 90; largo = 9; aLl = 0.15f; break;
                case Form1.TipoClima.Lluvia: lluvia = 170; largo = 16; aLl = 0.22f; break;
                case Form1.TipoClima.LluviaFuerte: case Form1.TipoClima.Tormenta: lluvia = 260; largo = 21; aLl = 0.26f; break;
            }
            if (lluvia > 0)
            {
                float inc = 0.16f + Math.Min(0.25f, viento * 0.005f), alto = H + 80 * k;
                Color4 cl = new Color4(0.72f, 0.78f, 1f, aLl * alfa);
                int cuantas = (int)(lluvia * vida);
                for (int i = 0; i < cuantas; i++)
                {
                    float v = (700 + 350 * Hs(i, 11)) * k, y = (float)(((Hs(i, 12) * alto + t * v) % alto)) - 40 * k;
                    float x = (Hs(i, 13) * (W + 200 * k) - inc * y) % (W + 200 * k); if (x < 0) x += W + 200 * k; x -= 100 * k;
                    float l = largo * k * (0.7f + 0.6f * Hs(i, 14));
                    d.Linea(x, y, x + inc * l, y - l, Math.Max(1f, 1.1f * k), cl);
                }
            }
            if (clima == Form1.TipoClima.Nieve)
            {
                int cuantos = (int)(150 * vida);
                for (int i = 0; i < cuantos; i++)
                {
                    float v = (16 + 26 * Hs(i, 21)) * k, y = (float)((Hs(i, 22) * (H + 20 * k) + t * v) % (H + 20 * k)) - 10 * k;
                    float x = Hs(i, 23) * W + (float)Math.Sin(t * 0.8 + i) * 12 * k, r = (1.5f + 2f * Hs(i, 24)) * k;
                    d.Brillo(x, y, r * 2.2f, new Color4(1f, 1f, 1f, 0.8f * alfa));
                }
            }
            if (clima == Form1.TipoClima.Neblina)
            {
                Color4 niebla = Dibujo.Mezcla(new Color4(0.62f, 0.66f, 0.82f), new Color4(0.85f, 0.88f, 0.95f), 1f - oscuridadSol);
                d.Degradado(0, H * 0.40f, W, yh - H * 0.40f, niebla.ConAlfa(0f), niebla.ConAlfa(0.45f * alfa));
                d.Degradado(0, yh, W, H - yh, niebla.ConAlfa(0.45f * alfa), niebla.ConAlfa(0.3f * alfa));
                for (int i = 0; i < 6; i++)
                {
                    float x = (float)(((Hs(i, 31) * W + t * (6 + 5 * Hs(i, 32)) * k) % (W + 600 * k))) - 300 * k, y = H * (0.55f + 0.3f * Hs(i, 33));
                    d.Brillo(x, y, 280 * k, niebla.ConAlfa(0.14f * alfa));
                }
            }
            if (relampago > 0)
            {
                d.Rect(0, 0, W, yh, new Color4(0.75f, 0.8f, 1f, 0.14f * relampago * alfa));
                int slot = (int)Math.Floor(t / 9.0); double edad = t - (slot * 9.0 + Hs(slot, 78) * 5.0);
                if (viveza > 0.9f && edad < 0.25)
                {
                    // el rayo: una linea quebrada de la nube al horizonte, con una rama
                    float x = W * (0.15f + 0.7f * Hs(slot, 79)), y = H * 0.16f, a = relampago * alfa;
                    float yfin = yh - 30 * k, paso = (yfin - y) / 10f;
                    float rx = 0, ry = 0;
                    for (int i = 0; i < 10; i++)
                    {
                        float x1 = x + (Hs(slot * 13 + i, 80) - 0.5f) * 46 * k, y1 = y + paso;
                        d.Linea(x, y, x1, y1, 2.6f * k, new Color4(0.92f, 0.94f, 1f, 0.95f * a));
                        d.Brillo(x1, y1, 26 * k, new Color4(0.7f, 0.75f, 1f, 0.12f * a));
                        if (i == 3) { rx = x1; ry = y1; }
                        x = x1; y = y1;
                    }
                    for (int i = 0; i < 4; i++) { float x1 = rx + (18 + 22 * Hs(slot * 7 + i, 81)) * k, y1 = ry + paso * 0.7f; d.Linea(rx, ry, x1, y1, 1.4f * k, new Color4(0.9f, 0.92f, 1f, 0.6f * a)); rx = x1; ry = y1; }
                }
            }
        }

        // ---- F2 y H6: LAS ESTRELLAS FUGACES (solo con nadie usando la rocola) ----
        private void Fugaces(float W, float H, double t, float alfa)
        {
            if (viveza < 0.9f || mCielo == null) return;
            float yh = Horizonte(H), o = VisEstrella(0.3f);
            if (lluviaNombre != null && oscuridadSol > 0.7f && verEstrellas > 0.5f)
                FugacesDe(W, yh, t, alfa * o, 2.0, 0.3f, lluviaX * W, lluviaY * yh, 1);
            double seg = cieloAhora.TimeOfDay.TotalSeconds;
            if (mCielo.MedianocheCielo && seg < 10) FugacesDe(W, yh, t, alfa, 0.45, 0f, -1, -1, 2);
        }
        private void FugacesDe(float W, float yh, double t, float alfa, double periodo, float salta, float rx, float ry, int sem)
        {
            int n1 = (int)Math.Floor(t / periodo);
            for (int n = n1 - 2; n <= n1; n++)
            {
                if (Hs(n, sem * 100) < salta) continue;
                double ts = n * periodo + Hs(n, sem * 100 + 1) * periodo * 0.6, dur = 0.55 + 0.4 * Hs(n, sem * 100 + 2), e = t - ts;
                if (e < 0 || e > dur) continue;
                float tx = W * (0.08f + 0.84f * Hs(n, sem * 100 + 3)), ty = yh * (0.12f + 0.55f * Hs(n, sem * 100 + 4)), x0, y0, dx, dy;
                if (rx < 0) { x0 = tx; y0 = ty * 0.6f; dx = Hs(n, sem * 100 + 5) < 0.5f ? -1 : 1; dy = 0.6f + 0.5f * Hs(n, sem * 100 + 6); }
                else { dx = tx - rx; dy = ty - ry; x0 = rx; y0 = ry; }
                float l = (float)Math.Sqrt(dx * dx + dy * dy); if (l < 1e-3f) continue; dx /= l; dy /= l;
                if (rx >= 0) { float sale = (80 + 220 * Hs(n, sem * 100 + 7)) * k; x0 += dx * sale; y0 += dy * sale; }
                float u = (float)(e / dur), largo = (150 + 120 * Hs(n, sem * 100 + 8)) * k, a = (float)Math.Sin(u * Math.PI) * alfa;
                float hx = x0 + dx * largo * u * 1.4f, hy = y0 + dy * largo * u * 1.4f, cola = largo * 0.6f;
                for (int sgm = 0; sgm < 6; sgm++)
                {
                    float f0 = sgm / 6f, f1 = (sgm + 1) / 6f;
                    d.Linea(hx - dx * cola * f0, hy - dy * cola * f0, hx - dx * cola * f1, hy - dy * cola * f1, 2f * k * (1f - f0 * 0.6f), Dibujo.Mezcla(new Color4(1f, 1f, 1f), new Color4(0.6f, 0.72f, 1f), f0).ConAlfa(a * (1f - f0) * 0.9f));
                }
                d.Brillo(hx, hy, 7 * k, new Color4(1f, 1f, 1f, a));
            }
        }

        // ---- O1: LAS CONSTELACIONES ----
        private static readonly float[][] ConsPuntos = {
            new float[] { .25f, .08f, .72f, .12f, .48f, 0f, .40f, .50f, .50f, .48f, .60f, .46f, .30f, .92f, .75f, .88f },
            new float[] { 0f, .30f, .05f, .62f, .30f, .70f, .33f, .40f, .52f, .33f, .72f, .30f, .98f, .45f },
            new float[] { 0f, .20f, .25f, .85f, .50f, .35f, .75f, .90f, 1f, .10f },
            new float[] { .85f, 0f, .92f, .12f, .88f, .26f, .72f, .18f, .62f, .30f, .55f, .43f, .50f, .56f, .42f, .70f, .30f, .82f, .15f, .86f, .05f, .76f, .08f, .62f } };
        private static readonly int[][] ConsLineas = {
            new[] { 2, 0, 2, 1, 0, 3, 1, 5, 3, 4, 4, 5, 3, 6, 5, 7 },
            new[] { 0, 1, 1, 2, 2, 3, 3, 0, 3, 4, 4, 5, 5, 6 },
            new[] { 0, 1, 1, 2, 2, 3, 3, 4 },
            new[] { 0, 3, 1, 3, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11 } };
        private static readonly float[] ConsCaja = { .13f, .16f, .17f, .30f, .42f, .12f, .26f, .16f, .46f, .44f, .18f, .12f, .11f, .16f, .2f, .32f };
        private static readonly string[] ConsNombre = { "Orión", "Osa Mayor", "Casiopea", "Escorpión" };
        // Orion es de las noches de noviembre a abril; de mayo a octubre va el Escorpion en su lugar.
        private int ConsDe(int i) { return i == 0 && cieloAhora.Month >= 5 && cieloAhora.Month <= 10 ? 3 : i; }
        private void Constelaciones(float W, float H, double t, float alfa)
        {
            if (mCielo == null || !mCielo.Constelaciones) return;
            float yh = Horizonte(H), vis = VisEstrella(0.25f) * alfa;
            if (vis <= 0.01f) return;
            for (int c0 = 0; c0 < 3; c0++)
            {
                int c = ConsDe(c0);
                float bx = ConsCaja[c * 4] * W, by = ConsCaja[c * 4 + 1] * yh, bw = ConsCaja[c * 4 + 2] * W, bh = ConsCaja[c * 4 + 3] * yh;
                float[] p = ConsPuntos[c];
                for (int i = 0; i < p.Length; i += 2)
                {
                    float a = vis * (0.75f + 0.25f * (float)Math.Sin(t * 1.1 + i + c * 3));
                    d.Brillo(bx + p[i] * bw, by + p[i + 1] * bh, 5 * k, new Color4(0.9f, 0.93f, 1f, a));
                    d.Brillo(bx + p[i] * bw, by + p[i + 1] * bh, 13 * k, new Color4(0.75f, 0.82f, 1f, 0.18f * a));
                }
            }
            if (viveza < 0.9f || oscuridadSol < 0.6f || verEstrellas < 0.5f) return;
            int cual = ConsDe((int)Math.Floor(t / 40.0) % 3); double fase = t % 40.0;
            if (fase > 9) return;
            float prog = (float)Math.Min(1, fase / 2), al = fase < 7 ? 1f : (float)(1 - (fase - 7) / 2);
            {
                float bx = ConsCaja[cual * 4] * W, by = ConsCaja[cual * 4 + 1] * yh, bw = ConsCaja[cual * 4 + 2] * W, bh = ConsCaja[cual * 4 + 3] * yh;
                float[] p = ConsPuntos[cual]; int[] ln = ConsLineas[cual]; int nl = ln.Length / 2;
                Color4 cl = new Color4(0.67f, 0.76f, 1f, 0.55f * al * vis);
                for (int j = 0; j < nl; j++)
                {
                    float q = Math.Max(0f, Math.Min(1f, prog * nl - j)); if (q <= 0) continue;
                    float ax = bx + p[ln[2 * j] * 2] * bw, ay = by + p[ln[2 * j] * 2 + 1] * bh, cx = bx + p[ln[2 * j + 1] * 2] * bw, cy = by + p[ln[2 * j + 1] * 2 + 1] * bh;
                    d.Linea(ax, ay, ax + (cx - ax) * q, ay + (cy - ay) * q, 1.3f * k, cl);
                }
                mini.EscribeCentrado(d, ConsNombre[cual], bx + bw / 2, by + bh + 12 * k, new Color4(0.78f, 0.84f, 1f, 0.75f * al * prog * vis));
            }
        }

        // ---- F1: LAS FIESTAS ----
        private void Fiestas(float W, float H, double t, float alfa)
        {
            if (fiesta == Fiesta.Ninguna) return;
            float yh = Horizonte(H), cuanto = 0.4f + 0.6f * viveza;
            double hora = cieloAhora.Hour + cieloAhora.Minute / 60.0;
            bool deNoche = oscuridadSol > 0.4f;   // (por el sol, no por la hora: en junio oscurece mas tarde)
            if ((fiesta & Fiesta.Muertos) != 0)
            {
                PapelPicado(W, H * 0.15f, t, alfa, ColoresMuertos, 0);
                PapelPicado(W, H * 0.25f, t, alfa * 0.9f, ColoresMuertos, 5);
                Veladoras(W, yh, t, alfa);
                Almas(W, yh, t, alfa * cuanto);
                Petalos(W, yh, t, alfa, (int)(48 * cuanto), new Color4(1f, 0.58f, 0.05f), new Color4(1f, 0.77f, 0f), 4.5f, 22, 41);
                Mariposas(W, yh, t, alfa, (int)(9 * cuanto), true, 121);   // las monarcas llegan con los muertos
            }
            if ((fiesta & Fiesta.MesPatrio) != 0) PapelPicado(W, H * 0.16f, t, alfa * 0.85f, ColoresPatrios, 2);
            if ((fiesta & Fiesta.Bandera) != 0) Bandera(W, yh, t, alfa);
            if ((fiesta & Fiesta.Candelaria) != 0) Farolitos(W, yh, t, alfa, (int)(16 * cuanto));
            if ((fiesta & Fiesta.Mujer) != 0) { Petalos(W, yh, t, alfa, (int)(56 * cuanto), Color4.Hex(0xC77DFF), Color4.Hex(0xF0C8FF), 6.5f, 18, 45); Mariposas(W, yh, t, alfa, (int)(6 * cuanto), false, 123); }
            if ((fiesta & Fiesta.SanPatricio) != 0) Treboles(W, yh, t, alfa, (int)(34 * cuanto));
            if ((fiesta & Fiesta.Primavera) != 0) { Mariposas(W, yh, t, alfa, (int)(14 * cuanto), false, 122); Petalos(W, yh, t, alfa, (int)(30 * cuanto), Color4.Hex(0xFFB3D1), Color4.Hex(0xFFF1A8), 4f, 16, 46); }
            if ((fiesta & (Fiesta.CincoDeMayo | Fiesta.Revolucion)) != 0) { PapelPicado(W, H * 0.16f, t, alfa, ColoresPatrios, 1); if (deNoche) Fuegos(W, yh, t, alfa, ColoresPatrios, 5.0, 53); }
            if ((fiesta & Fiesta.DiaDelPadre) != 0) { Confeti(W, yh, t, alfa, (int)(60 * cuanto), true); Globos(W, yh, t, alfa, (int)(12 * cuanto), ColoresPapa); }
            if ((fiesta & Fiesta.SantaCecilia) != 0) Notas(W, yh, t, alfa, (int)(22 * cuanto));
            if ((fiesta & Fiesta.Posadas) != 0) { Pinatas(W, H, t, alfa); Farolitos(W, yh, t, alfa * 0.8f, (int)(8 * cuanto)); }
            if ((fiesta & Fiesta.Halloween) != 0) Murcielagos(W, yh, t, alfa, (int)(10 * cuanto));
            if ((fiesta & Fiesta.Patrias) != 0)
            {
                PapelPicado(W, H * 0.16f, t, alfa, ColoresPatrios, 2);
                if ((fiesta & Fiesta.Grito) != 0) Fuegos(W, yh, t, alfa, ColoresPatrios, 0.85, 51);
                else if (deNoche) Fuegos(W, yh, t, alfa, ColoresPatrios, 6.0, 52);
            }
            if ((fiesta & Fiesta.Navidad) != 0)
            {
                bool buena = (fiesta & Fiesta.Nochebuena) != 0;
                Copos(W, H, t, alfa, (int)((buena ? 140 : 80) * cuanto));
                SerieDeLuces(W, yh, t, alfa);
                if (buena) EstrellaDeBelen(W, yh, t, alfa);
            }
            if ((fiesta & Fiesta.Reyes) != 0) { EstrellaDeBelen(W, yh, t, alfa); ReyesMagos(W, yh, t, alfa); }
            if ((fiesta & Fiesta.AnioNuevo) != 0)
            {
                Confeti(W, yh, t, alfa, (int)(60 * cuanto), false);
                if ((fiesta & Fiesta.FinDeAnio) != 0) { Fuegos(W, yh, t, alfa, ColoresAnio, 0.5, 61); CuentaYFeliz(W, H, alfa); }
                else if (cieloAhora.Month == 12 && deNoche) Fuegos(W, yh, t, alfa, ColoresAnio, 7.0, 62);
            }
            if ((fiesta & Fiesta.SanValentin) != 0) Corazones(W, yh, t, alfa, (int)(18 * cuanto), 71);
            if ((fiesta & Fiesta.Jacarandas) != 0) Petalos(W, yh, t, alfa, (int)(56 * cuanto), Color4.Hex(0xC9A8FF), Color4.Hex(0xE6D2FF), 5f, 18, 42);
            if ((fiesta & Fiesta.DiaDelNino) != 0) Globos(W, yh, t, alfa, (int)(14 * cuanto), ColoresGlobos);
            if ((fiesta & Fiesta.Madres) != 0) { Petalos(W, yh, t, alfa, (int)(40 * cuanto), Color4.Hex(0xD7263D), Color4.Hex(0xFF7AA2), 4.5f, 20, 43); Corazones(W, yh, t, alfa, (int)(7 * cuanto), 72); }
            if ((fiesta & Fiesta.Guadalupe) != 0)
            {
                d.Brillo(W / 2, 0, 360 * k, new Color4(1f, 0.82f, 0.4f, 0.08f * alfa));
                Petalos(W, yh, t, alfa, (int)(40 * cuanto), Color4.Hex(0xC8102E), Color4.Hex(0xFF4D6D), 4.5f, 20, 44);
            }
        }
        private static readonly Color4[] ColoresMuertos = { Color4.Hex(0xE6007E), Color4.Hex(0xFF8C00), Color4.Hex(0x7B2FBE), Color4.Hex(0x00A86B), Color4.Hex(0xFFD100), Color4.Hex(0xFF5FA2), Color4.Hex(0x1E90FF) };
        private static readonly Color4[] ColoresPatrios = { Color4.Hex(0x0B9A55), Color4.Hex(0xF4F4F4), Color4.Hex(0xE0202E) };
        private static readonly Color4[] ColoresLuces = { Color4.Hex(0xFF4646), Color4.Hex(0x50DC6E), Color4.Hex(0xFFC846), Color4.Hex(0x5AA0FF), Color4.Hex(0xFFFFFF) };
        private static readonly Color4[] ColoresGlobos = { Color4.Hex(0xFF4D4D), Color4.Hex(0xFFD23F), Color4.Hex(0x3FA9FF), Color4.Hex(0x4CD97B), Color4.Hex(0xFF8C3F), Color4.Hex(0xFF6EC7), Color4.Hex(0xA06EFF) };
        private static readonly Color4[] ColoresAnio = { Color4.Hex(0xFFCE5A), Color4.Hex(0x5AE6FF), Color4.Hex(0xFF6EDC), Color4.Hex(0xFFFFFF), Color4.Hex(0xB48CFF) };
        // Papel picado: una cuerda que cuelga y sus banderitas con su recorte, que se mecen.
        private void PapelPicado(float W, float y0, double t, float alfa, Color4[] colores, int desfase)
        {
            float paso = 46 * k, fw = 34 * k, fh = 42 * k, caida = 22 * k;
            int n = (int)(W / paso) + 1;
            Color4 cuerda = new Color4(0.85f, 0.85f, 0.9f, 0.55f * alfa), hueco = new Color4(0.05f, 0.06f, 0.18f, 0.55f * alfa);
            float px = 0, py = y0;
            for (int i = 0; i <= 40; i++) { float x = W * i / 40f, y = y0 + caida * (float)Math.Sin(Math.PI * ((x / (W / 3)) % 1)); if (i > 0) d.Linea(px, py, x, y, 1.2f * k, cuerda); px = x; py = y; }
            for (int i = 0; i < n; i++)
            {
                float x = (i + 0.5f) * paso, y = y0 + caida * (float)Math.Sin(Math.PI * ((x / (W / 3)) % 1));
                float mece = (float)Math.Sin(t * 1.6 + i * 0.7 + desfase) * 5 * k * (0.3f + 0.7f * viveza);
                Color4 c = colores[(i + desfase) % colores.Length].ConAlfa(0.88f * alfa);
                float x0 = x - fw / 2, x1 = x + fw / 2, yb = y + fh;
                d.Cuatro(x0, y, c, x1, y, c, x1 + mece, yb, c, x0 + mece, yb, c);
                // el fleco de abajo (picos) y el recorte (rombos y un circulo)
                for (int j = 0; j < 4; j++) { float a0 = x0 + mece + fw * j / 4f, a1 = x0 + mece + fw * (j + 1) / 4f; d.Triangulo(a0, yb - 0.5f, a1, yb - 0.5f, (a0 + a1) / 2, yb + 6 * k, c); }
                float cx = x + mece * 0.5f, cy = y + fh * 0.45f;
                d.Triangulo(cx - 5 * k, cy, cx, cy - 7 * k, cx + 5 * k, cy, hueco); d.Triangulo(cx - 5 * k, cy, cx, cy + 7 * k, cx + 5 * k, cy, hueco);
                d.Brillo(cx - 9 * k + mece * 0.2f, cy - 9 * k, 3 * k, hueco); d.Brillo(cx + 9 * k + mece * 0.2f, cy - 9 * k, 3 * k, hueco);
                d.Rect(x0 + 4 * k + mece * 0.85f, yb - 9 * k, fw - 8 * k, 2 * k, hueco);
            }
        }
        // Petalos que caen dando vueltas.
        private void Petalos(float W, float yh, double t, float alfa, int n, Color4 a, Color4 b, float tam, float vel, int sem)
        {
            float alto = yh + 40 * k;
            for (int i = 0; i < n; i++)
            {
                float v = (vel + vel * Hs(i, sem)) * k, y = (float)((Hs(i, sem + 1) * alto + t * v) % alto) - 20 * k;
                float x = Hs(i, sem + 2) * W + (float)Math.Sin(t * (0.6 + Hs(i, sem + 3)) + i) * 30 * k, s = tam * k * (0.7f + 0.6f * Hs(i, sem + 4));
                double ang = t * (0.8 + 1.6 * Hs(i, sem + 5)) + i;
                float ca = (float)Math.Cos(ang), sa = (float)Math.Sin(ang), ap = 0.55f + 0.45f * (float)Math.Abs(Math.Cos(ang * 0.7));
                Color4 c = (Hs(i, sem + 6) < 0.6f ? a : b).ConAlfa(0.85f * alfa);
                d.Cuatro(x + ca * s, y + sa * s, c, x - sa * s * 0.55f * ap, y + ca * s * 0.55f * ap, c, x - ca * s, y - sa * s, c, x + sa * s * 0.55f * ap, y - ca * s * 0.55f * ap, c);
            }
        }
        // Veladoras a lo largo del horizonte, con su flama que tiembla.
        private void Veladoras(float W, float yh, double t, float alfa)
        {
            for (int i = 0; i < 24; i++)
            {
                float x = (i + 0.5f) / 24f * W + (Hs(i, 51) - 0.5f) * 14 * k;
                float fl = 0.75f + 0.25f * (float)(Math.Sin(t * 9 + i * 3.1) * Math.Sin(t * 5.3 + i));
                d.Brillo(x, yh - 8 * k, 24 * k, new Color4(1f, 0.58f, 0.12f, 0.45f * fl * alfa));
                d.Rect(x - 2.5f * k, yh - 6 * k, 5 * k, 8 * k, new Color4(0.95f, 0.9f, 0.82f, 0.9f * alfa));
                d.Brillo(x, yh - 9.5f * k, 4.5f * k, new Color4(1f, 0.86f, 0.5f, 0.95f * fl * alfa));
            }
        }
        // Lucecitas que suben despacio desde las veladoras y se desvanecen.
        private void Almas(float W, float yh, double t, float alfa)
        {
            for (int i = 0; i < 9; i++)
            {
                double u = (Hs(i, 53) + t * 0.018 * (0.6 + Hs(i, 54))) % 1.0;
                float x = Hs(i, 55) * W + (float)Math.Sin(t * 0.4 + i * 2) * 22 * k, y = yh - 10 * k - (float)u * yh * 0.7f, a = (float)Math.Sin(u * Math.PI) * 0.4f * alfa;
                d.Brillo(x, y, 14 * k, new Color4(1f, 0.85f, 0.55f, a * 0.5f));
                d.Brillo(x, y, 4 * k, new Color4(1f, 0.95f, 0.85f, a));
            }
        }
        // Murcielagos que cruzan aleteando (31 de octubre).
        private void Murcielagos(float W, float yh, double t, float alfa, int n)
        {
            Color4 c = new Color4(0.05f, 0.04f, 0.09f, 0.92f * alfa);
            for (int i = 0; i < n; i++)
            {
                float v = (40 + 35 * Hs(i, 61)) * k, largo = W + 120 * k, dir = Hs(i, 62) < 0.5f ? 1 : -1;
                float x = (float)((Hs(i, 63) * largo + t * v) % largo) - 60 * k; if (dir < 0) x = W - x;
                float y = 40 * k + Hs(i, 64) * yh * 0.5f + (float)Math.Sin(t * 1.5 + i) * 26 * k, s = (1.4f + 0.7f * Hs(i, 65)) * k;
                float ala = (float)Math.Sin(t * 14 + i * 1.7) * 7 * s;
                d.Redondo(x - 3 * s, y - 3 * s, 6 * s, 8 * s, 3 * s, c);
                d.Triangulo(x - 2 * s, y, x - 18 * s, y - 3 * s - ala, x - 11 * s, y + 6 * s, c); d.Triangulo(x - 11 * s, y + 6 * s, x - 18 * s, y - 3 * s - ala, x - 21 * s, y + 4 * s - ala * 0.5f, c);
                d.Triangulo(x + 2 * s, y, x + 18 * s, y - 3 * s - ala, x + 11 * s, y + 6 * s, c); d.Triangulo(x + 11 * s, y + 6 * s, x + 18 * s, y - 3 * s - ala, x + 21 * s, y + 4 * s - ala * 0.5f, c);
                d.Triangulo(x - 3 * s, y - 2 * s, x - 1.5f * s, y - 6 * s, x - 0.5f * s, y - 2 * s, c); d.Triangulo(x + 3 * s, y - 2 * s, x + 1.5f * s, y - 6 * s, x + 0.5f * s, y - 2 * s, c);
            }
        }
        // Fuegos artificiales: un cohete que sube y revienta en chispas que caen y se apagan. 'periodo': cada cuanto sale uno.
        private void Fuegos(float W, float yh, double t, float alfa, Color4[] colores, double periodo, int sem)
        {
            const double vida = 3.6;
            int n1 = (int)Math.Floor(t / periodo);
            for (int n = (int)Math.Floor((t - vida) / periodo) - 1; n <= n1; n++)
            {
                if (viveza < 0.9f && Hs(n, sem) < 0.6f) continue;   // con alguien usando la rocola, uno de cada tres
                double ts = n * periodo + Hs(n, sem + 1) * periodo * 0.6, e = t - ts;
                if (e < 0 || e > vida) continue;
                float x0 = W * (0.12f + 0.76f * Hs(n, sem + 2)), ty = yh * (0.12f + 0.32f * Hs(n, sem + 3));
                Color4 c1 = colores[(int)(Hs(n, sem + 4) * colores.Length) % colores.Length], c2 = colores[(int)(Hs(n, sem + 5) * colores.Length) % colores.Length];
                if (e < 1.0)
                {
                    float u = (float)e, y = yh - (yh - ty) * (1f - (1f - u) * (1f - u));
                    d.Linea(x0, y, x0, y + 18 * k, 2f * k, c1.ConAlfa(0.7f * alfa));
                    d.Brillo(x0, y, 5 * k, new Color4(1f, 0.95f, 0.8f, 0.9f * alfa));
                    continue;
                }
                float tau = (float)(e - 1.0);
                if (tau < 0.3f) d.Brillo(x0, ty, 70 * k * (1f - tau / 0.3f) + 10 * k, c1.ConAlfa(0.45f * alfa * (1f - tau / 0.3f)));
                int m = viveza < 0.9f ? 26 : 46;
                float apaga = (float)Math.Pow(Math.Max(0, 1 - tau / 2.6), 1.5);
                for (int j = 0; j < m; j++)
                {
                    double ang = j / (double)m * 2 * Math.PI + Hs(n, sem + 6) * 6.28;
                    float sp = (70 + 70 * Hs(n * 64 + j, sem + 7)) * k;
                    float q0 = Math.Max(0, tau - 0.09f), d1 = sp * (1f - (float)Math.Exp(-1.8 * tau)) / 1.8f, d0 = sp * (1f - (float)Math.Exp(-1.8 * q0)) / 1.8f;
                    float px = x0 + (float)Math.Cos(ang) * d1, py = ty + (float)Math.Sin(ang) * d1 + 28 * k * tau * tau;
                    float qx = x0 + (float)Math.Cos(ang) * d0, qy = ty + (float)Math.Sin(ang) * d0 + 28 * k * q0 * q0;
                    float a = apaga * alfa * (tau > 1.6f ? 0.6f + 0.4f * (float)Math.Sin(t * 30 + j) : 1f);
                    Color4 c = (j % 3 == 0 ? c2 : c1);
                    d.Linea(qx, qy, px, py, 1.8f * k, c.ConAlfa(a));
                    d.Brillo(px, py, 4 * k, c.ConAlfa(0.8f * a));
                }
            }
        }
        // La nieve de Navidad (aparte de la del clima).
        private void Copos(float W, float H, double t, float alfa, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float v = (14 + 24 * Hs(i, 91)) * k, y = (float)((Hs(i, 92) * (H + 20 * k) + t * v) % (H + 20 * k)) - 10 * k;
                float x = Hs(i, 93) * W + (float)Math.Sin(t * 0.7 + i) * 14 * k, r = (1.3f + 2f * Hs(i, 94)) * k;
                d.Brillo(x, y, r * 2.2f, new Color4(1f, 1f, 1f, 0.75f * alfa));
            }
        }
        // Una serie de luces colgada sobre el horizonte, que se prende y apaga en cadena.
        private void SerieDeLuces(float W, float yh, double t, float alfa)
        {
            Color4[] col = ColoresLuces;
            float base_ = yh - 52 * k, caida = 20 * k;
            float px = 0, py = base_;
            for (int i = 1; i <= 80; i++) { float x = W * i / 80f, y = base_ + caida * (float)Math.Sin(Math.PI * ((x / (W / 4)) % 1)); d.Linea(px, py, x, y, 1.4f * k, new Color4(0.08f, 0.12f, 0.1f, 0.85f * alfa)); px = x; py = y; }
            for (int i = 0; i < 44; i++)
            {
                float x = (i + 0.5f) / 44f * W, y = base_ + caida * (float)Math.Sin(Math.PI * ((x / (W / 4)) % 1)) + 5 * k;
                float on = 0.35f + 0.65f * Suave((float)(0.5 + 0.5 * Math.Sin(t * 2.2 - i * 0.9)) * 1.4f - 0.2f) * (0.4f + 0.6f * viveza) + 0.35f * (1f - viveza);
                Color4 c = col[i % col.Length];
                d.Brillo(x, y, 11 * k, c.ConAlfa(0.5f * on * alfa));
                d.Brillo(x, y, 3 * k, Dibujo.Mezcla(c, new Color4(1f, 1f, 1f), 0.5f).ConAlfa(0.95f * on * alfa));
            }
        }
        // La estrella de Belen: grande, dorada, con sus rayos y una cola de luz hacia abajo.
        private void EstrellaDeBelen(float W, float yh, double t, float alfa)
        {
            float x = W * 0.6f, y = yh * 0.13f, pulso = 0.85f + 0.15f * (float)Math.Sin(t * 1.5);
            Color4 oro = new Color4(1f, 0.86f, 0.5f);
            d.Brillo(x, y, 110 * k, oro.ConAlfa(0.22f * alfa * pulso));
            d.Cuatro(x - 7 * k, y, oro.ConAlfa(0.35f * alfa), x + 7 * k, y, oro.ConAlfa(0.35f * alfa), x + 30 * k, yh * 0.8f, oro.ConAlfa(0f), x - 30 * k, yh * 0.8f, oro.ConAlfa(0f));
            Destello(x, y, 2.4f * k * pulso, oro, alfa);
            float L = 22 * k * pulso, g = 1.2f * k; Color4 cc = oro.ConAlfa(0.7f * alfa), nada = oro.ConAlfa(0f);
            d.Cuatro(x - L, y - L, nada, x + g, y - g, cc, x + L, y + L, nada, x - g, y + g, cc);
            d.Cuatro(x + L, y - L, nada, x + g, y + g, cc, x - L, y + L, nada, x - g, y - g, cc);
        }
        // Tres estrellitas que viajan despacio hacia la de Belen (Reyes).
        private void ReyesMagos(float W, float yh, double t, float alfa)
        {
            for (int i = 0; i < 3; i++)
            {
                float p = (float)((t * 0.004 + i * 0.035) % 1.0);
                float x = W * (0.95f - 0.62f * p), y = yh * (0.62f - 0.3f * p) + (i - 1) * 16 * k;
                Destello(x, y, 0.8f * k, new Color4(1f, 0.9f, 0.6f), alfa * (float)Math.Sin(p * Math.PI));
            }
        }
        // Confeti dorado y plateado que cae despacio y brilla (Año Nuevo).
        private void Confeti(float W, float yh, double t, float alfa, int n, bool azul)
        {
            for (int i = 0; i < n; i++)
            {
                float v = (10 + 14 * Hs(i, 101)) * k, y = (float)((Hs(i, 102) * yh + t * v) % yh);
                float x = Hs(i, 103) * W + (float)Math.Sin(t * 0.9 + i) * 10 * k, br = 0.5f + 0.5f * (float)Math.Sin(t * 5 + i * 2.3);
                Color4 c = Hs(i, 104) < 0.65f ? new Color4(1f, 0.84f, 0.4f) : azul ? new Color4(0.45f, 0.7f, 1f) : new Color4(0.9f, 0.92f, 1f);
                d.Brillo(x, y, (azul ? 4.5f : 3f) * k, c.ConAlfa(0.9f * br * alfa));
            }
        }
        // La cuenta de los ultimos diez segundos del ano, y luego «¡Feliz 2027!» cinco minutos.
        private void CuentaYFeliz(float W, float H, float alfa)
        {
            DateTime a = cieloAhora; double seg;
            if (a.Month == 12 && a.Day == 31) { seg = (a.Date.AddDays(1) - a).TotalSeconds; if (seg > 10 || seg <= 0) return; string s = ((int)Math.Ceiling(seg)).ToString(); Letra f = Titulo(150 * k); float fr = (float)(seg % 1.0); d.Brillo(W / 2, H * 0.36f, 160 * k, new Color4(1f, 0.8f, 0.35f, 0.25f * alfa * fr)); f.EscribeCentrado(d, s, W / 2, H * 0.36f - f.Tam * 0.55f, new Color4(1f, 0.88f, 0.5f, alfa * (0.4f + 0.6f * fr))); return; }
            seg = a.TimeOfDay.TotalSeconds; if (seg > 300) return;
            float al = (float)Math.Min(1, Math.Min(seg / 1.5, (300 - seg) / 3)) * alfa;
            string feliz = "¡Feliz " + a.Year + "!"; Letra g = Titulo(78 * k);
            d.Brillo(W / 2, H * 0.32f, 260 * k, new Color4(1f, 0.8f, 0.4f, 0.18f * al));
            g.EscribeCentrado(d, feliz, W / 2, H * 0.32f - g.Tam * 0.55f, new Color4(1f, 0.9f, 0.55f, al));
        }
        // Corazones que suben despacio meciendose.
        private void Corazones(float W, float yh, double t, float alfa, int n, int sem)
        {
            for (int i = 0; i < n; i++)
            {
                double u = (Hs(i, sem) + t * 0.012 * (0.6 + Hs(i, sem + 1))) % 1.0;
                float x = Hs(i, sem + 2) * W + (float)Math.Sin(t * 0.8 + i) * 20 * k, y = yh - (float)u * (yh + 40 * k), r = (7 + 7 * Hs(i, sem + 3)) * k;
                Color4 c = (Hs(i, sem + 4) < 0.5f ? new Color4(1f, 0.35f, 0.55f) : new Color4(1f, 0.6f, 0.75f)).ConAlfa(0.5f * alfa * (float)Math.Sin(u * Math.PI));
                Circulo(x - r * 0.52f, y, r * 0.58f, c); Circulo(x + r * 0.52f, y, r * 0.58f, c);
                d.Triangulo(x - r * 1.08f, y + r * 0.15f, x + r * 1.08f, y + r * 0.15f, x, y + r * 1.35f, c);
            }
        }
        // Globos de colores que suben con su hilo (Dia del Nino).
        private static readonly Color4[] ColoresPapa = { Color4.Hex(0x2F6BFF), Color4.Hex(0xFFC93C), Color4.Hex(0x4FC3F7), Color4.Hex(0x1E3FA8), Color4.Hex(0xE8EEF8) };
        private void Globos(float W, float yh, double t, float alfa, int n, Color4[] col)
        {
            for (int i = 0; i < n; i++)
            {
                double u = (Hs(i, 111) + t * 0.01 * (0.6 + Hs(i, 112))) % 1.0;
                float x = Hs(i, 113) * W + (float)Math.Sin(t * 0.6 + i) * 18 * k, y = yh + 40 * k - (float)u * (yh + 120 * k), s = (0.9f + 0.4f * Hs(i, 114)) * k;
                Color4 c = col[i % col.Length].ConAlfa(0.85f * alfa);
                d.Redondo(x - 12 * s, y - 15 * s, 24 * s, 30 * s, 12 * s, c);
                d.Brillo(x - 5 * s, y - 7 * s, 5 * s, new Color4(1f, 1f, 1f, 0.55f * alfa));
                d.Triangulo(x - 3 * s, y + 18 * s, x + 3 * s, y + 18 * s, x, y + 14 * s, c);
                float hx = x, hy = y + 18 * s;
                for (int j = 1; j <= 6; j++) { float nx = x + (float)Math.Sin(t * 2 + j + i) * 3 * s, ny = y + 18 * s + j * 8 * s; d.Linea(hx, hy, nx, ny, 1f, new Color4(0.85f, 0.85f, 0.9f, 0.6f * alfa)); hx = nx; hy = ny; }
            }
        }
    
        // ---- MAS FIESTAS (7-11-2026: «Pon mas festividades») ----
        // La bandera ondeando arriba a la izquierda (24 de febrero, 15 y 16 de septiembre, 20 de noviembre).
        private void Bandera(float W, float yh, double t, float alfa)
        {
            float x0 = W * 0.06f, y0 = yh * 0.16f, bw = 190 * k, bh = 112 * k, ola = 7 * k * (0.4f + 0.6f * viveza);
            d.Rect(x0 - 4 * k, y0 - 6 * k, 3 * k, bh + 120 * k, new Color4(0.75f, 0.72f, 0.65f, 0.85f * alfa));   // el asta
            const int n = 30;
            for (int i = 0; i < n; i++)
            {
                float u0 = i / (float)n, u1 = (i + 1) / (float)n;
                float o0 = (float)Math.Sin(u0 * 7 - t * 2.4) * ola * u0, o1 = (float)Math.Sin(u1 * 7 - t * 2.4) * ola * u1;
                float luz = 0.82f + 0.18f * (float)Math.Cos(u0 * 7 - t * 2.4);
                Color4 c = u0 < 1f / 3 ? Color4.Hex(0x0B8A4A) : u0 < 2f / 3 ? Color4.Hex(0xF2F2F2) : Color4.Hex(0xD21F2E);
                c = new Color4(c.R * luz, c.G * luz, c.B * luz, 0.92f * alfa);
                float xa = x0 + u0 * bw, xb = x0 + u1 * bw + 0.5f;
                d.Cuatro(xa, y0 + o0, c, xb, y0 + o1, c, xb, y0 + bh + o1, c, xa, y0 + bh + o0, c);
            }
            // el escudo, sencillo: el aguila en su nopal (un circulo cafe con su verde)
            float cx = x0 + bw / 2, oc = (float)Math.Sin(0.5 * 7 - t * 2.4) * ola * 0.5f, cy = y0 + bh / 2 + oc;
            d.Brillo(cx, cy, 15 * k, new Color4(0.55f, 0.36f, 0.16f, 0.9f * alfa));
            d.Brillo(cx, cy + 9 * k, 9 * k, new Color4(0.2f, 0.55f, 0.25f, 0.7f * alfa));
        }
        // Farolitos de papel que suben despacio con su luz (Candelaria y posadas).
        private void Farolitos(float W, float yh, double t, float alfa, int n)
        {
            for (int i = 0; i < n; i++)
            {
                double u = (Hs(i, 131) + t * 0.008 * (0.6 + Hs(i, 132))) % 1.0;
                float x = Hs(i, 133) * W + (float)Math.Sin(t * 0.3 + i) * 25 * k, y = yh - (float)u * yh * 0.9f, s = (0.8f + 0.5f * Hs(i, 134)) * k;
                float a = (float)Math.Min(1, Math.Min(u * 6, (1 - u) * 3)) * alfa, fl = 0.85f + 0.15f * (float)Math.Sin(t * 7 + i * 2.1);
                d.Brillo(x, y, 30 * s, new Color4(1f, 0.6f, 0.2f, 0.35f * a * fl));
                d.Redondo(x - 8 * s, y - 10 * s, 16 * s, 20 * s, 6 * s, new Color4(1f, 0.72f * fl, 0.32f, 0.85f * a));
                d.Rect(x - 8 * s, y - 10 * s, 16 * s, 2 * s, new Color4(0.5f, 0.25f, 0.1f, 0.7f * a));
                d.Brillo(x, y + 3 * s, 5 * s, new Color4(1f, 0.95f, 0.75f, 0.9f * a * fl));
            }
        }
        // Treboles verdes que caen dando vueltas (San Patricio).
        private void Treboles(float W, float yh, double t, float alfa, int n)
        {
            float alto = yh + 40 * k;
            for (int i = 0; i < n; i++)
            {
                float v = (18 + 18 * Hs(i, 141)) * k, y = (float)((Hs(i, 142) * alto + t * v) % alto) - 20 * k;
                float x = Hs(i, 143) * W + (float)Math.Sin(t * 0.7 + i) * 24 * k, r = (4 + 3 * Hs(i, 144)) * k;
                double ang = t * (0.6 + Hs(i, 145)) + i;
                Color4 c = (Hs(i, 146) < 0.5f ? Color4.Hex(0x2FB457) : Color4.Hex(0x5ED97A)).ConAlfa(0.85f * alfa);
                for (int h = 0; h < 3; h++)
                {
                    double a = ang + h * 2.094;
                    Circulo(x + (float)Math.Cos(a) * r * 0.85f, y + (float)Math.Sin(a) * r * 0.85f, r * 0.62f, c);
                }
                double ta = ang + 1.047 + Math.PI;
                d.Linea(x, y, x + (float)Math.Cos(ta) * r * 2f, y + (float)Math.Sin(ta) * r * 2f, 1.4f * k, c);
            }
        }
        // Mariposas que revolotean: de colores (primavera) o monarcas (Dia de Muertos).
        private static readonly Color4[] ColoresMariposa = { Color4.Hex(0xFFD23F), Color4.Hex(0x4FC3F7), Color4.Hex(0xFF7AB6), Color4.Hex(0xB388FF), Color4.Hex(0x7CE38B) };
        private void Mariposas(float W, float yh, double t, float alfa, int n, bool monarca, int sem)
        {
            for (int i = 0; i < n; i++)
            {
                float vx = (14 + 18 * Hs(i, sem)) * k * (Hs(i, sem + 1) < 0.5f ? 1 : -1), largo = W + 80 * k;
                float x = (float)((((Hs(i, sem + 2) * largo + t * vx) % largo) + largo) % largo) - 40 * k;
                float y = yh * (0.2f + 0.6f * Hs(i, sem + 3)) + (float)Math.Sin(t * 1.3 + i * 2) * 30 * k, s = (0.9f + 0.5f * Hs(i, sem + 4)) * k;
                float ab = 0.25f + 0.75f * Math.Abs((float)Math.Sin(t * 9 + i * 1.3));   // aleteo: cuanto se ven las alas
                Color4 c = monarca ? Color4.Hex(0xFF8C1A) : ColoresMariposa[i % ColoresMariposa.Length];
                c = c.ConAlfa(0.9f * alfa); Color4 borde = new Color4(0.08f, 0.06f, 0.06f, 0.85f * alfa);
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    float wx = lado * 11 * s * ab, wx2 = lado * 8 * s * ab;
                    if (monarca) { d.Triangulo(x, y - 1 * s, x + wx * 1.12f, y - 10 * s, x + wx * 1.12f, y + 2 * s, borde); }
                    d.Triangulo(x, y - 1 * s, x + wx, y - 9 * s, x + wx, y + 1 * s, c);
                    d.Triangulo(x, y + 1 * s, x + wx2, y + 2 * s, x + wx2 * 0.7f, y + 9 * s, c);
                }
                d.Rect(x - 0.8f * s, y - 5 * s, 1.6f * s, 10 * s, borde);
            }
        }
        // Notas musicales que flotan y suben (Santa Cecilia, el dia de los musicos y del mariachi).
        private static readonly Color4[] ColoresNotas = { Color4.Hex(0xFFE08A), Color4.Hex(0x9EE7FF), Color4.Hex(0xFFA8E0), Color4.Hex(0xB9FFB0), Color4.Hex(0xFFFFFF) };
        private void Notas(float W, float yh, double t, float alfa, int n)
        {
            for (int i = 0; i < n; i++)
            {
                double u = (Hs(i, 151) + t * 0.012 * (0.6 + Hs(i, 152))) % 1.0;
                float x = Hs(i, 153) * W + (float)Math.Sin(t * 0.9 + i) * 28 * k, y = yh - (float)u * (yh + 30 * k), s = (1f + 0.6f * Hs(i, 154)) * k;
                float a = (float)Math.Sin(u * Math.PI) * 0.85f * alfa;
                Color4 c = ColoresNotas[i % ColoresNotas.Length].ConAlfa(a);
                d.Brillo(x, y, 14 * s, c.ConAlfa(0.18f * a));
                Circulo(x, y, 4.5f * s, c);
                d.Rect(x + 3.4f * s, y - 20 * s, 1.6f * s, 20 * s, c);
                if (Hs(i, 155) < 0.5f)
                {   // la corchea: su banderita
                    d.Linea(x + 4.2f * s, y - 20 * s, x + 11 * s, y - 13 * s, 1.8f * s, c);
                }
                else
                {   // dos unidas
                    Circulo(x + 13 * s, y + 3 * s, 4.5f * s, c);
                    d.Rect(x + 16.4f * s, y - 17 * s, 1.6f * s, 20 * s, c);
                    d.Linea(x + 4.2f * s, y - 20 * s, x + 17.2f * s, y - 17 * s, 3f * s, c);
                }
            }
        }
        // Piñatas de estrella de siete picos colgadas arriba, que se mecen (posadas).
        private static readonly Color4[] ColoresPinata = { Color4.Hex(0xFF3B6B), Color4.Hex(0xFFC93C), Color4.Hex(0x3CC8FF), Color4.Hex(0x7CE04C), Color4.Hex(0xB46BFF), Color4.Hex(0xFF8A3C), Color4.Hex(0xFFFFFF) };
        private void Pinatas(float W, float H, double t, float alfa)
        {
            for (int p = 0; p < 3; p++)
            {
                float ax = W * (0.22f + 0.28f * p), ay = H * 0.10f, largo = H * (0.13f + 0.05f * (p % 2));
                double ang = Math.Sin(t * 0.9 + p * 1.7) * 0.18 * (0.4 + 0.6 * viveza);
                float cx = ax + (float)Math.Sin(ang) * largo, cy = ay + (float)Math.Cos(ang) * largo, r = 16 * k;
                d.Linea(ax, ay, cx, cy - r, 1.2f * k, new Color4(0.85f, 0.8f, 0.7f, 0.7f * alfa));
                for (int j = 0; j < 7; j++)
                {
                    double a = j / 7.0 * Math.PI * 2 - Math.PI / 2 + ang;
                    float ex = cx + (float)Math.Cos(a) * r * 2.4f, ey = cy + (float)Math.Sin(a) * r * 2.4f;
                    float bx0 = cx + (float)Math.Cos(a - 0.32) * r * 0.9f, by0 = cy + (float)Math.Sin(a - 0.32) * r * 0.9f, bx1 = cx + (float)Math.Cos(a + 0.32) * r * 0.9f, by1 = cy + (float)Math.Sin(a + 0.32) * r * 0.9f;
                    Color4 c = ColoresPinata[(j + p * 2) % ColoresPinata.Length].ConAlfa(0.92f * alfa);
                    d.Triangulo(bx0, by0, ex, ey, bx1, by1, c);
                    // las tiritas de papel de la punta
                    for (int q = 0; q < 2; q++) d.Linea(ex, ey, ex + (float)Math.Cos(a + 0.5 * (q - 0.5) + Math.Sin(t * 3 + j) * 0.3) * 9 * k, ey + (float)Math.Sin(a + 0.5 * (q - 0.5)) * 9 * k + 5 * k, 1.5f * k, c);
                }
                Circulo(cx, cy, r, Color4.Hex(0xF4E1B8).ConAlfa(0.95f * alfa));
                Circulo(cx, cy, r * 0.6f, ColoresPinata[p * 3 % 7].ConAlfa(0.9f * alfa));
            }
        }
}
}

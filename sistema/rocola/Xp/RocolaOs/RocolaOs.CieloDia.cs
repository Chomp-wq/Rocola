// EL CIELO VIVO DE ROCOLA OS, NUCLEO: DN1 (EL CIELO SIGUE AL SOL) Y DN5 (LA RAYA DEL HORIZONTE CON LA LUZ DE LA HORA). PLAN_CIELO_VIVO.md §5.
//
// LAS PALETAS de las maquetas que aprobo el dueno (y en fraccion del alto, pensadas para el horizonte de XP en 0,868; con otro horizonte se
// corren: lo de arriba se encoge o se estira hasta el horizonte y el suelo va de ahi hasta abajo). La noche es la de XP exacta: con el sol bajo
// -12 grados no se pinta nada (el gancho deja que XP pinte su cielo de siempre, con su tinte por la hora). Entre una paleta y otra se mezcla
// por la altura del sol, sin saltos (Pesos). El color del genero de XP abajo se conserva: en el suelo (bajo el horizonte) se mezcla al 35 %.
// REGLA 3: hasta 0,848 del alto ninguna paleta pasa de luminancia relativa 0,17 (lo comprueba la bateria, con las cuentas y en las capturas).
//
// Lo que se pinta: el degradado del cielo por la tarjeta (DrawSkyN, sin mezcla: lo mismo que el de XP), en los dos caminos de XP (el directo,
// CieloDirecto, y el de la imagen intermedia, CieloAmplio: el mismo cielo); las nebulosas de XP de dia mas tenues y hechas «velo» (cirros); el
// brillo de las estrellas de XP por la fase; y la raya del horizonte de XP (la de la aurora) con el color de la fase (DN5). Nada mas.
using System;
using System.Globalization;

namespace Rocola
{
    internal static class RocolaOsCieloDia
    {
        // ---------- las paletas (§5) ----------
        internal const int Noche = 0, Alba = 1, Amanecer = 2, Manana = 3, Mediodia = 4, Dorada = 5, Atardecer = 6, Anochecer = 7, Fases = 8;
        internal static readonly string[] Nombres = { "noche", "alba", "amanecer", "manana", "mediodia", "dorada", "atardecer", "anochecer" };
        private static float[] Y(params float[] y) { return y; }
        private static uint[] C(params uint[] c) { return c; }
        internal static readonly float[][] ParadasY =
        {
            null,
            Y(0f, 0.42f, 0.80f, 0.853f, 0.862f, 0.868f, 0.876f, 1f),
            Y(0f, 0.40f, 0.80f, 0.853f, 0.862f, 0.868f, 0.876f, 1f),
            Y(0f, 0.42f, 0.80f, 0.853f, 0.862f, 0.868f, 0.876f, 1f),
            Y(0f, 0.42f, 0.80f, 0.853f, 0.862f, 0.868f, 0.876f, 1f),
            Y(0f, 0.42f, 0.80f, 0.853f, 0.862f, 0.868f, 0.876f, 1f),
            Y(0f, 0.40f, 0.78f, 0.853f, 0.862f, 0.868f, 0.876f, 1f),
            Y(0f, 0.42f, 0.80f, 0.853f, 0.862f, 0.868f, 0.876f, 1f),
        };
        internal static readonly uint[][] ParadasC =
        {
            null,
            C(0x0b1233, 0x1d2a63, 0x34367c, 0x4a4088, 0x7a5aa6, 0xa07ac0, 0x3a2d5a, 0x221a38),
            C(0x142a66, 0x2c4690, 0x4a4a8e, 0x6a5490, 0xd08c88, 0xf8b490, 0x6a4e6c, 0x30263c),
            C(0x0e3778, 0x21529a, 0x2b5d9c, 0x3165a4, 0x7eb0e0, 0xacd4f4, 0x3a6290, 0x223c5c),
            C(0x0b306f, 0x1f4e98, 0x295a9c, 0x3064a4, 0x7cb0e2, 0xacd6f6, 0x355e8e, 0x203a5a),
            C(0x17336e, 0x33569a, 0x4a5290, 0x6a5a86, 0xd49a68, 0xf6c274, 0x6a5050, 0x322a3a),
            C(0x131f52, 0x33306e, 0x5e3672, 0x7a3c6a, 0xe06a50, 0xffa054, 0x5a3046, 0x2a1c34),
            C(0x080c28, 0x19225c, 0x352a6a, 0x4a2e6a, 0x9a4874, 0xc0547c, 0x2f1d3c, 0x181226),
        };
        // la raya del horizonte (DN5), cuanto se ven las estrellas y la nebulosa de XP, y el «velo» (la nebulosa hecha cirros) de cada fase
        internal static readonly uint[] RayaFase = { 0, 0xa888d8, 0xffc8a0, 0xe6f4ff, 0xf0f8ff, 0xffd590, 0xff9a58, 0xe07098 };
        internal static readonly float[] EstrellasFase = { 1f, 0.45f, 0.08f, 0f, 0f, 0f, 0.12f, 0.55f };
        internal static readonly float[] NebulosaFase = { 1f, 0.55f, 0.35f, 0.30f, 0.28f, 0.32f, 0.35f, 0.7f };
        internal static readonly float[] VeloFase = { 0f, 0.08f, 0.2f, 0.34f, 0.36f, 0.24f, 0.16f, 0f };
        internal const float HorizonteMaqueta = 0.868f, SueloDesde = 0.876f, SueloGenero = 0.35f, Regla3Hasta = 0.848f, Regla3Luz = 0.17f;
        // (madrugada: en las maquetas, estrellas 1,1 y nebulosa 0,9; aqui no: la regla 1 manda que la noche sea la de XP bit a bit)

        // ---------- la fase del dia por la altura del sol (§3) ----------
        // Puntos de anclaje (grados): en cada uno manda una paleta, y entre dos se mezcla en linea recta. La noche (bajo -12) es la de XP. Por la
        // manana: alba -12..-4, amanecer -4..+3, manana +3..+35, mediodia sobre +35; por la tarde: dorada +3..+15, atardecer -4..+3, anochecer -12..-4.
        // Las anclas van al centro de cada tramo (y el mediodia se mezcla de 30 a 40 grados: si el sol no llega a 35, casi no se nota: el mediodia es
        // casi la manana).
        private static readonly float[] AnclaManana = { -12f, -8f, -0.5f, 9f, 30f, 40f };
        private static readonly int[] FaseManana = { Noche, Alba, Amanecer, Manana, Manana, Mediodia };
        private static readonly float[] AnclaTarde = { -12f, -8f, -0.5f, 9f, 15f, 30f, 40f };
        private static readonly int[] FaseTarde = { Noche, Anochecer, Atardecer, Dorada, Manana, Manana, Mediodia };
        internal static void Pesos(double alt, bool sube, float[] pesos)
        {
            Array.Clear(pesos, 0, pesos.Length);
            float[] a = sube ? AnclaManana : AnclaTarde; int[] fz = sube ? FaseManana : FaseTarde;
            if (!(alt > a[0])) { pesos[fz[0]] = 1f; return; }   // (tambien NaN: la noche de XP)
            if (alt >= a[a.Length - 1]) { pesos[fz[fz.Length - 1]] = 1f; return; }
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (alt < a[i] || alt >= a[i + 1]) continue;
                float t = (float)((alt - a[i]) / (a[i + 1] - a[i]));
                pesos[fz[i]] += 1f - t; pesos[fz[i + 1]] += t;
                return;
            }
        }
        // las mezclas de la foto (estrellas, nebulosa, velo, raya, la fase de mas peso)
        internal static void Mezcla(RocolaOsCieloFoto f)
        {
            float e = 0, n = 0, v = 0, r = 0, g = 0, b = 0, dia = 0, mx = -1; int fase = 0;
            for (int i = 0; i < Fases; i++)
            {
                float w = f.Pesos[i];
                e += w * EstrellasFase[i]; n += w * NebulosaFase[i]; v += w * VeloFase[i];
                if (i > 0) { r += w * ((RayaFase[i] >> 16) & 255); g += w * ((RayaFase[i] >> 8) & 255); b += w * (RayaFase[i] & 255); dia += w; }
                if (w > mx) { mx = w; fase = i; }
            }
            f.Noche = f.Pesos[0];
            f.Estrellas = f.Noche >= 1f ? 1f : e; f.Nebulosa = f.Noche >= 1f ? 1f : n; f.Velo = f.Noche >= 1f ? 0f : v;
            f.Raya = dia > 0.0001f ? ((uint)Math.Round(r / dia) << 16) | ((uint)Math.Round(g / dia) << 8) | (uint)Math.Round(b / dia) : 0u;
            f.Fase = Nombres[fase];
        }
        internal static void RayaMezcla(uint a, uint b, float k, out float r, out float g, out float bl)
        {
            if (a == 0) a = b; if (b == 0) b = a;
            r = ((a >> 16) & 255) + ((((b >> 16) & 255) - (float)((a >> 16) & 255)) * k);
            g = ((a >> 8) & 255) + ((((b >> 8) & 255) - (float)((a >> 8) & 255)) * k);
            bl = (a & 255) + (((b & 255) - (float)(a & 255)) * k);
        }

        // ---------- lo de cada cuadro (en la pantalla) ----------
        internal static bool DN1Cuadro, DN5Cuadro;                 // las ideas, en este cuadro
        internal static int FirmaCielo, FirmaAurora;              // cambian si cambia lo que pinta DN1 (o el velo) o la raya de DN5
        internal static int FirmaAuroraExtra;                     // (para quien mas cambie la aurora: FI1 la sube cuando cambian sus colores)
        internal static int FirmaCieloExtra;                      // (para quien tina el cielo: los tintes la suben cuando cambia lo suyo)
        // (el nucleo se cayo: lo de XP desde el siguiente cuadro; las firmas cambian para que la aurora y la imagen intermedia se rehagan)
        internal static void Apaga() { DN1Cuadro = DN5Cuadro = false; unchecked { FirmaCielo += 7919; FirmaAurora += 7919; } }
        internal static void AlCuadro(RocolaOsCieloFoto f)
        {
            DN1Cuadro = f.DN1; DN5Cuadro = f.DN5;
            float noche = RocolaOsCielo.NocheCuadro;
            unchecked
            {
                int h = DN1Cuadro && noche < 1f ? 17 : 3;
                if (DN1Cuadro && noche < 1f)
                {
                    for (int i = 0; i < Fases; i++) h = h * 31 + (int)Math.Round(RocolaOsCielo.PesosCuadro[i] * 2048f);
                    h = h * 31 + (int)Math.Round(RocolaOsCielo.VeloCuadro * 2048f) * 7 + (int)Math.Round(RocolaOsCielo.NebulosaCuadro * 2048f);
                }
                // el horizonte de la escena (sube con el reposo): lo usan DN1 y los tintes de los grupos (Tine lo recibe), asi que entra siempre que
                // se pinte el cielo de Rocola OS, no solo con DN1
                if (Pinta) h = h * 31 + (int)Math.Round(RocolaOsCielo.Escena.Horizonte * 4f) + 1;
                h = h * 31 + FirmaCieloExtra + RocolaOsCielo.TintesVersion;
                FirmaCielo = h;
                int a = DN5Cuadro && noche < 1f ? 101 : 7;
                if (DN5Cuadro && noche < 1f)
                    a = a * 31 + (int)Math.Round(RocolaOsCielo.Escena.Reposo * 32f) * 104729 + (int)Math.Round(RocolaOsCielo.RayaR) * 65536 + (int)Math.Round(RocolaOsCielo.RayaG) * 256 + (int)Math.Round(RocolaOsCielo.RayaB) + (int)Math.Round((1f - noche) * 512f) * 7919;
                FirmaAurora = a * 31 + FirmaAuroraExtra;
            }
        }

        // ================================================================ DN1: EL DEGRADADO DEL CIELO
        private const int MaxParadas = 64;   // (13 fronteras de XP con C1 + 8 por paleta activa: 45 con 4 a la vez en un salto; los cuadros, 64 x 6 vertices)
        private static readonly float[] ys = new float[MaxParadas];
        private static readonly float[] cr = new float[MaxParadas], cg = new float[MaxParadas], cb = new float[MaxParadas];
        private static readonly float[] xpY = new float[16]; private static readonly uint[] xpC = new uint[16];
        // SI HAY QUE PINTAR EL CIELO NUEVO este cuadro (DN1 encendida y no es la noche cerrada, o algun tinte de otro grupo activo)
        internal static bool Pinta { get { return RocolaOsCielo.Va && ((DN1Cuadro && RocolaOsCielo.NocheCuadro < 1f) || RocolaOsCielo.HayTintes); } }
        // ARMA EL CIELO en 'v' (cuadros de 6 vertices, sin mezcla) con las tres paradas de XP de ESTE cuadro (top, mid, bot: con el tinte por la hora
        // y C2 ya puestos: la noche de XP), el color del genero sin ese tinte (genero: el del suelo de dia), el corte (midY), el horizonte de la
        // escena y lo de la foto. 'suave' = el cielo de doce franjas de XP (C1). 'extra': la
        // imagen intermedia lleva una franja mas abajo con el color de abajo. 'desde' > 0: las franjas que empiezan ahi o mas abajo no se mandan
        // (las tapa el estado opaco, como hace XP). Devuelve cuantos triangulos.
        internal static int Arma(Tarjeta.Vtx[] v, float ancho, float h, float midY, uint top, uint mid, uint bot, uint genero, float extra, bool suave, float horizonteY, float desde)
        {
            if (h < 2f || v == null) return 0;
            // las franjas de XP (las fronteras y sus colores), para mezclar con la noche de XP sin escalon
            int nx = 0;
            float x1 = midY / h;
            if (suave)
            {
                for (int tramo = 0; tramo < 2; tramo++)
                {
                    float ya = tramo == 0 ? 0f : midY, yb = tramo == 0 ? midY : h;
                    for (int k = 0; k < GpuStarfield.CieloFranjas; k++)
                    {
                        float y0 = ya + (yb - ya) * k / GpuStarfield.CieloFranjas;
                        if (nx == 0 || y0 > xpY[nx - 1]) { xpY[nx] = y0; xpC[nx] = Luz.ColorCielo(y0 / h, x1, top, mid, bot); nx++; }
                    }
                }
                xpY[nx] = h; xpC[nx] = Luz.ColorCielo(1f, x1, top, mid, bot); nx++;
            }
            else { xpY[0] = 0; xpC[0] = top; xpY[1] = midY; xpC[1] = mid; xpY[2] = h; xpC[2] = bot; nx = 3; }
            // las fronteras: las de XP y las de las paletas (corridas al horizonte de la escena)
            float H = horizonteY > 1f && horizonteY < h ? horizonteY / h : HorizonteMaqueta;
            int n = 0;
            for (int i = 0; i < nx; i++) Agrega(ref n, xpY[i]);
            for (int p = 1; p < Fases; p++)
            {
                if (RocolaOsCielo.PesosCuadro[p] <= 0f) continue;
                float[] py = ParadasY[p];
                for (int i = 0; i < py.Length; i++) Agrega(ref n, Corre(py[i], H) * h);
            }
            Array.Sort(ys, 0, n);
            // el color de cada frontera
            float noche = RocolaOsCielo.NocheCuadro;
            float gr = (genero >> 16) & 255, gg = (genero >> 8) & 255, gb = genero & 255;   // (sin el tinte por la hora de XP)
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                if (m > 0 && ys[i] - ys[m - 1] < 0.25f) continue;   // (casi la misma: una sola)
                float y = ys[i], r, g, b;
                ColorXp(y, nx, out r, out g, out b);
                if (noche < 1f && DN1Cuadro)
                {
                    float yf = y / h, dr = 0, dg = 0, db = 0, dw = 0;
                    for (int p = 1; p < Fases; p++)
                    {
                        float w = RocolaOsCielo.PesosCuadro[p];
                        if (w <= 0f) continue;
                        float pr, pg, pb; ColorPaleta(p, yf, H, out pr, out pg, out pb);
                        dr += w * pr; dg += w * pg; db += w * pb; dw += w;
                    }
                    if (dw > 0f)
                    {
                        dr /= dw; dg /= dw; db /= dw;
                        float s = Suelo(yf, H);   // el color del genero de XP en el suelo (35 %)
                        if (s > 0f) { dr += (gr - dr) * s; dg += (gg - dg) * s; db += (gb - db) * s; }
                        float kd = 1f - noche;
                        r = r * noche + dr * kd; g = g * noche + dg * kd; b = b * noche + db * kd;
                    }
                }
                RocolaOsCielo.Tine(y / h, y, H * h, ref r, ref g, ref b);
                ys[m] = y; cr[m] = r; cg[m] = g; cb[m] = b; m++;
            }
            // los cuadros
            int vi = 0;
            for (int i = 0; i + 1 < m && vi + 6 <= v.Length; i++)
            {
                if (desde > 0f && ys[i] >= desde && i > 0) break;
                uint c0 = Rgb(cr[i], cg[i], cb[i]), c1 = Rgb(cr[i + 1], cg[i + 1], cb[i + 1]);
                Cuadro(v, vi, 0f, ys[i], ancho, ys[i + 1], c0, c1); vi += 6;
            }
            if (extra > 0f && m > 0 && vi + 6 <= v.Length)
            {
                uint c = Rgb(cr[m - 1], cg[m - 1], cb[m - 1]);
                Cuadro(v, vi, 0f, h, ancho, h + extra, c, c); vi += 6;
            }
            return vi / 3;
        }
        private static void Agrega(ref int n, float y) { if (n < MaxParadas) ys[n++] = y; }
        // una parada de la maqueta (horizonte en 0,868) corrida a otro horizonte H (fracciones del alto)
        internal static float Corre(float y, float H)
        {
            if (Math.Abs(H - HorizonteMaqueta) < 1e-6f) return y;
            return y <= HorizonteMaqueta ? y * H / HorizonteMaqueta : H + (y - HorizonteMaqueta) / (1f - HorizonteMaqueta) * (1f - H);
        }
        // cuanto del color del genero va en el suelo a la altura yf (0 sobre el horizonte; sube hasta 35 % en la parada 0,876)
        private static float Suelo(float yf, float H)
        {
            float y0 = Corre(HorizonteMaqueta, H), y1 = Corre(SueloDesde, H);
            if (yf <= y0) return 0f;
            if (yf >= y1) return SueloGenero;
            return SueloGenero * (yf - y0) / Math.Max(1e-6f, y1 - y0);
        }
        // el color de XP a la altura y: la recta entre las fronteras de sus franjas (lo que hace la tarjeta dentro de cada franja de XP)
        private static void ColorXp(float y, int nx, out float r, out float g, out float b)
        {
            int i = 0;
            while (i + 2 < nx && y > xpY[i + 1]) i++;
            float y0 = xpY[i], y1 = xpY[i + 1];
            float t = y1 > y0 ? Math.Max(0f, Math.Min(1f, (y - y0) / (y1 - y0))) : 0f;
            uint a = xpC[i], c = xpC[i + 1];
            r = ((a >> 16) & 255) + ((((c >> 16) & 255) - (float)((a >> 16) & 255)) * t);
            g = ((a >> 8) & 255) + ((((c >> 8) & 255) - (float)((a >> 8) & 255)) * t);
            b = (a & 255) + (((c & 255) - (float)(a & 255)) * t);
        }
        // el color de una paleta de dia a la altura yf (fraccion del alto) con el horizonte H
        internal static void ColorPaleta(int p, float yf, float H, out float r, out float g, out float b)
        {
            float[] py = ParadasY[p]; uint[] pc = ParadasC[p];
            int i = 0;
            while (i + 2 < py.Length && yf > Corre(py[i + 1], H)) i++;
            float y0 = Corre(py[i], H), y1 = Corre(py[i + 1], H);
            float t = y1 > y0 ? Math.Max(0f, Math.Min(1f, (yf - y0) / (y1 - y0))) : 0f;
            uint a = pc[i], c = pc[i + 1];
            r = ((a >> 16) & 255) + ((((c >> 16) & 255) - (float)((a >> 16) & 255)) * t);
            g = ((a >> 8) & 255) + ((((c >> 8) & 255) - (float)((a >> 8) & 255)) * t);
            b = (a & 255) + (((c & 255) - (float)(a & 255)) * t);
        }
        private static uint Rgb(float r, float g, float b) { return 0xFF000000u | ((uint)B(r) << 16) | ((uint)B(g) << 8) | (uint)B(b); }
        private static int B(float v) { return v <= 0f ? 0 : v >= 255f ? 255 : (int)(v + 0.5f); }
        private static void Cuadro(Tarjeta.Vtx[] v, int o, float x0, float y0, float x1, float y1, uint c0, uint c1)
        {
            V(ref v[o + 0], x0, y0, c0); V(ref v[o + 1], x1, y0, c0); V(ref v[o + 2], x1, y1, c1);
            V(ref v[o + 3], x0, y0, c0); V(ref v[o + 4], x1, y1, c1); V(ref v[o + 5], x0, y1, c1);
        }
        private static void V(ref Tarjeta.Vtx v, float x, float y, uint c) { v.X = x; v.Y = y; v.Z = 0f; v.Rhw = 1f; v.Color = c; v.U = 0f; v.V = 0f; }

        // LA LUMINANCIA RELATIVA (sRGB, la de la regla 3) de un color
        internal static double Luminancia(float r, float g, float b)
        {
            return 0.2126 * Lin(r / 255.0) + 0.7152 * Lin(g / 255.0) + 0.0722 * Lin(b / 255.0);
        }
        private static double Lin(double v) { return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        // LO MAS CLARO DE UNA PALETA de 0 a 'hasta' (fraccion del alto), con el horizonte H: la regla 3 (la bateria)
        internal static double LuzMaxima(int p, float H, float hasta)
        {
            double mx = 0;
            for (int i = 0; i <= 1000; i++)
            {
                float yf = hasta * i / 1000f, r, g, b;
                ColorPaleta(p, yf, H, out r, out g, out b);
                double l = Luminancia(r, g, b); if (l > mx) mx = l;
            }
            return mx;
        }

        // ================================================================ DN1: LAS NEBULOSAS DE XP DE DIA (gancho g124 y g125)
        // El color y el alfa de una pasada de la nebulosa (o de la via lactea): de dia mas tenue (alfa por NebulosaFase) y «velo»: el color se
        // aclara y pierde color, como cirros (§5: nebulosa x (1 - velo) + su luminancia x 3,2 en #c7d6f2 x velo x 0,55). De noche, el mismo.
        internal static uint Niebla(uint argb)
        {
            float noche = RocolaOsCielo.NocheCuadro;
            if (!DN1Cuadro || noche >= 1f) return argb;
            float kn = RocolaOsCielo.NebulosaCuadro, velo = RocolaOsCielo.VeloCuadro;
            float a = (argb >> 24) & 255, r = (argb >> 16) & 255, g = (argb >> 8) & 255, b = argb & 255;
            if (velo > 0f)
            {
                float l = Math.Min(1f, (r + g + b) / (3f * 255f) * 3.2f) * 255f * 0.55f;
                r = r * (1f - velo) + l * 0.78f * velo; g = g * (1f - velo) + l * 0.84f * velo; b = b * (1f - velo) + l * 0.95f * velo;
            }
            a *= kn;
            return ((uint)B(a) << 24) | ((uint)B(r) << 16) | ((uint)B(g) << 8) | (uint)B(b);
        }
        // EL BRILLO DE LAS ESTRELLAS DE XP por la fase (gancho g126): de dia no se ven; al alba y al anochecer, a medias. 1 de noche. Con DN4
        // (estrellas_anochecer) el grupo del sol lo pone estrella por estrella (gancho g127) y aqui va 1.
        internal static float Estrellas
        {
            get
            {
                if (!DN1Cuadro || RocolaOsCielo.NocheCuadro >= 1f || RocolaOsCielo.EstrellasPorEstrella) return 1f;
                return Math.Max(0f, Math.Min(1f, RocolaOsCielo.EstrellasCuadro));
            }
        }

        // ================================================================ DN5: LA RAYA DEL HORIZONTE (gancho g128, por columna de la aurora)
        // La raya de XP (la que lleva el pie de la aurora: color c, brillo li, sumada) con la luz de la fase: como en las maquetas, la misma raya
        // (su forma, su resplandor de 34 px hacia arriba y 11 hacia abajo) en el color de la fase, con 2,2 veces su luz media (el promedio de sus tres
        // canales), algo mas fuerte en el centro; de noche, la de XP. Se mezcla con la de XP por el peso del dia. En el reposo con musica (la raya sube
        // a y=525 y el panel AHORA SUENA va debajo) la luz baja a 1,4 veces por la regla 3: con 2,2 el resplandor pasaba de 0,17 de luminancia justo
        // encima de la raya (0,18-0,19 medido de dia, el blanco a 4,4:1); con 1,4, 0,15-0,16 (el blanco a 5,0-5,3:1). Fuera del reposo, 2,2 exacto.
        internal const float RayaLuz = 2.2f, RayaLuzReposo = 1.4f;
        internal static void Raya(uint[] colores, float[] brillos, int i, float fx)
        {
            float noche = RocolaOsCielo.NocheCuadro;
            if (!DN5Cuadro || noche >= 1f) return;
            uint p = colores[i];
            float li = brillos[i];
            float pr = (p >> 16) & 255, pg = (p >> 8) & 255, pb = p & 255;
            float rr = RocolaOsCielo.RayaR, rg = RocolaOsCielo.RayaG, rb = RocolaOsCielo.RayaB;
            float mx = Math.Max(rr, Math.Max(rg, rb));
            if (mx < 1f) return;
            float media = (pr + pg + pb) / (3f * 255f);
            float dx = (fx - 0.5f) / 0.62f, centro = 0.6f + 0.4f * (float)Math.Exp(-dx * dx);
            float luz = RayaLuz + (RayaLuzReposo - RayaLuz) * Math.Max(0f, Math.Min(1f, RocolaOsCielo.Escena.Reposo));
            float a = li * media * luz * (mx / 255f) * centro;   // la luz que lleva (sumada): color normalizado x alfa
            float nr = rr / mx * 255f, ng = rg / mx * 255f, nb = rb / mx * 255f;
            if (a > 1f) a = 1f;
            float d = 1f - noche;
            colores[i] = ((uint)B(pr + (nr - pr) * d) << 16) | ((uint)B(pg + (ng - pg) * d) << 8) | (uint)B(pb + (nb - pb) * d);
            float nl = li + (a - li) * d;
            brillos[i] = nl < 0f ? 0f : (nl > 1f ? 1f : nl);
        }
    }
}

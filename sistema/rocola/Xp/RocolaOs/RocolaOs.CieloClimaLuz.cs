// EL CIELO VIVO DE ROCOLA OS, GRUPO «NUBES Y CLIMA»: EL CLIMA TOCA LO QUE YA HAY (27-11-2026; presentacion aprobada «Clima en la rocola»;
// FASE11_cielo-clima.md, seccion 10). El cielo de XP se queda igual: aqui solo se hace que el clima REACCIONE a sus luces y a su horizonte.
//   - LAS LUCES (RocolaOsClimaLuces): la luna, el sol, la estrella de XP, las luces de las fiestas sobre la raya del horizonte (las pone FI2) y los
//     fuegos (los pone FI4), con su sitio, su radio y su color; con ellas la lluvia BRILLA al pasar delante de una luz, las luces tienen HALO con
//     la neblina y la lluvia, se REFLEJAN en el piso mojado y los fuegos alumbran las nubes. Tambien las ZONAS CON TEXTO de cada escena (el
//     escaparate, la pared, el reposo con la letra): ahi lo del clima se atenua para que el texto siga leyendose (regla 3).
//   - EL PISO MOJADO (capa TrasAurora, tras el reflejo de la aurora de XP): con lluvia, el reflejo de la aurora (la malla que dibuja
//     DibujaReflejoAurora) se dibuja otra vez, mas claro y con ondas que se mueven; un brillo de piso en el color de la aurora (de la raya de dia),
//     ondas que se abren (anillos aplastados), los reflejos de las luces del horizonte (rayitas que tiemblan) y, con un relampago, un instante mas
//     de luz.
//   - LAS SALPICADURAS: gotitas pequenas sobre la raya del horizonte (una corona aplastada y dos gotas que saltan).
//   - EL FILO DE NIEVE: con nieve, una raya blanca con grumos sobre la raya del horizonte y un resplandor tenue encima.
//   - LOS HALOS: con neblina, lluvia o nieve, las luces (las del horizonte, el sol, la luna, la estrella) se rodean de un halo.
//   - LOS FUEGOS EN LAS NUBES: los fuegos de Ano Nuevo y del Grito alumbran las nubes cercanas, por debajo si estan debajo (la forma de la nube, sumada).
// Todo con arreglos fijos (sin memoria nueva por cuadro) y vertices acotados. Solo la tarjeta. Pintor «CL6» (orden 650: despues de las fiestas).
// REGLA 3: las ondas, el brillo del piso y los reflejos van sumados y escalados por escena (el piso de la pared y del reposo lleva texto: menos); la
// bateria mide el contraste. REGLA 5: todo lo del clima se aquieta con la calma. Sin clima (o sin el grupo) no se dibuja nada de esto.
using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal static class RocolaOsClimaLuces
    {
        // ---------- las luces ----------
        internal const int MaxAstros = 3, MaxFiesta = 40, MaxFuegos = 4;
        internal static readonly float[] AX = new float[MaxAstros], AY = new float[MaxAstros], AR = new float[MaxAstros], AA = new float[MaxAstros];
        internal static readonly float[] ACr = new float[MaxAstros], ACg = new float[MaxAstros], ACb = new float[MaxAstros];
        internal static readonly float[] FX = new float[MaxFiesta], FY = new float[MaxFiesta], FR = new float[MaxFiesta], FA = new float[MaxFiesta];
        internal static readonly float[] FCr = new float[MaxFiesta], FCg = new float[MaxFiesta], FCb = new float[MaxFiesta];
        internal static int FN; internal static double FMs = -1e12;
        internal static readonly float[] GX = new float[MaxFuegos], GY = new float[MaxFuegos], GR = new float[MaxFuegos], GA = new float[MaxFuegos];
        internal static readonly float[] GCr = new float[MaxFuegos], GCg = new float[MaxFuegos], GCb = new float[MaxFuegos];
        internal static readonly double[] GMs = new double[MaxFuegos];
        internal static float Hz, Esc = 1f;                       // el horizonte y la escala (alto / 768) de la escena de este cuadro
        internal static float AurR, AurG, AurB, AurK;             // la aurora de ahora (color medio y cuanta hay, 0 a 1), suavizada
        internal static double Ahora;                              // el reloj de la tarjeta de este cuadro (ms)

        internal static void Limpia() { for (int i = 0; i < MaxAstros; i++) AA[i] = 0f; FN = 0; for (int i = 0; i < MaxFuegos; i++) GA[i] = 0f; AurK = 0f; NZ = 0; }

        // LAS LUCES DE LAS FIESTAS (las pone FI2, la fila del horizonte, cada cuadro que pinta): de lo que dibuja como resplandores (x, y, rx, ry, color
        // con su alfa), las redondas y de punto de luz (no el resplandor ancho de la fecha)
        internal static void PonFiesta(float[] gx, float[] gy, float[] gr, float[] gry, uint[] gc, int gn, double ms)
        {
            int n = 0; int paso = gn > MaxFiesta * 2 ? 2 : 1;
            for (int i = 0; i < gn && n < MaxFiesta; i += paso)
            {
                float a = ((gc[i] >> 24) & 255) / 255f;
                if (a < 0.12f || Math.Abs(gr[i] - gry[i]) > 0.5f || gr[i] > 70f * Esc) continue;
                FX[n] = gx[i]; FY[n] = gy[i]; FR[n] = gr[i]; FA[n] = a;
                FCr[n] = (gc[i] >> 16) & 255; FCg[n] = (gc[i] >> 8) & 255; FCb[n] = gc[i] & 255; n++;
            }
            FN = n; FMs = ms;
        }
        // LOS FUEGOS (los pone FI4, una rafaga por casilla): fuerza 0 a 1 de su destello
        internal static void PonFuego(int i, float cx, float cy, float r, uint rgb, float fuerza, double ms)
        {
            if (i < 0 || i >= MaxFuegos) return;
            GX[i] = cx; GY[i] = cy; GR[i] = r; GA[i] = fuerza; GCr[i] = (rgb >> 16) & 255; GCg[i] = (rgb >> 8) & 255; GCb[i] = rgb & 255; GMs[i] = ms;
        }
        internal static bool FiestaFresca { get { return FN > 0 && Ahora - FMs < 400.0; } }
        internal static bool FuegoFresco(int i) { return GA[i] > 0.01f && Ahora - GMs[i] < 400.0; }

        // cuanto brilla un punto por las luces de alrededor (0 a 1) y de que color (0-255): la mas fuerte de las que lo alcanzan; la aurora cerca del horizonte
        internal static float Brillo(float x, float y, out float r, out float g, out float b)
        {
            float mejor = 0f; r = g = b = 255f;
            for (int i = 0; i < MaxAstros; i++)
            {
                if (AA[i] <= 0.01f) continue;
                float R = AR[i] * 3.4f, dx = x - AX[i], dy = y - AY[i], d2 = dx * dx + dy * dy;
                if (d2 >= R * R) continue;
                float t = 1f - (float)Math.Sqrt(d2) / R, v = t * t * AA[i];
                if (v > mejor) { mejor = v; r = ACr[i]; g = ACg[i]; b = ACb[i]; }
            }
            if (FiestaFresca)
                for (int i = 0; i < FN; i++)
                {
                    float R = Math.Max(FR[i] * 2.2f, 20f * Esc), dx = x - FX[i], dy = y - FY[i], d2 = dx * dx + dy * dy;
                    if (d2 >= R * R) continue;
                    float t = 1f - (float)Math.Sqrt(d2) / R, v = t * t * Math.Min(1f, FA[i] * 1.6f);
                    if (v > mejor) { mejor = v; r = FCr[i]; g = FCg[i]; b = FCb[i]; }
                }
            if (AurK > 0.02f && Hz > 40f && y < Hz)
            {
                float t = 1f - (Hz - y) / (110f * Esc);
                if (t > 0f) { float v = t * t * AurK * 0.8f; if (v > mejor) { mejor = v; r = AurR; g = AurG; b = AurB; } }
            }
            return mejor > 1f ? 1f : mejor;
        }

        // ---------- las zonas con texto (se atenua lo del clima encima: la regla 3) ----------
        internal const int MaxZonas = 10;
        internal static readonly float[] ZX0 = new float[MaxZonas], ZY0 = new float[MaxZonas], ZX1 = new float[MaxZonas], ZY1 = new float[MaxZonas], ZA = new float[MaxZonas], ZB = new float[MaxZonas];
        internal static int NZ;
        private static void Zona(float sx, float sy, float x, float y, float w, float h, float a, float borde)
        {
            if (NZ >= MaxZonas || a <= 0.005f) return;
            ZX0[NZ] = x * sx; ZY0[NZ] = y * sy; ZX1[NZ] = (x + w) * sx; ZY1[NZ] = (y + h) * sy; ZA[NZ] = a; ZB[NZ] = Math.Max(1f, borde * sy); NZ++;
        }
        // las zonas de la escena (en 1024 x 768 y a escala): el escaparate (el cubo y su mensaje), la pared (la barra de arriba, el titulo y las filas de abajo)
        // y el reposo con la letra (la columna de la letra y la franja de abajo); entre la pared y el reposo se mezclan por cuanto reposo hay
        internal static void ZonasDe(RocolaOsCieloEscena e)
        {
            NZ = 0;
            float sx = e.Ancho / 1024f, sy = e.Alto / 768f;
            Esc = sy; Hz = e.Horizonte;
            if (e.Modo == 4)
            {
                Zona(sx, sy, 358, 198, 312, 312, 1f, 3); Zona(sx, sy, 330, 516, 364, 92, 0.85f, 20);
            }
            else if (e.Modo == 1 || e.Pared)
            {
                float m = RocolaOsCieloAstros.Paso(e.Reposo, 0.3, 0.7), p = 1f - m;
                Zona(sx, sy, 0, 0, 1024, 60, p, 6); Zona(sx, sy, 4, 42, 224, 102, p, 6); Zona(sx, sy, 30, 684, 240, 36, 0.9f * p, 10);
                Zona(sx, sy, 570, 676, 454, 36, 0.9f * p, 10); Zona(sx, sy, 30, 728, 980, 30, 0.9f * p, 8);
                Zona(sx, sy, 215, 96, 760, 384, 0.62f * m, 30); Zona(sx, sy, 860, 488, 130, 22, 0.9f * m, 6); Zona(sx, sy, 0, 532, 1024, 236, 0.8f * m, 14);
            }
            else Zona(sx, sy, 0, 0, 1024, 768, 0.5f, 1);   // el buscador, el disco abierto: casi todo esta tapado; lo poco que se ve, a medias
        }
        // cuanto protege el texto a un punto (0 nada, 1 todo): lo que se atenua el clima ahi
        internal static float Proteccion(float x, float y)
        {
            float mejor = 0f;
            for (int i = 0; i < NZ; i++)
            {
                float b = ZB[i], dx = x < ZX0[i] ? ZX0[i] - x : (x > ZX1[i] ? x - ZX1[i] : 0f), dy = y < ZY0[i] ? ZY0[i] - y : (y > ZY1[i] ? y - ZY1[i] : 0f);
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d >= b) continue;
                float v = ZA[i] * (1f - d / b);
                if (v > mejor) mejor = v;
            }
            return mejor;
        }
        // el azar fijo de un entero y una semilla (0 a 1)
        internal static float Azar(int i, int s)
        {
            uint a = (uint)(i * 73 + s * 977 + 4321) * 2654435761u; a ^= a >> 15; a *= 2246822519u; a ^= a >> 13; a *= 3266489917u; a ^= a >> 16;
            return (a & 0xFFFFFF) / 16777216f;
        }
    }

    // CL6: EL PISO MOJADO, LAS SALPICADURAS, EL FILO DE NIEVE, LOS HALOS Y LOS FUEGOS EN LAS NUBES
    internal sealed class RocolaOsCieloClimaLuz : RocolaOsCieloPintor
    {
        internal RocolaOsCieloClimaLuz() { Quien = "CL6 el clima y las luces"; Orden = 650; }
        private const int MaxOndas = 28, MaxLucesPiso = 24, MaxGotas = 44, Columnas = 48;
        private static readonly Tarjeta.Vtx[] bufPiso = new Tarjeta.Vtx[4096];     // sumado: el piso mojado
        private static readonly Tarjeta.Vtx[] bufGotas = new Tarjeta.Vtx[MaxGotas * 30 + 24];   // sumado: salpicaduras
        private static readonly Tarjeta.Vtx[] bufFilo = new Tarjeta.Vtx[Columnas * 6 + 24];     // encima: el filo de nieve
        private static readonly Tarjeta.Vtx[] bufFiloLuz = new Tarjeta.Vtx[24];               // sumado: su resplandor
        private static Tarjeta.Vtx[] bufReflejo;
        private static int nPiso, nGotas, nFilo, nFiloLuz;
        private float modo = 1f;            // cuanto piso mojado lleva la escena (el escaparate 1; la pared y el reposo, menos: llevan texto)
        private bool hay;
        private double ultimoMs = -1, tm;  // el reloj propio (s), frenado por la calma

        internal static Tarjeta.Vtx[] MallaPiso { get { return bufPiso; } }
        internal static Tarjeta.Vtx[] MallaGotas { get { return bufGotas; } }
        internal static Tarjeta.Vtx[] MallaFilo { get { return bufFilo; } }
        internal static int TrisPiso { get { return nPiso / 3; } }
        internal static int TrisGotas { get { return nGotas / 3; } }
        internal static int TrisFilo { get { return nFilo / 3; } }

        internal override void Prepara(GpuPanel p)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            hay = f != null && (f.ClimaVa || f.DN6) && e.Ancho >= 64 && e.Alto >= 64;
            RocolaOsClimaLuces.Ahora = e.Ms;
            if (!hay) { RocolaOsClimaLuces.Limpia(); return; }
            RocolaOsCieloClimaVista.Actualiza();
            RocolaOsClimaLuces.ZonasDe(e);
            Astros(p, f, e);
            // la aurora de ahora (el color medio y cuanta hay), suavizada en 1 s
            float r, g, b; float k = p.RocolaOsAuroraMedia(out r, out g, out b);
            float kf = (float)(1.0 - Math.Exp(-e.Dt / 1.0));
            RocolaOsClimaLuces.AurK += (k - RocolaOsClimaLuces.AurK) * kf;
            if (k > 0.02f) { RocolaOsClimaLuces.AurR += (r - RocolaOsClimaLuces.AurR) * kf; RocolaOsClimaLuces.AurG += (g - RocolaOsClimaLuces.AurG) * kf; RocolaOsClimaLuces.AurB += (b - RocolaOsClimaLuces.AurB) * kf; }
            modo = e.Modo == 4 ? 1f : (e.Modo == 1 || e.Pared ? 0.5f - 0.24f * RocolaOsCieloAstros.Paso(e.Reposo, 0.3, 0.7) : 0.4f);
            double dt = ultimoMs < 0 ? 0 : Math.Max(0, Math.Min(100, e.Ms - ultimoMs)); ultimoMs = e.Ms; tm += dt / 1000.0 * (0.55 + 0.45 * e.Calma);
        }
        // la luna, el sol y la estrella de XP: sitio, radio, fuerza y color
        private static void Astros(GpuPanel p, RocolaOsCieloFoto f, RocolaOsCieloEscena e)
        {
            RocolaOsClimaLuces.Esc = e.Alto / 768f; RocolaOsClimaLuces.Hz = e.Horizonte;
            float cx, cy, r; Form1.SitioLuna(e.Ancho, e.Alto, out cx, out cy, out r);
            float noche = RocolaOsCielo.NocheCuadro;
            float mA = Math.Max(RocolaOsCieloClimaVista.Luna, noche * 0.7f) * (0.4f + 0.6f * (float)Math.Max(0.0, Math.Min(1.0, f.LunaLuz)));
            Pon(0, cx, cy, r, mA, 222f, 228f, 255f);
            float sx, sy; RocolaOsCieloAstros.Proyecta(RocolaOsCieloAstros.Este(f.SolAlt, f.SolAz), f.SolAlt, e.Ancho, e.Horizonte, out sx, out sy);
            float sA = RocolaOsCieloClimaVista.Sol * (1f - RocolaOsCieloAstros.Paso(sy - e.Horizonte, 4.0, 30.0));
            Pon(1, sx, sy, Math.Max(10f, e.Alto * RocolaOsCieloSol.RadioDelAlto), sA, 255f, 236f, 190f);
            float qx, qy, qr;
            if (p.RocolaOsEstrellaCentro(out qx, out qy, out qr)) Pon(2, qx, qy, qr, 0.7f, 190f, 200f, 255f);
            else RocolaOsClimaLuces.AA[2] = 0f;
        }
        private static void Pon(int i, float x, float y, float r, float a, float cr, float cg, float cb)
        {
            RocolaOsClimaLuces.AX[i] = x; RocolaOsClimaLuces.AY[i] = y; RocolaOsClimaLuces.AR[i] = r; RocolaOsClimaLuces.AA[i] = Math.Max(0f, Math.Min(1f, a));
            RocolaOsClimaLuces.ACr[i] = cr; RocolaOsClimaLuces.ACg[i] = cg; RocolaOsClimaLuces.ACb[i] = cb;
        }

        internal override void Pinta(GpuPanel p, int capa)
        {
            if (!hay) return;
            RocolaOsCieloFoto f = RocolaOsCielo.Foto; if (f == null || !f.ClimaVa) { if (capa == RocolaOsCieloCapa.TrasFondo) FuegosEnLasNubes(p); return; }
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            GpuStarfield st = p.RocolaOsEstrellas; if (st == null || e.Horizonte < 40f || e.Horizonte > e.Alto - 6f) return;
            if (capa == RocolaOsCieloCapa.TrasAurora)
            {
                if (f.ClimaSabido && f.CL2) { PisoMojado(p, st, e); FiloDeNieve(st, e); }   // (con la clave lluvia=0 nada de esto)
            }
            else if (capa == RocolaOsCieloCapa.TrasFondo)
            {
                if (f.ClimaSabido && f.CL2) Salpicaduras(st, e);
                if (f.ClimaSabido && (f.CL2 || f.CL4)) Halos(st, e);
                FuegosEnLasNubes(p);
            }
        }

        // ================================================================ EL PISO MOJADO
        internal static float Humedad(float calma)
        {
            float w = Math.Min(1f, RocolaOsCieloClimaVista.Lluvia * 1.25f);
            return w * (0.5f + 0.5f * calma);
        }
        private static uint Col(float r, float g, float b, float a)
        {
            int al = (int)(a * 255f + 0.5f); if (al < 0) al = 0; else if (al > 255) al = 255;
            int rr = (int)(r + 0.5f), gg = (int)(g + 0.5f), bb = (int)(b + 0.5f); if (rr > 255) rr = 255; if (gg > 255) gg = 255; if (bb > 255) bb = 255; if (rr < 0) rr = 0; if (gg < 0) gg = 0; if (bb < 0) bb = 0;
            return ((uint)al << 24) | ((uint)rr << 16) | ((uint)gg << 8) | (uint)bb;
        }
        private static void V(Tarjeta.Vtx[] b, ref int n, float x, float y, uint c) { b[n].X = x; b[n].Y = y; b[n].Z = 0f; b[n].Rhw = 1f; b[n].Color = c; b[n].U = 0f; b[n].V = 0f; n++; }
        private static void Quad(Tarjeta.Vtx[] b, ref int n, float x0, float y0, uint c0, float x1, float y1, uint c1, float x2, float y2, uint c2, float x3, float y3, uint c3)
        {
            if (n + 6 > b.Length) return;
            V(b, ref n, x0, y0, c0); V(b, ref n, x1, y1, c1); V(b, ref n, x2, y2, c2);
            V(b, ref n, x1, y1, c1); V(b, ref n, x3, y3, c3); V(b, ref n, x2, y2, c2);
        }
        // el color del piso: el de la aurora de ahora (de dia, el de la raya del horizonte, que ya es de la hora)
        private static void ColorPiso(out float r, out float g, out float b)
        {
            float noche = RocolaOsCielo.NocheCuadro, ka = RocolaOsClimaLuces.AurK;
            float ar = RocolaOsClimaLuces.AurR, ag = RocolaOsClimaLuces.AurG, ab = RocolaOsClimaLuces.AurB;
            if (ka < 0.05f) { ar = 150f; ag = 130f; ab = 230f; }
            float dr = RocolaOsCielo.RayaR, dg = RocolaOsCielo.RayaG, db = RocolaOsCielo.RayaB;
            if (dr + dg + db < 30f) { dr = 200f; dg = 190f; db = 235f; }
            r = ar * noche + dr * (1f - noche); g = ag * noche + dg * (1f - noche); b = ab * noche + db * (1f - noche);
            float mx = Math.Max(r, Math.Max(g, b)); if (mx > 1f) { float k = 255f / mx; r *= k; g *= k; b *= k; }
        }
        private void PisoMojado(GpuPanel p, GpuStarfield st, RocolaOsCieloEscena e)
        {
            nPiso = 0;
            float w = Humedad(e.Calma) * modo * (0.3f + 0.7f * RocolaOsCielo.NocheCuadro);   // (de dia el piso ya es claro por la raya: menos brillo, regla 3)
            float flash = RocolaOsCieloNubesRelampago.Destello * modo;
            if (w < 0.02f && flash < 0.02f) return;
            float hz = e.Horizonte, sc = e.Alto / 768f;
            float floorH = Math.Min(e.Alto - hz - 2f, (e.Modo == 4 ? 104f : (e.Pared && e.Reposo < 0.5f ? 70f : 78f)) * sc);
            if (floorH < 8f) return;
            float pr, pg, pb; ColorPiso(out pr, out pg, out pb);
            int n = 0; float t = (float)tm;
            // el brillo del piso: un degradado de la raya hacia abajo en el color de la aurora
            float ga = 0.34f * w + 0.12f * flash;
            uint top = Col(pr, pg, pb, ga), nada = Col(pr, pg, pb, 0f);
            if (ga > 0.004f) Quad(bufPiso, ref n, 0, hz + 1f, top, e.Ancho, hz + 1f, top, 0, hz + floorH, nada, e.Ancho, hz + floorH, nada);
            // el destello del relampago en el piso: un instante de luz fria
            if (flash > 0.02f)
            {
                uint fa = Col(190f, 195f, 255f, 0.10f * flash), fn = Col(190f, 195f, 255f, 0f);
                Quad(bufPiso, ref n, 0, hz + 1f, fa, e.Ancho, hz + 1f, fa, 0, hz + floorH * 0.8f, fn, e.Ancho, hz + floorH * 0.8f, fn);
            }
            // la raya mojada: un filo claro justo bajo el horizonte (el piso brilla donde empieza)
            if (w > 0.02f) { uint r0 = Col(pr * 0.4f + 150f, pg * 0.4f + 150f, pb * 0.4f + 170f, 0.34f * w), r1 = Col(pr, pg, pb, 0f); Quad(bufPiso, ref n, 0, hz + 1f, r0, e.Ancho, hz + 1f, r0, 0, hz + 9f * sc, r1, e.Ancho, hz + 9f * sc, r1); }
            nPiso = n;
            // el reflejo de la aurora de XP otra vez, mas claro y con ondas (la malla que ya hizo DibujaReflejoAurora)
            if (w > 0.02f)
            {
                if (bufReflejo == null) bufReflejo = new Tarjeta.Vtx[6 * 160];
                int nr = p.RocolaOsReflejoCopia(bufReflejo, 1.1f * w, t);
                if (nr >= 3) { st.DrawMeshAdd(bufReflejo, nr / 3); }
            }
            n = nPiso;
            // los reflejos de las luces del horizonte (rayitas que tiemblan, cada vez mas flojas hacia abajo)
            if (w > 0.02f && RocolaOsClimaLuces.FiestaFresca)
            {
                int cuantas = Math.Min(MaxLucesPiso, RocolaOsClimaLuces.FN);
                for (int i = 0; i < cuantas; i++)
                {
                    float x = RocolaOsClimaLuces.FX[i], a = Math.Min(1f, RocolaOsClimaLuces.FA[i] * 1.4f) * w, ww = Math.Max(3f * sc, RocolaOsClimaLuces.FR[i] * 0.32f);
                    float cr = RocolaOsClimaLuces.FCr[i], cg = RocolaOsClimaLuces.FCg[i], cb = RocolaOsClimaLuces.FCb[i];
                    uint c0 = Col(cr, cg, cb, 0.40f * a), c1 = Col(cr, cg, cb, 0f);
                    Quad(bufPiso, ref n, x - ww * 0.5f, hz + 1f, c0, x + ww * 0.5f, hz + 1f, c0, x - ww * 0.3f, hz + floorH * 0.62f, c1, x + ww * 0.3f, hz + floorH * 0.62f, c1);
                    for (int q = 0; q < 4; q++)
                    {
                        float y = hz + (5f + q * 9f + q * q * 1.2f) * sc, dx = (float)Math.Sin(t * 2.1 + i * 1.3 + q * 1.7) * 2.4f * sc, hw = ww * (1.5f - q * 0.25f), hh = 0.9f * sc;
                        uint cc = Col(cr, cg, cb, a * (0.55f - 0.12f * q));
                        Quad(bufPiso, ref n, x + dx - hw, y - hh, cc, x + dx + hw, y - hh, cc, x + dx - hw, y + hh, cc, x + dx + hw, y + hh, cc);
                    }
                }
            }
            // las ondas que se abren (anillos aplastados): mas y mas fuertes cuanto mas llueve
            if (w > 0.05f)
            {
                int cuantas = (int)(MaxOndas * Math.Min(1f, w * 1.1f));
                for (int i = 0; i < cuantas; i++)
                {
                    float dur = 1.5f + 1.2f * RocolaOsClimaLuces.Azar(i, 1), u0 = t / dur + RocolaOsClimaLuces.Azar(i, 2);
                    int ciclo = (int)Math.Floor(u0); float u = u0 - ciclo;
                    float cx = RocolaOsClimaLuces.Azar(i * 31 + ciclo, 3) * e.Ancho, prof = 0.12f + 0.8f * RocolaOsClimaLuces.Azar(i * 17 + ciclo, 4);
                    float cy = hz + floorH * prof, rx = (4f + 30f * u) * sc * (0.55f + 0.9f * prof), ry = rx * 0.2f;
                    float a = 0.95f * (1f - u) * (1f - u) * w * (0.45f + 0.55f * prof);
                    if (a < 0.01f) continue;
                    uint c = Col(pr * 0.4f + 150f, pg * 0.4f + 150f, pb * 0.4f + 170f, a), g = c;
                    const int seg = 12; float th = 0.8f * sc;
                    for (int s = 0; s < seg; s++)
                    {
                        float a0 = s * 6.2832f / seg, a1 = (s + 1) * 6.2832f / seg, c0 = (float)Math.Cos(a0), s0 = (float)Math.Sin(a0), c1 = (float)Math.Cos(a1), s1 = (float)Math.Sin(a1);
                        Quad(bufPiso, ref n, cx + (rx + th) * c0, cy + (ry + th * 0.4f) * s0, g, cx + (rx + th) * c1, cy + (ry + th * 0.4f) * s1, g,
                             cx + (rx - th) * c0, cy + (ry - th * 0.4f) * s0, g, cx + (rx - th) * c1, cy + (ry - th * 0.4f) * s1, g);
                    }
                }
            }
            nPiso = n;
            if (nPiso >= 3) st.DrawMeshAdd(bufPiso, nPiso / 3);
        }

        // ================================================================ LAS SALPICADURAS (sobre la raya del horizonte)
        private void Salpicaduras(GpuStarfield st, RocolaOsCieloEscena e)
        {
            nGotas = 0;
            float ll = RocolaOsCieloClimaVista.Lluvia;
            if (ll < 0.05f || RocolaOsCieloClimaVista.Nieve > 0.1f) return;
            float hz = e.Horizonte, sc = e.Alto / 768f, t = (float)tm, calma = 0.4f + 0.6f * e.Calma;
            int cuantas = (int)(MaxGotas * Math.Min(1f, ll * 1.15f) * calma);
            if (cuantas <= 0) return;
            float r, g, b; ColorPiso(out r, out g, out b);
            r = r * 0.45f + 130f; g = g * 0.45f + 135f; b = b * 0.45f + 150f;
            int n = 0;
            for (int i = 0; i < cuantas; i++)
            {
                float dur = 0.45f + 0.5f * RocolaOsClimaLuces.Azar(i, 5), u0 = t / dur + RocolaOsClimaLuces.Azar(i, 6);
                int ciclo = (int)Math.Floor(u0); float u = u0 - ciclo;
                float x = RocolaOsClimaLuces.Azar(i * 29 + ciclo, 7) * e.Ancho, pr = RocolaOsClimaLuces.Proteccion(x, hz - 4f * sc);
                float a = 0.95f * (1f - u) * ll * (1f - 0.85f * pr);
                if (a < 0.02f) continue;
                float y = hz + (RocolaOsClimaLuces.Azar(i * 13 + ciclo, 8) * 4f - 1f) * sc, rx = (2.4f + 8f * u) * sc, ry = rx * 0.3f;
                uint c = Col(r, g, b, a), c0 = Col(r, g, b, 0f);
                // la corona aplastada (un rombo suave)
                V(bufGotas, ref n, x - rx, y, c0); V(bufGotas, ref n, x, y - ry, c); V(bufGotas, ref n, x + rx, y, c0);
                V(bufGotas, ref n, x - rx, y, c0); V(bufGotas, ref n, x, y + ry, c); V(bufGotas, ref n, x + rx, y, c0);
                // dos gotas que saltan y caen
                for (int q = 0; q < 2; q++)
                {
                    float dir = q == 0 ? -1f : 1f, ux = u * (0.6f + 0.4f * q), gx = x + dir * (2f + 7f * ux) * sc, gy = y - (9f * ux - 12f * ux * ux) * sc * 2.2f, s = 1.1f * sc;
                    if (gy > y) continue;
                    uint cg = Col(r, g, b, a * 0.9f);
                    if (n + 6 > bufGotas.Length) break;
                    V(bufGotas, ref n, gx - s, gy - s, cg); V(bufGotas, ref n, gx + s, gy - s, cg); V(bufGotas, ref n, gx - s, gy + s, cg);
                    V(bufGotas, ref n, gx + s, gy - s, cg); V(bufGotas, ref n, gx + s, gy + s, cg); V(bufGotas, ref n, gx - s, gy + s, cg);
                }
            }
            nGotas = n;
            if (n >= 3) st.DrawMeshAdd(bufGotas, n / 3);
        }

        // ================================================================ EL FILO DE NIEVE (sobre la raya)
        private void FiloDeNieve(GpuStarfield st, RocolaOsCieloEscena e)
        {
            nFilo = 0; nFiloLuz = 0;
            float k = RocolaOsCieloClimaVista.Nieve;
            if (k < 0.03f) return;
            float hz = e.Horizonte, sc = e.Alto / 768f, dx = (float)e.Ancho / Columnas;
            int n = 0;
            for (int c = 0; c < Columnas; c++)
            {
                float xa = c * dx, xb = xa + dx;
                float ha = (3.2f + 4.6f * RocolaOsClimaLuces.Azar(c, 11)) * sc, hb = (3.2f + 4.6f * RocolaOsClimaLuces.Azar(c + 1, 11)) * sc;
                float aa = 0.94f * k, y1 = hz + 1.6f * sc;
                uint blanco = Col(240f, 246f, 255f, aa), borde = Col(240f, 246f, 255f, aa * 0.25f);
                // el cuerpo del filo (de la cima con grumos a la raya) y un borde de arriba suave
                Quad(bufFilo, ref n, xa, hz - ha, borde, xb, hz - hb, borde, xa, y1, blanco, xb, y1, blanco);
            }
            nFilo = n;
            if (n >= 3) st.DrawMesh(bufFilo, n / 3);
            // su resplandor: una franja tenue y fria encima, sumada
            int m = 0; uint r0 = Col(215f, 228f, 255f, 0.34f * k), r1 = Col(215f, 228f, 255f, 0f);
            Quad(bufFiloLuz, ref m, 0, hz - 4f * sc, r0, e.Ancho, hz - 4f * sc, r0, 0, hz - 20f * sc, r1, e.Ancho, hz - 20f * sc, r1);
            nFiloLuz = m; st.DrawMeshAdd(bufFiloLuz, m / 3);
        }

        // ================================================================ LOS HALOS (con neblina, lluvia o nieve)
        private void Halos(GpuStarfield st, RocolaOsCieloEscena e)
        {
            float hal = Math.Max(RocolaOsCieloClimaVista.Niebla, Math.Max(0.5f * RocolaOsCieloClimaVista.Lluvia, 0.35f * RocolaOsCieloClimaVista.Nieve));
            if (hal < 0.06f) return;
            float sc = e.Alto / 768f, k = 0.6f + 0.4f * e.Calma;
            int total = 0;
            for (int i = 0; i < RocolaOsClimaLuces.MaxAstros; i++) if (RocolaOsClimaLuces.AA[i] > 0.05f) total++;
            if (RocolaOsClimaLuces.FiestaFresca) total += RocolaOsClimaLuces.FN;
            if (total == 0) return;
            st.GlowBatchBegin(total);
            for (int i = 0; i < RocolaOsClimaLuces.MaxAstros; i++)
            {
                float a = RocolaOsClimaLuces.AA[i]; if (a <= 0.05f) continue;
                float pr = 1f - 0.8f * RocolaOsClimaLuces.Proteccion(RocolaOsClimaLuces.AX[i], RocolaOsClimaLuces.AY[i]);
                float al = Math.Min(0.34f, 0.30f * a * hal * k * pr);
                st.GlowBatchAdd(RocolaOsClimaLuces.AX[i], RocolaOsClimaLuces.AY[i], RocolaOsClimaLuces.AR[i] * 3.0f, Col(RocolaOsClimaLuces.ACr[i], RocolaOsClimaLuces.ACg[i], RocolaOsClimaLuces.ACb[i], al));
            }
            if (RocolaOsClimaLuces.FiestaFresca)
                for (int i = 0; i < RocolaOsClimaLuces.FN; i++)
                {
                    float r = Math.Max(RocolaOsClimaLuces.FR[i] * 2.0f, 24f * sc);
                    float al = Math.Min(0.42f, 0.55f * RocolaOsClimaLuces.FA[i] * hal * k);
                    st.GlowBatchAdd(RocolaOsClimaLuces.FX[i], RocolaOsClimaLuces.FY[i], r, Col(RocolaOsClimaLuces.FCr[i], RocolaOsClimaLuces.FCg[i], RocolaOsClimaLuces.FCb[i], al));
                }
            st.GlowBatchEnd();
        }

        // ================================================================ LOS FUEGOS ALUMBRAN LAS NUBES
        private static void FuegosEnLasNubes(GpuPanel p)
        {
            if (!RocolaOsCieloNubes.Activa || RocolaOsCieloNubes.Visibles <= 0) return;
            GpuStarfield st = p.RocolaOsEstrellas; if (st == null) return;
            for (int i = 0; i < RocolaOsClimaLuces.MaxFuegos; i++)
            {
                if (!RocolaOsClimaLuces.FuegoFresco(i)) continue;
                float fuerza = RocolaOsClimaLuces.GA[i], gx = RocolaOsClimaLuces.GX[i], gy = RocolaOsClimaLuces.GY[i], alcance = RocolaOsClimaLuces.GR[i] * 2.6f;
                uint rgb = ((uint)RocolaOsClimaLuces.GCr[i] << 16) | ((uint)RocolaOsClimaLuces.GCg[i] << 8) | (uint)RocolaOsClimaLuces.GCb[i];
                for (int k = 0; k < RocolaOsCieloNubes.Max; k++)
                {
                    float al = RocolaOsCieloNubes.Al[k]; if (al <= 0.05f) continue;
                    float dx = gx - RocolaOsCieloNubes.Cx[k], dy = gy - RocolaOsCieloNubes.Cy[k], d = (float)Math.Sqrt(dx * dx + dy * dy), R = alcance + RocolaOsCieloNubes.Rx[k] * 0.5f;
                    if (d >= R) continue;
                    float f = 1f - d / R, a = 0.34f * fuerza * f * f * Math.Min(1f, al / 0.5f);
                    if (a < 0.01f) continue;
                    // la luz le da por el lado de donde viene: por debajo si el fuego esta debajo de la nube
                    int tris = RocolaOsCieloNubes.ArmaLuz(k, rgb, a, dy > 0f ? 1f : -1f);
                    if (tris > 0) st.DrawMeshAdd(RocolaOsCieloNubes.MallaLuz, tris);
                }
            }
        }

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra()
        {
            RocolaOsCieloClimaLuz l = new RocolaOsCieloClimaLuz();
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasAurora, l);
            RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasFondo, l);
        }
    }

    internal sealed partial class GpuPanel
    {
        // el centro y el radio (px) de la estrella de XP en este cuadro (lo mismo que hace la letra del cielo): false si no se sabe
        internal bool RocolaOsEstrellaCentro(out float x, out float y, out float r)
        {
            x = y = r = 0f;
            if (Width < 64 || Height < 64) return false;
            float emph = LunaProtagonista ? Math.Max(MoonEmphasis(Dim, true), idleBlend) : 0;
            r = DetailedMoonDiameter(Height, emph) * MoonSceneScale(idleBlend, LunaActiva) * .5f;
            PointF sc = LunaActiva ? StarSceneCenter() : DetailedMoonCenter(Width, Height, moonMotionTime, idleBlend, IdleRise, false);
            x = Math.Max(r, Math.Min(Width - r, sc.X)); y = Math.Max(r, Math.Min(StatusTopY - r, sc.Y));
            return true;
        }
        // el color medio de la aurora de ahora (0-255; por el brillo de cada columna del pie) y cuanta hay (0 a 1); 0 si no se dibujo
        internal float RocolaOsAuroraMedia(out float r, out float g, out float b)
        {
            r = g = b = 0f;
            if (!aurDibujada || aurLc == null || aurLi == null) return 0f;
            int n = Math.Min(aurLc.Length, aurLi.Length); float sw = 0f, sr = 0f, sg = 0f, sb = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = aurLi[i]; if (!(w > 0.001f)) continue;
                uint c = aurLc[i]; sr += w * ((c >> 16) & 255); sg += w * ((c >> 8) & 255); sb += w * (c & 255); sw += w;
            }
            if (!(sw > 0.001f)) return 0f;
            r = sr / sw; g = sg / sw; b = sb / sw;
            return Math.Min(1f, sw / n * 1.6f);
        }
        // una copia del reflejo de la aurora (lo que dejo DibujaReflejoAurora) con el alfa por k y una onda en x que se mueve (el piso mojado); devuelve los vertices
        internal int RocolaOsReflejoCopia(Tarjeta.Vtx[] dst, float k, float t)
        {
            if (!aurDibujada || reflejoV == null || reflejoN < 3 || dst == null) return 0;
            int n = Math.Min(reflejoN, dst.Length);
            for (int i = 0; i < n; i++)
            {
                Tarjeta.Vtx v = reflejoV[i];
                int a = (int)(((v.Color >> 24) & 255) * k + 0.5f); if (a > 255) a = 255;
                v.X += (float)Math.Sin(v.Y * 0.55f + t * 3.0f + v.X * 0.012f) * 1.5f;
                v.Color = ((uint)a << 24) | (v.Color & 0x00FFFFFFu);
                dst[i] = v;
            }
            return n - n % 3;
        }
    }
}

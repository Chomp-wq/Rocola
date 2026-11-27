// EL CIELO VIVO DE ROCOLA OS, GRUPO «NUBES Y CLIMA»: CL2 (LA LLUVIA), CL3 (LOS RELAMPAGOS) Y CL4 (LA NEBLINA). PLAN_CIELO_VIVO.md §1 y §3;
// FASE11_cielo-clima.md. Solo la tarjeta. Claves lluvia=, tormenta= y neblina= (funcion clima); las tres necesitan el clima sabido (Open-Meteo, 3 h).
//
// CL2 (lluvia=): rayas en dos profundidades (las de atras, chicas, lentas y tenues; las de delante, largas, rapidas y mas claras) que caen
// DETRAS de todo (capa TrasFondo: detras de las portadas, el cubo y los textos, delante de la aurora y las estrellas) inclinadas con el
// VIENTO REAL (la direccion de Open-Meteo: la rocola mira al sur, el este queda a la izquierda; la inclinacion crece con los km/h hasta 0,5). Cuantas
// rayas, por lo que llueve (llovizna 30 %, lluvia 45-80 %, aguacero y tormenta casi todas; 420 como mucho); con nieve, copos blancos que
// bajan despacio y se mecen. Se detienen en el horizonte (no se pintan en el suelo). El color de las rayas no pasa de luminancia 0,17 (la regla
// 3): pizarra azulada, mas apagada de noche (por la luz del cielo).
// CL3 (tormenta=): relampagos DENTRO de las nubes (la mancha de luz, sumada, en el centro de una nube a la vista) con la tormenta: un solo pulso
// suave (sube en 0,07 s y baja en 0,2 s: nunca destellos rapidos ni cambios de la pantalla entera), como mucho UNO CADA 6 SEGUNDOS (de un
// comienzo al siguiente: 6 a 16 s), sin sonido, y su luz suma como mucho +45 al color de la nube (que es oscura: la regla 3). Mientras alguien
// elige no empiezan (regla 5) y los que ya van bajan con la calma. Sin nubes a la vista (nubes=0), no hay relampagos.
// CL4 (neblina=): una franja de niebla sobre el horizonte, de 0,2 del horizonte hacia arriba, que se aclara hacia la raya (alfa 0 a 0,55 en 0,848 del
// alto y 0,7 en el horizonte), con el color de la luz (el de las nubes arriba, mas claro en la franja fina de la raya, 0,853-0,868) y un
// vaiven lento de 24 columnas; detras de la aurora (capa TrasNieblas). Con la neblina de Open-Meteo, entera segun la visibilidad; un velo
// tenue con llovizna, lluvia y nieve.
// REGLA 5: la lluvia pierde rayas (x 0,35 + 0,65 calma) y velocidad (0,55 + 0,45 calma); la niebla no se mueve rapido de por si.
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal sealed class RocolaOsCieloLluvia : RocolaOsCieloPintor
    {
        internal RocolaOsCieloLluvia() { Quien = "CL2 la lluvia"; Orden = RocolaOsCieloOrden.Clima; }
        internal const int Max = 420;
        private static readonly float[] rx = new float[Max], rp = new float[Max], rl = new float[Max];
        private static readonly bool[] cerca = new bool[Max];
        private static Tarjeta.Vtx[] buf = new Tarjeta.Vtx[Max * 6 + 12];
        private static int nv;
        private static double fase;       // el reloj de las rayas (s), frenado por la calma
        private static double msAntes = -1;
        static RocolaOsCieloLluvia()
        {
            for (int i = 0; i < Max; i++)
            {
                uint a = (uint)(i + 1) * 2654435761u; a ^= a >> 15; a *= 2246822519u; a ^= a >> 13;
                uint b = (uint)(i + 7) * 3266489917u; b ^= b >> 16; b *= 668265263u; b ^= b >> 15;
                uint c = (uint)(i + 31) * 374761393u; c ^= c >> 13; c *= 1274126177u; c ^= c >> 16;
                rx[i] = (a & 0xFFFFFF) / 16777216f; rp[i] = (b & 0xFFFFFF) / 16777216f; rl[i] = 0.75f + 0.5f * ((c & 0xFFFF) / 65536f);
                cerca[i] = i % 3 == 0;                                           // un tercio delante, dos de atras
            }
        }
        // lo mas que sube la inclinacion con el viento (px de x por px de y): el viento hacia la izquierda (-1) da x negativo
        internal static float Inclina(float dirX, float vientoKmh) { return dirX * Math.Min(0.5f, Math.Max(0f, vientoKmh) / 50f); }
        // cuantas rayas (de Max) con tanta lluvia (0-1) y la calma
        internal static int Cuantas(float lluvia, float calma) { return (int)Math.Max(0f, Math.Min(Max, Max * lluvia * (0.35f + 0.65f * calma))); }
        // el color de una raya (0-255): pizarra, escalada por la luz del cielo; su luminancia no pasa de 0,17 (la regla 3)
        internal static void Color(bool cercana, float nivel, out float r, out float g, out float b)
        {
            float k = 0.30f + 0.70f * Math.Max(0f, Math.Min(1f, nivel));
            r = (cercana ? 100f : 92f) * k; g = (cercana ? 116f : 108f) * k; b = (cercana ? 142f : 134f) * k;
        }
        private static uint Rgba(float r, float g, float b, float a)
        {
            int al = (int)(a * 255f + 0.5f); if (al < 0) al = 0; else if (al > 255) al = 255;
            return ((uint)al << 24) | ((uint)(int)(r + 0.5f) << 16) | ((uint)(int)(g + 0.5f) << 8) | (uint)(int)(b + 0.5f);
        }
        private static void P(float x, float y, uint c) { buf[nv].X = x; buf[nv].Y = y; buf[nv].Z = 0f; buf[nv].Rhw = 1f; buf[nv].Color = c; buf[nv].U = 0f; buf[nv].V = 0f; nv++; }
        // UN CUADRO de lluvia o nieve: arma la malla (sin dibujar); devuelve los triangulos. 'llueve' 0-1, 'nieva' 0-1. Pura salvo por la fase.
        internal static int Arma(int ancho, float hz, float alto, float lluvia, float nieva, float dirX, float viento, float calma, float nivel, double dt)
        {
            nv = 0;
            fase += dt * (0.55 + 0.45 * calma);
            bool nieve = nieva > 0.02f;
            int n = Cuantas(nieve ? nieva : lluvia, calma);
            if (n <= 0 || hz < 20f) return 0;
            float esc = alto / 768f, incl = Inclina(dirX, viento);
            for (int i = 0; i < n; i++)
            {
                bool c = cerca[i];
                float vy = (nieve ? (c ? 95f : 55f) : (c ? 560f : 300f)) * esc, len = nieve ? (c ? 3.5f : 2.5f) * esc : (c ? 22f : 11f) * rl[i] * esc;
                float span = hz + len + 8f;
                double t = rp[i] + fase * vy / span; t -= Math.Floor(t);
                float yh = (float)(t * span) - 4f;                                   // la cabeza (abajo) de la raya
                float r, g, b; Color(c, nivel, out r, out g, out b);
                if (nieve)
                {
                    float sway = (float)Math.Sin(fase * 0.8 + rp[i] * 6.28) * 9f * esc;
                    float x = rx[i] * ancho + incl * yh * 0.5f + sway; x -= (float)Math.Floor(x / ancho) * ancho;
                    float y0 = yh - len, y1 = yh;
                    if (y0 >= hz || y1 < 0f) continue;
                    if (y1 > hz) y1 = hz;
                    float a = (c ? 0.55f : 0.40f) * Math.Min(1f, nieva * 1.2f), h = len * 0.5f;
                    uint col = Rgba(r, g, b, a);
                    P(x - h, y0, col); P(x + h, y0, col); P(x - h, y1, col);
                    P(x + h, y0, col); P(x + h, y1, col); P(x - h, y1, col);
                    continue;
                }
                float x0 = rx[i] * ancho + incl * yh;                                 // (la x de la cabeza: se inclina con la altura)
                x0 -= (float)Math.Floor(x0 / ancho) * ancho;
                float yt = yh - len; if (yt >= hz || yh < 0f) continue;
                float xt = x0 - incl * len;
                float yc = yh, xc = x0, ac = (c ? 0.36f : 0.22f);
                if (yc > hz) { float u = (hz - yt) / (yh - yt); xc = xt + (x0 - xt) * u; yc = hz; ac *= u; }
                float dx = xc - xt, dy = yc - yt, ln = (float)Math.Sqrt(dx * dx + dy * dy); if (ln < 0.5f) continue;
                float w = (c ? 1.7f : 1.1f) * esc * 0.5f, px = -dy / ln * w, py = dx / ln * w;
                uint cabeza = Rgba(r, g, b, ac), cola = Rgba(r, g, b, 0f);
                P(xt - px, yt - py, cola); P(xt + px, yt + py, cola); P(xc - px, yc - py, cabeza);
                P(xt + px, yt + py, cola); P(xc + px, yc + py, cabeza); P(xc - px, yc - py, cabeza);
            }
            return nv / 3;
        }
        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.CL2 || !f.ClimaSabido) { msAntes = -1; return; }
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (e.Ancho < 64 || e.Alto < 64) return;
            GpuStarfield st = p.RocolaOsEstrellas; if (st == null) return;
            RocolaOsCieloClimaVista.Actualiza();
            float llueve = RocolaOsCieloClimaVista.Lluvia, nieva = RocolaOsCieloClimaVista.Nieve;
            if (llueve < 0.02f && nieva < 0.02f) return;
            int tris = Arma(e.Ancho, e.Horizonte, e.Alto, llueve, nieva, RocolaOsCieloClimaVista.DirX, RocolaOsCieloClimaVista.Viento, e.Calma, RocolaOsCieloClimaVista.Nivel, e.Dt);
            if (tris > 0) st.DrawMesh(buf, tris);
        }
        internal static Tarjeta.Vtx[] Malla { get { return buf; } }
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasFondo, new RocolaOsCieloLluvia()); }
    }

    // CL3: LOS RELAMPAGOS (los pinta la capa de las nubes: dentro de ellas, detras de todo lo demas)
    internal static class RocolaOsCieloNubesRelampago
    {
        private static bool enFlash; private static int nube = -1; private static double inicio, proximo = -1;
        private static uint semilla = 12345u;
        private static string registro; private static bool registroLeido;
        internal const double IntervaloMinimo = 6.0, IntervaloMaximo = 16.0, SubeS = 0.07, BajaS = 0.2, DuraS = 0.9;
        internal const float Suma = 0.26f;   // el alfa de la mancha sumada de (180, 180, 240): +47 como mucho al color de la nube
        private static float Azar() { semilla = semilla * 1664525u + 1013904223u; return ((semilla >> 8) & 0xFFFF) / 65536f; }
        // el tiempo hasta el siguiente comienzo (s): de 6 a 16 (nunca menos de 6: el tope de CL3). Pura.
        internal static double Intervalo(float azar01) { return IntervaloMinimo + (IntervaloMaximo - IntervaloMinimo) * Math.Max(0f, Math.Min(1f, azar01)); }
        // la envolvente de un relampago a los u segundos de empezar (0 a 1): sube en 0,07 s, baja con constante de 0,2 s y acaba a los 0,9 s. Pura.
        internal static float Envolvente(double u)
        {
            if (u <= 0.0 || u >= DuraS) return 0f;
            if (u < SubeS) { double x = u / SubeS; return (float)(x * x * (3 - 2 * x)); }
            return (float)Math.Exp(-(u - SubeS) / BajaS);
        }
        // lo que pasa en un cuadro: (nada a la vista: se reinicia)
        internal static void Pinta(GpuStarfield st, RocolaOsCieloEscena e, RocolaOsCieloFoto f)
        {
            if (!f.CL3 || !f.ClimaSabido || RocolaOsCieloClimaVista.Tormenta < 0.5f || RocolaOsCieloNubes.Visibles <= 0) { enFlash = false; proximo = -1; return; }
            double t = e.Ms / 1000.0;
            if (proximo < 0) proximo = t + 2.0 + 4.0 * Azar();
            if (!enFlash && t >= proximo && e.Calma >= 0.95f)
            {
                int ok = 0; int[] cand = new int[RocolaOsCieloNubes.Max];
                for (int k = 0; k < RocolaOsCieloNubes.Max; k++)
                    if (RocolaOsCieloNubes.Al[k] > 0.25f && RocolaOsCieloNubes.Cx[k] > 0.08f * e.Ancho && RocolaOsCieloNubes.Cx[k] < 0.92f * e.Ancho) cand[ok++] = k;
                if (ok == 0) { proximo = t + 1.0; return; }
                nube = cand[(int)(Azar() * ok) % ok]; enFlash = true; inicio = t; proximo = t + Intervalo(Azar());
                Apunta(t);
            }
            if (!enFlash) return;
            double u = t - inicio;
            float env = Envolvente(u);
            if (u >= DuraS) { enFlash = false; return; }
            float al = Math.Min(1f, RocolaOsCieloNubes.Al[nube] / 0.5f);
            float a0 = Suma * env * e.Calma * al;
            if (a0 <= 0.003f) return;
            float cx = RocolaOsCieloNubes.Cx[nube], cy = RocolaOsCieloNubes.Cy[nube] - RocolaOsCieloNubes.Ry[nube] * 0.2f, r = RocolaOsCieloNubes.Rx[nube] * 0.75f;
            RocolaOsCieloAstros.Halo(st, true, cx, cy, r, 0xB4B4F0, a0, 1.6f, -2e9f, e.Horizonte);
        }
        // (pruebas: ROCOLA_CIELO_RELAMPAGOS=archivo: la hora de cada comienzo, en segundos)
        private static void Apunta(double t)
        {
            if (!registroLeido) { registroLeido = true; registro = Environment.GetEnvironmentVariable("ROCOLA_CIELO_RELAMPAGOS"); }
            if (string.IsNullOrEmpty(registro)) return;
            try { File.AppendAllText(registro, t.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\n"); } catch { }
        }
    }

    // CL4: LA NEBLINA
    internal sealed class RocolaOsCieloNeblina : RocolaOsCieloPintor
    {
        internal RocolaOsCieloNeblina() { Quien = "CL4 la neblina"; Orden = RocolaOsCieloOrden.Clima; }
        internal const int Columnas = 24;
        private static readonly Tarjeta.Vtx[] buf = new Tarjeta.Vtx[Columnas * 12 + 12];
        private static int nv;
        private static void P(float x, float y, uint c) { buf[nv].X = x; buf[nv].Y = y; buf[nv].Z = 0f; buf[nv].Rhw = 1f; buf[nv].Color = c; buf[nv].U = 0f; buf[nv].V = 0f; nv++; }
        // el vaiven de una columna (0,6 a 1): dos ondas lentas
        internal static float Vaiven(int c, double t) { return (float)(0.8 + 0.2 * Math.Sin(c * 1.9 + t * 0.045) * Math.Sin(c * 0.7 - t * 0.031 + 1.3)); }
        // el alfa de la franja a la altura y (px) de un horizonte hz, con la niebla 0-1: 0 a 0,80 hz, 0,55 en el 0,848 del alto (la regla 3), 0,7 en el horizonte
        internal static float Alfa(float y, float hz, float niebla)
        {
            float y0 = hz * 0.80f, y1 = hz * (RocolaOsCieloDia.Regla3Hasta / RocolaOsCieloDia.HorizonteMaqueta), a;
            if (y <= y0) a = 0f; else if (y <= y1) a = 0.55f * (y - y0) / (y1 - y0); else if (y <= hz) a = 0.55f + 0.15f * (y - y1) / (hz - y1); else a = 0f;
            return a * niebla;
        }
        // arma la franja (3 bandas por columna); devuelve los triangulos
        internal static int Arma(int ancho, float hz, float niebla, double t, float calma)
        {
            nv = 0;
            if (niebla <= 0.01f || hz < 40f) return 0;
            float y0 = hz * 0.80f, y1 = hz * (RocolaOsCieloDia.Regla3Hasta / RocolaOsCieloDia.HorizonteMaqueta), y2 = hz;
            float lr = RocolaOsCieloClimaVista.LitR, lg = RocolaOsCieloClimaVista.LitG, lb = RocolaOsCieloClimaVista.LitB;
            // (el cuerpo de la niebla tiene la luz de una nube; en la franja fina de la raya, algo mas clara)
            uint cuerpo = Rgba(lr, lg, lb, 0f);
            float k = 0.5f + 0.5f * calma;
            float dx = (float)ancho / Columnas;
            for (int c = 0; c < Columnas; c++)
            {
                float xa = c * dx, xb = (c + 1) * dx;
                float va = Vaiven(c, t) * k + (1f - k), vb = Vaiven(c + 1, t) * k + (1f - k);
                uint t0 = Rgba(lr, lg, lb, 0f);
                uint a1 = Rgba(lr, lg, lb, Alfa(y1, hz, niebla) * va), b1 = Rgba(lr, lg, lb, Alfa(y1, hz, niebla) * vb);
                float mr = Math.Min(255f, lr * 1.45f), mg = Math.Min(255f, lg * 1.45f), mb = Math.Min(255f, lb * 1.45f);
                uint a2 = Rgba(mr, mg, mb, Alfa(y2, hz, niebla) * va), b2 = Rgba(mr, mg, mb, Alfa(y2, hz, niebla) * vb);
                P(xa, y0, t0); P(xb, y0, t0); P(xa, y1, a1);
                P(xb, y0, t0); P(xb, y1, b1); P(xa, y1, a1);
                P(xa, y1, a1); P(xb, y1, b1); P(xa, y2, a2);
                P(xb, y1, b1); P(xb, y2, b2); P(xa, y2, a2);
            }
            return nv / 3;
        }
        private static uint Rgba(float r, float g, float b, float a)
        {
            int al = (int)(a * 255f + 0.5f); if (al < 0) al = 0; else if (al > 255) al = 255;
            return ((uint)al << 24) | ((uint)(int)(r + 0.5f) << 16) | ((uint)(int)(g + 0.5f) << 8) | (uint)(int)(b + 0.5f);
        }
        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.CL4 || !f.ClimaSabido) return;
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (e.Ancho < 64 || e.Alto < 64) return;
            GpuStarfield st = p.RocolaOsEstrellas; if (st == null) return;
            RocolaOsCieloClimaVista.Actualiza();
            int tris = Arma(e.Ancho, e.Horizonte, RocolaOsCieloClimaVista.Niebla, RocolaOsCieloClimaVista.Deriva, e.Calma);
            if (tris > 0) st.DrawMesh(buf, tris);
        }
        internal static Tarjeta.Vtx[] Malla { get { return buf; } }
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasNieblas, new RocolaOsCieloNeblina()); }
    }
}

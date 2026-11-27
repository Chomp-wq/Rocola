// EL CIELO VIVO DE ROCOLA OS, GRUPO «SOL, LUNA Y ESTRELLAS»: LOS DIBUJOS COMUNES Y DN2 (EL SOL EN SU CAMINO). PLAN_CIELO_VIVO.md §5 y §8;
// FASE11_cielo-sol.md. Solo Rocola OS, solo la tarjeta (el dibujo por procesador esta congelado y no se toca).
//
// DN2 (clave sol=, funcion cielo): un disco de radio 0,028 del alto (nucleo #fff6dc, dos halos #ffc070: r x 2,2 a 0,55 y r x 7 a 0,22) que
// cruza el cielo por su camino real (la altura y el acimut del sol de la ciudad del bar, la foto del cielo), SIEMPRE DETRAS DE TODO: se pinta
// en la capa TrasNieblas (g130: justo despues del degradado y las nebulosas, antes de la aurora, las estrellas, la luna, la estrella de XP, las
// portadas y los textos). El horizonte lo corta: lo de debajo de la raya (el suelo) no se pinta, asi que al salir y ponerse «asoma» sobre ella.
// EN LA PARED (la rejilla de portadas a la vista) solo asoma en la franja de 0,848 a 0,868 del horizonte (la ultima fila de titulos acaba en
// 0,848: regla 3, nunca detras de un titulo), con el halo x 0,22, y se apaga al subir (de 2 a 9 grados). Sin red, sin disco, sin memoria nueva
// por cuadro. El sol no se mueve con la calma (es lo que se ve cambiar un pixel cada varios minutos): lo que si hace es seguir sin saltos
// «Ver como se veria» (se desliza hacia su sitio nuevo en medio segundo).
//
// LOS DIBUJOS COMUNES (RocolaOsCieloAstros): la proyeccion de un astro (altura y acimut) al cielo de la rocola, y los halos y discos de malla
// con el color en los vertices y recorte horizontal (arriba y abajo), que usan el sol y la luna en su camino (DN3 B): la tarjeta no tiene
// recorte (scissor), asi que se recorta la malla a mano (triangulo por triangulo contra las dos rectas).
using System;
using System.Runtime.CompilerServices;

namespace Rocola
{
    internal static class RocolaOsCieloAstros
    {
        // LA PROYECCION: la rocola mira al sur. x sale del componente este-oeste del astro (este a la izquierda, sin saltos por el cenit ni por el
        // norte: en verano el sol del mediodia pasa al norte del cenit en el tropico) y y de su altura (el horizonte de la escena abajo; el cenit
        // a 0,12 del horizonte). Bajo el horizonte sigue la misma curva hacia abajo (para que el astro salga de a poco).
        internal static void Proyecta(double eEste, double altGrados, int ancho, float horizonte, out float x, out float y)
        {
            x = (float)(ancho * (0.5 - 0.46 * Math.Max(-1.0, Math.Min(1.0, eEste))));
            double s = Math.Sin(altGrados * Math.PI / 180.0);
            double c = Math.Pow(Math.Abs(s), 0.85) * (s < 0 ? -1.0 : 1.0);
            y = (float)(horizonte - (horizonte - horizonte * 0.12) * c);
        }
        // el componente este (+1 al este, -1 al oeste) de un astro a esa altura y acimut (grados)
        internal static double Este(double altGrados, double acimutGrados)
        { return Math.Sin(acimutGrados * Math.PI / 180.0) * Math.Cos(altGrados * Math.PI / 180.0); }
        // LA PARED: la rejilla de portadas a la vista y no el reposo con musica (que la esconde): ahi el sol y la luna B solo asoman en la franja de
        // 0,848 a 0,868 del horizonte (16 px de 768), con el radio algo mas chico para que quepa su cupula entera
        internal const float RadioParedK = 0.7f;
        internal static bool ConPortadas(RocolaOsCieloEscena e) { return e.Pared && e.Reposo < 0.2f; }
        // un paso suave de 0 a 1 entre a y b (a < b)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Paso(double v, double a, double b)
        {
            double t = (v - a) / (b - a); if (!(t > 0)) return 0f; if (t >= 1) return 1f;
            return (float)(t * t * (3 - 2 * t));
        }

        // ---------- mallas con recorte ----------
        private static Tarjeta.Vtx[] malla = new Tarjeta.Vtx[6144];
        private static int nv;
        private static readonly float[] px = new float[8], py = new float[8], pa = new float[8], qx = new float[8], qy = new float[8], qa = new float[8];
        private static uint colorRgb;
        // recorta el poligono (px,py,pa; n puntos) contra y >= corte (arriba: true) o y <= corte (false); deja el resultado en q* y devuelve cuantos
        private static int Corta(int n, float corte, bool conservaMayores)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                bool di = conservaMayores ? py[i] >= corte : py[i] <= corte, dj = conservaMayores ? py[j] >= corte : py[j] <= corte;
                if (di) { qx[m] = px[i]; qy[m] = py[i]; qa[m] = pa[i]; m++; }
                if (di != dj)
                {
                    float t = (corte - py[i]) / (py[j] - py[i]);
                    qx[m] = px[i] + (px[j] - px[i]) * t; qy[m] = corte; qa[m] = pa[i] + (pa[j] - pa[i]) * t; m++;
                }
            }
            return m;
        }
        // un triangulo (x, y, alfa 0 a 1 en cada punto) recortado entre yTop y yBot, a la malla
        private static void Tri(float x0, float y0, float a0, float x1, float y1, float a1, float x2, float y2, float a2, float yTop, float yBot)
        {
            px[0] = x0; py[0] = y0; pa[0] = a0; px[1] = x1; py[1] = y1; pa[1] = a1; px[2] = x2; py[2] = y2; pa[2] = a2;
            int n = 3;
            if (!(yTop < -1e8f))
            {
                if (y0 < yTop || y1 < yTop || y2 < yTop) { n = Corta(n, yTop, true); if (n < 3) return; Array.Copy(qx, px, n); Array.Copy(qy, py, n); Array.Copy(qa, pa, n); }
            }
            if (!(yBot > 1e8f))
            {
                if (py[0] > yBot || py[1] > yBot || py[2] > yBot || n > 3)
                {
                    bool fuera = false; for (int i = 0; i < n; i++) if (py[i] > yBot) fuera = true;
                    if (fuera) { n = Corta(n, yBot, false); if (n < 3) return; Array.Copy(qx, px, n); Array.Copy(qy, py, n); Array.Copy(qa, pa, n); }
                }
            }
            if (nv + (n - 2) * 3 > malla.Length) return;
            for (int k = 1; k < n - 1; k++) { Punto(px[0], py[0], pa[0]); Punto(px[k], py[k], pa[k]); Punto(px[k + 1], py[k + 1], pa[k + 1]); }
        }
        private static void Punto(float x, float y, float a)
        {
            int al = (int)(a * 255f + 0.5f); if (al < 0) al = 0; else if (al > 255) al = 255;
            malla[nv].X = x; malla[nv].Y = y; malla[nv].Z = 0f; malla[nv].Rhw = 1f; malla[nv].Color = ((uint)al << 24) | colorRgb; malla[nv].U = 0f; malla[nv].V = 0f;
            nv++;
        }
        private const int Segmentos = 28;
        private static readonly float[] cosT = new float[Segmentos + 1], sinT = new float[Segmentos + 1];
        private static readonly float[] alfaAnillo = new float[9];
        static RocolaOsCieloAstros() { for (int i = 0; i <= Segmentos; i++) { cosT[i] = (float)Math.Cos(i * 2 * Math.PI / Segmentos); sinT[i] = (float)Math.Sin(i * 2 * Math.PI / Segmentos); } }
        // UN HALO: el color rgb con alfa a0 en el centro que baja a 0 en el radio r como (1 - t)^gamma (en 8 anillos: la tarjeta lo degrada entre
        // vertices), recortado entre yTop y yBot (yTop < -1e9 y yBot > 1e9: sin recorte). aditivo: sumando luz (XP: DrawMeshAdd); si no, encima.
        internal static void Halo(GpuStarfield st, bool aditivo, float cx, float cy, float r, uint rgb, float a0, float gamma, float yTop, float yBot)
        {
            if (st == null) return;
            ArmaHalo(cx, cy, r, rgb, a0, gamma, yTop, yBot);
            Manda(st, aditivo);
        }
        // (la malla que arma el halo; la prueba la mira sin dibujar) devuelve cuantos vertices
        internal static int ArmaHalo(float cx, float cy, float r, uint rgb, float a0, float gamma, float yTop, float yBot)
        {
            nv = 0;
            if (!(a0 > 0.002f) || !(r > 1f)) return 0;
            const int anillos = 8;
            colorRgb = rgb & 0x00FFFFFFu;
            for (int j = 0; j <= anillos; j++) alfaAnillo[j] = j == anillos ? 0f : a0 * (float)Math.Pow(1f - (float)j / anillos, gamma);
            for (int s = 0; s < Segmentos; s++)
            {
                float c0 = cosT[s], s0 = sinT[s], c1 = cosT[s + 1], s1 = sinT[s + 1];
                for (int j = 0; j < anillos; j++)
                {
                    float ra = r * j / anillos, rb = r * (j + 1) / anillos;
                    float aa = alfaAnillo[j], ab = alfaAnillo[j + 1];
                    if (j == 0) Tri(cx, cy, aa, cx + rb * c0, cy + rb * s0, ab, cx + rb * c1, cy + rb * s1, ab, yTop, yBot);
                    else
                    {
                        Tri(cx + ra * c0, cy + ra * s0, aa, cx + rb * c0, cy + rb * s0, ab, cx + rb * c1, cy + rb * s1, ab, yTop, yBot);
                        Tri(cx + ra * c0, cy + ra * s0, aa, cx + rb * c1, cy + rb * s1, ab, cx + ra * c1, cy + ra * s1, aa, yTop, yBot);
                    }
                }
            }
            return nv;
        }
        // UN DISCO LLENO de alfa a, con un borde suave de 1,5 px (sin textura), recortado
        internal static void Disco(GpuStarfield st, float cx, float cy, float r, uint rgb, float a, float yTop, float yBot)
        {
            if (st == null) return;
            ArmaDisco(cx, cy, r, rgb, a, yTop, yBot);
            Manda(st, false);
        }
        internal static int ArmaDisco(float cx, float cy, float r, uint rgb, float a, float yTop, float yBot)
        {
            nv = 0;
            if (!(a > 0.002f) || !(r > 1f)) return 0;
            colorRgb = rgb & 0x00FFFFFFu;
            float ri = Math.Max(0.5f, r - 0.75f), ro = r + 0.75f;
            for (int s = 0; s < Segmentos; s++)
            {
                float c0 = cosT[s], s0 = sinT[s], c1 = cosT[s + 1], s1 = sinT[s + 1];
                Tri(cx, cy, a, cx + ri * c0, cy + ri * s0, a, cx + ri * c1, cy + ri * s1, a, yTop, yBot);
                Tri(cx + ri * c0, cy + ri * s0, a, cx + ro * c0, cy + ro * s0, 0f, cx + ro * c1, cy + ro * s1, 0f, yTop, yBot);
                Tri(cx + ri * c0, cy + ri * s0, a, cx + ro * c1, cy + ro * s1, 0f, cx + ri * c1, cy + ri * s1, a, yTop, yBot);
            }
            return nv;
        }
        internal static Tarjeta.Vtx[] Malla { get { return malla; } }
        private static void Manda(GpuStarfield st, bool aditivo)
        {
            if (nv < 3) return;
            if (aditivo) st.DrawMeshAdd(malla, nv / 3); else st.DrawMesh(malla, nv / 3);
        }
    }

    // DN2: EL SOL
    internal sealed class RocolaOsCieloSol : RocolaOsCieloPintor
    {
        internal RocolaOsCieloSol() { Quien = "DN2 el sol"; Orden = RocolaOsCieloOrden.Sol; }
        private double sEste, sAlt; private bool listo;   // lo que se ve (se desliza hacia la foto: «Ver como se veria» no da saltos)
        // el halo grande y el mediano de §5 y el nucleo
        internal const float RadioDelAlto = 0.028f;
        internal const uint Nucleo = 0xFFF6DC, Halo = 0xFFC070;
        internal override void Pinta(GpuPanel p, int capa)
        {
            RocolaOsCieloFoto f = RocolaOsCielo.Foto;
            if (f == null || !f.DN2) { listo = false; return; }
            RocolaOsCieloEscena e = RocolaOsCielo.Escena;
            if (e.Ancho < 64 || e.Alto < 64) return;
            double eT = RocolaOsCieloAstros.Este(f.SolAlt, f.SolAz);
            if (!listo) { sEste = eT; sAlt = f.SolAlt; listo = true; }
            else { double k = 1.0 - Math.Exp(-e.Dt / 0.5); sEste += (eT - sEste) * k; sAlt += (f.SolAlt - sAlt) * k; }
            if (sAlt < -6.5) return;                               // bajo el horizonte y sin resplandor: nada
            GpuStarfield st = p.RocolaOsEstrellas;
            if (st == null) return;
            bool pared = RocolaOsCieloAstros.ConPortadas(e);
            float hz = e.Horizonte, r = Math.Max(10f, e.Alto * RadioDelAlto) * (pared ? RocolaOsCieloAstros.RadioParedK : 1f), x, y;
            float yTop = -2e9f, yBot = hz;                         // el horizonte corta: bajo la raya no hay sol
            float kHalo = RocolaOsCieloAstros.Paso(sAlt, -6.0, -0.5), kDisco = 1f, mulHalo = 1f;
            if (pared)
            {
                // la pared: solo asoma al salir y al ponerse, en la franja de 0,848 a 0,868 del horizonte (sobre la raya, bajo la ultima fila de titulos)
                RocolaOsCieloAstros.Proyecta(sEste, 0.0, e.Ancho, hz, out x, out y);   // (solo la x)
                float s = (float)((Math.Max(-4.0, Math.Min(14.0, sAlt)) + 4.0) / 18.0);
                y = hz + 1.2f * r - s * 2.6f * r;
                kDisco = 1f - RocolaOsCieloAstros.Paso(sAlt, 2.0, 9.0);
                kHalo *= kDisco; mulHalo = 0.22f;                       // los halos x 0,22 (§5)
                yTop = hz * (RocolaOsCieloDia.Regla3Hasta / RocolaOsCieloDia.HorizonteMaqueta);
                if (kDisco <= 0.003f) return;
            }
            else RocolaOsCieloAstros.Proyecta(sEste, sAlt, e.Ancho, hz, out x, out y);
            RocolaOsCieloAstros.Halo(st, true, x, y, r * 7f, Halo, 0.22f * kHalo * mulHalo, 2f, yTop, yBot);
            RocolaOsCieloAstros.Halo(st, true, x, y, r * 2.2f, Halo, 0.55f * kHalo * mulHalo, 2f, yTop, yBot);
            RocolaOsCieloAstros.Disco(st, x, y, r, Nucleo, kDisco, yTop, yBot);
        }
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Registra() { RocolaOsCielo.PonPintor(RocolaOsCieloCapa.TrasNieblas, new RocolaOsCieloSol()); }
    }
}

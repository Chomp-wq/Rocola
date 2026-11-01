// LA ESTRELLA DEL CIELO (en XP, el «astro principal», luna=: la estrella viajera de EstrellaGpu.cs, LunaGpu.cs y Estrella3dGpu.cs). La
// clase Estrella3d es la de XP tal cual (12 vertices, 20 caras, sus luces y su color); el recorrido (StarTravelCenter), el tamano
// (DetailedMoonDiameter, un 32 % mas grande en el reposo), el vaiven (StarRoll), el giro (una vuelta cada ~3,4 s en reposo y cada ~19 s
// navegando), la corona, el polvo en tres orbitas y la estela de copias (una cada radio recorrido, 1,2 s de vida), en lo esencial.
// Interruptores de XP: luna=, estrella_3d= (si no, la estrella plana), halo_lunar=, polvo_lunar=, estela_estelar=.
using System;
using Rocola.Pantalla;

namespace Rocola
{
    internal static class Estrella3d
    {
        internal const int Borde = 10, Vertices = 12, Caras = 20;
        internal const float Valle = .445f;      // radio de los valles, el del icono (49 de 110)
        internal const float Relieve = .34f;     // altura del centro, por delante y por detras
        internal const float Camara = 4.5f;      // distancia de la camara, en radios (perspectiva suave)
        // Vertices del modelo (x a la derecha, y arriba, z hacia quien mira): 0..9 el borde empezando por la punta de
        // arriba y en el sentido del reloj visto de frente; 10 el centro de delante; 11 el de detras.
        internal static readonly float[] MX = new float[Vertices], MY = new float[Vertices], MZ = new float[Vertices];
        // Caras: A, B, C en sentido contrario al reloj vistas desde fuera (la normal sale hacia afuera).
        internal static readonly int[] CA = new int[Caras], CB = new int[Caras], CC = new int[Caras];
        // Aristas: sus dos vertices y las dos caras que las comparten (la malla es cerrada).
        internal const int Aristas = 30;
        internal static readonly int[] AV0 = new int[Aristas], AV1 = new int[Aristas], AF0 = new int[Aristas], AF1 = new int[Aristas];

        static Estrella3d()
        {
            for (int i = 0; i < Borde; i++)
            {
                double t = i * Math.PI / 5;
                float r = (i & 1) == 0 ? 1f : Valle;
                MX[i] = r * (float)Math.Sin(t); MY[i] = r * (float)Math.Cos(t); MZ[i] = 0f;
            }
            MX[10] = 0; MY[10] = 0; MZ[10] = Relieve;
            MX[11] = 0; MY[11] = 0; MZ[11] = -Relieve;
            for (int i = 0; i < Borde; i++)
            {
                int j = (i + 1) % Borde;
                CA[i] = 10; CB[i] = j; CC[i] = i;                   // delante
                CA[Borde + i] = 11; CB[Borde + i] = i; CC[Borde + i] = j;   // detras
            }
            Normaliza(-.55, .62, .56, out L1x, out L1y, out L1z);           // la principal: arriba a la izquierda, delante
            Normaliza(.75, -.25, .44, out L2x, out L2y, out L2z);           // la fria: a la derecha
            Normaliza(L1x, L1y, L1z + 1.0, out H1x, out H1y, out H1z);       // medio camino entre la principal y la vista
            int a = 0;
            for (int i = 0; i < Borde; i++)
            {
                int j = (i + 1) % Borde, k = (i + Borde - 1) % Borde;
                AV0[a] = i; AV1[a] = j; AF0[a] = i; AF1[a] = Borde + i; a++;          // el filo del borde
                AV0[a] = 10; AV1[a] = i; AF0[a] = k; AF1[a] = i; a++;                 // la arista del centro de delante
                AV0[a] = 11; AV1[a] = i; AF0[a] = Borde + k; AF1[a] = Borde + i; a++; // y la de detras
            }
        }

        // Una pose: gira (y alrededor del eje vertical, luego p hacia adelante y r en el plano) y proyecta con
        // perspectiva. Deja en px/py la pantalla, en vz la profundidad y, por cara, su normal, su centro (z) y si
        // mira hacia la camara. Todo en arreglos que pasa quien llama (sin reservas).
        internal static void Proyecta(float cx, float cy, float radio, double yaw, double pitch, double roll,
            float[] px, float[] py, float[] vx, float[] vy, float[] vz,
            float[] nx, float[] ny, float[] nz, float[] zc, bool[] mira)
        {
            double cyw = Math.Cos(yaw), syw = Math.Sin(yaw), cp = Math.Cos(pitch), sp = Math.Sin(pitch), cr = Math.Cos(roll), sr = Math.Sin(roll);
            for (int i = 0; i < Vertices; i++)
            {
                double x = MX[i], y = MY[i], z = MZ[i];
                double x1 = x * cr - y * sr, y1 = x * sr + y * cr;              // en el plano
                double x2 = x1 * cyw + z * syw, z2 = -x1 * syw + z * cyw;       // alrededor del eje vertical
                double y3 = y1 * cp - z2 * sp, z3 = y1 * sp + z2 * cp;          // hacia adelante
                vx[i] = (float)x2; vy[i] = (float)y3; vz[i] = (float)z3;
                double f = Camara / (Camara - z3);
                px[i] = cx + (float)(radio * x2 * f); py[i] = cy - (float)(radio * y3 * f);
            }
            for (int c = 0; c < Caras; c++)
            {
                int a = CA[c], b = CB[c], d = CC[c];
                float ux = vx[b] - vx[a], uy = vy[b] - vy[a], uz = vz[b] - vz[a];
                float wx = vx[d] - vx[a], wy = vy[d] - vy[a], wz = vz[d] - vz[a];
                float qx = uy * wz - uz * wy, qy = uz * wx - ux * wz, qz = ux * wy - uy * wx;
                float l = (float)Math.Sqrt(qx * qx + qy * qy + qz * qz); if (l < 1e-12f) l = 1e-12f;
                nx[c] = qx / l; ny[c] = qy / l; nz[c] = qz / l;
                float mx = (vx[a] + vx[b] + vx[d]) / 3f, my = (vy[a] + vy[b] + vy[d]) / 3f, mz = (vz[a] + vz[b] + vz[d]) / 3f;
                zc[c] = mz;
                // Mira hacia la camara (que esta en z = Camara): la normal apunta hacia ella desde el centro de la cara.
                mira[c] = nx[c] * (0f - mx) + ny[c] * (0f - my) + nz[c] * (Camara - mz) > 0f;
            }
        }

        // Orden de pintado: de atras (z menor) hacia adelante. Insercion sobre 20 elementos, sin reservas.
        internal static void Ordena(float[] zc, int[] orden)
        {
            for (int i = 0; i < Caras; i++) orden[i] = i;
            for (int i = 1; i < Caras; i++)
            {
                int v = orden[i]; float z = zc[v]; int j = i - 1;
                while (j >= 0 && zc[orden[j]] > z) { orden[j + 1] = orden[j]; j--; }
                orden[j + 1] = v;
            }
        }

        // La luz de una cara (0..1, mas un brillo aparte). Luz principal arriba a la izquierda y delante (la de las
        // caras plateadas del icono), otra fria y mas debil a la derecha, y un poco de ambiente. Con 'brillo' se
        // suma el reflejo de la luz principal (Blinn), que corre por las facetas al girar.
        private static readonly float L1x, L1y, L1z, L2x, L2y, L2z, H1x, H1y, H1z;
        private static void Normaliza(double x, double y, double z, out float ox, out float oy, out float oz)
        { double l = Math.Sqrt(x * x + y * y + z * z); ox = (float)(x / l); oy = (float)(y / l); oz = (float)(z / l); }
        internal static float Luz(float nx, float ny, float nz, bool brillo, out float reflejo)
        {
            float d1 = nx * L1x + ny * L1y + nz * L1z, d2 = nx * L2x + ny * L2y + nz * L2z;
            float luz = .20f + .78f * Math.Max(0f, d1) + .30f * Math.Max(0f, d2);
            reflejo = 0f;
            if (brillo)
            {
                float h = nx * H1x + ny * H1y + nz * H1z;
                if (h > 0f) { float h2 = h * h, h4 = h2 * h2, h8 = h4 * h4; reflejo = h8 * h8; }   // h^16
            }
            return luz;
        }

        // E3 (PulidoGpu.cs): el brillo de la luz principal con la potencia que se pida (16 de siempre; mas, mas nitido).
        internal static float Especular(float nx, float ny, float nz, float potencia)
        {
            float h = nx * H1x + ny * H1y + nz * H1z;
            return h > 0f ? (float)Math.Pow(h, potencia) : 0f;
        }
        // El color de una cara: el metal (plata azulada) tenido con el color de la cancion, por su luz, mas el reflejo
        // en blanco. Alfa aparte.
        internal static uint ColorCara(uint tinte, float luz, float reflejo, uint alfa)
        {
            float tr = ((tinte >> 16) & 255) / 255f, tg = ((tinte >> 8) & 255) / 255f, tb = (tinte & 255) / 255f;
            float r = .80f * tr * luz + .95f * reflejo, g = .86f * tg * luz + .95f * reflejo, b = .97f * tb * luz + .95f * reflejo;
            return (alfa << 24) | (Byte(r) << 16) | (Byte(g) << 8) | Byte(b);
        }
        private static uint Byte(float v) { return v <= 0f ? 0u : v >= 1f ? 255u : (uint)(v * 255f + .5f); }
    }


    internal sealed partial class VistaGrafica
    {
        private const float StarCrucero = .40f, StarCruceroMedios = .30f, StarCruceroAgudos = .20f;
        private double starTravelX = .65, starTravelY = 2.8, starYaw, moonMotionTime, starT = -1;
        private float velX, velY, spin;
        private readonly float[] e3px = new float[Estrella3d.Vertices], e3py = new float[Estrella3d.Vertices];
        private readonly float[] e3vx = new float[Estrella3d.Vertices], e3vy = new float[Estrella3d.Vertices], e3vz = new float[Estrella3d.Vertices];
        private readonly float[] e3nx = new float[Estrella3d.Caras], e3ny = new float[Estrella3d.Caras], e3nz = new float[Estrella3d.Caras];
        private readonly float[] e3zc = new float[Estrella3d.Caras];
        private readonly bool[] e3mira = new bool[Estrella3d.Caras];
        private readonly int[] e3orden = new int[Estrella3d.Caras];
        private struct Eco { public double T, Yaw; public float X, Y, R, Roll, Pitch; }
        private readonly Eco[] ecos = new Eco[12]; private int ecoSig; private float ecoCamino, ecoUltX, ecoUltY; private bool ecoHay;

        private static void Avanza(ref double fase, ref float vel, float meta, float dt)
        {
            const double respuesta = 5;
            double mezcla = 1 - Math.Exp(-dt * respuesta);
            fase += meta * dt + (vel - meta) * mezcla / respuesta;
            vel += (meta - vel) * (float)mezcla;
        }
        internal static float DiametroEstrella(float alto) { return Math.Max(48, Math.Min(180, alto * .115f)); }
        internal static void CentroViaje(float w, float h, double px, double py, float radio, float arriba, float abajo, out float x, out float y)
        {
            float mx = Math.Min(w * .45f, Math.Max(12, radio * 1.65f));
            float top = Math.Max(arriba + radio, Math.Min(h * .25f, Math.Max(12, radio * 1.65f)));   // (en Rocola OS, bajo la barra de arriba)
            float bottom = Math.Max(top, Math.Min(abajo - radio * 1.8f, h * .52f));
            double fx = .5 + .49 * Math.Sin(px + .18 * Math.Sin(py));
            double fy = .5 + .48 * Math.Sin(py + .24 * Math.Sin(px * .71));
            x = mx + (w - 2 * mx) * (float)fx; y = top + (bottom - top) * (float)fy;
        }

        // La estrella del cielo, detras de todo (va al final del cielo). 'reposo' (0..1): crece, va mas deprisa y se ve entera.
        private void EstrellaDelCielo(Controlador c, float W, float H, float reposo)
        {
            Form1 m = c.motor;
            if (!m.EstrellaCielo || c.AyudaAbierta || c.AyudaRegresando) return;   // durante la ayuda, la estrella es la de la ayuda
            double t = reloj.Elapsed.TotalSeconds; float dt = starT < 0 ? 0f : (float)Math.Min(.1, t - starT); starT = t;
            float calma = .18f + .82f * reposo;
            Avanza(ref starTravelX, ref velX, calma * (.13f + .62f * StarCrucero), dt);
            Avanza(ref starTravelY, ref velY, calma * (.10f + .30f * StarCruceroMedios + .22f * StarCruceroAgudos), dt);
            Avanza(ref starYaw, ref spin, calma * (float)(2 * Math.PI) * (.16f + .34f * StarCrucero), dt);
            moonMotionTime += dt * (.30f + 1.30f * reposo) * (1 + .55 * StarCrucero);
            float diam = DiametroEstrella(H) * (1 + .32f * reposo), r = diam * .5f * .86f;
            float cx, cy; CentroViaje(W, H, starTravelX, starTravelY, DiametroEstrella(H) * 1.32f * .5f, 64 * k, H - 120 * k, out cx, out cy);
            float vis = .40f + .60f * reposo;
            uint tinte = m.ColorSonando != 0 ? Mezcla(0xC9DAFFu, m.ColorSonando, .45f) : 0xC9DAFFu;
            tinte = 0xFF000000u | ((72 + ((tinte >> 16) & 255) * 183 / 255) << 16) | ((72 + ((tinte >> 8) & 255) * 183 / 255) << 8) | (72 + (tinte & 255) * 183 / 255);
            float roll = (float)((.08 + .16 * StarCrucero) * Math.Sin(moonMotionTime * .31));
            double pitch = .22 + .08 * Math.Sin(moonMotionTime * .21);
            // la estela: copias congeladas, una por radio recorrido, que nacen transparentes y se apagan en 1,2 s
            if (m.EstelaEstelar)
            {
                if (ecoHay) ecoCamino += (float)Math.Sqrt((cx - ecoUltX) * (cx - ecoUltX) + (cy - ecoUltY) * (cy - ecoUltY));
                ecoUltX = cx; ecoUltY = cy; ecoHay = true;
                if (ecoCamino >= r) { ecoCamino = 0; ecos[ecoSig] = new Eco { T = t, X = cx, Y = cy, R = r, Yaw = starYaw, Roll = roll, Pitch = (float)pitch }; ecoSig = (ecoSig + 1) % ecos.Length; }
                for (int i = 0; i < ecos.Length; i++)
                {
                    int j = (ecoSig + i) % ecos.Length; Eco e = ecos[j];
                    double edad = t - e.T; if (e.R <= 0 || edad < 0 || edad >= 1.2) continue;
                    float entra = edad < .06 ? (float)(edad / .06) : 1f, queda = (float)Math.Pow(1 - edad / 1.2, 1.4);
                    float a = .80f * vis * entra * queda * .55f;
                    Solido(e.X, e.Y, e.R, e.Yaw, e.Pitch, e.Roll, PaletaEco(tinte, j), a, m.Estrella3d);
                }
            }
            // la corona y el polvo de detras
            if (m.HaloLunar) { d.Brillo(cx, cy, r * 1.9f, Color4.Hex(tinte & 0xFFFFFF, 50 / 255f * vis * 2.2f)); d.Brillo(cx - r * .16f, cy - r * .06f, r * 1.2f, Color4.Hex(0xC9DAFF, 64 / 255f * vis * 1.6f)); }
            if (m.PolvoLunar) Polvo(cx, cy, r, vis, false);
            Solido(cx, cy, r, starYaw, pitch, roll, tinte, 1f, m.Estrella3d);
            if (m.PolvoLunar) Polvo(cx, cy, r, vis, true);
        }
        private static uint Mezcla(uint a, uint b, float f)
        {
            uint R(uint x, int s) { return (x >> s) & 255; }
            uint m(int s) { return (uint)(R(a, s) + (R(b, s) - (float)R(a, s)) * f); }
            return (m(16) << 16) | (m(8) << 8) | m(0);
        }
        private static uint PaletaEco(uint tinte, int i)
        {
            uint[] p = { 0xFF7AD9u, 0x7AD9FFu, 0xFFD45Au, 0x9D7AFFu, 0x7AFFB0u };
            return 0xFF000000u | Mezcla(tinte & 0xFFFFFF, p[i % p.Length], .55f);
        }
        // El solido de XP (o la estrella plana del icono con estrella_3d=0): sus caras de atras hacia adelante, iluminadas.
        private void Solido(float cx, float cy, float r, double yaw, double pitch, double roll, uint tinte, float alfa, bool tresD)
        {
            if (!tresD) { yaw = 0; pitch = 0; }
            Estrella3d.Proyecta(cx, cy, r, yaw, pitch, roll, e3px, e3py, e3vx, e3vy, e3vz, e3nx, e3ny, e3nz, e3zc, e3mira);
            Estrella3d.Ordena(e3zc, e3orden);
            for (int o = 0; o < Estrella3d.Caras; o++)
            {
                int f = e3orden[o]; if (!e3mira[f]) continue;
                float refl; float luz = Estrella3d.Luz(e3nx[f], e3ny[f], e3nz[f], true, out refl);
                if (!tresD) { luz = .95f; refl = 0f; }
                uint col = Estrella3d.ColorCara(tinte, luz, refl, 255u);
                Color4 c4 = Color4.Hex(col & 0xFFFFFF, alfa);
                int a = Estrella3d.CA[f], b = Estrella3d.CB[f], cc = Estrella3d.CC[f];
                d.Triangulo(e3px[a], e3py[a], e3px[b], e3py[b], e3px[cc], e3py[cc], c4);
            }
        }
        // El polvo: tres orbitas con su cola de seis granos; los de delante se pintan despues de la estrella.
        private void Polvo(float cx, float cy, float r, float vis, bool delante)
        {
            for (int orb = 0; orb < 3; orb++)
            {
                double ang = moonMotionTime * (.55 + orb * .12) + orb * 2.094;
                for (int cola = 5; cola >= 0; cola--)
                {
                    double a = ang - cola * .05, s = Math.Sin(a);
                    if ((s >= 0) != delante) continue;
                    float ox = (float)Math.Cos(a) * r * (1.55f + orb * .22f), oy = (float)s * r * (.38f + orb * .08f);
                    float g = r * (.10f - cola * .012f);
                    d.Brillo(cx + ox, cy + oy * .9f - oy * .3f, Math.Max(1.5f, g * 2.4f), Color4.Hex(0xEAF2FF, vis * (.55f - cola * .08f)));
                }
            }
        }
    }
}

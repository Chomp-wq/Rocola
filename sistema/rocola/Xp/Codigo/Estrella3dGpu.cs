// LA ESTRELLA EN 3D, GIRANDO, Y SU ESTELA DE COPIAS (solo tarjeta grafica)
//
// La estrella grande del fondo era una imagen plana (la del icono, horneada una vez) que se deslizaba por el cielo con
// un leve bamboleo. Aqui es un solido de verdad: la misma estrella de cinco puntas del icono, con el centro levantado
// por delante y por detras, veinte caras planas. Se gira en 3D -una vuelta entera alrededor de su eje vertical, con
// algo de inclinacion para que se vean las caras de arriba y de abajo- y cada cara se ilumina segun hacia donde mira:
// una luz principal arriba a la izquierda (la de las caras plateadas del icono), otra fria y mas debil a la derecha
// (el azul acero) y un brillo que corre por las facetas al girar. De frente se ve como el icono.
//
// Todo se calcula en el procesador y la tarjeta solo pinta triangulos ya colocados, como los cubos de las portadas:
// 12 vertices, 20 caras, ordenadas de atras hacia adelante. Los bordes de la silueta llevan una franja de un pixel
// que se desvanece (sin ella los bordes de una malla se ven dentados). Memoria reservada una vez; al dibujar no se
// reserva nada. El renderizador por procesador no se toca (sigue con su luna).
//
// LA ESTELA (tipo Sandevistan). Copias congeladas de la estrella tal como estaba -en su sitio y con su giro de ese
// momento-, cada una con un color de la paleta de la cancion, cada vez mas transparentes. Tres cosas las hacen
// distintas de los ecos de antes:
//   - se dejan por DISTANCIA recorrida, no por tiempo: quedan igual de separadas vaya rapido o lento (y si la estrella
//     casi no se mueve, casi no deja copias, que es lo que tiene que pasar);
//   - cada copia NACE transparente detras de la estrella y aparece en 60 ms: no hay copias que salten a la vista;
//   - como la estrella gira, cada copia guarda un giro distinto: la estela muestra el giro por pasos.
//
// LOS SALTITOS. La estrella se coloca para el momento en que se va a VER su cuadro, y el panel dibuja un cuadro por
// refresco de la pantalla: ver RelojPantallaGpu.cs.
#if ROCOLA_GPU
using System;

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

    internal sealed partial class GpuPanel
    {
        public bool Estrella3D = true;                  // Ctrl+F3 -> Fondo: "Estrella en 3D girando (GPU)"
        private double starYaw;                         // giro alrededor del eje vertical (radianes, sin tope)
        private float starSpin;                         // su velocidad, suavizada (rad/s)
        private double cuadroMs = -1;                   // el momento del cuadro de ahora para la estrella (UpdateMoonMusic)
        internal const float Estrella3dEscala = .86f;   // la estrella del icono ocupa el 86 % de su cuadro
        internal const float EcoPaso = 1.0f;            // una copia cada radio recorrido
        internal const double EcoVida = 1.2, EcoEntra = .06;   // lo que dura una copia y lo que tarda en aparecer
        // Memoria de la proyeccion (una pose por vez) y de lo que va a la tarjeta: se reserva una sola vez.
        private readonly float[] e3px = new float[Estrella3d.Vertices], e3py = new float[Estrella3d.Vertices];
        private readonly float[] e3vx = new float[Estrella3d.Vertices], e3vy = new float[Estrella3d.Vertices], e3vz = new float[Estrella3d.Vertices];
        private readonly float[] e3nx = new float[Estrella3d.Caras], e3ny = new float[Estrella3d.Caras], e3nz = new float[Estrella3d.Caras];
        private readonly float[] e3zc = new float[Estrella3d.Caras];
        private readonly bool[] e3mira = new bool[Estrella3d.Caras];
        private readonly int[] e3orden = new int[Estrella3d.Caras];
        private readonly uint[] e3col = new uint[Estrella3d.Caras];
        private readonly Tarjeta.Vtx[] e3caras = new Tarjeta.Vtx[Estrella3d.Caras * 3];
        private readonly Tarjeta.Vtx[] e3filo = new Tarjeta.Vtx[Estrella3d.Aristas * 6];
        private readonly Tarjeta.Vtx[] e3ecos = new Tarjeta.Vtx[12 * Estrella3d.Caras * 3];
        private readonly Tarjeta.Vtx[] e3ecoFilo = new Tarjeta.Vtx[12 * Estrella3d.Aristas * 6];
        // Los ecos son poses congeladas: solo cambia su alfa, no su proyeccion ni su luz.
        // Cache fija por slot del anillo, independiente del lote compacto que se manda a D3D.
        // 80 KiB de vertices como maximo por panel; ninguna reserva durante el dibujo.
        private readonly Tarjeta.Vtx[] e3ecoPose = new Tarjeta.Vtx[12 * Estrella3d.Caras * 3];
        private readonly Tarjeta.Vtx[] e3ecoBorde = new Tarjeta.Vtx[12 * Estrella3d.Aristas * 6];
        private readonly StarEcho[] e3ecoClave = new StarEcho[12];
        private readonly bool[] e3ecoValida = new bool[12];
        private readonly bool[] e3ecoDegradado = new bool[12];
        private readonly int[] e3ecoN = new int[12], e3ecoBordeN = new int[12];
        private float starEchoCamino, starEchoUltX, starEchoUltY;   // distancia recorrida desde la ultima copia
        private bool starEchoHayUlt;

        // Inclinacion de la estrella: algo hacia adelante (se ven las caras de arriba) con un vaivén lento.
        private double StarPitch() { return LunaActiva ? .22 + .08 * Math.Sin(moonMotionTime * .21) : .10; }

        internal static float StarEchoAlpha3d(double age, float visibility)
        {
            if (double.IsNaN(age) || age < 0 || age >= EcoVida) return 0;
            float entra = age < EcoEntra ? (float)(age / EcoEntra) : 1f;
            float queda = (float)Math.Pow(1 - age / EcoVida, 1.4);
            return .80f * SafeMoonLevel(visibility) * entra * queda;
        }

        private void DrawStar3d(double now, float cx, float cy, float radius, float roll, uint color)
        {
            float r = radius * Estrella3dEscala;
            double pitch = StarPitch(), yaw = LunaActiva ? starYaw : 0;   // quieta, de frente como el icono
            if (!(ReposoSinEstela && AhorroReposo))
            {
                UpdateStarEchoes3d(now, cx, cy, r, yaw, pitch, roll, color);
                DrawStarEchoes3d(now);
            }
            int filo, mejor; float brillo;
            int n = PoseEstrella(e3caras, 0, e3filo, 0, cx, cy, r, yaw, pitch, roll, color, 255, false, 1.1f, out filo, out mejor, out brillo);
            // La franja va DEBAJO: donde cae sobre otra cara que se ve, esa cara la tapa, y solo queda la de fuera.
            if (filo > 0) stars.DrawMesh(e3filo, filo / 3);
            if (n > 0) stars.DrawMesh(e3caras, n / 3);
            if (EstrellaEspejo) DibujaEspejoEstrella(n, cx, cy, r);   // E5: su reflejo bajo el horizonte (PulidoGpu.cs)
            // E3: el destello en cruz donde mas refleja, que se mueve con el giro (PulidoGpu.cs).
            if (EstrellaDestello && mejor >= 0 && brillo > .2f) { DibujaDestello(e3brilloX, e3brilloY, r, brillo, now); return; }
            // El destello: la faceta que mas refleja la luz principal se enciende al pasar por ella.
            if (mejor >= 0 && brillo > .2f)
            {
                int a = Estrella3d.CA[mejor], b = Estrella3d.CB[mejor], c = Estrella3d.CC[mejor];
                float gx = (e3px[a] + e3px[b] + e3px[c]) / 3f, gy = (e3py[a] + e3py[b] + e3py[c]) / 3f;
                stars.GlowBatchBegin(1);
                stars.GlowBatchAdd(gx, gy, r * (.14f + .22f * brillo), ((uint)(210 * Math.Min(1f, brillo)) << 24) | 0xF1F7FFu);
                stars.GlowBatchEnd();
            }
        }

        // Una pose de la estrella en triangulos: sus caras visibles de atras hacia adelante en 'caras' y la franja de
        // la silueta en 'filo' (se desvanece hacia afuera en 'ancho' pixeles). Con 'eco', color de holograma. Devuelve
        // cuantos vertices de caras escribio.
        private int PoseEstrella(Tarjeta.Vtx[] caras, int c0, Tarjeta.Vtx[] filo, int f0, float cx, float cy, float r,
            double yaw, double pitch, double roll, uint tinte, float alfa, bool eco, float ancho,
            out int nFilo, out int mejor, out float brillo)
        {
            Estrella3d.Proyecta(cx, cy, r, yaw, pitch, roll, e3px, e3py, e3vx, e3vy, e3vz, e3nx, e3ny, e3nz, e3zc, e3mira);
            Estrella3d.Ordena(e3zc, e3orden);
            uint a8 = alfa <= 0 ? 0u : alfa >= 255 ? 255u : (uint)(alfa + .5f);
            int v = c0; mejor = -1; brillo = 0;
            // E1-E4 (PulidoGpu.cs): con alguno encendido, la luz se calcula en cada vertice y no una vez por cara.
            bool pulida = eco ? EstrellaDegradado : (EstrellaDegradado || EstrellaRefleja || EstrellaBorde || EstrellaDestello);
            TinteLuz material = new TinteLuz();
            if (pulida)
            {
                PreparaLuzEstrella(eco);
                if (!eco)
                {
                    material.R = ((tinte >> 16) & 255) / 255f; material.G = ((tinte >> 8) & 255) / 255f; material.B = (tinte & 255) / 255f;
                    if (EstrellaBorde) material.Borde = Luz.Vivo(tinte, .3f);
                    if (EstrellaRefleja) material.Suelo = Luz.Vivo(GeneroArgb, .25f);
                }
            }
            for (int k = 0; k < Estrella3d.Caras; k++)
            {
                int c = e3orden[k];
                if (!e3mira[c]) continue;   // una cara de espaldas nunca se ve en un solido cerrado
                if (pulida)
                {
                    uint ca, cb, cc; float ra, rb, rc;
                    ca = ColorVertice(c, Estrella3d.CA[c], tinte, a8, eco, ref material, out ra);
                    cb = ColorVertice(c, Estrella3d.CB[c], tinte, a8, eco, ref material, out rb);
                    cc = ColorVertice(c, Estrella3d.CC[c], tinte, a8, eco, ref material, out rc);
                    e3col[c] = Promedio3(ca, cb, cc);
                    float rm = Math.Max(ra, Math.Max(rb, rc));
                    if (rm > brillo)
                    {
                        brillo = rm; mejor = c;
                        int iv = ra >= rb && ra >= rc ? Estrella3d.CA[c] : (rb >= rc ? Estrella3d.CB[c] : Estrella3d.CC[c]);
                        e3brilloX = e3px[iv]; e3brilloY = e3py[iv];
                    }
                    V3(caras, v++, e3px[Estrella3d.CA[c]], e3py[Estrella3d.CA[c]], ca);
                    V3(caras, v++, e3px[Estrella3d.CB[c]], e3py[Estrella3d.CB[c]], cb);
                    V3(caras, v++, e3px[Estrella3d.CC[c]], e3py[Estrella3d.CC[c]], cc);
                    continue;
                }
                float refl;
                float luz = Estrella3d.Luz(e3nx[c], e3ny[c], e3nz[c], LunaRealista && !eco, out refl);
                uint col = eco ? ColorEco(tinte, luz, a8) : Estrella3d.ColorCara(tinte, LunaRealista ? luz : .45f + .55f * luz, refl, a8);
                e3col[c] = col;
                if (refl > brillo)
                {
                    brillo = refl; mejor = c;
                    e3brilloX = (e3px[Estrella3d.CA[c]] + e3px[Estrella3d.CB[c]] + e3px[Estrella3d.CC[c]]) / 3f;
                    e3brilloY = (e3py[Estrella3d.CA[c]] + e3py[Estrella3d.CB[c]] + e3py[Estrella3d.CC[c]]) / 3f;
                }
                V3(caras, v++, e3px[Estrella3d.CA[c]], e3py[Estrella3d.CA[c]], col);
                V3(caras, v++, e3px[Estrella3d.CB[c]], e3py[Estrella3d.CB[c]], col);
                V3(caras, v++, e3px[Estrella3d.CC[c]], e3py[Estrella3d.CC[c]], col);
            }
            int f = f0;
            for (int e = 0; e < Estrella3d.Aristas; e++)
            {
                bool m0 = e3mira[Estrella3d.AF0[e]], m1 = e3mira[Estrella3d.AF1[e]];
                if (m0 == m1) continue;                                  // no es borde de la silueta
                int cara = m0 ? Estrella3d.AF0[e] : Estrella3d.AF1[e];
                int i0 = Estrella3d.AV0[e], i1 = Estrella3d.AV1[e];
                float x0 = e3px[i0], y0 = e3py[i0], x1 = e3px[i1], y1 = e3py[i1];
                float ex = x1 - x0, ey = y1 - y0, l = (float)Math.Sqrt(ex * ex + ey * ey);
                if (l < .01f) continue;
                float ox = -ey / l, oy = ex / l;                          // perpendicular...
                int ca = Estrella3d.CA[cara], cb = Estrella3d.CB[cara], cc = Estrella3d.CC[cara];
                float gx = (e3px[ca] + e3px[cb] + e3px[cc]) / 3f - x0, gy = (e3py[ca] + e3py[cb] + e3py[cc]) / 3f - y0;
                if (ox * gx + oy * gy > 0) { ox = -ox; oy = -oy; }        // ...hacia afuera de la cara visible
                uint dentro = e3col[cara], fuera = dentro & 0x00FFFFFFu;
                if (eco) { dentro = Neon(dentro); fuera = dentro & 0x00FFFFFFu; }
                float qx = ox * ancho, qy = oy * ancho;
                V3(filo, f++, x0, y0, dentro); V3(filo, f++, x1, y1, dentro); V3(filo, f++, x1 + qx, y1 + qy, fuera);
                V3(filo, f++, x0, y0, dentro); V3(filo, f++, x1 + qx, y1 + qy, fuera); V3(filo, f++, x0 + qx, y0 + qy, fuera);
            }
            nFilo = f - f0;
            return v - c0;
        }
        private static void V3(Tarjeta.Vtx[] a, int i, float x, float y, uint c)
        { a[i].X = x - .5f; a[i].Y = y - .5f; a[i].Z = 0f; a[i].Rhw = 1f; a[i].Color = c; a[i].U = 0f; a[i].V = 0f; }

        // Holograma: el color de la copia, vivo (algo mas saturado que el de la cancion) y con sus facetas todavia
        // legibles (la luz aclara u oscurece cada cara, pero menos que en la estrella).
        private static uint ColorEco(uint tinte, float luz, uint alfa)
        {
            // Una copia oscura sobre el cielo oscuro se lee como una mancha: el canal mas alto llega al menos a 190
            // (mismo tono, mas luz).
            float tr = (tinte >> 16) & 255, tg = (tinte >> 8) & 255, tb = tinte & 255, alto = Math.Max(tr, Math.Max(tg, tb));
            if (alto < 190f) { float e = 190f / Math.Max(1f, alto); tr *= e; tg *= e; tb *= e; }
            float k = .58f + .42f * Math.Min(1f, luz);
            float r = tr * k, g = tg * k, b = tb * k, m = (r + g + b) / 3f;
            r = m + (r - m) * 1.35f; g = m + (g - m) * 1.35f; b = m + (b - m) * 1.35f;
            return (alfa << 24) | (Canal(r) << 16) | (Canal(g) << 8) | Canal(b);
        }
        private static uint Canal(float v) { return v <= 0f ? 0u : v >= 255f ? 255u : (uint)(v + .5f); }
        // El filo de neon de una copia: su color, mas claro, con la mitad de su alfa (se suma encima).
        private static uint Neon(uint c)
        {
            uint a = (c >> 24) / 2, r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            r = r + (255 - r) / 3; g = g + (255 - g) / 3; b = b + (255 - b) / 3;
            return (a << 24) | (r << 16) | (g << 8) | b;
        }

        private void UpdateStarEchoes3d(double now, float x, float y, float r, double yaw, double pitch, float roll, uint color)
        {
            if (starEchoWidth != Width || starEchoHeight != Height || now < starEchoClock ||
                (starEchoClock >= 0 && now - starEchoClock > .3))
            { ResetStarEchoes(); starEchoWidth = Width; starEchoHeight = Height; }
            float dt = starEchoClock < 0 ? 0 : (float)Math.Max(0, Math.Min(.1, now - starEchoClock));
            starEchoClock = now;
            float target = EstelaEstelar && LunaActiva ? SafeMoonLevel(idleBlend) : 0;
            starEchoVisibility += (target - starEchoVisibility) * (1 - (float)Math.Exp(-dt * 8));
            if (target <= 0)
            {
                if (starEchoVisibility < .002f) { starEchoCount = 0; starEchoLast = -1; }
                starEchoHayUlt = false;
                return;
            }
            if (!starEchoHayUlt) { starEchoHayUlt = true; starEchoUltX = x; starEchoUltY = y; starEchoCamino = 0; return; }
            float dx = x - starEchoUltX, dy = y - starEchoUltY;
            starEchoUltX = x; starEchoUltY = y;
            starEchoCamino += (float)Math.Sqrt(dx * dx + dy * dy);
            float paso = Math.Max(4f, r * EcoPaso);
            if (starEchoCamino < paso) return;
            starEchoCamino = Math.Min(starEchoCamino - paso, paso);   // si va muy rapido no se acumula deuda
            starEchoes[starEchoHead] = new StarEcho { Time = now, X = x, Y = y, Radius = r, Angle = roll, Yaw = yaw, Pitch = (float)pitch,
                Strength = .48f + .52f * StarCrucero, Color = StarEchoColor(color) };
            starEchoHead = (starEchoHead + 1) % starEchoes.Length;
            starEchoCount = Math.Min(starEchoes.Length, starEchoCount + 1); starEchoLast = now;
        }

        private void DrawStarEchoes3d(double now)
        {
            int v, f;
            PreparaEcos3d(now, out v, out f);
            if (v > 0) stars.DrawMesh(e3ecos, v / 3);
            if (f > 0) stars.DrawMeshAdd(e3ecoFilo, f / 3);
        }

        private void PreparaEcos3d(double now, out int v, out int f)
        {
            v = f = 0;
            for (int i = 0; i < starEchoCount; i++)
            {
                int slot = (starEchoHead - starEchoCount + i + starEchoes.Length) % starEchoes.Length;
                StarEcho echo = starEchoes[slot];
                float alpha = StarEchoAlpha3d(now - echo.Time, starEchoVisibility) * echo.Strength;
                if (alpha < .003f) continue;
                StarEcho key = e3ecoClave[slot];
                int c0 = slot * Estrella3d.Caras * 3, f0 = slot * Estrella3d.Aristas * 6;
                if (!e3ecoValida[slot] || e3ecoDegradado[slot] != EstrellaDegradado || echo.X != key.X || echo.Y != key.Y || echo.Radius != key.Radius ||
                    echo.Yaw != key.Yaw || echo.Pitch != key.Pitch || echo.Angle != key.Angle || echo.Color != key.Color)
                {
                    int filo, mejor; float brillo;
                    e3ecoN[slot] = PoseEstrella(e3ecoPose, c0, e3ecoBorde, f0, echo.X, echo.Y, echo.Radius,
                        echo.Yaw, echo.Pitch, echo.Angle, echo.Color, 255f, true, 1.8f,
                        out filo, out mejor, out brillo);
                    e3ecoBordeN[slot] = filo;
                    e3ecoClave[slot] = echo; e3ecoValida[slot] = true;
                    e3ecoDegradado[slot] = EstrellaDegradado;
                }
                // Mismo redondeo que PoseEstrella; el filo aditivo usa alfa entero / 2.
                float alfa = alpha * 255f;
                uint a8 = alfa <= 0 ? 0u : alfa >= 255 ? 255u : (uint)(alfa + .5f);
                for (int k = 0; k < e3ecoN[slot]; k++)
                {
                    Tarjeta.Vtx vertex = e3ecoPose[c0 + k];
                    vertex.Color = (a8 << 24) | (vertex.Color & 0xFFFFFFu);
                    e3ecos[v++] = vertex;
                }
                for (int k = 0; k < e3ecoBordeN[slot]; k++)
                {
                    Tarjeta.Vtx vertex = e3ecoBorde[f0 + k];
                    vertex.Color = (vertex.Color & 0xFFFFFFu) | (vertex.Color >= 0x1000000u ? (a8 / 2) << 24 : 0u);
                    e3ecoFilo[f++] = vertex;
                }
            }
        }
    }
}
#endif

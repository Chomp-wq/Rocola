// ====================================================================================================
//  PULIR LOS GRAFICOS (14-10-2026). Peticion del usuario: «Quiero pulir los graficos de la rocola: la profundidad de
//  colores y degradados, y reflejos en la estrella animada, y mejoras de ese tipo que tu tambien me propongas»; y luego
//  «Haz todo menos O2 y O3» (PULIDO_GRAFICO_PROPUESTAS_MODEST_2026-10-14.md). Cada una es una opcion visual con su
//  interruptor en Ctrl+F3 -> Fondo -> hoja Pulido, encendida de fabrica. Solo con tarjeta grafica: el dibujo por CPU no
//  se toca.
//
//   C1. EL CIELO DE MUCHAS PARADAS. El degradado del cielo eran dos tramos rectos (arriba -> parada media -> abajo) y en
//       la parada se veia el quiebre. Ahora son doce franjas que siguen una curva suave por las tres paradas (sin
//       quiebre: la pendiente sigue de un tramo al otro), calculada como LUZ y no como numeros.
//   C2. COLORES CON MAS PROFUNDIDAD. Mezclar dos colores como numeros (0..255) da un medio mas oscuro y sucio de lo que
//       daria la luz: entre azul y violeta sale un gris morado. Las mezclas que hace el procesador (los tintes de las
//       nebulosas, la aurora) se hacen ahora como luz. Y los colores ganan algo de vida en los tonos medios (los ya
//       saturados casi no cambian) y el negro de arriba se hunde un poco hacia azul noche.
//   C3. LA AURORA MAS LIMPIA. Sus rampas de color (del pie a la punta y de grave a agudo) como luz, y con una curva suave
//       en vez de una recta: sin escalones entre franja y franja.
//   C4. LA INTERFAZ SIN ESCALONES. Los degradados oscuros que se hornean (panel de reposo, globos de la ayuda, tarjetas)
//       se pintan con tramado: cada pixel redondea a su manera y la franja de un nivel desaparece.
//   E1..E5 y O1: la estrella y la portada elegida (ver mas abajo).
// ====================================================================================================
#if ROCOLA_GPU
using System;
using System.Drawing;

namespace Rocola
{
    // La luz de un color: sus canales pasados de numeros de pantalla (sRGB) a cantidad de luz, y vuelta.
    internal static class Luz
    {
        private static readonly float[] aLuz = HazALuz();
        private const int TabN = 4096;
        private static readonly byte[] aPantalla = HazAPantalla();
        private static float[] HazALuz()
        {
            float[] t = new float[256];
            for (int i = 0; i < 256; i++)
            {
                double c = i / 255.0;
                t[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
            }
            return t;
        }
        private static byte[] HazAPantalla()
        {
            byte[] t = new byte[TabN + 1];
            for (int i = 0; i <= TabN; i++)
            {
                double l = (double)i / TabN;
                double c = l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(l, 1.0 / 2.4) - 0.055;
                int v = (int)Math.Round(c * 255.0);
                t[i] = (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
            }
            return t;
        }
        internal static float ALuz(uint canal) { return aLuz[canal & 255]; }
        internal static uint APantalla(float l)
        {
            if (!(l > 0f)) return 0u;
            if (l >= 1f) return 255u;
            return aPantalla[(int)(l * TabN + 0.5f)];
        }
        // Mezcla como luz. El alfa (si lo hay) se mezcla como numero.
        internal static uint Mezcla(uint c0, uint c1, float f)
        {
            if (!(f > 0f)) return c0;
            if (f >= 1f) return c1;
            float g = 1f - f;
            uint r = APantalla(ALuz(c0 >> 16) * g + ALuz(c1 >> 16) * f);
            uint gr = APantalla(ALuz(c0 >> 8) * g + ALuz(c1 >> 8) * f);
            uint b = APantalla(ALuz(c0) * g + ALuz(c1) * f);
            uint a = (uint)(((c0 >> 24) & 255) * g + ((c1 >> 24) & 255) * f + 0.5f);
            return (a << 24) | (r << 16) | (gr << 8) | b;
        }
        // Mas vida en los tonos medios: se aparta cada canal de la luminosidad, mas cuanto menos saturado estaba el color
        // (lo ya vivo casi no cambia y nada se quema). Conserva la luminosidad y el alfa.
        internal static uint Vivo(uint c, float k)
        {
            float r = ALuz(c >> 16), g = ALuz(c >> 8), b = ALuz(c);
            float lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            float sat = mx > 1e-5f ? (mx - mn) / mx : 0f;
            float e = 1f + k * (1f - sat);
            r = lum + (r - lum) * e; g = lum + (g - lum) * e; b = lum + (b - lum) * e;
            // Si algun canal se pasa, se recorta hacia la luminosidad en vez de quemar.
            float alto = Math.Max(r, Math.Max(g, b));
            if (alto > 1f) { float s = (1f - lum) / Math.Max(1e-5f, alto - lum); r = lum + (r - lum) * s; g = lum + (g - lum) * s; b = lum + (b - lum) * s; }
            return (c & 0xFF000000u) | (APantalla(r) << 16) | (APantalla(g) << 8) | APantalla(b);
        }
        // El negro de arriba, mas hondo: un 18 % menos de luz y algo menos de rojo y verde que de azul (azul noche).
        internal static uint Hondo(uint c)
        {
            float r = ALuz(c >> 16) * 0.78f, g = ALuz(c >> 8) * 0.82f, b = ALuz(c) * 0.92f;
            return (c & 0xFF000000u) | (APantalla(r) << 16) | (APantalla(g) << 8) | APantalla(b);
        }
        // Una curva suave por tres puntos (0, x1, 1) que pasa por cada uno sin quiebre (Hermite monotona, como luz).
        internal static float Curva3(float x, float x1, float y0, float y1, float y2)
        {
            if (x1 <= 0.001f) x1 = 0.001f; else if (x1 >= 0.999f) x1 = 0.999f;
            float s0 = (y1 - y0) / x1, s1 = (y2 - y1) / (1f - x1);
            float m1 = (s0 * s1 <= 0f) ? 0f : 2f * s0 * s1 / (s0 + s1);   // en la parada: la media armonica (no se pasa)
            float m0 = s0, m2 = s1;
            float xa, xb, ya, yb, ma, mb;
            if (x <= x1) { xa = 0f; xb = x1; ya = y0; yb = y1; ma = m0; mb = m1; }
            else { xa = x1; xb = 1f; ya = y1; yb = y2; ma = m1; mb = m2; }
            float hseg = xb - xa, t = (x - xa) / hseg;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float t2 = t * t, t3 = t2 * t;
            float v = (2f * t3 - 3f * t2 + 1f) * ya + (t3 - 2f * t2 + t) * hseg * ma + (-2f * t3 + 3f * t2) * yb + (t3 - t2) * hseg * mb;
            float lo = Math.Min(ya, yb), hi = Math.Max(ya, yb);
            return v < lo ? lo : (v > hi ? hi : v);
        }
        // El color del cielo a la altura x (0 arriba, 1 abajo) con la parada media en x1.
        internal static uint ColorCielo(float x, float x1, uint top, uint mid, uint bot)
        {
            uint r = APantalla(Curva3(x, x1, ALuz(top >> 16), ALuz(mid >> 16), ALuz(bot >> 16)));
            uint g = APantalla(Curva3(x, x1, ALuz(top >> 8), ALuz(mid >> 8), ALuz(bot >> 8)));
            uint b = APantalla(Curva3(x, x1, ALuz(top), ALuz(mid), ALuz(bot)));
            return 0xFF000000u | (r << 16) | (g << 8) | b;
        }
    }

    public partial class Form1
    {
        // C4 «Interfaz sin escalones» (Ctrl+F3 -> Fondo -> Pulido). Un degradado vertical horneado con tramado: cada pixel
        // redondea con un umbral distinto (Bayer 4x4), asi que entre dos niveles no queda una franja recta sino un paso
        // repartido que el ojo promedia. Lo usan solo los paneles que se hornean para la tarjeta grafica (reposo, globos
        // de la ayuda, tarjetas de las piezas); con el interruptor apagado, el degradado de siempre de Windows.
        internal static volatile bool InterfazTramada = true;
        private static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        // Cuatro fuentes de 4 x hasta 2048 pixeles: 128 KiB de pixeles como maximo.
        // El llamador recibe su propio pincel, que puede transformar/disponer sin tocar la fuente.
        // El cerrojo protege crear pinceles/disponer mapas de GDI+ si dos horneadores coinciden.
        private sealed class DegradadoGuardado
        {
            internal int Alto, Inicio, Fin;
            internal Bitmap Mapa;
        }
        private static readonly object degradadoCerrojo = new object();
        private static readonly DegradadoGuardado[] degradados = new DegradadoGuardado[4];
        private static int degradadoTurno;
        internal static int DegradadosGuardados, DegradadosReusados;
        internal static Brush DegradadoV(Rectangle r, Color c0, Color c1)
        {
            if (!InterfazTramada || r.Height < 2 || r.Width < 1) return new LinearGradientBrush(r, c0, c1, 90f);
            if (r.Height > 2048) return HorneaDegradadoV(r, c0, c1); // los tamanos extraordinarios no quedan retenidos
            lock (degradadoCerrojo)
            {
                int a = c0.ToArgb(), b = c1.ToArgb();
                for (int i = 0; i < degradados.Length; i++)
                {
                    DegradadoGuardado d = degradados[i];
                    if (d == null || d.Alto != r.Height || d.Inicio != a || d.Fin != b) continue;
                    TextureBrush copia = CopiaDegradado(d.Mapa, r.Location);
                    DegradadosReusados++; return copia;
                }
                Bitmap fuente = HorneaMapaDegradado(r.Height, c0, c1);
                TextureBrush nuevo;
                try { nuevo = CopiaDegradado(fuente, r.Location); }
                catch { fuente.Dispose(); throw; }
                DegradadoGuardado anterior = degradados[degradadoTurno];
                degradados[degradadoTurno] = new DegradadoGuardado { Alto = r.Height, Inicio = a, Fin = b, Mapa = fuente };
                degradadoTurno = (degradadoTurno + 1) % degradados.Length;
                if (anterior != null) anterior.Mapa.Dispose();
                DegradadosGuardados++; return nuevo;
            }
        }
        private static TextureBrush CopiaDegradado(Bitmap fuente, Point origen)
        {
            // Crear desde ARGB original evita otro redondeo de alfa al clonar un TextureBrush.
            TextureBrush copia = new TextureBrush(fuente, WrapMode.Tile);
            try { copia.TranslateTransform(origen.X, origen.Y); return copia; }
            catch { copia.Dispose(); throw; }
        }
        private static Brush HorneaDegradadoV(Rectangle r, Color c0, Color c1)
        {
            using (Bitmap b = HorneaMapaDegradado(r.Height, c0, c1)) return CopiaDegradado(b, r.Location);
        }
        private static Bitmap HorneaMapaDegradado(int alto, Color c0, Color c1)
        {
            Bitmap b = new Bitmap(4, alto, PixelFormat.Format32bppArgb);
            try
            {
                BitmapData d = b.LockBits(new Rectangle(0, 0, 4, alto), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int[] fila = new int[4];
                    for (int y = 0; y < alto; y++)
                    {
                        float t = (y + .5f) / alto;
                        float fa = c0.A + (c1.A - c0.A) * t, fr = c0.R + (c1.R - c0.R) * t, fg = c0.G + (c1.G - c0.G) * t, fb = c0.B + (c1.B - c0.B) * t;
                        for (int x = 0; x < 4; x++)
                        {
                            float u = (Bayer4[(y & 3) * 4 + x] + .5f) / 16f;
                            fila[x] = (Tramo(fa, u) << 24) | (Tramo(fr, u) << 16) | (Tramo(fg, u) << 8) | Tramo(fb, u);
                        }
                        System.Runtime.InteropServices.Marshal.Copy(fila, 0, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), 4);
                    }
                }
                finally { b.UnlockBits(d); }
                return b;
            }
            catch { b.Dispose(); throw; }
        }
        private static int Tramo(float v, float umbral)
        {
            int i = (int)Math.Floor(v + umbral - .5f + .5f);
            return i < 0 ? 0 : (i > 255 ? 255 : i);
        }
    }

    internal sealed partial class GpuStarfield
    {
        // C1 y C2 (Ctrl+F3 -> Fondo -> Pulido). Encendidos de fabrica; apagados, el cielo de siempre.
        public bool CieloSuave = true, ColoresProfundos = true;
        internal const int CieloFranjas = 6;   // por tramo: doce en total
        private D3D9.Vtx[] skySuave;
        private bool cieloSuaveListo;
        private float cieloSuaveAncho, cieloSuaveAlto, cieloSuaveMedio, cieloSuaveExtra;
        private uint cieloSuaveArriba, cieloSuaveCentro, cieloSuaveAbajo;
        private int cieloSuaveTris;
        private readonly float[] cieloSuaveY0 = new float[2 * CieloFranjas];   // donde empieza cada franja (de arriba abajo)
        internal int CielosSuavesCalculados, CielosSuavesReusados;
        // Los colores de las tres paradas, como se van a pintar (C2).
        internal void ColoresCielo(ref uint top, ref uint mid, ref uint bot)
        {
            if (!ColoresProfundos) return;
            top = Luz.Hondo(top); mid = Luz.Vivo(mid, 0.18f); bot = Luz.Vivo(bot, 0.22f);
        }
        // El cielo de doce franjas (C1). 'ancho' y 'extra' son para la imagen intermedia: algo mas ancho, y una franja
        // de abajo del todo con el color de abajo. Devuelve cuantos triangulos.
        internal int ArmaCieloSuave(float ancho, float h, float midY, uint top, uint mid, uint bot, float extra)
        {
            if (cieloSuaveListo && ancho == cieloSuaveAncho && h == cieloSuaveAlto && midY == cieloSuaveMedio &&
                extra == cieloSuaveExtra && top == cieloSuaveArriba && mid == cieloSuaveCentro && bot == cieloSuaveAbajo)
            { CielosSuavesReusados++; return cieloSuaveTris; }
            cieloSuaveListo = false;
            int n = 2 * CieloFranjas * 6 + 6;
            if (skySuave == null || skySuave.Length < n) skySuave = new D3D9.Vtx[n];
            float x1 = h > 0f ? midY / h : 0.5f;
            int v = 0;
            for (int tramo = 0; tramo < 2; tramo++)
            {
                float ya = tramo == 0 ? 0f : midY, yb = tramo == 0 ? midY : h;
                for (int k = 0; k < CieloFranjas; k++)
                {
                    float y0 = ya + (yb - ya) * k / CieloFranjas, y1 = ya + (yb - ya) * (k + 1) / CieloFranjas;
                    uint c0 = Luz.ColorCielo(h > 0f ? y0 / h : 0f, x1, top, mid, bot);
                    uint c1 = Luz.ColorCielo(h > 0f ? y1 / h : 1f, x1, top, mid, bot);
                    cieloSuaveY0[tramo * CieloFranjas + k] = y0;
                    SetQuad(skySuave, v, 0, y0, ancho, y1, c0, c0, c1, c1);
                    v += 6;
                }
            }
            if (extra > 0f) { SetQuad(skySuave, v, 0, h, ancho, h + extra, bot | 0xFF000000u, bot | 0xFF000000u, bot | 0xFF000000u, bot | 0xFF000000u); v += 6; }
            cieloSuaveAncho = ancho; cieloSuaveAlto = h; cieloSuaveMedio = midY; cieloSuaveExtra = extra;
            cieloSuaveArriba = top; cieloSuaveCentro = mid; cieloSuaveAbajo = bot;
            cieloSuaveTris = v / 3; cieloSuaveListo = true; CielosSuavesCalculados++;
            return cieloSuaveTris;
        }
        internal void DibujaCieloSuave(int tris) { if (gpu != null && skySuave != null) gpu.DrawSkyN(skySuave, tris); }
        // Las franjas se pintan de arriba abajo, cada una por su cuenta. Con el estado extendido opaco asentado, desde 'desde' hacia abajo
        // lo tapa el panel (alfa 255, sin mezcla): las franjas que empiezan ahi no se ven nunca y no se mandan. Una franja que solo
        // queda tapada en parte se pinta entera, asi que ningun pixel de lo que se ve cambia (ni su degradado).
        internal int CieloSuaveVisible(int tris, float desde)
        {
            if (!(desde > 0f)) return tris;
            int n = 0;
            while (n < cieloSuaveY0.Length && cieloSuaveY0[n] < desde) n++;
            return Math.Min(tris, Math.Max(1, n) * 2);
        }
    }

    internal sealed partial class GpuPanel
    {
        // ---------------- LA ESTRELLA (E1..E5) ----------------
        // E1 «Estrella con facetas en degradado»: la luz en cada vertice, con la normal de la cara inclinada hacia la del
        //    vertice (desde el centro de la estrella): cada faceta pasa de clara a oscura, como metal con volumen.
        // E2 «La estrella refleja el cielo»: cada punto refleja lo que tiene delante segun hacia donde mira (el reflejo
        //    de la vista): hacia arriba el cielo de arriba, hacia el horizonte el de en medio, hacia abajo el color del
        //    genero. Mas reflejo cuanto mas de canto (Fresnel), como el metal pulido.
        // E3 «Destello nitido en cruz»: el brillo de la luz mas concentrado, y donde mas refleja un destello de ocho
        //    puntas (cuatro largas) que gira despacio, en vez del halo redondo.
        // E4 «Borde de luz»: la silueta se enciende con el color de la cancion, mas cuanto mas de canto.
        // E5 «Reflejo en el horizonte»: una copia invertida, tenue y que se desvanece, bajo la raya del horizonte.
        // Todo son cuentas por vertice (20 caras, 60 vertices) y unos triangulos mas: nada para la tarjeta. Sin cara: solo
        // cambian el metal, sus reflejos y su luz (AGENTS.md).
        // C1 y C2 se guardan aqui y pasan al campo de estrellas en cada cuadro (PasaPulido); el campo dibuja el cielo.
        public bool CieloSuave = true, ColoresProfundos = true;
        private void PasaPulido() { if (stars != null) { stars.CieloSuave = CieloSuave; stars.ColoresProfundos = ColoresProfundos; } }
        public bool EstrellaDegradado = true, EstrellaRefleja = true, EstrellaDestello = true, EstrellaBorde = true, EstrellaEspejo = true;
        private float e3brilloX, e3brilloY;   // donde mas refleja la pose de ahora (PoseEstrella)
        private struct LuzVertice { internal float Largo, Vx, Vy, Vz; }
        private struct TinteLuz { internal float R, G, B; internal uint Borde, Suelo; }
        private readonly LuzVertice[] e3luz = new LuzVertice[Estrella3d.Vertices];
        private void PreparaLuzEstrella(bool eco)
        {
            // Un vertice pertenece a varias caras, pero su longitud y direccion
            // hacia la camara son las mismas en todas. Buffer fijo de 192 bytes.
            for (int i = 0; i < Estrella3d.Vertices; i++)
            {
                float px = e3vx[i], py = e3vy[i], pz = e3vz[i];
                if (EstrellaDegradado) e3luz[i].Largo = (float)Math.Sqrt(px * px + py * py + pz * pz);
                if (!eco && (EstrellaRefleja || EstrellaBorde))
                {
                    float vx = -px, vy = -py, vz = Estrella3d.Camara - pz;
                    float vl = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz); if (vl < 1e-6f) vl = 1e-6f;
                    e3luz[i].Vx = vx / vl; e3luz[i].Vy = vy / vl; e3luz[i].Vz = vz / vl;
                }
            }
        }
        private uint ColorVertice(int c, int vi, uint tinte, uint a8, bool eco, ref TinteLuz material, out float spec)
        {
            float nx = e3nx[c], ny = e3ny[c], nz = e3nz[c];
            float px = e3vx[vi], py = e3vy[vi], pz = e3vz[vi];
            if (EstrellaDegradado)
            {
                float pl = e3luz[vi].Largo;
                if (pl > 1e-5f)
                {
                    nx = nx * .62f + px / pl * .38f; ny = ny * .62f + py / pl * .38f; nz = nz * .62f + pz / pl * .38f;
                    float nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz); if (nl < 1e-6f) nl = 1e-6f;
                    nx /= nl; ny /= nl; nz /= nl;
                }
            }
            float refl;
            float luz = Estrella3d.Luz(nx, ny, nz, false, out refl);
            if (eco) { spec = 0f; return ColorEco(tinte, luz, a8); }
            float vx = e3luz[vi].Vx, vy = e3luz[vi].Vy, vz = e3luz[vi].Vz;
            float nv = nx * vx + ny * vy + nz * vz; if (nv < 0f) nv = 0f;
            float L = LunaRealista ? luz : .45f + .55f * luz;
            float tr = material.R, tg = material.G, tb = material.B;
            float r = .80f * tr * L, g = .86f * tg * L, b = .97f * tb * L;
            if (EstrellaRefleja)
            {
                float d = 2f * (nx * vx + ny * vy + nz * vz);
                float ry = d * ny - vy;   // el reflejo de la vista: hacia donde "mira" este punto
                uint ent = ry >= 0f ? Luz.Mezcla(SkyMid, SkyTop, Math.Min(1f, ry * 1.4f))
                                    : Luz.Mezcla(SkyMid, material.Suelo, Math.Min(1f, -ry * 1.6f));
                ent = Luz.Vivo(ent, .2f);
                // El cielo es oscuro: reflejado tal cual, la estrella se apagaria. Se refleja como luz (algo mas clara).
                float er = Math.Min(1f, ((ent >> 16) & 255) / 255f * 2.1f + .04f), eg = Math.Min(1f, ((ent >> 8) & 255) / 255f * 2.1f + .04f),
                      eb = Math.Min(1f, (ent & 255) / 255f * 2.1f + .06f);
                float f = 1f - nv, kE = .22f + .45f * f * f * f;
                r = r * (1f - kE) + er * kE; g = g * (1f - kE) + eg * kE; b = b * (1f - kE) + eb * kE;
            }
            spec = EstrellaDestello ? 1.05f * Estrella3d.Especular(nx, ny, nz, 40f)
                 : (LunaRealista ? .95f * Estrella3d.Especular(nx, ny, nz, 16f) : 0f);
            r += spec; g += spec; b += spec;
            if (EstrellaBorde)
            {
                float f = 1f - nv, rim = (float)Math.Pow(f, 2.2) * .75f;
                uint viva = material.Borde;
                r += ((viva >> 16) & 255) / 255f * rim; g += ((viva >> 8) & 255) / 255f * rim; b += (viva & 255) / 255f * rim;
            }
            return (a8 << 24) | (Canal(r * 255f) << 16) | (Canal(g * 255f) << 8) | Canal(b * 255f);
        }
        private static uint Promedio3(uint a, uint b, uint c)
        {
            uint r = 0;
            for (int s = 0; s < 32; s += 8) r |= ((((a >> s) & 255u) + ((b >> s) & 255u) + ((c >> s) & 255u) + 1u) / 3u) << s;
            return r;
        }

        // E3: ocho puntas (cuatro largas y cuatro cortas) que se apagan hacia la punta, sumando luz, y un nucleo pequeño.
        private readonly D3D9.Vtx[] destelloV = new D3D9.Vtx[8 * 3];
        private void DibujaDestello(float x, float y, float r, float brillo, double now)
        {
            float a = Math.Min(1f, brillo);
            uint ca = ((uint)(235f * a) << 24) | 0x00F4F8FFu, punta = 0x00F4F8FFu;
            float largo = r * (.30f + .42f * a), ancho = r * .028f + .6f;
            double th = now * .6;
            int i = 0;
            for (int brazo = 0; brazo < 8; brazo++)
            {
                double ang = th + brazo * Math.PI / 4;
                bool corto = (brazo & 1) == 1;
                float len = corto ? largo * .45f : largo, w = corto ? ancho * .7f : ancho;
                float dx = (float)Math.Cos(ang), dy = (float)Math.Sin(ang), qx = -dy * w, qy = dx * w;
                V3(destelloV, i++, x + qx, y + qy, ca); V3(destelloV, i++, x - qx, y - qy, ca); V3(destelloV, i++, x + dx * len, y + dy * len, punta);
            }
            stars.DrawMeshAdd(destelloV, 8);
            stars.GlowBatchBegin(1);
            stars.GlowBatchAdd(x, y, r * (.08f + .10f * a), ((uint)(200f * a) << 24) | 0x00F1F7FFu);
            stars.GlowBatchEnd();
        }

        // E5: la pose de ahora (e3caras), invertida bajo la raya del horizonte, tenue y desvaneciendose hacia abajo. Solo si
        // la estrella entera esta por encima de la raya. Misma mezcla que la estrella; como mucho un tercio de su alfa.
        private D3D9.Vtx[] espejoV;
        internal float Horizonte() { return aurDibujada ? aurDibujadaBase - AurLineaAlza : StatusTopY; }
        private void DibujaEspejoEstrella(int n, float cx, float cy, float r)
        {
            float hy = Horizonte();
            if (n <= 0 || cy + r * 1.05f >= hy || hy >= Height) return;
            if (espejoV == null || espejoV.Length < n) espejoV = new D3D9.Vtx[Math.Max(n, Estrella3d.Caras * 3)];
            float fade = r * 1.6f;
            for (int i = 0; i < n; i++)
            {
                D3D9.Vtx q = e3caras[i];
                float y2 = 2f * hy - (q.Y + .5f);
                float k = 1f - (y2 - hy) / fade; if (k < 0f) k = 0f; else if (k > 1f) k = 1f;
                uint c = q.Color;
                uint al = (uint)(((c >> 24) & 255) * .34f * k);
                uint rr = ((c >> 16) & 255) * 85 / 100, gg = ((c >> 8) & 255) * 85 / 100, bb = (c & 255) * 90 / 100;
                q.Y = y2 - .5f; q.Color = (al << 24) | (rr << 16) | (gg << 8) | bb;
                espejoV[i] = q;
            }
            stars.DrawMesh(espejoV, n / 3);
        }

        // ---------------- O1: EL BRILLO DE CRISTAL EN LA PORTADA ELEGIDA ----------------
        // Cada 6,5 s, una franja diagonal de luz cruza la cara de delante de la portada elegida en 1,2 s, como un reflejo en
        // un vidrio. Suma luz (no tapa la caratula), con un maximo de 70 de 255 en su centro y nada en sus bordes. Solo con la
        // cara casi de frente (mientras gira no se ve) y sin bamboleo.
        public bool PortadaBrillo = true;
        internal const double BrilloPeriodo = 6.5, BrilloBarrido = 1.2;
        private readonly D3D9.Vtx[] brilloV = new D3D9.Vtx[2 * 6 * 3];
        private readonly float[] clipU = new float[12], clipV = new float[12], clipU2 = new float[12], clipV2 = new float[12];
        // Donde va la franja en este momento (u + v, de -0,3 a 2,3), o NaN si no toca.
        internal static float FranjaBrillo(double segundos)
        {
            double t = segundos % BrilloPeriodo; if (t < 0) t += BrilloPeriodo;
            if (t > BrilloBarrido) return float.NaN;
            return (float)(t / BrilloBarrido) * 2.6f - .3f;
        }
        private void DibujaBrilloPortada(float x, float y, float w, float h, double angle, double tilt, float alpha, double ms)
        {
            if (!PortadaBrillo || alpha < .05f || w < 24f || h < 24f || Math.Abs(tilt) > .03) return;
            float s = FranjaBrillo(ms / 1000.0);
            if (float.IsNaN(s)) return;
            // La cara de delante del cubo (la misma cuenta que DrawCube): la que mas mira hacia aqui.
            double R = w / 2.0, cx = x + w / 2.0, mejorZ = -2; int mk = 0;
            for (int k = 0; k < 4; k++) { double z = Math.Sin(angle + Math.PI / 2 + k * Math.PI / 2); if (z > mejorZ) { mejorZ = z; mk = k; } }
            if (mejorZ < .9) return;
            double xa = cx + R * Math.Cos(angle + Math.PI / 4 + mk * Math.PI / 2), xb = cx + R * Math.Cos(angle + Math.PI / 4 + ((mk + 1) % 4) * Math.PI / 2);
            float left = (float)Math.Round(Math.Min(xa, xb)), right = (float)Math.Round(Math.Max(xa, xb));
            if (right - left < 16f) return;
            float pico = 70f * alpha * (float)((mejorZ - .9) / .1);
            const float bw = .22f;
            int v = 0;
            v = FranjaRecortada(v, s - bw, s, 0f, pico, left, y, right - left, h);
            v = FranjaRecortada(v, s, s + bw, pico, 0f, left, y, right - left, h);
            if (v >= 3) stars.DrawMeshAdd(brilloV, v / 3);
        }
        // La parte del cuadrado (0..1, 0..1) entre q0 <= u + v <= q1, en abanico, con el alfa de a0 (en q0) a a1 (en q1).
        private int FranjaRecortada(int v, float q0, float q1, float a0, float a1, float fx, float fy, float fw, float fh)
        {
            int n = 4;
            clipU[0] = 0; clipV[0] = 0; clipU[1] = 1; clipV[1] = 0; clipU[2] = 1; clipV[2] = 1; clipU[3] = 0; clipV[3] = 1;
            n = Recorta(clipU, clipV, n, clipU2, clipV2, q0, true);
            n = Recorta(clipU2, clipV2, n, clipU, clipV, q1, false);
            if (n < 3) return v;
            for (int i = 1; i + 1 < n && v + 3 <= brilloV.Length; i++)
            {
                v = PuntoBrillo(v, clipU[0], clipV[0], q0, q1, a0, a1, fx, fy, fw, fh);
                v = PuntoBrillo(v, clipU[i], clipV[i], q0, q1, a0, a1, fx, fy, fw, fh);
                v = PuntoBrillo(v, clipU[i + 1], clipV[i + 1], q0, q1, a0, a1, fx, fy, fw, fh);
            }
            return v;
        }
        private int PuntoBrillo(int v, float u, float vv, float q0, float q1, float a0, float a1, float fx, float fy, float fw, float fh)
        {
            float t = q1 > q0 ? (u + vv - q0) / (q1 - q0) : 0f; if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            int a = (int)(a0 + (a1 - a0) * t + .5f); if (a < 0) a = 0; else if (a > 255) a = 255;
            V3(brilloV, v, fx + u * fw, fy + vv * fh, ((uint)a << 24) | 0x00FFFFFFu);
            return v + 1;
        }
        // Sutherland-Hodgman contra u + v >= q (minimo) o u + v <= q.
        private static int Recorta(float[] iu, float[] iv, int n, float[] ou, float[] ov, float q, bool minimo)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float di = (iu[i] + iv[i]) - q, dj = (iu[j] + iv[j]) - q;
                if (!minimo) { di = -di; dj = -dj; }
                bool dentroI = di >= 0f, dentroJ = dj >= 0f;
                if (dentroI && m < ou.Length) { ou[m] = iu[i]; ov[m] = iv[i]; m++; }
                if (dentroI != dentroJ && m < ou.Length)
                {
                    float t = di / (di - dj);
                    ou[m] = iu[i] + (iu[j] - iu[i]) * t; ov[m] = iv[i] + (iv[j] - iv[i]) * t; m++;
                }
            }
            return m;
        }

        // C2: el tinte de cada nebulosa. Con «Nebulosas azul / violeta» el color de la cancion se mezcla con su azul o su
        // violeta como luz (antes como numeros: el medio salia gris), y despues gana algo de vida. Apagado, el de siempre.
        private uint TinteNeb(uint original, bool segunda)
        {
            if (stars == null || !stars.ColoresProfundos) return TinteNebulosa(original, segunda, NebulosasCine);
            uint c = original & 0x00FFFFFFu;
            if (NebulosasCine) c = Luz.Mezcla(c, segunda ? 0x009C70E8u : 0x003899D8u, 0.65f) & 0x00FFFFFFu;
            return Luz.Vivo(c, 0.15f) & 0x00FFFFFFu;
        }
    }
}
#endif

#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        // Mapas neutros del estilo natural. Solo se hornean al preparar el estilo o cambiar
        // tamano/detalle; la GPU conserva las mismas capas y les pone el color y la deriva.
        // No se modifica ninguna de las recetas legacy ni se generan ruidos por cuadro.
        internal static Bitmap BakeNebulosaNatural(int w, int h)
        {
            return BakeNebulosaNatural(w, h, true);
        }

        internal static Bitmap BakeNebulosaNatural(int w, int h, bool detallada)
        { return BakeNebulosaPreparada(w, h, detallada, Form1.nebNaturalFuerza, null, false); }

        internal static Bitmap BakeNebulosaPreparada(int w, int h, bool detallada, float fuerza, Func<bool> cancel, bool slow)
        {
            float detail = detallada ? NaturalNebDetail(fuerza) : 0f;
            int[] pixels = NaturalPixels(w, h);
            long next = Stopwatch.GetTimestamp() + CargaTrabajo.LimiteTicks(2);
            for (int y = 0; y < h; y++)
            {
                if (PausaNaturalFila(y, cancel, slow, ref next)) return null;
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)Math.Max(1, w - 1), v = y / (float)Math.Max(1, h - 1);
                    float wx = CloudNoise(u * 3.1f + 17f, v * 3.9f + 6f) - .5f;
                    float wy = CloudNoise(u * 3.6f + 4f, v * 3.3f + 19f) - .5f;
                    float px = u + wx * .085f, py = v + wy * .085f;
                    // Tres regiones separadas, no un velo que tine todo el cielo. Los huecos
                    // son ausencia de luz (alfa cero), no manchas negras pintadas por encima.
                    float envelope = NaturalCloudRegion(px, py, .18f, .23f, .28f, .22f)
                        + .72f * NaturalCloudRegion(px, py, .53f, .40f, .32f, .19f)
                        + .90f * NaturalCloudRegion(px, py, .83f, .20f, .25f, .19f);
                    float broad = CloudNoise(px * 6.5f + 9f, py * 7.5f + 13f);
                    float holes = Math.Max(0f, (broad - .38f) * 1.62f);
                    float ridge = 1f - Math.Abs(CloudNoise(px * 22f + wx * 5f, py * 28f + wy * 4f + 8f) * 2f - 1f);
                    float fine = CloudNoise(px * 54f + 5f, py * 43f + 17f);
                    float filament = ridge * ridge * ridge * (.62f + .38f * fine);
                    float value = Math.Min(1f, envelope) * holes * (.44f + detail * (filament * .98f + fine * .13f));
                    // 6) Globulos y vetas oscuras: polvo que recorta la nube por ausencia de luz.
                    float e = Math.Min(1f, envelope);
                    float lane = 1f - Math.Abs(CloudNoise(px * 11f + 3f, py * 13f + 29f) * 2f - 1f);
                    lane *= lane; lane *= lane * lane;
                    value *= (1f - .92f * GlobulosNatural(px, py)) * (1f - .55f * lane * e);
                    // El detalle suma filamentos sobre una nube base ligera; a cero la base
                    // sigue siendo usable, pero las vetas desaparecen. NebFuerza sigue siendo
                    // la referencia comun del proyecto, sin pasar su control al renderer CPU.
                    int alpha = (int)Math.Min(70f, value * 60f * Form1.NebFuerza);
                    // 5) El color propio del gas: polvo azul en los bordes, oxigeno verde azulado y el
                    // hidrogeno rosado en el corazon. La tarjeta lo sigue tiñendo con la cancion.
                    pixels[y * w + x] = unchecked((int)((uint)alpha << 24 | ColorGasNatural(e, filament)));
                }
            }
            if (cancel != null && cancel()) return null;
            return NaturalBitmap(w, h, pixels);
        }

        internal static Bitmap BakeViaNatural(int w, int h)
        { return BakeViaPreparada(w, h, null, false); }

        internal static Bitmap BakeViaPreparada(int w, int h, Func<bool> cancel, bool slow)
        {
            int[] pixels = NaturalPixels(w, h);
            long next = Stopwatch.GetTimestamp() + CargaTrabajo.LimiteTicks(2);
            for (int y = 0; y < h; y++)
            {
                if (PausaNaturalFila(y, cancel, slow, ref next)) return null;
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)Math.Max(1, w - 1), v = y / (float)Math.Max(1, h - 1);
                    float warp = CloudNoise(u * 4.1f + 31f, v * 5.8f + 12f) - .5f;
                    float center = .82f - .74f * u + warp * .018f;
                    float width = .085f + .014f * (float)Math.Sin(u * 5.2f);
                    float distance = (v - center) / width;
                    float envelope = (float)Math.Exp(-distance * distance * 1.75f);
                    float cloud = CloudNoise(u * 17f + 5f, v * 25f + 41f);
                    float grain = CloudNoise(u * 72f + 61f, v * 98f + 13f);
                    float lane = CloudNoise(u * 28f + warp * 7f, v * 39f + warp * 9f + 33f);
                    // Canales de polvo: se recorta la emision de la banda. No se anade una
                    // pasada negra ni un buffer extra para simular nubes que la tapen.
                    float gap = Math.Max(0f, (lane - .24f) / .76f);
                    gap *= .65f + .35f * gap;
                    // 9) El nucleo galactico: mas ancho, mas luz y calido; los brazos, blanco azulado.
                    float nd = (u - .42f) / .16f, core = (float)Math.Exp(-nd * nd);
                    float wide = (float)Math.Exp(-distance * distance * 1.75f / (1f + 1.2f * core));
                    envelope += (wide - envelope) * core;
                    gap += (1f - gap) * .38f * core;   // el centro brilla mas seguido: los canales no lo trocean
                    // 10) La gran grieta: polvo oscuro a lo largo de la banda, partiendola en dos.
                    float rd = (v - center + width * .12f) / (width * .26f);
                    float tramo = Smooth01((u - .16f) / .10f) * (1f - Smooth01((u - .56f) / .12f));
                    float rift = (float)Math.Exp(-rd * rd) * tramo * (.55f + .45f * CloudNoise(u * 9f + 71f, v * 7f + 3f));
                    // 11) Grano de estrellas: nubes de estrellas sin resolver, dentro del mismo mapa.
                    float stars = CloudNoise(u * 150f + 17f, v * 205f + 91f);
                    stars *= stars * stars;
                    float value = envelope * gap * (.26f + .74f * cloud) * (.65f + .35f * grain);
                    value *= (1f + .55f * core) * (1f - .70f * rift) * (.90f + .30f * stars);
                    float sky = 1f - v;
                    int alpha = (int)Math.Min(48f, value * 150f * (.22f + .78f * sky * sky));
                    pixels[y * w + x] = unchecked((int)((uint)alpha << 24 | ColorViaNatural(core, cloud)));
                }
            }
            if (cancel != null && cancel()) return null;
            return NaturalBitmap(w, h, pixels);
        }

        // Bok: diez globulos pequenos y redondos dentro de las tres regiones. Fijos (deterministas).
        private static readonly float[] GlobX = { .21f, .15f, .25f, .50f, .57f, .47f, .80f, .86f, .78f, .60f };
        private static readonly float[] GlobY = { .27f, .20f, .18f, .43f, .37f, .36f, .24f, .18f, .15f, .46f };
        private static readonly float[] GlobR = { .018f, .012f, .010f, .020f, .013f, .009f, .016f, .011f, .009f, .011f };
        internal static float GlobulosNatural(float x, float y)
        {
            float g = 0f;
            for (int i = 0; i < GlobX.Length; i++)
            {
                float dx = (x - GlobX[i]) / GlobR[i], dy = (y - GlobY[i]) / (GlobR[i] * 1.3f);
                float d2 = dx * dx + dy * dy;
                if (d2 < 9f) g = Math.Max(g, (float)Math.Exp(-d2 * .9f));
            }
            return g;
        }
        internal static uint ColorGasNatural(float e, float filament)
        {
            if (!(e > 0f)) e = 0f; else if (e > 1f) e = 1f;
            if (!(filament > 0f)) filament = 0f; else if (filament > 1f) filament = 1f;
            float t = Smooth01((e - .12f) / .45f);
            float core = Smooth01((e - .55f) / .40f) * (.65f + .35f * filament);
            float r = 120f + (95f - 120f) * t, g = 160f + (222f - 160f) * t, b = 255f + (212f - 255f) * t;
            r += (255f - r) * core; g += (118f - g) * core; b += (172f - b) * core;
            return RgbNormalizado(r, g, b);
        }
        internal static uint ColorViaNatural(float core, float cloud)
        {
            if (!(core > 0f)) core = 0f; else if (core > 1f) core = 1f;
            if (!(cloud > 0f)) cloud = 0f; else if (cloud > 1f) cloud = 1f;
            float k = Smooth01(core * 1.2f) * (.75f + .25f * cloud);
            float r = 204f + (255f - 204f) * k, g = 218f + (212f - 218f) * k, b = 255f + (168f - 255f) * k;
            return RgbNormalizado(r, g, b);
        }
        // El canal mas alto a 255: la textura lleva el TONO; la luz la siguen llevando alfa y tinte.
        private static uint RgbNormalizado(float r, float g, float b)
        {
            float m = Math.Max(r, Math.Max(g, b)); if (!(m > 1f)) return 0x00FFFFFFu;
            float k = 255f / m;
            uint ri = (uint)Math.Min(255f, r * k + .5f), gi = (uint)Math.Min(255f, g * k + .5f), bi = (uint)Math.Min(255f, b * k + .5f);
            return (ri << 16) | (gi << 8) | bi;
        }
        private static float Smooth01(float x)
        {
            if (!(x > 0f)) return 0f;
            if (x >= 1f) return 1f;
            return x * x * (3f - 2f * x);
        }

        private static bool PausaNaturalFila(int y, Func<bool> cancel, bool slow, ref long next)
        {
            if ((y & 3) != 0) return false;
            if (cancel != null && cancel()) return true;
            if (slow && Stopwatch.GetTimestamp() >= next) {
                Thread.Sleep(1);
                next = Stopwatch.GetTimestamp() + CargaTrabajo.LimiteTicks(2);
            }
            return false;
        }

        internal static Bitmap BakeVinetaNatural(int n)
        {
            if (n < 2) throw new ArgumentOutOfRangeException("n");
            int[] pixels = NaturalPixels(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double dx = 2.0 * x / (n - 1) - 1.0, dy = 2.0 * y / (n - 1) - 1.0;
                    // 32) La caida de luz de un objetivo de verdad (ley del coseno a la cuarta), no un
                    // escalon suave: oscurece poco a poco desde el centro y no dibuja ningun anillo. La
                    // esquina sigue en el mismo tope de antes y el centro queda limpio del todo.
                    double r2 = dx * dx + dy * dy, k2 = .0976;
                    double caida = 1.0 - 1.0 / ((1.0 + k2 * r2) * (1.0 + k2 * r2));
                    double esquina = 1.0 - 1.0 / ((1.0 + k2 * 2.0) * (1.0 + k2 * 2.0));
                    double f = Math.Pow(Math.Min(1.0, caida / esquina), 1.4) * 76.0;
                    f = Math.Max(0.0, (f - 2.0) * 76.0 / 74.0);
                    pixels[y * n + x] = unchecked((int)((uint)(f + .5) << 24));
                }
            return NaturalBitmap(n, n, pixels);
        }

        private static float NaturalNebDetail(float detail)
        {
            if (float.IsNaN(detail) || float.IsInfinity(detail)) return Form1.NebNaturalDef;
            return Math.Max(0f, Math.Min(1f, detail));
        }

        private static float NaturalCloudRegion(float x, float y, float cx, float cy, float sx, float sy)
        {
            float dx = (x - cx) / sx, dy = (y - cy) / sy;
            return (float)Math.Exp(-(dx * dx + dy * dy) * 1.75f);
        }

        private static int[] NaturalPixels(int w, int h)
        {
            if (w < 1) throw new ArgumentOutOfRangeException("w");
            if (h < 1) throw new ArgumentOutOfRangeException("h");
            return new int[checked(w * h)];
        }

        private static Bitmap NaturalBitmap(int w, int h, int[] pixels)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            try
            {
                BitmapData data = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < h; y++)
                        Marshal.Copy(pixels, y * w, new IntPtr(data.Scan0.ToInt64() + y * (long)data.Stride), w);
                }
                finally { b.UnlockBits(data); }
                return b;
            }
            catch { b.Dispose(); throw; }
        }
    }
}
#endif

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
                    // El detalle suma filamentos sobre una nube base ligera; a cero la base
                    // sigue siendo usable, pero las vetas desaparecen. NebFuerza sigue siendo
                    // la referencia comun del proyecto, sin pasar su control al renderer CPU.
                    int alpha = (int)Math.Min(70f, value * 60f * Form1.NebFuerza);
                    pixels[y * w + x] = unchecked((int)((uint)alpha << 24 | 0x00FFFFFFu));
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
                    float value = envelope * gap * (.26f + .74f * cloud) * (.65f + .35f * grain);
                    float sky = 1f - v;
                    int alpha = (int)Math.Min(48f, value * 150f * (.22f + .78f * sky * sky));
                    pixels[y * w + x] = unchecked((int)((uint)alpha << 24 | 0x00FFFFFFu));
                }
            }
            if (cancel != null && cancel()) return null;
            return NaturalBitmap(w, h, pixels);
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
                    // Mas espacio limpio alrededor del centro; el borde aparece lentamente
                    // y la esquina no hace un marco oscuro tan pesado como el estilo legacy.
                    double f = Math.Max(0.0, Math.Min(1.0, (dx * dx + dy * dy - .42) / 1.58));
                    f = f * f * (3.0 - 2.0 * f);
                    pixels[y * n + x] = unchecked((int)((uint)(76.0 * f + .5) << 24));
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

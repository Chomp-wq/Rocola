#if ROCOLA_GPU
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Rocola
{
    // LA LUNA DE ESTA NOCHE (idea 33 de la presentacion del 11-10-2026). Solo Espacio natural y solo GPU;
    // con su propio interruptor en Ctrl+F3 -> Fondo ("Luna con su fase real").
    //
    // Es la luna de verdad del dia de hoy: la fase sale de la fecha, la parte en sombra conserva la luz
    // tenue que le devuelve la Tierra, aclara un poco el cielo de alrededor y tapa y apaga las estrellas que
    // tiene al lado. Sus mares y crateres son el mapa que ya existia (MapaLunar, LunaGpu.cs), a 128 px.
    //
    // Coste: una textura de 128x128 que se hornea cuando cambia la fase (cada siete horas y pico), un halo y
    // un cuadrilatero por cuadro. Va detras de la aurora y de las estrellas, delante de las nebulosas.
    // La estrella protagonista (que en el codigo tambien se llama "luna") no cambia.
    internal sealed partial class GpuPanel
    {
        public bool LunaReal = true;
        internal const int LunaRealLado = 128;
        internal const float LunaRealX = .795f, LunaRealY = .30f, LunaRealRadio = .030f;
        private const int LunaRealPasos = 96;              // fases distintas en un ciclo: una cada 7,4 horas
        private IntPtr lunaRealTex; private int lunaRealTW, lunaRealTH; private float lunaRealU = 1f, lunaRealV = 1f;
        private int lunaRealPaso = -1, lunaRealRetryMs;
        private float lunaRealIlum;
        private readonly D3D9.Vtx[] lunaRealQuad = new D3D9.Vtx[6];
        private static float[] lunaRelieve, lunaAlbedo;

        // Fase de 0 (nueva) a 1, por la fecha. Luna nueva de referencia: 6-1-2000 18:14 UTC.
        internal static double FaseLunar(DateTime utc)
        {
            double dias = (utc - new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc)).TotalDays;
            double f = dias / 29.530588853; f -= Math.Floor(f);
            return f;
        }
        // Lo iluminado (0..1) visto desde la Tierra.
        internal static float IluminadaLunar(double fase)
        { return (float)((1 - Math.Cos(2 * Math.PI * fase)) * .5); }

        private void EnsureLunaReal()
        {
            if (!FondoNatural || !LunaReal || Height < 64)
            {
                stars.LunaRadio = 0f; stars.LunaLuz = 0f;
                if (lunaRealTex != IntPtr.Zero && !FondoNatural) { D3D9.Release(lunaRealTex); lunaRealTex = IntPtr.Zero; lunaRealPaso = -1; }
                return;
            }
            double fase = FaseLunar(DateTime.UtcNow);
            lunaRealIlum = IluminadaLunar(fase);
            int paso = (int)(fase * LunaRealPasos) % LunaRealPasos;
            if (lunaRealTex == IntPtr.Zero || paso != lunaRealPaso)
            {
                int now = Environment.TickCount;
                if (lunaRealTex == IntPtr.Zero && lunaRealRetryMs != 0 && unchecked(now - lunaRealRetryMs) < 1000) { stars.LunaRadio = 0f; return; }
                using (Bitmap b = BakeLunaReal(LunaRealLado, (paso + .5) / LunaRealPasos))
                    lunaRealTex = Upload(lunaRealTex, ref lunaRealTW, ref lunaRealTH, b, out lunaRealU, out lunaRealV);
                if (lunaRealTex != IntPtr.Zero) { lunaRealPaso = paso; lunaRealRetryMs = 0; } else lunaRealRetryMs = now;
            }
            // Cerca de la luna nueva no se ve (esta junto al sol): ni disco ni halo.
            bool visible = lunaRealTex != IntPtr.Zero && lunaRealIlum > .03f;
            float r = Height * LunaRealRadio;
            float cx, cy; LunaRealCentro(out cx, out cy);
            stars.LunaCx = cx; stars.LunaCy = cy;
            stars.LunaRadio = visible ? r : 0f;
            stars.LunaLuz = visible ? lunaRealIlum : 0f;
        }
        // Al apagar la tarjeta o recrear el dispositivo (StopGpu): la textura es del dispositivo viejo y no puede
        // seguir apuntada, ni retenerlo. Al volver, EnsureLunaReal la hornea de nuevo.
        private void SueltaLunaReal()
        {
            if (lunaRealTex != IntPtr.Zero) { D3D9.Release(lunaRealTex); lunaRealTex = IntPtr.Zero; }
            lunaRealTW = lunaRealTH = 0; lunaRealPaso = -1; lunaRealRetryMs = 0;
            if (stars != null) { stars.LunaRadio = 0f; stars.LunaLuz = 0f; }
        }
        private void LunaRealCentro(out float cx, out float cy)
        {
            // Acompana a la via lactea en su paseo lento, la mitad de lejos: no salta ni se sale.
            cx = Width * LunaRealX + (float)Math.Sin(HazFase) * Width * .012f;
            cy = Height * LunaRealY + (float)Math.Cos(HazFase) * Height * .008f - IdleRise * .12f;
        }
        private void DibujaLunaReal()
        {
            if (!FondoNatural || !LunaReal || lunaRealTex == IntPtr.Zero || !(stars.LunaRadio > 0f)) return;
            float r = stars.LunaRadio, cx = stars.LunaCx, cy = stars.LunaCy;
            // El resplandor alrededor, por lo iluminada que este.
            stars.GlowBatchBegin(2);
            stars.GlowBatchAdd(cx, cy, r * 7f, ((uint)(30f * lunaRealIlum * SceneDim + .5f) << 24) | 0xC8D7FFu);
            stars.GlowBatchAdd(cx, cy, r * 2.4f, ((uint)(46f * lunaRealIlum * SceneDim + .5f) << 24) | 0xE6EEFFu);
            stars.GlowBatchEnd();
            float luz = .72f + .28f * SceneDim;
            uint k = (uint)(255f * luz + .5f); if (k > 255) k = 255;
            SetTexQuadA(lunaRealQuad, cx - r, cy - r, cx + r, cy + r, lunaRealU, lunaRealV, 0xFF000000u | (k << 16) | (k << 8) | k);
            stars.DrawQuad(lunaRealTex, lunaRealQuad);
        }

        // Los mares grandes de la cara que vemos (Imbrio, Serenidad, Tranquilidad, Crisis, Procelas, Nubes y
        // Fecundidad), aproximados: es lo que hace que se reconozca la luna a simple vista.
        private static readonly double[] MarX = { -.30, .20, .33, .62, -.55, -.12, .55 };
        private static readonly double[] MarY = { -.35, -.38, -.10, -.30, .00, .38, .12 };
        private static readonly double[] MarR = { .28, .17, .20, .10, .32, .18, .14 };
        private static double Mares(double x, double y)
        {
            double m = 0;
            for (int i = 0; i < MarX.Length; i++)
            {
                double dx = (x - MarX[i]) / MarR[i], dy = (y - MarY[i]) / MarR[i];
                m += Math.Exp(-(dx * dx + dy * dy) * 1.5);
            }
            return Math.Min(1, m);
        }
        // La luna con la fase pedida: el sol la alumbra de lado segun la fase (creciente por la derecha,
        // menguante por la izquierda), con el relieve de los crateres y la luz de la Tierra en la sombra.
        // El disco es opaco: tapa las estrellas que tiene detras, como la de verdad.
        internal static Bitmap BakeLunaReal(int n, double fase)
        {
            if (n < 16 || n > 512) throw new ArgumentOutOfRangeException("n");
            if (double.IsNaN(fase) || double.IsInfinity(fase)) fase = .5;
            fase -= Math.Floor(fase);
            float[] relief, albedo;
            if (n == LunaRealLado)
            {
                if (lunaRelieve == null) { MapaLunar(n, out lunaRelieve, out lunaAlbedo); }
                relief = lunaRelieve; albedo = lunaAlbedo;
            }
            else MapaLunar(n, out relief, out albedo);
            double ang = 2 * Math.PI * fase;
            double sx = Math.Sin(ang), sz = -Math.Cos(ang), sy = -.08;   // sol: detras en la nueva, delante en la llena
            double sl = Math.Sqrt(sx * sx + sy * sy + sz * sz); sx /= sl; sy /= sl; sz /= sl;
            double tierra = .022 + .040 * (1 + Math.Cos(ang)) * .5;     // la luz cenicienta, mas cerca de la nueva
            double c0 = (n - 1) * .5, rad = n * 124.0 / 256.0, g = n / 7.0;
            int[] pixels = new int[n * n];
            for (int y = 1; y < n - 1; y++)
                for (int x = 1; x < n - 1; x++)
                {
                    double nx = (x - c0) / rad, ny = (y - c0) / rad, d = nx * nx + ny * ny;
                    if (d >= 1) continue;
                    int i = y * n + x;
                    double z = Math.Sqrt(1 - d);
                    double dx = (relief[i + 1] - relief[i - 1]) * g, dy = (relief[i + n] - relief[i - n]) * g;
                    double qx = nx - dx * z, qy = -(ny - dy * z), len = Math.Sqrt(qx * qx + qy * qy + z * z);
                    double sol = (qx * sx + qy * sy + z * sz) / len;
                    double luz = sol > 0 ? Math.Pow(sol, .72) : 0;
                    // Terminador suave (la luz rasante no corta de golpe).
                    double borde = Math.Max(0, Math.Min(1, (nx * sx + -ny * sy + z * sz + .06) / .12));
                    luz *= borde;
                    double a = albedo[i] * (1 - .36 * Mares(nx, ny));   // los mares oscuros de la cara visible
                    double lit = Math.Min(1, a * (.06 * borde + 1.02 * luz));
                    double ceniza = a * tierra * (1 - borde);
                    int r = (int)(255 * Math.Min(1, lit + ceniza * .80));
                    int gg = (int)(255 * Math.Min(1, lit * .985 + ceniza * .90));
                    int b = (int)(255 * Math.Min(1, lit * .95 + ceniza * 1.15 + .012));
                    int al = (int)(255 * Math.Min(1, (1 - Math.Sqrt(d)) * rad));
                    pixels[i] = (al << 24) | (r << 16) | (gg << 8) | b;
                }
            Bitmap bitmap = new Bitmap(n, n, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, n, n), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < n; y++) Marshal.Copy(pixels, y * n, new IntPtr(data.Scan0.ToInt64() + y * (long)data.Stride), n); }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }
    }
}
#endif

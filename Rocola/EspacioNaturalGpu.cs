#if ROCOLA_GPU
using System;
using System.Drawing;

namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        // Alternativa visual; no cambia el motor, el audio ni el presupuesto de dibujos.
        // Legacy sigue siendo el valor inicial, incluso con configuraciones antiguas.
        public bool FondoNatural = false;
        private bool nebEstiloDe, hazEstiloDe, vinetaEstiloDe;
        private readonly Bitmap[] mapasNebEstilo = new Bitmap[2], mapasViaEstilo = new Bitmap[2];
        private readonly int[] mapaNebAncho = new int[2], mapaNebAlto = new int[2];
        private readonly bool[] mapaNebDetalle = new bool[2];
        private readonly float[] mapaNebFuerza = new float[2];
        private readonly int[] mapaViaAncho = new int[2], mapaViaAlto = new int[2];

        // Solo dos mapas por familia, al cuarto de resolución; una sola textura GPU
        // por familia. Cambiar y volver no repite el ruido procedural. Los reintentos
        // de dispositivo reutilizan también los mapas, sin volver a hornearlos.
        private Bitmap MapaNebEstilo(int width, int height)
        {
            int slot = FondoNatural ? 1 : 0;
            Bitmap map = mapasNebEstilo[slot];
            if (map == null || mapaNebAncho[slot] != width || mapaNebAlto[slot] != height ||
                mapaNebDetalle[slot] != CieloProfundo || mapaNebFuerza[slot] != Form1.nebNaturalFuerza) {
                Bitmap replacement = FondoNatural ? BakeNebulosaNatural(width, height, CieloProfundo) :
                    CieloProfundo ? BakeNatural(width, height) : GpuStarfield.BakeNebulosas(width, height);
                if (map != null) map.Dispose();
                mapasNebEstilo[slot] = replacement;
                mapaNebAncho[slot] = width; mapaNebAlto[slot] = height;
                mapaNebDetalle[slot] = CieloProfundo; mapaNebFuerza[slot] = Form1.nebNaturalFuerza;
                map = replacement;
            }
            return map;
        }
        private Bitmap MapaViaEstilo(int width, int height)
        {
            int slot = FondoNatural ? 1 : 0;
            Bitmap map = mapasViaEstilo[slot];
            if (map == null || mapaViaAncho[slot] != width || mapaViaAlto[slot] != height) {
                Bitmap replacement = FondoNatural ? BakeViaNatural(width, height) : GpuStarfield.BakeViaLactea(width, height);
                if (map != null) map.Dispose();
                mapasViaEstilo[slot] = replacement;
                mapaViaAncho[slot] = width; mapaViaAlto[slot] = height; map = replacement;
            }
            return map;
        }
        private void LiberaMapasEstilo()
        {
            for (int i = 0; i < 2; i++) {
                if (mapasNebEstilo[i] != null) { mapasNebEstilo[i].Dispose(); mapasNebEstilo[i] = null; }
                if (mapasViaEstilo[i] != null) { mapasViaEstilo[i].Dispose(); mapasViaEstilo[i] = null; }
            }
        }

        internal static uint CieloEstilo(uint original, int layer, bool natural)
        {
            if (!natural) return original;
            // Perfil nocturno, no la paleta de portada: ésta permanece en la aurora.
            uint night = layer == 0 ? 0x00030610u : layer == 1 ? 0x000B1936u : 0x0010264Bu;
            int r = (int)((original >> 16) & 255), g = (int)((original >> 8) & 255), b = (int)(original & 255);
            r = (r + (int)((night >> 16) & 255) * 3) / 4;
            g = (g + (int)((night >> 8) & 255) * 3) / 4;
            b = (b + (int)(night & 255) * 3) / 4;
            r = Math.Min(r, b / 2); g = Math.Min(g, b * 3 / 4);
            return (original & 0xFF000000u) | (uint)(r << 16 | g << 8 | b);
        }
        internal static float RayoNatural(float legacy)
        {
            // Reutiliza las ondas existentes: más contraste entre vetas, sin más
            // senoides, vértices, partículas o asignaciones por fotograma.
            float ray = 1f + (legacy - 1f) * 2.1f;
            if (!(ray > .28f)) return .28f;
            return Math.Min(1.72f, ray);
        }
        internal static float BajaNatural(float f)
        {
            if (!(f > 0)) return 1;
            if (f >= 1) return 0;
            float f2 = f * f;
            return (1 - f2) * (1 - f2 * f2);
        }
        internal static int AurMallaNatural(D3D9.Vtx[] vertices, float[] heights, uint[] curtain, uint[] ground,
                                           float[] alpha, float[] rays, float width, float baseY, float height)
        { return AurMallaEstilo(vertices, heights, curtain, ground, alpha, rays, width, baseY, height, true); }
        internal static int AurBrilloNatural(D3D9.Vtx[] vertices, float[] heights, uint[] neon, float[] intensity,
                                            uint[] horizon, float[] horizonIntensity, float width, float baseY)
        { return AurBrilloEstilo(vertices, heights, neon, intensity, horizon, horizonIntensity, width, baseY, true); }
    }
}
#endif

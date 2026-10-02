#if ROCOLA_GPU
using System;

namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        // Las seis franjas existentes concentran la luz en la parte baja y abren
        // el velo arriba; no se añade ninguna malla, textura ni llamada al driver.
        private static readonly float[] aurNaturalNiveles = { 0f, .11f, .27f, .48f, .70f, .87f, 1f };
        private static readonly float[] aurNaturalLuz = CreaLuzAuroraNatural();

        private static float[] CreaLuzAuroraNatural()
        {
            float[] values = new float[256];
            for (int i = 0; i < values.Length; i++)
                values[i] = (float)Math.Sin(i * (Math.PI * 2.0 / values.Length));
            return values;
        }
        internal static float AuroraNaturalNivel(int row)
        {
            if (row <= 0) return 0f;
            if (row >= 6) return 1f;
            return aurNaturalNiveles[row];
        }
        private static float AuroraNaturalOnda(float phase)
        {
            // Interpolación continua; el reloj es el mismo que las ondas a
            // 24/50/60 FPS. No hay senoides por vértice ni saltos de tabla.
            float position = phase * 256f;
            int index = (int)Math.Floor(position);
            float fraction = position - index;
            index &= 255;
            return aurNaturalLuz[index] +
                (aurNaturalLuz[(index + 1) & 255] - aurNaturalLuz[index]) * fraction;
        }
        internal static uint AuroraNaturalVelo(uint rgb, int row, float x, float time, float ray)
        {
            // El alfa del color guarda una máscara interna de luz, no el alfa
            // final del vértice. En el horizonte es siempre 1: suelo, cortina y
            // barra de estado continúan siendo exactamente una sola pieza.
            if (row <= 0 || row >= 6) return 0xFF000000u | (rgb & 0xFFFFFFu);
            if (!(x >= 0f)) x = 0f; else if (x > 1f) x = 1f;
            if (!(time >= 0f)) time = 0f; else if (time > 628.31853f) time = 628.31853f;
            if (!(ray > .28f)) ray = .28f; else if (ray > 1.72f) ray = 1.72f;
            float height = aurNaturalNiveles[row];
            // Las vetas se curvan y atraviesan franjas a ritmos diferentes; la
            // silueta musical no se deforma ni adquiere dientes de sierra.
            float first = AuroraNaturalOnda(x * 13.4f - time * .036f + height * .80f + (ray - 1f) * .13f);
            float second = AuroraNaturalOnda(x * 7.1f + time * .024f - height * .33f);
            float ramp = height * (2f - height);
            float light = 1f + (-.28f + .22f * first + .06f * second) * ramp;
            int alpha = (int)(light * 255f + .5f);
            if (alpha < 1) alpha = 1; else if (alpha > 255) alpha = 255;
            return (uint)(alpha << 24) | (rgb & 0xFFFFFFu);
        }
        private static float AuroraNaturalPeso(uint color)
        {
            uint alpha = color >> 24;
            // Los llamadores de geometría que suministran solamente RGB siguen
            // funcionando: cero es "sin máscara", no un velo invisible.
            return alpha == 0 ? 1f : alpha * (1f / 255f);
        }
    }
}
#endif

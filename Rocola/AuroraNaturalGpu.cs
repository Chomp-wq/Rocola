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
        // 20) LOS COLORES DE UNA AURORA DE VERDAD (Ctrl+F3 -> Fondo, "Aurora con colores reales"). Abajo un
        // filo rosado (nitrogeno), el cuerpo verde del oxigeno y arriba rojo y violeta. La intensidad la sigue
        // poniendo la musica (TonoPorNivel), la forma tambien; solo cambia de donde sale el color.
        private static readonly uint[] AurRealFila = { 0xF078B4u, 0x5AF596u, 0x50EB8Cu, 0x5ADC9Cu, 0xC85A8Cu, 0xA64ABEu, 0x823CC8u };
        public bool AuroraReal = true;
        internal static uint AuroraRealBase(int fila, float x)
        {
            if (fila < 0) fila = 0; else if (fila > 6) fila = 6;
            uint c = AurRealFila[fila];
            // Un poco de variacion a lo ancho: el verde no es el mismo de punta a punta.
            float w = .5f + .5f * AuroraNaturalOnda(x * .9f + fila * .07f);
            uint r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            g = (uint)(g * (.92f + .08f * w)); b = (uint)(b * (.86f + .14f * (1f - w)));
            return (r << 16) | (g << 8) | b;
        }
        internal static uint AuroraRealPie(float x)
        {
            float w = .5f + .5f * AuroraNaturalOnda(x * .9f);
            return (uint)(54 + 12 * w) << 16 | (uint)(196 + 20 * w) << 8 | (uint)(132 - 10 * w);
        }
        // 21) Donde la silueta se inclina, la cortina se ve de canto y junta mas luz; y por dentro se curva
        // a lo ancho. Las dos cosas son de la COLUMNA (no de la fila): se calculan una vez por columna en
        // estos arreglos y la malla solo los lee. Solo los usa el hilo de pantalla (y las pruebas).
        private static readonly float[] aurPliegue = new float[AurCols + 1], aurCurva = new float[AurCols + 1],
                                        aurReflejoAlto = new float[AurCols + 1];
        internal static void PreparaPliegues(float[] h, float paso, float panel)
        {
            int n = AurCols;
            float inv = 1f / Math.Max(1f, panel * .25f);
            for (int i = 0; i <= n; i++)
            {
                int a = i > 0 ? i - 1 : 0, b = i < n ? i + 1 : n;
                float pend = Math.Abs(h[b] - h[a]) / Math.Max(1f, paso * (b - a));
                aurPliegue[i] = .82f + .42f * Math.Min(1f, pend * 1.6f);
                aurCurva[i] = paso * 1.8f * AuroraNaturalOnda(i * .033f + h[i] * .0021f);
                float hn = h[i] * inv; aurReflejoAlto[i] = !(hn > 0f) ? 0f : hn > 1f ? 1f : hn;
            }
        }
        // 'arco' es sin(pi*f) de la fila (cero en el pie y en la cresta), calculado una vez por fila.
        internal static float ArcoAurora(float f)
        { return !(f > 0f) || f >= 1f ? 0f : (float)Math.Sin(Math.PI * f); }
        // 22) Sin musica, parches que se encienden y apagan muy despacio, sin perder la forma.
        internal static float PulsoReposoAurora(float x, float t, float calma)
        {
            if (!(calma > 0f)) return 1f;
            if (calma > 1f) calma = 1f;
            float p = AuroraNaturalOnda(x * 1.5f + t * .049f) * AuroraNaturalOnda(t * .027f + x * .53f + .25f);
            return 1f + .38f * calma * p;
        }
        // 23) El reflejo de la cortina en el suelo: justo bajo el horizonte se ve un eco tenue de su color,
        // mas cuanto mas alta es la cortina en esa columna, y se apaga antes de llegar al texto de la franja.
        internal static float ReflejoFila(float s)
        {
            float k = 1f - s; if (!(k > 0f)) k = 0f; else if (k > 1f) k = 1f;
            float junto = s * AurSuelo; if (junto > 1f) junto = 1f;   // en el horizonte mismo, ninguno: una sola pieza
            return .20f * k * k * k * junto;
        }
        internal static uint SueloReflejo(uint suelo, uint eco, float alfa, float m, float refl, float s)
        {
            if (!(refl > 0f)) return AurArgb(suelo, alfa * m);
            float mezcla = Math.Min(.7f, refl * 5f);
            int sr = (int)((suelo >> 16) & 255), sg = (int)((suelo >> 8) & 255), sb = (int)(suelo & 255);
            uint r = (uint)(sr + ((int)((eco >> 16) & 255) - sr) * mezcla);
            uint g = (uint)(sg + ((int)((eco >> 8) & 255) - sg) * mezcla);
            uint b = (uint)(sb + ((int)(eco & 255) - sb) * mezcla);
            return AurArgb((r << 16) | (g << 8) | b, alfa * Math.Max(m, refl));
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

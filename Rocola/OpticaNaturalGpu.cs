#if ROCOLA_GPU
using System;

namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        // La luz se queda alrededor de la portada en vez de ocupar un circulo
        // del alto de una portada vertical. Reutiliza la misma textura lisa y
        // los mismos 18 vertices: sin una segunda capa ni horneados por cuadro.
        internal static void HaloPortadaNatural(float w, float h, uint original,
                                                out float rx, out float ry, out uint argb)
        {
            rx = w * .76f; ry = h * .76f;
            uint a = (original >> 24) * 4 / 5;
            uint r = (original >> 16) & 255, g = (original >> 8) & 255, b = original & 255;
            // Una pequena luz blanca dentro del acento evita el aspecto de tubo
            // de neon puro. Sigue siendo aditiva, nunca una caja opaca.
            r = (r * 9 + 214 + 5) / 10;
            g = (g * 9 + 224 + 5) / 10;
            b = (b * 9 + 240 + 5) / 10;
            argb = (a << 24) | (r << 16) | (g << 8) | b;
        }

        internal static uint TinteNebulosaEstilo(uint original, bool segunda, bool cine, bool natural)
        {
            if (!natural) return TinteNebulosa(original, segunda, cine);
            // El cielo natural es nocturno incluso con el acabado cine apagado:
            // el acento completo de portada/genero pertenece a la aurora, no a
            // toda la boveda. Cine aun cambia su tratamiento, sin tocar el ajuste.
            uint night = cine ? (segunda ? 0x00645594u : 0x00315F94u) :
                               (segunda ? 0x0047547Cu : 0x00315480u);
            uint accent = cine ? 26u : 14u;
            uint r = (((original >> 16) & 255) * accent + ((night >> 16) & 255) * (100 - accent) + 50) / 100;
            uint g = (((original >> 8) & 255) * accent + ((night >> 8) & 255) * (100 - accent) + 50) / 100;
            uint b = ((original & 255) * accent + (night & 255) * (100 - accent) + 50) / 100;
            // Un rojo/blanco extremo no puede convertir el cielo en una mancha
            // rosa. El azul conserva el protagonismo, pero no borra la variacion.
            r = Math.Min(r, b * 3 / 4); g = Math.Min(g, b * 9 / 10);
            return (r << 16) | (g << 8) | b;
        }
    }
}
#endif

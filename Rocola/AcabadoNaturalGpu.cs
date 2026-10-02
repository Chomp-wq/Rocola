#if ROCOLA_GPU
using System;

namespace Rocola
{
    internal sealed partial class GpuPanel
    {
        private float idleNaturalVelocity;
        private bool naturalLightReady;
        private float naturalDim;
        private float SceneDim { get { return FondoNatural && naturalLightReady ? naturalDim : Dim; } }

        // Un reloj para toda la escena: posicion del panel, horizonte, cubo,
        // estrella y ocultacion de controles leen el mismo idleBlend. La velocidad
        // se conserva al invertir el destino: tocar no provoca un salto de posicion.
        internal static float PasoReposoNatural(float value, bool target, ref float velocity, double dt)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = target ? 0 : 1;
            value = Math.Max(0, Math.Min(1, value));
            if (float.IsNaN(velocity) || float.IsInfinity(velocity)) velocity = 0;
            if (!(dt > 0)) return value;
            dt = Math.Min(.1, dt);
            float end = target ? 1f : 0f, rate = target ? 7f : 18f;
            float offset = value - end, term = velocity + rate * offset;
            float decay = (float)Math.Exp(-rate * dt);
            float next = end + (offset + term * (float)dt) * decay;
            velocity = (velocity - rate * term * (float)dt) * decay;
            if (next < 0 || next > 1 || (Math.Abs(next - end) < .001f && Math.Abs(velocity) < .01f)) {
                velocity = 0; return Math.Max(0, Math.Min(1, next < 0 || next > 1 ? next : end));
            }
            return next;
        }

        // Dim llega con el latido de la interfaz. Interpolar su luz
        // con el reloj GPU evita escalones a 144/180 Hz, sin interpolar ni frenar
        // entradas, audio, bandas, posiciones o la peticion de un nuevo panel.
        private void CapturaLuzNatural(double dt)
        {
            if (!FondoNatural) { naturalLightReady = false; return; }
            if (!naturalLightReady) {
                naturalDim = Dim; naturalLightReady = true; return;
            }
            float step = !(dt > 0) ? 0 : (float)(1 - Math.Exp(-Math.Min(.1, dt) * 24));
            naturalDim += (Dim - naturalDim) * step;
        }

        internal static void ProfundidadPolvoNatural(float depth, out float size, out float light)
        {
            if (float.IsNaN(depth)) depth = 0;
            float near = (Math.Max(-1, Math.Min(1, depth)) + 1) * .5f;
            size = .80f + .20f * near;
            light = .62f + .38f * near;
        }
    }

    internal sealed partial class GpuStarfield
    {
        internal static float RelevoNatural(float progress)
        {
            if (!(progress > 0)) return 0;
            if (progress >= 1) return 1;
            // La duracion y los dos campos son los existentes. Sin derivada
            // brusca en los extremos y sin un hueco oscuro durante el relevo.
            return progress * progress * (3 - 2 * progress);
        }
    }
}
#endif

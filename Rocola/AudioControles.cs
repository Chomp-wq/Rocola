using System;
namespace Rocola
{
    public partial class Form1
    {
        // Potencia media de los canales, NO potencia de su mezcla: evita cancelar
        // instrumentos abiertos/en contrafase. Estados independientes; cero reservas.
        private static void AccumulateEqFrame(float[] buffer, int offset, int channels,
            float[][] coefficients, float[] state, double[] sums)
        {
            double weight = 1.0 / channels;
            for (int c = 0; c < channels; c++)
            {
                float input = buffer[offset + c];
                for (int b = 0; b < 8; b++)
                {
                    float[] cf = coefficients[b]; if (cf == null) continue;
                    int q = c * 32 + b * 4;
                    float output = cf[0]*input + cf[1]*state[q] + cf[2]*state[q+1] - cf[3]*state[q+2] - cf[4]*state[q+3];
                    state[q+1]=state[q]; state[q]=input; state[q+3]=state[q+2]; state[q+2]=output;
                    sums[b] += (output * output) * weight;
                }
            }
        }
        private bool HasStereoEq(string path)
        {
            System.Collections.Generic.Dictionary<string, float[]> eqCache = lecturaPlan != null ? lecturaPlan.Eq : this.eqCache;
            float[] curve;
            return path != null && eqCache.TryGetValue(path, out curve) &&
                curve != null && curve.Length > EqFingerprint && curve[EqFingerprint] == 1f;
        }
        private bool NeedsStereoEqRefresh(string path)
        {
            System.Collections.Generic.Dictionary<string, float[]> eqCache = lecturaPlan != null ? lecturaPlan.Eq : this.eqCache;
            // Una duracion sin huella puede corresponder a un archivo demasiado corto.
            // No volver a decodificarlo para siempre: migrar solo huellas existentes.
            return eqCache.ContainsKey(path) && !HasStereoEq(path);
        }
        // Valores calculados al construir la etapa, nunca trigonometria ni reservas por muestra.
        internal static float ExciterPole(int rate)
        {
            if (rate <= 0 || rate == 44100) return .55f;
            return (float)(1 - Math.Pow(.45, 44100.0 / rate));
        }
        private partial class LevelTap
        {
            private struct AudioRamp
            {
                internal float Current, Target, Delta;
                internal int Remaining;
                internal void Init(float value) { Current=Target=value; Delta=0; Remaining=0; }
                internal void Aim(float value, int frames)
                {
                    if(value==Target)return;
                    Target=value;Remaining=frames;Delta=(Target-Current)/frames;
                }
                internal void Step()
                {
                    if(Remaining<=0)return;
                    Current+=Delta;
                    if(--Remaining==0)Current=Target;
                }
            }
            private readonly int audioChannels, rampFrames, eqFadeFrames;
            private readonly float exciterPole;
            private AudioRamp gainRamp, exciteRamp;
            private bool audioReady;
            private int audioChannel, eqFadeLeft;
            private float eqMix=1f;
            private PreparedEq currentEq, previousEq;

            private static bool SameEq(PreparedEq a, PreparedEq b)
            { return ReferenceEquals(a,b) || (a!=null && b!=null && ReferenceEquals(a.Curve,b.Curve)); }

            private void AdvanceAudioControls(float gain, float excite, PreparedEq requested)
            {
                gainRamp.Aim(gain,rampFrames);gainRamp.Step();
                if(exciteRamp.Current==0 && excite>0)Array.Clear(exLp,0,exLp.Length);
                exciteRamp.Aim(excite,rampFrames);exciteRamp.Step();
                // Solo dos cascadas como maximo. Un cambio durante la mezcla espera su fin;
                // se toma la ultima peticion, no se acumulan filtros ni estados.
                if(eqFadeLeft==0 && !SameEq(currentEq,requested))
                {
                    previousEq=currentEq;currentEq=requested;eqFadeLeft=eqFadeFrames;
                }
                if(eqFadeLeft>0)
                {
                    eqMix=(eqFadeFrames-eqFadeLeft+1f)/eqFadeFrames;
                    if(--eqFadeLeft==0){eqMix=1;previousEq=null;}
                }
            }
            private static float ApplyPreparedEq(PreparedEq eq, float input, int channel)
            {
                if(eq==null)return input;
                float[] state=eq.State;
                for(int b=0;b<eq.Active;b++)
                {
                    float[] cf=eq.Coefficients[b];int q=channel*32+b*4;
                    float output=cf[0]*input+cf[1]*state[q]+cf[2]*state[q+1]-cf[3]*state[q+2]-cf[4]*state[q+3];
                    state[q+1]=state[q];state[q]=input;state[q+3]=state[q+2];state[q+2]=output;
                    input=output;
                }
                return input;
            }
        }
    }
}

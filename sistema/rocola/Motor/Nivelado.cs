// EL NIVELADO, EL EQ Y EL REALCE (LevelTap), juntado de Rocola/Form1.Sonido.cs y Rocola/AudioControles.cs para el motor de Rocola OS.
using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // Toma el nivel de audio (RMS) del bloque recien leido SIN modificar el sonido: solo observa el
        // buffer que ya lleno el lector y lo reporta. Sirve para que las estrellas laten con la musica.
        // Como el colchon es de ~1s, el nivel va algo adelantado de lo que se oye (envolvente de energia).
        private partial class LevelTap : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly Form1 owner;
            internal readonly long Publicacion;   // la reproduccion para la que puede apuntar medidas y pulso
            internal MedidaCancion Medida;     // lo que aprende oyendola, si entra por relevo sin pausa (Form1.SinPausa.cs)
            public volatile float Gain = 1f;   // objetivos; la rampa se aplica DESPUES de medir
            public volatile float Master = 1f; // los picos los controla el limitador final
            // REALCE DE AGUDOS (exciter armonico, tipo DSEE): separa la banda alta con un paso-alto de un polo,
            // la satura suave (tanh: crea armonicos NUEVOS por encima, el "aire" que el MP3 recorta) y mezcla
            // una pizca de vuelta. Todo en el dominio del tiempo (nada de FFT). La saturacion se calcula al
            // doble de frecuencia para que sus armonicos no se plieguen (Realce.cs), incluso al activarlo
            // a media cancion; conserva el mismo retraso apagado, con historia barata sin ejecutar FIR.
            // 0 = apagado (la pasada ni se ejecuta si tampoco hay nivelado).
            public volatile float Excite = 0f;   // intensidad 0..1, suavizada al cambiar en vivo
            private float[] exLp;       // estado del filtro del exciter (uno por canal)
            // EQ AUTOMATICO: correccion en dB por banda (8) o null = apagado. Se aplica con biquads
            // PEAKING (RBJ) en cascada, SOLO en las bandas con correccion real (0 dB = identidad exacta,
            // el biquad ni se crea). ~9 operaciones por muestra por banda activa (tipico: 2-4 bandas).
            private sealed class PreparedEq
            {
                internal readonly float[] Curve, State;
                internal readonly float[][] Coefficients;
                internal readonly int Active;
                internal readonly int Mascara;   // las bandas que lleva (bit b = banda b): para heredar su memoria en un disco continuo
                internal PreparedEq(float[] curve, int rate, int channels)
                {
                    Curve = curve;
                    Coefficients = new float[8][];
                    int active = 0;
                    for (int b = 0; b < 8; b++)
                    {
                        float[] cf = Form1.EqPeak(Form1.EqCentros[b], rate, curve[b]);
                        if (cf != null) { Coefficients[active++] = cf; Mascara |= 1 << b; }
                    }
                    Active = active;
                    State = new float[Math.Max(1, channels) * 32];
                }
            }
            // Publicacion atomica: preparar trigonometria y memoria en el llamador (UI),
            // nunca en Read. Solo el lector de audio modifica State despues de publicarla.
            private volatile PreparedEq pendingEq;
            public float[] EqDb
            {
                get { PreparedEq p = pendingEq; return p != null ? p.Curve : null; }
                set
                {
                    PreparedEq p = pendingEq;
                    if (p != null && CurvaEqExacta(p.Curve, value)) return;
                    pendingEq = value == null ? null :
                        new PreparedEq(value, WaveFormat.SampleRate, WaveFormat.Channels);
                }
            }
            private static bool CurvaEqExacta(float[] a, float[] b)
            {
                if (ReferenceEquals(a, b)) return true;
                if (a == null || b == null || a.Length < 8 || b.Length < 8) return false;
                // Sin tolerancia: una curva distinta conserva su transicion normal.
                for (int i = 0; i < 8; i++) if (a[i] != b[i]) return false;
                return true;
            }
            public LevelTap(NAudio.Wave.ISampleProvider s, Form1 o)
            {
                src=s;owner=o;Publicacion=o==null?0:o.publicacionAudio;audioChannels=Math.Max(1,s.WaveFormat.Channels);
                canalesMusicales=FormatoAudio.Musicales(s.WaveFormat);
                rampFrames=Math.Max(1,(int)(s.WaveFormat.SampleRate*.020));
                eqFadeFrames=Math.Max(1,(int)(s.WaveFormat.SampleRate*.030));
                exciterPole=ExciterPole(s.WaveFormat.SampleRate);exLp=new float[audioChannels];
                PreparaRealce(); FijaModoRealce(0f);   // retraso estable, incluso si empieza apagado
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            private void Objetivos(out float g, out float ex)
            {
                g=Gain*Master; ex=Excite;
                if(float.IsNaN(g)||float.IsInfinity(g)||g<0)g=audioReady?gainRamp.Current:1f;
                if(float.IsNaN(ex)||float.IsInfinity(ex))ex=audioReady?exciteRamp.Current:0f;
                ex=ex>.001f?Math.Min(1f,ex):0f;
            }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (vaciando) return VaciaRealce(buffer, offset, count);
                int n = src.Read(buffer, offset, count);
                if (n <= 0 && modoRealce == 2 && audioReady && owner != null)
                {
                    // Se acabo la cancion: el realce 2x tiene retenidos sus 31 cuadros; salen enteros, no se cortan. Antes, lo que la siguiente
                    // del mismo disco necesita para seguir (Realce.cs).
                    GuardaFin();
                    vaciando = true; porVaciar = RealceSobremuestreado.Retraso * audioChannels;
                    return VaciaRealce(buffer, offset, count);
                }
                if (n > 0 && owner != null)
                {
                    // RED DE SEGURIDAD, y no es teorica: en una biblioteca con miles de archivos pobres o
                    // recortados, uno dañado puede decodificar a NaN o a infinito. Eso no seria "un ruido y
                    // ya": se queda PEGADO. Casi todo lo que viene despues arrastra un estado -el piso movil
                    // del detector de golpes, la ganancia del limitador, la sonoridad que se esta midiendo de
                    // la cancion, el promedio del EQ- y en cuanto entra un NaN esas cuentas ya no vuelven a
                    // valer: el limitador se quedaria mudo el resto de la noche. Aqui se corta de raiz, ANTES
                    // de medir nada. Es UNA comparacion por muestra, y de paso atrapa los valores absurdos
                    // (un archivo roto que devuelve 1e30) que reventarian el nivelado.
                    { int fin0 = offset + n; for (int i = offset; i < fin0; i++) { float x0 = buffer[i]; if (!(x0 >= -4f && x0 <= 4f)) buffer[i] = 0f; } }
                    owner.PublicaBloqueDe(this, buffer, offset, n, WaveFormat.SampleRate, WaveFormat.Channels);   // pulso visual; FuenteOriginal ya midio antes del DSP
                    float g, ex;
                    Objetivos(out g, out ex);
                    PreparedEq prepared=pendingEq;
                    if(!audioReady)
                    {
                        gainRamp.Init(g);exciteRamp.Init(ex);currentEq=prepared;audioReady=true;
                        FijaModoRealce(ex);
                        HeredaFin();   // disco continuo: el realce y el EQ siguen con los de la anterior (Realce.cs)
                    }
                    if (cebadoPorPasar > 0)
                    {   // y el cebado de delante pasa tal cual: el realce sigue justo donde acabo la musica de la anterior
                        int salta = Math.Min(n, cebadoPorPasar); cebadoPorPasar -= salta;
                        audioChannel = (audioChannel + salta) % audioChannels;
                        if (salta == n) return n;
                        int total = n; offset += salta; n -= salta;
                        if (modoRealce == 2) { ProcesaConRealce(buffer, offset, n, g, ex, prepared); return total; }
                    }
                    // Realce 2x (la cancion empezo con el realce encendido): siempre por la linea de retraso,
                    // tambien si luego se apaga (sale el seco intacto, retrasado lo mismo).
                    if(modoRealce==2){ProcesaConRealce(buffer,offset,n,g,ex,prepared);return n;}
                    bool identity=g==1f && gainRamp.Current==1f && gainRamp.Remaining==0 &&
                        ex==0 && exciteRamp.Current==0 && exciteRamp.Remaining==0 &&
                        eqFadeLeft==0 && SameEq(currentEq,prepared) && (currentEq==null||currentEq.Active==0);
                    if(identity) audioChannel=(audioChannel+n)%audioChannels;
                    else
                    {
                        int end=offset+n;
                        for(int i=offset;i<end;i++)
                        {
                            if(audioChannel==0)AdvanceAudioControls(g,ex,prepared);
                            float input=buffer[i]*gainRamp.Current;
                            float v=ApplyPreparedEq(currentEq,input,audioChannel);
                            if(eqMix<1f)
                            {
                                float old=ApplyPreparedEq(previousEq,input,audioChannel);
                                v=old+(v-old)*eqMix;
                            }
                            if(exciteRamp.Current>0)   // realce sin sobremuestrear: solo si se encendio a media cancion
                            {
                                float lp=(exLp[audioChannel]+=(v-exLp[audioChannel])*exciterPole);
                                float h=v-lp;
                                float mix=exciteRamp.Current*.5f;
                                v+=Form1.TanhRapida(h*2.5f)*.4f*mix;
                            }
                            buffer[i]=v; // limitador final conserva su lugar y anticipacion
                            if(++audioChannel>=audioChannels)audioChannel=0;
                        }
                    }
                }
                return n;
            }
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
            private readonly bool[] canalesMusicales;
            internal bool MideOriginal;
            private int limpia;   // cuadros desde la ultima limpieza de restos diminutos (Desnormales.cs)
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
                // En el silencio, los restos diminutos del EQ y del realce a cero (Desnormales.cs): cada 64 cuadros.
                if(++limpia==Desnormales.Cada)
                {
                    limpia=0;
                    if(currentEq!=null)Desnormales.Limpia(currentEq.State);
                    if(previousEq!=null)Desnormales.Limpia(previousEq.State);
                    Desnormales.Limpia(exLp);
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
        // Valores calculados al construir la etapa, nunca trigonometria ni reservas por muestra.
        internal static float ExciterPole(int rate)
        {
            if (rate <= 0 || rate == 44100) return .55f;
            return (float)(1 - Math.Pow(.45, 44100.0 / rate));
        }
        private static void LimpiaMuestras(float[] buffer, int offset, int n)
        {
            for (int i = offset; i < offset + n; i++)
            { float x = buffer[i]; if (!(x >= -4f && x <= 4f)) buffer[i] = 0f; }
        }
    }
}

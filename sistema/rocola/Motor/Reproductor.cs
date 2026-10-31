// LO QUE EL MOTOR OFRECE AL PROGRAMA (Rocola OS, hito 4): medir una cancion, armar la cadena y pasarla a 16 bits. Las etapas siguen
// siendo clases internas de Form1 (copiadas de XP); esto es lo publico.
using System;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // La cancion entera por la medida del repaso (como en XP): la ganancia para subirla hasta su limite sin rasparse (1 si no se pudo).
        public static float MideAlLimite(string ruta)
        {
            using (LectorCancion l = new LectorCancion(ruta))
            {
                MedidorBs1770 m = new MedidorBs1770(l.WaveFormat.SampleRate, new double[] { 1.0, 1.0 });
                float[] b = new float[16384]; int n;
                while ((n = l.Read(b.AsSpan())) > 0) m.Procesa(b, 0, n);
                SonoridadIntegrada s;
                if (!CierraIntegrada(m, m.Cuadros / (double)l.WaveFormat.SampleRate, l.WaveFormat.SampleRate, out s) || !s.ConP90) return 1f;
                return GananciaAlLimite(1f, Lin(s.PicoDb), Lin(s.P99Db), Lin(s.P90Db));
            }
        }
        private static float Lin(float db) { return (float)Math.Pow(10.0, db / 20.0); }

        // La cadena de fabrica de lo que ya esta en el motor: arreglos de la grabacion -> nivelado -> grave en mono -> limitador de pico real.
        public static ISampleProvider CadenaDeFabrica(ISampleProvider fuente, float ganancia)
        {
            ISampleProvider c = new ArreglaGrabacion(fuente, true, true);
            LevelTap tap = new LevelTap(c, (Form1)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Form1)));
            tap.Gain = ganancia; c = tap;
            c = new GraveMono(c, GraveMono.HzDeFabrica);
            c = new LimitadorPicoReal(c, TechoPicoReal, 3.0);
            return c;
        }

        // 16 bits con el dither TPDF de siempre (para escribir un WAV; la salida ALSA ya lo lleva dentro).
        public static IWaveProvider A16Bits(ISampleProvider s) { return new DitherWaveProvider16(s); }
    }
}

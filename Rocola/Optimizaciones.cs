using System;

namespace Rocola
{
    public partial class Form1
    {
        // Optimizaciones automaticas: no son preferencias visuales de Ctrl+F3.
        [ThreadStatic] private static Func<bool> measureCancellation;

        // ==========================================================================================
        //  QUIEN MIDE QUE: una libreta con candado para que dos trabajadores no midan lo mismo.
        //
        //  En la rocola hay dos que miden canciones -sonoridad, duracion y huella tonal- y ninguno
        //  sabia del otro:
        //
        //    - EL REPASO DE LA BIBLIOTECA arma su lista de pendientes UNA SOLA VEZ, al arrancar, y en
        //      su bucle no vuelve a mirar. Esa lista vive HORAS: una biblioteca de unos miles de
        //      canciones tarda varias en repasarse entera.
        //    - EL CALENTADOR DE LA SIGUIENTE mide la cancion que viene, en cuanto alguien la pide.
        //
        //  O sea que CADA CANCION QUE UN CLIENTE PEDIA se media dos veces: una al pedirla, y otra un
        //  rato despues, cuando al repaso le llegaba su turno. Medir una de cuatro minutos cuesta
        //  335 ms aqui -y mas en el equipo del bar, que descomprime MP3 y no WAV-, y eso es trabajo
        //  tirado peleando con la musica justo cuando hay gente eligiendo.
        //
        //  Aqui se apunta QUIEN esta midiendo que, y QUE se midio ya.
        //
        //  EL REPASO A FONDO NO ENTRA EN LA SEGUNDA PARTE, y es a proposito: ese existe justamente
        //  para volver a medir, porque analiza la cancion COMPLETA en vez de por tramos y saca
        //  material para la IA. Lo que si respeta es la primera: no se pone a medir lo que otro esta
        //  midiendo en ese instante.
        //
        //  Las rutas que se guardan son LAS MISMAS de las demas tablas (pasan por Pool), asi que lo
        //  que ocupa esto es el hueco de la lista, no las cadenas: del mismo orden que durCache, que
        //  ya guarda una fila por cancion.
        // ==========================================================================================
        private static readonly object medidasCerrojo = new object();
        private static readonly System.Collections.Generic.HashSet<string> medidasEnCurso =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Generic.HashSet<string> medidasHechas =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pide el turno para medir un archivo. Falso = no lo midas: o lo esta midiendo otro ahora
        // mismo, o ya se midio. Quien recibe verdadero TIENE QUE soltarlo despues, pase lo que pase.
        private static bool ReclamaMedida(string ruta, bool aFondo)
        {
            if (ruta == null) return false;
            lock (medidasCerrojo)
            {
                if (medidasEnCurso.Contains(ruta)) return false;
                if (!aFondo && medidasHechas.Contains(ruta)) return false;
                medidasEnCurso.Add(ruta);
                return true;
            }
        }
        private static void SueltaMedida(string ruta)
        {
            if (ruta == null) return;
            lock (medidasCerrojo) medidasEnCurso.Remove(ruta);
        }
        private static bool ReclamaMedidaEsperando(string ruta, bool aFondo, Func<bool> cancel)
        {
            if (ruta == null) return false;
            while (true)
            {
                if (cancel != null && cancel()) return false;
                lock (medidasCerrojo)
                    if (!medidasEnCurso.Contains(ruta)) return ReclamaMedida(ruta, aFondo);
                // Ocupado no significa completado: si el otro se cancela, este conserva su trabajo.
                System.Threading.Thread.Sleep(40);
            }
        }
        private static void ReiniciaMedidasHechas()
        {
            // No borrar turnos vivos: una generacion vieja aun debe soltar su propio archivo.
            lock (medidasCerrojo) medidasHechas.Clear();
        }
        // Se apunta cuando la medida ESTA PUESTA en las tablas, no cuando se pide. Un archivo roto no
        // da ni duracion ni sonoridad: darlo por medido seria no volver a intentarlo nunca.
        private static void MedidaPuesta(string ruta)
        {
            if (ruta == null) return;
            lock (medidasCerrojo) medidasHechas.Add(ruta);
        }
        private static void OlvidaMedidaHecha(string ruta)
        {
            if (ruta == null) return;
            lock (medidasCerrojo) medidasHechas.Remove(ruta);
        }
        private static bool YaSeMidio(string ruta)
        {
            if (ruta == null) return false;
            lock (medidasCerrojo) return medidasHechas.Contains(ruta);
        }
        private bool MeasureFileResponsive(string path, bool aFondo, Func<bool> cancel,
            out float rms, out float durSec, out float[] eqCurve)
        {
            Func<bool> previous = measureCancellation;
            // Solo en los trabajadores de analisis, nunca en el callback de reproduccion.
            // Ceder por bloques a las portadas visibles; comprobar cancelacion tambien durante la espera.
            string unit = UnidadDe(path);
            bool smallCpu = CargaTrabajo.Procesadores <= 2;
            measureCancellation = delegate
            {
                if (cancel != null && cancel()) return true;
                return !EsperaPortadas(unit, smallCpu, cancel);
            };
            try { return MeasureFile(path, aFondo, out rms, out durSec, out eqCurve); }
            finally { measureCancellation = previous; }
        }
        private static bool MeasurementCancelled()
        {
            Func<bool> cancel = measureCancellation;
            return cancel != null && cancel();
        }

        private sealed class AudioFftPlan
        {
            internal readonly float[] Window, Cos, Sin, SinNeg;
            internal readonly int[] Reverse;
            internal AudioFftPlan(int size)
            {
                Window = new float[size]; Reverse = new int[size];
                Cos = new float[size / 2]; Sin = new float[size / 2]; SinNeg = new float[size / 2];
                for (int i = 0; i < size; i++) Window[i] = (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / size));
                for (int i = 1, j = 0; i < size; i++)
                {
                    int bit = size >> 1;
                    for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                    j |= bit; Reverse[i] = j;
                }
                for (int k = 0; k < size / 2; k++)
                {
                    Cos[k] = (float)Math.Cos(2.0 * Math.PI * k / size);
                    Sin[k] = (float)Math.Sin(2.0 * Math.PI * k / size);
                    SinNeg[k] = -Sin[k];
                }
            }
        }
        private static readonly object fftPlanLock = new object();
        private static AudioFftPlan fft2048, fft4096;
        private static AudioFftPlan GetAudioFftPlan(int size)
        {
            if (size != 2048 && size != 4096) return new AudioFftPlan(size);
            lock (fftPlanLock)
            {
                if (size == 2048) return fft2048 ?? (fft2048 = new AudioFftPlan(size));
                return fft4096 ?? (fft4096 = new AudioFftPlan(size));
            }
        }

    }
}

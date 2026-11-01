// LO QUE EL MOTOR OFRECE AL PROGRAMA (Rocola OS): medir una cancion, armarle la cadena entera de XP y pasarla a 16 bits. Las etapas siguen
// siendo clases internas de Form1 (copiadas de XP); esto es lo publico.
using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // 16 bits con el dither TPDF de siempre (para escribir un WAV; la salida ALSA ya lo lleva dentro).
        public static IWaveProvider A16Bits(ISampleProvider s) { return new DitherWaveProvider16(s); }

        // ---- LA CADENA ENTERA, COMO EN XP (Cadena.cs: ArmaCadena) ----
        // Un motor = la rocola sin pantalla: los ajustes de fabrica de XP (los de sus campos) y sus tablas en memoria.

        // Una cancion lista para sonar: lo que se lee es Salida (flotante, antes del paso a 16 bits). Al soltarla (Dispose) se cierra lo
        // que se aprendio oyendola (el volumen, la huella del EQ, el volumen parejo del billar) y se guarda, como al acabar una cancion en XP.
        public sealed class Cancion : IDisposable
        {
            internal Form1 motor;
            internal object lector, cadena;   // IAudioSource y CadenaCancion (privados de Form1)
            private bool suelta;
            private IAudioSource Lector { get { return (IAudioSource)lector; } }
            private CadenaCancion Cadena { get { return (CadenaCancion)cadena; } }
            public ISampleProvider Salida { get { return Cadena.Salida; } }
            public TimeSpan Duracion { get { return Lector.TotalTime; } }
            public TimeSpan Posicion { get { return Lector.CurrentTime; } }
            public float GananciaDb { get { return (float)(20.0 * Math.Log10(Math.Max(1e-9f, Cadena.Tap.Gain))); } }
            public void Dispose() { if (suelta) return; suelta = true; motor.Termina(this); }
        }

        // La medida del repaso de la biblioteca (la sonoridad, los picos y el P90: Sonoridad.cs), si aun no esta en la tabla. Con ella la
        // cancion se nivela "al limite" desde la primera vez, como en XP despues del repaso. Se guarda en rocola_sonoridad.txt.
        public bool MideSiFalta(string ruta)
        {
            if (integCache == null) integCache = new Dictionary<string, SonoridadIntegrada>(StringComparer.OrdinalIgnoreCase);
            SonoridadIntegrada ya;
            if (integCache.TryGetValue(ruta, out ya) && ya.Algoritmo >= MedidorBs1770.Algoritmo) return true;
            SonoridadIntegrada s;
            if (!MideIntegrada(ruta, out s)) return false;
            PublicaIntegradaDeFuente(ruta, s, TomaFuenteMedida());   // con el sello del archivo medido, como el repaso de XP
            SaveIntegCache();
            return true;
        }

        // Abre la cancion y le arma la cadena entera de XP (ArmaCadena), con lo que este motor tenga puesto. Lo que sigue a ArmaCadena es lo
        // de StartOpenedTrack de XP (Form1.Sonido.cs): lo que se aprende oyendola va en los campos de siempre.
        public Cancion Arma(string ruta)
        {
            if (cadenaSonando != null) throw new InvalidOperationException("ya suena otra: hay que soltarla antes (Dispose)");
            IAudioSource lector = OpenAudio(ruta);
            try
            {
                lector.Volume = 1f;
                Track t = new Track(); t.Path = ruta;
                PlayItem item = new PlayItem(); item.Song = t;
                sonoAntes = nowPlaying; nowPlaying = item;
                CadenaCancion cad = ArmaCadena(t, lector, false, 0.0);
                cad.Item = item;
                lock (audioDatosLock)
                {
                    curMeasurePath = cad.MedidaRuta; curSumSq = 0; curSamples = 0;
                    curFuenteMedida = cad.Fuente;
                    eqMeasPath = cad.EqRuta;
                    eqMeasCut = 0f;   // el acantilado es de ESTA cancion: se vuelve a medir
                    eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0;
                    kwRate = 0;   // cada cancion mide desde cero
                    tapMedido = cad.Tap;
                }
                curSbr = cad.Sbr; curTap = cad.Tap; curBass = cad.Bajo;
                bassSongFactor = cad.BajoFactor; bassAutoBase = cad.BajoBase;
                cadenaSonando = cad;
                Cancion c = new Cancion(); c.motor = this; c.lector = lector; c.cadena = cad;
                return c;
            }
            catch { nowPlaying = null; lector.Dispose(); throw; }
        }

        // Al acabar (o saltar) la cancion: como DisposePlaybackNow de XP, la parte del sonido.
        internal void Termina(Cancion c)
        {
            CadenaCancion cad = (CadenaCancion)c.cadena;
            InvalidaPublicacionAudio();   // los bloques tardios ya no pueden publicar medidas
            lock (audioDatosLock)
            {
                if (object.ReferenceEquals(cadenaSonando, cad)) { AprendeParejo(cad); cadenaSonando = null; }   // el volumen parejo del billar, si sono entera
                FinalizeLoudMeasure();
                FinalizeEqMeasure();
            }
            curTap = null; curSbr = null; curBass = null;
            try { ((IAudioSource)c.lector).Dispose(); } catch { }
        }

        // Al cerrar: todo lo aprendido, al disco, y espera a que se escriba (como al cerrar la rocola de XP).
        public void Cierra()
        {
            try { SaveLoudCache(); } catch { }
            try { SaveIntegCache(); } catch { }
            try { FlushEqIfDirty(); } catch { }
            try { SaveDeepDone(); } catch { }
            try { SaveDurFast(); } catch { }
            EsperaGuardados(15000);
            Bitacora.Vacia(5000);
        }

        // Espera a que lo encargado al disco termine de escribirse (QueueDataWrite).
        private bool EsperaGuardados(int ms) { return dataWritesIdle.WaitOne(ms); }

        // Lo que hizo cada etapa (D1, BitacoraBillar.cs): en XP lo muestrea el reloj de la cancion cada medio segundo; aqui lo llama quien toca.
        public void MuestreaEtapas(Cancion c) { if (c != null) MuestreaEtapas((CadenaCancion)c.cadena); }
    }
}

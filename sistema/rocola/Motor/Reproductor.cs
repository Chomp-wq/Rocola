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

        // Una cancion lista para sonar: lo que se lee es Salida (flotante, antes del paso a 16 bits).
        public sealed class Cancion : IDisposable
        {
            internal object lector, cadena;   // IAudioSource y CadenaCancion (privados de Form1)
            private IAudioSource Lector { get { return (IAudioSource)lector; } }
            private CadenaCancion Cadena { get { return (CadenaCancion)cadena; } }
            public ISampleProvider Salida { get { return Cadena.Salida; } }
            public TimeSpan Duracion { get { return Lector.TotalTime; } }
            public TimeSpan Posicion { get { return Lector.CurrentTime; } }
            public float GananciaDb { get { return (float)(20.0 * Math.Log10(Math.Max(1e-9f, Cadena.Tap.Gain))); } }
            public void Dispose() { Lector.Dispose(); }
        }

        // La medida del repaso de la biblioteca (la sonoridad, los picos y el P90: Sonoridad.cs), si aun no esta en la tabla. Con ella la
        // cancion se nivela "al limite" desde la primera vez, como en XP despues del repaso. false si no se pudo medir.
        public bool MideSiFalta(string ruta)
        {
            if (integCache == null) integCache = new Dictionary<string, SonoridadIntegrada>(StringComparer.OrdinalIgnoreCase);
            if (integCache.ContainsKey(ruta)) return true;
            SonoridadIntegrada s;
            if (!MideIntegrada(ruta, out s)) return false;
            integCache[ruta] = s;
            ActualizaFuenteMedida(ruta, TomaFuenteMedida());   // el sello del archivo medido (FuentesMedidas.cs), como el publicador de XP
            return true;
        }

        // Abre la cancion y le arma la cadena entera de XP (ArmaCadena), con lo que este motor tenga puesto.
        public Cancion Arma(string ruta)
        {
            IAudioSource lector = OpenAudio(ruta);
            try
            {
                lector.Volume = 1f;
                Track t = new Track(); t.Path = ruta;
                Cancion c = new Cancion(); c.lector = lector; c.cadena = ArmaCadena(t, lector, false, 0.0);
                return c;
            }
            catch { lector.Dispose(); throw; }
        }
    }
}

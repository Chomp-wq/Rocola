// LO QUE EL MOTOR OFRECE AL PROGRAMA (Rocola OS): medir una cancion, guardar lo aprendido y pasar a 16 bits (la cola, en Cola.cs). Las etapas siguen
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

        // Al cerrar: todo lo aprendido, al disco, y espera a que se escriba (como al cerrar la rocola de XP).
        public void Cierra()
        {
            try { SaveLoudCache(); } catch { }
            try { SaveIntegCache(); } catch { }
            try { FlushEqIfDirty(); } catch { }
            try { SaveDeepDone(); } catch { }
            try { SaveDurFast(); } catch { }
            try { if (statsDirty) { statsDirty = false; SaveStats(false); } } catch { }
            EsperaGuardados(15000);
            Bitacora.Vacia(5000);
        }

        // Espera a que lo encargado al disco termine de escribirse (QueueDataWrite).
        private bool EsperaGuardados(int ms) { return dataWritesIdle.WaitOne(ms); }
    }
}

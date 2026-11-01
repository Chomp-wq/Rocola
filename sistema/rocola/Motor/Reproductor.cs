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

        // LA MEDIDA EN SEGUNDO PLANO (como el repaso de XP, que mide en hilos de fondo y publica en la pantalla): MideEnFondo la encarga,
        // un hilo de prioridad baja la mide (MideIntegrada no toca nada compartido) y PublicaMedidas, desde el hilo de siempre, la
        // pone en la tabla y la guarda. Asi pedir una cancion no congela la pantalla mientras se lee entera.
        private readonly System.Collections.Concurrent.BlockingCollection<string> porMedir = new System.Collections.Concurrent.BlockingCollection<string>();
        private readonly System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>>> medidas =
            new System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>>>();
        private System.Threading.Thread medidor;
        public void MideEnFondo(string ruta)
        {
            if (ruta == null) return;
            SonoridadIntegrada ya;
            if (integCache != null && integCache.TryGetValue(ruta, out ya) && ya.Algoritmo >= MedidorBs1770.Algoritmo) return;
            if (medidor == null)
            {
                medidor = new System.Threading.Thread(delegate ()
                {
                    foreach (string r in porMedir.GetConsumingEnumerable())
                    {
                        SonoridadIntegrada s;
                        try { if (MideIntegrada(r, out s)) medidas.Enqueue(new KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>>(r, new KeyValuePair<SonoridadIntegrada, FuenteMedida>(s, TomaFuenteMedida()))); }
                        catch { }
                    }
                }) { IsBackground = true, Name = "repaso (medir)", Priority = System.Threading.ThreadPriority.BelowNormal };
                medidor.Start();
            }
            porMedir.Add(ruta);
        }
        // Lo medido en segundo plano, a la tabla (llamar desde el hilo de siempre). Devuelve cuantas publico.
        public int PublicaMedidas()
        {
            int n = 0;
            KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>> m;
            if (integCache == null) integCache = new Dictionary<string, SonoridadIntegrada>(StringComparer.OrdinalIgnoreCase);
            while (medidas.TryDequeue(out m)) { PublicaIntegradaDeFuente(m.Key, m.Value.Key, m.Value.Value); n++; }
            if (n > 0) SaveIntegCache();
            return n;
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

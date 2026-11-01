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
        // Dos filas: lo PEDIDO (se mide antes que nada) y EL REPASO de la biblioteca (lo que aun no esta medido, poco a poco, como la tanda
        // larga de XP). Un solo hilo, de prioridad baja; el repaso se para mientras suena un video (mpv necesita el procesador).
        private readonly System.Collections.Concurrent.BlockingCollection<string> porMedir = new System.Collections.Concurrent.BlockingCollection<string>();
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> porRepasar = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private readonly System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>>> medidas =
            new System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>>>();
        private System.Threading.Thread medidor;
        public volatile bool PausaRepaso;
        public int PorRepasar { get { return porRepasar.Count; } }
        private bool YaMedida(string ruta)
        {
            SonoridadIntegrada ya;
            return integCache != null && integCache.TryGetValue(ruta, out ya) && ya.Algoritmo >= MedidorBs1770.Algoritmo;
        }
        private void ArrancaMedidor()
        {
            if (medidor != null) return;
            medidor = new System.Threading.Thread(delegate ()
            {
                while (true)
                {
                    string r;
                    if (!porMedir.TryTake(out r, PausaRepaso ? 1000 : 0))
                    {
                        if (PausaRepaso || !porRepasar.TryDequeue(out r)) { if (!porMedir.TryTake(out r, 1000)) continue; }
                    }
                    SonoridadIntegrada s;
                    try { if (MideIntegrada(r, out s)) medidas.Enqueue(new KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>>(r, new KeyValuePair<SonoridadIntegrada, FuenteMedida>(s, TomaFuenteMedida()))); }
                    catch { }
                }
            }) { IsBackground = true, Name = "repaso (medir)", Priority = System.Threading.ThreadPriority.Lowest };
            medidor.Start();
        }
        public void MideEnFondo(string ruta)
        {
            if (ruta == null || YaMedida(ruta)) return;
            ArrancaMedidor();
            porMedir.Add(ruta);
        }
        // EL REPASO: las canciones de la biblioteca que aun no estan medidas (con el algoritmo de hoy), a la fila de fondo. Devuelve cuantas.
        public int RepasaBiblioteca()
        {
            int n = 0;
            foreach (Track t in tracks) if (t.Path != null && !YaMedida(t.Path)) { porRepasar.Enqueue(t.Path); n++; }
            if (n > 0) ArrancaMedidor();
            return n;
        }
        private int ultimoGuardadoRepaso; private bool repasoSinGuardar;
        // Lo medido en segundo plano, a la tabla (llamar desde el hilo de siempre). Devuelve cuantas publico.
        public int PublicaMedidas()
        {
            int n = 0;
            KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>> m;
            if (integCache == null) integCache = new Dictionary<string, SonoridadIntegrada>(StringComparer.OrdinalIgnoreCase);
            while (medidas.TryDequeue(out m)) { PublicaIntegradaDeFuente(m.Key, m.Value.Key, m.Value.Value); n++; }
            if (n > 0 && unchecked(Environment.TickCount - ultimoGuardadoRepaso) > 30000) { ultimoGuardadoRepaso = Environment.TickCount; SaveIntegCache(); }   // no en cada una: cada 30 s
            else if (n > 0) repasoSinGuardar = true;
            if (repasoSinGuardar && unchecked(Environment.TickCount - ultimoGuardadoRepaso) > 30000) { ultimoGuardadoRepaso = Environment.TickCount; repasoSinGuardar = false; SaveIntegCache(); }
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

        // Un error, a rocola_error.txt (LogError de XP, con su sello y su tope).
        public void ApuntaError(string donde, Exception ex) { try { LogError(donde, ex); } catch { } }
    }
}

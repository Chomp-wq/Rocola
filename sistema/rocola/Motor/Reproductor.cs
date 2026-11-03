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
        // REPASAR LA BIBLIOTECA A FONDO (el boton de «Tu biblioteca» de XP): mientras este encendido, un segundo hilo mide tambien lo que
        // falta del repaso (los dos nucleos del A4), con la misma prioridad baja: la musica no se corta y se para con los videos. Se
        // apaga solo cuando ya no queda nada. Lo medido se publica igual que siempre (PublicaMedidas). MideIntegrada usa lo suyo por hilo.
        private volatile bool repasoAFondo;
        public bool RepasoAFondo { get { return repasoAFondo; } }
        public void PonRepasoAFondo(bool on)
        {
            repasoAFondo = on && porRepasar.Count > 0;
            if (!repasoAFondo) return;
            ArrancaMedidor();
            System.Threading.Thread t = new System.Threading.Thread(delegate ()
            {
                Prioridad.Fondo();
                while (repasoAFondo)
                {
                    if (PausaRepaso) { System.Threading.Thread.Sleep(1000); continue; }
                    string r;
                    if (!porRepasar.TryDequeue(out r)) { repasoAFondo = false; break; }
                    MideUna(r, true);
                }
            }) { IsBackground = true, Name = "repaso a fondo", Priority = System.Threading.ThreadPriority.Lowest };
            t.Start();
        }
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
                Prioridad.Fondo();   // nice 19: el repaso solo usa lo que sobra (en Linux, Priority = Lowest no hace nada)
                while (true)
                {
                    string r; bool deRepaso = false;
                    if (!porMedir.TryTake(out r, PausaRepaso ? 1000 : 0))
                    {
                        if (!PausaRepaso && porRepasar.TryDequeue(out r)) deRepaso = true;
                        else if (!porMedir.TryTake(out r, 1000)) continue;
                    }
                    MideUna(r, deRepaso);
                }
            }) { IsBackground = true, Name = "repaso (medir)", Priority = System.Threading.ThreadPriority.Lowest };
            medidor.Start();
        }
        // UNA CANCION: lo pedido, solo su sonoridad entera (rapido: hay que nivelarla ya). Lo del repaso que aun no se repaso A FONDO,
        // con la medida completa de XP (MeasureFile a fondo: la sonoridad entera, la huella del EQ, el corte de los agudos -la calidad-,
        // los picos recortados y, si ia_datos= esta encendido, el material para la IA), de una sola lectura.
        private sealed class MedidaCompleta { internal string Ruta; internal float Rms, Dur; internal float[] Eq; internal FuenteMedida Fuente; internal bool HayEntera; internal SonoridadIntegrada Entera; }
        private readonly System.Collections.Concurrent.ConcurrentQueue<MedidaCompleta> completas = new System.Collections.Concurrent.ConcurrentQueue<MedidaCompleta>();
        private bool RepasadaAFondo(string ruta) { lock (deepDoneLock) return deepDone.Contains(ruta); }
        private void MideUna(string r, bool deRepaso)
        {
            try
            {
                if ((deRepaso || EsVideo(r)) && !RepasadaAFondo(r))   // un video, siempre la medida de XP (MeasureFile): no tiene la entera
                {
                    float rms, dur; float[] eq;
                    bool ok = MeasureFile(r, true, out rms, out dur, out eq);
                    FuenteMedida f = TomaFuenteMedida();
                    SonoridadIntegrada e; bool hay = TomaIntegrada(out e);
                    if (ok) completas.Enqueue(new MedidaCompleta { Ruta = r, Rms = rms, Dur = dur, Eq = eq, Fuente = f, HayEntera = hay, Entera = e });
                    return;
                }
                SonoridadIntegrada s;
                if (MideIntegrada(r, out s)) medidas.Enqueue(new KeyValuePair<string, KeyValuePair<SonoridadIntegrada, FuenteMedida>>(r, new KeyValuePair<SonoridadIntegrada, FuenteMedida>(s, TomaFuenteMedida())));
            }
            catch { }
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
            foreach (Track t in tracks) if (t.Path != null && (!YaMedida(t.Path) || !RepasadaAFondo(t.Path))) { porRepasar.Enqueue(t.Path); n++; }
            // LOS VIDEOS (Rocola OS): su sonido se mide como en XP se media el de un video (MeasureFile: cuanto suena), ahora que se puede
            // abrir (AudioDeVideo.cs, por ffmpeg); con eso, VolumenVideo los empareja con las canciones. Detras de las canciones.
            if (videoAlbums != null)
            {
                HashSet<string> vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Album va in videoAlbums)
                    if (va != null && va.VideoFiles != null)
                        foreach (string v in va.VideoFiles) if (v != null && vistos.Add(v) && !RepasadaAFondo(v)) { porRepasar.Enqueue(v); n++; }
            }
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
            try { RehaceCortesIa(); } catch { }   // IA2: los cortes de la cosecha, de los de la biblioteca (CortesIa.cs)
            while (medidas.TryDequeue(out m)) { PublicaIntegradaDeFuente(m.Key, m.Value.Key, m.Value.Value); n++; }
            MedidaCompleta mc;
            while (completas.TryDequeue(out mc))
            {
                bool pub = PublicaMedidaDeFuente(mc.Ruta, mc.Rms, mc.Dur, mc.Eq, mc.Fuente);
                if (mc.HayEntera) PublicaIntegradaDeFuente(mc.Ruta, mc.Entera, mc.Fuente);
                if (pub) MarcaAFondo(mc.Ruta);   // nunca dar por terminada una medida de una fuente sustituida (XP)
                n++;
            }
            if (n > 0 && unchecked(Environment.TickCount - ultimoGuardadoRepaso) > 30000) { ultimoGuardadoRepaso = Environment.TickCount; SaveIntegCache(); SaveLoudCache(); FlushEqIfDirty(); if (iaDoneDirty) { iaDoneDirty = false; SaveIaDone(); } if (deepDoneDirty) { deepDoneDirty = false; SaveDeepDone(); } }   // no en cada una: cada 30 s
            else if (n > 0) repasoSinGuardar = true;
            if (repasoSinGuardar && unchecked(Environment.TickCount - ultimoGuardadoRepaso) > 30000) { ultimoGuardadoRepaso = Environment.TickCount; repasoSinGuardar = false; SaveIntegCache(); }
            return n;
        }

        // Al cerrar: todo lo aprendido, al disco, y espera a que se escriba (como al cerrar la rocola de XP).
        public void Cierra()
        {
            cerrandoEntreno = true;   // el entrenamiento de la IA, si iba, se corta (IaNueva.cs)
            try { SaveLoudCache(); } catch { }
            try { SaveIntegCache(); } catch { }
            try { FlushEqIfDirty(); } catch { }
            try { SaveDeepDone(); } catch { }
            try { SaveIaDone(); } catch { }
            try { SaveDurFast(); } catch { }
            try { if (statsDirty) { statsDirty = false; SaveStats(false); } } catch { }
            try { GuardaNoche(false); } catch { }
            EsperaGuardados(15000);
            Bitacora.Vacia(5000);
        }

        // Espera a que lo encargado al disco termine de escribirse (QueueDataWrite).
        private bool EsperaGuardados(int ms) { return dataWritesIdle.WaitOne(ms); }

        // Un error, a rocola_error.txt (LogError de XP, con su sello y su tope).
        public void ApuntaError(string donde, Exception ex) { try { LogError(donde, ex); } catch { } }

        // LA CAJA NEGRA DEL SONIDO EN ROCOLA OS (3-11-2026; «[Cortes de sonido]» de XP, SalidaSonido.cs): cada vez que la tarjeta se
        // quedo sin musica (un corte que se oye) la salida ALSA lo recupera y lo cuenta; al cambiar de cancion o cerrar la salida,
        // si hubo alguno, una linea en rocola_error.txt con la cancion, cuantos, la tarjeta y como iba el hilo del sonido. Por la bitacora
        // de fondo: la musica y la pantalla no esperan al disco. Sin cortes, nada.
        public void ApuntaCortes(string cancion, long cortes, string tarjeta, string hilo)
        {
            if (cortes <= 0) return;
            try
            {
                string c = string.IsNullOrEmpty(cancion) ? "" : "\"" + (cancion.Length > 80 ? cancion.Substring(0, 80) + "..." : cancion) + "\": ";
                Bitacora.ApuntaConSello(RutaDatos("rocola_error.txt"), 262144, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [Cortes de sonido]",
                    " " + c + cortes + (cortes == 1 ? " corte" : " cortes") + " (la tarjeta se quedo sin musica y se recupero); salida " + tarjeta
                    + ", hilo del sonido en " + (string.IsNullOrEmpty(hilo) ? "?" : hilo) + (PausaRepaso ? "" : ", con el repaso de la biblioteca en marcha") + "\r\n");
            }
            catch { }
        }
    }
}

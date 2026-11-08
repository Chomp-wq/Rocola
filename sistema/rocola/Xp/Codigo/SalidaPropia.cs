// LA SALIDA DE SONIDO CON SU PROPIO HILO (T5, 25 de octubre de 2026).
//
// POR QUE. La salida clasica era el WaveOut de NAudio con aviso por VENTANA: cada bloque que la tarjeta terminaba de sonar lo volvia a
// llenar EL HILO DE LA PANTALLA, en su bucle de mensajes (copiando del colchon del hilo de la musica, BombaSonido). Si la pantalla se
// paraba mas que el colchon de la salida (750 a 1000 ms en XP), la musica se cortaba. Y en el A4 la pantalla se paro 2015 ms (23-10, en
// el Present de la tarjeta) y 3172 ms (24-10, al arrancar). El WaveOutEvent de NAudio, que usa un hilo, no sirve: llama a
// WaitHandle.WaitOne(Int32), que el .NET del A4 (2.0.50727.1433, sin el SP2) no tiene, y la rocola truena.
//
// QUE HACE. Habla directamente con Windows (winmm: waveOutOpen con aviso por EVENTO) y tiene un hilo solo para eso, en prioridad alta:
// espera el aviso (WaitOne con su segundo parametro, que si existe en el A4), y cada bloque que la tarjeta termino lo vuelve a llenar y se
// lo devuelve. La pantalla ya no interviene: un atoro suyo no corta la musica. Los mismos bloques (latencia / 50 ms), el mismo formato, el
// mismo reloj de la cancion (waveOutGetPosition, en bytes) y el mismo aviso de parada (PlaybackStopped, que se pasa al hilo de la pantalla).
// No cambia nada de lo que suena: automatico, sin interruptor (regla del 15-09).
//
// SI ALGO FALLA. Si no abre por algo que no es el aparato (una funcion de Windows que no esta, cualquier cosa rara), Form1 usa el WaveOut de
// siempre durante esa sesion (SalidaSonido.cs). Un fallo del APARATO (desenchufado, sin controlador) se trata como siempre.
//
// Windows va detras de una interfaz (ISistemaWaveOut): la de verdad llama a winmm.dll, y las pruebas ponen una imitada que "suena" con un
// reloj (pruebas/salidapropia.cs). En Linux no hay winmm: la de verdad falla al abrir y se usaria la de siempre.
//
// EN LINUX (port nativo, 8-11-2026): la tarjeta es la de ALSA y esta salida le habla directo (Nativo/SalidaAlsa.cs), sin la interfaz con
// forma de winmm (ISistemaWaveOut), sin las WAVEHDR (CabeceraWave y sus banderas) y sin los MMSYSERR: los mismos bloques de 50 ms, en el
// mismo orden, el mismo hilo, la misma espera de 100 ms como mucho, el mismo final, el mismo reloj en bytes y la misma parada. Un bloque
// «ya sono» (lo que en XP era la bandera WHDR_DONE que pone Windows) cuando lo sonado en la tarjeta (SalidaAlsa.Sonado) pasa de donde acaba.
// Los errores son ErrorAlsa: Form1 decide con ellos lo mismo que con los de winmm (SalidaSonido.cs).
using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // EN LINUX: aqui XP tiene la interfaz ISistemaWaveOut (lo que se le pide a Windows, con los MMSYSERR de winmm; la imitan las pruebas),
    // WinmmWaveOut (la de verdad: waveOutOpen, waveOutPrepareHeader, waveOutWrite, waveOutReset, waveOutPause, waveOutRestart, waveOutClose
    // y waveOutGetPosition de winmm.dll) y CabeceraWave (una WAVEHDR en memoria no administrada). En Linux la tarjeta es la de ALSA y
    // WaveOutPropio la llama directo (Nativo/SalidaAlsa.cs).

    internal sealed class WaveOutPropio : IWavePlayer, IWavePosition
    {
        // EN LINUX: (las banderas WHDR_DONE y WHDR_INQUEUE y las medidas de la WAVEHDR, y Sistema: winmm o una imitada de las pruebas) la
        // tarjeta es la de ALSA (Nativo/SalidaAlsa.cs) y un bloque ya sono cuando lo sonado pasa de donde acaba (finDe)

        private readonly SynchronizationContext pantalla;
        private readonly object cerrojo = new object();      // las llamadas a Windows sobre la salida, de una en una   // EN LINUX: a ALSA
        private readonly AutoResetEvent aviso = new AutoResetEvent(false);   // EN LINUX: lo senalan Play, Stop y Dispose (Windows tambien al acabar cada bloque: aqui el hilo calcula cuando acaba, EsperaMs)
        private IWaveProvider fuente;
        private WaveFormat formato;
        private SalidaAlsa salida;   // EN LINUX: (IntPtr salida, el HWAVEOUT de winmm) la tarjeta abierta
        private byte[][] datos;      // EN LINUX: (IntPtr[] cabeceras, datos: las WAVEHDR y sus datos en memoria no administrada) cada bloque
        private int[] largos;        // EN LINUX: (dwBufferLength de cada WAVEHDR) lo que lleva cada bloque
        private long[] finDe;        // EN LINUX: (WHDR_DONE) hasta donde llega cada bloque escrito, en cuadros desde el ultimo Vacia; 0 = ya sono
        private byte[] lectura;
        private int largoBloque;
        private Thread hilo;
        private volatile PlaybackState estado = PlaybackState.Stopped;
        private volatile bool parar, fin, deLaFuente;
        private int avisado;                                  // 1 = ya se aviso la parada (Interlocked: la pantalla y el hilo pueden llegar a la vez)
        private bool cerrada;
        // El ultimo aviso de parada con error fue de la propia salida (Windows o este codigo), no de la musica que le dan (la cancion o la
        // cadena, que se tratan como siempre). Lo mira Form1 para volver a la salida de siempre (SalidaSonido.cs).
        internal bool FalloPropio { get { return !deLaFuente; } }
        // EN LINUX: la salida «de siempre» de XP (el WaveOut de NAudio, con winmm) aqui tambien es una WaveOutPropio, sobre la misma ALSA:
        // esto la distingue, para que Form1 decida con ella lo mismo que con el WaveOut (SalidaSonido.cs, EsLaPropia)
        internal bool LaDeSiempre;

        public int DesiredLatency = 300;
        public int NumberOfBuffers = 2;

        // EN LINUX: (WaveOutPropio(ISistemaWaveOut sistema), con el de Windows o uno imitado) solo la tarjeta de ALSA
        internal WaveOutPropio()
        {
            pantalla = SynchronizationContext.Current;   // se crea en la pantalla: ahi se avisa la parada
        }

        public event EventHandler<StoppedEventArgs> PlaybackStopped;
        public PlaybackState PlaybackState { get { return estado; } }
        public float Volume { get { return 1f; } set { } }   // la rocola nunca toca el volumen de la salida (AGENTS.md)
        public WaveFormat OutputWaveFormat { get { return formato; } }
        internal int Bloques { get { return datos == null ? 0 : datos.Length; } }   // EN LINUX: (cabeceras) los bloques

        public void Init(IWaveProvider waveProvider)
        {
            if (waveProvider == null) throw new ArgumentNullException("waveProvider");
            if (salida != null) throw new InvalidOperationException("ya iniciada");
            fuente = waveProvider; formato = waveProvider.WaveFormat;
            int n = Math.Max(2, NumberOfBuffers);
            int ms = Math.Max(10, DesiredLatency / n);
            largoBloque = (int)((long)formato.AverageBytesPerSecond * ms / 1000);
            int alin = Math.Max(1, formato.BlockAlign);
            largoBloque = Math.Max(alin, largoBloque - largoBloque % alin);
            // EN LINUX: (so.Abre: waveOutOpen con aviso por evento; si falla, MmException) la tarjeta de ALSA con un periodo de un bloque y
            // un bufer de todos (si no se puede, ErrorAlsa)
            SalidaAlsa h;
            lock (cerrojo) h = SalidaAlsa.Abre(formato, largoBloque / alin, n);
            salida = h;
            // EN LINUX: (las WAVEHDR en memoria no administrada, cada una con waveOutPrepareHeader) los bloques, en arreglos: ALSA no prepara nada
            datos = new byte[n][]; largos = new int[n]; finDe = new long[n]; lectura = new byte[largoBloque];
            for (int i = 0; i < n; i++) { datos[i] = new byte[largoBloque]; largos[i] = largoBloque; }
        }

        public void Play()
        {
            if (salida == null) throw new InvalidOperationException("sin iniciar");
            if (estado == PlaybackState.Playing) return;
            if (estado == PlaybackState.Paused)
            {
                // EN LINUX: (so.Sigue: waveOutRestart; si falla, MmException) snd_pcm_pause(0). Una tarjeta que no sabe pausar no se paro: sigue.
                lock (cerrojo) salida.Pausa(false);
                estado = PlaybackState.Playing; aviso.Set();
                return;
            }
            estado = PlaybackState.Playing; parar = false; fin = false; Interlocked.Exchange(ref avisado, 0);
            if (hilo == null || !hilo.IsAlive)
            {
                hilo = new Thread(PrioridadHilo.Con(Bucle));   // EN LINUX: (new Thread(X)) con la prioridad que XP le pone antes de Start: en Linux .NET no la aplica (Nativo/PrioridadHilo.cs)
                hilo.IsBackground = true; hilo.Name = "audio (salida)";
                try { hilo.Priority = ThreadPriority.Highest; } catch { }
                hilo.Start();
            }
            aviso.Set();
        }

        public void Pause()
        {
            if (estado != PlaybackState.Playing || salida == null) return;
            // EN LINUX: (so.Pausa: waveOutPause; si falla, MmException) snd_pcm_pause(1). Si la tarjeta no sabe pausar, suena lo que ya tiene
            // y se calla (el hilo no le da mas mientras este en pausa); XP no pausa la musica nunca (solo Play, Stop y Dispose).
            lock (cerrojo) salida.Pausa(true);
            estado = PlaybackState.Paused;
        }

        public void Stop()
        {
            if (estado == PlaybackState.Stopped) return;
            estado = PlaybackState.Stopped;
            if (salida != null) lock (cerrojo) Reinicia();   // devuelve todos los bloques; el hilo ya no los vuelve a llenar   // EN LINUX: (so.Reinicia: waveOutReset) Reinicia
            Avisa(null);
        }

        public long GetPosition()
        {
            if (salida == null) return 0;
            long b;
            // EN LINUX: (so.Posicion: waveOutGetPosition en bytes; si falla, MmException) lo que ya sono en la tarjeta desde el ultimo Vacia, en
            // bytes y de 32 bits como el MMTIME de Windows (Form1.AmpliaRelojSalida cuenta sus vueltas); si ALSA falla, ErrorAlsa
            lock (cerrojo) { if (salida == null) return 0; b = unchecked((long)(uint)(salida.Sonado() * Math.Max(1, formato.BlockAlign))); }
            return b;
        }

        // EL HILO DE LA SALIDA: espera el aviso de Windows (o 100 ms) y rellena los bloques que ya sonaron, en orden.
        // EN LINUX: espera a que acabe de sonar el bloque mas viejo (lo calcula con lo sonado, EsperaMs), o el aviso de Play, Stop o Dispose, o
        // 100 ms; y rellena los bloques que ya sonaron, en orden
        private void Bucle()
        {
            HilosRocola.Nombra("audio (salida)");   // para el reparto por hilo del diagnostico completo
            int siguiente = 0;
            bool[] escrito = new bool[datos.Length];   // EN LINUX: (cabeceras.Length) los bloques
            try
            {
                while (!parar)
                {
                    aviso.WaitOne(EsperaMs(escrito), false);   // con el bool: WaitOne(Int32) solo no existe en el .NET del A4   // EN LINUX: (WaitOne(100, false)) hasta que acabe el bloque mas viejo, como mucho 100 ms
                    if (parar) break;
                    if (estado != PlaybackState.Playing) continue;
                    long ya = Sonado();   // EN LINUX: lo que ya sono en la tarjeta (con eso se sabe que bloques acabaron: WHDR_DONE)
                    // En orden: se escribieron 0, 1, 2... y en ese orden vuelven. Si el mas viejo aun suena, los de detras tambien.
                    for (int k = 0; k < datos.Length && !parar && !fin && estado == PlaybackState.Playing; k++)
                    {
                        int i = siguiente;
                        if (escrito[i] && finDe[i] > ya) break;   // aun sonando   // EN LINUX: (WHDR_DONE sin poner) aun no sono hasta donde llega
                        if (Libres() < largoBloque / Math.Max(1, formato.BlockAlign)) break;   // EN LINUX: la tarjeta aun no tiene sitio para un bloque entero (si su bufer salio menor que todos los bloques)
                        escrito[i] = false;
                        int n = Llena(i);
                        if (n <= 0) { fin = true; break; }
                        // EN LINUX: (so.Escribe: waveOutWrite; si falla, MmException) el bloque a la tarjeta (snd_pcm_writei; si falla, ErrorAlsa)
                        lock (cerrojo)
                        {
                            if (parar || estado != PlaybackState.Playing) break;
                            salida.Escribe(datos[i], largos[i]);
                            finDe[i] = salida.Escritos;
                        }
                        escrito[i] = true;
                        siguiente = (i + 1) % datos.Length;
                    }
                    ya = Sonado();
                    int enCola = 0;
                    for (int i = 0; i < datos.Length; i++)
                        if (escrito[i] && finDe[i] > ya) enCola++;   // EN LINUX: (WHDR_DONE sin poner) los que aun no acaban de sonar
                    if (fin && enCola == 0 && estado == PlaybackState.Playing)
                    {   // se acabo lo que habia que sonar y ya sono todo
                        estado = PlaybackState.Stopped;
                        Avisa(null);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                estado = PlaybackState.Stopped;
                if (!parar) Avisa(ex);
            }
        }

        // Llena el bloque i desde la fuente (lo que falte para un bloque entero, en orden) y fija su largo. Devuelve los bytes.
        private int Llena(int i)
        {
            int hechos = 0;
            while (hechos < largoBloque)
            {
                int r;
                try { r = fuente.Read(lectura, hechos, largoBloque - hechos); }
                catch { deLaFuente = true; throw; }
                if (r <= 0) break;
                hechos += r;
            }
            hechos -= hechos % Math.Max(1, formato.BlockAlign);
            if (hechos <= 0) return 0;
            Buffer.BlockCopy(lectura, 0, datos[i], 0, hechos);   // EN LINUX: (Marshal.Copy a la memoria del bloque) al arreglo del bloque
            largos[i] = hechos;   // EN LINUX: (dwBufferLength de su WAVEHDR) su largo
            return hechos;
        }

        // EN LINUX: lo que en XP contesta Windows sobre los bloques (WHDR_DONE, el aviso al acabar cada uno) se calcula con lo sonado.
        // Lo sonado en la tarjeta, en cuadros desde el ultimo Vacia (si ALSA falla, ErrorAlsa: lo atiende el hilo, como un fallo de winmm)
        private long Sonado() { lock (cerrojo) return salida != null && !parar ? salida.Sonado() : long.MaxValue; }
        // Cuanto cabe en la tarjeta ahora, en cuadros
        private int Libres() { lock (cerrojo) return salida != null && !parar ? salida.Libres() : 0; }
        // Cuanto esperar al aviso: hasta que acabe de sonar el bloque escrito mas viejo que aun suena (en XP, Windows senala el evento en ese
        // momento), como mucho 100 ms (la espera de XP) y al menos 1 ms
        private int EsperaMs(bool[] escrito)
        {
            if (estado != PlaybackState.Playing) return 100;
            long ya, primero = long.MaxValue;
            try { ya = Sonado(); } catch { return 1; }   // (el hilo vera el fallo enseguida, al pedirlo otra vez)
            for (int i = 0; i < escrito.Length; i++) if (escrito[i] && finDe[i] > ya && finDe[i] < primero) primero = finDe[i];
            if (primero == long.MaxValue) return fin ? 100 : 1;   // nada sonando: si aun hay que escribir, enseguida
            long ms = (primero - ya) * 1000 / Math.Max(1, formato.SampleRate) + 1;
            return (int)Math.Max(1, Math.Min(100, ms));
        }
        // EN LINUX: (waveOutReset) lo que no sono se tira (snd_pcm_drop), la cuenta vuelve a 0 y todos los bloques quedan sonados. Con el cerrojo.
        private void Reinicia()
        {
            salida.Vacia();
            if (finDe != null) for (int i = 0; i < finDe.Length; i++) finDe[i] = 0;
        }

        // La parada, una sola vez, en el hilo de la pantalla (si se creo alli) como el WaveOut de siempre.
        private void Avisa(Exception ex)
        {
            if (Interlocked.Exchange(ref avisado, 1) != 0) return;
            EventHandler<StoppedEventArgs> h = PlaybackStopped;
            if (h == null) return;
            StoppedEventArgs e = new StoppedEventArgs(ex);
            SynchronizationContext p = pantalla;
            if (p != null) { try { p.Post(delegate { h(this, e); }, null); return; } catch { } }
            h(this, e);
        }

        public void Dispose()
        {
            if (cerrada) return;
            cerrada = true;
            parar = true;
            estado = PlaybackState.Stopped;
            if (salida != null) lock (cerrojo) Reinicia();   // EN LINUX: (so.Reinicia: waveOutReset) Reinicia
            aviso.Set();
            Thread t = hilo; hilo = null;
            // Si el hilo no acaba en 2 s (una lectura de la fuente atorada), los bloques NO se liberan: aun puede copiar en ellos, y
            // liberarlos tumbaria la rocola. Se cierra la salida y se pierde esa poca memoria, que es lo de menos.
            bool suelto = t == null || object.ReferenceEquals(t, Thread.CurrentThread) || t.Join(2000);
            // EN LINUX: (so.Cierra: waveOutClose) se cierra la tarjeta con el cerrojo y sin ella el hilo ya no la toca (comprueba que este)
            if (!suelto) { lock (cerrojo) { if (salida != null) { try { salida.Cierra(); } catch { } salida = null; } } return; }
            Libera();
            aviso.Close();
        }

        private void Libera()
        {
            lock (cerrojo)
            {
                // EN LINUX: (waveOutUnprepareHeader y FreeHGlobal de cada WAVEHDR y de sus datos) los bloques son arreglos: el recolector los suelta
                if (salida != null) { try { salida.Cierra(); } catch { } salida = null; }   // EN LINUX: (so.Cierra: waveOutClose) se cierra la tarjeta
            }
        }
    }
}

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
using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // Lo que se le pide a Windows. Los resultados son los MMSYSERR de winmm (0 = bien). Publica solo para que las pruebas pongan una imitada.
    public interface ISistemaWaveOut
    {
        int Abre(out IntPtr salida, WaveFormat formato, AutoResetEvent aviso);
        int Prepara(IntPtr salida, IntPtr cabecera, int tamano);
        int Escribe(IntPtr salida, IntPtr cabecera, int tamano);
        int Desprepara(IntPtr salida, IntPtr cabecera, int tamano);
        int Reinicia(IntPtr salida);
        int Pausa(IntPtr salida);
        int Sigue(IntPtr salida);
        int Cierra(IntPtr salida);
        int Posicion(IntPtr salida, out long bytes, WaveFormat formato);
    }

    // La de verdad: en Windows, winmm.dll; en Linux, ALSA (AlsaWaveOut, Plataforma/SonidoAlsa.cs; traduccion de XP).

    // Una WAVEHDR en memoria no administrada: lpData, dwBufferLength, dwBytesRecorded, dwUser, dwFlags, dwLoops, lpNext, reserved.
    [StructLayout(LayoutKind.Sequential)]
    internal struct CabeceraWave { public IntPtr Datos; public int Largo; public int Grabados; public IntPtr Usuario; public int Banderas; public int Vueltas; public IntPtr Siguiente; public IntPtr Reservado; }

    internal sealed class WaveOutPropio : IWavePlayer, IWavePosition
    {
        internal const int Hecho = 0x1, EnCola = 0x10;     // WHDR_DONE, WHDR_INQUEUE
        internal static readonly int TamCabecera = Marshal.SizeOf(typeof(CabeceraWave));
        internal static readonly int OffLargo = (int)Marshal.OffsetOf(typeof(CabeceraWave), "Largo");
        internal static readonly int OffBanderas = (int)Marshal.OffsetOf(typeof(CabeceraWave), "Banderas");
        // Windows de verdad; las pruebas ponen uno imitado.
        internal static Func<ISistemaWaveOut> Sistema = delegate { return new AlsaWaveOut(); };

        private readonly ISistemaWaveOut so;
        private readonly SynchronizationContext pantalla;
        private readonly object cerrojo = new object();      // las llamadas a Windows sobre la salida, de una en una
        private readonly AutoResetEvent aviso = new AutoResetEvent(false);
        private IWaveProvider fuente;
        private WaveFormat formato;
        private IntPtr salida;
        private IntPtr[] cabeceras, datos;
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

        public int DesiredLatency = 300;
        public int NumberOfBuffers = 2;

        internal WaveOutPropio() : this(null) { }
        internal WaveOutPropio(ISistemaWaveOut sistema)
        {
            so = sistema ?? Sistema();
            pantalla = SynchronizationContext.Current;   // se crea en la pantalla: ahi se avisa la parada
        }

        public event EventHandler<StoppedEventArgs> PlaybackStopped;
        public PlaybackState PlaybackState { get { return estado; } }
        public float Volume { get { return 1f; } set { } }   // la rocola nunca toca el volumen de la salida (AGENTS.md)
        public WaveFormat OutputWaveFormat { get { return formato; } }
        internal int Bloques { get { return cabeceras == null ? 0 : cabeceras.Length; } }

        public void Init(IWaveProvider waveProvider)
        {
            if (waveProvider == null) throw new ArgumentNullException("waveProvider");
            if (salida != IntPtr.Zero) throw new InvalidOperationException("ya iniciada");
            fuente = waveProvider; formato = waveProvider.WaveFormat;
            int n = Math.Max(2, NumberOfBuffers);
            int ms = Math.Max(10, DesiredLatency / n);
            largoBloque = (int)((long)formato.AverageBytesPerSecond * ms / 1000);
            int alin = Math.Max(1, formato.BlockAlign);
            largoBloque = Math.Max(alin, largoBloque - largoBloque % alin);
            IntPtr h;
            int r;
            lock (cerrojo) r = so.Abre(out h, formato, aviso);
            if (r != 0) throw new MmException((MmResult)r, "waveOutOpen");
            salida = h;
            cabeceras = new IntPtr[n]; datos = new IntPtr[n]; lectura = new byte[largoBloque];
            try
            {
                for (int i = 0; i < n; i++)
                {
                    datos[i] = Marshal.AllocHGlobal(largoBloque);
                    cabeceras[i] = Marshal.AllocHGlobal(TamCabecera);
                    CabeceraWave c = new CabeceraWave(); c.Datos = datos[i]; c.Largo = largoBloque;
                    Marshal.StructureToPtr(c, cabeceras[i], false);
                    lock (cerrojo) r = so.Prepara(salida, cabeceras[i], TamCabecera);
                    if (r != 0) throw new MmException((MmResult)r, "waveOutPrepareHeader");
                }
            }
            catch { Libera(); throw; }
        }

        public void Play()
        {
            if (salida == IntPtr.Zero) throw new InvalidOperationException("sin iniciar");
            if (estado == PlaybackState.Playing) return;
            if (estado == PlaybackState.Paused)
            {
                int r; lock (cerrojo) r = so.Sigue(salida);
                if (r != 0) throw new MmException((MmResult)r, "waveOutRestart");
                estado = PlaybackState.Playing; aviso.Set();
                return;
            }
            estado = PlaybackState.Playing; parar = false; fin = false; Interlocked.Exchange(ref avisado, 0);
            if (hilo == null || !hilo.IsAlive)
            {
                hilo = new Thread(Bucle);
                hilo.IsBackground = true; hilo.Name = "audio (salida)";
                try { hilo.Priority = ThreadPriority.Highest; } catch { }
                hilo.Start();
            }
            aviso.Set();
        }

        public void Pause()
        {
            if (estado != PlaybackState.Playing || salida == IntPtr.Zero) return;
            int r; lock (cerrojo) r = so.Pausa(salida);
            if (r != 0) throw new MmException((MmResult)r, "waveOutPause");
            estado = PlaybackState.Paused;
        }

        public void Stop()
        {
            if (estado == PlaybackState.Stopped) return;
            estado = PlaybackState.Stopped;
            if (salida != IntPtr.Zero) lock (cerrojo) so.Reinicia(salida);   // devuelve todos los bloques; el hilo ya no los vuelve a llenar
            Avisa(null);
        }

        public long GetPosition()
        {
            if (salida == IntPtr.Zero) return 0;
            long b; int r;
            lock (cerrojo) { if (salida == IntPtr.Zero) return 0; r = so.Posicion(salida, out b, formato); }
            if (r != 0) throw new MmException((MmResult)r, "waveOutGetPosition");
            return b;
        }

        // EL HILO DE LA SALIDA: espera el aviso de Windows (o 100 ms) y rellena los bloques que ya sonaron, en orden.
        private void Bucle()
        {
            HilosRocola.Nombra("audio (salida)");   // para el reparto por hilo del diagnostico completo
            int siguiente = 0;
            bool[] escrito = new bool[cabeceras.Length];
            try
            {
                while (!parar)
                {
                    aviso.WaitOne(100, false);   // con el bool: WaitOne(Int32) solo no existe en el .NET del A4
                    if (parar) break;
                    if (estado != PlaybackState.Playing) continue;
                    // En orden: se escribieron 0, 1, 2... y en ese orden vuelven. Si el mas viejo aun suena, los de detras tambien.
                    for (int k = 0; k < cabeceras.Length && !parar && !fin && estado == PlaybackState.Playing; k++)
                    {
                        int i = siguiente;
                        if (escrito[i] && (Marshal.ReadInt32(cabeceras[i], OffBanderas) & Hecho) == 0) break;   // aun sonando
                        escrito[i] = false;
                        int n = Llena(i);
                        if (n <= 0) { fin = true; break; }
                        int r;
                        lock (cerrojo) { if (parar || estado != PlaybackState.Playing) break; r = so.Escribe(salida, cabeceras[i], TamCabecera); }
                        if (r != 0) throw new MmException((MmResult)r, "waveOutWrite");
                        escrito[i] = true;
                        siguiente = (i + 1) % cabeceras.Length;
                    }
                    int enCola = 0;
                    for (int i = 0; i < cabeceras.Length; i++)
                        if (escrito[i] && (Marshal.ReadInt32(cabeceras[i], OffBanderas) & Hecho) == 0) enCola++;
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
            Marshal.Copy(lectura, 0, datos[i], hechos);
            Marshal.WriteInt32(cabeceras[i], OffLargo, hechos);
            return hechos;
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
            if (salida != IntPtr.Zero) lock (cerrojo) so.Reinicia(salida);
            aviso.Set();
            Thread t = hilo; hilo = null;
            // Si el hilo no acaba en 2 s (una lectura de la fuente atorada), los bloques NO se liberan: aun puede copiar en ellos, y
            // liberarlos tumbaria la rocola. Se cierra la salida y se pierde esa poca memoria, que es lo de menos.
            bool suelto = t == null || object.ReferenceEquals(t, Thread.CurrentThread) || t.Join(2000);
            if (!suelto) { lock (cerrojo) { if (salida != IntPtr.Zero) { try { so.Cierra(salida); } catch { } salida = IntPtr.Zero; } } return; }
            Libera();
            aviso.Close();
        }

        private void Libera()
        {
            lock (cerrojo)
            {
                if (cabeceras != null)
                    for (int i = 0; i < cabeceras.Length; i++)
                    {
                        if (cabeceras[i] != IntPtr.Zero)
                        {
                            if (salida != IntPtr.Zero) try { so.Desprepara(salida, cabeceras[i], TamCabecera); } catch { }
                            Marshal.FreeHGlobal(cabeceras[i]); cabeceras[i] = IntPtr.Zero;
                        }
                        if (datos[i] != IntPtr.Zero) { Marshal.FreeHGlobal(datos[i]); datos[i] = IntPtr.Zero; }
                    }
                if (salida != IntPtr.Zero) { try { so.Cierra(salida); } catch { } salida = IntPtr.Zero; }
            }
        }
    }
}

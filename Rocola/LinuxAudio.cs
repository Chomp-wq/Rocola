// ====================================================================================================
//  LO QUE EN WINDOWS PONE EL SISTEMA Y EN LINUX HAY QUE PONER A MANO
// ----------------------------------------------------------------------------------------------------
//  La rocola entera -la biblioteca, el EQ, el reconstructor de agudos, el nivelado, el dibujado- no sabe
//  en que sistema esta: son cuentas sobre numeros. Lo unico que le pedia a Windows para SONAR eran dos
//  cosas, y son las dos que hay aqui:
//
//    Mp3Linux    descomprimir un MP3. En Windows lo hace el propio Windows (ACM). En Linux eso no existe,
//                asi que se usa NLayer, que descomprime en el mismo lenguaje que la rocola y no necesita
//                nada del sistema. Es la MISMA cuenta que hace cualquier reproductor: un MP3 no se
//                descomprime "de dos maneras", se descomprime como dice la norma.
//
//    SalidaAlsa  sacar el sonido por la tarjeta. En Windows es WaveOut o WASAPI; en Linux es ALSA, que es
//                lo que trae cualquier Linux -Puppy incluido- sin instalar nada.
//
//  Todo lo demas de la cadena de sonido (el DSEE, el EQ automatico, el nivelado, el analizador que mueve
//  el fondo de estrellas) se queda exactamente igual: trabaja sobre muestras y no le importa de donde
//  vienen ni a donde van.
// ====================================================================================================
using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // -------------------------------------------------------------------------------------------------
    //  DESCOMPRIMIR UN MP3 SIN PEDIRSELO A WINDOWS
    // -------------------------------------------------------------------------------------------------
    // NLayer trae su propio lector de archivo (MpegFile), que es su puerta de entrada de siempre: abre el
    // archivo, salta las etiquetas, arma la tabla de tramas y entrega muestras. Se usa ESA y no una pieza
    // suelta a medida, justo porque lo que suena en un bar no es sitio para un descompresor casero.
    internal sealed class Mp3Linux : IDisposable
    {
        private readonly NLayer.MpegFile arch;
        private readonly WaveFormat formato;

        public Mp3Linux(string ruta) : this(new NLayer.MpegFile(ruta)) { }
        public Mp3Linux(System.IO.Stream flujo) : this(new NLayer.MpegFile(flujo)) { }
        private Mp3Linux(NLayer.MpegFile abierto)
        {
            arch = abierto;
            // Muestras en coma flotante, que es lo que la cadena de la rocola espera de punta a punta.
            formato = WaveFormat.CreateIeeeFloatWaveFormat(arch.SampleRate, arch.Channels);
        }

        public WaveFormat WaveFormat { get { return formato; } }
        public TimeSpan TotalTime { get { return arch.Duration; } }
        public TimeSpan CurrentTime { get { return arch.Time; } set { arch.Time = value; } }
        public int Read(float[] buffer, int offset, int count) { return arch.ReadSamples(buffer, offset, count); }
        public void Dispose() { try { arch.Dispose(); } catch { } }
    }

    // -------------------------------------------------------------------------------------------------
    //  SACAR EL SONIDO POR LA TARJETA (ALSA)
    // -------------------------------------------------------------------------------------------------
    // La rocola le pide muy poco a su salida: preparate con esta cadena, empieza, para, y avisame cuando
    // se acabe la cancion. Eso ultimo es lo importante: el aviso es lo que hace que entre la siguiente.
    internal sealed class SalidaAlsa : IWavePlayer
    {
        private const string LIB = "libasound.so.2";
        private const int STREAM_PLAYBACK = 0;
        private const int FORMAT_S16_LE = 2;
        private const int ACCESS_RW_INTERLEAVED = 3;

        [DllImport(LIB)] private static extern int snd_pcm_open(out IntPtr pcm, string nombre, int flujo, int modo);
        [DllImport(LIB)] private static extern int snd_pcm_set_params(IntPtr pcm, int formato, int acceso,
                                                                     uint canales, uint frecuencia, int remuestreo, uint latenciaUs);
        [DllImport(LIB)] private static extern int snd_pcm_get_params(IntPtr pcm, out ulong tamBuffer, out ulong tamPeriodo);
        [DllImport(LIB)] private static extern IntPtr snd_pcm_writei(IntPtr pcm, byte[] datos, ulong tramas);
        [DllImport(LIB)] private static extern int snd_pcm_recover(IntPtr pcm, int err, int callado);
        [DllImport(LIB)] private static extern int snd_pcm_prepare(IntPtr pcm);
        [DllImport(LIB)] private static extern int snd_pcm_drain(IntPtr pcm);
        [DllImport(LIB)] private static extern int snd_pcm_drop(IntPtr pcm);
        [DllImport(LIB)] private static extern int snd_pcm_close(IntPtr pcm);
        [DllImport(LIB)] private static extern IntPtr snd_strerror(int err);

        private static string Error(int e)
        {
            try { IntPtr p = snd_strerror(e); return (p == IntPtr.Zero) ? e.ToString() : Marshal.PtrToStringAnsi(p); }
            catch { return e.ToString(); }
        }

        private IntPtr pcm = IntPtr.Zero;
        private IWaveProvider fuente;
        private Thread hilo;
        private volatile bool corriendo, pausada;
        private PlaybackState estado = PlaybackState.Stopped;
        private float volumen = 1.0f;
        private readonly uint latenciaPedidaUs;
        private int retardoMs = 300;
        private readonly string dispositivo;

        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        // 'latenciaMs' es el colchon: cuanto sonido va por delante de lo que se esta calculando. Mas colchon
        // = mas dificil que se corte si la maquina se distrae, pero mas tarde llega la imagen al sonido.
        public SalidaAlsa(int latenciaMs) : this(latenciaMs, "default") { }
        public SalidaAlsa(int latenciaMs, string disp)
        {
            if (latenciaMs < 40) latenciaMs = 40;
            latenciaPedidaUs = (uint)(latenciaMs * 1000);
            dispositivo = string.IsNullOrEmpty(disp) ? "default" : disp;
        }

        // El colchon REAL que dio la tarjeta, en milisegundos. La rocola lo usa para alinear el latido del
        // fondo de estrellas con lo que se esta oyendo: sin esto, el fondo se mueve antes que la musica.
        public int RetardoMs { get { return retardoMs; } }

        public PlaybackState PlaybackState { get { return estado; } }

        public float Volume
        {
            get { return volumen; }
            set { volumen = (value < 0f) ? 0f : ((value > 1f) ? 1f : value); }
        }

        public void Init(IWaveProvider proveedor)
        {
            if (proveedor == null) throw new ArgumentNullException("proveedor");
            WaveFormat wf = proveedor.WaveFormat;
            if (wf.BitsPerSample != 16 || wf.Encoding != WaveFormatEncoding.Pcm)
                throw new NotSupportedException("La salida de Linux espera PCM de 16 bits (es lo que entrega la cadena de la rocola).");
            fuente = proveedor;

            int r = snd_pcm_open(out pcm, dispositivo, STREAM_PLAYBACK, 0);
            if (r < 0) throw new Exception("No se pudo abrir la tarjeta de sonido (" + dispositivo + "): " + Error(r));
            r = snd_pcm_set_params(pcm, FORMAT_S16_LE, ACCESS_RW_INTERLEAVED,
                                   (uint)wf.Channels, (uint)wf.SampleRate, 1, latenciaPedidaUs);
            if (r < 0) { snd_pcm_close(pcm); pcm = IntPtr.Zero; throw new Exception("La tarjeta no acepta este formato: " + Error(r)); }

            ulong buf, periodo;
            if (snd_pcm_get_params(pcm, out buf, out periodo) >= 0 && wf.SampleRate > 0)
                retardoMs = (int)(buf * 1000UL / (ulong)wf.SampleRate);
        }

        public void Play()
        {
            if (pcm == IntPtr.Zero) throw new InvalidOperationException("Hay que preparar la salida antes de arrancarla.");
            pausada = false;
            if (estado == PlaybackState.Playing) return;
            estado = PlaybackState.Playing;
            if (hilo == null)
            {
                corriendo = true;
                hilo = new Thread(Bombea);
                hilo.IsBackground = true;
                hilo.Priority = ThreadPriority.AboveNormal;   // el sonido antes que el dibujado: un corte se oye, un cuadro no
                hilo.Start();
            }
        }

        public void Pause() { pausada = true; estado = PlaybackState.Paused; }

        public void Stop()
        {
            corriendo = false;
            Thread h = hilo; hilo = null;
            if (h != null) { try { h.Join(1500); } catch { } }
            if (pcm != IntPtr.Zero) { try { snd_pcm_drop(pcm); } catch { } }
            estado = PlaybackState.Stopped;
        }

        // EL HILO QUE DA DE COMER A LA TARJETA. Pide muestras a la cadena de la rocola y las va entregando.
        // Cuando la cadena dice que ya no hay mas, avisa: ese aviso es lo que mete la siguiente cancion.
        private void Bombea()
        {
            Exception murio = null;
            try
            {
                WaveFormat wf = fuente.WaveFormat;
                int bytesPorTrama = wf.Channels * 2;
                int tramasPorVuelta = Math.Max(256, wf.SampleRate / 20);       // ~50 ms por vuelta
                byte[] tanda = new byte[tramasPorVuelta * bytesPorTrama];
                while (corriendo)
                {
                    if (pausada) { Thread.Sleep(10); continue; }
                    int leidos = fuente.Read(tanda, 0, tanda.Length);
                    if (leidos <= 0) break;                                     // se acabo la cancion
                    if (leidos < tanda.Length) Array.Clear(tanda, leidos, tanda.Length - leidos);
                    if (volumen < 0.999f) Atenua(tanda, leidos);
                    int tramas = leidos / bytesPorTrama;
                    if (!Entrega(tanda, tramas)) break;
                }
                if (corriendo && pcm != IntPtr.Zero) { try { snd_pcm_drain(pcm); } catch { } }   // que suene lo que ya se entrego
            }
            catch (Exception ex) { murio = ex; }
            estado = PlaybackState.Stopped;
            EventHandler<StoppedEventArgs> av = PlaybackStopped;
            if (av != null) { try { av(this, new StoppedEventArgs(murio)); } catch { } }
        }

        // Entrega una tanda entera, aunque la tarjeta la acepte a trozos. Si se queda sin sonido -la maquina
        // se distrajo- se recupera y sigue en vez de morirse: un tropiezo se oye una vez, morirse apaga la
        // musica hasta que alguien lo note.
        private bool Entrega(byte[] datos, int tramas)
        {
            int hechas = 0, bytesPorTrama = datos.Length / Math.Max(1, tramas);
            byte[] resto = null;
            while (hechas < tramas && corriendo)
            {
                byte[] aMandar = datos;
                int quedan = tramas - hechas;
                if (hechas > 0)
                {
                    if (resto == null || resto.Length < quedan * bytesPorTrama) resto = new byte[quedan * bytesPorTrama];
                    Buffer.BlockCopy(datos, hechas * bytesPorTrama, resto, 0, quedan * bytesPorTrama);
                    aMandar = resto;
                }
                long n = snd_pcm_writei(pcm, aMandar, (ulong)quedan).ToInt64();
                if (n < 0)
                {
                    int r = snd_pcm_recover(pcm, (int)n, 1);
                    if (r < 0) { snd_pcm_prepare(pcm); }
                    continue;
                }
                if (n == 0) { Thread.Sleep(1); continue; }
                hechas += (int)n;
            }
            return hechas >= tramas;
        }

        // Bajarle el volumen a la tanda. La rocola no lo usa -deja la salida a tope y nivela antes, al
        // medir el volumen real de cada cancion-, pero la salida tiene que cumplir lo que promete.
        private void Atenua(byte[] d, int n)
        {
            float v = volumen;
            for (int i = 0; i + 1 < n; i += 2)
            {
                int s = (short)(d[i] | (d[i + 1] << 8));
                int r = (int)(s * v);
                if (r > 32767) r = 32767; else if (r < -32768) r = -32768;
                d[i] = (byte)(r & 0xFF); d[i + 1] = (byte)((r >> 8) & 0xFF);
            }
        }

        public void Dispose()
        {
            Stop();
            if (pcm != IntPtr.Zero) { try { snd_pcm_close(pcm); } catch { } pcm = IntPtr.Zero; }
        }
    }
}

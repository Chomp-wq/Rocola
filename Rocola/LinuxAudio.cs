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
        // LOS FORMATOS QUE SE LE PUEDEN OFRECER A LA TARJETA, del mejor al que seguro acepta.
        //
        // Hasta ahora siempre se mandaban 16 bits, que es lo que habia que hacer en Windows XP. Para eso
        // hay que anadir un ruido diminuto a proposito -el dither- que tapa la aspereza de recortar a 16
        // bits en los pasajes callados. Casi cualquier tarjeta de los ultimos veinte anos acepta 24 o 32,
        // y ahi ese ruido SOBRA: se manda la cuenta tal como sale de la cadena y no hay nada que tapar.
        private const int FORMAT_S16_LE = 2;
        private const int FORMAT_S24_LE = 6;    // 4 bytes, 24 bits utiles abajo
        private const int FORMAT_S32_LE = 10;
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

        // ================== QUE LA MUSICA NO SE CORTE ==================
        //
        // LOS TROPIEZOS. Cuando la tarjeta se queda sin muestras -la maquina se distrajo leyendo la
        // biblioteca, decodificando una portada- el sonido da un tropiezo: un instante de silencio o un
        // chasquido. Abajo, en Entrega, eso se recupera y la musica sigue, que es lo correcto. Pero antes
        // no se contaba, y eso es peor de lo que parece: la rocola podia llevar meses cortandose en el bar
        // y no habia forma de saberlo, ni de saber si algo lo mejoraba. Ahora se cuentan y se ensenan en
        // Ctrl+F3. Un numero en pantalla convierte "a veces se oye raro" en algo que se puede perseguir.
        private int tropiezos;
        private DateTime ultimoTropiezo = DateTime.MinValue;
        public int Tropiezos { get { return tropiezos; } }
        public DateTime UltimoTropiezo { get { return ultimoTropiezo; } }

        // LA PRIORIDAD DE TIEMPO REAL. El hilo que le da de comer a la tarjeta pedia "prioridad alta" con
        // las palabras de Windows (ThreadPriority.AboveNormal). En Linux eso se traduce a "nice", que sirve
        // para que un programa no estorbe a otro pero NO para sonido: el planificador puede dejar al hilo
        // esperando su turno mientras el dibujado o el disco trabajan, y ese turno perdido es justo el
        // tropiezo de arriba.
        //
        // Linux tiene lo que hace falta: SCHED_FIFO, que quiere decir "a este hilo se le atiende ANTES que
        // a los demas". Normalmente hace falta permiso especial y por eso en un equipo de escritorio no se
        // puede; en Puppy la rocola corre como root, asi que si.
        //
        // Se pide prioridad MODERADA (10 de 99) a proposito. El hilo se pasa la vida dormido esperando a
        // que la tarjeta le pida mas sonido, asi que no puede acaparar la maquina; y si algo saliera mal,
        // con 10 el sistema sigue respondiendo, mientras que con 99 se quedaria tieso.
        //
        // Si el sistema no la concede se sigue como antes y se DICE en Ctrl+F3, en vez de prometer algo
        // que no esta.
        [StructLayout(LayoutKind.Sequential)] private struct ParamHorario { public int prioridad; }
        [DllImport("libc", SetLastError = true)] private static extern IntPtr pthread_self();
        [DllImport("libc", SetLastError = true)] private static extern int pthread_setschedparam(IntPtr hilo, int politica, ref ParamHorario p);
        private const int SCHED_FIFO = 1;
        private const int PrioridadSonido = 10;

        private static bool tiempoReal;
        public static bool EnTiempoReal { get { return tiempoReal; } }

        internal static bool PideTiempoReal()
        {
            try
            {
                ParamHorario p = new ParamHorario();
                p.prioridad = PrioridadSonido;
                bool ok = pthread_setschedparam(pthread_self(), SCHED_FIFO, ref p) == 0;
                if (ok) tiempoReal = true;
                return ok;
            }
            catch { return false; }
        }

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

        // Preparar la salida con una cadena que YA viene en 16 bits. Es como se hacia siempre y se deja
        // igual: hay codigo -y pruebas- que entregan sus muestras ya hechas y no tienen por que cambiar.
        public void Init(IWaveProvider proveedor)
        {
            if (proveedor == null) throw new ArgumentNullException("proveedor");
            WaveFormat wf = proveedor.WaveFormat;
            if (wf.BitsPerSample != 16 || wf.Encoding != WaveFormatEncoding.Pcm)
                throw new NotSupportedException("La salida de Linux espera PCM de 16 bits (es lo que entrega la cadena de la rocola).");
            fuente = proveedor;
            Abre(wf.Channels, wf.SampleRate, new int[] { FORMAT_S16_LE });
        }

        // PREPARAR LA SALIDA CON LA CADENA TAL COMO VIAJA POR DENTRO DE LA ROCOLA (coma flotante).
        //
        // Asi la salida puede NEGOCIAR con la tarjeta el mejor formato que acepte -32, luego 24, luego
        // 16- y hacer ella misma la conversion, con dither SOLO si acaba en 16 bits. Antes la cadena
        // recortaba a 16 antes de llegar aqui y esa decision ya no habia forma de deshacerla.
        public void Init(ISampleProvider fuenteFlotante)
        {
            if (fuenteFlotante == null) throw new ArgumentNullException("fuenteFlotante");
            WaveFormat wf = fuenteFlotante.WaveFormat;
            flotante = fuenteFlotante;
            fuente = null;
            Abre(wf.Channels, wf.SampleRate, new int[] { FORMAT_S32_LE, FORMAT_S24_LE, FORMAT_S16_LE });
        }

        // Abre la tarjeta probando los formatos en orden y se queda con el primero que acepte. Cada
        // intento abre y cierra: un intento fallido puede dejar la tarjeta a medio configurar, y arrancar
        // el siguiente sobre eso daria errores que no tienen nada que ver.
        private void Abre(int canales, int frecuencia, int[] candidatos)
        {
            string ultimo = "";
            for (int i = 0; i < candidatos.Length; i++)
            {
                int r = snd_pcm_open(out pcm, dispositivo, STREAM_PLAYBACK, 0);
                if (r < 0) throw new Exception("No se pudo abrir la tarjeta de sonido (" + dispositivo + "): " + Error(r));
                r = snd_pcm_set_params(pcm, candidatos[i], ACCESS_RW_INTERLEAVED,
                                       (uint)canales, (uint)frecuencia, 1, latenciaPedidaUs);
                if (r >= 0)
                {
                    formato = candidatos[i];
                    bytesPorMuestra = (formato == FORMAT_S16_LE) ? 2 : 4;
                    ulong buf, periodo;
                    if (snd_pcm_get_params(pcm, out buf, out periodo) >= 0 && frecuencia > 0)
                        retardoMs = (int)(buf * 1000UL / (ulong)frecuencia);
                    return;
                }
                ultimo = Error(r);
                snd_pcm_close(pcm); pcm = IntPtr.Zero;
            }
            throw new Exception("La tarjeta no acepta ningun formato de los que sabe mandar la rocola: " + ultimo);
        }

        private ISampleProvider flotante;
        private int formato = FORMAT_S16_LE;
        private int bytesPorMuestra = 2;
        // Cuantos bits se le estan mandando de verdad a la tarjeta, para poder ensenarlo en Ctrl+F3.
        public int BitsALaTarjeta { get { return (formato == FORMAT_S16_LE) ? 16 : (formato == FORMAT_S24_LE ? 24 : 32); } }

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
                hilo.Priority = ThreadPriority.AboveNormal;   // se deja por si acaso, pero lo que de verdad manda es el SCHED_FIFO que pide el propio hilo
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
            // Lo PRIMERO que hace este hilo: pedir que se le atienda antes que a nadie (ver arriba). Va
            // aqui dentro y no al crearlo porque la prioridad es de cada hilo, no del programa: pedirla
            // desde fuera se la pondria al hilo equivocado.
            PideTiempoReal();

            Exception murio = null;
            try
            {
                WaveFormat wf = (fuente != null) ? fuente.WaveFormat : flotante.WaveFormat;
                int bytesPorTrama = wf.Channels * bytesPorMuestra;
                int tramasPorVuelta = Math.Max(256, wf.SampleRate / 20);       // ~50 ms por vuelta
                byte[] tanda = new byte[tramasPorVuelta * bytesPorTrama];
                float[] flo = (flotante != null) ? new float[tramasPorVuelta * wf.Channels] : null;
                while (corriendo)
                {
                    if (pausada) { Thread.Sleep(10); continue; }
                    int tramas;
                    if (flotante != null)
                    {
                        int muestras = flotante.Read(flo, 0, flo.Length);
                        if (muestras <= 0) break;                               // se acabo la cancion
                        if (volumen < 0.999f) for (int i = 0; i < muestras; i++) flo[i] *= volumen;
                        Convierte(flo, muestras, tanda);
                        tramas = muestras / wf.Channels;
                    }
                    else
                    {
                        int leidos = fuente.Read(tanda, 0, tanda.Length);
                        if (leidos <= 0) break;                                 // se acabo la cancion
                        if (leidos < tanda.Length) Array.Clear(tanda, leidos, tanda.Length - leidos);
                        if (volumen < 0.999f) Atenua(tanda, leidos);
                        tramas = leidos / bytesPorTrama;
                    }
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
                    // UN TROPIEZO. Se cuenta antes de recuperarse: es el unico rastro que queda de que la
                    // musica se corto un instante, y sin el nadie se enteraria nunca (ver arriba).
                    tropiezos++;
                    ultimoTropiezo = DateTime.Now;
                    int r = snd_pcm_recover(pcm, (int)n, 1);
                    if (r < 0) { snd_pcm_prepare(pcm); }
                    continue;
                }
                if (n == 0) { Thread.Sleep(1); continue; }
                hechas += (int)n;
            }
            return hechas >= tramas;
        }

        // DE LA CUENTA DE LA ROCOLA A LOS BYTES QUE ESPERA LA TARJETA.
        //
        // Por dentro la rocola trabaja en coma flotante, donde el silencio es 0 y el maximo es 1. La
        // tarjeta quiere numeros enteros, y cuantos mas bits tenga ese entero, mas fino se le puede
        // contar lo que hay entre uno y otro.
        //
        // EL DITHER, Y POR QUE SOLO EN 16 BITS. Al pasar de la cuenta fina a un entero hay que redondear,
        // y redondear siempre igual crea un error que SIGUE a la musica en vez de ser ruido: se oye como
        // una aspereza en los pasajes callados. El truco de toda la vida es sumar un ruido diminuto antes
        // de redondear, y entonces el error se vuelve un siseo parejo que el oido no persigue. Eso hace
        // falta con 16 bits. Con 24 o 32 el escalon del redondeo esta tan abajo que queda por debajo del
        // ruido de la propia tarjeta y del bar entero: anadir ruido ahi seria empeorar el sonido a cambio
        // de nada, asi que no se hace.
        private uint rs;   // estado del ruido (xorshift32, mas barato que System.Random)
        private float Ruido() { if (rs == 0) rs = (uint)Environment.TickCount | 1u; rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5; return (rs >> 8) * (1f / 16777216f); }

        private void Convierte(float[] src, int n, byte[] dst)
        {
            int o = 0;
            if (formato == FORMAT_S16_LE)
            {
                for (int i = 0; i < n; i++)
                {
                    float d = Ruido() - Ruido();                   // triangular en (-1..1) del ultimo bit
                    int q = (int)Math.Floor(src[i] * 32767f + d + 0.5f);
                    if (q > 32767) q = 32767; else if (q < -32768) q = -32768;
                    dst[o++] = (byte)q; dst[o++] = (byte)(q >> 8);
                }
            }
            else if (formato == FORMAT_S24_LE)
            {
                // 24 bits utiles dentro de 4 bytes, alineados abajo (es lo que quiere decir S24_LE).
                //
                // La cuenta va en DOBLE precision, no en simple. Un numero de coma flotante normal guarda
                // 24 bits de cifras, justo los que hay que calcular aqui: multiplicando en simple, el
                // resultado no cabe entero y la ultima cifra se redondea sola antes de que la redondeemos
                // nosotros. Se veia: la peor muestra salia un escalon movida. Con 16 bits no pasaba
                // -sobra sitio- y por eso el fallo solo asomaba en este camino.
                for (int i = 0; i < n; i++)
                {
                    double x = Math.Floor((double)src[i] * 8388607.0 + 0.5);
                    if (x > 8388607.0) x = 8388607.0; else if (x < -8388608.0) x = -8388608.0;
                    int q = (int)x;
                    dst[o++] = (byte)q; dst[o++] = (byte)(q >> 8); dst[o++] = (byte)(q >> 16); dst[o++] = (byte)(q >> 24);
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    double q = Math.Floor((double)src[i] * 2147483647.0 + 0.5);
                    if (q > 2147483647.0) q = 2147483647.0; else if (q < -2147483648.0) q = -2147483648.0;
                    int v = (int)q;
                    dst[o++] = (byte)v; dst[o++] = (byte)(v >> 8); dst[o++] = (byte)(v >> 16); dst[o++] = (byte)(v >> 24);
                }
            }
            if (o < dst.Length) Array.Clear(dst, o, dst.Length - o);
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

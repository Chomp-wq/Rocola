// LA SALIDA DE SONIDO DE LA ROCOLA DE XP, EN ALSA (traduccion de XP, fase 1). En XP, WaveOutPropio (SalidaPropia.cs) habla con Windows por
// una interfaz (ISistemaWaveOut): la de Windows llama a winmm.dll. Aqui, la misma interfaz con ALSA (libasound): cada bloque que la
// rocola escribe (una WAVEHDR en memoria) se pasa a la tarjeta en orden desde un hilo, y se marca HECHO (WHDR_DONE) cuando ya sono (lo
// escrito menos lo que la tarjeta aun tiene por delante), con el mismo aviso (el AutoResetEvent). La posicion es lo que ya sono, en bytes,
// desde el ultimo Reinicia, como waveOutGetPosition. Los errores son los de winmm (MmResult), para que la rocola los trate como en XP.
// La tarjeta: la misma eleccion que el motor de Rocola OS (Motor/SalidaAlsa.cs): ROCOLA_ALSA si esta puesta (la pone /usr/bin/rocola con
// rocola-tarjeta) y si no, la primera salida que no sea HDMI, directa (plughw).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // LOS ERRORES DE LA SALIDA DE WINDOWS (MMSYSERR de winmm), con los mismos numeros: la rocola de XP decide con ellos si el fallo es del
    // aparato (SalidaSonido.cs, EsFalloDelDispositivo) o del formato (EsFormatoRechazado).
    public enum MmResult
    {
        NoError = 0, UnspecifiedError = 1, BadDeviceId = 2, NotEnabled = 3, AlreadyAllocated = 4, InvalidHandle = 5, NoDriver = 6,
        MemoryAllocationError = 7, NotSupported = 8, BadErrorNumber = 9, InvalidFlag = 10, InvalidParameter = 11, HandleBusy = 12,
        InvalidAlias = 13, BadRegistryDatabase = 14, RegistryKeyNotFound = 15, RegistryReadError = 16, RegistryWriteError = 17,
        RegistryDeleteError = 18, RegistryValueNotFound = 19, NoDriverCallback = 20, MoreData = 21,
        WaveBadFormat = 32, WaveStillPlaying = 33, WaveHeaderUnprepared = 34, WaveSync = 35,
    }
    public class MmException : Exception
    {
        public MmResult Result { get; private set; }
        public string Function { get; private set; }
        public MmException(MmResult result, string function) : base(result + " llamando a " + function) { Result = result; Function = function; }
    }

    internal sealed unsafe class AlsaWaveOut : ISistemaWaveOut
    {
        private const string Lib = "libasound.so.2";
        [DllImport(Lib)] private static extern int snd_pcm_open(out IntPtr pcm, string nombre, int stream, int modo);
        [DllImport(Lib)] private static extern int snd_pcm_set_params(IntPtr pcm, int formato, int acceso, uint canales, uint tasa, int remuestreo, uint latenciaUs);
        [DllImport(Lib)] private static extern long snd_pcm_writei(IntPtr pcm, byte* buffer, ulong cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_recover(IntPtr pcm, int error, int silencioso);
        [DllImport(Lib)] private static extern int snd_pcm_drop(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_prepare(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_pause(IntPtr pcm, int activa);
        [DllImport(Lib)] private static extern int snd_pcm_close(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_delay(IntPtr pcm, out long cuadros);
        private const int Reproducir = 0, AccesoIntercalado = 3, ENOENT = -2, EBUSY = -16, ENODEV = -19, EINVAL = -22, EPIPE = -32;
        private const int S16_LE = 2, S24_3LE = 32, S32_LE = 10, FLOAT_LE = 14, U8 = 1;
        private const int Hecho = 0x1, Preparada = 0x2, EnCola = 0x10;   // WHDR_DONE, WHDR_PREPARED, WHDR_INQUEUE
        private static readonly int OffDatos = (int)Marshal.OffsetOf(typeof(CabeceraWave), "Datos");
        private static readonly int OffLargo = (int)Marshal.OffsetOf(typeof(CabeceraWave), "Largo");
        private static readonly int OffBanderas = (int)Marshal.OffsetOf(typeof(CabeceraWave), "Banderas");

        // Una salida abierta (el IntPtr que la rocola guarda es su numero en la tabla)
        private sealed class Abierta
        {
            public IntPtr Pcm; public WaveFormat Formato; public AutoResetEvent Aviso;
            public readonly Queue<IntPtr> PorEscribir = new Queue<IntPtr>();
            public readonly List<KeyValuePair<IntPtr, long>> Sonando = new List<KeyValuePair<IntPtr, long>>();   // cabecera y donde acaba (cuadros)
            public long Escritos;   // cuadros dados a la tarjeta desde el ultimo Reinicia
            public bool Pausada, Cerrar;
            public Thread Hilo;
            public readonly object Cerrojo = new object();
            public readonly AutoResetEvent Hay = new AutoResetEvent(false);
        }
        private static readonly Dictionary<long, Abierta> abiertas = new Dictionary<long, Abierta>();
        private static long ultima;
        private static Abierta De(IntPtr h) { lock (abiertas) { Abierta a; return abiertas.TryGetValue(h.ToInt64(), out a) ? a : null; } }

        public int Abre(out IntPtr salida, WaveFormat formato, AutoResetEvent aviso)
        {
            salida = IntPtr.Zero;
            int fmt = FormatoAlsa(formato);
            if (fmt < 0) return (int)MmResult.WaveBadFormat;
            IntPtr pcm;
            int r;
            try { r = snd_pcm_open(out pcm, Elige(), Reproducir, 0); }
            catch (DllNotFoundException) { return (int)MmResult.NoDriver; }
            for (int espera = 0; espera < 15 && r == EBUSY; espera++) { Thread.Sleep(100); r = snd_pcm_open(out pcm, Elige(), Reproducir, 0); }
            if (r < 0) return r == EBUSY ? (int)MmResult.AlreadyAllocated : (int)MmResult.BadDeviceId;
            // 100 ms en la tarjeta: los bloques de la rocola (su latencia de 750-1000 ms) esperan en la cola de aqui, como en winmm
            r = snd_pcm_set_params(pcm, fmt, AccesoIntercalado, (uint)formato.Channels, (uint)formato.SampleRate, 1, 100000);
            if (r < 0) { snd_pcm_close(pcm); return r == EINVAL ? (int)MmResult.WaveBadFormat : (int)MmResult.NotSupported; }
            Abierta a = new Abierta { Pcm = pcm, Formato = formato, Aviso = aviso };
            long id;
            lock (abiertas) { id = ++ultima; abiertas[id] = a; }
            a.Hilo = new Thread(delegate () { Bucle(a); }) { IsBackground = true, Name = "audio (alsa)" };
            try { a.Hilo.Priority = ThreadPriority.Highest; } catch { }
            a.Hilo.Start();
            salida = new IntPtr(id);
            return 0;
        }
        private static int FormatoAlsa(WaveFormat f)
        {
            int bits = f.BitsPerSample;
            WaveFormatEncoding e = f.Encoding;
            if (e == WaveFormatEncoding.Extensible) e = bits == 32 && f is WaveFormatExtensible && ((WaveFormatExtensible)f).SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71") ? WaveFormatEncoding.IeeeFloat : WaveFormatEncoding.Pcm;
            if (e == WaveFormatEncoding.IeeeFloat && bits == 32) return FLOAT_LE;
            if (e != WaveFormatEncoding.Pcm) return -1;
            switch (bits) { case 8: return U8; case 16: return S16_LE; case 24: return S24_3LE; case 32: return S32_LE; }
            return -1;
        }

        public int Prepara(IntPtr salida, IntPtr cabecera, int tamano)
        {
            if (De(salida) == null) return (int)MmResult.InvalidHandle;
            Marshal.WriteInt32(cabecera, OffBanderas, (Marshal.ReadInt32(cabecera, OffBanderas) | Preparada) & ~EnCola);
            return 0;
        }
        public int Escribe(IntPtr salida, IntPtr cabecera, int tamano)
        {
            Abierta a = De(salida);
            if (a == null) return (int)MmResult.InvalidHandle;
            int b = Marshal.ReadInt32(cabecera, OffBanderas);
            if ((b & Preparada) == 0) return (int)MmResult.WaveHeaderUnprepared;
            lock (a.Cerrojo)
            {
                Marshal.WriteInt32(cabecera, OffBanderas, (b & ~Hecho) | EnCola);   // como waveOutWrite: ya no esta hecha, esta en la cola
                a.PorEscribir.Enqueue(cabecera);
            }
            a.Hay.Set();
            return 0;
        }
        public int Desprepara(IntPtr salida, IntPtr cabecera, int tamano)
        {
            int b = Marshal.ReadInt32(cabecera, OffBanderas);
            if ((b & EnCola) != 0) return (int)MmResult.WaveStillPlaying;
            Marshal.WriteInt32(cabecera, OffBanderas, b & ~Preparada);
            return 0;
        }
        // waveOutReset: lo que no sono se tira, todos los bloques vuelven hechos y la posicion vuelve a 0
        public int Reinicia(IntPtr salida)
        {
            Abierta a = De(salida);
            if (a == null) return (int)MmResult.InvalidHandle;
            lock (a.Cerrojo)
            {
                snd_pcm_drop(a.Pcm); snd_pcm_prepare(a.Pcm);
                while (a.PorEscribir.Count > 0) Termina(a.PorEscribir.Dequeue());
                foreach (KeyValuePair<IntPtr, long> s in a.Sonando) Termina(s.Key);
                a.Sonando.Clear();
                a.Escritos = 0; a.Pausada = false;
            }
            a.Aviso.Set();
            return 0;
        }
        public int Pausa(IntPtr salida)
        {
            Abierta a = De(salida);
            if (a == null) return (int)MmResult.InvalidHandle;
            lock (a.Cerrojo) { a.Pausada = true; snd_pcm_pause(a.Pcm, 1); }
            return 0;
        }
        public int Sigue(IntPtr salida)
        {
            Abierta a = De(salida);
            if (a == null) return (int)MmResult.InvalidHandle;
            lock (a.Cerrojo) { if (a.Pausada && snd_pcm_pause(a.Pcm, 0) < 0) snd_pcm_prepare(a.Pcm); a.Pausada = false; }
            a.Hay.Set();
            return 0;
        }
        public int Cierra(IntPtr salida)
        {
            Abierta a = De(salida);
            if (a == null) return (int)MmResult.InvalidHandle;
            a.Cerrar = true; a.Hay.Set();
            if (a.Hilo != null && a.Hilo != Thread.CurrentThread) a.Hilo.Join(2000);
            lock (a.Cerrojo) { snd_pcm_drop(a.Pcm); snd_pcm_close(a.Pcm); a.Pcm = IntPtr.Zero; }
            lock (abiertas) abiertas.Remove(salida.ToInt64());
            return 0;
        }
        public int Posicion(IntPtr salida, out long bytes, WaveFormat formato)
        {
            bytes = 0;
            Abierta a = De(salida);
            if (a == null) return (int)MmResult.InvalidHandle;
            lock (a.Cerrojo) bytes = Sonado(a) * a.Formato.BlockAlign;
            return 0;
        }
        // lo que ya sono (cuadros): lo escrito menos lo que la tarjeta aun tiene por delante
        private static long Sonado(Abierta a)
        {
            long retraso;
            if (a.Pcm == IntPtr.Zero || snd_pcm_delay(a.Pcm, out retraso) < 0) retraso = 0;
            return Math.Max(0, a.Escritos - Math.Max(0, retraso));
        }
        private static void Termina(IntPtr cabecera)
        {
            int b = Marshal.ReadInt32(cabecera, OffBanderas);
            Marshal.WriteInt32(cabecera, OffBanderas, (b | Hecho) & ~EnCola);
        }

        // EL HILO: da los bloques a la tarjeta en orden (la escritura espera a que haya sitio: es el reloj) y marca hechos los que ya sonaron
        private static void Bucle(Abierta a)
        {
            while (!a.Cerrar)
            {
                IntPtr c = IntPtr.Zero;
                lock (a.Cerrojo)
                {
                    if (!a.Pausada && a.PorEscribir.Count > 0) c = a.PorEscribir.Dequeue();
                    MarcaSonados(a);
                }
                if (c == IntPtr.Zero) { a.Hay.WaitOne(10); continue; }
                IntPtr datos = Marshal.ReadIntPtr(c, OffDatos);
                int largo = Marshal.ReadInt32(c, OffLargo), alin = Math.Max(1, a.Formato.BlockAlign);
                long cuadros = largo / alin, hechos = 0;
                while (hechos < cuadros && !a.Cerrar)
                {
                    long r;
                    lock (a.Cerrojo)
                    {
                        if (a.Pcm == IntPtr.Zero) break;
                        if (a.Pausada) r = 0;
                        else
                        {
                            // sin bloquear el cerrojo mucho: como mucho 20 ms por vuelta
                            long tanda = Math.Min(cuadros - hechos, Math.Max(1, a.Formato.SampleRate / 50));
                            r = snd_pcm_writei(a.Pcm, (byte*)datos + hechos * alin, (ulong)tanda);
                            if (r == EPIPE || r == -11 /* EAGAIN */ || r < 0) { if (r != -11) snd_pcm_recover(a.Pcm, (int)r, 1); r = 0; }
                            hechos += r; a.Escritos += r;
                        }
                    }
                    if (r == 0) Thread.Sleep(2);
                }
                lock (a.Cerrojo) { a.Sonando.Add(new KeyValuePair<IntPtr, long>(c, a.Escritos)); MarcaSonados(a); }
            }
        }
        private static void MarcaSonados(Abierta a)
        {
            if (a.Sonando.Count == 0) return;
            long ya = Sonado(a); bool alguno = false;
            while (a.Sonando.Count > 0 && a.Sonando[0].Value <= ya) { Termina(a.Sonando[0].Key); a.Sonando.RemoveAt(0); alguno = true; }
            if (alguno) a.Aviso.Set();
        }

        // LA TARJETA DEL AMPLIFICADOR (la misma eleccion que Motor/SalidaAlsa.cs)
        private static string elegida;
        internal static string Elige()
        {
            string e = Environment.GetEnvironmentVariable("ROCOLA_ALSA");
            if (!string.IsNullOrEmpty(e)) return e;
            if (elegida != null) return elegida;
            string mejor = "default";
            try
            {
                List<string> tarjetas = new List<string>(System.IO.Directory.GetDirectories("/proc/asound", "card*"));
                tarjetas.Sort(CompareNumero);
                foreach (string t in tarjetas)
                {
                    int n; if (!int.TryParse(System.IO.Path.GetFileName(t).Substring(4), out n)) continue;
                    List<string> pcms = new List<string>(System.IO.Directory.GetDirectories(t, "pcm*p"));
                    pcms.Sort(CompareNumero);
                    foreach (string p in pcms)
                    {
                        string info = "";
                        try { info = System.IO.File.ReadAllText(System.IO.Path.Combine(p, "info")); } catch { }
                        if (info.IndexOf("HDMI", StringComparison.OrdinalIgnoreCase) >= 0 || info.IndexOf("DisplayPort", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        string dev = System.IO.Path.GetFileName(p); dev = dev.Substring(3, dev.Length - 4);
                        mejor = "plughw:" + n + "," + dev;
                        goto hecho;
                    }
                }
            }
            catch { }
        hecho:
            elegida = mejor;
            return mejor;
        }
        private static int CompareNumero(string a, string b) { return Numero(a).CompareTo(Numero(b)); }
        private static int Numero(string r) { string f = System.IO.Path.GetFileName(r); int i = 0, n = 0; while (i < f.Length && !char.IsDigit(f[i])) i++; while (i < f.Length && char.IsDigit(f[i])) n = n * 10 + (f[i++] - '0'); return n; }
    }

    // EL WAVEOUT DE SIEMPRE (NAudio en XP, con aviso por ventana): la salida a la que la rocola vuelve si la suya falla por algo que no es
    // el aparato. En Linux no hay otra: es la misma salida propia sobre ALSA.
    public sealed class WaveOut : IWavePlayer, IWavePosition
    {
        private readonly WaveOutPropio p = new WaveOutPropio(new AlsaWaveOut());
        public int DesiredLatency { get { return p.DesiredLatency; } set { p.DesiredLatency = value; } }
        public int NumberOfBuffers { get { return p.NumberOfBuffers; } set { p.NumberOfBuffers = value; } }
        public int DeviceNumber { get; set; }
        public event EventHandler<StoppedEventArgs> PlaybackStopped { add { p.PlaybackStopped += value; } remove { p.PlaybackStopped -= value; } }
        public PlaybackState PlaybackState { get { return p.PlaybackState; } }
        public float Volume { get { return p.Volume; } set { p.Volume = value; } }
        public WaveFormat OutputWaveFormat { get { return p.OutputWaveFormat; } }
        public void Init(IWaveProvider waveProvider) { p.Init(waveProvider); }
        public void Play() { p.Play(); }
        public void Pause() { p.Pause(); }
        public void Stop() { p.Stop(); }
        public long GetPosition() { return p.GetPosition(); }
        public void Dispose() { p.Dispose(); }
    }
}

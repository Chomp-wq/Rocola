// LA SALIDA DE SONIDO DE ROCOLA OS (hito 3): ALSA directo (libasound), sin servidores de sonido en medio. Como WaveOutPropio en XP
// (SalidaPropia.cs): un hilo propio, de prioridad alta, que pide la musica a la cadena y la escribe en la tarjeta; la pantalla no
// interviene. 16 bits con el dither TPDF de siempre (DitherWaveProvider16). Si la tarjeta se queda sin datos (xrun) o se suspende,
// se recupera (snd_pcm_recover) y sigue.
using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    public sealed class SalidaAlsa : IDisposable
    {
        private const string Lib = "libasound.so.2";
        [DllImport(Lib)] private static extern int snd_pcm_open(out IntPtr pcm, string nombre, int stream, int modo);
        [DllImport(Lib)] private static extern int snd_pcm_set_params(IntPtr pcm, int formato, int acceso, uint canales, uint tasa, int remuestreo, uint latenciaUs);
        [DllImport(Lib)] private static extern unsafe long snd_pcm_writei(IntPtr pcm, byte* buffer, ulong cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_recover(IntPtr pcm, int error, int silencioso);
        [DllImport(Lib)] private static extern int snd_pcm_drain(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_drop(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_close(IntPtr pcm);
        [DllImport(Lib)] private static extern IntPtr snd_strerror(int error);
        private const int Reproducir = 0, S16Le = 2, Intercalado = 3;

        private readonly IWaveProvider fuente;
        private readonly int canales;
        private IntPtr pcm;
        private Thread hilo;
        private volatile bool parar;
        internal const uint LatenciaUs = 300000;   // el colchon de la tarjeta: 300 ms (en XP, de 750 a 1000)
        public event Action Acabo;                  // se acabo la musica (y ya sono)
        public long Recuperaciones;                 // xruns y suspensiones recuperadas
        private long escritos;                      // cuadros entregados a la tarjeta
        private readonly int tasa;
        // Cuadros que ya sonaron (aprox.): lo escrito menos el colchon de la tarjeta.
        public long CuadrosOidos { get { return Math.Max(0L, Interlocked.Read(ref escritos) - (long)LatenciaUs * tasa / 1000000L); } }
        public bool Sonando { get { Thread h = hilo; return h != null && h.IsAlive; } }

        public SalidaAlsa(ISampleProvider musica, string dispositivo = "default")
        {
            fuente = new Form1.DitherWaveProvider16(musica);
            canales = musica.WaveFormat.Channels; tasa = musica.WaveFormat.SampleRate;
            int r = snd_pcm_open(out pcm, dispositivo, Reproducir, 0);
            if (r < 0) throw new InvalidOperationException("ALSA: no se pudo abrir '" + dispositivo + "': " + Error(r));
            r = snd_pcm_set_params(pcm, S16Le, Intercalado, (uint)canales, (uint)musica.WaveFormat.SampleRate, 1, LatenciaUs);
            if (r < 0) { snd_pcm_close(pcm); pcm = IntPtr.Zero; throw new InvalidOperationException("ALSA: formato no admitido: " + Error(r)); }
        }
        private static string Error(int r) { return Marshal.PtrToStringAnsi(snd_strerror(r)); }

        public void Empieza()
        {
            hilo = new Thread(Bucle) { IsBackground = true, Name = "audio (ALSA)", Priority = ThreadPriority.Highest };
            hilo.Start();
        }

        public string PrioridadSonido = "";
        private static bool dicha;   // como quedo el hilo (Prioridad.cs)
        private unsafe void Bucle()
        {
            PrioridadSonido = Prioridad.Sonido();   // en Linux, Thread.Priority no hace nada: se le pide al nucleo
            if (!dicha) { dicha = true; try { Console.WriteLine("Sonido: el hilo de la salida va en " + PrioridadSonido); } catch { } }
            int cuadrosBloque = 1024, bytesCuadro = 2 * canales;
            byte[] b = new byte[cuadrosBloque * bytesCuadro];
            while (!parar)
            {
                int n = fuente.Read(b.AsSpan());
                if (n <= 0) break;
                int cuadros = n / bytesCuadro, hechos = 0;
                fixed (byte* p = b)
                    while (hechos < cuadros && !parar)
                    {
                        long w = snd_pcm_writei(pcm, p + hechos * bytesCuadro, (ulong)(cuadros - hechos));
                        if (w < 0)
                        {
                            if (snd_pcm_recover(pcm, (int)w, 1) < 0) { parar = true; break; }
                            Recuperaciones++;
                            continue;
                        }
                        hechos += (int)w; Interlocked.Add(ref escritos, w);
                    }
            }
            if (!parar) snd_pcm_drain(pcm);   // que suene lo que queda en la tarjeta
            Action a = Acabo; if (a != null && !parar) a();
        }

        public void Espera() { if (hilo != null) hilo.Join(); }

        public void Dispose()
        {
            parar = true;
            if (hilo != null && hilo != Thread.CurrentThread) hilo.Join(2000);
            if (pcm != IntPtr.Zero) { snd_pcm_drop(pcm); snd_pcm_close(pcm); pcm = IntPtr.Zero; }
        }
    }
}

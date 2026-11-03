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
        [DllImport(Lib)] private static extern int snd_pcm_delay(IntPtr pcm, out long cuadros);
        private const int Reproducir = 0, S16Le = 2, Intercalado = 3;

        private IWaveProvider fuente;
        private readonly int canales;
        private IntPtr pcm;
        private Thread hilo;
        private volatile bool parar;
        internal const uint LatenciaUs = 300000;   // el colchon de la tarjeta: 300 ms (en XP, de 750 a 1000)
        public event Action Acabo;                  // se acabo la musica (y ya sono)
        public long Recuperaciones;                 // xruns y suspensiones recuperadas
        private long escritos;                      // cuadros entregados a la tarjeta
        private readonly int tasa;
        // LO QUE YA SONO (3-11-2026): lo escrito menos lo que la tarjeta aun tiene por sonar, segun ella misma (snd_pcm_delay, que el
        // hilo del sonido pregunta tras cada escritura); antes se restaba el colchon pedido (300 ms), y la tarjeta puede dar otro.
        // Mientras no lo haya dicho, como antes.
        private long enCola = -1;
        public long CuadrosOidos
        {
            get
            {
                long c = Interlocked.Read(ref enCola);
                if (c < 0) c = (long)LatenciaUs * tasaSalida / 1000000L;
                long oidos = Math.Max(0L, Interlocked.Read(ref escritos) - c);
                return tasaSalida == tasa ? oidos : oidos * tasa / tasaSalida;   // en cuadros de la cancion
            }
        }
        public readonly string Dispositivo;   // el que se abrio de verdad
        public bool Sonando { get { Thread h = hilo; return h != null && h.IsAlive; } }

        public SalidaAlsa(ISampleProvider musica, string dispositivo = "default")
        {
            canales = musica.WaveFormat.Channels; tasa = tasaSalida = musica.WaveFormat.SampleRate;
            string elegido = dispositivo == "default" ? Elige() : dispositivo;
            ISampleProvider m = musica;
            string fallo;
            if (elegido.StartsWith("plughw:"))
            {
                // primero la tarjeta tal cual (sin plug): a la frecuencia de la cancion, si la admite (la HDA: 44,1 y 48 kHz). Si no (un MP3
                // viejo de 22 o 32 kHz), a 48 kHz con el remuestreo de la rocola (el de los discos continuos: 192 toques) y no con el lineal
                // de alsa-lib que pondria plug.
                string hw = "hw:" + elegido.Substring(7);
                fallo = Abre(hw, tasa);
                if (fallo == null) elegido = hw;
                else if (tasa != 48000 && (m = A48(musica)) != musica && Abre(hw, 48000) == null) { elegido = hw + " (a 48000 Hz)"; tasaSalida = 48000; fallo = null; }
                else { m = musica; fallo = Abre(elegido, tasa); }
            }
            else fallo = Abre(elegido, tasa);
            if (fallo != null && elegido != dispositivo)
            {   // la tarjeta elegida no se pudo abrir (ocupada, o no admite el formato): la de siempre
                if (!avisoFallo) { avisoFallo = true; try { Console.WriteLine("Sonido: " + fallo + "; se usa '" + dispositivo + "'"); } catch { } }
                m = musica; tasaSalida = tasa; elegido = dispositivo; fallo = Abre(elegido, tasa);
            }
            if (fallo != null) throw new InvalidOperationException(fallo);
            Dispositivo = elegido;
            fuente = new Form1.DitherWaveProvider16(m);
            // en el registro (rocola.txt), cuando cambia: que tarjeta se abrio de verdad y a que frecuencia
            string dicho = elegido + " a " + tasaSalida + " Hz" + (tasaSalida != tasa ? " (la cancion es de " + tasa + ")" : "");
            if (dicho != ultimaDicha) { ultimaDicha = dicho; try { Console.WriteLine("Sonido: abierta " + dicho); } catch { } }
        }
        private readonly int tasaSalida;   // a la que va la tarjeta (la de la cancion, salvo que no la admita: 48000)
        private static ISampleProvider A48(ISampleProvider musica)
        {
            try
            {
                WaveFormat d = FormatoAudio.Flotante(48000, musica.WaveFormat.Channels, FormatoAudio.Mascara(musica.WaveFormat));
                if (!RemuestreoContinuo.SePuede(musica.WaveFormat, d)) return musica;
                return new RemuestreoContinuo(musica, d, 0);
            }
            catch { return musica; }
        }
        private static bool avisoFallo; private static string ultimaDicha;
        private string Abre(string d, int frecuencia)
        {
            int r = snd_pcm_open(out pcm, d, Reproducir, 0);
            if (r < 0) { pcm = IntPtr.Zero; return "ALSA: no se pudo abrir '" + d + "': " + Error(r); }
            r = snd_pcm_set_params(pcm, S16Le, Intercalado, (uint)canales, (uint)frecuencia, 1, LatenciaUs);
            if (r < 0) { snd_pcm_close(pcm); pcm = IntPtr.Zero; return "ALSA: formato no admitido en '" + d + "': " + Error(r); }
            return null;
        }

        // LA TARJETA DEL AMPLIFICADOR, DIRECTA (3-11-2026). «default» en ALSA es la tarjeta 0 y, en una HDA, pasa por su mezclador (dmix),
        // que trabaja a 48 kHz: cada cancion de 44,1 kHz se convertia con el remuestreo lineal de alsa-lib (el peor; pierde agudos y
        // mete ruido de imagen), despues de todo lo que cuida la cadena. Y el A4 tiene dos HDA: la del HDMI de la Radeon y la analogica
        // (la que va al amplificador); la 0 puede ser la del HDMI. Aqui se elige la primera salida que no sea HDMI y se abre directa
        // (plughw: la frecuencia de la cancion tal cual, sin mezclador); si no se puede, la de siempre. ROCOLA_ALSA manda si esta puesta
        // (la pone /usr/bin/rocola con rocola-tarjeta, la misma eleccion). La musica y los videos no suenan a la vez: no hace falta mezclar.
        internal static string RaizAsound = Environment.GetEnvironmentVariable("ROCOLA_ASOUND") ?? "/proc/asound";   // (otra, para probarlo)
        private static string elegida;
        public static string Elige()
        {
            string e = Environment.GetEnvironmentVariable("ROCOLA_ALSA");
            if (!string.IsNullOrEmpty(e)) return e;
            if (elegida != null) return elegida;
            string mejor = "default";
            try
            {
                System.Collections.Generic.List<string> tarjetas = new System.Collections.Generic.List<string>(System.IO.Directory.GetDirectories(RaizAsound, "card*"));
                tarjetas.Sort(CompareNumero);
                foreach (string t in tarjetas)
                {
                    int n; if (!int.TryParse(System.IO.Path.GetFileName(t).Substring(4), out n)) continue;
                    System.Collections.Generic.List<string> pcms = new System.Collections.Generic.List<string>(System.IO.Directory.GetDirectories(t, "pcm*p"));
                    pcms.Sort(CompareNumero);
                    foreach (string p in pcms)
                    {
                        string info = "";
                        try { info = System.IO.File.ReadAllText(System.IO.Path.Combine(p, "info")); } catch { }
                        if (info.IndexOf("HDMI", StringComparison.OrdinalIgnoreCase) >= 0 || info.IndexOf("DisplayPort", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        string dev = System.IO.Path.GetFileName(p); dev = dev.Substring(3, dev.Length - 4);   // pcm0p -> 0
                        mejor = "plughw:" + n + "," + dev;
                        goto hecho;
                    }
                }
            }
            catch { }
        hecho:
            elegida = mejor;
            try { Console.WriteLine("Sonido: salida " + mejor); } catch { }
            return mejor;
        }
        private static int CompareNumero(string a, string b) { return Numero(a).CompareTo(Numero(b)); }
        private static int Numero(string r) { string f = System.IO.Path.GetFileName(r); int i = 0, n = 0; while (i < f.Length && !char.IsDigit(f[i])) i++; while (i < f.Length && char.IsDigit(f[i])) n = n * 10 + (f[i++] - '0'); return n; }
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
                            Interlocked.Increment(ref Recuperaciones);
                            continue;
                        }
                        hechos += (int)w; Interlocked.Add(ref escritos, w);
                        long d; if (snd_pcm_delay(pcm, out d) == 0 && d >= 0) Interlocked.Exchange(ref enCola, d);
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

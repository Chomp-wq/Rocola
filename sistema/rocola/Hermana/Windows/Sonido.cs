// LA SALIDA DE SONIDO DE LA ROCOLA DE XP, POR ALSA. La rocola de XP saca el sonido con su propia salida (SalidaPropia.cs:
// WaveOutPropio) sobre las llamadas waveOut de Windows, y deja poner otras (WaveOutPropio.Sistema, como hacen sus pruebas). Aqui se
// pone esta: las mismas llamadas (abrir, preparar, escribir bloques, pausa, seguir, reiniciar, posicion) hechas con ALSA. Un hilo
// escribe los bloques en orden; al acabar cada uno lo marca HECHO y avisa, como Windows. La posicion es lo escrito menos lo que la
// tarjeta aun no toca (snd_pcm_delay). El volumen de la tarjeta no se toca (AGENTS.md).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace Rocola.Linux
{
    internal sealed unsafe class SalidaAlsa : ISistemaWaveOut
    {
        private const string Lib = "libasound.so.2";
        [DllImport(Lib)] private static extern int snd_pcm_open(out IntPtr pcm, string nombre, int stream, int modo);
        [DllImport(Lib)] private static extern int snd_pcm_set_params(IntPtr pcm, int formato, int acceso, uint canales, uint tasa, int remuestreo, uint latenciaUs);
        [DllImport(Lib)] private static extern long snd_pcm_writei(IntPtr pcm, byte* buffer, ulong cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_recover(IntPtr pcm, int error, int silencioso);
        [DllImport(Lib)] private static extern int snd_pcm_drop(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_prepare(IntPtr pcm);
        [DllImport(Lib)] private static extern int snd_pcm_delay(IntPtr pcm, out long cuadros);
        [DllImport(Lib)] private static extern int snd_pcm_close(IntPtr pcm);
        private const int S16Le = 2, S24Le = 6, S32Le = 10, FloatLe = 14, Intercalado = 3;
        internal static string Dispositivo = Environment.GetEnvironmentVariable("ROCOLA_ALSA") ?? "default";
        // Cuanto colchon pide a la tarjeta; el resto lo pone la rocola con sus bloques (DesiredLatency).
        private const uint LatenciaUs = 60000;

        private sealed class Salida
        {
            public IntPtr Pcm; public int BytesCuadro; public AutoResetEvent Aviso;
            public readonly Queue<IntPtr> Cola = new Queue<IntPtr>();
            public readonly object Cerrojo = new object();
            public Thread Hilo; public volatile bool Fin, Pausada;
            public long Escritos;   // cuadros dados a la tarjeta desde el ultimo reinicio
            public int Generacion;
        }
        private static readonly Dictionary<IntPtr, Salida> salidas = new Dictionary<IntPtr, Salida>();
        private static int ultima;

        public int Abre(out IntPtr salida, WaveFormat formato, AutoResetEvent aviso)
        {
            salida = IntPtr.Zero;
            int f;
            if (formato.Encoding == WaveFormatEncoding.IeeeFloat && formato.BitsPerSample == 32) f = FloatLe;
            else if (formato.BitsPerSample == 16) f = S16Le;
            else if (formato.BitsPerSample == 24) f = S24Le;
            else if (formato.BitsPerSample == 32) f = S32Le;
            else return 32;   // WAVERR_BADFORMAT
            IntPtr pcm;
            int r = snd_pcm_open(out pcm, Dispositivo, 0, 0);
            if (r < 0) return 2;   // MMSYSERR_BADDEVICEID
            r = snd_pcm_set_params(pcm, f, Intercalado, (uint)formato.Channels, (uint)formato.SampleRate, 1, LatenciaUs);
            if (r < 0) { snd_pcm_close(pcm); return 32; }
            Salida s = new Salida(); s.Pcm = pcm; s.BytesCuadro = formato.BlockAlign; s.Aviso = aviso;
            salida = new IntPtr(Interlocked.Increment(ref ultima));
            lock (salidas) salidas[salida] = s;
            s.Hilo = new Thread(Escribe) { IsBackground = true, Name = "Salida ALSA", Priority = ThreadPriority.Highest };
            s.Hilo.Start(s);
            return 0;
        }
        private static Salida De(IntPtr h) { lock (salidas) { Salida s; return salidas.TryGetValue(h, out s) ? s : null; } }
        private static void Escribe(object o)
        {
            Salida s = (Salida)o;
            while (!s.Fin)
            {
                IntPtr cab = IntPtr.Zero; int gen;
                lock (s.Cerrojo)
                {
                    while (!s.Fin && (s.Pausada || s.Cola.Count == 0)) Monitor.Wait(s.Cerrojo, 100);
                    if (s.Fin) break;
                    cab = s.Cola.Peek(); gen = s.Generacion;
                }
                CabeceraWave* c = (CabeceraWave*)cab;
                byte* datos = (byte*)c->Datos; long cuadros = c->Largo / s.BytesCuadro, hechos = 0;
                bool cortado = false;
                while (hechos < cuadros && !s.Fin)
                {
                    lock (s.Cerrojo) if (s.Generacion != gen) { cortado = true; break; }
                    if (s.Pausada) { Thread.Sleep(5); continue; }
                    long porVez = Math.Min(cuadros - hechos, 1024);
                    long w = snd_pcm_writei(s.Pcm, datos + hechos * s.BytesCuadro, (ulong)porVez);
                    if (w < 0) { if (snd_pcm_recover(s.Pcm, (int)w, 1) < 0) { Thread.Sleep(10); } continue; }
                    hechos += w;
                    Interlocked.Add(ref s.Escritos, w);
                }
                lock (s.Cerrojo)
                {
                    if (!cortado && s.Generacion == gen && s.Cola.Count > 0 && s.Cola.Peek() == cab)
                    {
                        s.Cola.Dequeue();
                        c->Banderas = (c->Banderas & ~WaveOutPropio.EnCola) | WaveOutPropio.Hecho;
                    }
                }
                s.Aviso.Set();
            }
        }
        public int Prepara(IntPtr salida, IntPtr cabecera, int tamano) { ((CabeceraWave*)cabecera)->Banderas |= 2; return 0; }
        public int Desprepara(IntPtr salida, IntPtr cabecera, int tamano) { ((CabeceraWave*)cabecera)->Banderas &= ~2; return 0; }
        public int Escribe(IntPtr salida, IntPtr cabecera, int tamano)
        {
            Salida s = De(salida); if (s == null) return 5;
            CabeceraWave* c = (CabeceraWave*)cabecera;
            lock (s.Cerrojo)
            {
                c->Banderas = (c->Banderas & ~WaveOutPropio.Hecho) | WaveOutPropio.EnCola;
                s.Cola.Enqueue(cabecera);
                Monitor.PulseAll(s.Cerrojo);
            }
            return 0;
        }
        public int Reinicia(IntPtr salida)
        {
            Salida s = De(salida); if (s == null) return 5;
            lock (s.Cerrojo)
            {
                s.Generacion++;
                while (s.Cola.Count > 0) { CabeceraWave* c = (CabeceraWave*)s.Cola.Dequeue(); c->Banderas = (c->Banderas & ~WaveOutPropio.EnCola) | WaveOutPropio.Hecho; }
                snd_pcm_drop(s.Pcm); snd_pcm_prepare(s.Pcm);
                Interlocked.Exchange(ref s.Escritos, 0);
                s.Pausada = false;
                Monitor.PulseAll(s.Cerrojo);
            }
            s.Aviso.Set();
            return 0;
        }
        public int Pausa(IntPtr salida) { Salida s = De(salida); if (s == null) return 5; s.Pausada = true; return 0; }
        public int Sigue(IntPtr salida) { Salida s = De(salida); if (s == null) return 5; lock (s.Cerrojo) { s.Pausada = false; Monitor.PulseAll(s.Cerrojo); } return 0; }
        public int Cierra(IntPtr salida)
        {
            Salida s = De(salida); if (s == null) return 5;
            lock (s.Cerrojo) { if (s.Cola.Count > 0) return 33; s.Fin = true; Monitor.PulseAll(s.Cerrojo); }   // WAVERR_STILLPLAYING
            s.Hilo.Join(2000);
            snd_pcm_drop(s.Pcm); snd_pcm_close(s.Pcm);
            lock (salidas) salidas.Remove(salida);
            return 0;
        }
        public int Posicion(IntPtr salida, out long bytes, WaveFormat formato)
        {
            bytes = 0;
            Salida s = De(salida); if (s == null) return 5;
            long escritos = Interlocked.Read(ref s.Escritos), retraso;
            if (snd_pcm_delay(s.Pcm, out retraso) < 0) retraso = 0;
            long oidos = Math.Max(0, escritos - Math.Max(0, retraso));
            bytes = (oidos * s.BytesCuadro) & 0xFFFFFFFFL;   // de 32 bits, como Windows (la rocola cuenta las vueltas)
            return 0;
        }
    }
}

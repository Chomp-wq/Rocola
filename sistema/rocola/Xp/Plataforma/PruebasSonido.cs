// PRUEBAS DEL SONIDO NATIVO (port nativo de XP, pista windows; no son de XP): --prueba-lector y --prueba-salida (ver PruebasLinux.cs).
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    internal static class PruebasSonido
    {
        private static readonly string[] Videos = { ".mp4", ".avi", ".mpg", ".mpeg", ".wmv", ".mkv", ".mov", ".flv", ".vob", ".m4v" };

        // Cada archivo: con el lector de XP (LectorAudio), o su duracion si es un video (MedidorVideo)
        internal static int Lector(string[] archivos)
        {
            foreach (string f in archivos)
            {
                string ext = (Path.GetExtension(f) ?? "").ToLowerInvariant();
                if (Array.IndexOf(Videos, ext) >= 0)
                {
                    MedidorVideo m = MedidorVideo.Crea();
                    if (m == null) { Console.WriteLine(Path.GetFileName(f) + ": sin ffmpeg"); continue; }
                    double d = 0; int i = 0;
                    if (m.PideDuracion(f)) for (; i < 30 && !(d > 0.05 && d < 36000); i++) { d = m.Duracion; if (!(d > 0.05)) Thread.Sleep(100); }
                    m.Dispose();
                    Console.WriteLine(Path.GetFileName(f) + ": video, duracion " + ((float)d).ToString("0.##", CultureInfo.InvariantCulture) + " s (como rocola_duracion.txt), " + (i * 100) + " ms de espera");
                    continue;
                }
                try
                {
                    using (LectorAudio l = new LectorAudio(f))
                    {
                        float[] b = new float[l.WaveFormat.SampleRate * l.WaveFormat.Channels * 2];
                        int n = 0, r;
                        while (n < b.Length && (r = l.Read(b, n, b.Length - n)) > 0) n += r;
                        ulong h = 1469598103934665603UL;
                        double pico = 0;
                        for (int i = 0; i < n; i++) { uint bits = BitConverter.SingleToUInt32Bits(b[i]); h = (h ^ bits) * 1099511628211UL; pico = Math.Max(pico, Math.Abs(b[i])); }
                        Console.WriteLine(Path.GetFileName(f) + ": " + l.WaveFormat + ", largo " + l.Length + " bytes, " + l.TotalTime.TotalSeconds.ToString("0.0000", CultureInfo.InvariantCulture)
                            + " s; primeras " + n + " muestras: pico " + pico.ToString("0.0000", CultureInfo.InvariantCulture) + ", huella " + h.ToString("X16"));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(Path.GetFileName(f) + ": no abre: " + ex.GetType().Name + " (" + ex.Message + "); XP diria «" + Form1.MotivoFallo(ex) + "», danada=" + Form1.EsArchivoDanado(ex));
                }
            }
            return 0;
        }

        // La salida de XP con un tono, la posicion frente al reloj y el aviso de parada
        internal static int Salida(double segundos, double paraEn)
        {
            WaveFormat f = new WaveFormat(44100, 16, 2);
            Tono t = new Tono(f, (long)(segundos * f.SampleRate));
            WaveOutPropio w = new WaveOutPropio();
            w.DesiredLatency = 1000; w.NumberOfBuffers = 20;
            string aviso = null; ManualResetEvent paro = new ManualResetEvent(false);
            w.PlaybackStopped += delegate (object s, StoppedEventArgs e) { aviso = e.Exception == null ? "sin error" : e.Exception.GetType().Name + ": " + e.Exception.Message; paro.Set(); };
            System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();
            try { w.Init(t); }
            catch (Exception ex) { Console.WriteLine("Init: " + ex.GetType().Name + ": " + ex.Message + "; dispositivo=" + Form1.EsFalloDelDispositivoPrueba(ex) + " formato=" + Form1.EsFormatoRechazadoPrueba(ex)); return 1; }
            Console.WriteLine("abierta en " + reloj.ElapsedMilliseconds + " ms, " + w.Bloques + " bloques");
            reloj.Restart();
            w.Play();
            bool parado = false;
            while (!paro.WaitOne(100))
            {
                double s = reloj.Elapsed.TotalSeconds;
                long b = w.GetPosition();
                Console.WriteLine(s.ToString("0.000", CultureInfo.InvariantCulture) + " s: posicion " + b + " bytes = " + ((double)b / f.AverageBytesPerSecond).ToString("0.000", CultureInfo.InvariantCulture) + " s, leido " + ((double)t.Leidos / f.SampleRate).ToString("0.000", CultureInfo.InvariantCulture) + " s, estado " + w.PlaybackState);
                if (paraEn >= 0 && !parado && s >= paraEn) { parado = true; w.Stop(); Console.WriteLine("Stop a los " + s.ToString("0.000", CultureInfo.InvariantCulture) + " s"); }
                if (s > segundos + 5) { Console.WriteLine("no aviso la parada"); break; }
            }
            Console.WriteLine("aviso de parada a los " + reloj.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " s: " + aviso + "; posicion final " + w.GetPosition());
            w.Dispose();
            return 0;
        }
        private sealed class Tono : IWaveProvider
        {
            private readonly WaveFormat f; private readonly long total; internal long Leidos;
            internal Tono(WaveFormat f, long cuadros) { this.f = f; total = cuadros; }
            public WaveFormat WaveFormat { get { return f; } }
            public int Read(Span<byte> d) { return Puente.LeeBytes(Read, d); }
            public int Read(byte[] b, int o, int c)
            {
                int n = (int)Math.Min(c / 4, total - Leidos);
                for (int i = 0; i < n; i++)
                {
                    short v = (short)(8000 * Math.Sin(2 * Math.PI * 440 * (Leidos + i) / f.SampleRate));
                    b[o + 4 * i] = (byte)v; b[o + 4 * i + 1] = (byte)(v >> 8); b[o + 4 * i + 2] = (byte)v; b[o + 4 * i + 3] = (byte)(v >> 8);
                }
                Leidos += n;
                return n * 4;
            }
        }
    }
}

namespace Rocola
{
    // (pruebas) lo que decide Form1 con un fallo de la salida
    public partial class Form1
    {
        internal static bool EsFalloDelDispositivoPrueba(Exception e) { return EsFalloDelDispositivo(e); }
        internal static bool EsFormatoRechazadoPrueba(Exception e) { return EsFormatoRechazado(e); }
    }
}

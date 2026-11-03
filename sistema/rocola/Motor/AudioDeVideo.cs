// EL SONIDO DE UN VIDEO, PARA MEDIRLO (Rocola OS, 3-11-2026). En XP los videos los pone el reproductor de Windows y la rocola no puede
// abrir su sonido: nunca se median, asi que su volumen era el de la perilla («Volumen de los videos») y nada mas, y cada video sonaba
// como venia grabado. En Rocola OS el sistema trae ffmpeg (el mismo con el que mpv los pone): aqui se le pide solo la pista de sonido
// (sin descomprimir la imagen: -vn), en coma flotante, estereo y a 48 kHz, por una tuberia; el repaso la mide como a una cancion
// (MeasureFile: su sonoridad, como XP la media de un video). Lo que dura se lee de lo que ffmpeg dice del archivo («Duration:»).
// Solo se lee de principio a fin (el repaso mide los videos enteros); saltar no se puede. Sin ffmpeg, el video queda sin medir, como
// antes. ffmpeg hereda la prioridad baja del repaso (nice 19: Prioridad.Fondo).
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Rocola
{
    internal sealed class AudioDeVideo : IDisposable
    {
        internal const int Tasa = 48000, Canales = 2;
        private static readonly Regex Dura = new Regex(@"Duration:\s*(\d+):(\d\d):(\d\d(?:\.\d+)?)", RegexOptions.CultureInvariant);
        private Process p;
        private Stream salida;
        private long leidas;
        internal TimeSpan Duracion { get; private set; }
        internal TimeSpan Posicion { get { return TimeSpan.FromSeconds((double)leidas / (Tasa * Canales)); } }

        // null si no hay ffmpeg, el archivo no tiene sonido o no dice lo que dura.
        internal static AudioDeVideo Abre(string ruta)
        {
            string ffmpeg = Environment.GetEnvironmentVariable("ROCOLA_FFMPEG") ?? "ffmpeg";
            AudioDeVideo a = new AudioDeVideo();
            try
            {
                ProcessStartInfo pi = new ProcessStartInfo(ffmpeg);
                foreach (string s in new[] { "-nostdin", "-hide_banner", "-nostats", "-v", "info", "-i", ruta, "-map", "0:a:0", "-vn", "-sn", "-dn",
                    "-ac", Canales.ToString(), "-ar", Tasa.ToString(), "-f", "f32le", "pipe:1" })
                    pi.ArgumentList.Add(s);
                pi.UseShellExecute = false; pi.RedirectStandardOutput = true; pi.RedirectStandardError = true; pi.RedirectStandardInput = false;
                System.Threading.ManualResetEventSlim hay = new System.Threading.ManualResetEventSlim(false);
                TimeSpan dura = TimeSpan.Zero;
                a.p = Process.Start(pi);
                a.p.ErrorDataReceived += delegate (object o, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { hay.Set(); return; }
                    if (hay.IsSet) return;
                    Match m = Dura.Match(e.Data);
                    if (m.Success)
                    {
                        dura = new TimeSpan(0, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), 0)
                             + TimeSpan.FromSeconds(double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
                        hay.Set();
                    }
                    else if (e.Data.StartsWith("Output #0") || e.Data.StartsWith("Stream mapping")) hay.Set();   // ya paso la cabecera: sin duracion
                };
                a.p.BeginErrorReadLine();
                a.salida = a.p.StandardOutput.BaseStream;
                if (!hay.Wait(15000) || dura <= TimeSpan.Zero) { a.Dispose(); return null; }
                a.Duracion = dura;
                return a;
            }
            catch { a.Dispose(); return null; }
        }

        internal int Read(Span<float> d)
        {
            if (salida == null) return 0;
            Span<byte> destino = MemoryMarshal.AsBytes(d);
            int total = 0;
            int pide = destino.Length - destino.Length % (4 * Canales);
            while (total < pide)
            {
                int n;
                try { n = salida.Read(destino.Slice(total, pide - total)); } catch { n = 0; }
                if (n <= 0) break;
                total += n;
            }
            int resto = total % (4 * Canales);   // al acabar el archivo, un cuadro a medias se tira
            if (resto > 0 && total >= pide) { }
            total -= resto;
            leidas += total / 4;
            return total / 4;
        }

        public void Dispose()
        {
            try { if (p != null && !p.HasExited) p.Kill(); } catch { }
            try { if (p != null) { p.WaitForExit(2000); p.Dispose(); } } catch { }
            p = null; salida = null;
        }
    }
}

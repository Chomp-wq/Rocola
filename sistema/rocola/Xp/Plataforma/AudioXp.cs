// EL LECTOR DE AUDIO DE CUALQUIER FORMATO (AudioFileReader de NAudio en XP; traduccion de XP, fase 1). En Windows lee los MP3 y los WAV
// con NAudio y lo demas (WMA, M4A, AAC...) con los codecs de Windows (Media Foundation). En Linux: MP3 (Mp3FileReader, NAudioXp.cs), WAV
// y AIFF con NAudio, y lo demas con ffmpeg (el descompresor nativo de Linux, que ya trae Rocola OS), por una tuberia en coma flotante.
// Sin ffmpeg, esos formatos no abren (como en un Windows sin el codec).
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public class AudioFileReader : ISampleProvider, IDisposable
    {
        private readonly string ruta;
        private WaveStream lector;            // MP3, WAV, AIFF
        private ISampleProvider muestras;     // el de arriba en coma flotante
        private FfmpegLector ff;              // lo demas
        private float volumen = 1f;
        public AudioFileReader(string fileName)
        {
            ruta = fileName;
            string ext = (Path.GetExtension(fileName) ?? "").ToLowerInvariant();
            if (ext == ".mp3") lector = new Mp3FileReader(fileName);
            else if (ext == ".wav") lector = new WaveFileReader(fileName);
            else if (ext == ".aiff" || ext == ".aif") lector = new AiffFileReader(fileName);
            if (lector != null) muestras = new SampleChannel(lector, false);
            else ff = new FfmpegLector(fileName);
        }
        public string FileName { get { return ruta; } }
        public WaveFormat WaveFormat { get { return muestras != null ? muestras.WaveFormat : ff.Formato; } }
        public TimeSpan TotalTime { get { return lector != null ? lector.TotalTime : ff.Duracion; } }
        public TimeSpan CurrentTime
        {
            get { return lector != null ? lector.CurrentTime : ff.Posicion; }
            set { if (lector != null) lector.CurrentTime = value; else ff.Posicion = value; }
        }
        public float Volume { get { return volumen; } set { volumen = value; } }
        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
        public int Read(float[] buffer, int offset, int count)
        {
            int n = muestras != null ? muestras.Read(buffer, offset, count) : ff.Lee(buffer, offset, count);
            if (volumen != 1f) for (int i = 0; i < n; i++) buffer[offset + i] *= volumen;
            return n;
        }
        public void Dispose()
        {
            if (lector != null) { lector.Dispose(); lector = null; }
            if (ff != null) { ff.Dispose(); ff = null; }
        }
    }

    // ffmpeg por una tuberia: la frecuencia y los canales del archivo (ffprobe), en float32 intercalado. Saltar = volver a abrir con -ss.
    internal sealed class FfmpegLector : IDisposable
    {
        private readonly string ruta;
        private Process p; private Stream salida;
        private long leidas;   // muestras por canal desde el ultimo salto
        private TimeSpan desde;
        internal readonly WaveFormat Formato;
        internal readonly TimeSpan Duracion;
        private byte[] apoyo = new byte[0];
        internal FfmpegLector(string ruta)
        {
            this.ruta = ruta;
            int tasa = 44100, ch = 2; double dur = 0;
            string info = Ejecuta("ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries", "stream=sample_rate,channels:format=duration", "-of", "default=nw=1", ruta);
            if (info == null) throw new NotSupportedException("sin ffmpeg no se puede abrir " + Path.GetExtension(ruta));
            foreach (string l in info.Split('\n'))
            {
                string[] kv = l.Trim().Split('=');
                if (kv.Length != 2) continue;
                if (kv[0] == "sample_rate") int.TryParse(kv[1], out tasa);
                else if (kv[0] == "channels") int.TryParse(kv[1], out ch);
                else if (kv[0] == "duration") double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out dur);
            }
            if (ch < 1 || ch > 2) ch = 2;
            if (tasa < 8000) tasa = 44100;
            Formato = WaveFormat.CreateIeeeFloatWaveFormat(tasa, ch);
            Duracion = TimeSpan.FromSeconds(dur);
            Abre(TimeSpan.Zero);
        }
        private static string Ejecuta(string prog, params string[] args)
        {
            try
            {
                ProcessStartInfo pi = new ProcessStartInfo(prog) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string a in args) pi.ArgumentList.Add(a);
                using (Process q = Process.Start(pi)) { string r = q.StandardOutput.ReadToEnd(); q.WaitForExit(10000); return q.ExitCode == 0 ? r : null; }
            }
            catch { return null; }
        }
        private void Abre(TimeSpan en)
        {
            Cierra();
            ProcessStartInfo pi = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = false };
            foreach (string a in new[] { "-v", "quiet", "-nostdin", "-ss", en.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture), "-i", ruta, "-vn", "-f", "f32le",
                "-ac", Formato.Channels.ToString(CultureInfo.InvariantCulture), "-ar", Formato.SampleRate.ToString(CultureInfo.InvariantCulture), "-" }) pi.ArgumentList.Add(a);
            p = Process.Start(pi); salida = p.StandardOutput.BaseStream; desde = en; leidas = 0;
        }
        internal TimeSpan Posicion
        {
            get { return desde + TimeSpan.FromSeconds((double)leidas / Formato.SampleRate); }
            set { Abre(value < TimeSpan.Zero ? TimeSpan.Zero : value); }
        }
        internal int Lee(float[] buffer, int offset, int count)
        {
            if (salida == null) return 0;
            int bytes = count * 4;
            if (apoyo.Length < bytes) apoyo = new byte[bytes];
            int total = 0;
            while (total < bytes) { int n = salida.Read(apoyo, total, bytes - total); if (n <= 0) break; total += n; }
            int m = total / 4;
            Buffer.BlockCopy(apoyo, 0, buffer, offset * 4, m * 4);
            leidas += m / Formato.Channels;
            return m;
        }
        private void Cierra()
        {
            try { if (p != null && !p.HasExited) p.Kill(); } catch { }
            try { if (p != null) p.Dispose(); } catch { }
            p = null; salida = null;
        }
        public void Dispose() { Cierra(); }
    }
}

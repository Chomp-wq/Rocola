// EL LECTOR DE CANCIONES DE ROCOLA OS (hito 3). En XP los MP3 los descomprime el ACM de Windows (y NLayer solo en las que llegan al
// maximo: FlotanteNLayer.cs); en Linux no hay ACM: TODOS los MP3 van por NLayer 3, en coma flotante, sin recortar lo que pasa del
// maximo. Los WAV, con el lector de NAudio. Como en XP desde el 24-10-2026, un archivo mono sale como estereo de dos canales iguales.
// Los MP3 de frecuencias mezcladas se reconstruyen a la dominante, como en XP (Mp3DeUnaFrecuencia).
using System;
using System.IO;
using NAudio.Wave;

namespace Rocola
{
    public sealed class LectorCancion : ISampleProvider, IDisposable
    {
        private readonly NLayer.MpegFile mp3;
        private readonly WaveStream wav;
        private readonly ISampleProvider muestras;   // las del WAV, ya en coma flotante
        private readonly int canalesArchivo;
        private readonly WaveFormat formato;          // estereo (o los canales del archivo, para medir)
        private readonly bool comoEstereo;
        private float[] mono = new float[0];

        public LectorCancion(string ruta) : this(ruta, true) { }
        // comoEstereo = false: con los canales del archivo (la medida del repaso mide un mono como mono, como en XP: LectorMedida).
        public LectorCancion(string ruta, bool comoEstereo)
        {
            this.comoEstereo = comoEstereo;
            if (Path.GetExtension(ruta).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                MemoryStream limpio = Mp3DeUnaFrecuencia(ruta);
                if (limpio != null) { Reconstruido = true; mp3 = new NLayer.MpegFile(limpio); }
                else mp3 = new NLayer.MpegFile(ruta);
                canalesArchivo = mp3.Channels;
                formato = WaveFormat.CreateIeeeFloatWaveFormat(mp3.SampleRate, comoEstereo ? 2 : canalesArchivo);
            }
            else
            {
                WaveFileReader w = new WaveFileReader(ruta);
                wav = w; muestras = w.ToSampleProvider();
                canalesArchivo = w.WaveFormat.Channels;
                if (canalesArchivo > 2) { w.Dispose(); throw new NotSupportedException("mas de dos canales"); }
                formato = WaveFormat.CreateIeeeFloatWaveFormat(w.WaveFormat.SampleRate, comoEstereo ? 2 : canalesArchivo);
            }
        }

        // Un MP3 que ya esta en memoria (el clip con el que se mide el relleno, RellenoMp3.cs).
        public LectorCancion(Stream mp3EnMemoria)
        {
            comoEstereo = true;
            mp3 = new NLayer.MpegFile(mp3EnMemoria);
            canalesArchivo = mp3.Channels;
            formato = WaveFormat.CreateIeeeFloatWaveFormat(mp3.SampleRate, 2);
        }

        // true si era un MP3 de frecuencias (o canales) mezcladas y se leyo reconstruido (sin el relleno del codificador, como en XP).
        public bool Reconstruido { get; private set; }

        // LOS MP3 DE FRECUENCIAS MEZCLADAS (en XP, BuildCleanMp3 de Form1.Sonido.cs): unos pocos archivos traen cuadros de otra
        // frecuencia (o mono entre estereo). En XP el ACM falla con ellos y se reconstruyen; NLayer no falla, pero toca esos cuadros a
        // la frecuencia del primero (desafinados y mas largos). Aqui se mira SIEMPRE: si hay mas de una clave (frecuencia y canales),
        // se copian a memoria solo los cuadros de la dominante, como en XP (los de la minoria se descartan: a lo sumo un salto).
        // null = de una sola frecuencia (lo normal): se lee el archivo tal cual.
        internal static MemoryStream Mp3DeUnaFrecuencia(string ruta)
        {
            System.Collections.Generic.Dictionary<int, int> claves = new System.Collections.Generic.Dictionary<int, int>();
            using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                while (true)
                {
                    Mp3Frame f; try { f = Mp3Frame.LoadFromStream(fs, false); } catch { break; }
                    if (f == null) break;
                    int k = Clave(f); int c; claves.TryGetValue(k, out c); claves[k] = c + 1;
                }
            if (claves.Count < 2) return null;
            int dom = 0, mejor = -1;
            foreach (System.Collections.Generic.KeyValuePair<int, int> kv in claves) if (kv.Value > mejor) { mejor = kv.Value; dom = kv.Key; }
            MemoryStream ms = new MemoryStream();
            using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                while (true)
                {
                    Mp3Frame f; try { f = Mp3Frame.LoadFromStream(fs); } catch { break; }
                    if (f == null) break;
                    if (Clave(f) == dom && f.RawData != null) ms.Write(f.RawData, 0, f.RawData.Length);
                }
            if (ms.Length < 2048) { ms.Dispose(); return null; }   // casi nada util: que se lea tal cual
            ms.Position = 0;
            return ms;
        }
        private static int Clave(Mp3Frame f) { return f.SampleRate * 4 + (f.ChannelMode == ChannelMode.Mono ? 1 : 2); }

        public WaveFormat WaveFormat { get { return formato; } }
        public TimeSpan Duracion { get { return mp3 != null ? mp3.Duration : wav.TotalTime; } }
        public TimeSpan Posicion
        {
            get { return mp3 != null ? mp3.Time : wav.CurrentTime; }
            set { if (mp3 != null) mp3.Time = value; else wav.CurrentTime = value; }
        }

        public int Read(Span<float> destino)
        {
            if (!comoEstereo && canalesArchivo == 1) return LeeArchivo(destino);
            int pide = destino.Length & ~1;   // cuadros enteros
            if (pide == 0) return 0;
            if (canalesArchivo == 2) return LeeArchivo(destino.Slice(0, pide));
            // mono: se lee la mitad y cada muestra sale por los dos canales
            int n = pide / 2;
            if (mono.Length < n) mono = new float[n];
            int got = LeeArchivo(mono.AsSpan(0, n));
            for (int i = got - 1; i >= 0; i--) { float v = mono[i]; destino[2 * i] = v; destino[2 * i + 1] = v; }
            return got * 2;
        }
        private float[] apoyo = new float[0];
        private int LeeArchivo(Span<float> d)
        {
            if (mp3 == null) return muestras.Read(d);
            if (apoyo.Length < d.Length) apoyo = new float[d.Length];
            int n = mp3.ReadSamples(apoyo, 0, d.Length);
            if (n > 0) apoyo.AsSpan(0, n).CopyTo(d);
            return Math.Max(0, n);
        }

        public void Dispose() { if (mp3 != null) mp3.Dispose(); if (wav != null) wav.Dispose(); }
    }
}

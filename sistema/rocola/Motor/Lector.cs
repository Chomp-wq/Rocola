// EL LECTOR DE CANCIONES DE ROCOLA OS (hito 3). En XP los MP3 los descomprime el ACM de Windows (y NLayer solo en las que llegan al
// maximo: FlotanteNLayer.cs); en Linux no hay ACM: TODOS los MP3 van por NLayer 3, en coma flotante, sin recortar lo que pasa del
// maximo. Los WAV, con el lector de NAudio. Como en XP desde el 24-10-2026, un archivo mono sale como estereo de dos canales iguales.
// (Falta: el recorte sin huecos de la etiqueta LAME de RellenoMp3.cs y el resto de formatos.)
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
        private readonly WaveFormat formato;          // siempre estereo
        private float[] mono = new float[0];

        public LectorCancion(string ruta)
        {
            if (Path.GetExtension(ruta).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                mp3 = new NLayer.MpegFile(ruta);
                canalesArchivo = mp3.Channels;
                formato = WaveFormat.CreateIeeeFloatWaveFormat(mp3.SampleRate, 2);
            }
            else
            {
                WaveFileReader w = new WaveFileReader(ruta);
                wav = w; muestras = w.ToSampleProvider();
                canalesArchivo = w.WaveFormat.Channels;
                if (canalesArchivo > 2) { w.Dispose(); throw new NotSupportedException("mas de dos canales"); }
                formato = WaveFormat.CreateIeeeFloatWaveFormat(w.WaveFormat.SampleRate, 2);
            }
        }

        public WaveFormat WaveFormat { get { return formato; } }
        public TimeSpan Duracion { get { return mp3 != null ? mp3.Duration : wav.TotalTime; } }
        public TimeSpan Posicion
        {
            get { return mp3 != null ? mp3.Time : wav.CurrentTime; }
            set { if (mp3 != null) mp3.Time = value; else wav.CurrentTime = value; }
        }

        public int Read(Span<float> destino)
        {
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

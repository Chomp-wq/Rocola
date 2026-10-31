// ABRIR UNA CANCION PARA QUE SUENE (en XP, Rocola/Form1.Sonido.cs: OpenAudio y FileAudioSource). Lo de alrededor es lo mismo que en XP
// (sin el hueco del codificador -RellenoMp3.cs- y sin los silencios largos -SinSilencios.cs-, con el sello del archivo medido); el
// lector es el de Rocola OS (Lector.cs: todos los MP3 por NLayer, los mono ya como estereo), en lugar del de NAudio 1.9 y el ACM de Windows.
using System;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // La cancion de un archivo, con su volumen y sus tiempos (como FileAudioSource de XP).
        private sealed class FuenteArchivo : IAudioSource
        {
            private readonly LectorCancion lector;
            private volatile float volumen = 1f;
            internal FuenteArchivo(string ruta) { lector = new LectorCancion(ruta); }
            public WaveFormat WaveFormat { get { return lector.WaveFormat; } }
            public int Read(Span<float> destino)
            {
                int n = lector.Read(destino);
                float v = volumen;
                if (v != 1f) for (int i = 0; i < n; i++) destino[i] *= v;   // como el VolumeSampleProvider de NAudio: a 1, ni se toca
                return n;
            }
            public int Read(float[] buffer, int offset, int count) { return Read(buffer.AsSpan(offset, count)); }
            public float Volume { set { volumen = value; } }
            public TimeSpan CurrentTime { get { return lector.Posicion; } }
            public TimeSpan TotalTime { get { return lector.Duracion; } }
            public void Salta(TimeSpan donde) { lector.Posicion = donde; }
            public void Dispose() { lector.Dispose(); }
        }

        // Como OpenAudio de XP, sin la reconstruccion de los MP3 de frecuencias mezcladas (NLayer los lee de otra forma: queda pendiente).
        private IAudioSource OpenAudio(string path)
        {
            string original = RutaOriginalDe(path);
            FuenteMedida inicio = CapturaFuente(original);
            return SinSilencios(path, SinRelleno(path, SellaFuenteAudio(original, inicio, new FuenteArchivo(path))));
        }

        // El lector para MEDIR una cancion (el repaso; en XP, LectorMedida de Form1.Sonido.cs con el ACM): el de Rocola OS, con los
        // canales del archivo.
        private sealed class LectorMedida : IDisposable
        {
            private readonly LectorCancion lector;
            private float volumen = 1f;
            internal LectorMedida(string ruta) { lector = new LectorCancion(ruta, false); }
            internal WaveFormat WaveFormat { get { return lector.WaveFormat; } }
            internal float Volume { set { volumen = value; } }
            internal TimeSpan TotalTime { get { return lector.Duracion; } }
            internal int Read(float[] buffer, int offset, int count)
            {
                int n = lector.Read(buffer.AsSpan(offset, count));
                if (volumen != 1f) for (int i = offset; i < offset + n; i++) buffer[i] *= volumen;
                return n;
            }
            public void Dispose() { lector.Dispose(); }
        }
    }
}

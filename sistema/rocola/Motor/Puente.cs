// EL PUENTE A NAUDIO 3 (Rocola OS, 31-10-2026). En NAudio 3 ISampleProvider lee con Span<float>; las etapas de la rocola de XP leen con
// (float[], offset, count). Para empezar SIN TOCAR los algoritmos (y poder comprobar que salen igual que en XP), cada etapa conserva
// su lectura de siempre y gana una lectura con Span que la llama (Puente.Lee); y las llamadas de siempre a la etapa de delante
// (src.Read(buffer, offset, count)) siguen valiendo con la extension de abajo. Cuesta una copia por etapa; se quita etapa por etapa
// al modernizarlas.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal static class Puente
    {
        [ThreadStatic] private static float[] apoyo;
        // Una lectura con Span hecha con la lectura de siempre, sobre un arreglo de apoyo del hilo.
        internal static int Lee(Func<float[], int, int, int> lee, Span<float> destino)
        {
            float[] a = apoyo;
            if (a == null || a.Length < destino.Length) apoyo = a = new float[Math.Max(destino.Length, 8192)];
            int n = lee(a, 0, destino.Length);
            if (n > 0) a.AsSpan(0, n).CopyTo(destino);
            return n;
        }
        [ThreadStatic] private static byte[] apoyoBytes;
        // Lo mismo para un IWaveProvider (bytes): la salida de 16 bits con dither.
        internal static int LeeBytes(Func<byte[], int, int, int> lee, Span<byte> destino)
        {
            byte[] a = apoyoBytes;
            if (a == null || a.Length < destino.Length) apoyoBytes = a = new byte[Math.Max(destino.Length, 16384)];
            int n = lee(a, 0, destino.Length);
            if (n > 0) a.AsSpan(0, n).CopyTo(destino);
            return n;
        }
        // La lectura de siempre sobre cualquier ISampleProvider de NAudio 3.
        internal static int Read(this ISampleProvider p, float[] buffer, int offset, int count)
        {
            return p.Read(buffer.AsSpan(offset, count));
        }
    }
}

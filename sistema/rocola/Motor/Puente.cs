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
        // Una lectura con Span hecha con la lectura de siempre, sobre un arreglo de apoyo del hilo. UNO POR NIVEL: una etapa lee de la de
        // delante dentro de su propia lectura, y con un solo apoyo la de delante escribia en el arreglo que la de detras estaba usando
        // (la cadena entera salia mal aunque cada etapa sola saliera bien: CompararConXP, cadena entera).
        [ThreadStatic] private static float[][] apoyos;
        [ThreadStatic] private static int nivel;
        internal static int Lee(Func<float[], int, int, int> lee, Span<float> destino)
        {
            float[][] t = apoyos ?? (apoyos = new float[8][]);
            int k = nivel;
            if (k >= t.Length) { Array.Resize(ref t, t.Length * 2); apoyos = t; }
            float[] a = t[k];
            if (a == null || a.Length < destino.Length) t[k] = a = new float[Math.Max(destino.Length, 8192)];
            nivel = k + 1;
            try
            {
                int n = lee(a, 0, destino.Length);
                if (n > 0) a.AsSpan(0, n).CopyTo(destino);
                return n;
            }
            finally { nivel = k; }
        }
        [ThreadStatic] private static byte[] apoyoBytes;
        // Lo mismo para un IWaveProvider (bytes): la salida de 16 bits con dither (la ultima: no hay otra lectura de bytes dentro).
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

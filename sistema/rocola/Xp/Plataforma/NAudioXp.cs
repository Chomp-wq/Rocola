// LO QUE EN XP DA NAUDIO CON WINDOWS, EN LINUX (traduccion de XP, fase 1). La rocola de XP usa NAudio 1.9 entero; en Linux va NAudio 3
// (NAudio.Core, la parte sin Windows), como el motor de Rocola OS. Aqui se traduce lo que cambia:
//  - Puente: en NAudio 3 ISampleProvider lee con Span<float>; las etapas de XP leen con (float[], offset, count). Cada etapa gana la
//    lectura con Span que llama a la suya (una linea, «NAudio 3 (Puente.cs)»), y las llamadas de siempre a la etapa de delante
//    (src.Read(buffer, offset, count)) siguen valiendo con la extension de abajo. Igual que sistema/rocola/Motor/Puente.cs.
//  - Mp3FileReader: el lector de MP3 de NAudio 1.9 traducido (Mp3Xp.cs: el de NAudio 3, Mp3FileReaderBase, ya no hace lo mismo). Su
//    descompresor, lo que en XP era el ACM de Windows, es nativo: Nativo/DescompresorMp3.cs (libmpg123; sin ella, NLayer).
using System;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Rocola
{
    internal static class Puente
    {
        // Una lectura con Span hecha con la lectura de siempre, sobre un arreglo de apoyo del hilo. UNO POR NIVEL: una etapa lee de la de
        // delante dentro de su propia lectura (con un solo apoyo, la de delante escribiria en el que la de detras esta usando).
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
        [ThreadStatic] private static byte[][] apoyosBytes;
        [ThreadStatic] private static int nivelBytes;
        internal static int LeeBytes(Func<byte[], int, int, int> lee, Span<byte> destino)
        {
            byte[][] t = apoyosBytes ?? (apoyosBytes = new byte[8][]);
            int k = nivelBytes;
            if (k >= t.Length) { Array.Resize(ref t, t.Length * 2); apoyosBytes = t; }
            byte[] a = t[k];
            if (a == null || a.Length < destino.Length) t[k] = a = new byte[Math.Max(destino.Length, 16384)];
            nivelBytes = k + 1;
            try
            {
                int n = lee(a, 0, destino.Length);
                if (n > 0) a.AsSpan(0, n).CopyTo(destino);
                return n;
            }
            finally { nivelBytes = k; }
        }
        // La lectura de siempre sobre cualquier ISampleProvider / IWaveProvider de NAudio 3.
        internal static int Read(this ISampleProvider p, float[] buffer, int offset, int count) { return p.Read(buffer.AsSpan(offset, count)); }
        internal static int Read(this IWaveProvider p, byte[] buffer, int offset, int count) { return p.Read(buffer.AsSpan(offset, count)); }
    }

    // (Los descompresores de MP3, lo que en XP era el ACM de Windows, estan en Nativo/DescompresorMp3.cs.)
}

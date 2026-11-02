// EL DESCOMPRESOR DE MP3 DE ROCOLA OS. En XP los MP3 los descomprime el de Windows (ACM) y entrega enteros de 16 bits; la rocola lo pide
// en un solo sitio (DescompresorMp3.Crea, FlotanteNLayer.cs). En Linux no hay ACM: aqui cada cuadro lo descomprime NLayer (el mismo
// que la rocola de XP ya usa para su coma flotante: MotorNLayer) y se entrega en 16 bits, redondeado y recortado como el de Windows.
// Lo demas -el lector de NAudio con su tabla de cuadros, los saltos, la continuidad, y la coma flotante para las canciones que llegan
// al maximo (DecodificacionFlotante)- es lo de XP tal cual.
using System;
using NAudio.Wave;

namespace Rocola.Linux
{
    internal sealed unsafe class Mp3EnteroNLayer : IMp3FrameDecompressor
    {
        private readonly MotorNLayer motor;
        private readonly WaveFormat formato;
        private byte[] flotante = new byte[1152 * 2 * 4 * 2];
        public Mp3EnteroNLayer(WaveFormat mp3) { motor = new MotorNLayer(mp3); formato = new WaveFormat(mp3.SampleRate, 16, mp3.Channels); }
        public WaveFormat OutputFormat { get { return formato; } }
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            int cabe = frame.SampleCount * formato.Channels * 4;
            if (flotante.Length < cabe) flotante = new byte[cabe * 2];
            int bytes = motor.DecompressFrame(frame, flotante, 0);
            int n = bytes / 4;
            if (destOffset + n * 2 > dest.Length) n = (dest.Length - destOffset) / 2;
            fixed (byte* f = flotante) fixed (byte* d = dest)
            {
                float* x = (float*)f; short* y = (short*)(d + destOffset);
                for (int i = 0; i < n; i++)
                {
                    float v = x[i] * 32768f;
                    y[i] = v >= 32767f ? (short)32767 : v <= -32768f ? (short)-32768 : (short)MathF.Round(v, MidpointRounding.AwayFromZero);
                }
            }
            return n * 2;
        }
        public void Reset() { motor.Reset(); }
        public void Dispose() { motor.Dispose(); }
    }
}

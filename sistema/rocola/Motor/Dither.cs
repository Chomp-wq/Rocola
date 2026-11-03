// Los 16 bits con dither triangular (TPDF) que van a la tarjeta (Rocola/Form1.Sonido.cs, tal cual).
using System;
using NAudio.Wave;

namespace Rocola
{
    public partial class Form1
    {
        // Convierte flotante -> 16 bits con DITHER TPDF. El truncado directo (SampleToWaveProvider16) mete
        // distorsion de cuantizacion en pasajes MUY silenciosos (finales de cancion, colas de reverb): el
        // ultimo bit "escalona" la senal y suena a aspereza. El dither triangular de +-1 LSB convierte ese
        // error en un ruido de fondo parejo e inaudible (mucho mas natural, es lo que hace un CD bien
        // masterizado). Redondea en vez de truncar. ~4 operaciones por muestra. (En HD, WASAPI sale en
        // flotante puro y no pasa por aqui; esto ayuda a la salida WaveOut de 16 bits de XP y al respaldo.)
        internal sealed class DitherWaveProvider16 : NAudio.Wave.IWaveProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly NAudio.Wave.WaveFormat fmt;
            private float[] fbuf;
            private uint rs;   // estado xorshift para el dither (mas barato que System.Random)
            public DitherWaveProvider16(NAudio.Wave.ISampleProvider s)
            {
                src = s;
                fmt = FormatoAudio.Pcm(s.WaveFormat.SampleRate, 16, s.WaveFormat.Channels, FormatoAudio.Mascara(s.WaveFormat));
                rs = (uint)Environment.TickCount | 1u;   // semilla != 0 (xorshift no arranca en 0)
            }
            // Un uniforme [0,1) barato (xorshift32). Dos restados = dither TRIANGULAR (TPDF), estadisticamente
            // identico a Random.NextDouble (media 0, varianza 1/6, rango +/-1) pero ~1.5x mas rapido: y esto
            // corre en CADA muestra de 16 bits, SIEMPRE, en el fierro modesto de la rocola.
            private float NextUni() { rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5; return (rs >> 8) * (1f / 16777216f); }
            public NAudio.Wave.WaveFormat WaveFormat { get { return fmt; } }
            public int Read(Span<byte> destino) { return Puente.LeeBytes(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(byte[] buffer, int offset, int count)
            {
                int need = count / 2;                                  // muestras de 16 bits que caben
                if (fbuf == null || fbuf.Length < need) fbuf = new float[need];
                int got = src.Read(fbuf, 0, need);
                int bo = offset;
                for (int i = 0; i < got; i++)
                {
                    // dos uniformes restados = distribucion TRIANGULAR en (-1..1) LSB: el dither correcto
                    // para 16 bits (decorrelaciona el error, sin modular su volumen con la senal).
                    // SILENCIO EXACTO (3-11-2026): donde la cadena da cero exacto (entre canciones, el cebado, los silencios saltados) sale
                    // cero, sin el soplido del dither (con el amplificador a tope era lo unico que se oia en un silencio). Con musica, igual.
                    float m = fbuf[i];
                    if (m == 0f) { buffer[bo++] = 0; buffer[bo++] = 0; continue; }
                    float d = NextUni() - NextUni();
                    float x = m * 32767f + d;
                    int q = (int)Math.Floor(x + 0.5f);                 // redondeo (no truncado)
                    if (q > 32767) q = 32767; else if (q < -32768) q = -32768;
                    buffer[bo++] = (byte)(q & 0xFF);
                    buffer[bo++] = (byte)((q >> 8) & 0xFF);
                }
                return got * 2;
            }
        }

        // LA SALIDA DE 24 BITS (3-11-2026, Rocola OS): con la tarjeta abierta directa (hw) y si admite S32_LE (la HDA del A4: 24 bits
        // utiles en un contenedor de 32), la cadena sale con 24 bits: el mismo redondeo con dither triangular, pero de 1/8388608 (24 bits)
        // en vez de 1/32768; el ruido de esta etapa baja ~48 dB, por debajo del propio de la tarjeta. Cero exacto, cero.
        internal sealed class Salida24en32 : NAudio.Wave.IWaveProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly NAudio.Wave.WaveFormat fmt;
            private float[] fbuf;
            private uint rs;
            public Salida24en32(NAudio.Wave.ISampleProvider s)
            {
                src = s;
                fmt = FormatoAudio.Pcm(s.WaveFormat.SampleRate, 32, s.WaveFormat.Channels, FormatoAudio.Mascara(s.WaveFormat));
                rs = (uint)Environment.TickCount | 1u;
            }
            private double NextUni() { rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5; return (rs >> 8) * (1.0 / 16777216.0); }
            public NAudio.Wave.WaveFormat WaveFormat { get { return fmt; } }
            public int Read(Span<byte> destino) { return Puente.LeeBytes(Read, destino); }
            public int Read(byte[] buffer, int offset, int count)
            {
                int need = count / 4;
                if (fbuf == null || fbuf.Length < need) fbuf = new float[need];
                int got = src.Read(fbuf, 0, need);
                int bo = offset;
                for (int i = 0; i < got; i++)
                {
                    float m = fbuf[i];
                    int q = 0;
                    if (m != 0f)
                    {
                        double x = m * 8388607.0 + (NextUni() - NextUni());
                        double r = Math.Floor(x + 0.5);
                        q = r > 8388607 ? 8388607 : r < -8388608 ? -8388608 : (int)r;
                    }
                    int v = q << 8;   // los 24 bits arriba (S32_LE)
                    buffer[bo++] = (byte)v; buffer[bo++] = (byte)(v >> 8); buffer[bo++] = (byte)(v >> 16); buffer[bo++] = (byte)(v >> 24);
                }
                return got * 4;
            }
        }
    }
}

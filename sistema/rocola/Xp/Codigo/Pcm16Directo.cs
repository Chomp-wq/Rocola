// LOS 16 BITS A COMA FLOTANTE, SIN RODEOS (20 de octubre de 2026; las dos ramas).
//
// El repaso de la biblioteca (LectorMedida) y la reproduccion leian los MP3 (el descompresor del sistema entrega enteros de 16
// bits) y los WAV de 16 bits por el SampleChannel de NAudio, que son cuatro piezas una detras de otra: el convertidor de 16
// bits a coma flotante, el medidor de nivel (sin nadie escuchando: no hace nada), el volumen (a 1: no hace nada) y, ya en la
// rocola, el barrido que pone a cero lo que no es un numero. De todo eso solo cuenta la conversion, y la conversion de NAudio
// DIVIDE cada muestra entre 32768 (una division por muestra, que es de lo mas lento que hace el procesador con numeros de coma
// flotante). Dividir un entero de 16 bits entre 32768 es lo mismo que multiplicarlo por 2^-15: el resultado es exacto en los dos
// casos (un entero de 16 bits cabe sobrado en los 24 bits de una mantisa de float, y multiplicar o dividir por una potencia de
// dos solo mueve el exponente), asi que sale EXACTAMENTE el mismo float, bit a bit. Medido: de 5,3 a 2,8 ns por muestra.
//
// Y un entero de 16 bits entre 32768 esta siempre entre -1 y 1 (nunca NaN ni infinito): el barrido de arriba (LimpiaMuestras) no
// tiene nada que limpiar en ese camino y se omite -mientras el volumen sea 1: con otro volumen se multiplica y se barre como
// siempre-. En los demas formatos (coma flotante de NLayer, WAV de 8, 24 o 32 bits o en coma flotante) todo sigue como antes.
//
// Es una pieza para LEER: no guarda nada, no abre nada, no cierra nada (el lector de origen lo cierra quien lo abrio).
using System;
using NAudio.Wave;

namespace Rocola
{
    internal sealed class Pcm16Directo : ISampleProvider
    {
        // 2^-15: multiplicar por esto es EXACTAMENTE dividir entre 32768f.
        internal const float Escala = 1f / 32768f;
        private readonly IWaveProvider origen;
        private readonly WaveFormat formato;
        private byte[] bytes = new byte[0];
        // El volumen, por si alguien lo pide distinto de 1 (la medida y la reproduccion lo dejan en 1,0: crudo).
        internal float Volumen = 1f;

        // Lo mismo que decide SampleChannel de NAudio para usar su convertidor de 16 bits.
        internal static bool Vale(WaveFormat f)
        { return f != null && f.Encoding == WaveFormatEncoding.Pcm && f.BitsPerSample == 16; }

        internal Pcm16Directo(IWaveProvider origen)
        {
            if (origen == null) throw new ArgumentNullException("origen");
            if (!Vale(origen.WaveFormat)) throw new ArgumentException("No son 16 bits enteros", "origen");
            this.origen = origen;
            formato = WaveFormat.CreateIeeeFloatWaveFormat(origen.WaveFormat.SampleRate, origen.WaveFormat.Channels);
        }

        public WaveFormat WaveFormat { get { return formato; } }

        // Como Pcm16BitToSampleProvider de NAudio (pide el doble de bytes que muestras, convierte lo leido y devuelve la mitad de
        // los bytes), con la multiplicacion exacta en lugar de la division. Con volumen distinto de 1, como VolumeSampleProvider.
        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
        public int Read(float[] buffer, int offset, int count)
        {
            int pedidos = count * 2;
            if (bytes.Length < pedidos) bytes = new byte[pedidos];
            int leidos = origen.Read(bytes, 0, pedidos);
            byte[] b = bytes;
            int salida = offset;
            for (int n = 0; n + 1 < leidos; n += 2) buffer[salida++] = (short)(b[n] | (b[n + 1] << 8)) * Escala;
            int muestras = leidos / 2;
            float v = Volumen;
            if (v != 1f) for (int i = offset; i < offset + muestras; i++) buffer[i] *= v;
            return muestras;
        }
    }
}

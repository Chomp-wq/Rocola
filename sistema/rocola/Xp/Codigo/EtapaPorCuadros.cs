// LA BASE DE LAS TRES ETAPAS DE «VOZ, GOLPE Y RETUMBE» (27 de octubre de 2026): que el bombo no hunda la voz (BomboSinHundir.cs),
// el retumbe dinamico (RetumbeDinamico.cs) y el ataque mas claro (AtaqueClaro.cs).
//
// Las tres necesitan los dos canales del MISMO instante (una sola ganancia para los dos, la imagen estereo no se mueve), asi que
// trabajan por CUADROS ENTEROS, igual que SalaLocal y el grave en mono: en estereo se pide siempre un numero par; si piden una sola
// muestra se hace el cuadro entero aparte y su otra mitad sale en la lectura siguiente (los canales nunca se cruzan); si el origen acaba
// a medio cuadro, lo que queda sale tal cual. Uno o dos canales a 8 kHz o mas; con mas canales (no se sabe que bocina es cual) o a
// menos frecuencia, la etapa pasa tal cual y no cuesta nada.
//
// EL CEBADO (GraveMono.cs). La pista siguiente del mismo disco trae al empezar los cuadros de los retardos de delante (el realce, el
// des-recorte, el siseo), que el relevo tira: tras heredar el estado de la anterior (Sigue) esos cuadros pasan tal cual, sin entrar en
// lo heredado.
//
// Cada etapa dice si esta QUIETA (apagada y ya en su valor neutro: entonces no toca ni una muestra) y hace lo suyo sobre los cuadros.
using System;
using NAudio.Wave;

namespace Rocola
{
    internal abstract class EtapaPorCuadros : ISampleProvider
    {
        protected readonly ISampleProvider src;
        protected readonly int tasa, canales;
        protected readonly bool vale;              // 1 o 2 canales a 8 kHz o mas: si no, pasa tal cual
        internal int Cebado;                       // el cebado de delante (lo pone ArmaCadena): tras heredar, pasa tal cual
        private int cebadoPorPasar;                // muestras
        private bool empezo;
        private readonly float[] sale = new float[2];
        private int saleN;

        protected EtapaPorCuadros(ISampleProvider s)
        {
            src = s; tasa = s.WaveFormat.SampleRate; canales = s.WaveFormat.Channels;
            vale = (canales == 1 || canales == 2) && tasa >= 8000;
        }

        public WaveFormat WaveFormat { get { return src.WaveFormat; } }

        // En la primera lectura: tomar el estado de la anterior del mismo disco, si la hay (y devolver true si se heredo algo).
        protected abstract bool Hereda();
        // Apagada y en su valor neutro: la etapa no toca la muestra (y deja su memoria para que, al encender, parta de cero).
        protected abstract bool Quieta();
        // Los cuadros enteros que hay que procesar.
        protected abstract void Proceso(float[] b, int o, int cuadros);

        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
        public int Read(float[] buffer, int offset, int count)
        {
            if (!vale || canales != 2) return Lee(buffer, offset, count);
            int hechos = 0;
            if (saleN > 0 && count > 0) { buffer[offset] = sale[1]; saleN = 0; hechos = 1; }
            int pares = (count - hechos) & ~1;
            if (pares > 0)
            {
                int n = Lee(buffer, offset + hechos, pares);
                if (n <= 0) return hechos;
                hechos += n;
                if ((n & 1) == 1) return hechos;   // el origen acabo a medio cuadro: lo que queda sale tal cual
            }
            if (count - hechos == 1 && Lee(sale, 0, 2) == 2) { buffer[offset + hechos++] = sale[0]; saleN = 1; }
            return hechos;
        }

        private int Lee(float[] buffer, int offset, int count)
        {
            int n = src.Read(buffer, offset, count);
            if (!vale || n <= 0) return n;
            if (!empezo)
            {
                empezo = true;
                if (Hereda()) cebadoPorPasar = Math.Max(0, Cebado) * canales;
            }
            int o = offset, m = n;
            if (cebadoPorPasar > 0)
            {   // el cebado de la siguiente del mismo disco: sin tocar lo heredado (el relevo lo tira)
                int salta = Math.Min(n, cebadoPorPasar); cebadoPorPasar -= salta; o += salta; m -= salta;
                if (m == 0) return n;
            }
            if (Quieta()) return n;
            Proceso(buffer, o, m / canales);
            return n;
        }
    }
}

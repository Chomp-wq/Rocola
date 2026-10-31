// Lo que se aprende de una cancion al oirla (Rocola/SinPausa.cs).
using System;
using System.Collections.Generic;

namespace Rocola
{
    // ==============================================================================================
    //  LA MEDIDA EN VIVO DE UNA CANCION QUE ENTRA POR RELEVO
    //
    //  La sonoridad y la huella tonal se aprenden oyendo, bloque a bloque, en campos de la rocola que son de
    //  "la cancion que se esta leyendo". Con dos canciones seguidas por la misma salida, la de detras empieza
    //  a leerse en el hilo del sonido antes de que la pantalla se entere. Lo que cada una debe medir va aqui,
    //  preparado de antemano (con su memoria ya reservada): en el PRIMER bloque de la nueva se guarda lo medido
    //  de la anterior en 'Cierre' y se estrena lo de esta. La pantalla cierra 'Cierre' cuando llega el relevo.
    // ==============================================================================================
    internal sealed class MedidaCancion
    {
        internal sealed class Cierre
        {
            internal volatile bool Listo;         // lo lleno el hilo del sonido: ya se puede cerrar
            internal object Fuente;
            internal string Ruta; internal double SumSq; internal long Muestras; internal int Tasa, Canales;
            internal string EqRuta; internal double[] EqSumas; internal long EqN; internal int EqTasa;
        }
        internal readonly string Ruta;            // sonoridad que se aprende (null = ninguna)
        internal object Fuente;
        internal readonly string EqRuta;          // huella tonal que se aprende (null = ninguna)
        internal readonly double[] EqSumas = new double[8];
        internal readonly Cierre CierreAnterior = new Cierre();
        internal MedidaCancion(string ruta, string eqRuta) { Ruta = ruta; EqRuta = eqRuta; }
    }
}

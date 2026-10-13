// LOS RESTOS DIMINUTOS DE LOS FILTROS, A CERO (las dos ramas; 12 de octubre de 2026).
//
// Cuando la musica se calla del todo (ceros exactos: la cola muda de una cancion, una pausa dentro de ella, lo que
// sueltan los retardos de la cadena al acabar), cada filtro con memoria se va apagando poco a poco: su estado baja y
// baja sin llegar nunca a cero, hasta numeros tan pequenos que el procesador ya no los lleva por su camino rapido (los
// "desnormales": por debajo de 1e-38 en float y de 1e-308 en double). Cada cuenta con ellos cuesta decenas de veces
// mas, y la que sale de ellos tambien es diminuta, asi que el silencio se contagia hacia abajo por toda la cadena.
//
// Medido aqui con la cadena de verdad, lo de fabrica y sin saltar el silencio: con musica, la cadena entera gasta unos
// 19 ms por segundo de cancion; en silencio exacto, unos 140 ms, siete veces mas (lo vigila pruebas/desnormales.cs).
// El grave en mono pasaba de 0,8 a 26 ms; el bajo, el modo noche y la medida del original, de uno o dos a trece o
// veinticinco. Y justo ahi, al final de cada cancion, es cuando la pantalla prepara la siguiente.
//
// EL REMEDIO. Cada poco (como mucho cada 64 cuadros) lo que queda por debajo del tope se pone a cero; desde ahi, con
// ceros a la entrada, el filtro se queda en cero exacto y no cuesta nada. Cada poco y no en cada muestra: mientras
// suena la musica la cuenta de cada muestra no cambia; se le suma un contador por cuadro y, cada 64, unas pocas
// comparaciones. Los topes se eligen para que, en lo que tarda la siguiente limpieza, ni el filtro que mas rapido se
// apaga baje a desnormal (ni el cuadrado de lo que se mide al cuadrado). El paso-bajo del realce se apaga mas deprisa
// que eso (a 8 kHz, en unas pocas muestras): ese se limpia en cada muestra.
//
// LO QUE SUENA NO CAMBIA. 1e-15 son 300 dB por debajo del maximo (el bit mas pequeno de 24 bits es 6e-8 y el de
// 16 bits, 3e-5), y 1e-30, 600 dB. Con musica los estados no bajan de ahi, salvo que uno caiga justo en el instante de
// la limpieza a menos de 1e-15 de cero al cruzarlo, y aun asi lo que cambia es de ese tamano. Comparado el ejecutable de
// antes y el de ahora sobre canciones enteras: con musica, las mismas muestras; en el silencio, lo que antes salia
// como un resto diminuto sale como 0 (diferencias de 2e-16 como mucho), y tras el paso a 16 bits, los mismos bytes.
// Es una optimizacion que no cambia ni el sonido ni lo que se ve: siempre activa y sin interruptor.
namespace Rocola
{
    internal static class Desnormales
    {
        internal const int Cada = 64;              // cuadros entre limpieza y limpieza
        internal const float TopeFloat = 1e-15f;   // por debajo, a cero (estados en float)
        internal const double TopeDouble = 1e-30;  // por debajo, a cero (estados en double: su desnormal empieza en 1e-308)

        internal static float Limpia(float v) { return (v < TopeFloat && v > -TopeFloat) ? 0f : v; }
        internal static double Limpia(double v) { return (v < TopeDouble && v > -TopeDouble) ? 0.0 : v; }

        internal static void Limpia(float[] a)
        {
            if (a == null) return;
            for (int i = 0; i < a.Length; i++) { float v = a[i]; if (v < TopeFloat && v > -TopeFloat) a[i] = 0f; }
        }
        internal static void Limpia(double[] a)
        {
            if (a == null) return;
            for (int i = 0; i < a.Length; i++) { double v = a[i]; if (v < TopeDouble && v > -TopeDouble) a[i] = 0.0; }
        }
    }
}

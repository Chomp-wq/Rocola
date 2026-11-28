// ROCOLA OS (la cadena nueva del billar, B; CADENA_NUEVA.md §3 y §1.5, CADENA_NUEVA_B.md): EL BAJO VIRTUAL FIJO (billar_bajo_virtual=, APAGADO de
// fabrica: decide la prueba ciega). Las bocinas del billar no dan lo de debajo de su paso alto; el oido reconstruye un bajo que no suena si oye
// sus armonicos (la «fundamental ausente»). Aqui se hacen esos armonicos SIN NADA QUE SIGA A LA SENAL: ni envolvente, ni detector, ni ganancia
// que cambie (el bajo psicoacustico de hoy, DeepBassProvider, tiene envolvente y por eso no entra en la cadena nueva).
//
// LA NO LINEALIDAD: |x| (rectificar de onda completa) de lo que el paso alto quita (la fuente: el grave de debajo del corte, de fase cero, de la
// misma FFT del tramo, sin lo de debajo de 25 Hz para no hacer armonicos de un retumbe). |x| es HOMOGENEA de grado 1: para todo a >= 0,
// |a x| = a |x|. Lo demas del camino es lineal y fijo (los filtros de abajo y la mezcla), asi que el bajo virtual entero cumple
// BV(a x) = a BV(x): si la cancion sube 20 dB, los armonicos suben exactamente 20 dB; nunca cambia su proporcion con la musica (la prueba lo mide
// a -20 y a 0 dBFS). Es la UNICA funcion sin memoria de una senal que cumple eso y hace armonicos: una f con f(a x) = a f(x) para todo a > 0 es
// f(x) = p x para x > 0 y q x para x < 0, o sea p' x + q' |x|; lo lineal no hace armonicos, asi que es |x| (o la de media onda, max(x, 0) =
// (x + |x|)/2, que es lo mismo mas la fundamental, que el filtro quita). x·|x| o x² no sirven: son de grado 2 (con 20 dB mas, 40 dB mas de
// armonicos). |x| hace solo armonicos pares de cada tono: el 2.o a -7,5 dB de la fundamental (4/3pi), el 4.o 14 dB por debajo de el, el 6.o 21.
//
// LA CONTINUA Y LO QUE SE MUEVE DESPACIO: |x| siempre lleva continua (2/pi de la amplitud en un tono), y esa «continua» sube y baja con el grave
// (es una envolvente). La quita EXACTA un paso alto Butterworth de cuarto orden en el mismo corte que las bocinas (dos bicuadradas, con su cero
// doble en continua: (1 - z^-1)^2 dos veces; la suma de sus coeficientes es 0 exacto en coma flotante): a 10 Hz quita 72 dB.
// ARRIBA: un paso bajo de segundo orden en 4 veces el corte (320 Hz con el corte en 80): solo armonicos en la banda que las bocinas si dan y que
// no suena a zumbido. Las tres bicuadradas, en coma flotante doble y en forma directa II transpuesta.
//
// EL ALIASING: |x| tiene una esquina en cada cruce por cero, asi que sus armonicos bajan solo 12 dB por octava (1/n²). A 44,1 kHz, para doblarse
// a la banda que se queda (80-320 Hz) un armonico tiene que estar cerca de 44 kHz: el 274.o de un tono de 160 Hz, a unos -95 dB de el (la fuente
// casi no tiene nada encima de 2 veces el corte); y lo que se dobla a otras frecuencias lo quita el paso bajo. Medido en --prueba-billar2 con tonos
// de 35, 45 y 70 Hz de periodos justos en la ventana (lo doblado cae fuera de sus armonicos): lo mas alto fuera de ellos, de -102 a -109 dB del
// 2.o armonico. No hace falta sobremuestrear.
//
// LA MEZCLA: MezclaDb (de -14 a -4 dB, -8 de fabrica) es donde queda el 2.o armonico de un tono de la fuente frente a ese tono: lo que se suma
// es 10^(dB/20) / (4/3pi) por lo filtrado.
using System;

namespace Rocola
{
    internal static class RocolaOsBillar2Bajo
    {
        internal const double MezclaDeFabricaDb = -8.0, MezclaMinDb = -14.0, MezclaMaxDb = -4.0;
        internal const double SegundoArmonico = 4.0 / (3.0 * Math.PI);   // el 2.o armonico de |sen| (0,4244)
        internal const double PasoBajoVeces = 4.0;
        internal static double Mezcla(double db) { db = Math.Max(MezclaMinDb, Math.Min(MezclaMaxDb, db)); return Math.Pow(10.0, db / 20.0) / SegundoArmonico; }

        // UNA BICUADRADA (los coeficientes ya divididos entre a0)
        internal struct Bicuadrada
        {
            internal double B0, B1, B2, A1, A2;
            internal static Bicuadrada PasoAlto(int tasa, double f0, double q)
            {
                double w = 2.0 * Math.PI * f0 / tasa, c = Math.Cos(w), al = Math.Sin(w) / (2.0 * q), a0 = 1.0 + al;
                Bicuadrada b;
                b.B0 = (1.0 + c) * 0.5 / a0; b.B1 = -2.0 * b.B0; b.B2 = b.B0;   // (B1 = -2 B0 exacto: la suma de B0 + B1 + B2 es 0 exacto)
                b.A1 = -2.0 * c / a0; b.A2 = (1.0 - al) / a0;
                return b;
            }
            internal static Bicuadrada PasoBajo(int tasa, double f0, double q)
            {
                double w = 2.0 * Math.PI * f0 / tasa, c = Math.Cos(w), al = Math.Sin(w) / (2.0 * q), a0 = 1.0 + al;
                Bicuadrada b;
                b.B0 = (1.0 - c) * 0.5 / a0; b.B1 = 2.0 * b.B0; b.B2 = b.B0;
                b.A1 = -2.0 * c / a0; b.A2 = (1.0 - al) / a0;
                return b;
            }
            // lo que hace en una frecuencia (magnitud)
            internal double Respuesta(int tasa, double f)
            {
                double w = 2.0 * Math.PI * f / tasa, c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
                double nr = B0 + B1 * c1 + B2 * c2, ni = -(B1 * s1 + B2 * s2), dr = 1.0 + A1 * c1 + A2 * c2, di = -(A1 * s1 + A2 * s2);
                return Math.Sqrt((nr * nr + ni * ni) / (dr * dr + di * di));
            }
        }

        // LOS TRES FILTROS para una tasa y un corte: el paso alto de cuarto orden (Butterworth: Q 0,5412 y 1,3066) y el paso bajo de segundo (0,7071)
        internal sealed class Coefs
        {
            internal readonly Bicuadrada[] F = new Bicuadrada[3];
            internal readonly double Corte, Arriba;
            private Coefs(int tasa, double fc)
            {
                Corte = fc; Arriba = Math.Min(PasoBajoVeces * fc, 0.45 * tasa);
                F[0] = Bicuadrada.PasoAlto(tasa, fc, 0.54119610);
                F[1] = Bicuadrada.PasoAlto(tasa, fc, 1.30656296);
                F[2] = Bicuadrada.PasoBajo(tasa, Arriba, 0.70710678);
            }
            internal static Coefs Para(int tasa, double fc) { return new Coefs(tasa, fc); }
            internal double Respuesta(int tasa, double f) { return F[0].Respuesta(tasa, f) * F[1].Respuesta(tasa, f) * F[2].Respuesta(tasa, f); }
        }

        // LO QUE LLEVA CADA ETAPA: el estado de las tres (dos por bicuadrada). Se copia a la pista siguiente del mismo disco.
        internal sealed class Estado
        {
            internal readonly double[] Z = new double[6];
            internal void CopiaDe(Estado o) { Array.Copy(o.Z, Z, Z.Length); }
            // un cuadro: |fuente| por los tres filtros (sin la mezcla)
            internal double Uno(Coefs c, double fuente)
            {
                double x = fuente < 0 ? -fuente : fuente;
                double[] z = Z;
                Bicuadrada[] f = c.F;
                for (int i = 0, j = 0; i < 3; i++, j += 2)
                {
                    double y = f[i].B0 * x + z[j];
                    z[j] = f[i].B1 * x - f[i].A1 * y + z[j + 1];
                    z[j + 1] = f[i].B2 * x - f[i].A2 * y;
                    x = y;
                }
                return x;
            }
            // en el silencio, lo que queda no baja a los numeros desnormales (lentos): a cero de una vez
            internal void Limpia() { for (int i = 0; i < Z.Length; i++) if (Z[i] < 1e-30 && Z[i] > -1e-30) Z[i] = 0.0; }
        }
    }
}

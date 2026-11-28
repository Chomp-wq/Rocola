// LA MEDIDA NUEVA (cadena de sonido nueva, CADENA_NUEVA.md §1.2 y §5): LO MEDIDO DE UNA CANCION. Los datos; el medidor esta en
// RocolaOs.Medida.cs y la tabla, el repaso y la medida al entrar a la cola en RocolaOs.MedidaTabla.cs.
//
// Se mide la cancion ENTERA, decodificada en coma flotante (libmpg123 ENC_FLOAT_32: RocolaOs.CadenaNuevaMp3.cs) y pasada por la cadena nueva
// (la A, o la B con las piezas del billar) a GANANCIA 1 y sin el ecualizador del dueno: lo que sale de la cadena justo antes del ecualizador
// del dueno y de la ganancia. De ahi:
//   Lufs       la sonoridad integrada de ITU-R BS.1770-4 (ponderacion K, bloques de 400 ms con 75 % de solape, puertas de -70 LUFS y de
//              -10 LU), en LUFS
//   TpDb       el pico real (anexo 2 de BS.1770: sobremuestreo 4x con un FIR de fase lineal), en dBTP
//   PicoDb     el pico de muestra, en dBFS
//   Lra        el rango de sonoridad (EBU Tech 3342: ventanas de 3 s, puertas de -70 LUFS y -20 LU, del percentil 10 al 95), en LU
//   Tercios    el espectro de potencia en tercios de octava (30 bandas, de 20 Hz a 20 kHz: Hz(k)), en dB respecto del total: con el se estima
//              cuanto cambia la sonoridad el ecualizador del dueno y el tono parejo (RocolaOs.CadenaNuevaGanancia.cs)
//   y los ARREGLOS FIJOS, de lo que sale del decodificador (antes de la cadena): CanalMudo (un canal 30 dB o mas por debajo de la cancion entera),
//   Invertido (la correlacion del grave, por debajo de 150 Hz, de -0,6 o menos con grave de verdad), ContinuaL/R (la media de cada canal) y
//   ZumbidoHz (el que mide el repaso de XP, T6: se usa el de su tabla al medir, y se guarda con la medida).
//   (28-11-2026) PicosEq: el pico de muestra de lo que deja pasar la sonda de cada banda del ecualizador del dueno (RocolaOsSondasEq), para
//   estimar cuanto sube el pico real con su curva sin suponer lo maximo (RocolaOsPicosEq); CorteHz: el corte de agudos de la cancion (el
//   percentil 90 del acantilado de cada FFT con musica), para el DSEE de agudos_mp3 con su corte fijo.
// Si la cancion necesita un arreglo, la medida se repite con el arreglo puesto (segunda pasada): lo guardado es SIEMPRE lo que sale de la
// cadena tal como va a sonar.
using System;
using System.Globalization;

namespace Rocola
{
    internal sealed class RocolaOsMedidaNueva
    {
        internal const int Algoritmo = 1;
        internal const int Bandas = 30;                    // tercios de octava: 1000 * 2^(k/3), k = -16..13 (19,7 Hz a 20,2 kHz)
        internal static double Hz(int banda) { return 1000.0 * Math.Pow(2.0, (banda - 16) / 3.0); }

        internal string Ruta;
        internal char Cadena = 'A';
        internal string HuellaB = "-";                     // con la B: RocolaOsCadenaB.HuellaAhora cuando se midio
        internal long Largo, Creada, Escrita;              // el sello del archivo (como rocola_fuentes.txt; 0 0 0 para Spotify)
        internal int Alg = Algoritmo;
        internal double Segundos;
        internal double Lufs = double.NegativeInfinity;
        internal double TpDb = -200.0, PicoDb = -200.0;
        internal double Lra = -1.0;                        // -1: no hay (menos de 3 s)
        internal float[] Tercios;                          // null: sin espectro
        internal int CanalMudo = -1;
        internal bool Invertido;
        internal float ContinuaL, ContinuaR;
        internal int ZumbidoHz;
        internal long Unix;
        internal float[] PicosEq;                          // (28-11-2026) el pico de muestra de cada sonda del EQ del dueno (10, dBFS; null: medida de antes)
        internal float CorteHz;                            // (28-11-2026) el corte de agudos de la cancion (el DSEE de agudos_mp3); 0: sin medir
        internal bool Muda { get { return double.IsNegativeInfinity(Lufs) || Lufs < -69.0; } }
        internal bool ConArreglos { get { return CanalMudo >= 0 || Invertido || ContinuaL != 0f || ContinuaR != 0f || ZumbidoHz > 0; } }

        internal static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        internal string Resumen()
        {
            return Cadena + ": " + (Muda ? "muda" : F(Lufs, "0.0") + " LUFS") + ", pico real " + F(TpDb, "0.0") + " dBTP (muestra " + F(PicoDb, "0.0")
                + "), LRA " + (Lra >= 0 ? F(Lra, "0.0") : "-") + (CanalMudo >= 0 ? ", canal " + (CanalMudo == 0 ? "izquierdo" : "derecho") + " mudo" : "")
                + (Invertido ? ", canal invertido" : "") + (ContinuaL != 0f || ContinuaR != 0f ? ", continua " + F(ContinuaL, "0.0000") + "/" + F(ContinuaR, "0.0000") : "")
                + (ZumbidoHz > 0 ? ", zumbido " + ZumbidoHz + " Hz" : "");
        }
    }
}

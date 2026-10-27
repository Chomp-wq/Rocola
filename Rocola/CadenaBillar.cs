// LA CADENA DEL MODO BILLAR (27 de octubre de 2026). El modo automatico del local (LocalAuto.cs) ya no solo enciende piezas: ajusta la
// cadena de sonido entera para las bocinas del billar -dos medios de 6,5" y dos tweeters de corneta en el gabinete y tres cajas de 10 a
// 12" repartidas por zonas, sin subwoofer, en una nave de lamina, ladrillo y loseta con gente y bolas-. No es otra cadena: es la misma,
// con cada pieza afinada para que encaje con las demas. Con el modo apagado nada de esto se usa y la cadena sale bit a bit como antes.
//
// Lo que aqui se decide se midio con la cadena real (banco de la cadena, pruebas/cadenabillar.cs) en canciones sinteticas: pop y
// reggaeton masterizados, una balada de mucho rango y una cumbia vieja delgada. Nada se ha oido en el billar: son puntos de partida de
// ingenieria, faciles de cambiar aqui mismo.
//
// EL EQ AUTOMATICO A LA MEDIDA DE ESTAS BOCINAS (BillarEq*). El EQ de siempre corrige cada cancion hacia "lo bien grabado" de la
// biblioteca, +-4 dB por banda. En el billar eso chocaba con el local y con otras piezas:
//   - 50 Hz: lo subia hasta +3,4 dB, donde ninguna bocina del billar da casi nada (solo gastaba amplificador y margen) y en las
//     grabaciones viejas cortadas en grave subia el retumbo de la cinta, no musica. Ahora solo se BAJA, y no se toca si la grabacion viene
//     cortada por debajo de 60 Hz (EqCutLo).
//   - 400 Hz: con la claridad (-2,5 dB a 300 Hz) llegaba a -5,3 dB: la musica se quedaba sin cuerpo. Ahora como mucho -2,5 (antes de la
//     fuerza) y apenas sube.
//   - 2,5 y 5,6 kHz: con la claridad (+1,5 dB a 3 kHz) y el realce llegaba a +5 dB justo en la franja aspera que la loseta y la lamina
//     devuelven. 5,6 kHz solo BAJA; 2,5 kHz sube como mucho 1 dB.
//   - 15,8 kHz: pegada a la mitad de la frecuencia de muestreo el filtro sale torcido, y los tweeters del gabinete ahi ya no dan. No se toca.
//   - Un poco menos de agudos de fabrica (InclinacionBillar: -1,5 dB a 5,6 kHz y -1 dB a 10 kHz en el objetivo), por el rebote del local.
//   - Mas prudencia: una banda solo se toca si queda claramente por debajo del corte del codec (centro <= corte / 1,25: el filtro de la
//     huella tiene faldas), una banda casi muda (<= -20 dB) no se toca, una cancion de menos de 45 s (intros, cortinillas, voz sola) no
//     se ecualiza, una con picos aplastados de origen no recibe refuerzos por encima de 2 kHz (su espectro lleva la salpicadura del
//     recorte), una cancion rara se toca todavia con menos fuerza (piso 0,35), y lo que corregiria menos de 0,4 dB se deja (solo
//     encenderia filtros que no se oyen).
//
// LA PRESENCIA Y LAS PARTES SUAVES MIDEN CONTRA LA PROPIA CANCION (NivelLocalDe). Las dos se pensaron (22-10) cuando el nivelado
// llevaba todas las canciones al mismo volumen objetivo (normTarget): «6 dB bajo el nivel de siempre» y «12 dB bajo el nivel de
// siempre» eran, en la practica, 6 y 12 dB bajo la cancion. Desde el nivelado al limite (24-10) cada cancion sube hasta su techo y
// suena muy por encima de ese objetivo, asi que la presencia apretaba casi toda la cancion (y, con su compensacion topada en 3 dB, la
// dejaba 1 a 3 dB MAS BAJA en vez de acercar lo suave a lo fuerte) y las partes suaves casi nunca encontraban algo tan bajo. En el billar
// la referencia es el nivel al que de verdad suena ESTA cancion: su sonoridad medida por la ganancia que le pone el nivelado (en la
// misma escala que normTarget). Sin medida, la de siempre.
using System;

namespace Rocola
{
    public partial class Form1
    {
        // Por banda (50, 158, 400, 1000, 2500, 5600, 10000, 15800 Hz): cuanto puede subir y cuanto bajar, ANTES de la fuerza del EQ.
        internal static readonly float[] BillarEqSube = { 0f, 1.5f, 0.5f, 2.0f, 1.0f, 0f, 1.5f, 0f };
        internal static readonly float[] BillarEqBaja = { -3f, -3.5f, -2.5f, -3f, -4f, -4f, -3f, 0f };
        // Lo que se le quita al objetivo en el billar (dB), por el rebote del local.
        internal static readonly float[] InclinacionBillar = { 0f, 0f, 0f, 0f, 0f, -1.5f, -1f, 0f };
        internal const float BillarEqMinSeg = 45f;        // mas corta: no se ecualiza
        internal const float BillarEqCortaBajoHz = 60f;   // grabacion cortada por debajo de esto: la banda de 50 Hz no se toca
        internal const float BillarEqRecorte = 0.005f;    // fraccion de muestras aplastadas desde la que no hay refuerzos sobre 2 kHz
        internal const float BillarEqMuda = -20f;         // banda casi muda (dB frente al cuerpo): no se toca
        internal const float BillarEqRaraMin = 0.35f;     // el piso de la fuerza para una cancion rara
        internal const float BillarEqZonaMuerta = 0.4f;   // lo que corregiria menos de esto, no se toca (y a lo demas se le resta)
        internal const float BillarEqMargenCorte = 1.25f; // una banda solo se toca si su centro <= corte del codec / esto

        // CANCION FLACA: le falta cuerpo (las bandas de 158 y 400 Hz de su huella, de media) frente a su objetivo del EQ en al menos esto. A esas
        // no se les pone la claridad (que corta 2,5 dB a 300 Hz) y el EQ les devuelve cuerpo (mas cuerpo en voces, CorreccionBillar).
        internal const float CuerpoFlacaDb = 2.5f;

        // Cuanto cuerpo le falta a esta cancion (dB, positivo = le falta) frente al objetivo del EQ; NaN si no hay huella u objetivo.
        internal float FaltaDeCuerpoDb(string path)
        {
            float[] c, avg = eqAvg;
            if (path == null || avg == null || eqCache == null || !eqCache.TryGetValue(path, out c) || c == null || c.Length < EqLen
                || c[EqFingerprint] != EqVersionOriginal) return float.NaN;
            if (c[1] <= -25f || c[2] <= -25f) return float.NaN;
            float[] meta = MetaDeCancion(c) ?? avg;
            return 0.5f * ((meta[1] - c[1]) + (meta[2] - c[2]));
        }
        // L1, igual que el LRA: en racha con otra de su disco decide el disco (la media de sus pistas medidas), para que no cambie entre pistas.
        private float FaltaDeCuerpoParaDecidir(Track t)
        {
            float propio = FaltaDeCuerpoDb(t.Path);
            if (queue == null) return propio;
            System.Collections.Generic.List<Track> disco = RachaDeDisco(t);
            if (disco == null) return propio;
            float suma = 0f; int n = 0;
            for (int i = 0; i < disco.Count; i++)
            {
                Track tr = disco[i];
                if (tr == null || tr.Path == null) continue;
                float f = FaltaDeCuerpoDb(tr.Path);
                if (!float.IsNaN(f)) { suma += f; n++; }
            }
            return n > 0 ? suma / n : propio;
        }

        // La referencia de la presencia y las partes suaves para esta cancion: en el billar, el nivel al que suena (sonoridad x ganancia del
        // nivelado, sin el volumen general); con el modo apagado o sin medida, normTarget de siempre.
        private float NivelLocalDe(Track t, float ganancia)
        {
            if (!localAuto || t == null) return normTarget;
            float rms;
            if (!SonoridadParaNivelar(t.Path, out rms) || float.IsNaN(ganancia) || float.IsInfinity(ganancia) || ganancia <= 0f) return normTarget;
            float v = rms * ganancia;
            if (float.IsNaN(v) || float.IsInfinity(v)) return normTarget;
            return v < 0.02f ? 0.02f : (v > 1f ? 1f : v);
        }

        // EL REALCE DE BRILLO, EL DSEE Y LAS MEJORAS CON IA EN EL BILLAR.
        //   - Realce: fabrica armonicos por encima de 5,6 kHz, justo donde el EQ del billar BAJA (InclinacionBillar). En una cancion completa
        //     se peleaban (uno quita, el otro pone, y lo que queda es aspereza); en el billar el realce solo devuelve lo que el codec corto: 0
        //     en una cancion completa (corte de 19,2 kHz o mas) y entero en una cortada en 15 kHz o menos, de forma continua entre medias.
        //   - DSEE (reconstructor espectral): apagado en el billar. Recrea la banda de 16 a 20 kHz, que los tweeters de corneta apenas dan y la
        //     nave (gente, loseta) se come; lo que si se oye de esa falta lo cubre el realce de arriba, y se ahorra procesador en el A4.
        //   - Siseo (I2): apagado en el billar. A volumen de bar el siseo de una cinta queda muy por debajo del ruido del local, y quitarlo se
        //     llevaria tambien algo del aire de la musica (y gasta procesador).
        //   - A la medida (I1): encendido en el billar. El EQ automatico ya usa el objetivo de cada cancion (MetaDeCancion); el bajo
        //     psicoacustico se compara con la misma vara, para que no midan cosas distintas. Sin modelo del EQ no cambia nada.
        //   - La cantidad de realce, la de fabrica (RealceBillarCantidad) y el bajo psicoacustico, en automatico: los decide el modo, no la barra.
        //   - Los arreglos de la grabacion (un canal mudo, la continua y el zumbido): siempre. Solo actuan en las canciones con ese defecto.
        // El interruptor guardado de cada uno se conserva y vuelve al apagar el modo.
        private bool DseeEfectivo { get { return sbrOn && !localAuto; } }
        internal const float RealceBillarCantidad = 0.35f;   // la de fabrica
        private static bool CanalMudoEf { get { return canalMudo || localAuto; } }
        private static bool QuitaContinuaEf { get { return quitaContinua || localAuto; } }
        private static bool QuitaZumbidoEf { get { return quitaZumbido || localAuto; } }
        internal const float RealceBillarSinMedir = 0.5f;   // aun sin medir el corte: la mitad
        internal static float RealceBillar(float factorDeSiempre)
        {
            float f = (factorDeSiempre - ExciteCompleta) / (1.45f - ExciteCompleta);
            return f < 0f ? 0f : (f > 1f ? 1f : f);
        }

        // ¿Toca el EQ la banda b de esta huella en el billar? (Ademas de lo de siempre.)
        internal static bool BandaBillar(int b, float[] huella, float corte)
        {
            if (b == 7) return false;
            if (corte > 100f && EqCentros[b] * BillarEqMargenCorte > corte) return false;
            if (b == 0 && huella != null && huella.Length > EqCutLo && huella[EqCutLo] > BillarEqCortaBajoHz) return false;
            return true;
        }
        // La correccion de una banda en el billar a partir de lo que pide el objetivo (meta - huella), ya con la fuerza. Pura.
        internal static float CorreccionBillar(int b, float pide, float fuerza, bool recortada) { return CorreccionBillar(b, pide, fuerza, recortada, false); }
        // 'flaca': a la cancion le falta cuerpo y no lleva la claridad (que ya corta a 300 Hz): MAS CUERPO EN VOCES. 158 y 400 Hz pueden subir
        // mas (BillarEqSubeFlaca). Se penso hacerlo con armonicos generados en la zona de la voz (como el bajo psicoacustico, una octava mas arriba),
        // y no: en una nave de lamina y loseta los armonicos entre 500 Hz y 3 kHz suman aspereza, justo lo que la cadena del billar quita. El cuerpo
        // de una cancion flaca esta en 150-400 Hz y se le devuelve ahi, con el EQ, sin gastar procesador; si llegara a retumbar, el retumbe
        // dinamico (que mide despues) lo recorta en ese instante.
        internal static readonly float[] BillarEqSubeFlaca = { 0f, 3.0f, 2.5f, 2.0f, 1.0f, 0f, 1.5f, 0f };
        internal static float CorreccionBillar(int b, float pide, float fuerza, bool recortada, bool flaca)
        {
            float sube = flaca ? BillarEqSubeFlaca[b] : BillarEqSube[b];
            if (recortada && b >= 4) sube = 0f;
            float corr = pide > sube ? sube : (pide < BillarEqBaja[b] ? BillarEqBaja[b] : pide);
            corr *= fuerza;
            if (corr > BillarEqZonaMuerta) corr -= BillarEqZonaMuerta;
            else if (corr < -BillarEqZonaMuerta) corr += BillarEqZonaMuerta;
            else corr = 0f;
            return corr;
        }
    }
}

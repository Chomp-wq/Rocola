// LAS CADENAS DE SONIDO NUEVAS (plan aprobado por el dueno el 27-11-2026: CADENA_NUEVA.md): EL CONTRATO DE LAS PIEZAS (§8 de CADENA_NUEVA.md).
// Lo usan las dos cadenas nuevas: la normal (A, RocolaOs.CadenaNueva*.cs) y la del billar (B: sus piezas, RocolaOs.Billar2*.cs, las pone otro
// agente). Lo que aqui se fija:
//
//   UNA PIEZA es un ISampleProvider de NAudio 3 (como todas las etapas de la cadena de hoy: Read(Span<float>) con Puente.Lee y
//   Read(float[], int, int)), float, DOS canales, a la tasa de la cancion, armada con sus parametros FIJOS antes de sonar (nada que siga a la
//   musica: ni envolventes, ni detectores, ni compresores). SIN RETARDO: cada cuadro sale en su sitio (si necesita lo que viene, lee por
//   delante, como el grave en mono de hoy). Y ademas IRocolaOsPieza: el DISCO CONTINUO (Sigue, como Sigue/Hereda de las etapas de hoy).
//
//   LA RECETA (RocolaOsReceta): lo que va a sonar, decidido ANTES de sonar con lo medido de la cancion: la cadena (A o B), la ruta, la tasa,
//   lo medido (RocolaOsMedidaNueva), los arreglos fijos, las piezas en orden (Anota) y la ganancia. Se arma en el hilo de la pantalla (al
//   empezar una cancion o al preparar el relevo) o en el hilo de la medida (Midiendo = true: entonces nada del estado de XP).
//
//   LA CADENA B: el agente del billar pone en RocolaOsCadenaB.Arma (desde su [ModuleInitializer]) lo que va ENTRE los arreglos fijos y el
//   ecualizador del dueno: recibe la receta y lo que sale de los arreglos (float, 2 canales, la tasa de la cancion, con el canal mudo copiado, el
//   invertido volteado, la continua quitada y el zumbido) y devuelve lo que sigue (float, 2 canales, la misma tasa, sin retardo; sus piezas,
//   cada una anotada con r.Anota en orden). Lo de despues (el ecualizador del dueno con la memoria del billar, la ganancia, la noche y la
//   salida) lo pone el marco. Mientras Arma sea null, la B lleva una pieza de paso (identidad) en ese sitio. RocolaOsCadenaB.Huella dice en un
//   texto corto los parametros de sus piezas (el paso alto, la afinacion...): si cambia, lo medido con la B ya no vale y se mide otra vez.
using System;
using System.Collections.Generic;
using System.Threading;
using NAudio.Wave;

namespace Rocola
{
    // UNA PIEZA DE LAS CADENAS NUEVAS (ademas de ISampleProvider): el disco continuo.
    internal interface IRocolaOsPieza : ISampleProvider
    {
        // La pista que acaba entera, sin fundido, y la que entra es la siguiente de su disco: 'anterior' es la MISMA pieza (el mismo tipo, en el
        // mismo puesto de la receta) de la cadena que acaba. Se llama en el hilo de la pantalla ANTES de la primera lectura; la pieza la toma en
        // su primera lectura (en el hilo del sonido), solo si cuadra (misma tasa, canales y parametros), y sigue con su memoria: sin escalon en el
        // cambio de pista. Si no cuadra, empieza de cero. (Lo mismo que Sigue/Hereda de GraveMono, SalaLocal o EcualizadorDueno.)
        void Sigue(IRocolaOsPieza anterior);
    }

    // LA RECETA DE UNA CANCION EN UNA CADENA NUEVA: todo lo que suena, decidido antes de sonar. La arma RocolaOs.CadenaNueva.cs.
    internal sealed class RocolaOsReceta
    {
        internal char Cadena = 'A';                 // 'A' (la normal) o 'B' (la del billar)
        internal string Ruta;                       // la de la cancion (o «spotify:track:ID»)
        internal int Tasa;                          // la de lo que entra a las piezas (la de la cancion)
        internal const int Canales = 2;             // siempre dos (un MP3 mono llega como dos canales iguales)
        internal bool Midiendo;                     // en la medida (hilo de fondo, sin la pantalla): ni el estado de XP ni nada de la pantalla
        internal RocolaOsMedidaNueva Medida;        // lo medido de esta cancion con esta cadena (null: aun no; suena con la estimacion)
        // LOS ARREGLOS FIJOS (decididos por la medida: RocolaOsMedidaNueva.Arreglos; sin medida, ninguno)
        internal int ZumbidoHz;                     // 50 o 60 si el repaso de XP midio el zumbido de la corriente (T6); 0 si no
        internal int CanalMudo = -1;                // -1 ninguno; 0: el izquierdo no suena (se le copia el derecho); 1: el derecho
        internal bool Invertido;                    // el derecho va al reves (su grave en contrafase): se voltea
        internal float ContinuaL, ContinuaR;        // la continua de cada canal (se resta)
        // LO OPCIONAL (cada uno con su interruptor, apagado de fabrica: decide la prueba ciega; solo la A)
        internal bool Agudos;                       // agudos_mp3: el DSEE y el relleno de huecos (R1)
        internal float[] TonoDb;                    // tono_parejo: la curva fija por cancion (dB por banda de Form1.EqCentros); null = sin
        // LA GANANCIA (una, fija toda la cancion o el disco: RocolaOs.CadenaNuevaGanancia.cs)
        internal double GananciaDb;
        internal string Como = "";                  // de donde salio (la bitacora y la hoja)
        // (28-11-2026) lo de ESTA cancion con lo que se decidio la ganancia, para el tope si el dueno mueve el EQ mientras suena (a1) y para el
        // disco continuo (a3): su pico real antes del EQ del dueno (medido o estimado), los picos de sus sondas (o null: los tipicos), lo que le
        // suman al pico el tono parejo y los agudos, y su techo (C menos su pico real con el EQ de cuando se armo)
        internal double TpAntesEqDb = double.NaN;
        internal float[] PicosEq;
        internal double DtpOtrosDb;
        internal double TechoDb = double.NaN;
        // LAS PIEZAS, en orden (para el disco continuo: la pieza k de esta sigue a la pieza k de la anterior si son del mismo tipo)
        internal readonly List<IRocolaOsPieza> Piezas = new List<IRocolaOsPieza>();
        internal T Anota<T>(T p) where T : IRocolaOsPieza { if (p != null) Piezas.Add(p); return p; }
    }

    // LA CADENA B (las piezas del billar): las pone el agente del billar desde su [ModuleInitializer] (RocolaOs.Billar2*.cs). Mientras falten, de paso.
    internal static class RocolaOsCadenaB
    {
        // (receta, lo que sale de los arreglos fijos) -> lo que va al ecualizador del dueno. Corre en el hilo de la pantalla (al armar) o en el de
        // la medida (r.Midiendo): nada pesado (las tablas de filtros, una vez y guardadas) y, midiendo, nada del estado de XP. null: de paso.
        internal static Func<RocolaOsReceta, ISampleProvider, ISampleProvider> Arma;
        // Los parametros de sus piezas en un texto corto (para saber si lo medido con la B sigue valiendo); null: «sin-piezas».
        internal static Func<string> Huella;
        // (la ultima, un segundo: el repaso la pregunta por cada cancion de la biblioteca y sus ajustes no cambian a cada rato)
        private static string huellaUltima; private static long huellaMs = long.MinValue;
        internal static string HuellaAhora
        {
            get
            {
                if (Arma == null) return "sin-piezas";
                long ahora = Environment.TickCount64;
                string u = Volatile.Read(ref huellaUltima);
                if (u != null && ahora - Interlocked.Read(ref huellaMs) < 1000) return u;
                string s;
                try { Func<string> h = Huella; s = h != null ? h() : null; s = string.IsNullOrEmpty(s) ? "piezas" : s.Replace('\t', ' ').Replace('\n', ' '); }
                catch { s = "piezas"; }
                Volatile.Write(ref huellaUltima, s); Interlocked.Exchange(ref huellaMs, ahora);
                return s;
            }
        }
    }

    // LA PIEZA DE PASO: identidad (el sitio de la B mientras no esten sus piezas, y lo que no hace nada). No toca ni una muestra.
    internal sealed class RocolaOsPiezaDePaso : IRocolaOsPieza
    {
        private readonly ISampleProvider src;
        internal RocolaOsPiezaDePaso(ISampleProvider s) { src = s; }
        public WaveFormat WaveFormat { get { return src.WaveFormat; } }
        public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
        public int Read(float[] buffer, int offset, int count) { return src.Read(buffer, offset, count); }
        public void Sigue(IRocolaOsPieza anterior) { }
    }
}

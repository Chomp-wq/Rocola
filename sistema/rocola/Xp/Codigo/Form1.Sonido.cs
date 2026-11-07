// EL SONIDO: la cadena de audio, el nivelado, el EQ y el reconstructor de agudos
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {
        private float rmsLo, rmsMid, rmsHi;            // rms retrasado por banda (lo que se oye AHORA)
        private Dictionary<string, float> loudCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);   // ruta -> ESTIMACION de sonoridad (tramos u oida); la integrada BS.1770 va en integCache
        private Dictionary<string, float[]> eqCache = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        private float[] eqAvg = null;              // curva PROMEDIO de la biblioteca (el objetivo); null hasta tener 10 canciones medidas
        private float eqAmount = 0f;               // fuerza del EQ automatico 0..1 A MANO (perilla en Ctrl+F3; "eq_auto=" en config); 0 = apagado
        // AUTOMATICO (de fabrica): no hay perilla que ajustar. La fuerza del EQ y la cantidad de bajo las
        // decide el propio programa cancion por cancion, con lo que el analisis del DSEE ya midio de cada una
        // (acantilado de agudos, recorte de picos, corte por abajo y huella tonal). Ver EqFuerzaAuto/BajoAuto.
        private bool eqAuto = true;                // "eq_automatico=" en config
        private bool bassAuto = true;              // "bajo_automatico=" en config
        private float bassAutoBase = 0.30f;        // lo que el programa eligio para la cancion que suena
        private string eqMeasPath = null;          // cancion cuya huella se esta midiendo (null = no medir)
        private float eqMeasCut = 0f, eqMeasCutLo = 0f;   // cortes (arriba y abajo) de la cancion que suena (0 = aun no se sabe)
        private double[] eqMeasSum = new double[8]; private long eqMeasN = 0;   // energia acumulada por banda (hilo de audio)
        private float[][] eqMeasCoef = null; private float[] eqMeasSt = null; private int eqMeasRate = 0;   // medicion: 4 estados por banda y canal
        private float[] eqPend; private int eqPendN;   // un cuadro que una lectura dejo partido, para completarlo con la siguiente
        // BAJO PSICOACUSTICO: 0..1 ("bajo=" en config, perilla en Ctrl+F3). Recrea el grave que las bocinas
        // del local NO pueden dar, generando sus armonicos. Ver DeepBassProvider.
        private float bassAmount = 0.30f;
        // AJUSTE POR CANCION. La perilla dice cuanto grave quiere el dueno del local; ESTE numero dice cuanto
        // le toca a la cancion que suena. No todas necesitan lo mismo: una con mucho grave profundo pierde
        // mucho en unas bocinas chicas y agradece la ayuda; una que ya viene cargada de bajo-medio no necesita
        // nada y si le metes mas, se emborrona. Sale de la huella tonal que la rocola YA le mide a cada cancion
        // (no hace falta medir nada nuevo). 1 = la perilla tal cual, que es lo que hacia antes con todas.
        private float bassSongFactor = 1f;
        private float BassBaseNow() { return bassAuto || localAuto ? bassAutoBase : bassAmount; }   // el modo billar lo pone en automatico (CadenaBillar.cs)
        private float BassAmountNow() { return Clampf(BassBaseNow() * bassSongFactor * BajoMedido, 0f, 1f); }   // BajoMedido: 1 salvo en el billar afinado (AfinarLocal.cs)
        // Lo mismo para una cancion que aun no suena (la siguiente, preparada para entrar sin pausa).
        private float BassAmountPara(float factor, float autoBase) { return Clampf((bassAuto || localAuto ? autoBase : bassAmount) * factor * BajoMedido, 0f, 1f); }
        // LO PROPIO DE ESTA RAMA PARA EL RELEVO SIN PAUSA (Form1.SinPausa.cs): sin IA en vivo, el reconstructor lo
        // decide el interruptor para todas las canciones, y el colchon de WaveOut es fijo.
        private bool DseeDe(CadenaCancion c) { return DseeEfectivo; }
        private float ExcitePara(CadenaCancion c) { return ExciteAmountFor(c.Cancion.Path); }
        private int RetrasoAudible() { return RetardoVisualSalida() + AdelantoBomba(); }   // snapshot de la salida abierta + adelanto de la bomba
        private bool IaMejoradaPara(string path) { return false; }
        private void AlEntrarPorRelevo(CadenaCancion b) { }

        // ============================== MODO NOCHE (VUELVE: decision del dueno del 7-11-2026) ==============================
        // «El otro dia nos aventaron piedras por tener musica a las 12 A.M, asi que volveremos a poner el modo noche, con un slider desde
        // el 70 al 90% del audio, y con un boton para probar como se escucharia cuando se active.» (Se habia retirado el 24-10-2026.)
        //
        // De las 11 de la noche a las 7 de la manana (noche_desde=, noche_hasta=) la rocola suena al nochePct % (noche_pct=, de 70 a 90,
        // 80 de fabrica; interruptor noche=, ENCENDIDO de fabrica), musica y videos.
        //
        // EL PORCENTAJE ES DE LO QUE SE OYE, NO DE LA SENAL. Lo que se aprendio la primera vez: multiplicar la senal por 0,8 son -1,9 dB, y
        // eso casi no se nota (el ajuste parecia roto). El oido percibe el doble de fuerte con +10 dB, asi que «al 70 %» es 10*log2(0,70) =
        // -5,1 dB; al 80 %, -3,2 dB; al 90 %, -1,5 dB.
        //
        // Y BAJA POCO A POCO: veinte segundos de rampa (de golpe, a media cancion, suena a averia). «Probar» lo pone 15 s al momento (rampa
        // de 1,5 s, para oir la diferencia) y vuelve solo.
        private static bool nocheOn = true;                        // "noche=" en config (0 lo apaga del todo)
        private static int nocheDesde = 23;                        // "noche_desde="
        private static int nocheHasta = BusinessDayStartHour;      // "noche_hasta=" (las 7: cuando empieza el dia del negocio)
        private static int nochePct = 80;                          // "noche_pct=": de 70 a 90 (lo que se oye)
        internal const int NochePctMin = 70, NochePctMax = 90;
        private const int NocheRampaMs = 20000, NochePruebaRampaMs = 1500, NochePruebaMs = 15000;
        private float nocheFactor = 1f;                            // donde esta la rampa AHORA (1 = de dia)
        private int nocheUltMs;                                    // reloj de la ultima vuelta
        private bool nocheArrancada, nocheAvisada;
        private int nochePruebaHasta;                              // «Probar»: hasta cuando (TickCount; 0 = no)
        private bool nochePruebaRapida;                            // la rampa rapida de la prueba (al entrar y al salir)

        private static float NocheDb() { return (float)(10.0 * Math.Log(Math.Max(1, Math.Min(100, nochePct)) / 100.0, 2.0)); }
        private static int linDe = -1; private static float linVal = 1f;
        private static float NocheLin() { int p = nochePct; if (p != linDe) { linVal = (float)Math.Pow(10.0, NocheDb() / 20.0); linDe = p; } return linVal; }   // (una vez por porcentaje)

        // El rango CRUZA LA MEDIANOCHE: «hora >= 23 && hora < 7» es siempre falso. Va un O, no un Y (con su prueba: pruebas/noche.cs).
        private static bool EsDeNoche(DateTime t)
        {
            int h = t.Hour, a = nocheDesde, b = nocheHasta;
            if (a == b) return false;              // rango vacio
            if (a < b) return h >= a && h < b;     // rango normal, dentro del mismo dia
            return h >= a || h < b;                // el que cruza la medianoche
        }
        private bool ProbandoNoche { get { return nochePruebaHasta != 0 && unchecked(Environment.TickCount - nochePruebaHasta) < 0; } }
        // A donde TIENE que llegar la rampa: 1 de dia; de noche (o probando), lo que diga el deslizador.
        // (la hora se mira una vez por segundo, no en cada vuelta del temporizador de animacion)
        private bool nocheAhora; private int nocheMirada;
        private bool EsDeNocheAhora()
        {
            int t = Environment.TickCount;
            if (nocheMirada == 0 || unchecked(t - nocheMirada) >= 1000) { nocheAhora = EsDeNoche(DateTime.Now); nocheMirada = t | 1; }
            return nocheAhora;
        }
        private float MetaNoche() { return ((nocheOn && EsDeNocheAhora()) || ProbandoNoche) ? NocheLin() : 1f; }
        // Cuanto hay que bajar AHORA (el valor de la rampa). Antes de la primera vuelta del reloj, el destino (un video que empezara en ese
        // hueco saldria mudo con el campo aun en cero).
        private float FactorNoche() { return nocheArrancada ? nocheFactor : MetaNoche(); }
        // «Probar» (Ctrl+F3 -> Sonido): 15 s al nivel de la noche, al momento; luego vuelve solo (de dia) o se queda (si ya es de noche).
        internal void PruebaNoche() { nochePruebaHasta = (Environment.TickCount + NochePruebaMs) | 1; nochePruebaRapida = true; }
        internal int SegundosPruebaNoche { get { return ProbandoNoche ? Math.Max(1, unchecked(nochePruebaHasta - Environment.TickCount) / 1000 + 1) : 0; } }

        // Un paso de la rampa, por MILISEGUNDOS DE RELOJ (no por vueltas: el medidor de Ctrl+F3 llama al tick a mano).
        private static float AvanzaRampa(float actual, float meta, int msPasados, int rampaMs)
        {
            if (actual == meta) return meta;
            if (msPasados <= 0) return actual;
            float paso = (1f - 0.5f) * msPasados / rampaMs;   // recorre medio volumen en rampaMs: la noche (70 a 90 %) en menos que eso
            if (actual < meta) { actual += paso; if (actual > meta) actual = meta; }
            else { actual -= paso; if (actual < meta) actual = meta; }
            return actual;
        }

        // La lleva el temporizador de animacion, al lado de CheckBusinessDayReset.
        private void AvanzaModoNoche()
        {
            bool esNoche = nocheOn && EsDeNocheAhora();
            float meta = MetaNoche();
            int ahora = Environment.TickCount;
            // Al encender NO se hace la rampa: se arranca ya en el nivel que toca.
            if (!nocheArrancada) { nocheArrancada = true; nocheUltMs = ahora; nocheFactor = meta; nocheAvisada = esNoche; return; }
            int dt = unchecked(ahora - nocheUltMs); nocheUltMs = ahora;
            if (dt < 0 || dt > 5000) dt = 0;   // el reloj dio la vuelta, o el equipo estuvo dormido: no dar un salto
            if (nochePruebaHasta != 0 && !ProbandoNoche) { nochePruebaHasta = 0; nochePruebaRapida = true; }   // se acabo la prueba: vuelve rapido
            if (esNoche != nocheAvisada)
            {
                nocheAvisada = esNoche;
                ShowToast(esNoche ? ("Modo noche: el volumen baja al " + nochePct + " %") : "Se acabo el modo noche: vuelve el volumen de siempre",
                          Color.FromArgb(38, 78, 132));
            }
            float antes = nocheFactor;
            nocheFactor = AvanzaRampa(nocheFactor, meta, dt, nochePruebaRapida ? NochePruebaRampaMs : NocheRampaMs);
            if (nocheFactor == meta) nochePruebaRapida = false;
            // Y se empuja a la cancion que YA suena (y a la preparada, y al video): si no, no se oiria hasta la siguiente.
            if (nocheFactor != antes)
            {
                float m = MasterEfectivo() * FactorNoche();
                LevelTap t = curTap; if (t != null) t.Master = m;
                CadenaCancion p = cadenaPreparada; if (p != null && p.Tap != null) p.Tap.Master = m;   // y la siguiente: solo su volumen (antes, todo AplicaAPreparada en cada vuelta de la rampa); el video lo sigue solo (su sondeo relee VideoVolumeFor cada 400 ms)
            }
        }

        // El de los videos: la misma rampa, llevada a la perilla del reproductor (de 1 a nochePct/100).
        private float FactorVideoNoche()
        {
            float lin = NocheLin(); if (lin >= 1f) return 1f;
            float avance = (1f - FactorNoche()) / (1f - lin); if (avance < 0f) avance = 0f; else if (avance > 1f) avance = 1f;
            return 1f - avance * (1f - nochePct / 100f);
        }

        // LA TARJETA DE SONIDO A UNA FRECUENCIA FIJA (A4, pedido el 12-10-2026). En Windows XP, si la tarjeta trabaja a
        // 48 kHz y la cancion viene a 44,1 kHz, la conversion la hace el mezclador de Windows, que es de calidad baja (ensucia
        // los agudos). Con "frecuencia_salida=48000" (o 44100) cada cancion se convierte AQUI, con el mismo convertidor
        // sinc del relevo sin pausa (SinPausa.cs, RemuestreoContinuo) y su limitador de picos, y la tarjeta recibe ya su
        // frecuencia. 0 = como la cancion (de fabrica: sin convertir nada, como siempre). La primera vez la tabla del
        // convertidor se prepara en segundo plano: la cancion de ese momento sale tal cual y las siguientes ya convertidas.
        private static int frecuenciaSalida = 0;
        internal static int FrecuenciaSalidaValida(int hz) { return hz == 44100 || hz == 48000 ? hz : 0; }
        private void AFrecuenciaDeLaTarjeta(CadenaCancion c)
        {
            int hz = frecuenciaSalida;
            if (hz <= 0 || c == null || c.Salida == null) return;
            NAudio.Wave.WaveFormat de = c.Salida.WaveFormat;
            if (de == null || de.SampleRate == hz) return;
            NAudio.Wave.WaveFormat a;
            try { a = FormatoAudio.Flotante(hz, de.Channels, FormatoAudio.Mascara(de)); } catch { return; }
            if (!RemuestreoContinuo.SePuede(de, a)) return;   // otra disposicion o fuera de presupuesto: como venga
            double[] tabla = RemuestreoContinuo.CapturaTabla(de, a);
            if (tabla == null) return;
            ConvierteSalida(c, a, tabla);   // (Form1.SinPausa.cs)
            LimitadorPicoReal lim = c.LimiteSalida;
            // El reloj de la cancion descuenta el cebado en cuadros DE LA CANCION (SalidaSonido.cs): el convertidor ya tiro
            // el de la cadena y solo queda el del limitador, que va en cuadros de la salida.
            c.CebadoInicial = (int)Math.Round(lim.DelayFrames * (double)de.SampleRate / hz);
        }
        // Deja preparada en segundo plano la tabla mas comun (44,1 <-> 48 kHz), para que la primera cancion ya salga convertida.
        private static void CalientaFrecuenciaSalida()
        {
            int hz = frecuenciaSalida;
            if (hz <= 0) return;
            try { RemuestreoContinuo.CapturaTabla(FormatoAudio.Flotante(hz == 48000 ? 44100 : 48000, 2, 3), FormatoAudio.Flotante(hz, 2, 3)); } catch { }
        }
        private float crossfadeSec = 0.5f;   // FUNDIDO de salida entre canciones: segundos de rampa al terminar ("fundido=" en config; 0 = corte seco)
        private bool declipOn = true;        // DES-RECORTE (declipper): repara picos aplastados de origen en archivos mal masterizados ("declip=" en config)
        // ===== NIVELES DE MEJORA DE AUDIO segun la maquina (Ctrl+F3) =====
        // NIVEL 1: exciter (excAmount, arriba) - corre en CUALQUIER equipo (~1-2% de CPU).
        // NIVEL 2: reconstructor espectral (SpectralTap) - solo en maquinas que cumplen requisitos (ProbeAudioTier).
        // NIVEL 3 (futuro): DSEE con red neuronal. NO se puede embarcar honestamente en XP/.NET 3.5 (no existe
        //   runtime de inferencia y el CPU no alcanza); si algun dia la rocola corre en Windows 10 + .NET moderno,
        //   ESTE es el punto de enchufe: otro tap en la cadena de PlayTrack, gateado igual por ProbeAudioTier.
        private bool sbrOn = false;          // reconstruccion espectral activa (dsee= en config; Ctrl+F3)
        private bool sbrAllowed = false;     // la maquina cumple los requisitos (se mide al arrancar)
        private bool sbrForce = false;       // dsee_forzar=1 en config: salta la puerta (para pruebas)
        private string sbrReason = "";       // por que NO esta disponible (se muestra en Ctrl+F3)

        private string LoudFile() { return RutaDatos("rocola_volumen.txt"); }
        private string EqFile() { return RutaDatos("rocola_eq.txt"); }

        private readonly object eqSaveLock = new object();
        private readonly GuardadoReciente eqSaveQueue = new GuardadoReciente();
        private int eqSaveVersion;
        private int eqAvgSanas;                  // cuantas entraron en el promedio (se muestra en Ctrl+F3)
        private int eqCacheVer;                  // sube cada vez que cambia una huella: lo usan los recuentos cacheados
        private bool eqAvgSoloSanas;
        private Dictionary<string, float[]> eqAverageSource;
        private int eqAverageVersion, eqAverageCount;
        private bool eqAverageReady;

        // CUANTO bajo psicoacustico le toca a ESTA cancion (multiplica a la perilla). Sale de la misma huella
        // tonal que ya se le mide a cada cancion: la banda de 50 Hz es el grave profundo que las bocinas del
        // local no dan (lo que hay que rescatar) y la de 158 Hz es donde caen los armonicos que generamos (si
        // ahi ya viene lleno, meter mas solo emborrona). Se compara contra el PROMEDIO de la biblioteca, asi
        // que se calibra solo al gusto musical del local: la cancion promedio recibe exactamente lo que dice
        // la perilla, la mas grave hasta un 55% mas y la que no lo necesita hasta un 55% menos.
        // Sin huella todavia (cancion nueva) o con menos de 10 canciones medidas: 1, o sea como toda la vida.
        private float BassFactorFor(string path)
        {
            float[] c = null;
            if (path != null) eqCache.TryGetValue(path, out c);
            float f = 1f;
            // I1 (A la medida, Form1.Datos.cs): con el interruptor y el modelo del EQ, la vara es el objetivo de ESTA
            // cancion (cuanto grave tendria una bien grabada como ella) en vez del promedio de la biblioteca.
            float[] vara = MetaALaMedida(c) ?? eqAvg;
            if (c != null && c.Length >= EqLen && vara != null && c[0] > -25f && c[1] > -25f)
            {
                float rel = (c[0] - vara[0]) - (c[1] - vara[1]);   // dB de grave profundo DE MAS que la cancion tipica
                f = Clampf(1f + rel / 12f, 0.45f, 1.55f);
            }
            // Y ademas: si la GRABACION viene cortada por abajo (muchas viejas no tienen nada bajo 80 Hz), ese
            // grave no esta en el archivo y ninguna bocina lo va a sacar. Ahi el bajo psicoacustico es lo unico
            // que puede devolver la sensacion de fondo, asi que se le da mas. Una que baja a 30 Hz necesita menos.
            if (c != null && c.Length >= EqLen && c[EqCutLo] > 10f)
                f *= Clampf(0.90f + (c[EqCutLo] - 40f) / 110f, 0.90f, 1.35f);
            return Clampf(f, 0.40f, 1.70f);
        }

        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadLoudCacheRecords(TextReader reader, Dictionary<string, float> loudCache, Dictionary<string, float> durCache)
        {
            Dictionary<string, float> pending = new Dictionary<string, float>(loudCache.Comparer);
            Dictionary<string, float> durations = new Dictionary<string, float>(durCache.Comparer);
            FillLoudCacheRecords(reader, pending, durations);
            foreach (KeyValuePair<string, float> item in pending) loudCache[item.Key] = item.Value;
            foreach (KeyValuePair<string, float> item in durations) durCache[item.Key] = item.Value;
        }

        private static void FillLoudCacheRecords(TextReader reader, Dictionary<string, float> pending, Dictionary<string, float> durations)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int bar = line.IndexOf('|');
                if (bar <= 0 || bar >= line.Length - 1) continue;
                float rms;
                if (!float.TryParse(line.Substring(0, bar), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out rms)
                    || !(rms > 0f) || !Finito(rms)) continue;   // el silencio apuntado (LoudMudo) TIENE que sobrevivir al reinicio; NaN o infinito, no
                // Formato NUEVO "rms|duracion|ruta" y el VIEJO "rms|ruta" (una ruta de Windows no puede llevar
                // '|', asi que distinguirlos es seguro): lo viejo se sigue leyendo tal cual, sin duracion.
                string rest = line.Substring(bar + 1);
                int bar2 = rest.IndexOf('|');
                float dur = 0f;
                if (bar2 > 0 && bar2 < rest.Length - 1
                    && float.TryParse(rest.Substring(0, bar2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dur))
                    rest = rest.Substring(bar2 + 1);
                else dur = 0f;
                rest = Pool(rest);
                pending[rest] = rms;
                if (dur > 0.01f && Finito(dur)) durations[rest] = dur;
            }
        }

        // Guarda la tabla de sonoridades. Para no frenar el cambio de cancion en bibliotecas GRANDES (el archivo
        // puede tener miles de lineas), se toma una COPIA rapida en el hilo de la interfaz y el volcado a disco se
        // hace en SEGUNDO PLANO. Un candado asegura un solo escritor a la vez. Si algo truena, se ignora (la medida
        // se vuelve a aprender sola). Un archivo perdido/roto no rompe nada: se reconstruye oyendo las canciones.
        private readonly object loudSaveLock = new object();
        private readonly GuardadoReciente loudSaveQueue = new GuardadoReciente();
        private int loudSaveVersion;
        // Etapa (0..5) a la que pertenece una sonoridad lineal medida (solo para NOMBRARLA).
        private static int LoudTier(float rms)
        {
            if (rms <= 0.0001f) return 0;   // practicamente muda: la etapa mas baja
            float db = (float)(20.0 * Math.Log10(rms));
            for (int i = 0; i < TierDb.Length - 1; i++) if (db <= TierDb[i]) return i;
            return TierDb.Length - 1;   // por encima del ultimo punto = MUY ALTA
        }
        // Nombre de la etapa de una cancion ya medida ("" si aun no se ha medido).
        private string LoudTierName(string path)
        {
            float rms;
            if (!SonoridadParaNivelar(path, out rms)) return "";   // la misma medida con la que se nivela
            return TierName[LoudTier(rms)];
        }

        // Ganancia de nivelado para una cancion, segun su sonoridad medida (si ya se oyo alguna vez). 1 = sin cambio.
        private float NormGainFor(string path)
        {
            if (!normalizeOn || normStrength <= 0.001f || path == null) return 1f;
            float rms;
            // La medida de la cancion ENTERA (BS.1770) si ya esta; si no, la estimacion por tramos o la
            // aprendida oyendola. Misma escala y mismos topes de abajo (ver SonoridadParaNivelar).
            if (!SonoridadParaNivelar(path, out rms)) return 1f;   // aun no medida: va a su volumen original y se aprende en esta pasada
            // "Subir el piso": si la cancion suena mas BAJITO que el objetivo, se SUBE (segun la fuerza), con el tope
            // de SU ETAPA y sin pasar de lo que aguantan sus picos. Si suena bastante mas FUERTE, se baja un poco. La
            // cuenta esta en GananciaNivelado (Sonoridad.cs).
            float cap = (float)Math.Pow(10.0, TierCapDb(rms) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;   // NormMaxBoost sigue siendo el techo absoluto de seguridad
            float pico, p99, p90;
            bool hayPicos = PicosParaNivelar(path, out pico, out p99);
            // Con la medida nueva (algoritmo 5, con el P90): hasta su limite, sin objetivo (GananciaAlLimite, Sonoridad.cs). Con una de antes,
            // la regla de antes mientras la tanda larga la vuelve a medir.
            if (hayPicos && P90ParaNivelar(path, out p90)) return GananciaAlLimite(normStrength, pico, p99, p90);
            return GananciaNivelado(normTarget, rms, normStrength, cap, hayPicos, pico, p99);
        }

        // Cierra la medida de la cancion que acaba de sonar: calcula su RMS integrado y lo guarda (si se oyo lo
        // suficiente). La llama DisposePlaybackNow, ya con el audio detenido (sin carrera con el hilo de sonido).
        private void FinalizeLoudMeasure()
        {
            string path = curMeasurePath; curMeasurePath = null;
            double sumSq = curSumSq; long samples = curSamples; curSumSq = 0; curSamples = 0;
            CierraSonoridadDeFuente(path, sumSq, samples, kwRate, kwCanales, curFuenteMedida);
        }
        // Lo mismo con lo que se guardo de una cancion que acabo por relevo sin pausa (Form1.SinPausa.cs).
        private void CierraSonoridad(string path, double sumSq, long samples, int tasa, int canales)
        {
            if (path == null || tasa <= 0 || canales <= 0 || samples < (long)(NormMinSec * tasa * canales)) return;   // por duracion
            float rms = (float)Math.Sqrt(sumSq / samples);
            if (!(rms > 0.0001f) || !Finito(rms)) return;   // (tambien NaN: una medida rota no se guarda)
            float prev; bool had = loudCache.TryGetValue(path, out prev);
            float val = had ? (prev * 0.5f + rms * 0.5f) : rms;   // promedia con la medida anterior: se estabiliza si la cancion se salto en distinto punto
            // Si ya estaba medida y el valor casi no cambia, NO reescribir el archivo: en una biblioteca ya
            // "aprendida" el guardado deja de dispararse en cada cancion (menos escrituras a disco, cero churn).
            if (had && Math.Abs(val - prev) < 0.004f) return;
            loudCache[path] = val;
            MarcarVolumen(path);
            InvalidateVideoMeasurements();
            SaveLoudCache();
        }

        // PRECALENTADOR de miniaturas: hilo de MINIMA prioridad que va construyendo en disco la
        // miniatura de TODAS las caratulas que falten. La primera sesion en una maquina tarda unos
        // minutos en completarse (y se PAUSA mientras alguien usa la rocola para no estorbar);
        // a partir de ahi, abrir cualquier pagina es practicamente instantaneo para siempre.
        // ===================== PRE-ANALISIS DE LA BIBLIOTECA =====================
        // Antes, una cancion NUEVA sonaba a su volumen original la primera vez (aun no habia medida) y solo
        // quedaba nivelada a partir de la segunda. Con el pre-analisis eso se acabo: en segundo plano se MIDE
        // toda la biblioteca (sonoridad K-weighted, la misma norma que usa el nivelado, + la DURACION), asi que
        // cada cancion ya llega nivelada la PRIMERA vez que alguien la pone.
        // Para que no le robe la maquina a la rocola: hilo de prioridad MINIMA, espera a que nadie este usando
        // la rocola, cede el paso si hay video, y de cada cancion mide TRAMOS repartidos (no el archivo entero):
        // ~60 s bien elegidos dan una buena ESTIMACION a una fraccion del costo. La medida integrada de la cancion
        // ENTERA viene despues, en una segunda tanda (ver Sonoridad.cs). Lo medido se guarda en disco cada pocas
        // canciones: se puede apagar la rocola sin perder nada.
        private bool eqDirty = false;             // hay huellas de EQ nuevas sin guardar (las escribe FlushEqIfDirty)

        // ABRIR UN MP3 SIN DEJAR NADA COLGANDO.
        //
        // NAudio arma el descompresor de MP3 -que por dentro es un "stream" de ACM, del sistema- y JUSTO
        // DESPUES reserva su buffer y termina de prepararse. Si algo falla en ese hueco -una cabecera rota, un
        // tamano absurdo, y de esos hay unos cuantos en cualquier biblioteca grande- suelta el error y cierra
        // el archivo, pero NO cierra el descompresor: se queda tirado. Un rato despues el recolector de basura
        // lo cierra por su cuenta, Windows contesta que ese descompresor ya no vale, y NAudio lanza la queja
        // desde el hilo del recolector, donde NADIE la puede recoger: la rocola se cierra entera, a media
        // cancion. Eso es lo que quedaba apuntado en el archivo de errores como "InvalidParameter calling
        // acmStreamClose", y por eso salia dos veces en diez dias y siempre de madrugada: es cuando el repaso
        // de la biblioteca abre miles de archivos, alguno de ellos roto.
        //
        // NAudio deja un gancho para armar el descompresor uno mismo. Se usa para apuntar CUAL creo: si la
        // apertura falla, lo cerramos NOSOTROS ahi mismo y no queda nada pendiente para el recolector.
        private static Mp3FileReader AbreMp3(string ruta)
        {
            NAudio.Wave.IMp3FrameDecompressor hecho = null;
            Func<NAudio.Wave.WaveFormat, NAudio.Wave.IMp3FrameDecompressor> otroAcm = delegate(NAudio.Wave.WaveFormat f)
            { return Mp3FileReader.CreateAcmFrameDecompressor(f); };
            Mp3FileReader.FrameDecompressorBuilder armador = delegate(NAudio.Wave.WaveFormat wf)
            {
                hecho = Mp3FileReader.CreateAcmFrameDecompressor(wf);
                hecho = DecodificacionFlotante.Envuelve(wf, hecho, ruta, otroAcm);   // coma flotante con NLayer si la cancion llega al maximo (FlotanteNLayer.cs)
                return hecho;
            };
            try { return new Mp3FileReader(ruta, armador); }
            catch { if (hecho != null) { try { hecho.Dispose(); } catch { } } throw; }
        }

        // Y LO MISMO PARA UN MP3 QUE YA ESTA EN MEMORIA, que es por donde se colo.
        //
        // Cuando NAudio rechaza un MP3 -tipico: uno que cambia de frecuencia a mitad- la rocola lo
        // reconstruye en memoria y lo vuelve a intentar (ver OpenAudio y TolerantMp3Source). Ese segundo
        // intento abria el lector A PELO, sin este camino, y ahi seguia la misma fuga que arriba.
        //
        // Y es JUSTO el peor sitio donde dejarla: por aqui pasan SOLO los archivos que ya hicieron fallar a
        // NAudio una vez, o sea que la fuga vivia exactamente donde se concentran los fallos. Se cerro la
        // rocola entera dos veces por esto, y la segunda con el arreglo de la otra puerta ya puesto.
        private static Mp3FileReader AbreMp3(Stream flujo)
        {
            NAudio.Wave.IMp3FrameDecompressor hecho = null;
            Mp3FileReader.FrameDecompressorBuilder armador = delegate(NAudio.Wave.WaveFormat wf)
            { hecho = Mp3FileReader.CreateAcmFrameDecompressor(wf); return hecho; };
            try { return new Mp3FileReader(flujo, armador); }
            catch { if (hecho != null) { try { hecho.Dispose(); } catch { } } throw; }
        }

        // Lector para MEDIR una cancion (no para que suene). Los MP3 conservan el descompresor de arriba, que no deja
        // recursos tirados; solo se oculta un ID3 inicial validado que el medidor no usa. El resto, por donde siempre.
        // Es EL MISMO montaje que usa NAudio
        // por dentro (el lector mas un canal de muestras), asi que lo medido no cambia ni en un bit.
        private sealed class LectorMedida : IDisposable
        {
            private readonly WaveFormat formato;
            private readonly Mp3FileReader mp3;
            private readonly WaveStream wav;
            private readonly SampleChannel canal;
            private readonly Pcm16Directo directo;   // los 16 bits enteros (MP3 del sistema, WAV de 16 bits) sin el SampleChannel (Pcm16Directo.cs)
            private readonly AudioFileReader otro;
            private readonly Stream flujoMp3;   // Mp3FileReader(Stream, ...) NO cierra el stream que recibe

            // NAudio 1.9 copia DOS VECES el ID3 completo al abrir un MP3. Una caratula incrustada grande deja
            // dos reservas grandes por cada pasada de medida, aunque aqui nunca se leen las etiquetas.
            // No se cambia NAudio ni la reproduccion: esta vista conserva todos los bytes de audio y el ID3v1
            // final, con posiciones/longitud relativas al MISMO inicio que antes usaba su tabla de cuadros.
            private sealed class VistaMp3 : Stream
            {
                private Stream origen;
                private readonly long inicio, largo;
                internal VistaMp3(Stream archivo, long desde)
                {
                    if (archivo == null) throw new ArgumentNullException("archivo");
                    if (!archivo.CanRead || !archivo.CanSeek) throw new ArgumentException("El MP3 debe poder leerse y buscarse.", "archivo");
                    if (desde < 0 || desde > archivo.Length) throw new ArgumentOutOfRangeException("desde");
                    inicio = desde; largo = archivo.Length - desde;
                    archivo.Position = desde;
                    origen = archivo;
                }
                private Stream Abierto
                { get { if (origen == null) throw new ObjectDisposedException("VistaMp3"); return origen; } }
                public override bool CanRead { get { return origen != null && origen.CanRead; } }
                public override bool CanSeek { get { return origen != null && origen.CanSeek; } }
                public override bool CanWrite { get { return false; } }
                public override long Length { get { Stream s = Abierto; return largo; } }
                public override long Position
                {
                    get { return Abierto.Position - inicio; }
                    set { Seek(value, SeekOrigin.Begin); }
                }
                public override int Read(byte[] buffer, int offset, int count)
                {
                    Stream s = Abierto;
                    if (buffer == null) throw new ArgumentNullException("buffer");
                    if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
                    long quedan = largo - (s.Position - inicio);
                    if (quedan <= 0 || count == 0) return 0;
                    return s.Read(buffer, offset, (int)Math.Min((long)count, quedan));
                }
                public override int ReadByte()
                { Stream s = Abierto; return s.Position - inicio >= largo ? -1 : s.ReadByte(); }
                public override long Seek(long offset, SeekOrigin origin)
                {
                    Stream s = Abierto;
                    long basePos;
                    switch (origin)
                    {
                        case SeekOrigin.Begin: basePos = 0; break;
                        case SeekOrigin.Current: basePos = s.Position - inicio; break;
                        case SeekOrigin.End: basePos = largo; break;
                        default: throw new ArgumentException("Origen de busqueda no valido.", "origin");
                    }
                    long pos, fisica;
                    try { pos = checked(basePos + offset); fisica = checked(inicio + pos); }
                    catch (OverflowException) { throw new IOException("Posicion fuera del archivo MP3."); }
                    if (pos < 0) throw new IOException("No se puede buscar antes del audio MP3.");
                    s.Position = fisica;   // como FileStream, admite una posicion posterior al final; Read devuelve 0
                    return pos;
                }
                public override void Flush() { Stream s = Abierto; }
                public override void SetLength(long value) { throw new NotSupportedException(); }
                public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
                protected override void Dispose(bool disposing)
                {
                    Stream s = origen; origen = null;
                    try { if (disposing && s != null) s.Dispose(); }
                    finally { base.Dispose(disposing); }
                }
            }

            private static bool LeeDiez(Stream s, byte[] cabecera)
            {
                int n = 0, r;
                while (n < 10 && (r = s.Read(cabecera, n, 10 - n)) > 0) n += r;
                return n == 10;
            }
            // Solo los casos en que el desplazamiento es inequívoco y coincide con NAudio 1.9.
            // Un ID3 roto, truncado o con cabecera extendida/compresion vuelve a la apertura historica.
            // En particular, NAudio trata 0x40 avanzando otros cuatro bytes: NO se corrige eso aqui.
            private static long InicioAudioMp3(Stream s)
            {
                if (!s.CanRead || !s.CanSeek) return 0;
                long posicion = s.Position;
                try
                {
                    if (s.Length < 10) return 0;
                    s.Position = 0;
                    byte[] h = new byte[10];
                    if (!LeeDiez(s, h) || h[0] != 'I' || h[1] != 'D' || h[2] != '3') return 0;
                    int version = h[3], flags = h[5];
                    if (version < 2 || version > 4 || h[4] == 255 || (flags & 0x40) != 0) return 0;
                    int permitidos = version == 2 ? 0x80 : (version == 3 ? 0xA0 : 0xB0);
                    if ((flags & ~permitidos) != 0) return 0;
                    for (int i = 6; i < 10; i++) if ((h[i] & 0x80) != 0) return 0;
                    long tamano = ((long)h[6] << 21) | ((long)h[7] << 14) | ((long)h[8] << 7) | h[9];
                    bool pie = version == 4 && (flags & 0x10) != 0;
                    long fin = 10 + tamano + (pie ? 10 : 0);
                    // Conservar tambien el comportamiento de la busqueda de ID3v1 al final en MP3 muy cortos.
                    if (fin > s.Length - 128) return 0;
                    if (pie)
                    {
                        s.Position = 10 + tamano;
                        byte[] f = new byte[10];
                        if (!LeeDiez(s, f) || f[0] != '3' || f[1] != 'D' || f[2] != 'I') return 0;
                        for (int i = 3; i < 10; i++) if (f[i] != h[i]) return 0;
                    }
                    return fin;
                }
                finally { s.Position = posicion; }
            }
            private static Mp3FileReader AbreMp3Medida(string ruta, out Stream propio)
            {
                propio = null;
                FileStream archivo = null;
                Stream vista = null;
                NAudio.Wave.IMp3FrameDecompressor hecho = null;
                try
                {
                    archivo = File.OpenRead(ruta);   // misma apertura/comparticion de la ruta historica
                    long inicio;
                    try { inicio = InicioAudioMp3(archivo); }
                    catch (IOException) { inicio = 0; }
                    // Sin ID3 que saltar, el archivo tal cual: es lo que abria AbreMp3(ruta) (File.OpenRead), ahora con el lector
                    // ligero (Mp3Ligero.cs). Con ID3, la vista.
                    if (inicio == 0) { vista = archivo; archivo = null; }
                    else { vista = new VistaMp3(archivo, inicio); archivo = null; }   // la vista pasa a ser duena del archivo
                    Func<WaveFormat, NAudio.Wave.IMp3FrameDecompressor> otroAcm = delegate(WaveFormat f)
                    { return Mp3FileReader.CreateAcmFrameDecompressor(f); };
                    Mp3FileReader.FrameDecompressorBuilder armador = delegate(WaveFormat wf)
                    {
                        hecho = Mp3FileReader.CreateAcmFrameDecompressor(wf);
                        hecho = DecodificacionFlotante.Envuelve(wf, hecho, ruta, otroAcm);
                        return hecho;
                    };
                    // El repaso lee cada cuadro sin dejar basura (Mp3Ligero.cs): el mismo lector de NAudio, la misma salida.
                    Mp3FileReader lector = LectorMp3Ligero.Posible ? LectorMp3Ligero.Abre(vista, armador) : new Mp3FileReader(vista, armador);
                    propio = vista; vista = null;
                    return lector;
                }
                catch { if (hecho != null) { try { hecho.Dispose(); } catch { } } throw; }
                finally
                {
                    if (vista != null) { try { vista.Dispose(); } catch { } }
                    if (archivo != null) { try { archivo.Dispose(); } catch { } }
                }
            }
            public LectorMedida(string ruta)
            {
                try
                {
                    if (ruta != null && ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    {
                        Stream flujo = null;
                        Mp3FileReader r = AbreMp3Medida(ruta, out flujo);
                        try
                        {
                            if (Pcm16Directo.Vale(r.WaveFormat)) directo = new Pcm16Directo(r); else canal = new SampleChannel(r, false);
                            mp3 = r; flujoMp3 = flujo; r = null; flujo = null;
                        }
                        finally
                        {
                            if (r != null) { try { r.Dispose(); } catch { } }
                            if (flujo != null) { try { flujo.Dispose(); } catch { } }
                        }
                    }
                    else
                    {
                        WaveStream f = WavDirecto.Abre(ruta);
                        if (f != null)
                        {
                            try
                            {
                                if (Pcm16Directo.Vale(f.WaveFormat)) directo = new Pcm16Directo(f); else canal = new SampleChannel(f, false);
                                wav = f; f = null;
                            }
                            finally { if (f != null) { try { f.Dispose(); } catch { } } }
                        }
                        else otro = new AudioFileReader(ruta);
                    }
                    formato = FormatoAudio.DeArchivo((otro != null) ? otro.WaveFormat : (directo != null ? directo.WaveFormat : canal.WaveFormat), ruta);
                }
                catch { Dispose(); throw; }
            }
            private WaveStream Lector { get { return (mp3 != null) ? (WaveStream)mp3 : wav; } }
            public NAudio.Wave.WaveFormat WaveFormat { get { return formato; } }
            public TimeSpan TotalTime { get { return (otro == null) ? Lector.TotalTime : otro.TotalTime; } }
            public TimeSpan CurrentTime { set { if (otro == null) Lector.CurrentTime = value; else otro.CurrentTime = value; } }
            public float Volume { set { if (otro != null) otro.Volume = value; else if (directo != null) directo.Volumen = value; else canal.Volume = value; } }
            // UNA MUESTRA QUE NO ES UN NUMERO (NaN o infinito: un WAV en coma flotante estropeado, por ejemplo) cuenta como
            // silencio, que es lo que llega a oirse. Antes la medida entera salia NaN y se daba por buena: la huella del EQ
            // se guardaba con NaN y, con ella, la media de toda la biblioteca (ComputeEqAverage) y el EQ automatico de
            // todas las canciones. (v - v no es 0 solo si v es NaN o infinito.)
            public int Read(float[] b, int o, int c)
            {
                if (directo != null)
                {
                    int n16 = directo.Read(b, o, c);
                    // Un entero de 16 bits entre 32768 no puede ser NaN ni pasar de 1: con el volumen en 1 no hay nada que limpiar.
                    if (directo.Volumen != 1f) LimpiaMuestras(b, o, n16);
                    return n16;
                }
                int n = (otro == null) ? canal.Read(b, o, c) : otro.Read(b, o, c);
                LimpiaMuestras(b, o, n);
                return n;
            }
            public void Dispose()
            {
                if (mp3 != null) { try { mp3.Dispose(); } catch { } }
                if (flujoMp3 != null) { try { flujoMp3.Dispose(); } catch { } }
                if (wav != null) { try { wav.Dispose(); } catch { } }
                if (otro != null) { try { otro.Dispose(); } catch { } }
            }
        }

        // Mide un archivo SIN reproducirlo: la ESTIMACION de sonoridad (un filtro parecido al K de BS.1770, sin
        // puertas, igual que el nivelado en vivo), duracion
        // y la HUELLA TONAL del EQ automatico (8 bandas, mismo bandpass y misma referencia que al oirla). Analiza
        // PreSegs tramos repartidos por el tema (o el tema entero si es corto). Devuelve false si no se pudo.
        // "aFondo" = modo ENTRENAMIENTO A FONDO: analiza la cancion COMPLETA en vez de por tramos (la medida
        // mas exacta posible) y saca mucho mas material para la IA. En automatico se queda con el muestreo,
        // que da practicamente la misma sonoridad a una fraccion del costo.
        private bool MeasureFile(string path, bool aFondo, out float rms, out float durSec, out float[] eqCurve)
        {
            fuenteLeida = null;
            FuenteMedida fuenteInicio = CapturaFuente(path);
            rms = 0f; durSec = 0f; eqCurve = null;
            integradaLista = false;   // lo que deja para TomaIntegrada: nada, salvo que lea la cancion entera
            if (MeasurementCancelled()) return false;
            // De un VIDEO solo interesa CUANTO SUENA. Su sonido lo saca el reproductor de Windows, no
            // nosotros: no pasa por el ecualizador ni por el DSEE ni por los graves. Medirle la huella tonal
            // o cosechar cuadros para la IA seria trabajo y disco gastados en algo que nunca se usaria.
            bool esVideo = EsVideo(path);
            LectorMedida rd = null;
            TrabajoMedida trabajo = null;
            try
            {
                rd = new LectorMedida(path);
                rd.Volume = 1.0f;
                int rate = rd.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                int ch = rd.WaveFormat.Channels; if (ch < 1) ch = 1;
                double total = rd.TotalTime.TotalSeconds;
                if (total <= 0.05 || total > 36000) return false;   // vacio o absurdo (archivo roto)
                durSec = (float)total;
                trabajo = TomaTrabajoMedida(rate, ch, !esVideo);
                float[] c1 = trabajo.C1, c2 = trabajo.C2;   // misma receta K; estados nuevos por cancion
                float[] st = trabajo.Estado, buf = trabajo.Buffer;
                double sumSq = 0; long nSamp = 0;
                // RECORTE DE ORIGEN: muestras pegadas al techo digital en rachas de 3 o mas. No es distorsion
                // que metamos nosotros: viene asi masterizada. Se cuenta aqui, en el recorrido que ya se hacia.
                long clipN = 0; int[] clipRun = trabajo.Recorte;
                float[][] bp = trabajo.Bandas; float[] bpSt = trabajo.EstadoBandas;
                double[] bpSum = trabajo.SumaBandas; long bpN = 0;
                // COSECHA PARA LA IA: se analiza el canal 0 con el MISMO motor del DSEE. Los pares se guardan
                // en memoria y solo se escriben al final, si resulta que la cancion SI conserva sus agudos
                // (una cancion dañada no puede enseñar nada: los agudos reales ya no estan en el archivo).
                SpectralTap hv = null; float[] hvFrame = null, hvFeat = null, hvTgt = null;
                int hvFill = 0; List<float> hvPairs = null;
                // El presupuesto total no crece: cada ventana elige alternadamente
                // un canal y cada lista conserva el indice correcto en su origen.
                SpectralTap[] hvCanales = null; float[][] hvCuadros = null;
                List<float>[] hvPares = null; long hvTurno = 0;
                long hvVistos = 0, hvVistosMax = IaFramesPerSong;   // cuadros con sonido que han pasado / hasta donde mirar
                int hvStride = 1, hvFase = 0;
                // El ACANTILADO se mide SIEMPRE, cosechemos o no para la IA. Es el dato que dice si una cancion
                // llego completa o el codec le talo los agudos, y de eso dependen dos cosas importantes: si sirve
                // para fijar el objetivo del EQ automatico, y que bandas suyas tiene sentido corregir. Cuesta unas
                // pocas decenas de FFT por cancion, nada al lado de descomprimirla entera.
                bool soloCorte = false;
                // tramos: si el tema es corto se analiza entero; si no, PreSegs ventanas repartidas evitando
                // el arranque y el final (intros/silencios finales no representan la sonoridad del tema).
                // A FONDO: un solo "tramo" que abarca la cancion entera. En automatico: PreSegs ventanas.
                // (Se decide AQUI, antes de la cosecha, porque de cuanto audio se va a descomprimir depende
                // cada cuantos cuadros conviene tomar uno.)
                int segs = (aFondo || total <= PreSegSec * PreSegs) ? 1 : PreSegs;
                double segLen = (segs == 1) ? total : PreSegSec;
                // Y SI ESTE RECORRIDO LEE LA CANCION ENTERA (corta, o repaso a fondo), de paso se le mide la
                // sonoridad integrada BS.1770 (ver Sonoridad.cs). No cambia nada de lo que se mide aqui.
                MedidorBs1770 integ = null;
                if (segs == 1 && !esVideo && rd.WaveFormat.SampleRate >= 8000)
                {
                    double[] pesosInteg = DisposicionBs1770.DeArchivo(path, ch);
                    if (pesosInteg != null) { try { integ = trabajo.Sonoridad(rd.WaveFormat.SampleRate, pesosInteg); } catch { integ = null; } }
                }
                Func<float[], int, int, int> lee = rd.Read;
                int cuadroLen = 2048;   // el de SpectralTap; se confirma al crearlo
                long framesTot = (long)(segs * segLen * rate / cuadroLen);
                int diana = aFondo ? IaDeepFrames : IaFramesPerSong;
                // El paso se REDONDEA HACIA ARRIBA a proposito: con la division a secas el ultimo cuadro caia
                // antes del final y se quedaba sin mirar el ultimo 15% de cada cancion.
                long pasoIa = (framesTot - IaWarmFrames + diana - 1) / Math.Max(1, diana);
                int strideIa = (pasoIa > 1) ? (int)pasoIa : 1;
                // ----- CUANTAS VECES SE MIRA CADA CANCION -----
                // Niveles apuntados en rocola_ia_hechas.txt: 0 = nunca; 1 = una pasada del modo PASIVO;
                // 2 = repasada A FONDO (esa ya dio todo lo que tenia); 3..9 = 2..8 pasadas del pasivo.
                //
                // Antes, en cuanto una cancion llegaba al nivel 1 el modo pasivo NO volvia a mirarla nunca. Con
                // la biblioteca entera en nivel 1 el archivo de material dejaba de crecer y ahi se quedaba para
                // siempre: por eso despues de dias encendida seguia en los mismos gigas. Ahora cada pasada mira
                // cuadros DISTINTOS de la misma cancion (ver hvFase), asi que el material sigue creciendo sin
                // que el repaso cueste ni un milisegundo mas por cancion. Se para cuando las fases se agotan
                // (pasado 'strideIa' ya se habrian repetido cuadros, que es engordar el archivo por gusto).
                int nivelPrevio; lock (iaDoneLock) { if (!iaDone.TryGetValue(path, out nivelPrevio)) nivelPrevio = 0; }
                int pasadas = IaPasadasDe(nivelPrevio);
                int nivelAhora; bool cosechar;
                if (aFondo) { nivelAhora = 2; cosechar = (nivelPrevio != 2); }
                else { nivelAhora = IaNivelDePasada(pasadas); cosechar = pasadas < Math.Min(IaPasadasMax, strideIa); }
                cosechar = cosechar && iaHarvestOn && !IaBudgetFull() && !esVideo;
                if (!cosechar && !esVideo)
                {
                    // Solo hace falta el acantilado: se analizan unos pocos cuadros y se para (ni se guardan pares).
                    try { hv = trabajo.Analizador(0, rate); hvFrame = trabajo.Cuadro(0, hv.FrameLen); soloCorte = true; hvVistosMax = IaWarmFrames + 24; }
                    catch { hv = null; }
                }
                if (cosechar)
                {
                    try
                    {
                        hv = trabajo.Analizador(0, rate);
                        hvFrame = trabajo.Cuadro(0, hv.FrameLen); hvFeat = trabajo.Features; hvTgt = trabajo.Target;
                        hvPairs = trabajo.Pares(0);
                        int canalesIa = Math.Min(2, ch);
                        hvCanales = new SpectralTap[canalesIa]; hvCuadros = new float[canalesIa][];
                        hvPares = new List<float>[canalesIa];
                        hvCanales[0] = hv; hvCuadros[0] = hvFrame; hvPares[0] = hvPairs;
                        if (canalesIa == 2)
                        {
                            hvCanales[1] = trabajo.Analizador(1, rate);
                            hvCuadros[1] = trabajo.Cuadro(1, hv.FrameLen); hvPares[1] = trabajo.Pares(1);
                        }
                        // Los cuadros se toman REPARTIDOS por todo lo que se va a descomprimir, no seguidos
                        // desde el principio. Antes el modo pasivo cosechaba los 192 PRIMEROS: eso son 8,9
                        // segundos, siempre del mismo sitio de cada cancion, o sea que la red solo llegaba a
                        // ver esa rebanada de toda la biblioteca. Y dos cuadros pegados se parecen tanto que
                        // apilarlos engorda el archivo sin ensenar nada nuevo.
                        if (hv.FrameLen != cuadroLen)   // por si el cuadro cambiara: se rehace la cuenta
                        {
                            cuadroLen = hv.FrameLen;
                            framesTot = (long)(segs * segLen * rate / cuadroLen);
                            pasoIa = (framesTot - IaWarmFrames + diana - 1) / Math.Max(1, diana);
                            strideIa = (pasoIa > 1) ? (int)pasoIa : 1;
                        }
                        hvStride = strideIa;
                        hvFase = (hvStride > 1) ? (pasadas % hvStride) : 0;   // cada pasada, cuadros distintos
                        hvVistosMax = IaWarmFrames + (long)hvStride * diana;
                    }
                    catch { hv = null; }
                }
                // La ficha de calidad usa la misma ventana tanto si se cosecha IA como si no.
                // El analizador puede seguir avanzando para cosechar, pero no cambia esa ficha.
                int qualityFrames = IaWarmFrames + 24;
                // Calidad tiene SU estado y solo canal 0; alternar pares L/R, un canal mudo o
                // el paso de cosecha no pueden cambiar su ventana ni el calor del detector.
                SpectralTap qualityTap = hvCanales != null ? trabajo.Analizador(2, rate) : hv;
                long qualitySeen = 0;
                float measuredCut = qualityTap != null ? qualityTap.CutHz : 0f, measuredCutLo = qualityTap != null ? qualityTap.CutLoHz : 0f;
                for (int s = 0; s < segs && !preScanStop; s++)
                {
                    if (segs > 1)
                    {
                        double span = total * 0.80;                       // zona util: del 10% al 90% del tema
                        double pos = total * 0.10 + span * s / (double)segs;
                        if (pos + segLen > total) pos = Math.Max(0, total - segLen);
                        try { rd.CurrentTime = TimeSpan.FromSeconds(pos); } catch { }
                    }
                    long want = (long)(segLen * rate) * ch;
                    long got = 0;
                    while (got < want)
                    {
                        if (MeasurementCancelled()) return false;
                        int want2 = (int)Math.Min(trabajo.LecturaMax, want - got);
                        int n = LeeCuadrosCompletos(lee, buf, want2, ch);   // cuadros enteros: ningun canal se cruza con otro
                        if (n <= 0) break;
                        got += n;
                        if (integ != null) integ.Procesa(buf, 0, n);
                        // La huella del EQ de todo el trozo de una vez, banda por banda (HuellaDelTrozo): la misma cuenta, en el
                        // mismo orden y con la limpieza de restos en los mismos cuadros que cuadro a cuadro, en la mitad de tiempo.
                        if (!esVideo) HuellaDelTrozo(buf, n / ch, ch, bp, bpSt, bpSum, bpN);
                        // Y la sonoridad K y los recortes, igual de una vez (SonoridadDelTrozo): cuadro a cuadro y canal a canal, como
                        // siempre, porque su suma es una sola para los dos canales; solo sin ir y venir de la IA en cada cuadro.
                        SonoridadDelTrozo(buf, n / ch, ch, c1, c2, st, ref sumSq, ref nSamp, ref clipN, clipRun, bpN);
                        for (int i = 0; i + ch <= n; i += ch)
                        {
                            bpN++;
                            // canal 0 hacia el analizador de la IA (el DSEE tambien trabaja por canal)
                            if (hv != null && ((soloCorte ? false : hvVistos < hvVistosMax) || qualitySeen < qualityFrames))
                            {
                                hvFrame[hvFill] = buf[i];
                                if (hvCuadros != null && hvCuadros.Length == 2) hvCuadros[1][hvFill] = buf[i + 1];
                                hvFill++;
                                if (hvFill >= hvFrame.Length)
                                {
                                    hvFill = 0;
                                    if (qualitySeen < qualityFrames)
                                    {
                                        double qualityEnergy = 0;
                                        for (int q = 0; q < hvFrame.Length; q++) qualityEnergy += hvFrame[q] * hvFrame[q];
                                        if (qualityEnergy / hvFrame.Length > 1e-8)
                                        {
                                            qualityTap.AnalyzeFrame(hvFrame, 0); qualitySeen++;
                                            measuredCut = qualityTap.CutHz; measuredCutLo = qualityTap.CutLoHz;
                                        }
                                    }
                                    // Alternar por grupos del paso: con stride par, alternar cada
                                    // cuadro escogeria siempre el mismo canal para todos los pares.
                                    int canalIa = hvCanales == null ? 0 : (int)((hvTurno++ / hvStride) % hvCanales.Length);
                                    SpectralTap analizador = hvCanales == null ? hv : hvCanales[canalIa];
                                    float[] cuadroIa = hvCuadros == null ? hvFrame : hvCuadros[canalIa];
                                    List<float> paresIa = hvPares == null ? hvPairs : hvPares[canalIa];
                                    double en = 0; for (int q = 0; q < cuadroIa.Length; q++) en += cuadroIa[q] * cuadroIa[q];
                                    if (!soloCorte && hvVistos < hvVistosMax && en / cuadroIa.Length > 1e-8)   // saltar cuadros practicamente mudos
                                    {
                                        hvVistos++;
                                        // SOLO se le hace la FFT a los cuadros que van a servir. Antes se
                                        // analizaban TODOS los que pasaban hasta el tope y despues se tiraba
                                        // el trabajo de los que no tocaban: en el repaso a fondo eso eran mas
                                        // de tres cuartas partes de las FFT hechas para nada. Los primeros si
                                        // van seguidos: el acantilado necesita unos cuantos para asentarse.
                                        bool sirve = (hvVistos <= qualityFrames) || hvStride == 1
                                                     || (((hvVistos + hvFase) % hvStride) == 0);
                                        if (sirve)
                                        {
                                            analizador.AnalyzeFrame(cuadroIa, 0);
                                            if (!soloCorte && hvVistos > IaWarmFrames &&
                                                (hvStride == 1 || ((hvVistos + hvFase) % hvStride) == 0))
                                                for (int q = 0; q < IaCutsHz.Length; q++)
                                                    if (analizador.MakePair(IaCutsHz[q], hvFeat, hvTgt))
                                                    {
                                                        for (int z = 0; z < 28; z++) paresIa.Add(hvFeat[z]);
                                                        for (int z = 0; z < 12; z++) paresIa.Add(hvTgt[z]);
                                                    }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // Hasta el final del archivo, SOLO para la integrada: lo de arriba queda exactamente como antes. Si
                // el archivo se acaba antes de lo que dice durar, la integrada no vale y no se publica.
                if (integ != null && !preScanStop)
                {
                    while (true)
                    {
                        if (MeasurementCancelled()) return false;
                        int n = LeeCuadrosCompletos(lee, buf, trabajo.LecturaMax, ch);
                        if (n <= 0) break;
                        integ.Procesa(buf, 0, n);
                    }
                    SonoridadIntegrada si;
                    if (!MeasurementCancelled() && CierraIntegrada(integ, total, rd.WaveFormat.SampleRate, out si)) { integradaValor = si; integradaLista = true; }
                }
                if (MeasurementCancelled()) { integradaLista = false; return false; } // no publicar ni cosechar una medida cancelada
                // Solo las canciones COMPLETAS (sin acantilado, el mismo umbral que usa el DSEE para no tocarlas)
                // sirven de maestras: en las dañadas los agudos reales ya no existen y no hay nada que enseñar.
                if (hv != null && !soloCorte && hvPares != null)
                {
                    // Con su ORIGEN (cancion, carpeta, frecuencia, cuadro, canal 0) para que el entrenador pueda
                    // separar por cancion. Ver IaApuntaOrigenes. El origen se saca UNA vez por cancion (la etiqueta del archivo, el genero, las
                    // huellas: lo mismo para los dos canales salvo el numero de canal) y los canales se anexan juntos, con una confirmacion en
                    // disco por archivo (IaAppendPairsLote).
                    bool escribio = false, completas = true;
                    List<List<float>> lotesIa = new List<List<float>>(); List<int> canalesIa = new List<int>();
                    for (int canalIa = 0; canalIa < hvPares.Length; canalIa++)
                        if (hvPares[canalIa].Count >= 40 && hvCanales[canalIa].CutHz >= EqSanaHz) { lotesIa.Add(hvPares[canalIa]); canalesIa.Add(canalIa); }
                    if (lotesIa.Count > 0)
                    {
                        IaOrigen origenBase = IaOrigenDe(path, total, rate, hv.FrameLen, canalesIa[0]);
                        IaOrigen[] origenesIa = new IaOrigen[lotesIa.Count];
                        for (int k = 0; k < origenesIa.Length; k++) origenesIa[k] = IaOrigenParaCanal(origenBase, canalesIa[k]);
                        bool[] oksIa = IaAppendPairsLote(lotesIa.ToArray(), origenesIa);
                        for (int k = 0; k < oksIa.Length; k++) { escribio |= oksIa[k]; completas &= oksIa[k]; }
                    }
                    if (escribio && completas) MarkIaDone(path, nivelAhora);
                }
                float corteHz = measuredCut;
                float corteLo = measuredCutLo;
                float recorte = (nSamp > 0) ? (float)((double)clipN / nSamp) : 0f;
                if (nSamp < 40000) return durSec > 0.01f && TerminaFuenteLeida(path, fuenteInicio);   // muy poco audio util
                rms = (float)Math.Sqrt(sumSq / nSamp);
                // SILENCIO DE VERDAD. Un archivo sin nada de sonido da cero, y con cero no se guardaba: se
                // quedaba como "sin medir", se volvia a medir en cada arranque para siempre, y no aparecia
                // nunca en la limpieza de videos mudos. Se le pone un valor minusculo -por debajo del umbral
                // con el que TODO el programa decide "esto no tiene medida util", asi que nadie va a intentar
                // subirlo cien mil veces- para que quede APUNTADO como medido y callado.
                if (rms < LoudMudo) rms = LoudMudo;
                // Huella tonal en dB por banda, referida a la MEDIANA del cuerpo (158 Hz - 5.6 kHz): exactamente
                // la misma cuenta que hace FinalizeEqMeasure al aprenderla oyendo la cancion.
                if (bpN >= 40000 && !esVideo)
                {
                    float[] curve = new float[EqLen];
                    curve[EqCut] = corteHz; curve[EqClip] = recorte; curve[EqCutLo] = corteLo; curve[EqFingerprint] = EqVersionOriginal;
                    for (int b = 0; b < 8; b++) curve[b] = (float)(10.0 * Math.Log10(bpSum[b] / bpN + 1e-12));
                    float[] cuerpo = trabajo.Cuerpo;
                    for (int b = 0; b < cuerpo.Length; b++) cuerpo[b] = curve[b + 1];
                    Array.Sort(cuerpo);
                    float refDb = cuerpo[2];
                    for (int b = 0; b < 8; b++) curve[b] -= refDb;
                    eqCurve = curve;
                }
                return TerminaFuenteLeida(path, fuenteInicio);
            }
            catch { return durSec > 0.01f && TerminaFuenteLeida(path, fuenteInicio); }
            finally
            {
                if (rd != null) { try { rd.Dispose(); } catch { } }
                if (trabajo != null) trabajo.Suelta(); // tambien tras cancelacion, lectura rota o retorno temprano
            }
        }

#if ROCOLA_GPU
        // Los tres cubos de los que puede salir (o a los que puede llegar) el viaje de la portada. Cada uno
        // devuelve EXACTAMENTE la misma celda y el mismo giro con que el render dibuja ese cubo: por eso la
        // portada arranca y aterriza calcada, sin dar el salto que delataria el truco.

        // Cubo GRANDE del buscador (la vista previa del resultado elegido).
        private RectangleF SearchCubeRect(out double ang)
        {
            ang = 0;
            if (searchOverlay == null || !searchOpen) return RectangleF.Empty;   // en GPU el overlay no se pinta: mirar el estado, no Visible
            if (searchOverlay.Nuevo) return PortadaElegidaBuscador();   // el buscador con portadas: sale de la portada de su tarjeta
            Rectangle r; Image ca, cb; float f;
            if (!searchOverlay.GetPreviewCube(out r, out ca, out cb, out f)) return RectangleF.Empty;
            if (r.Width < 8 || r.Height < 8) return RectangleF.Empty;
            ang = searchOverlay.PreviewAngle;
            return new RectangleF(searchOverlay.Left + r.X, searchOverlay.Top + r.Y, r.Width, r.Height);
        }

        // Cubo del disco SELECCIONADO en la rejilla.
        private RectangleF SelectedDiscRect(out double ang)
        {
            ang = 0;
            if (albumView == null || !albumView.Visible) return RectangleF.Empty;
            GpuCubeState st = albumView.BuildCubeState();
            if (!st.Sel.Has || st.Sel.W < 8f || st.Sel.H < 8f) return RectangleF.Empty;
            ang = st.Sel.Angle;
            RectangleF r = new RectangleF(albumView.Left + st.Sel.X, albumView.Top + st.Sel.Y, st.Sel.W, st.Sel.H);
            // Con la portada enfocada, desde donde se VE (ya crecida): si no, el viaje saldria con un saltito.
            return (gpuPanel != null) ? gpuPanel.Enfocado(r) : r;
        }

        // Cubo GRANDE del interior de un disco.
        private RectangleF DiscCubeRect(out double ang)
        {
            ang = 0;
            if (panelSongs == null || cubeBox == null) return RectangleF.Empty;
            GpuCubeInfo ci = new GpuCubeInfo();
            FillCubeBoxGeom(ref ci, cubeBox, panelSongs.Left + cubeBox.Left, panelSongs.Top + cubeBox.Top);
            if (ci.W < 8f || ci.H < 8f) return RectangleF.Empty;
            ang = ci.Angle;
            return new RectangleF(ci.X, ci.Y, ci.W, ci.H);
        }

        // Estado del VIAJE de la portada para el panel GPU. El recorrido va con suavizado quintico (arranca y
        // frena mas suave que el clasico, se nota "caro"); el giro es el camino corto hasta el cubo de destino
        // mas media vuelta; el bamboleo sube y baja (la portada se ladea al volar y llega derecha); el destello
        // acompana al vuelo; y el fundido entra al final, ya encima del cubo de destino, para que se relevan sin
        // costura. Es como lo pidio el operador: primero el acercamiento y despues el degradado.
        // Convierte el rms de cada banda en la altura 0..1 que se dibuja.
        // - En DECIBELIOS, no lineal: es como oye el oido, y ademas evita que el grave aplaste a todo lo demas.
        // - Con INCLINACION (SpecTilt): en musica real los agudos traen mucha menos energia; sin compensarlos la
        //   aurora seria una loma a la izquierda y nada mas.
        // - La ventana de dB puede ser FIJA porque la biblioteca ya viene nivelada: una cancion bajita y una
        //   fuerte llegan aqui al mismo volumen, asi que la aurora luce igual de viva en las dos.
        // - Sube al instante y baja despacio: asi marca el golpe y no tiembla.
        // Por que casi no se movia: dibujaba el NIVEL de cada banda, y el nivel de una cancion es practicamente
        // constante de principio a fin (para eso la rocola las nivela). O sea que el fondo se quedaba en la misma
        // forma toda la cancion y no se distinguia de un fondo fijo. Las estrellas si se mueven porque no miran el
        // nivel: miran cuanto SOBRESALE del momento anterior, que es lo que uno oye como ritmo.
        //
        // Ahora cada banda hace las dos cosas: un CUERPO estable (el nivel, para que la forma del fondo diga de que
        // va la cancion) y encima un GOLPE que sube cuando esa banda pega por encima de su propio promedio. El
        // golpe es la mitad de la altura, asi que el fondo respira de verdad con la musica sin volverse nervioso.
        private void AvanzaAurora()
        {
            AvanzaBandas(1f);
            AvanzaPresencia(1f);
        }
        // Las bandas, 'pasos' latidos de 42 ms (fracciones incluidas: la tarjeta las avanza en cada cuadro, ver
        // AuroraFinaGpu.cs). Con un latido justo, la cuenta de siempre.
        private void AvanzaBandas(float pasos)
        {
            float kPiso = AurPorPasos(0.045f, pasos), kGolpe = AurQueda(0.80f, pasos), kBaja = AurPorPasos(0.22f, pasos);
            for (int b = 0; b < SpecBands; b++)
            {
                float rms = specNow[b];
                float cuerpo = 0f;
                if (rms > 1e-6f)
                {
                    float db = (float)(20.0 * Math.Log10(rms)) + SpecTilt[b];
                    cuerpo = (db - SpecPisoDb) / SpecRangoDb;
                    if (cuerpo < 0f) cuerpo = 0f; else if (cuerpo > 1f) cuerpo = 1f;
                    cuerpo *= cuerpo * (3f - 2f * cuerpo);   // suavizado: aplana el ruido de fondo y realza los picos
                }
                // PISO MOVIL de la banda (~0.9 s): lo que esta banda ha venido sonando ultimamente.
                float piso = specPiso[b];
                piso += (rms - piso) * kPiso;
                specPiso[b] = piso;
                // GOLPE: cuanto sobresale AHORA por encima de ese piso. Ataque instantaneo, caida rapida.
                float golpe = 0f;
                if (rms > piso * 1.18f + 0.0008f)
                {
                    golpe = (rms - piso) * 9f;
                    if (golpe > 1f) golpe = 1f;
                }
                float gp = specGolpe[b];
                specGolpe[b] = (golpe > gp) ? golpe : gp * kGolpe;   // ~0.25 s de caida (0,80 por latido de 42 ms)
                float v = cuerpo * 0.55f + specGolpe[b] * cuerpo * 0.45f;   // el golpe solo luce donde ya hay banda
                if (v > 1f) v = 1f;
                float prev = specVis[b];
                specVis[b] = (v > prev) ? v : prev + (v - prev) * kBaja;   // sube al instante, baja en ~0.2 s
            }
        }
        // Y la PRESENCIA: sube y baja despacio, para que la aurora no aparezca ni desaparezca de golpe.
        // Sin musica NO cae a cero: cae al REPOSO, que ademas respira (ver AurReposo).
        private void AvanzaPresencia(float pasos)
        {
            auroraFase += AurReposoPaso * pasos;
            if (auroraFase > 6.2831853f) auroraFase -= 6.2831853f;
            float pico = 0f;
            for (int b = 0; b < SpecBands; b++) if (specVis[b] > pico) pico = specVis[b];
            bool hayMusica = pico > 0.02f;
            // Con musica sube DEPRISA (~1.3 s) hasta arriba; sin ella baja DESPACIO (~3 s) hasta el reposo, y
            // ahi se queda respirando a ese mismo ritmo lento, que es lo que la deja viva sin llamar la
            // atencion. La respiracion NO usa el ritmo rapido: si no, subiria a tirones y bajaria suave.
            // Y LA AURORA FRENA IGUAL. Respira desde antes que la pared, pero hasta ahora no se paraba con
            // nadie: seguia moviendose delante del que estaba eligiendo cancion. Es la misma queja, asi que
            // pasa por la misma cuenta, con la misma quietud y el mismo tope.
            float meta = hayMusica ? 1f : (AurReposo * Respiracion(AurReposoOnda, quietud, auroraFase));
            auroraPres += (meta - auroraPres) * AurPorPasos(hayMusica ? 0.075f : 0.014f, pasos);
            if (auroraPres < 0f) auroraPres = 0f; else if (auroraPres > 1f) auroraPres = 1f;
        }

        // Estado del FONDO REACTIVO para el panel GPU. El relleno de color se apoya justo encima de la franja
        // de estado (BotStripTop), que es donde el cielo se acaba, y sube desde ahi. Nunca se apaga -sin
        // musica se queda en reposo, ver AurReposo- y se atenua junto con el fondo mientras se navega, para
        // que nunca le pelee a las portadas ni al texto.
        private GpuAurora BuildAuroraState()
        {
            GpuAurora g = new GpuAurora();
            if (auroraAmount <= 0.004f) return g;
            if (auroraPres <= 0.004f) return g;   // solo al arrancar, mientras la presencia sube desde cero
            int h = ClientSize.Height;
            g.Has = true; g.B = specVis;
            g.BaseY = BotStripTop(h) + 6f;
            // ALTO al que llega el color cuando una banda pega a tope. Ahora es un relleno que se desvanece
            // hacia arriba (antes eran focos), asi que puede ocupar bastante mas pantalla sin molestar: lo
            // fuerte se queda pegado abajo y lo de arriba ya casi no se ve. Eso es lo que hace que se note
            // crecer y encoger: con el ajuste de fabrica el color llega a casi el 40% de la pantalla en los
            // golpes y se encoge a menos de la decima parte cuando la cancion afloja.
            g.Alto = h * 1.02f * auroraAmount;
            // COLOR. La intensidad ya NO se dibuja con blanco -eso quemaba el centro y lavaba todo-, sino con
            // TONOS DEL MISMO COLOR: si el genero es morado, lo flojo sale morado oscuro y profundo, lo medio
            // sale del morado del genero, y lo fuerte sale de un morado claro. Nunca se sale de su familia, asi
            // que el fondo se lee como UN color con relieve en vez de como una mancha que se lava en el pico.
            // La escalera va abajo, en TonoPorNivel (la calcula el panel para cada punto).
            //
            // A lo ancho el tono tambien cambia -graves de un lado, agudos del otro- pero MUCHO mas suave que
            // antes (32 grados en vez de 98): asi los colores se funden en vez de parecer dos luces distintas.
            //
            // Y A LO ALTO cambia tambien: el PIE de la cortina lleva el color de la cancion y la PUNTA tira
            // hacia el violeta-rosa, que es lo que hace una aurora de verdad (ver GiroPunta).
            Color baseCol = ColorReactivo(bgAccent);
            float gp = GiroPunta(baseCol);
            g.Rgb = (uint)(GiraTono(baseCol, -10f, 1.00f).ToArgb() & 0x00FFFFFF);        // pie, extremo GRAVE (izquierda)
            g.Rgb2 = (uint)(GiraTono(baseCol, 22f, 0.98f).ToArgb() & 0x00FFFFFF);        // pie, extremo AGUDO (derecha)
            g.RgbA = (uint)(GiraTono(baseCol, gp - 12f, 0.98f).ToArgb() & 0x00FFFFFF);   // punta, extremo GRAVE
            g.Rgb2A = (uint)(GiraTono(baseCol, gp + 18f, 0.96f).ToArgb() & 0x00FFFFFF);  // punta, extremo AGUDO
            // ATENUACION al navegar (la misma idea que baja las estrellas al 45%: mientras alguien esta
            // eligiendo cancion, el fondo se calma y cansa menos la vista). La aurora ya no se queda casi como
            // estaba: baja al 65%. Antes no podia bajar porque estaba floja de por si y desaparecia; ahora sale
            // bastante mas fuerte, asi que en reposo -que es cuando nadie mira de cerca- luce entera, y en
            // cuanto alguien toca una tecla se serena hasta que vuelve a quedarse quieta.
            // 'quieto' va de 0 (alguien esta tocando botones) a 1 (nadie toca nada), siguiendo la misma
            // transicion suave que ya anima bgDim, asi que la aurora se calma y vuelve sin ningun salto.
            float quieto = (bgDim - BgDimLevel) / (1f - BgDimLevel);
            quieto = Clampf(quieto, 0f, 1f);
            g.Dim = (AurNavLevel + (1f - AurNavLevel) * quieto) * auroraPres;   // la presencia hace el fundido de entrada y de salida
            g.DimFilo = AurNavFilo + (1f - AurNavFilo) * quieto;
            // CUANTO HAY DE REPOSO en lo que se ve: 0 con la cancion sonando, 1 con la rocola callada. Es lo
            // que le dice al panel si tiene que dibujar la forma lenta (AurCalma) en vez de las bandas. Va
            // pegado a la presencia, asi que el relevo entre una y otra es el mismo fundido suave de siempre.
            // La cuenta se mide contra el punto MAS ALTO de la respiracion: asi, en todo el rato que la
            // rocola esta callada, el reposo vale 1 entero y su forma no cambia de tamano al respirar (lo que
            // respira es el brillo, que es la presencia). Con musica cae a cero solo.
            float cal = (1f - auroraPres) / (1f - AurReposo * (1f + AurReposoOnda));
            g.Calma = (cal < 0f) ? 0f : (cal > 1f ? 1f : cal);
            return g;
        }

        // COLOR DE LA CANCION para el fondo reactivo. NO vale el color del cielo: el cielo de abajo ya es el
        // color del genero apagado, asi que pintar encima el mismo color apagado no se ve. Aqui se toma el
        // color del genero y se sube a su brillo pleno, conservando el tono. Sobre el cielo oscuro eso SI se
        // ve: es el color de la cancion, no un aclarado del fondo. De ahi para abajo, la escalera de tonos
        // (TonoPorNivel) lo lleva de un tono hondo cuando afloja a uno claro cuando pega.
        private static Color ColorReactivo(Color accent)
        {
            int mx = accent.R; if (accent.G > mx) mx = accent.G; if (accent.B > mx) mx = accent.B;
            if (mx < 10) return Color.FromArgb(255, 60, 120, 235);   // sin genero: el azul de siempre
            return Color.FromArgb(255, accent.R * 250 / mx, accent.G * 250 / mx, accent.B * 250 / mx);
        }


        // CUANTO se gira el color al subir por la cortina (grados). En las auroras de verdad la punta tira
        // SIEMPRE hacia el violeta o el rosa, sea del color que sea el pie: el verde se vuelve magenta y el
        // cian se vuelve violeta. Girar siempre los mismos grados no sirve, porque desde un naranja te lleva
        // al verde pistacho y queda sucio. Aqui se gira hacia la zona violeta-rosa por el camino mas corto y
        // a poco mas de medio camino, con un giro MINIMO para que el color viaje tambien cuando el genero ya
        // es morado, y un TOPE para que nunca se vaya al otro lado de la rueda.
        internal static float GiroPunta(Color c)
        {
            float d = 300f - TonoDe(c);
            while (d > 180f) d -= 360f; while (d < -180f) d += 360f;
            float g = d * 0.55f;
            float mag = (g < 0f) ? -g : g;
            if (mag < 35f) g = (g < 0f) ? -35f : 35f;
            else if (mag > 75f) g = (g < 0f) ? -75f : 75f;
            return g;
        }

        private GpuTrans BuildTransState()
        {
            GpuTrans g = new GpuTrans();
            if (transMode == 0 || transImg == null) return g;
            float t = (float)(CubeFlightElapsed(transTick) / TransMs);   // reloj fino; la cadencia del cubo se aplica en el panel
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float e = t * t * t * (t * (t * 6f - 15f) + 10f);      // suavizado quintico
            RectangleF a = transFrom, b = transTo;
            g.Has = true; g.Bmp = transImg as Bitmap;
            g.Key = transTick;
            if (g.Bmp == null) { g.Has = false; return g; }
            g.X = a.X + (b.X - a.X) * e; g.Y = a.Y + (b.Y - a.Y) * e;
            g.W = a.Width + (b.Width - a.Width) * e; g.H = a.Height + (b.Height - a.Height) * e;
            g.Angle = transAng0 + transSpin * e;                   // gira mientras viaja y llega alineado
            double s = Math.Sin(Math.PI * t);
            g.Tilt = (float)(s * 0.14);                            // bamboleo: se ladea y vuelve a enderezarse
            g.Glow = (float)s;                                     // destello: nace, brilla a media ruta y se apaga
            if (t < TransHold) g.Alpha = 1f;                       // opaca casi todo el viaje
            else { float k = (t - TransHold) / (1f - TransHold); g.Alpha = 1f - k * k * (3f - 2f * k); }
            return g;
        }

        // El buscador sube desde fuera de pantalla, a escala nativa y frenando al llegar.
        // Se mueve su textura ya preparada: no se recompone por fotograma.
        internal static float DesplazamientoBuscador(float t, float alto, bool abre)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float u = abre ? 1f - t : t;
            return Math.Max(0f, alto) * u * u * u;
        }
        private GpuWin BuildWinState()
        {
            GpuWin g = new GpuWin();
            if (winMode == 0) return g;
            float t = (Environment.TickCount - winTick) / (float)WinMs;
            if (t < 0f) t = 0f;
            if (t >= 1f) return g;   // RED DE SEGURIDAD: pasado el tiempo se da por terminada aunque el temporizador
                                     // no la haya apagado todavia, asi la UI nunca se puede quedar escondida.
            if (winMode == 1)
            {
                g.Scale = g.ScaleY = 1f;
                g.Dy = DesplazamientoBuscador(t, ClientSize.Height, true);
                g.Alpha = 1f;
                g.Bmp = null; g.HideUi = true;                      // se anima la PROPIA textura de UI (ya es el buscador)
            }
            else
            {
                g.Scale = g.ScaleY = 1f;
                g.Dy = DesplazamientoBuscador(t, ClientSize.Height, false);
                g.Alpha = 1f;
                g.Bmp = winSnap; g.Robada = winRobada && winSnap == null;   // la imagen copiada, o la textura que tenia la UI
                if (g.Bmp == null && !g.Robada) return g;           // ni una ni otra: la textura de UI ya es la de navegacion
                g.HideUi = false;                                   // esa UI (la rejilla) SI se dibuja: la ventana se va sobre ella
            }
            g.Has = true;
            return g;
        }
#endif

        // LA MARCA DE "MEDIDO Y EN SILENCIO". Un archivo sin nada de sonido da cero al medirlo, y con cero
        // el pre-analisis no lo guardaba: se quedaba como "sin medir" y se volvia a medir en cada arranque,
        // para siempre. Se le pone este valor minusculo -por debajo del umbral con el que TODO el programa
        // decide "esto no tiene medida util", asi que nadie va a intentar subirlo cien mil veces- para que
        // quede apuntado como lo que es.
        private const float LoudMudo = 0.00001f;   // se guarda y se relee tal cual

        private void PlayTrack(Track t)
        {
            try { BeginAudioOpen(t.Path, delegate(IAudioSource source, Exception error) { StartOpenedTrack(t, source, error); }); }
            catch (Exception error) { AudioStartFailed(t, error); }
        }
        private void StartOpenedTrack(Track t, IAudioSource source, Exception error)
        {
            VigiaPantalla.Hace("arrancar la cancion ya abierta");
            try
            {
                reader = source;
                int abiertaMs = Desde(bitacoraCancion);   // (bitacora: MedidasBitacora.cs)
                if (error != null) throw error;
                reader.Volume = FixedVolume;   // se queda en 1.0 (crudo): el nivelado se aplica en LevelTap, tras medir el volumen REAL
                // RETOMADA: volvio el aparato de sonido y la cancion sigue desde donde iba (SalidaSonido.cs).
                double desde = TomaReanudar();
                if (desde > 0) reader.Salta(TimeSpan.FromTicks((long)Math.Round(desde * TimeSpan.TicksPerSecond)));
                // LA CADENA DE ESTA CANCION (ArmaCadena, mas abajo): la misma que arma el relevo sin pausa para la
                // siguiente, asi que las dos suenan exactamente igual.
                retardoFinalReloj = 0;
                CadenaCancion cad = ArmaCadena(t, reader, false, desde);
                SellaInicioReloj(cad);
                AFrecuenciaDeLaTarjeta(cad);   // A4: a la frecuencia fija de la tarjeta, si se eligio una
                cad.Item = nowPlaying;
                // Lo que se aprende oyendola va en los campos de siempre (la salida aun no arranco: sin carrera).
                curMeasurePath = cad.MedidaRuta; curSumSq = 0; curSamples = 0;
                curFuenteMedida = cad.Fuente;
                eqMeasPath = cad.EqRuta;
                eqMeasCut = 0f;   // el acantilado es de ESTA cancion: se vuelve a medir
                eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0;
                kwRate = 0;   // reinicia el estado del K-weighting: cada cancion mide desde cero (sin cola del filtro anterior)
                tapMedido = cad.Tap;
                curSbr = cad.Sbr; curTap = cad.Tap; curBass = cad.Bajo;   // para ajustarla en vivo (Ctrl+F3)
                bassSongFactor = cad.BajoFactor; bassAutoBase = cad.BajoBase;
                AprendeParejo(cadenaSonando);   // la de antes, si aun no se habia soltado (VolumenParejo.cs)
                cadenaSonando = cad; cadenaPreparada = null;
                // RELEVO SIN PAUSA (SinPausa.cs): la salida lee de la secuencia. Cuando esta cancion se acabe, si la
                // siguiente ya esta preparada, sigue en la MISMA lectura sin cerrar la salida.
                secuencia = new Encadenado(cad.Salida, cad);
                NAudio.Wave.ISampleProvider outChain = secuencia;
                outChain = CallaSiEsMedida(outChain);   // la medida de rendimiento: callada aqui, sin tocar volumenes de Windows (SalidaSonido.cs)
                // WaveOut (callback por ventana, 1 segundo de colchon en bloques de 50 ms): en XP, 16 bits con dither
                // TPDF como siempre; en Vista o posterior, coma flotante, y 16 bits si el sistema la rechaza
                // (SalidaSonido.cs).
                try { AbreSalidaClasica(outChain, FlotanteAdmitido()); }
                catch (Exception sex)
                {
                    // EL APARATO, NO LA CANCION: se espera y se retoma la misma (SalidaSonido.cs).
                    if (EsFalloDelDispositivo(sex) && SalidaCaida(t, desde, sex, false)) return;
                    throw;
                }
                // Sin parar la pantalla: la salida arranca cuando el hilo de la musica tiene el primer segundo (BombaSonido.cs).
                if (ArrancaCuandoEsteLista(delegate { SalidaClasicaLista(t, desde, abiertaMs); })) return;
                try { output.Play(); }
                catch (Exception pex) { if (EsFalloDelDispositivo(pex) && SalidaCaida(t, desde, pex, false)) return; throw; }
                SalidaArranco(IdSalidaPorDefecto());   // el aparato esta: en cual (SalidaSonido.cs; en XP no se pregunta)
                progressTimer.Start();
                UpdateNowPlayingUI(); // lector ya listo; refrescar estado tras la apertura asincrona
                PrecalientaSiguiente();   // la que viene detras, leida en segundo plano (ver arriba)
                ApuntaCancion(abiertaMs);
            }
            catch (Exception ex)
            {
                AudioStartFailed(t, ex);
            }
        }

        // LA CADENA DE UNA CANCION, del lector al fundido de salida (sin la salida). La usan el arranque de
        // siempre (StartOpenedTrack) y el relevo sin pausa (Form1.SinPausa.cs), asi que las dos suenan igual.
        // Solo ARMA: no toca lo que suena ahora (ni los campos de medida ni los controles en vivo).
        // 'saltadoSeg': la cancion se retoma desde ahi (volvio el aparato de sonido; SalidaSonido.cs); 0 = entera.
        private CadenaCancion ArmaCadena(Track t, IAudioSource source, bool iaMejorada, double saltadoSeg)
        {
            // La comparacion a ciegas se acaba antes de armar otra cancion: si no, se armaria con lo del tramo que suene (CompararCiegas.cs).
            if (ciegoActivo) TerminaCiego("Se acaba la cancion: comparacion cortada. ");
            CadenaCancion c = new CadenaCancion();
            c.Cancion = t; c.Lector = source; c.IaMejorada = iaMejorada;
            OlvidaSiEsOtroArchivo(t.Path, source);   // en esa ruta hay hoy otro archivo: lo aprendido del de antes no vale
            c.Fuente = FuenteDeLector(t.Path, source);
            if (c.Fuente != null && !c.Fuente.Igual(CapturaFuente(t.Path))) OlvidaMedidas(t.Path);
            // NIVELADO: si esta cancion ya se oyo antes, se aplica la ganancia que la lleva al nivel comun; si es
            // nueva, va a su volumen original y se APRENDE su sonoridad en esta pasada (para nivelarla la proxima vez).
            // De una cancion retomada a medias no se aprende nada: seria la medida de un trozo.
            bool entera = saltadoSeg <= 0 && !iaMejorada;
            c.MedidaRuta = (entera && normalizeOn && !durCache.ContainsKey(t.Path)) ? t.Path : null;   // ya pre-analizada = medida COMPLETA y fiable: no se vuelve a medir (y se ahorra el K-weighting)
            c.EqRuta = (entera && EqMideHuella && !HasStereoEq(t.Path)) ? t.Path : null;   // actualiza huellas antiguas sin borrar la biblioteca
            // NIVEL 2 (reconstructor espectral): solo se INSERTA en maquinas que cumplen requisitos; con
            // Active=false es un pase directo (costo ~cero). Ctrl+F3 lo alterna EN VIVO para comparar A/B.
            FuenteOriginal original = new FuenteOriginal(source, this);
            NAudio.Wave.ISampleProvider chain = original;
            int audioTailDelay = 0;   // estos retardos se vacian: el fundido debe abarcar tambien su cola
            // LOS ARREGLOS DE LA GRABACION (S1 y S2, ArreglosGrabacion.cs): un canal mudo por los dos y la continua fuera. Lo
            // primero, para que nada de lo de detras trabaje con el defecto. Sin retardo; sin el defecto, sale bit a bit.
            c.Arreglo = new ArreglaGrabacion(chain, CanalMudoEf, QuitaContinuaEf); chain = c.Arreglo;
            // EL ZUMBIDO DE LA CORRIENTE (T6, Zumbido.cs): solo en las canciones en que el repaso lo midio. Va siempre que lo traen, con el
            // interruptor decidiendo si actua (asi se enciende y se apaga en vivo); apagado, pasa tal cual. Sin retardo.
            int zumbidoHz = ZumbidoDe(t.Path);
            if (zumbidoHz > 0) { c.Zumbido = new QuitaZumbido(chain, zumbidoHz, QuitaZumbidoEf); chain = c.Zumbido; }
            // EL PRE-ECO (R2, PreEco.cs): con su interruptor y solo en las canciones en que el repaso lo midio. Retrasa 64 ms (a la cola del
            // fundido). Sin el interruptor o sin pre-eco medido ni se pone: sale bit a bit como antes.
            if (quitaPreEco && PreEcoDe(t.Path))
            {
                QuitaPreEco pe = new QuitaPreEco(chain); chain = pe; audioTailDelay += pe.DelayFrames; c.PreEco = pe;
            }
            // DES-RECORTE: repara picos aplastados de origen (despues de medir original, antes de DSEE). Ya sabemos, cancion
            // por cancion, si de verdad viene recortada; en las que NO -que son la mayoria- ni se pone en la
            // cadena: se ahorra su trabajo y su retardo de ~1.5 ms. Si aun no esta medida, se pone por si acaso.
            if (declipOn && DeclipVale(t.Path))
            {
                DeclipProvider dc = new DeclipProvider(chain); chain = dc; audioTailDelay += dc.DelayFrames; c.Declip = dc;
            }
            // QUITAR EL SISEO (I2, Siseo.cs): con su interruptor, antes del DSEE y del realce (que si no le darian brillo al
            // siseo). Retrasa 11,6 ms, que se suman a la cola que el fundido tiene que abarcar.
            if (quitaSiseo && !localAuto)   // en el modo billar no (CadenaBillar.cs)
            {
                QuitaSiseo qs = new QuitaSiseo(chain); chain = qs; audioTailDelay += qs.DelayFrames; c.Siseo = qs;
            }
            if (sbrAllowed)
            {
                c.Sbr = new SpectralTap(chain);
                c.Sbr.ConectaOriginal(original, audioTailDelay);
                c.Sbr.Red = ModeloDseePara(t);   // I3: el modelo de su genero, si se pidio y lo hay (Form1.Ia.cs)
                c.Sbr.Active = DseeEfectivo; chain = c.Sbr;
            }
            LevelTap tap = new LevelTap(chain, this);
            tap.Cebado = audioTailDelay;   // lo que llega de cebado (des-recorte, siseo): en un disco continuo no entra en el realce heredado (Realce.cs)
            original.Tap = tap; tap.MideOriginal = true;
            tap.Gain = GananciaDe(t);   // la del disco si suena en racha con otra de su disco (NivelDisco.cs); si no, la suya
            tap.Excite = ExciteAmountFor(t.Path);   // realce de agudos: mas a las dañadas, menos a las que ya traen su aire
            tap.Master = MasterEfectivo() * FactorNoche();  // SUBIR TODO (SubirTodo.cs), y de madrugada mas bajo (el modo noche)
            tap.EqDb = EqCorrFor(t.Path);      // EQ automatico: correccion aprendida (null la 1a vez que suena)
            audioTailDelay += tap.DelayFrames;   // realce 2x: retiene 31 cuadros y los entrega al final (Realce.cs)
            c.Tap = tap; tap.Medida = new MedidaCancion(c.MedidaRuta, c.EqRuta);   // lo que aprende oyendola (ver MedidaDe)
            tap.Medida.Fuente = c.Fuente;
            // GRAVE EN MONO: por debajo del corte, el mismo grave por todas las bocinas. Y, con sus interruptores de Ctrl+F3,
            // para las bocinas del billar: sin el subgrave que ninguna reproduce y con el estereo mas estrecho (GraveMono.cs).
            AjusteLocal loc = LocalDe(c);   // los mandos tal cual, o lo que decide el modo automatico del local para esta cancion, fijado en ella (LocalAuto.cs)
            // Con «Escuchar sin mejoras» puesto la cancion NACE sin lo del local (sin rampa de entrada que lo desvanezca); el subgrave y el estereo se quedan.
            if (sinMejoras) { loc.Claridad = false; loc.Suaves = false; loc.Presencia = false; loc.Graves = 0; loc.Agudos = 0; loc.Retumbe = false; loc.Ataque = false; loc.Aspereza = false; }   // el bombo se queda: es de la familia del limitador
            c.Mono = new GraveMono(tap, graveMonoHz, anchoEstereo, SubgraveEf, loc.Estrecho, loc.Subgrave);   // en el billar, a 40 Hz (CadenaBillar.cs)
            c.Mono.SoloSiHaceFalta(loc.EstrechoSoloSiHaceFalta);   // y solo si hace falta (S3)
            // Lo que llega de cebado (los retardos de hasta aqui): en un disco continuo pasa sin tocar los filtros heredados (GraveMono.cs).
            c.Mono.Cebado = audioTailDelay;
            // EL LOCAL (SalaLocal.cs): claridad, partes suaves y balance, con sus mandos de Ctrl+F3 (apagados de fabrica:
            // apagados no tocan ni una muestra). Delante del bajo psicoacustico y del limitador, que cuida los picos.
            float nivelLocal = NivelLocalDe(t, tap.Gain);   // en el billar, el nivel de ESTA cancion; si no, normTarget (CadenaBillar.cs)
            c.Sala = new SalaLocal(c.Mono, tap, nivelLocal, loc.Claridad, loc.Suaves, balanceDb, loc.Graves, loc.Agudos);
            c.Sala.Cebado = audioTailDelay;
            // RETUMBE DINAMICO (RetumbeDinamico.cs, 27-10-2026): detras del EQ automatico y la claridad (que ya cortan ese rango: su corte se descuenta
            // del presupuesto) y delante del compresor. Apagado (de fabrica) no toca ni una muestra.
            c.Retumbe = new RetumbeDinamico(c.Sala, loc.Retumbe, loc.Claridad, RetumbeDinamico.CorteEstaticoDb(tap.EqDb, loc.Claridad, c.Sala.WaveFormat.SampleRate));
            c.Retumbe.Cebado = audioTailDelay;
            // PRESENCIA EN SALA RUIDOSA (PresenciaSala.cs): un compresor suave detras del local; apagado (de fabrica) no toca ni una muestra.
            c.Presencia = new PresenciaSala(c.Retumbe, tap, nivelLocal, loc.Presencia, localAuto);
            if (!presenciaSinGrave) c.Presencia.SinGrave = false;   // solo durante "Presencia: nueva o de antes" (CompararCiegas.cs)
            c.Presencia.Cebado = audioTailDelay;
            // ATAQUE MAS CLARO (AtaqueClaro.cs, 27-10-2026): detras del compresor y delante de las eses. Apagado (de fabrica) no toca ni una muestra.
            c.Ataque = new AtaqueClaro(c.Presencia, loc.Ataque);
            c.Ataque.Cebado = audioTailDelay;
            // SUAVIZAR LAS ESES (T9, Eses.cs): detras de lo que sube los agudos, delante del bajo y del limitador. Apagada (de fabrica) ni se
            // calcula; encendida, fuera de las "s" sale bit a bit.
            // AGUDOS ASPEROS DINAMICOS (AsperezaDinamica.cs, 27-10-2026): detras del ataque (que puede levantar un platillo) y delante de las eses
            // (que van mas arriba). Apagado (de fabrica) no toca ni una muestra.
            c.Aspereza = new AsperezaDinamica(c.Ataque, loc.Aspereza);
            c.Aspereza.Cebado = audioTailDelay;
            c.Eses = new SuavizaEses(c.Aspereza, loc.Eses, localAuto);   // en el billar, con la referencia por encima de 1 kHz (CadenaBillar.cs)
            c.Eses.Cebado = audioTailDelay;
            // BAJO PSICOACUSTICO: despues de nivelar/ecualizar y ANTES del limitador, que es quien controla
            // los picos que anaden los armonicos.
            c.BajoFactor = BassFactorFor(t.Path);   // cuanto le toca a ESTA cancion (1 = la perilla tal cual)
            c.BajoBase = BajoAuto(t.Path);          // y de cuanto se parte, si el bajo va en automatico
            c.Bajo = new DeepBassProvider(c.Eses, BassAmountPara(c.BajoFactor, c.BajoBase));
            c.Bajo.Cebado = audioTailDelay;
            // QUE EL BOMBO NO HUNDA LA VOZ (BomboSinHundir.cs, 27-10-2026): el bajo con su propio margen, pegado al limitador. Apagado (de fabrica) no toca
            // ni una muestra; encendido, mientras nada pase del techo, tampoco.
            c.Hundir = new BomboSinHundir(c.Bajo, loc.Bombo);
            c.Hundir.Cebado = audioTailDelay;
            // LIMITADOR DE PICO REAL: techo con margen para el error de estimacion entre
            // muestras (ver PicoReal.cs). Detras solo quedan el fundido (que solo baja) y el paso a 16 bits
            // con su dither de un bit.
            // EN EL BILLAR, lo que le llega al limitador se mide para el volumen parejo (VolumenParejo.cs). Pasa tal cual.
            NAudio.Wave.ISampleProvider alLimitador = c.Hundir;
            if (localAuto) { c.Margen = new MideMargen(c.Hundir, TechoPicoReal, saltadoSeg < 0.5, ParejoAplicado(t), SegundosDe(t.Path)); alLimitador = c.Margen; }
            LimitadorPicoReal lim = new LimitadorPicoReal(alLimitador, TechoPicoReal, 3.0, localAuto);   // en el billar, respira menos (PicoReal.cs)
            lim.Cebado = audioTailDelay;   // disco continuo: el cebado de delante no entra en el limitador heredado (PicoReal.cs)
            audioTailDelay += lim.DelayFrames; c.Limitador = lim;
            NAudio.Wave.ISampleProvider outChain = lim;
            if (crossfadeSec > 0.01f)
            {
                // CUANTO DURA. Se pregunta primero a lo que el pre-analisis dejo apuntado, que es de
                // donde lo saca tambien el turno de la cola. Preguntarselo AL ARCHIVO obliga a
                // recorrerlo entero cuando no trae la cuenta escrita, que es lo normal: 3.87 ms y
                // nueve megas de disco en una cancion de cuatro minutos, y aqui eso caeria en el hilo
                // de la pantalla, en el instante en que el cliente acaba de soltar el boton. El camino
                // largo se deja solo para las canciones de las que aun no se sabe nada.
                long tf = 0;
                try
                {
                    int hz = source.WaveFormat.SampleRate;
                    float yaSe = SegundosDe(t.Path);
                    FuenteSinSilencios sinSilencios = source as FuenteSinSilencios;
                    // LO APUNTADO TIENE QUE SER DE ESTE ARCHIVO. Se apunta por ruta, y en esa ruta puede haber hoy otro
                    // archivo (la cancion completa en lugar de una descarga cortada, otra version con el mismo nombre). Con
                    // la duracion del de antes, el fundido bajaba a cero donde acababa aquel y el resto de la cancion pagada
                    // sonaba en SILENCIO. El lector ya esta abierto y sabe lo que dura sin leer nada (el MP3 se indexo al
                    // abrirlo): si no cuadra con lo apuntado (un segundo de margen), manda el archivo.
                    double delArchivo = 0;
                    try { delArchivo = ((sinSilencios != null) ? sinSilencios.Interior : source).TotalTime.TotalSeconds; } catch { }   // si no lo sabe, como antes
                    if (yaSe > 0.01f && delArchivo > 0 && Math.Abs(delArchivo - yaSe) > 1.0) yaSe = 0f;
                    tf = (yaSe > 0.01f) ? (long)(yaSe * hz) : (long)(delArchivo * hz);
                    FuenteSinRelleno sinRelleno = ((sinSilencios != null) ? sinSilencios.Interior : source) as FuenteSinRelleno;
                    if (sinRelleno != null && saltadoSeg <= 0) tf = sinRelleno.CuadrosMusica;   // sin el relleno acaba antes: el fundido tambien
                    // Sin los silencios largos: lo que queda por sonar desde donde va (el principio mudo ya se salto al
                    // abrirla, y una retomada ya movio la cuenta), asi el fundido cae en el margen mudo del final.
                    if (sinSilencios != null) tf = sinSilencios.Salen(tf);
                    else tf -= (long)(saltadoSeg * hz);   // retomada: el fundido cuenta desde donde se retomo
                }
                catch { }
                if (tf > 0) tf += audioTailDelay;
                c.Fundido = new FadeProvider(outChain, tf, crossfadeSec, audioTailDelay,
                                             saltadoSeg > 0 ? FadeProvider.EntradaRetomadaSeg : FadeProvider.EntradaSeg);   // fundido suave de entrada/salida
                outChain = c.Fundido;
            }
            else
            {
                // SIN FUNDIDO DE SALIDA ("fundido=0"): la entrada de 5 ms (o de 0,3 s si se retoma) no depende de el. Antes, sin fundido
                // tampoco habia entrada y una cancion que empieza en su pico arrancaba de golpe. Sin total, no hay fundido de salida.
                c.Fundido = new FadeProvider(outChain, 0, 0, audioTailDelay,
                                             saltadoSeg > 0 ? FadeProvider.EntradaRetomadaSeg : FadeProvider.EntradaSeg);
                outChain = c.Fundido;
            }
            c.Retardo = audioTailDelay; c.Salida = outChain;
            AplicaSinMejoras(c);   // «Escuchar sin mejoras» (LoQueSuena.cs): una cancion armada mientras dura (el relevo, o la siguiente) nace ya sin ellas; si no, sale como siempre
            return c;
        }

        // Abre el audio. Camino NORMAL: FileAudioSource (rapido, para el 99.99% de los archivos). Si NAudio lo
        // rechaza (tipico: MP3 que CAMBIA de frecuencia a mitad -> "Got a frame at sample rate ..."), reintenta con
        // un lector TOLERANTE que reconstruye el MP3 dejando solo los frames de la frecuencia dominante. Si tampoco
        // asi, relanza el error original (el llamador lo anota y salta). El reintento NUNCA afecta a los archivos
        // sanos y, en el peor caso, hace lo mismo que antes: saltar.
        private IAudioSource OpenAudio(string path)
        {
            string original = RutaOriginalDe(path);
            FuenteMedida inicio = CapturaFuente(original);
            try { return SinSilencios(path, SinRelleno(path, SellaFuenteAudio(original, inicio, ComoEstereo(new FileAudioSource(path))))); }   // sin el hueco del codificador ni los silencios largos
            catch
            {
                bool isMp3 = path != null && path.Length > 4 && path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
                if (isMp3)
                {
                    MemoryStream clean = null; Exception porQue = null;
                    try
                    {
                        clean = BuildCleanMp3(path);
                        if (clean != null) return SinSilencios(path, SellaFuenteAudio(original, inicio, ComoEstereo(new TolerantMp3Source(clean))));   // reconstruido a una sola frecuencia
                        porQue = new InvalidDataException("casi no quedaron cuadros de una sola frecuencia y canales");
                    }
                    catch (Exception ex2) { porQue = ex2; if (clean != null) { try { clean.Dispose(); } catch { } } }   // el tolerante tampoco pudo: cae al throw de abajo
                    // Que quede escrito POR QUE no pudo la reconstruccion (antes se tragaba): sin esto solo se veia el error del archivo.
                    try { LogError("MP3 con frecuencias mezcladas: la reconstruccion tampoco pudo con " + Path.GetFileName(path), porQue); } catch { }
                }
                throw;   // que el catch de PlayTrack muestre el aviso, lo anote y salte a la siguiente
            }
        }

        // Reconstruye un MP3 de frecuencia MEZCLADA como uno de una sola frecuencia (la dominante), copiando solo
        // los frames de esa frecuencia a memoria. NAudio 1.9 no soporta cambios de frecuencia dentro de un MP3;
        // esto le entrega un stream que SI puede reproducir. Los frames de la minoria (raros) se descartan: a lo
        // sumo un pequeño salto. Todo con la API de frames de NAudio, disponible en XP (decodificacion ACM).
        private static MemoryStream BuildCleanMp3(string path)
        {
            // La clave es frecuencia Y canales (mono/estereo): el descompresor se arma una vez para un formato, y un cuadro de otros
            // canales a mitad tambien lo rompe. Un cuadro suelto de basura (los bytes de una portada dentro de la etiqueta que
            // parecen una cabecera) cae casi siempre en otra clave y se descarta con los demas.
            Dictionary<int, int> rateCount = new Dictionary<int, int>();
            using (FileStream fs = File.OpenRead(path))               // pasada 1: cuenta frecuencias -> elige la DOMINANTE
            {
                while (true)
                {
                    Mp3Frame f; try { f = Mp3Frame.LoadFromStream(fs); } catch { break; }
                    if (f == null) break;
                    int k = f.SampleRate * 4 + (f.ChannelMode == ChannelMode.Mono ? 1 : 2);
                    int c; rateCount.TryGetValue(k, out c); rateCount[k] = c + 1;
                }
            }
            if (rateCount.Count == 0) return null;
            int domRate = 0, best = -1;
            foreach (KeyValuePair<int, int> kv in rateCount) if (kv.Value > best) { best = kv.Value; domRate = kv.Key; }

            MemoryStream ms = new MemoryStream();
            using (FileStream fs = File.OpenRead(path))               // pasada 2: copia SOLO los frames de la frecuencia dominante
            {
                while (true)
                {
                    Mp3Frame f; try { f = Mp3Frame.LoadFromStream(fs); } catch { break; }
                    if (f == null) break;
                    if (f.SampleRate * 4 + (f.ChannelMode == ChannelMode.Mono ? 1 : 2) == domRate && f.RawData != null) ms.Write(f.RawData, 0, f.RawData.Length);
                }
            }
            if (ms.Length < 2048) { ms.Dispose(); return null; }     // casi nada util: que el tolerante falle y se salte
            ms.Position = 0;
            return ms;
        }

        // Anota (una sola vez, incluso entre reinicios) una cancion que no se pudo reproducir, en rocola_incompatibles.txt
        // junto al .exe: lista lista para reconvertir esos pocos archivos (p.ej. con ffmpeg -ar 44100).
        private void NoteIncompatible(string path, Exception ex)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !incompatibleLogged.Add(path)) return;   // ya anotada
                string reason = (ex != null) ? ex.Message : "desconocido";
                if (reason.Length > 160) reason = reason.Substring(0, 160);
                Bitacora.Apunta(RutaDatos("rocola_incompatibles.txt"), 0,   // detras de la pantalla, como la bitacora
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  |  " + path + "  |  " + reason + "\r\n");
            }
            catch { }
        }

        // Carga las rutas ya anotadas para no repetirlas en rocola_incompatibles.txt entre reinicios.
        private void LoadIncompatible()
        {
            try { ReadIncompatible(RutaDatos("rocola_incompatibles.txt"), incompatibleLogged); } catch { }
        }

        private static void ReadIncompatible(string f, HashSet<string> incompatibleLogged)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (StreamReader reader = new StreamReader(f))
                    ReadIncompatibleRecords(reader, incompatibleLogged);
            }
            catch { }
        }

        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadIncompatibleRecords(TextReader reader, HashSet<string> incompatibleLogged)
        {
            HashSet<string> pending = new HashSet<string>(incompatibleLogged.Comparer);
            FillIncompatibleRecords(reader, pending);
            foreach (string item in pending) incompatibleLogged.Add(item);
        }

        private static void FillIncompatibleRecords(TextReader reader, HashSet<string> pending)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int a = line.IndexOf("  |  ");
                if (a < 0) continue;
                int b = line.IndexOf("  |  ", a + 5);
                string p = (b > a) ? line.Substring(a + 5, b - (a + 5)) : line.Substring(a + 5);
                p = p.Trim();
                if (p.Length > 0) pending.Add(Pool(p));
            }
        }

        private void Output_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Algunos controladores WinMM notifican desde su hilo de callback. Toda la secuencia siguiente
            // toca controles y la cola, asi que siempre se termina en el hilo de la interfaz.
            if (InvokeRequired) { try { BeginInvoke((MethodInvoker)delegate { Output_PlaybackStopped(sender, e); }); } catch { } return; }
            if (!object.ReferenceEquals(sender, output)) return;   // callback viejo de una salida ya reemplazada
            if (EsMudaDiag(nowPlaying)) { DisposePlaybackNow(); return; }   // la muda del diagnostico acabo o fallo: ni credito, ni danada, ni PlayNext
            Exception deLaSalida = e.Exception;   // lo que aviso la salida misma (no un fallo de lectura de la cancion)
            // RELEVO SIN PAUSA: la siguiente que ya habia entrado y aun no se daba por sonando, y un fallo de lectura
            // que atrapo la secuencia (se atiende aqui, UNA vez, como si lo hubiera avisado la salida). Ver RecogeAlParar.
            if (secuencia != null) e = RecogeAlParar(e);
            // LA SALIDA CON HILO PROPIO FALLO POR SI MISMA (T5, SalidaPropia.cs): ni el aparato ni la cancion. Desde ahora, la de siempre, y
            // la misma cancion se retoma desde donde iba (como cuando falla el aparato), sin devolver el credito ni darla por rota.
            WaveOutPropio propia = sender as WaveOutPropio;
            if (deLaSalida != null && propia != null && propia.FalloPropio && !EsFalloDelDispositivo(deLaSalida))
            {
                salidaPropiaFallo = true;
                LogError("La salida de sonido con hilo propio fallo a media cancion: sigue la de siempre en esta sesion", deLaSalida);
                if (nowPlaying != null && !nowPlaying.IsVideo && SalidaCaida(nowPlaying.Song, PosicionActual(), deLaSalida, true)) return;
            }
            if (e.Exception != null)
            {
                // EL APARATO, NO LA CANCION (SalidaSonido.cs): se espera y se retoma la misma desde donde iba.
                if (deLaSalida != null && EsFalloDelDispositivo(deLaSalida) && nowPlaying != null && !nowPlaying.IsVideo
                    && SalidaCaida(nowPlaying.Song, PosicionActual(), deLaSalida, true)) return;
                EstadoPasajero("Se cort\u00f3 la canci\u00f3n: " + MotivoFallo(e.Exception), EstadoPasajeroMs);   // el detalle, en rocola_error.txt
                LogError("PlaybackStopped", e.Exception);
                // Si fue el ARCHIVO (no la salida ni el disco), tampoco se deja volver a pedir, como cuando falla al empezar
                // (AudioStartFailed): si no, cada vez sonaba un trozo, se cortaba y devolvia el credito.
                if (deLaSalida == null && nowPlaying != null && !nowPlaying.IsVideo && nowPlaying.Song != null) MarcaDanada(nowPlaying.Song.Path, e.Exception);
                RefundCredit();   // la cancion que el cliente pago fallo al reproducirse: se le devuelve el credito
                ShowToast("No se pudo reproducir: credito devuelto", Color.FromArgb(200, 80, 70));
                playFailStreak++;
                if (playFailStreak >= 3)
                {
                    // Algo de fondo esta mal (NAudio o formato). Dejamos de saltar para no ciclar.
                    DisposePlaybackNow();
                    nowPlaying = null;
                    RefrescaCola();   // idem: sin cancion no hay marca de "SONANDO"
                    EstadoPasajero("No se pudo reproducir. Detalle en rocola_error.txt (junto al programa)", 60000);
                    UpdateNowPlayingUI();
                    return;
                }
            }
            else playFailStreak = 0;   // termino bien una cancion -> reinicia el contador
            PlayNext();
        }

        // EL TOPE DE LA COLA DE MEDIDAS tiene que guardar MAS tiempo del que la pantalla espera a que se oigan (el
        // colchon de la salida mas lo que la bomba lee por delante: ~1 s con WaveOut). Eran 96: con los bloques de 50 ms
        // de WaveOut, 1,2 s. Pero la bomba (BombaSonido.cs) lee trozos de 25 ms y cada uno da 4 medidas de 6 ms: 96 eran
        // 0,6 s, lo mas viejo se tiraba ANTES de oirse y a la pantalla no le llegaba nunca el pulso. Las estrellas ni se
        // deslizaban ni latian, y la luna tampoco. 384 son 2,4 s en el peor caso; en memoria, 12 KB.
        private const int MedidasTope = 384;

        // Bajo levelLock: un espectro no se reutiliza hasta que sale de la cola y la UI
        // ha copiado sus valores. Reciclar antes del alta mantiene tambien el pico en MedidasTope.
        private void ReturnLevelBuffer(float[] values)
        {
            if (values == null) return;
            if (levelBuffers == null) levelBuffers = new Stack<float[]>(MedidasTope);
            if (levelBuffers.Count < MedidasTope) levelBuffers.Push(values);
        }
        private void EnqueueLevel(int at, float lo, float mid, float hi, float[] values, int offset)
        {
            while (levelQ.Count >= MedidasTope) ReturnLevelBuffer(levelQ.Dequeue().Esp);
            float[] esp = null;
            if (values != null)
            {
                esp = (levelBuffers != null && levelBuffers.Count > 0) ? levelBuffers.Pop() : new float[SpecBands];
                Array.Copy(values, offset, esp, 0, SpecBands);
            }
            levelQ.Enqueue(new Lvl(at, lo, mid, hi, esp));
        }
        private void ConsumeLevels(int now)
        {
#if ROCOLA_GPU
            bool fina = AuroraFinaViva();   // la aurora la lleva la tarjeta, cuadro a cuadro (AuroraFinaGpu.cs)
#else
            const bool fina = false;
#endif
            int retraso = RetrasoAudible();   // el colchon de la salida y lo que la bomba lee por delante
            lock (levelLock)
                while (levelQ.Count > 0 && unchecked(now - (levelQ.Peek().At + retraso)) >= 0)
                {
                    Lvl l = levelQ.Dequeue(); rmsLo = l.Lo; rmsMid = l.Mid; rmsHi = l.Hi;
                    if (l.Esp != null && !fina) Array.Copy(l.Esp, specNow, SpecBands);
                    ReturnLevelBuffer(l.Esp);
                }
        }
        private void ClearLevels()
        {
            lock (levelLock)
            {
                while (levelQ.Count > 0) ReturnLevelBuffer(levelQ.Dequeue().Esp);
#if ROCOLA_GPU
                AuroraFinaVacia();
#endif
            }
        }

        // ==========  BOTON REDONDEADO  ==========

        // Fuente de audio para reproducir: expone lo que la rocola necesita (muestras float para el tap + medicion,
        // volumen, y tiempo actual/total para la barra de progreso). Dos implementaciones: la NORMAL (AudioFileReader,
        // cualquier formato) y la TOLERANTE (MP3 con frecuencia mezclada que NAudio rechazaria).
        private interface IAudioSource : NAudio.Wave.ISampleProvider, IDisposable
        {
            float Volume { set; }
            TimeSpan CurrentTime { get; }
            TimeSpan TotalTime { get; }
            void Salta(TimeSpan donde);   // retomar a media cancion (volvio el aparato de sonido; SalidaSonido.cs)
        }

        // Camino NORMAL para la cancion que suena: float + volumen + tiempos en un solo objeto.
        //
        // Los MP3 van aparte, por el mismo camino que usa el repaso de la biblioteca (AbreMp3). El lector de
        // NAudio tiene ahi una trampa por partida doble: arma el descompresor de MP3 y despues sigue
        // preparandose, y si algo falla en ese hueco -una cabecera rota, y de esas hay unas cuantas en
        // cualquier biblioteca grande- no cierra ni el descompresor ni el archivo. Lo que queda tirado lo
        // acaba cerrando el recolector de basura, Windows contesta que ya no vale, y la queja sale por un
        // hilo donde NADIE la puede recoger: se cierra la rocola entera. Por aqui se abren menos archivos que
        // en el repaso, pero se abren TODOS los que alguien pide en el bar. Es el mismo montaje que hace
        // NAudio por dentro (el lector mas un canal de muestras), asi que no cambia ni una muestra.
        private sealed class FileAudioSource : IAudioSource
        {
            private readonly WaveFormat formato;
            private readonly Mp3FileReader mp3;
            private readonly WaveStream wav;
            private readonly SampleChannel canal;
            private readonly AudioFileReader r;
            private MemoryStream enMemoria;   // la cancion entera, si se abrio desde la memoria (CancionEnMemoria.cs)
            internal bool EnMemoria { get { return enMemoria != null; } }
            public FileAudioSource(string path)
            {
                if (path != null && path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    // LA CANCION ENTERA EN MEMORIA, si cabe (CancionEnMemoria.cs): mientras suena ya no se lee el disco. Se
                    // oye lo mismo: el mismo lector y el mismo descompresor, sobre los mismos bytes.
                    MemoryStream ms = CancionEnMemoria(path);
                    Mp3FileReader m = null;
                    if (ms != null)
                    {
                        try { m = AbreMp3(path, ms); }
                        catch { ms = null; }   // desde la memoria no pudo: por donde siempre, que dira lo que haga falta
                    }
                    if (m == null) m = AbreMp3(path);
                    try { canal = new SampleChannel(m, false); mp3 = m; m = null; enMemoria = ms; }
                    finally { if (m != null) { try { m.Dispose(); } catch { } } }   // el canal fallo: el lector no se queda tirado
                }
                else
                {
                    WaveStream f = WavDirecto.Abre(path);
                    if (f != null)
                    {
                        try { canal = new SampleChannel(f, false); wav = f; f = null; }
                        finally { if (f != null) { try { f.Dispose(); } catch { } } }
                    }
                    else r = new AudioFileReader(path);
                }
                formato = FormatoAudio.DeArchivo((r == null) ? canal.WaveFormat : r.WaveFormat, path);
            }
            private WaveStream Lector { get { return (mp3 != null) ? (WaveStream)mp3 : wav; } }
            public NAudio.Wave.WaveFormat WaveFormat { get { return formato; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            { return (r == null) ? canal.Read(buffer, offset, count) : r.Read(buffer, offset, count); }
            public float Volume { set { if (r == null) canal.Volume = value; else r.Volume = value; } }
            public TimeSpan CurrentTime { get { return TiempoLector(r == null ? Lector : r, false); } }
            public TimeSpan TotalTime { get { return TiempoLector(r == null ? Lector : r, true); } }
            public void Salta(TimeSpan donde) { SaltaCuadro(r == null ? Lector : r, donde); }
            public void Dispose()
            {
                if (mp3 != null) { try { mp3.Dispose(); } catch { } }
                if (wav != null) { try { wav.Dispose(); } catch { } }
                if (r != null) { try { r.Dispose(); } catch { } }
                enMemoria = null;   // que la cancion se pueda recoger aunque alguien se quede con este objeto
            }
        }

        // Camino TOLERANTE: reproduce un MP3 ya reconstruido a una sola frecuencia (stream en memoria). Mp3FileReader
        // decodifica (float via SampleChannel, que ademas da el volumen); el tiempo actual/total sale del Mp3FileReader.
        private sealed class TolerantMp3Source : IAudioSource
        {
            private readonly MemoryStream ms;
            private readonly Mp3FileReader mp3;
            private readonly SampleChannel sample;
            public TolerantMp3Source(MemoryStream cleanMp3)
            {
                ms = cleanMp3;
                // POR EL CAMINO SEGURO, igual que FileAudioSource y LectorMedida. Aqui habia DOS fugas en
                // tres lineas: el lector se abria a pelo -si reventaba a medias dejaba el descompresor del
                // sistema tirado- y, si el que fallaba era el canal de muestras, se quedaba tirado el lector
                // entero. Lo tirado lo acaba cerrando el recolector de basura, Windows contesta que ya no
                // vale, y la queja sale por un hilo donde NADIE la puede recoger: se cierra la rocola.
                Mp3FileReader m = AbreMp3(ms);
                try { sample = new SampleChannel(m, false); mp3 = m; m = null; }
                finally { if (m != null) { try { m.Dispose(); } catch { } } }   // el canal fallo: el lector no se queda tirado
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return sample.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count) { return sample.Read(buffer, offset, count); }
            public float Volume { set { sample.Volume = value; } }
            public TimeSpan CurrentTime { get { return TiempoLector(mp3, false); } }
            public TimeSpan TotalTime { get { return TiempoLector(mp3, true); } }
            public void Salta(TimeSpan donde) { SaltaCuadro(mp3, donde); }
            public void Dispose() { try { mp3.Dispose(); } catch { } try { ms.Dispose(); } catch { } }
        }

        // LAS CANCIONES MONO, COMO ESTEREO DE DOS CANALES IGUALES (24-10-2026; decision del dueno: "lo que sea mejor para la calidad del
        // sonido"). Un archivo mono salia mono a la tarjeta y Windows mandaba lo mismo a las dos bocinas, que es lo que sale ahora; pero
        // el balance del local no se le aplicaba (SalaLocal solo lo pone con dos canales) y entre una cancion mono y una estereo no habia
        // relevo sin pausa (otra disposicion: se cerraba y reabria la salida). Ahora cada muestra sale dos veces, justo encima del lector:
        // el sello, el relleno del MP3 y los silencios van por fuera, como siempre. El nivelado no cambia (se mide por canal) y el grave en
        // mono, el estereo estrecho y los arreglos de la grabacion no hacen nada con los dos canales iguales. Esas canciones cuestan el
        // doble en la cadena. Los de mas de dos canales siguen como vienen.
        private static IAudioSource ComoEstereo(IAudioSource s) { return (s != null && s.WaveFormat.Channels == 1) ? new MonoComoEstereo(s) : s; }
        private sealed class MonoComoEstereo : IAudioSource
        {
            private readonly IAudioSource s;
            private readonly NAudio.Wave.WaveFormat formato;
            private float[] uno = new float[0];
            private float pendiente; private bool hayPendiente;   // la segunda copia de una muestra, si se pidio un numero impar
            internal MonoComoEstereo(IAudioSource s) { this.s = s; formato = FormatoAudio.Flotante(s.WaveFormat.SampleRate, 2, 3); }
            public NAudio.Wave.WaveFormat WaveFormat { get { return formato; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (count <= 0) return 0;
                int hechos = 0;
                if (hayPendiente) { buffer[offset] = pendiente; hayPendiente = false; hechos = 1; }
                int quiero = (count - hechos + 1) / 2;
                if (quiero > 0)
                {
                    if (uno.Length < quiero) uno = new float[quiero];   // crece una vez, hasta lo mas que se pida
                    int n = s.Read(uno, 0, quiero);
                    for (int i = 0; i < n; i++)
                    {
                        float v = uno[i];
                        buffer[offset + hechos++] = v;   // a mano: 'buffer' puede ser la vista flotante de un byte[]
                        if (hechos < count) buffer[offset + hechos++] = v; else { pendiente = v; hayPendiente = true; }
                    }
                }
                return hechos;
            }
            public float Volume { set { s.Volume = value; } }
            public TimeSpan CurrentTime { get { return s.CurrentTime; } }
            public TimeSpan TotalTime { get { return s.TotalTime; } }
            public void Salta(TimeSpan donde) { hayPendiente = false; s.Salta(donde); }
            public void Dispose() { s.Dispose(); }
        }

        // ==========  RECONSTRUCTOR ESPECTRAL DE AGUDOS (el DSEE "serio")  ==========
        // Parte el audio en ventanas (STFT de 2048 puntos, solape 50 %, raiz de Hann en analisis y sintesis),
        // localiza el "acantilado" donde el codec corto los agudos y re-crea la banda que falta copiando hacia
        // arriba, por BLOQUES y con la fase corregida, lo que sobrevivio bajo el corte (como el SBR de HE-AAC),
        // con la envolvente de la curva clasica o la que dice la IA propia (ver Regenerate). Archivos ya
        // completos (corte >= ~19,3 kHz) pasan intactos: sin acantilado no se toca ni un bin.
        private sealed class SpectralTap : NAudio.Wave.ISampleProvider
        {
            private const int Frame = 2048, Hop = Frame / 2;
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, rate;
            private readonly bool[] musicales;
            private readonly bool disposicionSegura;
            private readonly float[] cortesGrupo, gravesGrupo;
            private readonly int[] recreadosGrupo;
            private FuenteOriginal corteOriginal;
            private int retardoOriginal, canalDeteccion, ventanasCorte;
            private float[] originalRe, originalIm, originalReB, originalImB;
            private long inicioOriginal;
            internal long VentanasOriginales;
            internal RedChica Red;   // el modelo de esta cancion (I3, el de su genero); null = el general (NeuralEnv)
            internal void ConectaOriginal(FuenteOriginal original, int retardo)
            {
                // Sin etapa que cambie PCM, su FFT YA es original: no duplicar.
                if (retardo <= 0 || !disposicionSegura) return;
                corteOriginal = original; retardoOriginal = retardo;
                original.PreparaCorte(Frame, retardo);
                originalRe = new float[Frame]; originalIm = new float[Frame];
                if (ch > 1) { originalReB = new float[Frame]; originalImB = new float[Frame]; }
            }
            private readonly float binHz;
            public volatile bool Active;                   // false = pase directo (costo ~cero)
            // ENCENDER Y APAGAR EN VIVO SIN SALTOS (Ctrl+F3, A/B). Antes, al alternar se tiraba el colchon del
            // STFT: al APAGARLO se perdian de golpe los 23-93 ms de musica que ya se habian leido (un salto
            // hacia delante con chasquido) y al ENCENDERLO la banda recreada entraba de golpe. Ahora:
            //  - la banda recreada entra y sale con un fundido de ~100 ms, y nunca en menos de 4 ventanas (con las
            //    de 4096 del nivel HD, 171-186 ms); va por ventana y el solape lo suaviza;
            //  - al apagar, el STFT sigue hasta que el fundido llega a cero; entonces se entrega lo que ya
            //    estaba leido, tal cual, y se sigue en pase directo: no se pierde ni se repite ni una muestra;
            //  - al empezar la cancion NO hay fundido: si empieza encendido, reconstruye desde la primera muestra.
            private bool stft;                             // el STFT esta en marcha (encendido, o apagandose)
            private bool arrancado;                        // ya se leyo algo: la primera lectura no hace fundido
            private bool saliendo; private int saleIdx;    // apagado del todo: entregando lo ya leido, tal cual
            private float regen = 1f, regenObjetivo = 1f;  // cuanto de la banda recreada entra (0..1) y hacia donde va;
                                                           // 1 si nadie lo toca (el analizador de la cosecha no lee)
            private float pasoRegen;                       // cuanto cambia por ventana (~100 ms; al menos 4 ventanas)
            private bool ultimoCero;                       // la ultima ventana no recreo nada: se puede salir sin salto
            // CUADROS PARTIDOS. Si una lectura trae un cuadro a medias, lo que sobra espera al principio de 'inter'
            // (restoN muestras) y se completa con la siguiente; antes se tiraba y los canales quedaban cruzados el
            // resto de la cancion. (Arrancar el STFT a media trama no hace falta evitarlo: trata igual a todos los
            // canales y devuelve cada muestra a su sitio; solo cambiaria cual llama canal 0.)
            private int restoN;
            private bool firstFrame, detectPair;
            private readonly float[][] inBuf, outAcc;      // por canal: ventana de entrada y acumulador de solape
            private int fill;                              // muestras acumuladas (por canal) en inBuf
            private float[] re, im;                        // espectro de trabajo (se INTERCAMBIA con el del otro canal del par)
            private float[] reB, imB;                      // espectro del segundo canal del par (solo si hay 2 o mas)
            private readonly float[] win;                  // ventana raiz-de-Hann
            private readonly int[] bitrev; private readonly float[] twCos, twSin, twSinNeg;   // tablas de la FFT (el seno tambien cambiado de signo: ver Fft)
            private readonly float[] inter;                // buffer intercalado para leer del origen
            private readonly float[] outQ; private int qHead, qLen;   // FIFO de salida (intercalado)
            private float cutHz;                           // acantilado suavizado (0 = aun sin medir)
            private bool srcEnded;
            private long sourceFrames, emittedFrames;      // conserva tambien la fraccion final menor que un Hop
            private readonly float[] iaIn = new float[28], iaOut = new float[12];   // IA propia: entrada/salida de la red (reusables)
            // Limites exactos: solo dependen del corte y de esta FFT, no de las muestras.
            // Conservar la aritmetica original al prepararlos evita cambiar un solo bin.
            private int iaFeatureCut = -1, iaTargetCut = -1, iaTargetTop = -1;
            private readonly int[] iaFeatureA = new int[24], iaFeatureB = new int[24];
            private readonly int[] iaTargetA = new int[12], iaTargetB = new int[12];
            // LA COPIA POR BLOQUES (ver Regenerate)
            private int canalRegen;                        // canal que se esta recreando (lo pone ProcessFrame)
            private bool ventanaImpar;                     // paridad de la ventana: mantiene la fase de la copia
            private int corteRecreado;                     // el corte con el que se recrea, con histeresis (0 = aun ninguno)
            private float[] ganHoy;                        // por bin sobre el corte: la escala de la copia de una octava abajo
            private double[][] numSuave, denSuave;         // por canal y sub-banda: las dos energias de la escala, suavizadas
            private bool[] suaveListo;                     // por canal: ya hay energias previas con las que suavizar
            private readonly int[] bordes = new int[160];  // bordes (bin) de las sub-bandas de la copia
            private int[] marcas;                          // (para armar los bordes)
            private int nBordes, bordesCorte = -1, bordesTop = -1, bordesLargo = -1;
            private float alfaSuave;                       // cuanto se mueven esas energias por ventana (~50 ms)
            private readonly float[] ampSub = new float[160];   // por sub-banda: la escala de esta ventana, en amplitud
            private float[] fuenteSuave;                   // bajo el corte: cuanto suena la fuente por ahi, sin picos ni huecos
            private readonly double[] bandaFuente = new double[12];   // (las 12 bandas de 1/12 de octava de la fuente)
            private readonly int[] bordeFuente = new int[13];
            private readonly float[] medFuente = new float[12], cenFuente = new float[12];   // (su mediana y su centro)
            // EL NIVEL QUE DE VERDAD SONABA. La copia de antes perdia energia al solapar las ventanas: cada ventana
            // salia con otra forma en el tiempo, que la ventana de sintesis recortaba, y dos ventanas vecinas no se
            // sumaban en fase. Sonaba 10*log10(8/3) = 4,26 dB por debajo de la energia que le pedia la envolvente
            // (la copia por bloques sin esta escala salia de +3,8 a +4,2 dB mas fuerte, medido con ruido). La copia
            // por bloques no pierde nada, asi que se escala a lo que sonaba cuando funcionaba. (Ojo: casi siempre
            // funcionaba poco, porque media la costura en un trozo vacio; ver EnvolventeClasica.) Si el brillo se
            // quiere mas bajo o mas alto, es esta constante y nada mas.
            private const float PotenciaDeAntes = 0.375f;  // 3/8, en potencia

            private volatile bool mejoro;                  // ya reconstruyo al menos UNA vez en esta cancion
            // La cancion FUE mejorada en esta reproduccion: en cuanto Regenerate() actua un cuadro, esto
            // queda prendido hasta que la cancion cambie (cada cancion estrena su propio SpectralTap).
            // Es un SEGURO, no un termometro: el letrero MEJORADA no parpadea aunque la medicion del
            // acantilado vaya y venga; una cancion ya completa (que nunca se reconstruye) jamas lo prende.
            public bool Mejorando { get { return Active && mejoro; } }

            public SpectralTap(NAudio.Wave.ISampleProvider s)
            {
                src = s;
                ch = Math.Max(1, s.WaveFormat.Channels);
                musicales = FormatoAudio.Musicales(s.WaveFormat);
                disposicionSegura = ch <= 2 || FormatoAudio.Mascara(s.WaveFormat) != 0;
                cortesGrupo = new float[(ch + 1) / 2]; gravesGrupo = new float[cortesGrupo.Length];
                recreadosGrupo = new int[cortesGrupo.Length];
                rate = Math.Max(8000, s.WaveFormat.SampleRate);
                binHz = (float)rate / Frame;
                inBuf = new float[ch][]; outAcc = new float[ch][];
                for (int c = 0; c < ch; c++) { inBuf[c] = new float[Frame]; outAcc[c] = new float[Frame]; }
                re = new float[Frame]; im = new float[Frame];
                ganHoy = new float[Frame / 2];
                fuenteSuave = new float[Frame / 2];
                if (ch >= 2) { reB = new float[Frame]; imB = new float[Frame]; }   // los canales se procesan de dos en dos (ver ProcessFrame)
                AudioFftPlan plan = GetAudioFftPlan(Frame);
                win = plan.Window; bitrev = plan.Reverse;
                twCos = plan.Cos; twSin = plan.Sin; twSinNeg = plan.SinNeg;
                inter = new float[Hop * ch];
                outQ = new float[Frame * ch * 2];          // FIFO holgado (nunca acumula mas de Hop*ch)
            }

            // Tablas de la FFT (inversion de bits y factores de giro). Las comparten el camino de
            // reproduccion y el de solo analisis, para que ambos calculen EXACTAMENTE el mismo espectro.
            // Las tablas se crean en GetAudioFftPlan; no se modifican al procesar muestras.

            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }

            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                // Sin layout multicanal no adivinar LFE ni enlazar canales.
                if (!disposicionSegura) return src.Read(buffer, offset, count);
                bool act = Active;
                if (!arrancado)
                {
                    arrancado = true;
                    pasoRegen = Math.Min(0.25f, Hop / (0.1f * rate));   // ~100 ms de fundido, y nunca menos de 4 ventanas
                    regen = act ? 1f : 0f;                         // al empezar la cancion, tal cual: sin fundido
                    if (act) EmpiezaStft();
                }
                else if (act && !stft) { regen = 0f; EmpiezaStft(); }   // encendido a media cancion: entra con fundido
                regenObjetivo = act ? 1f : 0f;
                if (!stft) return src.Read(buffer, offset, count);   // pase directo

                int written = 0;
                while (written < count)
                {
                    if (qLen > 0)                                    // hay salida lista: entregarla
                    {
                        // De golpe, no muestra a muestra. Esto corre por CADA muestra que sale del
                        // reconstructor (88.200 por segundo en estereo) y antes llevaba una comprobacion de
                        // vuelta al principio del circular en cada una. Como mucho son dos copias en bloque:
                        // hasta el final del circular y, si daba la vuelta, lo que quede desde el principio.
                        // A MANO, NO CON Array.Copy, Y NO ES CAPRICHO. Lo que llega aqui como float[] es
                        // EL MISMO OBJETO que NAudio le entrega al reproductor como byte[]: su WaveBuffer
                        // solapa las dos vistas en la misma memoria a proposito. Escribir muestra a muestra
                        // funciona -para numeros no se comprueba el tipo-, pero Array.Copy SI lo comprueba,
                        // ve un byte[] donde le dicen float[] y lanza un error que MATA la cancion. Eso era
                        // lo que cortaba la musica al encender el DSEE.
                        int take = Math.Min(qLen, count - written);
                        int hasta = outQ.Length - qHead;
                        int t1 = (take < hasta) ? take : hasta;
                        int dst = offset + written;
                        for (int k = 0; k < t1; k++) buffer[dst + k] = outQ[qHead + k];
                        if (t1 < take)
                        {
                            int dst2 = dst + t1, resto = take - t1;
                            for (int k = 0; k < resto; k++) buffer[dst2 + k] = outQ[k];
                        }
                        qHead += take; if (qHead >= outQ.Length) qHead -= outQ.Length;
                        qLen -= take; written += take;
                        continue;
                    }
                    if (saliendo)
                    {
                        // Lo que ya estaba leido del origen y aun no salio, tal cual (el fundido ya llego a cero, asi
                        // que la reconstruccion de esas muestras seria ellas mismas). Despues, pase directo.
                        int enBuf = fill * ch, total = enBuf + restoN;
                        while (written < count && saleIdx < total)
                        {
                            buffer[offset + written] = saleIdx < enBuf ? inBuf[saleIdx % ch][saleIdx / ch] : inter[saleIdx - enBuf];
                            saleIdx++; written++;
                        }
                        if (saleIdx < total) break;
                        saliendo = false; stft = false; restoN = 0;
                        if (written < count) { int d = src.Read(buffer, offset + written, count - written); if (d > 0) written += d; }
                        break;
                    }
                    if (!srcEnded && regenObjetivo <= 0f && regen <= 0f && ultimoCero)
                    { saliendo = true; saleIdx = 0; continue; }       // apagado y ya sin nada recreado: salir sin salto
                    if (srcEnded)
                    {
                        long quedan = sourceFrames - emittedFrames;
                        if (quedan <= 0) break;
                        // Cada ventana normal emite Hop cuadros. Al final pueden quedar menos; se procesa
                        // otra ventana solapada y solo se publica lo que realmente pertenecia al origen.
                        for (int c = 0; c < ch; c++) Array.Clear(inBuf[c], fill, Frame - fill);
                        fill = Frame;
                        ProcessFrame((int)Math.Min((long)Hop, quedan));
                        continue;
                    }
                    int need = (Frame - fill) * ch - restoN;         // llenar la ventana de analisis
                    if (need > inter.Length - restoN) need = inter.Length - restoN;
                    int got = src.Read(inter, restoN, need);
                    if (got <= 0)
                    {
                        srcEnded = true;                              // un cuadro que quedo a medias al final no se completa
                        continue;
                    }
                    LimpiaMuestras(inter, restoN, got);   // antes de FFT, solapes y suavizados
                    got += restoN;
                    int frames = got / ch;
                    sourceFrames += frames;
                    if (ch == 2)
                    {   // el caso normal, con los dos arrays sacados FUERA del bucle (dentro, cada muestra
                        // volvia a buscar inBuf[c] en la tabla de arrays y a comprobar sus limites)
                        float[] b0 = inBuf[0], b1 = inBuf[1];
                        for (int f = 0, k = 0; f < frames; f++, k += 2) { b0[fill + f] = inter[k]; b1[fill + f] = inter[k + 1]; }
                    }
                    else
                        for (int f = 0; f < frames; f++)
                            for (int c = 0; c < ch; c++) inBuf[c][fill + f] = inter[f * ch + c];
                    restoN = got - frames * ch;                       // lo que sobra de un cuadro partido: al principio
                    for (int k = 0; k < restoN; k++) inter[k] = inter[frames * ch + k];
                    fill += frames;
                    if (fill >= Frame) ProcessFrame(Hop);
                }
                return written;
            }

            // Pone el STFT en marcha desde donde va el origen. Un Hop de ceros ANTES del audio completa el solape
            // inicial: se procesa, pero no se entrega, y la primera muestra real conserva sus dos ventanas y su
            // ataque. No se pierde ni se repite nada: lo que sale sigue justo donde iba el pase directo.
            private void EmpiezaStft()
            {
                inicioOriginal = corteOriginal != null ? corteOriginal.CuadrosOriginales - retardoOriginal : 0;
                ventanasCorte = 0;
                stft = true; saliendo = false; ultimoCero = false; restoN = 0;
                ventanaImpar = false;
                if (suaveListo != null) for (int c = 0; c < suaveListo.Length; c++) suaveListo[c] = false;
                fill = Hop; firstFrame = true; qHead = 0; qLen = 0;
                srcEnded = false; sourceFrames = 0; emittedFrames = 0;
                for (int c = 0; c < ch; c++)
                { Array.Clear(outAcc[c], 0, Frame); Array.Clear(inBuf[c], 0, Frame); }
            }

            // Procesa UNA ventana completa: FFT -> (detector + regeneracion) -> iFFT -> solape-y-suma.
            // Emite Hop muestras por canal al FIFO y desliza entrada/salida media ventana.
            // ---------- COSECHA DE MATERIAL PARA LA IA ----------
            // Para ENSEÑARLE a la red hacen falta pares "esto queda / estos eran los agudos reales". En un MP3
            // dañado los agudos reales YA NO EXISTEN, asi que solo sirven las canciones que aun los conservan:
            // a esas se les corta el agudo A PROPOSITO y se guarda lo que habia arriba. Todo esto usa las MISMAS
            // funciones que la reproduccion (BuildIaFeatures, Hz, la FFT), asi que el material de entrenamiento
            // coincide EXACTAMENTE con lo que la red vera en vivo: si no, lo aprendido no serviria de nada.
            public SpectralTap(int rateHz, int channels)   // solo analisis: no reproduce, no lee de ninguna fuente
            {
                src = null; ch = channels < 1 ? 1 : channels; rate = rateHz < 8000 ? 44100 : rateHz;
                musicales = new bool[ch]; for (int c = 0; c < ch; c++) musicales[c] = true;
                binHz = (float)rate / Frame;
                re = new float[Frame]; im = new float[Frame];
                ganHoy = new float[Frame / 2];
                fuenteSuave = new float[Frame / 2];
                AudioFftPlan plan = GetAudioFftPlan(Frame);
                win = plan.Window; bitrev = plan.Reverse;
                twCos = plan.Cos; twSin = plan.Sin; twSinNeg = plan.SinNeg;
            }
            public int FrameLen { get { return Frame; } }
            internal bool ReiniciaAnalisis(int rateHz)
            {
                // Solo el constructor de medidas. Jamás resetear aqui el STFT de reproduccion.
                int normal = rateHz < 8000 ? 44100 : rateHz;
                if (src != null || inBuf != null || cortesGrupo != null || corteOriginal != null ||
                    ch != 1 || rate != normal || FrameLen != 2048) return false;
                cutHz = cutLoHz = 0f;
                // AnalyzeFrame sobrescribe re/im completos; las caches de bins son pura geometria.
                return true;
            }
            public float CutHz { get {
                if (cortesGrupo == null) return cutHz; // analizador de cosecha, un canal
                float mayor = 0; for (int i = 0; i < cortesGrupo.Length; i++) mayor = Math.Max(mayor, cortesGrupo[i]);
                return mayor;
            } }
            public float CutLoHz { get {
                if (gravesGrupo == null) return cutLoHz;
                float menor = 0; for (int i = 0; i < gravesGrupo.Length; i++)
                    if (gravesGrupo[i] > 0 && (menor == 0 || gravesGrupo[i] < menor)) menor = gravesGrupo[i];
                return menor;
            } }

            // Analiza UN cuadro (Frame muestras de UN canal) y actualiza el acantilado. Igual que en vivo.
            public void AnalyzeFrame(float[] mono, int off)
            {
                for (int i = 0; i < Frame; i++) { re[i] = mono[off + i] * win[i]; im[i] = 0f; }
                Fft(false);
                DetectCliff();
            }
            // Con el cuadro ya analizado, arma un par de entrenamiento simulando un corte en cutHzSim:
            // entrada = lo que la red veria (solo mira POR DEBAJO del corte) y objetivo = la energia REAL que
            // hay ARRIBA, en la misma escala que la red debe predecir (log10 de la banda contra la costura).
            public bool MakePair(float cutHzSim, float[] featOut, float[] tgtOut)
            {
                int ny = Frame / 2;
                int cut = (int)(cutHzSim / binHz);
                if (cut < 64) return false;
                int topBin = Math.Min((int)(Math.Min(20500f, ny * binHz * 0.95f) / binHz), Math.Min(2 * cut - 2, ny - 4));
                if (topBin <= cut + 4) return false;
                if (!BuildIaFeatures(cut)) return false;
                for (int i = 0; i < 28; i++) featOut[i] = iaIn[i];
                float n2 = (float)Frame * (float)Frame;
                int sa = Hz(cut * binHz * 0.92f); if (sa >= cut) sa = cut - 1;
                double seamE = 0; for (int k = sa; k < cut; k++) seamE += re[k] * re[k] + im[k] * im[k];
                seamE = seamE / ((cut - sa) * n2) + 1e-12;
                PreparaIaTargetBands(cut, topBin);
                for (int j = 0; j < 12; j++)
                {
                    int a = iaTargetA[j], b = iaTargetB[j];
                    if (b <= a) return false;
                    double realE = 0; for (int k = a; k < b; k++) realE += re[k] * re[k] + im[k] * im[k];
                    realE = realE / ((b - a) * n2) + 1e-12;
                    float y = (float)Math.Log10(realE / seamE);
                    if (y > 0.1f) y = 0.1f; else if (y < -4f) y = -4f;   // mismos topes que aplica la reproduccion
                    tgtOut[j] = y;
                }
                return true;
            }

            // DOS CANALES POR EL PRECIO DE UNA FFT.
            //
            // Esto era, con diferencia, lo mas caro de toda la cadena de sonido: cuatro FFT de 2048 puntos por
            // ventana en estereo (ida y vuelta, por canal), el 73% del coste del audio... y todo ello en el
            // MISMO hilo que dibuja la pantalla, o sea tiempo robado a los cuadros.
            //
            // El truco es viejo y exacto: la musica es una senal REAL, asi que su espectro es simetrico y media
            // FFT compleja se estaba desperdiciando. Metiendo el canal izquierdo en la parte real y el derecho
            // en la imaginaria, UNA sola FFT trae los dos espectros mezclados, y se desenredan con esa misma
            // simetria (ver SeparaCanales). A la vuelta igual: se juntan los dos espectros ya arreglados en uno
            // solo y una sola FFT inversa devuelve los dos canales, uno en la parte real y otro en la imaginaria.
            //
            // Cuatro FFT pasan a ser DOS. No es una aproximacion ni un atajo: da lo mismo que antes salvo el
            // ultimo pelo de redondeo de los flotantes (~1e-7, unas mil veces por debajo del ultimo bit de 16).
            // Con un numero impar de canales, el que sobra va por el camino de siempre.
            private void ProcessFrame(int emitFrames)
            {
                ventanaImpar = !ventanaImpar;   // la copia por bloques corrige la fase segun la paridad de la ventana
                if (!firstFrame)   // la ventana de arranque no recrea nada: no gasta un paso del fundido
                {
                    if (regen < regenObjetivo) regen = Math.Min(regenObjetivo, regen + pasoRegen);
                    else if (regen > regenObjetivo) regen = Math.Max(regenObjetivo, regen - pasoRegen);
                }
                int cn = 0;
                for (; cn + 1 < ch; cn += 2)
                {
                    float[] x0 = inBuf[cn], x1 = inBuf[cn + 1];
                    for (int i = 0; i < Frame; i++) { float w = win[i]; re[i] = x0[i] * w; im[i] = x1[i] * w; }
                    Fft(false);
                    SeparaCanales();             // re/im = espectro del canal par ; reB/imB = el del impar
                    // La ventana de arranque (media ventana de ceros y media de musica) NO mide el acantilado: el
                    // salto de cero a musica en su centro salpica energia hasta 20 kHz y la daba por "completa";
                    // la reconstruccion no empezaba hasta que esa medida falsa se diluia (cientos de ms) y entonces
                    // aparecia de golpe. Se mide desde la primera ventana de musica entera. Y por lo mismo, tampoco las
                    // del final, rellenadas con ceros (srcEnded): el corte seco de la ultima nota salpica igual y
                    // movia el corte con el que se recreaban los ultimos milisegundos.
                    PreparaCorteGrupo(cn, true);
                    canalRegen = cn; if (musicales[cn]) Regenerate();
                    Intercambia(); canalRegen = cn + 1; if (musicales[cn + 1]) Regenerate(); Intercambia();
                    // se vuelven a juntar: parte real = canal par, parte imaginaria = canal impar
                    for (int i = 0; i < Frame; i++) { float t = re[i] - imB[i]; im[i] = im[i] + reB[i]; re[i] = t; }
                    Fft(true);
                    float[] a0 = outAcc[cn], a1 = outAcc[cn + 1];
                    for (int i = 0; i < Frame; i++) { float w = win[i]; a0[i] += re[i] * w; a1[i] += im[i] * w; }
                }
                for (; cn < ch; cn++)            // canal suelto (mono, o un numero impar de canales)
                {
                    float[] xin = inBuf[cn];
                    for (int i = 0; i < Frame; i++) { re[i] = xin[i] * win[i]; im[i] = 0f; }
                    Fft(false);
                    PreparaCorteGrupo(cn, false);
                    canalRegen = cn; if (musicales[cn]) Regenerate();
                    Fft(true);
                    float[] acc = outAcc[cn];
                    for (int i = 0; i < Frame; i++) acc[i] += re[i] * win[i];
                }
                ventanasCorte++;
                if (firstFrame) { emitFrames = 0; firstFrame = false; } // descarta solo el prefijo artificial
                if (emitFrames < 0) emitFrames = 0; else if (emitFrames > Hop) emitFrames = Hop;
                for (int f = 0; f < emitFrames; f++) // la primera mitad ya recibio sus DOS solapes: lista para salir
                    for (int c = 0; c < ch; c++)
                    { int tail = qHead + qLen; if (tail >= outQ.Length) tail -= outQ.Length; outQ[tail] = outAcc[c][f]; qLen++; }
                emittedFrames += emitFrames;
                for (int c = 0; c < ch; c++)
                {
                    Array.Copy(outAcc[c], Hop, outAcc[c], 0, Frame - Hop); Array.Clear(outAcc[c], Frame - Hop, Hop);
                    Array.Copy(inBuf[c], Hop, inBuf[c], 0, Frame - Hop);
                }
                fill = Frame - Hop;
                ultimoCero = regen <= 0f;
            }

            // Desenreda los dos espectros que vienen dentro de una sola FFT. Se apoya en que el espectro de una
            // senal real es simetrico: la mitad de arriba es el reflejo de la de abajo. Cada pareja de bins
            // (k y su reflejo N-k) se lee ENTERA antes de escribir nada, porque las cuatro salidas se calculan
            // con esos mismos dos numeros y se escriben justo encima.
            private void SeparaCanales()
            {
                int n = Frame, h = n >> 1;
                float[] r0 = re, i0 = im, r1 = reB, i1 = imB;
                r1[0] = i0[0]; i1[0] = 0f; i0[0] = 0f;       // el bin 0 y el del medio son su propio reflejo
                r1[h] = i0[h]; i1[h] = 0f; i0[h] = 0f;
                for (int k = 1; k < h; k++)
                {
                    int m = n - k;
                    float ar = r0[k], ai = i0[k], br = r0[m], bi = i0[m];
                    float pr = (ar + br) * 0.5f, pi = (ai - bi) * 0.5f;   // canal par
                    float qr = (ai + bi) * 0.5f, qi = (br - ar) * 0.5f;   // canal impar
                    r0[k] = pr; i0[k] = pi; r0[m] = pr; i0[m] = -pi;
                    r1[k] = qr; i1[k] = qi; r1[m] = qr; i1[m] = -qi;
                }
            }

            // Pone el espectro del otro canal del par en re/im (y viceversa). Son cuatro asignaciones de
            // referencia: asi Regenerate -y todo lo que cuelga de el- trabaja sobre el canal que toca sin
            // enterarse de nada ni copiar un solo numero.
            private void Intercambia() { float[] tr = re, ti = im; re = reB; im = imB; reB = tr; imB = ti; }

            // Cada pareja conserva su medida, sin que el frente gobierne los traseros
            // ni el LFE decida el corte. Con Declip la FFT extra usa PCM ORIGINAL,
            // alineado al retardo real; una de cada cuatro ventanas limita el costo.
            private void PreparaCorteGrupo(int canal, bool pareja)
            {
                int g = canal / 2;
                cutHz = cortesGrupo[g]; cutLoHz = gravesGrupo[g]; corteRecreado = recreadosGrupo[g];
                canalDeteccion = canal; detectPair = pareja;
                if (!firstFrame && !srcEnded && (musicales[canal] || (pareja && musicales[canal + 1])))
                {
                    if (corteOriginal == null) { DetectCliff(); ActualizaCorte(); }
                    else if ((cutHz <= 0 || (ventanasCorte & 3) == 1) &&
                        corteOriginal.CuadroOriginal(inicioOriginal + sourceFrames, canal, originalRe, originalIm, win))
                    {
                        float[] r = re, i = im, rb = reB, ib = imB;
                        re = originalRe; im = originalIm; reB = originalReB; imB = originalImB;
                        Fft(false); if (pareja) SeparaCanales();
                        DetectCliff(); ActualizaCorte(); VentanasOriginales++;
                        originalRe = re; originalIm = im; originalReB = reB; originalImB = imB;
                        re = r; im = i; reB = rb; imB = ib;
                    }
                }
                cortesGrupo[g] = cutHz; gravesGrupo[g] = cutLoHz; recreadosGrupo[g] = corteRecreado;
                detectPair = false;
            }

            private int Hz(float hz) { int b = (int)(hz / binHz); int ny = Frame / 2; return b < 1 ? 1 : (b > ny - 1 ? ny - 1 : b); }

            // Localiza el "acantilado" del codec: la frecuencia MAS ALTA donde todavia hay energia comparable
            // a la banda media (4-8 kHz). Suavizado entre ventanas para que no baile; en silencio no se mide.
            private double DetectionPower(int k)
            {
                double p = musicales[canalDeteccion] ? re[k] * re[k] + im[k] * im[k] : 0.0;
                if (!detectPair) return p;
                int n = musicales[canalDeteccion] ? 1 : 0;
                if (musicales[canalDeteccion + 1]) { p += reB[k] * reB[k] + imB[k] * imB[k]; n++; }
                return n > 0 ? p / n : 0.0;
            }
            private void DetectCliff()
            {
                int b4 = Hz(4000f), b8 = Hz(8000f);
                if (b8 <= b4 + 4) return;
                double refE = 0; for (int b = b4; b < b8; b++) refE += DetectionPower(b);
                refE /= (b8 - b4);
                if (refE < 1e-10) return;                        // silencio: conservar la medida previa
                // ...O CASI: de 4 a 8 kHz no hay musica, solo lo que la ventana derrama de un grave fuerte (un bajo o un
                // bombo solos, o la pausa entre dos golpes de charles). Ese derrame baja muy despacio hasta 20 kHz, asi
                // que en cada pausa el acantilado "subia" hacia los 20 kHz, el corte con el que se recrea se movia y
                // hasta se dejaba de recrear, por creer la cancion completa. En musica de verdad esta banda queda como
                // mucho a unos 40 dB del cuerpo (100 Hz - 4 kHz) y el derrame de un grave solo, a 52-88 dB (medido): mas
                // de 45 dB por debajo no es musica, y se conserva la medida previa.
                int b1 = Hz(100f); double cuerpo = 0;
                for (int b = b1; b < b4; b++) cuerpo += DetectionPower(b);
                if (refE < cuerpo / (b4 - b1) * 3e-5) return;
                float top = Math.Min(20000f, (Frame / 2) * binHz * 0.94f);
                float found = 0f;
                for (float hz = top - 300f; hz >= 9000f; hz -= 300f)
                {
                    int a = Hz(hz), b = Hz(hz + 300f); if (b <= a) b = a + 1;
                    double e = 0; for (int k = a; k < b; k++) e += DetectionPower(k);
                    e /= (b - a);
                    if (e > refE * 0.002) { found = hz + 300f; break; }   // ~-27 dB de la banda media: aqui sigue VIVO
                }
                if (found <= 0f) found = 9000f;                  // nada vivo arriba de 9k: el corte anda por ahi
                float alfa = corteOriginal == null ? .15f : .478f; // 1-.85^4: misma escala de suavizado temporal
                cutHz = (cutHz <= 0f) ? found : (cutHz * (1-alfa) + found * alfa);
                DetectCliffLow(refE);
            }

            // El acantilado de ABAJO. Muchas grabaciones viejas (y los vinilos pasados a digital) no tienen NADA
            // por debajo de 60-80 Hz: ahi el grave profundo no esta en el archivo, no es que la bocina no lo de.
            // Saber donde empieza a haber grave de verdad sirve para decidir cuanta ayuda necesita cada cancion.
            private float cutLoHz;
            private void DetectCliffLow(double refE)
            {
                float found = 0f;
                for (float hz = 25f; hz <= 140f; hz += 5f)
                {
                    int a = Hz(hz), b = Hz(hz + 5f); if (b <= a) b = a + 1;
                    double e = 0; for (int k = a; k < b; k++) e += DetectionPower(k);
                    e /= (b - a);
                    if (e > refE * 0.02) { found = hz; break; }   // aqui ya hay grave de verdad
                }
                if (found <= 0f) found = 140f;                   // nada vivo por debajo de 140: grabacion muy flaca
                float alfa = corteOriginal == null ? .15f : .478f;
                cutLoHz = (cutLoHz <= 0f) ? found : (cutLoHz * (1-alfa) + found * alfa);
            }



            private bool BuildIaFeatures(int cut)
            {
                float n2 = (float)Frame * (float)Frame;
                float fc = cut * binHz;
                if (fc < 1200f) return false;
                PreparaIaFeatureBands(cut);
                double sum = 0;
                for (int i = 0; i < 24; i++)
                {
                    int a = iaFeatureA[i], b = iaFeatureB[i];
                    double e = 0; for (int k = a; k < b; k++) e += re[k] * re[k] + im[k] * im[k];
                    iaIn[i] = (float)Math.Log10(e / ((b - a) * n2) + 1e-12);
                    sum += iaIn[i];
                }
                float mean = (float)(sum / 24.0);
                for (int i = 0; i < 24; i++) iaIn[i] -= mean;
                iaIn[24] = mean;
                iaIn[25] = fc / 20000f;
                double am = 0, gm = 0;
                for (int i = 16; i < 24; i++) { am += Math.Pow(10.0, iaIn[i] + mean); gm += iaIn[i] + mean; }
                iaIn[26] = (float)(gm / 8.0 - Math.Log10(am / 8.0 + 1e-12));
                iaIn[27] = 0f;
                return true;
            }



            

            private void PreparaIaFeatureBands(int cut)
            {
                if (iaFeatureCut == cut) return;
                float fc = cut * binHz;
                for (int i = 0; i < 24; i++)
                {
                    int a = Hz(200f * (float)Math.Pow(fc / 200f, i / 24.0));
                    int b = Hz(200f * (float)Math.Pow(fc / 200f, (i + 1) / 24.0));
                    if (b <= a) b = a + 1;
                    iaFeatureA[i] = a; iaFeatureB[i] = b;
                }
                iaFeatureCut = cut;
            }
            private void PreparaIaTargetBands(int cut, int topBin)
            {
                if (iaTargetCut == cut && iaTargetTop == topBin) return;
                float fc = cut * binHz, top = topBin * binHz;
                for (int j = 0; j < 12; j++)
                {
                    int a = Hz(fc * (float)Math.Pow(top / fc, j / 12.0));
                    int b = Hz(fc * (float)Math.Pow(top / fc, (j + 1) / 12.0));
                    if (a < cut) a = cut; if (b > topBin) b = topBin;
                    iaTargetA[j] = a; iaTargetB[j] = b;
                }
                iaTargetCut = cut; iaTargetTop = topBin;
            }

            private bool RegenNeural(int cut, int topBin)
            {
                if (!BuildIaFeatures(cut)) return false;
                if (!(Red ?? NeuralEnv).Predict(iaIn, iaOut)) return false;
                for (int t = cut; t <= topBin; t++) ganHoy[t - cut] = 0f;
                float n2 = (float)Frame * (float)Frame;
                int sa = Hz(cut * binHz * 0.92f); if (sa >= cut) sa = cut - 1;
                double seamE = 0; for (int k = sa; k < cut; k++) seamE += re[k] * re[k] + im[k] * im[k];
                seamE = seamE / ((cut - sa) * n2) + 1e-12;
                PreparaIaTargetBands(cut, topBin);
                for (int j = 0; j < 12; j++)
                {
                    int a = iaTargetA[j], b = iaTargetB[j]; if (b <= a) continue;
                    double mapped = 0;                               // energia de la fuente transpuesta en esta sub-banda (sin picos: ver FuenteSuave)
                    for (int t = a; t < b; t++) mapped += fuenteSuave[t >> 1];
                    mapped = mapped / ((b - a) * n2);
                    if (mapped < 1e-14) continue;                    // fuente muda: nada que transponer aqui
                    float yj = iaOut[j]; if (yj > 0.1f) yj = 0.1f; else if (yj < -4f) yj = -4f;   // tope: nunca mas fuerte que la costura (+~1dB)
                    float sc = (float)Math.Sqrt(seamE * Math.Pow(10.0, yj) / mapped) * 0.9f;      // 0.9 = colchon
                    if (sc > 3f) sc = 3f;
                    for (int t = a; t < b; t++) ganHoy[t - cut] = sc;   // la envolvente; la copia la hace CopiaPorBloques
                }
                return true;
            }

            // RE-CREA LA BANDA POR ENCIMA DEL ACANTILADO.
            //
            // CUANTO suena cada parte de la banda nueva (la envolvente) se decide como siempre: la IA propia si hay
            // modelo cargado (rocola_ia.bin) y, si no, la curva clasica (costura empatada y ~-9 dB por octava), las
            // dos sobre lo que daria copiar la octava de abajo. DE DONDE sale el detalle fino, en cambio, ya no es de
            // ahi (23 de septiembre de 2026):
            //
            // Antes cada bin copiaba al de la mitad de su frecuencia (t <- t/2). Dos bins vecinos salian de la MISMA
            // fuente desplazada distinto, y la fase no seguia de una ventana a la siguiente: un tono estable recreado
            // salia con bandas laterales casi tan fuertes como el (medido con un tono de 7 kHz: solo el 54 % de su
            // energia limpia con ventanas de 2048 y el 11 % con las de 4096). Eso se oye como aspereza o tremolo en
            // los agudos recreados de voces, cuerdas y sintetizadores.
            //
            // Ahora se copia por BLOQUES, como el SBR de los codecs AAC: el bloque de la octava bajo el corte sube
            // entero con un desplazamiento CONSTANTE y, como las ventanas avanzan media ventana, un desplazamiento
            // impar cambia de signo en las ventanas impares para que la fase siga de una a otra. Un tono estable sale
            // como UN tono estable. Cada sub-banda (de ~500 Hz, sin cruzar los bordes de las bandas de la IA ni los de
            // los bloques) se escala a la energia que le pide la envolvente, medida sobre cuanto suena la fuente por
            // ahi sin contar picos ni huecos sueltos (ver FuenteSuave), y a la potencia a la que sonaba la copia de
            // antes cuando funcionaba (ver PotenciaDeAntes). Las dos energias de esa PROPORCION son de la misma ventana
            // y se suavizan entre ventanas (~50 ms): quita el temblor de medir con pocos bins sin retrasar un golpe de
            // platillo, que llega en la misma ventana a los dos lados de la proporcion.
            private void Regenerate()
            {
                if (cutHz <= 0f || cutHz >= 19300f) return;      // sin medida aun, o archivo ya completo
                if (regen <= 0f) return;                          // apagandose y ya en cero: la ventana sale tal cual
                int ny = Frame / 2;
                int cut = corteRecreado > 0 ? corteRecreado : (int)(cutHz / binHz);
                if (cut < 64) return;                             // corte absurdamente bajo: no inventar tanto
                mejoro = true;                                    // desde aqui SI se reconstruye: el letrero queda toda la cancion
                int topBin = Math.Min((int)(Math.Min(20500f, ny * binHz * 0.95f) / binHz), Math.Min(2 * cut - 2, ny - 4));
                if (topBin <= cut + 4) return;
                FuenteSuave(cut);                                 // cuanto suena la fuente, sin picos ni huecos: lo usan las dos de abajo
                // La envolvente: la IA propia (aprendida) o, si no hay modelo o no puede, la curva clasica.
                if (!((Red ?? NeuralEnv).Loaded && RegenNeural(cut, topBin)) && !EnvolventeClasica(cut, topBin)) return;
                CopiaPorBloques(cut, topBin);
            }

            // LA CURVA CLASICA, bin a bin: la costura empatada con colchon (que NUNCA suba) y ~-9 dB por octava, sobre la
            // copia de una octava abajo. false = no hay nada que recrear (la fuente esta muda).
            //
            // LA COSTURA se mide justo DEBAJO de donde empieza la caida del filtro del codec (el mismo borde de arriba
            // del bloque que se copia), no en los 6 bins de justo debajo del corte. El corte medido puede quedar hasta
            // 300 Hz por encima de donde acaba la musica y ahi esos 6 bins estan casi vacios: con el corte ya quieto, la
            // cancion entera se quedaba sin agudos recreados (medido con tono y ruido cortados en 12 kHz: 31 dB menos
            // que antes). Antes lo tapaba, a medias, el propio baile del corte: en algunas ventanas caia mas abajo y los
            // agudos aparecian de golpe (en 4096, una de cada diez ventanas sonaba ~25 dB mas fuerte que las demas).
            private bool EnvolventeClasica(int cut, int topBin)
            {
                double mBelow = 0, mSrc = 0;                      // costura: magnitud media bajo la caida del codec vs banda fuente
                int s1 = cut - Math.Max(2, cut / 32);
                for (int k = s1 - 6; k < s1; k++) mBelow += Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                int s0 = cut >> 1;
                for (int k = s0; k < s0 + 6; k++) mSrc += Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                if (mSrc < 1e-9) return false;
                float g = (float)(mBelow / mSrc) * 0.85f;         // empatada con colchon (que la costura NUNCA suba)
                if (g > 1.1f) g = 1.1f;
                if (g <= 1e-6f) return false;
                float step = (float)Math.Pow(10.0, -0.45 / cut);  // ~-9 dB por octava (una octava = cut bins)
                float f = g;
                for (int t = cut; t <= topBin; t++) { ganHoy[t - cut] = f; f *= step; }
                return true;
            }

            // EL CORTE CON EL QUE SE RECREA. El medido baila unos bins de una ventana a otra (en un pasaje callado la
            // energia de arriba baja y parece que corta antes), y cada cambio del corte usado cambia los bloques y su
            // fase. Solo se mueve si el medido se aparta mas de un 2 %.
            private void ActualizaCorte()
            {
                if (cutHz <= 0f) return;
                int medido = (int)(cutHz / binHz);
                if (corteRecreado <= 0 || Math.Abs(medido - corteRecreado) > Math.Max(2, corteRecreado / 50)) corteRecreado = medido;
            }

            // CUANTO SUENA LA FUENTE POR CADA ZONA, SIN PICOS NI HUECOS SUELTOS, en la octava bajo el corte, que es de
            // donde leen la envolvente y los bloques. La proporcion de cada sub-banda compara dos zonas DISTINTAS de la
            // fuente (la de la copia de antes, una octava abajo, y la del bloque), asi que no puede mirar los picos: un
            // tono de 7 kHz, que la copia de antes subia a 14 kHz y la de bloques sube a 13,1, hacia que la escala
            // apagara el tono recreado e inflara hasta el tope el ruido de 14 kHz (con la energia bin a bin) o movia el
            // ruido recreado de alrededor hasta 23 dB (con la energia promediada en media octava). Aqui la octava se
            // parte en 12 bandas de 1/12 de octava, de cada banda se toma su energia media y de cada tres vecinas la
            // del medio (mediana): un tono o un hueco que caiga en una banda no cuenta. Entre los centros de las bandas
            // se interpola. Medido con ese tono (pruebas/dsee.cs, seccion 11): el ruido recreado de alrededor queda a
            // menos de 1 dB del que sale sin el tono. Los bordes van en enteros (2^(i/12) * 1024, redondeado) para que
            // salgan iguales en cualquier procesador.
            private static readonly int[] DozavoDeOctava = { 1024, 1085, 1149, 1218, 1290, 1367, 1448, 1534, 1625, 1722, 1825, 1933, 2048 };
            private void FuenteSuave(int cut)
            {
                // Con cut >= 64 (Regenerate no recrea por debajo) la banda mas estrecha tiene 2 bins: ninguna queda vacia.
                int k0 = cut >> 1;
                int[] e = bordeFuente; double[] m = bandaFuente; float[] v = medFuente, cc = cenFuente;
                for (int i = 0; i < 12; i++) e[i] = (k0 * DozavoDeOctava[i] + 512) >> 10;
                e[12] = cut;
                for (int i = 0; i < 12; i++)
                {
                    double s = 0; for (int k = e[i]; k < e[i + 1]; k++) s += re[k] * re[k] + im[k] * im[k];
                    m[i] = s / (e[i + 1] - e[i]);
                    cc[i] = (e[i] + e[i + 1] - 1) * 0.5f;
                }
                for (int i = 0; i < 12; i++) v[i] = Mediana3(m, i);
                float[] fs = fuenteSuave;
                int j = 0;
                for (int k = k0; k < cut; k++)
                {
                    while (j < 12 && cc[j] <= k) j++;             // j = la primera banda con el centro a la derecha de k
                    fs[k] = j == 0 ? v[0] : (j == 12 ? v[11] : v[j - 1] + (v[j] - v[j - 1]) * ((k - cc[j - 1]) / (cc[j] - cc[j - 1])));
                }
            }
            // La mediana de la banda i y sus dos vecinas (en los extremos, las tres del extremo).
            private static float Mediana3(double[] m, int i)
            {
                int j = i - 1; if (j < 0) j = 0; else if (j > 9) j = 9;
                double a = m[j], b = m[j + 1], c = m[j + 2];
                double med = a > b ? (b > c ? b : (a > c ? c : a)) : (a > c ? a : (b > c ? c : b));
                return (float)med;
            }

            // LA COPIA POR BLOQUES, con la envolvente que dejo ganHoy (ver Regenerate).
            private void CopiaPorBloques(int cut, int topBin)
            {
                int srcLo = cut >> 1;                             // el bloque fuente: desde una octava abajo...
                int srcHi = cut - Math.Max(2, cut / 32);          // ...hasta casi el corte (sin la caida del filtro del codec)
                int largo = srcHi - srcLo;                        // lo que sube cada bloque sobre el anterior
                if (largo < 8) return;
                ArmaSubbandas(cut, topBin, largo);
                int c = canalRegen;
                if (numSuave == null || numSuave.Length < ch) { numSuave = new double[ch][]; denSuave = new double[ch][]; suaveListo = new bool[ch]; }
                double[] ns = numSuave[c], ds = denSuave[c];
                if (ns == null) { ns = numSuave[c] = new double[bordes.Length]; ds = denSuave[c] = new double[bordes.Length]; }
                if (alfaSuave <= 0f) alfaSuave = (float)(1.0 - Math.Exp(-(double)Hop / (0.05 * rate)));
                bool previa = suaveListo[c];
                int nsb = nBordes - 1;
                // 1) La escala de cada sub-banda: la energia que le pide la envolvente sobre la copia de antes, entre la
                //    del bloque (las dos sobre FuenteSuave), al nivel al que sonaba la copia de antes. Se suavizan las DOS
                //    ENERGIAS por separado, no la escala: asi cuentan las ventanas con musica y no las casi mudas, cuya
                //    escala es ruido de medir. Suavizando la escala, una mezcla con charles en corcheas pasada a MP3 de
                //    128 kbps llevaba la escala de los silencios (hasta el tope) al golpe siguiente y sus agudos recreados
                //    salian hasta 40 dB mas fuertes que antes (medido). Y como son energias, el promedio es de POTENCIA,
                //    como el que hacia la copia de antes al sumar ventanas sueltas (suavizar la amplitud quedaba ~1 dB
                //    por debajo cuando la costura baila de una ventana a otra, medido).
                for (int k = 0; k < nsb; k++)
                {
                    int a = bordes[k], b = bordes[k + 1];
                    int d = cut + ((a - cut) / largo) * largo - srcLo;   // desplazamiento de su bloque
                    double eHoy = 0, eBlk = 0;
                    for (int t = a; t < b; t++)
                    {
                        float gh = ganHoy[t - cut];
                        eHoy += fuenteSuave[t >> 1] * gh * gh;
                        eBlk += fuenteSuave[t - d];
                    }
                    if (previa) { ns[k] += (eHoy - ns[k]) * alfaSuave; ds[k] += (eBlk - ds[k]) * alfaSuave; }
                    else { ns[k] = eHoy; ds[k] = eBlk; }
                    float pk = ds[k] > 1e-24 ? (float)(ns[k] / ds[k]) * PotenciaDeAntes : 0f;
                    if (pk > 36f) pk = 36f;                       // (x6 en amplitud) un hueco en la fuente no se infla sin limite
                    ampSub[k] = (float)Math.Sqrt(pk);
                }
                suaveListo[c] = true;
                // 2) La copia, bin a bin: la escala interpolada entre los centros de las sub-bandas de SU bloque, y el
                //    signo que mantiene la fase de una ventana a la siguiente.
                for (int k = 0; k < nsb; k++)
                {
                    int a = bordes[k], b = bordes[k + 1];
                    int r = (a - cut) / largo, d = cut + r * largo - srcLo;
                    float signo = ((d & 1) == 1 && ventanaImpar) ? -regen : regen;   // con el fundido de encendido (1 = tal cual)
                    float centro = (a + b - 1) * 0.5f;
                    bool conAnt = k > 0 && (bordes[k - 1] - cut) / largo == r;        // la sub-banda de antes es del mismo bloque
                    bool conSig = k + 1 < nsb && (bordes[k + 1] - cut) / largo == r;  // y la de despues
                    float cAnt = conAnt ? (bordes[k - 1] + a - 1) * 0.5f : 0f;
                    float cSig = conSig ? (b + bordes[k + 2] - 1) * 0.5f : 0f;
                    for (int t = a; t < b; t++)
                    {
                        float g = ampSub[k];
                        if (t < centro && conAnt) g = ampSub[k - 1] + (ampSub[k] - ampSub[k - 1]) * ((t - cAnt) / (centro - cAnt));
                        else if (t > centro && conSig) g = ampSub[k] + (ampSub[k + 1] - ampSub[k]) * ((t - centro) / (cSig - centro));
                        float fr = g * signo;
                        int s = t - d;                            // fuente: siempre bajo el corte (contenido real)
                        re[t] += re[s] * fr; im[t] += im[s] * fr;
                        int m = Frame - t;                        // espejo (frecuencias negativas): la señal sigue siendo real
                        re[m] = re[t]; im[m] = -im[t];
                    }
                }
            }

            // Las sub-bandas de la copia: bordes en el corte, al empezar cada bloque, en las bandas de la IA (las mismas
            // de RegenNeural y MakePair) y, si quedan anchas, partidas en trozos de ~500 Hz. Se rehacen solo si cambia
            // el corte, y entonces se olvida el suavizado, que era de otras sub-bandas.
            private void ArmaSubbandas(int cut, int topBin, int largo)
            {
                if (cut == bordesCorte && topBin == bordesTop && largo == bordesLargo) return;
                bordesCorte = cut; bordesTop = topBin; bordesLargo = largo;
                if (suaveListo != null) for (int c = 0; c < suaveListo.Length; c++) suaveListo[c] = false;
                if (marcas == null) marcas = new int[64];
                int n = 0, fin = topBin + 1;
                marcas[n++] = cut; marcas[n++] = fin;
                for (int p = cut + largo; p < fin && n < marcas.Length; p += largo) marcas[n++] = p;
                float fc = cut * binHz, top = topBin * binHz;
                for (int j = 1; j < 12 && n < marcas.Length; j++)
                {
                    int e = Hz(fc * (float)Math.Pow(top / fc, j / 12.0));
                    if (e > cut && e < fin) marcas[n++] = e;
                }
                Array.Sort(marcas, 0, n);
                int w = Math.Max(4, (int)(500f / binHz));
                nBordes = 0;
                for (int i = 0; i < n; i++)
                {
                    int e = marcas[i];
                    if (nBordes > 0 && e <= bordes[nBordes - 1]) continue;   // repetido
                    if (nBordes > 0)
                    {
                        int prev = bordes[nBordes - 1], ancho = e - prev, partes = (ancho + w - 1) / w;
                        for (int p = 1; p < partes && nBordes < bordes.Length - 1; p++) bordes[nBordes++] = prev + ancho * p / partes;
                    }
                    if (nBordes < bordes.Length) bordes[nBordes++] = e;
                }
            }

            // FFT radix-2 iterativa en sitio (tablas precalculadas). inverse=true incluye la escala 1/N.
            //
            // Es lo mas caro del reconstructor, y el bucle de mas adentro se recorre unas once mil veces por
            // cada FFT. Dos detalles que ahi dentro cuestan:
            //   - el signo del seno depende de si es ida o vuelta, y se estaba decidiendo EN CADA VUELTA del
            //     bucle. Ahora hay una segunda tabla con el seno ya cambiado de signo y se elige UNA vez.
            //   - los arreglos se leian del objeto en cada acceso. Sacados a variables locales, el traductor
            //     puede dejarlos en registros y ademas se ahorra comprobar sus limites tantas veces.
            private void Fft(bool inverse)
            {
                int n = Frame;
                float[] r = re, m = im;
                int[] br = bitrev;
                for (int i = 0; i < n; i++)
                {
                    int j = br[i];
                    if (j > i) { float t = r[i]; r[i] = r[j]; r[j] = t; t = m[i]; m[i] = m[j]; m[j] = t; }
                }
                float[] tc = twCos, ts = inverse ? twSin : twSinNeg;
                for (int size = 2; size <= n; size <<= 1)
                {
                    int half = size >> 1, tstep = n / size;
                    for (int i = 0; i < n; i += size)
                    {
                        int k = 0;
                        for (int j = i; j < i + half; j++, k += tstep)
                        {
                            float wr = tc[k], wi = ts[k];
                            int b = j + half;
                            float rb = r[b], mb = m[b];
                            float tr = rb * wr - mb * wi;
                            float ti = rb * wi + mb * wr;
                            r[b] = r[j] - tr; m[b] = m[j] - ti;
                            r[j] += tr; m[j] += ti;
                        }
                    }
                }
                if (inverse)
                {
                    float sc = 1f / n;
                    for (int i = 0; i < n; i++) { r[i] *= sc; m[i] *= sc; }
                }
            }
        }

        // Convierte flotante -> 16 bits con DITHER TPDF. El truncado directo (SampleToWaveProvider16) mete
        // distorsion de cuantizacion en pasajes MUY silenciosos (finales de cancion, colas de reverb): el
        // ultimo bit "escalona" la senal y suena a aspereza. El dither triangular de +-1 LSB convierte ese
        // error en un ruido de fondo parejo e inaudible (mucho mas natural, es lo que hace un CD bien
        // masterizado). Redondea en vez de truncar. ~4 operaciones por muestra. (En HD, WASAPI sale en
        // flotante puro y no pasa por aqui; esto ayuda a la salida WaveOut de 16 bits de XP y al respaldo.)
        private sealed class DitherWaveProvider16 : NAudio.Wave.IWaveProvider
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
                    float d = NextUni() - NextUni();
                    float x = fbuf[i] * 32767f + d;
                    int q = (int)Math.Floor(x + 0.5f);                 // redondeo (no truncado)
                    if (q > 32767) q = 32767; else if (q < -32768) q = -32768;
                    buffer[bo++] = (byte)(q & 0xFF);
                    buffer[bo++] = (byte)((q >> 8) & 0xFF);
                }
                return got * 2;
            }
        }

        // DES-RECORTE (declipper): muchos MP3 de la "guerra del volumen" vienen con los picos APLASTADOS
        // de fabrica (el maestro se grabo recortado): se oye aspero en las partes fuertes. Esto detecta los
        // "techos planos" (varias muestras seguidas pegadas al maximo digital) y RECONSTRUYE el pico redondo
        // que deberia haber ahi, continuando la trayectoria de la onda (arco parabolico) hasta aterrizar en
        // la salida del techo. Solo actua en recortes al TOPE digital (~0.985+), el caso tipico; el pico
        // reconstruido puede pasar de 1.0 y eso esta bien: el limitador del final lo baja LIMPIO. Si la senal
        // no viene recortada, pasa BIT-EXACTA (solo con ~1.5 ms de retardo). Por canal, sin tocar el estereo.
        // BAJO PSICOACUSTICO. Las bocinas de un local no bajan de ~80 Hz: el grave profundo de la musica
        // simplemente NO SALE por ellas. Subirlo con EQ no sirve -solo gasta amplificador y hace toser al cono-,
        // asi que en vez de eso se generan los ARMONICOS de esa nota grave (el doble y el triple de su
        // frecuencia), que la bocina SI reproduce, y el oido reconstruye solo la nota que falta. Es un efecto
        // conocido: la "fundamental ausente". Era la mitad que le faltaba a la cadena: habia realce de agudos
        // (DSEE) y nada abajo.
        // El grave se toma de la MEZCLA MONO -en casi toda la musica el bajo va al centro- y los mismos
        // armonicos se suman a los dos canales: el grave sale centrado y sin rarezas de fase.
        // Con Amount = 0 devuelve el audio tal cual (costo cero), asi que se deja SIEMPRE en la cadena y se
        // mueve en vivo desde Ctrl+F3 para comparar A/B sin cambiar de cancion.
        // CAMBIOS EN VIVO SIN ESCALON: antes la mezcla nueva entraba de golpe en el siguiente bloque (un escalon
        // del grave cada vez que se tocaba la perilla) y, al apagarlo, sus filtros se quedaban congelados: al
        // volver a encenderlo arrancaba con el estado de hacia minutos. Ahora la mezcla va en rampa de 20 ms
        // hasta el valor pedido exacto, y una vez usado en la cancion sus filtros siguen la musica aunque este en
        // cero (sin sumar nada: sale intacto). Si la cancion EMPIEZA en cero, ni se calcula: pase directo. Al
        // empezar la cancion la mezcla es la pedida desde la primera muestra (sin fundido que debilite el golpe).
        // El grave de un cuadro se suma al cuadro SIGUIENTE (1 muestra, 23 us: a 320 Hz son 2,6 grados): asi un
        // cuadro que llega partido entre dos lecturas se completa sin tocar lo ya entregado, y da igual como
        // lleguen las lecturas; antes suponia cuadros enteros (una muestra suelta se quedaba sin su grave).
        // DISCO CONTINUO (24-10-2026): la pista siguiente del mismo disco empezaba con los filtros en cero, y en el cambio el grave
        // sintetico daba un escalon (medido en la cadena entera: 0,003 con la musica a 0,3 de pico). Ahora sigue con los de la anterior
        // (Sigue, desde SigueA) y su cebado pasa tal cual: el grave que la anterior dejo pendiente cae en la primera muestra de musica.
        private sealed class DeepBassProvider : NAudio.Wave.ISampleProvider
        {
            private readonly bool[] musicales;
            private readonly int nMusicales, primeroMusical;
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch;
            public volatile float Amount;      // 0..1 (perilla de Ctrl+F3)
            private float lp1, lp2, lp3;       // aislar el grave profundo (3 polos = 18 dB/oct)
            private float env;                 // energia del grave (ataque rapido, caida lenta)
            private float hpS, lpS;            // banda por donde salen los armonicos
            private readonly float aLow, aHp, aLp, aAtk, aRel;
            private readonly int rampa;        // cuadros de la rampa de la mezcla (20 ms)
            private float mix, objetivo, paso; private int quedan;   // mezcla actual, pedida y su rampa
            private bool empezo, vivo;         // empezo: ya leyo; vivo: sus filtros siguen la musica
            private int fase; private float suma, hSal;   // cuadro que va llegando: canal, suma mono y lo que se le suma
            private readonly float[] cuadro;   // para completar un cuadro que llego partido (se reserva al construir)
            private int limpia;                // cuadros desde la ultima limpieza de restos diminutos
            private volatile DeepBassProvider anterior;   // la pista anterior del mismo disco, de la que seguir
            internal int Cebado;               // el cebado de delante (lo pone ArmaCadena): tras heredar pasa tal cual
            private int cebadoPorPasar;
            internal void Sigue(DeepBassProvider previa) { if (previa != null && !ReferenceEquals(previa, this)) anterior = previa; }
            private void Hereda()
            {
                DeepBassProvider a = anterior; anterior = null;
                if (a == null || !a.empezo || a.ch != ch || a.aLow != aLow || a.fase != 0) return;
                lp1 = a.lp1; lp2 = a.lp2; lp3 = a.lp3; env = a.env; hpS = a.hpS; lpS = a.lpS; hSal = a.hSal;
                if (a.vivo) vivo = true;
                cebadoPorPasar = Math.Max(0, Cebado) * ch;
            }
            public DeepBassProvider(NAudio.Wave.ISampleProvider s, float amount)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels); Amount = amount;
                musicales = FormatoAudio.Musicales(s.WaveFormat);
                primeroMusical = -1;
                if (ch <= 2 || FormatoAudio.Mascara(s.WaveFormat) != 0)
                    for (int c = 0; c < ch; c++) if (musicales[c]) { nMusicales++; if (primeroMusical < 0) primeroMusical = c; }
                int fs = s.WaveFormat.SampleRate; if (fs < 8000) fs = 44100;
                aLow = Coef(100f, fs);    // por debajo de aqui esta lo que la bocina no puede dar
                aHp = Coef(95f, fs);      // los armonicos salen de ~95 Hz...
                aLp = Coef(320f, fs);     // ...a ~320 Hz, donde cualquier bocina responde bien
                aAtk = Coef(1f / (2f * (float)Math.PI * 0.004f), fs);   // 4 ms: agarra el golpe del bombo
                aRel = Coef(1f / (2f * (float)Math.PI * 0.150f), fs);   // 150 ms: no bombea
                rampa = Math.Max(1, (int)(fs * .020));
                cuadro = new float[ch];
            }
            private static float Coef(float hz, int fs)
            {
                float a = (float)(1.0 - Math.Exp(-2.0 * Math.PI * hz / fs));
                return a < 0f ? 0f : (a > 1f ? 1f : a);
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0 || nMusicales == 0) return n;   // layout desconocido: no mezclar canales
                float amt = Amount, obj;
                if (float.IsNaN(amt) || float.IsInfinity(amt)) obj = objetivo;   // un valor roto no mueve nada
                else obj = amt > 0.001f ? amt * 0.85f : 0f;
                if (!empezo) { empezo = true; mix = objetivo = obj; Hereda(); }  // al empezar: la pedida, sin rampa (y lo de la anterior del disco)
                else if (obj != objetivo) { objetivo = obj; quedan = rampa; paso = (objetivo - mix) / rampa; }
                int i = offset, end = offset + n;
                if (cebadoPorPasar > 0)
                {   // el cebado de la siguiente del mismo disco: tal cual, y lo pendiente de la anterior espera a la musica
                    int salta = Math.Min(n, cebadoPorPasar); cebadoPorPasar -= salta; i += salta;
                    if (i == end) return n;
                }
                if (!vivo)
                {
                    if (mix == 0f && quedan == 0) return n;   // nunca encendido en esta cancion: pase directo, sin coste
                    vivo = true;
                }
                // Un cuadro que quedo a medias en la lectura anterior (lecturas que parten cuadros; en la cadena
                // normal no pasa): se completa, y su suma pasa por la misma cuenta que los cuadros enteros.
                if (fase != 0)
                {
                    int k = Math.Min(ch - fase, n);
                    for (int c = 0; c < k; c++) { float x = buffer[i + c]; if (musicales[fase + c]) { suma = (float)(suma + x); buffer[i + c] = x + hSal; } }
                    i += k; fase += k;
                    if (fase < ch) return n;
                    for (int c = 0; c < ch; c++) cuadro[c] = 0f; cuadro[primeroMusical] = suma;
                    fase = 0; suma = 0f;
                    Cuadros(cuadro, 0, 1);
                }
                int enteros = (end - i) / ch;
                Cuadros(buffer, i, enteros);
                i += enteros * ch;
                // Lo que sobra de un cuadro partido: recibe el grave del cuadro anterior y espera a la lectura siguiente.
                for (; i < end; i++) { float x = buffer[i]; if (musicales[fase]) { suma = (float)(suma + x); buffer[i] = x + hSal; } fase++; }
                return n;
            }
            // Cuadros enteros: a cada canal se le suma el grave del cuadro ANTERIOR (0 exacto si esta apagado) y con la
            // suma de este se calcula el del siguiente. Los redondeos a float son explicitos para que la cuenta sea la
            // misma se lea como se lea, tambien en el FPU x87 de XP (que si no guarda las variables con mas precision).
            // Por cuadros y no muestra a muestra: en Mono, la misma cuenta muestra a muestra costaba el doble (33 ns
            // por muestra frente a 16, lo mismo que antes de esta etapa).
            private void Cuadros(float[] b, int i, int cuadros)
            {
                float mx = mix, hs = hSal; int q = quedan;
                for (int f = 0; f < cuadros; f++, i += ch)
                {
                    float m = b[i + primeroMusical];
                    for (int c = primeroMusical + 1; c < ch; c++) if (musicales[c]) m = (float)(m + b[i + c]);
                    if (nMusicales > 1) m /= nMusicales;
                    for (int c = 0; c < ch; c++) if (musicales[c]) b[i + c] += hs;
                    // 1) aislar el grave que la bocina NO puede reproducir
                    lp1 += (m - lp1) * aLow;
                    lp2 += (lp1 - lp2) * aLow;
                    lp3 += (lp2 - lp3) * aLow;
                    float low = lp3;
                    // 2) seguir su energia: asi los armonicos respiran con el bajo y no con el resto
                    float a = low < 0f ? -low : low;
                    env += (a - env) * (a > env ? aAtk : aRel);
                    float h = 0f;
                    if (env > 1e-4f)   // en silencio no se inventa nada (no amplificar el ruido de fondo)
                    {
                        // 3) NORMALIZAR antes de generar: asi el timbre del bajo no cambia con el volumen.
                        //    x^2 da el 2o armonico y x^3 el 3o. Hacen falta DOS armonicos SEGUIDOS para que
                        //    el oido reconstruya la nota; solo con el 2o se oiria una octava mas arriba.
                        float nz = low / env;
                        if (nz > 1.5f) nz = 1.5f; else if (nz < -1.5f) nz = -1.5f;
                        float n2 = nz * nz;
                        h = ((n2 - 0.5f) * 0.8f + n2 * nz * 0.6f) * env;
                    }
                    // 4) dejar SOLO la banda que la bocina si da (el paso-alto tambien quita el continuo del x^2)
                    hpS += (h - hpS) * aHp; h -= hpS;
                    lpS += (h - lpS) * aLp;
                    if (q > 0) { mx = (float)(mx + paso); if (--q == 0) mx = objetivo; }
                    hs = (float)(lpS * mx);                   // el mismo grave a todos los canales: centrado
                    if (++limpia == Desnormales.Cada)   // en el silencio, los restos diminutos a cero (Desnormales.cs)
                    {
                        limpia = 0;
                        lp1 = Desnormales.Limpia(lp1); lp2 = Desnormales.Limpia(lp2); lp3 = Desnormales.Limpia(lp3);
                        env = Desnormales.Limpia(env); hpS = Desnormales.Limpia(hpS); lpS = Desnormales.Limpia(lpS);
                    }
                }
                hSal = hs; mix = mx; quedan = q;
            }
        }

        private sealed class DeclipProvider : NAudio.Wave.ISampleProvider
        {
            private const float Thresh = 0.985f;   // umbral del techo digital
            private const int MinRun = 3;          // menos de 3 muestras al tope = pico sano (no se toca)
            private const int MaxRun = 64;         // mas de ~1.5 ms plano = a proposito (sintetizador); no se toca
            private const int D = MaxRun + 4;      // retardo: el techo entero cabe dentro antes de salir
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch;
            private readonly float[] delay;        // circular: D cuadros * canales
            private int dpos;                      // cuadro de escritura actual
            private int primed;                    // cuadros cargados (mientras se llena sale silencio: ~1.5 ms una vez)
            private bool srcEnded, hadInput;
            private int drainSamples;              // muestras que quedan por vaciar del retardo al acabar (D cuadros enteros)
            private int canal;                     // canal de la muestra siguiente dentro del cuadro 'dpos' (0 = cuadro entero)
            private readonly int[] runLen;         // por canal: muestras seguidas pegadas al techo
            private readonly int[] runSign;        // por canal: signo del techo actual (+1/-1)
            private readonly float[] preVal;       // por canal: la muestra justo ANTES del techo
            private readonly float[] preSlope;     // por canal: pendiente de entrada al techo
            private readonly float[] last1, last2; // por canal: las dos muestras anteriores (para la pendiente)
            private readonly bool[] tooLong;       // por canal: el techo se paso de MaxRun (abandonar)
            public DeclipProvider(NAudio.Wave.ISampleProvider s)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                delay = new float[D * ch];
                runLen = new int[ch]; runSign = new int[ch]; preVal = new float[ch]; preSlope = new float[ch];
                last1 = new float[ch]; last2 = new float[ch]; tooLong = new bool[ch];
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int DelayFrames { get { return D; } }
            // Al acabar sale lo que queda en el retardo, muestra a muestra desde donde va (24-10-2026): antes se vaciaba por cuadros
            // enteros, y una lectura de menos de un cuadro devolvia 0 -que quiere decir "se acabo"- con 68 cuadros aun dentro. Son
            // copias: con cuadros enteros sale exactamente lo mismo que antes.
            private int Drain(float[] buffer, int offset, int count)
            {
                int k = Math.Min(drainSamples, count), w = dpos * ch + canal, largo = delay.Length;
                for (int i = 0; i < k; i++) { buffer[offset + i] = delay[w]; delay[w] = 0f; if (++w == largo) w = 0; }
                drainSamples -= k; dpos = w / ch; canal = w % ch;
                return k;
            }
            // S4 ("Escuchar sin mejoras"): con Pasa no se repara nada; sale lo grabado con su mismo retraso. Y lo que se
            // reparo, para que Ctrl+F3 lo ensene (S5).
            internal volatile bool Pasa;
            internal long Reparados;
            private void Repair(int c, float landing)
            {
                int L = runLen[c];
                if (L < MinRun || L > MaxRun || tooLong[c] || Pasa) return;
                Reparados++;
                // arco: p(i) = preVal + preSlope*i + a2*i^2, con p(L+1) = landing (aterriza en la salida del techo)
                float lp1 = L + 1f;
                float a2 = (landing - preVal[c] - preSlope[c] * lp1) / (lp1 * lp1);
                int sgn = runSign[c];
                for (int i = 1; i <= L; i++)
                {
                    int idx = dpos - L + (i - 1); if (idx < 0) idx += D;   // posicion del i-esimo cuadro del techo
                    int q = idx * ch + c;
                    float orig = delay[q];
                    float p = preVal[c] + preSlope[c] * i + a2 * i * i;
                    if (p > 4f) p = 4f; else if (p < -4f) p = -4f;         // tope de cordura del arco
                    // nunca por DEBAJO del techo grabado: en el peor caso queda igual que venia (cero riesgo)
                    if (sgn > 0) { if (p > orig) delay[q] = p; }
                    else { if (p < orig) delay[q] = p; }
                }
            }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (srcEnded) return Drain(buffer, offset, count);
                int n = src.Read(buffer, offset, count);
                if (n <= 0)
                {
                    srcEnded = true;
                    drainSamples = hadInput ? D * ch : 0;   // vacia TODO el retardo; no corta las ultimas muestras
                    return Drain(buffer, offset, count);
                }
                // UN CUADRO PARTIDO (24-10-2026). Si una lectura dejaba un cuadro a medias, sus muestras sueltas salian sin retardo ni
                // reparar y las lecturas siguientes iban con los canales corridos. Entonces se sigue muestra a muestra, sabiendo el
                // canal de cada una (MuestraAMuestra), con las mismas cuentas; con cuadros enteros, el camino de siempre.
                if (canal != 0 || n % ch != 0) { MuestraAMuestra(buffer, offset, n); return n; }
                int frames = n / ch;
                if (frames > 0) hadInput = true;
                for (int f = 0; f < frames; f++)
                {
                    int bi = offset + f * ch, di = dpos * ch;
                    for (int c = 0; c < ch; c++)
                    {
                        float x = buffer[bi + c];
                        float ax = x < 0f ? -x : x;
                        int sgn = x >= 0f ? 1 : -1;
                        if (ax >= Thresh)
                        {
                            if (runLen[c] > 0 && sgn != runSign[c])   // salto de techo + a techo - (onda cuadrada): cerrar y abrir
                            { Repair(c, x); runLen[c] = 0; tooLong[c] = false; }
                            if (runLen[c] == 0)
                            { runSign[c] = sgn; preVal[c] = last1[c]; preSlope[c] = last1[c] - last2[c]; }
                            runLen[c]++;
                            if (runLen[c] > MaxRun) tooLong[c] = true;
                        }
                        else if (runLen[c] > 0)
                        { Repair(c, x); runLen[c] = 0; tooLong[c] = false; }
                        last2[c] = last1[c]; last1[c] = x;
                        float outv = delay[di + c];            // sale la muestra de hace D cuadros (ya reparada si toco)
                        delay[di + c] = x;
                        buffer[bi + c] = (primed < D) ? 0f : outv;
                    }
                    dpos++; if (dpos >= D) dpos = 0;
                    if (primed < D) primed++;
                }
                return n;
            }
            // Lo mismo que el bucle de Read, una muestra cada vez y sabiendo de que canal es: el cuadro 'dpos' avanza al llegar su
            // ultimo canal. Solo cuando una lectura deja un cuadro a medias (con cuadros enteros no se usa).
            private void MuestraAMuestra(float[] buffer, int offset, int n)
            {
                hadInput = true;
                for (int i = 0; i < n; i++)
                {
                    int c = canal, q = dpos * ch + c, bi = offset + i;
                    float x = buffer[bi];
                    float ax = x < 0f ? -x : x;
                    int sgn = x >= 0f ? 1 : -1;
                    if (ax >= Thresh)
                    {
                        if (runLen[c] > 0 && sgn != runSign[c])
                        { Repair(c, x); runLen[c] = 0; tooLong[c] = false; }
                        if (runLen[c] == 0)
                        { runSign[c] = sgn; preVal[c] = last1[c]; preSlope[c] = last1[c] - last2[c]; }
                        runLen[c]++;
                        if (runLen[c] > MaxRun) tooLong[c] = true;
                    }
                    else if (runLen[c] > 0)
                    { Repair(c, x); runLen[c] = 0; tooLong[c] = false; }
                    last2[c] = last1[c]; last1[c] = x;
                    float outv = delay[q];
                    delay[q] = x;
                    buffer[bi] = (primed < D) ? 0f : outv;
                    if (++canal == ch)
                    {
                        canal = 0;
                        dpos++; if (dpos >= D) dpos = 0;
                        if (primed < D) primed++;
                    }
                }
            }
        }

        // LIMITADOR CON PRE-ESCUCHA (lookahead): en vez de recortar el pico en seco (lo que mete distorsion
        // aspera al subir el volumen), mira unos milisegundos HACIA ADELANTE y baja la ganancia SUAVE antes de
        // que llegue el pico, recuperandola despacio. Asi se puede subir mucho el volumen y que suene fuerte pero
        // LIMPIO. Aplica la MISMA ganancia a los dos canales (no mueve la imagen estereo). ~5 operaciones por
        // muestra: barato hasta en la XP. Va al final de la cadena, tras el nivelado/realce.
        //
        // Ya NO va en la cadena: lo reemplaza LimitadorPicoReal (PicoReal.cs), que hace lo mismo mirando la onda
        // reconstruida entre muestras. Se conserva como referencia: las pruebas (limite, picoreal) comparan
        // contra el lo que se gano.
        private sealed class LookaheadLimiter : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, look;
            private readonly float ceiling, atk, rel;
            private readonly float[] delay;
            // Maximo de la ventana [salida actual, entrada actual]. Cola monotona:
            // cada pico entra/sale una vez; no hay barridos de look muestras ni asignaciones en Read.
            private readonly float[] peaks;
            private readonly long[] peakFrames;
            private int peakHead, peakCount;
            private long frameIndex;
            private int dpos, primed;
            private bool srcEnded, hadInput;
            private int drainFrames;
            private float env = 1f;
            public LookaheadLimiter(NAudio.Wave.ISampleProvider s, float ceilingLin, double lookMs)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels); ceiling = ceilingLin;
                int rate = s.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                look = Math.Max(16, (int)(lookMs * 0.001 * rate));
                delay = new float[look * ch];
                peaks = new float[look + 1]; peakFrames = new long[look + 1];
                atk = 1f - (float)Math.Exp(-9.0 / look);
                rel = 1f - (float)Math.Exp(-1.0 / (0.12 * rate));
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int DelayFrames { get { return look; } }
            private void AdvanceGain(float peak)
            {
                long oldest = frameIndex - look;
                while (peakCount > 0 && peakFrames[peakHead] < oldest)
                { peakHead = (peakHead + 1) % peaks.Length; peakCount--; }
                while (peakCount > 0)
                {
                    int last = (peakHead + peakCount - 1) % peaks.Length;
                    if (peaks[last] > peak) break;
                    peakCount--;
                }
                int tail = (peakHead + peakCount) % peaks.Length;
                peaks[tail] = peak; peakFrames[tail] = frameIndex++; peakCount++;
                float max = peaks[peakHead];
                float target = max > ceiling ? ceiling / max : 1f;
                env += (target < env ? atk : rel) * (target - env);
            }
            private float OutputGain(int di)
            {
                float peak = 0f;
                for (int c = 0; c < ch; c++)
                { float a = Math.Abs(delay[di + c]); if (a > peak) peak = a; }
                // La exponencial se acerca al objetivo sin alcanzarlo exactamente. La correccion
                // residual tambien es una GANANCIA ESTEREO, nunca un recorte distinto por canal.
                if (peak > 0f && peak * env > ceiling)
                    env = (ceiling / peak) * 0.9999999f;
                return env;
            }
            private int Drain(float[] buffer, int offset, int count)
            {
                int frames = Math.Min(drainFrames, count / ch);
                for (int f = 0; f < frames; f++)
                {
                    AdvanceGain(0f);
                    int bi = offset + f * ch, di = dpos * ch;
                    float g = OutputGain(di);
                    for (int c = 0; c < ch; c++)
                    { buffer[bi + c] = delay[di + c] * g; delay[di + c] = 0f; }
                    dpos++; if (dpos >= look) dpos = 0;
                }
                drainFrames -= frames;
                return frames * ch;
            }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (count < ch) return 0;   // una lectura vacia no marca fin del origen
                if (srcEnded) return Drain(buffer, offset, count);
                int n = src.Read(buffer, offset, count - count % ch);
                if (n <= 0)
                {
                    srcEnded = true;
                    drainFrames = hadInput ? look : 0;
                    return Drain(buffer, offset, count);
                }
                int frames = n / ch;
                if (frames > 0) hadInput = true;
                for (int f = 0; f < frames; f++)
                {
                    int bi = offset + f * ch, di = dpos * ch;
                    float peak = 0f;
                    for (int c = 0; c < ch; c++)
                    { float a = Math.Abs(buffer[bi + c]); if (a > peak) peak = a; }
                    AdvanceGain(peak);
                    float g = OutputGain(di);
                    for (int c = 0; c < ch; c++)
                    {
                        float outv = delay[di + c] * g;
                        delay[di + c] = buffer[bi + c];
                        buffer[bi + c] = primed < look ? 0f : outv;
                    }
                    dpos++; if (dpos >= look) dpos = 0;
                    if (primed < look) primed++;
                }
                return n;
            }
        }

        // FUNDIDO de SALIDA (fade suave al TERMINAR cada cancion): baja el volumen suave en vez de cortar en
        // seco, para que el paso al siguiente tema no sea un tajo. NO lleva fundido de ENTRADA a proposito:
        // arrancar desde silencio se oia "bajo" al principio en la rocola. NO solapa dos canciones (un crossfade
        // con solape pediria rehacer el motor y arriesgar la fiabilidad del negocio). Va al FINAL de la cadena
        // (tras el limitador); la medicion de volumen no se ve afectada (va antes, en LevelTap). Lineal = sin clics.
        private sealed class FadeProvider : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch;
            private readonly long fadeFrames;    // frames de rampa del fundido de salida
            private long totalFrames;            // frames totales (0 = desconocido o muy corta: sin fundido de salida)
            private long pos;                    // frames ya entregados
            private int canal;                   // canal de la muestra siguiente dentro del cuadro 'pos' (0 = cuadro entero)
            // RELEVO SIN PAUSA a la pista siguiente del MISMO disco: el fundido se quita si aun no empezo (un disco
            // continuo tiene que seguir continuo). 0 = normal, 1 = quitado a tiempo, 2 = ya empezo y se termina.
            private int estadoFundido;
            // FUNDIDO DE ENTRADA (decima tanda, 12-10-2026). La cancion entraba de golpe: si su primera muestra no es
            // cero (un tema cortado de un disco en vivo, una retomada a media cancion) la bocina pasa de cero a donde va la
            // onda en una muestra y se oye un golpe seco. Ahora entra en una curva de 5 ms (inaudible en el ataque: es lo
            // que hace cualquier editor de audio en un corte) o de 0,3 s si se retoma a media cancion. Empieza donde
            // empieza la musica, despues del cebado de la cadena (sus primeros cuadros son ceros). La pista siguiente del
            // mismo disco que sigue sin corte no lleva entrada (SinEntrada): un disco continuo sigue continuo.
            private readonly long entradaDesde, entradaHasta;
            private int sinEntrada;
            internal const double EntradaSeg = 0.005, EntradaRetomadaSeg = 0.3;
            internal void SinEntrada() { System.Threading.Interlocked.Exchange(ref sinEntrada, 1); }
            // true: la cancion acaba entera, quitado ahora o ya antes (si la siguiente se retiro y se vuelve a preparar,
            // SiguienteConElInterruptor). false: el fundido ya empezo y se termina.
            internal bool Quita() { return System.Threading.Interlocked.CompareExchange(ref estadoFundido, 1, 0) != 2; }
            public FadeProvider(NAudio.Wave.ISampleProvider s, long total, double fadeSec) : this(s, total, fadeSec, 0, 0) { }
            public FadeProvider(NAudio.Wave.ISampleProvider s, long total, double fadeSec, long musicaDesde, double entradaSec)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                int rate = s.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                fadeFrames = (long)(fadeSec * rate); if (fadeFrames < 1) fadeFrames = 1;
                totalFrames = total;
                if (totalFrames > 0 && totalFrames < fadeFrames * 4) totalFrames = 0;   // cancion muy corta: sin fundido de salida
                entradaDesde = Math.Max(0L, musicaDesde);
                entradaHasta = entradaSec > 0 ? entradaDesde + Math.Max(1L, (long)(entradaSec * rate)) : entradaDesde;
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                // UN CUADRO PARTIDO (24-10-2026). La cuenta iba por cuadros enteros (n / ch): una lectura que dejaba un cuadro a medias
                // no avanzaba la posicion y las siguientes iban con los canales corridos. Leida de a una muestra, la entrada de 5 ms
                // caia en medio de la cancion (un hueco de hasta -6 dB) y el fundido de salida llegaba tarde. Entonces se sigue
                // muestra a muestra, sabiendo el cuadro y el canal de cada una; con cuadros enteros, el camino de siempre.
                if (canal != 0 || n % ch != 0) { MuestraAMuestra(buffer, offset, n); return n; }
                int frames = n / ch;
                if (pos < entradaHasta && System.Threading.Thread.VolatileRead(ref sinEntrada) == 0) Entrada(buffer, offset, frames);
                // Fuera de los ultimos segundos no hay nada que hacer, y eso es TODA la cancion menos el final.
                // Antes se recorria muestra a muestra igualmente para acabar multiplicando por uno.
                if (totalFrames <= 0 || pos + frames <= totalFrames - fadeFrames) { pos += frames; return n; }
                if (System.Threading.Interlocked.CompareExchange(ref estadoFundido, 2, 0) == 1) { pos += frames; return n; }   // quitado a tiempo
                for (int f = 0; f < frames; f++)
                {
                    long rem = totalFrames - (pos + f);                                 // frames que faltan para el final
                    if (rem >= fadeFrames) continue;
                    float g = (rem <= 0) ? 0f : (float)((double)rem / fadeFrames);      // fundido de SALIDA
                    int bi = offset + f * ch; for (int c = 0; c < ch; c++) buffer[bi + c] *= g;
                }
                pos += frames;
                return n;
            }
            // Lo mismo que Read y Entrada, una muestra cada vez: la de entrada y luego la de salida, cada una del cuadro de su muestra.
            private void MuestraAMuestra(float[] buffer, int offset, int n)
            {
                bool entra = System.Threading.Thread.VolatileRead(ref sinEntrada) == 0;
                double largo = entradaHasta - entradaDesde + 1;
                for (int i = 0; i < n; i++)
                {
                    long p = pos;
                    if (entra && p < entradaDesde) buffer[offset + i] = 0f;   // el cebado: silencio exacto (ver Entrada)
                    else if (entra && p < entradaHasta)
                        buffer[offset + i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * (p - entradaDesde + 1) / largo));
                    if (totalFrames > 0 && totalFrames - p < fadeFrames
                        && System.Threading.Interlocked.CompareExchange(ref estadoFundido, 2, 0) != 1)
                    {
                        long rem = totalFrames - p;
                        buffer[offset + i] *= (rem <= 0) ? 0f : (float)((double)rem / fadeFrames);
                    }
                    if (++canal == ch) { canal = 0; pos++; }
                }
            }
            // Media onda de coseno: arranca sin escalon y llega a uno sin quiebre.
            // EL CEBADO, EN SILENCIO EXACTO (24-10-2026). Antes de la musica solo hay el retardo de la cadena: ceros, salvo los ecos previos
            // de lo que lee por delante (la pre-oscilacion del realce, el eco de la banda que recrea el DSEE y, desde que el grave en mono es
            // de fase lineal, el del grave del lado, hasta 15 ms). En el relevo sin pausa ese cebado se descarta; en la primera cancion despues
            // de un silencio sonaba antes de la musica. Ahora sale en cero.
            private void Entrada(float[] buffer, int offset, int frames)
            {
                double largo = entradaHasta - entradaDesde + 1;
                for (int f = 0; f < frames; f++)
                {
                    long p = pos + f;
                    if (p < entradaDesde) { int bz = offset + f * ch; for (int c = 0; c < ch; c++) buffer[bz + c] = 0f; continue; }   // el cebado de la cadena
                    if (p >= entradaHasta) break;
                    float g = (float)(0.5 - 0.5 * Math.Cos(Math.PI * (p - entradaDesde + 1) / largo));
                    int bi = offset + f * ch; for (int c = 0; c < ch; c++) buffer[bi + c] *= g;
                }
            }
        }

        // ==========  RENDER DEL CUBO (compartido)  ==========
        // TANGENTE HIPERBOLICA aproximada (Pade 3/2), para la saturacion suave del realce de agudos. La de la
        // libreria matematica se llamaba UNA VEZ POR MUESTRA Y CANAL -88,200 veces por segundo- y era de lo mas
        // caro que hacia la cadena de audio. Esta cuesta 5.6 veces menos (medido: del 0.23% de un nucleo al
        // 0.04%) y dibuja la misma curva: se aparta como mucho 0.024, y como el realce entra al 7% en el peor
        // caso, en la salida eso queda 56 dB por debajo de la senal. Inaudible.
        // OJO: la red neuronal del DSEE usa la tangente DE VERDAD y ahi no se toca, porque el modelo se entreno
        // con esa; ademas alli se llama unas 2,600 veces por segundo, o sea nada.
        internal static float TanhRapida(float x)
        {
            if (!(x > -3f)) return -1f;   // atrapa tambien NaN: ninguna comparacion con NaN es cierta
            if (x > 3f) return 1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        // Toma el nivel de audio (RMS) del bloque recien leido SIN modificar el sonido: solo observa el
        // buffer que ya lleno el lector y lo reporta. Sirve para que las estrellas laten con la musica.
        // Como el colchon es de ~1s, el nivel va algo adelantado de lo que se oye (envolvente de energia).
        private partial class LevelTap : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly Form1 owner;
            internal readonly long Publicacion;   // la reproduccion para la que puede apuntar medidas y pulso
            internal MedidaCancion Medida;     // lo que aprende oyendola, si entra por relevo sin pausa (Form1.SinPausa.cs)
            public volatile float Gain = 1f;   // objetivos; la rampa se aplica DESPUES de medir
            public volatile float Master = 1f; // los picos los controla el limitador final
            // REALCE DE AGUDOS (exciter armonico, tipo DSEE): separa la banda alta con un paso-alto de un polo,
            // la satura suave (tanh: crea armonicos NUEVOS por encima, el "aire" que el MP3 recorta) y mezcla
            // una pizca de vuelta. Todo en el dominio del tiempo (nada de FFT). La saturacion se calcula al
            // doble de frecuencia para que sus armonicos no se plieguen (Realce.cs), incluso al activarlo
            // a media cancion; conserva el mismo retraso apagado, con historia barata sin ejecutar FIR.
            // 0 = apagado (la pasada ni se ejecuta si tampoco hay nivelado).
            public volatile float Excite = 0f;   // intensidad 0..1, suavizada al cambiar en vivo
            private float[] exLp;       // estado del filtro del exciter (uno por canal)
            // EQ AUTOMATICO: correccion en dB por banda (8) o null = apagado. Se aplica con biquads
            // PEAKING (RBJ) en cascada, SOLO en las bandas con correccion real (0 dB = identidad exacta,
            // el biquad ni se crea). ~9 operaciones por muestra por banda activa (tipico: 2-4 bandas).
            private sealed class PreparedEq
            {
                internal readonly float[] Curve, State;
                internal readonly float[][] Coefficients;
                internal readonly int Active;
                internal readonly int Mascara;   // las bandas que lleva (bit b = banda b): para heredar su memoria en un disco continuo
                internal PreparedEq(float[] curve, int rate, int channels)
                {
                    Curve = curve;
                    Coefficients = new float[8][];
                    int active = 0;
                    for (int b = 0; b < 8; b++)
                    {
                        float[] cf = Form1.EqPeak(Form1.EqCentros[b], rate, curve[b]);
                        if (cf != null) { Coefficients[active++] = cf; Mascara |= 1 << b; }
                    }
                    Active = active;
                    State = new float[Math.Max(1, channels) * 32];
                }
            }
            // Publicacion atomica: preparar trigonometria y memoria en el llamador (UI),
            // nunca en Read. Solo el lector de audio modifica State despues de publicarla.
            private volatile PreparedEq pendingEq;
            public float[] EqDb
            {
                get { PreparedEq p = pendingEq; return p != null ? p.Curve : null; }
                set
                {
                    PreparedEq p = pendingEq;
                    if (p != null && CurvaEqExacta(p.Curve, value)) return;
                    pendingEq = value == null ? null :
                        new PreparedEq(value, WaveFormat.SampleRate, WaveFormat.Channels);
                }
            }
            private static bool CurvaEqExacta(float[] a, float[] b)
            {
                if (ReferenceEquals(a, b)) return true;
                if (a == null || b == null || a.Length < 8 || b.Length < 8) return false;
                // Sin tolerancia: una curva distinta conserva su transicion normal.
                for (int i = 0; i < 8; i++) if (a[i] != b[i]) return false;
                return true;
            }
            public LevelTap(NAudio.Wave.ISampleProvider s, Form1 o)
            {
                src=s;owner=o;Publicacion=o==null?0:o.publicacionAudio;audioChannels=Math.Max(1,s.WaveFormat.Channels);
                canalesMusicales=FormatoAudio.Musicales(s.WaveFormat);
                rampFrames=Math.Max(1,(int)(s.WaveFormat.SampleRate*.020));
                eqFadeFrames=Math.Max(1,(int)(s.WaveFormat.SampleRate*.030));
                exciterPole=ExciterPole(s.WaveFormat.SampleRate);exLp=new float[audioChannels];
                PreparaRealce(); FijaModoRealce(0f);   // retraso estable, incluso si empieza apagado
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            private void Objetivos(out float g, out float ex)
            {
                g=Gain*Master; ex=Excite;
                if(float.IsNaN(g)||float.IsInfinity(g)||g<0)g=audioReady?gainRamp.Current:1f;
                if(float.IsNaN(ex)||float.IsInfinity(ex))ex=audioReady?exciteRamp.Current:0f;
                ex=ex>.001f?Math.Min(1f,ex):0f;
            }
            public int Read(Span<float> destino) { return Puente.Lee(Read, destino); }   // NAudio 3 (Puente.cs)
            public int Read(float[] buffer, int offset, int count)
            {
                if (vaciando) return VaciaRealce(buffer, offset, count);
                int n = src.Read(buffer, offset, count);
                if (n <= 0 && modoRealce == 2 && audioReady && owner != null)
                {
                    // Se acabo la cancion: el realce 2x tiene retenidos sus 31 cuadros; salen enteros, no se cortan. Antes, lo que la siguiente
                    // del mismo disco necesita para seguir (Realce.cs).
                    GuardaFin();
                    vaciando = true; porVaciar = RealceSobremuestreado.Retraso * audioChannels;
                    return VaciaRealce(buffer, offset, count);
                }
                if (n > 0 && owner != null)
                {
                    // RED DE SEGURIDAD, y no es teorica: en una biblioteca con miles de archivos pobres o
                    // recortados, uno dañado puede decodificar a NaN o a infinito. Eso no seria "un ruido y
                    // ya": se queda PEGADO. Casi todo lo que viene despues arrastra un estado -el piso movil
                    // del detector de golpes, la ganancia del limitador, la sonoridad que se esta midiendo de
                    // la cancion, el promedio del EQ- y en cuanto entra un NaN esas cuentas ya no vuelven a
                    // valer: el limitador se quedaria mudo el resto de la noche. Aqui se corta de raiz, ANTES
                    // de medir nada. Es UNA comparacion por muestra, y de paso atrapa los valores absurdos
                    // (un archivo roto que devuelve 1e30) que reventarian el nivelado.
                    { int fin0 = offset + n; for (int i = offset; i < fin0; i++) { float x0 = buffer[i]; if (!(x0 >= -4f && x0 <= 4f)) buffer[i] = 0f; } }
                    owner.PublicaBloqueDe(this, buffer, offset, n, WaveFormat.SampleRate, WaveFormat.Channels);   // pulso visual; FuenteOriginal ya midio antes del DSP
                    float g, ex;
                    Objetivos(out g, out ex);
                    PreparedEq prepared=pendingEq;
                    if(!audioReady)
                    {
                        gainRamp.Init(g);exciteRamp.Init(ex);currentEq=prepared;audioReady=true;
                        FijaModoRealce(ex);
                        HeredaFin();   // disco continuo: el realce y el EQ siguen con los de la anterior (Realce.cs)
                    }
                    if (cebadoPorPasar > 0)
                    {   // y el cebado de delante pasa tal cual: el realce sigue justo donde acabo la musica de la anterior
                        int salta = Math.Min(n, cebadoPorPasar); cebadoPorPasar -= salta;
                        audioChannel = (audioChannel + salta) % audioChannels;
                        if (salta == n) return n;
                        int total = n; offset += salta; n -= salta;
                        if (modoRealce == 2) { ProcesaConRealce(buffer, offset, n, g, ex, prepared); return total; }
                    }
                    // Realce 2x (la cancion empezo con el realce encendido): siempre por la linea de retraso,
                    // tambien si luego se apaga (sale el seco intacto, retrasado lo mismo).
                    if(modoRealce==2){ProcesaConRealce(buffer,offset,n,g,ex,prepared);return n;}
                    bool identity=g==1f && gainRamp.Current==1f && gainRamp.Remaining==0 &&
                        ex==0 && exciteRamp.Current==0 && exciteRamp.Remaining==0 &&
                        eqFadeLeft==0 && SameEq(currentEq,prepared) && (currentEq==null||currentEq.Active==0);
                    if(identity) audioChannel=(audioChannel+n)%audioChannels;
                    else
                    {
                        int end=offset+n;
                        for(int i=offset;i<end;i++)
                        {
                            if(audioChannel==0)AdvanceAudioControls(g,ex,prepared);
                            float input=buffer[i]*gainRamp.Current;
                            float v=ApplyPreparedEq(currentEq,input,audioChannel);
                            if(eqMix<1f)
                            {
                                float old=ApplyPreparedEq(previousEq,input,audioChannel);
                                v=old+(v-old)*eqMix;
                            }
                            if(exciteRamp.Current>0)   // realce sin sobremuestrear: solo si se encendio a media cancion
                            {
                                float lp=(exLp[audioChannel]+=(v-exLp[audioChannel])*exciterPole);
                                float h=v-lp;
                                float mix=exciteRamp.Current*.5f;
                                v+=Form1.TanhRapida(h*2.5f)*.4f*mix;
                            }
                            buffer[i]=v; // limitador final conserva su lugar y anticipacion
                            if(++audioChannel>=audioChannels)audioChannel=0;
                        }
                    }
                }
                return n;
            }
        }
    }
}

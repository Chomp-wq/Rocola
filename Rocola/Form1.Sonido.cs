// EL SONIDO: la cadena de audio, el nivelado, el EQ y el reconstructor de agudos
//
// Parte de Form1. La clase esta repartida en varios archivos por temas -son todos "partial class Form1"-
// para poder venir a tocar UNA cosa sin leerse el programa entero. Aqui no hay nada nuevo: es el mismo
// codigo que estaba en Form1.cs, movido tal cual.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Rocola
{
    public partial class Form1 : Form
    {
        private SalidaRemuestreada outputResampler;
        private NAudio.CoreAudioApi.MMDevice outputDevice;
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
        private float[] eqNetIn, eqNetOut, eqMeta; // buffers del modelo del EQ (reusados: cero basura por cancion)
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
        private float BassBaseNow() { return bassAuto ? bassAutoBase : bassAmount; }
        private float BassAmountNow() { return Clampf(BassBaseNow() * bassSongFactor, 0f, 1f); }
        // Lo mismo para una cancion que aun no suena (la siguiente, preparada para entrar sin pausa).
        private float BassAmountPara(float factor, float autoBase) { return Clampf((bassAuto ? autoBase : bassAmount) * factor, 0f, 1f); }

        // ============================== MODO NOCHE ==============================
        // De las 11 de la noche a las 7 de la manana la rocola baja 4 dB.
        //
        // POR QUE 4 Y NO "EL 80%". Se pidio "al 80%", pero el oido no funciona en linea recta: multiplicar
        // la senal por 0.8 son -1.9 dB, y el oido apenas distingue 1 dB. En un bar con gente eso no se
        // habria notado NADA y el ajuste habria parecido roto. Con -4 dB la musica se oye a un ~76% de
        // fuerte, que es lo que la gente quiere decir cuando dice "bajalo al 80".
        //
        // Y BAJA POCO A POCO. Veinte segundos de rampa: la musica simplemente ya esta mas baja y nadie
        // sabe cuando paso. De golpe, a mitad de cancion, se oye el escalon y suena a averia.
        //
        // Las 7 no son un numero suelto: son BusinessDayStartHour, la misma hora a la que esta rocola
        // empieza su dia. Cuadra que la noche acabe cuando empieza el dia del negocio.
        private static bool nocheOn = true;                        // "noche=" en config (0 lo apaga del todo)
        private static int nocheDesde = 23;                        // "noche_desde="
        private static int nocheHasta = BusinessDayStartHour;      // "noche_hasta="
        private static float nocheDb = -4f;                        // "noche_db=" (negativo: baja)
        private const int NocheRampaMs = 20000;                    // lo que tarda en bajar, y en subir
        private float nocheFactor = 1f;                            // donde esta la rampa AHORA (1 = de dia)
        private int nocheUltMs;                                    // reloj de pared de la ultima vuelta
        private bool nocheArrancada, nocheAvisada;

        private static float NocheLin() { return (float)Math.Pow(10.0, nocheDb / 20.0); }

        // AQUI ESTA LA TRAMPA DE TODO ESTO, y por eso tiene su propia funcion y su propia prueba: el rango
        // va de las 23 a las 7 PASANDO POR LA MEDIANOCHE. Escrito como sale solo -"hora >= 23 && hora < 7"-
        // es SIEMPRE FALSO: no existe ninguna hora que cumpla las dos a la vez. Y el fallo no da la cara,
        // porque el programa compila, arranca y no truena: el modo noche simplemente no ocurre nunca.
        private static bool EsDeNoche(DateTime t)
        {
            int h = t.Hour, a = nocheDesde, b = nocheHasta;
            if (a == b) return false;              // rango vacio
            if (a < b) return h >= a && h < b;     // rango normal, dentro del mismo dia
            return h >= a || h < b;                // el que cruza la medianoche: va un O, no un Y
        }

        // A donde TIENE que llegar la rampa segun el reloj: 1 de dia, y de noche lo que se haya puesto.
        private static float MetaNoche() { return (nocheOn && EsDeNoche(DateTime.Now)) ? NocheLin() : 1f; }

        // Cuanto hay que bajar AHORA MISMO. Es el valor de la rampa, no el destino: mientras baja, esto va
        // devolviendo los valores de en medio, y por eso se oye bajar en vez de pegar un salto.
        //
        // Antes de que el temporizador haya dado su primera vuelta se contesta el destino directamente, y no
        // el campo. Eso no es un adorno: si un video empezara a sonar en ese hueco, el campo todavia valdria
        // cero y el video saldria MUDO. Lo encontro la bateria de videos, que arma la rocola a mano y por
        // tanto cae siempre en ese hueco.
        private float FactorNoche() { return nocheArrancada ? nocheFactor : MetaNoche(); }

        // Un paso de la rampa. Va por MILISEGUNDOS DE RELOJ, no por veces que la llamen, y eso importa: el
        // medidor de Ctrl+F3 PARA el temporizador de animacion y llama al tick a mano, asi que contar
        // vueltas la volveria loca. A velocidad constante el recorrido entero dura siempre lo mismo, se
        // empiece donde se empiece.
        private static float AvanzaRampa(float actual, float meta, int msPasados)
        {
            if (actual == meta) return meta;
            if (msPasados <= 0) return actual;
            float recorrido = 1f - NocheLin();
            if (recorrido < 0.0001f) return meta;   // no baja nada: no hay rampa que hacer
            float paso = recorrido * msPasados / NocheRampaMs;
            if (actual < meta) { actual += paso; if (actual > meta) actual = meta; }
            else { actual -= paso; if (actual < meta) actual = meta; }
            return actual;
        }

        // La lleva el temporizador de animacion, al lado de CheckBusinessDayReset, que ya es donde vive lo
        // de mirar la hora.
        private void AvanzaModoNoche()
        {
            bool esNoche = nocheOn && EsDeNoche(DateTime.Now);
            float meta = MetaNoche();
            int ahora = Environment.TickCount;
            // Al encender NO se hace la rampa: se arranca ya en el nivel que toca. Una rocola que empieza
            // a todo volumen y baja sola durante veinte segundos parece que se esta estropeando.
            if (!nocheArrancada) { nocheArrancada = true; nocheUltMs = ahora; nocheFactor = meta; nocheAvisada = esNoche; return; }
            int dt = ahora - nocheUltMs; nocheUltMs = ahora;
            if (dt < 0 || dt > 5000) dt = 0;   // desbordamiento del reloj, o el equipo estuvo dormido: no dar un salto
            if (esNoche != nocheAvisada)
            {
                nocheAvisada = esNoche;
                ShowToast(esNoche ? ("Modo noche: el volumen baja " + (-nocheDb).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " dB")
                                  : "Se acabo el modo noche: vuelve el volumen de siempre",
                          Color.FromArgb(38, 78, 132));
            }
            float antes = nocheFactor;
            nocheFactor = AvanzaRampa(nocheFactor, meta, dt);
            // Y se empuja a la cancion que YA esta sonando: si no, la baja no se oiria hasta la siguiente.
            if (nocheFactor != antes) { LevelTap t = curTap; if (t != null) t.Master = MasterLin() * FactorNoche(); AplicaAPreparada(); }
        }
        private float crossfadeSec = 0.5f;   // FUNDIDO de salida entre canciones: segundos de rampa al terminar ("fundido=" en config; 0 = corte seco)
        private bool declipOn = true;        // DES-RECORTE (declipper): repara picos aplastados de origen en archivos mal masterizados ("declip=" en config)
        // TAMANO DE VENTANA DEL RECONSTRUCTOR DE AGUDOS (el DSEE normal). 0 = que lo decida la rocola.
        // El DSEE trabaja por trozos de 2048 o 4096 muestras. Medido en un equipo de 4 nucleos con
        // pruebas/dseeperf.exe: procesar UN segundo de musica cuesta 8.76 ms con ventana de 2048 y
        // 9.42 ms con 4096. Son 0.66 ms mas por segundo -siete centesimas del uno por ciento de un
        // nucleo-, o sea practicamente lo mismo. Y no es casualidad: al doblar la ventana cada FFT
        // cuesta poco mas del doble, pero caben la mitad por segundo, asi que el trabajo total solo
        // sube 12/11 = 9%. La ventana grande afina el doble en frecuencia y deja la costura mas
        // limpia, y sale casi gratis. El equipo del bar cae JUSTO en el borde de la regla automatica
        // (4 nucleos, 3.5 GB), asi que este renglon existe para poder fijarlo alli sin recompilar:
        // dsee_ventana=2048 o dsee_ventana=4096 en rocola_config.txt.
        private int dseeVentana = 0;
        private bool sbrOn = false;          // reconstruccion espectral activa (dsee= en config; Ctrl+F3)
        private bool sbrAllowed = false;     // la maquina cumple los requisitos (se mide al arrancar)
        private bool sbrForce = false;       // dsee_forzar=1 en config: salta la puerta (para pruebas)
        private string sbrReason = "";       // por que NO esta disponible (se muestra en Ctrl+F3)

        private string LoudFile() { return RutaDatos("rocola_volumen.txt"); }

        private string EqFile() { return RutaDatos("rocola_eq.txt"); }

        private readonly object eqSaveLock = new object();
        private readonly GuardadoReciente eqSaveQueue = new GuardadoReciente();
        private long eqSaveVersion;
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
            if (c != null && c.Length >= EqLen && eqAvg != null && c[0] > -25f && c[1] > -25f)
            {
                float rel = (c[0] - eqAvg[0]) - (c[1] - eqAvg[1]);   // dB de grave profundo DE MAS que la cancion tipica
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
                    || rms <= 0f) continue;   // el silencio apuntado (LoudMudo) TIENE que sobrevivir al reinicio
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
                if (dur > 0.01f) durations[rest] = dur;
            }
        }

        // Guarda la tabla de sonoridades. Para no frenar el cambio de cancion en bibliotecas GRANDES (el archivo
        // puede tener miles de lineas), se toma una COPIA rapida en el hilo de la interfaz y el volcado a disco se
        // hace en SEGUNDO PLANO. Un candado asegura un solo escritor a la vez. Si algo truena, se ignora (la medida
        // se vuelve a aprender sola). Un archivo perdido/roto no rompe nada: se reconstruye oyendo las canciones.
        private readonly object loudSaveLock = new object();
        private readonly GuardadoReciente loudSaveQueue = new GuardadoReciente();
        private long loudSaveVersion;
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
            // "Subir el piso": si la cancion suena mas BAJITO que el objetivo, se SUBE (segun la fuerza). Si ya suena
            // igual o mas FUERTE que el objetivo, se deja igual (nunca se baja). El tope de subida es el de SU ETAPA.
            float g = (float)Math.Pow(normTarget / rms, normStrength);
            float cap = (float)Math.Pow(10.0, TierCapDb(rms) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;   // NormMaxBoost sigue siendo el techo absoluto de seguridad
            if (g < 1f) g = 1f; else if (g > cap) g = cap;
            return g;
        }

        // ¿EL RECONSTRUCTOR ESTA CORRIENDO EN LA CANCION QUE SUENA?
        //
        // Son DOS preguntas distintas y antes las contestaba una sola variable, sbrOn, con tres consecuencias
        // malas: encender ULTRA apagaba la preferencia del usuario -y la guardaba apagada, asi que no volvia
        // nunca-, tocar el interruptor a mitad de cancion dejaba el reconstructor mudo hasta la siguiente, y
        // el realce de brillo acababa empujando dos veces lo mismo. Ahora cada una tiene su sitio:
        //
        //   sbrOn         lo que el usuario eligio en la ventana de sonido. No lo pisa nadie.
        //   DseeActivo()  si de verdad va a reconstruir AHORA.
        //
        // Con ULTRA apagado manda el interruptor. Con ULTRA encendido manda el respaldo, que se enciende en
        // las canciones que no suenan por la version de Apollo: sobre ESA no se vuelve a empujar, que ya trae
        // sus agudos puestos.
        private bool DseeActivo() { return sbrAllowed && (iaVivo ? iaSpectralFallback : sbrOn); }
        // Lo mismo para una cancion que aun no suena (la siguiente, preparada para entrar sin pausa): con ULTRA, el
        // respaldo depende de si ELLA viene mejorada, no de la que suena. Con la que suena da lo mismo que DseeActivo.
        private bool DseeActivoPara(bool iaMejorada) { return sbrAllowed && (iaVivo ? !iaMejorada : sbrOn); }
        // LO PROPIO DE ESTA RAMA PARA EL RELEVO SIN PAUSA (Form1.SinPausa.cs):
        private bool DseeDe(CadenaCancion c) { return DseeActivoPara(c.IaMejorada); }
        private float ExcitePara(CadenaCancion c) { return ExciteAmountPara(c.Cancion.Path, DseeDe(c)); }
        private int RetrasoAudible() { return audioVisualDelayMs; }
        // La misma cuenta que PlayTrack: si ya esta la version de Apollo, suena esa (y sale el letrero dorado);
        // si no, suena la original y se le pide al motor para la proxima.
        private bool IaMejoradaPara(string path)
        {
            if (!iaVivo || path == null) return false;
            bool yaBuena;
            string enh = IaEnhancedPath(path, out yaBuena);
            if (enh == null) { IaRequestJob(path); return false; }
            return !yaBuena;
        }
        private void AlEntrarPorRelevo(CadenaCancion b) { iaNowEnhanced = b.IaMejorada; RevisaRespaldoDsee(); }

        // Cierra la medida de la cancion que acaba de sonar: calcula su RMS integrado y lo guarda (si se oyo lo
        // suficiente). La llama DisposePlaybackNow, ya con el audio detenido (sin carrera con el hilo de sonido).
        private void FinalizeLoudMeasure()
        {
            string path = curMeasurePath; curMeasurePath = null;
            double sumSq = curSumSq; long samples = curSamples; curSumSq = 0; curSamples = 0;
            CierraSonoridad(path, sumSq, samples, kwRate, kwCanales);
        }
        // Lo mismo con lo que se guardo de una cancion que acabo por relevo sin pausa (Form1.SinPausa.cs).
        private void CierraSonoridad(string path, double sumSq, long samples, int tasa, int canales)
        {
            if (path == null || tasa <= 0 || canales <= 0 || samples < (long)(NormMinSec * tasa * canales)) return;   // por duracion
            float rms = (float)Math.Sqrt(sumSq / samples);
            if (rms <= 0.0001f) return;
            float prev; bool had = loudCache.TryGetValue(path, out prev);
            float val = had ? (prev * 0.5f + rms * 0.5f) : rms;   // promedia con la medida anterior: se estabiliza si la cancion se salto en distinto punto
            // Si ya estaba medida y el valor casi no cambia, NO reescribir el archivo: en una biblioteca ya
            // "aprendida" el guardado deja de dispararse en cada cancion (menos escrituras a disco, cero churn).
            if (had && Math.Abs(val - prev) < 0.004f) return;
            loudCache[path] = val;
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
            Mp3FileReader.FrameDecompressorBuilder armador = delegate(NAudio.Wave.WaveFormat wf)
            { hecho = Mp3FileReader.CreateAcmFrameDecompressor(wf); return hecho; };
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

        // Lector para MEDIR una cancion (no para que suene). Los MP3 van por el camino de arriba, que no deja
        // descompresores tirados; el resto de formatos, por donde siempre. Es EL MISMO montaje que usa NAudio
        // por dentro (el lector mas un canal de muestras), asi que lo medido no cambia ni en un bit.
        private sealed class LectorMedida : IDisposable
        {
            private readonly Mp3FileReader mp3;
            private readonly SampleChannel canal;
            private readonly AudioFileReader otro;
            public LectorMedida(string ruta)
            {
                if (ruta != null && ruta.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    Mp3FileReader r = AbreMp3(ruta);
                    try { canal = new SampleChannel(r, false); mp3 = r; r = null; }
                    finally { if (r != null) { try { r.Dispose(); } catch { } } }   // el canal fallo: el lector no se queda tirado
                }
                else otro = new AudioFileReader(ruta);
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return (mp3 != null) ? canal.WaveFormat : otro.WaveFormat; } }
            public TimeSpan TotalTime { get { return (mp3 != null) ? mp3.TotalTime : otro.TotalTime; } }
            public TimeSpan CurrentTime { set { if (mp3 != null) mp3.CurrentTime = value; else otro.CurrentTime = value; } }
            public float Volume { set { if (mp3 != null) canal.Volume = value; else otro.Volume = value; } }
            public int Read(float[] b, int o, int c) { return (mp3 != null) ? canal.Read(b, o, c) : otro.Read(b, o, c); }
            public void Dispose()
            {
                if (mp3 != null) { try { mp3.Dispose(); } catch { } }
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
            rms = 0f; durSec = 0f; eqCurve = null;
            integradaLista = false;   // lo que deja para TomaIntegrada: nada, salvo que lea la cancion entera
            if (MeasurementCancelled()) return false;
            // De un VIDEO solo interesa CUANTO SUENA. Su sonido lo saca el reproductor de Windows, no
            // nosotros: no pasa por el ecualizador ni por el DSEE ni por los graves. Medirle la huella tonal
            // o cosechar cuadros para la IA seria trabajo y disco gastados en algo que nunca se usaria.
            bool esVideo = EsVideo(path);
            LectorMedida rd = null;
            try
            {
                rd = new LectorMedida(path);
                rd.Volume = 1.0f;
                int rate = rd.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                int ch = rd.WaveFormat.Channels; if (ch < 1) ch = 1;
                double total = rd.TotalTime.TotalSeconds;
                if (total <= 0.05 || total > 36000) return false;   // vacio o absurdo (archivo roto)
                durSec = (float)total;
                float[] c1 = EqShelfHi(1681.974f, rate, 3.99984f, 0.70717f);   // mismas dos etapas del K-weighting
                float[] c2 = EqHighPass(38.1355f, rate, 0.50033f);             // que usa el nivelado en vivo
                float[] st = new float[ch * 8];
                float[] buf = new float[4096 * ch];
                double sumSq = 0; long nSamp = 0;
                // RECORTE DE ORIGEN: muestras pegadas al techo digital en rachas de 3 o mas. No es distorsion
                // que metamos nosotros: viene asi masterizada. Se cuenta aqui, en el recorrido que ya se hacia.
                long clipN = 0; int[] clipRun = new int[ch];
                float[][] bp = new float[8][]; float[] bpSt = new float[ch * 32];   // huella tonal: estados por canal y banda
                if (!esVideo) for (int b = 0; b < 8; b++) bp[b] = EqBpf(EqCentros[b], rate);   // (de un video no se saca huella: se quedan en nulo y el filtro se salta)
                double[] bpSum = new double[8]; long bpN = 0;
                // COSECHA PARA LA IA: se analiza el canal 0 con el MISMO motor del DSEE. Los pares se guardan
                // en memoria y solo se escriben al final, si resulta que la cancion SI conserva sus agudos
                // (una cancion dañada no puede enseñar nada: los agudos reales ya no estan en el archivo).
                SpectralTap hv = null; float[] hvFrame = null, hvFeat = null, hvTgt = null;
                int hvFill = 0; List<float> hvPairs = null;
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
                    if (pesosInteg != null) { try { integ = new MedidorBs1770(rd.WaveFormat.SampleRate, pesosInteg); } catch { integ = null; } }
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
                    try { hv = new SpectralTap(rate, 1); hvFrame = new float[hv.FrameLen]; soloCorte = true; hvVistosMax = IaWarmFrames + 24; }
                    catch { hv = null; }
                }
                if (cosechar)
                {
                    try
                    {
                        hv = new SpectralTap(rate, 1);
                        hvFrame = new float[hv.FrameLen]; hvFeat = new float[28]; hvTgt = new float[12];
                        hvPairs = new List<float>(4096);
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
                float measuredCut = hv != null ? hv.CutHz : 0f, measuredCutLo = hv != null ? hv.CutLoHz : 0f;
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
                        int want2 = (int)Math.Min(buf.Length, want - got);
                        int n = LeeCuadrosCompletos(lee, buf, want2, ch);   // cuadros enteros: ningun canal se cruza con otro
                        if (n <= 0) break;
                        got += n;
                        if (integ != null) integ.Procesa(buf, 0, n);
                        for (int i = 0; i + ch <= n; i += ch)
                        {
                            if (!esVideo) AccumulateEqFrame(buf, i, ch, bp, bpSt, bpSum);
                            bpN++;
                            // canal 0 hacia el analizador de la IA (el DSEE tambien trabaja por canal)
                            if (hv != null && hvVistos < hvVistosMax)
                            {
                                hvFrame[hvFill++] = buf[i];
                                if (hvFill >= hvFrame.Length)
                                {
                                    hvFill = 0;
                                    double en = 0; for (int q = 0; q < hvFrame.Length; q++) en += hvFrame[q] * hvFrame[q];
                                    if (en / hvFrame.Length > 1e-8)   // saltar cuadros practicamente mudos
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
                                            hv.AnalyzeFrame(hvFrame, 0);
                                            if (hvVistos <= qualityFrames) { measuredCut = hv.CutHz; measuredCutLo = hv.CutLoHz; }
                                            if (!soloCorte && hvVistos > IaWarmFrames &&
                                                (hvStride == 1 || ((hvVistos + hvFase) % hvStride) == 0))
                                                for (int q = 0; q < IaCutsHz.Length; q++)
                                                    if (hv.MakePair(IaCutsHz[q], hvFeat, hvTgt))
                                                    {
                                                        for (int z = 0; z < 28; z++) hvPairs.Add(hvFeat[z]);
                                                        for (int z = 0; z < 12; z++) hvPairs.Add(hvTgt[z]);
                                                    }
                                        }
                                    }
                                }
                            }
                            for (int c = 0; c < ch; c++)
                            {
                                int b = c * 8; float x = buf[i + c];
                                float y1 = c1[0]*x + c1[1]*st[b] + c1[2]*st[b+1] - c1[3]*st[b+2] - c1[4]*st[b+3];
                                st[b+1]=st[b]; st[b]=x; st[b+3]=st[b+2]; st[b+2]=y1;
                                float y2 = c2[0]*y1 + c2[1]*st[b+4] + c2[2]*st[b+5] - c2[3]*st[b+6] - c2[4]*st[b+7];
                                st[b+5]=st[b+4]; st[b+4]=y1; st[b+7]=st[b+6]; st[b+6]=y2;
                                sumSq += y2 * y2; nSamp++;
                                float ax = x < 0f ? -x : x;
                                if (ax >= 0.985f) { clipRun[c]++; if (clipRun[c] == 3) clipN += 3; else if (clipRun[c] > 3) clipN++; }
                                else clipRun[c] = 0;
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
                        int n = LeeCuadrosCompletos(lee, buf, buf.Length, ch);
                        if (n <= 0) break;
                        integ.Procesa(buf, 0, n);
                    }
                    SonoridadIntegrada si;
                    if (!MeasurementCancelled() && CierraIntegrada(integ, total, rd.WaveFormat.SampleRate, out si)) { integradaValor = si; integradaLista = true; }
                }
                if (MeasurementCancelled()) { integradaLista = false; return false; } // no publicar ni cosechar una medida cancelada
                // Solo las canciones COMPLETAS (sin acantilado, el mismo umbral que usa el DSEE para no tocarlas)
                // sirven de maestras: en las dañadas los agudos reales ya no existen y no hay nada que enseñar.
                if (hv != null && !soloCorte && hvPairs.Count >= 40 && hv.CutHz >= EqSanaHz)
                {
                    // Solo se marca hecha cuando el registro completo ya llego al disco. Si falla por espacio,
                    // permisos o desconexion, la proxima pasada vuelve a cosecharla.
                    // Con su ORIGEN (cancion, carpeta, frecuencia, cuadro, canal 0) para que el entrenador pueda
                    // separar por cancion. Ver IaApuntaOrigen.
                    if (IaAppendPairsOrigen(hvPairs, IaOrigenDe(path, total, rate, hv.FrameLen, 0))) MarkIaDone(path, nivelAhora);
                }
                float corteHz = measuredCut;
                float corteLo = measuredCutLo;
                float recorte = (nSamp > 0) ? (float)((double)clipN / nSamp) : 0f;
                if (nSamp < 40000) return durSec > 0.01f;   // muy poco audio util: al menos queda la duracion
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
                    curve[EqCut] = corteHz; curve[EqClip] = recorte; curve[EqCutLo] = corteLo; curve[EqFingerprint] = 1f;
                    for (int b = 0; b < 8; b++) curve[b] = (float)(10.0 * Math.Log10(bpSum[b] / bpN + 1e-12));
                    float[] cuerpo = { curve[1], curve[2], curve[3], curve[4], curve[5] };
                    Array.Sort(cuerpo);
                    float refDb = cuerpo[2];
                    for (int b = 0; b < 8; b++) curve[b] -= refDb;
                    eqCurve = curve;
                }
                return true;
            }
            catch { return durSec > 0.01f; }
            finally { if (rd != null) { try { rd.Dispose(); } catch { } } }
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
            return new RectangleF(albumView.Left + st.Sel.X, albumView.Top + st.Sel.Y, st.Sel.W, st.Sel.H);
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
                piso += (rms - piso) * 0.045f;
                specPiso[b] = piso;
                // GOLPE: cuanto sobresale AHORA por encima de ese piso. Ataque instantaneo, caida rapida.
                float golpe = 0f;
                if (rms > piso * 1.18f + 0.0008f)
                {
                    golpe = (rms - piso) * 9f;
                    if (golpe > 1f) golpe = 1f;
                }
                float gp = specGolpe[b];
                specGolpe[b] = (golpe > gp) ? golpe : gp * 0.80f;   // ~0.25 s de caida a 24 cuadros/s
                float v = cuerpo * 0.55f + specGolpe[b] * cuerpo * 0.45f;   // el golpe solo luce donde ya hay banda
                if (v > 1f) v = 1f;
                float prev = specVis[b];
                specVis[b] = (v > prev) ? v : prev + (v - prev) * 0.22f;   // sube al instante, baja en ~0.2 s
            }
            // Y la PRESENCIA: sube y baja despacio, para que la aurora no aparezca ni desaparezca de golpe.
            // Sin musica NO cae a cero: cae al REPOSO, que ademas respira (ver AurReposo).
            auroraFase += AurReposoPaso;
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
            auroraPres += (meta - auroraPres) * (hayMusica ? 0.075f : 0.014f);
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
            float t = (Environment.TickCount - transTick) / (float)TransMs;   // avance EN VIVO: fluido a los fps del render
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float e = t * t * t * (t * (t * 6f - 15f) + 10f);      // suavizado quintico
            RectangleF a = transFrom, b = transTo;
            g.Has = true; g.Bmp = transImg as Bitmap;
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

        // Estado de la animacion de VENTANA (el buscador) para el panel GPU, imitando a macOS:
        //  - ABRIENDO: entra al 86% de su tamano y un poco mas abajo, se pasa un pelin de su tamano final y
        //    REBOTA hasta asentarse, mientras aparece de golpe (el fundido acaba a media animacion). El rebote
        //    es algo mayor a lo ancho que a lo alto, asi que se "estira" un instante: es lo que le da vida.
        //  - CERRANDO: primero toma IMPULSO (crece un pelin) y luego se va encogiendo, hundiendose y acelerando.
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
                float u = t - 1f;
                float e = 1f + 4.2f * u * u * u + 3.2f * u * u;      // frenado con REBOTE: se pasa ~1.3x y vuelve
                float ey = 1f + 3.2f * u * u * u + 2.2f * u * u;     // vertical: rebota menos -> se estira un instante
                g.Scale = 0.86f + 0.14f * e;
                g.ScaleY = 0.86f + 0.14f * ey;
                g.Dy = (1f - e) * 22f;                              // sube un poco al abrirse (y se pasa un pelin)
                g.Alpha = t * 2.4f; if (g.Alpha > 1f) g.Alpha = 1f; // ya visible en el primer tercio
                g.Bmp = null; g.HideUi = true;                      // se anima la PROPIA textura de UI (ya es el buscador)
            }
            else
            {
                float e = t * t;                                    // acelera (ease-in): se va rapido
                float imp = 0.34f * t * (1f - t) * (1f - t);         // IMPULSO: crece ~3% al arrancar y se desvanece
                g.Scale = 1f - 0.16f * e + imp;
                g.ScaleY = 1f - 0.16f * e + imp * 0.6f;             // estira un poco a lo ancho al tomar impulso
                g.Dy = e * 14f;                                     // se hunde un poco al cerrarse
                g.Alpha = 1f - e;
                g.Bmp = winSnap; if (g.Bmp == null) return g;       // la textura de UI ya es la de navegacion
                g.HideUi = false;                                   // esa UI (la rejilla) SI se dibuja: la ventana se va sobre ella
            }
            g.Has = true;
            return g;
        }
#endif

        // ==========  LIMPIEZA: LOS VIDEOS QUE NO SUENAN  ==========
        //
        // En una biblioteca que lleva anos armandose acaban colandose videos con la pista de sonido rota,
        // muda o grabada tan bajo que no se oye nada. En la rocola son una loteria mala: el cliente paga, sale
        // el video y no suena. Aqui se localizan -de los que YA estan medidos; a uno sin medir no se le toca-
        // y se pueden mandar a la papelera.
        //
        // El corte son 30 dB por debajo del volumen objetivo de las canciones. No es "un poco bajo": es que
        // con el resto de la rocola a volumen normal, eso es un susurro que no se oye desde la barra.
        private const float LoudMudo = 0.00001f;   // marca de "medido y en silencio" (se guarda y se relee tal cual)
        // Fallo al arrancar WASAPI (sincrono o COM antes del primer bloque). Se reabre desde cero:
        // no se reutilizan lectores/taps que el intento fallido pudo haber consumido.
        private void ReintentaWaveOut(Track t, Exception ex)
        {
            LogError("WASAPI no arranco; reintento unico por WaveOut", ex);
            curMeasurePath = null; eqMeasPath = null;
            double desde = inicioSeg;   // si se estaba retomando una cancion (SalidaSonido.cs), se retoma en el mismo sitio
            DisposePlaybackNow();
            reintentandoWaveOut = true;
            reanudarEn = desde;
            try { PlayTrack(t); }
            finally { reintentandoWaveOut = false; }
        }
        private ISampleProvider PreparaSalidaWasapi(ISampleProvider samples)
        {
            // Se usa el MISMO endpoint al consultar y abrir. No dejamos que WasapiOut
            // inserte su DMO despues del limitador: puede crear picos y perder la cola.
            using (NAudio.CoreAudioApi.MMDeviceEnumerator devices = new NAudio.CoreAudioApi.MMDeviceEnumerator())
                outputDevice = devices.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Console);
            WaveFormat mix = outputDevice.AudioClient.MixFormat;
            WaveFormat requested = WaveFormat.CreateIeeeFloatWaveFormat(mix.SampleRate, samples.WaveFormat.Channels);
            if (!outputDevice.AudioClient.IsFormatSupported(NAudio.CoreAudioApi.AudioClientShareMode.Shared, requested)
                && samples.WaveFormat.Channels == 1 && mix.Channels == 2)
            {
                // Duplicacion exacta, sin alterar volumen ni dejar un remuestreo oculto.
                MonoToStereoSampleProvider stereo = new MonoToStereoSampleProvider(samples);
                stereo.LeftVolume = 1; stereo.RightVolume = 1; samples = stereo;
                requested = WaveFormat.CreateIeeeFloatWaveFormat(mix.SampleRate, 2);
            }
            if (!outputDevice.AudioClient.IsFormatSupported(NAudio.CoreAudioApi.AudioClientShareMode.Shared, requested))
                throw new NotSupportedException("La salida no admite el formato flotante sin conversion posterior");
            if (samples.WaveFormat.SampleRate != mix.SampleRate)
            {
                outputResampler = new SalidaRemuestreada(samples, mix.SampleRate);
                // Remuestrear reconstruye la onda y deja en muestras lo que antes caia ENTRE ellas: el ultimo
                // limitador va DESPUES, y mira el pico real (ver PicoReal.cs).
                samples = new LimitadorPicoReal(outputResampler, 0.98f, 3.0);
            }
            return samples;
        }
        private void IniciaSalidaWasapi(NAudio.Wave.IWavePlayer wa, NAudio.Wave.ISampleProvider samples)
        {
            output = wa;
            try
            {
                wa.PlaybackStopped += Output_PlaybackStopped;
                wa.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider(samples));
                audioVisualDelayMs = 240;
                wa.Play();
            }
            catch
            {
                wa.PlaybackStopped -= Output_PlaybackStopped;
                output = null;       // callbacks ya encolados dejan de pertenecer a la salida actual
                try { wa.Dispose(); } catch { }
                throw;
            }
        }

        private void PlayTrack(Track t)
        {
            try
            {
                string iaSrc = t.Path;   // IA EN VIVO: si ya hay version mejorada en cache, suena ESA; si no, la original (y se pide para la proxima)
                iaNowEnhanced = false;
                if (iaVivo && t.Path != null)
                {
                    bool yaBuena;
                    string enh = IaEnhancedPath(t.Path, out yaBuena);
                    if (enh != null) iaSrc = enh; else IaRequestJob(t.Path);   // aun no lista: suena la original y se pide para la proxima
                    iaNowEnhanced = (enh != null && !yaBuena);   // [IA] solo si de verdad suena la version mejorada (con .orig suena el original tal cual)
                }
                // Y con ULTRA encendido, todo lo que NO suene por la version de Apollo lo atiende el DSEE
                // normal mientras tanto. Las marcadas .orig incluidas: ahi suena el original, y si le falta
                // banda el reconstructor la recrea; si no le falta, es un pase directo que no cuesta nada.
                RevisaRespaldoDsee();
                // Si la version de la CACHE no abre (la limpieza la borro justo en este instante, o quedo
                // un encode raro), NO se salta la cancion ni se anota el ORIGINAL como incompatible ni se
                // devuelve el credito: el archivo malo se borra (el motor lo regenera) y suena el original.
                bool forceWaveOut = reintentandoWaveOut; // conservar el reintento al volver al bucle de mensajes
                BeginAudioOpen(iaSrc, delegate(IAudioSource source, Exception error)
                {
                    if (error != null && iaSrc != t.Path)
                    {
                        try
                        {
                            LogError("cache IA no abre (suena el original): " + Path.GetFileName(iaSrc), error);
                            try { File.Delete(iaSrc); } catch { }
                            IaRequestJob(t.Path);
                            iaNowEnhanced = false; RevisaRespaldoDsee();
                            BeginAudioOpen(t.Path, delegate(IAudioSource original, Exception originalError) {
                                StartOpenedTrack(t, original, originalError, forceWaveOut);
                            });
                        }
                        catch (Exception fallbackError) { AudioStartFailed(t, fallbackError); }
                    }
                    else StartOpenedTrack(t, source, error, forceWaveOut);
                });
            }
            catch (Exception error) { AudioStartFailed(t, error); }
        }
        private void StartOpenedTrack(Track t, IAudioSource source, Exception error, bool forceWaveOut)
        {
            try
            {
                reader = source;
                if (error != null) throw error;
                reader.Volume = FixedVolume;   // se queda en 1.0 (crudo): el nivelado se aplica en LevelTap, tras medir el volumen REAL
                // RETOMADA: volvio el aparato de sonido y la cancion sigue desde donde iba (SalidaSonido.cs).
                double desde = TomaReanudar();
                if (desde > 0) reader.Salta(TimeSpan.FromSeconds(desde));
                // LA CADENA DE ESTA CANCION (ArmaCadena, mas abajo): la misma que arma el relevo sin pausa para la
                // siguiente, asi que las dos suenan exactamente igual.
                CadenaCancion cad = ArmaCadena(t, reader, iaNowEnhanced, desde);
                cad.Item = nowPlaying;
                // Lo que se aprende oyendola va en los campos de siempre (la salida aun no arranco: sin carrera).
                curMeasurePath = cad.MedidaRuta; curSumSq = 0; curSamples = 0;
                eqMeasPath = cad.EqRuta;
                eqMeasCut = 0f;   // el acantilado es de ESTA cancion: se vuelve a medir
                eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0;
                kwRate = 0;   // reinicia el estado del K-weighting: cada cancion mide desde cero (sin cola del filtro anterior)
                tapMedido = cad.Tap;
                curSbr = cad.Sbr; curTap = cad.Tap; curBass = cad.Bajo;   // para ajustarla en vivo (Ctrl+F3)
                bassSongFactor = cad.BajoFactor; bassAutoBase = cad.BajoBase;
                cadenaSonando = cad; cadenaPreparada = null;
                // RELEVO SIN PAUSA (SinPausa.cs): la salida lee de la secuencia. Cuando esta cancion se acabe, si la
                // siguiente ya esta preparada, sigue en la MISMA lectura sin cerrar la salida.
                secuencia = new Encadenado(cad.Salida, cad);
                NAudio.Wave.ISampleProvider outChain = secuencia;
                outChain = CallaSiEsMedida(outChain);   // la medida de rendimiento: callada aqui, sin tocar volumenes de Windows (SalidaSonido.cs)
                // ===== SALIDA DE AUDIO (rama HD) =====
                // WASAPI compartido con muestras FLOTANTES: float de punta a punta hasta el motor de audio
                // moderno (sin bajar a 16 bits ni pasar por el mezclador clasico) y latencia menor. Si
                // La inicializacion o el arranque sincrono fallan: reabre la pista por WaveOut clasico
                // de siempre. salida= en config: auto (WASAPI si la maquina es HD) | wasapi | waveout.
                bool wantWasapi = !forceWaveOut && (outPref == "wasapi" || (outPref != "waveout" && tierHd));
                bool isWasapi = false;
                output = null;
                if (wantWasapi && Environment.OSVersion.Version.Major >= 6)
                {
                    NAudio.Wave.WasapiOut wa = null;
                    try
                    {
                        outChain = PreparaSalidaWasapi(outChain);
                        wa = new NAudio.Wave.WasapiOut(outputDevice, NAudio.CoreAudioApi.AudioClientShareMode.Shared, false, 300);
                        IniciaSalidaWasapi(wa, outChain);   // Init Y Play protegidos; mantiene salida float
                        isWasapi = true;
                        formatoSalida = "WASAPI float32";
                    }
                    catch (Exception wex)
                    {
                        ReintentaWaveOut(t, wex);
                        return;
                    }
                }
                if (output == null)
                {
                    // Camino CLASICO (WaveOut, 1 segundo de colchon en bloques de 50 ms): coma flotante en Vista o
                    // posterior, 16 bits con dither TPDF si no, o si el sistema la rechaza (SalidaSonido.cs).
                    try { AbreSalidaClasica(outChain, FlotanteAdmitido()); }
                    catch (Exception sex)
                    {
                        // EL APARATO, NO LA CANCION: se espera y se retoma la misma (SalidaSonido.cs).
                        if (EsFalloDelDispositivo(sex) && SalidaCaida(t, desde, sex, false)) return;
                        throw;
                    }
                }
                audioVisualDelayMs = isWasapi ? 240 : 850;   // el pulso visual se alinea al colchon REAL de la salida en uso
                if (!isWasapi)   // WASAPI ya arranco dentro de su bloque protegido
                {
                    try { output.Play(); }
                    catch (Exception pex) { if (EsFalloDelDispositivo(pex) && SalidaCaida(t, desde, pex, false)) return; throw; }
                }
                SalidaArranco(isWasapi && outputDevice != null ? outputDevice.ID : IdSalidaPorDefecto());   // el aparato esta: en cual (SalidaSonido.cs)
                progressTimer.Start();
                UpdateNowPlayingUI(); // lector ya listo; refrescar estado tras la apertura asincrona
                PrecalientaSiguiente();   // la que viene detras, leida en segundo plano (ver arriba)
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
            CadenaCancion c = new CadenaCancion();
            c.Cancion = t; c.Lector = source; c.IaMejorada = iaMejorada;
            bool dsee = DseeActivoPara(iaMejorada);   // el reconstructor para ESTA cancion (con ULTRA, segun si viene mejorada)
            // NIVELADO: si esta cancion ya se oyo antes, se aplica la ganancia que la lleva al nivel comun; si es
            // nueva, va a su volumen original y se APRENDE su sonoridad en esta pasada (para nivelarla la proxima vez).
            // De una cancion retomada a medias no se aprende nada: seria la medida de un trozo.
            bool entera = saltadoSeg <= 0;
            c.MedidaRuta = (entera && normalizeOn && !durCache.ContainsKey(t.Path)) ? t.Path : null;   // ya pre-analizada = medida COMPLETA y fiable: no se vuelve a medir (y se ahorra el K-weighting)
            c.EqRuta = (entera && EqMideHuella && !HasStereoEq(t.Path)) ? t.Path : null;   // actualiza huellas antiguas sin borrar la biblioteca
            // NIVEL 2 (reconstructor espectral): solo se INSERTA en maquinas que cumplen requisitos; con
            // Active=false es un pase directo (costo ~cero). Ctrl+F3 lo alterna EN VIVO para comparar A/B.
            NAudio.Wave.ISampleProvider chain = source;
            int audioTailDelay = 0;   // estos retardos se vacian: el fundido debe abarcar tambien su cola
            // DES-RECORTE: repara picos aplastados de origen (antes de medir y de DSEE). Ya sabemos, cancion
            // por cancion, si de verdad viene recortada; en las que NO -que son la mayoria- ni se pone en la
            // cadena: se ahorra su trabajo y su retardo de ~1.5 ms. Si aun no esta medida, se pone por si acaso.
            if (declipOn && DeclipVale(t.Path))
            {
                DeclipProvider dc = new DeclipProvider(chain); chain = dc; audioTailDelay += dc.DelayFrames;
            }
            if (sbrAllowed) { c.Sbr = new SpectralTap(chain, VentanaDsee()); c.Sbr.Active = dsee; chain = c.Sbr; }   // HD: ventana fina; respaldo espectral si ULTRA aun no tiene la version IA
            LevelTap tap = new LevelTap(chain, this);
            tap.Gain = NormGainFor(t.Path);
            tap.Excite = ExciteAmountPara(t.Path, dsee);   // realce de agudos: mas a las dañadas, menos a las que ya traen su aire
            tap.Master = MasterLin() * FactorNoche();  // SUBIR TODO (con rodilla suave anti-distorsion), y de madrugada mas bajo
            tap.EqDb = EqCorrFor(t.Path);      // EQ automatico: correccion aprendida (null la 1a vez que suena)
            audioTailDelay += tap.DelayFrames;   // realce 2x: retiene 31 cuadros y los entrega al final (Realce.cs)
            c.Tap = tap; tap.Medida = new MedidaCancion(c.MedidaRuta, c.EqRuta);   // lo que aprende oyendola (ver MedidaDe)
            // BAJO PSICOACUSTICO: despues de nivelar/ecualizar y ANTES del limitador, que es quien controla
            // los picos que anaden los armonicos.
            c.BajoFactor = BassFactorFor(t.Path);   // cuanto le toca a ESTA cancion (1 = la perilla tal cual)
            c.BajoBase = BajoAuto(t.Path);          // y de cuanto se parte, si el bajo va en automatico
            c.Bajo = new DeepBassProvider(tap, BassAmountPara(c.BajoFactor, c.BajoBase));
            // LIMITADOR DE PICO REAL: el techo de 0,98 vale para la onda que reconstruye el DAC, no solo para
            // las muestras (ver PicoReal.cs). Detras solo quedan el fundido (que solo baja) y, en la salida
            // clasica, el paso a 16 bits con su dither de un bit.
            LimitadorPicoReal lim = new LimitadorPicoReal(c.Bajo, 0.98f, 3.0);
            audioTailDelay += lim.DelayFrames;
            NAudio.Wave.ISampleProvider outChain = lim;   // limitador con pre-escucha (picos limpios)
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
                    tf = (yaSe > 0.01f) ? (long)(yaSe * hz) : (long)(source.TotalTime.TotalSeconds * hz);
                    tf -= (long)(saltadoSeg * hz);   // retomada: el fundido cuenta desde donde se retomo
                }
                catch { }
                if (tf > 0) tf += audioTailDelay;
                c.Fundido = new FadeProvider(outChain, tf, crossfadeSec); outChain = c.Fundido;   // fundido suave de entrada/salida
            }
            c.Retardo = audioTailDelay; c.Salida = outChain;
            return c;
        }

        // Abre el audio. Camino NORMAL: FileAudioSource (rapido, para el 99.99% de los archivos). Si NAudio lo
        // rechaza (tipico: MP3 que CAMBIA de frecuencia a mitad -> "Got a frame at sample rate ..."), reintenta con
        // un lector TOLERANTE que reconstruye el MP3 dejando solo los frames de la frecuencia dominante. Si tampoco
        // asi, relanza el error original (el llamador lo anota y salta). El reintento NUNCA afecta a los archivos
        // sanos y, en el peor caso, hace lo mismo que antes: saltar.
        private IAudioSource OpenAudio(string path)
        {
            try { return new FileAudioSource(path); }
            catch
            {
                bool isMp3 = path != null && path.Length > 4 && path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
                if (isMp3)
                {
                    MemoryStream clean = null;
                    try
                    {
                        clean = BuildCleanMp3(path);
                        if (clean != null) return new TolerantMp3Source(clean);   // reconstruido a una sola frecuencia: ahora SI suena
                    }
                    catch { if (clean != null) { try { clean.Dispose(); } catch { } } }   // el tolerante tampoco pudo: cae al throw de abajo
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
            Dictionary<int, int> rateCount = new Dictionary<int, int>();
            using (FileStream fs = File.OpenRead(path))               // pasada 1: cuenta frecuencias -> elige la DOMINANTE
            {
                while (true)
                {
                    Mp3Frame f; try { f = Mp3Frame.LoadFromStream(fs); } catch { break; }
                    if (f == null) break;
                    int c; rateCount.TryGetValue(f.SampleRate, out c); rateCount[f.SampleRate] = c + 1;
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
                    if (f.SampleRate == domRate && f.RawData != null) ms.Write(f.RawData, 0, f.RawData.Length);
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
                File.AppendAllText(RutaDatos("rocola_incompatibles.txt"),
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
            // WASAPI (rama HD) avisa desde SU hilo de reproduccion; todo lo de abajo toca UI -> saltar al hilo de la interfaz
            if (InvokeRequired) { try { BeginInvoke((MethodInvoker)delegate { Output_PlaybackStopped(sender, e); }); } catch { } return; }
            // Stop/Dispose de la salida anterior puede dejar un callback ya encolado. Si entretanto arranco
            // otra cancion, ese aviso viejo no debe saltarla ni devolverle otro credito al cliente.
            if (!object.ReferenceEquals(sender, output)) return;
            Exception deLaSalida = e.Exception;   // lo que aviso la salida misma (no un fallo de lectura de la cancion)
            // RELEVO SIN PAUSA: la siguiente que ya habia entrado y aun no se daba por sonando, y un fallo de lectura
            // que atrapo la secuencia (se atiende aqui, UNA vez, como si lo hubiera avisado la salida). Ver RecogeAlParar.
            if (secuencia != null) e = RecogeAlParar(e);
            if (e.Exception != null)
            {
                // El hilo de WasapiOut puede fallar DESPUES de Play(), antes del
                // primer bloque. No perder la pista ni devolver un credito de mas.
                if (output is WasapiOut && outputResampler != null && !outputResampler.HasOutput &&
                    (e.Exception is InvalidCastException || e.Exception is System.Runtime.InteropServices.COMException) &&
                    nowPlaying != null && nowPlaying.Song != null && !nowPlaying.IsVideo)
                {
                    ReintentaWaveOut(nowPlaying.Song, e.Exception);
                    return;
                }
                // EL APARATO, NO LA CANCION (SalidaSonido.cs): se espera y se retoma la misma desde donde iba.
                if (deLaSalida != null && EsFalloDelDispositivo(deLaSalida) && nowPlaying != null && !nowPlaying.IsVideo
                    && SalidaCaida(nowPlaying.Song, PosicionActual(), deLaSalida, true)) return;
                lblStatus.Text = "Aviso al reproducir: " + e.Exception.Message;
                LogError("PlaybackStopped", e.Exception);
                RefundCredit();   // la cancion que el cliente pago fallo al reproducirse: se le devuelve el credito
                ShowToast("No se pudo reproducir: credito devuelto", Color.FromArgb(200, 80, 70));
                playFailStreak++;
                if (playFailStreak >= 3)
                {
                    // Algo de fondo esta mal (NAudio o formato). Dejamos de saltar para no ciclar.
                    DisposePlaybackNow();
                    nowPlaying = null;
                    RefrescaCola();   // idem: sin cancion no hay marca de "SONANDO"
                    lblStatus.Text = "No se pudo reproducir. Detalle en rocola_error.txt (junto al programa)";
                    UpdateNowPlayingUI();
                    return;
                }
            }
            else playFailStreak = 0;   // termino bien una cancion -> reinicia el contador
            PlayNext();
        }

        private void DisposePlaybackNow()
        {
            CancelAudioOpen(); // saltar/cerrar invalida resultados que aun esten abriendo
            CancelaReintentoSalida();   // y la espera a que vuelva el aparato de sonido (SalidaSonido.cs)
            progressTimer.Stop();
            if (output != null)
            {
                output.PlaybackStopped -= Output_PlaybackStopped;
                try { output.Stop(); } catch { }
                try { output.Dispose(); } catch { }
                output = null;
            }
            if (outputResampler != null) { try { outputResampler.Dispose(); } catch { } outputResampler = null; }
            if (outputDevice != null) { try { outputDevice.Dispose(); } catch { } outputDevice = null; }
            if (reader != null) { try { reader.Dispose(); } catch { } reader = null; }
            SueltaSinPausa();   // la siguiente preparada (o ya entrando): con la salida ya parada
            curTap = null; curSbr = null; curBass = null;   // ya no hay taps actuales
            FinalizeLoudMeasure();   // guarda la sonoridad medida de la cancion que acaba de sonar (audio ya detenido: sin carrera)
            FinalizeEqMeasure();     // y su huella tonal (mismo momento seguro)
            ClearLevels();   // sin cancion no hay trozos pendientes: el pulso cae a cero
            rmsLo = rmsMid = rmsHi = 0f; avgLo = avgMid = avgHi = 0f;
        }

        // La llama el hilo de audio con el bloque recien leido: lo parte en sub-trozos de ~50ms, calcula el
        // RMS de cada uno y lo encola con la MARCA DE TIEMPO en que ese trozo SONARA (ahora + colchon).
        // El temporizador de UI los consume cuando les llega su momento -> el pulso va CON lo que se oye.
        // Buffers reusables del hilo de audio (4 trozos x 3 bandas). ThreadStatic: sin asignar por bloque.
        [ThreadStatic] private static float[] tmpLo; [ThreadStatic] private static float[] tmpMid; [ThreadStatic] private static float[] tmpHi;
        [ThreadStatic] private static float[] tmpEsp;   // 4 trozos x 8 bandas
        [ThreadStatic] private static double[] tmpSum;  // sumas por banda: reusado (nada de asignar en el hilo de audio)
        public void ReportAudioBlock(float[] buffer, int offset, int count, int rate, int ch)
        {
            if (count <= 0) return;
            int samplesPerSec = Math.Max(1, rate * ch);
            int now = Environment.TickCount;
            int chunks = 4; int len = count / chunks;
            if (len < 256) { chunks = 1; len = count; }   // bloque chico: un solo trozo
            float[] tLo = tmpLo ?? (tmpLo = new float[4]);
            float[] tMid = tmpMid ?? (tmpMid = new float[4]);
            float[] tHi = tmpHi ?? (tmpHi = new float[4]);
            bool espectro = EspectroActivo;
            float[] tEsp = espectro ? (tmpEsp ?? (tmpEsp = new float[4 * SpecBands])) : null;
            // ESCALERA del espectro: los coeficientes se calculan al ritmo de CUADROS (rate), porque la escalera
            // trabaja sobre la mezcla mono de cada cuadro y no muestra por muestra del stream entrelazado. Asi los
            // cortes caen en los mismos Hz de siempre pero con la MITAD de trabajo, y ademas la diferencia entre
            // canales deja de colarse como falso "aire". Se recalculan solo si cambia el formato.
            if (espectro && (specCoef == null || specRate != rate))
            {
                specRate = rate;
                specCoef = new float[SpecCortes.Length];
                for (int k = 0; k < SpecCortes.Length; k++)
                {
                    double a = 1.0 - Math.Exp(-2.0 * Math.PI * SpecCortes[k] / Math.Max(1, rate));
                    specCoef[k] = (float)(a < 0.0 ? 0.0 : (a > 1.0 ? 1.0 : a));
                }
                specLp = new float[SpecCortes.Length * SpecPolos];
            }
            float[] sc = specCoef, sl = specLp;
            double[] sSum = espectro ? (tmpSum ?? (tmpSum = new double[SpecBands])) : null;
            int chEsp = ch < 1 ? 1 : ch;
            float invCh = 1f / chEsp;
            float fSum = 0f; int fCnt = 0;   // acumulador del cuadro en curso (sigue vivo entre trozos del bloque)
            // Dos filtros paso-bajo de una via separan el audio en 3 bandas: grave = lp1, medio = lp2-lp1, agudo = x-lp2.
            // Los coeficientes marcan cortes aproximados (~150 Hz y ~2 kHz sobre el stream interleaved); es un
            // visualizador, no un ecualizador: basta que "se sienta" la separacion. El estado persiste entre bloques.
            const float A1 = 0.012f, A2 = 0.13f;
            float lp1 = fLp1, lp2 = fLp2;
            for (int c = 0; c < chunks; c++)              // el RMS se calcula FUERA del lock (no retiene a la UI)
            {
                int start = offset + c * len;
                int end = (c == chunks - 1) ? offset + count : start + len;
                double sumLo = 0, sumMid = 0, sumHi = 0;
                int nEsp = 0;
                if (espectro) for (int b = 0; b < SpecBands; b++) sSum[b] = 0;
                for (int i = start; i < end; i++)
                {
                    float v = buffer[i];
                    lp1 += (v - lp1) * A1;               // paso-bajo grave
                    lp2 += (v - lp2) * A2;               // paso-bajo medio
                    float lo = lp1, mid = lp2 - lp1, hi = v - lp2;
                    sumLo += lo * lo; sumMid += mid * mid; sumHi += hi * hi;
                    if (!espectro) continue;             // aurora apagada (o sin GPU): la escalera ni se toca
                    fSum += v;                           // junta los canales del cuadro; la escalera corre UNA vez por cuadro
                    if (++fCnt < chEsp) continue;
                    float m = fSum * invCh; fSum = 0f; fCnt = 0;
                    // ESPECTRO: cada peldano de la escalera se queda con lo que el anterior dejo pasar,
                    // asi que la resta de dos peldanos consecutivos ES la banda que hay entre sus cortes.
                    float ant = 0f;
                    for (int k = 0; k < 7; k++)
                    {
                        int q = k * SpecPolos;
                        float x = m, ck = sc[k];
                        // Cada polo se lleva a una local: antes se leia su casilla del array DOS veces por
                        // vuelta (una para sumarle y otra para volver a leerla) y el coeficiente en cada
                        // vuelta. Las cuentas son las mismas y en el mismo orden: el sonido no cambia ni un bit.
                        for (int pp = 0; pp < SpecPolos; pp++)
                        {
                            float pv = sl[q + pp];
                            pv += (x - pv) * ck;
                            sl[q + pp] = pv;
                            x = pv;
                        }
                        float bnd = x - ant; ant = x;     // lo que este peldano deja pasar menos lo del anterior
                        sSum[k] += bnd * bnd;
                    }
                    float top = m - ant;                 // lo que queda por encima del ultimo corte: el aire
                    sSum[7] += top * top;
                    nEsp++;
                }
                int n = Math.Max(1, end - start), ne = Math.Max(1, nEsp);
                tLo[c] = (float)Math.Sqrt(sumLo / n);
                tMid[c] = (float)Math.Sqrt(sumMid / n);
                tHi[c] = (float)Math.Sqrt(sumHi / n);
                if (espectro) for (int b = 0; b < SpecBands; b++) tEsp[c * SpecBands + b] = (float)Math.Sqrt(sSum[b] / ne);
            }
            fLp1 = lp1; fLp2 = lp2;   // persiste el estado de los filtros para el proximo bloque
            specLp = sl;
            // NIVELADO PERCEPTUAL (K-weighting, tipo LUFS/BS.1770): en vez del RMS crudo, mide la sonoridad
            // PESADA por como la oye el humano (realza agudos ~+4 dB, quita sub-graves) via 2 biquads/canal.
            // Asi una cancion con mucho bajo (que el RMS crudo sobre-estima) y una brillante quedan de verdad
            // al mismo volumen PERCIBIDO. Solo durante el aprendizaje de esta cancion.
            if (curMeasurePath != null)
            {
                int ch3 = ch < 1 ? 1 : ch;
                if (kwRate != rate || kwCoef1 == null || kwCanales != ch3 || kwSt == null)
                {
                    kwRate = rate; kwCanales = ch3; kwCanal = 0;
                    kwCoef1 = EqShelfHi(1681.974f, rate, 3.99984f, 0.70717f);   // "head" shelf de BS.1770
                    kwCoef2 = EqHighPass(38.1355f, rate, 0.50033f);             // RLB high-pass
                    kwSt = new float[ch3 * 8];
                }
                float[] st = kwSt, c1 = kwCoef1, c2 = kwCoef2;
                double sumK = 0; long nK = 0;
                // MUESTRA A MUESTRA, recordando el canal que toca entre bloques. Antes se recorria por cuadros
                // enteros: si una lectura partia un cuadro (un canal en este bloque y el otro en el siguiente),
                // ese trozo se tiraba y, peor, todo el bloque siguiente se leia corrido un canal: el izquierdo
                // pasaba por el filtro del derecho. Con cuadros enteros la cuenta es la misma que antes, bit a bit.
                int c = kwCanal; if (c >= ch3) c = 0;
                for (int i = offset; i < offset + count; i++)
                {
                    int b = c * 8; float x = buffer[i];
                    float y1 = c1[0]*x + c1[1]*st[b] + c1[2]*st[b+1] - c1[3]*st[b+2] - c1[4]*st[b+3];
                    st[b+1]=st[b]; st[b]=x; st[b+3]=st[b+2]; st[b+2]=y1;
                    float y2 = c2[0]*y1 + c2[1]*st[b+4] + c2[2]*st[b+5] - c2[3]*st[b+6] - c2[4]*st[b+7];
                    st[b+5]=st[b+4]; st[b+4]=y1; st[b+7]=st[b+6]; st[b+6]=y2;
                    sumK += y2 * y2; nK++;
                    if (++c >= ch3) c = 0;
                }
                kwCanal = c;
                curSumSq += sumK; curSamples += nK;   // energia K-pesada (misma escala que el RMS de antes)
            }
            // EQ AUTOMATICO: potencia por banda y canal, solo mientras falta una huella actual.
            if (eqMeasPath != null)
            {
                int ch2 = ch < 1 ? 1 : ch;
                if (eqMeasRate != rate || eqMeasCoef == null || eqMeasSt == null || eqMeasSt.Length != ch2 * 32)
                {
                    eqMeasRate = rate; eqMeasCoef = new float[8][]; eqMeasSt = new float[ch2 * 32];
                    for (int b = 0; b < 8; b++) eqMeasCoef[b] = EqBpf(EqCentros[b], rate);
                    eqPendN = 0;
                }
                if (eqPend == null || eqPend.Length != ch2) { eqPend = new float[ch2]; eqPendN = 0; }
                float[] st = eqMeasSt; double[] sums = eqMeasSum;
                int i = offset, fin = offset + count;
                // Lo mismo con la huella tonal: el cuadro que quedo partido en el bloque anterior se completa
                // con el principio de este, y el que quede partido al final se guarda para el siguiente.
                if (eqPendN > 0)
                {
                    while (eqPendN < ch2 && i < fin) eqPend[eqPendN++] = buffer[i++];
                    if (eqPendN == ch2) { AccumulateEqFrame(eqPend, 0, ch2, eqMeasCoef, st, sums); eqMeasN++; eqPendN = 0; }
                }
                if (eqPendN == 0)
                {
                    for (; i + ch2 <= fin; i += ch2)
                    {
                        AccumulateEqFrame(buffer, i, ch2, eqMeasCoef, st, sums);
                        eqMeasN++;
                    }
                    while (i < fin) eqPend[eqPendN++] = buffer[i++];
                }
            }
            lock (levelLock)
            {
                for (int c = 0; c < chunks; c++)
                {
                    int atMs = now + (int)((long)(c * len) * 1000 / Math.Max(1, samplesPerSec));   // desfase del trozo dentro del bloque
                    EnqueueLevel(atMs, tLo[c], tMid[c], tHi[c], espectro ? tEsp : null, c * SpecBands);
                }
            }
        }

        // Bajo levelLock: un espectro no se reutiliza hasta que sale de la cola y la UI
        // ha copiado sus valores. Reciclar antes del alta mantiene tambien el pico en 96.
        private void ReturnLevelBuffer(float[] values)
        {
            if (values == null) return;
            if (levelBuffers == null) levelBuffers = new Stack<float[]>(96);
            if (levelBuffers.Count < 96) levelBuffers.Push(values);
        }
        private void EnqueueLevel(int at, float lo, float mid, float hi, float[] values, int offset)
        {
            while (levelQ.Count >= 96) ReturnLevelBuffer(levelQ.Dequeue().Esp);
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
            lock (levelLock)
                while (levelQ.Count > 0 && unchecked(now - (levelQ.Peek().At + audioVisualDelayMs)) >= 0)
                {
                    Lvl l = levelQ.Dequeue(); rmsLo = l.Lo; rmsMid = l.Mid; rmsHi = l.Hi;
                    if (l.Esp != null) Array.Copy(l.Esp, specNow, SpecBands);
                    ReturnLevelBuffer(l.Esp);
                }
        }
        private void ClearLevels()
        {
            lock (levelLock) while (levelQ.Count > 0) ReturnLevelBuffer(levelQ.Dequeue().Esp);
        }

        // Detecta el golpe de UNA banda contra su propio piso movil y devuelve su pulso (0..1) con ataque instantaneo
        // y caida rapida. Como cada banda tiene MENOS energia que el total, sube algo la sensibilidad. Igual para las 3.
        private static float BandBeat(float rms, ref float avg, float lev)
        {
            avg += (rms - avg) * 0.06f;                                                     // piso movil (~0.7s)
            float beat = (rms > avg * 1.22f + 0.006f) ? Math.Min(1f, (rms - avg) * 11f) : 0f;   // cuanto SOBRESALE del piso
            float tgt = Math.Max(beat, Math.Min(1f, rms * 3.0f) * 0.22f);                  // golpe dominante + fondo suave
            if (tgt > lev) lev = tgt; else lev *= 0.78f;                                    // ataque instantaneo, caida rapida
            return lev < 0.003f ? 0f : lev;
        }

        private void UpdateNowPlayingUI()
        {
            if (nowPlaying != null)
            {
                string tit, art;
                nowPlaying.TituloArtista(out tit, out art);
                nowFullText = tit; nowArtista = art;
            }
            else { nowFullText = "Nada reproduciendose"; nowArtista = ""; }
            iaBadgeKind = MejKind();   // dorado = version IA (Apollo); plateado = DSEE espectral reconstruyendo DE VERDAD
            CambioElPanelDeReposo();   // la insignia sale tambien en el panel de reposo
            ActualizaGenero();    // la pastilla del genero, antes de acomodar: manda en el ancho del artista
            PositionMejorada();   // pone titulo y artista y coloca el letrero MEJORADA
            lblNext.Text = (queue.Count > 0) ? "Sigue:  " + queue[0].Display() : "Sigue:  -";
            pastCola.Valor = queue.Count.ToString();
            LlenaPortaditas();
            pastCola.Invalidate();
            if (miniCube != null)
            {
                if (nowPlaying != null)
                {
                    Image nueva = LoadCover(nowPlaying.Disc);
                    // EL RELEVO: si ya habia un cubo puesto con otra portada, no se cambia de golpe; el
                    // cubo da media vuelta y la nueva aparece en el camino. La primera cancion de la
                    // noche, y la portada que llega tarde a la que YA suena, entran sin giro: ahi no hay
                    // relevo de nada que enseñar.
                    if (relevoOn && miniCube.Visible && miniCube.Cover != null && nueva != null && !object.ReferenceEquals(nueva, miniCube.Cover))
                        miniCube.Relevo(nueva);
                    else if (!miniCube.Girando) miniCube.Cover = nueva;
                    miniCube.Visible = true;
                }
                else { miniCube.Cover = null; miniCube.Visible = false; }
                miniCube.Invalidate();
            }
        }

        // 0 = sin letrero, 1 = plateado (DSEE espectral RECONSTRUYENDO de verdad), 2 = dorado (version IA).
        // El plateado exige que la puerta interna del reconstructor este actuando: con el switch prendido
        // pero la cancion ya completa (pase directo) NO hay letrero, porque no se esta mejorando nada.
        private int MejKind()
        {
            if (nowPlaying == null || nowPlaying.IsVideo) return 0;
            if (iaNowEnhanced) return 2;
            SpectralTap sp = curSbr;
            return (DseeActivo() && sp != null && sp.Mejorando) ? 1 : 0;
        }

        // Coloca el letrero "MEJORADA" justo despues del texto de "Reproduciendo" (misma fila). Si el
        // titulo es largo, lblNow se ENCOGE (elipsa antes) para que el letrero siempre quepa sin encimarse.
        private void PositionMejorada()
        {
            if (lblMejorada == null || lblNow == null || lblNowTop == null) return;
            bool on = (iaBadgeKind != 0 && nowPlaying != null);
            int bw = 0;
            if (on)
            {
                lblMejorada.ForeColor = (iaBadgeKind == 2) ? IaGoldBase : IaSilverBase;   // color base: el brillo lo anima el tick (CPU) o un halo que recorre (GPU)
                bw = TextRenderer.MeasureText(lblMejorada.Text, lblMejorada.Font).Width + 4;
            }
            // El titulo va arriba y el artista debajo; el letrero se pone detras del ARTISTA, que es el renglon
            // corto, para no comerle sitio al titulo.
            if (lblNowTop.Text != nowFullText) lblNowTop.Text = nowFullText;
            if (lblNow.Text != nowArtista) lblNow.Text = nowArtista;
            if (lblNowTop.Width != iaNowRowW) lblNowTop.Width = iaNowRowW;

            // LA PASTILLA DEL GENERO va en este mismo renglon, entre el artista y el letrero: es de la
            // cancion, igual que el artista, y en el renglon de arriba -el titulo- no cabria sin comerle
            // sitio a lo unico que de verdad hay que leer entero.
            int gw = (pastGenero != null) ? pastGenero.AnchoQuePide() : 0;
            int anchoArt = iaNowRowW - (on ? bw + 12 : 0) - (gw > 0 ? gw + 12 : 0);
            if (anchoArt < 60) anchoArt = 60;
            if (lblNow.Width != anchoArt) lblNow.Width = anchoArt;

            int tw = TextRenderer.MeasureText(lblNow.Text, lblNow.Font).Width;
            if (tw > anchoArt) tw = anchoArt;
            int x = lblNow.Left + tw + 12;
            if (gw > 0)
            {
                int gh = Math.Min(pastGenero.AltoQuePide(), lblNow.Height);
                pastGenero.SetBounds(x, lblNow.Top + (lblNow.Height - gh) / 2, gw, gh);
                if (!pastGenero.Visible) pastGenero.Visible = true;
                x += gw + 12;
            }
            else if (pastGenero != null && pastGenero.Visible) pastGenero.Visible = false;

            if (!on) { if (lblMejorada.Visible) lblMejorada.Visible = false; return; }
            lblMejorada.SetBounds(x, lblNow.Top, bw, 22);
            lblMejorada.Visible = true;
        }

        // DE QUE ES LO QUE SUENA. La misma respuesta que dentro del disco y que en el buscador (GeneroDeCancion),
        // asi que las tres dicen lo mismo. Un VIDEO no lleva: no hay de donde sacarla. Si no se sabe, no sale
        // pastilla -mejor sin etiqueta que con una inventada-.
        private void ActualizaGenero()
        {
            if (pastGenero == null) return;
            string et = (nowPlaying != null && !nowPlaying.IsVideo) ? GeneroDeCancion(nowPlaying.Song, nowPlaying.Disc) : "";
            if (et == pastGenero.Texto) return;
            pastGenero.Texto = et;
            pastGenero.Tinte = (et.Length > 0) ? ColorDeEtiqueta(et) : DefaultAccent;
            PositionMejorada();   // el ancho del artista depende de si hay pastilla y de lo que mida
            // El genero se aprende en segundo plano, asi que puede llegar con la cancion ya sonando. El fondo
            // va del mismo color, asi que se entera aqui en vez de esperar a la siguiente.
            if (nowPlaying != null && !nowPlaying.IsVideo && panelSongs != null)
            {
                ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
            }
        }

        // Reparte el texto entre la linea de ARRIBA (ancho completo) y la de ABAJO (la que comparte fila con el
        // letrero, asi que va mas estrecha). Si entra entero en la de abajo, la de arriba se queda vacia y se ve
        // exactamente igual que siempre. Se corta por ESPACIO; si una sola palabra no cabe, se corta por letra
        // antes que dejar el titulo mochado. Lo que aun sobre lo elipsa la propia etiqueta.
        private static void PartirNow(string texto, Font f, int anchoArriba, int anchoAbajo, out string arriba, out string abajo)
        {
            arriba = ""; abajo = texto ?? "";
            if (abajo.Length == 0 || anchoAbajo < 20) return;
            if (TextRenderer.MeasureText(abajo, f).Width <= anchoAbajo) return;   // cabe de una: nada que partir
            int tope = anchoArriba - 6;   // margen: la textura de la GPU dibuja con GDI+ y mide un pelo mas ancho
            int corte = -1;
            for (int i = 0; i < abajo.Length; i++)
            {
                if (abajo[i] != ' ') continue;
                if (TextRenderer.MeasureText(abajo.Substring(0, i), f).Width > tope) break;
                corte = i;   // el ultimo espacio que todavia cabe arriba
            }
            if (corte > 0) { arriba = abajo.Substring(0, corte); abajo = abajo.Substring(corte + 1); return; }
            // ni un espacio util (un titulo de una sola palabra larguisima): se corta por letra
            int n = 0;
            while (n + 1 < abajo.Length && TextRenderer.MeasureText(abajo.Substring(0, n + 1), f).Width <= tope) n++;
            if (n > 0 && n < abajo.Length) { arriba = abajo.Substring(0, n); abajo = abajo.Substring(n); }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (reader == null) return;
            // LA SALIDA SE QUEDO MUDA (SalidaSonido.cs): como si hubiera avisado de un fallo del aparato.
            if (SalidaMuda()) { NAudio.Wave.IWavePlayer o = output; if (o != null) Output_PlaybackStopped(o, new StoppedEventArgs(new SalidaMudaException())); return; }
            PreparaSiguienteSinPausa();   // a la que suena le quedan pocos segundos: la siguiente, lista para entrar sin pausa
            TimeSpan cur = reader.CurrentTime, tot = reader.TotalTime;
            if (tot.TotalSeconds > 0) progress.Value = cur.TotalSeconds / tot.TotalSeconds;
            lblTime.Text = Fmt(cur) + " / " + Fmt(tot);
            // (la posicion ya NO se guarda: al reanudar tras un corte de luz la cancion arranca desde el
            // principio, asi que el estado solo se escribe en eventos -> cero escrituras periodicas a disco)
        }

        // DONDE VA LA CASILLA DE BUSCAR en la fila de arriba: en el MEDIO DE LA PANTALLA, que es donde el
        // ojo la busca. Si centrada pisaria las instrucciones se corre a la derecha lo justo, y si aun asi no
        // cabe antes del encabezado se pega al final del hueco. 'minX' es donde acaban las instrucciones y
        // 'derecha' donde acaba la barra.
        private static int SitioBusca(int W, int busW, int minX, int derecha)
        {
            int x = minX;
            int centrado = (W - busW) / 2;
            if (centrado > x) x = centrado;
            if (x + busW > derecha) { x = derecha - busW; if (x < minX) x = minX; }
            return x;
        }

        // ---------- PRECALENTADO: QUE LA PRIMERA VEZ VAYA COMO LAS DEMAS ----------
        //
        // Casi todo lo que la rocola dibuja se HORNEA la primera vez que hace falta -el teclado del buscador
        // son 40 teclas con su degradado, su contorno y su letra; la lista de un disco se guarda hecha para
        // poder deslizarla- y eso cae DENTRO del primer cuadro: el cliente pulsa, la rocola se para a hornear
        // y el movimiento arranca a tirones. De la segunda vez en adelante va fino.
        //
        // Medido con pruebas/primera.cs en el servidor (en la rocola del bar hay que multiplicar por 4 o 5):
        //
        //     el buscador al abrirse .... 20.8 ms la 1a vez contra 1.9 ms despues
        //     la lista de un disco ......  3.6 ms  ...  contra 0.2 ms
        //     el encabezado y la pastilla de creditos: 0.02 y 0.06 ms de mas. NO se precalientan, no ganan nada.
        //
        // AQUI SOLO VAN LAS PANTALLAS QUE AL ENCENDER NO SE VEN. La pared de discos y la barra de letras
        // tambien cuestan su primera vez, pero es que ELLAS SON la pantalla de arranque: ese cuadro se paga
        // igual, adelantarlo no adelanta nada. El buscador, la lista de un disco y la pantalla de reposo
        // aparecen despues, y ahi si se gana entero.
        //
        // Y se calienta DIBUJANDO de verdad, una vez, en un mapa de bits de usar y tirar. Se podria llamar a
        // mano a cada Ensure*/Hornea*, pero esa lista se queda vieja en cuanto alguien anada un dibujo nuevo;
        // dibujando se pasa por el MISMO camino que el dibujo de verdad y se calienta lo que ese camino
        // calienta, hoy y dentro de un ano.
        private int precalPaso = 0;             // que pieza toca (-1 = ya esta todo caliente)
        private int precalReintentos = 0;       // si una pieza aun no tiene tamano, se reintenta un rato
        private const int PrecalQuieta = 400;   // ms sin tocar nada para ponerse a calentar

        // Dibuja un control una vez en un mapa de bits de usar y tirar. Nada de esto se ve: lo unico que hace
        // es que lo que ese dibujo hornea quede hecho. false = todavia no tiene tamano, se reintenta luego.
        private bool EnSeco(Control c, Action<Graphics> dibuja)
        {
            if (c == null || c.Width < 8 || c.Height < 8) return false;
            using (Bitmap b = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                dibuja(g);
            }
            return true;
        }

        // Al cambiar la RESOLUCION lo horneado deja de servir -esta hecho al tamano de antes- y hay que
        // volver a calentar; si no, la primera vez despues del cambio vuelve a trabarse.
        //
        // Con el color de acento NO se hace, a proposito: el acento cambia con cada cancion (es el color del
        // genero), asi que volver a calentar en cada cambio seria estar horneando toda la noche para ahorrar
        // una vez. Ahi se deja que se rehornee solo cuando de verdad se dibuje.
        private void VuelveAPrecalentar() { precalPaso = 0; precalReintentos = 0; }

        private static string Fmt(TimeSpan t) { return ((int)t.TotalMinutes) + ":" + t.Seconds.ToString("00"); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            DetieneVigiaFoco();
            AdvanceThumbWarm();
            CancelStartupDataLoad();
            preScanStop = true;   // que el pre-analisis suelte el archivo y termine
            unchecked { preScanGen++; durScanGen++; }
            DisposePlaybackNow();
            SueltaLookahead();   // el lector que esperaba a la siguiente: se suelta el archivo al cerrar
            GuardaTodoAlCerrar();
            // APAGADO DELIBERADO vs APAGON. El boton de poder de la rocola esta configurado en Windows para APAGAR
            // -> al presionarlo, Windows avisa y ESTE evento se dispara (CloseReason.WindowsShutDown; tambien Alt+F4
            // = UserClosing). Ahi borramos el estado vivo para que manana arranque LIMPIA. En cambio, un APAGON corta
            // la corriente de golpe y no corre NADA de esto: el archivo SOBREVIVE y, al volver la luz, la rocola
            // REANUDA (creditos + cola). Es decir, la sola presencia del archivo al arrancar delata que fue apagon.
            // Si ni siquiera publicamos los datos iniciales, conservar tambien el estado anterior.
            if (!startupReadPending && (e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.UserClosing))
                BorraSeguro(StateFile());
            IaStopMotor();   // motor y ffmpeg hijo que lanzamos: se van con la rocola
            base.OnFormClosing(e);
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
            private readonly Mp3FileReader mp3;
            private readonly SampleChannel canal;
            private readonly AudioFileReader r;
            public FileAudioSource(string path)
            {
                if (path != null && path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    Mp3FileReader m = AbreMp3(path);
                    try { canal = new SampleChannel(m, false); mp3 = m; m = null; }
                    finally { if (m != null) { try { m.Dispose(); } catch { } } }   // el canal fallo: el lector no se queda tirado
                }
                else r = new AudioFileReader(path);
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return (mp3 != null) ? canal.WaveFormat : r.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count)
            { return (mp3 != null) ? canal.Read(buffer, offset, count) : r.Read(buffer, offset, count); }
            public float Volume { set { if (mp3 != null) canal.Volume = value; else r.Volume = value; } }
            public TimeSpan CurrentTime { get { return (mp3 != null) ? mp3.CurrentTime : r.CurrentTime; } }
            public TimeSpan TotalTime { get { return (mp3 != null) ? mp3.TotalTime : r.TotalTime; } }
            public void Salta(TimeSpan donde) { if (mp3 != null) mp3.CurrentTime = donde; else r.CurrentTime = donde; }
            public void Dispose()
            {
                if (mp3 != null) { try { mp3.Dispose(); } catch { } }
                if (r != null) { try { r.Dispose(); } catch { } }
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
            public int Read(float[] buffer, int offset, int count) { return sample.Read(buffer, offset, count); }
            public float Volume { set { sample.Volume = value; } }
            public TimeSpan CurrentTime { get { return mp3.CurrentTime; } }
            public TimeSpan TotalTime { get { return mp3.TotalTime; } }
            public void Salta(TimeSpan donde) { mp3.CurrentTime = donde; }
            public void Dispose() { try { mp3.Dispose(); } catch { } try { ms.Dispose(); } catch { } }
        }

        // ==========  RECONSTRUCTOR ESPECTRAL DE AGUDOS (el DSEE "serio"; NIVEL 2)  ==========
        // Trabaja como el DSEE de Sony pero sin red neuronal: parte el audio en ventanas (STFT de 2048 puntos,
        // solape 50%, ventana raiz-de-Hann en analisis Y sintesis: con el espectro intacto la reconstruccion
        // es EXACTA), LOCALIZA el "acantilado" donde el codec corto los agudos y RE-CREA la banda faltante
        // transponiendola UNA OCTAVA hacia arriba (bin destino t <- bin fuente t/2, como el SBR de HE-AAC),
        // con la magnitud EMPATADA en la costura y decayendo ~9 dB/octava (como decae la musica real). La fase
        // se hereda de la fuente (suena natural, no metalico). Archivos ya completos (WAV / 320kbps, corte
        // >= ~19.3 kHz) pasan INTACTOS: sin acantilado no se toca ni un bin. Costo: ~3-5% de CPU (4 FFT de
        // 2048 por ciclo de ~23ms en estereo); por eso SOLO se ofrece si la maquina cumple requisitos
        // (ProbeAudioTier). Active se alterna EN VIVO desde Ctrl+F3 para comparar A/B: la banda recreada entra y
        // sale con un fundido corto y no se pierde ni se repite ninguna muestra (ver los campos de abajo).
        private sealed class SpectralTap : NAudio.Wave.ISampleProvider
        {
            private readonly int Frame, Hop;   // 2048 normal; 4096 en NIVEL HD (bins mas finos = deteccion y costura mas limpias)
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, rate;
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

            private volatile bool mejoro;                  // ya reconstruyo al menos UNA vez en esta cancion
            // La cancion FUE mejorada en esta reproduccion: en cuanto Regenerate() actua un cuadro, esto
            // queda prendido hasta que la cancion cambie (cada cancion estrena su propio SpectralTap).
            // Es un SEGURO, no un termometro: el letrero plateado no parpadea aunque la medicion del
            // acantilado vaya y venga; una cancion ya completa (que nunca se reconstruye) jamas lo prende.
            public bool Mejorando { get { return Active && mejoro; } }
            private bool srcEnded;
            private long sourceFrames, emittedFrames;      // conserva tambien la fraccion final menor que un Hop
            private readonly float[] iaIn = new float[28], iaOut = new float[12];   // IA propia: entrada/salida de la red (reusables)

            public SpectralTap(NAudio.Wave.ISampleProvider s, int frameSize)
            {
                Frame = (frameSize >= 4096) ? 4096 : 2048; Hop = Frame / 2;   // solo potencias de 2 conocidas
                src = s;
                ch = Math.Max(1, s.WaveFormat.Channels);
                rate = Math.Max(8000, s.WaveFormat.SampleRate);
                binHz = (float)rate / Frame;
                inBuf = new float[ch][]; outAcc = new float[ch][];
                for (int c = 0; c < ch; c++) { inBuf[c] = new float[Frame]; outAcc[c] = new float[Frame]; }
                re = new float[Frame]; im = new float[Frame];
                if (ch >= 2) { reB = new float[Frame]; imB = new float[Frame]; }   // los canales se procesan de dos en dos (ver ProcessFrame)
                AudioFftPlan plan = GetAudioFftPlan(Frame);
                win = plan.Window; bitrev = plan.Reverse;
                twCos = plan.Cos; twSin = plan.Sin; twSinNeg = plan.SinNeg;
                inter = new float[Hop * ch];
                outQ = new float[Frame * ch * 2];          // FIFO holgado (nunca acumula mas de Hop*ch)
            }

            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }

            public int Read(float[] buffer, int offset, int count)
            {
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
                stft = true; saliendo = false; ultimoCero = false; restoN = 0;
                fill = Hop; firstFrame = true; qHead = 0; qLen = 0;
                srcEnded = false; sourceFrames = 0; emittedFrames = 0;
                for (int c = 0; c < ch; c++)
                { Array.Clear(outAcc[c], 0, Frame); Array.Clear(inBuf[c], 0, Frame); }
            }

            // Procesa UNA ventana completa: FFT -> (detector + regeneracion) -> iFFT -> solape-y-suma.
            // Emite Hop muestras por canal al FIFO y desliza entrada/salida media ventana.
            // Tablas de la FFT (inversion de bits y factores de giro). Las comparten el camino de
            // reproduccion y el de solo analisis, para que ambos calculen EXACTAMENTE el mismo espectro.
            // Las tablas se crean en GetAudioFftPlan; no se modifican al procesar muestras.

            // ---------- COSECHA DE MATERIAL PARA LA IA ----------
            // Para ENSEÑARLE a la red hacen falta pares "esto queda / estos eran los agudos reales". En un MP3
            // dañado los agudos reales YA NO EXISTEN, asi que solo sirven las canciones que aun los conservan:
            // a esas se les corta el agudo A PROPOSITO y se guarda lo que habia arriba. Todo esto usa las MISMAS
            // funciones que la reproduccion (BuildIaFeatures, Hz, la FFT), asi que el material de entrenamiento
            // coincide EXACTAMENTE con lo que la red vera en vivo: si no, lo aprendido no serviria de nada.
            public SpectralTap(int rateHz, int channels)   // solo analisis: no reproduce, no lee de ninguna fuente
            {
                // EL TAMANO DE CUADRO HAY QUE PONERLO AQUI. En esta rama 'Frame' es un CAMPO (el nivel HD usa
                // 4096 para afinar mas), no una constante, y este constructor no lo ponia: se quedaba en CERO.
                // Con Frame=0 este analizador no analizaba NADA -ni una FFT, ni el acantilado- asi que el
                // pre-analisis no juntaba ni un ejemplo para la IA y tampoco media a que altura el MP3 talo
                // los agudos. Se usan 2048, que es el cuadro del reconstructor normal y el mismo con el que
                // cosecha la otra rama: asi el material entrenado vale igual en las dos.
                Frame = 2048; Hop = Frame / 2;
                src = null; ch = channels < 1 ? 1 : channels; rate = rateHz < 8000 ? 44100 : rateHz;
                binHz = (float)rate / Frame;
                re = new float[Frame]; im = new float[Frame];
                AudioFftPlan plan = GetAudioFftPlan(Frame);
                win = plan.Window; bitrev = plan.Reverse;
                twCos = plan.Cos; twSin = plan.Sin; twSinNeg = plan.SinNeg;
            }
            public int FrameLen { get { return Frame; } }
            public float CutHz { get { return cutHz; } }   // acantilado detectado hasta ahora (0 = aun sin medir)
            public float CutLoHz { get { return cutLoHz; } }   // por ABAJO: donde la grabacion deja de tener grave (0 = sin medir)

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
                float fc = cut * binHz, top = topBin * binHz;
                for (int j = 0; j < 12; j++)
                {
                    int a = Hz(fc * (float)Math.Pow(top / fc, j / 12.0));
                    int b = Hz(fc * (float)Math.Pow(top / fc, (j + 1) / 12.0));
                    if (a < cut) a = cut; if (b > topBin) b = topBin;
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
                    // aparecia de golpe. Se mide desde la primera ventana de musica entera.
                    if (cn == 0 && !firstFrame)
                    {
                        // Potencia de AMBOS canales ya separados; no mezclar las ondas
                        // (los agudos en contrafase se cancelarian). Sin FFT adicionales.
                        detectPair = true; DetectCliff(); detectPair = false;
                    }
                    Regenerate();                // canal par
                    Intercambia(); Regenerate(); Intercambia();   // canal impar (Regenerate trabaja sobre re/im)
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
                    if (cn == 0 && !firstFrame) DetectCliff();   // la ventana de arranque no mide (ver arriba)
                    Regenerate();
                    Fft(true);
                    float[] acc = outAcc[cn];
                    for (int i = 0; i < Frame; i++) acc[i] += re[i] * win[i];
                }
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

            private int Hz(float hz) { int b = (int)(hz / binHz); int ny = Frame / 2; return b < 1 ? 1 : (b > ny - 1 ? ny - 1 : b); }

            // Localiza el "acantilado" del codec: la frecuencia MAS ALTA donde todavia hay energia comparable
            // a la banda media (4-8 kHz). Suavizado entre ventanas para que no baile; en silencio no se mide.
            private double DetectionPower(int k)
            {
                double p = re[k] * re[k] + im[k] * im[k];
                return detectPair ? (p + (reB[k] * reB[k] + imB[k] * imB[k])) * 0.5 : p;
            }
            private void DetectCliff()
            {
                int b4 = Hz(4000f), b8 = Hz(8000f);
                if (b8 <= b4 + 4) return;
                double refE = 0; for (int b = b4; b < b8; b++) refE += DetectionPower(b);
                refE /= (b8 - b4);
                if (refE < 1e-10) return;                        // silencio: conservar la medida previa
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
                cutHz = (cutHz <= 0f) ? found : (cutHz * 0.85f + found * 0.15f);
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
                cutLoHz = (cutLoHz <= 0f) ? found : (cutLoHz * 0.85f + found * 0.15f);
            }

            // Re-crea la banda por ENCIMA del acantilado: t <- t/2 (una octava abajo = contenido REAL que
            // sobrevivio). La ENVOLVENTE (cuanto suena cada sub-banda nueva) la decide la IA PROPIA si hay
            // modelo cargado (rocola_ia.bin); si no, la curva clasica: costura empatada y ~-9 dB/octava.
            private void Regenerate()
            {
                if (cutHz <= 0f || cutHz >= 19300f) return;      // sin medida aun, o archivo ya completo
                if (regen <= 0f) return;                          // apagandose y ya en cero: la ventana sale tal cual
                int ny = Frame / 2;
                int cut = (int)(cutHz / binHz);
                if (cut < 64) return;                             // corte absurdamente bajo: no inventar tanto
                mejoro = true;                                    // desde aqui SI se reconstruye (neural o clasico): el letrero queda toda la cancion
                int topBin = Math.Min((int)(Math.Min(20500f, ny * binHz * 0.95f) / binHz), Math.Min(2 * cut - 2, ny - 4));
                if (topBin <= cut + 4) return;
                if (NeuralEnv.Loaded && RegenNeural(cut, topBin)) return;   // IA propia: envolvente APRENDIDA
                double mBelow = 0, mSrc = 0;                      // costura: magnitud media bajo el corte vs banda fuente
                for (int k = cut - 6; k < cut; k++) mBelow += Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                int s0 = cut >> 1;
                for (int k = s0; k < s0 + 6; k++) mSrc += Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                if (mSrc < 1e-9) return;
                float g = (float)(mBelow / mSrc) * 0.85f;         // empatada con colchon (que la costura NUNCA suba)
                if (g > 1.1f) g = 1.1f;
                if (g <= 1e-6f) return;
                float step = (float)Math.Pow(10.0, -0.45 / cut);  // ~-9 dB por octava (una octava = cut bins)
                float f = g;
                for (int t = cut; t <= topBin; t++)
                {
                    int s = t >> 1;                               // fuente: una octava abajo (siempre < cut: contenido real)
                    float fr = f * regen;                         // con el fundido completo (1) es f exacto
                    re[t] += re[s] * fr; im[t] += im[s] * fr;
                    int m = Frame - t;                            // espejo (frecuencias negativas): la señal sigue siendo real
                    re[m] = re[t]; im[m] = -im[t];
                    f *= step;
                }
            }

            // ---- IA PROPIA: caracteristicas + envolvente aprendida ----
            // CONTRATO RIA1 (debe COINCIDIR EXACTO con ia/entrenar_ia.py):
            //   entrada[0..23]  = log10(energia media por bin, |X/N|^2) de 24 bandas log de 200 Hz al corte,
            //                     restandoles su media (forma espectral, invariante al volumen)
            //   entrada[24]     = esa media (sonoridad global, log10)
            //   entrada[25]     = corte en Hz / 20000
            //   entrada[26]     = planitud de las ultimas 8 bandas bajo el corte (log10 geo - log10 arit; <= 0)
            //   entrada[27]     = 0 (reservado)
            //   salida[0..11]   = log10(energia objetivo / energia de la COSTURA) por 12 sub-bandas log del
            //                     corte a min(2*corte, 20 kHz)   [costura = banda 0.92*corte .. corte]
            private bool BuildIaFeatures(int cut)
            {
                float n2 = (float)Frame * (float)Frame;
                float fc = cut * binHz;
                if (fc < 1200f) return false;
                double sum = 0;
                for (int i = 0; i < 24; i++)
                {
                    int a = Hz(200f * (float)Math.Pow(fc / 200f, i / 24.0));
                    int b = Hz(200f * (float)Math.Pow(fc / 200f, (i + 1) / 24.0));
                    if (b <= a) b = a + 1;
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

            private bool RegenNeural(int cut, int topBin)
            {
                if (!BuildIaFeatures(cut)) return false;
                if (!NeuralEnv.Predict(iaIn, iaOut)) return false;
                float n2 = (float)Frame * (float)Frame;
                int sa = Hz(cut * binHz * 0.92f); if (sa >= cut) sa = cut - 1;
                double seamE = 0; for (int k = sa; k < cut; k++) seamE += re[k] * re[k] + im[k] * im[k];
                seamE = seamE / ((cut - sa) * n2) + 1e-12;
                float fc = cut * binHz, top = topBin * binHz;
                for (int j = 0; j < 12; j++)
                {
                    int a = Hz(fc * (float)Math.Pow(top / fc, j / 12.0));
                    int b = Hz(fc * (float)Math.Pow(top / fc, (j + 1) / 12.0));
                    if (a < cut) a = cut; if (b > topBin) b = topBin; if (b <= a) continue;
                    double mapped = 0;                               // energia de la fuente transpuesta en esta sub-banda
                    for (int t = a; t < b; t++) { int s = t >> 1; mapped += re[s] * re[s] + im[s] * im[s]; }
                    mapped = mapped / ((b - a) * n2);
                    if (mapped < 1e-14) continue;                    // fuente muda: nada que transponer aqui
                    float yj = iaOut[j]; if (yj > 0.1f) yj = 0.1f; else if (yj < -4f) yj = -4f;   // tope: nunca mas fuerte que la costura (+~1dB)
                    float sc = (float)Math.Sqrt(seamE * Math.Pow(10.0, yj) / mapped) * 0.9f;      // 0.9 = colchon
                    if (sc > 3f) sc = 3f;
                    sc *= regen;                                     // fundido al encender/apagar (1 = tal cual)
                    for (int t = a; t < b; t++)
                    {
                        int s = t >> 1;
                        re[t] += re[s] * sc; im[t] += im[s] * sc;
                        int m = Frame - t;                           // espejo: la señal sigue siendo real
                        re[m] = re[t]; im[m] = -im[t];
                    }
                }
                return true;
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
        // flotante puro y no pasa por aqui; esto ayuda a la salida WaveOut de 16 bits de respaldo.)
        private sealed class DitherWaveProvider16 : NAudio.Wave.IWaveProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly NAudio.Wave.WaveFormat fmt;
            private float[] fbuf;
            private uint rs;   // estado xorshift para el dither (mas barato que System.Random)
            public DitherWaveProvider16(NAudio.Wave.ISampleProvider s)
            {
                src = s;
                fmt = new NAudio.Wave.WaveFormat(s.WaveFormat.SampleRate, 16, s.WaveFormat.Channels);
                rs = (uint)Environment.TickCount | 1u;   // semilla != 0 (xorshift no arranca en 0)
            }
            // Un uniforme [0,1) barato (xorshift32). Dos restados = dither TRIANGULAR (TPDF), estadisticamente
            // identico a Random.NextDouble (media 0, varianza 1/6, rango +/-1) pero ~1.5x mas rapido: y esto
            // corre en CADA muestra de 16 bits, SIEMPRE, en el fierro modesto de la rocola.
            private float NextUni() { rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5; return (rs >> 8) * (1f / 16777216f); }
            public NAudio.Wave.WaveFormat WaveFormat { get { return fmt; } }
            public int Read(byte[] buffer, int offset, int count)
            {
                int need = count / 2;                                  // muestras de 16 bits que caben
                if (fbuf == null || fbuf.Length < need) fbuf = new float[need];
                int got = src.Read(fbuf, 0, need);
                int bo = offset;
                for (int i = 0; i < got; i++)
                {
                    float d = NextUni() - NextUni();    // triangular en (-1..1) LSB
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
        private sealed class DeepBassProvider : NAudio.Wave.ISampleProvider
        {
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
            public DeepBassProvider(NAudio.Wave.ISampleProvider s, float amount)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels); Amount = amount;
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
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                float amt = Amount, obj;
                if (float.IsNaN(amt) || float.IsInfinity(amt)) obj = objetivo;   // un valor roto no mueve nada
                else obj = amt > 0.001f ? amt * 0.85f : 0f;
                if (!empezo) { empezo = true; mix = objetivo = obj; }            // al empezar: la pedida, sin rampa
                else if (obj != objetivo) { objetivo = obj; quedan = rampa; paso = (objetivo - mix) / rampa; }
                if (!vivo)
                {
                    if (mix == 0f && quedan == 0) return n;   // nunca encendido en esta cancion: pase directo, sin coste
                    vivo = true;
                }
                int i = offset, end = offset + n;
                // Un cuadro que quedo a medias en la lectura anterior (lecturas que parten cuadros; en la cadena
                // normal no pasa): se completa, y su suma pasa por la misma cuenta que los cuadros enteros.
                if (fase != 0)
                {
                    int k = Math.Min(ch - fase, n);
                    for (int c = 0; c < k; c++) { float x = buffer[i + c]; suma = (float)(suma + x); buffer[i + c] = x + hSal; }
                    i += k; fase += k;
                    if (fase < ch) return n;
                    cuadro[0] = suma; for (int c = 1; c < ch; c++) cuadro[c] = 0f;   // su suma, y ceros: la misma cuenta
                    fase = 0; suma = 0f;
                    Cuadros(cuadro, 0, 1);
                }
                int enteros = (end - i) / ch;
                Cuadros(buffer, i, enteros);
                i += enteros * ch;
                // Lo que sobra de un cuadro partido: recibe el grave del cuadro anterior y espera a la lectura siguiente.
                for (; i < end; i++) { float x = buffer[i]; suma = (float)(suma + x); buffer[i] = x + hSal; fase++; }
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
                    float m = b[i];
                    for (int c = 1; c < ch; c++) m = (float)(m + b[i + c]);
                    if (ch > 1) m /= ch;                          // antes de sumar el grave: con este orden Mono va el doble de rapido
                    for (int c = 0; c < ch; c++) b[i + c] += hs;
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
            private int drainFrames;
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
            private int Drain(float[] buffer, int offset, int count)
            {
                int frames = Math.Min(drainFrames, count / ch);
                for (int f = 0; f < frames; f++)
                {
                    int bi = offset + f * ch, di = dpos * ch;
                    for (int c = 0; c < ch; c++) { buffer[bi + c] = delay[di + c]; delay[di + c] = 0f; }
                    dpos++; if (dpos >= D) dpos = 0;
                }
                drainFrames -= frames;
                return frames * ch;
            }
            private void Repair(int c, float landing)
            {
                int L = runLen[c];
                if (L < MinRun || L > MaxRun || tooLong[c]) return;
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
            public int Read(float[] buffer, int offset, int count)
            {
                if (srcEnded) return Drain(buffer, offset, count);
                int n = src.Read(buffer, offset, count);
                if (n <= 0)
                {
                    srcEnded = true;
                    drainFrames = hadInput ? D : 0;   // vacia TODO el retardo; no corta las ultimas muestras
                    return Drain(buffer, offset, count);
                }
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
        }

        // LIMITADOR CON PRE-ESCUCHA (lookahead): en vez de recortar el pico en seco (lo que mete distorsion
        // aspera al subir el volumen), mira unos milisegundos HACIA ADELANTE y baja la ganancia SUAVE antes de
        // que llegue el pico, recuperandola despacio. Asi se puede subir mucho el volumen y que suene fuerte pero
        // LIMPIO. Aplica la MISMA ganancia a los dos canales (no mueve la imagen estereo). ~5 operaciones por
        // muestra: barato hasta en la XP. Va al final de la cadena, tras el nivelado/realce.
        // DMO necesita entrada adicional para entregar sus ultimas muestras. El relleno se
        // elimina contando los cuadros reales, no adivinando la duracion del archivo.
        // Solo HD/WASAPI; no introduce dependencias modernas en el camino XP de Modest.
        private sealed class SalidaRemuestreada : ISampleProvider, IDisposable
        {
            private sealed class FuenteConCola : ISampleProvider
            {
                private readonly ISampleProvider source;
                private int padding;
                public long RealSamples;
                public bool Ended;
                public FuenteConCola(ISampleProvider source, int targetRate)
                {
                    this.source = source;
                    // Margen holgado sobre el filtro DMO, tambien al bajar frecuencia.
                    padding = checked(512 * Math.Max(1, (source.WaveFormat.SampleRate + targetRate - 1) / targetRate) * source.WaveFormat.Channels);
                }
                public WaveFormat WaveFormat { get { return source.WaveFormat; } }
                public int Read(float[] b, int o, int n)
                {
                    int done = 0;
                    while (!Ended && done < n)
                    {
                        int got = source.Read(b, o + done, n - done);
                        if (got == 0) { Ended = true; break; }
                        done += got; RealSamples += got;
                    }
                    int zeros = Math.Min(padding, n - done);
                    // NAudio puede usar una vista float sobre byte[]: copiar por elemento.
                    for (int i = 0; i < zeros; i++) b[o + done + i] = 0;
                    padding -= zeros;
                    return done + zeros;
                }
            }
            private readonly FuenteConCola padded;
            private ResamplerDmoStream resampler;
            private ISampleProvider samples;
            private readonly WaveFormat format;
            private readonly float[] block;
            private readonly int rate, sourceRate, channels;
            private int position, available;
            private long emitted;
            private bool disposed;
            public SalidaRemuestreada(ISampleProvider source, int targetRate)
            {
                if (source == null) throw new ArgumentNullException("source");
                if (targetRate <= 0) throw new ArgumentOutOfRangeException("targetRate");
                rate = targetRate; sourceRate = source.WaveFormat.SampleRate; channels = source.WaveFormat.Channels;
                padded = new FuenteConCola(source, targetRate);
                format = WaveFormat.CreateIeeeFloatWaveFormat(rate, channels);
                block = new float[4096 * channels];
            }
            public WaveFormat WaveFormat { get { return format; } }
            public bool HasOutput { get { return emitted != 0; } }
            private long ValidSamples
            {
                get { return ((padded.RealSamples / channels * rate + sourceRate - 1) / sourceRate) * channels; }
            }
            public int Read(float[] b, int o, int n)
            {
                if (disposed) throw new ObjectDisposedException("SalidaRemuestreada");
                if (b == null) throw new ArgumentNullException("b");
                if (o < 0 || n < 0 || o > b.Length - n) throw new ArgumentOutOfRangeException();
                if (n == 0) return 0;
                // El DMO debe nacer en el hilo que lo lee. Construirlo en la UI STA
                // y usarlo en el hilo MTA de WASAPI falla con E_NOINTERFACE.
                if (samples == null)
                {
                    resampler = new ResamplerDmoStream(new SampleToWaveProvider(padded), format);
                    samples = new WaveToSampleProvider(resampler);
                }
                int done = 0;
                while (done < n)
                {
                    if (padded.Ended && emitted >= ValidSamples) break;
                    if (position == available)
                    {
                        available = samples.Read(block, 0, block.Length); position = 0;
                        if (available == 0)
                        {
                            if (!padded.Ended || emitted < ValidSamples)
                                throw new InvalidDataException("El remuestreador no entrego la cola completa");
                            break;
                        }
                    }
                    int take = Math.Min(n - done, available - position);
                    if (padded.Ended) take = (int)Math.Min(take, ValidSamples - emitted);
                    for (int i = 0; i < take; i++) b[o + done + i] = block[position + i];
                    done += take; position += take; emitted += take;
                }
                return done;
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (resampler != null) resampler.Dispose();
            }
        }

        // EL LIMITADOR DE MUESTRAS (el de siempre, descrito arriba). Ya NO va en la cadena: lo reemplaza
        // LimitadorPicoReal (PicoReal.cs), que hace lo mismo mirando la onda reconstruida entre muestras. Se
        // conserva como referencia: las pruebas (limite, picoreal) comparan contra el lo que se gano.
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
            // RELEVO SIN PAUSA a la pista siguiente del MISMO disco: el fundido se quita si aun no empezo (un disco
            // continuo tiene que seguir continuo). 0 = normal, 1 = quitado a tiempo, 2 = ya empezo y se termina.
            private int estadoFundido;
            internal bool Quita() { return System.Threading.Interlocked.CompareExchange(ref estadoFundido, 1, 0) == 0; }
            public FadeProvider(NAudio.Wave.ISampleProvider s, long total, double fadeSec)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels);
                int rate = s.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                fadeFrames = (long)(fadeSec * rate); if (fadeFrames < 1) fadeFrames = 1;
                totalFrames = total;
                if (totalFrames > 0 && totalFrames < fadeFrames * 4) totalFrames = 0;   // cancion muy corta: sin fundido de salida
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                int frames = n / ch;
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
            internal MedidaCancion Medida;     // lo que aprende oyendola, si entra por relevo sin pausa (Form1.SinPausa.cs)
            public volatile float Gain = 1f;   // objetivos; la rampa se aplica DESPUES de medir
            public volatile float Master = 1f; // los picos los controla el limitador final
            // REALCE DE AGUDOS (exciter armonico, tipo DSEE): separa la banda alta con un paso-alto de un polo,
            // la satura suave (tanh: crea armonicos NUEVOS por encima, el "aire" que el MP3 recorta) y mezcla
            // una pizca de vuelta. Todo en el dominio del tiempo (nada de FFT). La saturacion se calcula al
            // doble de frecuencia para que sus armonicos no se plieguen (Realce.cs); el camino de una muestra
            // por vez que queda abajo solo se usa si el realce se sube a media cancion desde cero.
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
                internal PreparedEq(float[] curve, int rate, int channels)
                {
                    Curve = curve;
                    Coefficients = new float[8][];
                    int active = 0;
                    for (int b = 0; b < 8; b++)
                    {
                        float[] cf = Form1.EqPeak(Form1.EqCentros[b], rate, curve[b]);
                        if (cf != null) Coefficients[active++] = cf;
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
                    if (p != null && ReferenceEquals(p.Curve, value)) return;
                    pendingEq = value == null ? null :
                        new PreparedEq(value, WaveFormat.SampleRate, WaveFormat.Channels);
                }
            }
            public LevelTap(NAudio.Wave.ISampleProvider s, Form1 o)
            {
                src=s;owner=o;audioChannels=Math.Max(1,s.WaveFormat.Channels);
                rampFrames=Math.Max(1,(int)(s.WaveFormat.SampleRate*.020));
                eqFadeFrames=Math.Max(1,(int)(s.WaveFormat.SampleRate*.030));
                exciterPole=ExciterPole(s.WaveFormat.SampleRate);exLp=new float[audioChannels];
                PreparaRealce();   // realce 2x (Realce.cs): su memoria se reserva aqui, nunca al leer
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            private void Objetivos(out float g, out float ex)
            {
                g=Gain*Master; ex=Excite;
                if(float.IsNaN(g)||float.IsInfinity(g)||g<0)g=audioReady?gainRamp.Current:1f;
                if(float.IsNaN(ex)||float.IsInfinity(ex))ex=audioReady?exciteRamp.Current:0f;
                ex=ex>.001f?Math.Min(1f,ex):0f;
            }
            public int Read(float[] buffer, int offset, int count)
            {
                if (vaciando) return VaciaRealce(buffer, offset, count);
                int n = src.Read(buffer, offset, count);
                if (n <= 0 && modoRealce == 2 && audioReady && owner != null)
                {
                    // Se acabo la cancion: el realce 2x tiene retenidos sus 31 cuadros; salen enteros, no se cortan.
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
                    owner.MedidaDe(this);   // relevo sin pausa: en su primer bloque, la medida en vivo pasa a ser de esta cancion
                    owner.ReportAudioBlock(buffer, offset, n, WaveFormat.SampleRate, WaveFormat.Channels);   // mide el nivel CRUDO (beat + sonoridad + huella tonal) ANTES de tocar nada
                    float g, ex;
                    Objetivos(out g, out ex);
                    PreparedEq prepared=pendingEq;
                    if(!audioReady)
                    {
                        gainRamp.Init(g);exciteRamp.Init(ex);currentEq=prepared;audioReady=true;
                        FijaModoRealce(ex);
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

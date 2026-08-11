// Rocola 2.5 - edicion clasica XP (rocola fisica; botones configurables, sin lenguaje de teclado de PC)
// Reemplaza TODO Form1.cs. Si tu proyecto NO se llama "Rocola", ajusta "namespace Rocola".
//
// BOTONES DE LA ROCOLA (configurables): la primera vez la rocola pide presionar cada boton
// y los guarda en rocola_teclas.txt (junto al .exe). Funciones:
//   ARRIBA / ABAJO  = pagina   (con REGRESAR mantenido = salta de letra A, B, C...)
//   IZQUIERDA / DERECHA = mover de disco
//   PONER  = entrar / poner cancion        REGRESAR = volver
//   MONEDA = credito                       SALTAR = saltar la cancion actual
//   BUSCAR = manten REGRESAR y presiona PONER
// Para reconfigurar botones: F8 (teclado de PC). F5 = re-escanear musica. F10 = pausa de mantenimiento.

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
        private const float FixedVolume = 1.0f;
        private const int Cols = 6;
        private const int Rows = 3;
        private const int PageSize = Cols * Rows;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr procId);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x02, MOUSEEVENTF_LEFTUP = 0x04;
        // ¿esta presionada AHORA esta tecla? (para los combos: REGRESAR + otra)
        private static bool KeyHeld(Keys k) { return (GetKeyState((int)(k & Keys.KeyCode)) & 0x8000) != 0; }

        // Sube la resolucion del reloj del sistema a 1ms para que el temporizador de animacion
        // dispare parejo (en XP, sin esto, un intervalo de ~33ms se "redondea" y la animacion se entrecorta).
        [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint p);
        [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint p);
        private bool timerResRaised = false;

        private static readonly Color DefaultAccent = Color.FromArgb(45, 95, 205);

        // ---- Controles ----
        private LetterBar letterBar;
        private AlbumView albumView;
        private Label lblHeader, lblStatus, lblNow, lblNext, lblCount, lblTime, lblAlbum, lblToast, lblCredits;
        // Linea de ARRIBA de "Reproduciendo": vacia casi siempre. Cuando el titulo no cabe en una sola
        // linea, la primera mitad se pinta aqui y el resto en lblNow (que sigue siendo la ULTIMA linea, la
        // que lleva el letrero MEJORADA detras). El hueco esta reservado SIEMPRE, asi que el acomodo de la
        // pantalla no se mueve nunca al cambiar de cancion: nada de rehornear la rejilla por un titulo largo.
        private Label lblNowTop;
        private string nowFullText = "Nada reproduciendose";   // el texto COMPLETO, antes de partirlo en lineas
        private HelpBar helpBar;
        private BgPanel panelSongs;
        private SongListView songList;
        private CubeBox cubeBox;
        private CubeBox miniCube;
        private ProgressView progress;
        private SearchOverlay searchOverlay;
        private System.Windows.Forms.Timer progressTimer, animTimer, toastTimer;
        private System.Windows.Forms.Timer focusTimer;                 // vigia de foco al arrancar (autoarranque)
        private bool maintMode = false;                                 // F10: pausa de mantenimiento del vigia de foco
        private DateTime maintUntil = DateTime.MinValue;                // la pausa expira sola (30 min) por si se olvida

        // ---- Reproduccion / cola ----
        private IWavePlayer output;   // WaveOut con callback por ventana (compatible con XP SIN .NET 3.5 SP1)
        private IAudioSource reader;   // fuente de audio: normal (AudioFileReader) o TOLERANTE (MP3 con frecuencia mezclada)
        // ---- BEAT para los visuales ----
        // El RMS se mide al LLENAR el colchon de WaveOut (~1s de buffers), o sea ~0.85s ANTES de que ese
        // pedazo SE OIGA: por eso el fondo "no iba con la musica" (pulsaba adelantado). Ahora cada bloque
        // se parte en sub-trozos de ~50ms con la MARCA DE TIEMPO en que sonaran, y el temporizador de UI
        // consume el trozo cuyo momento ya llego -> el pulso cae ENCIMA del golpe que se esta oyendo.
        private readonly object levelLock = new object();
        // 3 BANDAS (graves/medios/agudos) para que se SIENTA la separacion de beats: las estrellas GRANDES laten con
        // los graves (bombo), las medianas con medios/agudos. Cada trozo lleva el rms de las 3 bandas + su marca de tiempo.
        private struct Lvl { public int At; public float Lo, Mid, Hi; public float[] Esp; public Lvl(int at, float lo, float mid, float hi, float[] esp) { At = at; Lo = lo; Mid = mid; Hi = hi; Esp = esp; } }
        private readonly Queue<Lvl> levelQ = new Queue<Lvl>();
        private const int AudioVisualDelayMs = 850;   // colchon de WaveOut (5 x 200ms; en regimen va ~4 buffers adelantado)
        private float fLp1, fLp2;                      // estado de los filtros paso-bajo (hilo de audio) que separan las bandas
        private float rmsLo, rmsMid, rmsHi;            // rms retrasado por banda (lo que se oye AHORA)
        private float avgLo, avgMid, avgHi;            // piso movil por banda (referencia del golpe)
        private float levLo, levMid, levHi;            // pulso (envolvente) por banda para los visuales
        // ---------- AURORA DEL HORIZONTE (visualizador de espectro) ----------
        // 8 bandas medidas EN VIVO con una escalera de paso-bajos de una via (el mismo truco barato que ya
        // separaba graves/medios/agudos, extendido): banda[i] = lp[i] - lp[i-1]. Viajan por la MISMA cola
        // retrasada que el pulso, asi que la aurora se mueve encima del sonido que se esta oyendo y no un
        // segundo antes (el colchon de WaveOut es de ~0.85 s).
        private const int SpecBands = 8;
        // TRES polos por corte (18 dB/oct). Con UNO solo (6 dB/oct) las bandas se pisaban tanto que cualquier
        // tono encendia media aurora: se veia un bulto subiendo y bajando en vez de un espectro con forma.
        // Medido: con 1 polo, un tono de 240 Hz encendia las 8 bandas al maximo.
        private const int SpecPolos = 3;
        private static readonly float[] SpecCortes = { 60f, 150f, 350f, 800f, 1800f, 4000f, 9000f };   // 7 cortes = 8 bandas
        // Compensacion de inclinacion: en musica real los agudos traen MUCHA menos energia que los graves. Sin
        // esto la aurora seria una loma en la izquierda y nada mas. Son dB que se le suman a cada banda para que
        // todas puedan lucir (es lo mismo que hace cualquier analizador decente).
#if ROCOLA_GPU
        // CALIBRADA con ruido rosa (energia pareja por octava, que es como se reparte la musica real) al MISMO
        // volumen al que la rocola nivela todas las canciones. Los primeros 6 dB solo aplanan el banco; el resto
        // es una subida suave hacia los agudos, porque la musica de verdad trae menos brillo que el ruido rosa.
        // La version anterior (+20 dB arriba) convertia la fuga de los filtros en banda llena: la aurora salia
        // como una pared plana en vez de tener forma.
        private static readonly float[] SpecTilt = { 0f, 6f, 6f, 7f, 8f, 11f, 13f, 9.4f };
        // Ventana util en dB. Ajustada: con una ventana ancha, la fuga que dejan los filtros (aunque sea 40 dB
        // por debajo) se dibujaba como banda llena. La biblioteca ya viene NIVELADA, asi que la ventana puede
        // ser fija y ajustada sin que las canciones bajitas se queden sin aurora.
        private const float SpecPisoDb = -37f, SpecRangoDb = 34f;
#endif
        private float[] specLp;                        // estado de la escalera (hilo de audio)
        private int specRate;                          // ritmo de muestreo con el que se calcularon los coeficientes
        private float[] specCoef;                      // coeficiente por corte
        private readonly float[] specNow = new float[SpecBands];   // rms por banda que YA toca oirse
#if ROCOLA_GPU
        private readonly float[] specVis = new float[SpecBands];   // lo que se dibuja: sube al instante, baja suave
        private readonly float[] specPiso = new float[SpecBands];   // piso movil por banda (referencia del golpe)
        private readonly float[] specGolpe = new float[SpecBands];  // cuanto sobresale cada banda AHORA sobre su piso
#endif
        private float auroraAmount = 0.55f;            // "espectro=" en config (0 = apagada)
        // PRESENCIA de la aurora en pantalla, de 0 a 1. Antes se dibujaba o no se dibujaba segun si la banda
        // mas alta pasaba de un pelo: en un pasaje bajito (una intro, un silencio, el final de un tema) las
        // bandas caen a cero, la aurora DESAPARECIA de golpe y al volver la musica APARECIA de golpe. Eso era
        // el parpadeo. Ahora hay un mando suave: entra en algo mas de un segundo y se va en unos tres, asi que
        // ni aparece ni desaparece nunca de un cuadro para otro.
#if ROCOLA_GPU
        private float auroraPres;
#endif
        // ¿Hace falta analizar el espectro? Es el 90% del coste del analisis de audio, asi que cuando nadie
        // lo va a mirar (compilado sin GPU, o el usuario puso espectro=0) ni se calcula y el hilo de audio respira.
#if ROCOLA_GPU
        private bool EspectroActivo { get { return auroraAmount > 0.004f; } }
#else
        private bool EspectroActivo { get { return false; } }
#endif
        private float audioLevel = 0f;                // nivel GLOBAL (max de las 3 bandas): deriva, GPU, buscador
        private int playFailStreak = 0;   // fallos seguidos al reproducir (para no ciclar si NAudio falla)
        // ---- NIVELADO DE VOLUMEN (que todas las canciones suenen PAREJO) ----
        // Cada cancion viene masterizada a distinto volumen. Aqui se mide la sonoridad (RMS integrado) de cada
        // cancion MIENTRAS suena (aprovechando la medicion que ya se hace para el beat) y se guarda por ruta en
        // rocola_volumen.txt. En reproducciones siguientes se aplica una ganancia para llevarla a un nivel objetivo
        // COMUN, con recorte suave de picos. Asi, tras oirse una vez, cada cancion suena al mismo volumen que las
        // demas. La PRIMERA vez que se oye una cancion nueva va a su volumen original (aun no hay medida): se
        // aprende en esa pasada y ya queda pareja para siempre. Se puede apagar con normalizar=0 en rocola_config.txt.
        private bool normalizeOn = true;                                                     // nivelado activo (por defecto si)
        private string backupDir = null;                                                     // 2o destino del respaldo (idealmente OTRO disco); "respaldo_dir=" en config
        private readonly Dictionary<string, float> loudCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);   // ruta -> RMS integrado medido
        // DURACION de cada cancion (segundos). La calcula el PRE-ANALISIS junto con la sonoridad y se guarda en
        // el mismo archivo. Ademas de mostrarse en la lista, sirve de marca de "ya pre-analizada": esas canciones
        // ya tienen una medida COMPLETA y fiable, asi que sonar la cancion no vuelve a tocar su volumen medido.
        private readonly Dictionary<string, float> durCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);     // ruta -> duracion en segundos
        // DURACION RAPIDA. Sacar SOLO la duracion de un archivo cuesta ~600 veces menos que el analisis
        // completo: es leer la cabecera, no descomprimir el audio. Antes la duracion venia "de regalo" con ese
        // analisis pesado, asi que con cien mil canciones practicamente ninguna la mostraba (el analisis tarda
        // dias). Ahora va en su PROPIA pasada, que barre la biblioteca entera en minutos. Cuando el analisis
        // completo mide una cancion, SU duracion (del archivo entero) manda sobre esta.
        private readonly Dictionary<string, float> durFast = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> durFastPend = new List<string>();   // lineas apuntadas y aun sin escribir
        private readonly object durFastLock = new object();
        private System.Threading.Thread durScanTh;
        private volatile int durScanGen = 0;
        private volatile int durScanLeft = 0;   // cuantas duraciones faltan (para el panel)
        private System.Threading.Thread preScanTh;      // hilo del pre-analisis (uno solo a la vez)
        private volatile bool preScanStop = false;      // pedirle al pre-analisis que se detenga (al cerrar)
        private volatile int preScanLeft = 0;           // canciones que le faltan por medir (0 = al dia)
        private volatile int preScanGen = 0;            // generacion: al re-escanear la biblioteca (F5) el hilo viejo caduca y arranca uno nuevo
        private volatile int preScanTotal = 0;          // cuantas canciones tiene la tanda en curso (para la barra de avance)
        // ENTRENAMIENTO A FONDO (boton en Ctrl+F3, para cuando se hace mantenimiento): la rocola se dedica POR
        // COMPLETO a medir. Repasa TODA la biblioteca (no solo lo que falta), analiza cada cancion ENTERA en vez
        // de por muestras, y no espera a estar en reposo ni descansa entre archivos. Se apaga con el mismo boton.
        // A proposito NO se guarda en la configuracion: si se va la luz, la rocola vuelve a su modo normal sola
        // y nunca se queda a todo gas durante el horario del negocio por un olvido.
        private volatile bool preScanTurbo = false;
        // De que canciones se saco material para la IA y HASTA QUE NIVEL: 1 = el muestreo del modo automatico
        // (192 cuadros), 2 = el repaso A FONDO (12 veces mas). Se guarda el nivel, no un simple "ya esta",
        // porque los dos modos ACUMULAN: lo que junto el modo automatico se queda, y el repaso a fondo le
        // anade el resto. Dentro del MISMO nivel una cancion aporta una sola vez, para no sesgar hacia ella.
        private readonly Dictionary<string, int> iaDone = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Canciones que YA recibieron el repaso A FONDO (cancion completa). Se guarda en disco para que el
        // entrenamiento a fondo CONTINUE donde iba al detenerlo y volverlo a encender, al cerrar la rocola o
        // si se va la luz. Sin esto, cada arranque volvia a empezar por la primera cancion de la biblioteca y
        // con una biblioteca grande el repaso no terminaba nunca.
        private readonly HashSet<string> deepDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string curMeasurePath = null;                                                // cancion que se esta MIDIENDO ahora (null = no medir)
        private LevelTap curTap;                                                              // tap de la cancion actual (para cambiarle la ganancia en vivo desde Ctrl+F3)
        private double curSumSq = 0; private long curSamples = 0;                             // acumuladores del RMS integrado de la cancion actual (los llena el hilo de audio)
        // ----- EQ AUTOMATICO (correctivo y suave): cada cancion aprende su HUELLA TONAL (8 bandas anchas,
        // en dB relativos a su propia media) la primera vez que suena; desde la siguiente se corrige hacia
        // el promedio de la biblioteca, con tope de +-4 dB por banda y una perilla de fuerza en Ctrl+F3.
        // Corrige lo grosero (lodo en 200-400 Hz, canciones apagadas o chillonas) SIN uniformar el estilo. -----
        // 8 bandas centradas (biquads RBJ, el estandar de los EQ graficos: se portan bien hasta Nyquist,
        // donde los filtros simples de un polo se desmoronan). Medir usa BANDPASS de 0 dB con Q=2 (campana
        // angosta = poco sangrado entre bandas); corregir usa PEAKING con Q=1.1 (campana musical, ancha).
        internal static readonly float[] EqCentros = { 50f, 158f, 400f, 1000f, 2500f, 5600f, 10000f, 15800f };
        internal static float[] EqBpf(float fc, int rate)
        {
            double w = 2.0 * Math.PI * fc / Math.Max(8000, rate);
            if (w >= Math.PI * 0.95) return null;                       // el centro no cabe en este samplerate
            double al = Math.Sin(w) / 4.0, cw = Math.Cos(w), a0 = 1.0 + al;   // Q = 2
            return new float[] { (float)(al / a0), 0f, (float)(-al / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al) / a0) };
        }
        internal static float[] EqPeak(float fc, int rate, float dB)
        {
            double w = 2.0 * Math.PI * fc / Math.Max(8000, rate);
            if (w >= Math.PI * 0.95 || (dB > -0.1f && dB < 0.1f)) return null;   // sin correccion = ni se crea (identidad exacta)
            double A = Math.Pow(10.0, dB / 40.0), al = Math.Sin(w) / 2.2, cw = Math.Cos(w);   // Q = 1.1
            double a0 = 1.0 + al / A;
            return new float[] { (float)((1.0 + al * A) / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al * A) / a0), (float)(-2.0 * cw / a0), (float)((1.0 - al / A) / a0) };
        }
        // Biquads del K-weighting (BS.1770 / LUFS): un high-shelf que realza agudos (+4 dB) y un high-pass
        // que quita sub-graves. Con ellos, la sonoridad se mide como la OYE el humano. RBJ, a0-normalizado.
        internal static float[] EqShelfHi(float f0, int rate, float dB, float Q)
        {
            double w = 2.0 * Math.PI * f0 / Math.Max(8000, rate);
            double A = Math.Pow(10.0, dB / 40.0), cw = Math.Cos(w), al = Math.Sin(w) / (2.0 * Q), tsa = 2.0 * Math.Sqrt(A) * al;
            double a0 = (A + 1) - (A - 1) * cw + tsa;
            return new float[] {
                (float)(A * ((A + 1) + (A - 1) * cw + tsa) / a0),
                (float)(-2.0 * A * ((A - 1) + (A + 1) * cw) / a0),
                (float)(A * ((A + 1) + (A - 1) * cw - tsa) / a0),
                (float)(2.0 * ((A - 1) - (A + 1) * cw) / a0),
                (float)(((A + 1) - (A - 1) * cw - tsa) / a0) };
        }
        internal static float[] EqHighPass(float f0, int rate, float Q)
        {
            double w = 2.0 * Math.PI * f0 / Math.Max(8000, rate);
            double cw = Math.Cos(w), al = Math.Sin(w) / (2.0 * Q), a0 = 1.0 + al;
            return new float[] {
                (float)(((1 + cw) / 2.0) / a0), (float)((-(1 + cw)) / a0), (float)(((1 + cw) / 2.0) / a0),
                (float)((-2.0 * cw) / a0), (float)((1 - al) / a0) };
        }
        private float[] kwCoef1, kwCoef2; private float[] kwSt; private int kwRate = 0;   // K-weighting del nivelado (2 biquads/canal, 8 estados)
        // FICHA de cada cancion: 8 bandas de huella tonal en dB (relativas a la mediana de su cuerpo) y tres
        // datos de CALIDAD que salen del mismo analisis y antes se tiraban:
        //   [8] corte de ARRIBA (Hz): donde el codec le talo los agudos. Distingue una cancion sana de una
        //       dañada; decide si sirve para fijar el objetivo del EQ y que bandas tiene sentido corregirle.
        //   [9] RECORTE (0..1): que parte de la cancion viene con los picos aplastados desde la masterizacion.
        //   [10] corte de ABAJO (Hz): donde empieza a haber grave de verdad. Una grabacion vieja cortada en
        //       80 Hz necesita mas ayuda del bajo psicoacustico que una que baja a 30.
        // 0 = todavia no se sabe. Ninguno cuesta un analisis aparte: salen del recorrido que ya se hacia.
        private const int EqLen = 11, EqCut = 8, EqClip = 9, EqCutLo = 10;
        private readonly Dictionary<string, float[]> eqCache = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
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
        private float[][] eqMeasCoef = null; private float[] eqMeasSt = null; private int eqMeasRate = 0;   // bandpass de la medicion (mezcla mono; 4 estados por banda)
        private const long EqMinFrames = 882000;   // ~20 s de musica: con menos, la huella no es confiable
        // "SUBIR EL PISO": las canciones BAJITAS se SUBEN hacia un volumen objetivo; las que ya suenan FUERTE
        // NO se bajan (se prioriza subir las flojas, como pidio el operador). Ajustable desde la interfaz (Ctrl+F3).
        //   ganancia = (objetivo / rms) ^ fuerza , recortada a [1 , tope]   (fuerza 0..1; nunca por debajo de 1 = no baja)
        private float normTarget = 0.26f;                                                     // volumen OBJETIVO (RMS lineal) al que se suben las bajitas; mas alto = todo mas fuerte
        private float normStrength = 1.0f;                                                    // FUERZA del nivelado 0..1 (cuanto se acerca al objetivo; 0 = no toca nada)
        // REALCE DE AGUDOS (estilo DSEE de Sony, en su version honesta): el MP3 recorta el "aire" (agudos
        // altos) y eso ya NO se puede recuperar; lo que SI se puede es re-inventarlo, que es lo que hacen el
        // propio DSEE y los EXCITERS ARMONICOS clasicos (las cajas Aphex/BBE de los equipos de bar): tomar la
        // banda alta que sobrevivio (~5.5 kHz+), saturarla SUAVE (eso genera armonicos nuevos mas arriba) y
        // mezclar una pizca de vuelta. Se aplica en LevelTap tras nivelar; perilla en Ctrl+F3; "realce=" en config.
        private float excAmount = 0.35f;                                                      // intensidad del realce 0..1 (0 = apagado); 0.35 = sutil
        // BAJO PSICOACUSTICO: 0..1 ("bajo=" en config, perilla en Ctrl+F3). Recrea el grave que las bocinas
        // del local NO pueden dar, generando sus armonicos. Ver DeepBassProvider.
        private float bassAmount = 0.30f;
        private DeepBassProvider curBass;   // el de la cancion que suena (para mover la perilla en vivo)
        // AJUSTE POR CANCION. La perilla dice cuanto grave quiere el dueno del local; ESTE numero dice cuanto
        // le toca a la cancion que suena. No todas necesitan lo mismo: una con mucho grave profundo pierde
        // mucho en unas bocinas chicas y agradece la ayuda; una que ya viene cargada de bajo-medio no necesita
        // nada y si le metes mas, se emborrona. Sale de la huella tonal que la rocola YA le mide a cada cancion
        // (no hace falta medir nada nuevo). 1 = la perilla tal cual, que es lo que hacia antes con todas.
        private float bassSongFactor = 1f;
        private float BassBaseNow() { return bassAuto ? bassAutoBase : bassAmount; }
        private float BassAmountNow() { return Clampf(BassBaseNow() * bassSongFactor, 0f, 1f); }
        private float masterDb = 0f;         // SUBIR TODO: dB extra para ABSOLUTAMENTE todas las canciones (0..12; barra en Ctrl+F3, "subir_db=" en config)
        private float MasterLin() { return (float)Math.Pow(10.0, masterDb / 20.0); }   // dB -> ganancia lineal
        private float crossfadeSec = 0.5f;   // FUNDIDO de salida entre canciones: segundos de rampa al terminar ("fundido=" en config; 0 = corte seco)
        private bool declipOn = true;        // DES-RECORTE (declipper): repara picos aplastados de origen en archivos mal masterizados ("declip=" en config)
        // MATERIAL DE ENTRENAMIENTO PARA LA IA: mientras el pre-analisis recorre la biblioteca, las canciones
        // que AUN conservan sus agudos (320k/FLAC/WAV) sirven de maestras: se les corta el agudo a proposito y
        // se guarda "lo que queda / lo que habia". Ese archivo se lleva en una USB a una PC potente, se entrena
        // alli con ia/entrenar_ia.py y se regresa el rocola_ia.bin nuevo. Asi la IA aprende de TODA la
        // biblioteca sin tener que mover el disco de la rocola. "ia_datos=0" en config lo apaga.
        private bool iaHarvestOn = true;
        private volatile int iaPairsTotal = 0;          // pares ya guardados (solo para mostrar el avance)
        // ===== NIVELES DE MEJORA DE AUDIO segun la maquina (Ctrl+F3) =====
        // NIVEL 1: exciter (excAmount, arriba) - corre en CUALQUIER equipo (~1-2% de CPU).
        // NIVEL 2: reconstructor espectral (SpectralTap) - solo en maquinas que cumplen requisitos (ProbeAudioTier).
        // NIVEL 3 (futuro): DSEE con red neuronal. NO se puede embarcar honestamente en XP/.NET 3.5 (no existe
        //   runtime de inferencia y el CPU no alcanza); si algun dia la rocola corre en Windows 10 + .NET moderno,
        //   ESTE es el punto de enchufe: otro tap en la cadena de PlayTrack, gateado igual por ProbeAudioTier.
        private bool sbrOn = false;          // reconstruccion espectral activa (dsee= en config; Ctrl+F3)
        private Label lblMejorada;           // letrero "MEJORADA" dorado junto a "Reproduciendo": brilla cuando la cancion suena con DSEE
        private int mejRowW = 120;           // ancho total de la fila "Reproduciendo" (lo fija el layout; el letrero se acomoda dentro)
        private static readonly Color MejGoldBase = Color.FromArgb(238, 192, 76), MejGoldHi = Color.FromArgb(255, 233, 145);   // dorado: base y brillo
        private bool sbrAllowed = false;     // la maquina cumple los requisitos (se mide al arrancar)
        private bool sbrForce = false;       // dsee_forzar=1 en config: salta la puerta (para pruebas)
        private string sbrReason = "";       // por que NO esta disponible (se muestra en Ctrl+F3)
        private SpectralTap curSbr;          // tap de la cancion actual (para alternar A/B en vivo)
        private const float NormMaxBoost = 16.0f;                                             // techo ABSOLUTO de subida (+24 dB): evita amplificar al infinito una cancion casi muda (el tope real lo pone la etapa, ver TierMaxDb)
        private const float NormTargetMin = 0.06f, NormTargetMax = 0.40f;                     // rango del volumen objetivo que ofrece la interfaz (de flojo a muy fuerte)
        private const long NormMinSamples = 800000;                                           // ~9s en estereo 44.1k: minimo para aceptar una medida (si se salta antes, no se guarda)
        // ARCHIVOS INCOMPATIBLES: canciones que NO se pudieron reproducir ni con el lector tolerante (p.ej. MP3
        // demasiado dañados). Se anotan (una vez) en rocola_incompatibles.txt para poder repararlas/reconvertirlas.
        private readonly HashSet<string> incompatibleLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // ----- PAGINAS de canciones (vista de disco): la lista muestra solo la pagina actual -----
        private SongPagerBar songPager;                        // barra "pagina X de Y" con flechas, arriba de la lista
        private List<object> songItems = new List<object>();   // TODAS las canciones/videos del disco abierto
        private int songSel = 0;                               // seleccion global (entre todas las paginas)
        private readonly List<PlayItem> queue = new List<PlayItem>();
        private PlayItem nowPlaying;

        // ---- Estadisticas (base de "Lo mas sonado") ----
        // Se cuenta lo que entra a la cola CON EXITO (lo que la gente pide y paga). Se guarda en
        // rocola_stats.txt junto al .exe: en segundo plano cada minuto si hubo cambios, y al cerrar.
        private readonly Dictionary<string, int> songCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> videoCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private long statsTotal = 0, statsSongTotal = 0, statsVideoTotal = 0, statsCoins = 0, statsCreditsAdded = 0;
        private volatile bool statsDirty = false;
        private System.Windows.Forms.Timer statsTimer;
        // Topes del archivo de estadisticas: se conserva lo MAS sonado y reciente; lo viejo/poco sonado
        // se descarta solo, para que el archivo nunca crezca de mas ni alente la rocola.
        private const int SongStatCap = 2000, SongStatTarget = 1800;
        private const int VideoStatCap = 1000, VideoStatTarget = 900;
        private const int ArtistStatCap = 1000, ArtistStatTarget = 900;
        private readonly Dictionary<string, long> songSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);   // recencia (cuando se sono por ultima vez)
        private readonly Dictionary<string, long> videoSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private long statsSeq = 0;

        // ---- Datos ----
        private readonly List<Track> tracks = new List<Track>();
        private List<Album> albums = new List<Album>();
        private List<Album> videoAlbums = new List<Album>();   // discos que tienen archivo(s) de video
        private Dictionary<char, List<Album>> byLetter = new Dictionary<char, List<Album>>();
        private readonly Dictionary<string, Image> coverCache = new Dictionary<string, Image>();
        private readonly Queue<string> coverOrder = new Queue<string>();   // orden de llegada (para acotar memoria)
        private readonly HashSet<string> coverLoading = new HashSet<string>();
        private readonly List<Image> coverTrash = new List<Image>();   // expulsadas, a liberar de forma segura (en el timer)
        private volatile int coverGen = 0;     // sube al cambiar de pagina; descarta cargas de paginas viejas
        private bool coversOnHold = false;     // true mientras se hojea rapido: NO se piden caratulas
        private System.Windows.Forms.Timer coverSettleTimer;   // espera ~0.25s a que el usuario se detenga
        // DOS decodificaciones a la vez (una por nucleo): con SSD el cuello es el CPU, no el disco.
        // La interfaz no sufre (sus repintados son ligeros y por celda) y la musica esta blindada por su
        // colchon de 1 segundo. Resultado: una pagina con miniaturas listas carga en ~70-120ms.
        private static readonly System.Threading.Semaphore coverSem = new System.Threading.Semaphore(2, 2);
        // ----- PERFIL DE DISCO AUTOMATICO -----
        // Mide las primeras 16 lecturas de miniaturas y decide solo: SSD (lecturas ~0-2ms) -> 2 carriles
        // y prelectura amplia; HDD (>6ms promedio, por el cabezal) -> 1 carril y prelectura corta. Asi
        // la MISMA version queda optima en cualquier rocola, tenga el disco que tenga.
        private static int diskProbeN = 0, diskProbeMs = 0;
        private static volatile bool diskSlow = true;    // arranca en modo DISCO DURO (seguro); si las mediciones dicen SSD, se suelta
        private static void DiskProbe(int ms)
        {
            if (diskProbeN >= 16) return;
            System.Threading.Interlocked.Add(ref diskProbeMs, ms);
            if (System.Threading.Interlocked.Increment(ref diskProbeN) == 16)
                diskSlow = (diskProbeMs / 16) > 6;
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MemStatusEx { public uint Length, MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual; }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemStatusEx b);

        // Mide la maquina UNA vez (al arrancar) y decide si el NIVEL 2 (reconstructor espectral) se ofrece.
        // Regla: 2+ nucleos O un nucleo rapido (>=2200 MHz), y >=900 MB de RAM. El STFT en si usa poca RAM;
        // la puerta protege a los equipos que ya andan justos. Si la RAM no se pudo medir, no se bloquea por
        // eso; el CPU si es estricto (para eso existe la puerta). dsee_forzar=1 en config la salta (pruebas).
        private void ProbeAudioTier()
        {
            int cores = 1, mhz = 0; ulong ramMb = 0;
            try { cores = Environment.ProcessorCount; } catch { }
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0"))
                { object v = (k != null) ? k.GetValue("~MHz") : null; if (v != null) mhz = Convert.ToInt32(v); }
            }
            catch { }
            try
            {
                MemStatusEx ms = new MemStatusEx();
                ms.Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MemStatusEx));
                if (GlobalMemoryStatusEx(ref ms)) ramMb = ms.TotalPhys / (1024UL * 1024UL);
            }
            catch { }
            bool cpuOk = (cores >= 2) || (mhz >= 2200);
            bool ramOk = (ramMb == 0) || (ramMb >= 900);
            sbrAllowed = cpuOk && ramOk;
            if (!sbrAllowed)
                sbrReason = "CPU de " + cores + " nucleo(s) a " + mhz + " MHz" + (ramOk ? "" : (", RAM de " + ramMb + " MB"))
                          + "  (pide 2 nucleos o 2200 MHz, y 900 MB)";
        }

        private volatile int warmGen = 0;          // generacion del precalentador (se reinicia al recargar musica)
        private volatile int lastInputTick = 0;    // ultimo boton presionado (el precalentador pausa si hay gente)
        private List<string> cachedVideoPaths;            // rutas de video del cache de biblioteca (arranque rapido)
        private List<string> lastVideoPaths;              // rutas de video del ultimo armado (para guardar el cache)
        private Dictionary<string, string> cachedCovers;  // carpeta -> portada, del cache de biblioteca
        private string lastPageSig = null;
        private readonly object coverLock = new object();
        private const int ThumbPx = 256;         // resolucion de miniaturas (256 sobra a 1024x768; 384 gastaba 2x memoria sin verse mejor en la rocola)
        private const int CoverCacheMax = 220;   // ~ 256x256 x 220 ~ 56 MB (igual que la version fluida): cache amplia -> hojear no re-decodifica portadas (menos CPU al navegar)
        private readonly HashSet<char> letterAvailable = new HashSet<char>();
        private readonly List<SearchItem> searchIndex = new List<SearchItem>();
        private volatile bool libBuilding = false;   // el worker esta armando albums/byLetter/searchIndex: la UI no debe leerlos
        private char currentLetter = '#';
        private const char VideoMark = '\u25BA';   // "letra" especial = seccion VIDEOS
        private const char TopMark = '\u2605';     // "letra" especial = LO MAS SONADO (disco virtual)
        private const int TopMax = 100;             // tope de canciones del disco "Lo mas sonado"
        private List<Album> topDiscs = new List<Album>();   // discos virtuales "TOP - <genero>"
        // OPTIMIZACION (TOP): BuildTopDiscs recorre TODA la biblioteca (100k+). Antes se rebuildeaba en
        // cada cambio de pagina dentro de "Lo mas sonado". Ahora solo cuando los datos cambian (al contar
        // una reproduccion o tras re-escanear): topDirty=true marca que hay que reconstruir; si no, se
        // devuelve el topDiscs ya armado. Hojear TOP queda instantaneo.
        private bool topDirty = true;
        private readonly Dictionary<string, string> songGenre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // ruta -> etiqueta de genero (capturada al sonar)
        private int page = 0;
        private int selAlbumIndex = 0;
        private Album currentAlbum;
        private bool browsingVideo = false;        // estamos dentro de un disco de video
        private BackgroundWorker scanner;
        private double phase = 0;
        private Starfield formStars = new Starfield();   // cielo estrellado del fondo del formulario
        private int frameTick = 0;   // contador de cuadros: el fondo se refresca completo 1 de cada 3 (invisible al ojo)
        private int helpFlashPrev = 0;   // firma previa del brillo de la barra de ayuda (para el ultimo repintado que lo borra, en CPU)
        // ATENUACION al navegar con musica: mientras alguien usa los botones (para poner mas canciones) el
        // campo estrellado baja de brillo (evita agobiar con el movimiento/brillos); a los 10s sin botones
        // vuelve solo, con transicion suave. Solo atenua las ESTRELLAS (el cielo y los discos quedan igual).
        private float bgDim = 1f;
        private float bgDimPrev = 1f;              // valor del cuadro anterior: si cambia, se repinta TAMBIEN el fondo del form (franjas) para que atenue en sincronia con la rejilla
        private const float BgDimLevel = 0.45f;   // brillo del starfield mientras se navega (1 = normal)
        private const int BgDimIdleSecs = 10;     // segundos sin botones para volver al brillo normal
#if ROCOLA_GPU
        private GpuPanel gpuPanel;   // fondo de discos por GPU (experimental, Ctrl+F12)
        private Label lblGpu;        // etiqueta de estado/FPS del modo GPU (esquina)
#endif
        private bool searchOpen = false;
        private DateTime searchLastActivity = DateTime.Now;   // para auto-cerrar el buscador tras 1 min
        private string lastQuery = null;                      // busqueda incremental
        private List<SearchItem> lastMatches = null;          // coincidencias previas (para acotar el re-escaneo)
        private int credits = 0;                              // creditos disponibles (cada cosa cuesta 1)
        private const int CreditsPerCoin = 4;                 // monedas -> creditos
        // ACUMULACION de avisos: si repites la MISMA accion rapido (mientras su aviso sigue visible), el aviso
        // suma en vez de reiniciarse: monedas -> +4, +8, +12...; canciones -> "Cancion agregada", "... X2", "X3".
        private enum ToastKind { Other, Coin, Song }
        private ToastKind toastKind = ToastKind.Other; private int toastKindTick;
        private int coinTotal, songTotal;
        private void MarkToast(ToastKind k) { toastKind = k; toastKindTick = Environment.TickCount; }
        private bool SameToastActive(ToastKind k) { return toastKind == k && (Environment.TickCount - toastKindTick) < 1300; }   // 1300 = duracion del aviso

        // ---- BOTONES DE LA ROCOLA (configurables; se guardan en rocola_teclas.txt) ----
        // Valores por defecto (sirven para probar con teclado normal antes de configurar).
        private Keys keyUp = Keys.Up;       // pagina anterior / subir
        private Keys keyDown = Keys.Down;   // pagina siguiente / bajar
        private Keys keyLeft = Keys.Left;   // disco anterior
        private Keys keyRight = Keys.Right; // disco siguiente
        private Keys keyOK = Keys.Enter;    // aceptar / poner
        private Keys keyBack = Keys.Escape; // regresar
        private Keys keyCoin = Keys.Insert; // moneda / credito
        private Keys keySkip = Keys.ShiftKey; // saltar cancion
        // Asistente de configuracion
        private bool configMode = false;
        private bool needConfig = false;
        private int configStep = 0;
        private readonly Keys[] configCap = new Keys[8];
        private Label lblConfig;            // pantalla del asistente de configuracion
        private AttractView attractView;    // modo atraccion (reposo): fondo estrellado + cubo gigante
        private bool attractOn = false;
        private const int AttractIdleSecs = 40;   // reposo antes de entrar al modo atraccion
        private static readonly string[] ConfigSteps = new string[] {
            "ARRIBA   (subir / pagina anterior)",
            "ABAJO   (bajar / pagina siguiente)",
            "IZQUIERDA   (disco anterior)",
            "DERECHA   (disco siguiente)",
            "PONER / ACEPTAR   (elegir, poner cancion)",
            "REGRESAR   (atras / salir)",
            "MONEDA   (credito)",
            "SALTAR   (saltar la cancion actual)"
        };

        private Color bgAccent = DefaultAccent;
        private Color bgAccentTarget = DefaultAccent;

        // ---- Reproductor de video (Windows Media Player ActiveX, hosteado por codigo) ----
        private VideoHost video;
        private System.Windows.Forms.Timer videoPoll;
        private bool videoOn = false;              // hay un video cargado/reproduciendose
        // VOLUMEN DE LOS VIDEOS: el video lo reproduce Windows Media Player, cuyo sonido va DIRECTO a la
        // tarjeta sin pasar por la cadena de audio de la rocola (nivelado, limitador, EQ...). Por eso los
        // videos entraban a un volumen distinto al de las canciones ya niveladas. Esta perilla (Ctrl+F3) es
        // la BASE con la que suenan; ademas se le aplican los dB de "Subir TODO" para que sigan al resto.
        private int videoVol = 85;                 // 0..100, base del volumen de los videos ("video_vol=" en config)
        private int videoVolSet = -1;              // ultimo volumen puesto al WMP (para no repetir llamadas COM en cada sondeo)
        private double videoLastPos = -1;          // ultima posicion del video (para detectar si NO avanza)
        private int videoProgressTick = 0;         // momento del ultimo avance real (vigia de video colgado)
        private bool videoNudged = false;          // ya se intento el empujon de rescate (play explicito)
        private double videoMaxPos = 0;            // posicion maxima alcanzada (saber si el video SI se reprodujo)
        private bool videoFull = false;            // video en pantalla completa (tras inactividad)
        private DateTime lastInput = DateTime.Now; // ultima tecla (para detectar inactividad)
        private Label lblAttract;                  // aviso amarillo pulsante "toca una tecla"

        public Form1()
        {
            this.DoubleBuffered = true;
            formStars.Density = 1f;      // MISMA densidad que albumView: el borde entre secciones deja de notarse
            formStars.QuietBands = true;   // y ademas evitar acumularlas en el abecedario (arriba) y la barra de estado (abajo)
            try { if (timeBeginPeriod(1) == 0) timerResRaised = true; } catch { }   // reloj a 1ms -> animacion pareja
            try { int wt, ct; System.Threading.ThreadPool.GetMinThreads(out wt, out ct); System.Threading.ThreadPool.SetMinThreads(Math.Max(wt, 3), ct); } catch { }  // arranque de cargas mas agil
            this.KeyPreview = true;                 // el formulario ve KeyPress aunque otro control tenga el foco
            this.KeyPress += Form_KeyPress;         // admite TODO caracter en el buscador (ñ, acentos, comillas...)
            BuildUi();
            needConfig = !LoadKeys();   // si no hay botones guardados, lanzamos el asistente al mostrarse
            progressTimer = new System.Windows.Forms.Timer(); progressTimer.Interval = 500; progressTimer.Tick += ProgressTimer_Tick;
            coverSettleTimer = new System.Windows.Forms.Timer(); coverSettleTimer.Interval = 120; coverSettleTimer.Tick += CoverSettle_Tick;   // 0.12s: con SSD, casi inmediato pero sigue filtrando el hojeo
            animTimer = new System.Windows.Forms.Timer(); animTimer.Interval = 42; animTimer.Tick += AnimTimer_Tick; animTimer.Start();   // ~24fps (giros y pulsos); el fondo completo va 1 de cada 3 cuadros
            toastTimer = new System.Windows.Forms.Timer(); toastTimer.Interval = 1300; toastTimer.Tick += delegate (object s, EventArgs e) { toastTimer.Stop(); lblToast.Visible = false; if (searchOverlay != null) { searchOverlay.ToastOn = false; searchOverlay.Invalidate(); } };
            videoPoll = new System.Windows.Forms.Timer(); videoPoll.Interval = 400; videoPoll.Tick += VideoPoll_Tick;
            statsTimer = new System.Windows.Forms.Timer(); statsTimer.Interval = 60000; statsTimer.Tick += StatsTimer_Tick; statsTimer.Start();   // guarda estadisticas cada minuto si hubo cambios
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { if (statsDirty) { statsDirty = false; SaveStats(false); } } catch { }   // guardar estadisticas pendientes
            try { if (timerResRaised) { timeEndPeriod(1); timerResRaised = false; } } catch { }
            base.OnFormClosed(e);
        }

        // Al mostrarse: acomoda la UI, carga la musica, OCULTA EL CURSOR, fuerza el FOCO DEL TECLADO
        // (con un vigia durante el arranque) y, si hace falta, configura botones.
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadStats();   // estadisticas guardadas de sesiones anteriores (base de "Lo mas sonado")
            ProbeAudioTier();    // requisitos de maquina para el NIVEL 2 de audio (antes de leer dsee= de config)
            LoadConfigPrefs();   // preferencias: render GPU/CPU + carpetas de musica adicionales (Ctrl+F2)
            NeuralEnv.TryLoad(Path.Combine(Application.StartupPath, "rocola_ia.bin"));   // IA propia (si hay modelo entrenado junto al exe)
            LoadLoudCache();     // sonoridad medida por cancion (para nivelar el volumen de todas)
            LoadEqCache();       // huella tonal medida por cancion (para el EQ automatico)
            IaCountPairs();      // cuanto material de entrenamiento de la IA llevamos juntado
            LoadIaDone();        // de que canciones ya se saco material (no repetirlas en los repasos a fondo)
            LoadDeepDone();      // que canciones ya recibieron el repaso a fondo (para continuar donde iba)
            LoadDurFast();       // duraciones ya barridas (para mostrarlas desde el primer momento)
            LoadIncompatible();  // rutas ya anotadas como no reproducibles (para no repetirlas)
            BackupLearnedData(); // respalda lo aprendido de la sesion ANTERIOR antes de que esta lo modifique
            LayoutControls();
#if ROCOLA_GPU
            if (gpuPrefOn) ApplyAlbumGpu(true, false, false);   // render por GPU por defecto (o preferencia guardada); silencioso si el device falla; no re-guarda
#endif
            LoadMusicFolder();

            // SIN CURSOR: la rocola se usa con botones. Se oculta y se manda a una esquina
            // (en el autoarranque XP lo deja dibujado a media pantalla aunque no haya mouse).
            try
            {
                Cursor.Hide();
                Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Right - 1, Screen.PrimaryScreen.Bounds.Bottom - 1);
            }
            catch { }

            ForceFocus();
            // VIGIA DE FOCO: con el autoarranque tras un reinicio, el escritorio de Windows termina de
            // cargar DESPUES que la rocola y le roba el foco -> "no responde nada". Durante los primeros
            // 2 minutos el vigia lo re-toma solo cada segundo y medio; luego se apaga (asi, si conectas
            // teclado para dar mantenimiento, puedes salir de la rocola sin que te regrese a fuerzas).
            // VIGIA PERMANENTE: la rocola debe estar SIEMPRE al frente. Si cualquier ventana le roba el
            // foco (un dialogo de Windows, lo que sea), el vigia lo recupera; F10 (solo teclado de PC) lo
            // pausa 30 minutos para mantenimiento. Costo: una llamada ligera cada 1.5s, nada.
            maintMode = false;
            if (focusTimer == null)
            {
                focusTimer = new System.Windows.Forms.Timer();
                focusTimer.Interval = 1500;
                focusTimer.Tick += FocusTimer_Tick;
            }
            focusTimer.Start();

            if (needConfig) StartConfig();   // primera vez: configurar los botones de la rocola
        }

        // Trae la rocola al frente y le da el foco del teclado (robusto para el autoarranque).
        // Windows NIEGA el foco pedido por codigo desde una app de fondo (por eso "solo con mouse
        // funcionaba": el clic fisico si otorga el foco). Hacemos dos cosas:
        //  1) Truco AttachThreadInput: nos "pegamos" al hilo que tiene el foco para poder tomarlo.
        //  2) Si Windows aun se niega, generamos un CLIC DE MOUSE SINTETICO (mouse_event, no
        //     necesita mouse fisico) en la esquina inferior de NUESTRA ventana (zona sin botones):
        //     para Windows es identico a un clic real, y el foco se otorga siempre.
        private void ForceFocus()
        {
            try
            {
                if (GetForegroundWindow() == this.Handle) { this.Focus(); return; }

                IntPtr fg = GetForegroundWindow();
                uint fgT = (fg != IntPtr.Zero) ? GetWindowThreadProcessId(fg, IntPtr.Zero) : 0;
                uint myT = GetCurrentThreadId();
                bool attached = false;
                if (fgT != 0 && fgT != myT) { try { attached = AttachThreadInput(myT, fgT, true); } catch { } }

                this.TopMost = true;
                SetForegroundWindow(this.Handle);
                this.Activate();
                this.BringToFront();
                this.TopMost = false;
                SetFocus(this.Handle);
                this.Focus();

                if (attached) { try { AttachThreadInput(myT, fgT, false); } catch { } }

                // ¿Windows se siguio negando? Clic sintetico (lo mismo que hace un mouse real).
                if (GetForegroundWindow() != this.Handle)
                {
                    Rectangle sb = Screen.PrimaryScreen.Bounds;
                    Cursor.Position = new Point(sb.Right - 2, sb.Bottom - 2);   // esquina: solo etiquetas, nada que activar
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                }
            }
            catch { }
        }

        private void FocusTimer_Tick(object sender, EventArgs e)
        {
            if (maintMode && DateTime.Now > maintUntil) { maintMode = false; ShowToast("Vigia de pantalla reactivado"); }
            if (maintMode) return;                       // pausa de mantenimiento (F10)
            if (GetForegroundWindow() != this.Handle) ForceFocus();
        }

        // Entrada de texto del buscador: cualquier caracter imprimible (incluye Ñ, acentos, comillas) y Borrar.
        private void Form_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!searchOpen) return;
            char c = e.KeyChar;
            if (c == '\b') { searchOverlay.Backspace(); MarkSearchActivity(); e.Handled = true; return; }
            if (c == '\r' || c == '\n' || c == (char)27) { e.Handled = true; return; }  // Enter/Esc ya van en ProcessCmdKey
            if (!char.IsControl(c)) { searchOverlay.TypeChar(c); MarkSearchActivity(); e.Handled = true; }
        }

        private string ConfigFile { get { return Path.Combine(Application.StartupPath, "rocola_config.txt"); } }

        // ===================== PREFERENCIAS (rocola_config.txt) =====================
        // gpu=1/0 (render por GPU, Ctrl+F12) y dir=<ruta> (carpetas de musica ADICIONALES, Ctrl+F2; una por
        // linea, pueden estar en otros discos). Ambos savers escriben el archivo COMPLETO: nada se pisa.
        private bool gpuPrefOn = true;                                  // preferencia de render (GPU por defecto)
        // ANIMACION al entrar/salir de un disco: la portada hace zoom y se desvanece. Es SOLO un dibujo ENCIMA:
        // el cambio de pantalla sigue siendo instantaneo como siempre, asi que si la animacion se corta a media
        // (alguien pulsa algo, entra el modo atraccion, arranca un video...) no queda ningun estado a medias.
        // Necesita render por GPU: por CPU, escalar y mezclar una imagen grande por cuadro no da el ancho.
        // El AVANCE lo calcula el render (60 fps por Application.Idle), no el temporizador de 42 ms: asi el
        // zoom se ve fluido de verdad. El temporizador solo APAGA la animacion al cumplirse el tiempo.
#if ROCOLA_GPU
        private const int TransMs = 260;  // portada entrando/saliendo de un disco (o de la atraccion)
        private const int WinMs = 210;    // ventana del buscador abriendo/cerrando (mas corta: tipo macOS)
        private const float TransHold = 0.58f;   // hasta aqui la portada va opaca; despues se funde con el cubo de destino
        private int transMode = 0;        // 0 = nada, 1 = animacion de portada viva
        private int transTick = 0;        // TickCount del arranque
        private RectangleF transFrom;     // celda del cubo de ORIGEN (rejilla, disco, atraccion o buscador)
        private RectangleF transTo;       // celda del cubo de DESTINO
        private double transAng0;         // giro del cubo de origen (para arrancar calcado sobre el)
        private double transSpin;         // cuanto gira en total (camino corto al destino + media vuelta de gracia)
        private Image transImg;           // portada que se anima
        private RectangleF transNextFrom; // origen del PROXIMO EnterAlbum (se fija antes de cerrar el buscador)
        private double transNextAngle;    // y su giro
        private int winMode = 0;          // 0 = nada, 1 = abriendo el buscador, 2 = cerrandolo
        private int winTick = 0;
        private Bitmap winSnap;           // instantanea del buscador para animarlo AL CERRAR (al abrir se anima la textura de UI)
#endif
        private bool transAnimOn = true;  // "animacion=" en config
        private bool videoGpuOn = true;   // "video_gpu=" en config: interfaz por GPU tambien con video sonando
        private bool diagOn = false;      // "diagnostico=" en config: apuntar eventos clave en rocola_diagnostico.txt
        private readonly List<string> extraMusicDirs = new List<string>();   // carpetas de musica extra (ademas de "musica")

        // Bitacora de DIAGNOSTICO (diagnostico=1 en config): apunta los eventos clave (video, animaciones,
        // cambios de render) con hora en rocola_diagnostico.txt junto al exe. Para cazar a distancia lo que
        // pasa en el fierro real sin adivinar. Apagada no cuesta nada; el archivo se recorta solo a ~2 MB.
        private void Diag(string msg)
        {
            if (!diagOn) return;
            try
            {
                string f = Path.Combine(Application.StartupPath, "rocola_diagnostico.txt");
                try { FileInfo fi = new FileInfo(f); if (fi.Exists && fi.Length > 2097152) fi.Delete(); } catch { }
                File.AppendAllText(f, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\r\n");
            }
            catch { }
        }

        private void LoadConfigPrefs()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return;
                foreach (string line in File.ReadAllLines(ConfigFile))
                {
                    string s = line.Trim();
                    if (s.StartsWith("gpu=")) gpuPrefOn = s.Substring(4).Trim() != "0";
                    else if (s.StartsWith("animacion=")) transAnimOn = s.Substring(10).Trim() != "0";   // zoom al entrar/salir de un disco
                    else if (s.StartsWith("espectro=")) { float v; if (float.TryParse(s.Substring(9).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) auroraAmount = Clampf(v / 100f, 0f, 1f); }   // aurora del horizonte (0 = apagada)
                    else if (s.StartsWith("video_gpu=")) videoGpuOn = s.Substring(10).Trim() != "0";   // interfaz por GPU aunque haya video
                    else if (s.StartsWith("diagnostico=")) diagOn = s.Substring(12).Trim() != "0";   // bitacora en rocola_diagnostico.txt
                    else if (s.StartsWith("normalizar=")) normalizeOn = s.Substring(11).Trim() != "0";   // nivelar volumen de las canciones (por defecto si)
                    else if (s.StartsWith("respaldo_dir=")) { string d = s.Substring(13).Trim(); if (d.Length > 0) backupDir = d; }   // 2o destino del respaldo de datos aprendidos
                    else if (s.StartsWith("nivel_objetivo=")) { float v; if (float.TryParse(s.Substring(15).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) normTarget = Clampf(v, NormTargetMin, NormTargetMax); }
                    else if (s.StartsWith("nivel_fuerza=")) { float v; if (float.TryParse(s.Substring(13).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) normStrength = Clampf(v, 0f, 1f); }
                    else if (s.StartsWith("realce=")) { float v; if (float.TryParse(s.Substring(7).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) excAmount = Clampf(v, 0f, 1f); }   // realce de agudos (exciter, tipo DSEE)
                    else if (s.StartsWith("bajo=")) { float v; if (float.TryParse(s.Substring(5).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) bassAmount = Clampf(v, 0f, 1f); }   // bajo psicoacustico (armonicos del grave)
                    else if (s.StartsWith("subir_db=")) { float v; if (float.TryParse(s.Substring(9).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) masterDb = Clampf(v, 0f, 12f); }   // SUBIR TODO (dB extra general)
                    else if (s.StartsWith("fundido=")) { float v; if (float.TryParse(s.Substring(8).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) crossfadeSec = Clampf(v, 0f, 3f); }   // FUNDIDO entre canciones (segundos)
                    else if (s.StartsWith("declip=")) declipOn = s.Substring(7).Trim() != "0";   // des-recorte de picos aplastados (por defecto si)
                    else if (s.StartsWith("ia_datos_mb=")) { int v; if (int.TryParse(s.Substring(12).Trim(), out v)) iaMaxBytes = (v <= 0) ? 0L : (long)v * 1024 * 1024; }   // tope del archivo de material
                    else if (s.StartsWith("ia_datos=")) iaHarvestOn = s.Substring(9).Trim() != "0";   // juntar material para entrenar la IA
                    else if (s.StartsWith("video_vol=")) { int v; if (int.TryParse(s.Substring(10).Trim(), out v)) videoVol = v < 0 ? 0 : (v > 100 ? 100 : v); }   // volumen base de los videos
                    else if (s.StartsWith("eq_auto=")) { float v; if (float.TryParse(s.Substring(8).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) eqAmount = Clampf(v / 100f, 0f, 1f); }   // EQ automatico (fuerza % a mano)
                    else if (s.StartsWith("eq_automatico=")) eqAuto = s.Substring(14).Trim() != "0";     // la fuerza del EQ la decide el programa
                    else if (s.StartsWith("bajo_automatico=")) bassAuto = s.Substring(16).Trim() != "0"; // y la cantidad de bajo tambien
                    else if (s.StartsWith("dsee=")) sbrOn = s.Substring(5).Trim() == "1";   // reconstruccion espectral (NIVEL 2)
                    else if (s.StartsWith("dsee_forzar=")) { if (s.Substring(12).Trim() == "1") { sbrForce = true; sbrAllowed = true; sbrReason = ""; } }   // salta la puerta de requisitos (pruebas)
                    else if (s.StartsWith("dir="))
                    {
                        string d = s.Substring(4).Trim();
                        if (d.Length > 0 && !extraMusicDirs.Contains(d)) extraMusicDirs.Add(d);
                    }
                }
            }
            catch { }
            if (!sbrAllowed) sbrOn = false;   // la maquina no cumple los requisitos del NIVEL 2: apagado si o si
        }

        private static float Clampf(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private void SaveConfigPrefs()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("gpu=").Append(gpuPrefOn ? "1" : "0").Append("\r\n");
                sb.Append("animacion=").Append(transAnimOn ? "1" : "0").Append("\r\n");
                sb.Append("espectro=").Append(Math.Round(auroraAmount * 100f)).Append("\r\n");
                sb.Append("video_gpu=").Append(videoGpuOn ? "1" : "0").Append("\r\n");
                sb.Append("diagnostico=").Append(diagOn ? "1" : "0").Append("\r\n");
                sb.Append("normalizar=").Append(normalizeOn ? "1" : "0").Append("\r\n");
                if (!string.IsNullOrEmpty(backupDir)) sb.Append("respaldo_dir=").Append(backupDir).Append("\r\n");
                sb.Append("nivel_objetivo=").Append(normTarget.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("nivel_fuerza=").Append(normStrength.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("realce=").Append(excAmount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("bajo=").Append(bassAmount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("subir_db=").Append(masterDb.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("fundido=").Append(crossfadeSec.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("declip=").Append(declipOn ? "1" : "0").Append("\r\n");
                sb.Append("ia_datos=").Append(iaHarvestOn ? "1" : "0").Append("\r\n");
                sb.Append("ia_datos_mb=").Append(iaMaxBytes <= 0 ? 0L : iaMaxBytes / (1024 * 1024)).Append("\r\n");
                sb.Append("video_vol=").Append(videoVol).Append("\r\n");
                sb.Append("eq_auto=").Append(((int)Math.Round(eqAmount * 100f)).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
                sb.Append("eq_automatico=").Append(eqAuto ? "1" : "0").Append("\r\n");
                sb.Append("bajo_automatico=").Append(bassAuto ? "1" : "0").Append("\r\n");
                sb.Append("dsee=").Append(sbrOn ? "1" : "0").Append("\r\n");
                if (sbrForce) sb.Append("dsee_forzar=1\r\n");   // conservar la bandera manual de pruebas (este saver reescribe el archivo completo)
                for (int i = 0; i < extraMusicDirs.Count; i++) sb.Append("dir=").Append(extraMusicDirs[i]).Append("\r\n");
                File.WriteAllText(ConfigFile, sb.ToString());
            }
            catch { }
        }

        // ===================== NIVELADO DE VOLUMEN (rocola_volumen.txt) =====================
        // Una linea por cancion: "<rms>|<ruta>". El RMS integrado se mide al oirse la cancion y se guarda para que
        // la proxima vez suene al mismo nivel que las demas. Archivo chico (una linea por cancion oida).
        private string LoudFile() { return Path.Combine(Application.StartupPath, "rocola_volumen.txt"); }
        private string EqFile() { return Path.Combine(Application.StartupPath, "rocola_eq.txt"); }

        // Tabla de huellas tonales (d0;d1;...;d7|ruta). Perdida o rota no rompe nada: se re-aprende oyendo.
        private void LoadEqCache()
        {
            try
            {
                string f = EqFile();
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f))
                {
                    int bar = line.IndexOf('|');
                    if (bar <= 0 || bar >= line.Length - 1) continue;
                    string[] parts = line.Substring(0, bar).Split(';');
                    if (parts.Length < 8 || parts.Length > EqLen) continue;   // 8 y 9 son formatos viejos
                    float[] c = new float[EqLen]; bool ok = true;
                    for (int b = 0; b < parts.Length; b++)
                        if (!float.TryParse(parts[b], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out c[b])) { ok = false; break; }
                    for (int b = parts.Length; b < EqLen; b++) c[b] = 0f;   // ficha vieja: lo que falte queda "sin saber"
                    if (ok) { eqCache[Pool(line.Substring(bar + 1))] = c; eqCacheVer++; }
                }
            }
            catch { }
            RecomputeEqAvg();
        }

        private readonly object eqSaveLock = new object();
        private void SaveEqCache()
        {
            List<KeyValuePair<string, float[]>> snap;
            try { snap = new List<KeyValuePair<string, float[]>>(eqCache); }   // copia rapida en el hilo de UI
            catch { return; }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    StringBuilder sb = new StringBuilder(snap.Count * 72 + 16);
                    for (int i = 0; i < snap.Count; i++)
                    {
                        float[] c = snap[i].Value;
                        for (int b = 0; b < EqLen; b++)
                        { if (b > 0) sb.Append(';'); sb.Append(c[b].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)); }
                        sb.Append('|').Append(snap[i].Key).Append("\r\n");
                    }
                    lock (eqSaveLock) File.WriteAllText(EqFile(), sb.ToString());
                }
                catch { }
            });
        }

        // El OBJETIVO del EQ automatico. Antes era el promedio de TODA la biblioteca, y ahi estaba el problema:
        // si la mitad de tus canciones son MP3 flojos sin agudos, ese promedio viene apagado y el EQ estaba
        // empujando a TODAS hacia esa mediocridad, incluso a las que llegaron completas. Ahora el objetivo se
        // saca SOLO de las canciones sanas -las que conservan sus agudos, que es justo lo que el DSEE ya sabe
        // detectar-, asi que el objetivo pasa de ser "el promedio de lo que tengo" a "como suena lo que esta
        // bien grabado". Si no hay suficientes sanas (biblioteca entera de baja calidad) se usan todas, como antes.
        private const float EqSanaHz = 19300f;   // mismo umbral con el que el DSEE da una cancion por completa
        private int eqAvgSanas;                  // cuantas entraron en el promedio (se muestra en Ctrl+F3)
        private int eqCacheVer;                  // sube cada vez que cambia una huella: lo usan los recuentos cacheados
        private bool eqAvgSoloSanas;
        private void RecomputeEqAvg()
        {
            if (eqCache.Count < 10) { eqAvg = null; eqAvgSanas = 0; return; }
            float[] avg = new float[8]; int n = 0;
            foreach (KeyValuePair<string, float[]> kv in eqCache)
                if (kv.Value[EqCut] >= EqSanaHz) { for (int b = 0; b < 8; b++) avg[b] += kv.Value[b]; n++; }
            eqAvgSoloSanas = (n >= 10);
            if (!eqAvgSoloSanas)   // aun no hay bastantes sanas medidas: se usa todo, como se hacia antes
            {
                for (int b = 0; b < 8; b++) avg[b] = 0f;
                n = 0;
                foreach (KeyValuePair<string, float[]> kv in eqCache) { for (int b = 0; b < 8; b++) avg[b] += kv.Value[b]; n++; }
            }
            if (n < 10) { eqAvg = null; eqAvgSanas = 0; return; }
            for (int b = 0; b < 8; b++) avg[b] /= n;
            eqAvg = avg; eqAvgSanas = n;
        }

        // FUERZA del EQ cuando se deja en AUTOMATICO (de fabrica). No hay perilla que ajustar: la fuerza sale
        // de cuanto se puede uno FIAR del objetivo, que es lo unico que de verdad deberia decidirla.
        //   - Con pocas canciones medidas, el "sonido de la casa" todavia no significa nada: se corrige poco.
        //   - Segun el pre-analisis va midiendo la biblioteca, el objetivo es cada vez mas fiable y se corrige
        //     mas, hasta un tope que nunca llega al 100% (dejar un resto sin corregir conserva el caracter de
        //     cada disco: la idea es emparejar, no aplanar).
        //   - Con el modelo de IA entrenado el objetivo es POR CANCION en vez de uno para todas, asi que se
        //     puede afinar un poco mas sin miedo.
        // Menos de 10 canciones sanas medidas: cero. Ahi el EQ no toca nada, como si estuviera apagado.
        private float EqFuerzaAuto()
        {
            if (eqAvg == null || eqAvgSanas < 10) return 0f;
            float t = (eqAvgSanas - 10) / 50f;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float f = 0.35f + 0.50f * t;      // 35% con 10 canciones medidas -> 85% con 60 o mas
            if (!EqNet.Loaded) f *= 0.88f;    // sin modelo el objetivo es el promedio de TODAS: mas prudente
            return f;
        }
        // La huella tonal se mide siempre que haga falta para algo: en automatico hace falta SIEMPRE (de ella
        // salen tambien el bajo, el realce y el informe de calidad), y a mano solo si el EQ esta subido.
        private bool EqMideHuella { get { return eqAuto || eqAmount > 0.001f; } }

        // CUANTO bajo profundo le toca de partida a ESTA cancion cuando el bajo se deja en AUTOMATICO. Sale de
        // lo que el analisis del DSEE ya sabe del archivo, no de una perilla:
        //   - Si viene RECORTADA (masterizada a tope), meterle mas grave solo hace trabajar al limitador y
        //     emborrona: se le baja.
        //   - Si el DSEE le esta RECONSTRUYENDO los agudos, es una copia vieja y pobre; esas casi siempre estan
        //     tambien flojas de abajo y es justo donde el bajo psicoacustico mas se agradece: se le sube.
        // Encima de esto sigue actuando BassFactorFor, que mira cuanto grave trae de verdad frente al resto de
        // la biblioteca. Entre los dos, cada cancion recibe lo suyo sin que nadie toque nada.
        private float BajoAuto(string path)
        {
            float bas = 0.30f;   // punto de partida: el mismo que traia la perilla de fabrica
            float[] c = null;
            if (path != null) eqCache.TryGetValue(path, out c);
            if (c != null && c.Length >= EqLen)
            {
                if (c[EqClip] > 0.0015f) bas *= 0.75f;                          // picos aplastados: menos
                if (c[EqCut] > 100f && c[EqCut] < 16000f) bas *= 1.20f;         // agudos cortados: copia pobre, mas
            }
            return Clampf(bas, 0f, 1f);
        }

        // Correccion en dB por banda para una cancion (o null si apagado / aun sin huella / sin objetivo).
        // Correccion = (objetivo - huella) con tope +-4 dB, escalada por la fuerza. Una banda casi MUDA
        // (>25 dB bajo la referencia de su cancion) no se sube: seria inflar piso de ruido, no musica.
        private float[] EqCorrFor(string path)
        {
            float fuerza = eqAuto ? EqFuerzaAuto() : eqAmount;
            if (fuerza <= 0.001f || path == null || eqAvg == null) return null;
            float[] c;
            if (!eqCache.TryGetValue(path, out c)) return null;
            // Banda por ENCIMA del acantilado de esta cancion: NUNCA se corrige, y por dos razones distintas
            // segun el caso. Con el DSEE apagado, ahi el codec no dejo musica: subirla seria subir ruido. Y con
            // el DSEE encendido tampoco, porque la huella guardada describe el ARCHIVO (donde esa banda esta
            // vacia) mientras que lo que suena YA lleva los agudos re-creados: corregir con un numero viejo
            // seria empujar dos veces lo mismo.
            float corte = c[EqCut];
            // OBJETIVO POR CANCION. Para el cuerpo (158 Hz - 5.6 kHz) el objetivo sigue siendo el promedio de las
            // canciones sanas: es el "sonido de la casa". Pero para el grave y los agudos, si hay modelo entrenado,
            // el objetivo lo dice la IA mirando el cuerpo de ESTA cancion: cuanto grave y cuanto aire tendria una
            // cancion bien grabada que sonara como esta. Asi una balada de piano y un corrido dejan de ser
            // empujados hacia la misma curva. Sin modelo, todo sigue como antes (el promedio para las 8 bandas).
            float[] meta = eqAvg;
            if (EqNet.Loaded)
            {
                float[] ent = eqNetIn ?? (eqNetIn = new float[EqNetIn]);
                float[] sal = eqNetOut ?? (eqNetOut = new float[EqNetOut]);
                for (int i = 0; i < EqNetIn; i++) ent[i] = c[i + 1];   // el cuerpo: bandas 1..5
                if (EqNet.Predict(ent, sal))
                {
                    float[] m = eqMeta ?? (eqMeta = new float[8]);
                    for (int b = 0; b < 8; b++) m[b] = eqAvg[b];
                    for (int i = 0; i < EqNetOut; i++)
                    {
                        float v = sal[i];
                        // Cinturon de seguridad: por muy convencida que este la red, el objetivo no se aleja mas
                        // de 6 dB del promedio de la biblioteca. Un modelo mal entrenado no puede desmadrar el EQ.
                        float lim = eqAvg[EqNetSalida[i]];
                        if (v > lim + 6f) v = lim + 6f; else if (v < lim - 6f) v = lim - 6f;
                        m[EqNetSalida[i]] = v;
                    }
                    meta = m;
                }
            }
            // CANCION RARA. Si se aleja muchisimo del objetivo, casi seguro es que QUERIA sonar asi: un disco
            // muy oscuro a proposito, una grabacion en vivo, una voz sola. Empujarla a la fuerza hacia el resto
            // la estropea, asi que cuanto mas lejos esta de la biblioteca, con mas cuidado se le toca. Solo se
            // hace en automatico: si alguien puso la perilla a mano, manda la perilla.
            if (eqAuto)
            {
                float dif = 0f; int nb = 0;
                for (int b = 0; b < 8; b++)
                {
                    if (corte > 100f && EqCentros[b] > corte) continue;
                    if (c[b] <= -25f) continue;
                    float d = meta[b] - c[b]; if (d < 0f) d = -d;
                    dif += d; nb++;
                }
                if (nb > 0) fuerza *= Clampf(1.15f - (dif / nb) / 14f, 0.45f, 1f);
            }
            float[] dbs = new float[8]; bool any = false;
            for (int b = 0; b < 8; b++)
            {
                float corr = 0f;
                if (corte > 100f && EqCentros[b] > corte) { dbs[b] = 0f; continue; }
                if (c[b] > -25f)
                {
                    corr = meta[b] - c[b];
                    if (corr > 4f) corr = 4f; else if (corr < -4f) corr = -4f;
                    corr *= fuerza;
                }
                dbs[b] = corr;
                if (corr > 0.1f || corr < -0.1f) any = true;
            }
            return any ? dbs : null;
        }

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

        // CUANTO realce de brillo le toca a ESTA cancion. El EQ no puede devolver una banda que el codec borro
        // -ahi no hay musica que subir-, pero el realce SI: fabrica armonicos a partir de lo que aun queda, asi
        // que cae justo donde el EQ no llega. Por eso una cancion dañada recibe mas realce y una completa menos:
        // esa ya trae su aire de verdad y no hace falta inventarselo.
        private float ExciteFactorFor(string path)
        {
            float[] c;
            if (path == null || !eqCache.TryGetValue(path, out c) || c == null || c.Length < EqLen) return 1f;
            float corte = c[EqCut];
            if (corte <= 100f) return 1f;   // aun sin medir: como siempre
            return Clampf(1.45f - (corte - 15000f) / 6000f, 0.75f, 1.45f);
        }
        private float ExciteAmountFor(string path) { return Clampf(excAmount * ExciteFactorFor(path), 0f, 1f); }

        // ¿Vale la pena poner el des-recorte en esta cancion? Solo si de verdad viene con picos aplastados. Una
        // cancion aun sin medir se trata como sospechosa (mas vale ponerlo). El umbral es bajo a proposito: con
        // una de cada diez mil muestras recortadas ya se oye la aspereza en un pasaje fuerte.
        private bool DeclipVale(string path)
        {
            float[] c;
            if (path == null || !eqCache.TryGetValue(path, out c) || c == null || c.Length < EqLen) return true;
            if (c[EqCut] <= 100f) return true;   // ficha vieja o sin analizar: no se sabe, se pone
            return c[EqClip] > 0.0001f;
        }

        // ---- CALIDAD DE LA BIBLIOTECA (lo que el analisis ya sabe y antes no se veia) ----
        // Reparte las canciones medidas en tres cajones segun donde el codec les corto los agudos, y cuenta
        // aparte las que vienen recortadas de masterizacion. Sirve para saber que vale la pena volver a
        // conseguir en buena calidad.
        private const float CalBuenaHz = 19300f, CalMediaHz = 16500f;
        // Se pide DOS VECES POR SEGUNDO mientras la ventana de ajustes esta abierta, y recorrer la biblioteca
        // entera (decenas de miles de canciones) para volver a contar lo mismo es puro desperdicio: justo en ese
        // rato la maquina suele estar ademas con el entrenamiento a fondo. Se guarda el recuento y solo se
        // rehace cuando la huella de alguna cancion ha cambiado de verdad (ver eqCacheVer).
        // Ademas de la version se guarda CUANTAS habia: asi el recuento tampoco se queda viejo si alguien
        // cambiara la coleccion entera de golpe en vez de ir anadiendo huellas una a una.
        private int calVer = -1, calCuantas = -1, calBuenas, calMedias, calPobres, calRecort, calSinMedir;
        private void CalidadResumen(out int buenas, out int medias, out int pobres, out int recortadas, out int sinMedir)
        {
            if (calVer == eqCacheVer && calCuantas == eqCache.Count)
            {
                buenas = calBuenas; medias = calMedias; pobres = calPobres; recortadas = calRecort; sinMedir = calSinMedir;
                return;
            }
            CuentaCalidad(out buenas, out medias, out pobres, out recortadas, out sinMedir);
            calVer = eqCacheVer; calCuantas = eqCache.Count;
            calBuenas = buenas; calMedias = medias; calPobres = pobres; calRecort = recortadas; calSinMedir = sinMedir;
        }
        private void CuentaCalidad(out int buenas, out int medias, out int pobres, out int recortadas, out int sinMedir)
        {
            buenas = medias = pobres = recortadas = sinMedir = 0;
            foreach (KeyValuePair<string, float[]> kv in eqCache)
            {
                float[] c = kv.Value;
                if (c == null || c.Length < EqLen || c[EqCut] <= 100f) { sinMedir++; continue; }
                if (c[EqCut] >= CalBuenaHz) buenas++;
                else if (c[EqCut] >= CalMediaHz) medias++;
                else pobres++;
                if (c[EqClip] > 0.001f) recortadas++;
            }
        }

        // Escribe el informe: las peores primero, para ir directo a lo que conviene reemplazar.
        private string CalidadFile() { return Path.Combine(Application.StartupPath, "rocola_calidad.txt"); }
        // Abrir y cerrar la ventana varias veces seguidas encargaba varios informes a la vez y podian
        // chocar escribiendo el MISMO archivo (y el informe se quedaba sin escribir). Uno por vez.
        private static readonly object calSaveLock = new object();
        // El informe se escribe al abrir Ctrl+F3. Con una biblioteca grande, ordenar decenas de miles de
        // canciones y armar un archivo de varios megas es medio segundo de trabajo: hacerlo en el hilo de la
        // interfaz CONGELA la pantalla justo al abrir la ventana. Aqui solo se saca la lista (una pasada barata)
        // y el resto -ordenar, armar el texto y escribir el archivo- se hace en otro hilo.
        private void EscribeInformeCalidad()
        {
            try
            {
                List<KeyValuePair<string, float[]>> lista = new List<KeyValuePair<string, float[]>>();
                foreach (KeyValuePair<string, float[]> kv in eqCache)
                    if (kv.Value != null && kv.Value.Length >= EqLen && kv.Value[EqCut] > 100f) lista.Add(kv);
                if (lista.Count == 0) return;
                int buenas, medias, pobres, recortadas, sinMedir;
                CalidadResumen(out buenas, out medias, out pobres, out recortadas, out sinMedir);
                string destino = CalidadFile();
                System.Threading.ThreadPool.QueueUserWorkItem(delegate { InformeCalidadFondo(lista, destino, buenas, medias, pobres, recortadas, sinMedir); });
            }
            catch { }
        }
        private static void InformeCalidadFondo(List<KeyValuePair<string, float[]>> lista, string destino,
                                                int buenas, int medias, int pobres, int recortadas, int sinMedir)
        {
            try
            {
                lista.Sort(delegate (KeyValuePair<string, float[]> x, KeyValuePair<string, float[]> y)
                { return x.Value[EqCut].CompareTo(y.Value[EqCut]); });
                StringBuilder sb = new StringBuilder(lista.Count * 80 + 512);
                sb.Append("CALIDAD DE LA BIBLIOTECA  -  ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\r\n");
                sb.Append("Las peores van primero: son las que mas conviene volver a conseguir en buena calidad.\r\n");
                sb.Append("\r\n  COMPLETAS (WAV o 320k): ").Append(buenas.ToString("#,0"));
                sb.Append("\r\n  ACEPTABLES (~192-256k): ").Append(medias.ToString("#,0"));
                sb.Append("\r\n  POBRES (128k o menos):  ").Append(pobres.ToString("#,0"));
                sb.Append("\r\n  Con picos recortados de origen: ").Append(recortadas.ToString("#,0"));
                sb.Append("\r\n  Aun sin medir: ").Append(sinMedir.ToString("#,0"));
                sb.Append("\r\n\r\ncorte   recorte  archivo\r\n");
                for (int i = 0; i < lista.Count; i++)
                {
                    float[] c = lista[i].Value;
                    sb.Append((c[EqCut] / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" kHz  ");
                    sb.Append((c[EqClip] * 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("%   ");
                    sb.Append(lista[i].Key).Append("\r\n");
                }
                lock (calSaveLock) File.WriteAllText(destino, sb.ToString());
            }
            catch { }
        }

        // Cierra la medicion de huella tonal de la cancion que acaba de sonar (audio ya detenido).
        private void FinalizeEqMeasure()
        {
            string path = eqMeasPath; eqMeasPath = null;
            double[] sums = eqMeasSum; long n = eqMeasN;
            eqMeasSum = new double[8]; eqMeasN = 0;
            if (path == null || n < EqMinFrames) return;
            float[] curve = new float[EqLen];
            for (int b = 0; b < 8; b++) curve[b] = (float)(10.0 * Math.Log10(sums[b] / n + 1e-12));
            // referencia = MEDIANA de las bandas del CUERPO (158 Hz - 5.6 kHz): invariante al volumen y
            // ademas inmune a una banda desbocada (un lodo de +8 dB no arrastra la referencia consigo).
            float[] cuerpo = { curve[1], curve[2], curve[3], curve[4], curve[5] };
            Array.Sort(cuerpo);
            float refDb = cuerpo[2];
            for (int b = 0; b < 8; b++) curve[b] -= refDb;
            float[] prev; bool had = eqCache.TryGetValue(path, out prev);
            if (had)
            {
                float maxDelta = 0f;
                for (int b = 0; b < 8; b++) { curve[b] = prev[b] * 0.5f + curve[b] * 0.5f; float d = Math.Abs(curve[b] - prev[b]); if (d > maxDelta) maxDelta = d; }
                if (maxDelta < 0.2f && prev[EqCut] > 0f) return;   // ya aprendida y estable: no reescribir el archivo (cero churn)
            }
            // CORTE: lo mide el propio DSEE mientras la cancion suena, este encendido o no (el acantilado es del
            // ARCHIVO, no del efecto). Si no lo supimos esta vez, se conserva el que ya estuviera guardado.
            curve[EqCut] = (eqMeasCut > 0f) ? eqMeasCut : (had ? prev[EqCut] : 0f);
            curve[EqCutLo] = (eqMeasCutLo > 0f) ? eqMeasCutLo : (had ? prev[EqCutLo] : 0f);
            curve[EqClip] = had ? prev[EqClip] : 0f;   // el recorte solo lo mide el pre-analisis (aqui ya viene reparado)
            eqCache[path] = curve; eqCacheVer++;
            RecomputeEqAvg();
            SaveEqCache();
        }

        private void LoadLoudCache()
        {
            try
            {
                string f = LoudFile();
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f))
                {
                    int bar = line.IndexOf('|');
                    if (bar <= 0 || bar >= line.Length - 1) continue;
                    float rms;
                    if (!float.TryParse(line.Substring(0, bar), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out rms)
                        || rms <= 0.0001f) continue;
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
                    loudCache[rest] = rms;
                    if (dur > 0.01f) durCache[rest] = dur;
                }
            }
            catch { }
        }

        // Guarda la tabla de sonoridades. Para no frenar el cambio de cancion en bibliotecas GRANDES (el archivo
        // puede tener miles de lineas), se toma una COPIA rapida en el hilo de la interfaz y el volcado a disco se
        // hace en SEGUNDO PLANO. Un candado asegura un solo escritor a la vez. Si algo truena, se ignora (la medida
        // se vuelve a aprender sola). Un archivo perdido/roto no rompe nada: se reconstruye oyendo las canciones.
        private readonly object loudSaveLock = new object();
        private void SaveLoudCache()
        {
            List<KeyValuePair<string, float>> snap;
            Dictionary<string, float> durSnap;
            try { snap = new List<KeyValuePair<string, float>>(loudCache); durSnap = new Dictionary<string, float>(durCache, StringComparer.OrdinalIgnoreCase); }   // copia (rapida): las tablas solo se tocan aqui
            catch { return; }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    StringBuilder sb = new StringBuilder(snap.Count * 48 + 16);
                    for (int i = 0; i < snap.Count; i++)
                    {
                        float du; if (!durSnap.TryGetValue(snap[i].Key, out du)) du = 0f;
                        sb.Append(snap[i].Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                          .Append(du.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                          .Append(snap[i].Key).Append("\r\n");
                    }
                    lock (loudSaveLock) File.WriteAllText(LoudFile(), sb.ToString());   // el disco, fuera del hilo de la interfaz (no frena la musica)
                }
                catch { }
            });
        }

        // ESCALERA DE NIVELADO (etapas por que tan bajita viene la cancion). Antes habia UN solo tope de subida
        // (+15 dB) para todas; ahora cada etapa tiene el SUYO: una cancion ULTRA BAJA puede subir mucho mas (+20 dB)
        // para alcanzar al resto, mientras que una que ya viene FUERTE apenas se toca (no necesita ayuda y subirla
        // solo gastaria margen). Umbrales en dBFS de la sonoridad medida (K-weighted), de mas bajita a mas fuerte.
        private static readonly float[] TierDb = { -34f, -28f, -23f, -19f, -15f, -11f };        // puntos de la escalera (dBFS)
        // Tope de subida (dB) en cada punto. Las canciones BAJITAS se suben mas que antes (una grabacion vieja
        // y floja se quedaba corta y se oia menos que el resto); de la mitad de la escalera para arriba casi no
        // cambia nada, porque esas ya llegaban de sobra. Antes: 20/16/13/10/7/4.
        private static readonly float[] TierMaxDb = { 24f, 19f, 14.5f, 10.8f, 7.4f, 4.3f };
        private static readonly string[] TierName = { "ULTRA BAJA", "BAJA", "MEDIA-BAJA", "MEDIA", "ALTA", "MUY ALTA" };
        // Etapa (0..5) a la que pertenece una sonoridad lineal medida (solo para NOMBRARLA).
        private static int LoudTier(float rms)
        {
            if (rms <= 0.0001f) return 0;   // practicamente muda: la etapa mas baja
            float db = (float)(20.0 * Math.Log10(rms));
            for (int i = 0; i < TierDb.Length - 1; i++) if (db <= TierDb[i]) return i;
            return TierDb.Length - 1;   // por encima del ultimo punto = MUY ALTA
        }
        // Tope de subida (dB) para una sonoridad dada. Entre punto y punto de la escalera se INTERPOLA en vez
        // de saltar de golpe: con topes en escalon, una cancion apenas mas fuerte que una frontera terminaba
        // MAS BAJITA que una mas callada (un salto audible justo en la frontera). Interpolando, el resultado
        // sube siempre de forma ordenada: mas fuerte de origen nunca acaba sonando mas bajo.
        private static float TierCapDb(float rms)
        {
            if (rms <= 0.0001f) return TierMaxDb[0];
            float db = (float)(20.0 * Math.Log10(rms));
            if (db <= TierDb[0]) return TierMaxDb[0];
            for (int i = 0; i < TierDb.Length - 1; i++)
            {
                if (db <= TierDb[i + 1])
                {
                    float f = (db - TierDb[i]) / (TierDb[i + 1] - TierDb[i]);
                    return TierMaxDb[i] + f * (TierMaxDb[i + 1] - TierMaxDb[i]);
                }
            }
            return TierMaxDb[TierMaxDb.Length - 1];
        }
        // Nombre de la etapa de una cancion ya medida ("" si aun no se ha medido).
        private string LoudTierName(string path)
        {
            float rms;
            if (path == null || !loudCache.TryGetValue(path, out rms) || rms <= 0.0001f) return "";
            return TierName[LoudTier(rms)];
        }

        // Ganancia de nivelado para una cancion, segun su sonoridad medida (si ya se oyo alguna vez). 1 = sin cambio.
        private float NormGainFor(string path)
        {
            if (!normalizeOn || normStrength <= 0.001f || path == null) return 1f;
            float rms;
            if (!loudCache.TryGetValue(path, out rms) || rms <= 0.0001f) return 1f;   // aun no medida: va a su volumen original y se aprende en esta pasada
            // "Subir el piso": si la cancion suena mas BAJITO que el objetivo, se SUBE (segun la fuerza). Si ya suena
            // igual o mas FUERTE que el objetivo, se deja igual (nunca se baja). El tope de subida es el de SU ETAPA.
            float g = (float)Math.Pow(normTarget / rms, normStrength);
            float cap = (float)Math.Pow(10.0, TierCapDb(rms) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;   // NormMaxBoost sigue siendo el techo absoluto de seguridad
            if (g < 1f) g = 1f; else if (g > cap) g = cap;
            return g;
        }

        // Convierte entre el volumen objetivo (RMS lineal) y el porcentaje 0..100 que muestra la interfaz.
        private int TargetToPct(float t) { int p = (int)Math.Round((t - NormTargetMin) / (NormTargetMax - NormTargetMin) * 100f); return p < 0 ? 0 : (p > 100 ? 100 : p); }
        private float TargetFromPct(int p) { return NormTargetMin + (p / 100f) * (NormTargetMax - NormTargetMin); }

        // Aplica el nivelado a la cancion que suena AHORA (para oir el cambio al instante mientras se ajusta en Ctrl+F3).
        private void ApplyNormNow()
        {
            LevelTap t = curTap; if (t != null && nowPlaying != null && nowPlaying.Song != null) { t.Gain = NormGainFor(nowPlaying.Song.Path); t.Excite = ExciteAmountFor(nowPlaying.Song.Path); t.Master = MasterLin(); t.EqDb = EqCorrFor(nowPlaying.Song.Path); }
            DeepBassProvider bp = curBass; if (bp != null) bp.Amount = BassAmountNow();   // bajo psicoacustico: al instante, ya ajustado a la cancion
            SpectralTap sp = curSbr; if (sp != null) sp.Active = sbrOn;   // reconstruccion espectral: alterna EN VIVO (A/B desde Ctrl+F3)
        }

        // Cierra la medida de la cancion que acaba de sonar: calcula su RMS integrado y lo guarda (si se oyo lo
        // suficiente). La llama DisposePlaybackNow, ya con el audio detenido (sin carrera con el hilo de sonido).
        private void FinalizeLoudMeasure()
        {
            string path = curMeasurePath; curMeasurePath = null;
            double sumSq = curSumSq; long samples = curSamples; curSumSq = 0; curSamples = 0;
            if (path == null || samples < NormMinSamples) return;
            float rms = (float)Math.Sqrt(sumSq / samples);
            if (rms <= 0.0001f) return;
            float prev; bool had = loudCache.TryGetValue(path, out prev);
            float val = had ? (prev * 0.5f + rms * 0.5f) : rms;   // promedia con la medida anterior: se estabiliza si la cancion se salto en distinto punto
            // Si ya estaba medida y el valor casi no cambia, NO reescribir el archivo: en una biblioteca ya
            // "aprendida" el guardado deja de dispararse en cada cancion (menos escrituras a disco, cero churn).
            if (had && Math.Abs(val - prev) < 0.004f) return;
            loudCache[path] = val;
            SaveLoudCache();
        }

        // Todas las raices de musica que EXISTEN ahora: la carpeta "musica" principal + las agregadas con Ctrl+F2.
        // Una carpeta esta DENTRO de otra (o es la misma). Compara por tramos completos, asi "musica2" NO
        // cuenta como dentro de "musica".
        private static bool PathInside(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string x = a.TrimEnd('\\', '/'), y = b.TrimEnd('\\', '/');
            if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase)) return true;
            return x.Length > y.Length + 1
                && x.StartsWith(y, StringComparison.OrdinalIgnoreCase)
                && (x[y.Length] == '\\' || x[y.Length] == '/');
        }

        private List<string> AllMusicRoots()
        {
            List<string> roots = new List<string>();
            try { if (Directory.Exists(MusicFolder)) roots.Add(MusicFolder); } catch { }
            for (int i = 0; i < extraMusicDirs.Count; i++)
            {
                string d = extraMusicDirs[i];
                // Se descarta no solo la carpeta REPETIDA, sino la que ANIDA con otra ya incluida (una dentro
                // de la otra, en cualquier sentido): si no, todo lo que compartieran se escanearia DOS veces y
                // cada cancion saldria duplicada en las listas, el buscador y las estadisticas.
                bool dup = false;
                for (int j = 0; j < roots.Count && !dup; j++)
                    if (PathInside(d, roots[j]) || PathInside(roots[j], d)) dup = true;
                try { if (!dup && Directory.Exists(d)) roots.Add(d); } catch { }
            }
            return roots;
        }

        // Ctrl+F2: GESTOR VISUAL de carpetas de musica. Lista la principal ("musica", fija) y las ADICIONALES
        // (pueden estar en otros discos, SSD/HDD); permite AGREGAR y QUITAR. Los cambios se guardan al cerrar
        // y disparan UN solo re-escaneo. Es una herramienta de mantenimiento (requiere teclado/mouse): el
        // cursor se muestra solo mientras el gestor esta abierto y el vigia de foco queda en pausa.
        private void ShowMusicDirManager()
        {
            if (searchOpen) CloseSearch();
            bool wasMaint = maintMode;
            maintMode = true; maintUntil = DateTime.Now.AddMinutes(10);   // pausa el vigia de foco: el dialogo modal necesita quedarse al frente
            bool changed = false;
            try
            {
                Cursor.Show();
                using (Font fUi = new Font(FontFamily.GenericSansSerif, 10f))
                using (Form dlg = new Form())
                {
                    dlg.Text = "Rocola - Carpetas de musica";
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MaximizeBox = false; dlg.MinimizeBox = false; dlg.ShowInTaskbar = false; dlg.ShowIcon = false;
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ClientSize = new Size(640, 372);
                    dlg.BackColor = Color.FromArgb(14, 18, 42);
                    dlg.Font = fUi;

                    Label info = new Label();
                    info.SetBounds(14, 10, 612, 42);
                    info.BackColor = Color.Transparent;
                    info.ForeColor = Color.FromArgb(205, 214, 238);
                    info.Text = "La carpeta 'musica' (junto al programa) siempre se incluye. Aqui puedes agregar o quitar carpetas ADICIONALES, incluso de otros discos (SSD/HDD).";

                    ListBox list = new ListBox();
                    list.SetBounds(14, 58, 612, 230);
                    list.BackColor = Color.FromArgb(24, 30, 62);
                    list.ForeColor = Color.White;
                    list.BorderStyle = BorderStyle.FixedSingle;
                    list.IntegralHeight = false;

                    Button bAdd = new Button(), bDel = new Button(), bOk = new Button();
                    bAdd.Text = "Agregar carpeta..."; bAdd.SetBounds(14, 300, 180, 36);
                    bDel.Text = "Quitar seleccionada"; bDel.SetBounds(202, 300, 180, 36);
                    bOk.Text = "Listo"; bOk.SetBounds(530, 300, 96, 36);
                    Button[] bs = { bAdd, bDel, bOk };
                    for (int i = 0; i < bs.Length; i++)
                    {
                        bs[i].FlatStyle = FlatStyle.Flat;
                        bs[i].FlatAppearance.BorderColor = Color.FromArgb(90, 110, 170);
                        bs[i].BackColor = Color.FromArgb(34, 44, 86);
                        bs[i].ForeColor = Color.White;
                    }

                    // Rellena la lista: [0] = principal (fija); debajo las adicionales, marcando las no disponibles
                    // (disco desconectado): esas siguen guardadas y vuelven solas cuando el disco reaparezca.
                    Action refresh = delegate
                    {
                        list.Items.Clear();
                        string main = MusicFolder;
                        bool mainOk = false; try { mainOk = Directory.Exists(main); } catch { }
                        list.Items.Add(main + "   [principal - fija]" + (mainOk ? "" : "   (NO EXISTE)"));
                        for (int i = 0; i < extraMusicDirs.Count; i++)
                        {
                            bool ok = false; try { ok = Directory.Exists(extraMusicDirs[i]); } catch { }
                            list.Items.Add(extraMusicDirs[i] + (ok ? "" : "   (no disponible)"));
                        }
                        bDel.Enabled = extraMusicDirs.Count > 0;
                    };

                    bAdd.Click += delegate
                    {
                        using (FolderBrowserDialog fb = new FolderBrowserDialog())
                        {
                            fb.Description = "Elige una carpeta ADICIONAL con musica (puede estar en otro disco).";
                            fb.ShowNewFolderButton = false;
                            if (fb.ShowDialog(dlg) != DialogResult.OK || string.IsNullOrEmpty(fb.SelectedPath)) return;
                            string p = fb.SelectedPath;
                            // Tambien se rechaza si ANIDA con una ya incluida (una dentro de otra): esa musica
                            // se escanearia dos veces y saldria duplicada por todas partes.
                            bool dup = PathInside(p, MusicFolder) || PathInside(MusicFolder, p);
                            for (int i = 0; i < extraMusicDirs.Count && !dup; i++)
                                if (PathInside(p, extraMusicDirs[i]) || PathInside(extraMusicDirs[i], p)) dup = true;
                            if (dup) { MessageBox.Show(dlg, "Esa carpeta ya esta incluida (o esta dentro de otra que ya lo esta).", "Rocola"); return; }
                            extraMusicDirs.Add(p); changed = true; refresh();
                        }
                    };
                    bDel.Click += delegate
                    {
                        int i = list.SelectedIndex;
                        if (i < 0) { MessageBox.Show(dlg, "Selecciona una carpeta de la lista.", "Rocola"); return; }
                        if (i == 0) { MessageBox.Show(dlg, "La carpeta principal 'musica' no se puede quitar.", "Rocola"); return; }
                        extraMusicDirs.RemoveAt(i - 1); changed = true; refresh();
                    };
                    bOk.Click += delegate { dlg.Close(); };
                    dlg.CancelButton = bOk;   // Esc tambien cierra (los cambios ya hechos se aplican igual)

                    dlg.Controls.Add(info); dlg.Controls.Add(list);
                    dlg.Controls.Add(bAdd); dlg.Controls.Add(bDel); dlg.Controls.Add(bOk);
                    refresh();
                    dlg.ShowDialog(this);
                }
                if (changed)
                {
                    SaveConfigPrefs();
                    ShowToast("Carpetas actualizadas: escaneando musica...");
                    try { File.Delete(LibCacheFile()); } catch { }   // el cache de biblioteca ya no corresponde
                    LoadMusicFolder();
                }
            }
            catch { }
            finally { try { Cursor.Hide(); } catch { } maintMode = wasMaint; ForceFocus(); }
        }

        // Ctrl+F3: ajuste VISUAL del sonido. Tres perillas: "volumen objetivo" (que tan fuerte quedan todas),
        // "fuerza" (cuanto se suben las bajitas) y "realce de brillo" (exciter tipo DSEE: re-crea el "aire" que el
        // MP3 recorta). Filosofia del nivelado: subir las flojas, NO bajar las que ya suenan fuerte. Todo se oye
        // al instante en la cancion actual y aplica a las siguientes.
        // ======================= PIEZAS DE LA VENTANA DE SONIDO (Ctrl+F3) =======================
        // Windows XP dibuja sus barras, casillas y botones como en 1995: cuadrados, grises y con el marco
        // hundido. En una rocola que por fuera es un cielo estrellado eso desentona muchisimo, asi que estas
        // piezas se pintan solas. Por dentro son controles normales de Windows (raton, teclado y foco funcionan
        // igual); solo cambia el dibujo: esquinas redondeadas, un color de acento por ajuste y nada de marcos.

        // La familia de letra de la interfaz: se prueban por orden y se usa la primera que exista. En Windows XP
        // no hay Segoe UI, asi que ahi cae en Tahoma (la del sistema) y en equipos nuevos en Segoe UI.
        // Se busca UNA sola vez y se guarda. Antes se creaba una familia de letras nueva cada vez que se
        // abria la ventana de sonido, y una familia es un recurso del sistema que hay que devolver: como no
        // se devolvia, cada apertura dejaba uno colgado. Guardarla evita ademas la trampa contraria: la
        // familia de reserva (GenericSansSerif) es de Windows y NO se debe devolver nunca.
        private static FontFamily familiaUi;
        private static FontFamily FamiliaUi()
        {
            if (familiaUi != null) return familiaUi;
            string[] pref = { "Segoe UI", "Tahoma", "Verdana" };
            for (int i = 0; i < pref.Length; i++)
                try { familiaUi = new FontFamily(pref[i]); return familiaUi; } catch { }
            familiaUi = FontFamily.GenericSansSerif;
            return familiaUi;
        }

        // Arma una fila de ajuste dentro de una tarjeta. Es un delegado propio porque en .NET 3.5 los Action
        // de la biblioteca solo llegan a cuatro argumentos.
        private delegate void FilaAjuste(int k, Tarjeta card, int y, string titulo, Color ac, int min, int max, bool conAuto);

        // TARJETA: el recuadro redondeado que agrupa unos cuantos ajustes bajo un titulo.
        private sealed class Tarjeta : Panel
        {
            public string Titulo = "";
            public Color Acento = Color.FromArgb(120, 200, 255);
            private Font fTitulo;
            protected override void Dispose(bool d) { if (d && fTitulo != null) { fTitulo.Dispose(); fTitulo = null; } base.Dispose(d); }
            public Tarjeta()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.Transparent;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath pa = RoundedPath(r, 12))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 28, 56)))
                using (Pen pn = new Pen(Color.FromArgb(46, 58, 104)))
                { g.FillPath(b, pa); g.DrawPath(pn, pa); }
                if (!string.IsNullOrEmpty(Titulo))
                {
                    using (SolidBrush ac = new SolidBrush(Acento))
                    using (GraphicsPath ba = RoundedPath(new Rectangle(16, 15, 4, 14), 2))
                        g.FillPath(ac, ba);
                    if (fTitulo == null) fTitulo = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);   // una vez, no en cada repintado
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(226, 234, 255)))
                        g.DrawString(Titulo, fTitulo, tb, 28, 12);
                }
            }
        }

        // DESLIZADOR: la barra de un ajuste. Riel redondeado, la parte ya recorrida en el color del ajuste y un
        // tirador redondo. Responde al raton (arrastrar o pulsar en cualquier punto) y al teclado (flechas de
        // una en una, AvPag/RePag de diez en diez, Inicio/Fin a los extremos).
        private sealed class Deslizador : Control
        {
            public int Minimum = 0, Maximum = 100;
            public Color Acento = Color.FromArgb(120, 200, 255);
            public event EventHandler Scroll;
            private int val;
            private bool arrastra;
            public Deslizador()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                TabStop = true;
                Height = 22;
                Cursor = Cursors.Hand;
            }
            public int Value
            {
                get { return val; }
                set { int v = Acota(value); if (v != val) { val = v; Invalidate(); } }
            }
            private int Acota(int v) { return v < Minimum ? Minimum : (v > Maximum ? Maximum : v); }
            private const int Radio = 9;
            private int DeX(int x)
            {
                int w = Width - Radio * 2; if (w < 1) w = 1;
                float f = (x - Radio) / (float)w;
                if (f < 0f) f = 0f; else if (f > 1f) f = 1f;
                return Minimum + (int)(f * (Maximum - Minimum) + 0.5f);
            }
            private void Pon(int v)
            {
                v = Acota(v);
                if (v == val) return;
                val = v; Invalidate();
                if (Scroll != null) Scroll(this, EventArgs.Empty);
            }
            protected override void OnMouseDown(MouseEventArgs e)
            { if (Enabled) { Focus(); arrastra = true; Pon(DeX(e.X)); } base.OnMouseDown(e); }
            protected override void OnMouseMove(MouseEventArgs e)
            { if (arrastra && Enabled) Pon(DeX(e.X)); base.OnMouseMove(e); }
            protected override void OnMouseUp(MouseEventArgs e) { arrastra = false; base.OnMouseUp(e); }
            protected override bool IsInputKey(Keys k)
            {
                if (k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down
                 || k == Keys.PageUp || k == Keys.PageDown || k == Keys.Home || k == Keys.End) return true;
                return base.IsInputKey(k);
            }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (!Enabled) { base.OnKeyDown(e); return; }
                int salto = ((Maximum - Minimum) >= 40) ? 10 : 2;
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) { Pon(val - 1); e.Handled = true; }
                else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) { Pon(val + 1); e.Handled = true; }
                else if (e.KeyCode == Keys.PageDown) { Pon(val - salto); e.Handled = true; }
                else if (e.KeyCode == Keys.PageUp) { Pon(val + salto); e.Handled = true; }
                else if (e.KeyCode == Keys.Home) { Pon(Minimum); e.Handled = true; }
                else if (e.KeyCode == Keys.End) { Pon(Maximum); e.Handled = true; }
                base.OnKeyDown(e);
            }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
            protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int cy = Height / 2, alto = 6;
                Rectangle riel = new Rectangle(Radio, cy - alto / 2, Math.Max(1, Width - Radio * 2), alto);
                float f = (Maximum > Minimum) ? (val - Minimum) / (float)(Maximum - Minimum) : 0f;
                int cx = riel.X + (int)(riel.Width * f);
                Color ac = Enabled ? Acento : Color.FromArgb(92, 100, 124);
                using (GraphicsPath pa = RoundedPath(riel, alto / 2))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 46, 82)))
                    g.FillPath(b, pa);
                if (cx > riel.X + 1)
                    using (GraphicsPath pa = RoundedPath(new Rectangle(riel.X, riel.Y, cx - riel.X, riel.Height), alto / 2))
                    using (SolidBrush b = new SolidBrush(ac))
                        g.FillPath(b, pa);
                if (Focused)   // aro exterior: se ve cual esta enfocado al moverse con el teclado
                    using (Pen pn = new Pen(Color.FromArgb(130, ac.R, ac.G, ac.B), 2f))
                        g.DrawEllipse(pn, cx - Radio - 2, cy - Radio - 2, (Radio + 2) * 2, (Radio + 2) * 2);
                using (SolidBrush b = new SolidBrush(Enabled ? Color.FromArgb(238, 244, 255) : Color.FromArgb(130, 138, 160)))
                    g.FillEllipse(b, cx - Radio, cy - Radio, Radio * 2, Radio * 2);
                using (Pen pn = new Pen(ac, 2.2f))
                    g.DrawEllipse(pn, cx - Radio + 1, cy - Radio + 1, Radio * 2 - 2, Radio * 2 - 2);
            }
        }

        // INTERRUPTOR: la casilla de encender/apagar, con forma de pastilla. El texto va a su derecha y pulsar
        // en cualquier parte (pastilla o texto) lo cambia.
        private sealed class Interruptor : Control
        {
            public Color Acento = Color.FromArgb(110, 225, 160);
            public event EventHandler CheckedChanged;
            private bool marcado;
            public Interruptor()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                TabStop = true;
                Height = 26;
                Cursor = Cursors.Hand;
            }
            public bool Checked
            {
                get { return marcado; }
                set { if (marcado != value) { marcado = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
            }
            protected override void OnMouseDown(MouseEventArgs e) { if (Enabled) { Focus(); Checked = !Checked; } base.OnMouseDown(e); }
            protected override bool IsInputKey(Keys k) { return (k == Keys.Space) || base.IsInputKey(k); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)) { Checked = !Checked; e.Handled = true; }
                base.OnKeyDown(e);
            }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
            protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int h = 20, w = 40, y = (Height - h) / 2;
                Color ac = !Enabled ? Color.FromArgb(80, 88, 112) : (marcado ? Acento : Color.FromArgb(58, 68, 108));
                using (GraphicsPath pa = RoundedPath(new Rectangle(0, y, w, h), h / 2))
                using (SolidBrush b = new SolidBrush(ac))
                    g.FillPath(b, pa);
                if (Focused)
                    using (Pen pn = new Pen(Color.FromArgb(150, 190, 210, 255)))
                    using (GraphicsPath pa = RoundedPath(new Rectangle(0, y, w, h), h / 2))
                        g.DrawPath(pn, pa);
                int r = h - 6, kx = marcado ? (w - r - 3) : 3;
                using (SolidBrush b = new SolidBrush(Enabled ? Color.FromArgb(244, 248, 255) : Color.FromArgb(150, 156, 176)))
                    g.FillEllipse(b, kx, y + 3, r, r);
                if (!string.IsNullOrEmpty(Text))
                    using (SolidBrush tb = new SolidBrush(Enabled ? ForeColor : Color.FromArgb(110, 118, 142)))
                        g.DrawString(Text, Font, tb, w + 10, (Height - Font.Height) / 2f - 1f);
            }
        }

        // BOTON plano y redondeado, con su color de relleno. Los de Windows en XP salen grises con degradado.
        private sealed class BotonPlano : Control
        {
            public Color Relleno = Color.FromArgb(40, 52, 98);
            public Color Borde = Color.FromArgb(72, 90, 150);
            private bool encima;
            public BotonPlano()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent; TabStop = true; Height = 38; Cursor = Cursors.Hand;
                ForeColor = Color.White;
            }
            protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { encima = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
            protected override bool IsInputKey(Keys k) { return (k == Keys.Space) || base.IsInputKey(k); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
                base.OnKeyDown(e);
            }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                Color rel = encima ? Aclara(Relleno, 0.16f) : Relleno;
                using (GraphicsPath pa = RoundedPath(r, 9))
                using (SolidBrush b = new SolidBrush(rel))
                using (Pen pn = new Pen(Focused ? Aclara(Borde, 0.35f) : Borde, Focused ? 2f : 1f))
                { g.FillPath(b, pa); g.DrawPath(pn, pa); }
                SizeF sz = g.MeasureString(Text, Font);
                using (SolidBrush tb = new SolidBrush(ForeColor))
                    g.DrawString(Text, Font, tb, (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
            }
            private static Color Aclara(Color c, float t)
            { return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t)); }
        }

        // BARRA DE AVANCE plana (la de Windows en XP es verde con rayas y no pega con nada).
        private sealed class BarraAvance : Control
        {
            public float Valor;   // 0..1
            public Color Acento = Color.FromArgb(255, 170, 90);
            public BarraAvance()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent; Height = 8;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(4, Height));
                using (GraphicsPath pa = RoundedPath(r, Height / 2))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 46, 82)))
                    g.FillPath(b, pa);
                float f = Valor; if (f < 0f) f = 0f; else if (f > 1f) f = 1f;
                int w = (int)(r.Width * f);
                if (w > 2)
                    using (GraphicsPath pa = RoundedPath(new Rectangle(0, 0, w, r.Height), Height / 2))
                    using (SolidBrush b = new SolidBrush(Acento))
                        g.FillPath(b, pa);
            }
        }

        private void ShowVolumeManager()
        {
            if (searchOpen) CloseSearch();
            bool wasMaint = maintMode;
            maintMode = true; maintUntil = DateTime.Now.AddMinutes(10);
            bool changed = false;
            float bkTarget = normTarget, bkStrength = normStrength, bkExc = excAmount, bkUp = masterDb, bkEq = eqAmount, bkBass = bassAmount; int bkVid = videoVol; bool bkOn = normalizeOn, bkSbr = sbrOn, bkEqA = eqAuto, bkBassA = bassAuto;   // por si se Cancela
            try
            {
                Cursor.Show();
                FontFamily ff = FamiliaUi();
                using (Font fTit = new Font(ff, 15f, FontStyle.Bold))
                using (Font fSub = new Font(ff, 9.5f))
                using (Font fNom = new Font(ff, 10f, FontStyle.Bold))
                using (Font fVal = new Font(ff, 11f, FontStyle.Bold))
                using (Font fDes = new Font(ff, 8.5f))
                using (Font fBot = new Font(ff, 10f, FontStyle.Bold))
                using (Font fInt = new Font(ff, 9.5f, FontStyle.Bold))
                using (Form dlg = new Form())
                {
                    dlg.Text = "Rocola  -  Sonido";
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MaximizeBox = false; dlg.MinimizeBox = false; dlg.ShowInTaskbar = false; dlg.ShowIcon = false;
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ClientSize = new Size(960, 704);   // cabe entera en una pantalla de 1024x768
                    dlg.BackColor = Color.FromArgb(11, 14, 32);
                    dlg.Font = fSub;

                    Color cVol = Color.FromArgb(120, 230, 165);      // un color de acento por bloque
                    Color cFue = Color.FromArgb(140, 205, 255);
                    Color cSub = Color.FromArgb(255, 160, 140);
                    Color cVid = Color.FromArgb(140, 220, 232);
                    Color cCal = Color.FromArgb(178, 152, 255);
                    Color cGra = Color.FromArgb(255, 176, 122);
                    Color cBri = Color.FromArgb(255, 214, 140);
                    Color cBib = Color.FromArgb(255, 176, 102);
                    Color cTexto = Color.FromArgb(222, 230, 250);
                    Color cTenue = Color.FromArgb(138, 149, 182);    // texto de las explicaciones
                    Color cApagado = Color.FromArgb(96, 104, 128);

                    Label lTit = new Label(); lTit.SetBounds(22, 12, 700, 28); lTit.BackColor = Color.Transparent;
                    lTit.ForeColor = Color.FromArgb(232, 238, 255); lTit.Font = fTit; lTit.Text = "Sonido";
                    Label lSub = new Label(); lSub.SetBounds(24, 44, 916, 18); lSub.BackColor = Color.Transparent;
                    lSub.ForeColor = cTenue; lSub.Font = fSub;
                    lSub.Text = "Todo lo que cambies aqui se oye al instante en la cancion que esta sonando.";

                    // Las explicaciones ocupan DOS renglones y su hueco tiene sitio para dos: antes media 28 px,
                    // que con la letra de Windows se queda a un pelo justo de dos, y el segundo salia cortado
                    // ("...NUNCA se" y ahi se acababa). Ahora 32, y ademas cada texto lleva escrito a mano donde
                    // parte, para que no dependa de lo ancha que sea la letra en cada equipo.
                    Tarjeta tVol = new Tarjeta(); tVol.SetBounds(16, 70, 456, 400); tVol.Titulo = "VOLUMEN PAREJO"; tVol.Acento = cVol;
                    Tarjeta tCal = new Tarjeta(); tCal.SetBounds(488, 70, 456, 400); tCal.Titulo = "CALIDAD DEL SONIDO"; tCal.Acento = cCal;
                    Tarjeta tBib = new Tarjeta(); tBib.SetBounds(16, 480, 928, 160); tBib.Titulo = "TU BIBLIOTECA"; tBib.Acento = cBib;

                    // --- Una FILA de ajuste: nombre arriba, valor a la derecha, explicacion debajo y la barra abajo ---
                    Label[] nom = new Label[7], val = new Label[7], des = new Label[7];
                    Deslizador[] bar = new Deslizador[7];
                    Interruptor[] au = new Interruptor[7];
                    const int anchoFila = 424;
                    FilaAjuste Fila = delegate (int k, Tarjeta card, int y, string titulo, Color ac, int min, int max, bool conAuto)
                    {
                        int wNom = conAuto ? (anchoFila - 210) : (anchoFila - 100);
                        nom[k] = new Label(); nom[k].SetBounds(16, y, wNom, 18); nom[k].BackColor = Color.Transparent;
                        nom[k].ForeColor = cTexto; nom[k].Font = fNom; nom[k].Text = titulo;
                        if (conAuto)
                        {
                            au[k] = new Interruptor(); au[k].SetBounds(16 + wNom + 6, y - 2, 104, 22);
                            au[k].ForeColor = cTenue; au[k].Font = fInt; au[k].Text = "Auto"; au[k].Acento = ac;
                            card.Controls.Add(au[k]);
                        }
                        val[k] = new Label(); val[k].SetBounds(16 + anchoFila - 94, y - 2, 94, 20); val[k].BackColor = Color.Transparent;
                        val[k].ForeColor = ac; val[k].Font = fVal; val[k].TextAlign = ContentAlignment.TopRight;
                        des[k] = new Label(); des[k].SetBounds(16, y + 21, anchoFila, 32); des[k].BackColor = Color.Transparent;
                        des[k].ForeColor = cTenue; des[k].Font = fDes;
                        bar[k] = new Deslizador(); bar[k].SetBounds(16, y + 55, anchoFila, 22);
                        bar[k].Minimum = min; bar[k].Maximum = max; bar[k].Acento = ac;
                        card.Controls.Add(nom[k]); card.Controls.Add(val[k]); card.Controls.Add(des[k]); card.Controls.Add(bar[k]);
                    };

                    // ---------------- VOLUMEN PAREJO ----------------
                    Interruptor swOn = new Interruptor();
                    swOn.SetBounds(16, 40, 424, 26); swOn.ForeColor = cTexto; swOn.Font = fNom;
                    swOn.Text = "Nivelar el volumen de todas las canciones"; swOn.Acento = cVol; swOn.Checked = normalizeOn;
                    tVol.Controls.Add(swOn);
                    Fila(0, tVol, 76, "Volumen objetivo", cVol, 0, 100, false);
                    Fila(1, tVol, 156, "Fuerza del nivelado", cFue, 0, 100, false);
                    Fila(2, tVol, 236, "Subir TODO el volumen", cSub, 0, 12, false);
                    Fila(3, tVol, 316, "Volumen de los videos", cVid, 0, 100, false);
                    bar[0].Value = TargetToPct(normTarget);
                    bar[1].Value = (int)Math.Round(normStrength * 100f);
                    bar[2].Value = (int)Math.Round(masterDb);
                    bar[3].Value = videoVol;

                    // ---------------- CALIDAD DEL SONIDO ----------------
                    Fila(4, tCal, 44, "EQ automatico", cCal, 0, 100, true);
                    Fila(5, tCal, 124, "Graves (bajo profundo)", cGra, 0, 100, true);
                    Fila(6, tCal, 204, "Realce de brillo", cBri, 0, 100, false);
                    bar[4].Value = (int)Math.Round(eqAmount * 100f); au[4].Checked = eqAuto;
                    bar[5].Value = (int)Math.Round(bassAmount * 100f); au[5].Checked = bassAuto;
                    bar[6].Value = (int)Math.Round(excAmount * 100f);

                    Interruptor swSbr = new Interruptor();
                    swSbr.SetBounds(16, 292, 424, 26); swSbr.ForeColor = cTexto; swSbr.Font = fNom;
                    swSbr.Text = "DSEE"; swSbr.Acento = Color.FromArgb(150, 205, 255);
                    swSbr.Checked = sbrOn && sbrAllowed; swSbr.Enabled = sbrAllowed;
                    Label lSbr = new Label(); lSbr.SetBounds(16, 322, 424, 68); lSbr.BackColor = Color.Transparent;
                    lSbr.ForeColor = sbrAllowed ? cTenue : Color.FromArgb(224, 158, 120); lSbr.Font = fDes;
                    // El DSEE hace DOS cosas y conviene que se entienda cual es cual: una la hace SIEMPRE (medir
                    // donde el archivo se queda sin agudos, que es de donde salen el EQ y los graves automaticos)
                    // y la otra solo si se enciende (volver a crear esos agudos). El interruptor manda sobre la
                    // segunda, no sobre la primera, y por eso la explicacion las separa.
                    lSbr.Text = sbrAllowed
                        ? ("Oye cada cancion y ve donde el MP3 le corto los agudos.\n"
                           + "Eso lo mide SIEMPRE: de ahi sacan el EQ automatico y los\n"
                           + "graves automaticos cuanto le falta a cada una.\n"
                           + (NeuralEnv.Loaded
                              ? "EXPERIMENTAL: encendido, ademas los recrea con la IA."
                              : "EXPERIMENTAL: encendido, ademas recrea esa banda."))
                        : "No disponible en este equipo: " + sbrReason;
                    tCal.Controls.Add(swSbr); tCal.Controls.Add(lSbr);

                    // ---------------- TU BIBLIOTECA ----------------
                    Label lCal = new Label(); lCal.SetBounds(16, 40, 896, 20); lCal.BackColor = Color.Transparent;
                    lCal.ForeColor = Color.FromArgb(198, 214, 244); lCal.Font = fNom;
                    Label hint = new Label(); hint.SetBounds(16, 62, 896, 48); hint.BackColor = Color.Transparent;   // TRES renglones
                    hint.ForeColor = cTenue; hint.Font = fDes;
                    BotonPlano bTrain = new BotonPlano();
                    bTrain.SetBounds(16, 114, 300, 34); bTrain.Font = fBot;
                    Label lPct = new Label(); lPct.SetBounds(332, 112, 580, 18); lPct.BackColor = Color.Transparent;
                    lPct.ForeColor = cBib; lPct.Font = fDes; lPct.TextAlign = ContentAlignment.TopRight; lPct.Visible = false;
                    BarraAvance pbTrain = new BarraAvance();
                    pbTrain.SetBounds(332, 134, 580, 8); pbTrain.Acento = cBib; pbTrain.Visible = false;
                    tBib.Controls.Add(lCal); tBib.Controls.Add(hint); tBib.Controls.Add(bTrain); tBib.Controls.Add(lPct); tBib.Controls.Add(pbTrain);
                    EscribeInformeCalidad();   // el detalle, cancion por cancion, queda en un archivo junto al programa

                    // ---------------- PIE ----------------
                    BotonPlano bReset = new BotonPlano(); bReset.SetBounds(16, 652, 220, 38); bReset.Font = fBot;
                    bReset.Text = "Dejar como de fabrica";
                    BotonPlano bOk = new BotonPlano(); bOk.SetBounds(804, 652, 140, 38); bOk.Font = fBot;
                    bOk.Text = "Listo"; bOk.Relleno = Color.FromArgb(30, 96, 70); bOk.Borde = Color.FromArgb(72, 182, 132);

                    Action pintaTrain = delegate
                    {
                        bool on = preScanTurbo;
                        bTrain.Text = on ? "Detener el repaso a fondo" : "Repasar la biblioteca a fondo";
                        bTrain.Relleno = on ? Color.FromArgb(126, 52, 42) : Color.FromArgb(40, 52, 98);
                        bTrain.Borde = on ? Color.FromArgb(196, 96, 76) : Color.FromArgb(72, 90, 150);
                        bTrain.Invalidate();
                        int tot = preScanTotal, fal = preScanLeft;
                        bool corriendo = on && tot > 0;
                        pbTrain.Visible = corriendo; lPct.Visible = corriendo;
                        if (corriendo)
                        {
                            int hechas = tot - fal; if (hechas < 0) hechas = 0; if (hechas > tot) hechas = tot;
                            pbTrain.Valor = hechas / (float)Math.Max(1, tot);
                            pbTrain.Invalidate();
                            lPct.Text = hechas.ToString("#,0") + " de " + tot.ToString("#,0") + " canciones   (" + (hechas * 100 / Math.Max(1, tot)) + "%)";
                        }
                    };
                    Action updateLabels = delegate
                    {
                        // ---------- VOLUMEN ----------
                        val[0].Text = bar[0].Value + "%";
                        des[0].Text = "Que tan fuerte queda el conjunto.\nMas alto = todas las canciones suenan mas fuerte.";
                        val[1].Text = bar[1].Value + "%";
                        des[1].Text = "Cuanto se suben las canciones bajitas.\nLas que ya suenan fuerte NUNCA se bajan.";
                        val[2].Text = "+" + bar[2].Value + " dB";
                        des[2].Text = "De mas para TODAS, ademas del nivelado.\nPara cuando el local se queda corto de volumen.";
                        val[3].Text = bar[3].Value + "%";
                        des[3].Text = "Los videos no pasan por la cadena de sonido.\nSu volumen se empareja aqui a mano.";
                        // ---------- CALIDAD ---------- (en automatico se ensena lo que eligio el programa, no la barra)
                        int eqPct = (int)Math.Round((au[4].Checked ? EqFuerzaAuto() : bar[4].Value / 100f) * 100f);
                        val[4].Text = eqPct + "%";
                        des[4].Text = "Empareja el tono de cada cancion con el de tu biblioteca.\n"
                                    + (au[4].Checked
                                       ? (eqAvgSanas >= 10
                                          ? ("Solo: mas fuerza cuantas mas canciones medidas (van " + eqAvgSanas.ToString("#,0") + ").")
                                          : "Solo: aun no hay bastantes medidas; de momento no toca nada.")
                                       : "A mano: manda la barra.");
                        int bBase = (int)Math.Round((au[5].Checked ? bassAutoBase : bar[5].Value / 100f) * 100f);
                        int bEfe = (int)Math.Round(bBase * bassSongFactor);
                        val[5].Text = bBase + "%";
                        des[5].Text = "Devuelve el golpe de bajo que el archivo no trae.\n"
                                    + (au[5].Checked ? "Solo: a cada cancion se le da lo que le falta." : "A mano: manda la barra.")
                                    + ((nowPlaying != null && bEfe != bBase) ? ("   Esta: " + bEfe + "%.") : "");
                        val[6].Text = bar[6].Value + "%";
                        des[6].Text = "Devuelve el 'aire' que el MP3 apaga.\nSe le da mas a las canciones peor grabadas.";
                        // ---------- BIBLIOTECA ----------
                        {
                            int cb, cm, cp, cr, cs2;
                            CalidadResumen(out cb, out cm, out cp, out cr, out cs2);
                            lCal.Text = (cb + cm + cp > 0)
                                ? ("Calidad:   " + cb.ToString("#,0") + " completas     " + cm.ToString("#,0") + " aceptables     "
                                   + cp.ToString("#,0") + " pobres" + (cr > 0 ? ("     " + cr.ToString("#,0") + " con los picos recortados") : ""))
                                : "Calidad: aun sin medir. Deja que el repaso vaya haciendo su trabajo.";
                        }
                        string etapa = (nowPlaying != null && nowPlaying.Song != null) ? LoudTierName(nowPlaying.Song.Path) : "";
                        int tTot = preScanTotal, tFal = preScanLeft;
                        // TRES renglones, cada uno con lo suyo y con el corte escrito a mano: antes iba todo
                        // seguido en dos y con la biblioteca grande el ultimo se salia del hueco y no se veia.
                        hint.Text = (preScanTurbo && tTot > 0)
                            ? ((tFal > 0 ? "Repasando la biblioteca a fondo ahora mismo." : "Repaso a fondo terminado: la biblioteca esta entera.")
                               + "\nMaterial para la IA: " + IaInfoText() + "."
                               + (etapa.Length > 0 ? ("   La cancion que suena entra en la etapa " + etapa + ".") : "")
                               + "\nEl detalle, cancion por cancion, queda en rocola_calidad.txt junto al programa.")
                            : ("La biblioteca se mide sola mientras la rocola trabaja: "
                               + (tFal > 0 ? ("faltan " + tFal.ToString("#,0") + " canciones.") : "ya esta al dia.")
                               + (durScanLeft > 0 ? ("   Duraciones: faltan " + durScanLeft.ToString("#,0") + ".") : "")
                               + "\n" + (iaHarvestOn ? ("Material para la IA: " + IaInfoText() + ".   ") : "")
                               + (etapa.Length > 0 ? ("La cancion que suena entra en la etapa " + etapa + ".") : "")
                               + "\nEl detalle, cancion por cancion, queda en rocola_calidad.txt junto al programa.");
                        pintaTrain();
                        // Lo que no manda nadie se apaga, para que se vea de un vistazo que no hay que tocarlo.
                        bool en = swOn.Checked;
                        bar[0].Enabled = en; bar[1].Enabled = en;
                        nom[0].ForeColor = nom[1].ForeColor = en ? cTexto : cApagado;
                        val[0].ForeColor = en ? cVol : cApagado;
                        val[1].ForeColor = en ? cFue : cApagado;
                        des[0].ForeColor = des[1].ForeColor = en ? cTenue : cApagado;
                        bar[4].Enabled = !au[4].Checked;
                        bar[5].Enabled = !au[5].Checked;
                    };
                    EventHandler apply = delegate
                    {
                        normalizeOn = swOn.Checked;
                        normTarget = TargetFromPct(bar[0].Value);
                        normStrength = bar[1].Value / 100f;
                        masterDb = bar[2].Value;
                        videoVol = bar[3].Value;
                        if (videoOn && nowPlaying != null) SetVideoVolume(VideoVolumeFor(nowPlaying.VideoPath));   // el video que suena cambia YA
                        eqAuto = au[4].Checked; bassAuto = au[5].Checked;
                        eqAmount = bar[4].Value / 100f;
                        bassAmount = bar[5].Value / 100f;
                        excAmount = bar[6].Value / 100f;
                        sbrOn = swSbr.Checked && sbrAllowed;
                        changed = true;
                        updateLabels();
                        ApplyNormNow();   // se oye el cambio al instante en la cancion que suena
                        UpdateNowPlayingUI();   // y el letrero MEJORADA aparece/desaparece al instante
                    };
                    for (int k = 0; k < bar.Length; k++) { bar[k].Scroll += apply; if (au[k] != null) au[k].CheckedChanged += apply; }
                    swOn.CheckedChanged += apply; swSbr.CheckedChanged += apply;
                    bReset.Click += delegate
                    {
                        swOn.Checked = true;
                        bar[0].Value = TargetToPct(0.26f); bar[1].Value = 100; bar[2].Value = 0; bar[3].Value = 85;
                        bar[4].Value = 0; bar[5].Value = 30; bar[6].Value = 35;
                        au[4].Checked = true; au[5].Checked = true;          // EQ y graves en automatico
                        if (swSbr.Enabled) swSbr.Checked = false;            // el espectral apagado: es experimental
                        apply(bReset, EventArgs.Empty);
                    };
                    bOk.Click += delegate { dlg.Close(); };
                    // El boton enciende/apaga el repaso a fondo. Al encenderlo se relanza el pre-analisis con TODA
                    // la biblioteca; al apagarlo, la tanda en curso se corta y vuelve el ritmo normal (que seguira
                    // solo con lo que falte). Lo medido hasta ese momento ya quedo guardado.
                    bTrain.Click += delegate
                    {
                        preScanTurbo = !preScanTurbo;
                        StartPreScan();
                        if (!preScanTurbo) { preScanTotal = 0; }
                        updateLabels();
                        ShowToast(preScanTurbo ? "Repaso a fondo: ENCENDIDO" : "Repaso a fondo: detenido",
                                  preScanTurbo ? Color.FromArgb(150, 60, 45) : Color.FromArgb(40, 130, 90));
                    };

                    dlg.Controls.Add(lTit); dlg.Controls.Add(lSub);
                    dlg.Controls.Add(tVol); dlg.Controls.Add(tCal); dlg.Controls.Add(tBib);
                    dlg.Controls.Add(bReset); dlg.Controls.Add(bOk);
                    // Escape cierra, como toda la vida (los controles propios se comen las teclas, asi que la
                    // ventana las mira antes con KeyPreview).
                    dlg.KeyPreview = true;
                    dlg.KeyDown += delegate (object so, KeyEventArgs ke) { if (ke.KeyCode == Keys.Escape) dlg.Close(); };
                    // Refresco del avance mientras el dialogo esta abierto (el trabajo ocurre en otro hilo).
                    System.Windows.Forms.Timer tTrain = new System.Windows.Forms.Timer();
                    tTrain.Interval = 500;
                    tTrain.Tick += delegate { updateLabels(); };
                    tTrain.Start();
                    updateLabels();
                    dlg.ShowDialog(this);
                    tTrain.Stop(); tTrain.Dispose();
                }
                if (changed) { SaveConfigPrefs(); ShowToast("Sonido actualizado", Color.FromArgb(40, 130, 90)); }
            }
            catch { normTarget = bkTarget; normStrength = bkStrength; normalizeOn = bkOn; excAmount = bkExc; masterDb = bkUp; eqAmount = bkEq; bassAmount = bkBass; videoVol = bkVid; sbrOn = bkSbr; eqAuto = bkEqA; bassAuto = bkBassA; }   // si algo truena, deja los valores como estaban
            finally { try { Cursor.Hide(); } catch { } maintMode = wasMaint; ForceFocus(); }
        }

        // Busca automaticamente una carpeta llamada "musica" junto al programa o hasta 3 niveles arriba
        // (ej.: nueva\Release\Rocola.exe  +  nueva\musica). No requiere elegir carpeta.
        private string MusicFolder
        {
            get
            {
                try
                {
                    string dir = Application.StartupPath;
                    for (int i = 0; i < 4 && dir != null; i++)
                    {
                        string cand = Path.Combine(dir, "musica");
                        if (Directory.Exists(cand)) return cand;
                        DirectoryInfo p = Directory.GetParent(dir);
                        dir = (p != null) ? p.FullName : null;
                    }
                }
                catch { }
                return Path.Combine(Application.StartupPath, "musica");   // por defecto, junto al exe
            }
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutControls(); Invalidate(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 2 || h < 2) { base.OnPaintBackground(e); return; }
#if ROCOLA_GPU
            if (gpuPanel != null && gpuPanel.Visible) return;   // el panel GPU (pantalla completa) ya cubre todo; no gastes CPU pintando el cielo detras
#endif
            formStars.SkyOffsetX = 0; formStars.SkyOffsetY = 0; formStars.SkyFullW = w; formStars.SkyFullH = h;
            formStars.SkyMid = SkyColorMid(bgAccent);
            formStars.Dim = bgDim;   // atenua TAMBIEN las franjas de arriba/abajo en sincronia con la rejilla (una sola pieza)
            formStars.DrawCached(g, w, h, SkyColor(bgAccent), SkyColor2(bgAccent));   // cielo en degradado CONTINUO; etiquetas y mini-cubo transparentes encima
        }

        private static Color Shade(Color c, double towardBlack)
        {
            if (towardBlack < 0) towardBlack = 0; if (towardBlack > 1) towardBlack = 1;
            double k = 1.0 - towardBlack;
            return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        // Cielo del fondo: casi negro con un toque del color del genero (arranca oscuro como el modo
        // atraccion, pero cambia de tinte segun lo que suena).
        private static Color SkyColor(Color accent)
        {
            // ARRIBA del cielo: casi negro (apenas un dejo del genero). Se oscurece hacia el cenit.
            int r = 3 + accent.R / 22, g = 4 + accent.G / 22, b = 8 + accent.B / 18;
            if (r > 22) r = 22; if (g > 22) g = 22; if (b > 30) b = 30;
            return Color.FromArgb(255, r, g, b);
        }

        // Color INFERIOR del degradado del cielo: mas tenido al genero y un poco mas claro que el de arriba.
        private static Color SkyColor2(Color accent)
        {
            // ABAJO del cielo: resplandor de horizonte. Sin musica = azul default; con musica = el color del
            // genero normalizado a un brillo visible, con un leve piso azul para que siga sintiendose cielo.
            int mx = accent.R; if (accent.G > mx) mx = accent.G; if (accent.B > mx) mx = accent.B;
            int r, g, b;
            if (mx < 10) { r = 50; g = 88; b = 188; }
            else
            {
                r = accent.R * 165 / mx; g = accent.G * 165 / mx; b = accent.B * 165 / mx;
                r = (r * 3 + 50) / 4; g = (g * 3 + 88) / 4; b = (b * 3 + 188) / 4;
            }
            if (r > 205) r = 205; if (g > 205) g = 205; if (b > 220) b = 220;
            r = r * SkyDark / 100; g = g * SkyDark / 100; b = b * SkyDark / 100;   // ESTETICA: cielo un poco mas oscuro (el azul se veia muy brillante)
            return Color.FromArgb(255, r, g, b);
        }

        // Factor de OSCURIDAD del cielo (100 = como antes; menor = mas oscuro). Solo brillo, conserva el tinte.
        private const int SkyDark = 75;

        // EN MEDIO del cielo: el "azulado default". Casi independiente del genero (apenas un dejo) para que,
        // como pediste, despues del color del genero de abajo siga el azul antes de oscurecer al negro de arriba.
        private static Color SkyColorMid(Color accent)
        {
            int r = 22 + accent.R / 10, g = 50 + accent.G / 8, b = 120 + accent.B / 7;
            if (r > 60) r = 60; if (g > 105) g = 105; if (b > 205) b = 205;
            r = r * SkyDark / 100; g = g * SkyDark / 100; b = b * SkyDark / 100;   // ESTETICA: mitad del cielo mas oscura
            return Color.FromArgb(255, r, g, b);
        }

        private static Color Lighten(Color c, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t));
        }

        private static Color LerpColor(Color a, Color b, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        private static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            if (d < 2) d = 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string formd = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(formd.Length);
            for (int i = 0; i < formd.Length; i++)
                if (CharUnicodeInfo.GetUnicodeCategory(formd[i]) != UnicodeCategory.NonSpacingMark) sb.Append(formd[i]);
            return sb.ToString();
        }

        private static bool Has(string s, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++) if (s.Contains(keys[i])) return true;
            return false;
        }

        // Color por ARTISTA (excepciones) y luego por genero.
        private static Color ColorForTrack(Track t)
        {
            if (t == null) return DefaultAccent;
            string a = (t.Artist != null) ? t.Artist.ToLowerInvariant() : "";
            if (a.Contains("bts") || a.Contains("bangtan")) return Color.FromArgb(149, 86, 220);   // morado (peticion)
            return GenreToColor(t.Genre);
        }

        // Etiqueta canonica del genero para agrupar los discos TOP (mismas familias que GenreToColor).
        private static string GenreLabel(string genre)
        {
            if (string.IsNullOrEmpty(genre)) return "Otros";
            string g = genre.ToLowerInvariant();
            if (Has(g, "k-pop", "kpop", "j-pop", "jpop", "anime", "idol")) return "K-Pop";
            if (Has(g, "metal", "thrash", "death", "heavy", "hardcore", "grindcore")) return "Metal";
            if (Has(g, "punk")) return "Punk";
            if (Has(g, "gothic", "goth", "darkwave")) return "Gotico";
            if (Has(g, "industrial")) return "Industrial";
            if (Has(g, "reggaeton", "reggaet", "perreo")) return "Reggaeton";
            if (Has(g, "hip", "rap", "trap")) return "Hip-Hop";
            if (Has(g, "salsa", "mereng", "bachata", "tropical")) return "Tropical";
            if (Has(g, "cumbia", "banda", "norte", "ranch", "mariach", "regional", "tejano", "grupera", "corrido", "duranguense", "sonidero")) return "Regional";
            if (Has(g, "bolero", "tango", "ranchera")) return "Bolero";
            if (Has(g, "flamenco", "rumba", "copla")) return "Flamenco";
            if (Has(g, "latin", "latino", "espan")) return "Latino";
            if (Has(g, "reggae", "ska", "dub")) return "Reggae";
            if (Has(g, "drum", "bass", "jungle", "breakbeat", "garage")) return "DnB";
            if (Has(g, "electro", "techno", "house", "trance", "edm", "synth", "dance", "club", "dubstep", "rave", "hardstyle")) return "Electronica";
            if (Has(g, "ambient", "chill", "lo-fi", "lofi", "downtempo", "new age", "meditat")) return "Chill";
            if (Has(g, "jazz", "swing", "bebop")) return "Jazz";
            if (Has(g, "blues", "soul", "funk", "r&b", "rhythm", "motown")) return "Soul";
            if (Has(g, "gospel", "christian", "worship", "cristiana")) return "Cristiana";
            if (Has(g, "classic", "orchestr", "piano", "opera", "symphon", "baroque", "instrumental")) return "Clasica";
            if (Has(g, "soundtrack", "score", "cinematic", "banda sonora")) return "Soundtrack";
            if (Has(g, "country", "bluegrass", "americana")) return "Country";
            if (Has(g, "folk", "acoustic", "celt")) return "Folk";
            if (Has(g, "disco")) return "Disco";
            if (Has(g, "oldies", "50s", "60s", "doo")) return "Oldies";
            if (Has(g, "world", "ethnic", "african", "arab", "india", "asian")) return "World";
            if (Has(g, "vocal", "capella", "capela")) return "Vocal";
            if (Has(g, "comedy", "spoken", "podcast")) return "Hablado";
            if (Has(g, "child", "kids", "infantil")) return "Infantil";
            if (Has(g, "pop")) return "Pop";
            if (Has(g, "rock", "grunge", "alternative", "indie")) return "Rock";
            return "Otros";
        }

        private static Color GenreToColor(string genre)
        {
            if (string.IsNullOrEmpty(genre)) return DefaultAccent;
            string g = genre.ToLowerInvariant();
            // K-pop / J-pop / anime -> morado
            if (Has(g, "k-pop", "kpop", "j-pop", "jpop", "anime", "idol")) return Color.FromArgb(149, 86, 220);
            if (Has(g, "metal", "thrash", "death", "heavy", "hardcore", "grindcore")) return Color.FromArgb(205, 45, 55);
            if (Has(g, "punk")) return Color.FromArgb(232, 80, 58);
            if (Has(g, "gothic", "goth", "darkwave")) return Color.FromArgb(112, 58, 150);
            if (Has(g, "industrial")) return Color.FromArgb(150, 60, 70);
            if (Has(g, "reggaeton", "reggaet", "perreo")) return Color.FromArgb(150, 70, 210);
            if (Has(g, "hip", "rap", "trap")) return Color.FromArgb(135, 60, 205);
            if (Has(g, "salsa", "mereng", "bachata", "tropical")) return Color.FromArgb(235, 95, 35);
            if (Has(g, "cumbia", "banda", "norte", "ranch", "mariach", "regional", "tejano", "grupera", "corrido", "duranguense", "sonidero")) return Color.FromArgb(225, 120, 30);
            if (Has(g, "bolero", "tango", "ranchera")) return Color.FromArgb(195, 95, 70);
            if (Has(g, "flamenco", "rumba", "copla")) return Color.FromArgb(210, 110, 55);
            if (Has(g, "latin", "latino", "espan")) return Color.FromArgb(225, 130, 40);
            if (Has(g, "reggae", "ska", "dub")) return Color.FromArgb(40, 175, 85);
            if (Has(g, "drum", "bass", "jungle", "breakbeat", "garage")) return Color.FromArgb(20, 155, 160);
            if (Has(g, "electro", "techno", "house", "trance", "edm", "synth", "dance", "club", "dubstep", "rave", "hardstyle")) return Color.FromArgb(30, 175, 195);
            if (Has(g, "ambient", "chill", "lo-fi", "lofi", "downtempo", "new age", "meditat")) return Color.FromArgb(70, 165, 175);
            if (Has(g, "jazz", "swing", "bebop")) return Color.FromArgb(215, 165, 45);
            if (Has(g, "blues", "soul", "funk", "r&b", "rhythm", "motown")) return Color.FromArgb(205, 145, 50);
            if (Has(g, "gospel", "christian", "worship", "cristiana")) return Color.FromArgb(210, 180, 75);
            if (Has(g, "classic", "orchestr", "piano", "opera", "symphon", "baroque", "instrumental")) return Color.FromArgb(95, 95, 205);
            if (Has(g, "soundtrack", "score", "cinematic", "banda sonora")) return Color.FromArgb(110, 120, 190);
            if (Has(g, "country", "bluegrass", "americana")) return Color.FromArgb(170, 130, 55);
            if (Has(g, "folk", "acoustic", "celt")) return Color.FromArgb(150, 140, 70);
            if (Has(g, "disco")) return Color.FromArgb(212, 70, 160);
            if (Has(g, "oldies", "50s", "60s", "doo")) return Color.FromArgb(192, 120, 90);
            if (Has(g, "world", "ethnic", "african", "arab", "india", "asian")) return Color.FromArgb(180, 110, 60);
            if (Has(g, "vocal", "capella", "capela")) return Color.FromArgb(180, 150, 120);
            if (Has(g, "comedy", "spoken", "podcast")) return Color.FromArgb(120, 130, 150);
            if (Has(g, "child", "kids", "infantil")) return Color.FromArgb(235, 180, 60);
            if (Has(g, "pop")) return Color.FromArgb(215, 60, 140);
            if (Has(g, "rock", "grunge", "alternative", "indie")) return Color.FromArgb(215, 90, 40);
            return DefaultAccent;
        }

        private Font Bold(float size) { return new Font(this.Font.FontFamily, size, FontStyle.Bold); }

        private void TransLabel(Label l, float size)
        {
            l.BackColor = Color.Transparent;
            l.ForeColor = Color.White;
            l.Font = Bold(size);
        }

        private void BuildUi()
        {
            this.Text = "Rocola";
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }   // estrella metalica (barra de tareas / Alt-Tab); el exe ya la lleva embebida
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1100, 780);
            this.MinimumSize = new Size(820, 600);
            this.BackColor = Color.FromArgb(10, 14, 40);
            this.FormBorderStyle = FormBorderStyle.None;     // PANTALLA COMPLETA al iniciar (sin barra de titulo)
            this.WindowState = FormWindowState.Maximized;    // borde nulo + maximizado = cubre toda la pantalla

            lblStatus = new Label(); TransLabel(lblStatus, 9.5f); lblStatus.Text = "Cargando musica...";
            helpBar = new HelpBar();   // instrucciones con formas/colores (sin nombres de teclas)

            lblAttract = new Label();
            lblAttract.AutoSize = false;
            lblAttract.TextAlign = ContentAlignment.MiddleCenter;
            lblAttract.BackColor = Color.FromArgb(10, 12, 30);
            lblAttract.ForeColor = Color.FromArgb(250, 230, 50);
            lblAttract.Font = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold);
            lblAttract.Text = "¡ PRESIONA UN BOTON PARA USAR LA ROCOLA !";
            lblAttract.Visible = false;

            letterBar = new LetterBar();
            letterBar.Available = letterAvailable;
            letterBar.OnPick = delegate (char c)
            {
                if (c == VideoMark && (videoAlbums == null || videoAlbums.Count == 0)) return;
                if (c == TopMark) { BuildTopDiscs(); if (topDiscs == null || topDiscs.Count == 0) return; }
                currentLetter = c; page = 0; selAlbumIndex = 0; letterBar.Current = c;
                RefreshAlbums(); this.Focus();
            };

            lblHeader = new Label(); TransLabel(lblHeader, 12.5f);

            albumView = new AlbumView();
            albumView.Sky = formStars;   // MISMO campo de estrellas que el fondo del form: la rejilla pinta su ventana (una sola pieza, sin costuras)
            albumView.GetCover = LoadCoverAsync;
            albumView.GetCoverByPath = LoadCoverByPath;
            albumView.OnActivate = delegate (int local)
            {
                List<Album> list = CurrentList();
                int gi = page * PageSize + local;
                if (gi >= 0 && gi < list.Count) { selAlbumIndex = gi; albumView.Selected = local; EnterAlbum(list[gi]); }
            };

            panelSongs = new BgPanel();
            panelSongs.Sky = formStars;   // idem: el interior del disco pinta su ventana del MISMO campo compartido
            panelSongs.BackColor = Shade(DefaultAccent, 0.70);
            panelSongs.Visible = false;

            lblAlbum = new Label(); lblAlbum.BackColor = Color.Transparent; lblAlbum.ForeColor = Color.White; lblAlbum.Font = Bold(16f);

            cubeBox = new CubeBox();
            cubeBox.BackColor = Color.Transparent;

            songList = new SongListView();
            songList.BackColor = Shade(DefaultAccent, 0.76);
            songList.Font = Bold(13f);
            songList.RowH = 30;
            songList.GetCount = delegate { return songItems.Count; };
            songList.GetText = delegate (int i) { return (i >= 0 && i < songItems.Count) ? songItems[i].ToString() : ""; };
            songList.GetRight = delegate (int i)   // duracion de la cancion (los videos no se pre-analizan)
            {
                if (i < 0 || i >= songItems.Count) return "";
                Track tk = songItems[i] as Track;
                if (tk != null) return DurText(tk.Path);
                VideoItem vi = songItems[i] as VideoItem;   // los videos solo traen duracion donde se pueden medir (rama HD)
                return (vi != null) ? DurText(vi.Path) : "";
            };
            songList.OnPick = delegate (int idx) { songSel = idx; FillSongPage(); UpdateTopSongVisuals(); };         // click simple
            songList.OnActivate = delegate (int idx) { songSel = idx; FillSongPage(); EnqueueSelectedSong(); };      // doble-click

            songPager = new SongPagerBar();
            songPager.Visible = false;
            songPager.BackColor = Shade(DefaultAccent, 0.70);
            panelSongs.Controls.Add(lblAlbum);
            panelSongs.Controls.Add(cubeBox);
            panelSongs.Controls.Add(songList);
            panelSongs.Controls.Add(songPager);

            lblNow = new Label(); TransLabel(lblNow, 13f); lblNow.Text = "Nada reproduciendose"; lblNow.AutoEllipsis = true;
            lblNow.ForeColor = Color.FromArgb(125, 235, 165);   // ESTETICA: verde "sonando" (se distingue de "siguiente")
            lblNowTop = new Label(); TransLabel(lblNowTop, 13f); lblNowTop.Text = ""; lblNowTop.AutoEllipsis = true;
            lblNowTop.ForeColor = lblNow.ForeColor;             // misma linea, mismo verde: se leen como un solo texto
            lblNowTop.TextAlign = ContentAlignment.BottomLeft;  // pegada a la linea de abajo, no flotando
            lblNow.UseMnemonic = false; lblNowTop.UseMnemonic = false;   // un "&" del titulo es un "&", no un subrayado
            lblNext = new Label(); TransLabel(lblNext, 11f); lblNext.Text = "Siguiente:  -"; lblNext.AutoEllipsis = true;
            lblNext.ForeColor = Color.FromArgb(150, 200, 255);  // ESTETICA: azul claro "siguiente"
            lblNext.UseMnemonic = false;                        // igual que arriba: un "&" del titulo es un "&"
            lblCount = new Label(); TransLabel(lblCount, 11f); lblCount.TextAlign = ContentAlignment.MiddleLeft; lblCount.Text = "En cola:  0";
            lblCredits = new Label(); TransLabel(lblCredits, 14f); lblCredits.TextAlign = ContentAlignment.MiddleLeft; lblCredits.Text = "Inserta una moneda"; lblCredits.ForeColor = Color.FromArgb(255, 205, 90);
            lblMejorada = new Label(); TransLabel(lblMejorada, 10f); lblMejorada.Text = "MEJORADA"; lblMejorada.ForeColor = MejGoldBase;
            lblMejorada.TextAlign = ContentAlignment.MiddleLeft; lblMejorada.Visible = false;   // aparece solo si la cancion suena con DSEE (PositionMejorada)

            miniCube = new CubeBox();
            miniCube.BackColor = Color.Transparent;
            miniCube.Visible = false;

            progress = new ProgressView();
            lblTime = new Label(); TransLabel(lblTime, 10.5f); lblTime.Text = "0:00 / 0:00";

            lblToast = new Label();
            lblToast.TextAlign = ContentAlignment.MiddleCenter;
            lblToast.BackColor = Color.FromArgb(40, 165, 90);
            lblToast.ForeColor = Color.White;
            lblToast.Font = Bold(14f);
            lblToast.Visible = false;

            searchOverlay = new SearchOverlay();
            searchOverlay.Visible = false;
            searchOverlay.GetCover = LoadCover;
            searchOverlay.OnQueryChanged = RecomputeResults;
            searchOverlay.OnChoose = ChooseSearchResult;
            searchOverlay.OnClose = CloseSearch;

            attractView = new AttractView();
            attractView.Visible = false;
            attractView.GetCoverByPath = LoadCoverByPath;

            Controls.Add(lblStatus); Controls.Add(helpBar); Controls.Add(lblCredits);
            Controls.Add(letterBar); Controls.Add(lblHeader);
            Controls.Add(albumView); Controls.Add(panelSongs);
            Controls.Add(lblNowTop); Controls.Add(lblNow); Controls.Add(lblMejorada); Controls.Add(lblNext); Controls.Add(lblCount); Controls.Add(progress); Controls.Add(lblTime);
            Controls.Add(miniCube);
            Controls.Add(lblAttract);
            Controls.Add(lblToast);
            Controls.Add(searchOverlay);
            Controls.Add(attractView);

            // ----- DIAGNOSTICO TEMPORAL: letrero que muestra que tecla recibe la app -----
            // ----- Pantalla del ASISTENTE DE CONFIGURACION de botones -----
            lblConfig = new Label();
            lblConfig.AutoSize = false;
            lblConfig.TextAlign = ContentAlignment.MiddleCenter;
            lblConfig.BackColor = Color.FromArgb(8, 10, 26);
            lblConfig.ForeColor = Color.FromArgb(245, 248, 255);
            lblConfig.Font = new Font(FontFamily.GenericSansSerif, 20f, FontStyle.Bold);
            lblConfig.Text = "";
            lblConfig.Visible = false;
            Controls.Add(lblConfig);
            lblConfig.BringToFront();
        }

        private void LayoutControls()
        {
            if (albumView == null) return;
            int W = ClientSize.Width, H = ClientSize.Height, pad = 14;

            if (searchOverlay != null) searchOverlay.Bounds = SearchOverlayRect();
            if (attractView != null) attractView.Bounds = new Rectangle(0, 0, W, H);

            // PANTALLA COMPLETA del video: ocupa casi todo; arriba queda una franja con el aviso pulsante.
            if (videoFull && video != null)
            {
                int strip = 46;
                lblAttract.SetBounds(0, 0, W, strip);
                video.SetBounds(0, strip, W, H - strip);
                video.BringToFront();
                lblAttract.BringToFront();
                if (helpBar != null) helpBar.Visible = false;
                return;   // lo demas queda detras del video; se reacomoda al salir
            }
            if (helpBar != null) helpBar.Visible = true;
            if (lblAttract != null) lblAttract.Visible = false;

            // Con video, el contenido (letras, encabezado, rejilla) se encoge a la IZQUIERDA;
            // la barra inferior (lo de abajo) SIEMPRE ocupa todo el ancho.
            int splitX = videoOn ? (int)(W * 0.50) : (W - pad);
            int contentRight = videoOn ? (splitX - 12) : (W - pad);
            int contentW = Math.Max(220, contentRight - pad);

            int topRight = W - pad;
            int helpX = pad;
            int helpW = Math.Max(120, Math.Min(580, topRight - helpX));
            if (helpBar != null) helpBar.SetBounds(helpX, 8, helpW, 34);
            int statusX = helpX + helpW + 12;
            lblStatus.SetBounds(statusX, 15, Math.Max(40, topRight - statusX), 22);
            if (lblConfig != null) lblConfig.SetBounds(0, 0, W, H);   // asistente de config: pantalla completa

            int lettersY = 48, lettersH = 40;
            letterBar.SetBounds(pad, lettersY, contentW, lettersH);

            int headerY = lettersY + lettersH + 10;
            lblHeader.SetBounds(pad, headerY + 3, Math.Max(50, contentW), 28);

            int cubeSize = 64;
            int progH = 16;                                  // barra mas delgada
            int progY = H - pad - progH;
            int nextY = progY - 26;
            int nowY = nextY - 26;                           // la ULTIMA linea de "Reproduciendo": donde estuvo siempre
            // Se reserva UNA linea mas ARRIBA para los titulos largos. El hueco esta siempre, se use o no: asi el
            // acomodo no cambia nunca al pasar de cancion (si cambiara, la rejilla de discos se re-hornearia entera
            // cada vez que sonara un titulo largo). Con titulo corto el hueco queda vacio y todo se ve como antes.
            int nowLineH = Math.Max(24, (lblNow != null ? lblNow.Font.Height : 22) + 2);
            int nowTopY = nowY - nowLineH;

            // cluster derecho del fondo:  [cubo mini] [ Creditos (grande) / En cola ]   (ancho completo)
            int clusterTop = nowY;
            int clusterBottom = progY + progH;
            int clusterH = clusterBottom - clusterTop;
            int rightTextW = 200;
            int rightTextX = W - pad - rightTextW;
            int cubeX = rightTextX - 10 - cubeSize;
            int cubeY = clusterTop + (clusterH - cubeSize) / 2; if (cubeY < clusterTop) cubeY = clusterTop;
            miniCube.SetBounds(cubeX, cubeY, cubeSize, cubeSize);
            if (lblCredits != null) lblCredits.SetBounds(rightTextX, clusterTop + 2, rightTextW, 28);   // grande, arriba
            lblCount.SetBounds(rightTextX, clusterTop + 34, rightTextW, 22);                            // chico, abajo

            // area izquierda (texto + barra de duracion) termina antes del cubo mini
            int leftRight = cubeX - 14;
            int leftW = Math.Max(120, leftRight - pad);
            int timeW = 150;
            progress.SetBounds(pad, progY, Math.Max(50, leftW - timeW - 10), progH);
            lblTime.SetBounds(pad + leftW - timeW, progY - 1, timeW, 24);
            lblNext.SetBounds(pad, nextY, leftW, 22);
            lblNow.SetBounds(pad, nowY, leftW, 24);
            lblNowTop.SetBounds(pad, nowTopY, leftW, nowLineH);
            mejRowW = leftW; PositionMejorada();   // parte el titulo en 1 o 2 lineas y acomoda el letrero MEJORADA tras la ultima

            int albumY = headerY + 44;
            int albumBottom = nowTopY - 12;   // la rejilla termina antes del hueco reservado para la 2a linea
            int albumH = Math.Max(120, albumBottom - albumY);
            albumView.SetBounds(pad, albumY, contentW, albumH);
            panelSongs.SetBounds(pad, albumY, contentW, albumH);

            // Reproductor de video a la derecha (donde estan los cubos)
            if (video != null)
            {
                int vx = splitX;
                int vy = lettersY;
                int vw = Math.Max(120, (W - pad) - vx);
                int vh = Math.Max(90, albumBottom - vy);
                video.SetBounds(vx, vy, vw, vh);
                video.Visible = videoOn && !searchOpen;   // garantia: oculto si no hay video sonando (o si el buscador esta abierto)
            }

            LayoutSongsPanel();
#if ROCOLA_GPU
            // Un reacomodo REAL (video que entra o sale, cambio de tamano) mueve lo que la textura de UI tiene
            // horneado: se rehace YA, en el mismo cuadro. Pero LayoutControls tambien corre en CADA cambio de
            // cancion (PlayNext) sin que nada se mueva: la HUELLA evita rehornear y resubir la textura en vano
            // (un tropiezo de 10-20 ms por cancion en el CPU modesto, para nada).
            int huella = (ClientSize.Width * 131072) + (ClientSize.Height * 4) + (videoOn ? 2 : 0) + (videoFull ? 1 : 0);
            if (huella != layoutStamp)
            {
                layoutStamp = huella;
                uiOverlaySig = SigNada;
                if (gpuPanel != null) gpuPanel.UiUrgent = true;
            }
#endif
        }

        private void LayoutSongsPanel()
        {
            int w = panelSongs.ClientSize.Width, h = panelSongs.ClientSize.Height;
            lblAlbum.SetBounds(8, 8, Math.Max(50, w - 16), 32);
            int cube = Math.Min(h - 80, (int)(w * 0.40));
            if (cube < 80) cube = 80;
            cubeBox.SetBounds(12, 52, cube, cube);
            int listX = cube + 30, listW = Math.Max(60, w - cube - 42);
            if (songPager != null) songPager.SetBounds(listX, 52, listW, 24);   // instrucciones de pagina, ARRIBA de la lista
            songList.SetBounds(listX, 80, listW, Math.Max(60, h - 88));
            // La lista contiene SOLO la pagina actual (sin scroll); al cambiar el tamaño se reparte de nuevo.
            if (panelSongs.Visible && songItems.Count > 0 && songList.RowH > 0) FillSongPage();
        }

        private void Letter_Refresh() { letterBar.Current = currentLetter; letterBar.Invalidate(); }

        private static readonly string[] AudioExt = { ".mp3", ".wav" };
        private static readonly string[] VideoExt = { ".mp4", ".avi", ".wmv", ".mpg", ".mpeg", ".mov", ".mkv", ".flv", ".m4v", ".vob", ".divx", ".3gp", ".asf" };

        private void LoadMusicFolder()
        {
            List<string> roots = AllMusicRoots();   // carpeta "musica" principal + las agregadas con Ctrl+F2 (otros discos)
            if (roots.Count > 0) StartScan(roots);
            else lblStatus.Text = "No encontre una carpeta 'musica' junto al programa. Crea una carpeta llamada musica al lado del .exe (o agrega una con Ctrl+F2).";
        }

        private List<string> scanRoots;   // raices del escaneo en curso/vigente (las usa el worker; solo se asignan aqui)

        private void StartScan(List<string> roots)
        {
            if (scanner != null && scanner.IsBusy) return;   // ya hay un escaneo en curso (doble F5): ignorar
            libBuilding = true;   // desde aqui y hasta Completed, las listas son del worker
            if (searchOpen) CloseSearch();            // un re-escaneo invalida lo que el buscador muestra
            lastQuery = null; lastMatches = null;     // ...y su cache incremental (evita resultados huerfanos)
            tracks.Clear(); albums.Clear(); byLetter.Clear(); letterAvailable.Clear(); searchIndex.Clear();
            BackToAlbums();
            scanRoots = roots;
            lblStatus.Text = "Buscando musica en: " + roots[0] + (roots.Count > 1 ? (" (+" + (roots.Count - 1) + " carpetas mas)") : "") + " ...";

            scanner = new BackgroundWorker();
            scanner.WorkerReportsProgress = true;
            scanner.DoWork += Scanner_DoWork;
            scanner.ProgressChanged += Scanner_ProgressChanged;
            scanner.RunWorkerCompleted += Scanner_RunWorkerCompleted;
            scanner.RunWorkerAsync(roots);
        }

        private void Scanner_DoWork(object sender, DoWorkEventArgs e)
        {
            List<string> roots = (List<string>)e.Argument;
            // CACHE DE BIBLIOTECA: si es valido, evita las DOS caminatas del disco (en HDD eran minutos).
            List<Track> ctk; List<string> cvd; Dictionary<string, string> ccv;
            if (TryLoadLibraryCache(roots, out ctk, out cvd, out ccv))
            {
                // ARMADO EN SEGUNDO PLANO: ordenar e indexar las 118 mil canciones tomaba 1-3 segundos
                // DEL HILO DE LA INTERFAZ al arrancar; aqui corre en el worker y el arranque no se congela.
                tracks.AddRange(ctk);
                cachedVideoPaths = cvd; cachedCovers = ccv;
                BuildAlbums(roots); BuildVideoSection(roots); BuildSearchIndex();
                e.Result = true;    // vino del cache
                return;
            }
            // UN SOLO RECORRIDO del disco. Antes se caminaba el arbol DOS veces (una por el audio y otra
            // por el video) y ademas se pedia la lista de cada carpeta OTRA vez para buscar la portada: tres
            // lecturas de cada carpeta. Ahora se camina una sola vez y de esa misma lista salen las tres cosas.
            // El resultado se deja en cachedVideoPaths/cachedCovers, que es EXACTAMENTE lo que ya recibe el
            // camino del cache: de ahi en adelante todo sigue igual y sin volver a tocar el disco.
            List<Track> found = new List<Track>();
            List<string> audio = new List<string>();
            List<string> vids = new List<string>();
            Dictionary<string, string> covs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int r = 0; r < roots.Count; r++) ScanFolders(roots[r], audio, vids, covs);
            for (int i = 0; i < audio.Count; i++) found.Add(Track.FromFile(audio[i]));
            tracks.AddRange(found);
            cachedVideoPaths = vids; cachedCovers = covs;
            BuildAlbums(roots); BuildVideoSection(roots); BuildSearchIndex();   // tambien en segundo plano
            e.Result = false;   // escaneo fresco
        }

        private void Scanner_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblStatus.Text = "Encontradas " + ((int)e.UserState) + " canciones...";
        }

        private void Scanner_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            libBuilding = false;   // las listas vuelven a ser de la interfaz (tambien si hubo error)
            if (e.Error != null) { lblStatus.Text = "Error al escanear: " + e.Error.Message; return; }

            bool fromCache = (e.Result is bool) ? (bool)e.Result : false;

            letterAvailable.Clear();
            foreach (KeyValuePair<char, List<Album>> kv in byLetter) if (kv.Value.Count > 0) letterAvailable.Add(kv.Key);
            if (videoAlbums != null && videoAlbums.Count > 0) letterAvailable.Add(VideoMark);
            topDirty = true;   // OPTIMIZACION (TOP): la biblioteca se re-escaneo (nuevos Album/Track) -> reconstruir
            BuildTopDiscs();
            if (topDiscs != null && topDiscs.Count > 0) letterAvailable.Add(TopMark);   // hay estadisticas -> seccion "Lo mas sonado"

            string order = "\u2605#ABCDEFGHIJKLMNOPQRSTUVWXYZ\u25BA";
            currentLetter = '#';
            // default: primera letra REAL disponible (NO arrancar en una seccion especial)
            bool gotReal = false;
            for (int i = 0; i < order.Length; i++) { char c = order[i]; if (c == TopMark || c == VideoMark) continue; if (letterAvailable.Contains(c)) { currentLetter = c; gotReal = true; break; } }
            if (!gotReal) for (int i = 0; i < order.Length; i++) if (letterAvailable.Contains(order[i])) { currentLetter = order[i]; break; }
            page = 0; selAlbumIndex = 0;
            if (albumView != null) albumView.ResetCellCache();   // re-escaneo: descarta celdas horneadas (etiqueta/portada pueden haber cambiado para el mismo Folder)
            RefreshAlbums();

            if (albums.Count == 0) lblStatus.Text = "No encontre MP3 en esa carpeta: " + MusicFolder;
            else lblStatus.Text = "";   // las instrucciones las muestra la barra de ayuda (formas/colores)

            StartThumbWarm();   // en segundo plano: construir las miniaturas que falten (1a vez tarda; luego todo vuela)
            StartDurScan();     // barrido RAPIDO: la duracion de toda la biblioteca en minutos (no espera al analisis pesado)
            StartPreScan();     // en segundo plano: medir volumen y duracion de las canciones nuevas (nivelado desde la 1a vez)

            if (!fromCache) SaveLibraryCacheAsync();   // escaneo fresco: guardar la biblioteca para el proximo arranque
            cachedVideoPaths = null; cachedCovers = null; lastVideoPaths = null;

            // REANUDACION tras corte de luz: con la biblioteca ya armada, restaura creditos/cola/cancion
            // guardados (solo el PRIMER armado; un F5 a media jornada no toca lo que este sonando).
            if (!stateRestored && nowPlaying == null && queue.Count == 0) RestoreState();
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
        // ~60 s bien elegidos dan practicamente la misma sonoridad integrada que el tema completo, a una fraccion
        // del costo. Lo medido se guarda en disco cada pocas canciones: se puede apagar la rocola sin perder nada.
        private bool eqDirty = false;             // hay huellas de EQ nuevas sin guardar (las escribe FlushEqIfDirty)
        // Vuelca las huellas del EQ recien medidas. Se llama de tanto en tanto (no por cancion): recalcular el
        // promedio de la biblioteca y reescribir el archivo en cada tema seria puro desgaste de disco.
        private void FlushEqIfDirty()
        {
            if (!eqDirty) return;
            eqDirty = false;
            RecomputeEqAvg();
            SaveEqCache();
        }
        // ---------- Archivo con el material de entrenamiento de la IA ----------
        // Formato binario compacto (millones de pares caben sin problema): cabecera "RIAD" + version + tamaños,
        // y luego cada par como 28 float de entrada + 12 float de objetivo. numpy lo lee de un tiron.
        private const int IaFramesPerSong = 192;   // cuadros analizados por cancion (suficiente y barato)
        private const int IaDeepFrames = 1536;     // A FONDO: cuadros APROVECHADOS, repartidos por TODA la cancion
        private const int IaWarmFrames = 64;       // los primeros no se usan: el acantilado aun se esta midiendo
        // TOPE del archivo de material. Existe para que el archivo siga siendo MANEJABLE: hay que poder copiarlo
        // a una USB y cargarlo en la PC donde se entrena. 24 GB cubren de sobra una biblioteca de ~20 mil
        // canciones aptas cosechadas a fondo, y caben holgados en una memoria de 64 GB (con formato exFAT o NTFS;
        // en FAT32 ningun archivo puede pasar de 4 GB, asi que ahi habria que bajarlo). El entrenador NO necesita
        // 24 GB de RAM: toma una muestra repartida por todo el archivo (ver ia/entrenar_ia.py). Se cambia con
        // ia_datos_mb= en la configuracion; 0 lo quita del todo.
        private long iaMaxBytes = 24L * 1024 * 1024 * 1024;
        private bool IaBudgetFull() { return iaMaxBytes > 0 && (long)iaPairsTotal * 160L + 16L >= iaMaxBytes; }
        private string IaInfoText()
        {
            long mb = ((long)iaPairsTotal * 160L + 16L) / (1024 * 1024);
            string tam = (mb >= 1024) ? ((mb / 1024.0).ToString("0.0") + " GB") : (mb.ToString("#,0") + " MB");
            return iaPairsTotal.ToString("#,0") + " ejemplos (" + tam
                   + (IaBudgetFull() ? ", tope alcanzado" : "") + ")";
        }
        private static readonly float[] IaCutsHz = { 11000f, 13000f, 15000f, 16500f, 18000f };   // cortes simulados
        // Las mismas rutas viven en varias tablas (sonoridad, duracion, EQ, cosecha de IA, repaso a fondo) y en
        // la propia lista de canciones. Con cien mil canciones eso eran CINCO copias del mismo texto, decenas de
        // megas tirados en una maquina de 4 GB. Compartiendo UNA sola copia por ruta se juntan todas en una, y
        // de paso las busquedas salen mas rapido (comparar dos iguales se resuelve por referencia).
        private static string Pool(string s) { return (s == null || s.Length == 0) ? s : string.Intern(s); }
        private string IaDataFile() { return Path.Combine(Application.StartupPath, "rocola_ia_datos.bin"); }
        private string IaDoneFile() { return Path.Combine(Application.StartupPath, "rocola_ia_hechas.txt"); }
        // Formato: "ruta" (nivel 1, el de siempre) o "2|ruta" (nivel 2). Una ruta de Windows no puede llevar
        // "|", asi que distinguirlos es seguro y los archivos viejos se siguen leyendo tal cual. Si una cancion
        // aparece dos veces (subio de nivel) manda el nivel MAS ALTO.
        private void LoadIaDone()
        {
            try
            {
                string f = IaDoneFile();
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f))
                {
                    string r = line.Trim(); if (r.Length == 0) continue;
                    int niv = 1;
                    if (r.Length > 2 && r[1] == '|') { int d = r[0] - '0'; if (d >= 1 && d <= 9) { niv = d; r = r.Substring(2); } }
                    r = Pool(r);
                    int ant; if (iaDone.TryGetValue(r, out ant) && ant >= niv) continue;
                    iaDone[r] = niv;
                }
            }
            catch { }
        }
        private readonly object iaDoneLock = new object();
        private volatile bool iaDoneDirty = false;   // hay canciones nuevas cosechadas sin apuntar en disco
        private readonly List<string> iaPend = new List<string>();   // lineas apuntadas y aun sin escribir
        // Apunta que de esta cancion ya se cosecho hasta "nivel". false si ya estaba en ese nivel o mas.
        private bool MarkIaDone(string path, int nivel)
        {
            lock (iaDoneLock)
            {
                int ant; if (iaDone.TryGetValue(path, out ant) && ant >= nivel) return false;
                iaDone[path] = nivel;
                iaPend.Add((nivel > 1 ? (nivel.ToString() + "|") : "") + path);
            }
            iaDoneDirty = true;
            return true;
        }
        // Se ANADE al final en vez de reescribir el archivo entero. Con cien mil canciones, reescribirlo cada
        // 10 canciones durante el repaso a fondo eran megas y megas al disco cada minuto, para nada.
        private void SaveIaDone()
        {
            List<string> nuevas;
            lock (iaDoneLock) { if (iaPend.Count == 0) return; nuevas = new List<string>(iaPend); iaPend.Clear(); }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    lock (iaDoneLock) File.AppendAllText(IaDoneFile(), sb.ToString());
                }
                catch { }
            });
        }
        // ---- Canciones ya repasadas A FONDO (mismo formato que las de la IA: una ruta por linea) ----
        private string DeepDoneFile() { return Path.Combine(Application.StartupPath, "rocola_afondo.txt"); }
        private readonly object deepDoneLock = new object();
        private volatile bool deepDoneDirty = false;   // hay canciones repasadas sin apuntar en disco
        private readonly List<string> deepPend = new List<string>();   // rutas apuntadas y aun sin escribir
        private void LoadDeepDone()
        {
            try
            {
                string f = DeepDoneFile();
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f)) { string r = line.Trim(); if (r.Length > 0) deepDone.Add(Pool(r)); }
            }
            catch { }
        }
        private void MarkDeepDone(string path)
        {
            lock (deepDoneLock) { if (!deepDone.Add(path)) return; deepPend.Add(path); }
            deepDoneDirty = true;
        }
        // Igual que la lista de la IA: se ANADE al final, no se reescribe entera cada 10 canciones.
        private void SaveDeepDone()
        {
            List<string> nuevas;
            lock (deepDoneLock) { if (deepPend.Count == 0) return; nuevas = new List<string>(deepPend); deepPend.Clear(); }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    lock (deepDoneLock) File.AppendAllText(DeepDoneFile(), sb.ToString());
                }
                catch { }
            });
        }
        private void IaCountPairs()
        {
            try
            {
                FileInfo fi = new FileInfo(IaDataFile());
                iaPairsTotal = (fi.Exists && fi.Length > 16) ? (int)((fi.Length - 16) / 160) : 0;
            }
            catch { iaPairsTotal = 0; }
        }
        // Añade al archivo los pares de UNA cancion (solo se llama con canciones que si conservan sus agudos).
        private void IaAppendPairs(List<float> pairs)
        {
            if (pairs == null || pairs.Count < 40) return;
            try
            {
                string f = IaDataFile();
                bool nuevo = !File.Exists(f) || new FileInfo(f).Length < 16;
                using (FileStream fs = new FileStream(f, FileMode.Append, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    if (nuevo) { bw.Write((byte)'R'); bw.Write((byte)'I'); bw.Write((byte)'A'); bw.Write((byte)'D'); bw.Write(1); bw.Write(28); bw.Write(12); }
                    for (int i = 0; i < pairs.Count; i++) bw.Write(pairs[i]);
                }
                iaPairsTotal += pairs.Count / 40;
            }
            catch { }
        }

        // ---- Duraciones rapidas: "segundos|ruta" por linea, escritas POR ANADIDO ----
        private string DurFastFile() { return Path.Combine(Application.StartupPath, "rocola_duracion.txt"); }
        private void LoadDurFast()
        {
            try
            {
                string f = DurFastFile();
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f))
                {
                    int bar = line.IndexOf('|');
                    if (bar <= 0 || bar >= line.Length - 1) continue;
                    float d;
                    if (!float.TryParse(line.Substring(0, bar), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) || d <= 0.01f) continue;
                    durFast[Pool(line.Substring(bar + 1))] = d;
                }
            }
            catch { }
        }
        private void SaveDurFast()
        {
            List<string> nuevas;
            lock (durFastLock) { if (durFastPend.Count == 0) return; nuevas = new List<string>(durFastPend); durFastPend.Clear(); }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    lock (durFastLock) File.AppendAllText(DurFastFile(), sb.ToString());
                }
                catch { }
            });
        }

        // Barrido RAPIDO de duraciones: abre cada archivo, lee cuanto dura y lo suelta. No descomprime nada.
        // Se salta lo que ya tiene duracion (por esta via o por el analisis completo). Prioridad minima y un
        // respiro por archivo para no quitarle disco a la musica; con video en pantalla se detiene del todo.
        private void StartDurScan()
        {
            int myGen = ++durScanGen;
            List<string> pend = new List<string>();
            for (int i = 0; i < tracks.Count; i++)
            {
                Track tk = tracks[i];
                if (tk == null || tk.Path == null) continue;
                if (durCache.ContainsKey(tk.Path) || durFast.ContainsKey(tk.Path)) continue;
                pend.Add(tk.Path);
            }
            durScanLeft = pend.Count;
            if (pend.Count == 0) return;
            Diag("duraciones: faltan " + pend.Count);
            durScanTh = new System.Threading.Thread(delegate ()
            {
                List<string> lote = new List<string>(128);
                List<float> lotd = new List<float>(128);
                try
                {
                    System.Threading.Thread.Sleep(4000);   // deja respirar el arranque
                    for (int i = 0; i < pend.Count; i++)
                    {
                        if (preScanStop || myGen != durScanGen) break;
                        while (!preScanStop && myGen == durScanGen && videoOn) System.Threading.Thread.Sleep(500);
                        if (preScanStop || myGen != durScanGen) break;
                        string ruta = pend[i];
                        float seg = 0f;
                        try
                        {
                            using (AudioFileReader rd = new AudioFileReader(ruta))
                            {
                                double t = rd.TotalTime.TotalSeconds;
                                if (t > 0.05 && t < 36000) seg = (float)t;
                            }
                        }
                        catch { seg = 0f; }
                        if (seg > 0.01f) { lote.Add(ruta); lotd.Add(seg); }
                        // Publica el lote cada 128 archivos o al final, TAMBIEN si viene vacio: un archivo roto
                        // no da duracion pero SI es avance; sin esto, el contador de "faltan" del panel se
                        // quedaria clavado para siempre en el numero de archivos danados.
                        bool ultimo = (i == pend.Count - 1);
                        if (lote.Count >= 128 || ultimo)
                        {
                            string[] rr = lote.ToArray(); float[] dd = lotd.ToArray();
                            lote.Clear(); lotd.Clear();
                            int faltan = pend.Count - (i + 1);
                            try
                            {
                                if (!IsHandleCreated) break;
                                BeginInvoke((MethodInvoker)delegate { PublicaDuraciones(myGen, rr, dd, faltan); });
                            }
                            catch { break; }
                        }
                        System.Threading.Thread.Sleep(diskSlow ? 12 : 2);
                    }
                }
                catch { }
            });
            durScanTh.IsBackground = true;
            durScanTh.Priority = System.Threading.ThreadPriority.Lowest;
            durScanTh.Start();
        }
        // Corre en el hilo de la interfaz: las tablas son suyas. "faltan" es el avance ABSOLUTO que reporta el
        // hilo (cuenta tambien los archivos rotos, que no traen duracion pero ya quedaron atras).
        private void PublicaDuraciones(int gen, string[] rutas, float[] segs, int faltan)
        {
            if (gen != durScanGen) return;   // generacion caducada (hubo re-escaneo): descartar
            for (int i = 0; i < rutas.Length; i++)
            {
                if (durFast.ContainsKey(rutas[i])) continue;
                durFast[rutas[i]] = segs[i];
                lock (durFastLock) durFastPend.Add(segs[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + rutas[i]);
            }
            durScanLeft = faltan < 0 ? 0 : faltan;
            SaveDurFast();
            if (rutas.Length > 0 && panelSongs != null && panelSongs.Visible && songList != null)
            {
                songList.Invalidate();
#if ROCOLA_GPU
                uiOverlaySig = SigNada;   // la lista va horneada en la textura de UI: hay que rehacerla para que se vean
#endif
            }
        }

        private const double PreSegSec = 20.0;    // duracion de cada tramo analizado
        private const int PreSegs = 3;            // cuantos tramos por cancion (repartidos por el tema)
        private void StartPreScan()
        {
            preScanStop = false;
            // Cada llamada (arranque o re-escaneo con F5) abre una GENERACION nueva: el hilo anterior se da
            // por caducado en cuanto lo nota y sale solo. Antes se volvia con "ya hay uno trabajando", asi que
            // la musica AGREGADA despues no se media hasta reiniciar la rocola.
            int myGen = ++preScanGen;
            bool turbo = preScanTurbo;
            List<string> pend = new List<string>();
            int elegibles = 0;                 // canciones de la biblioteca que cuentan para la barra de avance
            for (int i = 0; i < tracks.Count; i++)
            {
                Track tk = tracks[i];
                if (tk == null || tk.Path == null) continue;
                // En modo NORMAL se salta lo ya medido. En ENTRENAMIENTO A FONDO entra toda la biblioteca MENOS
                // lo que ya recibio su repaso completo: asi, al detenerlo y volverlo a encender (o tras un apagon)
                // CONTINUA donde iba en vez de empezar por la primera cancion otra vez.
                elegibles++;
                if (!turbo && durCache.ContainsKey(tk.Path)) continue;
                if (turbo) { bool ya; lock (deepDoneLock) ya = deepDone.Contains(tk.Path); if (ya) continue; }
                pend.Add(tk.Path);
            }
            // CADA cuanto se vuelcan las tablas a disco. Un volcado reescribe ENTERAS las tablas de sonoridad y
            // de EQ: con cien mil canciones son ~17 MB por volcado. Hacerlo cada 10 canciones durante un repaso a
            // fondo son CIENTOS de gigas escritos para nada, y eso desgasta el SSD. Asi que el intervalo crece con
            // la biblioteca: en una chica se guarda tan seguido como siempre; en una grande, cada tanto (como mucho
            // se pierde un rato de trabajo si se va la luz, y lo perdido solo se vuelve a medir).
            int guardaCada = turbo ? 10 : 25;
            int escala = loudCache.Count / 500;                        // se calcula aqui, en el hilo de la interfaz
            if (escala > guardaCada) guardaCada = escala;              // (las tablas son suyas: leerlas desde el hilo de fondo seria una carrera)
            if (guardaCada > 300) guardaCada = 300;
            preScanLeft = pend.Count;
            // La barra del entrenamiento a fondo se mide sobre TODA la biblioteca (no sobre la tanda que queda),
            // para que al reanudar siga desde donde iba en vez de volver a arrancar en cero.
            preScanTotal = turbo ? elegibles : pend.Count;
            if (pend.Count == 0) return;
            preScanTh = new System.Threading.Thread(delegate ()
            {
                int hechas = 0;
                try
                {
                    if (!turbo) System.Threading.Thread.Sleep(15000);   // dejar respirar el arranque (y al calentador de caratulas)
                    for (int i = 0; i < pend.Count; i++)
                    {
                        if (preScanStop || myGen != preScanGen) break;
                        // En modo normal se espera a que la rocola este en reposo (si alguien la usa, o hay un
                        // video, este hilo no toca nada). En ENTRENAMIENTO A FONDO no espera a nadie: para eso
                        // se enciende, para que la maquina se dedique por completo a dejar todo calibrado.
                        while (!turbo && !preScanStop && myGen == preScanGen && (Environment.TickCount - lastInputTick < 8000 || videoOn))
                            System.Threading.Thread.Sleep(500);
                        if (preScanStop || myGen != preScanGen) break;
                        string ruta = pend[i];
                        float rms, dur; float[] eqCurve;
                        if (!MeasureFile(ruta, turbo, out rms, out dur, out eqCurve)) { preScanLeft--; continue; }
                        // publicar en el hilo de la interfaz (las tablas son suyas): sin carreras
                        try
                        {
                            if (!IsHandleCreated || preScanStop || myGen != preScanGen) break;
                            BeginInvoke((MethodInvoker)delegate
                            {
                                if (myGen != preScanGen) return;   // llego de una generacion caducada: descartar
                                if (dur > 0.01f) durCache[ruta] = dur;
                                // SOBRESCRIBE lo que hubiera: la medida del pre-analisis es de la cancion COMPLETA,
                                // asi que es mejor que lo aprendido a medias oyendola (que promediaba entre pasadas).
                                if (rms > 0.0001f) loudCache[ruta] = rms;
                                if (eqCurve != null) { eqCache[ruta] = eqCurve; eqCacheVer++; eqDirty = true; }   // huella tonal del EQ automatico, ya lista
                                if (preScanLeft > 0) preScanLeft--;
                                if (panelSongs != null && panelSongs.Visible && songList != null) songList.Invalidate();   // que aparezca la duracion recien sabida
                            });
                        }
                        catch { break; }
                        // Apuntada como repasada a fondo (despues de encargar la publicacion, no antes: si algo
                        // fallara al publicar no queremos darla por hecha sin haber guardado su medida).
                        if (turbo) MarkDeepDone(ruta);
                        hechas++;
                        if ((hechas % guardaCada) == 0)
                            try { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { SaveLoudCache(); FlushEqIfDirty(); if (iaDoneDirty) { iaDoneDirty = false; SaveIaDone(); } if (deepDoneDirty) { deepDoneDirty = false; SaveDeepDone(); } }); } catch { }
                        // Respiro entre archivos. En ENTRENAMIENTO A FONDO practicamente no hay: la maquina se
                        // dedica a esto. En modo normal, con algo SONANDO se descansa mas (decodificar cuesta CPU
                        // y no vale la pena arriesgar un corte), y la espera se ajusta al equipo: con UN SOLO
                        // nucleo hay que frenar fuerte; con dos o mas sobra maquina y puede ir mas rapido.
                        int respiro;
                        if (turbo) respiro = 1;
                        else if (nowPlaying != null) respiro = (Environment.ProcessorCount >= 2) ? (diskSlow ? 220 : 120) : (diskSlow ? 700 : 400);
                        else respiro = diskSlow ? 120 : 40;
                        System.Threading.Thread.Sleep(respiro);
                    }
                }
                catch { }
                finally
                {
                    try { if (IsHandleCreated && hechas > 0) BeginInvoke((MethodInvoker)delegate { SaveLoudCache(); FlushEqIfDirty(); if (iaDoneDirty) { iaDoneDirty = false; SaveIaDone(); } if (deepDoneDirty) { deepDoneDirty = false; SaveDeepDone(); } }); } catch { }
                }
            });
            preScanTh.IsBackground = true;
            // NUNCA por delante de la pantalla ni de la musica. A fondo iba en prioridad NORMAL, la misma que la
            // interfaz: en la maquina de dos nucleos ese hilo -que decodifica canciones enteras sin parar- le
            // quitaba el turno al dibujado, y el fondo estrellado se veia ir lento o a tirones. Por debajo de lo
            // normal sigue quedandose con TODO el tiempo que la interfaz no usa (que es casi todo), pero en
            // cuanto hay un cuadro que dibujar, manda el cuadro. En automatico, la minima de todas.
            preScanTh.Priority = turbo ? System.Threading.ThreadPriority.BelowNormal : System.Threading.ThreadPriority.Lowest;
            preScanTh.Start();
        }

        // Mide un archivo SIN reproducirlo: sonoridad K-weighted (BS.1770, igual que el nivelado en vivo), duracion
        // y la HUELLA TONAL del EQ automatico (8 bandas, mismo bandpass y misma referencia que al oirla). Analiza
        // PreSegs tramos repartidos por el tema (o el tema entero si es corto). Devuelve false si no se pudo.
        // "aFondo" = modo ENTRENAMIENTO A FONDO: analiza la cancion COMPLETA en vez de por tramos (la medida
        // mas exacta posible) y saca mucho mas material para la IA. En automatico se queda con el muestreo,
        // que da practicamente la misma sonoridad a una fraccion del costo.
        private bool MeasureFile(string path, bool aFondo, out float rms, out float durSec, out float[] eqCurve)
        {
            rms = 0f; durSec = 0f; eqCurve = null;
            AudioFileReader rd = null;
            try
            {
                rd = new AudioFileReader(path);
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
                float[][] bp = new float[8][]; float[] bpSt = new float[8 * 4];   // huella tonal: bandpass por banda
                for (int b = 0; b < 8; b++) bp[b] = EqBpf(EqCentros[b], rate);
                double[] bpSum = new double[8]; long bpN = 0;
                // COSECHA PARA LA IA: se analiza el canal 0 con el MISMO motor del DSEE. Los pares se guardan
                // en memoria y solo se escriben al final, si resulta que la cancion SI conserva sus agudos
                // (una cancion dañada no puede enseñar nada: los agudos reales ya no estan en el archivo).
                SpectralTap hv = null; float[] hvFrame = null, hvFeat = null, hvTgt = null;
                int hvFill = 0, hvFrames = 0; List<float> hvPairs = null;
                int hvStride = 1, hvFrameMax = IaFramesPerSong;
                // El ACANTILADO se mide SIEMPRE, cosechemos o no para la IA. Es el dato que dice si una cancion
                // llego completa o el codec le talo los agudos, y de eso dependen dos cosas importantes: si sirve
                // para fijar el objetivo del EQ automatico, y que bandas suyas tiene sentido corregir. Cuesta unas
                // pocas decenas de FFT por cancion, nada al lado de descomprimirla entera.
                bool soloCorte = false;
                // Los dos modos ACUMULAN. Antes bastaba con que el modo automatico hubiera tocado la cancion
                // (192 cuadros) para que el repaso A FONDO ya no la mirara NUNCA, y se perdian para siempre los
                // 12x cuadros que ese repaso saca de ella. Ahora se guarda el NIVEL cosechado: el repaso a fondo
                // vuelve a entrar en las que solo tienen el muestreo y les anade el resto del material.
                int nivelPrevio; lock (iaDoneLock) { if (!iaDone.TryGetValue(path, out nivelPrevio)) nivelPrevio = 0; }
                int nivelAhora = aFondo ? 2 : 1;
                bool cosechar = iaHarvestOn && nivelPrevio < nivelAhora && !IaBudgetFull();
                if (!cosechar)
                {
                    // Solo hace falta el acantilado: se analizan unos pocos cuadros y se para (ni se guardan pares).
                    try { hv = new SpectralTap(rate, 1); hvFrame = new float[hv.FrameLen]; soloCorte = true; hvFrameMax = IaWarmFrames + 24; }
                    catch { hv = null; }
                }
                if (cosechar)
                {
                    try
                    {
                        hv = new SpectralTap(rate, 1);
                        hvFrame = new float[hv.FrameLen]; hvFeat = new float[28]; hvTgt = new float[12];
                        hvPairs = new List<float>(4096);
                        // A FONDO: antes se amontonaban cuadros SEGUIDOS desde el principio del tema (2304 de golpe,
                        // que ni llegaban al primer minuto). Dos cuadros pegados de la misma cancion se parecen
                        // tanto que apilarlos engorda el archivo sin ensenarle nada nuevo a la red. Ahora se toma
                        // uno de cada tantos, REPARTIDOS por toda la cancion: menos bytes, mucha mas variedad, y
                        // ademas encuentra los buenos tramos esten donde esten (no solo al principio).
                        if (aFondo)
                        {
                            long framesTot = (long)(total * rate / Math.Max(1, hv.FrameLen));
                            // El paso se REDONDEA HACIA ARRIBA a proposito: con la division a secas el ultimo
                            // cuadro caia antes del final y se quedaba sin mirar el ultimo 15% de cada cancion.
                            long paso = (framesTot - IaWarmFrames + IaDeepFrames - 1) / IaDeepFrames;
                            hvStride = (paso > 1) ? (int)paso : 1;
                            hvFrameMax = IaWarmFrames + hvStride * IaDeepFrames;
                        }
                    }
                    catch { hv = null; }
                }
                // tramos: si el tema es corto se analiza entero; si no, PreSegs ventanas repartidas evitando
                // el arranque y el final (intros/silencios finales no representan la sonoridad del tema).
                // A FONDO: un solo "tramo" que abarca la cancion entera. En automatico: PreSegs ventanas.
                int segs = (aFondo || total <= PreSegSec * PreSegs) ? 1 : PreSegs;
                double segLen = (segs == 1) ? total : PreSegSec;
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
                        int want2 = (int)Math.Min(buf.Length, want - got);
                        int n = rd.Read(buf, 0, want2);
                        if (n <= 0) break;
                        got += n;
                        for (int i = 0; i + ch <= n; i += ch)
                        {
                            // HUELLA TONAL (EQ automatico): igual que en vivo, sobre la mezcla MONO del cuadro.
                            float m = buf[i];
                            for (int mc = 1; mc < ch; mc++) m += buf[i + mc];   // mc: el nombre c2 ya es el coeficiente del K-weighting
                            m /= ch;
                            for (int b2 = 0; b2 < 8; b2++)
                            {
                                float[] cf = bp[b2]; if (cf == null) continue;
                                int q = b2 * 4;
                                float o = cf[0] * m + cf[1] * bpSt[q] + cf[2] * bpSt[q + 1] - cf[3] * bpSt[q + 2] - cf[4] * bpSt[q + 3];
                                bpSt[q + 1] = bpSt[q]; bpSt[q] = m;
                                bpSt[q + 3] = bpSt[q + 2]; bpSt[q + 2] = o;
                                bpSum[b2] += o * o;
                            }
                            bpN++;
                            // canal 0 hacia el analizador de la IA (el DSEE tambien trabaja por canal)
                            if (hv != null && hvFrames < hvFrameMax)
                            {
                                hvFrame[hvFill++] = buf[i];
                                if (hvFill >= hvFrame.Length)
                                {
                                    hvFill = 0;
                                    double en = 0; for (int q = 0; q < hvFrame.Length; q++) en += hvFrame[q] * hvFrame[q];
                                    if (en / hvFrame.Length > 1e-8)   // saltar cuadros practicamente mudos
                                    {
                                        hv.AnalyzeFrame(hvFrame, 0);
                                        hvFrames++;
                                        if (soloCorte) { }   // solo queriamos el acantilado: nada que emparejar
                                        else if (hvFrames > IaWarmFrames && (hvStride == 1 || (hvFrames % hvStride) == 0))   // acantilado ya estabilizado; a fondo, repartido
                                            for (int q = 0; q < IaCutsHz.Length; q++)
                                                if (hv.MakePair(IaCutsHz[q], hvFeat, hvTgt))
                                                {
                                                    for (int z = 0; z < 28; z++) hvPairs.Add(hvFeat[z]);
                                                    for (int z = 0; z < 12; z++) hvPairs.Add(hvTgt[z]);
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
                // Solo las canciones COMPLETAS (sin acantilado, el mismo umbral que usa el DSEE para no tocarlas)
                // sirven de maestras: en las dañadas los agudos reales ya no existen y no hay nada que enseñar.
                if (hv != null && !soloCorte && hvPairs.Count >= 40 && hv.CutHz >= EqSanaHz)
                {
                    IaAppendPairs(hvPairs);
                    MarkIaDone(path, nivelAhora);
                }
                float corteHz = (hv != null) ? hv.CutHz : 0f;
                float corteLo = (hv != null) ? hv.CutLoHz : 0f;
                float recorte = (nSamp > 0) ? (float)((double)clipN / nSamp) : 0f;
                if (nSamp < 40000) return durSec > 0.01f;   // muy poco audio util: al menos queda la duracion
                rms = (float)Math.Sqrt(sumSq / nSamp);
                // Huella tonal en dB por banda, referida a la MEDIANA del cuerpo (158 Hz - 5.6 kHz): exactamente
                // la misma cuenta que hace FinalizeEqMeasure al aprenderla oyendo la cancion.
                if (bpN >= 40000)
                {
                    float[] curve = new float[EqLen];
                    curve[EqCut] = corteHz; curve[EqClip] = recorte; curve[EqCutLo] = corteLo;
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

        // Duracion "m:ss" de una cancion ya pre-analizada ("" si aun no se sabe).
        private string DurText(string path)
        {
            float d;
            if (path == null) return "";
            // Manda la del analisis completo (mide la cancion entera); si aun no la tiene, la del barrido rapido.
            if (!durCache.TryGetValue(path, out d) || d <= 0.01f)
                if (!durFast.TryGetValue(path, out d) || d <= 0.01f) return "";
            int tot = (int)(d + 0.5f);
            return (tot / 60) + ":" + (tot % 60).ToString("00");
        }
        private void StartThumbWarm()
        {
            warmGen++;
            int myGen = warmGen;
            List<Album> snap = new List<Album>(albums);
            System.Threading.Thread t = new System.Threading.Thread(delegate ()
            {
                try
                {
                    System.Threading.Thread.Sleep(12000);   // dejar respirar el arranque
                    for (int i = 0; i < snap.Count; i++)
                    {
                        if (myGen != warmGen) return;   // recargaron la musica: este hilo ya caduco
                        Album a = snap[i];
                        if (a == null || a.CoverPath == null) continue;
                        try { if (File.Exists(ThumbCachePath(a.CoverPath))) continue; } catch { continue; }
                        // si alguien uso la rocola hace poco, esperar (no robarle CPU ni DISCO a la interfaz)
                        while (Environment.TickCount - lastInputTick < 8000)
                        {
                            System.Threading.Thread.Sleep(400);
                            if (myGen != warmGen) return;
                        }
                        // si hay caratulas del primer plano en camino/formadas, o un VIDEO reproduciendose
                        // (el video lee del disco SIN colchon), cederles el disco por completo
                        while (true)
                        {
                            int busy; lock (coverLock) { busy = coverLoading.Count + coverQHi.Count + coverQLo.Count; }
                            if (busy == 0 && !videoOn) break;
                            System.Threading.Thread.Sleep(300);
                            if (myGen != warmGen) return;
                        }
                        coverSem.WaitOne();   // mismo turno unico de disco que las cargas normales
                        try
                        {
                            if (myGen != warmGen) return;
                            // si JUSTO llego trabajo del primer plano, soltar el turno y reintentar despues
                            bool fgBusy;
                            lock (coverLock) { fgBusy = (coverLoading.Count + coverQHi.Count + coverQLo.Count) > 0; }
                            if (fgBusy) { i--; System.Threading.Thread.Sleep(60); continue; }
                            Image im = DecodeCover(a.CoverPath);   // crea la miniatura en disco como efecto
                            if (im != null) im.Dispose();          // no ocupar memoria: solo queremos el disco
                        }
                        catch { }
                        finally { coverSem.Release(); }
                        System.Threading.Thread.Sleep(diskSlow ? 150 : 60);   // respiro mas largo en disco duro
                    }
                }
                catch { }
            });
            t.IsBackground = true;
            t.Priority = System.Threading.ThreadPriority.Lowest;
            t.Start();
        }

        // ===================== CACHE DE BIBLIOTECA (arranque rapido) =====================
        // El arranque hacia DOS caminatas completas del disco (audio y video) MAS ~una verificacion de
        // portada por cada disco: en disco duro, minutos. Aqui se guarda el resultado en un archivo y
        // los siguientes arranques lo leen en segundos. Se invalida solo si la carpeta de musica cambio
        // (cantidad de carpetas o fecha de la raiz). F5 siempre fuerza un re-escaneo completo.
        private string LibCacheFile() { return Path.Combine(Application.StartupPath, "rocola_biblioteca.txt"); }

        // En arranque desde cache: portadas resueltas SIN tocar el disco.
        // La portada de una carpeta: siempre sale del mapa que dejo el recorrido del disco (el escaneo fresco lo
        // arma sobre la marcha y el arranque por cache lo trae del archivo), asi que aqui ya no se toca el disco.
        private string FindCoverCached(string folder)
        {
            string cp;
            if (cachedCovers != null && cachedCovers.TryGetValue(folder, out cp)) return cp;
            return null;
        }

        // Firma de una raiz: fecha de modificacion + cantidad de subcarpetas (si cambia, hay que re-escanear).
        private static string RootSig(string root)
        {
            return Directory.GetLastWriteTimeUtc(root).Ticks + ":" + Directory.GetDirectories(root).Length;
        }
        private static string JoinRoots(List<string> roots)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < roots.Count; i++) { if (i > 0) sb.Append('|'); sb.Append(roots[i]); }
            return sb.ToString();
        }

        private bool TryLoadLibraryCache(List<string> roots, out List<Track> outTracks, out List<string> outVideos, out Dictionary<string, string> outCovers)
        {
            outTracks = null; outVideos = null; outCovers = null;
            try
            {
                string f = LibCacheFile();
                if (!File.Exists(f)) return false;
                string[] L = File.ReadAllLines(f);
                if (L.Length < 7 || L[0] != "ROCOLA-LIB v2") return false;   // v2: multiples raices (Ctrl+F2)
                if (!L[1].StartsWith("roots=") || !string.Equals(L[1].Substring(6), JoinRoots(roots), StringComparison.OrdinalIgnoreCase)) return false;
                string sig = ""; try { StringBuilder ss = new StringBuilder(); for (int i = 0; i < roots.Count; i++) { if (i > 0) ss.Append('|'); ss.Append(RootSig(roots[i])); } sig = ss.ToString(); } catch { return false; }
                if (!L[2].StartsWith("sig=") || L[2].Substring(4) != sig) return false;   // alguna raiz cambio -> re-escanear
                if (!L[3].StartsWith("dirs=")) return false;   // linea reservada (compatibilidad de formato)

                int li = 4;
                int na; if (!L[li].StartsWith("A") || !int.TryParse(L[li].Substring(1), out na)) return false; li++;
                if (li + na > L.Length) return false;
                List<Track> tk = new List<Track>(na);
                for (int i = 0; i < na; i++) { string p = L[li + i]; if (p.Length > 0) tk.Add(Track.FromFile(p)); }
                li += na;
                int nv; if (li >= L.Length || !L[li].StartsWith("V") || !int.TryParse(L[li].Substring(1), out nv)) return false; li++;
                if (li + nv > L.Length) return false;
                List<string> vd = new List<string>(nv);
                for (int i = 0; i < nv; i++) { string p = L[li + i]; if (p.Length > 0) vd.Add(p); }
                li += nv;
                int nc; if (li >= L.Length || !L[li].StartsWith("C") || !int.TryParse(L[li].Substring(1), out nc)) return false; li++;
                if (li + nc > L.Length) return false;
                Dictionary<string, string> cv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < nc; i++)
                {
                    string s = L[li + i]; int b = s.IndexOf('|'); if (b <= 0) continue;
                    cv[s.Substring(0, b)] = s.Substring(b + 1);
                }
                if (tk.Count == 0) return false;
                outTracks = tk; outVideos = vd; outCovers = cv;
                return true;
            }
            catch { return false; }
        }

        // Guarda la biblioteca en segundo plano (la copia de datos es instantanea; la escritura va al pool).
        private void SaveLibraryCacheAsync()
        {
            try
            {
                List<string> roots = (scanRoots != null && scanRoots.Count > 0) ? new List<string>(scanRoots) : AllMusicRoots();
                if (roots.Count == 0) return;
                List<string> ap = new List<string>(tracks.Count);
                for (int i = 0; i < tracks.Count; i++) ap.Add(tracks[i].Path);
                List<string> vp = (lastVideoPaths != null) ? new List<string>(lastVideoPaths) : new List<string>();
                Dictionary<string, string> cv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < albums.Count; i++) if (albums[i].CoverPath != null) cv[albums[i].Folder] = albums[i].CoverPath;
                if (videoAlbums != null) for (int i = 0; i < videoAlbums.Count; i++) if (videoAlbums[i].CoverPath != null) cv[videoAlbums[i].Folder] = videoAlbums[i].CoverPath;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate (object st)
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder(ap.Count * 64);
                        sb.Append("ROCOLA-LIB v2\r\n");
                        sb.Append("roots=").Append(JoinRoots(roots)).Append("\r\n");
                        sb.Append("sig=");
                        for (int i = 0; i < roots.Count; i++) { if (i > 0) sb.Append('|'); sb.Append(RootSig(roots[i])); }
                        sb.Append("\r\n");
                        sb.Append("dirs=0\r\n");   // linea reservada (el formato v1 la usaba; se conserva la posicion)
                        sb.Append("A").Append(ap.Count).Append("\r\n");
                        for (int i = 0; i < ap.Count; i++) sb.Append(ap[i]).Append("\r\n");
                        sb.Append("V").Append(vp.Count).Append("\r\n");
                        for (int i = 0; i < vp.Count; i++) sb.Append(vp[i]).Append("\r\n");
                        sb.Append("C").Append(cv.Count).Append("\r\n");
                        foreach (KeyValuePair<string, string> kv in cv) sb.Append(kv.Key).Append('|').Append(kv.Value).Append("\r\n");
                        File.WriteAllText(LibCacheFile(), sb.ToString());
                    }
                    catch { }
                });
            }
            catch { }
        }

        // Camina el arbol UNA vez y clasifica lo que encuentra: audio, video y la portada de cada carpeta.
        // Es el reemplazo de las dos caminatas separadas + el GetFiles extra que hacia FindCover por carpeta.
        private void ScanFolders(string root, List<string> audio, List<string> video, Dictionary<string, string> covers)
        {
            Stack<string> dirs = new Stack<string>();
            dirs.Push(root);
            while (dirs.Count > 0)
            {
                string dir = dirs.Pop();
                string[] files = null;
                try { files = Directory.GetFiles(dir); } catch { }
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                        if (Array.IndexOf(AudioExt, ext) >= 0) audio.Add(files[i]);
                        else if (Array.IndexOf(VideoExt, ext) >= 0) video.Add(files[i]);
                    }
                    if (!covers.ContainsKey(dir))
                    {
                        string cv = CoverFromList(dir, files);
                        if (cv != null) covers[dir] = cv;
                    }
                    if (audio.Count % 50 < files.Length) { try { scanner.ReportProgress(0, audio.Count); } catch { } }   // avance para la pantalla de arranque
                }
                string[] subs = null;
                try { subs = Directory.GetDirectories(dir); } catch { }
                if (subs != null) foreach (string d in subs) dirs.Push(d);
            }
        }

        // La portada de una carpeta a partir de la lista de archivos que YA se leyo. Mismo criterio de siempre:
        // primero los nombres tipicos (en su orden de preferencia) y, si no hay ninguno, la primera imagen.
        private static string CoverFromList(string folder, string[] files)
        {
            for (int i = 0; i < CoverNames.Length; i++)
                for (int j = 0; j < files.Length; j++)
                    if (string.Equals(Path.GetFileName(files[j]), CoverNames[i], StringComparison.OrdinalIgnoreCase))
                        return Path.Combine(folder, CoverNames[i]);
            for (int i = 0; i < files.Length; i++)
            {
                string ext = Path.GetExtension(files[i]).ToLowerInvariant();
                if (Array.IndexOf(ImgExt, ext) >= 0) return files[i];
            }
            return null;
        }

        private static readonly char[] SepChars = { '\\', '/' };

        // La carpeta del DISCO de una cancion: la subcarpeta de PRIMER nivel bajo su raiz de musica.
        // Asi, si un disco tiene subcarpetas, TODA su musica (a cualquier profundidad) cae DENTRO del
        // mismo disco en vez de aparecer como discos aparte. Si la cancion esta suelta en la raiz, el
        // disco es la raiz misma. Si por lo que sea no cae bajo ninguna raiz, se usa su carpeta inmediata.
        private static string AlbumFolderFor(string filePath, List<string> roots)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir)) return "";
            if (roots != null)
                for (int r = 0; r < roots.Count; r++)
                {
                    string root = roots[r] == null ? "" : roots[r].TrimEnd('\\', '/');
                    if (root.Length == 0) continue;
                    if (string.Equals(dir, root, StringComparison.OrdinalIgnoreCase)) return dir;   // suelta en la raiz -> el disco ES la raiz
                    if (dir.Length > root.Length + 1
                        && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                        && (dir[root.Length] == '\\' || dir[root.Length] == '/'))
                    {
                        int slash = dir.IndexOfAny(SepChars, root.Length + 1);   // fin de la subcarpeta de 1er nivel
                        return (slash < 0) ? dir : dir.Substring(0, slash);      // raiz + 1a subcarpeta (lo mas profundo cae aqui)
                    }
                }
            return dir;   // fuera de toda raiz conocida: como antes (carpeta inmediata)
        }

        private void BuildAlbums(List<string> roots)
        {
            Dictionary<string, Album> byFolder = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tracks.Count; i++)
            {
                Track t = tracks[i];
                string folder = AlbumFolderFor(t.Path, roots);
                if (folder == null) folder = "";
                Album al;
                if (!byFolder.TryGetValue(folder, out al)) { al = new Album(); al.Folder = folder; byFolder[folder] = al; }
                al.Tracks.Add(t);
            }

            albums = new List<Album>();
            foreach (KeyValuePair<string, Album> kv in byFolder)
            {
                Album al = kv.Value;
                string leaf = LeafName(al.Folder);
                string ar, nm;
                if (SplitArtist(leaf, out ar, out nm)) { al.Artist = ar; al.Name = nm; }
                else { al.Artist = leaf; al.Name = ""; }
                al.Letter = FirstLetter(al.Artist);
                al.CoverPath = FindCoverCached(al.Folder);
                al.Tracks.Sort(delegate (Track a, Track b) { return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase); });
                albums.Add(al);
            }

            albums.Sort(delegate (Album a, Album b)
            {
                int c = string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            byLetter = new Dictionary<char, List<Album>>();
            for (int i = 0; i < albums.Count; i++)
            {
                Album al = albums[i];
                List<Album> lst;
                if (!byLetter.TryGetValue(al.Letter, out lst)) { lst = new List<Album>(); byLetter[al.Letter] = lst; }
                lst.Add(al);
            }
        }

        private static string LeafName(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return "Video";
            string s = folder.TrimEnd('\\', '/');
            string leaf = Path.GetFileName(s);
            return string.IsNullOrEmpty(leaf) ? s : leaf;
        }

        // Separa "Artista - Titulo" o "Artista-Titulo" (guion con o sin espacios). Toma el PRIMER guion.
        private static bool SplitArtist(string s, out string left, out string right)
        {
            left = (s ?? "").Trim(); right = "";
            if (string.IsNullOrEmpty(s)) return false;
            int idx = s.IndexOf(" - ", StringComparison.Ordinal); int len = 3;
            if (idx < 0) { idx = s.IndexOf('-'); len = 1; }
            if (idx <= 0) return false;
            left = s.Substring(0, idx).Trim();
            right = s.Substring(idx + len).Trim();
            return left.Length > 0;
        }

        private static bool AllDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++) { char c = s[i]; if (c != ' ' && (c < '0' || c > '9')) return false; }
            return true;
        }

        private static string AlbumLabel(Album a)
        {
            if (a == null) return "";
            return (a.Name != null && a.Name.Length > 0) ? (a.Artist + " - " + a.Name) : a.Artist;
        }

        private void BuildVideoSection(List<string> roots)
        {
            // carpeta -> album de audio existente
            Dictionary<string, Album> byFolder = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albums.Count; i++) { albums[i].VideoFiles.Clear(); byFolder[albums[i].Folder] = albums[i]; }

            Dictionary<string, Album> videoOnly = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            // Fuente unica: la lista de videos que ya viene lista (del cache al arrancar, o del recorrido del
            // disco en un escaneo fresco). Antes aqui se volvia a caminar TODO el arbol solo por los videos.
            List<string> vsrc = cachedVideoPaths ?? new List<string>();
            foreach (string vf in vsrc)
            {
                string folder = AlbumFolderFor(vf, roots);
                Album al;
                if (byFolder.TryGetValue(folder, out al)) al.VideoFiles.Add(vf);
                else if (videoOnly.TryGetValue(folder, out al)) al.VideoFiles.Add(vf);
                else
                {
                    al = new Album();
                    al.Folder = folder;
                    al.Artist = LeafName(folder);
                    al.Name = "";
                    al.Letter = FirstLetter(al.Artist);
                    al.CoverPath = FindCoverCached(folder);
                    al.VideoFiles.Add(vf);
                    videoOnly[folder] = al;
                }
            }

            videoAlbums = new List<Album>();
            for (int i = 0; i < albums.Count; i++) if (albums[i].VideoFiles.Count > 0) videoAlbums.Add(albums[i]);
            foreach (KeyValuePair<string, Album> kv in videoOnly) videoAlbums.Add(kv.Value);
            videoAlbums.Sort(delegate (Album a, Album b) { return string.Compare(a.Artist, b.Artist, StringComparison.OrdinalIgnoreCase); });
            for (int i = 0; i < videoAlbums.Count; i++) videoAlbums[i].VideoFiles.Sort(StringComparer.OrdinalIgnoreCase);
            lastVideoPaths = cachedVideoPaths;   // para guardar el cache
        }

        private void BuildSearchIndex()
        {
            searchIndex.Clear();
            // Mapa artista -> todos sus discos (para el cubo que cicla caratulas)
            Dictionary<string, List<Album>> artistAlbums = new Dictionary<string, List<Album>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albums.Count; i++)
            {
                Album a = albums[i];
                if (string.IsNullOrEmpty(a.Artist)) continue;
                List<Album> lst;
                if (!artistAlbums.TryGetValue(a.Artist, out lst)) { lst = new List<Album>(); artistAlbums[a.Artist] = lst; }
                lst.Add(a);
            }
            HashSet<string> artistSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albums.Count; i++)
            {
                Album a = albums[i];
                if (!string.IsNullOrEmpty(a.Artist) && artistSeen.Add(a.Artist))
                {
                    SearchItem ar = new SearchItem();
                    ar.Kind = 0; ar.Display = a.Artist; ar.Key = Normalize(a.Artist); ar.AlbumRef = a;
                    artistAlbums.TryGetValue(a.Artist, out ar.Albums);
                    searchIndex.Add(ar);
                }
                SearchItem alb = new SearchItem();
                alb.Kind = 1; alb.Display = a.Artist + " - " + a.Name; alb.Key = Normalize(alb.Display); alb.AlbumRef = a;
                searchIndex.Add(alb);
                for (int j = 0; j < a.Tracks.Count; j++)
                {
                    Track t = a.Tracks[j];
                    SearchItem so = new SearchItem();
                    so.Kind = 2; so.Display = t.Artist + " - " + t.Title; so.Key = Normalize(so.Display); so.TrackRef = t; so.AlbumRef = a;
                    searchIndex.Add(so);
                }
            }
        }

        private static char FirstLetter(string s)
        {
            if (string.IsNullOrEmpty(s)) return '#';
            string n = s.Trim();
            if (n.Length == 0) return '#';
            char c = char.ToUpperInvariant(n[0]);
            string norm = c.ToString().Normalize(NormalizationForm.FormD);
            for (int i = 0; i < norm.Length; i++)
                if (CharUnicodeInfo.GetUnicodeCategory(norm[i]) != UnicodeCategory.NonSpacingMark) { c = char.ToUpperInvariant(norm[i]); break; }
            if (c >= 'A' && c <= 'Z') return c;
            return '#';
        }

        private static readonly string[] CoverNames = { "portada.jpg", "portada.jpeg", "portada.png", "cover.jpg", "cover.png", "folder.jpg", "front.jpg" };
        private static readonly string[] ImgExt = { ".jpg", ".jpeg", ".png", ".bmp" };

        private List<Album> CurrentList()
        {
            if (libBuilding) return new List<Album>();   // el worker esta mutando las listas: no leerlas
            if (currentLetter == VideoMark) return videoAlbums ?? new List<Album>();
            if (currentLetter == TopMark) return topDiscs ?? new List<Album>();
            List<Album> list;
            if (!byLetter.TryGetValue(currentLetter, out list)) list = new List<Album>();
            return list;
        }

        private void RefreshAlbums()
        {
            if (currentLetter == TopMark) BuildTopDiscs();   // discos TOP siempre al dia
            List<Album> list = CurrentList();
            int totalPages = Math.Max(1, (list.Count + PageSize - 1) / PageSize);
            if (page < 0) page = 0;
            if (page >= totalPages) page = totalPages - 1;
            if (selAlbumIndex < 0) selAlbumIndex = 0;
            if (selAlbumIndex >= list.Count) selAlbumIndex = Math.Max(0, list.Count - 1);

            // Cambio de pagina/letra -> nueva "generacion": las cargas de la pagina anterior se descartan
            // GRATIS en la bomba (revisa la generacion antes de decodificar). Gracias a eso, la pagina
            // VISIBLE se pide AL INSTANTE, sin esperar nada -> sensacion de carga inmediata. El
            // asentamiento (timer) queda solo para la PRELECTURA de las paginas vecinas.
            bool newPage = false;
            string psig = currentLetter + "|" + page;
            if (psig != lastPageSig)
            {
                lastPageSig = psig; coverGen++;
                newPage = true;
                coversOnHold = true;   // sigue frenando las peticiones nacidas del PINTADO durante el hojeo
                if (coverSettleTimer != null) { coverSettleTimer.Stop(); coverSettleTimer.Start(); }
            }

            List<Album> pageList = new List<Album>();
            int start = page * PageSize;
            for (int i = start; i < start + PageSize && i < list.Count; i++) pageList.Add(list[i]);

            if (newPage)
                for (int i = 0; i < pageList.Count; i++) RequestCover(pageList[i], false, true);   // fila ALTA, ya mismo

            albumView.Page = pageList;
#if ROCOLA_GPU
            // La pagina de AL LADO se va horneando mientras nadie toca nada (ver BakeAhead): al pasar de pagina
            // las casillas ya estan listas y el deslizamiento se ve fluido LA PRIMERA VEZ, no solo al volver.
            List<Album> siguiente = new List<Album>();
            for (int i = start + PageSize; i < start + PageSize * 2 && i < list.Count; i++) siguiente.Add(list[i]);
            albumView.SetAhead(siguiente);
#endif
            albumView.Selected = selAlbumIndex - start;
            albumView.SelAngle = 0;
            albumView.CancelHandoff();   // cambio de pagina/letra: cancela cualquier giro de relevo en curso
            albumView.Invalidate();

            string secName = (currentLetter == VideoMark) ? "VIDEOS" : (currentLetter == TopMark) ? "LO MAS SONADO" : ("Letra " + currentLetter);
            lblHeader.Text = secName + "    -    pagina " + (page + 1) + " de " + totalPages;
            Letter_Refresh();
        }

        // SINCRONO: para portadas sueltas (cubo grande del disco, mini-cubo, buscador). Pocas a la vez.
        // SOLO MEMORIA: si la caratula no esta en cache, la ENCARGA en segundo plano y devuelve null;
        // al llegar se aplica sola (ApplyArrivedCover). Antes decodificaba AQUI MISMO, en el hilo de la
        // interfaz: en disco duro eso congelaba la pantalla al entrar a un disco, al cambiar de cancion
        // y al pintar el buscador (100-500ms por caratula). Ese era el grueso de los tirones.
        private Image LoadCover(Album a)
        {
            if (a == null || a.CoverPath == null) return null;
            lock (coverLock) { Image c; if (coverCache.TryGetValue(a.CoverPath, out c)) return c; }
            RequestCover(a, false);   // encargar SIN el freno de hojeo (esto no es la rejilla)
            return null;
        }

        // ASINCRONO: para la rejilla (18 portadas por pagina). Devuelve la cacheada o null,
        // y carga en segundo plano para no trabar el cambio de pagina; al terminar, repinta.
        // El usuario se detuvo en una pagina: AHORA si pedimos sus caratulas.
        // (Las paginas que paso de largo nunca se piden: cero trabajo desperdiciado.)
        private void CoverSettle_Tick(object sender, EventArgs e)
        {
            coverSettleTimer.Stop();
            coversOnHold = false;
            List<Album> p = (albumView != null) ? albumView.Page : null;
            if (p != null) for (int i = 0; i < p.Count; i++) LoadCoverAsync(p[i]);
            // PRELECTURA: tambien se encargan las paginas vecinas (despues de la actual, por orden de
            // llegada). En disco duro, hojear con las flechas queda servido directo de la RAM.
            List<Album> all = CurrentList();
            PrefetchPage(all, page + 1);
            PrefetchPage(all, page - 1);
            if (!diskSlow)   // con SSD sobran lecturas: dos paginas de colchon; con HDD, solo una
            {
                PrefetchPage(all, page + 2);
                PrefetchPage(all, page - 2);
            }
        }

        private void PrefetchPage(List<Album> list, int p)
        {
            if (list == null || p < 0) return;
            int start = p * PageSize;
            if (start >= list.Count) return;
            int end = Math.Min(start + PageSize, list.Count);
            for (int i = start; i < end; i++) RequestCover(list[i], true, false);   // fila BAJA: nunca por delante de lo visible
        }

        // Una caratula recien decodificada llega del disco: se aplica donde haga falta.
        private void ApplyArrivedCover(string key)
        {
            try
            {
                if (albumView != null) albumView.RefreshCover(key);
                if (panelSongs != null && panelSongs.Visible && currentAlbum != null && cubeBox != null)
                {
                    if (currentAlbum.IsTop && currentAlbum.TrackCovers != null && songSel >= 0 && songSel < currentAlbum.TrackCovers.Count && currentAlbum.TrackCovers[songSel] == key)
                    { cubeBox.Cover = LoadCoverByPath(key); cubeBox.Invalidate(); }
                    else if (!currentAlbum.IsTop && currentAlbum.CoverPath == key)
                    { cubeBox.Cover = LoadCover(currentAlbum); cubeBox.Invalidate(); }
                }
                if (miniCube != null && miniCube.Visible && nowPlaying != null && nowPlaying.Disc != null && nowPlaying.Disc.CoverPath == key)
                { miniCube.Cover = LoadCover(nowPlaying.Disc); miniCube.Invalidate(); }
                if (searchOpen && searchOverlay != null) searchOverlay.Invalidate();
            }
            catch { }
        }

        private Image LoadCoverAsync(Album a) { return RequestCover(a, true, true); }

        // ----- DESPACHADOR DE CARATULAS CON PRIORIDAD -----
        // Antes todas las peticiones (pagina actual, prelectura de vecinas, lo que fuera) se formaban
        // revueltas en el mismo turno de disco: a veces las caratulas que ESTAS viendo esperaban detras
        // de la prelectura. Ahora hay dos filas: ALTA (lo visible) y BAJA (prelectura); una sola bomba
        // atiende el disco y SIEMPRE saca primero la fila alta.
        private class CoverJob { public string Key; public int Gen; }
        private readonly List<CoverJob> coverQHi = new List<CoverJob>();
        private readonly List<CoverJob> coverQLo = new List<CoverJob>();
        private int coverPumpCount = 0;   // hasta 2 bombas de decodificacion (una por nucleo)

        private Image RequestCover(Album a, bool respectHold) { return RequestCover(a, respectHold, true); }

        // Carga una caratula por RUTA directa (cache o encargo). Usado por los discos TOP (ciclado de
        // portadas y cambio de portada al navegar canciones).
        private Image LoadCoverByPath(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            lock (coverLock) { Image c; if (coverCache.TryGetValue(key, out c)) return c; }
            RequestCoverByPath(key, false);
            return null;
        }

        private void RequestCoverByPath(string key, bool highPriority)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (coverLock)
            {
                Image c;
                if (coverCache.TryGetValue(key, out c)) return;
                if (coverLoading.Contains(key)) return;
                coverLoading.Add(key);
                CoverJob j = new CoverJob(); j.Key = key; j.Gen = coverGen;
                if (highPriority) coverQHi.Add(j); else coverQLo.Add(j);
                if (coverPumpCount < (diskSlow ? 1 : 2))
                {
                    coverPumpCount++;
                    System.Threading.Thread t = new System.Threading.Thread(CoverPump);
                    t.IsBackground = true;
                    t.Priority = System.Threading.ThreadPriority.BelowNormal;   // decodificar caratulas NUNCA por delante de la interfaz/render (clave si el CPU es de un nucleo)
                    t.Start();
                }
            }
        }

        private Image RequestCover(Album a, bool respectHold, bool highPriority)
        {
            if (a == null || a.CoverPath == null) return null;
            string key = a.CoverPath;
            lock (coverLock)
            {
                Image c;
                if (coverCache.TryGetValue(key, out c)) return c;
                if (respectHold && coversOnHold) return null;   // hojeando rapido: todavia no pedir
                if (coverLoading.Contains(key)) return null;
                coverLoading.Add(key);
                CoverJob j = new CoverJob(); j.Key = key; j.Gen = coverGen;
                if (highPriority) coverQHi.Add(j); else coverQLo.Add(j);
                if (coverPumpCount < (diskSlow ? 1 : 2))   // SSD: un carril por nucleo; HDD: carril unico (cabezal)
                {
                    coverPumpCount++;
                    System.Threading.Thread t = new System.Threading.Thread(CoverPump);
                    t.IsBackground = true;
                    t.Priority = System.Threading.ThreadPriority.BelowNormal;   // idem: la caratula puede esperar; un cuadro perdido se NOTA
                    t.Start();
                }
            }
            return null;
        }

        // Una sola bomba de disco: fila ALTA primero, luego BAJA; termina y se apaga sola.
        private void CoverPump()
        {
            while (true)
            {
                CoverJob j = null;
                lock (coverLock)
                {
                    if (coverQHi.Count > 0) { j = coverQHi[0]; coverQHi.RemoveAt(0); }
                    else if (coverQLo.Count > 0) { j = coverQLo[0]; coverQLo.RemoveAt(0); }
                    else { coverPumpCount--; return; }
                }
                string key = j.Key;
                if (j.Gen != coverGen) { lock (coverLock) { coverLoading.Remove(key); } continue; }
                coverSem.WaitOne();   // turno unico de disco (compartido con el precalentador)
                try
                {
                    if (j.Gen != coverGen) { lock (coverLock) { coverLoading.Remove(key); } continue; }
                    Image thumb = DecodeCover(key);
                    lock (coverLock) { CachePut(key, thumb); coverLoading.Remove(key); }
                    try
                    {
                        if (this.IsHandleCreated)
                            this.BeginInvoke((MethodInvoker)delegate { ApplyArrivedCover(key); });
                    }
                    catch { }
                }
                finally { coverSem.Release(); }
            }
        }

        // Caché de miniaturas en DISCO: la 1a vez decodifica la portada grande y guarda una miniatura
        // pequeña; las siguientes veces lee la miniatura (leer ~30KB es muchisimo mas rapido que decodificar
        // un JPG grande del disco duro). Acelera enormemente volver a ver paginas ya vistas.
        private static string thumbDir;
        private static ImageCodecInfo jpegEnc;

        private static string ThumbCachePath(string coverPath)
        {
            if (thumbDir == null)
            {
                string d = Path.Combine(Path.GetTempPath(), "rocola_thumbs");
                try { if (!Directory.Exists(d)) Directory.CreateDirectory(d); } catch { }
                thumbDir = d;
            }
            ulong h = Fnv64(coverPath.ToLowerInvariant());
            return Path.Combine(thumbDir, h.ToString("x16") + "_" + ThumbPx + ".jpg");
        }

        private static ulong Fnv64(string s)
        {
            ulong hash = 1469598103934665603UL;
            for (int i = 0; i < s.Length; i++) { hash ^= s[i]; hash *= 1099511628211UL; }
            return hash;
        }

        private static void SaveThumb(Bitmap thumb, string cacheFile)
        {
            try
            {
                if (jpegEnc == null)
                    foreach (ImageCodecInfo ci in ImageCodecInfo.GetImageEncoders()) if (ci.MimeType == "image/jpeg") { jpegEnc = ci; break; }
                if (jpegEnc == null) { thumb.Save(cacheFile); return; }
                using (EncoderParameters ep = new EncoderParameters(1))
                {
                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 82L);
                    thumb.Save(cacheFile, jpegEnc, ep);
                }
            }
            catch { }
        }

        private static Image DecodeCover(string path)
        {
            string cacheFile = null;
            try { cacheFile = ThumbCachePath(path); } catch { }

            // 1) miniatura en cache -> leerla (rapido)
            if (cacheFile != null)
            {
                try
                {
                    if (File.Exists(cacheFile))
                    {
                        byte[] cdata;
                        if (diskProbeN < 16)   // perfilado: cronometra las primeras lecturas de miniaturas
                        {
                            System.Diagnostics.Stopwatch dsw = System.Diagnostics.Stopwatch.StartNew();
                            cdata = File.ReadAllBytes(cacheFile);
                            dsw.Stop();
                            DiskProbe((int)dsw.ElapsedMilliseconds);
                        }
                        else cdata = File.ReadAllBytes(cacheFile);   // I/O puro primero; GDI+ decodifica desde RAM
                        using (MemoryStream cms = new MemoryStream(cdata))
                        using (Image cimg = Image.FromStream(cms, false, false))
                        {
                            Bitmap tb = new Bitmap(ThumbPx, ThumbPx, PixelFormat.Format32bppPArgb);
                            using (Graphics g = Graphics.FromImage(tb))
                            {
                                // la miniatura YA mide ThumbPx: copia 1:1 (mismos pixeles, sin filtro caro)
                                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                                g.DrawImage(cimg, 0, 0, ThumbPx, ThumbPx);
                            }
                            return tb;
                        }
                    }
                }
                catch { /* cache corrupta -> recodificar abajo */ }
            }

            // 2) decodificar la portada original y GUARDAR la miniatura para la proxima vez
            try
            {
                byte[] data = File.ReadAllBytes(path);   // I/O puro primero (lectura secuencial corta); GDI+ decodifica desde RAM
                using (MemoryStream ms = new MemoryStream(data))
                using (Image src = Image.FromStream(ms, false, false))
                {
                    Bitmap thumb = new Bitmap(ThumbPx, ThumbPx, PixelFormat.Format32bppPArgb);
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;   // filtro original de la miniatura (1 vez por disco)
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(src, 0, 0, ThumbPx, ThumbPx);
                    }
                    if (cacheFile != null) SaveThumb(thumb, cacheFile);
                    return thumb;
                }
            }
            catch { return null; }
        }

        // Guarda en cache acotando el tamaño (FIFO). Llamar SIEMPRE bajo coverLock.
        // Las expulsadas NO se liberan aqui (el hilo de pantalla podria estar dibujandolas):
        // se mandan a la papelera y se liberan en el temporizador, cuando no hay repintado en curso.
        private void CachePut(string key, Image img)
        {
            if (coverCache.ContainsKey(key)) { coverCache[key] = img; return; }
            coverCache[key] = img;
            coverOrder.Enqueue(key);
            while (coverOrder.Count > CoverCacheMax)
            {
                string old = coverOrder.Dequeue();
                Image oldImg;
                if (coverCache.TryGetValue(old, out oldImg)) { coverCache.Remove(old); if (oldImg != null) coverTrash.Add(oldImg); }
            }
        }

        // Libera las carátulas expulsadas. Se llama desde el temporizador (mismo hilo que el dibujo,
        // asi que NUNCA hay un DrawImage en curso). No libera las que sigan en uso por el cubo del disco,
        // el mini-cubo o la animacion de disco (esas las recoge el GC cuando dejen de usarse).
        private void DrainCoverTrash()
        {
            List<Image> dump = null;
            lock (coverLock)
            {
                if (coverTrash.Count == 0) return;
                dump = new List<Image>(coverTrash);
                coverTrash.Clear();
            }
            Image pin1 = (miniCube != null) ? miniCube.Cover : null;
            Image pin2 = (cubeBox != null) ? cubeBox.Cover : null;
#if ROCOLA_GPU
            Image pin3 = transImg;   // la animacion de disco la esta dibujando AHORA: liberarla revienta el render
#else
            Image pin3 = null;
#endif
            for (int i = 0; i < dump.Count; i++)
            {
                Image im = dump[i];
                if (im == null) continue;
                if (im == pin1 || im == pin2 || im == pin3) continue;   // sigue en uso: que lo recoja el GC luego
                try { im.Dispose(); } catch { }
            }
        }

        private void SetSelectedLocal(int local, int dir)
        {
            albumView.MoveSelectionTo(local, dir);   // mueve con efecto de relevo (el anterior gira y se apaga)
            albumView.Invalidate();
        }

        private int PageDiscCount()
        {
            int total = CurrentList().Count;
            int c = total - page * PageSize;
            if (c < 0) c = 0;
            if (c > PageSize) c = PageSize;
            return c;
        }

        // Mueve la seleccion DENTRO de la pagina, con CICLO: derecha en el ultimo disco -> vuelve al
        // primero; izquierda en el primero -> va al ultimo. Nunca se atasca (agiliza busquedas cortas).
        private void MoveInPage(int delta)
        {
            int cnt = PageDiscCount();
            if (cnt == 0) return;
            int local = selAlbumIndex - page * PageSize + delta;
            local = ((local % cnt) + cnt) % cnt;   // modulo con negativos: -1 -> ultimo, cnt -> primero
            selAlbumIndex = page * PageSize + local;
            SetSelectedLocal(local, delta);
        }

        private void NextPage()
        {
            int total = CurrentList().Count;
            int totalPages = Math.Max(1, (total + PageSize - 1) / PageSize);
            if (page < totalPages - 1) { page++; selAlbumIndex = page * PageSize; RefreshAlbums(); DeslizaPagina(1); }
            else if (GotoLetter(1, true)) { page = 0; selAlbumIndex = 0; RefreshAlbums(); DeslizaPagina(1); }   // ultima pagina de la ultima seccion: CICLA al inicio
        }

        private void PrevPage()
        {
            if (page > 0) { page--; selAlbumIndex = page * PageSize; RefreshAlbums(); DeslizaPagina(-1); }
            else if (GotoLetter(-1, true))   // primera pagina de la primera seccion: CICLA al final
            {
                int total = CurrentList().Count;
                int tp = Math.Max(1, (total + PageSize - 1) / PageSize);
                page = tp - 1; selAlbumIndex = page * PageSize; RefreshAlbums(); DeslizaPagina(-1);
            }
        }

        // HOJEO: la pagina nueva entra deslizandose por el lado hacia el que vas. Solo por GPU: las casillas ya
        // se dibujan cada cuadro, asi que moverlas no cuesta un dibujo mas. Por CPU no se hace (ahi cada cuadro
        // es repintar de verdad, y eso si costaria).
        private void DeslizaPagina(int dir)
        {
#if ROCOLA_GPU
            if (gpuEnabled && albumView != null && albumView.Visible) albumView.StartSlide(dir, albumView.Width);
#endif
        }

        // Salta a la letra DISPONIBLE siguiente/anterior. Con 'wrap', al pasar el extremo CICLA al otro
        // lado (VIDEO -> TOP/#/A... y viceversa): el abecedario es una rueda, nunca se atasca.
        private bool GotoLetter(int dir, bool wrap)
        {
            string order = "\u2605#ABCDEFGHIJKLMNOPQRSTUVWXYZ\u25BA";
            int n = order.Length;
            int idx = order.IndexOf(currentLetter);
            if (idx < 0) idx = 0;
            int steps = wrap ? n : (dir > 0 ? n - 1 - idx : idx);   // sin wrap: hasta el extremo; con wrap: la vuelta completa (incluye regresar a la misma si es la unica)
            for (int k = 1; k <= steps; k++)
            {
                int i = idx + dir * k;
                if (wrap) i = ((i % n) + n) % n;
                if (letterAvailable.Contains(order[i])) { currentLetter = order[i]; return true; }
            }
            return false;
        }

        private void ChangeLetter(int dir)
        {
            if (GotoLetter(dir, true)) { page = 0; selAlbumIndex = 0; RefreshAlbums(); DeslizaPagina(dir); }   // REGRESAR+izq/der: las letras CICLAN
        }

        private void EnterAlbum(Album a)
        {
#if ROCOLA_GPU
            // De DONDE sale la portada: hay que medirlo ANTES de cambiar de pantalla (luego ese cubo ya no
            // existe). Si se viene del buscador, el origen se fijo antes de cerrarlo.
            double angDesde; RectangleF desde;
            if (transNextFrom.Width >= 8f) { desde = transNextFrom; angDesde = transNextAngle; }
            else desde = SelectedDiscRect(out angDesde);
            transNextFrom = RectangleF.Empty;
#endif
            currentAlbum = a;
            browsingVideo = (currentLetter == VideoMark);
            bool showVideos = browsingVideo || (a != null && a.TopIsVideo);   // seccion VIDEOS o disco TOP de videos
            lblAlbum.Text = browsingVideo ? (AlbumLabel(a) + "   (video)") : AlbumLabel(a);
            songItems.Clear();
            if (showVideos)
            {
                for (int i = 0; i < a.VideoFiles.Count; i++) songItems.Add(new VideoItem(a.VideoFiles[i]));
            }
            else
            {
                for (int i = 0; i < a.Tracks.Count; i++) songItems.Add(a.Tracks[i]);
            }
            songSel = 0;
            FillSongPage();
            cubeBox.Cover = LoadCover(a);
            UpdateTopSongVisuals();   // disco TOP: portada/etiqueta de la cancion seleccionada
            cubeBox.Angle = 0;
            albumView.Visible = false;
            letterBar.Visible = false;
            lblHeader.Visible = false;
            panelSongs.Visible = true;
            this.Focus();
#if ROCOLA_GPU
            if (gpuPanel != null && gpuEnabled) SyncGpuFullScreen();   // aplica el modo disco YA (transicion atomica)
            // Ya con el interior puesto: la portada vuela de su casilla al cubo GRANDE del disco y se funde con el.
            double angHasta; RectangleF hasta = DiscCubeRect(out angHasta);
            StartDiscTrans(desde, hasta, angDesde, angHasta, (a != null) ? RequestCover(a, false, true) : null);
#endif
        }

#if ROCOLA_GPU
        // Arranca el VIAJE de una portada entre dos CUBOS: sale calcada sobre el cubo de origen (misma celda y
        // mismo giro, asi que no se nota el relevo), gira mientras vuela y aterriza calcada sobre el de destino,
        // donde se funde con el. Sirve para las cuatro transiciones: entrar a un disco (celda de la rejilla ->
        // cubo grande del disco), salir (al reves), entrar al modo atraccion (celda -> cubo gigante) y salir de
        // el. Si falta cualquier cosa (sin GPU, sin portada, cubo invisible) simplemente no se anima: la
        // animacion NUNCA es requisito para que la pantalla cambie.
        private void StartDiscTrans(RectangleF from, RectangleF to, double ang0, double ang1, Image cover)
        {
            transMode = 0; transImg = null;
            if (!transAnimOn) return;
            if (cover == null) { Diag("anim disco: SIN portada (aun no cargaba)"); return; }
            if (gpuPanel == null || !gpuEnabled) { Diag("anim disco: render por CPU, no se anima"); return; }
            if (from.Width < 8f || from.Height < 8f || to.Width < 8f || to.Height < 8f)
            { Diag("anim disco: celda invisible " + (int)from.Width + "x" + (int)from.Height + " -> " + (int)to.Width + "x" + (int)to.Height); return; }
            Diag("anim disco: arranca " + (int)from.Width + "x" + (int)from.Height + " -> " + (int)to.Width + "x" + (int)to.Height);
            transFrom = from; transTo = to; transImg = cover;
            transAng0 = ang0;
            // Un cubo de 4 caras iguales se ve EXACTAMENTE igual cada cuarto de vuelta, asi que basta con
            // ajustar el giro dentro de ese cuarto para aterrizar calcado sobre el cubo de destino. Al camino
            // corto (como mucho +/-45 grados) se le suma un cuarto de vuelta entero: se ve girar de verdad y
            // aun asi encaja al llegar. Mas vueltas en 0.26 s se verian frenéticas.
            const double Cuarto = Math.PI / 2.0;
            double d = ang1 - ang0;
            d -= Math.Floor(d / Cuarto) * Cuarto;          // 0..90 grados
            if (d > Cuarto * 0.5) d -= Cuarto;             // camino CORTO: -45..+45 grados
            transSpin = d + Cuarto;
            transMode = 1;
            transTick = Environment.TickCount;
        }
        private void AbortDiscTrans() { transMode = 0; transImg = null; }

        // Arranca la animacion de VENTANA del buscador (tipo macOS). Al ABRIR se anima la propia textura de UI
        // (que en ese momento YA es el buscador): no se copia ni se sube nada. Al CERRAR esa textura pasa a ser
        // la de navegacion, asi que se guarda una instantanea de lo que habia.
        private void StartWinTrans(bool abriendo)
        {
            AbortWinTrans();
            if (!transAnimOn) return;
            if (gpuPanel == null || !gpuEnabled) return;
            if (!abriendo)
            {
                if (gpuMode != 2 || uiOverlay == null) return;   // el buscador no llego a componerse: nada que animar
                if (uiOverlay.Width < 40 || uiOverlay.Height < 40) return;
                try { winSnap = uiOverlay.Clone(new Rectangle(0, 0, uiOverlay.Width, uiOverlay.Height), PixelFormat.Format32bppPArgb); }
                catch { winSnap = null; return; }
            }
            winMode = abriendo ? 1 : 2;
            winTick = Environment.TickCount;
            Diag(abriendo ? "anim buscador: abre" : "anim buscador: cierra");
        }
        private void AbortWinTrans()
        {
            winMode = 0;
            if (winSnap != null) { try { winSnap.Dispose(); } catch { } winSnap = null; }   // ya no se dibuja: liberarla es seguro
        }
#endif

        // Donde va el CUBO GIGANTE del modo atraccion. Lo usan el render (BuildGpuCubeState) y la animacion de
        // entrada/salida de la atraccion: uno solo lo calcula para que nunca se desfasen.
        private RectangleF AttractCubeRect()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            int side = (int)(Math.Min(w, h) * 0.40); if (side < 80) side = 80;
            return new RectangleF(w / 2 - side / 2, (h - side) / 2 - 30, side, side);
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
            float pico = 0f;
            for (int b = 0; b < SpecBands; b++) if (specVis[b] > pico) pico = specVis[b];
            float meta = (pico > 0.02f) ? 1f : 0f;
            auroraPres += (meta - auroraPres) * (meta > auroraPres ? 0.075f : 0.014f);   // ~1.3 s para entrar, ~3 s para irse
            if (auroraPres < 0.0025f) auroraPres = 0f; else if (auroraPres > 1f) auroraPres = 1f;
        }

        // Estado del FONDO REACTIVO para el panel GPU. El relleno de color se apoya justo encima de la franja
        // de estado (BotStripTop), que es donde el cielo se acaba, y sube desde ahi. Se apaga solo cuando no
        // hay musica (las bandas caen a cero) y se atenua junto con el fondo mientras se navega, para que
        // nunca le pelee a las portadas ni al texto.
        private GpuAurora BuildAuroraState()
        {
            GpuAurora g = new GpuAurora();
            if (auroraAmount <= 0.004f) return g;
            if (auroraPres <= 0.004f) return g;   // sin musica no se dibuja nada (la presencia ya llego a cero)
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
            // ATENUACION al navegar. El resto del fondo (estrellas, brillos) baja al 45% para no pelearle a
            // las portadas, pero la aurora vive PEGADA ABAJO, donde no hay portadas: bajarla igual la borraba
            // de la pantalla justo cuando mas se mira. Aqui se atenua apenas: al 89% en vez de al 45%.
            g.Dim = (0.80f + 0.20f * bgDim) * auroraPres;   // la presencia hace el fundido de entrada y de salida
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
            return Color.FromArgb(255, accent.R * 235 / mx, accent.G * 235 / mx, accent.B * 235 / mx);
        }

        // TONO de un color, en grados de la rueda (0 = rojo, 120 = verde, 240 = azul).
        private static float TonoDe(Color c)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            float d = mx - mn;
            if (d < 0.0001f) return 0f;
            float h;
            if (mx == r) h = 60f * (((g - b) / d) % 6f);
            else if (mx == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
            while (h < 0f) h += 360f; while (h >= 360f) h -= 360f;
            return h;
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

        // Gira el TONO de un color (grados en la rueda de color) y le escala el brillo. Se usa una vez por cuadro
        // para los dos extremos del fondo reactivo, asi que el costo es nulo.
        private static Color GiraTono(Color c, float grados, float brillo)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            float v = mx, d = mx - mn;
            float sat = (mx <= 0.0001f) ? 0f : d / mx;
            float hue = 0f;
            if (d > 0.0001f)
            {
                if (mx == r) hue = 60f * (((g - b) / d) % 6f);
                else if (mx == g) hue = 60f * ((b - r) / d + 2f);
                else hue = 60f * ((r - g) / d + 4f);
            }
            hue += grados; while (hue < 0f) hue += 360f; while (hue >= 360f) hue -= 360f;
            v *= brillo; if (v > 1f) v = 1f;
            float cc = v * sat, x = cc * (1f - Math.Abs((hue / 60f) % 2f - 1f)), m = v - cc;
            float rr, gg, bb;
            if (hue < 60f) { rr = cc; gg = x; bb = 0f; }
            else if (hue < 120f) { rr = x; gg = cc; bb = 0f; }
            else if (hue < 180f) { rr = 0f; gg = cc; bb = x; }
            else if (hue < 240f) { rr = 0f; gg = x; bb = cc; }
            else if (hue < 300f) { rr = x; gg = 0f; bb = cc; }
            else { rr = cc; gg = 0f; bb = x; }
            return Color.FromArgb((int)((rr + m) * 255f + 0.5f), (int)((gg + m) * 255f + 0.5f), (int)((bb + m) * 255f + 0.5f));
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

        private void BackToAlbums()
        {
#if ROCOLA_GPU
            Album saliendo = currentAlbum;   // para el viaje de vuelta (se usa mas abajo)
            double angDesde; RectangleF desde = DiscCubeRect(out angDesde);   // el cubo GRANDE del disco, antes de cerrarlo
#endif
            panelSongs.Visible = false;
            albumView.Visible = true;
            letterBar.Visible = true;
            lblHeader.Visible = true;
            this.Focus();
#if ROCOLA_GPU
            if (gpuPanel != null && gpuEnabled) SyncGpuFullScreen();   // aplica el modo navegacion YA: no ver el interior detras un instante
            // Ya con la rejilla puesta, la casilla del disco existe otra vez: ahi es donde aterriza la portada.
            double angHasta; RectangleF hasta = SelectedDiscRect(out angHasta);
            StartDiscTrans(desde, hasta, angDesde, angHasta, (saliendo != null) ? RequestCover(saliendo, false, true) : null);
#endif
        }

        private void NavigateToAlbum(Album a)
        {
            if (a == null) return;
            currentLetter = a.Letter;
            List<Album> list = CurrentList();
            int gi = list.IndexOf(a);
            if (gi < 0) gi = 0;
            selAlbumIndex = gi;
            page = (PageSize > 0) ? gi / PageSize : 0;
            if (panelSongs.Visible) BackToAlbums();
            RefreshAlbums();
        }

        // Mueve la seleccion en la lista de canciones. Paso de a UNO (arriba/abajo): CICLA en los extremos
        // (ultima -> primera y al reves), igual que los discos. Salto de PAGINA (izq/der): se detiene en los
        // bordes (teletransportarse de la ultima pagina a la primera a mitad de lista confundiria).
        private void MoveSong(int delta)
        {
            if (songItems.Count == 0) return;
            songSel += delta;
            if (delta == 1 && songSel >= songItems.Count) songSel = 0;
            else if (delta == -1 && songSel < 0) songSel = songItems.Count - 1;
            if (songSel < 0) songSel = 0;
            if (songSel >= songItems.Count) songSel = songItems.Count - 1;
            FillSongPage();
            UpdateTopSongVisuals();
        }

        // En un disco TOP: la portada del cubo y la etiqueta cambian a las del DISCO DE ORIGEN de la
        // cancion seleccionada (asi el cliente sabe de que artista/album es). En discos normales no hace nada.
        private void UpdateTopSongVisuals()
        {
            if (currentAlbum == null || !currentAlbum.IsTop) return;
            int i = songSel;
            if (currentAlbum.TrackCovers != null && i >= 0 && i < currentAlbum.TrackCovers.Count && cubeBox != null)
            {
                cubeBox.Cover = LoadCoverByPath(currentAlbum.TrackCovers[i]);
                cubeBox.Invalidate();
            }
            if (currentAlbum.TrackInfo != null && i >= 0 && i < currentAlbum.TrackInfo.Count && lblAlbum != null)
                lblAlbum.Text = AlbumLabel(currentAlbum) + "   /   " + currentAlbum.TrackInfo[i];
        }

        // Filas que caben en UNA pagina de la lista (se adapta a cualquier resolucion de pantalla).
        private int SongRowsPerPage()
        {
            return (songList != null) ? songList.RowsPerPage : 1;
        }

        // Apunta la lista a la PAGINA donde cae la seleccion (songSel) y actualiza la barra de paginas.
        private void FillSongPage()
        {
            if (songItems.Count == 0)
            {
                if (songPager != null) songPager.Visible = false;
                songList.PageStart = 0; songList.Sel = -1; songList.Invalidate();
                return;
            }
            if (songSel < 0) songSel = 0;
            if (songSel >= songItems.Count) songSel = songItems.Count - 1;
            int rpp = SongRowsPerPage();
            int pages = (songItems.Count + rpp - 1) / rpp;
            int pg = songSel / rpp;
            songList.PageStart = pg * rpp; songList.Sel = songSel; songList.Accent = bgAccent;
            songList.Invalidate();
            if (songPager != null)
            {
                songPager.Page = pg; songPager.Pages = pages;
                songPager.Visible = pages > 1;
                songPager.Invalidate();
            }
        }

        private void EnqueueSelectedSong()
        {
            if (songSel < 0 || songSel >= songItems.Count) return;
            object item = songItems[songSel];
            VideoItem vi = item as VideoItem;
            if (vi != null)
            {
                Album vdisc = currentAlbum;
                if (currentAlbum != null && currentAlbum.IsTop && currentAlbum.TrackAlbums != null && songSel >= 0 && songSel < currentAlbum.TrackAlbums.Count)
                    vdisc = currentAlbum.TrackAlbums[songSel];   // disco real de origen del video
                AddVideoToQueue(vi.Path, vdisc);
            }
            else
            {
                Album disc = currentAlbum;
                if (currentAlbum != null && currentAlbum.IsTop && currentAlbum.TrackAlbums != null && songSel >= 0 && songSel < currentAlbum.TrackAlbums.Count)
                    disc = currentAlbum.TrackAlbums[songSel];   // disco real de origen (no el de genero)
                AddSong((Track)item, disc);
            }
        }

        private void ToggleSearch() { if (searchOpen) CloseSearch(); else OpenSearch(); }

        private void EnterAttract()
        {
#if ROCOLA_GPU
            AbortDiscTrans(); AbortWinTrans();   // lo que hubiera volando ya no aplica: la atraccion cambia toda la pantalla
#endif
            if (attractView == null || libBuilding) return;   // el worker esta armando 'albums': muestrearlo aqui seria una carrera (crash)
            // FIX: si la atraccion entra con el INTERIOR DE UN DISCO abierto, la textura de UI seguia
            // dibujando el interior encima del cielo de atraccion (la firma de UI mira el interior antes
            // que la atraccion) -> pantalla revuelta. Ademas el disco del cliente anterior quedaria
            // abierto al salir. Se cierra el interior ANTES de componer la atraccion, en ambos renders.
            if (panelSongs != null && panelSongs.Visible) BackToAlbums();
            // muestrear portadas repartidas por toda la coleccion (variedad) para el cubo
            List<string> covers = new List<string>();
            int na = albums.Count;
            if (na > 0)
            {
                int start = new Random().Next(na);
                int stride = (na > 120) ? na / 60 : 1;
                for (int k = 0; k < na * 2 && covers.Count < 60; k++)
                {
                    Album al = albums[(start + k * stride) % na];
                    if (al.CoverPath != null && !covers.Contains(al.CoverPath)) covers.Add(al.CoverPath);
                }
            }
            attractView.CycleCovers = covers;
            attractView.SongCount = tracks.Count;
            attractView.Phase = phase;
            for (int i = 0; i < covers.Count && i < 3; i++) RequestCoverByPath(covers[i], true);   // adelantar las primeras
            attractOn = true;
#if ROCOLA_GPU
            if (gpuEnabled && gpuPanel != null)
            {
                // La atraccion se compone por GPU: el control CPU no se pinta (queda oculto detras del panel).
                attractView.Visible = false;
                uiOverlaySig = SigNada;   // fuerza rehacer la textura de UI (textos de atraccion)
                gpuPanel.Visible = true; gpuPanel.BringToFront();
                SyncGpuFullScreen();   // aplica el modo atraccion YA (transicion atomica, como los demas modos)
                // La portada del disco que estaba elegido VUELA hasta el cubo gigante de la atraccion y se funde
                // con el: la misma animacion que al entrar a un disco, pero con el cubo gigante como destino.
                List<Album> lst = CurrentList();
                Album selAlb = (selAlbumIndex >= 0 && selAlbumIndex < lst.Count) ? lst[selAlbumIndex] : null;
                double angDesde; RectangleF desde = SelectedDiscRect(out angDesde);
                StartDiscTrans(desde, AttractCubeRect(), angDesde, attractView.Angle,
                               (selAlb != null) ? RequestCover(selAlb, false, true) : null);
            }
            else
#endif
            {
                attractView.Visible = true;
                attractView.BringToFront();
            }
        }

        private void ExitAttract()
        {
#if ROCOLA_GPU
            Image portadaAtraccion = null;   // la que muestra el cubo gigante AHORA: hay que leerla ANTES de apagar la atraccion
            double angAtraccion = 0;
            if (attractView != null && gpuPanel != null && gpuEnabled)
            { Image c2; float f2; attractView.GetCoverPair(out portadaAtraccion, out c2, out f2); angAtraccion = attractView.Angle; }
#endif
            attractOn = false;
            if (attractView != null) attractView.Visible = false;
#if ROCOLA_GPU
            uiOverlaySig = SigNada;   // rehace la textura de UI de inmediato (deja los textos de atraccion por la barra normal)
            if (gpuPanel != null && gpuEnabled)
            {
                SyncGpuFullScreen();   // aplica el modo navegacion YA (sin un cuadro mezclado de por medio)
                // Ya con la rejilla puesta: el cubo gigante se encoge hasta la casilla del disco elegido y se funde.
                double angHasta; RectangleF hasta = SelectedDiscRect(out angHasta);
                StartDiscTrans(AttractCubeRect(), hasta, angAtraccion, angHasta, portadaAtraccion);
            }
#endif
            this.Invalidate();
        }

        private void MarkSearchActivity() { searchLastActivity = DateTime.Now; }

        // El buscador NO cubre la franja de estado de abajo (cancion, progreso, creditos, cola): se
        // recorta su alto justo encima. La barra de estado va de (H - pad - 68) hacia abajo (ver
        // LayoutControls); aqui dejamos esa franja libre.
        private Rectangle SearchOverlayRect()
        {
            int W = ClientSize.Width, H = ClientSize.Height;
            int h = H - 14 - 68 - 4;          // tope de la barra de estado, menos un pequeno respiro
            if (h < 260) h = H;               // pantalla muy baja: cubrir todo (respaldo)
            return new Rectangle(0, 0, W, h);
        }

        private void OpenSearch()
        {
            searchOpen = true;
            searchLastActivity = DateTime.Now;
            lastQuery = null; lastMatches = null;   // empezar busqueda limpia
            if (video != null) video.Visible = false;   // el ActiveX se pinta encima del buscador; lo ocultamos
            searchOverlay.Accent = bgAccent;
            searchOverlay.SetQuery("");
            searchOverlay.Results = new List<SearchItem>();
            searchOverlay.SelRow = 1; searchOverlay.SelCol = 0;
            searchOverlay.Bounds = SearchOverlayRect();
#if ROCOLA_GPU
            if (gpuEnabled && gpuPanel != null)
            {
                // El buscador se compone por GPU: el control CPU NO se pinta (queda oculto detras del panel).
                // La entrada sigue funcionando por el formulario (ProcessCmdKey + Form_KeyPress con KeyPreview).
                searchOverlay.Visible = false;
                gpuPanel.Visible = true; gpuPanel.BringToFront();
                SyncGpuFullScreen();   // aplica el modo buscador YA (ShowAlbum=false + UI urgente): transicion atomica, sin ver la rejilla un instante
                StartWinTrans(true);   // la ventana del buscador se abre creciendo (tipo macOS)
            }
            else
#endif
            {
                searchOverlay.Visible = true;
                searchOverlay.BringToFront();
                searchOverlay.Focus();
            }
            searchOverlay.Invalidate();
        }

        private void CloseSearch()
        {
#if ROCOLA_GPU
            StartWinTrans(false);   // copia el buscador AHORA (en el proximo cuadro la textura de UI ya sera la rejilla)
#endif
            searchOpen = false;
            searchOverlay.Visible = false;
            if (videoOn && video != null) { video.Visible = true; video.BringToFront(); }
            this.Focus();
#if ROCOLA_GPU
            // Aplica el cambio de modo YA (no esperes al proximo AnimTimer, ~40ms). SyncGpuFullScreen detecta la
            // transicion buscador->navegacion y fuerza rehacer la UI en el MISMO cuadro en que reaparece la
            // rejilla de discos; si no, se veria la rejilla por los huecos del teclado un instante.
            if (gpuPanel != null && gpuEnabled) SyncGpuFullScreen();
#endif
        }

        // OPTIMIZACION (busqueda): " " + q y q.Split(' ') se calculaban DENTRO de Score, que corre
        // por CADA entrada de searchIndex (decenas de miles) en CADA tecla. Ahora se reciben ya
        // calculados (una sola vez por tecla desde RecomputeResults). El puntaje es identico.
        private static int Score(string cand, string q, string spaceQ, string[] toks)
        {
            if (q.Length == 0) return 0;
            if (cand == q) return 10000;
            if (cand.StartsWith(q, StringComparison.Ordinal)) return 6000 - Math.Min(cand.Length, 2000);
            int wi = cand.IndexOf(spaceQ, StringComparison.Ordinal);
            if (wi >= 0) return 4000 - wi;
            int idx = cand.IndexOf(q, StringComparison.Ordinal);
            if (idx >= 0) return 2000 - Math.Min(idx, 1500);
            // Coincidencia por PALABRAS: todas las palabras de la consulta aparecen (en cualquier orden).
            // Asi "metallica black" encuentra "Metallica - Black Album", y "enter metallica" tambien.
            if (toks != null)
            {
                int sc = 0, real = 0;
                for (int i = 0; i < toks.Length; i++)
                {
                    string tk = toks[i];
                    if (tk.Length == 0) continue;
                    real++;
                    int at = cand.IndexOf(tk, StringComparison.Ordinal);
                    if (at < 0) return 0;   // falta una palabra -> no coincide
                    bool wordStart = (at == 0) || (cand[at - 1] == ' ');
                    sc += (wordStart ? 300 : 150) - Math.Min(at, 100);
                }
                if (real >= 2) return 800 + sc;   // menos que substring directo, pero cuenta como coincidencia
            }
            return 0;
        }

        private void RecomputeResults(string query)
        {
            MarkSearchActivity();
            if (libBuilding)   // el worker esta llenando searchIndex: no iterarlo (carrera = crash)
            {
                searchOverlay.Results = new List<SearchItem>();
                searchOverlay.Invalidate();
                return;
            }
            string q = Normalize(query);
            List<SearchItem> res = new List<SearchItem>();
            if (q.Length > 0)
            {
                // OPTIMIZACION (busqueda): partes derivadas de la consulta calculadas UNA vez por tecla,
                // no por cada candidato del indice.
                string spaceQ = " " + q;
                string[] toks = (q.IndexOf(' ') >= 0) ? q.Split(' ') : null;
                // Incremental: si la nueva busqueda EMPIEZA con la anterior, las coincidencias nuevas
                // son un subconjunto de las anteriores -> re-evaluamos solo esas (mucho menos trabajo).
                List<SearchItem> source =
                    (lastQuery != null && lastQuery.Length > 0 && lastMatches != null && q.StartsWith(lastQuery, StringComparison.Ordinal))
                    ? lastMatches : searchIndex;

                List<SearchItem> matches = new List<SearchItem>();
                for (int i = 0; i < source.Count; i++)
                {
                    int sc = Score(source[i].Key, q, spaceQ, toks);
                    if (sc > 0) { source[i].Score = sc; matches.Add(source[i]); }
                }
                matches.Sort(delegate (SearchItem a, SearchItem b)
                {
                    if (b.Score != a.Score) return b.Score - a.Score;
                    if (a.Kind != b.Kind) return a.Kind - b.Kind;
                    return string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase);
                });
                lastMatches = matches; lastQuery = q;
                int take = Math.Min(40, matches.Count);   // mas resultados (la cuadricula muestra los que quepan)
                for (int i = 0; i < take; i++) res.Add(matches[i]);
            }
            else { lastMatches = null; lastQuery = null; }
            searchOverlay.Results = res;
            searchOverlay.Invalidate();
        }

        private void ChooseSearchResult(SearchItem it)
        {
            if (it == null) return;
            MarkSearchActivity();
            if (it.Kind == 2 && it.TrackRef != null)
            {
                PlayItem p = new PlayItem(); p.IsVideo = false; p.Song = it.TrackRef; p.Disc = it.AlbumRef;
                AddResult r = AddItem(p);
                if (r == AddResult.Added) ShowSongAddedToast(true);
                else if (r == AddResult.NoCredit) ShowSearchToast("Inserta una moneda", false);
                else ShowSearchToast("Esa cancion ya va seguida", false);
                return; // no cierra el buscador
            }
            else if (it.Kind == 1 && it.AlbumRef != null)
            {
#if ROCOLA_GPU
                transNextFrom = SearchCubeRect(out transNextAngle);   // la portada sale del CUBO GRANDE del buscador (luego ya no existe)
#endif
                CloseSearch(); NavigateToAlbum(it.AlbumRef); EnterAlbum(it.AlbumRef);
            }
            else if (it.Kind == 0 && it.AlbumRef != null) { CloseSearch(); NavigateToAlbum(it.AlbumRef); }
        }

        private void ShowSearchToast(string msg) { ShowSearchToast(msg, true); }
        private void ShowSearchToast(string msg, bool ok)
        {
            toastKind = ToastKind.Other;   // por defecto NO acumulable; cancion lo re-marca tras llamar
            searchOverlay.ToastMsg = msg;
            searchOverlay.ToastBack = ok ? Color.FromArgb(238, 40, 165, 90) : Color.FromArgb(238, 205, 130, 35);
            searchOverlay.ToastOn = true;
            searchOverlay.Invalidate();
            toastTimer.Stop(); toastTimer.Start();
        }

        // ¿La tecla kc esta asignada a ALGUN boton de la rocola? (las teclas configuradas tienen prioridad)
        private bool IsConfiguredKey(Keys kc)
        {
            return kc == keyUp || kc == keyDown || kc == keyLeft || kc == keyRight
                || kc == keyOK || kc == keyBack || kc == keyCoin || kc == keySkip;
        }
        // Cada accion responde a SU boton de rocola configurado Y a la tecla de PC por defecto.
        // La tecla de PC solo cuenta si NO esta tomada por otro boton configurado (evita choques).
        private bool IsUp(Keys kc) { return kc == keyUp || (kc == Keys.Up && !IsConfiguredKey(Keys.Up)); }
        private bool IsDown(Keys kc) { return kc == keyDown || (kc == Keys.Down && !IsConfiguredKey(Keys.Down)); }
        private bool IsLeft(Keys kc) { return kc == keyLeft || (kc == Keys.Left && !IsConfiguredKey(Keys.Left)); }
        private bool IsRight(Keys kc) { return kc == keyRight || (kc == Keys.Right && !IsConfiguredKey(Keys.Right)); }
        private bool IsOK(Keys kc) { return kc == keyOK || (kc == Keys.Enter && !IsConfiguredKey(Keys.Enter)); }
        private bool IsBack(Keys kc) { return kc == keyBack || (kc == Keys.Escape && !IsConfiguredKey(Keys.Escape)); }
        private bool IsCoin(Keys kc) { return kc == keyCoin || (kc == Keys.Insert && !IsConfiguredKey(Keys.Insert)); }
        private bool IsSkip(Keys kc) { return kc == keySkip || (kc == Keys.ShiftKey && !IsConfiguredKey(Keys.ShiftKey)); }
        // ¿Esta presionado AHORA el boton REGRESAR (rocola) o Esc (teclado)?  (para los combos)
        private bool BackHeld() { return KeyHeld(keyBack) || (KeyHeld(Keys.Escape) && !IsConfiguredKey(Keys.Escape)); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            lastInput = DateTime.Now;   // cualquier tecla reinicia la inactividad
            lastInputTick = Environment.TickCount;   // el precalentador de miniaturas pausa si hay actividad
            if (attractOn)
            {
                // Cualquier boton despierta la rocola... pero la MONEDA ademas ACREDITA. Antes este return se
                // tragaba el pulso del monedero: el cliente metia su moneda durante la atraccion (el estado MAS
                // comun al acercarse, entra sola a los 40s de reposo) y SU DINERO SE PERDIA sin dar creditos.
                ExitAttract();
                Keys akc = keyData & Keys.KeyCode;
                if (IsCoin(akc) && (msg.LParam.ToInt64() & 0x40000000) == 0) AddCoin();   // mismo filtro de auto-repeticion que abajo
                return true;
            }

            // ASISTENTE DE CONFIGURACION: captura el boton que se presiona para cada funcion.
            if (configMode) { CaptureConfigKey(keyData, msg); return true; }

            bool wasFull = videoFull;
            Keys kc = keyData & Keys.KeyCode;   // tecla base (sin modificadores)

            // ===== ENTRADAS ESPECIALES: SIEMPRE funcionan (buscador, video en pantalla completa, donde sea) =====
            // MONEDA (filtra AUTO-REPETICION: si la señal queda "mantenida", el teclado la repite y sumaria
            // creditos de mas; cada pulso real del monedero trae su propio pulsar/soltar y pasa siempre)
            if (IsCoin(kc)) { if ((msg.LParam.ToInt64() & 0x40000000) == 0) { if (wasFull) ExitVideoFull(); AddCoin(); } return true; }

            // SALTAR: salta la cancion/video actual SIEMPRE (filtra auto-repeticion)
            if (IsSkip(kc) && (msg.LParam.ToInt64() & 0x40000000) == 0)
            {
                SkipSong();   // si el video estaba en pantalla completa, StopVideoPlayback ya sale de ella
                return true;
            }

            // Cualquier OTRA tecla, si el video esta en pantalla completa, solo "despierta" la rocola.
            if (wasFull) { ExitVideoFull(); return true; }

            // BRILLO DE INSTRUCCIONES: al pulsar un boton fisico, resalta su simbolo en la barra de ayuda.
            // Mover: arriba/abajo ROJO, izq/der AMARILLO (como las flechas de la cruceta). Guia visual.
            if (searchOpen && searchOverlay != null)   // barra del buscador (Mover / Poner-Elegir / Cerrar)
            {
                if (IsOK(kc) && BackHeld()) searchOverlay.FlashClose();
                else if (IsOK(kc)) searchOverlay.FlashOK();
                else if (IsUp(kc) || IsDown(kc)) searchOverlay.FlashMove(true);
                else if (IsLeft(kc) || IsRight(kc)) searchOverlay.FlashMove(false);
            }
            else if (helpBar != null && !attractOn)    // barra de navegacion / interior de disco
            {
                if (IsOK(kc) && BackHeld()) { helpBar.Flash(HelpBar.FBuscar); helpBar.Flash(HelpBar.FEntrar); helpBar.Flash(HelpBar.FVolver); }   // combo: brillan el par Buscar Y los dos botones fisicos (verde + rojo) a la vez
                else if (IsOK(kc)) helpBar.Flash(HelpBar.FEntrar);
                else if (IsBack(kc)) helpBar.Flash(HelpBar.FVolver);
                else if (IsUp(kc) || IsDown(kc)) helpBar.FlashMove(true);
                else if (IsLeft(kc) || IsRight(kc)) helpBar.FlashMove(false);
            }

            // F5 = recargar musica (mantenimiento; sin boton visible)
            if (keyData == Keys.F5) { try { File.Delete(LibCacheFile()); } catch { } LoadMusicFolder(); return true; }   // F5 ignora el cache: re-escaneo completo
#if ROCOLA_GPU
            if (keyData == Keys.F12) { GpuTestWindow.Toggle(); return true; }   // F12 = ventana de prueba del render por GPU (Direct3D 9)
            if (keyData == (Keys.Control | Keys.F12)) { ToggleAlbumGpu(); return true; }   // Ctrl+F12 = fondo de discos por GPU (experimental)
            if (keyData == (Keys.Control | Keys.F11))   // Ctrl+F11 = alterna el interior de disco entre GPU (por defecto) y CPU (valvula de escape)
            {
                gpuDiscTry = !gpuDiscTry;
                uiOverlaySig = SigNada;   // fuerza rehacer la textura de UI
                if (lblStatus != null) lblStatus.Text = "Interior de disco por " + (gpuDiscTry ? "GPU" : "CPU");
                SyncGpuFullScreen(); Invalidate(true);
                return true;
            }
            if (keyData == (Keys.Control | Keys.F1))   // Ctrl+F1 = muestra/oculta el contador de FPS (oculto por defecto)
            {
                gpuShowFps = !gpuShowFps;
                if (gpuPanel != null) gpuPanel.ShowFps = gpuShowFps;
                return true;
            }
#endif
            if (keyData == (Keys.Control | Keys.F2))   // Ctrl+F2 = gestor de carpetas de musica (agregar/quitar; otros discos)
            {
                ShowMusicDirManager();
                return true;
            }
            if (keyData == (Keys.Control | Keys.F3))   // Ctrl+F3 = ajuste del nivelado de volumen (que todo suene parejo)
            {
                ShowVolumeManager();
                return true;
            }
            if (keyData == Keys.F8 && !IsConfiguredKey(Keys.F8))    // F8 = relanzar el asistente de botones
            {
                if (searchOpen) CloseSearch();
                StartConfig();
                return true;
            }
            if (keyData == Keys.F10 && !IsConfiguredKey(Keys.F10))   // F10 = mantenimiento: pausa/reactiva el vigia de foco
            {
                maintMode = !maintMode;
                if (maintMode) { maintUntil = DateTime.Now.AddMinutes(30); ShowToast("Mantenimiento: vigia pausado 30 min", Color.FromArgb(200, 130, 35)); }
                else ShowToast("Vigia de pantalla reactivado");
                return true;
            }

            // BUSCAR: manten REGRESAR y presiona PONER
            if (IsOK(kc) && BackHeld()) { ToggleSearch(); return true; }

            if (searchOpen)
            {
                MarkSearchActivity();
                if (IsLeft(kc)) { searchOverlay.MoveSel(-1, 0); return true; }
                if (IsRight(kc)) { searchOverlay.MoveSel(1, 0); return true; }
                if (IsUp(kc)) { searchOverlay.MoveSel(0, -1); return true; }
                if (IsDown(kc)) { searchOverlay.MoveSel(0, 1); return true; }
                if (IsOK(kc)) { searchOverlay.PressSelected(); return true; }
                if (IsBack(kc)) { searchOverlay.Backspace(); return true; }   // REGRESAR = borrar letra; cerrar = REGRESAR+PONER
                // letras, numeros, Ñ, acentos, comillas, espacio y BORRAR los entrega KeyPress (admite TODO caracter)
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (panelSongs.Visible)
            {
                int rpc = SongRowsPerPage();   // misma cuenta que la paginacion: izq/der = pagina anterior/siguiente
                // Con ROJO mantenido, arriba/abajo NO navegan: queda reservado para el combo de ocultar
                // (ROJO+ARRIBA+ABAJO 5 s), asi la lista no salta mientras se mantiene el combo.
                if (IsUp(kc)) { if (!BackHeld()) MoveSong(-1); return true; }
                if (IsDown(kc)) { if (!BackHeld()) MoveSong(1); return true; }
                if (IsLeft(kc)) { MoveSong(-rpc); return true; }     // pagina anterior
                if (IsRight(kc)) { MoveSong(rpc); return true; }      // pagina siguiente
                if (IsOK(kc)) { EnqueueSelectedSong(); return true; }
                if (IsBack(kc)) { BackToAlbums(); return true; }
            }
            else
            {
                // REGRESAR + IZQ/DER = saltar de letra (A,B,C...). Solo IZQ/DER = mover de disco.
                if (IsLeft(kc)) { if (BackHeld()) ChangeLetter(-1); else MoveInPage(-1); return true; }
                if (IsRight(kc)) { if (BackHeld()) ChangeLetter(1); else MoveInPage(1); return true; }
                // Con ROJO mantenido, arriba/abajo NO paginan: reservado para el combo de vetar (5 s).
                if (IsUp(kc)) { if (!BackHeld()) NextPage(); return true; }
                if (IsDown(kc)) { if (!BackHeld()) PrevPage(); return true; }
                if (IsOK(kc)) { List<Album> l = CurrentList(); if (selAlbumIndex >= 0 && selAlbumIndex < l.Count) EnterAlbum(l[selAlbumIndex]); return true; }
                if (IsBack(kc)) { return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // =================== ASISTENTE DE CONFIGURACION DE BOTONES ===================
        private string KeysFile() { return Path.Combine(Application.StartupPath, "rocola_teclas.txt"); }

        // Carga el mapeo de botones. Devuelve true solo si estan los 8 completos.
        private bool LoadKeys()
        {
            try
            {
                string f = KeysFile();
                if (!File.Exists(f)) return false;
                Keys u = Keys.None, d = Keys.None, l = Keys.None, r = Keys.None, ok = Keys.None, bk = Keys.None, co = Keys.None, sk = Keys.None;
                string[] lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    string s = lines[i].Trim();
                    int eq = s.IndexOf('=');
                    if (eq <= 0) continue;
                    string name = s.Substring(0, eq).Trim().ToLower();
                    int val;
                    if (!int.TryParse(s.Substring(eq + 1).Trim(), out val)) continue;
                    Keys k = (Keys)val;
                    if (name == "arriba") u = k;
                    else if (name == "abajo") d = k;
                    else if (name == "izquierda") l = k;
                    else if (name == "derecha") r = k;
                    else if (name == "poner") ok = k;
                    else if (name == "regresar") bk = k;
                    else if (name == "moneda") co = k;
                    else if (name == "saltar") sk = k;
                }
                if (u == Keys.None || d == Keys.None || l == Keys.None || r == Keys.None ||
                    ok == Keys.None || bk == Keys.None || co == Keys.None || sk == Keys.None) return false;
                keyUp = u; keyDown = d; keyLeft = l; keyRight = r; keyOK = ok; keyBack = bk; keyCoin = co; keySkip = sk;
                return true;
            }
            catch { return false; }
        }

        private void SaveKeys()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("arriba=").Append((int)keyUp).Append("\r\n");
                sb.Append("abajo=").Append((int)keyDown).Append("\r\n");
                sb.Append("izquierda=").Append((int)keyLeft).Append("\r\n");
                sb.Append("derecha=").Append((int)keyRight).Append("\r\n");
                sb.Append("poner=").Append((int)keyOK).Append("\r\n");
                sb.Append("regresar=").Append((int)keyBack).Append("\r\n");
                sb.Append("moneda=").Append((int)keyCoin).Append("\r\n");
                sb.Append("saltar=").Append((int)keySkip).Append("\r\n");
                File.WriteAllText(KeysFile(), sb.ToString());
            }
            catch { }
        }

        // Inicia el asistente (primera vez, o cuando se escriben diez ceros en el buscador).
        private void StartConfig()
        {
            if (searchOpen) CloseSearch();
            configMode = true;
            configStep = 0;
            for (int i = 0; i < configCap.Length; i++) configCap[i] = Keys.None;
            if (lblConfig != null) { lblConfig.Visible = true; lblConfig.BringToFront(); }
            UpdateConfigPrompt(null);
        }

        private void UpdateConfigPrompt(string warn)
        {
            if (lblConfig == null) return;
            string t = "CONFIGURACION DE BOTONES DE LA ROCOLA\r\n\r\n";
            t += "Paso " + (configStep + 1) + " de " + ConfigSteps.Length + "\r\n\r\n";
            t += "Presiona en la rocola el boton para:\r\n\r\n";
            t += ">>>   " + ConfigSteps[configStep] + "   <<<";
            if (warn != null) t += "\r\n\r\n" + warn;
            t += "\r\n\r\n\r\n(cada boton debe ser distinto)";
            lblConfig.Text = t;
            lblConfig.BringToFront();
        }

        // Captura el boton presionado durante la configuracion.
        private void CaptureConfigKey(Keys keyData, Message msg)
        {
            if ((msg.LParam.ToInt64() & 0x40000000) != 0) return;   // ignora auto-repeticion (boton mantenido)
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.None || k == Keys.Menu || k == Keys.ControlKey) return;   // ignora teclas vacias/control sueltas
            for (int j = 0; j < configStep; j++)
                if (configCap[j] == k) { UpdateConfigPrompt("Ese boton ya esta usado. Presiona OTRO."); return; }
            configCap[configStep] = k;
            configStep++;
            if (configStep >= ConfigSteps.Length) FinishConfig();
            else UpdateConfigPrompt(null);
        }

        private void FinishConfig()
        {
            keyUp = configCap[0]; keyDown = configCap[1]; keyLeft = configCap[2]; keyRight = configCap[3];
            keyOK = configCap[4]; keyBack = configCap[5]; keyCoin = configCap[6]; keySkip = configCap[7];
            SaveKeys();
            configMode = false;
            needConfig = false;
            if (lblConfig != null) lblConfig.Visible = false;
            this.Focus();
            ShowToast("¡Botones configurados!");
        }

        private void ShowToast(string text) { ShowToast(text, Color.FromArgb(40, 165, 90)); }
        private void ShowToast(string text, Color back)
        {
            toastKind = ToastKind.Other;   // por defecto NO acumulable; moneda/cancion lo re-marcan tras llamar
            int tw = ToastW, th = ToastH;
#if ROCOLA_GPU
            // Con el panel GPU el aviso es un cuadro mas de la GPU, no una etiqueta de Windows encima: entra
            // deslizandose y se va desvaneciendo, y sobre todo NO obliga a Windows a recortar el panel y
            // repintarlo (eso era un tiron justo al meter la moneda). Ver HorneaToast/BuildToastState.
            if (gpuEnabled)
            {
                HorneaToast(text, back);
                if (lblToast.Visible) lblToast.Visible = false;
                toastTimer.Stop(); toastTimer.Start();   // el temporizador sigue mandando para el aviso del buscador
                return;
            }
#endif
            lblToast.Text = text;
            lblToast.BackColor = back;
            tw = AnchoToast(text, lblToast.Font);   // misma pastilla que por GPU: se ajusta al texto
            lblToast.SetBounds((ClientSize.Width - tw) / 2, 80, tw, th);
            try { using (GraphicsPath gp = RoundRect(new Rectangle(0, 0, tw, th), 16)) lblToast.Region = new Region(gp); } catch { }  // esquinas redondeadas SIEMPRE
            lblToast.Visible = true;
            lblToast.BringToFront();
            toastTimer.Stop(); toastTimer.Start();
        }

        // El aviso se dibuja SIEMPRE en un bitmap de este tamano (por eso la textura se reusa y no cuesta nada),
        // pero la pastilla de color se ajusta al texto y va centrada dentro. Antes era fija en 380 y los avisos
        // largos ("No se pudo abrir el video: credito devuelto") salian cortados.
        private const int ToastW = 620, ToastH = 48, ToastMinW = 260;
        // Ancho de la pastilla para ESE texto (acotado): lo usan las dos formas de dibujarla, por GPU y por CPU,
        // para que se vea igual en las dos.
        internal static int AnchoToast(string text, Font font)
        {
            int w = TextRenderer.MeasureText(text, font).Width + 44;
            return w < ToastMinW ? ToastMinW : (w > ToastW ? ToastW : w);
        }
#if ROCOLA_GPU
        private Bitmap toastBmp; private int toastVer, toastTick = -1;
        private const int ToastMs = 1300, ToastInMs = 190, ToastOutMs = 260;   // ToastMs = lo mismo que dura el temporizador

        // Dibuja el aviso en su propio bitmap (se REUSA siempre el mismo: cero basura para el recolector) y le
        // sube la version, que es lo que hace que la GPU vuelva a subirlo.
        private void HorneaToast(string text, Color back)
        {
            if (toastBmp == null) toastBmp = new Bitmap(ToastW, ToastH, PixelFormat.Format32bppPArgb);
            PintaToast(toastBmp, text, back, lblToast.Font);
            toastVer++; toastTick = Environment.TickCount;
        }

        // El dibujo en si, aparte del estado: asi se puede ver y comprobar sin arrancar la rocola entera.
        internal static void PintaToast(Bitmap bmp, string text, Color back, Font font)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;   // gris: ClearType deja flecos sobre transparente
                int tw = AnchoToast(text, font);   // el texto MANDA el ancho de la pastilla
                int x0 = (ToastW - tw) / 2;
                using (GraphicsPath gp = RoundRect(new Rectangle(x0, 0, tw, ToastH), 16))
                {
                    using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, gp);
                    using (Pen pe = new Pen(Color.FromArgb(70, 255, 255, 255), 1.5f)) g.DrawPath(pe, gp);   // filo claro: lo despega del fondo
                }
                using (SolidBrush b = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(text, font, b, new RectangleF(x0 + 10, 0, tw - 20, ToastH), sf);
                }
            }
        }

        // CURVA del aviso (funcion pura, para poder comprobarla): baja desde arriba mientras aparece, se queda
        // quieto, y al final sube un poco mientras se desvanece.
        internal static void CurvaToast(int dt, out float alpha, out float dy)
        {
            alpha = 1f; dy = 0f;
            if (dt < 0 || dt >= ToastMs) { alpha = 0f; return; }
            if (dt < ToastInMs)
            {
                float q = dt / (float)ToastInMs;
                float e = 1f - (1f - q) * (1f - q) * (1f - q);   // frenado suave al llegar
                alpha = e; dy = -30f * (1f - e);
            }
            else if (dt > ToastMs - ToastOutMs)
            {
                float q = (dt - (ToastMs - ToastOutMs)) / (float)ToastOutMs;
                alpha = 1f - q; dy = -16f * q;
            }
        }

        // Estado del aviso para la GPU: entra deslizandose desde arriba mientras aparece, se queda quieto, y
        // al final sube un poco mientras se desvanece. El avance va con el reloj, asi que se ve fluido a los
        // fps que de el equipo (no a los 24 del temporizador).
        private GpuToast BuildToastState()
        {
            GpuToast t = new GpuToast();
            if (toastTick < 0 || toastBmp == null) return t;
            int dt = Environment.TickCount - toastTick;
            if (dt < 0 || dt >= ToastMs) { toastTick = -1; return t; }
            float a, dy; CurvaToast(dt, out a, out dy);
            t.Has = true; t.Bmp = toastBmp; t.Ver = toastVer;
            t.X = (ClientSize.Width - ToastW) * 0.5f;
            t.Y = 80f + dy;
            t.Alpha = a;
            return t;
        }
#endif

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d < 1) d = 1;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ---- Cola unificada (canciones + videos, en orden) + creditos ----
        private enum AddResult { Added, NoCredit, Repeat }

        private AddResult AddItem(PlayItem it)
        {
            if (it == null) return AddResult.Repeat;
            if (credits <= 0) return AddResult.NoCredit;                  // sin creditos no se puede poner nada
            // no repetir lo mismo dos veces seguidas (lo ultimo en sonar/encolar)
            PlayItem last = (queue.Count > 0) ? queue[queue.Count - 1] : nowPlaying;
            if (last != null && string.Equals(last.PathKey(), it.PathKey(), StringComparison.OrdinalIgnoreCase)) return AddResult.Repeat;
            queue.Add(it);
            RecordPlay(it);                                              // estadisticas: cuenta lo pedido
            credits--;                                                   // cada cosa cuesta 1 credito
            UpdateCredits();
            if (nowPlaying == null) PlayNext();   // nada sonando -> arranca (PlayNext guarda el estado); si algo suena, solo se encola
            else { UpdateNowPlayingUI(); SaveState(); }   // estado vivo: cola/creditos cambiaron
            return AddResult.Added;
        }

        private void AddSong(Track t, Album disc)
        {
            if (t == null) return;
            PlayItem it = new PlayItem(); it.IsVideo = false; it.Song = t; it.Disc = disc;
            AddResult r = AddItem(it);
            if (r == AddResult.Added) ShowSongAddedToast(false);
            else if (r == AddResult.NoCredit) ShowToast("Inserta una moneda", Color.FromArgb(200, 130, 35));
            else ShowToast("Esa ya va seguida", Color.FromArgb(200, 130, 35));
        }

        // Aviso de cancion agregada, con acumulacion: si agregas varias rapido (mientras el aviso sigue),
        // dice "Cancion agregada", "Cancion agregada  X2", "X3"... para que quede claro cuantas van y no confunda.
        private void ShowSongAddedToast(bool search)
        {
            songTotal = SameToastActive(ToastKind.Song) ? songTotal + 1 : 1;
            string msg = songTotal > 1 ? "Cancion agregada  X" + songTotal : "Cancion agregada";
            if (search) ShowSearchToast(msg, true); else ShowToast(msg);
            MarkToast(ToastKind.Song);
        }

        private void AddVideoToQueue(string path, Album disc)
        {
            if (string.IsNullOrEmpty(path)) return;
            PlayItem it = new PlayItem(); it.IsVideo = true; it.VideoPath = path; it.Disc = disc;
            AddResult r = AddItem(it);
            if (r == AddResult.Added) ShowToast("Video agregado a la cola");
            else if (r == AddResult.NoCredit) ShowToast("Inserta una moneda", Color.FromArgb(200, 130, 35));
            else ShowToast("Ese video ya va seguido", Color.FromArgb(200, 130, 35));
        }

        // Suma creditos (la moneda) y actualiza el letrero
        private void AddCoin()
        {
            credits += CreditsPerCoin;
            statsCoins++; statsCreditsAdded += CreditsPerCoin; statsDirty = true;   // estadisticas
            UpdateCredits();
            SaveState();   // estado vivo: si se corta la luz, el dinero del cliente no se pierde
            // Monedas rapidas seguidas: el aviso SUMA (+4, +8, +12...) en vez de reiniciar; si ya paso su
            // tiempo (el usuario fue lento), arranca de nuevo en +4.
            coinTotal = SameToastActive(ToastKind.Coin) ? coinTotal + CreditsPerCoin : CreditsPerCoin;
            ShowToast("+" + coinTotal + " creditos", Color.FromArgb(40, 165, 90));
            MarkToast(ToastKind.Coin);
        }

        // ===================== ESTADO VIVO (reanudar tras un corte de luz) =====================
        // Guarda creditos + lo que suena + la cola en rocola_estado.txt (solo cambia en eventos: moneda,
        // encolar, cambio de cancion). Si se corta la luz, al volver la rocola RETOMA: la cancion que estaba
        // sonando arranca DESDE EL PRINCIPIO (a peticion: media cancion confunde mas de lo que ayuda), con su
        // cola y los creditos del cliente intactos. Para el CIERRE del negocio: SALTAR repetido vacia la cola
        // y, con todo vacio, una pulsacion de SALTAR borra los creditos -> estado vacio -> arranca limpia.
        // El archivo se DESCARTA por tres vias que distinguen "cierre" de "apagon": (1) apagado deliberado
        // (boton de poder -> Windows apaga, u Alt+F4) borra el archivo en OnFormClosing; (2) al cruzar las 7 AM
        // estando ociosa (CheckBusinessDayReset); (3) al restaurar, si el estado ya cruzo las 7 AM y no es reciente.
        // Un apagon no dispara nada de esto -> el archivo sobrevive y la rocola reanuda.
        private string StateFile() { return Path.Combine(Application.StartupPath, "rocola_estado.txt"); }
        private bool stateRestored = false;   // la restauracion corre UNA vez (al primer armado de biblioteca)

        // El estado de recuperacion pertenece a UN "dia del negocio", que ARRANCA a las 7 AM (hora del sistema): a
        // esa hora ya cerro, y nada de la noche anterior debe cruzar las 7 AM. Todo lo de antes de las 7 AM cuenta
        // como el dia ANTERIOR. Al pasar esa hora: el estado guardado se DESCARTA al restaurar (arranque limpio en
        // la manana) y se LIMPIA si la rocola sigue encendida (creditos/cola a cero) -> cada dia arranca en cero.
        private const int BusinessDayStartHour = 7;
        private const int DayResetIdleMins = 10;   // minutos OCIOSA (sin musica/cola/botones) para aplicar el reinicio de 7 AM: si te desvelas, NO te interrumpe
        private const int StateRecentMins = 30;    // si el estado se guardo hace menos de esto, se reanuda aunque cruce las 7 AM (fue un corte breve en plena sesion)
        private static DateTime BusinessDay(DateTime dt) { return dt.AddHours(-BusinessDayStartHour).Date; }
        private DateTime stateDay = BusinessDay(DateTime.Now);   // dia del negocio del estado vivo actual

        private void SaveState()
        {
            try
            {
                StringBuilder sb = new StringBuilder(256);
                sb.Append("ROCOLA-ESTADO v1\r\n");
                sb.Append("ts=").Append(DateTime.Now.Ticks).Append("\r\n");   // cuando se guardo (para descartarlo si cruza las 7 AM)
                sb.Append("creditos=").Append(credits).Append("\r\n");
                if (nowPlaying != null)
                    sb.Append("sonando=").Append(nowPlaying.IsVideo ? 'V' : 'S').Append('|').Append(nowPlaying.PathKey()).Append("\r\n");
                for (int i = 0; i < queue.Count; i++)
                    sb.Append("cola=").Append(queue[i].IsVideo ? 'V' : 'S').Append('|').Append(queue[i].PathKey()).Append("\r\n");
                File.WriteAllText(StateFile(), sb.ToString());   // archivo diminuto (<1KB): escritura sincrona sin costo notable
            }
            catch { }
        }

        // Al cruzar las 7 AM (nuevo dia del negocio) reinicia el estado guardado. PERO nunca interrumpe una sesion
        // en uso: si te DESVELAS, tu musica NO deja de sonar y tus creditos NO se tocan. El reinicio ESPERA a que la
        // rocola quede OCIOSA (nada sonando, cola vacia y sin botones un buen rato); recien entonces borra los
        // creditos que hayan quedado (de alguien que se fue) y el archivo guardado -> el dia arranca limpio.
        private void CheckBusinessDayReset()
        {
            if (BusinessDay(DateTime.Now) == stateDay) return;                          // mismo dia del negocio: comun, nada que hacer
            if (nowPlaying != null || queue.Count > 0) return;                          // hay musica o cola: EN USO -> se aplaza
            if ((DateTime.Now - lastInput).TotalMinutes < DayResetIdleMins) return;     // botones recientes: EN USO -> se aplaza
            stateDay = BusinessDay(DateTime.Now);                                       // ociosa y dia nuevo -> reinicia UNA vez
            if (credits > 0) { credits = 0; UpdateCredits(); }                          // creditos que quedaron de anoche -> a cero
            try { File.Delete(StateFile()); } catch { }                                 // borra el estado guardado -> arranque limpio
            BackupLearnedData();                                                        // cierre del dia: respalda lo aprendido (una vez al dia)
        }

        // ===================== RESPALDO DE DATOS APRENDIDOS =====================
        // El disco puede morir (ya paso). La musica no se recupera, pero SI todo lo que la rocola aprendio:
        // volumenes nivelados, huellas del EQ, estadisticas de lo mas sonado, indice de biblioteca,
        // config, mapa de botones y el modelo de IA. Se copia a respaldo\<fecha>\ (y a respaldo_dir= si se
        // configuro, idealmente OTRO disco), guardando los 3 dias mas recientes. Son unos KB: barato y rapido.
        private string BackupBaseDir() { return Path.Combine(Application.StartupPath, "respaldo"); }
        private void BackupLearnedData()
        {
            string sp = Application.StartupPath, second = backupDir, baseDir = BackupBaseDir();
            string[] files = {
                LoudFile(), EqFile(), LibStatsFile(), LibCacheFile(), ConfigFile, KeysFile(),
                Path.Combine(sp, "rocola_ia.bin"), Path.Combine(sp, "rocola_incompatibles.txt"),
                IaDoneFile(), DeepDoneFile(),  // sin estas dos, un repaso a fondo empezaria de cero y la IA repetiria ejemplos
                DurFastFile()
            };
            System.Threading.ThreadPool.QueueUserWorkItem(delegate   // fuera del hilo de la interfaz (no frena nada)
            {
                try
                {
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd");
                    BackupTo(Path.Combine(baseDir, stamp), files); PruneBackups(baseDir, 3);
                    if (!string.IsNullOrEmpty(second))
                    { string s2 = Path.Combine(second, "respaldo_rocola"); try { BackupTo(Path.Combine(s2, stamp), files); PruneBackups(s2, 3); } catch { } }
                }
                catch { }
            });
        }
        private static void BackupTo(string dir, string[] files)
        {
            Directory.CreateDirectory(dir);
            for (int i = 0; i < files.Length; i++)
                try { if (File.Exists(files[i])) File.Copy(files[i], Path.Combine(dir, Path.GetFileName(files[i])), true); }
                catch { }
        }
        // Deja solo las 'keep' carpetas de respaldo mas recientes (por fecha de modificacion); borra el resto.
        private static void PruneBackups(string baseDir, int keep)
        {
            try
            {
                DirectoryInfo bd = new DirectoryInfo(baseDir);
                if (!bd.Exists) return;
                DirectoryInfo[] dirs = bd.GetDirectories();
                if (dirs.Length <= keep) return;
                Array.Sort(dirs, delegate (DirectoryInfo a, DirectoryInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                for (int i = keep; i < dirs.Length; i++) { try { dirs[i].Delete(true); } catch { } }
            }
            catch { }
        }

        // Cuando se guardo el estado: la marca "ts=" (ticks); si falta (formato viejo), la fecha del archivo.
        private static DateTime SavedStateTime(string[] lines, string file)
        {
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].StartsWith("ts="))
                { long t; if (long.TryParse(lines[i].Substring(3), out t) && t > 0) { try { return new DateTime(t); } catch { } } break; }
            try { return File.GetLastWriteTime(file); } catch { return DateTime.Now; }
        }

        // Reconstruye creditos/cola/cancion desde el archivo de estado (si hay algo que reanudar).
        private void RestoreState()
        {
            if (stateRestored) return;
            stateRestored = true;
            try
            {
                string f = StateFile();
                if (!File.Exists(f)) return;
                string[] L = File.ReadAllLines(f);
                if (L.Length < 1 || L[0] != "ROCOLA-ESTADO v1") return;
                // El estado solo vale DENTRO del mismo dia del negocio (corte a las 7 AM): lo de anoche, visto ya en
                // la manana, se descarta (arranque limpio). PERO si se guardo hace poquito (corte breve en plena
                // sesion, p.ej. un parpadeo de luz justo al dar las 7 mientras te desvelas), se reanuda igual.
                DateTime saved = SavedStateTime(L, f);
                if (BusinessDay(DateTime.Now) != BusinessDay(saved) && (DateTime.Now - saved).TotalMinutes > StateRecentMins)
                { try { File.Delete(f); } catch { } return; }
                // mapas ruta -> cancion/disco para reconstruir los elementos (solo lo que SIGA existiendo)
                Dictionary<string, Track> byPath = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, Album> discByPath = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < albums.Count; i++)
                    for (int j = 0; j < albums[i].Tracks.Count; j++)
                    {
                        Track t = albums[i].Tracks[j];
                        if (t != null && t.Path != null && !byPath.ContainsKey(t.Path)) { byPath[t.Path] = t; discByPath[t.Path] = albums[i]; }
                    }
                Dictionary<string, Album> vidDisc = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
                if (videoAlbums != null)
                    for (int i = 0; i < videoAlbums.Count; i++)
                        for (int j = 0; j < videoAlbums[i].VideoFiles.Count; j++)
                        { string vp = videoAlbums[i].VideoFiles[j]; if (vp != null && !vidDisc.ContainsKey(vp)) vidDisc[vp] = videoAlbums[i]; }

                int cr = 0; PlayItem playing = null; List<PlayItem> q = new List<PlayItem>();
                for (int i = 1; i < L.Length; i++)
                {
                    string s = L[i];
                    if (s.Length == 0) continue;
                    if (s.StartsWith("creditos=")) { int.TryParse(s.Substring(9), out cr); continue; }
                    if (s.StartsWith("pos=")) continue;   // formato viejo: la posicion ya no se usa (siempre desde el principio)
                    bool isNow = s.StartsWith("sonando=");
                    if (!isNow && !s.StartsWith("cola=")) continue;
                    string v = s.Substring(s.IndexOf('=') + 1);
                    if (v.Length < 3 || v[1] != '|') continue;
                    bool isVid = (v[0] == 'V'); string path = v.Substring(2);
                    PlayItem it = null;
                    if (isVid)
                    {
                        bool exists = false; try { exists = File.Exists(path); } catch { }
                        if (exists) { it = new PlayItem(); it.IsVideo = true; it.VideoPath = path; Album d; vidDisc.TryGetValue(path, out d); it.Disc = d; }
                    }
                    else
                    {
                        Track t;
                        if (byPath.TryGetValue(path, out t)) { it = new PlayItem(); it.Song = t; Album d; discByPath.TryGetValue(path, out d); it.Disc = d; }
                    }
                    if (it == null) continue;   // el archivo ya no existe / se re-escaneo distinto: se omite
                    if (isNow) playing = it; else q.Add(it);
                }
                if (cr <= 0 && playing == null && q.Count == 0) return;   // estado vacio: arranque limpio
                credits = Math.Max(0, cr);
                UpdateCredits();
                queue.AddRange(q);
                if (playing != null)
                {
                    queue.Insert(0, playing);   // lo que estaba sonando vuelve a entrar y arranca DESDE EL PRINCIPIO
                    PlayNext();
                    ShowToast("Reanudando la musica");
                }
                else UpdateNowPlayingUI();
            }
            catch { }
        }

        // ===================== MOTOR DE ESTADISTICAS =====================
        private string LibStatsFile() { return Path.Combine(Application.StartupPath, "rocola_stats.txt"); }

        // Registra una reproduccion pedida (algo que entro a la cola con exito).
        private void RecordPlay(PlayItem it)
        {
            if (it == null) return;
            statsTotal++;
            if (it.IsVideo)
            {
                statsVideoTotal++;
                string vp = it.VideoPath;
                if (!string.IsNullOrEmpty(vp)) { int n; videoCounts.TryGetValue(vp, out n); videoCounts[vp] = n + 1; videoSeq[vp] = ++statsSeq; }
            }
            else if (it.Song != null && !string.IsNullOrEmpty(it.Song.Path))
            {
                statsSongTotal++;
                int n; songCounts.TryGetValue(it.Song.Path, out n); songCounts[it.Song.Path] = n + 1;
                songSeq[it.Song.Path] = ++statsSeq;
                if (!string.IsNullOrEmpty(it.Song.Genre) && !songGenre.ContainsKey(it.Song.Path)) songGenre[it.Song.Path] = GenreLabel(it.Song.Genre);
                string ar = it.Song.Artist;
                if (!string.IsNullOrEmpty(ar)) { int a; artistCounts.TryGetValue(ar, out a); artistCounts[ar] = a + 1; }
                // primera cancion contada -> habilitar la seccion "Lo mas sonado" en la barra de letras
                if (letterAvailable != null && !letterAvailable.Contains(TopMark)) { letterAvailable.Add(TopMark); if (letterBar != null) letterBar.Invalidate(); }
            }
            statsDirty = true;
            topDirty = true;   // OPTIMIZACION (TOP): cambiaron counts/genero/artista -> reconstruir al entrar a TOP
        }

        private void StatsTimer_Tick(object sender, EventArgs e)
        {
            if (statsDirty) { statsDirty = false; SaveStats(true); }   // guardado periodico en segundo plano
        }

        // Mantiene el archivo acotado: descarta lo menos sonado (y mas viejo) para dejar entrar lo nuevo.
        private void PruneStats()
        {
            PruneCountDict(songCounts, songSeq, songGenre, SongStatCap, SongStatTarget);
            PruneCountDict(videoCounts, videoSeq, null, VideoStatCap, VideoStatTarget);
            PruneCountDict(artistCounts, null, null, ArtistStatCap, ArtistStatTarget);
            if (songGenre.Count > songCounts.Count)   // limpiar generos huerfanos
            {
                List<string> orphans = new List<string>();
                foreach (string k in songGenre.Keys) if (!songCounts.ContainsKey(k)) orphans.Add(k);
                for (int i = 0; i < orphans.Count; i++) songGenre.Remove(orphans[i]);
            }
        }

        private void PruneCountDict(Dictionary<string, int> counts, Dictionary<string, long> seq, Dictionary<string, string> extra, int cap, int target)
        {
            if (counts.Count <= cap) return;
            List<string> keys = new List<string>(counts.Keys);
            keys.Sort(delegate (string a, string b)
            {
                int ca = counts[a], cb = counts[b];
                if (ca != cb) return ca - cb;              // menos sonado primero (candidato a borrar)
                long sa = 0, sb = 0; if (seq != null) { seq.TryGetValue(a, out sa); seq.TryGetValue(b, out sb); }
                if (sa != sb) return (sa < sb) ? -1 : 1;   // mas viejo primero
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            int remove = counts.Count - target;
            for (int i = 0; i < remove && i < keys.Count; i++)
            {
                string k = keys[i];
                counts.Remove(k);
                if (seq != null) seq.Remove(k);
                if (extra != null) extra.Remove(k);
            }
        }

        // Claves ordenadas por recencia (mas viejo primero): al guardarse asi, al recargar se reconstruye
        // el orden de recencia leyendo el archivo de arriba hacia abajo.
        private List<string> KeysBySeq(Dictionary<string, int> counts, Dictionary<string, long> seq)
        {
            List<string> ks = new List<string>(counts.Keys);
            ks.Sort(delegate (string a, string b)
            {
                long sa = 0, sb = 0; seq.TryGetValue(a, out sa); seq.TryGetValue(b, out sb);
                if (sa != sb) return (sa < sb) ? -1 : 1;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            return ks;
        }

        private void SaveStats(bool async)
        {
            PruneStats();   // acotar antes de escribir
            string text;
            try { text = BuildStatsText(); } catch { return; }   // se arma en el hilo de la interfaz (sin acceso cruzado a los diccionarios)
            if (async)
                System.Threading.ThreadPool.QueueUserWorkItem(delegate (object st) { try { File.WriteAllText(LibStatsFile(), text); } catch { } });
            else
                try { File.WriteAllText(LibStatsFile(), text); } catch { }   // al cerrar: sincrono (el proceso podria salir antes de un hilo de fondo)
        }

        private string BuildStatsText()
        {
            StringBuilder sb = new StringBuilder(songCounts.Count * 48 + 256);
            sb.Append("ROCOLA-STATS v1\r\n");
            sb.Append("total-reproducciones=").Append(statsTotal).Append("\r\n");
            sb.Append("total-canciones=").Append(statsSongTotal).Append("\r\n");
            sb.Append("total-videos=").Append(statsVideoTotal).Append("\r\n");
            sb.Append("monedas=").Append(statsCoins).Append("\r\n");
            sb.Append("creditos-cargados=").Append(statsCreditsAdded).Append("\r\n");
            sb.Append("[CANCIONES] count|ruta\r\n");
            List<string> sk = KeysBySeq(songCounts, songSeq);
            for (int i = 0; i < sk.Count; i++) sb.Append(songCounts[sk[i]]).Append('|').Append(sk[i]).Append("\r\n");
            sb.Append("[ARTISTAS] count|artista\r\n");
            foreach (KeyValuePair<string, int> kv in artistCounts) sb.Append(kv.Value).Append('|').Append(kv.Key).Append("\r\n");
            sb.Append("[VIDEOS] count|ruta\r\n");
            List<string> vk = KeysBySeq(videoCounts, videoSeq);
            for (int i = 0; i < vk.Count; i++) sb.Append(videoCounts[vk[i]]).Append('|').Append(vk[i]).Append("\r\n");
            sb.Append("[GENEROS] genero|ruta\r\n");
            foreach (KeyValuePair<string, string> kv in songGenre) sb.Append(kv.Value).Append('|').Append(kv.Key).Append("\r\n");
            return sb.ToString();
        }

        private void LoadStats()
        {
            try
            {
                string f = LibStatsFile();
                if (!File.Exists(f)) return;
                string[] L = File.ReadAllLines(f);
                if (L.Length < 1 || L[0] != "ROCOLA-STATS v1") return;
                int section = 0;   // 0=totales 1=canciones 2=artistas 3=videos
                for (int i = 1; i < L.Length; i++)
                {
                    string line = L[i];
                    if (line.Length == 0) continue;
                    if (line.StartsWith("[CANCIONES]")) { section = 1; continue; }
                    if (line.StartsWith("[ARTISTAS]")) { section = 2; continue; }
                    if (line.StartsWith("[VIDEOS]")) { section = 3; continue; }
                    if (line.StartsWith("[GENEROS]")) { section = 4; continue; }
                    if (section == 0)
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq); long v;
                        if (!long.TryParse(line.Substring(eq + 1), out v)) continue;
                        if (k == "total-reproducciones") statsTotal = v;
                        else if (k == "total-canciones") statsSongTotal = v;
                        else if (k == "total-videos") statsVideoTotal = v;
                        else if (k == "monedas") statsCoins = v;
                        else if (k == "creditos-cargados") statsCreditsAdded = v;
                    }
                    else
                    {
                        int b = line.IndexOf('|');
                        if (b <= 0) continue;
                        string key = line.Substring(b + 1);
                        if (key.Length == 0) continue;
                        if (section == 4) { songGenre[key] = line.Substring(0, b); continue; }   // genero|ruta (lo de antes del | es la etiqueta, no un numero)
                        int cnt; if (!int.TryParse(line.Substring(0, b), out cnt)) continue;
                        if (section == 1) { songCounts[key] = cnt; songSeq[key] = ++statsSeq; }
                        else if (section == 2) artistCounts[key] = cnt;
                        else if (section == 3) { videoCounts[key] = cnt; videoSeq[key] = ++statsSeq; }
                    }
                }
            }
            catch { }
        }

        private class TopEntry { public Track T; public int C; public Album A; public string V; }

        // Construye el disco virtual "Lo mas sonado" con las canciones MAS PEDIDAS (de las estadisticas),
        // ordenadas de mas a menos. Se arma al entrar a la seccion, asi siempre refleja lo actual.
        // Construye los discos virtuales "TOP - <genero>" con las canciones mas pedidas de cada genero.
        private void BuildTopDiscs()
        {
            if (!topDirty && topDiscs != null) return;   // OPTIMIZACION (TOP): nada cambio -> reusar lo ya armado
            if (libBuilding) return;                     // el worker esta mutando albums/videoAlbums: iterarlos seria una carrera (crash). topDirty queda puesto -> se reconstruye al terminar
            topDirty = false;                            // limpio AQUI: cubre todas las salidas (incluidas las tempranas)
            topDiscs = new List<Album>();
            if (songCounts.Count == 0) return;

            // 1) recolectar todas las canciones pedidas que EXISTEN en la biblioteca, con su disco de origen
            List<TopEntry> all = new List<TopEntry>();
            for (int i = 0; i < albums.Count; i++)
            {
                Album al = albums[i];
                for (int j = 0; j < al.Tracks.Count; j++)
                {
                    Track t = al.Tracks[j];
                    int c;
                    if (t != null && t.Path != null && songCounts.TryGetValue(t.Path, out c))
                    { TopEntry te = new TopEntry(); te.T = t; te.C = c; te.A = al; all.Add(te); }
                }
            }
            if (all.Count == 0) return;

            // 2) agrupar por etiqueta de genero (las que no tengan genero capturado caen en "Otros")
            Dictionary<string, List<TopEntry>> byGenre = new Dictionary<string, List<TopEntry>>();
            List<string> order = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                string gl;
                if (!songGenre.TryGetValue(all[i].T.Path, out gl) || string.IsNullOrEmpty(gl)) gl = "Otros";
                List<TopEntry> lst;
                if (!byGenre.TryGetValue(gl, out lst)) { lst = new List<TopEntry>(); byGenre[gl] = lst; order.Add(gl); }
                lst.Add(all[i]);
            }

            // 3) un disco virtual por genero
            List<Album> discs = new List<Album>();
            Dictionary<Album, long> totals = new Dictionary<Album, long>();
            for (int gi = 0; gi < order.Count; gi++)
            {
                string gl = order[gi];
                List<TopEntry> lst = byGenre[gl];
                lst.Sort(delegate (TopEntry x, TopEntry y)
                {
                    if (y.C != x.C) return y.C - x.C;
                    return string.Compare(x.T.Path, y.T.Path, StringComparison.OrdinalIgnoreCase);
                });
                int take = (lst.Count < TopMax) ? lst.Count : TopMax;
                Album d = new Album();
                d.IsTop = true; d.Letter = TopMark; d.Folder = "::TOP::" + gl;
                d.Artist = "TOP 100"; d.Name = gl;             // AlbumLabel -> "TOP 100 - Rock"
                d.Tracks = new List<Track>();
                d.TrackCovers = new List<string>();
                d.TrackInfo = new List<string>();
                d.TrackAlbums = new List<Album>();
                List<string> cycle = new List<string>();
                long total = 0;
                for (int i = 0; i < take; i++)
                {
                    TopEntry te = lst[i];
                    d.Tracks.Add(te.T);
                    d.TrackCovers.Add(te.A.CoverPath);
                    d.TrackInfo.Add(AlbumLabel(te.A));
                    d.TrackAlbums.Add(te.A);
                    if (te.A.CoverPath != null && !cycle.Contains(te.A.CoverPath)) cycle.Add(te.A.CoverPath);
                    total += te.C;
                }
                d.CycleCovers = cycle;
                d.CoverPath = (cycle.Count > 0) ? cycle[0] : null;
                totals[d] = total;
                discs.Add(d);
            }

            // 3a) discos TOP por ARTISTA muy puesto (hasta 10), RANKEADOS: "TOP #1 - <artista>" ... "TOP #10 - ..."
            {
                Dictionary<string, List<TopEntry>> byArtist = new Dictionary<string, List<TopEntry>>(StringComparer.OrdinalIgnoreCase);
                List<string> aOrder = new List<string>();
                for (int i = 0; i < all.Count; i++)
                {
                    string ar = all[i].T.Artist;
                    if (string.IsNullOrEmpty(ar)) continue;
                    List<TopEntry> lst;
                    if (!byArtist.TryGetValue(ar, out lst)) { lst = new List<TopEntry>(); byArtist[ar] = lst; aOrder.Add(ar); }
                    lst.Add(all[i]);
                }
                // candidatos: artistas con >=2 canciones distintas puestas (un solo tema no amerita disco)
                List<string> cand = new List<string>();
                Dictionary<string, long> aTot = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < aOrder.Count; i++)
                {
                    string ar = aOrder[i];
                    List<TopEntry> lst = byArtist[ar];
                    if (lst.Count < 2) continue;
                    long t = 0; for (int j = 0; j < lst.Count; j++) t += lst[j].C;
                    aTot[ar] = t; cand.Add(ar);
                }
                cand.Sort(delegate (string a, string b)
                {
                    long ta = aTot[a], tb = aTot[b];
                    if (tb != ta) return (tb > ta) ? 1 : -1;
                    return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                });
                int amax = (cand.Count < 10) ? cand.Count : 10;   // hasta 10 artistas mas puestos
                for (int ai = 0; ai < amax; ai++)
                {
                    string ar = cand[ai];
                    List<TopEntry> lst = byArtist[ar];
                    lst.Sort(delegate (TopEntry x, TopEntry y)
                    {
                        if (y.C != x.C) return y.C - x.C;
                        return string.Compare(x.T.Path, y.T.Path, StringComparison.OrdinalIgnoreCase);
                    });
                    int take = (lst.Count < TopMax) ? lst.Count : TopMax;
                    Album d = new Album();
                    // RANKING VISIBLE: el puesto (#1..#10, por total de reproducciones) va en la etiqueta. Tambien
                    // va en Folder (la CLAVE de la celda horneada): si un artista cambia de puesto, su etiqueta se
                    // re-hornea; con la clave vieja se habria quedado el numero anterior pegado en el cache.
                    d.IsTop = true; d.Letter = TopMark; d.Folder = "::TOPART::" + (ai + 1) + "|" + ar;
                    d.Artist = "TOP #" + (ai + 1); d.Name = ar;             // AlbumLabel -> "TOP #1 - Depeche Mode"
                    d.Tracks = new List<Track>();
                    d.TrackCovers = new List<string>();
                    d.TrackInfo = new List<string>();
                    d.TrackAlbums = new List<Album>();
                    List<string> cyc = new List<string>();
                    for (int i = 0; i < take; i++)
                    {
                        TopEntry te = lst[i];
                        d.Tracks.Add(te.T);
                        d.TrackCovers.Add(te.A.CoverPath);
                        d.TrackInfo.Add(AlbumLabel(te.A));
                        d.TrackAlbums.Add(te.A);
                        if (te.A.CoverPath != null && !cyc.Contains(te.A.CoverPath)) cyc.Add(te.A.CoverPath);
                    }
                    d.CycleCovers = cyc;
                    d.CoverPath = (cyc.Count > 0) ? cyc[0] : null;
                    totals[d] = aTot[ar];
                    discs.Add(d);
                }
            }

            // 3b) disco TOP de VIDEOS (de videoCounts, recorriendo los discos con video)
            if (videoCounts.Count > 0 && videoAlbums != null)
            {
                List<TopEntry> vids = new List<TopEntry>();
                for (int i = 0; i < videoAlbums.Count; i++)
                {
                    Album va = videoAlbums[i];
                    for (int j = 0; j < va.VideoFiles.Count; j++)
                    {
                        string vf = va.VideoFiles[j];
                        int c;
                        if (vf != null && videoCounts.TryGetValue(vf, out c))
                        { TopEntry te = new TopEntry(); te.V = vf; te.C = c; te.A = va; vids.Add(te); }
                    }
                }
                if (vids.Count > 0)
                {
                    vids.Sort(delegate (TopEntry x, TopEntry y)
                    {
                        if (y.C != x.C) return y.C - x.C;
                        return string.Compare(x.V, y.V, StringComparison.OrdinalIgnoreCase);
                    });
                    int vtake = (vids.Count < TopMax) ? vids.Count : TopMax;
                    Album vd = new Album();
                    vd.IsTop = true; vd.TopIsVideo = true; vd.Letter = TopMark; vd.Folder = "::TOPVID::";
                    vd.Artist = "TOP 100"; vd.Name = "Videos";
                    vd.VideoFiles = new List<string>();
                    vd.TrackCovers = new List<string>();
                    vd.TrackInfo = new List<string>();
                    vd.TrackAlbums = new List<Album>();
                    List<string> vcyc = new List<string>();
                    long vtotal = 0;
                    for (int i = 0; i < vtake; i++)
                    {
                        TopEntry te = vids[i];
                        vd.VideoFiles.Add(te.V);
                        vd.TrackCovers.Add(te.A.CoverPath);
                        vd.TrackInfo.Add(AlbumLabel(te.A));
                        vd.TrackAlbums.Add(te.A);
                        if (te.A.CoverPath != null && !vcyc.Contains(te.A.CoverPath)) vcyc.Add(te.A.CoverPath);
                        vtotal += te.C;
                    }
                    vd.CycleCovers = vcyc;
                    vd.CoverPath = (vcyc.Count > 0) ? vcyc[0] : null;
                    totals[vd] = vtotal;
                    discs.Add(vd);
                }
            }

            // 4) ordenar por CLASE (genero primero, luego videos, luego artistas) y dentro de
            //    cada clase por total de reproducciones. Asi quedan agrupados y comodos de ver.
            discs.Sort(delegate (Album x, Album y)
            {
                int cx = x.TopIsVideo ? 1 : ((x.Folder != null && x.Folder.StartsWith("::TOPART::", StringComparison.Ordinal)) ? 2 : 0);
                int cy = y.TopIsVideo ? 1 : ((y.Folder != null && y.Folder.StartsWith("::TOPART::", StringComparison.Ordinal)) ? 2 : 0);
                if (cx != cy) return cx - cy;
                long tx = totals[x], ty = totals[y];
                if (ty != tx) return (ty > tx) ? 1 : -1;
                return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            topDiscs = discs;
        }

        // Algo que el cliente pago (cancion o video) no se pudo reproducir: como encolarlo costo 1 credito, se le
        // devuelve. Sube 1 credito y guarda el estado. El aviso ("credito devuelto") lo pone cada llamador.
        private void RefundCredit()
        {
            credits++;
            UpdateCredits();
            SaveState();   // estado vivo: los creditos cambiaron
        }

        private void UpdateCredits()
        {
            if (lblCredits == null) return;
            if (credits <= 0) { lblCredits.Text = "Inserta una moneda"; lblCredits.ForeColor = Color.FromArgb(255, 205, 90); }
            else { lblCredits.Text = "Creditos:  " + credits; lblCredits.ForeColor = Color.FromArgb(120, 240, 140); }
            lblCredits.Invalidate();
        }

        // Reproduce el siguiente elemento (cancion o video). Tambien sirve para SALTAR el actual.
        private void PlayNext()
        {
            DisposePlaybackNow();      // detiene cualquier cancion (se desuscribe antes; no re-dispara)
            StopVideoPlayback();       // detiene cualquier video

            if (queue.Count == 0)
            {
                nowPlaying = null;
                progress.Value = 0; lblTime.Text = "0:00 / 0:00";
                ApplyGenreBackground(null);
                LayoutControls(); this.Invalidate();
                UpdateNowPlayingUI();
                SaveState();   // estado vivo: ya no suena nada (los creditos que queden se conservan)
                return;
            }

            nowPlaying = queue[0];
            queue.RemoveAt(0);
            SaveState();   // estado vivo: cambio lo que suena / la cola (para reanudar tras un corte de luz)

            if (nowPlaying.IsVideo)
            {
                bgAccentTarget = DefaultAccent;       // el video no tiene genero
                StartVideoItem(nowPlaying.VideoPath);
            }
            else
            {
                nowPlaying.Song.EnsureTags();         // lee ID3 ahora (genero -> color de fondo)
                if (nowPlaying.Song.Path != null) { songGenre[nowPlaying.Song.Path] = GenreLabel(nowPlaying.Song.Genre); statsDirty = true; topDirty = true; }   // captura genero para los discos TOP
                ApplyGenreBackground(nowPlaying.Song);
                PlayTrack(nowPlaying.Song);
            }
            LayoutControls(); this.Invalidate();
            UpdateNowPlayingUI();
        }

        // SALTAR: salta el elemento actual (cancion o video). Los CREDITOS son LO ULTIMO de la fila: solo se
        // borran cuando ya no queda NADA sonando NI en cola (asi SALTAR nunca borra dinero mientras haya musica
        // pendiente). Con todo vacio, UNA pulsacion de SALTAR borra los creditos (para cerrar el negocio).
        private void SkipSong()
        {
            if (nowPlaying != null || queue.Count > 0) { PlayNext(); return; }   // salta (o reanuda la cola si quedo detenida); PlayNext guarda el estado
            if (credits > 0)
            {
                credits = 0; UpdateCredits(); SaveState();
                ShowToast("Creditos borrados", Color.FromArgb(200, 130, 35));
            }
        }

        // ===================  VIDEO (Windows Media Player ActiveX, por codigo)  ===================

        // Pantalla completa del video tras inactividad (para apreciarlo mejor). Deja una franja
        // arriba para el aviso "toca una tecla" (asi no encimamos nada sobre el ActiveX del video).
        private void EnterVideoFull()
        {
            if (videoFull || !videoOn || video == null) return;
            videoFull = true;
            LayoutControls();
            video.BringToFront();
            if (lblAttract != null) { lblAttract.Visible = true; lblAttract.BringToFront(); }
        }
        private void ExitVideoFull()
        {
            if (!videoFull) return;
            videoFull = false;
            if (lblAttract != null) lblAttract.Visible = false;
            LayoutControls();
            this.Invalidate();
        }

        // Abre y reproduce un video en el reproductor de la derecha (lo llama PlayNext).
        // Volumen (0..100) con el que debe sonar un video: la perilla base, mas los dB de "Subir TODO" (para
        // que los videos sigan al resto), mas -si ese video ya fue medido- el nivelado que lo lleva al MISMO
        // volumen objetivo de las canciones. En XP los videos no se pueden medir (no hay como decodificar su
        // audio), asi que alli solo actuan la perilla y el "Subir TODO"; en la rama HD si se miden.
        private int VideoVolumeFor(string path)
        {
            double g = MasterLin();
            float rms;
            if (normalizeOn && path != null && loudCache.TryGetValue(path, out rms) && rms > 0.0001f)
            {
                double lv = normTarget / rms;
                if (lv < 0.15) lv = 0.15;        // no enterrar un video muy fuerte (max -16 dB)
                else if (lv > 4.0) lv = 4.0;     // ni intentar rescatar uno mudo (el tope real lo pone el 100 del WMP)
                g *= lv;
            }
            int v = (int)Math.Round(videoVol * g);
            if (v < 0) v = 0; else if (v > 100) v = 100;   // el WMP no pasa de 100: un video muy bajito solo llega hasta ahi
            return v;
        }

        // Pone el volumen en el WMP solo si CAMBIO (el sondeo corre varias veces por segundo durante el fundido).
        private void SetVideoVolume(int v)
        {
            if (v == videoVolSet) return;
            videoVolSet = v;
            try
            {
                if (video == null) return;
                object ocx = video.GetOcxObj(); if (ocx == null) return;
                object settings = Get(ocx, "settings"); if (settings != null) Set(settings, "volume", v);
            }
            catch { }
        }

        private void StartVideoItem(string path)
        {
#if ROCOLA_GPU
            AbortDiscTrans(); AbortWinTrans();   // el video toma la pantalla: nada volando encima
#endif
            try
            {
                if (video == null)
                {
                    video = new VideoHost();
                    video.TabStop = false;
                    Controls.Add(video);
                    IntPtr forceHandle = video.Handle;   // fuerza la creacion del control ActiveX
                }
                video.Visible = !searchOpen;   // si el buscador esta abierto, el cuadro queda oculto (el audio sigue)

                object ocx = video.GetOcxObj();
                if (ocx == null) throw new Exception("control WMP nulo");

                Set(ocx, "uiMode", "none");
                Set(ocx, "stretchToFit", true);
                object settings = Get(ocx, "settings");
                videoVolSet = -1;   // fuerza el primer envio del volumen a este WMP recien apuntado
                int vol0 = VideoVolumeFor(path);
                if (settings != null) { Set(settings, "autoStart", true); Set(settings, "volume", vol0); }
                videoVolSet = vol0;
                Set(ocx, "URL", path);   // con autoStart=true deberia arrancar solo
                try { object c0 = Get(ocx, "controls"); if (c0 != null) Call(c0, "play"); } catch { }   // empujon explicito: autoStart no siempre engancha al instanciar el ActiveX

                videoOn = true;
                progress.Value = 0; lblTime.Text = "0:00 / 0:00";   // limpia el tiempo de la cancion anterior; VideoPoll lo llena con pos/dur del video
                videoLastPos = -1; videoProgressTick = Environment.TickCount; videoNudged = false; videoMaxPos = 0;   // arrancar el vigia de avance
                videoPoll.Interval = 400;   // sondeo normal (baja a 80 ms durante el fundido del final)
                video.BringToFront();
                videoPoll.Start();
                Diag("video: inicia");
            }
            catch (Exception ex)
            {
                videoOn = false;
                try { if (video != null) video.Visible = false; } catch { }
                RefundCredit();   // compensacion al cliente: el video no abrio
                ShowToast("No se pudo abrir el video: credito devuelto", Color.FromArgb(200, 80, 70));
                lblStatus.Text = "Video (WMP): " + ex.Message;
                Diag("video: NO ABRIO - " + ex.Message);
                // el video no arranco: pasar al siguiente sin trabar la cola (tras desenredar la pila de PlayNext)
                try { if (this.IsHandleCreated) this.BeginInvoke((MethodInvoker)delegate { PlayNext(); }); } catch { }
            }
        }

        // Detiene/oculta el video SIN tocar la cola (lo usan PlayNext y el cierre del buscador).
        private void StopVideoPlayback()
        {
            videoPoll.Stop();
            try { if (video != null) video.Visible = false; } catch { }   // ocultar PRIMERO (aunque el OCX truene al detener)
            try
            {
                if (video != null)
                {
                    object ocx = video.GetOcxObj();
                    if (ocx != null)
                    {
                        object controls = Get(ocx, "controls");
                        if (controls != null) Call(controls, "stop");
                        Set(ocx, "URL", "");
                    }
                }
            }
            catch { }
            videoOn = false;
            Diag("video: detenido");
            if (videoFull) ExitVideoFull();   // si estaba en pantalla completa, vuelve a la vista normal
        }

        private void VideoPoll_Tick(object sender, EventArgs e)
        {
            if (!videoOn || video == null) { videoPoll.Stop(); return; }
            int now = Environment.TickCount;
            int st = -1; double pos = -1, dur = -1;
            try
            {
                object ocx = video.GetOcxObj();
                if (ocx != null)
                {
                    object ps = Get(ocx, "playState");
                    if (ps != null) st = Convert.ToInt32(ps);
                    object controls = Get(ocx, "controls");
                    if (controls != null) { object cp = Get(controls, "currentPosition"); if (cp != null) pos = Convert.ToDouble(cp); }
                    object media = Get(ocx, "currentMedia");
                    if (media != null) { object d = Get(media, "duration"); if (d != null) dur = Convert.ToDouble(d); }
                }
            }
            catch { }

            if (pos > videoMaxPos) videoMaxPos = pos;

            // El tiempo/barra del VIDEO salen de aqui (ProgressTimer solo sirve a las canciones via 'reader',
            // que con video esta nulo). Sin esto el tiempo se quedaba congelado en lo ultimo de la cancion.
            if (pos >= 0 && dur > 0.5)
            {
                double frac = pos / dur; if (frac < 0) frac = 0; if (frac > 1) frac = 1;
                progress.Value = frac;
                lblTime.Text = Fmt(TimeSpan.FromSeconds(pos)) + " / " + Fmt(TimeSpan.FromSeconds(dur));
            }

            // FUNDIDO DE SALIDA del video: igual que las canciones, el final baja suave en vez de cortar en
            // seco. El video se da por terminado en dur-0.6 (ver abajo), asi que la rampa se mide contra ESE
            // final para que llegue a cero justo a tiempo. Durante el fundido se sondea mas seguido (80 ms)
            // para que la bajada se oiga continua; al terminar vuelve al sondeo normal.
            int volBase = VideoVolumeFor(nowPlaying != null ? nowPlaying.VideoPath : null);
            if (crossfadeSec > 0.01f && dur > 0.5 && pos >= 0)
            {
                double restan = (dur - 0.6) - pos;
                if (restan < crossfadeSec)
                {
                    double f = restan / crossfadeSec; if (f < 0) f = 0; else if (f > 1) f = 1;
                    videoPoll.Interval = 80;
                    SetVideoVolume((int)Math.Round(volBase * f));
                }
                else { videoPoll.Interval = 400; SetVideoVolume(volBase); }
            }
            else SetVideoVolume(volBase);

            // FIN NORMAL del video (sin reembolso, se reprodujo bien): MediaEnded (8); o la posicion
            // alcanzo la duracion (algunos archivos se congelan en el ultimo cuadro y NO reportan
            // MediaEnded -> esto era lo que dejaba la pantalla negra pegada); o Stopped tras reproducirse.
            bool reachedEnd = (dur > 0.5 && pos >= dur - 0.6);
            bool stoppedAfterPlaying = (st == 1 && videoMaxPos >= 3.0);
            if (st == 8 || reachedEnd || stoppedAfterPlaying) { PlayNext(); return; }

            // Stopped (1) SIN haberse reproducido = fallo real de arranque -> reembolso + saltar.
            if (st == 1 && videoMaxPos < 3.0)
            {
                try { LogError("video detenido sin reproducir: " + (nowPlaying != null ? nowPlaying.VideoPath : "?"), null); } catch { }
                RefundCredit();
                ShowToast("Video no disponible: credito devuelto", Color.FromArgb(200, 130, 35));
                PlayNext();
                return;
            }

            // VIGIA DE VIDEO COLGADO: si la posicion AVANZA, todo bien (se reinicia el cronometro). Si NO
            // avanza 12 segundos seguidos (y sin haber llegado al final), se da por fallido y se SALTA solo.
            if (pos > videoLastPos + 0.05) { videoLastPos = pos; videoProgressTick = now; }
            else if (now - videoProgressTick > 6000 && !videoNudged)
            {
                // 6s sin avanzar: un EMPUJON de rescate (play explicito) por si fue una traba transitoria
                // (no el codec). Si el archivo es reproducible pero se atoro, esto suele revivirlo.
                videoNudged = true;
                try { object oc = video.GetOcxObj(); if (oc != null) { object c = Get(oc, "controls"); if (c != null) Call(c, "play"); } } catch { }
            }
            else if (now - videoProgressTick > 12000)
            {
                // 12s sin avanzar pese al empujon: el sistema no puede con este archivo (codec ausente o
                // archivo ilegible). Se salta solo y se anota la ruta para identificarlo.
                try { LogError("video estancado (playState=" + st + "): " + (nowPlaying != null ? nowPlaying.VideoPath : "?"), null); }
                catch { }
                RefundCredit();   // compensacion al cliente: el video no se pudo reproducir
                ShowToast("Video no disponible: credito devuelto", Color.FromArgb(200, 130, 35));
                PlayNext();
            }
        }

        // Late-binding por reflexion: le hablamos al control WMP sin referencias COM ni 'dynamic'
        // (asi funciona igual en .NET 6 y en .NET 3.5/4.0 de XP).
        private static object Get(object o, string prop)
        {
            return o.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, o, null);
        }
        private static void Set(object o, string prop, object val)
        {
            o.GetType().InvokeMember(prop, BindingFlags.SetProperty, null, o, new object[] { val });
        }
        private static object Call(object o, string method, params object[] args)
        {
            return o.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, o, args);
        }

        private void ApplyGenreBackground(Track t)
        {
            Color acc = ColorForTrack(t);   // excepcion por artista (BTS=morado) y luego genero
            bgAccentTarget = acc;
            Color panel = Shade(acc, 0.70);
            Color list = Shade(acc, 0.76);
            panelSongs.BackColor = panel;
            if (songPager != null) { songPager.BackColor = panel; songPager.Invalidate(); }
            cubeBox.BackColor = Color.Transparent;
            songList.BackColor = list; songList.Accent = acc;
            songList.Invalidate();
        }

        private void PlayTrack(Track t)
        {
            try
            {
                reader = OpenAudio(t.Path);   // normal; si NAudio rechaza el MP3 (frecuencia mezclada, etc.) reintenta con el lector TOLERANTE
                reader.Volume = FixedVolume;   // se queda en 1.0 (crudo): el nivelado se aplica en LevelTap, tras medir el volumen REAL
                // NIVELADO: si esta cancion ya se oyo antes, se aplica la ganancia que la lleva al nivel comun; si es
                // nueva, va a su volumen original y se APRENDE su sonoridad en esta pasada (para nivelarla la proxima vez).
                curMeasurePath = (normalizeOn && !durCache.ContainsKey(t.Path)) ? t.Path : null;   // ya pre-analizada = medida COMPLETA y fiable: no se vuelve a medir (y se ahorra el K-weighting)
                curSumSq = 0; curSamples = 0;
                eqMeasPath = (EqMideHuella && !durCache.ContainsKey(t.Path)) ? t.Path : null;   // huella tonal: solo si no la trae ya del pre-analisis (que la mide completa)
                eqMeasCut = 0f;   // el acantilado es de ESTA cancion: se vuelve a medir
                eqMeasSum = new double[8]; eqMeasN = 0; eqMeasRate = 0;
                kwRate = 0;   // reinicia el estado del K-weighting: cada cancion mide desde cero (sin cola del filtro anterior)
                // NIVEL 2 (reconstructor espectral): solo se INSERTA en maquinas que cumplen requisitos; con
                // Active=false es un pase directo (costo ~cero). Ctrl+F3 lo alterna EN VIVO para comparar A/B.
                NAudio.Wave.ISampleProvider chain = reader;
                // DES-RECORTE: repara picos aplastados de origen (antes de medir y de DSEE). Ya sabemos, cancion
                // por cancion, si de verdad viene recortada; en las que NO -que son la mayoria- ni se pone en la
                // cadena: se ahorra su trabajo y su retardo de ~1.5 ms. Si aun no esta medida, se pone por si acaso.
                if (declipOn && DeclipVale(t.Path)) chain = new DeclipProvider(chain);
                if (sbrAllowed) { curSbr = new SpectralTap(chain); curSbr.Active = sbrOn; chain = curSbr; }
                else curSbr = null;
                LevelTap tap = new LevelTap(chain, this);
                tap.Gain = NormGainFor(t.Path);
                tap.Excite = ExciteAmountFor(t.Path);   // realce de agudos: mas a las dañadas, menos a las que ya traen su aire
                tap.Master = MasterLin();  // SUBIR TODO: los dB extra de Ctrl+F3 (con rodilla suave anti-distorsion)
                tap.EqDb = EqCorrFor(t.Path);      // EQ automatico: correccion aprendida (null la 1a vez que suena)
                curTap = tap;   // referencia para ajustar la ganancia en vivo (Ctrl+F3)
                // WaveOut con callback por VENTANA, no WaveOutEvent: el de eventos llama a
                // WaitHandle.WaitOne(Int32), que NO existe en .NET 3.5 sin Service Pack 1
                // (el de la rockola) y truena con MissingMethodException. Este no lo usa.
                WaveOut wo = new WaveOut();
                // El audio se rellena desde el bucle de mensajes de la interfaz; con 1 SEGUNDO de colchon,
                // la musica aguanta CUALQUIER atoron (paginas, caratulas, GC, video) sin cortarse jamas.
                // Rellenar 200ms de MP3 cuesta ~2-5ms de CPU: el colchon grande sale practicamente gratis.
                wo.DesiredLatency = 1000;
                wo.NumberOfBuffers = 5;     // 5 bloques de 200ms
                output = wo;
                output.PlaybackStopped += Output_PlaybackStopped;
                // BAJO PSICOACUSTICO: despues de nivelar/ecualizar y ANTES del limitador, que es quien controla
                // los picos que anaden los armonicos.
                bassSongFactor = BassFactorFor(t.Path);   // cuanto le toca a ESTA cancion (1 = la perilla tal cual)
                bassAutoBase = BajoAuto(t.Path);          // y de cuanto se parte, si el bajo va en automatico
                curBass = new DeepBassProvider(tap, BassAmountNow());
                NAudio.Wave.ISampleProvider outChain = new LookaheadLimiter(curBass, 0.98f, 3.0);
                if (crossfadeSec > 0.01f)
                {
                    long tf = 0; try { tf = (long)(reader.TotalTime.TotalSeconds * reader.WaveFormat.SampleRate); } catch { }
                    outChain = new FadeProvider(outChain, tf, crossfadeSec);
                }
                output.Init(new DitherWaveProvider16(outChain));   // 16 bits CON dither TPDF (menos aspereza en pasajes silenciosos)
                output.Play();
                progressTimer.Start();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "No se pudo reproducir " + Path.GetFileName(t.Path) + " (" + ex.Message + ")";
                LogError("PlayTrack " + Path.GetFileName(t.Path), ex);
                NoteIncompatible(t.Path, ex);   // ni con el lector tolerante: deja la ruta en rocola_incompatibles.txt para repararla
                RefundCredit();   // la cancion que el cliente pago no sono: se le devuelve el credito
                ShowToast("No se pudo reproducir: credito devuelto", Color.FromArgb(200, 80, 70));
                DisposePlaybackNow();
                // OPTIMIZACION (estabilidad): antes era PlayNext() directo. Si varias canciones seguidas
                // no abren (archivos movidos/corruptos) se apilaba PlayTrack->PlayNext->PlayTrack... hasta
                // un posible StackOverflow. Via BeginInvoke se desenreda la pila (igual que en el video).
                try { if (this.IsHandleCreated) this.BeginInvoke((MethodInvoker)delegate { PlayNext(); }); } catch { }
            }
        }

        // Abre el audio. Camino NORMAL: AudioFileReader (rapido, para el 99.99% de los archivos). Si NAudio lo
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
                File.AppendAllText(Path.Combine(Application.StartupPath, "rocola_incompatibles.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  |  " + path + "  |  " + reason + "\r\n");
            }
            catch { }
        }

        // Carga las rutas ya anotadas para no repetirlas en rocola_incompatibles.txt entre reinicios.
        private void LoadIncompatible()
        {
            try
            {
                string f = Path.Combine(Application.StartupPath, "rocola_incompatibles.txt");
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f))
                {
                    int a = line.IndexOf("  |  ");
                    if (a < 0) continue;
                    int b = line.IndexOf("  |  ", a + 5);
                    string p = (b > a) ? line.Substring(a + 5, b - (a + 5)) : line.Substring(a + 5);
                    p = p.Trim();
                    if (p.Length > 0) incompatibleLogged.Add(Pool(p));
                }
            }
            catch { }
        }

        // Guarda el error completo (tipo + mensaje + pila) en rocola_error.txt junto al .exe,
        // para poder ver el metodo exacto que falta (p. ej. por una version equivocada de NAudio).
        private void LogError(string context, Exception ex)
        {
            try
            {
                string f = Path.Combine(Application.StartupPath, "rocola_error.txt");
                string msg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + context + "]\r\n"
                           + (ex != null ? ex.ToString() : "(sin excepcion)") + "\r\n\r\n";
                try { FileInfo fi = new FileInfo(f); if (fi.Exists && fi.Length > 262144) fi.Delete(); } catch { }   // tope ~256KB
                File.AppendAllText(f, msg);
            }
            catch { }
        }

        private void Output_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
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
            progressTimer.Stop();
            if (output != null)
            {
                output.PlaybackStopped -= Output_PlaybackStopped;
                try { output.Stop(); } catch { }
                try { output.Dispose(); } catch { }
                output = null;
            }
            if (reader != null) { try { reader.Dispose(); } catch { } reader = null; }
            curTap = null; curSbr = null; curBass = null;   // ya no hay taps actuales
            FinalizeLoudMeasure();   // guarda la sonoridad medida de la cancion que acaba de sonar (audio ya detenido: sin carrera)
            FinalizeEqMeasure();     // y su huella tonal (mismo momento seguro)
            lock (levelLock) levelQ.Clear();   // sin cancion no hay trozos pendientes: el pulso cae a cero
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
            float[] tEsp = tmpEsp ?? (tmpEsp = new float[4 * SpecBands]);
            // ESCALERA del espectro: los coeficientes se calculan al ritmo de CUADROS (rate), porque la escalera
            // trabaja sobre la mezcla mono de cada cuadro y no muestra por muestra del stream entrelazado. Asi los
            // cortes caen en los mismos Hz de siempre pero con la MITAD de trabajo, y ademas la diferencia entre
            // canales deja de colarse como falso "aire". Se recalculan solo si cambia el formato.
            bool espectro = EspectroActivo;
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
            double[] sSum = tmpSum ?? (tmpSum = new double[SpecBands]);
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
                for (int b = 0; b < SpecBands; b++) sSum[b] = 0;
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
                        float x = m;
                        for (int pp = 0; pp < SpecPolos; pp++) { sl[q + pp] += (x - sl[q + pp]) * sc[k]; x = sl[q + pp]; }
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
                for (int b = 0; b < SpecBands; b++) tEsp[c * SpecBands + b] = (float)Math.Sqrt(sSum[b] / ne);
            }
            fLp1 = lp1; fLp2 = lp2;   // persiste el estado de los filtros para el proximo bloque
            specLp = sl;
            // NIVELADO PERCEPTUAL (K-weighting, tipo LUFS/BS.1770): en vez del RMS crudo, mide la sonoridad
            // PESADA por como la oye el humano (realza agudos ~+4 dB, quita sub-graves) via 2 biquads/canal.
            // Asi una cancion con mucho bajo (que el RMS crudo sobre-estima) y una brillante quedan de verdad
            // al mismo volumen PERCIBIDO. Solo durante el aprendizaje de esta cancion.
            if (curMeasurePath != null)
            {
                if (kwRate != rate || kwCoef1 == null)
                {
                    kwRate = rate;
                    kwCoef1 = EqShelfHi(1681.974f, rate, 3.99984f, 0.70717f);   // "head" shelf de BS.1770
                    kwCoef2 = EqHighPass(38.1355f, rate, 0.50033f);             // RLB high-pass
                    kwSt = new float[Math.Max(1, ch) * 8];
                }
                int ch3 = ch < 1 ? 1 : ch;
                float[] st = kwSt, c1 = kwCoef1, c2 = kwCoef2;
                double sumK = 0; long nK = 0;
                for (int i = offset; i + ch3 <= offset + count; i += ch3)
                {
                    for (int c = 0; c < ch3; c++)
                    {
                        int b = c * 8; float x = buffer[i + c];
                        float y1 = c1[0]*x + c1[1]*st[b] + c1[2]*st[b+1] - c1[3]*st[b+2] - c1[4]*st[b+3];
                        st[b+1]=st[b]; st[b]=x; st[b+3]=st[b+2]; st[b+2]=y1;
                        float y2 = c2[0]*y1 + c2[1]*st[b+4] + c2[2]*st[b+5] - c2[3]*st[b+6] - c2[4]*st[b+7];
                        st[b+5]=st[b+4]; st[b+4]=y1; st[b+7]=st[b+6]; st[b+6]=y2;
                        sumK += y2 * y2;
                    }
                    nK++;
                }
                curSumSq += sumK; curSamples += nK * ch3;   // energia K-pesada (misma escala que el RMS de antes)
            }
            // EQ AUTOMATICO: huella tonal de la cancion (energia por banda via bandpass RBJ, mezcla MONO).
            // Solo durante el aprendizaje de esta cancion; ~70 operaciones por cuadro, costo despreciable.
            if (eqMeasPath != null)
            {
                if (eqMeasRate != rate || eqMeasCoef == null)
                {
                    eqMeasRate = rate; eqMeasCoef = new float[8][]; eqMeasSt = new float[8 * 4];
                    for (int b = 0; b < 8; b++) eqMeasCoef[b] = EqBpf(EqCentros[b], rate);
                }
                int ch2 = ch < 1 ? 1 : ch;
                float[] st = eqMeasSt; double[] sums = eqMeasSum;
                for (int i = offset; i + ch2 <= offset + count; i += ch2)
                {
                    float m = buffer[i];
                    for (int c2 = 1; c2 < ch2; c2++) m += buffer[i + c2];
                    m /= ch2;
                    for (int b = 0; b < 8; b++)
                    {
                        float[] cf = eqMeasCoef[b]; if (cf == null) continue;
                        int q = b * 4;
                        float o = cf[0] * m + cf[1] * st[q] + cf[2] * st[q + 1] - cf[3] * st[q + 2] - cf[4] * st[q + 3];
                        st[q + 1] = st[q]; st[q] = m;
                        st[q + 3] = st[q + 2]; st[q + 2] = o;
                        sums[b] += o * o;
                    }
                    eqMeasN++;
                }
            }
            lock (levelLock)
            {
                for (int c = 0; c < chunks; c++)
                {
                    int atMs = now + (int)((long)(c * len) * 1000 / Math.Max(1, samplesPerSec));   // desfase del trozo dentro del bloque
                    float[] esp = null;
                    if (espectro)
                    {
                        esp = new float[SpecBands];   // 48 bytes por trozo (~20/seg): nada para el recolector
                        for (int b = 0; b < SpecBands; b++) esp[b] = tEsp[c * SpecBands + b];
                    }
                    levelQ.Enqueue(new Lvl(atMs, tLo[c], tMid[c], tHi[c], esp));
                }
                while (levelQ.Count > 96) levelQ.Dequeue();   // acotado (~5s de historial maximo)
            }
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
            nowFullText = (nowPlaying != null) ? "Reproduciendo:  " + nowPlaying.Display() : "Nada reproduciendose";
            PositionMejorada();   // parte el texto en 1 o 2 lineas y coloca el letrero
            lblNext.Text = (queue.Count > 0) ? "Siguiente:  " + queue[0].Display() : "Siguiente:  -";
            lblCount.Text = "En cola:  " + queue.Count;
            if (miniCube != null)
            {
                if (nowPlaying != null) { miniCube.Cover = LoadCover(nowPlaying.Disc); miniCube.Visible = true; }
                else { miniCube.Cover = null; miniCube.Visible = false; }
                miniCube.Invalidate();
            }
        }

        // El letrero corresponde a una MEJORA REAL: switch prendido Y el reconstructor de la cancion
        // actual reconstruyendo de verdad (su puerta interna deja intactos los archivos ya completos).
        private bool MejActivo()
        {
            SpectralTap sp = curSbr;
            return sbrOn && nowPlaying != null && !nowPlaying.IsVideo && sp != null && sp.Mejorando;
        }

        // Parte "Reproduciendo: <titulo>" en HASTA DOS lineas y coloca el letrero "MEJORADA" justo despues de
        // la ULTIMA. Antes era una sola linea que se encogia para dejarle sitio al letrero, asi que un titulo
        // largo se cortaba con puntos suspensivos bastante antes de tiempo. Ahora la primera parte se va a la
        // linea de arriba (que esta reservada siempre) y solo el resto comparte fila con el letrero.
        // Dorado: en esta rama el DSEE espectral es el unico que mejora, asi que el letrero es uno solo.
        private void PositionMejorada()
        {
            if (lblMejorada == null || lblNow == null || lblNowTop == null) return;
            bool on = MejActivo();
            int bw = on ? TextRenderer.MeasureText(lblMejorada.Text, lblMejorada.Font).Width + 4 : 0;
            int anchoUlt = on ? Math.Max(60, mejRowW - bw - 12) : mejRowW;   // lo que le queda a la ultima linea
            if (lblNow.Width != anchoUlt) lblNow.Width = anchoUlt;
            if (lblNowTop.Width != mejRowW) lblNowTop.Width = mejRowW;       // la de arriba no comparte con nadie

            string arriba, abajo;
            PartirNow(nowFullText, lblNow.Font, mejRowW, anchoUlt, out arriba, out abajo);
            if (lblNowTop.Text != arriba) lblNowTop.Text = arriba;
            if (lblNow.Text != abajo) lblNow.Text = abajo;

            if (!on) { if (lblMejorada.Visible) lblMejorada.Visible = false; return; }
            int tw = TextRenderer.MeasureText(lblNow.Text, lblNow.Font).Width;
            if (tw > anchoUlt) tw = anchoUlt;
            lblMejorada.SetBounds(lblNow.Left + tw + 10, lblNow.Top + 2, bw, 20);
            lblMejorada.Visible = true;
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

        // Anima el brillo del letrero MEJORADA. Sin panel GPU encima (gpuCovered=false) las letras RESPIRAN
        // de color (repintar una etiqueta chica es barato); con el panel GPU el texto va FIJO en la textura de
        // la franja y el brillo es un destello que RECORRE las letras (FillUiGlows): cero rehechas de textura.
        private void TickMejorada(bool gpuCovered)
        {
            if (lblMejorada == null) return;
            if (MejActivo() != lblMejorada.Visible) PositionMejorada();   // en cuanto el reconstructor ACTUA (~0.1s) aparece, y queda TODA la cancion (solo se va con el switch o al cambiar de cancion)
            if (!lblMejorada.Visible) return;
            if (!gpuCovered)
            {
                double pm = 0.5 + 0.5 * Math.Sin(phase * 2.8);
                lblMejorada.ForeColor = LerpColor(MejGoldBase, MejGoldHi, pm);
            }
            else if (lblMejorada.ForeColor.ToArgb() != MejGoldBase.ToArgb()) lblMejorada.ForeColor = MejGoldBase;   // al volver de CPU: color base fijo en la textura
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (reader == null) return;
            TimeSpan cur = reader.CurrentTime, tot = reader.TotalTime;
            if (tot.TotalSeconds > 0) progress.Value = cur.TotalSeconds / tot.TotalSeconds;
            lblTime.Text = Fmt(cur) + " / " + Fmt(tot);
            // (la posicion ya NO se guarda: al reanudar tras un corte de luz la cancion arranca desde el
            // principio, asi que el estado solo se escribe en eventos -> cero escrituras periodicas a disco)
        }

#if ROCOLA_GPU
        private static uint ToD3D(Color c) { return 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | (uint)c.B; }

        private bool gpuEnabled;                          // render por GPU activo. Por defecto SI (se guarda al alternar con Ctrl+F12)
        private bool gpuShowFps;                          // mostrar el contador de FPS (diagnostico). Oculto por defecto; Ctrl+F1 lo alterna
        private bool gpuDiscTry = true;                   // interior de disco por GPU (por defecto). Ctrl+F11 lo alterna a CPU como valvula de escape
        private int gpuMode = -1;                          // modo GPU actual (nav/buscador/disco/atraccion); al cambiar se rehace la UI atomica
        private int layoutStamp = -1;                      // huella del ultimo reacomodo: solo un cambio REAL rehace la textura de UI
        private Bitmap uiOverlay; private int uiOverlayVer; private long uiOverlaySig = SigNada;   // textura de las barras (UI)

        // Ctrl+F12: alterna el render entre GPU (Direct3D 9, por defecto) y CPU, y GUARDA la preferencia.
        private void ToggleAlbumGpu() { ApplyAlbumGpu(!gpuEnabled, true, true); }

        // Aplica el modo de render. 'on' = destino (GPU/CPU); 'announceFail' = avisar si el dispositivo no inicia;
        // 'persist' = guardar la preferencia (solo cuando lo pide el usuario con Ctrl+F12, no en el arranque). El
        // panel GPU (Direct3D 9) cubre toda la ventana y compone: fondo estrellado + cubos + barras + mini-cubo.
        private void ApplyAlbumGpu(bool on, bool announceFail, bool persist)
        {
            if (lblGpu == null)
            {
                lblGpu = new Label();
                lblGpu.AutoSize = false; lblGpu.Size = new Size(380, 26);
                lblGpu.Location = new Point(ClientSize.Width - 390, 46);
                lblGpu.BackColor = Color.FromArgb(200, 0, 0, 0); lblGpu.ForeColor = Color.Yellow;
                lblGpu.Font = new Font(FontFamily.GenericMonospace, 10f, FontStyle.Bold);
                lblGpu.TextAlign = ContentAlignment.MiddleCenter; lblGpu.Visible = false;
                Controls.Add(lblGpu);
            }
            if (gpuPanel == null)
            {
                gpuPanel = new GpuPanel();
                gpuPanel.ShowFps = gpuShowFps;
                gpuPanel.GetPulse = delegate { return audioLevel; };
                gpuPanel.GetAurora = delegate { return BuildAuroraState(); };   // aurora del horizonte (espectro)
                gpuPanel.GetToast = delegate { return BuildToastState(); };     // aviso flotante (moneda, cancion agregada...)
                gpuPanel.GetTrans = delegate { return BuildTransState(); };   // zoom al entrar/salir de un disco
                gpuPanel.GetWin = delegate { return BuildWinState(); };        // ventana del buscador abriendo/cerrando
                gpuPanel.GetGrid = albumView.BuildGpuGrid;   // rejilla de discos: cada celda su propio quad con textura cacheada (hojear = elegir texturas + posiciones)
                gpuPanel.GetContour = delegate { return albumView.ContourSpriteBmp; };          // resalte de seleccion
                gpuPanel.GetContourRect = delegate {
                    Rectangle r = albumView.ContourScreenRect();
                    if (r.Width > 0) r.Offset(albumView.Left, albumView.Top);   // coords de albumView -> pantalla
                    return r;
                };
                gpuPanel.GetCubeState = delegate { return BuildGpuCubeState(); };   // album (offset a pantalla) + mini-cubo
                gpuPanel.GetUi = delegate { return uiOverlay; };                    // textura PRINCIPAL de la UI (altura segun el modo)
                gpuPanel.GetUiVersion = delegate { return EnsureUiOverlay(); };
                gpuPanel.GetUiBot = delegate { return uiBotOverlay; };              // franja de ESTADO de abajo (textura chica aparte: el reloj no rehace la principal)
                gpuPanel.GetUiBotVersion = delegate { return EnsureUiBotOverlay(); };
                gpuPanel.GetUiGlows = FillUiGlows;                                   // halos animados (destello de boton, pulso de letra, TOP, atraccion): quads GPU, sin re-subir textura
                Controls.Add(gpuPanel);
            }
            if (on && !gpuEnabled)
            {
                albumView.InvalidateCubeCache();         // cubeCache fresco para subirlo a la GPU
                uiOverlaySig = SigNada;                     // fuerza reconstruir la textura de UI
                gpuPanel.AlbumRect = albumView.Bounds;
                gpuPanel.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);   // PANTALLA COMPLETA
                gpuPanel.SkyTop = ToD3D(SkyColor(bgAccent)); gpuPanel.SkyMid = ToD3D(SkyColorMid(bgAccent)); gpuPanel.SkyBot = ToD3D(SkyColor2(bgAccent));
                gpuPanel.Visible = true; gpuPanel.BringToFront();
                if (gpuPanel.StartGpu())
                {
                    gpuEnabled = true; lblGpu.Visible = false; KeepVideoOnTop();
                    gpuPanel.PrecalientaAnimaciones(ThumbPx, ToastW, ToastH);   // texturas de las animaciones creadas YA, no en la primera que se vea
                }
                else { gpuPanel.Visible = false; Diag("render: FALLO el dispositivo D3D9 (sigue CPU)"); if (announceFail) { lblGpu.Text = "GPU: fallo el dispositivo (sigue CPU)"; lblGpu.Visible = true; lblGpu.BringToFront(); } }
            }
            else if (!on && gpuEnabled)
            {
                gpuEnabled = false;
                gpuPanel.StopGpu(); gpuPanel.Visible = false;
                if (albumView != null) albumView.GpuCovered = false;   // vuelve a pintar la rejilla por CPU (ya no hay panel encima)
                if (lblGpu != null) lblGpu.Visible = false;
                Invalidate(true);   // repinta la UI normal (CPU) que estaba tapada por el panel
            }
            if (persist) { gpuPrefOn = gpuEnabled; SaveConfigPrefs(); }   // recuerda la eleccion para el proximo arranque
        }


        // Estado de los cubos que giran (en coords de PANTALLA). En navegacion: album (seleccionado + relevo).
        // En el buscador: el cubo de vista previa. El mini-cubo de la barra inferior es comun a ambos.
        private GpuCubeState BuildGpuCubeState()
        {
            GpuCubeState st;
            if (attractOn && attractView != null)   // MODO ATRACCION: cubo gigante centrado que cicla portadas (con fundido)
            {
                st = new GpuCubeState();
                Image ca, cb; float f;
                attractView.GetCoverPair(out ca, out cb, out f);   // puede dar null (portadas aun cargando): el cubo muestra relleno azulado
                RectangleF cubo = AttractCubeRect();   // mismo rectangulo que usa la animacion de entrada/salida
                st.Sel.Has = true;
                st.Sel.X = cubo.X; st.Sel.Y = cubo.Y; st.Sel.W = cubo.Width; st.Sel.H = cubo.Height;
                st.Sel.Angle = attractView.Angle; st.Sel.CoverA = ca as Bitmap; st.Sel.CoverB = cb as Bitmap; st.Sel.Fade = f;
                return st;   // en atraccion no hay mini-cubo (no suena nada)
            }
            if (searchOpen && searchOverlay != null)
            {
                st = new GpuCubeState();
                Rectangle area; Image ca, cb; float f;
                bool cubOk = searchOverlay.GetPreviewCube(out area, out ca, out cb, out f);
#if ROCOLA_GPU
                if (winMode == 1) cubOk = false;   // abriendo: la ventana se anima entera, un cubo suelto encima la delataria
#endif
                if (cubOk)
                {
                    area.Offset(searchOverlay.Left, searchOverlay.Top);   // coords del buscador -> pantalla
                    st.Sel.Has = true; st.Sel.X = area.X; st.Sel.Y = area.Y; st.Sel.W = area.Width; st.Sel.H = area.Height;
                    st.Sel.Angle = searchOverlay.PreviewAngle; st.Sel.CoverA = ca as Bitmap; st.Sel.CoverB = cb as Bitmap; st.Sel.Fade = f;
                }
            }
            else if (panelSongs != null && panelSongs.Visible && cubeBox != null)
            {
                st = new GpuCubeState();
                if (cubeBox.Cover != null)   // el cubo GRANDE del interior (gira); coords de panelSongs -> pantalla
                {
                    st.Sel.Has = true;
                    FillCubeBoxGeom(ref st.Sel, cubeBox, panelSongs.Left + cubeBox.Left, panelSongs.Top + cubeBox.Top);
                }
            }
            else
            {
                st = albumView.BuildCubeState();
                int ox = albumView.Left, oy = albumView.Top;
                if (st.Sel.Has) { st.Sel.X += ox; st.Sel.Y += oy; }
                if (st.Relay.Has) { st.Relay.X += ox; st.Relay.Y += oy; }
            }
            if (miniCube != null && miniCube.Visible && miniCube.Cover != null)   // mini-cubo (barra inferior): comun
            {
                st.Mini.Has = true;
                FillCubeBoxGeom(ref st.Mini, miniCube, miniCube.Left, miniCube.Top);
            }
            return st;
        }

        // Encuadre + bamboleo de un CubeBox (grande o mini) para que el cubo por GPU COINCIDA con el de CPU:
        // CubeBox.OnPaint dibuja el cubo al 78% del control (centrado) y lo inclina +/-10 grados segun Phase.
        // Sin esto el cubo por GPU salia mas grande (100% del control) y sin el bamboleo (giro plano).
        private static void FillCubeBoxGeom(ref GpuCubeInfo info, CubeBox cb, int screenX, int screenY)
        {
            int side = (int)(Math.Min(cb.Width, cb.Height) * 0.78);
            if (side < 1) side = 1;
            info.X = screenX + (cb.Width - side) / 2;
            info.Y = screenY + (cb.Height - side) / 2;
            info.W = side; info.H = side;
            info.Angle = cb.Angle;
            info.CoverA = cb.Cover as Bitmap;
            info.Tilt = (float)(10.0 * Math.Sin(cb.Phase * 0.9) * Math.PI / 180.0);   // mismo bamboleo que CubeBox.OnPaint
        }

        // Muestra/oculta el panel GPU (a pantalla completa) segun el modo: en NAVEGACION y en el BUSCADOR
        // (ambos se componen por GPU). En disco/video/atraccion/config se oculta y deja ver los controles CPU.
        // El cuadro del video (WMP) es una ventana HIJA hermana del panel GPU: si el panel acaba de
        // mostrarse, su BringToFront lo dejo por delante y taparia el video. Esto lo regresa arriba.
        private void KeepVideoOnTop() { try { if (videoOn && video != null && video.Visible) video.BringToFront(); } catch { } }

        private void SyncGpuFullScreen()
        {
            if (gpuPanel == null || !gpuEnabled) return;
            bool cfgOn = (lblConfig != null && lblConfig.Visible);
            // VIDEO con la interfaz por GPU (video_gpu=1, por defecto): un video en ventana YA NO manda todo
            // a render por CPU. Antes videoOn apagaba nav/disco/atraccion y la pantalla ENTERA (fondo
            // estrellado incluido) se repintaba por CPU mientras WMP decodificaba: en 2 nucleos ese era el
            // "rendimiento raro" con video, y como las animaciones son SOLO por GPU, con video sonando no
            // podian verse nunca (solo la del buscador, el unico modo que ya ignoraba el video). El cuadro
            // del video queda ENCIMA del panel (KeepVideoOnTop) y Windows recorta el panel donde el video
            // esta: no se pisan. Si en algun fierro se viera mal, video_gpu=0 vuelve al comportamiento de
            // antes. El video a PANTALLA COMPLETA si sigue apagando el panel: ahi el es dueno de todo.
            bool videoBlocks = videoOn && !videoGpuOn;
            bool notBlockedByVideoCfg = !videoBlocks && !videoFull && !cfgOn;
            bool noBlock = notBlockedByVideoCfg && !attractOn;
            bool disc = panelSongs != null && panelSongs.Visible;
            bool nav = albumView.Visible && !searchOpen && !disc && noBlock;
            // El VIDEO no bloquea el BUSCADOR: OpenSearch OCULTA el cuadro del video (el audio sigue), asi que
            // el panel GPU puede componer el buscador encima sin taparlo. Antes el video SI lo bloqueaba y el
            // buscador "se abria" INVISIBLE (panel oculto por video + overlay CPU oculto por GPU): el cliente
            // solo veia desaparecer y reaparecer el video. Nav/disco SI siguen bloqueados (ahi el video se VE).
            bool search = searchOpen && !attractOn && !cfgOn && !videoFull;
            // Interior de disco por GPU (por defecto). El bug de la lista era que RenderContent usaba
            // TextRenderer (GDI), que ignora la traslacion del Graphics: sobre la textura de UI (trasladada
            // a la posicion del panel) el texto caia en 0,0. Ya usa DrawString (GDI+), que la respeta.
            // Ctrl+F11 (gpuDiscTry) alterna a CPU como valvula de escape. Navegacion y buscador van por GPU.
            bool discGpu = disc && gpuDiscTry && !searchOpen && noBlock;
            bool attract = attractOn && notBlockedByVideoCfg;   // modo atraccion (reposo): cubo gigante + estrellas por GPU
            // Al CAMBIAR de modo, rehacer la textura de UI en el MISMO cuadro que cambia lo que se dibuja
            // (ShowAlbum, cubos): si no, se ve un instante el modo viejo detras del nuevo (p.ej. la rejilla de
            // discos por los huecos del teclado al cerrar el buscador). UiUrgent salta la limitacion de franja.
            int mode = search ? 2 : discGpu ? 3 : attract ? 4 : nav ? 1 : 0;
            if (mode != gpuMode) { gpuMode = mode; uiOverlaySig = SigNada; gpuPanel.UiUrgent = true; Diag("render: modo GPU " + mode + (videoOn ? " (video sonando)" : "")); }
            // Mientras el video SE VE, el panel baja a ~30 cuadros por segundo: el resto del equipo es para
            // el video (WMP decodifica por CPU en el fierro modesto). Sin video (o con el buscador abierto,
            // que lo oculta) vuelve al maximo.
            gpuPanel.ThrottleMs = (videoOn && video != null && video.Visible) ? 33 : 0;
            albumView.GpuCovered = (nav || search || discGpu || attract);   // panel GPU encima: albumView no repinta por CPU (evita rebuild invisible al paginar)
            if (nav || search || discGpu || attract)
            {
                if (gpuPanel.Left != 0 || gpuPanel.Top != 0 || gpuPanel.Width != ClientSize.Width || gpuPanel.Height != ClientSize.Height)
                    gpuPanel.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
                gpuPanel.AlbumRect = albumView.Bounds;
                gpuPanel.ShowAlbum = nav;   // solo en navegacion se dibuja la rejilla de cubos (buscador/disco/atraccion la cubren)
                if (!gpuPanel.Visible) { gpuPanel.Visible = true; gpuPanel.BringToFront(); KeepVideoOnTop(); }
            }
            else if (gpuPanel.Visible) { gpuPanel.Visible = false; Invalidate(true); }   // disco(CPU)/video/etc: deja ver los controles normales
        }

        // Donde EMPIEZA la franja de estado de abajo (la que va en su propia textura chica).
        private int BotStripTop(int h)
        {
            int t = (lblNowTop != null) ? lblNowTop.Top - 4 : (lblNow != null ? lblNow.Top - 4 : h - 96);   // la franja arranca en la linea de ARRIBA
            if (t < h / 2) t = h / 2;
            if (t > h - 8) t = h - 8;
            return t;
        }

        // Reconstruye la textura PRINCIPAL de la UI SOLO si cambio algo (firma). Devuelve la version.
        // La textura mide LO QUE EL MODO NECESITA, no la pantalla completa: en NAVEGACION solo la franja superior
        // (instrucciones + letras + encabezado, ~140px) -> rehacerla y subirla al paginar cuesta ~7x menos que
        // antes. Buscador/disco/atraccion usan hasta la franja de estado (que tiene su propia textura, mas abajo).
        private int EnsureUiOverlay()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 8 || h < 8) return uiOverlayVer;
            int mainH;
            if (searchOpen || (panelSongs != null && panelSongs.Visible) || attractOn) mainH = BotStripTop(h);
            else
            {
                mainH = 60;   // navegacion: hasta el fondo del encabezado/letras/instrucciones
                if (helpBar != null && helpBar.Bottom > mainH) mainH = helpBar.Bottom;
                if (letterBar != null && letterBar.Visible && letterBar.Bottom > mainH) mainH = letterBar.Bottom;
                if (lblHeader != null && lblHeader.Bottom > mainH) mainH = lblHeader.Bottom;
                if (lblStatus != null && lblStatus.Bottom > mainH) mainH = lblStatus.Bottom;
                mainH += 6;
            }
            if (mainH > h) mainH = h; if (mainH < 8) mainH = 8;
            long sig = UiSig();
            if (uiOverlay != null && uiOverlay.Width == w && uiOverlay.Height == mainH && sig == uiOverlaySig) return uiOverlayVer;
            if (uiOverlay == null || uiOverlay.Width != w || uiOverlay.Height != mainH)
            {
                if (uiOverlay != null) uiOverlay.Dispose();
                uiOverlay = new Bitmap(w, mainH, PixelFormat.Format32bppPArgb);
            }
            using (Graphics g = Graphics.FromImage(uiOverlay))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;   // gris: ClearType deja flecos sobre transparente
                if (searchOpen && searchOverlay != null)
                {
                    System.Drawing.Drawing2D.GraphicsState gs = g.Save();   // BUSCADOR (su UI sin starfield ni cubo)
                    g.TranslateTransform(searchOverlay.Left, searchOverlay.Top);
                    searchOverlay.RenderContent(g, new Rectangle(0, 0, searchOverlay.Width, searchOverlay.Height), true);
                    g.Restore(gs);
                }
                else if (panelSongs != null && panelSongs.Visible)
                {
                    DrawCtrlOverlay(g, helpBar);   // instrucciones ARRIBA tambien dentro del disco (guian al usuario, como en software)
                    System.Drawing.Drawing2D.GraphicsState gs = g.Save();   // INTERIOR DE DISCO: etiqueta + lista + barra de paginas (el cubo grande lo pone la GPU)
                    g.TranslateTransform(panelSongs.Left, panelSongs.Top);   // los controles del disco son hijos de panelSongs
                    DrawLabelOverlay(g, lblAlbum);
                    DrawCtrlOverlay(g, songList);
                    if (songPager != null && songPager.Visible) DrawCtrlOverlay(g, songPager);
                    g.Restore(gs);
                }
                else if (attractOn)   // MODO ATRACCION: solo los dos textos (el cubo gigante y las estrellas los pone la GPU)
                {
                    RectangleF cuboA = AttractCubeRect();   // mismo rectangulo que el cubo y que la animacion
                    int cubeBottom = (int)(cuboA.Y + cuboA.Height);
                    string line1 = (attractView != null && attractView.SongCount > 0) ? (attractView.SongCount.ToString("#,0") + " canciones") : "Tu rocola";
                    using (Font fBig = new Font(FontFamily.GenericSansSerif, 24f, FontStyle.Bold))
                    using (Font fSmall = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold))
                    using (StringFormat sfc = new StringFormat())
                    using (SolidBrush tb = new SolidBrush(Color.White))
                    {
                        sfc.Alignment = StringAlignment.Center; sfc.LineAlignment = StringAlignment.Center;
                        g.DrawString(line1, fBig, tb, new RectangleF(0, cubeBottom + 18, w, 36), sfc);
                        tb.Color = Color.FromArgb(235, 255, 220, 120);   // FIJO: el "respiro" lo pone un halo por GPU (FillUiGlows) -> esta textura no se rehace nunca en atraccion
                        g.DrawString("toca un boton para elegir", fSmall, tb, new RectangleF(0, cubeBottom + 58, w, 28), sfc);
                    }
                }
                else
                {
                    DrawCtrlOverlay(g, helpBar);
                    DrawCtrlOverlay(g, letterBar);
                    DrawLabelOverlay(g, lblHeader);
                    DrawLabelOverlay(g, lblStatus);
                }
                // Las etiquetas de estado de abajo YA NO van aqui: tienen su propia textura chica
                // (EnsureUiBotOverlay), asi el reloj de la cancion no rehace esta textura 2 veces/seg.
                // El contador de FPS tampoco: lo pinta el GpuPanel en su propia textura pequeña.
            }
            uiOverlaySig = sig; uiOverlayVer++;
            return uiOverlayVer;
        }

        // Reconstruye la textura de la FRANJA DE ESTADO de abajo (sonando/siguiente/tiempo/creditos/cola/
        // progreso) SOLO si cambio algo. Es una tira chica (~90px de alto): el tic del reloj y la barra de
        // progreso (~2 cambios/seg con musica) cuestan ~10x menos que rehacer la pantalla completa de antes.
        private Bitmap uiBotOverlay; private int uiBotVer; private long uiBotLastSig = SigNada;
        private int EnsureUiBotOverlay()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 8 || h < 8) return uiBotVer;
            int top = BotStripTop(h);
            int bh = h - top;
            long sig = UiBotSig();
            if (uiBotOverlay != null && uiBotOverlay.Width == w && uiBotOverlay.Height == bh && sig == uiBotLastSig) return uiBotVer;
            if (uiBotOverlay == null || uiBotOverlay.Width != w || uiBotOverlay.Height != bh)
            {
                if (uiBotOverlay != null) uiBotOverlay.Dispose();
                uiBotOverlay = new Bitmap(w, bh, PixelFormat.Format32bppPArgb);
            }
            using (Graphics g = Graphics.FromImage(uiBotOverlay))
            {
                g.Clear(Color.Transparent);
                if (!attractOn)   // en atraccion la pantalla es limpia: la franja queda transparente
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;   // gris: ClearType deja flecos sobre transparente
                    g.TranslateTransform(0, -top);   // las etiquetas estan en coords del form; la tira empieza en 'top'
                    DrawLabelOverlay(g, lblNowTop);   // 2a linea del titulo (vacia casi siempre: no dibuja nada)
                    DrawLabelOverlay(g, lblNow);
                    if (lblMejorada != null && lblMejorada.Visible) DrawLabelOverlay(g, lblMejorada);   // letrero MEJORADA (su brillo es un halo por quad, no rehace esta textura)
                    DrawLabelOverlay(g, lblNext);
                    DrawLabelOverlay(g, lblTime);
                    DrawLabelOverlay(g, lblCredits);
                    DrawLabelOverlay(g, lblCount);
                    DrawCtrlOverlay(g, progress);
                }
            }
            uiBotLastSig = sig; uiBotVer++;
            return uiBotVer;
        }

        // Llena el buffer con los HALOS ANIMADOS de la UI, en coords de pantalla. La GPU los dibuja como quads
        // aditivos: TODA animacion continua (destello del boton pulsado, pulso de la letra actual, dorado del TOP,
        // respiro del texto de atraccion) vive AQUI y no en la textura de UI -> la textura solo se rehace cuando
        // cambia un TEXTO real. Antes estos pulsos re-horneaban y RE-SUBIAN la pantalla completa hasta ~13+10
        // veces/seg: ese era el stuttering de la rocola. Devuelve cuantos escribio.
        private int FillUiGlows(GpuPanel.UiGlow[] buf)
        {
            if (buf == null) return 0;
            int n = 0;
            if (searchOpen && searchOverlay != null)
            {
                int sx = searchOverlay.Left, sy = searchOverlay.Top;
                for (int k = 0; k < 3 && n < buf.Length; k++)
                {
                    float cx, cy, r; int argb;
                    if (searchOverlay.GetHintGlow(k, out cx, out cy, out r, out argb))
                    { buf[n].Cx = sx + cx; buf[n].Cy = sy + cy; buf[n].R = r; buf[n].Argb = (uint)argb; n++; }
                }
            }
            else if (attractOn)
            {
                // Respiro DORADO detras de "toca un boton para elegir" (el texto va fijo en la textura).
                int w = ClientSize.Width, h = ClientSize.Height;
                int side = (int)(Math.Min(w, h) * 0.40); if (side < 80) side = 80;
                int cubeBottom = (h - side) / 2 - 30 + side;
                double p = 0.5 + 0.5 * Math.Sin(phase * 3.0);
                buf[n].Cx = w / 2f; buf[n].Cy = cubeBottom + 72; buf[n].R = 150f;
                buf[n].Argb = (uint)Color.FromArgb((int)(16 + 38 * p), 255, 220, 120).ToArgb(); n++;
            }
            else
            {
                if (helpBar != null && helpBar.Visible)   // destello del boton pulsado (navegacion e interior de disco)
                {
                    int hx = helpBar.Left, hy = helpBar.Top;
                    for (int k = 0; k < 4 && n < buf.Length; k++)
                    {
                        float cx, cy, r; int argb;
                        if (helpBar.GetFlashGlow(k, out cx, out cy, out r, out argb))
                        { buf[n].Cx = hx + cx; buf[n].Cy = hy + cy; buf[n].R = r; buf[n].Argb = (uint)argb; n++; }
                    }
                }
                if (letterBar != null && letterBar.Visible)
                {
                    float cx, cy, r;
                    if (n < buf.Length && letterBar.GetCurrentGlow(out cx, out cy, out r))   // pulso de la letra actual
                    {
                        double p = 0.5 + 0.5 * Math.Sin(phase * 2.2);   // mismo ritmo que antes, pero ahora a cuadro pleno y GRATIS
                        buf[n].Cx = letterBar.Left + cx; buf[n].Cy = letterBar.Top + cy; buf[n].R = r;
                        buf[n].Argb = (uint)Color.FromArgb((int)(45 + 65 * p), 110, 180, 255).ToArgb(); n++;
                    }
                    if (n < buf.Length && letterBar.GetTopGlow(out cx, out cy, out r))       // dorado pulsante del marcador TOP
                    {
                        double p = 0.5 + 0.5 * Math.Sin(phase * 2.6);
                        buf[n].Cx = letterBar.Left + cx; buf[n].Cy = letterBar.Top + cy; buf[n].R = r;
                        buf[n].Argb = (uint)Color.FromArgb((int)(22 + 50 * p), 255, 205, 90).ToArgb(); n++;
                    }
                }
            }
            // Letrero "MEJORADA" (franja de estado, visible en todos los modos menos atraccion): un resplandor
            // dorado suave que RESPIRA + un destello que RECORRE las letras cada ~4s (reflejo metalico).
            // El texto va FIJO en la textura de la franja: estos halos no rehacen NINGUNA textura.
            if (!attractOn && lblMejorada != null && lblMejorada.Visible && n + 1 < buf.Length)
            {
                float cy2 = lblMejorada.Top + lblMejorada.Height / 2f;
                double pb = 0.5 + 0.5 * Math.Sin(phase * 2.8);
                buf[n].Cx = lblMejorada.Left + lblMejorada.Width / 2f; buf[n].Cy = cy2;
                buf[n].R = lblMejorada.Width * 0.72f;
                buf[n].Argb = (uint)Color.FromArgb((int)(9 + 15 * pb), 255, 210, 100).ToArgb(); n++;
                double sw = (phase * 0.22) % 1.0;   // ciclo ~4s: 55% barrido de izq a der, 45% descanso
                if (sw < 0.55)
                {
                    double t = sw / 0.55;
                    int a = (int)(95 * Math.Sin(Math.PI * t));   // entra y sale suave
                    buf[n].Cx = lblMejorada.Left + (float)(t * lblMejorada.Width); buf[n].Cy = cy2; buf[n].R = 26f;
                    buf[n].Argb = (uint)Color.FromArgb(a, 255, 232, 150).ToArgb(); n++;
                }
            }
            return n;
        }

        // FIRMA de lo que hay que dibujar. Se compara CADA CUADRO para saber si hay que rehacer la textura,
        // asi que no puede armar una cadena: eso eran dos StringBuilder y dos cadenas nuevas por cuadro (~70 KB
        // por segundo de basura para el recolector, que es justo lo que provoca microtirones). Ahora se mezcla
        // todo en un numero de 64 bits (FNV-1a), sin reservar ni un byte. Dos estados distintos podrian caer en
        // el mismo numero, pero con 64 bits eso es 1 entre 18 trillones: y ni asi se rompe nada, solo tardaria
        // un cambio de texto en verse hasta el siguiente cambio.
        private const long SigNada = 0L;                     // valor imposible de la mezcla: fuerza rehacer
        private const long SigBase = unchecked((long)1469598103934665603L);
        private static long Mix(long h, int v) { unchecked { h ^= (uint)v; h *= 1099511628211L; } return h; }
        private static long Mix(long h, long v) { return Mix(Mix(h, (int)v), (int)(v >> 32)); }
        private static long Mix(long h, bool b) { return Mix(h, b ? 1 : 0); }
        private static long Mix(long h, string t) { return Mix(h, t == null ? 0 : t.GetHashCode()); }
        private long UiSig()
        {
            long sb = SigBase;
            sb = Mix(sb, ClientSize.Width); sb = Mix(sb, ClientSize.Height);
            if (searchOpen && searchOverlay != null)   // parte superior = buscador
            {
                sb = Mix(sb, 'S'); sb = Mix(sb, searchOverlay.Query); sb = Mix(sb, searchOverlay.SelRow); sb = Mix(sb, searchOverlay.SelCol);
                sb = Mix(sb, searchOverlay.FocusResults); sb = Mix(sb, searchOverlay.SelResult);
                sb = Mix(sb, searchOverlay.Results == null ? 0 : searchOverlay.Results.Count);
                sb = Mix(sb, searchOverlay.ToastOn); sb = Mix(sb, searchOverlay.ToastMsg);
            }
                  // El DESTELLO del boton pulsado YA NO entra en la firma: lo dibuja la GPU como halo aparte (FillUiGlows),
                  // asi que su desvanecido no re-hornea ni re-sube la textura del teclado -> tecleo fluido con brillo suave.
                  // Tampoco la fase del teclado (carisima). En reposo la firma es constante -> 0 reconstrucciones.
            else if (panelSongs != null && panelSongs.Visible)   // INTERIOR DE DISCO: etiqueta, seleccion, pagina
                // La GEOMETRIA entra en la firma: al arrancar o terminar un video, LayoutControls REACOMODA el
                // interior (con video, la lista y el cubo se comprimen a la mitad izquierda). Sin la geometria,
                // la firma no cambiaba y la textura quedaba horneada con las posiciones VIEJAS -la lista flotando
                // donde ya no va- hasta salir y volver a entrar al disco. El cubo no lo sufria (es en vivo).
            {
                sb = Mix(sb, 'D'); sb = Mix(sb, lblAlbum != null ? lblAlbum.Text : ""); sb = Mix(sb, songSel); sb = Mix(sb, songItems.Count);
                sb = Mix(sb, songList != null ? songList.PageStart : 0); sb = Mix(sb, bgAccent.ToArgb());
                sb = Mix(sb, panelSongs.Left); sb = Mix(sb, panelSongs.Top); sb = Mix(sb, panelSongs.Width); sb = Mix(sb, panelSongs.Height);
            }
            else if (attractOn)                        // MODO ATRACCION: solo la cantidad de canciones (el pulso del texto va como halo por quad)
            { sb = Mix(sb, 'A'); sb = Mix(sb, attractView != null ? attractView.SongCount : 0); }
            else if (letterBar != null)                // parte superior = navegacion (A-Z + encabezado)
            {
                sb = Mix(sb, 'N'); sb = Mix(sb, letterBar.Current); sb = Mix(sb, letterBar.Letters);
                sb = Mix(sb, letterBar.Available == null ? 0 : letterBar.Available.Count);
                sb = Mix(sb, lblHeader != null ? lblHeader.Text : ""); sb = Mix(sb, lblStatus != null ? lblStatus.Text : "");
            }
            // NI la fase del pulso de la letra NI el destello del boton pulsado (FlashSig) entran ya en la firma:
            // ambos los dibuja la GPU como halos aparte (FillUiGlows). Antes obligaban a rehacer Y RE-SUBIR esta
            // textura ~13 veces/seg en navegacion (pulso) y ~10 veces por CADA tecla (destello): ESE era el
            // stuttering de la rocola al paginar. Ahora, quieto en una pagina, la firma es constante -> 0 rehechas.
            // Las etiquetas de estado de abajo (sonando/tiempo/creditos/progreso) van en SU PROPIA textura chica
            // (UiBotSig/EnsureUiBotOverlay): el reloj de la cancion ya no rehace la pantalla completa 2 veces/seg.
            // El FPS tambien va en su propia textura (1/seg, diminuta).
            return (sb == SigNada) ? 1L : sb;   // que nunca coincida con "sin firma"
        }

        // Firma de la FRANJA DE ESTADO de abajo (sonando/siguiente/tiempo/creditos/cola/progreso): cambia ~2
        // veces/seg con musica sonando, pero su textura es una tira chica (~400KB vs ~4MB de pantalla completa).
        private long UiBotSig()
        {
            long sb = SigBase;
            sb = Mix(sb, ClientSize.Width); sb = Mix(sb, ClientSize.Height);
            if (attractOn) return Mix(sb, 'A');   // en atraccion la franja va limpia (transparente)
            if (lblNowTop != null) sb = Mix(sb, lblNowTop.Text);
            if (lblNow != null) sb = Mix(sb, lblNow.Text);
            if (lblMejorada != null && lblMejorada.Visible) { sb = Mix(sb, 'M'); sb = Mix(sb, lblMejorada.Left); sb = Mix(sb, lblMejorada.ForeColor.ToArgb()); }
            if (lblNext != null) sb = Mix(sb, lblNext.Text);
            if (lblTime != null) sb = Mix(sb, lblTime.Text);
            if (lblCredits != null) sb = Mix(sb, lblCredits.Text);
            if (lblCount != null) sb = Mix(sb, lblCount.Text);
            if (progress != null) { sb = Mix(sb, (int)(progress.Value * 300)); sb = Mix(sb, progress.Accent.ToArgb()); }
            return (sb == SigNada) ? 1L : sb;   // que nunca coincida con "sin firma"
        }

        // Dibuja un control (HelpBar/LetterBar/ProgressView) en la textura de UI, en su posicion del form.
        private static void DrawCtrlOverlay(Graphics g, Control c)
        {
            if (c == null || c.Width < 1 || c.Height < 1) return;
            System.Drawing.Drawing2D.GraphicsState st = g.Save();
            g.TranslateTransform(c.Left, c.Top);
            HelpBar hb = c as HelpBar;
            if (hb != null) hb.RenderContent(g, true);   // forGpu: sus pulsos van como halos por quad, no en la textura
            else { LetterBar lb = c as LetterBar; if (lb != null) lb.RenderContent(g, true);
            else { ProgressView pv = c as ProgressView; if (pv != null) pv.RenderContent(g);
            else { SongListView sl = c as SongListView; if (sl != null) sl.RenderContent(g);
            else { SongPagerBar sp = c as SongPagerBar; if (sp != null) sp.RenderContent(g); } } } }
            g.Restore(st);
        }

        // Dibuja el texto de una etiqueta en la textura de UI, respetando su fuente, color, alineacion y caja.
        // GDI reusables (StringFormat + pincel al que se muta el color): esto se llama ~6-7 veces por cada
        // reconstruccion de la textura de UI (que ocurre ~13fps en navegacion por el brillo de la letra).
        [ThreadStatic] private static StringFormat lblSf;
        [ThreadStatic] private static SolidBrush lblBr;
        private static void DrawLabelOverlay(Graphics g, Label l) { DrawLabelOverlay(g, l, (l != null) ? l.ForeColor : Color.White); }
        private static void DrawLabelOverlay(Graphics g, Label l, Color color)
        {
            if (l == null || string.IsNullOrEmpty(l.Text)) return;
            StringFormat sf = lblSf ?? (lblSf = new StringFormat());
            SolidBrush b = lblBr ?? (lblBr = new SolidBrush(Color.White)); b.Color = color;
            sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
            ContentAlignment a = l.TextAlign;
            if ((a & (ContentAlignment.TopLeft | ContentAlignment.MiddleLeft | ContentAlignment.BottomLeft)) != 0) sf.Alignment = StringAlignment.Near;
            else if ((a & (ContentAlignment.TopRight | ContentAlignment.MiddleRight | ContentAlignment.BottomRight)) != 0) sf.Alignment = StringAlignment.Far;
            else sf.Alignment = StringAlignment.Center;
            if ((a & (ContentAlignment.TopLeft | ContentAlignment.TopCenter | ContentAlignment.TopRight)) != 0) sf.LineAlignment = StringAlignment.Near;
            else if ((a & (ContentAlignment.BottomLeft | ContentAlignment.BottomCenter | ContentAlignment.BottomRight)) != 0) sf.LineAlignment = StringAlignment.Far;
            else sf.LineAlignment = StringAlignment.Center;
            g.DrawString(l.Text, l.Font, b, (RectangleF)l.Bounds, sf);
        }

#endif

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            phase += 0.045;
            frameTick++;
            // OPTIMIZACION CLAVE: los brillos del fondo se mueven MENOS DE MEDIO PIXEL por cuadro, asi
            // que repintar TODA la pantalla 25 veces/seg por ellos es desperdicio. El fondo completo se
            // repinta 1 de cada 3 cuadros (~8/seg, indistinguible en blobs borrosos); el cubo que gira
            // se repinta SOLO en su casilla a velocidad plena. Esto recorta ~3x el costo fijo de CPU.
            bool fullFrame = (frameTick % 3) == 0;   // fondo completo 1 de cada 3 (~8fps): el fondo es estatico, repintarlo mas seguido solo gasta CPU (clave en el CPU lento de la rocola)

#if ROCOLA_GPU
            // FIN de las animaciones. El AVANCE no se calcula aqui: lo calcula el render (60 fps), asi que el
            // zoom es fluido aunque este temporizador vaya a 24. Aqui solo se apagan al cumplirse su tiempo.
            if (transMode != 0 && Environment.TickCount - transTick >= TransMs) AbortDiscTrans();
            if (winMode != 0 && Environment.TickCount - winTick >= WinMs) AbortWinTrans();
#endif
            DrainCoverTrash();   // libera carátulas expulsadas (seguro: no hay repintado en curso)
#if ROCOLA_GPU
            // Con la rocola quieta, se van horneando las casillas de la pagina de al lado: cuando alguien pase
            // de pagina, ya estan listas y el deslizamiento sale fluido de una. Dos casillas por vuelta y con
            // tope de 4 ms: si alguien esta usando la rocola en ese momento, no le quita nada.
            if (gpuEnabled && albumView != null && albumView.Visible && !attractOn && !searchOpen && !videoOn
                && transMode == 0 && winMode == 0 && !albumView.Sliding) albumView.BakeAhead();
#endif
            CheckBusinessDayReset();   // al cruzar las 7 AM: reinicia el estado (nada de anoche cruza el corte del dia)

            // BEAT: toma el RMS cuyo momento de OIRSE ya llego (medido ~0.85s antes, al llenar el colchon) y
            // detecta GOLPES contra un piso movil: el pulso visual cae encima del golpe que se esta oyendo,
            // con ataque INSTANTANEO y caida rapida (pulso marcado), mas un fondo suave para que no muera
            // entre golpes. Antes era una envolvente de sonoridad adelantada ~1s: no transmitia el ritmo.
            int nowT = Environment.TickCount;
            lock (levelLock)
                while (levelQ.Count > 0 && nowT - (levelQ.Peek().At + AudioVisualDelayMs) >= 0)
                { Lvl l = levelQ.Dequeue(); rmsLo = l.Lo; rmsMid = l.Mid; rmsHi = l.Hi; if (l.Esp != null) Array.Copy(l.Esp, specNow, SpecBands); }
            if (nowPlaying == null) { rmsLo = rmsMid = rmsHi = 0f; for (int b = 0; b < SpecBands; b++) specNow[b] = 0f; }
#if ROCOLA_GPU
            // Con un VIDEO en pantalla el audio sale del reproductor de Windows y NO pasa por la cadena, asi que no
            // llegan bandas nuevas: sin esto el fondo se quedaria CONGELADO con la forma de la ultima cancion durante
            // todo el video. (El pulso no lo sufre: su detector de golpes se apaga solo contra su piso movil.)
            if (videoOn || !EspectroActivo) for (int b = 0; b < SpecBands; b++) specNow[b] = 0f;
            AvanzaAurora();   // el fondo lo dibuja el panel GPU; por CPU ni se calcula
#endif
            // Cada banda detecta su propio golpe contra su propio piso movil (ataque instantaneo, caida rapida). Asi
            // el bombo (graves) y los platillos/voz (agudos) laten por separado y se SIENTE la separacion de beats.
            levLo = BandBeat(rmsLo, ref avgLo, levLo);
            levMid = BandBeat(rmsMid, ref avgMid, levMid);
            levHi = BandBeat(rmsHi, ref avgHi, levHi);
            audioLevel = Math.Max(levLo, Math.Max(levMid, levHi));   // nivel GLOBAL (deriva/GPU/buscador): el mas fuerte de las 3

            // ATENUACION del starfield mientras se navega con musica sonando (ver BgDimLevel); transicion suave.
            float dimTgt = (nowPlaying != null && !attractOn && (DateTime.Now - lastInput).TotalSeconds < BgDimIdleSecs) ? BgDimLevel : 1f;
            bgDim += (dimTgt - bgDim) * 0.12f;
            if (Math.Abs(bgDim - dimTgt) < 0.01f) bgDim = dimTgt;
            albumView.BgDim = bgDim;
            if (panelSongs != null) panelSongs.BgDim = bgDim;   // el interior del disco comparte el mismo campo: atenua igual
            if (searchOverlay != null) searchOverlay.BgDim = bgDim;
            // Mientras la atenuacion CAMBIA, repinta el fondo del form (franjas de arriba/abajo) para que atenuen
            // junto con la rejilla: el campo es UNA SOLA PIEZA (si no, se veria una banda mas clara arriba/abajo).
            if (Math.Abs(bgDim - bgDimPrev) > 0.0015f) this.Invalidate();
            bgDimPrev = bgDim;
#if ROCOLA_GPU
            if (gpuPanel != null) gpuPanel.Dim = bgDim;
#endif

            bool transitioning = (bgAccent.R != bgAccentTarget.R || bgAccent.G != bgAccentTarget.G || bgAccent.B != bgAccentTarget.B);
            if (transitioning)
            {
                bgAccent = LerpColor(bgAccent, bgAccentTarget, 0.16);
                if (Math.Abs(bgAccent.R - bgAccentTarget.R) <= 2 && Math.Abs(bgAccent.G - bgAccentTarget.G) <= 2 && Math.Abs(bgAccent.B - bgAccentTarget.B) <= 2)
                    bgAccent = bgAccentTarget;
                this.Invalidate();   // tambien con buscador abierto: el fondo es barato y la franja de estado debe recolorear
            }
            albumView.BgAccent = bgAccent;
#if ROCOLA_GPU
            SyncGpuFullScreen();   // muestra/oculta el panel GPU (pantalla completa) segun el modo, ANTES de los returns por modo
            if (gpuPanel != null && gpuPanel.Visible)
            {
                if (attractOn)   // atraccion: cielo nocturno fijo (sin musica, no sigue al genero)
                { gpuPanel.SkyTop = 0xFF06081Au; gpuPanel.SkyMid = 0xFF0C0E28u; gpuPanel.SkyBot = 0xFF12143Au; }
                else
                { gpuPanel.SkyTop = ToD3D(SkyColor(bgAccent)); gpuPanel.SkyMid = ToD3D(SkyColorMid(bgAccent)); gpuPanel.SkyBot = ToD3D(SkyColor2(bgAccent)); }   // el degradado sigue al genero
            }
#endif
            if (progress != null) progress.Accent = bgAccent;   // ESTETICA: la barra de progreso sigue el color del genero

            if (searchOpen)
            {
                if ((DateTime.Now - searchLastActivity).TotalSeconds >= 60) { CloseSearch(); return; }   // 1 min sin actividad
                searchOverlay.Accent = bgAccent;
                searchOverlay.Phase = phase;
                searchOverlay.AudioPulse = audioLevel;
                searchOverlay.CubeAngle += 0.075;   // gira mas rapido
                searchOverlay.Invalidate();         // repinta cada tick (24fps): fondo + cubo mas fluidos (no-op si el buscador va por GPU: el control esta oculto)
                bool sGpu = false;
#if ROCOLA_GPU
                sGpu = (gpuPanel != null && gpuPanel.Visible);   // panel GPU encima: el mini-cubo lo dibuja la GPU (repintar el control seria invisible)
#endif
                if (miniCube.Visible) { miniCube.Phase = phase; miniCube.Angle += 0.06; if (!sGpu) miniCube.Invalidate(); }   // el angulo avanza siempre (la GPU lo lee)
                TickMejorada(sGpu);   // la franja de estado sigue visible bajo el buscador: el letrero sigue brillando
                return;
            }

            // VIDEO A PANTALLA COMPLETA tras inactividad (para apreciarlo mejor)
            if (videoOn && !videoFull && (DateTime.Now - lastInput).TotalSeconds > 8) EnterVideoFull();
            if (videoFull)
            {
                if (lblAttract != null)
                {
                    double pz = 0.5 + 0.5 * Math.Sin(phase * 4.0);   // aviso AMARILLO PULSANTE
                    lblAttract.ForeColor = Color.FromArgb((int)(140 + 115 * pz), (int)(110 + 125 * pz), (int)(0 + 60 * pz));
                }
                return;   // no repintamos lo de atras: el video va mas fluido en pantalla completa
            }

            // MODO ATRACCION: en reposo, sin nada sonando, mostrar la pantalla estrellada con el cubo.
            // (nunca durante un re-escaneo: EnterAttract muestrea 'albums' y el worker lo esta mutando)
            if (!attractOn && nowPlaying == null && queue.Count == 0 && !searchOpen && !videoOn && !libBuilding
                && albums.Count > 0 && (lblConfig == null || !lblConfig.Visible)
                && (DateTime.Now - lastInput).TotalSeconds > AttractIdleSecs)
                EnterAttract();
            if (attractOn)
            {
                attractView.Phase = phase; attractView.Angle += 0.05; attractView.Invalidate();
                return;
            }

            bool albumGpuBg = false;
#if ROCOLA_GPU
            albumGpuBg = (gpuPanel != null && gpuPanel.Visible);   // el panel GPU (opaco, pantalla completa) tapa los controles CPU: repintarlos seria trabajo invisible que ademas corta el bucle de render GPU
#endif
            // (CPU) Anima el BRILLO del boton pulsado en la barra de ayuda: sin esto el destello se disparaba
            // pero nadie repintaba el control -> no se veia. Repinta mientras dura (~0.4s) y UNA vez mas al
            // expirar (para borrar el ultimo halo). En GPU lo dibuja el panel como halo aparte (FillUiGlows).
            if (helpBar != null)
            {
                int hfs = helpBar.FlashSig();
                if ((hfs != 0 || helpFlashPrev != 0) && helpBar.Visible && !albumGpuBg) helpBar.Invalidate();
                helpFlashPrev = hfs;
            }
            if (letterBar.Visible) { letterBar.Phase = phase; if (!albumGpuBg) letterBar.Invalidate(); }   // 24fps (CPU); en GPU el pulso de la letra es un halo por quad (FillUiGlows): la textura NO se rehace
            TickMejorada(albumGpuBg);   // brillo del letrero MEJORADA (dorado, DSEE espectral)
            { SpectralTap sp0 = curSbr; if (sp0 != null && sp0.CutHz > 0f) { eqMeasCut = sp0.CutHz; eqMeasCutLo = sp0.CutLoHz; } }   // acantilados de la cancion que suena
            if (albumView.Visible && !albumGpuBg) { albumView.Phase = phase; albumView.PulseLo = levLo; albumView.PulseMid = levMid; albumView.PulseHi = levHi; albumView.AnimTick(fullFrame || transitioning); }
#if ROCOLA_GPU
            else if (albumView.Visible && albumGpuBg) { albumView.Phase = phase; albumView.StepSpinGpu(); }   // el giro avanza aunque el fondo+cubos los pinte la GPU (sin repintar por CPU)
#endif
            if (panelSongs.Visible)
            {
                panelSongs.Accent = bgAccent;
                if ((fullFrame || transitioning) && !albumGpuBg) { panelSongs.Invalidate(); lblAlbum.Invalidate(); }   // glows: 1 de cada 3 (solo si el CPU esta a la vista)
                cubeBox.Phase = phase; cubeBox.Angle += 0.06;   // el angulo/bamboleo avanzan SIEMPRE (la GPU los lee via BuildGpuCubeState)
                if (!albumGpuBg) cubeBox.Invalidate();          // el cubo grande por CPU solo se repinta si se ve
            }
            if (miniCube.Visible) { miniCube.Phase = phase; miniCube.Angle += 0.06; if (!albumGpuBg) miniCube.Invalidate(); }   // idem mini-cubo (en GPU lo dibuja el panel)
        }

        private static string Fmt(TimeSpan t) { return ((int)t.TotalMinutes) + ":" + t.Seconds.ToString("00"); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            preScanStop = true;   // que el pre-analisis suelte el archivo y termine
            DisposePlaybackNow();
            // APAGADO DELIBERADO vs APAGON. El boton de poder de la rocola esta configurado en Windows para APAGAR
            // -> al presionarlo, Windows avisa y ESTE evento se dispara (CloseReason.WindowsShutDown; tambien Alt+F4
            // = UserClosing). Ahi borramos el estado vivo para que manana arranque LIMPIA. En cambio, un APAGON corta
            // la corriente de golpe y no corre NADA de esto: el archivo SOBREVIVE y, al volver la luz, la rocola
            // REANUDA (creditos + cola). Es decir, la sola presencia del archivo al arrancar delata que fue apagon.
            if (e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.UserClosing)
                try { File.Delete(StateFile()); } catch { }
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
        }

        // Camino NORMAL: envoltura fina sobre AudioFileReader (float + volumen + tiempos en un solo objeto).
        private sealed class FileAudioSource : IAudioSource
        {
            private readonly AudioFileReader r;
            public FileAudioSource(string path) { r = new AudioFileReader(path); }
            public NAudio.Wave.WaveFormat WaveFormat { get { return r.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count) { return r.Read(buffer, offset, count); }
            public float Volume { set { r.Volume = value; } }
            public TimeSpan CurrentTime { get { return r.CurrentTime; } }
            public TimeSpan TotalTime { get { return r.TotalTime; } }
            public void Dispose() { r.Dispose(); }
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
                mp3 = new Mp3FileReader(ms);
                sample = new SampleChannel(mp3, false);
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return sample.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count) { return sample.Read(buffer, offset, count); }
            public float Volume { set { sample.Volume = value; } }
            public TimeSpan CurrentTime { get { return mp3.CurrentTime; } }
            public TimeSpan TotalTime { get { return mp3.TotalTime; } }
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
        // (ProbeAudioTier). Active se alterna EN VIVO desde Ctrl+F3 para comparar A/B (al alternar hay un
        // microcorte de ~46ms: es el colchon del STFT llenandose; normal y esperado).

        // Red neuronal chica cargada de un archivo RIA1 (capas de pesos+sesgos, ocultas con tanh y la de salida
        // lineal). La usan DOS cosas distintas, cada una con su modelo y su contrato de entradas/salidas: el DSEE
        // para la envolvente de los agudos que re-crea, y el EQ automatico para su objetivo por cancion.
        // Los dos modelos. El del DSEE re-crea la envolvente de los agudos (28 datos de la ventana -> 12 sub-bandas).
        // El del EQ mira la parte del espectro que los codecs NO tocan (el cuerpo, 158 Hz - 5.6 kHz) y dice cuanto
        // grave y cuanto aire tendria una cancion BIEN GRABADA con ese mismo cuerpo: 5 entradas -> 3 salidas.
        private static readonly RedChica NeuralEnv = new RedChica(28, 12);
        private static readonly RedChica EqNet = new RedChica(EqNetIn, EqNetOut);
        private const int EqNetIn = 5, EqNetOut = 3;
        private static readonly int[] EqNetSalida = { 0, 6, 7 };   // que bandas predice: 50 Hz, 10 kHz y 15.8 kHz

        private sealed class RedChica
        {
            public volatile bool Loaded;
            private float[][] W, B;            // pesos (fila-mayor) y sesgos por capa
            private int[] rOut, cIn;           // dimensiones por capa
            private float[] buf1, buf2;        // buffers de trabajo (sin asignar por ventana)
            private readonly int nIn, nOut;    // contrato: cuantas entradas y cuantas salidas debe tener el modelo
            public RedChica(int entradas, int salidas) { nIn = entradas; nOut = salidas; }

            public void TryLoad(string path)
            {
                try
                {
                    if (!File.Exists(path)) return;
                    byte[] d = File.ReadAllBytes(path);
                    if (d.Length < 12 || d[0] != (byte)'R' || d[1] != (byte)'I' || d[2] != (byte)'A' || d[3] != (byte)'1') return;
                    int p = 4;
                    int nl = BitConverter.ToInt32(d, p); p += 4;
                    if (nl < 1 || nl > 8) return;
                    float[][] w = new float[nl][], b = new float[nl][];
                    int[] ro = new int[nl], ci = new int[nl]; int maxDim = 0;
                    for (int L = 0; L < nl; L++)
                    {
                        int r = BitConverter.ToInt32(d, p); p += 4;
                        int c = BitConverter.ToInt32(d, p); p += 4;
                        if (r < 1 || r > 512 || c < 1 || c > 512) return;
                        w[L] = new float[r * c]; b[L] = new float[r];
                        for (int i = 0; i < r * c; i++) { w[L][i] = BitConverter.ToSingle(d, p); p += 4; }
                        for (int i = 0; i < r; i++) { b[L][i] = BitConverter.ToSingle(d, p); p += 4; }
                        ro[L] = r; ci[L] = c; if (r > maxDim) maxDim = r; if (c > maxDim) maxDim = c;
                    }
                    if (ci[0] != nIn || ro[nl - 1] != nOut) return;   // el modelo no es para este uso
                    for (int L = 1; L < nl; L++) if (ci[L] != ro[L - 1]) return;
                    buf1 = new float[maxDim]; buf2 = new float[maxDim];
                    W = w; B = b; rOut = ro; cIn = ci;
                    Loaded = true;
                }
                catch { Loaded = false; }
            }

            // y = red(x). Capas ocultas con tanh; la de salida lineal. Devuelve false si no hay modelo.
            public bool Predict(float[] x, float[] y)
            {
                float[][] w = W, b = B;
                if (!Loaded || w == null) return false;
                float[] cur = x, nxt = buf1;
                int curN = cIn[0];
                for (int L = 0; L < w.Length; L++)
                {
                    int r = rOut[L]; float[] wl = w[L], bl = b[L];
                    float[] dst = (L == w.Length - 1) ? y : nxt;
                    for (int i = 0; i < r; i++)
                    {
                        float s = bl[i]; int off = i * curN;
                        for (int j = 0; j < curN; j++) s += wl[off + j] * cur[j];
                        dst[i] = (L == w.Length - 1) ? s : (float)Math.Tanh(s);
                    }
                    cur = dst; curN = r;
                    nxt = (cur == buf1) ? buf2 : buf1;   // alterna buffers (la entrada x no se toca)
                }
                return true;
            }
        }

        private sealed class SpectralTap : NAudio.Wave.ISampleProvider
        {
            private const int Frame = 2048, Hop = Frame / 2;
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch, rate;
            private readonly float binHz;
            public volatile bool Active;                   // false = pase directo (costo ~cero)
            private bool wasActive;                        // para reiniciar el colchon al alternar (A/B)
            private readonly float[][] inBuf, outAcc;      // por canal: ventana de entrada y acumulador de solape
            private int fill;                              // muestras acumuladas (por canal) en inBuf
            private readonly float[] re, im, win;          // espectro de trabajo y ventana raiz-de-Hann
            private readonly int[] bitrev; private readonly float[] twCos, twSin;   // tablas de la FFT
            private readonly float[] inter;                // buffer intercalado para leer del origen
            private readonly float[] outQ; private int qHead, qLen;   // FIFO de salida (intercalado)
            private float cutHz;                           // acantilado suavizado (0 = aun sin medir)
            private bool srcEnded;
            private readonly float[] iaIn = new float[28], iaOut = new float[12];   // IA propia: entrada/salida de la red (reusables)

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
                rate = Math.Max(8000, s.WaveFormat.SampleRate);
                binHz = (float)rate / Frame;
                inBuf = new float[ch][]; outAcc = new float[ch][];
                for (int c = 0; c < ch; c++) { inBuf[c] = new float[Frame]; outAcc[c] = new float[Frame]; }
                re = new float[Frame]; im = new float[Frame];
                win = new float[Frame];
                for (int i = 0; i < Frame; i++) win[i] = (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / Frame));   // raiz de Hann: analisis*sintesis = Hann, que al 50% suma EXACTO 1
                bitrev = new int[Frame];
                twCos = new float[Frame / 2]; twSin = new float[Frame / 2];
                BuildFftTables();
                inter = new float[Hop * ch];
                outQ = new float[Frame * ch * 2];          // FIFO holgado (nunca acumula mas de Hop*ch)
            }

            // Tablas de la FFT (inversion de bits y factores de giro). Las comparten el camino de
            // reproduccion y el de solo analisis, para que ambos calculen EXACTAMENTE el mismo espectro.
            private void BuildFftTables()
            {
                for (int i = 1, j = 0; i < Frame; i++)
                {
                    int bit = Frame >> 1;
                    for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                    j |= bit; bitrev[i] = j;
                }
                for (int k = 0; k < Frame / 2; k++) { twCos[k] = (float)Math.Cos(2.0 * Math.PI * k / Frame); twSin[k] = (float)Math.Sin(2.0 * Math.PI * k / Frame); }
            }

            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }

            public int Read(float[] buffer, int offset, int count)
            {
                bool act = Active;
                if (act != wasActive)   // alternado en vivo: reinicia el colchon (microcorte de ~46ms) y sigue
                {
                    wasActive = act;
                    fill = 0; qHead = 0; qLen = 0;
                    for (int c = 0; c < ch; c++) Array.Clear(outAcc[c], 0, Frame);
                }
                if (!act) return src.Read(buffer, offset, count);   // pase directo

                int written = 0;
                while (written < count)
                {
                    if (qLen > 0)                                    // hay salida lista: entregarla
                    {
                        int take = Math.Min(qLen, count - written);
                        for (int i = 0; i < take; i++) { buffer[offset + written + i] = outQ[qHead]; qHead++; if (qHead >= outQ.Length) qHead = 0; }
                        qLen -= take; written += take;
                        continue;
                    }
                    if (srcEnded) break;                             // origen agotado y FIFO vacio -> fin (WaveOut detiene)
                    int need = (Frame - fill) * ch;                  // llenar la ventana de analisis
                    if (need > inter.Length) need = inter.Length;
                    int got = src.Read(inter, 0, need);
                    if (got <= 0)
                    {
                        srcEnded = true;
                        if (fill > 0)                                // cola final: completar con silencio y procesar una vez
                        {
                            for (int c = 0; c < ch; c++) Array.Clear(inBuf[c], fill, Frame - fill);
                            fill = Frame; ProcessFrame();
                        }
                        continue;
                    }
                    int frames = got / ch;
                    for (int f = 0; f < frames; f++)
                        for (int c = 0; c < ch; c++) inBuf[c][fill + f] = inter[f * ch + c];
                    fill += frames;
                    if (fill >= Frame) ProcessFrame();
                }
                return written;
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
                binHz = (float)rate / Frame;
                re = new float[Frame]; im = new float[Frame]; win = new float[Frame];
                for (int i = 0; i < Frame; i++) win[i] = (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / Frame));
                bitrev = new int[Frame]; twCos = new float[Frame / 2]; twSin = new float[Frame / 2];
                BuildFftTables();
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

            private void ProcessFrame()
            {
                for (int c = 0; c < ch; c++)
                {
                    float[] xin = inBuf[c];
                    for (int i = 0; i < Frame; i++) { re[i] = xin[i] * win[i]; im[i] = 0f; }
                    Fft(false);
                    if (c == 0) DetectCliff();   // el acantilado es del ARCHIVO: se mide en el canal 0 y aplica a todos
                    Regenerate();
                    Fft(true);
                    float[] acc = outAcc[c];
                    for (int i = 0; i < Frame; i++) acc[i] += re[i] * win[i];
                }
                for (int f = 0; f < Hop; f++)      // la primera mitad ya recibio sus DOS solapes: lista para salir
                    for (int c = 0; c < ch; c++)
                    { int tail = qHead + qLen; if (tail >= outQ.Length) tail -= outQ.Length; outQ[tail] = outAcc[c][f]; qLen++; }
                for (int c = 0; c < ch; c++)
                {
                    Array.Copy(outAcc[c], Hop, outAcc[c], 0, Frame - Hop); Array.Clear(outAcc[c], Frame - Hop, Hop);
                    Array.Copy(inBuf[c], Hop, inBuf[c], 0, Frame - Hop);
                }
                fill = Frame - Hop;
            }

            private int Hz(float hz) { int b = (int)(hz / binHz); int ny = Frame / 2; return b < 1 ? 1 : (b > ny - 1 ? ny - 1 : b); }

            // Localiza el "acantilado" del codec: la frecuencia MAS ALTA donde todavia hay energia comparable
            // a la banda media (4-8 kHz). Suavizado entre ventanas para que no baile; en silencio no se mide.
            private void DetectCliff()
            {
                int b4 = Hz(4000f), b8 = Hz(8000f);
                if (b8 <= b4 + 4) return;
                double refE = 0; for (int b = b4; b < b8; b++) refE += re[b] * re[b] + im[b] * im[b];
                refE /= (b8 - b4);
                if (refE < 1e-10) return;                        // silencio: conservar la medida previa
                float top = Math.Min(20000f, (Frame / 2) * binHz * 0.94f);
                float found = 0f;
                for (float hz = top - 300f; hz >= 9000f; hz -= 300f)
                {
                    int a = Hz(hz), b = Hz(hz + 300f); if (b <= a) b = a + 1;
                    double e = 0; for (int k = a; k < b; k++) e += re[k] * re[k] + im[k] * im[k];
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
                    double e = 0; for (int k = a; k < b; k++) e += re[k] * re[k] + im[k] * im[k];
                    e /= (b - a);
                    if (e > refE * 0.02) { found = hz; break; }   // aqui ya hay grave de verdad
                }
                if (found <= 0f) found = 140f;                   // nada vivo por debajo de 140: grabacion muy flaca
                cutLoHz = (cutLoHz <= 0f) ? found : (cutLoHz * 0.85f + found * 0.15f);
            }


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

            // Re-crea la banda por ENCIMA del acantilado: t <- t/2 (una octava abajo = contenido REAL que
            // sobrevivio). La ENVOLVENTE (cuanto suena cada sub-banda nueva) la decide la IA PROPIA si hay
            // modelo cargado (rocola_ia.bin); si no, la curva clasica: costura empatada y ~-9 dB/octava.
            private void Regenerate()
            {
                if (cutHz <= 0f || cutHz >= 19300f) return;      // sin medida aun, o archivo ya completo
                int ny = Frame / 2;
                int cut = (int)(cutHz / binHz);
                if (cut < 64) return;                             // corte absurdamente bajo: no inventar tanto
                mejoro = true;                                    // desde aqui SI se reconstruye: el letrero queda toda la cancion
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
                    re[t] += re[s] * f; im[t] += im[s] * f;
                    int m = Frame - t;                            // espejo (frecuencias negativas): la señal sigue siendo real
                    re[m] = re[t]; im[m] = -im[t];
                    f *= step;
                }
            }

            // FFT radix-2 iterativa en sitio (tablas precalculadas). inverse=true incluye la escala 1/N.
            private void Fft(bool inverse)
            {
                int n = Frame;
                for (int i = 0; i < n; i++)
                {
                    int j = bitrev[i];
                    if (j > i) { float t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
                }
                for (int size = 2; size <= n; size <<= 1)
                {
                    int half = size >> 1, tstep = n / size;
                    for (int i = 0; i < n; i += size)
                    {
                        int k = 0;
                        for (int j = i; j < i + half; j++, k += tstep)
                        {
                            float wr = twCos[k], wi = inverse ? twSin[k] : -twSin[k];
                            int b = j + half;
                            float tr = re[b] * wr - im[b] * wi;
                            float ti = re[b] * wi + im[b] * wr;
                            re[b] = re[j] - tr; im[b] = im[j] - ti;
                            re[j] += tr; im[j] += ti;
                        }
                    }
                }
                if (inverse)
                {
                    float sc = 1f / n;
                    for (int i = 0; i < n; i++) { re[i] *= sc; im[i] *= sc; }
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
        private sealed class DeepBassProvider : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch;
            public volatile float Amount;      // 0..1 (perilla de Ctrl+F3)
            private float lp1, lp2, lp3;       // aislar el grave profundo (3 polos = 18 dB/oct)
            private float env;                 // energia del grave (ataque rapido, caida lenta)
            private float hpS, lpS;            // banda por donde salen los armonicos
            private readonly float aLow, aHp, aLp, aAtk, aRel;
            public DeepBassProvider(NAudio.Wave.ISampleProvider s, float amount)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels); Amount = amount;
                int fs = s.WaveFormat.SampleRate; if (fs < 8000) fs = 44100;
                aLow = Coef(100f, fs);    // por debajo de aqui esta lo que la bocina no puede dar
                aHp = Coef(95f, fs);      // los armonicos salen de ~95 Hz...
                aLp = Coef(320f, fs);     // ...a ~320 Hz, donde cualquier bocina responde bien
                aAtk = Coef(1f / (2f * (float)Math.PI * 0.004f), fs);   // 4 ms: agarra el golpe del bombo
                aRel = Coef(1f / (2f * (float)Math.PI * 0.150f), fs);   // 150 ms: no bombea
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
                float amt = Amount;
                if (n <= 0 || amt <= 0.001f) return n;   // apagado: pase directo
                float mix = amt * 0.85f;
                int end = offset + n - (ch - 1);   // solo cuadros COMPLETOS (si sobra una muestra suelta, pasa tal cual)
                for (int i = offset; i < end; i += ch)
                {
                    float m = buffer[i];
                    for (int c = 1; c < ch; c++) m += buffer[i + c];
                    if (ch > 1) m /= ch;
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
                    lpS += (h - lpS) * aLp; h = lpS * mix;
                    for (int c = 0; c < ch; c++) buffer[i + c] += h;   // el mismo grave a los dos canales: centrado
                }
                return n;
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
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                int frames = n / ch;
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
        private sealed class LookaheadLimiter : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly int ch;
            private readonly float ceiling;    // techo (0..1): la senal nunca lo pasa
            private readonly int look;         // frames de pre-escucha (retardo)
            private readonly float[] delay;    // retardo circular: look frames * canales
            private int dpos;                  // frame actual del circular
            private int primed;                // frames ya cargados (durante el cebado sale silencio)
            private float env = 1f;            // ganancia de reduccion actual (1 = sin reducir)
            private readonly float atk, rel;   // bajada (rapida) y recuperacion (lenta)
            public LookaheadLimiter(NAudio.Wave.ISampleProvider s, float ceilingLin, double lookMs)
            {
                src = s; ch = Math.Max(1, s.WaveFormat.Channels); ceiling = ceilingLin;
                int rate = s.WaveFormat.SampleRate; if (rate < 8000) rate = 44100;
                look = Math.Max(16, (int)(lookMs * 0.001 * rate));
                delay = new float[look * ch];
                atk = 1f - (float)Math.Exp(-3.0 / look);            // baja ~95% en el tiempo de pre-escucha
                rel = 1f - (float)Math.Exp(-1.0 / (0.12 * rate));   // recupera en ~120 ms
            }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n <= 0) return n;
                int frames = n / ch;
                for (int f = 0; f < frames; f++)
                {
                    int bi = offset + f * ch;
                    float peak = 0f;
                    for (int c = 0; c < ch; c++) { float a = buffer[bi + c]; if (a < 0f) a = -a; if (a > peak) peak = a; }
                    float target = (peak > ceiling) ? (ceiling / peak) : 1f;
                    env += (target < env ? atk : rel) * (target - env);   // ataca rapido, suelta lento
                    int di = dpos * ch;
                    for (int c = 0; c < ch; c++)
                    {
                        float outv = delay[di + c] * env;    // muestra de hace look frames, ya con la ganancia bajada
                        delay[di + c] = buffer[bi + c];       // guarda la entrante
                        if (primed < look) outv = 0f;         // cebado: los primeros look frames salen en silencio
                        else if (outv > ceiling) outv = ceiling;    // red de seguridad si un transitorio se colo
                        else if (outv < -ceiling) outv = -ceiling;
                        buffer[bi + c] = outv;
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
                for (int f = 0; f < frames; f++)
                {
                    long p = pos + f;
                    float g = 1f;
                    if (totalFrames > 0)
                    {
                        long rem = totalFrames - p;                                     // frames que faltan para el final
                        if (rem < fadeFrames) { float go = (rem <= 0) ? 0f : (float)((double)rem / fadeFrames); if (go < g) g = go; }   // fundido de SALIDA
                    }
                    if (g < 1f) { int bi = offset + f * ch; for (int c = 0; c < ch; c++) buffer[bi + c] *= g; }
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
            if (x < -3f) return -1f;
            if (x > 3f) return 1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        // Toma el nivel de audio (RMS) del bloque recien leido SIN modificar el sonido: solo observa el
        // buffer que ya lleno el lector y lo reporta. Sirve para que las estrellas laten con la musica.
        // Como el colchon es de ~1s, el nivel va algo adelantado de lo que se oye (envolvente de energia).
        private class LevelTap : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider src;
            private readonly Form1 owner;
            public float Gain = 1f;   // NIVELADO: ganancia de volumen de esta cancion (1 = sin cambio). Se aplica DESPUES de medir el nivel crudo.
            public float Master = 1f; // SUBIR TODO: ganancia general (dB de Ctrl+F3) para todas las canciones. Los picos que se pasen los redondea la RODILLA SUAVE de abajo (no recorte aspero).
            // REALCE DE AGUDOS (exciter armonico, tipo DSEE): separa la banda alta con un paso-alto de un polo,
            // la satura suave (tanh: crea armonicos NUEVOS por encima, el "aire" que el MP3 recorta) y mezcla
            // una pizca de vuelta. Todo en el dominio del tiempo: ~2 operaciones y un tanh por muestra, ~1-2%
            // de CPU en la rocola (nada de FFT). 0 = apagado (la pasada ni se ejecuta si tampoco hay nivelado).
            public float Excite = 0f;   // intensidad 0..1 (perilla en Ctrl+F3; ajustable en vivo)
            private float[] exLp;       // estado del filtro del exciter (uno por canal)
            // EQ AUTOMATICO: correccion en dB por banda (8) o null = apagado. Se aplica con biquads
            // PEAKING (RBJ) en cascada, SOLO en las bandas con correccion real (0 dB = identidad exacta,
            // el biquad ni se crea). ~9 operaciones por muestra por banda activa (tipico: 2-4 bandas).
            public volatile float[] EqDb;
            private float[] eqDbUsed;   // referencia aplicada (los coeficientes se recalculan solo si cambia)
            private float[][] eqCoef;   // 8 biquads (null = banda sin correccion)
            private float[] eqSt;       // estados: canal * banda * 4
            private int eqActivos;
            public LevelTap(NAudio.Wave.ISampleProvider s, Form1 o) { src = s; owner = o; }
            public NAudio.Wave.WaveFormat WaveFormat { get { return src.WaveFormat; } }
            public int Read(float[] buffer, int offset, int count)
            {
                int n = src.Read(buffer, offset, count);
                if (n > 0 && owner != null)
                {
                    owner.ReportAudioBlock(buffer, offset, n, WaveFormat.SampleRate, WaveFormat.Channels);   // mide el nivel CRUDO (beat + sonoridad + huella tonal) ANTES de tocar nada
                    float g = Gain * Master, ex = Excite;
                    float[] eqDb = EqDb;
                    bool exOn = ex > 0.001f, eqOn = eqDb != null;
                    if (eqOn && !ReferenceEquals(eqDb, eqDbUsed))
                    {
                        int ch0 = WaveFormat.Channels; if (ch0 < 1) ch0 = 1;
                        eqCoef = new float[8][]; eqActivos = 0;
                        for (int b = 0; b < 8; b++) { eqCoef[b] = Form1.EqPeak(Form1.EqCentros[b], WaveFormat.SampleRate, eqDb[b]); if (eqCoef[b] != null) eqActivos++; }
                        eqSt = new float[ch0 * 32];
                        eqDbUsed = eqDb;
                    }
                    if (eqOn && eqActivos == 0) eqOn = false;
                    if (g != 1f || exOn || eqOn)   // nivelado + EQ + realce en UNA pasada (el limitador de la cadena controla los picos)
                    {
                        int ch = WaveFormat.Channels; if (ch < 1) ch = 1;
                        if (exOn && (exLp == null || exLp.Length < ch)) exLp = new float[ch];
                        const float A = 0.55f;    // paso-alto ~5.5 kHz (a 44.1k): la banda donde el MP3 pierde el "aire"
                        float mix = ex * 0.5f;    // tope de mezcla: el realce es una PIZCA (el exceso suena aspero)
                        int end = offset + n, ci = 0;
                        for (int i = offset; i < end; i++)
                        {
                            float v = buffer[i] * g;
                            if (eqOn)
                            {
                                int p0 = ci * 32;
                                for (int b = 0; b < 8; b++)
                                {
                                    float[] cf = eqCoef[b]; if (cf == null) continue;
                                    int q = p0 + b * 4;
                                    float o = cf[0] * v + cf[1] * eqSt[q] + cf[2] * eqSt[q + 1] - cf[3] * eqSt[q + 2] - cf[4] * eqSt[q + 3];
                                    eqSt[q + 1] = eqSt[q]; eqSt[q] = v;
                                    eqSt[q + 3] = eqSt[q + 2]; eqSt[q + 2] = o;
                                    v = o;
                                }
                            }
                            if (exOn)
                            {
                                float lp = (exLp[ci] += (v - exLp[ci]) * A);
                                float h = v - lp;                                   // solo la banda alta que sobrevivio
                                v += (float)Math.Tanh(h * 2.5) * 0.4f * mix;        // saturacion suave = armonicos nuevos por encima (brillo/presencia)
                            }
                            if (exOn || eqOn) { ci++; if (ci >= ch) ci = 0; }
                            // El control de picos ya NO se hace aqui (era una rodilla que distorsiona al empujar):
                            // lo hace el LIMITADOR CON PRE-ESCUCHA al final de la cadena, que baja la ganancia suave
                            // ANTES del pico. Aqui la senal sale CRUDA (puede pasar de 1.0; el limitador la controla).
                            buffer[i] = v;
                        }
                    }
                }
                return n;
            }
        }

        // Campo de estrellas reutilizable: cielo + estrellas titilantes (mas brillantes) + estrellas
        // fugaces ocasionales. Lo usan el fondo de navegacion (teñido por genero) y el modo atraccion
        // (cielo negro y estrellas multicolor). El titileo es LENTO a proposito para verse suave aun a 8fps.
        private class Starfield
        {
            public bool Varied = false;   // true = colores variados (atraccion); false = blanco->tinte (genero)
            public float Density = 1f;    // multiplicador de cantidad de estrellas (el fondo del form usa menos)
            public bool QuietBands = false;   // true = menos estrellas en las franjas de arriba (abecedario) y abajo (barra de estado)
            public float Dim = 1f;        // atenuacion del starfield (1 = normal; ~0.45 mientras se navega con musica). Solo estrellas/brillos, no el cielo

            private struct Star { public float X, Y; public int Size, Kind; public byte R, G, B; public float Tint, Off, Spd; public byte Band; }   // Band: 0=graves,1=medios,2=agudos (a que frecuencia late)
            private struct Shoot { public bool On; public float X, Y, VX, VY; public float Life, MaxLife; }
            private Star[] stars;
            private int sw = -1, sh = -1;
            private SolidBrush sb;
            private Pen sp;
            // ESTETICA (glow): sprites de resplandor radial pre-horneados UNA sola vez y COMPARTIDOS por todas
            // las instancias. Se estampan detras del nucleo de cada estrella: fuerte en la grande, tenue en la
            // chica, nada en los puntos -> "brillan de verdad" sin contaminar visualmente.
            private static Bitmap[] glowPal;   // ESTETICA/RENDIMIENTO: un sprite de glow PRE-HORNEADO por color (sin matriz por estrella)
            private static readonly Color[] glowCols = {
                Color.White,                          // 0 blanco
                Color.FromArgb(255, 120, 120),        // 1 rojo
                Color.FromArgb(255, 180, 110),        // 2 naranja
                Color.FromArgb(255, 225, 140),        // 3 dorado
                Color.FromArgb(150, 255, 180),        // 4 verde
                Color.FromArgb(150, 235, 255),        // 5 cian
                Color.FromArgb(150, 180, 255),        // 6 azul
                Color.FromArgb(225, 165, 255)         // 7 violeta/rosa
            };
            private static readonly object glowLock = new object();
            private readonly PointF[] spk = new PointF[8];   // ESTETICA: buffer reusable del sparkle (sin asignar por estrella)
            private Shoot[] shoots = new Shoot[2];
            private int nextShoot = 0, lastTick = 0;
            // TITILEO por CUADROS HORNEADOS: K capas con el MISMO patron de estrellas a distinto brillo.
            // Rotarlas (una copia por cuadro) reproduce el titileo SIN recalcular ~120 estrellas por cuadro:
            // recupera el efecto animado al costo de una copia (no de recalculo) -> fluido en el CPU lento.
            private const int TwK = 8;
            private Bitmap[] layers;
            private int lw = -1, lh = -1;
            private readonly Random rnd = new Random();
            // Cache del cuadro: el starfield se dibuja a un bitmap y solo se reconstruye cada ~110ms
            // (para el titileo). Asi cada Draw es un blit barato aunque lo pidan muchas veces seguidas
            // los controles transparentes encima (etiquetas de estado, lblAlbum, cubeBox). Antes se
            // redibujaban ~89 rectangulos en cada repintado transparente -> tormenta -> traba/crash.
            private Bitmap cache;
            private int cw = -1, ch = -1, cSky = 0, cTint = 0, lastBuild = 0;
            private int cSky2 = 0;                                        // color inferior del degradado del cache
            private int sBucket = int.MinValue, lBucket = int.MinValue;   // "minuto" del patron actual (cambia cada 60s)
            // PANTALLA COMPLETA como UNA SOLA PIEZA: el campo de estrellas se hornea al tamano de TODA la pantalla
            // (SkyFullW x SkyFullH) y cada fondo (franjas del form, rejilla de discos, interior del disco) pinta
            // solo su VENTANA (offset SkyOffsetX/Y). Asi las estrellas cruzan los bordes de las secciones sin
            // cortarse ni desalinearse: el patron, el cielo y la densidad son continuos entre secciones.
            public int SkyOffsetX = 0, SkyOffsetY = 0, SkyFullW = 0, SkyFullH = 0;
            private int FullW(int w) { return SkyFullW > w ? SkyFullW : w; }   // ancho de pantalla completa (>= la ventana); si no se fijo, la ventana ES la pantalla
            private int FullH(int h) { return SkyFullH > h ? SkyFullH : h; }
            public Color SkyMid = Color.Empty;                            // color INTERMEDIO (azul noche); si Empty -> 2 paradas (punto medio)
            private LinearGradientBrush skyBrush;                         // pincel de cielo en DEGRADADO (cacheado, ligado al genero)
            private int gbW = -1, gbOff = -99999, gbFull = -1, gbTop = 0, gbMid = 0, gbBot = 0;
            private Bitmap skyCache;                                       // RENDIMIENTO: cielo (degradado) HORNEADO a bitmap; se copia por cuadro en vez de rellenar el degradado cada vez
            private int scW = -1, scH = -1;
            private int fscTop = 0, fscMid = 0, fscBot = 0;               // colores con los que se horneo el cielo de pantalla completa (se re-hornea solo al cambiar genero/tamano)
            // DERIVA con la musica (igual que la version GPU): al sonar, el campo entero se desliza LENTO en diagonal
            // (abajo-derecha) y se envuelve por los bordes. Barato: en la vista de discos solo desplaza el blit de la
            // capa de estrellas (toroidal); en el buscador desplaza cada estrella. Solo avanza en repintados COMPLETOS.
            private int driftLastMs; private float driftEnv, driftX, driftY;
            private const float DriftVX = 18f, DriftVY = 9f;             // px/seg (derecha, abajo): lento, diagonal 2:1 (como GPU)

            // Avanza la deriva segun el tiempo real, con envolvente suave (arranca/para sin saltos). Se llama SOLO
            // en repintados completos para que las casillas parciales (el cubo que gira) cuadren con el fondo.
            private void UpdateDrift(int w, int h, float pulse)
            {
                int now = Environment.TickCount;
                float dt = (driftLastMs == 0) ? 0f : (now - driftLastMs) / 1000f;
                driftLastMs = now;
                if (dt < 0f) dt = 0f; else if (dt > 0.2f) dt = 0.2f;   // acota saltos (tras un hueco largo, p.ej. saliendo de GPU)
                float tgt = (pulse > 0.02f) ? 1f : 0f;
                driftEnv += (tgt - driftEnv) * (tgt > driftEnv ? Math.Min(1f, dt * 2.5f) : Math.Min(1f, dt * 0.6f));   // ataque medio, caida lenta
                driftX += driftEnv * DriftVX * dt; if (w > 0) driftX %= w;   // env>=0 -> solo crece; modulo -> [0,w)
                driftY += driftEnv * DriftVY * dt; if (h > 0) driftY %= h;
            }

            // Pincel de cielo en degradado vertical CONTINUO: el degradado abarca TODA la pantalla (SkyFullH)
            // y se desplaza segun donde esta este fondo (SkyOffsetY), para que las secciones no se noten
            // divididas (todo se ve como una sola pieza). Cacheado: se recrea solo al cambiar tamano/posicion/colores.
            private void EnsureSkyBrush(int w, int h, Color top, Color bottom)
            {
                int off = SkyOffsetY;
                int full = (SkyFullH > h) ? SkyFullH : h;
                Color mid = (SkyMid.A == 0)
                    ? Color.FromArgb(255, (top.R + bottom.R) / 2, (top.G + bottom.G) / 2, (top.B + bottom.B) / 2)   // sin mid: punto medio (= 2 paradas)
                    : SkyMid;
                int t = top.ToArgb(), m = mid.ToArgb(), b = bottom.ToArgb();
                if (skyBrush != null && gbW == w && gbOff == off && gbFull == full && gbTop == t && gbMid == m && gbBot == b) return;
                if (skyBrush != null) { skyBrush.Dispose(); skyBrush = null; }
                LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, -off, w, full + 1), top, bottom, LinearGradientMode.Vertical);
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { top, mid, bottom };          // negro arriba -> azul noche en medio -> resplandor/azul abajo
                cb.Positions = new float[] { 0f, 0.42f, 1f };          // negro SOLO en el cenit; el azul domina casi toda la pantalla; genero abajo
                lgb.InterpolationColors = cb;
                skyBrush = lgb;
                gbW = w; gbOff = off; gbFull = full; gbTop = t; gbMid = m; gbBot = b;
            }

            // Hornea un sprite de resplandor por color (una vez). Caro de crear, baratisimo de estampar despues.
            private static void EnsureGlow()
            {
                if (glowPal != null) return;
                lock (glowLock)
                {
                    if (glowPal != null) return;
                    Bitmap[] g2 = new Bitmap[glowCols.Length];
                    for (int i = 0; i < glowCols.Length; i++) g2[i] = BakeGlow(48, 185, glowCols[i]);   // glow FUERTE por color
                    glowPal = g2;
                }
            }

            // Glow del MISMO COLOR que la estrella: elige el sprite pre-horneado mas cercano (sin matriz por estrella).
            private static Bitmap GlowFor(int r, int g, int b)
            {
                Bitmap[] gp = glowPal;
                if (gp == null) return null;
                int best = 0, bd = int.MaxValue;
                for (int i = 0; i < glowCols.Length; i++)
                {
                    int dr = r - glowCols[i].R, dg = g - glowCols[i].G, db = b - glowCols[i].B;
                    int d = dr * dr + dg * dg + db * db;
                    if (d < bd) { bd = d; best = i; }
                }
                return gp[best];
            }

            private static Bitmap BakeGlow(int size, int peakAlpha, Color color)
            {
                Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                using (GraphicsPath gp = new GraphicsPath())
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    gp.AddEllipse(0, 0, size, size);
                    using (PathGradientBrush pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterPoint = new PointF(size / 2f, size / 2f);
                        pgb.CenterColor = Color.FromArgb(peakAlpha, color.R, color.G, color.B);   // nucleo del color de la estrella
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0, color.R, color.G, color.B) };   // se desvanece a transparente
                        g.FillEllipse(pgb, 0, 0, size, size);
                    }
                }
                return bmp;
            }

            // Dibuja una estrella de 4 puntas como UNA SOLA pieza (sparkle/destello), en vez de un cuadrito
            // con dos palos. Usa un buffer reusable; el llamador debe tener SmoothingMode.AntiAlias activo.
            private void DrawSparkle(Graphics g, Brush b, int px, int py, int R)
            {
                if (R < 2) R = 2;
                float r = R * 0.20f; if (r < 1f) r = 1f;   // radio interior: puntas finas pero unidas al centro
                spk[0] = new PointF(px, py - R);
                spk[1] = new PointF(px + r, py - r);
                spk[2] = new PointF(px + R, py);
                spk[3] = new PointF(px + r, py + r);
                spk[4] = new PointF(px, py + R);
                spk[5] = new PointF(px - r, py + r);
                spk[6] = new PointF(px - R, py);
                spk[7] = new PointF(px - r, py - r);
                g.FillPolygon(b, spk);
            }

            private void Ensure(int w, int h)
            {
                int bucket = 0;   // patron FIJO: regenerarlo cada minuto obligaba a re-hornear ~120 estrellas (glow+destello) -> tiron periodico en la rocola
                if (stars != null && sw == w && sh == h && sBucket == bucket) return;
                sw = w; sh = h; sBucket = bucket;
                Random r = new Random((20260620 + bucket * 40503) & 0x7FFFFFFF);   // seed FIJO (bucket=0): el patron ya no rota por minuto (regenerarlo daba un tiron periodico)
                int n = (int)(((double)(w * h) / 6500) * Density);   // MAS estrellas (densidad por area), escalada por Density
                int lo = (int)(90 * Density); if (lo < 20) lo = 20;
                // Tope de estrellas: este campo ahora cubre la PANTALLA COMPLETA (fondo + rejilla + interior de disco,
                // todo una sola pieza), asi que el tope sube para que no se vea disperso en monitores grandes. En la
                // rocola (1024x768) el conteo por area (~121) queda MUY por debajo del tope, asi que no cambia nada alli.
                if (n < lo) n = lo; if (n > 340) n = 340;
                Color[] pal = {
                    Color.White, Color.White, Color.White,                                     // blancas (peso, salen mas)
                    Color.FromArgb(170, 205, 255), Color.FromArgb(120, 170, 255),               // azul claro / azul
                    Color.FromArgb(205, 165, 255), Color.FromArgb(255, 170, 230),               // violeta / rosa
                    Color.FromArgb(255, 220, 140), Color.FromArgb(255, 165, 120),               // dorado / naranja
                    Color.FromArgb(150, 255, 225), Color.FromArgb(255, 150, 150)                // verde-agua / rojo suave
                };
                stars = new Star[n];
                // ESTETICA: insetar las posiciones para que ninguna estrella (los brazos del destello + su
                // glow) quede CORTADA en el borde del bitmap. El margen cubre el alcance del destello grande.
                float mfx = 22f / w; if (mfx > 0.2f) mfx = 0.2f;
                float mfy = 22f / h; if (mfy > 0.2f) mfy = 0.2f;
                float minD2 = 20f * 20f;   // ESTETICA: separacion minima (px) para que NO se encimen unas con otras
                for (int i = 0; i < n; i++)
                {
                    Star s = new Star();
                    // Posicion con SEPARACION MINIMA: hasta 12 intentos buscando un hueco lejos de las ya puestas.
                    float sx = mfx, sy = mfy;
                    for (int attempt = 0; attempt < 12; attempt++)
                    {
                        sx = mfx + (float)r.NextDouble() * (1f - 2f * mfx);
                        float yy = (float)r.NextDouble();
                        if (QuietBands)   // adelgaza SUAVE hacia los bordes (abecedario arriba, estado abajo) sin cortar de golpe
                        {
                            float keep = 1f;
                            if (yy < 0.12f) keep = yy / 0.12f;             // 0 en el borde de arriba -> densidad plena al llegar a los cubos
                            else if (yy > 0.91f) keep = (1f - yy) / 0.09f; // densidad plena al salir de los cubos -> 0 en el borde de abajo
                            if (keep < 1f && (float)r.NextDouble() > keep) yy = 0.12f + (float)r.NextDouble() * 0.79f;   // reubica al centro
                        }
                        sy = mfy + yy * (1f - 2f * mfy);   // inset vertical (mismo motivo que el horizontal)
                        bool clear = true;
                        for (int j = 0; j < i; j++)
                        {
                            float dxp = (sx - stars[j].X) * w, dyp = (sy - stars[j].Y) * h;
                            if (dxp * dxp + dyp * dyp < minD2) { clear = false; break; }
                        }
                        if (clear) break;   // hueco libre encontrado
                    }
                    s.X = sx; s.Y = sy;
                    int kk = r.Next(100);
                    s.Kind = (kk < 12) ? 2 : (kk < 34) ? 1 : 0;                       // 12% destello GRANDE, 22% destello chico, resto punto
                    s.Size = (s.Kind == 2) ? (6 + r.Next(5)) : (s.Kind == 1) ? (3 + r.Next(4)) : (2 + r.Next(3));   // destellos GRANDES 6-10, chicos 3-6, puntos 2-4
                    // A QUE FRECUENCIA late cada estrella (para SENTIR la separacion): las GRANDES con los graves
                    // (bombo), las medianas repartidas entre medios y agudos. Los puntos no laten (no reaccionan).
                    s.Band = (s.Kind == 2) ? (byte)0 : (byte)(1 + r.Next(2));
                    if (Varied)
                    {
                        Color c = pal[r.Next(pal.Length)]; s.R = c.R; s.G = c.G; s.B = c.B; s.Tint = 0f;   // atraccion: multicolor
                    }
                    else if (s.Kind == 2)
                    {
                        int bp = r.Next(100);   // destellos GRANDES: colores vivos como la imagen (blanco/dorado/cian/violeta)
                        Color c = (bp < 34) ? Color.White : (bp < 60) ? pal[7] : (bp < 80) ? pal[9] : pal[5];
                        s.R = c.R; s.G = c.G; s.B = c.B; s.Tint = 0f;
                    }
                    else
                    {
                        int pick = r.Next(100);
                        if (pick < 35) { s.R = 255; s.G = 255; s.B = 255; s.Tint = 0f; }                                       // blanca
                        else if (pick < 55) { s.R = 255; s.G = 255; s.B = 255; s.Tint = 0.45f + (float)r.NextDouble() * 0.55f; }   // tenida hacia el genero
                        else { Color c = pal[3 + r.Next(pal.Length - 3)]; s.R = c.R; s.G = c.G; s.B = c.B; s.Tint = 0f; }       // mas color variado
                    }
                    s.Off = (float)(r.NextDouble() * Math.PI * 2);
                    s.Spd = 0.6f + (float)r.NextDouble() * 1.6f;   // titileo LENTO
                    stars[i] = s;
                }
                if (sb == null) sb = new SolidBrush(Color.White);
                if (sp == null) sp = new Pen(Color.White, 2.2f);
            }

            // Patron FIJO (sin fundido): el campo de estrellas ya no cambia cada minuto. Antes el fundido +
            // la regeneracion provocaban un tiron periodico (re-hornear ~120 estrellas) y 3s de repintados
            // extra por minuto, costoso en el CPU lento de la rocola. Se mantiene 1 (sin atenuar) siempre.
            private static float StarFade() { return 1f; }

            public void Draw(Graphics g, int w, int h, Color skyTop, Color skyBottom, Color tint, double phase, bool shooting, float pulse)
            {
                if (w < 4 || h < 4) return;
                Ensure(w, h);
                int now = Environment.TickCount;
                int dt = now - lastBuild;
                bool needNew = cache == null || cw != w || ch != h;
                // En repintados PARCIALES (p.ej. solo la casilla del cubo que gira encima) NO reconstruimos
                // el cuadro entero: basta copiar el cache. Asi el +framerate (~24fps) no desperdicia CPU
                // redibujando ~120 estrellas para una franja minima; el fondo completo se refresca en los
                // repintados completos (cuando el recorte cubre toda la vista: buscador y atraccion cada cuadro).
                RectangleF clip = g.ClipBounds;
                bool partial = !needNew && (clip.Width < w - 1 || clip.Height < h - 1);
                bool rebuild = needNew
                    || (!partial && (cSky != skyTop.ToArgb() || cSky2 != skyBottom.ToArgb() || cTint != tint.ToArgb() || dt >= 40 || dt < 0));   // ~24fps
                if (rebuild)
                {
                    if (needNew)
                    {
                        if (cache != null) cache.Dispose();
                        cache = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                        cw = w; ch = h;
                    }
                    using (Graphics cg = Graphics.FromImage(cache))
                        RenderInto(cg, w, h, skyTop, skyBottom, tint, phase, shooting, pulse);
                    cSky = skyTop.ToArgb(); cSky2 = skyBottom.ToArgb(); cTint = tint.ToArgb(); lastBuild = now;
                }
                g.DrawImageUnscaled(cache, 0, 0);
            }

            // Dibuja un cuadro completo del starfield (cielo + estrellas + fugaces) sobre el bitmap cache.
            private void RenderInto(Graphics g, int w, int h, Color skyTop, Color skyBottom, Color tint, double phase, bool shooting, float pulse)
            {
                EnsureSkyBrush(w, h, skyTop, skyBottom);
                g.FillRectangle(skyBrush, 0, 0, w, h);
                pulse *= Dim;                       // atenuado mientras se navega: el beat tambien baja
                double boost = pulse * 0.62;        // el audio sube el brillo de las estrellas (medianas/grandes)
                int armUp = (int)(pulse * 7.0);     // y agranda las puntas del destello con el beat
                float fade = StarFade() * Dim;      // fundido out/in del patron (cada minuto) x atenuacion al navegar
                EnsureGlow();                       // ESTETICA: sprites de resplandor (horneados una vez)
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.SmoothingMode = SmoothingMode.AntiAlias;   // ESTETICA: el sparkle se ve liso (una sola pieza)
                float gfade = fade < 0f ? 0f : (fade > 1f ? 1f : fade);   // el glow encoge a 0 en el parpadeo del patron (sync con los nucleos)
                UpdateDrift(w, h, pulse);           // deriva con la musica (RenderInto solo corre en reconstruccion completa, ~24fps)
                int dx = (int)driftX, dy = (int)driftY;
                for (int i = 0; i < stars.Length; i++)
                {
                    Star s = stars[i];
                    bool reactive = (s.Kind != 0);   // BEAT (audio) + glow SOLO en medianas (1) y grandes (2); los puntos NO
                    double tw = (0.70 + 0.42 * (0.5 + 0.5 * Math.Sin(phase * s.Spd + s.Off)) + (reactive ? boost : 0.0)) * fade;
                    int a = (int)(tw * 255); if (a > 255) a = 255; if (a < 0) a = 0;
                    int cr, cg, cb;
                    if (s.Tint > 0f) { cr = (int)(255 + (tint.R - 255) * s.Tint); cg = (int)(255 + (tint.G - 255) * s.Tint); cb = (int)(255 + (tint.B - 255) * s.Tint); }
                    else { cr = s.R; cg = s.G; cb = s.B; }
                    int px = (int)(s.X * w) + dx; if (px >= w) px -= w;   // + deriva con la musica (envuelve)
                    int py = (int)(s.Y * h) + dy; if (py >= h) py -= h;
                    sb.Color = Color.FromArgb(a, cr, cg, cb);
                    // GLOW del MISMO COLOR detras del nucleo + estrella de 4 puntas. El beat agranda glow y puntas
                    // solo en medianas/grandes; los puntos van como punto redondo fijo, sin glow.
                    if (reactive)
                    {
                        int gd = (int)((s.Size * 3 + 6 + (int)(pulse * 10)) * gfade);   // el beat agranda el glow; el fundido lo encoge
                        Bitmap gb = GlowFor(cr, cg, cb);
                        if (gb != null && gd > 0) g.DrawImage(gb, new Rectangle(px - gd / 2, py - gd / 2, gd, gd));
                        DrawSparkle(g, sb, px, py, (s.Kind == 2 ? s.Size + 6 : s.Size + 2) + armUp);
                    }
                    else { float ds = s.Size < 2 ? 2 : s.Size; g.FillEllipse(sb, px - ds / 2f, py - ds / 2f, ds, ds); }
                }
                if (shooting) DrawShoots(g, w, h);
            }

            // Fondo del formulario (franjas): cielo cacheado + estrellas TITILANTES por cuadros horneados.
            public void DrawCached(Graphics g, int w, int h, Color skyTop, Color skyBottom)
            {
                if (w < 4 || h < 4) return;
                EnsureSkyCache(w, h, skyTop, skyBottom);
                EnsureLayers(w, h);
                BlitSkyAndStars(g, w, h);
            }

            // Hornea el cielo (degradado) al tamano de la PANTALLA COMPLETA (FullW x FullH) y lo reutiliza. Cada
            // fondo copia solo su ventana (en BlitSkyAndStars, con offset), asi el degradado es CONTINUO entre las
            // secciones sin costuras. Copiar un bitmap es mas barato que rellenar un degradado de 3 paradas por
            // cuadro (clave en alta resolucion). Se re-hornea solo al cambiar tamano de pantalla o colores (genero).
            private void EnsureSkyCache(int w, int h, Color top, Color bottom)
            {
                int FW = FullW(w), FH = FullH(h);
                Color mid = (SkyMid.A == 0)
                    ? Color.FromArgb(255, (top.R + bottom.R) / 2, (top.G + bottom.G) / 2, (top.B + bottom.B) / 2)   // sin mid: punto medio
                    : SkyMid;
                int t = top.ToArgb(), m = mid.ToArgb(), b = bottom.ToArgb();
                if (skyCache != null && scW == FW && scH == FH && fscTop == t && fscMid == m && fscBot == b) return;
                if (skyCache == null || scW != FW || scH != FH)
                {
                    if (skyCache != null) skyCache.Dispose();
                    skyCache = new Bitmap(FW, FH, PixelFormat.Format32bppPArgb);
                    scW = FW; scH = FH;
                }
                using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, 0, FW, FH + 1), top, bottom, LinearGradientMode.Vertical))
                {
                    ColorBlend cb = new ColorBlend(3);
                    cb.Colors = new Color[] { top, mid, bottom };          // negro arriba -> azul noche en medio -> resplandor/genero abajo
                    cb.Positions = new float[] { 0f, 0.42f, 1f };          // negro SOLO en el cenit; el azul domina casi toda la pantalla
                    lgb.InterpolationColors = cb;
                    using (Graphics sg = Graphics.FromImage(skyCache)) sg.FillRectangle(lgb, 0, 0, FW, FH);
                }
                fscTop = t; fscMid = m; fscBot = b;
            }

            // Pinta la VENTANA de este fondo dentro del campo de PANTALLA COMPLETA: cielo OPACO por copia directa
            // (SourceCopy, rapido) + la capa de estrellas del cuadro de titileo ACTUAL encima (SourceOver). Ambos
            // bitmaps son de pantalla completa; se dibujan corridos por (-SkyOffsetX, -SkyOffsetY) y el recorte del
            // pintor deja ver solo su seccion. Como todas las secciones leen el MISMO bitmap a la MISMA posicion de
            // pantalla, las estrellas cruzan los bordes sin cortarse ni desalinearse (todo es UNA SOLA PIEZA).
            // 'advance'=false (repintado PARCIAL: la casilla del cubo que gira) reusa la capa del ultimo repintado
            // completo: si avanzara por reloj, la casilla mostraria un cuadro de titileo distinto al del resto.
            private int shownLayer = -1;   // capa mostrada en el ultimo repintado completo
            private void BlitSkyAndStars(Graphics g, int w, int h) { BlitSkyAndStars(g, w, h, true); }
            private void BlitSkyAndStars(Graphics g, int w, int h, bool advance)
            {
                int offX = SkyOffsetX, offY = SkyOffsetY;
                CompositingMode ocm = g.CompositingMode;
                g.CompositingMode = CompositingMode.SourceCopy;   // cielo opaco: copia directa (sin mezclar alfa)
                g.DrawImageUnscaled(skyCache, -offX, -offY);      // la ventana [offX,offY] cae en (0,0); el recorte del pintor limita el area
                g.CompositingMode = CompositingMode.SourceOver;   // estrellas con transparencia encima
                if (advance || shownLayer < 0 || layers == null || shownLayer >= layers.Length) shownLayer = CurrentStarLayerIndex();
                Bitmap sl = (layers != null && shownLayer >= 0 && shownLayer < layers.Length) ? layers[shownLayer] : null;
                if (sl != null)
                {
                    if (Dim < 0.985f)
                    {
                        EnsureDimIA();   // copia con ALFA (atenuado): solo mientras se navega con musica
                        BlitLayerDim(g, sl, -offX, -offY, sl.Width, sl.Height);
                    }
                    else g.DrawImageUnscaled(sl, -offX, -offY);
                }
                g.CompositingMode = ocm;
            }

            // Atributos de imagen con el alfa de la atenuacion (cacheados; la matriz se rehace solo si Dim cambio de escalon)
            private ImageAttributes dimIA; private int dimIAKey = -1;
            private void EnsureDimIA()
            {
                int key = (int)(Dim * 50f);   // escalones de 0.02: sin rehacer la matriz cada cuadro durante la transicion
                if (dimIA != null && dimIAKey == key) return;
                if (dimIA == null) dimIA = new ImageAttributes();
                ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = Dim;
                dimIA.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                dimIAKey = key;
            }
            private void BlitLayerDim(Graphics g, Bitmap sl, int x, int y, int w, int h)
            {
                g.DrawImage(sl, new Rectangle(x, y, w, h), 0, 0, w, h, GraphicsUnit.Pixel, dimIA);
            }

            // Indice del cuadro de titileo segun el reloj (rota ~5 veces/seg). Los armonicos son ENTEROS, asi
            // que el set entero cicla y el titileo se repite suave y continuo, sin recalcular nada por cuadro.
            private int CurrentStarLayerIndex()
            {
                if (layers == null || layers.Length == 0) return -1;
                int idx = (Environment.TickCount / 200) % layers.Length;
                if (idx < 0) idx += layers.Length;
                return idx;
            }

            // Vista de discos (VENTANA del campo COMPARTIDO de pantalla completa): cielo cacheado + estrellas
            // titilantes + LATIDO con la musica. El cielo y las estrellas salen del MISMO bitmap de pantalla
            // completa que pinta el fondo del form, corridos por (SkyOffsetX/Y): por eso las estrellas cruzan
            // los bordes de las secciones sin costura. El latido y las fugaces solo en repintado COMPLETO (en el
            // parcial de la casilla del cubo se reusa la capa: no relatir ni relanzar fugaces por una franja).
            // pLo/pMid/pHi = pulso de graves/medios/agudos (0..1). Cada estrella late con SU banda (ver DrawBeatFlare).
            public void DrawReactive(Graphics g, int w, int h, Color skyTop, Color skyBottom, float pLo, float pMid, float pHi)
            {
                if (w < 4 || h < 4) return;
                EnsureSkyCache(w, h, skyTop, skyBottom);
                EnsureLayers(w, h);
                RectangleF clip = g.ClipBounds;
                bool full = (clip.Width >= w - 1 && clip.Height >= h - 1);   // repintado COMPLETO (no la casilla del cubo)
                BlitSkyAndStars(g, w, h, full);
                float pMax = Math.Max(pLo, Math.Max(pMid, pHi));
                if (full && pMax * Dim > 0.04f) DrawBeatFlare(g, w, h, pLo * Dim, pMid * Dim, pHi * Dim);   // latido por banda (atenuado al navegar)
                if (full) DrawShoots(g, w, h);
            }

            // LATIDO con la musica: a las estrellas medianas/grandes (las que llevan glow) les estampa un glow
            // EXTRA que crece con la energia del audio. Es barato: ~40 sprites pre-horneados, no recalculo ni
            // pasada de pantalla completa. Del MISMO color (sprite por color). Solo en repintado completo. Las
            // estrellas se posicionan en coordenadas de PANTALLA COMPLETA menos el offset de la ventana; las que
            // caen fuera se saltan (el resto las recorta el clip del pintor).
            // Cada estrella late con SU banda (grandes=graves, medianas=medios/agudos): recibe los 3 pulsos y usa el
            // de su Band. Las estrellas cuya banda NO esta sonando no reciben halo -> se VE la separacion de beats.
            private void DrawBeatFlare(Graphics g, int w, int h, float pLo, float pMid, float pHi)
            {
                if (stars == null) return;
                int FW = FullW(w), FH = FullH(h), offX = SkyOffsetX, offY = SkyOffsetY;
                for (int i = 0; i < stars.Length; i++)
                {
                    Star s = stars[i];
                    if (s.Kind == 0) continue;   // los puntos no reaccionan
                    float pulse = (s.Band == 0) ? pLo : (s.Band == 1) ? pMid : pHi;   // el pulso de SU frecuencia
                    if (pulse <= 0.02f) continue;   // su banda no suena ahora: sin halo (asi se nota la separacion)
                    int gd = s.Size * 2 + 4 + (int)(pulse * 16f);   // el halo crece con el golpe de su banda
                    int px = (int)(s.X * FW) - offX, py = (int)(s.Y * FH) - offY;
                    if (px + gd < 0 || px - gd > w || py + gd < 0 || py - gd > h) continue;   // fuera de la ventana: se salta
                    int cr, cg, cb;
                    if (s.Tint > 0f) { cr = 255; cg = 255; cb = 255; } else { cr = s.R; cg = s.G; cb = s.B; }
                    Bitmap gb = GlowFor(cr, cg, cb);
                    if (gb == null) continue;
                    g.DrawImage(gb, new Rectangle(px - gd / 2, py - gd / 2, gd, gd));
                }
            }

            // Hornea los TwK cuadros de titileo UNA vez (al arrancar / al cambiar de tamano de PANTALLA). Se hornean
            // al tamano de la pantalla COMPLETA (FullW x FullH), no al de la ventana: asi un solo juego de capas
            // sirve a todas las secciones (fondo del form, rejilla, interior del disco) y cada una copia su ventana.
            // El patron es fijo; lo unico que cambia entre cuadros es el brillo de cada estrella -> al rotarlos titila.
            private void EnsureLayers(int w, int h)
            {
                int FW = FullW(w), FH = FullH(h);
                Ensure(FW, FH);
                if (layers != null && lw == FW && lh == FH && lBucket == sBucket) return;
                if (layers != null) { for (int k = 0; k < layers.Length; k++) if (layers[k] != null) layers[k].Dispose(); }
                layers = new Bitmap[TwK];
                lw = FW; lh = FH; lBucket = sBucket;
                EnsureGlow();
                for (int j = 0; j < TwK; j++) layers[j] = BakeStarFrame(FW, FH, j);
            }

            // Un cuadro de titileo: el patron con el brillo de cada estrella en la fase j (armonicos enteros
            // -> el set cicla exacto). Solo se ejecuta al hornear, no por cuadro, asi que puede ser detallado.
            private Bitmap BakeStarFrame(int w, int h, int j)
            {
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics lg = Graphics.FromImage(bmp))
                using (SolidBrush b = new SolidBrush(Color.White))
                {
                    lg.Clear(Color.Transparent);
                    lg.InterpolationMode = InterpolationMode.Bilinear;
                    lg.SmoothingMode = SmoothingMode.AntiAlias;
                    double ph = 2.0 * Math.PI * j / TwK;   // fase de este cuadro
                    for (int i = 0; i < stars.Length; i++)
                    {
                        Star s = stars[i];
                        int harm = 1 + (i % 2);   // armonico ENTERO (1 o 2: bien muestreado con TwK=8) -> el set cicla exacto
                        double tw = 0.62 + 0.38 * (0.5 + 0.5 * Math.Sin(ph * harm + s.Off));   // titileo 0.62..1.00
                        int a = (int)(tw * 255); if (a > 255) a = 255; if (a < 0) a = 0;
                        int px = (int)(s.X * w), py = (int)(s.Y * h);
                        int cr2, cg2, cb2;
                        if (s.Tint > 0f) { cr2 = 255; cg2 = 255; cb2 = 255; }   // tenidas -> blancas
                        else { cr2 = s.R; cg2 = s.G; cb2 = s.B; }
                        b.Color = Color.FromArgb(a, cr2, cg2, cb2);
                        if (s.Kind != 0)
                        {
                            int gd = s.Size * 3 + 6;
                            Bitmap gb = GlowFor(cr2, cg2, cb2);   // glow del MISMO COLOR (sprite pre-horneado)
                            if (gb != null)
                            {
                                Rectangle gr = new Rectangle(px - gd / 2, py - gd / 2, gd, gd);
                                if (a >= 250) lg.DrawImage(gb, gr);
                                else using (ImageAttributes ia = new ImageAttributes())   // el glow tambien titila (matriz solo al hornear)
                                {
                                    ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = (float)tw;
                                    ia.SetColorMatrix(cm);
                                    lg.DrawImage(gb, gr, 0, 0, gb.Width, gb.Height, GraphicsUnit.Pixel, ia);
                                }
                            }
                            DrawSparkle(lg, b, px, py, s.Kind == 2 ? s.Size + 6 : s.Size + 2);
                        }
                        else { float ds = s.Size < 2 ? 2 : s.Size; lg.FillEllipse(b, px - ds / 2f, py - ds / 2f, ds, ds); }
                    }
                }
                return bmp;
            }

            private void DrawShoots(Graphics g, int w, int h)
            {
                int now = Environment.TickCount;
                float dt = (lastTick == 0) ? 0f : (now - lastTick) / 1000f;
                if (dt < 0f) dt = 0f; if (dt > 0.12f) dt = 0.12f;   // acotar saltos
                lastTick = now;

                if (now - nextShoot >= 0)   // RESTA, no >=: sobrevive el desbordamiento de TickCount (~25 dias encendida)
                {
                    nextShoot = now + 7000 + rnd.Next(9000);   // una fugaz cada 7-16s
                    for (int i = 0; i < shoots.Length; i++)
                        if (!shoots[i].On)
                        {
                            Shoot ns = new Shoot();
                            ns.On = true;
                            ns.X = (float)(rnd.NextDouble() * w * 0.55);
                            ns.Y = (float)(rnd.NextDouble() * h * 0.45);
                            float spd = (w + h) * 0.55f;                 // px por segundo
                            ns.VX = spd; ns.VY = spd * (0.35f + (float)rnd.NextDouble() * 0.30f);
                            ns.MaxLife = 1.0f + (float)rnd.NextDouble() * 0.5f; ns.Life = ns.MaxLife;
                            shoots[i] = ns;
                            break;
                        }
                }
                for (int i = 0; i < shoots.Length; i++)
                {
                    if (!shoots[i].On) continue;
                    Shoot s = shoots[i];
                    float k = s.Life / s.MaxLife; if (k < 0f) k = 0f;
                    int la = (int)(230 * k * Dim);   // atenuada mientras se navega con musica
                    float tx = s.X - s.VX * 0.10f, ty = s.Y - s.VY * 0.10f;   // estela corta
                    sp.Color = Color.FromArgb(la, 255, 255, 255);
                    g.DrawLine(sp, tx, ty, s.X, s.Y);
                    sb.Color = Color.FromArgb(Math.Min(255, la + 25), 255, 255, 255);
                    g.FillRectangle(sb, (int)s.X - 1, (int)s.Y - 1, 3, 3);
                    s.X += s.VX * dt; s.Y += s.VY * dt; s.Life -= dt;
                    if (s.Life <= 0f || s.X > w + 60 || s.Y > h + 60) s.On = false;
                    shoots[i] = s;
                }
            }
        }

        // Modo atraccion: pantalla negra con estrellas titilantes y un cubo gigante girando que cicla
        // portadas de la coleccion (con fundido). Aparece en reposo para llamar al cliente.
        private class AttractView : Control
        {
            public double Phase = 0;
            public double Angle = 0;
            public List<string> CycleCovers = new List<string>();
            public Func<string, Image> GetCoverByPath;
            public int SongCount { get; set; }   // propiedad (no campo): un campo de una clase por-referencia (Control) dispara la advertencia CS1690 al usarlo

            private SolidBrush sb;
            private Bitmap blendBmp;
            private Font fBig, fSmall; private StringFormat sfCen;

            private Starfield attractStars = new Starfield();

            public AttractView()
            {
                attractStars.Varied = true;   // estrellas multicolor en el modo atraccion
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            private void EnsureRes()
            {
                if (sb == null) sb = new SolidBrush(Color.White);
                if (fBig == null) fBig = new Font(FontFamily.GenericSansSerif, 24f, FontStyle.Bold);
                if (fSmall == null) fSmall = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold);
                if (sfCen == null) { sfCen = new StringFormat(); sfCen.Alignment = StringAlignment.Center; sfCen.LineAlignment = StringAlignment.Center; }
            }

            private Image BlendInto(Image a, Image b, float f)
            {
                if (blendBmp == null) blendBmp = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(blendBmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) g.DrawImage(a, dst);
                    if (b != null && f > 0f)
                    {
                        ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = f;
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(b, dst, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                }
                return blendBmp;
            }

            // Par de portadas a fundir + factor (0..1) segun la fase. Lo usa el CPU (BlendInto) y tambien la
            // GPU (que hace el fundido como en los discos TOP, sin generar el bitmap mezclado).
            public bool GetCoverPair(out Image a, out Image b, out float f)
            {
                a = null; b = null; f = 0f;
                if (CycleCovers == null || CycleCovers.Count == 0 || GetCoverByPath == null) return false;
                int m = CycleCovers.Count;
                double cyc = 4.2, hold = 3.0;
                double total = cyc * m;
                double pos = Phase - Math.Floor(Phase / total) * total;
                int idx = (int)(pos / cyc) % m;
                double within = pos - idx * cyc;
                if (within > hold) f = (float)((within - hold) / (cyc - hold));
                if (f < 0f) f = 0f; else if (f > 1f) f = 1f;
                a = GetCoverByPath(CycleCovers[idx]);
                b = (m > 1) ? GetCoverByPath(CycleCovers[(idx + 1) % m]) : null;
                return a != null || b != null;
            }

            private Image CurrentCover()
            {
                Image a, b; float f;
                if (!GetCoverPair(out a, out b, out f)) return null;
                if (f <= 0.002f || b == null) return a;
                if (a == null) return b;
                return BlendInto(a, b, f);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                EnsureRes();
                attractStars.SkyOffsetY = 0; attractStars.SkyFullH = h;
                attractStars.Draw(g, w, h, Color.FromArgb(255, 6, 8, 26), Color.FromArgb(255, 18, 20, 54), Color.White, Phase, true, 0f);   // cielo nocturno en degradado (atraccion: sin musica)

                // cubo gigante central rotando + ciclando portadas (con fundido)
                int side = (int)(Math.Min(w, h) * 0.40);
                if (side < 80) side = 80;
                Rectangle area = new Rectangle(w / 2 - side / 2, (h - side) / 2 - 30, side, side);
                g.InterpolationMode = InterpolationMode.Bilinear;
                Cube.Draw(g, area, CurrentCover(), Angle);

                // textos
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                string line1 = (SongCount > 0) ? (SongCount.ToString("#,0") + " canciones") : "Tu rocola";
                sb.Color = Color.White;
                g.DrawString(line1, fBig, sb, new RectangleF(0, area.Bottom + 18, w, 36), sfCen);
                double pz = 0.5 + 0.5 * Math.Sin(Phase * 3.0);
                sb.Color = Color.FromArgb((int)(150 + 105 * pz), 255, 220, 120);
                g.DrawString("toca un boton para elegir", fSmall, sb, new RectangleF(0, area.Bottom + 58, w, 28), sfCen);
            }
        }

        private static class Cube
        {
            // OPTIMIZACION (render): estos 3 arreglos se asignaban en CADA llamada a Draw, y Draw se
            // llama varias veces por cuadro (cubo seleccionado, relevo, cubeBox, miniCube, buscador,
            // atraccion). Reutilizarlos via [ThreadStatic] elimina cientos de asignaciones/seg.
            [ThreadStatic] private static double[] exBuf;
            [ThreadStatic] private static double[] depthBuf;
            [ThreadStatic] private static int[] orderBuf;
            [ThreadStatic] private static SolidBrush faceBrush;   // pincel reusable (cara/oscurecido): antes asignaba 1-2 por cara y cuadro
            [ThreadStatic] private static Pen edgePen;            // lapiz de contorno reusable (antes 1 por cuadro)

            public static void Draw(Graphics g, Rectangle bounds, Image cover, double angle)
            {
                double cx = bounds.X + bounds.Width / 2.0;
                int y0 = bounds.Y;
                int H = bounds.Height;
                double R = bounds.Width / 2.0;
                double DEG = Math.PI / 180.0;

                double[] ex = exBuf ?? (exBuf = new double[4]);
                for (int k = 0; k < 4; k++) ex[k] = cx + R * Math.Cos(angle + 45 * DEG + k * 90 * DEG);
                double[] depth = depthBuf ?? (depthBuf = new double[4]);
                for (int k = 0; k < 4; k++) depth[k] = Math.Sin(angle + 90 * DEG + k * 90 * DEG);

                int[] order = orderBuf ?? (orderBuf = new int[4]);
                order[0] = 0; order[1] = 1; order[2] = 2; order[3] = 3;
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 3 - i; j++)
                        if (depth[order[j]] > depth[order[j + 1]]) { int tmp = order[j]; order[j] = order[j + 1]; order[j + 1] = tmp; }

                for (int oi = 0; oi < 4; oi++)
                {
                    int k = order[oi];
                    if (depth[k] <= 0.0) continue;
                    int left = (int)Math.Round(Math.Min(ex[k], ex[(k + 1) % 4]));
                    int right = (int)Math.Round(Math.Max(ex[k], ex[(k + 1) % 4]));
                    int w = right - left;
                    if (w < 1) continue;
                    Rectangle rect = new Rectangle(left, y0, w, H);
                    SolidBrush fb = faceBrush ?? (faceBrush = new SolidBrush(Color.Black));
                    if (cover != null) g.DrawImage(cover, rect);
                    else { fb.Color = Color.FromArgb(60, 70, 110); g.FillRectangle(fb, rect); }
                    int darken = (int)((1.0 - depth[k]) * 130);
                    if (darken > 0) { fb.Color = Color.FromArgb(darken, 0, 0, 0); g.FillRectangle(fb, rect); }
                }

                double minx = ex[0], maxx = ex[0];
                for (int k = 1; k < 4; k++) { if (ex[k] < minx) minx = ex[k]; if (ex[k] > maxx) maxx = ex[k]; }
                Pen ep = edgePen ?? (edgePen = new Pen(Color.FromArgb(120, 0, 0, 0), 1));
                g.DrawRectangle(ep, (int)Math.Round(minx), y0, (int)Math.Round(maxx - minx), H - 1);
            }
        }

        // Estado del "brillo" de los simbolos de la barra de instrucciones al pulsar sus botones (halo que
        // decae). UN BRILLO POR SIMBOLO: en los COMBOS (p.ej. REGRESAR mantenido + PONER) se encienden VARIOS
        // a la vez y cada uno decae por su cuenta. (Antes habia UN solo brillo global: el nuevo pisaba al
        // anterior y en los combos solo se veia uno.) El color se fija al dispararlo (asi Mover puede ser
        // ROJO en arriba/abajo o AMARILLO en izq/der). Lo comparten HelpBar (4 simbolos) y el buscador (3).
        private sealed class InstrFlash
        {
            private const int Ms = 420;
            private readonly int[] tick = new int[4];      // momento del disparo por simbolo (0 = nunca disparado)
            private readonly Color[] cols = new Color[4];
            public void Fire(int w, Color c)
            {
                if (w < 0 || w >= tick.Length) return;
                int t = Environment.TickCount;
                tick[w] = (t == 0) ? 1 : t;   // 0 esta reservado como "nunca"
                cols[w] = c;
            }
            public float Level(int w)
            {
                if (w < 0 || w >= tick.Length || tick[w] == 0) return 0f;
                int dt = Environment.TickCount - tick[w];
                if (dt < 0 || dt > Ms) return 0f;
                return 1f - (float)dt / Ms;
            }
            public Color Col(int w) { return (w >= 0 && w < cols.Length) ? cols[w] : Color.White; }
            public int Sig()   // cambia mientras dure ALGUN brillo (para animarlo por CPU); 0 = todo apagado
            {
                int now = Environment.TickCount, s = 0;
                for (int w = 0; w < tick.Length; w++)
                {
                    if (tick[w] == 0) continue;
                    int dt = now - tick[w];
                    if (dt < 0 || dt > Ms) continue;
                    s = s * 37 + (w + 1) * 1000 + dt / 40;
                }
                return s;
            }
        }

        // Halo suave (radial) centrado en un simbolo de instrucciones; intensidad segun 'level' (0..1).
        private static void GlowAround(Graphics g, float leftX, float cy, float symW, float level, Color c)
        {
            if (level <= 0.02f) return;
            float cx = leftX + symW / 2f, r = symW * 0.9f + 6f;
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterPoint = new PointF(cx, cy);
                    pb.CenterColor = Color.FromArgb((int)(level * 215), c);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillPath(pb, gp);
                }
            }
        }

        // Ayudantes para dibujar los controles fisicos como FORMAS (circulo rojo/verde, cruceta).
        // GDI reusables para las formas de instrucciones (se llaman muchas veces por cuadro): pincel al que se
        // le muta el color, contorno blanco fijo y un buffer de 3 puntos. Evita asignar objetos GDI por forma.
        [ThreadStatic] private static SolidBrush ctrlB;
        [ThreadStatic] private static Pen ctrlOutline;
        [ThreadStatic] private static PointF[] ctrlTri3;
        private static SolidBrush CB(Color c) { if (ctrlB == null) ctrlB = new SolidBrush(c); else ctrlB.Color = c; return ctrlB; }
        private static Pen COut() { return ctrlOutline ?? (ctrlOutline = new Pen(Color.FromArgb(130, 255, 255, 255), 1.4f)); }

        private static float CtrlDot(Graphics g, float x, int cy, int d, Color c)
        {
            g.FillEllipse(CB(c), x, cy - d / 2f, d, d);
            g.DrawEllipse(COut(), x, cy - d / 2f, d, d);
            return x + d;
        }
        private static float CtrlTxt(Graphics g, Font f, string s, float x, int cy, Color c)
        {
            SizeF sz = g.MeasureString(s, f);
            g.DrawString(s, f, CB(c), x, cy - sz.Height / 2f);
            return x + sz.Width;
        }
        private static void CtrlTri(Graphics g, Color c, PointF a, PointF b, PointF d)
        {
            PointF[] p = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            p[0] = a; p[1] = b; p[2] = d;
            g.FillPolygon(CB(c), p);
        }
        private static float CtrlPad(Graphics g, float x, int cy, int sz)   // cruceta: arriba/abajo rojo, izq/der amarillo
        {
            Color red = Color.FromArgb(228, 72, 72), yellow = Color.FromArgb(242, 206, 72);
            float cx = x + sz / 2f, t = sz * 0.34f, w = t / 1.4f;
            CtrlTri(g, red, new PointF(cx, cy - sz / 2f), new PointF(cx - w, cy - sz / 2f + t), new PointF(cx + w, cy - sz / 2f + t));
            CtrlTri(g, red, new PointF(cx, cy + sz / 2f), new PointF(cx - w, cy + sz / 2f - t), new PointF(cx + w, cy + sz / 2f - t));
            CtrlTri(g, yellow, new PointF(x, cy), new PointF(x + t, cy - w), new PointF(x + t, cy + w));
            CtrlTri(g, yellow, new PointF(x + sz, cy), new PointF(x + sz - t, cy - w), new PointF(x + sz - t, cy + w));
            return x + sz;
        }
        // Icono del BOTON IZQUIERDA (flecha amarilla, mismo color que la cruceta horizontal)
        private static float CtrlDirLeft(Graphics g, float x, int cy, int sz, Color c)
        {
            PointF[] pts = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            pts[0] = new PointF(x, cy); pts[1] = new PointF(x + sz, cy - sz * 0.5f); pts[2] = new PointF(x + sz, cy + sz * 0.5f);
            g.FillPolygon(CB(c), pts); g.DrawPolygon(COut(), pts);
            return x + sz;
        }
        // Icono del BOTON DERECHA (flecha amarilla, espejo de la izquierda)
        private static float CtrlDirRight(Graphics g, float x, int cy, int sz, Color c)
        {
            PointF[] pts = ctrlTri3 ?? (ctrlTri3 = new PointF[3]);
            pts[0] = new PointF(x + sz, cy); pts[1] = new PointF(x, cy - sz * 0.5f); pts[2] = new PointF(x, cy + sz * 0.5f);
            g.FillPolygon(CB(c), pts); g.DrawPolygon(COut(), pts);
            return x + sz;
        }

        // Barra de ayuda: instrucciones con FORMAS y COLORES de los botones fisicos. Sin nombres de teclas.
        private class HelpBar : Control
        {
            public const int FBuscar = 0, FMover = 1, FEntrar = 2, FVolver = 3;   // simbolo a resaltar
            private static readonly Color RedC = Color.FromArgb(228, 72, 72), GreenC = Color.FromArgb(72, 212, 112), YellowC = Color.FromArgb(242, 206, 72);
            private readonly InstrFlash flash = new InstrFlash();

            public HelpBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            // El usuario pulso un boton: resalta (brillo que decae) el simbolo correspondiente.
            public void Flash(int which) { flash.Fire(which, which == FEntrar ? GreenC : which == FVolver ? RedC : Color.White); }
            public void FlashMove(bool vertical) { flash.Fire(FMover, vertical ? RedC : YellowC); }   // arriba/abajo ROJO, izq/der AMARILLO
            public int FlashSig() { return flash.Sig(); }
            private Font hbFont;   // fuente cacheada: la barra se redibuja ~13fps (glow) -> no crear una por cuadro

            protected override void OnPaint(PaintEventArgs e) { e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; RenderContent(e.Graphics, false); }
            // Centro/radio de los 4 halos (Buscar/Mover/Entrar/Volver), guardados al maquetar. En GPU el destello
            // NO se hornea en la textura: lo dibuja la GPU como quad (GetFlashGlow) -> pulsar un boton (p.ej. al
            // PAGINAR rapido) ya no re-hornea ni re-sube la textura de UI ~10 veces por tecla.
            private readonly float[] fgX = new float[4], fgY = new float[4], fgW = new float[4];
            private void StoreGlow(int k, float leftX, float cy, float symW) { fgX[k] = leftX + symW / 2f; fgY[k] = cy; fgW[k] = symW * 0.9f + 6f; }
            public bool GetFlashGlow(int k, out float cx, out float cy, out float r, out int argb)
            {
                cx = cy = r = 0f; argb = 0;
                if (k < 0 || k > 3) return false;
                float lvl = flash.Level(k);
                if (lvl <= 0.02f) return false;
                cx = fgX[k]; cy = fgY[k]; r = fgW[k];
                Color c = flash.Col(k);
                argb = Color.FromArgb((int)(lvl * 215f), c.R, c.G, c.B).ToArgb();   // alfa = brillo del destello; rgb = su color
                return true;
            }
            public void RenderContent(Graphics g) { RenderContent(g, false); }
            // Dibuja la leyenda en un Graphics cualquiera (el control, o la textura de UI del modo GPU). El
            // llamador fija el TextRenderingHint: ClearType para pantalla; AntiAlias (gris) para la textura
            // transparente del modo GPU (ClearType deja flecos de color sobre fondo transparente).
            // forGpu=true: NO hornea el halo del boton pulsado (lo pone la GPU via GetFlashGlow); solo guarda posiciones.
            public void RenderContent(Graphics g, bool forGpu)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int cy = Height / 2;
                Color txt = Color.FromArgb(228, 234, 250);
                int d = Math.Min(20, Height - 8); if (d < 12) d = 12;
                float gap = 7, grp = 20;
                if (hbFont == null) hbFont = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold);
                Font f = hbFont;
                float x = 2;
                StoreGlow(FBuscar, x, cy, d * 2 + 4);
                if (!forGpu) GlowAround(g, x, cy, d * 2 + 4, flash.Level(FBuscar), flash.Col(FBuscar));   // BUSCAR = 2 puntos
                x = CtrlDot(g, x, cy, d, RedC) + 4; x = CtrlDot(g, x, cy, d, GreenC);
                x = CtrlTxt(g, f, "Buscar", x + gap, cy, txt) + grp;
                StoreGlow(FMover, x, cy, d + 4);
                if (!forGpu) GlowAround(g, x, cy, d + 4, flash.Level(FMover), flash.Col(FMover));        // MOVER = cruceta (color segun direccion)
                x = CtrlPad(g, x, cy, d + 4);
                x = CtrlTxt(g, f, "Mover", x + gap, cy, txt) + grp;
                StoreGlow(FEntrar, x, cy, d);
                if (!forGpu) GlowAround(g, x, cy, d, flash.Level(FEntrar), flash.Col(FEntrar));           // ENTRAR/PONER = punto verde
                x = CtrlDot(g, x, cy, d, GreenC);
                x = CtrlTxt(g, f, "Entrar / Poner", x + gap, cy, txt) + grp;
                StoreGlow(FVolver, x, cy, d);
                if (!forGpu) GlowAround(g, x, cy, d, flash.Level(FVolver), flash.Col(FVolver));           // VOLVER = punto rojo
                x = CtrlDot(g, x, cy, d, RedC);
                x = CtrlTxt(g, f, "Volver", x + gap, cy, txt);
            }
        }

        // ==========  VISTA DE DISCOS (fondo animado estilo PS2 + cubos)  ==========
        // Panel con fondo estrellado (reactivo al genero), para el modo "dentro del disco"
        private class BgPanel : Panel
        {
            public Color Accent = DefaultAccent;
            public float BgDim = 1f;   // atenuacion del starfield mientras se navega con musica (el form la anima; igual que la rejilla)
            public BgPanel()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }
            protected override void OnPaintBackground(PaintEventArgs e) { /* todo en OnPaint */ }
            public Starfield Sky;   // COMPARTIDO con el fondo del form: este panel pinta su VENTANA del mismo campo (una sola pieza)
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width, h = Height;
                if (w < 4 || h < 4) return;

                // VENTANA del campo COMPARTIDO de pantalla completa: cielo + estrellas salen del mismo bitmap que
                // pinta el fondo del form, corridos por (Left, Top). Cacheado: solo se copia (rapido) y va fluido,
                // y las estrellas cruzan el borde con las franjas de arriba/abajo sin cortarse.
                if (Sky == null) return;
                Sky.SkyOffsetX = this.Left; Sky.SkyOffsetY = this.Top;
                Sky.SkyFullW = (this.Parent != null) ? this.Parent.ClientSize.Width : w;
                Sky.SkyFullH = (this.Parent != null) ? this.Parent.ClientSize.Height : h;
                Sky.SkyMid = SkyColorMid(Accent);
                Sky.Dim = BgDim;   // en sincronia con la rejilla y las franjas (una sola pieza)
                Sky.DrawCached(g, w, h, SkyColor(Accent), SkyColor2(Accent));   // cielo en degradado CONTINUO; un cubo transparente girando encima
            }

        }

        private class AlbumView : Control
        {
            public List<Album> Page = new List<Album>();
            public int Selected = 0;
            public double SelAngle = 0;
            public double Phase = 0;
            public Color BgAccent = DefaultAccent;
            public Func<Album, Image> GetCover;
            public Func<string, Image> GetCoverByPath;   // discos TOP: caratula por ruta (ciclado)
            public Action<int> OnActivate;
#if ROCOLA_GPU
            public bool GpuCovered;   // el panel GPU (pantalla completa, opaco) tapa esta vista: no malgastes su OnPaint (rejilla+estrellas) por CPU
#endif
            public float BgDim = 1f;  // atenuacion del starfield mientras se navega con musica (el form la anima)

            private Bitmap cubeCache;
            private int cacheW = -1, cacheH = -1;
            private string cacheSig = null;
            // CACHE DE CELDAS: cada disco (portada estatica + etiqueta) se HORNEA a un bitmap UNA vez y luego se
            // COPIA (blit 1:1) en cubeCache al paginar. Antes cada pagina re-ESCALABA 18 portadas y re-MAQUETABA
            // 18 etiquetas (ajuste de linea) -> carisimo al paginar rapido. Ahora paginar es casi solo copias.
            private sealed class CellSprite { public Bitmap Bmp; public bool HasCover; }
            private readonly Dictionary<string, CellSprite> cellCache = new Dictionary<string, CellSprite>();
            private int cellCacheW = -1, cellCacheH = -1;   // tamano con el que se horneo (si cambia, se limpia todo)
            private const int CellCacheCap = 6 * PageSize;  // ~6 paginas de historial en memoria (acotado; hojear ida/vuelta cae dentro)
            private bool cubesPending;                       // (CPU) quedan celdas por hornear: se reparte en varios repintados (como en GPU) para no congelar en equipos lentos
            private const int CpuBakeBudget = 9;             // (CPU) celdas horneadas por repintado (media pagina): una pagina nueva se llena en 2 pasadas, sin congelon
#if ROCOLA_GPU
            // La rejilla por GPU ya NO usa una textura a pantalla completa (cubeCache): el panel pide las celdas
            // con BuildGpuGrid y dibuja cada disco como su propio quad. cubeCache queda solo para el render por CPU.
            public Bitmap ContourSpriteBmp { get { EnsureContourSprite(); return contourSprite; } }   // sprite del resalte (lo hornea si falta: el panel GPU lo lee aunque el OnPaint por CPU este suprimido)
            public Rectangle ContourScreenRect()                     // donde va el contorno (celda seleccionada)
            {
                if (contourSprite == null || Page == null || Selected < 0 || Selected >= Page.Count) return Rectangle.Empty;
                if (Sliding) return Rectangle.Empty;   // hojeando: nada seleccionado hasta que la pagina termine de entrar
                int w = Width, h = Height; if (w < 8 || h < 8) return Rectangle.Empty;
                int cw = w / Cols, ch = h / Rows; if (cw < 4 || ch < 4) return Rectangle.Empty;
                Rectangle area, lbl; Geometry(w, h, Selected, out area, out lbl);
                Rectangle fr = area; fr.Inflate(4, 4);
                return new Rectangle(fr.X - ContourMargin, fr.Y - ContourMargin, contourSprite.Width, contourSprite.Height);
            }

            // --- Cubos que GIRAN (seleccionado + relevo) para el modo GPU ---
            // En CPU se dibujan ENCIMA de los cubos estaticos en cada cuadro (Cube.Draw). En GPU le pasamos
            // al panel la celda, el angulo y la(s) portada(s): el panel sube la portada como textura UNA vez
            // (al cambiar) y dibuja el cubo girando como GEOMETRIA. Asi el giro casi no cuesta CPU.

            // Avanza el giro SIN repintar por CPU (el fondo y los cubos los pinta la GPU). Lo llama el
            // temporizador a 24fps cuando el fondo GPU esta activo, en vez de AnimTick (que invalida y repinta).
            public void StepSpinGpu() { StepSpin(); }

            // Arma el estado de los cubos que giran para que el panel GPU los dibuje (mismo aspecto que CPU).
            public GpuCubeState BuildCubeState()
            {
                GpuCubeState st = new GpuCubeState();
                int w = Width, h = Height;
                if (Page == null || w < 8 || h < 8) return st;
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4) return st;
                // HOJEANDO: la rejilla entera se desliza, pero el cubo que gira y el resalte se dibujan en su
                // sitio fijo. Si se pintaran, se verian clavados y girando mientras todo lo demas pasa de
                // largo. Durante el deslizamiento no hay nada seleccionado; el cubo aparece -plano y quieto,
                // porque el giro tampoco avanza (ver StepSpin)- justo cuando la pagina nueva termina de entrar.
                if (Sliding) return st;
                if (Selected >= 0 && Selected < Page.Count)
                {
                    Rectangle a, l; Geometry(w, h, Selected, out a, out l);
                    Image ca, cb; float f; SelectedCovers(out ca, out cb, out f);
                    st.Sel.Has = true; st.Sel.X = a.X; st.Sel.Y = a.Y; st.Sel.W = a.Width; st.Sel.H = a.Height;
                    st.Sel.Angle = SelAngle; st.Sel.CoverA = ca as Bitmap; st.Sel.CoverB = cb as Bitmap; st.Sel.Fade = f;
                }
                if (prevIdx >= 0 && prevIdx != Selected && prevIdx < Page.Count)   // RELEVO: el que dejamos, girando y decayendo
                {
                    Rectangle a2, l2; Geometry(w, h, prevIdx, out a2, out l2);
                    st.Relay.Has = true; st.Relay.X = a2.X; st.Relay.Y = a2.Y; st.Relay.W = a2.Width; st.Relay.H = a2.Height;
                    st.Relay.Angle = prevAngle; st.Relay.CoverA = ((GetCover != null) ? GetCover(Page[prevIdx]) : null) as Bitmap;
                }
                return st;
            }

            private const int GridBakeBudget = 6;   // celdas NUEVAS horneadas por cuadro (barrer paginas nuevas sin bajar fps)
            // HOJEO: la pagina nueva ENTRA deslizandose por el lado hacia el que vas. No cuesta un solo dibujo
            // mas -las casillas ya se pintan cada cuadro-, solo cambian de sitio; por eso es practicamente gratis.
            // El avance se calcula EN VIVO con el reloj (no con el temporizador de 24): se ve fluido a los fps
            // que de el equipo, igual que las demas animaciones de la rocola.
            public const int SlideMs = 230;
            private int slideTick = -1; private float slideFrom;
            public void StartSlide(int dir, int ancho)   // dir: +1 la nueva entra por la derecha, -1 por la izquierda
            {
                if (ancho < 8) { slideTick = -1; return; }
                slideFrom = dir * (float)ancho; slideTick = Environment.TickCount;
            }
            public bool Sliding { get { return slideTick >= 0 && Environment.TickCount - slideTick < SlideMs; } }
            // CURVA del hojeo (funcion pura, para poder comprobarla): devuelve cuanto FALTA por recorrer
            // (1 = recien empieza, 0 = ya llego) y la opacidad de entrada.
            internal static float CurvaHojeo(int dt, out float alpha)
            {
                alpha = 1f;
                if (dt < 0 || dt >= SlideMs) return 0f;
                float t = dt / (float)SlideMs;
                float e = 1f - (1f - t) * (1f - t) * (1f - t);   // frenado suave al llegar (cubico)
                alpha = t < 0.45f ? (t / 0.45f) : 1f;
                return 1f - e;
            }
            // Devuelve el desplazamiento de este cuadro y, de paso, el desvanecido de entrada.
            private float SlideNow(out float alpha)
            {
                alpha = 1f;
                if (slideTick < 0) return 0f;
                int dt = Environment.TickCount - slideTick;
                if (dt < 0 || dt >= SlideMs) { slideTick = -1; return 0f; }
                return slideFrom * CurvaHojeo(dt, out alpha);
            }

            // PRE-HORNEADO de la pagina de al lado: mientras nadie toca nada, se van dejando listas las casillas
            // que se veran al pasar de pagina. Asi el hojeo se ve fluido LA PRIMERA VEZ y no solo al volver.
            public List<Album> Ahead;
            private int aheadPos;
            public void SetAhead(List<Album> lista) { Ahead = lista; aheadPos = 0; }
            public void BakeAhead()
            {
                if (Ahead == null || aheadPos >= Ahead.Count) return;
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4 || cellCacheW != w || cellCacheH != h) return;   // otro tamano: ya se rehorneara solo
                int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                int budget = 2;   // dos casillas por vuelta (~24 por segundo): invisible y en dos segundos hay pagina
                int t0 = Environment.TickCount;
                while (aheadPos < Ahead.Count && budget > 0 && Environment.TickCount - t0 < 4)
                {
                    Album a = Ahead[aheadPos++];
                    PeekOrBakeCell(a, cw, side, labelH, gap, (GetCover != null) ? GetCover(a) : null, ref budget);
                }
            }

            // Llena 'buf' con las celdas (disco estatico + etiqueta) de la pagina actual, en coords de PANTALLA (el
            // panel GPU es a pantalla completa). Cada celda trae su bitmap ya horneado para que el panel lo suba como
            // su PROPIA textura (cacheada por Folder). Se llama cada cuadro: para celdas cacheadas es casi gratis; las
            // nuevas se hornean con presupuesto (el resto sale null y el panel las salta hasta el proximo cuadro).
            public int BuildGpuGrid(GpuGridCell[] buf)
            {
                if (Page == null || buf == null) return 0;
                int w = Width, h = Height;
                if (w < 8 || h < 8) return 0;
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4) return 0;
                if (cellCacheW != w || cellCacheH != h) { ClearCellCache(); cellCacheW = w; cellCacheH = h; }   // tamano nuevo: rehornear
                else if (cellCache.Count > CellCacheCap) ClearCellCache();                                      // limite de memoria (raro)
                int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                float alpha; float dx = SlideNow(out alpha);   // hojeo: desplazamiento y desvanecido de ESTE cuadro
                int ox = this.Left, oy = this.Top;   // coords de albumView -> pantalla (mismo criterio que los cubos que giran)
                int budget = GridBakeBudget;
                int bakeT0 = Environment.TickCount;   // tope de TIEMPO ademas del de cantidad: en el CPU lento de la
                int n = 0;                            // rocola 6 horneadas podian pasar de 16ms -> cuadro perdido al paginar
                for (int i = 0; i < Page.Count && i < PageSize && n < buf.Length; i++)
                {
                    Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                    Album a = Page[i];
                    buf[n].Key = CellKey(a);
                    buf[n].Bmp = PeekOrBakeCell(a, cw, side, labelH, gap, (GetCover != null) ? GetCover(a) : null, ref budget);
                    buf[n].Slot = i;
                    buf[n].X = ox + (i % Cols) * cw + dx; buf[n].Y = oy + area.Y; buf[n].W = cw; buf[n].H = side + gap + labelH;
                    buf[n].A = alpha;
                    n++;
                    if (budget < GridBakeBudget && budget > 0 && Environment.TickCount - bakeT0 >= 6) budget = 0;   // ~6ms horneando: el resto al proximo cuadro
                }
                return n;
            }
#endif
            private Pen cPenGlow, cPenMid, cPenLine;   // contorno del disco seleccionado (cacheadas; antes se creaban por cuadro)
            private Bitmap contourSprite; private Size contourSpriteSize = Size.Empty;   // contorno HORNEADO (3 trazos AA -> 1 copia); todas las casillas miden igual
            private Font lblFont; private SolidBrush lblBrush; private StringFormat lblSF;       // recursos de etiquetas (reusables)
            private Bitmap blendBmp;   // para el fundido suave de portadas del cubo TOP
            public Starfield Sky;   // COMPARTIDO con el fondo del form: esta vista pinta su VENTANA del mismo campo (una sola pieza)
            public float PulseLo = 0f, PulseMid = 0f, PulseHi = 0f;   // pulso 0..1 por banda (graves/medios/agudos): cada estrella late con SU frecuencia

            private Image BlendInto(Image a, Image b, float f)
            {
                // 192 basta: el cubo se dibuja a ~154px; antes 320 gastaba el doble de relleno en cada fundido.
                if (blendBmp == null) blendBmp = new Bitmap(192, 192, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(blendBmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) g.DrawImage(a, dst);
                    if (b != null && f > 0f)
                    {
                        ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = f;
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(b, dst, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                }
                return blendBmp;
            }

            // Portadas a fundir del disco SELECCIONADO: para discos TOP cicla sus portadas (ca actual, cb
            // siguiente, f = mezcla 0..1); para discos normales devuelve la suya en ca (cb=null, f=0). La
            // usan OnPaint (CPU, que las funde a una imagen) y el modo GPU (que funde en dos pasadas).
            private void SelectedCovers(out Image ca, out Image cb, out float f)
            {
                ca = null; cb = null; f = 0f;
                if (Page == null || Selected < 0 || Selected >= Page.Count) return;
                Album selA = Page[Selected];
                if (selA != null && selA.CycleCovers != null && selA.CycleCovers.Count > 0 && GetCoverByPath != null)
                {
                    int m = selA.CycleCovers.Count;
                    double cyc = 3.8, hold = 2.6;
                    double total = cyc * m;
                    double pos = Phase - Math.Floor(Phase / total) * total;
                    int idx = (int)(pos / cyc) % m;
                    double within = pos - idx * cyc;
                    float ff = 0f;
                    if (within > hold) ff = (float)((within - hold) / (cyc - hold));
                    if (ff < 0f) ff = 0f; else if (ff > 1f) ff = 1f;
                    ca = GetCoverByPath(selA.CycleCovers[idx]);
                    cb = (m > 1) ? GetCoverByPath(selA.CycleCovers[(idx + 1) % m]) : null;
                    if (ca == null && GetCover != null) ca = GetCover(selA);   // mientras carga
                    f = ff;
                }
                else ca = (GetCover != null) ? GetCover(selA) : null;
            }

            // Caratula del disco SELECCIONADO ya FUNDIDA a una sola imagen (la usa el render por CPU).
            private Image SelectedCover()
            {
                Image ca, cb; float f; SelectedCovers(out ca, out cb, out f);
                return (cb == null || f <= 0.002f) ? ca : BlendInto(ca, cb, f);
            }

            private void EnsureLabelRes()
            {
                if (lblFont == null) lblFont = new Font(this.Font.FontFamily, 10f, FontStyle.Bold);
                if (lblBrush == null) lblBrush = new SolidBrush(Color.White);
                if (lblSF == null)
                {
                    lblSF = new StringFormat();
                    lblSF.Alignment = StringAlignment.Center; lblSF.LineAlignment = StringAlignment.Center;
                    lblSF.Trimming = StringTrimming.EllipsisCharacter;
                    lblSF.FormatFlags = StringFormatFlags.LineLimit;
                }
            }

            // Avanza el giro e invalida lo MINIMO: en cuadros intermedios solo la casilla del cubo que
            // gira (y la del relevo si lo hay); el cuadro completo (fondo+brillos) va 1 de cada 3.
            public void AnimTick(bool fullFrame)
            {
                Rectangle d = SpinRect();
                StepSpin();
                Rectangle d2 = SpinRect();
                if (fullFrame) { Invalidate(); return; }
                if (d2 != Rectangle.Empty) d = (d == Rectangle.Empty) ? d2 : Rectangle.Union(d, d2);
                if (d != Rectangle.Empty) Invalidate(d);
            }

            // Rectangulo que abarca el cubo seleccionado (girando) y el cubo en relevo, con margen
            // suficiente para el contorno blanco brillante.
            private Rectangle SpinRect()
            {
                if (Page == null || Page.Count == 0) return Rectangle.Empty;
                int w = Width, h = Height; if (w < 8 || h < 8) return Rectangle.Empty;
                Rectangle r = Rectangle.Empty, area, lbl;
                if (Selected >= 0 && Selected < Page.Count)
                { Geometry(w, h, Selected, out area, out lbl); area.Inflate(16, 16); r = area; }
                if (prevIdx >= 0 && prevIdx < Page.Count)
                { Geometry(w, h, prevIdx, out area, out lbl); area.Inflate(16, 16); r = (r == Rectangle.Empty) ? area : Rectangle.Union(r, area); }
                return r;
            }

            // Relevo de giro: al cambiar de cubo, el ANTERIOR da un giro rapido que decae (se "pasa la energia"),
            // y el NUEVO arranca plano. Asi se ve cual se dejo y cual es el actual, aunque se mueva muy rapido.
            private int prevIdx = -1;        // cubo que se acaba de dejar (gira y se apaga)
            private double prevAngle = 0;    // su angulo actual
            private float prevSpin = 0f;     // su velocidad de giro (decae)
            private float spinDir = 1f;      // sentido del giro (segun la direccion del movimiento)

            // Mueve la seleccion DENTRO de la pagina con efecto de relevo (lo llama el movimiento izq/der).
            public void MoveSelectionTo(int newSel, int dir)
            {
                int cnt = (Page != null) ? Page.Count : 0;
                if (newSel != Selected && Selected >= 0 && Selected < cnt)
                {
                    prevIdx = Selected;            // el que dejamos
                    prevAngle = SelAngle;          // continua desde donde iba
                    prevSpin = 0.7f * (dir >= 0 ? 1f : -1f);   // golpe de giro en el sentido del movimiento
                }
                spinDir = (dir >= 0 ? 1f : -1f);
                Selected = newSel;
                SelAngle = 0;                      // el nuevo empieza plano (y arranca a girar)
            }

            // Cancela cualquier relevo en curso (al cambiar de pagina/letra, donde los cubos ya no son los mismos).
            public void CancelHandoff() { prevIdx = -1; prevSpin = 0f; }

            // Avanza el giro del cubo actual y el del cubo en relevo (lo llama el temporizador, 24fps).
            public void StepSpin()
            {
#if ROCOLA_GPU
                if (Sliding) return;   // pasando de pagina: no hay cubo a la vista, y asi el nuevo empieza plano
#endif
                SelAngle += 0.06 * spinDir;
                if (prevIdx >= 0)
                {
                    if (Math.Abs(prevSpin) > 0.02f) { prevAngle += prevSpin; prevSpin *= 0.85f; }
                    else
                    {
                        // ya casi parado: lo asentamos al multiplo de 2π mas cercano (queda plano, sin salto)
                        double target = Math.Round(prevAngle / (2 * Math.PI)) * (2 * Math.PI);
                        prevAngle += (target - prevAngle) * 0.25;
                        if (Math.Abs(target - prevAngle) < 0.03) prevIdx = -1;
                    }
                }
            }

            public AlbumView()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            // Fuerza reconstruir la capa de cubos (cuando llega una portada en segundo plano)
            public void InvalidateCubeCache() { cacheSig = null; Invalidate(); }
            // Descarta las celdas horneadas (p.ej. tras re-escanear la biblioteca: mismo Folder pero etiqueta/portada
            // pueden haber cambiado). Fuerza rehornear en el proximo RebuildCubes.
            public void ResetCellCache() { ClearCellCache(); cellCacheW = cellCacheH = -1; cacheSig = null; Invalidate(); }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int cw = Width / Cols, ch = Height / Rows;
                if (cw > 0 && ch > 0)
                {
                    int col = e.X / cw, row = e.Y / ch;
                    if (col >= 0 && col < Cols && row >= 0 && row < Rows)
                    {
                        int idx = row * Cols + col;
                        if (Page != null && idx < Page.Count) { Selected = idx; if (OnActivate != null) OnActivate(idx); }
                    }
                }
                base.OnMouseClick(e);
            }

            private string Sig()
            {
                if (Page == null || Page.Count == 0) return "0";
                return Page.Count.ToString() + "|" + (Page[0].Folder == null ? "" : Page[0].Folder) + "|" + (Page[Page.Count - 1].Folder == null ? "" : Page[Page.Count - 1].Folder);
            }

            private void Geometry(int w, int h, int i, out Rectangle cubeArea, out Rectangle labelRect)
            {
                int cw = w / Cols, ch = h / Rows;
                int col = i % Cols, row = i / Cols;
                Rectangle cell = new Rectangle(col * cw, row * ch, cw, ch);
                int labelH = 38, padX = 8, gap = 3, padY = 6;   // labelH = 2 lineas; etiqueta pegada al cubo
                int side = Math.Min(cell.Width - 2 * padX, cell.Height - labelH - gap - 2 * padY);
                if (side < 10) side = 10;
                int blockH = side + gap + labelH;
                int top = cell.Y + Math.Max(padY, (cell.Height - blockH) / 2);   // centra cubo+etiqueta en la celda
                cubeArea = new Rectangle(cell.X + (cell.Width - side) / 2, top, side, side);
                labelRect = new Rectangle(cell.X + 2, cubeArea.Bottom + gap, cell.Width - 4, labelH);
            }

            // Dimensiones de la celda (cuadro del disco) IGUAL que las calcula Geometry, para hornear/colocar.
            private static void CellMetrics(int cw, int ch, out int side, out int labelH, out int gap)
            {
                labelH = 38; gap = 3; int padX = 8, padY = 6;
                side = Math.Min(cw - 2 * padX, ch - labelH - gap - 2 * padY);
                if (side < 10) side = 10;
            }

            // Clave de cache de la celda: Folder (estable y unico) para discos normales. Para discos TOP ("Lo mas
            // sonado"), que reusan el mismo Folder sintetico pero cuya portada cambia al variar las reproducciones,
            // se le agrega la ruta de portada: asi se re-hornea cuando cambia (la etiqueta es fija). null = no cachea.
            // Se pide para CADA casilla en CADA cuadro (el panel la usa de clave de su cache de texturas). En los
            // discos normales es la carpeta tal cual, sin gasto; en los TOP habia que pegar carpeta+portada, y eso
            // era una cadena nueva por casilla y por cuadro: en la pagina de LO MAS SONADO, seis por cuadro a 60
            // cuadros por segundo. Ahora se arma UNA vez y se guarda en el propio disco.
            private static string CellKey(Album a)
            {
                if (a == null || a.Folder == null) return null;
                if (!a.IsTop) return a.Folder;
                string cp = a.CoverPath ?? "";
                if (a.KeyCache == null || a.KeyFrom != cp) { a.KeyFrom = cp; a.KeyCache = a.Folder + "|" + cp; }
                return a.KeyCache;
            }

            // Hornea la celda de un disco (portada estatica en angulo 0 + etiqueta a 2 lineas) a un bitmap propio.
            private Bitmap BakeCell(Album a, int cw, int side, int labelH, int gap, Image cover)
            {
                Bitmap bmp = new Bitmap(cw, side + gap + labelH, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.Bilinear;   // mas rapido que bicubico (CPU vieja); las portadas ya son de buena resolucion
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Cube.Draw(g, new Rectangle((cw - side) / 2, 0, side, side), cover, 0.0);   // disco centrado (mismo x que Geometry)
                    EnsureLabelRes();
                    g.DrawString(AlbumLabel(a), lblFont, lblBrush, new Rectangle(2, side + gap, cw - 4, labelH), lblSF);   // etiqueta pegada al disco
                }
                return bmp;
            }

            // Devuelve el bitmap horneado de la celda del album; lo re-hornea si falta o si cambio el estado de
            // portada (placeholder -> cargada). Clave = CellKey(a) (ver arriba). Sin clave no se cachea.
            private Bitmap GetOrBakeCell(Album a, int cw, int side, int labelH, int gap, Image cover)
            {
                string key = CellKey(a);
                bool hasCover = (cover != null);
                CellSprite cs = null;
                if (key != null && cellCache.TryGetValue(key, out cs) && cs.Bmp != null && cs.HasCover == hasCover)
                    return cs.Bmp;   // reutiliza (paginar = solo copiar)
                Bitmap bmp = BakeCell(a, cw, side, labelH, gap, cover);
                if (key != null)
                {
                    if (cs != null && cs.Bmp != null && cs.Bmp != bmp) cs.Bmp.Dispose();   // reemplaza el placeholder por la version con portada
                    cellCache[key] = new CellSprite { Bmp = bmp, HasCover = hasCover };
                }
                return bmp;
            }

            // Como GetOrBakeCell pero con PRESUPUESTO de horneado por cuadro (para la rejilla por GPU): si la celda
            // no esta cacheada y no queda presupuesto, devuelve la version vieja (placeholder) si la hay, o null (el
            // panel la salta y aparece en el siguiente cuadro). Asi un barrido de paginas NUEVAS no hornea 18 celdas
            // de golpe -> los fps no bajan. Comparte cellCache con GetOrBakeCell (mismas claves).
            private Bitmap PeekOrBakeCell(Album a, int cw, int side, int labelH, int gap, Image cover, ref int budget)
            {
                string key = CellKey(a);
                bool hasCover = (cover != null);
                CellSprite cs = null;
                if (key != null && cellCache.TryGetValue(key, out cs) && cs.Bmp != null && cs.HasCover == hasCover)
                    return cs.Bmp;   // cacheada -> gratis
                if (budget <= 0) return (cs != null) ? cs.Bmp : null;   // sin presupuesto: usa la vieja (placeholder) si hay; si no, se salta
                budget--;
                Bitmap bmp = BakeCell(a, cw, side, labelH, gap, cover);
                if (key != null)
                {
                    if (cs != null && cs.Bmp != null && cs.Bmp != bmp) cs.Bmp.Dispose();
                    cellCache[key] = new CellSprite { Bmp = bmp, HasCover = hasCover };
                }
                return bmp;
            }

            private void ClearCellCache()
            {
                foreach (CellSprite cs in cellCache.Values) if (cs.Bmp != null) cs.Bmp.Dispose();
                cellCache.Clear();
            }

            // Compone la rejilla en cubeCache (bitmap a pantalla completa) para el render por CPU. En modo GPU NO se
            // usa (el panel dibuja cada disco como su propio quad via BuildGpuGrid); OnPaint por CPU esta suprimido.
            private void RebuildCubes(int w, int h)
            {
                if (cubeCache == null || cubeCache.Width != w || cubeCache.Height != h)
                {
                    if (cubeCache != null) cubeCache.Dispose();
                    cubeCache = new Bitmap(w, h);
                }
                if (cellCacheW != w || cellCacheH != h) { ClearCellCache(); cellCacheW = w; cellCacheH = h; }   // tamano nuevo: las celdas horneadas ya no encajan
                else if (cellCache.Count > CellCacheCap) ClearCellCache();                                     // limite de memoria (raro: solo tras paginar mucho)
                int budget = CpuBakeBudget;   // HORNEADO REPARTIDO (como en GPU): a lo mas CpuBakeBudget celdas nuevas por repintado; el
                cubesPending = false;         // resto queda para el proximo (OnPaint re-invalida). Evita el congelon de hornear 18 de golpe.
                using (Graphics g = Graphics.FromImage(cubeCache))
                {
                    g.Clear(Color.Transparent);
                    if (Page == null) return;
                    int cw = w / Cols, ch = h / Rows;
                    if (cw < 4 || ch < 4) return;
                    int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                    for (int i = 0; i < Page.Count && i < PageSize; i++)
                    {
                        // Todos los discos van estaticos (angulo 0). El seleccionado (y el de relevo) se dibujan
                        // girando ENCIMA en OnPaint; como girando nunca son mas angostos, lo tapan. Asi el cache
                        // NO depende de la seleccion -> moverse entre discos no lo reconstruye (fluido).
                        Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                        Bitmap cell = PeekOrBakeCell(Page[i], cw, side, labelH, gap, (GetCover != null) ? GetCover(Page[i]) : null, ref budget);
                        if (cell != null) g.DrawImageUnscaled(cell, (i % Cols) * cw, area.Y);   // copia 1:1 (sin re-escalar portada ni re-maquetar texto)
                        else if (Page[i] != null && Page[i].Folder != null) cubesPending = true;   // solo celdas CACHEABLES se completan luego (convergen); las raras sin Folder no bloquean el bucle
                    }
                }
            }

            // Cuando llega UNA caratula, redibuja SOLO ese cubo en el cache (no los 18).
            // Asi pasar de pagina no dispara reconstrucciones completas repetidas (evita el tiron en XP).
            public void RefreshCover(string coverPath)
            {
                if (Page == null || coverPath == null) return;
                int w = Width, h = Height;
                if (cubeCache == null || cubeCache.Width != w || cubeCache.Height != h) { InvalidateCubeCache(); return; }
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4) return;
                int side, labelH, gap; CellMetrics(cw, ch, out side, out labelH, out gap);
                bool any = false;
                Rectangle dirtyR = Rectangle.Empty;
                using (Graphics g = Graphics.FromImage(cubeCache))
                {
                    for (int i = 0; i < Page.Count && i < PageSize; i++)
                    {
                        if (Page[i] == null || Page[i].CoverPath != coverPath) continue;
                        int col = i % Cols, row = i / Cols;
                        Rectangle cell = new Rectangle(col * cw, row * ch, cw, ch);
                        Rectangle area, lbl; Geometry(w, h, i, out area, out lbl);
                        // rehornea la celda CON la portada (GetOrBakeCell detecta que HasCover cambio) y actualiza
                        // el cache: revisitar la pagina sigue siendo instantaneo. Luego la copia en cubeCache.
                        Bitmap cell2 = GetOrBakeCell(Page[i], cw, side, labelH, gap, (GetCover != null) ? GetCover(Page[i]) : null);
                        g.CompositingMode = CompositingMode.SourceCopy;   // deja la celda transparente antes de recomponer
                        using (SolidBrush tb = new SolidBrush(Color.Transparent)) g.FillRectangle(tb, cell);
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.DrawImageUnscaled(cell2, col * cw, area.Y);
                        dirtyR = (dirtyR == Rectangle.Empty) ? cell : Rectangle.Union(dirtyR, cell);
                        any = true;
                    }
                }
                if (any && dirtyR != Rectangle.Empty) Invalidate(dirtyR);   // solo su celda: 18 caratulas ya NO son 18 repintados completos
                // En modo GPU (cubeCache es null) esta ruta no se ejecuta: la portada llega a la rejilla por GPU sola,
                // porque BuildGpuGrid consulta GetCover cada cuadro y re-hornea la celda cuando la portada aparece.
            }

            private const int ContourMargin = 8;   // margen del sprite del contorno (para el resplandor ancho de 11px)

            // Hornea el sprite del contorno de seleccion (3 trazos AA -> 1 copia). Es INDEPENDIENTE de cual celda
            // este seleccionada (todas miden igual): solo se re-hornea al cambiar de tamano. Lo llaman el OnPaint
            // (CPU) y el panel GPU (via ContourSpriteBmp), asi existe aunque el OnPaint por CPU este suprimido.
            private void EnsureContourSprite()
            {
                int w = Width, h = Height;
                if (w < 8 || h < 8) return;
                int cw = w / Cols, ch = h / Rows;
                if (cw < 4 || ch < 4) return;
                Rectangle area, lbl; Geometry(w, h, 0, out area, out lbl);
                Rectangle fr = area; fr.Inflate(4, 4);
                Size need = new Size(fr.Width + 2 * ContourMargin, fr.Height + 2 * ContourMargin);
                if (contourSprite != null && contourSpriteSize == need) return;
                if (cPenGlow == null)
                {
                    cPenGlow = new Pen(Color.FromArgb(20, 255, 255, 255), 11f);   // resplandor ancho y tenue
                    cPenMid = new Pen(Color.FromArgb(45, 255, 255, 255), 6f);     // resplandor medio
                    cPenLine = new Pen(Color.FromArgb(235, 255, 255, 255), 2.4f); // linea nitida
                }
                if (contourSprite != null) contourSprite.Dispose();
                contourSprite = new Bitmap(need.Width, need.Height, PixelFormat.Format32bppPArgb);
                using (Graphics cg = Graphics.FromImage(contourSprite))
                using (GraphicsPath cp = RoundRect(new Rectangle(ContourMargin, ContourMargin, fr.Width, fr.Height), 10))
                {
                    cg.SmoothingMode = SmoothingMode.AntiAlias;
                    cg.DrawPath(cPenGlow, cp);
                    cg.DrawPath(cPenMid, cp);
                    cg.DrawPath(cPenLine, cp);
                }
                contourSpriteSize = need;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                int w = Width, h = Height;
                if (w < 8 || h < 8) { g.Clear(Shade(BgAccent, 0.82)); return; }
                // Cuando el fondo GPU (pantalla completa, opaco) esta encima, TODO esto queda oculto. Reconstruir
                // la rejilla + pintar estrellas + cubo por CPU en cada Invalidate (p.ej. al paginar, que llama
                // Invalidate por cambio de pagina) era trabajo invisible que ademas SALTABA la franja de la GPU
                // -> tirones al hojear. El panel GPU dibuja la rejilla por su cuenta (BuildGpuGrid, un quad/celda).
#if ROCOLA_GPU
                if (GpuCovered) return;
#endif

                bool sizeChanged = (cacheW != w || cacheH != h);
                string sig = Sig();
                if (sizeChanged || cubeCache == null || sig != cacheSig || cubesPending) RebuildCubes(w, h);   // ya NO se reconstruye por cambio de seleccion; cubesPending completa el horneado repartido
                cacheW = w; cacheH = h; cacheSig = sig;

                if (Sky != null)
                {
                    Sky.SkyOffsetX = this.Left; Sky.SkyOffsetY = this.Top;   // VENTANA del campo COMPARTIDO de pantalla completa (una sola pieza con las franjas)
                    Sky.SkyFullW = (this.Parent != null) ? this.Parent.ClientSize.Width : w;
                    Sky.SkyFullH = (this.Parent != null) ? this.Parent.ClientSize.Height : h;
                    Sky.SkyMid = SkyColorMid(BgAccent);
                    Sky.Dim = BgDim;   // atenuado mientras se navega con musica (vuelve solo tras 10s sin botones)
                    Sky.DrawReactive(g, w, h, SkyColor(BgAccent), SkyColor2(BgAccent), PulseLo, PulseMid, PulseHi);   // fondo titilante + LATIDO por banda (grandes=graves, medianas=medios/agudos)
                }
                g.DrawImageUnscaled(cubeCache, 0, 0);

                if (Page != null && Selected >= 0 && Selected < Page.Count)
                {
                    int cw = w / Cols, ch = h / Rows;
                    if (cw >= 4 && ch >= 4)
                    {
                        Rectangle area, lbl;
                        Geometry(w, h, Selected, out area, out lbl);
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        // Contorno blanco con resplandor del disco SELECCIONADO (ademas del giro), para ubicarlo facil.
                        Rectangle fr = area; fr.Inflate(4, 4);
                        EnsureContourSprite();   // sprite horneado (3 trazos AA -> 1 copia); solo se re-hornea al cambiar tamano
                        if (contourSprite != null) g.DrawImageUnscaled(contourSprite, fr.X - ContourMargin, fr.Y - ContourMargin);
                        Cube.Draw(g, area, SelectedCover(), SelAngle);
                    }
                }

                // Cubo de RELEVO: el que acabamos de dejar sigue girando (rapido y decayendo) sobre su
                // posicion. Como el cubo girando nunca es mas angosto que el del cache, lo tapa sin fantasma.
                if (prevIdx >= 0 && prevIdx != Selected && Page != null && prevIdx < Page.Count)
                {
                    int cw = w / Cols, ch = h / Rows;
                    if (cw >= 4 && ch >= 4)
                    {
                        Rectangle area2, lbl2;
                        Geometry(w, h, prevIdx, out area2, out lbl2);
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        Cube.Draw(g, area2, (GetCover != null) ? GetCover(Page[prevIdx]) : null, prevAngle);
                    }
                }
                if (cubesPending) Invalidate();   // quedan celdas por hornear: pide otro repintado para completarlas (repartido, sin congelar)
            }
        }

        // ==========  VIDEO: host del control ActiveX de Windows Media Player  ==========
        // (CLSID del OCX de wmp.dll)
        private class VideoHost : AxHost
        {
            public VideoHost() : base("6bf52a52-394a-11d3-b153-00c04f79faa6") { }
            public object GetOcxObj() { return this.GetOcx(); }
        }

        // Un elemento de la cola: una cancion O un video. Disc = disco para el cubo de abajo.
        private class PlayItem
        {
            public bool IsVideo;
            public Track Song;
            public string VideoPath;
            public Album Disc;
            public string PathKey()
            {
                if (IsVideo) return VideoPath ?? "";
                return (Song != null && Song.Path != null) ? Song.Path : "";
            }
            public string Display()
            {
                if (IsVideo)
                {
                    // Mostrar "album - nombre del video": el disco (carpeta) MAS el nombre del archivo.
                    string disc = (Disc != null && !string.IsNullOrEmpty(Disc.Artist)) ? Disc.Artist : "";
                    string file = "";
                    try { file = System.IO.Path.GetFileNameWithoutExtension(VideoPath); } catch { }
                    string n;
                    if (disc.Length > 0 && file.Length > 0) n = disc + " - " + file;
                    else if (file.Length > 0) n = file;
                    else n = disc;
                    return "\u25BA Video:  " + n;
                }
                return (Song != null) ? (Song.Artist + " - " + Song.Title) : "";
            }
        }

        // Un archivo de video dentro de un disco; en la lista se ve solo el nombre sin extension
        private class VideoItem
        {
            public string Path;
            public VideoItem(string p) { Path = p; }
            public override string ToString()
            {
                try { return System.IO.Path.GetFileNameWithoutExtension(Path); }
                catch { return Path; }
            }
        }

        // ==========  CUBO DE LA CANCION (gira + se inclina)  ==========
        private class CubeBox : Control
        {
            public Image Cover;
            public double Angle;
            public double Phase;

            public CubeBox()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.FromArgb(10, 15, 44);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                if (this.BackColor.A == 255) g.Clear(this.BackColor);
                int w = Width, h = Height;
                if (w < 12 || h < 12) return;
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int side = (int)(Math.Min(w, h) * 0.78);
                Rectangle area = new Rectangle((w - side) / 2, (h - side) / 2, side, side);

                float cx = w / 2f, cy = h / 2f;
                float tilt = (float)(10.0 * Math.Sin(Phase * 0.9));
                GraphicsState st = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(tilt);
                g.TranslateTransform(-cx, -cy);
                Cube.Draw(g, area, Cover, Angle);
                g.Restore(st);
            }
        }

        // ==========  TIRA DE LETRAS (con brillo animado)  ==========
        // Barra de paginas de la lista de canciones: "Pagina X de Y" al centro, y a los lados las
        // instrucciones VARIABLES con flechas DIBUJADAS (la fuente de XP no trae flechas unicode):
        //   [<] pagina 1        Pagina 2 de 3        pagina 3 [>]
        private class SongPagerBar : Control
        {
            public int Page = 0;    // pagina actual (desde 0)
            public int Pages = 1;   // total de paginas
            private Font pgFont;    // fuente cacheada (esta barra se redibuja al paginar / con el brillo)

            public SongPagerBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }
            // Dibuja la barra de paginas en cualquier Graphics (el control, o la textura de UI del modo GPU).
            public void RenderContent(Graphics g)
            {
                int w = Width, h = Height;
                g.FillRectangle(CB(BackColor), 0, 0, w, h);   // pincel reusable (mutado)
                if (Pages <= 1 || w < 60 || h < 10) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int cy = h / 2;
                Color txt = Color.FromArgb(228, 236, 252);
                Color dim = Color.FromArgb(175, 188, 215);
                Color yellow = Color.FromArgb(242, 206, 72);   // mismo amarillo que la cruceta izq/der
                if (pgFont == null) pgFont = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold);
                Font f = pgFont;
                {
                    // Centro: Pagina X de Y
                    string c = "Pagina " + (Page + 1) + " de " + Pages;
                    SizeF cs = g.MeasureString(c, f);
                    g.DrawString(c, f, CB(txt), (w - cs.Width) / 2f, cy - cs.Height / 2f);

                    // Izquierda: [<] pagina anterior (solo si existe)
                    if (Page > 0)
                    {
                        float x = 4;
                        x = CtrlDirLeft(g, x, cy, 11, yellow) + 5;
                        CtrlTxt(g, f, "pagina " + Page, x, cy, dim);
                    }
                    // Derecha: pagina siguiente [>] (solo si existe)
                    if (Page < Pages - 1)
                    {
                        string r = "pagina " + (Page + 2);
                        SizeF rs = g.MeasureString(r, f);
                        float xr = w - 4 - 11 - 5 - rs.Width;
                        xr = CtrlTxt(g, f, r, xr, cy, dim) + 5;
                        CtrlDirRight(g, xr, cy, 11, yellow);
                    }
                }
            }
        }

        private class LetterBar : Control
        {
            private SolidBrush pulseBrush; private Pen pulsePen;                       // resaltado de letra (reusables)
            private GraphicsPath pulsePath; private RectangleF pulseBox = RectangleF.Empty;  // trayectoria cacheada
            private GraphicsPath goldPath; private RectangleF goldBox = RectangleF.Empty;    // marcador TOP dorado
            private SolidBrush goldBrush; private Pen goldPen; private SolidBrush goldTextBrush;
            private Bitmap stripCache; private int scW = -1, scH = -1; private char scCur = '\0';
            private string scLetters; private int scAvailN = -2;                              // firma de la tira cacheada
            private Font fMain, fVideo; private StringFormat sfCen; private SolidBrush whiteB; // recursos fijos
            public string Letters = "\u2605#ABCDEFGHIJKLMNOPQRSTUVWXYZ\u25BA";
            public char Current = '#';
            public double Phase = 0;
            public HashSet<char> Available;
            public Action<char> OnPick;

            public LetterBar()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int n = Letters.Length;
                if (n == 0 || Width <= 0) { base.OnMouseClick(e); return; }
                int slot = Width / n;
                if (slot <= 0) { base.OnMouseClick(e); return; }
                int i = e.X / slot;
                if (i >= 0 && i < n && OnPick != null) OnPick(Letters[i]);
                base.OnMouseClick(e);
            }

            private void EnsureRes()
            {
                if (fMain == null) fMain = new Font(this.Font.FontFamily, 10.5f, FontStyle.Bold);
                if (fVideo == null) fVideo = new Font(this.Font.FontFamily, 7.5f, FontStyle.Bold);
                if (whiteB == null) whiteB = new SolidBrush(Color.White);
                if (sfCen == null)
                {
                    sfCen = new StringFormat();
                    sfCen.Alignment = StringAlignment.Center; sfCen.LineAlignment = StringAlignment.Center;
                }
            }

            // Dibuja TODAS las letras (menos la actual, que pulsa en vivo) en una tira reusable.
            private void RebuildStrip(int w, int h, int n, float slot)
            {
                if (stripCache == null || stripCache.Width != w || stripCache.Height != h)
                {
                    if (stripCache != null) stripCache.Dispose();
                    stripCache = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                }
                using (Graphics g = Graphics.FromImage(stripCache))
                using (SolidBrush tb = new SolidBrush(Color.White))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    for (int i = 0; i < n; i++)
                    {
                        char ch = Letters[i];
                        if (ch == Current) continue;
                        if (ch == '\u2605' && Available != null && Available.Contains(ch)) continue;   // TOP disponible: va dorado en vivo
                        bool isVideo = (ch == '\u25BA');
                        bool isTop = (ch == '\u2605');
                        string lbl = isVideo ? "VIDEO" : isTop ? "TOP" : ch.ToString();
                        Font useF = (isVideo || isTop) ? fVideo : fMain;
                        RectangleF slotRect = new RectangleF(i * slot, 0, slot, h);
                        bool avail = (Available == null) || Available.Contains(ch);
                        tb.Color = avail ? Color.FromArgb(225, 232, 245) : Color.FromArgb(110, 120, 150);
                        g.DrawString(lbl, useF, tb, slotRect, sfCen);
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics, false); }
            // Casilla de la letra ACTUAL (para el pulso por GPU): centro/radio en coords del control.
            // false si no aplica (la actual es TOP, que lleva su propio halo dorado, o no esta en la tira).
            public bool GetCurrentGlow(out float cx, out float cy, out float r)
            {
                cx = cy = r = 0f;
                int n = Letters.Length; if (n == 0 || Width < 4 || Height < 4) return false;
                if (Current == '\u2605') return false;
                int i = Letters.IndexOf(Current); if (i < 0) return false;
                float slot = (float)Width / n;
                cx = i * slot + slot / 2f; cy = Height / 2f;
                r = Math.Max(slot, Height) * 0.75f;
                return true;
            }
            // Casilla del marcador TOP (para su dorado pulsante por GPU); false si TOP no esta disponible.
            public bool GetTopGlow(out float cx, out float cy, out float r)
            {
                cx = cy = r = 0f;
                int n = Letters.Length; if (n == 0 || Width < 4 || Height < 4) return false;
                if (Available == null || !Available.Contains('\u2605')) return false;
                int i = Letters.IndexOf('\u2605'); if (i < 0) return false;
                float slot = (float)Width / n;
                cx = i * slot + slot / 2f; cy = Height / 2f;
                r = Math.Max(slot, Height) * 0.75f;
                return true;
            }
            public void RenderContent(Graphics g) { RenderContent(g, false); }
            // Dibuja la barra de letras en cualquier Graphics (el control, o la textura de UI del modo GPU).
            // forGpu=true: brillos FIJOS (mitad del pulso) en la textura; el pulso vivo lo ponen los halos por
            // quad (GetCurrentGlow/GetTopGlow) -> la textura de UI ya no se rehace ~13 veces/seg por esta barra.
            public void RenderContent(Graphics g, bool forGpu)
            {
                int w = Width, h = Height, n = Letters.Length;
                if (w < 4 || h < 4 || n == 0) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float slot = (float)w / n;
                double pulse = forGpu ? 0.45 : 0.5 + 0.5 * Math.Sin(Phase * 2.2);
                EnsureRes();

                // TIRA CACHEADA: las ~28 letras solo se redibujan al cambiar letra/tamano/disponibles.
                // Por cuadro: copiar la tira + la letra ACTUAL con su resplandor (antes: ~28 textos/cuadro).
                int availN = (Available == null) ? -1 : Available.Count;
                if (stripCache == null || scW != w || scH != h || scCur != Current || scLetters != Letters || scAvailN != availN)
                {
                    RebuildStrip(w, h, n, slot);
                    scW = w; scH = h; scCur = Current; scLetters = Letters; scAvailN = availN;
                }
                g.DrawImageUnscaled(stripCache, 0, 0);

                for (int i = 0; i < n; i++)
                {
                    char ch = Letters[i];
                    if (ch != Current || ch == '\u2605') continue;   // TOP lo dibuja el bloque dorado
                    bool isVideo = (ch == '\u25BA');
                    bool isTop = (ch == '\u2605');
                    string lbl = isVideo ? "VIDEO" : isTop ? "TOP" : ch.ToString();
                    Font useF = (isVideo || isTop) ? fVideo : fMain;
                    RectangleF slotRect = new RectangleF(i * slot, 0, slot, h);
                    int pad = 3;
                    RectangleF box = new RectangleF(slotRect.X + pad, pad, slotRect.Width - 2 * pad, h - 2 * pad);
                    int glowA = 70 + (int)(pulse * 110);
                    if (pulsePath == null || box != pulseBox)   // misma casilla -> misma trayectoria
                    {
                        if (pulsePath != null) pulsePath.Dispose();
                        pulsePath = RoundRectF(box, 8);
                        pulseBox = box;
                    }
                    if (pulseBrush == null) pulseBrush = new SolidBrush(Color.Black);
                    if (pulsePen == null) pulsePen = new Pen(Color.FromArgb(220, 150, 205, 255), 2f);
                    pulseBrush.Color = Color.FromArgb(glowA, 90, 170, 255);
                    g.FillPath(pulseBrush, pulsePath);
                    g.DrawPath(pulsePen, pulsePath);
                    g.DrawString(lbl, useF, whiteB, slotRect, sfCen);
                    break;
                }

                // Marcador TOP: SIEMPRE dorado pulsante para llamar la atencion del cliente (solo si hay
                // algo dentro). Se dibuja en vivo este o no seleccionado.
                int topI = Letters.IndexOf('\u2605');
                if (topI >= 0 && Available != null && Available.Contains('\u2605'))
                {
                    RectangleF gsl = new RectangleF(topI * slot, 0, slot, h);
                    double gpu = forGpu ? 0.5 : 0.5 + 0.5 * Math.Sin(Phase * 2.6);   // GPU: brillo FIJO en la textura (el pulso dorado lo pone el halo por quad, GetTopGlow)
                    bool topSel = (Current == '\u2605');
                    if (goldTextBrush == null) goldTextBrush = new SolidBrush(Color.Black);
                    if (topSel)
                    {
                        // SELECCIONADO: caja dorada (mismo concepto que la seleccion azul de las letras)
                        RectangleF gbx = new RectangleF(gsl.X + 3, 3, gsl.Width - 6, h - 6);
                        int ga = 80 + (int)(gpu * 150);
                        if (goldPath == null || gbx != goldBox) { if (goldPath != null) goldPath.Dispose(); goldPath = RoundRectF(gbx, 8); goldBox = gbx; }
                        if (goldBrush == null) goldBrush = new SolidBrush(Color.Black);
                        if (goldPen == null) goldPen = new Pen(Color.Black, 2.2f);
                        goldBrush.Color = Color.FromArgb(ga, 255, 200, 40);
                        goldPen.Color = Color.FromArgb(235, 255, 228, 130);
                        g.FillPath(goldBrush, goldPath);
                        g.DrawPath(goldPen, goldPath);
                        goldTextBrush.Color = Color.White;
                        g.DrawString("TOP", fVideo, goldTextBrush, gsl, sfCen);
                    }
                    else
                    {
                        // NO seleccionado: BRILLAN solo las letras (sin caja, para no confundir con seleccion).
                        // halo dorado tenue (4 pasadas desplazadas) + texto dorado pulsante encima.
                        int hAlpha = 35 + (int)(gpu * 55);
                        goldTextBrush.Color = Color.FromArgb(hAlpha, 255, 205, 90);
                        g.DrawString("TOP", fVideo, goldTextBrush, new RectangleF(gsl.X - 1, gsl.Y, gsl.Width, gsl.Height), sfCen);
                        g.DrawString("TOP", fVideo, goldTextBrush, new RectangleF(gsl.X + 1, gsl.Y, gsl.Width, gsl.Height), sfCen);
                        g.DrawString("TOP", fVideo, goldTextBrush, new RectangleF(gsl.X, gsl.Y - 1, gsl.Width, gsl.Height), sfCen);
                        g.DrawString("TOP", fVideo, goldTextBrush, new RectangleF(gsl.X, gsl.Y + 1, gsl.Width, gsl.Height), sfCen);
                        int gb = 60 + (int)(gpu * 70); if (gb > 255) gb = 255;
                        goldTextBrush.Color = Color.FromArgb(255, 255, 210, gb);   // dorado pulsante brillante
                        g.DrawString("TOP", fVideo, goldTextBrush, gsl, sfCen);
                    }
                }
            }

            private static GraphicsPath RoundRectF(RectangleF r, int radius)
            {
                GraphicsPath p = new GraphicsPath();
                float d = radius * 2;
                if (d > r.Height) d = r.Height;
                if (d > r.Width) d = r.Width;
                if (d < 2) d = 2;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }
        }

        // ==========  BARRA DE PROGRESO  ==========
        private class ProgressView : Control
        {
            private double _value;
            public double Value { get { return _value; } set { double v = value < 0 ? 0 : (value > 1 ? 1 : value); if (v != _value) { _value = v; Invalidate(); } } }
            private Color _accent = DefaultAccent;   // ESTETICA: la barra toma el color del genero que suena
            public Color Accent { get { return _accent; } set { if (value.ToArgb() != _accent.ToArgb()) { _accent = value; Invalidate(); } } }

            private SolidBrush trackB; private Pen trackP;   // riel: colores fijos -> se cachean (se dibuja en cada reconstruccion de UI)

            public ProgressView()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }
            // Dibuja la barra de progreso en cualquier Graphics (el control, o la textura de UI del modo GPU).
            public void RenderContent(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = Width, h = Height;
                if (w < 4 || h < 4) return;

                // Riel HUNDIDO: oscuro translucido + borde interior tenue (parece rebajado, no plano).
                Rectangle track = new Rectangle(0, 0, w - 1, h - 1);
                if (trackB == null) { trackB = new SolidBrush(Color.FromArgb(130, 6, 10, 26)); trackP = new Pen(Color.FromArgb(40, 255, 255, 255), 1f); }
                using (GraphicsPath tp = RoundedPath(track, h / 2))
                {
                    g.FillPath(trackB, tp);
                    g.DrawPath(trackP, tp);
                }

                int fw = (int)(_value * (w - 2));
                if (fw >= 3)
                {
                    Rectangle fill = new Rectangle(1, 1, fw, h - 3);
                    int rad = Math.Max(1, (h - 3) / 2);
                    using (GraphicsPath fp = RoundedPath(fill, rad))
                    {
                        // halo "neon" alrededor del relleno (acorde al cielo estrellado)
                        using (Pen glow = new Pen(Color.FromArgb(70, Lighten(_accent, 0.5)), 3f)) { glow.LineJoin = LineJoin.Round; g.DrawPath(glow, fp); }
                        // cuerpo: degradado vertical brillante arriba -> acento abajo
                        using (LinearGradientBrush fb = new LinearGradientBrush(new Rectangle(fill.X, fill.Y, fill.Width, fill.Height + 1), Lighten(_accent, 0.6), Shade(_accent, 0.1), LinearGradientMode.Vertical))
                            g.FillPath(fb, fp);
                        // gloss: franja clara en la mitad superior (aspecto vidrioso)
                        Rectangle gloss = new Rectangle(fill.X + 1, fill.Y + 1, Math.Max(1, fill.Width - 2), Math.Max(1, (fill.Height - 1) / 2));
                        using (GraphicsPath gpath = RoundedPath(gloss, Math.Max(1, gloss.Height / 2)))
                        using (LinearGradientBrush gb = new LinearGradientBrush(new Rectangle(gloss.X, gloss.Y, gloss.Width, gloss.Height + 1), Color.FromArgb(150, 255, 255, 255), Color.FromArgb(15, 255, 255, 255), LinearGradientMode.Vertical))
                            g.FillPath(gb, gpath);
                        // borde nitido brillante
                        using (Pen edge = new Pen(Color.FromArgb(180, Lighten(_accent, 0.65)), 1f)) g.DrawPath(edge, fp);
                    }
                }
            }
        }

        // ==========  LISTA DE CANCIONES (dibujada a mano, no ListBox nativo) ==========
        // Reemplaza al ListBox: asi el interior del disco se puede componer por GPU (un control nativo no se
        // puede poner sobre D3D). Es una VISTA de UNA pagina: la seleccion global la maneja el form (songSel).
        private class SongListView : Control
        {
            public Func<int> GetCount;          // total de items (songItems.Count)
            public Func<int, string> GetText;   // texto del item i
            public Func<int, string> GetRight;   // texto CORTO a la derecha del item i (la duracion); "" = nada
            public int PageStart = 0;           // primer item de la pagina visible
            public int Sel = 0;                 // seleccion GLOBAL (entre todas las paginas)
            public int RowH = 30;
            public Color Accent = DefaultAccent;   // color del genero (para el resaltado)
            public Action<int> OnActivate;      // doble-click sobre un item (encolar)
            public Action<int> OnPick;          // click simple: fija la seleccion

            private SolidBrush hlB; private Pen hlP;
            private SolidBrush txtB; private StringFormat txtFmt; private SolidBrush bgB;
            private SolidBrush durB; private StringFormat durFmt;   // duracion: gris suave, pegada a la derecha

            public SongListView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Shade(DefaultAccent, 0.76);
            }

            public int RowsPerPage { get { int ih = Math.Max(1, RowH); return Math.Max(1, ClientSize.Height / ih); } }

            protected override void OnPaintBackground(PaintEventArgs e) { /* todo en OnPaint */ }
            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics); }

            // Dibuja la pagina visible en cualquier Graphics (el control, o la textura de UI del modo GPU).
            // El fondo es OPACO (como el ListBox): la lista tapa el starfield donde esta (igual que en CPU).
            public void RenderContent(Graphics g)
            {
                int w = ClientSize.Width, h = ClientSize.Height;
                if (w < 4 || h < 4) return;
                int n = (GetCount != null) ? GetCount() : 0;
                int rpp = RowsPerPage;
                if (hlB == null) { hlB = new SolidBrush(Color.Black); hlP = new Pen(Color.Black, 1.4f); }
                if (txtB == null)
                {   // GDI+ (DrawString), NO TextRenderer: TextRenderer usa GDI e IGNORA la traslacion del Graphics,
                    txtB = new SolidBrush(Color.White);   // asi que sobre la textura de UI (trasladada) el texto caia en 0,0
                    txtFmt = new StringFormat();
                    txtFmt.FormatFlags = StringFormatFlags.NoWrap;
                    txtFmt.Trimming = StringTrimming.EllipsisCharacter;
                    txtFmt.Alignment = StringAlignment.Near;
                    txtFmt.LineAlignment = StringAlignment.Center;
                }
                if (bgB == null) bgB = new SolidBrush(BackColor); else bgB.Color = BackColor;
                g.FillRectangle(bgB, 0, 0, w, h);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int row = 0; row < rpp; row++)
                {
                    int idx = PageStart + row;
                    if (idx >= n) break;
                    Rectangle r = new Rectangle(0, row * RowH, w, RowH);
                    if (idx == Sel)
                    {
                        Rectangle hl = new Rectangle(r.X + 4, r.Y + 3, r.Width - 8, r.Height - 6);
                        hlB.Color = Color.FromArgb(150, Lighten(Accent, 0.15));
                        hlP.Color = Color.FromArgb(180, Lighten(Accent, 0.45));
                        using (GraphicsPath gp = RoundedPath(hl, 9)) { g.FillPath(hlB, gp); g.DrawPath(hlP, gp); }
                    }
                    // DURACION a la derecha (m:ss). Se dibuja primero para saber cuanto ancho quitarle al
                    // titulo: asi un nombre largo se corta con "..." ANTES de encimarse con la duracion.
                    string dur = (GetRight != null) ? GetRight(idx) : "";
                    int durW = 0;
                    if (!string.IsNullOrEmpty(dur))
                    {
                        if (durB == null)
                        {
                            durB = new SolidBrush(Color.White);
                            durFmt = new StringFormat();
                            durFmt.FormatFlags = StringFormatFlags.NoWrap;
                            durFmt.Alignment = StringAlignment.Far;
                            durFmt.LineAlignment = StringAlignment.Center;
                        }
                        durW = 58;
                        durB.Color = (idx == Sel) ? Color.FromArgb(235, 245, 255) : Color.FromArgb(175, 190, 215);
                        g.DrawString(dur, Font, durB, new Rectangle(r.Right - durW - 14, r.Y, durW, r.Height), durFmt);
                    }
                    Rectangle tr = new Rectangle(r.X + 16, r.Y, r.Width - 22 - durW, r.Height);
                    string text = (GetText != null) ? GetText(idx) : "";
                    g.DrawString(text, Font, txtB, tr, txtFmt);   // GDI+: respeta la traslacion del Graphics (en el control es identidad; en la textura de UI lleva la lista a su sitio)
                }
            }

            private int RowIndexAt(int y) { int row = y / Math.Max(1, RowH); return PageStart + row; }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int idx = RowIndexAt(e.Y);
                if (GetCount != null && idx >= 0 && idx < GetCount() && OnPick != null) OnPick(idx);
                base.OnMouseClick(e);
            }
            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                int idx = RowIndexAt(e.Y);
                if (GetCount != null && idx >= 0 && idx < GetCount() && OnActivate != null) OnActivate(idx);
                base.OnMouseDoubleClick(e);
            }
        }

        // ==========  BUSCADOR EN PANTALLA  ==========
        private enum KKind { Char, Space, Back, Choose }

        private class SearchKey
        {
            public KKind Kind;
            public char Ch;
            public string Label;
            public RectangleF R;
        }

        private class SearchOverlay : Control
        {
            public string Query = "";
            public List<SearchItem> Results = new List<SearchItem>();
            public int SelRow = 1;
            public int SelCol = 0;
            public bool FocusResults = false;
            public int SelResult = 0;
            private int resCols = 1, resRowsPerCol = 8;   // dimensiones de la cuadricula de resultados (se fijan al pintar)
            public double CubeAngle = 0;
            public double Phase = 0;
            public Color Accent = DefaultAccent;
            public Func<Album, Image> GetCover;
            public Action<string> OnQueryChanged;
            public Action<SearchItem> OnChoose;
            public Action OnClose;
            public bool ToastOn = false;
            public string ToastMsg = "";
            public Color ToastBack = Color.FromArgb(238, 40, 165, 90);
            private Rectangle cubeRect = Rectangle.Empty;
            public double PreviewAngle { get { return CubeAngle; } }   // angulo del cubo de vista previa (lo dibuja la GPU)
            // Calcula EN VIVO el cubo de vista previa (celda + par de portadas a fundir) para que la GPU lo
            // dibuje como geometria. Devuelve false si no hay cubo de vista previa visible ahora.
            public bool GetPreviewCube(out Rectangle area, out Image ca, out Image cb, out float f)
            {
                area = Rectangle.Empty; ca = null; cb = null; f = 0f;
                int W = Width, H = Height;
                if (W < 60 || H < 60) return false;
                if (!(FocusResults && Results != null && Results.Count > 0 && GetCover != null)) return false;
                int M = 30, contentY = M + 70 + 28, contentH = H - contentY - M;
                if (contentH < 60) return false;
                int kbW = (int)((W - 2 * M) * 0.52);
                return ComputePreview(new Rectangle(M, contentY, kbW, contentH), out ca, out cb, out f, out area);
            }
            private readonly List<Image> artistCovers = new List<Image>();
            private Bitmap blendBmp;
            private Starfield stars = new Starfield();   // fondo estrellado reactivo al genero
            public float AudioPulse = 0f;   // 0..1 nivel de audio
            public float BgDim = 1f;        // atenuacion del starfield mientras se navega con musica (el form la anima)
            // Brillo de la barra de instrucciones del buscador (Mover / Poner-Elegir / Cerrar) al pulsar.
            public const int SMover = 0, SPoner = 1, SCerrar = 2;
            private static readonly Color SRed = Color.FromArgb(228, 72, 72), SGreen = Color.FromArgb(72, 212, 112), SYellow = Color.FromArgb(242, 206, 72);
            private readonly InstrFlash hintFlash = new InstrFlash();
            public void FlashMove(bool vertical) { hintFlash.Fire(SMover, vertical ? SRed : SYellow); }   // arriba/abajo ROJO, izq/der AMARILLO
            public void FlashOK() { hintFlash.Fire(SPoner, SGreen); }
            public void FlashClose() { hintFlash.Fire(SCerrar, Color.White); }
            // Centro/radio de los 3 halos del hint bar (Mover/Poner/Cerrar), guardados al maquetar. En GPU el halo NO
            // se hornea en la textura: lo dibuja la GPU como quad (GetHintGlow) para que el desvanecido no re-suba la textura.
            private readonly float[] hgCx = new float[3], hgCy = new float[3], hgR = new float[3];
            private void StoreHintGlow(int k, float leftX, float cy, float symW) { hgCx[k] = leftX + symW / 2f; hgCy[k] = cy; hgR[k] = symW * 0.9f + 6f; }
            public bool GetHintGlow(int k, out float cx, out float cy, out float r, out int argb)
            {
                cx = cy = r = 0f; argb = 0;
                if (k < 0 || k > 2) return false;
                float lvl = hintFlash.Level(k);   // k == SMover/SPoner/SCerrar (0/1/2)
                if (lvl <= 0.02f) return false;
                cx = hgCx[k]; cy = hgCy[k]; r = hgR[k];
                Color c = hintFlash.Col(k);
                argb = Color.FromArgb((int)(lvl * 215f), c.R, c.G, c.B).ToArgb();   // alfa = brillo del destello; rgb = su color
                return true;
            }
            // Recursos de pintado CACHEADOS: el buscador creaba ~90 objetos GDI por cuadro (fuentes,
            // pinceles y plumas, incluso por cada tecla y resultado) a 24fps. Ahora todo es reusable.
            private Font fQuery, fHint, fToast, fHead, fEmpty, fRes, fResSub, fPrevT, fPrevS, fPrevH;
            private Font fChar, fSp; private float fKh = -1f;   // fuentes del teclado (dependen de su altura)
            private SolidBrush sbS; private Pen pnS;            // pincel/pluma reusables (se les muta color/grosor)
            // CACHE del teclado SIN seleccion: las ~40 teclas (con degradado, contorno y texto) son carisimas
            // de redibujar cada vez (RoundedPath + LinearGradientBrush por tecla). Como el teclado es estatico,
            // se dibuja UNA vez a un bitmap y luego solo se copia + se pinta EN VIVO la tecla seleccionada.
            private Bitmap kbCache; private int kbCacheW = -1, kbCacheH = -1, kbCacheAccent; private float kbCacheKh = -1f;
            private Point kbCacheOrigin;
            // CACHE de la lista de resultados (mismo motivo que el teclado): ~40 tarjetas con degradado, contorno
            // y 2 textos cada una. Los resultados dependen SOLO de la consulta, asi que se cachean por Query y
            // solo se pinta EN VIVO la tarjeta seleccionada. Moverse por teclas/resultados deja de dar tirones.
            private Bitmap resCache; private string resCacheQuery = "\0"; private int resCacheW = -1, resCacheH = -1, resCacheAccent;
            private Point resCacheOrigin;
            private void EnsurePaintRes()
            {
                if (fQuery != null) return;
                fQuery = new Font(FontFamily.GenericSansSerif, 20f, FontStyle.Bold);
                fHint = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold);
                fToast = new Font(FontFamily.GenericSansSerif, 15.5f, FontStyle.Bold);
                fHead = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold);
                fEmpty = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Italic);
                fRes = new Font(FontFamily.GenericSansSerif, 10.5f, FontStyle.Bold);
                fResSub = new Font(FontFamily.GenericSansSerif, 7.5f, FontStyle.Bold);
                fPrevT = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold);
                fPrevS = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Regular);
                fPrevH = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold);
                sbS = new SolidBrush(Color.Black);
                pnS = new Pen(Color.Black, 1f);
            }

            private readonly List<List<SearchKey>> rows = new List<List<SearchKey>>();
            private readonly List<KeyValuePair<RectangleF, int>> resultRects = new List<KeyValuePair<RectangleF, int>>();

            public SearchOverlay()
            {
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
                this.TabStop = true;
                this.BackColor = Color.FromArgb(8, 12, 36);
                BuildKeys();
            }

            private void BuildKeys()
            {
                AddRow("1234567890");
                AddRow("QWERTYUIOP");
                AddRow("ASDFGHJKL\u00D1");
                AddRow("ZXCVBNM");
                List<SearchKey> sp = new List<SearchKey>();
                sp.Add(new SearchKey { Kind = KKind.Space, Label = "ESPACIO" });
                sp.Add(new SearchKey { Kind = KKind.Back, Label = "BORRAR" });
                rows.Add(sp);
            }

            private void AddRow(string s)
            {
                List<SearchKey> row = new List<SearchKey>();
                for (int i = 0; i < s.Length; i++)
                    row.Add(new SearchKey { Kind = KKind.Char, Ch = s[i], Label = s[i].ToString() });
                rows.Add(row);
            }

            public void SetQuery(string q) { Query = q; FocusResults = false; SelResult = 0; Invalidate(); }
            public void TypeChar(char c) { Query += c; Changed(); }
            public void Backspace() { if (Query.Length > 0) Query = Query.Substring(0, Query.Length - 1); Changed(); }
            private void Changed() { FocusResults = false; SelResult = 0; if (OnQueryChanged != null) OnQueryChanged(Query); Invalidate(); }

            public void MoveSel(int dx, int dy)
            {
                if (FocusResults)
                {
                    int cap = Math.Max(1, resCols * resRowsPerCol);
                    int n = Math.Min((Results != null) ? Results.Count : 0, cap);
                    if (n == 0) { if (dx < 0) FocusResults = false; Invalidate(); return; }
                    if (SelResult < 0) SelResult = 0;
                    if (SelResult >= n) SelResult = n - 1;
                    int rpc = (resRowsPerCol > 0) ? resRowsPerCol : 1;
                    int col = SelResult / rpc, row = SelResult % rpc;
                    if (dx < 0 && col == 0) { FocusResults = false; Invalidate(); return; }   // izquierda en 1a columna -> teclado
                    if (dy != 0)
                    {
                        int nr = row + dy; if (nr < 0) nr = 0; if (nr >= rpc) nr = rpc - 1;
                        int ni = col * rpc + nr; if (ni < n) SelResult = ni;
                    }
                    if (dx != 0)
                    {
                        int nc = col + dx; if (nc < 0) nc = 0;
                        int ni = nc * rpc + row; if (ni >= n) ni = n - 1;
                        if (ni >= 0) SelResult = ni;
                    }
                    Invalidate();
                    return;
                }
                if (rows.Count == 0) return;
                if (dy != 0)
                {
                    SelRow += dy;
                    if (SelRow < 0) SelRow = 0;
                    if (SelRow >= rows.Count) SelRow = rows.Count - 1;
                    if (SelCol >= rows[SelRow].Count) SelCol = rows[SelRow].Count - 1;
                }
                if (dx != 0)
                {
                    if (dx > 0 && SelCol >= rows[SelRow].Count - 1 && Results != null && Results.Count > 0)
                    {
                        FocusResults = true; SelResult = 0; Invalidate(); return;
                    }
                    SelCol += dx;
                    if (SelCol < 0) SelCol = 0;
                    if (SelCol >= rows[SelRow].Count) SelCol = rows[SelRow].Count - 1;
                }
                Invalidate();
            }

            public void PressSelected()
            {
                if (FocusResults)
                {
                    if (Results != null && SelResult >= 0 && SelResult < Results.Count && OnChoose != null) OnChoose(Results[SelResult]);
                    return;
                }
                if (SelRow < 0 || SelRow >= rows.Count) return;
                if (SelCol < 0 || SelCol >= rows[SelRow].Count) return;
                Activate(rows[SelRow][SelCol]);
            }

            private void Activate(SearchKey k)
            {
                if (k.Kind == KKind.Char) TypeChar(k.Ch);
                else if (k.Kind == KKind.Space) TypeChar(' ');
                else if (k.Kind == KKind.Back) Backspace();
                else if (k.Kind == KKind.Choose) { if (Results != null && Results.Count > 0 && OnChoose != null) OnChoose(Results[0]); }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (!FocusResults)
                {
                    for (int r = 0; r < rows.Count; r++)
                        for (int c = 0; c < rows[r].Count; c++)
                            if (rows[r][c].R.Contains(e.X, e.Y)) { SelRow = r; SelCol = c; Activate(rows[r][c]); Invalidate(); base.OnMouseClick(e); return; }
                }
                else if (cubeRect != Rectangle.Empty && cubeRect.Contains(e.X, e.Y))
                {
                    FocusResults = false; Invalidate(); base.OnMouseClick(e); return;
                }
                for (int i = 0; i < resultRects.Count; i++)
                    if (resultRects[i].Key.Contains(e.X, e.Y))
                    {
                        int idx = resultRects[i].Value;
                        if (Results != null && idx >= 0 && idx < Results.Count && OnChoose != null) OnChoose(Results[idx]);
                        base.OnMouseClick(e); return;
                    }
                base.OnMouseClick(e);
            }

            protected override void OnPaint(PaintEventArgs e) { RenderContent(e.Graphics, e.ClipRectangle, false); }

            // Dibuja el buscador en cualquier Graphics. forGpu=true: NO pinta el starfield ni el cubo de vista
            // previa (los pone la GPU: starfield de fondo + cubo como geometria); aqui solo la UI (transparente).
            public void RenderContent(Graphics g, Rectangle clip, bool forGpu)
            {
                int W = Width, H = Height;
                if (W < 60 || H < 60) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (forGpu) clip = new Rectangle(0, 0, W, H);

                if (!forGpu)
                {
                    stars.SkyOffsetY = this.Top; stars.SkyFullH = (this.Parent != null) ? this.Parent.ClientSize.Height : H; stars.SkyMid = SkyColorMid(Accent);
                    stars.Dim = BgDim;   // atenuado mientras se navega con musica
                    stars.Draw(g, W, H, SkyColor(Accent), SkyColor2(Accent), Accent, Phase, true, AudioPulse);   // mismo fondo en degradado CONTINUO que el resto + late con la musica
                }

                int M = 30;
                Rectangle qb = new Rectangle(M, M, W - 2 * M, 70);
                Rectangle hintRect = new Rectangle(M, M + 72, W - 2 * M, 26);
                if (qb.IntersectsWith(clip) || hintRect.IntersectsWith(clip))
                {
                    EnsurePaintRes();
                    using (GraphicsPath gp = RoundedPath(qb, 16))
                    {
                        sbS.Color = Shade(Accent, 0.42); g.FillPath(sbS, gp);
                        pnS.Color = Lighten(Accent, 0.35); pnS.Width = 2f; g.DrawPath(pnS, gp);
                    }
                    // En GPU el cursor NO parpadea (fijo): parpadear obligaria a rehacer la textura del teclado
                    // (40 teclas) ~1/seg, carisimo. Fijo => el buscador en reposo no reconstruye nada -> pleno FPS.
                    bool caretOn = forGpu || Math.Sin(Phase * 3.0) > 0;
                    string shown = "Buscar:  " + Query + (caretOn ? "|" : "");
                    TextRenderer.DrawText(g, shown, fQuery, new Rectangle(qb.X + 18, qb.Y, qb.Width - 30, qb.Height), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    {
                        Font hf = fHint;
                        int hcy = hintRect.Y + hintRect.Height / 2;
                        Color htx = Color.FromArgb(214, 222, 242);
                        int hd = 20; float hgap = 7, hgrp = 20;   // MISMO tamano que la barra principal
                        float hx = hintRect.X;
                        // Guarda el centro/radio del halo (para dibujarlo por GPU). En GPU NO se hornea aqui (forGpu);
                        // en CPU si se pinta como siempre. Asi el destello se anima por GPU sin re-subir la textura.
                        StoreHintGlow(SMover, hx, hcy, hd + 2);
                        if (!forGpu) GlowAround(g, hx, hcy, hd + 2, hintFlash.Level(SMover), hintFlash.Col(SMover));   // MOVER = cruceta (color segun direccion)
                        hx = CtrlPad(g, hx, hcy, hd + 2);
                        hx = CtrlTxt(g, hf, "Mover", hx + hgap, hcy, htx) + hgrp;
                        StoreHintGlow(SPoner, hx, hcy, hd);
                        if (!forGpu) GlowAround(g, hx, hcy, hd, hintFlash.Level(SPoner), hintFlash.Col(SPoner));        // PONER/ELEGIR = punto verde
                        hx = CtrlDot(g, hx, hcy, hd, SGreen);
                        hx = CtrlTxt(g, hf, "Poner / Elegir", hx + hgap, hcy, htx) + hgrp;
                        StoreHintGlow(SCerrar, hx, hcy, hd * 2 + 3);
                        if (!forGpu) GlowAround(g, hx, hcy, hd * 2 + 3, hintFlash.Level(SCerrar), hintFlash.Col(SCerrar));   // CERRAR = 2 puntos
                        hx = CtrlDot(g, hx, hcy, hd, SRed) + 3; hx = CtrlDot(g, hx, hcy, hd, SGreen);
                        hx = CtrlTxt(g, hf, "Cerrar", hx + hgap, hcy, htx);
                    }
                }

                int contentY = M + 70 + 28;
                int contentH = H - contentY - M;
                if (contentH < 60) return;
                int kbW = (int)((W - 2 * M) * 0.52);   // teclado mas angosto -> resultados mas anchos (caben 2 columnas)
                Rectangle kbRegion = new Rectangle(M, contentY, kbW, contentH);
                Rectangle resRegion = new Rectangle(M + kbW + 20, contentY, (W - 2 * M) - kbW - 20, contentH);

                bool showCube = FocusResults && Results != null && Results.Count > 0 && GetCover != null;
                if (showCube)
                {
                    if (forGpu)
                    {
                        Image ca, cb; float f; Rectangle area;
                        if (ComputePreview(kbRegion, out ca, out cb, out f, out area)) DrawPreviewInfo(g, kbRegion, area);   // el cubo lo dibuja la GPU; aqui solo el texto/ayuda
                    }
                    else if (kbRegion.IntersectsWith(clip)) DrawPreviewCube(g, kbRegion);
                }
                else
                {
                    cubeRect = Rectangle.Empty;
                    LayoutKeys(kbRegion);
                    if (kbRegion.IntersectsWith(clip)) DrawKeys(g, kbRegion);
                }
                if (resRegion.IntersectsWith(clip)) DrawResults(g, resRegion);

                if (ToastOn) DrawToast(g, W, H);
            }

            private void DrawToast(Graphics g, int W, int H)
            {
                int tw = 400, th = 56;
                Rectangle r = new Rectangle((W - tw) / 2, 104, tw, th);
                EnsurePaintRes();
                using (GraphicsPath gp = RoundedPath(r, 16))
                {
                    sbS.Color = ToastBack; g.FillPath(sbS, gp);
                    pnS.Color = Lighten(ToastBack, 0.45); pnS.Width = 2.4f; g.DrawPath(pnS, gp);
                }
                TextRenderer.DrawText(g, ToastMsg, fToast, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            private void LayoutKeys(Rectangle region)
            {
                int rowsN = rows.Count;
                if (rowsN == 0) return;
                float gap = 6;
                float rowH = (region.Height - gap * (rowsN - 1)) / rowsN;
                int cols = 10;
                float keyW = (region.Width - gap * (cols - 1)) / cols;
                for (int r = 0; r < rows.Count; r++)
                {
                    float y = region.Y + r * (rowH + gap);
                    if (r < 4)
                    {
                        int c = rows[r].Count;
                        float totalW = c * keyW + gap * (c - 1);
                        float x0 = region.X + (region.Width - totalW) / 2f;
                        for (int c2 = 0; c2 < c; c2++)
                            rows[r][c2].R = new RectangleF(x0 + c2 * (keyW + gap), y, keyW, rowH);
                    }
                    else
                    {
                        int n = rows[r].Count;
                        float[] frac;
                        if (n == 2) frac = new float[] { 0.60f, 0.40f };          // ESPACIO mas ancho, BORRAR
                        else if (n == 3) frac = new float[] { 0.40f, 0.28f, 0.32f };
                        else { frac = new float[n]; for (int q = 0; q < n; q++) frac[q] = 1f / n; }
                        float avail = region.Width - gap * (n - 1);
                        float x = region.X;
                        for (int c2 = 0; c2 < n; c2++)
                        {
                            float fw = avail * frac[c2];
                            rows[r][c2].R = new RectangleF(x, y, fw, rowH);
                            x += fw + gap;
                        }
                    }
                }
            }

            private void DrawKeys(Graphics g, Rectangle region)
            {
                if (rows.Count == 0 || rows[0].Count == 0) return;
                float kh = rows[0][0].R.Height;
                if (kh < 8) kh = 8;
                EnsurePaintRes();
                if (fChar == null || fKh != kh)   // fuentes del teclado: solo se recrean si cambia su altura
                {
                    if (fChar != null) fChar.Dispose();
                    if (fSp != null) fSp.Dispose();
                    fChar = new Font(FontFamily.GenericSansSerif, Math.Max(12f, kh * 0.42f), FontStyle.Bold);
                    fSp = new Font(FontFamily.GenericSansSerif, Math.Max(9f, kh * 0.24f), FontStyle.Bold);
                    fKh = kh;
                }
                if (region.Width < 2 || region.Height < 2) return;
                // CACHE de las teclas SIN seleccion: se rehace solo si cambia tamano/altura/color (casi nunca).
                // Dibujar 40 teclas (degradado + contorno + texto) en cada cuadro era lo que hundia el FPS del
                // buscador; ahora se copia el cache + SOLO la tecla seleccionada en vivo (barato).
                int accentArgb = Accent.ToArgb();
                if (kbCache == null || kbCacheW != region.Width || kbCacheH != region.Height || kbCacheAccent != accentArgb || kbCacheKh != kh)
                {
                    if (kbCache == null || kbCache.Width != region.Width || kbCache.Height != region.Height)   // REUSA si el tamano no cambio (p.ej. solo cambio el color)
                    {
                        if (kbCache != null) kbCache.Dispose();
                        kbCache = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppPArgb);
                    }
                    using (Graphics cg = Graphics.FromImage(kbCache))
                    {
                        cg.Clear(Color.Transparent);
                        cg.SmoothingMode = SmoothingMode.AntiAlias;
                        for (int r = 0; r < rows.Count; r++)
                            for (int c = 0; c < rows[r].Count; c++)
                                DrawOneKey(cg, r, c, false, -region.X, -region.Y);   // TODAS sin seleccion, en coords del bitmap
                    }
                    kbCacheW = region.Width; kbCacheH = region.Height; kbCacheAccent = accentArgb; kbCacheKh = kh; kbCacheOrigin = region.Location;
                }
                g.DrawImageUnscaled(kbCache, kbCacheOrigin.X, kbCacheOrigin.Y);
                // Tecla seleccionada EN VIVO encima: su relleno es opaco y tapa la version sin seleccion del cache.
                if (SelRow >= 0 && SelRow < rows.Count && SelCol >= 0 && SelCol < rows[SelRow].Count)
                    DrawOneKey(g, SelRow, SelCol, true, 0, 0);
            }

            // Dibuja UNA tecla. dx/dy desplaza el rect (para el cache: coords relativas al bitmap). Se usa
            // Offset directo -en vez de TranslateTransform del Graphics- porque TextRenderer (GDI) IGNORA la
            // transformacion y el texto caeria fuera del cache.
            private void DrawOneKey(Graphics g, int r, int c, bool sel, int dx, int dy)
            {
                SearchKey k = rows[r][c];
                Rectangle kr = Rectangle.Round(k.R); kr.Offset(dx, dy);
                if (kr.Width < 2 || kr.Height < 2) return;
                double kp = 0.5 + 0.5 * Math.Sin(Phase * 2.2 - (r * 0.55 + c * 0.32));
                using (GraphicsPath gp = RoundedPath(kr, 10))
                {
                    Color top = sel ? Lighten(Accent, 0.32) : Lighten(Shade(Accent, 0.34), 0.10 + 0.14 * kp);
                    Color bot = sel ? Accent : Shade(Accent, 0.55);
                    using (LinearGradientBrush b = new LinearGradientBrush(kr, top, bot, LinearGradientMode.Vertical)) g.FillPath(b, gp);
                    if (sel)
                    {
                        int a = 130 + (int)((0.5 + 0.5 * Math.Sin(Phase * 2.6)) * 120);
                        pnS.Color = Color.FromArgb(a, 170, 215, 255); pnS.Width = 2.8f; g.DrawPath(pnS, gp);
                    }
                    else
                    {
                        int a = 85 + (int)(kp * 130);
                        pnS.Color = Color.FromArgb(a, 150, 200, 255); pnS.Width = 1.7f; g.DrawPath(pnS, gp);
                    }
                }
                Font use = (k.Kind == KKind.Char) ? fChar : fSp;
                TextRenderer.DrawText(g, k.Label, use, kr, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            private void DrawResults(Graphics g, Rectangle region)
            {
                resultRects.Clear();
                EnsurePaintRes();
                int top = region.Y + 30;
                int areaH = region.Bottom - top;
                int n = 0, cardH = 38;
                if (areaH < 28) { resCols = 1; resRowsPerCol = 1; }
                else
                {
                    int gapY = 5, gapX = 8, colMinW = 185;
                    int cols = (region.Width + gapX) / (colMinW + gapX);
                    if (cols < 1) cols = 1; if (cols > 2) cols = 2;            // hasta 2 columnas en este panel
                    int colW = (region.Width - (cols - 1) * gapX) / cols;
                    int rowsPerCol = (areaH + gapY) / (cardH + gapY);
                    if (rowsPerCol < 1) rowsPerCol = 1;
                    resCols = cols; resRowsPerCol = rowsPerCol;
                    int capacity = cols * rowsPerCol;
                    n = (Results != null) ? Math.Min(Results.Count, capacity) : 0;
                    if (FocusResults && n > 0) { if (SelResult < 0) SelResult = 0; if (SelResult >= n) SelResult = n - 1; }
                    for (int i = 0; i < n; i++)   // solo geometria (barato): rects absolutos para el raton y para pintar
                    {
                        int col = i / rowsPerCol, row = i % rowsPerCol;   // llena hacia ABAJO y luego a la siguiente columna
                        int rx = region.X + col * (colW + gapX), ry = top + row * (cardH + gapY);
                        resultRects.Add(new KeyValuePair<RectangleF, int>(new Rectangle(rx, ry, colW, cardH), i));
                    }
                }
                // CACHE: cabecera + tarjetas SIN seleccion. Se rehace solo al cambiar la consulta/region/color;
                // moverse por los resultados NO lo toca (solo copia el cache + la tarjeta seleccionada en vivo).
                int accentArgb = Accent.ToArgb();
                if (resCache == null || resCacheW != region.Width || resCacheH != region.Height || resCacheAccent != accentArgb || resCacheQuery != Query)
                {
                    int rw = Math.Max(1, region.Width), rh = Math.Max(1, region.Height);
                    if (resCache == null || resCache.Width != rw || resCache.Height != rh)   // REUSA el bitmap si el tamano no cambio
                    {
                        if (resCache != null) resCache.Dispose();   // solo se reasigna al cambiar de tamano, no en cada tecla (menos basura/tirones)
                        resCache = new Bitmap(rw, rh, PixelFormat.Format32bppPArgb);
                    }
                    using (Graphics cg = Graphics.FromImage(resCache))
                    {
                        cg.Clear(Color.Transparent);
                        cg.SmoothingMode = SmoothingMode.AntiAlias;
                        TextRenderer.DrawText(cg, "Resultados", fHead, new Rectangle(0, 0, region.Width, 24), Color.White, TextFormatFlags.Left);
                        if (n == 0)
                            TextRenderer.DrawText(cg, (Query.Length == 0) ? "Escribe para buscar..." : "Sin resultados", fEmpty, new Rectangle(0, 30, region.Width, 28), Color.FromArgb(190, 200, 225), TextFormatFlags.Left);
                        else
                            for (int i = 0; i < n; i++)
                            {
                                Rectangle rr = Rectangle.Round(resultRects[i].Key); rr.Offset(-region.X, -region.Y);
                                DrawOneResultCard(cg, i, rr, false);
                            }
                    }
                    resCacheW = region.Width; resCacheH = region.Height; resCacheAccent = accentArgb; resCacheQuery = Query; resCacheOrigin = region.Location;
                }
                g.DrawImageUnscaled(resCache, resCacheOrigin.X, resCacheOrigin.Y);
                // Tarjeta seleccionada EN VIVO encima: su relleno es opaco y tapa la version sin seleccion del cache.
                if (n > 0)
                {
                    int selIdx = FocusResults ? SelResult : 0;
                    if (selIdx >= 0 && selIdx < n) DrawOneResultCard(g, selIdx, Rectangle.Round(resultRects[selIdx].Key), true);
                }
            }

            // Dibuja UNA tarjeta de resultado en rr (ya en las coords donde toca: absolutas en vivo, relativas en el cache).
            private void DrawOneResultCard(Graphics g, int i, Rectangle rr, bool sel)
            {
                if (Results == null || i < 0 || i >= Results.Count) return;
                using (GraphicsPath gp = RoundedPath(rr, 8))
                {
                    sbS.Color = sel ? Shade(Accent, 0.26) : Shade(Accent, 0.50); g.FillPath(sbS, gp);
                    pnS.Color = Color.FromArgb(sel ? 215 : 100, 160, 200, 245); pnS.Width = sel ? 2f : 1.1f; g.DrawPath(pnS, gp);
                }
                SearchItem it = Results[i];
                string tag = (it.Kind == 0) ? "ARTISTA" : (it.Kind == 1) ? "ALBUM" : "CANCION";
                Color tagc = (it.Kind == 0) ? Color.FromArgb(150, 215, 255) : (it.Kind == 1) ? Color.FromArgb(255, 210, 120) : Color.FromArgb(170, 245, 170);
                TextRenderer.DrawText(g, tag, fResSub, new Rectangle(rr.X + 9, rr.Y + 4, rr.Width - 14, 13), tagc, TextFormatFlags.Left);
                TextRenderer.DrawText(g, it.Display, fRes, new Rectangle(rr.X + 9, rr.Y + 16, rr.Width - 16, rr.Height - 18), Color.White, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }

            // Cachea las caratulas (ya redimensionadas) de todos los discos del artista
            // Reconstruye SIEMPRE: pide las portadas vigentes a la cache (nunca guarda una
            // referencia que pudiera liberarse despues -> evita dibujar imagenes muertas).
            private List<Image> EnsureArtistCovers(SearchItem it)
            {
                artistCovers.Clear();
                if (it.Albums != null && GetCover != null)
                    for (int i = 0; i < it.Albums.Count; i++)
                    {
                        Image im = GetCover(it.Albums[i]);
                        if (im != null) artistCovers.Add(im);
                    }
                return artistCovers;
            }

            // Mezcla a->b con factor f (0..1) en un bitmap reutilizable
            private Image BlendInto(Image a, Image b, float f)
            {
                if (blendBmp == null) blendBmp = new Bitmap(320, 320, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(blendBmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    Rectangle dst = new Rectangle(0, 0, blendBmp.Width, blendBmp.Height);
                    if (a != null) g.DrawImage(a, dst);
                    if (b != null && f > 0f)
                    {
                        ColorMatrix cm = new ColorMatrix();
                        cm.Matrix33 = f;
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.DrawImage(b, dst, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                }
                return blendBmp;
            }

            // Calcula la(s) portada(s) y la celda del cubo de vista previa (sin dibujarlo). ca/cb/f = par a
            // fundir (discos TOP/artista con varias portadas). Devuelve false si no hay nada que mostrar.
            private bool ComputePreview(Rectangle region, out Image ca, out Image cb, out float f, out Rectangle area)
            {
                ca = null; cb = null; f = 0f; area = Rectangle.Empty;
                int n = (Results != null) ? Results.Count : 0;
                if (n == 0 || SelResult < 0 || SelResult >= n) { cubeRect = Rectangle.Empty; return false; }
                SearchItem it = Results[SelResult];
                if (it.Kind == 0)
                {
                    List<Image> cov = EnsureArtistCovers(it);
                    if (cov.Count == 0) ca = (GetCover != null && it.AlbumRef != null) ? GetCover(it.AlbumRef) : null;
                    else if (cov.Count == 1) ca = cov[0];
                    else
                    {
                        double cyc = 3.8, hold = 2.6;   // ~3.2s por caratula, funde ~1s
                        int m = cov.Count;
                        double total = cyc * m;
                        double pos = Phase - Math.Floor(Phase / total) * total;
                        int idx = (int)(pos / cyc) % m;
                        double within = pos - idx * cyc;
                        float ff = 0f;
                        if (within > hold) ff = (float)((within - hold) / (cyc - hold));
                        if (ff < 0f) ff = 0f; else if (ff > 1f) ff = 1f;
                        ca = cov[idx]; cb = cov[(idx + 1) % m]; f = ff;
                    }
                }
                else ca = (GetCover != null && it.AlbumRef != null) ? GetCover(it.AlbumRef) : null;

                int side = (int)(Math.Min(region.Width, region.Height) * 0.62);
                if (side < 40) side = 40;
                int cx = region.X + region.Width / 2;
                int top = region.Y + (region.Height - side) / 2 - 24;
                if (top < region.Y + 6) top = region.Y + 6;
                area = new Rectangle(cx - side / 2, top, side, side);
                cubeRect = area;
                return true;
            }

            // CPU: dibuja el cubo de vista previa (girando) + su texto.
            private void DrawPreviewCube(Graphics g, Rectangle region)
            {
                Image ca, cb; float f; Rectangle area;
                if (!ComputePreview(region, out ca, out cb, out f, out area)) return;
                Image cover = (cb == null || f <= 0.002f) ? ca : BlendInto(ca, cb, f);
                g.InterpolationMode = InterpolationMode.Bilinear;
                Cube.Draw(g, area, cover, CubeAngle);
                DrawPreviewInfo(g, region, area);
            }

            // Texto de la vista previa (titulo, subtitulo y ayuda) debajo del cubo. Comun a CPU y GPU.
            private void DrawPreviewInfo(Graphics g, Rectangle region, Rectangle area)
            {
                int n = (Results != null) ? Results.Count : 0;
                if (SelResult < 0 || SelResult >= n) return;
                SearchItem it = Results[SelResult];
                string title, sub;
                if (it.Kind == 1) { title = (it.AlbumRef != null) ? it.AlbumRef.Name : it.Display; sub = (it.AlbumRef != null) ? it.AlbumRef.Artist : ""; }
                else if (it.Kind == 2 && it.TrackRef != null) { title = it.TrackRef.Title; sub = it.TrackRef.Artist + ((it.AlbumRef != null) ? "   -   " + it.AlbumRef.Name : ""); }
                else { title = it.Display; int dc = (it.Albums != null) ? it.Albums.Count : 0; sub = dc + (dc == 1 ? " disco" : " discos"); }

                int ty = area.Bottom + 12;
                EnsurePaintRes();
                TextRenderer.DrawText(g, title, fPrevT, new Rectangle(region.X, ty, region.Width, 28), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, sub, fPrevS, new Rectangle(region.X, ty + 30, region.Width, 24), Color.FromArgb(215, 225, 245), TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                // Ayuda con ICONOS (mismo estilo que la barra de arriba), centrada.
                {
                    Font hf = fPrevH;
                    SmoothingMode oldsm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
                    System.Drawing.Text.TextRenderingHint oldth = g.TextRenderingHint;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    Color htx = Color.FromArgb(205, 214, 238);
                    Color yellow = Color.FromArgb(242, 206, 72), green = Color.FromArgb(72, 212, 112);
                    int hcy = region.Bottom - 14;
                    int dd = 15; float gap = 7, grp = 26;
                    string t1 = "volver al teclado", t2 = "elegir";
                    SizeF s1 = g.MeasureString(t1, hf), s2 = g.MeasureString(t2, hf);
                    float total = dd + gap + s1.Width + grp + dd + gap + s2.Width;
                    float hx = region.X + (region.Width - total) / 2f;
                    hx = CtrlDirLeft(g, hx, hcy, dd, yellow) + gap;   // boton IZQUIERDA
                    hx = CtrlTxt(g, hf, t1, hx, hcy, htx) + grp;
                    hx = CtrlDot(g, hx, hcy, dd, green) + gap;         // boton PONER (verde)
                    hx = CtrlTxt(g, hf, t2, hx, hcy, htx);
                    g.SmoothingMode = oldsm; g.TextRenderingHint = oldth;
                }
            }
        }

        private class SearchItem
        {
            public int Kind;     // 0=artista, 1=album, 2=cancion
            public string Display;
            public string Key;
            public int Score;
            public Album AlbumRef;
            public Track TrackRef;
            public List<Album> Albums;   // todos los discos del artista (Kind==0)
        }

        private class Album
        {
            public string Folder;
            public string Name;
            public string Artist;
            public char Letter;
            public string CoverPath;
            public List<Track> Tracks = new List<Track>();
            public List<string> VideoFiles = new List<string>();
            public bool IsTop = false;             // disco virtual de "lo mas sonado" por genero
            public bool TopIsVideo = false;        // (TOP) este disco virtual contiene VIDEOS, no canciones
            public string KeyCache, KeyFrom;       // clave de la casilla ya armada (ver CellKey): se pide en cada cuadro
            public List<string> CycleCovers;       // (TOP) caratulas distintas para ciclar en el cubo seleccionado
            public List<string> TrackCovers;       // (TOP) caratula de origen por cada cancion (alineado con Tracks)
            public List<string> TrackInfo;         // (TOP) "Artista - Album" de origen por cada cancion
            public List<Album> TrackAlbums;        // (TOP) disco de origen por cada cancion (para "sonando")
        }

        // ==========  ETIQUETAS (ID3)  ==========
        private class Track
        {
            public string Path;
            public string Artist;
            public string Title;
            public string Album;
            public string Genre;

            // En la lista del disco se muestra EXACTAMENTE el nombre del archivo (sin extension), tal
            // cual esta en la carpeta: cero adivinanzas. Title/Artist se siguen usando por dentro para
            // "Sonando:", el color por genero y el buscador.
            public override string ToString()
            {
                try { return System.IO.Path.GetFileNameWithoutExtension(Path); }
                catch { return Title; }
            }

            private static readonly string[] Id3v1Genres = {
                "Blues","Classic Rock","Country","Dance","Disco","Funk","Grunge","Hip-Hop","Jazz","Metal",
                "New Age","Oldies","Other","Pop","R&B","Rap","Reggae","Rock","Techno","Industrial",
                "Alternative","Ska","Death Metal","Pranks","Soundtrack","Euro-Techno","Ambient","Trip-Hop","Vocal","Jazz+Funk",
                "Fusion","Trance","Classical","Instrumental","Acid","House","Game","Sound Clip","Gospel","Noise",
                "AlternRock","Bass","Soul","Punk","Space","Meditative","Instrumental Pop","Instrumental Rock","Ethnic","Gothic",
                "Darkwave","Techno-Industrial","Electronic","Pop-Folk","Eurodance","Dream","Southern Rock","Comedy","Cult","Gangsta",
                "Top 40","Christian Rap","Pop/Funk","Jungle","Native American","Cabaret","New Wave","Psychadelic","Rave","Showtunes",
                "Trailer","Lo-Fi","Tribal","Acid Punk","Acid Jazz","Polka","Retro","Musical","Rock & Roll","Hard Rock"
            };

            private static string NormalizeGenre(string raw)
            {
                if (string.IsNullOrEmpty(raw)) return "";
                raw = raw.Trim();
                if (raw.Length > 0 && raw[0] == '(')
                {
                    int close = raw.IndexOf(')');
                    if (close > 1)
                    {
                        string num = raw.Substring(1, close - 1);
                        int n;
                        if (int.TryParse(num, out n) && n >= 0 && n < Id3v1Genres.Length)
                        {
                            string rest = raw.Substring(close + 1).Trim();
                            if (rest.Length > 0) return rest;
                            return Id3v1Genres[n];
                        }
                    }
                }
                int num2;
                if (int.TryParse(raw, out num2) && num2 >= 0 && num2 < Id3v1Genres.Length) return Id3v1Genres[num2];
                return raw;
            }

            // RAPIDO: no abre el archivo. Titulo/artista salen del nombre y la carpeta.
            // (Con +100k canciones, abrir cada MP3 para leer ID3 era lo que tardaba.)
            public static Track FromFile(string path)
            {
                Track t = new Track();
                t.Path = Pool(path); t.Artist = ""; t.Title = ""; t.Album = ""; t.Genre = "";
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                string fa, ft;
                bool split = SplitArtist(name, out fa, out ft);
                if (split && AllDigits(fa))
                {
                    // "01 - X - Y": quitar el numero de pista y reanalizar -> artista X, titulo Y.
                    string fa2, ft2;
                    if (SplitArtist(ft, out fa2, out ft2) && !AllDigits(fa2)) { t.Artist = fa2; t.Title = ft2; }
                    else t.Title = name;   // "01 - Titulo": igual que siempre
                }
                else if (split) { t.Artist = fa; t.Title = ft; }
                else { t.Title = name; }
                if (string.IsNullOrEmpty(t.Artist))
                {
                    string d = System.IO.Path.GetDirectoryName(path);
                    string leaf = (d != null) ? LeafName(d) : "Desconocido";
                    string da, dn;
                    if (SplitArtist(leaf, out da, out dn)) t.Artist = da; else t.Artist = leaf;
                }
                return t;
            }

            // Lee el ID3 bajo demanda (una sola vez), sobre todo para el GENERO (color de fondo).
            private bool tagsLoaded = false;
            public void EnsureTags()
            {
                if (tagsLoaded) return;
                tagsLoaded = true;
                try { if (System.IO.Path.GetExtension(Path).ToLowerInvariant() == ".mp3") ReadId3(Path, this); }
                catch { }
            }

            private static void ReadId3(string path, Track t)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[10];
                    if (fs.Read(header, 0, 10) == 10 && header[0] == (byte)'I' && header[1] == (byte)'D' && header[2] == (byte)'3')
                    {
                        int major = header[3];
                        int size = ((header[6] & 0x7f) << 21) | ((header[7] & 0x7f) << 14) | ((header[8] & 0x7f) << 7) | (header[9] & 0x7f);
                        if (size > 0 && size <= 33554432)   // tope 32MB: un encabezado CORRUPTO puede declarar hasta 256MB y ese new byte[] tumbaria la RAM de la rocola; se cae al ID3v1
                        {
                            byte[] tag = new byte[size];
                            int read = 0, r;
                            while (read < size && (r = fs.Read(tag, read, size - read)) > 0) read += r;
                            ParseId3v2(tag, read, major, t);
                        }
                    }
                }
                if (string.IsNullOrEmpty(t.Title) || string.IsNullOrEmpty(t.Artist) || string.IsNullOrEmpty(t.Album) || string.IsNullOrEmpty(t.Genre)) ReadId3v1(path, t);
            }

            private static void ParseId3v2(byte[] tag, int len, int major, Track t)
            {
                int pos = 0;
                while (pos + 10 <= len)
                {
                    string id = Encoding.ASCII.GetString(tag, pos, 4);
                    if (id[0] == '\0') break;
                    bool valid = true;
                    for (int i = 0; i < 4; i++) { char c = id[i]; if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) { valid = false; break; } }
                    if (!valid) break;
                    int frameSize;
                    if (major >= 4) frameSize = ((tag[pos + 4] & 0x7f) << 21) | ((tag[pos + 5] & 0x7f) << 14) | ((tag[pos + 6] & 0x7f) << 7) | (tag[pos + 7] & 0x7f);
                    else frameSize = (tag[pos + 4] << 24) | (tag[pos + 5] << 16) | (tag[pos + 6] << 8) | tag[pos + 7];
                    int startPos = pos + 10;
                    if (frameSize <= 0 || startPos + frameSize > len) break;
                    if (id == "TIT2" || id == "TPE1" || id == "TALB" || id == "TCON")
                    {
                        string val = DecodeText(tag, startPos, frameSize);
                        if (val != null) val = val.Trim();
                        bool has = !string.IsNullOrEmpty(val);
                        // El ID3 MANDA sobre titulo/artista: el nombre de archivo es solo una adivinanza
                        // (en discos de varios artistas suele venir al reves, o sin titulo siquiera).
                        if (id == "TIT2" && has) t.Title = val;
                        else if (id == "TPE1" && has) t.Artist = val;
                        else if (id == "TALB" && has && string.IsNullOrEmpty(t.Album)) t.Album = val;
                        else if (id == "TCON" && has && string.IsNullOrEmpty(t.Genre)) t.Genre = NormalizeGenre(val);
                    }
                    pos = startPos + frameSize;
                }
            }

            private static string DecodeText(byte[] b, int start, int size)
            {
                if (size < 1) return "";
                byte enc = b[start];
                int s = start + 1, n = size - 1;
                string result;
                switch (enc)
                {
                    case 1:
                        if (n >= 2 && b[s] == 0xFF && b[s + 1] == 0xFE) result = Encoding.Unicode.GetString(b, s + 2, n - 2);
                        else if (n >= 2 && b[s] == 0xFE && b[s + 1] == 0xFF) result = Encoding.BigEndianUnicode.GetString(b, s + 2, n - 2);
                        else result = Encoding.Unicode.GetString(b, s, n);
                        break;
                    case 2: result = Encoding.BigEndianUnicode.GetString(b, s, n); break;
                    case 3: result = Encoding.UTF8.GetString(b, s, n); break;
                    default: result = Encoding.GetEncoding(28591).GetString(b, s, n); break;
                }
                int z = result.IndexOf('\0');
                if (z >= 0) result = result.Substring(0, z);
                return result.Trim();
            }

            private static void ReadId3v1(string path, Track t)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < 128) return;
                    fs.Seek(-128, SeekOrigin.End);
                    byte[] b = new byte[128];
                    if (fs.Read(b, 0, 128) != 128) return;
                    if (!(b[0] == (byte)'T' && b[1] == (byte)'A' && b[2] == (byte)'G')) return;
                    Encoding latin = Encoding.GetEncoding(28591);
                    char[] tc = new char[] { '\0', ' ' };
                    string title = latin.GetString(b, 3, 30).Trim(tc);
                    string artist = latin.GetString(b, 33, 30).Trim(tc);
                    string album = latin.GetString(b, 63, 30).Trim(tc);
                    if (string.IsNullOrEmpty(t.Title) && title.Length > 0) t.Title = title;
                    if (string.IsNullOrEmpty(t.Artist) && artist.Length > 0) t.Artist = artist;
                    if (string.IsNullOrEmpty(t.Album) && album.Length > 0) t.Album = album;
                    int gi = b[127];
                    if (string.IsNullOrEmpty(t.Genre) && gi >= 0 && gi < Id3v1Genres.Length) t.Genre = Id3v1Genres[gi];
                }
            }
        }
    }
}
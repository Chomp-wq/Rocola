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
        private AlbumView albumView;
        private Label lblStatus, lblNow, lblNext, lblTime, lblAlbum, lblToast;
        private PastillaView pastCred, pastCola;   // creditos y cola, en capsula
        private GeneroView pastGenero;             // de que genero es lo que suena, detras del artista
        private HeaderView headerView;   // nombre de la seccion + en que pagina estas
        // Linea de ARRIBA de "Reproduciendo": vacia casi siempre. Cuando el titulo no cabe en una sola
        // linea, la primera mitad se pinta aqui y el resto en lblNow (que sigue siendo la ULTIMA linea, la
        // que lleva el letrero MEJORADA detras). El hueco esta reservado SIEMPRE, asi que el acomodo de la
        // pantalla no se mueve nunca al cambiar de cancion: nada de rehornear la rejilla por un titulo largo.
        private Label lblNowTop;
        private string nowFullText = "Nada reproduciendose";   // el TITULO de lo que suena
        private string nowArtista = "";                        // y de quien es
        private HelpBar helpBar;
        private BgPanel panelSongs;
        private SongListView songList;
        private CubeBox cubeBox;
        private ProgressView progress;
        private System.Windows.Forms.Timer progressTimer, animTimer, toastTimer;
        private System.Windows.Forms.Timer focusTimer;                 // vigia de foco al arrancar (autoarranque)
        // F10: pausa de mantenimiento del vigia de foco. ENCENDIDO O APAGADO, sin reloj: antes se
        // reactivaba sola a la media hora "por si se olvida", y eso mordia justo cuando hacia falta -en
        // mitad de un mantenimiento largo la rocola volvia a robar el foco sin avisar, y con las manos
        // dentro de otra ventana eso es peor que dejarla pausada-. Ahora se queda como se dejo hasta que
        // se vuelva a pulsar F10. Al arrancar SIEMPRE vuelve encendida (ver mas abajo), asi que una pausa
        // olvidada no sobrevive a un reinicio.
        private bool maintMode = false;

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
        private Stack<float[]> levelBuffers;   // acceso solo bajo levelLock; hasta 96 espectros libres
        private const int AudioVisualDelayMs = 850;   // colchon de WaveOut (5 x 200ms; en regimen va ~4 buffers adelantado)
        private float fLp1, fLp2;                      // estado de los filtros paso-bajo (hilo de audio) que separan las bandas
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
        // TOPE DE CUADROS POR SEGUNDO del fondo con tarjeta grafica ("tope_fps=" en config). En 0 -lo normal-
        // lo decide el propio panel: lo que refresque la pantalla y un poco mas (ver GpuPanel.TopeMs). Se deja
        // a mano por si alguna pantalla contesta un refresco raro y hay que fijarlo.
        private int fpsTope = 0;
        private Label lblMejorada;           // letrero "MEJORADA" dorado junto a "Reproduciendo": brilla cuando la cancion suena con DSEE
        private int mejRowW = 120;           // ancho total de la fila "Reproduciendo" (lo fija el layout; el letrero se acomoda dentro)
        // EN QUE ESTA EL LETRERO MEJORADA, GUARDADO. Antes se preguntaba (MejActivo) alli donde hiciera
        // falta, y el panel de reposo lo preguntaba dos veces y media por segundo. Ahora se apunta cuando
        // CAMBIA -que es lo que ya miraba TickMejorada- y quien lo necesite lo lee de aqui.
        // 0 = sin letrero, 2 = dorado (el DSEE espectral reconstruyendo de verdad).
        private int iaBadgeKind;
        private static readonly Color MejGoldBase = Color.FromArgb(238, 192, 76), MejGoldHi = Color.FromArgb(255, 233, 145);   // dorado: base y brillo
        private SpectralTap curSbr;          // tap de la cancion actual (para alternar A/B en vivo)
        private const float NormMaxBoost = 16.0f;                                             // techo ABSOLUTO de subida (+24 dB): evita amplificar al infinito una cancion casi muda (el tope real lo pone la etapa, ver TierMaxDb)
        private const float NormTargetMin = 0.06f, NormTargetMax = 0.40f;                     // rango del volumen objetivo que ofrece la interfaz (de flojo a muy fuerte)
        // Minimo de musica oida para aceptar una medida (si se salta antes, no se guarda). En SEGUNDOS: antes
        // eran 800.000 muestras, que son 9 s en estereo a 44,1 kHz pero 18 s en mono y 4,2 s en estereo a 96 kHz.
        private const double NormMinSec = 800000.0 / (2 * 44100.0);
        // ARCHIVOS INCOMPATIBLES: canciones que NO se pudieron reproducir ni con el lector tolerante (p.ej. MP3
        // demasiado dañados). Se anotan (una vez) en rocola_incompatibles.txt para poder repararlas/reconvertirlas.
        private HashSet<string> incompatibleLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // ----- PAGINAS de canciones (vista de disco): la lista muestra solo la pagina actual -----
        private SongPagerBar songPager;                        // barra "pagina X de Y" con flechas, arriba de la lista
        private List<object> songItems = new List<object>();   // TODAS las canciones/videos del disco abierto
        private int songSel = 0;                               // seleccion global (entre todas las paginas)
        private readonly List<PlayItem> queue = new List<PlayItem>();
        // DONDE VA CADA CANCION PEDIDA. Se consulta una vez por fila al dibujar la lista de un disco, asi que
        // no puede ser un recorrido de la cola: es un indice ruta -> puesto que se rehace SOLO cuando la cola
        // cambia. 'colaVer' sube en cada cambio y entra en la firma de la textura, para que la marca "EN COLA"
        // aparezca en cuanto pides la cancion y no la proxima vez que algo mas se mueva.
        // La que SUENA AHORA entra aqui tambien, con puesto -1: ya no esta en la cola (se saca al empezar a
        // sonar), asi que sin esto era la unica cancion del disco SIN ninguna marca, y es justo la que mas
        // hace falta marcar -si al cliente se le olvido que la acaba de poner, se gasta otro credito en ella-.
        private readonly Dictionary<string, int> colaPos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // De que DISCOS hay algo pedido (o sonando), por carpeta. Lo usa la marca de la pared.
        private readonly Dictionary<string, bool> colaDiscos = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static bool turnoOn = true;       // "turno=" en config
        private static bool marcaColaOn = true;   // "marca_cola=" en config
        private static bool colaPortadasOn = true;   // "cola_portadas=" en config
        // Y APARTE, cual esta sonando. Suelto y no dentro de colaPos porque una cancion puede estar sonando
        // Y ademas pedida otra vez: son dos cosas distintas y el cliente necesita ver las dos -que la esta
        // oyendo, y que ya la va a volver a oir-. Metidas en el mismo dato, una tapaba a la otra.
        private string sonandoKey = "";
        private int colaVer;
        private void RefrescaCola()
        {
            // El panel de reposo ensena la cancion y las de la cola, asi que se entera aqui. Este aviso
            // cubre las dos cosas de una: los tres sitios que cambian nowPlaying llaman a RefrescaCola en
            // la linea de al lado, a proposito, para que la marca de "SONANDO" salga en la misma pasada.
            CambioElPanelDeReposo();
            colaPos.Clear();
            // Y APARTE, DE QUE DISCOS. Un cliente recorre la pared, pide tres canciones y a los cinco
            // minutos ya no se acuerda de cuales: vuelve a pedir la misma. Con una marca en la esquina
            // del disco no hace falta acordarse. Se guarda por carpeta y no por el objeto del disco,
            // porque un re-escaneo de la biblioteca fabrica objetos nuevos para los mismos discos.
            colaDiscos.Clear();
            if (nowPlaying != null && nowPlaying.Disc != null && nowPlaying.Disc.Folder != null)
                colaDiscos[nowPlaying.Disc.Folder] = true;   // la que suena tambien: sigue siendo "ya la pusiste"
            for (int i = 0; i < queue.Count; i++)
            {
                PlayItem it = queue[i];
                if (it == null) continue;
                string k = it.PathKey();
                if (k.Length > 0 && !colaPos.ContainsKey(k)) colaPos[k] = i + 1;   // el primero manda
                if (it.Disc != null && it.Disc.Folder != null) colaDiscos[it.Disc.Folder] = true;
            }
            // Y aparte, cual esta sonando. Aparte y no aqui dentro porque las dos cosas pueden ser verdad a
            // la vez -la que suena puede estar pedida otra vez- y el cliente necesita ver LAS DOS marcas.
            sonandoKey = (nowPlaying != null) ? nowPlaying.PathKey() : "";
            colaVer++;
#if ROCOLA_GPU
            ActualizaEsperas(true);   // "EN COLA 3 · en ~9 min": con la cola nueva, antes de volver a hornear la lista
#endif
            // Y la lista del disco tiene que volver a hornearse: sus marcas (verde y ambar) van DENTRO del
            // mapa de bits horneado, asi que sin este aviso la cancion recien pedida seguiria saliendo sin
            // marca hasta que algo mas obligara a rehacerla.
            RefrescaLista();
            // Lo mismo para los resultados del buscador, que tambien van horneados a un mapa de bits: si no,
            // pedir una cancion desde el buscador no cambiaria nada de lo que se ve hasta teclear otra letra.
            if (searchOverlay != null)
            {
                searchOverlay.ColaVer = colaVer;
                if (searchOpen) searchOverlay.Invalidate();
            }
        }
        // LAS TRES PRIMERAS DE LA COLA, para la capsula. Se piden con LoadCover, que devuelve lo que ya
        // esta en memoria y encarga al disco lo que no; cuando lo que faltaba llegue, ApplyArrivedCover
        // vuelve a pasar por aqui y la portadita aparece sola.
        private void LlenaPortaditas()
        {
            if (pastCola == null || !pastCola.ConPortadas) return;
            if (pastCola.Portadas == null) pastCola.Portadas = new Image[3];
            for (int i = 0; i < pastCola.Portadas.Length; i++)
                pastCola.Portadas[i] = (i < queue.Count && queue[i] != null) ? LoadCover(queue[i].Disc) : null;
            pastCola.Mas = (queue.Count > pastCola.Portadas.Length) ? queue.Count - pastCola.Portadas.Length : 0;
        }

        private int turnoPuesto;      // que puesto ocupo la ultima cancion pedida (0 = no se sabe)
        private double turnoSegs;     // y cuanto falta para que suene (-1 = no se puede saber)
        private PlayItem nowPlaying;

        // ---- Estadisticas (base de "Lo mas sonado") ----
        // Se cuenta lo que entra a la cola CON EXITO (lo que la gente pide y paga). Se guarda en
        // rocola_stats.txt junto al .exe: en segundo plano cada minuto si hubo cambios, y al cerrar.
        private Dictionary<string, int> songCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Topes del archivo de estadisticas: se conserva lo MAS sonado y reciente; lo viejo/poco sonado
        // se descarta solo, para que el archivo nunca crezca de mas ni alente la rocola.
        private const int SongStatCap = 2000, SongStatTarget = 1800;
        private Dictionary<string, long> songSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);   // recencia (cuando se sono por ultima vez)

        // ---- Datos ----
        private readonly List<Track> tracks = new List<Track>();
        private List<Album> albums = new List<Album>();
        private Dictionary<char, List<Album>> byLetter = new Dictionary<char, List<Album>>();
        private bool coversOnHold = false;     // true mientras se hojea rapido: NO se piden caratulas
        // ----- PERFIL DE DISCO AUTOMATICO -----
        // La MISMA version queda optima en cualquier rocola, tenga el disco que tenga: SSD -> varios carriles,
        // prelectura amplia y sin respiros; disco duro -> un carril, prelectura corta y respiros largos, que es
        // lo que un cabezal necesita. Quien lo decide es PerfilaDisco, al arrancar (unas lineas mas abajo). Lo
        // de aqui es el RESPALDO: cronometrar las 16 primeras lecturas de miniaturas, que es como se decidia
        // antes, para los casos en que aquello no pudiera medir.
        private static int diskProbeN = 0, diskProbeMs = 0;
        private static volatile bool diskSlow = true;    // arranca en modo DISCO DURO (seguro); si las mediciones dicen SSD, se suelta
        private static volatile bool medidoLento = true; // lo que dijo la MEDIDA (se conserva aunque se fuerce a mano)
        private static volatile int perfilDisco = 0;     // 0 = automatico (medir), 1 = forzar SSD, 2 = forzar disco duro ("disco=" en config)
        // UNA ROCOLA PUEDE TENER LAS DOS COSAS, y es de lo mas normal: el programa y las miniaturas en el SSD
        // del sistema y la musica en un disco duro grande colgado detras, o la musica repartida entre los dos
        // (ver "dir=" en el config). Un solo veredicto para todo se equivoca en media biblioteca, asi que se
        // guarda uno POR UNIDAD (C:\, D:\...) y cada archivo se trata como merece el disco donde ESTA. Lo que
        // no se puede repartir -cuantas caratulas se leen a la vez- se decide por donde este LA MAYORIA.
        private static readonly Dictionary<string, bool> discoLento = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        // Y la mediana que dio cada unidad, en ms, para ensenarla en Ctrl+F3: una unidad ya medida no se vuelve
        // a medir (PerfilaDisco), pero su numero sigue a la vista. Mismo candado; se borra junto con discoLento.
        private static readonly Dictionary<string, int> discoMediana = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly object discoLock = new object();
        private static string UnidadDe(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return "";
            try { string r = Path.GetPathRoot(ruta); return r ?? ""; }
            catch { return ""; }
        }
        // Si ESTE archivo esta en un disco con cabezal. Elegido a mano manda; si no, el de su unidad; y si de
        // esa unidad no se sabe nada, el veredicto general.
        private static bool DiscoLento(string ruta)
        {
            int pf = perfilDisco;
            if (pf == 1) return false;
            if (pf == 2) return true;
            string u = UnidadDe(ruta);
            if (u.Length > 0) { bool v; lock (discoLock) { if (discoLento.TryGetValue(u, out v)) return v; } }
            return diskSlow;
        }
        // Deja el veredicto general y los carriles como toque: lo medido, o lo que se haya elegido a mano.
        private static void AplicaPerfilDisco()
        {
            int pf = perfilDisco;
            bool lento = (pf == 1) ? false : (pf == 2) ? true : medidoLento;
            diskSlow = lento;
            // Tope global por CPU; cada unidad HDD conserva un solo lector en TakeCoverJob.
            // En 2C/2T un trabajador, reservando margen para interfaz y audio incluso con SSD.
            int carriles = CargaTrabajo.Lectores(CargaTrabajo.Procesadores);
            // El semaforo solo puede CRECER (quitarle turnos concedidos mientras se usan no es seguro). No pasa
            // nada: quien de verdad limita cuantas bombas hay a la vez es coverCarriles, y ese si baja.
            lock (discoLock)
            {
                if (carriles > coverAbiertos) { try { coverSem.Release(carriles - coverAbiertos); coverAbiertos = carriles; } catch { } }
            }
            coverCarriles = carriles;
        }
        private static void DiskProbe(int ms)
        {
            if (diskProbeN >= 16) return;
            System.Threading.Interlocked.Add(ref diskProbeMs, ms);
            if (System.Threading.Interlocked.Increment(ref diskProbeN) == 16)
            {
                // Camino de RESPALDO: normalmente el perfil ya lo decidio PerfilaDisco al arrancar, antes de la
                // primera pagina. Esto solo entra si aquello no pudo medir (biblioteca vacia, permisos raros).
                medidoLento = (diskProbeMs / 16) > 6;
                AplicaPerfilDisco();
            }
        }

        // ----- PERFIL DE DISCO, DECIDIDO AL ARRANCAR -----
        // Antes se decidia con las 16 PRIMERAS miniaturas que se leyeran, o sea DESPUES de la primera pagina:
        // hasta ese momento una rocola con SSD trabajaba en modo disco duro -un carril, prelectura corta,
        // respiros largos- justo en el rato en que mas se nota. Y se decidia por PROMEDIO: una sola lectura
        // desafortunada (un antivirus pasando, una carpeta fria) levantaba el promedio y dejaba la rocola en
        // modo lento el resto de la noche.
        //
        // Ahora se mide a proposito, aqui, con la biblioteca ya en la mano y todavia en segundo plano: unas
        // lecturitas de 4 KB en sitios SALTEADOS de canciones repartidas por toda la biblioteca. Eso es
        // exactamente lo que separa a un disco duro de un SSD: saltar a otro sitio obliga al cabezal a moverse
        // y eso son milisegundos, y en un SSD no hay cabezal que mover. Y se decide por la MEDIANA -el valor de
        // en medio-, que no se deja arrastrar por una lectura desafortunada como si hacia el promedio.
        private static bool perfilHecho = false;
        private const int MuestrasDisco = 15;    // lecturas salteadas del perfilado (repartidas por la biblioteca)
        private const int UmbralDiscoMs = 3;     // mas de esto de mediana = hay un cabezal moviendose
        private static string perfilTexto = "";  // lo medido, en palabras (se ensena en Ctrl+F3)
        private void PerfilaDisco(List<Track> lista)
        {
            if (lista == null || lista.Count == 0) return;
            try
            {
                HashSet<string> conocidas;
                lock (discoLock) conocidas = new HashSet<string>(discoLento.Keys, StringComparer.OrdinalIgnoreCase);
                Dictionary<string, List<Track>> nuevas = new Dictionary<string, List<Track>>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> cantidades = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < lista.Count; i++)
                {
                    Track t = lista[i]; if (t == null || t.Path == null) continue;
                    string unidad = UnidadDe(t.Path); int cuenta;
                    cantidades.TryGetValue(unidad, out cuenta); cantidades[unidad] = cuenta + 1;
                    if (conocidas.Contains(unidad)) continue;
                    List<Track> muestras;
                    if (!nuevas.TryGetValue(unidad, out muestras)) { muestras = new List<Track>(); nuevas[unidad] = muestras; }
                    muestras.Add(t);
                }
                if (nuevas.Count == 0 && perfilHecho) return;
                byte[] buf = new byte[4096];
                Random rnd = new Random(unchecked(Environment.TickCount * 397));
                foreach (KeyValuePair<string, List<Track>> unidad in nuevas)
                {
                    List<int> medidas = new List<int>(MuestrasDisco);
                    List<Track> archivos = unidad.Value;
                    int count = Math.Min(MuestrasDisco, archivos.Count);
                    for (int k = 0; k < count; k++)
                    {
                        Track t = archivos[(int)((long)k * archivos.Count / count)];
                        try
                        {
                            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                            using (FileStream fs = new FileStream(t.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
                            {
                                long largo = fs.Length;
                                if (largo > 8192) fs.Position = (long)(rnd.NextDouble() * (largo - 4096));
                                fs.Read(buf, 0, buf.Length);
                            }
                            sw.Stop(); medidas.Add((int)sw.ElapsedMilliseconds);
                        }
                        catch { }
                    }
                    if (medidas.Count < 3) continue;   // mantener el respaldo conservador sin muestras suficientes
                    medidas.Sort();
                    int mediana = medidas[medidas.Count / 2];
                    lock (discoLock) { discoLento[unidad.Key] = mediana > UmbralDiscoMs; discoMediana[unidad.Key] = mediana; }
                }
                string mandaU = null; int mandaN = 0; bool mandaLento = true;
                StringBuilder det = new StringBuilder();
                lock (discoLock)
                {
                    foreach (KeyValuePair<string, int> cantidad in cantidades)
                    {
                        bool lento;
                        if (!discoLento.TryGetValue(cantidad.Key, out lento)) continue;
                        if (det.Length > 0) det.Append(", ");
                        det.Append(cantidad.Key).Append(' ').Append(lento ? "disco duro" : "SSD");
                        int ms; if (discoMediana.TryGetValue(cantidad.Key, out ms)) det.Append(" (").Append(ms).Append(" ms)");
                        if (cantidad.Value > mandaN) { mandaN = cantidad.Value; mandaU = cantidad.Key; mandaLento = lento; }
                    }
                }
                if (mandaU == null) { perfilHecho = false; return; }
                perfilHecho = true;
                medidoLento = mandaLento; diskProbeN = 16;
                AplicaPerfilDisco(); perfilTexto = det.ToString();
                Diag("disco: " + det + "  -> manda " + mandaU + ", " + coverCarriles + " carril(es)"
                     + (perfilDisco != 0 ? "  (forzado a mano: " + (perfilDisco == 1 ? "SSD" : "disco duro") + ")" : ""));
            }
            catch { }
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MemStatusEx { public uint Length, MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual; }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemStatusEx b);

        // CUANTA MEMORIA SE LE DEJA AL CAJON DE CASILLAS HORNEADAS.
        //
        // Cada disco de la pared se hornea UNA vez -su caratula ya escalada y su nombre ya maquetado- y a
        // partir de ahi pasar de pagina es solo copiar. Volver a una pagina que ya se salio del cajon cuesta
        // ocho veces mas (2 ms contra 0.24 medidos), y ademas sus caratulas ya no estan en memoria, asi que
        // hay que ir a buscarlas al disco: en un disco duro eso son 18 saltos de cabezal y se VE, porque las
        // caratulas van apareciendo de a poco en vez de estar ya puestas.
        //
        // DOCE PAGINAS, y no mas, porque esta MEDIDO (bateria 'historial', hojeando 140 paginas y volviendo):
        //
        //     historial      hojear adelante     volver 40 atras
        //      5 paginas          2.38 ms            2.16 ms      <- como estaba
        //     12 paginas          2.25 ms            2.15 ms      <- GRATIS: hojear no se entera
        //     24 paginas          2.44 ms            2.31 ms
        //     48 paginas          2.69 ms            0.43 ms      <- ya cabe volver 40 atras, +13% al hojear
        //     88 paginas          5.98 ms            0.40 ms      <- hojear cuesta el DOBLE Y MEDIO
        //
        // Volver a una pagina que sigue en el cajon cuesta 0.25 ms; una que ya salio, 2.1 ms -y da igual si
        // salio hace seis paginas o hace ciento treinta: el escalon es plano-.
        //
        // Doce es el cajon MAS GRANDE que no se nota al hojear hacia delante, que es lo que se hace todo el
        // rato. De ahi para arriba se empieza a pagar el caso comun: hay mas que podar, y liberar un mapa de
        // bits cuesta. Guardar cien paginas -que suena bien- deja el hojeo en menos de la mitad de rapido
        // para arreglar un caso, volver cuarenta paginas atras de golpe, que casi nadie hace.
        //
        // SE VOLVIO A MEDIR al agrandar el cajon de PORTADAS a mil (ver TopeDePortadas), por si aquello
        // movia este numero. NO lo mueve, y el motivo es que son dos cosas independientes: esta tabla mide
        // lo que cuesta HORNEAR una casilla, no lo que cuesta ir al disco a por su portada. Tres tandas
        // seguidas dan la misma forma que el dia que se eligio el doce: 48 paginas cuestan un 12% mas al
        // hojear hacia delante y 88 un 61%, a cambio de abaratar volver muy atras, que casi nadie hace.
        // Se queda en doce.
        //
        // El TECHO va aparte y en memoria, porque una casilla mide lo que le toque en pantalla: a 1024x768
        // son 101 KB y a 1920x1080 mas del triple. Asi doce paginas nunca se convierten en un disparate.
        // No es 'const' a proposito: asi la bateria 'historial' puede barrer el valor y volver a medir la
        // tabla de aqui arriba cuando cambie la maquina o el tamano de las casillas.
        //
        // El TECHO va aparte y en memoria, porque una casilla mide lo que le toque en pantalla: a 1024x768
        // son 101 KB y a 1920x1080 mas del triple. Asi doce paginas nunca se convierten en un disparate.
        // No es 'const' a proposito: asi la bateria 'historial' puede barrer el valor y volver a medir la
        // tabla de aqui arriba cuando cambie la maquina o el tamano de las casillas.
        private static int CeldasHistorialPaginas = 12;
        private static int cacheCeldasMb = -1;
        internal static int CacheCeldasMb()
        {
            if (cacheCeldasMb >= 0) return cacheCeldasMb;
            ulong ramMb = 0;
            try
            {
                MemStatusEx ms = new MemStatusEx();
                ms.Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MemStatusEx));
                if (GlobalMemoryStatusEx(ref ms)) ramMb = ms.TotalPhys / (1024UL * 1024UL);
            }
            catch { }
            // En 32 bits el programa no pasa de ~2 GB por mucha RAM que haya, asi que el techo se queda
            // MUY por debajo aunque la maquina tenga de sobra. Si no se sabe cuanta hay, se es prudente.
            int mb;
            if (ramMb == 0) mb = 48;
            else if (ramMb < 1200) mb = 32;        // maquina justa
            else mb = 64;                          // la del bar (4 GB): 64 MB de un techo de 2 GB
            if (IntPtr.Size >= 8) mb = 192;        // en 64 bits no hay techo de 2 GB que valga
            cacheCeldasMb = mb;
            return mb;
        }

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
        private volatile int lastInputTick = 0;    // ultimo boton presionado (el precalentador pausa si hay gente)
        private int ultimaFlecha = 0;              // cuando se acepto la ultima flecha (freno de la auto-repeticion)
        private const int MsEntreFlechas = 40;     // 25 movimientos por segundo: mas de lo que la pantalla ensena
        private List<string> cachedVideoPaths;            // rutas de video del cache de biblioteca (arranque rapido)
        private List<string> lastVideoPaths;              // rutas de video del ultimo armado (para guardar el cache)
        private Dictionary<string, string> cachedCovers;  // carpeta -> portada, del cache de biblioteca
        private string lastPageSig = null;
        // CUANTAS PORTADAS SE GUARDAN EN MEMORIA.
        //
        // Cada portada se decodifica UNA vez y se queda en un cajon; mientras siga ahi, hojear no toca el
        // disco para nada. Cuando el cajon se llena salen las mas viejas, y volver a verlas cuesta leer su
        // miniatura otra vez -1.5 ms medidos en una maquina rapida; en una modesta, mas-.
        //
        // Las 220 de siempre se eligieron para la rocola de Windows XP, que tiene 1 GB. Un equipo moderno
        // tiene 4 GB y es de 64 bits, y ahi cabe LA PARED ENTERA: con mil portadas dentro, hojear deja de
        // tocar el disco del todo.
        //
        // Lo que ocupan esta MEDIDO, no calculado: los mapas de bits viven fuera del recolector, asi que
        // hay que mirar la memoria de verdad del proceso.
        //
        //       220 portadas ->  67 MB     <- lo de siempre
        //       600 portadas -> 162 MB
        //      1000 portadas -> 266 MB     <- el equipo potente, de sus 4 GB
        //
        // En 32 bits el programa no pasa de ~2 GB por mucha RAM que tenga la maquina, asi que ahi se queda
        // como estaba; y si no se pudo medir la RAM, tampoco se arriesga. Ojo: si la biblioteca tiene menos
        // discos que el tope, el cajon simplemente no se llena y esto no cuesta nada.
        //
        // La REGLA va aparte de la MEDIDA a proposito: asi las pruebas pueden preguntarle "¿y con cuatro
        // gigas?" sin tener que fabricar una maquina de cuatro gigas. Sin esto, la version de Windows solo
        // se podria comprobar en un Windows de verdad, que es justo donde no hay pruebas corriendo.
        private static int TopeDePortadasPara(ulong ramMb)
        {
            return (IntPtr.Size >= 8 && ramMb >= 3500) ? 1000 : 220;
        }
        private static int topePortadas = -1;
        internal static int TopeDePortadas()
        {
            if (topePortadas >= 0) return topePortadas;
            ulong ramMb = 0;
            try
            {
                MemStatusEx ms = new MemStatusEx();
                ms.Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MemStatusEx));
                if (GlobalMemoryStatusEx(ref ms)) ramMb = ms.TotalPhys / (1024UL * 1024UL);
            }
            catch { }
            topePortadas = TopeDePortadasPara(ramMb);
            return topePortadas;
        }
        // Y el que manda en cada DISCO. Sale del MISMO paseo de fondo que el de las letras -no cuesta ni un
        // archivo mas- y es lo que le pone color a los resultados del buscador. Por carpeta, que es lo unico
        // que identifica a un disco sin ambiguedad.
        private Dictionary<string, string> discTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool tintesPend;   // hay que (re)calcularlo: la biblioteca cambio o nunca se calculo
        private readonly List<SearchItem> searchIndex = new List<SearchItem>();
        private char currentLetter = '#';
        private const char TopMark = '\u2605';     // "letra" especial = LO MAS SONADO (disco virtual)
        private const int TopMax = 100;             // tope de canciones del disco "Lo mas sonado"
        private List<Album> topDiscs = new List<Album>();   // discos virtuales "TOP - <genero>"
        // OPTIMIZACION (TOP): BuildTopDiscs recorre TODA la biblioteca (100k+). Antes se rebuildeaba en
        // cada cambio de pagina dentro de "Lo mas sonado". Ahora solo cuando los datos cambian (al contar
        // una reproduccion o tras re-escanear): topDirty=true marca que hay que reconstruir; si no, se
        // devuelve el topDiscs ya armado. Hojear TOP queda instantaneo.
        private bool topDirty = true;
        private Dictionary<string, string> songGenre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // ruta -> etiqueta de genero (capturada al sonar)
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

        // ---------- LA PARED QUE RESPIRA, Y SU FRENO ----------
        //
        // La pared de discos sube y baja de brillo muy despacio, una respiracion cada medio minuto. Es lo
        // que hace que una pantalla quieta parezca encendida en vez de colgada.
        //
        // Y TIENE FRENO: mientras alguien esta usando la rocola, NO respira. Una pantalla enorme moviendose
        // despacio delante de quien esta eligiendo cancion no es bonita, es mareante.
        //
        // PERO FRENAR NO ES PARARSE EN SECO. Cortando de golpe, la pared se quedaria clavada en el brillo
        // que le tocara en ese instante -a veces mas oscura, a veces mas clara de lo normal- y asi hasta
        // que alguien vuelva a soltar el boton, que es justo lo que se queria evitar. Lo que se le quita es
        // el AIRE: la fuerza baja a cero mientras el ciclo sigue corriendo por dentro, y entonces la pared
        // acaba SIEMPRE en su brillo de siempre, se toque el boton en el punto del ciclo que se toque.
        private const float RespiraTope = 0.06f;     // por mucho que pida el archivo, nunca mas de un 6%
        // De fabrica, un 4.5%. NO es un numero a ojo: medido sobre el degradado del cielo, un 2.5% -lo que
        // habia- mueve entre 4 y 6 niveles de 255, que en una pantalla grande y en penumbra esta justo en el
        // limite de no verse. Con 4.5% se mueve entre 8 y 10, y eso ya se lee como una marea. Un 1.2% son
        // DOS niveles: la respiracion corria y no se veia, que es exactamente lo que pasaba.
        private const float RespiraDef = 0.045f;
        private const float RespiraPaso = 0.00873f;  // radianes por cuadro: a 24 cuadros/s, una vuelta cada ~30 s
        private static bool respiraOn = true;        // "respira=" en config
        private static float respiraFuerza = RespiraDef;   // "respira_fuerza=" en config, en por ciento
        private float quietud = 0f;                  // 0 = alguien esta tocando AHORA; 1 = hace rato que nadie
        // LA VISTA PREVIA, y por que hace falta.
        //
        // La respiracion esta hecha para NO verse mientras se mira: el freno pide diez segundos sin que nadie
        // toque un boton, tarda otros cuatro en volver del todo, y entonces mueve el cielo ocho o diez
        // niveles de 255 repartidos en treinta segundos -unas tres decimas de nivel por segundo-. Eso es
        // exactamente lo que se pidio... y hace imposible elegir cuanto respira, porque al pulsar el boton
        // acabas de tocar la rocola y no se mueve nada.
        //
        // Asi que mientras se elige, y SOLO mientras se elige, respira deprisa: el ciclo entero en unos
        // segundos y sin freno. Se apaga sola. En el bar no cambia nada: ni la velocidad, ni la fuerza, ni el
        // freno.
        private const int RespiraDemoSegs = 6;       // lo que dura la vista previa
        private const float RespiraDemoPaso = 0.10f; // radianes por cuadro: una vuelta cada ~2.6 s
        private DateTime respiraDemoHasta = DateTime.MinValue;
        internal void MuestraRespiracion() { respiraDemoHasta = DateTime.Now.AddSeconds(RespiraDemoSegs); }
        private float respiraFase = 0f;
        private float bgDimResp = 1f;                // la atenuacion CON la respiracion puesta (estrellas y destellos)
        private float bgRespiro = 1f;                // y la respiracion SOLA, que es la que mueve el cielo

        // El brillo ahora mismo: 1 es el de siempre. Es la forma de la respiracion y nada mas, sin tope
        // propio: la comparten la pared y la aurora del horizonte, que respira desde antes y con bastante
        // mas fuerza (AurReposoOnda), y meterle aqui el tope de la pared le recortaria la suya sin que
        // nadie lo hubiera pedido. Quien acota es FuerzaRespira, que es el UNICO sitio por donde se le
        // pone fuerza a la pared.
        //
        // Lo que si esta garantizado aqui es el freno: con quietud 0 devuelve 1 EXACTO, sea cual sea el
        // punto del ciclo. Por eso la pared nunca se queda clavada a media respiracion.
        internal static float Respiracion(float fuerza, float quietud, float fase)
        {
            if (!(fuerza > 0f) || !(quietud > 0f)) return 1f;
            if (quietud > 1f) quietud = 1f;
            return 1f + fuerza * quietud * (float)Math.Sin(fase);
        }

        // A donde va la quietud en el cuadro siguiente. Baja DEPRISA -en cuanto alguien toca un boton, la
        // pared se para en menos de medio segundo- y sube DESPACIO: parar tiene que notarse poco, y volver
        // a arrancar, nada. Y llega a CERO exacto, no a un pelin de movimiento que no se acaba nunca.
        internal static float PasoQuietud(float quietud, bool tocando)
        {
            float meta = tocando ? 0f : 1f;
            float q = quietud + (meta - quietud) * (tocando ? 0.30f : 0.05f);
            if (Math.Abs(q - meta) < 0.005f) q = meta;
            return q;
        }

        // Lo que se le deja respirar, leido del archivo de configuracion (en por ciento). Acotado: la pared
        // de un bar es enorme y de noche; un 20% ahi no es vistoso, es mareante. Y una linea sin sentido no
        // apaga la pared: se queda como de fabrica.
        internal static float FuerzaRespira(string txt)
        {
            float v;
            if (txt == null) return RespiraDef;
            if (!float.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out v)) return RespiraDef;
            return Clampf(v / 100f, 0f, RespiraTope);
        }
#if ROCOLA_GPU
        private GpuPanel gpuPanel;   // fondo de discos por GPU (experimental, Ctrl+F12)
        private Label lblGpu;        // etiqueta de estado/FPS del modo GPU (esquina)
#endif
        private bool searchOpen = false;
        private DateTime searchLastActivity = DateTime.Now;   // para auto-cerrar el buscador tras 1 min
        private string lastQuery = null;                      // busqueda incremental
        private List<SearchItem> lastMatches = null;          // coincidencias previas (para acotar el re-escaneo)
        private readonly Dictionary<char, BolsaLetra> bolsaLetra = new Dictionary<char, BolsaLetra>();
        private int credits = 0;                              // creditos disponibles (cada cosa cuesta 1)
        private const int CreditsPerCoin = 4;                 // monedas -> creditos
        // ACUMULACION de avisos: si repites la MISMA accion rapido (mientras su aviso sigue visible), el aviso
        // suma en vez de reiniciarse: monedas -> +4, +8, +12...; canciones -> "Cancion agregada", "... X2", "X3".
        private enum ToastKind { Other, Coin, Song }
        private ToastKind toastKind = ToastKind.Other; private int toastKindTick;
        private int coinTotal, songTotal;
        private void MarkToast(ToastKind k) { toastKind = k; toastKindTick = Environment.TickCount; }
        private bool SameToastActive(ToastKind k) { return toastKind == k && (Environment.TickCount - toastKindTick) < VidaAviso(); }   // lo que dura el aviso (1300, o el claro)

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
        // SALIR SOLO DEL INTERIOR DE UN DISCO. Alguien abre un disco, se distrae -le hablan, se va a bailar,
        // se acaba su turno- y la rocola se queda con SU disco abierto. El siguiente que llega no ve la pared
        // de discos, ve una lista de canciones suelta y no sabe ni donde esta ni como volver. A los 30 segundos
        // sin tocar NADA vuelve sola a la pared. Cada tecla reinicia la cuenta, asi que a quien esta eligiendo
        // de verdad no lo echa nunca. Y va por delante del modo atraccion (40 s), que es el paso siguiente.
        private const int DiscoIdleSecs = 30;
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
        private Color bgAccentTarget = DefaultAccent;
        // LAS DOS NEBULOSAS (ver ColoresNebulosa). Se guardan el genero A SECAS y los dos colores de la
        // caratula, que son de donde salen; y los dos colores ya hechos, que se deslizan de una cancion a la
        // siguiente igual que el acento, para que al cambiar de disco no den un salto.
        private Color bgGeneroTarget = DefaultAccent;
        private bool bgSinGenero;   // ausencia tras consultar cancion, disco, artista y carpeta
        private bool bgArtistaFijo;  // BTS conserva su morado cuando hay genero reconocido
        private Color bgPort1 = SinColor, bgPort2 = SinColor;
        private Color nebA = DefaultAccent, nebB = DefaultAccent;
        private float nebFase = 0f;

        // ---- Reproductor de video (Windows Media Player ActiveX, hosteado por codigo) ----
        private VideoHost video;
        private DateTime lastInput = DateTime.Now; // ultima tecla (para detectar inactividad)
        private Label lblAttract;                  // aviso amarillo pulsante "toca una tecla"
        private StartupLoad startupLoad;
        private bool startupReadPending;   // evita guardar tablas vacias si se cierra antes de publicarlas
        private bool startupClosing;

        public Form1()
        {
            this.DoubleBuffered = true;
            DecodificacionFlotante.LaNecesita = LlegaAlMaximo;   // que MP3 se descomprimen en coma flotante (FlotanteNLayer.cs)
            formStars.Density = 1f;      // MISMA densidad que albumView: el borde entre secciones deja de notarse
            formStars.QuietBands = true;   // y ademas evitar acumularlas en el abecedario (arriba) y la barra de estado (abajo)
            try { if (timeBeginPeriod(1) == 0) timerResRaised = true; } catch { }   // reloj a 1ms -> animacion pareja
            try { int wt, ct; System.Threading.ThreadPool.GetMinThreads(out wt, out ct); System.Threading.ThreadPool.SetMinThreads(Math.Max(wt, 3), ct); } catch { }  // arranque de cargas mas agil
            this.KeyPreview = true;                 // el formulario ve KeyPress aunque otro control tenga el foco
            this.KeyPress += Form_KeyPress;         // admite TODO caracter en el buscador (ñ, acentos, comillas...)
            BeginStartupDataLoad();
            try { BuildUi(); } catch { CancelStartupDataLoad(); throw; }
            needConfig = !LoadKeys();   // si no hay botones guardados, lanzamos el asistente al mostrarse
            progressTimer = new System.Windows.Forms.Timer(); progressTimer.Interval = 500; progressTimer.Tick += ProgressTimer_Tick;
            coverSettleTimer = new System.Windows.Forms.Timer(); coverSettleTimer.Interval = 120; coverSettleTimer.Tick += CoverSettle_Tick;   // 0.12s: con SSD, casi inmediato pero sigue filtrando el hojeo
            // El mismo patron que el de arriba, para las letras del buscador: se espera a que la persona
            // pare de teclear antes de mover los resultados (ver PosarMs).
            posaTimer = new System.Windows.Forms.Timer(); posaTimer.Interval = PosarMs;
            posaTimer.Tick += delegate (object s2, EventArgs e2) { posaTimer.Stop(); if (searchOverlay != null) searchOverlay.PosaConsulta(); };
            animTimer = new System.Windows.Forms.Timer(); animTimer.Interval = 42; animTimer.Tick += AnimTimer_Tick; animTimer.Start();   // ~24fps (giros y pulsos); el fondo completo va 1 de cada 3 cuadros
            toastTimer = new System.Windows.Forms.Timer(); toastTimer.Interval = 1300; toastTimer.Tick += delegate (object s, EventArgs e) { toastTimer.Stop(); lblToast.Visible = false; if (searchOverlay != null) { searchOverlay.ToastOn = false; searchOverlay.Invalidate(); } };
            videoPoll = new System.Windows.Forms.Timer(); videoPoll.Interval = 400; videoPoll.Tick += VideoPoll_Tick;
            statsTimer = new System.Windows.Forms.Timer(); statsTimer.Interval = 60000; statsTimer.Tick += StatsTimer_Tick; statsTimer.Start();   // guarda estadisticas cada minuto si hubo cambios
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { if (statsDirty) { statsDirty = false; SaveStats(false); } } catch { }   // guardar estadisticas pendientes
            try { GuardaNoche(false); } catch { }   // y lo mas pedido esta noche (AtraccionGpu.cs)
            WaitDataWrites(5000);   // ultima barrera por si Windows cerro la ventana por otra ruta
            try { if (timerResRaised) { timeEndPeriod(1); timerResRaised = false; } } catch { }
            base.OnFormClosed(e);
        }

        // Al mostrarse: acomoda la UI, carga la musica, OCULTA EL CURSOR, fuerza el FOCO DEL TECLADO
        // (con un vigia durante el arranque) y, si hace falta, configura botones.
        protected override void OnShown(EventArgs e)
        {
            VigiaPantalla.Hace("el arranque: mostrarse");
            base.OnShown(e);
            if (startupClosing || IsDisposed) return;
            ArrancaVigia();   // cuanto tarda la pantalla en atender, siempre (MedidasBitacora.cs)
            ProbeAudioTier();    // requisitos de maquina para el NIVEL 2 de audio (antes de leer dsee= de config)
            LoadConfigPrefs();   // preferencias: render GPU/CPU + carpetas de musica adicionales (Ctrl+F2)
            Diag("rocola del " + DeCuandoEs());   // LO PRIMERO de la bitacora: de cuando es la que esta corriendo
            // AQUI y no en BuildUi: la pantalla se arma antes de leer la configuracion, asi que alla
            // "cola_portadas=0" todavia no se sabe. Y manda en el ANCHO de la capsula, que se mide luego.
            if (pastCola != null) pastCola.ConPortadas = colaPortadasOn;
            NeuralEnv.TryLoad(RutaLectura("rocola_ia.bin"));   // IA propia (el modelo entrenado: en la carpeta de datos, o el de fabrica)
            EqNet.TryLoad(RutaLectura("rocola_eq_ia.bin"));    // IA del EQ automatico (objetivo por cancion)
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
            // pausa para mantenimiento, y se queda pausado hasta que se vuelva a pulsar. Costo: una
            // llamada ligera cada 1.5s, nada.
            //
            // Esta linea de aqui abajo es la red: arrancar SIEMPRE enciende el vigia. Como la pausa ya no
            // caduca sola, es lo unico que impide que una pausa olvidada se quede puesta para siempre.
            maintMode = false;
            if (focusTimer == null)
            {
                focusTimer = new System.Windows.Forms.Timer();
                focusTimer.Interval = 1500;
                focusTimer.Tick += FocusTimer_Tick;
            }
            focusTimer.Start();

            // LAS TABLAS DEL ARRANQUE (volumen, EQ, lo mas sonado...) las lee un hilo aparte desde el constructor. Antes
            // la ventana se quedaba aqui PARADA esperandolas (Monitor.Wait): 150-230 ms medidos en este servidor, mas con
            // un disco mecanico lento, y en ese rato no pintaba ni atendia nada. Ahora sigue viva, y lo que necesita las
            // tablas sigue en SigueArranque cuando el lector avisa, en el mismo orden de siempre: tablas, biblioteca y, con
            // ella, creditos y cola. Las teclas de mientras esperan su turno (ProcessCmdKey).
            EsperaLasTablas();
        }

        private bool arranqueEnEspera;   // la ventana ya se ve y las tablas del arranque aun no estan
        private bool arranqueSeguido;    // SigueArranque ya corrio (una sola vez)
        private List<KeyValuePair<Message, Keys>> teclasEnEspera;

        private void EsperaLasTablas()
        {
            StartupLoad load = startupLoad;
            bool esperar = false;
            if (load != null && !startupClosing)
                lock (load)
                    if (!load.Done && !load.Cancelled)
                    {
                        esperar = true; arranqueEnEspera = true;
                        load.AlTerminar = delegate
                        {
                            try { BeginInvoke((MethodInvoker)SigueArranque); } catch { }   // la ventana se cerro mientras tanto
                        };
                    }
            if (!esperar) SigueArranque();
        }

        // Lo del arranque que necesita las tablas ya leidas (ver EsperaLasTablas).
        private void SigueArranque()
        {
            VigiaPantalla.Hace("el arranque: publicar las tablas y seguir");
            if (arranqueSeguido) return;
            arranqueSeguido = true;
            arranqueEnEspera = false;
            if (startupClosing || IsDisposed) { teclasEnEspera = null; return; }
            bool prepared = CompleteStartupDataLoad();
            if (!prepared) LoadStats();   // antes de armar TOP y restaurar creditos/cola
            if (!prepared) LoadLoudCache();   // fallback si fallo el trabajador
            if (!prepared) LoadIntegCache();
            if (!prepared) LoadEqCache();
            IaCountPairs();      // cuanto material de entrenamiento de la IA llevamos juntado
            if (!prepared) LoadIaDone();
            if (!prepared) LoadDeepDone();
            if (!prepared) LoadDurFast();
            if (!prepared) LoadIncompatible();
            startupReadPending = false;
            BackupLearnedData(); // respalda lo aprendido de la sesion ANTERIOR antes de que esta lo modifique
            LayoutControls();
#if ROCOLA_GPU
            if (gpuPrefOn) ApplyAlbumGpu(true, false, false);   // render por GPU por defecto (o preferencia guardada); silencioso si el device falla; no re-guarda
#endif
            LoadMusicFolder();

            if (needConfig) StartConfig();   // primera vez: configurar los botones de la rocola
            AtiendeTeclasEnEspera();
        }

        // Las teclas que llegaron mientras se leian las tablas, en el mismo orden: como las dejaba en cola la ventana parada.
        private void AtiendeTeclasEnEspera()
        {
            List<KeyValuePair<Message, Keys>> t = teclasEnEspera; teclasEnEspera = null;
            if (t == null) return;
            for (int i = 0; i < t.Count; i++) { Message m = t[i].Key; ProcessCmdKey(ref m, t[i].Value); }
        }

        // Trae la rocola al frente y le da el foco del teclado (robusto para el autoarranque).
        // Windows NIEGA el foco pedido por codigo desde una app de fondo (por eso "solo con mouse
        // funcionaba": el clic fisico si otorga el foco). Hacemos dos cosas:
        //  1) Truco AttachThreadInput: nos "pegamos" al hilo que tiene el foco para poder tomarlo.
        //  2) Si Windows aun se niega, generamos un CLIC DE MOUSE SINTETICO (mouse_event, no
        //     necesita mouse fisico) en la esquina inferior de NUESTRA ventana (zona sin botones):
        //     para Windows es identico a un clic real, y el foco se otorga siempre.
        private bool focoCerrando;
        private void DetieneVigiaFoco()
        {
            focoCerrando = true; // un Tick ya encolado tampoco puede recrear el Handle
            System.Windows.Forms.Timer anterior = focusTimer;
            focusTimer = null;
            if (anterior != null)
            {
                anterior.Stop();
                anterior.Tick -= FocusTimer_Tick;
                anterior.Dispose();
            }
        }
        private void ForceFocus()
        {
            if (focoCerrando || IsDisposed || Disposing || !IsHandleCreated) return;
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
            if (focoCerrando || IsDisposed || Disposing || !IsHandleCreated) return;
            if (maintMode) return;                       // pausa de mantenimiento (F10), hasta que se vuelva a pulsar
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

        // DE CUANDO ES ESTA ROCOLA.
        //
        // Hasta ahora la rocola no lo decia en ningun sitio, y eso costo una tanda entera: se arreglo algo,
        // se pidio una foto, la foto seguia igual... y no habia forma de distinguir "esta roto" de "es la
        // version de antes, que no se copio". Las dos cosas se ven exactamente igual.
        //
        // Se saca de la FECHA DEL PROPIO ARCHIVO y no de un numero escrito a mano. Un numero a mano hay que
        // acordarse de subirlo, y el dia que a alguien se le olvide la rocola mentira, que es peor que
        // callarse. La fecha del archivo no se puede olvidar: la pone el compilador.
        internal static string DeCuandoEs()
        {
            try
            {
                string exe = Application.ExecutablePath;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return "";
                return File.GetLastWriteTime(exe).ToString("dd/MM/yyyy HH:mm");
            }
            catch { return ""; }
        }

        private void Diag(string msg)
        {
            if (!diagOn) return;
            // Con la hora de ahora, pero la escribe un hilo de fondo (Bitacora, en MedidasBitacora.cs). Tope ~2 MB, como siempre.
            try { Bitacora.Apunta(RutaDatos("rocola_diagnostico.txt"), 2097152, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\r\n"); }
            catch { }
        }

        // Ojo con el orden: se pregunta "NO es mayor que lo" en vez de "es menor que lo". Con NaN las dos
        // comparaciones normales dan false y el NaN salia tal cual por el otro lado; asi cae en lo, que es lo
        // seguro. Lo mismo abajo en la tangente y en la escalera de tonos.
        private static float Clampf(float v, float lo, float hi) { return !(v > lo) ? lo : (v > hi ? hi : v); }
        private void QueueDataWrite(System.Threading.WaitCallback work)
        {
            lock (dataWritesLock) { dataWritesPending++; dataWritesIdle.Reset(); }
            try
            {
                bool encolado = System.Threading.ThreadPool.QueueUserWorkItem(delegate (object state)
                {
                    try { work(state); }
                    finally
                    {
                        lock (dataWritesLock)
                        {
                            dataWritesPending--;
                            if (dataWritesPending <= 0) { dataWritesPending = 0; dataWritesIdle.Set(); }
                        }
                    }
                });
                if (!encolado) throw new InvalidOperationException("ThreadPool no acepto el guardado");
            }
            catch
            {
                lock (dataWritesLock)
                {
                    dataWritesPending--;
                    if (dataWritesPending <= 0) { dataWritesPending = 0; dataWritesIdle.Set(); }
                }
                // Si el ThreadPool ya no acepta trabajos (normalmente durante el apagado), hacer el
                // guardado aqui mismo. Perderlo seria peor que demorar unas decimas el cierre.
                try { work(null); } catch { }
            }
        }

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

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutControls(); Invalidate(); VuelveAPrecalentar(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 2 || h < 2) { base.OnPaintBackground(e); return; }
#if ROCOLA_GPU
            if (gpuPanel != null && gpuPanel.Visible) return;   // el panel GPU (pantalla completa) ya cubre todo; no gastes CPU pintando el cielo detras
#endif
            formStars.SkyOffsetX = 0; formStars.SkyOffsetY = 0; formStars.SkyFullW = w; formStars.SkyFullH = h;
            formStars.SkyMid = EscalaBrillo(SkyColorMid(bgAccent), bgRespiro); formStars.Acento = bgAccent;
            // LAS DOS NEBULOSAS, AQUI QUIETAS. Y es a proposito.
            //
            // En esta maquina el cielo entero va horneado a un mapa de bits que se guarda, porque rehacerlo
            // cuesta de 6 a 27 ms segun la pantalla, y se rehornea cuando cambia alguno de sus colores. Se
            // probo pasarle los dos colores en movimiento -el cruce de la caratula y la respiracion- y esta
            // medido lo que costaba: los rehorneados pasaban de 73 a 151 en medio minuto, o sea 70 ms de
            // cada mil en una pantalla de 2560x1440. El doble de trabajo, en la maquina que se eligio
            // JUSTAMENTE por no tener tarjeta grafica.
            //
            // Asi que aqui se pasan los dos colores QUIETOS, y ninguno de los dos depende de la fase: el del
            // genero no depende de ella nunca, y el de la caratula es exactamente el que esta pared ya
            // pintaba antes de todo esto. Los dos cambian solo al cambiar de cancion, que es cuando el cielo
            // se rehornea de todas formas. Coste de esto, medido: CERO rehorneados mas.
            //
            // Lo que esta maquina se pierde es el MOVIMIENTO. Los dos colores estan -que era lo gordo, porque
            // antes habia uno solo-; lo que no hay es el cruce ni la deriva, que es lo que sale gratis con
            // tarjeta grafica y aqui costaria rehacer la pantalla entera.
            formStars.NebUno = nebA;
            formStars.NebDos = ColorNebulosa(bgAccent);
            formStars.HazFase = hazFase;   // el paseo lento de la luna: el mismo reloj que la via lactea
            formStars.ConLuna = lunaOn;
            formStars.Dim = bgDimResp;   // atenua TAMBIEN las franjas de arriba/abajo en sincronia con la rejilla (una sola pieza), respiracion incluida
            formStars.DrawCached(g, w, h, EscalaBrillo(SkyColor(bgAccent), bgRespiro), EscalaBrillo(SkyColor2(bgAccent), bgRespiro));   // cielo en degradado CONTINUO; etiquetas y mini-cubo transparentes encima
        }

        private static Color Shade(Color c, double towardBlack)
        {
            if (towardBlack < 0) towardBlack = 0; if (towardBlack > 1) towardBlack = 1;
            double k = 1.0 - towardBlack;
            return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }
        internal static float CorteCielo(string txt)
        {
            float v;
            if (txt == null) return CieloCorteDef;
            if (!float.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out v)) return CieloCorteDef;
            if (float.IsNaN(v) || float.IsInfinity(v)) return CieloCorteDef;
            return Clampf(v / 100f, 0.25f, 0.66f);
        }

        // Un por ciento del archivo, acotado entre 0 y 1. Una linea sin sentido no apaga nada: se queda como
        // de fabrica, que es la misma regla que ya sigue la fuerza de la respiracion.
        private static float LeePorCiento(string txt, float porDefecto)
        {
            float v;
            if (txt == null) return porDefecto;
            if (!float.TryParse(txt.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out v)) return porDefecto;
            if (float.IsNaN(v) || float.IsInfinity(v)) return porDefecto;
            return Clampf(v / 100f, 0f, 1f);
        }

        private static Color LerpColor(Color a, Color b, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(Hacia(a.A, b.A, t), Hacia(a.R, b.R, t), Hacia(a.G, b.G, t), Hacia(a.B, b.B, t));
        }

        // LOS DOS COLORES DE "YA LA PEDISTE" Y "SUENA AHORA". Viven aqui, y no dentro de la lista del disco
        // donde nacieron, porque ahora los usan DOS sitios: esa lista y los resultados del buscador. Con una
        // copia en cada uno, el dia que alguien cambiara el verde tendriamos una pantalla de un verde y la
        // otra de otro, y nadie se enteraria hasta verlas seguidas.
        //
        // El AMBAR es deliberadamente de otra familia de color que el verde: de un vistazo, y a la distancia
        // a la que se mira una rocola, tienen que distinguirse sin leer. Verde = la vas a oir; ambar = la
        // estas oyendo.
        internal static readonly Color ColaVerde = Color.FromArgb(120, 230, 165);

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
            buscaView = new BuscaView();   // la casilla de BUSCAR, en medio de la fila de arriba
            Controls.Add(buscaView);
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

            headerView = new HeaderView();

            albumView = new AlbumView();
            albumView.Sky = formStars;   // MISMO campo de estrellas que el fondo del form: la rejilla pinta su ventana (una sola pieza, sin costuras)
            albumView.GetCover = LoadCoverAsync;
#if ROCOLA_GPU
            albumView.PeekCoverGpu = delegate (Album a)
            {
                if (a == null || a.CoverPath == null) return null;
                lock (coverLock) { Image c; return CoverEnCache(a.CoverPath, false, out c) ? c : null; }
            };
            albumView.PeekPreviewGpu = PeekGpuPreview;
            albumView.PortadasLod = caratulasLod;
            albumView.CoverControl = coverGovernor;
#endif
            // LA MARCA DE "YA LA PUSISTE". La pared no guarda la cola: pregunta. Asi no hay dos listas
            // que puedan decir cosas distintas, y apagar la marca es dejar de contestar.
            albumView.EnCola = delegate (Album a)
            {
                if (!marcaColaOn || a == null || a.Folder == null || colaDiscos.Count == 0) return false;
                return colaDiscos.ContainsKey(a.Folder);
            };
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
            songList.GetSonando = delegate (int i)
            {
                if (sonandoKey.Length == 0 || i < 0 || i >= songItems.Count) return false;
                Track tk = songItems[i] as Track;
                string k = (tk != null) ? tk.Path : null;
                if (k == null) { VideoItem vi = songItems[i] as VideoItem; if (vi != null) k = vi.Path; }
                return k != null && string.Equals(k, sonandoKey, StringComparison.OrdinalIgnoreCase);
            };
            songList.GetQueuePos = delegate (int i)
            {
                if (i < 0 || i >= songItems.Count || colaPos.Count == 0) return 0;
                Track tk = songItems[i] as Track;
                string k = (tk != null) ? tk.Path : null;
                if (k == null) { VideoItem vi = songItems[i] as VideoItem; if (vi != null) k = vi.Path; }
                int pos;
                return (k != null && colaPos.TryGetValue(k, out pos)) ? pos : 0;
            };
            songList.GetGenero = delegate (int i)
            {
                if (i < 0 || i >= songItems.Count) return "";
                // En los discos de LO MAS SONADO cada cancion viene de un disco distinto, asi que se mira el
                // suyo y no el del monton. Lo demas lo resuelve la cascada (ver GeneroDeCancion).
                Album org = currentAlbum;
                if (currentAlbum != null && currentAlbum.IsTop && currentAlbum.TrackAlbums != null && i < currentAlbum.TrackAlbums.Count)
                    org = currentAlbum.TrackAlbums[i];
                return GeneroDeCancion(songItems[i] as Track, org);
            };
            songList.GetCount = delegate { return songItems.Count; };
            songList.GetText = delegate (int i)
            {
                string title = (i >= 0 && i < songItems.Count) ? songItems[i].ToString() : "";
#if ROCOLA_GPU
                if (gpuEnabled) title = TituloSinPista(title);
#endif
                return title;
            };
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

            // TARJETA DE LO QUE SUENA: lblNowTop lleva el TITULO (grande, blanco) y lblNow el ARTISTA (mas
            // chico, verde). Antes las dos eran la misma frase partida por ancho; ahora cada una dice una cosa.
            lblNow = new Label(); TransLabel(lblNow, 11.5f); lblNow.Text = ""; lblNow.AutoEllipsis = true;
            lblNow.ForeColor = Color.FromArgb(120, 230, 165);
            lblNow.ForeColor = Color.FromArgb(125, 235, 165);   // ESTETICA: verde "sonando" (se distingue de "siguiente")
            lblNowTop = new Label(); TransLabel(lblNowTop, 15f); lblNowTop.Text = "Nada reproduciendose"; lblNowTop.AutoEllipsis = true;
            lblNowTop.ForeColor = lblNow.ForeColor;             // misma linea, mismo verde: se leen como un solo texto
            lblNowTop.TextAlign = ContentAlignment.BottomLeft;  // pegada a la linea de abajo, no flotando
            lblNow.UseMnemonic = false; lblNowTop.UseMnemonic = false;   // un "&" del titulo es un "&", no un subrayado
            lblNext = new Label(); TransLabel(lblNext, 10.5f); lblNext.Text = "Sigue:  -"; lblNext.AutoEllipsis = true;
            lblNext.ForeColor = Color.FromArgb(150, 165, 200);
            lblNext.ForeColor = Color.FromArgb(150, 200, 255);  // ESTETICA: azul claro "siguiente"
            lblNext.UseMnemonic = false;                        // igual que arriba: un "&" del titulo es un "&"
            pastCola = new PastillaView(); pastCola.Etiqueta = "En cola"; pastCola.Valor = "0";
            pastCola.Acento = Color.FromArgb(140, 205, 255);
            pastCred = new PastillaView(); pastCred.Etiqueta = "Inserta una moneda"; pastCred.Valor = "";
            pastCred.Acento = Color.FromArgb(255, 205, 90);
            pastGenero = new GeneroView(); pastGenero.Font = new Font(this.Font.FontFamily, 10f, FontStyle.Bold); pastGenero.Visible = false;
            lblMejorada = new Label(); TransLabel(lblMejorada, 12f); lblMejorada.Text = "MEJORADA"; lblMejorada.ForeColor = MejGoldBase;
            lblMejorada.TextAlign = ContentAlignment.MiddleLeft; lblMejorada.Visible = false;   // aparece solo si la cancion suena con DSEE (PositionMejorada)

            miniCube = new CubeBox();
            miniCube.BackColor = Color.Transparent;
            miniCube.Visible = false;

            progress = new ProgressView();
            progress.Neon = progresoNeon;
            lblTime = new Label(); TransLabel(lblTime, 13f); lblTime.Text = "0:00 / 0:00";

            lblToast = new Label();
            lblToast.TextAlign = ContentAlignment.MiddleCenter;
            lblToast.BackColor = Color.FromArgb(40, 165, 90);
            lblToast.ForeColor = Color.White;
            lblToast.Font = Bold(14f);
            lblToast.Visible = false;

            searchOverlay = new SearchOverlay();
            searchOverlay.Sky = formStars;   // MISMO campo de estrellas que el resto: una sola pieza, sin costuras ni memoria doble
            searchOverlay.Visible = false;
            searchOverlay.GetCover = LoadCover;
            searchOverlay.GetCoverYa = PortadaEnMemoria;   // mientras se barren resultados, nada de encargos al disco
            // De que disco es cada resultado y de que genero. El genero lo va aprendiendo el paseo de fondo,
            // asi que puede llegar despues: por eso DatosVer, que hace que las tarjetas se rehagan al llegar.
            searchOverlay.GetDisco = delegate (SearchItem it)
            {
                if (it == null || it.AlbumRef == null) return "";
                if (it.Kind == 1) return "";                  // la tarjeta YA es el disco: no repetirlo
                return it.AlbumRef.Name ?? "";
            };
            searchOverlay.GetGenero = delegate (SearchItem it)
            {
                // La misma respuesta que dentro del disco: si es una cancion se le pregunta por ella; si es un
                // disco o un artista, por el disco. Antes el buscador y el interior podian decir cosas distintas.
                return (it != null) ? GeneroDeCancion(it.TrackRef, it.AlbumRef) : "";
            };
            // QUE CANCION YA ESTA PEDIDA Y CUAL SUENA. Se contesta con las MISMAS tablas que la lista de
            // dentro del disco (colaPos y sonandoKey), no con una copia: asi las dos pantallas no se pueden
            // desparejar, y la clave es la ruta del archivo, que es lo unico que sobrevive a un re-escaneo.
            //
            // Y NO MIRAN EL INTERRUPTOR DE Ctrl+F3, igual que no lo mira la lista de dentro del disco. Aqui
            // me equivoque y se vio en el bar: lo puse a mirarlo, y con "marca_cola=0" las marcas salian
            // dentro del disco y no en el buscador, que es justo la clase de diferencia que vuelve loco a
            // quien la sufre.
            //
            // Ese interruptor se llama "Marcar los discos que ya pediste" y habla del palomito de la PARED,
            // que va por disco. Esto va por CANCION, como las marcas de dentro del disco. Un interruptor que
            // apaga tres cosas distintas no es un interruptor, es una sorpresa.
            searchOverlay.GetQueuePos = delegate (SearchItem it)
            {
                if (it == null || it.TrackRef == null || colaPos.Count == 0) return 0;
                int pos;
                return (it.TrackRef.Path != null && colaPos.TryGetValue(it.TrackRef.Path, out pos)) ? pos : 0;
            };
            searchOverlay.GetSonando = delegate (SearchItem it)
            {
                if (it == null || it.TrackRef == null || sonandoKey.Length == 0) return false;
                return it.TrackRef.Path != null && string.Equals(it.TrackRef.Path, sonandoKey, StringComparison.OrdinalIgnoreCase);
            };
            searchOverlay.OnQueryChanged = RecomputeResults;
            searchOverlay.OnTecleo = AlTeclearBusqueda;
            searchOverlay.OnChoose = ChooseSearchResult;
            searchOverlay.OnClose = CloseSearch;

            attractView = new AttractView();
            attractView.Visible = false;
            attractView.GetCoverByPath = LoadCoverByPath;

            Controls.Add(lblStatus); Controls.Add(helpBar); Controls.Add(pastCred); Controls.Add(pastGenero);
            Controls.Add(letterBar); Controls.Add(headerView);
            Controls.Add(albumView); Controls.Add(panelSongs);
            Controls.Add(lblNowTop); Controls.Add(lblNow); Controls.Add(lblMejorada); Controls.Add(lblNext); Controls.Add(pastCola); Controls.Add(progress); Controls.Add(lblTime);
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
                if (buscaView != null) buscaView.Visible = false;
                return;   // lo demas queda detras del video; se reacomoda al salir
            }
            if (helpBar != null) helpBar.Visible = true;
            if (lblAttract != null) lblAttract.Visible = false;

            // Con video, el contenido (letras, encabezado, rejilla) se encoge a la IZQUIERDA;
            // la barra inferior (lo de abajo) SIEMPRE ocupa todo el ancho.
            int splitX = videoOn ? (int)(W * 0.50) : (W - pad);
            int contentRight = videoOn ? (splitX - 12) : (W - pad);
            int contentW = Math.Max(220, contentRight - pad);

            // LA FILA DE ARRIBA, apretada. Las instrucciones ocupan 439 px de verdad y se les reservaban 580:
            // 141 px de aire. Y el ENCABEZADO de la seccion tenia una fila entera para el solo, 44 px de alto en
            // 1024x768. Los dos comparten ahora esta fila, y esos 44 px se los queda la rejilla de discos, que es
            // lo que la gente mira. El encabezado se dibuja pegado a la derecha y se encoge solo si le aprietan.
            int barraDer = videoOn ? contentRight : (W - pad);
            int helpX = pad;
            // La fila de arriba crece con la pantalla, como la de las letras: en una pantalla grande, una
            // fila clavada en 34 px deja la casilla de BUSCAR pequenita justo donde tiene que verse de lejos.
            // Con 768 de alto la cuenta da 34 exactos, asi que en la maquina del bar no cambia nada.
            // La fila va mas alta que antes (34 -> 44) porque la casilla de BUSCAR tiene que verse desde
            // lejos, de pie y de noche: es lo que decide si el cliente que no sabe usarla acaba poniendo su
            // cancion o se va. Los 10 px salen de la rejilla de discos, que tiene de sobra.
            int filaH = H * 44 / 768; if (filaH < 44) filaH = 44; else if (filaH > 76) filaH = 76;
            int helpW = Math.Max(120, Math.Min(HelpBar.Ancho(filaH), barraDer - helpX));
            if (helpBar != null) helpBar.SetBounds(helpX, 8, helpW, filaH);
            // LA CASILLA DE BUSCAR, EN MEDIO. Va centrada respecto a la pantalla entera -no respecto al
            // hueco que queda- porque el sitio donde el ojo la busca es el centro de arriba. Si no cabe sin
            // pisar las instrucciones, se corre a la derecha lo justo.
            int busX = helpX + helpW + 12, busW = 0;
            if (buscaView != null)
            {
                busW = Math.Min(BuscaView.Ancho(filaH), Math.Max(0, barraDer - busX));
                busX = SitioBusca(W, busW, busX, barraDer);
                buscaView.SetBounds(busX, 8, busW, filaH);
                buscaView.Visible = (busW >= 120 && !videoOn);
            }
            int headX = (buscaView != null && buscaView.Visible) ? (busX + busW + 12) : (helpX + helpW + 12);
            headerView.SetBounds(headX, 8, Math.Max(0, barraDer - headX), filaH);
            if (lblConfig != null) lblConfig.SetBounds(0, 0, W, H);   // asistente de config: pantalla completa

            // LA BARRA DE LETRAS CRECE CON LA PANTALLA. Estaba clavada en 40 px de alto: en 1024x768 se ve
            // bien, pero en una pantalla ancha cada casilla pasa de 100 px y la letra seguia siendo la misma
            // pizca perdida en medio de tanto hueco. Con 768 de alto la cuenta da 40 exactos, asi que en la
            // maquina del bar no cambia absolutamente nada.
            // ...y tambien con lo ANCHA que le queda cada casilla. En una pantalla ultra ancha las 29 casillas
            // se reparten mas de 3000 px: quedan de 117 de ancho por 40 de alto, una ranura achatada con una
            // letra chiquita perdida dentro. Se le pide al alto que acompane al ancho de casilla para que la
            // casilla siga siendo una casilla. En 1024x768 las dos cuentas dan 40 o menos, asi que la maquina
            // del bar no cambia en nada.
            int lettersY = 8 + filaH + 4;
            int lettersH = H * 40 / 768;
            int porCasilla = (contentW * 8) / (29 * 10);   // 0.80 del ancho de casilla
            if (porCasilla > lettersH) lettersH = porCasilla;
            if (lettersH < 40) lettersH = 40; else if (lettersH > 104) lettersH = 104;
            letterBar.SetBounds(pad, lettersY, contentW, lettersH);

            // LA FRANJA DE ESTADO, en grande. Es lo que se lee DE LEJOS -de pie, a dos metros de la maquina-
            // y estaba pensada para mirarla de cerca: letra chica, cubo de 64 y una barra de duracion de 16 px
            // que casi no se veia. Todo sube alrededor de un cuarto. La franja crece unos 20 px de alto y la
            // rejilla de discos los cede: la aurora, el horizonte y la textura de la franja se acomodan solos
            // porque todos salen de BotStripTop.
            // ==========  LA TARJETA DE LO QUE SUENA  ==========
            // La portada manda a la izquierda y a su lado, en renglones: el titulo, el artista, lo que sigue y
            // la barra de duracion con el reloj. A la derecha, creditos y cola en capsulas. Ocupa 96 px de alto
            // -18 menos que los cuatro renglones sueltos de antes- y esos 18 se los queda la rejilla de discos.
            // Los cuatro renglones de la tarjeta, medidos con la letra que toque y sin un pixel de sobra:
            // titulo, artista, lo que sigue, y la barra de duracion. La portada mide lo mismo que los cuatro
            // juntos, asi que la tarjeta es un bloque cuadrado a la izquierda y texto a la derecha.
            int hTit = Math.Max(24, (lblNowTop != null ? lblNowTop.Font.Height : 24) + 1);
            int hArt = Math.Max(18, (lblNow != null ? lblNow.Font.Height : 18) + 1);
            int hSig = Math.Max(17, (lblNext != null ? lblNext.Font.Height : 17) + 1);
            int progH = 12;
            int tarjetaH = hTit + hArt + hSig + 4 + progH + 2;
            int tarjetaY = H - pad - tarjetaH;
            int cubeSize = tarjetaH;
            int nowTopY = tarjetaY;                  // el titulo manda donde arranca la franja (ver BotStripTop)
            miniCube.SetBounds(pad, tarjetaY, cubeSize, cubeSize);

            // Las capsulas, arriba a la derecha. Se miden por su contenido: "Inserta una moneda" es mucho mas
            // ancha que "Creditos 3", y una capsula a medias se veria mal.
            int pastH = AltoPastilla(tarjetaH);
            int wCred = PastillaView.Ancho(pastCred.Etiqueta, pastCred.Valor, pastH, this.Font.FontFamily, false);
            int wCola = PastillaView.Ancho(pastCola.Etiqueta, pastCola.Valor, pastH, this.Font.FontFamily, pastCola.ConPortadas);
            int pastY = tarjetaY;
            pastCred.SetBounds(W - pad - wCred, pastY, wCred, pastH);
            pastCola.SetBounds(W - pad - wCred - 10 - wCola, pastY, wCola, pastH);

            int txtX = pad + cubeSize + 14;
            int derPast = W - pad - wCred - wCola - 24;             // hasta donde puede llegar el titulo
            int txtW = Math.Max(120, derPast - txtX);
            lblNowTop.SetBounds(txtX, tarjetaY, txtW, hTit);                  // el TITULO
            lblNow.SetBounds(txtX, tarjetaY + hTit, txtW, hArt);              // el ARTISTA

            int timeW = 130;
            int anchoDer = W - pad - txtX;
            int progW = Math.Max(50, anchoDer - timeW - 12);
            lblNext.SetBounds(txtX, tarjetaY + hTit + hArt, progW, hSig);     // lo que SIGUE
            int filaY = tarjetaY + hTit + hArt + hSig + 4;                    // y al pie, la barra con el reloj
            progress.SetBounds(txtX, filaY, progW, progH);
            lblTime.SetBounds(txtX + progW + 12, filaY - (24 - progH) / 2, timeW, 24);
            mejRowW = txtW; PositionMejorada();   // acomoda el letrero MEJORADA tras el artista

            int albumY = lettersY + lettersH + 10;
            // EL AVISO DE ESTADO ya no ocupa sitio propio en la fila de arriba: durante el uso normal esta
            // VACIO (solo habla al escanear o si algo falla), y le estabamos guardando 200 px al lado de las
            // instrucciones para nada. Ahora se pone encima del borde de la rejilla, que esta justo vacia en
            // los unicos momentos en que el aviso tiene algo que decir.
            lblStatus.SetBounds(pad, albumY + 4, Math.Max(120, contentW), 22);
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
#if ROCOLA_GPU
            if (ListaPorGpu())
            {
                int pagerH = Math.Max(32, Math.Min(44, h / 20));
                if (songPager != null) songPager.SetBounds(listX, 48, listW, pagerH);
                int listY = 52 + pagerH;
                songList.SetBounds(listX, listY, listW, Math.Max(60, h - listY - 8));
            }
#endif
            // La lista contiene SOLO la pagina actual (sin scroll); al cambiar el tamaño se reparte de nuevo.
            if (panelSongs.Visible && songItems.Count > 0 && songList.RowH > 0) FillSongPage();
        }

        // El color de la seccion: dorado para lo mas sonado, el azul de los videos para los videos y, para una
        // letra, el color del genero que manda dentro de ella -el MISMO que luce arriba en la barra-.
        private Color AcentoSeccion()
        {
            if (currentLetter == TopMark) return Color.FromArgb(255, 205, 90);
            if (currentLetter == VideoMark) return Color.FromArgb(140, 220, 232);
            string et;
            if (letterTag != null && letterTag.TryGetValue(currentLetter, out et)) return ColorDeEtiqueta(et);
            return DefaultAccent;
        }

        private void LoadMusicFolder()
        {
            List<string> roots = AllMusicRoots();   // carpeta "musica" principal + las agregadas con Ctrl+F2 (otros discos)
            if (roots.Count > 0) StartScan(roots);
            else
            {
                lblStatus.Text = "No encontre musica. Conecta el disco donde este y eligelo en la ventana que se abre.";
                VigilaRaicesAusentes();
                PideElegirMusica();
            }
        }

        private void VigilaRaicesAusentes()
        {
            if (stateRestored) return;
            try { if (!File.Exists(StateFile()) && !File.Exists(StateFile() + ".bak")) return; } catch { return; }
            List<string> falta = new List<string>();
            List<string> cfg = new List<string>(); cfg.Add(MusicFolder);
            for (int i = 0; i < extraMusicDirs.Count; i++) cfg.Add(extraMusicDirs[i]);
            for (int i = 0; i < cfg.Count; i++)
            {
                string r = cfg[i]; if (string.IsNullOrEmpty(r)) continue;
                try { r = Path.GetFullPath(r); if (Directory.Exists(r)) continue; } catch { continue; }
                bool repetida = false;
                for (int k = 0; k < falta.Count; k++) if (string.Equals(falta[k], r, StringComparison.OrdinalIgnoreCase)) { repetida = true; break; }
                if (!repetida) falta.Add(r);
            }
            if (falta.Count > 0) { stateMissingRoots = falta; stateRootRetryTick = Environment.TickCount; }
        }

        // SIN MUSICA, LA VENTANA DE CARPETAS SE ABRE SOLA.
        //
        // Antes solo se escribia un letrero abajo diciendo "crea una carpeta musica al lado del .exe (o
        // agrega una con Ctrl+F2)". En la maquina del bar eso no sirve de nada: no hay ningun ".exe" a la
        // vista y hay que SABERSE una combinacion de teclas para poder hacer algo. Poner la musica es
        // mantenimiento -y ahi hay teclado y raton-, pero no puede hacer falta adivinar nada para llegar.
        //
        // EL SEGURO CONTRA EL BUCLE. La ventana, al cerrarse, vuelve a cargar la musica; si sigue sin haber,
        // esto se llamaria otra vez y se abriria sola para siempre, sin poder salir. Asi que se abre UNA vez
        // por sesion: si se cierra sin arreglar nada, se queda cerrada. Al proximo arranque vuelve a salir.
        private bool gestorAbierto = false;    // la ventana esta abierta AHORA
        private bool gestorOfrecido = false;   // ya se abrio sola una vez en esta sesion
        private void AbrePorFaltaDeMusica() { ShowMusicDirManager(true); }
        private void PideElegirMusica()
        {
            if (gestorAbierto || gestorOfrecido) return;
            gestorOfrecido = true;
            // Despues de que la pantalla este pintada: si no, la ventana saldria encima de una rocola a
            // medio dibujar.
            try { this.BeginInvoke(new MethodInvoker(AbrePorFaltaDeMusica)); } catch { }
        }
        private string lastFreshRootSig;  // firma tomada alrededor del MISMO recorrido que produjo la instantanea

        private void StartScan(List<string> roots)
        {
            if (scanner != null && scanner.IsBusy) return;   // ya hay un escaneo en curso (doble F5): ignorar
            CancelaPlanesBiblioteca(); // despertar los planes antes de vaciar la biblioteca de la generacion anterior
            ++preScanGen; ++durScanGen; // descartar tambien sus publicaciones ya encoladas en la pantalla
            InvalidateCoverMemory();   // F5/nuevo disco: una portada reemplazada no puede quedarse congelada en RAM
            libBuilding = true;   // desde aqui y hasta Completed, las listas son del worker
            if (searchOpen) CloseSearch();            // un re-escaneo invalida lo que el buscador muestra
            lastQuery = null; lastMatches = null;     // ...y su cache incremental (evita resultados huerfanos)
            bolsaLetra.Clear();                       // y lo guardado de cada letra suelta: apunta a fichas que ya no valen
            ClearRecentQueries();
            AdvanceThumbWarm();
            searchPostings = null;
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

        // Y como se dice. Sin tiempo fiable se dice SOLO el puesto: sigue sirviendo y no miente.
        internal static string TextoTurno(int puesto, double segundos)
        {
            // Apagado desde Ctrl+F3 devuelve texto vacio, y con eso el aviso vuelve solo al "Cancion
            // agregada" de siempre: el interruptor vive AQUI, en el unico sitio que escribe el letrero,
            // y no repartido por los dos sitios que lo piden.
            if (!turnoOn || puesto <= 0) return "";
            string donde = (puesto == 1) ? "Tu cancion es la siguiente" : ("Tu cancion es la " + puesto + "a");
            if (segundos < 0) return donde;
            if (segundos < 60) return donde + " - en menos de 1 min";
            return donde + " - en ~" + (int)((segundos + 30) / 60) + " min";
        }

        // Lo que le queda a la que suena. Con un VIDEO se devuelve "no se sabe": el video no lo lleva la
        // cadena de la rocola sino el reproductor de fuera, asi que por donde va no se puede preguntar
        // sin adivinar, y adivinar aqui es justo lo que no se quiere.
        private double RestanDeLaQueSuena()
        {
            if (nowPlaying == null) return 0;
            if (nowPlaying.IsVideo) return -1;
            double tot = SegundosDe(nowPlaying.PathKey());
            if (tot <= 0) return -1;
            double sin = QuedaSinSilencios(reader, tot);   // si se saltan silencios, lo que de verdad le queda por sonar
            if (sin >= 0) return sin;
            double va = 0; try { if (reader != null) va = reader.CurrentTime.TotalSeconds; } catch { }
            double q = tot - va;
            return (q > 0) ? q : 0;
        }

        // CUANTAS MINIATURAS FALTAN POR HACER. Lo mantiene el calentador y lo consulta el pre-analisis
        // para apartarse: las miniaturas son lo que SE VE en la pared, asi que van primero. En la maquina
        // de dos nucleos del bar, sueltos los dos a la vez se pelean por el disco y salen los dos peor.
        private volatile int minisPendientes;

        private void StartThumbWarm()
        {
            int myGen = AdvanceThumbWarm();
            List<Album> snap = new List<Album>(albums);
            System.Threading.Thread t = new System.Threading.Thread(delegate ()
            {
                try
                {
                    System.Threading.Thread.Sleep(12000);   // dejar respirar el arranque
                    // Antes de ponerse a construir, barrer lo antiguo (solo si la carpeta se hizo grande;
                    // ver BarreMiniaturas). Va aqui y no en el arranque para no gastar ni un milisegundo
                    // del rato en que la rocola esta apareciendo.
                    if (myGen != warmGen) return;
                    int barridas = BarreMiniaturas();
                    if (barridas > 0) Diag("miniaturas: barridas " + barridas + " de mas de " + MiniaturasDiasMax + " dias");
                    // CUANTAS FALTAN, antes de empezar. Es una pasada de File.Exists por disco -barata al
                    // lado de los 72 ms que cuesta hacer una- y con ella el pre-analisis sabe si tiene que
                    // apartarse: mientras queden miniaturas, mandan ellas, que son lo que se ve.
                    int faltan = 0;
                    for (int i = 0; i < snap.Count; i++)
                    {
                        if (myGen != warmGen) return;
                        Album a0 = snap[i];
                        if (a0 == null || a0.CoverPath == null) continue;
                        try { if (!File.Exists(ThumbCachePath(a0.CoverPath))
#if ROCOLA_GPU
                            || NeedsGpuPreview(a0)
#endif
                            ) faltan++; } catch { }
                    }
                    if (!PublishThumbRemaining(myGen, faltan)) return;
                    if (faltan > 0) Diag("miniaturas: faltan " + faltan + " por hacer");
                    for (int i = 0; i < snap.Count; i++)
                    {
                        if (myGen != warmGen) return;   // recargaron la musica: este hilo ya caduco
                        Album a = snap[i];
                        if (a == null || a.CoverPath == null) continue;
                        try { if (File.Exists(ThumbCachePath(a.CoverPath))
#if ROCOLA_GPU
                            && !NeedsGpuPreview(a)
#endif
                            ) continue; } catch { continue; }
                        // Si alguien uso la rocola hace poco, esperar (no robarle CPU ni DISCO a la interfaz).
                        //
                        // SALVO QUE ESTE SOLA. Hacer la miniatura de un disco cuesta 72 ms la primera vez
                        // contra 0.90 leyendola ya hecha -ochenta veces-, y con tres mil discos son tres
                        // minutos y medio de trabajo UNA vez en la vida de la rocola. Pero esperando ocho
                        // segundos sin botones antes de cada una, en un bar con gente eso no acaba nunca:
                        // semanas para tres minutos y medio, y mientras tanto la pared enseña las portadas
                        // llegando tarde. En atraccion, sin musica y sin video no hay a quien estorbar.
                        while (!RocolaSola() && Environment.TickCount - lastInputTick < 8000)
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
#if ROCOLA_GPU
                            string previewIdentity = gpuEnabled && caratulasLod ? ThumbCachePath(a.CoverPath) : null;
#endif
                            Image im = DecodeCover(a.CoverPath);   // crea la miniatura en disco como efecto
                            if (im != null)
                            {
                                try {
#if ROCOLA_GPU
                                    if (myGen == warmGen) WarmGpuPreview(a, im, previewIdentity);
#endif
                                } finally { im.Dispose(); }
                            }
                        }
                        catch { }
                        finally { coverSem.Release(); }
                        if (!PublishThumbRemaining(myGen, -1)) return;
                        // Respiro largo en disco duro; en SSD el freno era el respiro, no el disco. Y sola no
                        // hay de quien descansar.
                        if (!RocolaSola()) System.Threading.Thread.Sleep(diskSlow ? 150 : 20);
                    }
                    PublishThumbRemaining(myGen, 0);   // solo la generacion vigente
                }
                catch { }
                finally { PublishThumbRemaining(myGen, 0); }
            });
            t.IsBackground = true;
            t.Priority = System.Threading.ThreadPriority.Lowest;
            t.Start();
        }

        // Apunta la lista a la PAGINA donde cae la seleccion (songSel) y actualiza la barra de paginas.
        // El CONTENIDO de la lista cambio (llego un genero, una duracion, se movio la cola...). La lista se
        // dibuja horneada, asi que ademas de repintar hay que avisar de que lo horneado ya no vale. Todos los
        // sitios que tocan el contenido pasan por aqui: asi no se puede olvidar ninguno (lo vigila la bateria).
        private void RefrescaLista()
        {
            if (songList == null) return;
            songList.Datos++;
            songList.Invalidate();
        }

        // ================== SALIR DE LA ROCOLA (Ctrl+Alt+Q, tres veces) ==================
        //
        // Aqui la rocola se cierra con Alt+F4, que lo manda Windows. Pero Alt+F4 no siempre esta: en un
        // equipo montado como quiosco -sin escritorio, con el teclado capado por directiva- puede estar
        // bloqueado, y entonces no queda forma de salir sin reiniciar la maquina. En la rocola de Linux
        // directamente no existe. Asi que hay una tecla propia, que no depende de nadie.
        //
        // TRES VECES y no una: en un bar la rocola esta al alcance de cualquiera, y una sola combinacion se
        // aprieta sin querer o por curiosear. Tres seguidas dentro de unos segundos no le pasan a nadie por
        // accidente, y las dos primeras solo avisan en pantalla, asi que el que lo hizo sin querer ve lo que
        // esta a punto de pasar y para. Si se tarda, la cuenta vuelve a empezar: dos de anoche no suman con
        // una de hoy.
        //
        // Devuelve 0 si esa tecla no es la de salir, o 1, 2 o 3 = cuantas van. Quien llama ensena el aviso
        // y, al llegar a 3, cierra. Va aparte para poder probarlo sin tener que simular teclados.
        private const int SalirVeces = 3;                 // cuantas veces hay que pulsarlo
        private const double SalirSegundos = 3.0;         // y dentro de cuanto, entre una y otra
        private int salirCuenta = 0;
        private DateTime salirUltima = DateTime.MinValue;

        private int PidenSalir(Keys keyData)
        {
            if (keyData != (Keys.Control | Keys.Alt | Keys.Q)) return 0;
            DateTime ahora = DateTime.Now;
            if ((ahora - salirUltima).TotalSeconds > SalirSegundos) salirCuenta = 0;   // se tardo: vuelta a empezar
            salirUltima = ahora;
            salirCuenta++;
            if (salirCuenta > SalirVeces) salirCuenta = SalirVeces;
            return salirCuenta;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            VigiaPantalla.Hace("una tecla (" + keyData + ")");
            // SALIR (Ctrl+Alt+Q x3). Va lo primero para que ninguna pantalla se quede con la tecla: tiene
            // que funcionar siempre, este donde este el cliente. Cierra con Close(), que es el camino de
            // siempre -el mismo que Alt+F4-: cerrar
            // A PROPOSITO borra el estado guardado y manana la rocola arranca limpia (solo un apagon -que
            // no corre nada- deja el archivo, y por eso al volver la luz reanuda creditos y cola).
            int vanSalir = PidenSalir(keyData);
            if (vanSalir > 0)
            {
                if (vanSalir >= SalirVeces) { Close(); return true; }
                ShowToast("Salir de la rocola: " + vanSalir + " de " + SalirVeces, Color.FromArgb(200, 130, 35));
                return true;
            }

            // Mientras se leen las tablas del arranque (EsperaLasTablas) las teclas esperan su turno, como esperaban con la
            // ventana parada: una moneda no puede contarse antes de leer las monedas y lo mas sonado ya guardados (los
            // pisaria al publicarlos), y aun no hay biblioteca que elegir. Se atienden en orden al terminar.
            if (arranqueEnEspera)
            {
                if (teclasEnEspera == null) teclasEnEspera = new List<KeyValuePair<Message, Keys>>();
                if (teclasEnEspera.Count < 256) teclasEnEspera.Add(new KeyValuePair<Message, Keys>(msg, keyData));
                return true;
            }

            lastInput = DateTime.Now;   // cualquier tecla reinicia la inactividad
            lastInputTick = Environment.TickCount;   // el precalentador de miniaturas pausa si hay actividad
#if ROCOLA_GPU
            if (gpuEnabled && gpuPanel != null) gpuPanel.TouchUi();
#endif
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
                if (IsUp(kc) && BackHeld()) searchOverlay.FlashClose();
                else if (IsOK(kc)) searchOverlay.FlashOK();
                else if (IsUp(kc) || IsDown(kc)) searchOverlay.FlashMove(true);
                else if (IsLeft(kc) || IsRight(kc)) searchOverlay.FlashMove(false);
            }
            else if (helpBar != null && !attractOn)    // barra de navegacion / interior de disco
            {
                if (IsOK(kc)) helpBar.Flash(HelpBar.FEntrar);
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
                // El aviso dice como devolverlo, que ahora es la unica forma: ya no vuelve solo.
                if (maintMode) ShowToast("Mantenimiento: vigia pausado (F10 para reactivar)", Color.FromArgb(200, 130, 35));
                else ShowToast("Vigia de pantalla reactivado");
                return true;
            }

            // ABRIR / CERRAR EL BUSCADOR: ROJO + ARRIBA. Antes era ROJO + VERDE y nadie lo encontraba,
            // porque no habia forma de saber que era un combo. Ahora los dos botones estan DIBUJADOS en la
            // casilla de BUSCAR que parpadea arriba en medio, asi que no hay nada que adivinar.
            if (IsUp(kc) && BackHeld()) { ToggleSearch(); return true; }

            // FRENO DE LA AUTO-REPETICION DE LAS FLECHAS.
            //
            // Manteniendo pulsada una flecha, el teclado manda unas treinta pulsaciones por segundo. Cada una
            // mueve la seleccion y encarga un repintado... y la pantalla no da mas de sesenta cuadros por
            // segundo, asi que la mitad de ese trabajo no lo llega a ver nadie: solo sirve para llenar la cola
            // y que la rocola se sienta pastosa. Es el mismo problema que ya se filtra en el boton de la
            // moneda y en el de saltar.
            //
            // Pero aqui NO se puede tirar la repeticion como alli: mantener pulsada una flecha para recorrer
            // la biblioteca es como se usa una rocola de botones, y quitarlo seria peor que el problema. Lo
            // que se hace es ponerle TOPE: como mucho un movimiento cada 40 ms, que son veinticinco por
            // segundo -mas rapido de lo que la pantalla puede ensenar-. La primera pulsacion siempre pasa,
            // asi que pulsar suelto se siente exactamente igual que antes.
            //
            // Va por TIEMPO y no por el bit de "esto es una repeticion" a proposito: ese bit es cosa de
            // Windows, y esta rocola tambien corre en Linux, donde puede no venir.
            if (IsLeft(kc) || IsRight(kc) || IsUp(kc) || IsDown(kc))
            {
                int ahora = Environment.TickCount;
                if (ahora - ultimaFlecha < MsEntreFlechas) return true;   // demasiado seguidas: esta se descarta
                ultimaFlecha = ahora;
            }

            if (searchOpen)
            {
                MarkSearchActivity();
                if (IsLeft(kc)) { searchOverlay.MoveSel(-1, 0); return true; }
                if (IsRight(kc)) { searchOverlay.MoveSel(1, 0); return true; }
                if (IsUp(kc)) { searchOverlay.MoveSel(0, -1); return true; }
                if (IsDown(kc)) { searchOverlay.MoveSel(0, 1); return true; }
                if (IsOK(kc))
                {
                    // Una pulsacion, cuatro creditos: no repetir por mantener el boton.
                    if (!SecretCreditGate.Matches(searchOverlay.Escrito) || (msg.LParam.ToInt64() & 0x40000000L) == 0)
                        searchOverlay.PressSelected();
                    return true;
                }
                if (IsBack(kc)) { searchOverlay.Backspace(); return true; }   // REGRESAR = borrar letra; cerrar = REGRESAR+PONER
                // letras, numeros, Ñ, acentos, comillas, espacio y BORRAR los entrega KeyPress (admite TODO caracter)
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (panelSongs.Visible)
            {
                int rpc = SongRowsPerPage();   // misma cuenta que la paginacion: izq/der = pagina anterior/siguiente
                // Con ROJO mantenido, arriba/abajo NO navegan: queda reservado para el combo de ocultar
                // (ROJO+ARRIBA+ABAJO 5 s), asi la lista no salta mientras se mantiene el combo.
#if ROCOLA_GPU
                if (IsUp(kc)) { if (!BackHeld()) MoveSongInput(-1, true); return true; }
                if (IsDown(kc)) { if (!BackHeld()) MoveSongInput(1, true); return true; }
                if (IsLeft(kc)) { MoveSongInput(-rpc, false); return true; }
                if (IsRight(kc)) { MoveSongInput(rpc, false); return true; }
#else
                if (IsUp(kc)) { if (!BackHeld()) MoveSong(-1); return true; }
                if (IsDown(kc)) { if (!BackHeld()) MoveSong(1); return true; }
                if (IsLeft(kc)) { MoveSong(-rpc); return true; }     // pagina anterior
                if (IsRight(kc)) { MoveSong(rpc); return true; }      // pagina siguiente
#endif
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

        private void AddSong(Track t, Album disc)
        {
            if (t == null) return;
            PlayItem it = new PlayItem(); it.IsVideo = false; it.Song = t; it.Disc = disc;
            AddResult r = AddItem(it);
            if (r == AddResult.Added) ShowSongAddedToast(false);
            else if (r == AddResult.NoCredit) { if (!InvitaSinCredito(it)) ShowToast("Inserta una moneda", Color.FromArgb(200, 130, 35)); }
            else ShowToast("Esa ya va seguida", Color.FromArgb(200, 130, 35));
        }

        private void AddVideoToQueue(string path, Album disc)
        {
            if (string.IsNullOrEmpty(path)) return;
            PlayItem it = new PlayItem(); it.IsVideo = true; it.VideoPath = path; it.Disc = disc;
            AddResult r = AddItem(it);
            if (r == AddResult.Added)
            {   // el video tambien dice su turno: para quien lo pidio es exactamente lo mismo
                string mv = TextoTurno(turnoPuesto, turnoSegs);
                AvisoPedido();   // con avisos claros: "Pusiste «...»" y cuanto falta (SuenaVivoGpu.cs)
                ShowToast(mv.Length > 0 ? mv : "Video agregado a la cola");
            }
            else if (r == AddResult.NoCredit) { if (!InvitaSinCredito(it)) ShowToast("Inserta una moneda", Color.FromArgb(200, 130, 35)); }
            else ShowToast("Ese video ya va seguido", Color.FromArgb(200, 130, 35));
        }

        // Algo que el cliente pago (cancion o video) no se pudo reproducir: como encolarlo costo 1 credito, se le
        // devuelve. Sube 1 credito y guarda el estado. El aviso ("credito devuelto") lo pone cada llamador.
        private void RefundCredit()
        {
            credits++;
            UpdateCredits();
            SaveState();   // estado vivo: los creditos cambiaron
        }

        // Guarda el error completo (tipo + mensaje + pila) en rocola_error.txt junto al .exe,
        // para poder ver el metodo exacto que falta (p. ej. por una version equivocada de NAudio).
        // Cada apunte lleva la fecha del EJECUTABLE al lado (ver el mismo sello en Program.LogCrash): un
        // error que llega por mensaje no se puede diagnosticar si no se sabe de que version salio.
        private void LogError(string context, Exception ex)
        {
            // Lo escribe un hilo de fondo (Bitacora, en MedidasBitacora.cs), que tambien le pone el sello. Tope ~256KB.
            try
            {
                Bitacora.ApuntaConSello(RutaDatos("rocola_error.txt"), 262144, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + context + "]",
                                        "\r\n" + (ex != null ? ex.ToString() : "(sin excepcion)") + "\r\n\r\n");
            }
            catch { }
        }

        // Anima el brillo del letrero MEJORADA. Sin panel GPU encima (gpuCovered=false) las letras RESPIRAN
        // de color (repintar una etiqueta chica es barato); con el panel GPU el texto va FIJO en la textura de
        // la franja y el brillo es un destello que RECORRE las letras (FillUiGlows): cero rehechas de textura.
        private void TickMejorada(bool gpuCovered)
        {
            if (lblMejorada == null) return;
            int k = MejActivo() ? 2 : 0;   // en cuanto el reconstructor ACTUA (~0.1s) aparece, y queda TODA la cancion (solo se va con el switch o al cambiar de cancion)
            if (k != iaBadgeKind || (k != 0) != lblMejorada.Visible) { iaBadgeKind = k; PositionMejorada(); CambioElPanelDeReposo(); }
            if (!lblMejorada.Visible) return;
            if (!gpuCovered)
            {
                double pm = 0.5 + 0.5 * Math.Sin(phase * 2.8);
                lblMejorada.ForeColor = LerpColor(MejGoldBase, MejGoldHi, pm);
            }
            else if (lblMejorada.ForeColor.ToArgb() != MejGoldBase.ToArgb()) lblMejorada.ForeColor = MejGoldBase;   // al volver de CPU: color base fijo en la textura
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            VigiaPantalla.Hace("el latido (animaciones y avisos)");
            // Mientras se leen las tablas del arranque no se anima nada (EsperaLasTablas): antes la ventana ni atendia
            // este reloj, y en dos nucleos ese rato de procesador es del lector.
            if (arranqueEnEspera) return;
            phase += 0.045;
            frameTick++;
            // Si el disco que contenia la cola de un apagon vuelve a aparecer, reescanea y restaura sin
            // obligar a reiniciar la rocola. La comprobacion es espaciada para no castigar discos lentos.
            if (!stateRestored && stateMissingRoots != null && stateMissingRoots.Count > 0
                && Environment.TickCount - stateRootRetryTick > 5000
                && (scanner == null || !scanner.IsBusy) && nowPlaying == null && queue.Count == 0)
            {
                stateRootRetryTick = Environment.TickCount;
                bool alguna = false;
                for (int i = 0; i < stateMissingRoots.Count; i++)
                    try { if (Directory.Exists(stateMissingRoots[i])) { alguna = true; break; } } catch { }
                if (alguna)
                {
                    stateMissingRoots = null;
                    List<string> roots = AllMusicRoots();
                    if (roots.Count > 0) StartScan(roots);
                }
            }
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
            DiscoVivoTic();   // dentro del disco: los minutos de la cola y el vinilo de lo que suena (DiscoVivoGpu.cs)
            SuenaVivoTic();   // y el vinilo del panel de reposo en grande (SuenaVivoGpu.cs)
            AtraccionTic();   // y lo que se hornea para la atraccion, una cosa por vuelta (AtraccionGpu.cs)
#endif
            DrainCoverTrash();   // libera carátulas expulsadas (seguro: no hay repintado en curso)
#if ROCOLA_GPU
            // Con la rocola quieta, se van horneando las casillas de la pagina de al lado: cuando alguien pase
            // de pagina, ya estan listas y el deslizamiento sale fluido de una. Dos casillas por vuelta y con
            // tope de 4 ms: si alguien esta usando la rocola en ese momento, no le quita nada.
            if (gpuEnabled && albumView != null && albumView.Visible && !attractOn && !searchOpen && !videoOn
                && transMode == 0 && winMode == 0 && !albumView.Sliding && coverGovernor.BeginBackground(GpuPanel.AhoraMs()))
                albumView.BakeAheadGpu();
#endif
            PrecalientaUnPoco();   // y las pantallas que al encender no se ven, horneadas antes de que hagan falta
            CheckBusinessDayReset();   // al cruzar las 7 AM: reinicia el estado (nada de anoche cruza el corte del dia)
            AvanzaModoNoche();         // y de 11pm a 7am, el volumen baja 4 dB (bajando poco a poco, sin escalon)

            // BEAT: toma el RMS cuyo momento de OIRSE ya llego (medido ~0.85s antes, al llenar el colchon) y
            // detecta GOLPES contra un piso movil: el pulso visual cae encima del golpe que se esta oyendo,
            // con ataque INSTANTANEO y caida rapida (pulso marcado), mas un fondo suave para que no muera
            // entre golpes. Antes era una envolvente de sonoridad adelantada ~1s: no transmitia el ritmo.
            int nowT = Environment.TickCount;
            ConsumeLevels(nowT);
            RevisaRelevo(nowT);   // la siguiente entro sin pausa y ya se oye: la pantalla se entera (Form1.SinPausa.cs)
            if (nowPlaying == null) { rmsLo = rmsMid = rmsHi = 0f; for (int b = 0; b < SpecBands; b++) specNow[b] = 0f; }
#if ROCOLA_GPU
            // Con un VIDEO en pantalla el audio sale del reproductor de Windows y NO pasa por la cadena, asi que no
            // llegan bandas nuevas: sin esto el fondo se quedaria CONGELADO con la forma de la ultima cancion durante
            // todo el video. (El pulso no lo sufre: su detector de golpes se apaga solo contra su piso movil.)
            // La aurora la avanza la tarjeta en CADA cuadro (AuroraFinaGpu.cs); el latido solo la lleva si la tarjeta
            // lleva un rato sin pedir cuadros, para que la presencia y la respiracion sigan su curso.
            if (!AuroraFinaViva())
            {
                if (videoOn || !EspectroActivo) for (int b = 0; b < SpecBands; b++) specNow[b] = 0f;
                AvanzaAurora();   // el fondo lo dibuja el panel GPU; por CPU ni se calcula
            }
#endif
            // Cada banda detecta su propio golpe contra su propio piso movil (ataque instantaneo, caida rapida). Asi
            // el bombo (graves) y los platillos/voz (agudos) laten por separado y se SIENTE la separacion de beats.
            levLo = BandBeat(rmsLo, ref avgLo, levLo);
            levMid = BandBeat(rmsMid, ref avgMid, levMid);
            levHi = BandBeat(rmsHi, ref avgHi, levHi);
            // LA DERIVA DEL CAMPO DE ESTRELLAS, UNA VEZ POR LATIDO Y CON EL RELOJ EN LA MANO. Va aqui y no en
            // el dibujado porque el dibujado no ocurre a un ritmo parejo -depende de lo que toque rehacer en
            // cada momento- y el fondo tiene que deslizarse liso, no a tirones. El campo es UNO solo y lo
            // comparten el fondo, la pared de discos, el interior y el buscador: una sola cuenta los mueve a
            // todos a la vez y sin costuras.
            formStars.AvanzaDeriva(Math.Max(levLo, Math.Max(levMid, levHi)));
#if ROCOLA_GPU
            audioLevel = Math.Max(levLo, Math.Max(levMid, levHi));   // nivel GLOBAL para la tarjeta grafica: el mas fuerte de las 3
#endif

            // ATENUACION del starfield mientras se navega con musica sonando (ver BgDimLevel); transicion suave.
            float dimTgt = (nowPlaying != null && !attractOn && (DateTime.Now - lastInput).TotalSeconds < BgDimIdleSecs) ? BgDimLevel : 1f;
            bgDim += (dimTgt - bgDim) * 0.12f;
            bool muroQuieto = muroReposo && nowPlaying != null && !videoOn && !attractOn &&
                !searchOpen && !configMode && (lblConfig == null || !lblConfig.Visible) &&
                !ajustesAbiertos && albumView.Visible && (panelSongs == null || !panelSongs.Visible) &&
                (DateTime.Now - lastInput).TotalSeconds >= 15;
            muroAlpha = PasoMuro(muroAlpha, muroQuieto);
            albumView.WallOpacity = muroAlpha;
#if ROCOLA_GPU
            if (gpuPanel != null) gpuPanel.WallOpacity = GpuPanel.IdleWallAlpha(muroAlpha); // GPU: desaparece en reposo; CPU intacto.
            if (gpuPanel != null) gpuPanel.IdleExpandTarget = estadoExpandido && muroQuieto;
#endif
            if (Math.Abs(bgDim - dimTgt) < 0.01f) bgDim = dimTgt;
            // LA RESPIRACION va APARTE de bgDim y no dentro, a proposito: bgDim es de quien sale la cuenta
            // de 'quieto' que calma la aurora, y metiendosela dentro se armaria una pescadilla que se muerde
            // la cola. Aqui se multiplican al final, para pintar.
            quietud = PasoQuietud(quietud, (DateTime.Now - lastInput).TotalSeconds < BgDimIdleSecs);
            respiraFase += RespiraPaso;
            if (respiraFase > 6.2831853f) respiraFase -= 6.2831853f;
            // DOS COSAS DISTINTAS, y hay que tenerlas separadas.
            //
            // 'bgDimResp' es la atenuacion al navegar CON la respiracion encima, y va a las estrellas y a
            // los destellos. 'bgRespiro' es SOLO la respiracion, y es la que va a los colores del CIELO.
            //
            // Mezclarlas seria un fallo gordo: la atenuacion al navegar baja al 45%, y aplicada al cielo
            // dejaria toda la pantalla a media luz en cuanto alguien toca un boton con musica sonando. El
            // cielo no se atenua -nunca se atenuo- pero si respira.
            // LA VISTA PREVIA de Ctrl+F3: va deprisa y sin freno, y se apaga sola. Fuera de ella, todo igual.
            bool demo = respiraOn && DateTime.Now < respiraDemoHasta;
            if (demo) respiraFase += RespiraDemoPaso - RespiraPaso;   // el paso normal ya se sumo arriba
            bgRespiro = respiraOn ? Respiracion(respiraFuerza, demo ? 1f : quietud, respiraFase) : 1f;
            bgDimResp = bgDim * bgRespiro;
            albumView.BgDim = bgDimResp;
            if (panelSongs != null) panelSongs.BgDim = bgDimResp;   // el interior del disco comparte el mismo campo: atenua igual
            if (searchOverlay != null) searchOverlay.BgDim = bgDimResp;
            // Mientras la atenuacion CAMBIA, repinta el fondo del form (franjas de arriba/abajo) para que atenuen
            // junto con la rejilla: el campo es UNA SOLA PIEZA (si no, se veria una banda mas clara arriba/abajo).
            //
            // Y el umbral hace aqui un segundo trabajo que importa en la maquina del bar: la respiracion mueve
            // el brillo tan despacio que la mayoria de los cuadros no lo cambian lo bastante para verse, y esos
            // no obligan a repintar nada. Medido con el 2.5% de fabrica: 61 repintados en las 720 vueltas de
            // reloj que dura un ciclo, uno de cada once.
            if (Math.Abs(bgDimResp - bgDimPrev) > 0.0015f) this.Invalidate();
            bgDimPrev = bgDimResp;
#if ROCOLA_GPU
            if (gpuPanel != null) gpuPanel.Dim = bgDimResp;
#endif

            bool transitioning = (bgAccent.R != bgAccentTarget.R || bgAccent.G != bgAccentTarget.G || bgAccent.B != bgAccentTarget.B);
            if (transitioning)
            {
                bgAccent = LerpColor(bgAccent, bgAccentTarget, 0.16);
                if (Math.Abs(bgAccent.R - bgAccentTarget.R) <= 2 && Math.Abs(bgAccent.G - bgAccentTarget.G) <= 2 && Math.Abs(bgAccent.B - bgAccentTarget.B) <= 2)
                    bgAccent = bgAccentTarget;
                CambioElAcentoDelPanel(bgAccent);   // con umbral: esto corre en CADA latido, no solo al cambiar de cancion
                this.Invalidate();   // tambien con buscador abierto: el fondo es barato y la franja de estado debe recolorear
            }
            albumView.BgAccent = bgAccent;
            // LAS DOS NEBULOSAS. Se calculan aqui, una sola vez por cuadro, y de aqui tiran los dos caminos:
            // el del panel de tarjeta grafica y el del horneado del cielo. Es una cuenta de dos colores.
            //
            // Se DESLIZAN, como el acento: al cambiar de cancion el color de destino da un salto -otro genero,
            // otra portada- y el que se pinta llega a el en el mismo segundo escaso de siempre. Y como el
            // destino ademas se mueve solo (la portada va cruzando entre sus dos colores), el deslizamiento
            // lo sigue sin enterarse.
            nebFase += NebPaso;
            if (nebFase > 6.2831853f) nebFase -= 6.2831853f;
            hazFase += HazPaso;
            if (hazFase > 6.2831853f) hazFase -= 6.2831853f;
            Color nebAMeta, nebBMeta;
            ColoresNebulosa(bgGeneroTarget, bgPort1, bgPort2, nebFase, out nebAMeta, out nebBMeta);
            if (bgSinGenero) NebulosasSinGenero(bgPort1, bgPort2, nebFase, ref nebAMeta, ref nebBMeta);
            else if (bgArtistaFijo) nebAMeta = nebBMeta = ColorNebulosa(bgGeneroTarget);
            nebA = LerpColor(nebA, nebAMeta, 0.16);
            nebB = LerpColor(nebB, nebBMeta, 0.16);
#if ROCOLA_GPU
            SyncGpuFullScreen();   // muestra/oculta el panel GPU (pantalla completa) segun el modo, ANTES de los returns por modo
            if (gpuPanel != null && gpuPanel.Visible)
            {
                if (attractOn)   // atraccion: cielo nocturno fijo (sin musica, no sigue al genero)
                { gpuPanel.SkyTop = 0xFF06081Au; gpuPanel.SkyMid = 0xFF0C0E28u; gpuPanel.SkyBot = 0xFF12143Au; }
                else
                {
                    // EL CIELO RESPIRA AQUI. Estos tres colores se le pasan al panel ya calculados, asi que
                    // multiplicarlos no cuesta nada de tarjeta grafica: son tres numeros por cuadro. Antes la
                    // respiracion iba solo a 'Dim', que mueve las estrellas y los destellos y deja el cielo
                    // igual: la superficie grande -la que uno llama "la pared"- no se enteraba, y por eso no
                    // se veia respirar.
                    gpuPanel.SkyTop = ToD3D(EscalaBrillo(SkyColor(bgAccent), bgRespiro));
                    gpuPanel.SkyMid = ToD3D(EscalaBrillo(SkyColorMid(bgAccent), bgRespiro));
                    gpuPanel.SkyBot = ToD3D(EscalaBrillo(SkyColor2(bgAccent), bgRespiro));
                    // LAS DOS NEBULOSAS: una el genero, la otra la caratula. El desplazamiento va en la
                    // misma fase lenta, y en sentidos contrarios: es el cruce de las dos lo que se ve moverse.
                    gpuPanel.NebColor = ToD3D(EscalaBrillo(nebA, bgRespiro));
                    gpuPanel.NebColor2 = ToD3D(EscalaBrillo(nebB, bgRespiro));
                    gpuPanel.NebFase = nebFase;
                }   // el degradado sigue al genero
                // EL RELOJ LENTO DEL CIELO Y LA LUNA VAN FUERA DEL SI: en modo atraccion el cielo se queda
                // quieto de color -no sigue al genero porque no hay cancion- pero sigue siendo un cielo, y
                // ahi es justamente donde mas rato se le mira.
                gpuPanel.HazFase = hazFase;
                gpuPanel.ConLuna = lunaOn;
                SyncGpuCoverPalette();
                gpuPanel.GeneroArgb = ToD3D(bgAccent);   // el color del genero, para el resplandor de las portadas (EstiloGpu.cs)
            }
#endif
            if (progress != null) progress.Accent = bgAccent;   // ESTETICA: la barra de progreso sigue el color del genero

            if (searchOpen)
            {
                if ((DateTime.Now - searchLastActivity).TotalSeconds >= 60) { CloseSearch(); return; }   // 1 min sin actividad
                UpdateSecretCredits();
                searchOverlay.Accent = bgAccent;
#if ROCOLA_GPU
                if (gpuEnabled && coloresCache) searchOverlay.Accent = bgAccentTarget;
#endif
                searchOverlay.Phase = phase;
                searchOverlay.PulseLo = levLo; searchOverlay.PulseMid = levMid; searchOverlay.PulseHi = levHi;   // cada estrella late con SU banda
                searchOverlay.CubeAngle += 0.075;   // gira mas rapido
                // El cuadro COMPLETO una de cada tres veces (~8/seg, el mismo criterio que el fondo del resto del
                // programa); en los otros dos, solo lo que de verdad se mueve. Repintar la pantalla entera 24 veces
                // por segundo era el gasto fijo mas grande de la rocola. (No-op si el buscador va por GPU: el
                // control esta oculto y lo dibuja el panel.)
                if (fullFrame) searchOverlay.Invalidate();
                else searchOverlay.InvalidarVivo();
                bool sGpu = false;
#if ROCOLA_GPU
                sGpu = (gpuPanel != null && gpuPanel.Visible);   // panel GPU encima: el mini-cubo lo dibuja la GPU (repintar el control seria invisible)
#endif
                if (miniCube.Visible) { miniCube.Phase = phase; miniCube.Avanza(0.06); if (!sGpu || miniCube.Girando) miniCube.Invalidate(); }   // el angulo avanza siempre (la GPU lo lee)
                TickMejorada(sGpu);   // la franja de estado sigue visible bajo el buscador: el letrero sigue brillando
                return;
            }

            // Y lo mismo para el INTERIOR DE UN DISCO: a los 30 s sin tocar nada, de vuelta a la pared de
            // discos (ver DiscoIdleSecs). Con un video puesto no: ahi el disco abierto es lo de menos y el
            // video manda. Ni con el asistente de botones a la vista.
            // Ni con el asistente de botones a la vista, ni en MANTENIMIENTO: mientras el operador tiene
            // abierta una ventana de ajustes, sacarle el disco de debajo seria de lo mas molesto.
            if (panelSongs != null && panelSongs.Visible && !videoOn && !searchOpen && !maintMode
                && (lblConfig == null || !lblConfig.Visible)
                && (DateTime.Now - lastInput).TotalSeconds >= DiscoIdleSecs)
            { BackToAlbums(); return; }

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
            // (CPU) La pagina de AL LADO se hornea mientras nadie toca nada, igual que en la version con tarjeta
            // grafica: al pulsar "siguiente" las casillas ya estan hechas y no hay que re-escalar 18 caratulas en
            // ese instante. Con tope de dos por vuelta y 4 ms, asi que quien este usando la rocola no lo nota.
            if (!albumGpuBg && albumView != null && albumView.Visible) albumView.BakeAhead();
            // HOJEO dentro de un disco: sin tarjeta grafica la lista y su letrero se repintan solos hasta que
            // llegan. (Con tarjeta no hace falta: el hojeo entra en la firma de la textura y se rehace sola.)
            if (!albumGpuBg && panelSongs != null && panelSongs.Visible)
            {
                if (songList != null && songList.Sliding) songList.Invalidate();
                if (songPager != null && songPager.Animando) songPager.Invalidate();
            }

            // (CPU) Anima el BRILLO del boton pulsado en la barra de ayuda: sin esto el destello se disparaba
            // pero nadie repintaba el control -> no se veia. Repinta mientras dura (~0.4s) y UNA vez mas al
            // expirar (para borrar el ultimo halo). En GPU lo dibuja el panel como halo aparte (FillUiGlows).
            // La ceremonia de la moneda: mientras cae, la capsula se repinta. Con tarjeta grafica no hace
            // falta repintar el control -lo dibuja la textura de la interfaz-, pero su firma tiene que
            // cambiar, y de eso se encarga UiBotSig mirando el mismo Paso.
            if (pastCred != null && pastCred.Animando && !albumGpuBg) pastCred.Invalidate();
            if (helpBar != null)
            {
                int hfs = helpBar.FlashSig();
                if ((hfs != 0 || helpFlashPrev != 0) && helpBar.Visible && !albumGpuBg) helpBar.Invalidate();
                helpFlashPrev = hfs;
            }
            if (buscaView != null && buscaView.Visible)
            {
                buscaView.Phase = phase; buscaView.Abierto = searchOpen;
                if (!albumGpuBg) buscaView.Invalidate();   // en GPU el latido es un halo aparte: la textura no se rehace
            }
            if (letterBar.Visible)
            {
                letterBar.Phase = phase;
                // Solo se repintan los dos trozos que laten, no la barra entera (ver ZonasLatido). En GPU no
                // se repinta nada: el pulso de la letra es un halo por quad (FillUiGlows).
                if (!albumGpuBg)
                {
                    Rectangle zl1, zl2;
                    if (letterBar.ZonasLatido(out zl1, out zl2))
                    {
                        if (zl1.Width > 0) letterBar.Invalidate(zl1);
                        if (zl2.Width > 0) letterBar.Invalidate(zl2);
                    }
                    else letterBar.Invalidate();
                }
            }
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
            if (miniCube.Visible) { miniCube.Phase = phase; miniCube.Avanza(0.06); if (!albumGpuBg || miniCube.Girando) miniCube.Invalidate(); }   // idem mini-cubo (en GPU lo dibuja el panel)
        }

        // Barra de ayuda: instrucciones con FORMAS y COLORES de los botones fisicos. Sin nombres de teclas.
        private class HelpBar : Control
        {
            // BUSCAR ya no esta aqui: tiene su propia casilla grande y parpadeante en medio de la fila, con
            // los dos botones dibujados. Aqui se quedan los tres que se usan a cada rato.
            public const int FMover = 0, FEntrar = 1, FVolver = 2;   // simbolo a resaltar
            private static readonly Color RedC = Color.FromArgb(228, 72, 72), GreenC = Color.FromArgb(72, 212, 112), YellowC = Color.FromArgb(242, 206, 72);
            private readonly InstrFlash flash = new InstrFlash();

            protected override void Dispose(bool disposing)
            {
                // Solo recursos creados por esta vista: Font y portadas recibidas son prestadas.
                try { if (disposing) { SueltaDibujo(ref hbFont); } }
                finally { base.Dispose(disposing); }
            }

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
            private readonly float[] fgX = new float[3], fgY = new float[3], fgW = new float[3];
            private void StoreGlow(int k, float leftX, float cy, float symW) { fgX[k] = leftX + symW / 2f; fgY[k] = cy; fgW[k] = symW * 0.9f + 6f; }
            public bool GetFlashGlow(int k, out float cx, out float cy, out float r, out int argb)
            {
                cx = cy = r = 0f; argb = 0;
                if (k < 0 || k > 2) return false;
                float lvl = flash.Level(k);
                if (lvl <= 0.02f) return false;
                cx = fgX[k]; cy = fgY[k]; r = fgW[k];
                Color c = flash.Col(k);
                argb = Color.FromArgb((int)(lvl * 215f), c.R, c.G, c.B).ToArgb();   // alfa = brillo del destello; rgb = su color
                return true;
            }
            // LO QUE MIDE DE VERDAD. El acomodo le reservaba 400 px a ojo, y como sobraban, la casilla de
            // BUSCAR quedaba corrida a la derecha en vez de en el centro de la pantalla. Se mide una sola vez
            // por alto de fila (la letra es fija) y se guarda: esto se llama al acomodar, no al dibujar.
            private static int anchoAlto = -1, anchoCache = -1;
            public static int Ancho(int alto)
            {
                if (anchoAlto == alto) return anchoCache;
                int d = Math.Min(20, alto - 8); if (d < 12) d = 12;
                float gap = 7, grp = 20, x = 2;
                using (Font f = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold))
                using (Bitmap b = new Bitmap(1, 1))
                using (Graphics g = Graphics.FromImage(b))
                {
                    x += d + 4;                                                       // la cruceta
                    x += gap + g.MeasureString("Mover", f).Width + grp;
                    x += d + gap + g.MeasureString("Entrar / Poner", f).Width + grp;
                    x += d + gap + g.MeasureString("Volver", f).Width;
                }
                anchoAlto = alto; anchoCache = (int)x + 8;   // un pelin de aire al final
                return anchoCache;
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
    }
}

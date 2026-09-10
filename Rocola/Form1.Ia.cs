// LA IA: la red propia y el puente con el motor de Apollo
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
        // De que canciones se saco material para la IA y HASTA QUE NIVEL: 1 = el muestreo del modo automatico
        // (192 cuadros), 2 = el repaso A FONDO (12 veces mas). Se guarda el nivel, no un simple "ya esta",
        // porque los dos modos ACUMULAN: lo que junto el modo automatico se queda, y el repaso a fondo le
        // anade el resto. Dentro del MISMO nivel una cancion aporta una sola vez, para no sesgar hacia ella.
        private Dictionary<string, int> iaDone = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // MATERIAL DE ENTRENAMIENTO PARA LA IA: mientras el pre-analisis recorre la biblioteca, las canciones
        // que AUN conservan sus agudos (320k/FLAC/WAV) sirven de maestras: se les corta el agudo a proposito y
        // se guarda "lo que queda / lo que habia". Ese archivo se lleva en una USB a una PC potente, se entrena
        // alli con ia/entrenar_ia.py y se regresa el rocola_ia.bin nuevo. Asi la IA aprende de TODA la
        // biblioteca sin tener que mover el disco de la rocola. "ia_datos=0" en config lo apaga.
        private bool iaHarvestOn = true;
        private volatile int iaPairsTotal = 0;          // pares ya guardados (solo para mostrar el avance)
        // ===== IA EN VIVO (Apollo por demanda, rama HD) =====
        // Un motor externo (ia/tiempo_real/daemon_ia.py) mantiene Apollo en la GPU y procesa las canciones
        // que se ENCOLAN, dejando el resultado en una cache compartida. La rocola: al encolar pide el trabajo;
        // al sonar, si la version IA ya esta lista la reproduce, si no reproduce la ORIGINAL al instante (nunca
        // se bloquea). El switch DSEE ULTRA (Ctrl+F3) la enciende/apaga. Requiere GPU en la maquina de la rocola.
        private bool iaVivo = false;
        private bool iaNowEnhanced = false;      // la cancion actual suena con la version IA (Apollo) de la cache -> letrero MEJORADA dorado
        private bool iaSpectralFallback = false; // ULTRA activo pero la version IA aun no esta lista -> DSEE espectral MIENTRAS (1a vez) -> letrero plateado
        private Label lblMejorada;               // letrero "MEJORADA" junto a "Reproduciendo": plateado = DSEE espectral, dorado = version IA (Apollo)
        private int iaBadgeKind;                 // 0 = sin letrero, 1 = plateado (DSEE), 2 = dorado (IA)
        private int iaNowRowW = 120;             // ancho total de la fila "Reproduciendo" (lo fija el layout; el letrero se acomoda dentro)
        private static readonly Color IaSilverBase = Color.FromArgb(198, 205, 218), IaSilverHi = Color.FromArgb(246, 249, 255);   // plateado: base y brillo
        private static readonly Color IaGoldBase = Color.FromArgb(238, 192, 76), IaGoldHi = Color.FromArgb(255, 233, 145);        // dorado: base y brillo
        private int iaMotorCheckTick = 0;        // vigia: cada ~10s, si ULTRA esta activo y el motor no responde, lo relanza
        private string iaMezcla = "auto";     // cuanta IA (auto | 0..100); coincide con el motor
        private string iaGpu = "auto";        // que GPU hace los calculos de IA (auto | 0 | 1 | ...); selector en Ctrl+F3
        private string iaCacheDir = null;     // cache compartida con el motor (ia_cache= en config; por defecto junto al exe)
        private string outPref = "auto";     // salida= en config: auto (WASAPI si la maquina es HD) | wasapi | waveout
        private SpectralTap curSbr;          // tap de la cancion actual (para alternar A/B en vivo)
        private const float NormMaxBoost = 16.0f;                                             // techo ABSOLUTO de subida (+24 dB): evita amplificar al infinito una cancion casi muda (el tope real lo pone la etapa, ver TierMaxDb)
        private const float NormTargetMin = 0.06f, NormTargetMax = 0.40f;                     // rango del volumen objetivo que ofrece la interfaz (de flojo a muy fuerte)
        private const long NormMinSamples = 800000;                                           // ~9s en estereo 44.1k: minimo para aceptar una medida (si se salta antes, no se guarda)
        // ARCHIVOS INCOMPATIBLES: canciones que NO se pudieron reproducir ni con el lector tolerante (p.ej. MP3
        // demasiado dañados). Se anotan (una vez) en rocola_incompatibles.txt para poder repararlas/reconvertirlas.
        private HashSet<string> incompatibleLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // ----- PAGINAS de canciones (vista de disco): la lista muestra solo la pagina actual -----
        private SongPagerBar songPager;                        // barra "pagina X de Y" con flechas, arriba de la lista
        private List<object> songItems = new List<object>();   // TODAS las canciones/videos del disco abierto
        private int songSel = 0;                               // seleccion global (entre todas las paginas)
        private readonly List<PlayItem> queue = new List<PlayItem>();
        private static bool turnoOn = true;       // "turno=" en config
        private static bool marcaColaOn = true;   // "marca_cola=" en config
        // Y APARTE, cual esta sonando. Suelto y no dentro de colaPos porque una cancion puede estar sonando
        // Y ademas pedida otra vez: son dos cosas distintas y el cliente necesita ver las dos -que la esta
        // oyendo, y que ya la va a volver a oir-. Metidas en el mismo dato, una tapaba a la otra.
        private string sonandoKey = "";
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
        private readonly Dictionary<string, int> songCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Topes del archivo de estadisticas: se conserva lo MAS sonado y reciente; lo viejo/poco sonado
        // se descarta solo, para que el archivo nunca crezca de mas ni alente la rocola.
        private const int SongStatCap = 2000, SongStatTarget = 1800;
        private readonly Dictionary<string, long> songSeq = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);   // recencia (cuando se sono por ultima vez)

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
            int carriles = 1;
            if (!lento) { carriles = Environment.ProcessorCount; if (carriles > 4) carriles = 4; else if (carriles < 2) carriles = 2; }
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
            if (perfilHecho || lista == null || lista.Count == 0) return;
            perfilHecho = true;
            try
            {
                // Las medidas se van APARTANDO POR UNIDAD: si la musica esta repartida entre un SSD y un disco
                // duro, cada uno saca su propio veredicto en vez de salir todo del promedio de los dos.
                Dictionary<string, List<int>> porUnidad = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                byte[] buf = new byte[4096];
                Random rnd = new Random(unchecked(Environment.TickCount * 397));
                int paso = lista.Count / MuestrasDisco; if (paso < 1) paso = 1;
                int hechas = 0;
                for (int i = 0; i < lista.Count && hechas < MuestrasDisco; i += paso)
                {
                    Track t = lista[i];
                    if (t == null || t.Path == null) continue;
                    try
                    {
                        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                        using (FileStream fs = new FileStream(t.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
                        {
                            long largo = fs.Length;
                            if (largo > 8192) fs.Position = (long)(rnd.NextDouble() * (largo - 4096));   // a un sitio SALTEADO
                            fs.Read(buf, 0, buf.Length);
                        }
                        sw.Stop();
                        string u = UnidadDe(t.Path);
                        List<int> l;
                        if (!porUnidad.TryGetValue(u, out l)) { l = new List<int>(8); porUnidad[u] = l; }
                        l.Add((int)sw.ElapsedMilliseconds);
                        hechas++;
                    }
                    catch { }
                }
                if (hechas < 5) { perfilHecho = false; return; }   // no se pudo medir: sigue el respaldo de arriba
                // Cada unidad, por su MEDIANA. Y el veredicto general, el de la unidad donde cayeron mas
                // muestras: ahi es donde esta la mayoria de la musica.
                string mandaU = null; int mandaN = 0; bool mandaLento = true;
                StringBuilder det = new StringBuilder();
                lock (discoLock)
                {
                    foreach (KeyValuePair<string, List<int>> kv in porUnidad)
                    {
                        List<int> l = kv.Value;
                        if (l.Count < 3) continue;   // con una o dos medidas no se decide nada de esa unidad
                        l.Sort();
                        bool lentoU = l[l.Count / 2] > UmbralDiscoMs;
                        discoLento[kv.Key] = lentoU;
                        if (det.Length > 0) det.Append(", ");
                        det.Append(kv.Key).Append(' ').Append(lentoU ? "disco duro" : "SSD").Append(" (").Append(l[l.Count / 2]).Append(" ms)");
                        if (l.Count > mandaN) { mandaN = l.Count; mandaU = kv.Key; mandaLento = lentoU; }
                    }
                }
                if (mandaU == null) { perfilHecho = false; return; }
                medidoLento = mandaLento;
                diskProbeN = 16;   // ya esta decidido: el perfilado de oido no tiene nada que corregir
                AplicaPerfilDisco();
                perfilTexto = det.ToString();
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
            tierHd = (cores >= 4) && (ramMb == 0 || ramMb >= 3500) && (Environment.OSVersion.Version.Major >= 6);   // NIVEL HD: potente + Windows moderno
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
        // ---------- Archivo con el material de entrenamiento de la IA ----------
        // Formato binario compacto (millones de pares caben sin problema): cabecera "RIAD" + version + tamaños,
        // y luego cada par como 28 float de entrada + 12 float de objetivo. numpy lo lee de un tiron.
        private const int IaFramesPerSong = 192;   // cuadros COSECHADOS por cancion y pasada (repartidos por lo descomprimido)
        private const int IaPasadasMax = 8;        // pasadas del modo pasivo por cancion (cada una, cuadros distintos)
        // Traduccion entre el NIVEL apuntado en rocola_ia_hechas.txt y cuantas pasadas pasivas lleva la cancion:
        //   nivel 0 = ninguna;  1 = una;  2 = repasada A FONDO (ya dio todo lo suyo);  3..9 = 2..8 pasadas.
        // Los archivos viejos se siguen leyendo tal cual: en ellos solo hay niveles 1 y 2, con el mismo
        // significado que aqui.
        private static int IaPasadasDe(int nivel)
        { return (nivel <= 0) ? 0 : (nivel == 1) ? 1 : (nivel == 2) ? int.MaxValue : (nivel - 1); }
        private static int IaNivelDePasada(int pasadas)
        { return (pasadas <= 0) ? 1 : (pasadas + 2); }
        // CUANTO VALE cada nivel. No es su numero: el 2 -repasada A FONDO- es el TOPE, porque de esa
        // cancion ya no queda nada por sacar, aunque antes hubiera acumulado varias pasadas del pasivo
        // (que llevan numeros mas altos). Con esto el apunte sigue siendo de una sola direccion: una
        // cancion nunca baja de nivel, asi que no se puede volver a cosechar lo que ya se cosecho.
        private static int IaRango(int nivel) { return (nivel == 2) ? 100 : nivel; }
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
        private string IaDataFile() { return RutaDatos("rocola_ia_datos.bin"); }
        private string IaDoneFile() { return RutaDatos("rocola_ia_hechas.txt"); }
        private readonly object iaDoneLock = new object();
        private volatile bool iaDoneDirty = false;   // hay canciones nuevas cosechadas sin apuntar en disco
        private readonly List<string> iaPend = new List<string>();   // lineas apuntadas y aun sin escribir

        private static bool IaHeaderValida(BinaryReader br)
        {
            try
            {
                br.BaseStream.Position = 0;
                return br.BaseStream.Length >= 16
                    && br.ReadByte() == (byte)'R' && br.ReadByte() == (byte)'I'
                    && br.ReadByte() == (byte)'A' && br.ReadByte() == (byte)'D'
                    && br.ReadInt32() == 1 && br.ReadInt32() == 28 && br.ReadInt32() == 12;
            }
            catch { return false; }
        }
        private void IaCountPairs()
        {
            lock (iaFileLock)
            try
            {
                string f = IaDataFile();
                if (!File.Exists(f)) { iaPairsTotal = 0; return; }
                using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8, true))
                {
                    if (!IaHeaderValida(br)) { iaPairsTotal = 0; return; }
                    long completos = (fs.Length - 16) / 160;
                    long valido = 16 + completos * 160;
                    // Un apagon puede cortar el ultimo registro. Se quita SOLO esa cola incompleta; dejarla
                    // desplazaria para siempre todos los pares que se agreguen despues.
                    if (fs.Length != valido) { fs.SetLength(valido); fs.Flush(); try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { } }
                    iaPairsTotal = (completos > int.MaxValue) ? int.MaxValue : (int)completos;
                }
            }
            catch { iaPairsTotal = 0; }
        }
        // Añade al archivo los pares de UNA cancion (solo se llama con canciones que si conservan sus agudos).
        // Con el repaso a fondo en varios hilos, esto lo llaman VARIOS a la vez: dos anadidos solapados
        // dejarian el archivo de material entrelazado y por tanto inservible. El candado cuesta nada al lado
        // de descomprimir una cancion, que es lo que hace el hilo justo antes de llegar aqui.
        private readonly object iaFileLock = new object();
        private bool IaAppendPairs(List<float> pairs)
        {
            if (pairs == null || pairs.Count < 40 || (pairs.Count % 40) != 0) return false;
            lock (iaFileLock)
            try
            {
                string f = IaDataFile();
                using (FileStream fs = new FileStream(f, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8, true))
                using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8, true))
                {
                    if (fs.Length == 0)
                    { bw.Write((byte)'R'); bw.Write((byte)'I'); bw.Write((byte)'A'); bw.Write((byte)'D'); bw.Write(1); bw.Write(28); bw.Write(12); }
                    else if (!IaHeaderValida(br)) return false;   // no anexar encima de un archivo ajeno/corrupto
                    long completos = Math.Max(0, (fs.Length - 16) / 160);
                    long valido = 16 + completos * 160;
                    if (fs.Length != valido) fs.SetLength(valido);   // recupera una ultima escritura interrumpida
                    fs.Position = valido;
                    for (int i = 0; i < pairs.Count; i++) bw.Write(pairs[i]);
                    bw.Flush(); fs.Flush();
                    try { FlushFileBuffers(fs.SafeFileHandle.DangerousGetHandle()); } catch { }
                }
                iaPairsTotal += pairs.Count / 40;
                return true;
            }
            catch { return false; }
        }

        // ===== Puente con el motor de IA en vivo (carpeta compartida, mismo hash que daemon_ia.py) =====
        // Cache compartida con el motor. Por defecto: la MISMA que usa daemon_ia.py (ia\tiempo_real\cache),
        // hallada junto a la rocola -> rocola y motor coinciden SOLOS, sin configurar ni arrastrar nada.
        private string IaCache()
        {
            try
            {
                if (!string.IsNullOrEmpty(iaCacheDir))
                {
                    string d = iaCacheDir;
                    // Si la ruta configurada es RELATIVA se cuelga de la CARPETA DE DATOS, no de la del
                    // programa: hay UN solo sitio que decide donde viven los archivos de la rocola, y
                    // pruebas/carpeta.cs no deja que nadie se lo salte. Mientras nadie ponga ROCOLA_DATOS
                    // el resultado es EXACTAMENTE el mismo, porque la carpeta de datos es la del programa.
                    if (!Path.IsPathRooted(d)) d = RutaDatos(d);
                    return Path.GetFullPath(d);
                }
                string py, dm;
                if (IaFindEngine(out py, out dm)) return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dm), "cache"));
                return Path.GetFullPath(RutaDatos("ia_cache"));
            }
            catch { return RutaDatos("ia_cache"); }
        }

        private string IaPipelineFingerprint()
        {
            try
            {
                string f = Path.Combine(IaCache(), "modelo.fingerprint");
                if (!File.Exists(f)) return null;   // Apollo aun no esta listo: no publicar trabajos prematuros
                string s = File.ReadAllText(f).Trim();
                if (s.Length != 64) return null;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return null;
                }
                return s.ToLowerInvariant();
            }
            catch { return null; }
        }

        // Identidad compartida con daemon_ia.py. Incluye contenido aproximado (tamano+mtime), mezcla y
        // huella del modelo/pipeline: nunca se reutiliza audio de una fuente o Apollo anterior.
        private static string IaHashFor(string path, string mezclaKey, string pipeline)
        {
            try
            {
                string full = Path.GetFullPath(path).ToLowerInvariant();
                FileInfo fi = new FileInfo(full); if (!fi.Exists) return null;
                byte[] data = Encoding.UTF8.GetBytes(full + "|" + fi.Length.ToString(CultureInfo.InvariantCulture) + "|"
                    + fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)
                    + "|" + mezclaKey + "|" + pipeline);
                using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] h = sha.ComputeHash(data);
                    StringBuilder sb = new StringBuilder(64);
                    for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                    return sb.ToString().Substring(0, 16);
                }
            }
            catch { return null; }
        }

        // Ruta de la version IA ya lista para esta cancion; la ruta ORIGINAL (con yaBuena=true) si el
        // motor ya la analizo y era de calidad completa (marca .orig: no gasta disco ni necesita IA);
        // o null si aun no hay nada en cache.
        private string IaEnhancedPath(string path, out bool yaBuena)
        {
            yaBuena = false;
            try
            {
                string pipeline = IaPipelineFingerprint(); if (pipeline == null) return null;
                string h = IaHashFor(path, iaMezcla, pipeline); if (h == null) return null;
                string dir = Path.Combine(IaCache(), "listos");
                if (File.Exists(Path.Combine(dir, h + ".error"))) return null;             // backoff/terminal: original
                string p = Path.Combine(dir, h + ".mp3"); if (File.Exists(p)) return IaTouch(p);   // cache nueva (MP3 320: ~5x mas canciones por GB)
                p = Path.Combine(dir, h + ".wav"); if (File.Exists(p)) return IaTouch(p);          // cache vieja (WAV), sigue valiendo
                if (File.Exists(Path.Combine(dir, h + ".orig"))) { yaBuena = true; return path; }
                return null;
            }
            catch { return null; }
        }

        // Marca "recien usada" la version de cache que va a sonar. La limpieza del motor borra por fecha
        // de ULTIMA LECTURA (lo menos escuchado primero), pero Windows suele tener apagada esa fecha en
        // discos secundarios: sin este toque explicito, la limpieza degradaria a "borrar lo mas viejo
        // aunque suene a diario" y esas canciones se reprocesarian una y otra vez.
        private static string IaTouch(string p)
        {
            try { File.SetLastAccessTime(p, DateTime.Now); } catch { }
            return p;
        }

        // Le pide al motor que procese esta cancion (deja un .job atomico). Barato e idempotente.
        private void IaRequestJob(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                string pipeline = IaPipelineFingerprint(); if (pipeline == null) return;
                string full = Path.GetFullPath(path);
                string h = IaHashFor(full, iaMezcla, pipeline); if (h == null) return;
                string pend = Path.Combine(IaCache(), "pendientes");
                string dirL = Path.Combine(IaCache(), "listos");                          // ya atendida (mejorada o marcada de calidad completa): no re-pedir
                if (File.Exists(Path.Combine(dirL, h + ".mp3")) || File.Exists(Path.Combine(dirL, h + ".wav"))
                    || File.Exists(Path.Combine(dirL, h + ".orig")) || File.Exists(Path.Combine(dirL, h + ".error"))) return;
                Directory.CreateDirectory(pend);
                string job = Path.Combine(pend, h + ".job");
                if (File.Exists(job)) return;
                string json = "{\"src\":\"" + JsonEscape(full) + "\",\"mezcla\":\"" + JsonEscape(iaMezcla)
                    + "\",\"mezcla_key\":\"" + JsonEscape(iaMezcla) + "\",\"pipeline\":\"" + pipeline
                    + "\",\"cache_key\":\"" + h + "\"}";
                string tmp = job + "." + System.Diagnostics.Process.GetCurrentProcess().Id + "." + System.Threading.Thread.CurrentThread.ManagedThreadId + ".tmp";
                File.WriteAllText(tmp, json);
                try { File.Move(tmp, job); }   // aparicion atomica; si otro proceso gano, se conserva su job
                catch { try { File.Delete(tmp); } catch { } }
            }
            catch { }
        }

        private static string JsonEscape(string s)
        {
            if (s == null) return "";
            StringBuilder b = new StringBuilder(s.Length + 16);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') b.Append("\\\\"); else if (c == '"') b.Append("\\\"");
                else if (c == '\b') b.Append("\\b"); else if (c == '\f') b.Append("\\f");
                else if (c == '\n') b.Append("\\n"); else if (c == '\r') b.Append("\\r"); else if (c == '\t') b.Append("\\t");
                else if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4")); else b.Append(c);
            }
            return b.ToString();
        }

        // ¿Esta corriendo el motor de IA? Primero el testigo mas fuerte: el PROCESO que esta rocola
        // lanzo sigue vivo (aunque Apollo apenas este cargando y el latido aun no exista). Si no,
        // el "latido" (motor.vivo, que el motor refresca cada ~3s desde un hilo, SIEMPRE, incluso
        // a mitad de un trabajo largo). Sin esto, el vigia lanzaba motores de mas: N Apollos a la
        // vez = toda la RAM/VRAM del equipo (el candado motor.lock del motor es la ultima barrera).
        private bool IaMotorAlive()
        {
            try
            {
                System.Diagnostics.Process p = iaMotorProc;
                if (p != null && !p.HasExited) return true;
                string v = Path.Combine(IaCache(), "motor.vivo"); return File.Exists(v) && (DateTime.Now - File.GetLastWriteTime(v)).TotalSeconds < 15;
            }
            catch { return false; }
        }

        // Busca el motor (Python del venv + daemon) junto al programa o hasta 5 niveles arriba (como la carpeta 'musica').
        private static bool IaFindEngine(out string python, out string daemon)
        {
            python = null; daemon = null;
            try
            {
                string dir = Application.StartupPath;
                for (int i = 0; i < 6 && dir != null; i++)
                {
                    string py = Path.Combine(dir, "ia\\upscale\\venv\\Scripts\\pythonw.exe");
                    string dm = Path.Combine(dir, "ia\\tiempo_real\\daemon_ia.py");
                    if (File.Exists(py) && File.Exists(dm)) { python = py; daemon = dm; return true; }
                    DirectoryInfo p = Directory.GetParent(dir);
                    dir = (p != null) ? p.FullName : null;
                }
            }
            catch { }
            return false;
        }

        // Lanza el motor de IA OCULTO (sin ventana) si no esta corriendo ya. Asi la IA en vivo "solo funciona":
        // el operador no cuida ninguna terminal. Si el motor no esta instalado, no pasa nada (la IA no actua).
        private System.Diagnostics.Process iaMotorProc;
        private int iaMotorLaunchTick = 0;
        private void IaEnsureMotor()
        {
            try
            {
                if (IaMotorAlive()) return;                       // ya corre (este arranque u otro): no duplicar
                if (iaMotorLaunchTick != 0 && Environment.TickCount - iaMotorLaunchTick < 20000) return;   // lanzado hace poco: dale ~20s a cargar Apollo antes de reintentar (sin enjambre)
                string py, dm;
                if (!IaFindEngine(out py, out dm)) return;        // sin motor: la IA en vivo simplemente no hace nada
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = py;                                // pythonw.exe = sin ventana negra
                psi.Arguments = "\"" + dm + "\" --cache \"" + IaCache() + "\" --gpu " + iaGpu;
                psi.WorkingDirectory = Path.GetDirectoryName(dm);
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                iaMotorProc = System.Diagnostics.Process.Start(psi);
                iaMotorLaunchTick = Environment.TickCount;
            }
            catch { }
        }

        // GPUs CUDA que el motor detecto (cache\gpus.txt: una linea por GPU, el indice es la posicion).
        // Lo escribe el motor al arrancar; si aun no existe, el selector muestra solo "Automatica".
        private string[] IaGpuList()
        {
            try
            {
                string f = Path.Combine(IaCache(), "gpus.txt");
                if (!File.Exists(f)) return new string[0];
                List<string> lines = new List<string>();
                foreach (string l in File.ReadAllLines(f)) if (l.Trim().Length > 0) lines.Add(l.Trim());
                return lines.ToArray();
            }
            catch { return new string[0]; }
        }

        // Reinicia el motor de IA (para aplicar YA un cambio de GPU): mata el que lanzo esta rocola,
        // suelta el freno de 20s y lo relanza con los argumentos nuevos. Si el motor corriendo es de
        // otro (iniciar_motor.bat a mano), no se puede matar desde aqui: el cambio aplica cuando muera.
        private bool IaStopMotor()
        {
            System.Diagnostics.Process p = iaMotorProc;
            if (p == null) return false;
            bool parado = false;
            try
            {
                if (p.HasExited) { parado = true; return true; }
                // Process.Kill solo mata python.exe; ffmpeg quedaria huerfano. taskkill /T termina el arbol.
                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                    psi.FileName = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
                    psi.Arguments = "/PID " + p.Id.ToString(CultureInfo.InvariantCulture) + " /T /F";
                    psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    using (System.Diagnostics.Process killer = System.Diagnostics.Process.Start(psi))
                        if (killer != null) killer.WaitForExit(3000);
                }
                catch { }
                try
                {
                    if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); }
                    parado = p.HasExited;
                }
                catch { parado = false; }
                return parado;
            }
            catch
            {
                try { parado = p.HasExited; } catch { parado = false; }
                return parado;
            }
            finally
            {
                // No se pierde la unica referencia a un motor que no pudimos cerrar: el siguiente intento
                // todavia podra terminarlo y el vigia nunca lanzara un segundo Apollo encima.
                if (parado)
                {
                    if (object.ReferenceEquals(iaMotorProc, p)) iaMotorProc = null;
                    try { p.Dispose(); } catch { }
                }
            }
        }

        private void IaRestartMotor()
        {
            bool paro = IaStopMotor(); iaMotorLaunchTick = 0;
            if (paro) try { File.Delete(Path.Combine(IaCache(), "modelo.fingerprint")); } catch { }
            if (iaVivo) IaEnsureMotor();
        }

        // Al encender la IA: prepara lo que ya esta sonando y en cola (para que la IA los alcance).
        private void IaRequestQueueJobs()
        {
            try
            {
                if (nowPlaying != null && !nowPlaying.IsVideo && nowPlaying.Song != null) IaRequestJob(nowPlaying.Song.Path);
                for (int i = 0; i < queue.Count; i++) if (!queue[i].IsVideo && queue[i].Song != null) IaRequestJob(queue[i].Song.Path);
            }
            catch { }
        }

        private bool reintentandoWaveOut;

        // ==========  IA PROPIA: RED DE ENVOLVENTE ESPECTRAL (NIVEL 3, rama HD)  ==========
        // Un MLP chico (28 -> 48 -> 48 -> 12, tanh) ENTRENADO POR NOSOTROS (ia/entrenar_ia.py) que, viendo la
        // FORMA del espectro que sobrevivio al codec (24 bandas log + sonoridad + posicion del corte +
        // planitud), PREDICE la envolvente correcta de la banda perdida (12 sub-bandas sobre el corte,
        // relativas a la costura). El reconstructor espectral la usa EN VEZ de la caida fija de -9 dB/octava:
        // platillos recuperan su brillo real, pasajes oscuros se quedan oscuros. Pesos en rocola_ia.bin junto
        // al exe (formato RIA1); sin archivo, el reconstructor sigue con su curva fija (cero cambio). La
        // inferencia son ~4300 multiplicaciones por ventana (~23ms): costo despreciable hasta en un Atom.
        // Los dos modelos. El del DSEE re-crea la envolvente de los agudos (28 datos de la ventana -> 12 sub-bandas).
        // El del EQ mira la parte del espectro que los codecs NO tocan (el cuerpo, 158 Hz - 5.6 kHz) y dice cuanto
        // grave y cuanto aire tendria una cancion BIEN GRABADA con ese mismo cuerpo: 5 entradas -> 3 salidas.
        private static readonly RedChica NeuralEnv = new RedChica(28, 12);
        private static readonly RedChica EqNet = new RedChica(EqNetIn, EqNetOut);
        private const int EqNetIn = 5, EqNetOut = 3;
        private static readonly int[] EqNetSalida = { 0, 6, 7 };   // que bandas predice: 50 Hz, 10 kHz y 15.8 kHz

        // Red neuronal chica cargada de un archivo RIA1 (capas de pesos+sesgos, ocultas con tanh y la de salida
        // lineal). La usan DOS cosas distintas, cada una con su modelo y su contrato de entradas/salidas: el DSEE
        // para la envolvente de los agudos que re-crea, y el EQ automatico para su objetivo por cancion.
        private sealed class RedChica
        {
            private readonly object modeloLock = new object();
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
                    byte[] d;
                    // Ocho capas de 512x512 caben en menos de 9 MiB. No cargar
                    // archivos arbitrariamente grandes antes de validar el contrato.
                    using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (file.Length < 12 || file.Length > 9 * 1024 * 1024) return;
                        using (BinaryReader input = new BinaryReader(file))
                        {
                            d = input.ReadBytes((int)file.Length);
                            if (d.Length != file.Length) return;
                        }
                    }
                    if (d.Length < 12 || d[0] != (byte)'R' || d[1] != (byte)'I' || d[2] != (byte)'A' || d[3] != (byte)'1') return;
                    int p = 4;
                    int nl = BitConverter.ToInt32(d, p); p += 4;
                    if (nl < 1 || nl > 8) return;
                    float[][] w = new float[nl][], b = new float[nl][];
                    int[] ro = new int[nl], ci = new int[nl]; int maxDim = 0;
                    for (int L = 0; L < nl; L++)
                    {
                        if (d.Length - p < 8) return;
                        int r = BitConverter.ToInt32(d, p); p += 4;
                        int c = BitConverter.ToInt32(d, p); p += 4;
                        if (r < 1 || r > 512 || c < 1 || c > 512) return;
                        if (d.Length - p < (r * c + r) * 4) return;
                        w[L] = new float[r * c]; b[L] = new float[r];
                        for (int i = 0; i < r * c; i++)
                        { float v = BitConverter.ToSingle(d, p); p += 4; if (float.IsNaN(v) || float.IsInfinity(v)) return; w[L][i] = v; }
                        for (int i = 0; i < r; i++)
                        { float v = BitConverter.ToSingle(d, p); p += 4; if (float.IsNaN(v) || float.IsInfinity(v)) return; b[L][i] = v; }
                        ro[L] = r; ci[L] = c; if (r > maxDim) maxDim = r; if (c > maxDim) maxDim = c;
                    }
                    if (p != d.Length || ci[0] != nIn || ro[nl - 1] != nOut) return;   // contrato exacto, sin bytes sobrantes
                    for (int L = 1; L < nl; L++) if (ci[L] != ro[L - 1]) return;
                    float[] work1 = new float[maxDim], work2 = new float[maxDim];
                    lock (modeloLock)
                    {
                        buf1 = work1; buf2 = work2;
                        W = w; B = b; rOut = ro; cIn = ci;
                        Loaded = true;
                    }
                }
                catch { }   // una recarga invalida no destruye el ultimo modelo valido
            }

            // y = red(x). Capas ocultas con tanh; la de salida lineal. Devuelve false si no hay modelo.
            public bool Predict(float[] x, float[] y)
            {
                lock (modeloLock)
                {
                    float[][] w = W, b = B;
                    if (!Loaded || w == null || x == null || y == null || x.Length < nIn || y.Length < nOut) return false;
                    for (int i = 0; i < nIn; i++) if (float.IsNaN(x[i]) || float.IsInfinity(x[i])) return false;
                    float[] cur = x, nxt = buf1;
                    int curN = cIn[0];
                    for (int L = 0; L < w.Length; L++)
                    {
                        int r = rOut[L]; float[] wl = w[L], bl = b[L];
                        float[] dst = nxt;   // no publicar una salida parcial si falla otra neurona
                        for (int i = 0; i < r; i++)
                        {
                            float s = bl[i]; int off = i * curN;
                            for (int j = 0; j < curN; j++) s += wl[off + j] * cur[j];
                            if (float.IsNaN(s) || float.IsInfinity(s)) return false;
                            dst[i] = (L == w.Length - 1) ? s : (float)Math.Tanh(s);
                        }
                        cur = dst; curN = r;
                        nxt = (cur == buf1) ? buf2 : buf1;   // alterna buffers (la entrada x no se toca)
                    }
                    for (int i = 0; i < nOut; i++) y[i] = cur[i];
                    return true;
                }
            }
        }
    }
}

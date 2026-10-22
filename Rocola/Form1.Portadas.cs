// LAS PORTADAS: el despachador, las miniaturas y el precalentado
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
        private CubeBox miniCube;
        private readonly Dictionary<string, Image> coverCache = new Dictionary<string, Image>();
        private readonly LinkedList<string> coverOrder = new LinkedList<string>();   // menos usada al principio
        private readonly Dictionary<string, LinkedListNode<string>> coverNodes = new Dictionary<string, LinkedListNode<string>>();
        private readonly Dictionary<string, CoverJob> coverLoading = new Dictionary<string, CoverJob>();
        private readonly Dictionary<string, int> coverRetryUntil = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Image> coverTrash = new List<Image>();   // expulsadas, a liberar de forma segura (en el timer)
        private volatile int coverGen = 0;     // solo invalida contenido (F5), no lecturas utiles al cambiar pagina
        private System.Windows.Forms.Timer coverSettleTimer;   // espera 120 ms antes de precargar vecinas
        // CARRILES: tope global segun CPU (uno en 2C/2T); cada unidad HDD admite un lector,
        // SSD admite paralelismo dentro del tope. El semaforo se comparte con el generador
        // de miniaturas, que cede a las peticiones visibles. No se promete latencia fija de disco.
        private static readonly System.Threading.Semaphore coverSem = new System.Threading.Semaphore(2, 8);
        private static volatile int coverCarriles = 1;   // bombas de caratula a la vez (1 = disco duro, como arranca)
        private static int coverAbiertos = 2;            // turnos de disco ya concedidos (el semaforo solo puede CRECER)

        private volatile int warmGen = 0;          // generacion del precalentador (se reinicia al recargar musica)
        private readonly object coverLock = new object();
#if ROCOLA_GPU
        private readonly Dictionary<string, ColoresDisco> coloresPreparados = new Dictionary<string, ColoresDisco>(StringComparer.OrdinalIgnoreCase);
#endif
        private const int ThumbPx = 256;         // resolucion de miniaturas (256 sobra a 1024x768; 384 gastaba 2x memoria sin verse mejor en la rocola)
        // PRECALENTAR LA SIGUIENTE CANCION.
        //
        // Al arrancar una cancion, NAudio se recorre el MP3 COMPLETO para armar su indice de trozos (lo
        // necesita para poder moverse por la cancion y para saber cuanto dura), y eso pasa en el hilo de la
        // pantalla. En un disco duro mecanico, un archivo de 10 MB son unas decimas de segundo con la rocola
        // clavada, justo en el momento del cambio de cancion.
        //
        // Aqui se lee ese archivo ANTES, en segundo plano y sin ninguna prisa, para que cuando le toque ya
        // este en la memoria de Windows y abrirlo sea instantaneo. No se guarda nada: solo se pasa por
        // encima para que el sistema lo cachee. Va a 64 KB por vez con una pausa entre trozos (una cancion
        // de 10 MB se calienta en algo mas de un segundo, y quedan minutos por delante), cede el disco si
        // hay caratulas en camino o un video sonando, y se cancela sola si la cola cambia.
        private volatile int warmSongGen = 0;
        private string warmSongPath;
        // Ceder prelectura antes del siguiente bloque si el usuario navega. No afecta al audio actual.
        private bool YieldSongWarm(string route)
        { return YieldCoverWork(UnidadDe(route), CargaTrabajo.Procesadores <= 2); }
        private bool YieldCoverWork(string unit, bool smallCpu)
        {
            lock (coverLock)
            {
                if (smallCpu) return coverLoading.Count != 0;
                int active;
                if (coverActiveUnits.TryGetValue(unit, out active) && active > 0) return true;
                for (LinkedListNode<CoverJob> node = coverQHi.First; node != null; node = node.Next)
                    if (string.Equals(UnidadDe(node.Value.Key), unit, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }
        private bool EsperaPortadas(string unit, bool smallCpu, Func<bool> cancel)
        {
            lock (coverLock)
            {
                while (YieldCoverWork(unit, smallCpu))
                {
                    if (cancel != null && cancel()) return false;
                    System.Threading.Monitor.Wait(coverLock, 250);
                }
                return cancel == null || !cancel();
            }
        }
        private void PrecalientaSiguiente()
        {
            string ruta = null; Track cancion = null;
            for (int i = 0; i < queue.Count; i++)
            {
                PlayItem it = queue[i];
                if (it != null && !it.IsVideo && it.Song != null && it.Song.Path != null) { ruta = it.Song.Path; cancion = it.Song; break; }
            }
            if (ruta == null)
            {
                warmSongPath = null; unchecked { warmSongGen++; }
                SueltaLookahead();   // no hay siguiente: si habia un lector esperando, ya no vale
                return;
            }
            if (string.Equals(ruta, warmSongPath, StringComparison.OrdinalIgnoreCase)) return;   // ya calentada o calentandose
            SueltaLookahead();       // la siguiente es OTRA: lo que hubiera abierto era para la de antes
            warmSongPath = ruta;
            int mio = ++warmSongGen;
            // ¿YA ESTA MEDIDA? Se pregunta AQUI, que es el hilo de la pantalla y por tanto el dueño de las
            // tablas: no llevan candado -a proposito, para no pagarlo en cada consulta-, y el pre-analisis
            // escribe en ellas desde otro lado. Preguntarlo desde el hilo del calentador podria reventar
            // justo en el momento en que el otro esta metiendo una fila.
            bool yaMedida = durCache.ContainsKey(ruta);
            // Y SU ETIQUETA, si aun no se leyo: se copia AQUI la cancion tal como esta y el hilo lee sobre la copia. La
            // cancion no se toca hasta que la pantalla la empiece (Track.EnsureTags).
            Track etiquetaAntes = (cancion != null && !cancion.TagsLoaded) ? cancion.CopiaEtiqueta() : null;
            System.Threading.Thread t = new System.Threading.Thread(delegate ()
            {
                try
                {
                    if (!SiguienteUrgente()) System.Threading.Thread.Sleep(1500);   // que la cancion que acaba de empezar tenga el disco (cerca del final no: va primero la siguiente)
                    if (mio != warmSongGen) return;
                    while (true)   // caratulas del primer plano o un video: el disco es para ellos
                    {
                        int busy; lock (coverLock) { busy = coverLoading.Count + coverQHi.Count + coverQLo.Count; }
                        if ((busy == 0 || SiguienteUrgente()) && !videoOn) break;   // a 45 s del final la siguiente pasa antes que las caratulas
                        System.Threading.Thread.Sleep(300);
                        if (mio != warmSongGen) return;
                    }
                    // TROZOS segun el disco. Un disco duro se lee a trozos chicos y con pausa entre trozo y
                    // trozo, para no dejar al cabezal ocupado si de pronto hace falta para otra cosa. Un SSD
                    // rinde al contrario: cuanto mas grande el trozo, menos veces se le molesta y antes acaba,
                    // asi que se le piden 256 KB de una y la cancion queda en memoria en un pestaneo.
                    // Y en los dos casos se avisa a Windows de que esto se va a leer DE PRINCIPIO A FIN
                    // (SequentialScan): con eso adelanta la lectura por su cuenta en vez de ir trozo a trozo.
                    bool lentoAqui = DiscoLento(ruta);
                    int trozo = lentoAqui ? (64 * 1024) : (256 * 1024);
                    // Y SE GUARDA (CancionEnMemoria.cs): si es un MP3 que cabe, se lee a su sitio en memoria en vez de
                    // tirarlo, con los mismos trozos y las mismas pausas. Al abrirla ya no se vuelve a leer, y mientras
                    // suene no se toca el disco. La firma se toma ANTES de leer: si el archivo cambia, no se usa.
                    string firma = AudioFileSignature(ruta);
                    using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, trozo, FileOptions.SequentialScan))
                    {
                        byte[] entera = (firma != null) ? ReservaEnMemoria(ruta, fs.Length) : null;
                        byte[] buf = (entera == null) ? new byte[trozo] : null;
                        int hecho = 0;
                        while (true)
                        {
                            if (mio != warmSongGen) return;
                            if (!SiguienteUrgente() && !EsperaPortadas(UnidadDe(ruta), CargaTrabajo.Procesadores <= 2,
                                delegate { return mio != warmSongGen; })) return;
                            int n = (entera != null) ? fs.Read(entera, hecho, Math.Min(trozo, entera.Length - hecho)) : fs.Read(buf, 0, buf.Length);
                            if (n <= 0) break;
                            hecho += n;
                            System.Threading.Thread.Sleep(lentoAqui ? 8 : 1);
                        }
                        if (entera != null && hecho == entera.Length && mio == warmSongGen) DejaEnMemoria(ruta, firma, entera);
                        // Y SU ETIQUETA, de esos mismos bytes (o del archivo, que acaba de pasar entero por la memoria de
                        // Windows, si no cabia): al empezar la cancion la pantalla ya no lo abre para leerla.
                        if (etiquetaAntes != null && firma != null && mio == warmSongGen)
                            Track.PonEtiquetaLeida(cancion, etiquetaAntes,
                                Track.LeeEtiquetaAparte(etiquetaAntes, (entera != null && hecho == entera.Length) ? entera : null), firma);
                    }

                    // Y YA QUE ESTA LEIDA, SE ABRE SU LECTOR.
                    //
                    // Abrir un MP3 no es gratis: NAudio recorre el archivo entero armando el indice de sus
                    // cuadros, y ademas monta el descompresor del sistema. Desde el 17/09 eso ya no para la
                    // pantalla, pero sigue estando EN EL CAMINO: la cancion no empieza hasta que su lector
                    // esta listo. Hacerlo aqui lo saca de ese camino.
                    //
                    // Va DESPUES de leer los bytes, no antes: con el archivo ya en la memoria del sistema,
                    // abrir no vuelve a tocar el disco, que es de la cancion que esta sonando.
                    try
                    {
                        if (IsHandleCreated)
                            BeginInvoke((MethodInvoker)delegate { if (mio == warmSongGen && !preScanStop) PideLookahead(ruta); });
                    }
                    catch { }

                    // Y YA QUE ESTA LEIDA, SE MIDE.
                    //
                    // Medir una cancion cuesta dos cosas: leerla del disco y descomprimirla. La lectura
                    // acaba de pagarse aqui arriba, y hasta ahora se tiraba entera. Descomprimir lo que ya
                    // esta en la memoria del sistema cuesta 550 ms por minuto de cancion -medido-, unos 2.2
                    // segundos en una de cuatro minutos, y este hilo va en la prioridad mas baja que hay y
                    // tiene por delante los minutos que la cancion pase en la cola.
                    //
                    // A cambio, cuando le toque sonar ya viene nivelada de volumen A LA PRIMERA -antes solo
                    // a la segunda, porque la sonoridad se aprendia oyendola-, con su duracion apuntada y
                    // con el turno pudiendo decir los minutos que faltan.
                    if (yaMedida || mio != warmSongGen) return;
                    while (true)   // otra vez: si entro un video o hay caratulas en camino, primero ellos
                    {
                        int ocupado; lock (coverLock) { ocupado = coverLoading.Count + coverQHi.Count + coverQLo.Count; }
                        if (ocupado == 0 && !videoOn) break;
                        System.Threading.Thread.Sleep(300);
                        if (mio != warmSongGen) return;
                    }
                    // El turno, igual que en el repaso: si otro la esta midiendo -o ya se midio- no se
                    // vuelve a hacer el trabajo. Aqui se pide DESPUES de esperar a caratulas y video, no
                    // antes: un turno pedido y guardado mientras se espera taparia el archivo sin estar
                    // midiendolo.
                    if (!ReclamaMedidaEsperando(ruta, false, delegate { return mio != warmSongGen || preScanStop; })) return;
                    float wrms = 0f, wdur = 0f; float[] weq = null;
                    bool midio, queued = false;
                    try
                    {
                        Func<bool> cancela = delegate { return mio != warmSongGen || preScanStop; };
                        using (TurnoAnalisis turno = PideTurnoAnalisis(ruta, cancela))
                        {
                            if (turno == null) return;
                            midio = MeasureFileResponsive(ruta, false, cancela, out wrms, out wdur, out weq);
                        }
                        SonoridadIntegrada wentera; bool whay = TomaIntegrada(out wentera);   // solo si la leyo entera (canciones cortas)
                        FuenteMedida wfuente = TomaFuenteMedida();
                        if (!midio) return;
                        if (mio != warmSongGen) return;
                        // Las tablas son del hilo de la pantalla: se le encarga a el que las escriba.
                        try
                        {
                            if (IsHandleCreated)
                            {
                                BeginInvoke((MethodInvoker)delegate {
                                    try { if (mio == warmSongGen && !preScanStop) { PublicaMedidaDeFuente(ruta, wrms, wdur, weq, wfuente); if (whay) PublicaIntegradaDeFuente(ruta, wentera, wfuente); } }
                                    finally { SueltaMedida(ruta); }
                                });
                                queued = true;
                            }
                        }
                        catch { }
                    }
                    finally { if (!queued) SueltaMedida(ruta); }
                }
                catch { }
            });
            t.IsBackground = true;
            t.Priority = System.Threading.ThreadPriority.Lowest;
            t.Start();
        }

        // Generacion y contador cambian juntos: un trabajador retirado no puede pisar al nuevo.
        private int AdvanceThumbWarm()
        {
            lock (coverLock)
            {
                unchecked { warmGen++; }
                minisPendientes = 0;
                return warmGen;
            }
        }

        private bool PublishThumbRemaining(int generation, int remaining)
        {
            lock (coverLock)
            {
                if (generation != warmGen) return false;
                if (remaining < 0) { if (minisPendientes > 0) minisPendientes--; }
                else minisPendientes = remaining;
                return true;
            }
        }

        // En arranque desde cache: portadas resueltas SIN tocar el disco.
        // La portada de una carpeta: siempre sale del mapa que dejo el recorrido del disco (el escaneo fresco lo
        // arma sobre la marcha y el arranque por cache lo trae del archivo), asi que aqui ya no se toca el disco.
        private string FindCoverCached(string folder)
        {
            string cp;
            if (cachedCovers != null && cachedCovers.TryGetValue(folder, out cp)) return cp;
            return null;
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


        private static string AlbumFolderCached(string filePath, List<string> roots, string[] folderMemo)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir)) return "";
            if (string.Equals(dir, folderMemo[0], StringComparison.Ordinal)) return folderMemo[1];
            string folder = AlbumFolderFromDirectory(dir, roots);
            // El escaner entrega archivos consecutivos por carpeta: basta la ultima.
            // Dos referencias por reconstruccion, sin tabla hash ni retencion de todas las rutas.
            folderMemo[0] = dir; folderMemo[1] = folder;
            return folder;
        }

        private static string AlbumFolderFromDirectory(string dir, List<string> roots)
        {
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

        private static readonly string[] CoverNames = { "portada.jpg", "portada.jpeg", "portada.png", "cover.jpg", "cover.png", "folder.jpg", "front.jpg" };

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
#if ROCOLA_GPU
            // Incluye el salto entre letras al acabar la pagina. Durante una rafaga se
            // espera a este asentamiento; la misma cola baja evita trabajo duplicado.
            if (gpuEnabled && albumView != null && albumView.Ahead != null)
                for (int i = 0; i < albumView.Ahead.Count; i++) RequestCover(albumView.Ahead[i], true, false);
#endif
            // Cuantas paginas de colchon a cada lado: segun el perfil de carga, el disco y si las miniaturas van en JPEG.
            int colchon = PaginasDeColchon(
#if ROCOLA_GPU
                gpuEnabled, caratulasCarga,
#else
                false, 0,
#endif
                diskSlow, JpegActivo, CargaTrabajo.Procesadores);
            for (int d = 1; d <= colchon; d++) { PrefetchPage(all, page + d); PrefetchPage(all, page - d); }
        }

        // PAGINAS DE COLCHON (peticion del 13-10-2026: que los perfiles aprovechen el JPEG). Una miniatura JPEG pesa unas
        // seis veces menos que la PNG y se lee en un tercio del tiempo, sin pasar por la memoria de objetos grandes:
        //  - Fluidez: una pagina a cada lado, tenga el disco que tenga. Es el de los equipos mas debiles: menos trabajo de
        //    fondo; el JPEG ya le abarata cada lectura sin preleer mas.
        //  - Automatico y Equilibrado: dos con SSD; con disco duro, dos si van en JPEG (lecturas cortas) y una en PNG.
        //  - Carga rapida: igual, y tres con SSD y JPEG si sobran hilos (mas de dos).
        // Solo cambia CUANTO se pide por adelantado, con la fila baja de siempre: los lectores siguen con su tope (uno
        // por disco duro y uno en 2 hilos) y la calidad final es la misma. Sin GPU (congelado), lo de antes: 2 SSD, 1 HDD.
        internal static int PaginasDeColchon(bool gpu, int perfil, bool lento, bool jpeg, int procesadores)
        {
            if (!gpu) return lento ? 1 : 2;
            if (perfil == 1) return 1;
            if (lento) return jpeg ? 2 : 1;
            return (perfil == 3 && jpeg && procesadores > 2) ? 3 : 2;
        }

        private readonly List<string> coverArrivals = new List<string>();
        private bool coverArrivalsPosted;

        // Cada clave se publico una sola vez en esta generacion. Agrupar las llegadas
        // mientras espera el mensaje de UI, conservando su orden y sin demoras artificiales.
        private bool RegisterCoverArrival(string key, int generation)
        {
            lock (coverLock)
            {
                if (generation != coverGen) return false;
                coverArrivals.Add(key);
                if (coverArrivalsPosted) return false;
                coverArrivalsPosted = true;
                return true;
            }
        }

        private string[] TakeCoverArrivals()
        {
            lock (coverLock)
            {
                string[] keys = coverArrivals.ToArray();
                coverArrivals.Clear(); coverArrivalsPosted = false;
                return keys;
            }
        }

        private void FlushCoverArrivals()
        {
            string[] keys = TakeCoverArrivals();
            for (int i = 0; i < keys.Length; i++) ApplyArrivedCover(keys[i]);
            if (keys.Length == 0) return;
#if ROCOLA_GPU
            if (PortadasVisiblesEnCola(keys, colaDoce ? 12 : 3)) CambioElPanelDeReposo();
#else
            CambioElPanelDeReposo();
#endif
            try
            {
                if (searchOpen && searchOverlay != null) searchOverlay.Invalidate();
                // Estas dos vistas dependen del conjunto: refrescarlas una vez por tanda.
                if (pastCola != null && pastCola.ConPortadas) { LlenaPortaditas(); pastCola.Invalidate(); }
            }
            catch { }
        }

        // ----- DESPACHADOR DE CARATULAS CON PRIORIDAD -----
        // Antes todas las peticiones (pagina actual, prelectura de vecinas, lo que fuera) se formaban
        // revueltas en el mismo turno de disco: a veces las caratulas que ESTAS viendo esperaban detras
        // de la prelectura. Ahora hay dos filas: ALTA (lo visible) y BAJA (prelectura); una sola bomba
        // atiende el disco y SIEMPRE saca primero la fila alta.
        private class CoverJob { public string Key, Unit; public int Gen; public LinkedListNode<CoverJob> Node; }
        private readonly Dictionary<string, int> coverActiveUnits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<CoverJob> coverQHi = new LinkedList<CoverJob>();
        private readonly LinkedList<CoverJob> coverQLo = new LinkedList<CoverJob>();
        private int coverPumpCount = 0;   // hasta coverCarriles, nunca un hilo por solicitud

        // Llamar bajo coverLock. Si la prelectura ahora es visible, pasa a la fila alta
        // sin duplicar la decodificacion ni desplazar el resto de la cola.
        private bool CoverYaSolicitada(string key, bool highPriority)
        {
            CoverJob j;
            if (!coverLoading.TryGetValue(key, out j)) return false;
            if (highPriority && j.Node != null && j.Node.List == coverQLo)
            {
                coverQLo.Remove(j.Node);
                coverQHi.AddLast(j.Node);
            }
            return true;
        }

        // Cancelar solo lo que no ha empezado. Las lecturas en vuelo siguen siendo validas:
        // terminar y cachearlas evita repetir disco/decodificacion al volver o cambiar de pagina.
        private void PrepareCoverPage()
        {
            lock (coverLock)
            {
#if ROCOLA_GPU
                gpuPreviewPage++;
#endif
                CancelQueuedCovers(coverQHi);
                CancelQueuedCovers(coverQLo);
                System.Threading.Monitor.PulseAll(coverLock);
            }
        }

        // Llamar bajo coverLock: retirar cola y registro juntos, sin dejar solicitudes fantasma.
        private void CancelQueuedCovers(LinkedList<CoverJob> jobs)
        {
            while (jobs.Count > 0)
            {
                CoverJob job = jobs.First.Value;
                jobs.RemoveFirst(); job.Node = null;
                CoverJob registered;
                if (coverLoading.TryGetValue(job.Key, out registered) && object.ReferenceEquals(job, registered))
                    coverLoading.Remove(job.Key);
            }
        }

        private void InvalidateCoverMemory()
        {
            colorPortada.Clear();   // F5 invalida tambien los tonos de una imagen reemplazada
            lock (coverLock)
            {
                coverGen++;
#if ROCOLA_GPU
                coloresPreparados.Clear();
                foreach (Bitmap b in gpuPreviewCache.Values) coverTrash.Add(b);
                gpuPreviewCache.Clear(); gpuPreviewOrder.Clear(); gpuPreviewPage++; gpuPreviewLoaded = -1;
#endif
                coverArrivals.Clear();   // el mensaje pendiente puede atender llegadas de la nueva generacion
                coverQHi.Clear(); coverQLo.Clear(); coverLoading.Clear(); coverRetryUntil.Clear();
                System.Threading.Monitor.PulseAll(coverLock);
                foreach (KeyValuePair<string, Image> kv in coverCache) if (kv.Value != null) coverTrash.Add(kv.Value);
                coverCache.Clear(); coverOrder.Clear(); coverNodes.Clear();
            }
        }

        // Una lectura fallida (archivo copiandose, disco que desperto tarde) no se guarda como null para
        // siempre. Solo se frena unos segundos para que cada repintado no martillee el mismo archivo.
        // Llamar bajo coverLock.
        private bool CoverRetryPendiente(string key)
        {
            int hasta;
            if (!coverRetryUntil.TryGetValue(key, out hasta)) return false;
            if (unchecked(Environment.TickCount - hasta) < 0) return true;
            coverRetryUntil.Remove(key);
            return false;
        }

        // Bajo coverLock. Saltar unidades ocupadas permite atender otra unidad sin abrir mas hilos.
        private CoverJob TakeCoverJob(LinkedList<CoverJob> jobs)
        {
#if ROCOLA_GPU
            if (gpuEnabled && ActiveCoverReaders() >= AdaptiveCoverReaders()) return null;
#endif
            for (LinkedListNode<CoverJob> node = jobs.First; node != null; node = node.Next)
            {
                CoverJob job = node.Value;
                string unit = UnidadDe(job.Key);
                int active; coverActiveUnits.TryGetValue(unit, out active);
                int limit = DiscoLento(job.Key) ? 1 :
#if ROCOLA_GPU
                    AdaptiveCoverReaders();
#else
                    coverCarriles;
#endif
                if (active >= limit) continue;
                jobs.Remove(node); job.Node = null; job.Unit = unit;
                coverActiveUnits[unit] = active + 1;
                return job;
            }
            return null;
        }

        private void ReleaseCoverUnit(CoverJob job)
        {
            lock (coverLock)
            {
                int active;
                if (job.Unit != null && coverActiveUnits.TryGetValue(job.Unit, out active))
                {
                    if (active <= 1) coverActiveUnits.Remove(job.Unit);
                    else coverActiveUnits[job.Unit] = active - 1;
                }
                System.Threading.Monitor.PulseAll(coverLock);
            }
        }

        // Prioridad visible y limites por unidad. F5 no borra unidades ocupadas por lecturas en vuelo.
        private void CoverPump()
        {
#if ROCOLA_GPU
            int poolGeneration = coverGen;
#endif
            // Todo el hilo va dentro de un try (19-10-2026): una excepcion que se escapara de aqui (fuera de los catch de dentro) mataba el hilo sin
            // avisar -en CLR 2 una excepcion sin atrapar en un hilo cierra la rocola- y dejaba subido el contador de lectores (coverPumpCount), asi que
            // no se armaba otro. Ahora se apunta, se baja el contador y la portada en curso queda para reintentar.
            CoverJob enCurso = null; bool salio = false;
            try
            {
            while (true)
            {
#if ROCOLA_GPU
                if (gpuEnabled && coverGovernor.ReaderPause > 0)
                    System.Threading.Thread.Sleep(coverGovernor.ReaderPause);
#endif
                CoverJob j = null; enCurso = null;
                lock (coverLock)
                {
                    j = TakeCoverJob(coverQHi) ?? TakeCoverJob(coverQLo);
#if ROCOLA_GPU
                    if (gpuEnabled && j == null && !coverWorkersStop && !startupClosing && poolGeneration == coverGen
                        && coverPumpCount <= AdaptiveCoverReaders())
                    {
                        long idleUntil = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency * 2;
                        while (j == null && !coverWorkersStop && !startupClosing && poolGeneration == coverGen
                            && coverPumpCount <= AdaptiveCoverReaders())
                        {
                            int wait = (int)Math.Min(2000, Math.Max(0, (idleUntil - System.Diagnostics.Stopwatch.GetTimestamp()) * 1000 / System.Diagnostics.Stopwatch.Frequency));
                            if (wait <= 0) break;
                            System.Threading.Monitor.Wait(coverLock, wait);
                            if (!coverWorkersStop && !startupClosing && poolGeneration == coverGen)
                                j = TakeCoverJob(coverQHi) ?? TakeCoverJob(coverQLo);
                        }
                    }
#endif
                    if (j == null) { coverPumpCount--; salio = true; return; }
                }
                enCurso = j;
                string key = j.Key;
                bool entered = false;
                Bitmap miniaturaParaGuardar = null;
#if ROCOLA_GPU
                Bitmap preview = null; byte[] previewBytes = null;
#endif
                try
                {
                    if (j.Gen != coverGen) continue;
                    coverSem.WaitOne(); entered = true; // compartido con el generador de miniaturas
                    if (j.Gen != coverGen) continue;
                    Image thumb = null;
#if ROCOLA_GPU
                    try { LoadGpuPreviewBatch(j); } catch { } // preview no puede impedir la caratula final
#endif
                    string identidadAntes = null;
                    try
                    {
                        identidadAntes = ThumbCachePath(key);
#if ROCOLA_GPU
                        if (gpuEnabled) thumb = DecodeCoverForPublication(key, identidadAntes, out miniaturaParaGuardar);
                        else
#endif
                        thumb = DecodeCoverWithIdentity(key, identidadAntes);
                    }
                    catch { }
                    // Si la portada fue reemplazada mientras se decodificaba, el bitmap pertenece a la
                    // version anterior. No se publica en la cache de memoria con la misma ruta.
                    try
                    {
                        string identidadDespues = ThumbCachePath(key);
                        if (!string.Equals(identidadAntes, identidadDespues, StringComparison.OrdinalIgnoreCase))
                        {
                            if (thumb != null) thumb.Dispose();
                            thumb = null;
                        }
                    }
                    catch { if (thumb != null) { try { thumb.Dispose(); } catch { } thumb = null; } }
#if ROCOLA_GPU
                    if (gpuEnabled && caratulasLod && thumb != null)
                        try { preview = GpuPreviewStore.Small(thumb); previewBytes = GpuPreviewStore.ToBytes(preview); } catch { }
                    ColoresDisco tonos = new ColoresDisco();
                    if (gpuEnabled && thumb is Bitmap)
                    {
                        try
                        {
                            tonos.Prom = ColoresDePortada((Bitmap)thumb, out tonos.Uno, out tonos.Dos);
                            tonos.Paleta = ExtractCoverPalette((Bitmap)thumb); tonos.Fuente = thumb;
                        }
                        catch { tonos.Fuente = null; } // un fallo de GDI no mata el trabajador ni pierde la portada
                    }
#endif
                    lock (coverLock)
                    {
                        // Validar y publicar en el MISMO candado que invalida. Un trabajo viejo
                        // nunca puede borrar el registro ni publicar sobre la generacion nueva.
                        if (j.Gen != coverGen)
                        {
                            if (thumb != null) { try { thumb.Dispose(); } catch { } }
                            continue;
                        }
                        if (thumb != null) { CachePut(key, thumb); coverRetryUntil.Remove(key); }
                        else coverRetryUntil[key] = unchecked(Environment.TickCount + 30000);
#if ROCOLA_GPU
                        if (tonos.Fuente != null) coloresPreparados[key] = tonos;
                        if (preview != null) { PutGpuPreview(key, preview); preview = null; }
#endif
                        coverLoading.Remove(key);
                    }
                    if (thumb != null)
                        try
                        {
                            if (this.IsHandleCreated && RegisterCoverArrival(key, j.Gen))
                            {
                                try { this.BeginInvoke((MethodInvoker)FlushCoverArrivals); }
                                catch
                                {
                                    lock (coverLock) { coverArrivals.Clear(); coverArrivalsPosted = false; }
                                }
                            }
                        }
                        catch { }
                    // La imagen ya esta publicada y el aviso a UI ya esta encolado. Guardar SOLO la
                    // copia privada: la portada publicada puede estar dibujandose o haber sido expulsada.
                    if (miniaturaParaGuardar != null && thumb != null && j.Gen == coverGen)
                    {
                        try { if (string.Equals(identidadAntes, ThumbCachePath(key), StringComparison.OrdinalIgnoreCase))
                            SaveThumb(miniaturaParaGuardar, identidadAntes); }
                        catch { }
                    }
#if ROCOLA_GPU
                    if (previewBytes != null && thumb != null && j.Gen == coverGen)
                        try { PreviewStore().Save(identidadAntes, previewBytes); } catch { }
#endif
                }
                finally
                {
#if ROCOLA_GPU
                    if (preview != null) preview.Dispose();
#endif
                    if (miniaturaParaGuardar != null) try { miniaturaParaGuardar.Dispose(); } catch { }
                    if (entered) coverSem.Release(); ReleaseCoverUnit(j);
                }
            }
            }
            catch (Exception ex)
            {
                try { LogError("Lector de caratulas: fallo inesperado; el hilo termina y se arma otro cuando haga falta", ex); } catch { }
            }
            finally
            {
                if (!salio)
                    lock (coverLock)
                    {
                        coverPumpCount--;
                        if (enCurso != null && enCurso.Gen == coverGen && enCurso.Key != null)
                        { coverLoading.Remove(enCurso.Key); coverRetryUntil[enCurso.Key] = unchecked(Environment.TickCount + 30000); }
                        System.Threading.Monitor.PulseAll(coverLock);
                    }
            }
        }

        // Caché de miniaturas en DISCO: la 1a vez decodifica la portada grande y guarda una miniatura
        // pequeña; las siguientes veces lee la miniatura (leer ~30KB es muchisimo mas rapido que decodificar
        // un JPG grande del disco duro). Acelera enormemente volver a ver paginas ya vistas.
        private static string thumbDir;

        // DONDE VIVEN LAS MINIATURAS. Iban a la carpeta TEMPORAL del sistema, y eso las condenaba: es una
        // carpeta que por definicion se puede tirar, y Windows la limpia cada tanto -en la rocola de Linux
        // es peor todavia, porque alli esta EN LA MEMORIA y se borra al apagar-. O sea que la promesa de
        // aqui arriba -"la 1a vez tarda; luego todo vuela"- se rompia sola cada cierto tiempo y habia que
        // volver a decodificar la portada grande de cada disco. Medido: 16 ms la primera vez contra 1.5 ms
        // con la miniatura hecha, once veces mas.
        //
        // Ahora van con todo lo demas que la rocola guarda, en la carpeta de datos.
        private static string CarpetaMiniaturas()
        {
            if (thumbDir == null)
            {
                string d = RutaDatos("rocola_miniaturas");
                try { if (!Directory.Exists(d)) Directory.CreateDirectory(d); } catch { }
                thumbDir = d;
            }
            return thumbDir;
        }

        private static string ThumbCachePath(string coverPath)
        {
            string identidad = coverPath.ToLowerInvariant();
            try
            {
                FileInfo fi = new FileInfo(coverPath);
                identidad += "|" + fi.Length.ToString(CultureInfo.InvariantCulture)
                          + "|" + fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
            }
            catch { identidad += "|sin-archivo"; }
            ulong h = Fnv64(identidad);
            // Version nueva: nunca reutilizar el JPEG anterior, que perdia alfa y calidad.
            return Path.Combine(CarpetaMiniaturas(), h.ToString("x16") + "_" + ThumbPx + "_v2.png");
        }

        // CARATULAS EN JPEG (interruptor de Ctrl+F3, apagado de fabrica). La identidad de arriba no cambia; con el
        // interruptor, las portadas OPACAS se guardan en JPEG de calidad alta al lado (mismo nombre, .jpg): unas seis
        // veces menos disco y lectura que en PNG y mucho menos al guardarlas (medido con GDI+ de Mono: 22 KB frente a
        // 142 KB; leer 0,85 ms frente a 2,7; guardar 0,6 ms frente a 23). A cambio, JPEG guarda el color a media
        // resolucion: algo de nitidez en letras y bordes de color. Las que tienen TRANSPARENCIA siguen en PNG.
        // Apagado se ignoran los JPEG: cada caratula vuelve a su PNG sin perdida.
        internal const long MiniaturaCalidadJpeg = 92;
        private static bool JpegActivo
        {
            get
            {
#if ROCOLA_GPU
                return caratulasJpeg;   // solo con tarjeta grafica: el perfil por CPU esta congelado y sigue en PNG
#else
                return false;
#endif
            }
        }
        internal static string MiniaturaJpeg(string identidad) { return Path.ChangeExtension(identidad, ".jpg"); }
        // La miniatura guardada que vale ahora para esa identidad, o null si no hay.
        internal static string MiniaturaGuardada(string identidad)
        {
            if (string.IsNullOrEmpty(identidad)) return null;
            if (JpegActivo) { string jpg = MiniaturaJpeg(identidad); if (File.Exists(jpg)) return jpg; }
            return File.Exists(identidad) ? identidad : null;
        }
        // Una PNG transparente ya es definitiva. La marca vive junto a SU identidad (fuente/tamano/fecha/version),
        // no en una lista de RAM sin limite; no exige volver a decodificar la portada en cada censo o arranque.
        internal static string MiniaturaAlfa(string identidad) { return identidad + ".alfa"; }
        internal static bool MiniaturaPorRehacer(string identidad)
        {
            if (MiniaturaGuardada(identidad) == null) return true;
            return JpegActivo && !File.Exists(MiniaturaJpeg(identidad)) && !File.Exists(MiniaturaAlfa(identidad));
        }
#if ROCOLA_GPU
        private static void ConfirmaFormatoMiniatura(string identidad, bool alfa)
        {
            try
            {
                string marca = MiniaturaAlfa(identidad);
                if (alfa) File.WriteAllText(marca, "PNG-alpha-v1");
                else if (File.Exists(marca)) File.Delete(marca);
            }
            catch { }   // la imagen ya esta guardada; si no se pudo marcar se comprobara de nuevo mas adelante
        }
#endif
        // Si TODOS sus pixeles son opacos. Una pasada por 65 mil pixeles: mucho menos que guardar un PNG.
        internal static bool MiniaturaOpaca(Bitmap b)
        {
            if (b == null) return false;
            int w = b.Width, h = b.Height;
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] fila = new int[w];
                for (int y = 0; y < h; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + y * (long)d.Stride), fila, 0, w);
                    for (int x = 0; x < w; x++) if ((uint)fila[x] < 0xFF000000u) return false;
                }
                return true;
            }
            finally { b.UnlockBits(d); }
        }
        // Deja opacos los pixeles que el remuestreo dejo a medias (solo el filo): se quita la premultiplicacion para
        // no oscurecerlos. Solo para portadas sin transparencia, y solo con el interruptor.
        internal static void HazOpaca(Bitmap b)
        {
            if (b == null || b.PixelFormat != PixelFormat.Format32bppPArgb) return;
            int w = b.Width, h = b.Height;
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                int[] fila = new int[w];
                for (int y = 0; y < h; y++)
                {
                    IntPtr p = new IntPtr(d.Scan0.ToInt64() + y * (long)d.Stride);
                    System.Runtime.InteropServices.Marshal.Copy(p, fila, 0, w);
                    bool cambia = false;
                    for (int x = 0; x < w; x++)
                    {
                        uint v = (uint)fila[x], a = v >> 24;
                        if (a == 255) continue;
                        uint r = (v >> 16) & 255, gr = (v >> 8) & 255, bl = v & 255;
                        if (a > 0) { r = Math.Min(255u, (r * 255 + a / 2) / a); gr = Math.Min(255u, (gr * 255 + a / 2) / a); bl = Math.Min(255u, (bl * 255 + a / 2) / a); }
                        fila[x] = unchecked((int)(0xFF000000u | (r << 16) | (gr << 8) | bl)); cambia = true;
                    }
                    if (cambia) System.Runtime.InteropServices.Marshal.Copy(fila, 0, p, w);
                }
            }
            finally { b.UnlockBits(d); }
        }
        private static ImageCodecInfo codecJpegMiniatura; private static bool codecJpegBuscado;
        private static ImageCodecInfo CodecJpegMiniatura()
        {
            if (!codecJpegBuscado)
            {
                try { foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders()) if (c.MimeType == "image/jpeg") { codecJpegMiniatura = c; break; } }
                catch { }
                codecJpegBuscado = true;
            }
            return codecJpegMiniatura;
        }

        // EL BARRIDO. Al salir de la carpeta temporal ya nadie las borra nunca, asi que hay que barrer.
        // Pero NO por "esto ya no esta en la biblioteca": la rocola admite varios discos de musica, y
        // desconectar uno un fin de semana no puede costar reconstruir todas sus portadas. Se barre por
        // EDAD, y solo si la carpeta se ha hecho grande de verdad.
        //
        // El tope es holgado a proposito: una miniatura ocupa unos 23 KB, asi que 200 MB son casi nueve
        // mil discos. En una rocola normal esto no se dispara JAMAS; esta para que la carpeta no pueda
        // crecer sin final en una maquina que lleve anos cambiando de musica.
        //
        // La fecha que se mira es la de cuando se HIZO la miniatura, no la de cuando se uso: apuntar cada
        // uso seria escribir en el disco una vez por portada leida, que es justo lo que todo esto existe
        // para evitar. Como mucho se borra una que seguia en uso, y la proxima vez que se vea ese disco se
        // rehace sola: una portada, 16 ms, y ya esta otra vez.
        private const int MiniaturasTopeMb = 200;    // por debajo de esto no se barre nada
        private const int MiniaturasDiasMax = 365;   // y por encima, solo lo que lleve un ano sin rehacerse

        // Las claves de miniatura cambian con cada version de una portada. No retener
        // un candado eterno por version ni compartir estas franjas con creditos/datos.
        private static readonly object[] thumbWriteLocks = CreaCandadosMiniatura();
        private static object[] CreaCandadosMiniatura()
        {
            object[] locks = new object[64];
            for (int i = 0; i < locks.Length; i++) locks[i] = new object();
            return locks;
        }
        private static object CandadoMiniatura(string path)
        {
            return thumbWriteLocks[StringComparer.OrdinalIgnoreCase.GetHashCode(path) & (thumbWriteLocks.Length - 1)];
        }

        private static Image DecodeCover(string path)
        {
            string cacheFile = null;
            try { cacheFile = ThumbCachePath(path); } catch { }
            return DecodeCoverWithIdentity(path, cacheFile);
        }

        private static Image DecodeCoverWithIdentity(string path, string cacheFile)
        {
            Bitmap pendiente;
            return DecodeCoverCore(path, cacheFile, false, false, out pendiente);
        }
#if ROCOLA_GPU
        private static Image DecodeCoverForPublication(string path, string cacheFile, out Bitmap pendiente)
        { return DecodeCoverCore(path, cacheFile, true, false, out pendiente); }
        // El calentador, con "Caratulas en JPEG" encendido: una miniatura que solo esta en PNG se rehace desde la
        // portada original (si es opaca saldra en JPEG). La pantalla, en cambio, sigue leyendo el PNG: es rapido.
        private static Image DecodeCoverParaCalentar(string path, string cacheFile, out Bitmap pendiente)
        { return DecodeCoverCore(path, cacheFile, true, JpegActivo, out pendiente); }
#endif
        // LEER UNA CARATULA SIN DEJAR BASURA GRANDE (13-10-2026). Antes cada lectura era File.ReadAllBytes: un arreglo nuevo del
        // tamano del archivo. Una miniatura en PNG de 256x256 pesa casi siempre mas de 85 KB, y en .NET un arreglo asi va al
        // monton de objetos grandes, que solo se limpia con una recoleccion COMPLETA, la que para todos los hilos (tambien
        // el de la pantalla). Pasando paginas rapido son 18 por pagina: el cielo daba micro tirones aunque los FPS no
        // bajaran. Ahora cada hilo lector usa SU arreglo, reservado una vez y reutilizado (GDI+ termina con el antes de
        // devolver la miniatura: se copia con Clone o se dibuja en la nueva). Las JPEG (6 veces menos) ya cabian de sobra.
        // Un archivo de mas de LecturaReutilizada (una portada original enorme) se lee como siempre.
        internal const int LecturaReutilizada = 2 * 1024 * 1024;
        [ThreadStatic] private static byte[] lecturaCaratula;
        internal static byte[] LeeEntero(string ruta, out int largo)
        {
            using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read))   // lo mismo que File.ReadAllBytes
            {
                long n = fs.Length;
                if (n > int.MaxValue) throw new IOException("archivo demasiado grande");
                byte[] b;
                if (n > LecturaReutilizada) b = new byte[n];
                else
                {
                    b = lecturaCaratula;
                    if (b == null || b.Length < n) { b = new byte[Math.Max((int)n, 256 * 1024)]; lecturaCaratula = b; }
                }
                int hecho = 0, r;
                while (hecho < n && (r = fs.Read(b, hecho, (int)n - hecho)) > 0) hecho += r;
                if (hecho != n) throw new EndOfStreamException("la caratula cambio mientras se leia");
                largo = (int)n;
                return b;
            }
        }
        private static Image DecodeCoverCore(string path, string cacheFile, bool publicarPrimero, bool soloJpeg, out Bitmap pendiente)
        {
            pendiente = null;
            // 1) miniatura en cache -> leerla (rapido)
            if (cacheFile != null)
            {
                try
                {
                    string guardada = MiniaturaGuardada(cacheFile);   // PNG, o el JPEG si el interruptor esta encendido
                    if (soloJpeg && guardada != null && MiniaturaPorRehacer(cacheFile))
                        guardada = null;   // PNG sin decision definitiva: comprobar desde la original si puede pasar a JPEG
                    if (guardada != null)
                    {
                        byte[] cdata; int clargo;
                        if (diskProbeN < 16)   // perfilado: cronometra las primeras lecturas de miniaturas
                        {
                            System.Diagnostics.Stopwatch dsw = System.Diagnostics.Stopwatch.StartNew();
                            cdata = LeeEntero(guardada, out clargo);
                            dsw.Stop();
                            DiskProbe((int)dsw.ElapsedMilliseconds);
                        }
                        else cdata = LeeEntero(guardada, out clargo);   // I/O puro primero; GDI+ decodifica desde RAM
                        using (MemoryStream cms = new MemoryStream(cdata, 0, clargo, false))
                        using (Image cimg = Image.FromStream(cms, false, false))
                        {
                            // Una copia del bitmap, no otra pasada de composicion GDI+:
                            // evita redondear el alfa otra vez y no retiene el stream.
                            Bitmap cb = cimg as Bitmap;
                            if (cb == null || cb.Width != ThumbPx || cb.Height != ThumbPx)
                                throw new InvalidDataException("Miniatura de tamano incompatible");
#if ROCOLA_GPU
                            if (publicarPrimero) return CopiaMiniaturaGpu(cb);
#endif
                            return cb.Clone(new Rectangle(0, 0, ThumbPx, ThumbPx), PixelFormat.Format32bppPArgb);
                        }
                    }
                }
                catch { /* cache corrupta -> recodificar abajo */ }
            }

            // 2) decodificar la portada original y GUARDAR la miniatura para la proxima vez
            try
            {
                int largo;
                byte[] data = LeeEntero(path, out largo);   // I/O puro primero (lectura secuencial corta); GDI+ decodifica desde RAM
                using (MemoryStream ms = new MemoryStream(data, 0, largo, false))
                using (Image src = Image.FromStream(ms, false, false))
                {
                    Bitmap thumb = new Bitmap(ThumbPx, ThumbPx, PixelFormat.Format32bppPArgb);
                    // Con el interruptor JPEG: una portada SIN transparencia (casi todas) es opaca, pero al reducirla el
                    // filtro mezclaba el borde con lo de fuera y lo dejaba semitransparente. Se reduce repitiendo su
                    // propio borde y queda opaca de verdad, que es lo que permite guardarla en JPEG (SaveThumb). Las de
                    // paleta (GIF) pueden traer un color transparente sin canal alfa: esas no cuentan como opacas.
                    bool fuenteOpaca = JpegActivo && !Image.IsAlphaPixelFormat(src.PixelFormat)
                                       && (src.PixelFormat & PixelFormat.Indexed) == 0 && (src.Flags & (int)ImageFlags.HasAlpha) == 0;
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;   // filtro original de la miniatura (1 vez por disco)
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        if (fuenteOpaca)
                            using (ImageAttributes borde = new ImageAttributes())
                            {
                                borde.SetWrapMode(WrapMode.TileFlipXY);
                                g.DrawImage(src, new Rectangle(0, 0, ThumbPx, ThumbPx), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, borde);
                            }
                        else g.DrawImage(src, 0, 0, ThumbPx, ThumbPx);
                    }
                    if (fuenteOpaca) HazOpaca(thumb);
                    if (cacheFile != null)
                    {
                        if (publicarPrimero)
                            try { pendiente = (Bitmap)thumb.Clone(); } catch { }
                        if (pendiente == null) SaveThumb(thumb, cacheFile);
                    }
                    return thumb;
                }
            }
            catch { return null; }
        }

#if ROCOLA_GPU
        // Un Clone de una imagen decodificada no garantiza desprenderse de su stream en GDI+ antiguo.
        // Materializar los pixeles ANTES de cerrarlo y de reutilizar lecturaCaratula. Copia PArgb sin
        // otra composicion: conserva el alfa y no reserva un arreglo grande por cada portada.
        private static Bitmap CopiaMiniaturaGpu(Bitmap origen)
        {
            Bitmap copia = new Bitmap(origen.Width, origen.Height, PixelFormat.Format32bppPArgb);
            try
            {
                Rectangle area = new Rectangle(0, 0, origen.Width, origen.Height);
                BitmapData src = origen.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    BitmapData dst = copia.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                    try
                    {
                        byte[] fila = new byte[area.Width * 4];
                        for (int y = 0; y < area.Height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(new IntPtr(src.Scan0.ToInt64() + (long)y * src.Stride), fila, 0, fila.Length);
                            System.Runtime.InteropServices.Marshal.Copy(fila, 0, new IntPtr(dst.Scan0.ToInt64() + (long)y * dst.Stride), fila.Length);
                        }
                    }
                    finally { copia.UnlockBits(dst); }
                }
                finally { origen.UnlockBits(src); }
                return copia;
            }
            catch { copia.Dispose(); throw; }
        }
#endif

        // Guarda en cache acotando el tamaño (FIFO). Llamar SIEMPRE bajo coverLock.
        // Las expulsadas NO se liberan aqui (el hilo de pantalla podria estar dibujandolas):
        // se mandan a la papelera y se liberan en el temporizador, cuando no hay repintado en curso.
        // Estos tres metodos se llaman bajo coverLock. Consultar desde prelectura NO
        // rejuvenece una portada: solo el uso visible debe protegerla de la expulsion.
        private void CoverToca(string key)
        {
            LinkedListNode<string> node;
            if (!coverNodes.TryGetValue(key, out node) || node == coverOrder.Last) return;
            coverOrder.Remove(node);
            coverOrder.AddLast(node);   // reutilizar el nodo, sin asignaciones por repintado
        }

        private bool CoverEnCache(string key, bool visible, out Image img)
        {
            if (!coverCache.TryGetValue(key, out img)) return false;
            if (visible) CoverToca(key);
            return true;
        }

        // UNA pieza por latido, y solo con la rocola quieta: igual que el horneado de la pagina de al lado
        // (BakeAhead) y que el precalentador de portadas. Asi la rocola enciende igual de rapido que siempre
        // y en unos segundos esta todo caliente, sin quitarle la maquina a nadie.
#if ROCOLA_GPU
        private bool precalAudioLanzado;
        private readonly object precalTrabajoLock = new object();
        private volatile bool precalAudioConMargen, precalAudioEnCurso, precalPantallaEnCurso;
        private double precalUltimoCoste = 8.0;

        // Una sola pieza DSP por turno de un trabajador de baja prioridad, DESPUES del primer cuadro.
        // No se abre musica ni se lee el disco; calentar no cambia mediciones ni la cadena de audio.
        private void PrecalientaAudioDespuesDelCuadro()
        {
            precalAudioConMargen = gpuEnabled && gpuPanel != null && gpuPanel.CuadrosMedidos > 0
                && WindowState != FormWindowState.Minimized && gpuPanel.MsTrabajo < 20.0;
            if (precalAudioLanzado || !precalAudioConMargen || startupClosing
                || Environment.TickCount - lastInputTick < PrecalQuieta) return;
            precalAudioLanzado = true;
            System.Threading.Thread t = new System.Threading.Thread(delegate ()
            {
                try
                {
                    for (int paso = 0; paso < 4 && !startupClosing; paso++)
                    {
                        while (!startupClosing)
                        {
                            bool portadas; lock (coverLock) { portadas = coverLoading.Count != 0 || coverQHi.Count != 0; }
                            if (precalAudioConMargen && !precalPantallaEnCurso && !portadas && !videoOn
                                && Environment.TickCount - lastInputTick >= PrecalQuieta) break;
                            System.Threading.Thread.Sleep(100);
                        }
                        if (startupClosing) return;
                        lock (precalTrabajoLock)
                        {
                            if (startupClosing) return;
                            precalAudioEnCurso = true;
                            try { PrecalientaTablasAudio(paso); }
                            finally { precalAudioEnCurso = false; }
                        }
                        System.Threading.Thread.Sleep(CargaTrabajo.Procesadores <= 2 ? 100 : 40);
                    }
                }
                catch { } // un lujo de arranque: la primera llamada normal conserva su inicializacion
                finally { precalAudioEnCurso = false; }
            });
            t.IsBackground = true; t.Priority = System.Threading.ThreadPriority.Lowest; t.Start();
        }
#endif

        private void PrecalientaUnPoco()
        {
#if ROCOLA_GPU
            PrecalientaAudioDespuesDelCuadro();
            if (gpuEnabled)
            {
                if (gpuPanel == null || gpuPanel.CuadrosMedidos == 0 || WindowState == FormWindowState.Minimized
                    || precalAudioEnCurso || gpuPanel.MsTrabajo + precalUltimoCoste > 28.0) return;
                // No competir con una pagina nueva. La caché visual llega antes que pantallas ocultas.
                lock (coverLock) if (coverLoading.Count != 0 || coverQHi.Count != 0) return;
            }
#endif
            if (precalPaso < 0) return;
            if (Environment.TickCount - lastInputTick < PrecalQuieta) return;   // hay alguien usandola: ahora no
            bool hecho = false;
#if ROCOLA_GPU
            System.Diagnostics.Stopwatch coste = null;
            if (gpuEnabled)
            {
                if (!System.Threading.Monitor.TryEnter(precalTrabajoLock)) return; // nunca esperar al DSP en UI
                precalPantallaEnCurso = true; coste = System.Diagnostics.Stopwatch.StartNew();
            }
#endif
            try
            {
                switch (precalPaso)
                {
                    case 0:
#if ROCOLA_GPU
                        // Con tarjeta el buscador se pinta con forGpu=true (EnsureUiOverlay): ese es el camino que hay que calentar (PrecargaPaginaGpu.cs).
                        if (gpuEnabled) { hecho = PrecalientaBuscadorGpu(); break; }
#endif
                        hecho = EnSeco(searchOverlay, delegate (Graphics g)
                        { searchOverlay.RenderContent(g, new Rectangle(0, 0, searchOverlay.Width, searchOverlay.Height), false); });
                        break;
                    case 1:
#if ROCOLA_GPU
                        if (gpuEnabled) { hecho = PrecalientaListaGpu(); break; }   // con la marca de la tarjeta (la fila elegida dentro del cristal)
#endif
                        hecho = EnSeco(songList, delegate (Graphics g) { songList.RenderContent(g); });
                        break;
                    case 2:
                        // La pantalla de reposo no tiene RenderContent aparte: se le pide su propio dibujado,
                        // que es lo mismo que hace la pantalla cuando le toca pintarse.
                        hecho = EnSeco(attractView, delegate (Graphics g)
                        {
                            using (PaintEventArgs pe = new PaintEventArgs(g, new Rectangle(0, 0, attractView.Width, attractView.Height)))
                                InvokePaint(attractView, pe);
                        });
                        break;
#if ROCOLA_GPU
                    case 3: case 4: case 5: case 6:   // lo que solo pinta la tarjeta: estado extendido, pastilla y ficha, globos de la estrella, avisos (PrecargaPaginaGpu.cs)
                        hecho = !gpuEnabled || PrecalientaPiezaGpu(precalPaso);
                        break;
#endif
                    default:
                        precalPaso = -1; Diag("precalentado: las pantallas que no se ven al encender, ya horneadas");
                        return;
                }
            }
            catch { hecho = true; }   // precalentar es un lujo: si una pieza no se deja dibujar aun, se pasa a la siguiente
#if ROCOLA_GPU
            finally
            {
                if (coste != null)
                {
                    precalUltimoCoste = Math.Max(2.0, Math.Min(20.0, coste.Elapsed.TotalMilliseconds));
                    precalPantallaEnCurso = false; System.Threading.Monitor.Exit(precalTrabajoLock);
                }
            }
#endif
            if (hecho || ++precalReintentos > 60) { precalPaso++; precalReintentos = 0; }
        }
    }
}

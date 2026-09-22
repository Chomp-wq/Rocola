// LA BIBLIOTECA: escaneo, generos, duraciones y la cache de arranque
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

        // ---- Controles ----
        private LetterBar letterBar;
        private System.Threading.Thread[] preScanTh;    // hilos del pre-analisis (ver CarrilesRepaso)
        private volatile bool preScanStop = false;      // pedirle al pre-analisis que se detenga (al cerrar)
        // Lo que falta por medir. Lo bajan VARIOS carriles del repaso y tambien el hilo de la interfaz al
        // publicar cada medida, asi que se toca siempre con Interlocked (que ademas hace de barrera: por eso
        // no hace falta 'volatile', que encima no se puede combinar con Interlocked sin que el compilador
        // avise). Quien lo LEE es la barra de avance, y ahi un valor con unas milesimas de retraso da igual.
        private int preScanLeft = 0;                    // archivos que le faltan por medir (0 = al dia)
        private volatile int preScanGen = 0;            // generacion: al re-escanear la biblioteca (F5) el hilo viejo caduca y arranca uno nuevo
        private volatile int preScanTotal = 0;          // cuantas canciones tiene la tanda en curso (para la barra de avance)
        // ENTRENAMIENTO A FONDO (boton en Ctrl+F3, para cuando se hace mantenimiento): la rocola se dedica POR
        // COMPLETO a medir. Repasa TODA la biblioteca (no solo lo que falta), analiza cada cancion ENTERA en vez
        // de por muestras, y no espera a estar en reposo ni descansa entre archivos. Se apaga con el mismo boton.
        // A proposito NO se guarda en la configuracion: si se va la luz, la rocola vuelve a su modo normal sola
        // y nunca se queda a todo gas durante el horario del negocio por un olvido.
        private volatile bool preScanTurbo = false;
        private int generoVer;   // sube cuando el paseo de fondo aprende generos (entra en la firma del interior del disco)
        private Dictionary<string, int> artistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private const int ArtistStatCap = 1000, ArtistStatTarget = 900;

        private readonly HashSet<char> letterAvailable = new HashSet<char>();
        // EL COLOR DE CADA LETRA: el genero que MANDA dentro de ella. Sale de leer el ID3 de toda la
        // biblioteca, asi que se guarda en disco junto al numero de canciones con el que se calculo: mientras
        // la biblioteca no cambie, el proximo arranque ya lo tiene y no hay que volver a pasear los archivos.
        private Dictionary<char, string> letterTag = new Dictionary<char, string>();
        private volatile bool libBuilding = false;   // el worker esta armando albums/byLetter/searchIndex: la UI no debe leerlos
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

        // Escribe el informe: las peores primero, para ir directo a lo que conviene reemplazar.
        private string CalidadFile() { return RutaDatos("rocola_calidad.txt"); }

        private Color GeneroDeLoQueSuena(Track t, Album org)
        {
            if (t == null) return DefaultAccent;

            // El morado de BTS es una PETICION del bar, no una deduccion: se queda como esta y no se le
            // gira el tono. Lo pidieron morado, sale morado, tenga la portada el color que tenga.
            if (EsArtistaMorado(t)) return Color.FromArgb(149, 86, 220);
            string et = GeneroDeCancion(t, org);
            return (et.Length > 0) ? ColorDeEtiqueta(et) : DefaultAccent;
        }

        private void Letter_Refresh() { letterBar.Current = currentLetter; letterBar.Invalidate(); }

        private static readonly string[] AudioExt = { ".mp3", ".wav" };

        private List<string> scanRoots;   // raices del escaneo en curso/vigente (las usa el worker; solo se asignan aqui)

        private void Scanner_DoWork(object sender, DoWorkEventArgs e)
        {
            // El BackgroundWorker usa un hilo compartido: restaurar su prioridad al salir.
            System.Threading.Thread thread = System.Threading.Thread.CurrentThread;
            System.Threading.ThreadPriority previous = thread.Priority;
            scanDirectoryMemo = new ScanDirectoryMemo();
            try
            {
                try { thread.Priority = System.Threading.ThreadPriority.BelowNormal; } catch { }
                ScanLibraryCore(sender, e);
            }
            finally
            {
                scanDirectoryMemo.Clear(); scanDirectoryMemo = null;
                try { thread.Priority = previous; } catch { }
            }
        }

        private void ScanLibraryCore(object sender, DoWorkEventArgs e)
        {
            List<string> roots = (List<string>)e.Argument;
            lastFreshRootSig = null;
            // CACHE DE BIBLIOTECA: si es valido, evita las DOS caminatas del disco (en HDD eran minutos).
            List<Track> ctk; List<string> cvd; Dictionary<string, string> ccv;
            string sigObservada;
            if (TryLoadLibraryCache(roots, out ctk, out cvd, out ccv, out sigObservada))
            {
                // ARMADO EN SEGUNDO PLANO: ordenar e indexar las 118 mil canciones tomaba 1-3 segundos
                // DEL HILO DE LA INTERFAZ al arrancar; aqui corre en el worker y el arranque no se congela.
                tracks.AddRange(ctk);
                PerfilaDisco(tracks);   // disco duro o SSD, decidido ANTES de la primera pagina de caratulas
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
            // La cache rechazada ya hizo el recorrido previo. Sigue habiendo firma
            // antes y despues del escaneo para detectar cambios concurrentes.
            string sigAntes = sigObservada;
            if (sigAntes == null) try { sigAntes = RootsSigForScan(roots); } catch { }
            for (int r = 0; r < roots.Count; r++) ScanFolders(roots[r], audio, vids, covs);
            for (int i = 0; i < audio.Count; i++) found.Add(Track.FromFile(audio[i]));
            tracks.AddRange(found);
            PerfilaDisco(tracks);   // idem por el camino del escaneo fresco
            cachedVideoPaths = vids; cachedCovers = covs;
            BuildAlbums(roots); BuildVideoSection(roots); BuildSearchIndex();   // tambien en segundo plano
            try
            {
                string sigDespues = RootsSig(roots);
                if (sigAntes != null && sigAntes == sigDespues) lastFreshRootSig = sigDespues;
            }
            catch { lastFreshRootSig = null; }
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
            LeeTintes(FirmaBiblioteca());   // el color de cada letra, si ya se calculo para esta misma biblioteca
            RecalculaGenerosArtista();      // y de esos generos de disco salen los del artista, sin leer nada mas
            nombreTag.Clear();              // biblioteca nueva: lo adivinado por el nombre se vuelve a mirar
            RecalculaLetras();

            string order = LetrasOrden;
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
        // Vuelca las huellas del EQ recien medidas. Se llama de tanto en tanto (no por cancion): recalcular el
        // promedio de la biblioteca y reescribir el archivo en cada tema seria puro desgaste de disco.
        private void FlushEqIfDirty()
        {
            if (!eqDirty) return;
            eqDirty = false;
            EnsureEqAverage();
            SaveEqCache();
        }
        // Las mismas rutas viven en varias tablas (sonoridad, duracion, EQ, cosecha de IA, repaso a fondo) y en
        // la propia lista de canciones. Con cien mil canciones eso eran CINCO copias del mismo texto, decenas de
        // megas tirados en una maquina de 4 GB. Compartiendo UNA sola copia por ruta se juntan todas en una, y
        // de paso las busquedas salen mas rapido (comparar dos iguales se resuelve por referencia).
        // No internar rutas para toda la sesion: al retirar una biblioteca, sus textos deben
        // poder recogerse si ninguna tabla los necesita. El indice solo guarda referencias debiles.
        private sealed class PoolRutas
        {
            private sealed class Entrada : WeakReference
            {
                internal Entrada Siguiente;
                internal Entrada(string s, Entrada siguiente) : base(s) { Siguiente = siguiente; }
            }
            private readonly Entrada[] cubetas = new Entrada[65536];   // 256/512 KiB de indice en 32/64 bits
            private readonly object candado = new object();
            private const int MaxEntradas = 262144, MaxCadena = 64;
            private int cantidad, cursor, llamadas;

            private void Limpia(int index)
            {
                Entrada anterior = null, actual = cubetas[index];
                while (actual != null)
                {
                    Entrada siguiente = actual.Siguiente;
                    if (!actual.IsAlive)
                    {
                        if (anterior == null) cubetas[index] = siguiente; else anterior.Siguiente = siguiente;
                        cantidad--;
                    }
                    else anterior = actual;
                    actual = siguiente;
                }
            }
            private void RetiraCubeta(int index)
            {
                for (Entrada e = cubetas[index]; e != null; e = e.Siguiente) cantidad--;
                cubetas[index] = null;   // solo olvida deduplicacion, no datos de ninguna tabla
            }
            internal string Comparte(string s)
            {
                if (string.IsNullOrEmpty(s)) return s;
                lock (candado)
                {
                    if ((unchecked(++llamadas) & 255) == 0)
                        for (int i = 0; i < 8; i++) { Limpia(cursor); cursor = (cursor + 1) & (cubetas.Length - 1); }
                    // CLR 2 x86 concentra los bits bajos de algunos patrones de rutas.
                    // Mezclar tambien los altos evita cadenas largas sin cambiar la igualdad.
                    uint hash = unchecked((uint)s.GetHashCode());
                    hash ^= hash >> 16; hash = unchecked(hash * 0x7feb352dU); hash ^= hash >> 15;
                    int index = (int)(hash & (uint)(cubetas.Length - 1));
                    Entrada anterior = null, actual = cubetas[index];
                    int largo = 0;
                    while (actual != null)
                    {
                        Entrada siguiente = actual.Siguiente;
                        string viva = actual.Target as string;   // una sola lectura: referencia fuerte durante la comparacion
                        if (viva == null)
                        {
                            if (anterior == null) cubetas[index] = siguiente; else anterior.Siguiente = siguiente;
                            cantidad--;
                        }
                        else
                        {
                            if (string.Equals(viva, s, StringComparison.Ordinal)) return viva;
                            anterior = actual; largo++;
                        }
                        actual = siguiente;
                    }
                    // Topes tanto de bookkeeping como de trabajo ante colisiones.
                    // Rebasarlos puede perder un acierto, nunca cambiar el contenido de una ruta.
                    if (largo >= MaxCadena) RetiraCubeta(index);
                    while (cantidad >= MaxEntradas)
                    {
                        RetiraCubeta(cursor);
                        cursor = (cursor + 1) & (cubetas.Length - 1);
                    }
                    cubetas[index] = new Entrada(s, cubetas[index]); cantidad++;
                    return s;
                }
            }
        }
        private static readonly PoolRutas rutasCompartidas = new PoolRutas();
        private static string Pool(string s) { return rutasCompartidas.Comparte(s); }
        // Formato: "ruta" (nivel 1, el de siempre) o "N|ruta" (nivel N). Una ruta de Windows no puede llevar
        // "|", asi que distinguirlos es seguro y los archivos viejos se siguen leyendo tal cual.
        private void LoadIaDone()
        {
            try { ReadIaDone(IaDoneFile(), iaDone); } catch { }
        }

        private static void ReadIaDone(string f, Dictionary<string, int> iaDone)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (StreamReader reader = new StreamReader(f))
                    ReadIaDoneRecords(reader, iaDone);
            }
            catch { }
        }

        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadIaDoneRecords(TextReader reader, Dictionary<string, int> iaDone)
        {
            Dictionary<string, int> pending = new Dictionary<string, int>(iaDone.Comparer);
            FillIaDoneRecords(reader, pending, iaDone);
            foreach (KeyValuePair<string, int> item in pending) iaDone[item.Key] = item.Value;
        }

        private static void FillIaDoneRecords(TextReader reader, Dictionary<string, int> pending, Dictionary<string, int> iaDone)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string r = line.Trim(); if (r.Length == 0) continue;
                int niv = 1;
                if (r.Length > 2 && r[1] == '|') { int d = r[0] - '0'; if (d >= 1 && d <= 9) { niv = d; r = r.Substring(2); } }
                // Si una cancion aparece dos veces manda la de MAS VALOR (ver IaRango): normalmente es
                // la ultima, porque el archivo se escribe anadiendo al final, pero lo que decide es el
                // valor y no la posicion. Asi un archivo escrito a medias o desordenado no puede hacer
                // que se vuelva a cosechar algo ya cosechado.
                r = Pool(r);
                int ant; if ((pending.TryGetValue(r, out ant) || (iaDone != null && iaDone.TryGetValue(r, out ant))) && IaRango(ant) >= IaRango(niv)) continue;
                pending[r] = niv;
            }
        }
        // Apunta que de esta cancion ya se cosecho hasta "nivel". false si ya estaba en ese nivel o mas.
        private bool MarkIaDone(string path, int nivel)
        {
            lock (iaDoneLock)
            {
                int ant; if (iaDone.TryGetValue(path, out ant) && IaRango(ant) >= IaRango(nivel)) return false;
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
            lock (iaDoneLock)
            {
                if (iaPend.Count == 0 || iaDoneSaving) return;
                nuevas = new List<string>(iaPend); iaDoneSaving = true;
            }
            QueueDataWrite(delegate (object ignored)
            {
                bool ok = false;
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    AnexaSeguro(IaDoneFile(), sb.ToString());
                    ok = true;
                }
                catch { }
                finally
                {
                    lock (iaDoneLock)
                    {
                        if (ok)
                            for (int i = 0; i < nuevas.Count && iaPend.Count > 0; i++) iaPend.Remove(nuevas[i]);
                        iaDoneSaving = false; iaDoneDirty = iaPend.Count > 0;
                    }
                }
            });
        }
        private void LoadDeepDone()
        {
            try { ReadDeepDone(DeepDoneFile(), deepDone, loudCache, durCache, eqCache); } catch { }
        }

        private static void ReadDeepDone(string f, HashSet<string> deepDone, Dictionary<string, float> loudCache, Dictionary<string, float> durCache, Dictionary<string, float[]> eqCache)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (StreamReader reader = new StreamReader(f))
                    ReadDeepDoneRecords(reader, deepDone, loudCache, durCache, eqCache);
            }
            catch { }
        }

        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadDeepDoneRecords(TextReader reader, HashSet<string> deepDone, Dictionary<string, float> loudCache, Dictionary<string, float> durCache, Dictionary<string, float[]> eqCache)
        {
            HashSet<string> pending = new HashSet<string>(deepDone.Comparer);
            FillDeepDoneRecords(reader, pending, loudCache, durCache, eqCache);
            foreach (string item in pending) deepDone.Add(item);
        }

        private static void FillDeepDoneRecords(TextReader reader, HashSet<string> pending, Dictionary<string, float> loudCache, Dictionary<string, float> durCache, Dictionary<string, float[]> eqCache)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string r = line.Trim(); if (r.Length == 0) continue;
                r = Pool(r);
                // Una marca que sobrevivio pero cuyas tablas no llegaron al disco no es trabajo hecho.
                // Los videos no tienen huella EQ; para canciones se exigen las tres medidas.
                bool medida = loudCache.ContainsKey(r) && durCache.ContainsKey(r)
                           && (EsVideo(r) || eqCache.ContainsKey(r));
                if (medida) pending.Add(r);
            }
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
            lock (deepDoneLock)
            {
                if (deepPend.Count == 0 || deepDoneSaving) return;
                nuevas = new List<string>(deepPend); deepDoneSaving = true;
            }
            QueueDataWrite(delegate (object ignored)
            {
                bool ok = false;
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    AnexaSeguro(DeepDoneFile(), sb.ToString());
                    ok = true;
                }
                catch { }
                finally
                {
                    lock (deepDoneLock)
                    {
                        if (ok)
                            for (int i = 0; i < nuevas.Count && deepPend.Count > 0; i++) deepPend.Remove(nuevas[i]);
                        deepDoneSaving = false; deepDoneDirty = deepPend.Count > 0;
                    }
                }
            });
        }
        private void LoadDurFast()
        {
            try { ReadDurFast(DurFastFile(), durFast); } catch { }
        }

        private static void ReadDurFast(string f, Dictionary<string, float> durFast)
        {
            try
            {
                if (!File.Exists(f)) return;
                using (StreamReader reader = new StreamReader(f))
                    ReadDurFastRecords(reader, durFast);
            }
            catch { }
        }

        // Preparar por registros sin conservar todas las lineas. Un fallo de lectura
        // antes de EOF descarta pending: ninguna tabla de destino recibe una carga parcial.
        private static void ReadDurFastRecords(TextReader reader, Dictionary<string, float> durFast)
        {
            Dictionary<string, float> pending = new Dictionary<string, float>(durFast.Comparer);
            FillDurFastRecords(reader, pending);
            foreach (KeyValuePair<string, float> item in pending) durFast[item.Key] = item.Value;
        }

        private static void FillDurFastRecords(TextReader reader, Dictionary<string, float> pending)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int bar = line.IndexOf('|');
                if (bar <= 0 || bar >= line.Length - 1) continue;
                float d;
                if (!float.TryParse(line.Substring(0, bar), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) || d <= 0.01f) continue;
                pending[Pool(line.Substring(bar + 1))] = d;
            }
        }
        private void SaveDurFast()
        {
            List<string> nuevas;
            lock (durFastLock)
            {
                if (durFastPend.Count == 0 || durFastSaving) return;
                nuevas = new List<string>(durFastPend); durFastSaving = true;
            }
            QueueDataWrite(delegate (object ignored)
            {
                bool ok = false;
                try
                {
                    StringBuilder sb = new StringBuilder(nuevas.Count * 64 + 16);
                    for (int i = 0; i < nuevas.Count; i++) sb.Append(nuevas[i]).Append("\r\n");
                    AnexaSeguro(DurFastFile(), sb.ToString());
                    ok = true;
                }
                catch { }
                finally
                {
                    lock (durFastLock)
                    {
                        if (ok)
                            for (int i = 0; i < nuevas.Count && durFastPend.Count > 0; i++) durFastPend.Remove(nuevas[i]);
                        durFastSaving = false;
                    }
                }
            });
        }

        // Barrido RAPIDO de duraciones: abre cada archivo, lee cuanto dura y lo suelta. No descomprime nada.
        // Se salta lo que ya tiene duracion (por esta via o por el analisis completo). Prioridad minima y un
        // respiro por archivo para no quitarle disco a la musica; con video en pantalla se detiene del todo.
        // ==========  LO QUE ENSENA LA BARRA DE LETRAS  ==========
        // Debajo de cada letra va cuantas canciones hay detras, y la letra se pinta del color del genero que
        // MANDA dentro de ella. Lo primero es gratis (ya esta contado); lo segundo cuesta leer el ID3 de toda
        // la biblioteca, asi que se calcula una vez en segundo plano y se guarda.
        private string LetterTintFile() { return RutaDatos("rocola_letras.txt"); }

        // Con que biblioteca se calcularon los colores. Si esto cambia, hay que volver a leer los generos.
        private int FirmaBiblioteca() { return tracks.Count * 31 + albums.Count; }

        // Arma los dos diccionarios ENTEROS y se los entrega a la barra de una pieza. La barra los lee desde
        // el dibujado, asi que nunca puede pillar uno a medio llenar: o ve el de antes, o ve el nuevo.
        // El orden de siempre, pero solo con las que tienen algo detras. Si no hubiera ninguna -biblioteca
        // vacia o a medio leer- se deja "#": la barra nunca se queda sin una sola casilla, que la dejaria sin
        // ancho por casilla y sin nada donde estar parado.
        private const string LetrasOrden = "\u2605#ABCDEFGHIJKLMNOPQRSTUVWXYZ\u25BA";
        private string LetrasVisibles()
        {
            StringBuilder sb = new StringBuilder(LetrasOrden.Length);
            for (int i = 0; i < LetrasOrden.Length; i++)
            {
                char c = LetrasOrden[i];
                if (letterAvailable == null || letterAvailable.Contains(c)) sb.Append(c);
            }
            return (sb.Length > 0) ? sb.ToString() : "#";
        }

        // LO ALTA QUE ES LA CAPSULA DE CREDITOS / EN COLA, que es lo mismo que decir LO GRANDE QUE SALE SU
        // LETRA: la capsula se dibuja con cuerpos de 0.34 y 0.52 de su alto. Estaba clavada en 26 px, o sea
        // que los creditos y la cola salian en letra mas chica que todo lo demas de la franja... siendo lo
        // que el cliente tiene que poder leer de lejos, desde la barra del local. Ahora sale del alto de la
        // tarjeta de lo que suena, asi que acompana al resto: en 1024x768 pasa de 26 a 35 px.
        private static int AltoPastilla(int tarjetaH) { return Math.Max(26, (int)(tarjetaH * 0.44f)); }

        private void RecalculaLetras()
        {
            if (letterBar == null) return;
            // Mientras se re-escanea, las listas son del worker y byLetter se esta REHACIENDO. Este aviso puede
            // llegar justo entonces (lo manda el paseo de fondo por BeginInvoke), asi que aqui no se mira nada:
            // al terminar el escaneo se recalcula de todos modos.
            if (libBuilding) return;
            Dictionary<char, int> cuenta = new Dictionary<char, int>();
            foreach (KeyValuePair<char, List<Album>> kv in byLetter)
            {
                List<Album> la = kv.Value;
                if (la == null) continue;
                int n = 0;
                for (int i = 0; i < la.Count; i++) if (la[i] != null && la[i].Tracks != null) n += la[i].Tracks.Count;
                if (n > 0) cuenta[kv.Key] = n;
            }
            if (videoAlbums != null && videoAlbums.Count > 0)
            {
                // El mismo video puede estar en dos discos: se cuenta una sola vez.
                HashSet<string> vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < videoAlbums.Count; i++)
                {
                    Album va = videoAlbums[i];
                    if (va == null || va.VideoFiles == null) continue;
                    for (int j = 0; j < va.VideoFiles.Count; j++) if (va.VideoFiles[j] != null) vistos.Add(va.VideoFiles[j]);
                }
                if (vistos.Count > 0) cuenta[VideoMark] = vistos.Count;
            }
            if (topDiscs != null && topDiscs.Count > 0)
            {
                int nt = 0;
                for (int i = 0; i < topDiscs.Count; i++) if (topDiscs[i] != null && topDiscs[i].Tracks != null) nt += topDiscs[i].Tracks.Count;
                if (nt > 0) cuenta[TopMark] = nt;
            }
            Dictionary<char, Color> tinte = new Dictionary<char, Color>();
            foreach (KeyValuePair<char, string> kv in letterTag) tinte[kv.Key] = ColorDeEtiqueta(kv.Value);
            letterBar.Counts = cuenta;
            letterBar.Tints = tinte;
            // LAS LETRAS VACIAS NI SE DIBUJAN. Antes salian las 29 casillas siempre y las que no tenian nada
            // se pintaban apagadas: ocupaban su sitio para no decir nada. Ahora la barra solo lleva las que
            // tienen contenido, asi que las que quedan se reparten TODO el ancho: la casilla de cada una es
            // mas grande -mas facil de atinar con el dedo- y, cuando la barra va estrecha (con un video puesto
            // al lado), la letra misma tambien crece, porque ahi el cuerpo lo limitaba el ancho de la casilla.
            letterBar.Letters = LetrasVisibles();
            letterBar.Invalidate();
            if (searchOverlay != null) { searchOverlay.DatosVer++; if (searchOpen) searchOverlay.Invalidate(); }
            // El genero de cada cancion tambien se ensena DENTRO del disco. Como se aprende en segundo plano,
            // puede llegar con el disco ya abierto: sin esto, las pastillas no saldrian hasta salir y volver.
            generoVer++;
            ActualizaGenero();   // el genero de lo que suena tambien se aprende de fondo: que aparezca sin esperar
            if (panelSongs != null && panelSongs.Visible) RefrescaLista();
        }

        // Lee del disco el color de cada letra. Solo vale si se calculo con la MISMA biblioteca que hay ahora
        // (se compara el numero de canciones); si no, se descarta y se vuelve a calcular en segundo plano.
        private void LeeTintes(int firmaAhora)
        {
            letterTag.Clear(); discTag.Clear();
            tintesPend = true;
            try
            {
                string f = LetterTintFile();
                if (!File.Exists(f)) return;
                string[] L = File.ReadAllLines(f);
                // v1 solo tenia letras; v2 lleva ademas el genero de cada disco. Un archivo v1 se descarta y se
                // vuelve a calcular: es un paseo de fondo, no una perdida.
                if (L.Length < 2 || L[0] != "ROCOLA-LETRAS v2") return;
                int firma;
                if (!L[1].StartsWith("n=") || !int.TryParse(L[1].Substring(2), out firma)) return;
                if (firma != firmaAhora) return;   // la biblioteca cambio: hay que recalcular
                for (int i = 2; i < L.Length; i++)
                {
                    string ln = L[i];
                    if (ln.Length < 5) continue;
                    if (ln[0] == 'L' && ln[1] == '|' && ln[3] == '|') letterTag[ln[2]] = ln.Substring(4);
                    else if (ln[0] == 'D' && ln[1] == '|')
                    {
                        int b = ln.LastIndexOf('|');
                        if (b > 2) discTag[ln.Substring(2, b - 2)] = ln.Substring(b + 1);
                    }
                }
                tintesPend = false;
            }
            catch { letterTag.Clear(); discTag.Clear(); tintesPend = true; }
        }

        private readonly GuardadoReciente tintesSaveQueue = new GuardadoReciente();
        private long tintesSaveVersion;

        // El color de un disco: el genero que manda dentro de el. Si aun no se sabe, el de siempre.
        private Color ColorDisco(Album a)
        {
            string et;
            if (a != null && a.Folder != null && discTag.TryGetValue(a.Folder, out et)) return ColorDeEtiqueta(et);
            return DefaultAccent;
        }
        private string EtiquetaDisco(Album a)
        {
            string et;
            if (a != null && a.Folder != null && discTag.TryGetValue(a.Folder, out et)) return et;
            return "";
        }

        // ===================== DE QUE GENERO ES UNA CANCION =====================
        // Muchisimos MP3 vienen SIN etiqueta de genero. Antes, a esas canciones se les ensenaba el genero que
        // MANDA en su disco y, en cuanto sonaban, la pastilla cambiaba a "Otros": la lista decia una cosa y el
        // reproductor otra. Eso es lo que se veia en un disco de Black Eyed Peas -todas marcadas "Pop" y al
        // ponerlas, nada-.
        //
        // Ahora hay UNA sola respuesta, la misma en la lista del disco, en el buscador y al sonar, y se busca
        // en este orden, de lo mas seguro a lo mas adivinado:
        //
        //   1. LA ETIQUETA DE LA PROPIA CANCION. Si la trae, no hay nada que adivinar.
        //   2. EL DISCO. El genero que mas se repite entre las canciones del disco que SI la traen. Es la
        //      mejor pista que queda: las canciones de un mismo disco son del mismo genero casi siempre.
        //   3. EL ARTISTA. Si NINGUNA cancion del disco trae etiqueta pero otro disco del mismo artista si,
        //      ese vale. Sale GRATIS: se cuenta sobre los generos de disco que la rocola ya sabe, sin abrir
        //      ni un archivo mas ni guardar nada nuevo.
        //   4. EL NOMBRE. En una rocola de bar media biblioteca esta en carpetas que ya lo dicen: "Cumbias
        //      Mix", "Banda 2020", "Salsa Clasica", "Baladas Romanticas". Se leen el nombre del disco, su
        //      carpeta y la carpeta de encima. Es un recorrido de texto corto, hecho UNA vez por disco y
        //      guardado, asi que no vuelve a costar.
        //
        // Y si despues de los cuatro no hay nada, NO SE INVENTA: no sale pastilla. Mejor sin etiqueta que con
        // una etiqueta falsa.
        private readonly Dictionary<string, string> artistTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> nombreTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // carpeta -> lo que dice su NOMBRE (ya resuelto)

        private string GeneroDeCancion(Track tk, Album org)
        {
            string et;
            if (tk != null && tk.Path != null && songGenre.TryGetValue(tk.Path, out et) && et.Length > 0) return et;   // 1
            if (tk != null && !string.IsNullOrEmpty(tk.Genre)) { et = GenreLabel(tk.Genre); if (et != "Otros") return et; }
            if (org == null) return "";
            if (org.Folder != null && discTag.TryGetValue(org.Folder, out et) && et.Length > 0) return et;              // 2
            if (!string.IsNullOrEmpty(org.Artist) && artistTag.TryGetValue(org.Artist, out et) && et.Length > 0) return et;   // 3
            return GeneroPorNombre(org);                                                                                // 4
        }

        // Que genero se repite mas entre los DISCOS de cada artista. Se rehace cuando llegan votos nuevos del
        // paseo de fondo; cada disco pesa lo que canciones tiene, para que un disco suelto de dos temas no le
        // gane a uno de veinte.
        private void RecalculaGenerosArtista()
        {
            artistTag.Clear();
            if (libBuilding || discTag.Count == 0 || albums == null) return;   // 'albums' es del worker mientras se re-escanea
            Dictionary<string, Dictionary<string, int>> votos = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < albums.Count; i++)
            {
                Album a = albums[i];
                if (a == null || a.Folder == null || string.IsNullOrEmpty(a.Artist)) continue;
                string et;
                if (!discTag.TryGetValue(a.Folder, out et) || string.IsNullOrEmpty(et)) continue;
                int peso = (a.Tracks != null && a.Tracks.Count > 0) ? a.Tracks.Count : 1;
                Dictionary<string, int> vv;
                if (!votos.TryGetValue(a.Artist, out vv)) { vv = new Dictionary<string, int>(); votos[a.Artist] = vv; }
                int ya; vv.TryGetValue(et, out ya); vv[et] = ya + peso;
            }
            foreach (KeyValuePair<string, Dictionary<string, int>> kv in votos)
            {
                string mejor = null; int mejorN = 0;
                foreach (KeyValuePair<string, int> vv in kv.Value) if (vv.Value > mejorN) { mejorN = vv.Value; mejor = vv.Key; }
                if (mejor != null) artistTag[kv.Key] = mejor;
            }
        }

        // Palabras que dentro de un NOMBRE de carpeta o de disco casi solo pueden querer decir el genero. Se
        // comparan como PALABRA ENTERA a proposito: sin eso "Metallica" saldria de Metal, "Discoteca" de Disco
        // y "Poporopo" de Pop. Y quien traduce cada palabra a su etiqueta es GenreLabel, el mismo de siempre:
        // asi no hay dos listas de generos que se puedan desparejar (lo vigila la bateria 'genero').
        private static readonly string[] NombrePal = {
            "cumbia", "cumbias", "salsa", "salsas", "bachata", "bachatas", "merengue", "merengues",
            "reggaeton", "reguetón", "reggaetón", "banda", "bandas", "nortena", "nortenas", "norteña", "norteñas",
            "corrido", "corridos", "ranchera", "rancheras", "mariachi", "mariachis", "grupera", "gruperas",
            "duranguense", "sonidero", "sonideros", "tejano", "tejanas", "regional", "tropical", "tropicales",
            "balada", "baladas", "romantica", "romanticas", "romántica", "románticas", "bolero", "boleros",
            "tango", "tangos", "flamenco", "rumba", "rumbas", "reggae", "ska",
            "rock", "pop", "metal", "punk", "rap", "trap", "hip-hop", "hiphop", "jazz", "blues", "funk", "soul",
            "disco", "tecno", "techno", "trance", "electronica", "electrónica", "dance",
            "clasica", "clásica", "classical", "instrumental", "infantil", "infantiles", "gospel", "country",
            "chill", "oldies", "soundtrack",
            // Lo que se ve de verdad en las carpetas de una rocola de bar y no estaba: sin esto, una carpeta
            // llamada "Karaokes" o "Vallenatos" se quedaba sin genero y por tanto sin color.
            "karaoke", "karaokes", "navidad", "navidenas", "navideñas", "villancicos",
            "vallenato", "vallenatos", "quebradita", "quebraditas", "huapango", "huapangos",
            "sierreno", "sierreño", "zapateado", "guaracha", "guarachas", "mambo", "mambos",
            "danzon", "danzón", "marimba", "grupero", "gruperos", "samba", "sambas", "bossa"
        };

        private string GeneroPorNombre(Album a)
        {
            if (a == null || a.Folder == null) return "";
            string ya;
            if (nombreTag.TryGetValue(a.Folder, out ya)) return ya;
            string et = "";
            try
            {
                // El nombre del disco, su carpeta y la de encima ("...\Cumbias\Los Angeles Azules"). Nada mas
                // arriba: subiendo se llega a carpetas del sistema que no dicen nada de la musica.
                string carp = Path.GetFileName(a.Folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string padre = "";
                try { DirectoryInfo di = Directory.GetParent(a.Folder); if (di != null) padre = di.Name; }
                catch { }
                et = EnNombre(a.Name);
                if (et.Length == 0) et = EnNombre(carp);
                if (et.Length == 0) et = EnNombre(padre);
            }
            catch { }
            nombreTag[a.Folder] = et;
            return et;
        }

        private static string EnNombre(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            string t = texto.ToLowerInvariant();
            for (int i = 0; i < NombrePal.Length; i++)
            {
                if (!PalabraEntera(t, NombrePal[i])) continue;
                string et = GenreLabel(NombrePal[i]);
                if (!string.IsNullOrEmpty(et) && et != "Otros") return et;
            }
            return "";
        }

        // "cumbia" dentro de "Cumbia Mix" SI; dentro de "Sacumbiado" NO. Se comprueba que a los dos lados NO
        // haya letra ni numero (o sea: espacio, guion, punto, barra, parentesis... o el principio/final).
        private static bool PalabraEntera(string t, string pal)
        {
            int p = t.IndexOf(pal, StringComparison.Ordinal);
            while (p >= 0)
            {
                bool izq = (p == 0) || !char.IsLetterOrDigit(t[p - 1]);
                int f = p + pal.Length;
                bool der = (f >= t.Length) || !char.IsLetterOrDigit(t[f]);
                if (izq && der) return true;
                p = t.IndexOf(pal, p + 1, StringComparison.Ordinal);
            }
            return false;
        }

        // Lo que le falta a UN archivo: su duracion, sus votos de genero, o las dos cosas. Sirve para
        // visitarlo una sola vez en vez de dos (ver StartDurScan).
        private sealed class Visita { public string Ruta; public bool Dur; public List<int> Gen; }

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
            // Y LOS VIDEOS. No estaban: 'tracks' solo lleva canciones, los videos viven aparte (VideoFiles de
            // cada disco), asi que nunca les tocaba y en la lista salian sin duracion. Van al final de la
            // tanda, despues de la musica, porque medir un video cuesta bastante mas (ver MideDuracion).
            int cuantasCanciones = pend.Count;
            // Conserva el orden y la comparacion ordinal de List.Contains sin recorrer
            // toda la musica por cada video. Se crea solo al encontrar un candidato.
            HashSet<string> pendientesUnicos = null;
            if (videoAlbums != null)
                for (int i = 0; i < videoAlbums.Count; i++)
                {
                    Album va = videoAlbums[i];
                    if (va == null || va.VideoFiles == null) continue;
                    for (int j = 0; j < va.VideoFiles.Count; j++)
                    {
                        string vp = va.VideoFiles[j];
                        if (vp == null || durCache.ContainsKey(vp) || durFast.ContainsKey(vp)) continue;
                        if (pendientesUnicos == null) pendientesUnicos = new HashSet<string>(pend, StringComparer.Ordinal);
                        if (pendientesUnicos.Add(vp)) pend.Add(vp);   // el mismo video puede estar en dos discos
                    }
                }
            if (pend.Count > cuantasCanciones) Diag("duraciones: " + (pend.Count - cuantasCanciones) + " videos por medir");
            durScanLeft = pend.Count;

            // EL COLOR DE LAS LETRAS. Si hay que calcularlo, se prepara aqui la lista de (letra, archivo) y el
            // paseo de fondo le lee el ID3 a cada uno DESPUES de las duraciones. Se leen con un Track SUELTO,
            // no con el de la biblioteca, para no tocar desde este hilo nada que la pantalla este usando.
            char[] gLet = null; string[] gRuta = null; string[] gDisco = null;
            int firmaLet = FirmaBiblioteca();
            if (tintesPend)
            {
                List<char> cl = new List<char>(tracks.Count);
                List<string> rl = new List<string>(tracks.Count);
                List<string> dl = new List<string>(tracks.Count);
                foreach (KeyValuePair<char, List<Album>> kv in byLetter)
                {
                    List<Album> la = kv.Value; if (la == null) continue;
                    for (int i = 0; i < la.Count; i++)
                    {
                        if (la[i] == null || la[i].Tracks == null) continue;
                        List<Track> tt = la[i].Tracks;
                        string carp = la[i].Folder ?? "";
                        for (int j = 0; j < tt.Count; j++)
                            if (tt[j] != null && tt[j].Path != null) { cl.Add(kv.Key); rl.Add(tt[j].Path); dl.Add(carp); }
                    }
                }
                if (cl.Count > 0) { gLet = cl.ToArray(); gRuta = rl.ToArray(); gDisco = dl.ToArray(); }
            }

            if (pend.Count == 0 && gLet == null) return;
            if (pend.Count > 0) Diag("duraciones: faltan " + pend.Count);
            if (gLet != null) Diag("generos: " + gLet.Length + " por leer (color de las letras)");
            // UNA SOLA PASADA POR EL DISCO. Antes se recorria la biblioteca DOS veces: primero todas las
            // duraciones y despues todos los generos, abriendo cada archivo dos veces y en dos momentos
            // distintos, asi que el cabezal del disco hacia el viaje completo dos veces. Ahora se arma una
            // lista con lo que le falta a cada archivo y se visita UNA sola vez. Y se recorre ORDENADA POR
            // RUTA, que en un disco duro es la diferencia entre avanzar en linea recta y dar saltos: los
            // archivos de una misma carpeta estan juntos en el plato.
            Dictionary<string, Visita> mapa = new Dictionary<string, Visita>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pend.Count; i++)
            {
                Visita v;
                if (!mapa.TryGetValue(pend[i], out v)) { v = new Visita(); v.Ruta = pend[i]; mapa[pend[i]] = v; }
                v.Dur = true;
            }
            if (gLet != null)
                for (int i = 0; i < gLet.Length; i++)
                {
                    Visita v;
                    if (!mapa.TryGetValue(gRuta[i], out v)) { v = new Visita(); v.Ruta = gRuta[i]; mapa[gRuta[i]] = v; }
                    if (v.Gen == null) v.Gen = new List<int>(1);
                    v.Gen.Add(i);   // el mismo archivo puede votar por varias letras: se lee UNA vez y vota todas
                }
            List<Visita> visitas = new List<Visita>(mapa.Count);
            foreach (Visita v in mapa.Values) visitas.Add(v);
            visitas.Sort(delegate (Visita a, Visita b) { return string.CompareOrdinal(a.Ruta, b.Ruta); });
            // El total de duraciones se cuenta sobre las VISITAS, no sobre la lista de entrada: si un mismo
            // archivo estaba dos veces (el mismo video en dos discos, por ejemplo) se visita una sola, y si
            // aqui se pusiera el numero de entradas el contador de "faltan" no llegaria nunca a cero.
            int durTotal = 0;
            for (int i = 0; i < visitas.Count; i++) if (visitas[i].Dur) durTotal++;
            int genTotal = (gLet != null) ? gLet.Length : 0;

            durScanTh = new System.Threading.Thread(delegate ()
            {
                List<string> lote = new List<string>(128);
                List<float> lotd = new List<float>(128);
                object medidorVideo = null;   // el reproductor con el que se miden los videos (de este hilo)
                Dictionary<char, Dictionary<string, int>> votos = new Dictionary<char, Dictionary<string, int>>();
                Dictionary<string, Dictionary<string, int>> votosD = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
                int durHechas = 0, genHechas = 0;
                try
                {
                    System.Threading.Thread.Sleep(4000);   // deja respirar el arranque
                    for (int i = 0; i < visitas.Count; i++)
                    {
                        if (preScanStop || myGen != durScanGen) break;
                        while (!preScanStop && myGen == durScanGen && videoOn) System.Threading.Thread.Sleep(500);
                        if (preScanStop || myGen != durScanGen) break;
                        Visita v = visitas[i];
                        bool caro = false;   // hubo que leer el archivo entero (el respaldo), no solo la cabecera

                        // ---- LA DURACION ----
                        if (v.Dur)
                        {
                            // ¿YA LA MIDIO ALGUIEN MIENTRAS TANTO? Medir una cancion publica TAMBIEN su
                            // duracion, y esta lista se armo al arrancar: sin esta pregunta se vuelve a
                            // abrir la cabecera de un archivo cuya duracion ya esta puesta.
                            float seg = 0f;
                            if (!YaSeMidio(v.Ruta))
                            {
                                if (EsVideo(v.Ruta)) { seg = MideDuracionVideo(v.Ruta, ref medidorVideo); caro = true; }
                                else
                                {
                                    seg = DuracionPorCabecera(v.Ruta);            // unos kilobytes
                                    if (seg <= 0.01f) { seg = MideDuracionAudio(v.Ruta); caro = true; }   // respaldo: el archivo entero
                                }
                            }
                            if (seg > 0.01f) { lote.Add(v.Ruta); lotd.Add(seg); }
                            durHechas++;
                            // Se publica cada 128 archivos o al terminar, TAMBIEN si el lote viene vacio: un
                            // archivo roto no da duracion pero SI es avance; sin esto, el contador de "faltan"
                            // se quedaria clavado para siempre en el numero de archivos danados.
                            if (lote.Count >= 128 || durHechas >= durTotal)
                            {
                                string[] rr = lote.ToArray(); float[] dd = lotd.ToArray();
                                lote.Clear(); lotd.Clear();
                                int faltan = durTotal - durHechas;
                                try
                                {
                                    if (!IsHandleCreated) break;
                                    BeginInvoke((MethodInvoker)delegate { PublicaDuraciones(myGen, rr, dd, faltan); });
                                }
                                catch { break; }
                            }
                        }

                        // ---- EL GENERO, que es lo que da color a cada letra de la barra de arriba ----
                        // Se lee la etiqueta UNA vez por archivo, aunque vote por varias letras. Con un Track
                        // SUELTO, no con el de la biblioteca, para no tocar desde este hilo nada que la pantalla
                        // este usando.
                        if (v.Gen != null)
                        {
                            string et = "Otros";
                            try
                            {
                                string gg = Track.SoloGenero(v.Ruta);   // como mucho 32 KB, no la etiqueta entera
                                if (gg.Length > 0) et = GenreLabel(gg);
                            }
                            catch { }
                            for (int k = 0; k < v.Gen.Count; k++)
                            {
                                int gi = v.Gen[k];
                                Dictionary<string, int> vv2;
                                if (!votos.TryGetValue(gLet[gi], out vv2)) { vv2 = new Dictionary<string, int>(); votos[gLet[gi]] = vv2; }
                                int ya; vv2.TryGetValue(et, out ya); vv2[et] = ya + 1;
                                string carp = gDisco[gi];
                                if (carp.Length > 0)
                                {
                                    Dictionary<string, int> vd;
                                    if (!votosD.TryGetValue(carp, out vd)) { vd = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); votosD[carp] = vd; }
                                    int yd; vd.TryGetValue(et, out yd); vd[et] = yd + 1;
                                }
                                genHechas++;
                            }
                            // Se publica cada 256 votos, y por eso la barra se va tinendo poco a poco en vez de
                            // cambiar de golpe al final.
                            bool finGen = (genHechas >= genTotal);
                            if ((genHechas & 255) < v.Gen.Count || finGen)
                            {
                                // El genero que MANDA en cada letra hasta ahora. "Otros" no gana nunca: una
                                // letra cuyas canciones no traen genero se queda con su blanco de siempre, que
                                // es mas honesto que pintarla del color de "no se sabe".
                                List<char> kl = new List<char>(); List<string> vl = new List<string>();
                                foreach (KeyValuePair<char, Dictionary<string, int>> kv in votos)
                                {
                                    string mejor = null; int mejorN = 0;
                                    foreach (KeyValuePair<string, int> vv in kv.Value)
                                        if (vv.Key != "Otros" && vv.Value > mejorN) { mejorN = vv.Value; mejor = vv.Key; }
                                    if (mejor != null) { kl.Add(kv.Key); vl.Add(mejor); }
                                }
                                // Lo mismo para los discos, que es lo que le da color a los resultados del buscador.
                                List<string> dk = new List<string>(); List<string> dv = new List<string>();
                                foreach (KeyValuePair<string, Dictionary<string, int>> kv in votosD)
                                {
                                    string mejor = null; int mejorN = 0;
                                    foreach (KeyValuePair<string, int> vv in kv.Value)
                                        if (vv.Key != "Otros" && vv.Value > mejorN) { mejorN = vv.Value; mejor = vv.Key; }
                                    if (mejor != null) { dk.Add(kv.Key); dv.Add(mejor); }
                                }
                                char[] ka = kl.ToArray(); string[] va = vl.ToArray(); bool ult = finGen;
                                string[] dka = dk.ToArray(); string[] dva = dv.ToArray();
                                try
                                {
                                    if (!IsHandleCreated) break;
                                    BeginInvoke((MethodInvoker)delegate { PublicaTintes(myGen, ka, va, dka, dva, ult, firmaLet); });
                                }
                                catch { break; }
                            }
                        }

                        // EL RESPIRO va con el trabajo que se acaba de hacer. Leer una cabecera son unos
                        // kilobytes: con 4 ms de pausa el disco queda MUCHO mas libre que antes con 12, porque
                        // antes cada archivo se leia entero. Si hubo que leerlo entero (el respaldo, o un
                        // video), se respira lo de siempre.
                        //
                        // Y EN SSD el respiro se JUNTA. Dormir 1 ms por archivo parece nada, pero en una
                        // biblioteca de cien mil canciones son casi dos minutos de puro dormir, y un SSD no
                        // necesita que se le deje descansar entre lectura y lectura: no hay cabezal que se
                        // recaliente. Se respira una vez cada 32 archivos -lo justo para que la interfaz tenga
                        // su turno, que para eso este hilo va ademas en la prioridad mas baja- y el repaso
                        // termina en una fraccion del tiempo. Si hubo que leer el archivo entero, se respira
                        // igual: eso si le cuesta al disco, tenga cabezal o no.
                        // Y el respiro va por el disco donde este ESTE archivo, no por un veredicto general:
                        // con la musica repartida entre un SSD y un disco duro, cada archivo se trata como
                        // merece el suyo (ver DiscoLento).
                        if (DiscoLento(v.Ruta)) System.Threading.Thread.Sleep(caro ? 12 : 4);
                        else if (caro || (i & 31) == 31) System.Threading.Thread.Sleep(1);
                    }
                }
                catch { }
                finally { SueltaMedidorVideo(ref medidorVideo); }   // el objeto COM se crea y se suelta en ESTE hilo, que es el STA
            });
            durScanTh.IsBackground = true;
            durScanTh.Priority = System.Threading.ThreadPriority.Lowest;
            // El reproductor de Windows es un objeto COM y solo se deja usar desde un hilo STA. Sin esto, la
            // duracion de los videos no se podria medir aqui.
            try { durScanTh.SetApartmentState(System.Threading.ApartmentState.STA); } catch { }
            durScanTh.Start();
        }
        private static bool EsVideo(string ruta)
        {
            if (ruta == null) return false;
            string ext = null;
            try { ext = Path.GetExtension(ruta).ToLowerInvariant(); } catch { }
            return ext != null && Array.IndexOf(VideoExt, ext) >= 0;
        }

        // Cuanto dura un archivo de MUSICA: lo abre, lee la cabecera y lo suelta. No descomprime nada.
        // CUANTO DURA UN MP3 SIN LEERLO ENTERO.
        //
        // Preguntarle la duracion a NAudio obliga a recorrer el archivo COMPLETO: va frame a frame, de
        // principio a fin, armando su indice. Para saber cuanto dura una cancion de 5 MB hay que leer los 5 MB.
        // Con veinte mil canciones eso son CIEN GIGAS de lectura, y en un disco duro mecanico es media hora de
        // cabezal trabajando: por eso el pre-analisis tardaba tanto y por eso molestaba mientras suena musica.
        //
        // Pero la duracion viene escrita en la CABECERA. Casi todos los MP3 traen una etiqueta Xing/Info (o
        // VBRI) con el numero EXACTO de frames, y los de tasa fija se resuelven con una division. En los dos
        // casos basta leer los primeros kilobytes. Si el archivo no trae ninguna de las dos cosas -o algo no
        // cuadra- se devuelve 0 y quien llama recurre al metodo lento de siempre: nunca se pierde una duracion.
        private static readonly int[] BrMpeg1 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
        private static readonly int[] BrMpeg2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };
        private static readonly int[] SrMpeg1 = { 44100, 48000, 32000, 0 };
        private static readonly int[] SrMpeg2 = { 22050, 24000, 16000, 0 };
        private static readonly int[] SrMpeg25 = { 11025, 12000, 8000, 0 };
        private static float DuracionPorCabecera(string ruta)
        {
            try
            {
                if (Path.GetExtension(ruta).ToLowerInvariant() != ".mp3") return 0f;   // solo MP3; el resto, por el camino lento
                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
                {
                    long largo = fs.Length;
                    if (largo < 256) return 0f;
                    byte[] cab = new byte[10];
                    if (fs.Read(cab, 0, 10) != 10) return 0f;
                    // 1) LA ETIQUETA DE TEXTO del principio (ID3v2), que puede llevar dentro la caratula y
                    //    medir cientos de kilobytes. Su tamano viene en la propia cabecera: se salta de golpe.
                    long ini = 0;
                    if (cab[0] == (byte)'I' && cab[1] == (byte)'D' && cab[2] == (byte)'3')
                    {
                        // El tamano va en 4 bytes de 7 bits (el bit alto de cada uno no cuenta).
                        int tam = ((cab[6] & 0x7F) << 21) + ((cab[7] & 0x7F) << 14)
                                + ((cab[8] & 0x7F) << 7) + (cab[9] & 0x7F);
                        ini = 10 + tam;
                        if ((cab[5] & 0x10) != 0) ini += 10;   // con pie de etiqueta, diez bytes mas
                        if (ini <= 0 || ini >= largo) return 0f;
                    }
                    // 2) EL PRIMER FRAME. Se leen unos kilobytes desde ahi y se busca la marca de sincronismo.
                    //    Se tolera basura por delante (hay archivos con relleno entre la etiqueta y el audio).
                    fs.Position = ini;
                    byte[] buf = new byte[8192];
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n < 64) return 0f;
                    for (int i = 0; i + 4 <= n; i++)
                    {
                        if (buf[i] != 0xFF || (buf[i + 1] & 0xE0) != 0xE0) continue;
                        int verBits = (buf[i + 1] >> 3) & 3;      // 3=MPEG1  2=MPEG2  0=MPEG2.5  (1 no existe)
                        int capa = (buf[i + 1] >> 1) & 3;         // 1 = capa III
                        if (verBits == 1 || capa != 1) continue;
                        int iBr = (buf[i + 2] >> 4) & 15, iSr = (buf[i + 2] >> 2) & 3;
                        int pad = (buf[i + 2] >> 1) & 1, modo = (buf[i + 3] >> 6) & 3;
                        bool mpeg1 = (verBits == 3);
                        int br = (mpeg1 ? BrMpeg1[iBr] : BrMpeg2[iBr]) * 1000;
                        int sr = mpeg1 ? SrMpeg1[iSr] : (verBits == 2 ? SrMpeg2[iSr] : SrMpeg25[iSr]);
                        if (br <= 0 || sr <= 0) continue;          // valores reservados: no era un frame
                        int porFrame = mpeg1 ? 1152 : 576;         // muestras que trae cada frame
                        bool mono = (modo == 3);
                        // 3) LA CUENTA EXACTA DE FRAMES, si el archivo la trae. Va justo detras de la
                        //    informacion lateral del primer frame, cuyo tamano depende de version y canales.
                        int lado = mpeg1 ? (mono ? 17 : 32) : (mono ? 9 : 17);
                        int xi = i + 4 + lado;
                        if (xi + 12 <= n)
                        {
                            bool xing = (buf[xi] == (byte)'X' && buf[xi + 1] == (byte)'i' && buf[xi + 2] == (byte)'n' && buf[xi + 3] == (byte)'g')
                                     || (buf[xi] == (byte)'I' && buf[xi + 1] == (byte)'n' && buf[xi + 2] == (byte)'f' && buf[xi + 3] == (byte)'o');
                            if (xing && (buf[xi + 7] & 1) != 0)
                            {
                                long frames = ((long)buf[xi + 8] << 24) + ((long)buf[xi + 9] << 16) + ((long)buf[xi + 10] << 8) + buf[xi + 11];
                                if (frames > 0)
                                {
                                    double seg = (double)frames * porFrame / sr;
                                    if (seg > 0.05 && seg < 36000) return (float)seg;
                                }
                            }
                        }
                        int vi = i + 4 + 32;   // VBRI (Fraunhofer) va siempre a 32 bytes del frame
                        if (vi + 18 <= n && buf[vi] == (byte)'V' && buf[vi + 1] == (byte)'B' && buf[vi + 2] == (byte)'R' && buf[vi + 3] == (byte)'I')
                        {
                            long frames = ((long)buf[vi + 14] << 24) + ((long)buf[vi + 15] << 16) + ((long)buf[vi + 16] << 8) + buf[vi + 17];
                            if (frames > 0)
                            {
                                double seg = (double)frames * porFrame / sr;
                                if (seg > 0.05 && seg < 36000) return (float)seg;
                            }
                        }
                        // 4) SIN ETIQUETA: si el archivo es de tasa FIJA la cuenta es una division. Se comprueba
                        //    que el frame siguiente cae donde debe y trae la misma tasa; si no, puede ser de tasa
                        //    variable sin etiqueta y ahi no se puede adivinar: que lo mida el camino lento.
                        int tamFrame = (mpeg1 ? 144 : 72) * br / sr + pad;
                        if (tamFrame < 24) continue;
                        int sig = i + tamFrame;
                        if (sig + 4 > n) return 0f;
                        if (buf[sig] != 0xFF || (buf[sig + 1] & 0xE0) != 0xE0) return 0f;
                        if (((buf[sig + 2] >> 4) & 15) != iBr) return 0f;   // otra tasa -> variable sin etiqueta
                        long fin = largo;
                        // La etiqueta de texto del FINAL (ID3v1) son 128 bytes que no son audio.
                        if (largo > 128 + ini)
                        {
                            fs.Position = largo - 128;
                            byte[] tres = new byte[3];
                            if (fs.Read(tres, 0, 3) == 3 && tres[0] == (byte)'T' && tres[1] == (byte)'A' && tres[2] == (byte)'G') fin = largo - 128;
                        }
                        long audio = fin - (ini + i);
                        if (audio <= 0) return 0f;
                        double s2 = (double)audio * 8.0 / br;
                        if (s2 > 0.05 && s2 < 36000) return (float)s2;
                        return 0f;
                    }
                }
            }
            catch { }
            return 0f;
        }

        private static float MideDuracionAudio(string ruta)
        {
            // Primero por la cabecera (unos kilobytes). Solo si eso no da nada se lee el archivo entero.
            float rapida = DuracionPorCabecera(ruta);
            if (rapida > 0.01f) return rapida;
            try
            {
                using (LectorMedida rd = new LectorMedida(ruta))
                {
                    double t = rd.TotalTime.TotalSeconds;
                    if (t > 0.05 && t < 36000) return (float)t;
                }
            }
            catch { }
            return 0f;
        }

        // Cuanto dura un VIDEO. NAudio no sabe abrir un mp4 ni un avi, asi que se le pregunta a quien SI sabe
        // y ademas ya esta en la maquina: el reproductor de Windows, el mismo que la rocola usa para poner los
        // videos. Se le pide un objeto SUELTO (no el de la pantalla, que estaria reproduciendo) y se le manda
        // "abre este archivo y dime cuanto dura" -newMedia-, que lee la cabecera SIN reproducir nada.
        //
        // La duracion no siempre esta lista en el mismo instante: el reproductor la va rellenando mientras
        // reconoce el archivo. Por eso se le da un momento, mirando de tanto en tanto, y si en tres segundos no
        // contesta se deja pasar (ese video se queda sin duracion, como estaba antes, y no se cuelga nada).
        // El medidor se crea UNA vez por tanda y lo lleva el propio hilo (por eso va por referencia y no en un
        // campo): crearlo por archivo seria carisimo, y tenerlo en un campo compartido se cruzaria con la tanda
        // siguiente si alguien vuelve a escanear la biblioteca mientras esta corre.
        private float MideDuracionVideo(string ruta, ref object wmpMedidor)
        {
            try
            {
                if (wmpMedidor == null)
                {
                    Type t = Type.GetTypeFromProgID("WMPlayer.OCX.7");
                    if (t == null) t = Type.GetTypeFromProgID("WMPlayer.OCX");
                    if (t == null) return 0f;                       // sin reproductor de Windows: no se puede medir
                    wmpMedidor = Activator.CreateInstance(t);
                    try { Set(wmpMedidor, "uiMode", "none"); } catch { }
                    try { object s2 = Get(wmpMedidor, "settings"); if (s2 != null) { Set(s2, "autoStart", false); Set(s2, "volume", 0); } } catch { }
                }
                object media = Call(wmpMedidor, "newMedia", ruta);
                if (media == null) return 0f;
                try
                {
                    for (int intento = 0; intento < 30; intento++)   // hasta ~3 s, mirando cada 100 ms
                    {
                        double d = 0;
                        try { object o = Get(media, "duration"); if (o != null) d = Convert.ToDouble(o); } catch { }
                        if (d > 0.05 && d < 36000) return (float)d;
                        if (preScanStop) break;
                        // El reproductor puede tardar en rellenar la duracion, y cuando lo hace avisa por la
                        // cola de mensajes de ESTE hilo. Si nadie la vacia, el aviso no llega nunca y todos los
                        // videos se irian por el tope de tres segundos sin duracion. Este hilo no tiene ventanas
                        // propias, asi que vaciarla aqui no puede repintar ni disparar nada de la rocola.
                        try { Application.DoEvents(); } catch { }
                        System.Threading.Thread.Sleep(100);
                    }
                }
                finally { try { System.Runtime.InteropServices.Marshal.ReleaseComObject(media); } catch { } }
            }
            catch { }
            return 0f;
        }
        private static void SueltaMedidorVideo(ref object wmpMedidor)
        {
            object w = wmpMedidor; wmpMedidor = null;
            if (w == null) return;
            try { Call(w, "close"); } catch { }
            try { System.Runtime.InteropServices.Marshal.ReleaseComObject(w); } catch { }
        }

        private const double PreSegSec = 20.0;    // duracion de cada tramo analizado
        private const int PreSegs = 3;            // cuantos tramos por cancion (repartidos por el tema)
        // QUE HAY QUE MEDIR. Devuelve la tanda -las canciones y los videos que aun no tienen medida- y, por
        // "elegibles", cuantos archivos de la biblioteca cuentan para la barra de avance del repaso a fondo.
        // EN QUE ORDEN CONVIENE MEDIR.
        //
        // Medir una cancion cuesta descomprimirla entera: 550 ms por minuto de cancion, unos 2.2 segundos
        // por cada una de cuatro minutos. Una biblioteca de 3000 son casi dos horas de trabajo, y en la
        // maquina del bar bastante mas. Recorrerla EN EL ORDEN EN QUE ESTA significa pasar noches midiendo
        // discos que nadie ha puesto nunca mientras las cinco que suenan cada viernes siguen sin nivelar y
        // sin poder decir los minutos que faltan.
        //
        // El trabajo total es el mismo. Lo que cambia es CUANDO llega el beneficio: primero lo que esta
        // pedido ahora mismo, luego lo mas sonado, luego lo mas reciente, y al final lo demas.
        //
        // Y EL ULTIMO DESEMPATE ES EL SITIO QUE OCUPABA, que no es un detalle: en .NET 3.5 List.Sort no es
        // estable, asi que sin el, dos canciones con los mismos numeros saldrian hoy en un orden y manana
        // en otro y el pre-analisis empezaria cada vez por un sitio distinto, en vez de continuar por
        // donde iba. Pura a proposito: recibe las listas, asi se le pueden dar casos a mano.
        internal static void OrdenaPorUso(List<string> pend, Dictionary<string, int> veces,
                                          Dictionary<string, long> recientes, HashSet<string> enCola)
        {
            if (pend == null || pend.Count < 2) return;
            int n = pend.Count;
            string[] ruta = pend.ToArray();
            int[] idx = new int[n];
            int[] cola = new int[n];
            int[] vez = new int[n];
            long[] rec = new long[n];
            for (int i = 0; i < n; i++)
            {
                idx[i] = i;
                string k = ruta[i];
                cola[i] = (enCola != null && k != null && enCola.Contains(k)) ? 0 : 1;
                int v = 0; if (veces != null && k != null) veces.TryGetValue(k, out v); vez[i] = v;
                long r = 0; if (recientes != null && k != null) recientes.TryGetValue(k, out r); rec[i] = r;
            }
            Array.Sort(idx, delegate (int a, int b)
            {
                if (cola[a] != cola[b]) return cola[a] - cola[b];          // lo pedido ahora mismo, primero
                if (vez[a] != vez[b]) return (vez[b] > vez[a]) ? 1 : -1;   // luego lo mas sonado
                if (rec[a] != rec[b]) return (rec[b] > rec[a]) ? 1 : -1;   // empatadas, la que sono hace menos
                return a - b;                                             // y si todo empata, como estaba
            });
            for (int i = 0; i < n; i++) pend[i] = ruta[idx[i]];
        }

        private List<string> TandaPorMedir(bool turbo, out int elegibles)
        {
            List<string> pend = new List<string>();
            elegibles = 0;
            for (int i = 0; i < tracks.Count; i++)
            {
                Track tk = tracks[i];
                if (tk == null || tk.Path == null) continue;
                // En modo NORMAL se salta lo ya medido. En ENTRENAMIENTO A FONDO entra toda la biblioteca MENOS
                // lo que ya recibio su repaso completo: asi, al detenerlo y volverlo a encender (o tras un apagon)
                // CONTINUA donde iba en vez de empezar por la primera cancion otra vez.
                elegibles++;
                if (!turbo && durCache.ContainsKey(tk.Path) && !NeedsStereoEqRefresh(tk.Path)) continue;
                if (turbo) { bool ya; lock (deepDoneLock) ya = deepDone.Contains(tk.Path); if (ya && !NeedsStereoEqRefresh(tk.Path)) continue; }
                pend.Add(tk.Path);
            }
            // Y LOS VIDEOS. No estaban: 'tracks' solo lleva canciones, los videos viven aparte (VideoFiles de
            // cada disco). Sin medirlos, el nivelado de los videos no tenia NADA con que trabajar y cada uno
            // sonaba con el volumen que trajera de fabrica: unos bien, otros tan bajos que casi no se oian.
            // Van al final de la tanda, despues de la musica, que es lo que mas se usa.
            //
            // Windows XP no puede hacerlo: el sonido de un mp4 lo descomprime Media Foundation, que llego con
            // Vista. Alli los primeros videos fallan al abrirse y el hilo del repaso deja de intentarlo.
            int cuantosVideos = 0;
            if (videoAlbums != null)
            {
                HashSet<string> vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // el mismo video puede estar en dos discos
                for (int i = 0; i < videoAlbums.Count; i++)
                {
                    Album va = videoAlbums[i];
                    if (va == null || va.VideoFiles == null) continue;
                    for (int j = 0; j < va.VideoFiles.Count; j++)
                    {
                        string vp = va.VideoFiles[j];
                        if (vp == null || !vistos.Add(vp)) continue;
                        if (!turbo && loudCache.ContainsKey(vp)) continue;
                        if (turbo) { bool ya; lock (deepDoneLock) ya = deepDone.Contains(vp); if (ya) continue; }
                        pend.Add(vp); cuantosVideos++;
                    }
                }
                elegibles += vistos.Count;
            }
            if (cuantosVideos > 0) Diag("nivelado: " + cuantosVideos + " videos por medir");

            // Y AHORA EL ORDEN, que es donde esta la ganancia (ver OrdenaPorUso). La rocola ya sabe lo que
            // se pone en este bar: lo lleva apuntado para la lista de "Lo mas sonado". Aqui se le pregunta.
            //
            // Las tablas se miran SOLO por las canciones de esta tanda y no al reves: con cien mil canciones
            // apuntadas, copiar las tablas enteras para ordenar unos cuantos miles seria trabajo de mas.
            HashSet<string> enCola = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < queue.Count; i++)
            {
                PlayItem it = queue[i];
                if (it == null) continue;
                string k = it.PathKey();
                if (k.Length > 0) enCola.Add(k);
            }
            Dictionary<string, int> veces = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, long> recientes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pend.Count; i++)
            {
                string k = pend[i];
                if (k == null || veces.ContainsKey(k)) continue;
                int v; long q;
                // Las canciones van en una tabla y los videos en otra, pero aqui vienen revueltos en la
                // misma tanda: se pregunta en las dos.
                if (songCounts.TryGetValue(k, out v) || videoCounts.TryGetValue(k, out v)) veces[k] = v;
                if (songSeq.TryGetValue(k, out q) || videoSeq.TryGetValue(k, out q)) recientes[k] = q;
            }
            OrdenaPorUso(pend, veces, recientes, enCola);
            return pend;
        }

        // ¿ESTA SOLA LA ROCOLA? En modo atraccion (a los 40 s sin que nadie toque un boton), sin nada
        // sonando y sin ningun video puesto. Es el rato en que no hay nadie eligiendo, no hay pared de
        // discos que dibujar -solo el cubo gigante- y no hay musica a la que no se le pueda fallar.
        private bool RocolaSola() { return attractOn && nowPlaying == null && !videoOn; }

        // CUANTOS CARRILES LE TOCAN AL PRE-ANALISIS.
        //
        // Antes usaba UNO siempre, salvo en el repaso a fondo que se enciende a mano: en una maquina de
        // ocho nucleos, un nucleo de ocho, tambien a las cuatro de la manana con el bar cerrado. Ahora
        // tambien se sueltan cuando la rocola esta SOLA.
        //
        // Lo que mas importa de esta cuenta no es que suelte, es QUE FRENE: con alguien usandola devuelve
        // uno SIEMPRE, en cualquier maquina. Si eso fallara, la rocola se pondria lenta justo cuando hay
        // gente delante, y eso seria peor que no haber tocado nada. Pura a proposito, para poder darle los
        // cuatro casos a mano.
        internal static int CarrilesPara(bool turbo, bool sola, bool discoLento, int nucleos, bool minisPorHacer)
        {
            // Disco lento: aqui no manda el procesador, manda el cabezal. Varios hilos pidiendo canciones a
            // la vez lo ponen a saltar y sale mas lento, no mas rapido.
            if (discoLento) return 1;
            if (!turbo && !sola) return 1;      // hay alguien: ni uno mas
            // Y MIENTRAS QUEDEN MINIATURAS, ellas primero. Las dos cosas se sueltan cuando la rocola esta
            // sola, y sueltas a la vez en una maquina de dos nucleos se pelean por el disco y salen las dos
            // peor. La miniatura es lo que SE VE en la pared; el pre-analisis no se ve hasta que suena.
            if (minisPorHacer) return 1;
            if (nucleos < 4) return 1;          // en la de dos nucleos del bar no hay nada que repartir
            int c = nucleos - 1;                // siempre un nucleo libre para la pantalla y la musica
            if (c > 4) c = 4;                   // y de ahi no se pasa: mas no gana y calienta la maquina
            return c;
        }

        // Con cuantos carriles esta corriendo AHORA (0 = no hay nadie corriendo).
        private int preScanCarriles;

        // Al entrar y al salir de la atraccion: si ahora tocan otros carriles, se vuelve a empezar. Y si
        // tocan los mismos NO SE TOCA NADA: sin esto, cualquiera que pase por delante cada 40 segundos
        // reiniciaria el pre-analisis dos veces por paseo y no avanzaria nunca.
        private void RevisaCarriles()
        {
            if (preScanCarriles <= 0) return;   // no hay pre-analisis corriendo
            if (CarrilesPara(preScanTurbo, RocolaSola(), diskSlow, Environment.ProcessorCount, minisPendientes > 0) == preScanCarriles) return;
            StartPreScan(false);
        }

        private void StartPreScan() { StartPreScan(true); }
        private void StartPreScan(bool respiroDeArranque)
        {
            preScanStop = false;
            // Cada llamada (arranque o re-escaneo con F5) abre una GENERACION nueva: el hilo anterior se da
            // por caducado en cuanto lo nota y sale solo. Antes se volvia con "ya hay uno trabajando", asi que
            // la musica AGREGADA despues no se media hasta reiniciar la rocola.
            int myGen = ++preScanGen;
            ReiniciaMedidasHechas();
            bool turbo = preScanTurbo;
            int elegibles;
            List<string> pend = TandaPorMedir(turbo, out elegibles);
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
            // CUANTOS CARRILES. El repaso A FONDO es el trabajo mas largo que hace la rocola -descomprime la
            // biblioteca entera- y hasta ahora lo hacia UN solo hilo: en un equipo de ocho o dieciseis nucleos
            // eso es dejar quieto el 90% de la maquina durante horas.
            //
            // Se reparte solo cuando de verdad conviene, con la misma regla que ya usan las caratulas:
            //   - SOLO a fondo. El repaso normal esta pensado para no notarse y se queda en un carril.
            //   - SOLO en SSD. En un disco duro no manda el procesador, manda el cabezal: varios hilos
            //     pidiendo canciones a la vez lo ponen a saltar y sale mas lento, no mas rapido.
            //   - Siempre dejando un nucleo libre para la pantalla y la musica, y como mucho cuatro: mas
            //     carriles ya no ganan nada y calientan la maquina del bar para nada.
            // En la rocola de dos nucleos esto da 1, o sea exactamente lo de siempre.
            //
            // Y ADEMAS cuando la rocola esta SOLA (ver RocolaSola): en atraccion, sin musica y sin video no
            // hay nadie eligiendo ni nada a lo que fallarle. La cuenta esta aparte y se prueba con casos a
            // mano, porque lo que de verdad importa de ella es que con alguien delante devuelva uno.
            bool aSolas = RocolaSola();
            int carriles = CarrilesPara(turbo, aSolas, diskSlow, Environment.ProcessorCount, minisPendientes > 0);
            preScanCarriles = carriles;
            int siguiente = -1;      // de aqui saca cada carril la siguiente cancion (reparto por demanda: las
            int hechasTot = 0;       // canciones no duran lo mismo, y asi ninguno se queda esperando al otro)
            int vivos = carriles;
            Diag("repaso: " + pend.Count + " archivos en " + carriles + " carril(es)");
            preScanTh = new System.Threading.Thread[carriles];
            for (int carril = 0; carril < carriles; carril++)
            preScanTh[carril] = new System.Threading.Thread(delegate ()
            {
                bool ultimo = false;
                try
                {
                    // El respiro del arranque se paga UNA vez, no en cada revision de carriles: entrar y
                    // salir de la atraccion no es encender la rocola, y esperar 15 s cada vez se comeria
                    // justo el rato que se queria aprovechar.
                    if (!turbo && respiroDeArranque) System.Threading.Thread.Sleep(15000);   // dejar respirar el arranque (y al calentador de caratulas)
                    for (; ; )
                    {
                        int i = System.Threading.Interlocked.Increment(ref siguiente);
                        if (i >= pend.Count) break;
                        if (preScanStop || myGen != preScanGen) break;
                        // En modo normal se espera a que la rocola este en reposo (si alguien la usa, o hay un
                        // video, este hilo no toca nada). En ENTRENAMIENTO A FONDO no espera a nadie: para eso
                        // se enciende, para que la maquina se dedique por completo a dejar todo calibrado.
                        while (!turbo && !preScanStop && myGen == preScanGen && (Environment.TickCount - lastInputTick < 8000 || videoOn))
                            System.Threading.Thread.Sleep(500);
                        if (preScanStop || myGen != preScanGen) break;
                        string ruta = pend[i];
                        // En sistemas anteriores a Vista no existe Media Foundation. En los demas se prueba
                        // CADA video: tres archivos corruptos no deben deshabilitar la medicion de todos los buenos.
                        bool esVid = EsVideo(ruta);
                        if (esVid && videoSinMedir) { System.Threading.Interlocked.Decrement(ref preScanLeft); continue; }
                        // ¿LA ESTA MIDIENDO OTRO, O YA LA MIDIO ALGUIEN? Esta lista se armo al arrancar y
                        // vive horas; mientras tanto el calentador va midiendo lo que la gente pide. Sin
                        // esta pregunta, cada cancion pedida se media DOS VECES (ver ReclamaMedida).
                        if (!ReclamaMedidaEsperando(ruta, turbo, delegate { return preScanStop || myGen != preScanGen; }))
                        {
                            if (preScanStop || myGen != preScanGen) break;
                            System.Threading.Interlocked.Decrement(ref preScanLeft);   // idem que abajo: si no, el contador de 'faltan' se queda clavado
                            continue;
                        }
                        float rms = 0f, dur = 0f; float[] eqCurve = null;
                        bool midio, queued = false;
                        try
                        {
                            midio = MeasureFileResponsive(ruta, turbo, delegate { return preScanStop || myGen != preScanGen; }, out rms, out dur, out eqCurve);
                            if (!midio)
                            {
                                if (preScanStop || myGen != preScanGen) break;
                                System.Threading.Interlocked.Decrement(ref preScanLeft);
                                continue;
                            }
                            // publicar en el hilo de la interfaz (las tablas son suyas): sin carreras
                            try
                            {
                                if (!IsHandleCreated || preScanStop || myGen != preScanGen) break;
                                 BeginInvoke((MethodInvoker)delegate
                                 {
                                     try
                                     {
                                     if (myGen != preScanGen) return;   // llego de una generacion caducada: descartar
                                    PublicaMedida(ruta, rms, dur, eqCurve);
                                    if (turbo) MarkDeepDone(ruta);
                                     if (preScanLeft > 0) System.Threading.Interlocked.Decrement(ref preScanLeft);
                                     }
                                     finally { SueltaMedida(ruta); }
                                 });
                                queued = true;
                            }
                            catch { break; }
                        }
                        finally { if (!queued) SueltaMedida(ruta); }
                        int hechas = System.Threading.Interlocked.Increment(ref hechasTot);
                        if ((hechas % guardaCada) == 0)
                            try { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { SaveLoudCache(); FlushEqIfDirty(); if (iaDoneDirty) { iaDoneDirty = false; SaveIaDone(); } if (deepDoneDirty) { deepDoneDirty = false; SaveDeepDone(); } }); } catch { }
                        // Respiro entre archivos. En ENTRENAMIENTO A FONDO practicamente no hay: la maquina se
                        // dedica a esto. En modo normal, con algo SONANDO se descansa mas (decodificar cuesta CPU
                        // y no vale la pena arriesgar un corte), y la espera se ajusta al equipo: con UN SOLO
                        // nucleo hay que frenar fuerte; con dos o mas sobra maquina y puede ir mas rapido.
                        int respiro;
                        if (turbo || RocolaSola()) respiro = 1;   // sola no hay de quien descansar
                        else if (nowPlaying != null) respiro = (Environment.ProcessorCount >= 2) ? (diskSlow ? 220 : 120) : (diskSlow ? 700 : 400);
                        else respiro = diskSlow ? 120 : 40;
                        System.Threading.Thread.Sleep(respiro);
                    }
                }
                catch { }
                finally
                {
                    // El volcado final lo hace el ULTIMO carril que termina, no todos: si no, con cuatro
                    // carriles se reescribirian las tablas enteras cuatro veces seguidas para nada.
                    ultimo = System.Threading.Interlocked.Decrement(ref vivos) == 0;
                    try { if (ultimo && IsHandleCreated && hechasTot > 0) BeginInvoke((MethodInvoker)delegate { SaveLoudCache(); FlushEqIfDirty(); if (iaDoneDirty) { iaDoneDirty = false; SaveIaDone(); } if (deepDoneDirty) { deepDoneDirty = false; SaveDeepDone(); } }); } catch { }
                }
            });
            for (int carril = 0; carril < carriles; carril++)
            {
                preScanTh[carril].IsBackground = true;
            // NUNCA por delante de la pantalla ni de la musica. A fondo iba en prioridad NORMAL, la misma que la
            // interfaz: en la maquina de dos nucleos ese hilo -que decodifica canciones enteras sin parar- le
            // quitaba el turno al dibujado, y el fondo estrellado se veia ir lento o a tirones. Por debajo de lo
            // normal sigue quedandose con TODO el tiempo que la interfaz no usa (que es casi todo), pero en
            // cuanto hay un cuadro que dibujar, manda el cuadro. En automatico, la minima de todas.
                preScanTh[carril].Priority = turbo ? System.Threading.ThreadPriority.BelowNormal : System.Threading.ThreadPriority.Lowest;
                preScanTh[carril].Start();
            }
        }

        // CUANTO DURA UNA CANCION, Y EL UNICO SITIO QUE LO SABE.
        //
        // Manda la del analisis completo (que midio la cancion entera); si aun no la tiene, la del
        // barrido rapido. Devuelve 0 cuando no se sabe, y eso NO es lo mismo que cero segundos: quien
        // pregunta tiene que mirarlo y callarse en vez de inventar.
        //
        // Y aqui NO se le pregunta al archivo. El descompresor no sabe cuanto dura un MP3 hasta
        // recorrerlo entero cuando no trae cabecera Xing -medido: 3.87 ms de procesador en una cancion
        // de 4 minutos, mas la lectura de sus 9 MB del disco-. Multiplicado por toda la cola, y justo
        // en el instante en que alguien acaba de pulsar un boton. La duracion ya esta apuntada.
        private float SegundosDe(string path)
        {
            float d;
            if (path == null) return 0f;
            if (durCache.TryGetValue(path, out d) && d > 0.01f) return d;
            if (durFast.TryGetValue(path, out d) && d > 0.01f) return d;
            return 0f;
        }

        // Duracion "m:ss" de una cancion ya pre-analizada ("" si aun no se sabe).
        private string DurText(string path)
        {
            float d = SegundosDe(path);
            if (d <= 0f) return "";
            int tot = (int)(d + 0.5f);
            return (tot / 60) + ":" + (tot % 60).ToString("00");
        }

        // ===================== CACHE DE BIBLIOTECA (arranque rapido) =====================
        // El arranque hacia DOS caminatas completas del disco (audio y video) MAS ~una verificacion de
        // portada por cada disco: en disco duro, minutos. Aqui se guarda el resultado en un archivo y
        // los siguientes arranques lo leen en segundos. Se invalida solo si la carpeta de musica cambio
        // (cantidad de carpetas o fecha de la raiz). F5 siempre fuerza un re-escaneo completo.
        private string LibCacheFile() { return RutaDatos("rocola_biblioteca.txt"); }

        // Firma RECURSIVA de una raiz. La fecha de la carpeta superior NO cambia cuando se agrega una cancion
        // dentro de Artista\Album; por eso se incluyen ruta, tamano y fecha de cada archivo y cada directorio.
        private static void RootHash(ref ulong h, string s)
        {
            if (s == null) s = "";
            for (int i = 0; i < s.Length; i++) { h ^= char.ToLowerInvariant(s[i]); h *= 1099511628211UL; }
            h ^= 0xff; h *= 1099511628211UL;
        }
        private static string RootSig(string root)
        { return RootSigCaptured(root, null); }
        private static string RootSigCaptured(string root, ScanDirectoryMemo memo)
        {
            ulong h = 1469598103934665603UL; long filesN = 0, dirsN = 0;
            bool metadataEnumerada = Environment.Version.Major >= 4;
            Stack<string> pendientes = new Stack<string>(); pendientes.Push(Path.GetFullPath(root));
            while (pendientes.Count > 0)
            {
                string dir = pendientes.Pop(); dirsN++;
                long stamp = Directory.GetLastWriteTimeUtc(dir).Ticks;
                RootHash(ref h, dir); RootHash(ref h, stamp.ToString(CultureInfo.InvariantCulture));
                // .NET 4 reutiliza metadatos de la enumeracion. En CLR 2 (.NET 3.5)
                // GetFiles no aporta ese ahorro: conservar el recorrido medido mas rapido.
                FileInfo[] files = null; string[] paths = null; int count;
                string[] captured = null;
                if (metadataEnumerada)
                {
                    files = new DirectoryInfo(dir).GetFiles();
                    if (memo != null && memo.CanCapture(files.Length))
                    {
                        captured = new string[files.Length];
                        for (int i = 0; i < files.Length; i++) captured[i] = files[i].FullName;
                    }
                    Array.Sort(files, delegate(FileInfo a, FileInfo b) { return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name); });
                    count = files.Length;
                }
                else
                {
                    paths = Directory.GetFiles(dir);
                    if (memo != null && memo.CanCapture(paths.Length)) captured = (string[])paths.Clone();
                    Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
                    count = paths.Length;
                }
                for (int i = 0; i < count; i++)
                {
                    FileInfo fi = metadataEnumerada ? files[i] : new FileInfo(paths[i]); filesN++;
                    RootHash(ref h, fi.FullName); RootHash(ref h, fi.Length.ToString(CultureInfo.InvariantCulture));
                    RootHash(ref h, fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
                }
                string[] subs = Directory.GetDirectories(dir);
                if (memo != null && captured != null) memo.Remember(dir, stamp, captured, subs);
                Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
                for (int i = subs.Length - 1; i >= 0; i--)
                    if (!EsAtajo(subs[i]) && !EsCarpetaDelSistema(subs[i])) pendientes.Push(subs[i]);
            }
            return dirsN.ToString(CultureInfo.InvariantCulture) + ":" + filesN.ToString(CultureInfo.InvariantCulture) + ":" + h.ToString("x16");
        }
        private static string RootsSig(List<string> roots)
        {
            StringBuilder ss = new StringBuilder();
            for (int i = 0; i < roots.Count; i++) { if (i > 0) ss.Append('|'); ss.Append(RootSig(roots[i])); }
            return ss.ToString();
        }
        private static string JoinRoots(List<string> roots)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < roots.Count; i++) { if (i > 0) sb.Append('|'); sb.Append(roots[i]); }
            return sb.ToString();
        }

        private readonly object librarySaveLock = new object();
        private readonly GuardadoReciente librarySaveQueue = new GuardadoReciente();
        private long librarySaveVersion;
        // Guarda la biblioteca en segundo plano (la copia de datos es instantanea; la escritura va al pool).
        private void SaveLibraryCacheAsync()
        {
            try
            {
                List<string> roots = (scanRoots != null && scanRoots.Count > 0) ? new List<string>(scanRoots) : AllMusicRoots();
                if (roots.Count == 0) return;
                string firma = lastFreshRootSig;
                // Si el arbol cambio mientras se escaneaba, no hay una firma honesta para esta instantanea.
                if (string.IsNullOrEmpty(firma)) return;
                List<string> ap = new List<string>(tracks.Count);
                for (int i = 0; i < tracks.Count; i++) ap.Add(tracks[i].Path);
                List<string> vp = (lastVideoPaths != null) ? new List<string>(lastVideoPaths) : new List<string>();
                Dictionary<string, string> cv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < albums.Count; i++) if (albums[i].CoverPath != null) cv[albums[i].Folder] = albums[i].CoverPath;
                if (videoAlbums != null) for (int i = 0; i < videoAlbums.Count; i++) if (videoAlbums[i].CoverPath != null) cv[videoAlbums[i].Folder] = videoAlbums[i].CoverPath;
                long version = System.Threading.Interlocked.Increment(ref librarySaveVersion);
                GuardaUltimo(librarySaveQueue, delegate
                {
                    try
                    {
                        Func<bool> vigente = delegate { return version == System.Threading.Interlocked.Read(ref librarySaveVersion); };
                        if (!vigente()) return;
                        lock (librarySaveLock)
                        {
                            EscribeFlujoSeguro(LibCacheFile(), delegate(TextWriter writer)
                            {
                                writer.Write("ROCOLA-LIB v2\r\nroots="); writer.Write(JoinRoots(roots));
                                writer.Write("\r\nsig="); writer.Write(firma); writer.Write("\r\ndirs=0\r\nA");
                                writer.Write(ap.Count); writer.Write("\r\n");
                                for (int i = 0; i < ap.Count; i++)
                                {
                                    if ((i & 127) == 0 && !vigente()) return false;
                                    writer.Write(ap[i]); writer.Write("\r\n");
                                }
                                writer.Write('V'); writer.Write(vp.Count); writer.Write("\r\n");
                                for (int i = 0; i < vp.Count; i++)
                                {
                                    if ((i & 127) == 0 && !vigente()) return false;
                                    writer.Write(vp[i]); writer.Write("\r\n");
                                }
                                writer.Write('C'); writer.Write(cv.Count); writer.Write("\r\n");
                                int n = 0;
                                foreach (KeyValuePair<string, string> kv in cv)
                                {
                                    if ((n++ & 127) == 0 && !vigente()) return false;
                                    writer.Write(kv.Key); writer.Write('|'); writer.Write(kv.Value); writer.Write("\r\n");
                                }
                                return true;
                            }, vigente);
                        }
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
                string[] files = null, subs = null;
                bool reused = scanDirectoryMemo != null && scanDirectoryMemo.Take(dir, out files, out subs);
                if (!reused) try { files = Directory.GetFiles(dir); } catch { }
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
                if (!reused) try { subs = Directory.GetDirectories(dir); } catch { }
                if (subs != null)
                    foreach (string d in subs)
                    {
                        if (EsAtajo(d)) continue;                   // ver EsAtajo: si no, no se sale nunca
                        if (EsCarpetaDelSistema(d)) continue;       // Windows, los programas, la papelera...
                        dirs.Push(d);
                    }
            }
        }

        // CARPETAS DONDE NO PUEDE HABER MUSICA. Si se le dice a la rocola "la musica esta en este disco
        // entero", el recorrido se mete en la carpeta de Windows y en la de los programas, que son decenas
        // de miles de archivos y ni una sola cancion. Medido en la rocola de Linux, apuntando a la raiz: de
        // 40.000 carpetas miradas, el 90% estaban en sitios donde no puede haber musica.
        //
        // QUIEN LO SABE ES EL SISTEMA, cada uno a su manera. En Linux hay respuesta exacta -el sistema dice
        // de que TIPO es cada sitio montado- y por eso alli esto lo contesta Sistema. En Windows hay que ir
        // por el nombre, que son cuatro y bien conocidos. Se compara el nombre de la carpeta en cualquier
        // sitio del disco y no solo en la raiz: nadie llama "Windows" ni "$Recycle.Bin" a una carpeta de
        // musica, y asi tambien vale para un disco que se conecte a otra maquina.
        private static readonly string[] CarpetasDelSistema = {
            "windows", "winnt", "program files", "program files (x86)",
            "archivos de programa", "archivos de programa (x86)", "programdata",
            "$recycle.bin", "recycler", "system volume information", "perflogs",
            "msocache", "config.msi", "documents and settings"
        };
        private static bool EsCarpetaDelSistema(string dir)
        {
            try
            {
                // El ultimo tramo, cortando por las dos barras: la ruta puede venir con cualquiera de las
                // dos, y ademas asi esto se puede comprobar desde una maquina que no sea Windows.
                string t = dir.TrimEnd('\\', '/');
                int c = t.LastIndexOfAny(new char[] { '\\', '/' });
                string n = (c >= 0) ? t.Substring(c + 1) : t;
                // C:\Recovery es del sistema, pero "Recovery" dentro de una biblioteca puede ser un artista
                // o un album perfectamente valido. Solo se excluye cuando cuelga directamente de la unidad.
                if (string.Equals(n, "recovery", StringComparison.OrdinalIgnoreCase))
                {
                    string parent = null, drive = null;
                    try { DirectoryInfo di = Directory.GetParent(t); parent = di != null ? di.FullName.TrimEnd('\\', '/') : null; } catch { }
                    try { drive = Path.GetPathRoot(t); if (drive != null) drive = drive.TrimEnd('\\', '/'); } catch { }
                    if (!string.IsNullOrEmpty(parent) && string.Equals(parent, drive, StringComparison.OrdinalIgnoreCase)) return true;
                    return false;
                }
                for (int i = 0; i < CarpetasDelSistema.Length; i++)
                    if (string.Equals(n, CarpetasDelSistema[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        // UNA CARPETA QUE EN REALIDAD ES UN ATAJO A OTRO SITIO. No se sigue, y no es capricho: en un disco de
        // verdad los hay que dan vueltas -"/usr/bin/X11" apunta a si mismo, "/proc/<numero>/root" lleva de
        // vuelta a la raiz- y el recorrido no sale de ahi JAMAS. Medido antes de esto, apuntando al disco
        // entero: 40.000 carpetas y 401.023 archivos en 19 segundos sin terminar; y en la prueba con dos
        // atajos, 77.741 canciones encontradas donde solo habia dos. Es lo mismo que hace cualquier buscador
        // de archivos del sistema.
        //
        // La carpeta que se ELIGIO si se mira aunque sea un atajo -si la eligieron, la quieren-: esto solo
        // decide si se BAJA a los atajos que haya dentro.
        //
        // Y NO SE PIERDE NADA POR EL CAMINO, esta comprobado archivo por archivo: barriendo el disco entero
        // de esta maquina, antes salian 1.657 rutas y ahora 1.582, pero los NOMBRES distintos son 102 en los
        // dos casos. Los 75 de diferencia eran el mismo archivo contado dos veces -"/lib" es un atajo a
        // "/usr/lib", asi que todo lo de dentro salia por partida doble-, que es exactamente la duplicacion
        // que todo esto existe para evitar.
        private static bool EsAtajo(string dir)
        {
            try { return (new DirectoryInfo(dir).Attributes & FileAttributes.ReparsePoint) != 0; }
            catch { return false; }
        }

        private static readonly char[] SepChars = { '\\', '/' };

        private void BuildAlbums(List<string> roots)
        {
            string[] folderMemo = new string[2];
            Dictionary<string, Album> byFolder = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tracks.Count; i++)
            {
                Track t = tracks[i];
                string folder = AlbumFolderCached(t.Path, roots, folderMemo);
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

        // Solo listas selectivas y completas, en el orden original. Como maximo
        // 28 * 8192 referencias; las letras comunes siguen usando el recorrido normal.
        private List<SearchItem>[] searchPostings;

        private void BuildSearchIndex()
        {
            ClearRecentQueries();
            lastQuery = null; lastMatches = null;
            if (bolsaLetra != null) bolsaLetra.Clear();
            searchPostings = null;
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
                    ar.Kind = 0; ar.Display = a.Artist; ar.AlbumRef = a;
                    artistAlbums.TryGetValue(a.Artist, out ar.Albums);
                    searchIndex.Add(ar);
                }
                SearchItem alb = new SearchItem();
                alb.Kind = 1; alb.Display = a.Artist + " - " + a.Name; alb.AlbumRef = a;
                searchIndex.Add(alb);
                for (int j = 0; j < a.Tracks.Count; j++)
                {
                    Track t = a.Tracks[j];
                    SearchItem so = new SearchItem();
                    so.Kind = 2; so.Display = t.Artist + " - " + t.Title; so.TrackRef = t; so.AlbumRef = a;
                    searchIndex.Add(so);
                }
            }
            // La parte cara -limpiar acentos y sacar la marca de letras- es la misma cuenta repetida una vez
            // por entrada, y cada entrada solo depende de su propio texto: se puede repartir. Va aparte del
            // bucle de arriba a proposito, para que el indice quede armado y en orden ANTES de repartir.
            ClavesDelIndice();
            BuildSearchPostings();
        }

        // LAS CLAVES DEL BUSCADOR, REPARTIDAS ENTRE LOS NUCLEOS.
        //
        // Cada entrada mira solo su propio texto y escribe solo dentro de si misma, asi que no hay nada
        // compartido que proteger: ni candados ni esperas. Se reparte por TRAMOS y no de una en una porque
        // el trabajo de cada entrada es minusculo y ponerse de acuerdo costaria mas que hacerlo.
        //
        // Reservar margen para interfaz y musica, tambien al reescanear durante una reproduccion.
        // En 2C/2T se usa solo el worker actual. Por debajo de 8000 entradas no compensa repartir.
        //
        // Al volver de los Join ya no queda nadie tocando el indice, y ademas Join deja a la vista de este
        // hilo todo lo que escribieron los otros: de aqui en adelante el indice se lee como siempre.
        internal static int IndexWorkers(int count, int processors)
        { return count < 8000 ? 1 : CargaTrabajo.Lectores(processors); }
        private void ClavesDelIndice()
        {
            int n = searchIndex.Count;
            int hilos = IndexWorkers(n, CargaTrabajo.Procesadores);
            if (hilos <= 1) { TramoDeClaves(0, n); return; }

            int trozo = (n + hilos - 1) / hilos;
            System.Threading.Thread[] th = new System.Threading.Thread[hilos - 1];
            for (int k = 1; k < hilos; k++)
            {
                int desde = k * trozo;
                int hasta = Math.Min(n, desde + trozo);
                th[k - 1] = new System.Threading.Thread(delegate () { TramoDeClaves(desde, hasta); });
                th[k - 1].IsBackground = true;
                th[k - 1].Priority = System.Threading.ThreadPriority.BelowNormal;
                th[k - 1].Start();
            }
            TramoDeClaves(0, Math.Min(n, trozo));   // el hilo que reparte tambien lleva su tramo
            for (int k = 0; k < th.Length; k++) th[k].Join();
        }

        private void TramoDeClaves(int desde, int hasta)
        {
            for (int i = desde; i < hasta; i++)
            {
                SearchItem si = searchIndex[i];
                si.Key = Normalize(si.Display);
                si.Mask = MarcaLetras(si.Key);
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

            // Cambio de pagina/letra: retirar peticiones que aun no empezaron, conservando las
            // lecturas en vuelo y su generacion de contenido. La pagina visible se pide enseguida;
            // el asentamiento (timer) queda solo para la prelectura de las paginas vecinas.
            bool newPage = false;
            string psig = currentLetter + "|" + page;
            if (psig != lastPageSig)
            {
                lastPageSig = psig; PrepareCoverPage();
                newPage = true;
                coversOnHold = true;   // sigue frenando las peticiones nacidas del PINTADO durante el hojeo
                if (coverSettleTimer != null) { coverSettleTimer.Stop(); coverSettleTimer.Start(); }
            }

            List<Album> pageList = new List<Album>();
            int start = page * PageSize;
            for (int i = start; i < start + PageSize && i < list.Count; i++) pageList.Add(list[i]);

            if (newPage)
                for (int i = 0; i < pageList.Count; i++) RequestCover(pageList[i], false, true);   // fila ALTA, ya mismo
#if ROCOLA_GPU
            if (newPage && gpuEnabled) PrimeNextPage(list, page);
#endif

            albumView.Page = pageList;
            // La pagina de AL LADO se va horneando mientras nadie toca nada (ver BakeAhead): al pasar de pagina
            // las casillas ya estan listas y el deslizamiento se ve fluido LA PRIMERA VEZ, no solo al volver.
            // Tambien en el ordenador del bar (sin tarjeta grafica), que es justo donde re-escalar 18 caratulas
            // en el momento de pulsar mas se nota.
            List<Album> siguiente = new List<Album>();
            for (int i = start + PageSize; i < start + PageSize * 2 && i < list.Count; i++) siguiente.Add(list[i]);
#if ROCOLA_GPU
            if (gpuEnabled)
            {
                if (siguiente.Count == 0) siguiente = NextGpuPage(list, page);
                for (int i = Math.Max(0, start - PageSize); i < start && i < list.Count; i++) siguiente.Add(list[i]);
            }
#endif
            albumView.SetAhead(siguiente);
            albumView.Selected = selAlbumIndex - start;
            albumView.SelAngle = 0;
            albumView.CancelHandoff();   // cambio de pagina/letra: cancela cualquier giro de relevo en curso
            albumView.Invalidate();

            headerView.Section = (currentLetter == VideoMark) ? "VIDEOS" : (currentLetter == TopMark) ? "LO MAS SONADO" : ("LETRA " + currentLetter);
            headerView.Page = page; headerView.Total = totalPages;
            headerView.Accent = AcentoSeccion();
            headerView.Invalidate();
            Letter_Refresh();
        }

        // SINCRONO: para portadas sueltas (cubo grande del disco, mini-cubo, buscador). Pocas a la vez.
        // SOLO MEMORIA: si la caratula no esta en cache, la ENCARGA en segundo plano y devuelve null;
        // al llegar se aplica sola (ApplyArrivedCover). Antes decodificaba AQUI MISMO, en el hilo de la
        // interfaz: en disco duro eso congelaba la pantalla al entrar a un disco, al cambiar de cancion
        // y al pintar el buscador (100-500ms por caratula). Ese era el grueso de los tirones.
        // Devuelve la caratula SOLO si ya esta en memoria. No encarga nada al disco. La usa el buscador
        // mientras el cliente barre resultados con la flecha (ver Portada en SearchOverlay).
        private Image PortadaEnMemoria(Album a)
        {
            if (a == null || a.CoverPath == null) return null;
            lock (coverLock) { Image c; if (CoverEnCache(a.CoverPath, true, out c)) return c; }
            return null;
        }

        private Image LoadCover(Album a)
        {
            if (a == null || a.CoverPath == null) return null;
            lock (coverLock) { Image c; if (CoverEnCache(a.CoverPath, true, out c)) return c; }
            RequestCover(a, false);   // encargar SIN el freno de hojeo (esto no es la rejilla)
            return null;
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
                colorPortada.Remove(key);   // pudo recargarse una imagen distinta con la misma ruta
                if (albumView != null) albumView.RefreshCover(key);
                if (panelSongs != null && panelSongs.Visible && currentAlbum != null && cubeBox != null)
                {
                    if (currentAlbum.IsTop && currentAlbum.TrackCovers != null && songSel >= 0 && songSel < currentAlbum.TrackCovers.Count && currentAlbum.TrackCovers[songSel] == key)
                    { cubeBox.Cover = LoadCoverByPath(key); cubeBox.Invalidate(); }
                    else if (!currentAlbum.IsTop && currentAlbum.CoverPath == key)
                    { cubeBox.Cover = LoadCover(currentAlbum); cubeBox.Invalidate(); }
                }
                if (miniCube != null && miniCube.Visible && nowPlaying != null && nowPlaying.Disc != null && nowPlaying.Disc.CoverPath == key)
                {
                    miniCube.Cover = LoadCover(nowPlaying.Disc); miniCube.Invalidate();
                }
                if (nowPlaying != null && nowPlaying.Disc != null &&
                    string.Equals(nowPlaying.Disc.CoverPath, key, StringComparison.OrdinalIgnoreCase))
                {
                    // Y EL FONDO. La portada de la que suena puede llegar DESPUES de que empiece la
                    // cancion -se decodifica en otro hilo-, y entonces el color se calculo sin ella.
                    // Aqui ya esta: se vuelve a preguntar, y si ahora dice otra cosa, el fondo viaja
                    // hasta el color nuevo con el mismo fundido lento de siempre (bgAccentTarget).
                    if (fondoPortadaOn && !nowPlaying.IsVideo)
                    {
                        // Los picos pueden cambiar aunque el promedio siga siendo el mismo.
                        ApplyGenreBackground(nowPlaying.Song, nowPlaying.Disc);
                    }
                }
            }
            catch { }
        }

        private Image LoadCoverAsync(Album a) { return RequestCover(a, true, true); }

        private Image RequestCover(Album a, bool respectHold) { return RequestCover(a, respectHold, true); }

        // Carga una caratula por RUTA directa (cache o encargo). Usado por los discos TOP (ciclado de
        // portadas y cambio de portada al navegar canciones).
        private Image LoadCoverByPath(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            lock (coverLock) { Image c; if (CoverEnCache(key, true, out c)) return c; }
            RequestCoverByPath(key, true);   // la pide el cubo visible, no el precalentador
            return null;
        }

        private void RequestCoverByPath(string key, bool highPriority)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (coverLock)
            {
                Image c;
                if (CoverEnCache(key, highPriority, out c)) return;
                if (CoverYaSolicitada(key, highPriority)) return;
                if (CoverRetryPendiente(key)) return;
                CoverJob j = new CoverJob(); j.Key = key; j.Gen = coverGen;
                coverLoading.Add(key, j);
                j.Node = highPriority ? coverQHi.AddLast(j) : coverQLo.AddLast(j);
                if (coverPumpCount < coverCarriles)
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
                if (CoverEnCache(key, highPriority, out c)) return c;
                if (respectHold && coversOnHold) return null;   // hojeando rapido: todavia no pedir
                if (CoverYaSolicitada(key, highPriority)) return null;
                if (CoverRetryPendiente(key)) return null;
                CoverJob j = new CoverJob(); j.Key = key; j.Gen = coverGen;
                coverLoading.Add(key, j);
                j.Node = highPriority ? coverQHi.AddLast(j) : coverQLo.AddLast(j);
                if (coverPumpCount < coverCarriles)   // SSD: un carril por nucleo (hasta 4); HDD: carril unico (cabezal)
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
        private static ImageCodecInfo jpegEnc;
        private static int BarreMiniaturas()
        {
            try
            {
                string d = CarpetaMiniaturas();
                if (d == null || !Directory.Exists(d)) return 0;
                string[] files = Directory.GetFiles(d, "*.jpg");
                long total = 0;
                List<string> viejas = new List<string>();
                DateTime corte = DateTime.UtcNow.AddDays(-MiniaturasDiasMax);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(files[i]);
                        total += fi.Length;
                        if (fi.LastWriteTimeUtc < corte) viejas.Add(files[i]);
                    }
                    catch { }
                }
                if (total <= (long)MiniaturasTopeMb * 1024L * 1024L) return 0;
                int fuera = 0;
                for (int i = 0; i < viejas.Count; i++) { try { File.Delete(viejas[i]); fuera++; } catch { } }
                return fuera;
            }
            catch { return 0; }
        }

        private static ulong Fnv64(string s)
        {
            ulong hash = 1469598103934665603UL;
            for (int i = 0; i < s.Length; i++) { hash ^= s[i]; hash *= 1099511628211UL; }
            return hash;
        }

        private static void SaveThumb(Bitmap thumb, string cacheFile)
        {
            string tmp = cacheFile + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                lock (CandadoMiniatura(cacheFile))
                {
                    if (jpegEnc == null)
                        foreach (ImageCodecInfo ci in ImageCodecInfo.GetImageEncoders()) if (ci.MimeType == "image/jpeg") { jpegEnc = ci; break; }
                    if (jpegEnc == null) thumb.Save(tmp, ImageFormat.Jpeg);
                    else
                        using (EncoderParameters ep = new EncoderParameters(1))
                        {
                            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 82L);
                            thumb.Save(tmp, jpegEnc, ep);
                        }
                    if (File.Exists(cacheFile))
                    {
                        try { File.Replace(tmp, cacheFile, null, true); tmp = null; return; }
                        catch { try { File.Delete(cacheFile); } catch { } }
                    }
                    File.Move(tmp, cacheFile); tmp = null;
                }
            }
            catch { }
            finally { if (tmp != null) try { File.Delete(tmp); } catch { } }
        }

        private void CachePut(string key, Image img)
        {
            Image previous;
            if (coverCache.TryGetValue(key, out previous))
            {
                coverCache[key] = img;
                if (previous != null && !object.ReferenceEquals(previous, img)) coverTrash.Add(previous);
                return;   // reemplazar no equivale a un nuevo uso visible
            }
            coverCache.Add(key, img);
            coverNodes.Add(key, coverOrder.AddLast(key));
            while (coverOrder.Count > TopeDePortadas())
            {
                string old = coverOrder.First.Value;
                coverOrder.RemoveFirst(); coverNodes.Remove(old);
                Image oldImg;
                if (coverCache.TryGetValue(old, out oldImg)) { coverCache.Remove(old); if (oldImg != null) coverTrash.Add(oldImg); }
            }
        }

        // Libera las carátulas expulsadas. Se llama desde el temporizador (mismo hilo que el dibujo,
        // asi que NUNCA hay un DrawImage en curso). No libera las que sigan en uso por el cubo del disco,
        // el mini-cubo (incluido su relevo), la cola o la animacion de disco.
        // Esas las recoge el GC cuando dejen de usarse, sin mantener otra lista creciente.
        private void DrainCoverTrash()
        {
            List<Image> dump = null;
            lock (coverLock)
            {
                if (coverTrash.Count == 0) return;
                dump = new List<Image>(coverTrash);
                coverTrash.Clear();
            }
#if ROCOLA_GPU
            Image pin3 = transImg;   // la animacion de disco la esta dibujando AHORA: liberarla revienta el render
#else
            Image pin3 = null;
#endif
            for (int i = 0; i < dump.Count; i++)
            {
                Image im = dump[i];
                if (im == null) continue;
                if (im == pin3 || (miniCube != null && miniCube.UsaPortada(im))
                    || (cubeBox != null && cubeBox.UsaPortada(im))) continue;
                bool enCola = false;
                if (pastCola != null && pastCola.Portadas != null)
                    for (int j = 0; j < pastCola.Portadas.Length; j++)
                        if (object.ReferenceEquals(im, pastCola.Portadas[j])) { enCola = true; break; }
                if (enCola) continue;   // una capsula puede volver a dibujar esta imagen en otro cuadro
                try { im.Dispose(); } catch { }
            }
        }

        private void SetSelectedLocal(int local, int dir)
        {
            albumView.MoveSelectionTo(local, dir);   // mueve con efecto de relevo (el anterior gira y se apaga)
            // Y SE REPINTA SOLO LO QUE CAMBIA: las dos casillas, no la pared entera. Medido con
            // pruebas/usando.cs a 1280x1024: 1.75 ms la pared entera contra 0.28 ms las dos casillas, o sea
            // que se repintaba el 92% de la pantalla para nada. Con el freno de flechas son hasta 25
            // pulsaciones por segundo, y ese trabajo se lo estaba quitando al sonido.
            //
            // El cambio de PAGINA y el de LETRA siguen repintando todo, que ahi si cambian las 18 casillas.
            Rectangle r = albumView.RectSeleccion();
            if (r.Width > 0 && r.Height > 0) albumView.Invalidate(r); else albumView.Invalidate();
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
            string order = LetrasOrden;
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

        // LA PORTADA QUE VUELA A LA COLA.
        //
        // Al poner una cancion, su portada sale de la pared y aterriza en la pastilla de la cola. Sin una
        // sola palabra, el cliente ve QUE paso y DONDE quedo su cancion: es la mejor señal posible para
        // alguien que no va a leer un letrero.
        //
        // Y NO SE ESCRIBE UNA ANIMACION NUEVA. La rocola ya sabia llevar una portada de un rectangulo a
        // otro girando -es la del disco que se abre-, asi que esto es un SEGUNDO USO de esa misma pieza
        // con otros dos extremos. De regalo nace apagable: obedece al interruptor de "animacion" que ya
        // existia, porque quien se planta es StartDiscTrans en su primera linea.
        private void VuelaALaCola(Album a)
        {
#if ROCOLA_GPU
            if (pastCola == null || !pastCola.Visible || pastCola.Height < 8) return;
            double ang; RectangleF desde = SelectedDiscRect(out ang);
            // El destino es un cuadrado del alto de la pastilla, centrado en ella: la portada llega, se
            // encoge hasta el tamaño de la capsula y ahi se acaba.
            float lado = pastCola.Height;
            RectangleF hasta = new RectangleF(pastCola.Left + (pastCola.Width - lado) / 2f, pastCola.Top, lado, lado);
            StartDiscTrans(desde, hasta, ang, ang + Math.PI, (a != null) ? RequestCover(a, false, true) : null);
#endif
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
            songList.OlvidaPagina();   // disco nuevo: se llega a su pagina 1, no se hojea desde el disco anterior
            if (songPager != null) songPager.PonSinAnimar(0);
            FillSongPage();
            cubeBox.Cover = LoadCover(a);
            UpdateTopSongVisuals();   // disco TOP: portada/etiqueta de la cancion seleccionada
            cubeBox.Angle = 0;
            albumView.Visible = false;
            letterBar.Visible = false;
            headerView.Visible = false;
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

        private void BackToAlbums()
        {
#if ROCOLA_GPU
            Album saliendo = currentAlbum;   // para el viaje de vuelta (se usa mas abajo)
            double angDesde; RectangleF desde = DiscCubeRect(out angDesde);   // el cubo GRANDE del disco, antes de cerrarlo
#endif
            panelSongs.Visible = false;
            albumView.Visible = true;
            letterBar.Visible = true;
            headerView.Visible = true;
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
            // HOJEO ANIMADO: si la pagina cambia y el disco tiene mas de una, la que dejas sale por un lado y
            // la que eliges entra por el otro. Se avisa ANTES de mover PageStart, que es cuando lo horneado
            // todavia es la pagina que se va.
            int arranque = pg * rpp;
            if (pages > 1 && songList.PageStart != arranque && panelSongs != null && panelSongs.Visible)
                songList.StartSlide(arranque > songList.PageStart ? 1 : -1);
            songList.PageStart = arranque; songList.Sel = songSel; songList.Accent = bgAccent;
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
            // La rocola se queda sola: si ahora le tocan mas carriles para medir la biblioteca, se los toma.
            // Va lo PRIMERO, antes de armar la pantalla de atraccion, para no perder ese rato.
            RevisaCarriles();
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
            // Y AQUI ES DONDE FRENA, que es lo que de verdad importa: alguien acaba de tocar un boton y el
            // pre-analisis vuelve a su unico carril antes de que se note nada en la pantalla.
            RevisaCarriles();
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

        // ---------- CUANDO SE POSA LA CONSULTA ----------
        //
        // Mientras las letras estan entrando nadie esta leyendo los resultados, asi que moverlos en cada
        // una es trabajo tirado: 2.80 ms de rehacer y subir la pantalla completa a 1920, mas 3.07 ms de
        // rehornear las 40 tarjetas, POR LETRA. Se espera a que la persona pare.
        //
        // 220 ms. Se probaron 150 y se quedaba corto: a cinco letras por segundo -que es teclear normal en
        // un teclado de verdad- cada letra caia fuera de la espera y no se ahorraba nada. Con 220 entran los
        // dos ritmos rapidos y sigue siendo menos de lo que se tarda en leer una palabra. Y en la rocola del
        // bar, donde las letras se eligen con las flechas y tardan casi un segundo, esto no cambia nada: se
        // posa en cada letra, como siempre.
        internal const int PosarMs = 220;
        private int tecleoUltimo;                       // cuando entro la ultima letra
        private System.Windows.Forms.Timer posaTimer;

        // ¿Se posa YA, o se espera a que pare? Tras un rato sin teclear -abrir el buscador y escribir la
        // primera letra- se posa en el acto: si esa primera tardara, se notaria y pareceria que la rocola
        // no hace caso. Pura, para poder darle casos a mano.
        internal static bool PosarYa(int msDesdeLaUltimaTecla) { return msDesdeLaUltimaTecla >= PosarMs; }

        private void AlTeclearBusqueda()
        {
            UpdateSecretCredits();
            int ahora = Environment.TickCount;
            bool ya = PosarYa(ahora - tecleoUltimo);
            tecleoUltimo = ahora;
            if (posaTimer != null) posaTimer.Stop();
            if (ya) { if (searchOverlay != null) searchOverlay.PosaConsulta(); return; }
            if (posaTimer != null) posaTimer.Start();
        }

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
            if (posaTimer != null) posaTimer.Stop();   // el buscador se va: no queda consulta que posar
            searchOpen = false;
            UpdateSecretCredits();
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

        // MARCA DE LETRAS. Un numero de 32 bits que dice QUE letras contiene un texto. Con el se descarta
        // de un plumazo, SIN mirarle el texto, toda entrada a la que le falte alguna letra de lo tecleado:
        // si al cliente le falta una sola de sus letras, esa cancion no puede casar ni entera ni por
        // palabras. Comparar dos numeros cuesta nada; recorrer el titulo tres veces, no.
        // Bits 0..25 = letras a..z ; bit 26 = alguna cifra ; bit 27 = cualquier otro caracter. Los espacios
        // no cuentan. Meter todo lo raro en un mismo bit es a proposito: puede dejar pasar candidatos de
        // mas -que luego descarta Score-, pero nunca puede tirar uno bueno.
        private static int MarcaLetras(string s)
        {
            int m = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ') continue;
                if (c >= 'a' && c <= 'z') m |= 1 << (c - 'a');
                else if (c >= '0' && c <= '9') m |= 1 << 26;
                else m |= 1 << 27;
            }
            return m;
        }

        // OPTIMIZACION (busqueda): q.Split(' ') se calculaba DENTRO de Score, que corre por CADA
        // entrada de searchIndex (decenas de miles) en CADA tecla. Ahora se recibe ya calculado
        // (una sola vez por tecla desde RecomputeResults). El puntaje es identico.
        private static int Score(string cand, string q, string[] toks)
        {
            if (q.Length == 0) return 0;
            if (cand == q) return 10000;
            if (cand.StartsWith(q, StringComparison.Ordinal)) return 6000 - Math.Min(cand.Length, 2000);
            // UNA sola pasada por el texto. Antes se recorria DOS veces: una buscando " q" (empieza palabra)
            // y otra buscando "q" (en cualquier sitio). Para los que NO casan -que son la inmensa mayoria del
            // indice- las dos recorrian el texto ENTERO antes de rendirse. Ahora se busca "q" una vez y se
            // mira si la letra de delante era un espacio, que es lo mismo que buscar " q".
            int primera = -1;
            int pos = cand.IndexOf(q, StringComparison.Ordinal);
            while (pos >= 0)
            {
                if (primera < 0) primera = pos;
                if (pos > 0 && cand[pos - 1] == ' ') return 4000 - (pos - 1);
                pos = cand.IndexOf(q, pos + 1, StringComparison.Ordinal);
            }
            if (primera >= 0) return 2000 - Math.Min(primera, 1500);
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
            if (SecretCreditGate.Matches(query)) { UpdateSecretCredits(); return; }
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
                string[] toks = (q.IndexOf(' ') >= 0) ? q.Split(' ') : null;
                int qMask = MarcaLetras(q);
                // Incremental: si la nueva busqueda EMPIEZA con la anterior, las coincidencias nuevas
                // son un subconjunto de las anteriores -> re-evaluamos solo esas (mucho menos trabajo).
                List<SearchItem> source =
                    (lastQuery != null && lastQuery.Length > 0 && lastMatches != null && q.StartsWith(lastQuery, StringComparison.Ordinal))
                    ? lastMatches : searchIndex;

                // LA PRIMERA LETRA ES LA CARA. Con una sola letra tecleada, la marca de letras no descarta a
                // nadie -casi todas las canciones tienen una "a"- asi que hay que puntuar el indice ENTERO:
                // 3.2 ms de 20.000 entradas, contra 0.07 ms cuando ya hay tres letras. Y es la que mas se
                // nota, porque es la que abre cada busqueda.
                //
                // Como para una letra suelta el resultado es SIEMPRE el mismo mientras no cambie la musica, se
                // guarda: la primera vez que alguien teclea una "a" cuesta lo de siempre, y todas las demas
                // veces de la noche sale de memoria (medido: 10 veces menos, ver pruebas/bolsa.cs). Se guardan
                // tambien los puntos, porque el orden depende de ellos y cada busqueda los pisa.
                //
                // Cabe de sobra: con 8.000 canciones, la bolsa de la letra que mas casa son 96 KB.
                List<SearchItem> matches = null;
                BolsaLetra bolsa = null;
                if ((q.Length == 1 && bolsaLetra.TryGetValue(q[0], out bolsa) && bolsa != null) ||
                    (q.Length > 1 && (bolsa = FindRecentQuery(q)) != null))
                {
                    matches = bolsa.Lista;
                    for (int i = 0; i < matches.Count; i++) matches[i].Score = bolsa.Puntos[i];   // los puntos, que otra busqueda los piso
                }
                else
                {
                    matches = new List<SearchItem>();
                    source = SearchCandidates(source, qMask);
                    int cuantos = source.Count;
                    for (int i = 0; i < cuantos; i++)
                    {
                        SearchItem si = source[i];
                        if ((si.Mask & qMask) != qMask) continue;   // le falta alguna letra: imposible que case
                        int sc = Score(si.Key, q, toks);
                        if (sc > 0) { si.Score = sc; matches.Add(si); }
                    }
                    if (q.Length == 1 || (q.Length <= 128 && matches.Count <= 32768))
                    {
                        BolsaLetra nb = new BolsaLetra();
                        nb.Lista = matches; nb.Puntos = new int[matches.Count];
                        for (int i = 0; i < matches.Count; i++) nb.Puntos[i] = matches[i].Score;
                        if (q.Length == 1) bolsaLetra[q[0]] = nb;
                        else RememberQuery(q, nb);
                        bolsa = nb;
                    }
                }
                // De todo lo que casa solo se ENSENAN 40. Ordenar la lista entera para tirar el resto era el
                // gasto grande del buscador: con veinte mil canciones y una sola letra tecleada casan veinte
                // mil, y ordenarlas costaba 13 ms POR TECLA. Quedarse con los 40 mejores de una pasada cuesta
                // 0.19 ms. La lista sin ordenar se guarda igual, porque para acotar la busqueda de la tecla
                // siguiente lo unico que importa es QUIENES casan, no en que orden.
                lastMatches = matches; lastQuery = q;
                // Los puntos de TODAS las coincidencias ya fueron restaurados arriba:
                // la tecla siguiente sigue teniendo su lista completa para acotar.
                if (bolsa != null && bolsa.Mejores != null) res.AddRange(bolsa.Mejores);
                else
                {
                    TomaMejores(matches, res, 40);
                    if (bolsa != null) bolsa.Mejores = new List<SearchItem>(res);
                }
            }
            else { lastMatches = null; lastQuery = null; }
            searchOverlay.Results = res;
            searchOverlay.Invalidate();
        }

        // El orden en que se ensenan los resultados: primero el que mejor casa, a igualdad antes el artista
        // que el disco y antes el disco que la cancion, y a igualdad de todo, por nombre.
        private static int ComparaHit(SearchItem a, SearchItem b)
        {
            if (b.Score != a.Score) return b.Score - a.Score;
            if (a.Kind != b.Kind) return a.Kind - b.Kind;
            return string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase);
        }

        // Los N mejores de una lista, SIN ordenar el resto. Se recorre una vez manteniendo una lista corta
        // ordenada: cada candidato solo se compara con el peor de los que ya estan dentro, y solo si lo mejora
        // se le hace sitio. Da exactamente los mismos N primeros que ordenarlo todo.
        private static void TomaMejores(List<SearchItem> src, List<SearchItem> dst, int n)
        {
            int cnt = src.Count;
            if (cnt <= 0 || n <= 0) return;
            if (n > cnt) n = cnt;
            SearchItem[] top = new SearchItem[n];
            int hay = 0;
            for (int i = 0; i < cnt; i++)
            {
                SearchItem v = src[i];
                if (hay < n)
                {
                    int j = hay++;
                    while (j > 0 && ComparaHit(top[j - 1], v) > 0) { top[j] = top[j - 1]; j--; }
                    top[j] = v;
                }
                else if (ComparaHit(v, top[n - 1]) < 0)
                {
                    int j = n - 1;
                    while (j > 0 && ComparaHit(top[j - 1], v) > 0) { top[j] = top[j - 1]; j--; }
                    top[j] = v;
                }
            }
            for (int i = 0; i < hay; i++) dst.Add(top[i]);
        }

        private void ChooseSearchResult(SearchItem it)
        {
            if (it == null) return;
            MarkSearchActivity();
            if (it.Kind == 3) { GrantSecretCredits(it); return; }
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
    }
}

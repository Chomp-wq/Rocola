using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
namespace Rocola
{
    // Un trabajador y una solicitud pendiente (la mas reciente). Nunca aborta codecs nativos.
    internal sealed class AperturaEnFondo<T> where T : class, IDisposable
    {
        private sealed class Request
        {
            internal long Version;
            internal Func<T> Open;
            internal Action<T, Exception> Deliver;
            internal Action<Action> Post;
        }
        private sealed class Result { internal Request Request; internal T Value; internal Exception Error; }
        private readonly object gate = new object();
        private long version;
        private bool running;
        private Request pending;
        private Result ready;
        private static void Release(T value) { if (value != null) try { value.Dispose(); } catch { } }
        // Siempre bajo gate: una referencia solo puede transferirse o liberarse una vez.
        private T DetachReady() { T value = ready == null ? null : ready.Value; ready = null; return value; }
        internal void Cancel()
        {
            T old;
            lock (gate) { version++; pending = null; old = DetachReady(); }
            Release(old);
        }
        internal void Start(Func<T> open, Action<T, Exception> deliver, Action<Action> post)
        {
            if (open == null || deliver == null || post == null) throw new ArgumentNullException();
            T old; bool launch;
            lock (gate)
            {
                version++; old = DetachReady();
                pending = new Request { Version = version, Open = open, Deliver = deliver, Post = post };
                launch = !running; if (launch) running = true;
            }
            Release(old);
            if (launch)
            {
                try
                {
                    Thread worker = new Thread(PrioridadHilo.Con(Run));   // EN LINUX: (new Thread(X)) con la prioridad que XP le pone antes de Start: en Linux .NET no la aplica (Nativo/PrioridadHilo.cs)
                    worker.IsBackground = true;
                    PrioridadHilo.Pon(worker, ThreadPriority.BelowNormal);   // EN LINUX: (worker.Priority = ...) la prioridad, apuntada para ponerla al arrancar (con NativeAOT, .NET no la devuelve: Nativo/PrioridadHilo.cs)
                    // Linux: no hay COM ni apartamentos (en XP, STA para abrir el audio); SetApartmentState lanzaria PlatformNotSupportedException
                    worker.Start();
                }
                catch { lock (gate) { running = false; pending = null; } throw; }
            }
        }
        private void Run()
        {
            while (true)
            {
                Request request;
                lock (gate)
                {
                    request = pending; pending = null;
                    if (request == null) { running = false; return; }
                }
                Result result = new Result { Request = request };
                try { result.Value = request.Open(); }
                catch (Exception error) { result.Error = error; }
                bool current;
                lock (gate)
                {
                    current = request.Version == version;
                    if (current) ready = result;
                }
                if (!current) { Release(result.Value); continue; }
                try { request.Post(delegate { Deliver(result); }); }
                catch
                {
                    T abandoned = null;
                    lock (gate) { if (ReferenceEquals(ready, result)) abandoned = DetachReady(); }
                    Release(abandoned);
                }
            }
        }
        private void Deliver(Result result)
        {
            T value;
            lock (gate)
            {
                if (!ReferenceEquals(ready, result) || result.Request.Version != version) return;
                value = DetachReady(); // el llamador asume propiedad; el worker ya no toca el lector
            }
            result.Request.Deliver(value, result.Error);
        }
    }

    // ==============================================================================================
    //  MIRAR POR DELANTE: el lector de la que viene, abierto antes de que haga falta.
    //
    //  Desde el 17/09 abrir ya no para la pantalla: lo hace un trabajador aparte. Pero SIGUE estando en
    //  el camino hasta el primer sonido, porque la cancion no empieza hasta que su lector esta listo.
    //  Entre cancion y cancion, eso es silencio.
    //
    //  Aqui se guarda UN lector y la ruta para la que vale. Si cuando toca sonar piden esa misma ruta, se
    //  entrega y no se abre nada. Si piden otra -la cola cambio, o la version mejorada de Apollo termino
    //  mientras tanto- lo guardado se suelta y se abre como siempre. LA RUTA TIENE QUE COINCIDIR: un
    //  desajuste cuesta una apertura, nunca un archivo por otro.
    //
    //  Un fallo al abrir por delante NO se anota ni devuelve creditos: esa cancion todavia no es la que
    //  suena. Se tira y ya; cuando le toque, el camino normal la abre y ahi el error se trata como siempre.
    // ==============================================================================================
    internal sealed class MiraPorDelante<T> where T : class, IDisposable
    {
        private readonly AperturaEnFondo<T> apertura = new AperturaEnFondo<T>();
        private readonly object gate = new object();
        private long version;
        private string para;   // para que ruta vale lo que hay (o lo que se esta abriendo)
        private T listo;       // ya abierto, esperando su turno
        internal Func<string, string> Signature;
        private string firma;
        internal string Para { get { lock (gate) return para; } }
        internal bool Listo { get { lock (gate) return listo != null; } }
        // ¿Lo ya abierto para 'ruta' cumple 'condicion'? Sin entregarlo: el relevo sin pausa mira su formato
        // antes de llevarselo, y si no le sirve lo deja para el camino de siempre.
        internal bool Cumple(string ruta, Func<T, bool> condicion)
        {
            lock (gate)
                return listo != null && condicion != null && string.Equals(ruta, para, StringComparison.OrdinalIgnoreCase) && condicion(listo);
        }
        private static void Release(T value) { if (value != null) try { value.Dispose(); } catch { } }

        internal void Suelta()
        {
            T old;
            lock (gate) { version++; para = null; firma = null; old = listo; listo = null; }
            apertura.Cancel();
            Release(old);
        }

        internal void Pide(string ruta, Func<T> abre, Action<Action> post)
        {
            if (ruta == null || abre == null || post == null) { Suelta(); return; }
            string stamp = Signature == null ? "" : Signature(ruta);
            if (stamp == null) { Suelta(); return; }
            lock (gate) { if (string.Equals(ruta, para, StringComparison.OrdinalIgnoreCase) && stamp == firma) return; }
            Suelta();
            long mia;
            lock (gate) { para = ruta; firma = stamp; mia = version; }
            apertura.Start(abre, delegate(T value, Exception error)
            {
                bool unchanged = Signature == null || stamp == Signature(ruta);
                bool guardar;
                lock (gate) { guardar = (unchanged && error == null && value != null && mia == version && listo == null); if (guardar) listo = value; else if (mia == version) { para = null; firma = null; } }
                if (!guardar) Release(value);
            }, post);
        }

        internal T Toma(string ruta)
        {
            string stamp = Signature == null ? "" : Signature(ruta);
            T mio = null, sobra = null;
            lock (gate)
            {
                version++;
                if (listo != null && stamp != null && stamp == firma && string.Equals(ruta, para, StringComparison.OrdinalIgnoreCase)) mio = listo;
                else sobra = listo;
                listo = null; para = null; firma = null;
            }
            apertura.Cancel();   // lo que se estuviera abriendo ya no vale: esta sonando otra cosa
            Release(sobra);
            return mio;
        }
    }

    public partial class Form1
    {
        private AperturaEnFondo<IAudioSource> audioOpening;
        private MiraPorDelante<IAudioSource> lookahead;
        private IAudioSource audioReady; // propiedad explicita mientras BeginInvoke espera; cerrar tambien la libera
        private long audioStartVersion;
        private bool audioOpeningForMeasurement;
        // El medidor es una operacion explicita y sincronica: necesita la cadena lista
        // antes de medir. No debe dejar una apertura pendiente ni cobrar/devolver creditos.
        private void PlayTrackForMeasurement(Track track)
        {
            audioOpeningForMeasurement = true;
            try { PlayTrack(track); }
            finally { audioOpeningForMeasurement = false; }
        }
        private void BeginAudioOpen(string path, Action<IAudioSource, Exception> completed)
        {
            bitacoraCancion = Environment.TickCount;   // (bitacora: MedidasBitacora.cs)
            if (audioOpeningForMeasurement)
            {
                IAudioSource source = null; Exception error = null;
                try { source = OpenAudio(path); } catch (Exception ex) { error = ex; }
                completed(source, error);
                return;
            }
            // ¿ESTA YA ABIERTA? Se pide por la ruta EXACTA. Si el que espera es otro archivo, se suelta
            // ahi mismo y se abre como siempre.
            IAudioSource yaAbierto = (lookahead != null) ? lookahead.Toma(path) : null;
            warmSongPath = null; unchecked { warmSongGen++; } // tambien permite precargar dos pedidos consecutivos iguales
            if (yaAbierto != null)
            {
                // Se entrega por el MISMO camino que una apertura de verdad -un salto al hilo de la
                // pantalla- para que el orden de lo que pasa despues no cambie por venir ya abierta.
                CancelAudioOpen();
                audioReady = yaAbierto;
                long mio = audioStartVersion;
                try
                {
                    PantallaRocola.Encola(this, delegate   // EN LINUX: un encargo al hilo de la pantalla nativa (en XP, BeginInvoke: PostMessage a la ventana de la rocola)
                    {
                        if (!object.ReferenceEquals(audioReady, yaAbierto)) return;
                        audioReady = null;
                        if (audioStartVersion != mio || IsDisposed || Disposing) { try { yaAbierto.Dispose(); } catch { } return; }
                        completed(yaAbierto, null);
                    });
                }
                catch { CancelAudioOpen(); throw; }
                return;
            }
            if (audioOpening == null) audioOpening = new AperturaEnFondo<IAudioSource>();
            audioStartVersion++;
            audioOpening.Start(delegate { return OpenAudio(path); }, completed,
                delegate(Action action)
                {
                    if (IsDisposed || Disposing || !IsHandleCreated) throw new ObjectDisposedException("Rocola");
                    PantallaRocola.Encola(this, delegate { action(); });   // EN LINUX: un encargo al hilo de la pantalla nativa (en XP, BeginInvoke: PostMessage a la ventana de la rocola)
                });
        }
        // QUE ARCHIVO SUENA DE VERDAD. En esta rocola, el mismo que pide la cola: aqui no hay IA en vivo
        // -Apollo es solo de la rama de Windows potente- asi que nunca suena una version mejorada en vez
        // del original. Existe igual, y con el mismo nombre que alla, porque quien mira por delante tiene
        // que preguntarlo SIEMPRE: el dia que esta rocola tenga otra fuente, se cambia aqui y en un solo
        // sitio. Lo que no puede hacer nunca es tener efectos secundarios: esta cancion todavia no suena.
        private string RutaQueSonaria(string path) { return path; }
        private string RutaOriginalDe(string ruta) { return ruta; }   // sin Apollo, el archivo que suena es la cancion
        private void PideLookahead(string songPath)
        {
            // El medidor de Ctrl+F3 abre a la cara y no debe dejar nada pendiente: alli no se mira por delante.
            if (audioOpeningForMeasurement || songPath == null) { SueltaLookahead(); return; }
            string ruta = RutaQueSonaria(songPath);
            if (ruta == null) { SueltaLookahead(); return; }
            if (lookahead == null) { lookahead = new MiraPorDelante<IAudioSource>(); lookahead.Signature = AudioFileSignature; }
            try { lookahead.Pide(ruta, delegate { return OpenAudio(ruta); },
                delegate(Action action)
                {
                    if (IsDisposed || Disposing || !IsHandleCreated) throw new ObjectDisposedException("Rocola");
                    PantallaRocola.Encola(this, delegate { action(); });   // EN LINUX: un encargo al hilo de la pantalla nativa (en XP, BeginInvoke: PostMessage a la ventana de la rocola)
                }); }
            catch { SueltaLookahead(); } // precarga opcional: un fallo al crear el hilo no corta la UI
        }
        private void SueltaLookahead() { if (lookahead != null) lookahead.Suelta(); SueltaEnMemoria(); }   // (y lo leido por adelantado)
        // LAS CANCIONES DANADAS (tercera tanda, 12-10-2026). Una cancion cuyo ARCHIVO esta roto (formato, cortado,
        // cabecera mala) fallaba cada vez que alguien la ponia: entraba en la cola, al llegarle el turno no sonaba y se
        // devolvia el credito. Ahora, despues del primer fallo, al pedirla se dice al momento que esta danada y no se cobra.
        // Solo en esta sesion, y solo mientras el archivo sea el mismo (tamano y fechas): si el operador lo cambia, se
        // vuelve a poder pedir. Un disco desconectado o que no deja leer NO la marca: al volver el disco, suena.
        internal const string AvisoDanada = "Esa canci\u00f3n est\u00e1 da\u00f1ada: elige otra";
        private readonly Dictionary<string, string> danadas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal static bool EsArchivoDanado(Exception ex)
        {
            if (ex == null || ex is IOException && !(ex is EndOfStreamException)) return false;   // el disco, no el archivo
            if (ex is UnauthorizedAccessException || ex is System.Runtime.InteropServices.COMException || ex is OutOfMemoryException) return false;
            if (ex is ObjectDisposedException) return false;   // un lector ya cerrado (saltar, cerrar): es de la rocola, no del archivo
            return ex is EndOfStreamException || ex is FormatException || ex is InvalidDataException
                || ex is InvalidOperationException || ex is NotSupportedException || ex is ArgumentException;
        }
        private void MarcaDanada(string path, Exception ex)
        {
            if (string.IsNullOrEmpty(path) || !EsArchivoDanado(ex)) return;
            string firma = AudioFileSignature(path);
            if (firma == null) return;   // si ni existe, es el disco: no se marca
            if (danadas.Count >= 4096) danadas.Clear();   // tope: nunca crece sin limite
            danadas[path] = firma;
        }
        private bool EstaDanada(string path)
        {
            string antes;
            if (string.IsNullOrEmpty(path) || danadas.Count == 0 || !danadas.TryGetValue(path, out antes)) return false;
            if (antes == AudioFileSignature(path)) return true;
            danadas.Remove(path);   // cambio el archivo (o ya no esta): que se intente de nuevo
            return false;
        }

        internal static string AudioFileSignature(string path)
        {
            try
            {
                System.IO.FileInfo info = new System.IO.FileInfo(path);
                if (!info.Exists) return null;
                return info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                    info.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                    info.CreationTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return null; } // no conservar un lector cuya identidad no podemos revalidar
        }
        private void CancelAudioOpen()
        {
            audioStartVersion++;
            IAudioSource ready = audioReady; audioReady = null;
            if (ready != null) try { ready.Dispose(); } catch { }
            if (audioOpening != null) audioOpening.Cancel();
        }
        private void AudioStartFailed(Track t, Exception ex)
        {
            if (audioOpeningForMeasurement)
            {
                LogError("Audio del medidor", ex);
                DisposePlaybackNow();
                return;
            }
            // La cancion MUDA del diagnostico completo no es de nadie: si falla (tambien en el arranque diferido) ni se devuelve un credito, ni se marca
            // danada, ni se pasa a la cola. Se para y el diagnostico se queda sin musica propia.
            if (EsMudaDiag(nowPlaying)) { DisposePlaybackNow(); return; }
            EstadoPasajero("No se pudo reproducir: " + MotivoFallo(ex) + "  (" + System.IO.Path.GetFileName(t.Path) + ")", EstadoPasajeroMs);   // el motivo primero: un nombre largo se corta por la orilla y se llevaria el motivo; el detalle, en rocola_error.txt
            MarcaDanada(t.Path, ex);   // si es el archivo (no el disco), no se vuelve a dejar poner hasta que lo cambien
            LogError("PlayTrack " + System.IO.Path.GetFileName(t.Path), ex);
            NoteIncompatible(t.Path, ex);
            RefundCredit();
            ShowToast("No se pudo reproducir: credito devuelto", System.Drawing.Color.FromArgb(200, 80, 70));
            DisposePlaybackNow();
            long failedVersion = audioStartVersion;
            try
            {
                if (IsHandleCreated) PantallaRocola.Encola(this, delegate {   // EN LINUX: un encargo al hilo de la pantalla nativa (en XP, BeginInvoke: PostMessage a la ventana de la rocola)
                    if (audioStartVersion == failedVersion && !IsDisposed && !Disposing) PlayNext();
                });
            }
            catch { }
        }
    }
}

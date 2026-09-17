using System;
using System.Threading;
using System.Windows.Forms;
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
                    Thread worker = new Thread(Run);
                    worker.IsBackground = true;
                    worker.Priority = ThreadPriority.BelowNormal;
                    worker.SetApartmentState(ApartmentState.STA);
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

    public partial class Form1
    {
        private AperturaEnFondo<IAudioSource> audioOpening;
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
            if (audioOpeningForMeasurement)
            {
                IAudioSource source = null; Exception error = null;
                try { source = OpenAudio(path); } catch (Exception ex) { error = ex; }
                completed(source, error);
                return;
            }
            if (audioOpening == null) audioOpening = new AperturaEnFondo<IAudioSource>();
            audioStartVersion++;
            audioOpening.Start(delegate { return OpenAudio(path); }, completed,
                delegate(Action action)
                {
                    if (IsDisposed || Disposing || !IsHandleCreated) throw new ObjectDisposedException("Rocola");
                    BeginInvoke((MethodInvoker)delegate { action(); });
                });
        }
        private void CancelAudioOpen()
        {
            audioStartVersion++;
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
            lblStatus.Text = "No se pudo reproducir " + System.IO.Path.GetFileName(t.Path) + " (" + ex.Message + ")";
            LogError("PlayTrack " + System.IO.Path.GetFileName(t.Path), ex);
            NoteIncompatible(t.Path, ex);
            RefundCredit();
            ShowToast("No se pudo reproducir: credito devuelto", System.Drawing.Color.FromArgb(200, 80, 70));
            DisposePlaybackNow();
            long failedVersion = audioStartVersion;
            try
            {
                if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate {
                    if (audioStartVersion == failedVersion && !IsDisposed && !Disposing) PlayNext();
                });
            }
            catch { }
        }
    }
}

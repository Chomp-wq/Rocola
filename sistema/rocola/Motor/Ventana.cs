// LO QUE EN XP HACE LA VENTANA O WINDOWS Y LA CADENA NECESITA, en su forma de Rocola OS (sin ventana, en Linux). Lo que viene tal cual de XP
// lo dice; lo demas es propio de aqui.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        // LA CARPETA DE LOS DATOS: ROCOLA_DATOS si se puede escribir en ella (como en XP), si no /rocola/datos (la particion de los datos de
        // Rocola OS) y, si tampoco, la carpeta del programa. Se decide una vez.
        private static volatile string carpetaDatos;
        private static readonly object carpetaDatosCerrojo = new object();
        internal static string CarpetaDatos
        {
            get
            {
                string ya = carpetaDatos;
                if (ya != null) return ya;
                lock (carpetaDatosCerrojo)
                {
                    if (carpetaDatos != null) return carpetaDatos;
                    string d = null;
                    try { d = Environment.GetEnvironmentVariable("ROCOLA_DATOS"); } catch { }
                    foreach (string c in new string[] { string.IsNullOrEmpty(d) ? null : d.Trim(), "/rocola/datos" })
                        if (c != null && SePuedeEscribir(c)) { carpetaDatos = c; return c; }
                    carpetaDatos = AppContext.BaseDirectory;
                    return carpetaDatos;
                }
            }
        }
        private static bool SePuedeEscribir(string d)
        {
            try
            {
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                string prueba = Path.Combine(d, "rocola_escribible_" + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream sonda = new FileStream(prueba, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) sonda.WriteByte(1);
                return true;
            }
            catch { return false; }
        }

        // Que lo escrito llegue al disco (en XP, FlushFileBuffers de kernel32). En Linux el asa del archivo es su descriptor: fsync.
        [DllImport("libc", SetLastError = true)] private static extern int fsync(int fd);
        private static bool FlushFileBuffers(IntPtr asa) { return fsync((int)(long)asa) == 0; }

        // Lo que suena (en XP lo pone la cola de la ventana). El reproductor de Rocola OS lo pone al tocar.
        private PlayItem nowPlaying;
        // La lectura de la biblioteca en marcha (PlanBiblioteca.cs); en Rocola OS aun no hay biblioteca: siempre null.
        private LecturaBiblioteca lecturaPlan;
        // La pantalla: aun no hay. Lo que en XP refresca la lista o los videos no hace nada.
        private void InvalidateVideoMeasurements() { }
        private sealed class PanelSinPantalla { internal bool Visible; }
        private readonly PanelSinPantalla panelSongs = null;
        private void RefrescaLista() { }
        // Leer por delante la siguiente de la cola (AperturaAudio.cs, para el disco duro de XP): en Rocola OS aun no.
        private sealed class SinMirarPorDelante { internal string Para; internal void Suelta() { } }
        private readonly SinMirarPorDelante lookahead = null;
        private void PideLookahead(string songPath) { }

        // De cuando es el programa que corre (en XP, la fecha de Rocola.exe).
        internal static string DeCuandoEs()
        {
            try
            {
                string exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return "";
                return File.GetLastWriteTime(exe).ToString("dd/MM/yyyy HH:mm");
            }
            catch { return ""; }
        }

        // ---- tal cual de XP ----
        // de AperturaAudio.cs
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
        // de Form1.cs
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
        // de PlanBiblioteca.cs
        // Copias baratas hechas por la pantalla; los indices y las ordenaciones se hacen en el trabajador.
        private sealed class LecturaBiblioteca : IDisposable
        {
            internal List<Track> Tracks;
            internal List<Album> Videos;
            internal List<KeyValuePair<char, List<Album>>> Letras;
            internal List<KeyValuePair<string, float>> DurDatos, RapidasDatos, LoudDatos;
            internal List<KeyValuePair<string, float[]>> EqDatos;
            internal List<KeyValuePair<string, SonoridadIntegrada>> IntegDatos;
            internal List<KeyValuePair<string, int>> CancionesDatos, VideosDatos;
            internal List<KeyValuePair<string, long>> SecDatos, VideoSecDatos;
            internal List<string> DeepDatos, ColaDatos;
            internal Dictionary<string, float> Dur, Rapidas, Loud;
            internal Dictionary<string, float[]> Eq;
            internal Dictionary<string, SonoridadIntegrada> Integ;
            internal Dictionary<string, int> Canciones, VideoCounts;
            internal Dictionary<string, long> Sec, VideoSec;
            internal HashSet<string> Deep, Cola;
            internal bool Tintes;
            internal int Firma, LoudCount;
            // Los dos planes iniciales comparten solamente datos privados e inmutables.
            // Cancelacion y tablas de analisis adicionales siguen siendo independientes.
            internal LecturaBiblioteca BaseCompartida;
            private bool basePreparada;
            private readonly ManualResetEvent cancelada = new ManualResetEvent(false);
            private int cerrada;
            internal volatile bool Cancelada;
            internal void Cancela() { Cancelada = true; try { cancelada.Set(); } catch (ObjectDisposedException) { } }
            internal bool Espera(int ms)
            {
                if (Cancelada) return false;
                try { return !cancelada.WaitOne(ms, false); } catch (ObjectDisposedException) { return false; }
            }
            internal void Prepara()
            {
                LecturaBiblioteca origen = BaseCompartida ?? this;
                origen.PreparaBase(); Dur = origen.Dur; Rapidas = origen.Rapidas;
                Loud = Indice(LoudDatos);
                Eq = Indice(EqDatos); Integ = Indice(IntegDatos);
                Canciones = Indice(CancionesDatos); VideoCounts = Indice(VideosDatos);
                Sec = Indice(SecDatos); VideoSec = Indice(VideoSecDatos);
                Deep = new HashSet<string>(DeepDatos, StringComparer.OrdinalIgnoreCase);
                Cola = new HashSet<string>(ColaDatos, StringComparer.OrdinalIgnoreCase);
                DurDatos = null; RapidasDatos = null; LoudDatos = null; EqDatos = null; IntegDatos = null;
                CancionesDatos = null; VideosDatos = null; SecDatos = null; VideoSecDatos = null;
                DeepDatos = null; ColaDatos = null;
            }
            private void PreparaBase()
            {
                lock (this)
                {
                    if (basePreparada) return;
                    Dur = Indice(DurDatos); Rapidas = Indice(RapidasDatos);
                    DurDatos = null; RapidasDatos = null; basePreparada = true;
                }
            }
            private static Dictionary<string, T> Indice<T>(List<KeyValuePair<string, T>> filas)
            {
                if (filas == null) return new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, T> r = new Dictionary<string, T>(filas.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < filas.Count; i++) r[filas[i].Key] = filas[i].Value;
                return r;
            }
            public void Dispose() { if (Interlocked.Exchange(ref cerrada, 1) == 0) cancelada.Close(); }
        }
    }

    // de MedidasBitacora.cs
    // ============================== LA BITACORA ESCRIBE DETRAS DE LA PANTALLA ==============================
    //
    // rocola_diagnostico.txt (con "diagnostico=1"), rocola_error.txt y rocola_incompatibles.txt se escribian en el hilo que
    // apuntaba, muchas veces el de la pantalla: abrir el archivo, mirar cuanto mide y pegarle la linea. Con el diagnostico
    // encendido son varias lineas por cancion, y en un disco mecanico ocupado cada una puede tardar decenas de milisegundos:
    // la bitacora que sirve para cazar los tirones los provocaba. Ahora quien apunta deja la linea -con su hora de ahora-
    // en una cola y sigue; un hilo de fondo las escribe en el mismo orden, de a tandas. Lo que dice cada archivo, su tope de
    // tamano y el sello de version no cambian (el sello lo saca el hilo que escribe: mirar la fecha del programa tambien es
    // ir al disco). Si el archivo esta ocupado un momento -un antivirus- se reintenta en vez de perder la linea. Al cerrar y
    // ante un fallo que puede terminar el proceso se escribe todo ahi mismo (Vacia).
    internal static class Bitacora
    {
        private sealed class Apunte { public string Ruta, Cabeza, Cuerpo; public long Tope; public bool ConSello; }
        private static readonly object cerrojoCola = new object();    // la cola
        private static readonly object cerrojoDisco = new object();   // quien escribe: uno a la vez
        private static readonly Queue<Apunte> cola = new Queue<Apunte>();
        private static bool trabajando;          // hay un hilo de fondo escribiendo o por empezar
        private static int descartados;          // lo que no cupo en la cola
        private const int MaximoEnCola = 4000;   // si el disco no da abasto, la cola no crece sin limite
        private static string sello;             // "  (rocola del ...)", sacado una vez en el hilo que escribe

        // Una linea tal cual (el diagnostico). Tope: por encima de ese tamano el archivo empieza de nuevo (0: sin tope).
        internal static void Apunta(string ruta, long tope, string texto) { Mete(ruta, tope, texto, null, false); }
        // Un apunte de error: la cabeza, el sello de version y el cuerpo, como siempre.
        internal static void ApuntaConSello(string ruta, long tope, string cabeza, string cuerpo) { Mete(ruta, tope, cabeza, cuerpo, true); }

        private static void Mete(string ruta, long tope, string cabeza, string cuerpo, bool conSello)
        {
            if (string.IsNullOrEmpty(ruta)) return;
            Apunte a = new Apunte(); a.Ruta = ruta; a.Tope = tope; a.Cabeza = cabeza ?? ""; a.Cuerpo = cuerpo; a.ConSello = conSello;
            lock (cerrojoCola)
            {
                if (cola.Count >= MaximoEnCola) { descartados++; return; }
                cola.Enqueue(a);
                if (trabajando) return;
                trabajando = true;
            }
            bool encolado = false;
            try { encolado = ThreadPool.QueueUserWorkItem(Trabaja); } catch { }
            if (!encolado) lock (cerrojoCola) trabajando = false;   // sin hilos (al cerrar): queda en la cola para Vacia
        }

        private static void Trabaja(object nada)
        {
            while (true)
            {
                try { lock (cerrojoDisco) EscribeLoPendiente(); } catch { }
                lock (cerrojoCola) { if (cola.Count == 0) { trabajando = false; return; } }
            }
        }

        // Escribe ahora, en este hilo, todo lo pendiente: al cerrar, ante un fallo grave y en las pruebas. Espera como mucho
        // 'ms' a que acabe lo que estuviera escribiendo el hilo de fondo.
        internal static bool Vacia(int ms)
        {
            if (!Monitor.TryEnter(cerrojoDisco, ms)) return false;
            try { EscribeLoPendiente(); }
            catch { }
            finally { Monitor.Exit(cerrojoDisco); }
            return true;
        }

        // Con cerrojoDisco tomado. Lo seguido que va al mismo archivo sale de una sola vez.
        private static void EscribeLoPendiente()
        {
            Apunte[] tanda; int perdidos;
            lock (cerrojoCola)
            {
                if (cola.Count == 0) return;
                tanda = cola.ToArray(); cola.Clear();
                perdidos = descartados; descartados = 0;
            }
            if (sello == null)
            {
                string s = "";
                try { s = Form1.DeCuandoEs(); } catch { }
                sello = s.Length > 0 ? "  (rocola del " + s + ")" : "";
            }
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < tanda.Length)
            {
                string ruta = tanda[i].Ruta; long tope = tanda[i].Tope;
                sb.Length = 0;
                if (perdidos > 0) { sb.Append("(" + perdidos + " apuntes sin escribir: el disco no daba abasto)\r\n"); perdidos = 0; }
                for (; i < tanda.Length && string.Equals(tanda[i].Ruta, ruta, StringComparison.OrdinalIgnoreCase); i++)
                {
                    Apunte a = tanda[i];
                    sb.Append(a.Cabeza);
                    if (a.ConSello) sb.Append(sello);
                    if (a.Cuerpo != null) sb.Append(a.Cuerpo);
                }
                Pega(ruta, tope, sb.ToString());
            }
        }

        // El tope y el pegado de siempre (UTF-8 sin marca). Si el archivo esta ocupado, se reintenta durante un segundo.
        private static void Pega(string ruta, long tope, string texto)
        {
            if (tope > 0) { try { FileInfo fi = new FileInfo(ruta); if (fi.Exists && fi.Length > tope) fi.Delete(); } catch { } }
            for (int intento = 0; ; intento++)
            {
                try { File.AppendAllText(ruta, texto); return; }
                catch (DirectoryNotFoundException) { return; }
                catch (PathTooLongException) { return; }
                catch (IOException) { if (intento >= 20) return; Thread.Sleep(50); }
                catch { return; }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
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

        [ThreadStatic] private static LecturaBiblioteca lecturaPlan;
        private LecturaBiblioteca planDuracion, planRepaso;
        private LecturaBiblioteca planArranqueComun; // solo durante la publicacion UI de biblioteca
        private Thread preparaRepasoTh;
        // Terminar no borra las tablas: otro plan inicial puede seguir usando BaseCompartida.
        // Solo la pantalla suelta SU referencia, y un final antiguo nunca retira un plan nuevo.
        private void SueltaPlanTerminado(LecturaBiblioteca lectura, bool repaso, int generacion)
        {
            if (repaso)
            {
                if (!object.ReferenceEquals(planRepaso, lectura)) return;
                planRepaso = null;
                if (generacion == preScanGen) preScanCarriles = 0;
            }
            else if (object.ReferenceEquals(planDuracion, lectura)) planDuracion = null;
        }
        private void FinalizaLecturaBiblioteca(LecturaBiblioteca lectura, bool repaso, int generacion)
        {
            lectura.Dispose();
            // Sin pantalla viva no publicar; al cerrar se recoge el propio formulario.
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                if (PantallaRocola.FueraDeSuHilo(this)) PantallaRocola.Encola(this, delegate { SueltaPlanTerminado(lectura, repaso, generacion); });   // EN LINUX: FueraDeSuHilo es InvokeRequired (la rocola tiene ventana y este no es el hilo de la pantalla); y un encargo al hilo de la pantalla (BeginInvoke)
                else SueltaPlanTerminado(lectura, repaso, generacion);
            }
            catch (InvalidOperationException) { }
        }
        private LecturaBiblioteca CapturaLecturaBiblioteca()
        { return CapturaParaBiblioteca(true); }
        private LecturaBiblioteca CapturaParaBiblioteca(bool completa)
        {
            if (libBuilding || IsDisposed || Disposing) return null;
            LecturaBiblioteca comun = planArranqueComun;
            if (comun != null && completa) return comun;
            LecturaBiblioteca p = new LecturaBiblioteca();
            if (comun != null)
            {
                p.BaseCompartida = comun; p.Tracks = comun.Tracks; p.Videos = comun.Videos; p.Letras = comun.Letras;
            }
            else
            {
                p.Tracks = new List<Track>(tracks);
                p.Videos = videoAlbums != null ? new List<Album>(videoAlbums) : new List<Album>();
                p.Letras = new List<KeyValuePair<char, List<Album>>>(byLetter);
                p.DurDatos = new List<KeyValuePair<string, float>>(durCache);
                p.RapidasDatos = new List<KeyValuePair<string, float>>(durFast);
            }
            if (completa)
            {
                p.LoudDatos = new List<KeyValuePair<string, float>>(loudCache);
                p.EqDatos = new List<KeyValuePair<string, float[]>>(eqCache);
                lock (integCerrojo) p.IntegDatos = new List<KeyValuePair<string, SonoridadIntegrada>>(integCache);
                p.CancionesDatos = new List<KeyValuePair<string, int>>(songCounts);
                p.VideosDatos = new List<KeyValuePair<string, int>>(videoCounts);
                p.SecDatos = new List<KeyValuePair<string, long>>(songSeq);
                p.VideoSecDatos = new List<KeyValuePair<string, long>>(videoSeq);
                lock (deepDoneLock) p.DeepDatos = new List<string>(deepDone);
                p.ColaDatos = new List<string>(queue.Count);
                for (int i = 0; i < queue.Count; i++) if (queue[i] != null) p.ColaDatos.Add(queue[i].PathKey());
            }
            else { p.DeepDatos = new List<string>(); p.ColaDatos = new List<string>(); }
            p.Tintes = tintesPend; p.Firma = FirmaBiblioteca(); p.LoudCount = loudCache.Count;
            return p;
        }

        // El trabajador publica SOLO dominantes que cambiaron desde la tanda anterior.
        // El desempate recorre los votos en el mismo orden que la implementacion original.
        private sealed class VotosTinte<T>
        {
            private readonly Dictionary<T, Dictionary<string, int>> votos;
            private readonly Dictionary<T, string> publicados;
            private readonly HashSet<T> tocados;
            internal VotosTinte(IEqualityComparer<T> comparador)
            {
                votos = new Dictionary<T, Dictionary<string, int>>(comparador);
                publicados = new Dictionary<T, string>(comparador); tocados = new HashSet<T>(comparador);
            }
            internal void Vota(T clave, string etiqueta)
            {
                Dictionary<string, int> cuenta;
                if (!votos.TryGetValue(clave, out cuenta)) { cuenta = new Dictionary<string, int>(); votos[clave] = cuenta; }
                int n; cuenta.TryGetValue(etiqueta, out n); cuenta[etiqueta] = n + 1; tocados.Add(clave);
            }
            internal void Delta(out T[] claves, out string[] etiquetas)
            {
                List<T> k = new List<T>(); List<string> e = new List<string>();
                foreach (T clave in tocados)
                {
                    string mejor = null; int n = 0;
                    foreach (KeyValuePair<string, int> voto in votos[clave])
                        if (voto.Key != "Otros" && voto.Value > n) { mejor = voto.Key; n = voto.Value; }
                    string anterior;
                    if (mejor != null && (!publicados.TryGetValue(clave, out anterior) || anterior != mejor))
                    { publicados[clave] = mejor; k.Add(clave); e.Add(mejor); }
                }
                tocados.Clear(); claves = k.ToArray(); etiquetas = e.ToArray();
            }
        }

        private bool AplicaTintesNuevos(char[] letras, string[] etiquetas, string[] discos, string[] etDiscos)
        {
            bool cambio = false;
            for (int i = 0; i < letras.Length && i < etiquetas.Length; i++)
            {
                string antes;
                if (letterTag.TryGetValue(letras[i], out antes) && antes == etiquetas[i]) continue;
                letterTag[letras[i]] = etiquetas[i]; cambio = true;
            }
            for (int i = 0; i < discos.Length && i < etDiscos.Length; i++)
            {
                string antes;
                if (discTag.TryGetValue(discos[i], out antes) && antes == etDiscos[i]) continue;
                discTag[discos[i]] = etDiscos[i]; cambio = true;
            }
            return cambio;
        }
        private void CancelaPlanesBiblioteca()
        {
            if (planDuracion != null) planDuracion.Cancela();
            if (planRepaso != null) planRepaso.Cancela();
            lock (analisisUnidadLock) Monitor.PulseAll(analisisUnidadLock);
            lock (coverLock) Monitor.PulseAll(coverLock);
        }

        // Turnos de los lectores de analisis: un HDD no recibe varios archivos a la vez.
        // SSD: no imponer ese limite de cabezal; el presupuesto total lo decide la CPU efectiva.
        private readonly object analisisUnidadLock = new object();
        private readonly Dictionary<string, int> analisisUnidad = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private sealed class TurnoAnalisis : IDisposable
        {
            private Form1 f; private readonly string unidad;
            internal TurnoAnalisis(Form1 owner, string unit) { f = owner; unidad = unit; }
            public void Dispose()
            {
                Form1 x = f; f = null; if (x == null || unidad == null) return;
                lock (x.analisisUnidadLock)
                {
                    int n; if (x.analisisUnidad.TryGetValue(unidad, out n))
                    { if (n <= 1) x.analisisUnidad.Remove(unidad); else x.analisisUnidad[unidad] = n - 1; }
                    Monitor.PulseAll(x.analisisUnidadLock);
                }
            }
        }
        private TurnoAnalisis PideTurnoAnalisis(string path, Func<bool> cancel)
        {
            if (!DiscoLento(path)) return new TurnoAnalisis(this, null);
            string unit = UnidadDe(path);
            while (cancel == null || !cancel())
            {
                lock (analisisUnidadLock)
                {
                    int n; analisisUnidad.TryGetValue(unit, out n);
                    if (n == 0) { analisisUnidad[unit] = 1; return new TurnoAnalisis(this, unit); }
                    // Se despierta al liberar/cancelar; el limite solo cubre canceladores ajenos.
                    Monitor.Wait(analisisUnidadLock, 250);
                }
            }
            return null;
        }
        private static bool RepasoEnDiscoLento()
        {
            if (perfilDisco != 0) return perfilDisco == 2;
            lock (discoLock) foreach (bool lento in discoLento.Values) if (!lento) return false;
            return diskSlow;
        }

        private sealed class OrigenTop
        {
            internal Track Pista; internal Album Disco; internal int Orden;
        }
        private Dictionary<string, List<OrigenTop>> origenesTop;
        private List<Album> albumsDelIndiceTop;
        private static Dictionary<string, List<OrigenTop>> IndiceTop(List<Album> lista)
        {
            Dictionary<string, List<OrigenTop>> indice = new Dictionary<string, List<OrigenTop>>(StringComparer.OrdinalIgnoreCase);
            int n = 0;
            for (int i = 0; i < lista.Count; i++)
                for (int j = 0; j < lista[i].Tracks.Count; j++, n++)
                {
                    Track t = lista[i].Tracks[j]; if (t == null || t.Path == null) continue;
                    List<OrigenTop> origins;
                    if (!indice.TryGetValue(t.Path, out origins)) { origins = new List<OrigenTop>(1); indice[t.Path] = origins; }
                    OrigenTop o = new OrigenTop(); o.Pista = t; o.Disco = lista[i]; o.Orden = n; origins.Add(o);
                }
            return indice;
        }
    }
}

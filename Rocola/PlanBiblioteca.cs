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
            private readonly ManualResetEvent cancelada = new ManualResetEvent(false);
            internal volatile bool Cancelada;
            internal void Cancela() { Cancelada = true; try { cancelada.Set(); } catch (ObjectDisposedException) { } }
            internal bool Espera(int ms)
            {
                if (Cancelada) return false;
                try { return !cancelada.WaitOne(ms, false); } catch (ObjectDisposedException) { return false; }
            }
            internal void Prepara()
            {
                Dur = Indice(DurDatos); Rapidas = Indice(RapidasDatos); Loud = Indice(LoudDatos);
                Eq = Indice(EqDatos); Integ = Indice(IntegDatos);
                Canciones = Indice(CancionesDatos); VideoCounts = Indice(VideosDatos);
                Sec = Indice(SecDatos); VideoSec = Indice(VideoSecDatos);
                Deep = new HashSet<string>(DeepDatos, StringComparer.OrdinalIgnoreCase);
                Cola = new HashSet<string>(ColaDatos, StringComparer.OrdinalIgnoreCase);
                DurDatos = null; RapidasDatos = null; LoudDatos = null; EqDatos = null; IntegDatos = null;
                CancionesDatos = null; VideosDatos = null; SecDatos = null; VideoSecDatos = null;
                DeepDatos = null; ColaDatos = null;
            }
            private static Dictionary<string, T> Indice<T>(List<KeyValuePair<string, T>> filas)
            {
                Dictionary<string, T> r = new Dictionary<string, T>(filas.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < filas.Count; i++) r[filas[i].Key] = filas[i].Value;
                return r;
            }
            public void Dispose() { cancelada.Close(); }
        }

        [ThreadStatic] private static LecturaBiblioteca lecturaPlan;
        private LecturaBiblioteca planDuracion, planRepaso;
        private Thread preparaRepasoTh;
        private LecturaBiblioteca CapturaLecturaBiblioteca()
        {
            if (libBuilding || IsDisposed || Disposing) return null;
            LecturaBiblioteca p = new LecturaBiblioteca();
            p.Tracks = new List<Track>(tracks);
            p.Videos = videoAlbums != null ? new List<Album>(videoAlbums) : new List<Album>();
            p.Letras = new List<KeyValuePair<char, List<Album>>>(byLetter);
            p.DurDatos = new List<KeyValuePair<string, float>>(durCache);
            p.RapidasDatos = new List<KeyValuePair<string, float>>(durFast);
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
            p.Tintes = tintesPend; p.Firma = FirmaBiblioteca(); p.LoudCount = loudCache.Count;
            return p;
        }
        private void CancelaPlanesBiblioteca()
        {
            if (planDuracion != null) planDuracion.Cancela();
            if (planRepaso != null) planRepaso.Cancela();
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
                }
                Thread.Sleep(20);
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

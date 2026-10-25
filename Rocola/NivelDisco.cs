// NIVEL POR DISCO (las dos ramas; 28 de septiembre de 2026).
//
// El nivelado deja cada cancion al volumen objetivo por su cuenta. Cuando suenan seguidas pistas del MISMO disco, eso
// borra lo que el disco trae a proposito: la balada que va mas bajita que el tema de antes, la intro tranquila, el
// final que crece. Aqui, si la cancion que va a sonar tiene justo antes o justo despues otra de su mismo disco, las dos
// (y toda la racha) se nivelan con la MISMA ganancia: la del disco entero. Asi el disco llega al volumen de siempre y,
// por dentro, suena como se grabo.
//
// El DISCO es la carpeta de la cancion (asi lo arma la rocola): da igual si se pidio desde la pared, desde el buscador
// o desde "lo mas sonado". Su volumen es la media de potencia de sus pistas medidas enteras (pesada por lo que dura
// cada una) y sus picos, los mas altos de todas: la ganancia del disco no aplasta a ninguna (el mismo tope que el
// nivelado, GananciaNivelado). Hacen falta al menos 2 pistas medidas, la mitad del disco y la propia cancion; si no,
// se nivela cancion por cancion, como siempre. No lleva interruptor: sin racha de disco no cambia nada.
//
// RECOPILATORIOS PAREJOS (22 de octubre de 2026). Un "Exitos 90s" o un "Varios" no es un disco: son pistas de grabaciones
// distintas, y con la ganancia unica una queda 8 dB mas baja que la otra. Si entre las pistas medidas del disco la mas
// fuerte y la mas baja se llevan mas de DispersionDiscoDb (5 dB), no hay un volumen de disco que respetar y cada cancion
// se nivela por su cuenta. Las pistas de menos de 30 s (intros, cortinillas) no cuentan para la dispersion: en un disco de
// verdad siempre hay una. Tampoco lleva interruptor.
//
// AL LIMITE (24-10-2026). Con todas sus pistas medidas con el algoritmo 5 (con el P90), el disco entero sube hasta el limite de su
// pista mas exigente (los picos y el P90 mas altos de todas, GananciaAlLimite): ninguna se raspa, y las bajitas siguen sonando por
// debajo, como en el disco. Si falta alguna medida nueva, la regla de antes, hacia el objetivo, mientras la tanda larga las mide.
//
// La cancion que YA suena conserva la ganancia con la que empezo aunque despues se pida otra de su disco: cambiarla a
// media cancion se oiria. La racha cuenta desde la siguiente.
using System;
using System.Collections.Generic;

namespace Rocola
{
    public partial class Form1
    {
        // La mas fuerte y la mas baja de un disco (dB de la potencia por canal) pueden llevarse hasta esto y seguir siendo un
        // disco; mas, es un recopilatorio y se nivela cancion por cancion. Las pistas con menos de MinBloquesDispersion
        // bloques de 400 ms (30 s) no entran en esa cuenta.
        internal const float DispersionDiscoDb = 5f;
        internal const int MinBloquesDispersion = 300;

        private PlayItem sonoAntes;   // lo que sono justo antes de la que suena (null si hubo silencio o es la primera)

        // La ganancia de nivelado de una cancion en su sitio de la cola: la del disco si esta en racha, si no la suya.
        // Y con el margen que necesita el tono del local que le toca (T1, SalaLocal.MargenTonoDb).
        private float GananciaDe(Track t)
        {
            if (t == null) return 1f;
            List<Track> disco = RachaDeDisco(t);
            float g;
            if (!(disco != null && GananciaDeDisco(disco, t.Path, out g))) g = NormGainFor(t.Path);
            float m = MargenDelTono(t);
            return m == 1f ? g : g * m;
        }
        // Lo que baja el nivelado para el tono del local que de verdad suena en esta cancion (los mandos, o lo que decide el modo
        // automatico). Con "Escuchar sin mejoras" el tono no suena: no se baja. 1 = nada (tono en 0 o negativo).
        private float MargenDelTono(Track t)
        {
            if (sinMejoras) return 1f;
            AjusteLocal lo = LocalDe(t);
            double db = SalaLocal.MargenTonoDb(lo.Graves, lo.Agudos);
            return db > 0.0 ? (float)Math.Pow(10.0, -db / 20.0) : 1f;
        }

        // Las pistas del disco de 't' si suena en racha con otra de su disco (la de antes o la de despues); null si no.
        private List<Track> RachaDeDisco(Track t)
        {
            PlayItem np = nowPlaying, yo, antes, despues;
            if (np != null && object.ReferenceEquals(np.Song, t)) { yo = np; antes = sonoAntes; despues = queue.Count > 0 ? queue[0] : null; }
            else if (queue.Count > 0 && queue[0] != null && object.ReferenceEquals(queue[0].Song, t)) { yo = queue[0]; antes = np; despues = queue.Count > 1 ? queue[1] : null; }
            else return null;
            if (yo.IsVideo || t.Path == null) return null;
            string carpeta = CarpetaDe(t.Path);
            if (string.IsNullOrEmpty(carpeta) || !(MismaCarpeta(antes, carpeta) || MismaCarpeta(despues, carpeta))) return null;   // sin carpeta no hay disco
            return PistasDeCarpeta(yo.Disc, carpeta);
        }
        private static string CarpetaDe(string ruta)
        {
            try { return System.IO.Path.GetDirectoryName(ruta); } catch { return null; }
        }
        private static bool MismaCarpeta(PlayItem x, string carpeta)
        {
            return x != null && !x.IsVideo && x.Song != null && x.Song.Path != null
                && string.Equals(CarpetaDe(x.Song.Path), carpeta, StringComparison.OrdinalIgnoreCase);
        }
        // Las pistas de esa carpeta: las del disco con el que se pidio, si es de verdad ese disco; si no (un disco
        // "lo mas sonado", o pedida desde el buscador), el disco de la biblioteca con esa carpeta.
        private List<Track> PistasDeCarpeta(Album pedido, string carpeta)
        {
            if (pedido != null && !pedido.IsTop && pedido.Tracks != null && pedido.Tracks.Count > 0
                && string.Equals(CarpetaDe(pedido.Tracks[0].Path), carpeta, StringComparison.OrdinalIgnoreCase)) return pedido.Tracks;
            List<Album> todos = albums;
            if (todos != null)
                for (int i = 0; i < todos.Count; i++)
                {
                    Album a = todos[i];
                    if (a == null || a.IsTop || a.Tracks == null || a.Tracks.Count == 0) continue;
                    if (string.Equals(CarpetaDe(a.Tracks[0].Path), carpeta, StringComparison.OrdinalIgnoreCase)) return a.Tracks;
                }
            return null;
        }

        // La ganancia del disco entero. Falso si no hay bastante medido (se nivela cancion por cancion).
        private bool GananciaDeDisco(List<Track> pistas, string ruta, out float g)
        {
            g = 1f;
            if (!normalizeOn || normStrength <= 0.001f || pistas == null || integCache == null) return false;
            double peso = 0, pot = 0; float pico = 0f, p99 = 0f, p90 = 0f; bool picos = true, conP90 = true, esta = false; int medidas = 0;
            float rmsMin = float.MaxValue, rmsMax = 0f;
            for (int i = 0; i < pistas.Count; i++)
            {
                Track tr = pistas[i];
                SonoridadIntegrada s;
                if (tr == null || tr.Path == null || !integCache.TryGetValue(tr.Path, out s) || !s.Valida || s.Muda) continue;
                float rms = s.RmsPorCanal;
                if (!(rms > 0.0001f)) continue;
                double w = Math.Max(1, s.Bloques);
                pot += w * (double)rms * rms; peso += w; medidas++;
                if (s.Bloques >= MinBloquesDispersion) { if (rms < rmsMin) rmsMin = rms; if (rms > rmsMax) rmsMax = rms; }
                if (string.Equals(tr.Path, ruta, StringComparison.OrdinalIgnoreCase)) esta = true;
                if (s.ConPicos) { pico = Math.Max(pico, DbALin(s.PicoDb)); p99 = Math.Max(p99, DbALin(s.P99Db)); }
                else picos = false;
                if (s.ConP90) p90 = Math.Max(p90, DbALin(s.P90Db)); else conP90 = false;
            }
            if (!esta || medidas < 2 || medidas * 2 < pistas.Count || !(peso > 0)) return false;
            if (rmsMax > 0f && rmsMin < float.MaxValue && rmsMax > rmsMin * (float)Math.Pow(10.0, DispersionDiscoDb / 20.0)) return false;   // recopilatorio: cancion por cancion
            if (picos && conP90) { g = GananciaAlLimite(normStrength, pico, p99, p90); return true; }   // todas con la medida nueva: al limite
            float rmsDisco = (float)Math.Sqrt(pot / peso);
            float cap = (float)Math.Pow(10.0, TierCapDb(rmsDisco) / 20.0);
            if (cap > NormMaxBoost) cap = NormMaxBoost;
            g = GananciaNivelado(normTarget, rmsDisco, normStrength, cap, picos, pico, p99);
            return true;
        }
    }
}

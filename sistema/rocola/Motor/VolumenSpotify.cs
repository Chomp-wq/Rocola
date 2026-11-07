// EL VOLUMEN DE SPOTIFY (decision del dueno del 7-11-2026: «V1, V2 y V3»). Solo Rocola OS con el modo Spotify. Lo de Spotify llega ya con la
// normalizacion de Spotify (cerca de -14 LUFS, sin subir lo que se recortaria); encima, la rocola:
//  V1  LA MIDE COMO A TUS CANCIONES: mientras suena, lo que llega (antes de cualquier ajuste) pasa por el mismo medidor del repaso
//      (MedidorBs1770: sonoridad y picos por bloque); si sono casi entera (97 %), su medida se apunta bajo su ruta de Spotify y desde la
//      siguiente vez va «al limite» con la misma regla que tu biblioteca (GananciaAlLimite). Solo numeros; nada de sonido se guarda.
//  V2  LA PRIMERA VEZ, LA BASE APRENDIDA: la mediana de la ganancia al limite de las de Spotify ya medidas (con 5 o mas); antes, la de
//      siempre (normTarget / RmsSpotify).
//  V3  Y POR CANCION: rocola-spotify pasa el pico de cada cancion y el factor que le aplico Spotify; con ese pico y las proporciones
//      aprendidas (P99 y P90 frente al pico: de las de Spotify medidas, si no de tu biblioteca, si no 2 y 5 dB) se estima su ganancia al
//      limite y se corrige la base (entre -6 y +6 dB), desde el primer cuadro (espera sus datos hasta 0,7 s). Nunca a media cancion.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        private FuenteSpotify fuenteMide; private string rutaMide;
        private long calibradoDe = -1; private float baseV2 = float.NaN, r99Sp = DbALin(-2f), r90Sp = DbALin(-5f); private int medidasSp;

        private bool TieneP90(string ruta)
        {
            SonoridadIntegrada si;
            return ruta != null && integCache != null && integCache.TryGetValue(ruta, out si) && si.ConP90;
        }
        // La base para una de Spotify sin medir al limite (V2); NaN si aun no hay con que.
        private float BaseSpotifyAprendida() { CalibraSpotify(); return baseV2; }
        // Las medianas de lo ya medido (se rehacen cuando cambia la tabla de medidas).
        private void CalibraSpotify()
        {
            if (integCache == null || calibradoDe == integCambios) return;
            calibradoDe = integCambios;
            List<float> g = new List<float>(), a99 = new List<float>(), a90 = new List<float>(), b99 = new List<float>(), b90 = new List<float>();
            foreach (KeyValuePair<string, SonoridadIntegrada> kv in integCache)
            {
                SonoridadIntegrada s = kv.Value;
                if (!s.ConP90 || float.IsNaN(s.PicoDb) || float.IsNaN(s.P99Db)) continue;
                bool sp = EsRutaSpotify(kv.Key);
                (sp ? a99 : b99).Add(s.P99Db - s.PicoDb); (sp ? a90 : b90).Add(s.P90Db - s.PicoDb);
                if (sp) g.Add(GananciaAlLimite(normStrength, DbALin(s.PicoDb), DbALin(s.P99Db), DbALin(s.P90Db)));
            }
            medidasSp = g.Count;
            baseV2 = g.Count >= 5 ? Mediana(g) : float.NaN;
            if (a99.Count >= 5) { r99Sp = DbALin(Mediana(a99)); r90Sp = DbALin(Mediana(a90)); }
            else if (b99.Count >= 20) { r99Sp = DbALin(Mediana(b99)); r90Sp = DbALin(Mediana(b90)); }
        }
        private static float Mediana(List<float> l) { l.Sort(); return l.Count % 2 == 1 ? l[l.Count / 2] : 0.5f * (l[l.Count / 2 - 1] + l[l.Count / 2]); }

        // Al empezar una de Spotify (EmpiezaSpotify): cierra la medida de la anterior y prepara la de esta.
        private void PreparaVolumenSpotify(FuenteSpotify f, Track pista)
        {
            CierraMedidaSpotify();
            SpotifyEnVivo s = spotify;
            f.NormN0 = s != null ? Interlocked.Read(ref s.NormN) : 0;
            if (TieneP90(pista.Path)) return;   // ya medida: al limite con su medida (GananciaSpotify), sin medir ni estimar
            f.Medidor = new MedidorBs1770(SpotifyEnVivo.Hz, new double[] { 1.0, 1.0 });
            fuenteMide = f; rutaMide = pista.Path;
            if (normalizeOn && normStrength > 0.001f && s != null)
            {
                CalibraSpotify();
                f.Estimar = true; f.Fuerza = normStrength; f.R99 = r99Sp; f.R90 = r90Sp;
                f.Base = GananciaBaseSpotify(pista);
            }
        }
        // La ganancia de nivelado de una de Spotify sin medida al limite (sin el margen del tono): V2 o la de siempre.
        private float GananciaBaseSpotify(Track t)
        {
            float b = BaseSpotifyAprendida();
            if (!float.IsNaN(b)) return Math.Max(0.5f, Math.Min(4f, b));
            float rms;
            if (SonoridadParaNivelar(t.Path, out rms)) return NormGainFor(t.Path);   // (medida a la antigua, solo con su energia: SP3)
            return Math.Max(0.5f, Math.Min(4f, normTarget / RmsSpotify));
        }
        // Cada vuelta (AtiendeSpotify): si la que se medía acabo o ya no suena, se cierra su medida.
        private void SigueMedidaSpotify()
        {
            FuenteSpotify f = fuenteMide; if (f == null) return;
            CadenaCancion c = cadenaSonando;
            if (!f.Acabada && c != null && object.ReferenceEquals(c.Lector, f)) return;
            CierraMedidaSpotify();
        }
        private void CierraMedidaSpotify()
        {
            FuenteSpotify f = fuenteMide; string ruta = rutaMide; fuenteMide = null; rutaMide = null;
            if (f == null || f.Medidor == null || ruta == null) return;
            SpotifyEnVivo sp = spotify;
            if (sp != null && f.Decision != null) sp.Apunta("volumen la primera vez: " + ruta.Substring(ruta.LastIndexOf('/') + 1) + ": " + f.Decision);
            double ms = f.TotalTime.TotalMilliseconds, oido = f.CuadrosLeidos * 1000.0 / SpotifyEnVivo.Hz;
            if (!(ms >= 30000) || oido < ms * 0.97) return;   // no sono casi entera: no se apunta (la proxima vez)
            SonoridadIntegrada s; bool ok;
            lock (f.Medidor) ok = CierraIntegrada(f.Medidor, f.Medidor.Cuadros / (double)SpotifyEnVivo.Hz, SpotifyEnVivo.Hz, out s);
            if (!ok || !s.ConP90) return;
            PublicaIntegrada(ruta, s);
            if (sp != null) sp.Apunta("medida al limite: " + ruta.Substring(ruta.LastIndexOf('/') + 1) + ": pico " + s.PicoDb.ToString("0.0", CultureInfo.InvariantCulture)
                + ", P90 " + s.P90Db.ToString("0.0", CultureInfo.InvariantCulture) + " dBFS; la proxima vez "
                + (20 * Math.Log10(GananciaAlLimite(normStrength, DbALin(s.PicoDb), DbALin(s.P99Db), DbALin(s.P90Db)))).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " dB");
        }
    }
}

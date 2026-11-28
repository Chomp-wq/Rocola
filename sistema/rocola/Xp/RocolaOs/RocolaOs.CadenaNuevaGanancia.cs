// LA GANANCIA DE LA CADENA NUEVA (CADENA_NUEVA.md §1.2, §5): UNA por cancion (o por disco), decidida antes de sonar y fija toda la cancion.
//   g (dB) = min(T - L', C - TP')
//   T = nivel_lufs (-18 de fabrica), C = techo_dbtp (-1). L' y TP' = lo medido de la salida de la cadena a ganancia 1 (RocolaOsMedidaNueva) MAS
//   lo que le hara lo que va despues y no se midio: el ecualizador del dueno de esa memoria (para L', con el espectro de tercios de la cancion
//   por la curva, con la ponderacion K, y sin espectro el tipico de la biblioteca; para TP', lo que sube de verdad su pico con los picos de las
//   sondas de la cancion y la respuesta al impulso de la curva: RocolaOs.CadenaNuevaPicos.cs, a1 del 28-11-2026; hasta entonces, lo maximo de la
//   curva), el tono parejo si esta puesto (lo maximo de su curva) y, con agudos_mp3, medio dB mas de pico. Las que no caben quedan mas bajas (no hay limitador). Sin la medida: una ESTIMACION CONSERVADORA de las
//   tablas viejas de XP (su LKFS y su pico + 3 dB), fija toda la cancion; sin nada, -8 LUFS y +3 dBTP.
// DISCOS (la racha de hoy, NivelDisco.cs, con sus reglas: dos pistas medidas o mas, la mitad del disco y la propia cancion; no en los
// recopilatorios, con mas de 5 dB entre la mas fuerte y la mas baja): una sola ganancia del disco con L' = la media de energia de sus pistas
// (por lo que duran) y TP' = el mayor. (28-11-2026) El modo (por disco o cancion por cancion) lo decide la PRIMERA pista de la racha y las demas
// lo siguen (b10); y la que entra SIN PAUSA detras de la anterior del mismo disco lleva la ganancia de la que suena mientras quepa en su techo
// (a3: sin escalon; la regla de los recopilatorios no parte un disco continuo).
// SPOTIFY: su medida (de la cadena nueva, hecha mientras sono entera) o, la primera vez, una FIJA ESTIMADA de las de Spotify ya medidas (la
// mediana de su L' y de su TP' con 1,5 dB de margen; con menos de 5, las de rocola_spotify_niveles.txt de XP con su pico de muestra + 2 dB).
// LA SUSTITUCION INVISIBLE: la medida de la version de Spotify o, sin ella, la de tu MP3.
// NADA MAS mueve el nivel: ni limitador, ni preganancias, ni el margen E3, ni el parejo del billar, ni el «al limite». Un toque en Ctrl+F3 no
// cambia la ganancia de la que suena (la cadena nueva no tiene LevelTap que ApplyNormNow toque). El modo noche va aparte (el reloj).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        internal const double RocolaOsAgudosMargenDb = 0.5, RocolaOsSpotifyMargenDb = 1.5, RocolaOsEstimaPicoDb = 3.0;
        internal const int RocolaOsSpotifyMinimo = 5;

        // la ponderacion K (dB) en el centro de cada tercio, a esa tasa (para estimar lo que cambia la sonoridad)
        private static double[] rocolaOsKw; private static int rocolaOsKwTasa;
        internal static double[] RocolaOsKPesoDb(int tasa)
        {
            double[] k = rocolaOsKw;
            if (k != null && rocolaOsKwTasa == tasa) return k;
            double[] e1 = MedidorBs1770.Estante(tasa), e2 = MedidorBs1770.PasoAlto(tasa);
            k = new double[RocolaOsMedidaNueva.Bandas];
            for (int b = 0; b < k.Length; b++)
            {
                double f = RocolaOsMedidaNueva.Hz(b);
                k[b] = f < 0.5 * tasa ? EcualizadorDueno.MagDb(e1, 0, 2 * Math.PI * f / tasa) + EcualizadorDueno.MagDb(e2, 0, 2 * Math.PI * f / tasa) : -200;
            }
            rocolaOsKwTasa = tasa; rocolaOsKw = k;
            return k;
        }
        private static double[] RocolaOsHzTercios()
        {
            double[] hz = new double[RocolaOsMedidaNueva.Bandas];
            for (int b = 0; b < hz.Length; b++) hz[b] = RocolaOsMedidaNueva.Hz(b);
            return hz;
        }
        // LO QUE SUBE LA SONORIDAD una curva (dB por tercio) sobre una cancion con ese espectro (dB por tercio, relativo): puro. Sin espectro (28-11-2026,
        // a1), el TIPICO de la biblioteca (la mediana de los tercios medidos con esa cadena) y, sin nada medido, el de un ruido rosa; ya no lo
        // maximo que sube la curva (que daba por hecho que toda la musica estaba en la banda subida).
        internal static double RocolaOsDeltaL(float[] tercios, double[] curva, int tasa, char cadena = 'A')
        {
            if (curva == null) return 0.0;
            if (tercios == null || tercios.Length < RocolaOsMedidaNueva.Bandas)
            {
                float[] tt, tp; RocolaOsPicosEq.Tipico(cadena, out tt, out tp);
                tercios = tt ?? RocolaOsPicosEq.Rosa;
            }
            double[] kw = RocolaOsKPesoDb(tasa);
            double sin = 0, con = 0;
            for (int b = 0; b < RocolaOsMedidaNueva.Bandas; b++)
            {
                double p = Math.Pow(10.0, (tercios[b] + kw[b]) / 10.0);
                sin += p; con += p * Math.Pow(10.0, curva[b] / 10.0);
            }
            return sin > 0 ? 10.0 * Math.Log10(con / sin) : 0.0;
        }
        // LO MAXIMO QUE SUBE una curva de biquads entre 20 Hz y 20 kHz (200 puntos logaritmicos): para TP' del tono parejo, conservador
        internal static double RocolaOsMaxSube(Func<double[], double[]> respuesta, int tasa)
        {
            double tope = Math.Min(20000.0, 0.49 * tasa);
            double[] hz = new double[200];
            for (int i = 0; i < hz.Length; i++) hz[i] = 20.0 * Math.Exp(Math.Log(tope / 20.0) * i / (hz.Length - 1));
            double[] r = respuesta(hz);
            double mx = 0; foreach (double v in r) if (v > mx) mx = v;
            return mx;
        }

        // LO QUE LE AÑADE A L Y A TP lo que va despues de lo medido (el EQ del dueno, el tono parejo, los agudos). 'tpDb' es el pico real antes del
        // EQ del dueno y 'picos' los de las sondas de esa cancion (null: los tipicos): con ellos, lo que sube de verdad su pico (RocolaOsPicosEq,
        // a1 del 28-11-2026; hasta entonces, lo maximo de la curva). 'otros': lo del tono parejo y los agudos (sin el EQ del dueno).
        private void RocolaOsDespues(RocolaOsReceta r, float[] tercios, double tpDb, float[] picos, out double dl, out double dtp, out double otros)
        {
            RocolaOsDespuesCon(r, EqDuenoMedios(r.Cadena == 'B'), tercios, tpDb, picos, out dl, out dtp, out otros);
        }
        internal static void RocolaOsDespuesCon(RocolaOsReceta r, int[] eq, float[] tercios, double tpDb, float[] picos, out double dl, out double dtp, out double otros)
        {
            dl = 0; dtp = 0; otros = 0;
            int tasa = r.Tasa;
            bool hayEq = false; if (eq != null) foreach (int m in eq) if (m != 0) hayEq = true;
            double[] hzT = RocolaOsHzTercios();
            if (hayEq)
            {
                dl += RocolaOsDeltaL(tercios, EcualizadorDueno.Respuesta(eq, tasa, hzT), tasa, r.Cadena);
                float[] tt, tipicos = null;
                if (picos == null) RocolaOsPicosEq.Tipico(r.Cadena, out tt, out tipicos);
                dtp += RocolaOsPicosEq.SubeDb(eq, tasa, tpDb, picos, tipicos);
            }
            if (r.TonoDb != null)
            {
                double[] g = TonoParejoGanancias(r.TonoDb, tasa);
                dl += RocolaOsDeltaL(tercios, TonoParejoRespuesta(g, tasa, hzT), tasa, r.Cadena);
                otros += RocolaOsMaxSube(h => TonoParejoRespuesta(g, tasa, h), tasa);
            }
            if (r.Agudos) otros += RocolaOsAgudosMargenDb;
            dtp += otros;
        }
        private static double RocolaOsG(double l, double tp) { return Math.Max(RocolaOsCadenasNuevas.GananciaMinDb, Math.Min(RocolaOsCadenasNuevas.GananciaMaxDb, Math.Min(RocolaOsCadenasNuevas.T - l, RocolaOsCadenasNuevas.C - tp))); }
        private static string RocolaOsD1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        // lo de esta cancion para el tope del EQ y el disco continuo (RocolaOsReceta: TpAntesEqDb, PicosEq, DtpOtrosDb, TechoDb)
        private static double RocolaOsAnota(RocolaOsReceta r, double l, double tpAntes, float[] picos, double dtp, double otros)
        {
            r.TpAntesEqDb = tpAntes; r.PicosEq = picos; r.DtpOtrosDb = otros; r.TechoDb = RocolaOsCadenasNuevas.C - (tpAntes + dtp);
            return RocolaOsG(l, tpAntes + dtp);
        }

        // LA GANANCIA DE ESTA CANCION en esta receta (en dB); r.Como dice de donde salio. En el hilo de la pantalla. 'anterior': la que suena, si esta
        // entra SIN PAUSA detras de ella (la siguiente pista del mismo disco, en el relevo; null si no).
        private double RocolaOsGananciaNueva(RocolaOsReceta r, Track t, Track original, bool sombra, RocolaOsArmada anterior = null)
        {
            double g = RocolaOsGananciaSuya(r, t, original, sombra);
            // EL DISCO CONTINUO (a3 del 28-11-2026): la que entra sin pausa lleva la MISMA ganancia que la que suena (la del disco, si la racha va por
            // disco; si no, la de la pista anterior), mientras quepa en su techo: sin escalon en el cambio de pista. La regla de los recopilatorios
            // no parte un disco continuo.
            RocolaOsGananciaPieza ga = anterior != null ? anterior.Ganancia : null;
            if (ga != null && anterior.Receta != null && anterior.Receta.Cadena == r.Cadena && !double.IsNaN(r.TechoDb))
            {
                double gAnt = ga.EfectivaDb;
                double gc = Math.Max(RocolaOsCadenasNuevas.GananciaMinDb, Math.Min(gAnt, r.TechoDb));
                r.Como = "disco continuo: la de la anterior, " + RocolaOsD1(gAnt) + " dB" + (gc < gAnt - 1e-9 ? " bajada a su techo (" + RocolaOsD1(gc) + ")" : "") + "; la suya seria " + RocolaOsD1(g) + " (" + r.Como + ")";
                return gc;
            }
            return g;
        }

        // la suya (sin el disco continuo): la racha, lo medido, la sustitucion, Spotify, la estimacion
        private double RocolaOsGananciaSuya(RocolaOsReceta r, Track t, Track original, bool sombra)
        {
            double dl, dtp, ot;
            bool spotify = (t.Path ?? "").StartsWith("spotify:", StringComparison.Ordinal);
            // 1) DISCO EN RACHA (no con Spotify ni en la sustitucion): el modo (por disco o cancion por cancion) lo decide la PRIMERA pista de la
            //    racha (b10 del 28-11-2026: antes, si la 2 se pedia con la 1 ya sonando, la 1 iba por pista y la 2 por disco)
            if (!sombra && !spotify && original != null && original.Path != null)
            {
                double gd;
                if (RocolaOsRacha(r, original, t.Path, out gd)) return gd;
            }
            // 2) LO MEDIDO de esta cancion
            if (r.Medida != null)
            {
                if (r.Medida.Muda) { r.Como = (r.Como.Length > 0 ? r.Como + "; " : "") + "medida: muda"; r.TechoDb = RocolaOsCadenasNuevas.C; return 0.0; }
                RocolaOsDespues(r, r.Medida.Tercios, r.Medida.TpDb, r.Medida.PicosEq, out dl, out dtp, out ot);
                r.Como = (r.Como.Length > 0 ? r.Como + "; " : "") + "medida " + RocolaOsD1(r.Medida.Lufs) + " LUFS, " + RocolaOsD1(r.Medida.TpDb) + " dBTP" + (dl != 0 || dtp != 0 ? " (despues +" + RocolaOsD1(dl) + " LU, +" + RocolaOsD1(dtp) + " dB de pico)" : "");
                return RocolaOsAnota(r, r.Medida.Lufs + dl, r.Medida.TpDb, r.Medida.PicosEq, dtp, ot);
            }
            // 3) LA SUSTITUCION: la de tu MP3
            if (sombra && original != null && original.Path != null)
            {
                RocolaOsMedidaNueva mp3 = RocolaOsMedidaTabla.Valida(original.Path, r.Cadena);
                if (mp3 != null && mp3.Alg == RocolaOsMedidaNueva.Algoritmo && !mp3.Muda)
                {
                    RocolaOsDespues(r, mp3.Tercios, mp3.TpDb, mp3.PicosEq, out dl, out dtp, out ot);
                    r.Como = "la de Spotify sin medir: con la medida de tu MP3 (" + RocolaOsD1(mp3.Lufs) + " LUFS, " + RocolaOsD1(mp3.TpDb) + " dBTP)";
                    return RocolaOsAnota(r, mp3.Lufs + dl, mp3.TpDb, mp3.PicosEq, dtp, ot);
                }
            }
            // 4) SPOTIFY LA PRIMERA VEZ: fija, estimada de las de Spotify medidas
            if (spotify && !sombra)
            {
                double l, tp; string como;
                RocolaOsSpotifyEstimada(r, out l, out tp, out como);
                RocolaOsDespues(r, null, tp, null, out dl, out dtp, out ot);
                r.Como = "Spotify la primera vez: fija, estimada (" + como + ")";
                return RocolaOsAnota(r, l + dl, tp, null, dtp, ot);
            }
            // 5) SIN MEDIR: las tablas viejas de XP, conservador
            string p = sombra && original != null ? original.Path : t.Path;
            double le, tpe; string ce;
            RocolaOsEstimaVieja(p, out le, out tpe, out ce);
            RocolaOsDespues(r, null, tpe, null, out dl, out dtp, out ot);
            r.Como = (r.Como.Length > 0 ? r.Como + "; " : "") + "sin medir: " + ce;
            return RocolaOsAnota(r, le + dl, tpe, null, dtp, ot);
        }

        // LA RACHA (b10): la carpeta de la racha que va y el modo que decidio su primera pista. true con la ganancia del disco si va por disco; false
        // si va cancion por cancion (r.Como lo dice) o no hay racha.
        private string rocolaOsRachaCarpeta; private bool rocolaOsRachaDisco;
        private bool RocolaOsRacha(RocolaOsReceta r, Track original, string ruta, out double g)
        {
            g = 0;
            string carpeta = CarpetaDe(original.Path);
            if (string.IsNullOrEmpty(carpeta)) return false;
            // la que sono (o suena) justo antes de esta, como RachaDeDisco
            PlayItem np = nowPlaying, antes = null;
            if (np != null && ReferenceEquals(np.Song, original)) antes = sonoAntes;
            else if (queue.Count > 0 && queue[0] != null && ReferenceEquals(queue[0].Song, original)) antes = np;
            bool sigue = rocolaOsRachaCarpeta != null && string.Equals(rocolaOsRachaCarpeta, carpeta, StringComparison.OrdinalIgnoreCase) && MismaCarpeta(antes, carpeta);
            List<Track> disco = RachaDeDisco(original);
            bool medida = r.Medida != null && !r.Medida.Muda;
            if (!sigue)
            {   // la PRIMERA de la racha (o suelta): decide el modo con las reglas de siempre
                bool porDisco = disco != null && medida && RocolaOsGananciaDisco(r, disco, ruta, true, out g);
                rocolaOsRachaCarpeta = carpeta; rocolaOsRachaDisco = porDisco;
                return porDisco;
            }
            if (rocolaOsRachaDisco && disco != null && medida && RocolaOsGananciaDisco(r, disco, ruta, false, out g)) return true;
            if (!rocolaOsRachaDisco) r.Como = "la racha va cancion por cancion (lo decidio su primera pista)";
            return false;
        }

        // la estimacion conservadora de lo que XP ya sabe: su integrada (LKFS, del decodificado de 16 bits) y su pico + 3 dB; sin integrada, su
        // estimacion K-RMS (sin picos: +3 dBTP); sin nada, -8 LUFS y +3 dBTP
        private void RocolaOsEstimaVieja(string ruta, out double l, out double tp, out string como)
        {
            l = -8.0; tp = RocolaOsEstimaPicoDb; como = "nada de XP: -8 LUFS y +3 dBTP supuestos";
            if (ruta == null) return;
            SonoridadIntegrada s = new SonoridadIntegrada(); bool hay;
            lock (integCerrojo) hay = integCache != null && integCache.TryGetValue(ruta, out s);
            if (hay && s.Valida && !s.Muda && !float.IsNaN(s.Lkfs))
            {
                l = s.Lkfs;
                tp = s.ConPicos ? s.PicoDb + RocolaOsEstimaPicoDb : RocolaOsEstimaPicoDb;
                como = "la integrada de XP " + RocolaOsD1(l) + " LKFS y " + (s.ConPicos ? "su pico " + RocolaOsD1(s.PicoDb) + " + 3 dB" : "+3 dBTP supuestos");
                return;
            }
            float rms;
            if (loudCache != null && loudCache.TryGetValue(ruta, out rms) && rms > 1e-5f)
            {
                l = 2.32 + 20.0 * Math.Log10(rms);
                tp = RocolaOsEstimaPicoDb;
                como = "la estimacion K-RMS de XP " + RocolaOsD1(l) + " LKFS y +3 dBTP supuestos";
            }
        }

        // SPOTIFY LA PRIMERA VEZ: la mediana de las de Spotify medidas en la cadena nueva (L' con su espectro por lo que va despues); con menos de 5, las
        // de XP (rocola_spotify_niveles.txt: LKFS y pico de muestra + 2 dB); sin nada, -8 LUFS y +1 dBTP
        private void RocolaOsSpotifyEstimada(RocolaOsReceta r, out double l, out double tp, out string como)
        {
            List<double> ls = new List<double>(), tps = new List<double>();
            foreach (RocolaOsMedidaNueva m in RocolaOsMedidaTabla.DeSpotify(r.Cadena))
            {
                if (m.Muda) continue;
                ls.Add(m.Lufs); tps.Add(m.TpDb);
            }
            if (ls.Count >= RocolaOsSpotifyMinimo)
            {
                l = RocolaOsMediana(ls); tp = RocolaOsMediana(tps) + RocolaOsSpotifyMargenDb;
                como = "mediana de " + ls.Count + " de Spotify medidas: " + RocolaOsD1(l) + " LUFS, " + RocolaOsD1(tp - RocolaOsSpotifyMargenDb) + " dBTP + 1,5 dB";
                return;
            }
            ls.Clear(); tps.Clear();
            foreach (RocolaOsSpotifyNivela.Medida m in RocolaOsSpotifyNivela.Tabla.Values)
            {
                if (float.IsNaN(m.Lkfs) || float.IsInfinity(m.Lkfs) || m.Lkfs < -60f) continue;
                ls.Add(m.Lkfs); tps.Add(m.PicoDb);
            }
            if (ls.Count >= RocolaOsSpotifyMinimo)
            {
                l = RocolaOsMediana(ls); tp = RocolaOsMediana(tps) + 2.0;
                como = "mediana de " + ls.Count + " medidas por XP: " + RocolaOsD1(l) + " LKFS, pico " + RocolaOsD1(tp - 2.0) + " + 2 dB";
                return;
            }
            l = -8.0; tp = 1.0; como = "sin bastantes medidas: -8 LUFS y +1 dBTP supuestos";
        }
        internal static double RocolaOsMediana(List<double> v)
        {
            if (v == null || v.Count == 0) return double.NaN;
            List<double> s = new List<double>(v); s.Sort();
            int n = s.Count;
            return n % 2 == 1 ? s[n / 2] : 0.5 * (s[n / 2 - 1] + s[n / 2]);
        }
        // LA DISPERSION de las L de Spotify medidas (desviacion tipica, LU) y cuantas: si es muy chica, Soloist ya las entrega normalizadas
        internal static double RocolaOsDispersionSpotify(char cadena, out int n, out double media)
        {
            List<double> ls = new List<double>();
            foreach (RocolaOsMedidaNueva m in RocolaOsMedidaTabla.DeSpotify(cadena)) if (!m.Muda) ls.Add(m.Lufs);
            n = ls.Count; media = double.NaN;
            if (n < 2) return double.NaN;
            double s = 0; foreach (double v in ls) s += v; media = s / n;
            double q = 0; foreach (double v in ls) q += (v - media) * (v - media);
            return Math.Sqrt(q / (n - 1));
        }

        // EL DISCO: la ganancia unica de la racha (como GananciaDeDisco, con las medidas nuevas)
        private bool RocolaOsGananciaDisco(RocolaOsReceta r, List<Track> pistas, string ruta, bool recopilatorio, out double g)
        {
            g = 0;
            double peso = 0, pot = 0, tpMax = -200; int medidas = 0; bool esta = false;
            double lMin = double.MaxValue, lMax = double.MinValue;
            foreach (Track tr in pistas)
            {
                if (tr == null || tr.Path == null) continue;
                RocolaOsMedidaNueva m = string.Equals(tr.Path, ruta, StringComparison.OrdinalIgnoreCase) ? r.Medida : RocolaOsMedidaTabla.Busca(tr.Path, r.Cadena);
                if (m == null || m.Alg != RocolaOsMedidaNueva.Algoritmo || m.Muda) continue;
                double dl, dtp, ot;
                RocolaOsDespues(r, m.Tercios, m.TpDb, m.PicosEq, out dl, out dtp, out ot);
                if (string.Equals(tr.Path, ruta, StringComparison.OrdinalIgnoreCase)) { r.TpAntesEqDb = m.TpDb; r.PicosEq = m.PicosEq; r.DtpOtrosDb = ot; r.TechoDb = RocolaOsCadenasNuevas.C - (m.TpDb + dtp); }
                double lp = m.Lufs + dl, w = Math.Max(1.0, m.Segundos);
                pot += w * Math.Pow(10.0, lp / 10.0); peso += w; medidas++;
                if (m.Segundos >= 30) { if (m.Lufs < lMin) lMin = m.Lufs; if (m.Lufs > lMax) lMax = m.Lufs; }
                tpMax = Math.Max(tpMax, m.TpDb + dtp);
                if (string.Equals(tr.Path, ruta, StringComparison.OrdinalIgnoreCase)) esta = true;
            }
            if (!esta || medidas < 2 || medidas * 2 < pistas.Count || !(peso > 0)) return false;
            if (recopilatorio && lMax > lMin && lMax - lMin > DispersionDiscoDb) return false;   // recopilatorio: cancion por cancion (solo al decidir el modo de la racha, en su primera pista: b10)
            double l = 10.0 * Math.Log10(pot / peso);
            g = RocolaOsG(l, tpMax);
            r.Como = "el disco (" + medidas + " de " + pistas.Count + " pistas medidas): " + RocolaOsD1(l) + " LUFS, pico real " + RocolaOsD1(tpMax) + " dBTP";
            return true;
        }

        // EL TONO PAREJO de una cancion (tono_parejo): lo que su huella de tono (rocola_eq.txt de XP, 8 bandas) se aparta del objetivo (el de XP:
        // la media de las sanas o su IA), a la mitad, y de +-3 dB como mucho; nada por encima de su corte de agudos; una cancion muy lejos del
        // objetivo se toca menos (como XP: queria sonar asi). null si no tiene huella o no hay objetivo.
        private float[] RocolaOsTonoPedido(string ruta)
        {
            float[] c;
            if (ruta == null || eqAvg == null || eqCache == null) return null;
            if (!(eqCache.TryGetValue(ruta, out c) || RocolaOsSpotifyEqHuella(ruta, false, out c)) || c == null || c.Length < EqLen || c[EqFingerprint] != EqVersionOriginal) return null;
            float[] meta = MetaDeCancion(c) ?? eqAvg;
            float corte = c[EqCut];
            double dif = 0; int nb = 0;
            for (int b = 0; b < 8; b++) { if (corte > 100f && EqCentros[b] > corte / 1.25f) continue; dif += Math.Abs(meta[b] - c[b]); nb++; }
            if (nb == 0) return null;
            dif /= nb;
            double fuerza = dif <= 3.0 ? 0.5 : Math.Max(0.25, 0.5 - 0.05 * (dif - 3.0));
            float[] p = new float[8]; bool algo = false;
            for (int b = 0; b < 8; b++)
            {
                if (corte > 100f && EqCentros[b] > corte / 1.25f) continue;   // por encima del corte no hay musica que subir
                double v = fuerza * (meta[b] - c[b]);
                v = Math.Max(-RocolaOsTonoMaxDb, Math.Min(RocolaOsTonoMaxDb, v));
                if (Math.Abs(v) < 0.1) v = 0;
                p[b] = (float)v; if (v != 0) algo = true;
            }
            return algo ? p : null;
        }
    }
}

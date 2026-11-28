// EL EQ AUTOMATICO Y EL BRILLO A LA MEDIDA DE LAS CANCIONES DE SPOTIFY PEDIDAS (fase 11, funciones «spotify_eq» y «spotify_brillo», encendidas
// de fabrica; FASE11_spotify-nivela.md, seccion 7). Decision del dueno del 22-11-2026 (eligio las dos mejoras de sonido para Spotify). Las del
// buscador y del catalogo («spotify:track:ID», RocolaOsFuenteSpotify) no aprendian nada (g23: R7), asi que nunca tenian huella de tono: sin EQ
// automatico (EqCorrFor daba null) y con el realce de agudos de una sin medir (ExciteFactorFor: 1). Ahora:
//   1. APRENDE: si la funcion esta encendida, una pedida que empieza desde el principio se mide con LA MISMA MEDIDA DE LA CADENA que tus MP3
//      (g250: su EqRuta; el LevelTap junta la energia de las 8 bandas y el DSEE -SpectralTap- mide su acantilado, el corte de agudos real: con
//      Soloist, Ogg Vorbis 160, unos 16 kHz). Al cerrarse la medida (g251, CierraHuellaDeFuente: lo de XP no la ve) y SOLO si sono entera (97 %
//      de su duracion y 30 s o mas) se guarda la huella como la arma CierraHuella de XP (las 8 bandas contra la mediana del cuerpo, el corte y el
//      corte de abajo) por id en rocola_spotify_eq.txt (en los datos de rocola-xp, aparte de rocola_eq.txt: ni el promedio de la biblioteca, ni
//      el repaso, ni el informe de calidad ven rutas que no son archivos).
//   2. DESDE LA SEGUNDA VEZ: EqCorrCancion (g252), ExciteFactorFor (g253) y lo que le falta de cuerpo en el billar (g254, FaltaDeCuerpoDb, la
//      cancion «flaca») leen esa huella como si estuviera en eqCache: el mismo calculo de XP de punta a punta (la fuerza automatica, el objetivo
//      por cancion de la IA del EQ, la cancion rara, las reglas del modo billar con la afinacion del local, el corte para no tocar lo que el
//      codec borro; el realce con su corte, la mitad del extra con el DSEE y lo de «a la medida»). spotify_eq decide el EQ (y lo flaco del
//      billar); spotify_brillo, el realce.
// La sustitucion invisible (la sombra de una tuya, RocolaOs.Calidad*.cs, g61): su uri nunca se mide aqui (su lector es otro) y una uri que es
// pareja de una de tus canciones no se lee de esta tabla. Desde el 27-11-2026 la sombra usa la huella de TU MP3 (RocolaOsMp3EqDeSombra,
// RocolaOs.Calidad.cs): su EQ automatico, su realce y lo demas de la cadena (FASE11_spotify-nivela.md, seccion 8). Sin Rocola OS, sin el modo Spotify o con las dos funciones apagadas: como
// antes, bit a bit (ningun gancho cambia nada con una ruta que no es «spotify:»).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static class RocolaOsSpotifyEq
    {
        internal const string Nombre = "spotify_eq", NombreBrillo = "spotify_brillo";
        internal const string Archivo = "rocola_spotify_eq.txt";
        internal const string Cabecera = "ROCOLA-SPOTIFY-EQ v1";
        internal const int TopeTabla = 20000;
        internal const double MinimoSonadoSeg = 30.0, MinimoFraccion = 0.97;   // como spotify_nivela: solo de las que sonaron enteras

        // la huella de una cancion: las 8 bandas (dB contra la mediana del cuerpo), el corte de agudos y el de abajo (Hz; 0 = no se supo)
        internal sealed class Huella { internal float[] Bandas; internal float Corte, CorteBajo; internal long Unix; internal float[] Eq; }
        private static readonly ConcurrentDictionary<string, Huella> tabla = new ConcurrentDictionary<string, Huella>(StringComparer.Ordinal);
        internal static ConcurrentDictionary<string, Huella> Tabla { get { return tabla; } }
        private static volatile bool cargada;
        private static int eq, brillo, hilo;
        internal static bool Va { get { return cargada && RocolaOsSpotify.Encendido; } }
        internal static bool VaEq { get { return Volatile.Read(ref eq) != 0 && Va && RocolaOs.Encendida(Nombre); } }
        internal static bool VaBrillo { get { return Volatile.Read(ref brillo) != 0 && Va && RocolaOs.Encendida(NombreBrillo); } }
        internal static long Medidas, Descartadas;
        private static readonly AutoResetEvent hay = new AutoResetEvent(false);
        private static volatile bool parar, guardar;

        internal static void ArrancaEq() { Volatile.Write(ref eq, 1); Hilo(); }
        internal static void ArrancaBrillo() { Volatile.Write(ref brillo, 1); Hilo(); }
        private static void Hilo() { if (Interlocked.Exchange(ref hilo, 1) == 0) RocolaOs.Hilo(Nombre, Corre); }
        // (al cerrar: lo que falte por guardar se guarda aqui, en el hilo del cierre; el de la funcion es de fondo y puede no llegar)
        internal static void Para() { parar = true; if (guardar) { guardar = false; Guarda(); } hay.Set(); }
        internal static void Apunta(string que) { RocolaOs.Apunta(Nombre, que); }
        internal static void Guardala() { guardar = true; hay.Set(); }

        private static void Corre()
        {
            int n = Carga();
            cargada = true;
            Apunta("encendida (" + (Volatile.Read(ref eq) != 0 ? "EQ" : "") + (Volatile.Read(ref eq) != 0 && Volatile.Read(ref brillo) != 0 ? " y " : "")
                + (Volatile.Read(ref brillo) != 0 ? "brillo" : "") + "): " + n.ToString(CultureInfo.InvariantCulture) + " canciones de Spotify con su huella en " + Archivo);
            while (!parar)
            {
                hay.WaitOne(5000);
                if (guardar) { guardar = false; Guarda(); }
            }
        }

        // ---------- la tabla ----------
        internal static string RutaTabla { get { return RocolaOs.Datos(Archivo); } }
        private static int Carga()
        {
            string[] ls;
            try { ls = Form1.RocolaOsLeeLineas(RutaTabla); } catch { ls = null; }
            if (ls == null) return 0;
            foreach (string l in ls)
            {
                if (string.IsNullOrEmpty(l) || l[0] == '#' || l == Cabecera) continue;
                string[] c = l.Split('\t');
                if (c.Length < 12) continue;
                string id = c[0];
                if (RocolaOsSpotify.IdBase62("spotify:track:" + id, "spotify:track:").Length == 0) continue;
                float[] b = new float[8]; bool bien = true;
                for (int i = 0; i < 8; i++) { b[i] = Fl(c[1 + i]); if (float.IsNaN(b[i]) || float.IsInfinity(b[i])) bien = false; }
                float corte = Fl(c[9]), bajo = Fl(c[10]);
                if (!bien || float.IsNaN(corte) || float.IsNaN(bajo) || corte < 0f || corte > 30000f || bajo < 0f) continue;
                tabla[id] = new Huella { Bandas = b, Corte = corte, CorteBajo = bajo, Unix = L(c[11]) };
            }
            Recorta();
            return tabla.Count;
        }
        private static void Recorta()
        {
            if (tabla.Count <= TopeTabla) return;
            List<KeyValuePair<string, Huella>> l = new List<KeyValuePair<string, Huella>>(tabla);
            l.Sort((a, b) => a.Value.Unix.CompareTo(b.Value.Unix));
            for (int i = 0; i < l.Count && tabla.Count > TopeTabla; i++) { Huella z; tabla.TryRemove(l[i].Key, out z); }
        }
        private static readonly object cerrojoGuarda = new object();
        private static void Guarda() { lock (cerrojoGuarda) GuardaYa(); }
        private static void GuardaYa()
        {
            Recorta();
            StringBuilder sb = new StringBuilder();
            sb.Append(Cabecera).Append('\n');
            sb.Append("# id_spotify\tb50\tb158\tb300\tb630\tb1k3\tb2k6\tb5k6\tb10k\t(dB contra la mediana del cuerpo)\tcorte_hz\tcorte_bajo_hz\tmedida_unix\n");
            List<string> ids = new List<string>(tabla.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string k in ids)
            {
                Huella h; if (!tabla.TryGetValue(k, out h)) continue;
                sb.Append(k);
                for (int i = 0; i < 8; i++) sb.Append('\t').Append(Fs(h.Bandas[i]));
                sb.Append('\t').Append(Fs(h.Corte)).Append('\t').Append(Fs(h.CorteBajo)).Append('\t').Append(h.Unix.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            try { Form1.RocolaOsEscribeSeguro(RutaTabla, sb.ToString()); }
            catch (Exception ex) { RocolaOs.Falla(Nombre, "guardar " + Archivo, ex); }
        }
        private static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
        private static float Fl(string s) { float v; return s != "-" && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : float.NaN; }
        private static string Fs(float v) { return float.IsNaN(v) || float.IsInfinity(v) ? "-" : v.ToString("0.###", CultureInfo.InvariantCulture); }
        internal static string F0(double x) { return x.ToString("0", CultureInfo.InvariantCulture); }
        internal static string F1(double x) { return x.ToString("0.0", CultureInfo.InvariantCulture); }
        internal static string F2(double x) { return x.ToString("0.00", CultureInfo.InvariantCulture); }

        // el id de una pedida (null: no es una cancion de Spotify)
        internal static string IdDe(string uri)
        {
            if (uri == null || !uri.StartsWith("spotify:track:", StringComparison.Ordinal)) return null;
            string id = RocolaOsSpotify.IdBase62(uri, "spotify:track:"); return id.Length > 0 ? id : null;
        }
        // ¿es la pareja de una de tus canciones? (la sombra de la sustitucion invisible usa la misma uri: lo suyo no se toca)
        internal static bool EsPareja(string id)
        {
            foreach (RocolaOsCalidad.Pareja p in RocolaOsCalidad.Pares.Values) if (p != null && string.Equals(p.Id, id, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public partial class Form1
    {
        // EL CORTE DE AGUDOS DE UNA PEDIDA, en la fuente (en el hilo del sonido; la memoria se reserva al crearlo). XP saca el corte de tus MP3
        // de la ficha de calidad del repaso (un SpectralTap solo de analisis: cuadros de 2048 del canal 0 con sonido, IaWarmFrames + 24, del 10
        // al 90 % de la cancion) y, mientras suena, del DSEE (solo si esta encendido: apagado no analiza). Una de Spotify no pasa por el repaso:
        // aqui se hace lo mismo que su ficha de calidad con lo que llega de Soloist (los cuadros repartidos del 10 al 90 %). Al cerrar la
        // huella manda el del DSEE si lo midio (como en tus MP3); si no, este.
        private sealed class RocolaOsSpotifyCorte
        {
            private readonly SpectralTap tap;
            private readonly float[] cuadro;
            private int lleno, vistos;
            private readonly long desde, hasta, paso;   // en cuadros de sonido
            private long sonido, siguiente;
            internal RocolaOsSpotifyCorte(int hz, int durMs)
            {
                tap = new SpectralTap(hz, 1);
                cuadro = new float[tap.FrameLen];
                long dur = durMs > 0 ? (long)durMs * hz / 1000 : 0;
                desde = dur > 0 ? dur / 10 : 0; hasta = dur > 0 ? dur * 9 / 10 : long.MaxValue;
                long hay = dur > 0 ? (hasta - desde) / Cuantos : 0;
                paso = Math.Max(cuadro.Length, hay > 0 ? hay : (long)hz);   // sin duracion: uno por segundo
                siguiente = desde;
            }
            private const int Cuantos = IaWarmFrames + 24;   // los de la ficha de calidad (Form1.Sonido.cs)
            internal float CorteHz { get { return tap.CutHz; } }
            internal int Vistos { get { return vistos; } }
            internal void Procesa(Span<float> d, int n)
            {
                if (vistos >= Cuantos) return;
                for (int i = 0; i < n; i++, sonido++)
                {
                    if (lleno == 0 && (sonido < siguiente || sonido >= hasta)) continue;
                    cuadro[lleno++] = d[i * 2];
                    if (lleno < cuadro.Length) continue;
                    lleno = 0; siguiente = sonido + 1 + paso - cuadro.Length;
                    double e = 0; for (int q = 0; q < cuadro.Length; q++) e += cuadro[q] * cuadro[q];
                    if (e / cuadro.Length <= 1e-8) { siguiente = sonido + 1; continue; }   // sin sonido: el siguiente
                    try { tap.AnalyzeFrame(cuadro, 0); } catch { vistos = Cuantos; return; }
                    if (++vistos >= Cuantos) return;
                }
            }
        }
        private static readonly ConcurrentDictionary<string, RocolaOsSpotifyCorte> rocolaOsSpotifyCortes = new ConcurrentDictionary<string, RocolaOsSpotifyCorte>(StringComparer.Ordinal);
        // (desde RocolaOsSpotifyFuente, al abrir una pedida) su analizador del corte si se va a aprender su huella; null: como antes
        private RocolaOsSpotifyCorte RocolaOsSpotifyCortePara(string uri, int hz, int durMs)
        {
            if (!RocolaOs.Activo || !(RocolaOsSpotifyEq.VaEq || RocolaOsSpotifyEq.VaBrillo)) return null;
            string id = RocolaOsSpotifyEq.IdDe(uri);
            if (id == null || RocolaOsSpotifyEq.Tabla.ContainsKey(id) || RocolaOsSpotifyEq.EsPareja(id)) return null;
            RocolaOsSpotifyCorte c;
            try { c = new RocolaOsSpotifyCorte(hz, durMs); } catch { return null; }
            rocolaOsSpotifyCortes[uri] = c;
            return c;
        }

#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraSpotifyEq()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsSpotifyEq.Nombre,
                Que = "el EQ automatico de las canciones de Spotify pedidas: aprende su huella de tono al sonar entera y desde la siguiente vez la corrige como a tus MP3 (necesita «spotify»)",
                DeFabrica = false, SoloEnLaLista = true,   // (28-11-2026, «La cadena vieja borrada»: de la cadena de XP; con ROCOLA_OS=1 ya no va)
                Arranca = f => RocolaOsSpotifyEq.ArrancaEq(),
                Vivo = (f, sb) => sb.Append("spotify_eq huellas=").Append(RocolaOsSpotifyEq.Tabla.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" nuevas=").Append(Interlocked.Read(ref RocolaOsSpotifyEq.Medidas).ToString(CultureInfo.InvariantCulture))
                    .Append(" descartadas=").Append(Interlocked.Read(ref RocolaOsSpotifyEq.Descartadas).ToString(CultureInfo.InvariantCulture)).Append('\n'),
                ParaAlCerrar = razon => RocolaOsSpotifyEq.Para(),
            });
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsSpotifyEq.NombreBrillo,
                Que = "el realce de agudos de las canciones de Spotify pedidas con su corte de agudos medido (Soloist: Vorbis 160), como a tus MP3 (necesita «spotify»)",
                DeFabrica = false, SoloEnLaLista = true,   // (28-11-2026, «La cadena vieja borrada»: de la cadena de XP; con ROCOLA_OS=1 ya no va)
                Arranca = f => RocolaOsSpotifyEq.ArrancaBrillo(),
                ParaAlCerrar = razon => RocolaOsSpotifyEq.Para(),
            });
        }

        // g252, g253 y g254 (EqCorrCancion, ExciteFactorFor y FaltaDeCuerpoDb, en Codigo/): la huella de una de Spotify pedida, con la forma de las
        // de eqCache (EqLen numeros). paraRealce: la pide el realce (spotify_brillo); si no, el EQ (spotify_eq). false: como antes (sin huella).
        private bool RocolaOsSpotifyEqHuella(string path, bool paraRealce, out float[] c)
        {
            c = null;
            if (!RocolaOs.Activo || path == null || !path.StartsWith("spotify:track:", StringComparison.Ordinal)) return false;
            // (27-11-2026) LA SOMBRA DE UNA TUYA (la sustitucion invisible, g61) suena con la huella de TU MP3: su EQ automatico y su realce, como
            // si sonara tu archivo. La misma huella de eqCache (lo de XP, con su ficha de version); sin ella, como antes (sin EQ).
            if (RocolaOsMp3EqDeSombra(path, out c)) return true;
            if (paraRealce ? !RocolaOsSpotifyEq.VaBrillo : !RocolaOsSpotifyEq.VaEq) return false;
            string id = RocolaOsSpotifyEq.IdDe(path);
            RocolaOsSpotifyEq.Huella h;
            if (id == null || !RocolaOsSpotifyEq.Tabla.TryGetValue(id, out h) || h == null) return false;
            if (RocolaOsSpotifyEq.EsPareja(id)) return false;   // la sombra de una tuya: lo de la sustitucion, tal cual
            float[] e = h.Eq;
            if (e == null)
            {
                e = new float[EqLen];
                for (int b = 0; b < 8; b++) e[b] = h.Bandas[b];
                e[EqCut] = h.Corte; e[EqCutLo] = h.CorteBajo; e[EqClip] = 0f;   // el recorte no se mide aqui (solo el repaso de un archivo)
                e[EqFingerprint] = EqVersionOriginal;
                h.Eq = e;   // (de solo lectura despues: XP no escribe en lo que lee)
            }
            c = e;
            return true;
        }

        // g250 (Codigo/Form1.Sonido.cs, ArmaCadena): la huella que se aprende de una de Spotify pedida (su uri) o null (como antes: nada). Solo
        // desde el principio, de la fuente de una pedida (no la sombra de la sustitucion) y si aun no tiene huella. Y lo dice en la bitacora de
        // Spotify (la primera vez: se mide; despues, lo que le toca de EQ y de realce).
        private string RocolaOsSpotifyEqRuta(string path, IAudioSource source, double saltadoSeg, bool iaMejorada)
        {
            if (!RocolaOs.Activo || path == null || !path.StartsWith("spotify:track:", StringComparison.Ordinal) || !(source is RocolaOsFuenteSpotify)) return null;
            bool veq = RocolaOsSpotifyEq.VaEq, vbr = RocolaOsSpotifyEq.VaBrillo;
            if (!veq && !vbr) return null;
            string id = RocolaOsSpotifyEq.IdDe(path);
            if (id == null || RocolaOsSpotifyEq.EsPareja(id)) return null;
            if (RocolaOsSpotifyEq.Tabla.ContainsKey(id))
            {
                StringBuilder sb = new StringBuilder("eq de " + id + ": con su huella (corte " + RocolaOsSpotifyEq.F0(RocolaOsSpotifyEq.Tabla[id].Corte) + " Hz)");
                if (veq)
                {
                    float[] d = EqCorrFor(path);
                    sb.Append("; EQ automatico ");
                    if (d == null) sb.Append("sin correccion");
                    else for (int b = 0; b < d.Length; b++) sb.Append(b == 0 ? "[" : " ").Append(d[b].ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)).Append(b == d.Length - 1 ? "] dB" : "");
                }
                if (vbr) sb.Append("; realce ").Append(RocolaOsSpotifyEq.F2(ExciteAmountFor(path))).Append(" (sin medir ").Append(RocolaOsSpotifyEq.F2(ExciteAmountFor(null))).Append(')');
                RocolaOsSpotify.Apunta(sb.ToString());
                return null;
            }
            if (saltadoSeg > 0 || iaMejorada || !(vbr || EqMideHuella)) return null;
            RocolaOsSpotify.Apunta("eq de " + id + ": sin huella (sin EQ automatico y el realce de una sin medir); se mide para la proxima");
            return path;
        }

        // g251 (Codigo/FuentesMedidas.cs, CierraHuellaDeFuente): la medida de una de Spotify se cierra aqui (true: XP no la ve, ni eqCache ni
        // rocola_eq.txt). Las mismas cuentas que CierraHuella (Codigo/Form1.Datos.cs), pero solo si sono entera. false: no es de Spotify.
        private bool RocolaOsSpotifyEqCierra(string ruta, double[] sums, long n, int tasa, float corte, float corteBajo)
        {
            if (!RocolaOs.Activo || ruta == null || !ruta.StartsWith("spotify:", StringComparison.Ordinal)) return false;
            string id = RocolaOsSpotifyEq.IdDe(ruta);
            if (id == null || sums == null || sums.Length < 8 || tasa <= 0 || !RocolaOsSpotifyEq.Va) return true;
            double seg = n / (double)tasa;
            RocolaOsSpotifyPista p; int durMs = RocolaOsSpotify.Pistas.TryGetValue(ruta, out p) && p != null ? p.DuracionMs : 0;
            if (seg < RocolaOsSpotifyEq.MinimoSonadoSeg || (durMs > 0 && seg * 1000.0 < RocolaOsSpotifyEq.MinimoFraccion * durMs))
            {
                RocolaOsSpotifyCorte z; rocolaOsSpotifyCortes.TryRemove(ruta, out z);
                Interlocked.Increment(ref RocolaOsSpotifyEq.Descartadas);
                RocolaOsSpotify.Apunta("eq de " + id + ": no sono entera (" + RocolaOsSpotifyEq.F1(seg) + " s" + (durMs > 0 ? " de " + RocolaOsSpotifyEq.F1(durMs / 1000.0) : "") + "): su huella no se guarda");
                return true;
            }
            RocolaOsSpotifyCorte an; string deQuien = "el DSEE";
            if (rocolaOsSpotifyCortes.TryRemove(ruta, out an) && an != null && !(corte > 0f) && an.CorteHz > 0f) { corte = an.CorteHz; deQuien = "la fuente, " + an.Vistos.ToString(CultureInfo.InvariantCulture) + " cuadros"; }
            float[] curve = new float[8];
            for (int b = 0; b < 8; b++) curve[b] = (float)(10.0 * Math.Log10(sums[b] / n + 1e-12));
            float[] cuerpo = { curve[1], curve[2], curve[3], curve[4], curve[5] };
            Array.Sort(cuerpo);
            float refDb = cuerpo[2];
            for (int b = 0; b < 8; b++) curve[b] -= refDb;
            if (!Finitos(curve)) return true;   // una medida rota no se guarda
            RocolaOsSpotifyEq.Tabla[id] = new RocolaOsSpotifyEq.Huella { Bandas = curve, Corte = corte > 0f ? corte : 0f, CorteBajo = corteBajo > 0f ? corteBajo : 0f, Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            Interlocked.Increment(ref RocolaOsSpotifyEq.Medidas);
            RocolaOsSpotifyEq.Guardala();
            StringBuilder sb = new StringBuilder("eq de " + id + ": huella aprendida en " + RocolaOsSpotifyEq.F1(seg) + " s, corte de agudos " + (corte > 0f ? RocolaOsSpotifyEq.F0(corte) + " Hz (" + deQuien + ")" : "sin medir") + ", bandas");
            for (int b = 0; b < 8; b++) sb.Append(' ').Append(curve[b].ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture));
            RocolaOsSpotify.Apunta(sb.Append(" dB (EQ automatico y realce a la medida desde la proxima vez)").ToString());
            return true;
        }
    }
}

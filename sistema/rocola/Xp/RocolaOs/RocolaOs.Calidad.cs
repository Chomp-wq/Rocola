// LA SUSTITUCION INVISIBLE (fase 11, funcion «calidad_spotify»; FASE11_sustitucion.md). Peticion del dueno del 15-11-2026: «Que no se te
// olvide agregar la funcion de que si una cancion de mi rocola existe en mejor calidad en Spotify, que tome esa version». Es SP2 del programa
// anterior (decisiones del 6 y 7-11-2026: SP2 con A, B y C, SP3, SP4 y el volumen V1-V3) traido a rocola-xp por ganchos, con las reglas de
// la cuota de 2026 (modo desarrollo: NADA de recorrer la biblioteca de fondo).
//
// LA FUNCION: «calidad_spotify», ENCENDIDA de fabrica (su clave calidad_spotify= en /rocola/datos/xp/rocola_os.txt; «calidad_spotify=0» la
// apaga desde el siguiente arranque). Necesita la funcion «spotify» (el nucleo: Soloist, el anillo, la fuente) y, para buscar, la llave de la
// API web (rocola_spotify_api.txt, la del catalogo). Sin ROCOLA_OS, sin el modo, sin esta funcion, sin la llave o sin sesion de Soloist: nada
// cambia y la rocola es la de XP (ni una peticion).
//
// COMO VA, de punta a punta:
//   1. CUANDO ENTRA A LA COLA (pagada) una cancion tuya (el latido, cada segundo, mira la que suena y la cola: solo las nuevas de la sesion),
//      se toma en la pantalla lo que XP ya midio de ella (el corte de agudos de rocola_eq.txt y el pre-eco de rocola_sonoridad.txt) y un
//      hilo propio lee su cabecera (kbps, CBR/VBR, frecuencia, ISRC, titulo, artista, disco, duracion) y decide si SUENA PEOR (regla A,
//      RocolaOs.CalidadReglas.cs).
//   2. LA PAREJA (regla B): si suena peor y no se sabe su pareja (o «sin pareja» de hace mas de 30 dias), se pregunta UNA vez a la API (dos si la
//      del ISRC no da nada) por la cola del catalogo con prioridad baja (RocolaOs.CalidadApi.cs). Lo encontrado -o «sin pareja», con fecha-
//      se guarda en rocola_spotify_pares.txt: la ruta del MP3, su tamano y su fecha, el id de Spotify, el ISRC y numeros (nada mas).
//   3. AL SONAR (gancho g13 de siempre: OpenAudio -> RocolaOsSpotifyAbre -> RocolaOsCalidadAbre, en el hilo que abre): con pareja, el archivo
//      igual y Soloist con sesion, suena desde Spotify por el nucleo (RocolaOs.CalidadFuente.cs envuelve la fuente de Spotify) SIN NINGUN
//      CAMBIO VISIBLE: la cola, lo que suena, la portada, el credito, el turno, lo mas sonado y las estadisticas son los de tu cancion (el
//      PlayItem de XP no cambia: solo cambia de donde sale el sonido). La cadena se arma con la ruta de Spotify (gancho g61: sus medidas, no
//      las de tu MP3) y se nivela con su propia medida (gancho g62: V1; la primera vez, la mediana de las de Spotify medidas si hay 5 o mas: V2;
//      nunca a media cancion). Si Spotify no empieza a sonar en 6 s, o se corta, o falla: TU MP3 TAL CUAL, por el camino de XP (el mismo
//      que retoma una cancion cuando vuelve el aparato de sonido), desde el principio si sono menos de 10 s y si no desde donde iba; sin
//      aviso y sin cobrar de mas (el credito ya se cobro por tu cancion y no se devuelve).
//   4. LA FORMA (regla C): la primera vez que una pareja suena desde Spotify, un hilo lee tu MP3 (nunca en la pantalla) y se compara su
//      volumen por tramos con el de lo que sono: r >= 0,80 confirmada, r < 0,55 otra (ya no se sustituye), en medio, la proxima vez.
//   5. EL RELEVO SIN PAUSA (gancho g60, RutaQueSonaria de XP: «el dia que esta rocola tenga otra fuente, se cambia aqui»): una cancion que
//      se va a sustituir se ve por la ruta de Spotify, asi que el calentador y el relevo no la abren como MP3 por delante: entra por el camino
//      de siempre (PlayNext), como una de Spotify.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Rocola
{
    internal static partial class RocolaOsCalidad
    {
        internal const string Nombre = "calidad_spotify";
        internal const string Archivo = "rocola_spotify_pares.txt";
        internal const string Cabecera = "ROCOLA-SPOTIFY-PARES v1";
        internal const long OlvidaSinParejaSeg = 30L * 24 * 3600;   // «sin pareja»: no se vuelve a preguntar en 30 dias
        internal const int EsperaMs = 6000;                          // Spotify tiene 6 s para empezar a sonar; si no, tu MP3
        internal const int FallosSesion = 2;                         // dos veces sin sonar en la sesion: esa pareja, ya no en esta sesion

        // UNA PAREJA (un renglon de rocola_spotify_pares.txt). Inmutable: al cambiar se pone otra (la leen otros hilos sin cerrojo).
        internal sealed class Pareja
        {
            internal string Ruta, Id, Isrc, Por;   // Id null: «sin pareja»; Por: «isrc» o «titulo»
            internal long Bytes, FechaTicks, BuscadaUnix;
            internal int DurSpMs, DurMp3Ms, Verif;  // Verif: 1 confirmada por la forma, 0 sin decidir, -1 otra grabacion (no se sustituye)
            internal float PicoDb = float.NaN, P99Db = float.NaN, P90Db = float.NaN, R = float.NaN;
            internal bool ConMedida { get { return !float.IsNaN(PicoDb) && !float.IsNaN(P99Db) && !float.IsNaN(P90Db); } }
            internal string Uri { get { return Id != null ? "spotify:track:" + Id : null; } }
            internal Pareja Copia() { return (Pareja)MemberwiseClone(); }
        }

        // ---------- lo que se sabe (desde cualquier hilo) ----------
        private static readonly ConcurrentDictionary<string, Pareja> pares = new ConcurrentDictionary<string, Pareja>(StringComparer.Ordinal);
        internal static ConcurrentDictionary<string, Pareja> Pares { get { return pares; } }
        private static volatile bool cargada;
        private static int encendida;
        // LA FUNCION VA en esta sesion: encendida, con el nucleo de Spotify encendido y la tabla leida
        internal static bool Va { get { return Volatile.Read(ref encendida) != 0 && cargada && RocolaOsSpotify.Encendido; } }
        internal static readonly ConcurrentDictionary<string, long> Vistas = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);    // (las mira el latido una vez por sesion)
        internal static readonly ConcurrentDictionary<string, byte> NoSustituir = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal); // una vez tu MP3 (volvio de Spotify)
        private static readonly ConcurrentDictionary<string, byte> pendientes = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        internal static readonly ConcurrentDictionary<string, int> Fallos = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        internal static readonly ConditionalWeakTable<object, object> Sombras = new ConditionalWeakTable<object, object>();   // (la Track de Spotify -> su fuente)
        internal static long Vistas_, Peores, Buenas, Pedidas, Parejas, SinPareja, DesdeSpotify, AlMp3, Confirmadas, Descartadas, Medidas;

        // ---------- el hilo ----------
        private static readonly ConcurrentQueue<object> trabajo = new ConcurrentQueue<object>();
        private static readonly AutoResetEvent hay = new AutoResetEvent(false);
        private static Thread hilo;
        private static volatile bool parar;
        internal static void Arranca()
        {
            Volatile.Write(ref encendida, 1);
            hilo = RocolaOs.Hilo(Nombre, Corre);
        }
        internal static void Para() { parar = true; hay.Set(); }
        internal static void Encola(object x) { trabajo.Enqueue(x); hay.Set(); }

        // lo que manda el latido: una cancion tuya que entro a la cola
        internal sealed class Candidata { internal string Ruta, Titulo, Artista, Disco; internal float Corte; internal bool PreEco; internal int DurXpMs; }
        // lo que contesto la API
        private sealed class Llego { internal Candidata C; internal RocolaOsCalidadMp3 Mp3; internal long Bytes, Fecha; internal int DurMs; internal object R; internal string Error; }
        // lo que dejo una cancion que sono (o empezo a sonar) desde Spotify
        internal sealed class Resultado
        {
            internal string Ruta, Id; internal bool Natural; internal double SonidoSeg; internal int Hz;
            internal MedidorBs1770 Medidor; internal double[] E; internal long[] N; internal Forma Forma; internal long Hasta;
        }
        // la forma de tu MP3 (la mide un hilo: RocolaOsCalidadMideForma)
        internal sealed class Forma { internal volatile double[] E; internal volatile long[] N; internal volatile bool Hecha; }

        private static long estadoEscrito, ultimoCambio = 1;
        private static readonly List<Resultado> esperandoForma = new List<Resultado>();
        private static void Corre()
        {
            Carga();
            cargada = true;
            Apunta("encendida: " + pares.Count.ToString(CultureInfo.InvariantCulture) + " canciones en " + Archivo
                + " (" + Contar(true).ToString(CultureInfo.InvariantCulture) + " con pareja)");
            Cambio();
            while (!parar)
            {
                object x;
                bool guardar = false;
                while (trabajo.TryDequeue(out x))
                {
                    try
                    {
                        if (x is Candidata) Evalua((Candidata)x);
                        else if (x is Llego) guardar |= Recibe((Llego)x);
                        else if (x is Resultado) guardar |= Cierra((Resultado)x);
                    }
                    catch (Exception ex) { RocolaOs.Falla(Nombre, "trabajo", ex); }
                }
                for (int i = 0; i < esperandoForma.Count; i++)
                {
                    Resultado r = esperandoForma[i];
                    if (r.Forma.Hecha || Environment.TickCount64 > r.Hasta) { esperandoForma.RemoveAt(i); i--; try { guardar |= Cierra(r); } catch (Exception ex) { RocolaOs.Falla(Nombre, "la forma", ex); } }
                }
                if (guardar) Guarda();
                if (Interlocked.Read(ref ultimoCambio) != estadoEscrito) { estadoEscrito = Interlocked.Read(ref ultimoCambio); EscribeEstado(); }
                hay.WaitOne(esperandoForma.Count > 0 ? 500 : 2000);
            }
        }
        internal static void Cambio() { Interlocked.Increment(ref ultimoCambio); }
        private static int Contar(bool conPareja) { int n = 0; foreach (Pareja p in pares.Values) if ((p.Id != null) == conPareja) n++; return n; }
        internal static void Apunta(string que) { RocolaOs.Apunta(Nombre, que); }
        private static string Nom(string ruta) { try { return Path.GetFileName(ruta); } catch { return ruta; } }

        // ---------- 1 y 2: ¿suena peor? ¿cual es su pareja? ----------
        private static void Evalua(Candidata c)
        {
            FileInfo fi = new FileInfo(c.Ruta);
            if (!fi.Exists) return;
            long bytes = fi.Length, fecha = fi.LastWriteTimeUtc.Ticks;
            RocolaOsCalidadMp3 i = LeeMp3(c.Ruta);
            string motivo;
            Interlocked.Increment(ref Vistas_);
            if (!SuenaPeor(c.Corte, c.PreEco, i, out motivo)) { Interlocked.Increment(ref Buenas); Cambio(); return; }
            Interlocked.Increment(ref Peores); Cambio();
            Pareja p;
            if (pares.TryGetValue(c.Ruta, out p) && p.Bytes == bytes && p.FechaTicks == fecha)
            {
                if (p.Id != null) return;   // ya tiene su pareja (o es otra grabacion: Verif -1, no se vuelve a buscar)
                long hace = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - p.BuscadaUnix;
                if (hace < OlvidaSinParejaSeg) { Apunta(Nom(c.Ruta) + " suena peor (" + motivo + "), sin pareja (buscada hace " + (hace / 86400).ToString(CultureInfo.InvariantCulture) + " dias): no se vuelve a preguntar"); return; }
            }
            string porQueNo = null;
            if (!RocolaOsSpotify.Listo) porQueNo = "Soloist sin sesion";
            else if (!RocolaOsSpotifyCatalogoApi.HayLlave) porQueNo = "sin la llave de la API";
            else if (RocolaOsSpotifyCatalogoApi.Frena) porQueNo = "Spotify frena (429)";
            if (porQueNo != null)
            {
                long v; Vistas.TryRemove(c.Ruta, out v);   // (la proxima vez que entre a la cola se vuelve a mirar)
                Apunta(Nom(c.Ruta) + " suena peor (" + motivo + "): no se busca su pareja (" + porQueNo + ")");
                return;
            }
            if (!pendientes.TryAdd(c.Ruta, 0)) return;
            string titulo = !string.IsNullOrEmpty(i.Titulo) ? i.Titulo : c.Titulo, artista = !string.IsNullOrEmpty(i.Artista) ? i.Artista : c.Artista;
            int dur = i.DuracionMs > 0 ? i.DuracionMs : c.DurXpMs;
            Llego l = new Llego { C = c, Mp3 = i, Bytes = bytes, Fecha = fecha, DurMs = dur };
            l.Mp3.Titulo = titulo; l.Mp3.Artista = artista; if (string.IsNullOrEmpty(l.Mp3.Disco)) l.Mp3.Disco = c.Disco;
            Interlocked.Increment(ref Pedidas); Cambio();
            Apunta(Nom(c.Ruta) + " suena peor (" + motivo + "): se busca su pareja" + (i.Isrc != null ? " por su ISRC" : " por titulo y artista"));
            RocolaOsSpotifyCatalogoApi.BuscaPareja(i.Isrc, titulo, ArtistaPrincipal(artista), delegate (object r, string e) { l.R = r; l.Error = e; Encola(l); });
        }
        // el artista principal, tal cual (sin los invitados) para la consulta
        private static string ArtistaPrincipal(string a)
        {
            if (string.IsNullOrEmpty(a)) return "";
            string s = a;
            foreach (string sep in new[] { " feat.", " Feat.", " FEAT.", " feat ", " ft.", " Ft.", " featuring ", " Featuring ", ",", "&", ";", " / ", " x ", " con " })
            { int k = s.IndexOf(sep, StringComparison.Ordinal); if (k > 0) s = s.Substring(0, k); }
            return s.Trim();
        }
        private static bool Recibe(Llego l)
        {
            byte b; pendientes.TryRemove(l.C.Ruta, out b);
            RocolaOsCalidadRespuesta r = l.R as RocolaOsCalidadRespuesta;
            if (r == null)
            {
                long v; Vistas.TryRemove(l.C.Ruta, out v);
                Apunta(Nom(l.C.Ruta) + ": la busqueda de su pareja no llego (" + (l.Error ?? "sin respuesta") + "): nada se apunta; se vuelve a mirar la proxima vez que entre a la cola");
                return false;
            }
            string porQue;
            RocolaOsCalidadCandidata e = Elige(l.Mp3, l.Mp3.Titulo, l.Mp3.Artista, l.Mp3.Disco, l.DurMs, r.Candidatas, r.PorIsrc, out porQue);
            Pareja p = new Pareja { Ruta = l.C.Ruta, Bytes = l.Bytes, FechaTicks = l.Fecha, BuscadaUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DurMp3Ms = l.DurMs };
            if (e != null)
            {
                p.Id = e.Id; p.Isrc = e.Isrc ?? l.Mp3.Isrc; p.DurSpMs = e.DuracionMs; p.Por = r.PorIsrc ? "isrc" : "titulo";
                Interlocked.Increment(ref Parejas);
                Apunta(Nom(l.C.Ruta) + ": pareja por " + (r.PorIsrc ? "ISRC" : "titulo y artista") + " -> spotify:track:" + e.Id + " (" + r.Peticiones.ToString(CultureInfo.InvariantCulture) + " peticiones)");
                // lo que XP ya abrio por delante de esa cancion (el calentador, al entrar a la cola: aun sin pareja) es el MP3: se suelta, y al
                // tocarle se abre otra vez (por g13: desde Spotify)
                string ruta = l.C.Ruta;
                RocolaOs.Encarga("calidad-pareja", delegate { Form1 rf = RocolaOs.LaRocola; if (rf != null) rf.RocolaOsCalidadSueltaPorDelante(ruta); });
            }
            else
            {
                p.Isrc = l.Mp3.Isrc;
                Interlocked.Increment(ref SinPareja);
                Apunta(Nom(l.C.Ruta) + ": sin pareja (" + (porQue ?? "-") + "; " + r.Peticiones.ToString(CultureInfo.InvariantCulture) + " peticiones): suena tu MP3 y no se vuelve a preguntar en 30 dias");
            }
            pares[p.Ruta] = p;
            Cambio();
            return true;
        }

        // ---------- 3 y 4: lo que dejo la que sono desde Spotify (V1 y C) ----------
        private static bool Cierra(Resultado r)
        {
            Pareja p;
            if (!pares.TryGetValue(r.Ruta, out p) || p.Id != r.Id) return false;
            Pareja n = p.Copia(); bool cambia = false;
            // V1: lo que llego de Spotify, medido como el repaso mide tus canciones; si sono casi entera (97 %), se nivela con eso desde la proxima
            if (r.Medidor != null && r.Natural && !p.ConMedida && r.SonidoSeg >= 30 && (p.DurSpMs <= 0 || r.SonidoSeg * 1000 >= 0.97 * p.DurSpMs))
            {
                SonoridadIntegrada s;
                if (Form1.CierraIntegrada(r.Medidor, r.Medidor.Cuadros / (double)r.Hz, r.Hz, out s) && s.ConP90)
                {
                    n.PicoDb = s.PicoDb; n.P99Db = s.P99Db; n.P90Db = s.P90Db; cambia = true;
                    Interlocked.Increment(ref Medidas);
                    Apunta(Nom(r.Ruta) + ": la de Spotify medida al limite: pico " + F1(s.PicoDb) + ", P99 " + F1(s.P99Db) + ", P90 " + F1(s.P90Db) + " dBFS (se nivela con eso desde la proxima vez)");
                }
            }
            // C: la forma (la primera vez)
            if (p.Verif == 0 && r.Forma != null && r.E != null)
            {
                if (!r.Forma.Hecha && Environment.TickCount64 <= r.Hasta) { esperandoForma.Add(r); if (cambia) { pares[r.Ruta] = n; Cambio(); } return cambia; }
                if (r.Forma.Hecha && r.Forma.E != null)
                {
                    double rr; int usados;
                    string v = Compara(r.Forma.E, r.Forma.N, r.E, r.N, out rr, out usados);
                    if (usados > 0) n.R = (float)Math.Round(rr, 3);
                    if (v == "misma") { n.Verif = 1; Interlocked.Increment(ref Confirmadas); }
                    else if (v == "otra") { n.Verif = -1; Interlocked.Increment(ref Descartadas); }
                    cambia |= v != "no se sabe" || usados > 0;
                    Apunta(Nom(r.Ruta) + ": la forma -> " + v + " (r " + (usados > 0 ? rr.ToString("0.00", CultureInfo.InvariantCulture) : "-") + ", " + usados.ToString(CultureInfo.InvariantCulture) + " tramos)"
                        + (v == "otra" ? ": es otra grabacion, ya no se sustituye" : v == "misma" ? ": confirmada" : ": se vuelve a mirar la proxima vez"));
                }
                else Apunta(Nom(r.Ruta) + ": la forma de tu MP3 no se pudo medir: se vuelve a mirar la proxima vez");
            }
            if (cambia) { pares[r.Ruta] = n; Cambio(); }
            return cambia;
        }
        private static string F1(float x) { return x.ToString("0.0", CultureInfo.InvariantCulture); }

        // ---------- la tabla ----------
        internal static string RutaTabla { get { return RocolaOs.Datos(Archivo); } }
        private static void Carga()
        {
            string[] ls;
            try { ls = Form1.RocolaOsLeeLineas(RutaTabla); } catch { ls = null; }
            if (ls == null) return;
            foreach (string l in ls)
            {
                if (string.IsNullOrEmpty(l) || l[0] == '#' || l == Cabecera) continue;
                string[] c = l.Split('\t');
                if (c.Length < 13) continue;
                try
                {
                    Pareja p = new Pareja { Ruta = c[0], Bytes = L(c[1]), FechaTicks = L(c[2]), Id = c[3] == "-" ? null : c[3], Isrc = c[4] == "-" ? null : c[4],
                        DurSpMs = (int)L(c[5]), DurMp3Ms = (int)L(c[6]), Verif = (int)L(c[7]), BuscadaUnix = L(c[8]), PicoDb = Fl(c[9]), P99Db = Fl(c[10]), P90Db = Fl(c[11]), R = Fl(c[12]),
                        Por = c.Length > 13 && c[13] != "-" ? c[13] : null };
                    if (p.Id != null && RocolaOsSpotify.IdBase62("spotify:track:" + p.Id, "spotify:track:").Length == 0) continue;
                    if (p.Ruta.Length > 0) pares[p.Ruta] = p;
                }
                catch { }
            }
        }
        private static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
        private static float Fl(string s) { float v; return s != "-" && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : float.NaN; }
        private static string Fs(float v) { return float.IsNaN(v) ? "-" : v.ToString("0.###", CultureInfo.InvariantCulture); }
        // DE UN TIRON, con lo de XP (EscribeSeguro: el .bak y el cambio de nombre; un apagon no la deja a medias). Solo este hilo la escribe.
        private static void Guarda()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Cabecera).Append('\n');
            sb.Append("# ruta\tbytes\tfecha\tid_spotify\tisrc\tms_spotify\tms_mp3\tforma(1 misma, 0 sin decidir, -1 otra)\tbuscada_unix\tpico_db\tp99_db\tp90_db\tr\tpor\n");
            List<string> rutas = new List<string>(pares.Keys);
            rutas.Sort(StringComparer.Ordinal);
            foreach (string k in rutas)
            {
                Pareja p; if (!pares.TryGetValue(k, out p)) continue;
                if (p.Ruta.IndexOf('\t') >= 0 || p.Ruta.IndexOf('\n') >= 0 || p.Ruta.IndexOf('\r') >= 0) continue;
                sb.Append(p.Ruta).Append('\t').Append(p.Bytes.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(p.FechaTicks.ToString(CultureInfo.InvariantCulture))
                  .Append('\t').Append(p.Id ?? "-").Append('\t').Append(p.Isrc ?? "-").Append('\t').Append(p.DurSpMs.ToString(CultureInfo.InvariantCulture))
                  .Append('\t').Append(p.DurMp3Ms.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(p.Verif.ToString(CultureInfo.InvariantCulture))
                  .Append('\t').Append(p.BuscadaUnix.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Fs(p.PicoDb)).Append('\t').Append(Fs(p.P99Db))
                  .Append('\t').Append(Fs(p.P90Db)).Append('\t').Append(Fs(p.R)).Append('\t').Append(p.Por ?? "-").Append('\n');
            }
            try { Form1.RocolaOsEscribeSeguro(RutaTabla, sb.ToString()); }
            catch (Exception ex) { RocolaOs.Falla(Nombre, "guardar " + Archivo, ex); }
        }

        // LA QUE SE PUEDE SUSTITUIR (desde cualquier hilo, sin disco): con pareja, no descartada por la forma, sin dos fallos en la sesion
        internal static Pareja Elegible(string ruta)
        {
            if (ruta == null || !Va) return null;
            Pareja p; int f;
            if (!pares.TryGetValue(ruta, out p) || p.Id == null || p.Verif < 0) return null;
            if (Fallos.TryGetValue(ruta, out f) && f >= FallosSesion) return null;
            return p;
        }

        // ---------- el estado (/run/rocola-xp/spotify/calidad) ----------
        private static void EscribeEstado()
        {
            try
            {
                string ruta = RocolaOsSpotify.RunSpotify("calidad");
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                StringBuilder sb = new StringBuilder();
                sb.Append("vistas=").Append(Interlocked.Read(ref Vistas_)).Append('\n').Append("peores=").Append(Interlocked.Read(ref Peores)).Append('\n')
                  .Append("buenas=").Append(Interlocked.Read(ref Buenas)).Append('\n').Append("buscadas=").Append(Interlocked.Read(ref Pedidas)).Append('\n')
                  .Append("peticiones=").Append(RocolaOsSpotifyCatalogoApi.PeticionesPareja).Append('\n')
                  .Append("parejas_nuevas=").Append(Interlocked.Read(ref Parejas)).Append('\n').Append("sin_pareja_nuevas=").Append(Interlocked.Read(ref SinPareja)).Append('\n')
                  .Append("con_pareja=").Append(Contar(true)).Append('\n').Append("sin_pareja=").Append(Contar(false)).Append('\n')
                  .Append("desde_spotify=").Append(Interlocked.Read(ref DesdeSpotify)).Append('\n').Append("al_mp3=").Append(Interlocked.Read(ref AlMp3)).Append('\n')
                  .Append("confirmadas=").Append(Interlocked.Read(ref Confirmadas)).Append('\n').Append("descartadas=").Append(Interlocked.Read(ref Descartadas)).Append('\n')
                  .Append("medidas=").Append(Interlocked.Read(ref Medidas)).Append('\n');
                File.WriteAllText(ruta + ".t", sb.ToString());
                File.Move(ruta + ".t", ruta, true);
            }
            catch { }
        }
    }

    public partial class Form1
    {
        // ================================================================== LA FUNCION «calidad_spotify» (se registra sola)
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RocolaOsRegistraCalidad()
        {
            RocolaOs.Registra(new RocolaOsFuncion
            {
                Nombre = RocolaOsCalidad.Nombre,
                Que = "la sustitucion invisible: una cancion tuya que suena peor y existe en Spotify suena desde Spotify (necesita «spotify»)",
                DeFabrica = true,
                Arranca = f => f.RocolaOsArrancaCalidad(),
                Latido = f => f.RocolaOsLatidoCalidad(),
                ParaAlCerrar = razon => RocolaOsCalidad.Para(),
            });
            RocolaOsOrdenes.Si("--prueba-calidad", RocolaOsCalidad.Prueba);
        }
        private void RocolaOsArrancaCalidad()
        {
            if (!RocolaOs.Encendida(RocolaOsSpotify.Nombre)) { RocolaOsCalidad.Apunta("sin la funcion «spotify» no hace nada"); return; }
            RocolaOsCalidad.Arranca();
        }

        // CADA SEGUNDO (hilo de la pantalla; rapido, sin disco): las canciones tuyas que entraron a la cola (y la que suena) y aun no se miraron
        // en esta sesion se mandan al hilo con lo que XP ya midio de ellas
        private void RocolaOsLatidoCalidad()
        {
            if (!RocolaOsCalidad.Va) return;
            // (revision «invisible-y-reglas») la MUDA del diagnostico completo de Ctrl+F3 (DiagArrancaPropia: una cancion al azar, sin pagar) no es
            // una pedida: ni se mira ni se busca su pareja (al abrirla ya iba por tu MP3: PlayTrackForMeasurement)
            if (!EsMudaDiag(nowPlaying)) RocolaOsCalidadMira(nowPlaying);
            for (int i = 0; i < queue.Count && i < 64; i++) RocolaOsCalidadMira(queue[i]);
            RocolaOsCalidadSueltaSiYaNoVale();
        }
        // (revision «invisible-y-reglas») LO QUE EL CALENTADOR ABRIO POR DELANTE COMO MP3 Y YA SE SUSTITUIRIA. g60 le da la ruta de Spotify a la que
        // se va a sustituir, pero lo que XP abrio ANTES de poder sustituirla (al arrancar, con Soloist aun sin sesion; o al volver de un apagon,
        // con la de la cola) se quedaba esperando como MP3 y BeginAudioOpen se lo llevaba (Toma, por la ruta del MP3): esa vez sonaba tu MP3
        // aunque Soloist ya estuviera listo. Cada segundo, sin disco: si lo que espera ya iria por Spotify, se suelta (al tocarle se abre por g13).
        private void RocolaOsCalidadSueltaSiYaNoVale()
        {
            MiraPorDelante<IAudioSource> la = lookahead;
            string para = la != null ? la.Para : null;
            if (para == null || para.StartsWith("spotify:", StringComparison.Ordinal)) return;
            if (string.Equals(RocolaOsCalidadRuta(para), para, StringComparison.Ordinal)) return;   // (sigue siendo tu MP3: se queda)
            la.Suelta();
            RocolaOsCalidad.Apunta(Path.GetFileName(para) + ": lo que XP habia abierto por delante como MP3 se suelta (ya va desde Spotify)");
        }
        private void RocolaOsCalidadMira(PlayItem it)
        {
            if (it == null || it.IsVideo || it.Song == null) return;
            string p = it.Song.Path;
            if (string.IsNullOrEmpty(p) || p.StartsWith("spotify:", StringComparison.Ordinal)) return;
            if (RocolaOsCalidad.Vistas.Count > 20000) RocolaOsCalidad.Vistas.Clear();
            if (!RocolaOsCalidad.Vistas.TryAdd(p, Environment.TickCount64)) return;
            float corte = 0f;
            try { float[] c; if (eqCache != null && eqCache.TryGetValue(p, out c) && c != null && c.Length > EqCut) corte = c[EqCut]; } catch { }
            bool pe = false; try { pe = PreEcoDe(p); } catch { }
            float seg = 0f; try { seg = SegundosDe(p); } catch { }
            RocolaOsCalidad.Encola(new RocolaOsCalidad.Candidata
            {
                Ruta = p, Titulo = it.Song.Title, Artista = it.Song.Artist, Disco = !string.IsNullOrEmpty(it.Song.Album) ? it.Song.Album : (it.Disc != null ? it.Disc.Name : null),
                Corte = corte, PreEco = pe, DurXpMs = seg > 0 ? (int)(seg * 1000) : 0,
            });
        }

        // ================================================================== AL ABRIR (desde g13: OpenAudio -> RocolaOsSpotifyAbre)
        // En el hilo que abre (AperturaEnFondo). La apertura para MEDIR de Ctrl+F3 (sincrona, en la pantalla) y una retomada (volvio el aparato
        // de sonido: hay que saltar a media cancion, y Spotify no se puede) van siempre por tu MP3.
        private IAudioSource RocolaOsCalidadAbre(string path)
        {
            if (!RocolaOs.Activo || !RocolaOsCalidad.Va || path == null || path.StartsWith("spotify:", StringComparison.Ordinal)) return null;
            if (RocolaOs.EnLaPantalla || audioOpeningForMeasurement || reanudarEn > 0) return null;
            byte b;
            if (RocolaOsCalidad.NoSustituir.TryRemove(path, out b)) return null;   // (volvio de Spotify: esta vez tu MP3)
            RocolaOsCalidad.Pareja p = RocolaOsCalidad.Elegible(path);
            if (p == null || !RocolaOsSpotify.Listo) return null;
            try { FileInfo fi = new FileInfo(path); if (!fi.Exists || fi.Length != p.Bytes || fi.LastWriteTimeUtc.Ticks != p.FechaTicks) return null; }
            catch { return null; }
            RocolaOsFuenteCalidad f = new RocolaOsFuenteCalidad(this, path, p);
            Interlocked.Increment(ref RocolaOsCalidad.DesdeSpotify); RocolaOsCalidad.Cambio();
            RocolaOsCalidad.Apunta("suena desde Spotify: " + Path.GetFileName(path) + " -> " + p.Uri + (p.Verif == 1 ? " (confirmada)" : " (la forma, por mirar)")
                + (p.ConMedida ? "" : "; sin medir"));
            return f;
        }

        // ================================================================== LOS GANCHOS
        // g60 (Codigo/AperturaAudio.cs, RutaQueSonaria): la que se va a sustituir se ve por la ruta de Spotify (el calentador y el relevo sin pausa
        // no la abren como MP3 por delante). Sin efectos: solo pregunta. De XP (o sin el modo), path.
        private string RocolaOsCalidadRuta(string path)
        {
            if (!RocolaOs.Activo || path == null || !RocolaOsCalidad.Va || path.StartsWith("spotify:", StringComparison.Ordinal)) return path;
            if (RocolaOsCalidad.NoSustituir.ContainsKey(path) || !RocolaOsSpotify.Listo) return path;
            RocolaOsCalidad.Pareja p = RocolaOsCalidad.Elegible(path);
            return p != null ? p.Uri : path;
        }
        // g61 (Codigo/Form1.Sonido.cs, ArmaCadena): si el lector es la version de Spotify de una cancion tuya, la cadena se arma con una copia de
        // tu cancion con la ruta de Spotify (sus medidas -ninguna de XP: g23 no aprende nada de «spotify:»-, su nivelado -g62- y nada del MP3:
        // ni su EQ, ni su zumbido, ni su pre-eco, ni su realce de dañada). Lo que se ve (cad.Item, nowPlaying) sigue siendo tu cancion.
        private Track RocolaOsCalidadCancion(Track t, IAudioSource source)
        {
            RocolaOsFuenteCalidad f = source as RocolaOsFuenteCalidad;
            if (f == null || t == null) return t;
            Track s = t.CopiaEtiqueta();
            s.Path = f.Uri;
            f.Sombra = s;
            RocolaOsCalidad.Sombras.AddOrUpdate(s, f);
            string como;
            float g = RocolaOsCalidadGananciaDe(f, s, out como);
            RocolaOsCalidad.Apunta("la cadena de " + Path.GetFileName(f.Ruta) + " con " + f.Uri + ": nivelado " + (20 * Math.Log10(Math.Max(1e-6, g))).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " dB (" + como + ")");
            return s;
        }
        // g62 (Codigo/NivelDisco.cs, GananciaDe): la version de Spotify de una cancion tuya se nivela con lo suyo. Tambien cuando Ctrl+F3 vuelve
        // a nivelar en vivo (ApplyNormNow, con la cancion de XP de lo que suena). De XP, false.
        private bool RocolaOsCalidadGanancia(Track t, out float g)
        {
            g = 1f;
            if (!RocolaOs.Activo || t == null || !RocolaOsCalidad.Va) return false;
            object o; RocolaOsFuenteCalidad f;
            if (RocolaOsCalidad.Sombras.TryGetValue(t, out o)) f = (RocolaOsFuenteCalidad)o;
            else
            {
                f = reader as RocolaOsFuenteCalidad;
                if (f == null || f.Sombra == null || nowPlaying == null || !object.ReferenceEquals(nowPlaying.Song, t)) return false;
                t = f.Sombra;
            }
            string como;
            g = RocolaOsCalidadGananciaDe(f, t, out como);
            return true;
        }
        // LA GANANCIA (SP3, V1, V2): su medida al limite (GananciaAlLimite, como tus canciones); sin medida, la mediana de las de Spotify medidas
        // (con 5 o mas); si no, 1 (como una de XP sin medir). Con el margen del tono del local y, en el billar, el volumen parejo, como GananciaDe.
        private float RocolaOsCalidadGananciaDe(RocolaOsFuenteCalidad f, Track sombra, out string como)
        {
            float g;
            RocolaOsCalidad.Pareja p = f.Par;
            if (!normalizeOn || normStrength <= 0.001f) { g = 1f; como = "el nivelado apagado"; }
            else if (p.ConMedida) { g = GananciaAlLimite(normStrength, DbALin(p.PicoDb), DbALin(p.P99Db), DbALin(p.P90Db)); como = "su medida"; }
            else
            {
                List<float> l = new List<float>();
                foreach (RocolaOsCalidad.Pareja x in RocolaOsCalidad.Pares.Values)
                    if (x.Id != null && x.ConMedida) l.Add(GananciaAlLimite(normStrength, DbALin(x.PicoDb), DbALin(x.P99Db), DbALin(x.P90Db)));
                if (l.Count >= 5)
                {
                    l.Sort();
                    g = l.Count % 2 == 1 ? l[l.Count / 2] : 0.5f * (l[l.Count / 2 - 1] + l[l.Count / 2]);
                    como = "la mediana de " + l.Count.ToString(CultureInfo.InvariantCulture) + " de Spotify medidas";
                }
                else { g = 1f; como = "sin medir: como una de XP sin medir"; }
            }
            float m = MargenDelTono(sombra);
            if (localAuto) { float pa = ParejoAplicado(sombra); if (pa != 0f) m *= DbALin(pa); }
            return m == 1f ? g : g * m;
        }

        // (encargo, en la pantalla) una cancion acaba de tener pareja: si el calentador de XP ya la tenia abierta por delante como MP3, se suelta
        internal void RocolaOsCalidadSueltaPorDelante(string ruta)
        {
            if (lookahead == null || ruta == null || !string.Equals(lookahead.Para, ruta, StringComparison.OrdinalIgnoreCase)) return;
            lookahead.Suelta();
            RocolaOsCalidad.Apunta(Path.GetFileName(ruta) + ": lo que XP habia abierto por delante como MP3 se suelta (al tocarle se abre desde Spotify)");
        }

        // ================================================================== SI SPOTIFY NO PUEDE: TU MP3 (encargo de la fuente)
        // Por el camino de XP que retoma una cancion (ReintentoSalida_Tick de SalidaSonido.cs): se cierra lo que suena y la MISMA cancion se abre
        // otra vez, ahora como MP3 (NoSustituir), desde el principio o, si ya sono 10 s o mas, desde donde iba (reanudarEn, la retomada de XP).
        // Ni cola, ni creditos, ni lo mas sonado cambian. Si ya no le toca a esa (se salto, otra, un video), la fuente solo acaba.
        private void RocolaOsCalidadVuelve(RocolaOsFuenteCalidad f, double desdeSeg, string motivo)
        {
            if (f == null) return;
            if (!object.ReferenceEquals(reader, f) || videoOn || nowPlaying == null || nowPlaying.IsVideo || nowPlaying.Song == null
                || !string.Equals(nowPlaying.Song.Path, f.Ruta, StringComparison.Ordinal))
            { f.Abandona(); return; }
            RocolaOsCalidad.NoSustituir[f.Ruta] = 1;
            RocolaOsCalidad.Fallos.AddOrUpdate(f.Ruta, 1, (k, v) => v + 1);
            Interlocked.Increment(ref RocolaOsCalidad.AlMp3); RocolaOsCalidad.Cambio();
            double desde = desdeSeg >= 10.0 ? desdeSeg : 0.0;
            RocolaOsCalidad.Apunta("vuelve a tu MP3: " + Path.GetFileName(f.Ruta) + " (" + motivo + "), " + (desde > 0 ? "desde " + desde.ToString("0.0", CultureInfo.InvariantCulture) + " s" : "desde el principio"));
            Track t = nowPlaying.Song;
            DisposePlaybackNow();
            if (desde > 0) reanudarEn = desde;
            PlayTrack(t);
            UpdateNowPlayingUI();
        }

        // LA FORMA DE TU MP3 (regla C): su volumen por tramos de 0,5 s (el promedio de los canales, al cuadrado), leyendo el archivo entero con el
        // lector del repaso, en un hilo de fondo (nunca en la pantalla ni en el hilo del sonido). Solo numeros, en memoria.
        internal static void RocolaOsCalidadMideForma(string ruta, RocolaOsCalidad.Forma forma)
        {
            try
            {
                using (LectorMedida rd = new LectorMedida(ruta))
                {
                    rd.Volume = 1f;
                    int hz = rd.WaveFormat.SampleRate, ch = rd.WaveFormat.Channels;
                    if (hz < 8000 || ch < 1) return;
                    long porTramo = Math.Max(1, (long)(hz * RocolaOsCalidad.TramoSeg));
                    int T = (int)Math.Min(4000, rd.TotalTime.TotalSeconds / RocolaOsCalidad.TramoSeg + 4);
                    double[] e = new double[Math.Max(1, T)]; long[] n = new long[e.Length];
                    float[] buf = new float[ch * 4096];
                    long cuadro = 0;
                    while (true)
                    {
                        int k = rd.Read(buf, 0, buf.Length);
                        if (k <= 0) break;
                        int cu = k / ch;
                        for (int i = 0; i < cu; i++)
                        {
                            float x = 0; for (int c = 0; c < ch; c++) x += buf[i * ch + c];
                            x /= ch;
                            long t = (cuadro + i) / porTramo;
                            if (t < e.Length) { e[t] += x * x; n[t]++; }
                        }
                        cuadro += cu;
                    }
                    forma.E = e; forma.N = n;
                }
            }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsCalidad.Nombre, "la forma de " + Path.GetFileName(ruta), ex); }
            finally { forma.Hecha = true; RocolaOsCalidad.Encola(new object()); }
        }
    }
}

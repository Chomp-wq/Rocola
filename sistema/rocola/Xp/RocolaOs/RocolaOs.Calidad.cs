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
        // (revision «sonido-cuota-y-fallos») LO QUE NO SE PUDO PREGUNTAR se vuelve a mirar, si sigue en la cola, solo despues de esperar: antes se
        // quitaba de Vistas y el latido la mandaba otra vez al segundo siguiente, asi que sin red o con la API en 5xx salian dos peticiones cada
        // ~3 s por cada cancion mala de la cola (y con 403/400, para siempre), y sin sesion, sin llave o frenando, un renglon de bitacora y la
        // cabecera del MP3 leida cada segundo
        internal const int ReintentoSesionMs = 60000;                // Soloist aun sin sesion (al arrancar tarda unos segundos)
        internal const int ReintentoPasajeroMs = 300000;             // sin red, 5xx, frenando (429)
        internal const int ReintentoOtroMs = 1800000;                // un 4xx, la llave mala, sin la llave de la API

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
        // (las mira el latido una vez por sesion) 0: ya mirada; si no, el Environment.TickCount64 desde el que se puede volver a mirar
        internal static readonly ConcurrentDictionary<string, long> Vistas = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        internal static void Reintenta(string ruta, int ms) { Vistas[ruta] = Environment.TickCount64 + ms; }
        private static string Min(int ms) { return (ms / 60000).ToString(CultureInfo.InvariantCulture) + " min"; }
        internal static readonly ConcurrentDictionary<string, byte> NoSustituir = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal); // una vez tu MP3 (volvio de Spotify)
        private static readonly ConcurrentDictionary<string, byte> pendientes = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        internal static readonly ConcurrentDictionary<string, int> Fallos = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        internal static readonly ConditionalWeakTable<object, object> Sombras = new ConditionalWeakTable<object, object>();   // (la Track de Spotify -> su fuente)
        // (27-11-2026) LA RUTA DE TU MP3 DE CADA SOMBRA, por su uri de Spotify (la ultima que se armo): lo que XP mira por la ruta de la cancion (el
        // EQ, el bajo, el margen del ecualizador del dueno, el des-recorte, la duracion y el volumen parejo del billar) se mira en tu MP3
        private static readonly ConcurrentDictionary<string, string> sombraDe = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        internal static void PonSombra(string uri, string ruta) { if (sombraDe.Count > 4000) sombraDe.Clear(); sombraDe[uri] = ruta; }
        internal static string Mp3DeSombra(string uri) { string r; return uri != null && sombraDe.TryGetValue(uri, out r) ? r : null; }
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
            int quitadas = Carga();
            if (quitadas > 0) { Guarda(); Apunta(quitadas.ToString(CultureInfo.InvariantCulture) + " renglones viejos fuera de " + Archivo + " («sin pareja» de hace mas de 30 dias, o pasado el tope de " + TopeTabla.ToString(CultureInfo.InvariantCulture) + ")"); }
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
            string porQueNo = null; int espera = 0;
            if (!RocolaOsSpotify.Listo) { porQueNo = "Soloist sin sesion"; espera = ReintentoSesionMs; }
            else if (!RocolaOsSpotifyCatalogoApi.HayLlave) { porQueNo = "sin la llave de la API"; espera = ReintentoOtroMs; }
            else if (RocolaOsSpotifyCatalogoApi.Frena) { porQueNo = "Spotify frena (429)"; espera = ReintentoPasajeroMs; }
            if (porQueNo != null)
            {
                Reintenta(c.Ruta, espera);   // (si sigue en la cola, se vuelve a mirar pasado ese rato: no cada segundo)
                Apunta(Nom(c.Ruta) + " suena peor (" + motivo + "): no se busca su pareja (" + porQueNo + "; si sigue en la cola, se vuelve a mirar en " + Min(espera) + ")");
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
                int espera = RocolaOsSpotifyCatalogoApi.Pasajero(l.Error) || l.Error == null ? ReintentoPasajeroMs : ReintentoOtroMs;
                Reintenta(l.C.Ruta, espera);
                Apunta(Nom(l.C.Ruta) + ": la busqueda de su pareja no llego (" + (l.Error ?? "sin respuesta") + "): nada se apunta; si sigue en la cola, se vuelve a mirar en " + Min(espera));
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
                if (Form1.CierraIntegrada(r.Medidor, r.Medidor.Cuadros / (double)r.Hz, r.Hz, out s) && s.ConP90 && !s.Muda)   // (23-11-2026, revision: una casi muda no se guarda: daria una ganancia enorme)
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
        // (revision «sonido-cuota-y-fallos») LA TABLA NO CRECE SIN TOPE: se reescribe entera en cada cambio, asi que al leerla se quitan los
        // «sin pareja» de hace mas de 30 dias (se volverian a preguntar igual) y, pasado TopeTabla, los renglones mas viejos (los «sin pareja»
        // primero). Las parejas se quedan aunque el archivo ya no este (al volver el disco, valen); el tope las acota.
        internal const int TopeTabla = 20000;
        private static int Carga()
        {
            string[] ls;
            try { ls = Form1.RocolaOsLeeLineas(RutaTabla); } catch { ls = null; }
            if (ls == null) return 0;
            long ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int quitadas = 0;
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
                    if (p.Id == null && ahora - p.BuscadaUnix >= OlvidaSinParejaSeg) { quitadas++; continue; }
                    if (p.Ruta.Length > 0) pares[p.Ruta] = p;
                }
                catch { }
            }
            if (pares.Count > TopeTabla)
            {
                List<Pareja> l = new List<Pareja>(pares.Values);
                l.Sort((x, y) => (x.Id != null) != (y.Id != null) ? (x.Id == null ? -1 : 1) : x.BuscadaUnix.CompareTo(y.BuscadaUnix));
                for (int i = 0; i < l.Count && pares.Count > TopeTabla; i++) { Pareja z; if (pares.TryRemove(l[i].Ruta, out z)) quitadas++; }
            }
            return quitadas;
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
            long hasta;   // (0: ya mirada en la sesion; si no, desde cuando se puede volver a mirar: lo que no se pudo preguntar)
            if (RocolaOsCalidad.Vistas.TryGetValue(p, out hasta))
            {
                if (hasta == 0 || Environment.TickCount64 < hasta || !RocolaOsCalidad.Vistas.TryUpdate(p, 0, hasta)) return;
            }
            else if (!RocolaOsCalidad.Vistas.TryAdd(p, 0)) return;
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
            if (RocolaOs.EnLaPantalla || audioOpeningForMeasurement) return null;
            // (revision «sonido-cuota-y-fallos») SOLO LA QUE LE TOCA SONAR: PlayNext y la vuelta al MP3 ponen nowPlaying antes de abrirla. Lo que el
            // calentador abre por delante para la siguiente (MiraPorDelante: g60 le dio la ruta del MP3, p. ej. un disco que sigue sin pausa) es
            // tu MP3: antes, si la pareja llegaba entre g60 y esa apertura, o g60 daba la ruta del MP3 a proposito, el relevo se llevaba una
            // fuente de Spotify (y el hilo de la forma leia tu MP3 entero para nada)
            PlayItem np = nowPlaying;
            if (np == null || np.IsVideo || np.Song == null || !string.Equals(np.Song.Path, path, StringComparison.Ordinal)) return null;
            byte b;
            // (volvio de Spotify: esta vez tu MP3). ANTES de mirar la retomada: si volvia desde donde iba (reanudarEn), la marca se quedaba
            // puesta, g60 daba la ruta del MP3 y el calentador la abria como MP3: esa cancion ya no se sustituia en toda la sesion
            if (RocolaOsCalidad.NoSustituir.TryRemove(path, out b)) return null;
            if (reanudarEn > 0) return null;   // (una retomada de XP: hay que saltar a media cancion y Spotify no se puede)
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
            if (p == null) return path;
            if (RocolaOsCalidadEmpalma(path)) return path;   // (el disco que sigue sin pausa: tu MP3 por el relevo de XP)
            return p.Uri;
        }
        // (revision «sonido-cuota-y-fallos») EL DISCO QUE SIGUE SIN PAUSA. XP encadena la pista que sigue del mismo disco en la misma lectura (relevo
        // sin pausa: SiguienteDelDisco le quita el fundido de salida a la que suena y el de entrada a la siguiente; un disco en vivo o una
        // mezcla siguen continuos). Una sustituida no puede ir por el relevo (entra por PlayNext: la salida se cierra y se abre, y Soloist tarda
        // en empezar): en un disco continuo seria un corte que XP no tiene. Por eso, si la frontera es CONTINUA (la que suena acaba con musica
        // y esta no empieza con silencio, por lo que midio el repaso: FinUtil e InicioUtil del algoritmo 6, a 1 s o menos de la orilla), suena tu
        // MP3 por el relevo de XP. Sin la medida de la que suena, o entre pistas con su silencio (lo normal en un disco de estudio), se sustituye.
        // En la pantalla (g60 lo pregunta desde PideLookahead y PreparaSiguienteSinPausa), sin disco: las tablas de XP en memoria.
        internal const float EmpalmeSeg = 1.0f;
        private bool RocolaOsCalidadEmpalma(string path)
        {
            PlayItem a = nowPlaying, b = queue.Count > 0 ? queue[0] : null;
            if (a == null || b == null || b.Song == null || a.Song == null || !string.Equals(b.Song.Path, path, StringComparison.Ordinal) || !SiguienteDelDisco(a, b)) return false;
            return RocolaOsCalidadFronteraContinua(a.Song.Path, path);
        }
        private bool RocolaOsCalidadFronteraContinua(string antes, string despues)
        {
            Dictionary<string, SonoridadIntegrada> t = integCache;
            SonoridadIntegrada x, y;
            if (t == null || antes == null || despues == null || !t.TryGetValue(antes, out x) || x.Algoritmo < 6 || float.IsNaN(x.FinUtil)) return false;
            float dur = SegundosDe(antes);
            if (!(dur > 0f) || dur - x.FinUtil > EmpalmeSeg) return false;   // la que suena acaba con su silencio (o no se sabe): se sustituye
            // la siguiente: si ya se midio y empieza con su silencio, tampoco es continua. Aun sin medir (el calentador la mide DESPUES de abrirla
            // por delante, y g60 se pregunta antes) cuenta como continua: suena tu MP3, como en XP
            if (t.TryGetValue(despues, out y) && y.Algoritmo >= 6 && !float.IsNaN(y.InicioUtil) && y.InicioUtil > EmpalmeSeg) return false;
            return true;
        }
        // g61 (Codigo/Form1.Sonido.cs, ArmaCadena): si el lector es la version de Spotify de una cancion tuya, la cadena se arma con una copia de
        // tu cancion con la ruta de Spotify (g23 no aprende nada de «spotify:»). Desde el 27-11-2026 suena CON LO DE TU MP3: su EQ automatico y su
        // realce (RocolaOsSpotifyEqHuella: la huella de tu MP3 en eqCache), el bajo psicoacustico, el margen del ecualizador del dueno, el
        // des-recorte, la duracion minima del EQ del billar y el volumen parejo (g252-g254 y g430-g437 miran la huella de tu MP3 por la uri de
        // la sombra) y su NIVEL (RocolaOsCalidadGananciaDe: la ganancia de tu MP3, la del disco si suena en racha, y lo que Spotify se aparta de
        // ella). Sin el zumbido ni el pre-eco (son del archivo). Lo que se ve (cad.Item, nowPlaying) sigue siendo tu cancion.
        private Track RocolaOsCalidadCancion(Track t, IAudioSource source)
        {
            RocolaOsFuenteCalidad f = source as RocolaOsFuenteCalidad;
            if (f == null || t == null) return t;
            Track s = t.CopiaEtiqueta();
            s.Path = f.Uri;
            f.Sombra = s; f.Original = t;
            RocolaOsCalidad.PonSombra(f.Uri, f.Ruta);
            RocolaOsCalidad.Sombras.AddOrUpdate(s, f);
            string como;
            float g = RocolaOsCalidadGananciaDe(f, s, out como);
            RocolaOsCalidad.Apunta("la cadena de " + Path.GetFileName(f.Ruta) + " con " + f.Uri + ": nivelado " + (20 * Math.Log10(Math.Max(1e-6, g))).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " dB (" + como + ")");
            // (27-11-2026) y lo de tu MP3 que le toca en el resto de la cadena (la huella de tu MP3; sin ella, como antes: nada)
            try
            {
                float[] eq = EqCorrFor(s.Path);
                StringBuilder sb = new StringBuilder("la cadena de " + Path.GetFileName(f.Ruta) + " con " + f.Uri + ": con lo de tu MP3: EQ automatico ");
                if (eq == null) sb.Append("sin correccion");
                else for (int b = 0; b < eq.Length; b++) sb.Append(b == 0 ? "[" : " ").Append(eq[b].ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)).Append(b == eq.Length - 1 ? "] dB" : "");
                sb.Append("; realce ").Append(ExciteAmountFor(s.Path).ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append("; bajo x").Append(BassFactorFor(s.Path).ToString("0.00", CultureInfo.InvariantCulture)).Append(" de ").Append(BajoAuto(s.Path).ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append(DeclipVale(s.Path) ? "; con des-recorte" : "; sin des-recorte");
                sb.Append(EqDuenoPesosDe(s.Path, eq) != null ? "; margen E3 con su huella" : "; margen E3 sin huella");
                if (localAuto) sb.Append("; duracion ").Append(RocolaOsSegundosDe(s.Path).ToString("0", CultureInfo.InvariantCulture)).Append(" s; parejo ").Append(ParejoEstimado(s.Path).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)).Append(" dB");
                RocolaOsCalidad.Apunta(sb.ToString());
            }
            catch (Exception ex) { RocolaOs.Falla(RocolaOsCalidad.Nombre, "la cadena de tu MP3", ex); }
            return s;
        }
        // g62 (Codigo/NivelDisco.cs, GananciaDe): la version de Spotify de una cancion tuya se nivela con lo suyo. Tambien cuando Ctrl+F3 vuelve
        // a nivelar en vivo (ApplyNormNow, con la cancion de XP de lo que suena). De XP, false.
        private bool RocolaOsCalidadGanancia(Track t, out float g)
        {
            g = 1f;
            object o; RocolaOsFuenteCalidad f;
            // (22-11-2026, spotify_nivela) una de Spotify PEDIDA (no la sombra de una tuya): su nivelado (RocolaOs.SpotifyNivela.cs)
            if (RocolaOs.Activo && t != null && t.Path != null && t.Path.StartsWith("spotify:track:", StringComparison.Ordinal) && !RocolaOsCalidad.Sombras.TryGetValue(t, out o))
                return RocolaOsSpotifyNivelaGanancia(t, out g);
            if (!RocolaOs.Activo || t == null || !RocolaOsCalidad.Va) return false;
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
            float gMp3, gLimMp3;
            if (!normalizeOn || normStrength <= 0.001f) { g = 1f; como = "el nivelado apagado"; }
            else if (RocolaOsCalidadNivelMp3(f.Original, out gMp3, out gLimMp3))
            {   // (27-11-2026) EL NIVEL DE TU MP3: lo que le toca a tu cancion (la del disco si suena en racha) y, encima, lo que la de Spotify se
                // aparta de ella: con su medida al limite, el cociente de las dos ganancias al limite; sin ella (la primera vez), la mediana de ese
                // cociente en las parejas ya medidas (con 3 o mas; si no, 1). Fuera de un disco en racha, las dos al limite: queda como antes.
                float rel; string de;
                if (p.ConMedida) { rel = GananciaAlLimite(normStrength, DbALin(p.PicoDb), DbALin(p.P99Db), DbALin(p.P90Db)) / gLimMp3; de = "su medida"; }
                else { int cuantas; rel = RocolaOsCalidadRelMediana(out cuantas); de = cuantas >= 3 ? "la mediana de " + cuantas.ToString(CultureInfo.InvariantCulture) + " parejas medidas" : "sin medir: igual que tu MP3"; }
                rel = Clampf(rel, 0.25f, 4f);
                g = gMp3 * rel;
                como = "el nivel de tu MP3 " + (20 * Math.Log10(Math.Max(1e-6, gMp3))).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " dB" + (RachaDeDisco(f.Original) != null ? " (con su disco)" : "") + " y lo de Spotify: " + de;
            }
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
            if (localAuto) { float pa = ParejoAplicado(f.Original ?? sombra); if (pa != 0f) m *= DbALin(pa); }   // (27-11-2026: el parejo de tu MP3, en racha el de su disco)
            return m == 1f ? g : g * m;
        }
        // (27-11-2026) LA GANANCIA DE TU MP3 y la que lo deja al limite con su propia medida: la del disco si suena en racha con otra de su disco
        // (GananciaDeDisco) y si no la suya (NormGainFor), como GananciaDe de XP; sin la medida al limite de tu MP3 (algoritmo 5), false: lo de antes.
        private bool RocolaOsCalidadNivelMp3(Track orig, out float gMp3, out float gLimMp3)
        {
            gMp3 = 1f; gLimMp3 = 0f;
            if (orig == null || orig.Path == null) return false;
            float pico, p99, p90;
            if (!PicosParaNivelar(orig.Path, out pico, out p99) || !P90ParaNivelar(orig.Path, out p90)) return false;
            gLimMp3 = GananciaAlLimite(normStrength, pico, p99, p90);
            List<Track> disco = RachaDeDisco(orig); float g;
            if (!(disco != null && GananciaDeDisco(disco, orig.Path, out g))) g = NormGainFor(orig.Path);
            gMp3 = g;
            return gLimMp3 > 0f && gMp3 > 0f && !float.IsNaN(gLimMp3) && !float.IsInfinity(gLimMp3);
        }
        // la mediana de «ganancia al limite de Spotify entre ganancia al limite de tu MP3» de las parejas ya medidas (1 con menos de 3)
        private float RocolaOsCalidadRelMediana(out int cuantas)
        {
            List<float> l = new List<float>();
            foreach (RocolaOsCalidad.Pareja x in RocolaOsCalidad.Pares.Values)
            {
                if (x.Id == null || !x.ConMedida || x.Ruta == null) continue;
                float pico, p99, p90;
                if (!PicosParaNivelar(x.Ruta, out pico, out p99) || !P90ParaNivelar(x.Ruta, out p90)) continue;
                float a = GananciaAlLimite(normStrength, pico, p99, p90), b = GananciaAlLimite(normStrength, DbALin(x.PicoDb), DbALin(x.P99Db), DbALin(x.P90Db));
                if (a > 0f && b > 0f && !float.IsNaN(b / a) && !float.IsInfinity(b / a)) l.Add(b / a);
            }
            cuantas = l.Count;
            if (l.Count < 3) return 1f;
            l.Sort();
            return l.Count % 2 == 1 ? l[l.Count / 2] : 0.5f * (l[l.Count / 2 - 1] + l[l.Count / 2]);
        }

        // (27-11-2026) las ayudas de los ganchos g430-g437: lo que XP mira por la ruta de una cancion, para la sombra de una tuya, en tu MP3
        // g432 (DeclipVale) y g252-g254 (por RocolaOsSpotifyEqHuella): la huella de tu MP3 si la ruta es la sombra de una tuya; false para todo lo
        // demas (una pedida sin huella de recorte: el des-recorte se pone por si acaso, como antes)
        private bool RocolaOsMp3EqDeSombra(string path, out float[] c)
        {
            c = null;
            if (!RocolaOs.Activo || path == null || !RocolaOsCalidad.Va || !path.StartsWith("spotify:track:", StringComparison.Ordinal)) return false;
            string mp3 = RocolaOsCalidad.Mp3DeSombra(path);
            return mp3 != null && eqCache.TryGetValue(mp3, out c) && c != null;
        }
        // g434 y g435: la duracion para el EQ del billar y el volumen parejo. Una de Spotify no esta en durCache: la de su pista (lo que pidio el
        // catalogo) o, si es la sombra de una tuya, la de tu MP3. Lo demas, SegundosDe de XP.
        private float RocolaOsSegundosDe(string path)
        {
            float d = SegundosDe(path);
            if (d > 0.01f || !RocolaOs.Activo || path == null || !path.StartsWith("spotify:track:", StringComparison.Ordinal)) return d;
            string mp3 = RocolaOsCalidad.Mp3DeSombra(path);
            if (mp3 != null) { float m = SegundosDe(mp3); if (m > 0.01f) return m; }
            RocolaOsSpotifyPista pi;
            if (RocolaOsSpotify.Pistas.TryGetValue(path, out pi) && pi != null && pi.DuracionMs > 0) return pi.DuracionMs / 1000f;
            return d;
        }
        // g436 (ParejoEstimado): el volumen parejo del billar de la sombra es el de tu MP3
        private string RocolaOsRutaParejo(string path)
        {
            if (!RocolaOs.Activo || path == null || !path.StartsWith("spotify:track:", StringComparison.Ordinal)) return path;
            return RocolaOsCalidad.Mp3DeSombra(path) ?? path;
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

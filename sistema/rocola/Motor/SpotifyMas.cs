// LO QUE SPOTIFY ANADE A LA ROCOLA (decision del dueno del 6-11-2026, SPOTIFY_PROPUESTAS_MODEST_2026-11-06.md: «todo menos SP7, SP10,
// SP12»; nada que haga sonar musica sin creditos). Todo solo con el modo Spotify encendido (Spotify.cs):
//  SP4  EMPAREJAR: en segundo plano, cada cancion de la biblioteca se busca en Spotify y se apunta su pareja si es la misma grabacion
//       (mismo titulo y artista, y la duracion a 2 s o menos), en rocola_spotify_pares.txt (solo numeros y nombres: nada de musica).
//  SP2  LA VERSION DE SPOTIFY PARA LAS DANADAS, SIN QUE SE NOTE: una cancion tuya con los agudos cortados (el corte que mide el repaso)
//       y con pareja suena desde Spotify (320 kbps). En la pantalla, la cola, lo mas sonado y el estado sigue siendo TU cancion: mismo
//       nombre, portada, disco y credito. Si Spotify no la puede tocar, suena tu MP3 (sin aviso). Interruptor «calidad_spotify=».
//  SP3  NIVELADO DE SPOTIFY: lo que suena de Spotify se mide y se apunta por su numero de Spotify (como el repaso con las tuyas): la
//       segunda vez va al limite como tu biblioteca. (Antes, de Spotify no se aprendia nada: eso fue decision mia, no del dueno.)
//  SP1  LAS LETRAS (al estilo Apple Music, VistaGrafica): la letra sincronizada de Spotify de lo que suena, de Spotify o tuya con
//       pareja; se pide al momento y no se guarda. Interruptor «letras_spotify=».
//  SP5  PORTADAS Y GENEROS QUE FALTAN: los discos sin caratula toman la de Spotify (guardada en los datos, carpeta portadas_spotify; solo
//       imagenes) y las canciones sin genero, el del artista. Interruptor «portadas_spotify=».
//  SP6  COMPLETAR TUS DISCOS: las canciones que le faltan a un disco tuyo (el disco de Spotify de sus parejas) salen al final, con su
//       credito. Interruptor «completar_discos=».
//  SP8  EXITOS DEL MOMENTO y SP9 ATAJOS (tus mas escuchadas, lo que escuchaste hace poco): en la letra SPOTIFY y en el buscador.
//  SP11 EL ESTADO EXTENDIDO MAS VIVO: la foto del artista de fondo y el ano del disco. Interruptor «artista_fondo=».
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        public bool CalidadSpotify = true;     // «calidad_spotify=» SP2 (dentro del modo Spotify, que esta apagado de fabrica)
        public bool LetrasSpotify = true;      // «letras_spotify=» SP1
        public bool PortadasSpotify = true;    // «portadas_spotify=» SP5
        public bool CompletarDiscos = true;    // «completar_discos=» SP6
        public bool ArtistaFondo = true;       // «artista_fondo=» SP11
        internal const float CorteDanada = 16000f;   // con los agudos cortados por debajo de esto (el repaso lo mide), mejor la de Spotify

        // ---------------- SP4: LAS PAREJAS ----------------
        private sealed class Pareja { public string Id, AlbumId, ArtistaId, Portada, Disco, Anio, Generos; public long Ms; public bool Ninguna; public long Fecha; }
        private readonly Dictionary<string, Pareja> parejas = new Dictionary<string, Pareja>(StringComparer.OrdinalIgnoreCase);
        private bool parejasLeidas;
        private Thread emparejador;
        private volatile int parejasVersion;
        private string ArchivoParejas { get { return Path.Combine(CarpetaDatos, "rocola_spotify_pares.txt"); } }

        private void LeeParejas()
        {
            if (parejasLeidas) return;
            parejasLeidas = true;
            try
            {
                if (!File.Exists(ArchivoParejas)) return;
                foreach (string l in File.ReadAllLines(ArchivoParejas))
                {
                    string[] c = l.Split('\t');
                    if (c.Length < 3) continue;
                    Pareja p = new Pareja();
                    long.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out p.Fecha);
                    if (c[1] == "-") p.Ninguna = true;
                    else if (c.Length >= 10)
                    {
                        p.Id = c[1]; long.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out p.Ms);
                        p.AlbumId = c[4]; p.ArtistaId = c[5]; p.Portada = c[6]; p.Disco = c[7]; p.Anio = c[8]; p.Generos = c[9];
                    }
                    else continue;
                    lock (spotifyCerrojo) parejas[c[0]] = p;
                }
            }
            catch (Exception ex) { LogError("Spotify: rocola_spotify_pares.txt", ex); }
            parejasVersion++;   // (lo que se calculo con las parejas aun sin leer se rehace: los artistas de tu biblioteca, P1)
        }
        private void ApuntaPareja(string ruta, Pareja p)
        {
            lock (spotifyCerrojo) parejas[ruta] = p;
            string l = p.Ninguna ? ruta + "\t-\t" + p.Fecha
                : string.Join("\t", ruta, p.Id, p.Fecha.ToString(CultureInfo.InvariantCulture), p.Ms.ToString(CultureInfo.InvariantCulture), p.AlbumId, p.ArtistaId,
                    p.Portada ?? "", Sin(p.Disco), p.Anio ?? "", Sin(p.Generos));
            try { File.AppendAllText(ArchivoParejas, l + "\n"); } catch { }
            parejasVersion++; spotifyVersion++;
        }
        private static string Sin(string s) { return (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '); }
        private Pareja ParejaDe(string ruta)
        {
            if (ruta == null) return null;
            Pareja p; lock (spotifyCerrojo) parejas.TryGetValue(ruta, out p);
            return p != null && !p.Ninguna ? p : null;
        }

        // El emparejador: un hilo de fondo, una busqueda cada 2,5 s mientras Spotify esta dentro. Las que no tuvieron pareja se
        // vuelven a buscar a los 30 dias (Spotify crece).
        private void ArrancaEmparejador()
        {
            LeeParejas();
            if (emparejador != null && emparejador.IsAlive) return;
            emparejador = new Thread(Empareja) { IsBackground = true, Name = "spotify (parejas)", Priority = ThreadPriority.BelowNormal };
            emparejador.Start();
        }
        private void Empareja()
        {
            try { EmparejaVueltas(); }
            catch (Exception ex) { LogError("Spotify: el emparejador", ex); }   // (una excepcion aqui cerraria la rocola entera)
        }
        private void EmparejaVueltas()
        {
            while (true)
            {
                long ahora = DateTime.UtcNow.Ticks, mes = TimeSpan.FromDays(30).Ticks;
                List<Track> lista;
                lock (tracks) lista = new List<Track>(tracks);
                int hechas, pendientes = 0; lock (spotifyCerrojo) hechas = parejas.Count;
                { SpotifyEnVivo s0 = spotify; if (s0 != null) s0.Apunta("emparejando con Spotify: " + lista.Count + " canciones, " + hechas + " ya vistas"); }
                foreach (Track t in lista)
                {
                    SpotifyEnVivo s = spotify;
                    if (s == null || !SpotifyOn || cerrandoEntreno) return;
                    while (!s.Conectado) { Thread.Sleep(5000); if (spotify != s || !SpotifyOn) return; }
                    Pareja ya; lock (spotifyCerrojo) parejas.TryGetValue(t.Path, out ya);
                    if (ya != null && (!ya.Ninguna || ahora - ya.Fecha < mes)) continue;
                    if (!(SegundosDe(t.Path) > 10f)) { pendientes++; continue; }   // sin la duracion (el repaso aun no la mide) no se puede asegurar: en la otra vuelta
                    bool contesto = true;
                    try { contesto = BuscaPareja(s, t); } catch (Exception ex) { LogError("Spotify: emparejar " + Path.GetFileName(t.Path), ex); }
                    Thread.Sleep(contesto ? 2500 : 60000);   // sin respuesta (Spotify pone limite de consultas): un minuto
                }
                // otra vuelta en 2 minutos: las que aun no tenian duracion, y las canciones nuevas
                for (int i = 0; i < 120; i++) { Thread.Sleep(1000); if (spotify == null || !SpotifyOn || cerrandoEntreno) return; }
            }
        }
        // Falso si Spotify no contesto (se espera antes de la siguiente).
        private bool BuscaPareja(SpotifyEnVivo s, Track t)
        {
            try { t.EnsureTags(); } catch { }
            string titulo = Normaliza(TituloSinPista(string.IsNullOrEmpty(t.Title) ? Path.GetFileNameWithoutExtension(t.Path) : t.Title));
            string artista = Normaliza(t.Artist);
            if (artista.Length == 0) { Album a = DiscoDe(t.Path); if (a != null) artista = Normaliza(a.Artist); }
            float seg = SegundosDe(t.Path);
            if (titulo.Length < 2 || artista.Length < 2 || !(seg > 10f))
            {   // sin titulo, artista o duracion no se puede asegurar que sea la misma: queda sin pareja (se vuelve a mirar en 30 dias)
                s.Apunta("sin pareja posible (titulo «" + titulo + "», artista «" + artista + "», " + seg.ToString("0", CultureInfo.InvariantCulture) + " s): " + Path.GetFileName(t.Path));
                ApuntaPareja(t.Path, new Pareja { Ninguna = true, Fecha = DateTime.UtcNow.Ticks });
                return true;
            }
            string q = "track:" + titulo + " artist:" + artista;
            Pareja mejor = null; double dmejor = 2.0;
            using (JsonDocument d = Api(s, "/v1/search?type=track&limit=5&q=" + Q(q)))
            {
                if (d == null) return false;   // (la red o Spotify: se reintenta la proxima vuelta)
                if (d.RootElement.TryGetProperty("tracks", out JsonElement tr) && tr.TryGetProperty("items", out JsonElement it))
                    foreach (JsonElement x in it.EnumerateArray())
                    {
                        string nt = Normaliza(SpotifyEnVivo.Texto(x, "name"));
                        if (!(nt == titulo || (titulo.Length >= 4 && (nt.StartsWith(titulo) || titulo.StartsWith(nt)) && Math.Abs(nt.Length - titulo.Length) <= 12))) continue;
                        bool artistaBien = false; string artistaId = "";
                        if (x.TryGetProperty("artists", out JsonElement ar))
                            foreach (JsonElement a in ar.EnumerateArray())
                            {
                                string na = Normaliza(SpotifyEnVivo.Texto(a, "name"));
                                if (na.Length > 0 && (artista.Contains(na) || na.Contains(artista))) { artistaBien = true; if (artistaId.Length == 0) artistaId = SpotifyEnVivo.Texto(a, "id"); }
                            }
                        if (!artistaBien) continue;
                        long ms = x.TryGetProperty("duration_ms", out JsonElement du) && du.ValueKind == JsonValueKind.Number ? du.GetInt64() : 0;
                        double dif = Math.Abs(ms / 1000.0 - seg);
                        if (dif > dmejor) continue;
                        dmejor = dif;
                        JsonElement al; x.TryGetProperty("album", out al);
                        string fecha = al.ValueKind == JsonValueKind.Object ? SpotifyEnVivo.Texto(al, "release_date") : "";
                        mejor = new Pareja
                        {
                            Id = SpotifyEnVivo.Texto(x, "id"), Ms = ms, ArtistaId = artistaId,
                            AlbumId = al.ValueKind == JsonValueKind.Object ? SpotifyEnVivo.Texto(al, "id") : "",
                            Portada = al.ValueKind == JsonValueKind.Object ? Imagen(al) : null,
                            Disco = al.ValueKind == JsonValueKind.Object ? SpotifyEnVivo.Texto(al, "name") : "",
                            Anio = fecha.Length >= 4 ? fecha.Substring(0, 4) : "",
                        };
                    }
            }
            if (mejor == null) { ApuntaPareja(t.Path, new Pareja { Ninguna = true, Fecha = DateTime.UtcNow.Ticks }); return true; }
            mejor.Fecha = DateTime.UtcNow.Ticks;
            // SP5: el genero del artista (para las canciones que no lo traen)
            if (mejor.ArtistaId.Length > 0)
                using (JsonDocument a = Api(s, "/v1/artists/" + mejor.ArtistaId))
                    if (a != null && a.RootElement.TryGetProperty("genres", out JsonElement g) && g.ValueKind == JsonValueKind.Array)
                    {
                        StringBuilder b = new StringBuilder();
                        foreach (JsonElement x in g.EnumerateArray()) { if (b.Length > 0) b.Append(", "); b.Append(x.GetString()); }
                        mejor.Generos = b.ToString();
                    }
            ApuntaPareja(t.Path, mejor);
            PonGeneroDeSpotify(t, mejor);
            PonPortadaDeSpotify(s, t.Path, mejor);
            return true;
        }
        // El disco de una cancion de la biblioteca (el indice, o buscandola en los discos si aun no esta).
        private Album DiscoDe(string ruta)
        {
            Album a;
            if (discoDeRuta != null && discoDeRuta.TryGetValue(ruta, out a) && a != null) return a;
            List<Album> l; lock (tracks) l = new List<Album>(albums);
            foreach (Album x in l) foreach (Track t in x.Tracks) if (string.Equals(t.Path, ruta, StringComparison.OrdinalIgnoreCase)) return x;
            return null;
        }
        // Para comparar titulos y artistas: sin acentos, sin mayusculas, sin lo que va entre parentesis o corchetes, sin «- Remaster...»,
        // «feat.» ni signos.
        internal static string Normaliza(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // «Titulo - Remastered 2011», «Titulo - En vivo», «Titulo - Live at...», «Titulo - Radio Edit»: lo de despues del guion, fuera
            int g = s.LastIndexOf(" - ", StringComparison.Ordinal);
            if (g > 0)
            {
                string cola = s.Substring(g + 3).ToLowerInvariant();
                foreach (string w in new[] { "remaster", "live", "en vivo", "version", "versión", "edit", "mono", "stereo", "estéreo", "mix", "acoustic", "acústic" })
                    if (cola.Contains(w)) { s = s.Substring(0, g); break; }
            }
            string d = s.Normalize(NormalizationForm.FormD);
            StringBuilder b = new StringBuilder(d.Length);
            int prof = 0;
            foreach (char ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (ch == '(' || ch == '[') { prof++; continue; }
                if (ch == ')' || ch == ']') { if (prof > 0) prof--; continue; }
                if (prof > 0) continue;
                b.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
            }
            string r = " " + b.ToString() + " ";
            foreach (string corta in new[] { " feat ", " ft ", " featuring " })
            {
                int i = r.IndexOf(corta, StringComparison.Ordinal);
                if (i > 2) r = r.Substring(0, i) + " ";
            }
            return string.Join(" ", r.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        // ---------------- SP5: PORTADAS Y GENEROS QUE FALTAN ----------------
        private void PonGeneroDeSpotify(Track t, Pareja p)
        {
            if (!PortadasSpotify || p == null || string.IsNullOrEmpty(p.Generos)) return;
            try { t.EnsureTags(); } catch { }
            if (string.IsNullOrEmpty(t.Genre)) t.Genre = p.Generos.Split(',')[0].Trim();
        }
        // Las portadas de Spotify de tus discos: instalada, en el disco de la rocola (junto a la musica, como el material de la IA: lo
        // aprendido, datos.img, mide 512 MB); desde la memoria USB, en los datos. Solo imagenes.
        private static string CarpetaPortadasSpotify
        {
            get { return Directory.Exists("/rocola/disco/rocola-os") ? "/rocola/disco/rocola-os/portadas_spotify" : Path.Combine(CarpetaDatos, "portadas_spotify"); }
        }
        private readonly Dictionary<string, string> portadaDeCarpeta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private void PonPortadaDeSpotify(SpotifyEnVivo s, string ruta, Pareja p)
        {
            if (!PortadasSpotify || p == null || string.IsNullOrEmpty(p.Portada) || string.IsNullOrEmpty(p.AlbumId)) return;
            Album a = DiscoDe(ruta); if (a == null || !string.IsNullOrEmpty(a.CoverPath)) return;
            string dir = CarpetaPortadasSpotify;
            try { Directory.CreateDirectory(dir); } catch { }
            string archivo = Path.Combine(dir, LimpiaClave(p.AlbumId) + ".jpg");
            if (!File.Exists(archivo))
                using (JsonDocument d = s.Api.Pide(ControlSpotify.Orden("baja", "url", p.Portada, "archivo", archivo), 20000)) { }
            if (!File.Exists(archivo)) return;
            a.CoverPath = archivo;
            lock (spotifyCerrojo) portadaDeCarpeta[a.Folder] = archivo;
            spotifyVersion++;
        }
        // La portada de Spotify de un disco tuyo que no tenia (para la pared: Controlador.RevisaSpotify); null si no hay.
        public string PortadaSpotifyDe(string carpeta)
        {
            if (!SpotifyOn || !PortadasSpotify || carpeta == null) return null;
            string p; lock (spotifyCerrojo) portadaDeCarpeta.TryGetValue(carpeta, out p);
            return p;
        }
        // Al arrancar con Spotify: lo ya emparejado pone sus portadas y generos (las imagenes ya bajadas estan en los datos).
        private void AplicaParejasGuardadas()
        {
            if (!PortadasSpotify || discoDeRuta == null) return;
            string dir = CarpetaPortadasSpotify;
            List<KeyValuePair<string, Pareja>> l; lock (spotifyCerrojo) l = new List<KeyValuePair<string, Pareja>>(parejas);
            foreach (KeyValuePair<string, Pareja> kv in l)
            {
                if (kv.Value.Ninguna) continue;
                Track t; if (pistaDeRuta != null && pistaDeRuta.TryGetValue(kv.Key, out t) && t != null) PonGeneroDeSpotify(t, kv.Value);
                Album a;
                if (discoDeRuta.TryGetValue(kv.Key, out a) && a != null && string.IsNullOrEmpty(a.CoverPath) && !string.IsNullOrEmpty(kv.Value.AlbumId))
                {
                    string f = Path.Combine(dir, LimpiaClave(kv.Value.AlbumId) + ".jpg");
                    if (File.Exists(f)) { a.CoverPath = f; lock (spotifyCerrojo) portadaDeCarpeta[a.Folder] = f; }
                }
            }
            spotifyVersion++;
        }

        // ---------------- SP2: LA VERSION DE SPOTIFY, SIN QUE SE NOTE ----------------
        private readonly HashSet<PlayItem> sinSpotify = new HashSet<PlayItem>();   // ya se intento con Spotify y no se pudo: suena la tuya
        private PlayItem spotifySustituida;   // la tuya que esta sonando desde Spotify
        // Si esta cancion tuya debe sonar desde Spotify: con los agudos cortados (medido) y con pareja.
        private string IdParaSustituir(PlayItem it)
        {
            if (!CalidadSpotify || !SpotifyListo || it == null || it.IsVideo || it.Song == null || EsItemSpotify(it) || sinSpotify.Contains(it)) return null;
            Pareja p = ParejaDe(it.Song.Path); if (p == null) return null;
            float[] c; float corte = eqCache.TryGetValue(it.Song.Path, out c) && c != null && c.Length > EqCut ? c[EqCut] : 0f;
            return corte > 100f && corte < CorteDanada ? p.Id : null;
        }
        private bool SeSustituye(PlayItem it) { return IdParaSustituir(it) != null; }
        // La pista de Spotify de una pareja, apuntada como las demas (para la cadena: su nivelado es el de Spotify, SP3).
        private Track PistaDeSustituta(PlayItem it, string id)
        {
            Pareja p = ParejaDe(it.Song.Path);
            string nombre = LimpiaNombre(it.Song.Artist + " - " + it.Song.Title);
            string ruta = RaizSpotify + id + "/" + nombre + ".spotify";
            lock (spotifyCerrojo)
                if (!spotifyPistas.ContainsKey(ruta))
                    spotifyPistas[ruta] = new PistaSpotify { Id = id, Titulo = it.Song.Title, Artista = it.Song.Artist, Disco = p != null ? p.Disco : "", Ms = p != null ? p.Ms : 0 };
            return new Track { Path = ruta, Title = it.Song.Title, Artist = it.Song.Artist, Album = it.Song.Album, Genre = it.Song.Genre };
        }

        // ---------------- SP1: LAS LETRAS ----------------
        public sealed class LineaLetra { public int Ms; public string Texto; }
        public sealed class Letra
        {
            public List<LineaLetra> Lineas = new List<LineaLetra>();
            public bool Sincronizada;
            public string Proveedor = "";
            public uint Fondo, Resalte;   // los colores que da Spotify para esa letra (0: no hay)
        }
        private readonly Dictionary<string, Letra> letras = new Dictionary<string, Letra>(StringComparer.Ordinal);
        private readonly HashSet<string> letrasPedidas = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> letraFalloEn = new Dictionary<string, int>(StringComparer.Ordinal);   // sin respuesta: se reintenta al minuto
        // El id de Spotify de lo que suena: la de Spotify, la tuya sustituida o la tuya con pareja.
        private string IdSonando()
        {
            PlayItem p = nowPlaying;
            if (p == null || p.IsVideo || p.Song == null) return null;
            if (EsItemSpotify(p)) { string id = p.Song.Path.Substring(RaizSpotify.Length); int b = id.IndexOf('/'); return b > 0 ? id.Substring(0, b) : id; }
            Pareja par = ParejaDe(p.Song.Path);
            return par != null ? par.Id : null;
        }
        // La letra de lo que suena (null si no hay, o aun no llega: se pide la primera vez).
        public Letra LetraSonando()
        {
            if (!SpotifyOn || !LetrasSpotify) return null;
            string id = IdSonando(); if (id == null) return null;
            SpotifyEnVivo s = spotify; if (s == null || !s.Conectado) return null;
            lock (spotifyCerrojo)
            {
                Letra l; if (letras.TryGetValue(id, out l)) return l;
                int fallo; if (letraFalloEn.TryGetValue(id, out fallo) && unchecked(Environment.TickCount - fallo) < 60000) return null;
                if (!letrasPedidas.Add(id)) return null;
                if (letras.Count > 60) letras.Clear();   // (solo las de esta noche: no se guardan)
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                Letra l = null;
                try { l = PideLetra(s, id); } catch { }
                lock (spotifyCerrojo) { if (l != null) letras[id] = l; letrasPedidas.Remove(id); if (l == null) letraFalloEn[id] = Environment.TickCount; }
            });
            return null;
        }
        private static Letra PideLetra(SpotifyEnVivo s, string id)
        {
            Letra l = new Letra();
            using (JsonDocument d = s.Api.Pide(ControlSpotify.Orden("letra", "id", id), 15000))
            {
                if (d == null || !s.Conectado) return null;   // sin respuesta (la red): se reintenta
                if (!SpotifyEnVivo.Bool(d.RootElement, "ok")) return l;   // sin letra: queda vacia (no se vuelve a pedir esta noche)
                try
                {
                    using (JsonDocument j = JsonDocument.Parse(SpotifyEnVivo.Texto(d.RootElement, "cuerpo")))
                    {
                        JsonElement r = j.RootElement;
                        if (r.TryGetProperty("colors", out JsonElement co))
                        {
                            if (co.TryGetProperty("background", out JsonElement bg) && bg.ValueKind == JsonValueKind.Number) l.Fondo = unchecked((uint)bg.GetInt32());
                            if (co.TryGetProperty("highlightText", out JsonElement hl) && hl.ValueKind == JsonValueKind.Number) l.Resalte = unchecked((uint)hl.GetInt32());
                        }
                        if (!r.TryGetProperty("lyrics", out JsonElement ly)) return l;
                        l.Sincronizada = SpotifyEnVivo.Texto(ly, "syncType") == "LINE_SYNCED" || SpotifyEnVivo.Texto(ly, "syncType") == "SYLLABLE_SYNCED";
                        l.Proveedor = SpotifyEnVivo.Texto(ly, "providerDisplayName");
                        if (ly.TryGetProperty("lines", out JsonElement ls))
                            foreach (JsonElement x in ls.EnumerateArray())
                            {
                                int ms = 0; string st = SpotifyEnVivo.Texto(x, "startTimeMs");
                                if (st.Length > 0) int.TryParse(st, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                                else if (x.TryGetProperty("startTimeMs", out JsonElement sn) && sn.ValueKind == JsonValueKind.Number) ms = sn.GetInt32();
                                l.Lineas.Add(new LineaLetra { Ms = ms, Texto = (SpotifyEnVivo.Texto(x, "words") ?? "").Replace("♪", "").Trim() });
                            }
                    }
                }
                catch { l.Lineas.Clear(); }
            }
            return l;
        }
        // Donde va lo que suena, en ms (lo que ya se oye: lo leido menos lo que la tarjeta aun tiene por delante).
        public int MsSonando(long cuadrosPorDelante)
        {
            CadenaCancion c = cadenaSonando;
            if (c == null) return -1;
            try
            {
                double s = c.Lector.CurrentTime.TotalSeconds - Math.Max(0, cuadrosPorDelante) / (double)Math.Max(1, c.Lector.WaveFormat.SampleRate);
                return (int)Math.Max(0, s * 1000);
            }
            catch { return -1; }
        }

        // ---------------- SP11: EL ARTISTA DE FONDO Y EL ANO ----------------
        private readonly Dictionary<string, string> fotoArtista = new Dictionary<string, string>(StringComparer.Ordinal);   // id del artista -> foto (en /run)
        private readonly HashSet<string> fotoPedida = new HashSet<string>(StringComparer.Ordinal);
        // La foto del artista de lo que suena (pequena: se ve difuminada al estirarla) y el ano del disco; null si no hay.
        public string FotoArtistaSonando(out string anio)
        {
            anio = null;
            if (!SpotifyOn || !ArtistaFondo) return null;
            PlayItem p = nowPlaying; if (p == null || p.IsVideo || p.Song == null) return null;
            string artistaId = null;
            if (EsItemSpotify(p)) { PistaSpotify ps; lock (spotifyCerrojo) spotifyPistas.TryGetValue(p.Song.Path, out ps); if (ps != null) { artistaId = ps.ArtistaId; anio = ps.Anio; } }
            else { Pareja par = ParejaDe(p.Song.Path); if (par != null) { artistaId = par.ArtistaId; anio = par.Anio; } }
            if (string.IsNullOrEmpty(artistaId)) return null;
            SpotifyEnVivo s = spotify; if (s == null || !s.Conectado) return null;
            lock (spotifyCerrojo)
            {
                string f; if (fotoArtista.TryGetValue(artistaId, out f)) return f;
                if (!fotoPedida.Add(artistaId)) return null;
            }
            string aid = artistaId;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string f = null;
                using (JsonDocument a = Api(s, "/v1/artists/" + aid))
                    if (a != null)
                    {
                        string url = null; int menor = int.MaxValue;   // la mas chica que pase de 150 px: estirada, ya se ve difuminada
                        if (a.RootElement.TryGetProperty("images", out JsonElement im))
                            foreach (JsonElement i in im.EnumerateArray())
                            {
                                int w = i.TryGetProperty("width", out JsonElement we) && we.ValueKind == JsonValueKind.Number ? we.GetInt32() : 0;
                                if (w >= 150 && w < menor) { menor = w; url = SpotifyEnVivo.Texto(i, "url"); }
                            }
                        if (url != null) f = BajaPortada(s, url, "artista_" + aid);
                    }
                lock (spotifyCerrojo) fotoArtista[aid] = f;
            });
            return null;
        }

        // ---------------- SP6: COMPLETAR TUS DISCOS ----------------
        private readonly Dictionary<string, List<string>> faltanEnDisco = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);   // carpeta -> rutas de Spotify
        private readonly HashSet<string> discoCompletado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Las canciones de Spotify que le faltan a este disco tuyo (vacio si no hay o aun no se sabe; se pide la primera vez).
        public List<string> FaltanEnDisco(Disco d)
        {
            List<string> nada = new List<string>();
            if (!SpotifyOn || !CompletarDiscos || d == null || d.Videos || EsRutaSpotify(d.Carpeta) || d.Carpeta == null) return nada;
            lock (spotifyCerrojo) { List<string> l; if (faltanEnDisco.TryGetValue(d.Carpeta, out l)) return l; }
            Dictionary<string, int> votos = new Dictionary<string, int>();
            HashSet<string> ids = new HashSet<string>(); List<string> titulos = new List<string>();
            foreach (string r in d.Rutas)
            {
                if (EsRutaSpotify(r)) continue;
                Pareja p = ParejaDe(r);
                Track t; if (pistaDeRuta != null && pistaDeRuta.TryGetValue(r, out t) && t != null) titulos.Add(Normaliza(TituloSinPista(t.Title)));
                if (p == null || string.IsNullOrEmpty(p.AlbumId)) continue;
                ids.Add(p.Id); int v; votos.TryGetValue(p.AlbumId, out v); votos[p.AlbumId] = v + 1;
            }
            // el disco de Spotify de la mayoria de sus canciones: con al menos dos parejas (en un disco de una o dos canciones, con una)
            int propias = 0; foreach (string r in d.Rutas) if (!EsRutaSpotify(r)) propias++;
            string album = null; int mas = Math.Min(2, Math.Max(1, propias)) - 1;
            foreach (KeyValuePair<string, int> kv in votos) if (kv.Value > mas) { mas = kv.Value; album = kv.Key; }
            if (album == null) return nada;
            SpotifyEnVivo s = spotify; if (s == null || !s.Conectado) return nada;
            lock (spotifyCerrojo) if (!discoCompletado.Add(d.Carpeta)) return nada;
            string carpeta = d.Carpeta; string portada = d.Portada;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> faltan = new List<string>();
                try
                {
                    using (JsonDocument a = Api(s, "/v1/albums/" + album))
                        if (a != null && a.RootElement.TryGetProperty("tracks", out JsonElement tr) && tr.TryGetProperty("items", out JsonElement ti))
                        {
                            string nombre = SpotifyEnVivo.Texto(a.RootElement, "name");
                            foreach (JsonElement x in ti.EnumerateArray())
                            {
                                string id = SpotifyEnVivo.Texto(x, "id"), nt = Normaliza(SpotifyEnVivo.Texto(x, "name"));
                                if (ids.Contains(id) || titulos.Contains(nt)) continue;   // esa ya la tienes
                                string ruta = ApuntaPista(x, nombre, RaizSpotify + "album/" + album, null);
                                if (ruta != null) faltan.Add(ruta);
                            }
                        }
                }
                catch (Exception ex) { LogError("Spotify: completar " + carpeta, ex); }
                lock (spotifyCerrojo) faltanEnDisco[carpeta] = faltan;
                spotifyVersion++;
            });
            return nada;
        }
        // Lo ya sabido de un disco (sin pedir nada): null si no se sabe o no toca.
        public List<string> FaltanSabidas(Disco d)
        {
            if (!SpotifyOn || !CompletarDiscos || d == null || d.Carpeta == null) return null;
            lock (spotifyCerrojo) { List<string> l; return faltanEnDisco.TryGetValue(d.Carpeta, out l) ? l : null; }
        }
        // El titulo con que sale en el disco (marcado, para que se sepa que viene de Spotify).
        public string TituloSpotify(string ruta)
        {
            PistaSpotify p; lock (spotifyCerrojo) spotifyPistas.TryGetValue(ruta, out p);
            return p == null ? Path.GetFileNameWithoutExtension(ruta) : p.Titulo + "  · Spotify";
        }

        // ---------------- SP9: LOS ATAJOS DEL BUSCADOR ----------------
        private List<Resultado> spotifyTop, spotifyRecientes;
        public List<Resultado> AtajoSpotify(int cual) { lock (spotifyCerrojo) return cual == 0 ? spotifyTop : spotifyRecientes; }
        private void CargaAtajos(SpotifyEnVivo s)
        {
            List<Resultado> top = ListaDeCanciones(s, "/v1/me/top/tracks?limit=20&time_range=short_term", false);
            List<Resultado> rec = ListaDeCanciones(s, "/v1/me/player/recently-played?limit=20", true);
            lock (spotifyCerrojo) { spotifyTop = top; spotifyRecientes = rec; }
            foreach (List<Resultado> l in new[] { top, rec })
                if (l != null)
                    foreach (Resultado x in l)
                    {
                        PistaSpotify p; lock (spotifyCerrojo) spotifyPistas.TryGetValue(x.Ruta, out p);
                        if (p != null && p.PortadaUrl != null) x.Portada = BajaPortada(s, p.PortadaUrl, p.DiscoCarpeta ?? p.Id);
                    }
            spotifyVersion++;
        }
        private List<Resultado> ListaDeCanciones(SpotifyEnVivo s, string ruta, bool envueltas)
        {
            List<Resultado> r = new List<Resultado>();
            HashSet<string> vistas = new HashSet<string>();
            using (JsonDocument d = Api(s, ruta))
                if (d != null && d.RootElement.TryGetProperty("items", out JsonElement it))
                    foreach (JsonElement x in it.EnumerateArray())
                    {
                        JsonElement t = x; if (envueltas && x.TryGetProperty("track", out JsonElement tt)) t = tt;
                        string rr = ApuntaPista(t, null, null, null); if (rr == null || !vistas.Add(rr)) continue;
                        PistaSpotify p; lock (spotifyCerrojo) p = spotifyPistas[rr];
                        r.Add(new Resultado { Tipo = 2, Ruta = rr, Texto = p.Titulo, Detalle = "Spotify · " + p.Artista, Carpeta = p.DiscoCarpeta });
                    }
            return r.Count > 0 ? r : null;
        }

        // ---------------- SP8: LOS EXITOS DEL MOMENTO ----------------
        // Las listas de Spotify de exitos (Top 50 y Virales de Mexico); si Spotify no las deja ver a programas de fuera, no salen.
        private List<Disco> ExitosDelMomento(SpotifyEnVivo s)
        {
            List<Disco> r = new List<Disco>();
            foreach (string q in new[] { "Top 50 Mexico", "Viral 50 Mexico" })
                using (JsonDocument d = Api(s, "/v1/search?type=playlist&limit=5&q=" + Q(q)))
                    if (d != null && d.RootElement.TryGetProperty("playlists", out JsonElement pl) && pl.TryGetProperty("items", out JsonElement pi))
                        foreach (JsonElement p in pi.EnumerateArray())
                        {
                            if (p.ValueKind != JsonValueKind.Object) continue;
                            string duenio = p.TryGetProperty("owner", out JsonElement ow) ? SpotifyEnVivo.Texto(ow, "id") : "";
                            if (duenio != "spotify") continue;   // solo las de Spotify
                            string id = SpotifyEnVivo.Texto(p, "id"); if (id.Length == 0) continue;
                            r.Add(DiscoSpotify(RaizSpotify + "playlist/" + id, SpotifyEnVivo.Texto(p, "name"), "Éxitos del momento", Imagen(p)));
                            break;
                        }
            return r;
        }
    }
}

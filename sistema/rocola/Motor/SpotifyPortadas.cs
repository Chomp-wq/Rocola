// SPOTIFY EN LAS PORTADAS (decision del dueno del 7-11-2026: «no muchos en mi familia saben usar el buscador»; «Haz todo» sobre P1-P4).
// Todo solo con el modo Spotify encendido (Spotify.cs), con creditos como siempre y nada que suene solo; cada una con su interruptor
// (Ctrl+F3 -> Spotify), encendidas:
//  P1  SPOTIFY EN LAS LETRAS (spotify_letras=): en la letra de cada artista de tu biblioteca, junto a sus discos, sus discos mas conocidos
//      de Spotify que no tienes (hasta 3), y tus discos guardados en Spotify en la letra de su artista; con una marquita de Spotify.
//  P2  MAS DE ESTE ARTISTA (mas_del_artista=): dentro de un disco (tuyo o de Spotify), debajo de la portada, una tira con los demas discos
//      del artista en Spotify (hasta 8, los mas conocidos primero). ABAJO al final de la lista entra a la tira; PONER abre el disco y
//      REGRESAR vuelve al de antes.
//  P3  TUS ARTISTAS CON FOTO (spotify_artistas=): en la letra SPOTIFY, los artistas que sigues y los que mas escuchas (si no hay, los de
//      tu biblioteca con mas canciones), con su foto. Dentro: lo mas conocido del artista y, en la tira de P2, sus discos.
//  P4  POR GENERO Y EPOCA (spotify_generos=): en la letra SPOTIFY, «Cumbias», «Rock en español», «Éxitos de los 80»... (una lista de
//      Spotify de cada uno, buscada por su nombre).
// Los discos de cada artista (los de tu biblioteca, por las parejas de SP4) los trae un hilo de fondo, uno cada 2,5 s, y se apuntan en
// rocola_spotify_artistas.txt (solo nombres y numeros; se vuelven a pedir a los 14 dias). Las portadas se bajan SOLO cuando se ven en la
// pantalla (QuierePortada), a la memoria (/run), como las demas de Spotify.
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
        public bool SpotifyEnLetras = true;    // «spotify_letras=» P1
        public bool VerMasDelArtista = true;   // «mas_del_artista=» P2
        public bool SpotifyArtistas = true;    // «spotify_artistas=» P3
        public bool SpotifyGeneros = true;     // «spotify_generos=» P4
        internal const int DiscosPorArtistaEnLetra = 3, DiscosEnLaTira = 8, ArtistasEnPared = 24;
        private static readonly string[][] GenerosYEpocas =
        {
            new[] { "Género", "Cumbias" }, new[] { "Género", "Rock en español" }, new[] { "Género", "Baladas en español" },
            new[] { "Género", "Banda" }, new[] { "Género", "Norteñas" }, new[] { "Género", "Salsa" }, new[] { "Género", "Rancheras" },
            new[] { "Género", "Pop en español" }, new[] { "Época", "Éxitos de los 80" }, new[] { "Época", "Éxitos de los 90" },
        };

        private sealed class AlbumSp { public string Id, Nombre, Artista, PortadaUrl, Anio; public int Popularidad; }
        private sealed class ArtistaSp { public string Id, Nombre, FotoUrl; public long Fecha; public List<AlbumSp> Discos = new List<AlbumSp>(); }
        // Un disco de Spotify para una letra de la pared: va junto a los discos de 'JuntoA' (el artista como esta en tu biblioteca).
        public sealed class DiscoEnLetra { public Disco D; public char Letra; public string JuntoA; }

        private readonly Dictionary<string, ArtistaSp> artistasSp = new Dictionary<string, ArtistaSp>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> artistaDeAlbum = new Dictionary<string, string>(StringComparer.Ordinal);   // album -> artista
        private readonly List<string> artistaUrgente = new List<string>();   // los que pide la tira (P2): primero
        private List<string> artistasTuyos = new List<string>();             // los que sigues y los que mas escuchas (P3)
        private List<Disco> spotifyGuardados = new List<Disco>();            // tus discos guardados (P1)
        private bool artistasLeidos;
        private Thread discografo;
        private volatile int letrasSpVersion;
        private static readonly long Refresco = TimeSpan.FromDays(14).Ticks;
        private string ArchivoArtistas { get { return Path.Combine(CarpetaDatos, "rocola_spotify_artistas.txt"); } }

        // La version de lo que va en las letras (la pantalla rehace las letras cuando cambia; 0 = nada de Spotify en las letras).
        public int VersionLetrasSpotify { get { return SpotifyOn && SpotifyEnLetras ? letrasSpVersion + 1 : 0; } }

        // Al cambiar P3 o P4: la letra SPOTIFY se vuelve a cargar ya (sin esperar a la vuelta de cada 30 minutos).
        internal void RecargaParedSpotify() { if (spotifyBibliotecaDesde != 0) spotifyBibliotecaDesde = unchecked(Environment.TickCount - 31 * 60 * 1000) | 1; }

        // ---- lo guardado ----
        private void LeeArtistas()
        {
            if (artistasLeidos) return;
            artistasLeidos = true;
            try
            {
                if (!File.Exists(ArchivoArtistas)) return;
                foreach (string l in File.ReadAllLines(ArchivoArtistas))
                {
                    string[] c = l.Split('\t');
                    if (c.Length >= 5 && c[0] == "A")
                    {
                        ArtistaSp a = new ArtistaSp { Id = c[1], Nombre = c[3], FotoUrl = c[4].Length > 0 ? c[4] : null };
                        long.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out a.Fecha);
                        lock (spotifyCerrojo) artistasSp[a.Id] = a;   // (uno mas nuevo del mismo artista sustituye al de antes)
                    }
                    else if (c.Length >= 8 && c[0] == "D")
                    {
                        ArtistaSp a; lock (spotifyCerrojo) artistasSp.TryGetValue(c[1], out a);
                        if (a == null) continue;
                        AlbumSp x = new AlbumSp { Id = c[2], Anio = c[4], PortadaUrl = c[5].Length > 0 ? c[5] : null, Nombre = c[6], Artista = c[7] };
                        int.TryParse(c[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out x.Popularidad);
                        lock (spotifyCerrojo) { a.Discos.Add(x); artistaDeAlbum[x.Id] = a.Id; }
                    }
                }
            }
            catch (Exception ex) { LogError("Spotify: rocola_spotify_artistas.txt", ex); }
        }
        private void ApuntaArtista(ArtistaSp a)
        {
            StringBuilder b = new StringBuilder();
            b.Append("A\t").Append(a.Id).Append('\t').Append(a.Fecha.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Sin(a.Nombre)).Append('\t').Append(a.FotoUrl ?? "").Append('\n');
            foreach (AlbumSp x in a.Discos)
                b.Append("D\t").Append(a.Id).Append('\t').Append(x.Id).Append('\t').Append(x.Popularidad.ToString(CultureInfo.InvariantCulture)).Append('\t')
                 .Append(x.Anio ?? "").Append('\t').Append(x.PortadaUrl ?? "").Append('\t').Append(Sin(x.Nombre)).Append('\t').Append(Sin(x.Artista)).Append('\n');
            try { File.AppendAllText(ArchivoArtistas, b.ToString()); } catch { }
        }

        // ---- el hilo que trae los discos de cada artista ----
        private void ArrancaDiscografo()
        {
            LeeArtistas();
            if (discografo != null && discografo.IsAlive) return;
            discografo = new Thread(Discografo) { IsBackground = true, Name = "spotify (discos de artistas)", Priority = ThreadPriority.BelowNormal };
            discografo.Start();
        }
        private void Discografo()
        {
            try
            {
                while (true)
                {
                    SpotifyEnVivo s = spotify;
                    if (s == null || !SpotifyOn || cerrandoEntreno) return;
                    if (!s.Conectado) { Thread.Sleep(5000); continue; }
                    string id = SiguienteArtista();
                    if (id == null)
                    {   // nada pendiente: se mira otra vez en un minuto (o antes, si la tira pide uno)
                        for (int i = 0; i < 60; i++) { Thread.Sleep(1000); if (spotify != s || !SpotifyOn) return; if (SiguienteArtista(false) != null) break; }
                        continue;
                    }
                    bool contesto = true;
                    try { contesto = TraeArtista(s, id); } catch (Exception ex) { LogError("Spotify: discos del artista " + id, ex); }
                    Thread.Sleep(contesto ? 2500 : 60000);
                }
            }
            catch (Exception ex) { LogError("Spotify: el hilo de los discos", ex); }   // (una excepcion aqui cerraria la rocola entera)
        }
        private bool Fresco(string id)
        {
            ArtistaSp a; lock (spotifyCerrojo) artistasSp.TryGetValue(id, out a);
            return a != null && DateTime.UtcNow.Ticks - a.Fecha < Refresco;
        }
        // El siguiente artista por traer: los que pide la tira, los de tu biblioteca (los de mas canciones primero; son los de las letras) y tus artistas.
        private string SiguienteArtista(bool saca = true)
        {
            lock (spotifyCerrojo)
            {
                while (artistaUrgente.Count > 0) { string u = artistaUrgente[0]; if (!saca) { if (!Fresco(u)) return u; artistaUrgente.RemoveAt(0); continue; } artistaUrgente.RemoveAt(0); if (!Fresco(u)) return u; }
            }
            foreach (KeyValuePair<string, ArtistaLocal> kv in ArtistasDeBiblioteca()) if (!Fresco(kv.Key)) return kv.Key;
            lock (spotifyCerrojo) foreach (string t in artistasTuyos) if (!Fresco(t)) return t;
            return null;
        }
        private bool TraeArtista(SpotifyEnVivo s, string id)
        {
            ArtistaSp a = new ArtistaSp { Id = id, Fecha = DateTime.UtcNow.Ticks };
            List<AlbumSp> l = new List<AlbumSp>();
            using (JsonDocument d = Api(s, "/v1/artists/" + id + "/albums?include_groups=album&limit=20&market=from_token"))
            {
                if (d == null) { a.Fecha = DateTime.UtcNow.Ticks - Refresco + TimeSpan.FromDays(1).Ticks; lock (spotifyCerrojo) if (!artistasSp.ContainsKey(id)) artistasSp[id] = a; return false; }   // en un dia, otra vez
                if (d.RootElement.TryGetProperty("items", out JsonElement it) && it.ValueKind == JsonValueKind.Array)
                    foreach (JsonElement x in it.EnumerateArray())
                    {
                        string aid = SpotifyEnVivo.Texto(x, "id"); if (aid.Length == 0) continue;
                        string fecha = SpotifyEnVivo.Texto(x, "release_date");
                        l.Add(new AlbumSp { Id = aid, Nombre = SpotifyEnVivo.Texto(x, "name"), Artista = Artistas(x), PortadaUrl = Imagen(x), Anio = fecha.Length >= 4 ? fecha.Substring(0, 4) : "" });
                        if (string.IsNullOrEmpty(a.Nombre) && x.TryGetProperty("artists", out JsonElement ars) && ars.ValueKind == JsonValueKind.Array)
                            foreach (JsonElement ar in ars.EnumerateArray()) if (SpotifyEnVivo.Texto(ar, "id") == id) { a.Nombre = SpotifyEnVivo.Texto(ar, "name"); break; }
                    }
            }
            // lo conocido de cada disco (popularidad), de veinte en veinte
            if (l.Count > 0)
            {
                Thread.Sleep(500);
                StringBuilder ids = new StringBuilder(); foreach (AlbumSp x in l) { if (ids.Length > 0) ids.Append(','); ids.Append(x.Id); }
                using (JsonDocument d = Api(s, "/v1/albums?market=from_token&ids=" + ids))
                    if (d != null && d.RootElement.TryGetProperty("albums", out JsonElement al) && al.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement x in al.EnumerateArray())
                        {
                            if (x.ValueKind != JsonValueKind.Object) continue;
                            string aid = SpotifyEnVivo.Texto(x, "id");
                            int pop = x.TryGetProperty("popularity", out JsonElement pe) && pe.ValueKind == JsonValueKind.Number ? pe.GetInt32() : 0;
                            foreach (AlbumSp y in l) if (y.Id == aid) y.Popularidad = pop;
                        }
            }
            // el mismo disco varias veces (remasterizado, de lujo, aniversario...): se queda el mas conocido
            l.Sort((p, q) => q.Popularidad.CompareTo(p.Popularidad));
            HashSet<string> vistos = new HashSet<string>(StringComparer.Ordinal);
            foreach (AlbumSp x in l) if (vistos.Add(NombreBase(x.Nombre))) a.Discos.Add(x);
            // la foto (para tus artistas, P3), si no se tiene
            ArtistaSp antes; lock (spotifyCerrojo) artistasSp.TryGetValue(id, out antes);
            a.FotoUrl = antes != null ? antes.FotoUrl : null;
            if (a.FotoUrl == null || string.IsNullOrEmpty(a.Nombre))
            {
                Thread.Sleep(500);
                using (JsonDocument d = Api(s, "/v1/artists/" + id))
                    if (d != null) { a.FotoUrl = Imagen(d.RootElement) ?? a.FotoUrl; if (string.IsNullOrEmpty(a.Nombre)) a.Nombre = SpotifyEnVivo.Texto(d.RootElement, "name"); }
            }
            lock (spotifyCerrojo)
            {
                artistasSp[id] = a; foreach (AlbumSp x in a.Discos) artistaDeAlbum[x.Id] = id;
                Disco suyo;   // P3: el artista en la pared, con la foto que faltaba
                if (a.FotoUrl != null && spotifyDiscosPorCarpeta.TryGetValue(RaizSpotify + "artist/" + id, out suyo) && suyo.Portada == null) spotifyPortadaUrl[suyo] = a.FotoUrl;
            }
            ApuntaArtista(a);
            s.Apunta("discos de " + a.Nombre + ": " + a.Discos.Count);
            letrasSpVersion++; spotifyVersion++;
            return true;
        }
        // «Disco (Remastered 2011)», «Disco [Deluxe]», «Disco - Edición especial» -> «disco»
        internal static string NombreBase(string s)
        {
            StringBuilder b = new StringBuilder(); int dentro = 0;
            foreach (char c in s ?? "") { if (c == '(' || c == '[') dentro++; else if (c == ')' || c == ']') { if (dentro > 0) dentro--; } else if (dentro == 0) b.Append(c); }
            return Normaliza(b.ToString());
        }

        // ---- los artistas de tu biblioteca (por las parejas de SP4) ----
        private sealed class ArtistaLocal { public string Nombre; public char Letra; public int Canciones; }
        private List<KeyValuePair<string, ArtistaLocal>> artistasLocales;
        private int artistasLocalesDe = -1;
        private List<KeyValuePair<string, ArtistaLocal>> ArtistasDeBiblioteca()
        {
            int v = parejasVersion;
            lock (spotifyCerrojo) if (artistasLocales != null && artistasLocalesDe == v) return artistasLocales;
            Dictionary<string, ArtistaLocal> d = new Dictionary<string, ArtistaLocal>(StringComparer.Ordinal);
            List<KeyValuePair<string, Pareja>> ps; lock (spotifyCerrojo) ps = new List<KeyValuePair<string, Pareja>>(parejas);
            foreach (KeyValuePair<string, Pareja> kv in ps)
            {
                if (kv.Value.Ninguna || string.IsNullOrEmpty(kv.Value.ArtistaId)) continue;
                Album a; if (discoDeRuta == null || !discoDeRuta.TryGetValue(kv.Key, out a) || a == null || a.IsTop) continue;
                ArtistaLocal x; if (!d.TryGetValue(kv.Value.ArtistaId, out x)) { x = new ArtistaLocal { Nombre = a.Artist, Letra = a.Letter }; d[kv.Value.ArtistaId] = x; }
                x.Canciones++;
            }
            List<KeyValuePair<string, ArtistaLocal>> l = new List<KeyValuePair<string, ArtistaLocal>>(d);
            l.Sort((p, q) => q.Value.Canciones.CompareTo(p.Value.Canciones));
            lock (spotifyCerrojo) { artistasLocales = l; artistasLocalesDe = v; }
            return l;
        }
        // Lo que ya tienes: los discos de Spotify de tus canciones (sus parejas) y los nombres de tus discos, por artista.
        private HashSet<string> tienesAlbumes, tienesNombres;
        private int tienesDe = -1, tienesCuando;
        private void LoQueTienes(out HashSet<string> albumes, out HashSet<string> nombres)
        {
            int v = parejasVersion, ahora = Environment.TickCount;
            lock (spotifyCerrojo)
                if (tienesAlbumes != null && tienesDe == v && unchecked(ahora - tienesCuando) < 60000) { albumes = tienesAlbumes; nombres = tienesNombres; return; }
            albumes = new HashSet<string>(StringComparer.Ordinal); nombres = new HashSet<string>(StringComparer.Ordinal);
            List<Pareja> ps; lock (spotifyCerrojo) ps = new List<Pareja>(parejas.Values);
            foreach (Pareja p in ps) if (!p.Ninguna && !string.IsNullOrEmpty(p.AlbumId)) albumes.Add(p.AlbumId);
            List<Album> l; lock (tracks) l = new List<Album>(albums);
            foreach (Album a in l) if (!a.IsTop) nombres.Add(Normaliza(a.Artist) + "\u0001" + NombreBase(a.Name));
            lock (spotifyCerrojo) { tienesAlbumes = albumes; tienesNombres = nombres; tienesDe = v; tienesCuando = ahora; }   // (se rehace al cambiar las parejas, o al minuto)
        }
        private bool LoTienes(AlbumSp x, string artistaLocal, HashSet<string> albumes, HashSet<string> nombres)
        {
            return albumes.Contains(x.Id) || nombres.Contains(Normaliza(artistaLocal) + "\u0001" + NombreBase(x.Nombre)) || nombres.Contains(Normaliza(x.Artista) + "\u0001" + NombreBase(x.Nombre));
        }

        // ---- P1: lo que va en las letras ----
        public List<DiscoEnLetra> DiscosSpotifyPorLetra()
        {
            List<DiscoEnLetra> r = new List<DiscoEnLetra>();
            if (!SpotifyOn || !SpotifyEnLetras) return r;
            HashSet<string> albumes, nombres; LoQueTienes(out albumes, out nombres);
            HashSet<string> puestos = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ArtistaLocal> kv in ArtistasDeBiblioteca())
            {
                ArtistaSp a; lock (spotifyCerrojo) artistasSp.TryGetValue(kv.Key, out a);
                if (a == null) continue;
                int n = 0;
                List<AlbumSp> discos; lock (spotifyCerrojo) discos = new List<AlbumSp>(a.Discos);
                foreach (AlbumSp x in discos)
                {
                    if (n >= DiscosPorArtistaEnLetra) break;
                    if (LoTienes(x, kv.Value.Nombre, albumes, nombres) || !puestos.Add(x.Id)) continue;
                    r.Add(new DiscoEnLetra { D = DiscoDeAlbum(x), Letra = kv.Value.Letra, JuntoA = kv.Value.Nombre });
                    n++;
                }
            }
            List<Disco> guardados; lock (spotifyCerrojo) guardados = new List<Disco>(spotifyGuardados);
            foreach (Disco g in guardados)
            {
                string id = g.Carpeta.Substring((RaizSpotify + "album/").Length);
                if (albumes.Contains(id) || nombres.Contains(Normaliza(g.Artista) + "\u0001" + NombreBase(g.Nombre)) || !puestos.Add(id)) continue;
                r.Add(new DiscoEnLetra { D = g, Letra = FirstLetter(g.Artista), JuntoA = g.Artista });
            }
            return r;
        }
        private Disco DiscoDeAlbum(AlbumSp x) { return DiscoSpotify(RaizSpotify + "album/" + x.Id, x.Nombre, x.Artista, x.PortadaUrl); }
        // Para la pantalla: el mismo artista (sin acentos ni mayusculas), para poner los de Spotify junto a los tuyos.
        public static string ClaveArtista(string s) { return Normaliza(s); }

        // ---- P2: la tira «Más de este artista» ----
        private readonly Dictionary<string, string> artistaDeDisco = new Dictionary<string, string>(StringComparer.Ordinal);
        // Los otros discos del artista de 'd' en Spotify (null si no hay o aun no se sabe: se piden y llegan en unos segundos).
        public List<Disco> MasDelArtista(Disco d, out string artista)
        {
            artista = null;
            if (!SpotifyOn || !VerMasDelArtista || d == null || d.Videos || d.Carpeta == null || !SpotifyListo) return null;
            string id = ArtistaDeDisco(d); if (id == null) return null;
            ArtistaSp a; lock (spotifyCerrojo) artistasSp.TryGetValue(id, out a);
            if (a == null || DateTime.UtcNow.Ticks - a.Fecha >= Refresco)
            {
                lock (spotifyCerrojo) if (!artistaUrgente.Contains(id)) artistaUrgente.Add(id);
                if (a == null) return null;
            }
            artista = a.Nombre;
            string propio = d.Carpeta.StartsWith(RaizSpotify + "album/", StringComparison.Ordinal) ? d.Carpeta.Substring((RaizSpotify + "album/").Length) : null;
            HashSet<string> albumes, nombres; LoQueTienes(out albumes, out nombres);
            string nombreDisco = EsRutaSpotify(d.Carpeta) ? null : NombreBase(d.Nombre);
            List<Disco> r = new List<Disco>();
            List<AlbumSp> discos; lock (spotifyCerrojo) discos = new List<AlbumSp>(a.Discos);
            foreach (AlbumSp x in discos)
            {
                if (r.Count >= DiscosEnLaTira) break;
                if (x.Id == propio || (nombreDisco != null && NombreBase(x.Nombre) == nombreDisco)) continue;   // este mismo
                if (albumes.Contains(x.Id) || nombres.Contains(Normaliza(x.Artista) + "\u0001" + NombreBase(x.Nombre))) continue;   // uno tuyo (ya esta en tu pared)
                r.Add(DiscoDeAlbum(x));
            }
            return r.Count > 0 ? r : null;
        }
        private string ArtistaDeDisco(Disco d)
        {
            string id; lock (spotifyCerrojo) if (artistaDeDisco.TryGetValue(d.Carpeta, out id)) return id;
            string c = d.Carpeta;
            if (c.StartsWith(RaizSpotify + "artist/", StringComparison.Ordinal)) id = c.Substring((RaizSpotify + "artist/").Length);
            else if (EsRutaSpotify(c))
            {
                if (c.StartsWith(RaizSpotify + "album/", StringComparison.Ordinal)) lock (spotifyCerrojo) artistaDeAlbum.TryGetValue(c.Substring((RaizSpotify + "album/").Length), out id);
                if (id == null)
                {   // el artista de la mayoria de sus canciones (una lista de varios artistas no lleva tira)
                    Dictionary<string, int> votos = new Dictionary<string, int>(StringComparer.Ordinal); int total = 0;
                    List<string> rutas = d.Rutas;
                    foreach (string r in rutas) { PistaSpotify p; lock (spotifyCerrojo) spotifyPistas.TryGetValue(r, out p); if (p == null || string.IsNullOrEmpty(p.ArtistaId)) continue; total++; int v; votos.TryGetValue(p.ArtistaId, out v); votos[p.ArtistaId] = v + 1; }
                    foreach (KeyValuePair<string, int> kv in votos) if (kv.Value * 2 > total && total > 0) id = kv.Key;
                    if (id == null) return null;   // (aun sin canciones: se vuelve a mirar)
                }
            }
            else
            {
                Dictionary<string, int> votos = new Dictionary<string, int>(StringComparer.Ordinal); int total = 0;
                foreach (string r in d.Rutas) { if (EsRutaSpotify(r)) continue; Pareja p = ParejaDe(r); if (p == null || string.IsNullOrEmpty(p.ArtistaId)) continue; total++; int v; votos.TryGetValue(p.ArtistaId, out v); votos[p.ArtistaId] = v + 1; }
                foreach (KeyValuePair<string, int> kv in votos) if (kv.Value * 2 > total && total > 0) id = kv.Key;
                if (id == null) return null;   // (sin parejas aun: el emparejador va llegando)
            }
            lock (spotifyCerrojo) artistaDeDisco[c] = id;
            return id;
        }

        // El ano de un disco de Spotify (para la tira), o "" si no se sabe.
        public string AnioDeDisco(Disco d)
        {
            if (d == null || d.Carpeta == null || !d.Carpeta.StartsWith(RaizSpotify + "album/", StringComparison.Ordinal)) return "";
            string id = d.Carpeta.Substring((RaizSpotify + "album/").Length), aid; ArtistaSp a;
            lock (spotifyCerrojo)
            {
                if (!artistaDeAlbum.TryGetValue(id, out aid) || !artistasSp.TryGetValue(aid, out a)) return "";
                foreach (AlbumSp x in a.Discos) if (x.Id == id) return x.Anio ?? "";
            }
            return "";
        }

        // ---- P3 y P4: en la letra SPOTIFY ----
        // Tus artistas: los que sigues y los que mas escuchas; si no hay (la cuenta no da permiso), los de tu biblioteca con mas canciones.
        private List<Disco> TusArtistas(SpotifyEnVivo s)
        {
            List<Disco> r = new List<Disco>();
            if (!SpotifyArtistas) return r;
            List<string> ids = new List<string>(); HashSet<string> vistos = new HashSet<string>(StringComparer.Ordinal);
            Action<JsonElement> uno = delegate (JsonElement a)
            {
                if (r.Count >= ArtistasEnPared || a.ValueKind != JsonValueKind.Object) return;
                string id = SpotifyEnVivo.Texto(a, "id"); if (id.Length == 0 || !vistos.Add(id)) return;
                ids.Add(id);
                r.Add(DiscoSpotify(RaizSpotify + "artist/" + id, "", SpotifyEnVivo.Texto(a, "name"), Imagen(a)));
            };
            using (JsonDocument d = Api(s, "/v1/me/top/artists?limit=20&time_range=medium_term"))
                if (d != null && d.RootElement.TryGetProperty("items", out JsonElement it) && it.ValueKind == JsonValueKind.Array) foreach (JsonElement a in it.EnumerateArray()) uno(a);
            using (JsonDocument d = Api(s, "/v1/me/following?type=artist&limit=50"))
                if (d != null && d.RootElement.TryGetProperty("artists", out JsonElement ar) && ar.TryGetProperty("items", out JsonElement it) && it.ValueKind == JsonValueKind.Array) foreach (JsonElement a in it.EnumerateArray()) uno(a);
            if (r.Count == 0)
                foreach (KeyValuePair<string, ArtistaLocal> kv in ArtistasDeBiblioteca())
                {
                    if (r.Count >= ArtistasEnPared) break;
                    ArtistaSp a; lock (spotifyCerrojo) artistasSp.TryGetValue(kv.Key, out a);
                    ids.Add(kv.Key);
                    r.Add(DiscoSpotify(RaizSpotify + "artist/" + kv.Key, "", a != null && !string.IsNullOrEmpty(a.Nombre) ? a.Nombre : kv.Value.Nombre, a != null ? a.FotoUrl : null));
                }
            lock (spotifyCerrojo) artistasTuyos = ids;
            return r;
        }
        private List<Disco> PorGeneroYEpoca(SpotifyEnVivo s)
        {
            List<Disco> r = new List<Disco>();
            if (!SpotifyGeneros) return r;
            foreach (string[] g in GenerosYEpocas)
            {
                if (spotify != s) break;
                Disco elegido = null, otro = null;
                using (JsonDocument d = Api(s, "/v1/search?type=playlist&limit=5&q=" + Q(g[1])))
                    if (d != null && d.RootElement.TryGetProperty("playlists", out JsonElement pl) && pl.TryGetProperty("items", out JsonElement pi) && pi.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement p in pi.EnumerateArray())
                        {
                            if (p.ValueKind != JsonValueKind.Object) continue;   // (Spotify da huecos null en las listas)
                            string id = SpotifyEnVivo.Texto(p, "id"); if (id.Length == 0) continue;
                            string duenio = p.TryGetProperty("owner", out JsonElement ow) ? SpotifyEnVivo.Texto(ow, "id") : "";
                            if (duenio == "spotify") { elegido = DiscoSpotify(RaizSpotify + "playlist/" + id, g[1], g[0], Imagen(p)); break; }   // mejor la de Spotify
                            if (otro == null) otro = DiscoSpotify(RaizSpotify + "playlist/" + id, g[1], g[0], Imagen(p));
                        }
                if (elegido == null) elegido = otro;
                if (elegido != null) { elegido.Nombre = g[1]; elegido.Artista = g[0]; r.Add(elegido); }
                Thread.Sleep(300);
            }
            return r;
        }

        // ---- las portadas, solo las que se ven ----
        private readonly Queue<Disco> portadasPedidas = new Queue<Disco>();
        private readonly HashSet<Disco> portadaPedida = new HashSet<Disco>();
        private bool bajandoPortadas;
        // La pantalla lo llama por cada disco de Spotify que dibuja sin portada (barato: solo lo apunta).
        public void QuierePortada(Disco d)
        {
            if (d == null || d.Portada != null || !EsRutaSpotify(d.Carpeta)) return;
            SpotifyEnVivo s = spotify; if (s == null || !s.Conectado) return;
            lock (spotifyCerrojo)
            {
                if (!spotifyPortadaUrl.ContainsKey(d) || !portadaPedida.Add(d)) return;
                portadasPedidas.Enqueue(d);
                if (bajandoPortadas) return;
                bajandoPortadas = true;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    while (true)
                    {
                        Disco x;
                        lock (spotifyCerrojo) { if (portadasPedidas.Count == 0) { bajandoPortadas = false; return; } x = portadasPedidas.Dequeue(); }
                        try { PonPortada(s, x); } catch (Exception ex) { LogError("Spotify: portada", ex); }
                    }
                }
                catch { lock (spotifyCerrojo) bajandoPortadas = false; }
            });
        }
    }
}

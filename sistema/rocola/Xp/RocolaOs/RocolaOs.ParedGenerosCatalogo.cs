// LA PARED POR GENEROS: EL CATALOGO Y SUS PORTADAS (fase 11, grupo pared-generos; PLAN_PARED_GENEROS.md; FASE11_pared-generos.md).
// Lo que no toca la pantalla: bajar el catalogo de la rocola (la version «catalogo» de GitHub del repositorio publico, sin llave), leerlo
// y guardarlo en el DISCO de la rocola (nunca en datos.img), y bajar las portadas del Cover Art Archive al verse. Nada de esto pregunta a la
// API de Spotify. La pantalla (RocolaOs.ParedGeneros.cs) solo lee lo que dejan aqui.
//
// EL FORMATO «I8» (contrato con el grupo catalogo-datos): texto UTF-8 en gzip, campos separados por tabuladores:
//   #rocola-catalogo	1	fecha	artistas	discos
//   L	letra	artistas	discos                                   (las letras: # y de la A a la Z)
//   A	nombre	genero	fama	id_spotify	pais	w|m          (los artistas de la letra, del mas al menos famoso)
//   D	nombre	ano	id_spotify	mbid_grupo	portada(1|0)	[marcas]   (tras cada A, sus discos, del mas al menos escuchado; la columna 7, desde el
//                                                              27-11-2026, dice por que no va en la pared: letras de RocolaOs.ParedExclusiones.cs; un
//                                                              rocola-xp viejo la ignora, y este lee tambien el catalogo sin ella)
// Y a su lado rocola_catalogo.txt, con su sha256 y sus bytes («sha256=HEX» y «bytes=N»; vale tambien la salida de sha256sum).
// En memoria: los bytes sin comprimir y un indice por letra (donde empieza y acaba); los discos de una letra se leen al armarla.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Rocola
{
    public partial class Form1
    {
        internal sealed class RocolaOsPgArtista
        {
            internal string Nombre = "", Genero = "Otros", IdSpotify = "";
            internal string Marcas = "";                     // (27-11-2026) lo que saca de la pared a todos sus discos (genero o nombre: RocolaOs.ParedExclusiones.cs)
            internal int Fama;
            internal readonly List<RocolaOsPgDisco> Discos = new List<RocolaOsPgDisco>();
        }
        internal sealed class RocolaOsPgDisco
        {
            internal RocolaOsPgArtista Art;
            internal string Nombre = "", Anio = "", IdSpotify = "", Mbid = "", Clave = "";
            internal string Marcas = "";                     // (27-11-2026) s j m k i d h: la columna 7 del archivo y lo que dice su nombre (RocolaOs.ParedExclusiones.cs)
            internal bool Portada;
            // (27-11-2026) NO VA EN LA PARED: bandas sonoras, videojuegos y cosas asi (el suyo o el de su artista) y los discos sin portada; solo el buscador
            internal bool Excluido { get { return Marcas.Length > 0 || Art.Marcas.Length > 0 || !Portada; } }
        }
        internal sealed class RocolaOsPgCatalogo
        {
            internal byte[] Datos;
            internal string Fecha = "", Sha = "";
            internal int Artistas, Discos;
            internal readonly Dictionary<char, int[]> Bloques = new Dictionary<char, int[]>();          // letra -> {inicio, fin, artistas, discos}
            internal readonly Dictionary<string, int> PorGenero = new Dictionary<string, int>(StringComparer.Ordinal);   // etiqueta de XP -> discos
        }

        // ================================================================= leer =========================================================
        private static bool RocolaOsPgLetraValida(char c) { return c == '#' || (c >= 'A' && c <= 'Z'); }

        // los campos de un renglon [ini, fin): sus limites, sin copiar
        private static int RocolaOsPgCampos(byte[] b, int ini, int fin, int[] lim)
        {
            int n = 0, s = ini;
            for (int i = ini; i <= fin && n * 2 + 1 < lim.Length; i++)
                if (i == fin || b[i] == (byte)'\t') { lim[n * 2] = s; lim[n * 2 + 1] = i; n++; s = i + 1; }
            return n;
        }
        private static string RocolaOsPgTexto(byte[] b, int[] lim, int k, int n) { return k < n ? Encoding.UTF8.GetString(b, lim[k * 2], lim[k * 2 + 1] - lim[k * 2]) : ""; }

        // EL INDICE: valida la cabecera y apunta donde empieza y acaba cada letra; cuenta los discos de cada genero (para el orden). null si no vale.
        internal static RocolaOsPgCatalogo RocolaOsPgIndexa(byte[] datos, out string error)
        {
            error = null;
            RocolaOsPgCatalogo c = new RocolaOsPgCatalogo { Datos = datos };
            int[] lim = new int[16];
            Dictionary<string, string> etiquetas = new Dictionary<string, string>(StringComparer.Ordinal);
            int pos = 0, n = datos.Length;
            bool cabecera = false;
            char letra = '\0'; int inicio = 0, nA = 0, nD = 0;
            string genero = "Otros";
            Action cierra = delegate { if (letra != '\0') c.Bloques[letra] = new int[] { inicio, pos, nA, nD }; };
            while (pos < n)
            {
                int fin = Array.IndexOf(datos, (byte)'\n', pos);
                if (fin < 0) fin = n;
                int f = fin;
                if (f > pos && datos[f - 1] == (byte)'\r') f--;
                if (f > pos)
                {
                    int k = RocolaOsPgCampos(datos, pos, f, lim);
                    if (!cabecera)
                    {
                        if (RocolaOsPgTexto(datos, lim, 0, k) != "#rocola-catalogo" || RocolaOsPgTexto(datos, lim, 1, k) != "1") { error = "la cabecera no es la de un catalogo de la version 1"; return null; }
                        c.Fecha = RocolaOsPgTexto(datos, lim, 2, k);
                        cabecera = true;
                    }
                    else if (lim[1] - lim[0] == 1)
                    {
                        byte t = datos[pos];
                        if (t == (byte)'L')
                        {
                            cierra();
                            string l = RocolaOsPgTexto(datos, lim, 1, k);
                            letra = l.Length == 1 && RocolaOsPgLetraValida(l[0]) ? l[0] : '\0';
                            inicio = fin + 1; nA = 0; nD = 0;
                        }
                        else if (t == (byte)'A' && letra != '\0')
                        {
                            string g = RocolaOsPgTexto(datos, lim, 2, k), et;
                            if (!etiquetas.TryGetValue(g, out et)) { et = GenreLabel(g); etiquetas[g] = et; }
                            genero = et; nA++; c.Artistas++;
                        }
                        else if (t == (byte)'D' && letra != '\0' && nA > 0)
                        {
                            nD++; c.Discos++;
                            int ya; c.PorGenero.TryGetValue(genero, out ya); c.PorGenero[genero] = ya + 1;
                        }
                    }
                }
                pos = fin + 1;
            }
            if (pos > n) pos = n;
            cierra();
            if (!cabecera) { error = "vacio"; return null; }
            return c;
        }

        // LOS ARTISTAS Y DISCOS DE UNA LETRA, en el orden del archivo (desde cualquier hilo: solo lee)
        internal static List<RocolaOsPgArtista> RocolaOsPgLeeLetra(RocolaOsPgCatalogo c, char letra)
        {
            List<RocolaOsPgArtista> l = new List<RocolaOsPgArtista>();
            int[] b;
            if (c == null || !c.Bloques.TryGetValue(letra, out b)) return l;
            byte[] d = c.Datos;
            int[] lim = new int[16];
            Dictionary<string, string> etiquetas = new Dictionary<string, string>(StringComparer.Ordinal);
            int pos = b[0], n = Math.Min(b[1], d.Length);
            RocolaOsPgArtista ar = null;
            while (pos < n)
            {
                int fin = Array.IndexOf(d, (byte)'\n', pos, n - pos);
                if (fin < 0) fin = n;
                int f = fin;
                if (f > pos && d[f - 1] == (byte)'\r') f--;
                if (f > pos + 1 && d[pos + 1] == (byte)'\t')
                {
                    int k = RocolaOsPgCampos(d, pos, f, lim);
                    if (d[pos] == (byte)'A')
                    {
                        string g = RocolaOsPgTexto(d, lim, 2, k), et;
                        if (!etiquetas.TryGetValue(g, out et)) { et = GenreLabel(g); etiquetas[g] = et; }
                        int fama; int.TryParse(RocolaOsPgTexto(d, lim, 3, k), NumberStyles.Integer, CultureInfo.InvariantCulture, out fama);
                        ar = new RocolaOsPgArtista { Nombre = RocolaOsPgTexto(d, lim, 1, k).Trim(), Genero = et, Fama = fama, IdSpotify = RocolaOsPgTexto(d, lim, 4, k).Trim() };
                        if (ar.Nombre.Length > 0) { ar.Marcas = RocolaOsPgExclusiones.MarcasArtista(ar.Nombre, et); l.Add(ar); } else ar = null;
                    }
                    else if (d[pos] == (byte)'D' && ar != null)
                    {
                        RocolaOsPgDisco x = new RocolaOsPgDisco
                        {
                            Art = ar, Nombre = RocolaOsPgTexto(d, lim, 1, k).Trim(), Anio = RocolaOsPgTexto(d, lim, 2, k).Trim(),
                            IdSpotify = RocolaOsPgTexto(d, lim, 3, k).Trim(), Mbid = RocolaOsPgTexto(d, lim, 4, k).Trim(), Portada = RocolaOsPgTexto(d, lim, 5, k).Trim() == "1",
                        };
                        if (x.Nombre.Length == 0) { pos = fin + 1; continue; }
                        if (!RocolaOsPgMbidValido(x.Mbid)) { x.Mbid = ""; x.Portada = false; }
                        x.Marcas = RocolaOsPgExclusiones.Junta(RocolaOsPgTexto(d, lim, 6, k), RocolaOsPgExclusiones.Marcas(x.Nombre, null, null));
                        x.Clave = x.Mbid.Length > 0 ? x.Mbid : x.IdSpotify.Length > 0 ? "sp-" + x.IdSpotify : "n-" + MotorBusqueda.Normaliza(ar.Nombre) + "|" + MotorBusqueda.Normaliza(x.Nombre);
                        ar.Discos.Add(x);
                    }
                }
                pos = fin + 1;
            }
            return l;
        }
        // un mbid se usa como nombre de archivo y en la direccion: solo hexadecimal y guiones
        internal static bool RocolaOsPgMbidValido(string m)
        {
            if (string.IsNullOrEmpty(m) || m.Length > 40) return false;
            foreach (char ch in m) if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F') || ch == '-')) return false;
            return true;
        }

        // ================================================================= bajar el catalogo ============================================
        internal static class RocolaOsPgDescarga
        {
            internal const string Gz = "rocola_catalogo.tsv.gz", Txt = "rocola_catalogo.txt", Mirado = "rocola_catalogo_mirado.txt";
            internal const string UrlDeFabrica = "https://github.com/Chomp-wq/Rocola/releases/download/catalogo";
            private const long TopeBytes = 256L * 1024 * 1024;   // un catalogo de mas, no se baja (el de 234 000 discos son unos 10 MB)
            private static RocolaOsPgCatalogo actual;
            private static Thread hilo;
            private static string estado = "sin catalogo";
            internal static long Mirados, Bajados, Fallidos;
            internal static RocolaOsPgCatalogo Actual { get { return Volatile.Read(ref actual); } }
            internal static string Estado { get { return Volatile.Read(ref estado); } }

            // LA CARPETA: junto a lo grande de la IA (ROCOLA_IA_DATOS/../catalogo: en la rocola, /rocola/disco/rocola-os/catalogo); sin la variable,
            // en la de lo grande de XP (DatosGrandes)
            private static string carpeta;
            internal static string Carpeta
            {
                get
                {
                    string c = Volatile.Read(ref carpeta);
                    if (c != null) return c;
                    string d = null;
                    try { d = Environment.GetEnvironmentVariable("ROCOLA_IA_DATOS"); } catch { }
                    if (!string.IsNullOrEmpty(d) && d.Trim().Length > 0)
                    {
                        string padre = Path.GetDirectoryName(Path.GetFullPath(d.Trim()).TrimEnd('/'));
                        if (!string.IsNullOrEmpty(padre)) c = Path.Combine(padre, "catalogo");
                    }
                    if (c == null) c = RocolaOs.Grande("catalogo");
                    Volatile.Write(ref carpeta, c);
                    return c;
                }
            }
            internal static string Url
            {
                get
                {
                    string u = null;
                    try { u = Environment.GetEnvironmentVariable("ROCOLA_CATALOGO_URL"); } catch { }
                    return string.IsNullOrEmpty(u) ? UrlDeFabrica : u.TrimEnd('/');
                }
            }
            private static int EsperaMs
            {
                get
                {
                    string s = null; int v;
                    try { s = Environment.GetEnvironmentVariable("ROCOLA_CATALOGO_ESPERA_S"); } catch { }
                    return !string.IsNullOrEmpty(s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= 0 ? v * 1000 : 45000;
                }
            }

            internal static void Arranca()
            {
                lock (typeof(RocolaOsPgDescarga))
                {
                    if (hilo != null) return;
                    hilo = RocolaOs.Hilo("pared_catalogo", Corre);
                }
            }

            private static void Corre()
            {
                string dir = Carpeta;
                try { Directory.CreateDirectory(dir); }
                catch (Exception ex) { RocolaOs.Apunta("pared_generos", "no se pudo crear la carpeta del catalogo (" + ex.GetType().Name + "): sin catalogo"); Volatile.Write(ref estado, "sin carpeta"); return; }
                // 1) EL QUE YA HAY (sin red)
                string sha = "";
                try
                {
                    string gz = Path.Combine(dir, Gz);
                    if (File.Exists(gz))
                    {
                        byte[] b = File.ReadAllBytes(gz);
                        string esperado, err; long bytes;
                        LeeTxt(Path.Combine(dir, Txt), out esperado, out bytes);
                        string tiene = Sha(b);
                        if (esperado != null && !string.Equals(esperado, tiene, StringComparison.OrdinalIgnoreCase))
                            RocolaOs.Apunta("pared_generos", "el catalogo guardado no es el que dice su " + Txt + ": no se usa");
                        else
                        {
                            RocolaOsPgCatalogo c = Abre(b, tiene, out err);
                            if (c != null) { Publica(c, "guardado"); sha = tiene; }
                            else RocolaOs.Apunta("pared_generos", "el catalogo guardado no se pudo leer (" + err + ")");
                        }
                    }
                }
                catch (Exception ex) { RocolaOs.Falla("pared_generos", "al leer el catalogo guardado", ex); }
                if (sha.Length == 0) Volatile.Write(ref estado, "sin catalogo");
                // 2) EL DE LA RED: una vez al dia como mucho; sin red, a la hora
                Thread.Sleep(EsperaMs);
                while (true)
                {
                    long ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), mirado = LeeMirado(Path.Combine(dir, Mirado));
                    long falta = mirado + 86400 - ahora;
                    if (mirado > 0 && falta > 0 && mirado <= ahora) { Thread.Sleep((int)Math.Min(3600, falta) * 1000); continue; }
                    bool bien = false;
                    try { bien = Mira(dir, ref sha); }
                    catch (Exception ex) { RocolaOs.Falla("pared_generos", "al mirar el catalogo de la red", ex); }
                    Interlocked.Increment(ref Mirados);
                    if (bien) { try { EscribeAtomico(Path.Combine(dir, Mirado), Encoding.ASCII.GetBytes(ahora.ToString(CultureInfo.InvariantCulture) + "\n")); } catch { } }
                    else Interlocked.Increment(ref Fallidos);
                    Thread.Sleep(bien ? 3600 * 1000 : 3600 * 1000);
                }
            }

            // MIRAR: el .txt de la version (unos bytes); si su sha256 es otro, el .tsv.gz, comprobado, y se cambia de una pieza
            private static bool Mira(string dir, ref string sha)
            {
                string esperado; long bytes;
                byte[] txt = Baja(Url + "/" + Txt, 64 * 1024);
                if (txt == null) return false;
                LeeTexto(Encoding.UTF8.GetString(txt), out esperado, out bytes);
                if (esperado == null) { RocolaOs.Apunta("pared_generos", "el " + Txt + " de la red no dice su sha256"); return false; }
                if (string.Equals(esperado, sha, StringComparison.OrdinalIgnoreCase)) { Volatile.Write(ref estado, "al dia"); return true; }
                byte[] gz = Baja(Url + "/" + Gz, bytes > 0 && bytes < TopeBytes ? bytes + 1 : TopeBytes);
                if (gz == null) return false;
                if (bytes > 0 && gz.Length != bytes) { RocolaOs.Apunta("pared_generos", "el catalogo bajado no mide lo que dice (" + gz.Length + " de " + bytes + " bytes): no se usa"); return false; }
                string tiene = Sha(gz);
                if (!string.Equals(tiene, esperado, StringComparison.OrdinalIgnoreCase)) { RocolaOs.Apunta("pared_generos", "el catalogo bajado no es el que dice su sha256: no se usa"); return false; }
                string err;
                RocolaOsPgCatalogo c = Abre(gz, tiene, out err);
                if (c == null) { RocolaOs.Apunta("pared_generos", "el catalogo bajado no se pudo leer (" + err + "): no se usa"); return false; }
                EscribeAtomico(Path.Combine(dir, Gz), gz);
                EscribeAtomico(Path.Combine(dir, Txt), Encoding.ASCII.GetBytes("sha256=" + tiene + "\nbytes=" + gz.Length.ToString(CultureInfo.InvariantCulture) + "\n"));
                Interlocked.Increment(ref Bajados);
                sha = tiene;
                Publica(c, "bajado");
                return true;
            }
            private static void Publica(RocolaOsPgCatalogo c, string de)
            {
                Volatile.Write(ref actual, c);
                Volatile.Write(ref estado, de);
                List<char> l = new List<char>(c.Bloques.Keys); l.Sort();
                RocolaOs.Apunta("pared_generos", "catalogo " + de + " (" + c.Fecha + "): " + c.Artistas.ToString(CultureInfo.InvariantCulture) + " artistas, "
                    + c.Discos.ToString(CultureInfo.InvariantCulture) + " discos, letras " + new string(l.ToArray()) + ", " + (c.Datos.Length / 1024).ToString(CultureInfo.InvariantCulture) + " KB en memoria");
            }
            private static RocolaOsPgCatalogo Abre(byte[] gz, string sha, out string error)
            {
                byte[] datos;
                try
                {
                    using (MemoryStream m = new MemoryStream(gz))
                    using (GZipStream z = new GZipStream(m, CompressionMode.Decompress))
                    using (MemoryStream o = new MemoryStream(gz.Length * 4))
                    {
                        byte[] buf = new byte[65536]; int n;
                        while ((n = z.Read(buf, 0, buf.Length)) > 0) { o.Write(buf, 0, n); if (o.Length > TopeBytes) { error = "de mas de 256 MB"; return null; } }
                        datos = o.ToArray();
                    }
                }
                catch (Exception ex) { error = "gzip: " + ex.GetType().Name; return null; }
                RocolaOsPgCatalogo c = RocolaOsPgIndexa(datos, out error);
                if (c != null) c.Sha = sha;
                return c;
            }
            private static HttpClient http;
            internal static HttpClient Http
            {
                get
                {
                    HttpClient h = Volatile.Read(ref http);
                    if (h != null) return h;
                    h = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 8, ConnectTimeout = TimeSpan.FromSeconds(15), MaxConnectionsPerServer = RocolaOsPgPortadas.Hilos }) { Timeout = TimeSpan.FromMinutes(5) };
                    h.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "rocola-xp");
                    HttpClient otro = Interlocked.CompareExchange(ref http, h, null);
                    if (otro != null) { h.Dispose(); return otro; }
                    return h;
                }
            }
            // UN ARCHIVO de la red, con tope; null si no llega (se apunta, como mucho una vez cada 10 min)
            internal static byte[] Baja(string url, long tope)
            {
                try
                {
                    using (HttpResponseMessage r = Http.Send(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (r.StatusCode != HttpStatusCode.OK) { RocolaOsSpotifyCatalogoApi.ApuntaCallado("el catalogo no se bajo (" + ((int)r.StatusCode).ToString(CultureInfo.InvariantCulture) + ")"); return null; }
                        using (Stream s = r.Content.ReadAsStream())
                        using (MemoryStream m = new MemoryStream())
                        {
                            byte[] buf = new byte[65536]; int n;
                            while ((n = s.Read(buf, 0, buf.Length)) > 0) { m.Write(buf, 0, n); if (m.Length > tope) { RocolaOs.Apunta("pared_generos", "el catalogo de la red mide de mas: no se baja"); return null; } }
                            return m.ToArray();
                        }
                    }
                }
                catch (Exception ex) { RocolaOsSpotifyCatalogoApi.ApuntaCallado("el catalogo no se bajo (" + ex.GetType().Name + ")"); return null; }
            }
            internal static string Sha(byte[] b) { return Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant(); }
            private static void LeeTxt(string ruta, out string sha, out long bytes)
            {
                sha = null; bytes = 0;
                try { if (File.Exists(ruta)) LeeTexto(File.ReadAllText(ruta), out sha, out bytes); } catch { }
            }
            // «sha256=HEX», «bytes=N» (o «HEX  rocola_catalogo.tsv.gz», la salida de sha256sum)
            internal static void LeeTexto(string t, out string sha, out long bytes)
            {
                sha = null; bytes = 0;
                foreach (string r in t.Split('\n'))
                {
                    string l = r.Trim();
                    string bajo = l.ToLowerInvariant();
                    if (bajo.StartsWith("bytes", StringComparison.Ordinal))
                    {
                        string v = l.Substring(5).TrimStart('=', ':', ' ', '\t');
                        long n; if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) bytes = n;
                        continue;
                    }
                    foreach (string p in l.Split(new[] { ' ', '\t', '=', ':' }, StringSplitOptions.RemoveEmptyEntries))
                        if (sha == null && p.Length == 64 && EsHex(p)) sha = p.ToLowerInvariant();
                }
            }
            private static bool EsHex(string s) { foreach (char c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false; return true; }
            private static long LeeMirado(string ruta)
            {
                try { long v; if (File.Exists(ruta) && long.TryParse(File.ReadAllText(ruta).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v; } catch { }
                return 0;
            }
            internal static void EscribeAtomico(string ruta, byte[] b)
            {
                string t = ruta + ".t";
                using (FileStream f = new FileStream(t, FileMode.Create, FileAccess.Write, FileShare.None)) { f.Write(b, 0, b.Length); f.Flush(true); }
                File.Move(t, ruta, true);
            }
        }

        // ================================================================= las portadas del catalogo ======================================
        // Del Cover Art Archive (la de 250 px del disco de MusicBrainz), AL VERSE: primero las de la pagina que se mira y (rapido-spotify) despues,
        // por adelantado, las de las de al lado y la que sigue hacia donde se va (lo de otra pagina se deja sin pedir), 12 a la vez (antes 4: una
        // pagina de 18 tardaba 5 vueltas de la red; el Cover Art Archive hace dos redirecciones por portada), a su cache en el disco de la rocola
        // con tope (300 MB de fabrica; las que no se usaron en la sesion y son mas viejas se borran primero). Sin portada (404), la casilla de XP
        // sin caratula. Nada de esto es de Spotify ni gasta su cuota.
        internal static class RocolaOsPgPortadas
        {
            private sealed class Pedido { internal string Mbid; internal char Letra; internal int Pagina; internal bool Pack; }
            private static readonly object cerrojo = new object();
            private static readonly List<Pedido> cola = new List<Pedido>();
            private static readonly HashSet<string> enCola = new HashSet<string>(StringComparer.Ordinal);
            private static readonly HashSet<string> enDisco = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private static readonly HashSet<string> sinPortada = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private static readonly HashSet<string> usadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private static readonly Dictionary<string, long> fallo = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            private static char visLetra; private static int visPagina = -1; private static long visDesde;
            private static int visDir = 1;                 // (rapido-spotify) hacia donde se va: +1 (ARRIBA, la siguiente) o -1
            private static char visVecinaDespues, visVecinaAntes;   // (rapido-spotify) las letras de al lado (su primera pagina, al final)
            internal const int Hilos = 12;                 // (rapido-spotify; antes 4) esperan a la red, con nice 19
            // (27-11-2026) LO QUE YA ESTA A LA MANO (RocolaOs.ParedPortadas.cs): una portada del paquete solo hay que sacarla de el (un archivo
            // chico del disco): su pagina espera 40 ms (que no sea un tiron al hojear), no los 300 de las que van a la red
            internal const int AsientaPaqueteMs = 40;
            internal static long ManoSigValor;
            // (27-11-2026) ¿se cambio de pagina hace menos de 3 s? (el relleno espera: hojear no compite con la red de fondo)
            internal static bool Hojeando { get { lock (cerrojo) return Environment.TickCount64 - visDesde < 3000; } }
            // lo que cambia cuando hay mas portadas a la mano (llego una parte del paquete, el relleno bajo otras): la pared rearma sus letras
            internal static long ManoSig { get { return Interlocked.Read(ref ManoSigValor); } }
            // ¿LA PORTADA ESTA A LA MANO? En la cache del disco o en el paquete. Solo con esto un disco del catalogo entra en la pared.
            internal static bool ALaMano(string mbid)
            {
                if (string.IsNullOrEmpty(mbid)) return false;
                lock (cerrojo) { if (enDisco.Contains(mbid)) return true; }
                return RocolaOsPgPaquete.Tiene(mbid);
            }
            // AL VERSE: una pagina se pide cuando lleva 300 ms a la vista (como el asentamiento de las portadas de XP); al hojear deprisa
            // (mantener ARRIBA: unas 15-30 paginas por segundo) no se pide ninguna de las que pasan
            internal const int AsientaMs = 300;
            // (rapido-spotify, revision) EL ADELANTO (las paginas de al lado y la primera de las letras de al lado) solo con la pagina QUIETA de
            // verdad: la pantalla la dio por quieta (Quieta: QuietaCuadros cuadros pintados seguidos y QuietaMs sin cambiar de pagina). Al hojear,
            // un tiron de la pantalla (armar la letra siguiente al pasar de la ultima pagina, o la maquina cargada) deja la pagina mas de 300 ms
            // sin cambiar sin que nadie la mire: la de la pagina se pide como antes (como XP), pero el adelanto (4 paginas mas) no, porque en un
            // tiron no se pinta ningun cuadro. Lo de las de al lado espera en la cola (no se deja) hasta que la pagina este quieta o cambie.
            internal const int QuietaMs = 600, QuietaCuadros = 6;
            private static char quietaLetra; private static int quietaPagina = -1;
            internal static void Quieta(char letra, int pagina)
            {
                lock (cerrojo)
                {
                    if (letra == quietaLetra && pagina == quietaPagina) return;
                    quietaLetra = letra; quietaPagina = pagina;
                    Monitor.PulseAll(cerrojo);
                }
            }
            private static bool arrancado;
            private static string carpeta, carpetaBarra;
            private static long bytesEnDisco, desdeQueSeMidio;
            internal static long Bajadas, NoTienen, Fallidas, Dejadas, Borradas, Pedidas;
            internal static long BytesEnDisco { get { return Interlocked.Read(ref bytesEnDisco); } }
            internal static string Carpeta { get { return Volatile.Read(ref carpeta); } }
            // con «/» al final (g43 y g50 comparan rutas): null hasta arrancar
            internal static string CarpetaConBarra { get { return Volatile.Read(ref carpetaBarra); } }
            internal static long TopeBytes
            {
                get
                {
                    string s = null; long v;
                    try { s = Environment.GetEnvironmentVariable("ROCOLA_PARED_PORTADAS_MB"); } catch { }
                    return (!string.IsNullOrEmpty(s) && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v > 0 ? v : 300) * 1024 * 1024;
                }
            }
            internal static string Base
            {
                get
                {
                    string u = null;
                    try { u = Environment.GetEnvironmentVariable("ROCOLA_PORTADAS_CATALOGO"); } catch { }
                    return string.IsNullOrEmpty(u) ? "https://coverartarchive.org" : u.TrimEnd('/');
                }
            }

            internal static void Arranca(string dir)
            {
                lock (cerrojo)
                {
                    if (arrancado) return;
                    arrancado = true;
                    carpeta = Path.Combine(dir, "portadas");
                    Volatile.Write(ref carpetaBarra, carpeta.TrimEnd('/') + "/");
                }
                RocolaOs.Hilo("pared_portadas_indice", Indexa);
                RocolaOsPgPaquete.Arranca(dir);
                for (int i = 0; i < Hilos; i++) RocolaOs.Hilo("pared_portadas_" + i.ToString(CultureInfo.InvariantCulture), Corre);
            }
            // LO QUE YA HAY EN EL DISCO (en su hilo, al arrancar): sus nombres y cuanto ocupan, con las miniaturas
            private static void Indexa()
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(carpeta, "miniaturas"));
                    long total = 0;
                    List<string> nombres = new List<string>();
                    foreach (FileInfo f in new DirectoryInfo(carpeta).EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        if (f.Name.EndsWith(".t", StringComparison.Ordinal)) { try { f.Delete(); } catch { } continue; }
                        total += f.Length;
                        if (f.DirectoryName == carpeta && f.Name.EndsWith(".jpg", StringComparison.Ordinal)) nombres.Add(Path.GetFileNameWithoutExtension(f.Name));
                    }
                    lock (cerrojo) { foreach (string n in nombres) enDisco.Add(n); }
                    Interlocked.Exchange(ref bytesEnDisco, total);
                    if (nombres.Count > 0) Interlocked.Increment(ref ManoSigValor);   // (27-11-2026) las de la cache ya cuentan como a la mano: la pared se rearma
                    if (total > TopeBytes) Recorta();
                }
                catch (Exception ex) { RocolaOs.Falla("pared_generos", "al mirar la cache de portadas", ex); }
                // y despues, cada 15 s, lo que ocupa de verdad (con las miniaturas que XP va haciendo de estas portadas: unos 140 KB cada una,
                // mas que la portada) y, si pasa del tope, se recorta
                while (true)
                {
                    Thread.Sleep(15000);
                    Mide();
                    if (BytesEnDisco > TopeBytes) Recorta();
                }
            }
            internal static string Ruta(string mbid) { return Path.Combine(carpeta, mbid + ".jpg"); }
            internal static bool EnDisco(string mbid) { lock (cerrojo) return enDisco.Contains(mbid); }
            internal static void Usada(string mbid) { lock (cerrojo) usadas.Add(mbid); }
            // LA PAGINA QUE SE MIRA (la pantalla, en cada RefreshAlbums): lo pedido de otra se deja (salvo, desde rapido-spotify, lo de las de al
            // lado y la que sigue hacia donde se va, que se baja por adelantado). Devuelve hacia donde se va (+1 o -1).
            internal static int Mira(char letra, int pagina, char vecinaDespues = '\0', char vecinaAntes = '\0')
            {
                lock (cerrojo)
                {
                    visVecinaDespues = vecinaDespues; visVecinaAntes = vecinaAntes;
                    if (letra == visLetra && pagina == visPagina) return visDir;
                    visDir = letra == visLetra && pagina < visPagina ? -1 : 1;
                    visLetra = letra; visPagina = pagina; visDesde = Environment.TickCount64;
                    quietaPagina = -1;   // (revision: al volver a una pagina que ya estuvo quieta, el adelanto vuelve a esperar los 6 cuadros y 600 ms)
                    Monitor.PulseAll(cerrojo);
                    return visDir;
                }
            }
            // (rapido-spotify) EL TURNO de una pagina: 0 la que se mira; 1 la siguiente hacia donde se va, 2 la de atras, 3 la de dos mas alla; 4 y 5 la
            // primera de la letra de despues y la de antes; -1 las demas (ya no se miran: se dejan). Bajo el cerrojo.
            private static int Turno(Pedido q)
            {
                if (q.Letra != visLetra)
                {
                    if (q.Pagina != 0 || q.Letra == '\0') return -1;
                    return q.Letra == visVecinaDespues ? 4 : q.Letra == visVecinaAntes ? 5 : -1;
                }
                int d = (q.Pagina - visPagina) * visDir;
                return d == 0 ? 0 : d == 1 ? 1 : d == -1 ? 2 : d == 2 ? 3 : -1;
            }
            internal static void Pide(string mbid, char letra, int pagina)
            {
                lock (cerrojo)
                {
                    if (carpeta == null || enDisco.Contains(mbid) || sinPortada.Contains(mbid) || enCola.Contains(mbid)) return;
                    long hasta;
                    if (fallo.TryGetValue(mbid, out hasta) && Environment.TickCount64 < hasta) return;
                    cola.Add(new Pedido { Mbid = mbid, Letra = letra, Pagina = pagina, Pack = RocolaOsPgPaquete.Tiene(mbid) });
                    enCola.Add(mbid);
                    Monitor.PulseAll(cerrojo);
                }
            }
            private static void Corre()
            {
                while (true)
                {
                    Pedido p = null;
                    lock (cerrojo)
                    {
                        while (p == null)
                        {
                            bool hayPaquete = false;
                            for (int i = 0; i < cola.Count && !hayPaquete; i++) hayPaquete = cola[i].Pack;
                            long falta = visDesde + (hayPaquete ? AsientaPaqueteMs : AsientaMs) - Environment.TickCount64;
                            if (falta > 0 && cola.Count > 0) { Monitor.Wait(cerrojo, (int)falta + 1); continue; }
                            // la de la pagina que se mira primero y, si no queda ninguna de ella, la de su turno (rapido-spotify: las de al lado, por
                            // adelantado); en cada turno, en el orden en que se pidieron
                            int mejor = -1, mejorTurno = int.MaxValue;
                            bool quieta = quietaLetra == visLetra && quietaPagina == visPagina;   // (el adelanto, solo con la pagina quieta)
                            for (int i = 0; i < cola.Count; i++)
                            {
                                int tu = Turno(cola[i]);
                                if (tu > 0 && !quieta) continue;
                                if (tu >= 0 && tu < mejorTurno) { mejor = i; mejorTurno = tu; if (tu == 0) break; }
                            }
                            if (mejor >= 0) { p = cola[mejor]; cola.RemoveAt(mejor); }
                            // lo de las paginas que ya pasaron (hojear deprisa): se deja sin preguntar a la red
                            for (int i = cola.Count - 1; i >= 0; i--)
                                if (Turno(cola[i]) < 0) { enCola.Remove(cola[i].Mbid); cola.RemoveAt(i); Dejadas++; }
                            if (p == null) Monitor.Wait(cerrojo, 10000);
                        }
                    }
                    Baja(p);
                }
            }
            // (27-11-2026) EL RELLENO (RocolaOs.ParedPortadas.cs): bajar una portada a la cache sin que nadie la espere; true si quedo a la mano
            internal static bool BajaRelleno(string mbid)
            {
                if (carpeta == null || EnDisco(mbid) || RocolaOsPgPaquete.Tiene(mbid)) return false;
                lock (cerrojo) { long hasta; if (sinPortada.Contains(mbid) || (fallo.TryGetValue(mbid, out hasta) && Environment.TickCount64 < hasta)) return false; }
                if (BytesEnDisco > TopeBytes * 9 / 10) return false;   // (la cache ya esta casi llena: no se rellena para que Recorta la vacie)
                Baja(new Pedido { Mbid = mbid });
                return EnDisco(mbid);
            }
            private static void Baja(Pedido p)
            {
                string ruta = Ruta(p.Mbid);
                bool bien = false;
                try
                {
                    long delPaquete;
                    if (File.Exists(ruta)) bien = true;
                    else if ((delPaquete = RocolaOsPgPaquete.Extrae(p.Mbid, ruta)) > 0) { Interlocked.Add(ref bytesEnDisco, delPaquete); bien = true; }   // (27-11-2026) sacada del paquete: sin red
                    else
                    {
                        Interlocked.Increment(ref Pedidas);
                        // (rapido-spotify) una portada que se atasca suelta su hilo a los 20 s (el cliente es el del catalogo, con 5 min para el archivo
                        // grande): con 12 a la vez, una red que se cuelga no deja a la pagina sin hilos
                        using (CancellationTokenSource cts = new CancellationTokenSource(20000))
                        using (HttpResponseMessage r = RocolaOsPgDescarga.Http.Send(new HttpRequestMessage(HttpMethod.Get, Base + "/release-group/" + p.Mbid + "/front-250"), HttpCompletionOption.ResponseHeadersRead, cts.Token))
                        using (cts.Token.Register(delegate { try { r.Dispose(); } catch { } }))
                        {
                            int cod = (int)r.StatusCode;
                            if (cod == 200)
                            {
                                byte[] b;
                                using (Stream s = r.Content.ReadAsStream())
                                using (MemoryStream m = new MemoryStream())
                                {
                                    byte[] buf = new byte[16384]; int n;
                                    while ((n = s.Read(buf, 0, buf.Length)) > 0) { m.Write(buf, 0, n); if (m.Length > 2 * 1024 * 1024) throw new InvalidDataException("portada de mas de 2 MB"); }
                                    b = m.ToArray();
                                }
                                RocolaOsPgDescarga.EscribeAtomico(ruta, b);
                                Interlocked.Add(ref bytesEnDisco, b.Length);
                                Interlocked.Increment(ref Bajadas);
                                bien = true;
                            }
                            else if (cod == 404) { lock (cerrojo) sinPortada.Add(p.Mbid); Interlocked.Increment(ref NoTienen); }
                            else
                            {
                                lock (cerrojo) fallo[p.Mbid] = Environment.TickCount64 + 60000;
                                Interlocked.Increment(ref Fallidas);
                                RocolaOsSpotifyCatalogoApi.ApuntaCallado("una portada del catalogo no se bajo (" + cod.ToString(CultureInfo.InvariantCulture) + ")");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (cerrojo) { if (ex is InvalidDataException) sinPortada.Add(p.Mbid); else fallo[p.Mbid] = Environment.TickCount64 + 60000; }
                    Interlocked.Increment(ref Fallidas);
                    RocolaOsSpotifyCatalogoApi.ApuntaCallado("una portada del catalogo no se bajo (" + ex.GetType().Name + ")");
                }
                lock (cerrojo) { enCola.Remove(p.Mbid); if (bien) { enDisco.Add(p.Mbid); usadas.Add(p.Mbid); } }
                if (bien)
                {
                    string mbid = p.Mbid;
                    RocolaOs.Encarga("pared_generos:portada", delegate { Form1 f = RocolaOs.LaRocola; if (f != null) f.RocolaOsPgLlegoPortada(mbid); });
                    // el tope: con las miniaturas de XP (g43 las trae aqui), se vuelve a medir cada 64 bajadas
                    if (Interlocked.Increment(ref desdeQueSeMidio) % 64 == 0) Mide();
                    if (BytesEnDisco > TopeBytes) Recorta();
                }
            }
            private static void Mide()
            {
                try { long t = 0; foreach (FileInfo f in new DirectoryInfo(carpeta).EnumerateFiles("*", SearchOption.AllDirectories)) t += f.Length; Interlocked.Exchange(ref bytesEnDisco, t); } catch { }
            }
            // EL TOPE: se borran primero las que no se usaron en esta sesion, de la mas vieja a la mas nueva, hasta quedar en el 85 %
            private static readonly object cerrojoRecorta = new object();
            private static void Recorta()
            {
                lock (cerrojoRecorta)
                {
                    try
                    {
                        List<FileInfo> l = new List<FileInfo>(new DirectoryInfo(carpeta).EnumerateFiles("*", SearchOption.AllDirectories));
                        long total = 0; foreach (FileInfo f in l) total += f.Length;
                        long meta = TopeBytes * 85 / 100;
                        if (total <= TopeBytes) { Interlocked.Exchange(ref bytesEnDisco, total); return; }
                        HashSet<string> u; lock (cerrojo) u = new HashSet<string>(usadas, StringComparer.OrdinalIgnoreCase);
                        l.Sort(delegate (FileInfo a, FileInfo b)
                        {
                            bool ua = u.Contains(Path.GetFileNameWithoutExtension(a.Name)), ub = u.Contains(Path.GetFileNameWithoutExtension(b.Name));
                            if (ua != ub) return ua ? 1 : -1;
                            return a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc);
                        });
                        foreach (FileInfo f in l)
                        {
                            if (total <= meta) break;
                            try
                            {
                                long n = f.Length; f.Delete(); total -= n; Interlocked.Increment(ref Borradas);
                                if (f.DirectoryName == carpeta) lock (cerrojo) enDisco.Remove(Path.GetFileNameWithoutExtension(f.Name));
                            }
                            catch { }
                        }
                        Interlocked.Exchange(ref bytesEnDisco, total);
                    }
                    catch (Exception ex) { RocolaOs.Falla("pared_generos", "al recortar la cache de portadas", ex); }
                }
            }
        }
    }
}

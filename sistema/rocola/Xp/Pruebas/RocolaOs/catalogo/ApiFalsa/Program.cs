// LA API WEB DE SPOTIFY FALSA (fase 11, grupo catalogo): contesta como la de verdad de hoy (14-11-2026, modo desarrollo con los cambios de
// febrero de 2026), con datos INVENTADOS (artistas, discos y canciones de mentira; portadas dibujadas aqui), en 127.0.0.1. Solo para
// Pruebas/RocolaOs/catalogo/prueba.sh: rocola-xp la usa con ROCOLA_SPOTIFY_API=http://127.0.0.1:P/v1 y ROCOLA_SPOTIFY_CUENTAS=http://127.0.0.1:P.
//   api-falsa PUERTO CARPETA [client_id client_secret refresh_token]
// Escribe CARPETA/peticiones.txt (una linea por peticion: «metodo ruta-sin-consulta llave codigo») y CARPETA/listo cuando escucha.
// Lo que sabe hacer:
//   POST /api/token                          client_credentials (Basic con el id y el secreto) o refresh_token (da uno nuevo: rota; como
//                                            PKCE, sin Authorization y con client_id, o como Authorization Code, con el Basic)
//   GET  /v1/search?q&type&limit&market      limit de 1 a 10 (mas: 400, como la de verdad); busca en nombres y artistas
//   GET  /v1/albums/{id}, /v1/albums/{id}/tracks, /v1/artists/{id}/albums (limit hasta 10)
//   GET  /v1/me/tracks, /v1/me/albums, /v1/me/playlists, /v1/playlists/{id}/items     (solo con la llave de la cuenta: si no, 401)
//   GET  /v1/me/top/tracks, /v1/me/player/recently-played                             (los atajos de SP9; solo con la cuenta)
//   (grupo paredes-buscador) search con offset y con «artist:"NOMBRE"» (las canciones de ese artista); los artistas de una busqueda: los
//   que contienen lo escrito o estan a dos letras, y detras uno que no tiene que ver (como hace Spotify); un artista con 40 discos (Banda
//   Interminable: 3 paginas de pared y 120 canciones) y «Otra Banda» (un artista de los datos de prueba de XP: su carpeta no lleva
//   « - » y XP la toma por un disco sin nombre de «Otra Banda») con un disco del mismo nombre (el que la rocola ya tiene) y dos mas.
//   El registro apunta « q=..» en search y, si la peticion trae offset, « t=tipo off=N» detras.
//   GET  /img/{nombre}.jpg                   una portada dibujada (JPEG de 300x300)
//   GET  /control/429?seg=N[&cuota=1][&fecha=1]  la siguiente peticion a /v1 contesta 429 con Retry-After N (o, con fecha=1, la fecha de
//                                            dentro de N s, RFC 9110) y con cuota=1 el cuerpo QUOTA_EXCEEDED
//   GET  /control/vence                      las llaves dadas dejan de valer (la siguiente a /v1 da 401)
//   GET  /control/5xx?n=N                    las siguientes N peticiones a /v1 dan 503
//   (revision cuota-hilos-y-fallos) GET /control/lento?ms=N   cada peticion a /v1 tarda N ms mas (la red lenta del bar)
//   (revision cuota-hilos-y-fallos) GET /control/muchos       Banda Interminable pasa a tener 300 discos (los 40 y 260 mas, de 1 cancion):
//                                            su pared son 17 paginas, para hojear deprisa
//   (revision cuota-hilos-y-fallos, 2a pasada) GET /control/miente?total=N   en search, los discos dicen «total» N aunque haya menos (como
//                                            Spotify, cuyo total de search es una estimacion)
//   (revision cuota-hilos-y-fallos, 2a pasada) GET /control/imgfalla?n=N     las siguientes N portadas (/img/) dan 503
//   (pared-generos) GET /catalogo/ARCHIVO           el catalogo de la rocola (la version «catalogo» de GitHub): los archivos de la carpeta
//                                            de la variable API_CATALOGO (sin ella o sin el archivo, 404)
//   (pared-generos) GET /release-group/MBID/front-250   el Cover Art Archive: una portada dibujada; los MBID que acaban en 0, 404 (sin portada)
//   (pared-generos) GET /control/caalento?ms=N   cada portada del Cover Art Archive tarda N ms mas
//   (pared-generos) GET /v1/albums/catID         un disco del catalogo (4 canciones inventadas); los «catno...», 404 (Spotify no lo tiene)
//   (pared-generos) search con q=album:"NOMBRE" artist:"ARTISTA" (type=album): un disco con ese nombre (id «catb...»); si el nombre dice
//                                            «Sin Spotify», ninguno
//   (rapido-spotify, para MEDIR) GET /control/imglento?ms=N    cada portada de Spotify (/img/) tarda N ms mas
//   (rapido-spotify) GET /control/llavelento?ms=N               cada POST /api/token tarda N ms mas
//   (rapido-spotify) GET /control/conexion?ms=N                 la PRIMERA peticion de cada conexion nueva (otro puerto de origen) tarda
//                                            N ms mas: lo que cuesta abrirla de verdad (TCP y TLS: 2 o 3 viajes) y que aqui no se ve
//   (rapido-spotify) CARPETA/tiempos.txt: una linea por peticion «llego_ms contesto_ms metodo ruta codigo [nueva]» (ms de Unix), aparte
//                                            de peticiones.txt (cuyo formato leen las pruebas)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using SkiaSharp;

internal static class ApiFalsa
{
    sealed class Artista { public string Id, Nombre; }
    sealed class Cancion { public string Id, Nombre, Isrc; public Artista Art; public Disco Dis; public int Numero, Ms; }
    sealed class Disco { public string Id, Nombre, Tipo = "album"; public Artista Art; public List<Cancion> Canciones = new List<Cancion>(); }
    sealed class Lista { public string Id, Nombre, Dueno; public List<Cancion> Canciones = new List<Cancion>(); }

    static readonly List<Artista> artistas = new List<Artista>();
    static readonly List<Disco> discos = new List<Disco>();
    static readonly List<Cancion> canciones = new List<Cancion>();
    static readonly List<Lista> listas = new List<Lista>();
    // (la sustitucion invisible, Pruebas/RocolaOs/sustitucion: canciones aparte, que solo salen buscando «isrc:» o «track:"..." artist:"..."»;
    // sus ids son los del Soloist falso: su tono, su duracion y su envolvente)
    static readonly List<Cancion> sustitucion = new List<Cancion>();
    static string baseUrl, carpeta, clientId = "id-de-prueba", secreto = "secreto-de-prueba", refresco = "refresco-1";
    static readonly HashSet<string> llavesApp = new HashSet<string>(), llavesCuenta = new HashSet<string>();
    static int n429, seg429 = 30, n5xx, nLlave; static bool cuota429, fecha429;
    static int lento, miente, nImg, caaLento; static bool muchos; static readonly List<Disco> extraInter = new List<Disco>();   // (revision cuota-hilos-y-fallos; caaLento: pared-generos)
    static readonly Dictionary<string, Disco> delCatalogo = new Dictionary<string, Disco>();   // (pared-generos) los discos del catalogo inventados
    static readonly object cerrojo = new object();
    // (rapido-spotify) la latencia de las portadas de Spotify, de las llaves y de abrir una conexion; y cuando llego cada peticion
    static int imgLento, llaveLento, conexionMs;
    static readonly HashSet<string> conexiones = new HashSet<string>(StringComparer.Ordinal);
    [ThreadStatic] static long llegoMs;
    [ThreadStatic] static bool nuevaConexion;

    static int Main(string[] a)
    {
        ThreadPool.SetMinThreads(64, 64);   // (rapido-spotify: muchas a la vez que esperan su latencia, sin que el ThreadPool las ponga en fila)
        int puerto = int.Parse(a[0], CultureInfo.InvariantCulture);
        carpeta = a[1];
        if (a.Length > 4) { clientId = a[2]; secreto = a[3]; refresco = a[4]; }
        baseUrl = "http://127.0.0.1:" + puerto.ToString(CultureInfo.InvariantCulture);
        Inventa();
        HttpListener l = new HttpListener();
        l.Prefixes.Add(baseUrl + "/");
        l.Start();
        File.WriteAllText(Path.Combine(carpeta, "listo"), "si\n");
        while (true)
        {
            HttpListenerContext c = l.GetContext();
            ThreadPool.QueueUserWorkItem(delegate { try { Atiende(c); } catch (Exception ex) { Console.Error.WriteLine(ex); try { c.Response.Abort(); } catch { } } });
        }
    }

    // ---------- lo inventado ----------
    static void Inventa()
    {
        string[] nombresArt = { "Los Inventados", "La Banda de Prueba", "Grupo Ficticio", "Trio Imaginario" };
        string[][] nombresDis = {
            new[] { "Tema de Mentira", "Canciones Inventadas", "En Vivo Desde Ningun Lado" },
            new[] { "Prueba Uno", "Prueba Dos" },
            new[] { "Ficcion Total" },
            new[] { "Tres Tiempos Imaginarios", "Tema Sencillo" },
        };
        int nd = 0, nc = 0;
        for (int i = 0; i < nombresArt.Length; i++)
        {
            Artista ar = new Artista { Id = "art" + i.ToString("00", CultureInfo.InvariantCulture), Nombre = nombresArt[i] };
            artistas.Add(ar);
            foreach (string nd0 in nombresDis[i])
            {
                Disco d = new Disco { Id = "dis" + (nd++).ToString("000", CultureInfo.InvariantCulture), Nombre = nd0, Art = ar };
                if (nd0 == "Tema Sencillo") d.Tipo = "single";
                int cuantas = d.Tipo == "single" ? 1 : 5 + (nd * 3) % 8;
                for (int k = 1; k <= cuantas; k++)
                {
                    string nombre = k == 1 ? "Tema " + nd0 : (k % 3 == 0 ? "Cancion Inventada " : "Otra Tonada ") + k.ToString(CultureInfo.InvariantCulture);
                    Cancion c = new Cancion { Id = "can" + (nc++).ToString("0000", CultureInfo.InvariantCulture), Nombre = nombre, Art = ar, Dis = d, Numero = k, Ms = 150000 + k * 7311 };
                    d.Canciones.Add(c); canciones.Add(c);
                }
                discos.Add(d);
            }
        }
        // un disco de 60 canciones (pide una segunda pagina)
        Artista largo = new Artista { Id = "art99", Nombre = "Orquesta Interminable" };
        artistas.Add(largo);
        Disco dl = new Disco { Id = "dis999", Nombre = "Sesenta Temas Inventados", Art = largo };
        for (int k = 1; k <= 60; k++) { Cancion c = new Cancion { Id = "can9" + k.ToString("000", CultureInfo.InvariantCulture), Nombre = "Pieza Larga " + k.ToString(CultureInfo.InvariantCulture), Art = largo, Dis = dl, Numero = k, Ms = 120000 }; dl.Canciones.Add(c); canciones.Add(c); }
        discos.Add(dl);
        // (paredes) un artista con 40 discos de 3 canciones, y «Otra Banda» (en los datos de XP, un disco sin nombre de «Otra Banda»)
        Artista inter = new Artista { Id = "art50", Nombre = "Banda Interminable" };
        artistas.Add(inter);
        for (int i = 1; i <= 40; i++)
        {
            Disco d = new Disco { Id = "dis5" + i.ToString("00", CultureInfo.InvariantCulture), Nombre = "Disco Interminable " + i.ToString("00", CultureInfo.InvariantCulture), Art = inter };
            for (int k = 1; k <= 3; k++) { Cancion c = new Cancion { Id = "can5" + i.ToString("00", CultureInfo.InvariantCulture) + k.ToString(CultureInfo.InvariantCulture), Nombre = "Pieza Sin Fin " + ((i - 1) * 3 + k).ToString(CultureInfo.InvariantCulture), Art = inter, Dis = d, Numero = k, Ms = 140000 }; d.Canciones.Add(c); canciones.Add(c); }
            discos.Add(d);
        }
        // (revision cuota-hilos-y-fallos) 260 discos mas de Banda, solo con /control/muchos (ni en las busquedas ni en las demas escenas)
        for (int i = 41; i <= 300; i++)
        {
            Disco d = new Disco { Id = "dis7" + i.ToString("000", CultureInfo.InvariantCulture), Nombre = "Disco Sin Fondo " + i.ToString("000", CultureInfo.InvariantCulture), Art = inter };
            d.Canciones.Add(new Cancion { Id = "can7" + i.ToString("000", CultureInfo.InvariantCulture), Nombre = "Pieza Del Fondo " + i.ToString(CultureInfo.InvariantCulture), Art = inter, Dis = d, Numero = 1, Ms = 140000 });
            extraInter.Add(d);
        }
        Artista otra = new Artista { Id = "art60", Nombre = "Otra Banda" };
        artistas.Add(otra);
        foreach (string n0 in new[] { "Otra Banda", "Segundo Disco", "Tercer Disco" })
        {
            Disco d = new Disco { Id = "dis6" + discos.Count.ToString("00", CultureInfo.InvariantCulture), Nombre = n0, Art = otra };
            for (int k = 1; k <= 3; k++) { Cancion c = new Cancion { Id = "can6" + discos.Count.ToString("00", CultureInfo.InvariantCulture) + k.ToString(CultureInfo.InvariantCulture), Nombre = "Cancion Lejana " + k.ToString(CultureInfo.InvariantCulture), Art = otra, Dis = d, Numero = k, Ms = 150000 }; d.Canciones.Add(c); canciones.Add(c); }
            discos.Add(d);
        }
        Artista tc = new Artista { Id = "art80", Nombre = "Trio Calidad" }, tca = new Artista { Id = "art81", Nombre = "Trio Calidad Azul" };
        Disco joyas = new Disco { Id = "dis080", Nombre = "Joyas de Prueba", Art = tc }, exitos = new Disco { Id = "dis081", Nombre = "Exitos de Todos", Art = tc, Tipo = "compilation" },
              vivo = new Disco { Id = "dis082", Nombre = "Joyas en Vivo", Art = tc };
        Action<string, string, Artista, Disco, string> s = (id, nom, ar, di, isrc) => sustitucion.Add(new Cancion { Id = id, Nombre = nom, Art = ar, Dis = di, Numero = sustitucion.Count + 1, Ms = 32000, Isrc = isrc });
        s("f440d032m1bo", "Bolero de Prueba", tc, joyas, "MXA112600001");
        s("f560d032m1vr", "Vals Sin Codigo", tc, exitos, "MXA112699902");     // (el recopilatorio)
        s("f570d032m1vv", "Vals Sin Codigo - En Vivo", tc, vivo, "MXA112699903");   // (otra version)
        s("f580d032m1vz", "Vals Sin Codigo", tca, joyas, "MXA112699904");     // (otro artista)
        s("f550d032m1va", "Vals Sin Código", tc, joyas, "MXA112600002");      // (la buena: su disco; con acento)
        s("f660d032m2cu", "Cumbia Otra Forma", tc, joyas, "MXA112600003");    // (la forma de la envolvente 2: otra grabacion)
        s("nxsonnollega", "Son Que No Llega", tc, joyas, "MXA112600005");     // (nunca empieza)
        s("f770d032m1co", "Cumbia Cortada", tc, joyas, "MXA112600006");
        foreach (Cancion c in sustitucion) c.Dis.Canciones.Add(c);
        Lista propia = new Lista { Id = "lis001", Nombre = "Mi Lista de Prueba", Dueno = "dueno-falso" };
        for (int k = 0; k < 6; k++) propia.Canciones.Add(canciones[k * 3]);
        listas.Add(propia);
        listas.Add(new Lista { Id = "lis002", Nombre = "Lista de Otro", Dueno = "otro-falso" });
    }

    // ---------- atender ----------
    static void Atiende(HttpListenerContext c)
    {
        string ruta = c.Request.Url.AbsolutePath;
        llegoMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); nuevaConexion = false;
        if (!ruta.StartsWith("/control/", StringComparison.Ordinal))
        {
            int cm; bool nueva;
            lock (cerrojo) { cm = conexionMs; nueva = conexiones.Add(c.Request.RemoteEndPoint.ToString()); }
            nuevaConexion = nueva;
            if (nueva && cm > 0) Thread.Sleep(cm);
        }
        string auth = c.Request.Headers["Authorization"] ?? "";
        string tipoLlave = auth.StartsWith("Bearer ", StringComparison.Ordinal) ? (llavesCuenta.Contains(auth.Substring(7)) ? "cuenta" : llavesApp.Contains(auth.Substring(7)) ? "app" : "vencida") : auth.StartsWith("Basic ", StringComparison.Ordinal) ? "basic" : "nada";
        int cod = 200; string cuerpo; string ctype = "application/json";
        byte[] binario = null;
        Dictionary<string, string> extra = new Dictionary<string, string>();
        var q = System.Web.HttpUtility.ParseQueryString(c.Request.Url.Query);
        if (ruta.StartsWith("/control/", StringComparison.Ordinal))
        {
            lock (cerrojo)
            {
                if (ruta == "/control/429") { n429 = 1; seg429 = int.Parse(q["seg"] ?? "30", CultureInfo.InvariantCulture); cuota429 = q["cuota"] == "1"; fecha429 = q["fecha"] == "1"; }
                else if (ruta == "/control/vence") { llavesApp.Clear(); llavesCuenta.Clear(); }
                else if (ruta == "/control/5xx") n5xx = int.Parse(q["n"] ?? "1", CultureInfo.InvariantCulture);
                else if (ruta == "/control/lento") lento = int.Parse(q["ms"] ?? "0", CultureInfo.InvariantCulture);
                else if (ruta == "/control/muchos") muchos = true;
                else if (ruta == "/control/miente") miente = int.Parse(q["total"] ?? "1000", CultureInfo.InvariantCulture);
                else if (ruta == "/control/imgfalla") nImg = int.Parse(q["n"] ?? "1", CultureInfo.InvariantCulture);
                else if (ruta == "/control/caalento") caaLento = int.Parse(q["ms"] ?? "0", CultureInfo.InvariantCulture);
                else if (ruta == "/control/imglento") imgLento = int.Parse(q["ms"] ?? "0", CultureInfo.InvariantCulture);
                else if (ruta == "/control/llavelento") llaveLento = int.Parse(q["ms"] ?? "0", CultureInfo.InvariantCulture);
                else if (ruta == "/control/conexion") conexionMs = int.Parse(q["ms"] ?? "0", CultureInfo.InvariantCulture);
            }
            Contesta(c, 200, "{}", ctype, null, extra); return;
        }
        if (ruta.StartsWith("/img/", StringComparison.Ordinal))
        {
            bool falla; lock (cerrojo) { falla = nImg > 0; if (falla) nImg--; }
            if (falla) { Apunta(c, ruta, "img", 503); Contesta(c, 503, "{}", ctype, null, extra); return; }
        }
        if (ruta.StartsWith("/img/", StringComparison.Ordinal)) { int il; lock (cerrojo) il = imgLento; if (il > 0) Thread.Sleep(il); }
        if (ruta.StartsWith("/img/", StringComparison.Ordinal)) { binario = Portada(Path.GetFileNameWithoutExtension(ruta)); ctype = "image/jpeg"; cuerpo = null; Apunta(c, ruta, "img", 200); Contesta(c, 200, null, ctype, binario, extra); return; }
        if (ruta.StartsWith("/catalogo/", StringComparison.Ordinal))
        {
            string dir = Environment.GetEnvironmentVariable("API_CATALOGO");
            string f = string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, Path.GetFileName(ruta));
            if (f != null && File.Exists(f)) { Apunta(c, ruta, "catalogo", 200); Contesta(c, 200, null, "application/octet-stream", File.ReadAllBytes(f), extra); }
            else { Apunta(c, ruta, "catalogo", 404); Contesta(c, 404, "Not Found", "text/plain", null, extra); }
            return;
        }
        if (ruta.StartsWith("/release-group/", StringComparison.Ordinal))
        {
            string[] r = ruta.Split('/');
            string mbid = r.Length > 2 ? r[2] : "";
            int ms; lock (cerrojo) ms = caaLento;
            if (ms > 0) Thread.Sleep(ms);
            if (mbid.Length == 0 || mbid.EndsWith("0", StringComparison.Ordinal)) { Apunta(c, ruta, "caa", 404); Contesta(c, 404, "Not Found", "text/plain", null, extra); return; }
            Apunta(c, ruta, "caa", 200); Contesta(c, 200, null, "image/jpeg", Portada("caa-" + mbid), extra); return;
        }
        if (ruta == "/api/token" && c.Request.HttpMethod == "POST")
        {
            { int ll; lock (cerrojo) ll = llaveLento; if (ll > 0) Thread.Sleep(ll); }
            string form; using (StreamReader r = new StreamReader(c.Request.InputStream)) form = r.ReadToEnd();
            var f = System.Web.HttpUtility.ParseQueryString(form);
            string esperado = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secreto));
            // (la de la cuenta se renueva como PKCE: sin Authorization y con client_id en el cuerpo; o como Authorization Code, con el secreto)
            bool pkce = auth.Length == 0 && f["grant_type"] == "refresh_token" && f["client_id"] == clientId;
            if (auth != esperado && !pkce) { cod = 400; cuerpo = "{\"error\":\"invalid_client\",\"error_description\":\"Invalid client\"}"; }
            else if (f["grant_type"] == "client_credentials")
            {
                string t; lock (cerrojo) { t = "app-" + (++nLlave).ToString(CultureInfo.InvariantCulture); llavesApp.Add(t); }
                cuerpo = "{\"access_token\":\"" + t + "\",\"token_type\":\"Bearer\",\"expires_in\":3600}";
            }
            else if (f["grant_type"] == "refresh_token" && f["refresh_token"] == refresco)
            {
                string t; lock (cerrojo) { t = "cuenta-" + (++nLlave).ToString(CultureInfo.InvariantCulture); llavesCuenta.Add(t); refresco = "refresco-" + (nLlave + 1).ToString(CultureInfo.InvariantCulture); }
                cuerpo = "{\"access_token\":\"" + t + "\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"refresh_token\":\"" + refresco + "\",\"scope\":\"user-library-read playlist-read-private\"}";
            }
            else { cod = 400; cuerpo = "{\"error\":\"invalid_grant\",\"error_description\":\"Invalid refresh token\"}"; }
            Apunta(c, ruta, tipoLlave, cod); Contesta(c, cod, cuerpo, ctype, null, extra); return;
        }
        if (!ruta.StartsWith("/v1/", StringComparison.Ordinal)) { Apunta(c, ruta, tipoLlave, 404); Contesta(c, 404, "{\"error\":{\"status\":404,\"message\":\"Not found\"}}", ctype, null, extra); return; }
        lock (cerrojo)
        {
            if (n429 > 0)
            {
                n429--;
                extra["Retry-After"] = fecha429 ? DateTime.UtcNow.AddSeconds(seg429).ToString("r", CultureInfo.InvariantCulture) : seg429.ToString(CultureInfo.InvariantCulture);
                Apunta(c, ruta, tipoLlave, 429);
                Contesta(c, 429, cuota429 ? "{\"error\":{\"status\":429,\"message\":\"Too many requests\",\"reason\":\"QUOTA_EXCEEDED\"}}" : "{\"error\":{\"status\":429,\"message\":\"API rate limit exceeded\"}}", ctype, null, extra);
                return;
            }
            if (n5xx > 0) { n5xx--; Apunta(c, ruta, tipoLlave, 503); Contesta(c, 503, "{\"error\":{\"status\":503,\"message\":\"Service unavailable\"}}", ctype, null, extra); return; }
        }
        int espera; lock (cerrojo) espera = lento;
        if (espera > 0) Thread.Sleep(espera);
        if (tipoLlave != "app" && tipoLlave != "cuenta") { Apunta(c, ruta, tipoLlave, 401); Contesta(c, 401, "{\"error\":{\"status\":401,\"message\":\"The access token expired\"}}", ctype, null, extra); return; }
        try { cuerpo = Api(ruta, q, tipoLlave, out cod); }
        catch (Exception ex) { cod = 500; cuerpo = "{\"error\":{\"status\":500,\"message\":\"" + ex.GetType().Name + "\"}}"; }
        Apunta(c, ruta, tipoLlave, cod);
        Contesta(c, cod, cuerpo, ctype, null, extra);
    }

    static string Api(string ruta, System.Collections.Specialized.NameValueCollection q, string llave, out int cod)
    {
        cod = 200;
        string[] p = ruta.Substring(4).Split('/');
        int limite = q["limit"] != null ? int.Parse(q["limit"], CultureInfo.InvariantCulture) : 20;
        int desde = q["offset"] != null ? int.Parse(q["offset"], CultureInfo.InvariantCulture) : 0;
        if (p[0] == "search")
        {
            if (limite < 0 || limite > 10) { cod = 400; return "{\"error\":{\"status\":400,\"message\":\"Invalid limit\"}}"; }
            if (q["limit"] == null) limite = 5;
            if (desde < 0 || desde > 1000) { cod = 400; return "{\"error\":{\"status\":400,\"message\":\"Invalid offset\"}}"; }
            string t = (q["q"] ?? "").ToLowerInvariant();
            string tipos = q["type"] ?? "";
            if (t.StartsWith("isrc:", StringComparison.Ordinal) || t.StartsWith("track:", StringComparison.Ordinal))
            {   // (las de la sustitucion: por su ISRC, o por titulo y artista -contiene, sin acentos-, como un filtro de campo de la de verdad)
                List<Cancion> l;
                if (t.StartsWith("isrc:", StringComparison.Ordinal)) { string i = t.Substring(5).Trim().ToUpperInvariant(); l = sustitucion.FindAll(x => x.Isrc == i); }
                else
                {
                    string tr = Campo(t, "track:"), ar = Campo(t, "artist:");
                    l = sustitucion.FindAll(x => SinAcentos(x.Nombre).Contains(tr) && (ar.Length == 0 || SinAcentos(x.Art.Nombre).Contains(ar)));
                }
                return Escribe(w => { w.WriteStartObject(); w.WritePropertyName("tracks"); Pagina(w, l, limite, desde, CancionEntera); w.WriteEndObject(); });
            }
            // (pared-generos) un disco del catalogo sin id: «album:"NOMBRE" artist:"ARTISTA"»
            if (t.StartsWith("album:\"", StringComparison.Ordinal))
            {
                string orig = q["q"]; int fin = orig.IndexOf('"', 7);
                string nombre = fin > 7 ? orig.Substring(7, fin - 7) : "";
                List<Disco> l = new List<Disco>();
                if (nombre.Length > 0 && !nombre.ToLowerInvariant().Contains("sin spotify")) { uint h = 2166136261; foreach (char ch in nombre) { h ^= ch; h *= 16777619; } Disco d0 = DiscoDelCatalogo("catb" + h.ToString("x8", CultureInfo.InvariantCulture)); d0.Nombre = nombre; l.Add(d0); }
                return Escribe(w => { w.WriteStartObject(); w.WritePropertyName("albums"); Pagina(w, l, limite, desde, (ww, d) => DiscoSimple(ww, d)); w.WriteEndObject(); });
            }
            // «artist:"NOMBRE"»: solo las de ese artista (como el filtro de campo de Spotify)
            string deArtista = null;
            if (t.StartsWith("artist:\"", StringComparison.Ordinal) && t.EndsWith("\"", StringComparison.Ordinal) && t.Length > 9) deArtista = t.Substring(8, t.Length - 9);
            List<Artista> arts = artistas.FindAll(x => x.Nombre.ToLowerInvariant().Contains(t) || Distancia(x.Nombre.ToLowerInvariant(), t) <= 2);
            if (arts.Count > 0 && !arts.Contains(artistas[2])) arts.Add(artistas[2]);   // (y uno que no tiene que ver: Grupo Ficticio)
            return Escribe(w =>
            {
                w.WriteStartObject();
                if (tipos.Contains("track")) { w.WritePropertyName("tracks"); Pagina(w, canciones.FindAll(x => deArtista != null ? x.Art.Nombre.ToLowerInvariant() == deArtista : (x.Nombre + " " + x.Art.Nombre).ToLowerInvariant().Contains(t)), limite, desde, CancionEntera); }
                if (tipos.Contains("album")) { w.WritePropertyName("albums"); int mt; lock (cerrojo) mt = miente; Pagina(w, discos.FindAll(x => (x.Nombre + " " + x.Art.Nombre).ToLowerInvariant().Contains(t)), limite, desde, (ww, d) => DiscoSimple(ww, d), mt); }
                if (tipos.Contains("artist")) { w.WritePropertyName("artists"); Pagina(w, deArtista != null ? new List<Artista>() : arts, limite, desde, ArtistaEntero); }
                w.WriteEndObject();
            });
        }
        if (p[0] == "albums" && p.Length >= 2)
        {
            Disco d = discos.Find(x => x.Id == p[1]) ?? (muchos ? extraInter.Find(x => x.Id == p[1]) : null);
            if (d == null && p[1].StartsWith("cat", StringComparison.Ordinal) && !p[1].StartsWith("catno", StringComparison.Ordinal)) d = DiscoDelCatalogo(p[1]);   // (pared-generos)
            if (d == null) { cod = 404; return "{\"error\":{\"status\":404,\"message\":\"Resource not found\"}}"; }
            if (p.Length >= 3 && p[2] == "tracks") return Escribe(w => Pagina(w, d.Canciones, Math.Min(limite, 50), desde, CancionSimple));
            return Escribe(w => { w.WriteStartObject(); DiscoCampos(w, d); w.WritePropertyName("tracks"); Pagina(w, d.Canciones, 50, 0, CancionSimple); w.WriteEndObject(); });
        }
        if (p[0] == "artists" && p.Length >= 3 && p[2] == "albums")
        {
            if (limite > 10) { cod = 400; return "{\"error\":{\"status\":400,\"message\":\"Invalid limit\"}}"; }
            List<Disco> suyos = discos.FindAll(x => x.Art.Id == p[1]);
            if (muchos && p[1] == "art50") suyos.AddRange(extraInter);
            return Escribe(w => Pagina(w, suyos, limite, desde, (ww, d) => DiscoSimple(ww, d)));
        }
        if (p[0] == "me" || p[0] == "playlists")
        {
            if (llave != "cuenta") { cod = 401; return "{\"error\":{\"status\":401,\"message\":\"Valid user authentication required\"}}"; }
            if (p[0] == "me" && p.Length >= 2 && p[1] == "tracks")
                return Escribe(w => Pagina(w, canciones.GetRange(3, 7), limite, desde, (ww, x) => { ww.WriteStartObject(); ww.WriteString("added_at", "2026-11-01T00:00:00Z"); ww.WritePropertyName("track"); CancionEntera(ww, x); ww.WriteEndObject(); }));
            if (p[0] == "me" && p.Length >= 3 && p[1] == "top" && p[2] == "tracks")
                return Escribe(w => Pagina(w, new List<Cancion> { canciones[12], canciones[5], canciones[1], canciones[20] }, limite, desde, CancionEntera));
            if (p[0] == "me" && p.Length >= 3 && p[1] == "player" && p[2] == "recently-played")
                return Escribe(w => Pagina(w, new List<Cancion> { canciones[2], canciones[7], canciones[2], canciones[9] }, limite, desde, (ww, x) => { ww.WriteStartObject(); ww.WriteString("played_at", "2026-11-14T22:00:00Z"); ww.WritePropertyName("track"); CancionEntera(ww, x); ww.WriteEndObject(); }));
            if (p[0] == "me" && p.Length >= 2 && p[1] == "albums")
                return Escribe(w => Pagina(w, discos.GetRange(1, 2), limite, desde, (ww, x) => { ww.WriteStartObject(); ww.WriteString("added_at", "2026-11-01T00:00:00Z"); ww.WritePropertyName("album"); ww.WriteStartObject(); DiscoCampos(ww, x); ww.WritePropertyName("tracks"); Pagina(ww, x.Canciones, 50, 0, CancionSimple); ww.WriteEndObject(); ww.WriteEndObject(); }));
            if (p[0] == "me" && p.Length >= 2 && p[1] == "playlists")
                return Escribe(w => Pagina(w, listas, limite, desde, (ww, x) =>
                {
                    ww.WriteStartObject(); ww.WriteString("id", x.Id); ww.WriteString("name", x.Nombre); ww.WriteString("uri", "spotify:playlist:" + x.Id); ww.WriteBoolean("collaborative", false);
                    ww.WritePropertyName("owner"); ww.WriteStartObject(); ww.WriteString("id", x.Dueno); ww.WriteEndObject();
                    Imagenes(ww, "lista-" + x.Id);
                    ww.WritePropertyName("items"); ww.WriteStartObject(); ww.WriteNumber("total", x.Canciones.Count); ww.WriteEndObject();
                    ww.WriteEndObject();
                }));
            if (p[0] == "playlists" && p.Length >= 3 && p[2] == "items")
            {
                Lista li = listas.Find(x => x.Id == p[1]);
                if (li == null || li.Dueno != "dueno-falso") { cod = 403; return "{\"error\":{\"status\":403,\"message\":\"Forbidden\"}}"; }
                return Escribe(w => Pagina(w, li.Canciones, limite, desde, (ww, x) => { ww.WriteStartObject(); ww.WriteString("added_at", "2026-11-01T00:00:00Z"); ww.WritePropertyName("item"); CancionEntera(ww, x); ww.WriteEndObject(); }));
            }
        }
        cod = 404;
        return "{\"error\":{\"status\":404,\"message\":\"Service not found\"}}";
    }

    static string Campo(string q, string nombre)
    {
        int i = q.IndexOf(nombre, StringComparison.Ordinal);
        if (i < 0) return "";
        i += nombre.Length;
        if (i < q.Length && q[i] == '"') { int f = q.IndexOf('"', i + 1); return SinAcentos(f > i ? q.Substring(i + 1, f - i - 1) : q.Substring(i + 1)); }
        int e = q.IndexOf(' ', i); return SinAcentos(e > i ? q.Substring(i, e - i) : q.Substring(i));
    }
    static string SinAcentos(string s)
    {
        StringBuilder b = new StringBuilder();
        const string con = "áéíóúüñÁÉÍÓÚÜÑ", sin = "aeiouunAEIOUUN";   // (InvariantGlobalization: Normalize no separa los acentos)
        foreach (char c in s) { int k = con.IndexOf(c); b.Append(char.ToLowerInvariant(k >= 0 ? sin[k] : c)); }
        return b.ToString().Trim();
    }

    // (pared-generos) un disco del catalogo, inventado por su id (el mismo cada vez): 4 canciones
    static Disco DiscoDelCatalogo(string id)
    {
        lock (cerrojo)
        {
            Disco d;
            if (delCatalogo.TryGetValue(id, out d)) return d;
            Artista ar = new Artista { Id = "art-" + id, Nombre = "Artista del Catalogo" };
            d = new Disco { Id = id, Nombre = "Disco " + id, Art = ar };
            for (int k = 1; k <= 4; k++) d.Canciones.Add(new Cancion { Id = "c" + id + k.ToString(CultureInfo.InvariantCulture), Nombre = "Tema del Catalogo " + k.ToString(CultureInfo.InvariantCulture), Art = ar, Dis = d, Numero = k, Ms = 180000 });
            delCatalogo[id] = d;
            return d;
        }
    }

    // ---------- el JSON como el de verdad ----------
    static string Escribe(Action<Utf8JsonWriter> f)
    {
        using (MemoryStream m = new MemoryStream())
        {
            using (Utf8JsonWriter w = new Utf8JsonWriter(m)) f(w);
            return Encoding.UTF8.GetString(m.ToArray());
        }
    }
    static void Pagina<T>(Utf8JsonWriter w, List<T> todo, int limite, int desde, Action<Utf8JsonWriter, T> uno, int totalFalso = 0)
    {
        w.WriteStartObject();
        w.WriteString("href", baseUrl + "/v1/pagina");
        w.WriteNumber("limit", limite); w.WriteNumber("offset", desde); w.WriteNumber("total", Math.Max(todo.Count, totalFalso));
        if (desde + limite < todo.Count) w.WriteString("next", baseUrl + "/v1/siguiente"); else w.WriteNull("next");
        w.WriteNull("previous");
        w.WritePropertyName("items"); w.WriteStartArray();
        for (int i = desde; i < todo.Count && i < desde + limite; i++) uno(w, todo[i]);
        w.WriteEndArray();
        w.WriteEndObject();
    }
    static void Imagenes(Utf8JsonWriter w, string nombre)
    {
        w.WritePropertyName("images"); w.WriteStartArray();
        foreach (int t in new[] { 640, 300, 64 }) { w.WriteStartObject(); w.WriteString("url", baseUrl + "/img/" + nombre + "-" + t.ToString(CultureInfo.InvariantCulture) + ".jpg"); w.WriteNumber("height", t); w.WriteNumber("width", t); w.WriteEndObject(); }
        w.WriteEndArray();
    }
    static void ArtistaSimple(Utf8JsonWriter w, Artista a) { w.WriteStartObject(); w.WriteString("id", a.Id); w.WriteString("name", a.Nombre); w.WriteString("type", "artist"); w.WriteString("uri", "spotify:artist:" + a.Id); w.WriteEndObject(); }
    static void ArtistaEntero(Utf8JsonWriter w, Artista a) { w.WriteStartObject(); w.WriteString("id", a.Id); w.WriteString("name", a.Nombre); w.WriteString("type", "artist"); w.WriteString("uri", "spotify:artist:" + a.Id); w.WritePropertyName("genres"); w.WriteStartArray(); w.WriteEndArray(); Imagenes(w, "artista-" + a.Id); w.WriteEndObject(); }
    static void DiscoCampos(Utf8JsonWriter w, Disco d)
    {
        w.WriteString("album_type", d.Tipo); w.WriteNumber("total_tracks", d.Canciones.Count); w.WriteString("id", d.Id); w.WriteString("name", d.Nombre);
        w.WriteString("release_date", "2020-01-01"); w.WriteString("type", "album"); w.WriteString("uri", "spotify:album:" + d.Id);
        w.WritePropertyName("artists"); w.WriteStartArray(); ArtistaSimple(w, d.Art); w.WriteEndArray();
        Imagenes(w, "disco-" + d.Id);
    }
    static void DiscoSimple(Utf8JsonWriter w, Disco d) { w.WriteStartObject(); DiscoCampos(w, d); w.WriteEndObject(); }
    static void CancionCampos(Utf8JsonWriter w, Cancion c)
    {
        w.WritePropertyName("artists"); w.WriteStartArray(); ArtistaSimple(w, c.Art); w.WriteEndArray();
        w.WriteNumber("disc_number", 1); w.WriteNumber("duration_ms", c.Ms); w.WriteBoolean("explicit", false); w.WriteString("id", c.Id);
        w.WriteBoolean("is_playable", true); w.WriteString("name", c.Nombre); w.WriteNumber("track_number", c.Numero); w.WriteString("type", "track");
        w.WriteString("uri", "spotify:track:" + c.Id); w.WriteBoolean("is_local", false);
        if (c.Isrc != null) { w.WritePropertyName("external_ids"); w.WriteStartObject(); w.WriteString("isrc", c.Isrc); w.WriteEndObject(); }
    }
    static void CancionEntera(Utf8JsonWriter w, Cancion c) { w.WriteStartObject(); w.WritePropertyName("album"); DiscoSimple(w, c.Dis); CancionCampos(w, c); w.WriteEndObject(); }
    static void CancionSimple(Utf8JsonWriter w, Cancion c) { w.WriteStartObject(); CancionCampos(w, c); w.WriteEndObject(); }

    // ---------- la portada: un degradado con el nombre ----------
    static byte[] Portada(string nombre)
    {
        uint h = 2166136261; foreach (char ch in nombre) { h ^= ch; h *= 16777619; }
        SKColor a = new SKColor((byte)(h & 0xFF), (byte)((h >> 8) & 0xFF), (byte)((h >> 16) & 0xFF)), b = new SKColor((byte)(255 - a.Red), (byte)(200 - a.Green / 2), (byte)(120 + a.Blue / 3));
        using (SKBitmap bm = new SKBitmap(300, 300))
        using (SKCanvas cv = new SKCanvas(bm))
        using (SKPaint p = new SKPaint { IsAntialias = true })
        {
            p.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(300, 300), new[] { a, b }, SKShaderTileMode.Clamp);
            cv.DrawRect(0, 0, 300, 300, p);
            p.Shader = null; p.Color = SKColors.White.WithAlpha(200);
            cv.DrawCircle(150, 150, 70 + (h % 40), p);
            p.Color = a; cv.DrawCircle(150, 150, 18, p);
            cv.Flush();
            using (SKData d = bm.Encode(SKEncodedImageFormat.Jpeg, 90)) return d.ToArray();
        }
    }

    static int Distancia(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
    static void Apunta(HttpListenerContext c, string ruta, string llave, int cod)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(c.Request.Url.Query);
        string qq = ruta == "/v1/search" ? " q=" + (qs["q"] ?? "") : "";
        if (qs["offset"] != null) qq += " t=" + (qs["type"] ?? qs["include_groups"] ?? "") + " off=" + qs["offset"];
        lock (cerrojo)
        {
            File.AppendAllText(Path.Combine(carpeta, "peticiones.txt"), c.Request.HttpMethod + " " + ruta + " " + llave + " " + cod.ToString(CultureInfo.InvariantCulture) + qq + "\n");
            File.AppendAllText(Path.Combine(carpeta, "tiempos.txt"), llegoMs.ToString(CultureInfo.InvariantCulture) + " " + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
                + " " + c.Request.HttpMethod + " " + ruta + " " + cod.ToString(CultureInfo.InvariantCulture) + (nuevaConexion ? " nueva" : "") + "\n");
        }
    }
    static void Contesta(HttpListenerContext c, int cod, string cuerpo, string ctype, byte[] bin, Dictionary<string, string> extra)
    {
        byte[] b = bin ?? Encoding.UTF8.GetBytes(cuerpo ?? "");
        c.Response.StatusCode = cod;
        c.Response.ContentType = ctype;
        foreach (KeyValuePair<string, string> kv in extra) c.Response.Headers[kv.Key] = kv.Value;
        c.Response.ContentLength64 = b.Length;
        c.Response.OutputStream.Write(b, 0, b.Length);
        c.Response.OutputStream.Close();
    }
}

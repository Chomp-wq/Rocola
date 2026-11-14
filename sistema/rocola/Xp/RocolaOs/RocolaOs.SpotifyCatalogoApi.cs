// EL CATALOGO DE SPOTIFY: EL CLIENTE DE LA API WEB (fase 11, grupo catalogo; FASE11_catalogo.md). No es de XP: es lo que Rocola OS le
// pregunta a la API web de Spotify (https://api.spotify.com/v1) con la app PROPIA del dueno (su panel de desarrolladores, modo desarrollo),
// para buscar y abrir discos EN LA ROCOLA. El sonido no pasa por aqui (lo toca Soloist, el cliente oficial; lo lleva el nucleo).
//
// LA LLAVE (I7): /rocola/datos/xp/rocola_spotify_api.txt (0600; lo copia de la memoria USB el vigilante), lineas «clave=valor»:
//   client_id=...        la app del dueno                     \  Client Credentials: buscar, abrir discos y los discos de un artista
//   client_secret=...    (SECRETO)                            /  (sin usuario: lo que la documentacion permite sin cuenta)
//   refresh_token=...    (opcional, SECRETO) la cuenta del dueno autorizada con PKCE desde su laptop: «tus favoritas», tus discos y tus
//                        listas. Spotify puede dar una nueva al renovar: se guarda encima, de un tiron (.t y cambio de nombre, 0600)
//   mercado=MX           (opcional) el pais de las canciones que se pueden tocar (sin usuario hay que decirlo); de fabrica MX
// NADA de esto se apunta, ni sale en el estado, ni en la bitacora, ni en la pantalla. Sin client_id o client_secret no se pregunta nada.
//
// LO QUE SE PREGUNTA (documentacion oficial del 14-11-2026, con los cambios del modo desarrollo de febrero de 2026: FASE11_catalogo.md):
//   POST {cuentas}/api/token                         la llave (client_credentials o refresh_token), renovada 60 s antes de vencer
//   GET  /search?q=..&type=track,album,artist&limit=10&market=MX           UNA por busqueda (el tope de hoy es 10 por tipo)
//   GET  /albums/{id}?market=MX                      un disco con sus canciones (hasta 50; si tiene mas, /albums/{id}/tracks?offset=50)
//   GET  /artists/{id}/albums?include_groups=album,single&limit=10&market=MX   los discos de un artista
//   GET  /me/tracks?limit=50, /me/albums?limit=50, /me/playlists?limit=50     lo de la cuenta (solo con refresh_token; una vez por sesion)
//   GET  /playlists/{id}/items?limit=50&market=MX    una lista propia, al abrirla (ya no es /tracks; las de Spotify dan 404)
// LA CUOTA (por cuenta de desarrollador; con 429 hay bloqueos de horas): UNA peticion a la vez, en su hilo; lo que alguien espera (buscar,
// abrir) antes que lo de la cuenta; nada de recorridos de fondo; todo lo contestado se guarda en memoria toda la sesion; una busqueda se pide
// a los 300 ms de dejar de escribir (y solo la ultima); con 429 no se pregunta NADA hasta que pase su Retry-After («Spotify frena» en la
// bitacora y en el estado); con 401 se pide otra llave y se intenta una vez mas; con 5xx o sin red, una vez mas al segundo.
//
// PARA LAS PRUEBAS (solo ahi): ROCOLA_SPOTIFY_API (en lugar de https://api.spotify.com/v1) y ROCOLA_SPOTIFY_CUENTAS (en lugar de
// https://accounts.spotify.com): la API falsa de Pruebas/RocolaOs/catalogo.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Rocola
{
    // LO QUE CONTESTA SPOTIFY, ya leido (solo lo que la rocola usa)
    internal sealed class RocolaOsSpotifyCatalogoCancion
    {
        internal string Id, Uri, Titulo, Artista, Disco, DiscoId, DiscoUri, PortadaUrl;
        internal int DuracionMs, Numero;
    }
    internal sealed class RocolaOsSpotifyCatalogoDisco
    {
        internal string Id, Uri, Nombre, Artista, PortadaUrl, Tipo;   // Tipo: «album», «playlist» o «favoritas»
        internal List<RocolaOsSpotifyCatalogoCancion> Canciones;       // null = aun sin abrir
    }
    internal sealed class RocolaOsSpotifyCatalogoArtista
    {
        internal string Id, Uri, Nombre, PortadaUrl;
    }
    internal sealed class RocolaOsSpotifyCatalogoBusqueda
    {
        internal string Consulta;
        internal List<RocolaOsSpotifyCatalogoCancion> Canciones = new List<RocolaOsSpotifyCatalogoCancion>();
        internal List<RocolaOsSpotifyCatalogoDisco> Discos = new List<RocolaOsSpotifyCatalogoDisco>();
        internal List<RocolaOsSpotifyCatalogoArtista> Artistas = new List<RocolaOsSpotifyCatalogoArtista>();
    }

    internal static class RocolaOsSpotifyCatalogoApi
    {
        internal const string Archivo = "rocola_spotify_api.txt";
        internal const int EsperaBusquedaMs = 300;

        // ---------- los pedidos ----------
        private sealed class Pedido
        {
            internal string Que;              // «busca», «disco», «artista», «lista», «cuenta»
            internal string Clave;            // la consulta o el id
            internal bool Alguien;            // lo espera alguien (va antes que lo de la cuenta)
            internal long NoAntesDe;          // (las busquedas: a los 300 ms de la ultima tecla)
            internal Action<object, string> Listo;   // (resultado o null, error o null), en ESTE hilo: quien lo pide lo encarga a la pantalla
        }
        private static readonly object cerrojo = new object();
        private static readonly List<Pedido> cola = new List<Pedido>();
        private static Pedido busquedaPendiente;   // solo la ultima busqueda
        private static Thread hilo;
        private static readonly AutoResetEvent hayAlgo = new AutoResetEvent(false);

        // lo contestado, toda la sesion (solo en memoria)
        private static readonly Dictionary<string, RocolaOsSpotifyCatalogoBusqueda> busquedas = new Dictionary<string, RocolaOsSpotifyCatalogoBusqueda>(StringComparer.Ordinal);
        private static readonly Dictionary<string, RocolaOsSpotifyCatalogoDisco> discos = new Dictionary<string, RocolaOsSpotifyCatalogoDisco>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<RocolaOsSpotifyCatalogoDisco>> deArtista = new Dictionary<string, List<RocolaOsSpotifyCatalogoDisco>>(StringComparer.Ordinal);

        // ---------- lo que se sabe (para el estado y las pruebas: sin secretos) ----------
        private static long peticiones, peticionesBusqueda, peticionesDisco, peticionesArtista, peticionesCuenta, peticionesLlave, frenadas, fallos;
        private static int ultimoCodigo;
        private static long frenaHastaMs;          // (Environment.TickCount64) con 429: nada hasta entonces
        private static string frenaRazon = "";
        private static string api = "sin_llave";   // sin_llave, bien, frena, falla
        internal static long Peticiones { get { return Interlocked.Read(ref peticiones); } }
        internal static bool Frena { get { return Environment.TickCount64 < Volatile.Read(ref frenaHastaMs); } }
        internal static bool HayLlave { get { Config c = LeeConfig(); return c != null && c.Listo; } }
        internal static bool HayCuenta { get { Config c = LeeConfig(); return c != null && c.Listo && !string.IsNullOrEmpty(c.Refresco); } }

        // ---------- pedir (desde cualquier hilo; Listo corre en el hilo del catalogo) ----------
        // BUSCAR: la contestada se da al momento (Listo en el hilo que llama); si no, se pide a los 300 ms (solo la ultima)
        internal static RocolaOsSpotifyCatalogoBusqueda BuscaYa(string consulta)
        {
            lock (cerrojo) { RocolaOsSpotifyCatalogoBusqueda b; return busquedas.TryGetValue(consulta, out b) ? b : null; }
        }
        internal static void Busca(string consulta, Action<object, string> listo)
        {
            Pedido p = new Pedido { Que = "busca", Clave = consulta, Alguien = true, NoAntesDe = Environment.TickCount64 + EsperaBusquedaMs, Listo = listo };
            lock (cerrojo) { busquedaPendiente = p; }
            Despierta();
        }
        internal static void OlvidaBusqueda() { lock (cerrojo) { busquedaPendiente = null; } }
        internal static RocolaOsSpotifyCatalogoDisco DiscoYa(string id)
        {
            lock (cerrojo) { RocolaOsSpotifyCatalogoDisco d; return discos.TryGetValue(id, out d) && d.Canciones != null ? d : null; }
        }
        internal static void AbreDisco(string id, Action<object, string> listo) { Encola(new Pedido { Que = "disco", Clave = id, Alguien = true, Listo = listo }); }
        internal static void AbreLista(string id, Action<object, string> listo) { Encola(new Pedido { Que = "lista", Clave = id, Alguien = true, Listo = listo }); }
        internal static List<RocolaOsSpotifyCatalogoDisco> DeArtistaYa(string id)
        {
            lock (cerrojo) { List<RocolaOsSpotifyCatalogoDisco> l; return deArtista.TryGetValue(id, out l) ? l : null; }
        }
        internal static void AbreArtista(string id, Action<object, string> listo) { Encola(new Pedido { Que = "artista", Clave = id, Alguien = true, Listo = listo }); }
        // LO DE LA CUENTA (solo con refresh_token): una vez por sesion, detras de lo que alguien espera
        internal static void PideCuenta(Action<object, string> listo) { Encola(new Pedido { Que = "cuenta", Clave = "", Alguien = false, Listo = listo }); }

        private static void Encola(Pedido p)
        {
            lock (cerrojo)
            {
                foreach (Pedido q in cola) if (q.Que == p.Que && q.Clave == p.Clave) { Action<object, string> a = q.Listo, b = p.Listo; q.Listo = delegate (object r, string e) { if (a != null) a(r, e); if (b != null) b(r, e); }; return; }
                cola.Add(p);
            }
            Despierta();
        }
        private static void Despierta()
        {
            lock (cerrojo)
            {
                if (hilo == null) hilo = RocolaOs.Hilo("spotify_catalogo", Corre);
            }
            hayAlgo.Set();
        }

        // ---------- el hilo: una peticion a la vez ----------
        private static void Corre()
        {
            while (true)
            {
                Pedido p = null; int espera = 5000;
                lock (cerrojo)
                {
                    long ahora = Environment.TickCount64;
                    Pedido b = busquedaPendiente;
                    if (b != null && b.NoAntesDe <= ahora) { p = b; busquedaPendiente = null; }
                    else
                    {
                        int i = cola.FindIndex(x => x.Alguien);
                        if (i < 0 && b == null && cola.Count > 0) i = 0;   // lo de la cuenta, solo si nadie espera nada
                        if (i >= 0) { p = cola[i]; cola.RemoveAt(i); }
                        else if (b != null) espera = (int)Math.Max(1, b.NoAntesDe - ahora);
                    }
                }
                if (p == null) { hayAlgo.WaitOne(espera); continue; }
                object r = null; string error = null;
                try { r = Atiende(p, out error); }
                catch (Exception ex) { error = "fallo: " + ex.GetType().Name; RocolaOs.Falla("spotify_catalogo", "pedido «" + p.Que + "»", ex); }
                if (r == null && error == null) error = "sin respuesta";
                try { if (p.Listo != null) p.Listo(r, error); }
                catch (Exception ex) { RocolaOs.Falla("spotify_catalogo", "al contestar «" + p.Que + "»", ex); }
                EscribeEstado();
            }
        }

        private static object Atiende(Pedido p, out string error)
        {
            error = null;
            Config c = LeeConfig();
            if (c == null || !c.Listo) { error = "sin_llave"; api = "sin_llave"; return null; }
            string m = Uri.EscapeDataString(c.Mercado);
            if (p.Que == "busca")
            {
                RocolaOsSpotifyCatalogoBusqueda ya = BuscaYa(p.Clave);
                if (ya != null) return ya;
                JsonDocument d = Pide(c, false, "/search?q=" + Uri.EscapeDataString(p.Clave) + "&type=track,album,artist&limit=10&market=" + m, ref peticionesBusqueda, true, out error);
                if (d == null) return null;
                RocolaOsSpotifyCatalogoBusqueda b;
                using (d) b = LeeBusqueda(p.Clave, d.RootElement);
                lock (cerrojo) { busquedas[p.Clave] = b; foreach (RocolaOsSpotifyCatalogoDisco x in b.Discos) if (!discos.ContainsKey(x.Id)) discos[x.Id] = x; }
                return b;
            }
            if (p.Que == "disco")
            {
                RocolaOsSpotifyCatalogoDisco ya = DiscoYa(p.Clave);
                if (ya != null) return ya;
                JsonDocument d = Pide(c, false, "/albums/" + Uri.EscapeDataString(p.Clave) + "?market=" + m, ref peticionesDisco, true, out error);
                if (d == null) return null;
                RocolaOsSpotifyCatalogoDisco disco;
                int total, leidas;
                using (d)
                {
                    disco = LeeDisco(d.RootElement, null);
                    disco.Canciones = new List<RocolaOsSpotifyCatalogoCancion>();
                    JsonElement t;
                    total = 0;
                    if (d.RootElement.TryGetProperty("tracks", out t) && t.ValueKind == JsonValueKind.Object)
                    {
                        LeeCancionesDeDisco(t, disco);
                        total = Entero(t, "total");
                    }
                    leidas = disco.Canciones.Count;
                }
                // un disco de mas de 50 canciones: UNA pagina mas (de 50) como mucho
                if (total > leidas && leidas >= 50)
                {
                    string e2;
                    JsonDocument d2 = Pide(c, false, "/albums/" + Uri.EscapeDataString(p.Clave) + "/tracks?limit=50&offset=" + leidas.ToString(CultureInfo.InvariantCulture) + "&market=" + m, ref peticionesDisco, true, out e2);
                    if (d2 != null) using (d2) LeeCancionesDeDisco(d2.RootElement, disco);
                }
                lock (cerrojo) { discos[disco.Id] = disco; }
                return disco;
            }
            if (p.Que == "lista")
            {
                RocolaOsSpotifyCatalogoDisco ya = DiscoYa(p.Clave);
                if (ya != null) return ya;
                RocolaOsSpotifyCatalogoDisco lista;
                lock (cerrojo) { discos.TryGetValue(p.Clave, out lista); }
                if (lista == null) { error = "lista desconocida"; return null; }
                if (!c.TieneCuenta) { error = "sin cuenta"; return null; }
                JsonDocument d = Pide(c, true, "/playlists/" + Uri.EscapeDataString(p.Clave) + "/items?limit=50&market=" + m, ref peticionesDisco, true, out error);
                if (d == null) return null;
                List<RocolaOsSpotifyCatalogoCancion> l = new List<RocolaOsSpotifyCatalogoCancion>();
                using (d) LeeItems(d.RootElement, l);
                lock (cerrojo) { lista.Canciones = l; }
                return lista;
            }
            if (p.Que == "artista")
            {
                List<RocolaOsSpotifyCatalogoDisco> ya = DeArtistaYa(p.Clave);
                if (ya != null) return ya;
                JsonDocument d = Pide(c, false, "/artists/" + Uri.EscapeDataString(p.Clave) + "/albums?include_groups=album,single&limit=10&market=" + m, ref peticionesArtista, true, out error);
                if (d == null) return null;
                List<RocolaOsSpotifyCatalogoDisco> l = new List<RocolaOsSpotifyCatalogoDisco>();
                using (d)
                {
                    JsonElement items;
                    if (d.RootElement.TryGetProperty("items", out items) && items.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement a in items.EnumerateArray()) { RocolaOsSpotifyCatalogoDisco x = LeeDisco(a, null); if (x != null) l.Add(x); }
                }
                lock (cerrojo)
                {
                    for (int i = 0; i < l.Count; i++) { RocolaOsSpotifyCatalogoDisco y; if (discos.TryGetValue(l[i].Id, out y)) l[i] = y; else discos[l[i].Id] = l[i]; }
                    deArtista[p.Clave] = l;
                }
                return l;
            }
            if (p.Que == "cuenta")
            {
                if (!c.TieneCuenta) { error = "sin cuenta"; return null; }
                List<RocolaOsSpotifyCatalogoDisco> l = new List<RocolaOsSpotifyCatalogoDisco>();
                string e;
                // tus favoritas: las ultimas 50 canciones que guardaste, como un disco
                JsonDocument d = Pide(c, true, "/me/tracks?limit=50&market=" + m, ref peticionesCuenta, false, out e);
                if (d != null)
                {
                    RocolaOsSpotifyCatalogoDisco f = new RocolaOsSpotifyCatalogoDisco { Id = "favoritas", Uri = "spotify:favoritas", Nombre = "Tus favoritas", Artista = "Spotify", Tipo = "favoritas", Canciones = new List<RocolaOsSpotifyCatalogoCancion>() };
                    using (d) LeeItems(d.RootElement, f.Canciones);
                    if (f.Canciones.Count > 0) { f.PortadaUrl = f.Canciones[0].PortadaUrl; l.Add(f); }
                }
                else if (e != null) error = e;
                if (!Frena)
                {
                    d = Pide(c, true, "/me/albums?limit=50&market=" + m, ref peticionesCuenta, false, out e);
                    if (d != null)
                        using (d)
                        {
                            JsonElement items;
                            if (d.RootElement.TryGetProperty("items", out items) && items.ValueKind == JsonValueKind.Array)
                                foreach (JsonElement it in items.EnumerateArray())
                                {
                                    JsonElement a;
                                    if (!it.TryGetProperty("album", out a) || a.ValueKind != JsonValueKind.Object) continue;
                                    RocolaOsSpotifyCatalogoDisco x = LeeDisco(a, null);
                                    if (x == null) continue;
                                    JsonElement t;
                                    if (a.TryGetProperty("tracks", out t) && t.ValueKind == JsonValueKind.Object && Entero(t, "total") <= 50)
                                    { x.Canciones = new List<RocolaOsSpotifyCatalogoCancion>(); LeeCancionesDeDisco(t, x); }
                                    l.Add(x);
                                }
                        }
                    else if (e != null && error == null) error = e;
                }
                if (!Frena)
                {
                    d = Pide(c, true, "/me/playlists?limit=50", ref peticionesCuenta, false, out e);
                    if (d != null)
                        using (d)
                        {
                            JsonElement items;
                            if (d.RootElement.TryGetProperty("items", out items) && items.ValueKind == JsonValueKind.Array)
                                foreach (JsonElement it in items.EnumerateArray())
                                {
                                    if (it.ValueKind != JsonValueKind.Object) continue;
                                    string id = Texto(it, "id");
                                    if (string.IsNullOrEmpty(id)) continue;
                                    // solo las PROPIAS o colaborativas: de las demas Spotify ya no da las canciones (febrero de 2026)
                                    bool colaborativa = it.TryGetProperty("collaborative", out JsonElement col) && col.ValueKind == JsonValueKind.True;
                                    string dueno = it.TryGetProperty("owner", out JsonElement ow) && ow.ValueKind == JsonValueKind.Object ? Texto(ow, "id") : null;
                                    if (!colaborativa && c.Usuario != null && dueno != null && dueno != c.Usuario) continue;
                                    RocolaOsSpotifyCatalogoDisco x = new RocolaOsSpotifyCatalogoDisco { Id = id, Uri = Texto(it, "uri"), Nombre = Texto(it, "name") ?? "", Artista = "Spotify", Tipo = "playlist", PortadaUrl = Portada(it) };
                                    l.Add(x);
                                }
                        }
                    else if (e != null && error == null) error = e;
                }
                lock (cerrojo) { foreach (RocolaOsSpotifyCatalogoDisco x in l) { RocolaOsSpotifyCatalogoDisco y; if (!discos.TryGetValue(x.Id, out y) || (y.Canciones == null && x.Canciones != null)) discos[x.Id] = x; } }
                if (l.Count == 0 && error != null) return null;
                error = null;
                return l;
            }
            error = "pedido desconocido";
            return null;
        }

        // ---------- la red ----------
        private static HttpClient http;
        private static string base_, cuentas;
        private static string Base { get { Prepara(); return base_; } }
        private static void Prepara()
        {
            if (http != null) return;
            string b = Environment.GetEnvironmentVariable("ROCOLA_SPOTIFY_API"), cu = Environment.GetEnvironmentVariable("ROCOLA_SPOTIFY_CUENTAS");
            base_ = string.IsNullOrEmpty(b) ? "https://api.spotify.com/v1" : b.TrimEnd('/');
            cuentas = string.IsNullOrEmpty(cu) ? "https://accounts.spotify.com" : cu.TrimEnd('/');
            bool local = base_.StartsWith("http://127.0.0.1", StringComparison.Ordinal);   // (las pruebas: sin el proxy del entorno)
            SocketsHttpHandler h = new SocketsHttpHandler { UseProxy = !local, PooledConnectionLifetime = TimeSpan.FromMinutes(5), ConnectTimeout = TimeSpan.FromSeconds(8), AutomaticDecompression = DecompressionMethods.All };
            HttpClient c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(15) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("rocola-xp/1");
            http = c;
        }
        internal static HttpClient Http { get { Prepara(); return http; } }

        // UNA PETICION a la API (con la llave de la app, o la de la cuenta si deUsuario): el JSON, o null con el error dicho sin secretos
        private static JsonDocument Pide(Config c, bool deUsuario, string ruta, ref long contador, bool reintenta, out string error)
        {
            error = null;
            for (int intento = 0; intento < 2; intento++)
            {
                if (Frena) { error = "Spotify frena"; return null; }
                string llave = Llave(c, deUsuario, out error);
                if (llave == null) return null;
                HttpResponseMessage r;
                try
                {
                    HttpRequestMessage q = new HttpRequestMessage(HttpMethod.Get, Base + ruta);
                    q.Headers.TryAddWithoutValidation("Authorization", "Bearer " + llave);
                    Interlocked.Increment(ref peticiones); Interlocked.Increment(ref contador);
                    r = Http.Send(q);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref fallos); api = "falla";
                    error = "sin red (" + ex.GetType().Name + ")";
                    if (intento == 0 && reintenta) { Thread.Sleep(1000); continue; }
                    RocolaOs.Apunta("spotify_catalogo", "la API no contesto: " + error);
                    return null;
                }
                using (r)
                {
                    int cod = (int)r.StatusCode;
                    ultimoCodigo = cod;
                    if (cod == 200)
                    {
                        api = "bien";
                        try { using (Stream s = r.Content.ReadAsStream()) return JsonDocument.Parse(s); }
                        catch (Exception ex) { error = "respuesta rara (" + ex.GetType().Name + ")"; return null; }
                    }
                    if (cod == 429) { Frenada(r); error = "Spotify frena"; return null; }
                    if (cod == 401 && intento == 0) { OlvidaLlave(deUsuario); continue; }
                    if (cod >= 500 && intento == 0 && reintenta) { Thread.Sleep(1000); continue; }
                    Interlocked.Increment(ref fallos);
                    if (cod >= 500 || cod == 401 || cod == 403) api = "falla";
                    error = "la API contesto " + cod.ToString(CultureInfo.InvariantCulture);
                    RocolaOs.Apunta("spotify_catalogo", error + " (" + RutaSinConsulta(ruta) + ")");
                    return null;
                }
            }
            return null;
        }
        private static string RutaSinConsulta(string ruta) { int i = ruta.IndexOf('?'); return i < 0 ? ruta : ruta.Substring(0, i); }

        // 429: nada hasta que pase el Retry-After (sin el, 30 s); se dice en la bitacora y en el estado
        private static void Frenada(HttpResponseMessage r)
        {
            int seg = 30;
            try
            {
                IEnumerable<string> v;
                if (r.Headers.TryGetValues("Retry-After", out v)) foreach (string s in v) { int n; if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n >= 0) seg = Math.Max(1, n); break; }
            }
            catch { }
            string razon = "";
            try { string cuerpo = r.Content.ReadAsStringAsync().GetAwaiter().GetResult(); if (cuerpo.IndexOf("QUOTA_EXCEEDED", StringComparison.Ordinal) >= 0) razon = " (QUOTA_EXCEEDED: la cuota de la cuenta de desarrollador)"; }
            catch { }
            Volatile.Write(ref frenaHastaMs, Environment.TickCount64 + seg * 1000L);
            frenaRazon = razon;
            Interlocked.Increment(ref frenadas);
            api = "frena";
            RocolaOs.Apunta("spotify_catalogo", "Spotify frena (429): no se le pregunta nada en " + seg.ToString(CultureInfo.InvariantCulture) + " s" + razon);
        }

        // ---------- las llaves ----------
        private static string llaveApp, llaveCuenta;
        private static long venceApp, venceCuenta;
        private static void OlvidaLlave(bool deUsuario) { if (deUsuario) llaveCuenta = null; else llaveApp = null; }
        private static string Llave(Config c, bool deUsuario, out string error)
        {
            error = null;
            long ahora = Environment.TickCount64;
            if (!deUsuario && llaveApp != null && ahora < venceApp - 60000) return llaveApp;
            if (deUsuario && llaveCuenta != null && ahora < venceCuenta - 60000) return llaveCuenta;
            string cuerpo = deUsuario
                ? "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(c.Refresco) + "&client_id=" + Uri.EscapeDataString(c.Id)
                : "grant_type=client_credentials";
            HttpResponseMessage r;
            try
            {
                Prepara();
                HttpRequestMessage q = new HttpRequestMessage(HttpMethod.Post, cuentas + "/api/token");
                q.Headers.TryAddWithoutValidation("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Id + ":" + c.Secreto)));
                q.Content = new StringContent(cuerpo, Encoding.ASCII, "application/x-www-form-urlencoded");
                Interlocked.Increment(ref peticionesLlave);
                r = http.Send(q);
            }
            catch (Exception ex) { api = "falla"; error = "sin red al pedir la llave (" + ex.GetType().Name + ")"; return null; }
            using (r)
            {
                int cod = (int)r.StatusCode;
                ultimoCodigo = cod;
                if (cod == 429) { Frenada(r); error = "Spotify frena"; return null; }
                if (cod != 200)
                {
                    api = "falla";
                    error = "la llave no se dio (" + cod.ToString(CultureInfo.InvariantCulture) + (cod == 400 || cod == 401 ? (deUsuario ? ": la autorizacion de la cuenta vencio o la quitaron" : ": client_id o client_secret no valen") : "") + ")";
                    RocolaOs.Apunta("spotify_catalogo", error);
                    return null;
                }
                try
                {
                    using (Stream s = r.Content.ReadAsStream())
                    using (JsonDocument d = JsonDocument.Parse(s))
                    {
                        string t = Texto(d.RootElement, "access_token");
                        int exp = Entero(d.RootElement, "expires_in");
                        if (string.IsNullOrEmpty(t)) { error = "la llave vino vacia"; return null; }
                        long vence = ahora + Math.Max(120, exp) * 1000L;
                        if (deUsuario)
                        {
                            llaveCuenta = t; venceCuenta = vence;
                            string nueva = Texto(d.RootElement, "refresh_token");
                            if (!string.IsNullOrEmpty(nueva) && nueva != c.Refresco) GuardaRefresco(c, nueva);
                        }
                        else { llaveApp = t; venceApp = vence; }
                        return t;
                    }
                }
                catch (Exception ex) { error = "la llave vino rara (" + ex.GetType().Name + ")"; return null; }
            }
        }

        // ---------- el archivo de la llave ----------
        private sealed class Config
        {
            internal string Id, Secreto, Refresco, Mercado = "MX", Usuario;
            internal string[] Lineas;
            internal bool Listo { get { return !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(Secreto); } }
            internal bool TieneCuenta { get { return !string.IsNullOrEmpty(Refresco); } }
        }
        private static Config config;
        private static DateTime configFecha;
        private static long configMirado = long.MinValue / 2;
        private static readonly object cerrojoConfig = new object();
        internal static string RutaArchivo { get { return RocolaOs.Datos(Archivo); } }
        private static Config LeeConfig()
        {
            lock (cerrojoConfig)
            {
                long ahora = Environment.TickCount64;
                if (config != null && ahora - configMirado < 5000) return config;
                configMirado = ahora;
                string ruta = RutaArchivo;
                DateTime f;
                try { f = File.Exists(ruta) ? File.GetLastWriteTimeUtc(ruta) : DateTime.MinValue; } catch { f = DateTime.MinValue; }
                if (config != null && f == configFecha) return config;
                Config c = new Config();
                if (f != DateTime.MinValue)
                {
                    try
                    {
                        c.Lineas = File.ReadAllLines(ruta);
                        foreach (string l0 in c.Lineas)
                        {
                            string l = l0.Trim();
                            int i = l.IndexOf('=');
                            if (i <= 0 || l.StartsWith("#", StringComparison.Ordinal)) continue;
                            string k = l.Substring(0, i).Trim(), v = l.Substring(i + 1).Trim();
                            if (k == "client_id") c.Id = v;
                            else if (k == "client_secret") c.Secreto = v;
                            else if (k == "refresh_token") c.Refresco = v;
                            else if (k == "mercado" && v.Length == 2) c.Mercado = v.ToUpperInvariant();
                            else if (k == "usuario") c.Usuario = v;
                        }
                    }
                    catch (Exception ex) { RocolaOs.Apunta("spotify_catalogo", "no se pudo leer " + Archivo + " (" + ex.GetType().Name + ")"); }
                }
                bool cambio = config != null;
                config = c; configFecha = f;
                if (cambio) { llaveApp = null; llaveCuenta = null; }
                if (!c.Listo) api = "sin_llave";
                return c;
            }
        }
        // Spotify dio otra llave de refresco (con PKCE la de antes deja de valer): se guarda encima de la vieja, de un tiron y 0600
        private static void GuardaRefresco(Config c, string nueva)
        {
            try
            {
                string ruta = RutaArchivo;
                StringBuilder sb = new StringBuilder();
                bool puesta = false;
                foreach (string l in c.Lineas ?? new string[0])
                {
                    if (l.TrimStart().StartsWith("refresh_token=", StringComparison.Ordinal)) { if (!puesta) sb.Append("refresh_token=").Append(nueva).Append('\n'); puesta = true; }
                    else sb.Append(l).Append('\n');
                }
                if (!puesta) sb.Append("refresh_token=").Append(nueva).Append('\n');
                string t = ruta + ".t." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
                File.WriteAllText(t, sb.ToString());
                try { File.SetUnixFileMode(t, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
                File.Move(t, ruta, true);
                lock (cerrojoConfig) { c.Refresco = nueva; configFecha = File.GetLastWriteTimeUtc(ruta); }
                RocolaOs.Apunta("spotify_catalogo", "Spotify renovo la autorizacion de la cuenta: guardada en " + Archivo);
            }
            catch (Exception ex) { RocolaOs.Apunta("spotify_catalogo", "no se pudo guardar la autorizacion renovada (" + ex.GetType().Name + ")"); }
        }

        // ---------- leer lo que contesta ----------
        private static string Texto(JsonElement e, string nombre)
        {
            JsonElement v;
            return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nombre, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        private static int Entero(JsonElement e, string nombre)
        {
            JsonElement v; int n;
            return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nombre, out v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out n) ? n : 0;
        }
        private static string Artistas(JsonElement e)
        {
            JsonElement a;
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("artists", out a) || a.ValueKind != JsonValueKind.Array) return "";
            StringBuilder sb = new StringBuilder();
            foreach (JsonElement x in a.EnumerateArray()) { string n = Texto(x, "name"); if (string.IsNullOrEmpty(n)) continue; if (sb.Length > 0) sb.Append(", "); sb.Append(n); }
            return sb.ToString();
        }
        // la imagen mas cercana a 300 px (la de 300 de Spotify; lo que XP pinta en el cubo grande cabe)
        private static string Portada(JsonElement e)
        {
            JsonElement im;
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("images", out im) || im.ValueKind != JsonValueKind.Array) return null;
            string mejor = null; int dist = int.MaxValue;
            foreach (JsonElement x in im.EnumerateArray())
            {
                string u = Texto(x, "url");
                if (string.IsNullOrEmpty(u)) continue;
                int w = Entero(x, "width");
                int d = w <= 0 ? 1000 : Math.Abs(w - 300) + (w < 300 ? 200 : 0);
                if (d < dist) { dist = d; mejor = u; }
            }
            return mejor;
        }
        private static bool SePuede(JsonElement t)
        {
            JsonElement v;
            if (t.TryGetProperty("is_playable", out v) && v.ValueKind == JsonValueKind.False) return false;
            if (t.TryGetProperty("is_local", out v) && v.ValueKind == JsonValueKind.True) return false;
            string tipo = Texto(t, "type");
            return tipo == null || tipo == "track";
        }
        private static RocolaOsSpotifyCatalogoDisco LeeDisco(JsonElement a, string tipo)
        {
            string id = Texto(a, "id");
            if (string.IsNullOrEmpty(id)) return null;
            return new RocolaOsSpotifyCatalogoDisco { Id = id, Uri = Texto(a, "uri") ?? ("spotify:album:" + id), Nombre = Texto(a, "name") ?? "", Artista = Artistas(a), PortadaUrl = Portada(a), Tipo = tipo ?? "album" };
        }
        private static RocolaOsSpotifyCatalogoCancion LeeCancion(JsonElement t, RocolaOsSpotifyCatalogoDisco disco)
        {
            if (t.ValueKind != JsonValueKind.Object || !SePuede(t)) return null;
            string id = Texto(t, "id");
            if (string.IsNullOrEmpty(id)) return null;
            RocolaOsSpotifyCatalogoCancion c = new RocolaOsSpotifyCatalogoCancion
            {
                Id = id, Uri = Texto(t, "uri") ?? ("spotify:track:" + id), Titulo = Texto(t, "name") ?? "", Artista = Artistas(t),
                DuracionMs = Entero(t, "duration_ms"), Numero = Entero(t, "track_number"),
            };
            JsonElement al;
            if (t.TryGetProperty("album", out al) && al.ValueKind == JsonValueKind.Object)
            { c.Disco = Texto(al, "name") ?? ""; c.DiscoId = Texto(al, "id"); c.DiscoUri = Texto(al, "uri"); c.PortadaUrl = Portada(al); }
            else if (disco != null) { c.Disco = disco.Nombre; c.DiscoId = disco.Id; c.DiscoUri = disco.Uri; c.PortadaUrl = disco.PortadaUrl; }
            if (!c.Uri.StartsWith("spotify:track:", StringComparison.Ordinal)) return null;
            return c;
        }
        private static void LeeCancionesDeDisco(JsonElement paginas, RocolaOsSpotifyCatalogoDisco disco)
        {
            JsonElement items;
            if (!paginas.TryGetProperty("items", out items) || items.ValueKind != JsonValueKind.Array) return;
            foreach (JsonElement t in items.EnumerateArray()) { RocolaOsSpotifyCatalogoCancion c = LeeCancion(t, disco); if (c != null) disco.Canciones.Add(c); }
        }
        // las paginas de me/tracks y de las listas: cada renglon trae la cancion en «track» (me/tracks) o «item» (listas, desde 02-2026)
        private static void LeeItems(JsonElement raiz, List<RocolaOsSpotifyCatalogoCancion> l)
        {
            JsonElement items;
            if (!raiz.TryGetProperty("items", out items) || items.ValueKind != JsonValueKind.Array) return;
            int n = 0;
            foreach (JsonElement it in items.EnumerateArray())
            {
                if (it.ValueKind != JsonValueKind.Object) continue;
                JsonElement t;
                if (!it.TryGetProperty("track", out t) && !it.TryGetProperty("item", out t)) continue;
                RocolaOsSpotifyCatalogoCancion c = LeeCancion(t, null);
                if (c == null) continue;
                c.Numero = ++n;
                l.Add(c);
            }
        }
        private static RocolaOsSpotifyCatalogoBusqueda LeeBusqueda(string consulta, JsonElement r)
        {
            RocolaOsSpotifyCatalogoBusqueda b = new RocolaOsSpotifyCatalogoBusqueda { Consulta = consulta };
            JsonElement s, items;
            if (r.TryGetProperty("tracks", out s) && s.ValueKind == JsonValueKind.Object && s.TryGetProperty("items", out items) && items.ValueKind == JsonValueKind.Array)
                foreach (JsonElement t in items.EnumerateArray()) { RocolaOsSpotifyCatalogoCancion c = LeeCancion(t, null); if (c != null) b.Canciones.Add(c); }
            if (r.TryGetProperty("albums", out s) && s.ValueKind == JsonValueKind.Object && s.TryGetProperty("items", out items) && items.ValueKind == JsonValueKind.Array)
                foreach (JsonElement a in items.EnumerateArray()) { if (a.ValueKind != JsonValueKind.Object) continue; RocolaOsSpotifyCatalogoDisco d = LeeDisco(a, null); if (d != null) b.Discos.Add(d); }
            if (r.TryGetProperty("artists", out s) && s.ValueKind == JsonValueKind.Object && s.TryGetProperty("items", out items) && items.ValueKind == JsonValueKind.Array)
                foreach (JsonElement a in items.EnumerateArray())
                {
                    if (a.ValueKind != JsonValueKind.Object) continue;
                    string id = Texto(a, "id");
                    if (string.IsNullOrEmpty(id)) continue;
                    b.Artistas.Add(new RocolaOsSpotifyCatalogoArtista { Id = id, Uri = Texto(a, "uri"), Nombre = Texto(a, "name") ?? "", PortadaUrl = Portada(a) });
                }
            return b;
        }

        // ---------- el estado (/run/rocola-xp/spotify/catalogo: sin secretos) ----------
        private static long estadoEscrito = long.MinValue / 2;
        internal static void EscribeEstado()
        {
            try
            {
                long ahora = Environment.TickCount64;
                long hasta = Volatile.Read(ref frenaHastaMs);
                StringBuilder sb = new StringBuilder();
                sb.Append("api=").Append(Frena ? "frena" : api).Append('\n');
                sb.Append("llave=").Append(HayLlave ? "si" : "no").Append('\n');
                sb.Append("cuenta=").Append(HayCuenta ? "si" : "no").Append('\n');
                sb.Append("peticiones=").Append(Peticiones.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("busquedas=").Append(Interlocked.Read(ref peticionesBusqueda).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("discos=").Append(Interlocked.Read(ref peticionesDisco).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("artistas=").Append(Interlocked.Read(ref peticionesArtista).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("cuenta_peticiones=").Append(Interlocked.Read(ref peticionesCuenta).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("llaves=").Append(Interlocked.Read(ref peticionesLlave).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("frenadas=").Append(Interlocked.Read(ref frenadas).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("frena_s=").Append((hasta > ahora ? (hasta - ahora + 999) / 1000 : 0).ToString(CultureInfo.InvariantCulture)).Append(frenaRazon.Length > 0 && hasta > ahora ? " cuota" : "").Append('\n');
                sb.Append("fallos=").Append(Interlocked.Read(ref fallos).ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("ultimo_codigo=").Append(ultimoCodigo.ToString(CultureInfo.InvariantCulture)).Append('\n');
                string d = RocolaOs.Run("rocola-xp/spotify");
                Directory.CreateDirectory(d);
                string ruta = Path.Combine(d, "catalogo"), t = ruta + ".t";
                File.WriteAllText(t, sb.ToString());
                File.Move(t, ruta, true);
                estadoEscrito = ahora;
            }
            catch { }
        }
    }
}

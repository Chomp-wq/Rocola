// BUSCAR UNA CANCION POR SU LETRA EN GENIUS (fase 11, grupo letras, L1; FASE11_letras.md). Funcion «busca_letra», APAGADA de fabrica: la
// enciende el dueno («busca_letra=1» en rocola_os.txt; las condiciones de Genius son para uso no comercial: lo valora el). Necesita la llave
// de Genius en rocola_genius.txt (la carpeta de datos de rocola-xp, o la de encima, /rocola/datos, donde la dejaba el programa anterior): se
// LEE DONDE ESTA, en el hilo de Genius, cada vez que hace falta, y NUNCA se copia, se apunta, se escribe ni se sube (va solo en la cabecera
// Authorization de la peticion a api.genius.com).
// Como va: una frase de 4 palabras o mas en el buscador de XP; al dejar de escribir 0,8 s se pregunta a Genius (GET /search, en su hilo)
// de que canciones es. Sus tres primeras (sin repetir la misma cancion) van DELANTE de lo de XP, cada una:
//   1. TU CANCION, si la tienes: el mismo titulo como suena (sin lo de entre parentesis, con poca falta: 1 letra en los de 4 a 6, 2 en los
//      de 7 o mas) y el artista (alguna de sus palabras en el artista de tu cancion o de su disco). Es la tarjeta de XP de esa cancion.
//   2. si no, LA DE SPOTIFY (con el modo Spotify y su llave): una busqueda «titulo artista» por el cliente del catalogo
//      (RocolaOs.SpotifyCatalogoApi.cs, la misma de lo escrito), y la primera cancion con ese titulo; su tarjeta es la de Spotify del buscador
//      (g40/g41: elegirla la pide por I6, con sus creditos).
//   3. si no, UNA TARJETA «NO ESTA EN LA ROCOLA»: la tarjeta de cancion del buscador de XP con el titulo que dice Genius, y debajo el artista
//      y «NO ESTA EN LA ROCOLA» (el renglon del disco de XP). Nada nuevo que dibujar: un disco de mentira, sin portada (XP pone las iniciales).
//      Elegirla solo lo dice con el aviso del buscador de XP (g111).
// NADA SE GUARDA (en memoria, la sesion; y como mucho 300 preguntas a Genius por sesion). Sin llave, sin red o con Genius caido: nada (el
// buscador de XP tal cual). Apagada: ni el hilo, ni una peticion, ni se mira la llave.
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
    public partial class Form1
    {
        // LO QUE DIJO GENIUS DE UNA FRASE, ya llevado a la rocola (en la pantalla: SearchItem de XP), por la frase normalizada
        private Dictionary<string, List<SearchItem>> rocolaOsGeniusHechas;

        // g110 (por RocolaOsLetrasResultados): lo de Genius DELANTE; y se pide lo escrito (a los 0,8 s de dejar de escribir lo pregunta el hilo)
        private int RocolaOsGeniusDelante(string q, List<SearchItem> res)
        {
            if (!RocolaOsGenius.HayLlave) return 0;
            List<SearchItem> ya = null;
            if (rocolaOsGeniusHechas != null) rocolaOsGeniusHechas.TryGetValue(q, out ya);
            if (ya == null) { if (RocolaOsGenius.Quiere(q)) RocolaOsGenius.Pide(q, RocolaOsGeniusFoto()); return 0; }
            int n = 0;
            for (int i = ya.Count - 1; i >= 0; i--)
            {
                SearchItem it = ya[i];
                if (res.Remove(it)) res.Insert(0, it);   // (una tuya que XP ya daba: sube, sin contarla otra vez)
                else { res.Insert(0, it); n++; }
            }
            return n;
        }
        // las canciones de la biblioteca (una copia de la lista, en la pantalla: el hilo solo lee sus textos)
        private List<SearchItem> RocolaOsGeniusFoto()
        {
            if (libBuilding) return new List<SearchItem>();
            List<SearchItem> f = new List<SearchItem>(searchIndex.Count);
            for (int i = 0; i < searchIndex.Count; i++) { SearchItem si = searchIndex[i]; if (si != null && si.Kind == 2 && si.TrackRef != null) f.Add(si); }
            return f;
        }

        // LLEGO LO DE GENIUS (encargo «RocolaOs:busca_letra:llego», en la pantalla): las tarjetas, y si se sigue mirando esa frase, la lista otra vez
        private void RocolaOsGeniusLlego(string q, List<RocolaOsGenius.Hallada> hs)
        {
            if (rocolaOsGeniusHechas == null) rocolaOsGeniusHechas = new Dictionary<string, List<SearchItem>>(StringComparer.Ordinal);
            if (rocolaOsGeniusHechas.Count > 40) rocolaOsGeniusHechas.Clear();   // (la sesion: no crece sin fin)
            List<SearchItem> l = new List<SearchItem>();
            HashSet<string> vistas = new HashSet<string>(StringComparer.Ordinal);
            foreach (RocolaOsGenius.Hallada h in hs ?? new List<RocolaOsGenius.Hallada>())
            {
                if (l.Count >= 3) break;
                if (h.Tuya != null) { if (vistas.Add("ruta|" + h.Tuya.TrackRef.Path)) l.Add(h.Tuya); continue; }
                if (h.Spotify != null && RocolaOsSpotifyCatalogoVa)
                {
                    RocolaOsSpotifyCatalogoCancion c = h.Spotify;
                    if (!vistas.Add("ruta|" + c.Uri)) continue;
                    Album a = RocolaOsSpotifyCatalogoDiscoDe(c);
                    Track t = RocolaOsSpotifyCatalogoPista(c);
                    l.Add(new SearchItem { Kind = 2, Display = (c.Artista.Length > 0 ? c.Artista + " - " : "") + c.Titulo, Key = "", AlbumRef = a, TrackRef = t });
                    continue;
                }
                l.Add(new SearchItem { Kind = 2, Display = h.Titulo, Key = "", AlbumRef = RocolaOsGenius.NoEsta(h.Artista) });
            }
            rocolaOsGeniusHechas[q] = l;
            RocolaOs.Apunta("busca_letra", "Genius: " + hs.Count.ToString(CultureInfo.InvariantCulture) + " de la frase (" + l.Count.ToString(CultureInfo.InvariantCulture) + " tarjetas delante)");
            if (l.Count == 0 || !searchOpen || searchOverlay == null || MotorBusqueda.Normaliza(searchOverlay.Query) != q) return;
            List<SearchItem> antes = searchOverlay.Results;
            SearchItem sel = searchOverlay.FocusResults && antes != null && searchOverlay.SelResult >= 0 && searchOverlay.SelResult < antes.Count ? antes[searchOverlay.SelResult] : null;
            RecomputeResults(searchOverlay.Query);
            if (sel != null && searchOverlay.Results != null) { int n = searchOverlay.Results.IndexOf(sel); if (n >= 0) searchOverlay.SelResult = n; }
            searchOverlay.DatosVer++;
            searchOverlay.Invalidate();
        }

        // ================================================================= el hilo de Genius ============================================
        private static class RocolaOsGenius
        {
            internal const string ArchivoLlave = "rocola_genius.txt";
            internal const int EsperaMs = 800;            // dejar de escribir
            internal const int TopeSesion = 300;          // preguntas a Genius por sesion, como mucho
            internal sealed class Hallada { internal string Titulo, Artista; internal SearchItem Tuya; internal RocolaOsSpotifyCatalogoCancion Spotify; }
            private sealed class Pedido { internal string Q; internal long Desde; internal List<SearchItem> Foto; }
            private static Pedido pendiente;
            private static readonly HashSet<string> preguntadas = new HashSet<string>(StringComparer.Ordinal);   // (en la sesion: ni una repetida)
            private static readonly object cerrojo = new object();
            private static readonly AutoResetEvent despierta = new AutoResetEvent(false);
            private static Thread hilo;
            private static volatile bool parar, hayLlave;
            private static long peticiones;
            private static string estado = "sin_mirar";
            private static HttpClient http;
            internal static bool HayLlave { get { return hayLlave; } }
            internal static long Peticiones { get { return Interlocked.Read(ref peticiones); } }
            internal static string Estado { get { return Volatile.Read(ref estado); } }

            // al arrancar la funcion encendida (en la pantalla): el hilo, que mira si hay llave
            internal static void Arranca()
            {
                lock (cerrojo) { if (hilo == null) hilo = RocolaOs.Hilo("busca_letra", Bucle); }
            }
            internal static void Para() { parar = true; despierta.Set(); }
            // si esa frase aun no se pregunto ni espera (para no copiar la biblioteca en balde)
            internal static bool Quiere(string q)
            {
                lock (cerrojo) return !preguntadas.Contains(q) && (pendiente == null || pendiente.Q != q);
            }
            // lo escrito (desde la pantalla): reemplaza lo que esperaba
            internal static void Pide(string q, List<SearchItem> foto)
            {
                lock (cerrojo)
                {
                    if (preguntadas.Contains(q)) return;
                    if (pendiente != null && pendiente.Q == q) return;
                    pendiente = new Pedido { Q = q, Desde = Environment.TickCount64, Foto = foto };
                }
                despierta.Set();
            }

            private static void Bucle()
            {
                long llaveMirada = long.MinValue / 2;
                while (!parar)
                {
                    long ahora = Environment.TickCount64;
                    if (ahora - llaveMirada >= 60000) { llaveMirada = ahora; MiraLlave(); }
                    Pedido p;
                    lock (cerrojo) p = pendiente;
                    if (p == null) { despierta.WaitOne(60000); continue; }
                    long falta = p.Desde + EsperaMs - Environment.TickCount64;
                    if (falta > 0) { despierta.WaitOne((int)Math.Min(falta, 60000)); continue; }
                    lock (cerrojo)
                    {
                        if (pendiente != p) continue;   // (se escribio otra cosa mientras)
                        pendiente = null;
                        if (!preguntadas.Add(p.Q)) continue;
                    }
                    if (!hayLlave) continue;
                    if (Peticiones >= TopeSesion) { Volatile.Write(ref estado, "tope"); continue; }
                    List<Hallada> hs = Pregunta(p);
                    if (hs == null) continue;   // (sin respuesta: nada, y esa frase ya no se vuelve a preguntar en la sesion)
                    string q = p.Q;
                    Form1 r = RocolaOs.LaRocola;
                    if (r != null) RocolaOs.Encarga("busca_letra:llego", delegate { r.RocolaOsGeniusLlego(q, hs); });
                }
            }

            // LA LLAVE (en este hilo): si esta y parece una (no se guarda el texto: se vuelve a leer al preguntar)
            private static string RutaLlave()
            {
                string a = RocolaOs.Datos(ArchivoLlave);
                if (File.Exists(a)) return a;
                try
                {
                    string d = Path.GetDirectoryName(Path.GetFullPath(Form1.CarpetaDatos).TrimEnd('/'));
                    if (d != null && string.Equals(Path.GetFileName(Path.GetFullPath(Form1.CarpetaDatos).TrimEnd('/')), "xp", StringComparison.Ordinal))
                    {
                        string b = Path.Combine(d, ArchivoLlave);
                        if (File.Exists(b)) return b;
                    }
                }
                catch { }
                return null;
            }
            private static string LeeLlave()
            {
                try
                {
                    string r = RutaLlave();
                    if (r == null) return null;
                    string l = File.ReadAllText(r).Trim();
                    int n = l.IndexOfAny(new char[] { '\r', '\n' });
                    if (n >= 0) l = l.Substring(0, n).Trim();
                    return l.Length >= 10 && l.IndexOf(' ') < 0 ? l : null;
                }
                catch { return null; }
            }
            private static void MiraLlave()
            {
                bool hay = LeeLlave() != null;
                if (hay != hayLlave) RocolaOs.Apunta("busca_letra", hay ? "con la llave de Genius" : "sin la llave de Genius (" + ArchivoLlave + "): no se pregunta nada");
                hayLlave = hay;
                if (Volatile.Read(ref estado) == "sin_mirar" || Volatile.Read(ref estado) == "sin_llave" || Volatile.Read(ref estado) == "lista")
                    Volatile.Write(ref estado, hay ? "lista" : "sin_llave");
            }

            private static HttpClient Http(string api)
            {
                if (http != null) return http;
                bool local = api.StartsWith("http://127.0.0.1", StringComparison.Ordinal);   // (las pruebas: sin el proxy del entorno)
                SocketsHttpHandler h = new SocketsHttpHandler { UseProxy = !local, ConnectTimeout = TimeSpan.FromSeconds(6), AutomaticDecompression = DecompressionMethods.All };
                HttpClient c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(10) };
                c.DefaultRequestHeaders.UserAgent.ParseAdd("rocola-xp/1");
                http = c;
                return c;
            }
            private static string Api
            {
                get
                {
                    string e = Environment.GetEnvironmentVariable("ROCOLA_GENIUS_API");   // (las pruebas: un Genius falso)
                    return string.IsNullOrEmpty(e) ? "https://api.genius.com" : e.TrimEnd('/');
                }
            }

            // UNA PREGUNTA A GENIUS Y LO QUE ES CADA UNA (tuya, de Spotify o ninguna). null si no contesto.
            private static List<Hallada> Pregunta(Pedido p)
            {
                string llave = LeeLlave();
                if (llave == null) { hayLlave = false; Volatile.Write(ref estado, "sin_llave"); return null; }
                string api = Api, cuerpo;
                try
                {
                    HttpRequestMessage m = new HttpRequestMessage(HttpMethod.Get, api + "/search?per_page=5&q=" + Uri.EscapeDataString(p.Q));
                    m.Headers.TryAddWithoutValidation("Authorization", "Bearer " + llave);
                    llave = null;
                    Interlocked.Increment(ref peticiones);
                    using (HttpResponseMessage r = Http(api).Send(m))
                    {
                        int cod = (int)r.StatusCode;
                        if (cod != 200)
                        {
                            Volatile.Write(ref estado, "contesto_" + cod.ToString(CultureInfo.InvariantCulture));
                            RocolaOs.Apunta("busca_letra", "Genius contesto " + cod.ToString(CultureInfo.InvariantCulture) + (cod == 401 ? " (la llave no vale)" : ""));
                            return null;
                        }
                        using (StreamReader sr = new StreamReader(r.Content.ReadAsStream(), Encoding.UTF8)) cuerpo = sr.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    Volatile.Write(ref estado, "sin_red");
                    RocolaOs.Apunta("busca_letra", "Genius no contesto (" + ex.GetType().Name + ")");
                    return null;
                }
                Volatile.Write(ref estado, "lista");
                List<Hallada> hs = new List<Hallada>();
                HashSet<string> vistas = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    using (JsonDocument d = JsonDocument.Parse(cuerpo))
                    {
                        JsonElement resp, hits;
                        if (!d.RootElement.TryGetProperty("response", out resp) || !resp.TryGetProperty("hits", out hits) || hits.ValueKind != JsonValueKind.Array) return hs;
                        foreach (JsonElement h in hits.EnumerateArray())
                        {
                            if (hs.Count >= 3) break;
                            JsonElement res, pa;
                            if (h.ValueKind != JsonValueKind.Object || !h.TryGetProperty("result", out res) || res.ValueKind != JsonValueKind.Object) continue;
                            string titulo = Texto(res, "title"), artista = res.TryGetProperty("primary_artist", out pa) && pa.ValueKind == JsonValueKind.Object ? Texto(pa, "name") : "";
                            if (titulo.Length == 0 || !vistas.Add(MotorBusqueda.Normaliza(SinParentesis(titulo)) + "|" + MotorBusqueda.Normaliza(artista))) continue;   // («Querida» y «Querida (En Vivo)»: una)
                            hs.Add(new Hallada { Titulo = titulo, Artista = artista });
                        }
                    }
                }
                catch (Exception ex) { RocolaOs.Apunta("busca_letra", "Genius contesto algo raro (" + ex.GetType().Name + ")"); return null; }
                foreach (Hallada h in hs)
                {
                    h.Tuya = TuCancion(p.Foto, h.Titulo, h.Artista);
                    if (h.Tuya == null) h.Spotify = DeSpotify(h.Titulo, h.Artista);
                }
                return hs;
            }
            private static string Texto(JsonElement e, string k)
            {
                JsonElement v;
                return e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
            }
            internal static string SinParentesis(string s)
            {
                StringBuilder b = new StringBuilder(); int p = 0;
                foreach (char c in s ?? "") { if (c == '(' || c == '[') p++; else if (c == ')' || c == ']') { if (p > 0) p--; } else if (p == 0) b.Append(c); }
                return b.ToString();
            }
            private static int Tolerancia(int largo) { return largo >= 7 ? 2 : largo >= 4 ? 1 : 0; }
            // EL MISMO TITULO COMO SUENA: normalizado como el buscador de XP, sin espacios ni lo de entre parentesis, con poca falta
            internal static bool MismoTitulo(string a, string b)
            {
                string x = MotorBusqueda.Normaliza(SinParentesis(a)).Replace(" ", ""), y = MotorBusqueda.Normaliza(SinParentesis(b)).Replace(" ", "");
                if (x.Length == 0 || y.Length == 0) return false;
                if (x == y) return true;
                int tol = Tolerancia(x.Length);
                return tol > 0 && RocolaOsSpotifyDistancia(x, y, tol) <= tol;
            }
            private static SearchItem TuCancion(List<SearchItem> foto, string titulo, string artista)
            {
                if (foto == null) return null;
                List<string> fa = new List<string>();
                foreach (string w in MotorBusqueda.Normaliza(artista).Split(' ')) if (!RocolaOsLetrasMp3.EsVacia(w)) fa.Add(w);
                foreach (SearchItem si in foto)
                {
                    Track t = si.TrackRef;
                    if (t == null || !MismoTitulo(titulo, TituloSinPista(t.Title ?? ""))) continue;
                    if (fa.Count == 0) return si;
                    string suyo = " " + MotorBusqueda.Normaliza((t.Artist ?? "") + " " + (si.AlbumRef != null ? si.AlbumRef.Artist ?? "" : "")) + " ";
                    foreach (string w in fa) if (suyo.Contains(" " + w + " ", StringComparison.Ordinal)) return si;
                }
                return null;
            }
            // LA DE SPOTIFY (con el modo y su llave): la busqueda «titulo artista» del cliente del catalogo; la primera con ese titulo
            private static RocolaOsSpotifyCatalogoCancion DeSpotify(string titulo, string artista)
            {
                if (!RocolaOs.Encendida("spotify") || !RocolaOsSpotifyCatalogoApi.HayLlave) return null;
                string q = RocolaOsSpotifyCatalogoClave(SinParentesis(titulo) + " " + artista);
                RocolaOsSpotifyCatalogoBusqueda b = RocolaOsSpotifyCatalogoApi.BuscaYa(q);
                if (b == null)
                {
                    ManualResetEventSlim listo = new ManualResetEventSlim(false);
                    RocolaOsSpotifyCatalogoBusqueda llego = null;
                    RocolaOsSpotifyCatalogoApi.Busca(q, delegate (object r, string error) { llego = r as RocolaOsSpotifyCatalogoBusqueda; listo.Set(); });
                    listo.Wait(10000);
                    b = llego;
                }
                if (b == null || b.Canciones == null) return null;
                foreach (RocolaOsSpotifyCatalogoCancion c in b.Canciones)
                    if (c != null && !string.IsNullOrEmpty(c.Uri) && MismoTitulo(titulo, c.Titulo ?? "")) return c;
                return null;
            }

            // ---------- la tarjeta «NO ESTA EN LA ROCOLA» ----------
            internal const string CarpetaNoEsta = "/::LETRA::/";   // (absoluta y sin palabras: XP adivina el genero por la carpeta)
            internal const string NombreNoEsta = "NO ESTA EN LA ROCOLA";
            internal static Album NoEsta(string artista)
            {
                uint h = 2166136261; foreach (char ch in artista ?? "") { h ^= ch; h *= 16777619; }
                return new Album { Folder = CarpetaNoEsta + h.ToString("x8", CultureInfo.InvariantCulture), Name = NombreNoEsta, Artist = artista ?? "", Letter = '#' };
            }
            internal static bool EsNoEsta(Album a) { return a != null && a.Folder != null && a.Folder.StartsWith(CarpetaNoEsta, StringComparison.Ordinal); }
        }
    }
}

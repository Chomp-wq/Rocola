// EL CATALOGO DE SPOTIFY EN LAS PANTALLAS DE XP (fase 11, grupo catalogo; FASE11_catalogo.md). Una parte de Form1: lo de Spotify se busca,
// se ve y se pide EN LA ROCOLA, con lo que XP ya dibuja (regla R8 del dueno del 14-11-2026: nada de estilos ni elementos nuevos, solo el
// contenido de Spotify metido en los elementos de XP):
//   - EL BUSCADOR: debajo de las primeras de XP, las canciones, discos y artistas de Spotify como resultados mas de la MISMA lista (un
//     SearchItem de XP: su tarjeta, su portada, su cubo). Elegir una cancion la pide por I6 (RocolaOsSpotifyPide, del nucleo); un disco lo
//     abre en la vista de disco de XP; un artista abre SU PARED (RocolaOs.SpotifyCatalogoParedes.cs, grupo paredes-buscador).
//   - LA PARED: una letra mas al final de la barra (U+266B, la nota doble del juego de simbolos de Windows XP, WGL4; la cabecera dice
//     «SPOTIFY»), con portadas de discos de Spotify: lo de la cuenta (si hay refresh_token: «Tus favoritas», tus discos y tus listas) y
//     los discos abiertos o pedidos en esta sesion. Abrir uno es la vista de disco de XP; PONER pide por I6.
//   - LAS PORTADAS se bajan a /run/rocola-xp/spotify/portadas/<id>.jpg al salir en pantalla (nunca al disco), y XP las lee como las suyas;
//     su miniatura tambien va a /run (gancho g43), no a rocola_miniaturas.
// Las de Spotify NO entran en la biblioteca de XP (albums, byLetter, searchIndex): son Album y Track de XP que viven aqui, con la carpeta
// «/::SPOTIFY::/...» y la ruta «spotify:track:ID» (la Uri de I6), y entran a XP solo por los ganchos g40-g50 y por I6.
//
// SIN EL MODO (spotify=0, o sin ROCOLA_OS) cada gancho devuelve lo de XP tal cual: ni letra, ni resultados, ni peticiones a la red.
// Las reglas de las partes de Form1 (PLAN_FASE11.md §2.2): nombres RocolaOs..., sin inicializadores de campo con efectos ni constructores
// estaticos que trabajen. Todo lo de aqui corre en el hilo de la pantalla (los ganchos y los encargos «RocolaOs:spotify_catalogo:...»); la
// red, en los hilos del cliente (RocolaOs.SpotifyCatalogoApi.cs) y de las portadas (abajo).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using SkiaSharp;

namespace Rocola
{
    // lo que se ensena de cada cancion en la lista del disco (el nombre que XP saca del archivo: «NN Titulo»), por su ruta «spotify:track:ID»
    internal static class RocolaOsSpotifyCatalogoNombres
    {
        internal static readonly Dictionary<string, string> PorRuta = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public partial class Form1
    {
        internal const char RocolaOsSpotifyMarca = '♫';      // la letra de Spotify en la barra (WGL4: esta en las letras de XP)
        // la «carpeta» de un disco de Spotify (no existe): absoluta y sin palabras, porque XP adivina el genero de un disco por el nombre de
        // su carpeta y de la de encima (NombreDice, Form1.Biblioteca.cs); relativa, la de encima seria el directorio actual
        internal const string RocolaOsSpotifyCarpeta = "/::SPOTIFY::/";
        private const int RocolaOsSpotifyXpArriba = 8;            // cuantas de XP van antes de las de Spotify en el buscador
        private const int RocolaOsSpotifyRecientesMax = 40;

        // lo de Spotify que vive aqui (se crea al encender, la primera vez; null sin el modo)
        private Dictionary<string, Album> rocolaOsSpDiscos;                                   // por «tipo:id»
        private Dictionary<string, RocolaOsSpotifyCatalogoCancion> rocolaOsSpCanciones;       // por uri
        private Dictionary<Album, RocolaOsSpotifyCatalogoDisco> rocolaOsSpDeDisco;            // de que disco de Spotify es cada Album
        private Dictionary<Album, RocolaOsSpotifyCatalogoArtista> rocolaOsSpDeArtista;        // (los «discos» de los artistas del buscador)
        private List<Album> rocolaOsSpCuenta, rocolaOsSpRecientes, rocolaOsSpLetra;
        private List<SearchItem> rocolaOsSpXp;      // lo ultimo que dio XP en el buscador (para mezclar cuando llega lo de Spotify)
        private string rocolaOsSpConsulta;           // y su consulta (la clave)
        private string rocolaOsSpEscrito;            // (y tal cual se escribio: para la cuenta de XP)
        // (23-11-2026, «en el buscador cargan primero los locales y luego los de Spotify y se mueve todo») mientras se espera a Spotify se SIGUE
        // viendo lo de antes (lista y casilla) y todo cambia junto al llegar la respuesta, o a los 2 s, o si Spotify falla (g40)
        private const int RocolaOsSpEsperaMs = 2000;
        private bool rocolaOsSpEspera;               // hay lo local nuevo guardado sin mostrar
        private int rocolaOsSpGen;                   // cada g40 lo cambia: un reloj o una respuesta de otra consulta no hace nada
        private System.Threading.Timer rocolaOsSpReloj;
        private List<SearchItem> rocolaOsSpVisto;    // lo ultimo que se ENSENO en el buscador (lista, cuenta y consulta de la casilla)
        private int rocolaOsSpVistoCuenta;
        private string rocolaOsSpVistoConsulta;
        private bool rocolaOsSpCuentaPedida;
        private int rocolaOsSpAbriendo;              // el ultimo «abrir» pedido (si llega otro antes, el viejo no abre nada)
        private Album rocolaOsSpMirado;              // (g52) el disco de Spotify elegido en la pared, desde cuando, y los ya pedidos asi
        private int rocolaOsSpMiradoDesde;
        private HashSet<Album> rocolaOsSpPrevistos;

        // ---------- si va ----------
        // EL MODO SPOTIFY ENCENDIDO (la funcion «spotify» del nucleo, I3): sin ROCOLA_OS o con spotify=0, no (y no se crea nada)
        private bool RocolaOsSpotifyCatalogoVa
        {
            get
            {
                if (!RocolaOs.Activo || !RocolaOs.Encendida("spotify")) return false;
                if (rocolaOsSpDiscos == null) RocolaOsSpotifyCatalogoPrepara();
                return true;
            }
        }
        private void RocolaOsSpotifyCatalogoPrepara()
        {
            rocolaOsSpDiscos = new Dictionary<string, Album>(StringComparer.Ordinal);
            rocolaOsSpCanciones = new Dictionary<string, RocolaOsSpotifyCatalogoCancion>(StringComparer.Ordinal);
            rocolaOsSpDeDisco = new Dictionary<Album, RocolaOsSpotifyCatalogoDisco>();
            rocolaOsSpDeArtista = new Dictionary<Album, RocolaOsSpotifyCatalogoArtista>();
            rocolaOsSpCuenta = new List<Album>(); rocolaOsSpRecientes = new List<Album>(); rocolaOsSpLetra = new List<Album>();
            RocolaOs.Apunta("spotify_catalogo", "encendido: la API web con la app del dueno (" + (RocolaOsSpotifyCatalogoApi.HayLlave ? "con llave" : "SIN llave: " + RocolaOsSpotifyCatalogoApi.Archivo + " sin client_id o client_secret")
                + (RocolaOsSpotifyCatalogoApi.HayCuenta ? ", con la cuenta" : ", sin la cuenta") + ")");
            RocolaOsSpotifyCatalogoApi.PideEstado();   // (lo escribe el hilo del catalogo: en la pantalla, nada de archivos)
        }
        private static bool RocolaOsEsSpotify(Album a) { return a != null && a.Folder != null && a.Folder.StartsWith(RocolaOsSpotifyCarpeta, StringComparison.Ordinal); }
        // (rapido-spotify) EN EL LATIDO DEL NUCLEO (cada segundo, en la pantalla): al abrirse el buscador, calentar la llave y las conexiones
        // (RocolaOsSpotifyCatalogoApi.Calienta: sin peticiones a la API). Sin el modo o sin la llave, nada.
        private bool rocolaOsSpBuscadorAntes;
        private void RocolaOsSpotifyCatalogoLatido()
        {
            bool abierto = searchOpen;
            if (abierto && !rocolaOsSpBuscadorAntes && RocolaOsSpotifyCatalogoVa && RocolaOsSpotifyCatalogoApi.HayLlave) RocolaOsSpotifyCatalogoApi.Calienta();
            if (abierto && !rocolaOsSpBuscadorAntes && RocolaOsSpotifyCatalogoVa) RocolaOsSpotifyCatalogoRecorta();   // (A8: como mucho una vez por hora)
            rocolaOsSpBuscadorAntes = abierto;
            RocolaOsMejoresLatido(abierto);   // («LAS MEJORES CANCIONES DE...»: el prefijo no sobrevive al buscador; RocolaOs.Mejores.cs)
            RocolaOsSpotifyCatalogoMiraTarjeta(abierto);
            RocolaOsParedCasillasAlDia();   // (BUSCAR y AYUDA fuera de las paredes del buscador: RocolaOs.ParedLetrero.cs)
        }
        // (rapido-spotify) LA TARJETA DE SPOTIFY EN QUE SE QUEDA EL BUSCADOR (dos latidos seguidos en la misma: 1 a 2 s), como g52 en la pared: lo
        // que abriria PONER se pide ya, para que al pulsarlo se abra al momento. Un disco: sus canciones (1 peticion, la misma que haria al
        // abrirlo); un artista (tambien uno tuyo con su pared unida) o «Todos los discos de...»: la primera pagina de su pared (1 peticion de 10 y, si hay mas de 10,
        // la 2a: las mismas que al abrirla).
        // Lo contestado se queda en la memoria de la sesion como siempre (abrirlo despues: 0 peticiones). Para la cuota: solo lo que alguien esta
        // mirando, una tarjeta como mucho cada 3 s, cada una una vez, nada con 429 ni sin la llave.
        private SearchItem rocolaOsSpTarjetaMirada;
        private HashSet<Album> rocolaOsSpTarjetasAdelantadas;
        private long rocolaOsSpUltimoAdelantoMs = long.MinValue / 2;
        internal static long RocolaOsSpAdelantadas;
        // (revision) como mucho 3 tarjetas adelantadas cada vez que se abre el buscador (3 a 6 peticiones; abrir una lo pediria igual)
        private const int RocolaOsSpTopeAdelantos = 3;
        private int rocolaOsSpAdelantosBuscador;
        private void RocolaOsSpotifyCatalogoMiraTarjeta(bool abierto)
        {
            SearchItem it = null;
            if (abierto && searchOverlay != null && searchOverlay.FocusResults && rocolaOsSpDiscos != null)
            {
                List<SearchItem> r = searchOverlay.Results;
                int i = searchOverlay.SelResult;
                if (r != null && i >= 0 && i < r.Count) it = r[i];
            }
            // (revision) al cerrarse el buscador se olvida lo adelantado (sin crecer toda la sesion) y vuelve el tope de lo que se adelanta
            if (!abierto && rocolaOsSpTarjetasAdelantadas != null && rocolaOsSpTarjetasAdelantadas.Count > 0) { rocolaOsSpTarjetasAdelantadas.Clear(); rocolaOsSpAdelantosBuscador = 0; }
            if (it == null || !ReferenceEquals(it, rocolaOsSpTarjetaMirada)) { rocolaOsSpTarjetaMirada = it; return; }
            if (rocolaOsSpAdelantosBuscador >= RocolaOsSpTopeAdelantos) return;   // (revision: la cuota; quien baja despacio por la lista no gasta mas)
            Album a = it.AlbumRef;
            string unida = RocolaOsSpotifyUnidaId(it);   // (23-11-2026) tu artista que Spotify tambien tiene: la primera pagina de su pared unida
            if ((!RocolaOsEsSpotify(a) && unida == null) || !RocolaOsSpotifyCatalogoVa || RocolaOsSpotifyCatalogoApi.Frena || RocolaOsSpotifyCatalogoApi.SinRed || !RocolaOsSpotifyCatalogoApi.HayLlave) return;
            if (rocolaOsSpTarjetasAdelantadas == null) rocolaOsSpTarjetasAdelantadas = new HashSet<Album>();
            if (rocolaOsSpTarjetasAdelantadas.Contains(a)) return;
            long ahora = Environment.TickCount64;
            if (ahora - rocolaOsSpUltimoAdelantoMs < 3000) return;
            RocolaOsSpotifyCatalogoDisco d;
            RocolaOsSpotifyCatalogoArtista ar;
            RocolaOsSpotifyParedQue q;
            if (unida != null)
            {
                if (RocolaOsSpotifyCatalogoAdelantaPared(RocolaOsSpotifyCatalogoApi.ParedArtista, unida))
                { rocolaOsSpTarjetasAdelantadas.Add(a); rocolaOsSpUltimoAdelantoMs = ahora; RocolaOsSpAdelantadas++; rocolaOsSpAdelantosBuscador++; }
            }
            else if (it.Kind == 1 && rocolaOsSpDeDisco.TryGetValue(a, out d) && d.Tipo == "album" && d.Canciones == null && a.Tracks.Count == 0)
            {
                rocolaOsSpTarjetasAdelantadas.Add(a); rocolaOsSpUltimoAdelantoMs = ahora; RocolaOsSpAdelantadas++; rocolaOsSpAdelantosBuscador++;
                RocolaOsSpotifyCatalogoApi.AbreDisco(d.Id, delegate (object r, string error)
                {
                    RocolaOs.Encarga("spotify_catalogo:adelanto", delegate { RocolaOsSpotifyCatalogoLlegoPrevisto(a, r as RocolaOsSpotifyCatalogoDisco); });
                });
            }
            else if (rocolaOsSpParedDe != null && rocolaOsSpParedDe.TryGetValue(a, out q) && !string.IsNullOrEmpty(q.De))
            {   // «Todos los discos de «lo escrito»»
                if (RocolaOsSpotifyCatalogoAdelantaPared(q.Tipo == "todos" ? RocolaOsSpotifyCatalogoApi.ParedTodos : RocolaOsSpotifyCatalogoApi.ParedArtista, q.De))
                { rocolaOsSpTarjetasAdelantadas.Add(a); rocolaOsSpUltimoAdelantoMs = ahora; RocolaOsSpAdelantadas++; rocolaOsSpAdelantosBuscador++; }
            }
            else if (it.Kind == 0 && rocolaOsSpDeArtista.TryGetValue(a, out ar) && !string.IsNullOrEmpty(ar.Id))
            {
                if (RocolaOsSpotifyCatalogoAdelantaPared(RocolaOsSpotifyCatalogoApi.ParedArtista, ar.Id))
                { rocolaOsSpTarjetasAdelantadas.Add(a); rocolaOsSpUltimoAdelantoMs = ahora; RocolaOsSpAdelantadas++; rocolaOsSpAdelantosBuscador++; }
            }
        }
        // la primera pagina de una pared del buscador: su 1er bloque de 10 y, si hay mas, el 2o (las 18 casillas de su primera pagina). La respuesta
        // se queda en la memoria del cliente (al abrirla, PaginaYa: 0 peticiones); nada que hacer en la pantalla. false = ya estaba
        private static bool RocolaOsSpotifyCatalogoAdelantaPared(string tipo, string de)
        {
            if (RocolaOsSpotifyCatalogoApi.PaginaYa(tipo, de, 0) != null) return false;
            RocolaOsSpotifyCatalogoApi.PidePagina(tipo, de, 0, delegate (object r, string error)
            {
                RocolaOsSpotifyCatalogoPagina pg = r as RocolaOsSpotifyCatalogoPagina;
                if (pg != null && pg.Total > RocolaOsSpotifyCatalogoApi.PorPaginaPared && pg.Crudos >= RocolaOsSpotifyCatalogoApi.PorPaginaPared && !RocolaOsSpotifyCatalogoApi.Frena)
                    RocolaOsSpotifyCatalogoApi.PidePagina(tipo, de, RocolaOsSpotifyCatalogoApi.PorPaginaPared, null);
            });
            return true;
        }
        private static bool RocolaOsEsSpotify(Track t) { return t != null && t.Path != null && t.Path.StartsWith("spotify:track:", StringComparison.Ordinal); }

        // ================================================================= los ganchos =================================================

        // g40 (Form1.Biblioteca.cs, RecomputeResults: «searchOverlay.Results = res;»): LO DE SPOTIFY EN EL BUSCADOR. Sin el modo, res tal cual.
        // Con el, las de XP siguen igual y en su orden; despues de las primeras 8, lo de Spotify de esa consulta (si ya se tiene: si no, se
        // pide a los 300 ms y se mete al llegar, con la misma consulta) y luego el resto de XP. Con menos de 3 letras no se pregunta (cuota).
        private List<SearchItem> RocolaOsSpotifyCatalogoResultados(string consulta, List<SearchItem> res)
        {
            if (!RocolaOsSpotifyCatalogoVa) return res;
            string q = RocolaOsSpotifyCatalogoClave(consulta);
            rocolaOsSpXp = res; rocolaOsSpConsulta = q; rocolaOsSpEscrito = consulta;
            rocolaOsSpEspera = false; rocolaOsSpGen++;
            if (rocolaOsSpReloj != null) { try { rocolaOsSpReloj.Dispose(); } catch { } rocolaOsSpReloj = null; }
            if (q.Length < 3 || !RocolaOsSpotifyCatalogoApi.HayLlave) { RocolaOsSpotifyCatalogoApi.OlvidaBusqueda(); return RocolaOsSpotifyCatalogoVisto(res, consulta); }
            RocolaOsSpotifyCatalogoBusqueda b = RocolaOsSpotifyCatalogoApi.BuscaYa(q);
            if (b != null)
            {
                // (revision del asistente) lo contestado de memoria (lo mismo escrito otra vez, o AL VOLVER de una pared o de un disco) tambien
                // cuenta en la casilla: XP acaba de poner solo los suyos y, con todo de Spotify, decia «Sin resultados» encima de las tarjetas
                List<SearchItem> m = RocolaOsSpotifyCatalogoMezcla(res, b);
                CuentaResultados(consulta, (lastMatches != null ? lastMatches.Count : 0) + rocolaOsLetrasMetidas + m.Count - res.Count);   // (letras: + las de la letra que g114 metio en res)
                return RocolaOsSpotifyCatalogoVisto(m, consulta);
            }
            RocolaOsSpotifyCatalogoApi.Busca(q, delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:resultados", delegate { RocolaOsSpotifyCatalogoLlegoBusqueda(q, r as RocolaOsSpotifyCatalogoBusqueda, error); });
            });
            // (23-11-2026, «saldra cargando... y un anillo de carga») con el buscador de portadas la espera NO ensena lo de antes: una lista vacia, el
            // anillo y «Buscando…» en la casilla (RocolaOs.BuscadorCargando.cs); con el otro buscador, lo de antes como siempre
            bool anillo = searchOverlay.Nuevo;
            if (RocolaOsSpotifyCatalogoApi.Frena || RocolaOsSpotifyCatalogoApi.SinRed || (!anillo && (rocolaOsSpVisto == null || !object.ReferenceEquals(searchOverlay.Results, rocolaOsSpVisto))))
                return RocolaOsSpotifyCatalogoVisto(res, consulta);   // (sin tope que esperar, o nada de antes que seguir ensenando: lo local ya)
            // lo local nuevo espera a Spotify: se sigue viendo lo de antes (y su cuenta, que XP ya cambio) hasta que llegue o pasen 2 s
            rocolaOsSpEspera = true;
            if (!anillo) CuentaResultados(rocolaOsSpVistoConsulta, rocolaOsSpVistoCuenta);
            int gen = rocolaOsSpGen;
            rocolaOsSpReloj = new System.Threading.Timer(delegate
            {
                RocolaOs.Encarga("spotify_catalogo:espera", delegate { RocolaOsSpotifyCatalogoSueltaLocal(gen); });
            }, null, RocolaOsSpEsperaMs, System.Threading.Timeout.Infinite);
            return anillo ? searchOverlay.RocolaOsListaEspera() : rocolaOsSpVisto;
        }
        // lo que se ensena ahora en el buscador (para seguir ensenandolo mientras se espera a Spotify)
        private List<SearchItem> RocolaOsSpotifyCatalogoVisto(List<SearchItem> lista, string consulta)
        {
            lista = RocolaOsMejoresPrimera(lista, consulta);   // («LAS MEJORES CANCIONES DE ARTISTA» de primera con el prefijo puesto: RocolaOs.Mejores.cs; sin prefijo, la misma)
            rocolaOsSpVisto = lista; rocolaOsSpVistoConsulta = consulta;
            rocolaOsSpVistoCuenta = searchOverlay != null ? searchOverlay.TotalResultados : 0;   // (la cuenta ya puesta por XP; la de lo mezclado se pone antes de llamar)
            return lista;
        }
        // pasaron los 2 s (o Spotify fallo): lo local nuevo solo; si Spotify llega despues, se mezcla como siempre
        private void RocolaOsSpotifyCatalogoSueltaLocal(int gen)
        {
            if (!rocolaOsSpEspera || gen != rocolaOsSpGen) return;
            rocolaOsSpEspera = false;
            if (!searchOpen || rocolaOsSpXp == null) return;
            RocolaOsSpotifyCatalogoPoneResultados(rocolaOsSpXp, (lastMatches != null ? lastMatches.Count : 0) + rocolaOsLetrasMetidas);
        }
        // cambia la lista del buscador sin perder la tarjeta elegida si alguien andaba entre los resultados
        private void RocolaOsSpotifyCatalogoPoneResultados(List<SearchItem> m, int cuenta)
        {
            m = RocolaOsMejoresPrimera(m, rocolaOsSpEscrito);   // («LAS MEJORES CANCIONES DE ARTISTA» de primera con el prefijo puesto: RocolaOs.Mejores.cs; sin prefijo, la misma)
            List<SearchItem> antes = searchOverlay.Results;
            if (searchOverlay.FocusResults && antes != null && searchOverlay.SelResult >= 0 && searchOverlay.SelResult < antes.Count)
            {
                int n = m.IndexOf(antes[searchOverlay.SelResult]);
                if (n >= 0) searchOverlay.SelResult = n;
            }
            searchOverlay.Results = m;
            CuentaResultados(rocolaOsSpEscrito, cuenta);
            rocolaOsSpVisto = m; rocolaOsSpVistoConsulta = rocolaOsSpEscrito; rocolaOsSpVistoCuenta = cuenta;
            searchOverlay.DatosVer++;   // (la cache de las tarjetas se rehace: la consulta es la misma)
            searchOverlay.Invalidate();
        }
        internal static string RocolaOsSpotifyCatalogoClave(string consulta)
        {
            if (consulta == null) return "";
            StringBuilder sb = new StringBuilder(consulta.Length);
            bool espacio = false;
            foreach (char ch in consulta.Trim())
            {
                if (char.IsWhiteSpace(ch)) { espacio = true; continue; }
                if (espacio && sb.Length > 0) sb.Append(' ');
                espacio = false;
                sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }
        private void RocolaOsSpotifyCatalogoLlegoBusqueda(string q, RocolaOsSpotifyCatalogoBusqueda b, string error)
        {
            if (b == null)
            {
                // (sin red, la API caida o frenando, esto saldria en cada cosa escrita: el mismo renglon, una vez cada 10 minutos)
                if (error != null && error != "sin_llave" && error != RocolaOsSpotifyCatalogoApi.ErrorSinRedEspera) RocolaOsSpotifyCatalogoApi.ApuntaCallado("busqueda sin respuesta: " + error);   // (A3: la espera de «sin red» ya se apunto con su primer fallo)
                if (rocolaOsSpConsulta == q) RocolaOsSpotifyCatalogoSueltaLocal(rocolaOsSpGen);   // (23-11-2026) fallo: lo local ya, sin esperar los 2 s
                return;
            }
            if (!searchOpen || rocolaOsSpConsulta != q || rocolaOsSpXp == null) return;   // ya se escribio otra cosa (o se cerro)
            rocolaOsSpEspera = false;
            List<SearchItem> m = RocolaOsSpotifyCatalogoMezcla(rocolaOsSpXp, b);
            // quien ya andaba entre los resultados no pierde su sitio: lo de Spotify entra debajo de las primeras de XP
            RocolaOsSpotifyCatalogoPoneResultados(m, (lastMatches != null ? lastMatches.Count : 0) + rocolaOsLetrasMetidas + m.Count - rocolaOsSpXp.Count);   // (la casilla dice cuantos hay, con los de Spotify)
            RocolaOsSpotifyCatalogoApi.PideEstado();
        }
        // (27-11-2026, «sin repetidas en el buscador») ¿la cancion de Spotify es la MISMA GRABACION de alguna TUYA que XP ya puso en los resultados?
        // Entonces sale una sola vez, la tuya. La misma idea que el «no repetir» de la cola (RocolaOs.SpotifyRepetida.cs): la pareja de la sustitucion
        // invisible, o titulo base, marcas de version, artista principal y duracion a 3 s. Solo se miran las tuyas que ya estan en la lista de XP.
        private bool RocolaOsSpotifyYaEstaEnLocal(List<SearchItem> xp, RocolaOsSpotifyCatalogoCancion c)
        {
            foreach (SearchItem it in xp)
            {
                if (it == null || it.Kind != 2 || it.TrackRef == null || it.TrackRef.Path == null || it.TrackRef.Path.StartsWith("spotify:", StringComparison.Ordinal)) continue;
                Track local = it.TrackRef;
                RocolaOsCalidad.Pareja par;
                string porQue;
                if (RocolaOsCalidad.Pares.TryGetValue(local.Path, out par) && par != null && par.Id != null)
                {
                    if (string.Equals(par.Uri, c.Uri, StringComparison.Ordinal)) { if (par.Verif >= 0) return true; continue; }   // su pareja (salvo medida de que es otra grabacion)
                    if (par.Verif < 0) continue;
                }
                // (un MP3 sin etiquetas se llama como su archivo, «02 Cancion 2»: sin el numero de pista del principio, si el de Spotify no lo trae)
                string t = local.Title ?? "";
                int k = 0;
                while (k < t.Length && char.IsDigit(t[k])) k++;
                if (k > 0 && k <= 3 && k < t.Length && (t[k] == ' ' || t[k] == '.' || t[k] == '-') && !(c.Titulo ?? "").StartsWith(t.Substring(0, k), StringComparison.Ordinal))
                {
                    while (k < t.Length && (t[k] == ' ' || t[k] == '.' || t[k] == '-')) k++;
                    local = new Track { Path = local.Path, Artist = local.Artist, Title = t.Substring(k), Album = local.Album, Genre = local.Genre };
                }
                if (RocolaOsMismaPorDatos(local, c.Titulo ?? "", c.Artista ?? "", c.DuracionMs, out porQue)) return true;
            }
            return false;
        }
        private List<SearchItem> RocolaOsSpotifyCatalogoMezcla(List<SearchItem> xp, RocolaOsSpotifyCatalogoBusqueda b)
        {
            List<SearchItem> artistas;
            RocolaOsSpotifyParedesArtistas(b, out artistas);   // (RocolaOs.SpotifyCatalogoParedes.cs; tus artistas que Spotify tambien tiene: su pared unida, sin otra tarjeta)
            List<SearchItem> sp = new List<SearchItem>();
            // (letras, revision) las de Spotify que ya van en la lista (las tarjetas de Genius, g114): no otra vez
            HashSet<string> yaVan = null;
            foreach (SearchItem it in xp)
                if (it != null && it.Kind == 2 && it.TrackRef != null && it.TrackRef.Path != null && it.TrackRef.Path.StartsWith("spotify:", StringComparison.Ordinal))
                    (yaVan ?? (yaVan = new HashSet<string>(StringComparer.Ordinal))).Add(it.TrackRef.Path);
            int n = 0, repetidas = 0; StringBuilder nombresRep = new StringBuilder();
            foreach (RocolaOsSpotifyCatalogoCancion c in b.Canciones)
            {
                if (n >= 8) break;
                if (yaVan != null && c != null && c.Uri != null && yaVan.Contains(c.Uri)) continue;
                if (c != null && RocolaOsSpotifyYaEstaEnLocal(xp, c)) { repetidas++; if (nombresRep.Length < 300) nombresRep.Append(nombresRep.Length > 0 ? ", " : "").Append('«').Append(c.Titulo).Append("» de ").Append(c.Artista); continue; }   // (27-11-2026) la misma grabacion ya sale, la tuya: una sola vez
                Album a = RocolaOsSpotifyCatalogoDiscoDe(c);
                Track t = RocolaOsSpotifyCatalogoPista(c);
                sp.Add(new SearchItem { Kind = 2, Display = (c.Artista.Length > 0 ? c.Artista + " - " : "") + c.Titulo, Key = "", AlbumRef = a, TrackRef = t });
                n++;
            }
            if (repetidas > 0) RocolaOs.Apunta("spotify_catalogo", "buscador: " + repetidas.ToString(CultureInfo.InvariantCulture) + " cancion(es) de Spotify no salen: " + nombresRep + " (ya estan en tu musica)");
            n = 0;
            foreach (RocolaOsSpotifyCatalogoDisco d in b.Discos)
            {
                if (n >= 3) break;
                Album a = RocolaOsSpotifyCatalogoAlbum(d);
                sp.Add(new SearchItem { Kind = 1, Display = (d.Artista.Length > 0 ? d.Artista + " - " : "") + d.Nombre, Key = "", AlbumRef = a });
                n++;
            }
            // (paredes) al final de los discos de Spotify, «Todos los discos de «lo escrito»»: la pared con todos los de la busqueda
            SearchItem todos = RocolaOsSpotifyParedesTodosTarjeta(b);
            if (todos != null) sp.Add(todos);
            // LOS ARTISTAS (pedido del dueno del 7-11-2026): los de Spotify que NO estan en la rocola, justo despues de tus artistas que salgan
            // primero (si no hay, arriba). (23-11-2026) Ya no sale «ARTISTA · mas en Spotify» detras de tu tarjeta: tu tarjeta abre la pared unida
            int lideres = 0;
            while (lideres < xp.Count && xp[lideres] != null && xp[lideres].Kind == 0) lideres++;
            List<SearchItem> m = new List<SearchItem>(xp.Count + sp.Count + artistas.Count);
            int arriba = Math.Min(RocolaOsSpotifyXpArriba, xp.Count);
            if (lideres == 0) m.AddRange(artistas);
            for (int i = 0; i < xp.Count; i++)
            {
                SearchItem it = xp[i];
                m.Add(it);
                if (i == lideres - 1) m.AddRange(artistas);
                if (i == arriba - 1) m.AddRange(sp);
            }
            if (arriba == 0) m.AddRange(sp);
            return m;
        }

        // g41 (Form1.Biblioteca.cs, ChooseSearchResult: «if (it == null) return;»): ELEGIR UN RESULTADO DE SPOTIFY. true = ya lo atendio.
        private bool RocolaOsSpotifyCatalogoElige(SearchItem it)
        {
            // (23-11-2026, el dueno: «siempre si se escoge la opcion de artista o album en el buscador, se abra ya sea una pared de discos
            // temporal, o el disco dentro del buscador») un artista tuyo: SIEMPRE una pared del buscador (la unida si Spotify lo tiene, si no
            // solo con tus discos); un disco tuyo: dentro del buscador (REGRESAR vuelve a el). Nunca la pared de verdad.
            if (RocolaOsMejoresElige(it)) return true;   // («LAS MEJORES CANCIONES DE ARTISTA»: RocolaOs.Mejores.cs)
            if (it != null && (RocolaOsSpotifyArtistaTuyoElige(it) || RocolaOsSpotifyDiscoTuyoElige(it))) return true;
            if (it == null || !RocolaOsEsSpotify(it.AlbumRef) || !RocolaOsSpotifyCatalogoVa) return false;
            MarkSearchActivity();
            if (it.Kind == 2 && it.TrackRef != null) { RocolaOsSpotifyCatalogoPide(it.TrackRef, it.AlbumRef); return true; }   // (no cierra el buscador: como XP)
            if (RocolaOsSpotifyParedesElige(it)) return true;   // un artista o «Todos los discos de...»: su pared del buscador
            if (it.Kind == 1) { RocolaOsSpotifyCatalogoAbre(it.AlbumRef, true); return true; }
            return true;
        }

        // g42 (Form1.cs, AddSong: «if (t == null) return;»): PONER en un disco de Spotify (o «Tus favoritas»). true = ya lo atendio.
        private bool RocolaOsSpotifyCatalogoPone(Track t, Album disco)
        {
            if (!RocolaOsEsSpotify(t) || !RocolaOsSpotifyCatalogoVa) return false;
            RocolaOsSpotifyCatalogoPide(t, disco);
            return true;
        }

        // g43 (Form1.Portadas.cs, ThumbCachePath): LA MINIATURA DE UNA PORTADA DE SPOTIFY, en /run y no en rocola_miniaturas (nunca al disco).
        // Lo demas, la de XP tal cual. Estatica y desde los hilos de las portadas de XP: solo compara la ruta.
        private static string RocolaOsSpotifyCatalogoMiniatura(string portada, string deXp)
        {
            string pg = RocolaOsPgPortadas.CarpetaConBarra;   // (pared-generos) las del catalogo: sus miniaturas, en su cache con tope
            if (pg != null && portada != null && portada.StartsWith(pg, StringComparison.Ordinal)) return Path.Combine(pg, "miniaturas", Path.GetFileName(deXp));
            string d = RocolaOsSpotifyCatalogoPortadas.Carpeta;
            if (d == null || portada == null || !portada.StartsWith(d, StringComparison.Ordinal)) return deXp;
            return Path.Combine(Path.GetDirectoryName(d.TrimEnd('/')) ?? d, "miniaturas", Path.GetFileName(deXp));
        }

        // g50 (PreviewsIndexadosGpu.cs, IndexedSave): la previa de 32 px de una portada de Spotify no va a previews32-v1.bin (en el disco)
        internal static bool RocolaOsSpotifyCatalogoEsMiniatura(string identidad)
        {
            string pg = RocolaOsPgPortadas.CarpetaConBarra;   // (pared-generos) ni las del catalogo: son miles y tienen su cache
            if (pg != null && identidad != null && identidad.StartsWith(pg, StringComparison.Ordinal)) return true;
            string d = RocolaOsSpotifyCatalogoPortadas.Carpeta;
            if (d == null || identidad == null) return false;
            string mini = Path.Combine(Path.GetDirectoryName(d.TrimEnd('/')) ?? d, "miniaturas") + "/";
            return identidad.StartsWith(mini, StringComparison.Ordinal) || identidad.StartsWith(d, StringComparison.Ordinal);
        }

        // g44 (Form1.Biblioteca.cs, CurrentList): LOS DISCOS DE LA LETRA DE SPOTIFY. De XP: una lista vacia (lo de antes).
        private List<Album> RocolaOsSpotifyCatalogoLista(char letra)
        {
            if (letra == RocolaOsSpotifyMarca && RocolaOsSpotifyCatalogoVa) return rocolaOsSpLetra;
            if (letra == RocolaOsSpotifyParedMarca && rocolaOsSpPared != null && RocolaOsSpotifyCatalogoVa) return rocolaOsSpPared.Lista;   // (una pared del buscador)
            return new List<Album>();
        }

        // g45 (Form1.Biblioteca.cs, LetrasVisibles): LA LETRA DE SPOTIFY AL FINAL DE LA BARRA, si tiene discos. Sin el modo, lo de XP.
        private string RocolaOsSpotifyCatalogoLetras(string deXp)
        {
            if (!RocolaOsSpotifyCatalogoVa) return deXp;
            deXp = RocolaOsPgLetras(deXp);   // (pared-generos) las letras que trae el catalogo
            // (revision de mejoras del 24-11-2026, A1) lo de la cuenta (/me/tracks, /me/albums, /me/playlists) solo alimenta la letra ♫ de la pared:
            // sin ella (RocolaOsSpotifyLetraEnLaPared = false) nada se ve, asi que ni se pide (3 peticiones y un hilo de reintentos de menos).
            // Los atajos de SP9 (/me/top, recently-played) son otra cosa y siguen. Con la letra de vuelta, vuelve esto solo.
            if (RocolaOsSpotifyLetraEnLaPared && !rocolaOsSpCuentaPedida && RocolaOsSpotifyCatalogoApi.HayCuenta)
            {
                rocolaOsSpCuentaPedida = true;
                RocolaOsSpotifyCatalogoApi.PideCuenta(delegate (object r, string error)
                {
                    // (esto se pide en cuanto se arma la barra, que puede ser antes de que la ventana exista: el encargo espera a la ventana
                    // en su propio hilo, hasta 2 minutos, sin detener al del catalogo)
                    RocolaOs.Hilo("spotify_cuenta", delegate
                    {
                        for (int i = 0; i < 1200 && RocolaOs.LaRocola == null; i++) Thread.Sleep(100);
                        RocolaOs.Encarga("spotify_catalogo:cuenta", delegate { RocolaOsSpotifyCatalogoLlegoCuenta(r as List<RocolaOsSpotifyCatalogoDisco>, error); });
                    });
                });
            }
            if (letterAvailable == null) return deXp;
            if (rocolaOsSpLetra.Count > 0) letterAvailable.Add(RocolaOsSpotifyMarca); else letterAvailable.Remove(RocolaOsSpotifyMarca);
            if (rocolaOsSpLetra.Count == 0) return deXp;
            return deXp == "#" && !letterAvailable.Contains('#') ? RocolaOsSpotifyMarca.ToString() : deXp + RocolaOsSpotifyMarca;
        }

        // g46 (Form1.Biblioteca.cs, GotoLetter): EL ORDEN DE LAS LETRAS para REGRESAR+izquierda/derecha, con la de Spotify al final
        private string RocolaOsSpotifyCatalogoOrden(string deXp)
        {
            if (!RocolaOsSpotifyCatalogoVa) return deXp;
            if (currentLetter == RocolaOsSpotifyParedMarca && rocolaOsSpPared != null) return "";   // una pared del buscador: ni otras letras ni la vuelta
            if (rocolaOsSpLetra.Count == 0) return deXp;
            return deXp + RocolaOsSpotifyMarca;
        }

        // g47 (Form1.Biblioteca.cs, RefreshAlbums): LA CABECERA de la letra de Spotify dice «SPOTIFY» (como «VIDEOS» o «LO MAS SONADO»)
        private string RocolaOsSpotifyCatalogoSeccion(char letra, string deXp)
        {
            RocolaOsPgRefresco();   // (pared-generos) las portadas de la pagina que se mira
            if (letra == RocolaOsSpotifyParedMarca && rocolaOsSpPared != null && RocolaOsSpotifyCatalogoVa) return RocolaOsSpotifyParedMira();
            return letra == RocolaOsSpotifyMarca && RocolaOsSpotifyCatalogoVa ? "SPOTIFY" : deXp;
        }

        // g48 (Form1.Vistas.cs, Track.ToString): EL NOMBRE DE UNA CANCION DE SPOTIFY EN LA LISTA DEL DISCO («NN Titulo», como el archivo de
        // una de XP). De XP: null (y sigue lo de XP). Barata: se pregunta por cada renglon que se dibuja.
        internal static string RocolaOsSpotifyCatalogoNombre(string ruta)
        {
            if (ruta == null || ruta.Length < 15 || ruta[0] != 's' || !ruta.StartsWith("spotify:track:", StringComparison.Ordinal)) return null;
            lock (RocolaOsSpotifyCatalogoNombres.PorRuta)
            {
                string n;
                return RocolaOsSpotifyCatalogoNombres.PorRuta.TryGetValue(ruta, out n) ? n : null;
            }
        }

        // g49 (Form1.Biblioteca.cs, EnterAlbum: «currentAlbum = a;»): ENTRAR A UN DISCO DE SPOTIFY que aun no tiene sus canciones: se piden
        // (una peticion) y entran a la lista de XP al llegar. Devuelve el mismo disco.
        private Album RocolaOsSpotifyCatalogoEntra(Album a)
        {
            rocolaOsSpVueltaDisco = null; rocolaOsSpVueltaAlbum = null;   // (otro disco: REGRESAR ya no vuelve al buscador; sin el modo, siempre null)
            if (!RocolaOsEsSpotify(a) || !RocolaOsSpotifyCatalogoVa) return a;
            if (RocolaOsSpotifyParedesEntra(a)) return a;   // «Canciones de ARTISTA» o una casilla de una pared que aun no llega
            if (RocolaOsPgEntra(a)) return a;               // (pared-generos) un disco del catalogo de las letras
            RocolaOsSpotifyCatalogoDisco d;
            if (a.Tracks.Count == 0 && rocolaOsSpDeDisco.TryGetValue(a, out d)) RocolaOsSpotifyCatalogoAbre(a, false);
            return a;
        }

        // g51 (Form1.Biblioteca.cs, RecalculaLetras: «letterBar.Counts = cuenta;»): EL NUMERO DE LA LETRA DE SPOTIFY, como el de cada letra de
        // XP (las canciones que hay detras; con el, su rayita). Sin el modo, o sin canciones sabidas, el mismo diccionario de XP sin tocar.
        private Dictionary<char, int> RocolaOsSpotifyCatalogoCuentas(Dictionary<char, int> cuenta)
        {
            if (cuenta == null || !RocolaOsSpotifyCatalogoVa) return cuenta;
            int n = RocolaOsSpotifyCatalogoCanciones();
            if (n > 0) cuenta[RocolaOsSpotifyMarca] = n;
            return cuenta;
        }
        private int RocolaOsSpotifyCatalogoCanciones()
        {
            int n = 0;
            if (rocolaOsSpLetra != null) foreach (Album a in rocolaOsSpLetra) if (a != null && a.Tracks != null) n += a.Tracks.Count;
            return n;
        }
        // lo mismo sin rehacer toda la barra (cuando cambian las canciones de la letra y no si la letra esta): XP cambia el diccionario entero
        // (la barra lo compara por identidad), asi que aqui tambien
        private void RocolaOsSpotifyCatalogoPonCuenta()
        {
            if (letterBar == null || libBuilding) return;
            Dictionary<char, int> antes = letterBar.Counts;
            int n = RocolaOsSpotifyCatalogoCanciones(), ya = 0;
            if (antes != null) antes.TryGetValue(RocolaOsSpotifyMarca, out ya);
            if (n == ya) return;
            Dictionary<char, int> c = antes != null ? new Dictionary<char, int>(antes) : new Dictionary<char, int>();
            if (n > 0) c[RocolaOsSpotifyMarca] = n; else c.Remove(RocolaOsSpotifyMarca);
            letterBar.Counts = c;
            letterBar.Invalidate();
        }

        // g52 (PantallaVivaGpu.cs, AlbumElegidoEnPared: «return albumView.Page[i];», en cada cuadro): UN DISCO DE SPOTIFY QUE AUN NO TIENE SUS
        // CANCIONES (los de un artista, tus listas) y se queda elegido en la pared 0,6 s: se piden (una peticion, una vez por disco y sesion:
        // alguien lo esta mirando), para que la ficha y el letrero de XP digan las que trae y no «0 canciones». Devuelve el mismo disco.
        private Album RocolaOsSpotifyCatalogoElegido(Album a)
        {
            if (!RocolaOsEsSpotify(a) || a.Tracks.Count > 0 || !RocolaOsSpotifyCatalogoVa) return a;
            // (pared-generos) un disco del catalogo de las letras aun sin abrir: NO se pide nada (la pared no gasta la cuota) y no hay ficha ni
            // letrero (dirian «0 canciones»): como si no hubiera disco elegido
            if (RocolaOsPgEsDelCatalogo(a)) return null;
            int ahora = Environment.TickCount;
            if (!ReferenceEquals(a, rocolaOsSpMirado)) { rocolaOsSpMirado = a; rocolaOsSpMiradoDesde = ahora; return a; }
            if (unchecked(ahora - rocolaOsSpMiradoDesde) < 600) return a;
            if (rocolaOsSpPrevistos == null) rocolaOsSpPrevistos = new HashSet<Album>();
            RocolaOsSpotifyCatalogoDisco d;
            if (!rocolaOsSpPrevistos.Add(a) || !rocolaOsSpDeDisco.TryGetValue(a, out d) || d.Canciones != null) return a;
            Action<object, string> listo = delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:previsto", delegate { RocolaOsSpotifyCatalogoLlegoPrevisto(a, r as RocolaOsSpotifyCatalogoDisco); });
            };
            if (d.Tipo == "playlist") RocolaOsSpotifyCatalogoApi.AbreLista(d.Id, listo);
            else if (d.Tipo == "album") RocolaOsSpotifyCatalogoApi.AbreDisco(d.Id, listo);
            else if (d.Tipo == "canciones") RocolaOsSpotifyCancionesPrevista(a);   // («Canciones de ARTISTA»: su 1a pagina de 10, y no «0 canciones»)
            return a;
        }
        // llegaron: solo se llenan (la letra no se reacomoda: quien mira no pierde su sitio); la ficha y el letrero se rehacen con lo que XP ya
        // vigila (generoVer, duracionesVer). Si no llegaron, nada (al entrar, g49 lo vuelve a pedir y XP dice lo suyo).
        private void RocolaOsSpotifyCatalogoLlegoPrevisto(Album a, RocolaOsSpotifyCatalogoDisco d)
        {
            if (d == null || d.Canciones == null || a.Tracks.Count > 0) return;
            rocolaOsSpDeDisco[a] = d;
            RocolaOsSpotifyCatalogoLlena(a, d);
            generoVer++; duracionesVer++;
            RocolaOsSpotifyCatalogoPonCuenta();
        }

        // ================================================================= pedir (I6) =================================================

        private void RocolaOsSpotifyCatalogoPide(Track t, Album disco)
        {
            RocolaOsSpotifyCatalogoCancion c;
            if (!rocolaOsSpCanciones.TryGetValue(t.Path, out c)) return;
            if (!RocolaOsSpotifyListo)
            {
                // Soloist sin sesion: no se pide nada (ni se cobra); el aviso que XP ya tiene
                RocolaOsSpotifyCatalogoAviso("Espera un momento y vuelve a intentarlo");
                RocolaOs.Apunta("spotify_catalogo", "no se pidio «" + c.Uri + "»: Spotify aun no esta listo (Soloist sin sesion)");
                return;
            }
            RocolaOsSpotifyPista p = new RocolaOsSpotifyPista
            {
                Uri = c.Uri, Titulo = c.Titulo, Artista = c.Artista, Disco = c.Disco, DuracionMs = c.DuracionMs, PortadaUrl = c.PortadaUrl, DiscoUri = c.DiscoUri,
            };
            // (I6 del nucleo: con el buscador abierto, sus avisos son los del buscador, como ChooseSearchResult de XP; si no, los de AddSong)
            bool entro = RocolaOsSpotifyPide(p, searchOpen);
            if (entro)
            {
                Album a = disco;
                if (a == null || rocolaOsSpDeArtista.ContainsKey(a) || RocolaOsPgEsDelCatalogo(a)) a = RocolaOsSpotifyCatalogoDiscoDe(c);   // (pared-generos: a la letra ♫, el suyo)
                if (a != null && (!rocolaOsSpDeDisco.TryGetValue(a, out RocolaOsSpotifyCatalogoDisco d) || d.Tipo == "album")) RocolaOsSpotifyCatalogoReciente(a);
            }
        }
        private void RocolaOsSpotifyCatalogoAviso(string texto)
        {
            if (searchOpen) ShowSearchToast(texto, false);
            else ShowToast(texto, System.Drawing.Color.FromArgb(200, 130, 35));
        }

        // ================================================================= abrir ======================================================

        // ABRIR UN DISCO DE SPOTIFY en la vista de disco de XP (desde el buscador: como XP, la portada sale del cubo del buscador). Si aun no
        // tiene sus canciones, se piden y se abre al llegar (si nadie abrio otra cosa mientras tanto).
        private void RocolaOsSpotifyCatalogoAbre(Album a, bool desdeBuscador)
        {
            RocolaOsSpotifyCatalogoDisco d;
            if (!rocolaOsSpDeDisco.TryGetValue(a, out d)) return;
            bool enPared = rocolaOsSpLetra.Contains(a);   // (ya en la letra de Spotify: REGRESAR vuelve a ella, como XP; si no, al buscador)
            if (d.Canciones != null && a.Tracks.Count > 0)
            {
                if (desdeBuscador) RocolaOsSpotifyCatalogoEntraDesdeBuscador(a, enPared);
                return;
            }
            int yo = ++rocolaOsSpAbriendo;
            string escritoAl = searchOverlay != null ? (searchOverlay.Escrito ?? "") : "";   // (23-11-2026: lo escrito al pedirlo)
            Action<object, string> listo = delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:disco", delegate { RocolaOsSpotifyCatalogoLlegoDisco(a, r as RocolaOsSpotifyCatalogoDisco, error, desdeBuscador, yo, enPared, escritoAl); });
            };
            if (d.Tipo == "playlist") RocolaOsSpotifyCatalogoApi.AbreLista(d.Id, listo);
            else if (d.Tipo == "album") RocolaOsSpotifyCatalogoApi.AbreDisco(d.Id, listo);
        }
        private void RocolaOsSpotifyCatalogoLlegoDisco(Album a, RocolaOsSpotifyCatalogoDisco d, string error, bool desdeBuscador, int yo, bool enPared, string escritoAl)
        {
            if (d == null || d.Canciones == null)
            {
                RocolaOsSpotifyCatalogoApi.ApuntaCallado("no se pudo abrir el disco: " + (error ?? "sin respuesta"));
                if (yo == rocolaOsSpAbriendo && (desdeBuscador ? searchOpen : (panelSongs.Visible && currentAlbum == a))) RocolaOsSpotifyCatalogoAviso("Espera un momento y vuelve a intentarlo");
                return;
            }
            rocolaOsSpDeDisco[a] = d;
            RocolaOsSpotifyCatalogoLlena(a, d);
            RocolaOsSpotifyCatalogoPonCuenta();
            if (d.Tipo == "album") RocolaOsSpotifyCatalogoReciente(a);
            if (yo != rocolaOsSpAbriendo) return;
            // (23-11-2026, revision) solo si el buscador sigue con lo mismo escrito: si no, el disco viejo se abria encima de otra busqueda
            if (desdeBuscador) { if (searchOpen && (searchOverlay.Escrito ?? "") == escritoAl) RocolaOsSpotifyCatalogoEntraDesdeBuscador(a, enPared); return; }
            // ya estaba dentro (desde la pared): sus canciones entran a la lista de XP como en EnterAlbum
            if (panelSongs.Visible && currentAlbum == a)
            {
                songItems.Clear();
                for (int i = 0; i < a.Tracks.Count; i++) songItems.Add(a.Tracks[i]);
                songSel = 0;
                songList.OlvidaPagina();
                if (songPager != null) songPager.PonSinAnimar(0);
                FillSongPage();
                songList.Invalidate();
            }
        }
        // lo de XP al elegir un disco en el buscador (ChooseSearchResult, Kind 1), con el disco de Spotify en su letra. Si NO estaba ya en
        // la pared (pedido del dueno del 7-11-2026), REGRESAR vuelve al buscador con lo escrito y la misma eleccion (g54)
        private void RocolaOsSpotifyCatalogoEntraDesdeBuscador(Album a, bool enPared)
        {
            RocolaOsSpotifyVuelta v = enPared ? null : RocolaOsSpotifyGuardaBuscador();
            RocolaOsSpotifyCatalogoReciente(a);
            transNextFrom = SearchCubeRect(out transNextAngle);   // la portada sale del CUBO GRANDE del buscador (luego ya no existe)
            CloseSearch(); NavigateToAlbum(a); EnterAlbum(a);
            if (v != null) { rocolaOsSpVueltaDisco = v; rocolaOsSpVueltaAlbum = a; }
        }
        // ================================================================= la cuenta y la letra =======================================

        private void RocolaOsSpotifyCatalogoLlegoCuenta(List<RocolaOsSpotifyCatalogoDisco> l, string error)
        {
            if (l == null) { RocolaOs.Apunta("spotify_catalogo", "lo de la cuenta no llego: " + (error ?? "sin respuesta")); return; }
            rocolaOsSpCuenta.Clear();
            foreach (RocolaOsSpotifyCatalogoDisco d in l) rocolaOsSpCuenta.Add(RocolaOsSpotifyCatalogoAlbum(d));
            RocolaOs.Apunta("spotify_catalogo", "lo de la cuenta: " + l.Count.ToString(CultureInfo.InvariantCulture) + " discos y listas");
            RocolaOsSpotifyCatalogoRehaceLetra();
        }
        // un disco abierto o pedido: al principio de los recientes de la letra
        private void RocolaOsSpotifyCatalogoReciente(Album a)
        {
            if (a == null || rocolaOsSpCuenta.Contains(a)) return;
            int i = rocolaOsSpRecientes.IndexOf(a);
            if (i == 0) return;
            if (i > 0) rocolaOsSpRecientes.RemoveAt(i);
            rocolaOsSpRecientes.Insert(0, a);
            if (rocolaOsSpRecientes.Count > RocolaOsSpotifyRecientesMax) rocolaOsSpRecientes.RemoveRange(RocolaOsSpotifyRecientesMax, rocolaOsSpRecientes.Count - RocolaOsSpotifyRecientesMax);
            RocolaOsSpotifyCatalogoRehaceLetra();
        }
        // LA LETRA ♫ EN LA PARED (el dueno la quito el 22-11-2026, «cargan muy lento las caratulas», y el 24-11 dijo «voy a regresar la pared de
        // discos de spotify la proxima semana»; vuelve el 27-11-2026 con las portadas a la mano, RocolaOs.ParedPortadas.cs): lo de la cuenta (tus
        // favoritas, tus discos, tus listas) y lo reciente al final del abecedario, con el icono de Spotify en su casilla
        // (RocolaOs.ParedIcono.cs). false la vuelve a quitar (un disco de Spotify abierto desde el buscador vuelve al buscador con REGRESAR).
        internal const bool RocolaOsSpotifyLetraEnLaPared = false;   // (27-11-2026, el dueno: «Lo de la casilla final con icono de Spotify quitala, esa casilla no tiene utilidad realmente»)
        // LA LETRA: lo de la cuenta y despues lo reciente. Si alguien esta mirando la letra, su disco sigue elegido.
        private void RocolaOsSpotifyCatalogoRehaceLetra()
        {
            List<Album> antes = rocolaOsSpLetra;
            Album elegido = null;
            bool mirando = currentLetter == RocolaOsSpotifyMarca && albumView != null && albumView.Visible;
            if (mirando && selAlbumIndex >= 0 && selAlbumIndex < antes.Count) elegido = antes[selAlbumIndex];
            List<Album> l = new List<Album>(rocolaOsSpCuenta.Count + rocolaOsSpRecientes.Count);
            l.AddRange(rocolaOsSpCuenta);
            foreach (Album a in rocolaOsSpRecientes) if (!l.Contains(a)) l.Add(a);
            if (!RocolaOsSpotifyLetraEnLaPared) l.Clear();   // (sin la letra ♫: lo de Spotify solo por el buscador)
            bool vacia = antes.Count == 0;
            rocolaOsSpLetra = l;
            if (vacia != (l.Count == 0)) RecalculaLetras();   // la letra aparece (o se va)
            if (currentLetter == RocolaOsSpotifyMarca && !libBuilding)
            {
                if (elegido != null) { int i = l.IndexOf(elegido); if (i >= 0) { selAlbumIndex = i; page = PageSize > 0 ? i / PageSize : 0; } }
                if (!panelSongs.Visible) RefreshAlbums();
            }
            RocolaOsSpotifyCatalogoPonCuenta();
        }

        // ================================================================= de Spotify a XP ============================================

        private Album RocolaOsSpotifyCatalogoAlbum(RocolaOsSpotifyCatalogoDisco d)
        {
            string clave = d.Tipo + ":" + d.Id;
            Album a;
            if (!rocolaOsSpDiscos.TryGetValue(clave, out a))
            {
                a = new Album { Folder = RocolaOsSpotifyCarpeta + clave, Name = d.Nombre ?? "", Artist = d.Artista ?? "", Letter = RocolaOsSpotifyMarca };
                rocolaOsSpDiscos[clave] = a;
                rocolaOsSpDeDisco[a] = d;
                RocolaOsSpotifyCatalogoPortada(a, d.Id, d.PortadaUrl);
            }
            else if (d.Canciones != null && rocolaOsSpDeDisco.TryGetValue(a, out RocolaOsSpotifyCatalogoDisco viejo) && viejo.Canciones == null) rocolaOsSpDeDisco[a] = d;
            // (27-11-2026) el disco que se armo de una cancion de la busqueda no trae cuantas canciones tiene: se completa con el que si lo dice (las ediciones)
            if (d.NumCanciones > 0 && rocolaOsSpDeDisco.TryGetValue(a, out RocolaOsSpotifyCatalogoDisco v2) && v2.NumCanciones <= 0) v2.NumCanciones = d.NumCanciones;
            RocolaOsSpotifyCatalogoPortadaPerdida(a);
            RocolaOsSpotifyCatalogoDisco este = rocolaOsSpDeDisco[a];
            if (este.Canciones != null && a.Tracks.Count == 0) RocolaOsSpotifyCatalogoLlena(a, este);
            return a;
        }
        // el disco de una cancion (de la busqueda o de una lista): el de Spotify si se sabe, sin sus canciones hasta abrirlo
        private Album RocolaOsSpotifyCatalogoDiscoDe(RocolaOsSpotifyCatalogoCancion c)
        {
            if (string.IsNullOrEmpty(c.DiscoId)) return RocolaOsSpotifyCatalogoAlbum(new RocolaOsSpotifyCatalogoDisco { Id = "suelta-" + c.Id, Uri = c.Uri, Nombre = c.Disco ?? "", Artista = c.Artista, PortadaUrl = c.PortadaUrl, Tipo = "suelta" });
            RocolaOsSpotifyCatalogoDisco d = RocolaOsSpotifyCatalogoApi.DiscoYa(c.DiscoId) ?? new RocolaOsSpotifyCatalogoDisco { Id = c.DiscoId, Uri = c.DiscoUri, Nombre = c.Disco ?? "", Artista = c.Artista, PortadaUrl = c.PortadaUrl, Tipo = "album" };
            return RocolaOsSpotifyCatalogoAlbum(d);
        }
        private Album RocolaOsSpotifyCatalogoArtistaAlbum(RocolaOsSpotifyCatalogoArtista ar)
        {
            string clave = "artista:" + ar.Id;
            Album a;
            if (!rocolaOsSpDiscos.TryGetValue(clave, out a))
            {
                a = new Album { Folder = RocolaOsSpotifyCarpeta + clave, Name = "", Artist = ar.Nombre ?? "", Letter = RocolaOsSpotifyMarca };
                rocolaOsSpDiscos[clave] = a;
                rocolaOsSpDeArtista[a] = ar;
                RocolaOsSpotifyCatalogoPortada(a, "artista-" + ar.Id, ar.PortadaUrl);
            }
            else RocolaOsSpotifyCatalogoPortadaPerdida(a);
            return a;
        }
        private Track RocolaOsSpotifyCatalogoPista(RocolaOsSpotifyCatalogoCancion c)
        {
            rocolaOsSpCanciones[c.Uri] = c;
            string nombre = (c.Numero > 0 ? c.Numero.ToString("00", CultureInfo.InvariantCulture) + " " : "") + c.Titulo;
            lock (RocolaOsSpotifyCatalogoNombres.PorRuta) RocolaOsSpotifyCatalogoNombres.PorRuta[c.Uri] = nombre;
            return new Track { Path = c.Uri, Artist = c.Artista ?? "", Title = c.Titulo ?? "", Album = c.Disco ?? "", Genre = "" };
        }
        private void RocolaOsSpotifyCatalogoLlena(Album a, RocolaOsSpotifyCatalogoDisco d)
        {
            a.Tracks.Clear();
            foreach (RocolaOsSpotifyCatalogoCancion c in d.Canciones)
            {
                if (string.IsNullOrEmpty(c.PortadaUrl)) c.PortadaUrl = d.PortadaUrl;
                if (string.IsNullOrEmpty(c.DiscoId) && d.Tipo == "album") { c.DiscoId = d.Id; c.DiscoUri = d.Uri; c.Disco = d.Nombre; }
                a.Tracks.Add(RocolaOsSpotifyCatalogoPista(c));
            }
        }

        // ================================================================= el recorte de /run (A8) ===================================
        // (revision de mejoras del 24-11-2026, A8) /run es memoria: las portadas de Spotify (portadas/) y sus miniaturas (miniaturas/) solo se
        // limpiaban al arrancar (las de mas de un dia, y solo las de lo que suena). En una rocola que pasa semanas sin reiniciar, crecian sin tope.
        // Ahora, al abrir el buscador (como mucho una vez por hora) un hilo de fondo (nice 19) suma las dos carpetas y, si pasan de ~150 MB,
        // borra lo de acceso o modificacion mas viejo hasta quedar debajo. Nunca: lo que suena o esta en la cola, lo del disco y la pared abiertos,
        // la pila de paredes del buscador, los resultados que se ven, ni nada tocado en los ultimos 30 min. Los discos cuya portada se borro la
        // vuelven a pedir cuando reaparecen en una busqueda. Nada se guarda en disco.
        private Dictionary<Album, KeyValuePair<string, string>> rocolaOsSpPortadaDe;   // de cada Album de Spotify: (id, url) de su portada
        private HashSet<Album> rocolaOsSpPortadaBorrada;
        private void RocolaOsSpotifyCatalogoRecorta()
        {
            if (!RocolaOsSpotifyCatalogoRecorte.Toca()) return;
            HashSet<string> prot = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (nowPlaying != null && nowPlaying.Disc != null && nowPlaying.Disc.CoverPath != null) prot.Add(nowPlaying.Disc.CoverPath);
                foreach (PlayItem it in queue) if (it != null && it.Disc != null && it.Disc.CoverPath != null) prot.Add(it.Disc.CoverPath);
                if (currentAlbum != null && currentAlbum.CoverPath != null) prot.Add(currentAlbum.CoverPath);
                for (RocolaOsSpotifyPared p = rocolaOsSpPared; p != null; p = p.Vuelta != null ? p.Vuelta.Pared : null)
                {
                    foreach (Album a in p.Lista) if (a != null && a.CoverPath != null) prot.Add(a.CoverPath);
                    if (p.Canciones != null && p.Canciones.CoverPath != null) prot.Add(p.Canciones.CoverPath);
                }
                if (rocolaOsSpVueltaAlbum != null && rocolaOsSpVueltaAlbum.CoverPath != null) prot.Add(rocolaOsSpVueltaAlbum.CoverPath);
                List<SearchItem> r = searchOverlay != null ? searchOverlay.Results : null;
                if (r != null) foreach (SearchItem it in r) if (it != null && it.AlbumRef != null && it.AlbumRef.CoverPath != null) prot.Add(it.AlbumRef.CoverPath);
            }
            catch { }   // (una lista que cambia: lo que no se alcanzo a apuntar lo cubre la ventana de 30 min)
            RocolaOsSpotifyCatalogoRecorte.Lanza(prot, delegate (HashSet<string> borradas)
            {
                RocolaOs.Encarga("spotify_catalogo:recorte", delegate
                {
                    if (rocolaOsSpPortadaDe == null || borradas.Count == 0) return;
                    foreach (KeyValuePair<Album, KeyValuePair<string, string>> kv in rocolaOsSpPortadaDe)
                        if (kv.Key.CoverPath != null && borradas.Contains(kv.Key.CoverPath))
                        {
                            kv.Key.CoverPath = null;
                            (rocolaOsSpPortadaBorrada ?? (rocolaOsSpPortadaBorrada = new HashSet<Album>())).Add(kv.Key);
                        }
                });
            });
        }
        // un disco que ya se conocia vuelve a aparecer y su portada se borro: otra vez (se baja de nuevo; los discos no cambian)
        private void RocolaOsSpotifyCatalogoPortadaPerdida(Album a)
        {
            KeyValuePair<string, string> p;
            if (rocolaOsSpPortadaBorrada == null || !rocolaOsSpPortadaBorrada.Remove(a) || rocolaOsSpPortadaDe == null || !rocolaOsSpPortadaDe.TryGetValue(a, out p)) return;
            RocolaOsSpotifyCatalogoPortada(a, p.Key, p.Value);
        }

        // ================================================================= las portadas ===============================================
        // Se bajan a /run/rocola-xp/spotify/portadas/<id>.jpg (en memoria; nunca al disco), varias a la vez y aparte de la API (no gastan su
        // cuota: vienen del servidor de imagenes de Spotify), y al llegar el disco toma su ruta y XP la lee como cualquier portada.
        private Dictionary<string, List<Album>> rocolaOsSpEsperanPortada;   // ruta -> los discos que la esperan

        private void RocolaOsSpotifyCatalogoPortada(Album a, string id, string url)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(id)) return;
            string nombre = id;
            foreach (char ch in id) if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')) { nombre = "x" + ((uint)id.GetHashCode()).ToString("x8", CultureInfo.InvariantCulture); break; }
            string ruta = Path.Combine(RocolaOs.Run("rocola-xp/spotify/portadas"), nombre + ".jpg");
            (rocolaOsSpPortadaDe ?? (rocolaOsSpPortadaDe = new Dictionary<Album, KeyValuePair<string, string>>()))[a] = new KeyValuePair<string, string>(id, url);   // (A8: para volver a pedirla si el recorte la borra)
            if (File.Exists(ruta)) { a.CoverPath = ruta; return; }
            if (rocolaOsSpEsperanPortada == null) rocolaOsSpEsperanPortada = new Dictionary<string, List<Album>>(StringComparer.Ordinal);
            List<Album> esperan;
            if (rocolaOsSpEsperanPortada.TryGetValue(ruta, out esperan)) { if (!esperan.Contains(a)) esperan.Add(a); return; }
            rocolaOsSpEsperanPortada[ruta] = new List<Album> { a };
            RocolaOsSpotifyCatalogoPortadas.Baja(url, ruta);
        }
        internal void RocolaOsSpotifyCatalogoLlegoPortada(string ruta)
        {
            List<Album> l;
            if (rocolaOsSpEsperanPortada == null || !rocolaOsSpEsperanPortada.TryGetValue(ruta, out l)) return;
            rocolaOsSpEsperanPortada.Remove(ruta);
            foreach (Album a in l) a.CoverPath = ruta;
            foreach (Album a in l) RequestCover(a, false, true);   // (al decodificarla, XP la pone donde haga falta: ApplyArrivedCover)
            if (searchOpen) RocolaOsSpotifyCatalogoDatosJunta();
            if (albumView != null && albumView.Visible) albumView.Invalidate();
        }
        // (revision de mejoras del 24-11-2026, A6) DatosVer++ rehornea TODAS las tarjetas del buscador y se saltaba la junta de 120 ms de XP
        // (JuntaPortadasMs, Codigo/BuscadorPortadasGpu.cs): con 17 portadas llegando, 17 rehornadas. Ahora, como mucho una cada 120 ms: la que
        // llega tarde marca «sucio» y un reloj de una sola vez (el encargo vuelve al hilo de la pantalla) la sube al cumplirse el plazo. Cada
        // tarjeta que aun no tenia su portada la vuelve a preguntar igual (el DatosVer sube); solo se juntan los avisos.
        internal const int RocolaOsSpDatosJuntaMs = 120;
        private long rocolaOsSpDatosUltimoMs = long.MinValue / 2;
        private System.Threading.Timer rocolaOsSpDatosReloj;
        private bool rocolaOsSpDatosSucio;
        private void RocolaOsSpotifyCatalogoDatosJunta()
        {
            long ahora = Environment.TickCount64, falta = RocolaOsSpDatosJuntaMs - (ahora - rocolaOsSpDatosUltimoMs);
            if (falta <= 0)
            {
                rocolaOsSpDatosUltimoMs = ahora; rocolaOsSpDatosSucio = false;
                searchOverlay.DatosVer++; searchOverlay.Invalidate();
                return;
            }
            if (rocolaOsSpDatosSucio) return;   // (ya hay un reloj en camino)
            rocolaOsSpDatosSucio = true;
            if (rocolaOsSpDatosReloj != null) { try { rocolaOsSpDatosReloj.Dispose(); } catch { } }
            rocolaOsSpDatosReloj = new System.Threading.Timer(delegate
            {
                RocolaOs.Encarga("spotify_catalogo:portada_junta", delegate
                {
                    if (!rocolaOsSpDatosSucio) return;
                    rocolaOsSpDatosSucio = false; rocolaOsSpDatosUltimoMs = Environment.TickCount64;
                    if (searchOpen) { searchOverlay.DatosVer++; searchOverlay.Invalidate(); }
                });
            }, null, (int)Math.Max(1, falta), System.Threading.Timeout.Infinite);
        }
    }

    // LOS HILOS DE LAS PORTADAS DE SPOTIFY: de su servidor de imagenes (no gasta la cuota de la API), a /run; al llegar, la pantalla le pone la
    // ruta al disco (encargo «RocolaOs:spotify_catalogo:portada»).
    // (rapido-spotify) VARIAS A LA VEZ y LO ULTIMO QUE SE PIDIO PRIMERO. Antes, una a la vez y en el orden en que se pidieron: la pared de un
    // artista (17 portadas) se llenaba de una en una (con 120 ms por portada, la ultima a los 2,7 s de abrirla) y lo que pidio una busqueda
    // vieja iba delante de lo que se mira ahora. Ahora 6 hilos (lo que el navegador abre por servidor; esperan a la red, no gastan procesador,
    // con nice 19 como todo lo de fondo) y, de lo que espera, primero el ULTIMO LOTE (lo que la pantalla pidio en el mismo cuadro: la pagina
    // o la busqueda de ahora) y dentro de el en su orden (de arriba abajo). Lo que llega es lo mismo, al mismo archivo; solo antes.
    internal static class RocolaOsSpotifyCatalogoPortadas
    {
        internal const int Hilos = 6;
        private sealed class Pedido { internal string Url, Ruta; internal long Lote, Orden; }
        private static readonly object cerrojo = new object();
        private static readonly List<Pedido> cola = new List<Pedido>();
        private static int hilos;
        private static long lote, orden, ultimoPedidoMs = long.MinValue / 2;
        private static string carpeta;
        internal static long Bajadas;
        // (revision cuota-hilos-y-fallos de paredes-buscador, 2a pasada) LA QUE NO SE BAJO POR LA RED (sin red, el servidor caido: 5xx, 429):
        // antes se quedaba sin portada toda la sesion (el disco ya no la vuelve a pedir: queda esperandola). Ahora, otra vez a los 15 s y, si
        // tampoco, a los 60 s (al servidor de imagenes: no gasta la cuota de la API); despues, ya no. Un 404 o una de mas de 4 MB, no.
        private static readonly int[] reintentosMs = { 15000, 60000 };
        private static readonly List<KeyValuePair<long, Pedido>> luego = new List<KeyValuePair<long, Pedido>>();
        private static readonly Dictionary<string, int> intentos = new Dictionary<string, int>(StringComparer.Ordinal);
        // /run/rocola-xp/spotify/portadas/ (con «/» al final), desde que se baja la primera (antes, null: g43 no cambia nada)
        internal static string Carpeta { get { return Volatile.Read(ref carpeta); } }

        internal static void Baja(string url, string ruta)
        {
            lock (cerrojo)
            {
                // un lote: lo pedido en el mismo cuadro de la pantalla (las llamadas a menos de 30 ms una de otra)
                long ahora = Environment.TickCount64;
                if (ahora - ultimoPedidoMs > 30) lote++;
                ultimoPedidoMs = ahora;
                cola.Add(new Pedido { Url = url, Ruta = ruta, Lote = lote, Orden = ++orden });
                while (hilos < Hilos) { hilos++; RocolaOs.Hilo("spotify_portadas_" + hilos.ToString(CultureInfo.InvariantCulture), Corre); }
                Monitor.Pulse(cerrojo);
            }
        }
        // la siguiente: la del ultimo lote y, dentro de el, la primera que se pidio (bajo el cerrojo)
        private static Pedido Siguiente()
        {
            int mejor = -1;
            for (int i = 0; i < cola.Count; i++)
                if (mejor < 0 || cola[i].Lote > cola[mejor].Lote || (cola[i].Lote == cola[mejor].Lote && cola[i].Orden < cola[mejor].Orden)) mejor = i;
            if (mejor < 0) return null;
            Pedido p = cola[mejor];
            cola.RemoveAt(mejor);
            return p;
        }
        private static void Corre()
        {
            while (true)
            {
                Pedido p;
                lock (cerrojo)
                {
                    while (true)
                    {
                        int espera = 10000;
                        long ahora = Environment.TickCount64;
                        for (int i = luego.Count - 1; i >= 0; i--)
                            if (luego[i].Key <= ahora) { Pedido x = luego[i].Value; x.Lote = 0; cola.Add(x); luego.RemoveAt(i); }   // (detras de lo nuevo)
                            else espera = (int)Math.Max(1, Math.Min(espera, luego[i].Key - ahora));
                        p = Siguiente();
                        if (p != null) break;
                        Monitor.Wait(cerrojo, espera);
                    }
                }
                Baja(p);
            }
        }
        private static void Baja(Pedido p)
        {
            bool bien = false, otraVez = false;
            try
            {
                string dir = Path.GetDirectoryName(p.Ruta);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string mini = Path.Combine(Path.GetDirectoryName(dir), "miniaturas");
                if (!Directory.Exists(mini)) Directory.CreateDirectory(mini);
                Volatile.Write(ref carpeta, dir.TrimEnd('/') + "/");
                using (HttpResponseMessage r = RocolaOsSpotifyCatalogoApi.Http.Send(new HttpRequestMessage(HttpMethod.Get, p.Url)))
                {
                    RocolaOsSpotifyCatalogoApi.PortadaDe(p.Url);   // (el servidor de las portadas: Calienta abre su conexion al abrir el buscador)
                    if ((int)r.StatusCode == 200)
                    {
                        byte[] b;
                        using (Stream s = r.Content.ReadAsStream())
                        using (MemoryStream m = new MemoryStream())
                        {
                            byte[] buf = new byte[16384]; int n;
                            while ((n = s.Read(buf, 0, buf.Length)) > 0) { m.Write(buf, 0, n); if (m.Length > 4 * 1024 * 1024) throw new InvalidDataException("portada de mas de 4 MB"); }
                            b = m.ToArray();
                        }
                        // (cada hilo con su archivo de paso: dos pedidos de la misma ruta -un reintento y uno nuevo- no se pisan)
                        string t = p.Ruta + ".t" + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
                        File.WriteAllBytes(t, b);
                        File.Move(t, p.Ruta, true);
                        bien = true;
                        Interlocked.Increment(ref Bajadas);
                    }
                    else
                    {
                        int cod = (int)r.StatusCode;
                        otraVez = cod >= 500 || cod == 429;
                        // (revision: como mucho un renglon igual cada 10 min; sin red, cada disco que llega diria lo mismo)
                        RocolaOsSpotifyCatalogoApi.ApuntaCallado("una portada no se bajo (" + cod.ToString(CultureInfo.InvariantCulture) + ")");
                    }
                }
            }
            catch (Exception ex)
            {
                otraVez = !(ex is InvalidDataException);   // (la de mas de 4 MB, no: es la misma la proxima vez)
                RocolaOsSpotifyCatalogoApi.ApuntaCallado("una portada no se bajo (" + ex.GetType().Name + ")");
            }
            if (otraVez)
                lock (cerrojo)
                {
                    int n; intentos.TryGetValue(p.Ruta, out n);
                    if (n < reintentosMs.Length) { intentos[p.Ruta] = n + 1; luego.Add(new KeyValuePair<long, Pedido>(Environment.TickCount64 + reintentosMs[n], p)); Monitor.Pulse(cerrojo); }
                }
            if (bien)
            {
                string ruta = p.Ruta;
                RocolaOs.Encarga("spotify_catalogo:portada", delegate { Form1 f = RocolaOs.LaRocola; if (f != null) f.RocolaOsSpotifyCatalogoLlegoPortada(ruta); });
            }
        }
    }

    // EL RECORTE DE /run (A8): suma portadas y miniaturas de Spotify y borra lo mas viejo si pasan del tope
    internal static class RocolaOsSpotifyCatalogoRecorte
    {
        internal const long TopeBytes = 150L * 1024 * 1024;
        internal const long CadaMs = 3600000, RecienteMs = 30 * 60 * 1000;
        private static long ultimo = long.MinValue / 2;
        private static int corriendo;
        // desde la pantalla: toca? (una vez por hora; la primera vez de la sesion, al abrir el buscador)
        internal static bool Toca()
        {
            long ahora = Environment.TickCount64;
            if (ahora - ultimo < CadaMs || Volatile.Read(ref corriendo) != 0) return false;
            ultimo = ahora;
            return true;
        }
        internal static void Lanza(HashSet<string> protegidas, Action<HashSet<string>> alTerminar)
        {
            if (Interlocked.Exchange(ref corriendo, 1) != 0) return;
            RocolaOs.Hilo("spotify_recorte", delegate
            {
                try
                {
                    string portadas = RocolaOs.Run("rocola-xp/spotify/portadas"), mini = RocolaOs.Run("rocola-xp/spotify/miniaturas");
                    long tope = TopeBytes;
                    string env = Environment.GetEnvironmentVariable("ROCOLA_SPOTIFY_RECORTE_BYTES");   // (las pruebas)
                    long t; if (!string.IsNullOrEmpty(env) && long.TryParse(env, NumberStyles.Integer, CultureInfo.InvariantCulture, out t) && t > 0) tope = t;
                    HashSet<string> borradas = Recorta(portadas, mini, protegidas, tope, RecienteMs);
                    if (borradas.Count > 0) alTerminar(borradas);
                }
                catch (Exception ex) { RocolaOs.Falla("spotify_recorte", "al recortar", ex); }
                finally { Volatile.Write(ref corriendo, 0); }
            });
        }
        // devuelve las rutas de PORTADAS borradas (las miniaturas no se nombran: XP las rehace si faltan)
        internal static HashSet<string> Recorta(string portadas, string miniaturas, HashSet<string> protegidas, long tope, long recienteMs)
        {
            HashSet<string> borradas = new HashSet<string>(StringComparer.Ordinal);
            List<KeyValuePair<DateTime, FileInfo>> l = new List<KeyValuePair<DateTime, FileInfo>>();
            long total = 0;
            foreach (string dir in new[] { portadas, miniaturas })
            {
                if (dir == null || !Directory.Exists(dir)) continue;
                foreach (string ruta in Directory.EnumerateFiles(dir))
                {
                    string n = Path.GetFileName(ruta);
                    if (n.IndexOf(".t", StringComparison.Ordinal) >= 0 || n.EndsWith(".n", StringComparison.Ordinal)) continue;   // (las que se estan bajando)
                    try
                    {
                        FileInfo f = new FileInfo(ruta);
                        DateTime cuando = f.LastWriteTimeUtc > f.LastAccessTimeUtc ? f.LastWriteTimeUtc : f.LastAccessTimeUtc;
                        l.Add(new KeyValuePair<DateTime, FileInfo>(cuando, f));
                        total += f.Length;
                    }
                    catch { }
                }
            }
            if (total <= tope) return borradas;
            l.Sort((a, b) => a.Key.CompareTo(b.Key));
            DateTime limite = DateTime.UtcNow.AddMilliseconds(-recienteMs);
            string dirPortadas = portadas == null ? null : Path.GetFullPath(portadas).TrimEnd('/');
            foreach (KeyValuePair<DateTime, FileInfo> kv in l)
            {
                if (total <= tope || kv.Key > limite) break;   // (de ahi en adelante todo es reciente)
                bool dePortada = dirPortadas != null && string.Equals(kv.Value.DirectoryName, dirPortadas, StringComparison.Ordinal);
                if (dePortada && protegidas != null && (protegidas.Contains(kv.Value.FullName) || protegidas.Contains(Path.Combine(portadas, kv.Value.Name)))) continue;
                try { long len = kv.Value.Length; kv.Value.Delete(); total -= len; if (dePortada) borradas.Add(Path.Combine(portadas, kv.Value.Name)); }
                catch { }
            }
            return borradas;
        }
    }
}

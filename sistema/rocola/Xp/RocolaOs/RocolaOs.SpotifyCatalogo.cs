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
            if (q.Length < 3 || !RocolaOsSpotifyCatalogoApi.HayLlave) { RocolaOsSpotifyCatalogoApi.OlvidaBusqueda(); return res; }
            RocolaOsSpotifyCatalogoBusqueda b = RocolaOsSpotifyCatalogoApi.BuscaYa(q);
            if (b != null)
            {
                // (revision del asistente) lo contestado de memoria (lo mismo escrito otra vez, o AL VOLVER de una pared o de un disco) tambien
                // cuenta en la casilla: XP acaba de poner solo los suyos y, con todo de Spotify, decia «Sin resultados» encima de las tarjetas
                List<SearchItem> m = RocolaOsSpotifyCatalogoMezcla(res, b);
                CuentaResultados(consulta, (lastMatches != null ? lastMatches.Count : 0) + m.Count - res.Count);
                return m;
            }
            RocolaOsSpotifyCatalogoApi.Busca(q, delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:resultados", delegate { RocolaOsSpotifyCatalogoLlegoBusqueda(q, r as RocolaOsSpotifyCatalogoBusqueda, error); });
            });
            return res;
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
                if (error != null && error != "sin_llave") RocolaOsSpotifyCatalogoApi.ApuntaCallado("busqueda sin respuesta: " + error);
                return;
            }
            if (!searchOpen || rocolaOsSpConsulta != q || rocolaOsSpXp == null) return;   // ya se escribio otra cosa (o se cerro)
            List<SearchItem> antes = searchOverlay.Results;
            List<SearchItem> m = RocolaOsSpotifyCatalogoMezcla(rocolaOsSpXp, b);
            // quien ya andaba entre los resultados no pierde su sitio: lo de Spotify entra debajo de las primeras de XP
            if (searchOverlay.FocusResults && antes != null && searchOverlay.SelResult >= 0 && searchOverlay.SelResult < antes.Count)
            {
                int n = m.IndexOf(antes[searchOverlay.SelResult]);
                if (n >= 0) searchOverlay.SelResult = n;
            }
            searchOverlay.Results = m;
            CuentaResultados(rocolaOsSpEscrito, (lastMatches != null ? lastMatches.Count : 0) + m.Count - rocolaOsSpXp.Count);   // (la casilla dice cuantos hay, con los de Spotify)
            searchOverlay.DatosVer++;   // (la cache de las tarjetas se rehace: la consulta es la misma)
            searchOverlay.Invalidate();
            RocolaOsSpotifyCatalogoApi.PideEstado();
        }
        private List<SearchItem> RocolaOsSpotifyCatalogoMezcla(List<SearchItem> xp, RocolaOsSpotifyCatalogoBusqueda b)
        {
            List<SearchItem> artistas;
            Dictionary<string, SearchItem> mas;
            RocolaOsSpotifyParedesArtistas(b, out artistas, out mas);   // (RocolaOs.SpotifyCatalogoParedes.cs)
            List<SearchItem> sp = new List<SearchItem>();
            int n = 0;
            foreach (RocolaOsSpotifyCatalogoCancion c in b.Canciones)
            {
                if (n >= 8) break;
                Album a = RocolaOsSpotifyCatalogoDiscoDe(c);
                Track t = RocolaOsSpotifyCatalogoPista(c);
                sp.Add(new SearchItem { Kind = 2, Display = (c.Artista.Length > 0 ? c.Artista + " - " : "") + c.Titulo, Key = "", AlbumRef = a, TrackRef = t });
                n++;
            }
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
            // primero (si no hay, arriba); y detras de cada artista tuyo que Spotify tambien tiene, «ARTISTA · mas en Spotify»
            int lideres = 0;
            while (lideres < xp.Count && xp[lideres] != null && xp[lideres].Kind == 0) lideres++;
            List<SearchItem> m = new List<SearchItem>(xp.Count + sp.Count + artistas.Count + mas.Count);
            int arriba = Math.Min(RocolaOsSpotifyXpArriba, xp.Count);
            if (lideres == 0) m.AddRange(artistas);
            for (int i = 0; i < xp.Count; i++)
            {
                SearchItem it = xp[i];
                m.Add(it);
                SearchItem otro;
                if (it != null && it.Kind == 0 && mas.Count > 0 && mas.TryGetValue(MotorBusqueda.Normaliza(it.Display ?? ""), out otro)) m.Add(otro);
                if (i == lideres - 1) m.AddRange(artistas);
                if (i == arriba - 1) m.AddRange(sp);
            }
            if (arriba == 0) m.AddRange(sp);
            return m;
        }

        // g41 (Form1.Biblioteca.cs, ChooseSearchResult: «if (it == null) return;»): ELEGIR UN RESULTADO DE SPOTIFY. true = ya lo atendio.
        private bool RocolaOsSpotifyCatalogoElige(SearchItem it)
        {
            if (it == null || !RocolaOsEsSpotify(it.AlbumRef) || !RocolaOsSpotifyCatalogoVa) return false;
            MarkSearchActivity();
            if (it.Kind == 2 && it.TrackRef != null) { RocolaOsSpotifyCatalogoPide(it.TrackRef, it.AlbumRef); return true; }   // (no cierra el buscador: como XP)
            if (RocolaOsSpotifyParedesElige(it)) return true;   // un artista, «mas en Spotify» o «Todos los discos de...»: su pared
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
            string d = RocolaOsSpotifyCatalogoPortadas.Carpeta;
            if (d == null || portada == null || !portada.StartsWith(d, StringComparison.Ordinal)) return deXp;
            return Path.Combine(Path.GetDirectoryName(d.TrimEnd('/')) ?? d, "miniaturas", Path.GetFileName(deXp));
        }

        // g50 (PreviewsIndexadosGpu.cs, IndexedSave): la previa de 32 px de una portada de Spotify no va a previews32-v1.bin (en el disco)
        internal static bool RocolaOsSpotifyCatalogoEsMiniatura(string identidad)
        {
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
            if (!rocolaOsSpCuentaPedida && RocolaOsSpotifyCatalogoApi.HayCuenta)
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
                if (a == null || rocolaOsSpDeArtista.ContainsKey(a)) a = RocolaOsSpotifyCatalogoDiscoDe(c);
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
            Action<object, string> listo = delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:disco", delegate { RocolaOsSpotifyCatalogoLlegoDisco(a, r as RocolaOsSpotifyCatalogoDisco, error, desdeBuscador, yo, enPared); });
            };
            if (d.Tipo == "playlist") RocolaOsSpotifyCatalogoApi.AbreLista(d.Id, listo);
            else if (d.Tipo == "album") RocolaOsSpotifyCatalogoApi.AbreDisco(d.Id, listo);
        }
        private void RocolaOsSpotifyCatalogoLlegoDisco(Album a, RocolaOsSpotifyCatalogoDisco d, string error, bool desdeBuscador, int yo, bool enPared)
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
            if (desdeBuscador) { if (searchOpen) RocolaOsSpotifyCatalogoEntraDesdeBuscador(a, enPared); return; }
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

        // ================================================================= las portadas ===============================================
        // Se bajan a /run/rocola-xp/spotify/portadas/<id>.jpg (en memoria; nunca al disco), una a la vez y detras de la API (no gastan su
        // cuota: vienen del servidor de imagenes de Spotify), y al llegar el disco toma su ruta y XP la lee como cualquier portada.
        private Dictionary<string, List<Album>> rocolaOsSpEsperanPortada;   // ruta -> los discos que la esperan

        private void RocolaOsSpotifyCatalogoPortada(Album a, string id, string url)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(id)) return;
            string nombre = id;
            foreach (char ch in id) if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')) { nombre = "x" + ((uint)id.GetHashCode()).ToString("x8", CultureInfo.InvariantCulture); break; }
            string ruta = Path.Combine(RocolaOs.Run("rocola-xp/spotify/portadas"), nombre + ".jpg");
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
            if (searchOpen) { searchOverlay.DatosVer++; searchOverlay.Invalidate(); }
            if (albumView != null && albumView.Visible) albumView.Invalidate();
        }
    }

    // EL HILO DE LAS PORTADAS DE SPOTIFY: una a la vez, de su servidor de imagenes (no gasta la cuota de la API), a /run; al llegar, la
    // pantalla le pone la ruta al disco (encargo «RocolaOs:spotify_catalogo:portada»)
    internal static class RocolaOsSpotifyCatalogoPortadas
    {
        private static readonly object cerrojo = new object();
        private static readonly Queue<KeyValuePair<string, string>> cola = new Queue<KeyValuePair<string, string>>();
        private static readonly AutoResetEvent hay = new AutoResetEvent(false);
        private static Thread hilo;
        private static string carpeta;
        internal static long Bajadas;
        // (revision cuota-hilos-y-fallos de paredes-buscador, 2a pasada) LA QUE NO SE BAJO POR LA RED (sin red, el servidor caido: 5xx, 429):
        // antes se quedaba sin portada toda la sesion (el disco ya no la vuelve a pedir: queda esperandola). Ahora, otra vez a los 15 s y, si
        // tampoco, a los 60 s (al servidor de imagenes: no gasta la cuota de la API); despues, ya no. Un 404 o una de mas de 4 MB, no.
        private static readonly int[] reintentosMs = { 15000, 60000 };
        private static readonly List<KeyValuePair<long, KeyValuePair<string, string>>> luego = new List<KeyValuePair<long, KeyValuePair<string, string>>>();
        private static readonly Dictionary<string, int> intentos = new Dictionary<string, int>(StringComparer.Ordinal);
        // /run/rocola-xp/spotify/portadas/ (con «/» al final), desde que se baja la primera (antes, null: g43 no cambia nada)
        internal static string Carpeta { get { return Volatile.Read(ref carpeta); } }

        internal static void Baja(string url, string ruta)
        {
            lock (cerrojo)
            {
                cola.Enqueue(new KeyValuePair<string, string>(url, ruta));
                if (hilo == null) hilo = RocolaOs.Hilo("spotify_portadas", Corre);
            }
            hay.Set();
        }
        private static void Corre()
        {
            while (true)
            {
                KeyValuePair<string, string> p;
                int espera = 10000;
                lock (cerrojo)
                {
                    long ahora = Environment.TickCount64;
                    for (int i = luego.Count - 1; i >= 0; i--)
                        if (luego[i].Key <= ahora) { cola.Enqueue(luego[i].Value); luego.RemoveAt(i); }
                        else espera = (int)Math.Max(1, Math.Min(espera, luego[i].Key - ahora));
                    p = cola.Count == 0 ? default(KeyValuePair<string, string>) : cola.Dequeue();
                }
                if (p.Key == null) { hay.WaitOne(espera); continue; }
                bool bien = false, otraVez = false;
                try
                {
                    string dir = Path.GetDirectoryName(p.Value);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string mini = Path.Combine(Path.GetDirectoryName(dir), "miniaturas");
                    if (!Directory.Exists(mini)) Directory.CreateDirectory(mini);
                    Volatile.Write(ref carpeta, dir.TrimEnd('/') + "/");
                    using (HttpResponseMessage r = RocolaOsSpotifyCatalogoApi.Http.Send(new HttpRequestMessage(HttpMethod.Get, p.Key)))
                    {
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
                            string t = p.Value + ".t";
                            File.WriteAllBytes(t, b);
                            File.Move(t, p.Value, true);
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
                        int n; intentos.TryGetValue(p.Value, out n);
                        if (n < reintentosMs.Length) { intentos[p.Value] = n + 1; luego.Add(new KeyValuePair<long, KeyValuePair<string, string>>(Environment.TickCount64 + reintentosMs[n], p)); }
                    }
                if (bien)
                {
                    string ruta = p.Value;
                    RocolaOs.Encarga("spotify_catalogo:portada", delegate { Form1 f = RocolaOs.LaRocola; if (f != null) f.RocolaOsSpotifyCatalogoLlegoPortada(ruta); });
                }
            }
        }
    }
}

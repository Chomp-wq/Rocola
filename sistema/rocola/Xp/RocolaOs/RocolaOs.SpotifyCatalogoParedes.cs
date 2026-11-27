// LAS PAREDES DEL BUSCADOR (fase 11, grupo paredes-buscador; FASE11_paredes-buscador.md). Una parte de Form1, encima del catalogo
// (RocolaOs.SpotifyCatalogo*.cs). La decision del dueno del 15-11-2026 («crear paredes de discos que solo son accesibles desde el buscador,
// para que todo el catalogo de Spotify sea accesible de la manera mas comoda») y sus ideas aprobadas, y lo del buscador del 7-11-2026:
//   - LA PARED DE UN ARTISTA que solo esta en Spotify: la rejilla de discos de XP (18 por pagina, sus teclas y sus portadas), fuera de la
//     barra de letras (una «letra» que la barra no tiene: RocolaOsSpotifyParedMarca), con su nombre en la cabecera (como «VIDEOS»). La
//     casilla 1 es «Canciones de ARTISTA» (un disco de XP con sus canciones en el orden de la busqueda artist:"..."; se piden al entrar, de 10
//     en 10, hasta 50); luego sus discos y sencillos, de 10 en 10, pidiendo solo la pagina que se mira.
//   - LA PARED UNIDA (23-11-2026, el dueno: «unificar los discos locales con los de Spotify»; sustituye a «ARTISTA · mas en Spotify»): la
//     tarjeta de XP de un artista TUYO que Spotify tambien tiene abre una pared del buscador con TUS discos primero (en el orden de XP),
//     luego «Canciones de ARTISTA» y los discos de Spotify que no tienes. Se abre al momento con los tuyos; lo de Spotify llega detras.
//   - SIEMPRE UNA PARED DEL BUSCADOR (23-11-2026, el dueno: «siempre si se escoge la opcion de artista o album en el buscador, se abra ya sea
//     una pared de discos temporal, o el disco dentro del buscador»): sustituye a «la pared de verdad primero» y a «si ya se sabe que los
//     tienes todos, lo de XP». Tu artista abre la pared unida (si Spotify lo tiene) o una pared solo con tus discos (si no, sin pedir nada y
//     sin el icono de Spotify); tu disco se abre dentro del buscador (REGRESAR vuelve a el).
//   - «Todos los discos de «lo escrito»»: la ultima tarjeta de los discos de Spotify del buscador; una pared con todos los discos de la
//     busqueda, de 10 en 10 (hasta 1000).
//   - REGRESAR (la pila, como mucho 4): un disco -> su pared, en la misma pagina y casilla (eso ya lo hace XP: BackToAlbums) -> el buscador,
//     con lo escrito y la misma eleccion (g53). Un disco de Spotify abierto desde el buscador que no estaba en la letra ♫ -> el buscador (g54).
//   - SP9, los atajos del buscador: «Tus mas escuchadas» y «Escuchado hace poco», como los de XP (g55, g56); solo con la cuenta.
// Todo con lo que XP ya dibuja: la rejilla, la cabecera con su texto, las tarjetas y los atajos del buscador. Nada de zoom ni de marcas
// nuevas (son de la presentacion del final). Una casilla cuya pagina aun no llega es un disco sin nombre ni portada (lo de XP con un disco
// asi) y se llena al llegar.
// LA CUOTA: cada pagina de 18 casillas pide lo que le falta (1 o 2 peticiones de 10), solo al mirarla; nada de fondo; lo contestado se queda en
// memoria la sesion (el cliente del catalogo: su cola de una peticion, su freno de 429). Con 429, sin red o sin la llave: lo que no llega
// no se ensena (el aviso de XP «Espera un momento y vuelve a intentarlo»), y sin el modo nada de esto existe: cada gancho devuelve lo de XP.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rocola
{
    public partial class Form1
    {
        // la «letra» de las paredes del buscador: no esta en la barra (ni en LetrasOrden), asi que la barra no la ensena ni llega a ella
        internal const char RocolaOsSpotifyParedMarca = '♪';
        private const int RocolaOsSpotifyArtistasMax = 3;     // artistas de Spotify en el buscador (el pedido del 7-11-2026)
        internal const string RocolaOsSpotifyParedTuyos = "tuyos";   // (23-11-2026) la pared de un artista que Spotify no conoce: solo tus discos, sin De ni peticiones
        internal const int RocolaOsAtajoTop = 100, RocolaOsAtajoRecientes = 101;   // (los atajos de XP son 0 a 3)

        // LO QUE HAY QUE DEVOLVER AL REGRESAR AL BUSCADOR: lo escrito, la eleccion, y la pared de debajo como estaba
        private sealed class RocolaOsSpotifyVuelta
        {
            internal string Escrito;
            internal bool EnResultados;
            internal int Sel, Fila, Col;
            internal char Letra;
            internal int Pagina, Elegido;
            internal RocolaOsSpotifyPared Pared;     // (si el buscador se abrio desde otra pared del buscador)
        }

        // UNA PARED DEL BUSCADOR
        private sealed class RocolaOsSpotifyPared
        {
            internal string Tipo;                    // ParedArtista (un artista; tambien la pared unida) o ParedTodos
            internal string De;                      // el id del artista, o lo escrito (la clave de la busqueda)
            internal string Cabecera;
            internal Album Canciones;                // «Canciones de ARTISTA» (null en la de todos los discos)
            internal HashSet<string> Excluir;        // (la pared unida) los nombres de tus discos, sin acentos ni mayusculas
            internal readonly HashSet<Album> EdicionesDichas = new HashSet<Album>();   // (27-11-2026) las ediciones quitadas ya apuntadas en la bitacora
            internal HashSet<string> ExcluirClaves;  // (27-11-2026) lo mismo sin lo que dice «edicion» (RocolaOs.SpotifyEdiciones.cs): tu «Abbey Road» y su «(Remastered)» son uno
            internal List<Album> Tuyos;              // (la pared unida) tus discos de ese artista, en el orden de XP: van primero
            internal int Total = -1;                 // lo que dice Spotify (-1: aun no se sabe)
            internal readonly Dictionary<int, List<Album>> Bloques = new Dictionary<int, List<Album>>();   // de 10 en 10
            internal readonly Dictionary<int, List<Album>> Huecos = new Dictionary<int, List<Album>>();
            internal readonly Dictionary<Album, int> HuecoDe = new Dictionary<Album, int>();
            internal readonly HashSet<int> Pedidos = new HashSet<int>(), Fallados = new HashSet<int>();
            internal List<Album> Lista = new List<Album>();
            internal int PaginaMirada = -1;
            internal RocolaOsSpotifyVuelta Vuelta;
            // (revision cuota-hilos-y-fallos) la pagina que se mira, para el hilo del catalogo (-1: la pared ya no se ve), y la primera y la
            // ultima casilla de cada bloque por llenar: una pagina pedida que ya nadie mira se deja en la cola sin preguntar a la red
            internal volatile int PaginaPub;
            internal readonly Dictionary<int, int> Inicio = new Dictionary<int, int>(), Fin = new Dictionary<int, int>();
            // (revision cuota-hilos-y-fallos, 2a pasada) hasta donde llego cada bloque contestado (su offset + lo que trajo «items»)
            internal readonly Dictionary<int, int> FinCrudo = new Dictionary<int, int>();
        }
        // «Canciones de ARTISTA»: por donde va (se piden de 10 en 10 mientras alguien esta dentro)
        private sealed class RocolaOsSpotifyCancionesDe
        {
            internal string ArtistaId, Nombre;
            internal int Siguiente, Total = -1;
            internal bool Pidiendo, Acabado;
        }
        // lo que abre cada tarjeta nueva del buscador («Todos los discos de...») y la pared unida de tus artistas
        private sealed class RocolaOsSpotifyParedQue
        {
            internal string Tipo, De, Nombre, PortadaUrl;
            internal HashSet<string> Excluir;
        }

        private RocolaOsSpotifyPared rocolaOsSpPared;                 // la pared abierta (la de RocolaOsSpotifyParedMarca)
        private RocolaOsSpotifyVuelta rocolaOsSpVueltaDisco;          // (a) el disco abierto desde el buscador que no estaba en la letra
        private Album rocolaOsSpVueltaAlbum;
        private Album rocolaOsSpEntrandoHueco;                        // se entro a una casilla que aun no llega
        private Dictionary<Album, RocolaOsSpotifyCancionesDe> rocolaOsSpCancionesDe;
        private Dictionary<Album, RocolaOsSpotifyParedQue> rocolaOsSpParedDe;
        private Dictionary<string, RocolaOsSpotifyParedQue> rocolaOsSpUnidas;   // (23-11-2026) tus artistas que Spotify tambien tiene, sin acentos ni mayusculas
        private HashSet<string> rocolaOsSpTuyos;                      // tus artistas, sin acentos ni mayusculas
        private Dictionary<string, HashSet<string>> rocolaOsSpTusDiscos;   // (revision) de cada uno de tus artistas, los nombres de sus discos
        private int rocolaOsSpTuyosDe = -1;
        private List<Album> rocolaOsSpTuyosLista;                     // (la lista de la que salieron: un re-escaneo hace otra)
        private int rocolaOsSpTragaTick;                              // (revision) se volvio al buscador con REGRESAR: su repeticion no borra
        private bool rocolaOsSpAtajosPedidos;
        private int rocolaOsSpAtajosOtraVez;                          // (revision) un atajo no llego por la red: se vuelve a pedir desde aqui
        private int rocolaOsSpHuecos;

        // ================================================================= el buscador =================================================

        // LOS ARTISTAS DE SPOTIFY DE UNA BUSQUEDA (lo llama la mezcla del catalogo): los que NO estan en la rocola (ni por el artista de un
        // disco tuyo, sin acentos ni mayusculas), hasta 3: el primero si su nombre se parece a lo escrito (lo contiene, esta contenido o a dos
        // letras; la idea del programa anterior, Motor/Spotify.cs), los demas si lo contienen. Los que SI estan: su tarjeta de XP abre la pared
        // unida (23-11-2026; antes salia otra tarjeta detras, «ARTISTA · mas en Spotify», que el dueno vio confusa).
        private void RocolaOsSpotifyParedesArtistas(RocolaOsSpotifyCatalogoBusqueda b, out List<SearchItem> artistas)
        {
            artistas = new List<SearchItem>();
            string q = MotorBusqueda.Normaliza(b.Consulta ?? "");
            HashSet<string> tuyos = RocolaOsSpotifyTuyos();
            bool primero = true;
            foreach (RocolaOsSpotifyCatalogoArtista ar in b.Artistas)
            {
                bool esPrimero = primero; primero = false;
                string nn = MotorBusqueda.Normaliza(ar.Nombre ?? "");
                if (nn.Length == 0 || q.Length == 0) continue;
                if (tuyos.Contains(nn))
                {
                    RocolaOsSpotifyApuntaUnida(ar, nn);
                    continue;
                }
                if (artistas.Count >= RocolaOsSpotifyArtistasMax) continue;
                bool casa = nn.Contains(q) || (esPrimero && (q.Contains(nn) || RocolaOsSpotifyDistancia(nn, q, 2) <= 2));
                if (!casa) continue;
                Album a = RocolaOsSpotifyCatalogoArtistaAlbum(ar);
                artistas.Add(new SearchItem { Kind = 0, Display = ar.Nombre, Key = "", AlbumRef = a, Albums = new List<Album> { a } });
            }
        }
        // tus artistas (los de tus discos), sin acentos ni mayusculas; se rehace si cambia la biblioteca
        private HashSet<string> RocolaOsSpotifyTuyos()
        {
            if (rocolaOsSpTuyos != null && (libBuilding || (rocolaOsSpTuyosDe == albums.Count && ReferenceEquals(rocolaOsSpTuyosLista, albums)))) return rocolaOsSpTuyos;
            // (revision cuota-hilos-y-fallos) de una pasada, tambien los nombres de los discos de cada artista: antes, cada busqueda con
            // artistas tuyos recorria toda la biblioteca dos veces por artista, en el hilo de la pantalla (10 artistas y 1500 discos: 30 000
            // normalizaciones por cada tecla con la busqueda de Spotify ya sabida)
            HashSet<string> h = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, HashSet<string>> dd = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (!libBuilding)
                foreach (Album a in albums)
                {
                    if (a == null || a.IsTop || RocolaOsEsSpotify(a)) continue;
                    string art = MotorBusqueda.Normaliza(a.Artist ?? "");
                    if (art.Length > 0) h.Add(art);
                    HashSet<string> sus;
                    if (!dd.TryGetValue(art, out sus)) { sus = new HashSet<string>(StringComparer.Ordinal); dd[art] = sus; }
                    sus.Add(MotorBusqueda.Normaliza(string.IsNullOrEmpty(a.Name) ? (a.Artist ?? "") : a.Name));
                }
            rocolaOsSpTuyos = h; rocolaOsSpTusDiscos = dd; rocolaOsSpTuyosDe = libBuilding ? -1 : albums.Count; rocolaOsSpTuyosLista = albums;
            return h;
        }
        // los nombres de tus discos de ese artista, sin acentos ni mayusculas. XP saca artista y disco del nombre de la carpeta («Artista -
        // Disco», SplitArtist); una carpeta sin « - » es solo «artista» y su disco no tiene nombre: entonces cuenta su nombre (un disco con
        // el nombre del artista)
        // (de RocolaOsSpotifyTuyos, hecho de una pasada; solo se lee: lo comparten la tarjeta y su pared)
        private HashSet<string> RocolaOsSpotifyTusDiscos(string artistaNormalizado)
        {
            RocolaOsSpotifyTuyos();
            HashSet<string> h;
            if (libBuilding || rocolaOsSpTusDiscos == null || !rocolaOsSpTusDiscos.TryGetValue(artistaNormalizado, out h)) return new HashSet<string>(StringComparer.Ordinal);
            return h;
        }
        // (23-11-2026) TU ARTISTA QUE SPOTIFY TAMBIEN TIENE: se apunta su pared unida (la abre tu tarjeta de XP). Ya SIN el corte de antes
        // («si ya se saben todos sus discos y los tienes todos, tu tarjeta hace lo de XP», RocolaOsSpotifyHayMas, quitada): el dueno pidio que
        // elegir un artista en el buscador abra SIEMPRE una pared del buscador; con todos tuyos queda tuyos + «Canciones de ARTISTA» y lo ya
        // sabido sale de memoria (0 peticiones). El primero que da Spotify con ese nombre manda (el mas conocido).
        private void RocolaOsSpotifyApuntaUnida(RocolaOsSpotifyCatalogoArtista ar, string nn)
        {
            if (string.IsNullOrEmpty(ar.Id)) return;
            if (rocolaOsSpUnidas == null) rocolaOsSpUnidas = new Dictionary<string, RocolaOsSpotifyParedQue>(StringComparer.Ordinal);
            RocolaOsSpotifyParedQue q;
            if (rocolaOsSpUnidas.TryGetValue(nn, out q) && q.De != ar.Id) return;   // (ya hay otro con ese nombre, de una busqueda de antes)
            if (rocolaOsSpUnidas.Count > 500) rocolaOsSpUnidas.Clear();   // (sin crecer toda la sesion)
            rocolaOsSpUnidas[nn] = new RocolaOsSpotifyParedQue { Tipo = "unida", De = ar.Id, Nombre = ar.Nombre ?? "", PortadaUrl = ar.PortadaUrl, Excluir = RocolaOsSpotifyTusDiscos(nn) };
        }
        // la pared unida de esa tarjeta: tu tarjeta de artista (de XP) con un artista de Spotify apuntado; null si no
        private RocolaOsSpotifyParedQue RocolaOsSpotifyUnidaDe(SearchItem it)
        {
            if (it == null || it.Kind != 0 || it.AlbumRef == null || rocolaOsSpUnidas == null || RocolaOsEsSpotify(it.AlbumRef) || !RocolaOsSpotifyCatalogoVa) return null;
            RocolaOsSpotifyParedQue q;
            return rocolaOsSpUnidas.TryGetValue(MotorBusqueda.Normaliza(it.Display ?? ""), out q) ? q : null;
        }
        // (para el adelanto del buscador) el id del artista de Spotify de su pared unida, o null
        private string RocolaOsSpotifyUnidaId(SearchItem it)
        {
            RocolaOsSpotifyParedQue q = RocolaOsSpotifyUnidaDe(it);
            return q == null ? null : q.De;
        }
        // tus discos de ese artista (nombre normalizado), en el orden de XP (la biblioteca ya va asi)
        private List<Album> RocolaOsSpotifyTusAlbumes(string nn)
        {
            List<Album> tuyos = new List<Album>();
            if (!libBuilding)
                foreach (Album a in albums)
                    if (a != null && !a.IsTop && !RocolaOsEsSpotify(a) && MotorBusqueda.Normaliza(a.Artist ?? "") == nn) tuyos.Add(a);
            return tuyos;
        }
        // (23-11-2026, el dueno: «siempre si se escoge la opcion de artista o album en el buscador, se abra ya sea una pared de discos
        // temporal, o el disco dentro del buscador») ELEGIR TU TARJETA DE ARTISTA (desde g41): SIEMPRE una pared del buscador, nunca la pared
        // de verdad. Si Spotify lo tiene (hay pared unida apuntada): la unida (tus discos, «Canciones de» y los que no tienes; con todos tuyos,
        // sin mas de Spotify). Si no (Spotify no lo conoce o aun no llego su busqueda): una pared SOLO con tus discos, al momento, sin pedir
        // nada y sin el icono de Spotify. true = la abrio; false (ningun disco tuyo, raro) = lo de XP.
        private bool RocolaOsSpotifyArtistaTuyoElige(SearchItem it)
        {
            if (it == null || it.Kind != 0 || it.AlbumRef == null || RocolaOsEsSpotify(it.AlbumRef) || !RocolaOsSpotifyCatalogoVa) return false;
            string nn = MotorBusqueda.Normaliza(it.Display ?? "");
            List<Album> tuyos = RocolaOsSpotifyTusAlbumes(nn);
            if (tuyos.Count == 0) return false;
            MarkSearchActivity(); ApuntaBusqueda(it);   // (lo de XP con un artista: «Buscaste: ...» en los atajos)
            RocolaOsSpotifyParedQue q = RocolaOsSpotifyUnidaDe(it);
            string cab = "DISCOS · " + (it.Display ?? "").ToUpperInvariant();
            if (q != null) RocolaOsSpotifyAbrePared(RocolaOsSpotifyCatalogoApi.ParedArtista, q.De, q.Nombre, q.PortadaUrl, q.Excluir, "DISCOS · " + (it.Display ?? q.Nombre).ToUpperInvariant(), tuyos);
            else RocolaOsSpotifyAbrePared(RocolaOsSpotifyParedTuyos, null, null, null, null, cab, tuyos);
            return true;
        }
        // (23-11-2026, el mismo pedido) ELEGIR UN DISCO TUYO en el buscador (desde g41): se abre dentro del buscador, como uno de Spotify que no
        // esta en la letra: REGRESAR vuelve al buscador con lo escrito y la misma eleccion (g54), y a los 30 s sin tocar se cierra todo
        // (RocolaOsSpotifyDiscoInactivo). Lo de XP (MarkSearchActivity, ApuntaBusqueda, la animacion desde el cubo del buscador) se conserva;
        // NO va a los recientes de Spotify (son solo de discos de Spotify). true = lo abrio.
        private bool RocolaOsSpotifyDiscoTuyoElige(SearchItem it)
        {
            if (it == null || it.Kind != 1 || it.AlbumRef == null || RocolaOsEsSpotify(it.AlbumRef) || !RocolaOsSpotifyCatalogoVa || !searchOpen) return false;
            Album a = it.AlbumRef;
            MarkSearchActivity(); ApuntaBusqueda(it);
            RocolaOsSpotifyVuelta v = RocolaOsSpotifyGuardaBuscador();
            transNextFrom = SearchCubeRect(out transNextAngle);   // la portada sale del CUBO GRANDE del buscador (luego ya no existe)
            CloseSearch(); NavigateToAlbum(a); EnterAlbum(a);
            rocolaOsSpVueltaDisco = v; rocolaOsSpVueltaAlbum = a;   // (despues de EnterAlbum: g49 la borra al entrar a otro disco)
            RocolaOs.Apunta("spotify_catalogo", "disco tuyo abierto desde el buscador (REGRESAR vuelve a el)");
            return true;
        }
        // (23-11-2026) LA PARED DE VERDAD (SIN USO desde la peticion del dueno del 23-11-2026 «siempre [...] una pared de discos temporal»: el
        // buscador ya no lleva a la pared de verdad; se deja por si el dueno vuelve a pedirlo, ver FASE11_paredes-buscador.md): con la funcion «pared» encendida (el catalogo en la pared de XP) y la letra ya armada, el primer disco
        // de ese artista en ella si alli hay discos suyos del catalogo; null si no (sin la funcion, sin armar la letra o sin discos suyos del
        // catalogo: entonces su pared del buscador). Solo mira la memoria, no pide nada.
        private Album RocolaOsSpotifyEnLaParedReal(string nn, char letra)
        {
            if (nn.Length == 0 || !rocolaOsPgArrancada || !RocolaOsPgVa || !RocolaOsPgLetraSuya(letra) || rocolaOsPgListas == null) return null;
            if (!ReferenceEquals(albums, rocolaOsPgAlbumsXp) || libBuilding) return null;
            RocolaOsPgLetra e = null;
            for (int i = rocolaOsPgListas.Count - 1; i >= 0; i--) if (rocolaOsPgListas[i].Letra == letra) { e = rocolaOsPgListas[i]; break; }
            if (e == null || e.Lista == null || e.Catalogo == null || !ReferenceEquals(e.De, rocolaOsPgCat)) return null;
            Album primero = null;
            foreach (Album a in e.Lista)
            {
                if (a == null || MotorBusqueda.Normaliza(a.Artist ?? "") != nn) continue;
                if (primero == null) primero = a;
                if (e.Catalogo.ContainsKey(a)) return primero;
            }
            return null;
        }
        // «Todos los discos de «lo escrito»»: una tarjeta de disco de XP (su portada, la del primer disco de la busqueda), si hubo discos
        private SearchItem RocolaOsSpotifyParedesTodosTarjeta(RocolaOsSpotifyCatalogoBusqueda b)
        {
            if (b.Discos.Count == 0 || string.IsNullOrEmpty(b.Consulta)) return null;
            string escrito = (rocolaOsSpEscrito ?? b.Consulta).Trim();
            if (escrito.Length == 0) escrito = b.Consulta;
            // (la carpeta sin palabras: XP adivina el genero por el nombre de la carpeta)
            uint h = 2166136261; foreach (char ch in b.Consulta) { h ^= ch; h *= 16777619; }   // (FNV-1a: la misma en cada sesion)
            string clave = "todos:" + h.ToString("x8", CultureInfo.InvariantCulture);
            Album a;
            if (!rocolaOsSpDiscos.TryGetValue(clave, out a))
            {
                a = new Album { Folder = RocolaOsSpotifyCarpeta + clave, Name = "Todos los discos de «" + escrito + "»", Artist = "", Letter = RocolaOsSpotifyMarca };
                rocolaOsSpDiscos[clave] = a;
                RocolaOsSpotifyCatalogoPortada(a, b.Discos[0].Id, b.Discos[0].PortadaUrl);   // (la del primer disco: el mismo archivo)
            }
            if (rocolaOsSpParedDe == null) rocolaOsSpParedDe = new Dictionary<Album, RocolaOsSpotifyParedQue>();
            rocolaOsSpParedDe[a] = new RocolaOsSpotifyParedQue { Tipo = "todos", De = b.Consulta, Nombre = escrito };
            return new SearchItem { Kind = 1, Display = a.Name, Key = "", AlbumRef = a };
        }
        // la distancia entre dos textos (cambiada, de mas, de menos o dos vecinas al reves), con tope: si pasa de max, max + 1 (la de
        // BuscadorFlexible.Distancia del programa anterior, copiada)
        internal static int RocolaOsSpotifyDistancia(string a, string b, int max)
        {
            int aLen = a.Length, bLen = b.Length;
            if (Math.Abs(aLen - bLen) > max) return max + 1;
            if (aLen > 64 || bLen > 64) return max + 1;
            int[] p2 = new int[bLen + 1], p1 = new int[bLen + 1], cur = new int[bLen + 1];
            for (int j = 0; j <= bLen; j++) p1[j] = j;
            for (int i = 1; i <= aLen; i++)
            {
                cur[0] = i; int fila = i;
                for (int j = 1; j <= bLen; j++)
                {
                    int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = Math.Min(Math.Min(p1[j] + 1, cur[j - 1] + 1), p1[j - 1] + costo);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) v = Math.Min(v, p2[j - 2] + 1);
                    cur[j] = v; if (v < fila) fila = v;
                }
                if (fila > max) return max + 1;
                int[] t = p2; p2 = p1; p1 = cur; cur = t;
            }
            return p1[bLen];
        }

        // ELEGIR EN EL BUSCADOR una tarjeta que abre una pared (desde g41). true = era de esas.
        private bool RocolaOsSpotifyParedesElige(SearchItem it)
        {
            Album a = it.AlbumRef;
            RocolaOsSpotifyParedQue q;
            if (rocolaOsSpParedDe != null && rocolaOsSpParedDe.TryGetValue(a, out q))
            {
                RocolaOsSpotifyAbrePared(RocolaOsSpotifyCatalogoApi.ParedTodos, q.De, null, null, null, "DISCOS · «" + q.Nombre.ToUpperInvariant() + "»", null);   // (22-11-2026, el dueno: «el anuncio Discos-tal artista y al lado un icono de Spotify»; el icono: RocolaOs.ParedLetrero.cs)
                return true;
            }
            RocolaOsSpotifyCatalogoArtista ar;
            if (it.Kind == 0 && rocolaOsSpDeArtista.TryGetValue(a, out ar))
            {
                // (23-11-2026) siempre su pared del buscador (antes, con la funcion «pared», iba a la pared de verdad si sus discos estaban ahi)
                RocolaOsSpotifyAbrePared(RocolaOsSpotifyCatalogoApi.ParedArtista, ar.Id, ar.Nombre ?? "", ar.PortadaUrl, null, "DISCOS · " + (ar.Nombre ?? "").ToUpperInvariant(), null);
                return true;
            }
            return false;
        }

        // ================================================================= abrir una pared ============================================

        // Se pide lo de su primera pagina (1 o 2 peticiones) y se abre al llegar, si nadie eligio otra cosa mientras tanto; si no llega, el
        // aviso de XP y el buscador se queda como estaba. La pared unida (con tus discos) se abre al momento y lo de Spotify llega detras.
        private void RocolaOsSpotifyAbrePared(string tipo, string de, string nombre, string portadaUrl, HashSet<string> excluir, string cabecera, List<Album> tuyos)
        {
            if (tipo == RocolaOsSpotifyParedTuyos) RocolaOs.Apunta("spotify_catalogo", "artista tuyo: pared del buscador solo con tus discos (sin pedir nada)");
            RocolaOsSpotifyPared p = new RocolaOsSpotifyPared { Tipo = tipo, De = de, Cabecera = cabecera, Excluir = excluir, Tuyos = tuyos };
            if (tipo == RocolaOsSpotifyCatalogoApi.ParedArtista) p.Canciones = RocolaOsSpotifyCancionesAlbum(de, nombre, portadaUrl);
            if (tipo == RocolaOsSpotifyParedTuyos) p.Total = 0;   // (solo tus discos: ya se sabe todo; Faltan y PideBloque no piden nada sin De)
            p.Vuelta = RocolaOsSpotifyGuardaBuscador();
            int yo = ++rocolaOsSpAbriendo;
            RocolaOsSpotifyParedPrepara(p, yo);
        }
        private void RocolaOsSpotifyParedPrepara(RocolaOsSpotifyPared p, int yo)
        {
            if (yo != rocolaOsSpAbriendo || !searchOpen) return;
            // (23-11-2026, revision) si mientras llegaba se escribio otra cosa (u otro buscador), lo de antes ya no se abre encima
            if (p.Vuelta != null && (searchOverlay.Escrito ?? "") != p.Vuelta.Escrito) return;
            List<int> falta = RocolaOsSpotifyFaltan(p, 0);
            bool unida = p.Tuyos != null && p.Tuyos.Count > 0;
            if (unida)
            {
                // (la pared unida) se abre YA con tus discos (y «Canciones de»); lo de Spotify se pide y, al llegar, la pared se rehace
                // (RocolaOsSpotifyLlegoBloque); si no llega, quedan tus discos, como la pared de XP
                foreach (int b in falta) RocolaOsSpotifyPideBloque(p, b, null);
                RocolaOsSpotifyRecompone(p);
            }
            else
            {
                if (falta.Count > 0)
                {
                    foreach (int b in falta)
                        RocolaOsSpotifyPideBloque(p, b, delegate { if (p.Pedidos.Count == 0) RocolaOsSpotifyParedPrepara(p, yo); });
                    return;
                }
                if (p.Pedidos.Count > 0) return;   // (llega lo que falta y se vuelve aqui)
                if (p.Total < 0)
                {
                    RocolaOsSpotifyCatalogoAviso("Espera un momento y vuelve a intentarlo");
                    return;
                }
            }
            // lo de XP con un artista del buscador (ChooseSearchResult, Kind 0): CloseSearch y NavigateToAlbum, aqui a la pared
            if (rocolaOsSpPared != null && !ReferenceEquals(rocolaOsSpPared, p)) rocolaOsSpPared.PaginaPub = -1;   // (lo que pedia ya no se mira)
            rocolaOsSpPared = p;
            p.PaginaMirada = 0;
            CloseSearch();
            currentLetter = RocolaOsSpotifyParedMarca; page = 0; selAlbumIndex = 0;
            if (panelSongs.Visible) BackToAlbums();
            RefreshAlbums();
            RocolaOs.Apunta("spotify_catalogo", "pared del buscador: " + p.Tipo + " (" + p.Lista.Count.ToString(CultureInfo.InvariantCulture) + " casillas)");
        }

        // g47 (por RocolaOsSpotifyCatalogoSeccion, en RefreshAlbums): LA CABECERA de la pared, y lo que le falta a la pagina que se mira se
        // pide (1 o 2 peticiones de 10). Lo que fallo se vuelve a pedir solo al volver a mirar esa pagina (si no, con 429 o sin red, cada
        // RefreshAlbums lo pediria otra vez).
        private string RocolaOsSpotifyParedMira()
        {
            RocolaOsSpotifyPared p = rocolaOsSpPared;
            if (p.PaginaMirada != page) { p.PaginaMirada = page; p.Fallados.Clear(); }
            p.PaginaPub = page;   // (revision cuota-hilos-y-fallos) lo pedido para otra pagina que aun espera en la cola se deja
            if (!libBuilding) foreach (int b in RocolaOsSpotifyFaltan(p, page)) RocolaOsSpotifyPideBloque(p, b, null);
            RocolaOsParedConIcono = p.De != null;   // (la pared solo de tus discos no es de Spotify: sin su icono)
            RocolaOsParedCabecera = p.Cabecera;   // (la cabecera de XP le pone el icono de Spotify al lado: RocolaOs.ParedLetrero.cs)
            return p.Cabecera;
        }
        // los bloques de 10 que faltan en esa pagina de 18 casillas (ni pedidos ya, ni fallados en esta mirada)
        private List<int> RocolaOsSpotifyFaltan(RocolaOsSpotifyPared p, int pagina)
        {
            List<int> l = new List<int>();
            if (p.De == null) return l;   // (la pared solo de tus discos: nada que pedir)
            if (p.Total < 0)
            {
                if (!p.Pedidos.Contains(0) && !p.Fallados.Contains(0)) l.Add(0);
                return l;
            }
            int desde = pagina * PageSize, hasta = Math.Min(p.Lista.Count, desde + PageSize);
            for (int i = desde; i < hasta; i++)
            {
                int b;
                if (!p.HuecoDe.TryGetValue(p.Lista[i], out b)) continue;
                if (!l.Contains(b) && !p.Pedidos.Contains(b) && !p.Fallados.Contains(b)) l.Add(b);
            }
            return l;
        }
        private void RocolaOsSpotifyPideBloque(RocolaOsSpotifyPared p, int b, Action despues)
        {
            if (p.De == null) return;   // (la pared solo de tus discos: nunca pide)
            p.Pedidos.Add(b);
            int desde = b * RocolaOsSpotifyCatalogoApi.PorPaginaPared;
            Action<object, string> listo = delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:pared", delegate { RocolaOsSpotifyLlegoBloque(p, b, r as RocolaOsSpotifyCatalogoPagina, error); if (despues != null) despues(); });
            };
            RocolaOsSpotifyCatalogoPagina ya = RocolaOsSpotifyCatalogoApi.PaginaYa(p.Tipo, p.De, desde);
            if (ya != null) { listo(ya, null); return; }   // (de memoria: 0 peticiones; igual por un encargo, como lo que llega)
            // (revision cuota-hilos-y-fallos) las paginas donde caen sus casillas (la 1a, mientras no se sabe cuantas hay): si cuando le toque
            // en la cola la pagina que se mira es otra, se deja sin preguntar (lo lee el hilo del catalogo: solo PaginaPub, volatil)
            int pa = 0, pb = 0, i0, i1;
            if (p.Inicio.TryGetValue(b, out i0) && p.Fin.TryGetValue(b, out i1)) { pa = i0 / PageSize; pb = i1 / PageSize; }
            RocolaOsSpotifyCatalogoApi.PidePagina(p.Tipo, p.De, desde, listo, delegate { int m = p.PaginaPub; return m >= pa && m <= pb; });
        }
        private void RocolaOsSpotifyLlegoBloque(RocolaOsSpotifyPared p, int b, RocolaOsSpotifyCatalogoPagina pg, string error)
        {
            p.Pedidos.Remove(b);
            bool mirando = ReferenceEquals(rocolaOsSpPared, p) && currentLetter == RocolaOsSpotifyParedMarca;
            if (pg == null && error == RocolaOsSpotifyCatalogoApi.Dejada)
            {
                // (revision cuota-hilos-y-fallos) se dejo porque ya nadie miraba su pagina: ni fallo ni aviso; si entre tanto se volvio a ella,
                // se pide otra vez (con la pagina que se mira ya publicada: lo pedido de nuevo no se vuelve a dejar)
                if (mirando && !searchOpen && !libBuilding)
                {
                    p.PaginaPub = page;
                    foreach (int x in RocolaOsSpotifyFaltan(p, page)) RocolaOsSpotifyPideBloque(p, x, null);
                }
                return;
            }
            if (pg == null)
            {
                p.Fallados.Add(b);
                RocolaOsSpotifyCatalogoApi.ApuntaCallado("no llego una pagina de la pared: " + (error ?? "sin respuesta"));
                if (mirando && !searchOpen && (p.Tuyos == null || p.Tuyos.Count == 0)) RocolaOsSpotifyCatalogoAviso("Espera un momento y vuelve a intentarlo");   // (la unida: se quedan tus discos)
                return;
            }
            p.Fallados.Remove(b);
            if (p.Total < 0) p.Total = Math.Min(pg.Total, RocolaOsSpotifyCatalogoApi.TopeTodos);
            // (revision cuota-hilos-y-fallos, 2a pasada) DONDE SE ACABA DE VERDAD: el «total» de search es una estimacion (puede decir 1000 y
            // acabarse en 40) y el de un artista puede bajar entre una pagina y otra. Si no se corrige, la pagina que se mira se llena de casillas
            // que nunca llegan: cada bloque vacio corre la lista, la pagina pide los siguientes, y asi solo, sin que nadie toque nada, hasta el
            // final (unas 95 peticiones en «Todos los discos»). Un bloque con menos de 10 en «items» (contando los null) es el final, y un
            // «total» mas chico que el de antes manda; nunca por debajo de lo que ya llego.
            int desdeB = b * RocolaOsSpotifyCatalogoApi.PorPaginaPared;
            if (pg.Crudos > 0) p.FinCrudo[b] = desdeB + pg.Crudos;   // (uno vacio no prueba nada: puede llegar antes que el de delante)
            int fin = pg.Crudos < RocolaOsSpotifyCatalogoApi.PorPaginaPared ? desdeB + pg.Crudos : int.MaxValue;
            fin = Math.Min(fin, Math.Max(pg.Total, desdeB + pg.Crudos));
            if (fin < p.Total)
            {
                foreach (KeyValuePair<int, int> kv in p.FinCrudo) if (kv.Value > fin) fin = kv.Value;
                if (fin < p.Total) p.Total = fin;
            }
            List<Album> l = new List<Album>();
            foreach (RocolaOsSpotifyCatalogoDisco d in pg.Discos)
            {
                if (p.Excluir != null && (p.Excluir.Contains(MotorBusqueda.Normaliza(d.Nombre ?? "")) || RocolaOsSpotifyEsEdicionDeTuyo(p, d))) continue;   // (la pared unida: los que ya tienes no, ya van arriba; ni sus ediciones)
                Album a = RocolaOsSpotifyCatalogoAlbum(d);
                if (!l.Contains(a)) l.Add(a);
            }
            p.Bloques[b] = l;
            // (revision cuota-hilos-y-fallos) un bloque que llega con menos de 10 (los tuyos quitados en la pared unida, o Spotify que da
            // menos) corre las casillas de detras: la elegida, si ya era un disco y sigue en esta pagina, se queda en SU disco
            Album elegido = mirando && selAlbumIndex >= 0 && selAlbumIndex < p.Lista.Count ? p.Lista[selAlbumIndex] : null;
            Album hueco = rocolaOsSpEntrandoHueco;
            int enHueco = hueco != null ? p.Lista.IndexOf(hueco) : -1;
            RocolaOsSpotifyRecompone(p);
            if (elegido != null && !p.HuecoDe.ContainsKey(elegido))
            {
                int k = p.Lista.IndexOf(elegido);
                if (k >= 0 && k != selAlbumIndex && k / PageSize == page) selAlbumIndex = k;
            }
            if (!mirando || libBuilding || searchOpen) return;
            RefreshAlbums();   // (tambien con el disco abierto: al volver, la pagina ya esta al dia; XP no la rehace en BackToAlbums)
            // se habia entrado a una casilla que aun no llegaba: ahora es su disco
            // (23-11-2026, revision) «se acabo antes de esa casilla» solo si su casilla ya no esta en la lista (antes se comparaba su lugar VIEJO con el
            // largo NUEVO: un bloque de delante con menos de 10 sacaba del disco a quien esperaba una casilla que si iba a llegar)
            if (hueco != null && panelSongs.Visible && ReferenceEquals(currentAlbum, hueco) && enHueco >= 0 && p.Lista.IndexOf(hueco) < 0 && selAlbumIndex >= p.Lista.Count)
            {
                // (revision cuota-hilos-y-fallos, 2a pasada) la lista se acabo antes de esa casilla (el «total» de Spotify era de mas): no
                // hay disco que abrir; de vuelta a la pared (lo de XP al salir de un disco), en la ultima casilla, y no un disco vacio para siempre
                rocolaOsSpEntrandoHueco = null;
                selAlbumIndex = Math.Max(0, p.Lista.Count - 1); page = selAlbumIndex / PageSize;
                BackToAlbums();
                AbortDiscTrans();
                RefreshAlbums();
                return;
            }
            if (hueco != null && panelSongs.Visible && ReferenceEquals(currentAlbum, hueco) && selAlbumIndex >= 0 && selAlbumIndex < p.Lista.Count && !p.HuecoDe.ContainsKey(p.Lista[selAlbumIndex]))
            {
                rocolaOsSpEntrandoHueco = null;
                EnterAlbum(p.Lista[selAlbumIndex]);
                AbortDiscTrans();
            }
        }
        // (27-11-2026) ¿el disco de Spotify es una edicion de uno TUYO (la pared unida)?
        private static bool RocolaOsSpotifyEsEdicionDeTuyo(RocolaOsSpotifyPared p, RocolaOsSpotifyCatalogoDisco d)
        {
            if (p.ExcluirClaves == null)
            {
                HashSet<string> k = new HashSet<string>(StringComparer.Ordinal);
                foreach (string x in p.Excluir) k.Add(RocolaOsSpotifyEdiciones.Clave(x));
                p.ExcluirClaves = k;
            }
            string c = RocolaOsSpotifyEdiciones.Clave(d.Nombre ?? "");
            return c.Length > 0 && p.ExcluirClaves.Contains(c);
        }
        // la lista de la pared: tus discos (la unida), «Canciones de», y cada bloque de 10 como llego, o sus casillas por llenar
        private void RocolaOsSpotifyRecompone(RocolaOsSpotifyPared p)
        {
            List<Album> l = new List<Album>();
            if (p.Tuyos != null) l.AddRange(p.Tuyos);
            if (p.Canciones != null) l.Add(p.Canciones);
            HashSet<Album> vistos = new HashSet<Album>(l);
            // (27-11-2026) de las ediciones de un mismo disco que ya llegaron (Remastered, Deluxe...) queda UNA: el original, si no la de mas canciones
            Dictionary<string, Album> mejorEdicion = new Dictionary<string, Album>(StringComparer.Ordinal);
            List<int> orden = new List<int>(p.Bloques.Keys); orden.Sort();   // (en el orden de Spotify: a igualdad, la primera)
            foreach (int bq in orden)
                foreach (Album x in p.Bloques[bq])
                {
                    RocolaOsSpotifyCatalogoDisco dx;
                    if (!rocolaOsSpDeDisco.TryGetValue(x, out dx)) continue;
                    string clave = RocolaOsSpotifyEdiciones.Clave(dx.Nombre);
                    if (clave.Length == 0) continue;
                    Album y; RocolaOsSpotifyCatalogoDisco dy;
                    if (!mejorEdicion.TryGetValue(clave, out y) || !rocolaOsSpDeDisco.TryGetValue(y, out dy) || RocolaOsSpotifyEdiciones.Mejor(dx, dy)) mejorEdicion[clave] = x;
                }
            Func<Album, bool> esLaElegida = delegate (Album x)
            {
                RocolaOsSpotifyCatalogoDisco dx;
                if (!rocolaOsSpDeDisco.TryGetValue(x, out dx)) return true;
                string clave = RocolaOsSpotifyEdiciones.Clave(dx.Nombre);
                Album y;
                if (clave.Length == 0 || !mejorEdicion.TryGetValue(clave, out y) || ReferenceEquals(x, y)) return true;
                if (p.EdicionesDichas.Add(x)) RocolaOs.Apunta("spotify_catalogo", "edicion repetida: «" + dx.Nombre + "» no sale (queda «" + y.Name + "»)" + " - " + dx.NumCanciones.ToString(CultureInfo.InvariantCulture) + " canciones contra " + (rocolaOsSpDeDisco.TryGetValue(y, out RocolaOsSpotifyCatalogoDisco dy2) ? dy2.NumCanciones : -1).ToString(CultureInfo.InvariantCulture));
                return false;
            };
            int por = RocolaOsSpotifyCatalogoApi.PorPaginaPared;
            int nb = p.Total < 0 ? 0 : (p.Total + por - 1) / por;
            p.Inicio.Clear(); p.Fin.Clear();
            for (int b = 0; b < nb; b++)
            {
                List<Album> ya;
                // (23-11-2026, revision) el mismo disco en dos bloques (Spotify corrio su lista entre dos paginas) va una sola vez
                if (p.Bloques.TryGetValue(b, out ya)) { foreach (Album x in ya) if (esLaElegida(x) && vistos.Add(x)) l.Add(x); continue; }
                p.Inicio[b] = l.Count;
                List<Album> h;
                if (!p.Huecos.TryGetValue(b, out h))
                {
                    h = new List<Album>();
                    int n = Math.Min(por, p.Total - b * por);
                    for (int i = 0; i < n; i++)
                    {
                        Album x = new Album { Folder = RocolaOsSpotifyCarpeta + "hueco:" + (++rocolaOsSpHuecos).ToString(CultureInfo.InvariantCulture), Name = "", Artist = "", Letter = RocolaOsSpotifyParedMarca };
                        h.Add(x); p.HuecoDe[x] = b;
                    }
                    p.Huecos[b] = h;
                }
                l.AddRange(h);
                p.Fin[b] = Math.Max(p.Inicio[b], l.Count - 1);
            }
            p.Lista = l;
        }

        // g49 (por RocolaOsSpotifyCatalogoEntra): «Canciones de ARTISTA» (se piden al entrar) o una casilla que aun no llega (se espera a
        // su pagina). true = era de esas.
        private bool RocolaOsSpotifyParedesEntra(Album a)
        {
            rocolaOsSpEntrandoHueco = null;
            RocolaOsSpotifyCancionesDe c;
            if (rocolaOsSpCancionesDe != null && rocolaOsSpCancionesDe.TryGetValue(a, out c)) { RocolaOsSpotifyCancionesSigue(a, c); return true; }
            RocolaOsSpotifyPared p = rocolaOsSpPared;
            if (p != null && p.HuecoDe.ContainsKey(a))
            {
                rocolaOsSpEntrandoHueco = a;
                int b = p.HuecoDe[a];
                if (!p.Pedidos.Contains(b)) { p.Fallados.Remove(b); RocolaOsSpotifyPideBloque(p, b, null); }
                return true;
            }
            return false;
        }

        // ================================================================= «Canciones de ARTISTA» ======================================

        private Album RocolaOsSpotifyCancionesAlbum(string artistaId, string nombre, string portadaUrl)
        {
            string clave = "canciones:" + artistaId;
            Album a;
            if (!rocolaOsSpDiscos.TryGetValue(clave, out a))
            {
                a = new Album { Folder = RocolaOsSpotifyCarpeta + clave, Name = "Canciones de " + nombre, Artist = nombre ?? "", Letter = RocolaOsSpotifyMarca };
                rocolaOsSpDiscos[clave] = a;
                rocolaOsSpDeDisco[a] = new RocolaOsSpotifyCatalogoDisco { Id = artistaId, Uri = "spotify:artist:" + artistaId, Nombre = a.Name, Artista = nombre ?? "", PortadaUrl = portadaUrl, Tipo = "canciones" };
                RocolaOsSpotifyCatalogoPortada(a, "artista-" + artistaId, portadaUrl);
                if (rocolaOsSpCancionesDe == null) rocolaOsSpCancionesDe = new Dictionary<Album, RocolaOsSpotifyCancionesDe>();
                rocolaOsSpCancionesDe[a] = new RocolaOsSpotifyCancionesDe { ArtistaId = artistaId, Nombre = nombre ?? "" };
            }
            return a;
        }
        // (g52) se quedo elegida en la pared: su 1a pagina, para que la ficha y el letrero de XP digan cuantas trae (lo demas, al entrar)
        private void RocolaOsSpotifyCancionesPrevista(Album a)
        {
            RocolaOsSpotifyCancionesDe c;
            if (rocolaOsSpCancionesDe != null && rocolaOsSpCancionesDe.TryGetValue(a, out c) && c.Siguiente == 0) RocolaOsSpotifyCancionesSigue(a, c);
        }
        // la siguiente pagina de 10, si alguien esta dentro y faltan (hasta 50)
        private void RocolaOsSpotifyCancionesSigue(Album a, RocolaOsSpotifyCancionesDe c)
        {
            if (c.Pidiendo || c.Acabado) return;
            int tope = c.Total >= 0 ? Math.Min(c.Total, RocolaOsSpotifyCatalogoApi.TopeCanciones) : RocolaOsSpotifyCatalogoApi.TopeCanciones;
            if (c.Siguiente >= tope) { c.Acabado = true; return; }
            c.Pidiendo = true;
            int desde = c.Siguiente;
            Action<object, string> listo = delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:canciones", delegate { RocolaOsSpotifyLlegaronCanciones(a, c, desde, r as RocolaOsSpotifyCatalogoPagina, error); });
            };
            RocolaOsSpotifyCatalogoPagina ya = RocolaOsSpotifyCatalogoApi.PaginaYa(RocolaOsSpotifyCatalogoApi.ParedCanciones, c.Nombre, desde);
            if (ya != null) { listo(ya, null); return; }
            RocolaOsSpotifyCatalogoApi.PidePagina(RocolaOsSpotifyCatalogoApi.ParedCanciones, c.Nombre, desde, listo);
        }
        private void RocolaOsSpotifyLlegaronCanciones(Album a, RocolaOsSpotifyCancionesDe c, int desde, RocolaOsSpotifyCatalogoPagina pg, string error)
        {
            c.Pidiendo = false;
            bool dentro = panelSongs.Visible && ReferenceEquals(currentAlbum, a);
            if (pg == null)
            {
                RocolaOsSpotifyCatalogoApi.ApuntaCallado("no llegaron las canciones del artista: " + (error ?? "sin respuesta"));
                if (dentro && a.Tracks.Count == 0) RocolaOsSpotifyCatalogoAviso("Espera un momento y vuelve a intentarlo");
                return;
            }
            if (desde != c.Siguiente) return;   // (ya estaba)
            c.Total = pg.Total;
            int n = a.Tracks.Count, filtradas = 0;
            foreach (RocolaOsSpotifyCatalogoCancion t in pg.Canciones)
            {
                if (n >= RocolaOsSpotifyCatalogoApi.TopeCanciones) break;
                // (27-11-2026) solo las canciones donde el artista esta entre los de la pista: sin versiones de otros ni lo que la busqueda de texto trae de mas
                if (!RocolaOsSpotifyEdiciones.EsDeArtista(t, c.ArtistaId, c.Nombre)) { filtradas++; continue; }
                n++;
                // la misma cancion puede estar en su disco con otro numero: aqui va con la ruta «spotify:track:ID#artista» (su Uri de I6 no
                // cambia: PONER la pide igual) para que la lista diga su lugar en «Canciones de» («NN Titulo»)
                string ruta = t.Uri + "#" + c.ArtistaId;
                rocolaOsSpCanciones[ruta] = t;
                lock (RocolaOsSpotifyCatalogoNombres.PorRuta) RocolaOsSpotifyCatalogoNombres.PorRuta[ruta] = n.ToString("00", CultureInfo.InvariantCulture) + " " + t.Titulo;
                a.Tracks.Add(new Track { Path = ruta, Artist = t.Artista ?? "", Title = t.Titulo ?? "", Album = t.Disco ?? "", Genre = "" });
            }
            if (filtradas > 0) RocolaOs.Apunta("spotify_catalogo", "«" + a.Name + "»: " + filtradas.ToString(CultureInfo.InvariantCulture) + " canciones de la pagina " + (desde / RocolaOsSpotifyCatalogoApi.PorPaginaPared + 1).ToString(CultureInfo.InvariantCulture) + " no son de " + c.Nombre + " (se quitan)");
            c.Siguiente = desde + RocolaOsSpotifyCatalogoApi.PorPaginaPared;
            if (pg.Crudos < RocolaOsSpotifyCatalogoApi.PorPaginaPared || c.Siguiente >= Math.Min(c.Total, RocolaOsSpotifyCatalogoApi.TopeCanciones)) c.Acabado = true;
            generoVer++; duracionesVer++;   // (la ficha y el letrero de XP se rehacen con lo que ya vigilan)
            if (!dentro) return;
            // ya estaba dentro: las nuevas entran a la lista de XP como en EnterAlbum, sin mover la eleccion
            bool primera = songItems.Count == 0;
            songItems.Clear();
            for (int i = 0; i < a.Tracks.Count; i++) songItems.Add(a.Tracks[i]);
            if (primera) { songSel = 0; songList.OlvidaPagina(); if (songPager != null) songPager.PonSinAnimar(0); }
            FillSongPage();
            songList.Invalidate();
            RocolaOsSpotifyCancionesSigue(a, c);   // (la siguiente, mientras siga dentro)
        }

        // ================================================================= REGRESAR ===================================================

        private RocolaOsSpotifyVuelta RocolaOsSpotifyGuardaBuscador()
        {
            RocolaOsSpotifyVuelta v = new RocolaOsSpotifyVuelta
            {
                Escrito = searchOverlay.Escrito ?? "", EnResultados = searchOverlay.FocusResults, Sel = searchOverlay.SelResult,
                Fila = searchOverlay.SelRow, Col = searchOverlay.SelCol, Letra = currentLetter, Pagina = page, Elegido = selAlbumIndex,
                Pared = currentLetter == RocolaOsSpotifyParedMarca ? rocolaOsSpPared : null,
            };
            // la pila, como mucho 4 (disco, pared, buscador y la pared de debajo): si debajo habia otra pared del buscador, lo de debajo de
            // ella se suelta (su REGRESAR vuelve al buscador sobre la primera letra)
            if (v.Pared != null && v.Pared.Vuelta != null) v.Pared.Vuelta.Pared = null;
            return v;
        }
        // VOLVER AL BUSCADOR: la pared de debajo como estaba antes de abrirlo, y el buscador con lo escrito (lo contestado sale de memoria: 0
        // peticiones) y la misma eleccion
        private void RocolaOsSpotifyVuelveAlBuscador(RocolaOsSpotifyVuelta v)
        {
            if (rocolaOsSpPared != null && !ReferenceEquals(rocolaOsSpPared, v.Pared)) rocolaOsSpPared.PaginaPub = -1;   // (revision: lo suyo en la cola se deja)
            rocolaOsSpPared = v.Pared;
            RocolaOs.Apunta("spotify_catalogo", "REGRESAR: de vuelta al buscador con «" + v.Escrito + "»");
            char letra = v.Letra;
            bool otraLetra = letra == RocolaOsSpotifyParedMarca && rocolaOsSpPared == null;
            if (otraLetra) letra = RocolaOsSpotifyPrimeraLetra();
            // (23-11-2026, revision) en otra letra que la guardada, desde su principio (no con la pagina y casilla de la pared que ya no esta)
            currentLetter = letra; page = otraLetra ? 0 : v.Pagina; selAlbumIndex = otraLetra ? 0 : v.Elegido;
            if (panelSongs.Visible) { BackToAlbums(); AbortDiscTrans(); }
            if (!libBuilding) RefreshAlbums();
            OpenSearch();
            searchOverlay.RocolaOsRestaura(v.Escrito, v.EnResultados, v.Sel, v.Fila, v.Col);
            MarkSearchActivity();
            // (revision, funcionamiento-y-reglas) se llego con una pulsacion de REGRESAR que puede seguir apretada: (1) su repeticion (a los
            // 500 ms, unas 30 por segundo) ya no es «borrar letra» en el buscador (g57), y (2) con el rojo apretado, ROJO+ARRIBA no cierra el
            // buscador que esta pulsacion acaba de abrir: el disparo del buscador de XP (UnDisparo) queda tomado hasta soltar, como si la
            // combinacion ya hubiera hecho lo suyo. Asi, mantener el rojo en un disco de una pared lleva a la pared y ahi se queda, como XP
            // en un disco; y ROJO+ARRIBA en una pared deja el buscador abierto (en XP, desde un disco, abre el buscador).
            rocolaOsSpTragaTick = Environment.TickCount | 1;
            if (BackHeld()) disparoBuscador.Pide();
        }
        // g57 (Form1.cs, ProcessCmdKey, en el buscador: «if (IsBack(kc)) { searchOverlay.Backspace(); return true; }»): true = es la
        // repeticion de la pulsacion de REGRESAR que volvio al buscador, y no borra. Una pulsacion nueva (o una repeticion que llega mas de
        // 1 s despues de la anterior: el soltar se perdio) lo olvida. Sin el modo, nunca se puso: siempre false (Backspace, como XP).
        private bool RocolaOsSpotifyTragaRegresar(bool repetida)
        {
            if (rocolaOsSpTragaTick == 0) return false;
            int ahora = Environment.TickCount;
            if (!repetida || unchecked(ahora - rocolaOsSpTragaTick) > 1000) { rocolaOsSpTragaTick = 0; return false; }
            rocolaOsSpTragaTick = ahora | 1;
            return true;
        }
        private char RocolaOsSpotifyPrimeraLetra()
        {
            foreach (char c in LetrasOrden) if (c != TopMark && c != VideoMark && letterAvailable.Contains(c)) return c;
            foreach (char c in LetrasOrden) if (letterAvailable.Contains(c)) return c;
            return '#';
        }

        // g53 (Form1.cs, ProcessCmdKey, en la pared: «if (IsBack(kc)) { return true; }»): REGRESAR en una pared del buscador vuelve al
        // buscador. En las de XP (y sin el modo), nada: lo de XP. Su REPETICION no (revision): manteniendo el rojo en un disco de la pared,
        // la pulsacion lleva a la pared (BackToAlbums de XP) y sus repeticiones llegan aqui; no deben seguir hasta el buscador.
        private void RocolaOsSpotifyParedRegresa(bool repetida)
        {
            if (repetida) return;
            rocolaOsSpTragaTick = 0;
            if (currentLetter != RocolaOsSpotifyParedMarca || rocolaOsSpPared == null || rocolaOsSpPared.Vuelta == null) return;
            RocolaOsSpotifyVuelveAlBuscador(rocolaOsSpPared.Vuelta);
        }
        // g54 (Form1.cs, ProcessCmdKey, dentro de un disco: «if (IsBack(kc)) { BackToAlbums(); return true; }»): un disco de Spotify abierto
        // desde el buscador que no estaba en la letra ♫ vuelve al buscador. true = ya lo atendio; si no (y sin el modo), BackToAlbums de XP.
        private bool RocolaOsSpotifyDiscoRegresa()
        {
            RocolaOsSpotifyVuelta v = rocolaOsSpVueltaDisco;
            if (v == null || !ReferenceEquals(currentAlbum, rocolaOsSpVueltaAlbum)) return false;
            rocolaOsSpVueltaDisco = null; rocolaOsSpVueltaAlbum = null;
            RocolaOsSpotifyVuelveAlBuscador(v);
            return true;
        }

        // ================================================================= SP9: los atajos ===========================================

        // g55 (BuscadorPortadasGpu.cs, ArmaAtajos, antes de pasarle los textos al buscador): con la cuenta, «Tus mas escuchadas» y «Escuchado
        // hace poco» detras de los de XP (si no caben, se quedan fuera, como los de XP). Una peticion cada uno por sesion, la primera vez que se
        // abre el buscador; al llegar, los atajos se rehacen. Sin el modo o sin la cuenta, nada.
        private void RocolaOsSpotifyCatalogoAtajos()
        {
            if (!RocolaOs.Activo || !RocolaOsSpotifyCatalogoVa || !RocolaOsSpotifyCatalogoApi.HayCuenta) return;
            // (revision cuota-hilos-y-fallos) si no llegaron por la red (sin red al abrir el buscador, Spotify caido o frenando), se piden otra
            // vez al abrir el buscador, como mucho una vez por minuto (lo que si llego sale de memoria: 0 peticiones). Un 403 (faltan los
            // permisos) o sin la cuenta, no: una vez por sesion
            if (!rocolaOsSpAtajosPedidos || (rocolaOsSpAtajosOtraVez != 0 && unchecked(Environment.TickCount - rocolaOsSpAtajosOtraVez) >= 0))
            {
                rocolaOsSpAtajosPedidos = true; rocolaOsSpAtajosOtraVez = 0;
                Action<object, string> listo = delegate (object r, string error)
                {
                    if (r == null && error != null) RocolaOsSpotifyCatalogoApi.ApuntaCallado("un atajo no llego: " + error);
                    bool otraVez = r == null && RocolaOsSpotifyCatalogoApi.Pasajero(error);
                    RocolaOs.Encarga("spotify_catalogo:atajos", delegate
                    {
                        if (otraVez && rocolaOsSpAtajosOtraVez == 0) rocolaOsSpAtajosOtraVez = (Environment.TickCount + 60000) | 1;
                        if (searchOpen) ArmaAtajos();   // (el que si llego; el otro no se vuelve a pedir aqui: falta su minuto)
                    });
                };
                RocolaOsSpotifyCatalogoApi.PideAtajo(RocolaOsSpotifyCatalogoApi.AtajoTop, listo);
                RocolaOsSpotifyCatalogoApi.PideAtajo(RocolaOsSpotifyCatalogoApi.AtajoRecientes, listo);
            }
            List<RocolaOsSpotifyCatalogoCancion> top = RocolaOsSpotifyCatalogoApi.AtajoYa(RocolaOsSpotifyCatalogoApi.AtajoTop);
            if (top != null && top.Count > 0) AgregaAtajo(RocolaOsAtajoTop, "Tus más escuchadas", null);
            List<RocolaOsSpotifyCatalogoCancion> rec = RocolaOsSpotifyCatalogoApi.AtajoYa(RocolaOsSpotifyCatalogoApi.AtajoRecientes);
            if (rec != null && rec.Count > 0) AgregaAtajo(RocolaOsAtajoRecientes, "Escuchado hace poco", null);
        }
        // g56 (BuscadorPortadasGpu.cs, UsaAtajo): pulsaron uno de esos: sus canciones como resultados (MuestraAtajo, como «Lo mas pedido hoy»);
        // elegir una la pide por I6. true = era de esos.
        private bool RocolaOsSpotifyCatalogoUsaAtajo(int tipo, string texto)
        {
            if (tipo != RocolaOsAtajoTop && tipo != RocolaOsAtajoRecientes) return false;
            List<RocolaOsSpotifyCatalogoCancion> l = RocolaOsSpotifyCatalogoApi.AtajoYa(tipo == RocolaOsAtajoTop ? RocolaOsSpotifyCatalogoApi.AtajoTop : RocolaOsSpotifyCatalogoApi.AtajoRecientes);
            List<SearchItem> res = new List<SearchItem>();
            if (l != null)
                foreach (RocolaOsSpotifyCatalogoCancion c in l)
                {
                    if (res.Count >= AtajoResultadosMax) break;
                    Album a = RocolaOsSpotifyCatalogoDiscoDe(c);
                    Track t = RocolaOsSpotifyCatalogoPista(c);
                    res.Add(new SearchItem { Kind = 2, Display = (c.Artista.Length > 0 ? c.Artista + " - " : "") + c.Titulo, Key = "", AlbumRef = a, TrackRef = t });
                }
            searchOverlay.MuestraAtajo(texto, res);
            return true;
        }

        // EL BUSCADOR AL VOLVER: lo escrito (se posa al momento: lo contestado sale de memoria), la tecla o la tarjeta elegida, y la fila de
        // tarjetas donde estaba (lo de XP al moverse: AjustaScroll)
        private partial class SearchOverlay
        {
            internal void RocolaOsRestaura(string escrito, bool enResultados, int sel, int fila, int col)
            {
                Escrito = escrito ?? "";
                PosaConsulta();
                if (rows.Count > 0)
                {
                    SelRow = Math.Max(0, Math.Min(rows.Count - 1, fila));
                    SelCol = Math.Max(0, Math.Min(rows[SelRow].Count - 1, col));
                }
                int n = Results == null ? 0 : Results.Count;
#if ROCOLA_GPU
                if (Nuevo) n = CartasTotales();
#endif
                if (enResultados && n > 0)
                {
                    FocusResults = true;
                    SelResult = Math.Max(0, Math.Min(n - 1, sel));
#if ROCOLA_GPU
                    if (Nuevo) { AseguraReparto(); AjustaScroll(); }
#endif
                }
                Invalidate();
            }
        }
    }
}

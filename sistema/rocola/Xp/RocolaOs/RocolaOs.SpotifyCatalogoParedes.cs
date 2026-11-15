// LAS PAREDES DEL BUSCADOR (fase 11, grupo paredes-buscador; FASE11_paredes-buscador.md). Una parte de Form1, encima del catalogo
// (RocolaOs.SpotifyCatalogo*.cs). La decision del dueno del 15-11-2026 («crear paredes de discos que solo son accesibles desde el buscador,
// para que todo el catalogo de Spotify sea accesible de la manera mas comoda») y sus ideas aprobadas, y lo del buscador del 7-11-2026:
//   - LA PARED DE UN ARTISTA que solo esta en Spotify: la rejilla de discos de XP (18 por pagina, sus teclas y sus portadas), fuera de la
//     barra de letras (una «letra» que la barra no tiene: RocolaOsSpotifyParedMarca), con su nombre en la cabecera (como «VIDEOS»). La
//     casilla 1 es «Canciones de ARTISTA» (un disco de XP con sus canciones en el orden de la busqueda artist:"..."; se piden al entrar, de 10
//     en 10, hasta 50); luego sus discos y sencillos, de 10 en 10, pidiendo solo la pagina que se mira.
//   - «ARTISTA · mas en Spotify»: detras de la tarjeta de un artista TUYO que Spotify tambien tiene; su pared sin los discos que tienes.
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
            internal string Tipo;                    // ParedArtista (un artista; tambien «mas en Spotify») o ParedTodos
            internal string De;                      // el id del artista, o lo escrito (la clave de la busqueda)
            internal string Cabecera;
            internal Album Canciones;                // «Canciones de ARTISTA» (null en la de todos los discos)
            internal HashSet<string> Excluir;        // («mas en Spotify») los nombres de tus discos, sin acentos ni mayusculas
            internal int Total = -1;                 // lo que dice Spotify (-1: aun no se sabe)
            internal readonly Dictionary<int, List<Album>> Bloques = new Dictionary<int, List<Album>>();   // de 10 en 10
            internal readonly Dictionary<int, List<Album>> Huecos = new Dictionary<int, List<Album>>();
            internal readonly Dictionary<Album, int> HuecoDe = new Dictionary<Album, int>();
            internal readonly HashSet<int> Pedidos = new HashSet<int>(), Fallados = new HashSet<int>();
            internal List<Album> Lista = new List<Album>();
            internal int PaginaMirada = -1;
            internal RocolaOsSpotifyVuelta Vuelta;
        }
        // «Canciones de ARTISTA»: por donde va (se piden de 10 en 10 mientras alguien esta dentro)
        private sealed class RocolaOsSpotifyCancionesDe
        {
            internal string ArtistaId, Nombre;
            internal int Siguiente, Total = -1;
            internal bool Pidiendo, Acabado;
        }
        // lo que abre cada tarjeta nueva del buscador («mas en Spotify», «Todos los discos de...»)
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
        private HashSet<string> rocolaOsSpTuyos;                      // tus artistas, sin acentos ni mayusculas
        private int rocolaOsSpTuyosDe = -1;
        private bool rocolaOsSpAtajosPedidos;
        private int rocolaOsSpHuecos;

        // ================================================================= el buscador =================================================

        // LOS ARTISTAS DE SPOTIFY DE UNA BUSQUEDA (lo llama la mezcla del catalogo): los que NO estan en la rocola (ni por el artista de un
        // disco tuyo, sin acentos ni mayusculas), hasta 3: el primero si su nombre se parece a lo escrito (lo contiene, esta contenido o a dos
        // letras; la idea del programa anterior, Motor/Spotify.cs), los demas si lo contienen. Los que SI estan: su «mas en Spotify».
        private void RocolaOsSpotifyParedesArtistas(RocolaOsSpotifyCatalogoBusqueda b, out List<SearchItem> artistas, out Dictionary<string, SearchItem> mas)
        {
            artistas = new List<SearchItem>();
            mas = new Dictionary<string, SearchItem>(StringComparer.Ordinal);
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
                    if (!mas.ContainsKey(nn) && RocolaOsSpotifyHayMas(ar, nn)) mas[nn] = RocolaOsSpotifyMasTarjeta(ar, nn);
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
            if (rocolaOsSpTuyos != null && (libBuilding || rocolaOsSpTuyosDe == albums.Count)) return rocolaOsSpTuyos;
            HashSet<string> h = new HashSet<string>(StringComparer.Ordinal);
            if (!libBuilding)
                foreach (Album a in albums)
                    if (a != null && !a.IsTop && !string.IsNullOrEmpty(a.Artist) && !RocolaOsEsSpotify(a)) h.Add(MotorBusqueda.Normaliza(a.Artist));
            rocolaOsSpTuyos = h; rocolaOsSpTuyosDe = libBuilding ? -1 : albums.Count;
            return h;
        }
        // los nombres de tus discos de ese artista, sin acentos ni mayusculas. XP saca artista y disco del nombre de la carpeta («Artista -
        // Disco», SplitArtist); una carpeta sin « - » es solo «artista» y su disco no tiene nombre: entonces cuenta su nombre (un disco con
        // el nombre del artista)
        private HashSet<string> RocolaOsSpotifyTusDiscos(string artistaNormalizado)
        {
            HashSet<string> h = new HashSet<string>(StringComparer.Ordinal);
            if (libBuilding) return h;
            foreach (Album a in albums)
                if (a != null && !a.IsTop && !RocolaOsEsSpotify(a) && MotorBusqueda.Normaliza(a.Artist ?? "") == artistaNormalizado)
                    h.Add(MotorBusqueda.Normaliza(string.IsNullOrEmpty(a.Name) ? (a.Artist ?? "") : a.Name));
            return h;
        }
        // ¿hay discos suyos que no tienes? Sin pedir nada: si ya se saben TODOS sus discos (una pagina contestada) y los tienes todos, no;
        // si no se saben, se ensena la tarjeta (saberlo costaria una peticion por busqueda) y su pared dira cuales faltan
        private bool RocolaOsSpotifyHayMas(RocolaOsSpotifyCatalogoArtista ar, string nn)
        {
            RocolaOsSpotifyCatalogoPagina p = RocolaOsSpotifyCatalogoApi.PaginaYa(RocolaOsSpotifyCatalogoApi.ParedArtista, ar.Id, 0);
            if (p == null || p.Total > p.Discos.Count) return true;
            HashSet<string> tuyos = RocolaOsSpotifyTusDiscos(nn);
            foreach (RocolaOsSpotifyCatalogoDisco d in p.Discos) if (!tuyos.Contains(MotorBusqueda.Normaliza(d.Nombre ?? ""))) return true;
            return false;
        }
        private SearchItem RocolaOsSpotifyMasTarjeta(RocolaOsSpotifyCatalogoArtista ar, string nn)
        {
            string clave = "mas:" + ar.Id;
            Album a;
            if (!rocolaOsSpDiscos.TryGetValue(clave, out a))
            {
                a = new Album { Folder = RocolaOsSpotifyCarpeta + clave, Name = "", Artist = ar.Nombre ?? "", Letter = RocolaOsSpotifyMarca };
                rocolaOsSpDiscos[clave] = a;
                RocolaOsSpotifyCatalogoPortada(a, "artista-" + ar.Id, ar.PortadaUrl);   // (la foto del artista: el mismo archivo que su tarjeta)
            }
            if (rocolaOsSpParedDe == null) rocolaOsSpParedDe = new Dictionary<Album, RocolaOsSpotifyParedQue>();
            rocolaOsSpParedDe[a] = new RocolaOsSpotifyParedQue { Tipo = "mas", De = ar.Id, Nombre = ar.Nombre ?? "", PortadaUrl = ar.PortadaUrl, Excluir = RocolaOsSpotifyTusDiscos(nn) };
            return new SearchItem { Kind = 0, Display = (ar.Nombre ?? "") + " · más en Spotify", Key = "", AlbumRef = a, Albums = new List<Album> { a } };
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
                if (q.Tipo == "todos") RocolaOsSpotifyAbrePared(RocolaOsSpotifyCatalogoApi.ParedTodos, q.De, null, null, null, "TODOS LOS DISCOS DE «" + q.Nombre.ToUpperInvariant() + "»");
                else RocolaOsSpotifyAbrePared(RocolaOsSpotifyCatalogoApi.ParedArtista, q.De, q.Nombre, q.PortadaUrl, q.Excluir, q.Nombre.ToUpperInvariant() + " · MÁS EN SPOTIFY");
                return true;
            }
            RocolaOsSpotifyCatalogoArtista ar;
            if (it.Kind == 0 && rocolaOsSpDeArtista.TryGetValue(a, out ar))
            {
                RocolaOsSpotifyAbrePared(RocolaOsSpotifyCatalogoApi.ParedArtista, ar.Id, ar.Nombre ?? "", ar.PortadaUrl, null, (ar.Nombre ?? "").ToUpperInvariant());
                return true;
            }
            return false;
        }

        // ================================================================= abrir una pared ============================================

        // Se pide lo de su primera pagina (1 o 2 peticiones) y se abre al llegar, si nadie eligio otra cosa mientras tanto; si no llega, el
        // aviso de XP y el buscador se queda como estaba
        private void RocolaOsSpotifyAbrePared(string tipo, string de, string nombre, string portadaUrl, HashSet<string> excluir, string cabecera)
        {
            RocolaOsSpotifyPared p = new RocolaOsSpotifyPared { Tipo = tipo, De = de, Cabecera = cabecera, Excluir = excluir };
            if (tipo == RocolaOsSpotifyCatalogoApi.ParedArtista) p.Canciones = RocolaOsSpotifyCancionesAlbum(de, nombre, portadaUrl);
            p.Vuelta = RocolaOsSpotifyGuardaBuscador();
            int yo = ++rocolaOsSpAbriendo;
            RocolaOsSpotifyParedPrepara(p, yo);
        }
        private void RocolaOsSpotifyParedPrepara(RocolaOsSpotifyPared p, int yo)
        {
            if (yo != rocolaOsSpAbriendo || !searchOpen) return;
            List<int> falta = RocolaOsSpotifyFaltan(p, 0);
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
            // lo de XP con un artista del buscador (ChooseSearchResult, Kind 0): CloseSearch y NavigateToAlbum, aqui a la pared
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
            if (!libBuilding) foreach (int b in RocolaOsSpotifyFaltan(p, page)) RocolaOsSpotifyPideBloque(p, b, null);
            return p.Cabecera;
        }
        // los bloques de 10 que faltan en esa pagina de 18 casillas (ni pedidos ya, ni fallados en esta mirada)
        private List<int> RocolaOsSpotifyFaltan(RocolaOsSpotifyPared p, int pagina)
        {
            List<int> l = new List<int>();
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
            p.Pedidos.Add(b);
            int desde = b * RocolaOsSpotifyCatalogoApi.PorPaginaPared;
            Action<object, string> listo = delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:pared", delegate { RocolaOsSpotifyLlegoBloque(p, b, r as RocolaOsSpotifyCatalogoPagina, error); if (despues != null) despues(); });
            };
            RocolaOsSpotifyCatalogoPagina ya = RocolaOsSpotifyCatalogoApi.PaginaYa(p.Tipo, p.De, desde);
            if (ya != null) { listo(ya, null); return; }   // (de memoria: 0 peticiones; igual por un encargo, como lo que llega)
            RocolaOsSpotifyCatalogoApi.PidePagina(p.Tipo, p.De, desde, listo);
        }
        private void RocolaOsSpotifyLlegoBloque(RocolaOsSpotifyPared p, int b, RocolaOsSpotifyCatalogoPagina pg, string error)
        {
            p.Pedidos.Remove(b);
            bool mirando = ReferenceEquals(rocolaOsSpPared, p) && currentLetter == RocolaOsSpotifyParedMarca;
            if (pg == null)
            {
                p.Fallados.Add(b);
                RocolaOsSpotifyCatalogoApi.ApuntaCallado("no llego una pagina de la pared: " + (error ?? "sin respuesta"));
                if (mirando && !searchOpen) RocolaOsSpotifyCatalogoAviso("Espera un momento y vuelve a intentarlo");
                return;
            }
            p.Fallados.Remove(b);
            if (p.Total < 0) p.Total = Math.Min(pg.Total, RocolaOsSpotifyCatalogoApi.TopeTodos);
            List<Album> l = new List<Album>();
            foreach (RocolaOsSpotifyCatalogoDisco d in pg.Discos)
            {
                if (p.Excluir != null && p.Excluir.Contains(MotorBusqueda.Normaliza(d.Nombre ?? ""))) continue;   // («mas en Spotify»: los tuyos no)
                Album a = RocolaOsSpotifyCatalogoAlbum(d);
                if (!l.Contains(a)) l.Add(a);
            }
            p.Bloques[b] = l;
            RocolaOsSpotifyRecompone(p);
            if (!mirando || libBuilding || searchOpen) return;
            RefreshAlbums();   // (tambien con el disco abierto: al volver, la pagina ya esta al dia; XP no la rehace en BackToAlbums)
            // se habia entrado a una casilla que aun no llegaba: ahora es su disco
            Album hueco = rocolaOsSpEntrandoHueco;
            if (hueco != null && panelSongs.Visible && ReferenceEquals(currentAlbum, hueco) && selAlbumIndex >= 0 && selAlbumIndex < p.Lista.Count && !p.HuecoDe.ContainsKey(p.Lista[selAlbumIndex]))
            {
                rocolaOsSpEntrandoHueco = null;
                EnterAlbum(p.Lista[selAlbumIndex]);
                AbortDiscTrans();
            }
        }
        // la lista de la pared: «Canciones de», y cada bloque de 10 como llego, o sus casillas por llenar
        private void RocolaOsSpotifyRecompone(RocolaOsSpotifyPared p)
        {
            List<Album> l = new List<Album>();
            if (p.Canciones != null) l.Add(p.Canciones);
            int por = RocolaOsSpotifyCatalogoApi.PorPaginaPared;
            int nb = p.Total < 0 ? 0 : (p.Total + por - 1) / por;
            for (int b = 0; b < nb; b++)
            {
                List<Album> ya;
                if (p.Bloques.TryGetValue(b, out ya)) { l.AddRange(ya); continue; }
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
            int n = a.Tracks.Count;
            foreach (RocolaOsSpotifyCatalogoCancion t in pg.Canciones)
            {
                if (n >= RocolaOsSpotifyCatalogoApi.TopeCanciones) break;
                n++;
                // la misma cancion puede estar en su disco con otro numero: aqui va con la ruta «spotify:track:ID#artista» (su Uri de I6 no
                // cambia: PONER la pide igual) para que la lista diga su lugar en «Canciones de» («NN Titulo»)
                string ruta = t.Uri + "#" + c.ArtistaId;
                rocolaOsSpCanciones[ruta] = t;
                lock (RocolaOsSpotifyCatalogoNombres.PorRuta) RocolaOsSpotifyCatalogoNombres.PorRuta[ruta] = n.ToString("00", CultureInfo.InvariantCulture) + " " + t.Titulo;
                a.Tracks.Add(new Track { Path = ruta, Artist = t.Artista ?? "", Title = t.Titulo ?? "", Album = t.Disco ?? "", Genre = "" });
            }
            c.Siguiente = desde + RocolaOsSpotifyCatalogoApi.PorPaginaPared;
            if (pg.Canciones.Count < RocolaOsSpotifyCatalogoApi.PorPaginaPared || c.Siguiente >= Math.Min(c.Total, RocolaOsSpotifyCatalogoApi.TopeCanciones)) c.Acabado = true;
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
            rocolaOsSpPared = v.Pared;
            char letra = v.Letra;
            if (letra == RocolaOsSpotifyParedMarca && rocolaOsSpPared == null) letra = RocolaOsSpotifyPrimeraLetra();
            currentLetter = letra; page = v.Pagina; selAlbumIndex = v.Elegido;
            if (panelSongs.Visible) { BackToAlbums(); AbortDiscTrans(); }
            if (!libBuilding) RefreshAlbums();
            OpenSearch();
            searchOverlay.RocolaOsRestaura(v.Escrito, v.EnResultados, v.Sel, v.Fila, v.Col);
            MarkSearchActivity();
        }
        private char RocolaOsSpotifyPrimeraLetra()
        {
            foreach (char c in LetrasOrden) if (c != TopMark && c != VideoMark && letterAvailable.Contains(c)) return c;
            foreach (char c in LetrasOrden) if (letterAvailable.Contains(c)) return c;
            return '#';
        }

        // g53 (Form1.cs, ProcessCmdKey, en la pared: «if (IsBack(kc)) { return true; }»): REGRESAR en una pared del buscador vuelve al
        // buscador. En las de XP (y sin el modo), nada: lo de XP.
        private void RocolaOsSpotifyParedRegresa()
        {
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
            if (!rocolaOsSpAtajosPedidos)
            {
                rocolaOsSpAtajosPedidos = true;
                Action<object, string> listo = delegate (object r, string error)
                {
                    if (r == null && error != null) RocolaOs.Apunta("spotify_catalogo", "un atajo no llego: " + error);
                    RocolaOs.Encarga("spotify_catalogo:atajos", delegate { if (searchOpen) ArmaAtajos(); });
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

// «LAS MEJORES CANCIONES DE...» EN EL BUSCADOR (Rocola OS, modo Spotify con la llave de la API; el dueno, 22-11-2026: «Ahora en el buscador
// estara la de buscar las mejores canciones de cualquier artista, hazlo intuitivo con un boton que ponga en la barra de busqueda LAS MEJORES
// CANCIONES DE - El cliente escribe el nombre del artista»). FASE11_paredes-buscador.md, seccion «Las mejores canciones de...».
//   - EL ATAJO: «LAS MEJORES CANCIONES DE…» es el PRIMERO de la fila de atajos del buscador con portadas (en ambar, no el
//     azul apagado de los demas). Pulsarlo (PONER, o un clic) pone en la casilla una pastilla fija «LAS MEJORES CANCIONES DE» delante de
//     lo escrito y pasa la seleccion al teclado; el cliente solo escribe el artista. La pastilla NO se borra con la tecla BORRAR (con nada
//     escrito, no hace nada); se quita con REGRESAR (con nada escrito) o pulsando otra vez el mismo atajo (que entonces dice «× Quitar»).
//   - LA TARJETA: con el prefijo puesto y el artista escrito (2 letras o mas), los resultados llevan de PRIMERA una tarjeta de las grandes de
//     siempre, «LAS MEJORES CANCIONES DE ARTISTA» (etiqueta «LO MEJOR»), con el nombre del artista que dan los resultados (el tuyo si lo tienes,
//     si no el de Spotify, si no lo escrito) y su portada si la hay. Elegirla (PONER) pide a Spotify las canciones del artista en el orden de su
//     busqueda artist:"..." (la misma de «Canciones de ARTISTA» de las paredes del buscador: RocolaOs.SpotifyCatalogoParedes.cs, y de su memoria
//     de la sesion), hasta 30 en 3 peticiones, sin repetir la misma cancion en otra edicion, y abre un disco de XP (dentro del buscador:
//     REGRESAR vuelve al buscador con lo escrito y el prefijo puesto, y 30 s sin tocar cierra todo, como cualquier disco del buscador) con TUS
//     canciones de ese artista que esten entre ellas PRIMERO (la tuya suena desde su MP3, con su disco de verdad) y despues las de Spotify.
//     Se piden con creditos como todo. Si no llega nada de Spotify, el aviso de siempre y el buscador se queda como estaba.
//   - Cuota: 3 peticiones por artista, una vez por sesion (la memoria del cliente); si la tarjeta se queda elegida 1 a 2 s, la 1a pagina ya se pide.
//     Sin llave, sin red o con Spotify frenando (429), ni atajo ni tarjeta.
// Ganchos: g400 (el atajo, en ArmaAtajos), g401 (pulsarlo), g402 a g404 y g408 a g410 (la casilla, la etiqueta y el color: RocolaOs.MejoresCaja.cs),
// g405 y g406 (REGRESAR y BORRAR), g407 (el disco de tus canciones). Ademas, tres lineas en lo del catalogo: la tarjeta entra en la lista
// (RocolaOsSpotifyCatalogoVisto y RocolaOsSpotifyCatalogoPoneResultados), ELEGIR la atiende primero (RocolaOsSpotifyCatalogoElige) y el latido
// del catalogo llama a RocolaOsMejoresLatido (RocolaOs.SpotifyCatalogo.cs).
// SIN EL MODO (o sin la llave): nada de esto existe y el buscador es el de XP.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Rocola
{
    public partial class Form1
    {
        internal const int RocolaOsAtajoMejores = 102;   // (los de XP son 0 a 3; los del catalogo, 100 y 101)
        internal const string RocolaOsMejoresPrefijoTexto = "LAS MEJORES CANCIONES DE";
        private const int RocolaOsMejoresPaginas = 3;                // de 10: hasta 30 canciones de Spotify
        private const int RocolaOsMejoresToleranciaMs = 10000;       // tu cancion y la de Spotify, de la misma duracion con esta holgura (si se sabe de las dos)
        private const int RocolaOsMejoresTarjetasMax = 200;

        private sealed class RocolaOsMejoresDe
        {
            internal string Nombre, Norm, ArtistaId;
            internal bool Adelantada;
        }
        private bool rocolaOsMejoresOn;                                            // el prefijo esta puesto (vale tambien al ir y volver de una pared o disco del buscador)
        private Dictionary<Album, RocolaOsMejoresDe> rocolaOsMejoresDe;            // de cada disco «LAS MEJORES...», de quien es
        private Dictionary<string, SearchItem> rocolaOsMejoresTarjetas;            // por artista (sin acentos ni mayusculas): la misma tarjeta mientras no cambie
        private Dictionary<Track, Album> rocolaOsMejoresPropias;                   // tus canciones dentro de esos discos: su disco de verdad
        private static readonly HashSet<Album> RocolaOsMejoresAlbumes = new HashSet<Album>();
        private SearchItem rocolaOsMejoresMirada;                                  // la tarjeta en que se quedo la seleccion (el latido anterior)

        // ¿Va? El modo Spotify encendido con la llave de la API (sin ella no hay nada que pedir). Sin ROCOLA_OS, no (y no se crea nada)
        private bool RocolaOsMejoresVa { get { return RocolaOs.Activo && RocolaOsSpotifyCatalogoVa && RocolaOsSpotifyCatalogoApi.HayLlave; } }

        // ================================================================= el atajo =====================================================

        // g400 (BuscadorPortadasGpu.cs, ArmaAtajos, justo tras vaciar la lista): el atajo va el primero (los de XP y los del catalogo, detras), y
        // el prefijo de la casilla sigue lo que dice rocolaOsMejoresOn (asi no se pierde al ir y volver de una pared o de un disco del buscador)
        private void RocolaOsMejoresAtajo()
        {
            if (searchOverlay == null) return;
            bool va = RocolaOsMejoresVa;
            searchOverlay.RocolaOsHayChip = va;
            searchOverlay.RocolaOsAlPrefijo = RocolaOsMejoresCambio;
            if (!va) { rocolaOsMejoresOn = false; searchOverlay.Prefijo = null; return; }
            searchOverlay.Prefijo = rocolaOsMejoresOn ? RocolaOsMejoresPrefijoTexto : null;
            AgregaAtajo(RocolaOsAtajoMejores, rocolaOsMejoresOn ? "× Quitar «LAS MEJORES»" : "LAS MEJORES CANCIONES DE…", null);
        }
        // g401 (UsaAtajo): pulsaron el atajo: pone o quita el prefijo
        private void RocolaOsMejoresAlterna()
        {
            if (searchOverlay == null || !RocolaOsMejoresVa) return;
            rocolaOsMejoresOn = !rocolaOsMejoresOn;
            searchOverlay.Prefijo = rocolaOsMejoresOn ? RocolaOsMejoresPrefijoTexto : null;
            RocolaOs.Apunta("spotify_catalogo", rocolaOsMejoresOn ? "«LAS MEJORES CANCIONES DE»: el prefijo puesto" : "«LAS MEJORES CANCIONES DE»: el prefijo quitado (el atajo)");
            ArmaAtajos();                    // (la etiqueta del atajo)
            searchOverlay.RocolaOsRehaz();   // (la tarjeta entra o sale de los resultados)
            if (rocolaOsMejoresOn) searchOverlay.RocolaOsAlTeclado();
            else { searchOverlay.EnChips = true; searchOverlay.SelChip = 0; }
        }
        // REGRESAR con nada escrito quito el prefijo (lo hizo el buscador: RocolaOsQuitaPrefijo, g405)
        private void RocolaOsMejoresCambio(bool puesto)
        {
            rocolaOsMejoresOn = puesto;
            RocolaOs.Apunta("spotify_catalogo", "«LAS MEJORES CANCIONES DE»: el prefijo quitado (REGRESAR)");
            if (searchOpen) ArmaAtajos();
        }

        // el latido del catalogo (cada segundo, en la pantalla): (1) el prefijo no sobrevive al buscador: cerrado todo (ni buscador, ni pared o
        // disco del buscador a la vista) se quita; (2) la tarjeta en que se queda la seleccion pide ya su 1a pagina (una vez, solo lo que se mira)
        private void RocolaOsMejoresLatido(bool abierto)
        {
            if (rocolaOsMejoresOn && !abierto && rocolaOsSpPared == null && rocolaOsSpVueltaDisco == null)
            {
                rocolaOsMejoresOn = false;
                if (searchOverlay != null) searchOverlay.Prefijo = null;
                rocolaOsMejoresMirada = null;
                return;
            }
            SearchItem it = null;
            if (abierto && searchOverlay != null && searchOverlay.FocusResults && rocolaOsMejoresDe != null)
            {
                List<SearchItem> r = searchOverlay.Results;
                int i = searchOverlay.SelResult;
                if (r != null && i >= 0 && i < r.Count) it = r[i];
            }
            if (it == null || it.AlbumRef == null || !ReferenceEquals(it, rocolaOsMejoresMirada)) { rocolaOsMejoresMirada = it; return; }
            RocolaOsMejoresDe d;
            if (!rocolaOsMejoresDe.TryGetValue(it.AlbumRef, out d) || d.Adelantada || it.AlbumRef.Tracks.Count > 0) return;
            if (!RocolaOsMejoresVa || RocolaOsSpotifyCatalogoApi.Frena || RocolaOsSpotifyCatalogoApi.SinRed) return;
            d.Adelantada = true;
            if (RocolaOsSpotifyCatalogoApi.PaginaYa(RocolaOsSpotifyCatalogoApi.ParedCanciones, d.Nombre, 0) == null)
                RocolaOsSpotifyCatalogoApi.PidePagina(RocolaOsSpotifyCatalogoApi.ParedCanciones, d.Nombre, 0, null);
        }

        // ================================================================= la tarjeta ===================================================

        // (de RocolaOsSpotifyCatalogoVisto y RocolaOsSpotifyCatalogoPoneResultados) la lista del buscador con la tarjeta de PRIMERA, si hay prefijo
        // y artista escrito. Lista nueva: la de entrada no se toca. Sin prefijo, la misma lista.
        private List<SearchItem> RocolaOsMejoresPrimera(List<SearchItem> lista, string consulta)
        {
            if (lista == null || !rocolaOsMejoresOn || searchOverlay == null || searchOverlay.Prefijo == null || !RocolaOsMejoresVa) return lista;
            if (RocolaOsSpotifyCatalogoApi.Frena || RocolaOsSpotifyCatalogoApi.SinRed) return lista;
            string escrito = (consulta ?? "").Trim();
            if (escrito.Length < 2) return lista;
            SearchItem tarjeta = RocolaOsMejoresTarjeta(lista, escrito);
            List<SearchItem> m = new List<SearchItem>(lista.Count + 1);
            m.Add(tarjeta); m.AddRange(lista);
            return m;
        }
        // la tarjeta del artista que dan los resultados: el primero de tipo artista (tu tarjeta de XP si lo tienes, si no el de Spotify), o lo escrito
        private SearchItem RocolaOsMejoresTarjeta(List<SearchItem> lista, string escrito)
        {
            string nombre = null, id = null, url = null, propia = null;
            foreach (SearchItem it in lista)
            {
                if (it == null || it.Kind != 0 || string.IsNullOrEmpty(it.Display)) continue;
                nombre = it.Display;
                RocolaOsSpotifyCatalogoArtista ar; RocolaOsSpotifyParedQue q;
                if (it.AlbumRef != null && RocolaOsEsSpotify(it.AlbumRef) && rocolaOsSpDeArtista.TryGetValue(it.AlbumRef, out ar)) { id = ar.Id; url = ar.PortadaUrl; }
                else if ((q = RocolaOsSpotifyUnidaDe(it)) != null) { id = q.De; url = q.PortadaUrl; propia = it.AlbumRef != null ? it.AlbumRef.CoverPath : null; }
                else if (it.AlbumRef != null) propia = it.AlbumRef.CoverPath;
                break;
            }
            if (nombre == null) nombre = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(escrito.ToLowerInvariant());
            string norm = MotorBusqueda.Normaliza(nombre);
            // (revision 27-11-2026) un artista de solo simbolos («!!!») normaliza a nada: todas esas tarjetas compartian clave y abrian el artista equivocado; su
            // clave es entonces su nombre en minusculas (con un signo delante que no puede ser el de uno normalizado)
            if (norm.Length == 0) norm = "!" + nombre.Trim().ToLowerInvariant();
            if (rocolaOsMejoresTarjetas == null)
            {
                rocolaOsMejoresTarjetas = new Dictionary<string, SearchItem>(StringComparer.Ordinal);
                rocolaOsMejoresDe = new Dictionary<Album, RocolaOsMejoresDe>();
                rocolaOsMejoresPropias = new Dictionary<Track, Album>();
            }
            SearchItem t;
            if (rocolaOsMejoresTarjetas.TryGetValue(norm, out t))
            {
                Album ya = t.AlbumRef;
                if (string.IsNullOrEmpty(ya.CoverPath)) RocolaOsMejoresPortada(ya, id, url, propia);
                return t;
            }
            // (con la busqueda abierta no hay un disco de estos abierto: se puede soltar todo)
            if (rocolaOsMejoresTarjetas.Count >= RocolaOsMejoresTarjetasMax)
            { rocolaOsMejoresTarjetas.Clear(); rocolaOsMejoresDe.Clear(); rocolaOsMejoresPropias.Clear(); RocolaOsMejoresAlbumes.Clear(); }
            uint h = 2166136261; foreach (char ch in norm) { h ^= ch; h *= 16777619; }   // (FNV-1a: la carpeta sin palabras, XP adivina el genero por su nombre)
            Album a = new Album { Folder = RocolaOsSpotifyCarpeta + "mejores:" + h.ToString("x8", CultureInfo.InvariantCulture), Name = RocolaOsMejoresPrefijoTexto + " " + nombre.ToUpperInvariant(), Artist = nombre, Letter = RocolaOsSpotifyMarca };
            RocolaOsMejoresPortada(a, id, url, propia);
            RocolaOsMejoresAlbumes.Add(a);
            rocolaOsMejoresDe[a] = new RocolaOsMejoresDe { Nombre = nombre, Norm = norm, ArtistaId = id };
            t = new SearchItem { Kind = 1, Display = a.Name, Key = "", AlbumRef = a };
            rocolaOsMejoresTarjetas[norm] = t;
            return t;
        }
        // la clave de un artista (la de la tarjeta): sin acentos ni mayusculas; si no le queda nada (solo simbolos), su nombre en minusculas con «!» delante
        private static string RocolaOsMejoresClaveArtista(string artista)
        {
            string n = MotorBusqueda.Normaliza(artista ?? "");
            return n.Length > 0 ? n : "!" + (artista ?? "").Trim().ToLowerInvariant();
        }
        // su portada: la del artista en Spotify (si se conoce) o la de tu disco
        private void RocolaOsMejoresPortada(Album a, string id, string url, string propia)
        {
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(url)) { RocolaOsSpotifyCatalogoPortada(a, "artista-" + id, url); return; }
            if (!string.IsNullOrEmpty(propia)) { try { if (File.Exists(propia)) a.CoverPath = propia; } catch { } }
        }

        // ================================================================= elegirla ====================================================

        // (de RocolaOsSpotifyCatalogoElige, la primera linea) ELEGIR LA TARJETA: se piden las canciones del artista (3 peticiones como mucho) y,
        // al llegar, se abre el disco dentro del buscador. true = era la tarjeta
        private bool RocolaOsMejoresElige(SearchItem it)
        {
            RocolaOsMejoresDe d;
            if (it == null || it.AlbumRef == null || rocolaOsMejoresDe == null || !rocolaOsMejoresDe.TryGetValue(it.AlbumRef, out d)) return false;
            Album a = it.AlbumRef;
            if (!RocolaOsMejoresVa || !searchOpen) return true;
            MarkSearchActivity();
            if (a.Tracks.Count > 0) { RocolaOsMejoresEntra(a); return true; }   // (ya se armo: de memoria)
            int yo = ++rocolaOsSpAbriendo;
            RocolaOsMejoresPide(d, a, 0, new List<RocolaOsSpotifyCatalogoCancion>(), yo, searchOverlay.Escrito ?? "");
            return true;
        }
        private void RocolaOsMejoresPide(RocolaOsMejoresDe d, Album a, int desde, List<RocolaOsSpotifyCatalogoCancion> acum, int yo, string escritoAl)
        {
            Action<object, string> listo = delegate (object r, string error)
            {
                RocolaOs.Encarga("spotify_catalogo:mejores", delegate { RocolaOsMejoresLlego(d, a, desde, acum, yo, escritoAl, r as RocolaOsSpotifyCatalogoPagina, error); });
            };
            RocolaOsSpotifyCatalogoPagina ya = RocolaOsSpotifyCatalogoApi.PaginaYa(RocolaOsSpotifyCatalogoApi.ParedCanciones, d.Nombre, desde);
            if (ya != null) { listo(ya, null); return; }
            RocolaOsSpotifyCatalogoApi.PidePagina(RocolaOsSpotifyCatalogoApi.ParedCanciones, d.Nombre, desde, listo);
        }
        private void RocolaOsMejoresLlego(RocolaOsMejoresDe d, Album a, int desde, List<RocolaOsSpotifyCatalogoCancion> acum, int yo, string escritoAl, RocolaOsSpotifyCatalogoPagina pg, string error)
        {
            if (yo != rocolaOsSpAbriendo) return;   // (se eligio otra cosa mientras tanto)
            if (pg == null)
            {
                RocolaOsSpotifyCatalogoApi.ApuntaCallado("no llegaron las mejores canciones del artista: " + (error ?? "sin respuesta"));
                if (acum.Count == 0) { if (searchOpen) RocolaOsSpotifyCatalogoAviso("Espera un momento y vuelve a intentarlo"); return; }
            }
            else
            {
                acum.AddRange(pg.Canciones);
                int por = RocolaOsSpotifyCatalogoApi.PorPaginaPared, sig = desde + por, tope = Math.Min(pg.Total, RocolaOsMejoresPaginas * por);
                if (pg.Crudos >= por && sig < tope && !RocolaOsSpotifyCatalogoApi.Frena) { RocolaOsMejoresPide(d, a, sig, acum, yo, escritoAl); return; }
            }
            RocolaOsMejoresTermina(d, a, acum, escritoAl);
        }

        // la clave con que se comparan dos titulos: el titulo base y las marcas de version (las reglas de la sustitucion y de no repetir)
        private static string RocolaOsMejoresClave(string titulo)
        {
            string t = TituloSinPista(titulo ?? "");
            // (un MP3 sin titulo en las etiquetas se llama como su archivo: «04 Titulo», sin guion; el numero de pista no es del titulo)
            int i = 0;
            while (i < t.Length && i < 4 && t[i] >= '0' && t[i] <= '9') i++;
            if (i > 0 && i < 4 && i < t.Length && t[i] == ' ' && t.Substring(i).Trim().Length > 0) t = t.Substring(i).Trim();
            return RocolaOsCalidad.TituloBase(t) + "|" + RocolaOsCalidad.MarcasDe(t);
        }
        // arma el disco: tus canciones de ese artista que esten entre las de Spotify, primero (en el orden de Spotify), y despues las demas de Spotify
        private void RocolaOsMejoresTermina(RocolaOsMejoresDe d, Album a, List<RocolaOsSpotifyCatalogoCancion> acum, string escritoAl)
        {
            // la misma cancion en otra edicion del disco no entra otra vez
            List<RocolaOsSpotifyCatalogoCancion> unicas = new List<RocolaOsSpotifyCatalogoCancion>();
            HashSet<string> vistas = new HashSet<string>(StringComparer.Ordinal);
            foreach (RocolaOsSpotifyCatalogoCancion c in acum)
                if (c != null && !string.IsNullOrEmpty(c.Uri) && vistas.Add(RocolaOsMejoresClave(c.Titulo))) unicas.Add(c);
            if (unicas.Count == 0)
            {
                RocolaOs.Apunta("spotify_catalogo", "«LAS MEJORES CANCIONES DE " + d.Nombre + "»: Spotify no dio canciones");
                if (searchOpen) RocolaOsSpotifyCatalogoAviso("Spotify no tiene canciones de " + d.Nombre);
                return;
            }
            // tus canciones de ese artista (por el artista de tus discos), por esa clave
            Dictionary<string, List<KeyValuePair<Track, Album>>> tuyas = new Dictionary<string, List<KeyValuePair<Track, Album>>>(StringComparer.Ordinal);
            if (!libBuilding)
                foreach (Album x in albums)
                {
                    if (x == null || x.IsTop || RocolaOsEsSpotify(x) || RocolaOsMejoresClaveArtista(x.Artist) != d.Norm) continue;
                    foreach (Track t in x.Tracks)
                    {
                        if (t == null || string.IsNullOrEmpty(t.Path)) continue;
                        List<KeyValuePair<Track, Album>> l; string k = RocolaOsMejoresClave(t.Title);
                        if (!tuyas.TryGetValue(k, out l)) { l = new List<KeyValuePair<Track, Album>>(); tuyas[k] = l; }
                        l.Add(new KeyValuePair<Track, Album>(t, x));
                    }
                }
            List<KeyValuePair<Track, Album>> primero = new List<KeyValuePair<Track, Album>>();
            List<RocolaOsSpotifyCatalogoCancion> despues = new List<RocolaOsSpotifyCatalogoCancion>();
            HashSet<Track> usadas = new HashSet<Track>();
            foreach (RocolaOsSpotifyCatalogoCancion c in unicas)
            {
                List<KeyValuePair<Track, Album>> l; bool hay = false;
                if (tuyas.TryGetValue(RocolaOsMejoresClave(c.Titulo), out l))
                    foreach (KeyValuePair<Track, Album> p in l)
                    {
                        if (usadas.Contains(p.Key)) continue;
                        // la misma duracion (con holgura) si se sabe de las dos: otra grabacion del mismo titulo no es esta
                        int dl = (int)Math.Round(SegundosDe(p.Key.Path) * 1000.0);
                        if (c.DuracionMs > 0 && dl > 0 && Math.Abs(c.DuracionMs - dl) > RocolaOsMejoresToleranciaMs) continue;
                        primero.Add(p); usadas.Add(p.Key); hay = true;
                        break;
                    }
                if (!hay) despues.Add(c);
            }
            a.Tracks.Clear();
            foreach (KeyValuePair<Track, Album> p in primero) { a.Tracks.Add(p.Key); rocolaOsMejoresPropias[p.Key] = p.Value; }
            foreach (RocolaOsSpotifyCatalogoCancion c in despues)
            {
                // «spotify:track:ID#mejores»: su Uri de I6 no cambia (PONER la pide igual); la ruta propia dice su lugar en la lista («NN Titulo»)
                string ruta = c.Uri + "#mejores";
                rocolaOsSpCanciones[ruta] = c;
                lock (RocolaOsSpotifyCatalogoNombres.PorRuta) RocolaOsSpotifyCatalogoNombres.PorRuta[ruta] = (a.Tracks.Count + 1).ToString("00", CultureInfo.InvariantCulture) + " " + c.Titulo;
                a.Tracks.Add(new Track { Path = ruta, Artist = c.Artista ?? "", Title = c.Titulo ?? "", Album = c.Disco ?? "", Genre = "" });
            }
            generoVer++; duracionesVer++;   // (la ficha y el letrero de XP se rehacen con lo que ya vigilan)
            RocolaOs.Apunta("spotify_catalogo", "«LAS MEJORES CANCIONES DE " + d.Nombre + "»: " + a.Tracks.Count.ToString(CultureInfo.InvariantCulture) + " canciones (" + primero.Count.ToString(CultureInfo.InvariantCulture) + " tuyas primero, de " + tuyas.Count.ToString(CultureInfo.InvariantCulture) + " titulos tuyos de ese artista: " + string.Join("; ", new List<string>(tuyas.Keys).GetRange(0, Math.Min(4, tuyas.Count))) + ")");
            // solo si el buscador sigue con lo mismo escrito (si no, el disco se abria encima de otra busqueda)
            if (!searchOpen || (searchOverlay.Escrito ?? "") != escritoAl) return;
            RocolaOsMejoresEntra(a);
        }
        // abrir el disco dentro del buscador: REGRESAR vuelve al buscador con lo escrito y la misma eleccion (g54); 30 s sin tocar cierra todo
        private void RocolaOsMejoresEntra(Album a)
        {
            RocolaOsSpotifyVuelta v = RocolaOsSpotifyGuardaBuscador();
            transNextFrom = SearchCubeRect(out transNextAngle);   // la portada sale del CUBO GRANDE del buscador (luego ya no existe)
            CloseSearch(); NavigateToAlbum(a); EnterAlbum(a);
            rocolaOsSpVueltaDisco = v; rocolaOsSpVueltaAlbum = a;   // (despues de EnterAlbum: g49 la borra al entrar a otro disco)
        }

        // g407 (AddSong): una cancion tuya dentro de estos discos se pide con su disco de verdad (la portada, el genero, lo de «sonando» y lo
        // mas pedido son de el, no de la lista). Las de Spotify y las de los demas discos, el disco que dan.
        private Album RocolaOsMejoresDisco(Track t, Album disc)
        {
            Album real;
            return t != null && rocolaOsMejoresPropias != null && rocolaOsMejoresPropias.TryGetValue(t, out real) ? real : disc;
        }
    }
}
